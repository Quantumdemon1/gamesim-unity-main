#!/bin/sh
# Builds the old-save recorder (Tools/OldSaves/Recorder.cs) against one historical build's own simulation and save
# store, for Assets/Plans/OLD-SAVE-ACCEPTANCE.md. Git Bash on Windows.
#
#   sh Tools/OldSaves/build.sh <commit> <temporary worktree> <output folder> [rules source]
#
# The worktree is created detached with nothing checked out but Assets/Gamesim/Simulation and
# Assets/Gamesim/Runtime/Persistence at <commit>; remove it afterwards with `git worktree remove --force <it>`.
# The recorder is compiled by the editor's own Roslyn with the Simulation assembly's references and defines
# (from the Bee response file of a batchmode copy, GAMESIM_ACCEPTANCE), C# 9, and run by the editor's Mono:
#
#   "$DATA/MonoBleedingEdge/bin/mono.exe" <output folder>/recorder.exe record <save> <record folder>
#
# Persistence files that use UnityEngine (the importer, the profile and preference stores) are left out; the episode
# store, its migrations, frozen contracts and validation are not among them. OldSaveTrace.cs and Recorder.cs come
# from the tree this script is in, so every build hashes a checkpoint with the same code.
set -e
COMMIT="${1:?commit}"; WT="${2:?worktree}"; OUT="${3:?output folder}"; RULES="$4"
HERE=$(cd "$(dirname "$0")" && pwd)
REPO=$(cd "$HERE/../.." && pwd)
GIT="${GIT:-/c/Program Files/Git/cmd/git.exe}"
ACC="${GAMESIM_ACCEPTANCE:-/d/GamesimNoUma}"
DATA="${GAMESIM_UNITY_DATA:-/c/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Data}"
DAG=$(ls -td "$ACC"/Library/Bee/artifacts/*E.dag | head -1)
NEWTONSOFT=$(ls "$ACC"/Library/PackageCache/com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll | head -1)
CSC=$(ls -d "$DATA"/DotNetSdk/sdk/*/Roslyn/bincore/csc.dll | head -1)
win () { cygpath -m "$1"; }

# OLDSAVE_SOURCE_TREE=1 builds from <worktree> as a plain folder, uncommitted edits included, with no git check
# (a scratch copy holding a candidate fix); <commit> is then only a label.
if [ -z "$OLDSAVE_SOURCE_TREE" ]; then
  if [ ! -d "$WT" ]; then
    "$GIT" -C "$REPO" worktree add --detach --no-checkout "$(win "$WT")" "$COMMIT"
    "$GIT" -C "$WT" checkout "$COMMIT" -- Assets/Gamesim/Simulation Assets/Gamesim/Runtime/Persistence
  fi
  [ "$("$GIT" -C "$WT" rev-parse HEAD)" = "$("$GIT" -C "$REPO" rev-parse "$COMMIT^{commit}")" ] || { echo "worktree $WT is not at $COMMIT"; exit 1; }
fi

if [ -z "$RULES" ]; then
  if [ -f "$WT/Assets/Gamesim/Simulation/ShippedRules.cs" ]; then RULES="$HERE/Rules.Shipped.cs"; else RULES="$HERE/Rules.None.cs"; fi
fi

mkdir -p "$OUT"
RSP="$OUT/recorder.rsp"
# OLDSAVE_SIMULATION_RSP names a saved copy of the Simulation response file, for while the batchmode copy is compiling.
SIMRSP="${OLDSAVE_SIMULATION_RSP:-$DAG/Gamesim.Simulation.rsp}"
grep '^-r:"[A-Za-z]:/' "$SIMRSP" > "$RSP"
grep '^-define:' "$SIMRSP" >> "$RSP"
for d in $OLDSAVE_DEFINES; do echo "-define:$d" >> "$RSP"; done
{
  echo "-r:\"$(win "$NEWTONSOFT")\""
  echo "-target:exe"
  echo "-out:\"$(win "$OUT")/recorder.exe\""
  echo "-langversion:9.0"
  echo "-nowarn:0169,0649,0282,1701,1702,0618,0414,0219,0162,0168"
  echo "-nologo"
  echo "-deterministic"
  echo "-optimize+"
} >> "$RSP"
find "$WT/Assets/Gamesim/Simulation" -name '*.cs' | sort | while read -r f; do echo "\"$(win "$f")\""; done >> "$RSP"
for f in "$WT"/Assets/Gamesim/Runtime/Persistence/*.cs; do
  grep -q 'using UnityEngine' "$f" || echo "\"$(win "$f")\"" >> "$RSP"
done
for f in "$REPO/Assets/Gamesim/Tests/EditMode/OldSaveTrace.cs" "$HERE/Recorder.cs" "$RULES"; do echo "\"$(win "$f")\"" >> "$RSP"; done
"$DATA/DotNetSdk/dotnet.exe" exec "$(win "$CSC")" -nostdlib -noconfig "@$(win "$RSP")"
cp -f "$NEWTONSOFT" "$OUT/"
echo "built $OUT/recorder.exe at $COMMIT ($(basename "$RULES"))"
