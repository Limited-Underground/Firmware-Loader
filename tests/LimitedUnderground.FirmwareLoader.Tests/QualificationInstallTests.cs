using LimitedUnderground.FirmwareLoader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// L7 step 5: the production qualification install path end to end, on the real writer adapter
// driven by a fake helper that simulates board A's 16 MB flash. A throwaway key stands in for the
// owner's key; no process is started, no port opened, no administrator folder created.
internal static class QualificationInstallTests
{
    internal static string? RepositoryRoot { get; set; }

    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("L7 install: signed release on board A writes, verifies and boots", InstallsOnBoardA),
        ("L7 install: a different board is refused before any write", RefusesOtherBoard),
        ("L7 install: declining to set board A is refused before any write", RefusesWithoutPin),
        ("L7 install: wrong current firmware on the board is refused before any write", RefusesWrongCurrentFirmware),
        ("L7 install: a record changed after signing is refused before any write", RefusesChangedRecord),
        ("L7 install: a record for another key is refused before any write", RefusesOtherKey),
        ("L7 install: no board or two boards are refused", RefusesBoardCount),
        ("L7 install: production session is labelled as a real write, not a simulation", ProductionLabel),
        ("L7 install: real bench preservation plan accepts only whole sectors in the app partition", BindingGeometry),
        ("L7 install: pinned helper digest and embedded key match the committed files", PinsMatchFiles),
    };

    private static void Require(bool ok, string what)
    {
        if (!ok) throw new InvalidOperationException("L7 install: " + what);
    }

    private static T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();

    private sealed class Bench : IDisposable
    {
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "lu-l7-install-" + Guid.NewGuid().ToString("N"));
        internal readonly EsptoolHostUpdateAdapterTests.FakeHelper Helper = new();
        internal readonly IBenchBoard Board;
        internal readonly RSA Key = RSA.Create(3072);
        internal readonly LoaderSessionController Controller = new(LoaderProductionProviders.Factories);
        internal readonly QualificationBoardPin Pin;
        internal readonly byte[] Boot = Bytes(22_480, 1), Current = Bytes(604_944, 2), Candidate = Bytes(605_328, 3);
        internal readonly string Folder;
        internal HostOperationStore? Store;
        internal readonly string StoreRoot = Path.Combine(Path.GetTempPath(), "LUF.HostStore.Tests." + Guid.NewGuid().ToString("N"));
        internal byte[] Before;
        internal long Now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        internal Bench(Action<JsonObject>? change = null, bool sign = true)
        {
            Directory.CreateDirectory(Root);
            Board = BenchFlash.CreateBoard(Helper);
            Pin = new QualificationBoardPin(Path.Combine(Root, "pin"), protect: false);
            Boot.CopyTo(Helper.Flash, 0);
            Current.CopyTo(Helper.Flash, (int)BenchFlash.AppOffset);
            Before = (byte[])Helper.Flash.Clone();
            Require(Controller.SelectProduct("opentrail"), "select product");
            var release = QualificationReleaseBuilder.Build(new QualificationInputs(Candidate, Current, Boot, "trail/8f3c18a test",
                2, 1, 1, Now - 1000, BenchFlash.HelperSha256));
            var node = JsonNode.Parse(release.Manifest)!.AsObject();
            node["publicKeyFingerprint"] = HostReleaseAdmission.Digest(Key.ExportSubjectPublicKeyInfo());
            var bytes = Encoding.UTF8.GetBytes(node.ToJsonString());
            var signature = Key.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            change?.Invoke(node);
            Folder = Path.Combine(Root, "release");
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(Path.Combine(Folder, "release.json"), Encoding.UTF8.GetBytes(node.ToJsonString()));
            File.WriteAllBytes(Path.Combine(Folder, "release.sig"), signature);
            File.WriteAllBytes(Path.Combine(Folder, "raw.bin"), release.RawImage);
            File.WriteAllBytes(Path.Combine(Folder, "padded.bin"), release.PaddedImage);
        }

        internal QualificationPrepared Prepare(bool acceptPin = true, byte[]? key = null) => Wait(QualificationInstaller.PrepareAsync(
            Controller, Folder, Board, Pin, () => Store ??= HostOperationStore.CreateIsolatedTestStore(StoreRoot),
            BenchFlash.HelperSha256, new string('A', 64), key ?? Key.ExportSubjectPublicKeyInfo(), () => Now++, () => acceptPin, CancellationToken.None));

        internal bool NothingWritten => Helper.Flash.AsSpan().SequenceEqual(Before) && !Helper.Commands.Contains("write");

        public void Dispose()
        {
            Controller.Dispose(); Key.Dispose();
            if (Path.GetFileName(Root).StartsWith("lu-l7-install-", StringComparison.Ordinal)) Directory.Delete(Root, true);
            if (Directory.Exists(StoreRoot)) Directory.Delete(StoreRoot, true);
        }
    }

    private static byte[] Bytes(int length, int seed) { var b = new byte[length]; new Random(seed).NextBytes(b); return b; }

    private static void InstallsOnBoardA()
    {
        using var bench = new Bench();
        var prepared = bench.Prepare();
        Require(prepared.Workflow is not null, "prepared: " + prepared.Heading + " " + prepared.Detail);
        var workflow = prepared.Workflow!;
        Require(workflow.Choices.Count == 1 && bench.NothingWritten, "one choice, nothing written yet");
        workflow.SelectedChoice = workflow.Choices[0];
        Require(workflow.CanConfirm && !workflow.CanStart, "confirmation needed: " + workflow.Heading + " / " + workflow.Detail);
        workflow.Confirmed = true;
        workflow.StartAsync().GetAwaiter().GetResult();
        Require(workflow.Heading == "Update verified", "verified: " + workflow.Heading + " / " + workflow.Detail);
        var padded = QualificationReleaseBuilder.Pad(bench.Candidate);
        Require(bench.Helper.Flash.AsSpan((int)BenchFlash.AppOffset, padded.Length).SequenceEqual(padded), "new image on the board");
        Require(bench.Helper.Flash.AsSpan(0, (int)BenchFlash.AppOffset).SequenceEqual(bench.Before.AsSpan(0, (int)BenchFlash.AppOffset)) &&
                bench.Helper.Flash.AsSpan((int)BenchFlash.AppOffset + padded.Length).SequenceEqual(bench.Before.AsSpan((int)BenchFlash.AppOffset + padded.Length)),
                "every other byte unchanged");
        Require(bench.Helper.Commands.Count(c => c == "write") == 1 && bench.Helper.Commands.Count(c => c == "boot") == 1, "one write, one boot");
        Require(bench.Store is not null && !bench.Store.HasUnresolvedOperation, "journal resolved");
    }

    private static void RefusesOtherBoard()
    {
        using var bench = new Bench();
        Require(bench.Pin.TryPin("00:11:22:33:44:55", new string('A', 64)), "pin another board");
        var prepared = bench.Prepare();
        Require(prepared.Workflow is null && prepared.Heading == "Not board A" && bench.NothingWritten, "other board refused");
    }

    private static void RefusesWithoutPin()
    {
        using var bench = new Bench();
        var prepared = bench.Prepare(acceptPin: false);
        Require(prepared.Workflow is null && prepared.Heading == "Board A not set" && !bench.Pin.IsPinned && bench.NothingWritten, "no pin");
    }

    private static void RefusesWrongCurrentFirmware()
    {
        using var bench = new Bench();
        bench.Helper.Flash[(int)BenchFlash.AppOffset + 100] ^= 1;
        bench.Before = (byte[])bench.Helper.Flash.Clone();
        var prepared = bench.Prepare();
        Require(prepared.Workflow is null && prepared.Heading == "Release not admitted" && bench.NothingWritten, "wrong current firmware: " + prepared.Heading);
    }

    private static void RefusesChangedRecord()
    {
        using var bench = new Bench(change: node => node["sourceBuildId"] = "trail/changed after signing");
        var prepared = bench.Prepare();
        Require(prepared.Workflow is null && prepared.Heading == "Release not admitted" && bench.NothingWritten, "changed record: " + prepared.Heading);
    }

    private static void RefusesOtherKey()
    {
        using var bench = new Bench();
        using var other = RSA.Create(3072);
        var prepared = bench.Prepare(key: other.ExportSubjectPublicKeyInfo());
        Require(prepared.Workflow is null && prepared.Heading == "Release not signed for this Loader" && bench.NothingWritten, "other key");
    }

    private static void RefusesBoardCount()
    {
        using var bench = new Bench();
        bench.Helper.Ports = Array.Empty<(string, int, int)>();
        Require(bench.Prepare().Heading == "No board found", "no board");
        bench.Helper.Ports = new[] { ("COM10", 0x303A, 0x1001), ("COM11", 0x303A, 0x1001) };
        Require(bench.Prepare().Heading == "More than one board connected", "two boards");
        Require(bench.NothingWritten && !bench.Helper.Commands.Contains("identify"), "nothing read or written");
    }

    private static void ProductionLabel()
    {
        using var bench = new Bench();
        var workflow = bench.Prepare().Workflow!;
        Require(workflow.IsProduction && !workflow.IsFixture && workflow.SessionLabel.StartsWith("QUALIFICATION RELEASE", StringComparison.Ordinal), "label");
    }

    private static void BindingGeometry()
    {
        Require(QualificationInstall.BindingFor(new byte[8192]) is not null, "two sectors bind");
        Require(QualificationInstall.BindingFor(new byte[4095]) is null && QualificationInstall.BindingFor(new byte[0x4F1000]) is null, "partial or oversize refused");
        Require(QualificationInstall.TryParseChipIdentity("chip-md5:0x10000:604944:" + new string('A', 32), out var offset, out var length, out _) &&
                offset == 0x10000 && length == 604944, "identity parse");
        Require(!QualificationInstall.TryParseChipIdentity("chip-md5:0x10000:0:" + new string('A', 32), out _, out _, out _) &&
                !QualificationInstall.TryParseChipIdentity("md5:0x0:1:" + new string('A', 32), out _, out _, out _), "bad identity refused");
    }

    private static void PinsMatchFiles()
    {
        var root = RepositoryRoot ?? throw new InvalidOperationException("repository root not set");
        var helper = File.ReadAllBytes(Path.Combine(root, "tools", "flash-helper", "lu_flash_helper.py"));
        Require(Convert.ToHexString(SHA256.HashData(helper)) == BenchFlash.HelperSha256, "helper digest pin");
        Require(HostReleaseAdmission.Digest(ReleaseTrustAnchor.PublicKeySpki()) == ReleaseTrustAnchor.PublicKeySha256, "embedded key matches fingerprint");
        var lines = File.ReadAllLines(Path.Combine(root, ReleaseTrustAnchor.PublicKeyPemPath));
        Require(string.Concat(lines.Where(l => !l.StartsWith("-----", StringComparison.Ordinal))) == ReleaseTrustAnchor.PublicKeySpkiBase64, "embedded key equals PEM");
    }
}
