# Project status

Status: inspection-only preview; LUF-0009b accepted, populated LUF-0009c host/UI checkpoint owner-accepted; 2026-09-29.

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
limitations remain unchanged. Physical installation/recovery still requires
separate exact setup approval and authorization under LUF-0010a.

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
