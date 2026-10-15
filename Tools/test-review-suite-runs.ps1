# Isolated fixtures for how the review controller runs its suites: per-suite deadlines, a late run
# recorded and never killed, every suite run before the gate fails, the diagnostics define, and a
# source status probe that never rewrites .git/index. A fake Unity (a .cmd that writes NUnit XML) stands
# in for the editor; nothing here starts Unity or touches a real acceptance copy.
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'review-evidence.ps1')
. (Join-Path $PSScriptRoot 'review-workflow.ps1')
$fixtureBase=Assert-ReviewPlainPath (Join-Path ([IO.Path]::GetTempPath()) 'GamesimWorkflowFixtures')
$fixture=Join-Path $fixtureBase ([Guid]::NewGuid().ToString('N'))
$checks=[Collections.Generic.List[string]]::new(); $fixtureSucceeded=$false
$saved=@{}; foreach ($name in @('GAMESIM_ACCEPTANCE','GAMESIM_UNITY','GAMESIM_DIAGNOSTICS','GAMESIM_FAKE_FAIL','GAMESIM_FAKE_SLEEP','GIT_OPTIONAL_LOCKS','PATH')) { $saved[$name]=[Environment]::GetEnvironmentVariable($name) }
function Put([string]$Root,[string]$Relative,[string]$Text) {
    $path=Join-Path $Root $Relative; [void][IO.Directory]::CreateDirectory((Split-Path -Parent $path))
    [IO.File]::WriteAllText($path,$Text,[Text.UTF8Encoding]::new($false))
}
function Check([string]$Name,[bool]$Value) { if (-not $Value) {throw "Fixture failed: $Name"}; $checks.Add($Name) }
function Alive([int]$Id) { return [bool](Get-Process -Id $Id -ErrorAction SilentlyContinue) }
function WaitGone([int]$Id,[int]$Seconds) {
    $by=[DateTime]::UtcNow.AddSeconds($Seconds)
    while ((Alive $Id) -and [DateTime]::UtcNow -lt $by) { Start-Sleep -Milliseconds 200 }
    return -not (Alive $Id)
}
try {
    [void][IO.Directory]::CreateDirectory($fixture)
    $powershell=Join-Path $PSHOME 'powershell.exe'

    # 1. Per-suite deadlines: each at least 1.5x (in fact 2x) the longest whole-process time measured
    #    for that suite, PlayMode at least three hours, all inside the raised cap.
    $measured=@{'Gamesim.EditModeTests'=2305;'Gamesim.PlayModeTests'=5401;'Gamesim.Uma.PlayModeTests'=308;
        'Gamesim.DiagnosticsPlayModeTests'=135;'Gamesim.Uma.DiagnosticsPlayModeTests'=135}
    foreach ($assembly in $measured.Keys) {
        $platform=if ($assembly -like '*EditMode*') {'EditMode'} else {'PlayMode'}
        $deadline=Get-ReviewSuiteDeadline $assembly $platform
        Check "Deadline for $assembly is at least twice its measured $($measured[$assembly]) s" ($deadline -ge 2*$measured[$assembly])
        Check "Deadline for $assembly fits the cap" ($deadline -le $script:ReviewDeadlineCapSeconds)
    }
    Check 'PlayMode suite deadline is at least 10,800 s' ((Get-ReviewSuiteDeadline 'Gamesim.PlayModeTests' 'PlayMode') -ge 10800)
    Check 'An unlisted PlayMode assembly takes the PlayMode suite deadline' ((Get-ReviewSuiteDeadline 'Gamesim.SomethingNew' 'PlayMode') -eq 14400)
    Check 'An unlisted EditMode assembly takes the EditMode suite deadline' ((Get-ReviewSuiteDeadline 'Gamesim.SomethingNew' 'EditMode') -eq 7200)
    Check 'The cap is above the old 7,200 s' ($script:ReviewDeadlineCapSeconds -gt 7200)

    # 2. A process past its deadline is recorded late and failed, then left to finish on its own.
    $marker=Join-Path $fixture 'late-finished.txt'
    $late=Invoke-ReviewOwnedProcess $powershell @('-NoProfile','-Command',"Start-Sleep -Seconds 4; Set-Content -LiteralPath '$marker' -Value done") 1 (Join-Path $fixture 'late.json') 60
    Check 'A late process is recorded late and timed out' ($late.late -and $late.timedOut)
    Check 'A late process is never killed' (-not $late.stopped)
    Check 'A late process exited on its own and its exit code was kept' ($late.closed -and $late.exit -eq 0 -and -not $late.abandoned)
    Check 'A late process ran to its own end' (Test-Path -LiteralPath $marker)
    Check 'A late run is a failure' ($late.failure -like '*recorded late and failed*')
    $receipt=Get-Content -LiteralPath (Join-Path $fixture 'late.json') -Raw | ConvertFrom-Json
    Check 'The late receipt is retained' ($receipt.late -and -not $receipt.stopped -and $receipt.deadlineSeconds -eq 1)

    # 3. One that has still not exited after the grace period is abandoned, still running, not killed.
    $slow=Invoke-ReviewOwnedProcess $powershell @('-NoProfile','-Command','Start-Sleep -Seconds 8') 1 (Join-Path $fixture 'abandoned.json') 1
    Check 'An abandoned process is recorded abandoned, late and not closed' ($slow.abandoned -and $slow.late -and -not $slow.closed)
    Check 'An abandoned process is never killed' (-not $slow.stopped -and (Alive $slow.pid))
    Check 'The abandoned failure names the pid' ($slow.failure -like ('*'+$slow.pid+'*never killed*'))
    Check 'The abandoned process exits on its own' (WaitGone $slow.pid 30)

    # 4. An on-time process is neither late nor failed.
    $quick=Invoke-ReviewOwnedProcess $powershell @('-NoProfile','-Command','exit 3') 60 (Join-Path $fixture 'quick.json')
    Check 'An on-time process is not late and keeps its exit code' (-not $quick.late -and -not $quick.timedOut -and $quick.closed -and $quick.exit -eq 3 -and -not $quick.failure)

    # 5. The source status probe never rewrites .git/index (GIT_OPTIONAL_LOCKS / --no-optional-locks).
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        foreach ($candidate in @("$env:ProgramFiles\Git\cmd","${env:ProgramFiles(x86)}\Git\cmd")) {
            if (Test-Path -LiteralPath (Join-Path $candidate 'git.exe')) { $env:PATH=$candidate+';'+$env:PATH; break }
        }
    }
    $repo=Join-Path $fixture 'repo'; Put $repo 'tracked.txt' 'one'
    $env:GIT_OPTIONAL_LOCKS=$null
    & git -C $repo init --quiet; & git -C $repo -c user.name=fixture -c user.email=fixture@example.invalid add tracked.txt
    & git -C $repo -c user.name=fixture -c user.email=fixture@example.invalid commit --quiet -m fixture
    if ($LASTEXITCODE -ne 0) { throw 'Fixture git commit failed.' }
    # Same bytes, newer stat data: an ordinary `git status` refreshes the stat cache and rewrites the index.
    [IO.File]::SetLastWriteTimeUtc((Join-Path $repo 'tracked.txt'),[DateTime]::UtcNow.AddMinutes(5))
    $indexPath=Join-Path $repo '.git/index'
    $indexBefore=Get-ReviewHash $indexPath; $indexTimeBefore=[IO.File]::GetLastWriteTimeUtc($indexPath)
    $identity=Get-ReviewGitIdentity $repo
    Check 'The probe reads a clean status' ($identity.head -match '^[0-9a-f]{40}$' -and @($identity.status).Count -eq 0)
    Check 'The probe leaves .git/index byte for byte' ((Get-ReviewHash $indexPath) -ceq $indexBefore -and [IO.File]::GetLastWriteTimeUtc($indexPath) -eq $indexTimeBefore)

    # 6. The diagnostics define: added only on request, and taken off a copy otherwise.
    $plain="  scriptingDefineSymbols:`n    Standalone: KEEP`n    Android: KEEP`n"
    $withDiagnostics="  scriptingDefineSymbols:`n    Standalone: KEEP;GAMESIM_DIAGNOSTICS`n    Android: KEEP`n"
    Check 'Diagnostics activation adds exactly the Standalone token' ((ConvertTo-ReviewAcceptanceSettings $plain $true $false $true) -ceq $withDiagnostics)
    Check 'Diagnostics activation is idempotent' ((ConvertTo-ReviewAcceptanceSettings $withDiagnostics $true $false $true) -ceq $withDiagnostics)
    Check 'Without the request a copy never carries the diagnostics token' ((ConvertTo-ReviewAcceptanceSettings $withDiagnostics $true) -ceq $plain)
    Check 'Diagnostics and retained UMA compose' ((ConvertTo-ReviewAcceptanceSettings $plain $false $true $true) -ceq "  scriptingDefineSymbols:`n    Standalone: KEEP;GAMESIM_UMA;GAMESIM_DIAGNOSTICS`n    Android: KEEP`n")

    # 7. The controller end to end, against fixture projects and a fake Unity.
    $source=Join-Path $fixture 'source'; $destination=Join-Path $fixture 'destination'
    foreach ($root in @($source,$destination)) {
        Put $root 'ProjectSettings/ProjectVersion.txt' 'm_EditorVersion: fixture'
        Put $root 'ProjectSettings/ProjectSettings.asset' "  scriptingDefineSymbols:`n    Standalone: KEEP`n"
        Put $root 'Assets/Settings/PC_RPAsset.asset' "  m_GPUResidentDrawerMode: 1`n"
        Put $root 'Packages/manifest.json' '{}'; Put $root 'ArtSource/source.txt' 'authored'
    }
    Put $source 'Tools/baseline.txt' "Gamesim.EditModeTests 2`nGamesim.PlayModeTests 2`nGamesim.Uma.PlayModeTests 2`nGamesim.DiagnosticsPlayModeTests 2"
    foreach ($scriptName in @('review-evidence.ps1','review-workflow.ps1','sync-acceptance.ps1','run-review-suites.ps1','verify-review-candidate.ps1')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $scriptName) -Destination (Join-Path $source ('Tools/'+$scriptName))
    }
    $fakeRoot=Join-Path $fixture 'fake-unity'
    Put $fakeRoot 'fake-unity.ps1' @'
$named=@{}
for ($i=0; $i -lt $args.Count-1; $i++) { if ("$($args[$i])".StartsWith('-')) { $named["$($args[$i])"]="$($args[$i+1])".Trim('"') } }
$assembly=$named['-assemblyNames']
foreach ($rule in @("$env:GAMESIM_FAKE_SLEEP".Split(';') | Where-Object {$_})) {
    $parts=$rule.Split('='); if ($parts[0] -ceq $assembly) { Start-Sleep -Seconds ([int]$parts[1]) }
}
$failed=@("$env:GAMESIM_FAKE_FAIL".Split(';')) -ccontains $assembly
$second=if ($failed) {'Failed'} else {'Passed'}; $status=if ($failed) {'Failed'} else {'Passed'}
$xml='<test-run result="'+$status+'" total="2" passed="'+$(if ($failed) {1} else {2})+'" failed="'+$(if ($failed) {1} else {0})+'" skipped="0" inconclusive="0">'+
    '<test-suite type="Assembly" name="'+$assembly+'.dll" result="'+$status+'">'+
    '<test-case id="1" fullname="fixture.'+$assembly+'.one" result="Passed" /><test-case id="2" fullname="fixture.'+$assembly+'.two" result="'+$second+'" />'+
    '</test-suite></test-run>'
[IO.File]::WriteAllText($named['-testResults'],$xml)
[IO.File]::WriteAllText($named['-logFile'],"fake Unity ran $assembly")
exit $(if ($failed) {2} else {0})
'@
    Put $fakeRoot 'fake-unity.cmd' ("@echo off`r`n`"$powershell`" -NoProfile -ExecutionPolicy Bypass -File `"%~dp0fake-unity.ps1`" %*`r`nexit /b %ERRORLEVEL%`r`n")
    $env:GAMESIM_ACCEPTANCE=$destination; $env:GAMESIM_UNITY=Join-Path $fakeRoot 'fake-unity.cmd'; $env:GAMESIM_DIAGNOSTICS=$null
    function Summary([string]$Name) { Get-Content -LiteralPath (Join-Path $destination "Logs/$Name-summary.json") -Raw | ConvertFrom-Json }
    function Refused([scriptblock]$Action) { try { & $Action | Out-Null; return $false } catch { return $_.Exception.Message -like '*gate failed*' } }

    # 7a. A failing first suite no longer stops the plan: every suite runs and is recorded, then the gate fails.
    $env:GAMESIM_FAKE_FAIL='Gamesim.EditModeTests'; $env:GAMESIM_FAKE_SLEEP=$null
    Check 'A failed suite fails the full gate' (Refused {& (Join-Path $source 'Tools/verify-review-candidate.ps1') -Name 'every-suite' -WithoutUma})
    $every=Summary 'every-suite'
    Check 'Every planned suite ran and was recorded after the first one failed' (@($every.suites).Count -eq 2 -and
        @($every.suites | Where-Object {$_.suite -ceq 'Gamesim.PlayModeTests' -and $_.accepted}).Count -eq 1)
    Check 'The failed suite is named and the run is not acceptance' ($every.status -ceq 'Failed' -and -not $every.fullSuiteAcceptance -and $every.failure -like '*Gamesim.EditModeTests*')
    Check 'Each suite took its own measured deadline' ((@($every.suites | ForEach-Object {$_.deadlineSeconds}) -join ',') -ceq '7200,14400')

    # 7b. A late suite is recorded late and failed, never killed, and the next suite still runs.
    $env:GAMESIM_FAKE_FAIL=$null; $env:GAMESIM_FAKE_SLEEP='Gamesim.EditModeTests=7'
    # Deadline 5 s, so the 7 s suite is late at 5 s and exits on its own well before the 10 s abandon point.
    Check 'A late suite fails the full gate' (Refused {& (Join-Path $source 'Tools/verify-review-candidate.ps1') -Name 'late-suite' -WithoutUma -SuiteTimeoutSeconds 5})
    $lateSummary=Summary 'late-suite'
    $lateEdit=@($lateSummary.suites | Where-Object {$_.suite -ceq 'Gamesim.EditModeTests'})
    Check 'The late suite kept its own report and is marked late, not accepted' ($lateEdit.Count -eq 1 -and $lateEdit[0].late -and -not $lateEdit[0].accepted -and $lateEdit[0].passed -eq 2)
    Check 'The suite after a late one still ran' (@($lateSummary.suites | Where-Object {$_.suite -ceq 'Gamesim.PlayModeTests' -and $_.accepted -and -not $_.late}).Count -eq 1)
    $lateReceipt=Get-Content -LiteralPath (Join-Path $lateSummary.runRoot 'edit-process.json') -Raw | ConvertFrom-Json
    Check 'The late Unity was not killed' ($lateReceipt.late -and -not $lateReceipt.stopped -and $lateReceipt.closed -and $lateReceipt.exit -eq 0)

    # 7c. All green is still a pass.
    $env:GAMESIM_FAKE_SLEEP=$null
    & (Join-Path $source 'Tools/verify-review-candidate.ps1') -Name 'all-green' -WithoutUma | Out-Null
    $green=Summary 'all-green'
    Check 'All suites green is full acceptance' ($green.status -ceq 'Passed' -and $green.fullSuiteAcceptance -and @($green.suites).Count -eq 2)

    # 7d. The diagnostics define is refused for full acceptance and reaches only the copy for a plan.
    $env:GAMESIM_DIAGNOSTICS='1'
    Check 'GAMESIM_DIAGNOSTICS is refused for a full acceptance run' (Refused {& (Join-Path $source 'Tools/verify-review-candidate.ps1') -Name 'diagnostics-full' -WithoutUma})
    Check 'The refusal says why' ((Summary 'diagnostics-full').failure -like '*GAMESIM_DIAGNOSTICS*never for full acceptance*')
    & (Join-Path $source 'Tools/run-review-suites.ps1') -Name 'diagnostics-plan' -WithoutUma -SuiteSpecifications 'diag:PlayMode:Gamesim.DiagnosticsPlayModeTests' | Out-Null
    $plan=Summary 'diagnostics-plan'
    Check 'A diagnostics plan runs with its own deadline and floor' ($plan.status -ceq 'DiagnosticPassed' -and @($plan.suites)[0].deadlineSeconds -eq 3600 -and @($plan.suites)[0].minimumCount -eq 2)
    Check 'The copy carries the define for the diagnostics plan' ([IO.File]::ReadAllText((Join-Path $destination 'ProjectSettings/ProjectSettings.asset')) -cmatch '(?m)^    Standalone: KEEP;GAMESIM_DIAGNOSTICS\r?$')
    Check 'The source settings never carry it' ([IO.File]::ReadAllText((Join-Path $source 'ProjectSettings/ProjectSettings.asset')) -cnotmatch 'GAMESIM_DIAGNOSTICS')
    $env:GAMESIM_DIAGNOSTICS=$null
    & (Join-Path $source 'Tools/run-review-suites.ps1') -Name 'ordinary-plan' -WithoutUma -SuiteSpecifications 'play:PlayMode:Gamesim.PlayModeTests' | Out-Null
    Check 'The next ordinary sync takes the define off the copy' ([IO.File]::ReadAllText((Join-Path $destination 'ProjectSettings/ProjectSettings.asset')) -cnotmatch 'GAMESIM_DIAGNOSTICS')

    $fixtureSucceeded=$true
    Write-Output ("{0} suite-run checks passed; temporary fixtures and a fake Unity only." -f $checks.Count)
    $checks | ForEach-Object {Write-Output ('Passed: '+$_)}
} finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name,$saved[$name]) }
    $resolved=Assert-ReviewPlainPath $fixture
    if (-not $resolved.StartsWith($fixtureBase+'\',[StringComparison]::OrdinalIgnoreCase)) {throw 'Unsafe fixture cleanup boundary.'}
    if ($fixtureSucceeded -and (Test-Path -LiteralPath $resolved)) {Remove-Item -LiteralPath $resolved -Recurse -Force}
    elseif (-not $fixtureSucceeded) {Write-Output "Failed isolated suite-run fixture retained: $resolved"}
}
