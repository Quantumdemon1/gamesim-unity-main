# Read-only profile acceptance predicates. No player launch or project copy on import.
function Test-ProfileUnityHubServeProcess($Process, [string]$ProgramFilesRoot) {
    # This is one installed Hub helper, not a general Unity exemption. Missing CIM identity fails closed.
    if ($null -eq $Process -or [string]::IsNullOrEmpty($ProgramFilesRoot) -or
        [string]$Process.Name -ine 'Unity.exe' -or [string]::IsNullOrEmpty([string]$Process.ExecutablePath) -or
        [string]::IsNullOrEmpty([string]$Process.CommandLine) -or $Process.CreationDate -isnot [DateTime] -or
        $Process.CreationDate.Ticks -le 0) { return $false }
    [uint32]$processId = 0
    if (-not [uint32]::TryParse([string]$Process.ProcessId, [ref]$processId) -or $processId -eq 0) { return $false }
    $expected = Join-Path $ProgramFilesRoot 'Unity Hub\resources\unity.exe'
    [bool]([string]::Equals([string]$Process.ExecutablePath, $expected, [StringComparison]::Ordinal) -and
        [string]::Equals([string]$Process.CommandLine, ('"'+$expected+'" serve'), [StringComparison]::Ordinal))
}
function Test-ProfileRunValid($Run) {
    [bool]($Run.report -and $Run.houseSizeMatches -and @($Run.competingProcesses).Count -eq 0 -and
        $Run.status -eq 'Passed' -and $Run.errors -eq 0 -and $Run.exitCode -eq 0 -and
        $Run.ending -eq 'exited' -and $Run.deadlineMet -and $Run.reportFresh -and $Run.rawFramesBound)
}
function Get-ProfileRunVerdict($Run) {
    if (-not $Run.report) {
        return "Not assessed: no report ($($Run.ending), exit $($Run.exitCode))" + $(if ($Run.reportError) { '; ' + $Run.reportError } else { '' }) + '.'
    }
    $why = @()
    if (-not $Run.houseSizeMatches) { $why += "it measured $($Run.houseSize) houseguests (stress $($Run.reportStressRoster), season $($Run.seasonSource)) for the $($Run.houseSizeRequested) asked for" }
    if (@($Run.competingProcesses).Count -gt 0) { $why += "$(@($Run.competingProcesses) -join ', ') ran beside it" }
    if ($Run.status -ne 'Passed' -or $Run.errors -ne 0) { $why += "its own checks were $($Run.status) with $($Run.errors) errors" }
    if ($Run.exitCode -ne 0 -or $Run.ending -ne 'exited') { $why += "the player did not exit successfully ($($Run.ending), exit $($Run.exitCode))" }
    if (-not $Run.deadlineMet) { $why += 'the actual process exit exceeded the original deadline' }
    if (-not $Run.reportFresh -or -not $Run.rawFramesBound) { $why += 'the report/raw sample is not fresh evidence bound to this run' }
    $said = if ($Run.performanceAcceptance) { [string]$Run.performanceAcceptance } else { 'nothing' }
    if ($why.Count -gt 0) { return "Not assessed: $($why -join '; ') (the player said: $said)" }
    if (-not $Run.performanceAcceptance) { return 'Not assessed: the report carries no verdict.' }
    return [string]$Run.performanceAcceptance
}
function Assert-ProfileArtifactPath([string]$Root, [string]$Path) {
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd('\','/')
    $artifact = [IO.Path]::GetFullPath($Path)
    if (-not $artifact.StartsWith($rootPath+'\',[StringComparison]::OrdinalIgnoreCase)) {
        throw 'A profile artifact must be inside this run fresh save root.'
    }
    $cursor = $artifact
    while ($cursor -and $cursor.Length -ge $rootPath.Length) {
        $item = Get-Item -LiteralPath $cursor -Force -ErrorAction SilentlyContinue
        if ($item -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Profile artifacts cannot use reparse paths.' }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
    }
    if (-not (Test-Path -LiteralPath $artifact -PathType Leaf)) { throw 'Missing profile artifact.' }
    return $artifact
}
function Test-ProfileReportFresh([string]$Path, $Report, [DateTime]$Started, [DateTime]$Finished) {
    $reported = [DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParse([string]$Report.finishedUtc, [ref]$reported)) { return $false }
    $item = Get-Item -LiteralPath $Path -ErrorAction SilentlyContinue
    [bool]($item -and $item.LastWriteTimeUtc -ge $Started -and $item.LastWriteTimeUtc -le $Finished.AddSeconds(2) -and
        $reported.UtcDateTime -ge $Started -and $reported.UtcDateTime -le $Finished.AddSeconds(2))
}
