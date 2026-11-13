using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LimitedUnderground.FirmwareLoader;

internal sealed class LoaderDeviceChoice
{
    internal LoaderDeviceChoice(HostSelection selection, int number) { Selection = selection; DisplayName = $"Heltec V4 board on USB ({number}) · HTIT-WB32LAF V4.2"; }
    internal HostSelection Selection { get; }
    public string DisplayName { get; }
}

// One line of the install step list (L9 tester flow). Text only; no device identifiers.
internal enum LoaderStepStatus { Waiting, Active, Done, Failed, NotReached }
internal sealed record LoaderStep(string Label, LoaderStepStatus Status)
{
    public string Mark => Status switch
    {
        LoaderStepStatus.Done => "\u2713",
        LoaderStepStatus.Active => "\u25B6",
        LoaderStepStatus.Failed => "\u2717",
        LoaderStepStatus.NotReached => "-",
        _ => "\u25CB",
    };
    public string StatusText => Status switch
    {
        LoaderStepStatus.Done => "done",
        LoaderStepStatus.Active => "in progress",
        LoaderStepStatus.Failed => "stopped here",
        LoaderStepStatus.NotReached => "not reached",
        _ => "waiting",
    };
}

// UI-thread owned. Production has no factory or choices. Tests inject the accepted
// host engine, while the actual WPF controls and action handlers remain identical.
internal sealed class LoaderUpdateWorkflow : INotifyPropertyChanged
{
    private readonly Func<bool, HostSelection, HostUpdateEngine>? factory;
    private HostUpdateEngine? engine;
    private LoaderDeviceChoice? selected;
    private bool confirmed, running, recovery, uncertain;
    private int lastStage = -1;  // furthest install step seen while running (0 check .. 3 start)
    private long revision;
    private string heading = "Installation unavailable";
    private string detail = "Trusted release admission and a supported device adapter are not configured. Bundle inspection does not authorize an update.";
    internal LoaderUpdateWorkflow() { Choices = Array.Empty<LoaderDeviceChoice>(); }
    private readonly bool production;
    internal LoaderUpdateWorkflow(IEnumerable<HostSelection> choices, Func<bool, HostSelection, HostUpdateEngine> factory, bool production = false)
    {
        this.factory = factory;
        this.production = production;
        // Never project caller-supplied identifiers/paths into the visible device list.
        Choices = choices.Where(s => s.Target == "heltec_v4_bench" && s.Model == "HTIT-WB32LAF V4.2" && s.Generation != 0)
            .Select((s, i) => new LoaderDeviceChoice(s, i + 1)).ToArray();
        heading = "Choose the intended device";
        detail = "No device is selected automatically. Choose one device, review the checks, then confirm this exact attempt.";
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<LoaderDeviceChoice> Choices { get; private set; }
    public bool IsFixture => factory is not null && !production;
    public bool IsProduction => factory is not null && production;
    public string SessionLabel => IsProduction ? "QUALIFICATION RELEASE — board A only · writes to the connected board" :
        IsFixture ? "SIMULATED REVIEW — no physical device operation" : "PREVIEW — inspection only";
    public string Heading => heading;
    public string Detail => detail;
    public string Target => selected is null ? "Target: no device selected" : "Target: Limited Underground Trail · HTIT-WB32LAF V4.2";
    public string Selection => selected is null ? "Device: none selected" : selected.DisplayName;
    public string Firmware => engine is null ? "Firmware: no admitted file" :
        $"Firmware: generation {engine.ReleaseManifest.Generation} · compatibility {engine.ReleaseManifest.Compatibility} · {engine.ReleaseManifest.ImageLength} bytes";
    public string ImageDigest => engine is null ? "Image SHA-256: unavailable" : $"Image SHA-256: {engine.ReleaseManifest.ImageDigest}";
    public string ActionLabel => recovery ? "Restore reviewed original" : "Start reviewed update";
    public string ConfirmationLabel => recovery ? "I confirm this device and the separately reviewed original recovery image." : "I confirm this device and the reviewed update file for one attempt.";
    public bool CanChoose => factory is not null && Choices.Count > 0;
    public bool CanConfirm => engine?.State == HostUpdateState.Ready && !running;
    public bool CanStart => CanConfirm && confirmed && (!uncertain || recovery);
    public bool CanCancel => running || engine?.State == HostUpdateState.Ready;
    public bool CanReviewRecovery => uncertain && !running && selected is not null;
    // ---- L9 tester flow: step list, plain summary, next step, support file ----
    internal static readonly string[] StepLabels = { "Check the board", "Write the firmware", "Verify the written bytes", "Check that it starts" };
    public IReadOnlyList<LoaderStep> Steps => BuildSteps();
    public double ProgressValue => Steps.Count(s => s.Status == LoaderStepStatus.Done) / (double)StepLabels.Length;
    public string Summary => engine is null ? "No firmware release is ready to install yet." :
        $"{(recovery ? "Put back" : "Install")} Limited Underground Trail, release generation {engine.ReleaseManifest.Generation}, on the selected Heltec V4 board.";
    public string NextStep => NextStepText();
    public bool CanSaveSupportFile => engine is not null || uncertain;
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
        lastStage = -1;
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
    // Panel timer tick. While an attempt runs, refresh progress. While Ready and idle, passively
    // revalidate the selected device and authority so a disconnect, reconnect or authority change
    // clears the confirmation immediately rather than surfacing only when Start is pressed.
    internal void Poll()
    {
        if (running) { Refresh(); return; }
        if (engine is null || engine.State != HostUpdateState.Ready) return;
        if (!engine.Revalidate()) { confirmed = false; Refresh(); }
    }
    internal async Task StartAsync()
    {
        if (!CanStart || engine is null) return;
        var operation = engine; var operationRevision = revision;
        var token = operation.Confirm(); confirmed = false; running = true; lastStage = 1; Changed();
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
        int stage = StageOf(state);
        if (running && stage > lastStage) lastStage = stage;
        if (state is HostUpdateState.OutcomeUnknown or HostUpdateState.RecoveryReview) uncertain = true;
        (heading, detail) = state switch
        {
            HostUpdateState.Ready => (recovery ? "Recovery checks passed" : "Preflight checks passed", recovery ?
                "Review the selected device and exact original. Recovery needs its own confirmation and will be verified independently." :
                "Review the target and selected device. Confirmation applies only to this file, selection and attempt; changes invalidate it."),
            HostUpdateState.Writing or HostUpdateState.Recovering => (recovery ? "Restoring original" : "Writing update", "Write progress is not verification. Do not disconnect. Cancellation may leave an uncertain outcome."),
            HostUpdateState.Readback or HostUpdateState.RecoveryReadback => ("Written — verifying bytes", "Checking the device's own checksum of the written image and of every protected region. A write acknowledgement does not establish success."),
            HostUpdateState.BootWait or HostUpdateState.RecoveryBootWait => ("Bytes verified — checking startup", "Waiting for the exact firmware identity and health checks. A generic heartbeat is insufficient."),
            HostUpdateState.Verified => ("Update verified", "The written image checksum, protected regions and startup checks passed for this attempt. Field and release acceptance remain separate."),
            HostUpdateState.Recovered => ("Original restored", "The independently verified original started correctly. Recovery does not make the attempted update successful."),
            HostUpdateState.OutcomeUnknown => ("Outcome unknown — recovery review required", UnknownCause(engine) + " " + (engine.BytesVerified ?
                "Image bytes were verified, but the final outcome is unresolved. Do not retry automatically. Review the original and recovery requirements." :
                "The attempt may have changed the device. Do not retry, reset or restore automatically. Review the exact original and fresh recovery authority.")),
            HostUpdateState.RecoveryReview => ("Recovery review required", "An earlier attempt is unresolved. Select and separately review the exact original, current device and recovery authority."),
            HostUpdateState.NoWriteStopped => NoWriteStatus(engine),
            _ => ("Choose the intended device", "Nothing is selected automatically. An explicit current selection and confirmation are required.")
        };
        if (uncertain && state == HostUpdateState.NoWriteStopped)
        { heading = "Previous attempt outcome unknown"; detail = "This selection did not start a new write, but an earlier attempt remains unresolved. Do not retry automatically. Review the original and recovery requirements."; }
        if (state == HostUpdateState.Recovered) uncertain = false;
        Changed();
    }
    private static int StageOf(HostUpdateState state) => state switch
    {
        HostUpdateState.Ready => 0,
        HostUpdateState.Writing or HostUpdateState.Recovering => 1,
        HostUpdateState.Readback or HostUpdateState.RecoveryReadback => 2,
        HostUpdateState.BootWait or HostUpdateState.RecoveryBootWait => 3,
        HostUpdateState.Verified or HostUpdateState.Recovered => 4,
        _ => -1,
    };

    private IReadOnlyList<LoaderStep> BuildSteps()
    {
        var status = new LoaderStepStatus[StepLabels.Length];
        var state = engine?.State ?? HostUpdateState.DeviceUnselected;
        int stage = StageOf(state);
        if (engine is null || state == HostUpdateState.DeviceUnselected)
        {
            if (uncertain) { for (int i = 0; i < status.Length; i++) status[i] = LoaderStepStatus.NotReached; }
        }
        else if (state == HostUpdateState.NoWriteStopped)
        {
            status[0] = LoaderStepStatus.Failed;
            for (int i = 1; i < status.Length; i++) status[i] = LoaderStepStatus.NotReached;
        }
        else if (state is HostUpdateState.OutcomeUnknown or HostUpdateState.RecoveryReview)
        {
            // Stopped at the furthest step reached; verified bytes mean the write and the byte check passed.
            int failed = engine.BytesVerified || engine.Refusal is "boot-identity-health" or "completion-marker" ? 3 :
                engine.Refusal == "independent-readback" ? 2 : Math.Max(lastStage, engine.AttemptStarted ? 1 : 0);
            for (int i = 0; i < status.Length; i++)
                status[i] = i < failed ? LoaderStepStatus.Done : i == failed ? LoaderStepStatus.Failed : LoaderStepStatus.NotReached;
        }
        else
        {
            for (int i = 0; i < status.Length; i++)
                status[i] = i < stage || (i == 0 && stage == 0) ? LoaderStepStatus.Done :
                    i == stage && running ? LoaderStepStatus.Active : LoaderStepStatus.Waiting;
        }
        return StepLabels.Select((label, i) => new LoaderStep(label, status[i])).ToArray();
    }

    private string NextStepText()
    {
        if (engine is null)
            return uncertain ? "Do not retry. Save a support file and send it with a description of what happened." :
                Choices.Count == 0 ? "Connect one Heltec V4 board and open a signed release." : "Choose the board to install on.";
        return engine.State switch
        {
            HostUpdateState.Ready when uncertain && !recovery => "An earlier install did not finish. Save a support file before doing anything else.",
            HostUpdateState.Ready => "Tick the confirmation box, then press the button to start. Keep the board plugged in.",
            HostUpdateState.Writing or HostUpdateState.Readback or HostUpdateState.BootWait or
            HostUpdateState.Recovering or HostUpdateState.RecoveryReadback or HostUpdateState.RecoveryBootWait =>
                "Keep the board plugged in until all four steps are done.",
            HostUpdateState.Verified => "Done. The board is running the new firmware; you can unplug it.",
            HostUpdateState.Recovered => "The previous firmware is back. Save a support file and report the failed install.",
            HostUpdateState.NoWriteStopped when engine.BindingFault is HostBindingFault.DeviceDisconnected or HostBindingFault.DeviceChanged =>
                "Nothing was written. Plug the board back in, choose it again and start again.",
            HostUpdateState.NoWriteStopped => "Nothing was written. Save a support file if this happens again.",
            HostUpdateState.OutcomeUnknown or HostUpdateState.RecoveryReview =>
                "Do not unplug, retry or reset the board. Save a support file and send it before doing anything else.",
            _ => "Choose the board to install on.",
        };
    }

    // Plain-text support file (fixed fields only). Never includes ports, serial numbers, MAC
    // addresses, paths, keys or other device identifiers (AGENTS.md "Fail-closed safety and privacy").
    internal string SupportReport(DateTimeOffset now, string loaderVersion)
    {
        var lines = new List<string>
        {
            "Limited Underground Firmware Loader - support file",
            $"Created (UTC): {now.UtcDateTime:yyyy-MM-dd HH:mm:ss}",
            $"Loader version: {loaderVersion}",
            $"Session: {(IsProduction ? "qualification release" : IsFixture ? "simulated review" : "preview")}",
            $"Mode: {(recovery ? "put back previous firmware" : "install")}",
            $"Status: {Heading}",
            $"Detail: {Detail}",
            "Steps: " + string.Join("; ", Steps.Select(s => $"{s.Label}: {s.StatusText}")),
            $"Earlier attempt unresolved: {(uncertain ? "yes" : "no")}",
        };
        if (engine is not null)
        {
            lines.Add($"Engine state: {engine.State}");
            lines.Add($"Stop reason code: {(engine.Refusal.Length == 0 ? "none" : engine.Refusal)}");
            lines.Add($"Binding check: {engine.BindingFault}");
            lines.Add($"Write started: {(engine.AttemptStarted ? "yes" : "no")}");
            lines.Add($"Bytes verified: {(engine.BytesVerified ? "yes" : "no")}");
            lines.Add($"Safety journal kept its record: {(engine.UncertaintyMarkerRetained ? "yes" : "no")}");
            lines.Add($"Release generation: {engine.ReleaseManifest.Generation}");
            lines.Add($"Release compatibility: {engine.ReleaseManifest.Compatibility}");
            lines.Add($"Image bytes: {engine.ReleaseManifest.ImageLength}");
            lines.Add($"Image SHA-256: {engine.ReleaseManifest.ImageDigest}");
        }
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    // Fixed status text chosen from the engine's fixed refusal and binding-fault codes only.
    // No adapter, exception or device-identifying text can reach these strings.
    private static (string Heading, string Detail) NoWriteStatus(HostUpdateEngine engine)
    {
        var fault = engine.BindingFault;
        string heading = !engine.AttemptStarted && fault == HostBindingFault.DeviceDisconnected ? "Device disconnected" :
            !engine.AttemptStarted && fault == HostBindingFault.DeviceChanged ? "Device changed — select it again" :
            "Stopped before writing";
        string next = fault is HostBindingFault.DeviceDisconnected or HostBindingFault.DeviceChanged or HostBindingFault.DeviceUnselected
            ? "Reconnect or choose the intended device again, review the checks, then confirm a new attempt."
            : "Review the checks before a new attempt.";
        string journal = engine.UncertaintyMarkerRetained
            ? " The safety journal could not confirm that it withdrew its record of this attempt, so another attempt may require recovery review."
            : "";
        return (heading, StopCause(engine) + " No update command was sent." + journal + " " + next);
    }

    private static string StopCause(HostUpdateEngine engine) => engine.Refusal switch
    {
        "exclusive-owner" => "Another loader operation currently owns the device.",
        "uncertainty-marker" => "The safety journal could not record the attempt before writing.",
        "deadline" => "The operation time limit expired before writing started.",
        "confirmation-consumed" => "That confirmation was already used.",
        "adapter-cancel-failure" => "Cancellation could not be confirmed cleanly.",
        "adapter-or-authority-failure" => "A required check failed unexpectedly.",
        _ => FaultSentence(engine.BindingFault),
    };

    private static string UnknownCause(HostUpdateEngine engine)
    {
        bool bindingChanged = engine.BindingFault is not (HostBindingFault.None or HostBindingFault.Cancelled);
        return engine.Refusal switch
        {
            "independent-readback" => "Readback did not match the admitted image or its protected regions.",
            "boot-identity-health" => "Startup did not report the exact firmware identity, health and protected state.",
            "deadline" => "The device did not finish within its time limit.",
            "completion-marker" => "Every check passed, but the safety journal could not record completion.",
            "adapter-or-authority-failure" => "The device connection reported a failure while a command may have been in progress.",
            "adapter-cancel-failure" => "Cancellation could not be confirmed while a command may have been in progress.",
            _ when bindingChanged => "The connection or authority changed during the attempt. " + FaultSentence(engine.BindingFault),
            "cancelled" => "The attempt was cancelled after a command may have reached the device.",
            _ => "The attempt stopped while a command may have been in progress.",
        };
    }

    private static string FaultSentence(HostBindingFault fault) => fault switch
    {
        HostBindingFault.Cancelled => "The attempt was cancelled.",
        HostBindingFault.AuthorityChanged => "The product session, inspected file or release authority changed, expired or was revoked.",
        HostBindingFault.DeviceUnselected => "No device is selected.",
        HostBindingFault.DeviceDisconnected => "The selected device is no longer connected.",
        HostBindingFault.DeviceChanged => "The connected device changed or reconnected since it was selected, so the earlier selection and confirmation no longer apply.",
        HostBindingFault.DeviceNotSupported => "The selected device does not match the exact supported model, target and adapter.",
        HostBindingFault.ProfileIncomplete => "The adapter timing and side-effect profile is incomplete.",
        HostBindingFault.JournalState => "An unresolved earlier attempt, or a mismatched recovery original, blocks this operation.",
        HostBindingFault.CheckFailed => "The device or authority check could not be completed.",
        _ => "Selection, file, authority or confirmation is missing, stale or cancelled.",
    };

    private void Changed([CallerMemberName] string? ignored = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
