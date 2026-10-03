# LUF-0009b bounded host engine checkpoint

## Implemented boundary

[HostReleaseAdmission](../../src/LimitedUnderground.FirmwareLoader/HostReleaseAdmission.cs)
implements actual RSA-PSS-3072/SHA-256 verification for a deliberately internal
host fixture envelope. It binds a separately injected public-key digest, signer,
policy revision/freshness/revocation, exact product/target/layout/compatibility,
release floor, original-recovery digest, image length/digest and erase-rounded
allowed/protected spans. Bounded image, signature and public-key buffers are copied
before verification; the admitted image remains private and is copied on access.
The fixture envelope is neither a registered cross-project release contract nor
a new production file format. Ephemeral test keys are generated in memory; no
production signer, private key, certificate or trust entry is added.

[HostUpdateEngine](../../src/LimitedUnderground.FirmwareLoader/HostUpdateEngine.cs)
implements the actual asynchronous state machine. It binds the existing real
controller/inspection context, current file-result identity, inspected image
hash/length/generation, immutable admitted bytes/policy, explicit selected unit,
connection generation and one single-use confirmation. Selecting a different unit
invalidates older confirmations. Every adapter command carries the expected
selection; the adapter contract requires atomic comparison before any effect.
The deterministic adapter exercises a replacement immediately at that boundary.

All new implementation types are internal; production UI/provider registration
never instantiates this engine. The production trust registry stays empty, and
inspection results still have SignerTrusted=false and AdmissionAllowed=false.
The application continues to expose no connected-device or installation authority.
The sole inspector change retains its already-verified image digest internally
so a different same-target image cannot be substituted at the engine boundary.

## Operation, ownership and recovery trace

| Transition | Implemented evidence boundary | Refusal/recovery |
| --- | --- | --- |
| Inspection to selection | Actual controller context plus matching current file/hash/length/generation and fixture admission | Missing/revoked/stale policy, foreign controller, substituted artifact or changed file refuses before commands |
| Explicit selection to Ready | Exact in-memory unit identity, nonzero connection generation, target/model/adapter profile and finite bounds | No automatic selection; missing side-effect/boot permission or unresolved update refuses |
| Confirmation to Writing | Atomic single-use start; injected exclusive lease; uncertainty marker before submission; final binding check and selection-bound command | Pre-submission cancellation produces no write; failure after possible submission is uncertain; no automatic retries |
| Writing to Readback | Write completion is separate from independent returned bytes and protected-span comparison | Bad/missing bytes or changed authority prevents boot |
| Readback to BootWait | Matching bytes preserve BytesVerified while startup remains unconfirmed | Generic heartbeat, wrong image, unhealthy/protected-state failure cannot verify |
| BootWait to Verified | Exact image/health evidence and current bindings claim terminal completion under the cancellation gate, then journal completion | Cancel before claim wins. Cancel after claim, including a reentrant journal callback, cannot undo verified evidence. Failed terminal journal commit remains uncertain |
| Cancellation/timeout to OutcomeUnknown | Pending adapter task retains exclusive lease until actual quiescence | Late success only releases ownership and cannot advance any operation; a hung task keeps ownership |
| Restart to RecoveryReview | Injected unresolved marker prevents normal update/resume and automatic selection | Missing exact independent original or fresh recovery confirmation refuses |
| Recovery to Recovered | New engine/confirmation, exact original digest, separate write/readback/boot verification | Old update token cannot authorize recovery; failed recovery remains uncertain without recursive retry |

One injected monotonic millisecond clock accounts for phase/whole-operation
budgets and rollback. Real Task.WaitAsync limits bound an unresponsive fixture
command as well; timeout does not pretend to terminate that command or release
its ownership. These are host timing checks, not measured hardware deadlines.

## Validation

VERIFIED 2026-09-29: from the repository root, ran
`& tools/Test-Loader.ps1` using installed SDK **8.0.425**,
Release, `net8.0-windows`, warnings treated as errors. Application and test builds
both reported **0 warnings, 0 errors**. The maintained runner passed **106 host
groups**, comprising 75 prior groups and 31 new groups. No UI launch or device
operation occurred. The runner deletes its exact temporary build tree normally;
this task retains no binary/package and makes no packaging claim.

[HostUpdateEngineTests](../../tests/LimitedUnderground.FirmwareLoader.Tests/HostUpdateEngineTests.cs)
covers all 24 LUF-0009a scenario IDs at their actual host boundaries. It includes
genuine/corrupt signatures; unknown, revoked and substituted signers; wrong target,
version/generation and layout; unsafe offsets/erase spans; image immutability;
missing authority; stale file/device/controller/policy; duplicate confirmation;
partial-write failure; byte/boot separation; interrupted recovery; uncertain
journals; deadline/clock faults; and cancellation while a command is still active.

Independent review identified artifact substitution, mutable admission buffers,
selection replacement between sample and submission, and terminal cancellation
ordering. The corrected implementation includes dedicated artifact-substitution,
command-admission and terminal-ordering regression groups. The initial new fixture
was rejected by the existing inspector because its signature buffer contained only
zeros; that fixture was corrected before reaching the engine. All final groups pass.

Private command output and a hash inventory are retained under this repository's
privately retained evidence set; these are not public release artifacts.
Whitespace and report-link checks accompany this report. Repository-local
`tools/check_repository_docs.py` and `tests/host/repository_docs_tests.py` do not
exist, so those gates are unavailable and were not copied from another repository.

## Remaining gates and exact next step

Only deterministic fixtures implement the adapter, exclusivity and uncertainty
journal interfaces. No physical adapter, OS cross-process device lock, durable
journal backend, release-enrollment service or production trust/time authority
is implemented or validated. Future adapters must enforce expected-selection
comparison atomically at command admission and must not synchronously block
before returning a task. Real driver side effects, firmware address policies,
exact received-unit matching, finite physical timings, readback/boot health,
protected storage, independent restoration and physical acceptance remain their
separate gates. Host fakes cannot discharge them.

Independent source review cleared all four findings after inspecting the final
bindings, regressions and 106-group validation log; no further actionable issue
was found within this host scope. The reviewer did not independently rerun tests.

This host checkpoint was subsequently owner-accepted. The
[LUF-0009c UI checkpoint](LUF-0009c-UI-CHECKPOINT-2026-09-29.md) binds the accepted engine to the UI. Production capabilities
remain disabled until their independent authorities and physical gates are accepted.
No source publication, signing, installation, deployment, progress credit or public
website status change occurred. Work remains local and uncommitted.
