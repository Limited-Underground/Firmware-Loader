# License, IP and third-party records

Scope: source baseline d74832b1d2579762fc1aea0634ce409a9e4c21ec and this documentation/model delta. This is a factual inventory and routing record, not legal clearance, an IP ownership determination or release approval.

## Repository license and names

The repository [LICENSE](../LICENSE) is Apache License 2.0 and is unchanged. [BRANDING.md](../BRANDING.md) retains the separate trademark/endorsement restrictions and working-name clearance limit. Public names are Limited Underground Firmware Loader — Preview, Limited Underground Trail and Limited Underground Display. Technical keys/source symbols and historical records keep their exact identities.

No patent, trademark registration, contributor assignment, third-party relicensing or attorney clearance is established by the source build or this documentation review. Do not infer that an open-source code license grants product-name rights. The existing LUF-0011a release process must use the actual package contents, applicable notices and recorded naming decision; this page selects no new legal outcome.

## Dependencies and provenance

| Material | Current use / provenance | Applicable record and limit |
| --- | --- | --- |
| Loader C# / XAML / tests | Source in this repository; no external PackageReference in either project, no extra package feed. | Repository LICENSE; [project files](../src/LimitedUnderground.FirmwareLoader/LimitedUnderground.FirmwareLoader.csproj), [test project](../tests/LimitedUnderground.FirmwareLoader.Tests/LimitedUnderground.FirmwareLoader.Tests.csproj), [NuGet.Config](../NuGet.Config). This is not a claim that the installed SDK contains no third-party components. |
| .NET 10 and WPF | Existing platform dependency; hosted SDK10.0.403. Framework-dependent build, no accepted bundled runtime/package. | Upstream [runtime license](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT), [WPF license](https://github.com/dotnet/wpf/blob/main/LICENSE.TXT) (MIT), and [runtime third-party notices](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT). Use the license/notices actually accompanying any later selected binary; main-branch links are references, not a binary bill of materials. |
| Git, PowerShell and pinned GitHub Actions | Development/hosted-validation tools, not Loader runtime components or a redistributed installer here. | [Workflow](../.github/workflows/loader-host.yml) pins action commits; [PowerShell license](https://github.com/PowerShell/PowerShell/blob/master/LICENSE.txt). Exact tool versions/source-build proof are in [Recovery](RECOVERY.md). Preserve each tool's supplied license if separately redistributed. |
| Structurizr / generated site | Pinned official documentation tool; model source is versioned, generated presentation/vendor assets remain in the private handbook evidence. | [Model/export pins](architecture/README.md), [official binaries](https://docs.structurizr.com/binaries). No vendor JS/CSS or WAR is newly imported into this repository. An exported site has its own bundled components; this record does not relicense them under the Loader license. |
| Target-contract provenance | Source-embedded key/hash and historical public contract commit in OpenTrailInspectionProvider.cs. No firmware/original-device payload is copied into Loader. | [Interfaces](INTERFACES.md), [release authority](testing/LUF-0008a-RELEASE-AUTHORITY-2026-09-23.md). Hash/provenance is not target support, signer trust or permission to redistribute an artifact. |

## Distribution boundary

There is no accepted signed installer, production firmware bundle or self-contained runtime distribution in this foundation. Consequently no completed package-specific third-party notice inventory, reproducible-binary equality or legal release clearance is claimed. Before an authorized LUF-0011a package/distribution operation, bind the actual chosen bytes, embedded/runtime dependencies, licenses/notices and branding decision to that existing release record. Retain their original notices and repository visibility; do not publish private keys or raw recovery material to close an inventory gap.
