# LUF-0009d: durable host operation ownership

Validation layer: **host implementation reviewed, validated and owner-accepted**. This does not enable production firmware installation or establish physical-device recovery.

## Result and boundary

The existing host engine previously had only test-double lease/journal implementations. `HostOperationStore` now implements both existing interfaces using one stable exclusive file handle and a bounded durable uncertainty notice. Cooperating processes sharing the same explicitly initialized test store cannot operate concurrently. An interrupted operation cannot resume automatically after restart.

The engine, production provider registry, signer trust, UI, target rules and device adapter are unchanged. Production still has no operational factory/choices and the engine still requires `host-fixture-v1`. Only a fresh isolated disposable test store can be explicitly initialized; production storage enrollment/root/ACL authority is not implemented.

## Complete operation reviewed

| Before state / owner | Trigger and source | Required effect and evidence | Forbidden effect / failure outcome |
|---|---|---|---|
| No enrolled store | Explicit `CreateIsolatedTestStore` | New canonical temporary GUID root; CreateNew files; flushed idle record | Ordinary open never initializes, repairs or replaces an established store |
| Idle host engine | Existing explicit selection and confirmation | Current fixture target, artifact, policy and one-use token checked | No automatic selection, cached confirmation or production admission |
| No owner | `HostOperationStore.TryAcquire` | Stable lock file opened with `FileShare.None`; canonical handle checked | Contender gets no lease and submits zero commands |
| Exclusive owner | Strict post-lock reload plus engine `CheckBinding` | Current bounded canonical record checked again | Missing/corrupt/unreadable/changed store cannot pass a stale startup snapshot |
| Confirmed owner, before mutation | `MarkUncertain` | Exact recovery digest plus random operation digest; temporary write, storage flush, atomic replacement, fresh exact readback | Any marker failure prevents all adapter commands; no automatic retry |
| Durable marker, owned operation | Existing engine write | One fake-adapter write with exact current fixture selection | Timeout, cancellation or possible partial failure becomes OutcomeUnknown |
| Write acknowledgement | Existing independent readback | Exact immutable image and protected-state comparison | Acknowledgement alone cannot establish verified bytes or authorize boot |
| Verified bytes | Existing boot evidence | Exact image digest, healthy boot and protected-state match | Wrong/unhealthy/missing boot cannot establish success |
| Exact boot evidence, live owner | `MarkComplete` | Exact owned operation and recovery binding; flush/replace/readback terminal notice | Foreign/stale/disposed completion refuses; failed terminal persistence reports OutcomeUnknown |
| Pending command after interruption | Existing engine finally/continuation | Lock retained until actual adapter task quiescence | A late result releases ownership only; cannot progress or clear uncertainty |
| Closed or killed process | Fresh store/engine | OS handle released; current journal reloaded | Unresolved operation refuses normal update; restart does not provide confirmation or physical success |
| Recovery review | Existing separately admitted original and fresh confirmation | Exact stored recovery digest, complete write/read/boot sequence and terminal commit | Wrong original or reused candidate token cannot authorize recovery |

Source: [host store](../../src/LimitedUnderground.FirmwareLoader/HostOperationStore.cs), [unchanged engine](../../src/LimitedUnderground.FirmwareLoader/HostUpdateEngine.cs), [focused tests](../../tests/LimitedUnderground.FirmwareLoader.Tests/HostOperationStoreTests.cs).

If complete replacement has landed before readback fails, the current instance stays poisoned and its engine reports OutcomeUnknown. A fresh instance may read the canonical complete notice: exact engine byte/protected-state/boot checks preceded that replacement. This reconciles the durable state; the notice alone supplies no physical success, automatic resume or cached confirmation authority. Failures before terminal replacement retain the unresolved notice.

## Executed validation

- Installed .NET SDK **8.0.425**, Windows, `net8.0-windows`; no installation or network operation.
- Focused host-store run: **20 groups passed**, with warning-as-error build at zero warnings/errors. The final small cleanup/crash-loop coverage changes were included in the final complete matrix.
- One final maintained `tools/Test-Loader.ps1` run: **136 groups passed** (116 existing + 20 new); both Release warning-as-error builds had **0 warnings, 0 errors**.
- Final run: **2026-10-02T00:49:48Z through 00:50:04Z** (2026-10-01 local project date). Exit 0.
- Frozen source inventory: **28 source/XAML/project/configuration/runner files**, identical before/after the run. HEAD `cc8e172442a7733a901d7c929f94c1471b399a12`, branch `codex/loader-host-progress-20260929`, and Git index unchanged. No Git mutation/publication occurred.
- Independent source review found no blocker, including post-lock freshness, exact operation/recovery completion binding, marker ordering, process quiescence, privacy and the replacement-landed boundary. The final exact-GUID cleanup guard was separately acknowledged.

| Exercised case | Evidence boundary |
|---|---|
| Separate-process contender and live lease | Actual subprocess ACKs; actual competing engine submits zero commands |
| Abrupt process termination | Before write, before flush, after flush/before replace, after replace, after marker ACK and during actual engine fake write/readback/boot |
| Admission race | Another operation lands between the engine's pre-lock admission and actual lock acquisition; post-lock binding stops all commands |
| Cancellation and timeout | Actual engine keeps another process excluded until the pending fake command quiesces; late completion leaves uncertainty |
| Persistence faults | Injected write/flush/replace/readback failures at start and completion; prewrite failure means zero commands, terminal failure cannot fabricate Verified/Recovered |
| Established-store failures | Missing/deleted/inaccessible, empty/corrupt/truncated/oversized, noncanonical/unknown-schema, replaced store identity and missing stable lock; no implicit recreation |
| Path/ownership binding | Relative/dot/trailing-separator roots, directory junction, hard-linked journal, changed operation/recovery digest, foreign/stale/disposed completion and repeat disposal |
| Recovery and privacy | Fresh separately confirmed exact-original recovery; wrong digest/token refusal; real files contain only schema/state/opaque digests, never identifying payloads |

Test children use bounded checkpoint acknowledgements, not timed guesses. Cleanup verifies an exact canonical temporary GUID root; junction and hardlink aliases are removed separately with native nonrecursive APIs before the owned root's native recursive deletion. No cross-shell deletion or broad temporary cleanup is performed.

## Final pins

| Input | SHA-256 |
|---|---|
| `HostOperationStore.cs` | `b74f9dee8d6ffda244982172112fa66809f1fd7acdb1a104e969b6d2a12b25d8` |
| `HostOperationStoreTests.cs` | `700a5d3095cd39c3159edf85415a274b7d22140d5a0ab0e8af6a0d5dcd1a74c9` |
| `Program.cs` | `4eaa886858bd3d35b38be0fc420637e4868e9458fd172e2b0c3a780513480af2` |
| Unchanged `HostUpdateEngine.cs` | `af9da26aa0de8c91084278200f0072d24fe964b6bc0dca9f33153661995a7f29` |
| Unchanged `tools/Test-Loader.ps1` | `70202c4cb1c1bcaa9457ad2f06fab368df7193bff90ece8a66eff70e2c260c9e` |
| Final test assembly, 147456 bytes | `1c353bb551008193892ff62d845b1931dc4b7f6d3ebc19ce91e0c71b40eef665` |
| Final matrix log | `3d7bbccf1d84b87cf28636458de076b07e586cf73402bb9806c1fd61ac9c6db7` |

The private receipt retains the full source and build artifact pins. The maintained runner deletes its exact temporary build directory after validation; reproducible binary equality is not claimed.

## Remaining gates

Production store enrollment/root/ACL selection, other Windows accounts or unrelated tools/driver exclusion, real signer/envelope/revocation/time authority, real physical adapter/device installation and recovery, sudden power-loss durability, network filesystems, packaging/signing/deployment and enabled production UI remain unvalidated and separately gated. Inaccessible-journal tests use actual exclusive-file denial; operating-system ACL-denial, disk-full and power-cut tests were not run. Injected persistence failures are host evidence rather than measured hardware storage failures.

No hardware access, enumeration, device identifiers, firmware artifacts, signing keys, public website change or V1 physical/release progress credit occurred. The owner accepted this bounded revision-1 host result. The next gate is separately approved production authority/adapter work.
