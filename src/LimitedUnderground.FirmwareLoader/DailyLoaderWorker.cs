using System.ComponentModel;
using System.IO;

namespace LimitedUnderground.FirmwareLoader;

internal static class DailyLoaderWorker
{
    internal const string Argument = "--daily-worker";
    private static readonly QualificationOperationStatus Refused = new(
        "Qualification setup unavailable", "Ask your test coordinator to check the installed Firmware Loader.", "", false, false);

    internal static async Task<int> RunAsync()
    {
        if (!MachineRecoveryStorePolicy.IsLocalSystem()) return 2;
        try
        {
            if (!ProtectedInstallLocation()) return 2;
            using var setup = MachineRecoveryStorePolicy.OpenDailyAssignment(out var assignmentFile, out var clock);
            if (assignmentFile.Length is <= 0 or > 32768) return 2;
            var bytes = new byte[(int)assignmentFile.Length];
            assignmentFile.ReadExactly(bytes);
            if (!LoaderDailyAssignment.TryParse(bytes, out var assignment) || assignment is null) return 2;

            using var pipe = DailyLoaderPipe.CreateServer(assignment.OperatorSid);
            using var connect = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await pipe.WaitForConnectionAsync(connect.Token).ConfigureAwait(true);
            if (await DailyLoaderProtocol.ReadRequestAsync(pipe, connect.Token).ConfigureAwait(true) != DailyLoaderRequest.Connect ||
                DailyLoaderPipe.ReadCallerSid(pipe) is not { } sid || !assignment.AllowsOperator(sid)) return 2;

            var runtime = PackagedFlashRuntime.OpenInstalled();
            string helper = Path.Combine(AppContext.BaseDirectory, "flash-helper", "lu_flash_helper.py");
            if (runtime is null || !ProcessFlashHelperRunner.FileMatches(helper, BenchFlash.HelperSha256)) return 2;
            var runner = new ProcessFlashHelperRunner(runtime.CreateHelperInstall(helper));
            using var controller = new LoaderSessionController(LoaderProductionProviders.Factories);
            if (!controller.SelectProduct("opentrail")) return 2;

            using var operation = new QualificationOperationSession(controller,
                BenchFlash.CreateBoard(runner, MachineRecoveryStorePolicy.DailyRoot), null,
                MachineRecoveryStorePolicy.Open, BenchFlash.HelperSha256, ReleaseTrustAnchor.PublicKeySpki(),
                () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), assignment: assignment, assignedClock: clock, operatorSid: sid);

            using (var sendHello = new CancellationTokenSource(TimeSpan.FromSeconds(8)))
                await DailyLoaderProtocol.WriteStatusAsync(pipe, operation.Status, sendHello.Token).ConfigureAwait(true);

            while (true)
            {
                using var receive = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                var request = await DailyLoaderProtocol.ReadRequestAsync(pipe, receive.Token).ConfigureAwait(true);
                if (!assignment.AllowsOperator(DailyLoaderPipe.ReadCallerSid(pipe) ?? "") ||
                    MachineRecoveryStorePolicy.HasPendingDailyAssignment()) return 2;

                bool terminal = false;
                QualificationOperationStatus response;
                switch (request)
                {
                    case DailyLoaderRequest.Prepare:
                        var files = await DailyLoaderProtocol.ReadReleaseAsync(pipe, receive.Token).ConfigureAwait(true);
                        using (var prepare = new CancellationTokenSource(TimeSpan.FromMinutes(10)))
                            response = await operation.PrepareAsync(files, prepare.Token).ConfigureAwait(true);
                        break;
                    case DailyLoaderRequest.Select:
                        operation.SelectBoard();
                        response = operation.Status;
                        break;
                    case DailyLoaderRequest.Confirm:
                        // Disconnecting the window must not stop independent verification. The engine
                        // owns its own deadline and persists uncertainty if a command may have landed.
                        response = await operation.ConfirmAndRunAsync().ConfigureAwait(true) ?? Refused;
                        terminal = true;
                        break;
                    default:
                        return 2;
                }
                using var send = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                try { await DailyLoaderProtocol.WriteStatusAsync(pipe, response, send.Token).ConfigureAwait(true); }
                finally
                {
                    // A timed-out command may still own the adapter. Keep the process and
                    // protected store lease alive even when its client has disconnected.
                    if (terminal) await operation.WhenQuiescent.ConfigureAwait(true);
                }
                if (terminal) return 0;
            }
        }
        catch (EndOfStreamException) { return 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
            InvalidOperationException or Win32Exception or OperationCanceledException) { return 2; }
    }

    private static bool ProtectedInstallLocation()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!Path.IsPathFullyQualified(programFiles)) return false;
        string relative = Path.GetRelativePath(programFiles, AppContext.BaseDirectory);
        if (relative is "." or ".." || Path.IsPathRooted(relative) ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return false;

        // Setup checks the installed tree's write permissions before registering its exact
        // executable. Refuse redirecting any protected program directory at runtime.
        for (var item = new DirectoryInfo(AppContext.BaseDirectory); item is not null; item = item.Parent)
            if ((item.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != FileAttributes.Directory)
                return false;
        return true;
    }
}
