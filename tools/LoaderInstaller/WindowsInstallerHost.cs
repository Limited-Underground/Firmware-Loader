using System.Diagnostics;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;

namespace LimitedUnderground.FirmwareLoader.Setup;

internal sealed class WindowsInstallerHost(InstallerOptions options) : IInstallerHost
{
    private const string Loader = "LimitedUnderground.FirmwareLoader.exe";
    private const string ProgramDirectoryName = "LimitedUnderground.FirmwareLoader";
    private const string ProvisionScript = "Install-LoaderDailyWorker.ps1";
    private const string AdministratorSid = "S-1-5-32-544";
    private const string SystemSid = "S-1-5-18";
    private const string UsersSid = "S-1-5-32-545";
    private const string TrustedInstallerSid = "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";
    private static readonly string[] SetupScripts = [ProvisionScript, "LoaderDailyProvisioning.psm1", "Initialize-LoaderMachine.ps1"];
    private static readonly string[] PlainSetupErrors =
    [
        "The preserved Windows user conflicts with this setup.",
        "The interrupted setup conflicts with this assignment. Repeat the exact previous setup input.",
        "The preserved assignment conflicts with this setup. Use the explicit assignment replacement option for a reviewed list change.",
        "An earlier board A qualification record needs administrator-reviewed migration before daily worker setup.",
        "Another Firmware Loader setup is in progress.",
    ];

    private static string ProgramFiles =>
        LockedPaths.AbsoluteLocalPath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
    private static string InstallDirectory => Path.Combine(ProgramFiles, ProgramDirectoryName);
    private static string ShellPath => Path.Combine(ProgramFiles, "PowerShell", "7", "pwsh.exe");

    public bool IsAdministrator
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public void EnsureInstalled(VerifiedPayload payload)
    {
        ValidateSource();
        using var programFilesGuard = LockedPaths.PinDirectory(ProgramFiles);
        AssertAncestors(new DirectoryInfo(ProgramFiles));
        using var shell = OpenPowerShell();
        var expected = GetInstallHashes(payload);
        if (LockedPaths.Exists(InstallDirectory))
        {
            AssertInstalledTree(InstallDirectory, expected);
            return;
        }

        string staging = Path.Combine(ProgramFiles, $"{ProgramDirectoryName}.setup-{Guid.NewGuid():N}");
        if (LockedPaths.Exists(staging))
            throw new InstallerRefusalException("The temporary installation path already exists. Run setup again.");
        new DirectoryInfo(staging).Create(NewDirectorySecurity());
        using (LockedPaths.PinDirectory(staging))
        {
            foreach (var relative in payload.AppFiles)
                WriteNew(staging, relative, stream => payload.CopyAppFile(relative, stream));
            foreach (string script in SetupScripts)
            {
                using var embedded = OpenSetupScript(script);
                WriteNew(staging, $"setup/{script}", embedded.CopyTo);
            }
        }
        AssertInstalledTree(staging, expected);
        // Publish an intact directory as one rename. Existing or interrupted trees are preserved.
        Directory.Move(staging, InstallDirectory);
        AssertInstalledTree(InstallDirectory, expected);
    }

    public string Provision(string? assignmentFile, bool replaceAssignment, bool uninstall)
    {
        using var installGuard = LockedPaths.PinDirectory(InstallDirectory);
        AssertAncestors(new DirectoryInfo(InstallDirectory));
        using var shell = OpenPowerShell();
        string shellFile = ShellPath;
        using var installed = LockedPaths.OpenFile(Path.Combine(InstallDirectory, Loader));

        var scriptHandles = new List<FileStream>();
        try
        {
            foreach (string script in SetupScripts)
            {
                string path = Path.Combine(InstallDirectory, "setup", script);
                AssertOperatingSystemAcl(new FileInfo(path), ancestorOnly: false);
                var actual = LockedPaths.OpenFile(path);
                scriptHandles.Add(actual);
                using var embedded = OpenSetupScript(script);
                if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(actual), SHA256.HashData(embedded)))
                    throw new InstallerRefusalException("The installed setup files do not match this installer.");
            }

            var start = CreateProvisioningStartInfo(shellFile, InstallDirectory);
            foreach (string arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File",
                Path.Combine(InstallDirectory, "setup", ProvisionScript), "-InstalledLoader", Path.Combine(InstallDirectory, Loader) })
                start.ArgumentList.Add(arg);
            if (uninstall)
                start.ArgumentList.Add("-Uninstall");
            else
            {
                start.ArgumentList.Add("-AssignmentFile");
                start.ArgumentList.Add(assignmentFile!);
                if (replaceAssignment) start.ArgumentList.Add("-ReplaceAssignment");
            }

            using var process = Process.Start(start) ??
                throw new InstallerRefusalException("Firmware Loader could not start its approved setup.");
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> errors = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            string diagnostics = errors.GetAwaiter().GetResult();
            string result = output.GetAwaiter().GetResult().Trim();
            if (process.ExitCode != 0)
                throw new InstallerRefusalException(PlainSetupErrors.FirstOrDefault(message =>
                    diagnostics.Contains(message, StringComparison.Ordinal)) ??
                    "Firmware Loader could not apply setup. Ask the installation administrator to check the approved Windows user, board assignment and retained setup state.");
            return result switch
            {
                "Daily worker setup is ready." => "Firmware Loader setup is ready for the assigned Windows user.",
                "Daily worker setup already matches the approved assignment." => "Firmware Loader setup already matches the approved assignment.",
                "Daily worker registration removed. Protected records were kept." when uninstall =>
                    "Firmware Loader removed its worker task and kept its installed files and protected records.",
                _ => throw new InstallerRefusalException("Firmware Loader could not confirm setup completion. Ask the installation administrator to check setup."),
            };
        }
        finally { foreach (var handle in scriptHandles) handle.Dispose(); }
    }

    internal static ProcessStartInfo CreateProvisioningStartInfo(string shellFile, string installDirectory)
    {
        var start = new ProcessStartInfo(shellFile)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = installDirectory,
        };
        string systemDirectory = Environment.SystemDirectory;
        string windowsDirectory = Path.GetDirectoryName(systemDirectory)!;
        string shellDirectory = Path.GetDirectoryName(shellFile)!;
        start.Environment.Clear();
        start.Environment["SystemRoot"] = windowsDirectory;
        start.Environment["WINDIR"] = windowsDirectory;
        // Supply the Windows volume for known-folder expansion without inheriting caller-supplied paths.
        start.Environment["SystemDrive"] = Path.GetPathRoot(windowsDirectory)!.TrimEnd(Path.DirectorySeparatorChar);
        // The native apphost uses this default when the runtime has no registry registration.
        start.Environment["ProgramFiles"] = ProgramFiles;
        // A native startup failure must return to the unattended provisioning process.
        start.Environment["DOTNET_DISABLE_GUI_ERRORS"] = "1";
        start.Environment["Path"] = systemDirectory + ";" + shellDirectory;
        start.Environment["PSModulePath"] = Path.Combine(shellDirectory, "Modules");
        start.Environment["TEMP"] = start.Environment["TMP"] = Path.Combine(installDirectory, "setup");
        return start;
    }

    private void ValidateSource()
    {
        string suppliedSetup = Path.Combine(LockedPaths.AbsoluteLocalPath(options.PackageRoot!), "installer", "FirmwareLoaderSetup.exe");
        if (!string.Equals(Environment.ProcessPath, suppliedSetup, StringComparison.OrdinalIgnoreCase))
            throw new InstallerRefusalException("Start the installer from the approved package's installer folder.");
        string assignment = LockedPaths.AbsoluteLocalPath(options.AssignmentFile!);
        string name = Path.GetFileName(assignment);
        if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
            !VerifiedPayload.IsPlainRelativePath(name) || !LockedPaths.Exists(assignment))
            throw new InstallerRefusalException("Supply the administrator-owned approved board assignment JSON file.");
    }

    private static FileStream OpenPowerShell()
    {
        if (!LockedPaths.Exists(ShellPath))
            throw new InstallerRefusalException("Firmware Loader setup requires approved PowerShell 7.5 or later in Program Files.");
        var shellRoot = new DirectoryInfo(Path.GetDirectoryName(ShellPath)!);
        AssertAncestors(shellRoot);
        int entries = 0;
        Visit(shellRoot);
        return LockedPaths.OpenFile(ShellPath);

        void Visit(DirectoryInfo directory)
        {
            using var guard = LockedPaths.PinDirectory(directory.FullName);
            foreach (var item in directory.EnumerateFileSystemInfos())
            {
                if (++entries > 32768)
                    throw new InstallerRefusalException("Firmware Loader setup could not verify the installed PowerShell program tree.");
                AssertOperatingSystemAcl(item, ancestorOnly: false);
                if (item is DirectoryInfo child) Visit(child);
            }
        }
    }

    private static Stream OpenSetupScript(string name) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream("LoaderSetup." + name) ??
        throw new InstallerRefusalException("This installer is missing an approved setup script.");

    private static Dictionary<string, string> GetInstallHashes(VerifiedPayload payload)
    {
        var expected = payload.AppFiles.ToDictionary(name => name, payload.AppHash, StringComparer.Ordinal);
        foreach (string script in SetupScripts)
        {
            using var embedded = OpenSetupScript(script);
            expected.Add($"setup/{script}", Convert.ToHexString(SHA256.HashData(embedded)));
        }
        return expected;
    }

    private static void WriteNew(string root, string name, Action<Stream> copy)
    {
        string path = Path.Combine(root, name.Replace('/', '\\'));
        string parent = root;
        string[] parts = name.Split('/');
        foreach (string segment in parts[..^1])
        {
            parent = Path.Combine(parent, segment);
            if (!LockedPaths.Exists(parent)) new DirectoryInfo(parent).Create(NewDirectorySecurity());
        }
        using var file = new FileInfo(path).Create(FileMode.CreateNew, FileSystemRights.FullControl, FileShare.None,
            65536, FileOptions.WriteThrough, NewFileSecurity());
        copy(file);
        file.Flush(true);
    }

    private static void AssertInstalledTree(string root, Dictionary<string, string> expected)
    {
        using var rootGuard = LockedPaths.PinDirectory(root);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var dirs = new HashSet<string>(StringComparer.Ordinal);
        foreach (string name in expected.Keys)
            for (int slash = name.LastIndexOf('/'); slash > 0; slash = name.LastIndexOf('/', slash - 1))
                dirs.Add(name[..slash]);
        Visit(root);
        if (seen.Count != expected.Count) RefuseInstalledTree();

        void Visit(string directory)
        {
            using var guard = LockedPaths.PinDirectory(directory);
            AssertExactInstallAcl(new DirectoryInfo(directory), DirectoryInheritance);
            foreach (var item in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                string name = Path.GetRelativePath(root, item.FullName).Replace('\\', '/');
                if (!VerifiedPayload.IsPlainRelativePath(name) || (item.Attributes & FileAttributes.ReparsePoint) != 0)
                    RefuseInstalledTree();
                if (item is DirectoryInfo)
                {
                    if (!dirs.Contains(name)) RefuseInstalledTree();
                    Visit(item.FullName);
                    continue;
                }
                if (!expected.TryGetValue(name, out string? hash) || !seen.Add(name)) RefuseInstalledTree();
                using var actual = LockedPaths.OpenFile(item.FullName);
                AssertExactInstallAcl(new FileInfo(item.FullName), InheritanceFlags.None);
                if (Convert.ToHexString(SHA256.HashData(actual)) != hash) RefuseInstalledTree();
            }
        }
    }

    private static void RefuseInstalledTree() => throw new InstallerRefusalException(
        "The installed Firmware Loader files or permissions differ from this package. Ask the installation administrator to review the retained files.");

    private const InheritanceFlags DirectoryInheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

    private static DirectorySecurity NewDirectorySecurity()
    {
        var security = new DirectorySecurity();
        ApplyInstallAcl(security, DirectoryInheritance);
        return security;
    }

    private static FileSecurity NewFileSecurity()
    {
        var security = new FileSecurity();
        ApplyInstallAcl(security, InheritanceFlags.None);
        return security;
    }

    private static void ApplyInstallAcl(FileSystemSecurity security, InheritanceFlags inheritance)
    {
        security.SetOwner(new SecurityIdentifier(AdministratorSid));
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (string sid in new[] { AdministratorSid, SystemSid, UsersSid })
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid), Rights(sid), inheritance,
                PropagationFlags.None, AccessControlType.Allow));
    }

    private static FileSystemRights Rights(string sid) => sid == UsersSid
        ? FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize : FileSystemRights.FullControl;

    private static FileSystemSecurity ReadAcl(FileSystemInfo item) => item is DirectoryInfo directory
        ? directory.GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner)
        : ((FileInfo)item).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);

    private static void AssertExactInstallAcl(FileSystemInfo item, InheritanceFlags inheritance)
    {
        var acl = ReadAcl(item);
        var rules = acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
        if (!acl.AreAccessRulesProtected || acl.GetOwner(typeof(SecurityIdentifier))?.Value != AdministratorSid ||
            rules.Length != 3 || new[] { AdministratorSid, SystemSid, UsersSid }.Any(sid =>
                rules.Count(rule => rule.IdentityReference.Value == sid && !rule.IsInherited &&
                    rule.AccessControlType == AccessControlType.Allow && rule.FileSystemRights == Rights(sid) &&
                    rule.InheritanceFlags == inheritance && rule.PropagationFlags == PropagationFlags.None) != 1))
            RefuseInstalledTree();
    }

    private static void AssertAncestors(DirectoryInfo directory)
    {
        AssertOperatingSystemAcl(directory, ancestorOnly: false);
        for (var parent = directory.Parent; parent is not null; parent = parent.Parent)
            AssertOperatingSystemAcl(parent, ancestorOnly: true);
    }

    private static bool OsTrusted(string? sid) => sid is AdministratorSid or SystemSid or TrustedInstallerSid;

    private static void AssertOperatingSystemAcl(FileSystemInfo item, bool ancestorOnly)
    {
        if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InstallerRefusalException("Firmware Loader setup refused a redirected program path.");
        var acl = ReadAcl(item);
        var descriptor = new RawSecurityDescriptor(acl.GetSecurityDescriptorBinaryForm(), 0);
        const FileSystemRights change = FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles |
            FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership | FileSystemRights.WriteAttributes |
            FileSystemRights.WriteExtendedAttributes;
        var disallowed = ancestorOnly ? change : change | FileSystemRights.Write;
        if (descriptor.DiscretionaryAcl is null || !OsTrusted(acl.GetOwner(typeof(SecurityIdentifier))?.Value) ||
            acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().Any(rule =>
                rule.AccessControlType == AccessControlType.Allow && !OsTrusted(rule.IdentityReference.Value) &&
                (rule.PropagationFlags & PropagationFlags.InheritOnly) == 0 &&
                ((rule.FileSystemRights & disallowed) != 0 || ((int)rule.FileSystemRights & 0x50000000) != 0)))
            throw new InstallerRefusalException("Firmware Loader setup requires administrator-managed program files.");
    }
}
