# Firmware-Loader Agent Guide

## Applicability and canonical authority

C:\lu\AGENTS.md applies in full. This file adds Firmware-Loader-specific constraints and may not weaken the workspace rules. Stop and report any conflict.

- C:\Projects\LimitedUnderground\Firmware-Loader is the canonical Firmware Loader implementation and source authority.
- C:\Projects\LimitedUnderground\Trail\tools\windows-loader is a frozen historical predecessor. It is not a synchronized mirror, must not be edited, and must not be used to infer current Firmware-Loader behavior.
- OpenTrail and OpenGauge own their target manifests, compatibility evidence, firmware artifacts, signer decisions, and recovery decisions.
- Do not copy project-private records, identifiers, keys, or artifacts into this repository.
- Any later loader implementation work must be explicitly scoped to this canonical repository.

## Micro-Task Protocol (Mandatory)

You are not allowed to accept or execute large, multi-day, multi-component, or open-ended tasks.

### Before any implementation

1. Restate the single concrete problem in one or two sentences.
2. State the exact measurable outcome that proves the task is done.
3. List the exact files you expect to touch (prefer ≤ 7).
4. List the exact validation commands / test suites that must pass.
5. If the task cannot be finished and host-validated in one focused session (roughly 30–90 minutes of real work), you MUST refuse and propose a smaller split.

### Hard task-size limits

- One coherent result only.
- Prefer changes that affect a single authority, mode, gate, or boundary.
- No “also fix related issues” or “while we’re here”.
- No expanding scope because the first attempt revealed a deeper problem. Record the deeper problem and stop.

### Required response shape for every task

1. Diagnosis (what is actually broken or missing)
2. Proposed micro-change (exact behavior)
3. Files that will change
4. Exact validation steps
5. Only after the above is accepted may you write code.

### Failure handling

- If a test or physical observation fails, do not immediately rewrite large amounts of code.
- First produce a minimal diagnosis of the new failure.
- Propose the next micro-correction.
- Never claim a physical result from host-only evidence.

### Forbidden behaviors

- Accepting tasks such as “finish OT-0238c”, “make enrollment work”, “fix the radio path”, “complete the loader”, etc.
- Generating large speculative refactors.
- Changing multiple independent authorities in one step.
- Treating host test success as physical acceptance.
- Silently changing an existing mode instead of adding an explicit new mode or gate when behavior must diverge.

### Project-specific additions

- Prefer adding an explicit new mode or gate over silently changing an existing one.
- After any physical failure, the next task is diagnosis only until the root cause is isolated.
- Host success never equals physical acceptance. Always say so explicitly.
- Keep original recovery, retained modes, and new modes clearly separated; never let cleanup-only or historical bytes become accepted originals.

## Product boundary

- The public working name is Limited Underground Firmware Loader — Preview, pending attorney clearance.
- The first interaction presents exactly Limited Underground Trail and Limited Underground Display as provider choices.
- Engineering keys remain opentrail and opengauge; public names never become protocol or compatibility identifiers.
- Selecting Trail or Display selects only a provider namespace. It never grants bundle, signer, device, write, erase, reset, reboot, rollback, recovery, or eFuse authority.
- Until every target-specific physical-write and recovery gate is separately accepted, the application remains inspection-only and must not expose, advertise, or imply firmware-installation authority.

## Fail-closed safety and privacy

1. Never automatically select a physical device. Require an exact, user-visible device match and explicit selection within the separately authorized operation.
2. Fail closed when the product provider, target manifest, signer, device identity, compatibility decision, or recovery plan is missing, stale, conflicting, or ambiguous.
3. No firmware write, erase, reset, reboot, recovery, rollback, or eFuse action may occur without an exact reviewed adapter, exact target/artifact binding, and separately recorded current owner authorization for that action.
4. Never persist or publish serial ports, USB serials, MAC addresses, pairing data, keys, coordinates, vehicle identifiers, device-specific identifiers, or private local paths.
5. A successful inspection, build, or test never grants installation authority.

## Validation

- After any source change, run tools\Test-Loader.ps1.
- Warning-as-error builds and deterministic core tests must pass before a change is described as validated.
- Keep source, build, test, package, clean-machine, signing, hardware-write, recovery, and production evidence separate.
- Future artifacts must record source identity, target manifest, signer, exact bytes, SHA-256, timestamp, and validation layer.

## Documentation authority

- README.md is the stable product introduction and navigation.
- Project architecture and project-status documents own detailed engineering facts.
- The repository backlog owns sequencing.
- C:\lu\.tracker\PROJECTS\Firmware-Loader.md owns only a dated workspace summary and links.
- C:\lu\.tracker\CURRENT-FOCUS.md solely owns any active objective, blocker, next action, and acceptance criteria.
- Update only repository records whose owned facts changed; do not duplicate mutable status across files.

## Completion and publication

- Follow C:\lu\AGENTS.md and C:\lu\.tracker for workspace and publication authority.
- Documentation does not authorize commit, push, PR creation, website work, deployment, or remote verification.
- When a distinct publication operation is explicitly authorized, publish only the validated public-ready scope to the intended repository and independently verify the remote result only when that verification is separately authorized.
- If implementation is complete but a required publication operation is unavailable or unauthorized, report implementation complete; publication pending and name the exact remaining action.
- Never publish private project material, sensitive device information, or unsupported installation claims.