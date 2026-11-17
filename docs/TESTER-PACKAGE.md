# Firmware Loader tester package (L11 / LUF-0011a)

L11 is “Tester package: installer and instructions.” The existing release plan also requires “clean-machine installation, operator guidance and support/recovery documentation.” The local package assembler supplies the layout, byte checks and operator guide. Separate acceptance covers the installer, physical operations, signing and distribution.

## Inputs and layout

| Package path | Approved source |
| --- | --- |
| `installer/FirmwareLoaderSetup.exe` | Exact installer handoff, with its publisher and SHA-256 confirmed through the release route. The installer verifies the staged payload, installs it into an administrator-managed directory, and calls L9’s one-time setup. This task does not build or sign the final Windows installer. |
| `app/` | Exact publish output with the L9 pins embedded, the packaged runtime and the existing helper. Preserve the selected .NET 10 Desktop prerequisite. |
| `metadata/runtime-files.json` | Exact L9 stage receipt used to publish that app. Runtime files must agree with this receipt and the package lock. |
| `firmware/install/` and `firmware/restore/` | Matching local release folders. Only `release.json`, `raw.bin`, `padded.bin` and each original `release.sig` enter a qualification candidate. Board-specific authorization and signature verification remain the Loader’s responsibility. |
| `notices/` | Reviewed notices and applicable source/redistribution materials for the exact app and bundled runtime. Include `NOTICE.txt`. |
| `README.md`, `LICENSE`, `package-manifest.json` | Current [tester guide](../tools/tester-package/README.md), repository license and exact assembled-file inventory. |

A fixture uses `installer/UNSIGNED-SETUP-FIXTURE.txt` in place of the installer and omits both signatures. Its first README line warns against installation or flashing. The assembler excludes private release-builder extras, including original-image and summary files. A restore image enters through a separately approved release folder and is checked against the install release’s recovery digest.

## Assemble and verify

Use [New-LoaderTesterPackage.ps1](../tools/New-LoaderTesterPackage.ps1) with PowerShell 7.5 or later. Prepare an owner-reviewed input lock with `format` set to `LU-LOADER-TESTER-INPUTS-V1`, `mode` set to `fixture` or `qualification-candidate`, and a `files` object keyed by the exact forward-slash package paths in the table. Each entry has exactly two keys, `size` for the file’s byte count and `sha256` for its uppercase SHA-256.

```powershell
$inputs = @{
    AppDirectory = $app
    InstallRelease = $install
    RestoreRelease = $restore
    Installer = $installer
    RuntimePins = $runtimePins
    NoticesDirectory = $notices
    InputLock = $lock
    InputLockSha256 = $approvedLockHash
    OutputDirectory = $newOutput
}
./tools/New-LoaderTesterPackage.ps1 @inputs
```

Only files named by the approved lock are copied. The assembler supplies the README and license, verifies input bytes, rejects unsafe entries, and checks that the two release records bind the supplied images and expected restore generation. It writes the package manifest last. An interrupted or refused partial staging folder has no valid completion manifest.

The result reports the package mode, file count and manifest SHA-256. Store that digest with the immutable release identity outside the package. Verify the package before use with the separately supplied digest.

```powershell
./tools/New-LoaderTesterPackage.ps1 -VerifyDirectory $package -ManifestSha256 $approvedPackageHash
```

The verifier refuses an incorrect manifest digest, a missing, changed or unlisted file and inconsistent runtime or firmware records. It checks package integrity. Signer and board admission remain separate. Preserve publisher verification and exact signed artifact evidence with the release handoff.

Run [Test-LoaderTesterPackage.ps1](../tools/Test-LoaderTesterPackage.ps1) with `-OutputDirectory` pointing at a new local scratch directory. It creates inert files and exercises assembly, verification and refusals without starting an installer, runtime or device.
