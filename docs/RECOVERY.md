# Source recovery and reproducibility

This describes source-file/host recovery, **not** LUF-0010a physical firmware restoration. Product development remains frozen by the November 2, 2026 owner decision.

## Declared inputs

| Input | Identity |
| --- | --- |
| Source/history | Public repository https://github.com/Limited-Underground/Firmware-Loader.git; merged .NET 10 baseline **d74832b1d2579762fc1aea0634ce409a9e4c21ec** |
| Framework / SDK policy | net10.0-windows; [global.json](../global.json): stable 10.0.100, latestFeature roll-forward, no previews |
| Host prerequisites | Windows with WPF/desktop support, Git, PowerShell 7.5+, installed stable .NET 10 SDK with Windows Desktop reference/runtime packs |
| Packages | [NuGet.Config](../NuGet.Config) clears feeds; the app/test projects require no external PackageReference or secret feed |
| Validation | [Test-Loader.ps1](../tools/Test-Loader.ps1), full suite; [Testing](TESTING.md) owns selectors, receipts and focused-run rules |
| Protected dependencies | None needed to retrieve/build the public inspection-only source. Private overlays, captures, originals, signing material and device state are excluded, not reconstructed. |

The installed SDK is a declared prerequisite; this is not provisioning or qualification of a fresh Windows machine. An original checkout, its bin/obj, private recovery folders or a populated user NuGet cache must not supply hidden reconstruction inputs.

## Reconstruct in a new isolated directory

Run from an approved staging parent; choose a nonexistent task-owned destination. These commands leave an existing working copy untouched:

    git clone --no-local https://github.com/Limited-Underground/Firmware-Loader.git NEW_DIRECTORY
    git -C NEW_DIRECTORY checkout --detach d74832b1d2579762fc1aea0634ce409a9e4c21ec
    git -C NEW_DIRECTORY fsck --full
    git -C NEW_DIRECTORY status --porcelain

Record resolved source and source-file hashes. Create a separate task-owned temporary parent outside the clone and point TEMP/TMP there. Run the maintained wrapper from the clone in an isolated PowerShell process with LUF_UI_ONLY and LUF_STORE_ONLY not equal to 1. It creates fresh package cache, CLI home and artifacts, retains diagnostics/renderings/receipts, and restores environment variables it changes.

Require final success after both warning-as-error Release builds, the actual full-suite count, environment restoration and receipt finalization. A started receipt or one successful stage is not a full pass.

## November 2 reconstruction and evidence layers

| Layer | Evidence / limit |
| --- | --- |
| Source/history retrievable | Fresh remote HTTPS clone of exact .NET 10 baseline was materialized and checked. No original source overlay was copied into it. |
| Local-only source preserved | Older working overlay and Git state captured separately; membership/size/SHA-256, copied Git fsck and all-refs bundle verification passed. Originals/refs retained; private receipts own paths and seals. |
| Exact accepted binary retrievable | No signed/released installer or production artifact is accepted by this foundation. Hosted CI retains sanitized result metadata, not an accepted product binary. |
| Source rebuild | Pending coordinated heavy-work slot. No fresh local .NET 10 reconstruction run is claimed yet. |
| Byte-identical rebuild | Not established: no accepted binary reference selected. Record and explain source/toolchain/artifact differences before claiming equality. |
| Runtime/data restoration | Synthetic host-store/WPF cases belong to the executed host suite. No production enrollment, device data or physical runtime is restored. |
| Independent backup | Source and backup were verified on different physical internal disks. This is source-disk protection, not off-laptop/off-site protection. Protected .private holdings are excluded and remain a custody gap. |

[Integration PR #17](https://github.com/Limited-Underground/Firmware-Loader/pull/17) provides retained hosted evidence for its exact tested head; it is not a new local reconstruction run. Replace the pending rebuild row only after actual execution and verified receipts.

## Loss boundaries and retention

Never reset, stash, clean or silently integrate older local framework overlays to make a recovery claim. Reconstruct current main separately. Existing code to preserve is not automatically approved for main.

This source-only example does not recover excluded private holdings, unknown historical roots, complete remote metadata, downloaded OneDrive backup bytes, off-device storage, signer keys or production enrollment. Existing [October 22 recovery evidence](testing/VALIDATION-WORKFLOW-RECOVERY-2026-10-22.md) remains historical at its own revision/SDK/layer. No prior checkpoint, failed run or historical worktree is retired here; [Maintenance](MAINTENANCE.md) governs exact retention. A topic commit/draft PR is not a merged source authority or release.
