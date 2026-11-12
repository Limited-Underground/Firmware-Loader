# Flash writer for the Heltec V4 bench board (L7 step 1)

Owner decision D-063 (2026-11-12) sets three rules for the first signed release:

- The first release is a qualification release for board A only.
- The writer is Espressif's esptool, pinned to an exact version.
- A write is verified by the chip's own checksum instead of a full byte readback. Reading more than 64 KB back over USB fails on the development PC.

This page describes the writer that follows those rules. The writer is **not wired into production**: no update choice uses it, production signer trust stays empty, and the engine still accepts only the host fixture adapter. Turning it on is a separate later change (L7 step 5).

## Parts

| Part | Licence | What it does |
|---|---|---|
| [lu_flash_helper.py](../tools/flash-helper/lu_flash_helper.py) | GPL-2.0-or-later (it uses esptool as a library) | A separate program. Each run prints one JSON line. Its commands are `list` (passive), `identify` and `digests` (read-only), `write` and `boot`. It requires esptool 5.3.1, an ESP32-S3 and 16 MB of flash. It never erases the whole chip, never changes flash settings and makes no retries. |
| [FlashHelperRunner.cs](../src/LimitedUnderground.FirmwareLoader/FlashHelperRunner.cs) | Apache-2.0 | Starts the helper only when the Python and helper files match their pinned SHA-256. The time limit is bounded. On cancellation or timeout it kills the process tree and waits for it to end. It accepts exactly one JSON object line. |
| [EsptoolHostUpdateAdapter.cs](../src/LimitedUnderground.FirmwareLoader/EsptoolHostUpdateAdapter.cs) | Apache-2.0 | The `IHostUpdateAdapter` for the board (adapter name `esptool-helper-v1`). |

The source-guard test allows process and flashing code in exactly these two C# files. Even those two may not open a port themselves or name an erase command.

## How an update is checked

1. **Finding a board is passive.** Only ports with USB ID `303A:1001` are offered. Nothing is opened until the user picks one. Then `identify` reads the chip, MAC address and flash size, and the selection is bound to that MAC and a new generation number.
2. **Image shape.** The image must start at `0x10000`, be a whole number of 4 KB sectors and fit inside the factory app partition. The writer checks this before any command, so esptool's sector erase can only touch the bytes being written.
3. **Before writing**, the chip's MD5 is taken for every span of the 16 MB layout. The layout comes from Trail's `heltec_v4_bench` `partitions.csv`: bootloader, partition table, otadata, gap, nvs, the written span, the rest of the factory partition, ota_0, ota_1 and ot_state.
4. **Write:** esptool writes the image and checks its own MD5. The helper then reports the chip's MD5 of the region, and the adapter requires it to equal the MD5 of the image it was given.
5. **Verify** (a separate command): all span digests are taken again. The engine computes the expected MD5 of the admitted image itself and compares it with the chip's digest of the written span. Every other span must be unchanged. MD5 here proves only that the transfer was correct. The release signature already proved the image is authentic before anything was written.
6. **Boot:** a hard reset, then the start-up log is watched for a bounded time. The board is healthy only if the bootloader loaded the app from `0x10000` and Trail printed `companion boot self-check PASS`. The boot identity is asserted by the adapter, from that log and the matching checksum. A firmware-reported release identity comes with L7 step 4.

## Not yet done

- The pinned Python and esptool files are not chosen or downloaded; that needs the owner's approval.
- The real-board timings come from the first device session; the values in `EsptoolAdapterLimits.Bench` are first bounds.
- The writer has not run against a board. Host tests use a fake helper that simulates the 16 MB flash and computes real digests.
- Production wiring, release building and signing are later L7 steps.
