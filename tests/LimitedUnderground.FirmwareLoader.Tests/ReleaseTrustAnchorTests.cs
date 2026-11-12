using LimitedUnderground.FirmwareLoader;
using System.Security.Cryptography;

internal static class ReleaseTrustAnchorTests
{
    internal static string? RepositoryRoot { get; set; }

    internal static IReadOnlyList<(string Name, Action Run)> All { get; } = new (string, Action)[]
    {
        ("L6 pinned release key fingerprint matches the committed public key", FingerprintMatchesCommittedKey),
        ("L6 pinned release key is one complete RSA 3072 public key", KeyIsCompleteRsa3072),
        ("L6 pinned fingerprint uses the admission digest format", FingerprintUsesAdmissionFormat),
        ("L6 pinned release key verifies RSA-PSS SHA-256 and rejects other keys", KeyVerifiesOnlyItsOwnSignatures),
        ("L6 release trust anchor is not registered as production trust", AnchorIsNotRegistered),
    };

    private static void Require(bool ok, string what)
    {
        if (!ok) throw new InvalidOperationException("L6 release trust anchor: " + what);
    }

    private static byte[] CommittedSpki()
    {
        var root = RepositoryRoot ?? throw new InvalidOperationException("repository root not set");
        var lines = File.ReadAllLines(Path.Combine(root, ReleaseTrustAnchor.PublicKeyPemPath));
        Require(lines.Length >= 3 && lines[0] == "-----BEGIN PUBLIC KEY-----", "PEM header");
        var end = Array.IndexOf(lines, "-----END PUBLIC KEY-----");
        Require(end > 1 && lines.Skip(end + 1).All(string.IsNullOrWhiteSpace), "single PEM block");
        return Convert.FromBase64String(string.Concat(lines.Skip(1).Take(end - 1)));
    }

    private static RSA ImportExact(byte[] spki)
    {
        var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(spki, out int read);
        Require(read == spki.Length, "SPKI has no trailing bytes");
        return rsa;
    }

    private static void FingerprintMatchesCommittedKey() =>
        Require(Convert.ToHexString(SHA256.HashData(CommittedSpki())) == ReleaseTrustAnchor.PublicKeySha256,
            "pinned fingerprint equals SHA-256 of the committed SPKI");

    private static void KeyIsCompleteRsa3072()
    {
        using var rsa = ImportExact(CommittedSpki());
        Require(rsa.KeySize == ReleaseTrustAnchor.KeySizeBits && ReleaseTrustAnchor.KeySizeBits == 3072, "RSA 3072");
        Require(ReleaseTrustAnchor.Algorithm == "rsa_pss_3072_sha256" && ReleaseTrustAnchor.TrustRevision == 1 &&
            ReleaseTrustAnchor.SignerRole == "release-approver-and-signing-custodian", "algorithm, role and revision");
    }

    private static void FingerprintUsesAdmissionFormat()
    {
        var pinned = ReleaseTrustAnchor.PublicKeySha256;
        Require(pinned.Length == 64 && pinned.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F'), "uppercase hex");
        Require(HostReleaseAdmission.Digest(CommittedSpki()) == pinned, "same form as HostReleaseAdmission.Digest");
    }

    private static void KeyVerifiesOnlyItsOwnSignatures()
    {
        // The pinned public key cannot sign; a signature from any other key must never verify under it.
        using var anchor = ImportExact(CommittedSpki());
        using var other = RSA.Create(3072);
        var data = "Limited Underground release manifest test"u8.ToArray();
        var signature = other.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        Require(signature.Length == 384, "RSA 3072 signature length");
        Require(other.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss), "control key verifies itself");
        Require(!anchor.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss), "other key rejected");
        Require(HostReleaseAdmission.Digest(other.ExportSubjectPublicKeyInfo()) != ReleaseTrustAnchor.PublicKeySha256,
            "other key has a different fingerprint");
    }

    private static void AnchorIsNotRegistered()
    {
        Require(LoaderProductionProviders.SignerTrustPolicies.Count == 0, "production signer trust stays empty");
        Require(new LoaderUpdateWorkflow().Choices.Count == 0, "no update choices are offered");
    }
}
