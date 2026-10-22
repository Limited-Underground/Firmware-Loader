# File maintenance

File maintenance runs in report-only mode. No unattended service or deletion capability is enabled.

## Purpose and ownership

File maintenance keeps project files in their declared roles, reports growth and missing evidence, and prepares exact retirement decisions. Its only responsibility is file stewardship. It does not implement product features, validate devices, change Git history or publish releases.

The project owner establishes the storage map. Each developer or AI operator reads the same map, this policy and the current handoff. A machine-specific adapter may supply absolute paths; this portable document does not require another developer to reproduce the owner's drive letters.

## Four storage roles

| Role | Contents | Default action |
|---|---|---|
| Live | Authoritative source, build definitions, tests, rules, current documentation, admitted private material and Git administration | Keep required working material; flag unexpected outputs |
| Archive | Retired or historical collections with origin, identity and retention reason | Retain as provenance; never treat as active source implicitly |
| Backup | Dated source/evidence checkpoints with manifests, verification and restoration records | Preserve verified snapshots; do not overwrite in place |
| Restore Test | Isolated reconstruction and its validation evidence | Keep separate from Live; activation requires its own decision |

Validation output belongs in a fresh explicitly owned run directory, outside protected source evidence. Record the actual output parent and run identity; caller TEMP is not itself a project-owned cleanup boundary. Temporary-name and failed-run labels never establish disposability.

## Report-only workflow

1. Read approved roots, current operation/retention decisions and existing run manifests. If authority is missing, report the exact missing input without guessing a root.
2. Inventory only approved roots without following reparse points or links. Report errors and skipped entries; do not equate inaccessible with missing. Protected payload access requires its separately authorized scope.
3. Report file counts, logical bytes by role and run, free capacity, and differences from the prior observation. Logical size is not allocated size or guaranteed reclaimable space. Do not hash all private files merely to calculate space.
4. Classify each new or changed holding: required source, protected evidence, Git administration, historical provenance, snapshot, restored copy, generated output or unresolved. Unresolved means retain.
5. Check exact producer and consumers before proposing retirement. Successful later validation can depend on output from an earlier failed run. Matching hashes do not erase provenance requirements.
6. Emit an ordinary local report with tool identity, observation time, scope, counts, limitations, retained holds and exact proposed actions. Keep private paths/identities out of public reports.
7. Update the current handoff's pointer and changed owned facts. Preserve dated evidence; avoid duplicating competing current statuses across journals.

Run after a material authorized storage change, during an intentional maintenance review or in response to a capacity concern. Do not repeatedly scan unchanged holdings just to show activity. A scheduler or background service is not enabled by this document.

## Retention and action controls

Default action is retain. No automatic age-based purge, generic Temp cleanup, wildcard deletion, deduplication, Git garbage collection or removal of linked worktrees. A checkpoint on the same drive is not independent protection. A newer successful checkpoint does not by itself retire its predecessor or failed evidence.

Before any approved retirement, produce an exact list with path, source identity, role, owner, dependencies, preservation/restore evidence, proposed action, estimated benefit and recovery/recreation method. Include excluded files. Confirm the active-consumer boundary for that exact collection, without sweeping unrelated user activity.

At execution, recheck expected identities, approved root boundaries, fresh/occupied destination rules and capacity. Refuse changed sources and reparse paths. Create destination files exclusively. Verify copies before any separately authorized original removal. Record started/completed/failed results and retain interrupted partials; do not silently overwrite on retry. Do not remove verified receipts needed to interpret retained artifacts.

Any future action-capable automation requires its own narrow implementation, disposable-fixture validation and deployment authorization. Report-only qualification does not qualify deletion.

## Backup and restore maintenance

Capture meaningful source/evidence changes with unique checkpoint IDs and explicit scope, including admitted uncommitted work. Record exclusions. Preserve tooling/format/trust needed to interpret the checkpoint; repository source alone is not necessarily the full evidence set.

Prove restoration into a fresh isolated location and verify the declared file/Git domains. Test relevant usability separately; host tests do not establish clean-machine, signing, physical or release acceptance. Keep a separate record of off-computer custody and verification; do not claim it from a local path.

Recheck restore compatibility after material tooling/format changes or as required by the adopted protection policy. No arbitrary backup frequency or retirement count is invented here.

## Handoff acceptance

A reader must be able to locate the authoritative Live copy, current identity and dirty-work state, selected checkpoint and trust record, isolated restore evidence, pending holds, exact next safe action and required owner decisions without chat history or model-specific memory. State what was not verified. Maintenance completion never grants product installation, publication or ordinary-work resumption.
