# Packaging the Firmware Loader flash runtime

The Firmware Loader checks the packaged flash runtime and the separate flash helper before opening a MeshTrail qualification release. If the package is missing or damaged, ask for a verified copy.

## Build a package from approved files

The [input manifest](../tools/flash-runtime-inputs.json) lists the proposed Windows x64 CPython 3.14 archives. The release owner records the approved byte count and uppercase SHA-256 for each input after verifying its provenance. The staged tree is usable only after all pins are filled. The owner still needs to decide how to obtain an esptool 5.3.1 wheel. PyPI publishes a source archive for that version.

1. Put the approved archives in one local folder. Run [Package-LoaderRuntime.ps1](../tools/Package-LoaderRuntime.ps1) with `-ArchiveDirectory`, an unused `-StageDirectory`, and `-InputManifest` pointing to the recorded manifest. The script checks every input before extraction and writes the exact output hashes to `runtime-files.json`. It installs the Python modules from the wheels without running their scripts.
2. Build or publish the Loader project with `-p:LoaderRuntimeStage=<stage-directory>`. The build embeds the output hashes in the Loader and copies `flash-runtime` beside it. Use a new output directory. Include the entire application output in the installer payload, including its existing `flash-helper` folder.
3. Install that verified payload into an administrator-managed program directory, such as Program Files. Preserve the bundled license files. At the end of setup in the existing elevated installer process, call [Initialize-LoaderMachine.ps1](../tools/Initialize-LoaderMachine.ps1) with `-InstalledLoader` pointing at the installed executable. This validates the runtime and checks or enrolls the machine recovery store. It preserves an existing valid store and stops if the store is incomplete or damaged.

PowerShell 7.5 or later is required for the packaging and setup scripts. The packaging script consumes local files only. It does not run Python or use an existing Python installation.

The current board A qualification install still uses the administrator-only journal, board pin and admission clock. One-time setup prepares the journal and lease. The host-tested qualification handler uses an existing board assignment and protected operation storage. It validates release bytes inside the trusted operation boundary. Standard-user installs still need the installed worker, local caller authentication and reviewed board assignment from setup.

## Host checks

Run [Test-LoaderRuntimePackaging.ps1](../tools/Test-LoaderRuntimePackaging.ps1) with `-OutputDirectory` set to an unused scratch path. It makes archive fixtures locally and checks staging and refusal cases without running any runtime binary. [Test-Loader.ps1](../tools/Test-Loader.ps1) includes the runtime validation and setup sequencing groups. Package import and standard-user install checks require the approved real inputs and a Windows test installation.
