# Pure suite-plan fixtures. No mirrors, filesystem writes, or process launches.
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'review-workflow.ps1')
$checks=[Collections.Generic.List[string]]::new()
function Check([string]$Name,[bool]$Value) {
    if (-not $Value) { throw "Fixture failed: $Name" }
    $checks.Add($Name)
}
function Reject([string]$Name,[string]$Specification,[string]$Message) {
    $refused=$false
    try { Get-ReviewSuitePlan $false $Specification | Out-Null }
    catch {
        if ($_.Exception.Message -notlike ('*'+$Message+'*')) { throw }
        $refused=$true
    }
    Check $Name $refused
}
$all=Get-ReviewSuitePlan $false ''
Check 'Full UMA default is not diagnostic' (-not $all.diagnostic)
Check 'Full UMA default discovers three suites' ($all.suites.Count -eq 3)
Check 'Full UMA default preserves assembly order' ((@($all.suites | ForEach-Object {$_.assembly}) -join ',') -ceq 'Gamesim.EditModeTests,Gamesim.PlayModeTests,Gamesim.Uma.PlayModeTests')
Check 'Full UMA default preserves ids/platforms' ((@($all.suites | ForEach-Object {$_.id+':'+$_.platform}) -join ',') -ceq 'edit:EditMode,play:PlayMode,uma:PlayMode')
Check 'Full UMA default has no filters' (@($all.suites | Where-Object {$_.filter -cne ''}).Count -eq 0)
$plain=Get-ReviewSuitePlan $true $null
Check 'Full NoUMA default is not diagnostic' (-not $plain.diagnostic)
Check 'Full NoUMA default has only Edit and Play' ($plain.suites.Count -eq 2 -and ($plain.suites[0].assembly -ceq 'Gamesim.EditModeTests') -and ($plain.suites[1].assembly -ceq 'Gamesim.PlayModeTests'))
$explicit=Get-ReviewSuitePlan $false "edit:EditMode:Gamesim.EditModeTests`nplay:PlayMode:Gamesim.PlayModeTests`numa:PlayMode:Gamesim.Uma.PlayModeTests"
Check 'Explicit even-unfiltered complete plan remains diagnostic' ($explicit.diagnostic -and $explicit.suites.Count -eq 3)
# Each suite's floor line (Tools/baseline.txt): the UMA build of Gamesim.PlayModeTests has its own.
Check 'Full UMA default holds Gamesim.PlayModeTests to its UMA build line' ((@($all.suites | ForEach-Object {$_.floorKey}) -join ',') -ceq 'Gamesim.EditModeTests,Gamesim.PlayModeTests.UmaBuild,Gamesim.Uma.PlayModeTests')
Check 'Full NoUMA default keeps every assembly''s own line' ((@($plain.suites | ForEach-Object {$_.floorKey}) -join ',') -ceq 'Gamesim.EditModeTests,Gamesim.PlayModeTests')
Check 'A UMA diagnostic plan reads the UMA build line too' ((@($explicit.suites | ForEach-Object {$_.floorKey}) -join ',') -ceq 'Gamesim.EditModeTests,Gamesim.PlayModeTests.UmaBuild,Gamesim.Uma.PlayModeTests')
Check 'A NoUMA diagnostic plan keeps the shared line' ((Get-ReviewSuitePlan $true 'play:PlayMode:Gamesim.PlayModeTests:Tour_').suites[0].floorKey -ceq 'Gamesim.PlayModeTests')
Check 'Only Gamesim.PlayModeTests has a UMA build line' ((Get-ReviewSuiteFloorKey 'Gamesim.Uma.DiagnosticsPlayModeTests' $true) -ceq 'Gamesim.Uma.DiagnosticsPlayModeTests' -and
    (Get-ReviewSuiteFloorKey 'Gamesim.EditModeTests' $true) -ceq 'Gamesim.EditModeTests')
Check 'The UMA build line is read only by a plan built with UMA' ((Get-ReviewSuiteFloorKey 'Gamesim.PlayModeTests' $true) -ceq 'Gamesim.PlayModeTests.UmaBuild' -and
    (Get-ReviewSuiteFloorKey 'Gamesim.PlayModeTests' $false) -ceq 'Gamesim.PlayModeTests')
$emptyFilter=Get-ReviewSuitePlan $true 'scope:EditMode:Gamesim.EditModeTests:'
Check 'Explicit empty optional filter remains diagnostic' ($emptyFilter.diagnostic -and $emptyFilter.suites[0].filter -ceq '')
foreach ($filter in @('Gamesim.Tests.Fixture.Test','^Fixture\.(Foo|Bar).*','Fixture[0-9]+','Fixture\s+Name','Fixture\\','Fixture;Other','^Fixture[-_A-Z]+$')) {
    $plan=Get-ReviewSuitePlan $false ('scope:EditMode:Gamesim.EditModeTests:'+$filter)
    Check ('Safe identifier/regex token preserved: '+$filter) ($plan.diagnostic -and $plan.suites.Count -eq 1 -and $plan.suites[0].filter -ceq $filter)
}
$crlf=Get-ReviewSuitePlan $false "one:EditMode:Gamesim.EditModeTests:Foo`r`ntwo:PlayMode:Gamesim.PlayModeTests:Bar"
Check 'CRLF separates diagnostic suite records without filter whitespace' ($crlf.suites.Count -eq 2 -and $crlf.suites[0].filter -ceq 'Foo' -and $crlf.suites[1].filter -ceq 'Bar')
$filters=@(
    @('double quote','Foo" -quit "'),
    @('single quote',"Foo'Bar"),
    @('space','Foo -quit'),
    @('tab',"Foo`t-quit"),
    @('carriage return',"Foo`r-quit"),
    @('null',('Foo'+[char]0+'Bar')),
    @('delete',('Foo'+[char]127+'Bar')),
    @('nonbreaking space',('Foo'+[char]160+'Bar')),
    @('zero-width format',('Foo'+[char]8203+'Bar')),
    @('leading option','-quit'),
    @('double leading option','--quit')
)
foreach ($item in $filters) { Reject ('Unsafe filter refuses: '+$item[0]) ('scope:EditMode:Gamesim.EditModeTests:'+$item[1]) 'Unsafe test filter' }
Reject 'Embedded newline cannot introduce an argument token' "scope:EditMode:Gamesim.EditModeTests:Foo`n-quit" 'Invalid suite specification'
Reject 'Whitespace-only specification cannot become defaults' ' ' 'Invalid suite specification'
Reject 'Unsafe artifact id refuses' 'bad id:EditMode:Gamesim.EditModeTests:Foo' 'Invalid suite specification'
Reject 'Quoted assembly refuses' 'scope:EditMode:Gamesim.EditModeTests":Foo' 'Invalid suite specification'
Reject 'Trailing control in artifact id refuses' ("bad`r:EditMode:Gamesim.EditModeTests:Foo") 'Invalid suite specification'
Reject 'Trailing control in assembly refuses' ("scope:EditMode:Gamesim.EditModeTests`r:Foo") 'Invalid suite specification'
Reject 'Platform is exact case' 'scope:editmode:Gamesim.EditModeTests:Foo' 'Invalid suite specification'
Reject 'Extra colon does not escape the record grammar' 'scope:EditMode:Gamesim.EditModeTests:Foo:Bar' 'Invalid suite specification'
Reject 'Duplicate artifact ids refuse case-insensitively' "same:EditMode:Gamesim.EditModeTests:Foo`nSAME:PlayMode:Gamesim.PlayModeTests:Bar" 'Duplicate suite artifact id'
$sixteen=(1..16 | ForEach-Object {'scope'+$_+':EditMode:Gamesim.EditModeTests:Foo'}) -join "`n"
Check 'Sixteen diagnostic suites preserve the existing bound' ((Get-ReviewSuitePlan $false $sixteen).suites.Count -eq 16)
Reject 'Seventeen diagnostic suites refuse' ($sixteen+"`nscope17:EditMode:Gamesim.EditModeTests:Foo") 'between 1 and 16'
Reject 'Empty diagnostic records cannot become full defaults' "`n`n" 'between 1 and 16'
$all.suites[0].filter='mutated prior plan'
Check 'Subsequent full plan is detached and unfiltered' ((Get-ReviewSuitePlan $false '').suites[0].filter -ceq '')
# Confirm the controller consumes the same pure planner before acquiring its lease.
$tokens=$null; $parseErrors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'run-review-suites.ps1'),[ref]$tokens,[ref]$parseErrors)
Check 'Controller parses without errors' ($parseErrors.Count -eq 0)
$commands=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.CommandAst]},$true))
$planner=@($commands | Where-Object {$_.GetCommandName() -ceq 'Get-ReviewSuitePlan'})
$leases=@($commands | Where-Object {$_.GetCommandName() -ceq 'Enter-ReviewLease'})
$processes=@($commands | Where-Object {$_.GetCommandName() -ceq 'Invoke-ReviewOwnedProcess'})
Check 'Unsafe plan validation precedes the single lease and process call' ($planner.Count -eq 1 -and $leases.Count -eq 1 -and $processes.Count -eq 1 -and $planner[0].Extent.StartOffset -lt $leases[0].Extent.StartOffset -and $planner[0].Extent.StartOffset -lt $processes[0].Extent.StartOffset)
Write-Output ('Suite specifications fixtures passed: '+$checks.Count)
