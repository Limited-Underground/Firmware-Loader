using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace LimitedUnderground.FirmwareLoader.Setup;

internal static class LockedPaths
{
    private const uint ReadFile = 0x80000000;
    private const uint ReadDirectory = 0x00020081;
    private const uint ShareRead = 1;
    private const uint ShareReadWrite = 3;
    private const uint OpenExisting = 3;
    private const uint OpenReparsePoint = 0x00200000;
    private const uint BackupSemantics = 0x02000000;

    internal static string AbsoluteLocalPath(string value)
    {
        if (value.Length < 3 || value[0] is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z') ||
            value[1] != ':' || value[2] != '\\' || value.AsSpan(3).Contains(':'))
            throw new InstallerRefusalException("Use a fixed, absolute local path for Firmware Loader setup.");
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        if (!full.Equals(Path.TrimEndingDirectorySeparator(value), StringComparison.OrdinalIgnoreCase))
            throw new InstallerRefusalException("Use a fixed, absolute local path for Firmware Loader setup.");
        return full;
    }

    internal static IDisposable PinDirectory(string path)
    {
        var handles = new List<SafeFileHandle>();
        try
        {
            string root = Path.GetPathRoot(path)!;
            handles.Add(OpenExact(root, directory: true));
            string current = root;
            if (!root.Equals(path, StringComparison.OrdinalIgnoreCase))
            {
                foreach (string component in path[root.Length..].Split('\\'))
                {
                    current = Path.Combine(current, component);
                    handles.Add(OpenExact(current, directory: true));
                }
            }
            return new DirectoryChain(handles);
        }
        catch
        {
            new DirectoryChain(handles).Dispose();
            throw;
        }
    }

    internal static FileStream OpenFile(string path)
    {
        using var directory = PinDirectory(Path.GetDirectoryName(path)!);
        var handle = OpenExact(path, directory: false);
        try { return new FileStream(handle, FileAccess.Read); }
        catch { handle.Dispose(); throw; }
    }

    internal static bool Exists(string path)
    {
        try { _ = File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    private static SafeFileHandle OpenExact(string path, bool directory)
    {
        var handle = CreateFile(path, directory ? ReadDirectory : ReadFile, directory ? ShareReadWrite : ShareRead,
            IntPtr.Zero, OpenExisting, OpenReparsePoint | (directory ? BackupSemantics : 0), IntPtr.Zero);
        try
        {
            if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            var name = new StringBuilder(32768);
            if (!GetFileInformationByHandle(handle, out var info) ||
                (info.FileAttributes & FileAttributes.ReparsePoint) != 0 ||
                ((info.FileAttributes & FileAttributes.Directory) != 0) != directory ||
                !directory && info.NumberOfLinks != 1 ||
                GetFinalPathNameByHandle(handle, name, (uint)name.Capacity, 0) is 0 or >= 32768 ||
                !name.ToString().Equals(@"\\?\" + path, StringComparison.OrdinalIgnoreCase))
                throw new InstallerRefusalException("Firmware Loader setup refused a redirected, aliased or unexpected path.");
            return handle;
        }
        catch { handle.Dispose(); throw; }
    }

    private sealed class DirectoryChain(List<SafeFileHandle> handles) : IDisposable
    {
        public void Dispose()
        {
            for (int i = handles.Count - 1; i >= 0; i--) handles[i].Dispose();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        internal FileAttributes FileAttributes;
        internal System.Runtime.InteropServices.ComTypes.FILETIME CreationTime, LastAccessTime, LastWriteTime;
        internal uint VolumeSerialNumber, FileSizeHigh, FileSizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition,
        uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation info);

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint length, uint flags);
}
