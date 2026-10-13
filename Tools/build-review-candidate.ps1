# Build only exact inputs proven by a full schema4 suite run. No player is launched.
param([Parameter(Mandatory=$true)][string]$TestName,[switch]$WithoutUma,[switch]$RetainInstalledUma,[int]$BuildTimeoutSeconds=5400)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'review-evidence.ps1')
. (Join-Path $PSScriptRoot 'review-workflow.ps1')
$projectRoot=Split-Path -Parent $PSScriptRoot
$destination=Get-ReviewDestination $projectRoot
$runRoot=New-ReviewRunRoot $destination ('build-'+$TestName)
$lease=$null; $snapshot=$null; $before=$null; $after=$null; $failure=$null; $accepted=$false
$process=$null; $report=$null; $postDrift=@(); $summary=$null; $baselineSha256=$null; $buildRoot=$null
try {
    $lease=Enter-ReviewLease $destination; Assert-ReviewEditorIdle $destination
    if ($TestName -notmatch '^[a-zA-Z0-9_-]{1,100}$') { throw 'Supply a simple passing test name.' }
    $summaryPath=Join-Path $destination ("Logs/"+$TestName+'-summary.json')
    $summary=Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
    if ($summary.schema -ne 4 -or $summary.status -cne 'Passed' -or -not $summary.fullSuiteAcceptance -or
        $summary.umaEnabled -eq [bool]$WithoutUma -or $summary.retainedInstalledUma -ne [bool]$RetainInstalledUma) {
        throw 'Fresh complete schema4 suites for exactly this UMA projection are required.'
    }
    $testedRoot=Assert-ReviewPlainPath $summary.runRoot
    if (-not $testedRoot.StartsWith((Join-Path $destination 'Logs/validation')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Test evidence is outside the destination evidence boundary.' }
    if ((Get-ReviewHash $summaryPath) -ne (Get-ReviewHash (Join-Path $testedRoot 'summary.json'))) { throw 'Test summary convenience copy changed.' }
    foreach ($pair in @(@($summary.sourceManifest,$summary.sourceManifestSha256),
        @($summary.finalSourceManifest,$summary.finalSourceManifestSha256),@($summary.driftReport,$summary.driftReportSha256),
        @($summary.snapshotProof,$summary.snapshotProofSha256),@($summary.provenance,$summary.provenanceSha256))) {
        Assert-ReviewEvidenceFile $pair[0] $testedRoot $pair[1]
    }
    $testedBefore=Get-Content -LiteralPath $summary.sourceManifest -Raw | ConvertFrom-Json
    $testedAfter=Get-Content -LiteralPath $summary.finalSourceManifest -Raw | ConvertFrom-Json
    $testDrift=Get-Content -LiteralPath $summary.driftReport -Raw | ConvertFrom-Json
    if (@($testDrift.changes | Where-Object {-not $_.allowed}).Count -or @($testDrift.sourceChanges).Count) { throw 'Passing evidence contains source drift.' }
    $expected=@('Gamesim.EditModeTests','Gamesim.PlayModeTests')
    if (-not $WithoutUma) { $expected+='Gamesim.Uma.PlayModeTests' }
    if ((@($summary.suites.suite | Sort-Object) -join '|') -cne (($expected | Sort-Object) -join '|')) { throw 'Incomplete or duplicate owned suites.' }
    foreach ($suite in $summary.suites) {
        Assert-ReviewEvidenceFile $suite.report $testedRoot $suite.reportSha256
        Assert-ReviewEvidenceFile $suite.log $testedRoot $suite.logSha256
        $actual=Read-ReviewSuiteReport $suite.report $suite.suite $suite.minimumCount $suite.exit ([bool]$suite.filtered)
        if (-not $actual.fullSuite -or $actual.total -ne $suite.total -or $actual.passed -ne $suite.passed) { throw 'Not complete passing suite evidence.' }
    }
    $baselineSha256=Assert-ReviewSuiteFloorEvidence $summary (Join-Path $PSScriptRoot 'baseline.txt') $expected $testedBefore $testedAfter
    $snapshot=New-ReviewSnapshot $projectRoot $runRoot
    $testedSnapshot=Assert-ReviewPlainPath $summary.snapshot
    if ($testedSnapshot -ne (Join-Path $testedRoot 'snapshot')) { throw 'Unexpected tested snapshot path.' }
    $testedRaw=Get-Content -LiteralPath (Join-Path $testedRoot 'snapshot.json') -Raw | ConvertFrom-Json
    Assert-ReviewManifestEqual $testedRaw (New-ReviewRawManifest $testedSnapshot)
    Assert-ReviewManifestEqual $testedRaw $snapshot.manifest
    $preview=New-ReviewInputManifest -ProjectRoot $snapshot.root -WorkflowRoot $snapshot.root -WithoutUma:$WithoutUma -PreviewSync -RetainedRoot $destination -RetainInstalledUma:$RetainInstalledUma
    Write-ReviewNewJson $preview (Join-Path $runRoot 'projection-before.json')
    $previewDrift=@(Compare-ReviewInputs -Before $testedBefore -After $preview -AllowShippingGpu -RequireWorkflowStable)
    Write-ReviewNewJson $previewDrift (Join-Path $runRoot 'projection-drift.json')
    Assert-ReviewRetainedVendorEqual $testedBefore $preview
    if (@($previewDrift | Where-Object {-not $_.allowed}).Count) { throw 'Current retained dependencies/product inputs differ from passing tests; nothing synced or built.' }
    & (Join-Path $PSScriptRoot 'sync-acceptance.ps1') -WithoutUma:$WithoutUma -RetainInstalledUma:$RetainInstalledUma -ValidationLease $lease -Snapshot $snapshot
    Assert-ReviewConfiguration $destination ([bool]$WithoutUma) 1
    $before=New-ReviewInputManifest -ProjectRoot $destination -WorkflowRoot $snapshot.root -WithoutUma:$WithoutUma -RetainInstalledUma:$RetainInstalledUma -MetaArchive (Join-Path $runRoot 'meta-before')
    Write-ReviewNewJson $before (Join-Path $runRoot 'inputs-before.json')
    $preDrift=@(Compare-ReviewInputs -Before $testedBefore -After $before -AllowShippingGpu -RequireWorkflowStable)
    Write-ReviewNewJson $preDrift (Join-Path $runRoot 'before-drift.json')
    if (@($preDrift | Where-Object {-not $_.allowed}).Count) { throw 'Synced inputs do not match passing tests.' }
    $version=(Get-Content -LiteralPath (Join-Path $destination 'ProjectSettings/ProjectVersion.txt') | Select-String '^m_EditorVersion:\s*(.+)$').Matches[0].Groups[1].Value.Trim()
    $unity=if ($env:GAMESIM_UNITY) {$env:GAMESIM_UNITY} else {"C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"}
    $rawReport=Join-Path $destination 'Logs/review13-build-report.json'
    $buildRoot=Join-Path $destination 'Builds/Port-Windows-Review13'
    # Preserve every byte of previous fixed-output build before the existing build owner overwrites it.
    $previous=@()
    if (Test-Path -LiteralPath $buildRoot) {
        foreach ($file in @(Get-ReviewFiles $buildRoot)) {
            $relative=$file.FullName.Substring($buildRoot.Length+1).Replace('\','/')
            $previous+=Get-ReviewFileRecord $file.FullName $relative (Join-Path $runRoot ('previous-build/'+$relative))
        }
    }
    if (Test-Path -LiteralPath $rawReport) { [void](Get-ReviewFileRecord $rawReport 'review13-build-report.json' (Join-Path $runRoot 'previous-build-report.json')) }
    Write-ReviewNewJson $previous (Join-Path $runRoot 'previous-build-tree.json')
    $log=Join-Path $runRoot 'build.log'
    $arguments=@('-batchmode','-quit','-accept-apiupdate','-projectPath',('"'+$destination+'"'),'-executeMethod',
        'Gamesim.Editor.U01ProjectSetup.BuildReviewCandidate','-logFile',('"'+$log+'"'))
    $buildStarted=[DateTime]::UtcNow
    $process=Invoke-ReviewOwnedProcess $unity $arguments $BuildTimeoutSeconds (Join-Path $runRoot 'build-process.json')
    if ($process.failure -or -not $process.closed -or $process.stopped -or $process.timedOut -or $process.exit -ne 0) { throw 'Owned build did not finish normally within its deadline.' }
    if (-not (Test-Path -LiteralPath $rawReport) -or (Get-Item -LiteralPath $rawReport).LastWriteTimeUtc -lt $buildStarted) { throw 'Missing fresh build report.' }
    [void](Get-ReviewFileRecord $rawReport 'build-report.json' (Join-Path $runRoot 'build-report.json'))
    $report=Get-Content -LiteralPath (Join-Path $runRoot 'build-report.json') -Raw | ConvertFrom-Json
    if ($report.result -cne 'Succeeded' -or $report.errors -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $buildRoot 'Gamesim.exe') -PathType Leaf)) { throw 'Build report/executable gate failed.' }
} catch { $failure=$_.Exception.Message }
finally {
    try {
        if ($snapshot) {
            try {
                $after=New-ReviewInputManifest -ProjectRoot $destination -WorkflowRoot $snapshot.root -WithoutUma:$WithoutUma -RetainInstalledUma:$RetainInstalledUma -MetaArchive (Join-Path $runRoot 'meta-after')
                Write-ReviewNewJson $after (Join-Path $runRoot 'inputs-after.json')
                if ($summary) { $postDrift=@(Compare-ReviewInputs -Before $testedAfter -After $after -AllowShippingGpu -AllowBuildSettings -RequireWorkflowStable) }
                Write-ReviewNewJson $postDrift (Join-Path $runRoot 'after-drift.json')
                $sourceFinal=New-ReviewRawManifest $projectRoot
                Write-ReviewNewJson $sourceFinal (Join-Path $runRoot 'source-final.json')
                Assert-ReviewManifestEqual $snapshot.original $sourceFinal
                Assert-ReviewSnapshot $snapshot
                Assert-ReviewSourceIdentity $snapshot
                Assert-ReviewRetainedVendorEqual $testedAfter $after
                Assert-ReviewEditorIdle $destination
                if ((Get-ReviewHash (Join-Path $PSScriptRoot 'baseline.txt')) -ne $baselineSha256) { throw 'Baseline changed during build.' }
            } catch { $failure=($failure+'; final audit: '+$_.Exception.Message) }
        }
        try {
            if (-not $failure -and $report -and @($postDrift | Where-Object {-not $_.allowed}).Count -eq 0) {
                $tree=@(Get-ReviewFiles $buildRoot | ForEach-Object {Get-ReviewFileRecord $_.FullName $_.FullName.Substring($buildRoot.Length+1).Replace('\','/')})
                Write-ReviewNewJson ([ordered]@{root=$buildRoot;files=$tree}) (Join-Path $runRoot 'build-tree.json')
                $accepted=$true
            }
        } catch { $failure=($failure+'; build output audit: '+$_.Exception.Message); $accepted=$false }
        $evidence=[ordered]@{schema=4;status=$(if ($accepted) {'Succeeded'} else {'Failed'});failure=$failure;runRoot=$runRoot;
            executable=$(if ($buildRoot) {Join-Path $buildRoot 'Gamesim.exe'} else {$null});umaEnabled=(-not $WithoutUma);
            retainedInstalledUma=[bool]$RetainInstalledUma;baselinePath=(Join-Path $PSScriptRoot 'baseline.txt');baselineSha256=$baselineSha256;
            suiteFloors=$(if ($summary) {$summary.suiteFloors} else {$null});testSummary=$(if ($summary) {$summaryPath} else {$null});
            testSummarySha256=$(if ($summary) {Get-ReviewHash $summaryPath} else {$null});
            inputsBefore=$(if ($before) {Join-Path $runRoot 'inputs-before.json'} else {$null});inputsBeforeSha256=$(if ($before) {Get-ReviewHash (Join-Path $runRoot 'inputs-before.json')} else {$null});
            inputsAfter=$(if ($after) {Join-Path $runRoot 'inputs-after.json'} else {$null});inputsAfterSha256=$(if ($after) {Get-ReviewHash (Join-Path $runRoot 'inputs-after.json')} else {$null});
            buildTree=$(if ($accepted) {Join-Path $runRoot 'build-tree.json'} else {$null});buildTreeSha256=$(if ($accepted) {Get-ReviewHash (Join-Path $runRoot 'build-tree.json')} else {$null});
            report=$(if ($report) {Join-Path $runRoot 'build-report.json'} else {$null});reportSha256=$(if ($report) {Get-ReviewHash (Join-Path $runRoot 'build-report.json')} else {$null});
            log=$(if ($process) {$log} else {$null});logSha256=$(if ($process -and (Test-Path -LiteralPath $log)) {Get-ReviewHash $log} else {$null});
            playerLaunched=$false;leaseToken=$(if ($lease) {$lease.token} else {$null})}
        Write-ReviewNewJson $evidence (Join-Path $runRoot 'evidence.json')
    } finally { Exit-ReviewLease $lease }
}
if (-not $accepted) { throw "Build/snapshot gate failed: $runRoot. $failure" }
$evidence | ConvertTo-Json -Depth 5
