# Current V1 scope and engineering reading path

**November 2, 2026: product development is frozen indefinitely.** Only authorized preservation, documentation and isolated source recovery continue. This page selects the existing V1 requirements and current evidence; it grants no implementation, device, signing, release or merge authority. [Project #3](https://github.com/orgs/Limited-Underground/projects/3) owns live task status; historical backlog labels do not select work.

## Product and accepted V1 boundary

Limited Underground Firmware Loader — Preview is a Windows utility intended to inspect and, only after separately accepted gates, install firmware for supported Limited Underground products. The existing base-V1 installation/recovery scope is **Limited Underground Trail**, under LUF-0006 through LUF-0011a. Limited Underground Display remains an exact visible chooser option, but its provider/installation is outside that base release and is not a dependency of it. Server, gateway, repeater and other product additions are outside this V1 scope.

[LUF-0007a](testing/LUF-0007a-V1-RELEASE-BOUNDARY-2026-09-23.md) records the owner-accepted boundary proposal: initial Windows 11 Pro 25H2 x64 scope, one Heltec WiFi LoRa 32 V4 / HTIT-WB32LAF V4.2 candidate and proposed direct USB/ESP32-S3 ROM-bootloader transport. These are planning/qualification boundaries, not installation support. Its November 2 amendment accepts **.NET 10 LTS**; the original .NET 8/tooling observations remain history. Windows Server 2025 hosted tests do not qualify the proposed customer Windows installation. No accepted deployable firmware, physical adapter, package or signed installer is selected by this documentation.

Source facts below map to merged .NET 10 main **d74832b1d2579762fc1aea0634ce409a9e4c21ec** (PRs #15–#17). Topic documentation remains a candidate until reviewed and merged. Technical keys `opentrail`, `opengauge` and `heltec_v4_bench`, source symbols, filenames and historical evidence identities are preserved; they are not public product branding.

## Implemented, demonstrated and incomplete

| Layer | Current result | Evidence and limit |
| --- | --- | --- |
| Production chooser/provider | Exact two product choices; revision/lease-bound lifecycle. Only Limited Underground Trail has an inspection provider. | [Architecture](ARCHITECTURE.md), production registry and lifecycle tests. Selection is not device or signer authority. |
| Production inspection | One explicitly chosen read-only local candidate; bounded archive/canonical manifest, CRC-32, image length/SHA-256 and signature-presence checks; sanitized results and lifecycle clearing. | [Interfaces](INTERFACES.md). Presence/integrity is not authenticity; production signer trust/admission remain false. |
| Synthetic host work | Fixture authenticity/admission, finite engine/update UI, durable uncertainty, immutable multi-range preservation, payload snapshots and pure consistency preflight. | [Host matrix](testing/LUF-0010a-HOST-MATRIX-2026-10-01.md), [binding contract](HOST-PRESERVATION-BINDING-ADMISSION-CONTRACT.md), source/test links in Architecture. No production adapter, engine factory or storage enrollment follows from these tests. |
| Windows source reconstruction/build | Fresh remote source retrieval and exact-source hosted run41523963520 passed both warning-as-error Release builds/full host process with SDK10.0.403, PowerShell7.6.9; environment restored. | [Recovery](RECOVERY.md) owns checkout/artifact identities, isolation and independent PASS. No duplicate local build is required. Numeric counts/renderings/raw receipts/output hashes are absent from the sanitized artifact and are not claimed. |
| Architecture presentation | All twelve arc42 sections; C4 context/container/component views, officially validated/exported with pinned Structurizr. | [Model and current export pins](architecture/README.md). Syntax/rendering is not product acceptance. ISO/IEC/IEEE 42010:2022 is a review reference, with no conformance claim. |
| Supported installation/release | Incomplete. Production remains inspection-only; physical, package, clean-machine, signing and release gates remain open. | Existing acceptance below. Host success is never physical acceptance. |

## Existing V1 acceptance still required

| Existing gate | Required result still missing from production/physical acceptance | Authority |
| --- | --- | --- |
| LUF-0008a / concrete inputs LUF-0008b | Exact target/layout/compatibility and release bytes; accepted production envelope, signer custody/scope, authenticated revocation/freshness and generation policy. Candidate JSON and synthetic fixture RSA envelope are insufficient. | [Release authority](testing/LUF-0008a-RELEASE-AUTHORITY-2026-09-23.md); target-project owner supplies facts, Loader independently enforces trust. |
| LUF-0009a/b/c | Real identity-bound adapter and finite supported profile; explicit visible unit selection, current artifact/selection binding, truthful write/readback/boot/uncertain outcomes and separately accepted production composition. | [Lifecycle](testing/LUF-0009a-UPDATE-RECOVERY-LIFECYCLE-2026-09-23.md). Accepted host/fixture checkpoints are retained, not repeated as new implementation. |
| LUF-0009d / production storage | Accepted root/account/ACL/enrollment, custody and recovery policy for production persistence; cooperating-process host storage alone does not supply these. | [Durable host-store contract](testing/LUF-0009d-DURABLE-HOST-STORE-2026-10-01.md), current Project children. November 2 no-command-submitted withdrawal amendment remains distinct from any possibly submitted command. |
| LUF-0010a prerequisites | Complete target-owned protected/allowed/erase geometry and preservation rules; independently accepted originals/recovery coverage for every affected span. Mathematical plans and copied bytes authenticate no target facts. | [Prerequisite matrix](testing/LUF-0010a-HOST-MATRIX-2026-10-01.md). Existing preservation/binding/preflight validators are completed host work. |
| LUF-0010a physical cases | Exact current unit/artifact/operation grant; wrong-device refusal, exact write/readback, boot confirmation, interruption and independently demonstrated restoration for each claimed capability. | Target owner and separately authorized physical reviewer. No hardware grant is active. |
| LUF-0011a | Exact package/capability/support documentation, standard-user clean-machine/operator acceptance, signer/distribution custody and immutable release identity. | [Remaining V1 plan](../tasks/V1_REMAINING_PLAN.md), [branding](../BRANDING.md) and [third-party records](THIRD_PARTY.md). No installable release is accepted. |

These are the existing scope/gaps, not a new approval system. No task is marked complete by this page. Current details and dependencies remain on the owning Project items.

## Decisions and next step

Unresolved inputs are recorded once through their owning authorities: production trust/target/recovery inputs belong to LUF-0008a/b; storage enrollment belongs to the existing LUF-0009 children; release/package and branding clearance belong to LUF-0011a and BRANDING.md. Recommended resolution is to obtain those exact owner-approved inputs in their existing tasks, retain inspection-only/Preview while absent, and avoid choosing keys, offsets, a package format or a legal result here.

**Next authorized step now:** fresh reading-path review of this coherent documentation delta, followed by the coordinator's exact-delta release before any commit/push through existing draft PR #18. No duplicate PR or merge. Shared handbook presentation is owned separately; it consumes pinned source, not a second specification. After an explicit decision to resume product work, reconcile the existing LUF-0008b concrete-input dependency before choosing dependent engineering work; this page does not start it.

## Reading route and reusable procedures

| Need | Owning record |
| --- | --- |
| Product behavior and safe operator use | [README](../README.md) |
| Portable rules / tool-skill routing | [CONTRIBUTING](../CONTRIBUTING.md), [agent adapter](../AGENTS.md) |
| Current task/evidence handoff | [Project status](PROJECT_STATUS.md), Project #3 |
| Components, runtime/refusal flows and decisions | [Architecture](ARCHITECTURE.md), [C4 model](architecture/README.md) |
| Candidate versus fixture contracts / security boundaries | [Interfaces](INTERFACES.md) and its selected contracts |
| Exact build/full/focused test procedure and diagnostics | [Testing](TESTING.md) |
| Source/toolchain/reference artifacts and clean recovery | [Recovery](RECOVERY.md) |
| Preservation, failed-run retention, scoped cleanup | [Maintenance](MAINTENANCE.md) |
| Source license, names, dependency and distribution limits | [LICENSE](../LICENSE), [BRANDING](../BRANDING.md), [third-party records](THIRD_PARTY.md) |
| Historical requirements/results | [Backlog](../tasks/BACKLOG.md), [remaining V1 plan](../tasks/V1_REMAINING_PLAN.md), [progress log](PROGRESS_LOG.md), linked dated evidence |

**Implemented** identifies source; **demonstrated** identifies an executed check/revision; **intended** identifies unimplemented scope; **unresolved** identifies a missing decision/result. Reuse exact unchanged evidence. Do not rerun a host suite for a text-only change, infer actual counts, promote fixture success to physical acceptance or silently retire failed evidence.

Private manifests, raw diagnostics, original-device data, credentials and machine-specific paths stay in established custody. A handbook imports an explicit hash-pinned allowlist; it is not a recursive source mirror, public website or secret store.
