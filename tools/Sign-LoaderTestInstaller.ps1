#Requires -Version 7.5

[CmdletBinding(DefaultParameterSetName = 'Sign')]
param(
    [Parameter(Mandatory)]
    [string] $Installer,
    [Parameter(Mandatory)]
    [ValidatePattern('\A[0-9A-Fa-f]{40}\z')]
    [string] $Thumbprint,
    [Parameter(Mandatory, ParameterSetName = 'Sign')]
    [ValidatePattern('\A[0-9A-Fa-f]{64}\z')]
    [string] $ExpectedUnsignedSha256,
    [Parameter(Mandatory, ParameterSetName = 'Verify')]
    [switch] $VerifyOnly,
    [Parameter(Mandatory, ParameterSetName = 'Verify')]
    [ValidatePattern('\A[0-9A-Fa-f]{64}\z')]
    [string] $ExpectedSignedSha256
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (!$IsWindows) { throw 'The owner signing and verification steps require Windows.' }
if (![IO.Path]::IsPathFullyQualified($Installer) -or $Installer -notmatch '\A[A-Za-z]:[\\/]') {
    throw 'Supply the full path to the local installer copy.'
}
$path = [IO.Path]::GetFullPath($Installer)
if ([IO.Path]::GetFileName($path) -ine 'FirmwareLoaderSetup.exe') {
    throw 'Choose the exact FirmwareLoaderSetup.exe copy.'
}
$installerFile = Get-Item -LiteralPath $path
if ($installerFile -isnot [IO.FileInfo] -or $installerFile.Length -le 0 -or
    ($installerFile.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw 'The installer must be a nonempty file at a direct local path.'
}
for ($directory = $installerFile.Directory; $null -ne $directory; $directory = $directory.Parent) {
    if (($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Use an installer copy whose parent directories have no links or junctions.'
    }
}

$thumbprint = $Thumbprint.ToUpperInvariant()
$subject = 'CN=Limited Underground Firmware Loader Test (D-141)'
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash
$expected = if ($VerifyOnly) { $ExpectedSignedSha256 } else { $ExpectedUnsignedSha256 }
if ($hash -ine $expected) { throw 'The installer SHA-256 does not match the approved input.' }

if (!$VerifyOnly) {
    $original = Get-AuthenticodeSignature -LiteralPath $path
    if ($original.Status -ne 'NotSigned') { throw 'Use the reviewed unsigned installer copy.' }
    $certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$thumbprint"
    $usage = @($certificate.Extensions |
        Where-Object { $_ -is [Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension] } |
        ForEach-Object { $_.EnhancedKeyUsages } | ForEach-Object { $_.Value })
    if ($certificate.Thumbprint -ine $thumbprint -or !$certificate.HasPrivateKey -or
        $certificate.Subject -cne $subject -or $certificate.Issuer -cne $subject -or
        (Get-Date) -lt $certificate.NotBefore -or (Get-Date) -ge $certificate.NotAfter -or
        '1.3.6.1.5.5.7.3.3' -notin $usage) {
        throw 'The owner must select the valid D-141 code-signing certificate in CurrentUser\My.'
    }

    # The owner signs only the hash-checked copy. No timestamp server or trust installation is used.
    $null = Set-AuthenticodeSignature -LiteralPath $path -Certificate $certificate -HashAlgorithm SHA256 -IncludeChain Signer
}

$signature = Get-AuthenticodeSignature -LiteralPath $path
$signer = $signature.SignerCertificate
if ($signature.SignatureType -ne 'Authenticode' -or $null -eq $signer -or
    $signer.Thumbprint -ine $thumbprint -or $signer.Subject -cne $subject -or $signer.Issuer -cne $subject -or
    (Get-Date) -lt $signer.NotBefore -or (Get-Date) -ge $signer.NotAfter) {
    throw 'The installer does not carry the expected D-141 Authenticode signer.'
}
if ($VerifyOnly) {
    foreach ($store in 'Root', 'TrustedPublisher') {
        $public = Get-Item -LiteralPath "Cert:\LocalMachine\$store\$thumbprint"
        if ($public.Thumbprint -ine $thumbprint -or $public.HasPrivateKey -or
            [Convert]::ToHexString($public.RawData) -cne [Convert]::ToHexString($signer.RawData)) {
            throw 'The test machine must trust the exact public D-141 certificate in both machine stores.'
        }
    }
    if ($signature.Status -ne 'Valid') {
        throw "Test-machine Authenticode verification failed ($($signature.Status))."
    }
    $state = 'VerifiedOnTestMachine'
} else {
    if ($signature.Status -notin @('Valid', 'NotTrusted')) {
        throw "Signing readback failed ($($signature.Status))."
    }
    $state = 'SignedPendingTestMachineVerification'
}

$signedHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash
if ($VerifyOnly -and $signedHash -ine $ExpectedSignedSha256) {
    throw 'The signed installer bytes changed during verification.'
}
[pscustomobject][ordered]@{
    State = $state
    AuthenticodeStatus = [string]$signature.Status
    Thumbprint = $signer.Thumbprint
    Sha256 = $signedHash
}
