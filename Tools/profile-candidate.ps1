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
# Refuses to start while Unity.exe or Gamesim.exe is running (close them yourself, saving any work),
# and records any that start during a run. Without -BuildEvidence the runs are still made, but the
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
$invariant = [Globalization.CultureInfo]::InvariantCulture

$acceptanceRoot = if ($env:GAMESIM_ACCEPTANCE) { $env:GAMESIM_ACCEPTANCE } else { 'D:\GamesimAcceptance' }
if (-not $Executable) { $Executable = Join-Path $acceptanceRoot 'Builds\Port-Windows-Review13\Gamesim.exe' }
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) { throw "No player executable at $Executable. Build one with build-review-candidate.ps1 first." }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
if ([IO.Path]::GetFileName($Executable) -ne 'Gamesim.exe') { throw "Profile the built Gamesim.exe, not $Executable." }

function Get-CompetingProcesses {
    @(Get-Process -Name 'Unity', 'Gamesim' -ErrorAction SilentlyContinue)
}
function Assert-NothingCompeting([string]$When) {
    $competing = Get-CompetingProcesses
    if ($competing.Count -gt 0) {
        $names = ($competing | ForEach-Object { "$($_.ProcessName).exe (pid $($_.Id))" }) -join ', '
        throw "Refusing to profile ${When}: $names running. A clean performance run has no editor, build or other game beside it; close them yourself (saving any work) and run again."
    }
}
Assert-NothingCompeting 'at all'

if (-not $OutputRoot) { $OutputRoot = Join-Path $acceptanceRoot ('Logs\profile-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ', $invariant)) }
if ((Test-Path -LiteralPath $OutputRoot) -and @(Get-ChildItem -LiteralPath $OutputRoot -Force).Count -ne 0) {
    throw "The output root already holds files: $OutputRoot. Every profile gets a fresh one."
}
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$OutputRoot = (Resolve-Path -LiteralPath $OutputRoot).Path
if ($Seconds -lt 300) { Write-Warning "A $Seconds s sample is shorter than the 300 s the verdict requires; every run will read 'Not assessed'." }

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
        powerScheme = ((powercfg /getactivescheme 2>$null) -join ' ').Trim() }
}

# A run's verdict counts only when the run is valid evidence: its report was read, it measured the
# house asked for in a season it started, nothing competed beside it, and its own checks Passed.
function Test-ProfileRunValid($Run) {
    [bool]($Run.report -and $Run.houseSizeMatches -and @($Run.competingProcesses).Count -eq 0 -and $Run.status -eq 'Passed')
}
function Get-ProfileRunVerdict($Run) {
    if (-not $Run.report) {
        return "Not assessed: no report ($($Run.ending), exit $($Run.exitCode))" + $(if ($Run.reportError) { '; ' + $Run.reportError } else { '' }) + '.'
    }
    $why = @()
    if (-not $Run.houseSizeMatches) { $why += "it measured $($Run.houseSize) houseguests (stress $($Run.reportStressRoster), season $($Run.seasonSource)) for the $($Run.houseSizeRequested) asked for" }
    if (@($Run.competingProcesses).Count -gt 0) { $why += "$(@($Run.competingProcesses) -join ', ') ran beside it" }
    if ($Run.status -ne 'Passed') { $why += "its own checks were $($Run.status) with $($Run.errors) errors" }
    $said = if ($Run.performanceAcceptance) { [string]$Run.performanceAcceptance } else { 'nothing' }
    if ($why.Count -gt 0) { return "Not assessed: $($why -join '; ') (the player said: $said)" }
    if (-not $Run.performanceAcceptance) { return 'Not assessed: the report carries no verdict.' }
    return [string]$Run.performanceAcceptance
}

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
    $null = $process.Handle   # Cached now, or Windows PowerShell reports no exit code once the player has gone.
    $deadline = $started.AddSeconds($Seconds + $StartupAllowanceSeconds)
    $competingSeen = @{}
    while (-not $process.HasExited -and [DateTime]::UtcNow -lt $deadline) {
        foreach ($other in Get-CompetingProcesses) {
            if ($other.Id -ne $process.Id) { $competingSeen["$($other.ProcessName).exe (pid $($other.Id))"] = $true }
        }
        Start-Sleep -Seconds 5
    }
    $ending = 'exited'
    if (-not $process.HasExited) {
        # Past the steady sample and the whole startup allowance: ask the window to close, and only
        # then end the one process this script started.
        $null = $process.CloseMainWindow()
        if ($process.WaitForExit(60000)) { $ending = 'closed after the deadline' }
        else { Stop-Process -Id $process.Id -Force; $process.WaitForExit(); $ending = 'stopped after the deadline' }
    }
    $process.WaitForExit()
    $finished = [DateTime]::UtcNow

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
        report = if ($report) { $reportPath } else { $null }; reportSha256 = if (Test-Path -LiteralPath $reportPath) { Get-ReviewHash $reportPath } else { $null }
        reportError = $reportError
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
        $result.rawFrameTimesSha256 = if ($report.rawFrameTimesFile -and (Test-Path -LiteralPath $report.rawFrameTimesFile)) { Get-ReviewHash $report.rawFrameTimesFile } else { $null }
    }
    $result.valid = Test-ProfileRunValid $result
    $result.verdict = Get-ProfileRunVerdict $result
    $results += $result
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
$complete = (@($results | Where-Object { -not $_.valid }).Count -eq 0) -and [bool]$build.evidenceMatches -and $oneBuild
$gameplay = @($results | Where-Object { -not $_.stressRoster } | ForEach-Object { $_.verdict })
$stress = @($results | Where-Object { $_.stressRoster } | ForEach-Object { $_.verdict })
$summary = [ordered]@{
    schema = 2; createdUtc = [DateTime]::UtcNow.ToString('o', $invariant); outputRoot = $OutputRoot
    target = [ordered]@{ resolution = '1920x1080'; window = 'Windowed'; uncapped = $true; p95LimitMs = 16.7; p99LimitMs = 33.3; minimumSeconds = 300; requestedSeconds = $Seconds }
    evidenceComplete = $complete
    buildEvidenceMatches = [bool]$build.evidenceMatches
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
if ($complete) { exit 0 } else { exit 1 }
