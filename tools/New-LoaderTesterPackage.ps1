#requires -Version 7.5
[CmdletBinding(DefaultParameterSetName = 'Assemble')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Assemble')][string] $AppDirectory,
    [Parameter(Mandatory, ParameterSetName = 'Assemble')][string] $InstallRelease,
    [Parameter(Mandatory, ParameterSetName = 'Assemble')][string] $RestoreRelease,
    [Parameter(Mandatory, ParameterSetName = 'Assemble')][string] $Installer,
    [Parameter(Mandatory, ParameterSetName = 'Assemble')][string] $RuntimePins,
    [Parameter(Mandatory, ParameterSetName = 'Assemble')][string] $NoticesDirectory,
    [Parameter(Mandatory, ParameterSetName = 'Assemble')][string] $InputLock,
    [Parameter(Mandatory, ParameterSetName = 'Assemble')][string] $InputLockSha256,
    [Parameter(Mandatory, ParameterSetName = 'Assemble')][string] $OutputDirectory,
    [Parameter(Mandatory, ParameterSetName = 'Verify')][string] $VerifyDirectory,
    [Parameter(Mandatory, ParameterSetName = 'Verify')][string] $ManifestSha256
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$utf8 = [Text.UTF8Encoding]::new($false)
$packageName = 'package-manifest.json'
$appIdentity = 'LimitedUnderground.FirmwareLoader'
$releaseFiles = 'release.json', 'raw.bin', 'padded.bin', 'release.sig'
$emptyFileSha256 = 'E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855'

function Open-PlainFile([string]$Path) {
    $file = [IO.FileInfo]::new([IO.Path]::GetFullPath($Path))
    if (!$file.Exists -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "A package file is missing or redirected: $($file.Name)."
    }
    Assert-PlainDirectory $file.DirectoryName
    [IO.File]::Open($file.FullName, 'Open', 'Read', 'Read')
}

function Assert-PlainDirectory([string]$Path) {
    for ($d = [IO.DirectoryInfo]::new($Path); $null -ne $d; $d = $d.Parent) {
        if (!$d.Exists -or ($d.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Use an existing local directory without redirected folders.'
        }
    }
}

function Test-RelativeFile([string]$Path) {
    if (!$Path -or $Path.Length -gt 240) { return $false }
    foreach ($part in $Path.Split('/')) {
        if (!$part -or $part -notmatch '^[ -~]+$' -or $part -match '[<>:"\\|?*]|[ .]$' -or
            $part -match '^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|$)') { return $false }
    }
    return $true
}

function Test-Hash([string]$Hash) {
    $Hash -cmatch '^[0-9A-F]{64}$' -and $Hash -cne ('0' * 64)
}

function Read-JsonFile([string]$Path, [long]$MaxBytes, [string]$ExpectedHash = '') {
    $file = Open-PlainFile $Path
    try {
        if ($file.Length -lt 2 -or $file.Length -gt $MaxBytes) { throw 'A package record has an invalid size.' }
        $bytes = [byte[]]::new([int]$file.Length)
        $file.ReadExactly($bytes)
        if ($ExpectedHash -and [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)) -cne $ExpectedHash) {
            throw 'The package record does not match its expected SHA-256.'
        }
        $utf8.GetString($bytes) | ConvertFrom-Json -AsHashtable -Depth 8
    }
    finally { $file.Dispose() }
}

function Assert-FileRecord($record, [bool]$AllowEmpty) {
    if ($record -isnot [Collections.IDictionary] -or $record.Count -ne 2 -or
        $record['size'] -isnot [long] -or $record['size'] -lt 0 -or $record['size'] -gt 512MB -or
        $record['sha256'] -isnot [string] -or !(Test-Hash $record['sha256']) -or
        ($record['size'] -eq 0 -and (!$AllowEmpty -or $record['sha256'] -cne $emptyFileSha256))) {
        throw 'A package file record needs an exact byte count and uppercase SHA-256.'
    }
}

function Test-AllowedPath([string]$name, [string]$Mode, [bool]$Package) {
    if (!(Test-RelativeFile $name) -or $name -match '(?i)(?:\.key(?:\.|$)|\.(?:pem|pfx|p12|snk|kdbx)$|(?:^|/)\.[^/]+)') {
        return $false
    }
    if ($name -cmatch '^(?:app|notices)/.+$' -or $name -ceq 'metadata/runtime-files.json') { return $true }
    $setup = if ($Mode -ceq 'fixture') { 'installer/UNSIGNED-SETUP-FIXTURE.txt' } else { 'installer/FirmwareLoaderSetup.exe' }
    if ($name -ceq $setup -or ($Package -and $name -cin 'README.md','LICENSE')) { return $true }
    if ($name -cmatch '^firmware/(install|restore)/([^/]+)$') {
        return $Matches[2] -cin $releaseFiles -and ($Mode -cne 'fixture' -or $Matches[2] -cne 'release.sig')
    }
    return $false
}

function Read-Layout([string]$Path, [string]$Hash, [bool]$Package) {
    if (!(Test-Hash $Hash)) { throw 'Provide the expected uppercase SHA-256 from the package owner.' }
    $layout = Read-JsonFile $Path 4MB $Hash
    $format = if ($Package) { 'LU-LOADER-TESTER-PACKAGE-V1' } else { 'LU-LOADER-TESTER-INPUTS-V1' }
    if ($layout -isnot [Collections.IDictionary] -or $layout.Count -ne 3 -or
        $layout['format'] -cne $format -or $layout['mode'] -cnotin 'fixture','qualification-candidate' -or
        $layout['files'] -isnot [Collections.IDictionary] -or $layout['files'].Count -notin (1..16384)) {
        throw 'The tester package record has an invalid layout.'
    }
    $required = @("app/$appIdentity.exe", "app/$appIdentity.dll", "app/$appIdentity.deps.json",
        "app/$appIdentity.runtimeconfig.json", 'app/flash-helper/lu_flash_helper.py',
        'app/flash-runtime/python.exe', 'app/flash-runtime/python314._pth', 'app/flash-runtime/python314.zip',
        'app/flash-runtime/python314.dll', 'app/flash-runtime/packages/esptool/__init__.py',
        'app/flash-runtime/packages/serial/__init__.py', 'metadata/runtime-files.json', 'notices/NOTICE.txt')
    $required += if ($layout['mode'] -ceq 'fixture') { 'installer/UNSIGNED-SETUP-FIXTURE.txt' } else { 'installer/FirmwareLoaderSetup.exe' }
    foreach ($side in 'install','restore') {
        $required += "firmware/$side/release.json","firmware/$side/raw.bin","firmware/$side/padded.bin"
        if ($layout['mode'] -ceq 'qualification-candidate') { $required += "firmware/$side/release.sig" }
    }
    if ($Package) { $required += 'README.md','LICENSE' }

    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($name in $layout['files'].Keys) {
        if (!$seen.Add($name) -or !(Test-AllowedPath $name $layout['mode'] $Package)) {
            throw 'The package list contains an unsafe, repeated or unexpected path.'
        }
        $allowEmpty = $name.StartsWith('app/flash-runtime/', [StringComparison]::Ordinal) -and $name -cnotin $required
        Assert-FileRecord $layout['files'][$name] $allowEmpty
    }

    foreach ($name in $required) {
        if ($name -cnotin $layout['files'].Keys) { throw "The package list is missing $name." }
    }
    return $layout
}

function Check-File([string]$Path, $record, [string]$Label, [string]$CopyTo = '') {
    $file = Open-PlainFile $Path
    try {
        if ($file.Length -ne $record['size'] -or
            [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($file)) -cne $record['sha256']) {
            throw "A package file does not match its expected size and SHA-256: $Label."
        }
        if ($CopyTo) {
            $file.Position = 0
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($CopyTo)) | Out-Null
            $to = [IO.File]::Open($CopyTo, 'CreateNew', 'Write', 'None')
            try { $file.CopyTo($to) }
            finally { $to.Dispose() }
        }
    }
    finally { $file.Dispose() }
}

function Assert-Runtime($Layout, [string]$Root) {
    $runtime = Read-JsonFile (Join-Path $Root 'metadata/runtime-files.json') 4MB
    if ($runtime -isnot [Collections.IDictionary] -or $runtime.Count -ne 2 -or
        $runtime['format'] -cne 'LU-FLASH-RUNTIME-FILES-V1' -or $runtime['files'] -isnot [Collections.IDictionary]) {
        throw 'The L9 runtime record is invalid.'
    }
    $files = $Layout['files']
    $runtimePaths = @($files.Keys | Where-Object { $_.StartsWith('app/flash-runtime/', [StringComparison]::Ordinal) })
    if ($runtimePaths.Count -ne $runtime['files'].Count) { throw 'The L9 runtime file list does not match the package.' }
    foreach ($name in $runtimePaths) {
        $relative = $name.Substring('app/flash-runtime/'.Length)
        if ($relative -cnotin $runtime['files'].Keys -or $files[$name]['sha256'] -cne $runtime['files'][$relative]) {
            throw "An L9 runtime pin does not match: $relative."
        }
    }
}

function Get-AppIdentity([string]$Path, [long]$Offset) {
    $file = Open-PlainFile $Path
    try {
        $digest = [Convert]::ToHexString([Security.Cryptography.MD5]::HashData($file))
        'chip-md5:0x{0:X}:{1}:{2}' -f $Offset, $file.Length, $digest
    }
    finally { $file.Dispose() }
}

function Assert-ReleasePair($Layout, [string]$Root) {
    $files = $Layout['files']
    $records = @{}
    foreach ($side in 'install','restore') {
        $folder = "firmware/$side"
        $m = Read-JsonFile (Join-Path $Root "$folder/release.json") 16384
        $raw = $files["$folder/raw.bin"]
        $padded = $files["$folder/padded.bin"]
        if ($m -isnot [Collections.IDictionary] -or $m['format'] -cne 'PRODUCTION-RELEASE-MANIFEST-V1' -or
            $m['product'] -cne 'opentrail' -or $m['target'] -cne 'heltec_v4_bench' -or
            $m['generation'] -isnot [long] -or $m['minimumCurrentGeneration'] -isnot [long] -or
            $m['maximumCurrentGeneration'] -isnot [long] -or $m['minimumCurrentGeneration'] -le 0 -or
            $m['maximumCurrentGeneration'] -ne $m['minimumCurrentGeneration'] -or
            $m['generation'] -le $m['maximumCurrentGeneration'] -or
            $raw['size'] -gt $padded['size'] -or $padded['size'] -gt 16MB -or
            $m['rawImageLength'] -ne $raw['size'] -or $m['rawImageSha256'] -cne $raw['sha256'] -or
            $m['paddedImageLength'] -ne $padded['size'] -or $m['paddedImageSha256'] -cne $padded['sha256'] -or
            $m['destinationOffset'] -isnot [long] -or $m['destinationOffset'] -ne 65536 -or
            $m['destinationLength'] -ne $padded['size'] -or !(Test-Hash $m['recoveryImageDigest'])) {
            throw "The $side firmware record does not match its image, target or generation."
        }
        if ($Layout['mode'] -ceq 'qualification-candidate') {
            if ($files["$folder/release.sig"]['size'] -ne 384 -or !(Test-Hash $m['publicKeyFingerprint'])) {
                throw "The $side firmware folder needs an original 384-byte signature and signer fingerprint."
            }
            $signature = Open-PlainFile (Join-Path $Root "$folder/release.sig")
            try {
                $bytes = [byte[]]::new(384)
                $signature.ReadExactly($bytes)
                if ([Convert]::ToHexString($bytes) -ceq ('0' * 768)) { throw "The $side signature is empty." }
            }
            finally { $signature.Dispose() }
        }
        $image = Open-PlainFile (Join-Path $Root "$folder/padded.bin")
        try {
            $prefix = [byte[]]::new([int]$raw['size'])
            $image.ReadExactly($prefix)
            if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($prefix)) -cne $raw['sha256']) {
                throw "The $side raw firmware is not the prefix of its padded image."
            }
        }
        finally { $image.Dispose() }
        $records[$side] = $m
    }
    $install = $records['install']
    $restore = $records['restore']
    if ($restore['minimumCurrentGeneration'] -ne $install['generation'] -or
        $restore['paddedImageSha256'] -cne $install['recoveryImageDigest'] -or
        $install['paddedImageSha256'] -cne $restore['recoveryImageDigest'] -or
        $restore['requiredCurrentFirmware'] -cne (Get-AppIdentity (Join-Path $Root 'firmware/install/raw.bin') $install['destinationOffset']) -or
        $install['requiredCurrentFirmware'] -cne (Get-AppIdentity (Join-Path $Root 'firmware/restore/raw.bin') $restore['destinationOffset'])) {
        throw 'The restore release does not return the previous image after this install.'
    }
    foreach ($key in 'targetProfile','targetDigest','layoutDigest','writerProfileDigest','preservationPlanDigest',
        'recoveryRequirementsDigest','compatibility','destinationOffset','requiredBootloader') {
        if ($null -eq $install[$key] -or $install[$key] -cne $restore[$key]) {
            throw 'The install and restore releases disagree on the target, writer, layout or bootloader.'
        }
    }
}

function Assert-Tree($Layout, [string]$Root, [bool]$HasManifest) {
    Assert-PlainDirectory $Root
    $files = $Layout['files']
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push($Root)
    $entries = 0
    while ($pending.Count) {
        foreach ($path in [IO.Directory]::EnumerateFileSystemEntries($pending.Pop())) {
            if (++$entries -gt 32768) { throw 'The package has too many entries.' }
            $attr = [IO.File]::GetAttributes($path)
            if ($attr -band [IO.FileAttributes]::ReparsePoint) { throw 'The package contains a redirected entry.' }
            if ($attr -band [IO.FileAttributes]::Directory) { $pending.Push($path); continue }
            $name = [IO.Path]::GetRelativePath($Root, $path).Replace('\', '/')
            if ($HasManifest -and $name -ceq $packageName) { continue }
            if (!$seen.Add($name) -or $name -cnotin $files.Keys) { throw "The package contains an unexpected file: $name." }
            Check-File $path $files[$name] $name
        }
    }
    if ($seen.Count -ne $files.Count) { throw 'The package is missing one or more listed files.' }
    Assert-Runtime $Layout $Root
    Assert-ReleasePair $Layout $Root
}

if ($PSCmdlet.ParameterSetName -ceq 'Verify') {
    $root = [IO.Path]::GetFullPath($VerifyDirectory)
    $layout = Read-Layout (Join-Path $root $packageName) $ManifestSha256 $true
    Assert-Tree $layout $root $true
    [pscustomobject]@{ mode = $layout['mode']; manifestSha256 = $ManifestSha256; files = $layout['files'].Count }
    return
}

$layout = Read-Layout $InputLock $InputLockSha256 $false
$root = [IO.Path]::GetFullPath($OutputDirectory)
Assert-PlainDirectory ([IO.Path]::GetDirectoryName($root))
if (Test-Path -LiteralPath $root) { throw 'Choose a new package output directory.' }
$source = @{}
foreach ($name in $layout['files'].Keys) {
    if ($name.StartsWith('app/')) { $from = Join-Path $AppDirectory $name.Substring(4) }
    elseif ($name.StartsWith('firmware/install/')) { $from = Join-Path $InstallRelease $name.Substring(17) }
    elseif ($name.StartsWith('firmware/restore/')) { $from = Join-Path $RestoreRelease $name.Substring(17) }
    elseif ($name.StartsWith('notices/')) { $from = Join-Path $NoticesDirectory $name.Substring(8) }
    elseif ($name -ceq 'metadata/runtime-files.json') { $from = $RuntimePins }
    else { $from = $Installer }
    $source[$name] = [IO.Path]::GetFullPath($from)
    Check-File $source[$name] $layout['files'][$name] $name
}

[IO.Directory]::CreateDirectory($root) | Out-Null
$records = [ordered]@{}
foreach ($name in ($layout['files'].Keys | Sort-Object -CaseSensitive)) {
    Check-File $source[$name] $layout['files'][$name] $name (Join-Path $root $name)
    $records[$name] = $layout['files'][$name]
}
$instructions = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'tester-package/README.md'))
if ($layout['mode'] -ceq 'fixture') {
    $instructions = "# Unsigned package fixture — do not install or flash" + [char]10 + [char]10 + $instructions
}
[IO.File]::WriteAllText((Join-Path $root 'README.md'), $instructions, $utf8)
[IO.File]::Copy((Join-Path $PSScriptRoot '../LICENSE'), (Join-Path $root 'LICENSE'))
foreach ($name in 'README.md','LICENSE') {
    $path = Join-Path $root $name
    $records[$name] = [ordered]@{ size = (Get-Item -LiteralPath $path).Length; sha256 = (Get-FileHash -LiteralPath $path).Hash }
}
$record = [ordered]@{ format = 'LU-LOADER-TESTER-PACKAGE-V1'; mode = $layout['mode']; files = $records }
Assert-Tree $record $root $false
# Publish the completion record only after checking the whole staged tree.
$manifest = Join-Path $root $packageName
[IO.File]::WriteAllText($manifest, ($record | ConvertTo-Json -Depth 6), $utf8)
[pscustomobject]@{ mode = $record['mode']; manifestSha256 = (Get-FileHash -LiteralPath $manifest).Hash; files = $records.Count }
