# Compiles the PLAYER variant of the Gamesim assemblies by replaying the response files Unity's
# player build (the "...P.dag" folder) generated. Catches editor-only API used in runtime code,
# which the editor compile cannot see.
#
#   powershell -NoProfile -File Tools\player-compile.ps1
#
# Runs against the batchmode copy (GAMESIM_ACCEPTANCE, default D:\GamesimAcceptance) because only a
# player build generates the P.dag; build one there first with
#   Unity.exe -batchmode -projectPath <copy> -executeMethod Gamesim.Editor.U01ProjectSetup.BuildPortDesktop
# Output goes to %LOCALAPPDATA%\Gamesim\player-compile. GAMESIM_UNITY_EDITOR overrides the editor
# folder, otherwise it is derived from ProjectSettings/ProjectVersion.txt.
$ErrorActionPreference = 'Stop'

$Project = if ($env:GAMESIM_ACCEPTANCE) { $env:GAMESIM_ACCEPTANCE } else { 'D:\GamesimAcceptance' }
$Out     = Join-Path $env:LOCALAPPDATA 'Gamesim\player-compile'

$artifacts = Join-Path $Project 'Library\Bee\artifacts'
$Dag = Get-ChildItem -LiteralPath $artifacts -Directory -Filter '*P.dag' -ErrorAction SilentlyContinue |
       Select-Object -First 1 | ForEach-Object { $_.FullName }
if (-not $Dag) { throw "No player dag under $artifacts - run a player build on the acceptance copy first." }

$Version = (Get-Content -LiteralPath (Join-Path $Project 'ProjectSettings\ProjectVersion.txt') |
            Select-String '^m_EditorVersion:\s*(.+)$').Matches[0].Groups[1].Value.Trim()
$EditorRoot = if ($env:GAMESIM_UNITY_EDITOR) { $env:GAMESIM_UNITY_EDITOR }
              else { "C:\Program Files\Unity\Hub\Editor\$Version\Editor" }
$Sdk     = Get-ChildItem -LiteralPath (Join-Path $EditorRoot 'Data\DotNetSdk\sdk') -Directory |
           Sort-Object Name -Descending | Select-Object -First 1
$Csc     = Join-Path $Sdk.FullName 'Roslyn\bincore\csc.dll'
$Dotnet  = Join-Path $EditorRoot 'Data\DotNetSdk\dotnet.exe'
if (-not (Test-Path -LiteralPath $Dotnet)) { $Dotnet = 'dotnet' }

if (-not (Test-Path -LiteralPath $Out)) { New-Item -ItemType Directory -Force -Path $Out | Out-Null }
Set-Location -LiteralPath $Project
Write-Output "dag: $Dag"

# Sources live in the acceptance copy too (sync-and-run.sh mirrors them), so files created after the
# player build wrote its response files are discovered here, as offline-compile.ps1 does.
$Roots = @{
  'Gamesim.Simulation' = @{ Root = 'Assets\Gamesim\Simulation'; Recurse = $true  }
  'Gamesim.Runtime'    = @{ Root = 'Assets\Gamesim\Runtime';    Recurse = $true  }
  'Gamesim.Uma'        = @{ Root = 'Assets\Gamesim\Uma';        Recurse = $false }
}

# Dependencies first, so a freshly built Simulation is what Runtime compiles against.
$order = @('Gamesim.Simulation', 'Gamesim.Runtime', 'Gamesim.Uma')
$names = Get-ChildItem -LiteralPath $Dag -Filter 'Gamesim*.rsp' -File |
         Where-Object { $_.Name -notlike '*.mvfrm.rsp' -and $_.Name -notlike '*.rsp2' } |
         ForEach-Object { $_.BaseName } |
         Sort-Object { $i = [array]::IndexOf($order, $_); if ($i -lt 0) { 99 } else { $i } }

$failed = 0
foreach ($name in $names) {
    $rsp = Join-Path $Dag "$name.rsp"
    $lines = [System.Collections.Generic.List[string]](Get-Content -LiteralPath $rsp)
    $kept = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $lines) {
        if ($line -like '-refout:*') { continue }
        if ($line -like '-out:*') { $kept.Add('-out:"' + (Join-Path $Out "$name.dll") + '"'); continue }
        if ($line -match '^-r:"Library/Bee/artifacts/[^/]+/(Gamesim\.[A-Za-z.]+)\.ref\.dll"') {
            $dep = Join-Path $Out ($Matches[1] + '.dll')
            if (Test-Path -LiteralPath $dep) { $kept.Add('-r:"' + $dep + '"'); continue }
        }
        $kept.Add($line)
    }

    if ($Roots.ContainsKey($name)) {
        $listed = @{}
        foreach ($line in $kept) {
            if ($line -match '^"(.+\.cs)"$') { $listed[$Matches[1].Replace('/', '\').ToLowerInvariant()] = $true }
        }
        $t = $Roots[$name]
        $root = Join-Path $Project $t.Root
        if (Test-Path -LiteralPath $root) {
            $found = if ($t.Recurse) { Get-ChildItem -LiteralPath $root -Recurse -Filter *.cs -File }
                     else { Get-ChildItem -LiteralPath $root -Filter *.cs -File }
            foreach ($f in $found) {
                $rel = $f.FullName.Substring($Project.Length + 1)
                if (-not $t.Recurse -and $rel -like '*\Editor\*') { continue }
                if ($listed.ContainsKey($rel.ToLowerInvariant())) { continue }
                Write-Output "  + added $rel"
                $kept.Add('"' + $rel.Replace('\', '/') + '"')
            }
        }
    }
    $tmp = Join-Path $Out "$name.rsp"
    Set-Content -LiteralPath $tmp -Value $kept -Encoding utf8

    Write-Output "=== $name (player) ==="
    $result = & $Dotnet exec $Csc /nostdlib /noconfig "@$tmp" 2>&1
    $errors = $result | Where-Object { $_ -match ': error ' }
    if ($errors) { $failed++; $errors | Select-Object -First 12 | ForEach-Object { Write-Output "  $_" } }
    else { Write-Output "  OK" }
}
Write-Output ""
Write-Output "player assemblies with errors: $failed"
if ($failed -gt 0) { exit 1 }
