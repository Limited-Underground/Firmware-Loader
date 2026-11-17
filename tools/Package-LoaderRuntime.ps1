#requires -Version 7.5
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $ArchiveDirectory,
    [Parameter(Mandatory)][string] $StageDirectory,
    [string] $InputManifest = (Join-Path $PSScriptRoot 'flash-runtime-inputs.json')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$streams = [Collections.Generic.List[IO.FileStream]]::new()
$archives = [Collections.Generic.List[IO.Compression.ZipArchive]]::new()
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$inventory = [Collections.Generic.List[object]]::new()
$required = 'python.exe', 'python314.dll', 'python314.zip', 'python314._pth',
    'packages/esptool/__init__.py', 'packages/serial/__init__.py'

function Assert-PlainDirectory([string] $Path) {
    for ($dir = [IO.DirectoryInfo]::new($Path); $null -ne $dir; $dir = $dir.Parent) {
        if (!$dir.Exists -or ($dir.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Use an existing folder with no redirected paths.'
        }
    }
}

function Test-RelativeFile([string] $Path) {
    if (!$Path -or $Path.Length -gt 240) { return $false }
    foreach ($part in $Path.Split('/')) {
        if (!$part -or $part -notmatch '^[ -~]+$' -or $part -match '[<>:"\\|?*]|[ .]$' -or
            $part -match '^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|$)') { return $false }
    }
    return $true
}

try {
    $inputs = [IO.Path]::GetFullPath($ArchiveDirectory)
    $stage = [IO.Path]::GetFullPath($StageDirectory)
    Assert-PlainDirectory $inputs
    Assert-PlainDirectory ([IO.Path]::GetDirectoryName($stage))
    if (Test-Path -LiteralPath $stage) { throw 'Choose a new staging folder.' }

    $manifest = Get-Content -LiteralPath $InputManifest -Raw | ConvertFrom-Json
    if ($manifest.format -cne 'LU-FLASH-RUNTIME-INPUTS-V1' -or $manifest.target -cne 'win-x64-cp314' -or
        $manifest.archives.Count -notin (3..40) -or @($manifest.archives | Where-Object kind -CEQ 'python').Count -ne 1) {
        throw 'The flash runtime input manifest is invalid.'
    }

    # Keep the exact verified files open until extraction completes.
    foreach ($pin in $manifest.archives) {
        $name = [string]$pin.filename
        if ($name -cnotmatch '^[A-Za-z0-9][A-Za-z0-9_.-]+\.(zip|whl)$' -or !$seen.Add($name) -or
            $pin.kind -cnotin @('python','wheel') -or
            ($pin.kind -ceq 'python' -and $name -cnotmatch '^python-3\.14\.\d+-embed-amd64\.zip$') -or
            ($pin.kind -ceq 'wheel' -and $name -cnotmatch '\.whl$')) {
            throw 'The flash runtime manifest contains an invalid or repeated archive.'
        }
        if ($pin.sha256 -cnotmatch '^[A-F0-9]{64}$' -or $pin.sha256 -ceq ('0' * 64) -or
            $null -eq $pin.size -or $pin.size -isnot [long] -or $pin.size -le 0 -or $pin.size -gt 100MB) {
            throw "The release owner has not pinned the approved size and SHA-256 for $name."
        }
        $path = Join-Path $inputs $name
        $file = Get-Item -LiteralPath $path -ErrorAction SilentlyContinue
        if (!$file -or $file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "The approved runtime file $name is missing or redirected."
        }
        $stream = [IO.File]::Open($path, 'Open', 'Read', 'Read')
        $streams.Add($stream)
        if ($stream.Length -ne $pin.size -or [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) -cne $pin.sha256) {
            throw "The runtime file $name does not match its approved size or SHA-256."
        }
        $stream.Position = 0
        $archive = [IO.Compression.ZipArchive]::new($stream, 'Read', $true)
        $archives.Add($archive)
        $inventory.Add(@{kind = $pin.kind; archive = $archive})
    }

    # Review every destination before writing. ZIP inputs may not redirect, overwrite or escape staging.
    $targets = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
    [long]$expanded = 0
    foreach ($input in $inventory) {
        foreach ($entry in $input.archive.Entries) {
            $name = $entry.FullName
            $type = ($entry.ExternalAttributes -shr 16) -band 0xf000
            if (($type -notin @(0,0x4000,0x8000)) -or ($entry.ExternalAttributes -band 0x400)) {
                throw 'A runtime archive contains a link or special file.'
            }
            if (!(Test-RelativeFile $name.TrimEnd('/'))) { throw 'A runtime archive has an unsafe path.' }
            if ($name.EndsWith('/')) { continue }
            if ($input.kind -ceq 'wheel') {
                if ($name -match '^[^/]+\.data/scripts/') { continue }
                if ($name -match '^[^/]+\.data/(purelib|platlib)/(.+)$') { $name = $Matches[2] }
                elseif ($name -match '^[^/]+\.data/') { throw 'A wheel needs an unsupported installation layout.' }
                $name = "packages/$name"
            }
            elseif ($name -match '^packages(?:/|$)') { throw 'The Python archive contains a reserved path.' }
            if (!(Test-RelativeFile $name) -or !$targets.TryAdd($name, $entry)) {
                throw 'Runtime archives contain duplicate or unsafe file paths.'
            }
            $expanded += $entry.Length
            if ($targets.Count -gt 16384 -or $expanded -gt 300MB) { throw 'The expanded flash runtime is too large.' }
        }
    }
    foreach ($name in $required) {
        if (!$targets.ContainsKey($name)) { throw "The approved archives are missing $name." }
    }
    foreach ($name in $targets.Keys) {
        $parent = $name
        while ($parent.Contains('/')) {
            $parent = $parent.Substring(0, $parent.LastIndexOf('/'))
            if ($targets.ContainsKey($parent)) { throw 'Runtime archives contain a file and folder at the same path.' }
        }
    }

    New-Item -ItemType Directory -Path $stage -ErrorAction Stop | Out-Null
    $runtime = Join-Path $stage 'flash-runtime'
    $utf8 = [Text.UTF8Encoding]::new($false)
    $pins = [ordered]@{}
    foreach ($name in ($targets.Keys | Sort-Object -CaseSensitive)) {
        $entry = $targets[$name]
        $destination = Join-Path $runtime $name
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
        $output = [IO.File]::Open($destination, 'CreateNew', 'Write', 'None')
        try {
            if ($name -ceq 'python314._pth') {
                # No site import or host package search.
                $output.Write($utf8.GetBytes(("python314.zip", ".", "packages", "" -join [char]10)))
            }
            else {
                $inputStream = $entry.Open()
                try { $inputStream.CopyTo($output) } finally { $inputStream.Dispose() }
            }
        }
        finally { $output.Dispose() }
        $pins[$name] = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    }
    $layout = [ordered]@{ format = 'LU-FLASH-RUNTIME-FILES-V1'; files = $pins } | ConvertTo-Json -Depth 4
    $receipt = Join-Path $stage 'runtime-files.json'
    # Write the completion record last. Partial staging folders cannot be published.
    [IO.File]::WriteAllText($receipt, $layout, $utf8)
    Write-Output 'The pinned flash runtime is staged for the Loader build.'
}
finally {
    foreach ($archive in $archives) { $archive.Dispose() }
    foreach ($stream in $streams) { $stream.Dispose() }
}
