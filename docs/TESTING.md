# Loader validation instructions

These instructions describe the adopted validation interface. Host evidence and remaining qualification limits are recorded in project status; dated release evidence remains historical.

## Full gate

From the intended checkout, run `./tools/Test-Loader.ps1` using PowerShell 7.5 or newer; the synthetic qualification uses 7.6.5. Windows PowerShell 5.1 is not supported by this candidate. Both LUF_UI_ONLY and LUF_STORE_ONLY must differ from the exact string `1`; the full wrapper refuses focused selection rather than resetting the caller's flags. An installed stable .NET8 SDK is required. No tool installation or online package retrieval is part of validation.

The wrapper builds Release application/tests with existing warning-as-error project settings, then runs the exact newly built test DLL. Read the actual suite mode and count in this run's output/receipt; introductory or historical counts do not establish coverage. Host test success never establishes device, physical, signing, deployment or owner acceptance.

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

The [GitHub host gate](testing/CI-HOST-GATE.md) describes the workflow, sanitized evidence, failure handling and acceptance limits. Hosted execution remains unverified until an actual run succeeds.
