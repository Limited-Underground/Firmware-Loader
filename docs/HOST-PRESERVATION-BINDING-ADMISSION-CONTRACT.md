# Host preservation binding to fixture admission: preflight contract

Status: original design contract dated October 19, 2026; implementation status reconciled October 22, 2026. The pure host checker is now implemented in [HostPreservationBindingPreflight.cs](../src/LimitedUnderground.FirmwareLoader/HostPreservationBindingPreflight.cs), with 24 registered deterministic groups in [HostPreservationBindingPreflightTests.cs](../tests/LimitedUnderground.FirmwareLoader.Tests/HostPreservationBindingPreflightTests.cs), and was merged in [PR #11](https://github.com/Limited-Underground/Firmware-Loader/pull/11). The historical proposal wording below describes the design boundary, not an outstanding need to implement that checker again. The checker assesses fixture consistency only; engine integration, production admission/trust, device operations and physical acceptance remain outside this implemented scope. The current complete host-validation layer is recorded in [Project status](PROJECT_STATUS.md). Host consistency is not physical acceptance.

## Problem and one intended consumer

[PR #6](https://github.com/Limited-Underground/Firmware-Loader/pull/6) supplies immutable mathematical preservation plans. [PR #8](https://github.com/Limited-Underground/Firmware-Loader/pull/8) associates private synthetic payload snapshots with exact plan write ranges. [PR #9](https://github.com/Limited-Underground/Firmware-Loader/pull/9) and the [LUF-0010a board checkpoint](https://github.com/orgs/Limited-Underground/projects/1?pane=issue&itemId=270977114) record those host results and remaining gates. None connects the new binding to release admission or the engine.

The one proposed consumer is a future pure host-fixture preflight between an existing admitted fixture release and a preservation payload binding, before a later separately approved engine integration could use them together. It answers only: do these retained bytes, ranges and fixture-policy snapshots describe one consistent single-image fixture attempt? It must not mint a release, confirmation, selection, operation token or installation authority.

The existing engine accepts one image. This contract therefore refuses zero or multiple payloads; it does not concatenate images, pick the first entry, split an engine operation or design multi-payload execution. Multiple allowed/protected classification ranges remain supported in the mathematical plan.

## Current source facts and inspection boundary

Source was read on 2026-10-19 from the approved C: working copy; the four implementation blobs below were also compared with GitHub main and matched. These are source observations, not a new build/test run or evidence of physical behavior.

| Source | Existing responsibility / observed symbol | Inspected Git blob |
|---|---|---|
| [HostReleaseAdmission.cs](../src/LimitedUnderground.FirmwareLoader/HostReleaseAdmission.cs) | `HostReleaseAdmission.Admit`, `HostReleaseManifest`, `HostAdmissionPolicy`, `HostAdmittedRelease.CopyImage`; internal `LUF-host-fixture-envelope-v1` signature and single-range policy | `8ecdc6fff3799a8977471a89679cd7f34c439bb6` |
| [HostPreservationPlan.cs](../src/LimitedUnderground.FirmwareLoader/HostPreservationPlan.cs) | `TryCreate`, immutable `Writes`, `EraseRanges`, `AllowedRanges`, `ProtectedRanges`, `AddressSpaceLength`, `EraseBlockSize` | `8f5646d02605947928bda74ad995e56236a9689d` |
| [HostPreservationPayloadBinding.cs](../src/LimitedUnderground.FirmwareLoader/HostPreservationPayloadBinding.cs) | `TryCreate`, exact `Plan` reference, read-only `Payloads`, `CopyBytesAt`; 256-entry and 16 MiB aggregate host limits | `4ce3ea6a026a0c31d3d6bf2a5dfbd5bcc6fd5ee6` |
| [HostUpdateEngine.cs](../src/LimitedUnderground.FirmwareLoader/HostUpdateEngine.cs) | `CheckBinding`, `Select`, `Confirm`, `RunAsync`, `Submit`; current mutable-authority and command-admission boundaries | `26ca363396cf3e13f7641f7927ea233c0e535f55` |

`HostAdmissionPolicy` remains the retained one-allowed/one-protected-range fixture record. `HostPreservationPlan` does not authenticate geometry or target facts. `HostPreservationPayloadBinding` does not authenticate an artifact, signer or policy. `HostAdmittedRelease` has an internal constructor, so its type alone is not an unforgeable proof of successful admission. Current production remains inspection-only.

## Proposed inputs, output and ownership

The future pure checker receives four explicit inputs: a non-null existing `HostAdmittedRelease`, a non-null `HostPreservationPayloadBinding`, a non-null current `HostAdmissionPolicy` snapshot supplied by the host operation owner, and the owner's current nonnegative monotonic-time value in the same units/domain as that fixture policy's `ValidUntil`. It does not obtain time, policy, images or device data itself.

The fixture harness must first obtain the release through the real existing `HostReleaseAdmission.Admit`, using its existing synthetic signing setup. The checker has no signature/key input and cannot re-prove signature verification or admission provenance. Directly constructed inconsistent release objects may be used only as negative robustness fixtures. Even a structurally matching object yields only consistency, never a trust assertion.

Output is conceptually either `FixtureConsistent` or `Refused` with a bounded, non-identifying reason category. These are proposed meanings, not existing source enums or a selected C# API. A positive outcome must not expose mutable bytes or act as cached authority. Exceptions or unavailable inputs cannot be translated into a positive outcome. No serializer, persistence format or canonical whole-plan identity is introduced.

| Fact | Owner / required treatment |
|---|---|
| Signature, fixture product/target, target/layout digests, compatibility/generation, signer/key digest and policy revision binding | Existing `HostReleaseAdmission.Admit`; retain every current check. Preflight compares consistency and current supplied policy; it does not replace admission or populate production trust. |
| Retained release image | `HostAdmittedRelease.CopyImage`; take one defensive copy for this evaluation. Never reopen a path or accept replacement bytes from caller metadata. |
| Validated plan and payload snapshots | Existing plan/binding factories; consume `binding.Plan`, `binding.Payloads` and one `CopyBytesAt(0)` copy. Do not accept a separately substituted plan or trust hashes without checking retained bytes. |
| Fresh current policy and clock value | Host operation owner supplies the snapshot and time for each evaluation. Equality follows the record value equality already used by `CheckBinding`, not reference equality or selected-field equality. |
| Session/provider/context, current inspection, selection/connection identity and generation, cancellation, profile, journal/recovery state and command ownership | Existing `HostUpdateEngine.CheckBinding`, confirmation and submission boundaries. These remain mandatory and outside the pure checker. |

## Ordered consistency and refusal rules

All ranges below are half-open. Use checked unsigned arithmetic for endpoints and rounding, with explicit positive lengths and bounds; arithmetic failure refuses. These checks are additional constraints for a future host-fixture comparison and do not change the retained admission behavior today.

1. **Input boundary.** Refuse missing release, binding or current policy, negative time, unavailable retained bytes or any inability to evaluate the bounded inputs. Use the immutable plan actually retained by the binding.
2. **Single-image boundary.** Require exactly one `Plan.Writes` range, one `Plan.EraseRanges` range and one payload metadata entry. Do not select, concatenate or discard entries to satisfy this condition.
3. **Fixture policy boundary.** Require current policy record value equality with `release.Policy`, positive policy revision, not revoked, and `0 <= now < ValidUntil`. Require manifest policy revision, target/layout digests, compatibility, recovery digest and signer to agree with the retained/current fixture policy; generation must be nonzero and satisfy its minimum. Require the existing fixture schema/product/target constants from `Admit`, without defining new identifiers. This consistency recheck is not signature verification and does not waive any existing `Admit` condition.
4. **Exact raw image boundary.** The sole plan-write offset, payload offset and manifest offset must agree. Its length, payload metadata length, copied payload length, copied admitted-image length and positive manifest image length must agree, within the retained 16 MiB host bound. Compare copied payload and copied admitted-image bytes exactly. Recompute SHA-256 from both retained copies; both must equal the manifest `ImageDigest` and payload `Sha256` using the current ordinal uppercase-hex convention. Do not pad, truncate, normalize, substitute erase bytes or infer equivalence from matching metadata alone.
5. **Geometry boundary.** Apply all rules in the next section using the retained plan and current equal policy. A mathematically valid plan can still fail fixture compatibility. Never repair or enlarge a policy/plan during evaluation.
6. **Outcome boundary.** Report only fixture consistency after every check passes. Otherwise refuse. Evaluation performs zero file/network/device I/O and submits zero adapter commands; it cannot confirm, start, resume, mark complete or recover an operation.

## Geometry compatibility with the retained fixture

| Check | Required comparison / refusal |
|---|---|
| Explicit geometry | Retain the plan's positive address-space length and validated power-of-two erase block. Require `Plan.EraseBlockSize == policy.EraseBlock`. No flash capacity or erase geometry is inferred from a model name, target key or manifest. |
| Policy endpoints | Compute allowed and protected endpoints in checked `ulong`; lengths must be positive and both intervals must fit within `Plan.AddressSpaceLength`. The two policy intervals must be disjoint for this future preflight. Ambiguous/overlapping policy declarations refuse rather than silently subtracting protection. |
| Allowed classification | Every plan allowed interval must be contained in the fixture policy's allowed interval and disjoint from its protected interval. A plan may conservatively protect additional bytes inside the policy's allowed interval; it may not enlarge writable authority. |
| Protected classification | The union of plan protected intervals must completely cover the fixture policy's protected interval. The already-validated plan must still completely classify its declared address space. Adjacent protected intervals may jointly cover it; a single overlapping interval is insufficient evidence of full coverage. |
| Retained admission alignment | Manifest/write offset must be aligned to the matching erase block, as the current fixture `Admit` requires. A valid mathematical plan with an unaligned raw write refuses at this fixture compatibility boundary. |
| Exact erase footprint | Recompute the write end and rounded erase end with checked arithmetic; avoid an overflowing `end + block - 1` intermediate. Require the sole stored erase interval to equal that recomputed footprint, remain inside the plan and policy allowed ranges, and avoid every protected plan/policy byte. Require the computed write/rounded end to fit the existing `Admit` boundary of `uint.MaxValue`; do not narrow by a wrapping cast. |

The policy remains a fixture input, not proof that a real target accepts that address-space size, erase plan or preservation declaration. A broader production policy, authenticated target/layout mapping and accepted-release-to-plan identity remain separately unresolved. In particular, payload SHA-256 equality does not authenticate the plan's geometry.

## Existing mutable-authority checks remain mandatory

`HostUpdateEngine.CheckBinding` currently checks controller/session publication, exact current inspection reference, manifest target/image/generation, equality of current/retained policy, revocation/time, exact selection identity and generation, the fixture adapter discriminator, positive finite profile bounds and journal/recovery state. `Select` uses that check before Ready, `RunAsync` rechecks before and after ownership acquisition and between phases, and `Submit` checks again under `commandGate` before adapter admission. The adapter's existing atomic selection comparison remains separate.

A future integration must preserve those checks, the one-use confirmation, cancellation behavior, durable uncertainty ordering and ownership until actual command quiescence. It must not replace them with a prior `FixtureConsistent` outcome. Changed policy, time, context, inspection, selection or cancellation still refuses at the existing owner boundary even when immutable bytes remain mathematically consistent.

This document names a potential host preflight consumer; it selects no integration hook and modifies no engine state transition. A separately accepted implementation plan is required before adding the pure checker or integrating it. Recovery still requires its own exact originals, binding and current authorization; this proposal does not design multi-image or recovery execution.

## Synthetic test cases for a future implementation

These are design cases, not tests executed by this document. Use the real existing admission path and synthetic signing setup from [HostUpdateEngineTests](../tests/LimitedUnderground.FirmwareLoader.Tests/HostUpdateEngineTests.cs) for the positive admitted-release fixture. No production keys, firmware files, ports or private identifiers are inputs.

A concrete proposed positive geometry is address space `[0,128)`, erase block `16`, fixture allowed `[16,80)` and protected `[96,128)`, plan allowed `[16,48)` plus `[48,80)`, and plan protected `[0,16)`, `[80,96)` plus `[96,128)`. The one raw write is `[32,49)` containing 17 synthetic bytes `0x00` through `0x10`; its erase footprint is `[32,64)`. The fixture manifest/policy must be generated consistently and actually admitted by the existing fixture verifier. These numbers are synthetic host fixtures, not hardware facts.

| Case | Input/change | Expected result / responsible boundary |
|---|---|---|
| P01 | Positive geometry and admitted/bound exact 17-byte image, equal current policy, valid time | `FixtureConsistent`; zero I/O/commands and no operational authority. |
| P02 | Split/reorder allowed/protected input classifications without changing their valid unions; retain one exact write | Same consistency result after existing factories normalize ranges. |
| N01 | Missing release, binding or current policy; unavailable copied bytes | Refused at input boundary. |
| N02 | A separately valid two-write/two-payload binding | Refused at single-image boundary; no partial acceptance. |
| N03 | Current policy differs in any field, is revoked, has zero revision, or time is negative/expired | Refused; original admitted object cannot freeze stale policy authority. |
| N04 | Construct an inconsistent release fixture with changed target/layout/recovery/compatibility/generation/signer/revision metadata | Refused at fixture policy/manifest consistency; never treat direct construction as admission proof. |
| N05 | Exact-length bytes changed in the bound payload while release stays original; digest/metadata substituted | Refused at exact-byte/digest boundary. Copies and recomputed digests, not asserted metadata, decide. |
| N06 | Valid binding has a different raw offset or length from the admitted manifest | Refused at exact raw image boundary, even when both independent objects are valid. |
| N07 | Plan erase block differs from retained policy, or mathematical write is unaligned for retained fixture admission | Refused at geometry boundary. |
| N08 | Plan allows any bytes beyond policy allowed range, or classifies policy-protected bytes as allowed | Refused at geometry boundary despite the plan's complete mathematical coverage. |
| N09 | Policy ranges exceed declared address space, overlap, or endpoint/rounding arithmetic is invalid/out of the retained uint boundary | Refused; no wrapping, fallback or inferred geometry. |
| N10 | Payload fits raw range but its erase footprint reaches protected bytes | Existing plan/admission construction refuses; preflight must not invent a bypass or manufacture a successful binding. |
| N11 | Caller mutates original input arrays or a byte copy returned by either component | Retained copies stay unchanged; evaluation uses retained snapshots. Unchanged admitted/bound snapshots retain their original result. |
| B01 | Session/inspection/selection changes or cancellation occurs after a positive pure check | Existing engine/adapter boundary still refuses; a cached consistency outcome cannot override it. Keep existing lifecycle regression coverage. |

Malformed inputs that existing immutable factories already refuse must be tested at those factories, not fabricated by weakening their constructors. The cases above guide a future bounded test plan; no new group count or fresh 165-group run is claimed here.

## Remaining authority and publication boundaries

OpenTrail owns accepted target/layout/artifact/recovery facts; Firmware Loader owns consuming validation and its application policy enforcement. Production signer/key custody, trust/revocation freshness, accepted release schema, exact physical device identity, complete target-owned erase/preservation facts, production storage enrollment, independent original custody/recovery, and physical authorization/acceptance remain unresolved as recorded in the [host matrix](testing/LUF-0010a-HOST-MATRIX-2026-10-01.md). This host proposal closes none of those gates and does not unblock package/physical completion.

The source/merge history in PRs #6-#9 is retained evidence, not validation newly performed for this design. Only this document is added; no source/test/board edits, branches, commits or PR operations are part of this task.

Daily CI batching is retained: keep related accepted work local for a separately authorized repository batch at 8:00 AM America/New_York or the day's start, at most one PR per repository per day and none while an existing repository PR has CI running. This task opens no PR and schedules nothing. Documentation/link/whitespace review is appropriate here; the full Windows host matrix is not rerun per documentation micro-task. Future code validation and the repository's morning CI batch remain separately scoped.
