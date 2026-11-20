using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LimitedUnderground.FirmwareLoader.Setup;

// Invoke the installer workflow in process through an inert host. These fixtures never
// start the setup executable, invoke provisioning scripts or reach a device adapter.
if (args.Length != 1 || !Path.IsPathFullyQualified(args[0]) || !Directory.Exists(args[0]))
    throw new InvalidOperationException("Specify an existing absolute host evidence directory.");

var cases = new (string Name, Action<PackageFixture> Run)[]
{
    ("non-administrator refused before changes", NonAdministrator),
    ("missing payload file refused before changes", MissingFile),
    ("altered same-size payload refused before changes", AlteredFile),
    ("unlisted payload file refused before changes", ExtraFile),
    ("wrong outer manifest digest refused before changes", WrongDigest),
    ("unsafe payload record refused before changes", UnsafePath),
    ("missing assignment stops setup with guidance", MissingAssignment),
    ("malformed assignment refusal is passed through", MalformedAssignment),
    ("conflicting assignment requires explicit replacement", ConflictingAssignment),
    ("same-input rerun keeps protected state and staged app identity", SameInputs),
    ("verified payload denies a concurrent writer during staging", PinnedDuringStaging),
    ("uninstall removes task and retains protected state", Uninstall),
};

int failures = 0;
for (int index = 0; index < cases.Length; index++)
{
    var test = cases[index];
    try
    {
        var folder = Path.Combine(args[0], $"case-{index + 1:00}");
        Directory.CreateDirectory(folder);
        test.Run(new PackageFixture(folder));
        Console.WriteLine($"PASS: {test.Name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL: {test.Name}: {ex.Message}");
    }
}

if (failures != 0) return 1;
Console.WriteLine($"PASS: {cases.Length} installer workflow fixture groups; injected host, no installer execution.");
return 0;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void Refused(InstallResult result, string detail)
{
    Require(result.ExitCode != 0, "Workflow unexpectedly succeeded.");
    Require(result.Message.Contains(detail, StringComparison.OrdinalIgnoreCase), "Expected a plain " + detail + " refusal.");
}

static void Untouched(FakeInstallerHost host)
{
    Require(host.InstallCalls == 0 && host.ProvisionCalls == 0 && host.AppFiles.Count == 0 &&
        !host.TaskRegistered && host.Protected is null, "Refusal crossed the installation boundary.");
}

static void NonAdministrator(PackageFixture fixture)
{
    var host = new FakeInstallerHost { IsAdministrator = false };
    Refused(InstallerWorkflow.Run(fixture.Options(), host), "administrator");
    Untouched(host);
}

static void MissingFile(PackageFixture fixture)
{
    var host = new FakeInstallerHost();
    var options = fixture.Options(omit: PackageFixture.Helper);
    Refused(InstallerWorkflow.Run(options, host), "package");
    Untouched(host);
}

static void AlteredFile(PackageFixture fixture)
{
    var host = new FakeInstallerHost();
    var options = fixture.Options(alter: PackageFixture.Helper);
    Refused(InstallerWorkflow.Run(options, host), "package");
    Untouched(host);
}

static void ExtraFile(PackageFixture fixture)
{
    var host = new FakeInstallerHost();
    var options = fixture.Options(extra: true);
    Refused(InstallerWorkflow.Run(options, host), "package");
    Untouched(host);
}

static void WrongDigest(PackageFixture fixture)
{
    var host = new FakeInstallerHost();
    var options = fixture.Options() with { ManifestSha256 = new string('F', 64) };
    Refused(InstallerWorkflow.Run(options, host), "manifest");
    Untouched(host);
}

static void UnsafePath(PackageFixture fixture)
{
    var host = new FakeInstallerHost();
    var options = fixture.Options(unsafeEntry: true);
    Refused(InstallerWorkflow.Run(options, host), "package");
    Untouched(host);
}

static void MissingAssignment(PackageFixture fixture)
{
    var host = new FakeInstallerHost();
    var options = fixture.Options() with { AssignmentFile = null };
    Refused(InstallerWorkflow.Run(options, host), "assignment");
    Untouched(host);
}

static void MalformedAssignment(PackageFixture fixture)
{
    var host = new FakeInstallerHost();
    var options = fixture.Options();
    File.WriteAllText(options.AssignmentFile!, "{ incomplete fixture");
    Refused(InstallerWorkflow.Run(options, host), "assignment");
    Require(host.ProvisionCalls == 1 && !host.TaskRegistered && host.Protected is null,
        "Malformed delegated assignment affected protected state.");
}

static void ConflictingAssignment(PackageFixture fixture)
{
    var host = new FakeInstallerHost();
    var options = fixture.Options();
    Require(InstallerWorkflow.Run(options, host).ExitCode == 0, "Fixture setup failed.");
    var preserved = host.Protected!;
    string different = fixture.WriteAssignment("replacement.json", "next-inert-approval");

    Refused(InstallerWorkflow.Run(options with { AssignmentFile = different }, host), "assignment");
    Require(ReferenceEquals(preserved, host.Protected) && preserved.Assignment == "initial-inert-approval" &&
        host.TaskRegistered, "Conflicting assignment replaced preserved state.");

    var replacement = InstallerWorkflow.Run(options with { AssignmentFile = different, ReplaceAssignment = true }, host);
    Require(replacement.ExitCode == 0 && ReferenceEquals(preserved, host.Protected) &&
        preserved.Assignment == "next-inert-approval" && preserved.Journal == "retained journal",
        "Explicit replacement did not preserve the journal or pass through the flag.");
}

static void SameInputs(PackageFixture fixture)
{
    var host = new FakeInstallerHost();
    var options = fixture.Options();
    Require(InstallerWorkflow.Run(options, host).ExitCode == 0, "Initial setup failed.");
    var preserved = host.Protected!;
    Require(host.TaskRegistered && preserved.Assignment == "initial-inert-approval", "Initial assignment did not reach setup.");
    Require(host.AppFiles.Count == PackageFixture.AppFileCount &&
        host.AppFiles["flash-helper/lu_flash_helper.py"] == PackageFixture.Digest(PackageFixture.Bytes(PackageFixture.Helper)),
        "Installer did not copy exactly the admitted app bytes.");
    Require(InstallerWorkflow.Run(options, host).ExitCode == 0 && ReferenceEquals(preserved, host.Protected) &&
        preserved.Journal == "retained journal" && host.TaskRegistered && host.AppWrites == 1 &&
        host.InstallCalls == 2 && host.ProvisionCalls == 2, "Same-input setup rewrote preserved state.");
}

static void PinnedDuringStaging(PackageFixture fixture)
{
    var options = fixture.Options();
    string helper = Path.Combine(options.PackageRoot!, PackageFixture.Helper);
    bool writerDenied = false;
    var host = new FakeInstallerHost
    {
        BeforeInstall = () =>
        {
            try { using var writer = new FileStream(helper, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); }
            catch (IOException) { writerDenied = true; }
        },
    };
    Require(InstallerWorkflow.Run(options, host).ExitCode == 0 && writerDenied && host.AppWrites == 1 &&
        host.AppFiles["flash-helper/lu_flash_helper.py"] == PackageFixture.Digest(PackageFixture.Bytes(PackageFixture.Helper)),
        "Approved payload handles did not protect the inert staging input.");
}

static void Uninstall(PackageFixture fixture)
{
    var host = new FakeInstallerHost();
    Require(InstallerWorkflow.Run(fixture.Options(), host).ExitCode == 0, "Initial setup failed.");
    var preserved = host.Protected!;
    var result = InstallerWorkflow.Run(new InstallerOptions(null, null, null, Uninstall: true), host);
    Require(result.ExitCode == 0 && !host.TaskRegistered && ReferenceEquals(preserved, host.Protected) &&
        preserved.Journal == "retained journal" && host.InstallCalls == 1 && host.ProvisionCalls == 2,
        "Uninstall altered app or protected records or retained the task.");
}

sealed class PackageFixture(string folder)
{
    internal const string Helper = "app/flash-helper/lu_flash_helper.py";
    internal const int AppFileCount = 11;
    private const string App = "app/LimitedUnderground.FirmwareLoader";
    private static readonly string[] Files =
    [
        App + ".exe", App + ".dll", App + ".deps.json", App + ".runtimeconfig.json", Helper,
        "app/flash-runtime/python.exe", "app/flash-runtime/python314._pth",
        "app/flash-runtime/python314.zip", "app/flash-runtime/python314.dll",
        "app/flash-runtime/packages/esptool/__init__.py", "app/flash-runtime/packages/serial/__init__.py",
        "firmware/install/release.json", "firmware/install/release.sig", "firmware/install/raw.bin", "firmware/install/padded.bin",
        "firmware/restore/release.json", "firmware/restore/release.sig", "firmware/restore/raw.bin", "firmware/restore/padded.bin",
        "installer/FirmwareLoaderSetup.exe", "metadata/runtime-files.json", "notices/NOTICE.txt", "README.md", "LICENSE"
    ];

    internal static byte[] Bytes(string name) => Encoding.UTF8.GetBytes("Inert host fixture: " + name);
    internal static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    internal string WriteAssignment(string leaf, string assignment)
    {
        string path = Path.Combine(folder, leaf);
        File.WriteAllText(path, JsonSerializer.Serialize(new { assignment }));
        return path;
    }

    internal InstallerOptions Options(string? omit = null, string? alter = null, bool extra = false, bool unsafeEntry = false)
    {
        string package = Path.Combine(folder, "package");
        Directory.CreateDirectory(package);
        var records = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (string name in Files)
        {
            byte[] bytes = Bytes(name);
            records.Add(name, new { size = bytes.Length, sha256 = Digest(bytes) });
            if (name == omit) continue;
            if (name == alter) bytes[0] ^= 1;
            WriteFile(package, name, bytes);
        }
        if (extra) WriteFile(package, "app/unlisted.txt", Bytes("unlisted"));
        if (unsafeEntry) records.Add("app/../unlisted.txt", new { size = 1, sha256 = Digest([1]) });
        byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(new
        {
            format = "LU-LOADER-TESTER-PACKAGE-V1",
            mode = "qualification-candidate",
            files = records,
        });
        WriteFile(package, "package-manifest.json", manifest);
        return new InstallerOptions(package, Digest(manifest), WriteAssignment("assignment.json", "initial-inert-approval"));
    }

    private static void WriteFile(string root, string name, byte[] bytes)
    {
        string path = Path.Combine(root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }
}

sealed class FakeInstallerHost : IInstallerHost
{
    public bool IsAdministrator { get; init; } = true;
    internal Action? BeforeInstall { get; init; }
    internal int InstallCalls { get; private set; }
    internal int AppWrites { get; private set; }
    internal int ProvisionCalls { get; private set; }
    internal bool TaskRegistered { get; private set; }
    internal ProtectedFixture? Protected { get; private set; }
    internal Dictionary<string, string> AppFiles { get; private set; } = new(StringComparer.Ordinal);

    public void EnsureInstalled(VerifiedPayload payload)
    {
        InstallCalls++;
        BeforeInstall?.Invoke();
        var current = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string name in payload.AppFiles)
        {
            if (name.StartsWith("firmware/", StringComparison.Ordinal) || name.StartsWith("installer/", StringComparison.Ordinal))
                throw new InvalidOperationException("Install attempted to stage a non-app path.");
            using var buffer = new MemoryStream();
            payload.CopyAppFile(name, buffer);
            current.Add(name, PackageFixture.Digest(buffer.ToArray()));
        }
        if (AppFiles.Count == 0)
        {
            AppFiles = current;
            AppWrites++;
        }
        else if (!AppFiles.OrderBy(file => file.Key).SequenceEqual(current.OrderBy(file => file.Key)))
        {
            throw new InstallerRefusalException("The installed fixture payload differs.");
        }
    }

    public string Provision(string? assignmentFile, bool replaceAssignment, bool uninstall)
    {
        ProvisionCalls++;
        if (uninstall)
        {
            if (assignmentFile is not null || replaceAssignment)
                throw new InvalidOperationException("Uninstall was passed a mutating assignment request.");
            TaskRegistered = false;
            return "Fixture task removed. Protected records kept.";
        }
        if (assignmentFile is null || !File.Exists(assignmentFile))
            throw new InstallerRefusalException("The assignment file is missing.");
        string assignment;
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllBytes(assignmentFile));
            assignment = json.RootElement.GetProperty("assignment").GetString() ?? throw new JsonException();
        }
        catch (JsonException) { throw new InstallerRefusalException("The assignment file is malformed."); }

        if (Protected is null)
            Protected = new ProtectedFixture(assignment);
        else if (Protected.Assignment != assignment)
        {
            if (!replaceAssignment)
                throw new InstallerRefusalException("The assignment conflicts with preserved setup.");
            Protected.Assignment = assignment;
        }
        TaskRegistered = true;
        return "Fixture setup ready.";
    }
}

sealed class ProtectedFixture(string assignment)
{
    internal string Assignment { get; set; } = assignment;
    internal string Journal { get; } = "retained journal";
}
