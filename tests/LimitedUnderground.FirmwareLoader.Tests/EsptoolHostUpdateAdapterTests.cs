using LimitedUnderground.FirmwareLoader;
using System.Security.Cryptography;
using System.Text.Json;

// L7 step 1: the esptool writer adapter against a fake flash helper that simulates the board's
// 16 MB flash in memory and computes real MD5 digests. No process is started, no port opened.
internal static class EsptoolHostUpdateAdapterTests
{
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("L7 writer layout covers the whole 16 MB flash in order", LayoutCoversFlash),
        ("L7 writer finds only the board's own USB ID", FindsOnlyBoardUsbId),
        ("L7 writer verified path: chip MD5 matches and protected spans are unchanged", VerifiedPath),
        ("L7 writer refuses misaligned or partial-sector images before any command", RefusesBadImageShape),
        ("L7 writer refuses a write whose chip MD5 does not match", RefusesWriteMismatch),
        ("L7 writer reports a changed protected span", ReportsChangedProtectedSpan),
        ("L7 writer boot without the self-check is not healthy", BootWithoutSelfCheck),
        ("L7 writer refuses a replaced selection without a command", RefusesReplacedSelection),
        ("L7 writer refuses a helper refusal and a wrong chip", RefusesHelperRefusal),
        ("L7 flash helper output must be exactly one JSON object line", HelperOutputIsOneLine),
        ("L7 flash helper runner refuses files that do not match their pins", RunnerRefusesUnpinnedFiles),
        ("L7 engine verification compares the chip MD5 with the admitted image", EngineComparesMd5),
    };

    private static void Require(bool ok, string what)
    {
        if (!ok) throw new InvalidOperationException("L7 writer: " + what);
    }

    private static void Throws(Action action, string what)
    {
        try { action(); }
        catch (Exception) { return; }
        throw new InvalidOperationException("L7 writer: expected refusal: " + what);
    }

    private static T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();
    private static void Wait(Task task) => task.GetAwaiter().GetResult();

    internal sealed class FakeHelper : IFlashHelperRunner
    {
        internal readonly byte[] Flash = new byte[EsptoolHostUpdateAdapter.FlashSize];
        internal readonly List<string> Commands = new();
        internal string Chip = "ESP32-S3";
        internal bool Healthy = true;
        internal bool CorruptWrite;
        internal bool TouchNvsOnWrite;
        internal bool Refuse;

        internal FakeHelper() { new Random(7).NextBytes(Flash); }

        public Task<FlashHelperResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan limit, CancellationToken cancellation)
        {
            Commands.Add(arguments[0]);
            string Arg(string name) => arguments[arguments.ToList().IndexOf(name) + 1];
            object reply;
            if (Refuse) reply = new { ok = false, error = "refused" };
            else switch (arguments[0])
            {
                case "list":
                    reply = new { ok = true, ports = new object[] {
                        new { port = "COM10", vid = 0x303A, pid = 0x1001, serial = "x" },
                        new { port = "COM15", vid = 0x04E8, pid = 0x6860, serial = "y" } } };
                    break;
                case "identify": reply = new { ok = true, chip = Chip, mac = "8C:FD:49:B7:09:50", flash = "16MB" }; break;
                case "digests":
                    reply = new { ok = true, spans = Arg("--spans").Split(',').Select(s =>
                    {
                        var p = s.Split(':'); int o = Convert.ToInt32(p[0], 16), n = Convert.ToInt32(p[1], 16);
                        return new { offset = o, length = n, md5 = Convert.ToHexString(MD5.HashData(Flash.AsSpan(o, n))) };
                    }).ToArray() };
                    break;
                case "write":
                    var image = File.ReadAllBytes(Arg("--file"));
                    int at = Convert.ToInt32(Arg("--offset"), 16);
                    image.CopyTo(Flash, at);
                    if (CorruptWrite) Flash[at + 5] ^= 1;
                    if (TouchNvsOnWrite) Flash[0xD000] ^= 1;
                    reply = new { ok = true, offset = at, length = image.Length, md5 = Convert.ToHexString(MD5.HashData(Flash.AsSpan(at, image.Length))) };
                    break;
                case "boot": reply = new { ok = true, loaded = true, healthy = Healthy, lines = 40 }; break;
                default: throw new InvalidOperationException("unknown command");
            }
            var json = JsonSerializer.SerializeToElement(reply);
            return Task.FromResult(new FlashHelperResult(Refuse ? 1 : 0, json));
        }
    }

    private static (FakeHelper Helper, EsptoolHostUpdateAdapter Adapter, HostSelection Selection) Board()
    {
        var helper = new FakeHelper();
        var adapter = new EsptoolHostUpdateAdapter(helper, EsptoolAdapterLimits.Bench);
        var selection = Wait(adapter.SelectAsync("COM10", CancellationToken.None));
        return (helper, adapter, selection);
    }

    private static byte[] Image(int sectors = 3)
    {
        var image = new byte[sectors * 4096];
        new Random(11).NextBytes(image);
        return image;
    }

    private static void LayoutCoversFlash()
    {
        foreach (int length in new[] { 4096, 0x123000, (int)(EsptoolHostUpdateAdapter.AppEnd - EsptoolHostUpdateAdapter.AppOffset) })
        {
            var spans = EsptoolHostUpdateAdapter.SpansFor(EsptoolHostUpdateAdapter.AppOffset, length);
            ulong cursor = 0;
            foreach (var span in spans) { Require(span.Offset == cursor && span.Length > 0, "contiguous spans"); cursor += span.Length; }
            Require(cursor == EsptoolHostUpdateAdapter.FlashSize, "whole flash covered");
            Require(spans.Count(s => s.Offset == EsptoolHostUpdateAdapter.AppOffset && s.Length == length) == 1, "one write span");
        }
    }

    private static void FindsOnlyBoardUsbId()
    {
        var adapter = new EsptoolHostUpdateAdapter(new FakeHelper(), EsptoolAdapterLimits.Bench);
        var boards = Wait(adapter.FindBoardsAsync(CancellationToken.None));
        Require(boards.Count == 1 && boards[0] == "COM10", "only 303A:1001");
        Require(adapter.CurrentSelection is null, "finding boards selects nothing");
    }

    private static void VerifiedPath()
    {
        var (helper, adapter, selection) = Board();
        Require(selection.Adapter == EsptoolHostUpdateAdapter.AdapterName && selection.Target == "heltec_v4_bench", "selection facts");
        var image = Image();
        Wait(adapter.WriteAsync(selection, 0x10000, image, 65536, CancellationToken.None));
        var check = Wait(adapter.ReadbackAsync(selection, 0x10000, image.Length, CancellationToken.None));
        Require(check.RegionMd5 == HostRegionCheck.Md5Hex(image) && check.ProtectedSpansMatch, "region and protected spans");
        var boot = Wait(adapter.BootAsync(selection, CancellationToken.None));
        Require(boot.Healthy && boot.ProtectedStateMatches && boot.ImageDigest == HostReleaseAdmission.Digest(image), "boot evidence");
        Require(string.Join(",", helper.Commands) == "identify,digests,write,digests,boot", "command order");
    }

    private static void RefusesBadImageShape()
    {
        foreach (var (offset, length) in new[] { (0x10000u, 4095), (0x10001u, 4096), (0x0u, 4096), (0x10000u, 0x4F1000) })
        {
            var (helper, adapter, selection) = Board();
            Throws(() => Wait(adapter.WriteAsync(selection, offset, new byte[length], 65536, CancellationToken.None)), "bad shape");
            Require(helper.Commands.SequenceEqual(new[] { "identify" }), "no command before shape check");
        }
    }

    private static void RefusesWriteMismatch()
    {
        var (helper, adapter, selection) = Board();
        helper.CorruptWrite = true;
        Throws(() => Wait(adapter.WriteAsync(selection, 0x10000, Image(), 65536, CancellationToken.None)), "md5 mismatch");
        Throws(() => Wait(adapter.ReadbackAsync(selection, 0x10000, Image().Length, CancellationToken.None)), "no verification after a failed write");
    }

    private static void ReportsChangedProtectedSpan()
    {
        var (helper, adapter, selection) = Board();
        helper.TouchNvsOnWrite = true;
        var image = Image();
        Wait(adapter.WriteAsync(selection, 0x10000, image, 65536, CancellationToken.None));
        var check = Wait(adapter.ReadbackAsync(selection, 0x10000, image.Length, CancellationToken.None));
        Require(check.RegionMd5 == HostRegionCheck.Md5Hex(image) && !check.ProtectedSpansMatch, "nvs change detected");
        Require(!Wait(adapter.BootAsync(selection, CancellationToken.None)).ProtectedStateMatches, "boot carries the protected result");
    }

    private static void BootWithoutSelfCheck()
    {
        var (helper, adapter, selection) = Board();
        helper.Healthy = false;
        var image = Image();
        Wait(adapter.WriteAsync(selection, 0x10000, image, 65536, CancellationToken.None));
        Wait(adapter.ReadbackAsync(selection, 0x10000, image.Length, CancellationToken.None));
        var boot = Wait(adapter.BootAsync(selection, CancellationToken.None));
        Require(!boot.Healthy && boot.ImageDigest == "", "no identity without health");
    }

    private static void RefusesReplacedSelection()
    {
        var (helper, adapter, selection) = Board();
        var copy = selection with { };
        Throws(() => Wait(adapter.WriteAsync(copy, 0x10000, Image(), 65536, CancellationToken.None)), "equal-looking copy");
        var newer = Wait(adapter.SelectAsync("COM10", CancellationToken.None));
        Require(newer.Generation > selection.Generation, "new generation");
        Throws(() => Wait(adapter.WriteAsync(selection, 0x10000, Image(), 65536, CancellationToken.None)), "stale selection");
        Require(helper.Commands.SequenceEqual(new[] { "identify", "identify" }), "no write command");
    }

    private static void RefusesHelperRefusal()
    {
        var helper = new FakeHelper { Chip = "ESP32" };
        var adapter = new EsptoolHostUpdateAdapter(helper, EsptoolAdapterLimits.Bench);
        Throws(() => Wait(adapter.SelectAsync("COM10", CancellationToken.None)), "wrong chip");
        Require(adapter.CurrentSelection is null, "nothing selected");
        var (board, boardAdapter, selection) = Board();
        board.Refuse = true;
        Throws(() => Wait(boardAdapter.WriteAsync(selection, 0x10000, Image(), 65536, CancellationToken.None)), "helper refusal");
    }

    private static void HelperOutputIsOneLine()
    {
        Require(FlashHelperOutput.Parse("{\"ok\":true}\r\n").GetProperty("ok").GetBoolean(), "one line accepted");
        foreach (var bad in new[] { "", "{\"ok\":true}\n{\"ok\":true}\n", "[1]", "not json", "{\"ok\":true} trailing" })
            Throws(() => FlashHelperOutput.Parse(bad), "bad output " + bad);
    }

    private static void RunnerRefusesUnpinnedFiles()
    {
        var folder = Path.Combine(Path.GetTempPath(), "lu-l7-pin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var file = Path.Combine(folder, "helper.py");
            File.WriteAllText(file, "print('x')");
            var right = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
            Require(ProcessFlashHelperRunner.FileMatches(file, right), "pinned file matches");
            Require(!ProcessFlashHelperRunner.FileMatches(file, new string('0', 64)), "wrong pin refused");
            Require(!ProcessFlashHelperRunner.FileMatches(Path.Combine(folder, "missing.exe"), right), "missing file refused");
            var runner = new ProcessFlashHelperRunner(new FlashHelperInstall(Path.Combine(folder, "missing.exe"), right, file, right));
            Throws(() => Wait(runner.RunAsync(new[] { "list" }, TimeSpan.FromSeconds(5), CancellationToken.None)), "unpinned interpreter");
            Throws(() => Wait(runner.RunAsync(new[] { "list" }, TimeSpan.FromMinutes(11), CancellationToken.None)), "unbounded limit");
        }
        finally { Directory.Delete(folder, true); }
    }

    private static void EngineComparesMd5()
    {
        var image = Image();
        Require(HostRegionCheck.Of(image, true).RegionMd5 == Convert.ToHexString(MD5.HashData(image)), "digest form");
        var altered = (byte[])image.Clone(); altered[0] ^= 1;
        Require(HostRegionCheck.Of(altered, true).RegionMd5 != HostRegionCheck.Md5Hex(image), "altered image differs");
    }
}
