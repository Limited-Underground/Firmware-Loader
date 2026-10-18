using System.Security.Cryptography;

namespace LimitedUnderground.FirmwareLoader;

internal readonly record struct HostPreservationPayload(ulong Offset, byte[]? Bytes);
internal readonly record struct HostPreservationPayloadInfo(ulong Offset, int Length, string Sha256);

// Internal synthetic host checkpoint only; not signed release admission or device authority.
internal sealed class HostPreservationPayloadBinding
{
    internal const int MaximumPayloadBytes = 16 * 1024 * 1024; // Host resource cap, not target geometry.
    private readonly byte[][] payloadBytes;
    internal HostPreservationPlan Plan { get; }
    internal IReadOnlyList<HostPreservationPayloadInfo> Payloads { get; }

    private HostPreservationPayloadBinding(HostPreservationPlan plan, byte[][] bytes,
        HostPreservationPayloadInfo[] metadata)
    {
        Plan = plan;
        payloadBytes = bytes;
        Payloads = Array.AsReadOnly(metadata);
    }

    internal byte[] CopyBytesAt(int index) => (byte[])payloadBytes[index].Clone();

    internal static bool TryCreate(HostPreservationPlan? plan,
        IReadOnlyList<HostPreservationPayload>? payloads, out HostPreservationPayloadBinding? binding)
    {
        binding = null;
        if (plan is null || payloads is null) return false;
        int count = payloads.Count;
        if (count <= 0 || count > HostPreservationPlan.MaximumSpans || count != plan.Writes.Count) return false;

        // Snapshot each entry exactly once. Bound the aggregate before any payload clone.
        var inputs = new HostPreservationPayload[count];
        int total = 0;
        for (int i = 0; i < count; i++)
        {
            var input = payloads[i];
            var bytes = input.Bytes;
            if (bytes is null || bytes.Length == 0 || bytes.Length > MaximumPayloadBytes - total) return false;
            total = checked(total + bytes.Length);
            inputs[i] = input;
        }
        Array.Sort(inputs, (a, b) => a.Offset.CompareTo(b.Offset));
        for (int i = 0; i < count; i++)
        {
            // Validated plan writes are sorted and distinct. Equality also refuses duplicate offsets.
            if (inputs[i].Offset != plan.Writes[i].Offset ||
                (ulong)inputs[i].Bytes!.Length != plan.Writes[i].Length) return false;
        }

        var copies = new byte[count][];
        for (int i = 0; i < count; i++) copies[i] = (byte[])inputs[i].Bytes!.Clone();
        var metadata = new HostPreservationPayloadInfo[count];
        for (int i = 0; i < count; i++)
            metadata[i] = new(inputs[i].Offset, copies[i].Length, Convert.ToHexString(SHA256.HashData(copies[i])));
        binding = new(plan, copies, metadata);
        return true;
    }
}
