# Compiles the PLAYER variant of the Gamesim assemblies by replaying the response files Unity's
# player build (the "...P.dag" folder) generated. Catches editor-only API used in runtime code,
# which the editor compile cannot see.
#
#   powershell -NoProfile -File Tools\player-compile.ps1
#
# Reads current working-tree sources, using the acceptance copy's cached player references because
# only a player build generates the P.dag; build one there first with
#   Unity.exe -batchmode -projectPath <copy> -executeMethod Gamesim.Editor.U01ProjectSetup.BuildPortDesktop
# Output goes to %LOCALAPPDATA%\Gamesim\player-compile. GAMESIM_UNITY_EDITOR overrides the editor
# folder, otherwise it is derived from ProjectSettings/ProjectVersion.txt.
param([switch]$WithoutUma)
$ErrorActionPreference = 'Stop'

$Project = Split-Path -Parent $PSScriptRoot
$Acceptance = if ($env:GAMESIM_ACCEPTANCE) { $env:GAMESIM_ACCEPTANCE } else { 'D:\GamesimAcceptance' }
$Out     = Join-Path $env:LOCALAPPDATA ('Gamesim\player-compile\' + [Guid]::NewGuid().ToString('N'))

$artifacts = Join-Path $Acceptance 'Library\Bee\artifacts'
$Dag = Get-ChildItem -LiteralPath $artifacts -Directory -Filter '*P.dag' -ErrorAction SilentlyContinue |
       Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1 | ForEach-Object { $_.FullName }
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
Write-Output "sources: $Project"

# Discover new working-tree sources independently of when the acceptance copy was last synced.
$Roots = @{
  'Gamesim.Simulation' = @{ Root = 'Assets\Gamesim\Simulation'; Recurse = $true  }
  'Gamesim.Runtime'    = @{ Root = 'Assets\Gamesim\Runtime';    Recurse = $true  }
  'Gamesim.Uma'        = @{ Root = 'Assets\Gamesim\Uma';        Recurse = $false }
}

# Dependencies first, so a freshly built Simulation is what Runtime compiles against.
$order = @('Gamesim.Simulation', 'Gamesim.Runtime', 'Gamesim.Uma')
$names = if ($WithoutUma) { @('Gamesim.Simulation', 'Gamesim.Runtime') } else { $order }
foreach ($name in $names) {
    if (-not (Test-Path -LiteralPath (Join-Path $Dag ($name + '.rsp')))) { throw "Missing required player response file: $name." }
}
$runtimeResponse = Get-Content -LiteralPath (Join-Path $Dag 'Gamesim.Runtime.rsp')
if ((@($runtimeResponse | Where-Object { $_ -eq '-define:GAMESIM_UMA' }).Count -gt 0) -eq [bool]$WithoutUma) { throw 'Cached player response files do not match the requested UMA configuration.' }
if (@($runtimeResponse | Where-Object { $_ -eq '-define:UNITY_EDITOR' }).Count -ne 0) { throw 'Editor response files cannot prove player compilation.' }
$compiled = @{}

$failed = 0
foreach ($name in $names) {
    $rsp = Join-Path $Dag "$name.rsp"
    $lines = [System.Collections.Generic.List[string]](Get-Content -LiteralPath $rsp)
    $kept = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $lines) {
        if ($line -match '^".+\.cs"$') { continue }
        if ($line -like '-refout:*') { continue }
        if ($line -like '-out:*') { $kept.Add('-out:"' + (Join-Path $Out "$name.dll") + '"'); continue }
        if ($line -match '^-r:"Library/Bee/artifacts/[^/]+/(Gamesim\.[A-Za-z.]+)\.ref\.dll"') {
            $dependency = $Matches[1]
            if (-not $compiled.ContainsKey($dependency)) { throw "Dependency $dependency was not successfully compiled in this invocation." }
            $kept.Add('-r:"' + $compiled[$dependency] + '"'); continue
        }
        $kept.Add($line.Replace('"Library/', '"' + $Acceptance.Replace('\','/') + '/Library/'))
    }

    if ($Roots.ContainsKey($name)) {
        $listed = @{}
        foreach ($line in $kept) {
            if ($line -match '^"(.+\.cs)"$') { $listed[$Matches[1].Replace('/', '\').ToLowerInvariant()] = $true }
        }
        $t = $Roots[$name]
        $root = Join-Path $Project $t.Root
        if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw "Missing player source root for $name." }
        if (Test-Path -LiteralPath $root) {
            $found = if ($t.Recurse) { Get-ChildItem -LiteralPath $root -Recurse -Filter *.cs -File }
                     else { Get-ChildItem -LiteralPath $root -Filter *.cs -File }
            if (@($found).Count -eq 0) { throw "No current player sources for $name." }
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
    $compilerExit = $LASTEXITCODE
    $errors = $result | Where-Object { $_ -match '\berror\s+[A-Z]+[0-9]+:' }
    if ($errors -or $compilerExit -ne 0) {
        $failed++
        if (-not $errors) { $errors = @("Compiler exit $compilerExit") + @($result) }
        $errors | Select-Object -First 12 | ForEach-Object { Write-Output "  $_" }
        throw "Compilation failed for $name; no later assembly may use a stale output."
    }
    else { $compiled[$name] = Join-Path $Out "$name.dll"; Write-Output "  OK" }
}
Write-Output ""
Write-Output "player assemblies with errors: $failed"
Write-Output "Fresh player assemblies checked: $($compiled.Count) of $($names.Count); output: $Out"
if ($compiled.Count -ne $names.Count -or $compiled.Count -eq 0) { throw 'Incomplete player smoke compilation.' }
if ($failed -gt 0) { exit 1 }
