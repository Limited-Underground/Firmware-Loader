# Limited Underground Firmware Loader — MeshTrail tester guide

Use only the package, board and test instructions assigned to you by your Limited Underground test coordinator. Flashing replaces the board's firmware. Ask the coordinator to confirm the package, the board and permission for this test before starting.

Qualification is limited to the boards and signed releases approved for this PC. A package marked **Unsigned package fixture — do not install or flash** is a host test fixture. Stop there.

## Install and open

1. Use the Windows 11 Pro 25H2 x64 PC agreed with your coordinator. Keep the package folders together. The coordinator must supply the expected package identity and installer publisher.
2. Open **installer/FirmwareLoaderSetup.exe**. Check that Windows shows the expected publisher before approving setup. If the installer is missing, unsigned or from a different publisher, stop and request a verified package.
3. Follow setup's instructions. An administrator approves installation and prepares recovery storage. If Windows reports a missing .NET 10 Desktop Runtime, or setup cannot finish, contact the coordinator for the approved prerequisite and installation help.
4. Sign in to the Windows account your coordinator assigned, then open **Limited Underground Firmware Loader** normally. If you see **Daily qualification unavailable**, ask the coordinator to check this PC's setup and assigned account.

## Choose the board and firmware

1. Connect only your assigned MeshTrail board with a data-capable USB cable. Close other applications that might use the board. Keep the matching install and restore folders supplied by the coordinator.
2. Choose **Limited Underground MeshTrail**, then **Install signed qualification release**. Choose the package's **firmware/install** folder. Keep release.json, release.sig, raw.bin and padded.bin together.
3. Wait while the Loader checks the release and board against this PC's approved assignment.
4. At **Choose the board**, verify this is the board your coordinator assigned and select **OK**.
5. Compare the MeshTrail release generation with your assignment. At **Confirm firmware update**, confirm once for this board and signed release.

## Wait for the result

Leave the board and cable connected until the Loader reports the result.

| Step | What the Loader does |
| --- | --- |
| Check the board | Checks the release and the board state it requires. |
| Write the firmware | Replaces the app firmware. |
| Verify the written bytes | Checks the written app and protected regions. |
| Check that it starts | Waits for the expected firmware and startup checks. |

Success requires **Update verified**. The summary says **Installed Limited Underground MeshTrail, release generation …**, identifying the release now running after verification and startup checks. Compare that generation with the assignment and record the result. You may unplug after the Loader reports verified success.

## If something stops

| Message or problem | What to do |
| --- | --- |
| **Daily qualification unavailable** or **Windows account not approved** | Contact your test coordinator to check the Loader setup and your assigned Windows account. |
| **Flash tool unavailable** or **Recovery storage unavailable** | Ask the coordinator to check the package or installation. |
| **No board found** | Check the assigned board's data cable, then choose the signed release again. Contact the coordinator if it is still missing. |
| **More than one board connected** | Keep only the assigned board connected, then choose the signed release again. |
| **Board not approved**, a release refusal, or unexpected board details | Stop and send the visible message to the coordinator. Keep the assigned files together. |
| Cable drop, failed verification, **Outcome unknown**, or **Recovery review required** | Stop. Do not retry, reset, unplug further or try a restore. Save a support file and contact the coordinator first. |
| **Stop here** after a stopped attempt | Save a support file and ask the coordinator before another attempt. |

Choose **Save a support file** when available. Review it and send it privately through the agreed test channel, with what happened and when. If it is unavailable during preparation, send the visible message instead. Keep firmware files and board details private.

## Restore only when instructed

A restore uses its own approved, signed **firmware/restore** folder and the board state that release expects. Wait for the coordinator's instruction for that restore session. Use **Install signed qualification release** again with the restore folder and review its generation before confirming. A restore can have a higher release generation while putting back earlier firmware.

Wait for **Update verified** and the installed generation assigned for the restore. An uncertain attempt first needs the coordinator's recovery assessment.
