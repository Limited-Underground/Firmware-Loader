# Contributing and resuming Firmware Loader

This is the portable entrypoint for developers and AI tools. [README](README.md) introduces the product; this document carries the working rules independently of local machine instructions. Local tooling may add checkout routing, but must not weaken these rules.

## Working rules

Ordinary feature work is paused during the owner-directed portfolio recovery, effective October 22, 2026. Continue only authorized recovery or an explicit owner exception. Use a clear outcome, bounded scope and proportionate evidence; there is no mandatory six-section ceremony or automatic stop after each small task. Diagnose routine failures without discarding work or expanding scope.

GitHub Projects owns current task scope, status, priority, dependencies and recorded owner decisions. Repository records own technical contracts and evidence. Reconcile the current Project with those records before selecting work; historical backlog labels are not current authorization. A board state never grants hardware, publication or merge authority. The retired hosted checklist, private control APIs and legacy workspace tracker are not execution prerequisites.

Preserve existing working changes and original evidence. Before moving or deleting anything, establish exact paths, source identity, unique contents and an appropriate recovery copy; remove only approved obsolete material. Do not turn copied artifacts, historical test results or publication into current runtime acceptance. Keep credentials, private identifiers, personal data and machine-specific private paths out of public records.

Publication needs explicit authorization for the repository, destination and ref, including each applicable commit, fetch/pull, push, PR, merge and remote-verification operation. Do not bulk-publish unrelated changes, rewrite history, force-push, delete backup branches or bypass required checks. Website deployment is separate scope.

### Existing CI cadence

Morning means 8:00 AM America/New_York. Accumulate related changes locally and open at most one PR per repository per day, first thing in the morning or when the day starts. Do not open another while an existing PR's CI is running. Prefer batching two or three related tasks where practical. Full host CI is expected once per repository in the morning rather than per small task; docs-only changes should avoid unrelated full-matrix reruns. Every configured required check must pass before an explicitly authorized merge; branch protection remains in force.

## File maintenance

Follow [File maintenance](docs/MAINTENANCE.md) for storage roles, run ownership, report-only checks, retention holds and exact retirement decisions. Maintenance does not authorize deletion, Git operations, background scheduling or project resumption. Machine-specific paths and private custody evidence remain in the approved local recovery records.

## Resume and hand off

Start from the linked engineering status and contracts below, then inspect the actual checkout/ref and working changes before mutation. Do not assume a fresh clone contains another developer's uncommitted work. If the current Project, source identity, authorization or preservation state cannot be established, record the specific gap and continue only unaffected authorized work.

Update existing owning records rather than creating a competing journal. A handoff must identify the authorized outcome, exact source/candidate and environment, preserved dirty work, verification and its limits, blockers, next action, and applicable approval/recovery conditions. Reconcile actual state after interruption before retrying a mutation. These records must be sufficient without chat history, model memory or a user-local tool skill. Historical evidence remains historical until reverified.

## Engineering entrypoints

- [Architecture](docs/ARCHITECTURE.md): application/session/provider and inspection boundaries.
- [Project status](docs/PROJECT_STATUS.md): engineering checkpoint, proven versus unproven behavior, and linked host evidence.
- [Remaining V1 plan](tasks/V1_REMAINING_PLAN.md) and [backlog](tasks/BACKLOG.md): technical sequence, IDs and acceptance boundaries; reconcile status with the current GitHub Project.
- [Host preservation/binding admission contract](docs/HOST-PRESERVATION-BINDING-ADMISSION-CONTRACT.md): preservation and admission requirements.
- [Progress log](docs/PROGRESS_LOG.md): historical milestones, not a replacement for current acceptance.
- [Repository](https://github.com/Limited-Underground/Firmware-Loader): source identity and access point. [Trail V1 Project](https://github.com/orgs/Limited-Underground/projects/1) is the associated tracking board observed October 22, 2026; recheck the exact item before selecting work. LUF-0010a concerns physical firmware installation/recovery, not source-file restoration.

The standalone Firmware-Loader repository owns the common loader implementation. The old Trail tools/windows-loader implementation is a frozen predecessor, not a synchronized mirror or current implementation authority. Trail and Display retain their own target manifests, compatibility evidence, artifacts, signer decisions and recovery decisions.

## Product controls

The public working name is Limited Underground Firmware Loader — Preview, pending attorney clearance. The first interaction presents exactly Limited Underground Trail and Limited Underground Display. Engineering keys remain opentrail and opengauge; display names are not compatibility identifiers.

Provider selection grants no signer, bundle, device or mutation authority. Keep the production application inspection-only until every applicable target-specific physical-write and recovery gate has been separately accepted. Never automatically select hardware. Missing, stale, ambiguous or conflicting provider, target, signer, compatibility, identity or recovery information fails closed.

Write, erase, reset, reboot, rollback, recovery and eFuse operations require an exact reviewed adapter, target/artifact binding and separately recorded current authorization. After a physical failure, the next physical investigation is diagnosis only until the cause is isolated. Host success is not physical acceptance. Separate original recovery, retained modes and new modes; cleanup-only or historical bytes cannot become accepted originals.

Do not persist or publish serial ports, USB serials, MAC addresses, pairing data, keys, coordinates, vehicle/device identifiers or private local paths. Preserve existing modes; prefer an explicit new mode or gate over silently changing one.

## Validation and closeout

After executable-source changes, run the existing tools/Test-Loader.ps1 entrypoint. For policy-only documentation changes, verify the affected text, links and preservation instead; do not imply that a new .NET run occurred or is required solely by this documentation edit. Warning-as-error builds and deterministic core tests must pass before describing the changed source as validated. Do not infer current test counts from introductory historical descriptions: use the actual runner and owning evidence when an authorized validation runs.

Keep source/build, host tests, packaging, clean-machine checks, signing, device-write, recovery and production evidence separate. Future artifacts record source identity, target manifest, signer, exact bytes/SHA-256, timestamp and validation layer. Update project status and the relevant existing evidence/contract only when their owned facts change. If publication remains unauthorized, report implementation and publication separately.

This documentation recovery did not rerun validation or grant new implementation, device, signing, release or publication authority.

## Recovery handoff — October 22, 2026

At the initial recovery inventory, HEAD was `80f8b1d381acda6f75f3f001f7b863c1e0ab2458` on `feature/host-preservation-binding-preflight`. There were two tracked modifications, `AGENTS.md` and `tasks/V1_REMAINING_PLAN.md`, and zero untracked files. These counts precede the recovery documentation changes and are not a current clean-tree claim. Existing changes were retained in place; retention is not a complete backup, and ignored/private contents were not inventoried here.

Subsequent scoped source capture and isolated restoration passed, followed by full restored host validation. The later two-file maintenance-policy adoption is preserved separately and is not part of that earlier source checkpoint. Current Git/publication reconciliation and remaining acceptance decisions are still open; use [project status](docs/PROJECT_STATUS.md), the current Project item and the local recovery evidence for exact identities and holds. Retain historical originals and validated output dependencies until an exact retirement decision. Continue the authorized recovery freeze; do not select ordinary feature work from historical backlog status. No runtime or physical acceptance was added by this handoff.
