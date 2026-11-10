using LimitedUnderground.FirmwareLoader;

internal static class D5FiniteWriterContractTests
{
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("T-0068 fully assigned synthetic D5 contract retains immutable span evidence", Snapshot),
        ("T-0068 unassigned interface profile driver reset erase timing facts refuse", Unassigned),
        ("T-0068 single-attempt finite command and stage budgets are required", Finite),
        ("T-0068 write rejects outside payload span including erase tails gaps and overflow", Writes),
        ("T-0068 fresh exact opaque selection binding is required", Identity),
        ("T-0068 missing original readback boot or recovery binding refuses", Bindings),
        ("T-0068 whole address comparison must cover every gap and tail", Coverage),
        ("T-0068 erase-side-effect and preserved-tail restoration declarations are required", Effects),
        ("T-0068 completion refuses missing wrong or partial comparison evidence", Comparisons),
        ("T-0068 original recovery comparisons and boot identity remain separate", RecoveryBindings),
        ("T-0068 terminal success requires actual verified state and quiescence", Completion),
        ("T-0068 synthetic contract uses actual bounded host engine and durable store", Engine),
        ("T-0068 refused contract never constructs or submits an engine attempt", RefusedEngine),
        ("T-0068 pending cancellation retains lease and never becomes completion proof", Quiescence),
    };
    private static void Require(bool ok){if(!ok)throw new InvalidOperationException("T-0068 D5 host-contract assertion");}
    private static void Refuse(D5ContractDraft draft)=>Require(!D5FiniteWriterContract.TryCreateHostFixture(draft,out _));
    private static void Snapshot()
    {
        using var f=new Fixture();var c=f.Create();f.Evidence[0]=f.Evidence[0] with{OriginalSha256=new string('E',64)};
        Require(c.Spans[0].OriginalSha256==new string('B',64));
        Require(c.AllowsFixtureWrite(f.Source.Selection,4096,4,1));
    }
    private static void Unassigned()
    {
        using var f=new Fixture();var d=f.Draft;
        foreach(var bad in new[]{d with{ProfileId=null},d with{InterfaceId=null},d with{DriverId=null},d with{Selection=null},
            d with{Binding=null},d with{ResetEffect=null},d with{EraseEffect=null},d with{EraseBlock=null},d with{RestoreEraseTails=null},
            d with{ObservableQuiescence=null},d with{MaximumAttempts=null},d with{Limits=null},d with{Spans=null},
            d with{ProfileId="UNASSIGNED"},d with{DriverId="UNKNOWN"},d with{InterfaceId=" UNASSIGNED "},d with{DriverId="bad\nidentity"}})Refuse(bad);
        Require(!D5FiniteWriterContract.TryCreateHostFixture(null,out _));
    }
    private static void Finite()
    {
        using var f=new Fixture();var d=f.Draft;var l=d.Limits!;
        foreach(var limits in new[]{l with{WriteMilliseconds=0},l with{ReadMilliseconds=0},l with{BootMilliseconds=0},
            l with{TotalMilliseconds=0},l with{TotalMilliseconds=1},l with{MaximumChunk=0},l with{MaximumChunk=65537},l with{MaximumChunk=int.MaxValue},
            l with{SideEffectsApproved=false},l with{BootApproved=false},l with{WriteMilliseconds=int.MaxValue,ReadMilliseconds=int.MaxValue,BootMilliseconds=int.MaxValue}})
            Refuse(d with{Limits=limits});
        Refuse(d with{MaximumAttempts=0});Refuse(d with{MaximumAttempts=2});Refuse(d with{ObservableQuiescence=false});
    }
    private static void Writes()
    {
        using var f=new Fixture();var c=f.Create();
        foreach(var span in new[]{new HostPreservationRange(0,1),new(4095,2),new(4100,1),new(8192,4),new(12288,4),new(4096,0),new(ulong.MaxValue,4),new(4096,ulong.MaxValue)})
            Require(!c.AllowsFixtureWrite(f.Source.Selection,span.Offset,span.Length,1));
        Require(!c.AllowsFixtureWrite(f.Source.Selection,4096,4,0)&&!c.AllowsFixtureWrite(f.Source.Selection,4096,4,2));
        Require(c.AllowsFixtureWrite(f.Source.Selection,4097,2,1));
        var small=f.Draft with{Limits=f.Source.Profile with{MaximumChunk=1}};Require(D5FiniteWriterContract.TryCreateHostFixture(small,out var bounded));
        Require(!bounded!.AllowsFixtureWrite(f.Source.Selection,4096,2,1));
    }
    private static void Identity()
    {
        using var f=new Fixture();var c=f.Create();var s=f.Source.Selection;
        foreach(var changed in new HostSelection?[]{null,s with{Identity=new object()},s with{Generation=2},s with{Target="other"},s with{Model="other"},s with{Adapter="other"}})
            Require(!c.AllowsFixtureWrite(changed,4096,4,1));
    }
    private static void Bindings()
    {
        using var f=new Fixture();var d=f.Draft;
        Refuse(d with{OriginalSetDigest=null});Refuse(d with{OriginalBootImageDigest=null});Refuse(d with{WrittenBootImageDigest=null});
        Refuse(d with{WrittenBootImageDigest=new string('E',64)});
        for(int i=0;i<f.Evidence.Length;i++)
        {
            var copy=f.Evidence.ToArray();copy[i]=copy[i] with{OriginalSha256=""};Refuse(d with{Spans=copy});
            copy=f.Evidence.ToArray();copy[i]=copy[i] with{ExpectedReadbackSha256=""};Refuse(d with{Spans=copy});
        }
    }
    private static void Coverage()
    {
        using var f=new Fixture();var d=f.Draft;
        Refuse(d with{Spans=f.Evidence[..2]});
        var gap=f.Evidence.ToArray();gap[2]=gap[2] with{Span=new(4101,16379)};Refuse(d with{Spans=gap});
        var overlap=f.Evidence.ToArray();overlap[2]=overlap[2] with{Span=new(4099,16381)};Refuse(d with{Spans=overlap});
        var overflow=f.Evidence.ToArray();overflow[2]=overflow[2] with{Span=new(4100,ulong.MaxValue)};Refuse(d with{Spans=overflow});
        var extra=f.Evidence.ToArray();extra[0]=extra[0] with{Role=D5SpanRole.Written};Refuse(d with{Spans=extra});
        var changed=f.Evidence.ToArray();changed[2]=changed[2] with{ExpectedReadbackSha256=new string('E',64)};Refuse(d with{Spans=changed});
    }
    private static void Effects()
    {
        using var f=new Fixture();var d=f.Draft;
        Refuse(d with{EraseBlock=8});Refuse(d with{ResetEffect=(D5ResetEffect)99});Refuse(d with{EraseEffect=(D5EraseEffect)99});
        Refuse(d with{RestoreEraseTails=false});
    }
    private static void Comparisons()
    {
        using var f=new Fixture();var c=f.Create();var actual=f.Compared();var boot=f.Boot();
        Require(c.CompletionConsistent(HostUpdateState.Verified,true,f.Draft.OriginalSetDigest,actual,boot));
        Require(!c.CompletionConsistent(HostUpdateState.Verified,true,f.Draft.OriginalSetDigest,null,boot));
        Require(!c.CompletionConsistent(HostUpdateState.Verified,true,f.Draft.OriginalSetDigest,actual[..2],boot));
        actual[2]=actual[2] with{Sha256=new string('E',64)};Require(!c.CompletionConsistent(HostUpdateState.Verified,true,f.Draft.OriginalSetDigest,actual,boot));
        Require(!c.CompletionConsistent(HostUpdateState.Verified,true,new string('E',64),f.Compared(),boot));
        Require(!c.CompletionConsistent(HostUpdateState.Verified,true,f.Draft.OriginalSetDigest,f.Compared(),boot with{Healthy=false}));
        Require(!c.CompletionConsistent(HostUpdateState.Verified,true,f.Draft.OriginalSetDigest,f.Compared(),boot with{ProtectedStateMatches=false}));
        Require(!c.CompletionConsistent(HostUpdateState.Verified,true,f.Draft.OriginalSetDigest,f.Compared(),null));
    }
    private static void RecoveryBindings()
    {
        using var f=new Fixture();var c=f.Create();
        var originals=f.Evidence.Select(e=>new D5ObservedSpan(e.Span,e.OriginalSha256)).ToArray();
        var boot=new HostBootResult(f.Draft.OriginalBootImageDigest!,true,true);
        Require(c.CompletionConsistent(HostUpdateState.Recovered,true,f.Draft.OriginalSetDigest,originals,boot,true));
        Require(!c.CompletionConsistent(HostUpdateState.Recovered,true,f.Draft.OriginalSetDigest,f.Compared(),boot,true));
        Require(!c.CompletionConsistent(HostUpdateState.Recovered,true,f.Draft.OriginalSetDigest,originals,f.Boot(),true));
    }
    private static void Completion()
    {
        using var f=new Fixture();var c=f.Create();
        Require(!c.CompletionConsistent(HostUpdateState.Verified,false,f.Draft.OriginalSetDigest,f.Compared(),f.Boot()));
        foreach(var state in new[]{HostUpdateState.OutcomeUnknown,HostUpdateState.Writing,HostUpdateState.BootWait,HostUpdateState.NoWriteStopped})
            Require(!c.CompletionConsistent(state,true,f.Draft.OriginalSetDigest,f.Compared(),f.Boot()));
    }
    private static void Engine()
    {
        using var f=new Fixture();var c=f.Create();var e=f.Engine(f.Draft);Require(e is not null&&e.Select(f.Source.Selection));
        e!.RunAsync(e.Confirm()).GetAwaiter().GetResult();
        Require(e.State==HostUpdateState.Verified&&f.Source.Adapter.Writes==1&&f.Source.Adapter.Reads==1&&f.Source.Adapter.Boots==1&&!f.Store.HasUnresolvedOperation);
        Require(c.CompletionConsistent(e.State,true,f.Draft.OriginalSetDigest,f.Compared(),f.Boot()));
        Require(LoaderProductionProviders.SignerTrustPolicies.Count==0&&new LoaderUpdateWorkflow().Choices.Count==0);
    }
    private static void RefusedEngine()
    {
        using var f=new Fixture();var bad=f.Draft with{OriginalSetDigest=null};Require(f.Engine(bad) is null);
        Require(f.Source.Adapter.Writes==0&&f.Source.Adapter.Reads==0&&f.Source.Adapter.Boots==0);
        Require(File.ReadAllBytes(f.Journal).AsSpan().SequenceEqual(f.InitialJournal));
    }
    private static void Quiescence()
    {
        using var f=new Fixture();var c=f.Create();var pending=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Source.Adapter.Write=()=>pending.Task;var e=f.Engine(f.Draft)!;Require(e.Select(f.Source.Selection));var run=e.RunAsync(e.Confirm());
        try
        {
            Require(f.Source.Adapter.Writes==1&&!pending.Task.IsCompleted);e.Cancel();run.GetAwaiter().GetResult();
            using var blocked=new HostOperationStore(f.Root).TryAcquire();Require(blocked is null);
            Require(!c.CompletionConsistent(e.State,pending.Task.IsCompleted,f.Draft.OriginalSetDigest,f.Compared(),f.Boot()));
        }
        finally{pending.TrySetResult();run.GetAwaiter().GetResult();}
        Require(SpinWait.SpinUntil(f.CanAcquire,3000));
        Require(e.State==HostUpdateState.OutcomeUnknown&&f.Store.HasUnresolvedOperation&&f.Source.Adapter.Reads==0&&f.Source.Adapter.Boots==0);
        Require(!c.CompletionConsistent(e.State,true,f.Draft.OriginalSetDigest,f.Compared(),f.Boot()));
    }
    private sealed class Fixture:IDisposable
    {
        internal readonly HostUpdateEngineTests.Fixture Source=new(select:false);
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"LUF.HostStore.Tests."+Guid.NewGuid().ToString("N"));
        internal readonly HostOperationStore Store;
        internal readonly byte[] InitialJournal;
        internal string Journal=>Path.Combine(Root,HostOperationStore.JournalName);
        internal readonly D5SpanEvidence[] Evidence;
        internal readonly D5ContractDraft Draft;
        internal Fixture()
        {
            Require(HostPreservationPlan.TryCreate(20480,4096,[new(4096,8192)],[new(0,4096),new(12288,8192)],[new(4096,4)],out var plan));
            Require(HostPreservationPayloadBinding.TryCreate(plan,[new(4096,Source.Image)],out var binding));
            Store=HostOperationStore.CreateIsolatedTestStore(Root);InitialJournal=File.ReadAllBytes(Journal);
            Evidence=[new(new(0,4096),D5SpanRole.Preserved,new string('B',64),new string('B',64)),
                new(new(4096,4),D5SpanRole.Written,new string('C',64),Source.Manifest.ImageDigest),
                new(new(4100,16380),D5SpanRole.Preserved,new string('D',64),new string('D',64))];
            Draft=new("fixture-profile","fixture-interface","fixture-driver",Source.Selection,binding,D5ResetEffect.NoAdditionalMutations,
                D5EraseEffect.OnlyDeclaredEraseRanges,4096,true,true,1,Source.Profile,Evidence,Source.Manifest.RecoveryDigest,Source.Manifest.RecoveryDigest,Source.Manifest.ImageDigest);
        }
        internal D5FiniteWriterContract Create(){Require(D5FiniteWriterContract.TryCreateHostFixture(Draft,out var c));return c!;}
        internal HostUpdateEngine? Engine(D5ContractDraft draft)=>D5FiniteWriterContract.TryCreateHostFixture(draft,out var c)
            ?new HostUpdateEngine(Source.Controller,Source.Context,Source.Inspection,()=>Source.CurrentInspection,Source.Admit(),Source.Policy,()=>Source.CurrentPolicy,
                Source.Adapter,Store,Store,()=>Source.Now,c!.Limits):null;
        internal D5ObservedSpan[] Compared()=>Evidence.Select(e=>new D5ObservedSpan(e.Span,e.ExpectedReadbackSha256)).ToArray();
        internal HostBootResult Boot()=>new(Source.Manifest.ImageDigest,true,true);
        internal bool CanAcquire(){using var lease=new HostOperationStore(Root).TryAcquire();return lease is not null;}
        public void Dispose()
        {
            Source.Dispose();string leaf=Path.GetFileName(Root);const string prefix="LUF.HostStore.Tests.";
            Require(Path.GetDirectoryName(Root)==Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)&&leaf.StartsWith(prefix,StringComparison.Ordinal)&&Guid.TryParseExact(leaf[prefix.Length..],"N",out _)&&(File.GetAttributes(Root)&FileAttributes.ReparsePoint)==0);
            Directory.Delete(Root,true);
        }
    }
}