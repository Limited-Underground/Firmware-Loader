using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using LimitedUnderground.FirmwareLoader;

internal static class PackagedFlashRuntimeTests
{
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } =
    [
        ("packaged flash helper uses the runtime beside the Loader", ValidLayout),
        ("packaged flash helper refuses a modified or added runtime file before launch", RefusesMutation),
        ("packaged flash runtime refuses unsafe or duplicate pins", RefusesUnsafePins),
        ("installer setup checks privilege and runtime before preparing storage", SetupOrder)
    ];

    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Packaged runtime fixture check failed.");
    }

    private sealed class Fixture
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "LUF.FlashRuntime." + Guid.NewGuid().ToString("N"));
        internal Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);
        internal Fixture()
        {
            foreach (string path in new[] { "python.exe", "python314.dll", "python314.zip", "python314._pth",
                         "packages/esptool/__init__.py", "packages/serial/__init__.py" })
            {
                string full = Path.Combine(Root, path);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                byte[] bytes = System.Text.Encoding.ASCII.GetBytes("fixture/" + path);
                File.WriteAllBytes(full, bytes);
                Files.Add(path, Convert.ToHexString(SHA256.HashData(bytes)));
            }
        }

        internal PackagedFlashRuntime? Open()
        {
            using var pins = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new
            {
                format = "LU-FLASH-RUNTIME-FILES-V1",
                files = Files
            }));
            return PackagedFlashRuntime.Open(Root, pins);
        }
    }

    private static void ValidLayout()
    {
        var fixture = new Fixture();
        var runtime = fixture.Open();
        Require(runtime is not null && runtime.Matches());
        var install = runtime!.CreateHelperInstall(Path.Combine(fixture.Root, "helper.py"));
        Require(ReferenceEquals(install.Runtime, runtime) && install.PythonPath == Path.Combine(fixture.Root, "python.exe") &&
                install.PythonSha256 == fixture.Files["python.exe"] && install.HelperSha256 == BenchFlash.HelperSha256);
    }

    private static void RefusesMutation()
    {
        var fixture = new Fixture();
        var runtime = fixture.Open()!;
        File.AppendAllText(Path.Combine(fixture.Root, "packages/esptool/__init__.py"), "changed");
        Require(fixture.Open() is null && !runtime.Matches());
        try
        {
            new ProcessFlashHelperRunner(runtime.CreateHelperInstall(Path.Combine(fixture.Root, "helper.py")))
                .RunAsync(["--help"], TimeSpan.FromSeconds(1), CancellationToken.None).GetAwaiter().GetResult();
            throw new Exception("Modified runtime did not refuse.");
        }
        catch (InvalidOperationException ex) when (ex.Message == "flash helper files do not match their pinned SHA-256") { }

        var second = new Fixture();
        File.WriteAllText(Path.Combine(second.Root, "packages/unexpected.py"), "fixture");
        Require(second.Open() is null);
    }

    private static void RefusesUnsafePins()
    {
        var fixture = new Fixture();
        fixture.Files.Add("../outside.py", new string('A', 64));
        Require(fixture.Open() is null);
        fixture.Files.Remove("../outside.py");
        fixture.Files.Add("PYTHON.EXE", fixture.Files["python.exe"]);
        Require(fixture.Open() is null);
    }

    private static void SetupOrder()
    {
        int checks = 0, stores = 0;
        bool Verify(bool result) { checks++; return result; }
        void Prepare() => stores++;
        Require(LoaderMachineSetup.Install(() => false, () => Verify(true), Prepare) == LoaderSetupResult.NeedsAdministrator);
        Require(checks == 0 && stores == 0);
        Require(LoaderMachineSetup.Install(() => true, () => Verify(false), Prepare) == LoaderSetupResult.RuntimeUnavailable);
        Require(checks == 1 && stores == 0);
        Require(LoaderMachineSetup.Install(() => true, () => Verify(true), Prepare) == LoaderSetupResult.Ready);
        Require(checks == 2 && stores == 1);
        Require(LoaderMachineSetup.Install(() => true, () => true, () => throw new IOException()) == LoaderSetupResult.StoreUnavailable);
    }
}
