namespace LimitedUnderground.FirmwareLoader.Setup;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess)
        {
            Console.Error.WriteLine("Firmware Loader setup requires 64-bit Windows.");
            return 3;
        }

        var options = InstallerOptions.Parse(args);
        InstallResult result = options is null
            ? new(2, InstallerWorkflow.Usage)
            : InstallerWorkflow.Run(options, new WindowsInstallerHost(options));
        (result.ExitCode == 0 ? Console.Out : Console.Error).WriteLine(result.Message);
        return result.ExitCode;
    }
}
