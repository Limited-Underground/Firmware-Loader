namespace LimitedUnderground.FirmwareLoader.Setup;

internal sealed record InstallerOptions(string? PackageRoot, string? ManifestSha256, string? AssignmentFile,
    bool ReplaceAssignment = false, bool Uninstall = false)
{
    internal static InstallerOptions? Parse(string[] args)
    {
        if (args is ["--uninstall"]) return new(null, null, null, Uninstall: true);
        string? package = null, manifest = null, assignment = null;
        bool replace = false;
        for (int i = 0; i < args.Length; i++)
        {
            string option = args[i];
            if (option == "--replace-assignment" && !replace)
            {
                replace = true;
                continue;
            }
            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                return null;
            switch (option)
            {
                case "--package" when package is null: package = args[++i]; break;
                case "--manifest-sha256" when manifest is null: manifest = args[++i]; break;
                case "--assignment-file" when assignment is null: assignment = args[++i]; break;
                default: return null;
            }
        }
        return package is null || manifest is null || assignment is null ? null : new(package, manifest, assignment, replace);
    }
}

internal sealed record InstallResult(int ExitCode, string Message);
internal sealed class InstallerRefusalException(string message) : Exception(message);

internal interface IInstallerHost
{
    bool IsAdministrator { get; }
    void EnsureInstalled(VerifiedPayload payload);
    string Provision(string? assignmentFile, bool replaceAssignment, bool uninstall);
}

internal static class InstallerWorkflow
{
    internal const string Usage = "Use --package <directory> --manifest-sha256 <approved SHA-256> --assignment-file <approved JSON> [--replace-assignment], or --uninstall.";

    internal static InstallResult Run(InstallerOptions options, IInstallerHost host)
    {
        if (!host.IsAdministrator)
            return new(3, "Run Firmware Loader setup as a Windows administrator.");
        if (options.Uninstall && (options.PackageRoot is not null || options.ManifestSha256 is not null ||
            options.AssignmentFile is not null || options.ReplaceAssignment))
            return new(2, Usage);

        if (!options.Uninstall)
        {
            if (string.IsNullOrWhiteSpace(options.AssignmentFile))
                return new(2, "Supply the administrator-owned approved board assignment JSON file.");
            if (string.IsNullOrWhiteSpace(options.PackageRoot) || !VerifiedPayload.IsDigest(options.ManifestSha256))
                return new(2, "Supply the package directory and the approved uppercase manifest SHA-256.");
        }

        try
        {
            if (!options.Uninstall)
            {
                using var payload = VerifiedPayload.Open(options.PackageRoot!, options.ManifestSha256!);
                host.EnsureInstalled(payload);
            }
            return new(0, host.Provision(options.AssignmentFile, options.ReplaceAssignment, options.Uninstall));
        }
        catch (InstallerRefusalException ex) { return new(1, ex.Message); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
            InvalidOperationException or System.ComponentModel.Win32Exception or System.Security.SecurityException)
        {
            return new(1, "Firmware Loader setup could not verify or prepare its files. Ask the installation administrator to check the package and setup.");
        }
    }
}
