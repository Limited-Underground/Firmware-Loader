using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace LimitedUnderground.FirmwareLoader;

// L7 step 2 (D-063): the first signed release is a qualification release of the Trail bench build
// for board A only. This builds its canonical release record and the three contract documents the
// record names. Production wiring (L7 step 5) must recompute the same documents from this code.
// Building is not admission: nothing here signs, trusts or installs anything.
internal sealed record QualificationInputs(byte[] CandidateRaw, byte[] CurrentRaw, byte[] CurrentBootloader,
    string SourceBuildId, ulong Generation, ulong CurrentGeneration, ulong PolicyRevision, long IssuedAt, string HelperSha256);

internal sealed record QualificationRelease(byte[] Manifest, byte[] RawImage, byte[] PaddedImage, byte[] WriterProfile,
    byte[] PreservationPlan, byte[] RecoveryRequirements, byte[] RecoveryImage, HostAdmissionPolicy Policy, ProductionCurrentState ExpectedCurrent);

internal static class QualificationReleaseBuilder
{
    // Trail heltec_v4_bench target-contract.json (6,861 bytes) and partitions.csv (546 bytes),
    // as pinned in PRODUCTION-RELEASE-MANIFEST-V1.md and unchanged on Trail main at 1e45288.
    internal const string TargetDigest = "BF40D99AA261A616060672238EB0C301E462E164C5FA2AE3CCDABB7E78EBBBBA";
    internal const string LayoutDigest = "CD612068AD53B8BA7C3ECD0A2DB9128AD8B35515FAB4F10D9D0386E1CA9300F5";
    internal const uint Compatibility = 1;
    internal const byte PaddingByte = 0xFF; // erased flash
    internal const uint ProtectedLength = BenchFlash.AppOffset; // bootloader, table, otadata, nvs

    internal static byte[] Pad(byte[] raw)
    {
        if (raw.Length == 0 || raw.Length > BenchFlash.AppEnd - BenchFlash.AppOffset)
            throw new ArgumentException("image size");
        int padded = checked((raw.Length + (int)BenchFlash.Sector - 1) / (int)BenchFlash.Sector * (int)BenchFlash.Sector);
        if (padded > BenchFlash.AppEnd - BenchFlash.AppOffset) throw new ArgumentException("image size");
        var bytes = new byte[padded];
        Array.Fill(bytes, PaddingByte);
        raw.CopyTo(bytes, 0);
        return bytes;
    }

    // I-036: the bench bootloader region is 0x0..0x8000. The board's required bootloader state is
    // taken from a read-back of the board itself (at least that long), never from a build folder:
    // board A ran an older bootloader than the current builds.
    internal const int BootloaderRegionLength = 0x8000;

    internal static byte[] BootloaderRegionFromReadback(byte[] readback)
    {
        if (readback.Length < BootloaderRegionLength) throw new ArgumentException("the read-back must cover 0x0..0x8000");
        return readback.AsSpan(0, BootloaderRegionLength).ToArray();
    }

    internal static string ChipIdentity(uint offset, byte[] bytes) =>
        "chip-md5:0x" + offset.ToString("X") + ":" + bytes.Length + ":" + HostRegionCheck.Md5Hex(bytes);

    internal static byte[] Json(Action<Utf8JsonWriter> body)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) { writer.WriteStartObject(); body(writer); writer.WriteEndObject(); }
        return stream.ToArray();
    }

    internal static byte[] WriterProfile(string helperSha256) => BenchFlash.WriterProfile(helperSha256);

    internal static byte[] PreservationPlan() => Json(w =>
    {
        w.WriteString("format", "LU-PRESERVATION-PLAN-V1");
        w.WriteString("layout", "OTHP0/v1");
        w.WriteStartArray("spans");
        foreach (var (offset, length) in BenchFlash.Layout)
        {
            w.WriteStartObject(); w.WriteNumber("offset", offset); w.WriteNumber("length", length); w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteString("rule", "only-destination-range-may-change");
    });

    internal static byte[] RecoveryRequirements() => Json(w =>
    {
        w.WriteString("format", "LU-RECOVERY-V1");
        w.WriteString("method", "install-recovery-image-with-same-writer");
        w.WriteNumber("offset", BenchFlash.AppOffset);
        w.WriteString("confirmation", "separate");
        w.WriteString("verification", "chip-md5-every-span-and-boot-self-check");
    });

    internal static QualificationRelease Build(QualificationInputs input)
    {
        if (input.Generation <= input.CurrentGeneration || input.CurrentGeneration == 0 || input.PolicyRevision == 0 ||
            input.IssuedAt < 0 || string.IsNullOrWhiteSpace(input.SourceBuildId) || input.SourceBuildId.Length > 256 ||
            input.SourceBuildId.Any(c => c < 32 || c > 126) || input.CurrentBootloader.Length == 0 ||
            input.CurrentBootloader.Length > ProtectedLength)
            throw new ArgumentException("release inputs");
        var raw = (byte[])input.CandidateRaw.Clone();
        var padded = Pad(raw);
        var recovery = Pad(input.CurrentRaw);
        var writer = WriterProfile(input.HelperSha256);
        var plan = PreservationPlan();
        var recoveryDoc = RecoveryRequirements();
        long expires = checked(input.IssuedAt + (long)ProductionReleaseManifest.LifetimeMilliseconds);
        string boot = ChipIdentity(0, input.CurrentBootloader);
        string firmware = ChipIdentity(BenchFlash.AppOffset, input.CurrentRaw);
        string Sha(byte[] b) => HostReleaseAdmission.Digest(b);
        var manifest = Json(w =>
        {
            w.WriteString("format", ProductionReleaseManifest.Format);
            w.WriteString("product", "opentrail");
            w.WriteString("target", BenchFlash.Target);
            w.WriteString("targetProfile", ProductionReleaseManifest.Profile);
            w.WriteString("targetDigest", TargetDigest);
            w.WriteString("layoutDigest", LayoutDigest);
            w.WriteString("sourceBuildId", input.SourceBuildId);
            w.WriteNumber("rawImageLength", raw.Length);
            w.WriteString("rawImageSha256", Sha(raw));
            w.WriteNumber("paddedImageLength", padded.Length);
            w.WriteString("paddedImageSha256", Sha(padded));
            w.WriteNumber("destinationOffset", BenchFlash.AppOffset);
            w.WriteNumber("destinationLength", padded.Length);
            w.WriteNumber("compatibility", Compatibility);
            w.WriteString("requiredBootloader", boot);
            w.WriteString("requiredCurrentFirmware", firmware);
            w.WriteNumber("minimumCurrentGeneration", input.CurrentGeneration);
            w.WriteNumber("maximumCurrentGeneration", input.CurrentGeneration);
            w.WriteNumber("generation", input.Generation);
            w.WriteString("signerRole", ReleaseTrustAnchor.SignerRole);
            w.WriteString("publicKeyFingerprint", ReleaseTrustAnchor.PublicKeySha256);
            w.WriteNumber("policyRevision", input.PolicyRevision);
            w.WriteNumber("issuedAtUtcMilliseconds", input.IssuedAt);
            w.WriteNumber("expiresAtUtcMilliseconds", expires);
            w.WriteString("writerProfileDigest", Sha(writer));
            w.WriteString("preservationPlanDigest", Sha(plan));
            w.WriteString("recoveryRequirementsDigest", Sha(recoveryDoc));
            w.WriteString("recoveryImageDigest", Sha(recovery));
        });
        if (!ProductionReleaseManifest.TryParse(manifest, out var parsed) || parsed is null ||
            !parsed.CanonicalBytes().AsSpan().SequenceEqual(manifest))
            throw new InvalidDataException("built record is not canonical");
        var policy = new HostAdmissionPolicy(TargetDigest, LayoutDigest, Compatibility, input.Generation,
            ReleaseTrustAnchor.SignerRole, ReleaseTrustAnchor.PublicKeySha256, input.PolicyRevision, expires, false,
            BenchFlash.AppOffset, BenchFlash.AppEnd - BenchFlash.AppOffset,
            BenchFlash.Sector, 0, ProtectedLength, Sha(recovery));
        var current = new ProductionCurrentState(ProductionReleaseManifest.Profile, boot, firmware, input.CurrentGeneration,
            Sha(writer), Sha(plan), Sha(recoveryDoc));
        return new(manifest, raw, padded, writer, plan, recoveryDoc, recovery, policy, current);
    }
}
