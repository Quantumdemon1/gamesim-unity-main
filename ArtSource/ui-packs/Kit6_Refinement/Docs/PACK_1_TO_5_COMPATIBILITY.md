# How Kit 6 relates to the earlier packs

Do not overwrite Packs 1–5. This kit is a small, additive correction layer, not a wholesale replacement or a theme reset.

During this pass, the mounted ZIPs were inspected programmatically for image dimensions and component examples:

| Earlier item | Observed construction | How to use it safely |
|---|---|---|
| Pack 1 `panel_resting_9slice.png` | 528×272 including its margins | Keep the explicit rect fixed; do not swap SetNativeSize between state variants |
| Pack 1 `panel_selected_9slice.png` | 592×336, with larger glow margins | Do not use the same assumed small slice border as the resting image; prefer Kit 6 fill/edge/halo separation |
| Pack 2 `meter_allied_fill.png` | Track and partial fill appear in one bitmap | Treat as a visual example; use a real empty track + runtime fill for values |
| Pack 3 `threat_gauge.png` | Gauge and needle share the same bitmap | Do not display as a live gauge unless its parts are separated; a number is safer until the value mapping is specified |
| Pack 4 `diary_choice_selected_9slice.png` | Colored/glowing selected-state art | Do not make it the default background of every private record row |

Specific replacements in this kit:

- `panel/card/button_fill` + `*_edge_rest` + `*_edge_focus`: matching dimensions per control family.
- `panel_focus_halo`: separate decorative layer with explicit 32-unit outward padding at 1:1 reference PPU.
- `meter_track` + `meter_fill_rect`: no number or partial percentage baked in.
- White/tintable status/empty-state art: existing UiTheme remains authoritative.
- Exact per-sprite border manifest and manual import helper: no generic “try 40 px” rule.

The regenerated concept layouts do not promise a more detailed 3D house. Furniture, rig intersections, facial animation, source portrait resolution and world-light glare are separate work. Kit 6 deliberately preserves the actual scene captures in its conversation and Diary Room references.
