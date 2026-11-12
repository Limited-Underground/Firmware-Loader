namespace LimitedUnderground.FirmwareLoader;

internal enum HostUpdateState { DeviceUnselected, Ready, Writing, Readback, BootWait, Verified,
    NoWriteStopped, OutcomeUnknown, RecoveryReview, Recovering, RecoveryReadback, RecoveryBootWait, Recovered }
// Which binding check failed. Fixed codes only; they never carry device identifiers.
internal enum HostBindingFault { None, Cancelled, AuthorityChanged, DeviceUnselected, DeviceDisconnected,
    DeviceChanged, DeviceNotSupported, ProfileIncomplete, JournalState, CheckFailed }
internal sealed record HostSelection(object Identity, ulong Generation, string Target, string Model, string Adapter);
internal sealed record HostAdapterProfile(int WriteMilliseconds, int ReadMilliseconds, int BootMilliseconds,
    int TotalMilliseconds, int MaximumChunk, bool SideEffectsApproved, bool BootApproved);
// D-063: a write is verified by the chip's own MD5 of the written region (transfer integrity;
// the release signature already proved authenticity). The engine computes the expected digest itself.
internal sealed record HostRegionCheck(string RegionMd5, bool ProtectedSpansMatch)
{
    internal static string Md5Hex(byte[] bytes) => Convert.ToHexString(System.Security.Cryptography.MD5.HashData(bytes));
    internal static HostRegionCheck Of(byte[] observedBytes, bool protectedSpansMatch) => new(Md5Hex(observedBytes), protectedSpansMatch);
}
internal sealed record HostBootResult(string ImageDigest, bool Healthy, bool ProtectedStateMatches);

// Only deterministic host fixtures implement these interfaces in this checkpoint.
// No implementation is registered or reachable from the production UI.
internal interface IHostUpdateAdapter
{
    // MUST be a passive, non-blocking read of an observation the adapter already holds. It must
    // never open, probe, reset or otherwise touch a device (on ESP32 USB-serial boards merely
    // opening a port can toggle DTR/RTS and reset the chip). The UI revalidates it about every
    // 100 ms while Ready, and the engine withdraws a no-write uncertainty marker only because
    // nothing but the three selection-bound commands below can change device state.
    HostSelection? CurrentSelection { get; }
    // Implementations MUST atomically compare expected selection/connection identity
    // at command admission, before any effect. Rechecking after a command is insufficient.
    Task WriteAsync(HostSelection expected, uint offset, byte[] image, int maximumChunk, CancellationToken cancellation);
    Task<HostRegionCheck> ReadbackAsync(HostSelection expected, uint offset, int length, CancellationToken cancellation);
    Task<HostBootResult> BootAsync(HostSelection expected, CancellationToken cancellation);
}
// Host-side exclusive ownership only. Acquiring or releasing it MUST NOT open or signal a
// device; any device-affecting step belongs in a selection-bound adapter command.
internal interface IHostOperationLease { IDisposable? TryAcquire(); }
internal interface IHostUncertaintyJournal
{
    bool HasUnresolvedOperation { get; }
    string? RecoveryDigest { get; }
    bool MarkUncertain(string recoveryDigest);
    bool MarkComplete();
    // Withdraw this live owner's own marker after the engine proves no adapter command was
    // submitted. Implementations restore the exact prior resolved record and MUST refuse when
    // the marker replaced an earlier unresolved operation.
    bool ReleaseUnsubmitted();
}
internal sealed class HostConfirmation
{
    internal HostConfirmation(object owner) { Owner = owner; }
    internal object Owner { get; }
    internal int Consumed;
}

internal sealed class HostUpdateEngine
{
    private readonly object commandGate = new();
    private int cancelled;
    private object confirmationOwner = new();
    private readonly LoaderSessionController controller;
    private readonly LoaderBundleInspectionContext context;
    private readonly FirmwareBundleCandidateResult inspection;
    private readonly Func<FirmwareBundleCandidateResult?> currentInspection;
    private readonly HostAdmittedRelease release;
    private readonly HostAdmissionPolicy policy;
    private readonly Func<HostAdmissionPolicy?> currentPolicy;
    private readonly IHostUpdateAdapter adapter;
    private readonly IHostOperationLease ownership;
    private readonly IHostUncertaintyJournal journal;
    private readonly Func<long> milliseconds;
    private readonly HostAdapterProfile profile;
    private readonly bool recovery;
    private HostSelection? selection;
    private readonly CancellationTokenSource cancel = new();
    private int started;
    private bool mutationPossible;
    private bool markerWritten;
    private bool completionClaimed;
    private long startTime;
    private long lastTime;
    internal HostUpdateState State { get; private set; }
    internal string Refusal { get; private set; } = "";
    internal bool BytesVerified { get; private set; }
    internal HostBindingFault BindingFault { get; private set; }
    internal bool AttemptStarted => Volatile.Read(ref started) != 0;
    // Set when a stop after the durable marker but before any command withdrew (or could not
    // withdraw) the marker. Neither value ever applies once a command may have been submitted.
    internal bool UncertaintyMarkerReleased { get; private set; }
    internal bool UncertaintyMarkerRetained { get; private set; }
    internal HostReleaseManifest ReleaseManifest => release.Manifest;

    internal HostUpdateEngine(LoaderSessionController controller, LoaderBundleInspectionContext context,
        FirmwareBundleCandidateResult inspection, Func<FirmwareBundleCandidateResult?> currentInspection, HostAdmittedRelease release, HostAdmissionPolicy policy,
        Func<HostAdmissionPolicy?> currentPolicy, IHostUpdateAdapter adapter, IHostOperationLease ownership,
        IHostUncertaintyJournal journal, Func<long> milliseconds, HostAdapterProfile profile, bool recovery = false)
    {
        this.controller = controller; this.context = context; this.inspection = inspection; this.currentInspection = currentInspection;
        this.release = release; this.policy = policy; this.currentPolicy = currentPolicy;
        this.adapter = adapter; this.ownership = ownership; this.journal = journal;
        this.milliseconds = milliseconds; this.profile = profile; this.recovery = recovery;
        State = journal.HasUnresolvedOperation ? HostUpdateState.RecoveryReview : HostUpdateState.DeviceUnselected;
    }

    internal bool Select(HostSelection chosen)
    {
        lock (commandGate)
        {
            if (Volatile.Read(ref started) != 0 || Volatile.Read(ref cancelled) != 0) return false;
            confirmationOwner = new object();
            selection = chosen;
            if (!CheckBinding()) { Stop("selection-or-authority"); return false; }
            BindingFault = HostBindingFault.None;
            State = HostUpdateState.Ready;
            return true;
        }
    }

    // Passive recheck while Ready and not started: reads only the adapter's current observation
    // and the existing authority callbacks. A stale binding (for example a disconnected or
    // reconnected device) revokes any issued confirmation and stops before any command.
    // Returns true only while the engine remains Ready with a current binding.
    internal bool Revalidate()
    {
        lock (commandGate)
        {
            if (State != HostUpdateState.Ready || Volatile.Read(ref started) != 0 ||
                Volatile.Read(ref cancelled) != 0) return false;
            bool current;
            try { current = CheckBinding(); }
            catch (Exception) { BindingFault = HostBindingFault.CheckFailed; current = false; }
            if (current) return true;
            confirmationOwner = new object();
            Stop("selection-or-authority");
            return false;
        }
    }

    // Explicit host confirmation, intentionally internal and not a production operation permission.
    internal HostConfirmation? Confirm()
    {
        lock (commandGate)
            return State == HostUpdateState.Ready && Volatile.Read(ref started) == 0 &&
                Volatile.Read(ref cancelled) == 0 ? new(confirmationOwner) : null;
    }

    internal void Cancel()
    {
        lock (commandGate)
        {
            if (completionClaimed) return;
            Volatile.Write(ref cancelled, 1);
            if (Volatile.Read(ref started) == 0) Stop("cancelled");
        }
        cancel.Cancel();
    }

    private bool CheckBinding()
    {
        var fault = ClassifyBinding();
        if (fault != HostBindingFault.None) BindingFault = fault;
        return fault == HostBindingFault.None;
    }

    // The same conjunction as the original single predicate, evaluated in the same order;
    // it additionally reports which group failed first so status can be specific.
    private HostBindingFault ClassifyBinding()
    {
        var actual = adapter.CurrentSelection;
        long now = milliseconds();
        if (Volatile.Read(ref cancelled) != 0) return HostBindingFault.Cancelled;
        if (!(controller.CanPublishOfflineBundleInspection(context, inspection) &&
            context.ProductKey == "opentrail" && ReferenceEquals(inspection, currentInspection()) &&
            inspection.TargetKey == release.Manifest.Target && inspection.ImageSha256 == release.Manifest.ImageDigest &&
            inspection.ImageBytes == release.Manifest.ImageLength && inspection.ReleaseGeneration == release.Manifest.Generation &&
            policy == release.Policy && policy == currentPolicy() && !policy.Revoked && now >= 0 && now < policy.ValidUntil &&
            release.Manifest.PolicyRevision == policy.Revision)) return HostBindingFault.AuthorityChanged;
        if (selection is null) return HostBindingFault.DeviceUnselected;
        if (actual is null) return HostBindingFault.DeviceDisconnected;
        if (!ReferenceEquals(selection.Identity, actual.Identity) || selection != actual) return HostBindingFault.DeviceChanged;
        if (selection.Generation == 0 || selection.Target != "heltec_v4_bench" ||
            selection.Model != "HTIT-WB32LAF V4.2" || selection.Adapter != "host-fixture-v1") return HostBindingFault.DeviceNotSupported;
        if (!(profile.WriteMilliseconds > 0 && profile.ReadMilliseconds > 0 && profile.BootMilliseconds > 0 &&
            profile.TotalMilliseconds > 0 && profile.MaximumChunk > 0 && profile.MaximumChunk <= 65536 &&
            profile.SideEffectsApproved && profile.BootApproved)) return HostBindingFault.ProfileIncomplete;
        if (!(recovery ? journal.HasUnresolvedOperation && journal.RecoveryDigest == release.Manifest.ImageDigest
                       : !journal.HasUnresolvedOperation || markerWritten)) return HostBindingFault.JournalState;
        return Volatile.Read(ref cancelled) == 0 ? HostBindingFault.None : HostBindingFault.Cancelled;
    }

    private void Stop(string reason)
    {
        Refusal = reason;
        State = mutationPossible ? HostUpdateState.OutcomeUnknown :
            recovery ? HostUpdateState.RecoveryReview : HostUpdateState.NoWriteStopped;
    }

    private int Remaining(int phaseBudget)
    {
        long now = milliseconds();
        if (now < lastTime || now < startTime || now - startTime >= profile.TotalMilliseconds)
            throw new TimeoutException();
        lastTime = now;
        return (int)Math.Min(phaseBudget, profile.TotalMilliseconds - (now - startTime));
    }

    private T Submit<T>(Func<T> command, bool firstMutation = false) where T : Task
    {
        lock (commandGate)
        {
            if (!CheckBinding()) throw new OperationCanceledException();
            if (firstMutation) mutationPossible = true;
            return command();
        }
    }

    private void CheckPhase(long began, int budget)
    {
        Remaining(budget);
        if (lastTime - began >= budget) throw new TimeoutException();
    }

    internal async Task RunAsync(HostConfirmation? confirmation)
    {
        lock (commandGate)
        {
            if (confirmation is null || !ReferenceEquals(confirmation.Owner, confirmationOwner) ||
                Interlocked.CompareExchange(ref started, 1, 0) != 0) return;
            if (Interlocked.Exchange(ref confirmation.Consumed, 1) != 0) { Stop("confirmation-consumed"); return; }
        }
        IDisposable? lease = null;
        Task? pending = null;
        using var commandCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);
        try
        {
            if (!CheckBinding()) { Stop("preflight-binding"); return; }
            lease = ownership.TryAcquire();
            if (lease is null) { Stop("exclusive-owner"); return; }
            if (!CheckBinding()) { Stop("preflight-binding"); return; }
            startTime = lastTime = milliseconds();
            // Mark possible mutation durably BEFORE submitting anything. Failure stops without a command.
            if (!journal.MarkUncertain(recovery ? release.Manifest.ImageDigest : release.Manifest.RecoveryDigest))
            { Stop("uncertainty-marker"); return; }
            markerWritten = true;
            if (!CheckBinding()) { Stop("preflight-binding"); return; }

            State = recovery ? HostUpdateState.Recovering : HostUpdateState.Writing;
            var bytes = release.CopyImage();
            int budget = Remaining(profile.WriteMilliseconds);
            long phaseStart = milliseconds();
            pending = Submit(() => adapter.WriteAsync(selection!, release.Manifest.Offset, bytes, profile.MaximumChunk, commandCancellation.Token), firstMutation: true);
            await pending.WaitAsync(TimeSpan.FromMilliseconds(budget), commandCancellation.Token).ConfigureAwait(false);
            CheckPhase(phaseStart, budget);
            if (!CheckBinding()) { Stop("changed-after-write"); return; }
            State = recovery ? HostUpdateState.RecoveryReadback : HostUpdateState.Readback;
            budget = Remaining(profile.ReadMilliseconds);
            phaseStart = milliseconds();
            var read = Submit(() => adapter.ReadbackAsync(selection!, release.Manifest.Offset, release.Manifest.ImageLength, commandCancellation.Token));
            pending = read;
            var observed = await read.WaitAsync(TimeSpan.FromMilliseconds(budget), commandCancellation.Token).ConfigureAwait(false);
            CheckPhase(phaseStart, budget);
            if (!CheckBinding()) { Stop("changed-after-readback"); return; }
            if (!observed.ProtectedSpansMatch || !string.Equals(observed.RegionMd5, HostRegionCheck.Md5Hex(release.CopyImage()), StringComparison.Ordinal))
            { Stop("independent-readback"); return; }
            BytesVerified = true;
            State = recovery ? HostUpdateState.RecoveryBootWait : HostUpdateState.BootWait;
            budget = Remaining(profile.BootMilliseconds);
            phaseStart = milliseconds();
            var boot = Submit(() => adapter.BootAsync(selection!, commandCancellation.Token));
            pending = boot;
            var startup = await boot.WaitAsync(TimeSpan.FromMilliseconds(budget), commandCancellation.Token).ConfigureAwait(false);
            CheckPhase(phaseStart, budget);
            if (!CheckBinding()) { Stop("changed-after-boot"); return; }
            if (startup.ImageDigest != release.Manifest.ImageDigest || !startup.Healthy || !startup.ProtectedStateMatches)
            { Stop("boot-identity-health"); return; }
            lock (commandGate)
            {
                if (!CheckBinding()) { Stop("changed-before-completion"); return; }
                // Linearization point: verified evidence claims completion before journal commit.
                // Cancel before this point wins; cancellation after it cannot undo verified bytes.
                // Even a reentrant journal callback cannot cancel a claimed terminal commit.
                completionClaimed = true;
                if (!journal.MarkComplete()) { Stop("completion-marker"); return; }
                State = recovery ? HostUpdateState.Recovered : HostUpdateState.Verified;
            }
        }
        catch (OperationCanceledException) { Stop("cancelled"); }
        catch (TimeoutException) { Stop("deadline"); }
        catch (Exception) { Stop("adapter-or-authority-failure"); }
        finally
        {
            // Cancellation/timeout cannot free an adapter still doing work. Late completion
            // only releases ownership and can never advance the operation or a new session.
            try { commandCancellation.Cancel(); }
            catch (Exception) { Stop("adapter-cancel-failure"); }
            // Before mutation, a stop means no update began (LUF-0009a). If the durable marker
            // was written but no adapter command was ever submitted, withdraw this owner's own
            // update marker while ownership is still held, so a no-write stop does not demand a
            // recovery write. Recovery keeps its pre-existing marker; failure keeps it (fail closed).
            if (markerWritten && !mutationPossible && !recovery)
            {
                bool released;
                try { released = journal.ReleaseUnsubmitted(); }
                catch (Exception) { released = false; }
                if (released) UncertaintyMarkerReleased = true; else UncertaintyMarkerRetained = true;
            }
            if (pending is { IsCompleted: false } && lease is not null)
            {
                var retained = lease;
                _ = pending.ContinueWith(task => { _ = task.Exception; retained.Dispose(); },
                    CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                lease = null;
            }
            lease?.Dispose();
        }
    }
}
