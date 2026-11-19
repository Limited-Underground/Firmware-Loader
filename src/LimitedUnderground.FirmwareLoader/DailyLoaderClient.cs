using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace LimitedUnderground.FirmwareLoader;

internal sealed class DailyLoaderClient(NamedPipeClientStream pipe) : IAsyncDisposable
{
    internal const string TaskName = "LimitedUnderground.FirmwareLoader.DailyWorker";

    internal static bool HasRegisteredTask() => AccessScheduledTask(start: false);

    internal static async Task<DailyLoaderClient?> ConnectAsync(CancellationToken cancellation)
    {
        if (!await Task.Run(() => AccessScheduledTask(start: true), cancellation).ConfigureAwait(true)) return null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            while (!deadline.IsCancellationRequested)
            {
                var transport = DailyLoaderPipe.TryConnect();
                if (transport is null)
                {
                    await Task.Delay(125, deadline.Token).ConfigureAwait(true);
                    continue;
                }
                try
                {
                    await DailyLoaderProtocol.WriteRequestAsync(transport, DailyLoaderRequest.Connect, deadline.Token).ConfigureAwait(true);
                    await DailyLoaderProtocol.ReadStatusAsync(transport, deadline.Token).ConfigureAwait(true);
                    return new DailyLoaderClient(transport);
                }
                catch { await transport.DisposeAsync().ConfigureAwait(true); throw; }
            }
            return null;
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { return null; }
        catch (Exception ex) when (ex is IOException or Win32Exception or UnauthorizedAccessException or JsonException) { return null; }
    }

    // The installed task accepts no user arguments. Activation and its authorization stay in Windows.
    private static bool AccessScheduledTask(bool start)
    {
        object? service = null, folder = null, task = null, run = null;
        try
        {
            Type? taskService = Type.GetTypeFromProgID("Schedule.Service");
            if (taskService is null || (service = Activator.CreateInstance(taskService)) is null) return false;
            dynamic connection = service;
            connection.Connect();
            folder = connection.GetFolder("\\");
            task = ((dynamic)folder).GetTask(TaskName);
            if (start) run = ((dynamic)task).Run(null);
            return true;
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or
            Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or InvalidOperationException) { return false; }
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
