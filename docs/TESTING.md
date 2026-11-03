# Loader validation instructions

These instructions describe the adopted validation interface. Host evidence and remaining qualification limits are recorded in project status; dated release evidence remains historical.

## Full gate

From the intended checkout, run `./tools/Test-Loader.ps1` using PowerShell 7.5 or newer; the older synthetic qualification used 7.6.5, while the November 2 hosted receipt records 7.6.9. Windows PowerShell 5.1 is not supported by this candidate. Both LUF_UI_ONLY and LUF_STORE_ONLY must differ from the exact string `1`; the full wrapper refuses focused selection rather than resetting the caller's flags. An installed stable .NET 10 SDK is required: the wrapper selects the highest installed stable `10.0.x` SDK, consistent with the repository `global.json` (major/minor 10.0, latest feature band, no previews). No tool installation or online package retrieval is part of validation.

The wrapper builds Release application/tests with existing warning-as-error project settings, then runs the exact newly built test DLL. For a local run read the actual suite mode and count in its retained output; the hosted sanitized receipt reports full mode/stage success without a numeric count. Introductory or historical counts do not establish current coverage. Host test success never establishes device, physical, signing, deployment or owner acceptance.

## Practical full invocation

Use an isolated PowerShell 7.5+ process from the intended source checkout. Choose an approved task-owned temporary parent outside source, create it first and set both TEMP/TMP to it. Do not use another task's output or an existing validation-run directory. In that isolated caller:

```powershell
# TEMP/TMP already point at the existing task-owned temporary parent.
$env:LUF_UI_ONLY = '0'
$env:LUF_STORE_ONLY = '0'
.\tools\Test-Loader.ps1
```

This one command builds both Release projects and executes the freshly built test DLL; no separate dotnet run is needed. It selects the highest installed stable10.0 SDK and creates fresh NuGet packages, CLI home, artifacts and evidence directories. The installed SDK/Desktop packs are declared prerequisites, not cached project input. NuGet sources are cleared; the source has no external PackageReference. A missing prerequisite is a blocker to diagnose, not permission to add a feed or migrate the runtime.

For a retained local run inspect ownership.json, started.json, all three stage JSON/log pairs and final.json. Require app-build/test-build/tests native exit0, final status passed, suite full and environment_restored=true; record the actual output count/renderings and artifact hashes. The script restores the variables it changes, while this isolated caller owns its chosen TEMP/TMP and selector settings. A CI sanitized summary deliberately omits raw paths/logs/counts; it must not be described as local raw evidence.
## Direct focused runner

The ordinary runner takes exactly two arguments after its DLL: source root and a new screenshot directory outside the source tree. The screenshot parent must already exist; the child must not. Example argument shape:

```powershell
& dotnet $builtTestDll $sourceRoot $newScreenshotDirectory
```

Select UI-only with LUF_UI_ONLY=`1` or store-only with LUF_STORE_ONLY=`1` in an isolated caller process. When both equal `1`, existing store-only precedence remains. Restore the flags afterward if using a persistent shell. Focused success is never the full gate. Child store fixtures use their internal child-dispatch contract, not these ordinary arguments.

Source root is for source-policy reads; screenshots never default to source/.private. Existing output paths, duplicate PNG names and observed reparse boundaries fail rather than overwrite evidence. Generated fixture renderings are not an interactive application walkthrough.

## Retained diagnostics

Every allocated run retains wrapper-owned build outputs, logs, screenshots and receipts on success or failure for this recovery pilot. The wrapper reports the exact local run path. A started or stage-only receipt is not a pass; require final success after tests, caller environment restoration and evidence finalization. Preflight failures may precede allocation and must not be described as saved runs.

Never publish raw local logs automatically. Some test fixture classes still dispose their own temporary stores; retained wrapper outputs do not mean all fixture bytes survived. Do not delete earlier failed runs or recovery checkpoints because a newer run passed. Maintenance retirement requires its own policy and coverage decision.

## Current limits

The output guards assume cooperative single-user execution and do not eliminate hostile concurrent path replacement. The applied correction passed a full189-group host run with actual WPF rendering; see [validation evidence](testing/VALIDATION-WORKFLOW-RECOVERY-2026-10-22.md). A later isolated restored-source run also passed all 189 groups with source/working directory and temporary output on different volumes; see the separate restored-run observation in that evidence record. Manual interaction and physical acceptance remain unverified by these host results. Consult project status for current executed cases and remaining gaps.

## Hosted CI

The [GitHub host gate](testing/CI-HOST-GATE.md) describes the workflow, sanitized evidence, failure handling and acceptance limits. Hosted execution has demonstrated a successful full host gate for [PR #17](https://github.com/Limited-Underground/Firmware-Loader/pull/17) on November 2, 2026. This result applies to that tested head and hosted environment, not a new local run or clean-machine/physical acceptance. [Source recovery](RECOVERY.md) owns the fresh reconstruction result and its limits.

## Proven troubleshooting and limits

- Older dotnet run/artifacts-path launch failure: use the maintained wrapper's exact newly built DLL invocation; do not revive the retired command from September evidence.
- Focused selectors accidentally set: the full wrapper refuses them. Run in the isolated caller above, not a UI-only/store-only process presented as a full pass.
- Damaged candidate bytes versus stream I/O: CRC/UTF-8/archive/canonical/digest failures map to fixed candidate refusals; a caller-stream read failure remains an I/O failure. Preserve that distinction rather than exposing raw exception text.
- Stale session/device or uncertain operation: source/fixture checks revoke stale authority and retain uncertainty after any possible command. The accepted no-command-submitted withdrawal is narrow; it never turns a submitted/unknown outcome into success.
- Host output protection assumes cooperating execution. It is not defense against hostile concurrent filesystem replacement or proof of production ACL/enrollment.

Source ownership and the associated tests are linked from [Architecture](ARCHITECTURE.md); current V1 gaps are selected once in [Foundation](FOUNDATION.md). Reuse valid unchanged evidence and diagnose a relevant failing transition before adding a check or running a broader suite.