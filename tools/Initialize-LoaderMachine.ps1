#requires -Version 7.5
[CmdletBinding()]
param([Parameter(Mandatory)][string] $InstalledLoader)

$ErrorActionPreference = 'Stop'
$exe = Get-Item -LiteralPath $InstalledLoader -ErrorAction SilentlyContinue
if (!$exe -or $exe.PSIsContainer -or $exe.Name -cne 'LimitedUnderground.FirmwareLoader.exe' -or
    ($exe.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw 'Choose the installed Firmware Loader application.'
}
$start = [Diagnostics.ProcessStartInfo]::new($exe.FullName)
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.WorkingDirectory = $exe.DirectoryName
$start.ArgumentList.Add('--setup-machine-store')
try { $process = [Diagnostics.Process]::Start($start) }
catch { throw 'Firmware Loader setup could not start.' }
if (!$process) { throw 'Firmware Loader setup could not start.' }
try {
    $process.WaitForExit()
    switch ($process.ExitCode) {
        0 { Write-Output 'Firmware Loader recovery storage is ready.' }
        20 { throw 'Run Firmware Loader setup as an administrator.' }
        21 { throw 'The Firmware Loader package is incomplete or damaged. Use a verified copy.' }
        22 { throw 'Firmware Loader could not prepare recovery storage. Ask an administrator to check it.' }
        default { throw 'Firmware Loader setup stopped. Ask an administrator to check the installation.' }
    }
}
finally { $process.Dispose() }
