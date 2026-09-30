# LUF-0008a Trail release admission authority proposal

OBSERVED 2026-09-23T19:00:32-04:00. **Owner review pending; production trust and installation admission remain unavailable.** LUF-0007a's boundary was accepted before this task started. That acceptance does not certify Windows/hardware support or authorize signing, device operations or publication. This report proposes the authority needed for that boundary and exercises current refusal behavior; it does not implement a release verifier.

## Owner decision: who can approve an update and which files it may accept

Approve or revise the following proposal: OpenTrail's owner appoints the people allowed to approve Trail release bytes and signing keys; the Loader's owner separately approves the exact trusted public-key fingerprints and revocation policy consumed by the application. A firmware file, provider, selected product, successful signature check or accepted task cannot appoint its own signer or grant a physical operation. Installation remains blocked until the signer, exact artifact, compatibility, safe address plan, current device, recovery and operation-specific authorization gates all pass.

No named signer, real key, key custodian, production certificate, release image or signing service is selected here. These are explicit missing inputs, not implicit defaults. Accepting this policy may allow subsequent design work; it cannot enable trust or a writer. If the owner wants to appoint those authorities now, the decision must identify the release approver and key custodian, exact public-key fingerprint/algorithm and trust revision, revocation publisher/freshness rule, release generation floor policy and target manifest/artifact. Secret material must never enter this report or repository.

## Reconcile the existing target and candidate schemas

The candidate boundary remains [LUF-0007a](LUF-0007a-V1-RELEASE-BOUNDARY-2026-09-23.md): one Trail provider, one HTIT-WB32LAF V4.2 board profile, Windows 11 Pro 25H2 x64 baseline and proposed USB ROM serial transport. Neither this task nor that proposal adds a Display dependency.

| Source | Observed identity and implication |
| --- | --- |
| [Canonical provider](../../src/LimitedUnderground.FirmwareLoader/OpenTrailInspectionProvider.cs) | `opentrail`, lifecycle 1, exact inspection key `heltec_v4_bench`; pinned historical OpenTrail Git blob SHA-256 `ec818efab9a14ce4f0900068c9474acfe2577d74e2e39fa4850f3ff0567e9776` at `a327104ac67a3f5918a8b0191c96dceb05b5399b`. Production signer policies remain empty. No pin was changed. ManifestSha256 is configured rule identity; there is no artifact-supplied target-manifest hash to compare. Implemented publication checks bind exact target key and current rule revision, not full artifact compatibility. |
| Current target-owner manifest | OpenTrail `firmware/targets/heltec_v4_bench/target-contract.json`: schema OTTB0/version 0, target_id `heltec-v4-bench-candidate`, supported=false, null received-revision/RF fields, no updater/OTA/storage/recovery authority. This owner target_id is distinct from the Loader inspection key; an accepted release must bind both explicitly. |
| Current partition source | OpenTrail `firmware/targets/heltec_v4_bench/partitions.csv` header describes OTHP0/v1; target-contract partition_layout still says schema_version 0. This disagreement must be reconciled by the target owner before any release manifest is pinned. No input was rewritten to hide it. |
| Historical physical plans | `physical-flash-plan.json` and `oled-startup-flash-plan.json` contain previously executed, consumed authorizations and old artifacts. The former includes a past full-chip erase; the latter an app-only span. Neither is a current operation plan or permission. Current files and recovery evidence must be independently frozen before any new operation. |
| [Current candidate inspector](../../src/LimitedUnderground.FirmwareLoader/FirmwareBundleCandidateInspector.cs) | Exact eight-field `firmware_bundle_candidate_v1` manifest plus image.bin and manifest.sig. It recognizes `rsa_pss_3072_sha256` and a nonempty 384-byte signature field but does not cryptographically verify it. The schema contains no target-manifest digest, firmware compatibility version, address plan, protected-region policy, revocation revision, freshness deadline or recovery artifact binding. It is insufficient as a production release-admission schema. |

Target-owner working-tree inputs were inspected read-only from the active OpenTrail checkout at HEAD `fb1d558d4791b24efdf2a4a95308e6981dc109ca`. Raw file SHA-256 observations are `bfa9407407041f403271d627914b3a2ddf417d0cafcf8fcd762fd1e0cba3f419` (target-contract.json, 7,013 bytes) and `9c27fd7e647ace6eb32524281bfaa52cd370419fec2457f1acdebc6b0cbfa52a` (partitions.csv, 557 bytes). These filesystem-byte observations are not normalized Git-blob comparisons or approved replacement pins. OpenTrail OT-0101c owns received-board identity evidence and its support limitations.

## Proposed authority matrix

This is a review proposal, not a registered cross-project contract or accepted trust configuration. Any future consumed release contract must be versioned and registered by its owners before use.

| Gate | Authority and required binding | Refusal rule / current state |
| --- | --- | --- |
| Target and compatibility | OpenTrail owns exact received model/revision/RF profile, target_id, Loader key mapping, manifest bytes/digest, partition layout, image type and supported prior firmware/bootloader versions. Loader enforces the accepted mapping. | Reject missing, stale, mismatched or unsupported mapping. Current historical inspection pin cannot admit current evaluation firmware. No deployable release selected. |
| Release approval | OpenTrail owner appoints release approver; approver signs off exact source/build identity, image bytes/hash, manifest, recovery evidence and allowed release generation. | No implicit approval from branch name, provider or digest. Named authority and release identity pending. |
| Signer enrollment | OpenTrail owner selects authorized release signer/custodian; Loader owner separately approves algorithm, full public-key fingerprint, provider/target scope and immutable trust revision. No trust-on-first-use or keys supplied by the candidate. | Unknown, empty, mismatched-scope or unapproved signer refused. Existing rsa_pss_3072_sha256 is candidate syntax, not an accepted cryptographic policy or key. Production registry stays empty. |
| Authenticity | Future verifier validates a signature over the exact canonical release manifest, including image digest/size, product, target, manifest/layout identities, generation and complete operation plan. | Altered bytes, invalid signature or unsupported algorithm refused. A digest and nonempty signature field do not satisfy authenticity. Exact envelope/version and cryptographic implementation pending separate accepted design. |
| Revocation | Loader-approved authenticated revocation publisher, monotonic policy revision and explicit freshness lifetime; a locally retained authenticated snapshot supports offline use only while valid. | Unknown/revoked signer, missing/stale/rollback snapshot or untrusted time/freshness state blocks admission. No implicit network-success requirement in the Trail field path. Publisher, clock basis, lifetime and recovery/rotation policy pending; no evaluator exists. |
| Firmware version/generation | OpenTrail defines compatibility semantics and authorized security/release generation floor; Loader must compare authenticated candidate metadata with trusted current-device state and durable anti-rollback state. | Reject incompatible version, lower generation or uncertain floor. A positive integer is currently syntax only; neither max value nor signature grants downgrade or update authority. Same-generation reinstall policy pending, default deny. |
| Offsets and protected regions | OpenTrail supplies exact image/address/length/hash plan and partition-layout identity. Loader validates checked arithmetic, erase-rounded spans, flash bounds, overlaps and target-owned allowlist before access. | Missing/extra segments, unsafe arithmetic, overlap, protected-region contact or unaccepted layout refused. Current schema has no offset support and does not evaluate any such ranges. |
| Device and operation | Loader owns fresh exact identity binding and explicit visible selection; owner separately authorizes the exact action/attempt. | Product selection, trust policy, admission or fixture success never selects a physical unit. Stale selection, reconnect or ambiguous identity invalidates operation. No adapter in current source. |
| Recovery and outcome | OpenTrail owns exact original/recovery artifact and boot/readback expectations; Loader owns truthful lifecycle enforcement. | No operation without independently accepted recovery and current original/artifact bindings. Partial write is an uncertain outcome, never success. Physical proof remains LUF-0010a. |
| Package/distribution | Loader owner approves exact binary, runtime/platform support evidence, signing custody and distribution route separately. | Source/tests do not authorize package signing or publication. LUF-0011a remains separate. |

Revocation expiration is proposed to block the Loader update operation only; it must not disable already installed offline-first Trail field communication. Refreshing a signed policy is distinct from transmitting private device data. No current network or key changes occur here.

## Address boundary to be reconciled by OpenTrail

These are observations from the current partition CSV and historical public plans, not authorized writable ranges. End addresses are exclusive. All writes currently remain denied.

| Observed span | Proposed default for a future app-only update |
| --- | --- |
| Bootloader/partition-table and gaps below 0x010000 | Preserve. No bootloader, partition-table, otadata, gap or erase-all changes without a separate reviewed target migration. Historical plan identifies table at 0x008000; it is not new authority. |
| otadata [0x009000,0x00b000) | Preserve; no OTA activation writes are authorized by this app-only proposal. |
| ordinary NVS [0x00d000,0x010000) | Preserve existing ownership/bond/ordinary state. It is not the historical encrypted rollback-floor authority. |
| factory app [0x010000,0x500000) | Only a future exact signed app image plus target-owner-accepted erase-rounded subspan may become a candidate. No present permission; maximum partition capacity is not permission to overwrite the whole partition. |
| ota_0 [0x500000,0xa00000), ota_1 [0xa00000,0xf00000) | Preserve; OTA slot changes/boot selection excluded until independently accepted. |
| ot_state [0xf00000,0x1000000) | Preserve reserved state; no storage authority implied by partition presence. |
| Flash outside [0,0x1000000), overlaps, integer wrap | Reject without opening a device. eFuse/security/key changes remain excluded. |

The future admitted manifest must specify the exact intended update type and file set. An app-only image, merged/full image and recovery image must never be interchangeable. The single-image inspection format cannot safely carry these meanings implicitly. Existing plans' consumed erase/reset settings must not be imported into a new release by default.

## Real flow and refusal points

| Before state / owner | Trigger and actual implementation | Required effect | Premature effect forbidden / failure |
| --- | --- | --- | --- |
| Unselected / session controller | MainWindow selects Trail; production factory activates exact lease | Current immutable provider/context; other session revoked | No device or trust authority; activation failures remain providerless. |
| Active lease / offline workflow | Explicit read-only file choice; inspector parses three-entry candidate | Bounded structure/digest/signature-presence checks; caller stream restored | Malformed schema/digest rejected before result publication; no cryptographic claim. |
| Inspection result / controller | CanPublishOfflineBundleInspection checks current context and exact product/target | Publish sanitized inspection fields only | Cross-product/target or stale context cannot publish; this is not release admission. |
| Inspected candidate / future admission owner | No production admission transition exists | Future independent authenticity, revocation, compatibility and address gates required | Current SignerTrusted and AdmissionAllowed always false, including plausible metadata. |
| Admitted release / future operation owner | No device/writer transition exists | Separate fresh selection, authorization, recovery and serialized operation required | No implicit write, reset, erase, boot or recovery. |
| Switch/close / session controller | Existing lease invalidation and workflow clearing | Old context loses publication authority | Future admission/operation tokens must obey the same invalidation plus device/trust revisions; not implemented here. |

## Negative fixtures and what they actually prove

[TrailReleaseAdmissionBoundaryTests.cs](../../tests/LimitedUnderground.FirmwareLoader.Tests/TrailReleaseAdmissionBoundaryTests.cs) adds 12 executable fixtures using the real production registry, controller and inspector. It changes no production code, keys or policy. All signatures are deliberately noncryptographic bytes; synthetic signer IDs contain no key material. Every case also checks stream restoration and that all physical capability flags remain false.

| Cases | Current observed refusal/result | Remaining evidence required for future admission |
| --- | --- | --- |
| Unknown signer; synthetic revoked-label signer | Well-formed inspection-only results, SignerTrusted=false and AdmissionAllowed=false; production trust empty | This is blanket non-admission, not a revocation lookup. Genuine/invalid signature and actually revoked trusted signer require a future verifier plus accepted policy fixtures. |
| Wrong product; wrong target (enrolled evaluation key) | CanPublishOfflineBundleInspection refuses exact production context mismatch | This proves inspection-publication refusal, not release admission or physical device matching. |
| Wrong schema v2; zero generation | Actual parser rejects unsupported manifest fields | These are schema/generation syntax checks, not firmware-version compatibility or anti-rollback tests. |
| Maximum positive generation | Inspection may publish, but cannot gain trust or admission | No accepted-generation floor, monotonic update or compatibility semantics exists. |
| Altered image with unchanged digest | Actual image SHA-256 check rejects before result publication | No authenticity claim; a malicious self-consistent image/digest remains untrusted. |
| Added write_offset into NVS, reserved state, beyond flash, integer maximum | Canonical property whitelist rejects each extra field | No range/overlap/erase rounding/protected-region arithmetic was executed. Future address validator must test these and boundary-touch/overlap cases with its actual accepted operation schema. |

There is no test that calls a mock verifier successful and then treats it as production trust. The new fixtures expose the currently missing semantic gates rather than claiming those implementations exist.

## Validation and closeout

VERIFIED 2026-09-23: the normal maintained `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Test-Loader.ps1` now passes: .NET SDK 8.0.425, warning-as-error Release application/test builds with zero warnings/errors, and **75 deterministic host groups** (63 existing plus 12 new). Windows baseline is the same observed host as LUF-0007a. Before the bounded validation repair, the maintained command reproduced default SDK 10 offline restore failure for absent 8.0.30 packs; LUF-0007a also recorded the SDK 8 run/artifact-path failure. The repair selects/reports the newest installed stable 8.0 SDK, invokes its CLI explicitly, builds the tests and executes their actual artifact DLL, and checks the temporary cleanup boundary. It installs nothing, changes no global/repository SDK or feed configuration, and leaves production application code untouched. Maintained-run temporary artifacts are removed by its existing cleanup lifecycle; retained alternate-run artifacts below remain host evidence only.

The earlier alternate command also passed; it is retained for reproducibility of the hashed artifacts below. The maintained command above is now the primary validation path. Earlier alternate command, canonical worktree:

```powershell
$validationDir = Join-Path $env:TEMP 'LUF-0008a-host-20260923'
New-Item -ItemType Directory -Path $validationDir -Force | Out-Null
& dotnet $sdkCli build tests/LimitedUnderground.FirmwareLoader.Tests/LimitedUnderground.FirmwareLoader.Tests.csproj --configuration Release --artifacts-path $validationDir
# Proceed only after exit code 0.
& dotnet (Join-Path $validationDir 'bin/LimitedUnderground.FirmwareLoader.Tests/release/LimitedUnderground.FirmwareLoader.Tests.dll') (Get-Location).Path
```

Generated host artifacts under `%TEMP%/LUF-0008a-host-20260923/bin`, timestamp 2026-09-23T22:59:58Z, source baseline plus these uncommitted test changes:

| Relative artifact | Bytes | SHA-256 |
| --- | --- | --- |
| LimitedUnderground.FirmwareLoader/release/LimitedUnderground.FirmwareLoader.dll | 69120 | `a23cc33d8aaeac1d0b97121a6f7128ab4051682d83d3072d6019e14e97c131fb` |
| LimitedUnderground.FirmwareLoader.Tests/release/LimitedUnderground.FirmwareLoader.Tests.dll | 75264 | `a1d308f1efbf230cb33b910d3d5197882317019f99587e27cadd3434bd9b03e1` |

Exact next action: owner reviews this authority proposal and its missing concrete inputs. LUF-0009a may start only after the owner accepts LUF-0008a and live task approval/dependencies pass. Production trust stays empty until separately accepted exact trust configuration and implementation gates pass; this task is review-ready, not owner-accepted or release-complete.
