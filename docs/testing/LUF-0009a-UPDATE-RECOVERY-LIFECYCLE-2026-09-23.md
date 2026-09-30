# LUF-0009a Exact Trail update and recovery lifecycle

OBSERVED 2026-09-23T19:55:31-04:00. **Design proposal for owner review; no update engine, device adapter or physical operation is implemented or authorized here.** LUF-0008a was accepted before this lifecycle proposal, which was subsequently accepted. Policy acceptance does not populate missing signer, artifact or device authority.

## What the operator would do

Choose Trail, inspect an update file, then explicitly choose the intended device. The Loader must show the exact supported model and update identity and explain any missing checks before offering an update. One final confirmation applies only to that file, that freshly matched device and that attempt. A changed file, changed device, reconnect or changed trust policy requires fresh checks and confirmation.

The Loader may report success only after comparing the written bytes and confirming that the intended firmware started correctly. If the cable disconnects or the result is unclear, it must say that the outcome is uncertain and stop. It must not silently retry, reset the device or start restoration. Recovery requires a separately approved original image and recovery plan, a fresh device match and explicit authorization. Successful recovery means the original was restored; it does not make the attempted update successful.

Today the application stops after read-only file inspection. The later controls and states below are proposed for implementation and physical acceptance under their own approved tasks. Existing offline Trail field communication remains independent of the Loader, the network and revocation-policy refresh.

## Source boundary and retained evidence

[LUF-0007a](LUF-0007a-V1-RELEASE-BOUNDARY-2026-09-23.md) defines the one-target platform/transport candidate. [LUF-0008a](LUF-0008a-RELEASE-AUTHORITY-2026-09-23.md) defines the release-authority proposal and unresolved target-contract/layout reconciliation. Those detailed claims are not repeated or promoted here. Historical planning context supplies no hardware evidence.

The real current flow is [MainWindow](../../src/LimitedUnderground.FirmwareLoader/MainWindow.xaml.cs) product selection -> one read-only file picker -> [OfflineBundleInspectionWorkflow](../../src/LimitedUnderground.FirmwareLoader/OfflineBundleInspectionWorkflow.cs) -> bounded inspector -> [LoaderSessionController](../../src/LimitedUnderground.FirmwareLoader/LoaderSessionController.cs) publication check. Choosing a different product, returning to the chooser or closing invalidates the context; canceled file selection leaves the previous inspected result. No serial connection or mutation occurs. Current status distinguishes signature presence from trust and explicitly blocks installation.

Read-only source SHA-256 observations: MainWindow.xaml.cs `f043a315c6686694197d9cd4b70183bbd256bd1e1940e7b52b452283443f217c`; OfflineBundleInspectionWorkflow.cs `242c98bb4434c9d7a62ec4eae0f18d3efa8f7d71f570e3ca84e2d262ba1a7922`; LoaderSessionController.cs `ef1e91e9afbec898baaafffcfa4626232e44aa1c20d1e1fd4c74497f852132d7`. No source changed in this task.

## One proposed adapter and its ownership

The proposed physical boundary is one directly attached USB ROM-serial adapter for the accepted Trail candidate HTIT-WB32LAF V4.2 / ESP32-S3 profile. It is not a generic ESP32, BLE, network, LoRa or multi-device updater. The exact USB interface/driver and board discriminator remain target-owner/physical gates; vendor/product IDs, a COM name or a family string alone cannot establish the intended physical unit. The Loader inspection key `heltec_v4_bench` is not a blanket alias for enrolled evaluation, full images or recovery images.

The future adapter exposes separately reviewed passive enumeration, exact identity probe, one authorized write plan, readback and separately authorized boot/recovery phases. It must declare every port-open, DTR/RTS, stub-loading, erase, reset or reboot side effect before execution. Calling an API read-only does not authorize a reset. A probe needing ROM entry or any physical effect stops until that particular action is permitted. No esptool version, reset flag, baud rate, driver or packet protocol is selected merely from historical plans.

One operation owner holds one exclusive connection and mutation lease. It must exclude concurrent application instances as well as concurrent tasks. The UI observes immutable progress; it cannot directly drive transport commands. Enumerators cannot own mutation authority; providers cannot mint signer or device authority. Independent recovery must work when the candidate application cannot boot, using a target-owner-approved ROM/recovery route and previously secured exact original, without relying on candidate telemetry or its filesystem.

Device identity and volatile endpoint data remain in memory only. The visible selection uses the exact model/revision and a session-local label tied to the approved physical discriminator. No raw port, USB serial, MAC, pairing value, device-specific identifier or private path is persisted, exported or included in diagnostics. If that safe display cannot unambiguously distinguish the intended unit, refuse; do not assume the only visible device is the right one.

## Immutable operation binding and preflight

A future single-use operation record must bind all of these before `Ready`:

- Current controller/session revision, provider identity/version/generation, exact target rule and accepted target-manifest/layout identities.
- Exact admitted candidate bytes or stable read-only handle, byte counts and digests, release/compatibility generation, trust and revocation revisions/freshness, and the approved complete write/erase-rounded plan.
- Exact current device observation and selection generation, adapter identity/version, connection generation and exclusive ownership. A port name is only a transient locator.
- Exact current original/recovery artifact and policy, independent restoration verification requirements, protected-span expectations and permitted boot transition.
- Operation-specific owner authorization, one mutation-attempt allowance, the confirmed action, and the complete target-approved timing/transport profile.

Hash and inspect the exact retained bytes; do not reopen a path later and assume it still names the inspected image. Revalidate binding immediately before the first mutation and each subsequent command using the same still-valid operation record; the operator does not reconfirm each chunk. A fresh confirmation is required only after invalidation or for a new/recovery operation. Changes invalidate authority. The transition to `Writing` atomically consumes the single-use permission and marks mutation possible **before** submitting a command that could reach the device. Failure without an acknowledgement cannot prove that no write happened.

The target owner must provide finite positive bounds for open/probe, per-command acknowledgement, whole write, readback, boot and recovery phases; maximum chunk size, erase granularity, permitted reset behavior and safe cancellation boundary also must be explicit. These values are not invented in this design. Missing values refuse preflight. Deadlines use one host monotonic clock per phase/operation; queueing and in-flight work count against the original budget. Human selection/confirmation has no authority until fresh preflight passes. Expired authority is discarded rather than extending a timeout silently. Automatic mutation attempts/retries are zero beyond the one explicitly confirmed attempt.

## Proposed state and refusal matrix

Only the first three rows describe an implemented user path. Their state labels summarize observed behavior, not existing C# state enums. All later physical/admission/update-cancellation states are future behavior requiring the missing accepted inputs and implementation. Current file-picker cancellation simply returns and preserves the previous inspection result; no current update-cancel control or in-flight writer exists.

| Before state and owner | Trigger / required effect | Next state and visible result | Refusal, interruption and resource handling |
| --- | --- | --- | --- |
| Idle / session | Explicit Trail choice activates provider lease | ProductSelected | Unknown product/activation failure stays without authority. |
| ProductSelected / inspection | Explicit candidate file; real bounded parser/digest checks | CandidateInspected: inspection only | Invalid archive or mismatched product/target does not publish; no hardware access. |
| CandidateInspected / admission | Current production has no trusted release verifier | AdmissionBlocked: installation unavailable | No selected provider, metadata or fixture can bypass this. |
| CandidateInspected / future admission | All independently accepted authenticity/compatibility/address gates pass | DeviceUnselected | Do not auto-enumerate/open hardware unless that exact discovery/probe scope is authorized. |
| DeviceUnselected / selection | Present passive candidates; operator explicitly selects intended unit | DeviceSelected | Never automatically select, including a single candidate; ambiguous identity refuses. |
| DeviceSelected / operation owner | Fresh identity, source bytes, authority, recovery and finite profile checks | Preflight then Ready: exact review and confirmation | Wrong device/version, unaccepted layout, stale policy, missing original or driver side effects stop before mutation. No blanket reset. |
| Ready / operation owner | Atomic exact confirmation and fresh binding | Writing: one attempt consumed | Stale/double confirmation, device generation change, duplicate process owner or cancellation before start -> NoWriteStopped. |
| Writing / adapter owner | Execute only permitted segments with bounded acknowledgements | Readback: written, checking bytes | Disconnect, timeout, mismatch or cancellation after a possible write -> OutcomeUnknown. Do not retry, erase, reset or declare rollback automatically. |
| Readback / verifier | Independently read written spans and accepted protected-span checks | BootWait: bytes verified, startup unconfirmed | A writer acknowledgement or writer-computed hash is insufficient. Any mismatch/timeout -> OutcomeUnknown, with proven facts retained. |
| BootWait / operation owner | Perform only expressly authorized boot transition; observe exact expected release identity and bounded health/protected-state criteria | Verified: update verified | Generic heartbeat, stale logs, wrong build, repeated reboot or timeout -> OutcomeUnknown. No field/radio capability inferred. |
| Any future pre-mutation update state / session | Cancel, switch product/file/device or close | NoWriteStopped; clear authority | No future commands; close owned resources without side effects. No auto retry. |
| Writing/Readback/BootWait / operation owner | Cancel, switch or close after mutation became possible | OutcomeUnknown; stop further commands and reconcile | Invalidate UI/session authorization immediately, but do not abandon the in-flight transport owner. Bounded quiescence and release must precede any new owner. Late completion cannot advance a new session or convert canceled work into success. |
| OutcomeUnknown / reconciliation | Show known written/verified phases; acknowledge unresolved outcome | RecoveryReview | Stop automatic actions. Reconnect never resumes a write; re-enumerate and reselect only under new hardware scope. |
| RecoveryReview / recovery owner | Validate exact original, recovery tool/profile, fresh device, independent route and separate current permission | RecoveryReady | Missing original/hash/policy or unavailable independent route remains blocked. Do not reuse update authorization or a historical consumed plan. |
| RecoveryReady / recovery owner | Consume one separate exact recovery authorization | Recovering | Preserve unapproved spans; no full-chip erase, eFuse or security change implicit. No second automatic attempt. |
| Recovering / recovery verifier | Restoration commands finish, then independently compare original and protected spans | RecoveryReadback then RecoveryBootWait | Any uncertainty/mismatch/disconnect -> OutcomeUnknown; no recursive recovery. |
| RecoveryBootWait / recovery owner | Exact restored original identity and accepted boot/state criteria pass | Recovered: original restored, update unsuccessful | A reconnect or generic boot line is insufficient. Failure stays unresolved. |
| Restart after nonterminal operation / reconciliation | Discard all old runtime tokens; inspect minimal uncertainty marker if present | RecoveryReview without a selected device | Never resume from a saved port, device identifier, old callback or cached permission. If history cannot prove completion, show uncertainty. |

## Cancellation, restart and truthful progress

Before mutation, Cancel means no update began. After a command might have reached the device, Cancel means stop scheduling more commands and reconcile; it never means the original is intact. The adapter owner must distinguish not-submitted, submitted/uncertain, acknowledged, independently verified and boot-confirmed phases. It may finish observing a command already in flight within its original deadline; it must not issue another write/reset to make cancellation look clean. If the transport cannot confirm a safe boundary, retain OutcomeUnknown and stop. Process termination/power loss follows the same conservative rule on restart.

A proposed minimal local journal may record a random operation nonce, phase, artifact/policy digests and unresolved/completed status, but never raw device identity, endpoints, secrets or private paths. It is an uncertainty notice, not authority. Journal durability, protected storage, atomic mutation-start marker, crash replay and completion ordering require review/tests before implementation can claim safe restart. If durable marking fails, refuse before mutation. A stale or missing journal must never grant success or resume permission; new sessions independently reconcile the selected device under new authority. No journal is implemented here.

Progress reports separate admitted, selected, preflighted, writing, verifying bytes, checking startup and recovered. Percent written is only transport progress. Verified requires the exact operation's full byte/protected-state and boot predicates. Readback success followed by unknown boot retains the known byte result while reporting unconfirmed startup. Successful restoration reports the original version and failed update outcome separately; it is never relabeled as a successful candidate installation.

## Review scenarios and validation handoff

[LUF-0009a lifecycle scenarios](LUF-0009a-LIFECYCLE-SCENARIOS-2026-09-23.json) contains 24 **design review fixtures**, not an executed adapter or simulated proof. Each gives a start state, events, expected end state and first refusing boundary. They cover current inspection, missing admission, explicit selection, wrong device, port reuse, changed bindings, unset timing, pre-write cancellation, duplicate submission, disconnect, in-flight cancel, write/readback/boot separation, restart, independent recovery and cross-process ownership.

The fixture flag about possible mutation describes only the hypothetical separately authorized future flow. It grants no action today. A JSON/schema-reference check establishes only internal consistency of this proposal. No test double claims to implement revocation, readback, safe device matching or recovery. The earlier maintained **75-group** pass remains reusable host evidence for unchanged code; it is not new lifecycle or physical acceptance.

| Next gate | Required owner/implementation/physical evidence |
| --- | --- |
| Exact release inputs / OpenTrail and Loader | Accepted current target/layout mapping, signed artifact and trust/revocation/compatibility policy from LUF-0008a; real keys remain absent. |
| Adapter contract / OpenTrail + Loader | Exact physical interface/driver/device discriminator, side-effect declaration, finite timing/chunk/erase/reset profile and independent recovery plan. Undefined fields must remain blocked. |
| Engine / LUF-0009b | Implement only separately approved host engine slice with real state logic and deterministic transport fakes; include all 24 scenarios with genuine first-boundary checks, partial writes and stale completions. Fakes must not turn submitted work into verified bytes. |
| UI / LUF-0009c | Explicit selection and exact confirmation, session cancellation, distinct uncertainty/progress/recovery wording, no private data retained; populated UI review. |
| Physical / LUF-0010a | Fresh enumeration under separate authorization, wrong-device refusal, exact write/readback/boot, interruption and independently verified original restoration for this exact target. Access remains serialized. |
| Package / LUF-0011a | Accepted supported capability matrix and clean-machine/operator evidence, separate signing/distribution authorization. |

## Scope and closeout

VERIFIED 2026-09-23T19:59:42-04:00: PowerShell ConvertFrom-Json and explicit assertions passed for 24 unique scenarios, unique/known state references, required fields and proposed-only evidence labels; all six report relative links resolved; git diff --check passed; no src diff was present. Canonical repository document-check scripts remain unavailable. These checks validate document/fixture consistency only, not the proposed engine.

Exact next action: owner reviews the state/refusal and recovery proposal. Acceptance allows only the next approved design/host task after a fresh live dependency check; it cannot fill missing signer/artifact/adapter inputs or authorize a physical operation. Work remains local and uncommitted; LUF-0009a is review-ready only after independent review and the stated document/fixture checks, not owner-complete until accepted.
