#!/bin/sh
# Every tracked asset under Assets/ must have its own tracked .meta, no .meta may be an orphan, and
# no two may share a GUID. Unity writes a .meta on import, so a file committed without one still
# passes every test on the acceptance copy and only breaks in a fresh clone, where Unity invents a
# new GUID and every reference to the asset points at nothing. Run this before a commit.
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

orphans=$(git ls-files -z Assets | tr '\0' '\n' | grep '\.meta$' \
  | while IFS= read -r m; do
      a=${m%.meta}
      git ls-files --error-unmatch "$a" >/dev/null 2>&1 || [ -d "$a" ] || echo "  $m"
    done)
[ -n "$orphans" ] && { echo ".meta files with no asset:"; echo "$orphans"; status=1; }

dupes=$(git ls-files -z Assets | grep -z '\.meta$' | xargs -0 grep -h '^guid: ' | sort | uniq -d)
[ -n "$dupes" ] && { echo "GUIDs used by more than one .meta:"; echo "$dupes" | sed 's/^/  /'; status=1; }

count=$(git ls-files -z Assets | grep -z '\.meta$' | xargs -0 grep -hc '^guid: ' | wc -l)
[ "$status" -eq 0 ] && echo "Asset metadata OK: $count metas, none missing, none orphaned, no GUID reused."
exit $status
