using LimitedUnderground.FirmwareLoader;
using System.ComponentModel;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

// Builds real local pipes with the worker's production security (owner set to the test user, because
// only SYSTEM may assign SYSTEM as owner) and opens them with the Loader's exact client request.
internal static class DailyLoaderPipeTests
{
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("daily worker pipe opens for the assigned user and grants only the needed rights", AssignedUserOpens),
        ("daily worker pipe refuses a user it does not name", OtherUserRefused),
        ("daily worker pipe without read-attributes refuses every open (VM run 8)", FormerGrantRefused),
        ("daily worker pipe security keeps SYSTEM as owner in production", ProductionSecurity),
    };

    private const int FileReadAttributes = 0x80;
    private static string Self => WindowsIdentity.GetCurrent().User!.Value;
    private static string NewPath() => @"\\.\pipe\LimitedUnderground.FirmwareLoader.Tests." + Guid.NewGuid().ToString("N");

    private static void AssignedUserOpens()
    {
        string path = NewPath();
        using var server = DailyLoaderPipe.CreateServer(path, DailyLoaderPipe.ServerSecurity(Self, Self));
        using var client = DailyLoaderPipe.TryConnect(path, requireSystemOwner: false);
        Require(client is not null, "the assigned user could not open the worker pipe");

        // Read the security Windows actually applied to the live pipe.
        var rules = server.GetAccessControl().GetAccessRules(true, false, typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>().ToArray();
        var operatorRule = rules.Single(rule => rule.IdentityReference.Value == Self);
        int granted = (int)operatorRule.PipeAccessRights;
        Require(operatorRule.AccessControlType == AccessControlType.Allow && granted == (int)DailyLoaderPipe.OperatorGrant,
            $"live operator grant is 0x{granted:X8}");
        Require((granted & FileReadAttributes) != 0, "live grant lacks read-attributes");
        const int ChangePermissions = 0x40000, TakeOwnership = 0x80000, Delete = 0x10000, WriteAttributes = 0x100, CreateInstance = 0x4;
        Require((granted & (ChangePermissions | TakeOwnership | Delete | WriteAttributes | CreateInstance)) == 0,
            "live grant lets the user change, take, delete or add to the pipe");
        Require(rules.Length == 2 && rules.Any(rule => rule.IdentityReference.Value == "S-1-5-18"),
            "live pipe names more than SYSTEM and the assigned user");
    }

    private static void OtherUserRefused()
    {
        string path = NewPath();
        using var server = DailyLoaderPipe.CreateServer(path, DailyLoaderPipe.ServerSecurity("S-1-5-21-101-102-103-1001", Self));
        int error = OpenError(path);
        Require(error == 5, $"a user the pipe does not name got {error} instead of access denied");
    }

    private static void FormerGrantRefused()
    {
        // The grant used until VM run 8: read/write data, READ_CONTROL, SYNCHRONIZE without read-attributes.
        string path = NewPath();
        using var server = DailyLoaderPipe.CreateServer(path, $"O:{Self}G:{Self}D:P(A;;GA;;;SY)(A;;0x00120003;;;{Self})");
        int error = OpenError(path);
        Require(error == 5, $"the former grant opened with {error}; the regression no longer reproduces");
    }

    private static void ProductionSecurity()
    {
        string security = DailyLoaderPipe.ServerSecurity("S-1-5-21-101-102-103-1001");
        Require(security == "O:SYG:SYD:P(A;;GA;;;SY)(A;;0x00120083;;;S-1-5-21-101-102-103-1001)", security);
    }

    private static int OpenError(string path)
    {
        try
        {
            using var client = DailyLoaderPipe.TryConnect(path, requireSystemOwner: false);
            return client is null ? -1 : 0;
        }
        catch (Win32Exception ex) { return ex.NativeErrorCode; }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
