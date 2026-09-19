#!/bin/sh
# Builds the Windows player on the acceptance copy and runs its self-verification.
#
#   Tools/build-and-verify.sh [--graphical] [--no-season] [--no-build] [--name <label>] [player args...]
#
# Mirrors the working tree over the copy first (Tools/sync-and-run.sh with no suites does only
# that), builds with the same entry point the editor menu uses, then runs the exe with
# --gamesim-verify (and --gamesim-verify-season unless --no-season) into an isolated save root
# and prints the reports. Anything else on the command line goes to the player, e.g.
# --gamesim-profile-seconds 300 --gamesim-house-size 16.
#   --graphical  run with a window instead of -batchmode -nographics (the C rows need a window)
#   --no-build   reuse the exe already on the copy
#   --name       label for the output folder and log (default verify-season / verify-profile)
# Override the copy with GAMESIM_ACCEPTANCE (Windows path).
HERE="$(cd "$(dirname "$0")" && pwd)"
DST="${GAMESIM_ACCEPTANCE:-D:\\GamesimAcceptance}"
DSTP="$(cygpath -u "$DST")"
DSTFWD="$(echo "$DST" | tr '\\' '/')"
VER="$(sed -n 's/^m_EditorVersion: //p' "$HERE/../ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
UNITY="${GAMESIM_UNITY:-/c/Program Files/Unity/Hub/Editor/$VER/Editor/Unity.exe}"
EXE="$DSTP/Builds/Port-Windows-V7/Gamesim.exe"

graphical=0; season=1; build=1; name=""; extra=""
while [ $# -gt 0 ]; do
  case "$1" in
    --graphical) graphical=1 ;;
    --no-season) season=0 ;;
    --no-build) build=0 ;;
    --name) shift; name="$1" ;;
    *) extra="$extra $1" ;;
  esac
  shift
done
[ -n "$name" ] || { if [ "$season" = 1 ]; then name=verify-season; else name=verify-profile; fi; }
OUT="$DSTFWD/Logs/$name"
OUTP="$DSTP/Logs/$name"
OUTW="$DST\\Logs\\$name"

if [ "$build" = 1 ]; then
  "$HERE/sync-and-run.sh" || exit 1
  echo "=== building ($(date +%T)) ==="
  rm -f "$DSTP/Logs/build.log"
  "$UNITY" -batchmode -quit -accept-apiupdate -projectPath "$DSTFWD" \
    -executeMethod Gamesim.Editor.U01ProjectSetup.BuildPortDesktop -logFile "$DSTFWD/Logs/build.log"
  echo "build exit: $?"
  grep -iE "error CS|BuildMethodException|Build succeeded|Build failed|Build completed" "$DSTP/Logs/build.log" | head -8
fi
[ -f "$EXE" ] || { echo "NO EXE at $EXE"; exit 1; }
echo "exe: $(ls -la --time-style=+%F\ %T "$EXE" | awk '{print $6, $7, $5}') bytes"

args="--gamesim-verify"
[ "$season" = 1 ] && args="$args --gamesim-verify-season"
echo "=== verifying $name ($(date +%T)): $args$extra ==="
rm -rf "$OUTP"; mkdir -p "$OUTP"
if [ "$graphical" = 1 ]; then
  "$EXE" $args $extra --gamesim-save-root "$OUTW" -logFile "$OUT.log"
else
  # -batchmode alone: -nographics leaves no device for the portrait RenderTextures or SSAO, and
  # the verifier counts every one of those failures as an error before the season starts.
  "$EXE" -batchmode $args $extra --gamesim-save-root "$OUTW" -logFile "$OUT.log"
fi
echo "player exit: $?"
python - "$OUTP" <<'PY'
import json, os, sys
root = sys.argv[1]
for name in ("verification.json", "season-verification.json"):
    p = os.path.join(root, name)
    if not os.path.exists(p): print(name, "MISSING"); continue
    d = json.load(open(p, encoding="utf-8"))
    keep = {k: v for k, v in d.items() if not isinstance(v, (list, dict)) or k in ("errors", "unreachedBranches", "houseEventKinds", "phases")}
    print("---", name, "---"); print(json.dumps(keep, indent=1)[:3500])
PY
grep -iE "exception|error" "$OUTP.log" 2>/dev/null | grep -v "0 errors" | head -8
