# D5 finite writer contract V1 — host shape only

T-0068 supplies a reviewable contract shape and synthetic host fixtures for
LUF-0008b, LUF-0009f and LUF-0009h. It is **not an admitted physical profile,
installation authority or recovery proof**. Production choices and signer
catalogs remain disabled. Host validation never establishes physical acceptance.

## Unassigned production inputs

The following are required facts, not defaults. Their production values remain
**UNASSIGNED**. No USB, reset, erase or timing value is chosen by this document.

| Required fact | Production value / refusal |
|---|---|
| Profile revision and exact interface/driver identity | UNASSIGNED; missing, ambiguous or stale identity refuses |
| USB identifiers, selected exact unit and interface-open behavior | UNASSIGNED; no discovery, selection or open is authorized |
| Reset entry/exit sequence and all reset side effects | UNASSIGNED; unknown side effects refuse |
| Erase geometry, erase operations and every affected byte | UNASSIGNED; unknown erase effects refuse |
| Write/read/boot/total deadlines, maximum chunk and attempt bound | UNASSIGNED; missing or unbounded values refuse |
| Observable command completion and quiescence mechanism | UNASSIGNED; cancellation or timeout alone is insufficient |
| Complete current originals, custody and independently authorized recovery | UNASSIGNED; missing originals or recovery authority refuse |
| Readback coverage, expected bytes and boot-health predicates | UNASSIGNED; partial comparison or unbound boot observation refuses |

The numerical values and `fixture-*` identities in the tests are synthetic,
reusing the existing bounded host fixture. They are neither proposed device
settings nor evidence of real transport, preservation or recovery behavior.
Nullable draft fields retain missing facts; the fixture validator refuses null,
unknown enum values, blank/reserved identities and unbounded limits. A passing
fixture must not be registered as a production profile.

## Identity and immutable binding

A prospective profile must identify its reviewed interface, driver and profile
revision. Each attempt additionally binds the explicit selection's opaque
identity, generation, target, model and adapter. A replacement selection or
changed generation invalidates a write even if its display text matches.

The fixture captures an immutable `HostPreservationPayloadBinding`, finite
`HostAdapterProfile` limits and a copied, read-only span-evidence collection.
Caller collection replacement cannot change the validated contract. It retains
original-set, original-boot and written-boot SHA-256 identities. This checker
issues no admission or operational token and performs no I/O.

The existing production manifest's `writerProfileDigest`,
`preservationPlanDigest` and `recoveryRequirementsDigest` references remain
unchanged. This task defines no canonical production contract encoding, real
contract digest or new manifest property. Matching hash references alone do not
supply missing originals, accepted custody, device facts or recovery authority.
The adopted production admission/composition boundary remains separate.

## Complete side-effect and span model

All ranges are half-open `[offset, offset + length)`. The immutable preservation
plan already requires bounded, nonoverflowing geometry, power-of-two erase
blocks and erase-rounded containment. The D5 fixture further requires a complete,
nonoverlapping partition of the entire declared address space, with at most 256
spans. Nothing may disappear between named partitions or at an address-space
tail. Every byte is classified as written or preserved and compared.

For the existing app-only engine fixture there is exactly one payload/write span.
Its expected readback digest must equal the retained payload digest. A command
must fit entirely within that raw write span and the maximum chunk. A write into
an erase-rounded tail, unused allowed gap, protected span, empty range or wrapped
address is refused. Erase-rounded containment does not enlarge write authority.

Every other byte is preserved: each span binds its original digest and the same
expected post-operation digest. Preserved spans cannot overlap the raw write.
If an erase range intersects preserved bytes, explicit tail restoration is
required. A real implementation would need accepted original bytes and a bounded
read/merge/restore mechanism for those bytes; a declaration or hash is not proof
that such a mechanism exists. Unknown reset effects and erase effects outside
the declared erase ranges refuse the fixture contract.

## Finite commands and quiescence

Every used stage has a positive finite write/read/boot deadline, a total budget
at least as large as their sum, and a positive bounded maximum chunk. This
fixture allows one existing engine attempt only. It does not authorize transport
retries or automatically resume an uncertain operation. Actual limits remain
unassigned until supported by the reviewed physical interface contract.

Tests reuse `HostUpdateEngine` and the same concrete isolated
`HostOperationStore` for lease and durable journal. Refused drafts construct no
engine attempt and submit no command. A submitted command retains ownership
until its task actually completes. Cancellation, deadline expiry, a requested
reset or return from a cancellation wrapper cannot assert quiescence. The
pending-command test confirms that a second store owner cannot acquire the
lease before completion. Even eventual quiescence does not turn
`OutcomeUnknown` into verified success or clear the unresolved journal.

## Originals, readback and boot evidence

Each evidence span carries an original hash and its expected readback hash.
Normal completion requires the existing engine's `Verified` state, observed
quiescence, the exact original-set binding, complete ordered comparisons for
all spans, and healthy/protected-state boot evidence bound to the written image.
Missing, partial, reordered, changed or unrelated observations refuse.

Recovery is a separate predicate: it requires `Recovered`, quiescence, the same
original-set identity, every span compared against its original digest, and
boot evidence bound to the original image. Written-image success cannot stand
in for restored-original success. Synthetic recovery metadata exercises this
separation; it does not execute recovery or prove possession or restoration of
real originals. Independently authorized recovery and physical boot-health
acceptance remain required later.

## Host implementation and evidence limits

- [D5FiniteWriterContract.cs](../src/LimitedUnderground.FirmwareLoader/D5FiniteWriterContract.cs)
  implements the pure fixture consistency, write-boundary and completion predicates.
- [D5FiniteWriterContractTests.cs](../tests/LimitedUnderground.FirmwareLoader.Tests/D5FiniteWriterContractTests.cs)
  exercises missing facts, finite bounds, identity changes, full coverage,
  original/readback/boot binding, real bounded-engine/store reuse and pending
  command ownership. Program.cs registers these groups in the full host suite.
- [TESTING.md](TESTING.md) defines the maintained full host gate. Both Release
  builds and the full suite are required; host results are not physical evidence.

No device adapter, production registration, protected-store enrollment, trust
activation, signing, artifact nomination or manifest-format change is included.
The next planned step is the D2 nomination **checklist**, not nomination itself.
