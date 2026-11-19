using System.IO;

namespace LimitedUnderground.FirmwareLoader;

internal sealed record QualificationOperationStatus(string Heading, string Detail, string ReleaseSummary,
    bool CanChooseBoard, bool CanConfirm, string? BoardLabel = null);

/// <summary>
/// Trusted worker logic for one authenticated connection. The transport must authenticate and
/// serialize that connection and keep other sessions away from the board for its lifetime.
/// The scheduled worker owns the endpoint and keeps the session alive until it quiesces.
/// </summary>
internal sealed class QualificationOperationSession(LoaderSessionController controller, IBenchBoard board,
    QualificationBoardPin? pin, Func<HostOperationStore> openStore, string helperSha256, byte[] trustedKey,
    Func<long> milliseconds, LoaderDailyAssignment? assignment = null, ProductionAdmissionState? assignedClock = null,
    string? operatorSid = null) : IDisposable
{
    private LoaderUpdateWorkflow? workflow;
    private QualificationOperationStatus status = new("Choose the signed release", "", "", false, false);
    private bool preparing;
    private bool disposed;

    internal Task WhenQuiescent => workflow?.WhenQuiescent ?? Task.CompletedTask;

    internal QualificationOperationStatus Status => workflow is { } current ? new(
        current.Heading, current.Detail, current.Summary,
        current is { CanChoose: true, SelectedChoice: null } && current.Choices.Count == 1,
        current.CanConfirm, current.Choices.Count == 1 ? current.Choices[0].DisplayName : null) : status;

    internal async Task<QualificationOperationStatus> PrepareAsync(QualificationReleaseFiles files, CancellationToken cancellation)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (preparing || workflow is not null) return Status;
        preparing = true;
        try
        {
            var prepared = await QualificationInstaller.PrepareAssignedAsync(controller, files, board, pin, openStore,
                helperSha256, trustedKey, milliseconds, cancellation, assignment, assignedClock, operatorSid).ConfigureAwait(true);
            if (!disposed)
            {
                workflow = prepared.Workflow;
                status = new(prepared.Heading, prepared.Detail, "", false, false);
            }
            return Status;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            status = new("Preparation stopped", "Ask your test coordinator to check this PC and release.", "", false, false);
            return status;
        }
        finally { preparing = false; }
    }

    internal bool SelectBoard()
    {
        if (disposed || workflow is not { CanChoose: true, SelectedChoice: null } current || current.Choices.Count != 1)
            return false;
        current.SelectedChoice = current.Choices[0];
        return current.CanConfirm;
    }

    internal async Task<QualificationOperationStatus?> ConfirmAndRunAsync()
    {
        if (disposed || workflow is not { CanConfirm: true } current) return null;
        current.Confirmed = true;
        if (!current.CanStart) return null;
        await current.StartAsync().ConfigureAwait(true);
        return disposed ? null : Status;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        workflow?.Invalidate();
        workflow = null;
    }
}
