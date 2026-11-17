# Limited Underground Firmware Loader — MeshTrail tester guide

Use the package and USB board assigned to you by your Limited Underground test coordinator. Ask the person who sent the package to confirm its identity and your test scope before you start. This guide covers the approved MeshTrail qualification test. The Display entry is outside this test.

## Install the Loader

1. Confirm you have the approved package for Windows 11 Pro 25H2 x64. Use the package identity and handoff supplied by your test coordinator. If something is missing or Windows cannot verify the installer from the expected publisher, stop and ask for a verified copy.
2. Keep the package folders together. Open `installer/FirmwareLoaderSetup.exe` and follow its setup prompts. The administrator approves setup so the Loader can prepare its protected recovery storage. If setup reports a missing prerequisite or cannot prepare storage, ask your coordinator.
3. Open **Limited Underground Firmware Loader**. Follow the access instructions your coordinator gave you for the exact build. The current board A qualification flow requires administrator rights. Standard-user qualification is a separate acceptance check.

## Install MeshTrail firmware on the assigned board

1. Ask your coordinator to confirm the board and the install and restore folders for this test. Connect that one board with a data-capable USB cable.
2. In the Loader choose **Limited Underground MeshTrail**, then **Install signed qualification release**. Choose the supplied `firmware/install` folder. Keep all four release files together.
3. Read the board and release details. Continue only if they match your assignment. Confirm once and leave the board and cable connected while the Loader writes, checks the bytes and checks startup.
4. Record success only when the Loader says **Update verified**. If it refuses the release, the board disconnects, or the result is uncertain, leave the setup as it is and contact your coordinator before retrying.

## Restore MeshTrail firmware

Use the separately signed `firmware/restore` folder supplied for this exact board and installed release. The coordinator must confirm that the board is in the state the restore expects. Use **Install signed qualification release** again with that folder and confirm the restore details. Wait for **Update verified**. Keep the result and contact the coordinator if the expected startup check fails.

## Get help

On the install panel choose **Save a support file** when it is available. Choose where to save the text file, review it, then send it privately to the Limited Underground test coordinator who assigned the package. Include what happened and when. If the button is unavailable, send the visible step and message instead. Share the support file through the agreed test channel. Keep firmware files and board details private.
