# Full owned suites, complete inputs, and an explicit after-run drift audit.
param([Parameter(Mandatory=$true)][string]$Name, [switch]$WithoutUma, [string]$ApprovedMetaDrift)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'review-evidence.ps1')
if ($Name -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Use a simple, fresh artifact name.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$acceptanceRoot = (Resolve-Path -LiteralPath $(if ($env:GAMESIM_ACCEPTANCE) { $env:GAMESIM_ACCEPTANCE } else { 'D:\GamesimAcceptance' })).Path
$approvals = @(Read-ReviewApproval $ApprovedMetaDrift)
$logRoot = Join-Path $acceptanceRoot 'Logs'
if (Test-Path -LiteralPath $logRoot) {
    if (@(Get-ChildItem -LiteralPath $logRoot -Filter "$Name-*").Count -gt 0) { throw 'Use a fresh artifact name to retain previous evidence.' }
}
& (Join-Path $PSScriptRoot 'sync-acceptance.ps1') -DisableGpuResidentDrawer -WithoutUma:$WithoutUma
Assert-ReviewConfiguration $acceptanceRoot ([bool]$WithoutUma) 0
[void][IO.Directory]::CreateDirectory($logRoot)
$manifestPath = Join-Path $logRoot "$Name-source.json"
$afterPath = Join-Path $logRoot "$Name-source-after.json"
$driftPath = Join-Path $logRoot "$Name-drift.json"
$summaryPath = Join-Path $logRoot "$Name-summary.json"
$before = New-ReviewInputManifest -ProjectRoot $acceptanceRoot -WorkflowRoot $projectRoot -WithoutUma:$WithoutUma -MetaArchive (Join-Path $logRoot "$Name-meta-before")
Write-ReviewJson $before $manifestPath
$version = (Get-Content -LiteralPath (Join-Path $acceptanceRoot 'ProjectSettings/ProjectVersion.txt') |
    Select-String '^m_EditorVersion:\s*(.+)$').Matches[0].Groups[1].Value.Trim()
$unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
$summaries = @(); $failure = $null
$suites = @(@{id='edit';platform='EditMode';assembly='Gamesim.EditModeTests'},
    @{id='play';platform='PlayMode';assembly='Gamesim.PlayModeTests'})
if (-not $WithoutUma) { $suites += @{id='uma';platform='PlayMode';assembly='Gamesim.Uma.PlayModeTests'} }
try {
    if (-not (Test-Path -LiteralPath $unity)) { throw "Unity is not installed: $unity" }
    foreach ($suite in $suites) {
        $xmlPath = Join-Path $logRoot "$Name-$($suite.id).xml"
        $logPath = Join-Path $logRoot "$Name-$($suite.id).log"
        $arguments = @('-batchmode','-accept-apiupdate','-projectPath',('"'+$acceptanceRoot+'"'),'-runTests',
            '-testPlatform',$suite.platform,'-assemblyNames',$suite.assembly,'-testResults',('"'+$xmlPath+'"'),'-logFile',('"'+$logPath+'"'))
        Write-Output "Running $($suite.assembly); log: $logPath"
        $process = Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
        if (-not (Test-Path -LiteralPath $xmlPath)) { throw "No test report; Unity exit $($process.ExitCode): $logPath" }
        [xml]$result = Get-Content -LiteralPath $xmlPath
        $run = $result.'test-run'
        $assemblies = @($result.SelectNodes('//test-suite[@type="Assembly"]'))
        if ($assemblies.Count -ne 1 -or $assemblies[0].name -ne ($suite.assembly + '.dll')) { throw "Wrong test assembly: $xmlPath" }
        $cases = @($result.SelectNodes('//test-case'))
        if ([int]$run.total -eq 0 -or $cases.Count -ne [int]$run.total) { throw "Empty or inconsistent test report: $xmlPath" }
        $entry = [pscustomobject]@{suite=$suite.assembly;total=[int]$run.total;passed=[int]$run.passed;failed=[int]$run.failed;
            skipped=[int]$run.skipped;exit=$process.ExitCode;report=$xmlPath;reportSha256=(Get-ReviewHash $xmlPath);
            log=$logPath;logSha256=(Get-ReviewHash $logPath);testNames=@($cases.fullname | Sort-Object)}
        $summaries += $entry
        Write-Output "$($entry.suite): $($entry.passed)/$($entry.total) passed; $($entry.failed) failed, $($entry.skipped) skipped; exit $($entry.exit)"
        foreach ($case in $result.SelectNodes('//test-case[@result="Failed"]')) { Write-Output "FAILED: $($case.fullname)" }
    }
} catch { $failure = $_.Exception.Message }
finally {
    $after = New-ReviewInputManifest -ProjectRoot $acceptanceRoot -WorkflowRoot $projectRoot -WithoutUma:$WithoutUma -MetaArchive (Join-Path $logRoot "$Name-meta-after")
    Write-ReviewJson $after $afterPath
    $changes = @(Compare-ReviewInputs -Before $before -After $after -AllowImportMeta -ApprovedMetaDrift $approvals)
    Write-ReviewJson ([ordered]@{before=$manifestPath;after=$afterPath;approvedMetaDrift=$approvals;changes=$changes}) $driftPath
    $passed = -not $failure -and $summaries.Count -eq $suites.Count -and
        @($summaries | Where-Object { $_.failed -ne 0 -or $_.skipped -ne 0 -or $_.passed -ne $_.total -or $_.exit -ne 0 }).Count -eq 0 -and
        @($changes | Where-Object { -not $_.allowed }).Count -eq 0
    Write-ReviewJson ([ordered]@{schema=3;name=$Name;status=$(if ($passed) {'Passed'} else {'Failed'});umaEnabled=(-not $WithoutUma);
        sourceManifest=$manifestPath;sourceManifestSha256=(Get-ReviewHash $manifestPath);
        finalSourceManifest=$afterPath;finalSourceManifestSha256=(Get-ReviewHash $afterPath);
        driftReport=$driftPath;driftReportSha256=(Get-ReviewHash $driftPath);failure=$failure;suites=$summaries}) $summaryPath
}
if (-not $passed) { throw "Candidate suites or source-drift audit failed: $summaryPath. $failure" }
Write-Output "All owned suites passed with accounted input drift: $summaryPath"
