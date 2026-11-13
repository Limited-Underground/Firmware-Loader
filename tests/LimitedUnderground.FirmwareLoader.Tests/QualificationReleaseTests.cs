using LimitedUnderground.FirmwareLoader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// L7 step 2: the qualification release builder. A record built here, signed with a throwaway
// test key in place of the owner's, must pass the Loader's real production admission.
internal static class QualificationReleaseTests
{
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("L7 builder pads with erased-flash bytes to whole sectors", PadsToSectors),
        ("I-036 builder takes the bootloader region from a board read-back", BootloaderFromReadback),
        ("L7 builder contract documents are canonical and bound to the helper", ContractDocuments),
        ("L7 builder record is canonical, anchored and expires in exactly 180 days", RecordIsCanonical),
        ("L7 builder refuses bad generations and inputs", RefusesBadInputs),
        ("L7 builder record signed by a test key passes real production admission", PassesRealAdmission),
        ("L7 builder admission refuses a changed field or a different board state", AdmissionRefusesChanges),
    };

    private static readonly string Helper = new('A', 64);

    private static void Require(bool ok, string what)
    {
        if (!ok) throw new InvalidOperationException("L7 builder: " + what);
    }

    private static void Throws(Action action, string what)
    {
        try { action(); }
        catch (Exception) { return; }
        throw new InvalidOperationException("L7 builder: expected refusal: " + what);
    }

    private static byte[] Bytes(int length, int seed)
    {
        var b = new byte[length]; new Random(seed).NextBytes(b); return b;
    }

    private static QualificationInputs Inputs() => new(Bytes(604_950, 1), Bytes(604_944, 2), Bytes(22_480, 3),
        "trail-test/abcdef0", 2, 1, 1, 1_800_000_000_000, Helper);

    private static void BootloaderFromReadback()
    {
        var readback = Bytes(0x10000, 9);
        var region = QualificationReleaseBuilder.BootloaderRegionFromReadback(readback);
        Require(region.Length == 0x8000 && region.AsSpan().SequenceEqual(readback.AsSpan(0, 0x8000)), "first 0x8000 bytes");
        Throws(() => QualificationReleaseBuilder.BootloaderRegionFromReadback(new byte[0x7FFF]), "short read-back");
        var release = QualificationReleaseBuilder.Build(Inputs() with { CurrentBootloader = region });
        Require(ProductionReleaseManifest.TryParse(release.Manifest, out var m) && m is not null &&
                m.Text("requiredBootloader") == QualificationReleaseBuilder.ChipIdentity(0, region) &&
                m.Text("requiredBootloader").StartsWith("chip-md5:0x0:32768:", StringComparison.Ordinal), "required bootloader covers the region");
    }

    private static void PadsToSectors()
    {
        var raw = Bytes(5000, 4);
        var padded = QualificationReleaseBuilder.Pad(raw);
        Require(padded.Length == 8192 && padded.AsSpan(0, raw.Length).SequenceEqual(raw) && padded.Skip(raw.Length).All(b => b == 0xFF), "padding");
        Require(QualificationReleaseBuilder.Pad(new byte[4096]).Length == 4096, "exact sector unchanged");
        Throws(() => QualificationReleaseBuilder.Pad(Array.Empty<byte>()), "empty");
        Throws(() => QualificationReleaseBuilder.Pad(new byte[0x4F0001]), "larger than the app partition");
        EsptoolHostUpdateAdapter.SpansFor(EsptoolHostUpdateAdapter.AppOffset, padded.Length);
    }

    private static void ContractDocuments()
    {
        Require(QualificationReleaseBuilder.WriterProfile(Helper).AsSpan().SequenceEqual(QualificationReleaseBuilder.WriterProfile(Helper)), "stable profile");
        Require(HostReleaseAdmission.Digest(QualificationReleaseBuilder.WriterProfile(Helper)) !=
                HostReleaseAdmission.Digest(QualificationReleaseBuilder.WriterProfile(new string('B', 64))), "helper changes the profile");
        Throws(() => QualificationReleaseBuilder.WriterProfile("abc"), "bad helper digest");
        var plan = JsonNode.Parse(QualificationReleaseBuilder.PreservationPlan())!;
        Require(plan["spans"]!.AsArray().Count == EsptoolHostUpdateAdapter.Layout.Length, "every layout span in the plan");
        Require(JsonNode.Parse(QualificationReleaseBuilder.RecoveryRequirements())!["format"]!.GetValue<string>() == "LU-RECOVERY-V1", "recovery document");
        Require(QualificationReleaseBuilder.ChipIdentity(0x10000, new byte[] { 1 }) == "chip-md5:0x10000:1:" + Convert.ToHexString(MD5.HashData(new byte[] { 1 })), "identity format");
    }

    private static void RecordIsCanonical()
    {
        var input = Inputs();
        var release = QualificationReleaseBuilder.Build(input);
        Require(ProductionReleaseManifest.TryParse(release.Manifest, out var m) && m is not null, "parses");
        Require(m!.CanonicalBytes().AsSpan().SequenceEqual(release.Manifest), "round trip");
        Require(m.Text("publicKeyFingerprint") == ReleaseTrustAnchor.PublicKeySha256, "anchor fingerprint");
        Require(m.ExpiresAt - m.IssuedAt == (long)ProductionReleaseManifest.LifetimeMilliseconds, "180 days");
        Require(m.Number("generation") == 2 && m.Number("maximumCurrentGeneration") == 1, "generations");
        Require(m.Text("rawImageSha256") == HostReleaseAdmission.Digest(input.CandidateRaw) &&
                m.Text("paddedImageSha256") == HostReleaseAdmission.Digest(release.PaddedImage), "image digests");
        Require(m.Text("recoveryImageDigest") == HostReleaseAdmission.Digest(QualificationReleaseBuilder.Pad(input.CurrentRaw)), "recovery image");
        Require(m.Text("writerProfileDigest") == HostReleaseAdmission.Digest(release.WriterProfile) &&
                m.Text("preservationPlanDigest") == HostReleaseAdmission.Digest(release.PreservationPlan) &&
                m.Text("recoveryRequirementsDigest") == HostReleaseAdmission.Digest(release.RecoveryRequirements), "contract digests");
        Require(release.Manifest.AsSpan().SequenceEqual(QualificationReleaseBuilder.Build(input).Manifest), "same inputs, same bytes");
    }

    private static void RefusesBadInputs()
    {
        var input = Inputs();
        foreach (var bad in new[] { input with { Generation = 1 }, input with { CurrentGeneration = 0 }, input with { PolicyRevision = 0 },
            input with { SourceBuildId = "" }, input with { SourceBuildId = "bad\nid" }, input with { CurrentBootloader = Array.Empty<byte>() },
            input with { CandidateRaw = Array.Empty<byte>() }, input with { IssuedAt = -1 }, input with { HelperSha256 = "x" } })
            Throws(() => QualificationReleaseBuilder.Build(bad), "bad input");
    }

    // The owner's private key is never available to tests; swap in a throwaway key and its digest.
    private static (byte[] Manifest, byte[] Signature, byte[] Key, HostAdmissionPolicy Policy) SignWithTestKey(QualificationRelease release,
        Action<JsonObject>? change = null)
    {
        using var rsa = RSA.Create(3072);
        var key = rsa.ExportSubjectPublicKeyInfo();
        var node = JsonNode.Parse(release.Manifest)!.AsObject();
        node["publicKeyFingerprint"] = HostReleaseAdmission.Digest(key);
        change?.Invoke(node);
        var bytes = Encoding.UTF8.GetBytes(node.ToJsonString());
        return (bytes, rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss), key,
            release.Policy with { PublicKeyDigest = HostReleaseAdmission.Digest(key) });
    }

    private static ProductionAdmittedRelease? Admit(QualificationRelease release, byte[] manifest, byte[] signature, byte[] key,
        HostAdmissionPolicy policy, ProductionCurrentState current)
    {
        var root = Path.Combine(Path.GetTempPath(), "lu-l7-builder-" + Guid.NewGuid().ToString("N"));
        try
        {
            var state = ProductionAdmissionState.CreateIsolatedTestState(root);
            return ProductionReleaseAdmission.Admit(manifest, release.RawImage, release.PaddedImage, signature, key, policy, current,
                state, Inputs().IssuedAt + 1000, out _);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static void PassesRealAdmission()
    {
        var release = QualificationReleaseBuilder.Build(Inputs());
        var (manifest, signature, key, policy) = SignWithTestKey(release);
        var admitted = Admit(release, manifest, signature, key, policy, release.ExpectedCurrent);
        Require(admitted is not null, "admitted");
        Require(admitted!.HostRelease.CopyImage().AsSpan().SequenceEqual(release.PaddedImage), "admitted image is the padded image");
        Require(admitted.HostRelease.Manifest.Offset == EsptoolHostUpdateAdapter.AppOffset, "app offset");
    }

    private static void AdmissionRefusesChanges()
    {
        var release = QualificationReleaseBuilder.Build(Inputs());
        var (manifest, signature, key, policy) = SignWithTestKey(release);
        var altered = (byte[])manifest.Clone();
        int at = Encoding.UTF8.GetString(altered).IndexOf("trail-test", StringComparison.Ordinal);
        altered[at] = (byte)'T';
        Require(Admit(release, altered, signature, key, policy, release.ExpectedCurrent) is null, "changed field after signing");
        foreach (var current in new[] { release.ExpectedCurrent with { Bootloader = "chip-md5:0x0:1:" + new string('0', 32) },
            release.ExpectedCurrent with { Firmware = "other" }, release.ExpectedCurrent with { Generation = 2 },
            release.ExpectedCurrent with { WriterProfileDigest = new string('E', 64) } })
            Require(Admit(release, manifest, signature, key, policy, current) is null, "different board state or writer");
        Require(Admit(release, manifest, signature, key, policy with { MinimumGeneration = 3 }, release.ExpectedCurrent) is null, "policy floor");
    }
}
