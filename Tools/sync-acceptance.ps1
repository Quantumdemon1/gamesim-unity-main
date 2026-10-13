# Mirrors only named project folders into the pre-existing, idle acceptance copy.
param([switch]$DisableGpuResidentDrawer, [switch]$WithoutUma, [switch]$RetainInstalledUma,
    [object]$ValidationLease, [object]$Snapshot)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'review-evidence.ps1')
. (Join-Path $PSScriptRoot 'review-workflow.ps1')
$sourceRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\','/')
$originalSource = $sourceRoot
$destinationRoot = Get-ReviewDestination $sourceRoot
$ownLease = -not $ValidationLease; $syncEvidence = $null; $syncFailure = $null; $synced = $false
try {
if ($ownLease) {
    $syncEvidence = New-ReviewRunRoot $destinationRoot 'sync'
    $ValidationLease = Enter-ReviewLease $destinationRoot
    Assert-ReviewEditorIdle $destinationRoot
    if ($Snapshot) { throw 'Direct sync may not substitute an externally supplied snapshot.' }
    $Snapshot = New-ReviewSnapshot $sourceRoot $syncEvidence
}
Assert-ReviewLease $ValidationLease $destinationRoot
if (-not $Snapshot) { throw 'Sync requires a verified immutable source snapshot.' }
Assert-ReviewSnapshot $Snapshot
$sourceRoot = Assert-ReviewPlainPath $Snapshot.root
foreach ($folder in @('Assets','ProjectSettings','Packages','ArtSource')) {
    $target = Join-Path $destinationRoot $folder
    if (Test-Path -LiteralPath $target) { [void](Get-ReviewFiles $target) }
}
if ($destinationRoot -eq $originalSource -or $destinationRoot.StartsWith($originalSource + '\', [StringComparison]::OrdinalIgnoreCase) -or
    $originalSource.StartsWith($destinationRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath (Join-Path $destinationRoot 'ProjectSettings\ProjectVersion.txt'))) {
    throw 'The destination must be a separate existing Unity acceptance project.'
}
$busy = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -match ([regex]::Escape($destinationRoot) + '(?=[\\/"\s]|$)') -or
        $_.CommandLine -match ([regex]::Escape($destinationRoot.Replace('\','/')) + '(?=[\\/"\s]|$)')
}
if ($busy) { throw 'The acceptance editor is still running; nothing was copied.' }
if ($WithoutUma -and (Test-Path -LiteralPath (Join-Path $destinationRoot 'Assets\UMA'))) {
    throw 'Use the existing UMA-free acceptance copy; this option never removes an installed package.'
}
# The converse: Assets\UMA is gitignored, so a worktree or fresh clone has none, and /MIR from one
# would delete it from an acceptance copy that has it. That is a 6,100-file reimport to undo.
if (-not $WithoutUma -and (Test-Path -LiteralPath (Join-Path $destinationRoot 'Assets\UMA')) -and
    -not (Test-Path -LiteralPath (Join-Path $sourceRoot 'Assets\UMA')) -and -not $RetainInstalledUma) {
    throw 'The source has no Assets\UMA and the acceptance copy does; mirroring would remove UMA from it. Run from the main checkout, or use -WithoutUma into the UMA-free copy.'
}
if ($RetainInstalledUma -and ($WithoutUma -or -not (Test-Path -LiteralPath (Join-Path $destinationRoot 'Assets/UMA') -PathType Container))) {
    throw 'Retained UMA mode requires the separately installed destination vendor, and cannot be used for NoUMA.'
}
$projection = New-ReviewInputManifest -ProjectRoot $sourceRoot -WorkflowRoot $sourceRoot -PreviewSync -RetainedRoot $destinationRoot -WithoutUma:$WithoutUma -RetainInstalledUma:$RetainInstalledUma
if ($syncEvidence) { Write-ReviewNewJson $projection (Join-Path $syncEvidence 'projection-before.json') }
$projectUmaOverrides = @(Get-ReviewProjectUmaOverrides -ProjectRoot $sourceRoot -WithoutUma:$WithoutUma)
if ($WithoutUma) {
    # /XD preserves an existing excluded destination. Preflight all four exact targets
    # before removing stale native assets, with no links anywhere in their ancestry/tree.
    $optionalTargets = @()
    foreach ($relative in @(Get-ReviewOptionalUmaContentPaths)) {
        $target = [IO.Path]::GetFullPath((Join-Path $destinationRoot $relative))
        if (-not $target.StartsWith($destinationRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Invalid optional UMA removal boundary: $relative"
        }
        for ($ancestor = $target; -not [string]::IsNullOrEmpty($ancestor); $ancestor = [IO.Path]::GetDirectoryName($ancestor)) {
            $item = Get-Item -LiteralPath $ancestor -Force -ErrorAction SilentlyContinue
            if ($item -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw "Optional UMA cleanup refuses a reparse path: $ancestor"
            }
        }
        $item = Get-Item -LiteralPath $target -Force -ErrorAction SilentlyContinue
        if (-not $item) { continue }
        if ($item.PSIsContainer -eq $relative.EndsWith('.meta', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Unexpected optional UMA target type: $relative"
        }
        if ($item.PSIsContainer) {
            $pending = New-Object 'Collections.Generic.Stack[string]'
            $pending.Push($target)
            while ($pending.Count -gt 0) {
                foreach ($child in Get-ChildItem -LiteralPath $pending.Pop() -Force) {
                    if (($child.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                        throw "Optional UMA cleanup refuses a reparse child: $($child.FullName)"
                    }
                    if ($child.PSIsContainer) { $pending.Push($child.FullName) }
                }
            }
        }
        $optionalTargets += $item
    }
    foreach ($target in $optionalTargets) {
        if ($target.PSIsContainer) { Remove-Item -LiteralPath $target.FullName -Recurse -Force }
        else { Remove-Item -LiteralPath $target.FullName -Force }
    }
}
foreach ($folder in @('Assets','ProjectSettings','ArtSource','Packages')) {
    $sourcePath = [IO.Path]::GetFullPath((Join-Path $sourceRoot $folder))
    $destinationPath = [IO.Path]::GetFullPath((Join-Path $destinationRoot $folder))
    # /MIR can delete destination files. Check the resolved absolute boundary before every mirror.
    if (-not $destinationPath.StartsWith($destinationRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
        -not $sourcePath.StartsWith($sourceRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $sourcePath -PathType Container)) { throw "Invalid mirror boundary for $folder." }
    $copyOptions = @('/MIR','/NJH','/NJS','/NP','/NDL','/NFL','/R:1','/W:1')
    if ($folder -eq 'Assets') {
        # Acceptance-specific UMA library data is deliberately retained.
        $copyOptions += '/XD'
        foreach ($retained in @('Resources','UMAProjectData')) {
            $copyOptions += @((Join-Path $sourcePath $retained),(Join-Path $destinationPath $retained))
        }
        if ($WithoutUma -or $RetainInstalledUma) {
            $copyOptions += @((Join-Path $sourcePath 'UMA'),(Join-Path $destinationPath 'UMA'))
        }
        if ($WithoutUma) {
            foreach ($relative in @(Get-ReviewOptionalUmaContentPaths | Where-Object { -not $_.EndsWith('.meta') })) {
                $copyOptions += @((Join-Path $sourceRoot $relative),(Join-Path $destinationRoot $relative))
            }
        }
        $copyOptions += '/XF'
        foreach ($retained in @('Resources.meta','UMAProjectData.meta')) {
            $copyOptions += @((Join-Path $sourcePath $retained),(Join-Path $destinationPath $retained))
        }
        if ($WithoutUma -or $RetainInstalledUma) {
            $copyOptions += @((Join-Path $sourcePath 'UMA.meta'),(Join-Path $destinationPath 'UMA.meta'))
        }
        if ($WithoutUma) {
            foreach ($relative in @(Get-ReviewOptionalUmaContentPaths | Where-Object { $_.EndsWith('.meta') })) {
                $copyOptions += @((Join-Path $sourceRoot $relative),(Join-Path $destinationRoot $relative))
            }
        }
    }
    & robocopy.exe $sourcePath $destinationPath @copyOptions | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Failed to mirror $folder (robocopy $LASTEXITCODE). No Unity process was started." }
}
# The optional project's preferred index is portable source, while other retained data is not.
# Copy an explicit file allowlist; never mirror this subtree or erase its local cache.
foreach ($relativeOverride in $projectUmaOverrides) {
    $sourceOverride = [IO.Path]::GetFullPath((Join-Path $sourceRoot $relativeOverride))
    $destinationOverride = [IO.Path]::GetFullPath((Join-Path $destinationRoot $relativeOverride))
    if (-not $sourceOverride.StartsWith($sourceRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
        -not $destinationOverride.StartsWith($destinationRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Invalid project UMA index copy boundary.'
    }
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $destinationOverride))
    Copy-Item -LiteralPath $sourceOverride -Destination $destinationOverride -Force
}
$settings = Join-Path $destinationRoot 'ProjectSettings\ProjectSettings.asset'
$content = ConvertTo-ReviewAcceptanceSettings ([IO.File]::ReadAllText($settings)) ([bool]$WithoutUma) ([bool]$RetainInstalledUma)
[IO.File]::WriteAllText($settings,$content,[Text.UTF8Encoding]::new($false))
if ($DisableGpuResidentDrawer) {
    $pipeline = Join-Path $destinationRoot 'Assets\Settings\PC_RPAsset.asset'
    $content = [IO.File]::ReadAllText($pipeline).Replace('m_GPUResidentDrawerMode: 1','m_GPUResidentDrawerMode: 0')
    [IO.File]::WriteAllText($pipeline,$content,[Text.UTF8Encoding]::new($false))
}
if ($WithoutUma) { Assert-ReviewNoUmaContentAbsent $destinationRoot }
$actual = New-ReviewInputManifest -ProjectRoot $destinationRoot -WorkflowRoot $sourceRoot -WithoutUma:$WithoutUma
# Compare actual test GPU0 towards the predicted shipping GPU1; no other projection drift.
$projectionDrift = @(Compare-ReviewInputs -Before $actual -After $projection -AllowShippingGpu -RequireWorkflowStable)
Assert-ReviewRetainedVendorEqual $projection $actual
if ($syncEvidence) {
    Write-ReviewNewJson $actual (Join-Path $syncEvidence 'projection-after.json')
    Write-ReviewNewJson $projectionDrift (Join-Path $syncEvidence 'projection-drift.json')
}
if (@($projectionDrift | Where-Object {-not $_.allowed}).Count) {
    throw ('Mirrored inputs do not match the exact UMA/NoUMA source projection: '+(@($projectionDrift | Where-Object {-not $_.allowed} | ForEach-Object {$_.path}) -join ', '))
}
Assert-ReviewSnapshot $Snapshot
$synced = $true
Write-Output "Snapshot ready: $destinationRoot; GPU Resident Drawer disabled for tests: $DisableGpuResidentDrawer"
} catch { $syncFailure = $_.Exception.Message; throw }
finally {
    try {
        if ($syncEvidence) {
            $sourceDrift = $null
            try { Assert-ReviewManifestEqual $Snapshot.original (New-ReviewRawManifest $originalSource); Assert-ReviewSourceIdentity $Snapshot }
            catch { $sourceDrift = $_.Exception.Message; $synced = $false }
            Write-ReviewNewJson ([ordered]@{schema=1;status=$(if ($synced) {'Synced'} else {'Failed'});destination=$destinationRoot;
                snapshot=$(if ($Snapshot) {$Snapshot.root} else {$null});retainedInstalledUma=[bool]$RetainInstalledUma;
                failure=$syncFailure;sourceFailure=$sourceDrift;nativeTestsExecuted=$false;leaseToken=$(if ($ValidationLease) {$ValidationLease.token} else {$null})}) (Join-Path $syncEvidence 'sync-receipt.json')
            if ($sourceDrift -and -not $syncFailure) { throw $sourceDrift }
        }
    } finally { if ($ownLease) { Exit-ReviewLease $ValidationLease } }
}
