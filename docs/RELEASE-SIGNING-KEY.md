# Release signing key (trust anchor)

The owner's key that signs Trail firmware release records. Created 2026-11-12 by the owner, who is the single release approver and key custodian.

| Item | Value |
|---|---|
| Algorithm | RSA 3072, RSA-PSS with SHA-256 (salt 32), detached 384-byte signatures (`rsa_pss_3072_sha256`) |
| Public key | [keys/lu-release-2026.pub.pem](keys/lu-release-2026.pub.pem) |
| Fingerprint | SHA-256 of the DER SubjectPublicKeyInfo: `7E0217140CA694F8109B0BA071D0C97CA10BF4D1D302AB330DB950E82E9F4254` (the same value OpenSSL prints in lower case) |
| Signer role | `release-approver-and-signing-custodian` |
| Trust revision | 1 |
| Source constant | [ReleaseTrustAnchor](../src/LimitedUnderground.FirmwareLoader/ReleaseTrustAnchor.cs) |

## What this does and does not do

The key is pinned as the one trust anchor that a future production admission policy must use when the first real release is prepared (V1 checklist L7). It is **not** registered as production signer trust: `LoaderProductionProviders.SignerTrustPolicies` stays empty, no update choice is offered and installation remains disabled. A production policy also needs the accepted target, layout, compatibility, write ranges and recovery image, which come from nominating the first firmware build ([D2 nomination checklist](D2-NOMINATION-CHECKLIST-V1.md)).

[ReleaseTrustAnchorTests](../tests/LimitedUnderground.FirmwareLoader.Tests/ReleaseTrustAnchorTests.cs) check that the pinned fingerprint is the SHA-256 of the committed public key, in the same form `HostReleaseAdmission.Digest` produces; that the key is one complete RSA 3072 public key; that signatures from any other key are rejected; and that production trust stays empty.

## Custody, loss and replacement

The private key never enters this repository. It is passphrase-encrypted and held only by the owner; where it is kept is recorded in the owner's workspace records, not here.

- **Lost:** the owner restores it from their own backup copy; nothing here changes.
- **Stolen or exposed:** the owner creates a new key; this file and the source constant move to the new fingerprint at trust revision 2, and releases signed by the old key are no longer accepted. Until that change ships, releases signed with the old key could be accepted, so replacement is urgent.
- Releases are signed by the owner only. No AI or automated process signs or holds the private key.
