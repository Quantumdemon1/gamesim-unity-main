#!/bin/sh
# Diagnostic bridge: same destination lease/snapshot/XML gates as full verification.
# Each argument remains name:platform:assembly[:filter]. Never overwrites prior artifacts.
set -eu
[ "$#" -gt 0 ] || { echo 'Supply name:platform:assembly[:filter]'; exit 2; }
SRC_POSIX="$(cd "$(dirname "$0")/.." && pwd)"
SPECS=''
for spec in "$@"; do
  [ -z "$SPECS" ] || SPECS="$SPECS
"
  SPECS="$SPECS$spec"
done
NAME="diagnostic-$(date -u '+%Y%m%dT%H%M%SZ')-$$"
# Explicit installed-vendor retention is required for vendor-free worktrees.
if [ "${GAMESIM_WITHOUT_UMA:-0}" = '1' ]; then
  powershell -NoProfile -File "$SRC_POSIX/Tools/run-review-suites.ps1" -Name "$NAME" -WithoutUma -SuiteSpecifications "$SPECS"
elif [ "${GAMESIM_RETAIN_INSTALLED_UMA:-0}" = '1' ]; then
  powershell -NoProfile -File "$SRC_POSIX/Tools/run-review-suites.ps1" -Name "$NAME" -RetainInstalledUma -SuiteSpecifications "$SPECS"
else
  powershell -NoProfile -File "$SRC_POSIX/Tools/run-review-suites.ps1" -Name "$NAME" -SuiteSpecifications "$SPECS"
fi
