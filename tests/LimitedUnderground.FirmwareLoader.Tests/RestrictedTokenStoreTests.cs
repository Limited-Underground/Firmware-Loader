using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using LimitedUnderground.FirmwareLoader;
using Microsoft.Win32.SafeHandles;

internal static class RestrictedTokenStoreTests
{
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("restricted same-account token poisons retained store without clearing uncertainty", RetainedStoreRefusal),
    };

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void RetainedStoreRefusal()
    {
        using var originalIdentity = WindowsIdentity.GetCurrent();
        var originalUser = originalIdentity.User ?? throw new InvalidOperationException("fixture user unavailable");
        Require(!IsTokenRestricted(originalIdentity.AccessToken), "fixture requires an ordinary unrestricted starting token");
        using var restrictedToken = ReduceToken(originalIdentity.AccessToken);
        Require(IsTokenRestricted(restrictedToken), "native token restriction must be present");
        string temporaryParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string root = Path.Combine(temporaryParent, "LUF.HostStore.Tests." + Guid.NewGuid().ToString("N"));
        string journal = Path.Combine(root, HostOperationStore.JournalName);
        string recovery = new('A', 64);
        bool denied = false;
        int nativeError = 0;
        try
        {
            var store = HostOperationStore.CreateIsolatedTestStore(root);
            using var lease = store.TryAcquire();
            Require(lease is not null && store.MarkUncertain(recovery), "ordinary caller holds durable unresolved intent");
            byte[] intent = File.ReadAllBytes(journal);
            WindowsIdentity.RunImpersonated(restrictedToken, () =>
            {
                using var currentIdentity = WindowsIdentity.GetCurrent();
                Require(currentIdentity.User is { } currentUser && originalUser.Equals(currentUser) && IsTokenRestricted(currentIdentity.AccessToken),
                    "the effective token must be restricted for the same account");
                try
                {
                    using var probe = new FileStream(journal, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                catch (UnauthorizedAccessException error)
                {
                    denied = true;
                    nativeError = error.HResult & 0xffff;
                }
                Require(denied && nativeError == 5, "real restricted-token journal access must be denied by Windows");
                Require(store.HasUnresolvedOperation && store.RecoveryDigest is null &&
                    !store.MarkComplete() && !store.ReleaseUnsubmitted() && !store.MarkUncertain(recovery),
                    "retained store must poison and refuse authority under reduced rights");
            });
            using var restoredIdentity = WindowsIdentity.GetCurrent();
            Require(restoredIdentity.User is { } restoredUser && originalUser.Equals(restoredUser) && !IsTokenRestricted(restoredIdentity.AccessToken),
                "ordinary effective context must be restored");
            Require(File.ReadAllBytes(journal).AsSpan().SequenceEqual(intent) && Directory.GetFiles(root).Length == 2,
                "token transition preserves exact unresolved bytes without extra files");
            Require(store.HasUnresolvedOperation && store.RecoveryDigest is null &&
                !store.MarkComplete() && !store.ReleaseUnsubmitted() && !store.MarkUncertain(recovery),
                "restoring access cannot unpoison retained authority");
            var competing = new HostOperationStore(root);
            using var blocked = competing.TryAcquire();
            Require(blocked is null, "retained lease still excludes a fresh ordinary owner");
            lease!.Dispose();
            var reopened = new HostOperationStore(root);
            using var freshLease = reopened.TryAcquire();
            Require(freshLease is not null && reopened.HasUnresolvedOperation && reopened.RecoveryDigest == recovery &&
                !reopened.MarkComplete() && !reopened.ReleaseUnsubmitted(),
                "fresh owner after lease release retains uncertainty without inherited completion authority");
            Require(File.ReadAllBytes(journal).AsSpan().SequenceEqual(intent), "fresh ownership cannot rewrite the intent");
            Console.WriteLine("Restricted-token fixture: same-account=True; restricted=True; access-denied=" + nativeError +
                "; ordinary-context-restored=True; unresolved-bytes-preserved=True; poisoned-caller-refused=True");
        }
        finally
        {
            string leaf = Path.GetFileName(root);
            if (Path.GetDirectoryName(root) != temporaryParent || !leaf.StartsWith("LUF.HostStore.Tests.", StringComparison.Ordinal) ||
                !Guid.TryParseExact(leaf["LUF.HostStore.Tests.".Length..], "N", out _))
                throw new InvalidOperationException("fixture cleanup boundary changed");
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static SafeAccessTokenHandle ReduceToken(SafeAccessTokenHandle originalToken)
    {
        var sid = new SecurityIdentifier(WellKnownSidType.RestrictedCodeSid, null);
        byte[] bytes = new byte[sid.BinaryLength];
        sid.GetBinaryForm(bytes, 0);
        IntPtr nativeSid = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, nativeSid, bytes.Length);
            var restrictions = new[] { new SidAndAttributes { Sid = nativeSid, Attributes = 0 } };
            const uint disableMaximumPrivileges = 1;
            if (!CreateRestrictedToken(originalToken, disableMaximumPrivileges, 0, IntPtr.Zero, 0, IntPtr.Zero,
                1, restrictions, out var restrictedToken))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "restricted fixture token creation failed");
            return restrictedToken;
        }
        finally
        {
            Marshal.FreeHGlobal(nativeSid);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SidAndAttributes
    {
        internal IntPtr Sid;
        internal uint Attributes;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateRestrictedToken(SafeAccessTokenHandle existingToken, uint flags,
        uint disabledSidCount, IntPtr disabledSids, uint deletedPrivilegeCount, IntPtr deletedPrivileges,
        uint restrictedSidCount, [In] SidAndAttributes[] restrictedSids, out SafeAccessTokenHandle newToken);

    [DllImport("advapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsTokenRestricted(SafeAccessTokenHandle token);
}