[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Only fixed enums, booleans and constrained version strings leave the runner.
# Receipt error messages, source paths, raw logs and images are never published.
function Get-LoaderCiReceiptResult([string]$RunRoot) {
    $result = [ordered]@{ status='failed'; reason='receipt-invalid'; sdk=$null; powershell=$null;
        environment_restored=$false; stages=[ordered]@{} }
    try {
        $started = Get-Content -LiteralPath (Join-Path $RunRoot 'started.json') -Raw | ConvertFrom-Json
        $final = Get-Content -LiteralPath (Join-Path $RunRoot 'final.json') -Raw | ConvertFrom-Json
        $ownership = Get-Content -LiteralPath (Join-Path $RunRoot 'ownership.json') -Raw | ConvertFrom-Json
        $id = Split-Path -Leaf $RunRoot
        if ($id -cnotmatch '^LimitedUnderground\.Loader\.Validation\.[0-9a-f]{32}$' -or
            $started.run_id -cne $id -or $final.run_id -cne $id -or $ownership.run_id -cne $id -or
            $started.schema -ne 1 -or $final.schema -ne 1 -or
            $started.suite -cne 'full' -or $final.suite -cne 'full' -or
            $started.status -cne 'started' -or $started.ui_selector -ceq '1' -or $started.store_selector -ceq '1') {
            return $result
        }
        if ($started.sdk -cnotmatch '^8\.0\.[0-9]+$' -or $started.powershell -cnotmatch '^[0-9]+\.[0-9]+\.[0-9]+$') { return $result }
        $result.sdk = $started.sdk; $result.powershell = $started.powershell
        $result.environment_restored = $final.environment_restored -is [bool] -and $final.environment_restored
        $allPassed = $true
        foreach ($name in @('app-build','test-build','tests')) {
            $stage = Get-Content -LiteralPath (Join-Path $RunRoot "stage-$name.json") -Raw | ConvertFrom-Json
            $passed = $stage.stage -ceq $name -and $stage.status -ceq 'passed' -and
                $stage.native_exit_code -is [long] -and $stage.native_exit_code -eq 0 -and
                $null -eq $stage.failure_kind -and @($stage.secondary_errors).Count -eq 0
            $result.stages[$name] = $(if ($passed) { 'passed' } else { 'failed' })
            $allPassed = $allPassed -and $passed
        }
        if ($allPassed -and $final.status -ceq 'passed' -and $result.environment_restored -and
            $final.artifacts_retained -is [bool] -and $final.artifacts_retained -and
            $null -eq $final.failure_kind -and $null -eq $final.error) {
            $result.status='passed'; $result.reason='full-host-gate-passed'
        } else { $result.reason='host-gate-or-finalization-failed' }
    } catch { $result.reason='receipt-invalid' }
    return $result
}

# This adapter is for disposable hosted jobs only, never a local recovery run.
if ($env:GITHUB_ACTIONS -cne 'true' -or $env:RUNNER_ENVIRONMENT -cne 'github-hosted' -or -not $IsWindows) {
    throw 'This entrypoint requires a GitHub-hosted Windows job.'
}
$publicRoot = Join-Path $env:RUNNER_TEMP 'loader-ci-public'
$privateRoot = Join-Path $env:RUNNER_TEMP 'loader-ci-private'
if ((Test-Path -LiteralPath $publicRoot) -or (Test-Path -LiteralPath $privateRoot)) { throw 'Existing CI output refused.' }
[IO.Directory]::CreateDirectory($publicRoot) | Out-Null
[IO.Directory]::CreateDirectory($privateRoot) | Out-Null
$summaryPath = Join-Path $publicRoot 'result.json'
$summary = [ordered]@{ schema=1; layer='host-only'; status='incomplete'; reason='interrupted-or-not-finalized';
    source_commit=$null; result=$null; raw_diagnostics_published=$false }
if ($env:GITHUB_SHA -cmatch '^[0-9a-f]{40}$') { $summary.source_commit=$env:GITHUB_SHA }
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding utf8
$oldTemp=$env:TEMP; $oldTmp=$env:TMP
try {
    $env:TEMP=$privateRoot; $env:TMP=$privateRoot
    $wrapperPassed=$false
    try {
        & (Join-Path $PSScriptRoot 'Test-Loader.ps1') *> (Join-Path $privateRoot 'wrapper.log')
        $wrapperPassed=$true
    } catch { $summary.reason='wrapper-failed' }
    $runs=@(Get-ChildItem -LiteralPath $privateRoot -Directory -Filter 'LimitedUnderground.Loader.Validation.*')
    if ($runs.Count -eq 1) {
        $summary.result=Get-LoaderCiReceiptResult $runs[0].FullName
        if ($wrapperPassed -and $summary.result.status -ceq 'passed') {
            $summary.status='passed'; $summary.reason='full-host-gate-passed'
        } else { $summary.status='failed'; $summary.reason='wrapper-or-receipts-failed' }
    } else { $summary.status='failed'; $summary.reason='run-allocation-incomplete' }
} catch { $summary.status='failed'; $summary.reason='ci-adapter-failed' }
finally {
    $env:TEMP=$oldTemp; $env:TMP=$oldTmp
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding utf8
}
Write-Host ('Loader host gate: '+$summary.status+' ('+$summary.reason+').')
if ($summary.status -cne 'passed') { exit 1 }
