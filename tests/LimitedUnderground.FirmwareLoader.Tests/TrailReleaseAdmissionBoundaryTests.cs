using LimitedUnderground.FirmwareLoader;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

// LUF-0008a fixtures exercise the CURRENT inspection boundary, not a future
// cryptographic verifier, revocation service, rollback floor or address validator.
internal static class TrailReleaseAdmissionBoundaryTests
{
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } =
        new (string Name, Action Run)[]
        {
            ("unknown signer fixture cannot gain production admission", () => CheckFixture("1111111111111111", Expected.InspectionOnly)),
            ("revoked-label fixture cannot gain production admission without revocation implementation", () => CheckFixture("2222222222222222", Expected.InspectionOnly)),
            ("wrong-product release fixture cannot publish in Trail session", () => CheckFixture("1111111111111111", Expected.PublicationDenied, product: "opengauge")),
            ("wrong-target release fixture cannot publish in Trail session", () => CheckFixture("1111111111111111", Expected.PublicationDenied, target: "heltec_v4_enrolled_eval")),
            ("wrong-schema release fixture is rejected before inspection publication", () => CheckFixture("1111111111111111", Expected.FieldsRejected, transform: text => text.Replace("firmware_bundle_candidate_v1", "firmware_bundle_candidate_v2", StringComparison.Ordinal))),
            ("zero-generation release fixture is rejected before inspection publication", () => CheckFixture("1111111111111111", Expected.FieldsRejected, generation: 0)),
            ("positive generation has no rollback or release authority", () => CheckFixture("1111111111111111", Expected.InspectionOnly, generation: ulong.MaxValue)),
            ("altered-image release fixture is rejected by actual digest check", () => CheckFixture("1111111111111111", Expected.DigestRejected, alterImage: true)),
            ("offset into ordinary NVS is refused as unsupported schema", () => CheckOffset("53248")),
            ("offset into reserved state is refused as unsupported schema", () => CheckOffset("15728640")),
            ("offset beyond flash is refused as unsupported schema", () => CheckOffset("16777216")),
            ("overflow offset is refused as unsupported schema", () => CheckOffset("18446744073709551615")),
        };

    private enum Expected { InspectionOnly, PublicationDenied, FieldsRejected, DigestRejected, SchemaRejected }

    private static void CheckOffset(string offset) => CheckFixture(
        "1111111111111111", Expected.SchemaRejected,
        transform: text => text[..^1] + ",\"write_offset\":" + offset + "}");

    private static void CheckFixture(
        string signer, Expected expected, string product = "opentrail",
        string target = "heltec_v4_bench", ulong generation = 1,
        Func<string, string>? transform = null, bool alterImage = false)
    {
        Require(LoaderProductionProviders.SignerTrustPolicies.Count == 0, "production trust must remain empty");
        using var controller = new LoaderSessionController(
            LoaderProductionProviders.Factories, LoaderProductionProviders.SignerTrustPolicies);
        Require(controller.SelectProduct("opentrail"), "Trail selected");
        Require(controller.TryCreateOfflineBundleInspectionContext(out var context) && context is not null, "real production context");
        var image = new byte[] { 0x13, 0x27, 0x39, 0x4b };
        var manifest = FirmwareBundleCandidateInspector.SerializeCanonicalManifest(
            product, target, generation, (uint)image.Length,
            Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant(), signer);
        if (transform is not null)
        {
            manifest = Encoding.UTF8.GetBytes(transform(Encoding.UTF8.GetString(manifest)));
        }
        if (alterImage) image[0] ^= 0xff;
        using var candidate = new MemoryStream();
        using (var archive = new ZipArchive(candidate, ZipArchiveMode.Create, leaveOpen: true))
        {
            Entry(archive, "manifest.json", manifest);
            Entry(archive, "image.bin", image);
            // Deliberately not a cryptographic signature. No keys exist in these fixtures.
            Entry(archive, "manifest.sig", Enumerable.Repeat((byte)0x5a, FirmwareBundleCandidateInspector.SignatureBytes).ToArray());
        }
        candidate.Position = 3;
        var expectedError = expected switch
        {
            Expected.FieldsRejected => "Candidate manifest fields are not accepted.",
            Expected.DigestRejected => "Candidate image digest does not match its manifest.",
            Expected.SchemaRejected => "Candidate manifest is not canonical.",
            _ => null,
        };
        FirmwareBundleCandidateResult? result = null;
        try
        {
            result = FirmwareBundleCandidateInspector.Inspect(candidate, context!);
        }
        catch (InvalidDataException exception)
        {
            Require(expectedError is not null && exception.Message == expectedError,
                "actual first rejecting boundary must match fixture expectation");
        }
        Require(candidate.Position == 3, "caller stream position restored");
        if (expectedError is not null)
        {
            Require(result is null, "invalid candidate must not produce an inspection result");
        }
        else
        {
            Require(result is not null, "well-formed candidate must reach actual authority boundary");
            Require(!result!.SignerTrusted && !result.AdmissionAllowed, "presence/digest must never admit release");
            Require(controller.CanPublishOfflineBundleInspection(context!, result) == (expected == Expected.InspectionOnly),
                "exact production product/target publication boundary");
        }
        var snapshot = controller.Snapshot;
        Require(!snapshot.ConnectedDeviceInspectionAvailable && !snapshot.FirmwareBundleSelectionAvailable &&
                !snapshot.DeviceBundleMatchAvailable && !snapshot.FirmwareWritingAvailable && !snapshot.RecoveryAvailable,
                "no fixture may open physical authority");
    }

    private static void Entry(ZipArchive archive, string name, byte[] bytes)
    {
        using var stream = archive.CreateEntry(name, CompressionLevel.NoCompression).Open();
        stream.Write(bytes);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
