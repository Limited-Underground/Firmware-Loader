# Loader validation instructions

These instructions describe the adopted validation interface. Host evidence and remaining qualification limits are recorded in project status; dated release evidence remains historical.

## Documentation checks

For current-documentation and documentation-tooling changes, run these offline PowerShell 7.5+ standard-library checks from the intended checkout; they require no SDK, network, package installation or product execution:

```powershell
.\tools\Test-LoaderDocumentation.ps1
.\tools\Test-LoaderDocumentation.Tests.ps1
```

The [validator](../tools/Test-LoaderDocumentation.ps1) and [negative fixtures](../tools/Test-LoaderDocumentation.Tests.ps1) are repository-local tooling. The validator derives its current-page selection from [Foundation's reading route](FOUNDATION.md#reading-route-and-reusable-procedures), checks inline local file/heading references, and compares declared runtime/SDK/source/target/model identities with the existing owning Recovery rows, project/global/workflow configuration, embedded target constants and UTF-8/LF model bytes. It also detects selected known freeze/inspection/publication contradictions and transient PR-based next actions. It does not create a second status authority.

A section headed exactly `## Retained history` ends a document's current selection. Links to historical source/evidence remain checked, but their original bodies are not rewritten or treated as current permissions. The negative fixtures replace input text in memory; they mutate no repository files. They demonstrate refusal of representative missing/escaping/broken references, source/tool/artifact mismatches and authority contradictions, plus acceptance of explicit history and date-only changes.

These are bounded consistency checks, not a Markdown implementation or proof of every paraphrase. They do not authenticate the declared Git commit or hosted artifact, contact external links/private Projects, infer freshness from timestamps, prove architecture truth, approve a task/ref, confer signer/device authority or establish production/physical acceptance. Fresh human/source review remains necessary for changed claims and access/owner decisions. Cooperative filesystem assumptions still apply.

The full Test-Loader.ps1 gate runs the validator and its fixtures before SDK enumeration, output allocation and the unchanged application/test build stages. A failed documentation preflight stops that invocation; there is no skip flag added to the full gate. An existing application-build result remains evidence for its original inputs, not proof that later gateway/checker code ran. Use the focused checks above for a documentation-only delta; do not repeat a completed host build merely to refresh a date.
## Full gate

From the intended checkout, run `./tools/Test-Loader.ps1` using PowerShell 7.5 or newer; the older synthetic qualification used 7.6.5, while the November 2 hosted receipt records 7.6.9. Windows PowerShell 5.1 is not supported by this candidate. Both LUF_UI_ONLY and LUF_STORE_ONLY must differ from the exact string `1`; the full wrapper refuses focused selection rather than resetting the caller's flags. An installed stable .NET 10 SDK is required: the wrapper selects the highest installed stable `10.0.x` SDK, consistent with the repository `global.json` (major/minor 10.0, latest feature band, no previews). No tool installation or online package retrieval is part of validation.

The wrapper builds Release application/tests with existing warning-as-error project settings, then runs the exact newly built test DLL. For a local run read the actual suite mode and count in its retained output; the hosted sanitized receipt reports full mode/stage success without a numeric count. Introductory or historical counts do not establish current coverage. Host test success never establishes device, physical, signing, deployment or owner acceptance.

## Candidate publication validation

Before authorized publication of executable, test or validation-tooling changes, require a successful full host matrix for the **exact candidate** or the verified intended merge result. Bind the result to that source identity and changed-file bytes, suite mode/selectors, all native stage results and final environment restoration. Testing only the merge base, an earlier head or a different tree is insufficient. Recheck the intended merge result when its base changes.

`tools/Test-Loader.ps1` is the local full-matrix entrypoint. `tools/Invoke-LoaderCi.ps1` is the GitHub-hosted Windows wrapper; do not substitute it for local validation or simulate its hosted environment. Use the existing invocation and retained-evidence rules below.

Focused store, UI or individual tests help diagnose and verify a bounded change, but do not replace required full publication validation. Report the actual selected/finished count for the tested revision; do not carry a later branch's count onto main or combine separate runs into one full result. Configured required CI and exact publication/Ready/merge approval remain applicable.

Documentation-only wording changes use the existing documentation checks and reuse genuinely unchanged application-build evidence. Do not rebuild the product solely for wording or claim that an older full run tested changed executable/tooling inputs.
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