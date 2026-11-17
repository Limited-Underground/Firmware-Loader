#requires -Version 7.5
[CmdletBinding()]
param([ValidateNotNullOrEmpty()][string] $EvidenceRoot)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'LoaderValidationEvidence.ps1')
$root = New-LoaderValidationRun $EvidenceRoot 'RootTest'
$callerTemp = Join-Path $root 'caller-temp'
[IO.Directory]::CreateDirectory($callerTemp) | Out-Null
$requested = Join-Path $root 'selected'
$variables = @{
    TEMP = $callerTemp
    TMP = $callerTemp
    TMPDIR = $callerTemp
    LUF_EVIDENCE_ROOT = (Join-Path $callerTemp 'unused-root')
}
$original = @{}
foreach ($name in $variables.Keys) {
    $original[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

$watcher = [IO.FileSystemWatcher]::new($callerTemp)
$watcher.IncludeSubdirectories = $true
$ids = @()
try {
    foreach ($eventName in @('Changed','Created','Deleted','Renamed','Error')) {
        $id = "LoaderEvidence.$eventName.$([Guid]::NewGuid().ToString('N'))"
        $null = Register-ObjectEvent -InputObject $watcher -EventName $eventName -SourceIdentifier $id
        $ids += $id
    }
    $watcher.EnableRaisingEvents = $true
    foreach ($name in $variables.Keys) {
        [Environment]::SetEnvironmentVariable($name, $variables[$name], 'Process')
    }

    & (Join-Path $PSScriptRoot 'Test-Loader.ps1') -EvidenceRoot $requested

    foreach ($name in $variables.Keys) {
        if ([Environment]::GetEnvironmentVariable($name, 'Process') -cne $variables[$name]) {
            throw "Full validation did not restore $name."
        }
    }
    $runs = @(Get-ChildItem -LiteralPath $requested -Directory)
    if ($runs.Count -ne 1 -or $runs[0].Name -cnotmatch '^LimitedUnderground\.Loader\.Validation\.[0-9]{8}T[0-9]{9}Z\.[0-9a-f]{32}$') {
        throw 'Expected one dated full validation run under the requested evidence root.'
    }
    $final = Get-Content -LiteralPath (Join-Path $runs[0].FullName 'final.json') -Raw | ConvertFrom-Json
    if ($final.run_id -cne $runs[0].Name -or $final.status -cne 'passed' -or $final.suite -cne 'full' -or
        -not $final.environment_restored -or -not (Test-Path -LiteralPath (Join-Path $runs[0].FullName 'tmp') -PathType Container)) {
        throw 'Expected a successful full run with isolated scratch and restored environment.'
    }
    $events = @(Get-Event | Where-Object SourceIdentifier -In $ids)
    if ($events.Count -ne 0 -or @(Get-ChildItem -LiteralPath $callerTemp -Force).Count -ne 0) {
        throw 'Full validation wrote to the caller temporary directory.'
    }
    Write-Host 'PASS: explicit Loader evidence root, dated full run, environment restoration, and no writes to caller TEMP/TMP.'
} finally {
    $watcher.EnableRaisingEvents = $false
    foreach ($id in $ids) {
        Unregister-Event -SourceIdentifier $id
        Get-Event -SourceIdentifier $id -ErrorAction SilentlyContinue | Remove-Event
    }
    $watcher.Dispose()
    foreach ($name in $original.Keys) {
        if ($null -eq $original[$name]) { Remove-Item -LiteralPath ('Env:' + $name) -ErrorAction SilentlyContinue }
        else { [Environment]::SetEnvironmentVariable($name, $original[$name], 'Process') }
    }
}
