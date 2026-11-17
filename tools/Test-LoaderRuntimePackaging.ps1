#requires -Version 7.5
[CmdletBinding(DefaultParameterSetName = 'EvidenceRoot', PositionalBinding = $false)]
param(
    [Parameter(ParameterSetName = 'EvidenceRoot')][ValidateNotNullOrEmpty()][string] $EvidenceRoot,
    [Parameter(Mandatory, Position = 0, ParameterSetName = 'OutputDirectory')][string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LoaderValidationEvidence.ps1')
if ($PSCmdlet.ParameterSetName -ceq 'OutputDirectory') {
    $root = [IO.Path]::GetFullPath($OutputDirectory)
    if (Test-Path -LiteralPath $root) { throw 'Choose a new fixture output directory.' }
    [IO.Directory]::CreateDirectory($root) | Out-Null
    Write-Host "Loader validation run: $root"
} else {
    $root = New-LoaderValidationRun $EvidenceRoot 'LimitedUnderground.Loader.RuntimePackaging'
}
$source = Join-Path $root 'inputs'
New-Item -ItemType Directory -Path $source | Out-Null
$pack = Join-Path $PSScriptRoot 'Package-LoaderRuntime.ps1'
$utf8 = [Text.UTF8Encoding]::new($false)
$inputs = [Collections.Generic.List[object]]::new()
function Add-Fixture([string] $Name, [string] $Kind, [hashtable] $Entries) {
    $path = Join-Path $source $Name
    $stream = [IO.File]::Open($path, 'CreateNew', 'Write')
    try {
        $archive = [IO.Compression.ZipArchive]::new($stream, 'Create', $true)
        try {
            foreach ($key in $Entries.Keys) {
                $entry = $archive.CreateEntry($key)
                $value = $entry.Open()
                try { $value.Write($utf8.GetBytes($Entries[$key])) } finally { $value.Dispose() }
            }
        } finally { $archive.Dispose() }
    } finally { $stream.Dispose() }
    $inputs.Add(@{filename=$Name; kind=$Kind; size=(Get-Item -LiteralPath $path).Length
        sha256=(Get-FileHash -LiteralPath $path).Hash})
}
function Save-Manifest([string]$Name, [object[]]$Archives) {
    $file = Join-Path $root $Name
    $json = @{format='LU-FLASH-RUNTIME-INPUTS-V1'; target='win-x64-cp314'; archives=$Archives} | ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText($file, $json, $utf8)
    return $file
}
function Refuse([string]$Name, [string]$Manifest, [string]$Message) {
    $stage = Join-Path $root $Name
    try {
        & $pack -InputManifest $Manifest -ArchiveDirectory $source -StageDirectory $stage
        throw 'Accepted a refused fixture.'
    } catch {
        if ($_.Exception.Message -notlike $Message -or (Test-Path -LiteralPath $stage)) { throw }
    }
}
$exe = 'python.exe'
Add-Fixture 'python-3.14.10-embed-amd64.zip' 'python' @{
    $exe='fixture interpreter'; 'python314._pth'='untrusted path from archive'; 'python314.zip'='fixture standard library'; 'python314.dll'='fixture DLL'
}
Add-Fixture 'esptool-5.3.1-py3-none-any.whl' 'wheel' @{
    'esptool-5.3.1.data/purelib/esptool/__init__.py'='fixture module'; 'esptool-5.3.1.data/scripts/esptool'='ignored executable entry point'
}
Add-Fixture 'pyserial-3.5-py3-none-any.whl' 'wheel' @{'serial/__init__.py'='fixture module'}
$valid = Save-Manifest 'valid.json' $inputs.ToArray()
$stage = Join-Path $root 'stage'
& $pack -InputManifest $valid -ArchiveDirectory $source -StageDirectory $stage
$files = (Get-Content -LiteralPath (Join-Path $stage 'runtime-files.json') -Raw | ConvertFrom-Json -AsHashtable).files
if ($files.Count -ne 6 -or $files.Contains('packages/esptool') -or
    (Get-Content -LiteralPath (Join-Path $stage 'flash-runtime/python314._pth') -Raw) -cne
        ('python314.zip','.','packages','' -join [char]10)) { throw 'Unexpected staged layout.' }
foreach ($file in $files.Keys) {
    if ($files[$file] -cne (Get-FileHash -LiteralPath (Join-Path $stage "flash-runtime/$file")).Hash) { throw 'Bad output hash.' }
}
$badPin = $inputs[0].Clone()
$badPin.sha256 = 'F' * 64
Refuse 'bad-hash' (Save-Manifest 'bad-hash.json' @($badPin,$inputs[1],$inputs[2])) '*does not match*'
$badPin.size = $null
Refuse 'unpinned' (Save-Manifest 'unpinned.json' @($badPin,$inputs[1],$inputs[2])) '*has not pinned*'
$draft = @(foreach ($pin in $inputs) {
    $copy = $pin.Clone()
    $copy.size = $null
    $copy.sha256 = $null
    $copy
})
Refuse 'draft-manifest' (Save-Manifest 'draft.json' $draft) '*has not pinned*'
Add-Fixture 'unsafe-1.0-py3-none-any.whl' 'wheel' @{'../escaped.py'='fixture traversal'}
Refuse 'unsafe' (Save-Manifest 'unsafe.json' $inputs.ToArray()) '*unsafe path*'
Add-Fixture 'overlap-1.0-py3-none-any.whl' 'wheel' @{'ESpTool/__init__.py'='fixture duplicate'}
Refuse 'overlap' (Save-Manifest 'overlap.json' @($inputs[0],$inputs[1],$inputs[2],$inputs[4])) '*duplicate*'
'PASS: runtime layout, staged hashes, missing pins, mismatched pins, traversal, and duplicate destinations.'
