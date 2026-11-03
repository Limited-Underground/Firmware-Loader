[CmdletBinding()]
param([string]$Root = (Split-Path -Parent $PSScriptRoot))
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Test-LoaderDocumentation.ps1') -Root $Root -DefinitionsOnly

$baseline=Get-LoaderDocumentationReport -Root $Root
if ($baseline.Issues.Count) { throw 'Negative fixtures require a passing real-document baseline.' }
$passed=0
function Assert-Refused([string]$Name,[hashtable]$Changes,[string]$ExpectedCode) {
    $report=Get-LoaderDocumentationReport -Root $Root -Overrides $Changes
    if ($ExpectedCode -cnotin @($report.Issues | ForEach-Object Code)) {
        throw "Documentation negative fixture did not detect $Name ($ExpectedCode)."
    }
    $script:passed++
}
function Read-Source([string]$Relative) { return [IO.File]::ReadAllText((Join-Path $Root $Relative)) }

Assert-Refused 'missing required current page' @{'docs/TESTING.md'=$null} 'Input'
Assert-Refused 'broken current local reference' @{'README.md'=(Read-Source 'README.md')+"`n[Required](docs/not-a-real-document.md)`n"} 'Reference'
Assert-Refused 'missing heading anchor' @{'README.md'=(Read-Source 'README.md')+"`n[Required](docs/TESTING.md#no-such-heading)`n"} 'Reference'
Assert-Refused 'reference escapes selected repository' @{'README.md'=(Read-Source 'README.md')+"`n[Required](../../outside.md)`n"} 'Reference'
Assert-Refused 'protected local payload reference' @{'README.md'=(Read-Source 'README.md')+"`n[Forbidden](.private/fixture.bin)`n"} 'Reference'
Assert-Refused 'current baseline differs from owning record' @{'README.md'=(Read-Source 'README.md')+"`nImplementation baseline: ``1111111111111111111111111111111111111111``.`n"} 'SourceIdentity'
$recovery=Read-Source 'docs/RECOVERY.md'
$wrongCheckout=[regex]::Replace($recovery,'checkout --detach [0-9a-f]{40}','checkout --detach 2222222222222222222222222222222222222222')
Assert-Refused 'reconstruction command selects a different revision' @{'docs/RECOVERY.md'=$wrongCheckout} 'SourceIdentity'
$sdk=Read-Source 'global.json'
$sdkConfig=$sdk | ConvertFrom-Json
$wrongSdk=$sdk.Replace($sdkConfig.sdk.version,'99.0.100')
Assert-Refused 'SDK source configuration changed without documentation' @{'global.json'=$wrongSdk} 'SdkIdentity'
$workflow=Read-Source '.github/workflows/loader-host.yml'
$wrongWorkflow=[regex]::Replace($workflow,'dotnet-version:\s*''[0-9.]+''',"dotnet-version: '99.0.400'")
Assert-Refused 'hosted SDK differs from owning declaration' @{'.github/workflows/loader-host.yml'=$wrongWorkflow} 'SdkIdentity'
Assert-Refused 'malformed source configuration' @{'global.json'='{invalid'} 'Configuration'
$project='src/LimitedUnderground.FirmwareLoader/LimitedUnderground.FirmwareLoader.csproj'
$wrongRuntime=[regex]::Replace((Read-Source $project),'<TargetFramework>[^<]+</TargetFramework>','<TargetFramework>net99.0-windows</TargetFramework>')
Assert-Refused 'application/test/runtime claim divergence' @{$project=$wrongRuntime} 'FrameworkIdentity'
$target=Read-Source 'src/LimitedUnderground.FirmwareLoader/OpenTrailInspectionProvider.cs'
$targetHash=[regex]::Match($target,'TargetContractSha256\s*=\s*"([0-9a-f]{64})"').Groups[1].Value
Assert-Refused 'documented target bytes differ from embedded source' @{'docs/RECOVERY.md'=$recovery.Replace($targetHash,('0'*64))} 'ArtifactIdentity'
$guide=Read-Source 'docs/architecture/README.md'
$wrongModel=[regex]::Replace($guide,'(DSL SHA-256[^*\r\n]*\*\*)[0-9a-f]{64}',('${1}'+('0'*64)))
Assert-Refused 'recorded model bytes differ from selected DSL' @{'docs/architecture/README.md'=$wrongModel} 'ArtifactIdentity'
Assert-Refused 'stale development-resumption permission' @{'README.md'=(Read-Source 'README.md')+"`nOrdinary development may resume.`n"} 'Authority'
Assert-Refused 'production installation claim' @{'README.md'=(Read-Source 'README.md')+"`nProduction installation is enabled.`n"} 'Authority'
Assert-Refused 'host result promoted to physical acceptance' @{'README.md'=(Read-Source 'README.md')+"`nHost validation establishes physical acceptance.`n"} 'Authority'
Assert-Refused 'retired publication cadence reinstated' @{'README.md'=(Read-Source 'README.md')+"`nDaily CI batching is required.`n"} 'Authority'
Assert-Refused 'transient publication snapshot as next action' @{'README.md'=(Read-Source 'README.md')+"`nNext safe action: push draft PR #999.`n"} 'TransientNextAction'

Assert-Refused 'owning reading-route selection missing' @{'docs/FOUNDATION.md'=(Read-Source 'docs/FOUNDATION.md').Replace('## Reading route and reusable procedures','## Removed route')} 'ReadingRoute'
$packageProject=(Read-Source $project).Replace('</Project>','<ItemGroup><PackageReference Include="Synthetic.Package" Version="1.0.0" /></ItemGroup></Project>')
Assert-Refused 'package declaration contradicts documented package-free build' @{$project=$packageProject} 'PackageIdentity'
$feed=(Read-Source 'NuGet.Config').Replace('</packageSources>','<add key="fixture" value="https://example.invalid/fixture" /></packageSources>')
Assert-Refused 'feed declaration contradicts documented offline policy' @{'NuGet.Config'=$feed} 'PackageIdentity'
# Controls show selection is semantic: history is retained, dates are not proof of freshness.
$history=Get-LoaderDocumentationReport -Root $Root -Overrides @{'README.md'=(Read-Source 'README.md')+"`n## Retained history`nOrdinary development may resume.`n"}
if ($history.Issues.Count) { throw 'Explicit historical content was incorrectly treated as current permission.' }
$dated=Get-LoaderDocumentationReport -Root $Root -Overrides @{'README.md'=(Read-Source 'README.md').Replace('November 2, 2026','January 1, 2000')}
if ($dated.Issues.Count) { throw 'Timestamp change alone was incorrectly treated as semantic drift.' }
$lineEndings=Get-LoaderDocumentationReport -Root $Root -Overrides @{'docs/architecture/workspace.dsl'=(Read-Source 'docs/architecture/workspace.dsl').Replace("`r`n","`n").Replace("`n","`r`n")}
if ($lineEndings.Issues.Count) { throw 'Checkout newline conversion was incorrectly treated as changed model content.' }
Write-Host "Loader documentation fixtures passed: $passed negative cases and 3 selection controls; no repository fixture files were changed."
