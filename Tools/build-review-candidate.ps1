# Builds only the inputs proven by a named fully passing test run. No player is launched.
param([Parameter(Mandatory=$true)][string]$TestName, [switch]$WithoutUma)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'review-evidence.ps1')
if ($TestName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Supply the simple name of the final passing test run.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$acceptanceRoot = (Resolve-Path -LiteralPath $(if ($env:GAMESIM_ACCEPTANCE) { $env:GAMESIM_ACCEPTANCE } else { 'D:\GamesimAcceptance' })).Path
$summaryPath = Join-Path $acceptanceRoot "Logs/$TestName-summary.json"
$summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
# Schema 3 changed what buildSettingsSha256 means, so evidence captured by an earlier version
# is not comparable with a manifest captured now: the hashes would differ for a file that had
# not changed, and the audit would refuse a sound build for the wrong reason. Rejecting the
# stale summary outright says so, rather than leaving that to be read out of a drift report.
if ($summary.schema -ne 3 -or $summary.status -ne 'Passed' -or $summary.umaEnabled -eq [bool]$WithoutUma) {
    throw 'A fully passing schema3 summary for the same UMA configuration is required; re-run verify-review-candidate.ps1 with a fresh name if the evidence predates the current input accounting.'
}
foreach ($pair in @(@($summary.sourceManifest,$summary.sourceManifestSha256),
    @($summary.finalSourceManifest,$summary.finalSourceManifestSha256), @($summary.driftReport,$summary.driftReportSha256))) {
    if ((Get-ReviewHash $pair[0]) -ne $pair[1]) { throw "Test evidence changed: $($pair[0])" }
}
$expectedSuites = @('Gamesim.EditModeTests','Gamesim.PlayModeTests')
if (-not $WithoutUma) { $expectedSuites += 'Gamesim.Uma.PlayModeTests' }
if ((@($summary.suites.suite | Sort-Object) -join '|') -ne (($expectedSuites | Sort-Object) -join '|')) { throw 'Incomplete owned test suites.' }
foreach ($suite in $summary.suites) {
    if ($suite.total -le 0 -or $suite.passed -ne $suite.total -or $suite.failed -ne 0 -or $suite.skipped -ne 0 -or $suite.exit -ne 0 -or
            (Get-ReviewHash $suite.report) -ne $suite.reportSha256 -or (Get-ReviewHash $suite.log) -ne $suite.logSha256) {
        throw "Missing, failed or changed test evidence: $($suite.suite)"
    }
    [xml]$xml = Get-Content -LiteralPath $suite.report
    if ([int]$xml.'test-run'.total -ne $suite.total -or [int]$xml.'test-run'.passed -ne $suite.total) { throw 'Test totals do not match their XML.' }
}
$testedBefore = Get-Content -LiteralPath $summary.sourceManifest -Raw | ConvertFrom-Json
$testedAfter = Get-Content -LiteralPath $summary.finalSourceManifest -Raw | ConvertFrom-Json
if ($testedBefore.schema -ne 3 -or $testedAfter.schema -ne 3) {
    throw 'The tested input manifests are not schema3; re-run verify-review-candidate.ps1 with a fresh name.'
}
$stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$prefix = Join-Path $acceptanceRoot "Logs/review13-build-$stamp"
$preview = New-ReviewInputManifest -ProjectRoot $projectRoot -WorkflowRoot $projectRoot -WithoutUma:$WithoutUma -PreviewSync -RetainedRoot $acceptanceRoot
Write-ReviewJson $preview "$prefix-live-inputs.json"
$previewDrift = @(Compare-ReviewInputs -Before $testedBefore -After $preview -AllowShippingGpu)
Write-ReviewJson $previewDrift "$prefix-live-drift.json"
if (@($previewDrift | Where-Object { -not $_.allowed }).Count -ne 0) { throw "Live product inputs differ from the passing snapshot: $prefix-live-drift.json. Nothing synced or built." }
& (Join-Path $PSScriptRoot 'sync-acceptance.ps1') -WithoutUma:$WithoutUma
Assert-ReviewConfiguration $acceptanceRoot ([bool]$WithoutUma) 1
$before = New-ReviewInputManifest -ProjectRoot $acceptanceRoot -WorkflowRoot $projectRoot -WithoutUma:$WithoutUma -MetaArchive "$prefix-meta-before"
Write-ReviewJson $before "$prefix-inputs-before.json"
$preDrift = @(Compare-ReviewInputs -Before $testedBefore -After $before -AllowShippingGpu)
Write-ReviewJson $preDrift "$prefix-before-drift.json"
if (@($preDrift | Where-Object { -not $_.allowed }).Count -ne 0) { throw "Synced product inputs differ from passing tests: $prefix-before-drift.json" }
$version = (Get-Content -LiteralPath (Join-Path $acceptanceRoot 'ProjectSettings/ProjectVersion.txt') |
    Select-String '^m_EditorVersion:\s*(.+)$').Matches[0].Groups[1].Value.Trim()
$unityPath = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
if (-not (Test-Path -LiteralPath $unityPath)) { throw "Unity is not installed: $unityPath" }
$log = "$prefix.log"
$arguments = @('-batchmode','-quit','-accept-apiupdate','-projectPath',('"'+$acceptanceRoot+'"'),'-executeMethod',
    'Gamesim.Editor.U01ProjectSetup.BuildReviewCandidate','-logFile',('"'+$log+'"'))
$buildStarted = [DateTime]::UtcNow
$failure = $null; $process = $null
try { $process = Start-Process -FilePath $unityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait }
catch { $failure = $_.Exception.Message }
finally {
    $after = New-ReviewInputManifest -ProjectRoot $acceptanceRoot -WorkflowRoot $projectRoot -WithoutUma:$WithoutUma -MetaArchive "$prefix-meta-after"
    Write-ReviewJson $after "$prefix-inputs-after.json"
    $postDrift = @(Compare-ReviewInputs -Before $testedAfter -After $after -AllowShippingGpu -AllowBuildSettings)
    Write-ReviewJson $postDrift "$prefix-after-drift.json"
}
$rawReport = Join-Path $acceptanceRoot 'Logs/review13-build-report.json'
$reportPath = "$prefix-report.json"
if (Test-Path -LiteralPath $rawReport) {
    if ((Get-Item -LiteralPath $rawReport).LastWriteTimeUtc -ge $buildStarted) { Copy-Item -LiteralPath $rawReport -Destination $reportPath }
}
if ($failure -or -not $process -or $process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $reportPath)) {
    throw "Build failed or produced no fresh report. $failure See $log. No executable launched."
}
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if ($report.result -ne 'Succeeded' -or $report.errors -ne 0 -or @($postDrift | Where-Object { -not $_.allowed }).Count -ne 0) {
    throw "Build or post-build input audit failed: $reportPath; $prefix-after-drift.json"
}
$buildRoot = Join-Path $acceptanceRoot 'Builds/Port-Windows-Review13'
$exe = Join-Path $buildRoot 'Gamesim.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Successful report has no player executable.' }
$buildFiles = @(Get-ChildItem -LiteralPath $buildRoot -File -Recurse -Force | ForEach-Object {
    [pscustomobject]@{path=$_.FullName.Substring($buildRoot.Length+1).Replace('\','/');bytes=$_.Length;sha256=(Get-ReviewHash $_.FullName)}
} | Sort-Object path)
Write-ReviewJson ([ordered]@{capturedUtc=[DateTime]::UtcNow.ToString('o');root=$buildRoot;files=$buildFiles}) "$prefix-build-tree.json"
$evidence = [ordered]@{status='Succeeded';executable=$exe;umaEnabled=(-not $WithoutUma);
    testSummary=$summaryPath;testSummarySha256=(Get-ReviewHash $summaryPath);report=$reportPath;reportSha256=(Get-ReviewHash $reportPath);
    inputsBefore="$prefix-inputs-before.json";inputsBeforeSha256=(Get-ReviewHash "$prefix-inputs-before.json");
    inputsAfter="$prefix-inputs-after.json";inputsAfterSha256=(Get-ReviewHash "$prefix-inputs-after.json");
    buildTree="$prefix-build-tree.json";buildTreeSha256=(Get-ReviewHash "$prefix-build-tree.json");log=$log;logSha256=(Get-ReviewHash $log)}
Write-ReviewJson $evidence "$prefix-evidence.json"
$evidence | ConvertTo-Json -Depth 5
