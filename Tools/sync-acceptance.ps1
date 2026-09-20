# Mirrors only named project folders into the pre-existing, idle acceptance copy.
param([switch]$DisableGpuResidentDrawer, [switch]$WithoutUma)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'review-evidence.ps1')
$sourceRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\','/')
$destinationRoot = (Resolve-Path -LiteralPath $(if ($env:GAMESIM_ACCEPTANCE) { $env:GAMESIM_ACCEPTANCE } else { 'D:\GamesimAcceptance' })).Path.TrimEnd('\','/')
if ($destinationRoot -eq $sourceRoot -or $destinationRoot.StartsWith($sourceRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
    $sourceRoot.StartsWith($destinationRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath (Join-Path $destinationRoot 'ProjectSettings\ProjectVersion.txt'))) {
    throw 'The destination must be a separate existing Unity acceptance project.'
}
$busy = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -match [regex]::Escape($destinationRoot) -or $_.CommandLine -match [regex]::Escape($destinationRoot.Replace('\','/'))
}
if ($busy) { throw 'The acceptance editor is still running; nothing was copied.' }
if ($WithoutUma -and (Test-Path -LiteralPath (Join-Path $destinationRoot 'Assets\UMA'))) {
    throw 'Use the existing UMA-free acceptance copy; this option never removes an installed package.'
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
        if ($WithoutUma) { $copyOptions += @((Join-Path $sourcePath 'UMA'),(Join-Path $destinationPath 'UMA')) }
        $copyOptions += '/XF'
        foreach ($retained in @('Resources.meta','UMAProjectData.meta')) {
            $copyOptions += @((Join-Path $sourcePath $retained),(Join-Path $destinationPath $retained))
        }
        if ($WithoutUma) { $copyOptions += (Join-Path $sourcePath 'UMA.meta') }
    }
    & robocopy.exe $sourcePath $destinationPath @copyOptions | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Failed to mirror $folder (robocopy $LASTEXITCODE). No Unity process was started." }
}
$settings = Join-Path $destinationRoot 'ProjectSettings\ProjectSettings.asset'
$content = ConvertTo-ReviewAcceptanceSettings ([IO.File]::ReadAllText($settings)) ([bool]$WithoutUma)
[IO.File]::WriteAllText($settings,$content,[Text.UTF8Encoding]::new($false))
if ($DisableGpuResidentDrawer) {
    $pipeline = Join-Path $destinationRoot 'Assets\Settings\PC_RPAsset.asset'
    $content = [IO.File]::ReadAllText($pipeline).Replace('m_GPUResidentDrawerMode: 1','m_GPUResidentDrawerMode: 0')
    [IO.File]::WriteAllText($pipeline,$content,[Text.UTF8Encoding]::new($false))
}
Write-Output "Snapshot ready: $destinationRoot; GPU Resident Drawer disabled for tests: $DisableGpuResidentDrawer"
