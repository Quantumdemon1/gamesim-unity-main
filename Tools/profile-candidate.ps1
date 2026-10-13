# Profiles a built player against the owner's performance target: 1920x1080 at 60 fps on the target
# machine (OWNER_COMPLETION_DECISIONS, 2026-10-04). Three clean runs, one at a time, each into its own
# fresh save root: the six-person house, the twelve-person house, and the sixteen-person stress house
# (verification-only, both rosters: --gamesim-stress-roster). Every run is the player's own
# --gamesim-verify profile: windowed, uncapped, VSync off, 1920x1080, the steady sample after the
# startup. NEVER -batchmode or -nographics: a headless frame is not presentation.
#
# The verdict is each report's performanceAcceptance ("Met" / "Not met" against p95 <= 16.7 ms and
# p99 <= 33.3 ms, or "Not assessed: <reasons>"), taken only from a valid run: one whose report exists,
# measured the house asked for, had nothing competing beside it and passed its own checks. Any other
# run is "Not assessed" in the summary, whatever the player said. A report's status of Passed is its
# execution checks only, never the verdict. This script writes profile-summary.json and
# profile-summary.md beside the three run folders and exits 0 only when the evidence is complete:
# three valid runs, of one build (one buildGuid), whose whole tree matched -BuildEvidence file by
# file. The exit code is the evidence's completeness, never the performance verdict.
#
# Refuses to start while a Unity Editor, unknown Unity.exe or Gamesim.exe is running (close them yourself,
# saving any work). Only the exact installed Hub executable's literal quoted serve command is classified
# separately; it is retained in host telemetry, not a claim that CPU/GPU background work is globally quiet.
# Records known-name processes seen during a run. Without -BuildEvidence the runs are still made, but the
# evidence is incomplete: Gamesim.exe alone is Unity's player stub and does not identify a build.
#
#   powershell -NoProfile -File Tools/profile-candidate.ps1
#   powershell -NoProfile -File Tools/profile-candidate.ps1 -Executable D:\GamesimAcceptance\Builds\Port-Windows-Review13\Gamesim.exe `
#       -BuildEvidence D:\GamesimAcceptance\Logs\review13-build-<stamp>-evidence.json
param(
    [string]$Executable,
    [string]$OutputRoot,
    [string]$BuildEvidence,
    [ValidateRange(10, 1800)][double]$Seconds = 300,
    [ValidateRange(60, 3600)][int]$StartupAllowanceSeconds = 600
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'review-evidence.ps1')
. (Join-Path $PSScriptRoot 'review-workflow.ps1')
. (Join-Path $PSScriptRoot 'profile-evidence.ps1')
$invariant = [Globalization.CultureInfo]::InvariantCulture

$acceptanceRoot = if ($env:GAMESIM_ACCEPTANCE) { $env:GAMESIM_ACCEPTANCE } else { 'D:\GamesimAcceptance' }
if (-not $Executable) { $Executable = Join-Path $acceptanceRoot 'Builds\Port-Windows-Review13\Gamesim.exe' }
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) { throw "No player executable at $Executable. Build one with build-review-candidate.ps1 first." }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
if ([IO.Path]::GetFileName($Executable) -ne 'Gamesim.exe') { throw "Profile the built Gamesim.exe, not $Executable." }

$script:profileKnownProcesses = @{}
$profileProgramFiles = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
function Get-CompetingProcesses {
    # CIM supplies executable/command/creation identity. Never publish an unknown Unity command line:
    # editor launch arguments can carry credentials. A failed query cannot become a clean sample.
    foreach ($item in @(Get-CimInstance -ClassName Win32_Process -Filter "Name='Unity.exe' OR Name='Gamesim.exe'" -ErrorAction Stop)) {
        $ignored = Test-ProfileUnityHubServeProcess $item $profileProgramFiles
        $startedUtc = if ($item.CreationDate -is [DateTime]) { $item.CreationDate.ToUniversalTime().ToString('o') } else { $null }
        $key = [string]$item.ProcessId+'|'+$startedUtc+'|'+[string]$item.ExecutablePath+'|'+[string]$ignored
        $now = [DateTime]::UtcNow.ToString('o')
        if (-not $script:profileKnownProcesses.ContainsKey($key)) {
            $script:profileKnownProcesses[$key] = [ordered]@{name=[string]$item.Name;pid=$item.ProcessId;startedUtc=$startedUtc;
                executable=[string]$item.ExecutablePath;firstSeenUtc=$now;lastSeenUtc=$now;
                classification=$(if ($ignored) {'ExactUnityHubServeHelper'} else {'Competitor'});
                exactInstalledHubServeMatch=[bool]$ignored}
        } else { $script:profileKnownProcesses[$key].lastSeenUtc=$now }
        if (-not $ignored) {
            [pscustomobject]@{ProcessName=[IO.Path]::GetFileNameWithoutExtension([string]$item.Name);Id=$item.ProcessId;
                executable=[string]$item.ExecutablePath;startedUtc=$startedUtc}
        }
    }
}
function Assert-NothingCompeting([string]$When) {
    $competing = @(Get-CompetingProcesses)
    if ($competing.Count -gt 0) {
        $names = ($competing | ForEach-Object { "$($_.ProcessName).exe (pid $($_.Id))" }) -join ', '
        throw "Refusing to profile ${When}: $names running. A clean performance run has no editor, build or other game beside it; close them yourself (saving any work) and run again."
    }
}
if (-not $OutputRoot) { $OutputRoot = Join-Path $acceptanceRoot ('Logs\profile-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ', $invariant) + '-' + [Guid]::NewGuid().ToString('N')) }
Assert-ReviewPlainPath $OutputRoot | Out-Null
if ((Test-Path -LiteralPath $OutputRoot) -and @(Get-ChildItem -LiteralPath $OutputRoot -Force).Count -ne 0) {
    throw "The output root already holds files: $OutputRoot. Every profile gets a fresh one."
}
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$OutputRoot = (Resolve-Path -LiteralPath $OutputRoot).Path
if ($Seconds -lt 300) { Write-Warning "A $Seconds s sample is shorter than the 300 s the verdict requires; every run will read 'Not assessed'." }
$lease = $null
$ownedProcess = $null
$results = @()
try {
    $lease = Enter-ReviewLease $acceptanceRoot
    Assert-NothingCompeting 'at all'

# ------------------------------------------------------------------ what is being measured, and where

$exeItem = Get-Item -LiteralPath $Executable
$exeHash = Get-ReviewHash $Executable
$buildRoot = Split-Path -Parent $Executable
$build = [ordered]@{ executable = $Executable; root = $buildRoot; sha256 = $exeHash; bytes = $exeItem.Length; lastWriteUtc = $exeItem.LastWriteTimeUtc.ToString('o', $invariant);
    fileVersion = $exeItem.VersionInfo.FileVersion; productVersion = $exeItem.VersionInfo.ProductVersion;
    evidence = $null; evidenceSha256 = $null; evidenceTree = $null; evidenceTreeFiles = $null; evidenceMatches = $false }
if ($BuildEvidence) {
    # The whole tree, not the launcher: Gamesim.exe is Unity's player stub, identical across builds
    # of one Unity and product version. The game is UnityPlayer.dll and Gamesim_Data/**.
    $BuildEvidence = (Resolve-Path -LiteralPath $BuildEvidence).Path
    $evidence = Get-Content -LiteralPath $BuildEvidence -Raw | ConvertFrom-Json
    $build.evidence = $BuildEvidence
    $build.evidenceSha256 = Get-ReviewHash $BuildEvidence
    $build.evidenceTree = $evidence.buildTree
    $tree = Get-Content -LiteralPath $evidence.buildTree -Raw | ConvertFrom-Json
    $build.evidenceTreeFiles = @($tree.files).Count
    $treeIntact = (Get-ReviewHash $evidence.buildTree) -eq $evidence.buildTreeSha256
    Write-Host "Hashing the player's build tree against $($evidence.buildTree) ($($build.evidenceTreeFiles) files)"
    $differences = @(Compare-ReviewBuildTree $buildRoot $tree.files)
    $build.evidenceMatches = ($evidence.status -eq 'Succeeded') -and $treeIntact -and ($differences.Count -eq 0) -and
        (@($tree.files | Where-Object { $_.path -eq 'Gamesim.exe' }).Count -eq 1)
    if (-not $build.evidenceMatches) {
        $shown = @($differences | Select-Object -First 10) -join '; '
        throw "The player at $buildRoot is not the build its evidence recorded ($BuildEvidence): status $($evidence.status), tree record intact $treeIntact, $($differences.Count) file differences$(if ($shown) { ': ' + $shown } else { '' })."
    }
} else {
    Write-Warning 'No -BuildEvidence: the runs are made, but without the build they measured the evidence is incomplete.'
}

function Get-HostIdentity {
    $gpus = @(Get-CimInstance -ClassName Win32_VideoController -ErrorAction SilentlyContinue | ForEach-Object {
        [ordered]@{ name = $_.Name; driverVersion = $_.DriverVersion; driverDate = "$($_.DriverDate)";
            display = "$($_.CurrentHorizontalResolution)x$($_.CurrentVerticalResolution)"; refreshHz = $_.CurrentRefreshRate } })
    $cpu = @(Get-CimInstance -ClassName Win32_Processor -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
    $os = Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction SilentlyContinue
    [ordered]@{ computer = $env:COMPUTERNAME; gpus = $gpus; processors = $cpu;
        operatingSystem = if ($os) { "$($os.Caption) $($os.Version)" } else { $null };
        memoryBytes = if ($os) { [int64]$os.TotalVisibleMemorySize * 1024 } else { $null };
        powerScheme = ((powercfg /getactivescheme 2>$null) -join ' ').Trim();
        knownNameProcessObservations = @($script:profileKnownProcesses.Values | Sort-Object name,pid,startedUtc);
        ignoredUnityHubHelpers = @($script:profileKnownProcesses.Values | Where-Object classification -CEQ 'ExactUnityHubServeHelper' | Sort-Object pid,startedUtc);
        processClassifier = [ordered]@{queriedNames=@('Unity.exe','Gamesim.exe');expectedHubExecutable=(Join-Path $profileProgramFiles 'Unity Hub\resources\unity.exe');
            policy='Only exact installed executable plus exact quoted serve command with complete CIM identity is a non-editor helper. All other monitored names compete except this script''s exact owned player during its own sample.';
            limitation='This known-name classification does not establish globally quiet CPU/GPU or eliminate the helper''s background cost.'} }
}

# A run's verdict counts only when the run is valid evidence: its report was read, it measured the
# house asked for in a season it started, nothing competed beside it, and its own checks Passed.
# ------------------------------------------------------------------ the three runs, one at a time

$runs = @(
    [ordered]@{ name = 'house-06'; houseSize = 6; stress = $false; purpose = 'six-person gameplay' },
    [ordered]@{ name = 'house-12'; houseSize = 12; stress = $false; purpose = 'twelve-person gameplay' },
    [ordered]@{ name = 'house-16-stress'; houseSize = 16; stress = $true; purpose = 'sixteen-person stress (verification-only roster)' }
)
$results = @()
foreach ($run in $runs) {
    Assert-NothingCompeting "the $($run.name) run"
    $root = Join-Path $OutputRoot $run.name
    New-Item -ItemType Directory -Path $root | Out-Null
    $log = Join-Path $root 'player.log'
    $arguments = @('--gamesim-verify', '--gamesim-save-root', ('"' + $root + '"'),
        '--gamesim-profile-seconds', $Seconds.ToString('0.###', $invariant),
        '--gamesim-house-size', $run.houseSize.ToString($invariant),
        '-screen-fullscreen', '0', '-screen-width', '1920', '-screen-height', '1080',
        '-logFile', ('"' + $log + '"'))
    if ($run.stress) { $arguments += '--gamesim-stress-roster' }
    if (@($arguments | Where-Object { $_ -match '^-(batchmode|nographics)$' }).Count -ne 0) { throw 'A performance run is never headless.' }

    $started = [DateTime]::UtcNow
    Write-Host "Profiling $($run.purpose): $($Seconds) s steady sample into $root"
    $process = Start-Process -FilePath $Executable -ArgumentList $arguments -WorkingDirectory (Split-Path -Parent $Executable) -PassThru
    $ownedProcess = $process
    $null = $process.Handle   # Cached now, or Windows PowerShell reports no exit code once the player has gone.
    $deadline = $started.AddSeconds($Seconds + $StartupAllowanceSeconds)
    $competingSeen = @{}
    while (-not $process.WaitForExit(0) -and [DateTime]::UtcNow -lt $deadline) {
        foreach ($other in Get-CompetingProcesses) {
            if ($other.Id -ne $process.Id) { $competingSeen["$($other.ProcessName).exe (pid $($other.Id))"] = $true }
        }
        Start-Sleep -Seconds 5
    }
    $ending = 'exited'
    if (-not $process.WaitForExit(0)) {
        # Past the steady sample and the whole startup allowance: ask the window to close, and only
        # then end the one process this script started.
        $null = $process.CloseMainWindow()
        if ($process.WaitForExit(10000)) { $ending = 'closed after the deadline' }
        else {
            $process.Kill()
            if (-not $process.WaitForExit(5000)) { throw 'The owned isolated profile player did not close within its bounded cleanup.' }
            $ending = 'stopped after the deadline'
        }
    }
    if (-not $process.WaitForExit(0)) { throw 'The owned profile player has not signaled completion.' }
    $finished = $process.ExitTime.ToUniversalTime()
    $deadlineMet = $finished -le $deadline
    if (-not $deadlineMet -and $ending -eq 'exited') { $ending = 'exited after the deadline' }

    $reportPath = Join-Path $root 'verification.json'
    $report = $null
    $reportError = $null
    if (Test-Path -LiteralPath $reportPath) {
        # One unreadable report is that run's failure, not the end of the other runs or the summary.
        try { $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json }
        catch { $reportError = "verification.json could not be read: $($_.Exception.Message)" }
    }
    $result = [ordered]@{
        name = $run.name; purpose = $run.purpose; houseSizeRequested = $run.houseSize; stressRoster = $run.stress
        saveRoot = $root; arguments = ($arguments -join ' '); startedUtc = $started.ToString('o', $invariant); finishedUtc = $finished.ToString('o', $invariant)
        ending = $ending; exitCode = $process.ExitCode; competingProcesses = @($competingSeen.Keys | Sort-Object)
        deadlineUtc = $deadline.ToString('o', $invariant); deadlineMet = $deadlineMet
        actualProcessSeconds = ($finished - $process.StartTime.ToUniversalTime()).TotalSeconds
        report = if ($report) { $reportPath } else { $null }; reportSha256 = if (Test-Path -LiteralPath $reportPath) { Get-ReviewHash $reportPath } else { $null }
        reportError = $reportError
        reportFresh = if ($report) { Test-ProfileReportFresh $reportPath $report $started $finished } else { $false }
        rawFramesBound = $false
        ignoredUnityHubHelpers = @($script:profileKnownProcesses.Values | Where-Object {
            $_.classification -ceq 'ExactUnityHubServeHelper' -and [DateTimeOffset]::Parse($_.lastSeenUtc).UtcDateTime -ge $started
        } | Sort-Object pid,startedUtc)
    }
    if ($report) {
        $result.performanceAcceptance = $report.performanceAcceptance
        $result.performanceAcceptanceBasis = $report.performanceAcceptanceBasis
        $result.status = $report.status
        $result.houseSize = $report.houseSize
        $result.houseSizeNote = $report.houseSizeNote
        $result.reportStressRoster = $report.stressRoster
        $result.seasonSource = $report.seasonSource
        $result.seasonSeed = $report.seasonSeed
        $result.sessionId = $report.sessionId
        # A request the player clamped is not the measurement asked for: twelve is not sixteen. Nor is
        # the scene's own season, which plays with every rule off: each run is a season it started.
        $result.houseSizeMatches = ($report.houseSize -eq $run.houseSize) -and ([bool]$report.stressRoster -eq $run.stress) -and
            ($report.seasonSource -eq $(if ($run.stress) { 'stress' } else { 'director' }))
        $result.measuredSeconds = $report.measuredSeconds
        $result.frameCount = $report.frameCount
        $result.averageFps = $report.averageFps
        $result.frameMedianMs = $report.frameMedianMs
        $result.frameP95Ms = $report.frameP95Ms
        $result.frameP99Ms = $report.frameP99Ms
        $result.frameP999Ms = $report.frameP999Ms
        $result.frameMaxMs = $report.frameMaxMs
        $result.slowFramesOver16_7Ms = $report.slowFramesOver16_7Ms
        $result.slowFramesOver33_3Ms = $report.slowFramesOver33_3Ms
        $result.slowFramesOver50Ms = $report.slowFramesOver50Ms
        $result.frameHistogramUpperMs = $report.frameHistogramUpperMs
        $result.frameHistogramCounts = $report.frameHistogramCounts
        $result.startupFrames = $report.startup.frames
        $result.startupMaxMs = $report.startup.maxMs
        $result.startupFramesOver50Ms = $report.startup.framesOver50Ms
        $result.startupSampleBeganSeconds = $report.startupSampleBeganSeconds
        $result.startupStages = $report.startupStages
        $result.bodyAssemblySeconds = $report.bodyAssemblySeconds
        $result.bodiesAssembled = $report.bodiesAssembled
        $result.memoryPeak = $report.memoryPeak
        $result.memoryAtSampleStart = $report.memoryAtSampleStart
        $result.memoryAtSampleEnd = $report.memoryAtSampleEnd
        $result.sampledResolution = $report.sampledResolution
        $result.sampledResolutionMismatchCount = $report.sampledResolutionMismatchCount
        $result.sampledDisplayModeMismatchCount = $report.sampledDisplayModeMismatchCount
        $result.sampledFrameCapMismatchCount = $report.sampledFrameCapMismatchCount
        $result.displayResolution = $report.displayResolution
        $result.qualityLevelName = $report.qualityLevelName
        $result.renderPipeline = $report.renderPipeline
        $result.renderScale = $report.renderScale
        $result.buildVersion = $report.buildVersion
        $result.buildGuid = $report.buildGuid
        $result.developmentBuild = $report.developmentBuild
        $result.gpu = $report.gpu
        $result.graphicsDeviceVersion = $report.graphicsDeviceVersion
        $result.errors = @($report.errors).Count
        $result.rawFrameTimesFile = $report.rawFrameTimesFile
        try {
            $raw = Assert-ProfileArtifactPath $root ([string]$report.rawFrameTimesFile)
            $rawItem = Get-Item -LiteralPath $raw
            $result.rawFrameTimesSha256 = Get-ReviewHash $raw
            $result.rawFramesBound = $rawItem.LastWriteTimeUtc -ge $started -and $rawItem.LastWriteTimeUtc -le $finished.AddSeconds(2)
        } catch { $result.rawFrameTimesSha256 = $null; $result.rawFramesError = $_.Exception.Message }
    }
    $result.valid = Test-ProfileRunValid $result
    $result.verdict = Get-ProfileRunVerdict $result
    $results += $result
    Write-ReviewNewJson $result (Join-Path $root 'run-evidence.json')
    $process.Dispose(); $ownedProcess = $null
}

# ------------------------------------------------------------------ the summary

function Get-CombinedVerdict($Verdicts) {
    $list = @($Verdicts)
    if (@($list | Where-Object { $_ -ne 'Met' }).Count -eq 0 -and $list.Count -gt 0) { return 'Met' }
    if (@($list | Where-Object { $_ -eq 'Not met' }).Count -gt 0) { return 'Not met' }
    return 'Not assessed'
}
# One build behind all three reports: the player's own buildGUID, beside the tree checked above.
$buildGuids = @($results | Where-Object { $_.report } | ForEach-Object { [string]$_.buildGuid } | Sort-Object -Unique)
$oneBuild = ($buildGuids.Count -eq 1) -and -not [string]::IsNullOrEmpty($buildGuids[0]) -and (@($results | Where-Object { -not $_.report }).Count -eq 0)
$buildAfterMatches = $false
if ($build.evidenceMatches) { $buildAfterMatches = @(Compare-ReviewBuildTree $buildRoot $tree.files).Count -eq 0 }
$targetGpuMatches = @($results | Where-Object { $_.gpu -notmatch '(?i)GTX\s*1060' }).Count -eq 0
$complete = $results.Count -eq 3 -and (@($results | Where-Object { -not $_.valid }).Count -eq 0) -and
    [bool]$build.evidenceMatches -and $buildAfterMatches -and $oneBuild -and $targetGpuMatches -and $Seconds -ge 300
$gameplay = @($results | Where-Object { -not $_.stressRoster } | ForEach-Object { $_.verdict })
$stress = @($results | Where-Object { $_.stressRoster } | ForEach-Object { $_.verdict })
$summary = [ordered]@{
    schema = 3; createdUtc = [DateTime]::UtcNow.ToString('o', $invariant); outputRoot = $OutputRoot
    target = [ordered]@{ resolution = '1920x1080'; window = 'Windowed'; uncapped = $true; p95LimitMs = 16.7; p99LimitMs = 33.3; minimumSeconds = 300; requestedSeconds = $Seconds }
    evidenceComplete = $complete
    buildEvidenceMatches = [bool]$build.evidenceMatches
    buildAfterMatches = $buildAfterMatches
    targetGpuMatches = $targetGpuMatches
    buildGuids = $buildGuids
    oneBuild = $oneBuild
    gameplayVerdict = Get-CombinedVerdict $gameplay
    stressVerdict = Get-CombinedVerdict $stress
    note = "Each run's verdict is its report's performanceAcceptance when the run is valid (report read, the house asked for in a season the player started, nothing competing, its own checks Passed), and 'Not assessed' otherwise. A report's status of Passed is its execution checks only. gameplayVerdict is Met only when the six- and twelve-person runs are both Met; the sixteen-person stress run uses a verification-only roster and is reported apart. evidenceComplete also needs -BuildEvidence matched file by file and one buildGuid across the reports."
    build = $build; host = Get-HostIdentity; runs = $results
}
$jsonPath = Join-Path $OutputRoot 'profile-summary.json'
Write-ReviewJson $summary $jsonPath

function Format-Ms($Value) { if ($null -eq $Value) { '-' } else { ([double]$Value).ToString('0.0', $invariant) } }
function Format-MiB($Value) { if ($null -eq $Value -or [int64]$Value -lt 0) { '-' } else { ([int64]$Value / 1MB).ToString('0', $invariant) } }
$md = New-Object Text.StringBuilder
[void]$md.AppendLine('# Performance profile')
[void]$md.AppendLine()
[void]$md.AppendLine("Target: 1920x1080 windowed, uncapped, VSync off; p95 <= 16.7 ms and p99 <= 33.3 ms over a steady sample of at least 300 s. A report's ``Passed`` is its execution checks, not this verdict.")
[void]$md.AppendLine()
[void]$md.AppendLine("- **Six- and twelve-person gameplay: $($summary.gameplayVerdict)**")
[void]$md.AppendLine("- **Sixteen-person stress (verification-only roster): $($summary.stressVerdict)**")
[void]$md.AppendLine("- Evidence complete (three valid runs, one buildGuid, the build tree matched file by file): $complete")
[void]$md.AppendLine("- Executable: ``$($build.executable)`` sha256 ``$($build.sha256)``" + $(if ($build.evidence) { "; build evidence ``$($build.evidence)``: all $($build.evidenceTreeFiles) files of ``$($build.root)`` match" } else { '; **no build evidence named** - the launcher alone does not identify a build' }))
[void]$md.AppendLine("- Build GUIDs in the reports: $(if ($buildGuids.Count -gt 0) { $buildGuids -join ', ' } else { 'none' }) (one build: $oneBuild)")
[void]$md.AppendLine("- Host: $($summary.host.computer); $(($summary.host.gpus | ForEach-Object { "$($_.name) (driver $($_.driverVersion), display $($_.display) @ $($_.refreshHz) Hz)" }) -join '; '); $($summary.host.processors -join '; '); $($summary.host.operatingSystem)")
[void]$md.AppendLine()
[void]$md.AppendLine('| Run | Houseguests | Verdict | Frames | Seconds | Avg fps | p50 ms | p95 ms | p99 ms | p99.9 ms | Max ms | >16.7 | >33.3 | >50 | Startup s | Bodies s | Startup max ms | Peak system MiB | Peak Unity alloc MiB | Status |')
[void]$md.AppendLine('|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|')
foreach ($r in $results) {
    $peak = $r.memoryPeak
    [void]$md.AppendLine("| $($r.name) | $(if ($r.houseSize) { $r.houseSize } else { $r.houseSizeRequested }) | $($r.verdict) | $($r.frameCount) | $(Format-Ms $r.measuredSeconds) | $(Format-Ms $r.averageFps) | $(Format-Ms $r.frameMedianMs) | $(Format-Ms $r.frameP95Ms) | $(Format-Ms $r.frameP99Ms) | $(Format-Ms $r.frameP999Ms) | $(Format-Ms $r.frameMaxMs) | $($r.slowFramesOver16_7Ms) | $($r.slowFramesOver33_3Ms) | $($r.slowFramesOver50Ms) | $(Format-Ms $r.startupSampleBeganSeconds) | $(Format-Ms $r.bodyAssemblySeconds) | $(Format-Ms $r.startupMaxMs) | $(if ($peak) { Format-MiB $peak.systemUsedBytes } else { '-' }) | $(if ($peak) { Format-MiB $peak.totalAllocatedBytes } else { '-' }) | $($r.status) |")
}
[void]$md.AppendLine()
foreach ($r in $results) {
    [void]$md.AppendLine("## $($r.name): $($r.purpose)")
    [void]$md.AppendLine()
    [void]$md.AppendLine("- Verdict: $($r.verdict)" + $(if ($r.performanceAcceptanceBasis) { " ($($r.performanceAcceptanceBasis))" } else { '' }))
    [void]$md.AppendLine("- Ended: $($r.ending), exit code $($r.exitCode); report ``$($r.report)``; raw frames ``$($r.rawFrameTimesFile)``")
    if ($r.reportError) { [void]$md.AppendLine("- **Report unreadable:** $($r.reportError)") }
    if ($r.houseSizeNote) { [void]$md.AppendLine("- House: $($r.houseSizeNote)") }
    if ($r.report) {
        [void]$md.AppendLine("- Season: $($r.seasonSource), seed $($r.seasonSeed), session $($r.sessionId)")
        [void]$md.AppendLine("- Window: sampled $($r.sampledResolution) on a $($r.displayResolution) display; mismatched frames: resolution $($r.sampledResolutionMismatchCount), window $($r.sampledDisplayModeMismatchCount), cap/VSync $($r.sampledFrameCapMismatchCount)")
        [void]$md.AppendLine("- Build $($r.buildVersion) ($($r.buildGuid)), development build $($r.developmentBuild); quality $($r.qualityLevelName); $($r.renderPipeline) render scale $($r.renderScale); $($r.gpu), $($r.graphicsDeviceVersion)")
        $stages = @($r.startupStages | ForEach-Object { "$($_.name) $($_.frames) frames, max $(Format-Ms $_.maxMs) ms, $($_.framesOver50Ms) over 50 ms" }) -join '; '
        [void]$md.AppendLine("- Startup (before the steady sample, $(Format-Ms $r.startupSampleBeganSeconds) s after launch): $stages; bodies built in $(Format-Ms $r.bodyAssemblySeconds) s (all built: $($r.bodiesAssembled))")
        $histogram = @(for ($i = 0; $i -lt @($r.frameHistogramCounts).Count; $i++) {
            $label = if ($i -lt @($r.frameHistogramUpperMs).Count) { '<=' + ([double]$r.frameHistogramUpperMs[$i]).ToString('0.#', $invariant) } else { '>' + ([double]$r.frameHistogramUpperMs[-1]).ToString('0', $invariant) }
            "$label ms: $($r.frameHistogramCounts[$i])" }) -join ', '
        [void]$md.AppendLine("- Frame-time histogram: $histogram")
        if ($r.memoryPeak) {
            [void]$md.AppendLine("- Memory (MiB) start/end/peak: system $(Format-MiB $r.memoryAtSampleStart.systemUsedBytes)/$(Format-MiB $r.memoryAtSampleEnd.systemUsedBytes)/$(Format-MiB $r.memoryPeak.systemUsedBytes); Unity allocated $(Format-MiB $r.memoryAtSampleStart.totalAllocatedBytes)/$(Format-MiB $r.memoryAtSampleEnd.totalAllocatedBytes)/$(Format-MiB $r.memoryPeak.totalAllocatedBytes); reserved peak $(Format-MiB $r.memoryPeak.totalReservedBytes); mono heap peak $(Format-MiB $r.memoryPeak.monoHeapBytes); gfx peak $(Format-MiB $r.memoryPeak.gfxUsedBytes)")
        }
        [void]$md.AppendLine("- Report status: $($r.status) with $($r.errors) errors")
    }
    if ($r.report -and -not $r.houseSizeMatches) { [void]$md.AppendLine("- **The house measured was not the house requested** ($($r.houseSize) for $($r.houseSizeRequested), stress $($r.reportStressRoster), season $($r.seasonSource)) - this run is not the evidence asked for.") }
    if ($r.report -and $r.status -ne 'Passed') { [void]$md.AppendLine("- **The report's own checks were $($r.status)** - this run is not valid evidence, whatever its numbers.") }
    if ($r.competingProcesses.Count -gt 0) { [void]$md.AppendLine("- **Competing processes during the run:** $($r.competingProcesses -join ', ') - this run is not clean evidence.") }
    [void]$md.AppendLine()
}
$mdPath = Join-Path $OutputRoot 'profile-summary.md'
[IO.File]::WriteAllText($mdPath, $md.ToString(), (New-Object Text.UTF8Encoding $false))
Write-Host "Summary: $jsonPath and $mdPath. Gameplay $($summary.gameplayVerdict); stress $($summary.stressVerdict); evidence complete $complete."
if ($complete) { $profileExit = 0 } else { $profileExit = 1 }
} catch {
    Write-ReviewNewJson ([ordered]@{schema=3;status='Failed';createdUtc=[DateTime]::UtcNow.ToString('o');
        failure=$_.Exception.Message;completedRuns=@($results);evidenceComplete=$false}) (Join-Path $OutputRoot 'profile-failure.json')
    Write-Error $_ -ErrorAction Continue
    $profileExit = 1
} finally {
    $cleanup = [ordered]@{executable=$Executable;pid=$null;startedUtc=$null;closed=$true;normalCloseRequested=$false;
        killed=$false;cleanupWaitSeconds=10;failure=$null;
        knownNameProcessObservations=@($script:profileKnownProcesses.Values | Sort-Object name,pid,startedUtc);
        ignoredUnityHubHelpers=@($script:profileKnownProcesses.Values | Where-Object classification -CEQ 'ExactUnityHubServeHelper' | Sort-Object pid,startedUtc)}
    try {
        if ($ownedProcess) {
            try {
                $cleanup.pid = $ownedProcess.Id
                $cleanup.startedUtc = $ownedProcess.StartTime.ToUniversalTime().ToString('o')
                $cleanup.closed = $ownedProcess.WaitForExit(0)
                if (-not $cleanup.closed) {
                    $cleanup.normalCloseRequested = $true
                    [void]$ownedProcess.CloseMainWindow()
                    $cleanup.closed = $ownedProcess.WaitForExit(5000)
                    if (-not $cleanup.closed) {
                        $cleanup.killed = $true
                        $ownedProcess.Kill()
                        $cleanup.closed = $ownedProcess.WaitForExit(5000)
                    }
                }
                if (-not $cleanup.closed) { throw 'The exact owned isolated profile process did not signal closure within bounded cleanup.' }
            } catch {
                $cleanup.failure = $_.Exception.Message
                $cleanup.closed = $false
                $profileExit = 1
                Write-Error $_ -ErrorAction Continue
            } finally { $ownedProcess.Dispose() }
        }
        Write-ReviewNewJson $cleanup (Join-Path $OutputRoot 'profile-cleanup.json')
    } finally {
        Exit-ReviewLease $lease
    }
}
exit $profileExit
