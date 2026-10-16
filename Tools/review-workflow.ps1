# Shared validation ownership and bounded evidence. Dot-source; never launches on import.
function Get-ReviewSuitePlan([bool]$WithoutUma,[string]$Specifications) {
    $suites=@(@{id='edit';platform='EditMode';assembly='Gamesim.EditModeTests';filter=''},
        @{id='play';platform='PlayMode';assembly='Gamesim.PlayModeTests';filter=''})
    if (-not $WithoutUma) { $suites+=@{id='uma';platform='PlayMode';assembly='Gamesim.Uma.PlayModeTests';filter=''} }
    $diagnostic=[bool]$Specifications
    if ($diagnostic) {
        $suites=@()
        foreach ($line in @($Specifications -split '\r?\n' | Where-Object {$_})) {
            $parts=@($line.Split(':'))
            if ($parts.Count -lt 3 -or $parts.Count -gt 4 -or $parts[0] -notmatch '\A[a-zA-Z0-9_-]+\z' -or
                $parts[1] -cnotin @('EditMode','PlayMode') -or $parts[2] -notmatch '\AGamesim\.[a-zA-Z0-9_.]+\z') { throw 'Invalid suite specification.' }
            if (@($suites | Where-Object id -eq $parts[0]).Count) { throw 'Duplicate suite artifact id.' }
            $filter=if ($parts.Count -eq 4) {$parts[3]} else {''}
            # Start-Process joins its ArgumentList on Windows. A single raw token avoids
            # quoted trailing-backslash rewriting; never admit token separators or quotes.
            if ($filter -match '[\s\p{C}"\x27]' -or $filter.StartsWith('-',[StringComparison]::Ordinal)) {
                throw 'Unsafe test filter: quotes, whitespace, control characters, and leading option hyphens are refused.'
            }
            $suites+=@{id=$parts[0];platform=$parts[1];assembly=$parts[2];filter=$filter}
        }
        if ($suites.Count -lt 1 -or $suites.Count -gt 16) { throw 'Request between 1 and 16 diagnostic suites.' }
    }
    return [pscustomobject]@{diagnostic=$diagnostic;suites=$suites}
}
function Assert-ReviewPlainPath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\','/')
    $cursor = $full; $depth = 0
    while ($cursor) {
        if (++$depth -gt 64) { throw 'Path ancestry exceeds the validation bound.' }
        try {
            $attributes = [IO.File]::GetAttributes($cursor)
            if ($attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Validation refuses a reparse path: $cursor" }
        } catch [IO.FileNotFoundException] {
            # A not-yet-created output leaf is allowed; still inspect every ancestor.
        } catch [IO.DirectoryNotFoundException] {
            # A not-yet-created parent is allowed; access/other failures still refuse.
        }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
    }
    return $full
}
function Get-ReviewDestination([string]$SourceRoot) {
    $root = Assert-ReviewPlainPath $(if ($env:GAMESIM_ACCEPTANCE) { $env:GAMESIM_ACCEPTANCE } else { 'D:\GamesimAcceptance' })
    $source = Assert-ReviewPlainPath $SourceRoot
    if ($root -eq $source -or $root.StartsWith($source+'\',[StringComparison]::OrdinalIgnoreCase) -or
        $source.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath (Join-Path $root 'ProjectSettings/ProjectVersion.txt') -PathType Leaf)) {
        throw 'The destination must be a separate existing Unity acceptance project.'
    }
    return $root
}
function Enter-ReviewLease([string]$Root) {
    $root = Assert-ReviewPlainPath $Root
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw 'Validation destination does not exist.' }
    $path = Join-Path $root '.gamesim-validation.lock'
    [void](Assert-ReviewPlainPath $path)
    try { $stream = [IO.File]::Open($path,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None) }
    catch { throw 'Another sync/test/build workflow holds the destination validation lease. Nothing was mirrored or launched.' }
    try {
        $token = [Guid]::NewGuid().ToString('N')
        $text = [Text.Encoding]::UTF8.GetBytes((@{pid=$PID;token=$token;startedUtc=[DateTime]::UtcNow.ToString('o');root=$root} | ConvertTo-Json -Compress))
        $stream.SetLength(0); $stream.Write($text,0,$text.Length); $stream.Flush($true)
        return [pscustomobject]@{root=$root;path=$path;stream=$stream;token=$token;pid=$PID}
    } catch { $stream.Dispose(); throw }
}
function Assert-ReviewLease($Lease,[string]$Root) {
    $root = Assert-ReviewPlainPath $Root
    if (-not $Lease -or $Lease.pid -ne $PID -or $Lease.root -ne $root -or
        $Lease.path -ne (Join-Path $root '.gamesim-validation.lock') -or
        $Lease.stream -isnot [IO.FileStream] -or -not $Lease.stream.CanWrite -or
        $Lease.stream.SafeFileHandle.IsClosed -or $Lease.stream.Name -ne $Lease.path) { throw 'A live held destination validation lease is required.' }
}
function Exit-ReviewLease($Lease) { if ($Lease -and $Lease.stream) { $Lease.stream.Dispose() } }
function Assert-ReviewEditorIdle([string]$Root) {
    $escaped = [regex]::Escape($Root)
    $forward = [regex]::Escape($Root.Replace('\','/'))
    $busy = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
        $_.CommandLine -match ($escaped+'(?=[\\/"\s]|$)') -or $_.CommandLine -match ($forward+'(?=[\\/"\s]|$)')
    })
    if ($busy.Count) { throw 'The acceptance editor is still running; nothing was copied or launched.' }
}
function Get-ReviewFiles([string]$Root) {
    $root = Assert-ReviewPlainPath $Root
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw "Missing input directory: $root" }
    $stack = [Collections.Generic.Stack[string]]::new(); $stack.Push($root)
    $files = [Collections.Generic.List[object]]::new(); $directories = 0
    while ($stack.Count) {
        if (++$directories -gt 250000) { throw 'Input directory inventory exceeds its bound.' }
        $directory = [IO.DirectoryInfo]::new($stack.Pop())
        foreach ($item in $directory.EnumerateFileSystemInfos()) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Validation refuses a reparse child: $($item.FullName)" }
            if ($item -is [IO.DirectoryInfo]) { $stack.Push($item.FullName) }
            else { $files.Add($item); if ($files.Count -gt 250000) { throw 'Input file inventory exceeds its bound.' } }
        }
    }
    return $files.ToArray()
}
function Get-ReviewFileRecord([string]$Path,[string]$Relative,[string]$CopyTo) {
    [void](Assert-ReviewPlainPath $Path)
    $inputStream = [IO.File]::Open($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    $outputStream = $null; $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        $length = $inputStream.Length
        if ($length -lt 0 -or $length -gt 536870912) { throw "Input file exceeds 512 MiB: $Relative" }
        if ($CopyTo) {
            [void](Assert-ReviewPlainPath $CopyTo)
            [void][IO.Directory]::CreateDirectory((Split-Path -Parent $CopyTo))
            $outputStream = [IO.File]::Open($CopyTo,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        }
        $buffer = [byte[]]::new(65536); [long]$readTotal = 0
        while ($readTotal -lt $length) {
            $count = $inputStream.Read($buffer,0,[int][Math]::Min($buffer.Length,$length-$readTotal))
            if ($count -le 0) { throw "Input truncated while hashing: $Relative" }
            [void]$algorithm.TransformBlock($buffer,0,$count,$null,0)
            if ($outputStream) { $outputStream.Write($buffer,0,$count) }
            $readTotal += $count
        }
        if ($inputStream.ReadByte() -ne -1 -or $inputStream.Length -ne $length) { throw "Input length changed while hashing: $Relative" }
        [void]$algorithm.TransformFinalBlock([byte[]]::new(0),0,0)
        $record=[pscustomobject]@{path=$Relative;bytes=$length;sha256=([BitConverter]::ToString($algorithm.Hash).Replace('-','').ToLowerInvariant())}
        if ($outputStream) {
            $outputStream.Flush($true); $outputStream.Dispose(); $outputStream=$null
            $copied=Get-ReviewFileRecord $CopyTo $Relative
            if ($copied.bytes -ne $record.bytes -or $copied.sha256 -cne $record.sha256) { throw "Preserved copy checksum mismatch: $Relative" }
        }
        return $record
    } finally { if ($outputStream) { $outputStream.Dispose() }; $inputStream.Dispose(); $algorithm.Dispose() }
}
function New-ReviewRawManifest([string]$Root,[string]$CopyRoot) {
    $root = Assert-ReviewPlainPath $Root
    $rows = [Collections.Generic.List[object]]::new(); [long]$total = 0
    foreach ($folder in @('Assets','ProjectSettings','Packages','ArtSource','Tools')) {
        $path = Join-Path $root $folder
        if ($CopyRoot) { [void][IO.Directory]::CreateDirectory((Join-Path $CopyRoot $folder)) }
        foreach ($file in @(Get-ReviewFiles $path)) {
            $relative = $file.FullName.Substring($root.Length+1).Replace('\','/')
            $copy = if ($CopyRoot) { Join-Path $CopyRoot $relative } else { $null }
            $record = Get-ReviewFileRecord $file.FullName $relative $copy
            $rows.Add($record); $total += $record.bytes
            if ($rows.Count -gt 250000 -or $total -gt 68719476736) { throw 'Source snapshot exceeds 250,000 files / 64 GiB.' }
        }
    }
    return [pscustomobject]@{schema=1;root=$root;files=@($rows | Sort-Object path);bytes=$total}
}
function Assert-ReviewManifestEqual($Before,$After) {
    $changes = @(Compare-ReviewInputs -Before $Before -After $After -RequireWorkflowStable -NoTolerances)
    if ($changes.Count) { throw "Immutable source snapshot changed ($($changes.Count) paths)." }
}
function Write-ReviewNewJson($Value,[string]$Path) {
    [void](Assert-ReviewPlainPath $Path)
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $Value -Depth 32))
    if ($bytes.Length -gt 67108864) { throw 'Evidence JSON exceeds 64 MiB.' }
    $stream = [IO.File]::Open($Path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $stream.Write($bytes,0,$bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
}
function New-ReviewRunRoot([string]$Destination,[string]$Name) {
    if ($Name -notmatch '^[a-zA-Z0-9_-]{1,100}$') { throw 'Use a simple artifact name (maximum 100 characters).' }
    $base = Join-Path (Assert-ReviewPlainPath $Destination) 'Logs/validation'
    [void][IO.Directory]::CreateDirectory($base)
    $path = Join-Path $base ($Name+'-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')+'-'+[Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $path -ErrorAction Stop)
    return $path
}
function Get-ReviewGitIdentity([string]$Root) {
    # PS5 turns native stderr into a terminating error under Stop, even with 2>$null.
    # An archive/isolated fixture has no root marker; do not probe an inherited repository.
    $marker=Join-Path $Root '.git'
    if (-not [IO.File]::Exists($marker) -and -not [IO.Directory]::Exists($marker)) {
        return [pscustomobject]@{head='not-a-git-worktree';status=@()}
    }
    [void](Assert-ReviewPlainPath $marker)
    # --no-optional-locks: a status probe must never refresh and rewrite the source's .git/index. That
    # rewrite is invisible to the Assets/ProjectSettings/Packages/ArtSource/Tools manifest, yet it moves
    # the index bytes any custody check of the source pins.
    $head=& git --no-optional-locks -C $Root rev-parse HEAD 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot capture source Git HEAD.' }
    $status=& git --no-optional-locks -C $Root status --porcelain=v1 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot capture source Git status.' }
    return [pscustomobject]@{head=($head -join '');status=@($status)}
}
function Assert-ReviewSourceIdentity($Snapshot) {
    $current=Get-ReviewGitIdentity $Snapshot.source
    if ($current.head -cne $Snapshot.identity.head -or (@($current.status) -join "`n") -cne (@($Snapshot.identity.status) -join "`n")) {
        throw 'Source HEAD or complete dirty status changed during validation.'
    }
}
function New-ReviewSnapshot([string]$SourceRoot,[string]$RunRoot) {
    $snapshot = Join-Path $RunRoot 'snapshot'
    [void](New-Item -ItemType Directory -Path $snapshot -ErrorAction Stop)
    $identity=Get-ReviewGitIdentity $SourceRoot
    $before = New-ReviewRawManifest $SourceRoot
    Write-ReviewNewJson $before (Join-Path $RunRoot 'source-before.json')
    $copy = New-ReviewRawManifest $SourceRoot $snapshot
    Write-ReviewNewJson $copy (Join-Path $RunRoot 'source-copy.json')
    $after = New-ReviewRawManifest $SourceRoot
    Write-ReviewNewJson $after (Join-Path $RunRoot 'source-after-copy.json')
    $actual = New-ReviewRawManifest $snapshot
    Write-ReviewNewJson $actual (Join-Path $RunRoot 'snapshot.json')
    Assert-ReviewManifestEqual $before $copy; Assert-ReviewManifestEqual $before $after; Assert-ReviewManifestEqual $before $actual
    $result=[pscustomobject]@{root=$snapshot;manifest=$actual;source=$SourceRoot;original=$before;identity=$identity}
    Assert-ReviewSourceIdentity $result
    Write-ReviewNewJson ([ordered]@{head=$identity.head;status=@($identity.status);source=$SourceRoot;snapshot=$snapshot;
        proof='Byte-identical bounded source/copy/after inventory; not an adversarial atomic filesystem snapshot.'}) (Join-Path $RunRoot 'provenance.json')
    return $result
}
function Assert-ReviewSnapshot($Snapshot) { Assert-ReviewManifestEqual $Snapshot.manifest (New-ReviewRawManifest $Snapshot.root) }
function Assert-ReviewRetainedVendorEqual($Before,$After) {
    $left=[pscustomobject]@{files=@($Before.files | Where-Object {$_.path -match '^Assets/UMA(/|\.meta$)'})}
    $right=[pscustomobject]@{files=@($After.files | Where-Object {$_.path -match '^Assets/UMA(/|\.meta$)'})}
    Assert-ReviewManifestEqual $left $right
}
function Assert-ReviewEvidenceFile([string]$Path,[string]$RunRoot,[string]$Hash) {
    $root=Assert-ReviewPlainPath $RunRoot; $path=Assert-ReviewPlainPath $Path
    if (-not $path.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $path -PathType Leaf) -or $Hash -notmatch '^[a-f0-9]{64}$' -or
        (Get-ReviewHash $path) -cne $Hash) { throw 'Missing, escaped, or changed retained evidence.' }
}
function Read-ReviewSuiteReport([string]$Path,[string]$Assembly,[int]$Minimum,[Nullable[int]]$ExitCode,[bool]$Filtered=$false) {
    if ($null -eq $ExitCode) { throw 'An actual owned process exit code is required.' }
    [void](Assert-ReviewPlainPath $Path)
    $info = Get-Item -LiteralPath $Path -ErrorAction Stop
    if ($info.Length -le 0 -or $info.Length -gt 134217728) { throw 'Test report is empty or exceeds 128 MiB.' }
    $settings = [Xml.XmlReaderSettings]::new(); $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit; $settings.XmlResolver = $null
    $settings.MaxCharactersInDocument = 134217728
    $reader = [Xml.XmlReader]::Create($Path,$settings)
    try { $xml = [Xml.XmlDocument]::new(); $xml.XmlResolver = $null; $xml.Load($reader) } finally { $reader.Dispose() }
    $run = $xml.DocumentElement
    if (-not $run -or $run.Name -ne 'test-run') { throw 'Expected a NUnit test-run report.' }
    $assemblies = @($xml.SelectNodes('//test-suite[@type="Assembly"]'))
    if ($assemblies.Count -ne 1 -or $assemblies[0].GetAttribute('name') -cne ($Assembly+'.dll')) { throw 'Wrong or ambiguous discovered test assembly.' }
    $cases = @($xml.SelectNodes('//test-case')); $passed=0; $failed=0; $skipped=0; $inconclusive=0
    if (@($assemblies[0].SelectNodes('.//test-case')).Count -ne $cases.Count) { throw 'Test cases exist outside the sole requested assembly.' }
    foreach ($case in $cases) {
        if ([string]::IsNullOrWhiteSpace($case.GetAttribute('fullname'))) { throw 'A discovered test case has no full name.' }
        switch -CaseSensitive ($case.GetAttribute('result')) {
            'Passed' { $passed++ }; 'Failed' { $failed++ }; 'Skipped' { $skipped++ }; 'Inconclusive' { $inconclusive++ }
            default { throw 'Unknown or unfinished test-case outcome.' }
        }
    }
    $counters = @{}
    foreach ($key in @('total','passed','failed','skipped','inconclusive')) {
        $number = 0
        if (-not [int]::TryParse($run.GetAttribute($key),[ref]$number) -or $number -lt 0) { throw "Missing or invalid NUnit counter: $key" }
        $counters[$key] = $number
    }
    if ($cases.Count -eq 0 -or $cases.Count -ne $counters.total -or $passed -ne $counters.passed -or
        $failed -ne $counters.failed -or $skipped -ne $counters.skipped -or $inconclusive -ne $counters.inconclusive -or
        $cases.Count -ne ($passed+$failed+$skipped+$inconclusive)) { throw 'Test discovery/leaves and report counters disagree.' }
    if (-not $Filtered) { Assert-ReviewSuiteMinimum $Assembly $cases.Count $Minimum }
    $success = $ExitCode -eq 0 -and $failed -eq 0 -and $skipped -eq 0 -and $inconclusive -eq 0 -and $passed -eq $cases.Count -and
        $run.GetAttribute('result') -ceq 'Passed' -and $assemblies[0].GetAttribute('result') -ceq 'Passed'
    return [pscustomobject]@{suite=$Assembly;minimumCount=$Minimum;total=$cases.Count;passed=$passed;failed=$failed;skipped=$skipped;
        inconclusive=$inconclusive;exit=$ExitCode;filtered=$Filtered;accepted=$success;fullSuite=($success -and -not $Filtered);
        report=$Path;reportSha256=(Get-ReviewHash $Path);testNames=@($cases | ForEach-Object {$_.GetAttribute('fullname')} | Sort-Object)}
}
# The longest deadline any owned process may declare: eight hours.
$script:ReviewDeadlineCapSeconds = 28800
# Per-suite deadlines, each at least twice the longest whole-process time measured for that suite
# (Unity start, import, compile, the tests and exit), so ordinary growth never reads as a late run:
#   Gamesim.EditModeTests      2,305 s for 7,846 tests (NoUMA, 2026-10-14)                -> 7,200 s (3.1x)
#   Gamesim.PlayModeTests      5,557 s for 1,197 tests (NoUMA, 2026-10-15; 5,401 s for 1,218
#                              earlier that day); 4,305 s for 1,081 under GAMESIM_UMA      -> 14,400 s (2.6x)
#   Gamesim.Uma.PlayModeTests    308 s for 89 tests (2026-10-15)                           -> 3,600 s (11.7x)
#   Gamesim.DiagnosticsPlayModeTests 261 s, Gamesim.Uma.DiagnosticsPlayModeTests 294 s
#                              (seven probes each, 2026-10-15)                             -> 3,600 s (12x)
# An assembly not listed takes its platform's suite deadline. Player builds keep their own 5,400 s
# (build-review-candidate.ps1; measured builds took 150-298 s).
$script:ReviewSuiteDeadlines = @{
    'Gamesim.EditModeTests'=7200; 'Gamesim.PlayModeTests'=14400; 'Gamesim.Uma.PlayModeTests'=3600
    'Gamesim.DiagnosticsPlayModeTests'=3600; 'Gamesim.Uma.DiagnosticsPlayModeTests'=3600
}
function Get-ReviewSuiteDeadline([string]$Assembly,[string]$Platform) {
    if ($script:ReviewSuiteDeadlines.ContainsKey($Assembly)) { return [int]$script:ReviewSuiteDeadlines[$Assembly] }
    if ($Platform -ceq 'EditMode') { return 7200 }
    if ($Platform -ceq 'PlayMode') { return 14400 }
    throw "No suite deadline for $Assembly on $Platform."
}
# Never kills what it started. Past its deadline a process is recorded late (timedOut) and failed,
# then left to exit on its own: a Unity run past its deadline is usually still writing its results or
# shutting down, and killing an exiting run leaves children holding the copy. If it has still not
# exited after a further grace period (the deadline again, by default) it is abandoned while running,
# and the failure names its pid for an operator; the sync and the suites refuse to start while any
# Unity process holds the copy.
function Invoke-ReviewOwnedProcess([string]$Executable,[string[]]$Arguments,[int]$Seconds,[string]$ReceiptPath,[int]$LateGraceSeconds=-1) {
    if ($Seconds -lt 1 -or $Seconds -gt $script:ReviewDeadlineCapSeconds) { throw "Owned process deadline must be between 1 and $($script:ReviewDeadlineCapSeconds) seconds." }
    if ($LateGraceSeconds -lt 0) { $LateGraceSeconds = $Seconds }
    if ($LateGraceSeconds -gt $script:ReviewDeadlineCapSeconds) { throw "Late grace must be at most $($script:ReviewDeadlineCapSeconds) seconds." }
    [void](Assert-ReviewPlainPath $Executable)
    $executableHash=Get-ReviewHash $Executable
    $receipt = [ordered]@{executable=$Executable;executableSha256=$executableHash;arguments=$Arguments;deadlineSeconds=$Seconds;lateGraceSeconds=$LateGraceSeconds;pid=$null;
        startedUtc=$null;exitedUtc=$null;exit=$null;kernelSignaled=$false;timedOut=$false;late=$false;abandoned=$false;stopped=$false;closed=$false;
        executableChanged=$false;failure=$null}
    $process = $null
    try {
        $process = Start-Process -FilePath $Executable -ArgumentList $Arguments -WindowStyle Hidden -PassThru -ErrorAction Stop
        [void]$process.Handle
        $receipt.pid = $process.Id; $receipt.startedUtc = $process.StartTime.ToUniversalTime().ToString('o')
        $clock = [Diagnostics.Stopwatch]::StartNew()
        while (-not $process.WaitForExit(0)) {
            if (-not $receipt.late -and $clock.Elapsed.TotalSeconds -ge $Seconds) {
                $receipt.timedOut=$true; $receipt.late=$true
                Write-Warning "The owned process $($process.Id) passed its $Seconds s deadline. It is recorded late and failed, and left to exit on its own; it is never killed."
            }
            if ($receipt.late -and $clock.Elapsed.TotalSeconds -ge $Seconds + $LateGraceSeconds) {
                $receipt.abandoned=$true
                throw "The owned process $($process.Id) is still running $($Seconds + $LateGraceSeconds) s after it started. It was left running, never killed; nothing may use the copy until it exits."
            }
            Start-Sleep -Milliseconds 200
        }
        $receipt.kernelSignaled=$true; $receipt.closed=$true; $receipt.exit=$process.ExitCode
        $receipt.exitedUtc=$process.ExitTime.ToUniversalTime().ToString('o')
        if (($process.ExitTime.ToUniversalTime()-$process.StartTime.ToUniversalTime()).TotalSeconds -gt $Seconds) { $receipt.timedOut=$true; $receipt.late=$true }
        if ($receipt.late) { throw "The owned process exited after its $Seconds s deadline; the run is recorded late and failed." }
    } catch { $receipt.failure=$_.Exception.Message }
    finally {
        # No Kill(), ever: stopped stays false. A process still running here was abandoned above.
        try {
            if ((Get-ReviewHash $Executable) -cne $executableHash) { $receipt.executableChanged=$true; $receipt.failure=($receipt.failure+'; executable bytes changed') }
        } catch { $receipt.executableChanged=$true; $receipt.failure=($receipt.failure+'; executable audit: '+$_.Exception.Message) }
        try { Write-ReviewNewJson $receipt $ReceiptPath } finally { if ($process) { $process.Dispose() } }
    }
    return [pscustomobject]$receipt
}
