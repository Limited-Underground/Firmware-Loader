# Firmware Loader tester package (L11 / LUF-0011a)

L11 is “Tester package: installer and instructions.” The existing release plan also requires “clean-machine installation, operator guidance and support/recovery documentation.” The local package assembler supplies the layout, byte checks and a short guide shipped as README.md. The guide covers the explicit board choice, release review, verified result and stop messages. Separate acceptance covers the installer, physical operations, signing and distribution.

## Inputs and layout

| Package path | Approved source |
| --- | --- |
| `installer/FirmwareLoaderSetup.exe` | Exact installer handoff, with its publisher and SHA-256 confirmed through the release route. The installer verifies the staged payload, installs it into an administrator-managed directory, and calls L9’s one-time setup with the owner-approved assignment. The package assembler consumes the exact installer handoff. |
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

From the approved installer’s elevated process, point `-InstalledLoader` at the installed executable in its administrator-managed program directory and `-AssignmentFile` at the approved local input. The setup entrypoint enrolls or verifies the protected recovery store and registers the fixed on-demand task `LimitedUnderground.FirmwareLoader.DailyWorker`. The named operator receives permission to read and run that task. Trusted Windows principals own the task configuration and protected state. The provisioning entrypoint is [Install-LoaderDailyWorker.ps1](../tools/Install-LoaderDailyWorker.ps1).

Run setup again with the same assignment to preserve the journal, board bindings and admission clock. An interrupted provisioning run must resume with the exact staged assignment. A replacement board list requires an explicit administrator call with `-ReplaceAssignment` and the same operator. Missing, malformed or conflicting assignments stop setup. `-Uninstall` uses `-InstalledLoader` to verify and remove the exact task and retains protected state for reinstall or a later owner decision.

Existing legacy board A assignment data stops daily setup. Agree on a migration that preserves the prior board pin, journal and admission clock before moving that PC to daily operation.

Clean-Windows acceptance must exercise first-day use by the assigned account, another launch after reboot, refusal of a second account, same-input setup, explicit board-list replacement and task removal. Install and restore on a connected board require separately authorized device sessions.

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
