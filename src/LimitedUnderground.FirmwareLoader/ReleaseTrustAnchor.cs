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
        "6CF0CC7B5968ED2D4118842C2523650E2AE998C3DB6B367CD7A4B44886001111";

    internal const string PublicKeyPemPath = "docs/keys/lu-release-2026.pub.pem";

    // The same key as DER SubjectPublicKeyInfo (base64), embedded so production admission does not
    // depend on a file next to the program. A test checks it equals the committed PEM.
    internal const string PublicKeySpkiBase64 =
        "MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEAuAPFMnsYn1OH7GVE3BwXEkMCzOqOD1RNJlyg4vU4dPPbXfK0TNcBE+nwQt4uib2sYWFddBJikfbKTEci1ouYjHoiUO9IxvfHj5lr3JgtyoMgqPRmSN0octWRQWPyNekVovPv9uuim5N2ZQHWD0fcL6TAIo0l93AQYDDdX+uHDZdz9T2m2TMNcGaHfua25ZOruFgImAlAZ7krGXYdOkE/gFkOEHx4ImkSEeeddTkwsKUVU1DAJxxeL3GYCvDYKeOEbzrXIpAR0VNmzKmqiGbdaeRj6QVO8XjXD0m2t5qjYl52VQnAnfVBuZvgztY1C1ZpEMHBoAA1HXvZPg+Jtgvu9v5tZXV0H7df/173JiNf8MvBASE8JKy6u+aOq2/vNGvBDl4s4Mvk2vbYaPh5Ji72D/kgNhOD5PRKXAaId5xr4iRgn7UglQNPhzTXJQPBx40QlFlv9QhnvqBo6xbL0jbNfXBOkXiZKiKtzTmFDidiN4HWqs4Wnik9Ho8PMpieDipjAgMBAAE=";

    internal static byte[] PublicKeySpki() => Convert.FromBase64String(PublicKeySpkiBase64);
}
