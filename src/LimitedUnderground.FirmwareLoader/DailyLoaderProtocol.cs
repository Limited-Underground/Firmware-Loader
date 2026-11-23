using System.Buffers.Binary;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace LimitedUnderground.FirmwareLoader;

internal enum DailyLoaderRequest : byte { Connect = 1, Prepare, Select, Confirm }

internal static class DailyLoaderProtocol
{
    private const int MaximumImageBytes = 16 * 1024 * 1024;
    private const int MaximumStatusBytes = 8192;

    internal static async Task<DailyLoaderRequest> ReadRequestAsync(Stream stream, CancellationToken cancellation)
    {
        byte[] command = new byte[1];
        await stream.ReadExactlyAsync(command, cancellation).ConfigureAwait(true);
        return (DailyLoaderRequest)command[0];
    }

    internal static ValueTask WriteRequestAsync(Stream stream, DailyLoaderRequest request, CancellationToken cancellation) =>
        stream.WriteAsync(new byte[] { (byte)request }, cancellation);

    internal static async Task<QualificationReleaseFiles> ReadReleaseAsync(Stream stream, CancellationToken cancellation) => new(
        await ReadBlockAsync(stream, 16384, cancellation).ConfigureAwait(true),
        await ReadBlockAsync(stream, 384, cancellation).ConfigureAwait(true),
        await ReadBlockAsync(stream, MaximumImageBytes, cancellation).ConfigureAwait(true),
        await ReadBlockAsync(stream, MaximumImageBytes, cancellation).ConfigureAwait(true));

    internal static async Task WriteReleaseAsync(Stream stream, QualificationReleaseFiles files, CancellationToken cancellation)
    {
        await WriteBlockAsync(stream, files.Manifest, cancellation).ConfigureAwait(true);
        await WriteBlockAsync(stream, files.Signature, cancellation).ConfigureAwait(true);
        await WriteBlockAsync(stream, files.Raw, cancellation).ConfigureAwait(true);
        await WriteBlockAsync(stream, files.Padded, cancellation).ConfigureAwait(true);
    }

    internal static async Task<QualificationOperationStatus> ReadStatusAsync(Stream stream, CancellationToken cancellation)
    {
        byte[] bytes = await ReadBlockAsync(stream, MaximumStatusBytes, cancellation).ConfigureAwait(true);
        return JsonSerializer.Deserialize<QualificationOperationStatus>(bytes) ?? throw new InvalidDataException("worker-response");
    }

    internal static Task WriteStatusAsync(Stream stream, QualificationOperationStatus status, CancellationToken cancellation)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(status);
        if (bytes.Length > MaximumStatusBytes) throw new InvalidDataException("worker-response");
        return WriteBlockAsync(stream, bytes, cancellation);
    }

    private static async Task<byte[]> ReadBlockAsync(Stream stream, int maximum, CancellationToken cancellation)
    {
        byte[] header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellation).ConfigureAwait(true);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > maximum) throw new InvalidDataException("worker-frame");
        byte[] bytes = new byte[length];
        await stream.ReadExactlyAsync(bytes, cancellation).ConfigureAwait(true);
        return bytes;
    }

    private static async Task WriteBlockAsync(Stream stream, byte[] bytes, CancellationToken cancellation)
    {
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, cancellation).ConfigureAwait(true);
        await stream.WriteAsync(bytes, cancellation).ConfigureAwait(true);
    }
}

// Local-only bounded pipe. The Windows owner check lets a normal client authenticate the
// machine service identity without opening an unrelated privileged process or token.
internal static class DailyLoaderPipe
{
    private const string Path = @"\\.\pipe\LimitedUnderground.FirmwareLoader.DailyWorker.v1";
    private const uint Overlapped = 0x40000000;
    private const uint OperatorAccess = 0x00120003; // read/write data, READ_CONTROL, SYNCHRONIZE
    // CreateFileW adds FILE_READ_ATTRIBUTES (0x80) to every open. Without it in the grant the pipe
    // refuses every open by the assigned user with access denied, even READ_CONTROL alone.
    internal const uint OperatorGrant = OperatorAccess | 0x80;

    internal static string ServerSecurity(string operatorSid, string owner = "SY") =>
        $"O:{owner}G:{owner}D:P(A;;GA;;;SY)(A;;0x{OperatorGrant:X8};;;{operatorSid})";

    internal static NamedPipeServerStream CreateServer(string operatorSid) => CreateServer(Path, ServerSecurity(operatorSid));

    // Separate entry points let host tests build a pipe with the production security under a test name.
    internal static NamedPipeServerStream CreateServer(string path, string security)
    {
        var descriptor = new RawSecurityDescriptor(security);
        var bytes = new byte[descriptor.BinaryLength];
        descriptor.GetBinaryForm(bytes, 0);
        var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            var attributes = new SecurityAttributes
            {
                Length = Marshal.SizeOf<SecurityAttributes>(),
                Descriptor = pin.AddrOfPinnedObject()
            };
            const uint duplex = 3, firstInstance = 0x00080000, rejectRemoteClients = 8;
            var handle = CreateNamedPipe(path, duplex | firstInstance | Overlapped, rejectRemoteClients,
                1, 8192, 8192, 0, ref attributes);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastPInvokeError();
                handle.Dispose();
                throw new Win32Exception(error);
            }
            try { return new NamedPipeServerStream(PipeDirection.InOut, true, false, handle); }
            catch { handle.Dispose(); throw; }
        }
        finally { pin.Free(); }
    }

    internal static NamedPipeClientStream? TryConnect() => TryConnect(Path, requireSystemOwner: true);

    internal static NamedPipeClientStream? TryConnect(string path, bool requireSystemOwner)
    {
        const uint securityQosPresent = 0x00100000, securityIdentification = 0x00010000, openExisting = 3;
        var handle = OpenPipe(path, OperatorAccess, 0, IntPtr.Zero, openExisting,
            Overlapped | securityQosPresent | securityIdentification, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            if (error is 2 or 231) return null; // not started yet or currently busy
            throw new Win32Exception(error);
        }

        try
        {
            if (requireSystemOwner && !HasSystemOwner(handle)) throw new UnauthorizedAccessException("worker-endpoint");
            return new NamedPipeClientStream(PipeDirection.InOut, true, true, handle);
        }
        catch { handle.Dispose(); throw; }
    }

    internal static string? ReadCallerSid(NamedPipeServerStream server)
    {
        string? result = null;
        server.RunAsClient(() =>
        {
            using var caller = WindowsIdentity.GetCurrent(ifImpersonating: true);
            result = caller?.User?.Value;
        });
        return result;
    }

    private static bool HasSystemOwner(SafePipeHandle handle)
    {
        if (GetSecurityInfo(handle, 6, 1, out var owner, out _, out _, out _, out var descriptor) != 0) return false;
        try
        {
            return owner != IntPtr.Zero && MachineRecoveryStorePolicy.System.Equals(new SecurityIdentifier(owner));
        }
        finally { _ = LocalFree(descriptor); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr Descriptor;
        public int InheritHandle;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateNamedPipeW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafePipeHandle CreateNamedPipe(string name, uint access, uint mode,
        uint maximumInstances, uint outputSize, uint inputSize, uint timeout, ref SecurityAttributes attributes);

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafePipeHandle OpenPipe(string name, uint access, uint sharing, IntPtr attributes,
        uint disposition, uint flags, IntPtr template);

    [DllImport("advapi32.dll")]
    private static extern uint GetSecurityInfo(SafePipeHandle handle, int type, uint information,
        out IntPtr owner, out IntPtr group, out IntPtr dacl, out IntPtr sacl, out IntPtr descriptor);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr pointer);
}
