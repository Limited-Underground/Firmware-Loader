using System.IO;
using System.Text.Json;

namespace LimitedUnderground.FirmwareLoader;

// L7 step 1 (D-063): the real writer for the Heltec V4 bench board, through the separate esptool
// flash helper. Not wired into production: no update choice uses it until the production wiring
// change (L7 step 5). Design: chip MD5 of every span of the 16 MB layout before and after the
// write; only the written span may change; a write acknowledgement is never verification.
internal sealed record EsptoolBoardIdentity(string Port, string Mac);

internal sealed record EsptoolAdapterLimits(TimeSpan List, TimeSpan Identify, TimeSpan Digests, TimeSpan Write, TimeSpan Boot, int BootWatchSeconds)
{
    internal static EsptoolAdapterLimits Bench { get; } = new(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(240), TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(60), 30);
}

internal sealed class EsptoolHostUpdateAdapter : IHostUpdateAdapter
{
    internal const string AdapterName = "esptool-helper-v1";
    internal const string Target = "heltec_v4_bench";
    internal const string Model = "HTIT-WB32LAF V4.2";
    internal const int UsbVendor = 0x303A, UsbProduct = 0x1001;
    internal const uint AppOffset = 0x10000, AppEnd = 0x500000, FlashSize = 0x1000000, Sector = 0x1000;

    // Trail heltec_v4_bench partitions.csv (OTHP0/v1), complete and in order, covering all 16 MB.
    internal static readonly (uint Offset, uint Length)[] Layout =
    {
        (0x0, 0x8000), (0x8000, 0x1000), (0x9000, 0x2000), (0xB000, 0x2000), (0xD000, 0x3000),
        (AppOffset, AppEnd - AppOffset), (0x500000, 0x500000), (0xA00000, 0x500000), (0xF00000, 0x100000),
    };

    private readonly IFlashHelperRunner runner;
    private readonly EsptoolAdapterLimits limits;
    private readonly object gate = new();
    private HostSelection? selection;
    private ulong generation;
    private (uint Offset, uint Length)[]? spans;
    private string[]? before;
    private int writtenIndex = -1;
    private string? writtenSha256;
    private bool protectedMatched;

    internal EsptoolHostUpdateAdapter(IFlashHelperRunner runner, EsptoolAdapterLimits limits)
    {
        this.runner = runner; this.limits = limits;
    }

    public HostSelection? CurrentSelection { get { lock (gate) return selection; } }

    // Passive: lists ports without opening any. Only the board's own USB ID is offered.
    internal async Task<IReadOnlyList<string>> FindBoardsAsync(CancellationToken cancellation)
    {
        var result = await Run(new[] { "list" }, limits.List, cancellation).ConfigureAwait(false);
        return result.GetProperty("ports").EnumerateArray()
            .Where(p => p.GetProperty("vid").ValueKind == JsonValueKind.Number && p.GetProperty("vid").GetInt32() == UsbVendor &&
                        p.GetProperty("pid").ValueKind == JsonValueKind.Number && p.GetProperty("pid").GetInt32() == UsbProduct)
            .Select(p => p.GetProperty("port").GetString() ?? "").Where(p => p.Length > 0).ToArray();
    }

    // Runs only when the user picks one board. Read-only: chip, MAC and flash size.
    internal async Task<HostSelection> SelectAsync(string port, CancellationToken cancellation)
    {
        lock (gate) selection = null;
        var result = await Run(new[] { "identify", "--port", port }, limits.Identify, cancellation).ConfigureAwait(false);
        if (result.GetProperty("chip").GetString() != "ESP32-S3" || result.GetProperty("flash").GetString() != "16MB")
            throw new InvalidDataException("not the supported board");
        var mac = result.GetProperty("mac").GetString() ?? "";
        if (mac.Length != 17) throw new InvalidDataException("board identity");
        lock (gate)
        {
            selection = new HostSelection(new EsptoolBoardIdentity(port, mac), ++generation, Target, Model, AdapterName);
            spans = null; before = null; writtenIndex = -1; writtenSha256 = null; protectedMatched = false;
            return selection;
        }
    }

    internal static (uint Offset, uint Length)[] SpansFor(uint offset, int length)
    {
        if (offset != AppOffset || length <= 0 || length % Sector != 0 || (ulong)length > AppEnd - AppOffset)
            throw new ArgumentException("the image must start at the app offset and be whole sectors inside the app partition");
        var list = new List<(uint, uint)>();
        foreach (var span in Layout)
        {
            if (span.Offset != AppOffset) { list.Add(span); continue; }
            list.Add((AppOffset, (uint)length));
            if ((uint)length < span.Length) list.Add((AppOffset + (uint)length, span.Length - (uint)length));
        }
        return list.ToArray();
    }

    public async Task WriteAsync(HostSelection expected, uint offset, byte[] image, int maximumChunk, CancellationToken cancellation)
    {
        var board = Expect(expected);
        if (maximumChunk <= 0) throw new ArgumentOutOfRangeException(nameof(maximumChunk));
        var layout = SpansFor(offset, image.Length);
        var original = await Digests(board, layout, cancellation).ConfigureAwait(false);
        lock (gate) { spans = layout; before = original; writtenIndex = Array.FindIndex(layout, s => s.Offset == offset); writtenSha256 = null; }
        string folder = Path.Combine(Path.GetTempPath(), "lu-loader-flash");
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".bin");
        await File.WriteAllBytesAsync(file, image, CancellationToken.None).ConfigureAwait(false);
        try
        {
            var result = await Run(new[] { "write", "--port", board.Port, "--offset", "0x" + offset.ToString("X"), "--file", file },
                limits.Write, cancellation).ConfigureAwait(false);
            if (result.GetProperty("md5").GetString() != HostRegionCheck.Md5Hex(image) ||
                result.GetProperty("offset").GetInt64() != offset || result.GetProperty("length").GetInt64() != image.Length)
                throw new InvalidDataException("write check");
            lock (gate) writtenSha256 = HostReleaseAdmission.Digest(image);
        }
        finally { try { File.Delete(file); } catch (Exception) { } }
    }

    public async Task<HostRegionCheck> ReadbackAsync(HostSelection expected, uint offset, int length, CancellationToken cancellation)
    {
        var board = Expect(expected);
        (uint Offset, uint Length)[] layout; string[] original; int index;
        lock (gate)
        {
            if (spans is null || before is null || writtenSha256 is null || writtenIndex < 0 ||
                spans[writtenIndex] != (offset, (uint)length)) throw new InvalidOperationException("no matching write");
            layout = spans; original = before; index = writtenIndex;
        }
        var after = await Digests(board, layout, cancellation).ConfigureAwait(false);
        bool same = true;
        for (int i = 0; i < layout.Length; i++) if (i != index && after[i] != original[i]) same = false;
        lock (gate) protectedMatched = same;
        return new HostRegionCheck(after[index], same);
    }

    public async Task<HostBootResult> BootAsync(HostSelection expected, CancellationToken cancellation)
    {
        var board = Expect(expected);
        string? digest; bool protectedOk;
        lock (gate) { digest = writtenSha256; protectedOk = protectedMatched; }
        if (digest is null) throw new InvalidOperationException("no verified write");
        var result = await Run(new[] { "boot", "--port", board.Port, "--seconds", limits.BootWatchSeconds.ToString() },
            limits.Boot, cancellation).ConfigureAwait(false);
        bool healthy = result.GetProperty("loaded").GetBoolean() && result.GetProperty("healthy").GetBoolean();
        // Adapter-asserted identity: the app was loaded from the written offset and passed its own
        // self-check after the chip checksum matched. A firmware-reported identity comes with L7 step 4.
        return new HostBootResult(healthy ? digest : "", healthy, protectedOk);
    }

    private EsptoolBoardIdentity Expect(HostSelection expected)
    {
        lock (gate)
        {
            if (selection is null || !ReferenceEquals(expected, selection) || expected.Identity is not EsptoolBoardIdentity board)
                throw new InvalidOperationException("selection changed");
            return board;
        }
    }

    private async Task<string[]> Digests(EsptoolBoardIdentity board, (uint Offset, uint Length)[] layout, CancellationToken cancellation)
    {
        string list = string.Join(",", layout.Select(s => "0x" + s.Offset.ToString("X") + ":0x" + s.Length.ToString("X")));
        var result = await Run(new[] { "digests", "--port", board.Port, "--spans", list }, limits.Digests, cancellation).ConfigureAwait(false);
        var items = result.GetProperty("spans").EnumerateArray().ToArray();
        if (items.Length != layout.Length) throw new InvalidDataException("digest count");
        var digests = new string[layout.Length];
        for (int i = 0; i < layout.Length; i++)
        {
            var md5 = items[i].GetProperty("md5").GetString() ?? "";
            if (items[i].GetProperty("offset").GetInt64() != layout[i].Offset || items[i].GetProperty("length").GetInt64() != layout[i].Length ||
                md5.Length != 32 || !md5.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F')) throw new InvalidDataException("digest");
            digests[i] = md5;
        }
        return digests;
    }

    private async Task<JsonElement> Run(string[] arguments, TimeSpan limit, CancellationToken cancellation)
    {
        var result = await runner.RunAsync(arguments, limit, cancellation).ConfigureAwait(false);
        if (result.ExitCode != 0 || !result.Json.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True)
            throw new InvalidOperationException("flash helper refused");
        return result.Json;
    }
}

// Neutral names for the bench writer facts other code needs (the release builder), so that only
// this reviewed file names the flashing tool.
internal static class BenchFlash
{
    internal const string AdapterName = EsptoolHostUpdateAdapter.AdapterName;
    internal const string Target = EsptoolHostUpdateAdapter.Target;
    internal const uint AppOffset = EsptoolHostUpdateAdapter.AppOffset, AppEnd = EsptoolHostUpdateAdapter.AppEnd;
    internal const uint FlashSize = EsptoolHostUpdateAdapter.FlashSize, Sector = EsptoolHostUpdateAdapter.Sector;
    internal static (uint Offset, uint Length)[] Layout => EsptoolHostUpdateAdapter.Layout;
    internal static EsptoolAdapterLimits Limits => EsptoolAdapterLimits.Bench;
    internal static (uint Offset, uint Length)[] SpansFor(uint offset, int length) => EsptoolHostUpdateAdapter.SpansFor(offset, length);

    // LU-WRITER-PROFILE-V1: its SHA-256 is the release record's writerProfileDigest (L7 step 2).
    internal static byte[] WriterProfile(string helperSha256)
    {
        if (helperSha256.Length != 64 || !helperSha256.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F'))
            throw new ArgumentException("helper digest");
        var limits = EsptoolAdapterLimits.Bench;
        return QualificationReleaseBuilder.Json(w =>
        {
            w.WriteString("format", "LU-WRITER-PROFILE-V1");
            w.WriteString("adapter", EsptoolHostUpdateAdapter.AdapterName);
            w.WriteString("esptool", "5.3.1");
            w.WriteString("helperSha256", helperSha256);
            w.WriteString("chip", "ESP32-S3");
            w.WriteNumber("usbVendor", EsptoolHostUpdateAdapter.UsbVendor);
            w.WriteNumber("usbProduct", EsptoolHostUpdateAdapter.UsbProduct);
            w.WriteNumber("flashSize", EsptoolHostUpdateAdapter.FlashSize);
            w.WriteNumber("appOffset", EsptoolHostUpdateAdapter.AppOffset);
            w.WriteNumber("sector", EsptoolHostUpdateAdapter.Sector);
            w.WriteNumber("attempts", 1);
            w.WriteString("verification", "chip-md5-every-span-before-and-after");
            w.WriteNumber("writeSeconds", (long)limits.Write.TotalSeconds);
            w.WriteNumber("digestSeconds", (long)limits.Digests.TotalSeconds);
            w.WriteNumber("bootSeconds", (long)limits.Boot.TotalSeconds);
        });
    }
}
