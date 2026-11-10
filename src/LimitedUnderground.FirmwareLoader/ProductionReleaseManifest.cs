using System.IO;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace LimitedUnderground.FirmwareLoader;

// T-0043. Typed, canonical release metadata. Parsing is not trust or operational admission.
internal sealed class ProductionReleaseManifest
{
    internal const string Format = "PRODUCTION-RELEASE-MANIFEST-V1";
    internal const ulong LifetimeMilliseconds = 180UL * 24 * 60 * 60 * 1000;
    internal const string Profile = "HTIT-WB32LAF-V4.2/ESP32-S3R2-v0.2/16MiB/2MiB/US915";
    private static readonly string[] Fields = ["format", "product", "target", "targetProfile", "targetDigest", "layoutDigest",
        "sourceBuildId", "rawImageLength", "rawImageSha256", "paddedImageLength", "paddedImageSha256",
        "destinationOffset", "destinationLength", "compatibility", "requiredBootloader", "requiredCurrentFirmware",
        "minimumCurrentGeneration", "maximumCurrentGeneration", "generation", "signerRole", "publicKeyFingerprint",
        "policyRevision", "issuedAtUtcMilliseconds", "expiresAtUtcMilliseconds", "writerProfileDigest",
        "preservationPlanDigest", "recoveryRequirementsDigest", "recoveryImageDigest"];
    private static readonly HashSet<string> Numbers = new(StringComparer.Ordinal)
    { "rawImageLength", "paddedImageLength", "destinationOffset", "destinationLength", "compatibility",
      "minimumCurrentGeneration", "maximumCurrentGeneration", "generation", "policyRevision",
      "issuedAtUtcMilliseconds", "expiresAtUtcMilliseconds" };
    private readonly JsonElement values;
    private ProductionReleaseManifest(JsonElement values) { this.values = values.Clone(); }
    internal string Text(string name) => values.GetProperty(name).GetString()!;
    internal ulong Number(string name) => values.GetProperty(name).GetUInt64();
    internal long IssuedAt => checked((long)Number("issuedAtUtcMilliseconds"));
    internal long ExpiresAt => checked((long)Number("expiresAtUtcMilliseconds"));
    internal ulong PolicyRevision => Number("policyRevision");
    internal HostReleaseManifest HostBindings => new(1, Text("product"), Text("target"), Text("targetDigest"),
        Text("layoutDigest"), checked((uint)Number("compatibility")), Number("generation"), Text("signerRole"),
        PolicyRevision, Text("paddedImageSha256"), checked((int)Number("paddedImageLength")),
        checked((uint)Number("destinationOffset")), Text("recoveryImageDigest"));

    internal byte[] CanonicalBytes()
    {
        using var bytes = new MemoryStream();
        using (var writer = new Utf8JsonWriter(bytes))
        {
            writer.WriteStartObject();
            foreach (var name in Fields)
                if (Numbers.Contains(name)) writer.WriteNumber(name, Number(name));
                else writer.WriteString(name, Text(name));
            writer.WriteEndObject();
        }
        return bytes.ToArray();
    }

    internal static bool TryParse(byte[]? input, out ProductionReleaseManifest? manifest)
    {
        manifest = null;
        if (input is null || input.Length == 0 || input.Length > 16384) return false;
        var bytes = (byte[])input.Clone();
        try
        {
            using var doc = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 2 });
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            var names = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
            // Exact sequence also excludes unknown, missing and duplicate fields.
            if (!names.SequenceEqual(Fields, StringComparer.Ordinal)) return false;
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (Numbers.Contains(p.Name))
                {
                    if (p.Value.ValueKind != JsonValueKind.Number || !p.Value.TryGetUInt64(out _)) return false;
                }
                else if (p.Value.ValueKind != JsonValueKind.String || p.Value.GetString() is not { } text ||
                    text.Length > 256 || text.Any(c => c < 32 || c > 126)) return false;
            }
            var m = new ProductionReleaseManifest(doc.RootElement);
            if (m.Text("format") != Format || m.Text("product") != "opentrail" || m.Text("target") != "heltec_v4_bench" ||
                m.Text("targetProfile") != Profile || m.Text("signerRole") != "release-approver-and-signing-custodian") return false;
            foreach (var name in new[] { "targetDigest", "layoutDigest", "rawImageSha256", "paddedImageSha256",
                "writerProfileDigest", "preservationPlanDigest", "recoveryRequirementsDigest", "recoveryImageDigest" })
                if (!Hash(m.Text(name))) return false;
            // Empty fingerprint represents unsigned draft metadata only; admission always refuses it.
            if (m.Text("publicKeyFingerprint") != "" && !Hash(m.Text("publicKeyFingerprint"))) return false;
            foreach (var name in new[] { "sourceBuildId", "requiredBootloader", "requiredCurrentFirmware" })
                if (string.IsNullOrWhiteSpace(m.Text(name))) return false;
            ulong raw = m.Number("rawImageLength"), padded = m.Number("paddedImageLength");
            if (raw == 0 || raw > padded || padded > 16 * 1024 * 1024 || m.Number("destinationLength") != padded ||
                m.Number("destinationOffset") > uint.MaxValue || padded > uint.MaxValue - m.Number("destinationOffset") ||
                m.Number("compatibility") == 0 || m.Number("compatibility") > uint.MaxValue ||
                m.Number("generation") == 0 || m.PolicyRevision == 0 ||
                m.Number("minimumCurrentGeneration") > m.Number("maximumCurrentGeneration") ||
                m.Number("issuedAtUtcMilliseconds") > (ulong)long.MaxValue - LifetimeMilliseconds ||
                m.Number("expiresAtUtcMilliseconds") != m.Number("issuedAtUtcMilliseconds") + LifetimeMilliseconds) return false;
            if (!bytes.AsSpan().SequenceEqual(m.CanonicalBytes())) return false;
            manifest = m; return true;
        }
        catch (JsonException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (OverflowException) { return false; }
        catch (ArgumentException) { return false; }
    }
    private static bool Hash(string text) => text.Length == 64 && text.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
}

internal sealed record ProductionCurrentState(string TargetProfile, string Bootloader, string Firmware, ulong Generation,
    string WriterProfileDigest, string PreservationPlanDigest, string RecoveryRequirementsDigest);
internal sealed record ProductionAdmittedRelease(ProductionReleaseManifest Manifest, HostAdmittedRelease HostRelease);

// Explicit isolated file state, never auto-created/enrolled by admission or registered in production.
// Exclusive read/check/advance plus Flush(true); missing/corrupt/unwritable state refuses.
internal sealed class ProductionAdmissionState
{
    private readonly string path;
    internal ProductionAdmissionState(string path) { this.path = Path.GetFullPath(path); }
    internal static ProductionAdmissionState CreateIsolatedTestState(string directory)
    {
        string root = Path.GetFullPath(directory);
        if (Directory.Exists(root)) throw new IOException("Existing test state refused.");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "production-admission.state");
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        file.Write(Encoding.ASCII.GetBytes("T-0043\n0\n0\n")); file.Flush(true);
        return new(path);
    }
    internal bool TryAdvance(long now, ulong revision)
    {
        if (now < 0 || revision == 0) return false;
        try
        {
            string? parent = Path.GetDirectoryName(path);
            if (parent is null || (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0 ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return false;
            using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            if (file.Length is < 10 or > 128) return false;
            byte[] bytes = new byte[(int)file.Length]; file.ReadExactly(bytes);
            string[] parts = Encoding.ASCII.GetString(bytes).Split('\n');
            if (parts.Length != 4 || parts[0] != "T-0043" || parts[3] != "" ||
                !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long high) ||
                !ulong.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out ulong policy) ||
                now < high || revision < policy) return false;
            byte[] next = Encoding.ASCII.GetBytes("T-0043\n" + now.ToString(CultureInfo.InvariantCulture) + "\n" + revision.ToString(CultureInfo.InvariantCulture) + "\n");
            file.Position = 0; file.SetLength(0); file.Write(next); file.Flush(true); return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}

internal static class ProductionReleaseAdmission
{
    internal static ProductionAdmittedRelease? Admit(byte[]? manifestBytes, byte[]? rawImage, byte[]? paddedImage,
        byte[]? signature, byte[]? publicKey, HostAdmissionPolicy? policy, ProductionCurrentState? current,
        ProductionAdmissionState? persistedState, long now, out string refusal)
    {
        refusal = "production-input";
        if (!ProductionReleaseManifest.TryParse(manifestBytes, out var m) || m is null || rawImage is null ||
            paddedImage is null || rawImage.Length > 16 * 1024 * 1024 || paddedImage.Length > 16 * 1024 * 1024 ||
            signature is null || publicKey is null || policy is null || current is null || persistedState is null) return null;
        rawImage = (byte[])rawImage.Clone(); paddedImage = (byte[])paddedImage.Clone();
        refusal = "production-binding";
        if (m.Text("publicKeyFingerprint") == "" || m.Text("publicKeyFingerprint") != policy.PublicKeyDigest ||
            m.ExpiresAt != policy.ValidUntil || now < m.IssuedAt ||
            (ulong)rawImage.Length != m.Number("rawImageLength") || (ulong)paddedImage.Length != m.Number("paddedImageLength") ||
            !paddedImage.AsSpan(0, rawImage.Length).SequenceEqual(rawImage) ||
            HostReleaseAdmission.Digest(rawImage) != m.Text("rawImageSha256") ||
            HostReleaseAdmission.Digest(paddedImage) != m.Text("paddedImageSha256") ||
            current.WriterProfileDigest != m.Text("writerProfileDigest") || current.PreservationPlanDigest != m.Text("preservationPlanDigest") ||
            current.RecoveryRequirementsDigest != m.Text("recoveryRequirementsDigest") || current.TargetProfile != m.Text("targetProfile") || current.Bootloader != m.Text("requiredBootloader") ||
            current.Firmware != m.Text("requiredCurrentFirmware") || current.Generation == 0 || current.Generation < m.Number("minimumCurrentGeneration") ||
            current.Generation > m.Number("maximumCurrentGeneration") || current.Generation >= m.Number("generation")) return null;
        var admitted = HostReleaseAdmission.AdmitProduction(m, paddedImage, signature, publicKey, policy, now, out refusal);
        if (admitted is null) return null;
        refusal = "persisted-clock-or-policy";
        if (!persistedState.TryAdvance(now, m.PolicyRevision)) return null;
        refusal = ""; return new(m, admitted);
    }
}