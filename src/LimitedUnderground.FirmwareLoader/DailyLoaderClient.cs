using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace LimitedUnderground.FirmwareLoader;

internal sealed class DailyLoaderClient(NamedPipeClientStream pipe) : IAsyncDisposable
{
    internal const string TaskName = "LimitedUnderground.FirmwareLoader.DailyWorker";

    internal static bool HasRegisteredTask() => AccessScheduledTask(start: false) == 0;

    // The worker creates its endpoint soon after the task starts and waits 30 seconds for this client.
    // Its first reply comes only after it has verified every packaged runtime file, which on a freshly
    // started PC (cold disk cache, first antivirus scan) can take far longer than starting does.
    internal static readonly TimeSpan EndpointLimit = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan FirstReplyLimit = TimeSpan.FromMinutes(3);

    // A failed connection carries a short reference for the test coordinator. It names the step only;
    // the worker's own stage is in the scheduled task's last result.
    //   T-<hresult>  the task could not be opened or started for this Windows user
    //   E1           the worker endpoint did not appear in time
    //   E2-<code>    opening the endpoint failed with this Windows error
    //   E3           the endpoint is not owned by the Windows system account
    //   R1           the worker did not reply in time
    //   R2           the worker closed the connection before replying
    //   R3           the worker's reply was unreadable
    internal sealed record ConnectResult(DailyLoaderClient? Client, string Reference);

    internal static Task<ConnectResult> ConnectAsync(CancellationToken cancellation) =>
        ConnectAsync(() => AccessScheduledTask(start: true), DailyLoaderPipe.TryConnect, EndpointLimit, FirstReplyLimit, cancellation);

    internal static async Task<ConnectResult> ConnectAsync(Func<int> startWorker, Func<NamedPipeClientStream?> tryConnect,
        TimeSpan endpointLimit, TimeSpan firstReplyLimit, CancellationToken cancellation)
    {
        int started = await Task.Run(startWorker, cancellation).ConfigureAwait(true);
        if (started != 0) return new(null, $"T-{started:X8}");
        NamedPipeClientStream? transport = null;
        using (var endpoint = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
        {
            endpoint.CancelAfter(endpointLimit);
            try
            {
                while ((transport = tryConnect()) is null)
                    await Task.Delay(125, endpoint.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { return new(null, "E1"); }
            catch (Win32Exception ex) { return new(null, $"E2-{ex.NativeErrorCode}"); }
            catch (UnauthorizedAccessException) { return new(null, "E3"); }
            catch (IOException ex) { return new(null, $"E2-{ex.HResult & 0xFFFF}"); }
        }
        try
        {
            using var reply = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            reply.CancelAfter(firstReplyLimit);
            await DailyLoaderProtocol.WriteRequestAsync(transport, DailyLoaderRequest.Connect, reply.Token).ConfigureAwait(true);
            await DailyLoaderProtocol.ReadStatusAsync(transport, reply.Token).ConfigureAwait(true);
            return new(new DailyLoaderClient(transport), "");
        }
        catch (Exception ex) when (ex is OperationCanceledException && !cancellation.IsCancellationRequested ||
            ex is IOException or JsonException or Win32Exception or UnauthorizedAccessException)
        {
            await transport.DisposeAsync().ConfigureAwait(true);
            return new(null, ex switch
            {
                OperationCanceledException => "R1",
                EndOfStreamException => "R2",
                InvalidDataException or JsonException => "R3",
                _ => "R2",
            });
        }
        catch
        {
            await transport.DisposeAsync().ConfigureAwait(true);
            throw;
        }
    }

    // The installed task accepts no user arguments. Activation and its authorization stay in Windows.
    // Returns 0 on success, otherwise the Windows result code (or -1 when Task Scheduler is unavailable).
    private static int AccessScheduledTask(bool start)
    {
        object? service = null, folder = null, task = null, run = null;
        try
        {
            Type? taskService = Type.GetTypeFromProgID("Schedule.Service");
            if (taskService is null || (service = Activator.CreateInstance(taskService)) is null) return -1;
            dynamic connection = service;
            connection.Connect();
            folder = connection.GetFolder("\\");
            task = ((dynamic)folder).GetTask(TaskName);
            if (start) run = ((dynamic)task).Run(null);
            return 0;
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or
            Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or InvalidOperationException) { return ex.HResult == 0 ? -1 : ex.HResult; }
        finally
        {
            foreach (object? value in new[] { run, task, folder, service })
                if (value is not null && Marshal.IsComObject(value)) _ = Marshal.ReleaseComObject(value);
        }
    }

    internal async Task<QualificationOperationStatus> PrepareAsync(QualificationReleaseFiles supplied, CancellationToken cancellation)
    {
        var files = supplied.Snapshot() ?? throw new InvalidDataException("worker-release");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromMinutes(11));
        await DailyLoaderProtocol.WriteRequestAsync(pipe, DailyLoaderRequest.Prepare, deadline.Token).ConfigureAwait(true);
        await DailyLoaderProtocol.WriteReleaseAsync(pipe, files, deadline.Token).ConfigureAwait(true);
        return await DailyLoaderProtocol.ReadStatusAsync(pipe, deadline.Token).ConfigureAwait(true);
    }

    internal Task<QualificationOperationStatus> SelectBoardAsync(CancellationToken cancellation) =>
        ExchangeAsync(DailyLoaderRequest.Select, TimeSpan.FromSeconds(20), cancellation);

    internal Task<QualificationOperationStatus> ConfirmAndRunAsync(CancellationToken cancellation) =>
        ExchangeAsync(DailyLoaderRequest.Confirm, TimeSpan.FromMinutes(25), cancellation);

    private async Task<QualificationOperationStatus> ExchangeAsync(
        DailyLoaderRequest request, TimeSpan limit, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(limit);
        await DailyLoaderProtocol.WriteRequestAsync(pipe, request, deadline.Token).ConfigureAwait(true);
        return await DailyLoaderProtocol.ReadStatusAsync(pipe, deadline.Token).ConfigureAwait(true);
    }

    public ValueTask DisposeAsync() => pipe.DisposeAsync();
}
