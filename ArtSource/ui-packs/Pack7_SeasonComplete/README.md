# GameSim Season Complete Pack 7

Purpose
-------
This pack supports the redesigned Season Complete / Final Stats / Winner presentation.

Design goals
------------
- Make the winner and runner-up the immediate focal point.
- Surface the five core season scores without making the screen feel like a spreadsheet.
- Keep Final Standings, Jury Vote Breakdown, Season Timeline, and Career readable simultaneously.
- Use gold only for winner / achievement / power states.
- Keep jury-member rows and career history lower-emphasis.
- Remove raw/debug-feeling dense tables where a card, badge, or bar communicates better.
- Keep the current uGUI architecture; skin existing controls instead of rebuilding test-dependent hierarchy.

Recommended layout
------------------
1. Header: SEASON COMPLETE + summary + four actions.
2. Winner/Runner-Up hero row.
3. Five primary stat cards.
4. Three-column body:
   - Final Standings
   - Jury Vote Breakdown
   - Season Timeline
5. Bottom career strip.
6. Separate tabs/filters for deeper house and career drill-down.

Recommended Unity import
------------------------
- *_9slice.png: Sprite (2D and UI), Image Type = Sliced, Mip Maps Off.
- Badges/icons: Sprite Simple, Preserve Aspect On.
- Charts: Sprite Simple; mask/scale horizontally for dynamic values.
- Decorative images: Raycast Target Off.

Animation
---------
- Winner hero: 250–350 ms gold glow in.
- Stat cards: 50–80 ms stagger.
- Jury votes: reveal one row at a time, 120–180 ms.
- Timeline: animate node progression left-to-right.
- Career strip: fade in after season stats settle.

Do not expose raw filesystem/save-slot paths on this screen.
