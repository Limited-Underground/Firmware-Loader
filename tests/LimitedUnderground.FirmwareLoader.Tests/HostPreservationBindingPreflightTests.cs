using LimitedUnderground.FirmwareLoader;
using System.Security.Cryptography;

internal static class HostPreservationBindingPreflightTests
{
    private static readonly RSA Key = RSA.Create(3072);
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("preservation preflight accepts genuinely admitted exact synthetic image", Positive),
        ("preservation preflight accepts reordered split classification unions", SplitClassifications),
        ("preservation preflight accepts equal policy values and exact expiry boundary", EqualPolicyAndTime),
        ("preservation preflight default result refuses and contains no references", DefaultResult),
        ("preservation preflight refuses each missing explicit input", MissingInputs),
        ("preservation preflight refuses a valid multi-payload binding", MultiplePayloads),
        ("preservation preflight compares every current policy field", ChangedPolicyFields),
        ("preservation preflight refuses revoked zero revision and expired policy", UnusablePolicy),
        ("preservation preflight refuses inconsistent manifest authority metadata", ManifestMismatch),
        ("preservation preflight refuses malformed fixture digest and signer declarations", MalformedDeclarations),
        ("preservation preflight compares retained bytes rather than equal lengths", ChangedPayload),
        ("preservation preflight recomputes image digest and enforces uppercase hex", ChangedDigest),
        ("preservation preflight refuses different raw offsets and lengths", RawRangeMismatch),
        ("preservation preflight refuses invalid retained image lengths", InvalidImageLength),
        ("preservation preflight refuses incompatible erase block and unaligned write", BlockAndAlignment),
        ("preservation preflight refuses any enlarged allowed classification", EnlargedAllowance),
        ("preservation preflight refuses policy protection classified as allowed", ProtectedAsAllowed),
        ("preservation preflight refuses policy ranges outside the declared geometry", PolicyOutsideGeometry),
        ("preservation preflight refuses overlapping and empty policy intervals", InvalidPolicyIntervals),
        ("preservation preflight accepts exact uint end and refuses endpoint above it", UintEndpoint),
        ("preservation preflight keeps erase and rounding factory refusals", FactoryEraseRefusals),
        ("preservation preflight keeps null empty and oversized payload factory refusals", FactoryPayloadRefusals),
        ("preservation preflight snapshots survive caller and returned-copy mutation", SnapshotMutation),
        ("preservation preflight reevaluates changed policy and time after success", FreshEvaluation),
    };

    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    private static byte[] Image() => Enumerable.Range(0, 17).Select(value => (byte)value).ToArray();
    private static HostAdmissionPolicy Policy() => new(
        new string('A', 64), new string('B', 64), 1, 3, "synthetic-preflight-only",
        HostReleaseAdmission.Digest(Key.ExportSubjectPublicKeyInfo()), 7, 1000, false,
        16, 64, 16, 96, 32, new string('C', 64));
    private static HostReleaseManifest Manifest(HostAdmissionPolicy policy, byte[] image, uint offset = 32) => new(
        1, "opentrail", "heltec_v4_bench", policy.TargetDigest, policy.LayoutDigest,
        policy.Compatibility, 7, policy.Signer, policy.Revision,
        HostReleaseAdmission.Digest(image), image.Length, offset, policy.RecoveryDigest);

    private sealed class Fixture
    {
        internal byte[] Bytes { get; }
        internal HostAdmissionPolicy Policy { get; }
        internal HostReleaseManifest Manifest { get; }
        internal HostAdmittedRelease Release { get; }
        internal Fixture(HostAdmissionPolicy? policy = null, uint offset = 32, byte[]? image = null)
        {
            Policy = policy ?? HostPreservationBindingPreflightTests.Policy();
            Bytes = image ?? Image();
            Manifest = HostPreservationBindingPreflightTests.Manifest(Policy, Bytes, offset);
            byte[] signature = Key.SignData(Manifest.CanonicalBytes(), HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            Release = HostReleaseAdmission.Admit(Manifest, Bytes, signature, Key.ExportSubjectPublicKeyInfo(),
                Policy, 10, out var refusal) ?? throw new InvalidOperationException("synthetic fixture admission: " + refusal);
        }
    }

    private static HostPreservationPlan Plan(ulong size = 128, ulong block = 16,
        HostPreservationRange[]? allowed = null, HostPreservationRange[]? protectedRanges = null,
        HostPreservationRange[]? writes = null)
    {
        Require(HostPreservationPlan.TryCreate(size, block,
            allowed ?? new HostPreservationRange[] { new(16, 32), new(48, 32) },
            protectedRanges ?? new HostPreservationRange[] { new(0, 16), new(80, 16), new(96, 32) },
            writes ?? new HostPreservationRange[] { new(32, 17) }, out var plan), "valid synthetic plan refused");
        return plan!;
    }
    private static HostPreservationPayloadBinding Binding(HostPreservationPlan? plan = null, byte[]? bytes = null)
    {
        plan ??= Plan();
        Require(HostPreservationPayloadBinding.TryCreate(plan,
            new[] { new HostPreservationPayload(plan.Writes[0].Offset, bytes ?? Image()) }, out var binding),
            "valid synthetic binding refused");
        return binding!;
    }
    private static HostPreservationBindingPreflightResult Evaluate(Fixture fixture,
        HostPreservationPayloadBinding? binding = null, long now = 10) =>
        HostPreservationBindingPreflight.Evaluate(fixture.Release, binding ?? Binding(), fixture.Policy, now);
    private static void Accept(HostPreservationBindingPreflightResult result)
    {
        Require(result.Status == HostPreservationBindingPreflightStatus.FixtureConsistent &&
            result.Reason == HostPreservationBindingPreflightReason.None, "consistent synthetic fixture refused");
    }
    private static void Refuse(HostAdmittedRelease? release, HostPreservationPayloadBinding? binding,
        HostAdmissionPolicy? policy, long now = 10)
    {
        var result = HostPreservationBindingPreflight.Evaluate(release, binding, policy, now);
        Require(result.Status == HostPreservationBindingPreflightStatus.Refused &&
            result.Reason != HostPreservationBindingPreflightReason.None &&
            Enum.IsDefined(result.Reason), "inconsistent synthetic fixture accepted or unbounded reason returned");
    }

    private static void Positive()
    {
        var fixture = new Fixture(); var binding = Binding();
        Require(binding.Plan.Writes[0] == new HostPreservationRange(32, 17) &&
            binding.Plan.EraseRanges[0] == new HostPreservationRange(32, 32), "fixture raw and erase spans");
        Accept(Evaluate(fixture, binding));
        Require(binding.CopyBytesAt(0).SequenceEqual(fixture.Release.CopyImage()), "exact retained snapshots");
    }
    private static void SplitClassifications()
    {
        var fixture = new Fixture();
        var plan = Plan(
            allowed: new HostPreservationRange[] { new(64, 16), new(32, 32), new(16, 16) },
            protectedRanges: new HostPreservationRange[] { new(112, 16), new(80, 16), new(0, 16), new(96, 16) });
        Accept(Evaluate(fixture, Binding(plan)));
        // Adjacent protected spans jointly cover the complete fixture interval [96,128).
        Require(plan.ProtectedRanges.Count == 4, "classification split not retained");
    }
    private static void EqualPolicyAndTime()
    {
        var fixture = new Fixture(); var equal = fixture.Policy with { };
        Require(!ReferenceEquals(equal, fixture.Policy) && equal == fixture.Policy, "equal-value independent policy");
        Accept(HostPreservationBindingPreflight.Evaluate(fixture.Release, Binding(), equal, 0));
        Accept(Evaluate(fixture, now: fixture.Policy.ValidUntil - 1));
        Refuse(fixture.Release, Binding(), fixture.Policy, fixture.Policy.ValidUntil);
        Refuse(fixture.Release, Binding(), fixture.Policy, -1);
    }
    private static void DefaultResult()
    {
        var result = default(HostPreservationBindingPreflightResult);
        Require(result.Status == HostPreservationBindingPreflightStatus.Refused &&
            result.Reason == HostPreservationBindingPreflightReason.Unspecified, "default result must refuse");
        Require(!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<HostPreservationBindingPreflightResult>(),
            "result must not retain bytes, policy, release, plan or other authority");
    }
    private static void MissingInputs()
    {
        var fixture = new Fixture(); var binding = Binding();
        Refuse(null, binding, fixture.Policy); Refuse(fixture.Release, null, fixture.Policy);
        Refuse(fixture.Release, binding, null); Refuse(null, null, null);
    }
    private static void MultiplePayloads()
    {
        var fixture = new Fixture();
        var plan = Plan(writes: new HostPreservationRange[] { new(16, 1), new(48, 1) });
        Require(HostPreservationPayloadBinding.TryCreate(plan,
            new HostPreservationPayload[] { new(48, new byte[] { 2 }), new(16, new byte[] { 1 }) }, out var binding),
            "independent valid two-payload binding");
        var result = HostPreservationBindingPreflight.Evaluate(fixture.Release, binding, fixture.Policy, 10);
        Require(result.Status == HostPreservationBindingPreflightStatus.Refused &&
            result.Reason == HostPreservationBindingPreflightReason.SingleImage, "no partial multi-payload acceptance");
    }
    private static void ChangedPolicyFields()
    {
        var fixture = new Fixture(); var policy = fixture.Policy;
        foreach (var changed in new[]
        {
            policy with { TargetDigest = new string('D', 64) }, policy with { LayoutDigest = new string('D', 64) },
            policy with { Compatibility = 2 }, policy with { MinimumGeneration = 2 },
            policy with { Signer = "another-synthetic-signer" }, policy with { PublicKeyDigest = new string('D', 64) },
            policy with { Revision = 8 }, policy with { ValidUntil = 1001 }, policy with { Revoked = true },
            policy with { AllowedOffset = 0 }, policy with { AllowedLength = 80 },
            policy with { EraseBlock = 8 }, policy with { ProtectedOffset = 80 },
            policy with { ProtectedLength = 16 }, policy with { RecoveryDigest = new string('D', 64) },
        })
            Refuse(fixture.Release, Binding(), changed);
    }
    private static void UnusablePolicy()
    {
        byte[] image = Image();
        foreach (var policy in new[] { Policy() with { Revoked = true }, Policy() with { Revision = 0 },
            Policy() with { ValidUntil = 0 }, Policy() with { ValidUntil = -1 }, Policy() with { MinimumGeneration = 8 } })
        {
            // Direct construction is a negative robustness fixture, never proof of admission.
            Refuse(new HostAdmittedRelease(Manifest(policy, image), image, policy), Binding(), policy);
        }
    }
    private static void ManifestMismatch()
    {
        var fixture = new Fixture(); var manifest = fixture.Manifest;
        foreach (var changed in new[]
        {
            manifest with { Schema = 2 }, manifest with { Product = "opengauge" }, manifest with { Target = "other" },
            manifest with { TargetDigest = new string('D', 64) }, manifest with { LayoutDigest = new string('D', 64) },
            manifest with { RecoveryDigest = new string('D', 64) }, manifest with { Compatibility = 2 },
            manifest with { Generation = 0 }, manifest with { Generation = 2 },
            manifest with { Signer = "another-synthetic-signer" }, manifest with { PolicyRevision = 8 },
        })
            Refuse(new HostAdmittedRelease(changed, fixture.Bytes, fixture.Policy), Binding(), fixture.Policy);
    }
    private static void MalformedDeclarations()
    {
        byte[] image = Image();
        foreach (var policy in new[]
        {
            Policy() with { TargetDigest = "invalid" }, Policy() with { LayoutDigest = "invalid" },
            Policy() with { RecoveryDigest = "invalid" }, Policy() with { Signer = " " },
        })
            Refuse(new HostAdmittedRelease(Manifest(policy, image), image, policy), Binding(), policy);
    }
    private static void ChangedPayload()
    {
        var fixture = new Fixture(); var changed = Image(); changed[8] ^= 1;
        Refuse(fixture.Release, Binding(bytes: changed), fixture.Policy);
        Refuse(new HostAdmittedRelease(fixture.Manifest, changed, fixture.Policy), Binding(), fixture.Policy);
    }
    private static void ChangedDigest()
    {
        var fixture = new Fixture();
        foreach (string digest in new[] { new string('0', 64), fixture.Manifest.ImageDigest.ToLowerInvariant(), "invalid" })
        {
            Require(digest != fixture.Manifest.ImageDigest, "fixture digest must exercise ordinal comparison");
            Refuse(new HostAdmittedRelease(fixture.Manifest with { ImageDigest = digest }, fixture.Bytes, fixture.Policy),
                Binding(), fixture.Policy);
        }
    }
    private static void RawRangeMismatch()
    {
        var fixture = new Fixture();
        Refuse(fixture.Release, Binding(Plan(writes: new HostPreservationRange[] { new(48, 17) })), fixture.Policy);
        foreach (int length in new[] { 16, 18 })
        {
            var bytes = Enumerable.Range(0, length).Select(value => (byte)value).ToArray();
            Refuse(fixture.Release, Binding(Plan(writes: new HostPreservationRange[] { new(32, (ulong)length) }), bytes), fixture.Policy);
        }
    }
    private static void InvalidImageLength()
    {
        var fixture = new Fixture();
        foreach (int length in new[] { -1, 0, 16, 18, HostPreservationPayloadBinding.MaximumPayloadBytes + 1 })
            Refuse(new HostAdmittedRelease(fixture.Manifest with { ImageLength = length }, fixture.Bytes, fixture.Policy),
                Binding(), fixture.Policy);
        Refuse(new HostAdmittedRelease(fixture.Manifest, Array.Empty<byte>(), fixture.Policy), Binding(), fixture.Policy);
    }
    private static void BlockAndAlignment()
    {
        var fixture = new Fixture();
        Refuse(fixture.Release, Binding(Plan(block: 8)), fixture.Policy);
        var unaligned = Plan(writes: new HostPreservationRange[] { new(33, 17) });
        Refuse(new HostAdmittedRelease(fixture.Manifest with { Offset = 33 }, fixture.Bytes, fixture.Policy),
            Binding(unaligned), fixture.Policy);
    }
    private static void EnlargedAllowance()
    {
        var fixture = new Fixture();
        foreach (var plan in new[]
        {
            Plan(allowed: new HostPreservationRange[] { new(0, 80) },
                protectedRanges: new HostPreservationRange[] { new(80, 48) }),
            Plan(allowed: new HostPreservationRange[] { new(16, 80) },
                protectedRanges: new HostPreservationRange[] { new(0, 16), new(96, 32) }),
        })
            Refuse(fixture.Release, Binding(plan), fixture.Policy);
    }
    private static void ProtectedAsAllowed()
    {
        var fixture = new Fixture();
        var plan = Plan(allowed: new HostPreservationRange[] { new(16, 64), new(112, 16) },
            protectedRanges: new HostPreservationRange[] { new(0, 16), new(80, 32) });
        Refuse(fixture.Release, Binding(plan), fixture.Policy);
    }
    private static void PolicyOutsideGeometry()
    {
        foreach (var policy in new[]
        {
            Policy() with { AllowedLength = 128 },
            Policy() with { ProtectedOffset = 120, ProtectedLength = 16 },
        })
        {
            // Admission accepts these raw-image-safe policies; preflight adds explicit geometry containment.
            var fixture = new Fixture(policy);
            Refuse(fixture.Release, Binding(), policy);
        }
    }
    private static void InvalidPolicyIntervals()
    {
        var overlapping = new Fixture(Policy() with { AllowedLength = 96 });
        Refuse(overlapping.Release, Binding(), overlapping.Policy);
        byte[] image = Image();
        foreach (var policy in new[] { Policy() with { AllowedLength = 0 }, Policy() with { ProtectedLength = 0 } })
            Refuse(new HostAdmittedRelease(Manifest(policy, image), image, policy), Binding(), policy);
    }
    private static void UintEndpoint()
    {
        const ulong size = 1UL << 32;
        uint offset = uint.MaxValue - 17;
        var policy = Policy() with { AllowedOffset = offset, AllowedLength = 17, EraseBlock = 1,
            ProtectedOffset = 0, ProtectedLength = offset };
        var fixture = new Fixture(policy, offset);
        var exactPlan = Plan(size, 1, new HostPreservationRange[] { new(offset, 17) },
            new HostPreservationRange[] { new(0, offset), new(uint.MaxValue, 1) },
            new HostPreservationRange[] { new(offset, 17) });
        Accept(Evaluate(fixture, Binding(exactPlan)));

        uint overflowingOffset = offset + 1;
        var abovePolicy = policy with { AllowedOffset = overflowingOffset, ProtectedLength = overflowingOffset };
        byte[] image = Image(); var aboveManifest = Manifest(abovePolicy, image, overflowingOffset);
        var abovePlan = Plan(size, 1, new HostPreservationRange[] { new(overflowingOffset, 17) },
            new HostPreservationRange[] { new(0, overflowingOffset) },
            new HostPreservationRange[] { new(overflowingOffset, 17) });
        Refuse(new HostAdmittedRelease(aboveManifest, image, abovePolicy), Binding(abovePlan), abovePolicy);
        byte[] signature = Key.SignData(aboveManifest.CanonicalBytes(), HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        Require(HostReleaseAdmission.Admit(aboveManifest, image, signature, Key.ExportSubjectPublicKeyInfo(),
            abovePolicy, 10, out var reason) is null && reason == "address-plan", "retained admission uint endpoint still refuses");
    }
    private static void FactoryEraseRefusals()
    {
        Require(!HostPreservationPlan.TryCreate(128, 16, new HostPreservationRange[] { new(16, 33) },
            new HostPreservationRange[] { new(0, 16), new(49, 79) },
            new HostPreservationRange[] { new(32, 17) }, out var plan) && plan is null,
            "raw bytes fit but rounded erase reaches protected bytes");
        Require(!HostPreservationPlan.TryCreate(ulong.MaxValue, 2,
            new HostPreservationRange[] { new(0, ulong.MaxValue) }, Array.Empty<HostPreservationRange>(),
            new HostPreservationRange[] { new(ulong.MaxValue - 1, 1) }, out plan) && plan is null,
            "unrepresentable rounded address space must not wrap");
        var policy = Policy() with { ProtectedOffset = 49, ProtectedLength = 79 };
        byte[] image = Image(); var manifest = Manifest(policy, image);
        byte[] signature = Key.SignData(manifest.CanonicalBytes(), HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        Require(HostReleaseAdmission.Admit(manifest, image, signature, Key.ExportSubjectPublicKeyInfo(),
            policy, 10, out var reason) is null && reason == "address-plan", "admission erase-rounded protection remains mandatory");
    }
    private static void FactoryPayloadRefusals()
    {
        var plan = Plan();
        foreach (byte[]? bytes in new byte[]?[] { null, Array.Empty<byte>() })
            Require(!HostPreservationPayloadBinding.TryCreate(plan,
                new[] { new HostPreservationPayload(32, bytes) }, out var binding) && binding is null,
                "unavailable retained payload cannot be manufactured");
        Require(!HostPreservationPayloadBinding.TryCreate(plan, Array.Empty<HostPreservationPayload>(), out var missing) &&
            missing is null, "zero payload must remain a factory refusal");
        int over = HostPreservationPayloadBinding.MaximumPayloadBytes + 1;
        var oversizedPlan = Plan((ulong)over, 1, new HostPreservationRange[] { new(0, (ulong)over) },
            Array.Empty<HostPreservationRange>(), new HostPreservationRange[] { new(0, (ulong)over) });
        Require(!HostPreservationPayloadBinding.TryCreate(oversizedPlan,
            new[] { new HostPreservationPayload(0, new byte[over]) }, out var oversized) && oversized is null,
            "existing aggregate bound refuses before retained copies");
    }
    private static void SnapshotMutation()
    {
        var fixture = new Fixture(); byte[] original = Image(); var binding = Binding(bytes: original);
        fixture.Bytes[0] ^= 1; original[1] ^= 1;
        var releaseCopy = fixture.Release.CopyImage(); releaseCopy[2] ^= 1;
        var payloadCopy = binding.CopyBytesAt(0); payloadCopy[3] ^= 1;
        Accept(Evaluate(fixture, binding));
        Require(fixture.Release.CopyImage().SequenceEqual(Image()) && binding.CopyBytesAt(0).SequenceEqual(Image()),
            "preflight must consume retained immutable snapshots");
    }
    private static void FreshEvaluation()
    {
        var fixture = new Fixture(); var binding = Binding();
        Accept(Evaluate(fixture, binding));
        Refuse(fixture.Release, binding, fixture.Policy with { Revision = fixture.Policy.Revision + 1 });
        Refuse(fixture.Release, binding, fixture.Policy, fixture.Policy.ValidUntil);
        // A previous positive value carries no mutable authority and cannot change a later refusal.
        Accept(Evaluate(fixture, binding));
    }
}
