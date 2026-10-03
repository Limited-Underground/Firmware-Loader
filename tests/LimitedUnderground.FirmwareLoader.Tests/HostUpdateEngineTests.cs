using LimitedUnderground.FirmwareLoader;
using System.IO.Compression;
using System.Security.Cryptography;

internal static class HostUpdateEngineTests
{
    private static readonly RSA Key = RSA.Create(3072);
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("host admission verifies genuine RSA-PSS and freezes image bytes", AdmissionPositive),
        ("host admission refuses missing policy corrupt signature image target version signer and address", AdmissionNegative),
        ("S01 S02 production inspection stays untrusted after host engine work", ProductionDisabled),
        ("S03 no automatic selection or confirmation", NoSelection),
        ("S04 wrong model revision identity refuses before command", WrongSelection),
        ("S05 reconnect generation and identity invalidate confirmation", StaleSelection),
        ("S06 changed product file policy controller cannot reuse authority", StaleAuthority),
        ("host reselection revokes already issued confirmation", ReselectionRevokesConfirmation),
        ("S07 missing profile recovery or erase authority refuses", MissingProfile),
        ("S08 prewrite cancellation consumes authority without commands", CancelBefore),
        ("S09 concurrent duplicate confirmation submits one command", Duplicate),
        ("S10 disconnect after possible partial write is uncertain without retry", PartialFailure),
        ("S11 in-flight cancellation retains lease and discards late success", CancelDuring),
        ("S12 write acknowledgement waits for independent readback", ReadbackPending),
        ("S13 corrupt bytes and changed protected spans refuse boot", BadReadback),
        ("S14 verified bytes still await exact boot evidence", BootPending),
        ("S15 generic wrong or unhealthy boot cannot verify", BadBoot),
        ("S16 exact write readback boot sequence verifies host outcome", Complete),
        ("S17 restart unresolved marker requires fresh recovery review", Restart),
        ("S18 missing independent original refuses recovery", MissingRecovery),
        ("S19 S20 S21 separate original recovery verifies independently", Recovery),
        ("S22 failed recovery remains uncertain without recursive retry", RecoveryFailure),
        ("S23 exclusive owner refusal blocks competing engine", Exclusive),
        ("S24 side effects and boot require explicit approved profile", SideEffects),
        ("host phase total and backwards-clock deadlines fail closed", Deadlines),
        ("host hung command timeout retains ownership until actual completion", HungTimeout),
        ("host journal start failure prevents write completion failure keeps uncertainty", JournalFailures),
        ("host changed authority between readback and boot prevents new command", ChangedAfterRead),
        ("review same-target substituted bytes generation length and new file refuse", ArtifactBinding),
        ("review selection replacement at atomic adapter admission refuses every command", CommandSelectionBinding),
        ("review cancellation before terminal claim wins and after claim cannot erase outcome", CompletionCancellation),
    };
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Run(HostUpdateEngine engine, HostConfirmation? confirmation = null) => engine.RunAsync(confirmation ?? engine.Confirm()).GetAwaiter().GetResult();
    private static void State(Fixture f, HostUpdateState state) => Require(f.Engine.State == state, $"expected {state}, got {f.Engine.State}: {f.Engine.Refusal}");
    private static void Unknown(Fixture f) { State(f, HostUpdateState.OutcomeUnknown); Require(f.Journal.HasUnresolvedOperation, "uncertainty retained"); }

    private static void AdmissionPositive()
    {
        using var f = new Fixture();
        var admitted = f.Admit(); var saved = admitted.CopyImage(); f.Image[0] ^= 1;
        Require(admitted.CopyImage().SequenceEqual(saved), "admitted bytes immutable");
        var copy = admitted.CopyImage(); copy[0] ^= 1;
        Require(admitted.CopyImage().SequenceEqual(saved), "returned bytes copied");
    }
    private static void AdmissionNegative()
    {
        using var f = new Fixture();
        void Refuse(string reason, HostReleaseManifest? m = null, byte[]? image = null, byte[]? signature = null,
            HostAdmissionPolicy? p = null, byte[]? key = null, long now = 10)
        {
            var result = HostReleaseAdmission.Admit(m ?? f.Manifest, image ?? f.Image, signature ?? f.Signature,
                key ?? Key.ExportSubjectPublicKeyInfo(), p ?? f.Policy, now, out var actual);
            Require(result is null && actual == reason, $"first admission boundary {reason} != {actual}");
        }
        Require(HostReleaseAdmission.Admit(f.Manifest, f.Image, f.Signature, Key.ExportSubjectPublicKeyInfo(), null, 10, out _) is null, "missing trust");
        Refuse("manifest-binding", m: f.Manifest with { Target = "other" });
        Refuse("manifest-binding", m: f.Manifest with { Product = "opengauge" });
        Refuse("manifest-binding", m: f.Manifest with { Schema = 2 });
        Refuse("manifest-binding", m: f.Manifest with { Compatibility = 2 });
        Refuse("manifest-binding", m: f.Manifest with { Generation = 0 });
        Refuse("manifest-binding", p: f.Policy with { MinimumGeneration = 8 });
        Refuse("manifest-binding", m: f.Manifest with { LayoutDigest = new string('B', 64) });
        Refuse("signer-policy", p: f.Policy with { Revoked = true });
        Refuse("signer-policy", m: f.Manifest with { Signer = "unknown" });
        Refuse("signer-policy", p: f.Policy with { Revision = 2 });
        Refuse("signer-policy", p: f.Policy with { PublicKeyDigest = new string('0', 64) });
        Refuse("signer-policy", now: 100000);
        Refuse("image-digest", image: new byte[] { 8, 8, 8 });
        Refuse("address-plan", p: f.Policy with { EraseBlock = 0 });
        Refuse("address-plan", m: f.Manifest with { Offset = 1 });
        Refuse("address-plan", p: f.Policy with { AllowedLength = 4095 });
        Refuse("address-plan", p: f.Policy with { ProtectedOffset = 4097 });
        Refuse("address-plan", m: f.Manifest with { Offset = uint.MaxValue });
        var corrupt = (byte[])f.Signature.Clone(); corrupt[0] ^= 1; Refuse("signature", signature: corrupt);
        Refuse("signature", m: f.Manifest with { Generation = 2 });
        // A key substituted alongside a valid signature cannot appoint itself as trusted.
        using var other = RSA.Create(3072);
        Refuse("signer-policy", key: other.ExportSubjectPublicKeyInfo(),
            signature: other.SignData(f.Manifest.CanonicalBytes(), HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    }
    private static void ProductionDisabled()
    {
        using var f = new Fixture(); Run(f.Engine); State(f, HostUpdateState.Verified);
        Require(LoaderProductionProviders.SignerTrustPolicies.Count == 0, "production trust empty");
        Require(!f.Inspection.AdmissionAllowed && !f.Inspection.SignerTrusted &&
            !f.Controller.Snapshot.FirmwareWritingAvailable && !f.Controller.Snapshot.RecoveryAvailable, "production capabilities disabled");
    }
    private static void NoSelection() { using var f = new Fixture(select: false); Run(f.Engine); State(f, HostUpdateState.DeviceUnselected); Require(f.Adapter.Writes == 0, "no auto selection"); }
    private static void WrongSelection()
    {
        foreach (var selection in new[] { new HostSelection(new object(), 1, "heltec_v4_bench", "wrong revision", "host-fixture-v1"),
            new HostSelection(new object(), 0, "heltec_v4_bench", "HTIT-WB32LAF V4.2", "host-fixture-v1") })
        { using var f = new Fixture(select: false); f.Adapter.CurrentSelection = selection; Require(!f.Engine.Select(selection), "wrong selection refused"); Require(f.Adapter.Writes == 0, "no command"); }
    }
    private static void StaleSelection()
    {
        foreach (bool identity in new[] { false, true })
        { using var f = new Fixture(); var token = f.Engine.Confirm(); f.Adapter.CurrentSelection = identity ? f.Selection with { Identity = new object() } : f.Selection with { Generation = 2 }; Run(f.Engine, token); State(f, HostUpdateState.NoWriteStopped); Require(f.Adapter.Writes == 0, "stale selection"); }
    }
    private static void ReselectionRevokesConfirmation()
    {
        using var f = new Fixture();
        var old = f.Engine.Confirm();
        var next = f.Selection with { Identity = new object(), Generation = 2 };
        f.Adapter.CurrentSelection = next;
        Require(f.Engine.Select(next), "explicit new selection");
        Run(f.Engine, old);
        Require(f.Adapter.Writes == 0 && f.Engine.State == HostUpdateState.Ready, "old confirmation cannot target newly selected unit");
        Run(f.Engine); State(f, HostUpdateState.Verified);
    }
    private static void StaleAuthority()
    {
        using (var f = new Fixture()) { var token = f.Engine.Confirm(); f.Controller.SelectProduct("opengauge"); Run(f.Engine, token); State(f, HostUpdateState.NoWriteStopped); }
        using (var f = new Fixture()) { f.CurrentPolicy = f.Policy with { Revision = 2 }; Run(f.Engine); State(f, HostUpdateState.NoWriteStopped); }
        using (var f = new Fixture()) { var token = f.Engine.Confirm(); var newer = f.NewEngine(); Require(newer.Select(f.Selection), "new selection"); Run(newer, token); Require(f.Adapter.Writes == 0 && newer.State == HostUpdateState.Ready, "foreign file/operation token"); }
        using (var f = new Fixture()) { using var foreign = new LoaderSessionController(LoaderProductionProviders.Factories); foreign.SelectProduct("opentrail"); var e = f.NewEngine(controller: foreign); Require(!e.Select(f.Selection), "cross controller"); }
    }
    private static void MissingProfile()
    {
        using var f = new Fixture(select: false); var e = f.NewEngine(profile: f.Profile with { TotalMilliseconds = 0 }); Require(!e.Select(f.Selection), "unset timing");
        var bad = f.Manifest with { RecoveryDigest = "" };
        Require(HostReleaseAdmission.Admit(bad, f.Image, f.Signature, Key.ExportSubjectPublicKeyInfo(), f.Policy, 10, out _) is null, "missing original binding");
    }
    private static void CancelBefore() { using var f = new Fixture(); var token = f.Engine.Confirm(); f.Engine.Cancel(); Run(f.Engine, token); State(f, HostUpdateState.NoWriteStopped); Require(f.Adapter.Writes == 0, "cancel before command"); }
    private static void Duplicate()
    {
        using var f = new Fixture(); var pending = new TaskCompletionSource(); f.Adapter.Write = () => pending.Task;
        var token = f.Engine.Confirm(); var first = f.Engine.RunAsync(token); f.Engine.RunAsync(token).GetAwaiter().GetResult();
        Require(f.Adapter.Writes == 1, "exactly one write"); pending.SetResult(); first.GetAwaiter().GetResult(); State(f, HostUpdateState.Verified);
    }
    private static void PartialFailure() { using var f = new Fixture(); f.Adapter.Write = () => Task.FromException(new IOException("private device detail must not escape")); Run(f.Engine); Unknown(f); Require(f.Adapter.Writes == 1 && f.Adapter.Reads == 0 && f.Engine.Refusal == "adapter-or-authority-failure", "no retry or private diagnostics"); }
    private static void CancelDuring()
    {
        using var f = new Fixture(); var pending = new TaskCompletionSource(); f.Adapter.Write = () => pending.Task;
        var run = f.Engine.RunAsync(f.Engine.Confirm()); f.Engine.Cancel(); run.GetAwaiter().GetResult(); Unknown(f);
        Require(f.Lease.Held, "unquiesced lease retained"); pending.SetResult();
        Require(SpinWait.SpinUntil(() => !f.Lease.Held, 1000), "lease releases only after quiescence"); Unknown(f); Require(f.Adapter.Reads == 0, "late write cannot advance");
    }
    private static void ReadbackPending()
    {
        using var f = new Fixture(); var pending = new TaskCompletionSource<HostReadback>(); f.Adapter.Read = () => pending.Task;
        var run = f.Engine.RunAsync(f.Engine.Confirm()); State(f, HostUpdateState.Readback); Require(!f.Engine.BytesVerified, "ack not verification");
        pending.SetResult(new(f.Image, true)); run.GetAwaiter().GetResult(); State(f, HostUpdateState.Verified);
    }
    private static void BadReadback()
    {
        foreach (bool protect in new[] { true, false }) { using var f = new Fixture(); f.Adapter.Read = () => Task.FromResult(new HostReadback(protect ? new byte[] { 9 } : f.Image, protect)); Run(f.Engine); Unknown(f); Require(f.Adapter.Boots == 0, "bad readback prevents boot"); }
    }
    private static void BootPending()
    {
        using var f = new Fixture(); var pending = new TaskCompletionSource<HostBootResult>(); f.Adapter.Boot = () => pending.Task;
        var run = f.Engine.RunAsync(f.Engine.Confirm()); State(f, HostUpdateState.BootWait); Require(f.Engine.BytesVerified, "byte evidence retained");
        pending.SetResult(new(f.Manifest.ImageDigest, true, true)); run.GetAwaiter().GetResult(); State(f, HostUpdateState.Verified);
    }
    private static void BadBoot()
    {
        foreach (var mode in new[] { 0, 1, 2 }) { using var f = new Fixture(); f.Adapter.Boot = () => Task.FromResult(new HostBootResult(mode == 0 ? "heartbeat" : f.Manifest.ImageDigest, mode != 1, mode != 2)); Run(f.Engine); Unknown(f); Require(f.Engine.BytesVerified, "known bytes not lost"); }
    }
    private static void Complete() { using var f = new Fixture(); Run(f.Engine); State(f, HostUpdateState.Verified); Require(!f.Journal.HasUnresolvedOperation && !f.Lease.Held && f.Adapter.Writes == 1 && f.Adapter.Reads == 1 && f.Adapter.Boots == 1, "complete release and evidence"); }
    private static void Restart() { using var f = new Fixture(); f.Journal.MarkUncertain(f.Manifest.RecoveryDigest); var e = f.NewEngine(); Require(e.State == HostUpdateState.RecoveryReview && e.Confirm() is null && !e.Select(f.Selection), "restart cannot resume"); }
    private static void MissingRecovery() { using var f = new Fixture(); f.Journal.MarkUncertain(new string('B', 64)); var e = f.NewEngine(recovery: true); Require(!e.Select(f.Selection) && e.State == HostUpdateState.RecoveryReview, "unbound original refused"); }
    private static void Recovery()
    {
        using var f = new Fixture(); var updateToken = f.Engine.Confirm(); f.Journal.MarkUncertain(f.Manifest.ImageDigest);
        var e = f.NewEngine(recovery: true); Require(e.Select(f.Selection), "original selected"); Run(e, updateToken); Require(f.Adapter.Writes == 0, "update token no recovery authority");
        Run(e); Require(e.State == HostUpdateState.Recovered && !f.Journal.HasUnresolvedOperation && f.Adapter.Reads == 1 && f.Adapter.Boots == 1, "independent original verified");
    }
    private static void RecoveryFailure() { using var f = new Fixture(); f.Journal.MarkUncertain(f.Manifest.ImageDigest); var e = f.NewEngine(recovery: true); Require(e.Select(f.Selection), "recovery selection"); f.Adapter.Write = () => Task.FromException(new IOException()); Run(e); Require(e.State == HostUpdateState.OutcomeUnknown && f.Adapter.Writes == 1 && f.Journal.HasUnresolvedOperation, "no recursive recovery"); }
    private static void Exclusive() { using var f = new Fixture(); using var held = f.Lease.TryAcquire(); Run(f.Engine); State(f, HostUpdateState.NoWriteStopped); Require(f.Adapter.Writes == 0, "no competing command"); }
    private static void SideEffects() { using var f = new Fixture(select: false); foreach (var p in new[] { f.Profile with { SideEffectsApproved = false }, f.Profile with { BootApproved = false } }) Require(!f.NewEngine(profile: p).Select(f.Selection), "side effects refuse before open"); }
    private static void Deadlines()
    {
        foreach (var time in new long[] { 1010, 3010, 9 }) { using var f = new Fixture(); f.Adapter.Write = () => { f.Now = time; return Task.CompletedTask; }; Run(f.Engine); Unknown(f); Require(f.Adapter.Reads == 0 && f.Engine.Refusal == "deadline", "deadline first boundary"); }
    }
    private static void HungTimeout()
    {
        using var f = new Fixture(select: false); var e = f.NewEngine(profile: f.Profile with { WriteMilliseconds = 20 }); Require(e.Select(f.Selection), "selected");
        var pending = new TaskCompletionSource(); f.Adapter.Write = () => pending.Task; Run(e);
        Require(e.State == HostUpdateState.OutcomeUnknown && f.Lease.Held, "timeout cannot release running transport"); pending.SetResult();
        Require(SpinWait.SpinUntil(() => !f.Lease.Held, 1000) && e.State == HostUpdateState.OutcomeUnknown, "late completion release only");
    }
    private static void JournalFailures()
    {
        using (var f = new Fixture()) { f.Journal.StartWorks = false; Run(f.Engine); State(f, HostUpdateState.NoWriteStopped); Require(f.Adapter.Writes == 0, "marker before write"); }
        using (var f = new Fixture()) { f.Journal.CompleteWorks = false; Run(f.Engine); Unknown(f); }
    }
    private static void ChangedAfterRead() { using var f = new Fixture(); f.Adapter.Read = () => { f.CurrentPolicy = f.Policy with { Revoked = true }; return Task.FromResult(new HostReadback(f.Image, true)); }; Run(f.Engine); Unknown(f); Require(f.Adapter.Boots == 0, "changed authority stops next command"); }

    private static void ArtifactBinding()
    {
        using var f = new Fixture(select: false);
        using var changedBytes = new Fixture(select: false, image: new byte[] { 2, 4, 6, 8 });
        using var changedLength = new Fixture(select: false, image: new byte[] { 1, 3, 5 });
        using var changedGeneration = new Fixture(select: false, generation: 2);
        foreach (var substitute in new[] { changedBytes, changedLength, changedGeneration })
            Require(!f.NewEngine(release: substitute.Admit()).Select(f.Selection), "inspected artifact cannot admit same-target substitution");
        Require(f.Engine.Select(f.Selection), "original selected");
        var confirmation = f.Engine.Confirm();
        f.CurrentInspection = changedBytes.Inspection;
        Run(f.Engine, confirmation); State(f, HostUpdateState.NoWriteStopped);
        Require(f.Adapter.Writes == 0, "changed file invalidates issued confirmation");
    }
    private static void CommandSelectionBinding()
    {
        foreach (int phase in new[] { 0, 1, 2 })
        {
            using var f = new Fixture(); int calls = 0;
            f.Adapter.AdmissionHook = () =>
            {
                if (calls++ == phase) f.Adapter.CurrentSelection = f.Selection with { Identity = new object(), Generation = 2 };
            };
            Run(f.Engine); Unknown(f);
            Require(f.Adapter.Writes == (phase > 0 ? 1 : 0) && f.Adapter.Reads == (phase > 1 ? 1 : 0) &&
                f.Adapter.Boots == 0, "replacement after last engine sample must not execute a command on replacement");
        }
    }
    private static void CompletionCancellation()
    {
        using (var f = new Fixture())
        {
            f.Adapter.Boot = () => { f.Engine.Cancel(); return Task.FromResult(new HostBootResult(f.Manifest.ImageDigest, true, true)); };
            Run(f.Engine); Unknown(f);
        }
        using (var f = new Fixture())
        {
            f.Journal.OnComplete = f.Engine.Cancel;
            Run(f.Engine); State(f, HostUpdateState.Verified);
            Require(!f.Journal.HasUnresolvedOperation, "terminal claim before callback wins cancellation ordering");
        }
        using (var f = new Fixture())
        {
            f.Journal.OnComplete = f.Engine.Cancel; f.Journal.CompleteWorks = false;
            Run(f.Engine); Unknown(f);
        }
    }

    internal sealed class Fixture : IDisposable
    {
        internal byte[] Image = { 1, 3, 5, 7 };
        internal HostReleaseManifest Manifest;
        internal HostAdmissionPolicy Policy;
        internal HostAdmissionPolicy? CurrentPolicy;
        internal byte[] Signature;
        internal long Now = 10;
        internal HostAdapterProfile Profile = new(1000, 1000, 1000, 3000, 256, true, true);
        internal HostSelection Selection = new(new object(), 1, "heltec_v4_bench", "HTIT-WB32LAF V4.2", "host-fixture-v1");
        internal LoaderSessionController Controller = new(LoaderProductionProviders.Factories);
        internal LoaderBundleInspectionContext Context;
        internal FirmwareBundleCandidateResult Inspection;
        internal FirmwareBundleCandidateResult? CurrentInspection;
        internal FakeAdapter Adapter = new();
        internal FakeLease Lease = new();
        internal FakeJournal Journal = new();
        internal HostUpdateEngine Engine;
        internal Fixture(bool select = true, byte[]? image = null, ulong generation = 1)
        {
            if (image is not null) Image = image;
            var hash = new string('A', 64);
            Policy = new(hash, hash, 1, 1, "test-only", HostReleaseAdmission.Digest(Key.ExportSubjectPublicKeyInfo()), 1, 100000, false, 4096, 8192, 4096, 16384, 4096, hash);
            CurrentPolicy = Policy;
            Manifest = new(1, "opentrail", "heltec_v4_bench", hash, hash, 1, generation, "test-only", 1, HostReleaseAdmission.Digest(Image), Image.Length, 4096, hash);
            Signature = Key.SignData(Manifest.CanonicalBytes(), HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            Controller.SelectProduct("opentrail"); Require(Controller.TryCreateOfflineBundleInspectionContext(out var context), "inspection context"); Context = context!;
            using var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                void Put(string name, byte[] bytes) { using var entry = zip.CreateEntry(name).Open(); entry.Write(bytes); }
                Put("manifest.json", FirmwareBundleCandidateInspector.SerializeCanonicalManifest("opentrail", "heltec_v4_bench", generation, (uint)Image.Length, HostReleaseAdmission.Digest(Image).ToLowerInvariant(), "1111111111111111"));
                Put("image.bin", Image); Put("manifest.sig", Enumerable.Repeat((byte)0x5a, 384).ToArray());
            }
            stream.Position = 0; Inspection = FirmwareBundleCandidateInspector.Inspect(stream, Context); CurrentInspection = Inspection;
            Adapter.CurrentSelection = Selection;
            Adapter.Read = () => Task.FromResult(new HostReadback((byte[])Image.Clone(), true));
            Adapter.Boot = () => Task.FromResult(new HostBootResult(Manifest.ImageDigest, true, true));
            Engine = NewEngine(); if (select) Require(Engine.Select(Selection), "valid host selection");
        }
        internal HostAdmittedRelease Admit() => HostReleaseAdmission.Admit(Manifest, Image, Signature, Key.ExportSubjectPublicKeyInfo(), Policy, Now, out var reason) ?? throw new InvalidOperationException(reason);
        internal HostUpdateEngine NewEngine(bool recovery = false, HostAdapterProfile? profile = null, LoaderSessionController? controller = null, HostAdmittedRelease? release = null) =>
            new(controller ?? Controller, Context, Inspection, () => CurrentInspection, release ?? Admit(), Policy, () => CurrentPolicy, Adapter, Lease, Journal, () => Now, profile ?? Profile, recovery);
        public void Dispose() => Controller.Dispose();
    }
    internal sealed class FakeAdapter : IHostUpdateAdapter
    {
        public HostSelection? CurrentSelection { get; set; }
        internal int Writes, Reads, Boots;
        internal Action? AdmissionHook;
        private void CheckExpected(HostSelection expected)
        {
            if (CurrentSelection != expected || !ReferenceEquals(CurrentSelection.Identity, expected.Identity))
                throw new InvalidOperationException("selection-replaced-before-command");
        }
        internal Func<Task> Write = () => Task.CompletedTask;
        internal Func<Task<HostReadback>> Read = () => Task.FromResult(new HostReadback(Array.Empty<byte>(), false));
        internal Func<Task<HostBootResult>> Boot = () => Task.FromResult(new HostBootResult("", false, false));
        public Task WriteAsync(HostSelection expected, uint offset, byte[] image, int maximumChunk, CancellationToken cancellation) { AdmissionHook?.Invoke(); CheckExpected(expected); Writes++; return Write(); }
        public Task<HostReadback> ReadbackAsync(HostSelection expected, uint offset, int length, CancellationToken cancellation) { AdmissionHook?.Invoke(); CheckExpected(expected); Reads++; return Read(); }
        public Task<HostBootResult> BootAsync(HostSelection expected, CancellationToken cancellation) { AdmissionHook?.Invoke(); CheckExpected(expected); Boots++; return Boot(); }
    }
    internal sealed class FakeLease : IHostOperationLease
    {
        private int held;
        internal bool Held => Volatile.Read(ref held) != 0;
        public IDisposable? TryAcquire() => Interlocked.CompareExchange(ref held, 1, 0) == 0 ? new Lease(this) : null;
        private sealed class Lease(FakeLease owner) : IDisposable { public void Dispose() => Interlocked.Exchange(ref owner.held, 0); }
    }
    internal sealed class FakeJournal : IHostUncertaintyJournal
    {
        public bool HasUnresolvedOperation { get; private set; }
        public string? RecoveryDigest { get; private set; }
        internal bool StartWorks = true, CompleteWorks = true;
        internal Action? OnComplete;
        public bool MarkUncertain(string digest) { if (!StartWorks) return false; HasUnresolvedOperation = true; RecoveryDigest = digest; return true; }
        public bool MarkComplete() { OnComplete?.Invoke(); if (!CompleteWorks) return false; HasUnresolvedOperation = false; return true; }
    }
}
