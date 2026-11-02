using LimitedUnderground.FirmwareLoader;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

// Realistic damaged-file scenarios for the production offline inspection path:
// truncated downloads, flipped bytes, corrupted archive directories and read failures.
internal static class CandidateIntegrityTests
{
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("integrity invalid UTF-8 manifest is a typed refusal not a crash", InvalidUtf8Manifest),
        ("integrity corrupted signature bytes fail archive CRC-32 instead of publishing", CorruptedSignature),
        ("integrity corrupted image bytes fail archive CRC-32 before use", CorruptedImage),
        ("integrity directory offset past archive end is damage not file I/O", DirectoryOffsetPastEnd),
        ("integrity caller stream read failure reports file unavailable", SourceReadFailure),
        ("integrity each refusal maps to a specific sanitized outcome", RefusalOutcomes),
        ("integrity blocked candidates explain product target and session cause", BlockedOutcomes),
        ("integrity published outcome shows verified SHA-256 and generation", PublishedDigest),
        ("integrity damaged and truncated corpus never escapes or publishes altered bytes", DamagedCorpus),
    };

    private static readonly byte[] Image = Enumerable.Range(0, 1024).Select(i => (byte)(i * 13 + 7)).ToArray();
    private static readonly byte[] Signature = Enumerable.Repeat((byte)0x5a, FirmwareBundleCandidateInspector.SignatureBytes).ToArray();

    private static void InvalidUtf8Manifest()
    {
        var manifest = Manifest("opentrail", "heltec_v4_bench", Image);
        var index = Encoding.ASCII.GetString(manifest).IndexOf("heltec", StringComparison.Ordinal);
        manifest[index] = 0xff; // Same length; only the UTF-8 validity changes.
        var bundle = Bundle(manifest, Image, Signature, CompressionLevel.NoCompression);
        ExpectRejection(bundle, FirmwareBundleCandidateRejection.ManifestInvalid);
        var outcome = Workflow(bundle);
        Require(outcome.Status == OfflineBundleInspectionStatus.ManifestRejected &&
            outcome.Heading == "Candidate not accepted" && !outcome.Published, "manifest outcome");
    }

    private static void CorruptedSignature()
    {
        var bundle = Bundle(Manifest("opentrail", "heltec_v4_bench", Image), Image, Signature, CompressionLevel.NoCompression);
        bundle[IndexOf(bundle, Signature) + 100] ^= 0x01;
        ExpectRejection(bundle, FirmwareBundleCandidateRejection.EntryChecksumMismatch);
        var outcome = Workflow(bundle);
        Require(outcome.Status == OfflineBundleInspectionStatus.ArchiveDamaged &&
            outcome.Heading == "Integrity check failed" && !outcome.Published &&
            outcome.Summary.Contains("CRC-32", StringComparison.Ordinal), "damaged outcome");
    }

    private static void CorruptedImage()
    {
        var bundle = Bundle(Manifest("opentrail", "heltec_v4_bench", Image), Image, Signature, CompressionLevel.NoCompression);
        bundle[IndexOf(bundle, Image) + 512] ^= 0x80;
        ExpectRejection(bundle, FirmwareBundleCandidateRejection.EntryChecksumMismatch);
        Require(Workflow(bundle).Status == OfflineBundleInspectionStatus.ArchiveDamaged, "image damage outcome");
    }

    private static void DirectoryOffsetPastEnd()
    {
        foreach (var level in new[] { CompressionLevel.NoCompression, CompressionLevel.Optimal })
        {
            var bundle = Bundle(Manifest("opentrail", "heltec_v4_bench", Image), Image, Signature, level);
            RewriteCentralDirectory(bundle, "image.bin", 42, (uint)bundle.Length + 4096);
            ExpectRejection(bundle, FirmwareBundleCandidateRejection.ArchiveUnreadable);
            var outcome = Workflow(bundle);
            Require(outcome.Status == OfflineBundleInspectionStatus.NotCandidateArchive &&
                outcome.Heading == "Candidate not accepted", "corrupt directory is not reported as file I/O");
        }
    }

    private static void SourceReadFailure()
    {
        var bundle = Bundle(Manifest("opentrail", "heltec_v4_bench", Image), Image, Signature, CompressionLevel.NoCompression);
        var (controller, context) = Session("opentrail");
        using (controller)
        {
            using var failing = new FailingReadStream(bundle, failAfterBytes: 64) { Position = 5 };
            bool io = false;
            try { FirmwareBundleCandidateInspector.Inspect(failing, context); }
            catch (InvalidDataException) { Require(false, "read failure is not a verdict on the candidate"); }
            catch (IOException) { io = true; }
            Require(io && failing.Position == 5, "read failure surfaces as I/O and restores position");
        }
        using var production = Production();
        Require(production.SelectProduct("opentrail"), "Trail selected");
        var outcome = OfflineBundleInspectionWorkflow.Inspect(production, new FailingReadStream(bundle, failAfterBytes: 64));
        Require(outcome.Status == OfflineBundleInspectionStatus.FileUnavailable && outcome.Heading == "File unavailable" &&
            outcome.StructureStatus == "Not published" && !outcome.Published, "file unavailable reaches no verdict");
    }

    private static void RefusalOutcomes()
    {
        var manifest = Manifest("opentrail", "heltec_v4_bench", Image);
        Check(Bundle(Manifest("opentrail", "heltec_v4_bench", new byte[] { 1, 2, 3, 4 }), Image, Signature),
            FirmwareBundleCandidateRejection.ImageLengthMismatch, OfflineBundleInspectionStatus.ImageIntegrityFailed, "Integrity check failed", "length");
        var altered = (byte[])Image.Clone(); altered[0] ^= 0xff;
        Check(Bundle(manifest, altered, Signature),
            FirmwareBundleCandidateRejection.ImageDigestMismatch, OfflineBundleInspectionStatus.ImageIntegrityFailed, "Integrity check failed", "SHA-256");
        Check(Bundle(manifest, Image, new byte[FirmwareBundleCandidateInspector.SignatureBytes]),
            FirmwareBundleCandidateRejection.SignatureEmpty, OfflineBundleInspectionStatus.SignatureMissing, "Candidate not accepted", "signature");
        Check(Bundle(manifest, Image, new byte[FirmwareBundleCandidateInspector.SignatureBytes - 1]),
            FirmwareBundleCandidateRejection.EntrySize, OfflineBundleInspectionStatus.EntrySizeRejected, "Candidate not accepted", "384 bytes");
        Check(Bundle(manifest, Image, Signature, extraEntry: true),
            FirmwareBundleCandidateRejection.EntrySet, OfflineBundleInspectionStatus.NotCandidateArchive, "Candidate not accepted", "manifest.json");
        Check(new byte[] { 1, 2, 3, 4 },
            FirmwareBundleCandidateRejection.ArchiveUnreadable, OfflineBundleInspectionStatus.NotCandidateArchive, "Candidate not accepted", "readable");
        Check(new byte[FirmwareBundleCandidateInspector.MaximumArchiveBytes + 1],
            FirmwareBundleCandidateRejection.ArchiveSize, OfflineBundleInspectionStatus.SizeLimitExceeded, "Candidate not accepted", "20 MiB");
        Check(Bundle(Encoding.UTF8.GetBytes("{\"schema\":1}"), Image, Signature),
            FirmwareBundleCandidateRejection.ManifestInvalid, OfflineBundleInspectionStatus.ManifestRejected, "Candidate not accepted", "canonical");
        var forged = Bundle(Manifest("opentrail", "heltec_v4_bench", new byte[] { 9 }), new byte[] { 9, 9, 9, 9 }, Signature);
        RewriteCentralDirectory(forged, "image.bin", 24, 1);
        Check(forged, FirmwareBundleCandidateRejection.EntrySizeMismatch, OfflineBundleInspectionStatus.ArchiveDamaged, "Integrity check failed", "recorded-size");

        static void Check(byte[] bundle, FirmwareBundleCandidateRejection reason, OfflineBundleInspectionStatus status, string heading, string phrase)
        {
            ExpectRejection(bundle, reason);
            var outcome = Workflow(bundle);
            Require(outcome.Status == status && outcome.Heading == heading && !outcome.Published &&
                outcome.Summary.Contains(phrase, StringComparison.Ordinal), $"outcome for {reason}: {outcome.Status} {outcome.Summary}");
            Require(outcome.ProductKey == "Not published" && outcome.TargetKey == "Not published" &&
                outcome.ImageSha256 == "Not published" && outcome.ReleaseGeneration == "Not published", "refused fields withheld");
            Require(outcome.AdmissionStatus == "Blocked" && outcome.SignerTrustStatus == "Not configured", "refusal grants nothing");
        }
    }

    private static void BlockedOutcomes()
    {
        var crossProduct = Workflow(Bundle(Manifest("opengauge", "display_reference_target", Image), Image, Signature));
        Require(crossProduct.Status == OfflineBundleInspectionStatus.ProductMismatch && crossProduct.Heading == "Candidate blocked" &&
            crossProduct.Summary.Contains("different Limited Underground system", StringComparison.Ordinal), "product mismatch explained");
        var unknownTarget = Workflow(Bundle(Manifest("opentrail", "unknown_target", Image), Image, Signature));
        Require(unknownTarget.Status == OfflineBundleInspectionStatus.TargetNotSupported && unknownTarget.Heading == "Candidate blocked" &&
            unknownTarget.TargetKey == "Not published", "unsupported target explained without publishing it");

        // A product switch while the file is still being read must not publish into the new session.
        using var controller = Production();
        Require(controller.SelectProduct("opentrail"), "Trail selected");
        var bundle = Bundle(Manifest("opentrail", "heltec_v4_bench", Image), Image, Signature);
        using var switching = new CallbackStream(bundle, () => controller.ReturnToProductChoice());
        var stale = OfflineBundleInspectionWorkflow.Inspect(controller, switching);
        Require(stale.Status == OfflineBundleInspectionStatus.SessionChanged && !stale.Published &&
            stale.Summary.Contains("session changed", StringComparison.Ordinal), "session change explained");
        Require(controller.Snapshot.Revision == 2 && !controller.Snapshot.HasProduct, "switch happened during inspection");
    }

    private static void PublishedDigest()
    {
        var outcome = Workflow(Bundle(Manifest("opentrail", "heltec_v4_bench", Image, generation: 42), Image, Signature, CompressionLevel.Optimal));
        Require(outcome.Published && outcome.Status == OfflineBundleInspectionStatus.Published, "published");
        Require(outcome.ImageSha256 == Convert.ToHexString(SHA256.HashData(Image)).ToLowerInvariant(), "exact lowercase image SHA-256");
        Require(outcome.ReleaseGeneration == "42" && outcome.ImageBytes == "1024", "generation and length");
        Require(outcome.SignatureStatus.Contains("not cryptographically verified", StringComparison.Ordinal) &&
            outcome.AdmissionStatus.StartsWith("Blocked", StringComparison.Ordinal), "digest display grants no trust");
    }

    private static void DamagedCorpus()
    {
        var expected = Convert.ToHexString(SHA256.HashData(Image)).ToLowerInvariant();
        var random = new Random(20261102);
        var (controller, context) = Session("opentrail");
        using var production = Production();
        Require(production.SelectProduct("opentrail"), "Trail selected");
        int cases = 0, published = 0;
        using (controller)
        {
            foreach (var level in new[] { CompressionLevel.NoCompression, CompressionLevel.Optimal })
            {
                var good = Bundle(Manifest("opentrail", "heltec_v4_bench", Image), Image, Signature, level);
                for (int length = 0; length < good.Length; length++) One(good[..length]);
                for (int i = 0; i < 2500; i++)
                {
                    var damaged = (byte[])good.Clone();
                    for (int flips = 1 + random.Next(4); flips > 0; flips--) damaged[random.Next(damaged.Length)] = (byte)random.Next(256);
                    One(damaged);
                }
            }
        }
        // Every truncation of both archives plus 5000 seeded corruptions; damage to unprotected
        // ZIP metadata (for example timestamps) may still publish, but never altered entry bytes.
        Require(cases > 5000 && published < cases / 10, $"corpus exercised refusals ({cases} cases, {published} published)");

        void One(byte[] data)
        {
            cases++;
            using var stream = new MemoryStream(data) { Position = data.Length / 2 };
            try { FirmwareBundleCandidateInspector.Inspect(stream, context); }
            catch (InvalidDataException) { }
            Require(stream.Position == data.Length / 2, "caller position restored for damaged input");
            stream.Position = 0;
            if (FirmwareBundleCandidateInspector.TryInspect(stream, context, out var result, out var reason))
                Require(reason == FirmwareBundleCandidateRejection.None && result is not null, "success has no reason");
            else
                Require(reason != FirmwareBundleCandidateRejection.None && result is null, "refusal has a reason");
            var outcome = OfflineBundleInspectionWorkflow.Inspect(production, new MemoryStream(data));
            Require(Enum.IsDefined(outcome.Status), "defined outcome");
            Require(outcome.Status != OfflineBundleInspectionStatus.FileUnavailable, "in-memory damage is never reported as file I/O");
            if (outcome.Published)
            {
                published++;
                Require(outcome.ImageSha256 == expected, "a published result never carries altered image bytes");
            }
        }
    }

    private static OfflineBundleInspectionOutcome Workflow(byte[] bundle)
    {
        using var controller = Production();
        Require(controller.SelectProduct("opentrail"), "Trail selected");
        return OfflineBundleInspectionWorkflow.Inspect(controller, new MemoryStream(bundle));
    }

    private static void ExpectRejection(byte[] bundle, FirmwareBundleCandidateRejection reason)
    {
        var (controller, context) = Session("opentrail");
        using (controller)
        {
            using var stream = new MemoryStream(bundle) { Position = 1 };
            Require(!FirmwareBundleCandidateInspector.TryInspect(stream, context, out var result, out var actual) &&
                result is null && actual == reason, $"expected {reason}, got {actual}");
            Require(stream.Position == 1, "position restored after typed refusal");
            bool invalid = false;
            try { FirmwareBundleCandidateInspector.Inspect(stream, context); }
            catch (InvalidDataException) { invalid = true; }
            Require(invalid, "throwing contract stays InvalidDataException");
        }
    }

    private static (LoaderSessionController Controller, LoaderBundleInspectionContext Context) Session(string product)
    {
        var controller = Production();
        Require(controller.SelectProduct(product), "select " + product);
        Require(controller.TryCreateOfflineBundleInspectionContext(out var context) && context is not null, "context");
        return (controller, context!);
    }

    private static LoaderSessionController Production() =>
        new(LoaderProductionProviders.Factories, LoaderProductionProviders.SignerTrustPolicies);

    private static byte[] Manifest(string product, string target, byte[] image, ulong generation = 1) =>
        FirmwareBundleCandidateInspector.SerializeCanonicalManifest(product, target, generation, (uint)image.Length,
            Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant(), "0123456789abcdef");

    private static byte[] Bundle(byte[] manifest, byte[] image, byte[] signature,
        CompressionLevel level = CompressionLevel.NoCompression, bool extraEntry = false)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Put("manifest.json", manifest); Put("image.bin", image); Put("manifest.sig", signature);
            if (extraEntry) Put("notes.txt", new byte[] { 1 });
            void Put(string name, byte[] bytes) { using var entry = archive.CreateEntry(name, level).Open(); entry.Write(bytes); }
        }
        return output.ToArray();
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle)) return i;
        throw new InvalidOperationException("fixture bytes not stored verbatim");
    }

    // Rewrites one little-endian 32-bit field of the named central-directory record.
    private static void RewriteCentralDirectory(byte[] bytes, string entryName, int fieldOffset, uint value)
    {
        var name = Encoding.UTF8.GetBytes(entryName); int matches = 0;
        for (int offset = 0; offset <= bytes.Length - 46; offset++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)) != 0x02014b50) continue;
            int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 28, 2));
            if (offset + 46 + nameLength <= bytes.Length && bytes.AsSpan(offset + 46, nameLength).SequenceEqual(name))
            { BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + fieldOffset, 4), value); matches++; }
        }
        Require(matches == 1, "central-directory record located");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + message);
    }

    // Simulates a removable drive or network share failing mid-read.
    private sealed class FailingReadStream(byte[] bytes, int failAfterBytes) : MemoryStream(bytes, writable: false)
    {
        private int delivered;
        // A derived MemoryStream routes span reads through this overload.
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (delivered >= failAfterBytes) throw new IOException("simulated device removal");
            int read = base.Read(buffer, offset, Math.Min(count, failAfterBytes - delivered));
            delivered += read;
            return read;
        }
    }

    // Runs one callback on the first read, after the inspection context was minted.
    private sealed class CallbackStream(byte[] bytes, Action callback) : MemoryStream(bytes, writable: false)
    {
        private Action? pending = callback;
        public override int Read(byte[] buffer, int offset, int count)
        {
            Interlocked.Exchange(ref pending, null)?.Invoke();
            return base.Read(buffer, offset, count);
        }
    }
}
