using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using LimitedUnderground.FirmwareLoader;

internal static class ProductionReleaseManifestTests
{
    private static readonly RSA TestKey = RSA.Create(3072); // Ephemeral test-only key, never exported privately.
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("T-0043 canonical production manifest roundtrip and immutable snapshots", RoundTrip),
        ("T-0043 rejects every missing duplicate unknown wrong-type or reordered field", Grammar),
        ("T-0043 validates every canonical manifest value and numerical bound", Values),
        ("T-0043 verifies genuine production-format signature using existing RSA verifier", Positive),
        ("T-0043 refuses missing altered wrong-key or appended-key signatures", Signatures),
        ("T-0043 signature binds new source metadata outside legacy fixture payload", SignedMetadata),
        ("T-0043 refuses altered raw padded lengths digests and representations", Images),
        ("T-0043 refuses missing revoked changed trust policy and empty draft fingerprint", Authority),
        ("T-0043 refuses incompatible current target bootloader firmware generation and D5 references", Compatibility),
        ("T-0043 enforces issued-at exact 180-day expiry and no grace", Expiry),
        ("T-0043 persists clock high-water across reopen and refuses clock rollback", Clock),
        ("T-0043 persists policy revision across reopen and refuses rollback", Revision),
        ("T-0043 refuses missing corrupt locked or unavailable persisted state", StateFailures),
        ("T-0043 invalid signature cannot advance persisted authority state", FailedAttempt),
    };
    private static void Require(bool ok) { if (!ok) throw new InvalidOperationException("T-0043 production boundary assertion"); }
    private static void RoundTrip()
    {
        using var f = new Fixture(); var bytes = f.Bytes();
        Require(ProductionReleaseManifest.TryParse(bytes, out var parsed));
        Require(parsed!.CanonicalBytes().AsSpan().SequenceEqual(bytes));
        bytes[0] = 0; var returned = parsed.CanonicalBytes(); returned[0] = 0;
        Require(parsed.CanonicalBytes()[0] == (byte)'{');
        Require(ProductionReleaseManifest.TryParse(parsed.CanonicalBytes(), out var again));
        Require(again!.CanonicalBytes().AsSpan().SequenceEqual(parsed.CanonicalBytes()));
    }
    private static void Grammar()
    {
        using var f = new Fixture();
        foreach (var name in f.Data.Select(p => p.Key).ToArray())
        {
            var copy = (JsonObject)f.Data.DeepClone(); copy.Remove(name); Reject(copy);
            copy = (JsonObject)f.Data.DeepClone(); copy[name] = null; Reject(copy);
            string original = Encoding.UTF8.GetString(f.Bytes());
            Require(!ProductionReleaseManifest.TryParse(Encoding.UTF8.GetBytes(original[..^1] + ",\"" + name + "\":null}"), out _));
        }
        var unknown = (JsonObject)f.Data.DeepClone(); unknown["extra"] = 0; Reject(unknown);
        foreach (string bad in new[] { "{}", "[]", "null", "", " " + Encoding.UTF8.GetString(f.Bytes()),
            Encoding.UTF8.GetString(f.Bytes()).Replace("\"format\":", "\"format\" :", StringComparison.Ordinal),
            Encoding.UTF8.GetString(f.Bytes()).Replace("\"rawImageLength\":4", "\"rawImageLength\":4.0", StringComparison.Ordinal) })
            Require(!ProductionReleaseManifest.TryParse(Encoding.UTF8.GetBytes(bad), out _));
        var reversed = new JsonObject(); foreach (var item in f.Data.Reverse()) reversed[item.Key] = item.Value!.DeepClone(); Reject(reversed);
        Require(!ProductionReleaseManifest.TryParse(null, out _) && !ProductionReleaseManifest.TryParse(new byte[16385], out _));
    }
    private static void Values()
    {
        using var f = new Fixture();
        var invalid = new Dictionary<string, JsonNode?>
        {
            ["format"]="other",["product"]="other",["target"]="other",["targetProfile"]="other",
            ["targetDigest"]="bad",["layoutDigest"]="bad",["sourceBuildId"]="",["rawImageLength"]=0,
            ["rawImageSha256"]="bad",["paddedImageLength"]=0,["paddedImageSha256"]="bad",
            ["destinationOffset"]=ulong.MaxValue,["destinationLength"]=1,["compatibility"]=0,
            ["requiredBootloader"]="",["requiredCurrentFirmware"]="",["minimumCurrentGeneration"]=99,
            ["maximumCurrentGeneration"]=-1,["generation"]=0,["signerRole"]="other",["publicKeyFingerprint"]="bad",
            ["policyRevision"]=0,["issuedAtUtcMilliseconds"]=ulong.MaxValue,["expiresAtUtcMilliseconds"]=0,
            ["writerProfileDigest"]="bad",["preservationPlanDigest"]="bad",["recoveryRequirementsDigest"]="bad",["recoveryImageDigest"]="bad"
        };
        Require(invalid.Count == f.Data.Count);
        foreach (var item in invalid) { var copy=(JsonObject)f.Data.DeepClone(); copy[item.Key]=item.Value?.DeepClone(); Reject(copy); }
    }
    private static void Reject(JsonObject data) => Require(!ProductionReleaseManifest.TryParse(Encoding.UTF8.GetBytes(data.ToJsonString()), out _));
    private static void Positive()
    {
        using var f=new Fixture(); var result=f.Admit(); Require(result is not null && result!.HostRelease.CopyImage().AsSpan().SequenceEqual(f.Padded));
        Require(LoaderProductionProviders.SignerTrustPolicies.Count == 0 && new LoaderUpdateWorkflow().Choices.Count == 0);
    }
    private static void Signatures()
    {
        using var f=new Fixture(); f.Signature=[]; Require(f.Admit() is null); f.Sign(); f.Signature[0]^=1; Require(f.Admit() is null);
        f.Sign(); f.KeyBytes=[..f.KeyBytes!,0]; Require(f.Admit() is null);
        using var other=RSA.Create(3072); f.KeyBytes=other.ExportSubjectPublicKeyInfo(); Require(f.Admit() is null);
        f.KeyBytes=null; Require(f.Admit() is null);
    }
    private static void SignedMetadata()
    {
        using var f=new Fixture(); f.Data["sourceBuildId"]="different-source"; Require(f.Admit() is null);
        f.Sign(); Require(f.Admit() is not null); // Existing verifier verifies the new canonical payload, not old fixture bytes.
    }
    private static void Images()
    {
        using var f=new Fixture(); byte[] original=(byte[])f.Raw.Clone(); f.Raw[0]^=1; Require(f.Admit() is null); f.Raw=original;
        f.Padded[7]^=1; Require(f.Admit() is null); f.Padded=[1,3,5,7,255,255,255]; Require(f.Admit() is null);
        f.Raw=[]; Require(f.Admit() is null);
    }
    private static void Authority()
    {
        using var f=new Fixture(); var policy=f.Policy!;
        foreach(var p in new HostAdmissionPolicy?[] {null, policy with {Revoked=true},policy with {Revision=2},
            policy with {TargetDigest=new string('E',64)},policy with {PublicKeyDigest=new string('E',64)},
            policy with {ValidUntil=policy.ValidUntil+1},policy with {ProtectedOffset=4096}})
        { f.Policy=p; Require(f.Admit() is null); }
        f.Policy=policy; f.Data["publicKeyFingerprint"]=""; f.Sign(); Require(ProductionReleaseManifest.TryParse(f.Bytes(),out _)); Require(f.Admit() is null);
    }
    private static void Compatibility()
    {
        using var f=new Fixture(); f.Data["minimumCurrentGeneration"]=0; f.Sign(); var state=f.Current!;
        foreach(var s in new ProductionCurrentState?[] {null,state with {TargetProfile="other"},state with {Bootloader="other"},
            state with {Firmware="other"},state with {Generation=0},state with {Generation=2},
            state with {WriterProfileDigest=new string('E',64)},state with {PreservationPlanDigest=new string('E',64)},
            state with {RecoveryRequirementsDigest=new string('E',64)}})
        {f.Current=s;Require(f.Admit() is null);}
    }
    private static void Expiry()
    {
        using var f=new Fixture(); f.Now=999;Require(f.Admit() is null);
        f.Now=f.Policy!.ValidUntil;Require(f.Admit() is null); f.Now++;Require(f.Admit() is null);
        f.Now=f.Policy.ValidUntil-1;Require(f.Admit() is not null);
    }
    private static void Clock()
    {
        using var f=new Fixture(); f.Now=1200;Require(f.Admit() is not null);
        f.State=new ProductionAdmissionState(f.StatePath); f.Now=1199;Require(f.Admit() is null);
        f.Now=1200;Require(f.Admit() is not null);f.Now=-1;Require(f.Admit() is null);
    }
    private static void Revision()
    {
        using var f=new Fixture(); f.Data["policyRevision"]=2;f.Policy=f.Policy! with {Revision=2};f.Sign();Require(f.Admit() is not null);
        f.State=new ProductionAdmissionState(f.StatePath);f.Data["policyRevision"]=1;f.Policy=f.Policy! with {Revision=1};f.Sign();Require(f.Admit() is null);
    }
    private static void StateFailures()
    {
        using var f=new Fixture();f.State=null;Require(f.Admit() is null);
        f.State=new ProductionAdmissionState(Path.Combine(f.Root,"missing"));Require(f.Admit() is null);
        f.State=new ProductionAdmissionState(f.StatePath);
        using(var held=new FileStream(f.StatePath,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) Require(f.Admit() is null);
        File.WriteAllText(f.StatePath,"corrupt");Require(f.Admit() is null);
    }
    private static void FailedAttempt()
    {
        using var f=new Fixture();var before=File.ReadAllBytes(f.StatePath);f.Signature[0]^=1;f.Now=9000;Require(f.Admit() is null);
        Require(File.ReadAllBytes(f.StatePath).AsSpan().SequenceEqual(before));
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"T-0043."+Guid.NewGuid().ToString("N"));
        internal string StatePath=>Path.Combine(Root,"production-admission.state");
        internal ProductionAdmissionState? State;
        internal byte[] Raw=[1,3,5,7],Padded=[1,3,5,7,255,255,255,255],Signature=[];
        internal byte[]? KeyBytes=TestKey.ExportSubjectPublicKeyInfo();
        internal long Now=1000;
        internal HostAdmissionPolicy? Policy;
        internal ProductionCurrentState? Current=new(ProductionReleaseManifest.Profile,"boot-test","firmware-test",1,new string('A',64),new string('B',64),new string('C',64));
        internal readonly JsonObject Data;
        internal Fixture()
        {
            string hash=new('D',64),key=HostReleaseAdmission.Digest(KeyBytes!);
            ulong expiry=1000+ProductionReleaseManifest.LifetimeMilliseconds;
            Policy=new(hash,hash,1,2,"release-approver-and-signing-custodian",key,1,(long)expiry,false,4096,8192,4096,16384,4096,hash);
            Data=new JsonObject
            {
                ["format"]=ProductionReleaseManifest.Format,["product"]="opentrail",["target"]="heltec_v4_bench",["targetProfile"]=ProductionReleaseManifest.Profile,
                ["targetDigest"]=hash,["layoutDigest"]=hash,["sourceBuildId"]="test-only/source+build",
                ["rawImageLength"]=4,["rawImageSha256"]=HostReleaseAdmission.Digest(Raw),["paddedImageLength"]=8,["paddedImageSha256"]=HostReleaseAdmission.Digest(Padded),
                ["destinationOffset"]=4096,["destinationLength"]=8,["compatibility"]=1,["requiredBootloader"]="boot-test",["requiredCurrentFirmware"]="firmware-test",
                ["minimumCurrentGeneration"]=1,["maximumCurrentGeneration"]=1,["generation"]=2,["signerRole"]="release-approver-and-signing-custodian",["publicKeyFingerprint"]=key,
                ["policyRevision"]=1,["issuedAtUtcMilliseconds"]=1000,["expiresAtUtcMilliseconds"]=expiry,
                ["writerProfileDigest"]=new string('A',64),["preservationPlanDigest"]=new string('B',64),["recoveryRequirementsDigest"]=new string('C',64),["recoveryImageDigest"]=hash
            };
            State=ProductionAdmissionState.CreateIsolatedTestState(Root);Sign();
        }
        internal byte[] Bytes()=>Encoding.UTF8.GetBytes(Data.ToJsonString());
        internal void Sign()=>Signature=TestKey.SignData(Bytes(),HashAlgorithmName.SHA256,RSASignaturePadding.Pss);
        internal ProductionAdmittedRelease? Admit()=>ProductionReleaseAdmission.Admit(Bytes(),Raw,Padded,Signature,KeyBytes,Policy,Current,State,Now,out _);
        public void Dispose()
        {
            if(Path.GetDirectoryName(Root)!=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) ||
                !Path.GetFileName(Root).StartsWith("T-0043.",StringComparison.Ordinal) ||
                (File.GetAttributes(Root)&FileAttributes.ReparsePoint)!=0) throw new IOException("Fixture ownership lost.");
            Directory.Delete(Root,true);
        }
    }
}