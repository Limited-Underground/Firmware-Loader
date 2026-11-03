# Interfaces and contract selection

Scope: implemented source at **d74832b1d2579762fc1aea0634ce409a9e4c21ec**. Product development is frozen. This page selects existing authorities; it defines no new protocol.

## Production-facing boundaries

| Interface | Selected implementation | Proof and limit |
| --- | --- | --- |
| Family/provider lifecycle | [LoaderProviderLifecycle](../src/LimitedUnderground.FirmwareLoader/LoaderProviderLifecycle.cs), lifecycle contract version 1; [controller](../src/LimitedUnderground.FirmwareLoader/LoaderSessionController.cs) | Exact context/lease/generation binding. Product selection grants no signer or device authority. |
| Target facts | [OpenTrailInspectionProvider](../src/LimitedUnderground.FirmwareLoader/OpenTrailInspectionProvider.cs) | Exact opentrail / heltec_v4_bench rule; public contract commit and Git-blob SHA-256 are constants in source. Limited Underground Display has no provider. These are source-embedded constants with external provenance; there is no build-time retrieval or runtime networking. |
| Local candidate | [FirmwareBundleCandidateInspector](../src/LimitedUnderground.FirmwareLoader/FirmwareBundleCandidateInspector.cs), Schema and SerializeCanonicalManifest | Readable/seekable ZIP stream with exactly manifest.json, image.bin, manifest.sig; 20 MiB archive, 4 KiB manifest, 16 MiB image, 384-byte nonempty signature field. No signer authentication. |
| Sanitized outcome | [OfflineBundleInspectionWorkflow](../src/LimitedUnderground.FirmwareLoader/OfflineBundleInspectionWorkflow.cs), fixed OfflineBundleInspectionStatus | Refusals withhold candidate facts; published results carry inspected target/generation/length/digest, never path or raw exception. Publication is not admission. |

ZIP/CRC-32, UTF-8 JSON and SHA-256 are established formats/algorithms. The Loader adds explicit bounds, canonical encoding and lifecycle binding. Entry names, identifiers, limits and manifest property order are custom candidate-contract rules. The serializer writes schema, product_key, target_key, release_generation, image_bytes, image_sha256, signer_id and signature_algorithm in its defined order; source is the exact encoding authority. Structure, CRC and digest checks do not authenticate a signed production release.

## Host-only fixture contracts

[HostReleaseAdmission](../src/LimitedUnderground.FirmwareLoader/HostReleaseAdmission.cs) uses internal LUF-host-fixture-envelope-v1 canonical bytes, RSA-3072/PSS/SHA-256 and an explicit fixture policy. This synthetic binary envelope is not candidate JSON or an accepted product release format.

The [preservation/binding contract](HOST-PRESERVATION-BINDING-ADMISSION-CONTRACT.md) and [preflight](../src/LimitedUnderground.FirmwareLoader/HostPreservationBindingPreflight.cs) compare immutable single-image snapshots, full fixture-policy values and mathematical geometry. They issue no operational token and do not re-prove signature provenance. The range validator uses immutable half-open spans, bounded span counts, a positive power-of-two erase block, complete address-space coverage and erase-rounded allowed/protected containment. Payload binding snapshots exact offset/length/bytes under a 16 MiB aggregate host cap and returns copies, not mutable aliases. These are host resource/mathematical constraints, not accepted target geometry. The current pure preflight is standalone; HostUpdateEngine does not call it or consume its result as new authority.

[HostUpdateEngine](../src/LimitedUnderground.FirmwareLoader/HostUpdateEngine.cs) defines finite selection-bound adapter/profile/journal interfaces. Tests supply device commands. [HostOperationStore](../src/LimitedUnderground.FirmwareLoader/HostOperationStore.cs) persists bounded digest-only synthetic uncertainty under cooperating-process ownership; production root/account/ACL enrollment is unresolved. No real USB, serial, BLE or radio protocol is selected here.

## Owning decisions and remaining inputs

[Release authority](testing/LUF-0008a-RELEASE-AUTHORITY-2026-09-23.md), [lifecycle](testing/LUF-0009a-UPDATE-RECOVERY-LIFECYCLE-2026-09-23.md) and [host matrix](testing/LUF-0010a-HOST-MATRIX-2026-10-01.md) retain rationale and acceptance boundaries. Project #3 owns current LUF-0008b inputs and dependent admission/adapter/storage/composition tasks; [Current V1](FOUNDATION.md#existing-v1-acceptance-still-required) gives their scope/acceptance map. Future product work requires their accepted target, artifact, signer/revocation/freshness, layout/erase, transport and recovery decisions. This foundation supplies none of those decisions.
