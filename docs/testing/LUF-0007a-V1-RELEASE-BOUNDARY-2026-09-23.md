# LUF-0007a Canonical Trail Loader V1 release boundary

OBSERVED 2026-09-23T18:31:06-04:00. Proposal and host evidence for owner review; no supported installation release is declared. This boundary proposal was subsequently owner-accepted; physical acceptance remains separate. This is the bounded Trail branch of the existing LUF-006 installation/recovery gate, allocated as LUF-0006a in the checklist. Display is excluded and is not a dependency of this Trail proposal.

## Source and scope reconciliation

Sources: [provider](../../src/LimitedUnderground.FirmwareLoader/OpenTrailInspectionProvider.cs), [architecture](../ARCHITECTURE.md), [status](../PROJECT_STATUS.md), [backlog](../../tasks/BACKLOG.md), [remaining task scope](../../tasks/V1_REMAINING_PLAN.md), [project configuration](../../src/LimitedUnderground.FirmwareLoader/LimitedUnderground.FirmwareLoader.csproj), and [maintained validation](../../tools/Test-Loader.ps1).

## Exact proposed release matrix

These are candidate boundaries for owner approval, not accepted support claims. Approval of this matrix authorizes no physical action, signer change or distribution.

| Dimension | Exact candidate to accept or revise | Current evidence and limitation |
| --- | --- | --- |
| Product/provider | Limited Underground Trail; engineering product/provider `opentrail`; lifecycle contract version 1 | VERIFIED source and deterministic tests. The existing chooser still presents Trail and Display; this scope changes neither chooser nor Display. |
| Windows | Windows 11 Pro, version 25H2, x64; initial validation baseline build 26200.9457 | OBSERVED host registry: DisplayVersion 25H2, EditionID Professional, CurrentBuild 26200, UBR 9457; runtime OS architecture X64. Host tests passed here. Populated WPF UI, standard-user/clean-machine installation and future cumulative builds are UNKNOWN. Other Windows versions, editions, ARM64 and x86 are outside this proposed initial claim. |
| Runtime/toolchain | Existing `net8.0-windows` WPF application; tested with SDK 8.0.425 and installed Windows Desktop runtime/reference pack 8.0.31 | VERIFIED local build/test only. No self-contained/framework-dependent package choice or supported runtime servicing policy is accepted. The target framework's generated windows7.0 suffix is not Windows 7 acceptance. |
| One hardware target candidate | Heltec WiFi LoRa 32 V4, HTIT-WB32LAF V4.2; ESP32-S3/S3R2 revision v0.2; 40 MHz crystal, 16 MiB flash, physically identified 2 MiB PSRAM | OpenTrail-owned OT-0101c audit identifies two received units independently from OT-103 and OT-119. Identity evidence does not establish electrical, RF or production support. Current evaluation builds use no PSRAM. Exact RF/antenna applicability remains target-owner acceptance. |
| Loader target rule | Existing exact `heltec_v4_bench` only | VERIFIED provider pin: target-contract Git blob SHA-256 `ec818efab9a14ce4f0900068c9474acfe2577d74e2e39fa4850f3ff0567e9776`, OpenTrail source commit `a327104ac67a3f5918a8b0191c96dceb05b5399b`. This is an inspection rule, not a deployable release identity. Current OpenTrail bench contract retains `supported=false`; enrolled/pair evaluation artifacts cannot silently substitute for this rule. LUF-0008a must reconcile exact release manifest, target key and new pin before admission. |
| Current transport | One offline local ZIP candidate stream selected explicitly and opened read-only | VERIFIED host workflow and source checks. No connected-device transport exists. No installation support today. |
| Proposed future physical transport | One directly attached USB data cable, using the ESP32-S3 ROM serial bootloader route for this exact target | INFERRED candidate from target-owner recovery history; UNKNOWN in canonical Loader. No USB enumeration, serial adapter, driver/device-binding policy, automatic reset policy or writer exists here. Exact interface/driver, entry/exit method and independent recovery must be accepted under LUF-0009a and physically validated under LUF-0010a. BLE, LoRa, OTA/network updates and other transports are excluded. |

Target-owner evidence read: OpenTrail `docs/testing/OT-0101c-TARGET-CONFIGURATION-AUDIT-2026-09-21.md` in the active OpenTrail worktree. It explicitly leaves production support blocked and requires manifest reconciliation; no live device identity, port or hardware status was inferred. Historical restoration success does not grant current recovery authority.

## Capability and authority matrix

VERIFIED entries below mean current source plus the 63 host groups, not UI/hardware acceptance.

| Capability | Existing evidence | Remaining gate / owner |
| --- | --- | --- |
| Selection/session/provider lifecycle | VERIFIED exact products, one selected session, immutable provider identity, lease/revision binding, revoke-before-close, stale/cross-controller/reentrant refusal | Populated UI/operator review remains UNKNOWN. Loader owns implementation. |
| Offline candidate inspection | VERIFIED exact manifest.json/image.bin/manifest.sig archive, canonical manifest, size/expansion limits, image SHA-256, nonempty fixed-size signature presence, exact target/product, stream restoration | A present signature is not cryptographic authenticity. No device operation is admitted. |
| Inspection UI | VERIFIED source-policy checks and workflow tests: one read-only picker, sanitized output, no retained path, session result clearing | Window was not launched; visual/accessibility/operator acceptance UNKNOWN. |
| Signer/revocation/release admission | VERIFIED production signer registry empty; trust and admission remain false | BLOCKED pending LUF-0008a. OpenTrail owns artifact/target signer decisions; Loader owns independently bound trust enforcement. No signer identity, custody holder, revocation source or release approver has been appointed by this report. |
| Exact physical device selection | VERIFIED operational capability false; no enumeration or identity adapter | BLOCKED pending LUF-0009a/b/c and separately authorized physical proof. Explicit visible selection required; family identity alone cannot authorize a write. |
| Write/offset/protected regions | VERIFIED no mutation adapter | BLOCKED pending product-owned exact artifact/hash/address plan, protected-region policy and reviewed adapter. No offset or erase plan selected here. |
| Readback and boot confirmation | No canonical implementation or physical evidence | BLOCKED pending independent byte comparison and exact post-boot evidence under LUF-0010a. Host fakes cannot close this row. |
| Interruption/rollback/recovery | Target-owner historical recovery evidence exists; no canonical Loader recovery implementation | BLOCKED pending independently verified restoration for each supported outcome and exact current device/original. No blanket reset, rollback, erase or recovery permission. |
| Packaging/signing/distribution | No accepted package, clean-machine result or signed release in this scope | BLOCKED pending LUF-0011a and separate operation-specific owner authorization. Build output is not a distributable release. |

## Current host validation and tooling limitations

1. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Test-Loader.ps1` in the sandbox could not read the normal user NuGet configuration. This is an execution-environment failure, not a source regression.
2. The same command outside the sandbox exited 1: default SDK 10.0.303 requested absent 8.0.30 reference/host packs, while installed packs are 8.0.31 and repository NuGet sources are cleared. No packages or SDKs were installed and no source/feed configuration changed.
3. Process-local SDK 8.0.425 selection built the application with zero warnings/errors, but the maintained script's `dotnet run --artifacts-path` attempted the default test bin path and failed to launch. The maintained script therefore did not pass.
4. Explicit SDK 8 build and actual test DLL invocation passed: zero warnings/errors, all 63 groups. Source-policy checks are part of those groups; they are not live UI acceptance. This bounded alternate invocation establishes current build/host evidence but does not conceal the maintained-command defect. Repairing the runner is a separate bounded implementation proposal.

Reproducible successful commands (PowerShell, canonical worktree; temporary output only):

```powershell
$validationDir = Join-Path $env:TEMP 'LUF-0007a-host-20260923'
New-Item -ItemType Directory -Path $validationDir -Force | Out-Null
& dotnet $sdkCli build tests/LimitedUnderground.FirmwareLoader.Tests/LimitedUnderground.FirmwareLoader.Tests.csproj --configuration Release --artifacts-path $validationDir
# Proceed only after exit code 0.
& dotnet (Join-Path $validationDir 'bin/LimitedUnderground.FirmwareLoader.Tests/release/LimitedUnderground.FirmwareLoader.Tests.dll') (Get-Location).Path
```

Retained host outputs beneath `%TEMP%/LUF-0007a-host-20260923` are compile/test evidence only, generated from the source identity above at 2026-09-23T22:30:51Z. Each DLL is 69,120 bytes:

| Relative output | SHA-256 |
| --- | --- |
| bin/LimitedUnderground.FirmwareLoader/release/LimitedUnderground.FirmwareLoader.dll | `0cfe6c046aba71feae10311cfacbc307ada574b53a551e292ae52e2bfbf66a0c` |
| bin/LimitedUnderground.FirmwareLoader.Tests/release/LimitedUnderground.FirmwareLoader.Tests.dll | `0da25c3644b7eb7424ee84ccb1240ae68bb4503a138d918d2e5a49e9480782a8` |

## Owner checkpoint and closeout

Owner review must accept or revise the exact candidate Windows/board/transport rows and the authority split. This report leaves cryptographic identities, release artifact, offsets, protected regions, device binding and recovery procedures unresolved for their approved successor tasks. No declaration of support is frozen before that decision. LUF-0008a remains dependency-blocked until the owner accepts LUF-0007a; subsequent approvals never authorize physical or publication actions implicitly.

Only this new evidence document was authored. Existing backlog/plan modifications were preserved; source, status and frozen historical tree were unchanged. No Git mutation, signing, device access, installation, remote verification, publication or deployment occurred. No V1 completion credit or public website status changed. Work is local and uncommitted; the parent task handles the hosted evidence submission and owner review.
