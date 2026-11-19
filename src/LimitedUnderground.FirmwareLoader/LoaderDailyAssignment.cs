using System.Globalization;
using System.Text.Json;

namespace LimitedUnderground.FirmwareLoader;

internal sealed class LoaderDailyAssignment
{
    private readonly Dictionary<string, (string Install, string Restore)> boards;
    internal string OperatorSid { get; }
    private LoaderDailyAssignment(string operatorSid, Dictionary<string, (string, string)> boards)
    {
        OperatorSid = operatorSid;
        this.boards = boards;
    }

    internal bool AllowsOperator(string? sid) => string.Equals(OperatorSid, sid, StringComparison.Ordinal);
    internal bool ContainsBoard(string fingerprint) => boards.ContainsKey(fingerprint);
    internal bool AllowsRelease(string fingerprint, byte[] manifest)
    {
        if (!boards.TryGetValue(fingerprint, out var release)) return false;
        string digest = HostReleaseAdmission.Digest(manifest);
        return digest == release.Install || digest == release.Restore;
    }

    internal static bool TryParse(byte[]? bytes, out LoaderDailyAssignment? assignment)
    {
        assignment = null;
        if (bytes is not { Length: > 0 and <= 16384 }) return false;
        try
        {
            using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 5 });
            var root = json.RootElement;
            if (!HasKeys(root, "schema", "operatorSid", "boards") || root.GetProperty("schema").GetRawText() != "1" ||
                ReadString(root, "operatorSid") is not { } sid || !IsOperatorSid(sid)) return false;
            var records = root.GetProperty("boards");
            if (records.ValueKind != JsonValueKind.Array || records.GetArrayLength() is < 1 or > 32) return false;
            var boards = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
            var releases = new HashSet<string>(StringComparer.Ordinal);
            foreach (var board in records.EnumerateArray())
            {
                if (!HasKeys(board, "fingerprint", "installManifestSha256", "restoreManifestSha256") ||
                    ReadString(board, "fingerprint") is not { } fingerprint || !IsDigest(fingerprint) ||
                    ReadString(board, "installManifestSha256") is not { } install || !IsDigest(install) ||
                    ReadString(board, "restoreManifestSha256") is not { } restore || !IsDigest(restore) ||
                    !boards.TryAdd(fingerprint, (install, restore)) || !releases.Add(install) || !releases.Add(restore))
                    return false;
            }
            assignment = new(sid, boards);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException) { return false; }
    }

    private static bool HasKeys(JsonElement obj, params string[] keys)
    {
        if (obj.ValueKind != JsonValueKind.Object) return false;
        var fields = obj.EnumerateObject().Select(field => field.Name).ToArray();
        return fields.Length == keys.Length && keys.All(key => fields.Contains(key, StringComparer.Ordinal));
    }

    private static string? ReadString(JsonElement obj, string key) =>
        obj.GetProperty(key) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    private static bool IsDigest(string value) => value.Length == 64 &&
        value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F') && value.Any(c => c != '0');

    private static bool IsOperatorSid(string sid)
    {
        var parts = sid.Split('-');
        return parts.Length == 8 && sid.StartsWith("S-1-5-21-", StringComparison.Ordinal) &&
            parts.Skip(4).All(part => uint.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out uint number) &&
                number.ToString(CultureInfo.InvariantCulture) == part) && parts[7] != "0";
    }
}
