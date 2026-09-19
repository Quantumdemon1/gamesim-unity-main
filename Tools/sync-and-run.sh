#!/bin/sh
# Runs the Gamesim test suites headless: waits for the batchmode copy on D: to be free, THEN mirrors
# this working tree over it (Assets, ProjectSettings and ArtSource), THEN runs each requested suite.
#
#   Tools/sync-and-run.sh edit:EditMode:Gamesim.EditModeTests play:PlayMode:Gamesim.PlayModeTests
#
# Each argument is name:platform:assembly; results land in <copy>/Logs/<name>.xml and .log.
# Override the copy with GAMESIM_ACCEPTANCE (Windows path). See Tools/README.md for the traps.
#
# The sync has to be inside the wait: a robocopy issued while a previous run was still going once
# swapped the assets out from under a live PlayMode run - the run kept going and its result meant
# nothing.
SRC_POSIX="$(cd "$(dirname "$0")/.." && pwd)"
SRC="$(cygpath -w "$SRC_POSIX")"
DST="${GAMESIM_ACCEPTANCE:-D:\\GamesimAcceptance}"
DSTP="$(cygpath -u "$DST")"
DSTFWD="$(echo "$DST" | tr '\\' '/')"
VER="$(sed -n 's/^m_EditorVersion: //p' "$SRC_POSIX/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
UNITY="${GAMESIM_UNITY:-/c/Program Files/Unity/Hub/Editor/$VER/Editor/Unity.exe}"

[ -f "$UNITY" ] || { echo "no Unity at $UNITY (set GAMESIM_UNITY)"; exit 1; }
[ -d "$DSTP" ] || { echo "no acceptance copy at $DST - robocopy Assets, Packages, ProjectSettings, UserSettings and Library there first"; exit 1; }

waitfree () {
  i=0
  while [ $i -lt 180 ]; do
    busy=$(powershell -NoProfile -Command "(Get-CimInstance Win32_Process -Filter \"Name='Unity.exe'\" | Where-Object {\$_.CommandLine -match 'GamesimAcceptance' -and \$_.CommandLine -notmatch 'AssetImportWorker'} | Measure-Object).Count" 2>/dev/null | tr -d '\r')
    [ "$busy" = "0" ] && return 0
    sleep 10; i=$((i+1))
  done
  return 1
}

waitfree || { echo "project never freed; nothing was synced or run"; exit 1; }

powershell -NoProfile -Command "robocopy '$SRC\\Assets' '$DST\\Assets' /MIR /NJH /NJS /NP /NDL /NFL /XD '$DST\\Assets\\Resources' '$DST\\Assets\\UMAProjectData' | Out-Null; robocopy '$SRC\\ProjectSettings' '$DST\\ProjectSettings' /MIR /NJH /NJS /NP /NDL /NFL | Out-Null; robocopy '$SRC\\ArtSource' '$DST\\ArtSource' /MIR /NJH /NJS /NP /NDL /NFL | Out-Null; if (\$LASTEXITCODE -lt 8) { 'sync ok' } else { exit 1 }" || { echo "SYNC FAILED"; exit 1; }

# Test-copy-only: the PC render pipeline asset's GPU Resident Drawer crashes PlayMode runs
# intermittently. The repository still ships it enabled; this never travels back.
sed -i 's/m_GPUResidentDrawerMode: 1/m_GPUResidentDrawerMode: 0/' "$DSTP/Assets/Settings/PC_RPAsset.asset"

mkdir -p "$DSTP/Logs"
for pair in "$@"; do
  n=$(echo "$pair"|cut -d: -f1); p=$(echo "$pair"|cut -d: -f2); a=$(echo "$pair"|cut -d: -f3)
  waitfree || { echo "$n: project never freed"; continue; }
  rm -f "$DSTP/Logs/$n.log" "$DSTP/Logs/$n.xml"
  # Forward slashes on purpose: backslash paths here silently drop segments and disable -assemblyNames.
  "$UNITY" -batchmode -accept-apiupdate -projectPath "$DSTFWD" -runTests \
    -testPlatform "$p" -assemblyNames "$a" \
    -testResults "$DSTFWD/Logs/$n.xml" -logFile "$DSTFWD/Logs/$n.log"
  echo "=== $n: $(grep -o 'Test run completed.*' "$DSTP/Logs/$n.log" 2>/dev/null | tail -1) crashes: $(grep -c 'Crash!!!' "$DSTP/Logs/$n.log" 2>/dev/null)"
  if [ -f "$DSTP/Logs/$n.xml" ]; then
    head -c 700 "$DSTP/Logs/$n.xml" | grep -o 'total="[0-9]*"\|passed="[0-9]*"\|failed="[0-9]*"' | tr '\n' ' '; echo
    grep -o '<test-case[^>]*result="Failed"[^>]*' "$DSTP/Logs/$n.xml" | grep -o 'fullname="[^"]*"' | head -20
  else echo "  NO XML"; tail -2 "$DSTP/Logs/$n.log" 2>/dev/null; fi
done
