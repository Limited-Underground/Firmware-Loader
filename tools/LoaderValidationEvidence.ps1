function Assert-PlainDirectory([string] $Path) {
    if (-not [IO.Path]::IsPathFullyQualified($Path)) { throw 'A fully qualified directory is required.' }
    $item = [IO.DirectoryInfo]::new([IO.Path]::GetFullPath($Path))
    while ($null -ne $item) {
        $item.Refresh()
        if (-not $item.Exists -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Missing or linked directory refused.'
        }
        $item = $item.Parent
    }
}

function New-LoaderValidationRun([string] $EvidenceRoot, [string] $RunPrefix) {
    if (-not $EvidenceRoot) {
        $EvidenceRoot = $env:LUF_EVIDENCE_ROOT
    }
    if (-not $EvidenceRoot) {
        $defaultParent = 'D:\LimitedUnderground\Build'
        if ([IO.Directory]::Exists($defaultParent)) {
            $EvidenceRoot = [IO.Path]::Combine($defaultParent, 'ldr-validation')
        } else {
            $EvidenceRoot = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { $env:TEMP }
        }
    }
    if (-not $EvidenceRoot -or -not [IO.Path]::IsPathFullyQualified($EvidenceRoot)) {
        throw 'A fully qualified Loader evidence root is required.'
    }
    $parent = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($EvidenceRoot))
    if (-not [IO.Directory]::Exists($parent)) {
        Assert-PlainDirectory ([IO.Path]::GetDirectoryName($parent))
        [IO.Directory]::CreateDirectory($parent) | Out-Null
    }
    Assert-PlainDirectory $parent
    $timestamp = [DateTime]::UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'", [Globalization.CultureInfo]::InvariantCulture)
    $runId = "$RunPrefix.$timestamp.$([Guid]::NewGuid().ToString('N'))"
    $run = Join-Path $parent $runId
    if (Test-Path -LiteralPath $run) { throw 'Existing validation run refused.' }
    [IO.Directory]::CreateDirectory($run) | Out-Null
    Assert-PlainDirectory $run
    Write-Host "Loader evidence root: $parent"
    Write-Host "Loader validation run: $run"
    return $run
}
