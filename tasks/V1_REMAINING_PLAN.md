# V1 remaining task plan

This is the remaining-work plan for the existing V1 scope, not completion evidence or operational approval. The backlog registers IDs; the private hosted checklist records owner decisions. Reuse accepted exact-artifact evidence. Unknown defects discovered during execution require bounded child tasks, not silently expanded scope.

Optional products and V1.5 are excluded. Each physical or release task must first freeze exact artifacts, criteria, device/host identity and recovery authority.

## LUF-0007a Freeze canonical Trail Loader release boundary

- Parent milestone: LUF-0007.
- Scope: Reconcile canonical provider, offline inspection, UI and test evidence. Define one exact Trail target, supported Windows platform and transport for the existing LUF-0006 installation/recovery gate. Preserve historical evidence without treating the frozen OpenTrail loader as current implementation.
- Exclusions: Trail Loader base-release gate only. Display provider/installation, Server, Gateway, Tracker, Console, Repeater and other optional products are excluded. No change to frozen the frozen historical loader. Proposal does not authorize hardware access, signing/key changes, installation, Git mutation, publication or deployment. Split materially expanded implementation into a separately bounded approved child.
- Acceptance: Owner-reviewed supported-target and capability matrix separates existing inspection evidence from missing signer, device, write, verification, boot and recovery gates.
- Dependencies: None.

## LUF-0008a Define Trail target signer and release admission authority

- Parent milestone: LUF-0008.
- Scope: For the selected Trail target, reconcile the project-owned manifest and define exact signer trust, revocation, artifact compatibility, offsets, protected regions and release admission. Keep production trust unavailable until accepted.
- Exclusions: Trail Loader base-release gate only. Display provider/installation, Server, Gateway, Tracker, Console, Repeater and other optional products are excluded. No change to frozen the frozen historical loader. Proposal does not authorize hardware access, signing/key changes, installation, Git mutation, publication or deployment. Split materially expanded implementation into a separately bounded approved child.
- Acceptance: Accepted target-owner and Loader authority matrix plus negative fixtures for unknown/revoked signer, wrong product/target/version, altered image and unsafe offsets. No implicit write authority.
- Dependencies: LUF-0007a.

## LUF-0009a Specify exact Trail device update and recovery lifecycle

- Parent milestone: LUF-0009.
- Scope: Define one target adapter and visible device-selection lifecycle through preflight, writing, readback, boot confirmation, interruption, rollback and recovery. Bind every operation to the exact artifact and current selected device.
- Exclusions: Trail Loader base-release gate only. Display provider/installation, Server, Gateway, Tracker, Console, Repeater and other optional products are excluded. No change to frozen the frozen historical loader. Proposal does not authorize hardware access, signing/key changes, installation, Git mutation, publication or deployment. Split materially expanded implementation into a separately bounded approved child.
- Acceptance: Reviewed state and refusal matrix includes stale selection, wrong device, disconnect, uncertain writes and independent recovery. Identify all target-owner and physical gates before implementation.
- Dependencies: LUF-0008a.

## LUF-0009b Implement approved Trail authenticity and adapter engine slice

- Parent milestone: LUF-0009.
- Scope: Implement only the approved one-target signer/admission and adapter engine boundaries with deterministic fakes. Preserve current provider/session revision binding and fail-closed behavior. Stop at the accepted engine validation checkpoint before physical execution.
- Exclusions: Trail Loader base-release gate only. Display provider/installation, Server, Gateway, Tracker, Console, Repeater and other optional products are excluded. No change to frozen the frozen historical loader. Proposal does not authorize hardware access, signing/key changes, installation, Git mutation, publication or deployment. Split materially expanded implementation into a separately bounded approved child.
- Acceptance: tools/Test-Loader.ps1 passes with warning-as-error builds and deterministic genuine, corrupt, wrong-target/version, revoked-signer, stale-selection, interruption and uncertain-outcome cases. Simulated success never enables unsupported physical authority.
- Dependencies: LUF-0009a.

## LUF-0009c Implement Trail device selection and update status UI

- Parent milestone: LUF-0009.
- Scope: Bind the approved one-target engine to explicit user-visible device selection, preflight, progress, failure and recovery guidance. Preserve product-switch/session cancellation and sanitized diagnostics; prevent automatic physical-device selection.
- Exclusions: Trail Loader base-release gate only. Display provider/installation, Server, Gateway, Tracker, Console, Repeater and other optional products are excluded. No change to frozen the frozen historical loader. Proposal does not authorize hardware access, signing/key changes, installation, Git mutation, publication or deployment. Split materially expanded implementation into a separately bounded approved child.
- Acceptance: Focused tests and populated UI review demonstrate exact target/device visibility, no stale action after selection changes, accurate uncertain/failure states and no private paths or identifiers retained in diagnostics.
- Dependencies: LUF-0009b.

## LUF-0010a Validate exact Trail installation and independent recovery

- Parent milestone: LUF-0010.
- Scope: Prepare the exact target, artifact, offsets, protected-region and recovery matrix. Execute each bounded physical case only after separate current authorization and fresh device enumeration; serialize device access.
- Exclusions: Trail Loader base-release gate only. Display provider/installation, Server, Gateway, Tracker, Console, Repeater and other optional products are excluded. No change to frozen the frozen historical loader. Proposal does not authorize hardware access, signing/key changes, installation, Git mutation, publication or deployment. Split materially expanded implementation into a separately bounded approved child.
- Acceptance: Independent evidence proves wrong-device refusal, exact write/readback, boot confirmation, interrupted-update outcome and restoration/recovery for every claimed capability. Record exact source, artifact hash, target and validation layer; any unmet physical gate remains blocked.
- Dependencies: LUF-0009c.

## LUF-0011a Accept Trail Loader package and supported release documentation

- Parent milestone: LUF-0011.
- Scope: Package only the accepted Trail capability set and validate clean-machine installation, operator guidance and support/recovery documentation. Prepare exact signing and distribution evidence; perform those operations only with separate authorization.
- Exclusions: Trail Loader base-release gate only. Display provider/installation, Server, Gateway, Tracker, Console, Repeater and other optional products are excluded. No change to frozen the frozen historical loader. Proposal does not authorize hardware access, signing/key changes, installation, Git mutation, publication or deployment. Split materially expanded implementation into a separately bounded approved child.
- Acceptance: Exact package and capability/support matrix agree with accepted tests and physical evidence. Clean-machine/operator acceptance passes; signer custody, immutable artifact identity and authorized release route are recorded before distribution. Inspection-only claims remain wherever installation evidence is absent.
- Dependencies: LUF-0010a.
