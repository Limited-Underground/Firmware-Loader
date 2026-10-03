using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace LimitedUnderground.FirmwareLoader;

internal enum HostStoreStep { Write, Flush, Replace, Readback }

// Internal host checkpoint only: no production root, device or UI registration.
// Both views share one stable lock file and refresh the notice after acquiring it.
internal sealed class HostOperationStore : IHostOperationLease, IHostUncertaintyJournal
{
    internal const string LockName = "operation.lock";
    internal const string JournalName = "uncertainty.json";
    private const int MaximumRecordBytes = 1024;
    private readonly object gate = new();
    private readonly string root;
    private readonly Action<HostStoreStep>? checkpoint;
    private Lease? active;
    private Record? snapshot;
    private string? storeDigest, ownedOperation, ownedRecovery;
    private bool poisoned, committing;
    private sealed record Record(string StoreDigest, string State, string? OperationDigest, string? RecoveryDigest);

    internal HostOperationStore(string directory, Action<HostStoreStep>? checkpoint = null)
    {
        try { root = CanonicalDirectory(directory); }
        catch (Exception) { throw new InvalidOperationException("host-store-unavailable"); }
        this.checkpoint = checkpoint;
        lock (gate) Refresh();
    }

    internal static HostOperationStore CreateIsolatedTestStore(string directory)
    {
        // No ordinary open/operation can initialize or repair a missing store.
        // This explicit factory is limited to a fresh disposable test root.
        try
        {
            string path = Path.GetFullPath(directory);
            string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            string leaf = Path.GetFileName(path);
            if (path != directory || Path.GetDirectoryName(path) != parent ||
                !leaf.StartsWith("LUF.HostStore.Tests.", StringComparison.Ordinal) ||
                !Guid.TryParseExact(leaf["LUF.HostStore.Tests.".Length..], "N", out _) ||
                Directory.Exists(path) || File.Exists(path)) throw new IOException();
            CheckDirectory(parent);
            Directory.CreateDirectory(path);
            path = CanonicalDirectory(path);
            using var lease = new FileStream(Path.Combine(path, LockName), FileMode.CreateNew,
                FileAccess.ReadWrite, FileShare.None);
            CheckHandle(lease.SafeFileHandle, Path.Combine(path, LockName));
            var initial = new Record(Digest(RandomNumberGenerator.GetBytes(32)), "idle", null, null);
            using (var file = new FileStream(Path.Combine(path, JournalName), FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                CheckHandle(file.SafeFileHandle, Path.Combine(path, JournalName));
                file.Write(Encode(initial)); file.Flush(true);
            }
            var store = new HostOperationStore(path);
            if (store.poisoned) throw new IOException();
            return store;
        }
        catch (Exception) { throw new InvalidOperationException("host-store-unavailable"); }
    }

    public bool HasUnresolvedOperation
    {
        get { lock (gate) { Refresh(); return snapshot is null || snapshot.State == "unresolved"; } }
    }
    public string? RecoveryDigest
    {
        get { lock (gate) { Refresh(); return snapshot?.State == "unresolved" ? snapshot.RecoveryDigest : null; } }
    }

    public IDisposable? TryAcquire()
    {
        lock (gate)
        {
            if (active is not null || poisoned || committing) return null;
            FileStream? file = null;
            try
            {
                CheckDirectory(root);
                string path = Path.Combine(root, LockName);
                CheckRegular(path);
                file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                CheckHandle(file.SafeFileHandle, path);
                Refresh(); // Never trust a snapshot sampled before exclusive ownership.
                if (snapshot is null) throw new IOException();
                active = new Lease(this, file);
                file = null;
                ownedOperation = ownedRecovery = null;
                return active;
            }
            catch (Exception) { return null; }
            finally { file?.Dispose(); }
        }
    }

    public bool MarkUncertain(string recoveryDigest)
    {
        lock (gate)
        {
            if (active is null || poisoned || committing || ownedOperation is not null || !Hash(recoveryDigest)) return false;
            Refresh();
            if (snapshot is null || (snapshot.State == "unresolved" && snapshot.RecoveryDigest != recoveryDigest)) return false;
            var next = new Record(snapshot.StoreDigest, "unresolved",
                Digest(RandomNumberGenerator.GetBytes(32)), recoveryDigest);
            if (!Commit(next)) return false;
            ownedOperation = next.OperationDigest;
            ownedRecovery = recoveryDigest;
            return true;
        }
    }

    public bool MarkComplete()
    {
        lock (gate)
        {
            if (active is null || poisoned || committing || ownedOperation is null) return false;
            Refresh();
            if (snapshot is null || snapshot.State != "unresolved" || snapshot.OperationDigest != ownedOperation ||
                snapshot.RecoveryDigest != ownedRecovery) return false;
            var next = snapshot with { State = "complete" };
            if (!Commit(next)) return false;
            ownedOperation = ownedRecovery = null;
            return true;
        }
    }

    private bool Commit(Record next)
    {
        string? temporary = null;
        committing = true;
        try
        {
            CheckDirectory(root);
            var prior = snapshot ?? throw new IOException();
            byte[] expectedPrior = Encode(prior), bytes = Encode(next);
            if (!ReadRecord().AsSpan().SequenceEqual(expectedPrior)) throw new IOException();
            temporary = Path.Combine(root, "pending-" + Guid.NewGuid().ToString("N") + ".json");
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough))
            {
                CheckHandle(file.SafeFileHandle, temporary);
                checkpoint?.Invoke(HostStoreStep.Write);
                file.Write(bytes);
                checkpoint?.Invoke(HostStoreStep.Flush);
                file.Flush(true);
            }
            checkpoint?.Invoke(HostStoreStep.Replace);
            CheckDirectory(root);
            if (!ReadRecord().AsSpan().SequenceEqual(expectedPrior)) throw new IOException();
            File.Replace(temporary, Path.Combine(root, JournalName), null);
            temporary = null;
            checkpoint?.Invoke(HostStoreStep.Readback);
            if (!ReadRecord().AsSpan().SequenceEqual(bytes)) throw new IOException();
            snapshot = next;
            return true;
        }
        catch (Exception)
        {
            // Replacement may have landed; never guess rollback or let this
            // uncertain instance admit a later mutation/completion.
            poisoned = true; snapshot = null; ownedOperation = ownedRecovery = null;
            return false;
        }
        finally
        {
            if (temporary is not null)
            {
                try { File.Delete(temporary); }
                catch (Exception) { poisoned = true; snapshot = null; ownedOperation = ownedRecovery = null; }
            }
            committing = false;
        }
    }

    private void Refresh()
    {
        if (poisoned) { snapshot = null; return; }
        try
        {
            byte[] bytes = ReadRecord();
            using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 4 });
            var value = json.RootElement;
            if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != 5 ||
                value.GetProperty("schema").GetInt32() != 1) throw new IOException();
            var record = new Record(value.GetProperty("storeDigest").GetString()!,
                value.GetProperty("state").GetString()!, value.GetProperty("operationDigest").GetString(),
                value.GetProperty("recoveryDigest").GetString());
            if (!Hash(record.StoreDigest) || record.State is not ("idle" or "unresolved" or "complete") ||
                (record.State == "idle" ? record.OperationDigest is not null || record.RecoveryDigest is not null
                    : !Hash(record.OperationDigest) || !Hash(record.RecoveryDigest)) ||
                (storeDigest is not null && record.StoreDigest != storeDigest) ||
                !Encode(record).AsSpan().SequenceEqual(bytes)) throw new IOException();
            storeDigest ??= record.StoreDigest;
            snapshot = record;
        }
        catch (Exception) { poisoned = true; snapshot = null; ownedOperation = ownedRecovery = null; }
    }

    private byte[] ReadRecord()
    {
        CheckDirectory(root);
        string path = Path.Combine(root, JournalName);
        CheckRegular(path);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        CheckHandle(file.SafeFileHandle, path);
        if (file.Length is <= 0 or > MaximumRecordBytes) throw new IOException();
        byte[] bytes = new byte[checked((int)file.Length)];
        file.ReadExactly(bytes);
        if (file.ReadByte() != -1) throw new IOException();
        return bytes;
    }

    private static byte[] Encode(Record value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject(); writer.WriteNumber("schema", 1);
            writer.WriteString("storeDigest", value.StoreDigest); writer.WriteString("state", value.State);
            writer.WriteString("operationDigest", value.OperationDigest);
            writer.WriteString("recoveryDigest", value.RecoveryDigest); writer.WriteEndObject();
        }
        return stream.ToArray();
    }
    private static bool Hash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    private static string Digest(byte[] value) => Convert.ToHexString(SHA256.HashData(value));

    private static string CanonicalDirectory(string value)
    {
        string path = Path.GetFullPath(value);
        if (path != value || path.Length < 4 || path[1] != ':' || path[2] != '\\' ||
            path[3..].Contains(':') || path.EndsWith('\\') || path.EndsWith('/')) throw new IOException();
        CheckDirectory(path);
        return path;
    }
    private static void CheckDirectory(string path)
    {
        for (DirectoryInfo? entry = new(path); entry is not null; entry = entry.Parent)
            if (!entry.Exists || (entry.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException();
    }
    private static void CheckRegular(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0) throw new IOException();
    }
    private static void CheckHandle(SafeFileHandle handle, string path)
    {
        var buffer = new StringBuilder(32768);
        uint length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
        string final = buffer.ToString();
        if (length == 0 || length >= buffer.Capacity || !final.StartsWith("\\\\?\\", StringComparison.Ordinal) ||
            !string.Equals(final[4..], path, StringComparison.OrdinalIgnoreCase) ||
            !GetFileInformationByHandle(handle, out var info) || info.NumberOfLinks != 1) throw new IOException();
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle file, StringBuilder path, uint length, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written;
        public uint Volume, SizeHigh, SizeLow, NumberOfLinks, IndexHigh, IndexLow;
    }

    private sealed class Lease(HostOperationStore owner, FileStream file) : IDisposable
    {
        private int disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            lock (owner.gate)
            {
                if (ReferenceEquals(owner.active, this))
                { owner.active = null; owner.ownedOperation = owner.ownedRecovery = null; }
                file.Dispose();
            }
        }
    }
}
