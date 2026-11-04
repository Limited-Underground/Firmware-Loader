# LUF-0009g — controlled host storage bring-forward, November 3, 2026

## Authority and source

The November 3 owner exception permits only LUF-0009g's accepted storage-policy bring-forward and remaining isolated host validation. [Firmware Loader Project #3](https://github.com/orgs/Limited-Underground/projects/3) owns the live task and decisions. The wider development freeze remains; this record grants no publication, merge, production, machine-store or physical authority.

This uncommitted candidate uses base commit `747467163ec597ef377fd6d71d5f8c0f09ad5914` (.NET 10), distinct from the retained foundation reconstruction, on `feature/luf-0009g-host-storage-bring-forward`. The accepted October 29 policy overlay and earlier isolated ACL regressions were preserved before integration. A three-way reconciliation keeps current main's prior-record withdrawal, new interruption tests and accepted policy guard transfer/disposal. The existing dirty source was not overwritten. Earlier foundation/source-build evidence remains valid only for its declared inputs; it does not validate this new source delta.

The product-source delta is five files: [HostOperationStore](../../src/LimitedUnderground.FirmwareLoader/HostOperationStore.cs), [MachineRecoveryStorePolicy](../../src/LimitedUnderground.FirmwareLoader/MachineRecoveryStorePolicy.cs), [store tests](../../tests/LimitedUnderground.FirmwareLoader.Tests/HostOperationStoreTests.cs), [policy tests](../../tests/LimitedUnderground.FirmwareLoader.Tests/MachineRecoveryStorePolicyTests.cs) and [test registration](../../tests/LimitedUnderground.FirmwareLoader.Tests/Program.cs). Documentation adds this evidence, updates the owning [project-status](../PROJECT_STATUS.md) entry and reconciles the exact host exception in README, AGENTS, CONTRIBUTING and Foundation.

## Reused accepted behavior

- Elevated-administrator operations, with standard-user inspection-only behavior and no broker/UI wiring. No production engine factory or device adapter is registered.
- Exact CommonApplicationData namespace, protected Administrators/SYSTEM-only descriptors, strict fresh enrollment, one-use unforgeable permit, directory-chain pins and handle-bound file checks. Administrators/SYSTEM are trusted administrators, not adversaries isolated by their own ACL.
- Stable-lock ownership and post-lock reload, digest-only durable uncertainty, verified write/flush/replace/readback, strict refusal without repair, and current main's separately accepted no-command-submitted withdrawal rules.
- Three retained actual same-account isolated ACL regressions deny journal ReadData, lock WriteData and fixture-root CreateFiles, then restore the exact original DACL in finally blocks. They are neither new production enrollment nor cross-account acceptance.

The accepted machine-policy file remains byte-for-byte unchanged: SHA-256 `F7A8D99BD63C5457712665FC332D8873490614F2F426C82A48B67A24659276D6`. The October 29 207-group/38-focused and separately authorized one-time enrollment results are retained historical evidence at their original scope, not fresh runs for this candidate. Enrollment was not repeated.

## New isolated regression groups

| Case | Observable done criterion | Limit |
| --- | --- | --- |
| Concurrent null/forged proofs | 256 calls through existing ConsumeEnrollmentAuthority refuse; subsequent forged/null initializer calls still refuse. | Negative proof concurrency only; no valid permit, elevated enrollment race or token transition is simulated. |
| Isolated pin path binding | Nonfixture suffix, trailing separator, dot alias and nested fixture name refuse without creation; exact owned path still pins. | Uses existing isolated-pin API; cannot select a machine root or mint enrollment authority. |
| Journal substituted after ownership | Foreign canonical store identity prevents the first marker; bytes remain unchanged; restoring fixture bytes cannot unpoison the retained object. | Same-account isolated tamper; no protected-root or different-account claim. |
| Stable lock name substitution | Actual rename and deletion are denied while the lease is held; no second owner/journal change; rename works and ownership remains usable after release. | Isolated stable file-sharing behavior, not privileged machine namespace acceptance. |
| Hard link introduced at readback | Actual alias after atomic replace causes no-write stop and zero adapter calls; removing owned alias exposes landed durable uncertainty without clearing it. | Existing Readback checkpoint and fixture adapter only; not a real device or power-loss test. |

## Current validation result

Installed stable SDK 10.0.303 and PowerShell 7.6.5; both projects retain .NET 10 Windows targets and TreatWarningsAsErrors. The focused Release test-project build (including its application reference) passed with **0 warnings and 0 errors**. The existing direct store-only runner passed **29/29 groups**. Read-only invocation of the already-built policy collection's existing Action delegates passed **17/17 groups**. Actual token context was non-elevated; machine APIs refused at the privilege check before filesystem access.

The single required `tools/Test-Loader.ps1` full gate passed on November 3, 2026, using SDK 10.0.303 and PowerShell 7.6.5. Both selectors were explicitly `0`; suite mode was `full`.

| Full-gate observation | Actual result |
| --- | --- |
| Release application build | Native exit 0, 0 warnings, 0 errors |
| Release test build | Native exit 0, 0 warnings, 0 errors |
| Full host tests | Native exit 0, 237 selected groups and 237 PASS lines; final runner count 237 |
| WPF output | 13 retained fixture renderings |
| Wrapper completion | Final status passed; environment_restored=true; all stage stderr files empty |
| Source binding | All five compiled source SHA-256 values unchanged before and after execution |
| Token context | elevated-administrator=False; actual machine API privilege refusal before filesystem access |

All 237 selected Action delegates ran; no runner group was skipped. That does not imply positive elevated enrollment, cross-account or retained-object token-transition coverage. The wrapper started at 20:38:07Z and completed at 20:38:37Z. Its exclusive heavy-build lease was released at completion; no overlapping build or repeated full run was started. Raw source pins, commands, stage logs, resource measurements and lease timestamps remain in task-owned private evidence, outside the public repository.

To reproduce the maintained full host gate, use the [Testing instructions](../TESTING.md#practical-full-invocation) with a fresh approved task-owned temporary parent outside source:

```powershell
$env:LUF_UI_ONLY = '0'
$env:LUF_STORE_ONLY = '0'
.\tools\Test-Loader.ps1
```

The wrapper selects the installed stable .NET 10 SDK, clears package feeds through the repository configuration, runs offline documentation checks, builds both Release projects and executes the newly built test DLL with fresh output. All caches, temporary fixtures and retained evidence must stay in the approved task-owned location. No elevation or machine enrollment is a validation prerequisite.

## Additional terminal permission fixture — November 3, 2026

The preceding 237-group checkpoint is now the accepted source in [draft PR #20](https://github.com/Limited-Underground/Firmware-Loader/pull/20), revision `dd586748bda325281e7b9c63b6d88b77ae2f8d33`. Its exact-head host CI and independent review passed. The earlier base/uncommitted labels describe that original bring-forward checkpoint. This follow-up was authored on the separate `test/luf-0009g-terminal-permission` topic from that revision; the accepted PR #20 source checkpoint was preserved. The current PR head and Actions determine subsequent publication and CI state.

One remaining real permission window is added in `TerminalRootAclDenial`: after durable intent and the fixture adapter's write/readback/boot, deny CreateFiles on the owned temporary root before terminal completion. Existing initial root-creation denial and injected terminal I/O failures do not exercise this actual late OS-denial path. No application source, accepted policy, production composition or new fault/privilege API changes are needed.

The test requires actual UnauthorizedAccessException/access-denied 5 from an owned create probe, OutcomeUnknown despite verified bytes and exactly one fixture write/read/boot, byte-identical unresolved intent with no pending file, exact original DACL restoration in finally, permanent refusal of the poisoned object and a fresh owner that retains uncertainty without inherited completion/withdrawal authority. The ACL applies only to the owned GUID fixture; there is no machine-store or account operation.

Executed on November 4, 2026, from 05:07:47Z to 05:08:05Z, using SDK 10.0.303 and PowerShell 7.6.5. One Release test-project build, including the unchanged application reference, passed with **0 warnings and 0 errors**. Build, direct test runner and enclosing command exited **0**. The existing store-only runner selected and passed **30/30 groups**, with zero runner groups skipped; this includes the one new terminal-permission group. This is a focused result, not a new full-suite count.

Actual output recorded `CreateFiles denial=True; error=5; unresolved bytes preserved=True; original DACL restored=True`. The engine assertions passed for OutcomeUnknown despite verified fixture commands, exact retained unresolved bytes/no pending file, exact DACL restoration, poisoned-object refusal and a fresh owner without inherited completion or withdrawal authority. Current administrator role was false; no elevation, impersonation or machine API was invoked.

All three prebuild input hashes were unchanged during execution, and read-only application/fixture dependencies remained unchanged. The accepted PR #20 application/full 237-group/CI proof is reused only at its original scope; that unchanged full gate was not rerun. All new cache/temp/output stayed in the approved C-local task directory; actual scratch was 2,405,435 bytes within the 1 GiB lease budget. The exclusive lease was released, with no task-owned build/test process remaining. Independent coordinator source/native-result/current-document review qualified this additional delta with no findings. At its source-review checkpoint it was local; current PR head/checks determine later publication and CI. No broader task completion, production or physical acceptance follows.
## Review and remaining gates

Author self-review found no unresolved merge markers, dropped main withdrawal/interruption behavior, changed accepted policy file or production wiring. The coordinator independently rehashed and reviewed the exact seven-file delta: qualified source PASS with no actionable findings, before this full gate. Source remains unchanged. The coordinator independently verified completed native stage logs, wrapper completion and exact unchanged compiled pins and completed final result/documentation review with no actionable findings; the earlier policy review retains its historical scope. No additional reviewer or subagent was launched. No task is marked complete by this record.

Still blocked without separate authority: actual different-account isolation; retained machine-object effective-token transitions; protected machine-root ACL tamper and adversarial enrollment races; real disk-full; abrupt power-loss/durability and physical recovery. Existing generic I/O-fault and subprocess-kill tests do not close those gates. No new privilege/root overrides, accounts, machine/global ACL changes, disk-exhaustion setup or test policies were introduced to claim them.

Production trust/target inputs, identity-bound adapter, production composition, protected-store admission/custody, authentic preservation/recovery, operator/package/signing/distribution and physical acceptance remain separate existing V1 gates. No live store was accessed, no UAC/admin operation was performed, and no hardware was operated. Host validation does not establish physical acceptance. Implementation remains local; no commit, push, PR creation or merge is authorized by this receipt.
