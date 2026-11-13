namespace LimitedUnderground.FirmwareLoader;

// The owner's Trail release signing key (owner is approver and key custodian). Pinned here as the
// single trust anchor for production admission (V1 checklist L7). It is NOT registered in
// LoaderProductionProviders.SignerTrustPolicies, so bundle inspection still never marks anything
// trusted; only the qualification install path (QualificationInstall, L7 step 5) admits releases
// signed by it, through full production admission. See docs/RELEASE-SIGNING-KEY.md.
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

    // The same key as DER SubjectPublicKeyInfo (base64), embedded so production admission does not
    // depend on a file next to the program. A test checks it equals the committed PEM.
    internal const string PublicKeySpkiBase64 =
        "MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEAxsDvNVRiKjqZ8D0hEMfjBNaEOdqv8oLXCTCQwa3y44v8NX3X0wjo043xxxu6Oq4d5ucDfVgHhwjI05fVcJ5w7LDvdA8qf56y//ib8s2XxGGiiHJc8oPC+asT+fHXn56UYiHga6uV1i3wd3OpJEyRIyBXla1NbxfDOiEAdHJVwfK7R1Xy2CG71p5dXsV4FXBsYNOvZ6KQdgo5R2L8OwvHLUy2LZ4OX6bKJ3N0Ig/n43OGXi/QQU9BQ9S8FaL59wnholuvDN7cCx3AiARIX0KJYuiBrb7SBgDQuFDi8aNg+kCg9zuknLr44ZlBKzF3RBViW6gIyEkhZXcPn1c64+cuP+N8koK2CsWdEgXnTQpBdIumB/hIlx3OnK5eaOFxSnrgdEsbs09WBWJEn77ci+d7GdyTD0KDpL7ZzHuXtpPu5GGV/nrtlXuiWjRnCbtarsePHsuzLzsQHUOenaCyE/+Fh9kxbbeJEhhFBwf9/EiL6e1Dov6Nqglo77rK5VO2IzO9AgMBAAE=";

    internal static byte[] PublicKeySpki() => Convert.FromBase64String(PublicKeySpkiBase64);
}
