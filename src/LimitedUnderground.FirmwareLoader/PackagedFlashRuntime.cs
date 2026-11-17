using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace LimitedUnderground.FirmwareLoader;

// The installed files are checked against pins embedded in the packaged Loader build.
internal sealed class PackagedFlashRuntime
{
    internal const string ResourceName = "LimitedUnderground.FirmwareLoader.FlashRuntimePins";
    internal const string DirectoryName = "flash-runtime";
    private readonly string root;
    private readonly Dictionary<string, string> hashes;

    private PackagedFlashRuntime(string root, Dictionary<string, string> hashes)
    {
        this.root = root;
        this.hashes = hashes;
    }

    internal static PackagedFlashRuntime? OpenInstalled()
    {
        using var pins = typeof(PackagedFlashRuntime).Assembly.GetManifestResourceStream(ResourceName);
        return pins is null ? null : Open(Path.Combine(AppContext.BaseDirectory, DirectoryName), pins);
    }

    internal static PackagedFlashRuntime? Open(string root, Stream pins)
    {
        try
        {
            if (!pins.CanSeek || pins.Length is <= 0 or > 4 * 1024 * 1024)
                return null;
            using var document = JsonDocument.Parse(pins, new JsonDocumentOptions { MaxDepth = 4 });
            var record = document.RootElement;
            if (record.ValueKind != JsonValueKind.Object || record.EnumerateObject().Count() != 2 ||
                record.GetProperty("format").GetString() != "LU-FLASH-RUNTIME-FILES-V1")
                return null;

            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in record.GetProperty("files").EnumerateObject())
            {
                var digest = item.Value.GetString();
                if (files.Count >= 16384 || !IsRelativeFile(item.Name) ||
                    digest is not { Length: 64 } || !digest.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F') ||
                    !files.TryAdd(item.Name, digest))
                    return null;
            }

            if (!files.ContainsKey("python.exe") || !files.ContainsKey("python314._pth") ||
                !files.ContainsKey("python314.zip") || !files.ContainsKey("python314.dll") ||
                !files.ContainsKey(ProcessFlashHelperRunner.PackagedWriterModule) || !files.ContainsKey("packages/serial/__init__.py"))
                return null;

            var runtime = new PackagedFlashRuntime(Path.GetFullPath(root), files);
            return runtime.Matches() ? runtime : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or
            ArgumentException or InvalidOperationException or KeyNotFoundException or NotSupportedException)
        {
            return null;
        }
    }

    internal FlashHelperInstall CreateHelperInstall(string helperPath) =>
        new(Path.Combine(root, "python.exe"), hashes["python.exe"], helperPath, BenchFlash.HelperSha256)
        {
            Runtime = this
        };

    internal bool Matches()
    {
        try
        {
            for (var directory = new DirectoryInfo(root); directory is not null; directory = directory.Parent)
                if ((directory.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != FileAttributes.Directory)
                    return false;
            var pending = new Stack<string>();
            pending.Push(root);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Bound traversal even if an unexpected directory has no files.
            int entries = 0;
            long bytes = 0;
            while (pending.TryPop(out var directory))
            {
                foreach (var path in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (++entries > 32768) return false;
                    var attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) return false;
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        pending.Push(path);
                        continue;
                    }

                    string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                    if (!seen.Add(relative) || !hashes.TryGetValue(relative, out var digest)) return false;
                    using var file = File.OpenRead(path);
                    if (file.Length > 300L * 1024 * 1024 - bytes) return false;
                    bytes += file.Length;
                    if (!string.Equals(Convert.ToHexString(SHA256.HashData(file)), digest, StringComparison.Ordinal))
                        return false;
                }
            }
            return seen.Count == hashes.Count;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool IsRelativeFile(string path) =>
        path.Length is > 0 and <= 240 && path.Split('/').All(part =>
            part.Length > 0 && !part.EndsWith('.') && !part.EndsWith(' ') &&
            part.All(c => c is >= ' ' and <= '~' && !"<>:\"\\|?*".Contains(c)));
}
