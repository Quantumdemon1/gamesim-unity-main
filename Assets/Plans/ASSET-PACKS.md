# Asset packs 1-5 - what was imported and where each file is meant to go

Five packs of UI and house art, imported 2026-09-22. **Nothing is wired yet**: every file is in the project with
fixed import settings, and this document says where each one is meant to be used. Wiring is the next step and
is not done by importing.

- **Where they live.** UI sprites (packs 1-4, and pack 5's broadcast cards) are under
  `Assets/Gamesim/Resources/Packs/<Pack>/<Category>/`, because the UI is built in code and loads by path:
  `Resources.Load<Sprite>("Packs/Pack1_Foundation/Panels/panel_resting_9slice")`. Pack 5's world textures are under
  `Assets/Gamesim/Art/Packs/Pack5_HouseBroadcast/`, outside Resources, because the house is dressed by editor scripts
  that reference textures from the saved scene - so the build ships only what the house uses.
- **Reference material** (preview sheets, READMEs, the pack-5 manifest, pack 1's SVG sources and the packs 1-4
  implementation guide) is in `ArtSource/ui-packs/`, not in the build.
- **Import settings** are applied by `Assets/Gamesim/Editor/UiPackImporter.cs` from `UiPackCatalogue.cs`, which
  `ArtSource/ui-packs/tools/bb_ui_packs.py` generates from the zips; `UiPackImportTests` holds every file to it.
  Kinds: **UiSliced** (Sprite, full-rect mesh, 9-slice border measured from the pixels), **UiSprite** (Sprite, drawn
  whole), **WorldColour** and **Particle** (Default, mipmapped, sRGB), **NeonMask** and **DetailMask** (Default,
  mipmapped, linear; a detail mask repeats). UI sprites are uncompressed up to 1024 px on a side and high-quality
  compressed above; everything else is high-quality compressed.
- **Border** is Unity's left, bottom, right, top. **Body inset** is how far the solid body (alpha >= 128) sits inside
  the image - the glow and padding a rect must allow so the visible edge lands where intended. Several buttons carry
  ~40 px of baked glow and 81-83 px borders on a 208 px image: drop one into a 57-unit row as it is and it visibly
  shrinks (see Buttons).

## Read before wiring anything

The destinations below were mapped against the code by one agent per pack and then checked by a second agent that
corrected what the first got wrong. The recurring cautions:

- **This UI draws its chrome procedurally** (`UiTheme.Glass`, `AddGlow`, `AddBorder`, `EpisodeHud.Chrome`), and
  many tests pin colours, child names (`Border`, `Glow`) and sprite names. A baked border or glow in a pack sprite
  is invisible to those guards - `Chrome_WearsNoAccentEdgeWhileNothingIsAskingToBeActedOn` only reads `Border`
  children - and cannot be promoted on hover by `HudEmphasis.Promote`.
- **Colour is baked** into most pack sprites, so `Image.color` goes white and `UiTheme` tokens stop applying; check
  contrast against the guards in `UiThemeContrastTests` before swapping a token-coloured surface for a baked one.
- **Captions are identity.** Skin around the existing control and keep its caption in its existing Text child.
- **Several destinations do not exist yet** (marked below) - they are new screens or objects, not reskins.

## Pack 1 - Pack1_Foundation

Foundational visual grammar: panels, buttons, chips, rings, badges, glows, icons, background.

### Backgrounds

**Destination** (partly exists): Main target: the "Scrim" in CastSelect.Rebuild() (Runtime/Presentation/CastSelect.cs:245, scrim at :266). This is the guide's "Choose Your Houseguest" screen. Add the image as a child Image with raycast off, first sibling under the opaque scrim. The scrim itself must keep raycastTarget=true because it catches clicks (:268). The other full-screen scrims are HudPrimitives.Fill objects: MainMenu.Rebuild (MainMenu.cs:102, scrim :113, .98), CharacterCreator.RebuildForm (CharacterCreator.cs:202, scrim :209), WeeklyRecapScreen (WeeklyRecapScreen.cs:142) and SeasonReport (SeasonReport.cs:169). Each lives on its own runtime canvas ("Gamesim Cast Select", "Gamesim Main Menu", "Gamesim Character Creator", "Gamesim Weekly Recap", "Gamesim Season Report"), created by X.Attach under the director "Gamesim Episode" (EpisodeDirector.cs:155-165). None is a saved scene object. Leave the ceremony scrims (VoteReveal :228, KeyCeremony :204, CompetitionResult :96) alone, because they are meant to show the house through them.

*Evidence:* CastSelect.cs:171 root 'Gamesim Cast Select', :245 Rebuild, :266-269 Scrim=Fill(UiTheme.Background) + raycastTarget; MainMenu.cs:59,102,113; CharacterCreator.cs:85,202,209; WeeklyRecapScreen.cs:66,142; SeasonReport.cs:117,169; EpisodeDirector.cs:155,156,159,160,165; director GO 'Gamesim Episode' made by Editor/EpisodeProjectSetup.cs:215 and saved in EpisodeHouse.unity

*Caution:* UiThemeContrastTests.TheMockupsTokens_ClearTheirMinimumsOnGlass (Tests/EditMode/UiThemeContrastTests.cs:108, lift assertion :134-135) requires CardFill to measure more than 1.12:1 against UiTheme.Background #0B1220 (it measures 1.21 on that flat colour). The image is not flat: it runs from #072039 at top centre down to #050B16. I measured CardFill (#1A2438 at .95) at 1.06:1 against the lit top, so the roster cards (CastSelect.cs:482/514) would vanish there while the test, which measures a token no longer on screen, keeps passing. EpisodePlayModeTests.CastChrome.cs:178 pins the card's own colour to CardFill, so the fix belongs in the background, for example by darkening the top. For the main menu, the guide's recipe calls for an in-engine house image instead, so this image is only the fallback there.

- `background_dark_navy_1920x1080.png` - Opaque radial/vertical gradient (#072039 at top centre, #050B16 at the bottom and edges), not the flat Background token. UiPackImporter.Apply (Editor/UiPackImporter.cs:~83) leaves UI sprites up to 1024 px uncompressed and compresses this one CompressedHQ because it is over that limit.

Files in `Assets/Gamesim/Resources/Packs/Pack1_Foundation/Backgrounds/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `background_dark_navy_1920x1080.png` | 1920x1080 | UiSprite |  |  |

### Badges

**Destination** (partly exists): HudPrimitives.AddRoleMark (Runtime/Presentation/HudPrimitives.cs:351; RoleMark enum :334, IconFor :412, Tint :423). The pack sprite replaces the procedural Disc("Role mark") + "Role glyph" pair and the drawn fallback shapes (:381-406). badge_hoh maps to RoleMark.HeadOfHousehold, badge_veto to VetoHolder, badge_nominee to Nominee. Callers: CastRail.cs:357, RelationshipWeb.cs:482 (web node) and :812 (focus column), VoteReveal.cs:262, KeyCeremony.cs:286/323, and EpisodeHud.cs:982 (the portrait modal row decoration, which the draft missed). badge_selected has no RoleMark. Its closest fit is CastSelect.Card's chosen state (CastSelect.cs:529-533 plus the 'PLAYING AS' line :679-681).

*Evidence:* HudPrimitives.cs:351-409 (Disc 'Role mark' + 'Role glyph' from UiTheme.Icon crown/target/veto-token, glyph colour OnColor(Tint) :375); callers CastRail.cs:357, RelationshipWeb.cs:482,812, VoteReveal.cs:262, KeyCeremony.cs:286,323, EpisodeHud.cs:982; grep shows no test references 'Role mark'/'Role glyph'

*Caution:* No test pins these objects. The badges have their colours baked in (gold #F6C344, red #FF4D5E; UiTheme.Gold is #FFC726 and Danger is #FF6B6B), so Image.color must go white and Tint() stops applying. Keep the procedural path as the fallback for a clone without the pack. Size is max(14, d*.34): 14 px on the cast rail at its .72 scale floor (16 px at scale 1), up to about 45 px on the KeyCeremony stage (132 px portrait at FontScale 1, larger at large text), all from a 256 px source. UiPackImporter.Apply turns mipmaps off for every UI sprite (Editor/UiPackImporter.cs:~77, mipmapEnabled = !ui), so expect aliasing at 14-16 px unless these four get a smaller Max Size. For badge_selected, CastSelect's upper-right card corner is already taken by the 22 px "Category mark" (CastSelect.cs:636-649).

- `badge_selected.png` - No RoleMark exists for it; it is blue (#1C8CFF fill, #00BFFF edge). It goes on CastSelect's picked card or on RelationshipWeb's selected node (Halo, RelationshipWeb.cs:473-477). It must supplement the stated 'PLAYING AS' text, never replace it.
- `badge_veto.png` - Same gold as badge_hoh. Only the glyph tells them apart, which is also true today (crown vs veto-token, both Tint Gold).

Files in `Assets/Gamesim/Resources/Packs/Pack1_Foundation/Badges/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `badge_hoh.png` | 256x256 | UiSprite |  |  |
| `badge_nominee.png` | 256x256 | UiSprite |  |  |
| `badge_selected.png` | 256x256 | UiSprite |  |  |
| `badge_veto.png` | 256x256 | UiSprite |  |  |

### Buttons

**Destination** (partly exists): primary: the MainMenu.Button primary branch (MainMenu.cs:179-201: AccentDeep fill, Accent border, Continue or New Season at :160-161), CompetitionResult "Continue from competition results" (CompetitionResult.cs:144-146), and CastSelect.Footer StartCaption (CastSelect.cs:830 via Chip(active)). secondary: MainMenu's non-primary buttons (:162-163), EpisodeHud.FixedButton (EpisodeHud.cs:1277; Notebook/Save/Settings :231-233, 'Go to diary room [R]' EpisodeHud.Chrome.cs:121/141), and CompetitionResult "Review competition score details" (:139-143, which is AccentDeep today like Continue). danger: 'Commit nominations' (EpisodeHud.ChoosePair :934), 'Vote to evict X' (EpisodeDirector.DiaryRoom.cs:533), 'Evict X' at the final eviction (EpisodeDirector.cs:1144). gold: the veto use 'Save X (HoH chooses replacement)' (EpisodeDirector.DiaryRoom.cs:498). success: EpisodeHud.DealAcceptCaption 'Accept this offer' (EpisodeDirector.Conversation.cs:364). Every modal row goes through EpisodeHud.Action/ActionFor (EpisodeHud.cs:879/894/941) and then Chrome(Interactive), which has no parameter for what the action means. That parameter has to be added before danger/gold/success can be assigned.

*Evidence:* MainMenu.cs:160-163,179-201; EpisodeHud.cs:231-233,879-941,1277-1290; EpisodeHud.Chrome.cs:121,141; Pressable EpisodeHud.Radial.cs:161 (highlight 1.35 at :169); CompetitionResult.cs:139-146; EpisodeDirector.DiaryRoom.cs:498,533; EpisodeDirector.Conversation.cs:364; EpisodeDirector.cs:1144; UiPackCatalogue.cs:150-154

*Caution:* Pressable's ColorTint (highlighted 1.35, EpisodeHud.Radial.cs:169) multiplies vertex colour, which clamps at 1, so on a baked sprite drawn with a white Image.color the button shows no hover. Hover actually comes from HudEmphasis.Promote (HudMotion.cs:52) recolouring the "Border" child, and a border baked into the sprite cannot be promoted. The primary/danger/gold/success sprites carry a 40 px baked glow margin (BodyInset, Editor/UiPackCatalogue.cs:150-154) and 81-83 px slice borders, while rows are only 57*FontScale tall (68 with a portrait). They need the rect outset by the inset plus a pixelsPerUnitMultiplier of about 2.3-3, or the button visibly shrinks. The Chrome_WearsNoAccentEdge test (EpisodePlayModeTests.Chrome.cs:37) only inspects "Border" children, so a cyan edge baked into a sprite would get past it silently. Captions must stay in the existing Text child (caption contract).

- `button_secondary_9slice.png` - No glow: 8 px pad, 33 px border, #0E203B fill with a #39587B edge. This is the only button that drops into FixedButton/MainMenu rects without outsetting.
- `button_success_9slice.png` - Border is 81 px on every side, not 83/82 like its glowing siblings (UiPackCatalogue.cs:154).

Files in `Assets/Gamesim/Resources/Packs/Pack1_Foundation/Buttons/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `button_danger_9slice.png` | 592x208 | UiSliced | 83, 82, 83, 82 | 40, 40, 40, 40 |
| `button_gold_9slice.png` | 592x208 | UiSliced | 83, 82, 83, 82 | 40, 40, 40, 40 |
| `button_primary_9slice.png` | 592x208 | UiSliced | 83, 82, 83, 82 | 40, 40, 40, 40 |
| `button_secondary_9slice.png` | 528x144 | UiSliced | 33, 33, 33, 33 | 8, 8, 8, 8 |
| `button_success_9slice.png` | 592x208 | UiSliced | 81, 81, 81, 81 | 40, 40, 40, 40 |

### Chips

**Destination** (exists): Consumers of HudPrimitives.Chip (HudPrimitives.cs:202/216). On CastSelect, the filled archetype pill (CastSelect.cs:665, CategoryTint :736) and the outlined trait pills (:709, TraitTint :754) map as: strategic to Strategist and strategic traits, social to Underdog and warm traits, romance to Socialite, danger to Wildcard and confrontational, orange to Competitor and competitive (the Joke amber), and the default Accent to cyan (see the chip_cyan note). Also HouseMap's Tally (HouseMap.cs:167), RelationshipWeb.Chip (RelationshipWeb.cs:997-1008; the HOH/VETO/NOM chip at :838 maps to chip_gold/chip_danger), and EpisodeHud.Tag (EpisodeHud.cs:839, an Accent .16 panel rather than a HudPrimitives.Chip). CastRail's role pill "Badge" (CastRail.cs:425) takes gold/danger. The pill helpers that do not go through HudPrimitives.Chip take neutral at rest and selected when active: CastSelect.Chip (CastSelect.cs:919: nav row, roster tabs, filters, Start), CharacterCreator.Chip (CharacterCreator.cs:568) and SeasonReport.Chip (SeasonReport.cs:666).

*Evidence:* HudPrimitives.cs:202-240; CastSelect.cs:665,709,736-770,919-965; CharacterCreator.cs:568-570; SeasonReport.cs:666-668; CastRail.cs:425-437,449-456; RelationshipWeb.cs:838,997-1008; HouseMap.cs:167; EpisodeHud.cs:839-869; UiPackCatalogue.cs:155-164

*Caution:* The pack chips are dark fills (e.g. strategic #3A1A67, selected #073C66, at alpha .93). The filled HudPrimitives.Chip draws its word in OnColor(tint)=Ink, which assumes a saturated ground, so the archetype word disappears on a pack chip. CastRail's Badge also writes Ink (:434). AFilledPillsWordIsReadable (UiThemeContrastTests.cs:150) measures the token, not the sprite, and would keep passing. The CastRail standing tag's Image.color is pinned to StandingGround(kind) (EpisodePlayModeTests.CastStanding.cs:95). CastChrome.cs:182-209 pins exactly one "Border" per card and zero Edge(Active) borders anywhere on the cast screen. The slice border is 35 px per side, and chips draw 15 px (CastRail Badge, 46x15*scale), 20-24 px (HudPrimitives.Chip callers, EpisodeHud.Tag) or 38 px (the CastSelect/Creator pills) tall. They need a pixelsPerUnitMultiplier of roughly 2 to 4.7, the same bulge problem HudPrimitives.cs:219-222 already works around.

- `chip_blueviolet_9slice.png` - No destination. UiTheme has no token that means blue-violet: AccentDeep is the room-outline blue, and Award (#7C3AED) is reserved for the competition banner. Leave it unused until someone assigns it a meaning.
- `chip_cyan_9slice.png` - This chip is teal (#15D6D0 edge on #0D4A4B), not the project's pale-blue Accent #99D9FF or Glow #5CC8FF, and no UiTheme token is teal. For the Accent default, EpisodeHud.Tag and HouseMap Tally, chip_selected (#00BFFF) is the closer hue. Using chip_cyan introduces a new colour.
- `chip_selected_9slice.png` - CastSelect active pill. Today that is a bright Glow fill plus AddGlow (CastSelect.cs:924-943). The pack version is a dark #073C66 fill, so the label colour has to flip from OnColor(Glow)=Ink to a light colour.
- `chip_gold_9slice.png` - CastRail Badge now draws Ink on a saturated standing colour (CastRail.cs:431-437). Tests read the word through Roles(); the colour itself is not pinned.

Files in `Assets/Gamesim/Resources/Packs/Pack1_Foundation/Chips/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `chip_blueviolet_9slice.png` | 272x88 | UiSliced | 35, 35, 35, 35 | 8, 8, 8, 8 |
| `chip_cyan_9slice.png` | 272x88 | UiSliced | 35, 35, 35, 35 | 8, 8, 8, 8 |
| `chip_danger_9slice.png` | 272x88 | UiSliced | 35, 35, 35, 35 | 8, 8, 8, 8 |
| `chip_gold_9slice.png` | 272x88 | UiSliced | 35, 35, 35, 35 | 8, 8, 8, 8 |
| `chip_neutral_9slice.png` | 272x88 | UiSliced | 35, 35, 35, 35 | 8, 8, 8, 8 |
| `chip_orange_9slice.png` | 272x88 | UiSliced | 35, 35, 35, 35 | 8, 8, 8, 8 |
| `chip_romance_9slice.png` | 272x88 | UiSliced | 35, 35, 35, 35 | 8, 8, 8, 8 |
| `chip_selected_9slice.png` | 272x88 | UiSliced | 35, 35, 35, 35 | 8, 8, 8, 8 |
| `chip_social_9slice.png` | 272x88 | UiSliced | 35, 35, 35, 35 | 8, 8, 8, 8 |
| `chip_strategic_9slice.png` | 272x88 | UiSliced | 35, 35, 35, 35 | 8, 8, 8, 8 |

### Dividers

**Destination** (partly exists): The only separators today are three procedural 1 px "Rule" fills: RelationshipWeb.Stats (RelationshipWeb.cs:846, Outline), RelationshipWeb.SectionHeading (RelationshipWeb.cs:898, rule :909, Hairline at .45) and the KeyCeremony card close (KeyCeremony.cs:261, Muted at .35, 132*scale wide). A grep of every named Fill finds no other screen that draws one. The guide's 'dividers over boxes' would mean adding new ones, for example inside the EpisodeHud right-column cards House vibe and Recent events (EpisodeHud.Chrome.cs:240/320).

*Evidence:* RelationshipWeb.cs:846-850,898-913; KeyCeremony.cs:261-263; no test references 'Rule'

*Caution:* divider_soft_blue is a 2 px line (rows 16-17 of 32, #2A4668 at alpha .71) with hard ends at x=64 and x=960, so it is not actually soft and loses 6% of its length at each end. On today's 1 px rects, with no mipmaps, it samples a single texture row: the line shows at partial alpha or disappears depending on pixel alignment. Give it a rect about 16 px tall, or crop the sprite to its two lit rows. Its colour is close to Outline #3A5068, so there is no palette clash.

Files in `Assets/Gamesim/Resources/Packs/Pack1_Foundation/Dividers/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `divider_soft_blue.png` | 1024x32 | UiSprite |  |  |

### Glows

**Destination** (partly exists): glow_cyan: RelationshipWeb's selected-node "Halo" (RelationshipWeb.cs:476, now a Disc at Glow .35), CastSelect's picked-card glow (CastSelect.cs:531) and active-pill glow (:938). glow_gold: the CeremonyTakeover "Takeover mark" (CeremonyTakeover.cs:290, tinted per kind in Play() :166/:175-184 via Tint :149) for Veto/Winner/VetoSelection, the CompetitionResult winner portrait (CompetitionResult.cs:109) and the SeasonReport winner finalist (SeasonReport.cs:245/263). glow_red: the same Takeover mark for Nomination/Eviction, VoteReveal nominee portraits (VoteReveal.cs:259) and KeyCeremony nominees (KeyCeremony.cs:317). glow_purple: the Diary Room does have a surface. RenderDiary (EpisodeDirector.DiaryRoom.cs:249-251) docks the shared "Episode panel" right at 600 wide through EpisodeHud.SetActivityLayout(Diary) (EpisodeHud.Activities.cs:62). Other candidate: the strategic petal (EpisodeDirector.cs:1032, built by EpisodeHud.Petal, EpisodeHud.Radial.cs:99).

*Evidence:* UiTheme.AddGlow UiTheme.cs:147-162; RelationshipWeb.cs:473-477; CastSelect.cs:531,938; CeremonyTakeover.cs:149-158,166-187,290; EpisodeDirector.DiaryRoom.cs:251; EpisodeDirector.HouseActivities.cs:42; EpisodeHud.Activities.cs:62-74; RelationshipWeb test EpisodePlayModeTests.RelationshipWeb.cs:224 pins Halo by name and parent only

*Caution:* Do not route pack glows through UiTheme.AddGlow: UiThemeGlassPlayModeTests.cs:61 pins its sprite name "UiTheme Glow 14" and child order [Glow, Border] (:55/:85). CeremonyGlassPlayModeTests.AssertGlass (:143-165) pins the ceremony Glow child's offsets at ±GlowWidth=10. CastChrome.cs:183/199 requires a child named "Glow" on the picked card and none on a resting card, so a pack glow there must take that name. The pack glows are 512 px soft rounded squares (peak alpha .49), meant to be drawn whole rather than 9-sliced as halos, so stretched across a 300x168 card they become ellipses. They fit round or square subjects best: portraits, the Halo, the ceremony mark. House Activities also uses ActivityLayout.Diary (EpisodeDirector.HouseActivities.cs:42), so key the purple glow off RenderDiary, not the layout.

- `glow_purple.png` - Goes behind the Episode panel while RenderDiary is showing (EpisodeDirector.DiaryRoom.cs:249-251). Pack 4 DiaryRoom may supersede it. UiTheme.Strategic is #A56BFF; the pack's purple is #8A5CF6.

Files in `Assets/Gamesim/Resources/Packs/Pack1_Foundation/Glows/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `glow_cyan.png` | 512x512 | UiSprite |  |  |
| `glow_gold.png` | 512x512 | UiSprite |  |  |
| `glow_purple.png` | 512x512 | UiSprite |  |  |
| `glow_red.png` | 512x512 | UiSprite |  |  |

### Icons_PNG

**Destination** (partly exists): UiTheme.Icon (UiTheme.cs:262) only loads Resources/GamesimIcons/<name>, which IconForge.Generate writes (Editor/IconForge.cs:27/31, white 128 px glyphs). Using pack icons needs a second lookup root or a rename. crown: HudPrimitives.IconFor HoH (HudPrimitives.cs:417), HouseMap.RoomIcon HoH (HouseMap.cs:38), RelationshipWeb legend (:593). trophy: CeremonyTakeover.IconFor Winner (CeremonyTakeover.cs:143), CastSelect.CategoryIcon competitor (CastSelect.cs:779). star: CastSelect wildcard (:781), HouseMap yard (:45). IconRail marks are drawn from discs and bars (IconRail.cs:128 Draw; entries EpisodeHud.cs:245-250). grid is a 2x2 of outlined squares, which is exactly Mark.Rooms 'Who is where' (the four-room floor plan). home fits Mark.Overview (a viewfinder with the house at its centre) or HouseMap's fallback 'house' (HouseMap.cs:46). user fits Mark.People, though it shows one head where the mark draws two. save: a decorative child glyph on the EpisodeHud 'Save [F5]' FixedButton (EpisodeHud.cs:232). random: the CharacterCreator.Studio 'Randomize unlocked' chip (CharacterCreator.Studio.cs:168).

*Evidence:* UiTheme.cs:262-269; IconForge.cs:27,31-46,473-474 (generated set includes crown, trophy, star, house, houseguest, people, settings); HudPrimitives.cs:69-86,412-421; IconRail.cs:128-180; HouseMap.cs:35-46; CastSelect.cs:774-785; CeremonyTakeover.cs:136-146; RelationshipWeb.cs:591-595

*Caution:* crown, star and trophy duplicate glyphs IconForge already generates, and home overlaps 'house', all in a different stroke style (the pack is 1.8/24 round-cap outlines). The guide asks for one consistent stroke weight, so pick one set rather than mixing. The pack icons are #DCE8F5 rather than white, so every tint applied through HudPrimitives.Glyph (HudPrimitives.cs:82), AddRoleMark (:375) or IconRail's Accent/Ink ink lands 4-14% darker than its token. The IconRail test IconRail_JumpsToEverySectionOfTheNotebook (EpisodePlayModeTests.MemoryWall.cs:124) only reads ActiveMarkName (:166) and captions, so replacing the drawn marks is safe. The save glyph must be decoration only, never appended to the caption.

- `grid.png` - IconRail Mark.Rooms (four squares, the same shape IconRail.cs Draw already makes). The generated set has no equivalent.
- `home.png` - IconRail Mark.Overview or the HouseMap fallback (HouseMap.cs:46). Overlaps IconForge 'house'.
- `random.png` - Used only at CharacterCreator.Studio.cs:168. The generated set has no equivalent.
- `save.png` - EpisodeHud 'Save [F5]' (EpisodeHud.cs:232) and MainMenu 'Settings and saves' (MainMenu.cs:34/162). The generated set has no equivalent.
- `user.png` - A stroke icon, so it cannot stand in for the filled 'houseguest' silhouette (HudPrimitives.cs:313, CastSelect.cs:606). Use it for IconRail People; IconForge's 'people' glyph also exists and is unused by the rail.

Files in `Assets/Gamesim/Resources/Packs/Pack1_Foundation/Icons_PNG/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `crown.png` | 128x128 | UiSprite |  |  |
| `grid.png` | 128x128 | UiSprite |  |  |
| `home.png` | 128x128 | UiSprite |  |  |
| `random.png` | 128x128 | UiSprite |  |  |
| `save.png` | 128x128 | UiSprite |  |  |
| `star.png` | 128x128 | UiSprite |  |  |
| `trophy.png` | 128x128 | UiSprite |  |  |
| `user.png` | 128x128 | UiSprite |  |  |

### Icons_SVG

**Destination** (does not exist yet): No runtime or editor consumer: nothing in Assets/Gamesim references .svg, and no .svg exists under Assets. The import tool keeps them as source only, in ArtSource/ui-packs/Pack1_Foundation/Icons_SVG/ outside Assets (ArtSource/ui-packs/tools/bb_ui_packs.py:8), and UiPackCatalogue has no SVG entries. Their use is as master shapes if the PNGs need re-rendering at another size, or as a style reference for an IconForge pass.

*Evidence:* ArtSource/ui-packs/Pack1_Foundation/Icons_SVG/*.svg (8 files, 24-unit viewBox, stroke 1.8); Editor/UiPackCatalogue.cs has no .svg; Packages/manifest.json:49 com.unity.modules.vectorgraphics; ProjectVersion 6000.6.0f1

*Caution:* Keep them out of Assets/. The manifest includes com.unity.modules.vectorgraphics, so a copy under Resources would be imported and shipped unused. The stroke colour is hard-coded to #DCE8F5.

Vector sources, kept in `ArtSource/ui-packs/Pack1_Foundation/Icons_SVG/` - the project has no vector importer; the PNGs are the runtime copies.

### Panels

**Destination** (exists): panel_resting: EpisodeHud.Chrome() at its default Resting level (EpisodeHud.cs:1402). There are 14 resting call sites: Navigation, Status, Interaction prompt and Episode panel (EpisodeHud.cs:230/266/280/296), Voter (:548), Live feed (:593), Overview (:629), Follow chip (:1113), Brand, Objective, House pill, House vibe and Recent events (EpisodeHud.Chrome.cs:78/106/151/240/320), and Exploration controls (EpisodeHud.Activities.cs:123). panel_interactive: Chrome(Emphasis.Interactive), which has 3 call sites: modal rows EpisodeHud.Action (:881/:897) and dial petals (EpisodeHud.Radial.cs:106). panel_selected: the HudEmphasis promoted state (HudMotion.cs:43-75), CastSelect's chosen card (CastSelect.cs:529), and the other Edge(Active) holders, the IconRail active cell (IconRail.cs:86-94) and the CastRail followed chip (CastRail.cs:338-342). panel_danger: VoteReveal CardGlass (VoteReveal.cs:332-342), KeyCeremony CardGlass (KeyCeremony.cs:347-357), and the CeremonyTakeover "Card glass" for Nomination/Eviction (built once at CeremonyTakeover.cs:278-281, so the per-kind swap belongs in Play() :166). panel_gold_power: the Takeover ground for Veto/Winner, and CompetitionResult "Card glass" (CompetitionResult.cs:103-105). UiTheme.Glass (UiTheme.cs:124) is not resting: it adds a cyan Hairline border and a Glow halo. Its persistent users, HouseMap "Room card" (HouseMap.cs:109) and RoomLabels "Chip" (RoomLabels.cs:66), are further panel_resting candidates.

*Evidence:* UiTheme.cs:94-116 (Emphasis/Edge),124-132 (Glass = AddGlow + AddBorder(Hairline)),213-241,375-418; EpisodeHud.cs:548,1376-1409; HudMotion.cs:43-75; IconRail.cs:86-94; CastRail.cs:338-342; CeremonySting.cs:184; CeremonyTakeover.cs:166,278-281; CompetitionResult.cs:103-105; KeyCeremony.cs:347-357; VoteReveal.cs:332-342; HouseMap.cs:109; RoomLabels.cs:66; UiPackCatalogue.cs:178-182

*Caution:* This category collides with the tests the most. UiThemeGlassPlayModeTests.cs:52-67 pins the sprite names "UiTheme Fill/Glow/Outline 14" and child order [Glow, Border] on whatever UiTheme.Glass produces. CeremonyGlassPlayModeTests.AssertGlass (:143-165) pins the ceremony ground's Image.color == GlassFill (sting, takeover, reveal, keys, competition result) and requires Border and Glow children. EpisodePlayModeTests.Chrome.cs:37-58 fails if any active "Border" wears Edge(Active) at rest, and also fails if no "Border" wears the resting colour, so dropping the Border children for baked-edge sprites breaks it. HudEmphasis.Promote animates panel.Find("Border"). CastChrome.cs:178 pins a resting cast card's colour to CardFill. The pack panels bake both the fill (#0B1930 at .93, where GlassFill is #0B1220 at .85, or .94 in Chrome) and the edge (#2A4668) into the texture, so Image.color would have to go white and the Border child would draw a second edge. selected, danger and gold carry a 40 px glow margin with 85-86 px borders. The safer route is to retune UiTheme's generator (Build :375, Edge :108) to the pack's look, and use the sprites only on screens that do not exist yet. danger and power are not Emphasis levels today.

- `panel_gold_power_9slice.png` - Baked 40 px outer glow. The rect needs outsetting by BodyInset (UiPackCatalogue.cs:179).
- `panel_selected_9slice.png` - Its baked cyan edge is exactly what the no-lit-edge-at-rest test cannot see. Only one panel should use it at a time.

Files in `Assets/Gamesim/Resources/Packs/Pack1_Foundation/Panels/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `panel_danger_9slice.png` | 592x336 | UiSliced | 86, 85, 86, 85 | 40, 40, 40, 40 |
| `panel_gold_power_9slice.png` | 592x336 | UiSliced | 86, 85, 86, 85 | 40, 40, 40, 40 |
| `panel_interactive_9slice.png` | 528x272 | UiSliced | 37, 37, 37, 37 | 8, 8, 8, 8 |
| `panel_resting_9slice.png` | 528x272 | UiSliced | 37, 37, 37, 37 | 8, 8, 8, 8 |
| `panel_selected_9slice.png` | 592x336 | UiSliced | 86, 85, 86, 85 | 40, 40, 40, 40 |

### Rings

**Destination** (exists): Four places draw a ring. HudPrimitives.Portrait builds a "Ring" disc (HudPrimitives.cs:278-282), and CastRail's rim (CastRail.cs:349), CastSelect.Card's rim (CastSelect.cs:560) and the CeremonyTakeover slot rim (CeremonyTakeover.cs:414) each draw their own. selection_ring_cyan: the CastSelect chosen rim (today UiTheme.Glow) and the RelationshipWeb selected node (the Halo at RelationshipWeb.cs:473-477). The RelationshipWeb player node's Accent ring (:443) marks identity ('YOU'), not selection, so it should not take the cyan selection ring. portrait_ring_gold: CastRail rims for HOH/VETO/WINNER (CastRail.cs:279-283) and the CompetitionResult winner (CompetitionResult.cs:109). power_ring_gold_glow: the SeasonReport winner finalist (SeasonReport.cs:245/263), the CompetitionResult winner, and the CeremonyTakeover slots for gold kinds. The pack has no red ring for nominees (VoteReveal.cs:259, KeyCeremony.cs:317) and no ring per relationship kind (RelationshipWeb.RingColour :267), so those stay procedural.

*Evidence:* HudPrimitives.cs:278-331; CastRail.cs:277-285,347-357; CastSelect.cs:556-564; CeremonyTakeover.cs:414; RelationshipWeb.cs:267,443,473-482; CompetitionResult.cs:109; SeasonReport.cs:245,263; UiTheme.cs:62-70 (Brass)

*Caution:* The pack rings are hollow: outer diameter 88% of the image, a stroke about 4% wide (20 of 512 px), a hole of 80%. selection_ring_cyan and power_ring_gold_glow also carry a soft halo out to 98%. The project's "Ring" is instead a filled disc of diameter+2*ringWidth behind a masked Frame, so a pack ring has to be a top overlay about 1.25x the portrait. On CastRail that overlay overhangs the ring foot, where the Badge and standing tag sit (BadgeLift 11), and CastRail_ARoleAndAStandingShareTheRingFootInsideTheBorderAtEveryScale (EpisodePlayModeTests.CastStanding.cs:114) measures that layout. The guide's houseguest recipe puts a gold ring on every roster face, but UiTheme.Brass (UiTheme.cs:62-70) and GoldIsNotNearlyTheColourOfAnythingThatIsNotPower (UiThemeContrastTests.cs:58) deliberately keep the resting CastSelect rings off Gold. Use portrait_ring_gold only for people who hold power, never for the roster.

- `portrait_ring_gold.png` - Power holders only (HOH/VETO/WINNER). Keep Brass for resting roster rings.

Files in `Assets/Gamesim/Resources/Packs/Pack1_Foundation/Rings/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `portrait_ring_gold.png` | 512x512 | UiSprite |  |  |
| `power_ring_gold_glow.png` | 512x512 | UiSprite |  |  |
| `selection_ring_cyan.png` | 512x512 | UiSprite |  |  |

## Pack 2 - Pack2_Gameplay

Core gameplay UI: HUD, cast states, conversation, live feed, meters, relationship web, notebook, ceremonies, minimap, creator.

### Ceremony

**Destination** (partly exists): The ceremony overlays, each on its own ScreenSpaceOverlay root (Attach, CeremonySting.cs:79 / CeremonyTakeover.cs:72). Targets: the CeremonySting 'Card' strip (Runtime/Presentation/CeremonySting.cs:176-184); CeremonyTakeover 'Card glass' (:278) and the per-subject slots in Faces() (:389-471, slotRect :405); KeyCeremony.Stage (Runtime/Presentation/KeyCeremony.cs:271, the SAFE houseguest) and KeyCeremony.StageBlock (:304-338, the two nominees); the VoteReveal nominee columns (Runtime/Presentation/VoteReveal.cs:253-280). The player's own ceremony decisions are drawn in the docked 'Episode panel' by EpisodeDirector.RenderPlayerDecision (Runtime/Episode/EpisodeDirector.DiaryRoom.cs:446-540), which is called from EpisodeDirector.cs:1140 and, in the diary room, from DiaryRoom.cs:280.

*Evidence:* CeremonyTakeover.cs:278 'Card glass'; CeremonySting.cs:176 'Card'; KeyCeremony.cs:271 Stage(), called only with 'SAFE' (:176) or null (:170); StageBlock :304 with 'Nominated' chip :328; KeyCeremony 'Card glass' :349; VoteReveal.cs:255-280 loop, 'Card glass' :334; DiaryRoom.cs:449-477 (nominate, via hud.ChooseNominationPair :469), :479-506 (veto), :517-538 (vote, ActionFor :533), :542-550 VetoReplacements; EpisodeDirector.Ceremony.cs:29-63 CeremonySubjects, :179 ReactToCeremony

*Caution:* CeremonyGlassPlayModeTests.AssertGlass (Tests/PlayMode/CeremonyGlassPlayModeTests.cs:143-166) runs on the Sting 'Card' (:52) and on the 'Card glass' of Takeover (:64), VoteReveal (:83), KeyCeremony (:105) and CompetitionResult (:128). It pins each ground's Image.color == UiTheme.GlassFill and requires 'Border' and 'Glow' children, with Glow offset = GlowWidth (10). Pack sprites have their colour baked in and need Image.color = white, so they go on a new child, never on the card's own Image. AssertNothingTakesAClick (:172-179) covers Sting, Takeover, VoteReveal and KeyCeremony, so every Image added there needs raycastTarget = false. The KeyCeremony test counts children whose names start with 'Key ' (:108-113). Never swap sprites inside UiTheme.Glass/Style: UiThemeGlassPlayModeTests.cs:52/61/67 pin 'UiTheme Fill/Glow/Outline 14'. ceremony_banner and replacement_nominee_frame have a 40 px baked glow margin (slice borders 80-81).

- `ceremony_banner_9slice.png` - The CeremonySting 'Card', the broadcast strip across the top (CeremonySting.cs:176; insets :54-59). The card is only about 90 px tall (14 + headline 34 + 6 + detail 19 + 14), and the banner's top and bottom borders total 160 px, so it needs pixelsPerUnitMultiplier of about 2. It could also go on CeremonyTakeover 'Card glass' (:278). With the 40 px baked glow, the visible banner sits 40 px inside its rect.
- `nominee_slot_9slice.png` - KeyCeremony.StageBlock's per-nominee slot (250 wide, KeyCeremony.cs:304-338). Stage's `badge == "NOMINATED"` branch (:285) is never reached. Also the CeremonyTakeover subject slotRect (:405, 150x116 at scale 1) for NominationKind. In the docked panel it would go on the ChoosePair portrait rows (EpisodeHud.cs:912-935, reached from ChooseNominationPair in EpisodeHud.Decisions.cs:38), which are Action(caption,portrait) Chrome rows (:894).
- `eviction_candidate_frame_9slice.png` - VoteReveal's two nominee columns (VoteReveal.cs:253-280; slot 300 wide at x = ±150). No per-nominee container exists: rim, 'Nominee', 'Votes' and 'Votes caption' are loose children of the column, so the frame has to be a new sibling behind them. Also the 'Vote to evict X' ActionFor rows (DiaryRoom.cs:533).
- `replacement_nominee_frame_9slice.png` - The VetoReplacements rows (DiaryRoom.cs:542-550). No ceremony card shows who the replacement is: CeremonySubjects badges every VetoKind nominee 'NOMINATED' (Ceremony.cs:46-49). ReactToCeremony already works out the replacement (:191-196) but only uses it to turn heads. To use this frame on CeremonyTakeover, add a 'REPLACEMENT' badge. 40 px glow margin.
- `veto_decision_panel_9slice.png` - There is no dedicated veto decision screen. CeremonyTakeover only shows the veto outcome (VetoKind, IconFor 'gavel' :141). The target is the docked 'Episode panel' (EpisodeHud.cs:296), only while the veto branch of RenderPlayerDecision is running (DiaryRoom.cs:479-506).

Files in `Assets/Gamesim/Resources/Packs/Pack2_Gameplay/Ceremony/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `ceremony_banner_9slice.png` | 1000x210 | UiSliced | 81, 80, 81, 80 | 40, 40, 40, 40 |
| `eviction_candidate_frame_9slice.png` | 436x516 | UiSliced | 32, 32, 32, 32 | 8, 8, 8, 8 |
| `nominee_slot_9slice.png` | 396x556 | UiSliced | 32, 32, 32, 32 | 8, 8, 8, 8 |
| `replacement_nominee_frame_9slice.png` | 560x340 | UiSliced | 81, 80, 81, 80 | 40, 40, 40, 40 |
| `veto_decision_panel_9slice.png` | 876x436 | UiSliced | 32, 32, 32, 32 | 8, 8, 8, 8 |

### CharacterCreator

**Destination** (exists): The CharacterCreator studio (Runtime/Presentation/CharacterCreator.Studio.cs): the setup steps in BuildNavigation (:58-71), the category row (:156-163), ControlSlider (:306-340), WardrobeSelector style cards (:373-401) and ColorSelector (:437-457). Every pill goes through the shared CharacterCreator.Chip (Runtime/Presentation/CharacterCreator.cs:568-593).

*Evidence:* Studio.cs:65-70 pages Appearance/Identity/Personality/My Houseguests/Review; :156 categories Body/Face/Hair/Clothing/Colors; :313 '<label> slider' track (24 tall), :316 'Handle'; :379 'Wear <item>' cards 198x128; :454 swatch colour; CharacterCreator.cs:570 Chip fill AccentDeep/SurfaceRaised, 36 tall (:574), Border :576

*Caution:* Chip paints Image.color AccentDeep/SurfaceRaised and adds a Border child, and every creator pill uses it, so add a variant parameter instead of changing Chip. The pack sprites have baked colour (the step_active centre is about #073C66), so Image.color must be white. Chips are 36 px tall, and every pill sprite's top and bottom borders are taller than that: step_inactive 26+26, category_tile 30+30, step_active 72+72 (also 40 px glow). So each needs a pixelsPerUnitMultiplier of about 1.5-4, or an outset child. The slider track is 24 px against 48 px of track borders. Tests find controls by name or caption ('Previous <slot>', 'Next <slot>', 'Lock <category>', 'Next starting look', 'Next profiles'), so keep those names.

- `creator_step_active_9slice.png / creator_step_inactive_9slice.png` - The Chip called from BuildNavigation (Studio.cs:69), with active = (studioPage == page).
- `creator_category_tile_9slice.png` - The Appearance category chips Body/Face/Hair/Clothing/Colors (Studio.cs:156-163), 120x36.
- `creator_slider_track_9slice.png / creator_slider_fill_9slice.png / creator_slider_handle.png` - ControlSlider: the track (Studio.cs:313) and 'Handle' (:316, 22x26). slider.fillRect is never assigned today, so the fill needs a new Fill Area/Fill child wired to slider.fillRect.
- `creator_thumbnail_frame_9slice.png / creator_thumbnail_selected_9slice.png` - The WardrobeSelector 'Wear <label>' cards (Studio.cs:379-401). Today 'selected' = AccentDeep fill when item.Matches(selected). The profile thumbnail (CharacterProfileBrowser.Thumbnail, CharacterProfileBrowser.cs:72-78, called at Studio.cs:502) is a 38 px circular portrait, and a square 9-slice frame does not fit it.
- `creator_swatches_panel_9slice.png` - Only as a ground behind the ColorSelector row (Studio.cs:440 StudioRow). The swatch buttons cannot take a pack sprite, because ColorSelector writes the swatch colour into their Image.color (:454).

Files in `Assets/Gamesim/Resources/Packs/Pack2_Gameplay/CharacterCreator/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `creator_category_tile_9slice.png` | 276x136 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `creator_slider_fill_9slice.png` | 416x52 | UiSliced | 24, 24, 24, 24 | 8, 8, 8, 8 |
| `creator_slider_handle.png` | 128x128 | UiSprite |  |  |
| `creator_slider_track_9slice.png` | 576x52 | UiSliced | 24, 24, 24, 24 | 8, 8, 8, 8 |
| `creator_step_active_9slice.png` | 320x152 | UiSliced | 77, 72, 77, 72 | 40, 40, 40, 40 |
| `creator_step_inactive_9slice.png` | 256x88 | UiSliced | 26, 26, 26, 26 | 8, 8, 8, 8 |
| `creator_swatches_panel_9slice.png` | 656x236 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `creator_thumbnail_frame_9slice.png` | 196x196 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `creator_thumbnail_selected_9slice.png` | 260x260 | UiSliced | 80, 79, 80, 79 | 40, 40, 40, 40 |

### CharacterStates

**Destination** (partly exists): Houseguest cards: the CastRail.Entry 'Chip' in the bottom strip (Runtime/Presentation/CastRail.cs:306-359; chip :329, edge :338-342) and CastSelect.Card on the choose-your-houseguest screen (Runtime/Presentation/CastSelect.cs:510-538). Corner overlays go beside the HudPrimitives.AddRoleMark call sites (Runtime/Presentation/HudPrimitives.cs:351): CastRail.cs:357, EpisodeHud.Annotate (Runtime/Episode/EpisodeHud.cs:982) and RelationshipWeb.Node (Runtime/Presentation/RelationshipWeb.cs:482). There are also nominee-only sites at KeyCeremony.cs:286/:323, VoteReveal.cs:262 and RelationshipWeb.cs:812.

*Evidence:* CastRail.Read WINNER/FINAL 2/OUT/HOH/VETO/NOM/YOU (CastRail.cs:277-287); followed chip gets Edge(Active) (:338-342); chip is (88-4)-2x2 = 80 wide and 96-6 = 90 tall; CastSelect chosen card AddGlow + Glow border (:529-533), CardHeight 168 (:61); RelationshipWeb.Kind enum (RelationshipWeb.cs:67)

*Caution:* EpisodePlayModeTests.CastChrome.cs:178 and :228 pin the CastSelect card Image.color == UiTheme.CardFill. :182 and :201 require exactly one 'Border' child. :183 and :199 allow a 'Glow' child (CastSelect.CardGlowName, :93) only on the picked card, and :204-208 forbid any Border at Edge(Active) on that screen. CastStanding.cs:95 pins the standing-tag ground. So pack cards have to be a child layer. The selected/hoh/nominee sprites are 600x300 with a 40 px glow margin and 79-80 px borders, while resting/hover are 536x236 with an 8 px margin and 30 px borders, so swapping them in place shrinks the visible card. The 80x90 rail chips are smaller than the glow variants' 160x158 of borders. Chrome_WearsNoAccentEdgeWhileNothingIsAskingToBeActedOn (EpisodePlayModeTests.Chrome.cs:37-60) asserts that no active 'Border' carries Edge(Active) while panels are closed, and that at least one Border is at Edge(Resting). It does not allow 'one lit card'. A lit pack sprite on persistent chrome breaks the same rule even though the test only reads Borders.

- `houseguest_card_resting_9slice.png` - The CastRail 'Chip' (CastRail.cs:329) and the CastSelect card ground (CastSelect.cs:514).
- `houseguest_card_hover_9slice.png` - There is no hover swap today: the buttons use colour tints. It needs a HudEmphasis-style component (Runtime/Presentation/HudMotion.cs:43-75) that swaps sprites instead of edge colours.
- `houseguest_card_selected_9slice.png / overlay_selected_corner.png` - The CastRail followed chip (CastRail.cs:338-342) and the chosen CastSelect card (CastSelect.cs:529-533).
- `houseguest_card_hoh_9slice.png / houseguest_card_nominee_9slice.png / overlay_hoh_corner.png / overlay_nominee_corner.png` - CastRail standing HOH/NOM (CastRail.cs:282, :284). The corners go beside the AddRoleMark badge sites. They are card-corner overlays, not the portrait-shoulder disc AddRoleMark draws (HudPrimitives.cs:351-375).
- `overlay_romance_corner.png` - Nothing uses this yet. There is no romance/showmance state anywhere in Simulation or Runtime, and RelationshipWeb.Kind = {Neutral, Friendship, Alliance, Rivalry, Distrust}. A romance relationship kind would have to be added first.
- `overlay_event_corner.png` - Nothing uses this yet. No per-houseguest 'has news' flag exists. One could be derived from the audience-filtered state.events (EpisodeHud.Chrome.cs:312-316).

Files in `Assets/Gamesim/Resources/Packs/Pack2_Gameplay/CharacterStates/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `houseguest_card_hoh_9slice.png` | 600x300 | UiSliced | 80, 79, 80, 79 | 40, 40, 40, 40 |
| `houseguest_card_hover_9slice.png` | 536x236 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `houseguest_card_nominee_9slice.png` | 600x300 | UiSliced | 80, 79, 80, 79 | 40, 40, 40, 40 |
| `houseguest_card_resting_9slice.png` | 536x236 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `houseguest_card_selected_9slice.png` | 600x300 | UiSliced | 80, 79, 80, 79 | 40, 40, 40, 40 |
| `overlay_event_corner.png` | 196x196 | UiSprite |  |  |
| `overlay_hoh_corner.png` | 196x196 | UiSprite |  |  |
| `overlay_nominee_corner.png` | 196x196 | UiSprite |  |  |
| `overlay_romance_corner.png` | 196x196 | UiSprite |  |  |
| `overlay_selected_corner.png` | 196x196 | UiSprite |  |  |

### Competition

**Destination** (partly exists): CompetitionGameScreen.Show (Runtime/Presentation/CompetitionGameScreen.cs:54-133) for Memory, Reaction and Endurance play, and CompetitionResult.Build (Runtime/Presentation/CompetitionResult.cs:91-150) for the standings. There is no quiz. CompetitionMiniGames.Kind has four kinds: {Precision, Endurance, Reaction, Memory} (CompetitionMiniGames.cs:86). Precision is the timing bar drawn in the docked panel by EpisodeHud.ChallengeMeter (Runtime/Episode/EpisodeHud.cs:1120-1128, called from EpisodeDirector.Challenge.cs:319).

*Evidence:* CompetitionGameScreen.cs:78 'Competition clock', :79 'Progress', :140 'Memory card n' (205x91), recoloured per state at :287, :99 'Reaction target' (130x92), :111-115 Endurance 'Grip track' 802x58 / 'Grip remaining', :122 'Countdown'; CompetitionResult.cs:121 'Standing n' rows (1116x30), :125-126 Track/Bar (8 px), :128 'Score', :110 'Winner'; scene objects 'Competition podium 1-3' (EpisodeHouse.unity; authored Editor/HousePrototypeSetup.cs:118, rebuilt by Editor/HouseSetPieces.cs:662-707)

*Caution:* CeremonyGlassPlayModeTests.cs:128-131 pins the CompetitionResult 'Card glass' to GlassFill and pins the two button names. CompetitionSurfacePlayModeTests find controls by name ('Memory card n', etc.). Memory cards get Image.color written per state (PositiveDeep matched / AccentDeep face-up / Ink, :287), so a sprite swap has to replace that line and set colour to white. Result rows are 30 px tall and tracks 8 px, while the pack's 28 px borders sum to 56 px, so they need pixelsPerUnitMultiplier of 2 or more. answer_tile_selected, competition_timer_frame and leader_marker have a 40 px glow margin (76-79 px borders).

- `answer_tile_9slice.png / answer_tile_selected_9slice.png` - The 16 'Memory card n' buttons (CompetitionGameScreen.cs:140). 'Selected' means the face-up or keyboard-focused card; there is no 'matched' variant. There is no A/B answer UI.
- `competition_timer_frame_9slice.png` - The 'Competition clock' label (CompetitionGameScreen.cs:78, 450x42), and possibly 'Countdown' (:122). The 40 px glow margin and 78 px borders exceed the 42 px label height.
- `competition_score_tile_9slice.png` - The 'Progress' label (CompetitionGameScreen.cs:79) and the result 'Score' cells (CompetitionResult.cs:128).
- `competition_podium_label_9slice.png` - The result 'Standing n' rows (CompetitionResult.cs:121). The world podiums 'Competition podium 1-3' have no labels, so a world-space label on them would be new.
- `leader_marker_9slice.png` - The winner row (entry.IsWinner, CompetitionResult.cs:121/126) and the 'Winner' line (:110).
- `elimination_badge_9slice.png` - Nothing uses this yet. No minigame has elimination or knock-out state; the standings only rank scores.
- `progress_lane_9slice.png` - The result 'Track' (CompetitionResult.cs:125), the Endurance 'Grip track' (CompetitionGameScreen.cs:111), and the docked-panel 'Precision meter' (EpisodeHud.cs:1120-1128).

Files in `Assets/Gamesim/Resources/Packs/Pack2_Gameplay/Competition/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `answer_tile_9slice.png` | 376x176 | UiSliced | 28, 28, 28, 28 | 8, 8, 8, 8 |
| `answer_tile_selected_9slice.png` | 440x240 | UiSliced | 79, 78, 79, 78 | 40, 40, 40, 40 |
| `competition_podium_label_9slice.png` | 376x106 | UiSliced | 28, 28, 28, 28 | 8, 8, 8, 8 |
| `competition_score_tile_9slice.png` | 236x196 | UiSliced | 28, 28, 28, 28 | 8, 8, 8, 8 |
| `competition_timer_frame_9slice.png` | 360x200 | UiSliced | 79, 78, 79, 78 | 40, 40, 40, 40 |
| `elimination_badge_9slice.png` | 276x106 | UiSliced | 28, 28, 28, 28 | 8, 8, 8, 8 |
| `leader_marker_9slice.png` | 320x166 | UiSliced | 79, 76, 79, 76 | 40, 40, 40, 40 |
| `progress_lane_9slice.png` | 736x86 | UiSliced | 28, 28, 28, 28 | 8, 8, 8, 8 |

### ConversationWheel

**Destination** (partly exists): The live dial is EpisodeHud.ConversationRadial (Runtime/Episode/EpisodeHud.Radial.cs:65-93), which is actually a 4-column GridLayoutGroup (:72-81). EpisodeHud.Petal (:99-127) fills it, seated from EpisodeDirector.cs:1028-1047. A true ring and hub exist only in HudPrimitives.Dial/Radial (Runtime/Presentation/HudPrimitives.cs:124-199, 'Dial hub' :193), which only Tests/EditMode/HudDialTests.cs uses.

*Evidence:* EpisodeDirector.cs:1029-1045 petals: SmallTalk chat/Accent, StrategicDiscussion bulb/Strategic, PersonalChat heart/Flirt, More journal/Muted, RelationshipBuilding handshake/Allied, ShareSecret gossip/Strategic, Talk 'Spend time together' star/Joke; Petal Chrome(Interactive) + HudEmphasis.Promote (EpisodeHud.Radial.cs:106-107)

*Caution:* The pack petals are 420x260 shapes meant to rotate around a hub. The live cells are about 200x112 grid rectangles holding full-sentence captions, and those captions are the test and screen-reader contract (PetalCaptions, EpisodePlayModeTests.Chrome.cs ~:64). Pressable's colour ramp multiplies by 1.35 (EpisodeHud.Radial.cs:169), which shifts the hue of a baked-colour petal; the pack guide says to animate scale/alpha instead. HudEmphasis.Promote finds the 'Border' child by name (HudMotion.cs:54), so keep it.

- `wheel_base_ring.png / wheel_center_hub.png` - The HudPrimitives.Radial root and 'Dial hub' (HudPrimitives.cs:178-199). This geometry is dormant in the live UI.
- `wheel_petal_resting.png / wheel_petal_selected.png` - Petal Chrome (EpisodeHud.Radial.cs:106). 'Selected' = hover/focus, which HudEmphasis currently shows as an edge colour change (HudMotion.cs:71-74).
- `wheel_petal_disabled.png` - No petal is ever non-interactable today. The dial is skipped outside Social/Campaign (EpisodeDirector.cs:1003-1004), and a spent budget only shows as the 'N left' line (:1000, ActionsLeft :448). Greying petals at zero actions would be new. (DiaryRoom.cs:310 is the diary room's Study-the-house block, not the dial.)
- `wheel_petal_positive.png / wheel_petal_strategy.png / wheel_petal_danger.png` - positive = RelationshipBuilding/Allied (EpisodeDirector.cs:1039). strategy = StrategicDiscussion and ShareSecret (:1032, :1042). danger has no petal: whisper, call-out, lie and scheme are ordinary Action rows below the grid (:1068-1086). SmallTalk (Accent), PersonalChat (Flirt) and Talk (Joke) have no matching pack colour.

Files in `Assets/Gamesim/Resources/Packs/Pack2_Gameplay/ConversationWheel/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `wheel_base_ring.png` | 384x384 | UiSprite |  |  |
| `wheel_center_hub.png` | 300x300 | UiSprite |  |  |
| `wheel_petal_danger.png` | 420x260 | UiSprite |  |  |
| `wheel_petal_disabled.png` | 420x260 | UiSprite |  |  |
| `wheel_petal_positive.png` | 420x260 | UiSprite |  |  |
| `wheel_petal_resting.png` | 420x260 | UiSprite |  |  |
| `wheel_petal_selected.png` | 420x260 | UiSprite |  |  |
| `wheel_petal_strategy.png` | 420x260 | UiSprite |  |  |

### Dialog

**Destination** (partly exists): The HouseConversationCaption bubble 'Witnessed generic topic' with 'Caption tail', built in Create() (Runtime/Episode/HouseConversationCaption.cs:150-195) on its own overlay canvas 'Observed house conversation' (sortingOrder 69). Also the EpisodeHud.NpcDialogue lines in the docked panel (Runtime/Episode/EpisodeHud.cs:452-473).

*Evidence:* HouseConversationCaption.cs:27-29 PanelName/TailName, :166 panel Style(Ink, PanelRadius), :171-182 rotated-square tail, bubble 430x62 / banner 650x70 (:31-32); EpisodeHud.cs:462/465/472 'NPC spoken dialogue'/'NPC follow-up dialogue' FlowText; Runtime/House/HouseNpc.cs:13, :43-84 TextMesh name tag

*Caution:* Tests find the bubble by PanelName and TailName (EpisodePlayModeTests.MemoryWall.cs:203, :261-264, :291, :323), and the ambient-caption test drives banner mode, so keep both. The pack bubble has no tail; keep the existing tail child, which is tinted Ink (:180) and will not match the bubble's baked colour. The panel Image is tinted Ink; with a pack sprite it needs to be white.

- `speech_bubble_9slice.png` - The HouseConversationCaption panel (HouseConversationCaption.cs:159-166). Its 32+32 borders are slightly taller than the 62 px bubble.
- `secret_whisper_bubble_9slice.png` - The NPC reply after ShareSecret or a whisper (EpisodeHud.NpcDialogue :452-473). NpcDialogue writes the same bare 'NPC spoken dialogue' FlowText whatever the action was, so this needs a card wrapper plus a check on acceptedAction. WhisperCaption (:87) is only the action-row caption.
- `npc_nameplate_9slice.png` - In-world names are 3D TextMesh (HouseNpc.cs:43-84, authored in Editor/HousePrototypeSetup.cs:186 'Maya name'), which cannot take a uGUI sprite; it needs a world-space canvas or a SpriteRenderer backer. In the UI it fits RelationshipWeb.NameChip (RelationshipWeb.cs:525) and FollowChip (EpisodeHud.cs:1110-1118).
- `unavailable_action_callout_9slice.png` - The 'No social actions remain…' paragraph in the diary room's Study-the-house block (EpisodeDirector.DiaryRoom.cs:310), and the non-interactable rows: the house activity slots (EpisodeDirector.HouseActivities.cs:60), the diary visit row (EpisodeDirector.cs:1100) and the diary travel button (EpisodeHud.Chrome.cs:121/141).

Files in `Assets/Gamesim/Resources/Packs/Pack2_Gameplay/Dialog/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `npc_nameplate_9slice.png` | 436x136 | UiSliced | 28, 28, 28, 28 | 8, 8, 8, 8 |
| `secret_whisper_bubble_9slice.png` | 636x216 | UiSliced | 32, 32, 32, 32 | 8, 8, 8, 8 |
| `speech_bubble_9slice.png` | 636x216 | UiSliced | 32, 32, 32, 32 | 8, 8, 8, 8 |
| `unavailable_action_callout_9slice.png` | 476x116 | UiSliced | 28, 28, 28, 28 | 8, 8, 8, 8 |

### HUD

**Destination** (partly exists): EpisodeHud.Begin (Runtime/Episode/EpisodeHud.cs:185-336) and EpisodeHud.Chrome.cs. There is no single top bar or right-rail container. The top band is three separate Chrome() cards: 'Brand' (BrandCard, Chrome.cs:76-78, inside the 'Left column' VerticalLayoutGroup at EpisodeHud.cs:218), 'House pill' (HousePill, Chrome.cs:149-151) and 'Navigation' (EpisodeHud.cs:230). The right column is three free-standing cards (RightColumn, Chrome.cs:290-298).

*Evidence:* EpisodeHud.Chrome(name) = GlassFill .94 + AddBorder(Edge(emphasis)) (EpisodeHud.cs:1402-1409); IconRail root 'Icon rail' (Runtime/Presentation/IconRail.cs:69, Width 52 :35, no Image); CastRail root 'Cast rail' (Runtime/Presentation/CastRail.cs:155, no Image); 'Objective' (ObjectiveCard, EpisodeHud.Chrome.cs:104-106); PillWidth 340 / TopBarHeight 64 (Chrome.cs:47/:29)

*Caution:* EpisodePlayModeTests.Accessibility.cs:112-134 asserts that Brand, Navigation, Objective, Exploration controls, Status, House pill, Live feed, House vibe, Recent events, Cast rail and Icon rail never overlap at either text size. Display.cs:28-31 checks by name which of them the compact HUD keeps or drops. A new rail container must not take those names, and it must be added to SetChromeVisible in SetActivityLayout (EpisodeHud.Activities.cs:87-98) and to WorldCaptionSafeBounds (:28-50). Chrome_WearsNoAccentEdgeWhileNothingIsAskingToBeActedOn (EpisodePlayModeTests.Chrome.cs:37-60) needs no Border at Edge(Active) and at least one at Edge(Resting), so keep AddBorder and put the pack sprite on a child or ground. A ground with a pack sprite needs Image.color = white instead of GlassFill.

- `top_status_bar_9slice.png` - There is no single bar. The best fit is the 'House pill' ground (340x64, EpisodeHud.Chrome.cs:151-162). A strip behind Brand, pill and Navigation would be a new unnamed sibling.
- `left_nav_rail_9slice.png` - The IconRail root 'Icon rail' (IconRail.cs:69), which has no Image today; add one with raycastTarget off. At 52 px the rail is narrower than two 33 px slice borders (66 px), so it needs pixelsPerUnitMultiplier of about 1.3.
- `right_info_rail_9slice.png` - A new container behind the RightColumn cards (EpisodeHud.Chrome.cs:290-298). Each card already has its own glass ground, so this doubles the chrome unless those grounds are dropped.
- `bottom_cast_strip_9slice.png` - The CastRail root 'Cast rail' (CastRail.cs:155), which has no Image today.
- `objective_card_active_9slice.png` - 'Objective' (EpisodeHud.Chrome.cs:106) is persistent, so a lit sprite there breaks the resting-chrome rule. Use it only while the objective is the thing to act on now, or on the docked 'Episode panel' (EpisodeHud.cs:296) / 'Interaction prompt' (:280). 40 px glow margin.

Files in `Assets/Gamesim/Resources/Packs/Pack2_Gameplay/HUD/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `bottom_cast_strip_9slice.png` | 1516x176 | UiSliced | 33, 33, 33, 33 | 8, 8, 8, 8 |
| `left_nav_rail_9slice.png` | 276x856 | UiSliced | 33, 33, 33, 33 | 8, 8, 8, 8 |
| `objective_card_active_9slice.png` | 600x330 | UiSliced | 80, 79, 80, 79 | 40, 40, 40, 40 |
| `right_info_rail_9slice.png` | 396x856 | UiSliced | 33, 33, 33, 33 | 8, 8, 8, 8 |
| `top_status_bar_9slice.png` | 1096x106 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |

### Icons_PNG

**Destination** (partly exists): UiTheme.Icon (Runtime/Presentation/UiTheme.cs:262-269) loads only Resources/GamesimIcons/<name>. HudPrimitives.Glyph draws those sprites and tints them through Image.color (Runtime/Presentation/HudPrimitives.cs:69-86). The set is generated by IconForge (Editor/IconForge.cs:27-52, glyph table :472-481). Pack icons at Resources/Packs/Pack2_Gameplay/Icons_PNG cannot be reached until UiTheme.Icon accepts a pack path.

*Evidence:* Resources/GamesimIcons holds 42 PNGs (bed, bulb, calendar, camera, chat, crown, … target, task, trophy, veto-token); IconRail marks are drawn from bars and discs (IconRail.cs:135-180); pack glyph colour sampled #DCE8F5

*Caution:* calendar, chat and target share basenames with IconForge output. Copying them into GamesimIcons means the next 'Gamesim ▸ U07 ▸ Generate HUD icons' overwrites them, so load them from the Packs path instead. The pack glyphs are #DCE8F5, not pure white, so every Image.color tint renders about 4-14% darker per channel than the tint colour. UiThemeContrastTests only check palette colours, not icons, so nothing would catch this. Every caller must still cope with null, since Icon() returns null when an icon is missing.

- `alliance.png` - Where 'handshake' is used now: HouseVibe 'Deals & alliances' (Runtime/Presentation/HouseVibe.cs:28), EventGlyph 'alliance' (EpisodeHud.Chrome.cs:365), the RelationshipBuilding petal (EpisodeDirector.cs:1039), and the RelationshipWeb Allies stat (RelationshipWeb.cs:857) and alliance rows (:742).
- `calendar.png` - The HousePill WEEK cell (EpisodeHud.Chrome.cs:155). Same name as an IconForge icon.
- `chat.png` - The SmallTalk petal (EpisodeDirector.cs:1029) and EventGlyph conversation (EpisodeHud.Chrome.cs:367). Same name as an IconForge icon.
- `close.png` - The 'Close [Esc]' button (EpisodeHud.cs:307), as a decoration child only; the caption stays as the button's identity.
- `diary.png` - The diary travel button (EpisodeHud.Chrome.cs:121/141) and the diary visit row (EpisodeDirector.cs:1100).
- `filter.png` - The RelationshipWeb 'Relationship filters' toolbar (RelationshipWeb.cs:309-330) and the SeasonReport filter chips (Runtime/Presentation/SeasonReport.cs:595-607).
- `flirt.png` - The PersonalChat petal, which uses 'heart' today (EpisodeDirector.cs:1035).
- `lock.png` - The creator 'Lock/Unlock <category>' chip (CharacterCreator.Studio.cs:165) and the final-four veto lock line (EpisodeDirector.DiaryRoom.cs:486-490).
- `search.png` - The creator's saved-profile search: the 'Profile name search' field and the 'Search profiles' / 'Clear search' buttons (Runtime/Presentation/CharacterProfileBrowser.cs:42-61).
- `sort.png` - The SeasonReport cast-table sort chips, 'Sort by placement/name/…' (SeasonReport.CastControls, Runtime/Presentation/SeasonReport.cs:579-593; captions :44-53).
- `strategy.png` - The StrategicDiscussion petal, which uses 'bulb' today (EpisodeDirector.cs:1032).
- `target.png` - Same name as the IconForge 'target', which is used by the nominee role badge (HudPrimitives.cs:416), EventGlyph nomination (EpisodeHud.Chrome.cs:360), CeremonyTakeover.IconFor (CeremonyTakeover.cs:140), and the RelationshipWeb Rivals stat (RelationshipWeb.cs:858) and legend (:595).
- `vote.png` - IconRail Mark.Votes, drawn from bars today (IconRail.cs:152), and the notebook heading 'HOW THE HOUSE VOTED' (EpisodeDirector.cs:841/850).

Files in `Assets/Gamesim/Resources/Packs/Pack2_Gameplay/Icons_PNG/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `alliance.png` | 128x128 | UiSprite |  |  |
| `calendar.png` | 128x128 | UiSprite |  |  |
| `chat.png` | 128x128 | UiSprite |  |  |
| `close.png` | 128x128 | UiSprite |  |  |
| `diary.png` | 128x128 | UiSprite |  |  |
| `filter.png` | 128x128 | UiSprite |  |  |
| `flirt.png` | 128x128 | UiSprite |  |  |
| `lock.png` | 128x128 | UiSprite |  |  |
| `search.png` | 128x128 | UiSprite |  |  |
| `sort.png` | 128x128 | UiSprite |  |  |
| `strategy.png` | 128x128 | UiSprite |  |  |
| `target.png` | 128x128 | UiSprite |  |  |
| `vote.png` | 128x128 | UiSprite |  |  |

### LiveFeed

**Destination** (partly exists): EpisodeHud.LiveFeedCard (Runtime/Episode/EpisodeHud.cs:590-620) and EpisodeHud.RecentEventsCard (Runtime/Episode/EpisodeHud.Chrome.cs:309-350), stacked in the right column by RightColumn (Chrome.cs:290-298).

*Evidence:* EpisodeHud.cs:595-598 'LIVE FEED' heading + 'Live dot' disc (Conflict), SetLiveFeedPaused :616-620; Chrome.cs:332-348 event rows (54 tall, RightColumnWidth 286), drawn as bare FixedText with no row object; EpisodeEvent carries sequence/week/phase/kind/text/audienceIds and no clock time (Simulation/EpisodeState.cs:180-185)

*Caution:* 'Live feed' and 'Recent events' are in the overlap list (EpisodePlayModeTests.Accessibility.cs:119-121) and are hidden by SetActivityLayout (EpisodeHud.Activities.cs:89-90). RecentEventRows = 3 was chosen to clear the controls box (Chrome.cs:64-69), so padded rows can bring the overlap back. Urgent red should stay exceptional.

- `live_badge_9slice.png` - Replaces the 'Live dot' disc and 'LIVE FEED' heading (EpisodeHud.cs:595-598). It needs a paused variant, because SetLiveFeedPaused recolours to Muted and says 'FEED PAUSED'. 40 px glow margin.
- `event_row_9slice.png` - Each Recent events row (Chrome.cs:332-348). The rows have no ground or container today, so each needs a new row rect.
- `event_row_urgent_9slice.png` - The same rows when the event kind is nomination or final-eviction (EventGlyph 'target', Chrome.cs:360), or eviction (:361).
- `timestamp_chip_9slice.png` - The 'WEEK n' label, which is drawn only for older events (Chrome.cs:343-345). Events carry no clock time.
- `toast_notification_9slice.png` - No toast system exists. The nearest thing is the lower-third 'Status' caption (EpisodeHud.cs:266-276, which runs HudReveal on a new message). A toast would be new chrome and must stay clear of the overlap test. 40 px glow margin.

Files in `Assets/Gamesim/Resources/Packs/Pack2_Gameplay/LiveFeed/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `event_row_9slice.png` | 736x156 | UiSliced | 28, 28, 28, 28 | 8, 8, 8, 8 |
| `event_row_urgent_9slice.png` | 736x156 | UiSliced | 28, 28, 28, 28 | 8, 8, 8, 8 |
| `live_badge_9slice.png` | 440x144 | UiSliced | 75, 69, 75, 69 | 40, 40, 40, 40 |
| `timestamp_chip_9slice.png` | 156x74 | UiSliced | 23, 23, 23, 23 | 8, 8, 8, 8 |
| `toast_notification_9slice.png` | 640x180 | UiSliced | 77, 77, 77, 77 | 40, 40, 40, 40 |

### Meters

**Destination** (partly exists): EpisodeHud.Meter 'Track'/'Fill' (Runtime/Episode/EpisodeHud.cs:1050-1078), animated by HudFill driving anchorMax.x (Runtime/Presentation/HudMotion.cs:82-110). Also the HouseVibe card's 'Vibe track'/'Vibe fill' (Runtime/Episode/EpisodeHud.Chrome.cs:259-271), where the fill is a sibling sized by width, not HudFill, and the Endurance grip (Runtime/Presentation/CompetitionGameScreen.cs:111-115).

*Evidence:* Meter callers: EpisodeDirector.cs:1162 'Interactions available', EpisodeDirector.Season.cs:61 'Master volume'; Meter track 8 px (EpisodeHud.cs:1064); HouseVibe rows Conversations (Paper) / Deals & alliances (Allied) / Game moves (Danger) (HouseVibe.cs:24-31), vibe track 9 px; gripFill anchorMax + colour (CompetitionGameScreen.cs:304-305) on an 802x58 track

*Caution:* The meter files import as plain UiSprite with no slice border (Editor/UiPackCatalogue.cs:252-257), at 776x68. The project's tracks are 8-9 px tall, so a Simple sprite gets squashed about 8x; only the 58 px grip track is close. Image.Type Filled would require HudFill to drive fillAmount instead of anchorMax.x. The fills are baked gradients, while Meter(tint) and gripFill.color (Warning below 25%) recolour them, so Image.color must stay white and the tint parameter becomes a choice of sprite.

- `meter_track_empty.png` - 'Track' (EpisodeHud.cs:1062), 'Vibe track' (Chrome.cs:259), and the result 'Track' (CompetitionResult.cs:125).
- `meter_allied_fill.png` - The 'Deals & alliances' vibe row fill (HouseVibe.cs:28 → Chrome.cs:267).
- `meter_conflict_fill.png` - The 'Game moves' vibe row fill (HouseVibe.cs:31).
- `meter_trust_fill.png` - No trust bar exists. Trust is shown as a number in the RelationshipWeb 'Your trust' tile (RelationshipWeb.cs:867) and the Annotate 'Trust n' reading (EpisodeHud.cs:997). A bar would be new.
- `meter_influence_fill.png` - No influence stat exists. The nearest meter is 'Interactions available' (EpisodeDirector.cs:1162).
- `meter_stamina_fill.png` - Endurance 'Grip remaining' (CompetitionGameScreen.cs:112, updated :304-305). This is the one meter whose 58 px height suits the 68 px art.

Files in `Assets/Gamesim/Resources/Packs/Pack2_Gameplay/Meters/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `meter_allied_fill.png` | 776x68 | UiSprite |  |  |
| `meter_conflict_fill.png` | 776x68 | UiSprite |  |  |
| `meter_influence_fill.png` | 776x68 | UiSprite |  |  |
| `meter_stamina_fill.png` | 776x68 | UiSprite |  |  |
| `meter_track_empty.png` | 776x68 | UiSprite |  |  |
| `meter_trust_fill.png` | 776x68 | UiSprite |  |  |

### Minimap

**Destination** (does not exist yet): No minimap exists. The observation mode is the overview camera, EpisodeDirector.ShowOverview (Runtime/Episode/EpisodeDirector.Overview.cs:41-55). It shows the RoomLabels world-space chips (Runtime/Presentation/RoomLabels.cs:51-75) and the 'Overview column' (Runtime/Episode/EpisodeHud.cs:623-646). A minimap would need a second orthographic camera and a RenderTexture on the LiveFeed pattern ('Live feed camera', Runtime/Presentation/LiveFeed.cs:17-25), plus a card in the overview column.

*Evidence:* RoomLabels.cs:59-66 '<Room> label' world canvas + HudPrimitives.Glass 'Chip' (ChipWidth 330 x ChipHeight 56, :20); HouseMap 'Room card' (Runtime/Presentation/HouseMap.cs:109); FollowRing 3D disc + diamond (Runtime/Presentation/FollowRing.cs:69-86); no minimap or pin objects in EpisodeHouse.unity

*Caution:* The pins have baked colours: player #15D6D0, houseguest #00BFFF, alert #FF4D5E, room #F6C344. The gold room pin clashes with the gold-is-power rule (UiTheme.Gold :28; the rationale is in the Brass doc :61-66). RoomLabels builds its chips on Show and destroys them on Hide so nothing leaks into ordinary frames, and pins must do the same.

- `room_label_plate_9slice.png` - This one has a real home: the RoomLabels 'Chip' (RoomLabels.cs:66, via HudPrimitives.Glass, so the Glass ground/Border/Glow rules apply) and the notebook's HouseMap 'Room card' (HouseMap.cs:109).
- `minimap_frame_circle.png` - Nothing to frame yet: it needs a new map camera, a RenderTexture and a card for them.
- `pin_player.png / pin_houseguest.png` - No pins exist. They would be world-space chips per HouseNpc while the overview is up, as RoomLabels does, or pins on a new map. FollowRing is the only existing marker.
- `pin_room.png` - At the HouseRoomMarker positions (EpisodeDirector.Overview.cs:83-87).
- `pin_event_alert.png` - There is no located-event marker. HouseEventState (Simulation/HouseEventState.cs:20-40) carries no room or position to pin to.

Files in `Assets/Gamesim/Resources/Packs/Pack2_Gameplay/Minimap/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `minimap_frame_circle.png` | 336x336 | UiSprite |  |  |
| `pin_event_alert.png` | 128x160 | UiSprite |  |  |
| `pin_houseguest.png` | 128x160 | UiSprite |  |  |
| `pin_player.png` | 128x160 | UiSprite |  |  |
| `pin_room.png` | 128x160 | UiSprite |  |  |
| `room_label_plate_9slice.png` | 736x196 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |

### Notebook

**Destination** (partly exists): The notebook is drawn in EpisodeDirector.Render (Runtime/Episode/EpisodeDirector.cs:910; notebook branch :933-977), one section at a time. People (RenderNotebookPeople :822) and Votes (RenderNotebookVotes :835) use EpisodeHud.PortraitRow, a read-only Chrome('Voter') card (Runtime/Episode/EpisodeHud.cs:545-577). Story (RenderNotebookStory :864) is flow Paragraphs. Secrets and promises also appear in the RelationshipWeb column (Runtime/Presentation/RelationshipWeb.cs:704-730).

*Evidence:* EpisodeDirector.cs:852-860 vote rows, :883-892 promise lines, :740-747 NotebookSection names; RelationshipWeb.cs:704-710 'Secrets known' = all of the player's memories, with isPrivate only switching the glyph eye/journal; EpisodeHud.Tag(Button,…) (EpisodeHud.cs:836-869) is an Accent .16 chip

*Caution:* EpisodeHud.Tag only attaches to a Button, but PortraitRow and Paragraph are not buttons, so notebook tags need a new overload or child. Its seats are measured to clear the chevron, trust reading and ALLY tag (EpisodeHud.cs:855-863); keep the tag text as TMP. The section names 'Section · …' are what the icon rail scrolls to: EpisodeHud.Mark (:651) renames the last content child. The pack guide says certainty colours must not overload sentiment colours.

- `note_row_9slice.png` - The PortraitRow 'Voter' card (EpisodeHud.cs:548).
- `tag_promise_9slice.png` - The Story promise lines (EpisodeDirector.cs:883-892) and the RelationshipWeb 'Between you' list (RelationshipWeb.cs:711-730).
- `tag_vote_9slice.png` - The Votes section rows (EpisodeDirector.cs:852-860).
- `tag_secret_9slice.png` - RelationshipWeb 'Secrets known' rows where memory.isPrivate (RelationshipWeb.cs:704-710).
- `tag_target_9slice.png` - The backdoor-plan rows 'Aim this week at X', which already carry a Tag (Category SetBackdoorPlan) (EpisodeDirector.DiaryRoom.cs:457-461), and the 'This week is aimed at' line (:466). They live in the decision panel, not the notebook.
- `tag_confirmed_9slice.png / tag_suspected_9slice.png / tag_rumor_9slice.png` - Nothing uses these yet: the simulation has no information-certainty model. SpreadRumor is a command (Simulation/EpisodeState.cs:421) with an engine handler (EpisodeEngine.cs:1339), but no stored rumour or knowledge record. A certainty field on knowledge would have to be added first.

Files in `Assets/Gamesim/Resources/Packs/Pack2_Gameplay/Notebook/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `note_row_9slice.png` | 736x136 | UiSliced | 28, 28, 28, 28 | 8, 8, 8, 8 |
| `tag_confirmed_9slice.png` | 236x84 | UiSliced | 32, 32, 32, 32 | 8, 8, 8, 8 |
| `tag_promise_9slice.png` | 236x84 | UiSliced | 32, 32, 32, 32 | 8, 8, 8, 8 |
| `tag_rumor_9slice.png` | 236x84 | UiSliced | 32, 32, 32, 32 | 8, 8, 8, 8 |
| `tag_secret_9slice.png` | 236x84 | UiSliced | 32, 32, 32, 32 | 8, 8, 8, 8 |
| `tag_suspected_9slice.png` | 236x84 | UiSliced | 32, 32, 32, 32 | 8, 8, 8, 8 |
| `tag_target_9slice.png` | 236x84 | UiSliced | 32, 32, 32, 32 | 8, 8, 8, 8 |
| `tag_vote_9slice.png` | 236x84 | UiSliced | 32, 32, 32, 32 | 8, 8, 8, 8 |

### RelationshipWeb

**Destination** (exists): RelationshipWeb.Build (Runtime/Presentation/RelationshipWeb.cs:277), reached through EpisodeHud.SocialGraphPanel (Runtime/Episode/EpisodeHud.cs:519-529) on the notebook's Network page. Edges come from Edge/Segment (:393-435), nodes from Node (:437-490), and the side panel is the 'Relationship column' 'Glass' (:675-680).

*Evidence:* EdgeColour: Alliance=AccentDeep, Friendship=Allied #4ADE80, Rivalry/Distrust=Conflict #FF5A5A (RelationshipWeb.cs:255-265); distrust dashed (:415-421); selected 'Halo' (:476); player node (:385-387); Kind enum (:67); ColumnName (:31)

*Caution:* The line sprites are 512x64 with a 6 px solid core (rows 30-35), but the edges are 1.5-5 px rects (:401-403), so a stretched sprite draws the core at sub-pixel size; the rect height has to be about 10x the weight. The semantics clash: here Alliance is blue and Friendship green, while the pack's allied is green #2ED47A and friend is cyan #00BFFF, the other way round. The pack's distrust line is solid orange #FF7A1A; the project dashes distrust in red. Tests count 'Edge' objects (EpisodePlayModeTests.RelationshipWeb.cs:191), expect exactly one 'Halo' (:224), and pin MoodColour (EditMode/RelationshipWebTests.cs:190-191). The node's transparent Circle hit image is its Button target (:452-457), so keep it.

- `line_allied.png / line_friend.png / line_rival.png / line_distrust.png` - Edge Segments (RelationshipWeb.cs:393-435). Map them by meaning, not by file name, because the colours are inverted (see caution).
- `line_romance.png` - Nothing uses this yet: there is no romance Kind (RelationshipWeb.cs:67). Its pink #FF4F9A is close to UiTheme.Flirt.
- `node_regular.png / node_player.png` - The Node rim/holder (RelationshipWeb.cs:449-481). The player's node is placed at :385-387, isPlayer with an Accent ring (:443).
- `node_selected.png` - Replaces the 'Halo' disc on the selected node (RelationshipWeb.cs:476).
- `side_info_panel_9slice.png` - The 'Relationship column' Glass (RelationshipWeb.cs:675-680). It is a Fill plus AddBorder, not UiTheme.Glass.

Files in `Assets/Gamesim/Resources/Packs/Pack2_Gameplay/RelationshipWeb/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `line_allied.png` | 512x64 | UiSprite |  |  |
| `line_distrust.png` | 512x64 | UiSprite |  |  |
| `line_friend.png` | 512x64 | UiSprite |  |  |
| `line_rival.png` | 512x64 | UiSprite |  |  |
| `line_romance.png` | 512x64 | UiSprite |  |  |
| `node_player.png` | 300x300 | UiSprite |  |  |
| `node_regular.png` | 236x236 | UiSprite |  |  |
| `node_selected.png` | 300x300 | UiSprite |  |  |
| `side_info_panel_9slice.png` | 536x556 | UiSliced | 32, 32, 32, 32 | 8, 8, 8, 8 |

## Pack 3 - Pack3_Systems

Deep systems and progression: main menu, saves, dossier, intelligence, competition HUD, recap, finale/jury.

### CompetitionHUD

**Destination** (partly exists): Split across three screens that exist. (1) CompetitionGameScreen.Show() (Runtime/Presentation/CompetitionGameScreen.cs:54-133): the full-screen mini-game surface ('Competition studio', 1420x780, with the clock, endurance grip track and 16 memory cards). (2) CompetitionResult.Build() (Runtime/Presentation/CompetitionResult.cs:91-152, not 94-160): the standings card. It has the 'Award' label at :107, 'Winner' at :110 and 'Standing N' rows at :121, and the winner row is already tinted gold. (3) CeremonyTakeover.Faces() (Runtime/Presentation/CeremonyTakeover.cs:389): the veto-field portrait row for VetoSelectionKind (:94). It is played from EpisodeDirector.cs:661, with VetoField() at EpisodeDirector.Ceremony.cs:86. There is no quiz. The kinds are Precision/Endurance/Reaction/Memory (CompetitionMiniGames.cs:86), and the web 'Mental' category maps to Memory (:104). The quiz_* art and answer badges C/D have nothing to go on.

*Evidence:* CompetitionGameScreen.cs:70-72 ('Competition studio'), :77 'Rules', :78 'Competition clock', :80 'Game surface', :83 'Competition field' (a text list), :111-116 'Grip track'/'Grip remaining'/'Toggle grip', :122 'Countdown', :140 'Memory card N', :277/:287/:305 Refresh tints. CompetitionResult.cs:103 'Card glass', :107, :110, :121. CeremonyTakeover.cs:94/:143-144/:157. EpisodeDirector.Challenge.cs:89-131 CompetitionBriefing (:102 'not drawn for this veto field'). EpisodeHud.cs:738-739 jury 'A · '/'B · ' actions (not :736-737). EpisodeDirector.cs:1134-1135 spectator results. MiniGameRun.cs:38 Remaining/TimeLimit. Slice borders are in UiPackCatalogue.cs:285-297.

*Caution:* CeremonyGlassPlayModeTests.CompetitionResult_IsDrawnOnGlass (Tests/PlayMode/CeremonyGlassPlayModeTests.cs:117-137, AssertGlass :143-166) pins 'Card glass' Image.color == UiTheme.GlassFill, with 'Border' and 'Glow' children. It also pins exactly two Buttons: 'Continue from competition results' and 'Review competition score details'. Takeover_IsDrawnOnGlass (:57) pins CeremonyTakeover's 'Card glass' the same way. It also runs AssertNothingTakesAClick (:172-178), so any veto-card art under the takeover must set raycastTarget=false. Pack art can therefore only be a child decoration, never the card sprite. CompetitionSurface tests find 'Memory card N', 'Game surface', 'Toggle grip' and 'Competition studio' by name. Neither those tests nor the result tests pin colours. Both canvases use a 1600x900 reference (CompetitionGameScreen.cs:48, CompetitionResult.cs:46), not 1920. The selected/active/first/banner sprites carry 80 px borders with 40 px glow insets (UiPackCatalogue.cs:287/292/294/296); the glow bleeds past small rects. The baked colours are cyan #00BFFF and gold #F6C344, not UiTheme.Accent #99D9FF, AccentDeep #1A73FF or Gold #FFC726 (verified by pixel sample).

- `timer_ring.png` - CompetitionGameScreen 'Competition clock' (:78) and 'Countdown' (:122). Needs Image.Type=Filled with fillAmount = run.Remaining / run.TimeLimit (MiniGameRun.cs:38), set in Refresh() (:277). Today it is text only.
- `endurance_stamina_panel_9slice.png` - The Endurance branch 'Grip track' behind 'Grip remaining' (CompetitionGameScreen.cs:111-116). The fill turns UiTheme.Warning under 25% (:305). The panel's baked orange is #FF7A1A, which is not a UiTheme token (Warning is #FF8A5C).
- `leaderboard_row_9slice.png` - CompetitionResult 'Standing N' rows (:121), currently UiTheme.Surface, 30 px tall at a 34 px pitch. The sprite border is 30 px plus an 8 px inset (UiPackCatalogue.cs:288), so it needs pixelsPerUnitMultiplier of about 2.5 or more to fit. Secondary use: the spectator score paragraphs at EpisodeDirector.cs:1135 (not :1128, which is the CompetitionBriefing call).
- `leaderboard_first_9slice.png` - The winner row in the same loop (:121, the hard-coded Color(.28,.23,.10)). Its 40 px glow inset overlaps the neighbouring rows at the 34 px pitch.
- `results_winner_banner_9slice.png` - Behind the CompetitionResult 'Winner' label and the gold portrait (:109-110).
- `power_awarded_panel_9slice.png` - No 'power awarded' card exists, and 'Card glass' is test-pinned. It could go in as a non-raycast child behind the 'Award' label (:107) and the Winner block.
- `veto_player_card_9slice.png` - A per-subject slot in CeremonyTakeover.Faces (:405-419; the slot is 150 px x 116 px at scale 1) for VetoSelectionKind. It must be non-raycast. Secondary use: CompetitionGameScreen 'Competition field' (:83), a plain text list today.
- `veto_player_active_9slice.png` - The player's own slot in the veto field. Faces has no active-player state: the Subject struct (CeremonyTakeover.cs:40-52) would need an IsPlayer flag. At 380x300 with a 40 px glow it is large for a 150-px slot.
- `quiz_question_panel_9slice.png` - No quiz mini-game exists. The nearest host is the 'Rules' header (CompetitionGameScreen.cs:77).
- `quiz_answer_A_9slice.png` - No quiz. The only A/B choice in the game is the finalist's jury answer (EpisodeHud.cs:738).
- `quiz_answer_B_9slice.png` - Same as A (EpisodeHud.cs:739). Could also be the resting skin for 'Memory card N' buttons (tinted Ink at CompetitionGameScreen.cs:287).
- `quiz_answer_selected_9slice.png` - No quiz. Could be the face-up memory card (AccentDeep at :287).
- `answer_badge_A.png` - Jury answer A only (EpisodeHud.cs:738).
- `answer_badge_B.png` - Jury answer B only (EpisodeHud.cs:739).
- `answer_badge_C.png` - No consumer: nothing in the game offers four choices.
- `answer_badge_D.png` - No consumer.
- `eliminated_overlay_9slice.png` - No mini-game has an elimination state: each is one attempt, and seeded scores are resolved at commit. The nearest thing is the 'not drawn for this veto field' paragraph (EpisodeDirector.Challenge.cs:102).

Files in `Assets/Gamesim/Resources/Packs/Pack3_Systems/CompetitionHUD/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `answer_badge_A.png` | 120x120 | UiSprite |  |  |
| `answer_badge_B.png` | 120x120 | UiSprite |  |  |
| `answer_badge_C.png` | 120x120 | UiSprite |  |  |
| `answer_badge_D.png` | 120x120 | UiSprite |  |  |
| `eliminated_overlay_9slice.png` | 736x106 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `endurance_stamina_panel_9slice.png` | 656x136 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `leaderboard_first_9slice.png` | 800x170 | UiSliced | 80, 77, 80, 77 | 40, 40, 40, 40 |
| `leaderboard_row_9slice.png` | 736x106 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `power_awarded_panel_9slice.png` | 776x276 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `quiz_answer_A_9slice.png` | 396x156 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `quiz_answer_B_9slice.png` | 396x156 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `quiz_answer_selected_9slice.png` | 460x220 | UiSliced | 80, 79, 80, 79 | 40, 40, 40, 40 |
| `quiz_question_panel_9slice.png` | 996x276 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `results_winner_banner_9slice.png` | 980x220 | UiSliced | 80, 79, 80, 79 | 40, 40, 40, 40 |
| `timer_ring.png` | 320x320 | UiSprite |  |  |
| `veto_player_active_9slice.png` | 380x300 | UiSliced | 80, 79, 80, 79 | 40, 40, 40, 40 |
| `veto_player_card_9slice.png` | 316x236 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |

### Dossier

**Destination** (partly exists): No full dossier screen exists; two surfaces cover part of it. (a) RelationshipWeb.Column() (Runtime/Presentation/RelationshipWeb.cs:635) is a 250-px column (ColumnWidth :57) beside the graph on the notebook's Network page. It is reached through EpisodeHud.SocialGraphPanel() (Runtime/Episode/EpisodeHud.cs:519), called at EpisodeDirector.cs:973, and built from Header (:800), Stats (:842), SectionHeading (:898), PersonRow (:917) and TextRow (:952). Key allies, Top rivals and Secrets known render only when the player is the focus (:692-712). Between you, What you know and Recent history render for any other houseguest (:713-760). (b) EpisodeHud.ChooseNominationPair() (Runtime/Episode/EpisodeHud.Decisions.cs:38-79) already draws a two-column 'Selected candidate comparison'. It has one 'Candidate context <id>' card per candidate (:69), each with a portrait, the record 'HoH N · Veto N · Nominated N' (DecisionContext.cs:34), trust and promises. A true 2–3-column dossier would be a new full-screen overlay like SeasonReport, fed from these helpers.

*Evidence:* RelationshipWeb.cs:39-44 (section headings), :675 ('Glass' behind the column), :807 (Header portrait), :835-838 (HOH/VETO/NOM chip), :855-870 (Stats tiles are a glyph, a number and a caption, with no per-tile background). EpisodeHud.Decisions.cs:13-15 (captions/names), :64-75, :88 (card ground via UiTheme.Style Surface). EpisodeDirector.cs:822 RenderNotebookPeople (a PortraitRow per houseguest with status and trust). Simulation/ThreatAssessment.cs:48/:60/:69: nothing in Runtime references it. The mapping was wrong that hohWins/vetoWins appear only in SeasonReport: in-season they are shown in DecisionContext.cs:34, and also at SeasonReport.cs:292-296/:335-339/:474.

*Caution:* The RelationshipWeb PlayMode tests (Tests/PlayMode/EpisodePlayModeTests.RelationshipWeb.cs:22-276) pin fit, no clipping and scrolling inside the 250-px column at both text sizes. The catalogue slice borders are 26–41 px, each plus an 8 px inset (UiPackCatalogue.cs:298-306: badges 26, history row 28, stat panel 33, portrait frame 35, shell 41). That would eat most of the column, and a Stats tile is only about 75 px wide. EpisodeDecisionContextPlayModeTests.cs:75-95 finds 'Selected candidate comparison' and 'Candidate context <id>' by name. dossier_portrait_frame is rectangular (436x536). Every HUD portrait is the circular masked HudPrimitives.Portrait (HudPrimitives.cs:278), and the underlying CharacterPortraits render is a square crop. The badges bake red #FF4D5E, purple #8A5CF6 and green #2ED47A (verified), not UiTheme.Danger #FF6B6B / Conflict #FF5A5A, Strategic #A56BFF or Allied #4ADE80.

- `dossier_shell_9slice.png` - The column's 'Glass' (RelationshipWeb.cs:675). The 1296x816 shell really wants a new full-screen dossier.
- `dossier_portrait_frame_9slice.png` - The Header portrait (RelationshipWeb.cs:807). It needs a rectangular crop of the square portrait render instead of the circular Mask; no rectangular portrait exists today.
- `dossier_stat_panel_9slice.png` - Behind the Stats() tiles (:842-895), which have no background today, or as the 'Candidate context <id>' card ground (EpisodeHud.Decisions.cs:69/:88).
- `dossier_history_row_9slice.png` - The 'Recent history' TextRows (RelationshipWeb.cs:756-760) and PersonRow (:917).
- `dossier_alliance_badge_9slice.png` - The alliance lines under 'Between you' (:716-717; :718-723 are promises) and the 'Key allies' section (:695-698).
- `dossier_rival_badge_9slice.png` - The 'Top rivals' section (:701-704).
- `dossier_nom_badge_9slice.png` - The Header 'NOM' chip (:838), currently UiTheme.Danger.
- `dossier_comp_badge_9slice.png` - The Header 'HOH'/'VETO' chip (:835-838), or the candidate record line 'HoH N · Veto N' (DecisionContext.cs:34, drawn at EpisodeHud.Decisions.cs:73). That line is the only place in-season that shows win counts.
- `dossier_secret_badge_9slice.png` - The 'Secrets known' section (:708-711; player focus only).
- `threat_gauge.png` - Nothing to host it. ThreatAssessment.Total(state, evaluatorId, targetId) (Simulation/ThreatAssessment.cs:60) is computed but never displayed. It would become a Stats tile or a line in the candidate card.

Files in `Assets/Gamesim/Resources/Packs/Pack3_Systems/Dossier/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `dossier_alliance_badge_9slice.png` | 216x88 | UiSliced | 26, 26, 26, 26 | 8, 8, 8, 8 |
| `dossier_comp_badge_9slice.png` | 216x88 | UiSliced | 26, 26, 26, 26 | 8, 8, 8, 8 |
| `dossier_history_row_9slice.png` | 936x126 | UiSliced | 28, 28, 28, 28 | 8, 8, 8, 8 |
| `dossier_nom_badge_9slice.png` | 216x88 | UiSliced | 26, 26, 26, 26 | 8, 8, 8, 8 |
| `dossier_portrait_frame_9slice.png` | 436x536 | UiSliced | 35, 35, 35, 35 | 8, 8, 8, 8 |
| `dossier_rival_badge_9slice.png` | 216x88 | UiSliced | 26, 26, 26, 26 | 8, 8, 8, 8 |
| `dossier_secret_badge_9slice.png` | 216x88 | UiSliced | 26, 26, 26, 26 | 8, 8, 8, 8 |
| `dossier_shell_9slice.png` | 1296x816 | UiSliced | 41, 41, 41, 41 | 8, 8, 8, 8 |
| `dossier_stat_panel_9slice.png` | 576x276 | UiSliced | 33, 33, 33, 33 | 8, 8, 8, 8 |
| `threat_gauge.png` | 420x260 | UiSprite |  |  |

### FinaleJury

**Destination** (partly exists): In-season, the finale is text in the shared 900x300 'Episode panel' modal (EpisodeHud.cs:296, sized at :338-339). The pieces are EpisodeHud.JuryQuestioning() (EpisodeHud.cs:692; FlowText 'Jury question' at :710), FinalSpeech() (:749), the player-as-juror ballot buttons 'Vote for X to win' (EpisodeDirector.cs:1147-1151; buttons, not a list of juror votes) and the Finished panel (EpisodeDirector.cs:1107-1120), which lists 'HOW THE JURY VOTED' ballot lines at :1114-1115. After the season, SeasonReport is the real finale screen: Winner()/Finalist() (SeasonReport.cs:231-282), HowTheJuryVoted() (:416) and Standings() (:443). CeremonyTakeover also plays a WinnerKind card (CeremonyTakeover.cs:104/:143) off the engine's 'winner' event (EpisodeEngine.cs:720). There is no finalist-slot or jury-vote-reveal screen; VoteReveal only covers evictions (EpisodeDirector.cs:678).

*Evidence:* EpisodeHud.cs:23-29 jury/speech captions; EpisodeDirector.cs:1105-1106 phase dispatch; SeasonReport.cs:245-247 (WINNER/RUNNER-UP), :271 'Badge' label, :395 JuryBallots, :424-427 tally, :432-439 ballot rows, :466-475 standings rows, :768 PlacementTint; EpisodeEngine.cs:705 'jury-vote' events

*Caution:* 'Episode panel' is built with Chrome() (EpisodeHud.cs:1402) for every phase, and tests look it up by name (EpisodePlayModeTests.Keyboard.cs:35, .Chrome.cs:150, EpisodePlayModeTests.cs:284/:316, .Shots.cs:170). Chrome_WearsNoAccentEdgeWhileNothingIsAskingToBeActedOn (EpisodePlayModeTests.Chrome.cs:36-60) runs after ClosePanels(), so it does not pin the open panel's Border. It does pin the resting edge on the persistent chrome, which also comes from Chrome(). So a finale skin must be phase-conditional on the panel only, never a change to Chrome() itself. Captions such as JuryContinueCaption and 'Vote for X to win' are the test contract. There is no jury-ballot privacy test: EpisodeBallotPrivacyTests (Tests/EditMode) covers eviction ballots only. In fact the NPC jurors' votes are logged as public 'jury-vote' events (EpisodeEngine.cs:705) before the player-juror votes. SeasonReport tests (EpisodePlayModeTests.SeasonReport.cs) read names and lines, not colours.

- `jury_shell_9slice.png` - The 'Episode panel' during JuryQuestioning/FinalSpeeches/Jury. Its 1376x836 aspect assumes a full-screen jury view that does not exist.
- `jury_question_panel_9slice.png` - Behind FlowText 'Jury question' (EpisodeHud.cs:710).
- `juror_card_9slice.png` - SeasonReport.HowTheJuryVoted rows (SeasonReport.cs:432), and in-season the Finished panel's ballot paragraphs (EpisodeDirector.cs:1115). There is no in-season juror roster before Finished.
- `juror_card_voted_9slice.png` - Same rows after the vote. A pre-reveal 'has voted' state exists in the data (NPC ballots are recorded and logged at EpisodeEngine.cs:705 while the player-juror has not yet voted). SeasonReport.JuryBallots (:395) only counts them once the finalists are Winner/RunnerUp.
- `jury_vote_token_9slice.png` - The tally line '<winner> N · <runner-up> M' (SeasonReport.cs:424-427).
- `finalist_card_9slice.png` - The SeasonReport.Finalist() runner-up slot (:247). In-season, finalists are only names in paragraphs.
- `winner_card_9slice.png` - The SeasonReport.Finalist() winner slot (:245) and the CeremonyTakeover WinnerKind card (its 'Card glass' is test-pinned, so child decoration only).
- `placement_row_9slice.png` - SeasonReport.Standings rows (:466), 34 px tall. The border is 28 px plus an 8 px inset (UiPackCatalogue.cs:319), so it needs pixelsPerUnitMultiplier of 2 or more.
- `badge_winner.png` - The SeasonReport 'Badge' label WINNER (:271-274).
- `badge_placement.png` - The rank cell in Standings (:468).
- `badge_jury.png` - The StatusWord Jury cell in Standings (:471) and the juror spectator note (EpisodeDirector.Journal.cs:178 SpectatorDetail).
- `badge_vote.png` - Ballot rows in HowTheJuryVoted (:432).
- `badge_season_star.png` - The 'SEASON COMPLETE' header (SeasonReport.cs:214) or the YourCareer card (:350).

Files in `Assets/Gamesim/Resources/Packs/Pack3_Systems/FinaleJury/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `badge_jury.png` | 180x180 | UiSprite |  |  |
| `badge_placement.png` | 180x180 | UiSprite |  |  |
| `badge_season_star.png` | 180x180 | UiSprite |  |  |
| `badge_vote.png` | 180x180 | UiSprite |  |  |
| `badge_winner.png` | 180x180 | UiSprite |  |  |
| `finalist_card_9slice.png` | 480x580 | UiSliced | 85, 84, 85, 84 | 40, 40, 40, 40 |
| `juror_card_9slice.png` | 316x356 | UiSliced | 33, 33, 33, 33 | 8, 8, 8, 8 |
| `juror_card_voted_9slice.png` | 316x356 | UiSliced | 33, 33, 33, 33 | 8, 8, 8, 8 |
| `jury_question_panel_9slice.png` | 936x246 | UiSliced | 32, 32, 32, 32 | 8, 8, 8, 8 |
| `jury_shell_9slice.png` | 1376x836 | UiSliced | 42, 42, 42, 42 | 8, 8, 8, 8 |
| `jury_vote_token_9slice.png` | 236x88 | UiSliced | 26, 26, 26, 26 | 8, 8, 8, 8 |
| `placement_row_9slice.png` | 776x106 | UiSliced | 28, 28, 28, 28 | 8, 8, 8, 8 |
| `winner_card_9slice.png` | 500x600 | UiSliced | 85, 84, 85, 84 | 40, 40, 40, 40 |

### Icons_PNG

**Destination** (partly exists): The glyphs map onto existing controls, but UiTheme.Icon(name) (Runtime/Presentation/UiTheme.cs:262-270) only loads Resources/GamesimIcons/<name>, so nothing can reach Packs/Pack3_Systems/Icons_PNG until that lookup changes. continue, new, settings and quit go on MainMenu.Button() (MainMenu.cs:179) as a sibling image beside its captions. save and settings also go on the HUD's 'Save [F5]' and 'Settings' FixedButtons (EpisodeHud.cs:232-233). history goes on WeeklyRecapScreen.ReviewCaption and RelationshipWeb's 'Recent history' heading (SectionHeading, :898, has no icon slot today). secret goes on the secrets TextRow (RelationshipWeb.cs:711, currently 'eye'/'journal'). winner goes on the SeasonReport badge and CeremonyTakeover.IconFor(WinnerKind) (CeremonyTakeover.cs:143, currently 'trophy'). jury goes on SeasonReport.HowTheJuryVoted. winner and jury also fit EpisodeHud.EventGlyph (EpisodeHud.Chrome.cs:356; 'winner'/'jury-vote'/'jury-tie' use 'crown' at :364), and secret could replace the 'journal' default there for 'rumour' events. threat has nothing to go on: no threat UI exists.

*Evidence:* UiTheme.cs:262-270; Editor/IconForge.cs:27 (Folder = Assets/Gamesim/Resources/GamesimIcons), :473 (generates 'settings'), :475 ('exit'); Resources/GamesimIcons already has settings.png, exit.png, trophy.png, crown.png, journal.png and eye.png; EpisodeHud.Chrome.cs:356-369 EventGlyph

*Caution:* settings.png collides with GamesimIcons/settings.png. Dropping pack icons into GamesimIcons is not safe either: IconForge regenerates that folder on 'Generate HUD icons' and would overwrite them. The pack icons are #DCE8F5 (verified), not white, so tinting with Image.color gives about 86–96% of the token colour. HUD captions are the contract: an icon must be a sibling of the TMP label, never text in the caption. Accessibility_NoCopyIsClippedAtEitherTextSize (EpisodePlayModeTests.Accessibility.cs:31) and Accessibility_FixedChromeNeverOverlapsAtEitherTextSize (:57) will catch the 122-px 'Save [F5]' button losing label room.

- `threat.png` - No consumer until a threat readout exists (see Dossier threat_gauge).
- `settings.png` - Name collides with the IconForge-generated GamesimIcons/settings.png.

Files in `Assets/Gamesim/Resources/Packs/Pack3_Systems/Icons_PNG/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `continue.png` | 128x128 | UiSprite |  |  |
| `history.png` | 128x128 | UiSprite |  |  |
| `jury.png` | 128x128 | UiSprite |  |  |
| `new.png` | 128x128 | UiSprite |  |  |
| `quit.png` | 128x128 | UiSprite |  |  |
| `save.png` | 128x128 | UiSprite |  |  |
| `secret.png` | 128x128 | UiSprite |  |  |
| `settings.png` | 128x128 | UiSprite |  |  |
| `threat.png` | 128x128 | UiSprite |  |  |
| `winner.png` | 128x128 | UiSprite |  |  |

### MainMenu

**Destination** (exists): MainMenu.Rebuild() (Runtime/Presentation/MainMenu.cs:102-175) builds the scrim, the 'Column', the eye 'Mark' and the title, then three or four captioned buttons from MainMenu.Button() (:179-201): Continue appears only when canContinue. Below them sit the career and offline lines. EpisodeDirector.OpenMainMenu() (Runtime/Episode/EpisodeDirector.Season.cs:120) opens it as an overlay at sort order 130 (MainMenu.cs:66), in front of the already-built house. That happens at launch (EpisodeDirector.cs:175) and from Settings 'Main menu' (Season.cs:60). Continue is offered from File.Exists only (Season.cs:127), so a Continue card showing Week/HoH/remaining would have to read engine.Snapshot as loaded from disk.

*Evidence:* MainMenu.cs:32-35 captions, :113 scrim (0.98 alpha), :122 'Column', :134-152 'Mark', :155-157 title/tagline/recovery note, :160-163 buttons, :168 career line, :171 offline note, :181-184 fill + UiTheme.AddBorder

*Caution:* EpisodePlayModeTests.MainMenu.cs finds buttons through CastButtons (Tests/PlayMode/EpisodePlayModeTests.CastSelect.cs:29-33), which matches label.text == caption exactly. Metadata must go in extra labels and never be appended to the caption. Button() adds a UiTheme.AddBorder child named 'Border' (UiTheme.cs:227-230); drop it when the sprite brings its own edge, or the edge is drawn twice. No colour pins on this screen. The 0.98 scrim (:113) hides the house the guide wants as the backdrop; lowering it shows the live set.

- `main_menu_primary_panel_9slice.png` - New backing for 'Column' (:122). The column has no panel today.
- `main_menu_continue_card_9slice.png` - The ContinueCaption button (:160). Season metadata labels would be added as siblings.
- `menu_primary_button_9slice.png` - NewSeasonCaption when there is nothing to continue (primary=!canContinue, :161). 80 px borders with a 40 px glow inset (UiPackCatalogue.cs:336) against a 54-px button.
- `menu_secondary_button_9slice.png` - SettingsCaption (:162), and NewSeason when Continue exists.
- `menu_quit_button_9slice.png` - QuitCaption (:163), currently a plain SurfaceRaised secondary button. The pack sprite is red-edged; the guide wants quiet secondary actions.
- `main_menu_secondary_card_9slice.png` - The career line (:168) and offline note (:171).
- `season_badge_9slice.png` - The title 'BIG BROTHER' (:155), or a new Season/Week chip. The data is CareerLine() and engine.Snapshot.week.
- `profile_ring_selected.png` - The menu has no profile. The nearest host is CharacterProfileBrowser.Thumbnail() (CharacterProfileBrowser.cs:72), used by CastSelect.cs:359/:382 and CharacterCreator.Studio.cs:502.
- `loading_bar_track_9slice.png` - No loading screen exists. Bootstrap.unity runs GamesimBootstrap.Start (Runtime/Bootstrap/GamesimBootstrap.cs:37), which calls LoadSceneAsync at :43 with no UI, so a loader would have to be built there.
- `loading_bar_fill_9slice.png` - Same as the track: no loading screen to fill. The existing bars are EpisodeHud.Meter track/fill (EpisodeHud.cs:1050, Pack 2 Meters territory) and the competition 'Grip track'.

Files in `Assets/Gamesim/Resources/Packs/Pack3_Systems/MainMenu/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `loading_bar_fill_9slice.png` | 436x52 | UiSliced | 24, 24, 24, 24 | 8, 8, 8, 8 |
| `loading_bar_track_9slice.png` | 696x52 | UiSliced | 24, 24, 24, 24 | 8, 8, 8, 8 |
| `main_menu_continue_card_9slice.png` | 840x360 | UiSliced | 85, 84, 85, 84 | 40, 40, 40, 40 |
| `main_menu_primary_panel_9slice.png` | 876x736 | UiSliced | 39, 39, 39, 39 | 8, 8, 8, 8 |
| `main_menu_secondary_card_9slice.png` | 776x196 | UiSliced | 33, 33, 33, 33 | 8, 8, 8, 8 |
| `menu_primary_button_9slice.png` | 600x190 | UiSliced | 81, 80, 81, 80 | 40, 40, 40, 40 |
| `menu_quit_button_9slice.png` | 536x126 | UiSliced | 32, 32, 32, 32 | 8, 8, 8, 8 |
| `menu_secondary_button_9slice.png` | 536x126 | UiSliced | 32, 32, 32, 32 | 8, 8, 8, 8 |
| `profile_ring_selected.png` | 320x320 | UiSprite |  |  |
| `season_badge_9slice.png` | 276x92 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |

### NotebookIntel

**Destination** (partly exists): The notebook is the journalOpen branch of EpisodeDirector.Render() (Runtime/Episode/EpisodeDirector.cs:933-978; PanelTitle 'YOUR NOTEBOOK' at :935), one section at a time inside the docked 900x300 'Episode panel' (EpisodeHud.cs:296, :338-339). Intel-like content sits in three places. RenderNotebookVotes (:835) has vote rows via EpisodeHud.PortraitRow (EpisodeHud.cs:545). RenderNotebookStory (:864) has promises at :883-891 and memories at :900. RenderStorySoFar (EpisodeDirector.Journal.cs:118) has event lines at :143. The intel_shell needs a dedicated full-screen notebook, which does not exist. Certainty tags have no data behind them: MemoryState (Simulation/EpisodeState.cs:157) holds only owner, subject, text, week and isPrivate.

*Evidence:* EpisodeDirector.cs:740-747 NotebookSection; :852-859 vote rows with vote.reason; :456/:891 PromiseStanding; :900 memories as Paragraph; :822 RenderNotebookPeople; EpisodeDirector.Journal.cs:143; PromiseStatus.Broken (EpisodeState.cs:28); DealStatus.Broken (DealState.cs:128) and DealStatus.Binds (DealState.cs:138); schemaVersion = 13 (EpisodeState.cs:197)

*Caution:* Confirmed/suspected/rumour tags must be derived from event kind at display time. Otherwise it is a new saved field: a schema bump to 14, a migration, a frozen V13 DTO (FrozenEpisodeV12 is the latest today) and a new PersistenceV14MigrationTests (the existing PersistenceV13MigrationTests covers V12→V13). hud.Mark(section) markers must survive, because the icon rail scrolls to them. Accessibility_NoPanelClipsItsCopyAtEitherTextSize (EpisodePlayModeTests.Accessibility.cs:431) runs over this modal. Tests find 'Episode panel' by name. The Chrome edge test runs with panels closed, so it pins Chrome() (EpisodeHud.cs:1402), not the open modal. intel_vote_tag bakes indigo #5865F2 (verified), which has no UiTheme token.

- `intel_shell_9slice.png` - No full-screen notebook exists; the notebook is the shared 900x300 modal.
- `intel_card_9slice.png` - Memory entries (EpisodeDirector.cs:900) and RelationshipWeb 'What you know' rows (RelationshipWeb.cs:744-750; not :728-734, which is the deals block).
- `evidence_receipt_9slice.png` - The recorded vote.reason, shown as the PortraitRow body (EpisodeDirector.cs:859).
- `timeline_event_9slice.png` - RenderStorySoFar per-event lines (EpisodeDirector.Journal.cs:143).
- `intel_confirmed_tag_9slice.png` - No certainty field exists. The nearest proxy is MemoryState.isPrivate.
- `intel_suspected_tag_9slice.png` - No certainty field exists.
- `intel_rumor_tag_9slice.png` - No certainty field. Rumours exist only as the player's own 'rumour'/'rumour-backfire' events, logged by EpisodeEngine.SpreadRumor (Simulation/EpisodeEngine.cs:1339; Log calls at :1353/:1367/:1380; PublicCallout/WhisperCampaign are its command variants, :1275). They reach the notebook as plain RenderStorySoFar lines.
- `intel_target_tag_9slice.png` - PromiseState.targetId (EpisodeState.cs:136), deal targetId (DealState.cs:29) or ThreatAssessment.RankedTargets (ThreatAssessment.cs:69). None is shown with a tag today.
- `intel_vote_tag_9slice.png` - Rows in RenderNotebookVotes (:835).
- `intel_broken_promise_tag_9slice.png` - Promises with PromiseStatus.Broken (EpisodeDirector.cs:891) and broken promises under RelationshipWeb 'Between you' (:718-723, which prints promise.status). Broken DEALS never appear there: the deal loop filters on DealStatus.Binds (DealState.cs:138), which excludes Broken.

Files in `Assets/Gamesim/Resources/Packs/Pack3_Systems/NotebookIntel/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `evidence_receipt_9slice.png` | 556x276 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `intel_broken_promise_tag_9slice.png` | 236x84 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `intel_card_9slice.png` | 536x236 | UiSliced | 32, 32, 32, 32 | 8, 8, 8, 8 |
| `intel_confirmed_tag_9slice.png` | 236x84 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `intel_rumor_tag_9slice.png` | 236x84 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `intel_shell_9slice.png` | 1296x816 | UiSliced | 41, 41, 41, 41 | 8, 8, 8, 8 |
| `intel_suspected_tag_9slice.png` | 236x84 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `intel_target_tag_9slice.png` | 236x84 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `intel_vote_tag_9slice.png` | 236x84 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `timeline_event_9slice.png` | 876x126 | UiSliced | 26, 26, 26, 26 | 8, 8, 8, 8 |

### SaveSlots

**Destination** (partly exists): No slot picker exists. There is one active slot, picked from PlayerPrefs 'Gamesim.ActiveSave' (EpisodeDirector.cs:120-122). EpisodeDirector.Settings() (EpisodeDirector.Season.cs:48) shows it as one line, 'Slot: <file name>' (:54), with Save/Reload/Recover backup/New season actions (:56-59). Older episode-<guid>.json and import-<guid>.json slots (Season.cs:201/:260) are kept on disk but never listed. Autosave runs on every commit, and its only sign is the text ' · Saved locally.' added to the status message (EpisodeDirector.cs:625, Season.cs:92). That message is drawn in the 'Status' lower third (EpisodeHud.cs:266-276). A slot list, thumbnails and a toast would all have to be built, probably as a list in Settings() or a screen reached from MainMenu.SettingsCaption.

*Evidence:* EpisodeSaveStore.cs:24-25 (SavePath, BackupPath), :56/:72 TryLoad/TryRecoverBackup; Season.cs:50 'SAVE RECOVERY' title when blockedRecovery; EpisodeDirector.cs:128 and Durable.cs:85 set blockedRecovery; MainMenu.cs:157 recovery note in Warning; no Directory.GetFiles/EnumerateFiles over saves anywhere in Runtime; CharacterProfileStore.cs:39 is the only *.json enumeration (character profiles), the pattern a slot list would copy

*Caution:* Never show paths. Season.cs:51-53 records why the full path was removed. Tests/PlayMode/EpisodeDurableTransactionTests.cs:121/:177/:321/:352 all assert that director.StatusMessage does NOT contain 'Saved locally' on failed commits. A toast must key off the same success path and leave the message text alone. PortVerification.Season.cs:115-130/:189 pins slot preservation across 'New season in a NEW slot'. A thumbnail or metadata field inside EpisodeState forces a schema bump, so store it in a sidecar file. The autosave teal #15D6D0 (verified) has no UiTheme token.

- `save_slot_resting_9slice.png` - No slot list exists; this would be a new row per saveRoot/*.json.
- `save_slot_selected_9slice.png` - The active slot (saves.SavePath) in that new list.
- `save_slot_autosave_9slice.png` - The active slot is itself the autosave. The validated .backup (BackupPath) is the other real card.
- `save_slot_corrupt_9slice.png` - The blockedRecovery state (Season.cs:50, EpisodeDirector.cs:128).
- `save_meta_chip_9slice.png` - Week, phase, HoH and remaining count from EpisodeState. No metadata is read for slots today.
- `save_thumbnail_frame_9slice.png` - No screenshot is captured at save time (no ScreenCapture use in Runtime outside PortVerification).
- `autosave_toast_9slice.png` - No toast exists. Feedback is the status-line suffix (EpisodeDirector.cs:625), animated by HudReveal on message change (EpisodeHud.cs:269).

Files in `Assets/Gamesim/Resources/Packs/Pack3_Systems/SaveSlots/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `autosave_toast_9slice.png` | 600x168 | UiSliced | 76, 76, 76, 76 | 40, 40, 40, 40 |
| `save_meta_chip_9slice.png` | 196x74 | UiSliced | 24, 24, 24, 24 | 8, 8, 8, 8 |
| `save_slot_autosave_9slice.png` | 936x296 | UiSliced | 33, 33, 33, 33 | 8, 8, 8, 8 |
| `save_slot_corrupt_9slice.png` | 936x296 | UiSliced | 33, 33, 33, 33 | 8, 8, 8, 8 |
| `save_slot_resting_9slice.png` | 936x296 | UiSliced | 33, 33, 33, 33 | 8, 8, 8, 8 |
| `save_slot_selected_9slice.png` | 1000x360 | UiSliced | 83, 82, 83, 82 | 40, 40, 40, 40 |
| `save_thumbnail_frame_9slice.png` | 396x236 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |

### SeasonRecap

**Destination** (exists): WeeklyRecapScreen.Rebuild() (Runtime/Presentation/WeeklyRecapScreen.cs:137-187) is the week-end screen at sort order 110 (:74). It can be reopened from the notebook's 'WEEKS SO FAR' through ReviewWeek (EpisodeDirector.cs:877 → EpisodeDirector.Journal.cs:275). The HOH/nominee/veto/eviction beats are Ceremony() rows (:189-199), and relationship shifts are Relationships() (:209-221). The data comes from WeeklyRecap.Build() (WeeklyRecap.cs:43). It stores names, not ids, so portraits on the cards would need ids added to WeeklyRecap.Week (:256; presentation only, no save impact). SeasonReport.WeekByWeek() (SeasonReport.cs:489) is a second, season-level recap table.

*Evidence:* WeeklyRecapScreen.cs:30-32 captions, :142 scrim, :145-151 transparent full-height 'Viewport' with RectMask2D, :171-173 title/headline, :192-198 Row() calls, :217 positive=UiTheme.Accent / negative=UiTheme.Danger, :276-282 Row() (30-px Surface panel that also feeds Lines); WeeklyRecap.cs:259-260 (names, nominees list)

*Caution:* Row() feeds the Lines seam, which tests assert on (EpisodePlayModeTests.WeeklyRecap.cs:60-63 expects 'Evicted: X'). Card-ifying the ceremony must keep adding those lines. The rows are 30 px, while recap card borders are 33 px (+8 inset), and 83 px (+40 glow) for the HoH card (UiPackCatalogue.cs:359-362). Cards therefore mean taller rows and a cursor re-layout. The code tints a positive relationship move cyan (UiTheme.Accent, :217), but the pack's relationship_change_positive is green #2ED47A (verified). Adopting it changes that meaning to match UiTheme.Positive/Allied. The button captions ContinueCaption/ReviewCaption/BackCaption are the contract.

- `week_recap_shell_9slice.png` - Behind 'Viewport' (:145-151), which is 1080 wide against the 1376-wide art.
- `recap_hoh_card_9slice.png` - The Row 'Head of Household' (:192).
- `recap_nominee_card_9slice.png` - The Row 'Nominees' (:193). One card per name means splitting recap.nominees (WeeklyRecap.cs:260).
- `recap_veto_card_9slice.png` - The Rows 'Veto' and 'Veto used' (:194-197).
- `recap_eviction_card_9slice.png` - The Row 'Evicted' (:198).
- `relationship_change_positive_9slice.png` - Relationships() lines with delta>0 (:214-218).
- `relationship_change_negative_9slice.png` - Relationships() lines with delta<=0.
- `highlight_thumbnail_frame_9slice.png` - Nothing to host it: no ceremony frames are captured. The only live picture is the LiveFeed RenderTexture (Runtime/Presentation/LiveFeed.cs:25/:85).

Files in `Assets/Gamesim/Resources/Packs/Pack3_Systems/SeasonRecap/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `highlight_thumbnail_frame_9slice.png` | 536x316 | UiSliced | 32, 32, 32, 32 | 8, 8, 8, 8 |
| `recap_eviction_card_9slice.png` | 376x256 | UiSliced | 33, 33, 33, 33 | 8, 8, 8, 8 |
| `recap_hoh_card_9slice.png` | 440x320 | UiSliced | 83, 82, 83, 82 | 40, 40, 40, 40 |
| `recap_nominee_card_9slice.png` | 376x256 | UiSliced | 33, 33, 33, 33 | 8, 8, 8, 8 |
| `recap_veto_card_9slice.png` | 376x256 | UiSliced | 33, 33, 33, 33 | 8, 8, 8, 8 |
| `relationship_change_negative_9slice.png` | 276x88 | UiSliced | 26, 26, 26, 26 | 8, 8, 8, 8 |
| `relationship_change_positive_9slice.png` | 276x88 | UiSliced | 26, 26, 26, 26 | 8, 8, 8, 8 |
| `week_recap_shell_9slice.png` | 1376x836 | UiSliced | 42, 42, 42, 42 | 8, 8, 8, 8 |

## Pack 4 - Pack4_Presentation

Presentation layer: Diary Room, cinematic ceremonies, schedule, memory wall, Final Three, social alerts, transitions, broadcast overlays.

### BroadcastOverlays

**Destination** (partly exists): There is no broadcast-overlay canvas. live_corner_badge restyles the 'Live dot' Disc (EpisodeHud.cs:596) in EpisodeHud.LiveFeedCard() (:590); SetLiveFeedPaused() (:616) recolours that dot Conflict/Muted. camera_label goes behind the feed caption (:605), whose text is EpisodeDirector.RoomCaption(), e.g. "KITCHEN · 2 HOUSEGUESTS" (EpisodeDirector.LiveFeed.cs:105), or the witnessed-conversation line (:91). week_day_bug goes on the WEEK cell of EpisodeHud.HousePill() (EpisodeHud.Chrome.cs:149/155) or the Objective 'Phase bug' (Chrome.cs:128). breaking_event_banner and lower_third_nameplate have no home: they need a new overlay that takes no clicks, built like CeremonySting.Attach (CeremonySting.cs:79, a scene-root canvas at sortingOrder 90).

*Evidence:* Runtime/Episode/EpisodeHud.cs:590-620 (LiveFeedCard, 'Live dot', 'Picture' :599, caption :605, 'LIVE FEED'/'FEED PAUSED'); Runtime/Episode/EpisodeHud.Chrome.cs:128 ('Phase bug'), :149-155 (HousePill, StatCell 'calendar' "WEEK "+week); Runtime/Episode/EpisodeHud.cs:266-276 ('Status' with 'Caption rule'; the comment at :271 calls it the lower third); Runtime/Episode/EpisodeDirector.LiveFeed.cs:19 (LiveFeedCardName "Live feed"), :91, :105; Runtime/Presentation/CeremonySting.cs:79-95; Runtime/Episode/EpisodeHud.Activities.cs:89 (Live feed hidden in every activity layout)

*Caution:* 'House pill', 'Live feed' and 'Status' are named fixed chrome that AssertFixedChromeDoesNotOverlap (Tests/PlayMode/EpisodePlayModeTests.Accessibility.cs:112, names at :119) looks up by name, so skin them in place without renaming, moving or growing them. The Live feed card shows only during exploration and is hidden in the Diary, Conversation and every other activity layout (Activities.cs:89). The guide wants broadcast overlays during reveals and confessionals and off normal navigation, so a badge on that card restyles standing chrome; it is not the guide's use. Three sources compete for the LIVE badge: this one, Pack 2 LiveFeed/live_badge_9slice and Pack 5 Broadcast/live_card. Pack 5 also ships lower_third_blank_9slice, camera_identifier_blank_9slice, week_card and day_card. Pick one source per role.

- `icon_camera.png` - A Simple icon. It would replace the 'camera' glyph in CardHeading(card,"LIVE FEED","camera") (EpisodeHud.cs:595), which today is the IconForge sprite Resources/GamesimIcons/camera.png loaded through UiTheme.Icon (UiTheme.cs:262). UiTheme.Icon cannot reach Resources/Packs, so this needs a second loader. It is the third camera in Pack 4, with Icons_PNG/camera.png and DiaryRoom/icon_diary_camera.png.
- `lower_third_nameplate_9slice.png` - No nameplate exists. Candidates are the diary interview shot and the conversation two-shot. The diary set is 'Diary interview backdrop', which DiarySeatPose.BuildStudio() (Runtime/House/DiarySeatPose.cs:144/148) creates at runtime during the visit; it is not saved in EpisodeHouse.unity. A nameplate must not cover the seated face: EpisodePlayModeTests.Shots.cs:166 asserts the face stays between 0.3 and 0.7 of the viewport.
- `breaking_event_banner_9slice.png` - The nearest is the CeremonySting 'Card' strip (CeremonySting.cs:176, Build :172), but it only plays for nomination, veto, eviction and winner (IsCeremony :98). A banner for house events or crises would be a new overlay kind.
- `week_day_bug_9slice.png` - The simulation has no day: EpisodeState has only week (Simulation/EpisodeState.cs:201) and phase (:202). A DAY field would be invented copy, so use the bug as WEEK N · phase.

Files in `Assets/Gamesim/Resources/Packs/Pack4_Presentation/BroadcastOverlays/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `breaking_event_banner_9slice.png` | 1280x200 | UiSliced | 80, 79, 80, 79 | 40, 40, 40, 40 |
| `camera_label_9slice.png` | 336x88 | UiSliced | 24, 24, 24, 24 | 8, 8, 8, 8 |
| `icon_camera.png` | 180x180 | UiSprite |  |  |
| `live_corner_badge_9slice.png` | 236x86 | UiSliced | 26, 26, 26, 26 | 8, 8, 8, 8 |
| `lower_third_nameplate_9slice.png` | 776x126 | UiSliced | 28, 28, 28, 28 | 8, 8, 8, 8 |
| `week_day_bug_9slice.png` | 316x98 | UiSliced | 26, 26, 26, 26 | 8, 8, 8, 8 |

### CeremonyCinematics

**Destination** (partly exists): These go on the ceremony overlays, which are scene-root canvases the director dispatches at EpisodeDirector.cs:664-698: CeremonySting (the strip, CeremonySting.cs:172 Build, :150 Tint), CeremonyTakeover (the title card, CeremonyTakeover.cs:250 Build, :389 Faces), KeyCeremony (nominations, KeyCeremony.cs:187 Build) and VoteReveal (eviction, VoteReveal.cs:212 Build). vote_reveal_strip goes to VoteReveal 'Dots' (:298) and 'Progress' (:313). vote_chip_evict goes to each 'Pip' (:306), which Tally() (:175) tints Danger per revealed ballot. ceremony_result_banner goes to VoteReveal 'Result banner' (:316) and the KeyCeremony 'Nominated' chip (KeyCeremony.cs:329). veto_lower_third goes to the sting's gold VetoKind card (CeremonySting.cs:155), which does play because veto is never 'revealed'.

*Evidence:* Runtime/Episode/EpisodeDirector.cs:657-661 (veto-selection takeover), :664-698 (ceremony dispatch; takeover skipped when revealed :690; sting only when !revealed :694); Runtime/Presentation/CeremonySting.cs:32-35,98,150-160,172-190; CeremonyTakeover.cs:81 (sort 100),136-147 (IconFor),250-312,389-473; VoteReveal.cs:94 (sort 110),175-210,212-334; KeyCeremony.cs:78 (sort 110),187-349; CompetitionResult.cs:42-44 (GraphicRaycaster, sort 105),91-152; Runtime/Episode/EpisodeDirector.Ceremony.cs:29-63 (CeremonySubjects badges), :179-215 (ReactToCeremony), :240 (React)

*Caution:* CeremonyGlassPlayModeTests.AssertGlass (Tests/PlayMode/CeremonyGlassPlayModeTests.cs:143-166) pins the colour of 'Card' (the sting) and 'Card glass' (takeover, reveal, keys and CompetitionResult) to UiTheme.GlassFill (±0.005), and requires 'Border' and 'Glow' children with offsets of ±GlowWidth (10). UiThemeGlassPlayModeTests.cs:52-67 pins the sprite names 'UiTheme Fill/Glow/Outline 14'. Add pack sprites as non-raycasting children and do not re-sprite UiTheme.Glass. AssertNothingTakesAClick (:172) applies to the sting, takeover, reveal and keys, but not CompetitionResult, which has a raycaster. No KeyCeremony decoration may start with 'Key ' (the slot count, KeyCeremony.cs:345). The sting 'Card' rect is checked against chrome (EpisodePlayModeTests.Ceremonies.cs:283). Sort orders: sting 90, takeover 100, CompetitionResult 105, KeyCeremony and VoteReveal 110. Overlaps: Pack 2 Ceremony has ceremony_banner and replacement_nominee_frame, and Pack 3 CompetitionHUD has results_winner_banner.

- `nomination_lower_third_9slice.png / eviction_lower_third_9slice.png` - CeremonySting has both kinds, but the sting is skipped whenever KeyCeremony or VoteReveal plays (EpisodeDirector.cs:694 '!revealed'), so a sting-only skin shows only in the fallback path. The practical homes are the KeyCeremony 'HoH' line (KeyCeremony.cs:229) and 'Nominated' chip (:329), and the VoteReveal 'Title' (VoteReveal.cs:248) and 'Result banner' (:316).
- `hoh_lower_third_9slice.png` - No HoH strip exists: CeremonySting has no HoH kind (:32-35), and 'competition' is not IsCeremony. The HoH win is shown by CompetitionResult.Build() (CompetitionResult.cs:91), whose 'Winner' label (:110) sits on an input-taking card with a 'Continue from competition results' button (:144). Either skin that winner row or add a new sting kind.
- `replacement_nominee_banner_9slice.png` - Not shown anywhere. The veto takeover badges every current nominee 'NOMINATED' (EpisodeDirector.Ceremony.cs:45-48). The replacement is only worked out inside ReactToCeremony (:191-197), so CeremonySubjects would need wasNominated passed in and a new 'REPLACEMENT' badge. This duplicates Pack 2 Ceremony/replacement_nominee_frame_9slice.
- `reaction_positive/negative/neutral/shock_9slice.png` - No UI reaction frame exists. Reactions are 3D body clips: CharacterPresentation.Reaction {Nominated, Saved, Evicted, Won, Cheered} (CharacterPresentation.cs:30), called via EpisodeDirector.Ceremony.cs:240. Positive maps to Saved/Won/Cheered and negative to Nominated/Evicted; neutral and shock have no source. The nearest UI is CeremonyTakeover.Faces() 'Ring' (:414) and 'Subject badge' (:458), but those portraits are circular masked Discs, so rectangular 9-slices will not fit them.
- `vote_chip_keep_9slice.png` - Every ballot is a vote to evict, and the Pips show reveal progress, not the target. 'Keep' could only mean a ballot on the surviving nominee's side after Result() (VoteReveal.cs:196). Ballot.VoterName exists (:39) but is never drawn, so per-voter chips would be new work.

Files in `Assets/Gamesim/Resources/Packs/Pack4_Presentation/CeremonyCinematics/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `ceremony_result_banner_9slice.png` | 1180x260 | UiSliced | 80, 79, 80, 79 | 40, 40, 40, 40 |
| `eviction_lower_third_9slice.png` | 1000x210 | UiSliced | 80, 79, 80, 79 | 40, 40, 40, 40 |
| `hoh_lower_third_9slice.png` | 1000x210 | UiSliced | 80, 79, 80, 79 | 40, 40, 40, 40 |
| `nomination_lower_third_9slice.png` | 1000x210 | UiSliced | 80, 79, 80, 79 | 40, 40, 40, 40 |
| `reaction_negative_9slice.png` | 296x376 | UiSliced | 33, 33, 33, 33 | 8, 8, 8, 8 |
| `reaction_neutral_9slice.png` | 296x376 | UiSliced | 33, 33, 33, 33 | 8, 8, 8, 8 |
| `reaction_positive_9slice.png` | 296x376 | UiSliced | 33, 33, 33, 33 | 8, 8, 8, 8 |
| `reaction_shock_9slice.png` | 360x440 | UiSliced | 83, 82, 83, 82 | 40, 40, 40, 40 |
| `replacement_nominee_banner_9slice.png` | 936x146 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `veto_lower_third_9slice.png` | 1000x210 | UiSliced | 80, 79, 80, 79 | 40, 40, 40, 40 |
| `vote_chip_evict_9slice.png` | 236x86 | UiSliced | 28, 28, 28, 28 | 8, 8, 8, 8 |
| `vote_chip_keep_9slice.png` | 236x86 | UiSliced | 28, 28, 28, 28 | 8, 8, 8, 8 |
| `vote_reveal_strip_9slice.png` | 1280x240 | UiSliced | 83, 82, 83, 82 | 40, 40, 40, 40 |

### DiaryRoom

**Destination** (partly exists): diary_shell goes on the docked 'Episode panel' modal (EpisodeHud.cs:296). EpisodeDirector.RenderDiary() (EpisodeDirector.DiaryRoom.cs:249) calls SetActivityLayout(Diary) (:251), which re-anchors it into a 600-wide right-hand column and hides the Live feed, Recent events, cast strip and other chrome (EpisodeHud.Activities.cs:62-98). diary_choice_card goes on the hud.Action() rows (EpisodeHud.cs:879), used for reflections (DiaryRoom.cs:333), study (:315-316), the confirm/cancel rows (:266-268) and pending decisions (RenderPlayerDecision, :446, called at :280). diary_choice_selected is the hover/focus step, which today is HudEmphasis.Promote recolouring the row's 'Border' child (Runtime/Presentation/HudMotion.cs:51). The 3D set behind it is built at runtime by DiarySeatPose.BuildStudio() (Runtime/House/DiarySeatPose.cs:144) as 'Diary interview backdrop'.

*Evidence:* Runtime/Episode/EpisodeDirector.DiaryRoom.cs:249-289 (RenderDiary: PanelTitle :252, headings, memories as Paragraphs :288), :291-297 (record), :300-317 (study), :319-337 (reflection + Tag); Runtime/Episode/EpisodeHud.Activities.cs:11,62-107; Runtime/Episode/EpisodeHud.cs:30-43 (diary/study caption constants), :836-887 (Tag/Action; the row GameObject is named by its caption, :881); Runtime/House/DiarySeatPose.cs:85,144-165; Tests/PlayMode/EpisodePlayModeTests.Shots.cs:166-172

*Caution:* Chrome() and Action() are shared by every panel, so skin them only when CurrentActivityLayout == Diary (Activities.cs:24). The Shots test asserts that the 'Episode panel' rect does not contain the seated face and that 'Diary interview backdrop' is active (:170-172). Captions are the test and screen-reader contract (EpisodeHud.cs:30-43), and the exposure badge must stay a Tag chip (DiaryRoom.cs:330-334). HudEmphasis only recolours 'Border'; a selected-sprite swap needs a HudEmphasis change. Purple is UiTheme.Strategic (A56BFF, UiTheme.cs:74), contrast-tested on glass (Tests/EditMode/UiThemeContrastTests.cs:115).

- `diary_confessional_banner_9slice.png` - Today this is hud.PanelTitle("PRIVATE DIARY ROOM", …) (DiaryRoom.cs:252): a Heading and Paragraph that scroll with the content, not a fixed banner. A fixed banner child at the top of the Diary-layout modal is new work.
- `diary_journal_tab_active_9slice.png / diary_tab_inactive_9slice.png` - No tabs exist. The diary is one scrolling column of Headings: YOUR DIARY RECORD, STUDY THE HOUSE, POST-EVICTION REFLECTION, YOUR PENDING DECISION, YOUR PRIVATE REFLECTIONS. Tabs would have to be built, for example like SeasonReport's pill Chip controls (SeasonReport.cs:579, Chip :665).
- `diary_memory_card_9slice.png` - Memories render as hud.Paragraph("Week N: …") (DiaryRoom.cs:288); the notebook Story section does the same (EpisodeDirector.cs:~900). A card needs a new HUD content primitive.
- `icon_diary_camera.png / icon_diary_mic.png` - No diary glyph exists; IconForge has 'camera' but no mic. Candidates are child decoration beside the DiaryTravelCaption button 'Go to diary room [R]' (EpisodeHud.Chrome.cs:121/141) or the new banner. Both icons duplicate Icons_PNG camera.png and mic.png.

Files in `Assets/Gamesim/Resources/Packs/Pack4_Presentation/DiaryRoom/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `diary_choice_card_9slice.png` | 536x166 | UiSliced | 32, 32, 32, 32 | 8, 8, 8, 8 |
| `diary_choice_selected_9slice.png` | 600x230 | UiSliced | 81, 80, 81, 80 | 40, 40, 40, 40 |
| `diary_confessional_banner_9slice.png` | 940x190 | UiSliced | 80, 79, 80, 79 | 40, 40, 40, 40 |
| `diary_journal_tab_active_9slice.png` | 256x86 | UiSliced | 28, 28, 28, 28 | 8, 8, 8, 8 |
| `diary_memory_card_9slice.png` | 876x166 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `diary_shell_9slice.png` | 1296x796 | UiSliced | 41, 41, 41, 41 | 8, 8, 8, 8 |
| `diary_tab_inactive_9slice.png` | 256x86 | UiSliced | 28, 28, 28, 28 | 8, 8, 8, 8 |
| `icon_diary_camera.png` | 180x180 | UiSprite |  |  |
| `icon_diary_mic.png` | 180x180 | UiSprite |  |  |

### FinalThree

**Destination** (partly exists): No Final Three screen exists. The only final-three moment is the FinalEviction branch of the phase panel: when the player won the final HoH, it renders a Paragraph and one 'Evict <name>' hud.ActionFor row per remaining houseguest (EpisodeDirector.cs:1141-1145; ActionFor at EpisodeHud.cs:941) in the standard 900x300 docked 'Episode panel' (ModalWidth/Height, EpisodeHud.cs:338-339). That is where final_decision_panel, final_three_shell and final_choice_cut would skin. The final HoH parts go through CompetitionResult, titled 'Final HoH · Part N' by AwardTitle (EpisodeDirector.Ceremony.cs:78-80). A real presentation with finalist slots and a cut/keep choice needs a new overlay or activity layout.

*Evidence:* Runtime/Episode/EpisodeDirector.cs:1140-1145 (RenderPlayerDecision does not handle FinalEviction, then the FinalEvict rows), :1240 (PhaseTitle 'CHOOSE YOUR FINAL TWO'), :704 (weekly recap queued only for EvictionKind); Runtime/Episode/EpisodeHud.cs:941-946 (ActionFor), :1322-1329 (PhaseTint FinalEviction = Danger); Runtime/Presentation/CeremonySting.cs:98 (IsCeremony excludes 'final-eviction'); Simulation/EpisodeEngine.cs:672 (Log 'final-eviction'); Runtime/Episode/EpisodeHud.Chrome.cs:360; Runtime/Presentation/SeasonReport.cs:231-275 (Winner/Finalist, final two only)

*Caution:* The 'Evict ' + name captions are pinned (Tests/PlayMode/EpisodePlayModeTests.cs:686, EpisodePlayModeTests.Keyboard.cs:308, Runtime/Episode/PortVerification.Season.cs:237), so skin the rows and do not rename them. FinalEvict commits at once with no confirm step, so a 'keep' control or a confirming decision panel would change the command flow. When an NPC holds the final HoH, no overlay presents the cut: 'final-eviction' is not a sting or takeover kind and does not queue the weekly recap. It surfaces only as a 'target' row in the Recent events card and in the notebook's WeeklyRecap data. Pack 3 FinaleJury (finalist_card, winner_card) competes for the same SeasonReport slots.

- `finalist_slot_9slice.png / finalist_slot_selected_9slice.png` - The nearest is SeasonReport.Finalist() (SeasonReport.cs:256), called twice by Winner() (:231) for the winner and runner-up after the jury, not for three finalists. Its portraits are circular (HudPrimitives.Portrait, HudPrimitives.cs:278).
- `final_choice_keep_9slice.png` - No keep control exists. Keeping is implied: whoever is not evicted goes to the final two.
- `icon_final_three.png` - The glyph for FinalEviction. EventGlyph maps 'final-eviction' to 'target' (EpisodeHud.Chrome.cs:360). It duplicates Icons_PNG/final3.png.
- `memory_wall_thumbnail_frame_9slice.png` - The guide lists it under FinalThree (inventory :444), so the pack did not misfile it, but no finale screen shows memory-wall thumbnails. Its plausible uses are a finale cast retrospective (nearest: SeasonReport.Cast, SeasonReport.cs:534) or the not-yet-built 2D memory wall. It has no current consumer.

Files in `Assets/Gamesim/Resources/Packs/Pack4_Presentation/FinalThree/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `final_choice_cut_9slice.png` | 436x136 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `final_choice_keep_9slice.png` | 436x136 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `final_decision_panel_9slice.png` | 1000x400 | UiSliced | 83, 82, 83, 82 | 40, 40, 40, 40 |
| `final_three_shell_9slice.png` | 1396x836 | UiSliced | 42, 42, 42, 42 | 8, 8, 8, 8 |
| `finalist_slot_9slice.png` | 440x580 | UiSliced | 85, 84, 85, 84 | 40, 40, 40, 40 |
| `finalist_slot_selected_9slice.png` | 440x580 | UiSliced | 85, 84, 85, 84 | 40, 40, 40, 40 |
| `icon_final_three.png` | 180x180 | UiSprite |  |  |
| `memory_wall_thumbnail_frame_9slice.png` | 836x256 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |

### Icons_PNG

**Destination** (partly exists): The icon system is UiTheme.Icon(name) (Runtime/Presentation/UiTheme.cs:262), which loads only Resources/GamesimIcons/<name>. Those sprites are white-on-transparent and generated by IconForge.Generate() (Editor/IconForge.cs:32, menu 'Gamesim/U07/Generate HUD icons'); callers tint them through HudPrimitives.Glyph (HudPrimitives.cs:69). Pack icons in Resources/Packs/Pack4_Presentation/Icons_PNG are unreachable without a second loader path. calendar and camera already exist in GamesimIcons; final3, fire, memory, mic, spark and vote are new names.

*Evidence:* Runtime/Presentation/UiTheme.cs:254-272; Editor/IconForge.cs:26-52 (Folder = Resources/GamesimIcons); Resources/GamesimIcons (calendar, camera, chat, gavel, journal, target, evicted and others; no mic, vote, memory, fire or spark); Runtime/Episode/EpisodeHud.Chrome.cs:155 ('calendar'), :337 ('Event mark'), :356-370 (EventGlyph); Runtime/Episode/EpisodeHud.cs:595 ('camera'); Runtime/Presentation/IconRail.cs:128-182 (drawn marks); Runtime/Presentation/CeremonyTakeover.cs:136-147 (IconFor)

*Caution:* calendar.png and camera.png collide by name with GamesimIcons; a copy placed there would be overwritten the next time IconForge runs. Pack icons that ship coloured will be multiplied by the caller's tint (Gold, Accent, Muted). No test pins icon sprites, but every caller must cope with null. Pack 2 Icons_PNG also ships calendar.png and vote.png, and ScheduleTimeline, MemoryWall, FinalThree and BroadcastOverlays each carry a duplicate icon.

- `calendar.png` - Replaces the HousePill WEEK cell glyph (EpisodeHud.Chrome.cs:155). It collides with IconForge 'calendar', Pack 2 Icons_PNG/calendar.png and ScheduleTimeline/icon_calendar.png.
- `camera.png` - Replaces the LIVE FEED heading glyph (EpisodeHud.cs:595). It collides with IconForge 'camera' and duplicates BroadcastOverlays/icon_camera.png.
- `vote.png` - Would replace IconRail.Draw(Mark.Votes), a tally drawn from three bars (IconRail.cs:152-157), and the EventGlyph 'gavel' for eviction/vote-reveal/private-vote (Chrome.cs:361). It duplicates Pack 2 Icons_PNG/vote.png.
- `final3.png` - For EventGlyph 'final-eviction' (Chrome.cs:360, currently 'target') and the FinalEviction phase. It duplicates FinalThree/icon_final_three.png.
- `mic.png` - No mic glyph exists. Candidates are the diary and the 'eviction-speech'/'final-speech' EventGlyph, currently 'chat' (Chrome.cs:367).
- `memory.png` - No memory glyph exists. RelationshipWeb memory rows use 'journal'/'eye' (RelationshipWeb.cs:749). It duplicates MemoryWall/icon_memory.png.
- `fire.png / spark.png` - No consumer. Plausible for the house-event kinds crisis and emergent (Simulation/HouseEventState.cs:116-119) if SocialEvents alerts are built.

Files in `Assets/Gamesim/Resources/Packs/Pack4_Presentation/Icons_PNG/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `calendar.png` | 128x128 | UiSprite |  |  |
| `camera.png` | 128x128 | UiSprite |  |  |
| `final3.png` | 128x128 | UiSprite |  |  |
| `fire.png` | 128x128 | UiSprite |  |  |
| `memory.png` | 128x128 | UiSprite |  |  |
| `mic.png` | 128x128 | UiSprite |  |  |
| `spark.png` | 128x128 | UiSprite |  |  |
| `vote.png` | 128x128 | UiSprite |  |  |

### MemoryWall

**Destination** (partly exists): The memory wall that exists is 3D. 'House Architecture/Memory Wall' in EpisodeHouse.unity holds 'Memory frame 00'-'15', each with a 'Border' renderer (the authored bb_set_memorywall frame, emissive Neon Gold) and a 'Portrait' quad. MemoryWall.Refresh() (Runtime/Presentation/MemoryWall.cs:45) sets each one lit or dimmed, Active versus not, with no winner or finalist state. Editor/MemoryWallBuilder.Build() (:43) builds them. These 2D frames have no in-season screen. The closest 2D consumer is SeasonReport.Cast() (SeasonReport.cs:534), whose status rows already dim evicted portraits and ring-tint by PlacementTint (:766: Winner Gold, RunnerUp Accent, Jury Warning, Evicted Muted), but only at season end. An in-season wall would be a new notebook block, e.g. beside RenderNotebookPeople (EpisodeDirector.cs:822).

*Evidence:* Assets/Gamesim/Scenes/EpisodeHouse.unity ('Memory Wall', 'Memory frame 00'..'15', 16 'Border', 16 'Portrait', 'Authored wall'); Runtime/Presentation/MemoryWall.cs:28-30,45-89; Editor/MemoryWallBuilder.cs:28-30,43,129-176; Runtime/Presentation/HudPrimitives.cs:278-331 (Portrait, dim flag); Runtime/Presentation/SeasonReport.cs:534-575,766-776; Runtime/Episode/EpisodeDirector.cs:822-833; Runtime/Episode/EpisodeHud.cs:243-251 (IconRail entries)

*Caution:* Do not texture the 3D frames with these sprites. Tests/EditMode/EpisodeHouseMemoryWallTests.cs:32-44 needs 'Border' and 'Portrait' children with renderers. EpisodePlayModeTests.MemoryWall.cs:49 asserts each portrait texture Is.SameAs CharacterPortraits.Get(actor), and :87 asserts evicted brightness is below lit via the _BaseColor grayscale (helper at :359-367). Every 2D portrait is a circular masked Disc (HudPrimitives.Portrait), so rectangular frames need a new rectangular portrait primitive. A new IconRail entry changes the tested 'Icon rail' chrome (Accessibility.cs:119) and the ActiveMarkName check (MemoryWall.cs:166). The evicted cast strip belongs to Pack 2 CharacterStates on CastRail; do not double-skin it. Pack 5 has memory_wall_header and display_memory_wall for the in-world wall.

- `evicted_overlay_x.png` - A Simple sprite over a dimmed HudPrimitives.Portrait (dim=true, HudPrimitives.cs:279), e.g. SeasonReport.Cast's Evicted rows (:552-553). IconForge already ships an 'evicted' glyph, used by the takeover (CeremonyTakeover.cs:142).
- `memory_portrait_finalist_9slice.png / memory_portrait_winner_9slice.png` - Only SeasonReport distinguishes Winner and RunnerUp (PlacementTint :766). The 3D MemoryWall has two states, Lit and Dimmed.
- `icon_memory.png` - For a new rail entry or card heading. It duplicates Icons_PNG/memory.png.
- `memory_wall_shell_9slice.png` - The shell of the not-yet-built 2D screen: either the docked 'Episode panel' or a WeeklyRecapScreen-style canvas parented to the director (WeeklyRecapScreen.Attach, :64-69).

Files in `Assets/Gamesim/Resources/Packs/Pack4_Presentation/MemoryWall/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `evicted_overlay_x.png` | 360x420 | UiSprite |  |  |
| `icon_memory.png` | 180x180 | UiSprite |  |  |
| `memory_portrait_active_9slice.png` | 336x396 | UiSliced | 33, 33, 33, 33 | 8, 8, 8, 8 |
| `memory_portrait_evicted_9slice.png` | 336x396 | UiSliced | 33, 33, 33, 33 | 8, 8, 8, 8 |
| `memory_portrait_finalist_9slice.png` | 400x460 | UiSliced | 83, 82, 83, 82 | 40, 40, 40, 40 |
| `memory_portrait_winner_9slice.png` | 400x460 | UiSliced | 83, 82, 83, 82 | 40, 40, 40, 40 |
| `memory_wall_shell_9slice.png` | 1396x836 | UiSliced | 42, 42, 42, 42 | 8, 8, 8, 8 |

### ScheduleTimeline

**Destination** (partly exists): No schedule or timeline screen exists, and the sim has no days or clock: EpisodeState holds only week and phase (Simulation/EpisodeState.cs:201-202), with the EpisodePhase enum at :7-12 and eviction-night sub-steps in EvictionStage (:24). Beat progression already renders as text: the notebook Story section, RenderNotebookStory() (EpisodeDirector.cs:864), lists 'WEEKS SO FAR' (:872) as a headline plus a 'Read the week N recap' row per closed week; WeeklyRecapScreen.Ceremony() (WeeklyRecapScreen.cs:189) lists the week's HoH, Nominees, Veto, Veto used and Evicted; the current beat shows as PhaseBand (EpisodeHud.cs:1357) and the Objective 'Phase bug' (EpisodeHud.Chrome.cs:128). A timeline built from the phase enum, with PhaseTitle (EpisodeDirector.cs:1229) and PhaseTint (EpisodeHud.cs:1322), fits best as a new block at the top of RenderNotebookStory, with schedule_event_card skinning the existing week rows.

*Evidence:* Simulation/EpisodeState.cs:7-12,24,195-202; Runtime/Episode/EpisodeDirector.cs:864-900 (RenderNotebookStory), :1229-1246 (PhaseTitle); Runtime/Episode/EpisodeHud.cs:73 (ReviewWeekCaption), :1322-1374 (PhaseTint, PhaseBand); Runtime/Episode/EpisodeHud.Chrome.cs:126-134; Runtime/Presentation/WeeklyRecapScreen.cs:189-199; Runtime/Presentation/IconRail.cs:41 (Mark.Story)

*Caution:* The guide's event classes (power gold, danger red, social green, current cyan) disagree with PhaseTint: HoH and the final HoH parts are Accent, Veto is AccentDeep, VetoMeeting is PositiveDeep and only Finished is Gold. Pick one. A new IconRail entry changes tested fixed chrome ('Icon rail' in the overlap suite; IconRail.ActiveMarkName in EpisodePlayModeTests.MemoryWall.cs:166), which is why the Story section is the safer host. ReviewWeekCaption is a caption contract. Inventing days would add saved state and need a schema version. Pack 3 NotebookIntel/timeline_event_9slice and SeasonRecap cards compete for the same rows.

- `schedule_day_header_9slice.png` - There are no days, so use it as the per-week header (WEEK N) above the 'WEEKS SO FAR' rows.
- `timeline_rail.png` - A Simple vertical rail for the new timeline block. It has no current equivalent.
- `icon_calendar.png` - Duplicates Icons_PNG/calendar.png, Pack 2 Icons_PNG/calendar.png and IconForge 'calendar' (the HousePill week cell).
- `schedule_event_power/danger/social/active_9slice.png` - One per phase class; 'active' marks state.phase. This needs a phase-to-class mapping rule, since PhaseTint does not match the guide.

Files in `Assets/Gamesim/Resources/Packs/Pack4_Presentation/ScheduleTimeline/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `icon_calendar.png` | 180x180 | UiSprite |  |  |
| `schedule_day_header_9slice.png` | 536x116 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `schedule_event_active_9slice.png` | 800x210 | UiSliced | 80, 79, 80, 79 | 40, 40, 40, 40 |
| `schedule_event_card_9slice.png` | 736x146 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `schedule_event_danger_9slice.png` | 736x146 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `schedule_event_power_9slice.png` | 736x146 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `schedule_event_social_9slice.png` | 736x146 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `schedule_shell_9slice.png` | 1336x776 | UiSliced | 41, 41, 41, 41 | 8, 8, 8, 8 |
| `timeline_rail.png` | 140x860 | UiSprite |  |  |

### SocialEvents

**Destination** (partly exists): No alert card exists. House events (HouseEventState, Simulation/HouseEventState.cs:20) render inline in the Social phase panel via EpisodeDirector.PendingHouseEvent() (EpisodeDirector.Conversation.cs:488, called at EpisodeDirector.cs:1174) as a Heading, the narrative, one ActionFor row per choice and a RiskTag. The nearest edge-of-HUD feed is the 'Recent events' card, EpisodeHud.RecentEventsCard() (EpisodeHud.Chrome.cs:309) in RightColumn (:290), which already carries 'house-ambient' narration (Simulation/EpisodeEngine.cs:1160) under the default 'journal' glyph. NPC drama elsewhere surfaces through HouseConversationCaption ('Witnessed generic topic', Runtime/Episode/HouseConversationCaption.cs:27, canvas sort 69) and the live-feed caption (EpisodeDirector.LiveFeed.cs:66). A one-at-a-time alert card would be a new RightColumn element stacked with the Live feed and Recent events cards.

*Evidence:* Runtime/Episode/EpisodeDirector.Conversation.cs:476-503; Simulation/HouseEventState.cs:20-40 (no room field), :100-120 (six kinds: phase/house/proximity/ambient/emergent/crisis; Asks() excludes ambient :131); Simulation/HouseEventSources.cs:48-66,101,110 ({LOCATION} substituted into narrative); Simulation/EpisodeEngine.cs:1147-1161 (NarrateHouse); Runtime/Episode/EpisodeHud.Chrome.cs:290-296,309-350,356-370; Runtime/Episode/EpisodeDirector.NpcSocial.cs:306; Runtime/Episode/EpisodeHud.Activities.cs:28 (WorldCaptionSafeBounds), :89-90 (right-column cards hidden in activities)

*Caution:* The event kinds are phase/house/proximity/ambient/emergent/crisis, not drama/romance/secret/social, so alert colour needs a mapping rule. The room is baked into the narrative string, so location_chip needs a new saved field, with a schema version and migration. A new right-column card must pass AssertFixedChromeDoesNotOverlap (Accessibility.cs:112), be accounted for in WorldCaptionSafeBounds, and be hidden by SetActivityLayout like its siblings. Pack 2 LiveFeed (toast_notification, event_row, event_row_urgent) competes for the same role.

- `dismiss_button_9slice.png` - This contradicts the documented design for house events: "There is no way to dismiss one" (EpisodeDirector.Conversation.cs:483). It fits only ambient narration, which asks nothing, or alerts about NPC conversations. It is a new caption contract.
- `investigate_button_9slice.png` - No investigate action exists. It can be built without new state from HouseEventState.involvedIds and EpisodeDirector.FollowHouseguest(id) (EpisodeDirector.Camera.cs:51, which CastRail receives at EpisodeHud.cs:214), or by travel via player.TryMoveTo as GoToDiary does (DiaryRoom.cs:73/81). It is a new caption contract.
- `location_chip_9slice.png` - Needs the room as data, which is not saved today. RoomLabels.Title (Runtime/Presentation/RoomLabels.cs:35) formats room ids for display.
- `icon_drama.png` - An alert glyph with no existing equivalent. See also Icons_PNG fire and spark.

Files in `Assets/Gamesim/Resources/Packs/Pack4_Presentation/SocialEvents/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `dismiss_button_9slice.png` | 356x108 | UiSliced | 28, 28, 28, 28 | 8, 8, 8, 8 |
| `drama_alert_9slice.png` | 840x220 | UiSliced | 80, 79, 80, 79 | 40, 40, 40, 40 |
| `icon_drama.png` | 180x180 | UiSprite |  |  |
| `investigate_button_9slice.png` | 420x172 | UiSliced | 79, 78, 79, 78 | 40, 40, 40, 40 |
| `location_chip_9slice.png` | 276x78 | UiSliced | 24, 24, 24, 24 | 8, 8, 8, 8 |
| `romance_alert_9slice.png` | 776x156 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `secret_alert_9slice.png` | 776x156 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |
| `social_alert_9slice.png` | 776x156 | UiSliced | 30, 30, 30, 30 | 8, 8, 8, 8 |

### Transitions

**Destination** (partly exists): No transition layer exists. The existing full-screen pieces are OpeningSequence.Flashbulbs() (Runtime/Presentation/OpeningSequence.cs:502), a white 'Flash' Fill (:506) at 0.85 alpha fading over 0.22 s and skipped when Motionless (:245), which is the destination for white_flash_overlay; and the near-opaque 'Scrim' Fills on each overlay (CeremonyTakeover.cs:264, VoteReveal.cs:228, KeyCeremony.cs:204, CompetitionResult.cs:96, WeeklyRecapScreen.cs:142, OpeningSequence.Scrim :350). The cyan, gold, purple and red washes need a new scene-root overlay with no raycaster, built like CeremonySting.Attach and fired from the ceremony dispatch (EpisodeDirector.cs:664-698). Colour follows CeremonySting.Tint (:150): red for nomination and eviction, gold for veto and winner. Purple fits TryOpenDiary (DiaryRoom.cs:87); cyan fits a conversation opening (EpisodeHud.SpeakerTitle :407 / EpisodeHud.Radial.cs ConversationRadial :65).

*Evidence:* Runtime/Presentation/OpeningSequence.cs:227-257,350-369,501-523,572 (Motionless); Runtime/Presentation/CeremonyTakeover.cs:261-266; Runtime/Episode/EpisodeDirector.cs:664-698; Runtime/Episode/EpisodeDirector.CeremonyFraming.cs:44-73 (FrameCeremony, skipped under reducedMotion); Editor/HouseCinematicLighting.cs:370 (Grade), :393-394 (URP Vignette 0.28); Runtime/House/HouseCameraRig.cs:97 (CloseUpVolumeName)

*Caution:* Any wash must have no GraphicRaycaster and raycastTarget=false, as CeremonyGlassPlayModeTests.AssertNothingTakesAClick (:172) requires of the overlays it copies, and it must skip or hold under reducedMotion like its siblings. Sort orders taken: 50 HouseInteraction, 69 conversation caption, 70 HUD, 90 sting, 100 takeover, 104 CompetitionGameScreen, 105 CompetitionResult, 110 VoteReveal, KeyCeremony and WeeklyRecap, 120 SeasonReport, 125 CastSelect, 127 CharacterCreator, 130 MainMenu, 140 OpeningSequence and HouseTutorial. Do not leave a colour wash up while the HUD is in use.

- `cinematic_vignette.png` - Duplicates the URP Vignette in the house volume profile (HouseCinematicLighting.Grade, Editor/HouseCinematicLighting.cs:393) and the rig's 'Close-up Volume'. Use it only where the Volume cannot be driven, e.g. the diary shot or FrameCeremony.
- `broadcast_scanlines.png` - No equivalent. The natural place is over the live-feed 'Picture' RawImage (EpisodeHud.cs:599-604), not full screen.
- `white_flash_overlay.png` - Would replace the Fill sprite in OpeningSequence.Flashbulbs (:506). No test pins 'Flash' or 'Scrim'.

Files in `Assets/Gamesim/Resources/Packs/Pack4_Presentation/Transitions/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `broadcast_scanlines.png` | 1920x1080 | UiSprite |  |  |
| `cinematic_vignette.png` | 1920x1080 | UiSprite |  |  |
| `transition_cyan_overlay.png` | 1920x1080 | UiSprite |  |  |
| `transition_gold_overlay.png` | 1920x1080 | UiSprite |  |  |
| `transition_purple_overlay.png` | 1920x1080 | UiSprite |  |  |
| `transition_red_overlay.png` | 1920x1080 | UiSprite |  |  |
| `white_flash_overlay.png` | 1920x1080 | UiSprite |  |  |

## Pack 5 - Pack5_HouseBroadcast

House + broadcast branding: wall graphics, neon masks, digital displays, competition kit, signage, broadcast cards, surface decals, VFX textures.

### Broadcast

**Destination** (partly exists): Resources UI sprites (Sprite, loadable by path) for the code-built overlays: episode_opening_card -> OpeningSequence.Intro() (Runtime/Presentation/OpeningSequence.cs:227; it prints 'BIG BROTHER' / 'A NEW SEASON BEGINS' at :232-233). nomination_ceremony_card -> KeyCeremony.Play (KeyCeremony.cs:91, title :226), with the CeremonyTakeover fallback (EpisodeDirector.cs:685-688). veto_meeting_card -> the CeremonyTakeover VetoKind 'Veto Meeting' (CeremonyTakeover.cs:102; Build :250). eviction_night_card and live_card -> VoteReveal.Play (VoteReveal.cs:107, 'LIVE EVICTION' :249). finale_card -> the WinnerKind takeover ('The Winner', :104) and SeasonReport.Show (SeasonReport.cs:149). week_card -> WeeklyRecapScreen.Show (Runtime/Presentation/WeeklyRecapScreen.cs:103, 'WEEK n IS OVER' :171). previously_card -> only the notebook's 'PREVIOUSLY ON BIG BROTHER' eyebrow exists (EpisodeDirector.Journal.cs:121). lower_third_blank_9slice -> the EpisodeHud 'Status' lower third (EpisodeHud.cs:266, via Chrome() :1402). camera_identifier_blank_9slice -> the 'Live feed' card (EpisodeHud.LiveFeedCard :590; caption :605).

*Evidence:* Assets/Gamesim/Runtime/Episode/EpisodeDirector.cs:655-694 (veto-field takeover, voteReveal/keyCeremony/takeover/sting dispatch); Assets/Gamesim/Runtime/Presentation/CeremonyTakeover.cs:96-105 TitleFor, :177, :250, :268 'Card', :278 'Card glass'; KeyCeremony.cs:209 'Card', :226, :349 'Card glass'; VoteReveal.cs:233 'Card', :249, :334 'Card glass'; OpeningSequence.cs:227-247; SeasonReport.cs:214 'SEASON COMPLETE', :245 'WINNER'; WeeklyRecapScreen.cs:171; Assets/Gamesim/Runtime/Episode/EpisodeHud.cs:266-276, :590-606, :1402-1409; Assets/Gamesim/Editor/UiPackCatalogue.cs:442, :449 (borders 38/37/37/38); Assets/Gamesim/Tests/PlayMode/CeremonyGlassPlayModeTests.cs:52-128 (sting, takeover, reveal, keys, result), :143-165 AssertGlass, :177 raycastTarget false; EpisodePlayModeTests.Chrome.cs:54, :198-217; Assets/Gamesim/Runtime/Presentation/Localisation.cs:30

*Caution:* CeremonyGlassPlayModeTests pins each overlay's 'Card glass' (the sting's 'Card') to UiTheme.GlassFill with 'Border' and 'Glow' children, and requires no raycast targets. A card sprite therefore has to be an extra non-raycast child, never the ground. The cards are opaque full-bleed 1920x1080 images with their own dark ground and frame, so inside the card column they would hide the pinned glass. They fit better as a full-screen layer in place of the Scrim. The takeover, key ceremony, vote reveal and intro already print their own TMP titles where the art has its word baked in, so the text would appear twice, and the baked English bypasses Localisation.Text. The resting 'Status' edge colour and band geometry are pinned (Chrome tests :54, :198-217). There is no Day counter, and there is no 'later tonight' or replay beat.

- `day_card.png` - No day concept in the simulation or the HUD. No destination.
- `later_tonight_card.png / reaction_replay_card.png` - No such beats exist; they would need a teaser or replay segment.
- `week_card.png` - 'WEEK' is baked in the centre with no slot for the number. The number is dynamic, so an overlaid number collides with the art.
- `nomination_ceremony_card.png` - Defect, confirmed by eye: 'NOMINATION CEREMONY' runs past its red frame on both sides.
- `previously_card.png` - Only a notebook eyebrow exists (Journal.cs:121). A full-screen 'previously' interstitial would have to be built.
- `episode_opening_card.png` - The baked word is 'EPISODE', while Intro titles 'BIG BROTHER'. Use it as the ground only if the TMP title stays.
- `lower_third_blank_9slice.png / camera_identifier_blank_9slice.png` - 9-slice borders 38/37/37/38 (UiPackCatalogue.cs:442, :449; the metas match). They go into Chrome() panels, whose resting edge is pinned.

Files in `Assets/Gamesim/Resources/Packs/Pack5_HouseBroadcast/Broadcast/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `camera_identifier_blank_9slice.png` | 480x120 | UiSliced | 38, 37, 37, 38 | 14, 13, 13, 14 |
| `day_card.png` | 1920x1080 | UiSprite |  |  |
| `episode_opening_card.png` | 1920x1080 | UiSprite |  |  |
| `eviction_night_card.png` | 1920x1080 | UiSprite |  |  |
| `finale_card.png` | 1920x1080 | UiSprite |  |  |
| `later_tonight_card.png` | 1920x1080 | UiSprite |  |  |
| `live_card.png` | 1920x1080 | UiSprite |  |  |
| `lower_third_blank_9slice.png` | 1200x180 | UiSliced | 38, 37, 37, 38 | 14, 13, 13, 14 |
| `nomination_ceremony_card.png` | 1920x1080 | UiSprite |  |  |
| `previously_card.png` | 1920x1080 | UiSprite |  |  |
| `reaction_replay_card.png` | 1920x1080 | UiSprite |  |  |
| `veto_meeting_card.png` | 1920x1080 | UiSprite |  |  |
| `week_card.png` | 1920x1080 | UiSprite |  |  |

### CompetitionKit

**Destination** (partly exists): The yard course, built by editor scripts. HouseSetPieces.Course() (Editor/HouseSetPieces.cs:698) places three lit bb_set_comp_lane pieces (each 4.8 x 9 m) coloured Neon Red, Blue and Green left to right (:715). Each lane has a gate at its head, a stack and a crate, and bb_set_comp_backdrop stands behind all three. HouseSetPieces.Podiums() (:795) puts a bb_set_podium on each 'Competition podium 1-3' block and disables the block. Lane decals, start/finish/checkpoint markers and the hazard stripe become floor quads under 'Set Pieces/Competition course'. podium_*_front needs a decal quad on each podium's front_panel. Runtime arena: EpisodeDirector.BeginCompetitionArena (Runtime/Episode/EpisodeDirector.CompetitionArena.cs:27) draws flat discs of radius 0.425 m, one per contestant (AddCompetitionStationMarker :139; named 'Your competition station', 'HoH station' or 'Veto station'), sharing the runtime material 'Competition award accent' (:109). It also draws a TextMeshPro sign 'Award and discipline' (:116-123). The station symbols belong on the discs, and scoreboard_face goes behind the sign.

*Evidence:* Assets/Gamesim/Editor/HouseSetPieces.cs:698-760 Course, :715 lane colours, :727 backdrop, :734-736 signs, :795-835 Podiums. Scene: 'Set Pieces' > 'Competition course', 'Podiums' > 'Competition podium 1/2/3 (set)' at x -6/0/6, z 17. ArtSource/setpieces/bb_set_podium.py:31 (W 2.2, D 1.3, H 1.0), :36 front_panel (W*0.66 x H*0.34 = 1.45 x 0.34 m, neon_blue); bb_set_comp_lane.py:9. Assets/Gamesim/Runtime/Episode/EpisodeDirector.CompetitionArena.cs:106-123, :125-137 CreateCompetitionStationMesh (vertices and triangles only, no UVs), :139-146. Assets/Gamesim/Runtime/Presentation/CompetitionMiniGames.cs:98-104; CompetitionResult.cs:91 Build, :109 winner portrait. Assets/Gamesim/Simulation/WebEnduranceCompetition.cs:17, :55.

*Caution:* There are three podiums and three lanes, not four, so podium_D and lane_yellow have no slot. The pack's podium colours (A cyan, B green, C yellow, D red) do not match the course order (red, blue, green). The podium front panel is 1.45 x 0.34 m (4.3:1) against 512x512 art, and all three share bb_mat_neon_blue with world-scale UVs, so each front needs its own small decal quad rather than a material texture. The station disc mesh has no UVs, so it cannot show a texture until UVs are generated. The lanes are already lit 3D strips, so decals would draw them twice. The simulation records an endurance elimination order (WebEnduranceCompetition.cs:17), but no runtime code shows it, and checkpoints and rounds have no state at all. The static bb_set_sign_hoh reads 'HOH COMPETITION' even during veto competitions. Everything imports as WorldColour (Default, Clamp) under Art/Packs, so runtime use needs references passed from EpisodeDirector. A RawImage in CompetitionResult could draw a Default texture, but CompetitionResult is code-built and its 'Card glass' is pinned (CeremonyGlassPlayModeTests.cs:128).

- `balance_beam_symbol.png / buzzer_station_symbol.png / puzzle_station_symbol.png` - Runtime station discs, chosen by category: Endurance -> balance beam, Skill (Reaction) -> buzzer, Mental (Memory) -> puzzle. The disc mesh needs UVs first.
- `scoreboard_face_1920x1080.png` - Backing for the 'Award and discipline' TMP sign at 2.7 m above the yard floor on its back line (CompetitionArena.cs:116-123). It is the one blank display, so the dynamic text can sit on top.
- `hoh_crown_icon.png` - Byte-identical duplicate of EnvironmentWallGraphics/hoh_crown_wall.png. It could head the award sign, or be CompetitionResult's award glyph through a RawImage with a passed reference.
- `veto_medallion_icon.png` - A circle with a slash, which reads as 'prohibited'. Same destinations as the crown, for the veto.
- `winner_burst.png` - Behind the winner portrait in CompetitionResult.Build (CompetitionResult.cs:109). It has to be an extra non-raycast child, not the glass. Alternatively a floor decal under the winning podium.
- `round_complete_marker.png` - Defect, confirmed by eye: 'ROUND COMPLETE' runs past its green frame on both sides. No round state exists to show it.
- `eliminated_marker.png / checkpoint_marker.png` - No runtime state drives these. Start and finish can be static: finish at the gate end (deck.max.z - 0.7, :746), start at the podium end.
- `podium_A_front.png / podium_B_front.png / podium_C_front.png` - Square art on a 1.45 x 0.34 m panel. Use a 0.34 m decal centred on each front_panel, left to right in x order.
- `lane_yellow_decal.png / podium_D_front.png` - No fourth lane or podium exists.

Files in `Assets/Gamesim/Art/Packs/Pack5_HouseBroadcast/CompetitionKit/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `balance_beam_symbol.png` | 512x512 | WorldColour |  |  |
| `buzzer_station_symbol.png` | 512x512 | WorldColour |  |  |
| `checkpoint_marker.png` | 1024x512 | WorldColour |  |  |
| `eliminated_marker.png` | 1024x512 | WorldColour |  |  |
| `finish_marker.png` | 1024x512 | WorldColour |  |  |
| `hazard_stripe_decal.png` | 1600x320 | WorldColour |  |  |
| `hoh_crown_icon.png` | 1024x1024 | WorldColour |  |  |
| `lane_blue_decal.png` | 1600x320 | WorldColour |  |  |
| `lane_green_decal.png` | 1600x320 | WorldColour |  |  |
| `lane_red_decal.png` | 1600x320 | WorldColour |  |  |
| `lane_yellow_decal.png` | 1600x320 | WorldColour |  |  |
| `podium_A_front.png` | 512x512 | WorldColour |  |  |
| `podium_B_front.png` | 512x512 | WorldColour |  |  |
| `podium_C_front.png` | 512x512 | WorldColour |  |  |
| `podium_D_front.png` | 512x512 | WorldColour |  |  |
| `puzzle_station_symbol.png` | 512x512 | WorldColour |  |  |
| `round_complete_marker.png` | 1024x512 | WorldColour |  |  |
| `scoreboard_face_1920x1080.png` | 1920x1080 | WorldColour |  |  |
| `start_marker.png` | 1024x512 | WorldColour |  |  |
| `veto_medallion_icon.png` | 1024x1024 | WorldColour |  |  |
| `winner_burst.png` | 1024x1024 | WorldColour |  |  |

### DigitalDisplays

**Destination** (partly exists): There are three in-world screens. (1) The ceremony screen board in the Nomination room (bb_set_ceremonyscreen, HouseSetPieces.cs:167, instance at (0.075, 0, -18.9); the board is 2.6 x 1.5 m, about 16:9). It is the natural home of display_nomination and display_idle_gamesim. (2) The Games-room TV (cabinetTelevision, which maps to bb_set_tvconsole, :263, at (9.46, 0, -10.8)). The veto meeting and veto selection are framed here, so display_veto_competition ('POWER OF VETO') fits. (3) The HoH-suite TV (:231, at (-10.43, 0, -19.4)) for display_hoh_competition. Competitions themselves are framed in the Yard (CeremonyRoom 'competition' returns 'Yard'). The eviction and the winner are framed in the Living room (CeremonyFraming.cs:26-27), but the living room has no working screen (see caution). Nothing at runtime swaps a screen's image by phase. That switcher would have to be written in EpisodeDirector; MemoryWall.cs:181, which sets _BaseMap per portrait quad at runtime, is the pattern to copy.

*Evidence:* Assets/Gamesim/Editor/HouseSetPieces.cs:160-167, :231, :263; Assets/Gamesim/Editor/HouseCatalogue.cs:65 (cabinetTelevision -> bb_set_tvconsole), :83 (televisionModern -> bb_set_tv). ArtSource/setpieces/bb_set_ceremonyscreen.py:37 glow_screen, :41-42 (BOARD 2.60 x 1.50, SILL 1.00); bb_set_tvconsole.py:26 (H 0.45), :37 (tv_screen 1.04 x 0.49); ArtSource/tools/bb_build.py:15-32 _box_uv, :52-58 (UVs taken after verts are offset, so piece-space metres). Assets/Gamesim/Art/Authored/SetPieces/Materials/bb_mat_glow_screen.mat, bound by name (AuthoredAssetImporter.cs:150-157). Assets/Gamesim/Runtime/Episode/EpisodeDirector.CeremonyFraming.cs:19-30. Scene: two 'Television' boxes at (-5, 1.4, -1.5), House Architecture/Television (Ink) and Broadcast Dressing/Television (Emissive/TV Screen.mat), both with renderers disabled, plus a disabled 'Television console'; practical lights 'Television practical' x2, 'Television (model) practical' and 'televisionModern practical' x2 remain. Assets/Gamesim/Editor/HouseFurnishing.cs:53 (Absorbed). Assets/Gamesim/Runtime/House/HouseInteractionAnchor.cs:136 (episode-destination anchor on bb_set_ceremonyscreen). Assets/Gamesim/Runtime/Presentation/MemoryWall.cs:181.

*Caution:* bb_mat_glow_screen is one material shared by the ceremony board, both TV consoles, bb_set_tv and the clutter laptop screen (ArtSource/setpieces/clutter_pieces.py:101), so painting it puts one image everywhere. Use per-renderer materials or a MaterialPropertyBlock. The box UVs are piece-space metres: the board runs u about -1.3..1.3 and v about 1.0..2.5, the TV screen u about -0.52..0.52 and v about 0.58..1.07. The pack textures import Clamp, so a 1920x1080 image shows only edge streaks, not a tiled picture. Either set per-renderer scale and offset or overlay a 0..1-UV quad. The TV screen is 2.1:1, so 16:9 art crops. Every display has baked English ('HEAD OF HOUSEHOLD', 'NOMINATIONS') that Localisation.Text cannot translate. Backing the 'Award and discipline' TMP sign with display_hoh_competition or display_veto_competition would print the same words twice (EpisodeDirector.CompetitionArena.cs:121). A runtime switcher needs serialized references on EpisodeDirector (UiPackImportTests.cs:108).

- `display_nomination.png` - Ceremony screen board, Nomination room (the KeyCeremony frame).
- `display_idle_gamesim.png` - Default state for the board and both TV consoles.
- `display_hoh_competition.png / display_veto_competition.png / display_endurance.png` - HoH-suite TV and Games TV as ambient states. The yard, where competitions are framed, has no screen. Behind the 'Award and discipline' sign (CompetitionArena.cs:116) the titles would double.
- `display_eviction.png / display_winner.png` - Living room, which has no working screen: both 'Television' boxes are disabled, and bb_set_tv has 0 instances. A living-room screen has to be restored or placed first.
- `display_final_three.png` - The FinalHoHPart1-3 phases (EpisodeDirector.cs:1237-1239). Ceremony board.
- `display_quiz.png` - Loose fit: 'Mental' maps to a memory-card game (CompetitionMiniGames.For, CompetitionMiniGames.cs:98-104), not a quiz.
- `display_house_party.png` - No party event exists in the simulation or the runtime.
- `display_diary_room.png` - The Private/diary room has no screen. The runtime diary backdrop is the only candidate.
- `display_memory_wall.png` - No screen near the Memory Wall (living-room west wall).

Files in `Assets/Gamesim/Art/Packs/Pack5_HouseBroadcast/DigitalDisplays/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `display_diary_room.png` | 1920x1080 | WorldColour |  |  |
| `display_endurance.png` | 1920x1080 | WorldColour |  |  |
| `display_eviction.png` | 1920x1080 | WorldColour |  |  |
| `display_final_three.png` | 1920x1080 | WorldColour |  |  |
| `display_hoh_competition.png` | 1920x1080 | WorldColour |  |  |
| `display_house_party.png` | 1920x1080 | WorldColour |  |  |
| `display_idle_gamesim.png` | 1920x1080 | WorldColour |  |  |
| `display_memory_wall.png` | 1920x1080 | WorldColour |  |  |
| `display_nomination.png` | 1920x1080 | WorldColour |  |  |
| `display_quiz.png` | 1920x1080 | WorldColour |  |  |
| `display_veto_competition.png` | 1920x1080 | WorldColour |  |  |
| `display_winner.png` | 1920x1080 | WorldColour |  |  |

### EnvironmentWallGraphics

**Destination** (partly exists): Most rooms have no hero-wall surface. The house walls are 1.5 m cutaways, and the south wing's walls (HoH, Nomination, Games) and the South cutaway wall at z -10 are 1.1 m, so a graphic has to go on something that already exists. Three kinds of surface do. (1) The three prototype picture frames under House Architecture/Broadcast Dressing/Decor. Each 'Frame Inner' is a primitive cube, about 1.0 x 0.65 m, with 0..1 UVs per face: 'Frame - Living South' at (-9, .65, -9.8) on the 1.1 m South cutaway wall, 'Frame - Kitchen East' at (13.8, .75, -7) and 'Frame - Bedroom West' at (-13.8, .75, 6). EpisodeHouseDressing.CarryVolumeAndDecor() carries them over. (2) The runtime 'Diary interview backdrop' > 'Upholstered interview wall', 3.6 x 2.9 m (DiarySeatPose.BuildStudio()). It exists only during a diary visit. (3) Memory Wall > Authored wall > Backing (MemoryWallBuilder.Authored()). The HoH, Games and Nomination rooms have no mount. They need a new quad on the inner face of a 1.1 m wall, or a free-standing panel like bb_set_ceremonyscreen.

*Evidence:* Scene EpisodeHouse.unity: Decor > 'Frame - Living South'/'Frame - Kitchen East'/'Frame - Bedroom West' > 'Frame Inner' (Mint.mat, Coral.mat and Emissive/Neon Blue.mat) and 'Frame Outer' (Warm Oak.mat); 'South cutaway wall' (0, .55, -10), 1.1 m. Assets/Gamesim/Editor/EpisodeHouseDressing.cs:108 CarryVolumeAndDecor, :140 TransplantDecor (destroys and re-copies Decor). Assets/Gamesim/Runtime/House/DiarySeatPose.cs:144 BuildStudio, :148 'Diary interview backdrop', :151 'Upholstered interview wall', :159 'Diary upholstery', built at :85 and destroyed at :146/:183; Runtime/Episode/EpisodeDirector.DiaryRoom.cs:127 (AddComponent<DiarySeatPose> on the player). Assets/Gamesim/Editor/MemoryWallBuilder.cs:136 Authored, :177 'Backing' (bb_mat_ink); ArtSource/setpieces/bb_set_memorywall.py:34-36 (2 rows of 0.53 m frames around 0.75 m, so 0.19-1.31 m). Assets/Gamesim/Editor/HouseSetPieces.cs:52-61 (1.5 m / 1.1 m cutaways), :211 bb_set_hohbed, :263 Games cabinetTelevision, :547 Entrance / :570 'Arch lintel', :727-736 backdrop and signs. Assets/Gamesim/Editor/HouseCinematicLighting.cs:65 Bake the house, :187 MarkEmitters.

*Caution:* Each 'Frame Inner' uses a shared prototype material: Mint.mat (12 renderers), Coral.mat (9) and Emissive/Neon Blue.mat (9, plus the middle competition lane, which HouseSetPieces.Course paints with the same material). Each frame therefore needs its own material; setting a texture on the shared one repaints other decor. The Memory Wall Backing wears bb_mat_ink, which about 26 authored set pieces share, so the header needs its own quad. DiarySeatPose is added to the player at runtime and cannot hold a serialized field. The texture has to be serialized on the scene's EpisodeDirector (it has SerializeFields, EpisodeDirector.cs:21) and handed down, because world textures live in Art/Packs outside Resources (UiPackImportTests.cs:108). Re-running CarryVolumeAndDecor destroys and re-copies Decor from HousePrototype.unity, which drops anything set only on the episode-scene frames. The yard backdrop already carries three authored neon signs (:734-736), and the README asks for one hero graphic per room. New quads must be collider-free, or HouseFurnitureCollision will fit a proxy. Emissive art needs 'Light the house (cinematic)' (MarkEmitters :187) and then 'Bake the house' (:65).

- `bedroom_botanical.png` - Bedroom: 'Frame Inner' of 'Frame - Bedroom West'. That frame currently shares Emissive/Neon Blue.mat, so give it its own material. There is one frame, so choose this or bedroom_geometric.
- `bedroom_geometric.png` - Alternative for the same frame. Using both needs a new quad on the 1.5 m West wall.
- `competition_grid.png` - Yard: the face of bb_set_comp_backdrop (Course :727, instance at z 19.9). It clashes with the three neon lanes and the backdrop signs, so use it at low contrast or not at all.
- `diary_room_halo.png` - Private room (the diary): a texture on 'Upholstered interview wall' (DiarySeatPose.cs:151-161). The wall is runtime-only and its material is created in code, so the reference must come from EpisodeDirector.
- `game_room_pattern.png` - Games room: no mount. The north wall is the 1.1 m South cutaway wall, and the TV console standing in front of it (world (9.46, 0, -10.8), :263) is 1.4 m wide with a screen top at 1.1 m, so a quad directly behind it is hidden. Put it beside the console (the speakers stand at about ±2.8 m).
- `gamesim_eye_large.png` - Brand mark with no object. The 'Arch lintel' (:570) is a 5.04 x 0.31 m box in Neon Gold.mat (165 renderers) at y 3.1, too shallow for 1:1 art, so a panel above the arch would be needed. The other option is the Private/diary room.
- `gamesim_house_geometry.png` - House glyph with no object; the entrance arch has the same constraint as above. Same meaning as the IconForge 'house' icon (IconForge.cs:46).
- `hoh_crown_wall.png` - HoH suite: a new quad on a 1.1 m wall near bb_set_hohbed (instance at (-10.43, 0, -12.8), :211). Byte-identical to CompetitionKit/hoh_crown_icon.png (same md5), so one file serves both.
- `memory_wall_header.png` - Memory Wall ((-13.815, 0, -5), 1.5 m wall). Only about 0.19 m is free above the frames (they reach 1.31 m), so at 4.44:1 the header is at most about 0.85 m wide unless the frames move. It needs its own quad, not the shared bb_mat_ink Backing.
- `same_house_different_stories.png` - The same words are already the placed neon bb_set_sign_samehouse on the yard backdrop (:736). If used at all, put it in 'Frame - Living South' (Mint.mat, needs its own material).
- `social_strategy_survival.png` - Overlaps the placed bb_set_sign_pillars ('STRATEGY STRENGTH SOCIAL SURVIVAL', :734). Candidates: 'Frame - Kitchen East' (Coral.mat) or the Nomination room, which has no mount.
- `strategy_people_power.png` - Same issue. Candidate: the Nomination room, on a 1.1 m wall beside the ceremony screen. No mount exists.
- `the_game_never_sleeps.png` - 'Frame - Bedroom West' or 'Frame - Living South'. Duplicates neon_game_never_sleeps_mask, so use one or the other.

Files in `Assets/Gamesim/Art/Packs/Pack5_HouseBroadcast/EnvironmentWallGraphics/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `bedroom_botanical.png` | 1600x900 | WorldColour |  |  |
| `bedroom_geometric.png` | 1600x900 | WorldColour |  |  |
| `competition_grid.png` | 1600x900 | WorldColour |  |  |
| `diary_room_halo.png` | 1600x900 | WorldColour |  |  |
| `game_room_pattern.png` | 1600x900 | WorldColour |  |  |
| `gamesim_eye_large.png` | 1024x1024 | WorldColour |  |  |
| `gamesim_house_geometry.png` | 1024x1024 | WorldColour |  |  |
| `hoh_crown_wall.png` | 1024x1024 | WorldColour |  |  |
| `memory_wall_header.png` | 1600x360 | WorldColour |  |  |
| `same_house_different_stories.png` | 1600x700 | WorldColour |  |  |
| `social_strategy_survival.png` | 1600x700 | WorldColour |  |  |
| `strategy_people_power.png` | 1600x700 | WorldColour |  |  |
| `the_game_never_sleeps.png` | 1600x700 | WorldColour |  |  |

### NeonMasks

**Destination** (does not exist yet): Lit signs on room walls. No mask-driven sign or mount exists. The project builds neon as 3D tube-lettering set pieces (ArtSource/tools/bb_sign.py -> bb_set_sign_*.fbx, glowing through bb_mat_neon_* and Prototype/Emissive materials). A mask can be used without a Shader Graph: a URP/Lit quad with the mask as _EmissionMap (its RGB is black off-glyph and white on-glyph) and an HDR _EmissionColor. It also needs the mask as _BaseMap with alpha clipping or transparency, or it draws as a dark card. A new HouseSetPieces step would place it. Intended spots: neon_hoh_suite beside bb_set_hohdoor (HouseSetPieces.cs:216); neon_diary_room on the diary backdrop (runtime-only, DiarySeatPose.BuildStudio) or a Private-room wall; neon_gamesim on the living-room wall where the unplaced bb_set_sign_gamesim was meant to hang; neon_good_people_bigger_stories on the yard wall where the unplaced bb_set_sign_goodpeople was meant to hang. The other three slogans have no room.

*Evidence:* Assets/Gamesim/Editor/HouseSetPieces.cs:78-83 SteppedModels and :734-736 (only bb_set_sign_pillars, _hoh and _samehouse are placed). Assets/Gamesim/Art/Authored/SetPieces/bb_set_sign_gamesim.fbx, bb_set_sign_goodpeople.fbx and bb_set_sign_goodcompany.fbx each have 0 GUID references in EpisodeHouse.unity. ArtSource/setpieces/bb_set_sign_gamesim.py:1 ('living-room wall'), bb_set_sign_goodpeople.py:1 ('yard wall'), bb_set_sign_goodcompany.py:1 ('handwritten half of the living-room sign'), bb_set_sign_hoh.py:20 ('HOH COMPETITION'). Assets/Gamesim/Editor/AuthoredAssetImporter.cs:187, :217-223 (neon emission x3.2). Assets/Gamesim/Editor/UiPackImporter.cs:39-41, :74 ('importer.sRGBTexture = !data;'). neon_*_mask.png.meta: sRGBTexture 0, wrap Clamp. No .shadergraph or .shader under Assets/Gamesim.

*Caution:* Three texts duplicate authored 3D signs: GAMESIM, GOOD PEOPLE/BIGGER STORIES (both unplaced) and SAME HOUSE/DIFFERENT STORIES (placed). Pick one medium per message. 'HOH SUITE' is not a duplicate of bb_set_sign_hoh, which reads 'HOH COMPETITION'; it overlaps RoomSignage/hoh_suite_sign and RoomLabels.Title('HoH') instead. The importer is currently correct: UiPackImporter.cs:74 sets sRGB = !data, IsPack (:39-41) covers only the two pack roots, and the metas show the masks import linear and Clamp. The earlier 'MUTATION' reading is stale. MarkEmitters (HouseCinematicLighting.cs:187-200) treats any _EMISSION, neon, glow or screen material as a baked emitter, so new signs need that pass re-run and a re-bake (:65).

- `neon_same_house_different_stories_mask.png` - Defect, confirmed: the alpha bounds run x 0-1600, so 'DIFFERENT STORIES.' is clipped at both edges. The phrase is also already on the yard backdrop as bb_set_sign_samehouse.
- `neon_diary_room_reference.png / neon_gamesim_reference.png / neon_hoh_suite_reference.png` - Colour previews, imported as WorldColour (sRGB). They are for tuning the emission colour, not for runtime use.
- `neon_power_changes_everything_mask.png` - No room or object. The HoH suite fits, but it already takes neon_hoh_suite.
- `neon_strategy_lives_here_mask.png` - No room or object. Nomination or Games would fit; both have only 1.1 m walls.
- `neon_game_never_sleeps_mask.png` - No object. Duplicates EnvironmentWallGraphics/the_game_never_sleeps.png.

Files in `Assets/Gamesim/Art/Packs/Pack5_HouseBroadcast/NeonMasks/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `neon_diary_room_mask.png` | 1600x500 | NeonMask |  |  |
| `neon_diary_room_reference.png` | 1600x500 | WorldColour |  |  |
| `neon_game_never_sleeps_mask.png` | 1600x500 | NeonMask |  |  |
| `neon_gamesim_mask.png` | 1600x500 | NeonMask |  |  |
| `neon_gamesim_reference.png` | 1600x500 | WorldColour |  |  |
| `neon_good_people_bigger_stories_mask.png` | 1600x500 | NeonMask |  |  |
| `neon_hoh_suite_mask.png` | 1600x500 | NeonMask |  |  |
| `neon_hoh_suite_reference.png` | 1600x500 | WorldColour |  |  |
| `neon_power_changes_everything_mask.png` | 1600x500 | NeonMask |  |  |
| `neon_same_house_different_stories_mask.png` | 1600x500 | NeonMask |  |  |
| `neon_strategy_lives_here_mask.png` | 1600x500 | NeonMask |  |  |

### RoomSignage

**Destination** (partly exists): Floor decals at each HouseRoomMarker ('Room - Living' (-5, 0, -7), 'Room - Kitchen' (3, 0, -6), 'Room - Bedroom' (-5, 0, 2), 'Room - Private' (7, 0, 3), 'Room - Yard' (0, 0, 14), 'HoH room marker', 'Nomination room marker', 'Games room marker'), placed by a new editor step next to HouseSetPieces. RoomLabels.Title's doc calls its words the names 'as the set paints it on the floor', but nothing paints them today. Room names appear in three places, all through RoomLabels.Title: the overview's world-space glass chips 3.4 m above each marker (RoomLabels.Show :51, called from EpisodeDirector.Overview.cs:52), the Live feed caption (EpisodeDirector.LiveFeed.cs:110) and the HUD 'WHO IS WHERE' column (EpisodeHud.cs:637). The eight rooms are Living, Kitchen, Bedroom, Private (the diary), Yard (the competition yard, with the pool at its east end), HoH, Nomination and Games. There is no gym, bathroom, dining room or separate pool room, and the pack has no Nomination sign.

*Evidence:* Assets/Gamesim/Runtime/House/HouseRoomQuery.cs:31-40 (8 floors and room ids); Assets/Gamesim/Runtime/Presentation/RoomLabels.cs:8-13 (built and destroyed per overview), :34-47 Title, :51 Show; Runtime/Episode/EpisodeDirector.Overview.cs:51-52; EpisodeDirector.LiveFeed.cs:110; EpisodeHud.cs:637; EpisodeDirector.DiaryRoom.cs:40-50 (diary = 'Private' marker); Assets/Gamesim/Editor/HouseSetPieces.cs:120-136 (dining table in the Kitchen), :216 bb_set_hohdoor, :236-239 (HoH en-suite), :289 bb_set_diarychair, :300-305 (pool, hot tub, loungers); Assets/Gamesim/Simulation/HouseRooms.cs:25-35; Assets/Gamesim/Runtime/Presentation/HouseMap.cs:35-45 RoomIcon (pinned by Tests/EditMode/HouseMapTests.cs:12-21); Assets/Settings/PC_Renderer.asset (SSAO only), Mobile_Renderer.asset (m_RendererFeatures: [])

*Caution:* The URP renderers have no Decal renderer feature, so the README's 'Decal Projector' option means editing both renderer assets. Unlit transparent quads avoid that. Always-on floor signs would duplicate the overview chips while RoomLabels is showing. The diary sign says 'DIARY ROOM' but RoomLabels.Title("Private") returns 'PRIVATE ROOM', which also feeds the Live feed caption and the HUD column. Agree on one name. RoomLabels is attached at runtime (RoomLabels.Attach), so any sign art for its chips must be passed from EpisodeDirector; the textures are outside Resources.

- `living_room_sign.png` - 'Room - Living'. Matches 'LIVING ROOM'.
- `kitchen_sign.png` - 'Room - Kitchen'. Matches 'KITCHEN'.
- `bedroom_sign.png` - 'Room - Bedroom'. Matches 'BEDROOM'.
- `diary_room_sign.png` - 'Room - Private' (bb_set_diarychair at (7, 0, 4), :289). Conflicts with 'PRIVATE ROOM' in RoomLabels.Title.
- `hoh_suite_sign.png` - 'HoH room marker', or at bb_set_hohdoor (:216). Matches 'HOH SUITE'. Also the same words as NeonMasks/neon_hoh_suite.
- `game_room_sign.png` - 'Games room marker'. Matches 'GAME ROOM'.
- `competition_yard_sign.png` - 'Room - Yard'. Matches 'COMPETITION YARD'.
- `backyard_sign.png` - Same room as the competition yard (the narration calls it 'the backyard'). Only a second zone sign at the pool end, or drop it.
- `pool_sign.png` - The pool is a prop inside the Yard (bb_set_pool at (11.2, 0, 15.7)), not a room. A zone sign beside it is possible.
- `dining_area_sign.png` - No room or marker. The dining table and 16 chairs sit at the Kitchen's south end (:120-136), and a 'Dining Area' Decor subtree exists. Zone sign only.
- `bathrooms_sign.png` - No bathroom room exists; the only fixtures are the HoH en-suite (:236-239). HouseRooms.All still narrates 'the bathroom'.
- `gym_sign.png` - No gym exists, only the HouseMap.RoomIcon 'gym' branch. It needs a room before it has a use.

Files in `Assets/Gamesim/Art/Packs/Pack5_HouseBroadcast/RoomSignage/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `backyard_sign.png` | 1200x220 | WorldColour |  |  |
| `bathrooms_sign.png` | 1200x220 | WorldColour |  |  |
| `bedroom_sign.png` | 1200x220 | WorldColour |  |  |
| `competition_yard_sign.png` | 1200x220 | WorldColour |  |  |
| `diary_room_sign.png` | 1200x220 | WorldColour |  |  |
| `dining_area_sign.png` | 1200x220 | WorldColour |  |  |
| `game_room_sign.png` | 1200x220 | WorldColour |  |  |
| `gym_sign.png` | 1200x220 | WorldColour |  |  |
| `hoh_suite_sign.png` | 1200x220 | WorldColour |  |  |
| `kitchen_sign.png` | 1200x220 | WorldColour |  |  |
| `living_room_sign.png` | 1200x220 | WorldColour |  |  |
| `pool_sign.png` | 1200x220 | WorldColour |  |  |

### SurfaceDecals

**Destination** (partly exists): Detail masks on existing materials, set by editor passes. HouseFloorDressing.Surface() and Walls() (Editor/HouseFloorDressing.cs:123, :157) set up the floor materials (Art/Authored/Materials/bb_mat_floor_*) and the shell wall (Art/Authored/Shell/Materials/bb_mat_shell_wall.mat, :145); add URP/Lit _DetailMask or detail albedo there. Set-piece materials in Art/Authored/SetPieces/Materials are existing .mat files that the FBX importer binds by name rather than regenerating (AuthoredAssetImporter.cs:144-157), so hand edits survive re-import. Target materials: fabric_weave -> bb_mat_ph_sofa, bb_mat_velvet_teal, bb_mat_mattress_cold and the runtime 'Diary upholstery'; stone_veins -> bb_mat_counter_stone and bb_mat_coping_stone; frosted_glass_pattern and glass_smudge_mask -> bb_mat_shower_glass, bb_mat_oven_glass and bb_mat_tray_glass; tile_grid_mask -> bb_mat_pool_tile; geometric_wall_mask -> bb_mat_shell_wall; surface_noise_fine and surface_scratches -> the floors and competition props.

*Evidence:* Assets/Gamesim/Editor/HouseFloorDressing.cs:46-56 Plan, :123-143 Surface (metallic and occlusion if present), :145-171 WallMaterial and Walls; Assets/Gamesim/Art/Authored/Textures (bb_tex_ph_oak, tile, walnut and plaster each have albedo, normal and occlusion, no metallic); Assets/Gamesim/Art/Authored/SetPieces/Materials/*.mat (all named targets exist); Assets/Gamesim/Editor/AuthoredAssetImporter.cs:150 OnAssignMaterialModel; Assets/Gamesim/Runtime/House/DiarySeatPose.cs:159 'Diary upholstery' (created in code); SurfaceDecals/*.png.meta (sRGBTexture 0, wrap Repeat); no .cs sets _DetailAlbedoMap or _DETAIL_MULX2

*Caution:* Nothing in the project uses detail maps today. The floors and walls already wear Poly Haven scans with their own normal and AO maps (tile, oak, walnut, plaster; there are no metallic maps; the yard is a baked lawn), so tile_grid_mask and geometric_wall_mask would fight real texture. The masks measured (tile_grid, fabric_weave, stone_veins, geometric_wall) have binary RGB, 0 where transparent and 255 where marked, and carry their strength in alpha, which peaks at only 23-70 of 255. As a _DetailAlbedoMap under _DETAIL_MULX2 they would multiply by 0 or 2 (black or blown out). Use them as _DetailMask, where URP reads alpha, or convert them to a 0.5-neutral grey first. The import is correct as it stands: linear and Repeat, per UiPackImporter.cs:74, :78 and the metas. The earlier 'MUTATION' note is stale. There is no Decal renderer feature for the projector route. 'Diary upholstery' is built at runtime and needs code plus a passed reference.

- `tile_grid_mask.png` - The Kitchen and Games floors already use the bb_tex_ph_tile scan, and HouseSetPieces.CheckerKitchen (:513) lays checker tiles. Use it only on bb_mat_pool_tile.
- `glass_smudge_mask.png` - The largest file in the category (267 KB). Only the glass materials qualify: shower, oven, tray.

Files in `Assets/Gamesim/Art/Packs/Pack5_HouseBroadcast/SurfaceDecals/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `fabric_weave_mask.png` | 1024x1024 | DetailMask |  |  |
| `frosted_glass_pattern.png` | 1024x1024 | DetailMask |  |  |
| `geometric_wall_mask.png` | 1024x1024 | DetailMask |  |  |
| `glass_smudge_mask.png` | 1024x1024 | DetailMask |  |  |
| `stone_veins_mask.png` | 1024x1024 | DetailMask |  |  |
| `surface_noise_fine.png` | 1024x1024 | DetailMask |  |  |
| `surface_scratches.png` | 1024x1024 | DetailMask |  |  |
| `tile_grid_mask.png` | 1024x1024 | DetailMask |  |  |

### VFXTextures

**Destination** (does not exist yet): No consumer exists. The project has no ParticleSystem at runtime. OpeningSequence deliberately draws confetti and flashes as UI Images (Confetti() at OpeningSequence.cs:467, 90 HudPrimitives.Fill rects; Flashbulbs() at :502) to avoid world particles over a screen-space overlay. Intended uses: confetti_gold and confetti_mixed as falling sheets for OpeningSequence.Confetti or a winner moment (CompetitionResult.cs:109, SeasonReport 'WINNER' :245); gold_glow, star_sparkle and light_streak for HoH or veto wins and Flashbulbs; red_pulse for the nominee and eviction beats (KeyCeremony, VoteReveal); cyan_glow for the competition station discs (not FollowRing, which never pulses); pink_heart_glow for flirt/showmance (UiTheme.Flirt); smoke_puff and soft_particle have no beat. All of these need new code, and particle materials if they are to be world particles.

*Evidence:* Grep of Assets/Gamesim for ParticleSystem: only OpeningSequence.cs:32-37 (doc rejecting runtime ParticleSystems) and HouseFurnitureCollision.cs:318 (skips ParticleSystemRenderer). OpeningSequence.cs:48 ConfettiCount 90, :467-500 Confetti, :502 Flashbulbs, :572 Motionless. Assets/Gamesim/Runtime/Presentation/FollowRing.cs:19-20 ('neither part pulses or spins'); CharacterPresentation.cs:30 Reaction {Nominated, Saved, Evicted, Won, Cheered}; UiTheme.cs:73 Flirt; UiPackCatalogue kind Particle (Default, sRGB, Clamp, mipmaps).

*Caution:* The files import as Default textures under Art/Packs, not as sprites and not under Resources. A runtime UI use (OpeningSequence, CompetitionResult) needs either a catalogue kind change (regenerate with ArtSource/ui-packs/tools/bb_ui_packs.py) or references passed from EpisodeDirector (UiPackImportTests.cs:43, :108). All motion must stay behind the ReducedMotion preference, which OpeningSequence honours through Motionless (:572). FollowRing states by design that it never pulses. The confetti textures are whole fields of confetti, not single flakes.

- `confetti_gold.png / confetti_mixed.png` - Full-field 1024x1024 sheets. OpeningSequence.Confetti (:467) animates 90 single rects, so these would need a falling-sheet approach instead.
- `pink_heart_glow.png` - By eye, a plain pink radial glow with no heart shape. No flirt or showmance visual exists; the only hook is the UiTheme.Flirt colour.
- `smoke_puff.png` - No beat uses smoke.

Files in `Assets/Gamesim/Art/Packs/Pack5_HouseBroadcast/VFXTextures/`:

| File | Size | Kind | Border (L,B,R,T) | Body inset (L,B,R,T) |
|---|---|---|---|---|
| `confetti_gold.png` | 1024x1024 | Particle |  |  |
| `confetti_mixed.png` | 1024x1024 | Particle |  |  |
| `cyan_glow_particle.png` | 512x512 | Particle |  |  |
| `gold_glow_particle.png` | 512x512 | Particle |  |  |
| `light_streak.png` | 1024x256 | Particle |  |  |
| `pink_heart_glow.png` | 512x512 | Particle |  |  |
| `red_pulse_particle.png` | 512x512 | Particle |  |  |
| `smoke_puff.png` | 512x512 | Particle |  |  |
| `soft_particle.png` | 512x512 | Particle |  |  |
| `star_sparkle.png` | 512x512 | Particle |  |  |


## Refinement Kit 6 - Kit6_Refinement

Imported 2026-09-23 by `ArtSource/ui-packs/tools/bb_ui_kit6.py` (sha256, size and border checked against the kit's
manifest); 71 white, tintable sprites under `Resources/Packs/Kit6_Refinement/{Chrome,Widgets,Icons,EmptyStates}`, the
previews, review boards, SVG sources and the kit's two documents under `ArtSource/ui-packs/Kit6_Refinement/`. Unlike
packs 1-5 the kit is **wired**: it is the notebook's pages, the conversation notice and the diary's record, and
`PackArt.Kit*` names every sprite the HUD draws with it.

- **White masks, tinted by role.** `Image.color` is a `UiTheme` token - `CardFill` on a card, `SurfaceRaised` on a
  quiet pill, `ActionBlue` on the chosen filter, `Edge(Interactive)` on a resting edge. No kit colour replaces a token.
- **Slice borders are the manifest's**, not measured: card 18, button 14, pill 32/30, panel 24 (left, bottom, right,
  top). `UiPackImportTests` exempts the kit from packs 1-4's `_9slice` naming rule and holds each sliced sprite to
  its catalogue kind.
- **A card is two images** (`HudPrimitives.KitCard`): the fill and a `Border` child from `card_edge_rest` (or
  `card_edge_focus`). No `Glow` child - the kit's rule, and `NotebookRooms_EveryoneIsInOneRoomCardAndNoCardGlows`
  holds the room cards to it.
- **Opacity is on the ground Image, never a CanvasGroup**: the notebook's ground is at least .97, the conversation
  notice's .97.

| Screen | Where | Kit parts |
|---|---|---|
| Who is where | `HouseMap.Build`, `EpisodeDirector.RenderNotebookRooms` | card fill/edge, pill fill/edge (filters), room icons (`RoomLabels.Mark`), `ic_info` (footer), `ic_people` (footer action) |
| Houseguests, profile | `EpisodeHud.Roster.cs`, `EpisodeDirector.RenderNotebookPeople` | card rows, pill status, `ic_search`, `ic_chevron_right`, `ic_arrow_back` |
| The vote | `EpisodeHud.Votes.cs`, `EpisodeDirector.RenderNotebookVotes` | card records, `votes_no_records`, `ic_lock` |
| Conversation unavailable | `EpisodeHud.Availability.cs` (`ActivityLayout.ConversationNotice`) | card fill/edge as the panel's ground, pills, `ic_lock` |
| Diary record | `EpisodeHud.DiaryRecord.cs`, `EpisodeDirector.RenderDiary` | cards, pills (tabs), `ic_book`, `ic_person`, `ic_jury`, `ic_info`, `ic_chevron_*`, `ic_note`, `private_no_decision` |

Not used yet: the panel family and its focus halo, the meter parts (a signed trust meter needs a confirmed score
domain - the pages print the signed number instead), the timeline and relation widgets, `records_load_error` (no
vote record can fail to load: the page reads the save in memory), `room_no_known_occupants` (every located
houseguest is known; "Location unavailable" covers the rest).
