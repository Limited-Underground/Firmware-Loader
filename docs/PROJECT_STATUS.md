# Project status

## Current working state — October 25, 2026

The owner accepted the bounded source-recovery baseline on October 23. Ordinary Firmware Loader development may resume in this standalone repository under its task-specific rules. [Firmware Loader Project #3](https://github.com/orgs/Limited-Underground/projects/3) is the sole current task authority; the former Trail V1 Loader entries are archived history. Select the next outcome there, then inspect the actual branch, working changes, applicable contract and validation scope before editing.

Before this documentation update, local `main` and GitHub `main` matched at `f1ebbf388c22f7eb6263aaf01cbb5d5eb893ed5c` on October 25 and the working tree was clean. Recheck those facts for the next task. The accepted recovery and prior host validation do not grant manual UI, clean-machine, signer, release, device-write or physical acceptance. Production remains inspection-only. LUF-0012 tracks the accepted bounded source recovery and retained exceptions; consult its exact Project item before changing that scope.

The dated October 22 recovery checkpoints below describe their state at that time. Their earlier “work paused” and “owner acceptance open” statements are historical and do not override this accepted bounded resumption.

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
