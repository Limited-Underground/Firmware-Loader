#requires -Version 7.5
[CmdletBinding()]
param([string] $EvidenceRoot)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'LoaderValidationEvidence.ps1')
$evidence = New-LoaderValidationRun $EvidenceRoot 'LimitedUnderground.Loader.Installer'
$root = Split-Path -Parent $PSScriptRoot
$workflowProject = Join-Path $PSScriptRoot 'LoaderInstaller\LoaderInstaller.csproj'
if (!(Test-Path -LiteralPath $workflowProject -PathType Leaf)) {
    $red = 'FAIL: installer workflow entry point is required to exercise administrator, payload and setup boundaries.'
    $red | Tee-Object -FilePath (Join-Path $evidence 'results.log') | Write-Host
    throw 'The installer workflow is missing.'
}

$fixtureProject = Join-Path $root 'tests\LoaderInstaller.Tests\LoaderInstaller.Tests.csproj'
$dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$artifacts = Join-Path $evidence 'artifacts'
$scratch = Join-Path $evidence 'scratch'
[IO.Directory]::CreateDirectory($scratch) | Out-Null
$settings = @{
    TEMP = $scratch
    TMP = $scratch
    TMPDIR = $scratch
    DOTNET_CLI_HOME = Join-Path $evidence 'dotnet'
    NUGET_PACKAGES = Join-Path $evidence 'packages'
    DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    DOTNET_NOLOGO = '1'
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
}
$prior = @{}
try {
    foreach ($name in $settings.Keys) {
        $prior[$name] = [Environment]::GetEnvironmentVariable($name)
        [Environment]::SetEnvironmentVariable($name, $settings[$name])
    }
    & $dotnet build $fixtureProject -c Release --artifacts-path $artifacts --disable-build-servers *>&1 |
        Tee-Object -FilePath (Join-Path $evidence 'build.log')
    if ($LASTEXITCODE -ne 0) { throw "Installer fixture build failed (exit $LASTEXITCODE)." }
    $fixture = @(Get-ChildItem -LiteralPath (Join-Path $artifacts 'bin\LoaderInstaller.Tests') -Recurse -File -Filter 'LoaderInstaller.Tests.dll')
    if ($fixture.Count -ne 1) { throw 'Expected exactly one built installer fixture harness.' }
    & $dotnet $fixture[0].FullName $evidence *>&1 | Tee-Object -FilePath (Join-Path $evidence 'results.log')
    if ($LASTEXITCODE -ne 0) { throw "Installer fixture checks failed (exit $LASTEXITCODE)." }
}
finally {
    foreach ($name in $prior.Keys) {
        if ($null -eq $prior[$name]) { Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue }
        else { [Environment]::SetEnvironmentVariable($name, $prior[$name]) }
    }
}
