#!/bin/sh
# Historical V7 helper is intentionally fail-closed: it could launch an old player after build failure.
echo 'This historical helper is retired. Run Tools/verify-review-candidate.ps1 -Name <fresh-name> and Tools/build-review-candidate.ps1 -TestName <passing-name>.' >&2
echo 'Launch the reported review candidate explicitly with an isolated --gamesim-save-root for graphical verification.' >&2
exit 2