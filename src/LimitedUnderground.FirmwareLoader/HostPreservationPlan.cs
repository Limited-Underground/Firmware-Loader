namespace LimitedUnderground.FirmwareLoader;

internal readonly record struct HostPreservationRange(ulong Offset, ulong Length);

// Mathematical host validation only: this result grants no operational authority.
// Inputs describe [0, addressSpaceLength); no target or erase defaults are inferred.
internal sealed class HostPreservationPlan
{
    internal const int MaximumSpans = 256;
    internal ulong AddressSpaceLength { get; }
    internal ulong EraseBlockSize { get; }
    internal IReadOnlyList<HostPreservationRange> AllowedRanges { get; }
    internal IReadOnlyList<HostPreservationRange> ProtectedRanges { get; }
    internal IReadOnlyList<HostPreservationRange> Writes { get; }
    internal IReadOnlyList<HostPreservationRange> EraseRanges { get; }

    private HostPreservationPlan(ulong length, ulong block,
        HostPreservationRange[] allowed, HostPreservationRange[] protectedRanges,
        HostPreservationRange[] writes, HostPreservationRange[] eraseRanges)
    {
        AddressSpaceLength = length;
        EraseBlockSize = block;
        AllowedRanges = Array.AsReadOnly(allowed);
        ProtectedRanges = Array.AsReadOnly(protectedRanges);
        Writes = Array.AsReadOnly(writes);
        EraseRanges = Array.AsReadOnly(eraseRanges);
    }

    internal static bool TryCreate(ulong addressSpaceLength, ulong eraseBlockSize,
        IReadOnlyList<HostPreservationRange>? allowedRanges,
        IReadOnlyList<HostPreservationRange>? protectedRanges,
        IReadOnlyList<HostPreservationRange>? writes, out HostPreservationPlan? plan)
    {
        plan = null;
        if (addressSpaceLength == 0 || eraseBlockSize == 0 ||
            (eraseBlockSize & (eraseBlockSize - 1)) != 0 || addressSpaceLength % eraseBlockSize != 0 ||
            allowedRanges is null || protectedRanges is null || writes is null) return false;

        int allowedCount = allowedRanges.Count, protectedCount = protectedRanges.Count, writeCount = writes.Count;
        if (allowedCount <= 0 || protectedCount < 0 || writeCount <= 0 ||
            allowedCount > MaximumSpans || protectedCount > MaximumSpans || writeCount > MaximumSpans ||
            allowedCount + protectedCount > MaximumSpans) return false;

        // Snapshot bounded caller input once. Only these private value arrays are validated/retained.
        var allowed = Snapshot(allowedRanges, allowedCount);
        var protectedCopy = Snapshot(protectedRanges, protectedCount);
        var writeCopy = Snapshot(writes, writeCount);
        if (!ValidRanges(allowed, addressSpaceLength) || !ValidRanges(protectedCopy, addressSpaceLength) ||
            !ValidRanges(writeCopy, addressSpaceLength)) return false;

        Array.Sort(allowed, CompareOffset);
        Array.Sort(protectedCopy, CompareOffset);
        Array.Sort(writeCopy, CompareOffset);
        var coverage = new (HostPreservationRange Range, bool Allowed)[allowedCount + protectedCount];
        for (int i = 0; i < allowedCount; i++) coverage[i] = (allowed[i], true);
        for (int i = 0; i < protectedCount; i++) coverage[allowedCount + i] = (protectedCopy[i], false);
        Array.Sort(coverage, (a, b) => CompareOffset(a.Range, b.Range));
        ulong cursor = 0;
        foreach (var entry in coverage)
        {
            if (entry.Range.Offset != cursor) return false; // Both gaps and all overlaps refuse.
            cursor = checked(entry.Range.Offset + entry.Range.Length);
        }
        if (cursor != addressSpaceLength) return false;

        var eraseRanges = new HostPreservationRange[writeCount];
        ulong previousEraseEnd = 0;
        for (int i = 0; i < writeCount; i++)
        {
            var write = writeCopy[i];
            ulong end = checked(write.Offset + write.Length);
            ulong start = write.Offset - write.Offset % eraseBlockSize;
            // Avoid end + block - 1, which can overflow even for an already aligned valid end.
            ulong remainder = end % eraseBlockSize;
            ulong padding = remainder == 0 ? 0 : eraseBlockSize - remainder;
            if (padding > addressSpaceLength - end) return false;
            ulong eraseEnd = checked(end + padding);
            if (i != 0 && start < previousEraseEnd) return false;
            foreach (var entry in coverage)
            {
                if (entry.Range.Offset >= eraseEnd) break;
                ulong regionEnd = checked(entry.Range.Offset + entry.Range.Length);
                if (regionEnd > start && !entry.Allowed) return false;
            }
            eraseRanges[i] = new(start, checked(eraseEnd - start));
            previousEraseEnd = eraseEnd;
        }
        plan = new(addressSpaceLength, eraseBlockSize, allowed, protectedCopy, writeCopy, eraseRanges);
        return true;
    }

    private static HostPreservationRange[] Snapshot(IReadOnlyList<HostPreservationRange> input, int count)
    {
        var copy = new HostPreservationRange[count];
        for (int i = 0; i < count; i++) copy[i] = input[i];
        return copy;
    }

    private static bool ValidRanges(HostPreservationRange[] ranges, ulong length)
    {
        foreach (var range in ranges)
            if (range.Length == 0 || range.Offset >= length || range.Length > length - range.Offset) return false;
        return true;
    }

    private static int CompareOffset(HostPreservationRange a, HostPreservationRange b) => a.Offset.CompareTo(b.Offset);
}
