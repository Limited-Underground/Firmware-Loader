# Firmware-Loader Agent Guide

## Applicability and canonical authority

[CONTRIBUTING.md](CONTRIBUTING.md) owns the portable working rules, recovery priority, preservation, publication and handoff instructions. This guide adds Firmware-Loader-specific technical constraints. The mandatory six-section ceremony is retired. The machine-local workspace guide supplies approved checkout routing; those paths are not prerequisites for another clone. Another developer must establish the intended checkout's identity without recreating those paths. Report substantive conflicts without weakening the product controls.

- The approved standalone Firmware-Loader checkout is the canonical implementation and source authority; establish its identity from local routing records before work.
- The Trail repository tools/windows-loader directory is a frozen historical predecessor. It is not a synchronized mirror, must not be edited, and must not be used to infer current Firmware-Loader behavior.
- OpenTrail and OpenGauge own their target manifests, compatibility evidence, firmware artifacts, signer decisions, and recovery decisions.
- Do not copy project-private records, identifiers, keys, or artifacts into this repository.
- Any later loader implementation work must be explicitly scoped to this canonical repository.

## Recovery execution

Follow [CONTRIBUTING.md](CONTRIBUTING.md). Ordinary feature work is paused. Continue authorized bounded recovery with clear outcomes and appropriate validation; no fixed response format, duration/file-count refusal, or automatic stop after each task applies. Preserve existing scope and do not perform speculative refactors or silently change an existing mode. Diagnose failures before a supported correction. Physical and publication authority remain separately constrained below.

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
- GitHub Projects owns current task scope, status, priority, dependencies, blockers and recorded owner decisions.
- The repository backlog retains engineering IDs, technical scope and links to acceptance evidence; reconcile it with the current GitHub Project when selecting work.
- Legacy workspace trackers and the retired hosted checklist are historical context, not current execution or approval gates.
- Update only repository records whose owned facts changed; do not duplicate mutable status across files.

## Completion and publication

- Follow [CONTRIBUTING.md](CONTRIBUTING.md) for the authorized recovery scope and execution/publication rules. Local machine routing does not replace this portable policy. GitHub Projects supplies current task/status records; board status does not authorize a physical operation or publication.
- Documentation does not authorize commit, push, PR creation, website work, deployment, or remote verification.
- When a distinct publication operation is explicitly authorized, publish only the validated public-ready scope to the intended repository and independently verify the remote result only when that verification is separately authorized.
- If implementation is complete but a required publication operation is unavailable or unauthorized, report implementation complete; publication pending and name the exact remaining action.
- Never publish private project material, sensitive device information, or unsupported installation claims.