using LimitedUnderground.FirmwareLoader;
using System.Buffers.Binary;
using System.IO;

internal static class DailyLoaderProtocolTests
{
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("daily worker transport keeps release bytes and public status messages distinct", RoundTrip),
        ("daily worker transport rejects unbounded and truncated release frames", RefusesBadFrames),
    };

    private static void RoundTrip()
    {
        using var stream = new MemoryStream();
        var original = new QualificationReleaseFiles([1, 2], [3, 4], [5, 6, 7], [8, 9, 10, 11]);
        var approved = new QualificationOperationStatus("Ready", "Verify this selection.", "Release 7", false, true,
            "Assigned Heltec V4 board");
        DailyLoaderProtocol.WriteRequestAsync(stream, DailyLoaderRequest.Prepare, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        DailyLoaderProtocol.WriteReleaseAsync(stream, original, CancellationToken.None).GetAwaiter().GetResult();
        DailyLoaderProtocol.WriteStatusAsync(stream, approved, CancellationToken.None).GetAwaiter().GetResult();

        stream.Position = 0;
        var request = DailyLoaderProtocol.ReadRequestAsync(stream, CancellationToken.None).GetAwaiter().GetResult();
        var actual = DailyLoaderProtocol.ReadReleaseAsync(stream, CancellationToken.None).GetAwaiter().GetResult();
        var status = DailyLoaderProtocol.ReadStatusAsync(stream, CancellationToken.None).GetAwaiter().GetResult();
        Require(request == DailyLoaderRequest.Prepare && actual.Manifest.SequenceEqual(original.Manifest) &&
            actual.Signature.SequenceEqual(original.Signature) && actual.Raw.SequenceEqual(original.Raw) &&
            actual.Padded.SequenceEqual(original.Padded) && status == approved &&
            stream.Position == stream.Length, "exact independent frame round trip");
    }

    private static void RefusesBadFrames()
    {
        using var oversized = new MemoryStream(new byte[4]);
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, 16385);
        oversized.Write(header);
        oversized.Position = 0;
        RequireRefusal<InvalidDataException>(oversized, "reject oversized manifest before reading payload");
        Require(oversized.Position == 4, "only oversized header consumed");

        using var truncated = new MemoryStream(new byte[] { 3, 0, 0, 0, 0xff });
        RequireRefusal<EndOfStreamException>(truncated, "reject incomplete release");
    }

    private static void RequireRefusal<T>(Stream stream, string reason) where T : Exception
    {
        try { _ = DailyLoaderProtocol.ReadReleaseAsync(stream, CancellationToken.None).GetAwaiter().GetResult(); }
        catch (T) { return; }
        throw new InvalidOperationException(reason);
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
