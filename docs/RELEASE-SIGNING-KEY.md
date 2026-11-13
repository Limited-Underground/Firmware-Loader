# Release signing key (trust anchor)

The owner's key that signs Trail firmware release records. Created 2026-11-12 by the owner, who is the single release approver and key custodian.

| Item | Value |
|---|---|
| Algorithm | RSA 3072, RSA-PSS with SHA-256 (salt 32), detached 384-byte signatures (`rsa_pss_3072_sha256`) |
| Public key | [keys/lu-release-2026.pub.pem](keys/lu-release-2026.pub.pem) |
| Fingerprint | SHA-256 of the DER SubjectPublicKeyInfo: `6CF0CC7B5968ED2D4118842C2523650E2AE998C3DB6B367CD7A4B44886001111` (the same value OpenSSL prints in lower case) |
| Signer role | `release-approver-and-signing-custodian` |
| Trust revision | 1 |
| Source constant | [ReleaseTrustAnchor](../src/LimitedUnderground.FirmwareLoader/ReleaseTrustAnchor.cs) |

## What this does and does not do

The key is pinned as the one trust anchor that a future production admission policy must use when the first real release is prepared (V1 checklist L7). It is **not** registered as bundle-inspection signer trust: `LoaderProductionProviders.SignerTrustPolicies` stays empty. Since L7 step 5 the key is used by exactly one path, the board A qualification install (D-063 to D-065), which admits a release only through full production admission against this key and a fixed qualification policy ([qualification release](QUALIFICATION-RELEASE.md#installing-it-l7-step-5)). The key is also embedded in the program as DER (`PublicKeySpkiBase64`); a test checks it equals the committed PEM.

[ReleaseTrustAnchorTests](../tests/LimitedUnderground.FirmwareLoader.Tests/ReleaseTrustAnchorTests.cs) check that the pinned fingerprint is the SHA-256 of the committed public key, in the same form `HostReleaseAdmission.Digest` produces; that the key is one complete RSA 3072 public key; that signatures from any other key are rejected; and that production trust stays empty.

## Custody, loss and replacement

The private key never enters this repository. It is passphrase-encrypted and held only by the owner; where it is kept is recorded in the owner's workspace records, not here.

- **Lost:** the owner restores it from their own backup copy; nothing here changes.
- **Stolen or exposed:** the owner creates a new key; this file and the source constant move to the new fingerprint at trust revision 2, and releases signed by the old key are no longer accepted. Until that change ships, releases signed with the old key could be accepted, so replacement is urgent.
- Releases are signed by the owner only. No AI or automated process signs or holds the private key.

## Replacement history

The first key (fingerprint `7E0217140CA694F8…`, created 2026-11-12) was replaced the same day, before it had signed or admitted anything: its saved passphrase did not unlock it. Releases are still at trust revision 1, because no release was ever signed by the first key and its fingerprint no longer matches anything the Loader trusts.
