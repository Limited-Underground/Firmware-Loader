using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LimitedUnderground.FirmwareLoader.Setup;

internal sealed class VerifiedPayload : IDisposable
{
    private const string ManifestName = "package-manifest.json";
    private const string AppPrefix = "app/";
    private const int MaximumFiles = 16384;
    private const int MaximumEntries = 32768;
    private const int MaximumManifestBytes = 4 * 1024 * 1024;
    private const long MaximumFileBytes = 512L * 1024 * 1024;
    private const string LoaderName = "LimitedUnderground.FirmwareLoader";
    private static readonly string[] Required =
    [
        "README.md", "LICENSE", "installer/FirmwareLoaderSetup.exe", "metadata/runtime-files.json",
        "notices/NOTICE.txt", "app/" + LoaderName + ".exe", "app/" + LoaderName + ".dll",
        "app/" + LoaderName + ".deps.json", "app/" + LoaderName + ".runtimeconfig.json",
        "app/flash-helper/lu_flash_helper.py", "app/flash-runtime/python.exe",
        "app/flash-runtime/python314._pth", "app/flash-runtime/python314.zip",
        "app/flash-runtime/python314.dll", "app/flash-runtime/packages/esptool/__init__.py",
        "app/flash-runtime/packages/serial/__init__.py", "firmware/install/release.json",
        "firmware/install/raw.bin", "firmware/install/padded.bin", "firmware/install/release.sig",
        "firmware/restore/release.json", "firmware/restore/raw.bin", "firmware/restore/padded.bin",
        "firmware/restore/release.sig",
    ];
    private static readonly string[] ForbiddenExtensions =
    [
        ".pem", ".pfx", ".p12", ".snk", ".kdbx", ".cer", ".crt", ".cert", ".der", ".p7b", ".p7c",
    ];
    private static readonly Regex DeviceName = new(@"\A(?:CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|\z)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private readonly Dictionary<string, (FileStream Stream, string Sha256)> files = new(StringComparer.Ordinal);
    internal IReadOnlyList<string> AppFiles => Array.AsReadOnly(files.Keys.ToArray());

    internal static bool IsDigest(string? text) =>
        text is { Length: 64 } && text.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F') &&
        text.Any(c => c != '0');

    internal static VerifiedPayload Open(string packageRoot, string expectedManifestSha256)
    {
        var result = new VerifiedPayload();
        try
        {
            string root = LockedPaths.AbsoluteLocalPath(packageRoot);
            using var parent = LockedPaths.PinDirectory(root);
            using var manifest = LockedPaths.OpenFile(Path.Combine(root, ManifestName));
            if (manifest.Length is < 2 or > MaximumManifestBytes)
                throw new InstallerRefusalException("The package manifest has an invalid size.");
            byte[] manifestBytes = new byte[(int)manifest.Length];
            manifest.ReadExactly(manifestBytes);
            if (!IsDigest(expectedManifestSha256) ||
                Convert.ToHexString(SHA256.HashData(manifestBytes)) != expectedManifestSha256)
                throw new InstallerRefusalException("The package manifest does not match the approved SHA-256.");

            Dictionary<string, (long Size, string Sha256)> records = ReadRecords(manifestBytes);
            result.VerifyTree(root, records);
            return result;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            result.Dispose();
            throw new InstallerRefusalException("The package manifest has an invalid layout.");
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private static Dictionary<string, (long, string)> ReadRecords(byte[] bytes)
    {
        using var manifest = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 5 });
        var root = manifest.RootElement;
        if (!HasKeys(root, "format", "mode", "files") ||
            root.GetProperty("format").GetString() != "LU-LOADER-TESTER-PACKAGE-V1" ||
            root.GetProperty("mode").GetString() != "qualification-candidate" ||
            root.GetProperty("files").ValueKind != JsonValueKind.Object)
            throw new InstallerRefusalException("Use an approved qualification-candidate package.");

        var records = new Dictionary<string, (long, string)>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in root.GetProperty("files").EnumerateObject())
        {
            if (seen.Count >= MaximumFiles || !IsAllowedPackageFile(record.Name) || !seen.Add(record.Name) ||
                !HasKeys(record.Value, "size", "sha256"))
                throw new InstallerRefusalException("The package manifest lists an unsafe, duplicate or unexpected file.");
            var size = record.Value.GetProperty("size");
            string? hash = record.Value.GetProperty("sha256").GetString();
            if (size.ValueKind != JsonValueKind.Number || !size.TryGetInt64(out long length) ||
                length is < 1 or > MaximumFileBytes || !IsDigest(hash))
                throw new InstallerRefusalException("A package manifest file entry has an invalid size or SHA-256.");
            records.Add(record.Name, (length, hash!));
        }
        if (Required.Any(name => !records.ContainsKey(name)))
            throw new InstallerRefusalException("The approved package is missing a required manifest entry.");
        return records;
    }

    private static bool HasKeys(JsonElement element, params string[] keys)
    {
        if (element.ValueKind != JsonValueKind.Object) return false;
        string[] actual = element.EnumerateObject().Select(property => property.Name).ToArray();
        return actual.Length == keys.Length && keys.All(actual.Contains);
    }

    private static bool IsAllowedPackageFile(string path)
    {
        if (!IsPlainRelativePath(path)) return false;
        if (path.StartsWith(AppPrefix, StringComparison.Ordinal))
        {
            string relative = path[AppPrefix.Length..];
            return !relative.Equals("setup", StringComparison.OrdinalIgnoreCase) &&
                !relative.StartsWith("setup/", StringComparison.OrdinalIgnoreCase);
        }
        return path.StartsWith("notices/", StringComparison.Ordinal) || Required.Contains(path);
    }

    internal static bool IsPlainRelativePath(string path)
    {
        if (path.Length is < 1 or > 240) return false;
        foreach (string part in path.Split('/'))
        {
            if (part.Length == 0 || part[0] == '.' || part[^1] is ' ' or '.' ||
                part.Any(c => c is < ' ' or > '~' or '<' or '>' or ':' or '"' or '\\' or '|' or '?' or '*') ||
                DeviceName.IsMatch(part) ||
                part.EndsWith(".key", StringComparison.OrdinalIgnoreCase) ||
                part.Contains(".key.", StringComparison.OrdinalIgnoreCase) ||
                ForbiddenExtensions.Contains(Path.GetExtension(part), StringComparer.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    private void VerifyTree(string root, Dictionary<string, (long Size, string Sha256)> expected)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var directories = new HashSet<string>(StringComparer.Ordinal);
        foreach (string name in expected.Keys)
            for (int slash = name.LastIndexOf('/'); slash > 0; slash = name.LastIndexOf('/', slash - 1))
                directories.Add(name[..slash]);

        int entries = 0;
        VerifyDirectory(root);
        if (seen.Count != expected.Count)
            throw new InstallerRefusalException("The approved package is missing one or more listed files.");

        void VerifyDirectory(string directory)
        {
            using var pin = LockedPaths.PinDirectory(directory);
            foreach (var item in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                string name = Path.GetRelativePath(root, item.FullName).Replace('\\', '/');
                if (++entries > MaximumEntries || !IsPlainRelativePath(name) ||
                    (item.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InstallerRefusalException("The package contains an unsafe, redirected or excessive entry.");
                if ((item.Attributes & FileAttributes.Directory) != 0)
                {
                    if (!directories.Contains(name))
                        throw new InstallerRefusalException("The package contains an unexpected directory.");
                    VerifyDirectory(item.FullName);
                    continue;
                }

                if (name == ManifestName) continue;
                if (!expected.TryGetValue(name, out var record) || !seen.Add(name))
                    throw new InstallerRefusalException("The package contains an unexpected file.");
                var file = LockedPaths.OpenFile(item.FullName);
                bool retain = false;
                try
                {
                    if (file.Length != record.Size || Convert.ToHexString(SHA256.HashData(file)) != record.Sha256)
                        throw new InstallerRefusalException("A package file does not match its approved size or SHA-256.");
                    if (name.StartsWith(AppPrefix, StringComparison.Ordinal))
                    {
                        files.Add(name[AppPrefix.Length..], (file, record.Sha256));
                        retain = true;
                    }
                }
                finally { if (!retain) file.Dispose(); }
            }
        }
    }

    internal void CopyAppFile(string relativeName, Stream destination)
    {
        var source = files[relativeName].Stream;
        source.Position = 0;
        source.CopyTo(destination);
    }

    internal string AppHash(string relativeName) => files[relativeName].Sha256;

    public void Dispose()
    {
        foreach (var file in files.Values) file.Stream.Dispose();
        files.Clear();
    }
}
