# Backlog

| ID | Status | Item | Acceptance boundary |
| --- | --- | --- | --- |
| LUF-001 | done | Shared loader product chooser foundation | Buildable WPF shell; exact Trail/Display choices; one revision-bound selection; all operational capabilities false; deterministic host and source-policy tests pass; no hardware access |
| LUF-002 | done | Product-bound offline bundle-candidate inspection | Opaque controller/session context and inspector-only results; exact three-entry canonical schema; independent expansion ceilings; restored caller stream; bounded metadata; image SHA-256 and signature presence; stale, fabricated, cross-controller, cross-product, forged-size, compressed-overflow, and malformed results blocked; signer trust and admission always false; no UI or hardware path |
| LUF-003 | done | Versioned product-provider lifecycle | Immutable one-provider-per-product registry; exact product/provider/version/generation identity; revoke-before-close close-once lease; failed/reentrant/stale transitions contained; immutable project target rules; independently injected exact-bound signer policy that grants no trust or admission; 52 deterministic groups; production provider/trust registries empty; no device or write authority |
| LUF-004 | done | Migrate Trail inspection provider | Exact production `opentrail` provider/version; immutable `heltec_v4_bench` rule pinned to OpenTrail public target-contract SHA-256 and commit; bounded offline inspection publication; Display provider and signer trust absent; all device/write/recovery capabilities false; 58 deterministic groups |
| LUF-004A | done | Add Trail offline candidate-selection workflow | One native file choice opened read-only; no recent-file entry or retained/displayed path; current provider/context/rule publication required; sanitized result/failure surface; signer trust and admission false; lifecycle clearing; 63 deterministic groups; no device or mutation authority |
| LUF-005 | next | Add Display inspection provider | Requires an OpenGauge-owned target manifest and evidence boundary; no inferred compatibility |
| LUF-006 | blocked | Firmware installation and recovery | Requires independent signer verification, exact-device, write/readback/boot/rollback/recovery acceptance for every claimed target |


## Checklist task allocations

Owner-requested ID assignment only; all rows are planned and grant no implementation,
hardware or publication authority. Four-digit numbers identify parent tasks; lowercase
letter suffixes identify child work. Historical IDs, filenames and accepted evidence
remain unchanged. Completed historical parent records are not reopened by child allocation.
These rows own the IDs used by the private checklist. Resolve overlapping planning
and implementation scope before execution; do not perform the same work twice.

| ID | Status | Parent | Task | Acceptance |
| --- | --- | --- | --- | --- |
| LUF-0007 | planned | — | Reconcile supported platforms/devices/transports and existing engine, UI and test evidence | Reconcile existing evidence; define the remaining bounded task and objective validation before implementation approval. |
| LUF-0008 | planned | — | Identify remaining authenticity, signer/revocation, compatibility, offset and protected-region authority gates | Reconcile existing evidence; define the remaining bounded task and objective validation before implementation approval. |
| LUF-0009 | planned | — | Bound approved detection, selection, write/readback, boot, interruption and recovery engine/UI gaps | Reconcile existing evidence; define the remaining bounded task and objective validation before implementation approval. |
| LUF-0010 | planned | — | Validate wrong-device/version, corrupt artifacts, interrupted updates and independently verified recovery | Reconcile existing evidence; define the remaining bounded task and objective validation before implementation approval. |
| LUF-0011 | planned | — | Complete approved packaging/signing/distribution, support docs and acceptance/release evidence | Reconcile existing evidence; define the remaining bounded task and objective validation before implementation approval. |
| LUF-0006a | planned | LUF-0006 | Close canonical Firmware Loader V1 release gate | Canonical Loader authority records accepted signer, device matching, write/verify and recovery evidence. Reconcile before implementation; never use the frozen OpenTrail/tools/windows-loader tree as current evidence. |


## V1 remaining executable task register

Planning only: the remaining approved V1 scope is decomposed below. Registration grants no execution, physical, signing or publication approval. Existing accepted evidence remains authoritative. Full scope and acceptance: [V1 remaining task plan](V1_REMAINING_PLAN.md). Optional product tracks are excluded.

| ID | Status | Parent milestone | Bounded task |
|---|---|---|---|
| LUF-0007a | accepted boundary proposal | LUF-0007 | [Freeze canonical Trail Loader release boundary](../docs/testing/LUF-0007a-V1-RELEASE-BOUNDARY-2026-09-23.md) |
| LUF-0008a | accepted authority proposal | LUF-0008 | [Define Trail target signer and release admission authority](../docs/testing/LUF-0008a-RELEASE-AUTHORITY-2026-09-23.md) |
| LUF-0009a | accepted lifecycle proposal | LUF-0009 | [Specify exact Trail device update and recovery lifecycle](../docs/testing/LUF-0009a-UPDATE-RECOVERY-LIFECYCLE-2026-09-23.md) |
| LUF-0009b | accepted host checkpoint | LUF-0009 | [Approved Trail host admission and adapter engine slice](../docs/testing/LUF-0009b-HOST-ENGINE-2026-09-29.md) |
| LUF-0009c | host/UI checkpoint owner-accepted | LUF-0009 | [Trail device selection and update status UI](../docs/testing/LUF-0009c-UI-CHECKPOINT-2026-09-29.md) |
| LUF-0009d | done (owner-accepted host scope) | LUF-0009 | [Durable process ownership and uncertainty notice](../docs/testing/LUF-0009d-DURABLE-HOST-STORE-2026-10-01.md): existing interfaces, strict post-lock reload and digest-only atomic persistence; 20 new/136 complete groups pass with warning-as-error builds and independent review. Production storage enrollment, adapter, signer/format, devices and distribution remain separate. |
| LUF-0010a | host matrix prepared; physical prerequisites blocked | LUF-0010 | [Exact Trail installation and recovery matrix](../docs/testing/LUF-0010a-HOST-MATRIX-2026-10-01.md); production authority, real adapter, complete preservation policy and current recovery setup remain prerequisites. |
| LUF-0011a | planned | LUF-0011 | Accept Trail Loader package and supported release documentation |
