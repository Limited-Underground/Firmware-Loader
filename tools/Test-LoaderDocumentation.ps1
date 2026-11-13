[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [switch]$DefinitionsOnly
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Offline consistency checks, not task authorization or semantic/physical acceptance.
# Overrides are an in-memory negative-fixture seam, not a command-line bypass.
function Get-LoaderDocumentationReport([string]$Root, [hashtable]$Overrides = @{}) {
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $prefix = $rootPath + [IO.Path]::DirectorySeparatorChar
    $issues = [Collections.Generic.List[object]]::new()
    $cache = @{}
    function Add-Issue([string]$Code, [string]$Path, [string]$Message) {
        $issues.Add([pscustomobject]@{ Code=$Code; Path=$Path; Message=$Message })
    }
    function Get-PlainPath([string]$Relative) {
        if ($Relative.Replace('\','/') -match '(^|/)\.(private|git)(/|$)') { throw 'Protected holdings are not documentation inputs' }
        $full=[IO.Path]::GetFullPath((Join-Path $rootPath $Relative))
        if (-not $full.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Outside root' }
        $part=[IO.FileInfo]::new($full)
        while ($null -ne $part -and $part.FullName.Length -ge $rootPath.Length) {
            if ($part.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked input' }
            $part=if ($part -is [IO.FileInfo]) { $part.Directory } else { $part.Parent }
        }
        if (-not [IO.File]::Exists($full)) { throw 'Missing file' }
        return $full
    }
    function Get-Text([string]$Relative) {
        $Relative=$Relative.Replace('\','/')
        if ($cache.ContainsKey($Relative)) { return $cache[$Relative] }
        try {
            $full=Get-PlainPath $Relative
            if ($Overrides.ContainsKey($Relative)) {
                if ($null -eq $Overrides[$Relative]) { throw 'Missing fixture input' }
                $value=[string]$Overrides[$Relative]
            } else { $value=[IO.File]::ReadAllText($full) }
        } catch {
            Add-Issue 'Input' $Relative 'Required plain repository file is unavailable.'
            $value=''
        }
        $cache[$Relative]=$value
        return $value
    }
    function Get-Current([string]$Text) {
        # Explicit selection, not a date heuristic. Historical bodies remain untouched.
        return ([regex]::Split($Text, '(?m)^## Retained history\s*$'))[0]
    }
    function Get-Links([string]$Text) {
        return [regex]::Matches($Text, '\[[^\]]*\]\(([^)]+)\)')
    }
    function Resolve-Reference([string]$From, [string]$Link) {
        $pieces = $Link -split '#', 2
        $path = [Uri]::UnescapeDataString($pieces[0])
        $full = if ($path -eq '') { Join-Path $rootPath $From } else {
            [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent (Join-Path $rootPath $From)) $path))
        }
        if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Outside root' }
        return [pscustomobject]@{ Path=[IO.Path]::GetRelativePath($rootPath,$full).Replace('\','/');
            Anchor=$(if ($pieces.Count -eq 2) { [Uri]::UnescapeDataString($pieces[1]) } else { '' }) }
    }
    function Get-Anchors([string]$Text) {
        $seen = @{}
        foreach ($h in [regex]::Matches($Text, '(?m)^#{1,6}\s+(.+?)\s*#*\s*$')) {
            $slug = [regex]::Replace($h.Groups[1].Value.ToLowerInvariant(), '[^\p{L}\p{Nd}\-_\s]', '')
            $slug = [regex]::Replace($slug, '\s', '-')
            if ($seen.ContainsKey($slug)) { $seen[$slug]++; "$slug-$($seen[$slug])" }
            else { $seen[$slug]=0; $slug }
        }
    }
    function Get-Row([string]$Text, [string]$Name) {
        $match = [regex]::Match($Text, '(?m)^\| '+[regex]::Escape($Name)+' \| (.+) \|\s*$')
        if (-not $match.Success) { Add-Issue 'Identity' 'docs/RECOVERY.md' "Missing owning row: $Name" }
        return $match.Groups[1].Value
    }

    $currentDocs = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($seed in @('README.md','CONTRIBUTING.md','AGENTS.md','docs/FOUNDATION.md')) { $null=$currentDocs.Add($seed) }
    $foundation = Get-Current (Get-Text 'docs/FOUNDATION.md')
    $route = [regex]::Match($foundation, '(?ms)^## Reading route and reusable procedures\s*\n(.+?)(?=^## |\z)')
    if (-not $route.Success) { Add-Issue 'ReadingRoute' 'docs/FOUNDATION.md' 'Current reading-route selection is absent.' }
    foreach ($link in (Get-Links $route.Groups[1].Value)) {
        $url = $link.Groups[1].Value
        if ($url -match '^https?://') { continue }
        try {
            $reference = Resolve-Reference 'docs/FOUNDATION.md' $url
            if ($reference.Path.EndsWith('.md',[StringComparison]::OrdinalIgnoreCase)) { $null=$currentDocs.Add($reference.Path) }
        } catch { Add-Issue 'Reference' 'docs/FOUNDATION.md' 'Reading route escapes the repository or has an invalid reference.' }
    }
    $references=0; $anchors=0
    $contradictions = @(
        'ordinary (?:host-only )?development (?:may|can) resume',
        'production (?:firmware )?(?:installation|writing|admission) (?:is|are) (?:enabled|available|accepted)',
        'host (?:validation|tests?|success|build)(?: success)? (?:proves|establishes|equals|grants) (?:physical|device|production|release) acceptance',
        'documentation (?:alone )?(?:authorizes|grants) (?:publication|merge|hardware|installation)',
        'daily CI batching (?:is|remains) (?:required|retained)',
        'at most one PR per (?:repository|repo) per day'
    )
    foreach ($doc in ($currentDocs | Sort-Object)) {
        $text = Get-Current (Get-Text $doc)
        foreach ($pattern in $contradictions) {
            if ($text -match $pattern) { Add-Issue 'Authority' $doc 'Known freeze, inspection-only or publication contradiction in current text.' }
        }
        if ($text -match '(?im)next(?: safe| authorized)? (?:action|step)[^\r\n]{0,500}(?:draft PR|PR #\d+|handbook\d+)') {
            Add-Issue 'TransientNextAction' $doc 'Current next action depends on a transient PR/publication snapshot.'
        }
        foreach ($link in (Get-Links $text)) {
            $url=$link.Groups[1].Value
            if ($url -match '^https?://') { continue } # No network or external freshness claim.
            try {
                $reference=Resolve-Reference $doc $url
                $null=Get-PlainPath $reference.Path
                if ($Overrides.ContainsKey($reference.Path) -and $null -eq $Overrides[$reference.Path]) { throw 'Unavailable reference' }
                $references++
                if ($reference.Anchor) {
                    $anchors++
                    if (-not $reference.Path.EndsWith('.md',[StringComparison]::OrdinalIgnoreCase)) { throw 'Only Markdown heading anchors are checked' }
                    if ($reference.Anchor -cnotin @(Get-Anchors (Get-Text $reference.Path))) { throw 'Missing heading' }
                }
            } catch { Add-Issue 'Reference' $doc "Missing, escaping or invalid local reference: $url" }
        }
    }
    $recovery=Get-Current (Get-Text 'docs/RECOVERY.md')
    $sourceRow=Get-Row $recovery 'Source/history'
    $baseline=[regex]::Match($sourceRow,'\b[0-9a-f]{40}\b').Value
    if (-not $baseline) { Add-Issue 'SourceIdentity' 'docs/RECOVERY.md' 'Owning source baseline must be one full Git commit identity.' }
    foreach ($doc in $currentDocs) {
        $text=Get-Current (Get-Text $doc)
        foreach ($claim in [regex]::Matches($text,'(?i)(?:source at|baseline|main)\s*[:*` ]*([0-9a-f]{40})')) {
            if ($claim.Groups[1].Value -cne $baseline) { Add-Issue 'SourceIdentity' $doc 'Current baseline declaration differs from Recovery owning row.' }
        }
    }
    foreach ($checkout in [regex]::Matches($recovery,'checkout --detach ([0-9a-f]{40})')) {
        if ($checkout.Groups[1].Value -cne $baseline) { Add-Issue 'SourceIdentity' 'docs/RECOVERY.md' 'Recorded checkout differs from its declared baseline.' }
    }
    $dsl=Get-Text 'docs/architecture/workspace.dsl'
    $modelSource=[regex]::Match($dsl,'source at ([0-9a-f]{7,40})').Groups[1].Value
    if (-not $modelSource -or -not $baseline.StartsWith($modelSource,[StringComparison]::Ordinal)) {
        Add-Issue 'SourceIdentity' 'docs/architecture/workspace.dsl' 'Model source mapping differs from the selected implementation baseline.'
    }
    $guide=Get-Current (Get-Text 'docs/architecture/README.md')
    $pin=[regex]::Match($guide,'DSL SHA-256[^*\r\n]*\*\*([0-9a-f]{64})\*\*').Groups[1].Value
    $canonical=[Text.UTF8Encoding]::new($false).GetBytes($dsl.Replace("`r`n","`n"))
    $modelHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($canonical)).ToLowerInvariant()
    if (-not $pin -or $pin -cne $modelHash) { Add-Issue 'ArtifactIdentity' 'docs/architecture/README.md' 'Recorded model digest differs from the UTF-8/LF source bytes.' }

    try {
        $sdk=(Get-Text 'global.json' | ConvertFrom-Json).sdk
        $policy=Get-Row $recovery 'Framework / SDK policy'
        $frameworks=@()
        foreach ($project in @('src/LimitedUnderground.FirmwareLoader/LimitedUnderground.FirmwareLoader.csproj','tests/LimitedUnderground.FirmwareLoader.Tests/LimitedUnderground.FirmwareLoader.Tests.csproj')) {
            $xml=[Xml.XmlDocument]::new(); $xml.XmlResolver=$null; $xml.LoadXml((Get-Text $project))
            $frameworks+= $xml.SelectSingleNode('//TargetFramework').InnerText
            if ($xml.SelectNodes('//PackageReference').Count -gt 0 -and (Get-Row $recovery 'Packages') -match 'no external PackageReference') {
                Add-Issue 'PackageIdentity' $project 'Documented no-external-package claim differs from project references.'
            }
        }
        if ($frameworks[0] -cne $frameworks[1] -or -not $policy.Contains($frameworks[0])) {
            Add-Issue 'FrameworkIdentity' 'docs/RECOVERY.md' 'Documented runtime or application/test frameworks disagree.'
        }
        if (-not $policy.Contains([string]$sdk.version) -or -not $policy.Contains([string]$sdk.rollForward) -or
            ($sdk.allowPrerelease -eq $false -and $policy -notmatch 'no previews')) {
            Add-Issue 'SdkIdentity' 'docs/RECOVERY.md' 'Documented SDK selection differs from global.json.'
        }
        $workflow=Get-Text '.github/workflows/loader-host.yml'
        $hostSdk=[regex]::Match($workflow,'(?m)^\s*dotnet-version:\s*[''"]?([0-9]+\.[0-9]+\.[0-9]+)').Groups[1].Value
        $hostRow=Get-Row $recovery 'Hosted SDK'
        if (-not $hostSdk -or -not $hostRow.Contains($hostSdk)) { Add-Issue 'SdkIdentity' 'docs/RECOVERY.md' 'Hosted SDK declaration differs from the workflow.' }
        foreach ($doc in $currentDocs) {
            $text=Get-Current (Get-Text $doc)
            foreach ($claim in [regex]::Matches($text,'\bnet[0-9]+\.[0-9]+-windows\b')) {
                if ($claim.Value -cne $frameworks[0]) { Add-Issue 'FrameworkIdentity' $doc 'Current runtime claim differs from project configuration.' }
            }
            foreach ($claim in [regex]::Matches($text,'(?i)hosted SDK\s*([0-9]+\.[0-9]+\.[0-9]+)')) {
                if ($claim.Groups[1].Value -cne $hostSdk) { Add-Issue 'SdkIdentity' $doc 'Current hosted SDK claim differs from the workflow.' }
            }
        }
        $nuget=[Xml.XmlDocument]::new(); $nuget.XmlResolver=$null; $nuget.LoadXml((Get-Text 'NuGet.Config'))
        if ((Get-Row $recovery 'Packages') -match 'clears feeds' -and
            ($nuget.SelectNodes('//packageSources/clear').Count -ne 1 -or $nuget.SelectNodes('//packageSources/add').Count -gt 0)) {
            Add-Issue 'PackageIdentity' 'NuGet.Config' 'Documented cleared package sources differ from configuration.'
        }
    } catch { Add-Issue 'Configuration' 'global.json/project/workflow' 'Required toolchain configuration is unavailable or malformed.' }

    $provider=Get-Text 'src/LimitedUnderground.FirmwareLoader/OpenTrailInspectionProvider.cs'
    $targetSha=[regex]::Match($provider,'TargetContractSha256\s*=\s*"([0-9a-f]{64})"').Groups[1].Value
    $targetCommit=[regex]::Match($provider,'//\s*OpenTrail commit ([0-9a-f]{40})').Groups[1].Value
    $provenance=Get-Row $recovery 'Target-contract provenance'
    if (-not $targetSha -or -not $targetCommit -or -not $provenance.Contains($targetSha) -or -not $provenance.Contains($targetCommit)) {
        Add-Issue 'ArtifactIdentity' 'docs/RECOVERY.md' 'Target-contract byte/provenance identities differ from embedded source constants.'
    }
    if ($provider -notmatch 'Array\.Empty<LoaderSignerTrustPolicy>\(\)') {
        Add-Issue 'InspectionBoundary' 'src/LimitedUnderground.FirmwareLoader/OpenTrailInspectionProvider.cs' 'Empty production signer-trust claim requires reconciliation with source.'
    }
    if ((Get-Current (Get-Text 'CONTRIBUTING.md')) -notmatch '(?i)owner approves each of these first') {
        Add-Issue 'Authority' 'CONTRIBUTING.md' 'Current owner approval rules are missing.'
    }
    if ((Get-Current (Get-Text 'README.md')) -notmatch '(?i)default session inspects firmware bundles only') {
        Add-Issue 'InspectionBoundary' 'README.md' 'Current product installation boundary is missing.'
    }
    return [pscustomobject]@{ CurrentDocuments=@($currentDocs | Sort-Object); References=$references; Anchors=$anchors;
        SourceBaseline=$baseline; ModelSha256=$modelHash; Issues=@($issues.ToArray()) }
}

if (-not $DefinitionsOnly) {
    $report=Get-LoaderDocumentationReport -Root $Root
    if ($report.Issues.Count) {
        foreach ($issue in $report.Issues) { [Console]::Error.WriteLine("$($issue.Code): $($issue.Path): $($issue.Message)") }
        throw "Loader documentation validation failed ($($report.Issues.Count) findings)."
    }
    Write-Host "Loader documentation checks passed: $($report.CurrentDocuments.Count) current documents, $($report.References) local references, $($report.Anchors) anchors."
}
