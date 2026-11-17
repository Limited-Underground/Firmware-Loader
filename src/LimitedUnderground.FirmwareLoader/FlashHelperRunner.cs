using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace LimitedUnderground.FirmwareLoader;

// L7 step 1 (D-063). The flash helper is a separate GPL program (tools/flash-helper) that drives
// esptool. The Loader only starts it and reads one JSON line. This file and
// EsptoolHostUpdateAdapter.cs are the only sources allowed to start a process.
internal sealed record FlashHelperResult(int ExitCode, JsonElement Json);

internal interface IFlashHelperRunner
{
    // Returns only after the helper process has ended (observable quiescence). Cancellation or the
    // time limit kills the whole process tree and still waits for it to end before throwing.
    Task<FlashHelperResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan limit, CancellationToken cancellation);
}

// Exact files the helper runs from, each pinned by SHA-256 (uppercase hex). Nothing runs if any differs.
internal sealed record FlashHelperInstall(string PythonPath, string PythonSha256, string HelperPath, string HelperSha256)
{
    internal PackagedFlashRuntime? Runtime { get; init; }
}

internal sealed class ProcessFlashHelperRunner : IFlashHelperRunner
{
    internal static readonly TimeSpan MaximumLimit = TimeSpan.FromMinutes(10);
    internal const string PackagedWriterModule = "packages/esptool/__init__.py";
    private readonly FlashHelperInstall install;

    internal ProcessFlashHelperRunner(FlashHelperInstall install) => this.install = install;

    internal static bool FileMatches(string path, string sha256)
    {
        try { return File.Exists(path) && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) == sha256; }
        catch (Exception) { return false; }
    }

    public async Task<FlashHelperResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan limit, CancellationToken cancellation)
    {
        if (limit <= TimeSpan.Zero || limit > MaximumLimit) throw new ArgumentOutOfRangeException(nameof(limit));
        cancellation.ThrowIfCancellationRequested();
        if (install.Runtime?.Matches() == false || !FileMatches(install.PythonPath, install.PythonSha256) ||
            !FileMatches(install.HelperPath, install.HelperSha256))
            throw new InvalidOperationException("flash helper files do not match their pinned SHA-256");
        var info = new ProcessStartInfo(install.PythonPath)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        info.ArgumentList.Add("-I");
        if (install.Runtime is not null)
        {
            info.ArgumentList.Add("-B");
            info.WorkingDirectory = Path.GetDirectoryName(install.PythonPath)!;
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            info.Environment["PATH"] = string.Join(Path.PathSeparator, info.WorkingDirectory,
                Environment.SystemDirectory, windows);
        }
        info.ArgumentList.Add(install.HelperPath);
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        cancellation.ThrowIfCancellationRequested();
        using var process = new Process { StartInfo = info };
        if (!process.Start()) throw new InvalidOperationException("flash helper did not start");
        var stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(limit);
        try { await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (Exception) { }
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        string output = await stdout.ConfigureAwait(false);
        _ = await stderr.ConfigureAwait(false);
        return new(process.ExitCode, FlashHelperOutput.Parse(output));
    }
}

internal static class FlashHelperOutput
{
    // Exactly one JSON object line; anything else is a failure.
    internal static JsonElement Parse(string output)
    {
        var lines = output.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();
        if (lines.Length != 1 || lines[0].Length > 65536) throw new InvalidDataException("flash helper output");
        using var document = JsonDocument.Parse(lines[0]);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("flash helper output");
        return document.RootElement.Clone();
    }
}
