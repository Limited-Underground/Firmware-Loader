using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("LimitedUnderground.FirmwareLoader.Tests")]

namespace LimitedUnderground.FirmwareLoader;

// LUF-0009b internal host checkpoint only. This is NOT a project release format,
// production trust enrollment, device adapter, or externally accepted contract.
internal sealed record HostReleaseManifest(
    uint Schema, string Product, string Target, string TargetDigest, string LayoutDigest,
    uint Compatibility, ulong Generation, string Signer, ulong PolicyRevision,
    string ImageDigest, int ImageLength, uint Offset, string RecoveryDigest)
{
    internal byte[] CanonicalBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("LUF-host-fixture-envelope-v1");
        writer.Write(Schema); writer.Write(Product); writer.Write(Target);
        writer.Write(TargetDigest); writer.Write(LayoutDigest); writer.Write(Compatibility);
        writer.Write(Generation); writer.Write(Signer); writer.Write(PolicyRevision);
        writer.Write(ImageDigest); writer.Write(ImageLength); writer.Write(Offset);
        writer.Write(RecoveryDigest);
        writer.Flush(); return stream.ToArray();
    }
}

internal sealed record HostAdmissionPolicy(
    string TargetDigest, string LayoutDigest, uint Compatibility, ulong MinimumGeneration,
    string Signer, string PublicKeyDigest, ulong Revision, long ValidUntil, bool Revoked,
    uint AllowedOffset, uint AllowedLength, uint EraseBlock,
    uint ProtectedOffset, uint ProtectedLength, string RecoveryDigest);

internal sealed class HostAdmittedRelease
{
    private readonly byte[] image;
    internal HostAdmittedRelease(HostReleaseManifest manifest, byte[] bytes, HostAdmissionPolicy policy)
    { Manifest = manifest; image = (byte[])bytes.Clone(); Policy = policy; }
    internal HostReleaseManifest Manifest { get; }
    internal HostAdmissionPolicy Policy { get; }
    internal byte[] CopyImage() => (byte[])image.Clone();
}

internal static class HostReleaseAdmission
{
    internal static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static bool Hash(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);

    internal static HostAdmittedRelease? Admit(HostReleaseManifest manifest, byte[] image,
        byte[] signature, byte[] publicKey, HostAdmissionPolicy? policy, long now, out string refusal)
    {
        // Freeze bounded caller-owned inputs before any digest or signature check.
        // A later caller mutation cannot change the key verified or the bytes retained.
        refusal = "input-bounds";
        if (image.Length == 0 || image.Length > 16 * 1024 * 1024 ||
            signature.Length != 384 || publicKey.Length == 0 || publicKey.Length > 8192) return null;
        image = (byte[])image.Clone();
        signature = (byte[])signature.Clone();
        publicKey = (byte[])publicKey.Clone();
        refusal = "missing-authority";
        if (policy is null) return null;
        refusal = "manifest-binding";
        if (manifest.Schema != 1 || manifest.Product != "opentrail" || manifest.Target != "heltec_v4_bench" ||
            !Hash(manifest.TargetDigest) || !Hash(manifest.LayoutDigest) || !Hash(manifest.RecoveryDigest) ||
            manifest.TargetDigest != policy.TargetDigest || manifest.LayoutDigest != policy.LayoutDigest ||
            manifest.Compatibility != policy.Compatibility || manifest.Generation == 0 ||
            manifest.Generation < policy.MinimumGeneration || manifest.RecoveryDigest != policy.RecoveryDigest)
            return null;
        refusal = "signer-policy";
        if (manifest.Signer != policy.Signer || string.IsNullOrWhiteSpace(policy.Signer) ||
            Digest(publicKey) != policy.PublicKeyDigest || policy.Revoked || policy.Revision == 0 || manifest.PolicyRevision != policy.Revision ||
            now < 0 || now >= policy.ValidUntil) return null;
        refusal = "image-digest";
        if (image.Length == 0 || image.Length > 16 * 1024 * 1024 || manifest.ImageLength != image.Length ||
            Digest(image) != manifest.ImageDigest) return null;
        refusal = "address-plan";
        // Whole erase-rounded span must be allowed and disjoint from protected bytes.
        if (policy.EraseBlock == 0 || policy.AllowedLength == 0 || policy.ProtectedLength == 0 ||
            manifest.Offset % policy.EraseBlock != 0) return null;
        ulong end = (ulong)manifest.Offset + (ulong)image.Length;
        ulong roundedEnd = ((end + policy.EraseBlock - 1) / policy.EraseBlock) * policy.EraseBlock;
        if (roundedEnd > uint.MaxValue || manifest.Offset < policy.AllowedOffset ||
            roundedEnd > (ulong)policy.AllowedOffset + policy.AllowedLength ||
            ((ulong)manifest.Offset < (ulong)policy.ProtectedOffset + policy.ProtectedLength &&
             roundedEnd > policy.ProtectedOffset)) return null;
        refusal = "signature";
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicKey, out int consumed);
            if (consumed != publicKey.Length || rsa.KeySize != 3072 || signature.Length != 384 ||
                !rsa.VerifyData(manifest.CanonicalBytes(), signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                return null;
        }
        catch (CryptographicException) { return null; }
        catch (ArgumentException) { return null; }
        refusal = "";
        return new HostAdmittedRelease(manifest, image, policy);
    }
}
