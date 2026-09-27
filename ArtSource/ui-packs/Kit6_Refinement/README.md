# GameSim UI Refinement Kit 6

A screen-specific follow-up for the menus missed by the earlier competition/navigation infographic.

## Start here

- `Docs/SCREEN_BY_SCREEN_REVIEW.md` — review of each supplied menu and its proposed composition.
- `Previews/00_screen_index.png` — visual index; eight complete screens also ship individually.
- `Previews/ReviewBoards/` — five current-versus-proposed annotated comparison boards.
- `Docs/UNITY_INTEGRATION.md` — import settings, correct layering, theme binding and migration boundaries.
- `Docs/REGRESSION_CHECKLIST.md` — what to test; no claim these Unity tests were run.
- `Docs/PACK_1_TO_5_COMPATIBILITY.md` — where old example sprites need care.
- `Docs/ASSET_INVENTORY.csv` — exact runtime sprite inventory.
- `Docs/LAYOUTS_1920x1080.json` — reference-space layout proposals.

## Runtime contents

71 tintable PNG sprites and 44 original SVG sources. Copy only `Assets/GameSim/UI/RefinementKit6` into Unity. Optional manual Editor importer is under Tools > GameSim > Refinement Kit 6. It only touches manifest-listed kit textures.

No UI Toolkit, HDRP, paid add-on, replacement gameplay controller, or lighting change is required. Keep uGUI, URP 17.6, existing UiTheme and the pinned baked-night scene.

The eight screen PNGs are static design references, not implemented Unity prefabs. Their portrait/camera samples are unmodified crops from the supplied images. Those crops are not high-resolution replacements for the original portrait assets. No font files are distributed. Rendered sample words remain in previews only, not in runtime sprites.

Proposed layout and copy changes will require targeted approval of affected rect/caption assertions; preserve behavior/privacy tests. The Editor C# helper has not been tested in the user's Unity project.
