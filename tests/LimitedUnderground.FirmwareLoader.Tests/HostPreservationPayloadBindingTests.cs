using LimitedUnderground.FirmwareLoader;
using System.Collections;
using System.Security.Cryptography;

internal static class HostPreservationPayloadBindingTests
{
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("payload binding accepts multiple exact writes in plan order", MultipleWrites),
        ("payload binding accepts reversed input and derives correct snapshot digests", ReversedInputs),
        ("payload binding refuses null plan collection and bytes", MissingInputs),
        ("payload binding refuses empty payloads and collections", EmptyInputs),
        ("payload binding refuses missing extra and duplicate entries", EntryMismatch),
        ("payload binding refuses wrong offsets and exact-length mismatches", RangeMismatch),
        ("payload binding matches raw write spans rather than erase-rounded spans", RawWriteBinding),
        ("payload binding accepts exact write endpoints with unsigned offsets", UpperOffsets),
        ("payload binding accepts 256 entries and refuses 257 before enumeration", EntryLimit),
        ("payload binding accepts exactly 16 MiB aggregate across multiple entries", AggregateBoundary),
        ("payload binding refuses aggregate and individual payloads above 16 MiB", AggregateRefusal),
        ("payload binding freezes caller entries and byte arrays", CallerMutation),
        ("payload binding returns byte copies and immutable metadata", ResultMutation),
        ("payload binding retains the exact plan instance without conflating geometry", ExactPlan),
        ("payload binding snapshots caller collection entries exactly once", SingleSnapshot),
    };

    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    private static HostPreservationPlan Plan(ulong size = 64, ulong block = 8,
        HostPreservationRange[]? writes = null, HostPreservationRange[]? allowed = null,
        HostPreservationRange[]? protectedRanges = null)
    {
        Require(HostPreservationPlan.TryCreate(size, block,
            allowed ?? new HostPreservationRange[] { new(0, size) },
            protectedRanges ?? Array.Empty<HostPreservationRange>(),
            writes ?? new HostPreservationRange[] { new(1, 3), new(17, 2) }, out var plan), "fixture plan refused");
        return plan!;
    }
    private static HostPreservationPayload[] Inputs() =>
        new HostPreservationPayload[] { new(1, new byte[] { 1, 2, 3 }), new(17, new byte[] { 4, 5 }) };
    private static HostPreservationPayloadBinding Accept(HostPreservationPlan plan, IReadOnlyList<HostPreservationPayload> inputs)
    {
        Require(HostPreservationPayloadBinding.TryCreate(plan, inputs, out var binding) && binding is not null,
            "valid payload binding refused");
        return binding!;
    }
    private static void Refuse(HostPreservationPlan? plan, IReadOnlyList<HostPreservationPayload>? inputs)
    { Require(!HostPreservationPayloadBinding.TryCreate(plan, inputs, out var binding) && binding is null, "invalid binding accepted"); }
    private static void MultipleWrites()
    {
        var plan = Plan(); var inputs = Inputs(); var bound = Accept(plan, inputs);
        Require(ReferenceEquals(plan, bound.Plan) && bound.Payloads.Count == 2, "plan/count binding");
        for (int i = 0; i < 2; i++)
        {
            Require(bound.Payloads[i].Offset == plan.Writes[i].Offset &&
                (ulong)bound.Payloads[i].Length == plan.Writes[i].Length, "exact range");
            Require(bound.CopyBytesAt(i).SequenceEqual(inputs[i].Bytes!), "exact payload bytes");
        }
    }
    private static void ReversedInputs()
    {
        var bound = Accept(Plan(), Inputs().Reverse().ToArray());
        Require(bound.Payloads[0].Offset == 1 && bound.Payloads[1].Offset == 17, "plan order");
        Require(bound.Payloads[0].Sha256 == "039058C6F2C0CB492C533B0A4D14EF77CC0F78ABCCCED5287D84A1A2011CFB81", "known SHA256 vector");
        for (int i = 0; i < bound.Payloads.Count; i++)
            Require(bound.Payloads[i].Sha256 == Convert.ToHexString(SHA256.HashData(bound.CopyBytesAt(i))), "digest from retained bytes");
    }
    private static void MissingInputs()
    {
        Refuse(null, Inputs()); Refuse(Plan(), null);
        Refuse(Plan(), new HostPreservationPayload[] { new(1, null), Inputs()[1] });
    }
    private static void EmptyInputs()
    {
        Refuse(Plan(), Array.Empty<HostPreservationPayload>());
        Refuse(Plan(), new HostPreservationPayload[] { new(1, Array.Empty<byte>()), Inputs()[1] });
    }
    private static void EntryMismatch()
    {
        var inputs = Inputs(); Refuse(Plan(), inputs.Take(1).ToArray());
        Refuse(Plan(), inputs.Concat(new[] { new HostPreservationPayload(32, new byte[] { 9 }) }).ToArray());
        Refuse(Plan(), new[] { inputs[0], inputs[0] });
        Refuse(Plan(), new[] { inputs[1], inputs[1] });
    }
    private static void RangeMismatch()
    {
        foreach (ulong offset in new ulong[] { 0, 2, 16, 18, 64, ulong.MaxValue })
            Refuse(Plan(), new HostPreservationPayload[] { new(offset, new byte[] { 1, 2, 3 }), Inputs()[1] });
        Refuse(Plan(), new HostPreservationPayload[] { new(1, new byte[] { 1, 2 }), Inputs()[1] });
        Refuse(Plan(), new HostPreservationPayload[] { new(1, new byte[] { 1, 2, 3, 4 }), Inputs()[1] });
    }
    private static void RawWriteBinding()
    {
        var plan = Plan(); Accept(plan, Inputs());
        Refuse(plan, new HostPreservationPayload[] { new(0, new byte[8]), Inputs()[1] });
        Refuse(plan, new HostPreservationPayload[] { new(1, new byte[8]), Inputs()[1] });
    }
    private static void UpperOffsets()
    {
        var plan = Plan(ulong.MaxValue, 1, new HostPreservationRange[] { new(ulong.MaxValue - 2, 2) });
        var bound = Accept(plan, new HostPreservationPayload[] { new(ulong.MaxValue - 2, new byte[] { 8, 9 }) });
        Require(bound.Payloads[0].Offset == ulong.MaxValue - 2 && bound.Payloads[0].Length == 2, "unsigned endpoint");
        Refuse(plan, new HostPreservationPayload[] { new(ulong.MaxValue - 1, new byte[] { 8, 9 }) });
    }
    private static void EntryLimit()
    {
        var writes = Enumerable.Range(0, 256).Select(i => new HostPreservationRange((ulong)i, 1)).ToArray();
        var plan = Plan(256, 1, writes);
        var inputs = Enumerable.Range(0, 256).Select(i => new HostPreservationPayload((ulong)i, new[] { (byte)i })).ToArray();
        Require(Accept(plan, inputs.Reverse().ToArray()).Payloads.Count == 256, "256 payloads accepted");
        Refuse(plan, new OversizedInputs());
    }
    private static HostPreservationPlan LargePlan() => Plan(32UL * 1024 * 1024, 1,
        new HostPreservationRange[] { new(0, 8UL * 1024 * 1024), new(8UL * 1024 * 1024, 8UL * 1024 * 1024) });
    private static void AggregateBoundary()
    {
        var inputs = new HostPreservationPayload[] { new(0, new byte[8 * 1024 * 1024]), new(8UL * 1024 * 1024, new byte[8 * 1024 * 1024]) };
        var bound = Accept(LargePlan(), inputs);
        Require(bound.Payloads.Sum(p => p.Length) == 16 * 1024 * 1024, "aggregate inclusive bound");
    }
    private static void AggregateRefusal()
    {
        // All entries match their plan: only the aggregate resource limit fails.
        ulong half = 8UL * 1024 * 1024;
        var plan = Plan(32UL * 1024 * 1024, 1, new HostPreservationRange[] { new(0, half), new(half, half + 1) });
        Refuse(plan, new HostPreservationPayload[] { new(0, new byte[(int)half]), new(half, new byte[(int)half + 1]) });
        int over = HostPreservationPayloadBinding.MaximumPayloadBytes + 1;
        Refuse(Plan((ulong)over, 1, new HostPreservationRange[] { new(0, (ulong)over) }),
            new HostPreservationPayload[] { new(0, new byte[over]) });
    }
    private static void CallerMutation()
    {
        var inputs = Inputs(); var originalFirst = inputs[0].Bytes!; var originalSecond = inputs[1].Bytes!;
        var bound = Accept(Plan(), inputs); string digest = bound.Payloads[0].Sha256;
        originalFirst[0] = 99; originalSecond[0] = 98;
        inputs[0] = new(17, new byte[] { 9 }); Array.Reverse(inputs);
        Require(bound.CopyBytesAt(0).SequenceEqual(new byte[] { 1, 2, 3 }) &&
            bound.CopyBytesAt(1).SequenceEqual(new byte[] { 4, 5 }) && bound.Payloads[0].Sha256 == digest, "caller mutation leaked");
    }
    private static void ResultMutation()
    {
        var bound = Accept(Plan(), Inputs()); var bytes = bound.CopyBytesAt(0); bytes[0] = 99;
        Require(bound.CopyBytesAt(0)[0] == 1 && !ReferenceEquals(bytes, bound.CopyBytesAt(0)), "output alias leaked");
        Require(bound.Payloads is not HostPreservationPayloadInfo[], "metadata array exposed");
        var metadata = (IList<HostPreservationPayloadInfo>)bound.Payloads;
        Require(metadata.IsReadOnly, "metadata mutable");
        bool refused = false; try { metadata[0] = new(0, 1, "changed"); } catch (NotSupportedException) { refused = true; }
        Require(refused, "metadata edit accepted");
    }
    private static void ExactPlan()
    {
        var first = Plan(); var second = Plan(block: 16); var bound = Accept(first, Inputs());
        Require(first.Writes.SequenceEqual(second.Writes) && ReferenceEquals(bound.Plan, first) &&
            !ReferenceEquals(bound.Plan, second), "plan instance lost");
    }
    private static void SingleSnapshot()
    {
        var source = new SingleReadInputs(Inputs()); var bound = Accept(Plan(), source);
        Require(source.Reads.All(count => count == 1) && bound.CopyBytesAt(0)[0] == 1, "entry reread");
    }
    private sealed class OversizedInputs : IReadOnlyList<HostPreservationPayload>
    {
        public int Count => 257;
        public HostPreservationPayload this[int index] => throw new InvalidOperationException("oversized input inspected");
        public IEnumerator<HostPreservationPayload> GetEnumerator() => throw new InvalidOperationException("unbounded enumeration");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class SingleReadInputs(HostPreservationPayload[] inputs) : IReadOnlyList<HostPreservationPayload>
    {
        internal int[] Reads { get; } = new int[inputs.Length];
        public int Count => inputs.Length;
        public HostPreservationPayload this[int index] => ++Reads[index] == 1 ? inputs[index] :
            throw new InvalidOperationException("input entry read twice");
        public IEnumerator<HostPreservationPayload> GetEnumerator() => throw new InvalidOperationException("unexpected enumeration");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
