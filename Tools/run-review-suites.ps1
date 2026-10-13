# One destination lease spans snapshot, mirror, owned suites, and final audits.
param([Parameter(Mandatory=$true)][string]$Name,[switch]$WithoutUma,[switch]$RetainInstalledUma,
    [string]$ApprovedMetaDrift,[string]$SuiteSpecifications,[int]$SuiteTimeoutSeconds=5400)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'review-evidence.ps1')
. (Join-Path $PSScriptRoot 'review-workflow.ps1')
$projectRoot=Split-Path -Parent $PSScriptRoot
$destination=Get-ReviewDestination $projectRoot
$runRoot=New-ReviewRunRoot $destination $Name
$lease=$null; $snapshot=$null; $before=$null; $after=$null; $failure=$null; $passed=$false
$summaries=@(); $changes=@(); $sourceChanges=@(); $suiteFloors=@{}; $diagnostic=$false; $baselineSha256=$null; $approvals=@(); $suites=@()
$manifestPath=Join-Path $runRoot 'tested-before.json'; $afterPath=Join-Path $runRoot 'tested-after.json'
$driftPath=Join-Path $runRoot 'drift.json'; $summaryPath=Join-Path $runRoot 'summary.json'
$aliasPath=Join-Path $destination "Logs/$Name-summary.json"
try {
    $plan=Get-ReviewSuitePlan ([bool]$WithoutUma) $SuiteSpecifications
    $suites=@($plan.suites); $diagnostic=$plan.diagnostic
    $lease=Enter-ReviewLease $destination
    Assert-ReviewEditorIdle $destination
    if (Test-Path -LiteralPath $aliasPath) { throw 'Use a fresh artifact name; prior summary and logs are retained.' }
    if ($SuiteTimeoutSeconds -lt 1 -or $SuiteTimeoutSeconds -gt 7200) { throw 'Suite deadline must be 1..7200 seconds.' }
    $snapshot=New-ReviewSnapshot $projectRoot $runRoot
    $baselinePath=Join-Path $snapshot.root 'Tools/baseline.txt'; $baselineSha256=Get-ReviewHash $baselinePath
    $approvals=@(Read-ReviewApproval $ApprovedMetaDrift)
    Write-ReviewNewJson $approvals (Join-Path $runRoot 'import-approvals.json')
    foreach ($suite in $suites) { $suiteFloors[$suite.assembly]=Get-ReviewSuiteFloor $baselinePath $suite.assembly }
    & (Join-Path $PSScriptRoot 'sync-acceptance.ps1') -DisableGpuResidentDrawer -WithoutUma:$WithoutUma -RetainInstalledUma:$RetainInstalledUma -ValidationLease $lease -Snapshot $snapshot
    Assert-ReviewConfiguration $destination ([bool]$WithoutUma) 0
    $before=New-ReviewInputManifest -ProjectRoot $destination -WorkflowRoot $snapshot.root -WithoutUma:$WithoutUma -RetainInstalledUma:$RetainInstalledUma -MetaArchive (Join-Path $runRoot 'meta-before')
    Write-ReviewNewJson $before $manifestPath
    Write-ReviewNewJson @($before.files | Where-Object {$_.path -match '^Assets/(UMA|Resources|UMAProjectData)(/|\.meta$)'}) (Join-Path $runRoot 'retained-dependencies-before.json')
    $versions=@(Get-Content -LiteralPath (Join-Path $destination 'ProjectSettings/ProjectVersion.txt') | Select-String '^m_EditorVersion:\s*(.+)$')
    if ($versions.Count -ne 1) { throw 'Ambiguous Unity version.' }
    $version=$versions[0].Matches[0].Groups[1].Value.Trim()
    $unity=if ($env:GAMESIM_UNITY) {$env:GAMESIM_UNITY} else {"C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"}
    if (-not (Test-Path -LiteralPath $unity -PathType Leaf)) { throw "Unity is not installed: $unity" }
    foreach ($suite in $suites) {
        Assert-ReviewLease $lease $destination; Assert-ReviewEditorIdle $destination; Assert-ReviewSnapshot $snapshot
        $xmlPath=Join-Path $runRoot ($suite.id+'.xml'); $logPath=Join-Path $runRoot ($suite.id+'.log')
        $arguments=@('-batchmode','-accept-apiupdate','-projectPath',('"'+$destination+'"'),'-runTests',
            '-testPlatform',$suite.platform,'-assemblyNames',$suite.assembly,'-testResults',('"'+$xmlPath+'"'),'-logFile',('"'+$logPath+'"'))
        if ($suite.filter) { $arguments+=@('-testFilter',$suite.filter) }
        Write-Output "Running $($suite.assembly); retained evidence: $runRoot"
        $process=Invoke-ReviewOwnedProcess $unity $arguments $SuiteTimeoutSeconds (Join-Path $runRoot ($suite.id+'-process.json'))
        if ($process.failure -or -not $process.closed -or $process.stopped -or $process.timedOut) { throw "Owned suite failed: $($process.failure)" }
        $entry=Read-ReviewSuiteReport $xmlPath $suite.assembly $suiteFloors[$suite.assembly] $process.exit ([bool]$suite.filter)
        if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) { throw 'Missing owned suite log.' }
        $entry | Add-Member log $logPath
        $entry | Add-Member logSha256 (Get-ReviewHash $logPath)
        $summaries+=$entry
        Write-Output "$($entry.suite): $($entry.passed)/$($entry.total) passed; $($entry.failed) failed; $($entry.skipped) skipped; exit $($entry.exit)"
        if (-not $entry.accepted) { throw 'Suite failed, skipped/inconclusive, or exited nonzero.' }
    }
} catch { $failure=$_.Exception.Message }
finally {
    try {
        if ($snapshot) {
            try {
                $after=New-ReviewInputManifest -ProjectRoot $destination -WorkflowRoot $snapshot.root -WithoutUma:$WithoutUma -RetainInstalledUma:$RetainInstalledUma -MetaArchive (Join-Path $runRoot 'meta-after')
                Write-ReviewNewJson $after $afterPath
                Write-ReviewNewJson @($after.files | Where-Object {$_.path -match '^Assets/(UMA|Resources|UMAProjectData)(/|\.meta$)'}) (Join-Path $runRoot 'retained-dependencies-after.json')
                if ($before) {
                    $changes=@(Compare-ReviewInputs -Before $before -After $after -AllowImportMeta -ApprovedMetaDrift $approvals -RequireWorkflowStable)
                    Assert-ReviewRetainedVendorEqual $before $after
                }
                $sourceAfter=New-ReviewRawManifest $projectRoot
                Write-ReviewNewJson $sourceAfter (Join-Path $runRoot 'source-final.json')
                $sourceChanges=@(Compare-ReviewInputs -Before $snapshot.original -After $sourceAfter -NoTolerances -RequireWorkflowStable)
                Assert-ReviewSnapshot $snapshot
                Assert-ReviewSourceIdentity $snapshot
                Assert-ReviewEditorIdle $destination
            } catch { $failure=($failure+'; final audit: '+$_.Exception.Message) }
        }
        Write-ReviewNewJson ([ordered]@{changes=$changes;sourceChanges=$sourceChanges;approvedMetaDrift=$approvals}) $driftPath
        $passed=-not $failure -and $before -and $after -and $summaries.Count -eq $suites.Count -and
            @($summaries | Where-Object {-not $_.accepted}).Count -eq 0 -and
            @($changes | Where-Object {-not $_.allowed}).Count -eq 0 -and $sourceChanges.Count -eq 0
        $summary=[ordered]@{schema=4;name=$Name;status=$(if (-not $passed) {'Failed'} elseif ($diagnostic) {'DiagnosticPassed'} else {'Passed'});
            fullSuiteAcceptance=($passed -and -not $diagnostic);umaEnabled=(-not $WithoutUma);retainedInstalledUma=[bool]$RetainInstalledUma;
            runRoot=$runRoot;snapshot=$(if ($snapshot) {$snapshot.root} else {$null});snapshotProof=$(if ($snapshot) {Join-Path $runRoot 'snapshot.json'} else {$null});
            snapshotProofSha256=$(if ($snapshot) {Get-ReviewHash (Join-Path $runRoot 'snapshot.json')} else {$null});
            provenance=$(if ($snapshot) {Join-Path $runRoot 'provenance.json'} else {$null});provenanceSha256=$(if ($snapshot) {Get-ReviewHash (Join-Path $runRoot 'provenance.json')} else {$null});
            leaseToken=$(if ($lease) {$lease.token} else {$null});suiteFloors=$suiteFloors;baselinePath=$(if ($snapshot) {Join-Path $snapshot.root 'Tools/baseline.txt'} else {$null});
            baselineSha256=$baselineSha256;sourceManifest=$manifestPath;sourceManifestSha256=$(if ($before) {Get-ReviewHash $manifestPath} else {$null});
            finalSourceManifest=$afterPath;finalSourceManifestSha256=$(if ($after) {Get-ReviewHash $afterPath} else {$null});
            driftReport=$driftPath;driftReportSha256=(Get-ReviewHash $driftPath);failure=$failure;suites=$summaries}
        Write-ReviewNewJson $summary $summaryPath
        if (-not (Test-Path -LiteralPath $aliasPath)) { Write-ReviewNewJson $summary $aliasPath }
    } finally { Exit-ReviewLease $lease }
}
if (-not $passed) { throw "Candidate suite/snapshot gate failed: $summaryPath. $failure" }
Write-Output "$(if ($diagnostic) {'Diagnostic only'} else {'All owned suites'}): $summaryPath"
