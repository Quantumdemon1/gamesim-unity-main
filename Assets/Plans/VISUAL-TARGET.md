# The visual target — bringing the game to the mockups' level

*Written 2026-09-19 against twelve mockups in `ArtSource/reference/mockups/` and the build of that
day (`290261c`, plus Dan's body in `6d5c6f5`). Companion to `MASTER-PLAN.md` Part 4; the master plan's
sequencing table carries this as the presentation's next phase.*

## 1. What the mockups are, and what that means for the work

The twelve images are the game's own screens, drawn photoreal. They reuse the game's copy word for
word — "CHOOSE YOUR HOUSEGUEST · Pick who you play as, then set the size of the house. Everyone else
is cast from the same roster" is `CastSelect`'s heading and subtitle; "Current Objective", "Social
Points", "Days Left", the cast strip, the left rail of Overview / Houseguests / Relationships / Tasks
/ Journal / Settings are the HUD as it stands. Every panel in them maps to something the build
already has:

| Mockup | What it shows | What exists today |
| --- | --- | --- |
| 01 living room | a houseguest selected, a radial of Chat · Flirt · Strategize · Joke · Gossip, a relationships column, social goals, a cast strip | `EpisodeHud` social actions (fourteen, as rows), `SocialGraph`, the notebook's goals, `CastRail` (left edge) |
| 02 choose your houseguest | roster tabs, category filters, cards with photo · name · age · archetype · two traits, house size stepper | `CastSelect`: the same tabs, filters, cards (24 templates), stepper, copy |
| 03 house overview | an isometric cutaway with labelled rooms, a live-feed thumbnail, recent events, social goals | the house and its eight room markers, `HouseMap` (rooms as cards), the notebook's event log; no overview camera, no live feed |
| 04 conflict | an argument, "How should you respond?" with five choices, a conflict meter, house-vibe bars | house events and storylines resolved by choice, moods per houseguest; no vibe bars, no conflict meter |
| 05 HoH competition | a neon stage in the yard, lanes, a timer, per-houseguest stage progress | three minigames and `CompetitionResult`; the yard's `bb_set_compring`; the timing bar |
| 06 night overview | sleeping houseguests, a secret conversation, "You can eavesdrop" | NPC conversations with venues and a caption, the eavesdrop action, the room-tone clock; no sleep, no night |
| 07 relationship web | a graph with five edge kinds, a selected-houseguest column with allies, rivals, secrets | `SocialGraph` (nodes, edges by trust), the notebook's intel and secrets |
| 08 eviction vote | the diary chair, two nominee cards, confirm | the eviction stages and ballot, `VoteReveal`, the diary room |
| 09 HoH nominees | a three-way conversation with bubbles, a private-chat log, nominee cards with trust · threat · recent conflict, a strategy insight | nominations, alliances, trust scores, the NPC conversation system; no bubbles, no threat bar, no insight card |
| 10 nomination ceremony | everyone gathered, a screen with two nominees, reactions | `KeyCeremony`, `CeremonyTakeover`, the four authored reactions |
| 11 diary room | the confessional chair, subtitles, Talk Strategy · Vent · Reflect · Discuss Alliance · End | the diary room and its reflections and study choices |
| 12 kitchen conversation | a radial around the pair, a two-person relationship card | the focused-NPC conversation panel, trust deltas |

So this is not a redesign of the game. It is the same game with four layers of presentation raised,
and the layers are not equal. Ranked by how much of the mockups' impression they carry per day of
work:

1. **The UI chrome** — the dark glass panels, the cyan hairlines, the chips, the icons, the mood
   faces, the font, the radial. Half the impression; the cheapest layer; entirely in our hands.
2. **Light and post** — warm practicals, LED strips, neon bloom, a night outside the glass, baked
   bounce, ambient occlusion, depth of field on the close shots. A quarter of the impression; two
   days; mostly settings and a bake.
3. **The cast** — people who look like people. A quarter of the impression and the only layer with a
   ceiling we cannot lift; see §2.
4. **The set's surfaces** — marble, oak, plaster, glass, plants. The last tenth; a week with CC0
   assets through the pipeline we already have.

## 2. The ceiling, stated once

The faces in the mockups are offline AI renders. No real-time cast this project can afford will
photograph like that, and the plan should not pretend otherwise. The reachable target is
**stylised-realistic and cinematic**: UMA 3 bodies (already in the working tree) with real hair, real
clothes and real facial expressions, standing in a lit, textured house under the mockups' chrome. At
that point the *screens* match the mockups — the layout, the light, the atmosphere, the colour — and
the people are convincing game characters rather than photographs. Call that the target and measure
against it.

If a photoreal cast is a must, it is a different project: a licensed character generator (Reallusion
Character Creator, a few hundred dollars, produces game-ready photoreal humans with the Unity
export), a move from URP to HDRP for the skin and hair shaders that need it, and a machine to
profile it on. That is a decision to take with the rest of this plan done, not before.

## 3. Where the game is today (measured, not remembered)

From the editor on 2026-09-19:

| Layer | State |
| --- | --- |
| Lighting | 37 realtime point lights (lamps, television glow, two fills); **0 lightmaps, 0 light probes, 0 reflection probes**; ambient flat; default skybox; no fog; **nothing in the scene is marked static** |
| Camera and AA | one camera, FOV 55, pitch tied to distance; **no anti-aliasing** (MSAA 1, camera AA none); HDR on |
| Post | `HouseVolumeProfile`: bloom, tonemapping, colour adjustments, vignette; SSAO on `PC_Renderer`; colour grading LDR; no depth of field, no film grain, no lens flare, no shadows/midtones/highlights grade |
| Materials | 997 URP/Lit, 110 with a base texture (floors, plaster); the rest flat colour; emissives: the neon signs, the screens, the candle |
| Cast | six low-poly CC0 bodies in the repo; UMA 3 bodies in the working tree (940 assets, gitignored, 1 GB); Dan on the low-poly rig |
| UI | `UiTheme`: navy surfaces, pale-neon-blue accent, gold, procedurally generated rounded panels (radius 10, 2 px border), generated icon set; **one font (LiberationSans)**; rectangles built in code |
| Screens | every screen in §1's table exists; the cast strip is on the left, the mockups put it along the bottom |

The "before" captures are in `ArtSource/reference/before/` (the graphical verification's house,
settings and season-phase shots from this day's build, with the UMA cast the working tree runs).
Regenerate them with `Tools/build-and-verify.sh --graphical --name look`. Three tell most of it:

- `season-house-event.png` — the house from its default height and pitch: rooms already edged in
  neon strips (yellow and blue), flat-colour furniture under flat light, the cast small at that
  distance, a translucent navy panel with a blue border over it. The chrome's language is the
  mockups' language already; the distance, the light and the surfaces are not.
- `season-finale.png` — the competition card and a portrait in a gold ring: the UMA head renders
  pale and dark, which is the portrait lighting, not the face. V3's portrait rig is that fix.
- `house.png` — the opening: the cast cards carry coloured discs, because the portraits are
  rendered from built bodies and the opening runs before UMA has built them. A V3 item: portraits
  from the look's recipe, ahead of the body.

## 4. The mockups' visual language, itemised

Everything below is what a screen has to have to read as one of the mockups. It is the checklist
for Phase V2 and the sampling sheet for `UiTheme`.

**Chrome.** A top bar: logo left; a week · day · time chip; an objective chip with an icon; three
stat chips (houseguests, social points, days left); a tagline right. A left rail of six items with
icons, the active one filled. A right column of cards. A bottom strip of cast chips. A quote line in
the bottom-right card. Neon taglines *in the world* ("Good People, Bigger Stories", "Same house.
Different stories.") on the walls of the set.

**Panels.** Near-black navy at about 85 % opacity, a one-pixel cyan-blue hairline, a soft outer glow
of the same hue, corner radius 12–16. Headings 16 px semibold in white; body 13 px in a muted
blue-grey; every card heading carries an icon. Chips are pills in an accent colour with a dark
inside. Buttons are gradient-filled with the hairline; the primary one glows.

**Type.** A geometric sans (Inter or Manrope, both OFL). Headings tracked and uppercase. Small
labels in caps at 11 px.

**Colour.** Background `#0B1220`; hairline `#3AA0FF`; glow `#5CC8FF`; accents by meaning — pink
`#FF4FA3` (flirt, playful), purple `#A56BFF` (strategic, gossip), yellow `#FFC93C` (joke, ambitious),
green `#4ADE80` (reassure, chill, allied), red `#FF5A5A` (conflict, nominated), gold `#FFC726`
(competition, HoH). These extend `UiTheme`'s tokens rather than replace them: the navy, the pale
blue, the gold and the danger red are already there.

**People on screen.** A portrait chip: photo, name, a mood face in the mood's colour, a one-word
state, a relationship bar. Mood faces are glyphs, not emoji fonts: happy, confident, playful,
charming, tense, shocked, concerned, amused, suspicious, anxious, relieved, observant — drawn once
each. Name tags above heads carry the mood glyph. Speech bubbles for what an NPC is saying, with a
tail. A green diamond over the selected houseguest. A radial around a houseguest you are talking
to: a hub with their portrait and six to seven petals.

**Cards that exist in the mockups and not in the build.** Live Feed (a second camera's picture with a
caption). Recent Events as a timeline with icons and times. Social Goals with progress. House Vibe
(fun, trust, drama, harmony) and Conflict Status (two faces and a tension bar). Potential Nominees
with trust · threat · recent conflict. Strategy Insight (three lines of advice). Secrets Known.

**World.** Warm practicals (2700–3000 K) with visible bulbs; LED strips under counters and along
floor edges in white and blue; pink and blue neon; candles; plants in every shot; marble, oak, glass
and brushed metal; a pool that glows; a night sky and city lights beyond the glass; bloom on every
emissive; shallow depth of field on two-shots and the diary chair; a faint blue haze.

**Camera.** Scenes at eye height or a little above, 20–30° down, 35–50 mm. The overview from high
and steep, near-orthographic, the roof gone, room labels as chips. The diary room over the
shoulder. The competition wide from behind the crowd.

## 5. The plan

Phases are ordered by impression per day. Each names the tools, the assets and their licences, what
"done" looks like, and who does it: **me** means through the MCPs and scripts, unattended; **you**
means a login, a purchase or an eye.

### V0 — A look sheet, so progress is a picture (½ day, me)

One command that starts the built player, walks to the twelve situations the mockups show, and
captures each at 1920 × 1080: the living room with a houseguest selected and the action panel open;
the cast screen; the overview; a house event's choice panel; a competition mid-play; a night scene
with a conversation; the relationship graph; the eviction ballot; a nomination discussion; the key
ceremony; the diary room; a kitchen conversation. Built on `PortVerification.Season`, which already
walks the season and photographs phases. Output beside the mockups as `after-NN.png`, so a
side-by-side is `mockup-NN` against `after-NN`. **Done when** the twelve pairs regenerate from one
command and the acceptance matrix has a row for them.

### V1 — Light the house (1–2 days, me; the biggest change per hour)

- Mark the shell, the floors and every set piece static; leave characters, doors and clutter that
  moves dynamic. An editor method does it by folder, so a re-export keeps it.
- Bake: lightmaps (Progressive GPU, 2 texels/unit for the shell, 4 for hero furniture), a light
  probe group on a 1.5 m grid per room, one reflection probe per room. The probe placement comes
  from the eight room markers.
- Lights: the practicals become Mixed (baked indirect, realtime direct) with shadows on the eight
  hero lamps only; colour temperature 2900 K for lamps, 5500 K blue for screens; LED strips as thin
  emissive quads (`bb_mat_led_warm`, `bb_mat_led_blue`) under the kitchen run, the bar and the HoH
  bed; neon taglines as emissive text (`bb_mat_neon_pink`, `bb_mat_neon_blue`) on two walls.
- Outside: a night HDRI from Poly Haven (CC0; `dikhololo_night` or `moonless_golf`) as the sky and
  the ambient, dim, with a city-lights plane beyond the yard's glass.
- Camera: MSAA 4× on `PC_RPAsset` (or SMAA on the camera), SSAO tuned to 0.5 intensity, 0.3 m
  radius.
- Post: ACES tonemapping; a grade that pushes shadows to navy and highlights warm
  (Shadows/Midtones/Highlights); bloom threshold 0.9, intensity 0.6, scatter 0.7 so only emissives
  bloom; vignette 0.25; film grain 0.1; URP 17's screen-space lens flare on the neon; **depth of
  field** in a second volume that the director's framing presets blend in for two-shots and the
  diary chair (`EpisodeDirector.CeremonyFraming` already moves the camera; it gains a volume weight).
- A subtle blue fog (0.01) for the haze.

**Done when** the look sheet's living room, kitchen, night and diary shots have bounce light, soft
shadows, glowing neon and a dark window, and the windowed profile in the acceptance matrix stays
within its thresholds. Tests: a PlayMode test that the episode scene ships lightmaps, a probe group
and a reflection probe per room, and that the camera has anti-aliasing.

### V2 — The chrome (3–5 days, me; the mockups' identity)

All of it lives in `UiTheme`, `HudPrimitives` and the screens, which build their rectangles in code,
so the change is tokens and primitives first and screens second.

1. **Tokens.** The palette in §4 as named colours beside the existing ones; radius 14; a
   glass-panel style (surface at 85 %, hairline, glow) generated the way the rounded sprites are —
   a second nine-slice with a blurred ring — so nothing ships as an image.
2. **Type.** Inter (OFL) from Google Fonts, three TMP font assets (regular, semibold, a display
   weight for headings), tracked uppercase headings through a `Heading` primitive. LiberationSans
   stays as the fallback.
3. **Glyphs.** The generated icon set grows to the mockups' vocabulary (house, people, heart, task,
   journal, settings, camera, calendar, star, trophy, crown, gavel, ear, chat, flirt, bulb, joke,
   gossip, handshake, exit, bed, dumbbell, fork) and gains the twelve mood faces, each drawn once
   as a vector and rasterised at 64 px into the atlas.
4. **Primitives.** Portrait chip (photo, name, mood glyph, state word, bar); pill chip; card with an
   icon heading; timeline row (icon, time, line); progress row; gradient button; bubble.
5. **Screens**, in the order the mockups are looked at: the HUD's top bar and left rail; the cast
   strip moved to the bottom as chips (its captions do not change — they are the contract the tests
   and screen readers use); the right column as cards (Relationships with mood and hearts, Social
   Goals, Recent Events, Live Feed placeholder); the conversation radial around the focused NPC,
   which groups the fourteen social actions into the mockups' seven petals with the rest one level
   in; speech bubbles for the NPC caption; the diamond over the selection (on `FollowRing`); the
   cast screen's cards and stepper; the relationship web's legend and side column; the eviction
   ballot; the diary room's five options; the nominee cards with trust · threat · recent conflict
   (threat is the competition record plus alliance count, already computed for the jury); the
   house-vibe bars (four sums over the week's events); the conflict meter.
6. **Motion** stays what `HudMotion` does now — fade, press, travel — with a glow pulse on the
   primary button.

**Done when** every look-sheet pair reads as the same screen at a glance, and every existing
PlayMode test that finds a control by caption still finds it. Cost to the profile: none that
matters; it is the same canvas with better sprites.

### V3 — The cast (1–2 weeks; me for the plumbing, you for the eye and two logins)

Route A from the master plan, taken: **UMA 3 is the shipped cast.**

- **The clone story** (me, ½ day): UMA is a gigabyte and stays out of Git. A documented one-time
  step — install UMA 3 from the Asset Store into `Assets/UMA` — and the define that already
  bootstraps on its presence. A clone without it keeps the low-poly cast and every test.
- **Twenty-four looks** (me, 3 days; you, an afternoon of "not that nose"): one per template, as
  data — race, the DNA sliders for face and build, skin tone, hair, an outfit from UMA's included
  wardrobe, the palette colour on the fabric (`UmaBodyTint` does this already). The eight of the
  mockups first: Alex, Emma, Jordan, Casey, Riley, Jamie, Taylor, Maya, matched to their cards.
- **Faces** (me, 2 days): UMA's expression player drives real brows and mouths from `mood` ×
  `stressLevel`, replacing the eye-cluster shapes for UMA bodies; a lip flap while `Talking`.
- **Animation** (you, an hour on Mixamo with an Adobe login; me, a day to wire): the loops and
  reactions on Humanoid — sit, sit-talk, talk, listen, argue, cheer, celebrate, shrug, sleep — so a
  UMA body plays what the low-poly rig plays today. The controller already has the states.
- **Portraits** (me, ½ day): a three-point portrait rig in `CharacterPortraits` at 512 px with a
  soft key, a rim and a dark backdrop, which is where the mockups' headshot look comes from.
- **Hair and clothes beyond the box** (you, a decision): UMA's free content covers casual wear and
  a dozen hairstyles; the mockups' variety (the pink hair, the sequins, the suits) is Asset Store
  packs at $10–40 each, or Blender-authored garments on UMA's base mesh through the pipeline.

**Done when** the eight mockup houseguests stand in the lit house in the look sheet and a viewer
can name each from their card. The windowed profile at twelve is re-run; UMA's cost was measured
today at 3.2 ms median and that is the number to hold.

### V4 — The set's surfaces (1 week, me)

Poly Haven is CC0 and reachable from the Blender MCP as of today (85 furniture models, several
hundred textures, night HDRIs).

- A tool, `ArtSource/tools/bb_polyhaven.py`: fetch a model or texture set by id, import it,
  decimate to the §4.3 detail budget, build two LODs, rename to `bb_`, pack the textures to
  URP's layout with `bb_bake.py`'s writer, export through `export_collection`. The catalogue picks
  it up by name, so a hero piece replaces its flat-colour twin with no scene edit.
- Hero pieces first, one per room, because they are what the camera dwells on: the sofa, the dining
  table and chairs, the kitchen island and stools, the beds, the bar, the coffee table, the
  loungers, the diary chair.
- Surfaces: marble for the island and the bathroom, oak for the living room and bedrooms, tile for
  the kitchen floor, plaster with a normal map for the walls, fabric for the sofa — applied through
  the existing bake tool so the tiling stays world-scale.
- Glass: the yard's doors and the pool's fence as URP transparent with a reflection probe behind
  them; the pool's water gains a scrolling normal map and an emissive tint.
- Plants: Poly Haven's potted plants, one type per room, at LOD1 beyond four metres.

**Done when** each room's look-sheet capture holds up against the overview mockup at the game's
camera distance, and the tri budget per room (§4.3) and the profile hold.

### V5 — Camera modes and the live feed (3–4 days, me)

- **Overview**: a camera preset — 62° down, 30 m, a 20° field of view for the near-orthographic
  look — reached from the rail's Overview item and the map key; room labels as world-space chips
  from the eight markers; characters keep moving under it. `HouseMap`'s cards become its side
  column.
- **Two-shot**: a conversation preset at 25° down, 4 m, framing both bodies, with the depth-of-field
  volume weighted in; the ceremony framing partial already owns the move.
- **Diary chair**: over the shoulder, the screen in frame.
- **Competition**: a wide from behind the crowd, the lanes lit.
- **Live Feed**: a second camera rendering to a 320 × 180 texture, aimed at the venue of the most
  recent NPC conversation or event, with the event's sentence as its caption; the card sits in the
  right column. Cost: one extra camera at low resolution every third frame.
- **Night**: the room tone's clock already knows the hour; the sky, the practicals and the LED
  strips follow it, and houseguests in beds after midnight is a Seated-style state on the existing
  bed venues.

Cinemachine is not needed for any of this; the rig's timed moves are what the shots want.

### V6 — Motion, reactions, life (ongoing)

Facial expressions at every mood change (V3); arm gestures in arguments and cheering at
competitions (the Mixamo set); idle variety so sixteen people are not one person; the crowd at a
ceremony turning to look at the nominees (the reactions already fire; the heads should turn).

## 6. Downloads and tools

| What | Source | Licence | Who |
| --- | --- | --- | --- |
| Poly Haven models, textures, HDRIs | the Blender MCP (enabled 2026-09-19) or `api.polyhaven.com` | CC0 | me |
| Inter (and Manrope as a fallback candidate) | Google Fonts on GitHub | OFL 1.1 | me |
| UMA 3 | Asset Store, already in `Assets/UMA` | MIT-style UMA licence | installed |
| Mixamo clips (sit, talk, argue, cheer, celebrate, sleep) | mixamo.com | Adobe general terms, game use allowed | **you** (delivered 2026-09-20: twelve takes) |
| Extra hair and wardrobe for UMA | Asset Store packs | per pack | **you** (decision, purchase) |
| Cinemachine | UPM | Unity | not needed |
| SSAO, depth of field, lens flare, MSAA, lightmapper | URP 17.6 / Unity 6 | in the project | in |
| Blender 5.2 with `bb_bake.py`, `bb_export.py`, the MCP | in the project | in | in |

Poly Pizza and Sketchfab need API keys in the addon; neither is required, Poly Haven covers the set.

## 7. Sequence and estimate

| Phase | Days | Depends on | Owner |
| --- | --- | --- | --- |
| V0 look sheet | ½ | — | me |
| V1 light the house | 1–2 | V0 | me |
| V2 chrome | 3–5 | V0 | me |
| V3 cast | 5–10 | V1 for the light on skin; your logins | me + you |
| V4 surfaces | 5 | V1 for the bake | me |
| V5 camera modes, live feed | 3–4 | V1, V2 | me |
| V6 motion | ongoing | V3 | me + you |

V1 and V2 are the first fortnight and carry most of the distance. The decision in §2 — whether the
stylised-realistic ceiling is the target or a photoreal cast is a must — is the one thing to settle
before V3 starts; everything before it is worth doing either way.

## 7a. Status (2026-09-19, late evening)

| Phase | State | Where |
| --- | --- | --- |
| V0 look sheet | done: twelve captures and a report from a graphical build, each moment marked reached or nearest with the reason | `PortVerification.LookSheet.cs`, `Tools/build-and-verify.sh --look-sheet`, `ArtSource/reference/after/after-NN.png` |
| V1 light the house | done: the pass, the bake, the sky, the grade, the probes; pinned by `Lighting_TheEpisodeSceneShipsItsBakedNight` | `HouseCinematicLighting.cs`, `ArtSource/reference/after/set-after-lighting.png` |
| V2 chrome | done for the screens the mockups draw: the top bar and the right column, the conversation as a dial, the cast strip, the roster cards, the five ceremony overlays, the relationship web, the rooms, and — 2026-09-20 — the episode panel docked low and wide so the house fills the top of the frame, the conversation caption as a bubble over the pair, and the house-vibe bars. Left: the vote section's rows; the relationships and social-goals cards; the conflict meter. **All three want screen room the HUD does not have** — see below | `EpisodeHud.cs`, `EpisodeHud.Chrome.cs`, `HouseConversationCaption.cs`, `HouseVibe.cs`, `CastRail.cs`, `RelationshipWeb.cs` |
| V3 cast | done but for your eye: a look for each of the roster's houseguests as data, expressions driven from mood × stress with a lip flap, the portrait rig lit in three points, and the twelve mocap takes you downloaded wired into a Humanoid controller so a UMA body sits, talks, argues and celebrates instead of dropping every cue but `Speed`. Left: your eye on the twenty-four looks, three reaction beats no take covers, and a Listen state — see below | `UmaCastLibrary.cs`, `UmaExpressions.cs`, `CharacterPortraits.cs`, `HumanoidClipWiring.cs` |
| V4 surfaces | done but for two: seven hero pieces, scanned floors and walls, plants, the long table's sixteen chairs, and — 2026-09-20 — the competition stage mockup-05 asks for: three lit lanes, a gate over each, the stacking prop the houseguests are actually working at, a crate of spares, a 28 m panelled backdrop with eight tubes, and the lettering on it. Left: the glass (yard doors, pool fence, the water's scroll) and marble, which is a mesh material rather than a floor | `bb_set_comp_*.py`, `bb_set_sign.py`, `HouseSetPieces.Course`, `ArtSource/tools/bb_polyhaven.py`, `HouseFloorDressing.cs` |
| V5 camera, live feed | done: the overview, the conversation's two-shot, the diary chair, the competition wide, and the live feed's second camera and card. The night clock is not done and is not costed here — see below | `HouseCameraRig.Shot`, `EpisodeDirector.Overview/LiveFeed`, `LiveFeed.cs` |
| V6 motion | heads turn to the subject of every ceremony — the veto included, which was the one beat that never turned a head until 2026-09-20 — every body idles on its own phase rather than one of five, and the Mixamo set is in: twelve Humanoid takes, a standing idle and a walk borrowed from UMA at runtime so nothing outside this repository is serialised. Left: three reaction beats — see below | `CharacterPresentation.LookAt`, `EpisodeDirector.Ceremony.cs`, `HumanoidClipWiring.cs` |

**The night clock, honestly.** V5 assumed "the room tone's clock already knows the hour". It does
not: the room tone follows the room under the camera, and the simulation carries a week and a phase
and no hour at all. A day cycle would need either a saved field, which means a schema version and a
migration, or a presentational clock that resets on every load — and either way it would fight the
bake, because the house is lightmapped for night and a mixed light's indirect term is fixed at bake
time. The night the mockups show is the night V1 delivered; houseguests asleep after midnight is a
separate feature with a real cost, not a finishing touch, and it is not in this plan's ledger.

**The HUD is out of room, and that is now the blocker.** Three of the mockups' cards have nowhere
to go: Relationships and Social Goals from mockup-01, and the conflict meter from mockup-04. The
right column is already Live Feed plus Recent Events and runs into the exploration controls at
y 660; the left column is Brand plus Objective plus House Vibe and runs into the interaction prompt
at y 741. The mockups fit more because they spend the space differently: the cast strip is a row
along the BOTTOM rather than a column down the LEFT, and the left gutter carries a six-item
navigation rail instead of twelve faces. That one move frees a 92-px column the full height of the
screen and is what every remaining card is waiting on. It is not a small change - `LeftColumnX` is
derived from `CastRail.Width`, the ceremony overlays inset against it, and three panels already own
the bottom band - but it is one change rather than three, and nothing else on the V2 list can land
until it is made.

**The panel is docked, and what that did not fix.** The episode panel was a 790x680 block in the
middle of a 1600x900 canvas: nine of the twelve captures were mostly one opaque rectangle, while
twelve of the twelve mockups are mostly the house with the decision drawn as a wide, short card low
in the frame. It now sits on the floor of the frame at 900 x 300, clear of the status band and the
interaction prompt, and the house fills the top half. It grows to its old footprint for one screen
only: the conversation dial is a ring of seven cards 538 units tall and clipping it mid-petal is
worse than a taller panel, so seating a dial restores the height and the top edge the dial was
tuned against. Even there the panel is narrower than it was, so the live feed and the recent-events
card are no longer behind it.

Two things this deliberately did not do. The mockups float the dial **in the world** around the
houseguest, with one-word petals; ours stays in the panel because the keyboard ring walks the
controls inside the panel and a dial outside it is focus the ring cannot reach, which
`Chrome_MorePetalHandsTheKeyboardBeneathTheDial` pins on purpose. And the ceremony scrims stay at
0.975: the code records that 0.93 was tried and rejected because the house behind it competed with
the card for attention, and the mockups do not solve that with a lighter overlay but by staging the
ceremony on a lit screen **in the room**, with the cast seated watching it. That screen is the next
piece of geometry worth building, not a number worth lowering.

**The set's lettering, and why it is geometry.** Seven walls in the mockups carry lit type and the
house had none of it: a room with a lit sign reads as a built set, and the same room without one
reads as a grey box with furniture in it. `ArtSource/setpieces/bb_set_sign.py` takes a string, a
font, a cap height and whether it wants a dark backing, and extrudes the letters as real geometry
rather than painting them on a quad — neon IS a tube with depth, and a flat alpha-mapped panel gives
the trick away at exactly the angle the overview camera looks from. It also needs no texture
pipeline: the letters carry the same `bb_mat_neon_*` name every other glowing thing here does, so
the importer lights them without a new rule. Six signs are built; three hang on the
competition backdrop. The living room's `GAMESIM` and `Good Company` are cut and not hung, and the
reason is worth writing down rather than rediscovering: **this house has no wall to hang them on.**
Its walls are cutaways 1.1 m tall - `South cutaway wall` is 28.3 m long and stands from 0.05 m to
1.15 m - because the overview camera looks down into the rooms and a full-height wall would close
them. The mockups' living room is rendered with real walls, so its neon sits above a sofa at about
2 m. Hanging ours needs a decision, not a placement: either a feature wall behind the sofa that is
tall on one side only, or the sign standing on the wall top like a light box. The competition
backdrop did not have this problem because the yard's stage wall is a piece of set dressing rather
than architecture.

**Three reaction beats, honestly.** The twelve takes cover a win and a cheer. They do not cover the
three a houseguest takes badly — being nominated, being saved by the veto, being evicted — and the
nearest takes in the set are a shrug and a round of applause, which would have a houseguest shrug
off their own eviction and applaud their own rescue. Those three triggers are therefore left
undeclared on the Humanoid controller: `CharacterPresentation` sends a cue only to a controller that
declares it, so the body holds its idle for the beat, which is what a UMA body did before any of
this. The low-poly cast still acts all five out from its authored takes, so nothing regressed.

Two rules for whoever fills them, both learned the hard way on 2026-09-20:

- **The save and the nomination land together or not at all.** A veto ceremony fires both in the
  same instant, `Saved` at whoever came off the block and `Nominated` at whoever replaced them, and
  one implies the other because the block keeps its size. Wiring only the save was tried and backed
  out: it leaves the houseguest just put up — what the scene is about — the one body in the room
  not moving, which is not half the scene but the scene inverted.
- **The take has to change the silhouette.** A ceremony is framed room-wide at eleven metres
  (`CeremonyFraming.CeremonyDistance`), where faces are still readable and nothing smaller is. The
  authored Generic take for the save is "the arms lift, the head comes up"; a Mixamo "Relieved Sigh"
  was downloaded, imported and removed again because it has no arm movement at all — at that
  distance it reads as nothing. Ask for a relief take that opens the arms.

So it is two more downloads, not three: a dejected or defeated standing take for the nomination and
the eviction, and a relief take with an upward beat for the save. Each drops in as
`Assets/Gamesim/Art/Authored/Animation/Humanoid/bb_anim_React_<beat>.fbx`, plus the state name in
`HumanoidClipWiring.Reactions` and one run of **Gamesim > U07 > Wire the Humanoid takes**.

**One cue the UMA cast still drops.** `Listening` is declared on the Humanoid controller and
answered by nothing: there is no Listen state, so a UMA body that is being talked at falls out of
the talk ring into its idle, and the only sign it is in a conversation is the procedural head nod.
The Generic cast plays a whole `Listen_loop` for the same cue. That is a third download — an
attentive standing take, not a cheerful one — and a state beside `Talk` in `HumanoidClipWiring`.

## 8. Definition of done

Twelve side-by-sides in `ArtSource/reference/` that a stranger would call the same screens; the
acceptance matrix's C rows within their thresholds at twelve houseguests, windowed; every existing
test green, the captions unchanged; a clone without UMA still runs and tests on the low-poly cast.
