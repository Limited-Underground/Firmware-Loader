using LimitedUnderground.FirmwareLoader;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Security.AccessControl;
using System.Security.Principal;

internal static class HostOperationStoreTests
{
    private static readonly string Recovery = new('A', 64);
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("durable store requires explicit fresh isolated initialization", Initialization),
        ("durable marker requires live ownership and exact operation/recovery binding", MarkerBinding),
        ("shared store reloads after lock and refuses foreign completion", FreshReload),
        ("actual host engine rejects a marker changed between admission and lock", AdmissionLockRace),
        ("missing and corrupt initialized journals refuse without recreation", BrokenJournal),
        ("missing stable lock and noncanonical path aliases refuse", PathRefusal),
        ("hard-linked journal alias refuses", HardLinkRefusal),
        ("directory junction aliases refuse canonical store authority", JunctionRefusal),
        ("journal substitution after ownership refuses first marker without repair", JournalSubstitution),
        ("live stable lock refuses rename and deletion until release", LockNameSubstitution),
        ("hard link introduced at readback prevents submission and retains durable uncertainty", ReadbackHardLink),
        ("journal OS ACL denial preserves unresolved intent and refuses repair", JournalAclDenial),
        ("stable lock OS ACL denial refuses ownership without recreation", LockAclDenial),
        ("root OS ACL creation denial prevents durable intent commit", RootAclDenial),
        ("durable journal contains only bounded opaque digests and state", Privacy),
        ("actual host engine verifies using shared durable lease and journal", EngineComplete),
        ("journal write flush replace readback failures prevent adapter submission", StartFailures),
        ("terminal persistence failures preserve uncertain engine outcome", CompletionFailures),
        ("actual terminal root creation denial retains uncertainty after verified fixture commands", TerminalRootAclDenial),
        ("separate process contender cannot acquire a live lease", ProcessExclusion),
        ("hard kill releases ownership but retains durable recovery requirement", CrashRestart),
        ("kill before atomic replacement cannot submit a command", CrashBeforeReplace),
        ("kill after replacement reloads unresolved marker", CrashAfterReplace),
        ("actual engine submission has durable marker before subprocess kill", EngineCrash),
        ("actual cancellation keeps competing processes out until command quiescence", CancelQuiescence),
        ("actual timeout keeps competing processes out until command quiescence", TimeoutQuiescence),
        ("restart recovery requires exact digest and fresh confirmation", RecoveryBinding),
        ("pre-submission withdrawal restores exact prior record and never clears earlier uncertainty", Withdrawal),
        ("actual engine withdraws its durable marker when stopped before any command", EngineWithdrawal),
        ("withdrawal persistence failures stay conservative and honest on restart", WithdrawalFailures),
    };
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    private static string Journal(string root) => Path.Combine(root, HostOperationStore.JournalName);
    private static HostUpdateEngine Engine(HostUpdateEngineTests.Fixture f, HostOperationStore store,
        bool recovery = false, HostAdapterProfile? profile = null) =>
        new(f.Controller, f.Context, f.Inspection, () => f.CurrentInspection, f.Admit(), f.Policy,
            () => f.CurrentPolicy, f.Adapter, store, store, () => f.Now, profile ?? f.Profile, recovery);
    private static void Run(HostUpdateEngine engine) => engine.RunAsync(engine.Confirm()).GetAwaiter().GetResult();

    private static void Initialization()
    {
        using var t = new TemporaryStore();
        Require(!t.Store.HasUnresolvedOperation && t.Store.RecoveryDigest is null, "fresh idle checkpoint");
        bool refused = false;
        try { HostOperationStore.CreateIsolatedTestStore(t.Root); }
        catch (InvalidOperationException e) { refused = e.Message == "host-store-unavailable"; }
        Require(refused, "existing root cannot be initialized again");
        string absent = Path.Combine(Path.GetTempPath(), "LUF.HostStore.Tests." + Guid.NewGuid().ToString("N"));
        try { _ = new HostOperationStore(absent); Require(false, "missing root refused"); }
        catch (InvalidOperationException e) { Require(e.Message == "host-store-unavailable" && !Directory.Exists(absent), "ordinary open cannot create root"); }
    }
    private static void MarkerBinding()
    {
        using var t = new TemporaryStore();
        Require(!t.Store.MarkUncertain(Recovery) && !t.Store.MarkComplete(), "no ownership no marks");
        var lease = t.Store.TryAcquire(); Require(lease is not null, "lease acquired");
        Require(!t.Store.MarkUncertain("private-device-not-a-digest") && t.Store.MarkUncertain(Recovery), "digest-only start");
        Require(!t.Store.MarkUncertain(Recovery), "one marker per owned operation");
        string original = File.ReadAllText(Journal(t.Root));
        File.WriteAllText(Journal(t.Root), original.Replace(Recovery, new string('B', 64), StringComparison.Ordinal));
        Require(!t.Store.MarkComplete(), "another recovery cannot be cleared by old operation");
        File.WriteAllText(Journal(t.Root), original);
        using (var json = JsonDocument.Parse(original))
        {
            string operation = json.RootElement.GetProperty("operationDigest").GetString()!;
            File.WriteAllText(Journal(t.Root), original.Replace(operation, new string('D', 64), StringComparison.Ordinal));
            Require(!t.Store.MarkComplete(), "foreign operation cannot be cleared");
            File.WriteAllText(Journal(t.Root), original);
        }
        Require(t.Store.MarkComplete(), "exact live binding completes");
        lease!.Dispose(); Require(!t.Store.MarkComplete(), "disposed operation cannot mark");
        using var second = t.Store.TryAcquire(); Require(second is not null, "new lease");
        lease.Dispose();
        using var other = new HostOperationStore(t.Root).TryAcquire();
        Require(other is null, "old lease disposal cannot release new handle");
    }
    private static void FreshReload()
    {
        using var t = new TemporaryStore(); var earlier = new HostOperationStore(t.Root);
        using (var held = t.Store.TryAcquire())
        {
            Require(held is not null && t.Store.MarkUncertain(Recovery), "original durable marker");
            Require(earlier.TryAcquire() is null && !earlier.MarkComplete(), "foreign instance cannot clear");
        }
        using var own = earlier.TryAcquire();
        Require(own is not null && earlier.HasUnresolvedOperation && earlier.RecoveryDigest == Recovery, "fresh post-lock unresolved state");
        Require(!earlier.MarkComplete() && !earlier.MarkUncertain(new string('B', 64)), "no inherited operation or unrelated recovery");
        Require(earlier.MarkUncertain(Recovery) && earlier.MarkComplete(), "new exact recovery operation");
    }
    private static void BrokenJournal()
    {
        foreach (string? contents in new string?[] { null, "", "{", "{}", new string('x', 1025),
            "{\"schema\":1,\"storeDigest\":\"x\",\"state\":\"idle\",\"operationDigest\":null,\"recoveryDigest\":null}" })
        {
            using var t = new TemporaryStore(); string original = File.ReadAllText(Journal(t.Root));
            if (contents is null) File.Delete(Journal(t.Root)); else File.WriteAllText(Journal(t.Root), contents);
            var reopened = new HostOperationStore(t.Root);
            Require(reopened.HasUnresolvedOperation && reopened.RecoveryDigest is null && reopened.TryAcquire() is null,
                "missing/corrupt store cannot acquire or auto-clear");
            Require(!reopened.MarkUncertain(Recovery) && !reopened.MarkComplete(), "corruption refuses marks");
            Require(t.Store.HasUnresolvedOperation, "existing instance detects disappearance");
            File.WriteAllText(Journal(t.Root), original);
            Require(t.Store.TryAcquire() is null && reopened.TryAcquire() is null, "poisoned instance cannot self-repair");
        }
        using (var t = new TemporaryStore())
        {
            string original = File.ReadAllText(Journal(t.Root));
            using (var inaccessible = new FileStream(Journal(t.Root), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Require(new HostOperationStore(t.Root).TryAcquire() is null, "unreadable journal refuses");
            string store = JsonDocument.Parse(original).RootElement.GetProperty("storeDigest").GetString()!;
            File.WriteAllText(Journal(t.Root), original.Replace(store, new string('C', 64), StringComparison.Ordinal));
            Require(t.Store.HasUnresolvedOperation && t.Store.TryAcquire() is null, "observed store identity cannot be swapped");
        }
        foreach (bool schema in new[] { false, true })
        {
            using var t = new TemporaryStore(); string original = File.ReadAllText(Journal(t.Root));
            File.WriteAllText(Journal(t.Root), schema ? original.Replace("\"schema\":1", "\"schema\":2", StringComparison.Ordinal) : " " + original);
            Require(t.Store.HasUnresolvedOperation && t.Store.TryAcquire() is null, "unknown schema and noncanonical record refuse");
        }
    }
    private static FileSystemAccessRule DenyCurrent(FileSystemRights rights) =>
        new(WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("test account unavailable"),
            rights, AccessControlType.Deny);

    // Windows normalizes descriptor control flags on write, so restoration is verified by rules, not raw bytes.
    private static string[] AccessRules(DirectorySecurity acl) =>
        acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>()
            .Select(rule => string.Join('|', rule.IdentityReference.Value, rule.AccessControlType,
                (int)rule.FileSystemRights, rule.InheritanceFlags, rule.PropagationFlags, rule.IsInherited))
            .OrderBy(entry => entry, StringComparer.Ordinal).ToArray();

    private static void JournalAclDenial()
    {
        using var t = new TemporaryStore();
        using (var lease = t.Store.TryAcquire())
            Require(lease is not null && t.Store.MarkUncertain(Recovery), "durable intent before denial");
        var file = new FileInfo(Journal(t.Root));
        var originalAcl = file.GetAccessControl(AccessControlSections.Access);
        byte[] original = File.ReadAllBytes(file.FullName);
        HostOperationStore? denied = null;
        try
        {
            var changed = file.GetAccessControl(AccessControlSections.Access);
            changed.AddAccessRule(DenyCurrent(FileSystemRights.ReadData)); file.SetAccessControl(changed);
            bool osDenied = false;
            try { _ = File.ReadAllBytes(file.FullName); }
            catch (UnauthorizedAccessException) { osDenied = true; }
            Require(osDenied, "actual OS journal read refusal must be exercised");
            denied = new HostOperationStore(t.Root);
            Require(denied.HasUnresolvedOperation && denied.RecoveryDigest is null &&
                denied.TryAcquire() is null && !denied.MarkComplete(), "denied state never becomes success");
        }
        finally { originalAcl.SetSecurityDescriptorBinaryForm(originalAcl.GetSecurityDescriptorBinaryForm(), AccessControlSections.Access); file.SetAccessControl(originalAcl); }
        Require(File.ReadAllBytes(file.FullName).AsSpan().SequenceEqual(original), "journal preserved byte-for-byte");
        Require(denied is not null && denied.TryAcquire() is null, "denied instance cannot silently repair itself");
        var reopened = new HostOperationStore(t.Root);
        using var fresh = reopened.TryAcquire();
        Require(fresh is not null && reopened.HasUnresolvedOperation && reopened.RecoveryDigest == Recovery &&
            !reopened.MarkComplete(), "fresh open retains recovery requirement, not inherited completion authority");
    }

    private static void LockAclDenial()
    {
        using var t = new TemporaryStore();
        var file = new FileInfo(Path.Combine(t.Root, HostOperationStore.LockName));
        var originalAcl = file.GetAccessControl(AccessControlSections.Access);
        byte[] journal = File.ReadAllBytes(Journal(t.Root));
        try
        {
            var changed = file.GetAccessControl(AccessControlSections.Access);
            changed.AddAccessRule(DenyCurrent(FileSystemRights.WriteData)); file.SetAccessControl(changed);
            bool osDenied = false;
            try { using var probe = new FileStream(file.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
            catch (UnauthorizedAccessException) { osDenied = true; }
            Require(osDenied, "actual OS stable-lock refusal must be exercised");
            Require(t.Store.TryAcquire() is null && !t.Store.MarkUncertain(Recovery) && !t.Store.MarkComplete(),
                "no ownership or mutation with inaccessible stable lock");
        }
        finally { originalAcl.SetSecurityDescriptorBinaryForm(originalAcl.GetSecurityDescriptorBinaryForm(), AccessControlSections.Access); file.SetAccessControl(originalAcl); }
        Require(file.Exists && File.ReadAllBytes(Journal(t.Root)).AsSpan().SequenceEqual(journal),
            "existing lock and idle journal retained");
        using var lease = new HostOperationStore(t.Root).TryAcquire();
        Require(lease is not null, "fresh owner may acquire after explicit ACL restoration");
    }

    private static void RootAclDenial()
    {
        using var t = new TemporaryStore();
        using var lease = t.Store.TryAcquire(); Require(lease is not null, "own stable lease");
        var directory = new DirectoryInfo(t.Root);
        var originalAcl = directory.GetAccessControl(AccessControlSections.Access);
        byte[] journal = File.ReadAllBytes(Journal(t.Root));
        try
        {
            var changed = directory.GetAccessControl(AccessControlSections.Access);
            changed.AddAccessRule(DenyCurrent(FileSystemRights.CreateFiles)); directory.SetAccessControl(changed);
            bool osDenied = false;
            string probe = Path.Combine(t.Root, "acl-create-probe");
            try { using var created = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
            catch (UnauthorizedAccessException) { osDenied = true; }
            Require(osDenied, "actual OS directory create refusal must be exercised");
            Require(!t.Store.MarkUncertain(Recovery) && t.Store.HasUnresolvedOperation && !t.Store.MarkComplete(),
                "failed durable intent poisons caller instead of admitting work");
        }
        finally { originalAcl.SetSecurityDescriptorBinaryForm(originalAcl.GetSecurityDescriptorBinaryForm(), AccessControlSections.Access); directory.SetAccessControl(originalAcl); }
        Require(File.ReadAllBytes(Journal(t.Root)).AsSpan().SequenceEqual(journal) &&
            Directory.GetFiles(t.Root).Length == 2, "denied commit creates no journal replacement or leftover payload");
        Require(!t.Store.MarkUncertain(Recovery), "permission restoration cannot repair poisoned caller");
    }

    private static void AdmissionLockRace()
    {
        using var t = new TemporaryStore(); using var f = new HostUpdateEngineTests.Fixture(select: false);
        var ownership = new BeforeAcquire(t.Store, () =>
        {
            var other = new HostOperationStore(t.Root); using var lease = other.TryAcquire();
            Require(lease is not null && other.MarkUncertain(Recovery), "other operation lands after pre-lock admission");
        });
        var engine = new HostUpdateEngine(f.Controller, f.Context, f.Inspection, () => f.CurrentInspection,
            f.Admit(), f.Policy, () => f.CurrentPolicy, f.Adapter, ownership, t.Store, () => f.Now, f.Profile);
        Require(engine.Select(f.Selection), "initially idle explicit selection"); Run(engine);
        Require(engine.State == HostUpdateState.NoWriteStopped && f.Adapter.Writes == 0 && f.Adapter.Reads == 0 &&
            f.Adapter.Boots == 0 && t.Store.HasUnresolvedOperation, "post-lock binding rejects stale pre-lock admission");
    }
    private static void PathRefusal()
    {
        using var t = new TemporaryStore();
        foreach (string path in new[] { t.Root + "\\", t.Root + "\\." })
        {
            bool refused = false;
            try { _ = new HostOperationStore(path); }
            catch (InvalidOperationException e) { refused = e.Message == "host-store-unavailable"; }
            Require(refused, "canonical absolute root required");
        }
        var originalDirectory = Environment.CurrentDirectory;
        try
        {
            // Resolve an existing valid store on its own volume, even when the caller is elsewhere.
            Environment.CurrentDirectory = Path.GetDirectoryName(t.Root)!;
            var relativeRoot = Path.GetFileName(t.Root);
            Require(!Path.IsPathRooted(relativeRoot) && !Path.IsPathFullyQualified(relativeRoot) &&
                Directory.Exists(relativeRoot) && Path.GetFullPath(relativeRoot) == t.Root,
                "relative fixture must resolve to the existing valid store");
            _ = new HostOperationStore(t.Root);
            bool refused = false;
            try { _ = new HostOperationStore(relativeRoot); }
            catch (InvalidOperationException e) { refused = e.Message == "host-store-unavailable"; }
            Require(refused, "relative existing store must be refused");
        }
        finally { Environment.CurrentDirectory = originalDirectory; }
        File.Delete(Path.Combine(t.Root, HostOperationStore.LockName));
        Require(t.Store.TryAcquire() is null && !File.Exists(Path.Combine(t.Root, HostOperationStore.LockName)), "missing stable lock is never recreated");
    }
    private static void HardLinkRefusal()
    {
        using var t = new TemporaryStore(); string alias = Path.Combine(t.Root, "alias.json");
        Require(CreateHardLink(alias, Journal(t.Root), IntPtr.Zero), "test hard link created");
        try { Require(t.Store.HasUnresolvedOperation && t.Store.TryAcquire() is null, "multiply linked journal refuses alias authority"); }
        finally { File.Delete(alias); }
    }
    private static void JunctionRefusal()
    {
        using var t = new TemporaryStore(); string alias = Path.Combine(t.Root, "junction");
        string system = Environment.GetEnvironmentVariable("SystemRoot") ?? throw new InvalidOperationException("Windows runtime unavailable");
        var start = new ProcessStartInfo(Path.Combine(system, "System32", "cmd.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.Arguments = "/c mklink /J \"" + alias + "\" \"" + t.Root + "\"";
        using var child = Process.Start(start) ?? throw new InvalidOperationException("junction fixture unavailable");
        Require(child.WaitForExit(15000) && child.ExitCode == 0 &&
            (File.GetAttributes(alias) & FileAttributes.ReparsePoint) != 0, "bounded junction fixture created");
        try
        {
            bool refused = false;
            try { _ = new HostOperationStore(alias); }
            catch (InvalidOperationException e) { refused = e.Message == "host-store-unavailable"; }
            Require(refused, "junction path cannot create a second authority");
        }
        finally { Directory.Delete(alias, recursive: false); }
    }
    private static void JournalSubstitution()
    {
        using var t = new TemporaryStore();
        byte[] original = File.ReadAllBytes(Journal(t.Root));
        using (var lease = t.Store.TryAcquire())
        {
            Require(lease is not null, "owned lease before isolated tamper");
            using var json = JsonDocument.Parse(original);
            string digest = json.RootElement.GetProperty("storeDigest").GetString()!;
            string replacement = digest == new string('B', 64) ? new string('C', 64) : new string('B', 64);
            byte[] foreign = System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(original)
                .Replace(digest, replacement, StringComparison.Ordinal));
            File.WriteAllBytes(Journal(t.Root), foreign);
            Require(!t.Store.MarkUncertain(Recovery) && !t.Store.MarkComplete() && !t.Store.ReleaseUnsubmitted(),
                "foreign store identity cannot grant marker or completion authority");
            Require(t.Store.HasUnresolvedOperation && t.Store.RecoveryDigest is null &&
                File.ReadAllBytes(Journal(t.Root)).AsSpan().SequenceEqual(foreign),
                "tampered bytes preserved and instance refuses without repair");
            File.WriteAllBytes(Journal(t.Root), original);
            Require(!t.Store.MarkUncertain(Recovery), "restoring bytes does not unpoison retained object");
        }
        Require(t.Store.TryAcquire() is null, "poison remains after lease disposal");
        var fresh = new HostOperationStore(t.Root);
        using var freshLease = fresh.TryAcquire();
        Require(freshLease is not null && !fresh.HasUnresolvedOperation,
            "fresh isolated object reads restored idle bytes without fabricated uncertainty");
    }
    private static void LockNameSubstitution()
    {
        using var t = new TemporaryStore();
        string path = Path.Combine(t.Root, HostOperationStore.LockName), alias = Path.Combine(t.Root, "moved.lock");
        byte[] original = File.ReadAllBytes(Journal(t.Root));
        using (var lease = t.Store.TryAcquire())
        {
            Require(lease is not null, "live isolated owner");
            foreach (Action attempt in new Action[] { () => File.Move(path, alias), () => File.Delete(path) })
            {
                bool refused = false;
                try { attempt(); }
                catch (IOException) { refused = true; }
                catch (UnauthorizedAccessException) { refused = true; }
                Require(refused && File.Exists(path) && !File.Exists(alias), "stable lock name remains bound while held");
            }
            Require(new HostOperationStore(t.Root).TryAcquire() is null &&
                File.ReadAllBytes(Journal(t.Root)).AsSpan().SequenceEqual(original), "no second owner or journal mutation");
        }
        File.Move(path, alias); File.Move(alias, path);
        using var next = t.Store.TryAcquire();
        Require(next is not null && new FileInfo(path).Length == 0,
            "ordinary owned fixture rename succeeds after release and lock remains usable");
    }
    private static void ReadbackHardLink()
    {
        using var t = new TemporaryStore(); using var f = new HostUpdateEngineTests.Fixture(select: false);
        string alias = Path.Combine(t.Root, "readback-alias.json"); bool linked = false;
        var store = new HostOperationStore(t.Root, step =>
        {
            if (step == HostStoreStep.Readback)
                linked = CreateHardLink(alias, Journal(t.Root), IntPtr.Zero);
        });
        var engine = Engine(f, store);
        Require(engine.Select(f.Selection), "explicit fixture selection before tamper");
        try
        {
            Run(engine);
            Require(linked && File.Exists(alias), "actual hard link introduced after atomic replacement");
            Require(engine.State == HostUpdateState.NoWriteStopped && f.Adapter.Writes == 0 &&
                f.Adapter.Reads == 0 && f.Adapter.Boots == 0, "failed readback prevents all adapter commands");
            Require(store.HasUnresolvedOperation && store.RecoveryDigest is null && store.TryAcquire() is null,
                "ambiguous instance refuses further authority");
            byte[] durable = File.ReadAllBytes(Journal(t.Root));
            Require(File.ReadAllBytes(alias).AsSpan().SequenceEqual(durable), "alias refers to landed journal bytes");
            File.Delete(alias);
            var fresh = new HostOperationStore(t.Root);
            Require(fresh.HasUnresolvedOperation && fresh.RecoveryDigest == Recovery &&
                File.ReadAllBytes(Journal(t.Root)).AsSpan().SequenceEqual(durable),
                "removing owned alias reveals landed uncertainty without clearing or rewriting it");
            Require(!store.MarkComplete() && !store.ReleaseUnsubmitted(), "retained poisoned object cannot clear uncertainty");
        }
        finally { if (File.Exists(alias)) File.Delete(alias); }
    }
    private static void Privacy()
    {
        using var t = new TemporaryStore(); using var lease = t.Store.TryAcquire();
        Require(lease is not null && t.Store.MarkUncertain(Recovery), "marker saved");
        byte[] bytes = File.ReadAllBytes(Journal(t.Root));
        using var document = JsonDocument.Parse(bytes); var value = document.RootElement;
        Require(bytes.Length <= 1024 && value.EnumerateObject().Select(p => p.Name).SequenceEqual(
            new[] { "schema", "storeDigest", "state", "operationDigest", "recoveryDigest" }), "bounded schema only");
        foreach (string key in new[] { "storeDigest", "operationDigest", "recoveryDigest" })
            Require(value.GetProperty(key).GetString() is { Length: 64 } hash && hash.All(Uri.IsHexDigit), "opaque digests only");
        Require(!System.Text.Encoding.UTF8.GetString(bytes).Contains(t.Root, StringComparison.Ordinal) &&
            Directory.GetFiles(t.Root).Length == 2 && new FileInfo(Path.Combine(t.Root, HostOperationStore.LockName)).Length == 0,
            "no paths device identifiers keys or firmware payload persisted");
    }
    private static void EngineComplete()
    {
        using var t = new TemporaryStore(); using var f = new HostUpdateEngineTests.Fixture(select: false);
        var engine = Engine(f, t.Store); Require(engine.Select(f.Selection), "explicit selection"); Run(engine);
        Require(engine.State == HostUpdateState.Verified && engine.BytesVerified && f.Adapter.Writes == 1 &&
            f.Adapter.Reads == 1 && f.Adapter.Boots == 1, "actual engine exact host sequence");
        var reopened = new HostOperationStore(t.Root); using var held = reopened.TryAcquire();
        Require(held is not null && !reopened.HasUnresolvedOperation && !reopened.MarkComplete(), "terminal marker durable without replay authority");
        Require(LoaderProductionProviders.SignerTrustPolicies.Count == 0 && !f.Controller.Snapshot.FirmwareWritingAvailable,
            "production remains inspection only");
    }
    private static void StartFailures()
    {
        foreach (var step in Enum.GetValues<HostStoreStep>())
        {
            using var t = new TemporaryStore(); using var f = new HostUpdateEngineTests.Fixture(select: false);
            var store = new HostOperationStore(t.Root, s => { if (s == step) throw new IOException("private path must not escape"); });
            var engine = Engine(f, store); Require(engine.Select(f.Selection), "selected before fault"); Run(engine);
            Require(engine.State == HostUpdateState.NoWriteStopped && f.Adapter.Writes == 0 && f.Adapter.Reads == 0 &&
                f.Adapter.Boots == 0 && store.HasUnresolvedOperation && store.RecoveryDigest is null, "failed marker prevents all commands");
            var fresh = new HostOperationStore(t.Root);
            Require(fresh.HasUnresolvedOperation == (step == HostStoreStep.Readback), "replacement boundary is honest on restart");
        }
    }
    private static void CompletionFailures()
    {
        foreach (var step in Enum.GetValues<HostStoreStep>())
        {
            using var t = new TemporaryStore(); using var f = new HostUpdateEngineTests.Fixture(select: false); int writes = 0;
            var store = new HostOperationStore(t.Root, s => { if (s == HostStoreStep.Write) writes++; if (writes == 2 && s == step) throw new IOException(); });
            var engine = Engine(f, store); Require(engine.Select(f.Selection), "selected before completion fault"); Run(engine);
            Require(engine.State == HostUpdateState.OutcomeUnknown && engine.BytesVerified && f.Adapter.Writes == 1 &&
                f.Adapter.Reads == 1 && f.Adapter.Boots == 1 && store.HasUnresolvedOperation && store.TryAcquire() is null,
                "terminal persistence cannot fabricate success or clear poisoned instance");
            Require(new HostOperationStore(t.Root).HasUnresolvedOperation == (step != HostStoreStep.Readback),
                "fresh durable completion only if replacement actually landed");
        }
    }
    private static void TerminalRootAclDenial()
    {
        using var t = new TemporaryStore(); using var f = new HostUpdateEngineTests.Fixture(select: false);
        var directory = new DirectoryInfo(t.Root);
        var originalAcl = directory.GetAccessControl(AccessControlSections.Access);
        byte[] originalDescriptor = originalAcl.GetSecurityDescriptorBinaryForm(), intent = Array.Empty<byte>();
        string[] originalRules = AccessRules(originalAcl);
        bool osDenied = false; int nativeError = 0;
        f.Adapter.Boot = () =>
        {
            intent = File.ReadAllBytes(Journal(t.Root));
            var denied = directory.GetAccessControl(AccessControlSections.Access);
            denied.AddAccessRule(DenyCurrent(FileSystemRights.CreateFiles)); directory.SetAccessControl(denied);
            try { using var probe = new FileStream(Path.Combine(t.Root, "terminal-acl-probe"), FileMode.CreateNew, FileAccess.Write, FileShare.None); }
            catch (UnauthorizedAccessException error) { osDenied = true; nativeError = error.HResult & 0xffff; }
            return Task.FromResult(new HostBootResult(f.Manifest.ImageDigest, true, true));
        };
        var engine = Engine(f, t.Store);
        try
        {
            Require(engine.Select(f.Selection), "explicit selection before actual terminal denial"); Run(engine);
            Require(osDenied && nativeError == 5 && intent.Length > 0, "actual OS access-denied must occur after durable intent");
            using var record = JsonDocument.Parse(intent);
            Require(record.RootElement.GetProperty("state").GetString() == "unresolved" &&
                record.RootElement.GetProperty("recoveryDigest").GetString() == Recovery, "exact uncertainty predates terminal denial");
            Require(engine.State == HostUpdateState.OutcomeUnknown && engine.BytesVerified &&
                f.Adapter.Writes == 1 && f.Adapter.Reads == 1 && f.Adapter.Boots == 1,
                "verified fixture commands cannot turn failed terminal persistence into success");
            Require(t.Store.HasUnresolvedOperation && t.Store.RecoveryDigest is null &&
                File.ReadAllBytes(Journal(t.Root)).AsSpan().SequenceEqual(intent) && Directory.GetFiles(t.Root).Length == 2,
                "failed terminal commit preserves intent bytes without repair or pending files");
        }
        finally
        {
            originalAcl.SetSecurityDescriptorBinaryForm(originalDescriptor, AccessControlSections.Access);
            directory.SetAccessControl(originalAcl);
        }
        Require(AccessRules(directory.GetAccessControl(AccessControlSections.Access)).SequenceEqual(originalRules),
            "exact original fixture DACL rules restored");
        Require(File.ReadAllBytes(Journal(t.Root)).AsSpan().SequenceEqual(intent) && t.Store.TryAcquire() is null &&
            !t.Store.MarkComplete() && !t.Store.ReleaseUnsubmitted(), "ACL restoration cannot unpoison or clear retained caller");
        var reopened = new HostOperationStore(t.Root); using var freshLease = reopened.TryAcquire();
        Require(freshLease is not null && reopened.HasUnresolvedOperation && reopened.RecoveryDigest == Recovery &&
            !reopened.MarkComplete() && !reopened.ReleaseUnsubmitted(), "fresh owner retains uncertainty without inherited completion authority");
        Console.WriteLine("Terminal permission fixture: actual CreateFiles denial=True; error=" + nativeError +
            "; unresolved bytes preserved=True; original DACL restored=True");
    }
    private static void ProcessExclusion()
    {
        using var t = new TemporaryStore(); using var child = Child.Start("hold", t.Root); child.Expect("locked");
        Competitor(t.Root, false); Require(t.Store.TryAcquire() is null, "local contender blocked");
        using (var f = new HostUpdateEngineTests.Fixture(select: false))
        {
            var engine = Engine(f, t.Store); Require(engine.Select(f.Selection), "idle contender selected"); Run(engine);
            Require(engine.State == HostUpdateState.NoWriteStopped && f.Adapter.Writes == 0 && f.Adapter.Reads == 0 &&
                f.Adapter.Boots == 0, "actual contender submits zero commands");
        }
        child.Send("release"); child.Expect("released"); child.Success(); Competitor(t.Root, true);
    }
    private static void CrashRestart()
    {
        using var t = new TemporaryStore(); using (var child = Child.Start("mark", t.Root)) { child.Expect("marked"); Competitor(t.Root, false); child.Kill(); }
        Competitor(t.Root, true); var fresh = new HostOperationStore(t.Root);
        Require(fresh.HasUnresolvedOperation && fresh.RecoveryDigest == Recovery, "kill retains exact recovery marker");
        using var f = new HostUpdateEngineTests.Fixture(select: false); var engine = Engine(f, fresh);
        Require(!engine.Select(f.Selection) && engine.Confirm() is null && f.Adapter.Writes == 0, "restart never auto-resumes candidate");
    }
    private static void CrashBeforeReplace()
    {
        foreach (var step in new[] { HostStoreStep.Write, HostStoreStep.Flush, HostStoreStep.Replace }) CrashAt(step, false);
    }
    private static void CrashAfterReplace() => CrashAt(HostStoreStep.Readback, true);
    private static void CrashAt(HostStoreStep step, bool unresolved)
    {
        using var t = new TemporaryStore();
        using (var child = Child.Start("checkpoint", t.Root, step.ToString())) { child.Expect("checkpoint"); child.Kill(); }
        var fresh = new HostOperationStore(t.Root); using var own = fresh.TryAcquire();
        Require(own is not null && fresh.HasUnresolvedOperation == unresolved, "restart trusts only canonical atomic record");
        Require(!fresh.MarkComplete(), "crashed process operation cannot be completed by new owner");
    }
    private static void EngineCrash()
    {
        foreach (string phase in new[] { "write", "read", "boot" })
        {
            using var t = new TemporaryStore();
            using (var child = Child.Start("submit", t.Root, phase)) { child.Expect("submitted"); Require(t.Store.HasUnresolvedOperation, "durable before adapter ACK"); child.Kill(); }
            var fresh = new HostOperationStore(t.Root); Require(fresh.HasUnresolvedOperation && fresh.RecoveryDigest == Recovery, "submission crash stays unresolved");
            using var f = new HostUpdateEngineTests.Fixture(select: false); var engine = Engine(f, fresh);
            Require(!engine.Select(f.Selection) && f.Adapter.Writes == 0, "zero candidate replay after kill");
        }
    }
    private static void CancelQuiescence() => Quiescence("cancel");
    private static void TimeoutQuiescence() => Quiescence("timeout");
    private static void Quiescence(string mode)
    {
        using var t = new TemporaryStore(); using var child = Child.Start(mode, t.Root); child.Expect("submitted");
        if (mode == "cancel") child.Send("cancel");
        child.Expect("uncertain-pending"); Competitor(t.Root, false);
        child.Send("quiesce"); child.Expect("quiesced"); Competitor(t.Root, true); child.Success();
        Require(t.Store.HasUnresolvedOperation, "late command completion never clears uncertainty");
    }
    private static void RecoveryBinding()
    {
        using var t = new TemporaryStore(); using var f = new HostUpdateEngineTests.Fixture(select: false);
        var old = Engine(f, t.Store); Require(old.Select(f.Selection), "old selection"); var oldToken = old.Confirm();
        using (var lease = t.Store.TryAcquire()) Require(lease is not null && t.Store.MarkUncertain(Recovery), "unrelated original marked");
        var wrong = Engine(f, new HostOperationStore(t.Root), recovery: true);
        Require(!wrong.Select(f.Selection) && f.Adapter.Writes == 0, "wrong recovery digest refused");
        using var correct = new TemporaryStore();
        using (var lease = correct.Store.TryAcquire()) Require(lease is not null && correct.Store.MarkUncertain(f.Manifest.ImageDigest), "exact independent original marked");
        var recover = Engine(f, new HostOperationStore(correct.Root), recovery: true); Require(recover.Select(f.Selection), "fresh original selection");
        recover.RunAsync(oldToken).GetAwaiter().GetResult(); Require(f.Adapter.Writes == 0, "candidate confirmation cannot authorize recovery");
        Run(recover); Require(recover.State == HostUpdateState.Recovered && !new HostOperationStore(correct.Root).HasUnresolvedOperation,
            "separate exact recovery verifies and durably completes");
    }

    private static void Withdrawal()
    {
        using (var t = new TemporaryStore())
        {
            Require(!t.Store.ReleaseUnsubmitted(), "no ownership no withdrawal");
            byte[] idle = File.ReadAllBytes(Journal(t.Root));
            using (var lease = t.Store.TryAcquire())
            {
                Require(lease is not null && !t.Store.ReleaseUnsubmitted(), "no owned marker no withdrawal");
                Require(t.Store.MarkUncertain(Recovery) && t.Store.HasUnresolvedOperation, "marker saved");
                Require(t.Store.ReleaseUnsubmitted() && !t.Store.HasUnresolvedOperation &&
                    File.ReadAllBytes(Journal(t.Root)).SequenceEqual(idle), "exact idle record restored");
                Require(!t.Store.ReleaseUnsubmitted() && !t.Store.MarkComplete(), "withdrawn operation cannot be withdrawn or completed");
                Require(t.Store.MarkUncertain(Recovery) && t.Store.MarkComplete(), "completed operation");
                byte[] complete = File.ReadAllBytes(Journal(t.Root));
                Require(t.Store.MarkUncertain(Recovery) && t.Store.ReleaseUnsubmitted() &&
                    File.ReadAllBytes(Journal(t.Root)).SequenceEqual(complete), "exact complete record restored");
                Require(t.Store.MarkUncertain(Recovery), "operation left unresolved");
            }
            using (var lease = t.Store.TryAcquire())
            {
                Require(lease is not null && t.Store.HasUnresolvedOperation && t.Store.MarkUncertain(Recovery), "new marker over unresolved record");
                Require(!t.Store.ReleaseUnsubmitted() && t.Store.HasUnresolvedOperation, "earlier uncertainty is never withdrawn");
            }
        }
        using (var t = new TemporaryStore())
        {
            var held = t.Store.TryAcquire();
            Require(held is not null && t.Store.MarkUncertain(Recovery), "marker before disposal");
            held!.Dispose();
            Require(!t.Store.ReleaseUnsubmitted() && t.Store.HasUnresolvedOperation, "disposed ownership cannot withdraw");
        }
        using (var t = new TemporaryStore())
        using (var lease = t.Store.TryAcquire())
        {
            Require(lease is not null && t.Store.MarkUncertain(Recovery), "owned marker");
            string original = File.ReadAllText(Journal(t.Root));
            using (var json = JsonDocument.Parse(original))
            {
                string operation = json.RootElement.GetProperty("operationDigest").GetString()!;
                File.WriteAllText(Journal(t.Root), original.Replace(operation, new string('D', 64), StringComparison.Ordinal));
            }
            Require(!t.Store.ReleaseUnsubmitted(), "foreign operation is never withdrawn");
            File.WriteAllText(Journal(t.Root), original);
            Require(t.Store.ReleaseUnsubmitted() && !t.Store.HasUnresolvedOperation, "exact owned operation withdrawn");
        }
    }
    private static void EngineWithdrawal()
    {
        using var t = new TemporaryStore(); using var f = new HostUpdateEngineTests.Fixture(select: false);
        byte[] idle = File.ReadAllBytes(Journal(t.Root)); bool disconnected = false;
        // The device disappears just after the durable marker lands, before any command.
        var store = new HostOperationStore(t.Root, step =>
        { if (step == HostStoreStep.Readback && !disconnected) { disconnected = true; f.Adapter.CurrentSelection = null; } });
        var engine = Engine(f, store); Require(engine.Select(f.Selection), "selected before disconnect"); Run(engine);
        Require(disconnected && engine.State == HostUpdateState.NoWriteStopped && engine.UncertaintyMarkerReleased &&
            f.Adapter.Writes == 0 && f.Adapter.Reads == 0 && f.Adapter.Boots == 0, "no command and marker withdrawn");
        Require(File.ReadAllBytes(Journal(t.Root)).SequenceEqual(idle) && !new HostOperationStore(t.Root).HasUnresolvedOperation,
            "durable record restored exactly");
        f.Adapter.CurrentSelection = f.Selection;
        var next = Engine(f, new HostOperationStore(t.Root));
        Require(next.State == HostUpdateState.DeviceUnselected && next.Select(f.Selection), "no recovery review demanded");
        Run(next); Require(next.State == HostUpdateState.Verified && f.Adapter.Writes == 1, "next attempt verifies");
    }
    private static void WithdrawalFailures()
    {
        foreach (var step in Enum.GetValues<HostStoreStep>())
        {
            using var t = new TemporaryStore(); using var f = new HostUpdateEngineTests.Fixture(select: false);
            int commits = 0; bool disconnected = false;
            var store = new HostOperationStore(t.Root, s =>
            {
                if (s == HostStoreStep.Write) commits++;
                if (commits == 1 && s == HostStoreStep.Readback && !disconnected) { disconnected = true; f.Adapter.CurrentSelection = null; }
                if (commits == 2 && s == step) throw new IOException("private path must not escape");
            });
            var engine = Engine(f, store); Require(engine.Select(f.Selection), "selected before withdrawal fault"); Run(engine);
            Require(engine.State == HostUpdateState.NoWriteStopped && engine.UncertaintyMarkerRetained && !engine.UncertaintyMarkerReleased &&
                f.Adapter.Writes == 0 && store.HasUnresolvedOperation && store.TryAcquire() is null,
                "failed withdrawal keeps the poisoned instance conservative");
            Require(new HostOperationStore(t.Root).HasUnresolvedOperation == (step != HostStoreStep.Readback),
                "fresh view resolved only if the restoring replacement actually landed");
        }
    }

    internal static bool TryRunChild(string[] args)
    {
        if (args.Length == 0 || args[0] != "--host-store-child") return false;
        Require(args.Length is 3 or 4, "child arguments"); string mode = args[1], root = args[2];
        if (mode is "hold" or "mark" or "checkpoint")
        {
            var store = new HostOperationStore(root, mode == "checkpoint" ? step =>
            { if (step.ToString() == args[3]) { Ack("checkpoint"); Require(Console.ReadLine() == "continue", "checkpoint release"); } } : null);
            using var lease = store.TryAcquire();
            if (lease is null) { Ack("blocked"); return true; }
            if (mode != "hold") Require(store.MarkUncertain(Recovery), "child mark");
            Ack(mode == "hold" ? "locked" : "marked"); Require(Console.ReadLine() == "release", "child release");
            lease.Dispose(); Ack("released"); return true;
        }
        Require(mode is "submit" or "cancel" or "timeout", "child mode");
        using var f = new HostUpdateEngineTests.Fixture(select: false); var durable = new HostOperationStore(root);
        var pending = new TaskCompletionSource();
        string phase = args.Length == 4 ? args[3] : "write";
        Require(phase is "write" or "read" or "boot", "child submission phase");
        if (phase == "write") f.Adapter.Write = () => { Ack("submitted"); return pending.Task; };
        else if (phase == "read") f.Adapter.Read = async () => { Ack("submitted"); await pending.Task; return new HostReadback(f.Image, true); };
        else f.Adapter.Boot = async () => { Ack("submitted"); await pending.Task; return new HostBootResult(f.Manifest.ImageDigest, true, true); };
        var profile = f.Profile with { WriteMilliseconds = mode == "timeout" ? 250 : 10000, TotalMilliseconds = 20000 };
        var engine = Engine(f, durable, profile: profile); Require(engine.Select(f.Selection), "child selected");
        var run = engine.RunAsync(engine.Confirm());
        if (mode == "submit") { Require(Console.ReadLine() == "quiesce", "submission quiescence"); pending.SetResult(); run.GetAwaiter().GetResult(); return true; }
        if (mode == "cancel") { Require(Console.ReadLine() == "cancel", "cancel ACK"); engine.Cancel(); }
        run.GetAwaiter().GetResult(); Require(engine.State == HostUpdateState.OutcomeUnknown, "actual uncertain engine"); Ack("uncertain-pending");
        Require(Console.ReadLine() == "quiesce", "quiescence ACK"); pending.SetResult();
        Require(SpinWait.SpinUntil(() => { using var proof = new HostOperationStore(root).TryAcquire(); return proof is not null; }, 3000), "actual owner released after pending command");
        Require(engine.State == HostUpdateState.OutcomeUnknown && f.Adapter.Reads == 0 && f.Adapter.Boots == 0, "late result cannot progress"); Ack("quiesced");
        return true;
    }
    private static void Ack(string text) { Console.WriteLine(text); Console.Out.Flush(); }
    private static void Competitor(string root, bool acquired)
    {
        using var child = Child.Start("hold", root); child.Expect(acquired ? "locked" : "blocked");
        if (acquired) { child.Send("release"); child.Expect("released"); }
        child.Success();
    }
    private sealed class Child : IDisposable
    {
        private readonly Process process;
        private Child(Process process) { this.process = process; }
        internal static Child Start(string mode, string root, string? step = null)
        {
            string host = Environment.ProcessPath ?? throw new InvalidOperationException("test host unavailable");
            var start = new ProcessStartInfo(host) { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            if (Path.GetFileNameWithoutExtension(host).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                start.ArgumentList.Add(typeof(HostOperationStoreTests).Assembly.Location);
            foreach (string argument in new[] { "--host-store-child", mode, root }) start.ArgumentList.Add(argument);
            if (step is not null) start.ArgumentList.Add(step);
            return new Child(Process.Start(start) ?? throw new InvalidOperationException("test child unavailable"));
        }
        internal void Expect(string value)
        {
            string? actual = process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
            Require(actual == value, $"child checkpoint expected {value}, got {actual ?? "EOF"}");
        }
        internal void Send(string text) { process.StandardInput.WriteLine(text); process.StandardInput.Flush(); }
        internal void Success()
        {
            Require(process.WaitForExit(15000) && process.ExitCode == 0, "child successful exit");
            Require(process.StandardError.ReadToEnd().Length == 0, "child privacy-safe diagnostics");
        }
        internal void Kill() { if (!process.HasExited) process.Kill(entireProcessTree: true); Require(process.WaitForExit(15000), "killed process closed"); }
        public void Dispose() { Kill(); process.Dispose(); }
    }
    private sealed class TemporaryStore : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "LUF.HostStore.Tests." + Guid.NewGuid().ToString("N"));
        internal HostOperationStore Store { get; }
        internal TemporaryStore() { Store = HostOperationStore.CreateIsolatedTestStore(Root); }
        public void Dispose()
        {
            string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            string leaf = Path.GetFileName(Root);
            Require(Path.GetFullPath(Root) == Root && Path.GetDirectoryName(Root) == parent &&
                leaf.StartsWith("LUF.HostStore.Tests.", StringComparison.Ordinal) &&
                Guid.TryParseExact(leaf["LUF.HostStore.Tests.".Length..], "N", out _) &&
                (File.GetAttributes(Root) & FileAttributes.ReparsePoint) == 0, "owned temporary cleanup only");
            Directory.Delete(Root, recursive: true);
        }
    }
    private sealed class BeforeAcquire(IHostOperationLease inner, Action before) : IHostOperationLease
    { public IDisposable? TryAcquire() { before(); return inner.TryAcquire(); } }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string alias, string original, IntPtr reserved);
}
