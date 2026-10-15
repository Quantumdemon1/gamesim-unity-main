$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'profile-evidence.ps1')
$checks = 0
function Check([bool]$Value, [string]$Why) { if (-not $Value) { throw $Why }; $script:checks++ }
function ValidRun {
    @{report='verification.json';houseSizeMatches=$true;competingProcesses=@();status='Passed';errors=0;exitCode=0;
        ending='exited';deadlineMet=$true;reportFresh=$true;rawFramesBound=$true}
}
Check (Test-ProfileRunValid (ValidRun)) 'A clean bound run should be valid.'
foreach ($field in @('deadlineMet','reportFresh','rawFramesBound','houseSizeMatches')) {
    $run = ValidRun; $run[$field] = $false
    Check (-not (Test-ProfileRunValid $run)) "$field is required."
}
foreach ($field in @('errors','exitCode')) {
    $run = ValidRun; $run[$field] = 1
    Check (-not (Test-ProfileRunValid $run)) "$field must be zero."
}
$run = ValidRun; $run.ending = 'stopped after the deadline'
Check (-not (Test-ProfileRunValid $run)) 'A killed run cannot authorize acceptance.'
$run = ValidRun; $run.competingProcesses = @('Unity.exe')
Check (-not (Test-ProfileRunValid $run)) 'A competing process invalidates the sample.'
$run = ValidRun; $run.status = 'Failed'
Check (-not (Test-ProfileRunValid $run)) 'A failed report cannot authorize acceptance.'
$run = ValidRun; $run.performanceAcceptance = 'Met'; $run.errors = 1
Check ((Get-ProfileRunVerdict $run).StartsWith('Not assessed:')) 'A passing report with errors cannot display a Met verdict.'
$run = ValidRun; $run.performanceAcceptance = 'Met'; $run.deadlineMet = $false
Check ((Get-ProfileRunVerdict $run).StartsWith('Not assessed:')) 'A late natural-zero exit cannot display a Met verdict.'
try { Assert-ProfileArtifactPath 'D:\profile\run-01' 'D:\profile\run-02\frames.csv'; throw 'An unrelated run was accepted.' }
catch { Check ($_.Exception.Message -eq 'A profile artifact must be inside this run fresh save root.') 'Reject sibling evidence.' }
try { Assert-ProfileArtifactPath 'D:\profile\run-01' 'D:\profile\run-01\..\old\frames.csv'; throw 'Traversal was accepted.' }
catch { Check ($_.Exception.Message -eq 'A profile artifact must be inside this run fresh save root.') 'Reject traversal evidence.' }
Check (-not (Test-ProfileReportFresh 'D:\missing-profile-evidence.json' @{finishedUtc='not-a-time'} ([DateTime]::UtcNow) ([DateTime]::UtcNow))) 'Invalid report clocks fail closed.'
$programFiles = 'C:\Program Files'
$hubExecutable = Join-Path $programFiles 'Unity Hub\resources\unity.exe'
function HubServeProcess {
    @{Name='Unity.exe';ProcessId=10440;CreationDate=[DateTime]::UtcNow;ExecutablePath=$hubExecutable;
        CommandLine=('"'+$hubExecutable+'" serve')}
}
Check (Test-ProfileUnityHubServeProcess (HubServeProcess) $programFiles) 'Only the complete exact installed Hub serve identity is ignored.'
foreach ($field in @('Name','ProcessId','CreationDate','ExecutablePath','CommandLine')) {
    $identity = HubServeProcess; $identity[$field] = $null
    Check (-not (Test-ProfileUnityHubServeProcess $identity $programFiles)) "Missing $field remains competing."
}
foreach ($pidValue in @(0,-1,'not-a-pid')) {
    $identity = HubServeProcess; $identity.ProcessId = $pidValue
    Check (-not (Test-ProfileUnityHubServeProcess $identity $programFiles)) 'Invalid process IDs remain competing.'
}
foreach ($created in @([DateTime]::MinValue,'unknown-start')) {
    $identity = HubServeProcess; $identity.CreationDate = $created
    Check (-not (Test-ProfileUnityHubServeProcess $identity $programFiles)) 'Unknown creation identity remains competing.'
}
foreach ($command in @(('"'+$hubExecutable+'"'),('"'+$hubExecutable+'" serve --other'),('"'+$hubExecutable+'" serve '),
    ('"'+$hubExecutable+'"  serve'),($hubExecutable+' serve'),('"'+$hubExecutable+'" SERVE'),('"'+$hubExecutable+'" -projectPath D:\game'))) {
    $identity = HubServeProcess; $identity.CommandLine = $command
    Check (-not (Test-ProfileUnityHubServeProcess $identity $programFiles)) 'Unexpected Hub arguments/quoting/spacing/case remain competing.'
}
foreach ($path in @('C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe','D:\unknown\Unity.exe',
    'C:\Program Files\Unity Hub\resources\..\resources\unity.exe')) {
    $identity = HubServeProcess; $identity.ExecutablePath = $path; $identity.CommandLine = '"'+$path+'" serve'
    Check (-not (Test-ProfileUnityHubServeProcess $identity $programFiles)) 'Editor/unknown/nonliteral paths remain competing.'
}
foreach ($processName in @('Gamesim.exe','UnityHub.exe','other.exe')) {
    $identity = HubServeProcess; $identity.Name = $processName
    Check (-not (Test-ProfileUnityHubServeProcess $identity $programFiles)) 'Other process names never receive this Unity exemption.'
}
Check (-not (Test-ProfileUnityHubServeProcess (HubServeProcess) '')) 'Missing installed ProgramFiles identity remains competing.'
Check (-not (Test-ProfileUnityHubServeProcess (HubServeProcess) 'D:\Program Files')) 'A different ProgramFiles root remains competing.'
$identity = HubServeProcess; $identity.ExecutablePath = $hubExecutable.ToUpperInvariant()
Check (-not (Test-ProfileUnityHubServeProcess $identity $programFiles)) 'The approved executable spelling is literal, not a broad path alias.'
Write-Output "Profile evidence fixture passed $checks checks; no player was launched."
