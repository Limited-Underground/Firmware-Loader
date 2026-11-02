using System.Globalization;
using System.IO;

namespace LimitedUnderground.FirmwareLoader;

// Fixed outcome codes. None of them carries file, path, device or exception data.
public enum OfflineBundleInspectionStatus
{
    Published = 1,
    ProviderUnavailable,
    FileUnavailable,
    NotCandidateArchive,
    SizeLimitExceeded,
    EntrySizeRejected,
    ArchiveDamaged,
    ManifestRejected,
    ImageIntegrityFailed,
    SignatureMissing,
    ProductMismatch,
    TargetNotSupported,
    SessionChanged,
}

public sealed record OfflineBundleInspectionOutcome(
    bool Published,
    string Heading,
    string Summary,
    string ProductKey,
    string TargetKey,
    string ImageBytes,
    string StructureStatus,
    string ImageDigestStatus,
    string SignatureStatus,
    string SignerTrustStatus,
    string AdmissionStatus,
    OfflineBundleInspectionStatus Status,
    string ReleaseGeneration,
    string ImageSha256);

public static class OfflineBundleInspectionWorkflow
{
    private const string NotPublished = "Not published";
    private const string NotAccepted = "Not accepted";

    public static OfflineBundleInspectionOutcome Inspect(
        LoaderSessionController controller,
        Stream candidate)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(candidate);

        if (!controller.TryCreateOfflineBundleInspectionContext(out var context) ||
            context is null)
        {
            return Unavailable();
        }

        FirmwareBundleCandidateResult? result;
        try
        {
            if (!FirmwareBundleCandidateInspector.TryInspect(
                    candidate,
                    context,
                    out result,
                    out var rejection))
            {
                return Rejected(rejection);
            }
        }
        catch (InvalidDataException)
        {
            return NotCandidateArchive();
        }
        catch (IOException)
        {
            return FileUnavailable();
        }
        catch (UnauthorizedAccessException)
        {
            return FileUnavailable();
        }

        if (!controller.CanPublishOfflineBundleInspection(context, result))
        {
            return Blocked(controller.ClassifyOfflineBundleInspectionBlock(context, result));
        }

        return new OfflineBundleInspectionOutcome(
            Published: true,
            Heading: "Candidate inspected",
            Summary: "Structure, archive CRC-32, product binding, target rule, and image SHA-256 passed offline inspection.",
            ProductKey: result.ProductKey,
            TargetKey: result.TargetKey,
            ImageBytes: result.ImageBytes.ToString(CultureInfo.InvariantCulture),
            StructureStatus: "Verified",
            ImageDigestStatus: "Verified",
            SignatureStatus: "Present — not cryptographically verified",
            SignerTrustStatus: "Not configured",
            AdmissionStatus: "Blocked — inspection does not authorize installation",
            Status: OfflineBundleInspectionStatus.Published,
            ReleaseGeneration: result.ReleaseGeneration.ToString(CultureInfo.InvariantCulture),
            ImageSha256: result.ImageSha256.ToLowerInvariant());
    }

    // The selected file could not be opened or read; no verdict about its bytes exists.
    public static OfflineBundleInspectionOutcome FileUnavailable() =>
        Empty(
            OfflineBundleInspectionStatus.FileUnavailable,
            "File unavailable",
            "The selected file could not be opened or read completely for read-only inspection. No verdict about its contents was reached.",
            NotPublished);

    private static OfflineBundleInspectionOutcome Unavailable() =>
        Empty(
            OfflineBundleInspectionStatus.ProviderUnavailable,
            "Inspection unavailable",
            "The selected product does not have an active inspection provider.");

    private static OfflineBundleInspectionOutcome NotCandidateArchive() =>
        Empty(
            OfflineBundleInspectionStatus.NotCandidateArchive,
            "Candidate not accepted",
            "The selected file is not a readable firmware candidate archive. A candidate contains exactly manifest.json, image.bin, and manifest.sig.");

    private static OfflineBundleInspectionOutcome Rejected(FirmwareBundleCandidateRejection reason) =>
        reason switch
        {
            FirmwareBundleCandidateRejection.ArchiveSize => Empty(
                OfflineBundleInspectionStatus.SizeLimitExceeded,
                "Candidate not accepted",
                "The selected file is empty or larger than the 20 MiB candidate limit."),
            FirmwareBundleCandidateRejection.EntrySize => Empty(
                OfflineBundleInspectionStatus.EntrySizeRejected,
                "Candidate not accepted",
                "An archive entry is empty or outside its limit: manifest up to 4 KiB, image up to 16 MiB, signature exactly 384 bytes."),
            FirmwareBundleCandidateRejection.EntrySizeMismatch or
            FirmwareBundleCandidateRejection.EntryChecksumMismatch => Empty(
                OfflineBundleInspectionStatus.ArchiveDamaged,
                "Integrity check failed",
                "The archive is damaged or inconsistent: an entry failed its CRC-32 or recorded-size check. Obtain a fresh copy; do not use this file."),
            FirmwareBundleCandidateRejection.ManifestInvalid => Empty(
                OfflineBundleInspectionStatus.ManifestRejected,
                "Candidate not accepted",
                "The candidate manifest is not accepted. It must be canonical UTF-8 JSON with the supported schema, a known product, a valid target, a nonzero release generation, and the image SHA-256."),
            FirmwareBundleCandidateRejection.ImageLengthMismatch => Empty(
                OfflineBundleInspectionStatus.ImageIntegrityFailed,
                "Integrity check failed",
                "The firmware image length does not match its manifest. The file is corrupted or has been altered; do not use it."),
            FirmwareBundleCandidateRejection.ImageDigestMismatch => Empty(
                OfflineBundleInspectionStatus.ImageIntegrityFailed,
                "Integrity check failed",
                "The firmware image does not match the SHA-256 recorded in its manifest. The file is corrupted or has been altered; do not use it."),
            FirmwareBundleCandidateRejection.SignatureEmpty => Empty(
                OfflineBundleInspectionStatus.SignatureMissing,
                "Candidate not accepted",
                "The candidate signature field is empty."),
            _ => NotCandidateArchive(),
        };

    private static OfflineBundleInspectionOutcome Blocked(LoaderInspectionBlock block) =>
        block switch
        {
            LoaderInspectionBlock.ProductMismatch => Empty(
                OfflineBundleInspectionStatus.ProductMismatch,
                "Candidate blocked",
                "The candidate is built for a different Limited Underground system than the one selected."),
            LoaderInspectionBlock.TargetNotSupported => Empty(
                OfflineBundleInspectionStatus.TargetNotSupported,
                "Candidate blocked",
                "The candidate's target is not in the selected system's accepted target rules."),
            LoaderInspectionBlock.SessionChanged => Empty(
                OfflineBundleInspectionStatus.SessionChanged,
                "Candidate blocked",
                "The product session changed while the file was inspected. Choose the file again."),
            _ => Empty(
                OfflineBundleInspectionStatus.SessionChanged,
                "Candidate blocked",
                "The candidate does not match the current product session and target rules."),
        };

    private static OfflineBundleInspectionOutcome Empty(
        OfflineBundleInspectionStatus status,
        string heading,
        string summary,
        string verdict = NotAccepted) =>
        new(
            Published: false,
            Heading: heading,
            Summary: summary,
            ProductKey: NotPublished,
            TargetKey: NotPublished,
            ImageBytes: NotPublished,
            StructureStatus: verdict,
            ImageDigestStatus: verdict,
            SignatureStatus: verdict,
            SignerTrustStatus: "Not configured",
            AdmissionStatus: "Blocked",
            Status: status,
            ReleaseGeneration: NotPublished,
            ImageSha256: NotPublished);
}
