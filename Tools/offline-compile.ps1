# Compiles the Gamesim assemblies outside Unity by replaying the response files Unity's Bee build
# already generated, so a change can be type-checked in seconds without the editor. Nothing is
# written back into Library/; all output goes to %LOCALAPPDATA%\Gamesim\offline-compile.
#
#   powershell -NoProfile -File Tools\offline-compile.ps1
#
# Overrides: GAMESIM_ACCEPTANCE (the batchmode copy, default D:\GamesimAcceptance) and
# GAMESIM_UNITY_EDITOR (the editor folder, default derived from ProjectSettings/ProjectVersion.txt).
$ErrorActionPreference = 'Stop'

$Project    = Split-Path -Parent $PSScriptRoot
$Acceptance = if ($env:GAMESIM_ACCEPTANCE) { $env:GAMESIM_ACCEPTANCE } else { 'D:\GamesimAcceptance' }
$Out        = Join-Path $env:LOCALAPPDATA 'Gamesim\offline-compile'

# The acceptance copy's dag is preferred because it has response files for every assembly, including
# ones created after the last compile on the live project. Sources still come from $Project, so this
# checks the working tree, not the copy.
function Find-EditorDag([string] $root) {
    $artifacts = Join-Path $root 'Library\Bee\artifacts'
    if (-not (Test-Path -LiteralPath $artifacts)) { return $null }
    $dag = Get-ChildItem -LiteralPath $artifacts -Directory -Filter '*E.dag' | Select-Object -First 1
    if ($dag) { return $dag.FullName } else { return $null }
}
$Dag = Find-EditorDag $Acceptance
if (-not $Dag) { $Dag = Find-EditorDag $Project }
if (-not $Dag) { throw "No Bee editor dag under $Acceptance or $Project - open the project in Unity once." }

$Version = (Get-Content -LiteralPath (Join-Path $Project 'ProjectSettings\ProjectVersion.txt') |
            Select-String '^m_EditorVersion:\s*(.+)$').Matches[0].Groups[1].Value.Trim()
$EditorRoot = if ($env:GAMESIM_UNITY_EDITOR) { $env:GAMESIM_UNITY_EDITOR }
              else { "C:\Program Files\Unity\Hub\Editor\$Version\Editor" }
$Sdk    = Get-ChildItem -LiteralPath (Join-Path $EditorRoot 'Data\DotNetSdk\sdk') -Directory |
          Sort-Object Name -Descending | Select-Object -First 1
$Csc    = Join-Path $Sdk.FullName 'Roslyn\bincore\csc.dll'
$Dotnet = Join-Path $EditorRoot 'Data\DotNetSdk\dotnet.exe'
if (-not (Test-Path -LiteralPath $Dotnet)) { $Dotnet = 'dotnet' }
if (-not (Test-Path -LiteralPath $Out)) { New-Item -ItemType Directory -Force -Path $Out | Out-Null }

# assembly -> source root (relative to the project) used to discover files added since the
# response file was generated.
$Targets = @(
  # Simulation first: Gamesim.Runtime references it, and the script only prefers a freshly
  # built dependency over the stale Bee one when the fresh output already exists.
  @{ Name = 'Gamesim.Simulation';        Root = 'Assets\Gamesim\Simulation';     Recurse = $true  },
  @{ Name = 'Gamesim.Runtime';           Root = 'Assets\Gamesim\Runtime';        Recurse = $true  },
  @{ Name = 'Gamesim.Uma';               Root = 'Assets\Gamesim\Uma';            Recurse = $false },
  @{ Name = 'Gamesim.Uma.Editor';        Root = 'Assets\Gamesim\Uma\Editor';     Recurse = $true  },
  @{ Name = 'Gamesim.Editor';            Root = 'Assets\Gamesim\Editor';         Recurse = $true  },
  @{ Name = 'Gamesim.PlayModeTests';     Root = 'Assets\Gamesim\Tests\PlayMode'; Recurse = $true  },
  @{ Name = 'Gamesim.EditModeTests';     Root = 'Assets\Gamesim\Tests\EditMode'; Recurse = $true  },
  @{ Name = 'Gamesim.Uma.PlayModeTests'; Root = 'Assets\Gamesim\Uma\Tests';      Recurse = $true  }
)

Set-Location -LiteralPath $Project
Write-Output "dag: $Dag"
$failed = 0

foreach ($t in $Targets) {
    $name = $t.Name
    $rsp  = Join-Path $Dag "$name.rsp"
    if (-not (Test-Path -LiteralPath $rsp)) { Write-Output "SKIP $name (no response file)"; continue }

    $lines = [System.Collections.Generic.List[string]](Get-Content -LiteralPath $rsp)

    # Redirect output; drop the reference-assembly emit we do not need.
    $kept = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $lines) {
        if ($line -like '-refout:*') { continue }
        if ($line -like '-out:*') { $kept.Add('-out:"' + (Join-Path $Out "$name.dll") + '"'); continue }
        # Prefer freshly built dependencies over the stale ones in the Bee artifacts folder.
        if ($line -match '^-r:"Library/Bee/artifacts/[^/]+/(Gamesim\.[A-Za-z.]+)\.ref\.dll"') {
            $dep = Join-Path $Out ($Matches[1] + '.dll')
            if (Test-Path -LiteralPath $dep) { $kept.Add('-r:"' + $dep + '"'); continue }
        }
        $kept.Add($line)
    }

    # Add sources created after the response file was written.
    $listed = @{}
    foreach ($line in $kept) {
        if ($line -match '^"(.+\.cs)"$') { $listed[$Matches[1].Replace('/', '\').ToLowerInvariant()] = $true }
    }
    $root = Join-Path $Project $t.Root
    if (Test-Path -LiteralPath $root) {
        $found = if ($t.Recurse) { Get-ChildItem -LiteralPath $root -Recurse -Filter *.cs -File }
                 else { Get-ChildItem -LiteralPath $root -Filter *.cs -File }
        foreach ($f in $found) {
            $rel = $f.FullName.Substring($Project.Length + 1)
            # Gamesim.Uma owns only its top level; Editor/ is a separate assembly.
            if (-not $t.Recurse -and $rel -like '*\Editor\*') { continue }
            if ($listed.ContainsKey($rel.ToLowerInvariant())) { continue }
            Write-Output "  + added $rel"
            $kept.Add('"' + $rel.Replace('\', '/') + '"')
        }
    }

    $tmp = Join-Path $Out "$name.rsp"
    Set-Content -LiteralPath $tmp -Value $kept -Encoding utf8

    Write-Output "=== $name ==="
    $result = & $Dotnet exec $Csc /nostdlib /noconfig "@$tmp" 2>&1
    $errors = $result | Where-Object { $_ -match ': error ' }
    if ($errors) {
        $failed++
        $errors | Select-Object -First 25 | ForEach-Object { Write-Output "  $_" }
    } else {
        Write-Output "  OK"
    }
}

Write-Output ""
Write-Output "assemblies with errors: $failed"
if ($failed -gt 0) { exit 1 }
