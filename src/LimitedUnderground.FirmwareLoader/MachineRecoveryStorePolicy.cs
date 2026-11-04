using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace LimitedUnderground.FirmwareLoader;

// No elevation, broker, UI wiring or standard-user journal disclosure is performed here.
// Administrators/SYSTEM are trusted administrators, not adversaries isolated by this ACL.
internal static class MachineRecoveryStorePolicy
{
    private static object? enrollmentAuthority;
    internal static bool ConsumeEnrollmentAuthority(object proof) =>
        proof is not null && ReferenceEquals(Interlocked.CompareExchange(ref enrollmentAuthority, null, proof), proof);
    internal static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    internal static readonly SecurityIdentifier System = new(WellKnownSidType.LocalSystemSid, null);
    internal static string RootPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "LimitedUnderground.FirmwareLoader", "recovery", "v1", "opentrail");

    internal static bool IsElevatedAdministrator()
    {
        if (!OperatingSystem.IsWindows()) return false;
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator) &&
            GetTokenInformation(identity.AccessToken, 20, out int elevated, sizeof(int), out _) && elevated != 0;
    }

    private static void RequirePrivilege()
    {
        if (!IsElevatedAdministrator()) throw new InvalidOperationException("machine-store-unavailable");
    }

    internal static HostOperationStore Open()
    {
        using var guard = Acquire();
        return HostOperationStore.OpenMachine();
    }

    internal static HostOperationStore Enroll()
    {
        RequirePrivilege();
        string common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (Directory.Exists(Path.Combine(common, "LimitedUnderground.FirmwareLoader")) ||
            File.Exists(Path.Combine(common, "LimitedUnderground.FirmwareLoader")))
            throw new InvalidOperationException("machine-store-unavailable");
        using var guard = PinChain(create: true);
        // Any evidence of an earlier/partial enrollment is refused, never overwritten.
        if (Directory.EnumerateFileSystemEntries(RootPath).Any())
            throw new InvalidOperationException("machine-store-unavailable");
        var permit = new object();
        if (Interlocked.CompareExchange(ref enrollmentAuthority, permit, null) is not null)
            throw new InvalidOperationException("machine-store-unavailable");
        try { return HostOperationStore.EnrollMachine(permit); }
        finally { Interlocked.CompareExchange(ref enrollmentAuthority, null, permit); }
    }

    internal static IDisposable Acquire()
    {
        RequirePrivilege();
        return PinChain(create: false);
    }

    internal static DirectorySecurity DirectoryPolicy()
    {
        var security = new DirectorySecurity();
        security.SetOwner(Administrators);
        security.SetAccessRuleProtection(true, false);
        foreach (var sid in new[] { Administrators, System })
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        return security;
    }

    internal static FileSecurity FilePolicy()
    {
        var security = new FileSecurity();
        security.SetOwner(Administrators);
        security.SetAccessRuleProtection(true, false);
        foreach (var sid in new[] { Administrators, System })
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
        return security;
    }

    internal static bool HasExactPolicy(FileSystemSecurity security, bool directory)
    {
        if (!security.AreAccessRulesProtected || !Administrators.Equals(security.GetOwner(typeof(SecurityIdentifier)))) return false;
        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
        return rules.Length == 2 && new[] { Administrators, System }.All(sid => rules.Count(rule =>
            sid.Equals(rule.IdentityReference) && !rule.IsInherited && rule.AccessControlType == AccessControlType.Allow &&
            rule.FileSystemRights == FileSystemRights.FullControl && rule.PropagationFlags == PropagationFlags.None &&
            rule.InheritanceFlags == (directory ? InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit : InheritanceFlags.None)) == 1);
    }

    internal static void CheckFile(SafeFileHandle handle)
    {
        using var guard = Acquire();
        if (!HasExactHandlePolicy(handle, false)) throw new InvalidOperationException("machine-store-unavailable");
    }

    private static bool HasExactHandlePolicy(SafeFileHandle handle, bool directory)
    {
        if (GetSecurityInfo(handle, 1, 5, out _, out _, out _, out _, out var descriptor) != 0) throw new IOException();
        try
        {
            uint length = GetSecurityDescriptorLength(descriptor);
            if (length == 0 || length > 65536) throw new IOException();
            byte[] bytes = new byte[length]; Marshal.Copy(descriptor, bytes, 0, bytes.Length);
            FileSystemSecurity security = directory ? new DirectorySecurity() : new FileSecurity();
            security.SetSecurityDescriptorBinaryForm(bytes, AccessControlSections.Owner | AccessControlSections.Access);
            return HasExactPolicy(security, directory);
        }
        finally { LocalFree(descriptor); }
    }

    internal static FileStream CreateFile(string path, FileShare share)
    {
        using var guard = Acquire();
        CheckChildPath(path);
        var file = new FileInfo(path).Create(FileMode.CreateNew, FileSystemRights.FullControl,
            share, 4096, FileOptions.WriteThrough, FilePolicy());
        try { if (!HasExactHandlePolicy(file.SafeFileHandle, false)) throw new IOException(); return file; }
        catch { file.Dispose(); throw; }
    }

    private static void CheckChildPath(string path)
    {
        string leaf = Path.GetFileName(path);
        if (path != Path.GetFullPath(path) || Path.GetDirectoryName(path) != RootPath ||
            !(leaf is HostOperationStore.LockName or HostOperationStore.JournalName ||
              leaf.StartsWith("pending-", StringComparison.Ordinal) && leaf.EndsWith(".json", StringComparison.Ordinal) &&
              Guid.TryParseExact(leaf[8..^5], "N", out _))) throw new InvalidOperationException("machine-store-unavailable");
    }

    // Shares the real no-delete/reparse guard, but cannot name a production root or mint authority.
    internal static IDisposable PinIsolatedTestDirectory(string path)
    {
        string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string leaf = Path.GetFileName(path);
        if (path != Path.GetFullPath(path) || Path.GetDirectoryName(path) != parent ||
            !leaf.StartsWith("LUF.HostStore.Tests.", StringComparison.Ordinal) ||
            !Guid.TryParseExact(leaf["LUF.HostStore.Tests.".Length..], "N", out _))
            throw new InvalidOperationException("machine-store-unavailable");
        var handles = new List<SafeFileHandle>();
        try
        {
            string current = Path.GetPathRoot(path) ?? throw new IOException();
            Pin(current, handles);
            foreach (string part in Path.GetRelativePath(current, path).Split(Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, part);
                Pin(current, handles);
            }
            return new PinnedChain(handles);
        }
        catch
        {
            foreach (var handle in handles) handle.Dispose();
            throw new InvalidOperationException("machine-store-unavailable");
        }
    }

    private static IDisposable PinChain(bool create)
    {
        var handles = new List<SafeFileHandle>();
        try
        {
            string common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!Path.IsPathFullyQualified(common) || common.StartsWith("\\\\", StringComparison.Ordinal)) throw new IOException();
            string root = Path.GetPathRoot(common) ?? throw new IOException();
            string current = root;
            Pin(current, handles);
            foreach (string part in Path.GetRelativePath(root, RootPath).Split(Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, part);
                bool owned = current.Length > common.Length;
                if (create && owned)
                {
                    // Native CreateDirectory receives the protected security descriptor at creation.
                    // Do not adopt a racing creator's directory.
                    byte[] descriptor = DirectoryPolicy().GetSecurityDescriptorBinaryForm();
                    var pin = GCHandle.Alloc(descriptor, GCHandleType.Pinned);
                    try
                    {
                        var attributes = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), Descriptor = pin.AddrOfPinnedObject() };
                        if (!CreateDirectory(current, ref attributes)) throw new IOException();
                    }
                    finally { pin.Free(); }
                }
                else if (!Directory.Exists(current)) throw new IOException();
                var handle = Pin(current, handles);
                if (owned && !HasExactHandlePolicy(handle, true))
                    throw new IOException();
            }
            return new PinnedChain(handles);
        }
        catch
        {
            foreach (var handle in handles) handle.Dispose();
            throw new InvalidOperationException("machine-store-unavailable");
        }
    }

    private static SafeFileHandle Pin(string path, List<SafeFileHandle> handles)
    {
        // FILE_LIST_DIRECTORY participates in share-access checks; metadata-only opens do not.
        // No FILE_SHARE_DELETE: names cannot be renamed/replaced while the chain is held.
        var handle = CreateFileNative(path, 0x20081, 3, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        handles.Add(handle);
        var buffer = new StringBuilder(32768);
        if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var info) ||
            (info.Attributes & 0x410) != 0x10 ||
            GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0) is 0 or >= 32768 ||
            !string.Equals(buffer.ToString(), "\\\\?\\" + path, StringComparison.OrdinalIgnoreCase)) throw new IOException();
        return handle;
    }

    private sealed class PinnedChain(List<SafeFileHandle> handles) : IDisposable
    {
        public void Dispose() { for (int i = handles.Count - 1; i >= 0; i--) handles[i].Dispose(); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { public int Length; public IntPtr Descriptor; public int Inherit; }
    [StructLayout(LayoutKind.Sequential)] private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written;
        public uint Volume, SizeHigh, SizeLow, NumberOfLinks, IndexHigh, IndexLow;
    }
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern uint GetSecurityInfo(SafeFileHandle handle, int kind, uint information,
        out IntPtr owner, out IntPtr group, out IntPtr dacl, out IntPtr sacl, out IntPtr descriptor);
    [DllImport("advapi32.dll")] private static extern uint GetSecurityDescriptorLength(IntPtr descriptor);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr pointer);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int kind, out int value, int length, out int returned);
    [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectory(string path, ref SecurityAttributes attributes);
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileNative(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle file, StringBuilder path, uint length, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);
}
