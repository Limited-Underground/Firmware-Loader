[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$validationParent = [System.IO.Path]::GetFullPath($env:TEMP).TrimEnd('\', '/')
$validationRoot = [System.IO.Path]::GetFullPath((Join-Path $validationParent ("LimitedUnderground.Loader.Validation." + [Guid]::NewGuid().ToString('N'))))
$appProject = Join-Path $projectRoot 'src\LimitedUnderground.FirmwareLoader\LimitedUnderground.FirmwareLoader.csproj'
$testProject = Join-Path $projectRoot 'tests\LimitedUnderground.FirmwareLoader.Tests\LimitedUnderground.FirmwareLoader.Tests.csproj'
$dotnetHost = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source

# The repository targets .NET 8 and clears NuGet sources. A newer default SDK
# can request reference packs unavailable offline. Use an installed stable 8.0
# SDK explicitly without installing anything or changing global/repo settings.
$sdkInventory = & $dotnetHost --list-sdks
if ($LASTEXITCODE -ne 0) { throw 'Unable to enumerate installed .NET SDKs.' }
$sdk = $sdkInventory | ForEach-Object {
    if ($_ -match '^(8\.0\.\d+) \[(.+)\]$') {
        [pscustomobject]@{ Version = [version]$Matches[1]; Root = $Matches[2] }
    }
} | Sort-Object Version -Descending | Select-Object -First 1
if ($null -eq $sdk) { throw 'Offline Loader validation requires an installed stable .NET 8.0 SDK.' }
$sdkCli = Join-Path (Join-Path $sdk.Root $sdk.Version.ToString()) 'dotnet.dll'
if (-not (Test-Path -LiteralPath $sdkCli -PathType Leaf)) { throw 'Selected .NET 8 SDK CLI is unavailable.' }
Write-Host ("Loader validation SDK: " + $sdk.Version)

try {
    New-Item -ItemType Directory -Force -Path $validationRoot | Out-Null
    $env:NUGET_PACKAGES = Join-Path $validationRoot 'packages'
    $env:DOTNET_CLI_HOME = Join-Path $validationRoot 'dotnet-home'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:DOTNET_NOLOGO = '1'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'

    & $dotnetHost $sdkCli build $appProject --configuration Release --artifacts-path (Join-Path $validationRoot 'app-artifacts')
    if ($LASTEXITCODE -ne 0) { throw "Application build failed with exit code $LASTEXITCODE." }

    $testArtifacts = Join-Path $validationRoot 'test-artifacts'
    & $dotnetHost $sdkCli build $testProject --configuration Release --artifacts-path $testArtifacts
    if ($LASTEXITCODE -ne 0) { throw "Test build failed with exit code $LASTEXITCODE." }

    # .NET 8 dotnet run can look in the default bin directory despite a custom
    # artifacts path. Execute the exact DLL that the build above produced.
    $testAssembly = Join-Path $testArtifacts 'bin\LimitedUnderground.FirmwareLoader.Tests\release\LimitedUnderground.FirmwareLoader.Tests.dll'
    if (-not (Test-Path -LiteralPath $testAssembly -PathType Leaf)) { throw 'Built test assembly was not found.' }
    & $dotnetHost $testAssembly $projectRoot
    if ($LASTEXITCODE -ne 0) { throw "Foundation tests failed with exit code $LASTEXITCODE." }
}
finally {
    if (Test-Path -LiteralPath $validationRoot) {
        $resolvedValidation = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $validationRoot).Path)
        if (-not $resolvedValidation.StartsWith($validationParent + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -or
            [System.IO.Path]::GetFileName($resolvedValidation) -notmatch '^LimitedUnderground\.Loader\.Validation\.[0-9a-f]{32}$') {
            throw 'Refusing cleanup outside the exact temporary Loader validation directory.'
        }
        Remove-Item -LiteralPath $resolvedValidation -Recurse -Force
    }
}

Write-Host 'Shared firmware loader offline validation passed.'
