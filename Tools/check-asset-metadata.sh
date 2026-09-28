#!/bin/sh
# Every tracked asset under Assets/ must have its own tracked .meta, no .meta may be an orphan, no
# two may share a GUID, and no binary .asset may be stored as text. Unity writes a .meta on import,
# so a file committed without one still passes every test on the acceptance copy and only breaks in
# a fresh clone, where Unity invents a new GUID and every reference to the asset points at nothing.
# A binary asset under a text rule breaks the same way: the checkout normalises its line endings and
# Unity cannot load it (LightingData.asset, 2026-09-27). Run this before a commit.
#
# Null-delimited on purpose: this project has space-bearing paths (Assets/TextMesh Pro/...), and the
# obvious `git ls-files | xargs grep` splits them and skips them without saying so.
cd "$(dirname "$0")/.." || exit 1
status=0

missing=$(git ls-files -z Assets | tr '\0' '\n' | grep -v '\.meta$' \
  | while IFS= read -r f; do
      git ls-files --error-unmatch "$f.meta" >/dev/null 2>&1 || echo "  $f"
    done)
[ -n "$missing" ] && { echo "Tracked assets with no tracked .meta:"; echo "$missing"; status=1; }

# A folder's .meta with nothing tracked under it is kept on purpose, as .github/workflows/ci.yml
# says: git carries no empty folders, and deleting the .meta would have Unity mint a new GUID for
# the folder on every machine. So a folder .meta (folderAsset: yes) is never an orphan here, and
# whether the folder exists in this checkout is not the question; CI applies the same rule.
orphans=$(git ls-files -z Assets | tr '\0' '\n' | grep '\.meta$' \
  | while IFS= read -r m; do
      a=${m%.meta}
      git ls-files --error-unmatch "$a" >/dev/null 2>&1 \
        || grep -q '^folderAsset: yes' "$m" 2>/dev/null || echo "  $m"
    done)
[ -n "$orphans" ] && { echo ".meta files with no asset:"; echo "$orphans"; status=1; }

# The committed blob is what a clone gets. A NUL byte in it means binary; the effective attribute
# must then leave it alone (text unset). `text=auto` is also safe, since git detects the NULs, but
# an eol rule on top of anything except unset is not.
binary_as_text=$(git ls-files -z Assets | tr '\0' '\n' | grep '\.asset$' \
  | while IFS= read -r f; do
      nuls=$(git cat-file blob ":$f" 2>/dev/null | head -c 65536 | tr -cd '\000' | wc -c)
      [ "$nuls" -gt 0 ] || continue
      text=$(git check-attr text -- "$f" | sed 's/.*: text: //')
      eol=$(git check-attr eol -- "$f" | sed 's/.*: eol: //')
      if [ "$text" = "set" ] || { [ "$eol" != "unspecified" ] && [ "$text" != "unset" ]; }; then
        echo "  $f (text: $text, eol: $eol)"
      fi
    done)
[ -n "$binary_as_text" ] && { echo "Binary assets git would normalise as text (give them a 'binary' rule in .gitattributes):"; echo "$binary_as_text"; status=1; }

dupes=$(git ls-files -z Assets | grep -z '\.meta$' | xargs -0 grep -h '^guid: ' | sort | uniq -d)
[ -n "$dupes" ] && { echo "GUIDs used by more than one .meta:"; echo "$dupes" | sed 's/^/  /'; status=1; }

count=$(git ls-files -z Assets | grep -z '\.meta$' | xargs -0 grep -hc '^guid: ' | wc -l)
[ "$status" -eq 0 ] && echo "Asset metadata OK: $count metas, none missing, none orphaned, no GUID reused, no binary asset stored as text."
exit $status
