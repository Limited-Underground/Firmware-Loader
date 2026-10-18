using LimitedUnderground.FirmwareLoader;

internal static class HostPreservationPlanTests
{
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("preservation accepts complete unsorted multiple allowed and protected ranges", CompletePlan),
        ("preservation accepts adjacent allowed union and half-open boundaries", AdjacentAllowed),
        ("preservation rejects missing inputs and empty required collections", MissingInputs),
        ("preservation rejects invalid address space and erase geometry", Geometry),
        ("preservation rejects zero length and out-of-bounds classification ranges", InvalidClassification),
        ("preservation rejects prefix internal and suffix coverage gaps", Gaps),
        ("preservation rejects same-kind cross-kind and duplicate coverage overlaps", Overlaps),
        ("preservation rejects invalid and overflowing write ranges", InvalidWrites),
        ("preservation rejects direct protected contact and rounding into protection", ProtectedContact),
        ("preservation rejects overlapping writes and overlapping erase footprints", WriteOverlap),
        ("preservation accepts exactly 256 classification spans and refuses 257", ClassificationLimit),
        ("preservation accepts exactly 256 writes and refuses 257", WriteLimit),
        ("preservation handles upper unsigned address boundaries without wrapping", UpperBoundary),
        ("preservation freezes inputs and exposes read-only range collections", Immutability),
    };

    private static readonly HostPreservationRange[] Allowed = { new(0, 32), new(48, 48) };
    private static readonly HostPreservationRange[] Protected = { new(32, 16), new(96, 32) };
    private static readonly HostPreservationRange[] Writes = { new(1, 30), new(49, 30) };
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    private static HostPreservationPlan Accept(ulong size, ulong block, HostPreservationRange[] allowed,
        HostPreservationRange[] protectedRanges, HostPreservationRange[] writes)
    {
        Require(HostPreservationPlan.TryCreate(size, block, allowed, protectedRanges, writes, out var plan), "valid plan refused");
        return plan ?? throw new InvalidOperationException("successful validation returned null");
    }
    private static void Refuse(ulong size = 128, ulong block = 16,
        HostPreservationRange[]? allowed = null, HostPreservationRange[]? protectedRanges = null,
        HostPreservationRange[]? writes = null)
    {
        Require(!HostPreservationPlan.TryCreate(size, block, allowed ?? Allowed, protectedRanges ?? Protected,
            writes ?? Writes, out var plan) && plan is null, "invalid plan accepted or result leaked");
    }
    private static void CompletePlan()
    {
        var p = Accept(128, 16, Allowed.Reverse().ToArray(), Protected.Reverse().ToArray(), Writes.Reverse().ToArray());
        Require(p.AddressSpaceLength == 128 && p.EraseBlockSize == 16, "explicit geometry retained");
        Require(p.AllowedRanges.SequenceEqual(Allowed) && p.ProtectedRanges.SequenceEqual(Protected), "classification sorted");
        Require(p.Writes.SequenceEqual(Writes) && p.EraseRanges.SequenceEqual(new HostPreservationRange[] { new(0, 32), new(48, 32) }), "rounded spans");
    }
    private static void AdjacentAllowed()
    {
        var p = Accept(64, 16, new HostPreservationRange[] { new(0, 7), new(7, 25), new(48, 16) },
            new HostPreservationRange[] { new(32, 16) }, new HostPreservationRange[] { new(0, 32), new(48, 16) });
        Require(p.EraseRanges[0] == new HostPreservationRange(0, 32), "adjacent allowed union");
        Accept(32, 16, new HostPreservationRange[] { new(0, 32) }, Array.Empty<HostPreservationRange>(),
            new HostPreservationRange[] { new(0, 16), new(16, 16) });
    }
    private static void MissingInputs()
    {
        Require(!HostPreservationPlan.TryCreate(128, 16, null, Protected, Writes, out var a) && a is null, "missing allowed");
        Require(!HostPreservationPlan.TryCreate(128, 16, Allowed, null, Writes, out var b) && b is null, "missing protected");
        Require(!HostPreservationPlan.TryCreate(128, 16, Allowed, Protected, null, out var c) && c is null, "missing writes");
        Refuse(allowed: Array.Empty<HostPreservationRange>());
        Refuse(writes: Array.Empty<HostPreservationRange>());
        Refuse(protectedRanges: Array.Empty<HostPreservationRange>()); // Missing coverage, not an implicit default.
    }
    private static void Geometry()
    { Refuse(size: 0); Refuse(block: 0); Refuse(block: 3); Refuse(block: 256); Refuse(size: 127); }
    private static void InvalidClassification()
    {
        foreach (var invalid in new HostPreservationRange[] { new(0, 0), new(128, 1), new(127, 2), new(ulong.MaxValue, 2), new(1, ulong.MaxValue) })
        { Refuse(allowed: new[] { invalid }); Refuse(protectedRanges: new[] { invalid }); }
    }
    private static void Gaps()
    {
        Refuse(allowed: new HostPreservationRange[] { new(1, 31), new(48, 48) });
        Refuse(allowed: new HostPreservationRange[] { new(0, 31), new(48, 48) });
        Refuse(protectedRanges: new HostPreservationRange[] { new(32, 16), new(96, 31) });
    }
    private static void Overlaps()
    {
        Refuse(allowed: new HostPreservationRange[] { new(0, 32), new(16, 16), new(48, 48) });
        Refuse(protectedRanges: new HostPreservationRange[] { new(32, 16), new(40, 8), new(96, 32) });
        Refuse(allowed: new HostPreservationRange[] { new(0, 33), new(48, 48) });
        Refuse(allowed: Allowed.Concat(new[] { Allowed[0] }).ToArray());
        Refuse(protectedRanges: Protected.Concat(new[] { Protected[0] }).ToArray());
    }
    private static void InvalidWrites()
    {
        foreach (var invalid in new HostPreservationRange[] { new(0, 0), new(128, 1), new(127, 2), new(ulong.MaxValue, 2), new(1, ulong.MaxValue) })
            Refuse(writes: new[] { invalid });
    }
    private static void ProtectedContact()
    {
        Refuse(writes: new HostPreservationRange[] { new(32, 1) });
        Refuse(writes: new HostPreservationRange[] { new(31, 2) });
        Refuse(allowed: new HostPreservationRange[] { new(0, 31), new(48, 48) },
            protectedRanges: new HostPreservationRange[] { new(31, 17), new(96, 32) },
            writes: new HostPreservationRange[] { new(16, 15) }); // Raw end fits, rounded end touches protection.
        Refuse(allowed: new HostPreservationRange[] { new(1, 31), new(48, 48) },
            protectedRanges: new HostPreservationRange[] { new(0, 1), new(32, 16), new(96, 32) },
            writes: new HostPreservationRange[] { new(1, 15) }); // Rounded start touches protection.
        Refuse(writes: new HostPreservationRange[] { new(16, 48) }); // Allowed endpoints straddle protection.
    }
    private static void WriteOverlap()
    {
        Refuse(writes: new HostPreservationRange[] { new(0, 16), new(0, 16) });
        Refuse(writes: new HostPreservationRange[] { new(0, 20), new(16, 16) });
        Refuse(writes: new HostPreservationRange[] { new(0, 1), new(2, 1) });
    }
    private static void ClassificationLimit()
    {
        var allowed = Enumerable.Range(0, 128).Select(i => new HostPreservationRange((ulong)i * 2, 1)).ToArray();
        var protectedRanges = Enumerable.Range(0, 128).Select(i => new HostPreservationRange((ulong)i * 2 + 1, 1)).ToArray();
        Accept(256, 1, allowed, protectedRanges, new HostPreservationRange[] { new(0, 1) });
        var tooMany = Enumerable.Range(0, 257).Select(i => new HostPreservationRange((ulong)i, 1)).ToArray();
        Refuse(size: 257, block: 1, allowed: tooMany, protectedRanges: Array.Empty<HostPreservationRange>(), writes: new HostPreservationRange[] { new(0, 1) });
        Refuse(size: 257, block: 1, allowed: allowed.Concat(new HostPreservationRange[] { new(256, 1) }).ToArray(),
            protectedRanges: protectedRanges, writes: new HostPreservationRange[] { new(0, 1) });
        Refuse(protectedRanges: tooMany);
    }
    private static void WriteLimit()
    {
        var writes = Enumerable.Range(0, 256).Select(i => new HostPreservationRange((ulong)i, 1)).ToArray();
        Accept(512, 1, new HostPreservationRange[] { new(0, 512) }, Array.Empty<HostPreservationRange>(), writes);
        Refuse(size: 512, block: 1, allowed: new HostPreservationRange[] { new(0, 512) },
            protectedRanges: Array.Empty<HostPreservationRange>(),
            writes: Enumerable.Range(0, 257).Select(i => new HostPreservationRange((ulong)i, 1)).ToArray());
    }
    private static void UpperBoundary()
    {
        Accept(ulong.MaxValue, 1, new HostPreservationRange[] { new(0, ulong.MaxValue) },
            Array.Empty<HostPreservationRange>(), new HostPreservationRange[] { new(ulong.MaxValue - 1, 1) });
        ulong size = ulong.MaxValue - 15;
        Accept(size, 16, new HostPreservationRange[] { new(0, size) }, Array.Empty<HostPreservationRange>(),
            new HostPreservationRange[] { new(size - 1, 1) });
        Refuse(size: size, allowed: new HostPreservationRange[] { new(0, size) },
            protectedRanges: Array.Empty<HostPreservationRange>(), writes: new HostPreservationRange[] { new(size - 1, 2) });
    }
    private static void Immutability()
    {
        var allowed = Allowed.ToArray(); var protectedRanges = Protected.ToArray(); var writes = Writes.ToArray();
        var p = Accept(128, 16, allowed, protectedRanges, writes);
        allowed[0] = new(0, 1); protectedRanges[0] = new(0, 128); writes[0] = new(32, 16);
        Require(p.AllowedRanges.SequenceEqual(Allowed) && p.ProtectedRanges.SequenceEqual(Protected) &&
            p.Writes.SequenceEqual(Writes) && p.EraseRanges[0] == new HostPreservationRange(0, 32), "caller mutation leaked");
        foreach (var collection in new[] { p.AllowedRanges, p.ProtectedRanges, p.Writes, p.EraseRanges })
        {
            Require(collection is not HostPreservationRange[], "mutable array exposed");
            var list = (IList<HostPreservationRange>)collection;
            Require(list.IsReadOnly, "collection mutable");
            bool refused = false;
            try { list[0] = new(0, 1); } catch (NotSupportedException) { refused = true; }
            Require(refused, "output mutation accepted");
        }
    }
}
