#requires -Version 7.5
[CmdletBinding()]
param([string] $EvidenceRoot)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'LoaderValidationEvidence.ps1')
$evidence = New-LoaderValidationRun $EvidenceRoot 'LimitedUnderground.Loader.DailyProvisioning'
$module = Join-Path $PSScriptRoot 'LoaderDailyProvisioning.psm1'
if (Test-Path -LiteralPath $module) { Import-Module $module -Force }

$operator = 'S-1-5-21-101-102-103-104'
$acceptOperator = { param($candidate) $candidate -ceq 'S-1-5-21-101-102-103-104' }
$board = '{"fingerprint":"' + ('A' * 64) + '","installManifestSha256":"' + ('B' * 64) + '","restoreManifestSha256":"' + ('C' * 64) + '"}'
$board2 = '{"fingerprint":"' + ('D' * 64) + '","installManifestSha256":"' + ('E' * 64) + '","restoreManifestSha256":"' + ('F' * 64) + '"}'
function Assignment([string] $Boards, [string] $Sid = $operator) {
    '{"schema":1,"operatorSid":"' + $Sid + '","boards":[' + $Boards + ']}'
}
function Normalize([string] $Text) {
    ConvertTo-LoaderDailyAssignment -Json $Text -ValidateOperator $acceptOperator
}
function Assert([bool] $Ok, [string] $Reason) { if (!$Ok) { throw $Reason } }
function Refuse([scriptblock] $Action, [string] $Reason) {
    try { & $Action | Out-Null }
    catch {
        if ($_.Exception.Message -clike $Reason) { return }
        throw
    }
    throw "Expected refusal: $Reason"
}

function FakeBackend([hashtable] $State) {
    @{
        ReadState = { @{ Active = $State.Active; Pending = $State.Pending } }.GetNewClosure()
        InspectTask = { if ($State.ForeignTask) { throw 'Conflicting worker task.' }; return $State.Registered -and $State.Enabled }.GetNewClosure()
        InitializeStore = {
            if ($State.PartialStore) { throw 'The preserved machine store requires administrator review.' }
            $State.Prepared++
            if ($State.InterruptStore) {
                $State.PartialStore = $true
                throw 'Simulated interruption before store enrollment completed.'
            }
        }.GetNewClosure()
        EnsureClock = { param($Fresh) if ($Fresh) { $State.Clock = 1 } elseif ($State.Clock -eq 0) { throw 'Preserved clock unavailable.' } }.GetNewClosure()
        Begin = { param($Assignment) $State.Pending = $Assignment }.GetNewClosure()
        StageTask = { $State.Registrations++; $State.Registered = $true; $State.Enabled = $false; if ($State.Interrupt) { throw 'Simulated interruption.' } }.GetNewClosure()
        Publish = { param($Assignment) $State.Active = $Assignment }.GetNewClosure()
        EnableTask = { $State.Enabled = $true }.GetNewClosure()
        Complete = { $State.Pending = $null }.GetNewClosure()
        RemoveTask = { $State.Registered = $false; $State.Enabled = $false }.GetNewClosure()
    }
}
function New-State {
    @{Active = $null; Pending = $null; Prepared = 0; Clock = 0; Registrations = 0; InterruptStore = $false; PartialStore = $false; Registered = $false; Enabled = $false; Interrupt = $false; ForeignTask = $false}
}

$cases = [ordered]@{
    'missing list' = { Refuse { Normalize ('{"schema":1,"operatorSid":"' + $operator + '"}') } 'The board assignment*' }
    'empty list' = { Refuse { Normalize (Assignment '') } 'The board assignment*' }
    'malformed fingerprint' = {
        Refuse { Normalize (Assignment ($board.Replace(('A' * 64), ('a' * 64)))) } 'The board assignment*'
        Refuse { Normalize (Assignment ($board.Replace(('A' * 64), ('0' * 64)))) } 'The board assignment*'
    }
    'duplicate fingerprint and shared release digest' = {
        Refuse { Normalize (Assignment "$board,$board") } 'The board assignment*'
        Refuse { Normalize (Assignment ($board + ',' + $board2.Replace(('E' * 64), ('B' * 64)))) } 'The board assignment*'
    }
    'duplicate JSON member' = {
        Refuse { Normalize ((Assignment $board).Replace('"schema":1', '"schema":1,"schema":1')) } 'The board assignment*'
    }
    'wrong user or missing user' = {
        Refuse { Normalize (Assignment $board 'S-1-5-21-101-102-103-105') } 'The Windows user*'
        Refuse { Normalize ('{"schema":1,"boards":[' + $board + ']}') } 'The Windows user*'
    }
    'unsupported or malformed user SID' = {
        foreach ($invalid in 'S-1-12-1-101-102-103-104','S-1-5-21-101-102-103-0','S-1-5-21-101-102-103-4294967296','S-1-5-21-001-102-103-104') {
            Refuse { ConvertTo-LoaderDailyAssignment -Json (Assignment $board $invalid) -ValidateOperator { $true } } 'The Windows user*'
        }
    }
    'wrong shape' = {
        Refuse { Normalize (Assignment $board2).Replace('"schema":1','"schema":1.0') } 'The board assignment*'
        Refuse { Normalize ((Assignment $board2).Replace('{"fingerprint"', '{"extra":true,"fingerprint"')) } 'The board assignment*'
    }
    'same list and explicit changed list' = {
        $s = New-State
        $backend = FakeBackend $s
        $a = Normalize (Assignment "$board2,$board")
        $same = Normalize (Assignment "$board,$board2")
        Assert ($a -ceq $same) 'Order changes the approved list.'
        Invoke-LoaderDailyProvisioning -Assignment $a -Backend $backend | Out-Null
        Assert ($s.Enabled -and $s.Clock -eq 1 -and !$s.Pending -and $s.Active -ceq $a) 'Fresh setup did not converge.'
        $s.Clock = 19
        Invoke-LoaderDailyProvisioning -Assignment $same -Backend $backend | Out-Null
        Assert ($s.Clock -eq 19 -and $s.Active -ceq $a -and $s.Registrations -eq 1) 'Same input replaced preserved state or task.'
        $changed = Normalize (Assignment $board2)
        Refuse { Invoke-LoaderDailyProvisioning -Assignment $changed -Backend $backend } 'The preserved assignment conflicts*'
        Assert ($s.Active -ceq $a -and !$s.Pending -and $s.Enabled) 'Implicit change affected state.'
        Invoke-LoaderDailyProvisioning -Assignment $changed -Backend $backend -ReplaceAssignment | Out-Null
        Assert ($s.Active -ceq $changed -and $s.Clock -eq 19 -and $s.Enabled) 'Explicit change affected the admission clock.'
        Invoke-LoaderDailyProvisioning -Backend $backend -Uninstall | Out-Null
        Assert (!$s.Registered -and $s.Active -ceq $changed -and $s.Clock -eq 19) 'Uninstall removed preserved state.'
    }
    'conflicting task and operator' = {
        $s = New-State
        $backend = FakeBackend $s
        $a = Normalize (Assignment $board)
        $s.ForeignTask = $true
        Refuse { Invoke-LoaderDailyProvisioning -Assignment $a -Backend $backend } 'Conflicting worker task.'
        Assert (!$s.Pending -and $s.Prepared -eq 0) 'Conflict altered storage.'
        $s.ForeignTask = $false
        Invoke-LoaderDailyProvisioning -Assignment $a -Backend $backend | Out-Null
        $differentSid = ConvertTo-LoaderDailyAssignment -Json (Assignment $board 'S-1-5-21-101-102-103-105') -ValidateOperator { $true }
        Refuse { Invoke-LoaderDailyProvisioning -Assignment $differentSid -Backend $backend -ReplaceAssignment } 'The preserved Windows user conflicts*'
        Assert ($s.Active -ceq $a -and $s.Enabled -and !$s.Pending) 'Conflicting operator altered storage.'
    }
    'interrupted base enrollment preserved for review' = {
        $s = New-State
        $backend = FakeBackend $s
        $a = Normalize (Assignment $board)
        $s.InterruptStore = $true
        Refuse { Invoke-LoaderDailyProvisioning -Assignment $a -Backend $backend } 'Simulated interruption before store enrollment completed.'
        Assert ($s.PartialStore -and $s.Prepared -eq 1 -and !$s.Pending -and !$s.Registered -and $s.Clock -eq 0) 'Interrupted enrollment published assignment or task authority.'
        $s.InterruptStore = $false
        Refuse { Invoke-LoaderDailyProvisioning -Assignment $a -Backend $backend } 'The preserved machine store requires administrator review.'
        Assert ($s.PartialStore -and $s.Prepared -eq 1 -and !$s.Active -and !$s.Registered) 'Retry overwrote incomplete base enrollment.'
    }
    'interrupted provisioning and exact retry' = {
        $s = New-State
        $backend = FakeBackend $s
        $a = Normalize (Assignment $board)
        $s.Interrupt = $true
        Refuse { Invoke-LoaderDailyProvisioning -Assignment $a -Backend $backend } 'Simulated interruption.'
        Assert ($s.Pending -ceq $a -and !$s.Active -and !$s.Enabled -and $s.Clock -eq 1) 'Interrupted setup left authority enabled.'
        Refuse { Invoke-LoaderDailyProvisioning -Assignment (Normalize (Assignment $board2)) -Backend $backend -ReplaceAssignment } 'The interrupted setup conflicts*'
        $s.Interrupt = $false
        Invoke-LoaderDailyProvisioning -Assignment $a -Backend $backend | Out-Null
        Assert (!$s.Pending -and $s.Active -ceq $a -and $s.Enabled) 'Exact retry did not complete setup.'
    }
}

$results = foreach ($case in $cases.GetEnumerator()) {
    try { & $case.Value; "PASS: $($case.Key)" }
    catch { "FAIL: $($case.Key): $($_.Exception.Message)" }
}
$results | Tee-Object -FilePath (Join-Path $evidence 'results.log')
if ($results -match '^FAIL:') { throw 'Daily worker provisioning fixtures failed.' }
"PASS: $($cases.Count) daily worker provisioning fixture cases."
