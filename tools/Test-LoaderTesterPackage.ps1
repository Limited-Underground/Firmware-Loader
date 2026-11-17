#requires -Version 7.5
[CmdletBinding()]
param([Parameter(Mandatory)][string] $OutputDirectory)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$utf8 = [Text.UTF8Encoding]::new($false)
$root = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $root) { throw 'Choose a new tester package fixture directory.' }
[IO.Directory]::CreateDirectory($root) | Out-Null
$builder = Join-Path $PSScriptRoot 'New-LoaderTesterPackage.ps1'
$fixtureRoot = Join-Path $root 'synthetic-input'
$paths = [ordered]@{}

function Write-Bytes([string]$Path, [byte[]]$Bytes) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path)) | Out-Null
    [IO.File]::WriteAllBytes($Path, $Bytes)
}
function Write-Text([string]$Path, [string]$Text) { Write-Bytes $Path $utf8.GetBytes($Text) }
function Pin([string]$Path) { @{ size = (Get-Item -LiteralPath $Path).Length; sha256 = (Get-FileHash -LiteralPath $Path).Hash } }
function Fixture([string]$Name, [string]$Content) {
    $file = Join-Path $fixtureRoot $Name
    Write-Text $file $Content
    $paths[$Name] = $file
}
function Refuse([string]$Case, [scriptblock]$Action, [string]$Expected) {
    try { & $Action | Out-Null }
    catch {
        if ($_.Exception.Message -notlike $Expected) { throw }
        "PASS: $Case refused."
        return
    }
    throw "The packager accepted the refused case: $Case."
}
function Assert([bool]$Condition, [string]$Reason) { if (!$Condition) { throw $Reason } }

$loader = 'LimitedUnderground.FirmwareLoader'
foreach ($extension in 'exe','dll','deps.json','runtimeconfig.json') {
    Fixture "app/$loader.$extension" "Synthetic unsigned test-build $extension. Do not run."
}
Fixture 'app/flash-helper/lu_flash_helper.py' '# inert fixture'
$runtime = [ordered]@{}
foreach ($leaf in 'python.exe','python314._pth','python314.zip','python314.dll','packages/esptool/__init__.py','packages/serial/__init__.py') {
    $name = "app/flash-runtime/$leaf"
    Fixture $name "Synthetic runtime file: $leaf"
    $runtime[$leaf] = (Pin $paths[$name]).sha256
}
Fixture 'metadata/runtime-files.json' (@{ format = 'LU-FLASH-RUNTIME-FILES-V1'; files = $runtime } | ConvertTo-Json -Depth 5)
Fixture 'installer/UNSIGNED-SETUP-FIXTURE.txt' 'Synthetic installer layout marker. There is no installer in this fixture.'
Fixture 'notices/NOTICE.txt' 'Synthetic notices. The approved package needs its exact third-party notices and source obligations.'

foreach ($side in 'install','restore') {
    Fixture "firmware/$side/raw.bin" "Synthetic MeshTrail $side image"
    $raw = [IO.File]::ReadAllBytes($paths["firmware/$side/raw.bin"])
    $padded = [byte[]]::new(4096)
    [Array]::Fill[byte]($padded, 255)
    $raw.CopyTo($padded, 0)
    $path = Join-Path $fixtureRoot "firmware/$side/padded.bin"
    Write-Bytes $path $padded
    $paths["firmware/$side/padded.bin"] = $path
    # The local release-builder folder can contain private originals and engineering files.
    Write-Text (Join-Path $fixtureRoot "firmware/$side/recovery-image.bin") 'Private-source fixture. Must not be copied.'
    Write-Text (Join-Path $fixtureRoot "firmware/$side/SUMMARY.txt") 'Private-source fixture. Must not be copied.'
}
foreach ($side in 'install','restore') {
    $raw = Pin $paths["firmware/$side/raw.bin"]
    $padded = Pin $paths["firmware/$side/padded.bin"]
    $previous = if ($side -ceq 'install') { 'restore' } else { 'install' }
    $generation = if ($side -ceq 'install') { 2 } else { 3 }
    $m = [ordered]@{
        format='PRODUCTION-RELEASE-MANIFEST-V1'; product='opentrail'; target='heltec_v4_bench'
        targetProfile='HTIT-WB32LAF-V4.2/ESP32-S3R2-v0.2/16MiB/2MiB/US915'
        targetDigest='A' * 64; layoutDigest='B' * 64
        sourceBuildId="fixture-$side"; rawImageLength=$raw.size; rawImageSha256=$raw.sha256
        paddedImageLength=$padded.size; paddedImageSha256=$padded.sha256
        destinationOffset=65536; destinationLength=$padded.size; compatibility=1
        requiredBootloader='chip-md5:0x0:32768:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'
        requiredCurrentFirmware=('chip-md5:0x10000:{0}:{1}' -f
            (Get-Item -LiteralPath $paths["firmware/$previous/raw.bin"]).Length,
            (Get-FileHash -LiteralPath $paths["firmware/$previous/raw.bin"] -Algorithm MD5).Hash)
        minimumCurrentGeneration=$generation-1; maximumCurrentGeneration=$generation-1; generation=$generation
        signerRole='release-approver-and-signing-custodian'; publicKeyFingerprint=''; policyRevision=1
        issuedAtUtcMilliseconds=0; expiresAtUtcMilliseconds=15552000000
        writerProfileDigest='C' * 64; preservationPlanDigest='D' * 64; recoveryRequirementsDigest='E' * 64
        recoveryImageDigest=(Pin $paths["firmware/$previous/padded.bin"]).sha256
    }
    Fixture "firmware/$side/release.json" ($m | ConvertTo-Json -Compress)
}
$lock = Join-Path $root 'fixture-lock.json'
function Save-Lock([string]$Mode = 'fixture') {
    $pins = [ordered]@{}
    foreach ($name in $paths.Keys) { $pins[$name] = Pin $paths[$name] }
    Write-Text $lock (@{ format='LU-LOADER-TESTER-INPUTS-V1'; mode=$Mode; files=$pins } | ConvertTo-Json -Depth 6)
    (Pin $lock).sha256
}
$sourceArgs = @{
    AppDirectory=(Join-Path $fixtureRoot 'app'); InstallRelease=(Join-Path $fixtureRoot 'firmware/install')
    RestoreRelease=(Join-Path $fixtureRoot 'firmware/restore'); Installer=$paths['installer/UNSIGNED-SETUP-FIXTURE.txt']
    RuntimePins=$paths['metadata/runtime-files.json']; NoticesDirectory=(Join-Path $fixtureRoot 'notices'); InputLock=$lock
}
$lockHash = Save-Lock
$out = Join-Path $root 'unsigned-package'
$result = & $builder @sourceArgs -InputLockSha256 $lockHash -OutputDirectory $out
$valid = & $builder -VerifyDirectory $out -ManifestSha256 $result.manifestSha256
Assert ($valid.mode -ceq 'fixture' -and $valid.files -eq $result.files) 'The fixture did not verify.'
Assert ((Get-Content -LiteralPath (Join-Path $out 'README.md') -TotalCount 1) -like '*do not install or flash*') 'Fixture warning absent.'
foreach ($side in 'install','restore') {
    foreach ($leaf in 'release.sig','recovery-image.bin','SUMMARY.txt') {
        Assert (!(Test-Path -LiteralPath (Join-Path $out "firmware/$side/$leaf"))) 'Unexpected private or signature material in fixture.'
    }
}
'PASS: unsigned fixture assembled, verified, marked and limited to the approved files.'

Refuse 'an incorrect approved-input lock digest' {
    & $builder @sourceArgs -InputLockSha256 ('1' * 64) -OutputDirectory (Join-Path $root 'bad-lock')
} '*expected SHA-256*'
Refuse 'an incorrect package manifest digest' {
    & $builder -VerifyDirectory $out -ManifestSha256 ('1' * 64)
} '*expected SHA-256*'
Refuse 'an existing package output' {
    & $builder @sourceArgs -InputLockSha256 $lockHash -OutputDirectory $out
} '*new package output*'

$runtimeFile = $paths['app/flash-runtime/python.exe']
$original = [IO.File]::ReadAllBytes($runtimeFile)
$modified = [byte[]]$original.Clone()
$modified[0] = $modified[0] -bxor 1
Write-Bytes $runtimeFile $modified
Refuse 'a same-size changed input' {
    & $builder @sourceArgs -InputLockSha256 $lockHash -OutputDirectory (Join-Path $root 'tampered-input')
} '*expected size and SHA-256*'
$changedLock = Save-Lock
Refuse 'a changed runtime input even with an updated package lock' {
    & $builder @sourceArgs -InputLockSha256 $changedLock -OutputDirectory (Join-Path $root 'bad-runtime-pin')
} '*L9 runtime pin does not match*'
Assert (!(Test-Path -LiteralPath (Join-Path $root 'bad-runtime-pin/package-manifest.json'))) 'Rejected stage has a completion record.'
Write-Bytes $runtimeFile $original

$restorePath = $paths['firmware/restore/release.json']
$originalRestore = [IO.File]::ReadAllText($restorePath)
$wrong = $originalRestore | ConvertFrom-Json -AsHashtable
$wrong['minimumCurrentGeneration'] = 4
$wrong['maximumCurrentGeneration'] = 4
$wrong['generation'] = 5
Write-Text $restorePath ($wrong | ConvertTo-Json -Compress)
$changedLock = Save-Lock
Refuse 'a restore for the wrong installed generation' {
    & $builder @sourceArgs -InputLockSha256 $changedLock -OutputDirectory (Join-Path $root 'wrong-restore')
} '*does not return the previous image*'
Write-Text $restorePath $originalRestore

$fixtureSetup = $paths['installer/UNSIGNED-SETUP-FIXTURE.txt']
$paths.Remove('installer/UNSIGNED-SETUP-FIXTURE.txt')
$paths['installer/FirmwareLoaderSetup.exe'] = $fixtureSetup
$candidate = Save-Lock 'qualification-candidate'
Refuse 'the unsigned fixture presented as a qualification candidate' {
    & $builder @sourceArgs -InputLockSha256 $candidate -OutputDirectory (Join-Path $root 'unsigned-candidate')
} '*missing firmware/install/release.sig*'
$paths.Remove('installer/FirmwareLoaderSetup.exe')
$paths['installer/UNSIGNED-SETUP-FIXTURE.txt'] = $fixtureSetup

Fixture 'app/../escape.txt' 'Unsafe path fixture'
$unsafe = Save-Lock
Refuse 'a traversal path in the approved list' {
    & $builder @sourceArgs -InputLockSha256 $unsafe -OutputDirectory (Join-Path $root 'unsafe-path')
} '*unsafe, repeated or unexpected path*'

$packed = Join-Path $out 'app/flash-runtime/python.exe'
$held = Join-Path $root 'held-runtime-fixture'
Move-Item -LiteralPath $packed -Destination $held
Refuse 'a missing packaged runtime file' {
    & $builder -VerifyDirectory $out -ManifestSha256 $result.manifestSha256
} '*missing one or more listed files*'
Move-Item -LiteralPath $held -Destination $packed
Write-Bytes $packed $modified
Refuse 'a changed packaged runtime file' {
    & $builder -VerifyDirectory $out -ManifestSha256 $result.manifestSha256
} '*expected size and SHA-256*'
Write-Bytes $packed $original
$unexpected = Join-Path $out 'app/unexpected.txt'
Write-Text $unexpected 'extra fixture'
Refuse 'an unlisted packaged file' {
    & $builder -VerifyDirectory $out -ManifestSha256 $result.manifestSha256
} '*unexpected file*'
Move-Item -LiteralPath $unexpected -Destination (Join-Path $root 'held-extra-fixture')
& $builder -VerifyDirectory $out -ManifestSha256 $result.manifestSha256
