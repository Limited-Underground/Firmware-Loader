# Firmware-Loader Progress Log

Historical milestones reconstructed from the public Git history and linked repository
records. Evidence dates are used when a report predates its publication commit;
commit links identify publication. Later acceptance recorded in an owning report
does not imply that acceptance occurred on its original report date. Current
engineering state and sequencing remain in [project status](PROJECT_STATUS.md) and
the [backlog](../tasks/BACKLOG.md). Host evidence never establishes physical acceptance.

## 2026-10-18

### Preserve private evidence outside Git

Added the private-evidence exclusion without changing application behavior.
[Source commit](https://github.com/Limited-Underground/Firmware-Loader/commit/a51a60d)
landed through [PR #3](https://github.com/Limited-Underground/Firmware-Loader/pull/3).
This housekeeping supplies no installation, signing or device acceptance.

## 2026-10-17

### Publish canonical checkout and bounded task guidance

Updated the agent guide to identify the canonical implementation, preserve the
frozen historical predecessor and require bounded tasks with separate acceptance
gates. [PR #2](https://github.com/Limited-Underground/Firmware-Loader/pull/2)
published documentation only. No implementation capability changed.
[Guide](../AGENTS.md).

## 2026-10-03

### Publish durable host checkpoint and installation matrix

Published the October host-storage implementation, tests and matrix records alongside
the preceding host engine and WPF work through
[PR #1](https://github.com/Limited-Underground/Firmware-Loader/pull/1).
The publication commit records the validated host scope; production remained
inspection-only. [Publication commit](https://github.com/Limited-Underground/Firmware-Loader/commit/23a661e);
[owning engineering status](PROJECT_STATUS.md).

## 2026-10-01

### LUF-0010a Prepare the exact installation and recovery matrix

Reconciled available target, image and layout evidence, candidate address ranges,
protected-region questions and adverse cases. Host matrix preparation completed;
physical completion remained blocked by production release trust, a real adapter,
complete address/preservation policy and independently accepted recovery authority.
No candidate image or historical recovery record granted a device operation.
[Matrix and limits](testing/LUF-0010a-HOST-MATRIX-2026-10-01.md).

### LUF-0009d Persist host ownership and uncertain outcomes

Added cooperating-process exclusion, strict post-lock reload and digest-only atomic
intent persistence. Twenty focused groups and the maintained 136-group matrix
passed with both Release builds at zero warnings/errors. Independent review and
owner acceptance cover this revision-1 host result. Production storage enrollment,
unrelated-tool exclusion, real devices, power loss and installation remain separate.
[Validation and limits](testing/LUF-0009d-DURABLE-HOST-STORE-2026-10-01.md).

## 2026-09-29

### LUF-0009c Integrate the accepted host engine into populated WPF controls

Bound device selection, explicit confirmation, update progress, uncertainty and
separate recovery to actual controls using injected fixture authority. Corrected
uncertainty preservation on invalidation/reselection. The complete 116-group matrix,
both warning-as-error Release builds and populated WPF renders passed; the report
records independent review and owner acceptance. Production has no device choices
or engine factory, so offscreen UI and fixture results grant no physical authority.
[UI checkpoint](testing/LUF-0009c-UI-CHECKPOINT-2026-09-29.md).

### LUF-0009b Validate the host admission and update/recovery engine

Implemented fixture-envelope signature/admission validation and an asynchronous,
selection-bound update/recovery state machine. Independent review findings were
corrected; 106 host groups and both Release builds passed with zero warnings/errors.
The report records subsequent owner acceptance. Fixtures supply transport,
exclusivity and journal behavior; production trust and physical capabilities remain
disabled. [Engine evidence](testing/LUF-0009b-HOST-ENGINE-2026-09-29.md);
[publication commit](https://github.com/Limited-Underground/Firmware-Loader/commit/cc8e172).

## 2026-09-23

### LUF-0009a Specify the update and recovery lifecycle

Defined exact selection, preflight, separate confirmation, write/readback/boot,
uncertain outcome and independently authorized recovery transitions, with 24 design
scenarios. The owning record reports subsequent proposal acceptance. This was design,
not an implemented device adapter or physical operation.
[Lifecycle and scenarios](testing/LUF-0009a-UPDATE-RECOVERY-LIFECYCLE-2026-09-23.md).

### LUF-0008a Define release admission authority

Proposed separate target-owner release approval and Loader-owned signer enrollment,
revocation, freshness, generation, compatibility, offset and recovery checks. Existing
inspection syntax and nonempty signatures were explicitly insufficient for release
admission. The [backlog](../tasks/BACKLOG.md) records the accepted authority proposal;
real keys, artifacts, trusted production configuration and physical authority remained
missing. [Proposal and evidence](testing/LUF-0008a-RELEASE-AUTHORITY-2026-09-23.md).

### LUF-0007a Bound the candidate Trail release

Proposed one exact Trail/Windows/board/USB recovery boundary while excluding Display
from that proposal. The report records subsequent boundary acceptance, separate from
support or installation acceptance. An alternate explicit SDK-8 build and direct test
DLL invocation passed 63 groups at zero warnings/errors. The maintained runner had
failed on toolchain/launch-path constraints; that failure was preserved rather than
reported as a passing maintained command.
[Boundary, reproduction and limits](testing/LUF-0007a-V1-RELEASE-BOUNDARY-2026-09-23.md).

## 2026-09-09

### Establish implementation and documentation authority

Recorded workspace rules, canonical Loader ownership, the frozen predecessor and
separate documentation authorities. These governance changes did not enable device
operations. [Commit](https://github.com/Limited-Underground/Firmware-Loader/commit/b6676c5);
[architecture](ARCHITECTURE.md).

## 2026-08-31

### Move the public repository into Limited Underground

Recorded the organization move and Firmware-Loader repository name while preserving
repository identity, main history and Apache license detection. Administrative
publication changed no loader capability or readiness claim.
[Commit](https://github.com/Limited-Underground/Firmware-Loader/commit/7f65cba);
[status record](PROJECT_STATUS.md).

## 2026-08-17

### LUF-004A Expose read-only Trail candidate selection

Added one explicit file choice, read-only opening, sanitized output and lifecycle
clearing without retaining the local path. The baseline reached 63 deterministic
groups; signer trust, admission and all device mutations remained unavailable.
[Commit](https://github.com/Limited-Underground/Firmware-Loader/commit/761b538);
[historical acceptance boundary](../tasks/BACKLOG.md).

### LUF-004 Bind the Trail inspection provider

Registered the exact versioned Trail provider and project-owned bench target rule
with its pinned target-contract identity. The baseline reached 58 groups. Display
and signer trust stayed absent, with no physical-write or recovery capability.
[Commit](https://github.com/Limited-Underground/Firmware-Loader/commit/c3ce693);
[engineering evidence](PROJECT_STATUS.md).

## 2026-08-16

### LUF-003 Freeze the provider lifecycle

Added immutable provider registration, exact identity/generation checks,
revoke-before-close leases, close-once disposal and contained failed/reentrant/stale
transitions. The recorded baseline passed 52 deterministic groups; production
registries were empty at that milestone.
[Commit](https://github.com/Limited-Underground/Firmware-Loader/commit/ad1b167);
[lifecycle boundary](../tasks/BACKLOG.md).

### Publish the independent Apache-2.0 repository

Added Apache-2.0 licensing and recorded the independent public loader repository.
[License commit](https://github.com/Limited-Underground/Firmware-Loader/commit/eee3383);
[repository record commit](https://github.com/Limited-Underground/Firmware-Loader/commit/a24ca93).
Publication did not grant installation or release acceptance.

### LUF-002 Inspect product-bound offline candidates

Added exact three-entry canonical bundle inspection, bounded ZIP expansion, image
SHA-256 checks and stale/cross-product refusal, with caller stream restoration.
Signature presence was inspected; signer verification and installation admission
remained false. [Commit](https://github.com/Limited-Underground/Firmware-Loader/commit/2cfeda7);
[inspection architecture](ARCHITECTURE.md).

### LUF-001 Establish the shared chooser foundation

Created the .NET 8 WPF shell with exact Trail/Display choices and a single
revision-bound selection. Host/source-policy checks covered the foundation while
all operational capabilities stayed false. No hardware path was added.
[Initial commit](https://github.com/Limited-Underground/Firmware-Loader/commit/bdb586c);
[foundation acceptance](../tasks/BACKLOG.md).
