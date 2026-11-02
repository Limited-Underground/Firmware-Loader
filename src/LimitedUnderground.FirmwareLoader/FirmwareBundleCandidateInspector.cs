using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Unicode;

namespace LimitedUnderground.FirmwareLoader;

public sealed class FirmwareBundleCandidateResult
{
    internal FirmwareBundleCandidateResult(
        bool structureVerified,
        bool imageDigestVerified,
        bool productMatched,
        bool signaturePresent,
        bool signerTrusted,
        bool admissionAllowed,
        LoaderBundleInspectionContext context,
        string productKey,
        string targetKey,
        ulong releaseGeneration,
        uint imageBytes,
        string summary,
        string blockerText,
        string imageSha256 = "")
    {
        StructureVerified = structureVerified;
        ImageDigestVerified = imageDigestVerified;
        ProductMatched = productMatched;
        SignaturePresent = signaturePresent;
        SignerTrusted = signerTrusted;
        AdmissionAllowed = admissionAllowed;
        Context = context;
        SessionRevision = context.SessionRevision;
        ProductKey = productKey;
        TargetKey = targetKey;
        ReleaseGeneration = releaseGeneration;
        ImageBytes = imageBytes;
        ImageSha256 = imageSha256;
        Summary = summary;
        BlockerText = blockerText;
    }

    public bool StructureVerified { get; }

    public bool ImageDigestVerified { get; }

    public bool ProductMatched { get; }

    public bool SignaturePresent { get; }

    public bool SignerTrusted { get; }

    public bool AdmissionAllowed { get; }

    public ulong SessionRevision { get; }

    public string ProductKey { get; }

    public string TargetKey { get; }

    public ulong ReleaseGeneration { get; }

    public uint ImageBytes { get; }

    public string Summary { get; }

    public string BlockerText { get; }

    internal string ImageSha256 { get; }

    internal LoaderBundleInspectionContext Context { get; }
}

// Fixed, data-free reason codes for a refused candidate. A reason explains a refusal;
// it never grants inspection, trust, admission or device authority.
public enum FirmwareBundleCandidateRejection
{
    None = 0,
    InputNotAccepted,
    ArchiveSize,
    ArchiveUnreadable,
    EntrySet,
    EntrySize,
    EntrySizeMismatch,
    EntryChecksumMismatch,
    ManifestInvalid,
    ImageLengthMismatch,
    ImageDigestMismatch,
    SignatureEmpty,
}

public static class FirmwareBundleCandidateInspector
{
    public const string Schema = "firmware_bundle_candidate_v1";
    public const string SignatureAlgorithm = "rsa_pss_3072_sha256";
    public const int MaximumArchiveBytes = 20 * 1024 * 1024;
    public const int MaximumManifestBytes = 4096;
    public const int MaximumImageBytes = 16 * 1024 * 1024;
    public const int SignatureBytes = 384;

    private const string ManifestEntryName = "manifest.json";
    private const string ImageEntryName = "image.bin";
    private const string SignatureEntryName = "manifest.sig";

    private static readonly string[] CanonicalPropertyNames =
    [
        "schema",
        "product_key",
        "target_key",
        "release_generation",
        "image_bytes",
        "image_sha256",
        "signer_id",
        "signature_algorithm",
    ];

    // Existing contract: every refusal of the candidate's bytes is an InvalidDataException
    // with a fixed message. A failure to read the caller's stream is an IOException.
    public static FirmwareBundleCandidateResult Inspect(
        Stream input,
        LoaderBundleInspectionContext context)
    {
        try
        {
            return InspectCore(input, context);
        }
        catch (CandidateRejectionException rejection)
        {
            throw new InvalidDataException(rejection.Message, rejection.InnerException);
        }
    }

    // Same checks as Inspect, but a refusal of the candidate's bytes returns a fixed reason
    // code instead of throwing. Read failures of the caller's stream still throw IOException.
    public static bool TryInspect(
        Stream input,
        LoaderBundleInspectionContext context,
        [NotNullWhen(true)] out FirmwareBundleCandidateResult? result,
        out FirmwareBundleCandidateRejection rejection)
    {
        try
        {
            result = InspectCore(input, context);
            rejection = FirmwareBundleCandidateRejection.None;
            return true;
        }
        catch (CandidateRejectionException refused)
        {
            result = null;
            rejection = refused.Reason;
            return false;
        }
    }

    private static FirmwareBundleCandidateResult InspectCore(
        Stream input,
        LoaderBundleInspectionContext context)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        if (!input.CanRead || !input.CanSeek)
        {
            throw Rejected(
                FirmwareBundleCandidateRejection.InputNotAccepted,
                "Candidate bundle input must be readable and seekable.");
        }

        var originalPosition = input.Position;
        try
        {
            input.Position = 0;
            using var source = new CandidateSourceStream(input);
            try
            {
                return InspectAtStart(source, context);
            }
            catch (CandidateRejectionException)
            {
                throw;
            }
            catch (CandidateSourceReadException exception)
            {
                // A genuine read failure of the caller's stream is not a verdict on the
                // candidate bytes. Report it as I/O so the caller can say so accurately.
                throw new IOException("Candidate bundle could not be read.", exception.InnerException);
            }
            catch (Exception exception) when (IsUntrustedFormatFailure(exception))
            {
                // Archive-library failures caused by malformed bytes fail closed as one
                // typed refusal instead of escaping as unrelated runtime exceptions.
                throw Rejected(
                    FirmwareBundleCandidateRejection.ArchiveUnreadable,
                    "Candidate bundle is not a readable archive.",
                    exception);
            }
        }
        finally
        {
            input.Position = originalPosition;
        }
    }

    private static bool IsUntrustedFormatFailure(Exception exception) =>
        exception is InvalidDataException or IOException or InvalidOperationException or
            NotSupportedException or ArgumentException or FormatException or
            OverflowException or JsonException;

    private static CandidateRejectionException Rejected(
        FirmwareBundleCandidateRejection reason,
        string message,
        Exception? innerException = null) =>
        new(reason, message, innerException);

    private static FirmwareBundleCandidateResult InspectAtStart(
        Stream input,
        LoaderBundleInspectionContext context)
    {
        if (input.Length <= 0 || input.Length > MaximumArchiveBytes)
        {
            throw Rejected(
                FirmwareBundleCandidateRejection.ArchiveSize,
                "Candidate bundle size is outside the inspection limit.");
        }
        if (LoaderProductCatalog.Find(context.ProductKey) is null ||
            context.SessionRevision == 0)
        {
            throw Rejected(
                FirmwareBundleCandidateRejection.InputNotAccepted,
                "Selected product context is not accepted.");
        }

        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count != 3)
        {
            throw Rejected(
                FirmwareBundleCandidateRejection.EntrySet,
                "Candidate bundle must contain exactly three entries.");
        }

        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            if (!entries.TryAdd(entry.FullName, entry) ||
                entry.FullName is not (ManifestEntryName or ImageEntryName or SignatureEntryName))
            {
                throw Rejected(
                    FirmwareBundleCandidateRejection.EntrySet,
                    "Candidate bundle entry set is not accepted.");
            }
        }

        var manifestBytes = ReadBoundedEntry(
            entries[ManifestEntryName],
            minimumBytes: 2,
            maximumBytes: MaximumManifestBytes,
            label: "manifest");
        var manifest = ParseCanonicalManifest(manifestBytes);

        var imageBytes = ReadBoundedEntry(
            entries[ImageEntryName],
            minimumBytes: 1,
            maximumBytes: MaximumImageBytes,
            label: "image");
        if (imageBytes.Length != manifest.ImageBytes)
        {
            throw Rejected(
                FirmwareBundleCandidateRejection.ImageLengthMismatch,
                "Candidate image length does not match its manifest.");
        }

        var imageDigest = SHA256.HashData(imageBytes);
        if (!CryptographicOperations.FixedTimeEquals(imageDigest, manifest.ImageSha256))
        {
            throw Rejected(
                FirmwareBundleCandidateRejection.ImageDigestMismatch,
                "Candidate image digest does not match its manifest.");
        }

        var signature = ReadBoundedEntry(
            entries[SignatureEntryName],
            minimumBytes: SignatureBytes,
            maximumBytes: SignatureBytes,
            label: "signature");
        if (signature.All(static value => value == 0))
        {
            throw Rejected(
                FirmwareBundleCandidateRejection.SignatureEmpty,
                "Candidate signature is empty.");
        }

        var productMatched = string.Equals(
            manifest.ProductKey,
            context.ProductKey,
            StringComparison.Ordinal);

        return new FirmwareBundleCandidateResult(
            structureVerified: true,
            imageDigestVerified: true,
            productMatched: productMatched,
            signaturePresent: true,
            signerTrusted: false,
            admissionAllowed: false,
            context: context,
            productKey: manifest.ProductKey,
            targetKey: manifest.TargetKey,
            releaseGeneration: manifest.ReleaseGeneration,
            imageBytes: manifest.ImageBytes,
            imageSha256: Convert.ToHexString(imageDigest),
            summary: productMatched
                ? "Candidate structure, product binding, and image SHA-256 verified"
                : "Candidate belongs to a different Limited Underground system",
            blockerText: productMatched
                ? "BLOCKED: No trusted release signer or target provider is configured."
                : "BLOCKED: Candidate product does not match the selected system.");
    }

    public static byte[] SerializeCanonicalManifest(
        string productKey,
        string targetKey,
        ulong releaseGeneration,
        uint imageBytes,
        string imageSha256,
        string signerId)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
            output,
            new JsonWriterOptions { Indented = false, SkipValidation = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", Schema);
            writer.WriteString("product_key", productKey);
            writer.WriteString("target_key", targetKey);
            writer.WriteNumber("release_generation", releaseGeneration);
            writer.WriteNumber("image_bytes", imageBytes);
            writer.WriteString("image_sha256", imageSha256);
            writer.WriteString("signer_id", signerId);
            writer.WriteString("signature_algorithm", SignatureAlgorithm);
            writer.WriteEndObject();
        }
        return output.ToArray();
    }

    private static CandidateManifest ParseCanonicalManifest(byte[] bytes)
    {
        // JsonDocument defers some string transcoding; reject invalid UTF-8 up front.
        if (!Utf8.IsValid(bytes))
        {
            throw Rejected(
                FirmwareBundleCandidateRejection.ManifestInvalid,
                "Candidate manifest is not valid UTF-8.");
        }

        try
        {
            return ParseValidUtf8Manifest(bytes);
        }
        catch (CandidateRejectionException)
        {
            throw;
        }
        catch (Exception exception) when (IsUntrustedFormatFailure(exception) ||
            exception is KeyNotFoundException)
        {
            throw Rejected(
                FirmwareBundleCandidateRejection.ManifestInvalid,
                "Candidate manifest is not accepted.",
                exception);
        }
    }

    private static CandidateManifest ParseValidUtf8Manifest(byte[] bytes)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                bytes,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 3,
                });
        }
        catch (JsonException exception)
        {
            throw Rejected(
                FirmwareBundleCandidateRejection.ManifestInvalid,
                "Candidate manifest is not valid JSON.",
                exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.EnumerateObject().Select(static property => property.Name)
                    .SequenceEqual(CanonicalPropertyNames, StringComparer.Ordinal))
            {
                throw Rejected(
                    FirmwareBundleCandidateRejection.ManifestInvalid,
                    "Candidate manifest is not canonical.");
            }

            var schema = RequiredString(root, "schema", 64);
            var productKey = RequiredString(root, "product_key", 24);
            var targetKey = RequiredString(root, "target_key", 64);
            var releaseGeneration = RequiredUInt64(root, "release_generation");
            var imageBytes = RequiredUInt32(root, "image_bytes");
            var imageSha256Text = RequiredString(root, "image_sha256", 64);
            var signerId = RequiredString(root, "signer_id", 16);
            var signatureAlgorithm = RequiredString(root, "signature_algorithm", 32);

            if (schema != Schema ||
                LoaderProductCatalog.Find(productKey) is null ||
                !IsEngineeringKey(targetKey, 64) ||
                releaseGeneration == 0 ||
                imageBytes == 0 ||
                imageBytes > MaximumImageBytes ||
                !IsLowerHex(imageSha256Text, 64) ||
                !IsLowerHex(signerId, 16) ||
                signerId.All(static value => value == '0') ||
                signatureAlgorithm != SignatureAlgorithm)
            {
                throw Rejected(
                    FirmwareBundleCandidateRejection.ManifestInvalid,
                    "Candidate manifest fields are not accepted.");
            }

            var canonical = SerializeCanonicalManifest(
                productKey,
                targetKey,
                releaseGeneration,
                imageBytes,
                imageSha256Text,
                signerId);
            if (!bytes.AsSpan().SequenceEqual(canonical))
            {
                throw Rejected(
                    FirmwareBundleCandidateRejection.ManifestInvalid,
                    "Candidate manifest is not canonically encoded.");
            }

            return new CandidateManifest(
                productKey,
                targetKey,
                releaseGeneration,
                imageBytes,
                Convert.FromHexString(imageSha256Text));
        }
    }

    private static byte[] ReadBoundedEntry(
        ZipArchiveEntry entry,
        int minimumBytes,
        int maximumBytes,
        string label)
    {
        if (entry.Length < minimumBytes || entry.Length > maximumBytes)
        {
            throw Rejected(
                FirmwareBundleCandidateRejection.EntrySize,
                $"Candidate {label} size is outside its inspection limit.");
        }

        using var input = entry.Open();
        using var output = new MemoryStream((int)Math.Min(entry.Length, maximumBytes));
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var remaining = maximumBytes + 1L - output.Length;
            if (remaining <= 0)
            {
                throw Rejected(
                    FirmwareBundleCandidateRejection.EntrySizeMismatch,
                    $"Candidate {label} expands beyond its inspection limit.");
            }

            var read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read == 0)
            {
                break;
            }
            output.Write(buffer, 0, read);
        }

        if (output.Length < minimumBytes ||
            output.Length > maximumBytes ||
            output.Length != entry.Length)
        {
            throw Rejected(
                FirmwareBundleCandidateRejection.EntrySizeMismatch,
                $"Candidate {label} expanded size is not accepted.");
        }

        // .NET does not verify ZIP CRC-32 values on read. Check them so accidental
        // corruption of any entry, including signature bytes that are otherwise only
        // presence-checked, is refused as archive damage.
        var bytes = output.ToArray();
        if (ComputeCrc32(bytes) != entry.Crc32)
        {
            throw Rejected(
                FirmwareBundleCandidateRejection.EntryChecksumMismatch,
                $"Candidate {label} failed its archive CRC-32 check.");
        }
        return bytes;
    }

    private static readonly uint[] Crc32Table = CreateCrc32Table();

    private static uint[] CreateCrc32Table()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
            }
            table[index] = value;
        }
        return table;
    }

    internal static uint ComputeCrc32(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc = Crc32Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }
        return ~crc;
    }

    private static string RequiredString(JsonElement root, string name, int maximumLength)
    {
        var element = root.GetProperty(name);
        if (element.ValueKind != JsonValueKind.String)
        {
            throw Rejected(
                FirmwareBundleCandidateRejection.ManifestInvalid,
                "Candidate manifest field type is not accepted.");
        }

        var value = element.GetString() ?? string.Empty;
        if (value.Length == 0 || value.Length > maximumLength)
        {
            throw Rejected(
                FirmwareBundleCandidateRejection.ManifestInvalid,
                "Candidate manifest text is outside its limit.");
        }
        return value;
    }

    private static uint RequiredUInt32(JsonElement root, string name)
    {
        var element = root.GetProperty(name);
        if (!element.TryGetUInt32(out var value))
        {
            throw Rejected(
                FirmwareBundleCandidateRejection.ManifestInvalid,
                "Candidate manifest integer is outside its limit.");
        }
        return value;
    }

    private static ulong RequiredUInt64(JsonElement root, string name)
    {
        var element = root.GetProperty(name);
        if (!element.TryGetUInt64(out var value))
        {
            throw Rejected(
                FirmwareBundleCandidateRejection.ManifestInvalid,
                "Candidate manifest integer is outside its limit.");
        }
        return value;
    }

    private static bool IsLowerHex(string value, int length) =>
        value.Length == length && value.All(static character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsEngineeringKey(string value, int maximumLength) =>
        value.Length <= maximumLength &&
        value[0] is >= 'a' and <= 'z' &&
        value.All(static character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');

    // Internal carrier only; public callers see InvalidDataException or a reason code.
    private sealed class CandidateRejectionException(
        FirmwareBundleCandidateRejection reason,
        string message,
        Exception? innerException)
        : Exception(message, innerException)
    {
        internal FirmwareBundleCandidateRejection Reason { get; } = reason;
    }

    private sealed class CandidateSourceReadException(Exception innerException)
        : Exception("Candidate source read failed.", innerException);

    // Distinguishes failures of the caller's stream (I/O) from failures caused by the
    // candidate's own bytes. Seeks are archive-directed and stay format failures.
    private sealed class CandidateSourceStream(Stream inner) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                try { return inner.Length; }
                catch (Exception exception) when (IsSourceFailure(exception))
                { throw new CandidateSourceReadException(exception); }
            }
        }

        public override long Position
        {
            get
            {
                try { return inner.Position; }
                catch (Exception exception) when (IsSourceFailure(exception))
                { throw new CandidateSourceReadException(exception); }
            }
            set => inner.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            try { return inner.Read(buffer, offset, count); }
            catch (Exception exception) when (IsSourceFailure(exception))
            { throw new CandidateSourceReadException(exception); }
        }

        public override int Read(Span<byte> buffer)
        {
            try { return inner.Read(buffer); }
            catch (Exception exception) when (IsSourceFailure(exception))
            { throw new CandidateSourceReadException(exception); }
        }

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void Flush()
        {
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        private static bool IsSourceFailure(Exception exception) =>
            exception is IOException or UnauthorizedAccessException or ObjectDisposedException;
    }

    private sealed record CandidateManifest(
        string ProductKey,
        string TargetKey,
        ulong ReleaseGeneration,
        uint ImageBytes,
        byte[] ImageSha256);
}
