# D2 firmware-nomination checklist V1

T-0071 prepares the evidence packet for LUF-0008b [#37](https://github.com/Limited-Underground/Firmware-Loader/issues/37).
**No firmware artifact is nominated here.** All candidate source/build, image,
manifest, signature and artifact-acceptance fields remain **UNASSIGNED**. Source
A's November build, a diagnostic/experimental image, a host fixture or the newest
available image is not an automatic nominee. The firmware owner must supply a
qualifying packet after security admission; this checklist neither builds nor
selects one. A signed pre-release qualification candidate need not be a final
public V1 release, but it must meet its recorded prerequisites.

## Existing references, not a candidate

- [PRODUCTION-RELEASE-MANIFEST-V1](PRODUCTION-RELEASE-MANIFEST-V1.md), adopted by
  [PR #48](https://github.com/Limited-Underground/Firmware-Loader/pull/48), owns
  exact canonical metadata, representation, compatibility, signature and admission
  rules. This checklist changes none of them.
- D1 target provenance: [Trail #222](https://github.com/Limited-Underground/Trail/pull/222),
  commit `8a16cf75280f53b8c085f040eb8304b070ad65a5`,
  `firmware/targets/heltec_v4_bench/target-contract.json`, exact bytes **6861**,
  SHA-256 **BF40D99AA261A616060672238EB0C301E462E164C5FA2AE3CCDABB7E78EBBBBA**.
  The associated accepted layout CSV is **546** bytes, SHA-256
  **CD612068AD53B8BA7C3ECD0A2DB9128AD8B35515FAB4F10D9D0386E1CA9300F5**,
  as recorded in the manifest contract. These are target/layout references, not
  image hashes or installation authority. Loader's
  [inspection pin](../src/LimitedUnderground.FirmwareLoader/OpenTrailInspectionProvider.cs)
  agrees with the target hash; a Git object ID is not a delivered-byte SHA-256.
- [D5 shape in PR #51](https://github.com/Limited-Underground/Firmware-Loader/pull/51),
  reviewed head `bbdd5735886bdd08aba3cb7e90c1e078a1a2217c`, supplies the
  [finite-writer contract](https://github.com/Limited-Underground/Firmware-Loader/blob/bbdd5735886bdd08aba3cb7e90c1e078a1a2217c/docs/D5-FINITE-WRITER-CONTRACT-V1.md).
  It is a contract shape, not a real profile or recovery proof. Its real
  USB/reset/erase/timing values remain UNASSIGNED. Referencing its synthetic
  limits does not fill a production field.

## Packet checklist

For each row, retain an immutable evidence reference, its exact source identity,
the check result and the recorded acceptance authority. Missing, conflicting,
expired or unassigned evidence stays blocked; a filename or unchecked hash is
insufficient. No row below is populated with a candidate by this document.

| Item / evidence required | Where it comes from | How to check | Gate / current packet state |
|---|---|---|---|
| Qualifying Trail source and security admission: exact source commit, accepted source selection, OT-0237e admission decision and all eight gate evidence references | Existing firmware-owner security/admission record for [OT-0237e #88](https://github.com/Limited-Underground/Trail/issues/88) and [OT-0005i #75](https://github.com/Limited-Underground/Trail/issues/75) | Verify the accepted commit is the packet's source, and that every admission gate is accepted for that source; an unrelated or earlier success cannot qualify it | **Blocked until G-FW-4 security admission**; UNASSIGNED |
| Build identity: immutable sourceBuildId, full commit, toolchain/SDK/compiler versions, dependency identities, configuration and build recipe/result | Firmware owner's retained build record for that qualifying source | Cross-check commit and configuration against admission, tie both delivered files to the same build record and preserve exact provenance; do not infer a build from a branch/date or rebuild here | **Blocked until G-FW-4** and a qualifying firmware-owner build packet; UNASSIGNED |
| Raw factory-app representation: exact file identity, length and SHA-256 | Firmware owner's immutable build delivery and artifact manifest | Independently count/hash delivered bytes, compare with rawImageLength/rawImageSha256 and the build record; never use a Git SHA or ZIP/container hash in place of image bytes | **Blocked until G-FW-4** and delivery; UNASSIGNED |
| Padded representation: exact file identity, length, SHA-256 and documented padding provenance | The same immutable delivery/build record | Count/hash separately; require 0 < raw length <= padded length <= 16 MiB and raw bytes equal the padded prefix. Do not invent a padding value or derive identity from a filename | **Blocked until G-FW-4** and delivery; UNASSIGNED |
| Destination and D1/layout binding: target/profile identities, targetDigest/layoutDigest and intended destination span | Accepted D1 target/layout above, firmware-owner delivery and production manifest | Compare exact target/layout bytes and hashes, target/profile strings and compatibility. destinationLength equals padded length; check nonoverflowing end, factory-app bounds, whole erase-rounded containment and no unadmitted protected span/gap/tail | D1 reference available at **G-LDR-2**; candidate binding **blocked until G-FW-4** delivery and accepted real D5 geometry; UNASSIGNED |
| Compatibility/generation predicates: required bootloader/current firmware, compatibility value, inclusive current-generation range and candidate generation | Firmware owner's accepted compatibility record and manifest; later authorized current-state evidence | Require exact predicate matches, positive compatibility/generations, min <= observed <= max, candidate > observed and policy floor satisfied. Refuse unknown current state or same-generation reinstall; nomination is not a device observation | Definitions **blocked until G-FW-4** delivery; actual current-unit match **blocked until G-LDR-5** authorized acceptance; UNASSIGNED |
| D5 writer/preservation/originals: exact contract identities, complete spans, original image/set hashes, custody and independently authorized recovery requirements | Firmware/Loader owners' accepted real D5 input records, using PR #51's shape | Match writerProfileDigest, preservationPlanDigest, recoveryRequirementsDigest and recoveryImageDigest to actual accepted bytes; cover every written/preserved/compared byte including gaps/tails. Require finite commands and observable quiescence; reject fixture substitution or missing originals | Shape only. Real inputs remain **blocked for G-LDR-3 integration and G-LDR-5 readiness**; UNASSIGNED |
| Boot and recovery: specified healthy/protected-state predicates, candidate/original boot identities, complete readback comparisons and separately authorized restoration evidence | Firmware owner's boot/recovery requirements; existing separately authorized acceptance records, when available | Before nomination, record exact predicates, original binding and evidence status. Later verify candidate boot/readback and independent original restoration against D5; distinguish expected predicates from executed proof, and original recovery from candidate success | Requirements **blocked until G-FW-4** qualified packet; executed physical proof **blocked until G-LDR-5**. No physical proof supplied here |
| Production manifest and detached signature: exact canonical manifest bytes/length/hash, manifest-to-images/build/target/D5 binding, signature bytes/hash and recorded status | Firmware/release owner's immutable manifest/signature delivery under PRODUCTION-RELEASE-MANIFEST-V1 | Use existing parser/verifier; exact 28 fields/order, canonical UTF-8 and all frozen bounds. Verify full RSA-PSS-3072/SHA-256 signature (384 bytes) against the approved complete public SPKI; unsigned/diagnostic/test-key data cannot pass production admission | Real signing **blocked until G-LDR-4 owner key and signed qualification release**; UNASSIGNED |
| Trust/policy and admission: owner-approved public SPKI fingerprint/trust revision, authorized policy/snapshot, issued-at/expiry, revision and existing persisted-state evidence; exact admission result/refusal | Owner/release custodian and accepted #38/#40 authority/storage records, then the existing Loader verifier | D3 role appointment is not a key. Check enrolled key/policy and D4's exact 180 days/no grace, revocation/revision and persisted clock high-water rules. Missing/corrupt/unknown state refuses; no auto-enrollment/repair or new time authority | Key/signature **blocked until G-LDR-4**; actual policy/store/D5 inputs still required for **G-LDR-3** admission. UNASSIGNED; production admission remains refused |
| Nomination and acceptance statement: firmware owner's explicit exact image-pair/manifest nomination, purpose, review references and recorded status of signature, admission, boot and recovery | Existing firmware-owner delivery/decision route and #37, with existing responsible reviewers/owner | Bind the statement to all immutable identities above; quote its actual acceptance limits. A diagnostic/experimental image, nomination for qualification, approved signed pre-release and physical acceptance are distinct facts; never promote one to another | **Blocked until G-FW-4** and the firmware owner's qualifying evidence/statement; later signing and physical claims retain **G-LDR-4/5** gates. NONE nominated by this checklist |

## How to nominate later

1. After G-FW-4, obtain the firmware owner's existing qualifying source/build
   packet through #37's evidence route. Do not select source A's November image,
   generate replacement bytes or nominate an artifact as a Loader worker.
2. Freeze the delivered raw/padded files, manifest and any signature/acceptance
   records. Record their identities and independently check every applicable row.
   Retain missing items as UNASSIGNED with the named gate and responsible source;
   never fill a hash, signer, timing value or positive verdict by assumption.
3. Have the firmware owner explicitly identify the exact proposed image pair and
   corresponding manifest and record the existing independent review/acceptance
   status. Read back that evidence on #37 for the Loader owner. Incomplete
   nomination prerequisites keep the packet pending. Signature/admission and
   later physical proof are separate claims: missing G-LDR-4/5 results must remain
   visibly blocked, rather than being inferred from nomination or made up here.
4. Only the existing authorized release/Loader admission process may evaluate a
   signed qualification candidate with real accepted inputs. Record actual
   refusal or acceptance and its source/hash binding. A final public V1 release
   is not an added circular prerequisite; nomination never grants installation.
5. Keep #37 open until its original requirements are accepted. No checklist
   completion, host test, source merge or nomination statement by itself completes
   production integration, signing or G-LDR-5 physical flash/restore acceptance.

This document completes the planned checklist preparation only. If this and the
preceding host changes land, the currently scoped G-LDR-3 **PC preparation work**
is complete; genuine blocked production inputs, G-LDR-4 owner-only key/signing
and G-LDR-5 physical acceptance remain. It changes neither manifest/D5 contracts
nor Project status. Host validation is never physical acceptance.
