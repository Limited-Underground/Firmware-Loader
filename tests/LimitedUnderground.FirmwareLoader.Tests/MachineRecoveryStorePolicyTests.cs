using LimitedUnderground.FirmwareLoader;
using System.Security.AccessControl;
using System.Security.Principal;

internal static class MachineRecoveryStorePolicyTests
{
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("machine root is exact CommonApplicationData product path", Root),
        ("machine directory descriptor grants only Administrators and SYSTEM", DirectoryDescriptor),
        ("machine file descriptor grants only Administrators and SYSTEM", FileDescriptor),
        ("machine policy refuses additional ordinary user grant", OrdinaryGrant),
        ("machine policy refuses wrong owner", WrongOwner),
        ("machine policy refuses unsafe inheritance", Inheritance),
        ("machine policy refuses incomplete permissions", IncompleteRights),
        ("machine policy refuses deny ACE and descriptor type confusion", DenyAndType),
        ("machine descriptor callers cannot alter later policy descriptors", FreshDescriptor),
        ("ordinary host constructor cannot open machine storage", ConstructorCannotBypass),
        ("isolated host factory cannot enroll machine storage", FactoryCannotBypass),
        ("machine initializer refuses forged enrollment authority", ForgedAuthority),
        ("concurrent null and forged proofs cannot consume enrollment authority", ConcurrentForgedAuthority),
        ("isolated directory pin rejects path aliases and nonfixture names", PinPathBinding),
        ("actual shared directory pin prevents name substitution until release", PinPreventsRename),
        ("actual shared directory pin refuses junction aliases", PinRefusesJunction),
        ("actual token gates machine APIs without machine enrollment in tests", ActualToken),
    };
    private static readonly SecurityIdentifier Users = new(WellKnownSidType.BuiltinUsersSid, null);
    private static void Require(bool value, string label)
    { if (!value) throw new InvalidOperationException(label); }
    private static void Refused(Action action, string expected)
    {
        try { action(); }
        catch (InvalidOperationException e) { Require(e.Message == expected, "sanitized policy refusal"); return; }
        throw new InvalidOperationException("expected policy refusal");
    }
    private static void Root()
    {
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "LimitedUnderground.FirmwareLoader", "recovery", "v1", "opentrail");
        Require(MachineRecoveryStorePolicy.RootPath == expected && Path.IsPathFullyQualified(expected), "exact approved root");
        Require(!expected.StartsWith("\\\\", StringComparison.Ordinal), "local root only");
        Refused(() => { using var guard = MachineRecoveryStorePolicy.PinIsolatedTestDirectory(expected); }, "machine-store-unavailable");
    }
    private static void DirectoryDescriptor()
    {
        var s = MachineRecoveryStorePolicy.DirectoryPolicy();
        Require(MachineRecoveryStorePolicy.HasExactPolicy(s, true), "directory descriptor accepted");
        Require(s.AreAccessRulesProtected && s.GetAccessRules(true, true, typeof(SecurityIdentifier)).Count == 2,
            "protected exact two-principal directory policy");
    }
    private static void FileDescriptor() => Require(MachineRecoveryStorePolicy.HasExactPolicy(
        MachineRecoveryStorePolicy.FilePolicy(), false), "file descriptor accepted");
    private static void OrdinaryGrant()
    {
        var s = MachineRecoveryStorePolicy.DirectoryPolicy();
        s.AddAccessRule(new FileSystemAccessRule(Users, FileSystemRights.ReadAndExecute,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        Require(!MachineRecoveryStorePolicy.HasExactPolicy(s, true), "ordinary account grant rejected even read only");
    }
    private static void WrongOwner()
    {
        var s = MachineRecoveryStorePolicy.DirectoryPolicy(); s.SetOwner(Users);
        Require(!MachineRecoveryStorePolicy.HasExactPolicy(s, true), "wrong owner rejected");
    }
    private static void Inheritance()
    {
        var s = MachineRecoveryStorePolicy.DirectoryPolicy(); s.SetAccessRuleProtection(false, false);
        Require(!MachineRecoveryStorePolicy.HasExactPolicy(s, true), "unprotected inheritance rejected");
        var f = MachineRecoveryStorePolicy.FilePolicy(); f.SetAccessRuleProtection(false, false);
        Require(!MachineRecoveryStorePolicy.HasExactPolicy(f, false), "file inheritance rejected");
    }
    private static void IncompleteRights()
    {
        var s = new DirectorySecurity(); s.SetOwner(MachineRecoveryStorePolicy.Administrators);
        s.SetAccessRuleProtection(true, false);
        foreach (var sid in new[] { MachineRecoveryStorePolicy.Administrators, MachineRecoveryStorePolicy.System })
            s.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.ReadAndExecute,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        Require(!MachineRecoveryStorePolicy.HasExactPolicy(s, true), "partial grants cannot satisfy enrollment");
    }
    private static void DenyAndType()
    {
        var s = MachineRecoveryStorePolicy.DirectoryPolicy();
        s.AddAccessRule(new FileSystemAccessRule(MachineRecoveryStorePolicy.System, FileSystemRights.WriteData,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Deny));
        Require(!MachineRecoveryStorePolicy.HasExactPolicy(s, true), "unexpected deny rejected");
        Require(!MachineRecoveryStorePolicy.HasExactPolicy(MachineRecoveryStorePolicy.DirectoryPolicy(), false), "directory ACEs not file policy");
        Require(!MachineRecoveryStorePolicy.HasExactPolicy(MachineRecoveryStorePolicy.FilePolicy(), true), "file ACEs not directory policy");
    }
    private static void FreshDescriptor()
    {
        var first = MachineRecoveryStorePolicy.DirectoryPolicy(); first.SetOwner(Users);
        Require(MachineRecoveryStorePolicy.HasExactPolicy(MachineRecoveryStorePolicy.DirectoryPolicy(), true), "no mutable shared descriptor");
    }
    private static void ConstructorCannotBypass()
    {
        Refused(() => _ = new HostOperationStore(MachineRecoveryStorePolicy.RootPath), "host-store-unavailable");
        Refused(() => _ = new HostOperationStore(Path.Combine(MachineRecoveryStorePolicy.RootPath, ".")), "host-store-unavailable");
    }
    private static void FactoryCannotBypass() => Refused(() => _ = HostOperationStore.CreateIsolatedTestStore(
        MachineRecoveryStorePolicy.RootPath), "host-store-unavailable");
    private static void ForgedAuthority()
    {
        Refused(() => _ = HostOperationStore.EnrollMachine(new object()), "host-store-unavailable");
        Refused(() => _ = HostOperationStore.EnrollMachine(null!), "host-store-unavailable");
    }
    private static void ConcurrentForgedAuthority()
    {
        object forged = new(); int accepted = 0;
        Parallel.For(0, 256, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
        {
            object proof = i % 3 == 0 ? null! : i % 3 == 1 ? forged : new object();
            if (MachineRecoveryStorePolicy.ConsumeEnrollmentAuthority(proof)) Interlocked.Increment(ref accepted);
        });
        Require(accepted == 0, "negative proof concurrency cannot mint or consume valid authority");
        Refused(() => _ = HostOperationStore.EnrollMachine(forged), "host-store-unavailable");
        Refused(() => _ = HostOperationStore.EnrollMachine(null!), "host-store-unavailable");
        // No valid permit, elevated enrollment race or token transition is simulated here.
    }
    private static void PinPathBinding()
    {
        string root = FreshRoot();
        Require(!Directory.Exists(root), "fresh owned pin fixture");
        Directory.CreateDirectory(root);
        try
        {
            foreach (string path in new[] { root + "x", root + Path.DirectorySeparatorChar,
                Path.Combine(root, "."), Path.Combine(root, "LUF.HostStore.Tests." + Guid.NewGuid().ToString("N")) })
                Refused(() => { using var guard = MachineRecoveryStorePolicy.PinIsolatedTestDirectory(path); },
                    "machine-store-unavailable");
            Require(!Directory.Exists(root + "x") && Directory.GetFileSystemEntries(root).Length == 0,
                "invalid bindings never create or repair fixture paths");
            using var valid = MachineRecoveryStorePolicy.PinIsolatedTestDirectory(root);
            Require(Directory.GetFileSystemEntries(root).Length == 0, "exact isolated path remains admissible without enrollment");
        }
        finally { Directory.Delete(root, false); }
    }
    private static string FreshRoot() => Path.Combine(Path.GetTempPath(), "LUF.HostStore.Tests." + Guid.NewGuid().ToString("N"));
    private static void PinPreventsRename()
    {
        string root = FreshRoot(), moved = FreshRoot();
        Require(!Directory.Exists(root) && !Directory.Exists(moved), "fresh owned fixture paths");
        Directory.CreateDirectory(root);
        try
        {
            using (var guard = MachineRecoveryStorePolicy.PinIsolatedTestDirectory(root))
            {
                bool refused = false;
                try { Directory.Move(root, moved); }
                catch (IOException) { refused = true; }
                catch (UnauthorizedAccessException) { refused = true; }
                Require(refused && Directory.Exists(root) && !Directory.Exists(moved), "pinned name cannot be replaced");
            }
            Directory.Move(root, moved);
            Require(Directory.Exists(moved) && !Directory.Exists(root), "pin releases owned directory cleanly");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, false);
            if (Directory.Exists(moved)) Directory.Delete(moved, false);
        }
    }
    private static void PinRefusesJunction()
    {
        string target = FreshRoot(), alias = FreshRoot();
        Require(!Directory.Exists(target) && !Directory.Exists(alias), "fresh owned junction fixture paths");
        Directory.CreateDirectory(target);
        try
        {
            string system = Environment.GetEnvironmentVariable("SystemRoot") ?? throw new InvalidOperationException("Windows runtime unavailable");
            var start = new System.Diagnostics.ProcessStartInfo(Path.Combine(system, "System32", "cmd.exe"))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.Arguments = "/c mklink /J \"" + alias + "\" \"" + target + "\"";
            using var child = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("junction fixture unavailable");
            Require(child.WaitForExit(15000) && child.ExitCode == 0 &&
                (File.GetAttributes(alias) & FileAttributes.ReparsePoint) != 0, "actual bounded junction created");
            Refused(() => { using var guard = MachineRecoveryStorePolicy.PinIsolatedTestDirectory(alias); }, "machine-store-unavailable");
            Require(Directory.Exists(target), "guard refusal leaves target untouched");
        }
        finally
        {
            if (Directory.Exists(alias)) Directory.Delete(alias, false);
            if (Directory.Exists(target)) Directory.Delete(target, false);
        }
    }
    private static void ActualToken()
    {
        bool elevated = MachineRecoveryStorePolicy.IsElevatedAdministrator();
        Console.WriteLine("Machine policy test context: elevated-administrator=" + elevated);
        if (elevated)
        {
            // Never create/open the real ProgramData store merely to obtain a positive test.
            Console.WriteLine("LIMIT: elevated production enrollment/open not executed; no machine-store access authorized by host test.");
            return;
        }
        Refused(() => _ = MachineRecoveryStorePolicy.Open(), "machine-store-unavailable");
        Refused(() => _ = MachineRecoveryStorePolicy.Enroll(), "machine-store-unavailable");
    }
}
