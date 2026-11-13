using System.IO;
using System.IO.Compression;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;

namespace LimitedUnderground.FirmwareLoader;

// L7 step 5 (D-063, D-064, D-065): production wiring for the one board A qualification release.
// Trust comes only from the pinned ReleaseTrustAnchor through full production admission; the
// bundle-inspection trust list stays empty. Nothing here writes to a board.
internal sealed record QualificationReleaseFiles(byte[] Manifest, byte[] Signature, byte[] Raw, byte[] Padded)
{
    internal static QualificationReleaseFiles? TryRead(string? folder)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return null;
            byte[]? Read(string name, int maximum)
            {
                var path = Path.Combine(folder, name);
                var info = new FileInfo(path);
                if (!info.Exists || info.Length <= 0 || info.Length > maximum ||
                    (info.Attributes & FileAttributes.ReparsePoint) != 0) return null;
                return File.ReadAllBytes(path);
            }
            var manifest = Read("release.json", 16384);
            var signature = Read("release.sig", 384);
            var raw = Read("raw.bin", 16 * 1024 * 1024);
            var padded = Read("padded.bin", 16 * 1024 * 1024);
            return manifest is null || signature is null || raw is null || padded is null ? null :
                new(manifest, signature, raw, padded);
        }
        catch (Exception) { return null; }
    }
}

internal static class QualificationInstall
{
    internal const ulong MinimumGeneration = 2;

    // The Loader-side policy. Values are fixed here; only the expiry and the recovery image come
    // from the signed record, which production admission has verified against the pinned key.
    internal static HostAdmissionPolicy PolicyFor(ProductionReleaseManifest manifest, string keyDigest) => new(
        QualificationReleaseBuilder.TargetDigest, QualificationReleaseBuilder.LayoutDigest, QualificationReleaseBuilder.Compatibility,
        MinimumGeneration, ReleaseTrustAnchor.SignerRole, keyDigest, ReleaseTrustAnchor.TrustRevision,
        manifest.ExpiresAt, false, BenchFlash.AppOffset, BenchFlash.AppEnd - BenchFlash.AppOffset, BenchFlash.Sector,
        0, QualificationReleaseBuilder.ProtectedLength, manifest.Text("recoveryImageDigest"));

    // Real 16 MB bench geometry: only the factory app partition may be written.
    internal static HostPreservationPayloadBinding? BindingFor(byte[] padded)
    {
        if (padded.Length == 0 || padded.Length % (int)BenchFlash.Sector != 0 ||
            (ulong)padded.Length > BenchFlash.AppEnd - BenchFlash.AppOffset) return null;
        if (!HostPreservationPlan.TryCreate(BenchFlash.FlashSize, BenchFlash.Sector,
                [new(BenchFlash.AppOffset, BenchFlash.AppEnd - BenchFlash.AppOffset)],
                [new(0, BenchFlash.AppOffset), new(BenchFlash.AppEnd, BenchFlash.FlashSize - BenchFlash.AppEnd)],
                [new(BenchFlash.AppOffset, (ulong)padded.Length)], out var plan) || plan is null) return null;
        return HostPreservationPayloadBinding.TryCreate(plan, [new(BenchFlash.AppOffset, padded)], out var binding) ? binding : null;
    }

    // The engine re-checks a bundle inspection before every command. The bundle is built here from
    // the admitted record; inspection never trusts it, the signed record carries the authority.
    internal static byte[] BundleFor(ProductionReleaseManifest manifest, byte[] padded, byte[] signature)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            void Put(string name, byte[] bytes) { using var entry = zip.CreateEntry(name).Open(); entry.Write(bytes); }
            Put("manifest.json", FirmwareBundleCandidateInspector.SerializeCanonicalManifest("opentrail", BenchFlash.Target,
                manifest.Number("generation"), checked((uint)padded.Length), HostReleaseAdmission.Digest(padded).ToLowerInvariant(),
                manifest.Text("publicKeyFingerprint")[..16].ToLowerInvariant()));
            Put("image.bin", padded);
            Put("manifest.sig", signature);
        }
        return stream.ToArray();
    }

    // "chip-md5:0x<offset>:<length>:<MD5>" as written by the release builder.
    internal static bool TryParseChipIdentity(string text, out uint offset, out uint length, out string md5)
    {
        offset = 0; length = 0; md5 = "";
        var parts = text.Split(':');
        if (parts.Length != 4 || parts[0] != "chip-md5" || !parts[1].StartsWith("0x", StringComparison.Ordinal) ||
            !uint.TryParse(parts[1][2..], System.Globalization.NumberStyles.AllowHexSpecifier, null, out offset) ||
            !uint.TryParse(parts[2], System.Globalization.NumberStyles.None, null, out length) || length == 0 ||
            parts[3].Length != 32 || !parts[3].All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F')) return false;
        md5 = parts[3];
        return (ulong)offset + length <= BenchFlash.FlashSize;
    }

    // One read-only snapshot of the board, taken before admission. It is never re-read during the
    // attempt (the write itself changes the app), so the engine compares against this snapshot.
    internal static ProductionCurrentState ObservedState(ProductionReleaseManifest manifest, string? bootMd5, string? appMd5, string helperSha256)
    {
        string Observe(string required, string? actual) =>
            TryParseChipIdentity(required, out _, out _, out var md5) && actual == md5 ? required : "chip-md5:mismatch";
        string boot = Observe(manifest.Text("requiredBootloader"), bootMd5);
        string firmware = Observe(manifest.Text("requiredCurrentFirmware"), appMd5);
        // The signed record names the exact app the board must run and that app's generation
        // (minimum = maximum). When the chip checksum proves the board runs that app, the board is at
        // that generation (I-037): generation 1 for the pre-L7 bench image, 2 after L7, and so on.
        ulong expected = manifest.Number("minimumCurrentGeneration");
        ulong generation = firmware == manifest.Text("requiredCurrentFirmware") &&
            expected == manifest.Number("maximumCurrentGeneration") ? expected : 0;
        return new(ProductionReleaseManifest.Profile, boot, firmware, generation,
            HostReleaseAdmission.Digest(QualificationReleaseBuilder.WriterProfile(helperSha256)),
            HostReleaseAdmission.Digest(QualificationReleaseBuilder.PreservationPlan()),
            HostReleaseAdmission.Digest(QualificationReleaseBuilder.RecoveryRequirements()));
    }

    internal static HostAdapterProfile Profile()
    {
        var limits = BenchFlash.Limits;
        int write = (int)(limits.Write.TotalMilliseconds + 2 * limits.Digests.TotalMilliseconds);
        int read = (int)limits.Digests.TotalMilliseconds, boot = (int)limits.Boot.TotalMilliseconds;
        return new(write, read, boot, write + read + boot, 65536, true, true);
    }
}

// D-065 part 2: the one allowed board is named only on this PC, as the SHA-256 of its MAC, in an
// administrator-only folder next to the machine operation store. The admission clock lives there too.
internal sealed class QualificationBoardPin
{
    internal static string RootPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "LimitedUnderground.FirmwareLoader.Qualification");
    private readonly string root;
    private readonly bool protect;
    // protect: create the folder administrator-only (production). Host tests use a plain temporary folder.
    internal QualificationBoardPin(string root, bool protect = true) { this.root = Path.GetFullPath(root); this.protect = protect; }
    private string PinPath => Path.Combine(root, "board-a.sha256");
    internal string StatePath => Path.Combine(root, "production-admission.state");
    private string PythonPinPath => Path.Combine(root, "helper-python.sha256");

    internal static string MacDigest(string mac) =>
        Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes("LU-QUALIFICATION-BOARD-V1\n" + mac.Trim().ToUpperInvariant())));

    internal bool IsPinned => File.Exists(PinPath);

    internal bool Matches(string? mac)
    {
        try
        {
            if (mac is null || mac.Length != 17) return false;
            var stored = File.ReadAllText(PinPath, Encoding.ASCII);
            return stored.Length == 65 && stored[64] == '\n' &&
                CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(stored[..64]), Encoding.ASCII.GetBytes(MacDigest(mac)));
        }
        catch (Exception) { return false; }
    }

    // D-065 part 1: the SHA-256 of this PC's ESP-IDF python.exe, recorded when board A is set.
    internal string? PythonSha256()
    {
        try
        {
            var text = File.ReadAllText(PythonPinPath, Encoding.ASCII);
            return text.Length == 65 && text[64] == '\n' && text[..64].All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F') ? text[..64] : null;
        }
        catch (Exception) { return null; }
    }

    // Once only; refuses an existing pin (replacing board A is a deliberate manual step).
    internal bool TryPin(string mac, string pythonSha256)
    {
        try
        {
            if (mac.Length != 17 || pythonSha256.Length != 64 || File.Exists(PinPath)) return false;
            if (protect) new DirectoryInfo(root).Create(MachineRecoveryStorePolicy.DirectoryPolicy());
            else Directory.CreateDirectory(root);
            using (var python = new FileStream(PythonPinPath, FileMode.Create, FileAccess.Write, FileShare.None))
            { python.Write(Encoding.ASCII.GetBytes(pythonSha256.ToUpperInvariant() + "\n")); python.Flush(true); }
            using (var pin = new FileStream(PinPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { pin.Write(Encoding.ASCII.GetBytes(MacDigest(mac) + "\n")); pin.Flush(true); }
            if (!File.Exists(StatePath))
            {
                using var state = new FileStream(StatePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                state.Write(Encoding.ASCII.GetBytes("T-0043\n0\n0\n")); state.Flush(true);
            }
            return true;
        }
        catch (Exception) { return false; }
    }

    internal ProductionAdmissionState? AdmissionState() => File.Exists(StatePath) ? new ProductionAdmissionState(StatePath) : null;
}

// The bench board as the install path needs it. Implemented by the reviewed writer adapter.
internal interface IBenchBoard : IHostUpdateAdapter
{
    Task<IReadOnlyList<string>> FindBoardsAsync(CancellationToken cancellation);
    Task<HostSelection> SelectAsync(string port, CancellationToken cancellation);
    Task<string?> ChipMd5Async(HostSelection selection, uint offset, uint length, CancellationToken cancellation);
    string? MacOf(HostSelection selection);
}

internal sealed record QualificationPrepared(LoaderUpdateWorkflow? Workflow, string Heading, string Detail);

// Prepares one attempt. Every step refuses with a plain reason; only the workflow's own confirmed
// Start writes to the board.
internal static class QualificationInstaller
{
    internal static async Task<QualificationPrepared> PrepareAsync(LoaderSessionController controller, string folder,
        IBenchBoard board, QualificationBoardPin pin, Func<HostOperationStore> openStore, string helperSha256,
        string pythonSha256, byte[] trustedKey, Func<long> milliseconds, Func<bool> confirmPinThisBoard, CancellationToken cancellation)
    {
        static QualificationPrepared No(string heading, string detail) => new(null, heading, detail);
        var files = QualificationReleaseFiles.TryRead(folder);
        if (files is null) return No("Release folder not usable", "Choose the folder made by the release builder and signed by the owner (release.json, release.sig, raw.bin, padded.bin).");
        if (!ProductionReleaseManifest.TryParse(files.Manifest, out var manifest) || manifest is null)
            return No("Release record not accepted", "release.json is not a valid release record.");
        string keyDigest = HostReleaseAdmission.Digest(trustedKey);
        if (manifest.Text("publicKeyFingerprint") != keyDigest)
            return No("Release not signed for this Loader", "The record names a different signing key.");

        var ports = await board.FindBoardsAsync(cancellation).ConfigureAwait(true);
        if (ports.Count != 1) return No(ports.Count == 0 ? "No board found" : "More than one board connected",
            "Connect exactly one Heltec V4 board by USB, then try again.");
        var selection = await board.SelectAsync(ports[0], cancellation).ConfigureAwait(true);
        var mac = board.MacOf(selection);
        if (!pin.IsPinned)
        {
            if (mac is null || !confirmPinThisBoard() || !pin.TryPin(mac, pythonSha256))
                return No("Board A not set", "The qualification release installs only on board A. Board A was not set on this PC.");
        }
        if (!pin.Matches(mac)) return No("Not board A", "This qualification release installs only on board A, and the connected board is a different one. Nothing was written.");

        if (!QualificationInstall.TryParseChipIdentity(manifest.Text("requiredBootloader"), out var bootOffset, out var bootLength, out _) ||
            !QualificationInstall.TryParseChipIdentity(manifest.Text("requiredCurrentFirmware"), out var appOffset, out var appLength, out _))
            return No("Release record not accepted", "The record's required board state is not readable.");
        var bootMd5 = await board.ChipMd5Async(selection, bootOffset, bootLength, cancellation).ConfigureAwait(true);
        var appMd5 = await board.ChipMd5Async(selection, appOffset, appLength, cancellation).ConfigureAwait(true);
        var observed = QualificationInstall.ObservedState(manifest, bootMd5, appMd5, helperSha256);

        var clock = pin.AdmissionState();
        var policy = QualificationInstall.PolicyFor(manifest, keyDigest);
        var admitted = ProductionReleaseAdmission.Admit(files.Manifest, files.Raw, files.Padded, files.Signature,
            trustedKey, policy, observed, clock, milliseconds(), out var refusal);
        if (admitted is null) return No("Release not admitted", refusal switch
        {
            "production-binding" => "The board is not running the firmware this release expects, or the release files do not match the record. Nothing was written.",
            "signature" => "The signature does not match the owner's key. Nothing was written.",
            "persisted-clock-or-policy" => "This PC's clock is earlier than a previous admission. Nothing was written.",
            _ => "The release did not pass admission (" + refusal + "). Nothing was written.",
        });

        if (!controller.TryCreateOfflineBundleInspectionContext(out var context) || context is null)
            return No("Product not selected", "Choose Limited Underground Trail first.");
        FirmwareBundleCandidateResult inspection;
        using (var bundle = new MemoryStream(QualificationInstall.BundleFor(manifest, files.Padded, files.Signature)))
            inspection = FirmwareBundleCandidateInspector.Inspect(bundle, context);
        var binding = QualificationInstall.BindingFor(files.Padded);
        if (binding is null) return No("Release image not usable", "The padded image does not fit the app partition.");
        var store = openStore();
        var profile = QualificationInstall.Profile();

        HostUpdateEngine Factory(bool recovery, HostSelection _)
        {
            if (recovery) throw new InvalidOperationException("Restoring the previous firmware uses its own signed release (L8).");
            if (!ProductionUpdateComposition.TryCreate(controller, context, inspection, () => inspection, admitted, binding,
                    () => policy, () => observed, clock, board, store, milliseconds, profile, out var engine) || engine is null)
                throw new InvalidOperationException("composition refused");
            return engine;
        }
        var workflow = new LoaderUpdateWorkflow([selection], Factory, production: true);
        return new(workflow, "Board A ready", "Review the release and board, confirm once, then start. The Loader writes the app, checks the chip's checksum of every region and waits for the board to start.");
    }
}
