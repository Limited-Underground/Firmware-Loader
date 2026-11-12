using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using LimitedUnderground.FirmwareLoader;

internal static class ProductionUpdateCompositionTests
{
    private static readonly RSA TestKey = RSA.Create(3072); // Ephemeral fixture only.
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("T-0056 genuine production result drives actual confirmed workflow", Workflow),
        ("T-0056 construction retains typed proof without selecting confirming or owning", Construction),
        ("T-0056 refuses missing input or replacement clock-state identity", Missing),
        ("T-0056 refuses wrong payload destination and pairing before intent", Payload),
        ("T-0056 invalid canonical signature cannot produce a paired admission result", Pairing),
        ("T-0056 rechecks all live compatibility and D5 reference values", ContextChanges),
        ("T-0056 refuses revoked changed expired or unavailable policy", PolicyChanges),
        ("T-0056 rechecks persisted clock and policy floors and unavailable state", Floors),
        ("T-0056 actual workflow invalidates confirmation on file session or selection change", Invalidation),
        ("T-0056 withdraws only own pre-command marker on changed production context", BeforeCommand),
        ("T-0056 post-submission context changes retain durable uncertainty", AfterCommand),
        ("T-0056 cancellation retains real ownership until command quiescence", Cancellation),
        ("T-0056 recovery needs distinct genuine admission and separate confirmation", Recovery),
        ("T-0056 unrelated recovery context cannot clear earlier uncertainty", WrongRecovery),
        ("T-0056 immutable caller copies and default production remain isolated", Copies),
    };
    private static void Require(bool ok) { if (!ok) throw new InvalidOperationException("T-0056 composed production boundary assertion"); }
    private static void None(Fixture f) => Require(f.Source.Adapter.Writes == 0 && f.Source.Adapter.Reads == 0 && f.Source.Adapter.Boots == 0);
    private static void Run(HostUpdateEngine e) => e.RunAsync(e.Confirm()).GetAwaiter().GetResult();
    private static void Construction()
    {
        using var f=new Fixture(); var e=f.Create(); Require(e.State==HostUpdateState.DeviceUnselected && e.Confirm() is null);
        using var lease=new HostOperationStore(f.OperationRoot).TryAcquire(); Require(lease is not null);
        Require(File.ReadAllBytes(f.Journal).AsSpan().SequenceEqual(f.InitialJournal)); None(f);
        Require(typeof(ProductionAdmittedRelease).GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).All(c=>c.IsPrivate));
        Require(typeof(ProductionAdmittedRelease).GetProperties(BindingFlags.Instance|BindingFlags.NonPublic).All(p=>p.SetMethod is null));
    }
    private static void Workflow()
    {
        using var f=new Fixture(); var w=f.Workflow(); Require(w.SelectedChoice is null && !w.CanStart);
        w.SelectedChoice=w.Choices[0]; w.StartAsync().GetAwaiter().GetResult(); None(f);
        w.Confirmed=true; w.StartAsync().GetAwaiter().GetResult();
        Require(w.Heading=="Update verified" && f.Source.Adapter.Writes==1 && f.Source.Adapter.Reads==1 && f.Source.Adapter.Boots==1 && !f.Store.HasUnresolvedOperation);
        w.StartAsync().GetAwaiter().GetResult();Require(f.Source.Adapter.Writes==1);
    }
    private static void Missing()
    {
        foreach(string input in new[]{"release","binding","policy","current","state","other-state","clock","operation-store"})
        {
            using var f=new Fixture();
            switch(input)
            {
                case "release":f.Release=null;break; case "binding":f.Binding=null;break;case "policy":f.PolicyObservation=null;break;
                case "current":f.CurrentObservation=null;break;case "state":f.StateInput=null;break;
                case "other-state":f.StateInput=new ProductionAdmissionState(f.StatePath);break;
                case "clock":f.Clock=null;break;case "operation-store":f.StoreInput=null;break;
            }
            Require(!f.TryCreate(out var e) && e is null);None(f);
        }
    }
    private static void Payload()
    {
        foreach(bool offset in new[]{false,true})
        {
            using var f=new Fixture(); byte[] before=File.ReadAllBytes(f.StatePath); var bytes=(byte[])f.Source.Image.Clone();
            if(!offset)bytes[0]^=1; f.Binding=Bind(bytes,offset?8192u:4096u);
            Require(!f.TryCreate(out _));None(f);Require(File.ReadAllBytes(f.Journal).AsSpan().SequenceEqual(f.InitialJournal));
            Require(File.ReadAllBytes(f.StatePath).AsSpan().SequenceEqual(before));
        }
    }
    private static void Pairing()
    {
        using var f=new Fixture(); var old=f.Release; f.Data["sourceBuildId"]="tampered";
        Require(f.Admit() is null);f.Release=null;Require(!f.TryCreate(out _));None(f);
        f.Sign();var fresh=f.Admit();Require(fresh is not null && !ReferenceEquals(old,fresh));f.Release=fresh;
        Require(f.TryCreate(out _));None(f);
    }
    private static void ContextChanges()
    {
        using var f=new Fixture();var original=f.Current!;
        foreach(var changed in new[]{original with{TargetProfile="other"},original with{Bootloader="other"},original with{Firmware="other"},
            original with{Generation=2}, original with{WriterProfileDigest=new string('E',64)},
            original with{PreservationPlanDigest=new string('E',64)},original with{RecoveryRequirementsDigest=new string('E',64)}})
        {
            f.Current=original;var e=f.Create();Require(e.Select(f.Source.Selection));var token=e.Confirm();f.Current=changed;
            Require(!e.Revalidate());e.RunAsync(token).GetAwaiter().GetResult();None(f);
        }
        f.Current=null;Require(!f.TryCreate(out _));f.CurrentObservation=()=>throw new IOException("private context unavailable");Require(!f.TryCreate(out _));
    }
    private static void PolicyChanges()
    {
        foreach(string change in new[]{"revoke","revision","expiry","missing","exception","clock-exception"})
        {
            using var f=new Fixture();var e=f.Create();Require(e.Select(f.Source.Selection));var token=e.Confirm();
            switch(change)
            {
                case "revoke":f.Source.CurrentPolicy=f.Source.Policy with{Revoked=true};break;
                case "revision":f.Source.CurrentPolicy=f.Source.Policy with{Revision=2};break;
                case "expiry":f.Source.Now=f.Source.Policy.ValidUntil;break;case "missing":f.Source.CurrentPolicy=null;break;
                case "exception":f.ThrowPolicy=true;break;case "clock-exception":f.ThrowClock=true;break;
            }
            Require(!e.Revalidate());e.RunAsync(token).GetAwaiter().GetResult();None(f);
            Require(File.ReadAllBytes(f.Journal).AsSpan().SequenceEqual(f.InitialJournal));
        }
    }
    private static void Floors()
    {
        foreach(string change in new[]{"clock","unknown","policy","corrupt","missing","locked"})
        {
            using var f=new Fixture();var e=f.Create();Require(e.Select(f.Source.Selection));
            FileStream? held=null;
            try
            {
                switch(change)
                {
                    case "clock":Require(f.State.TryAdvance(f.Source.Now+1,1));break;
                    case "unknown":f.Source.Now=-1;break;case "policy":Require(f.State.TryAdvance(f.Source.Now,2));break;
                    case "corrupt":File.WriteAllText(f.StatePath,"corrupt");break;
                    case "missing":File.Move(f.StatePath,f.StatePath+".retained");break;
                    case "locked":held=new FileStream(f.StatePath,FileMode.Open,FileAccess.ReadWrite,FileShare.None);break;
                }
                Require(!e.Revalidate());Run(e);None(f);
            }
            finally{held?.Dispose();}
        }
    }
    private static void Invalidation()
    {
        foreach(string change in new[]{"file","session","selection"})
        {
            using var f=new Fixture();var w=f.Workflow();w.SelectedChoice=w.Choices[0];w.Confirmed=true;
            if(change=="file")f.Source.CurrentInspection=null;
            else if(change=="session")f.Source.Controller.ReturnToProductChoice();
            else f.Source.Adapter.CurrentSelection=f.Source.Selection with{Generation=2};
            w.Poll();Require(!w.Confirmed && !w.CanStart);w.StartAsync().GetAwaiter().GetResult();None(f);
        }
    }
    private static void BeforeCommand()
    {
        using var f=new Fixture(); var original=f.Current;
        f.CurrentObservation=()=>File.ReadAllText(f.Journal).Contains("\"state\":\"unresolved\"",StringComparison.Ordinal)
            ? original! with{WriterProfileDigest=new string('E',64)}:original;
        var e=f.Create();Require(e.Select(f.Source.Selection));Run(e);
        Require(e.State==HostUpdateState.NoWriteStopped && e.UncertaintyMarkerReleased && File.ReadAllBytes(f.Journal).AsSpan().SequenceEqual(f.InitialJournal));None(f);
    }
    private static void AfterCommand()
    {
        using var f=new Fixture();f.Source.Adapter.Write=()=>{f.Current=f.Current! with{RecoveryRequirementsDigest=new string('E',64)};return Task.CompletedTask;};
        var e=f.Create();Require(e.Select(f.Source.Selection));Run(e);
        Require(e.State==HostUpdateState.OutcomeUnknown && f.Source.Adapter.Writes==1 && f.Source.Adapter.Reads==0 && f.Source.Adapter.Boots==0 && f.Store.HasUnresolvedOperation);
        Require(new HostOperationStore(f.OperationRoot).RecoveryDigest==f.Source.Manifest.RecoveryDigest);
    }
    private static void Cancellation()
    {
        using var f=new Fixture();var pending=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Source.Adapter.Write=()=>pending.Task;var e=f.Create();Require(e.Select(f.Source.Selection));var run=e.RunAsync(e.Confirm());
        try{Require(f.Source.Adapter.Writes==1);e.Cancel();run.GetAwaiter().GetResult();using var lease=new HostOperationStore(f.OperationRoot).TryAcquire();Require(lease is null && e.State==HostUpdateState.OutcomeUnknown);}
        finally{pending.TrySetResult();run.GetAwaiter().GetResult();}
        Require(SpinWait.SpinUntil(f.CanAcquire,3000));Require(f.Source.Adapter.Reads==0 && f.Source.Adapter.Boots==0 && f.Store.HasUnresolvedOperation);
    }
    private static void Recovery()
    {
        using var f=new Fixture();using(var lease=f.Store.TryAcquire())Require(lease is not null && f.Store.MarkUncertain(f.Release!.Manifest.Text("paddedImageSha256")));
        var first=f.Release;var w=f.Workflow();w.SelectedChoice=w.Choices[0];Require(w.CanReviewRecovery && !w.CanStart);
        w.ReviewRecovery();Require(!ReferenceEquals(first,f.Release));Require(w.CanConfirm && !w.CanStart);
        w.StartAsync().GetAwaiter().GetResult();None(f);w.Confirmed=true;w.StartAsync().GetAwaiter().GetResult();
        Require(w.Heading=="Original restored" && !f.Store.HasUnresolvedOperation && f.Source.Adapter.Writes==1);
    }
    private static void WrongRecovery()
    {
        using var f=new Fixture();using(var lease=f.Store.TryAcquire())Require(lease is not null && f.Store.MarkUncertain(new string('E',64)));
        var bytes=File.ReadAllBytes(f.Journal);f.Release=f.Admit();var e=f.Create(true);Require(!e.Select(f.Source.Selection));Run(e);None(f);
        Require(f.Store.HasUnresolvedOperation && File.ReadAllBytes(f.Journal).AsSpan().SequenceEqual(bytes));
    }
    private static void Copies()
    {
        using var f=new Fixture();var e=f.Create();var image=f.Release!.HostRelease.CopyImage();var canonical=f.Release.Manifest.CanonicalBytes();
        f.Source.Image[0]^=1;canonical[0]=0;f.Binding!.CopyBytesAt(0)[0]^=1;
        f.Source.Adapter.Read=()=>Task.FromResult(HostRegionCheck.Of((byte[])image.Clone(),true));Require(e.Select(f.Source.Selection));Run(e);
        Require(e.State==HostUpdateState.Verified && LoaderProductionProviders.SignerTrustPolicies.Count==0 && new LoaderUpdateWorkflow().Choices.Count==0 && !f.Source.Controller.Snapshot.FirmwareWritingAvailable);
    }
    private static HostPreservationPayloadBinding Bind(byte[] bytes,uint offset=4096)
    {
        Require(HostPreservationPlan.TryCreate(20480,4096,[new(4096,8192)],[new(0,4096),new(12288,8192)],[new(offset,(ulong)bytes.Length)],out var plan));
        Require(HostPreservationPayloadBinding.TryCreate(plan,[new(offset,bytes)],out var binding));return binding!;
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly HostUpdateEngineTests.Fixture Source=new(select:false,generation:3);
        internal readonly string OperationRoot=Path.Combine(Path.GetTempPath(),"LUF.HostStore.Tests."+Guid.NewGuid().ToString("N"));
        internal readonly string ClockRoot=Path.Combine(Path.GetTempPath(),"T-0056.clock."+Guid.NewGuid().ToString("N"));
        internal readonly HostOperationStore Store;
        internal readonly ProductionAdmissionState State;
        internal readonly byte[] InitialJournal;
        internal string Journal=>Path.Combine(OperationRoot,HostOperationStore.JournalName);
        internal string StatePath=>Path.Combine(ClockRoot,"production-admission.state");
        internal HostOperationStore? StoreInput;
        internal ProductionAdmissionState? StateInput;
        internal ProductionAdmittedRelease? Release;
        internal HostPreservationPayloadBinding? Binding;
        internal ProductionCurrentState? Current=new(ProductionReleaseManifest.Profile,"boot-test","firmware-test",1,new string('B',64),new string('C',64),new string('D',64));
        internal Func<HostAdmissionPolicy?>? PolicyObservation;
        internal Func<ProductionCurrentState?>? CurrentObservation;
        internal Func<long>? Clock;
        internal bool ThrowPolicy,ThrowClock;
        internal readonly JsonObject Data;
        private byte[] Signature=[];
        internal Fixture()
        {
            Source.Policy=Source.Policy with{Signer="release-approver-and-signing-custodian",PublicKeyDigest=HostReleaseAdmission.Digest(TestKey.ExportSubjectPublicKeyInfo()),ValidUntil=(long)ProductionReleaseManifest.LifetimeMilliseconds};
            Source.CurrentPolicy=Source.Policy;Store=HostOperationStore.CreateIsolatedTestStore(OperationRoot);StoreInput=Store;InitialJournal=File.ReadAllBytes(Journal);
            State=ProductionAdmissionState.CreateIsolatedTestState(ClockRoot);StateInput=State;
            PolicyObservation=()=>ThrowPolicy?throw new IOException("private policy error"):Source.CurrentPolicy;
            CurrentObservation=()=>Current;Clock=()=>ThrowClock?throw new IOException("private clock error"):Source.Now;
            var h=Source.Manifest;
            Data=new JsonObject
            {
                ["format"]=ProductionReleaseManifest.Format,["product"]=h.Product,["target"]=h.Target,["targetProfile"]=ProductionReleaseManifest.Profile,
                ["targetDigest"]=h.TargetDigest,["layoutDigest"]=h.LayoutDigest,["sourceBuildId"]="T-0056-test-only",
                ["rawImageLength"]=Source.Image.Length,["rawImageSha256"]=h.ImageDigest,["paddedImageLength"]=Source.Image.Length,["paddedImageSha256"]=h.ImageDigest,
                ["destinationOffset"]=h.Offset,["destinationLength"]=Source.Image.Length,["compatibility"]=h.Compatibility,["requiredBootloader"]="boot-test",["requiredCurrentFirmware"]="firmware-test",
                ["minimumCurrentGeneration"]=1,["maximumCurrentGeneration"]=2,["generation"]=h.Generation,["signerRole"]=Source.Policy.Signer,["publicKeyFingerprint"]=Source.Policy.PublicKeyDigest,
                ["policyRevision"]=1,["issuedAtUtcMilliseconds"]=0,["expiresAtUtcMilliseconds"]=ProductionReleaseManifest.LifetimeMilliseconds,
                ["writerProfileDigest"]=new string('B',64),["preservationPlanDigest"]=new string('C',64),["recoveryRequirementsDigest"]=new string('D',64),["recoveryImageDigest"]=h.RecoveryDigest
            };
            Sign();Release=Admit();Require(Release is not null);Binding=Bind(Source.Image);
        }
        internal void Sign()=>Signature=TestKey.SignData(Encoding.UTF8.GetBytes(Data.ToJsonString()),HashAlgorithmName.SHA256,RSASignaturePadding.Pss);
        internal ProductionAdmittedRelease? Admit()=>ProductionReleaseAdmission.Admit(Encoding.UTF8.GetBytes(Data.ToJsonString()),Source.Image,Source.Image,Signature,TestKey.ExportSubjectPublicKeyInfo(),Source.CurrentPolicy,Current,State,Source.Now,out _);
        internal bool TryCreate(out HostUpdateEngine? e,bool recovery=false)=>ProductionUpdateComposition.TryCreate(Source.Controller,Source.Context,Source.Inspection,()=>Source.CurrentInspection,Release,Binding,PolicyObservation,CurrentObservation,StateInput,Source.Adapter,StoreInput,Clock,Source.Profile,out e,recovery);
        internal HostUpdateEngine Create(bool recovery=false){Require(TryCreate(out var e,recovery)&&e is not null);return e!;}
        internal LoaderUpdateWorkflow Workflow()=>new([Source.Selection],(recovery,_)=>{if(recovery){Release=Admit();Require(Release is not null);}return Create(recovery);});
        internal bool CanAcquire(){using var lease=new HostOperationStore(OperationRoot).TryAcquire();return lease is not null;}
        public void Dispose()
        {
            Source.Dispose();foreach(var root in new[]{OperationRoot,ClockRoot})
            {
                string prefix=root==OperationRoot?"LUF.HostStore.Tests.":"T-0056.clock.";
                string leaf=Path.GetFileName(root);
                Require(Path.GetDirectoryName(root)==Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)&&leaf.StartsWith(prefix,StringComparison.Ordinal)&&Guid.TryParseExact(leaf[prefix.Length..],"N",out _)&&(File.GetAttributes(root)&FileAttributes.ReparsePoint)==0);
                Directory.Delete(root,true);
            }
        }
    }
}