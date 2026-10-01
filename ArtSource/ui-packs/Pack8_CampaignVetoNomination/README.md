# GameSim Pack 8 — Campaign, Veto Selection & Nomination

Purpose
-------
This pack skins the three redesigned weekly strategy screens:
1. Campaign
2. Power of Veto Player Selection
3. Nomination

It is designed for the current uGUI project and the established GameSim semantic palette.

Core UX rules
--------------
CAMPAIGN
- Put the nominee/player situation first.
- Houseguest cards are the main action surface.
- Relationship and vote confidence are separate dimensions.
- Show Actions Left persistently.
- Keep recent intel and goals visible without requiring scroll.
- Use tabs for deeper views: Talk, Relationship Intel, Talking Points, Storylines, Voting Outlook.

VETO PLAYER SELECTION
- Fit the complete draw on one 1080p screen.
- Separate automatic participants from eligible players.
- Make the draw bag/three slots the visual center.
- Reveal-draw is the single primary CTA.
- Do not force scrolling just to understand who is playing.

NOMINATION
- Use a clear four-step phase tracker:
  Lobby -> Stay Off the Block -> Nomination Ceremony -> Outcome.
- Show HOH, phase, conversations left, and objective as summary cards.
- Character cards communicate HOH/Nominee/selected status.
- Use red only for nomination/danger states.
- Put the next step in a single bottom CTA rather than a long text report.

Unity import
------------
*_9slice.png
- Texture Type: Sprite (2D and UI)
- Image Type: Sliced
- Mip Maps: Off
- Wrap: Clamp
- Alpha Is Transparency: On

Meters / chips / icons
- Sprite Simple
- Preserve Aspect where appropriate
- Decorative children: Raycast Target Off

Implementation
--------------
Preserve current tested uGUI Button/Text/TMP hierarchy where possible.
Replace Image sprites and add non-interactive child visuals rather than rebuilding test-dependent controls.

Suggested motion
----------------
- Card hover: 100–140 ms, scale 1.00 -> 1.02
- Selected card: cyan/red/gold edge fades in 140–180 ms
- Draw chips: 120 ms stagger, then reveal card
- Phase tracker transition: 180–240 ms
- Intel/toast: slide + fade 160–220 ms
