# Isolated BCL/PowerShell fixtures. Never starts Unity, builds a player, or uses real acceptance.
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'review-evidence.ps1')
. (Join-Path $PSScriptRoot 'review-workflow.ps1')
$fixtureBase=Assert-ReviewPlainPath (Join-Path ([IO.Path]::GetTempPath()) 'GamesimWorkflowFixtures')
$fixture=Join-Path $fixtureBase ([Guid]::NewGuid().ToString('N'))
$source=Join-Path $fixture 'source'; $destination=Join-Path $fixture 'destination'; $lease=$null
$checks=[Collections.Generic.List[string]]::new()
$previousAcceptance=$env:GAMESIM_ACCEPTANCE; $previousUnity=$env:GAMESIM_UNITY; $fixtureSucceeded=$false
function Put([string]$Root,[string]$Relative,[string]$Text) {
    $path=Join-Path $Root $Relative; [void][IO.Directory]::CreateDirectory((Split-Path -Parent $path))
    [IO.File]::WriteAllText($path,$Text,[Text.UTF8Encoding]::new($false))
}
function Check([string]$Name,[bool]$Value) { if (-not $Value) {throw "Fixture failed: $Name"}; $checks.Add($Name) }
function Reject([string]$Name,[scriptblock]$Action,[string]$Message) {
    $refused=$false
    try { & $Action | Out-Null } catch { if ($Message -and $_.Exception.Message -notlike ('*'+$Message+'*')) {throw}; $refused=$true }
    Check $Name $refused
}
function Report([string]$Results,[string]$Assembly='Gamesim.EditModeTests',[string]$OverrideCounters) {
    $passed=0; $failed=0; $skipped=0; $inconclusive=0; $cases=''; $i=0
    foreach ($outcome in $Results.Split(',')) {
        switch ($outcome) {'Passed' {$passed++} 'Failed' {$failed++} 'Skipped' {$skipped++} 'Inconclusive' {$inconclusive++}}
        $cases+='<test-case id="'+(++$i)+'" fullname="fixture.'+$i+'" result="'+$outcome+'" />'
    }
    $counters=if ($OverrideCounters) {$OverrideCounters} else {'total="'+$i+'" passed="'+$passed+'" failed="'+$failed+'" skipped="'+$skipped+'" inconclusive="'+$inconclusive+'"'}
    $status=if ($passed -eq $i) {'Passed'} else {'Failed'}
    '<test-run result="'+$status+'" '+$counters+'><test-suite type="Assembly" name="'+$Assembly+'.dll" result="'+$status+'">'+$cases+'</test-suite></test-run>'
}
try {
    # Missing future output paths are allowed without creating them; real input IO still refuses.
    $missingPath=Join-Path $fixture 'not-created/inner/file.bin'
    Check 'Missing descendants preserve the canonical output spelling' ((Assert-ReviewPlainPath $missingPath) -ceq [IO.Path]::GetFullPath($missingPath))
    Check 'Plain-path inspection never creates missing ancestors' (-not (Test-Path -LiteralPath (Join-Path $fixture 'not-created')))
    Reject 'Missing file is not a readable hashing input' {Get-ReviewFileRecord $missingPath 'missing.bin'} ''
    Reject 'Malformed paths cannot masquerade as missing files' {Assert-ReviewPlainPath ("invalid"+[char]0+"path")} ''
    # Deterministic, fixture-owned bytes only: independent expected SHA and exact copy readback.
    $binaryRoot=Join-Path $fixture 'binary-boundaries'
    [void][IO.Directory]::CreateDirectory($binaryRoot)
    foreach($length in [int[]]@(0,1,65535,65536,65537,131071,131072,131073,1048575,1048576,1048577)) {
        $payload=[byte[]]::new($length); [Random]::new(1701+$length).NextBytes($payload)
        $inputPath=Join-Path $binaryRoot ("input-"+$length+".bin")
        $copyPath=Join-Path $binaryRoot ("copies/output-"+$length+".bin")
        [IO.File]::WriteAllBytes($inputPath,$payload)
        $expectedAlgorithm=[Security.Cryptography.SHA256]::Create()
        try {$expectedHash=[BitConverter]::ToString($expectedAlgorithm.ComputeHash($payload)).Replace('-','').ToLowerInvariant()}
        finally {$expectedAlgorithm.Dispose()}
        $direct=Get-ReviewFileRecord $inputPath ("boundary-"+$length)
        Check ("Boundary "+$length+" hashes every byte with the actual length") ($direct.bytes -eq $length -and $direct.sha256 -ceq $expectedHash)
        $copyRecord=Get-ReviewFileRecord $inputPath ("boundary-"+$length) $copyPath
        Check ("Boundary "+$length+" preserved copy returns the same digest") ($copyRecord.bytes -eq $length -and $copyRecord.sha256 -ceq $expectedHash)
        Check ("Boundary "+$length+" preserved bytes are exact") ([Linq.Enumerable]::SequenceEqual([byte[]][IO.File]::ReadAllBytes($copyPath),$payload))
        Reject ("Boundary "+$length+" copy refuses overwrite") {Get-ReviewFileRecord $inputPath ("boundary-"+$length) $copyPath} ''
        $stillSource=Get-ReviewFileRecord $inputPath ("boundary-"+$length)
        $stillCopy=Get-ReviewFileRecord $copyPath ("boundary-"+$length)
        Check ("Boundary "+$length+" refusal preserves source and copy") ($stillSource.bytes -eq $length -and $stillCopy.bytes -eq $length -and
            $stillSource.sha256 -ceq $expectedHash -and $stillCopy.sha256 -ceq $expectedHash)
    }
    $hiddenPath=Join-Path $binaryRoot 'hidden-system.bin'
    [IO.File]::WriteAllBytes($hiddenPath,[byte[]]@(17,31,255))
    [IO.File]::SetAttributes($hiddenPath,[IO.FileAttributes]::Hidden -bor [IO.FileAttributes]::System)
    $listed=@(Get-ReviewFiles $binaryRoot)
    Check 'Lazy enumeration retains the exact nested binary fixture inventory' ($listed.Count -eq 23)
    Check 'Lazy enumeration returns actual FileInfo entries' (@($listed | Where-Object {$_ -isnot [IO.FileInfo]}).Count -eq 0)
    $hiddenEntries=@($listed | Where-Object {$_.FullName -ceq $hiddenPath})
    Check 'Lazy enumeration includes hidden and system files without changing attributes' ($hiddenEntries.Count -eq 1 -and
        ($hiddenEntries[0].Attributes -band [IO.FileAttributes]::Hidden) -and ($hiddenEntries[0].Attributes -band [IO.FileAttributes]::System))
    foreach ($root in @($source,$destination)) {
        Put $root 'ProjectSettings/ProjectVersion.txt' 'm_EditorVersion: fixture'
        Put $root 'ProjectSettings/ProjectSettings.asset' "  scriptingDefineSymbols:`n    Standalone: KEEP;GAMESIM_UMA`n"
        Put $root 'Assets/Settings/PC_RPAsset.asset' "  m_GPUResidentDrawerMode: 1`n"
        Put $root 'Packages/manifest.json' '{}'; Put $root 'ArtSource/source.txt' 'authored'; Put $root 'Tools/baseline.txt' 'Gamesim.EditModeTests 2'
    }
    $lease=Enter-ReviewLease $destination
    Assert-ReviewLease $lease $destination
    Reject 'Canonical destination spelling contends on the same held file' {Enter-ReviewLease ($destination.ToUpperInvariant()+'\.') } 'holds the destination'
    Reject 'Wrong-root nested lease refuses' {Assert-ReviewLease $lease $source} 'live held'
    Exit-ReviewLease $lease
    $lease=Enter-ReviewLease $destination
    Check 'Released lease can be reacquired' $lease.stream.CanWrite
    $run=New-ReviewRunRoot $destination 'fixture'
    $snapshot=New-ReviewSnapshot $source $run
    Assert-ReviewSnapshot $snapshot
    $sourceSettings="  scriptingDefineSymbols:`r`n    Standalone: SENTIS_ANALYTICS_ENABLED;KEEP`r`n    Android: KEEP`r`n"
    $activated="  scriptingDefineSymbols:`r`n    Standalone: KEEP;GAMESIM_UMA`r`n    Android: KEEP`r`n"
    Check 'Retained UMA activation is exact Standalone-only addition' ((ConvertTo-ReviewAcceptanceSettings $sourceSettings $false $true) -ceq $activated)
    Check 'Vendor presence does not implicitly activate standard source mode' ((ConvertTo-ReviewAcceptanceSettings $sourceSettings $false) -ceq $sourceSettings.Replace('SENTIS_ANALYTICS_ENABLED;',''))
    Check 'Repeated retained activation is idempotent' ((ConvertTo-ReviewAcceptanceSettings $activated $false $true) -ceq $activated)
    Check 'NoUMA removes explicitly local UMA token again' ((ConvertTo-ReviewAcceptanceSettings $activated $true) -ceq $sourceSettings.Replace('SENTIS_ANALYTICS_ENABLED;',''))
    Reject 'Conflicting UMA/NoUMA projection refuses' {ConvertTo-ReviewAcceptanceSettings $sourceSettings $true $true} 'cannot be used for NoUMA'
    Check 'Before/copy/after snapshot proof retained' ((Test-Path -LiteralPath (Join-Path $run 'source-before.json')) -and (Test-Path -LiteralPath (Join-Path $run 'source-after-copy.json')))
    Put $source 'ArtSource/source.txt' 'changed live authoring'
    Check 'Snapshot is detached from later live writes' ([IO.File]::ReadAllText((Join-Path $snapshot.root 'ArtSource/source.txt')) -ceq 'authored')
    Reject 'Live source drift is strict' {Assert-ReviewManifestEqual $snapshot.original (New-ReviewRawManifest $source)} 'changed'
    Put $snapshot.root 'Tools/baseline.txt' 'tampered snapshot'
    Reject 'Tools inside snapshot cannot silently refresh' {Assert-ReviewSnapshot $snapshot} 'changed'
    $before=[pscustomobject]@{files=@([pscustomobject]@{path='Tools/x';bytes=1;sha256='a'})}
    $after=[pscustomobject]@{files=@([pscustomobject]@{path='Tools/x';bytes=1;sha256='b'})}
    Check 'Strict workflow drift rejects changed Tools' (-not @(Compare-ReviewInputs $before $after -RequireWorkflowStable)[0].allowed)
    $duplicate=[pscustomobject]@{files=@($before.files[0],$before.files[0])}
    Reject 'Duplicate manifest path refuses rather than overwriting' {Compare-ReviewInputs $duplicate $after} 'Duplicate'
    $fontBefore=[pscustomobject]@{files=@([pscustomobject]@{path='Assets/Gamesim/Resources/Fonts/Inter-Regular SDF.asset';bytes=1;sha256='a'})}
    $fontAfter=[pscustomobject]@{files=@([pscustomobject]@{path='Assets/Gamesim/Resources/Fonts/Inter-Regular SDF.asset';bytes=1;sha256='b'})}
    Reject 'Raw snapshot equality never uses dynamic-font tolerance' {Assert-ReviewManifestEqual $fontBefore $fontAfter} 'changed'
    $receipt=Join-Path $run 'failure.json'
    Write-ReviewNewJson @{accepted=$false;failure='fixture'} $receipt
    $pin=Get-ReviewHash $receipt
    Reject 'Failure receipt is CreateNew and not replaceable' {Write-ReviewNewJson @{accepted=$true} $receipt} ''
    Check 'Original failure bytes retained' ((Get-ReviewHash $receipt) -ceq $pin)
    $another=New-ReviewRunRoot $destination 'fixture'
    Check 'Repeated artifact label yields distinct retained roots' ($run -cne $another)
    $xml=Join-Path $run 'fixture.xml'
    Put $run 'fixture.xml' (Report 'Passed,Passed')
    $good=Read-ReviewSuiteReport $xml 'Gamesim.EditModeTests' 2 0
    Check 'Complete XML exact assembly/counters/leaves meets floor' ($good.fullSuite -and $good.total -eq 2)
    Check 'Nonzero native exit remains red despite all-pass XML' (-not (Read-ReviewSuiteReport $xml 'Gamesim.EditModeTests' 2 1).accepted)
    Reject 'Missing actual exit cannot coerce to success zero' {Read-ReviewSuiteReport $xml 'Gamesim.EditModeTests' 2 $null} 'actual owned'
    Check 'Filtered diagnostic cannot become full-suite evidence' (-not (Read-ReviewSuiteReport $xml 'Gamesim.EditModeTests' 999 0 $true).fullSuite)
    Reject 'Full suite shortfall refuses' {Read-ReviewSuiteReport $xml 'Gamesim.EditModeTests' 3 0} 'SHORTFALL'
    Reject 'Wrong assembly refuses' {Read-ReviewSuiteReport $xml 'Gamesim.PlayModeTests' 1 0} 'assembly'
    foreach ($outcome in @('Failed','Skipped','Inconclusive')) {
        Put $run 'fixture.xml' (Report ('Passed,'+$outcome))
        Check ("Actual $outcome case remains red") (-not (Read-ReviewSuiteReport $xml 'Gamesim.EditModeTests' 2 0).accepted)
    }
    Put $run 'fixture.xml' (Report 'Passed,Passed' 'Gamesim.EditModeTests' 'total="2" passed="1" failed="1" skipped="0" inconclusive="0"')
    Reject 'Counter/leaf disagreement refuses' {Read-ReviewSuiteReport $xml 'Gamesim.EditModeTests' 2 0} 'disagree'
    Put $run 'fixture.xml' '<test-run />'
    Reject 'Empty/wrong discovered assembly refuses' {Read-ReviewSuiteReport $xml 'Gamesim.EditModeTests' 1 0} 'assembly'
    Put $run 'fixture.xml' '<test-run'
    Reject 'Malformed XML refuses' {Read-ReviewSuiteReport $xml 'Gamesim.EditModeTests' 1 0} ''
    Put $run 'fixture.xml' '<!DOCTYPE test-run [<!ENTITY x SYSTEM "file:///not-allowed">]><test-run>&x;</test-run>'
    Reject 'External XML entities refuse' {Read-ReviewSuiteReport $xml 'Gamesim.EditModeTests' 1 0} ''
    Reject 'Unknown/missing baseline floor refuses' {Get-ReviewSuiteFloor (Join-Path $source 'Tools/baseline.txt') 'Unknown.Tests'} 'No suite floor'
    Put $source 'Tools/baseline.txt' "Gamesim.EditModeTests 2`nGamesim.EditModeTests 3"
    Reject 'Duplicate floor refuses' {Get-ReviewSuiteFloor (Join-Path $source 'Tools/baseline.txt') 'Gamesim.EditModeTests'} 'duplicate'
    # A real filesystem junction only in this owned fixture; no traversal/recursive junction removal.
    $junction=Join-Path $source 'Assets/link'
    [void](New-Item -ItemType Junction -Path $junction -Target (Join-Path $destination 'Assets'))
    try {
        Reject 'Plain-path guard refuses a real reparse leaf' {Assert-ReviewPlainPath $junction} 'reparse path'
        Reject 'Plain-path guard refuses a reparse ancestor of a missing descendant' {Assert-ReviewPlainPath (Join-Path $junction 'missing/child.bin')} 'reparse path'
        Reject 'File hashing refuses a reparse ancestor of an existing file' {Get-ReviewFileRecord (Join-Path $junction 'Settings/PC_RPAsset.asset') 'linked.asset'} 'reparse path'
        Reject 'Walker refuses a reparse root before enumeration' {Get-ReviewFiles $junction} 'reparse path'
        Reject 'Source walker refuses nested reparse before descent' {Get-ReviewFiles (Join-Path $source 'Assets')} 'reparse child'
    }
    finally { [IO.Directory]::Delete($junction) }
    Check 'Lease still held after fixture refusals' $lease.stream.CanWrite
    # Exercise real entrypoint failure paths against this fixture destination only.
    foreach ($scriptName in @('review-evidence.ps1','review-workflow.ps1','sync-acceptance.ps1','run-review-suites.ps1','verify-review-candidate.ps1','build-review-candidate.ps1')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $scriptName) -Destination (Join-Path $source ('Tools/'+$scriptName))
    }
    Put $source 'Tools/baseline.txt' "Gamesim.EditModeTests 2`nGamesim.PlayModeTests 2`nGamesim.Uma.PlayModeTests 2"
    $env:GAMESIM_ACCEPTANCE=$destination; $env:GAMESIM_UNITY=Join-Path $fixture 'never-installed-Unity.exe'
    Reject 'Controller lease contention retains a failed summary without sync' {& (Join-Path $source 'Tools/verify-review-candidate.ps1') -Name 'held-failure' -WithoutUma} 'gate failed'
    $held=Get-Content -LiteralPath (Join-Path $destination 'Logs/held-failure-summary.json') -Raw | ConvertFrom-Json
    Check 'Lease-contention receipt explicitly refuses acceptance' ($held.status -ceq 'Failed' -and -not $held.fullSuiteAcceptance -and -not $held.snapshot)
    Exit-ReviewLease $lease; $lease=$null
    Reject 'Missing Unity entrypoint retains snapshot and final failure audits' {& (Join-Path $source 'Tools/verify-review-candidate.ps1') -Name 'missing-editor' -WithoutUma} 'gate failed'
    $missing=Get-Content -LiteralPath (Join-Path $destination 'Logs/missing-editor-summary.json') -Raw | ConvertFrom-Json
    Check 'No process/test acceptance invented by missing executable' ($missing.status -ceq 'Failed' -and @($missing.suites).Count -eq 0 -and
        (Test-Path -LiteralPath $missing.snapshotProof) -and (Test-Path -LiteralPath $missing.finalSourceManifest) -and
        @((Get-Content -LiteralPath $missing.driftReport -Raw | ConvertFrom-Json).sourceChanges).Count -eq 0)
    $lease=Enter-ReviewLease $destination
    Check 'Controller error releases destination lease' $lease.stream.CanWrite
    Exit-ReviewLease $lease; $lease=$null
    Put $destination 'Logs/stale-summary.json' '{"schema":3,"status":"Passed"}'
    Reject 'Build refuses old unbound schema3 pass without launching' {& (Join-Path $source 'Tools/build-review-candidate.ps1') -TestName 'stale' -WithoutUma} 'gate failed'
    $buildFailures=@(Get-ChildItem -LiteralPath (Join-Path $destination 'Logs/validation') -Directory | Where-Object {$_.Name -like 'build-stale-*'})
    Check 'Rejected build retains explicit no-player failure receipt' ($buildFailures.Count -eq 1 -and
        (Get-Content -LiteralPath (Join-Path $buildFailures[0].FullName 'evidence.json') -Raw | ConvertFrom-Json).status -ceq 'Failed' -and
        -not (Test-Path -LiteralPath (Join-Path $buildFailures[0].FullName 'build-process.json')))
    $lease=Enter-ReviewLease $destination
    Check 'Rejected build releases destination lease' $lease.stream.CanWrite
    $fixtureSucceeded=$true
    Write-Output ("{0} workflow checks passed; temporary fixtures only, no Unity/build/player." -f $checks.Count)
    $checks | ForEach-Object {Write-Output ('Passed: '+$_)}
} finally {
    Exit-ReviewLease $lease
    $env:GAMESIM_ACCEPTANCE=$previousAcceptance; $env:GAMESIM_UNITY=$previousUnity
    $resolved=Assert-ReviewPlainPath $fixture
    if (-not $resolved.StartsWith($fixtureBase+'\',[StringComparison]::OrdinalIgnoreCase)) {throw 'Unsafe fixture cleanup boundary.'}
    if ($fixtureSucceeded -and (Test-Path -LiteralPath $resolved)) {Remove-Item -LiteralPath $resolved -Recurse -Force}
    elseif (-not $fixtureSucceeded) {Write-Output "Failed isolated workflow fixture retained: $resolved"}
}
