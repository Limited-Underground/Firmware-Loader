# Limited Underground Firmware Loader — Preview

**The November 2 indefinite freeze retains a narrow November 3 host exception for LUF-0009g.** The owner authorized accepted storage-policy bring-forward and isolated host validation in a clean source-bound topic. The [adopted host result and its limits](docs/PROJECT_STATUS.md#current-authority-and-next-action) include the storage bring-forward and the terminal sharing-denial regression. Production device and store wiring remain disabled. Other development remains frozen. The application remains **inspection-only**. Host validation never establishes physical acceptance.

Start with the [current V1 scope and reading path](docs/FOUNDATION.md), then [CONTRIBUTING](CONTRIBUTING.md) and [project status](docs/PROJECT_STATUS.md). They identify accepted requirements, implemented versus demonstrated capability, exact remaining gates and the next authorized step without private chat history. [Project #3](https://github.com/orgs/Limited-Underground/projects/3) owns live task status.

Public source: <https://github.com/Limited-Underground/Firmware-Loader>. The .NET 10 implementation baseline was merged at d74832b1d2579762fc1aea0634ce409a9e4c21ec (PRs #15–#17). Architecture/recovery documentation was adopted through [PR #18](https://github.com/Limited-Underground/Firmware-Loader/pull/18), merge commit 747467163ec597ef377fd6d71d5f8c0f09ad5914. Documentation adoption does not establish a production release.

## What the application does today

One .NET 10 WPF window presents exactly **Limited Underground Trail** and **Limited Underground Display**. Choosing a product opens a revision-bound provider session and clears prior authority; it does not inspect hardware or authorize a write. Only Limited Underground Trail has a production offline-inspection provider. Limited Underground Display remains providerless and its inspection action is unavailable.

For offline inspection, explicitly choose a product and use its local candidate picker. The application opens one selected file read-only, avoids recent-file registration and retains/displays no filename or private path. It accepts a bounded archive containing exactly manifest.json, image.bin and manifest.sig, checks canonical metadata, ZIP CRC-32, image length/SHA-256 and signature presence, and reports fixed sanitized outcomes. A product/session change, a new attempted inspection or close invalidates/clears the corresponding result; cancelling the native picker does not attempt a new candidate. See [interface details](docs/INTERFACES.md) and [runtime behavior](docs/ARCHITECTURE.md).

A present signature or matching digest is not release authentication. Production signer trust and admission remain false. There is no production device enumeration, USB/serial/Bluetooth adapter, write, erase, reset, boot confirmation, rollback or recovery path. Internal fake adapters and the host engine/store/validators exist for synthetic tests only; they are not production composition.

## V1 and remaining work

The accepted base-V1 installation/recovery scope is Limited Underground Trail, not the optional Limited Underground Display provider. It is incomplete. [Current V1 acceptance](docs/FOUNDATION.md#existing-v1-acceptance-still-required) maps the existing signer/release, exact-device, preservation/storage, physical recovery and package/operator gates to their owning records. Only the [named host exception](CONTRIBUTING.md#working-rules) may proceed; do not infer broader authority from historical backlog labels.

Each product owns its target manifests, artifacts, compatibility, signer and recovery decisions. Loader owns the common application/session and enforcement boundary. Technical keys `opentrail`, `opengauge` and existing source symbols retain their identities; public names do not become protocol keys.

## Build, test and recover source

From the intended Windows checkout with PowerShell 7.5+ and the declared stable .NET 10 SDK/Desktop prerequisites:

```powershell
.\tools\Test-Loader.ps1
```

[Testing](docs/TESTING.md) gives the practical full/focused commands, selector rules and retained diagnostics; its [documentation-only checks](docs/TESTING.md#documentation-checks) run without the SDK or an application build. [Recovery](docs/RECOVERY.md) pins source/toolchain/reference evidence and fresh reconstruction steps. Existing exact-source hosted Windows validation passed; documentation-only changes reuse it and do not claim a new local build, numerical test count, clean-machine or physical result.

## License and names

Source is under [Apache License 2.0](LICENSE). [BRANDING](BRANDING.md) retains name/trademark restrictions and the working-name clearance limit. [Third-party records](docs/THIRD_PARTY.md) distinguish platform/tool dependencies, provenance and unaccepted distribution work. No installer, signed production release or legal clearance is established by this Preview documentation.
