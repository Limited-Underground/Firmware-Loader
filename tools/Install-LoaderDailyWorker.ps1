#requires -Version 7.5
#requires -RunAsAdministrator
[CmdletBinding(DefaultParameterSetName = 'Install', PositionalBinding = $false)]
param(
    [Parameter(Mandatory)][string] $InstalledLoader,
    [Parameter(Mandatory, ParameterSetName = 'Install')][string] $AssignmentFile,
    [Parameter(ParameterSetName = 'Install')][switch] $ReplaceAssignment,
    [Parameter(Mandatory, ParameterSetName = 'Uninstall')][switch] $Uninstall
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Format-SetupDetail($Failure) {
    # One redacted line for the installer: the failing step, the message and the Windows result code.
    $inner = $Failure
    while ($inner.InnerException) { $inner = $inner.InnerException }
    $text = "$($script:setupStep): $($inner.Message)"
    if ($inner -is [Runtime.InteropServices.COMException] -or $inner -is [ComponentModel.Win32Exception] -or
        $inner -is [IO.IOException] -or $inner -is [UnauthorizedAccessException]) {
        $text += (' (0x{0:X8})' -f $inner.HResult)
    }
    $text = [regex]::Replace($text, '[\x00-\x1F\x7F]+', ' ')
    $text = [regex]::Replace($text, '(?i)[0-9a-f]{64,}', '[redacted SHA-256]')
    $text = [regex]::Replace($text, 'S-1-5-21(-[0-9]+){3,4}', '[user SID]')
    $text = $text.Trim()
    if ($text.Length -gt 300) { $text = $text.Substring(0, 300) }
    return 'LOADER-SETUP-DETAIL: ' + $text
}
$script:setupStep = 'checking setup inputs'
trap {
    try { [Console]::Error.WriteLine((Format-SetupDetail $_.Exception)) } catch { }
    break
}

Import-Module (Join-Path $PSScriptRoot 'LoaderDailyProvisioning.psm1') -Force

$administrators = [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544')
$system = [Security.Principal.SecurityIdentifier]::new('S-1-5-18')
$sidType = [Security.Principal.SecurityIdentifier]
$utf8 = [Text.UTF8Encoding]::new($false,$true)
$taskName = 'LimitedUnderground.FirmwareLoader.DailyWorker'
$taskArgument = '--daily-worker'

function Get-PlainPathItem([string] $Path) {
    $entry = Get-Item -LiteralPath $Path -Force
    if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'The Firmware Loader setup path is redirected.' }
    return $entry
}

function Test-SetupItemPresent([string] $Path) {
    try {
        $null = [IO.File]::GetAttributes($Path)
        return $true
    }
    catch {
        $failure = $_.Exception
        while ($failure.InnerException) { $failure = $failure.InnerException }
        if ($failure -is [IO.FileNotFoundException] -or $failure -is [IO.DirectoryNotFoundException]) {
            return $false
        }
        throw 'Firmware Loader setup could not inspect preserved storage. Ask the installation administrator to check it.'
    }
}

function Assert-LocalPath([string] $Path) {
    if ($Path -notmatch '\A[A-Za-z]:\\' -or $Path.Substring(2).Contains(':') -or
        $Path -ine [IO.Path]::GetFullPath($Path)) { throw 'Use a fixed, absolute local Firmware Loader setup path.' }
    for ($item = Get-PlainPathItem $Path; $null -ne $item;) {
        $parent = if ($item -is [IO.DirectoryInfo]) { $item.Parent } else { $item.Directory }
        $item = if ($parent) { Get-PlainPathItem $parent.FullName } else { $null }
    }
}

function Assert-NoOrdinaryWrites($Item, [switch] $AncestorOnly) {
    $acl = [IO.FileSystemAclExtensions]::GetAccessControl($Item, 'Owner,Access')
    Assert-LoaderSetupPathAcl -Acl $acl -AncestorOnly:$AncestorOnly
}

function Get-ProtectedLoader([string] $Path) {
    Assert-LocalPath $Path
    $exe = Get-PlainPathItem $Path
    $programFiles = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
    $relative = [IO.Path]::GetRelativePath($programFiles,$exe.FullName)
    if ($exe -isnot [IO.FileInfo] -or $exe.Name -cne 'LimitedUnderground.FirmwareLoader.exe' -or
        [IO.Path]::IsPathRooted($relative) -or $relative.StartsWith('..\',[StringComparison]::Ordinal) -or
        $exe.Directory.FullName -ieq $programFiles) {
        throw 'Install Firmware Loader in its own protected Program Files subdirectory before setting up the worker.'
    }
    for ($folder = $exe.Directory.Parent; $folder; $folder = $folder.Parent) {
        Assert-NoOrdinaryWrites (Get-PlainPathItem $folder.FullName) -AncestorOnly
    }
    $entries = [Collections.Generic.Stack[IO.FileSystemInfo]]::new()
    $entries.Push($exe.Directory)
    while ($entries.Count -ne 0) {
        $entry = Get-PlainPathItem ($entries.Pop().FullName)
        Assert-NoOrdinaryWrites $entry
        if ($entry -is [IO.DirectoryInfo]) {
            foreach ($child in $entry.EnumerateFileSystemInfos()) { $entries.Push($child) }
        }
    }
    return $exe
}

function New-StoreDescriptor([bool] $Directory) {
    if ($Directory) {
        $descriptor = [Security.AccessControl.DirectorySecurity]::new()
        $inheritance = [Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit'
    } else {
        $descriptor = [Security.AccessControl.FileSecurity]::new()
        $inheritance = [Security.AccessControl.InheritanceFlags]::None
    }
    $descriptor.SetOwner($administrators)
    $descriptor.SetAccessRuleProtection($true,$false)
    foreach ($sid in $administrators,$system) {
        $descriptor.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
            $sid,'FullControl',$inheritance,'None','Allow'))
    }
    return $descriptor
}

function Assert-StoreItem([string] $Path, [bool] $Directory) {
    $item = Get-PlainPathItem $Path
    if (($item -is [IO.DirectoryInfo]) -ne $Directory) { throw 'Protected Firmware Loader storage has an unexpected item.' }
    $acl = [IO.FileSystemAclExtensions]::GetAccessControl($item,'Owner,Access')
    $inheritance = if ($Directory) { 'ContainerInherit, ObjectInherit' } else { 'None' }
    $rules = @($acl.GetAccessRules($true,$true,$sidType))
    if (!$acl.AreAccessRulesProtected -or $acl.GetOwner($sidType) -ne $administrators -or $rules.Count -ne 2) {
        throw 'The protected Firmware Loader storage permissions do not match setup.'
    }
    foreach ($sid in $administrators,$system) {
        $exact = @($rules | Where-Object {
            $_.IdentityReference -eq $sid -and !$_.IsInherited -and $_.AccessControlType -eq 'Allow' -and
            $_.FileSystemRights -eq 'FullControl' -and $_.PropagationFlags -eq 'None' -and
            $_.InheritanceFlags -eq [Security.AccessControl.InheritanceFlags]$inheritance
        })
        if ($exact.Count -ne 1) { throw 'The protected Firmware Loader storage permissions do not match setup.' }
    }
}

function Read-BoundedText([string] $Path, [int] $Maximum) {
    $file = [IO.File]::Open($Path,'Open','Read','Read')
    try {
        if ($file.Length -lt 1 -or $file.Length -gt $Maximum) { throw 'A Firmware Loader setup record has an invalid size.' }
        $bytes = [byte[]]::new([int]$file.Length)
        $file.ReadExactly($bytes)
        return $utf8.GetString($bytes)
    }
    finally { $file.Dispose() }
}

function Read-ProtectedText([string] $Path, [int] $Maximum = 16384) {
    if (!(Test-SetupItemPresent $Path)) { return $null }
    Assert-StoreItem $Path $false
    return Read-BoundedText $Path $Maximum
}

function Write-ProtectedText([string] $Path, [string] $Text) {
    $security = New-StoreDescriptor $false
    $file = [IO.FileSystemAclExtensions]::Create([IO.FileInfo]::new($Path),'CreateNew','FullControl','None',4096,'WriteThrough',$security)
    try {
        $bytes = $utf8.GetBytes($Text)
        $file.Write($bytes,0,$bytes.Length)
        $file.Flush($true)
    }
    finally { $file.Dispose() }
    Assert-StoreItem $Path $false
}

function Write-SetupRecord([string] $Path, [string] $Text, [switch] $Replace) {
    $unique = [Guid]::NewGuid().ToString('N')
    $temporary = Join-Path $dailyRoot "setup-stage-$unique"
    Write-ProtectedText $temporary $Text
    if ($Replace) {
        [IO.File]::Replace($temporary,$Path,(Join-Path $dailyRoot "prior-assignment-$unique.json"))
    } else {
        [IO.File]::Move($temporary,$Path)
    }
    Assert-StoreItem $Path $false
}

function Test-LocalUserSid([string] $Sid) {
    # A syntactically valid SID can identify a group. Ask Windows for the account type.
    if (!('LimitedUnderground.Setup.UserSidLookup' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace LimitedUnderground.Setup;
public static class UserSidLookup
{
    [DllImport("advapi32.dll", EntryPoint = "LookupAccountSidW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Lookup(string? system, byte[] sid, StringBuilder name, ref uint nameSize,
        StringBuilder domain, ref uint domainSize, out int type);

    public static bool IsUser(byte[] sid)
    {
        const int SidTypeUser = 1;
        var name = new StringBuilder(257);
        var domain = new StringBuilder(257);
        uint nameSize = (uint)name.Capacity, domainSize = (uint)domain.Capacity;
        return Lookup(null, sid, name, ref nameSize, domain, ref domainSize, out int type) && type == SidTypeUser;
    }
}
'@ -CompilerOptions '/nullable:enable'
    }
    $identity = [Security.Principal.SecurityIdentifier]::new($Sid)
    $bytes = [byte[]]::new($identity.BinaryLength)
    $identity.GetBinaryForm($bytes,0)
    return [LimitedUnderground.Setup.UserSidLookup]::IsUser($bytes)
}

function Get-SetupAssignment {
    Assert-LocalPath $AssignmentFile
    $configFile = Get-PlainPathItem $AssignmentFile
    if ($configFile -isnot [IO.FileInfo]) { throw 'Supply an administrator-owned board assignment JSON file.' }
    Assert-NoOrdinaryWrites $configFile
    Assert-NoOrdinaryWrites (Get-PlainPathItem $configFile.Directory.FullName)
    for ($folder = $configFile.Directory.Parent; $folder; $folder = $folder.Parent) {
        Assert-NoOrdinaryWrites (Get-PlainPathItem $folder.FullName) -AncestorOnly
    }
    return ConvertTo-LoaderDailyAssignment -Json (Read-BoundedText $configFile.FullName 16384) -ValidateOperator { param($sid) Test-LocalUserSid $sid }
}

function Get-WorkerTaskSDDL([string] $Sid) {
    return "O:BAD:P(A;;FA;;;SY)(A;;FA;;;BA)(A;;FRFX;;;$Sid)"
}

function Get-RegisteredWorker([string] $Sid) {
    try { $task = $taskFolder.GetTask($taskName) }
    catch {
        $failure = $_.Exception
        while ($failure.InnerException) { $failure = $failure.InnerException }
        if ($failure.HResult -eq -2147024894) { return $null } # HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND)
        throw ('Firmware Loader setup could not inspect Task Scheduler (0x{0:X8}).' -f $failure.HResult)
    }
    if (!$Sid) { throw 'An existing worker task has no matching protected assignment.' }
    $def = $task.Definition
    $principal = $def.Principal
    try { $principalSid = [Security.Principal.SecurityIdentifier]::new($principal.UserId).Value }
    catch {
        $principalSid = [Security.Principal.NTAccount]::new($principal.UserId).Translate($sidType).Value
    }
    if ($principalSid -cne $system.Value -or $principal.LogonType -ne 5 -or $def.Triggers.Count -ne 0 -or
        $def.Actions.Count -ne 1 -or $def.Settings.MultipleInstances -ne 2 -or !$def.Settings.AllowDemandStart -or
        $def.Settings.AllowHardTerminate -or $def.Settings.ExecutionTimeLimit -cne 'PT0S' -or $def.Settings.RestartCount -ne 0 -or
        $def.Settings.DisallowStartIfOnBatteries -or $def.Settings.StopIfGoingOnBatteries -or $def.Settings.RunOnlyIfIdle -or
        $def.Settings.IdleSettings.StopOnIdleEnd -or $def.Settings.IdleSettings.RestartOnIdle -or
        $def.Settings.RunOnlyIfNetworkAvailable -or $def.Settings.StartWhenAvailable -or $def.Settings.WakeToRun -or
        ![string]::IsNullOrEmpty($def.Settings.DeleteExpiredTaskAfter)) {
        throw 'The existing worker task does not match the protected setup.'
    }
    $action = $def.Actions.Item(1)
    if ($action.Type -ne 0 -or $action.Path -ine $exe.FullName -or $action.Arguments -cne $taskArgument -or
        $action.WorkingDirectory -ine $exe.DirectoryName) { throw 'The existing worker task does not match the installed Firmware Loader.' }
    $actual = [Security.AccessControl.RawSecurityDescriptor]::new($task.GetSecurityDescriptor(5))
    $expected = [Security.AccessControl.RawSecurityDescriptor]::new((Get-WorkerTaskSDDL $Sid))
    if ($actual.Owner -ne $expected.Owner -or $actual.DiscretionaryAcl.Count -ne 3 -or
        !(($actual.ControlFlags -band [Security.AccessControl.ControlFlags]::DiscretionaryAclProtected))) {
        throw 'The worker task permissions do not match the assigned Windows user.'
    }
    foreach ($entry in $expected.DiscretionaryAcl) {
        $matching = @($actual.DiscretionaryAcl | Where-Object {
            $_ -is [Security.AccessControl.CommonAce] -and $_.AceType -eq $entry.AceType -and
            $_.AceFlags -eq $entry.AceFlags -and $_.AccessMask -eq $entry.AccessMask -and
            $_.SecurityIdentifier -eq $entry.SecurityIdentifier
        })
        if ($matching.Count -ne 1) { throw 'The worker task permissions do not match the assigned Windows user.' }
    }
    return $task
}

function Stop-WorkerLaunches($Task) {
    $Task.Enabled = $false
    if ($Task.Enabled -or $Task.GetInstances(0).Count -ne 0) {
        throw 'Wait for the current Firmware Loader worker to finish, then retry the same setup.'
    }
}

function New-WorkerTaskDefinition {
    $definition = $scheduler.NewTask(0)
    $definition.RegistrationInfo.Description = 'Limited Underground Firmware Loader qualification worker'
    $definition.Principal.UserId = $system.Value
    $definition.Principal.LogonType = 5 # TASK_LOGON_SERVICE_ACCOUNT
    $definition.Settings.Enabled = $false
    $definition.Settings.AllowDemandStart = $true
    $definition.Settings.AllowHardTerminate = $false
    $definition.Settings.DisallowStartIfOnBatteries = $false
    $definition.Settings.StopIfGoingOnBatteries = $false
    $definition.Settings.RunOnlyIfIdle = $false
    $definition.Settings.IdleSettings.StopOnIdleEnd = $false
    $definition.Settings.IdleSettings.RestartOnIdle = $false
    $definition.Settings.RunOnlyIfNetworkAvailable = $false
    $definition.Settings.StartWhenAvailable = $false
    $definition.Settings.WakeToRun = $false
    # DeleteExpiredTaskAfter stays unset: the worker has no triggers and so never expires.
    # Windows refuses any value without a trigger end boundary (0x80041319, EndBoundary).
    # The engine owns phase deadlines and holds the recovery lease through native quiescence.
    $definition.Settings.ExecutionTimeLimit = 'PT0S'
    $definition.Settings.MultipleInstances = 2 # TASK_INSTANCES_IGNORE_NEW
    $definition.Settings.Hidden = $true
    $action = $definition.Actions.Create(0) # TASK_ACTION_EXEC
    $action.Path = $exe.FullName
    $action.Arguments = $taskArgument
    $action.WorkingDirectory = $exe.DirectoryName
    return $definition
}

function Register-DisabledWorker([string] $Sid) {
    $definition = New-WorkerTaskDefinition
    # TASK_CREATE | TASK_DONT_ADD_PRINCIPAL_ACE. Refuse a racing or foreign task.
    $null = $taskFolder.RegisterTaskDefinition($taskName,$definition,18,$system.Value,$null,5,(Get-WorkerTaskSDDL $Sid))
    $registered = Get-RegisteredWorker $Sid
    if (!$registered -or $registered.Enabled) { throw 'Firmware Loader setup could not confirm the disabled worker registration.' }
}

function Read-ProvisioningState {
    if (Test-SetupItemPresent $machineRoot) { Assert-StoreItem $machineRoot $true }
    if (Test-SetupItemPresent $dailyRoot) { Assert-StoreItem $dailyRoot $true }
    $active = Read-ProtectedText $activePath
    $pending = Read-ProtectedText $pendingPath
    if (!$active -and !$pending -and (Test-SetupItemPresent $clockPath)) {
        throw 'The existing Firmware Loader admission clock has no assignment record.'
    }
    return @{ Active = $active; Pending = $pending }
}

function Initialize-ProtectedStore {
    # Existing setup validates the packaged runtime and creates or verifies the protected journal and lease.
    $null = & (Join-Path $PSScriptRoot 'Initialize-LoaderMachine.ps1') -InstalledLoader $exe.FullName
    Assert-StoreItem $machineRoot $true
    if (!(Test-SetupItemPresent $dailyRoot)) {
        [IO.FileSystemAclExtensions]::Create([IO.DirectoryInfo]::new($dailyRoot),(New-StoreDescriptor $true))
    }
    Assert-StoreItem $dailyRoot $true
}

function Initialize-DailyClock([bool] $Fresh) {
    $clock = Read-ProtectedText $clockPath 128
    if ($clock) {
        $fields = $clock.Split([char]10)
        if ($fields.Count -ne 4 -or $fields[0] -cne 'T-0043' -or $fields[3] -cne '' -or
            $fields[1] -cnotmatch '\A[0-9]+\z' -or $fields[2] -cnotmatch '\A[0-9]+\z' -or
            ![long]::TryParse($fields[1],[ref]$null) -or ![ulong]::TryParse($fields[2],[ref]$null)) {
            throw 'The protected Firmware Loader admission clock is invalid.'
        }
        return
    }
    if (!$Fresh) { throw 'The preserved Firmware Loader admission clock is missing.' }
    Write-SetupRecord $clockPath ("T-0043" + [char]10 + "0" + [char]10 + "0" + [char]10)
}

function Hold-RecoveryLease([bool] $Changing) {
    $leasePath = Join-Path $journalRoot 'operation.lock'
    $journalPath = Join-Path $journalRoot 'uncertainty.json'
    Assert-StoreItem $leasePath $false
    try { $transaction.Lease = [IO.File]::Open($leasePath,'Open','ReadWrite','None') }
    catch { throw 'Wait for the current Firmware Loader operation to finish, then retry the same setup.' }
    if ($Changing) {
        $journal = Read-ProtectedText $journalPath 1024 | ConvertFrom-Json -AsHashtable
        if (!$journal -or $journal.state -cnotin 'idle','complete') {
            throw 'The protected recovery journal requires review before changing the approved boards.'
        }
    }
}

function Lock-Setup {
    # The task installer is shared across elevated sessions. A protected mutex keeps concurrent
    # installer invocations from observing and publishing different halves of one transaction.
    $descriptor = [Security.AccessControl.MutexSecurity]::new()
    $descriptor.SetOwner($administrators)
    $descriptor.SetAccessRuleProtection($true,$false)
    foreach ($sid in $administrators,$system) {
        $descriptor.AddAccessRule([Security.AccessControl.MutexAccessRule]::new($sid,'FullControl','Allow'))
    }
    $created = $false
    $mutex = [Threading.MutexAcl]::Create($false,'Global\LimitedUnderground.FirmwareLoader.DailySetup.V1',[ref]$created,$descriptor)
    try {
        $acl = [Threading.ThreadingAclExtensions]::GetAccessControl($mutex)
        $rules = @($acl.GetAccessRules($true,$true,$sidType))
        if (!$acl.AreAccessRulesProtected -or $acl.GetOwner($sidType) -ne $administrators -or $rules.Count -ne 2) {
            throw 'Firmware Loader setup could not confirm exclusive installer ownership.'
        }
        foreach ($sid in $administrators,$system) {
            if (@($rules | Where-Object { $_.IdentityReference -eq $sid -and !$_.IsInherited -and
                $_.AccessControlType -eq 'Allow' -and $_.MutexRights -eq 'FullControl' }).Count -ne 1) {
                throw 'Firmware Loader setup could not confirm exclusive installer ownership.'
            }
        }
        try {
            if (!$mutex.WaitOne(0)) { throw 'Another Firmware Loader setup is in progress.' }
        } catch [Threading.AbandonedMutexException] {
            # The protected pending assignment holds any unfinished work. The mutex is now ours.
        }
        return ,$mutex
    }
    catch { $mutex.Dispose(); throw }
}

if (!$IsWindows -or ![Environment]::Is64BitProcess) { throw 'Firmware Loader setup requires 64-bit Windows PowerShell 7.' }
$script:setupStep = 'checking the installed Loader'
$exe = Get-ProtectedLoader $InstalledLoader
$script:setupStep = 'reading the board assignment'
$assignment = if (!$Uninstall) { Get-SetupAssignment } else { $null }
$script:setupStep = 'locking setup'
$mutex = Lock-Setup
$transaction = @{ Lease = $null }
try {
    $script:setupStep = 'checking ProgramData'
    $commonData = [Environment]::GetFolderPath([Environment+SpecialFolder]::CommonApplicationData)
    Assert-LocalPath $commonData
    Assert-NoOrdinaryWrites (Get-PlainPathItem $commonData) -AncestorOnly
    $legacyQualification = Join-Path $commonData 'LimitedUnderground.FirmwareLoader.Qualification'
    if (!$Uninstall -and (Test-SetupItemPresent $legacyQualification)) {
        throw 'An earlier board A qualification record needs administrator-reviewed migration before daily worker setup.'
    }
    $machineRoot = Join-Path $commonData 'LimitedUnderground.FirmwareLoader'
    $journalRoot = Join-Path $machineRoot 'recovery\v1\opentrail'
    $dailyRoot = Join-Path $machineRoot 'daily-v1'
    $activePath = Join-Path $dailyRoot 'assignment.json'
    $pendingPath = Join-Path $dailyRoot 'assignment.pending.json'
    $clockPath = Join-Path $dailyRoot 'production-admission.state'

    $script:setupStep = 'connecting to Task Scheduler'
    $scheduler = New-Object -ComObject 'Schedule.Service'
    $scheduler.Connect()
    $taskFolder = $scheduler.GetFolder('\')
    $backend = @{
        ReadState = { $script:setupStep = 'reading preserved state'; Read-ProvisioningState }
        InspectTask = { param($sid) $script:setupStep = 'inspecting the worker task'; $task = Get-RegisteredWorker $sid; return [bool]($task -and $task.Enabled) }
        InitializeStore = { $script:setupStep = 'initializing the machine store'; Initialize-ProtectedStore }
        EnsureClock = { param($fresh) $script:setupStep = 'writing the admission clock'; Initialize-DailyClock $fresh }
        Begin = {
            param($candidate, $prior)
            $script:setupStep = 'recording the pending assignment'
            if ((Read-ProtectedText $activePath) -cne $prior) { throw 'The preserved Firmware Loader assignment changed during setup.' }
            Write-SetupRecord $pendingPath $candidate
        }
        StageTask = {
            param($sid, $change)
            $script:setupStep = 'registering the worker task'
            $task = Get-RegisteredWorker $sid
            if ($task) { Stop-WorkerLaunches $task } else { Register-DisabledWorker $sid }
            Hold-RecoveryLease $change
        }
        Publish = {
            param($candidate, $prior)
            $script:setupStep = 'publishing the assignment'
            if ((Read-ProtectedText $pendingPath) -cne $candidate -or (Read-ProtectedText $activePath) -cne $prior) {
                throw 'The preserved Firmware Loader assignment changed during setup.'
            }
            if ($candidate -cne $prior) { Write-SetupRecord $activePath $candidate -Replace:([bool]$prior) }
        }
        EnableTask = {
            param($sid)
            $script:setupStep = 'enabling the worker task'
            $task = Get-RegisteredWorker $sid
            if (!$task) { throw 'The protected Firmware Loader worker task disappeared during setup.' }
            $task.Enabled = $true
            if (!(Get-RegisteredWorker $sid).Enabled) { throw 'Firmware Loader setup could not enable its worker task.' }
        }
        Complete = {
            param($candidate)
            $script:setupStep = 'confirming the assignment'
            if ((Read-ProtectedText $activePath) -cne $candidate -or (Read-ProtectedText $pendingPath) -cne $candidate) {
                throw 'Firmware Loader setup could not confirm the committed assignment.'
            }
            [IO.File]::Move($pendingPath,(Join-Path $dailyRoot ('applied-assignment-' + [Guid]::NewGuid().ToString('N') + '.json')))
        }
        RemoveTask = {
            param($sid)
            $script:setupStep = 'removing the worker task'
            $task = Get-RegisteredWorker $sid
            if ($task) {
                Stop-WorkerLaunches $task
                $taskFolder.DeleteTask($taskName,0)
                if (Get-RegisteredWorker $sid) { throw 'Firmware Loader setup could not confirm worker task removal.' }
            }
        }
    }
    if ($Uninstall) { Invoke-LoaderDailyProvisioning -Uninstall -Backend $backend }
    else { Invoke-LoaderDailyProvisioning -Assignment $assignment -ReplaceAssignment:$ReplaceAssignment -Backend $backend }
}
finally {
    if ($transaction.Lease) { $transaction.Lease.Dispose() }
    $mutex.ReleaseMutex()
    $mutex.Dispose()
}
