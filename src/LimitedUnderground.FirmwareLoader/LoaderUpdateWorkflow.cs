using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LimitedUnderground.FirmwareLoader;

internal sealed class LoaderDeviceChoice
{
    internal LoaderDeviceChoice(HostSelection selection, int number) { Selection = selection; DisplayName = $"Device {number} · HTIT-WB32LAF V4.2"; }
    internal HostSelection Selection { get; }
    public string DisplayName { get; }
}

// UI-thread owned. Production has no factory or choices. Tests inject the accepted
// host engine, while the actual WPF controls and action handlers remain identical.
internal sealed class LoaderUpdateWorkflow : INotifyPropertyChanged
{
    private readonly Func<bool, HostSelection, HostUpdateEngine>? factory;
    private HostUpdateEngine? engine;
    private LoaderDeviceChoice? selected;
    private bool confirmed, running, recovery, uncertain;
    private long revision;
    private string heading = "Installation unavailable";
    private string detail = "Trusted release admission and a supported device adapter are not configured. Bundle inspection does not authorize an update.";
    internal LoaderUpdateWorkflow() { Choices = Array.Empty<LoaderDeviceChoice>(); }
    internal LoaderUpdateWorkflow(IEnumerable<HostSelection> choices, Func<bool, HostSelection, HostUpdateEngine> factory)
    {
        this.factory = factory;
        // Never project caller-supplied identifiers/paths into the visible device list.
        Choices = choices.Where(s => s.Target == "heltec_v4_bench" && s.Model == "HTIT-WB32LAF V4.2" && s.Generation != 0)
            .Select((s, i) => new LoaderDeviceChoice(s, i + 1)).ToArray();
        heading = "Choose the intended device";
        detail = "No device is selected automatically. Choose one device, review the checks, then confirm this exact attempt.";
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<LoaderDeviceChoice> Choices { get; private set; }
    public bool IsFixture => factory is not null;
    public string SessionLabel => IsFixture ? "SIMULATED REVIEW — no physical device operation" : "PREVIEW — inspection only";
    public string Heading => heading;
    public string Detail => detail;
    public string Target => selected is null ? "Target: no device selected" : "Target: Limited Underground Trail · HTIT-WB32LAF V4.2";
    public string Selection => selected is null ? "Device: none selected" : selected.DisplayName;
    public string Firmware => engine is null ? "Firmware: no admitted file" :
        $"Firmware: generation {engine.ReleaseManifest.Generation} · compatibility {engine.ReleaseManifest.Compatibility} · {engine.ReleaseManifest.ImageLength} bytes";
    public string ImageDigest => engine is null ? "Image SHA-256: unavailable" : $"Image SHA-256: {engine.ReleaseManifest.ImageDigest}";
    public string ActionLabel => recovery ? "Restore reviewed original" : "Start reviewed update";
    public string ConfirmationLabel => recovery ? "I confirm this device and the separately reviewed original recovery image." : "I confirm this device and the reviewed update file for one attempt.";
    public bool CanChoose => IsFixture && Choices.Count > 0;
    public bool CanConfirm => engine?.State == HostUpdateState.Ready && !running;
    public bool CanStart => CanConfirm && confirmed && (!uncertain || recovery);
    public bool CanCancel => running || engine?.State == HostUpdateState.Ready;
    public bool CanReviewRecovery => uncertain && !running && selected is not null;
    public bool IsBusy => running;
    public bool IsIndeterminate => running;
    public bool Confirmed { get => confirmed; set { confirmed = value && CanConfirm; Changed(); } }
    public LoaderDeviceChoice? SelectedChoice
    {
        get => selected;
        set
        {
            if (ReferenceEquals(selected, value)) return;
            Retire();
            selected = value is not null && Choices.Any(c => ReferenceEquals(c, value)) ? value : null;
            recovery = false;
            if (selected is null) { heading = uncertain ? "Previous attempt outcome unknown" : "Choose the intended device"; detail = uncertain ? "No device is selected. An earlier attempt remains unresolved; review the original and recovery requirements before any new operation." : "No device is selected. An earlier confirmation cannot be reused."; }
            else Prepare(false);
            Changed();
        }
    }
    private void Retire()
    {
        revision++;
        if (running) uncertain = true;
        try { engine?.Cancel(); } catch (Exception) { uncertain = true; }
        engine = null; running = false; confirmed = false;
    }
    internal void Invalidate()
    {
        Retire(); selected = null; Choices = Array.Empty<LoaderDeviceChoice>(); recovery = false;
        heading = uncertain ? "Previous attempt outcome unknown" : "Installation unavailable";
        detail = uncertain ? "The previous attempt was interrupted. Do not retry or reconnect automatically. Review the exact original and recovery requirements before any new operation." :
            "The product, file or session changed. Device selection and confirmation were cleared. Production installation remains unavailable.";
        Changed();
    }
    private void Prepare(bool forRecovery)
    {
        if (selected is null || factory is null) return;
        try
        {
            engine = factory(forRecovery, selected.Selection);
            if (!forRecovery && engine.State == HostUpdateState.RecoveryReview)
            { uncertain = true; Refresh(); return; }
            if (!engine.Select(selected.Selection)) { Refresh(); return; }
            recovery = forRecovery;
            if (uncertain && !forRecovery)
            { heading = "Recovery review required"; detail = "A previous attempt is unresolved. Review a separately admitted original before any recovery; do not retry the update."; }
            else Refresh();
        }
        catch (Exception)
        { engine = null; heading = uncertain ? "Previous attempt outcome unknown" : "Preflight blocked"; detail = uncertain ? "This preflight failed, and an earlier attempt remains unresolved. Review the original and recovery requirements; do not retry automatically." : "The required device, file, authority or recovery checks are unavailable. No new operation was started."; }
    }
    internal void ReviewRecovery()
    {
        if (!CanReviewRecovery) return;
        Retire(); recovery = true; Prepare(true); Changed();
    }
    internal void Cancel()
    {
        if (!CanCancel) return;
        try { engine?.Cancel(); } catch (Exception) { uncertain = true; }
        confirmed = false;
        if (running) { uncertain = true; heading = "Stopping — outcome unknown"; detail = "No further commands will be scheduled. Ownership remains held until the current command stops. No automatic retry or recovery."; }
        else Refresh();
        Changed();
    }
    internal async Task StartAsync()
    {
        if (!CanStart || engine is null) return;
        var operation = engine; var operationRevision = revision;
        var token = operation.Confirm(); confirmed = false; running = true; Changed();
        try { await operation.RunAsync(token); }
        catch (Exception)
        {
            if (revision != operationRevision || !ReferenceEquals(engine, operation)) return;
            uncertain = true; running = false; heading = "Outcome unknown — recovery review required";
            detail = "The attempt did not finish reliably. Do not retry automatically; review the original and recovery requirements.";
            Changed(); return;
        }
        if (revision != operationRevision || !ReferenceEquals(engine, operation)) return;
        running = false; Refresh();
    }
    internal void Refresh()
    {
        if (engine is null) { Changed(); return; }
        var state = engine.State;
        if (state is HostUpdateState.OutcomeUnknown or HostUpdateState.RecoveryReview) uncertain = true;
        (heading, detail) = state switch
        {
            HostUpdateState.Ready => (recovery ? "Recovery checks passed" : "Preflight checks passed", recovery ?
                "Review the selected device and exact original. Recovery needs its own confirmation and will be verified independently." :
                "Review the target and selected device. Confirmation applies only to this file, selection and attempt; changes invalidate it."),
            HostUpdateState.Writing or HostUpdateState.Recovering => (recovery ? "Restoring original" : "Writing update", "Write progress is not verification. Do not disconnect. Cancellation may leave an uncertain outcome."),
            HostUpdateState.Readback or HostUpdateState.RecoveryReadback => ("Written — verifying bytes", "Comparing the exact image and protected regions. A write acknowledgement does not establish success."),
            HostUpdateState.BootWait or HostUpdateState.RecoveryBootWait => ("Bytes verified — checking startup", "Waiting for the exact firmware identity and health checks. A generic heartbeat is insufficient."),
            HostUpdateState.Verified => ("Update verified", "The exact bytes, protected regions and startup checks passed for this attempt. Field and release acceptance remain separate."),
            HostUpdateState.Recovered => ("Original restored", "The independently verified original started correctly. Recovery does not make the attempted update successful."),
            HostUpdateState.OutcomeUnknown => ("Outcome unknown — recovery review required", engine.BytesVerified ?
                "Image bytes were verified, but the final outcome is unresolved. Do not retry automatically. Review the original and recovery requirements." :
                "The attempt may have changed the device. Do not retry, reset or restore automatically. Review the exact original and fresh recovery authority."),
            HostUpdateState.RecoveryReview => ("Recovery review required", "An earlier attempt is unresolved. Select and separately review the exact original, current device and recovery authority."),
            HostUpdateState.NoWriteStopped => ("Stopped before writing", "No update command was submitted. Selection, file, authority or confirmation is missing, stale or cancelled. Review the checks before a new attempt."),
            _ => ("Choose the intended device", "Nothing is selected automatically. An explicit current selection and confirmation are required.")
        };
        if (uncertain && state == HostUpdateState.NoWriteStopped)
        { heading = "Previous attempt outcome unknown"; detail = "This selection did not start a new write, but an earlier attempt remains unresolved. Do not retry automatically. Review the original and recovery requirements."; }
        if (state == HostUpdateState.Recovered) uncertain = false;
        Changed();
    }
    private void Changed([CallerMemberName] string? ignored = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
