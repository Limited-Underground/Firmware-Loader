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
dotnet run --project tools/ReleaseBuilder -c Release -- --candidate <Trail build folder> --current <Trail build folder of what is on the board> --source-build-id "<Trail commit and build>" --out <new folder>
```

Each build folder is an ESP-IDF build of `heltec_v4_bench`, containing `opentrail_heltec_v4_bench.bin` and `bootloader/bootloader.bin`. The output folder must not already exist.

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
