using System.Security.Cryptography;

namespace LimitedUnderground.FirmwareLoader;

internal enum HostPreservationBindingPreflightStatus { Refused, FixtureConsistent }
internal enum HostPreservationBindingPreflightReason { Unspecified, None, Input, SingleImage, Policy, Payload, Geometry }

// Value data only. Even a positive result is not admission or operational authority.
internal readonly struct HostPreservationBindingPreflightResult
{
    internal HostPreservationBindingPreflightStatus Status { get; }
    internal HostPreservationBindingPreflightReason Reason { get; }

    private HostPreservationBindingPreflightResult(HostPreservationBindingPreflightStatus status,
        HostPreservationBindingPreflightReason reason)
    { Status = status; Reason = reason; }

    internal static HostPreservationBindingPreflightResult Refuse(HostPreservationBindingPreflightReason reason) =>
        new(HostPreservationBindingPreflightStatus.Refused, reason);
    internal static HostPreservationBindingPreflightResult Consistent() =>
        new(HostPreservationBindingPreflightStatus.FixtureConsistent, HostPreservationBindingPreflightReason.None);
}

// Pure single-image host-fixture comparison. No signature re-verification, I/O, or engine integration.
internal static class HostPreservationBindingPreflight
{
    internal static HostPreservationBindingPreflightResult Evaluate(HostAdmittedRelease? release,
        HostPreservationPayloadBinding? binding, HostAdmissionPolicy? currentPolicy, long now)
    {
        if (release is null || binding is null || currentPolicy is null || now < 0 ||
            release.Manifest is not { } manifest || release.Policy is not { } retainedPolicy)
            return Refuse(HostPreservationBindingPreflightReason.Input);

        var plan = binding.Plan;
        if (plan.Writes.Count != 1 || plan.EraseRanges.Count != 1 || binding.Payloads.Count != 1)
            return Refuse(HostPreservationBindingPreflightReason.SingleImage);

        if (currentPolicy != retainedPolicy || currentPolicy.Revoked || currentPolicy.Revision == 0 ||
            now >= currentPolicy.ValidUntil || manifest.PolicyRevision != currentPolicy.Revision ||
            manifest.Schema != 1 || manifest.Product != "opentrail" || manifest.Target != "heltec_v4_bench" ||
            !Hash(manifest.TargetDigest) || !Hash(manifest.LayoutDigest) || !Hash(manifest.RecoveryDigest) ||
            !Hash(currentPolicy.PublicKeyDigest) || string.IsNullOrWhiteSpace(currentPolicy.Signer) ||
            manifest.TargetDigest != currentPolicy.TargetDigest || manifest.LayoutDigest != currentPolicy.LayoutDigest ||
            manifest.Compatibility != currentPolicy.Compatibility || manifest.RecoveryDigest != currentPolicy.RecoveryDigest ||
            manifest.Signer != currentPolicy.Signer || manifest.Generation == 0 ||
            manifest.Generation < currentPolicy.MinimumGeneration)
            return Refuse(HostPreservationBindingPreflightReason.Policy);

        var write = plan.Writes[0];
        var payloadInfo = binding.Payloads[0];
        if (manifest.ImageLength <= 0 || manifest.ImageLength > HostPreservationPayloadBinding.MaximumPayloadBytes ||
            write.Offset != manifest.Offset || payloadInfo.Offset != write.Offset ||
            write.Length != (ulong)manifest.ImageLength || payloadInfo.Length != manifest.ImageLength)
            return Refuse(HostPreservationBindingPreflightReason.Payload);

        // Copy each retained snapshot once; asserted metadata cannot replace exact retained bytes.
        var image = release.CopyImage();
        var payload = binding.CopyBytesAt(0);
        if (image.Length != manifest.ImageLength || payload.Length != manifest.ImageLength ||
            !image.AsSpan().SequenceEqual(payload))
            return Refuse(HostPreservationBindingPreflightReason.Payload);
        try
        {
            string imageDigest = Convert.ToHexString(SHA256.HashData(image));
            string payloadDigest = Convert.ToHexString(SHA256.HashData(payload));
            if (!string.Equals(imageDigest, manifest.ImageDigest, StringComparison.Ordinal) ||
                !string.Equals(payloadDigest, manifest.ImageDigest, StringComparison.Ordinal) ||
                !string.Equals(payloadDigest, payloadInfo.Sha256, StringComparison.Ordinal))
                return Refuse(HostPreservationBindingPreflightReason.Payload);
        }
        catch (CryptographicException)
        { return Refuse(HostPreservationBindingPreflightReason.Payload); }

        return GeometryMatches(plan, currentPolicy, write)
            ? HostPreservationBindingPreflightResult.Consistent()
            : Refuse(HostPreservationBindingPreflightReason.Geometry);
    }

    private static bool GeometryMatches(HostPreservationPlan plan, HostAdmissionPolicy policy,
        HostPreservationRange write)
    {
        try
        {
            ulong size = plan.AddressSpaceLength, block = plan.EraseBlockSize;
            if (size == 0 || block == 0 || (block & (block - 1)) != 0 || size % block != 0 ||
                block != policy.EraseBlock || policy.AllowedLength == 0 || policy.ProtectedLength == 0)
                return false;

            ulong allowedStart = policy.AllowedOffset, protectedStart = policy.ProtectedOffset;
            ulong allowedEnd = checked(allowedStart + policy.AllowedLength);
            ulong protectedEnd = checked(protectedStart + policy.ProtectedLength);
            if (allowedEnd > size || protectedEnd > size ||
                Overlaps(allowedStart, allowedEnd, protectedStart, protectedEnd)) return false;

            // The immutable factory already proves complete, non-overlapping classification of [0,size).
            // This boundary additionally forbids a plan from enlarging the fixture policy's allowance.
            foreach (var range in plan.AllowedRanges)
            {
                ulong end = checked(range.Offset + range.Length);
                if (range.Offset < allowedStart || end > allowedEnd ||
                    Overlaps(range.Offset, end, protectedStart, protectedEnd)) return false;
            }
            if (!Covers(plan.ProtectedRanges, protectedStart, protectedEnd)) return false;

            if (write.Offset % block != 0) return false; // Retained fixture admission is stricter than the math factory.
            ulong writeEnd = checked(write.Offset + write.Length);
            if (writeEnd > size || writeEnd > uint.MaxValue) return false;
            ulong remainder = writeEnd % block;
            ulong padding = remainder == 0 ? 0 : block - remainder;
            if (padding > size - writeEnd) return false;
            ulong eraseEnd = checked(writeEnd + padding);
            var erase = plan.EraseRanges[0];
            if (eraseEnd > uint.MaxValue || erase.Offset != write.Offset ||
                erase.Length != checked(eraseEnd - write.Offset) || write.Offset < allowedStart ||
                eraseEnd > allowedEnd || Overlaps(write.Offset, eraseEnd, protectedStart, protectedEnd) ||
                !Covers(plan.AllowedRanges, write.Offset, eraseEnd)) return false;
            foreach (var range in plan.ProtectedRanges)
                if (Overlaps(write.Offset, eraseEnd, range.Offset, checked(range.Offset + range.Length))) return false;
            return true;
        }
        catch (OverflowException) { return false; }
    }

    // Sorted immutable ranges may jointly cover an interval across adjacent boundaries.
    private static bool Covers(IReadOnlyList<HostPreservationRange> ranges, ulong start, ulong end)
    {
        ulong cursor = start;
        foreach (var range in ranges)
        {
            ulong rangeEnd = checked(range.Offset + range.Length);
            if (rangeEnd <= cursor) continue;
            if (range.Offset > cursor) return false;
            cursor = Math.Min(rangeEnd, end);
            if (cursor == end) return true;
        }
        return false;
    }

    private static bool Overlaps(ulong start, ulong end, ulong otherStart, ulong otherEnd) =>
        start < otherEnd && otherStart < end;
    private static bool Hash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    private static HostPreservationBindingPreflightResult Refuse(HostPreservationBindingPreflightReason reason) =>
        HostPreservationBindingPreflightResult.Refuse(reason);
}
