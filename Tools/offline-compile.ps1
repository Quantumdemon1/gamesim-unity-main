# Compiles the Gamesim assemblies outside Unity by replaying the response files Unity's Bee build
# already generated, so a change can be type-checked in seconds without the editor. Nothing is
# written back into Library/; all output goes to %LOCALAPPDATA%\Gamesim\offline-compile.
#
#   powershell -NoProfile -File Tools\offline-compile.ps1
#
# Overrides: GAMESIM_ACCEPTANCE (the batchmode copy, default D:\GamesimAcceptance) and
# GAMESIM_UNITY_EDITOR (the editor folder, default derived from ProjectSettings/ProjectVersion.txt).
param([switch]$WithoutUma)
$ErrorActionPreference = 'Stop'

$Project    = Split-Path -Parent $PSScriptRoot
$Acceptance = if ($env:GAMESIM_ACCEPTANCE) { $env:GAMESIM_ACCEPTANCE } else { 'D:\GamesimAcceptance' }
$Out        = Join-Path $env:LOCALAPPDATA ('Gamesim\offline-compile\' + [Guid]::NewGuid().ToString('N'))

# The acceptance copy's dag is preferred because it has response files for every assembly, including
# ones created after the last compile on the live project. Sources still come from $Project, so this
# checks the working tree, not the copy.
function Find-EditorDag([string] $root) {
    $artifacts = Join-Path $root 'Library\Bee\artifacts'
    if (-not (Test-Path -LiteralPath $artifacts)) { return $null }
    $dag = Get-ChildItem -LiteralPath $artifacts -Directory -Filter '*E.dag' | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if ($dag) { return $dag.FullName } else { return $null }
}
$Dag = Find-EditorDag $Acceptance
if (-not $Dag) { throw "No Bee editor dag under $Acceptance - generate the matching acceptance editor cache first." }

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
if ($WithoutUma) { $Targets = @($Targets | Where-Object { $_.Name -notlike 'Gamesim.Uma*' }) }
foreach ($target in $Targets) {
    if (-not (Test-Path -LiteralPath (Join-Path $Dag ($target.Name + '.rsp')))) { throw "Missing required response file: $($target.Name). This is not a complete smoke check." }
}
$runtimeResponse = Get-Content -LiteralPath (Join-Path $Dag 'Gamesim.Runtime.rsp')
if ((@($runtimeResponse | Where-Object { $_ -eq '-define:GAMESIM_UMA' }).Count -gt 0) -eq [bool]$WithoutUma) { throw 'Cached editor response files do not match the requested UMA configuration.' }
if (@($runtimeResponse | Where-Object { $_ -eq '-define:UNITY_EDITOR' }).Count -eq 0) { throw 'Expected editor response files.' }
$compiled = @{}

Set-Location -LiteralPath $Project
Write-Output "dag: $Dag"
$failed = 0

foreach ($t in $Targets) {
    $name = $t.Name
    $rsp  = Join-Path $Dag "$name.rsp"

    $lines = [System.Collections.Generic.List[string]](Get-Content -LiteralPath $rsp)

    # Redirect output; drop the reference-assembly emit we do not need.
    $kept = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $lines) {
        if ($line -match '^".+\.cs"$') { continue } # Rebuild the source list, including removals.
        if ($line -like '-refout:*') { continue }
        if ($line -like '-out:*') { $kept.Add('-out:"' + (Join-Path $Out "$name.dll") + '"'); continue }
        # Prefer freshly built dependencies over the stale ones in the Bee artifacts folder.
        if ($line -match '^-r:"Library/Bee/artifacts/[^/]+/(Gamesim\.[A-Za-z.]+)\.ref\.dll"') {
            $dependency = $Matches[1]
            if (-not $compiled.ContainsKey($dependency)) { throw "Dependency $dependency was not successfully compiled in this invocation." }
            $kept.Add('-r:"' + $compiled[$dependency] + '"'); continue
        }
        $kept.Add($line.Replace('"Library/', '"' + $Acceptance.Replace('\','/') + '/Library/'))
    }

    # Add sources created after the response file was written.
    $listed = @{}
    foreach ($line in $kept) {
        if ($line -match '^"(.+\.cs)"$') { $listed[$Matches[1].Replace('/', '\').ToLowerInvariant()] = $true }
    }
    $root = Join-Path $Project $t.Root
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw "Missing source root for $name." }
    if (Test-Path -LiteralPath $root) {
        $found = if ($t.Recurse) { Get-ChildItem -LiteralPath $root -Recurse -Filter *.cs -File }
                 else { Get-ChildItem -LiteralPath $root -Filter *.cs -File }
        if (@($found).Count -eq 0) { throw "No current sources for $name." }
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
    $compilerExit = $LASTEXITCODE
    $errors = $result | Where-Object { $_ -match '\berror\s+[A-Z]+[0-9]+:' }
    if ($compilerExit -ne 0 -and -not $errors) { $errors = @("Compiler exit $compilerExit") + @($result) }
    if ($errors) {
        $failed++
        $errors | Select-Object -First 25 | ForEach-Object { Write-Output "  $_" }
        throw "Compilation failed for $name; no later assembly may use a stale output."
    } else {
        $compiled[$name] = Join-Path $Out "$name.dll"
        Write-Output "  OK"
    }
}

Write-Output ""
Write-Output "assemblies with errors: $failed"
Write-Output "Fresh assemblies checked: $($compiled.Count) of $($Targets.Count); output: $Out"
if ($compiled.Count -ne $Targets.Count -or $compiled.Count -eq 0) { throw 'Incomplete smoke compilation.' }
if ($failed -gt 0) { exit 1 }
