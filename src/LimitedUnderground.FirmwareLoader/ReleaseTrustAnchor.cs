namespace LimitedUnderground.FirmwareLoader;

// The owner's Trail release signing key (owner is approver and key custodian). Pinned here as the
// single trust anchor that future production admission policies must use (V1 checklist L7).
// Deliberately NOT registered in LoaderProductionProviders.SignerTrustPolicies: on its own it grants
// no trust, admission or installation capability. See docs/RELEASE-SIGNING-KEY.md.
internal static class ReleaseTrustAnchor
{
    internal const string Algorithm = "rsa_pss_3072_sha256";

    internal const string SignerRole = "release-approver-and-signing-custodian";

    internal const ulong TrustRevision = 1;

    internal const int KeySizeBits = 3072;

    // SHA-256 of the DER SubjectPublicKeyInfo in PublicKeyPemPath, in the uppercase hex form used by
    // HostReleaseAdmission.Digest and the release manifest's publicKeyFingerprint field.
    internal const string PublicKeySha256 =
        "7E0217140CA694F8109B0BA071D0C97CA10BF4D1D302AB330DB950E82E9F4254";

    internal const string PublicKeyPemPath = "docs/keys/lu-release-2026.pub.pem";
}
