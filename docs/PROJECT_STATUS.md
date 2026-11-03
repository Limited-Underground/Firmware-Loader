# Project status
## Current foundation freeze — November 2, 2026

Product development is frozen indefinitely by current owner direction. Earlier resumption statements below are dated history. Permitted work is preservation, the [foundation reading path](FOUNDATION.md), arc42/C4/Structurizr architecture and isolated [source recovery](RECOVERY.md); no features, devices, signing, release, deployment or merge.

Merged source was verified at d74832b1d2579762fc1aea0634ce409a9e4c21ec, including [PR #15](https://github.com/Limited-Underground/Firmware-Loader/pull/15), [.NET10 PR #16](https://github.com/Limited-Underground/Firmware-Loader/pull/16) and [integration #17](https://github.com/Limited-Underground/Firmware-Loader/pull/17). PR #17's full host check passed on its identified head. Current source is .NET10; old .NET8 evidence remains historical. No framework migration is part of this foundation.

Current foundation task: [LUF-0012a on Project #3](https://github.com/orgs/Limited-Underground/projects/3?pane=issue&itemId=291511595), child of the retained LUF-0012 recovery acceptance. This documentation is a topic candidate until separately reviewed/merged. Older local overlays stay preserved and unintegrated. Production remains inspection-only; physical and release gates remain open.

Current evidence: [source recovery](RECOVERY.md) records the independently reviewed exact-source hosted Windows gate41523963520 with SDK10.0.403 and its omitted raw/count/binary evidence. The earlier three evidence corrections and source/model proof passed independent review; this expanded [current V1 documentation](FOUNDATION.md) is a new coherent draft awaiting a fresh reading-path review. No local build or physical run is claimed. Next safe action: complete focused documentation/model checks, then obtain the coordinator-released fresh review and exact-delta release before committing/pushing through existing draft PR #18. Shared handbook integration is owned separately; its earlier012 snapshot consumed the prior corrected pins, not automatically this new draft. No product work, duplicate PR or merge follows.

## Current working state — October 25, 2026

The owner accepted the bounded source-recovery baseline on October 23. Ordinary Firmware Loader development may resume in this standalone repository under its task-specific rules. [Firmware Loader Project #3](https://github.com/orgs/Limited-Underground/projects/3) is the sole current task authority; the former Trail V1 Loader entries are archived history. Select the next outcome there, then inspect the actual branch, working changes, applicable contract and validation scope before editing.

After [PR #12](https://github.com/Limited-Underground/Firmware-Loader/pull/12) merged with its required host gate, approved `main` and GitHub `main` matched at `4477b3923239a533971378cfb24ea48509524ac8` on October 25. The checkout was clean at that post-merge checkpoint. Recheck Git before the next task. LUF-0012 is Done for the accepted bounded source recovery with retained exceptions. That result does not grant manual UI, clean-machine, signer, release, device-write or physical acceptance; production remains inspection-only. Consult the exact Project item before changing its scope.

The dated October 22 recovery checkpoints below describe their state at that time. Their earlier “work paused” and “owner acceptance open” statements are historical and do not override this accepted bounded resumption.

## Integrated .NET 10 candidate (PR #15 + PR #16) — November 2, 2026

Branch `claude/net10-integration-pr15-pr16` merges the unchanged heads of PR #16 (`afa030c`) and PR #15 (`6e9e285`) into `main` at `064d796`. After owner approval, PR #16 merged into `main` as `b67cd99` on 2026-11-02, and this branch was then updated from `main` for its final gate. The only conflict was this file, and both entries below are kept verbatim.

- Review of the no-write marker withdrawal against LUF-0009d: before its first command, the engine reaches the device only through `CurrentSelection` and the host lease, and it marks mutation possible under the command gate before the write command is invoked. Recovery markers, earlier unresolved records and any possibly submitted command are never withdrawn; a crash before the withdrawal lands leaves the marker unresolved. Two review fixes ride this branch: the adapter and lease contract now requires side-effect-free device observation and lease acquisition, because `CurrentSelection` is polled about every 100 ms while Ready; and a failed withdrawal no longer claims that recovery review "will" be required. The final review added regression tests for a withdrawal that throws (the marker stays, the stop is reported as retained, and the host lease is still released) and for the shipped panel's polling of the factory-less production workflow, which must stay inert.
- Owner decisions: the withdrawal makes the journal non-monotonic (an unresolved record can revert to the older resolved record) and removes the durable trace of a confirmed but unsent attempt. LUF-0009d revision 1 did not include this transition. On 2026-11-02 the owner accepted both decisions: .NET 10 LTS as the release runtime (LUF-0007a amendment) and the no-command-submitted withdrawal under its fail-closed and side-effect-free adapter requirements (LUF-0009d revision 2).
- Linux validation on SDK 10.0.403: both Release builds had 0 warnings and 0 errors, and all 181 portable groups passed on runtime 10.0.14. The real wrapper selected 10.0.403 and passed both offline build stages; its test stage stopped because the Windows Desktop runtime does not exist on Linux. The 10 WPF rendering groups and 23 Win32 store groups run only on Windows, and the hosted gate on the integration PR is their record.

No device, physical, clean-machine, signing or release evidence exists.

## .NET 10 LTS toolchain migration candidate — November 2, 2026

Branch `claude/dotnet10-lts-migration`, from `main` at `064d796` and separate from the integrity work in PR #15, is not merged. .NET 8 support ends on 2026-11-10; .NET 10 is the current LTS, supported until 2028-11-14.

- Both projects now target `net10.0-windows` with WPF. There are no NuGet package references or `netstandard` targets, and NuGet sources remain cleared. A new `global.json` requires a stable 10.0 SDK (`10.0.100`, `latestFeature`, no previews). The wrapper selects the highest stable 10.0 SDK, the CI receipt validator accepts only 10.0 SDK receipts, and the workflow provisions SDK 10.0.403 with runtime 10.0.14, the October 2026 security release.
- No source change was required. Array `Reverse()` calls in tests bind to the non-mutating .NET 10 `Enumerable` overload under C# 14. The official Windows client and server support matrices for 8.0 and 10.0 are identical. Over the same fuzz corpus, `main`'s inspector showed no new escaping exception type on .NET 10: the archive `IOException` escape no longer occurs, while the invalid-UTF-8 escape and unverified ZIP CRC-32 are unchanged and are addressed separately by PR #15.
- Linux host validation in a cloud session used the Microsoft-signed SDK 10.0.403. Both Release builds had 0 warnings and 0 errors, and all 159 portable groups passed on runtime 10.0.14, matching the .NET 8 baseline of 159. The real `tools/Test-Loader.ps1` under PowerShell 7.6.9 selected 10.0.403 and passed both offline Release build stages; its test stage stopped only because the Windows Desktop runtime does not exist on Linux. WPF rendering and Win32 store groups run only on Windows, and the hosted gate for this change is their record.

Owner decision: the LUF-0007a release boundary records `net8.0-windows`, so accepting this changes the runtime of that boundary. No device, physical, clean-machine, signing or release evidence exists.

## Integrity and interruption hardening candidate — November 2, 2026

Branch `claude/vigilant-pascal-w4pne4` (not merged) changes executable source; production remains inspection-only with no device adapter, signer trust or installation authority.

- Offline inspection (production path): a fuzz probe of 44,831 truncated or corrupted candidates showed malformed bytes escaping as `InvalidOperationException` (invalid UTF-8 in the manifest, which the window's handlers did not catch) and `IOException` (reported to the operator as an unreadable file), and that corrupted signature bytes still published because ZIP CRC-32 values were not checked. Every candidate-byte failure is now a typed refusal, entry CRC-32 values are verified, source read failures stay distinct, and each refusal or block has a specific sanitized outcome. Published results show the release generation and verified SHA-256.
- Host engine and durable store (fixture-only, not production-reachable): a stop after the durable uncertainty marker but before any adapter command now restores the exact prior resolved journal record under the live lease, so a provable no-write stop no longer forces recovery review or an unnecessary recovery write. Recovery-mode markers, any possibly submitted command and failed withdrawals keep the marker. Binding failures are classified (disconnected, changed or reconnected, unsupported, authority changed, profile, journal), a passive `Revalidate` detects them while Ready and revokes the issued confirmation, and the update workflow names the specific cause of each stop or uncertain outcome without adapter text.
- Host validation on Linux (cloud session): Microsoft .NET SDK 8.0.426 with `EnableWindowsTargeting` built the application and test projects in Release with warnings as errors at 0 warnings and 0 errors. A session-local portable runner compiled the real sources and executed all 179 non-WPF, non-Win32 groups, including 20 new groups, which passed. The 10 WPF rendering groups and 23 Win32 store groups, 3 of them new, run only under `tools/Test-Loader.ps1` on Windows and were not executed in that session; the hosted host gate for this change is the record of that run.

These are host results only. No device, physical, clean-machine, signing or release evidence exists, and no installation authority is added.

## Historical recovery baseline — October 22, 2026

[PR #11](https://github.com/Limited-Underground/Firmware-Loader/pull/11) merged as `932275461ef8d5183c399396fd0ae614a5f7e7d5`. The merged source includes the pure host preservation-binding preflight, validation output isolation and diagnostics corrections, host CI, and the corrected existing-directory relative-path refusal fixture. A fresh scoped source/Git checkpoint was independently restored and verified; restored host validation passed both Release builds with zero warnings/errors, all 189 groups and 13 WPF renderings. Earlier local-only, uncommitted and older-checkpoint observations below remain dated evidence. Maintenance remains report-only. File/tracking closeout and owner acceptance of the recovery baseline remain open; ordinary feature work stays paused. Production remains inspection-only, and no manual, clean-machine, device, release or physical acceptance is added by these results.

## Validation workflow recovery — October 22, 2026

The source/output isolation and validation diagnostics correction is applied locally. Both warning-as-error Release builds passed and the full189host groups passed, including actual WPF rendering into the isolated output directory. See [validation evidence and limits](testing/VALIDATION-WORKFLOW-RECOVERY-2026-10-22.md) and [current testing instructions](TESTING.md). Ordinary feature work remains paused for recovery; host success is not physical acceptance or owner resumption. Earlier dated status below remains historical.

Status: inspection-only preview; LUF-0009b host engine, LUF-0009c UI checkpoint and LUF-0009d durable host store owner-accepted; 2026-10-03.

The public repository moved into the Limited Underground organization and was
renamed `Firmware-Loader`. GitHub repository ID `1336427703`, `main` history,
visibility, and Apache License detection were preserved, and the former URL
redirects to <https://github.com/Limited-Underground/Firmware-Loader>. This
administrative migration changes no loader capability, trust, installation
authority, hardware evidence, or readiness claim.

## Proven

- A .NET 8 WPF application builds with warnings treated as errors.
- The first application surface presents exactly Limited Underground Trail and Limited Underground Display.
- One session controller permits only one selected product and invalidates its revision on product changes or return to the chooser.
- Unknown products fail without mutation, and exact reselection is a no-op.
- Every device, bundle, write, and recovery capability remains false.
- An immutable provider registry accepts at most one exact lifecycle-v1 provider per catalog product.
- Each accepted activation receives an opaque lease and nonzero generation; switch, chooser return, and application close revoke before an interlocked close-once call.
- Close/open exceptions, null providers, identity mismatches, stale contexts, reentrant operations, and owner disposal remain providerless and expose only generic status.
- Project-owned target rules are immutable and exact. Signer trust is a separate application-owned registry bound to the exact product/provider/version.
- Provider trust configuration cannot set signer verification or release admission.
- Production registers exactly one provider: `opentrail`, lifecycle version `1`, for Limited Underground Trail.
- Its immutable rule set admits only exact target key `heltec_v4_bench` and pins SHA-256 `ec818efab9a14ce4f0900068c9474acfe2577d74e2e39fa4850f3ff0567e9776` for the target-contract Git blob bytes from OpenTrail commit `a327104ac67a3f5918a8b0191c96dceb05b5399b`.
- Production signer trust remains empty; candidate inspection can publish bounded structure/product/digest results but never signer trust or admission.
- A canonical three-entry bundle-candidate schema is bounded by product key, target key, release generation, image length, SHA-256, signer identifier, and signature algorithm.
- Offline inspection contexts require an active provider lease and bind exact controller, session, activation, provider identity/generation, target-rule revision, optional trust revision, and context reference.
- Publication additionally requires an exact case-sensitive target in the active project-owned rule set. Signer trust and admission remain false.
- Stored and deflated ZIP entries are independently expansion-limited; exact maximum, maximum-plus-one, forged-size, oversized manifest/signature, stream-position restoration, cross-product, digest-mismatched, noncanonical, extra-entry, empty-signature, and unknown-product cases are covered.
- Trail exposes a one-file, read-only offline inspection workflow. It retains no local path, publishes sanitized fields only, clears stale results with the session lifecycle, and continues to report signer trust and installation admission as unavailable.
- Display keeps offline inspection unavailable because it has no accepted provider or target manifest.
- The original inspection baseline passed 63 deterministic groups and source-policy checks without launching the UI or accessing hardware; later host validation is recorded below.
- The independent public repository is published at <https://github.com/Limited-Underground/Firmware-Loader>; `main` is the default branch and GitHub detects Apache License 2.0.

## Not proven

- Production registers no Display provider and no signer-trust policy.
- No Display loader provider or accepted target manifest exists.
- No production signer verification/admission, device match, USB/serial/Bluetooth adapter, physical writer/readback/boot/recovery, installer, release signing, or physical acceptance exists here. The internal host checkpoint below uses isolated fixture authority and transports.
- No signed binary release, update channel, or public firmware package is configured.

## Next gate

The independently reviewed populated
[LUF-0009c UI checkpoint](testing/LUF-0009c-UI-CHECKPOINT-2026-09-29.md) is owner-accepted.
The LUF-0009b host engine is owner-accepted; its production trust and physical
limitations remain unchanged. The [LUF-0010a host matrix](testing/LUF-0010a-HOST-MATRIX-2026-10-01.md) now identifies exact available target/image/layout inputs and the remaining production release, real adapter, complete protected-span policy and independent recovery gates. Physical installation/recovery still requires separate exact setup approval and current authorization. [LUF-0009d durable host storage](testing/LUF-0009d-DURABLE-HOST-STORE-2026-10-01.md) is owner-accepted for its validated host-only scope under approved revision 1. Twenty new groups and the complete 136-group warning-as-error matrix pass. Cooperating-process exclusion and durable digest-only intent are validated in an isolated host store; production root/ACL enrollment, unrelated-tool exclusion, real device/power-loss acceptance and enabled installation remain separate.

## LUF-0009b host engine checkpoint

The [host engine report](testing/LUF-0009b-HOST-ENGINE-2026-09-29.md) records actual
internal fixture-envelope signature/admission validation and a selection-bound
asynchronous update/recovery state machine. That checkpoint passed 106 host groups and warning-as-error Release builds.
Production provider trust remains empty and all physical capabilities remain disabled.
Only deterministic fixtures implement device commands, exclusivity and journal
storage. Production release schema/trust, exact hardware adapter, durable storage,
physical verification/recovery and release evidence remain separate gates.
This host checkpoint is owner-accepted; it grants
no physical authority or V1 progress credit. The populated UI checkpoint below follows that accepted host work.

## LUF-0009c populated UI checkpoint

The actual MainWindow now contains the device/update panel. Production has no
device choices or engine factory, so installation stays disabled. Test injection
binds the same WPF controls to the real host engine. Selection, admitted firmware
identity, separate confirmation, write/readback/startup, uncertain outcome and
separate recovery are visible. Product/file/close invalidation and reselection
preserve uncertainty and reject stale action/results. Ten new UI groups and the
complete 116-group warning-as-error matrix pass; populated actual WPF renders
were reviewed, including minimum-window scrolling. See the linked report above.
This local UI checkpoint passed independent review and is owner-accepted; no
hardware, release, publication or progress credit is claimed.

## Recovery continuation — October 22, 2026

The isolated restored source also passed the full 189-group wrapper on a different volume from its temporary outputs, with two Release builds reporting zero warnings/errors and 13 isolated WPF renders. Independent declared-file/checkpoint comparisons passed afterward. See the separate restored-run observation in [validation evidence](testing/VALIDATION-WORKFLOW-RECOVERY-2026-10-22.md).

[File maintenance](MAINTENANCE.md) is adopted in report-only mode. It enables no scheduler or deletion. Its later documentation delta is preserved separately from the earlier source checkpoint. Known historical private members are covered by the checkpoint and their original-format provenance collection was separately copied and verified; originals remain retained. These bounded results do not establish all-root coverage, independent off-computer protection, physical acceptance or owner resumption. The PR #11 publication and scoped source-restoration results are recorded in the current baseline above. Remaining file/tracking closeout, manual UI, clean-machine, release, physical and owner-resumption decisions remain separate.
