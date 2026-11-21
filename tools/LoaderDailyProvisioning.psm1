Set-StrictMode -Version Latest

function Read-AssignmentObject($Node) {
    if ($Node.ValueKind -ne [Text.Json.JsonValueKind]::Object) { throw 'The board assignment is malformed.' }
    $fields = [Collections.Generic.Dictionary[string,Text.Json.JsonElement]]::new([StringComparer]::Ordinal)
    foreach ($entry in $Node.EnumerateObject()) {
        if (!$fields.TryAdd($entry.Name, $entry.Value)) { throw 'The board assignment contains a repeated field.' }
    }
    return ,$fields
}

function Convert-AssignmentDigest($Node) {
    if ($Node.ValueKind -ne [Text.Json.JsonValueKind]::String) { throw 'The board assignment digest is malformed.' }
    $digest = $Node.GetString()
    if ($digest -cnotmatch '\A[0-9A-F]{64}\z' -or $digest -ceq ('0' * 64)) {
        throw 'The board assignment requires uppercase, nonzero SHA-256 values.'
    }
    return $digest
}

function ConvertTo-LoaderDailyAssignment {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string] $Json,
        [Parameter(Mandatory)][scriptblock] $ValidateOperator
    )
    if ($Json.Length -gt 16384 -or [string]::IsNullOrWhiteSpace($Json)) { throw 'The board assignment is missing or too large.' }
    try { $document = [Text.Json.JsonDocument]::Parse($Json) }
    catch { throw 'The board assignment JSON is malformed.' }
    try {
        $root = Read-AssignmentObject $document.RootElement
        if (!$root.ContainsKey('operatorSid') -or $root['operatorSid'].ValueKind -ne [Text.Json.JsonValueKind]::String) {
            throw 'The Windows user must be supplied as one account SID.'
        }
        $sid = $root['operatorSid'].GetString()
        if ($sid -cnotmatch '\AS-1-5-21(?:-(?:0|[1-9][0-9]{0,9})){4}\z') {
            throw 'The Windows user SID must name one account in the supported Windows SID format.'
        }
        $sidFields = $sid.Split('-')
        foreach ($field in $sidFields[4..7]) {
            if (![uint]::TryParse($field,[Globalization.NumberStyles]::None,[Globalization.CultureInfo]::InvariantCulture,[ref]$null)) {
                throw 'The Windows user SID is malformed.'
            }
        }
        if ($sidFields[7] -ceq '0') { throw 'The Windows user SID has no account identifier.' }
        try { $canonicalSid = [Security.Principal.SecurityIdentifier]::new($sid).Value }
        catch { throw 'The Windows user SID is malformed.' }
        if ($sid -cne $canonicalSid -or $sid -cin 'S-1-5-18','S-1-5-19','S-1-5-20' -or !(& $ValidateOperator $sid)) {
            throw 'The Windows user must resolve to an approved user account.'
        }
        if ($root.Count -ne 3 -or !$root.ContainsKey('schema') -or $root['schema'].GetRawText() -cne '1' -or
            !$root.ContainsKey('boards') -or $root['boards'].ValueKind -ne [Text.Json.JsonValueKind]::Array -or
            $root['boards'].GetArrayLength() -notin (1..32)) {
            throw 'The board assignment requires schema 1 and one to 32 boards.'
        }

        $boards = [Collections.Generic.SortedDictionary[string,object]]::new([StringComparer]::Ordinal)
        $releases = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($item in $root['boards'].EnumerateArray()) {
            $fields = Read-AssignmentObject $item
            if ($fields.Count -ne 3 -or !$fields.ContainsKey('fingerprint') -or
                !$fields.ContainsKey('installManifestSha256') -or !$fields.ContainsKey('restoreManifestSha256')) {
                throw 'The board assignment requires a board fingerprint and both signed release digests.'
            }
            $fingerprint = Convert-AssignmentDigest $fields['fingerprint']
            $install = Convert-AssignmentDigest $fields['installManifestSha256']
            $restore = Convert-AssignmentDigest $fields['restoreManifestSha256']
            if ($boards.ContainsKey($fingerprint) -or !$releases.Add($install) -or !$releases.Add($restore)) {
                throw 'The board assignment contains a repeated fingerprint or release digest.'
            }
            $boards.Add($fingerprint, [ordered]@{
                fingerprint = $fingerprint
                installManifestSha256 = $install
                restoreManifestSha256 = $restore
            })
        }
        return ConvertTo-Json -Compress -Depth 5 -InputObject ([ordered]@{
            schema = 1; operatorSid = $sid; boards = @($boards.Values)
        })
    }
    finally { $document.Dispose() }
}

function Read-PreservedAssignment([string] $Json) {
    if (!$Json) { return $null }
    $canonical = ConvertTo-LoaderDailyAssignment -Json $Json -ValidateOperator { $true }
    if ($canonical -cne $Json) { throw 'The preserved assignment is malformed. Ask the installation administrator to check setup.' }
    return ConvertFrom-Json -AsHashtable -InputObject $Json
}

function Invoke-LoaderDailyProvisioning {
    [CmdletBinding(DefaultParameterSetName = 'Install')]
    param(
        [Parameter(Mandatory, ParameterSetName = 'Install')][string] $Assignment,
        [Parameter(ParameterSetName = 'Install')][switch] $ReplaceAssignment,
        [Parameter(Mandatory, ParameterSetName = 'Uninstall')][switch] $Uninstall,
        [Parameter(Mandatory)][hashtable] $Backend
    )

    $state = & $Backend.ReadState
    $active = Read-PreservedAssignment $state.Active
    $pending = Read-PreservedAssignment $state.Pending
    if ($active -and $pending -and $active.operatorSid -cne $pending.operatorSid) {
        throw 'The preserved Windows user conflicts with the pending setup.'
    }
    $prior = $active ?? $pending
    $existingSid = if ($prior) { $prior.operatorSid } else { $null }
    if ($Uninstall) {
        if ($pending) { throw 'The interrupted setup must be completed before uninstalling the worker.' }
        $null = & $Backend.InspectTask $existingSid
        if ($active) { $null = & $Backend.RemoveTask $existingSid }
        return 'Daily worker registration removed. Protected records were kept.'
    }

    $desired = Read-PreservedAssignment $Assignment
    if ($prior -and $existingSid -cne $desired.operatorSid) { throw 'The preserved Windows user conflicts with this setup.' }
    if ($pending -and $state.Pending -cne $Assignment) { throw 'The interrupted setup conflicts with this assignment. Repeat the exact previous setup input.' }
    if ($active -and !$pending -and $state.Active -cne $Assignment -and !$ReplaceAssignment) {
        throw 'The preserved assignment conflicts with this setup. Use the explicit assignment replacement option for a reviewed list change.'
    }

    $taskEnabled = & $Backend.InspectTask $existingSid
    if ($active -and !$pending -and $state.Active -ceq $Assignment -and $taskEnabled) {
        $null = & $Backend.EnsureClock $false
        $null = & $Backend.InitializeStore
        return 'Daily worker setup already matches the approved assignment.'
    }

    if ($active) { $null = & $Backend.EnsureClock $false }
    $null = & $Backend.InitializeStore
    if (!$pending) { $null = & $Backend.Begin $Assignment $state.Active }
    if (!$active) { $null = & $Backend.EnsureClock $true }
    $null = & $Backend.StageTask $desired.operatorSid ([bool]($active -and $state.Active -cne $Assignment))
    $null = & $Backend.Publish $Assignment $state.Active
    $null = & $Backend.EnableTask $desired.operatorSid
    $null = & $Backend.Complete $Assignment
    return 'Daily worker setup is ready.'
}

function Assert-LoaderSetupPathAcl([Security.AccessControl.FileSystemSecurity] $Acl, [switch] $AncestorOnly) {
    $raw = [Security.AccessControl.RawSecurityDescriptor]::new($Acl.GetSecurityDescriptorBinaryForm(),0)
    if ($null -eq $raw.DiscretionaryAcl) { throw 'The Firmware Loader setup path has no protective access list.' }
    $sidType = [Security.Principal.SecurityIdentifier]
    $trustedOwners = 'S-1-5-32-544','S-1-5-18','S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464'
    if ($Acl.GetOwner($sidType).Value -cnotin $trustedOwners) {
        throw 'Use an administrator-managed Firmware Loader setup path.'
    }
    $rights = [Security.AccessControl.FileSystemRights]'Delete,DeleteSubdirectoriesAndFiles,ChangePermissions,TakeOwnership'
    if (!$AncestorOnly) { $rights = $rights -bor [Security.AccessControl.FileSystemRights]::Write }
    foreach ($ace in $Acl.GetAccessRules($true,$true,$sidType)) {
        if ($ace.AccessControlType -eq 'Allow' -and $ace.IdentityReference.Value -cnotin $trustedOwners -and
            !(($ace.PropagationFlags -band [Security.AccessControl.PropagationFlags]::InheritOnly)) -and
            (([int]$ace.FileSystemRights -band [int]$rights) -or ([int]$ace.FileSystemRights -band 0x50000000))) {
            throw 'Firmware Loader setup files and the installed worker must be protected from ordinary-user changes.'
        }
    }
}

Export-ModuleMember -Function ConvertTo-LoaderDailyAssignment, Invoke-LoaderDailyProvisioning, Assert-LoaderSetupPathAcl
