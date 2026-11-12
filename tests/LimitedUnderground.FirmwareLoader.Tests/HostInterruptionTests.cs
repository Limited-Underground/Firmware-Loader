using LimitedUnderground.FirmwareLoader;
using Fixture = HostUpdateEngineTests.Fixture;

// Interrupted, failed and disconnected attempts against the actual host engine and the
// actual update workflow projection (no WPF needed). Fixture adapters only; no hardware.
internal static class HostInterruptionTests
{
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("interruption disconnect after durable marker withdraws it and sends nothing", DisconnectAfterMarker),
        ("interruption cancel revoke or deadline after marker withdraws it before any command", OtherStopsAfterMarker),
        ("interruption marker withdrawal failure keeps recovery requirement", WithdrawalFailure),
        ("interruption throwing withdrawal stays fail-closed and still releases ownership", WithdrawalThrows),
        ("interruption recovery pre-submission stop never clears the earlier marker", RecoveryKeepsMarker),
        ("interruption any possible submission keeps the marker", PossibleSubmissionKeepsMarker),
        ("connection revalidation detects disconnect reconnect and authority change while ready", Revalidation),
        ("connection revalidation is passive outside ready and fails closed on check errors", RevalidationBoundaries),
        ("status disconnect while ready clears confirmation and recovers on reconnect", WorkflowDisconnect),
        ("status pre-submission disconnect reports no write and needs no recovery", WorkflowNoWrite),
        ("status each possible-write failure names its cause without private detail", WorkflowUnknownCauses),
        ("status pre-write refusals name their cause", WorkflowRefusalCauses),
        ("status production workflow polling stays inert without any engine or device", ProductionPollInert),
    };

    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("FAILED: " + message); }
    private static void Run(HostUpdateEngine engine) => engine.RunAsync(engine.Confirm()).GetAwaiter().GetResult();

    private static void DisconnectAfterMarker()
    {
        using var f = new Fixture();
        f.Journal.OnMarkUncertain = () => f.Adapter.CurrentSelection = null;
        Run(f.Engine);
        Require(f.Engine.State == HostUpdateState.NoWriteStopped && f.Adapter.Writes == 0 && f.Adapter.Reads == 0 && f.Adapter.Boots == 0,
            "no command after a disconnect observed post-marker");
        Require(f.Engine.BindingFault == HostBindingFault.DeviceDisconnected && f.Engine.UncertaintyMarkerReleased &&
            !f.Engine.UncertaintyMarkerRetained, "specific fault and withdrawn marker");
        Require(!f.Journal.HasUnresolvedOperation && f.Journal.Releases == 1 && !f.Lease.Held, "journal resolved and ownership released");
        f.Journal.OnMarkUncertain = null;
        var next = f.NewEngine();
        Require(next.State == HostUpdateState.DeviceUnselected, "no-write stop does not demand recovery review");
        f.Adapter.CurrentSelection = f.Selection with { Identity = new object(), Generation = 2 };
        Require(next.Select(f.Adapter.CurrentSelection), "fresh explicit selection after reconnect");
        Run(next);
        Require(next.State == HostUpdateState.Verified && f.Adapter.Writes == 1, "new attempt proceeds normally");
    }

    private static void OtherStopsAfterMarker()
    {
        using (var f = new Fixture())
        {
            f.Journal.OnMarkUncertain = f.Engine.Cancel;
            Run(f.Engine);
            Require(f.Engine.State == HostUpdateState.NoWriteStopped && f.Engine.BindingFault == HostBindingFault.Cancelled &&
                f.Adapter.Writes == 0 && !f.Journal.HasUnresolvedOperation && f.Journal.Releases == 1, "cancel after marker");
        }
        using (var f = new Fixture())
        {
            f.Journal.OnMarkUncertain = () => f.CurrentPolicy = f.Policy with { Revoked = true };
            Run(f.Engine);
            Require(f.Engine.State == HostUpdateState.NoWriteStopped && f.Engine.BindingFault == HostBindingFault.AuthorityChanged &&
                f.Adapter.Writes == 0 && !f.Journal.HasUnresolvedOperation, "revocation after marker");
        }
        using (var f = new Fixture())
        {
            // A slow durable flush can consume the whole operation budget before the first command.
            f.Journal.OnMarkUncertain = () => f.Now += f.Profile.TotalMilliseconds;
            Run(f.Engine);
            Require(f.Engine.State == HostUpdateState.NoWriteStopped && f.Engine.Refusal == "deadline" &&
                f.Adapter.Writes == 0 && !f.Journal.HasUnresolvedOperation && f.Engine.UncertaintyMarkerReleased, "deadline before first command");
        }
    }

    private static void WithdrawalFailure()
    {
        using var f = new Fixture();
        f.Journal.ReleaseWorks = false;
        f.Journal.OnMarkUncertain = () => f.Adapter.CurrentSelection = null;
        Run(f.Engine);
        Require(f.Engine.State == HostUpdateState.NoWriteStopped && f.Adapter.Writes == 0 && f.Engine.UncertaintyMarkerRetained &&
            !f.Engine.UncertaintyMarkerReleased && f.Journal.HasUnresolvedOperation, "failed withdrawal keeps durable marker");
        Require(f.NewEngine().State == HostUpdateState.RecoveryReview, "retained marker still forces recovery review (fail closed)");
    }

    private static void WithdrawalThrows()
    {
        // The withdrawal runs inside the engine's finally block; an escaping exception there would
        // skip lease disposal (ownership leaked) and misreport the stop as an uncertain outcome.
        using var f = new Fixture();
        f.Journal.ReleaseThrows = true;
        f.Journal.OnMarkUncertain = () => f.Adapter.CurrentSelection = null;
        f.Engine.RunAsync(f.Engine.Confirm()).GetAwaiter().GetResult();
        Require(f.Engine.State == HostUpdateState.NoWriteStopped && f.Adapter.Writes == 0 && f.Adapter.Reads == 0 &&
            f.Adapter.Boots == 0, "throwing withdrawal still reports a no-write stop");
        Require(f.Engine.UncertaintyMarkerRetained && !f.Engine.UncertaintyMarkerReleased && f.Journal.HasUnresolvedOperation,
            "throwing withdrawal keeps the durable marker");
        Require(!f.Lease.Held, "ownership is released even when withdrawal throws");
        Require(f.NewEngine().State == HostUpdateState.RecoveryReview, "retained marker still forces recovery review");
    }

    private static void RecoveryKeepsMarker()
    {
        using var f = new Fixture();
        f.Journal.MarkUncertain(f.Manifest.ImageDigest);
        var recovery = f.NewEngine(recovery: true);
        Require(recovery.Select(f.Selection), "recovery selected");
        f.Journal.OnMarkUncertain = () => f.Adapter.CurrentSelection = null;
        Run(recovery);
        Require(recovery.State == HostUpdateState.RecoveryReview && f.Adapter.Writes == 0 && f.Journal.HasUnresolvedOperation &&
            f.Journal.RecoveryDigest == f.Manifest.ImageDigest && f.Journal.Releases == 0 &&
            !recovery.UncertaintyMarkerReleased && !recovery.UncertaintyMarkerRetained, "earlier uncertainty is never cleared by recovery");
    }

    private static void PossibleSubmissionKeepsMarker()
    {
        foreach (int scenario in new[] { 0, 1, 2 })
        {
            using var f = new Fixture();
            if (scenario == 0) f.Adapter.AdmissionHook = () => f.Adapter.CurrentSelection = null; // refused at command admission
            if (scenario == 1) f.Adapter.Write = () => Task.FromException(new IOException("private device detail"));
            if (scenario == 2) f.Adapter.Write = () => { f.Adapter.CurrentSelection = null; return Task.CompletedTask; };
            Run(f.Engine);
            Require(f.Engine.State == HostUpdateState.OutcomeUnknown && f.Journal.HasUnresolvedOperation && f.Journal.Releases == 0 &&
                !f.Engine.UncertaintyMarkerReleased && !f.Engine.UncertaintyMarkerRetained, $"possible submission {scenario} stays uncertain");
        }
    }

    private static void Revalidation()
    {
        using (var f = new Fixture())
        {
            var token = f.Engine.Confirm();
            Require(f.Engine.Revalidate() && f.Engine.State == HostUpdateState.Ready, "current binding stays ready");
            f.Adapter.CurrentSelection = null;
            Require(!f.Engine.Revalidate() && f.Engine.State == HostUpdateState.NoWriteStopped &&
                f.Engine.BindingFault == HostBindingFault.DeviceDisconnected && !f.Engine.AttemptStarted, "disconnect detected while ready");
            f.Adapter.CurrentSelection = f.Selection;
            f.Engine.RunAsync(token).GetAwaiter().GetResult();
            Require(f.Adapter.Writes == 0 && !f.Journal.HasUnresolvedOperation, "confirmation issued before the disconnect is revoked");
        }
        using (var f = new Fixture())
        {
            f.Adapter.CurrentSelection = f.Selection with { Identity = new object(), Generation = 2 };
            Require(!f.Engine.Revalidate() && f.Engine.BindingFault == HostBindingFault.DeviceChanged, "reconnect is a different unit");
        }
        using (var f = new Fixture())
        {
            f.CurrentPolicy = f.Policy with { Revoked = true };
            Require(!f.Engine.Revalidate() && f.Engine.BindingFault == HostBindingFault.AuthorityChanged, "authority change detected");
        }
        using (var f = new Fixture())
        {
            var token = f.Engine.Confirm();
            Require(f.Engine.Revalidate(), "intact binding");
            f.Engine.RunAsync(token).GetAwaiter().GetResult();
            Require(f.Engine.State == HostUpdateState.Verified, "passive revalidation keeps a valid confirmation");
        }
    }

    private static void RevalidationBoundaries()
    {
        using (var f = new Fixture())
        {
            var pending = new TaskCompletionSource(); f.Adapter.Write = () => pending.Task;
            var run = f.Engine.RunAsync(f.Engine.Confirm());
            f.Adapter.CurrentSelection = null;
            Require(!f.Engine.Revalidate() && f.Engine.State == HostUpdateState.Writing, "no side effect while an attempt runs");
            f.Adapter.CurrentSelection = f.Selection; pending.SetResult(); run.GetAwaiter().GetResult();
            Require(f.Engine.State == HostUpdateState.Verified, "running attempt keeps its own binding checks");
        }
        using (var f = new Fixture(select: false))
        {
            var adapter = new ObservingAdapter(f.Adapter);
            var engine = new HostUpdateEngine(f.Controller, f.Context, f.Inspection, () => f.CurrentInspection, f.Admit(), f.Policy,
                () => f.CurrentPolicy, adapter, f.Lease, f.Journal, () => f.Now, f.Profile);
            Require(engine.Select(f.Selection), "selected through observing adapter");
            adapter.Fail = true;
            Require(!engine.Revalidate() && engine.State == HostUpdateState.NoWriteStopped &&
                engine.BindingFault == HostBindingFault.CheckFailed && f.Adapter.Writes == 0, "failed observation fails closed");
        }
    }

    private static LoaderUpdateWorkflow Workflow(Fixture f) =>
        new(new[] { f.Selection }, (recovery, _) => f.NewEngine(recovery: recovery));

    private static void SelectAndConfirm(LoaderUpdateWorkflow m)
    {
        m.SelectedChoice = m.Choices[0]; m.Confirmed = true;
        Require(m.CanStart && m.Heading == "Preflight checks passed", "selected and confirmed: " + m.Heading);
    }

    private static void Reselect(LoaderUpdateWorkflow m)
    {
        var choice = m.Choices[0]; m.SelectedChoice = null; m.SelectedChoice = choice;
    }

    private static void WorkflowDisconnect()
    {
        using var f = new Fixture(select: false);
        var m = Workflow(f); SelectAndConfirm(m);
        m.Poll();
        Require(m.Confirmed && m.CanStart, "polling an intact binding keeps the confirmation");
        f.Adapter.CurrentSelection = null; m.Poll();
        Require(m.Heading == "Device disconnected" && !m.Confirmed && !m.CanStart && !m.CanConfirm && m.CanChoose &&
            m.Detail.Contains("no longer connected", StringComparison.Ordinal) &&
            m.Detail.Contains("No update command was sent", StringComparison.Ordinal), "disconnect shown immediately: " + m.Detail);
        Reselect(m);
        Require(m.Heading == "Device disconnected" && !m.CanStart && !m.CanConfirm, "reselection while absent stays blocked");
        f.Adapter.CurrentSelection = f.Selection with { Identity = new object(), Generation = 2 };
        Reselect(m);
        Require(m.Heading == "Device changed — select it again" && !m.CanStart, "stale choice cannot bind a reconnected unit");
        f.Adapter.CurrentSelection = f.Selection;
        Reselect(m); m.Confirmed = true;
        m.StartAsync().GetAwaiter().GetResult();
        Require(m.Heading == "Update verified" && f.Adapter.Writes == 1, "exact reconnected selection proceeds");
    }

    private static void WorkflowNoWrite()
    {
        using var f = new Fixture(select: false);
        var m = Workflow(f); SelectAndConfirm(m);
        f.Journal.OnMarkUncertain = () => { f.Adapter.CurrentSelection = null; f.Journal.OnMarkUncertain = null; };
        m.StartAsync().GetAwaiter().GetResult();
        Require(m.Heading == "Stopped before writing" && m.Detail.Contains("no longer connected", StringComparison.Ordinal) &&
            m.Detail.Contains("No update command was sent", StringComparison.Ordinal) && !m.CanReviewRecovery && !m.CanStart,
            "accurate no-write status: " + m.Detail);
        f.Adapter.CurrentSelection = f.Selection;
        Reselect(m);
        Require(m.Heading == "Preflight checks passed" && !m.CanReviewRecovery, "no recovery review after a no-write stop");
    }

    private static void WorkflowUnknownCauses()
    {
        var cases = new (string Phrase, Action<Fixture> Arrange)[]
        {
            ("connection reported a failure", f => f.Adapter.Write = () => Task.FromException(new IOException("private device detail COM99"))),
            ("time limit", f => f.Adapter.Write = () => { f.Now += f.Profile.WriteMilliseconds; return Task.CompletedTask; }),
            ("Readback did not match", f => f.Adapter.Read = () => Task.FromResult(HostRegionCheck.Of(new byte[] { 9 }, true))),
            ("Startup did not report", f => f.Adapter.Boot = () => Task.FromResult(new HostBootResult("heartbeat", true, true))),
            ("could not record completion", f => f.Journal.CompleteWorks = false),
            ("no longer connected", f => f.Adapter.Write = () => { f.Adapter.CurrentSelection = null; return Task.CompletedTask; }),
        };
        foreach (var (phrase, arrange) in cases)
        {
            using var f = new Fixture(select: false);
            var m = Workflow(f); SelectAndConfirm(m); arrange(f);
            m.StartAsync().GetAwaiter().GetResult();
            Require(m.Heading.StartsWith("Outcome unknown", StringComparison.Ordinal) && m.CanReviewRecovery && !m.CanStart &&
                m.Detail.Contains(phrase, StringComparison.Ordinal) && m.Detail.Contains("Do not retry", StringComparison.Ordinal),
                $"cause '{phrase}' shown: {m.Detail}");
            Require(!m.Detail.Contains("private", StringComparison.Ordinal) && !m.Detail.Contains("COM99", StringComparison.Ordinal),
                "adapter exception text never projected");
            Require(f.Journal.HasUnresolvedOperation, "possible-write failure keeps durable uncertainty");
        }
    }

    private static void WorkflowRefusalCauses()
    {
        using (var f = new Fixture(select: false))
        {
            var m = Workflow(f); SelectAndConfirm(m);
            using var held = f.Lease.TryAcquire();
            m.StartAsync().GetAwaiter().GetResult();
            Require(m.Heading == "Stopped before writing" && m.Detail.Contains("Another loader operation", StringComparison.Ordinal) &&
                f.Adapter.Writes == 0, "exclusive owner named: " + m.Detail);
        }
        using (var f = new Fixture(select: false))
        {
            var m = Workflow(f); SelectAndConfirm(m); f.Journal.StartWorks = false;
            m.StartAsync().GetAwaiter().GetResult();
            Require(m.Heading == "Stopped before writing" && m.Detail.Contains("safety journal could not record", StringComparison.Ordinal),
                "journal start failure named: " + m.Detail);
        }
        using (var f = new Fixture(select: false))
        {
            var m = Workflow(f); SelectAndConfirm(m);
            f.Journal.ReleaseWorks = false; f.Journal.OnMarkUncertain = () => f.Adapter.CurrentSelection = null;
            m.StartAsync().GetAwaiter().GetResult();
            Require(m.Heading == "Stopped before writing" && m.Detail.Contains("may require recovery review", StringComparison.Ordinal) &&
                !m.Detail.Contains("will be required", StringComparison.Ordinal),
                "retained marker disclosed: " + m.Detail);
        }
    }

    private static void ProductionPollInert()
    {
        // The shipped panel builds the factory-less workflow and its timer calls Poll about every
        // 100 ms. That path must never create an engine, offer a device, enable an action or throw.
        var m = new LoaderUpdateWorkflow();
        string heading = m.Heading, detail = m.Detail;
        for (int i = 0; i < 1000; i++) m.Poll();
        Require(!m.IsFixture && m.Choices.Count == 0 && m.SelectedChoice is null && !m.CanChoose && !m.CanConfirm &&
            !m.CanStart && !m.CanCancel && !m.CanReviewRecovery && !m.IsBusy && !m.Confirmed,
            "production polling grants no device, confirmation or operation");
        Require(m.Heading == heading && m.Heading == "Installation unavailable" && m.Detail == detail &&
            m.Firmware == "Firmware: no admitted file" && m.SessionLabel == "PREVIEW — inspection only",
            "production polling leaves the inspection-only status unchanged");
        m.Confirmed = true; m.Poll();
        Require(!m.Confirmed && !m.CanStart, "a confirmation cannot be forced on the production workflow");
    }

    private sealed class ObservingAdapter(HostUpdateEngineTests.FakeAdapter inner) : IHostUpdateAdapter
    {
        internal bool Fail;
        public HostSelection? CurrentSelection => Fail ? throw new IOException("private enumeration detail") : inner.CurrentSelection;
        public Task WriteAsync(HostSelection expected, uint offset, byte[] image, int maximumChunk, CancellationToken cancellation) =>
            inner.WriteAsync(expected, offset, image, maximumChunk, cancellation);
        public Task<HostRegionCheck> ReadbackAsync(HostSelection expected, uint offset, int length, CancellationToken cancellation) =>
            inner.ReadbackAsync(expected, offset, length, cancellation);
        public Task<HostBootResult> BootAsync(HostSelection expected, CancellationToken cancellation) =>
            inner.BootAsync(expected, cancellation);
    }
}
