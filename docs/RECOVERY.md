# Source recovery and reproducibility

This describes source-file/host recovery, **not** LUF-0010a physical firmware restoration. Product development remains frozen by the November 2, 2026 owner decision.

## Declared inputs

| Input | Identity |
| --- | --- |
| Source/history | Public repository https://github.com/Limited-Underground/Firmware-Loader.git; merged .NET 10 baseline **d74832b1d2579762fc1aea0634ce409a9e4c21ec** |
| Framework / SDK policy | net10.0-windows; [global.json](../global.json): stable 10.0.100, latestFeature roll-forward, no previews |
| Hosted SDK | [Workflow](../.github/workflows/loader-host.yml) provisions 10.0.403; installed SDK/Desktop packs remain declared inputs. |
| Host prerequisites | Windows with WPF/desktop support, Git, PowerShell 7.5+, installed stable .NET 10 SDK with Windows Desktop reference/runtime packs |
| Packages | [NuGet.Config](../NuGet.Config) clears feeds; the app/test projects require no external PackageReference or secret feed |
| Validation | [Test-Loader.ps1](../tools/Test-Loader.ps1), full suite; [Testing](TESTING.md) owns the practical invocation/selectors/receipts. [Hosted workflow](../.github/workflows/loader-host.yml) pins exact action commits and SDK10.0.403. |
| Protected dependencies | None needed to retrieve/build the public inspection-only source. Private overlays, captures, originals, signing material and device state are excluded, not reconstructed. |

The installed SDK is a declared prerequisite; this is not provisioning or qualification of a fresh Windows machine. An original checkout, its bin/obj, private recovery folders or a populated user NuGet cache must not supply hidden reconstruction inputs.

## Reconstruct in a new isolated directory

Run from an approved staging parent; choose a nonexistent task-owned destination. These commands leave an existing working copy untouched. They reconstruct the recorded implementation/build baseline and its original tooling, rather than selecting the live documentation branch. Select any current authorized documentation/tooling ref through the owning task and verify its own check results; retained host proof does not cover later gateway/checker edits:

    git clone --no-local https://github.com/Limited-Underground/Firmware-Loader.git NEW_DIRECTORY
    git -C NEW_DIRECTORY checkout --detach d74832b1d2579762fc1aea0634ce409a9e4c21ec
    git -C NEW_DIRECTORY fsck --full
    git -C NEW_DIRECTORY status --porcelain

Record resolved source and source-file hashes. Create a separate task-owned temporary parent outside the clone and point TEMP/TMP there. Run the maintained wrapper from the clone in an isolated PowerShell process with LUF_UI_ONLY and LUF_STORE_ONLY not equal to 1. It creates fresh package cache, CLI home and artifacts, retains diagnostics/renderings/receipts, and restores environment variables it changes.

Require final success after both warning-as-error Release builds, full-suite mode, environment restoration and receipt finalization. For a locally retained run, also record the actual full-suite count, renderings and output hashes. The hosted workflow retains a deliberately sanitized result with the limits stated below. A started receipt or one successful stage is not a full pass.

## November 2 reconstruction and evidence layers

| Layer | Evidence / limit |
| --- | --- |
| Source/history retrievable | Fresh remote HTTPS clone of exact .NET 10 baseline was materialized and checked. No original source overlay was copied into it. |
| Local-only source preserved | Older working overlay and Git state captured separately; membership/size/SHA-256, copied Git fsck and all-refs bundle verification passed. Originals/refs retained; private receipts own paths and seals. |
| Exact accepted binary retrievable | No signed/released installer or production artifact is accepted by this foundation. Hosted CI retains sanitized result metadata, not an accepted product binary. |
| Source rebuild | Fresh GitHub-hosted Windows run [41523963520](https://github.com/Limited-Underground/Firmware-Loader/actions/runs/41523963520) passed both warning-as-error Release builds and the full host test process with SDK 10.0.403 and PowerShell 7.6.9; finalization and environment restoration passed. This is hosted evidence, not a local SDK 10.0.303 run. |
| Byte-identical rebuild | Not established: no accepted binary reference selected. Record and explain source/toolchain/artifact differences before claiming equality. |
| Runtime/data restoration | Synthetic host-store/WPF cases belong to the executed host suite. No production enrollment, device data or physical runtime is restored. |
| Independent backup | Source and backup were verified on different physical internal disks. This is source-disk protection, not off-laptop/off-site protection. Protected .private holdings are excluded and remain a custody gap. |

[Draft PR #18](https://github.com/Limited-Underground/Firmware-Loader/pull/18), head ec08d996d14092a052897f63d538f0c4ab82118d, triggered that fresh run. The actual checkout was GitHub's synthetic merge **1de64b439403d9cb7e3329cdd5333d503d93038e** into the declared d74832b baseline. Its tree matches the topic head; the only differences from the baseline are eleven documentation/model files. Executable source, tests, SDK/dependency policy and validation tooling are unchanged. The disposable Windows runner retrieved remote source and provisioned the declared SDK with caching disabled; the wrapper used fresh package, CLI-home, build and evidence directories. NuGet feeds are cleared and no external PackageReference is required.

The retained sanitized artifact reports all three stages passed and environment restoration true. Its archive SHA-256 is **deb929653e115bb75dd75cc4ae447cec3fd00f84b2e934d7fe24bf52d6cb8290**. It deliberately excludes raw stage/final receipts, actual numeric group/render counts, renderings and built-output hashes; those are not claimed here. The remote-only source clone plus this exact-source hosted rebuild demonstrate source retrieval and hosted Windows build success, while accepted-binary custody, byte equality and production restoration remain separate. No local run occurred. Additional local execution requires a concrete unmet criterion and the coordinated resource slot; it is not necessary merely to repeat the demonstrated source-build outcome. [Integration PR #17](https://github.com/Limited-Underground/Firmware-Loader/pull/17) remains historical evidence at its own tested head.

## Reference artifacts and recoverability

| Reference | Exact identity / result | What it does not establish |
| --- | --- | --- |
| Authoritative source | Merged main d74832b1d2579762fc1aea0634ce409a9e4c21ec; global.json10.0.100/latestFeature/no preview, both projects net10.0-windows. | Unintegrated local overlays are preserved separately, not inputs to reconstruction. |
| Hosted source/build result | Run41523963520, actual merge1de64b439403d9cb7e3329cdd5333d503d93038e, artifact13178302781; exact archive digest above. Artifact is sanitized result.json, retained privately before hosted expiry. | No accepted application binary, raw local receipts or actual numeric test count. |
| Target-contract provenance | Production inspection key heltec_v4_bench; historical contract commit 8a16cf75280f53b8c085f040eb8304b070ad65a5, Git-blob SHA256 bf40d99aa261a616060672238eb0c301e462e164c5fa2ae3ccdabb7e78ebbbba. | Inspection rule identity, not deployable target support, device authority or release firmware. No live target-owner file is substituted. |
| Diagram reference | Versioned self-contained DSL and pinned official tool/output identities in [model/export record](architecture/README.md). | Generated presentation is derived documentation, not executable/device acceptance. |
| Accepted release binary / firmware / signer material | None selected or accepted by this source-only foundation. Existing dated bench/reference bytes retain their own scope and custody. | No byte-identical rebuild, production restore, key recovery or distributable release claim. |

Source/history retrieval and hosted source rebuild have passed independently of the original checkout. Exact accepted-binary retrieval/hash, byte equality and production runtime/data restoration are separate claims and remain unestablished or inapplicable to this inspection-only source example. Do not manufacture a new package, read device/customer data or copy keys to make those rows appear complete. [Third-party records](THIRD_PARTY.md) own the dependency/license/distribution boundary.
## Loss boundaries and retention

Never reset, stash, clean or silently integrate older local framework overlays to make a recovery claim. Reconstruct current main separately. Existing code to preserve is not automatically approved for main.

This source-only example does not recover excluded private holdings, unknown historical roots, complete remote metadata, downloaded OneDrive backup bytes, off-device storage, signer keys or production enrollment. Existing [October 22 recovery evidence](testing/VALIDATION-WORKFLOW-RECOVERY-2026-10-22.md) remains historical at its own revision/SDK/layer. No prior checkpoint, failed run or historical worktree is retired here; [Maintenance](MAINTENANCE.md) governs exact retention. A topic commit/draft PR is not a merged source authority or release.
