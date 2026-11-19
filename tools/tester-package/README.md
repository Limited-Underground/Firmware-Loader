# Limited Underground Firmware Loader — MeshTrail tester guide

Use only the package, board and test instructions assigned to you by your Limited Underground test coordinator. Flashing replaces the board's firmware. Ask the coordinator to confirm the package, the board and permission for this test before starting.

This build supports the board A qualification test only. If you were assigned another board, ask for a supported build and release. A package marked **Unsigned package fixture — do not install or flash** is a host test fixture. Stop there.

## Install and open

1. Use the Windows 11 Pro 25H2 x64 PC agreed with your coordinator. Keep the package folders together. The coordinator must supply the expected package identity and installer publisher.
2. Open **installer/FirmwareLoaderSetup.exe**. Check that Windows shows the expected publisher before approving setup. If the installer is missing, unsigned or from a different publisher, stop and request a verified package.
3. Follow setup's instructions. An administrator approves installation and prepares recovery storage. If Windows reports a missing .NET 10 Desktop Runtime, or setup cannot finish, contact the coordinator for the approved prerequisite and installation help.
4. Open **Limited Underground Firmware Loader**. The current board A test needs the coordinator's approved qualification session. If you see **Standard-user installation unavailable**, stop and contact the coordinator. The installer for standard-user daily use is still pending.

## Choose the board and firmware

1. Connect only the assigned board A with a data-capable USB cable. Close other applications that might use the board. Keep the matching install and restore folders supplied by the coordinator.
2. Choose **Limited Underground MeshTrail**, then **Install signed qualification release**. Choose the package's **firmware/install** folder. Keep release.json, release.sig, raw.bin and padded.bin together.
3. Wait at **Step 1 of 4: checking the release and the board**. No update starts during this preparation.
4. On a PC with no board A set, the Loader asks **Set board A**. Approve only if the coordinator authorized this PC's first setup with this exact board. Ask for help if you are unsure. This choice cannot be changed from the Loader later.
5. At **Board A ready**, choose **Board A · Heltec V4 · HTIT-WB32LAF V4.2** in the board list. The Loader leaves the choice empty until you select it.
6. Compare the MeshTrail release generation in the summary with the coordinator's assignment. Open **Details** to compare **Image SHA-256** if requested. Tick the confirmation box for this board and release, then choose **Start reviewed update** once.

## Wait for the result

Leave the board and cable connected through all four steps.

| Step | What the Loader does |
| --- | --- |
| Check the board | Checks the release and the board state it requires. |
| Write the firmware | Replaces the app firmware. |
| Verify the written bytes | Checks the written app and protected regions. |
| Check that it starts | Waits for the expected firmware and startup checks. |

Success requires **Update verified** and all four steps marked done. The summary then says **Installed Limited Underground MeshTrail, release generation …**, identifying the release now running. Compare that generation with the assignment and record the result. The image SHA-256 is in **Details**. You may unplug after the Loader says it is done.

## If something stops

| Message or problem | What to do |
| --- | --- |
| **Standard-user installation unavailable** | Contact your test coordinator about the approved qualification session. |
| **Flash tool unavailable** or **Recovery storage unavailable** | Ask the coordinator to check the package or installation. |
| **No board found** | Check the assigned board's data cable, then choose the signed release again. Contact the coordinator if it is still missing. |
| **More than one board connected** | Keep only the assigned board connected, then choose the signed release again. |
| **Not board A**, a release refusal, or unexpected board details | Stop and send the visible message to the coordinator. Keep the assigned files together. |
| Cable drop, failed verification, **Outcome unknown**, or **Recovery review required** | Stop. Do not retry, reset, unplug further or try a restore. Save a support file and contact the coordinator first. |
| **Stop here** after a stopped attempt | Save a support file and ask the coordinator before another attempt. |

Choose **Save a support file** when available. Review it and send it privately through the agreed test channel, with what happened and when. If it is unavailable during preparation, send the visible message instead. Keep firmware files and board details private.

## Restore only when instructed

A restore uses its own approved, signed **firmware/restore** folder and the board state that release expects. Wait for the coordinator's instruction for that restore session. Use **Install signed qualification release** again with the restore folder and review its generation before confirming. A restore can have a higher release generation while putting back earlier firmware.

Wait for **Update verified**, all four completed steps and the installed generation assigned for the restore. An uncertain attempt first needs the coordinator's recovery assessment. The disabled **Review recovery** button supplies no shortcut.
