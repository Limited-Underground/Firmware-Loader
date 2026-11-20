# Firmware Loader tester package (L11 / LUF-0011a)

L11 is “Tester package: installer and instructions.” The existing release plan also requires “clean-machine installation, operator guidance and support/recovery documentation.” The local package assembler supplies the layout, byte checks and a short guide shipped as README.md. The guide covers the explicit board choice, release review, verified result and stop messages. Separate acceptance covers the installer, physical operations, signing and distribution.

## Inputs and layout

| Package path | Approved source |
| --- | --- |
| `installer/FirmwareLoaderSetup.exe` | The reviewed installer for this exact package, signed by the owner for the D-141 test. Record the signed executable's size and SHA-256 in the input lock. |
| `app/` | Exact publish output with the L9 pins embedded, the packaged runtime and the existing helper. Preserve the selected .NET 10 Desktop prerequisite. |
| `metadata/runtime-files.json` | Exact L9 stage receipt used to publish that app. Runtime files must agree with this receipt and the package lock. |
| `firmware/install/` and `firmware/restore/` | Matching local release folders. Only `release.json`, `raw.bin`, `padded.bin` and each original `release.sig` enter a qualification candidate. Board-specific authorization and signature verification remain the Loader’s responsibility. |
| `notices/` | Reviewed notices and applicable source/redistribution materials for the exact app and bundled runtime. Include `NOTICE.txt`. |
| `README.md`, `LICENSE`, `package-manifest.json` | Current [tester guide](../tools/tester-package/README.md), repository license and exact assembled-file inventory. |

A fixture uses `installer/UNSIGNED-SETUP-FIXTURE.txt` in place of the installer and omits both signatures. Its first README line warns against installation or flashing. The assembler excludes private release-builder extras, including original-image and summary files. A restore image enters through a separately approved release folder and is checked against the install release’s recovery digest.

## One-time setup for daily use

D-132 requires the installer to enroll a private assignment supplied by an administrator. Keep this local JSON file outside the tester package and source control, under administrator ownership and writable only by trusted administrators. The file names one Windows user and contains the exact install and restore release pair for each approved board.

| Field | Required value |
| --- | --- |
| `schema` | Integer `1`. |
| `operatorSid` | The stable SID of the one named Windows user. Resolve the account by name before submitting its SID. |
| `boards` | A nonempty array of up to 32 owner-approved assignments. |
| `fingerprint` | The existing D-065 board fingerprint in the Loader’s SHA-256 format. |
| `installManifestSha256` | SHA-256 of the exact signed install release’s original `release.json` bytes. |
| `restoreManifestSha256` | SHA-256 of that board’s separately approved signed restore `release.json` bytes. |

The three digest fields are uppercase, 64-character hexadecimal SHA-256 values. Zero digests, duplicate board fingerprints and reused release digests are refused. A qualification package still carries its matched install and restore pair. An assignment can authorize multiple packages for their own boards. The worker checks the connected board and the supplied manifest against the same assignment before proceeding through the existing signature, state, confirmation and verification checks.

The installer passes its fixed installed executable to `-InstalledLoader` and the private input to `-AssignmentFile`. The setup entrypoint enrolls or verifies the protected recovery store and registers the fixed on-demand task `LimitedUnderground.FirmwareLoader.DailyWorker`. The named operator receives permission to read and run that task. Trusted Windows principals own the task configuration and protected state. The provisioning entrypoint is [Install-LoaderDailyWorker.ps1](../tools/Install-LoaderDailyWorker.ps1).

The setup code refuses missing, malformed or conflicting assignments. Resume an interrupted provisioning run with the exact staged assignment. The retained recovery journal, board bindings and admission clock remain under the existing D-132 protection.

Existing legacy board A assignment data stops daily setup. Agree on a migration that preserves the prior board pin, journal and admission clock before moving that PC to daily operation.

Clean-Windows acceptance must exercise first-day use by the assigned account, another launch after reboot, refusal of a second account, same-input setup, explicit board-list replacement and task removal. Install and restore on a connected board require separately authorized device sessions.

## Run the reviewed installer

Use the reviewed qualification package on the designated clean Windows 11 x64 test computer. The installer and Loader use the x64 .NET 10 Windows Desktop Runtime. The one-time setup uses PowerShell 7.5 or later from its protected Program Files location. Check these prerequisites before the acceptance session.

D-141 uses an owner-created test certificate trusted only on the clean test computer. The build produces an unsigned `FirmwareLoaderSetup.exe`. The owner signs a separate reviewed copy using [Sign-LoaderTestInstaller.ps1](../tools/Sign-LoaderTestInstaller.ps1), pins the **signed** file's SHA-256 and size in the input lock, and reassembles the qualification package. Verify the installer signature and expected certificate thumbprint on the test computer before running it. Obtain the approved package manifest SHA-256 independently of the package.

Run from PowerShell 7 as an administrator. The installer uses the existing D-132 setup entrypoint after verifying the package and installing its app files.

~~~powershell
$setup = Join-Path $package 'installer\FirmwareLoaderSetup.exe'
& $setup --package $package --manifest-sha256 $approvedPackageHash --assignment-file $privateAssignment
if ($LASTEXITCODE -ne 0) { throw 'Firmware Loader setup stopped.' }
~~~

Setup verifies the package manifest digest and staged payload before copying the app to `Program Files\LimitedUnderground.FirmwareLoader`. It delegates the private assignment, task and recovery-store rules to the existing provisioning scripts. The installer performs PC setup without starting a firmware operation.

Repeat the same command with the same package and assignment to preserve state. For an authorized board-list replacement with the same operator, add `--replace-assignment`. For task removal, invoke the reviewed installer with `--uninstall` from PowerShell as an administrator. Protected records remain for the next setup or an owner-approved retirement.

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
