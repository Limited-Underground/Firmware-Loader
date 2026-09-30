# LUF-0009c populated Loader UI checkpoint

## Actual UI integration

[MainWindow](../../src/LimitedUnderground.FirmwareLoader/MainWindow.xaml) now embeds
[LoaderUpdatePanel](../../src/LimitedUnderground.FirmwareLoader/LoaderUpdatePanel.xaml)
inside its existing scrollable selected-product view. The production panel has no
device source or engine factory: selection, confirmation and update controls stay
disabled, no device enumeration occurs, and the existing inspection-only banner
remains visible. Bundle inspection still supplies no signer/admission authority.
Changing the selected product, accepted file choice, returning to the chooser or
closing the actual MainWindow clears inspection and invalidates the panel.

[LoaderUpdateWorkflow](../../src/LimitedUnderground.FirmwareLoader/LoaderUpdateWorkflow.cs)
binds the actual accepted host engine to those same controls when a deterministic
fixture factory is injected by tests. A visible simulated-review banner makes that
layer explicit. There is no production fixture toggle or physical adapter. Device
choices use fixed public model text and session-local numbering, never raw ports,
serials, paths or arbitrary caller labels. The selected exact model, admitted
release generation/compatibility/byte count and full image digest are visible
before confirmation. The engine exposes its immutable release manifest solely
for this display projection; its admission and operation behavior is unchanged.

The ComboBox does not select a device automatically. A separate CheckBox confirms
one reviewed attempt; Start remains disabled until preflight and that confirmation
are valid. Selection changes consume prior UI confirmation and cancel the prior
engine. UI operations carry a revision so late completion from an invalidated
product/file/device/session cannot populate the new view. Unexpected errors are
mapped to fixed safe text, with no exception payload shown or retained in diagnostics.

## State and failure review

| Actual state/action | Visible result and boundary |
| --- | --- |
| No production authority | Installation unavailable, empty disabled device selector, no operation buttons enabled |
| Explicit fixture selection | Exact model and admitted image identity; preflight passed, separate confirmation required |
| Writing / restoring | In-progress phase, cancellation warning; never described as verified |
| Readback | Written, verifying exact bytes and protected spans; write acknowledgement is insufficient |
| Boot wait | Bytes verified, checking exact startup identity and health; still not successful |
| Verified | Exact host operation checks passed, with field/release acceptance explicitly separate |
| Cancel / uncertain result | Outcome unknown and recovery guidance; ownership remains with pending engine command until it stops |
| Reselection after uncertainty | Prior unresolved mutation remains visible, even if the new selection itself did not write |
| Recovery review | Separate original admission and fresh confirmation; normal update is not retried |
| Recovered | Original restored; attempted update is not relabeled successful |
| Product/file/close invalidation | Choices/confirmation cleared, old results suppressed; earlier uncertainty retained |

A UI DispatcherTimer refreshes phase text while a real engine task is active.
It stops on control unload, which also invalidates the workflow. No simulated
percentage is displayed. Cancel and recovery use the actual engine methods;
changing UI state cannot turn a submitted command into a no-write result.

The first focused runs exposed two real UI issues: a normal Select call could
replace an initial RecoveryReview with NoWriteStopped, and WPF's null-selection
callback during list clearing could overwrite prior uncertainty. Both were fixed
and are covered by recovery, invalidation and reselection regressions. Independent
review also called out the reselection wording; the corrected UI explicitly
retains the earlier unresolved attempt. Test setup corrections restored System.IO
implicit import when enabling WPF, drained bindings before simulated selection,
and corrected an assertion about the negative recovery-success sentence.

## Validation and populated visual evidence

VERIFIED 2026-09-29: the maintained `tools/Test-Loader.ps1` path used installed
.NET SDK **8.0.425**, Release, `net8.0-windows`, warning-as-error builds. Focused UI
validation passed **10 groups**; the final complete affected matrix passed
**116 groups** (106 existing plus 10 WPF/UI groups). Both final builds reported
**0 warnings, 0 errors**. No external installation or tooling download was needed.
The maintained runner deletes its exact temporary build directory normally;
no installer, retained application binary or production package is claimed.

[LoaderUpdateUiTests](../../tests/LimitedUnderground.FirmwareLoader.Tests/LoaderUpdateUiTests.cs)
uses STA Dispatcher execution, actual ComboBox and CheckBox bindings, and actual
Start/Cancel/Recovery routed handlers. Tests cover explicit selection/confirmation,
stale confirmation, session invalidation before/during commands, late completion,
uncertain readback, separate recovery and sanitized factory failure. The existing
host engine tests remain in the final matrix. The test-only `LUF_UI_ONLY=1` switch
selects focused UI groups; it is absent from the final matrix command.

The tests render actual WPF controls using RenderTargetBitmap, not a separate HTML
mockup. Thirteen panel/window-state images were visually inspected: production
blocked, unselected, preflight ready, writing, readback, startup wait, uncertain,
verified, recovery ready, recovered and blocked preflight, plus the actual
MainWindow at its minimum 720x520 size with top/bottom scrolling. Text and action
controls remain readable/reachable; the main window keeps the inspection-only
footer outside the scrolling content. Images are private review evidence under
the privately retained UI evidence set; they are not public marketing or hardware screenshots.

## Remaining gates

This is a completed, independently reviewed host/UI checkpoint awaiting owner
acceptance. Production remains inspection-only. Real device enumeration, atomic
physical adapter selection, durable journal storage, cross-process device locking,
release trust/enrollment, physical write/readback/startup/recovery, clean-machine
installation and distribution remain separate work. Offscreen WPF rendering does
not establish physical device behavior or a full accessibility/desktop-operator
acceptance matrix. No hardware, production signing, installation, publication,
deployment, V1 credit or public website status change occurred.

Independent review passed after reading the final source, corrected uncertainty
regression and 116-group log and viewing all 13 final images. The reviewer did
not rerun the suite. No actionable issue remains in this bounded host/UI scope.

The UI checkpoint is owner-accepted. LUF-0010a physical installation/recovery
requires its separately approved exact setup and fresh physical authorization;
this checkpoint does not authorize that execution.
