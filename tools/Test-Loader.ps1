[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ArgumentList and exact empty environment restoration require modern PowerShell.
if ($PSVersionTable.PSVersion -lt [version]'7.5') { throw 'Loader validation requires PowerShell 7.5 or newer.' }
if ($env:LUF_UI_ONLY -ceq '1' -or $env:LUF_STORE_ONLY -ceq '1') {
    throw 'Full Loader validation refuses focused selectors. Use the documented direct runner for focused tests.'
}
function Assert-PlainDirectory([string]$Path) {
    if (-not [IO.Path]::IsPathFullyQualified($Path)) { throw 'A fully qualified directory is required.' }
    $item = [IO.DirectoryInfo]::new([IO.Path]::GetFullPath($Path))
    while ($null -ne $item) {
        $item.Refresh()
        if (-not $item.Exists -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Missing or linked directory refused.' }
        $item = $item.Parent
    }
}
function Write-NewJson([string]$Path, $Value) {
    $stream = [IO.FileStream]::new($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($Value | ConvertTo-Json -Depth 12))
        $stream.Write($bytes,0,$bytes.Length); $stream.Flush($true)
    } finally { $stream.Dispose() }
}
function Invoke-LoggedStage([string]$Name, [string[]]$Arguments) {
    Assert-PlainDirectory $script:validationRoot
    Write-NewJson (Join-Path $script:validationRoot "stage-$Name.started.json") @{
        stage=$Name; status='started'; started_utc=[DateTime]::UtcNow.ToString('o')
    }
    $out = $null; $err = $null; $process = $null; $nativeCode = $null; $failure = $null
    $copyCancellation = [Threading.CancellationTokenSource]::new()
    $kind = 'evidence-write'; $secondary = [Collections.Generic.List[string]]::new()
    try {
        $out = [IO.FileStream]::new((Join-Path $script:validationRoot "stage-$Name.stdout.log"), 'CreateNew','Write','Read')
        $err = [IO.FileStream]::new((Join-Path $script:validationRoot "stage-$Name.stderr.log"), 'CreateNew','Write','Read')
        $info = [Diagnostics.ProcessStartInfo]::new()
        $info.FileName = $script:dotnetHost
        $info.UseShellExecute = $false; $info.CreateNoWindow = $true
        $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
        $info.WorkingDirectory = $script:projectRoot
        foreach ($arg in $Arguments) { $info.ArgumentList.Add($arg) }
        $process = [Diagnostics.Process]::new(); $process.StartInfo = $info
        $kind = 'launch'
        if (-not $process.Start()) { throw 'Native process did not start.' }
        $kind = 'evidence-write'
        # Drain raw bytes concurrently: no pipe deadlock or text recoding.
        $copyOut = $process.StandardOutput.BaseStream.CopyToAsync($out,81920,$copyCancellation.Token)
        $copyErr = $process.StandardError.BaseStream.CopyToAsync($err,81920,$copyCancellation.Token)
        while (-not $process.WaitForExit(100)) {
            if ($copyOut.IsFaulted -or $copyOut.IsCanceled -or $copyErr.IsFaulted -or $copyErr.IsCanceled) {
                throw 'Log-copy failure while native process was running.'
            }
        }
        $nativeCode = $process.ExitCode
        if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($copyOut,$copyErr),30000)) {
            throw 'Log drain exceeded 30 seconds after native process exit; evidence incomplete.'
        }
        $out.Flush($true); $err.Flush($true)
        if ($nativeCode -ne 0) { $kind = 'stage'; throw "$Name failed with native exit code $nativeCode." }
    } catch {
        $failure = $_
        $copyCancellation.Cancel()
        if ($null -ne $process) {
            try {
                if (-not $process.HasExited) {
                    $process.Kill($true)
                    if (-not $process.WaitForExit(5000)) { $secondary.Add('Owned process did not exit after log-failure termination.') }
                } elseif ($null -eq $nativeCode) { $nativeCode = $process.ExitCode }
            } catch { $secondary.Add('Owned process state/termination could not be confirmed.') }
        }
        if ($null -ne $nativeCode -and $nativeCode -ne 0 -and $kind -ne 'stage') {
            $secondary.Add($failure.Exception.Message)
            $failure = [Management.Automation.ErrorRecord]::new([InvalidOperationException]::new("$Name failed with native exit code $nativeCode."),'NativeStage','InvalidOperation',$null)
            $kind = 'stage'
        }
    }
    finally {
        foreach ($resource in @($out,$err,$process,$copyCancellation)) {
            if ($null -ne $resource) {
                try { $resource.Dispose() }
                catch {
                    if ($null -eq $failure) { $failure=$_; $kind='evidence-write' }
                    else { $secondary.Add($_.Exception.Message) }
                }
            }
        }
    }
    $record = @{ stage=$Name; finished_utc=[DateTime]::UtcNow.ToString('o'); native_exit_code=$nativeCode;
        status=$(if ($null -eq $failure) {'passed'} else {'failed'});
        failure_kind=$(if ($null -ne $failure) {$kind} else {$null}); secondary_errors=@($secondary.ToArray());
        stdout="stage-$Name.stdout.log"; stderr="stage-$Name.stderr.log" }
    try { Write-NewJson (Join-Path $script:validationRoot "stage-$Name.json") $record }
    catch {
        if ($null -eq $failure) { $failure = $_; $kind='evidence-write' }
        else { [Console]::Error.WriteLine('Secondary failure writing stage receipt: '+$_.Exception.Message) }
    }
    if ($null -ne $failure) { $failure.Exception.Data['loader_failure_kind']=$kind; throw $failure }
}

$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
Assert-PlainDirectory $projectRoot
if ([string]::IsNullOrWhiteSpace($env:TEMP)) { throw 'TEMP is required.' }
$validationParent = [IO.Path]::GetFullPath($env:TEMP)
Assert-PlainDirectory $env:TEMP
$dotnetHost = (Get-Command dotnet -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$sdkInventory = & $dotnetHost --list-sdks
if ($LASTEXITCODE -ne 0) { throw 'Unable to enumerate installed .NET SDKs.' }
$sdk = $sdkInventory | ForEach-Object {
    if ($_ -match '^(8\.0\.\d+) \[(.+)\]$') { [pscustomobject]@{Version=[version]$Matches[1];Root=$Matches[2]} }
} | Sort-Object Version -Descending | Select-Object -First 1
if ($null -eq $sdk) { throw 'Offline Loader validation requires an installed stable .NET 8.0 SDK.' }
$sdkCli = Join-Path (Join-Path $sdk.Root $sdk.Version.ToString()) 'dotnet.dll'
if (-not (Test-Path -LiteralPath $sdkCli -PathType Leaf)) { throw 'Selected SDK CLI unavailable.' }
$runId = 'LimitedUnderground.Loader.Validation.'+[Guid]::NewGuid().ToString('N')
$validationRoot = Join-Path $validationParent $runId
if (Test-Path -LiteralPath $validationRoot) { throw 'Existing validation run refused.' }
[IO.Directory]::CreateDirectory($validationRoot) | Out-Null
Assert-PlainDirectory $validationRoot
Write-NewJson (Join-Path $validationRoot 'ownership.json') @{run_id=$runId; created_utc=[DateTime]::UtcNow.ToString('o')}

$names = @('NUGET_PACKAGES','DOTNET_CLI_HOME','DOTNET_CLI_TELEMETRY_OPTOUT','DOTNET_GENERATE_ASPNET_CERTIFICATE','DOTNET_NOLOGO','DOTNET_SKIP_FIRST_TIME_EXPERIENCE')
$saved = @{}
$savedPresent = @{}
foreach ($name in $names) {
    $saved[$name] = [Environment]::GetEnvironmentVariable($name,'Process')
    $savedPresent[$name] = [Environment]::GetEnvironmentVariables('Process').Contains($name)
}
$failure = $null; $restoreFailures = [Collections.Generic.List[string]]::new()
try {
    $identities = @{}
    foreach ($relative in @('tools/Test-Loader.ps1','tests/LimitedUnderground.FirmwareLoader.Tests/Program.cs','tests/LimitedUnderground.FirmwareLoader.Tests/LoaderUpdateUiTests.cs','tests/LimitedUnderground.FirmwareLoader.Tests/HostOperationStoreTests.cs')) {
        $identities[$relative] = (Get-FileHash -LiteralPath (Join-Path $projectRoot $relative) -Algorithm SHA256).Hash
    }
    Write-NewJson (Join-Path $validationRoot 'started.json') @{
        schema=1; run_id=$runId; status='started'; suite='full'; started_utc=[DateTime]::UtcNow.ToString('o');
        source_root=$projectRoot; source_files=$identities; sdk=$sdk.Version.ToString(); host=$dotnetHost;
        powershell=$PSVersionTable.PSVersion.ToString(); ui_selector=$env:LUF_UI_ONLY; store_selector=$env:LUF_STORE_ONLY;
        retention='retain wrapper-owned outputs; nested fixtures may dispose their own files'
    }
    $env:NUGET_PACKAGES=Join-Path $validationRoot 'packages'; $env:DOTNET_CLI_HOME=Join-Path $validationRoot 'dotnet-home'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'; $env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
    $env:DOTNET_NOLOGO='1'; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
    Invoke-LoggedStage 'app-build' @($sdkCli,'build',(Join-Path $projectRoot 'src/LimitedUnderground.FirmwareLoader/LimitedUnderground.FirmwareLoader.csproj'),'--configuration','Release','--artifacts-path',(Join-Path $validationRoot 'app-artifacts'))
    $testArtifacts=Join-Path $validationRoot 'test-artifacts'
    Invoke-LoggedStage 'test-build' @($sdkCli,'build',(Join-Path $projectRoot 'tests/LimitedUnderground.FirmwareLoader.Tests/LimitedUnderground.FirmwareLoader.Tests.csproj'),'--configuration','Release','--artifacts-path',$testArtifacts)
    $testAssembly=Join-Path $testArtifacts 'bin/LimitedUnderground.FirmwareLoader.Tests/release/LimitedUnderground.FirmwareLoader.Tests.dll'
    if (-not (Test-Path -LiteralPath $testAssembly -PathType Leaf)) { throw 'Built test assembly was not found.' }
    Invoke-LoggedStage 'tests' @($testAssembly,$projectRoot,(Join-Path $validationRoot 'ui-renderings'))
} catch { $failure = $_ }
finally {
    foreach ($name in $names) {
        try {
            if ($savedPresent[$name]) { [Environment]::SetEnvironmentVariable($name,$saved[$name],'Process') }
            else { Remove-Item -LiteralPath ('Env:'+$name) -ErrorAction SilentlyContinue }
            if ([Environment]::GetEnvironmentVariables('Process').Contains($name) -ne $savedPresent[$name]) { throw 'Restored presence differs.' }
            if ([Environment]::GetEnvironmentVariable($name,'Process') -cne $saved[$name]) { throw 'Restored value differs.' }
        } catch { $restoreFailures.Add($name) }
    }
}
if ($restoreFailures.Count -gt 0) {
    [Console]::Error.WriteLine('Environment restoration failed for: '+($restoreFailures -join ', '))
    if ($null -eq $failure) {
        $failure = [Management.Automation.ErrorRecord]::new([InvalidOperationException]::new('Caller environment restoration failed.'),'EnvironmentRestore','InvalidOperation',$null)
        $failure.Exception.Data['loader_failure_kind']='environment-restoration'
    }
}
try {
    Assert-PlainDirectory $validationRoot
    Write-NewJson (Join-Path $validationRoot 'final.json') @{
        schema=1;run_id=$runId;suite='full';finished_utc=[DateTime]::UtcNow.ToString('o');
        status=$(if ($null -eq $failure) {'passed'} else {'failed'});
        failure_kind=$(if ($null -ne $failure) { if ($failure.Exception.Data.Contains('loader_failure_kind')) {$failure.Exception.Data['loader_failure_kind']} else {'preparation-or-evidence'} } else {$null});
        environment_restored=($restoreFailures.Count -eq 0); artifacts_retained=$true;
        error=$(if ($null -ne $failure) {$failure.Exception.Message} else {$null})
    }
} catch {
    [Console]::Error.WriteLine('Final evidence incomplete: '+$_.Exception.Message)
    if ($null -eq $failure) { $failure = $_ }
}
Write-Host ('Loader validation evidence retained at: '+$validationRoot)
if ($null -ne $failure) { throw $failure }
Write-Host 'Shared firmware loader FULL offline validation passed; evidence retained.'
