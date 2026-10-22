# GitHub host validation

The `Loader host validation` workflow runs `Loader full host gate` on a disposable GitHub-hosted Windows Server 2025 runner. It invokes `tools/Invoke-LoaderCi.ps1`, which invokes the existing `tools/Test-Loader.ps1` full gate. It does not use `dotnet test`, launch a manual application session, flash hardware, publish a release or deploy anything.

The workflow provisions SDK 8.0.426 into its own runner directory. Provisioning uses the network; the subsequent wrapper retains the existing cleared NuGet sources and offline validation contract. The wrapper selects the highest stable .NET 8 SDK visible to that host and the sanitized receipt records the actual version. PowerShell must be at least 7.5. Hosted image updates and SDK availability can still cause a job failure; do not substitute another runtime silently to get a green result.

## Events and checks

Pull requests targeting main, pushes to main and manual dispatch trigger the gate. Normal pull requests use GitHub's event checkout, including its merge revision; the result records that tested commit, not necessarily the branch head. There are no path filters, secret inputs, package caches, deployment credentials or write permissions. Checkout does not persist credentials. Dependencies use full commit pins. Batch related changes into the existing PR to follow the shared CI cadence; do not push every small edit separately.

The host step has a 20-minute timeout within a 30-minute job. Failure is not converted into success with continue-on-error. The script requires a successful wrapper return and matching ownership/start/final receipts, full-suite selection, all three successful stage receipts and restored environment. A partial receipt is never a pass. These consistency checks are not protection against a malicious contributor changing the workflow or verifier; review remains necessary.

## Public evidence and diagnosis

The only uploaded file is `loader-ci-public/result.json`, retained for 14 days. It contains a constrained commit identity, result enums, stage results, SDK/PowerShell versions and environment-restoration outcome. It deliberately omits raw exceptions, local paths, logs, screenshots, application bundles and private records. Stage status is reported instead of inferring a historical test-group count.

Raw wrapper diagnostics remain only on the disposable runner and are lost when GitHub destroys it. This is intentionally less diagnostic retention than a local recovery run: automatically publishing raw logs is prohibited. A failure requiring raw diagnostics needs a separately reviewed, sanitized diagnostic change or an authorized local reproduction; do not broaden the artifact path to the checkout or TEMP. The artifact is short-lived CI evidence, not a project backup.

An incomplete result is written before the wrapper starts. If checkout, provisioning or adapter setup fails before that point, no summary may exist; upload then also fails rather than fabricating a pass. Hard runner loss or job cancellation can prevent upload entirely. Summary writes are not atomic, so interruption can also leave malformed JSON. Require valid JSON and a successful job conclusion; artifact presence alone never establishes a pass.

## Acceptance limits

A green check proves only this host job. It does not prove interactive selection, clean-machine packaging, signing, physical operation, firmware recovery, independent backups or owner acceptance. Adding the workflow does not configure required checks or branch protection. The initial adoption checkpoint was subsequently qualified by [hosted run 39846978593](https://github.com/Limited-Underground/Firmware-Loader/actions/runs/39846978593), which passed on SDK 8.0.426 and PowerShell 7.6.6. That result is tied to its tested revision; require the current PR head's successful check before any separately authorized merge.
