# Qualification release builder (L7 step 2)

Owner decision D-063 (2026-11-12): the first signed release is a **qualification release** of the Trail bench build. It is for board A only, labelled test-only and never offered to testers. This page describes how that release is built. Building is not signing, trust or installation:

- the builder never reads a private key and never signs;
- production trust stays empty;
- no update choice uses the result until production wiring (L7 step 5).

## Parts

- [QualificationRelease.cs](../src/LimitedUnderground.FirmwareLoader/QualificationRelease.cs) builds the canonical [release record](PRODUCTION-RELEASE-MANIFEST-V1.md) with the Loader's own JSON writer. It then checks that `ProductionReleaseManifest.TryParse` accepts the bytes and gives them back unchanged. It also builds the three contract documents the record names, plus the Loader policy and the board state the record expects.
- The writer profile (`LU-WRITER-PROFILE-V1`) is built in [EsptoolHostUpdateAdapter.cs](../src/LimitedUnderground.FirmwareLoader/EsptoolHostUpdateAdapter.cs), next to the writer it describes (see the [flash writer](FLASH-WRITER.md)). It includes the flash helper's SHA-256, so a release signed for one helper can't be installed with another.
- [tools/ReleaseBuilder](../tools/ReleaseBuilder/Program.cs) is a small command-line program around the builder. It writes a release folder and a plain-language `SUMMARY.txt` for the owner to read before signing.

## Choices made here

- **Padding:** the app is padded to whole 4 KB sectors with `0xFF`, the value of erased flash.
- **Generations:** admission needs a positive current generation, so the image now on board A counts as generation 1 and the qualification release is generation 2.
- **Board state:** the board must show these before anything is written:
  - `requiredBootloader` is `chip-md5:0x0:<length>:<MD5>` of the bootloader that is on it now;
  - `requiredCurrentFirmware` is `chip-md5:0x10000:<length>:<MD5>` of the app that is on it now.

  The Loader checks both with the chip's own checksum.
- **Recovery image:** the padded app that is on the board now. Its SHA-256 is `recoveryImageDigest`. Restoring it is L8.
- **Policy:**
  - allowed `0x10000` + `0x4F0000` (the factory app partition);
  - protected `0x0` + `0x10000` (bootloader, partition table, otadata, nvs);
  - 4 KB erase block;
  - the minimum generation equals the release's generation.
- **Target and layout digests:** they stay pinned to Trail `target-contract.json` (`BF40D99A…`, 6,861 bytes) and `partitions.csv` (`CD612068…`, 546 bytes). Both still match Trail `main` at `1e45288`.

## Use

From the Loader checkout, in PowerShell 7:

```powershell
dotnet run --project tools/ReleaseBuilder -c Release -- --candidate <Trail build folder> --current-app <app image last written to the board> --boot-readback <read-back of the board from 0x0, at least 0x8000 bytes> --source-build-id "<Trail commit and build>" --out <new folder>
```

The candidate folder is an ESP-IDF build of `heltec_v4_bench` containing `opentrail_heltec_v4_bench.bin`. The board's current state comes from the board, not from a build (I-036): `--current-app` is the exact app image last written to it, and `--boot-readback` is a read-back of its boot area, of which the builder uses the first 0x8000 bytes. Board A runs an older bootloader than current builds, so a build's `bootloader.bin` would describe the wrong board. The output folder must not already exist.

## Installing it (L7 step 5)

Owner decision D-065 (2026-11-12) set three things:

- the flash helper runs on this PC's ESP-IDF Python, pinned by SHA-256;
- board A is named only on this PC, as a hash of its MAC address;
- the administrator-only storage is enrolled once.

[QualificationInstall.cs](../src/LimitedUnderground.FirmwareLoader/QualificationInstall.cs) adds one install path for this release only. Bundle inspection still never marks anything trusted (`LoaderProductionProviders.SignerTrustPolicies` stays empty). Trust comes only from the pinned key (`ReleaseTrustAnchor`, now with the key embedded) through full production admission.

In the window, **Install signed qualification release**:

1. Runs only with administrator rights. The operation journal (`MachineRecoveryStorePolicy`) and the board A pin are administrator-only.
2. Opens a signed release folder: `release.json`, `release.sig`, `raw.bin`, `padded.bin`.
3. Needs exactly one connected board (USB ID `303A:1001`). It reads the board's MAC address (read-only).
4. **Board A:** the first time, it asks to set the connected board as board A. It then stores the SHA-256 of its MAC, together with the SHA-256 of the ESP-IDF `python.exe`, in `ProgramData\LimitedUnderground.FirmwareLoader.Qualification\`. From then on any other board is refused, and the Loader offers no way to change board A.
5. Reads, read-only, the chip's checksums of the bootloader and the app now on the board. Unless they match the record's required board state, the release is not admitted. The record names that app's generation (minimum = maximum). When the chip's checksum proves the board runs that app, the board counts as being at that generation: 1 for the pre-L7 bench image, 2 after L7. A restore release is therefore an ordinary signed release with a higher generation (I-037).
6. Runs full production admission against the pinned key and the fixed qualification policy. The policy fixes the target, layout, allowed region `0x10000`+`0x4F0000`, protected region `0x0`+`0x10000` and minimum generation 2. The expiry and the recovery image come from the signed record. The admission clock is kept in the same administrator-only folder.
7. Builds the bundle the update engine checks before each command, in memory from the admitted record (inspection never trusts it). It then offers the one board as the only choice in the existing update workflow, labelled as a real write.
8. The engine accepts the reviewed writer adapter alongside the host fixture. After one confirmation it writes, verifies by the chip's checksum of every region, and waits for the boot line.

The flash helper now also accepts Trail's `lu_ident … partition=0x10000 … self_check=PASS` line (Trail PR #245) as the boot check, so the writer profile digest changed. A release must therefore be built from this version of the Loader. Restoring the previous firmware (L8) uses the same path, with its own signed release: the earlier image at the next generation. While a board A session is active, the window's footer says the session can write firmware (I-038).

## Tests

[QualificationReleaseTests.cs](../tests/LimitedUnderground.FirmwareLoader.Tests/QualificationReleaseTests.cs) cover:

- padding;
- stable contract documents;
- a canonical record anchored to the owner's key, expiring in exactly 180 days;
- refusal of bad inputs;
- the real production admission accepting a built record signed with a throwaway test key, and refusing:
  - a field changed after signing;
  - a different board state or writer;
  - a higher policy floor.

The builder was also run once on the archived Trail `ce6048c` build, into a scratch folder.

[QualificationInstallTests.cs](../tests/LimitedUnderground.FirmwareLoader.Tests/QualificationInstallTests.cs) run the whole install path. They use the real writer adapter on a simulated 16 MB board A and a throwaway key. They check:

- one write, one verify and one boot;
- the new image is on the board and every other byte is unchanged;
- refusals before any write for:
  - a different board;
  - board A not set;
  - wrong current firmware;
  - a changed record;
  - another key;
  - no board, or two boards;
- the real-write label;
- the bench preservation geometry;
- the pinned helper digest and the embedded key match the committed files.
