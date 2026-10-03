# LUF-0010a host preparation: exact Trail installation/recovery matrix

VERIFIED source/artifact inspection 2026-10-02T00:13:13Z (local date 2026-10-01). Preparation only: no device enumeration, build, test, signing, installation or publication. Physical acceptance remains blocked.

Baseline: canonical Firmware-Loader HEAD `cc8e172442a7733a901d7c929f94c1471b399a12`; tracked/staged clean before the new task allocations. Target inputs remain owned by OpenTrail. No firmware, original custody, device identifiers or private payloads are copied into Loader.

## Frozen source inputs and executable gates

| Boundary | Observed source/input | Required before canonical physical installation |
|---|---|---|
| Candidate target | HTIT-WB32LAF V4.2 / ESP32-S3, 16 MiB flash. Current owner target ID `heltec-v4-bench-candidate`, `supported=false`; revision/RF fields and updater/storage/recovery authority unresolved. | OpenTrail accepts exact release target/profile and current physical unit; family/COM/VID-PID alone cannot establish the intended unit. Evaluation firmware is not a production Loader release. |
| Inspection mapping | Loader key `heltec_v4_bench`; historical target Git-blob pin `ec818efab9a14ce4f0900068c9474acfe2577d74e2e39fa4850f3ff0567e9776` at OpenTrail commit `a327104ac67a3f5918a8b0191c96dceb05b5399b`. Current owner target filesystem SHA-256 `bfa9407407041f403271d627914b3a2ddf417d0cafcf8fcd762fd1e0cba3f419`. | Accepted mapping/pin/byte convention; neither filesystem normalization nor a newer candidate silently replaces an inspection pin. |
| Standard bench candidate | Freshly read owner build `build/ot0302-settings-standard-a-r2/opentrail_heltec_v4_bench.bin`: 602,096 bytes; SHA-256 `33c27be5dea55e38276eafe96f278504353ca5ab1749bec0e5e95db1668e5193`. | Select one accepted signed release identity, compatibility/generation and boot-health criteria. This available candidate alone grants no installation authority. |
| Distinct padded bench representation | In-memory `0xff` padding to 733,184 bytes: SHA-256 `14c8f1ce62ff7d023cadfb1ec64bfa38eb2e3c371fb254b116dce5cd4bd5ff7d`; no artifact written. | Explicit exact length/hash/address representation. Raw and padded images are not interchangeable release bytes. |
| Layout | Owner `firmware/targets/heltec_v4_bench/partitions.csv`; raw SHA-256 `9c27fd7e647ace6eb32524281bfaa52cd370419fec2457f1acdebc6b0cbfa52a`. Ranges below end at 16 MiB. | Accepted release layout plus independent current-device table comparison; actual erase geometry/timing/side effects remain unapproved. |
| Release/adapter | Production signer registry empty; internal fixture envelope and `host-fixture-v1` only. Default UI has no choices/factory. | Production envelope/trust/revocation/time authority; real atomic identity-bound adapter and finite physical profile. No direct promotion of fixtures or Python bench tooling. |
| Ownership/persistence | At preparation baseline only FakeLease/FakeJournal implemented the host interfaces; LUF-0009d now adds a validated file-backed host implementation, owner-accepted for its host-only scope. | Separate production storage enrollment and adapter wiring; host-only LUF-0009d acceptance does not enable installation. |
| Recovery | OpenTrail owns exact original custody and restoration evidence; no private originals copied or admitted here. Existing bench custody covers named prefix/state regions, not a whole-chip backup. | Fresh exact original/recovery binding for every affected/erase-rounded span, independent recovery route, current recovery authorization, independent original/protected readback and original-boot criteria. Historical consumed plans are not current grants. |

## Address and preservation matrix

Half-open source ranges; no write authority. Preserve everything outside the explicitly accepted image/erase plan. Never infer whole-slot erase, OTA changes, NVS/state reset or eFuse/security changes.

| Region | Offset | Bytes | End | Boundary |
|---|---:|---:|---:|---|
| Bootloader custody | `0x000000` | 32,768 | `0x008000` | Preserve/compare; not implicitly writable. |
| Partition table | `0x008000` | 4,096 | `0x009000` | Exact layout and preservation. |
| OTA data | `0x009000` | 8,192 | `0x00b000` | Preserve boot selection; standard bench path requires empty selection. |
| Reserved gap | `0x00b000` | 8,192 | `0x00d000` | Preserve; seven-region custody does not independently read this gap. |
| NVS | `0x00d000` | 12,288 | `0x010000` | Preserve bonds/owner state; exact-original recovery only. |
| Factory slot | `0x010000` | 5,177,344 | `0x500000` | Slot bound, not whole-slot erase permission. |
| Raw candidate | `0x010000` | 602,096 | `0x0a2ff0` | Exact available raw bytes; erase-rounded plan unapproved. |
| Padded trial prefix | `0x010000` | 733,184 | `0x0c3000` | Distinct exact trial bytes/custody prefix. |
| Remaining factory tail | `0x0c3000` | 4,444,160 | `0x500000` | Preserve; not covered by prefix recovery claims. |
| OTA slot 0 | `0x500000` | 5,242,880 | `0xa00000` | Preserve; excluded. |
| OTA slot 1 | `0xa00000` | 5,242,880 | `0xf00000` | Preserve; excluded. |
| State half 0 | `0xf00000` | 524,288 | `0xf80000` | Opaque protected state; captured exact-original recovery only. |
| State half 1 | `0xf80000` | 524,288 | `0x1000000` | Same; aggregate 1 MiB. |

Current HostAdmissionPolicy models one allowed and one protected range. That fixture policy cannot represent all ranges above merely by protecting `ot_state`; complete preservation and erase-rounded policy require a separately scoped production contract.

## Finite acceptance sequence

1. Freeze accepted target/layout, signed exact image, complete allowed/protected/erase plan, side-effect/timing profile and original recovery binding. Missing fields refuse before device access.
2. Host negatives: wrong product/target/hash/signer/version, stale/revoked policy, arithmetic/overlap/protected contact, stale/foreign selection and unresolved storage all submit zero commands.
3. Separately authorized physical selection: fresh enumerate, explicit intended unit, independent identity and wrong-device refusal at atomic command admission.
4. One write attempt; independent byte/protected readback; then exact boot identity/health. Keep bytes verified and startup acceptance distinct.
5. Bounded interruption cases: before submission, possible submission, write/readback/boot timeout/disconnect. No automatic replay; ownership retained until actual command quiescence.
6. New separately confirmed recovery: exact originals, independent route/readback/protected checks and original boot. Unknown original boot must prevent settings replay; failure remains unresolved, not recursively retried.

Host preparation is complete; LUF-0010a physical completion remains blocked by production release/adapter/address-policy/recovery authority. LUF-0009d is an approved independent host prerequisite under LUF-0009 with [validated host evidence](LUF-0009d-DURABLE-HOST-STORE-2026-10-01.md), owner-accepted and depending on accepted LUF-0009b. Existing physical gates are preserved. No V1 progress or public capability change.

Sources: Loader `tasks/V1_REMAINING_PLAN.md:47-53`, `docs/PROJECT_STATUS.md`, `docs/testing/LUF-0009b-HOST-ENGINE-2026-09-29.md:84-92`, `HostReleaseAdmission.cs:31-34,80-89`, `HostUpdateEngine.cs:123-128`, `LoaderUpdateWorkflow.cs:13-25`; OpenTrail target/layout and OT-0101c audit. Artifact hashes were recomputed with the existing isolated Python; no tests executed.
