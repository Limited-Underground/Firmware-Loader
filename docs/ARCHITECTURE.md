# Firmware Loader architecture

This maintained description uses [arc42](https://arc42.org/overview/) and [C4](https://c4model.com/diagrams). It maps implemented source at **d74832b1d2579762fc1aea0634ce409a9e4c21ec**. Product development is frozen indefinitely. [Foundation](FOUNDATION.md) distinguishes implemented, demonstrated, intended and unresolved facts; documentation is not product or physical acceptance.

## 1. Introduction and Goals

Limited Underground Firmware Loader — Preview is one Windows utility for Limited Underground Trail and Limited Underground Display. **Implemented:** production selects a family and inspects a local Trail candidate offline. **Intended, not enabled:** separately accepted installation/recovery for each supported target.

| Stakeholder | Concern |
| --- | --- |
| Operator | Know what inspection proves; never confuse it with installation permission. |
| Target-project owner | Retain target/layout/artifact/compatibility/signer/recovery authority. |
| Fresh maintainer | Find current source, interfaces, tests, limitations and recovery without chat history. |
| Reviewer | Trace claims to source/evidence and identify unresolved acceptance. |

Goals are bounded parsing, stale-authority refusal, sanitized status and reproducible host checks. Section 10 gives observable scenarios; no new performance or device promise is introduced.

## 2. Architecture Constraints

- **Implemented:** .NET 10 WPF, net10.0-windows, nullable C# and warnings-as-errors. [global.json](../global.json) requests stable 10.0.100 with latestFeature roll-forward; the wrapper selects installed stable 10.0.x.
- **Owner constraint:** indefinite November 2 product freeze. Preserve the merged framework baseline; no migration/downgrade, features, devices, signing, release, deployment or merge.
- **Product boundary:** inspection-only production; choice of provider is not trust, device or operation authority.
- **Legal/branding:** [Apache-2.0 source](../LICENSE), [branding terms](../BRANDING.md), Preview and recorded naming/release gates.
- **Evidence limit:** synthetic host tests and WPF renderings are not manual interaction, clean-machine qualification or physical acceptance.

## 3. System Scope and Context

This project is the shared customer-tool boundary. It does not own Limited Underground Trail or Limited Underground Display firmware; historical project/source identifiers are retained as provenance. Product projects remain authoritative for compatibility identifiers, target manifests, image hashes, signatures, recovery procedures, and physical acceptance.

| External boundary | Current interaction and ownership |
| --- | --- |
| Operator / Windows picker | Explicit product choice and one read-only candidate stream; no automatic device selection. |
| Windows filesystem | Untrusted candidate file; path not retained in displayed result. No production operational store is configured. |
| Limited Underground Trail / Limited Underground Display authorities | Target/artifact/compatibility/recovery facts; Trail embeds a target key/hash and public-contract provenance as source constants; there is no build-time retrieval or runtime network API. |
| Physical device, signer service, production recovery store | Unresolved / no production connection. Fixture interfaces imply no real command path. |

The [Structurizr model](architecture/workspace.dsl) owns context/container views and a focused component view distinguishing production from fixtures. One desktop runtime is one container; C# classes are not containers. See [model/export instructions](architecture/README.md).

## 4. Solution Strategy

Separate target-owned facts from application-owned trust. The WPF composition root explicitly injects the production Trail inspection provider and empty signer trust. Immutable exact contexts bind publication to a current session/provider activation. Switching or closing revokes prior authority. Bounded stream parsing and fixed diagnostic categories protect data and status.

The internal fixture admission/engine/store path supports synthetic validation, not production composition. Mathematical preservation and byte-binding checks authenticate neither geometry nor signer/device facts. Reuse existing contracts rather than treating fixture success as operational approval.

## 5. Building Block View

| Responsibility | Source | Tests/evidence |
| --- | --- | --- |
| Catalog/session | [catalog](../src/LimitedUnderground.FirmwareLoader/LoaderProductFamily.cs), [controller](../src/LimitedUnderground.FirmwareLoader/LoaderSessionController.cs) | [core runner](../tests/LimitedUnderground.FirmwareLoader.Tests/Program.cs) |
| Provider/lease/target/trust | [contract](../src/LimitedUnderground.FirmwareLoader/LoaderProviderLifecycle.cs), [production registry](../src/LimitedUnderground.FirmwareLoader/OpenTrailInspectionProvider.cs) | [lifecycle](../tests/LimitedUnderground.FirmwareLoader.Tests/ProviderLifecycleTests.cs), [production provider](../tests/LimitedUnderground.FirmwareLoader.Tests/OpenTrailInspectionProviderTests.cs) |
| Candidate inspection | [inspector](../src/LimitedUnderground.FirmwareLoader/FirmwareBundleCandidateInspector.cs), [workflow](../src/LimitedUnderground.FirmwareLoader/OfflineBundleInspectionWorkflow.cs) | [boundary](../tests/LimitedUnderground.FirmwareLoader.Tests/TrailReleaseAdmissionBoundaryTests.cs), [workflow](../tests/LimitedUnderground.FirmwareLoader.Tests/OfflineBundleInspectionWorkflowTests.cs) |
| WPF surface | [MainWindow](../src/LimitedUnderground.FirmwareLoader/MainWindow.xaml.cs), [update workflow](../src/LimitedUnderground.FirmwareLoader/LoaderUpdateWorkflow.cs) | [UI](../tests/LimitedUnderground.FirmwareLoader.Tests/LoaderUpdateUiTests.cs) |
| Synthetic finite engine/admission/store | [admission](../src/LimitedUnderground.FirmwareLoader/HostReleaseAdmission.cs), [engine](../src/LimitedUnderground.FirmwareLoader/HostUpdateEngine.cs), [store](../src/LimitedUnderground.FirmwareLoader/HostOperationStore.cs) | [engine](../tests/LimitedUnderground.FirmwareLoader.Tests/HostUpdateEngineTests.cs), [store](../tests/LimitedUnderground.FirmwareLoader.Tests/HostOperationStoreTests.cs) |
| Immutable ranges/payload/preflight | [plan](../src/LimitedUnderground.FirmwareLoader/HostPreservationPlan.cs), [binding](../src/LimitedUnderground.FirmwareLoader/HostPreservationPayloadBinding.cs), [preflight](../src/LimitedUnderground.FirmwareLoader/HostPreservationBindingPreflight.cs) | [preflight tests](../tests/LimitedUnderground.FirmwareLoader.Tests/HostPreservationBindingPreflightTests.cs), [contract](HOST-PRESERVATION-BINDING-ADMISSION-CONTRACT.md) |

### Retained first-screen contract

`LoaderProductCatalog` contains exactly two product families with stable engineering keys:

| Engineering key | Public working name | Current production provider state |
| --- | --- | --- |
| `opentrail` | Limited Underground Trail | Lifecycle-v1 offline-inspection provider active for exact `heltec_v4_bench` target rules |
| `opengauge` | Limited Underground Display | Provider and accepted target manifest do not exist |

The display names are presentation only. They are not wire values, schema keys, cryptographic context, or hardware identifiers.

`LoaderSessionController` owns one scalar selection and monotonically increasing revision. Selecting a different product or returning to the chooser invalidates the previous revision. Unknown keys fail without mutation. Reselecting the exact current product is a no-op.

Every current snapshot reports all operational capabilities false:

- connected-device inspection;
- firmware-bundle selection;
- device/bundle matching;
- firmware writing; and
- recovery.

These operational capabilities remain false. A separate `OfflineBundleInspectionAvailable` projection becomes true only for an active exact Trail provider lease; it grants access solely to the stream-based inspector and never implies signer, device, admission, or installation authority.

### Retained provider lifecycle contract

The controller accepts an immutable provider-factory registry with at most one exact provider per catalog product. Registration requires a sanitized lowercase provider key and contract version `1`. Signer-trust policies are injected through a separate application-owned registry and must bind to the exact same product, provider key, and contract version.

Each activation receives an opaque token and nonzero monotonically increasing provider generation. A product may remain visibly selected when its provider is absent or activation fails, but no offline-inspection context can be minted without an active exact lease.

A product switch, chooser return, or owner disposal:

1. revokes and detaches the active lease and its rule/trust state;
2. advances the session revision when the selection changes;
3. closes the detached provider outside the state lock through an interlocked close-once wrapper; and
4. only then permits a replacement provider to open.

A failed close blocks replacement activation. Null, throwing, or identity-mismatched factories remain providerless, and any returned rejected provider is closed once. Transition state rejects reentrant selection, chooser return, context minting, and result publication. Reentrant owner disposal aborts activation and closes a newly returned candidate once. `MainWindow` disposes the controller on window close.

Providers expose only their exact open context, immutable project-owned target rules, and `Close`. The interface contains no enumeration, connection, write, erase, reset, reboot, recovery, signer-trust, or admission method. Provider exception text is never published.

Target rules and signer trust are independent authorities. Rules bind exact target keys to a project-owned manifest identity and source revision. A separately injected signer policy may identify signer IDs and configured public-key fingerprints, but configuration alone never sets `SignerTrusted` or `AdmissionAllowed`; real cryptographic verification and revocation are later gates.

Production registers one provider factory for product/provider key `opentrail` and contract version `1`. Its immutable rule set contains only `heltec_v4_bench`; both the rule identity and source revision pin SHA-256 `ec818efab9a14ce4f0900068c9474acfe2577d74e2e39fa4850f3ff0567e9776` for the Git blob bytes of Limited Underground Trail's historical public target contract at commit `a327104ac67a3f5918a8b0191c96dceb05b5399b`. The provider owns no file, device, transport, signer, admission, or mutation adapter and closes without external work.

Production signer trust remains empty. Display remains providerless. The default controller constructor also remains empty for isolated consumers and lifecycle tests; the WPF composition root explicitly injects the production registry.

## 6. Runtime View

### Production inspection

1. Explicit family choice activates an exact provider lease.
2. Current context and read-only candidate stream enter bounded inspection.
3. Structure/CRC/canonical manifest/image checks produce a fixed outcome.
4. Exact context/lease is rechecked before publishing successful inspection.
5. Family/attempt/chooser/close changes invalidate or clear the result; trust and installation remain unavailable.

`FirmwareBundleCandidateInspector` owns one product-neutral candidate schema. The archive must contain exactly `manifest.json`, `image.bin`, and `manifest.sig`; manifest property order and encoding are canonical; archive, manifest, image, signature, identifier, and integer sizes are bounded. The manifest product key must be exactly `opentrail` or `opengauge`.

A caller cannot fabricate accepted authority: the controller mints an immutable context only from an active provider lease. The context binds opaque controller and activation tokens, exact context reference, selected product, session revision, provider key, contract version, provider generation, target-rule source revision, and optional signer-trust source revision. Result construction is internal to the inspector and retains the exact context object.

Publication requires every binding to remain current, verified structure/digest/signature presence, an exact product match, an exact case-sensitive target in the active project-owned rule set, and false signer-trust/admission flags. A switch, chooser return, provider replacement, failed activation, disposal, fabricated public object, or matching numeric revision from another controller fails.

The archive may use stored or deflated entries. The entire candidate is limited to 20 MiB, and each entry is independently read with an exact maximum-plus-one ceiling: 4 KiB manifest, 16 MiB image, and 384-byte signature. The expanded byte count must equal ZIP metadata, so forged central-directory sizes fail closed. The caller's original stream position is restored in `finally` after success or failure.

Inspection verifies structure, each entry's ZIP CRC-32, image length, SHA-256, and nonempty fixed-size signature presence. It has no device input, admission output, or operation authority.

The .NET archive reader does not verify CRC-32 values, so the inspector recomputes them after each bounded read; damage to any entry, including the presence-only signature bytes, is refused rather than published. Every failure caused by the candidate's own bytes, including archive-library failures and invalid UTF-8 in the manifest, becomes one fixed `FirmwareBundleCandidateRejection` reason. `TryInspect` returns that reason; `Inspect` keeps its `InvalidDataException` contract and messages. A failure to read the caller's stream surfaces as `IOException` and is never reported as a verdict on the candidate.

The WPF composition root enables a single-file chooser only when `OfflineBundleInspectionAvailable` is true. The window disables recent-file registration, opens the selected file with `FileMode.Open`, `FileAccess.Read`, and `FileShare.Read`, and passes only the stream to `OfflineBundleInspectionWorkflow`. The workflow owns no path or filename field, catches malformed candidate data into fixed messages, rechecks current controller publication authority, and returns immutable sanitized display fields. The UI never displays the path or raw exception text and clears every rendered result on a new attempt, product change, chooser return, or close. Canceling the native picker leaves the prior inspected result unchanged because no new candidate was attempted.

Each outcome carries a fixed `OfflineBundleInspectionStatus`. Refusals distinguish a file that could not be read, a non-candidate or unreadable archive, the archive and entry size limits, a damaged archive (CRC-32 or recorded size), a rejected manifest, an image length or SHA-256 integrity failure, an empty signature, a different product, an unsupported target, and a product-session change during inspection. The controller's diagnostic classifier only explains a publication refusal under the same lock; it never grants or widens publication. Refused outcomes still withhold product, target, generation and digest. A published outcome additionally shows the release generation and the verified image SHA-256 as lowercase hexadecimal; displaying a digest grants no signer trust or admission.

### Host-only update and recovery

Tests inject a fake adapter, fixture policy/profile and journal into the actual host engine. Confirmation is one-use; current selection/policy/inspection/cancellation are rechecked before commands and phases. Durable uncertainty precedes possible mutation. Current code may withdraw only the live owner's own marker after proving no adapter command was submitted. After possible mutation, cancellation/failure retains uncertainty and ownership until quiescence; exact readback, boot and recovery remain independent gates. See [lifecycle](testing/LUF-0009a-UPDATE-RECOVERY-LIFECYCLE-2026-09-23.md) and [actual engine tests](../tests/LimitedUnderground.FirmwareLoader.Tests/HostUpdateEngineTests.cs). This is not a physical route.

## 7. Deployment View

**Implemented:** one Windows WPF process; no product server, network endpoint or device agent. Current output is framework-dependent and requires a matching Windows .NET Desktop runtime. SDK/PowerShell are development prerequisites, not an accepted installer. [Testing](TESTING.md) and [Recovery](RECOVERY.md) declare exact inputs.

**Demonstrated remotely:** the exact-source [PR #18 hosted run](https://github.com/Limited-Underground/Firmware-Loader/actions/runs/41523963520) passed on disposable Windows Server 2025 with SDK10.0.403; [Recovery](RECOVERY.md) binds the synthetic merge, source tree, sanitized artifact and independent review. This does not qualify the proposed customer Windows platform or establish a local run. **Unresolved:** standard-user clean-machine/package/support acceptance.

The local/private engineering handbook is documentation presentation, not a Loader runtime service. Bind previews only to loopback; exclude credentials, captures and private records.

## 8. Crosscutting Concepts

| Concept | Current rule |
| --- | --- |
| Authority | Exact controller/context/activation identity and generations; matching numbers do not reuse a revoked lease. |
| Integrity versus trust | CRC/canonical encoding/SHA-256 verify structure/bytes. Signature presence is not signer authentication; production trust is empty. |
| Privacy | Fixed statuses; no candidate path, raw exception, port/device identifier or private key in displayed results. |
| Preservation | Immutable half-open spans and exact copies are mathematical/host facts, not authentic target geometry. |
| Recovery | Digest-only fixture uncertainty/originals are not production store enrollment or physical recovery. |
| Evidence | Source, SDK, stage/final receipts, binary hashes and validation layer stay distinct. Source restoration is not firmware restoration. |

Selected interface versions and standard/custom boundaries: [Interfaces](INTERFACES.md).

## 9. Architectural Decisions

| Existing decision | Rationale / owning record |
| --- | --- |
| Standalone common utility | Neither product owns the other's firmware or evidence; retain target-project authority. |
| Preview/inspection-only release boundary | [LUF-0007a](testing/LUF-0007a-V1-RELEASE-BOUNDARY-2026-09-23.md) |
| Independent trust/release/device gates | [LUF-0008a](testing/LUF-0008a-RELEASE-AUTHORITY-2026-09-23.md), current Project #3 |
| Finite lifecycle and independent recovery | [LUF-0009a](testing/LUF-0009a-UPDATE-RECOVERY-LIFECYCLE-2026-09-23.md) |
| Cooperating-process durable uncertainty | [LUF-0009d](testing/LUF-0009d-DURABLE-HOST-STORE-2026-10-01.md) |
| Stable .NET 10 baseline | Merged [PR #16](https://github.com/Limited-Underground/Firmware-Loader/pull/16) and [integration #17](https://github.com/Limited-Underground/Firmware-Loader/pull/17); no new migration here |
| Freeze / shared documentation approach | November 2 owner foundation decision; [current reading path](FOUNDATION.md) |

No new protocol or invented ADR identities. Concrete target/release/trust inputs and adapter/storage/production composition remain unresolved under their existing Project owners. The [current V1 acceptance table](FOUNDATION.md#existing-v1-acceptance-still-required) selects those existing gates; [third-party records](THIRD_PARTY.md) separate source licensing from package/naming clearance.

## 10. Quality Requirements

| Scenario | Observable outcome | Evidence / acceptance limit |
| --- | --- | --- |
| Damaged/oversized/noncanonical candidate | Sanitized fixed refusal | Inspector/workflow tests; not signer authentication |
| Product/provider changes during inspection | Old result cannot publish | Lifecycle/workflow tests; not physical reconnect proof |
| Retained bytes disagree with plan/release | Pure preflight refuses without authority | Preservation/preflight tests; fixture policy only |
| Host command may partly complete | Preserve uncertainty/ownership until quiescence | Engine/store subprocess tests; synthetic commands only |
| Working source is lost | Retrieve pinned source into new directory; run declared gate | [Recovery](RECOVERY.md); protected custody and exact binaries separate |

Input/link checks and official model parsing support documentation consistency; they cannot establish truth. Independent fresh-reader review checks stakeholder concerns, view consistency, rationale and remaining gaps using [ISO/IEC/IEEE 42010:2022](https://www.iso.org/standard/74393.html) as a reference. No conformance/certification claim is made.

## 11. Risks and Technical Debt

- Production signer/key custody, revocation/freshness, schema, authentic geometry, exact adapter, enrolled store and physical recovery remain separate gates.
- Display remains a chooser option without a provider/target contract.
- Source-only backups exclude protected holdings; separate internal disks are not off-laptop protection. See [Recovery](RECOVERY.md).
- Older local framework overlays are retained, not integrated or accepted as current main.
- Manual operator, clean-machine packaging and naming/release acceptance remain unresolved.
- The current freeze supersedes old resumption language; documentation does not resume development.

Product selection can never be reused as write approval. A future writer requires, at minimum, exact product provider, exact received-device identity, exact target manifest, admitted signer/release generation, complete address/file/hash plan, bounded attempt count, readback verification, boot confirmation, and accepted recovery plan. Owner authorization remains a separate operation-scoped gate.

## 12. Glossary

| Term | Meaning |
| --- | --- |
| Limited Underground Firmware Loader — Preview | Current common Windows application and acceptance boundary. |
| Limited Underground Trail / Limited Underground Display | Exact visible choices; opentrail / opengauge remain engineering identifiers. |
| Inspection | Bounded candidate structure/integrity checks, without admission. |
| Provider lease | Opaque activation/context identity plus generation; lifecycle invalidation applies. |
| Admission | Separate trust/policy decision; not enabled in production. |
| Host fixture | Synthetic envelope, adapter, profile/store used to test host behavior. |
| Recovery | Qualify source reconstruction, synthetic restart or authorized physical restoration; they are not interchangeable. |
| Container (C4) | Application/runtime or data store, not a C# class or necessarily a Docker container. |
