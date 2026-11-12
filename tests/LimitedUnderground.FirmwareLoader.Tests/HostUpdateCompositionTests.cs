using LimitedUnderground.FirmwareLoader;

internal static class HostUpdateCompositionTests
{
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("composition construction checks consistency without selecting confirming or owning", Construction),
        ("composition drives actual workflow through explicit confirmation and durable completion", WorkflowSuccess),
        ("composition refuses missing release binding policy reader store or clock", MissingInputs),
        ("composition refuses mismatched payload offset and enlarged geometry", BindingMismatch),
        ("composition refuses changed initial policy and stale inspection context", InitialAuthority),
        ("composition rechecks changed policy and expiry after successful construction", ReadyAuthorityChanges),
        ("composition refuses unavailable policy and clock without commands", UnavailableChecks),
        ("composition invalidates actual workflow confirmation on session or selection changes", WorkflowInvalidation),
        ("composition withdraws only its own unsubmitted durable marker on changed authority", ChangeAfterMarker),
        ("composition retains durable uncertainty when authority changes after submission", ChangeAfterWrite),
        ("composition retains real store ownership until cancelled command quiesces", CancelQuiescence),
        ("composition requires a separately reviewed recovery attempt and fresh confirmation", WorkflowRecovery),
        ("composition rejects mismatched original recovery without clearing earlier uncertainty", WrongRecovery),
        ("composition retains immutable admitted bytes despite caller-copy mutation", ImmutableBytes),
    };

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void NoCommands(Fixture fixture)
    {
        Require(fixture.Source.Adapter.Writes == 0 && fixture.Source.Adapter.Reads == 0 &&
            fixture.Source.Adapter.Boots == 0, "refused composition submits no fixture command");
    }

    private static void Run(HostUpdateEngine engine)
    {
        engine.RunAsync(engine.Confirm()).GetAwaiter().GetResult();
    }

    private static void Construction()
    {
        using var f = new Fixture();
        var engine = f.Create();
        Require(engine.State == HostUpdateState.DeviceUnselected && engine.Confirm() is null,
            "consistent inputs do not select or confirm");
        using var independentLease = new HostOperationStore(f.Root).TryAcquire();
        Require(independentLease is not null && File.ReadAllBytes(f.Journal).AsSpan().SequenceEqual(f.InitialJournal),
            "construction neither acquires nor writes the store");
        NoCommands(f);
    }

    private static void WorkflowSuccess()
    {
        using var f = new Fixture();
        var workflow = f.Workflow();
        Require(workflow.SelectedChoice is null && !workflow.CanStart, "no automatic workflow selection");
        workflow.SelectedChoice = workflow.Choices[0];
        Require(workflow.CanConfirm && !workflow.CanStart, "selection requires separate confirmation");
        workflow.StartAsync().GetAwaiter().GetResult();
        NoCommands(f);
        workflow.Confirmed = true;
        workflow.StartAsync().GetAwaiter().GetResult();
        Require(workflow.Heading == "Update verified" && f.Source.Adapter.Writes == 1 &&
            f.Source.Adapter.Reads == 1 && f.Source.Adapter.Boots == 1 && !f.Store.HasUnresolvedOperation,
            "actual workflow and concrete store complete the single fixture sequence");
        workflow.StartAsync().GetAwaiter().GetResult();
        Require(f.Source.Adapter.Writes == 1, "completed confirmation cannot resubmit");
        Require(new LoaderUpdateWorkflow().Choices.Count == 0 && LoaderProductionProviders.SignerTrustPolicies.Count == 0 &&
            !f.Source.Controller.Snapshot.FirmwareWritingAvailable, "production remains inspection-only");
    }

    private static void MissingInputs()
    {
        foreach (string missing in new[] { "release", "binding", "policy-reader", "store", "clock", "inspection-reader" })
        {
            using var f = new Fixture();
            switch (missing)
            {
                case "release": f.Release = null; break;
                case "binding": f.Binding = null; break;
                case "policy-reader": f.PolicyObservation = null; break;
                case "store": f.StoreInput = null; break;
                case "clock": f.Clock = null; break;
                case "inspection-reader": f.InspectionObservation = null; break;
            }
            Require(!f.TryCreate(out var engine) && engine is null, "missing explicit composition input refuses");
            NoCommands(f);
        }
    }

    private static void BindingMismatch()
    {
        foreach (int mismatch in new[] { 0, 1, 2 })
        {
            using var f = new Fixture();
            var bytes = (byte[])f.Source.Image.Clone();
            if (mismatch == 0) bytes[0] ^= 0xff;
            f.Binding = Bind(bytes, offset: mismatch == 1 ? 8192u : 4096u, enlarged: mismatch == 2);
            Require(!f.TryCreate(out var engine) && engine is null, "payload or plan mismatch refuses construction");
            Require(File.ReadAllBytes(f.Journal).AsSpan().SequenceEqual(f.InitialJournal), "refusal does not write intent");
            NoCommands(f);
        }
    }

    private static void InitialAuthority()
    {
        using var f = new Fixture();
        f.Source.CurrentPolicy = f.Source.Policy with { Revision = 2 };
        Require(!f.TryCreate(out _), "changed initial policy refuses");
        f.Source.CurrentPolicy = f.Source.Policy;
        f.Source.CurrentInspection = null;
        Require(!f.TryCreate(out _), "stale inspected file refuses");
        f.Source.CurrentInspection = f.Source.Inspection;
        f.Source.Controller.ReturnToProductChoice();
        Require(!f.TryCreate(out _), "retired provider context refuses");
        NoCommands(f);
    }

    private static void ReadyAuthorityChanges()
    {
        foreach (bool expire in new[] { false, true })
        {
            using var f = new Fixture();
            var engine = f.Create();
            Require(engine.Select(f.Source.Selection), "explicit selected composition");
            var confirmation = engine.Confirm();
            if (expire) f.Source.Now = f.Source.Policy.ValidUntil;
            else f.Source.CurrentPolicy = f.Source.Policy with { Revoked = true };
            Require(!engine.Revalidate(), "prior consistency never caches current authority");
            engine.RunAsync(confirmation).GetAwaiter().GetResult();
            NoCommands(f);
            Require(File.ReadAllBytes(f.Journal).AsSpan().SequenceEqual(f.InitialJournal), "stale authority leaves store unchanged");
        }
    }

    private static void UnavailableChecks()
    {
        foreach (bool failClock in new[] { false, true })
        {
            using var f = new Fixture();
            if (failClock) f.Clock = () => throw new IOException("private clock unavailable");
            else f.PolicyObservation = () => throw new IOException("private policy unavailable");
            Require(!f.TryCreate(out _), "unavailable initial check fails closed");
            f.Clock = () => f.Source.Now;
            f.PolicyObservation = () => f.Source.CurrentPolicy;
            var engine = f.Create();
            Require(engine.Select(f.Source.Selection), "selection before live failure");
            f.Source.CurrentPolicy = null;
            Require(!engine.Revalidate(), "unavailable live authority revokes selection");
            Run(engine);
            NoCommands(f);
        }
    }

    private static void WorkflowInvalidation()
    {
        foreach (bool changeSession in new[] { false, true })
        {
            using var f = new Fixture();
            var workflow = f.Workflow();
            workflow.SelectedChoice = workflow.Choices[0];
            workflow.Confirmed = true;
            if (changeSession) f.Source.Controller.ReturnToProductChoice();
            else f.Source.Adapter.CurrentSelection = f.Source.Selection with { Generation = 2 };
            workflow.Poll();
            Require(!workflow.Confirmed && !workflow.CanStart, "actual workflow clears stale confirmation");
            workflow.StartAsync().GetAwaiter().GetResult();
            NoCommands(f);
        }
    }

    private static void ChangeAfterMarker()
    {
        using var f = new Fixture();
        f.PolicyObservation = () => File.ReadAllText(f.Journal).Contains("\"state\":\"unresolved\"", StringComparison.Ordinal)
            ? f.Source.Policy with { Revision = 2 } : f.Source.Policy;
        var engine = f.Create();
        Require(engine.Select(f.Source.Selection), "explicit selected attempt before marker race");
        Run(engine);
        Require(engine.State == HostUpdateState.NoWriteStopped && engine.UncertaintyMarkerReleased &&
            File.ReadAllBytes(f.Journal).AsSpan().SequenceEqual(f.InitialJournal),
            "live guard after durable marker withdraws this unsubmitted attempt exactly");
        NoCommands(f);
    }

    private static void ChangeAfterWrite()
    {
        using var f = new Fixture();
        f.Source.Adapter.Write = () =>
        {
            f.Source.CurrentPolicy = f.Source.Policy with { Revision = 2 };
            return Task.CompletedTask;
        };
        var engine = f.Create();
        Require(engine.Select(f.Source.Selection), "selected attempt before live write-time change");
        Run(engine);
        Require(engine.State == HostUpdateState.OutcomeUnknown && f.Source.Adapter.Writes == 1 &&
            f.Source.Adapter.Reads == 0 && f.Source.Adapter.Boots == 0 && f.Store.HasUnresolvedOperation &&
            !engine.UncertaintyMarkerReleased, "post-submission change retains uncertainty and stops later commands");
        var reopened = new HostOperationStore(f.Root);
        Require(reopened.RecoveryDigest == f.Source.Manifest.RecoveryDigest, "restart sees the exact recovery binding");
    }

    private static void CancelQuiescence()
    {
        using var f = new Fixture();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Source.Adapter.Write = () => pending.Task;
        var engine = f.Create();
        Require(engine.Select(f.Source.Selection), "selected pending fixture command");
        var run = engine.RunAsync(engine.Confirm());
        try
        {
            Require(f.Source.Adapter.Writes == 1 && !pending.Task.IsCompleted, "command submitted and still pending");
            engine.Cancel();
            run.GetAwaiter().GetResult();
            using var blocked = new HostOperationStore(f.Root).TryAcquire();
            Require(blocked is null && engine.State == HostUpdateState.OutcomeUnknown,
                "cancel cannot release concrete ownership before command quiescence");
        }
        finally
        {
            pending.TrySetResult();
            run.GetAwaiter().GetResult();
        }
        Require(SpinWait.SpinUntil(f.CanAcquire, 3000), "concrete ownership releases after actual quiescence");
        Require(f.Source.Adapter.Reads == 0 && f.Source.Adapter.Boots == 0 && f.Store.HasUnresolvedOperation,
            "late result cannot advance the composed attempt");
    }

    private static void WorkflowRecovery()
    {
        using var f = new Fixture();
        using (var lease = f.Store.TryAcquire())
            Require(lease is not null && f.Store.MarkUncertain(f.Release!.Manifest.ImageDigest), "separate original recovery binding");
        var workflow = f.Workflow();
        workflow.SelectedChoice = workflow.Choices[0];
        Require(workflow.CanReviewRecovery && !workflow.CanStart, "unresolved store requires recovery review");
        workflow.ReviewRecovery();
        Require(workflow.CanConfirm && !workflow.CanStart, "reviewed original needs fresh confirmation");
        workflow.StartAsync().GetAwaiter().GetResult();
        NoCommands(f);
        workflow.Confirmed = true;
        workflow.StartAsync().GetAwaiter().GetResult();
        Require(workflow.Heading == "Original restored" && !f.Store.HasUnresolvedOperation &&
            f.Source.Adapter.Writes == 1 && f.Source.Adapter.Reads == 1 && f.Source.Adapter.Boots == 1,
            "separately confirmed fixture original completes through the same durable store");
    }

    private static void WrongRecovery()
    {
        using var f = new Fixture();
        using (var lease = f.Store.TryAcquire())
            Require(lease is not null && f.Store.MarkUncertain(new string('B', 64)), "different original recorded");
        byte[] intent = File.ReadAllBytes(f.Journal);
        var engine = f.Create(recovery: true);
        Require(!engine.Select(f.Source.Selection), "unrelated original cannot gain recovery authority");
        Run(engine);
        Require(File.ReadAllBytes(f.Journal).AsSpan().SequenceEqual(intent) && f.Store.HasUnresolvedOperation,
            "failed recovery does not clear earlier intent");
        NoCommands(f);
    }

    private static void ImmutableBytes()
    {
        using var f = new Fixture();
        var engine = f.Create();
        byte[] admitted = f.Release!.CopyImage();
        f.Source.Adapter.Read = () => Task.FromResult(HostRegionCheck.Of((byte[])admitted.Clone(), true));
        f.Source.Image[0] ^= 0xff;
        f.Binding!.CopyBytesAt(0)[0] ^= 0xff;
        Require(engine.Select(f.Source.Selection), "caller copy changes cannot alter retained binding");
        Run(engine);
        Require(engine.State == HostUpdateState.Verified && f.Binding!.CopyBytesAt(0).AsSpan().SequenceEqual(admitted),
            "composed attempt retains admitted and preservation bytes");
    }

    private static HostPreservationPayloadBinding Bind(byte[] bytes, uint offset = 4096, bool enlarged = false)
    {
        var allowed = new[] { new HostPreservationRange(enlarged ? 0ul : 4096ul, enlarged ? 12288ul : 8192ul) };
        var protectedRanges = enlarged
            ? new[] { new HostPreservationRange(12288, 8192) }
            : new[] { new HostPreservationRange(0, 4096), new HostPreservationRange(12288, 8192) };
        Require(HostPreservationPlan.TryCreate(20480, 4096, allowed, protectedRanges,
            new[] { new HostPreservationRange(offset, (ulong)bytes.Length) }, out var plan), "complete fixture plan");
        Require(HostPreservationPayloadBinding.TryCreate(plan,
            new[] { new HostPreservationPayload(offset, bytes) }, out var binding), "immutable fixture payload binding");
        return binding!;
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly HostUpdateEngineTests.Fixture Source = new(select: false);
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "LUF.HostStore.Tests." + Guid.NewGuid().ToString("N"));
        internal readonly HostOperationStore Store;
        internal readonly byte[] InitialJournal;
        internal string Journal => Path.Combine(Root, HostOperationStore.JournalName);
        internal HostAdmittedRelease? Release;
        internal HostPreservationPayloadBinding? Binding;
        internal HostOperationStore? StoreInput;
        internal Func<HostAdmissionPolicy?>? PolicyObservation;
        internal Func<FirmwareBundleCandidateResult?>? InspectionObservation;
        internal Func<long>? Clock;

        internal Fixture()
        {
            Store = HostOperationStore.CreateIsolatedTestStore(Root);
            StoreInput = Store;
            InitialJournal = File.ReadAllBytes(Journal);
            Release = Source.Admit();
            Binding = Bind(Source.Image);
            PolicyObservation = () => Source.CurrentPolicy;
            InspectionObservation = () => Source.CurrentInspection;
            Clock = () => Source.Now;
        }

        internal bool TryCreate(out HostUpdateEngine? engine, bool recovery = false) =>
            HostUpdateComposition.TryCreate(Source.Controller, Source.Context, Source.Inspection, InspectionObservation,
                Release, Binding, Source.Policy, PolicyObservation, Source.Adapter, StoreInput, Clock, Source.Profile,
                out engine, recovery);

        internal HostUpdateEngine Create(bool recovery = false)
        {
            Require(TryCreate(out var engine, recovery) && engine is not null, "valid composition creation");
            return engine!;
        }

        internal LoaderUpdateWorkflow Workflow() => new(new[] { Source.Selection }, (recovery, _) => Create(recovery));

        internal bool CanAcquire()
        {
            using var lease = new HostOperationStore(Root).TryAcquire();
            return lease is not null;
        }

        public void Dispose()
        {
            Source.Dispose();
            string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            string leaf = Path.GetFileName(Root);
            Require(Path.GetFullPath(Root) == Root && Path.GetDirectoryName(Root) == parent &&
                leaf.StartsWith("LUF.HostStore.Tests.", StringComparison.Ordinal) &&
                Guid.TryParseExact(leaf["LUF.HostStore.Tests.".Length..], "N", out _) &&
                (File.GetAttributes(Root) & FileAttributes.ReparsePoint) == 0, "owned fixture cleanup boundary");
            Directory.Delete(Root, recursive: true);
        }
    }
}