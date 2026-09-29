<!-- Written 2026-09-29 from the mockup-gap-map workflow (wf_b1d1f9de-d26): eight readers, eight
adversarial verifiers, a synthesis and a completeness critic over the owner's mockups 48-63. The
critic's corrections are in section 6; apply each with the slice it names. -->

# Mockup pass plan: mockups 48 to 63

The owner sent 16 mockups and storyboards, numbered 48 to 63, and asked for each screen, menu and cut-scene to be checked against them and brought closer to them. Earlier the same day the owner asked for couches in the eviction room, so the whole house sits together for the announcement with the two nominees in the red eviction chairs.

This plan turns the verified gap map into build slices. It sits beside `ENDGAME-PLAN.md` (F1 to F8), `CEREMONY-CUTSCENES-PLAN.md` (sections 7 and 8, slices S1 to S8) and `ASSET-PACKS.md`, and does not replace them. When a slice here is one of theirs, it says so.

## Where things stand

- **Already committed.** The Season Complete redesign (F6b) is in 58497f3. The final case and live questioning, with schema 21, are in 639c821.
- **The only uncommitted work in this worktree** is `ArtSource/setpieces/bb_set_lounge3.py`, `bb_set_lounge4.py` and `bb_set_wingback.py`, plus their FBX exports under `Assets/Gamesim/Art/Authored/SetPieces/`. They are untracked, have no `.meta`, and no code references them. They were written the same day for CEREMONY-CUTSCENES S1 and are M20's pieces; D: test copies for M1 to M19 leave them out until M20 lands.
- **Captures that do not exist yet:**
  - the winner moment: the JuryReveal result and the Finished panel. Every capture under `D:\GamesimVerify\*` is a blank 18,959-byte frame, not only `finale.png` (the runner never wrote a real frame there apart from the settings captures), so M7's PlayMode capture is the evidence;
  - a final HoH part's game screen;
  - the spectator path;
  - the Final 2 decision screen scrolled to the top;
  - the top of the juror ballot;
  - the jury house at three, and the jury house with finale rules on.

  The slices below add them.
- **Mockup numbering.** The owner's numbers map to the plans' numbers as follows. 48 is ENDGAME mockup 28. 49 and 53 are 29 and 33. 50 is 30 and 55 is 35. 51, 52 and 54 are 31, 32 and 34. 56 and 57 are 36 and 37. 58, 59 and 60 are 38, 39 and 40. 61 to 63 are the storyboards in CEREMONY-CUTSCENES §7 and §8.

## Rules every slice keeps

1. **Captions are contracts.** Tests and screen readers find controls by caption. Never rename one. Decorate around it with glyphs, eyebrows, subtitles and frames. Where a mockup relabels a control, the pinned caption is named in the slice.
2. **The knowledge gate.** During play, nothing shows hidden scores, true sentiment, jury leanings, grudges, NPC-to-NPC private events or invented quotes. A quote needs a recorded source: final speeches, eviction pleas in the event log, diary-room entries, jury ballot reasons, or juryExchanges. After the season ends more may be revealed, but still only recorded facts.
3. **The cast is UMA only.** Every portrait comes from the portrait studio (`CharacterPortraits.Get`, drawn with `HudPrimitives.RectPortrait` so late faces bind). There is no photo art and there are no stand-in bodies.
4. **Saved fields.** No slice below adds a saved field. Schema 22 is needed only if the owner picks an option marked "(schema 22)" in section 4, and it must be claimed together with the story session.
5. **3D.** Set pieces are authored in Blender under `ArtSource/setpieces`, one script per export, origin centred, standing on z = 0. Collision proxies go on layer 8, then the NavMesh is baked. Runtime-cloned furniture has no NavMesh hole. Capsule clearance skips the furniture layer, so test layer 8 explicitly.
6. **Cut-scenes.** Batch runs and reduced motion skip every stage and keep the cards, and the audited walk stays on the cards. A skip gives up the order of beats, never the outcome. Nothing on a stage writes saved state.

## How a slice lands

Each slice lands the way CEREMONY-CUTSCENES §8.3 sets out:

1. The offline compile.
2. The Unity-free subset (`dotnet test Tools/SimulationTests`).
3. The filtered suites on the D: copy.
4. The audited pipeline, run alone from the main checkout.
5. A PR.

Every visible change is checked by a capture, not by measurement. New labels are sized at 1.3 times their font. `AssertDecisionCopyFits` runs at FontScale 1.0 and 1.2. Layouts are checked for houses of 3 to 16, which means up to 14 jurors. A new `.cs` file needs its meta, and a new PNG needs a full TextureImporter meta. Pure rules such as ranks, pickers and parsers go in Simulation classes with EditMode tests.

**Effort:** S is hours. M is one to two days. L is three to five days. XL is more than a week, usually with art and a rebake.

---

## 1. Summary

| Mockup | Today | How close | Headline change | Slices |
|---|---|---|---|---|
| **48 Season Complete** | `SeasonReport` and its Dashboard partial (F6b) | Close in its parts and look, but a scrolling report rather than the one-screen grid. Two defects: the timeline overlap and a duplicated quote. | Season number, stat tiles and a footer of large actions. A bigger winner hero with a juror strip and tally bar. Player statistics with ranks, relationships, diary reflection, signature moments and legacy. Then the one-screen grid. | M1, M5, M6, M16, M17 |
| **49 Season finale winner** | The JuryReveal card fades after 5.2 s, then the Finished text panel | Far. The winner moment is transient and has no screen of its own. | A held winner screen inside the Finished panel: gold lockup, 'BY A VOTE OF 5 TO 2', a 2nd-place plate, juror cards and the season's numbers. The four ways on are kept and decorated. | M4, M7 |
| **50 Jury voting** | `JurorBallot` (F6a) | Complete in function, a scrolling text panel in look | A full-frame, three-column juror screen: a profile card, and finalist cases with portraits, trait chips, stats and a VS. Wide vote buttons keep `Vote for {name} to win`. Finale notes and a jury roster. | M3, M8, M9, M10 |
| **51 Final HoH decision (storyboard)** | Crowning card, decision screen, final-eviction card | The decision is well served; nothing is staged after it | Now: a crowning camera with confetti, a FINALIST badge, and no chrome under the cards. Later: the decision staged in the living room and the third place's walk through a TO THE JURY door. | M2, M26, M28, M29 |
| **52 The finale (storyboard)** | Questioning, speeches, vote and reveal on the HUD, with jurors standing in rings | Every beat exists as data; none plays in the room | Now: heads turn and the house claps at the reveal, and the memory wall lights the winner. After the couches: jurors walk in and sit, the finalists sit in the red wingbacks, a ballot box, and the reveal on the living-room screen. | M26, M30, M31 |
| **53 Finale live show winner** | As 49, plus the HUD frame | Further than 49. The mockup's three-finalist format is not the game's. | The same winner screen with a card per finalist. Third place is shown as 3rd place and a juror. The pill shows the jury. | M3, M4, M7 |
| **54 Final 3 to Final HoH (storyboard)** | Final Three and bracket cards on the HUD; the yard is staged only for the player's own attempt | The information matches; the staging does not | No chrome under the cards and a readable yard sign. The finalists at the three lanes. A watch beat when the player sits a part out. A memory-wall shot. Part 3 announced to the three, seated. | M2, M26, M27, M28 |
| **55 Jury questioning** | `JuryQuestioning` live layout (F5) | Close in HUD structure, but the responses overflow and there is no 3D | A framed asker card and question panel, four or five compact responses that fit, and a leaner receipt with the week's tally. Room staging comes with the seated jury. | M11, M30 |
| **56 The Jury House** | `JuryHouseScreen` (F4a) | The substance is built and knowledge-gated; the look is far | A dashboard: compact cards seven across with status dots, a highlights side card, what-matters icon rows, and a 2D callout tableau. More "Knows" lines from the public record. | M12 |
| **57 Prepare your final case** | `FinalCaseScreen` (F4b) | The function matches; it is one scrolling column | Three columns plus a tray: narrative, a résumé with portrait and tiles, and moment cards with portraits. A gold `Lock final argument`. | M15 |
| **58 Final HoH Part 2** | Bracket card, briefing with stakes line, minigame screen | The part information exists; the "watching" scenario does not | A part tracker, competitor cards, a "winner advances" band and a gold final-part band. Later, the two competitors staged in the yard while the Part 1 winner watches. | M3, M13, M27, M35 |
| **59 Choose your Final 2** | `FinalTwoChoice` (F2) | The content is right and more honest than the mockup; the panel is tall and scrolls | A compact card grid with the portrait on the left, trait chips, icon rows, a VS, a gold hero head and a selection highlight. The columns line up. `Evict {name}` stays under 'Take {name}'. | M1, M3, M8 |
| **60 Final 3 overview** | The F1 frame | Already the mockup's skeleton | A crowned objective chip and a gold objectives card. A jury card with named faces. Wider finalist chips with traits. Room names and readable name chips at map distance. | M3, M14 |
| **61 Eviction night (storyboard + couches request)** | Living-room staging: 2 cloned hot seats, 3 sofa seats, everyone else standing on the rug | The mechanics match; the set and the exit do not | A U of cream couches and two red wingbacks that seat a full house of 16 (S1 to S3). A voter roster and a three-line result on the screen. A goodbye beat. The exit through a living-room door that closes behind them. | M2, M18 to M23, M36 |
| **62 Veto competition and ceremony** | HUD cards; the meeting frames an empty Game room | The data is complete; nothing is staged | Copy fixes now, and the meeting gathered on today's nomination set. Then the lineup, necklace, box and wingbacks (S6), and the yard set (S7). | M1, M25, M34, M35 |
| **63 Nomination ceremony** | Staged in the nomination room on a ring of chairs | The skeleton is built | Now: clean frames (no name plates, the SAFE chip fixed, the screen unblocked), a shot of the HoH, fist pumps, a two-shot of the block, a GAMESIM board. Later: the couch circle, keybox and walk-ups. | M2, M24, M32, M33 |

---

## 2. Build slices

**Two tracks.** M1 to M17 are HUD and presentation work: `Runtime/Episode` HUD partials, `Runtime/Presentation` screens and small Simulation readers. M18 onward are staging, set and cut-scene work: `CeremonyStage`, `CeremonySets`, the scene, `ArtSource`. They touch different files and can be built by different sessions in parallel.

If one person builds everything, follow the numbers. The UI slices that need no new art come first. The eviction-room couches (M20 to M22) come right after the two eviction slices that need no set. M20 can start at once in parallel, since the pieces are already exported. M21 waits on decisions 1 and 2.

**Dependency summary:**
- M4 before M7. M6 before M7 (shared nameplate constants). M8 before M9 and M10. M6 and M16 before M17.
- M20, then M21, then M22. M19 and M21 before M23.
- M28 before M29. M22, M23 and M29 before M30, and M30 before M31. M32 before M33. M33, M22 and M28 before M34.

### M1. End-screen and copy defects

- **Serves:** 48, 51, 54, 58, 59, 62
- **Closes:**
  - **Raw phase names in the status line.** Today it reads 'Week 4 · FinalHoHPart2 · Saved locally.' In `EpisodeDirector.cs:723-726, 740`, when the visible event's kind is `phase`, build the status text from a phase-label table ('Week 4 · Final HoH, Part 2 of 3', 'Week 4 · The final decision', 'Jury questioning') using `EpisodeHud.PhaseShort` and `FinalHoHPartLabel`. The saved event text is left alone.
  - **Timeline row overlap on Season Complete.** In `SeasonReport.Dashboard.cs` Timeline (:455-465), take the height returned by `EndScreenKit.Wrapped` for the 'Evicted: X (a–b)' label and start the Detail line below max(topHeight, goneHeight).
  - **The winner's quote repeated on the runner-up.** In `SeasonReport.Dashboard.cs` Finalist() (:171), when the runner-up's excerpt equals the winner's, use the runner-up's next sentence, or no quote. Put the rule in a helper next to `EndScreenKit.Excerpt` (:241) so M7 reuses it.
  - **Placements fixture.** `FinishedWithWeeks` (EpisodePlayModeTests.SeasonReport.cs:37-77, extended by EndScreens.cs:32-48) gets a jury ledger in eviction order, and only jurors cast ballots. In Standings (Dashboard.cs:276-313), break a fallback placement tie by `JuryHouseRead.LeftWeek`, latest to leave ranked higher, instead of printing '7' five times.
  - **COMP WINS.** The champion's road (SeasonReport.cs:699) and 'Your season' (:777) move to `FinalistRead.Wins`, which includes the final HoH parts. The career strip's COMP WINS (Dashboard.cs:517) stays hoh+veto, because `CareerSeason` stores only those two. Say so in ENDGAME F6.
  - **Final 2 choice columns out of line.** In `EpisodeHud.Finalists.cs:71-73, :87`, when the columns sit side by side, set the row's `childForceExpandHeight = true` and give each FinalistCard `flexibleHeight = 1`. The headlines, warnings and buttons then share baselines, and neither button is clipped when the screen opens.
  - **Veto field copy when the house is bigger than six.** At `EpisodeDirector.cs:787`, call the six-argument `Play` with a line when `vetoPlayers.Count < Active.Count`: 'Six play for the Golden Power of Veto: the Head of Household, both nominees and n drawn from the house.'
  - **VETO USED / NOT USED.** In `PlayGenericCeremonyCard` for VetoKind, the line becomes 'Veto used: a nominee is removed and a replacement is named.' or 'Veto not used: the nominations stay the same.', depending on whether the block changed. Today it is always 'The veto holder decides…' (CeremonyTakeover.cs:209-210).
  - **Plan text.** Correct ENDGAME-PLAN.md:756-758. Each confessional is logged verbatim as a `diary-room` event (WebDiaryRoom.cs:139), so the plan's "no answer text" is wrong.
- **Data:** committed state only.
- **Effort / risk:** S / low.
- **Tests and pins:**
  - New asserts: a vertical check in EndScreens (under each Week row, the Evicted label's bottom is above the Detail label's top); the winner's and runner-up's quotes differ; the fixture's placements read 1 to 8; both Final 2 controls are whole and level when the screen opens, at both text sizes; no raw enum in the status line at a final part.
  - Pins that must hold: FinalThree.cs:55-60 and 90-106; the 'COMP WINS' label counts (SeasonReport tests :238), whose values change but whose labels do not.
  - Re-capture season-complete, season-complete-large and endgame-final-choice.
- **Kind:** presentation only.

### M2. Clean ceremony frames

- **Serves:** 51, 52, 54, 58, 61, 62, 63
- **Closes:**
  - **Name plates and the player's disc during stages.** Call `SetPlatesSuppressed(true)` (EpisodeDirector.Opening.cs:441-453) from `CeremonyStage.StartCard`, and false in `End` (:1134). Plates stay up through the summons so the player can find a seat. Hold the suppression through a staged walk-out until `FinishWalkOut`. Also suppress while `keyCeremony` or `voteReveal` plays on the HUD frame, because a plate shows through the 0.55 scrim in `ceremony-eviction-in-episode.png`.
    - *As built, every walk-out holds the suppression, not only a staged one.* The stage's Release step already waits for the door, so the extra `walkingOutId != null` term (`TickCeremonyPlates`) covers two other cases. One is a staged walk-out whose stage ran out of its 150 s budget before the door. The other is a walk-out after an eviction that played on the HUD frame because the house could not be staged. Both are the same goodbye, with the camera on the walker and their line on the strip, and a press skips it (40 s at most).
  - **SAFE chip covering the HoH line.** On KeyCeremony's screen frame only (Frame.OnScreen :422-430; chip :596-600), pin the chip inside its slot (y = -chipHeight/2) or move StageY about 30 units down. Frame.Hud is unchanged.
  - **The living-room screen idles on NOMINATIONS.** In `CeremonySets.DressTheLivingRoom`, after Clone, disable the cloned child named `ScreenSurface.IdleDisplayName`. Once M24 swaps the nomination board to GAMESIM, the clone inherits that board instead, and this line can go.
  - **The yard's part sign reads mirrored.** In `CompetitionArena.cs:120-131`, face the 'Award and discipline' sign toward the deck (identity rotation) and lift it clear of the centre gate.
    - *As built, most of the lifted sign stands above the backdrop.* Its lowest line stands 0.25 m over the gate's head, at deck + 2.85 m. At fontSize 5, three lines reach about deck + 4.4 m, which is about 1.4 m over the 3.0 m wall. Before the change it was about 0.5 m. The wall has only 0.4 m above the gate's head, and under the head the yard display and the HOH neon hold the centre, so three readable lines cannot fit on the wall. Check the `competition-yard-sign`, `competition-assembly` and `competition-practice` captures before landing. If the upper lines read poorly against what stands behind the yard, drop `label.fontSize` to about 3.5 or 4.
  - **HUD chrome under the endgame cards.** Call `HoldHudForReveal()` in `PlayFinalHoHCard`, in the FinalThree branch (EpisodeDirector.cs:873-879), and in `PlayGenericCeremonyCard` for FinalEvictionKind. In `TickCeremonies` (EpisodeDirector.CeremonyTruth.cs:119-127), count `takeover.IsPlaying` when `PlayingKind` is final-three, final-hoh-part, final-hoh-crowned or final-eviction.
- **Data:** none new.
- **Effort / risk:** S / low to medium. Any FinaleReveal fixture that reads chrome text while a card is up must be measured.
- **Tests and pins:** a stage test that no plate is visible after StartCard; a screen-frame test that the chip's rect clears the HoH line; captures of `ceremony-stage-key-screen` and of the yard sign.
- **Kind:** presentation only.

### M3. Endgame frame chrome

- **Serves:** 50, 53, 55, 56, 57, 58, 59, 60
- **Closes:**
  - **House pill** (`EpisodeHud.Chrome.cs` HousePill :367-392):
    - Draw the jury cell when `IsEndgame(state) && (holder == null || FinaleNight(phase))`.
    - At the Final 2, the cell after the jury reads '2 Finalists' in place of 'Actions left'.
    - At Finished, '{n} Houseguests' replaces '0/{n}'.
    - Keep the word 'Jury' in the cell. The Final 3 pin (Endgame.cs:61-63) is unaffected.
    - At the final decision, follow decision 51.
  - **Brand strap:** BrandCard (:149) reads 'THE HOUSE | SEASON FINALE' while `FinaleNight(state.phase)`.
  - **Objective chip crown:** when `IsEndgame`, ObjectiveChip (:185-212) draws `PackArt.KitIconCrown` tinted `UiTheme.Gold` in place of the ring and '!'. The title and the 'Next stop:' line are unchanged (pinned at Endgame.cs:57-59, 113-114).
  - **Phase titles and band.** In `EpisodeDirector.cs` PhaseTitle (:1547-1563):
    - Jury becomes 'FINALE · JURY VOTE' and JuryQuestioning becomes 'FINALE · FACE THE JURY'.
    - A final HoH part reads 'FINAL HEAD OF HOUSEHOLD', and PhaseBand's subline (EpisodeHud.cs:1895-1930) reads 'Part n of 3 · {category}' in gold.
    - At FinalEviction, a player HoH gets a gold band and a crown; when an NPC decides, the title reads 'THE FINAL EVICTION'.
    - `PhaseTint` (:1833) and `PhaseGlyph` (:1864) must take the state. `PhaseTitle` is also read by the dashboard (Dashboard.cs:74); check it there.
  - **FINAL 3 OBJECTIVES card** (EndgameCard :627-700):
    - A gold border and a crown before the heading.
    - Each row keeps exactly one Image named 'Objective mark', drawn as a hollow ring while open and gold when done. Checks and the crown take other names, so the count stays 3.
    - The optional tagline 'Last competition. Last decision.' goes as a muted subline under the heading.
    - At FinalEviction with the player as HoH, row 2 reads 'Take one to the Final 2' with the status 'Cannot be undone' (:711-722).
  - **Recent Events:** a small 'View all' text button in the heading corner (CardHeading reserves 72 units, :535-536) opens the notebook's Story page. It must stay distinct from 'See all'.
  - **Season number:** cache `EpisodeDirector.SeasonNumberFor(state)` (Opening.cs:155-169) once per session, since it reads the career file, for M5, M7 and M10.
- **Effort / risk:** S to M / low.
- **Tests and pins:** add a Final 2 jury-cell assert to `Endgame_TheFinalTwoFaceTheJuryInTheFrame`. The 'Objective mark' count of 3 and the chip words stay pinned.
- **Kind:** presentation only.

### M4. The jury reveal's result, and two finale ways on

- **Serves:** 49, 53
- **Closes:**
  - **Vote chips by outcome.** In `JuryReveal.Result()`, recolour each chip `UiTheme.Gold` when its target is the winner and steel blue otherwise, with text colour from `UiTheme.OnColor` (JuryReveal.cs:439-447, :475). Never before the result.
  - **'BY A VOTE OF' as a headline.** New labels 'Count eyebrow' ('BY A VOTE OF') and 'Count' ('5 TO 2') go above the pinned Host and Result labels in `Result()`. For a tie: 'THE JURY IS TIED · 3 TO 3' over the tie-rule line. For a jury of one: 'WITH THE JURY'S ONLY VOTE'. Grow the hand-sized column (:375).
  - **`Watch finale replay`,** a new caption on the Finished panel (EpisodeDirector.cs:1393-1413). `EpisodeDirector.ReplayJuryReveal()` calls `juryReveal.Play(JuryFinalists(Snapshot), JuryVotes(Snapshot), Snapshot.winnerId, reducedMotion, ceremonyPace, (int)seed)` and then `HoldHudForReveal()`. TickCeremonies brings the HUD back.
  - **`The jury's questions`,** a new caption. A Disclosure lists each juror's recorded question and both finalists' answers from `state.juryExchanges`, never `correctChoice`, and each juror's ballot reason in full. This is the truthful stand-in for 53's "roundtable".
- **Effort / risk:** S / low.
- **Tests and pins:**
  - The Host, Progress and Result text stay unchanged (JuryRevealPlayModeTests.cs:57, 74-75, 106-108). A new caption under each count must not be named 'Votes' (:73). AssertEveryLabelDraws at the result.
  - Each new caption appears once on ModalRoot. Update the finale focus ring's expected set in Keyboard.cs:190-197.
- **Kind:** presentation only.

### M5. Season Complete: header, season number and footer

- **Serves:** 48
- **Closes:**
  - **Season number.** Pass the cached season number from `EpisodeDirector.Journal.cs:818 ShowSeasonReport` to `SeasonReport.Show` as an optional int. Header draws a 'SEASON {n}' eyebrow beside the unpinned 'SEASON COMPLETE'. The optional HUD strap reuses it.
  - **Header title card.** Frame the title and summary line in Pack 7 `Shells/section_panel_selected_9slice`, with `PackArt.SeasonWinnerCrown` in place of the trophy (the trophy moves to the legacy panel in M16).
  - **Stat tiles** in a second framed card: 'Houseguests' (`contestants.Count`), '{week} Weeks' (decision 9), and a third tile per decision 14. Use the 'people' and 'calendar' glyphs.
  - **Tagline:** keep the factual summary line. Add a second line per decision 10.
  - **Footer of actions.** Move the ways on (SeasonReport.cs:487-491, :525-532) into a fixed footer band outside the scroll, and shrink the viewport by its height.
    - Each becomes a wide Pack 7 button with a glyph and chevron, built on `button_new_season`, `button_review_season`, `button_main_menu` and `button_continue_legacy` 9-slices (add the PackArt constants).
    - The caption stays word for word. A subtitle is a separate label, for example 'A new group. A new story.' under `Start a new season`.
    - `Main menu` wears the 'Continue to Legacy' face per decision 21, with the caption unchanged.
    - The tabs and the scroll hint stay as a slim row under the header.
  - **The rail:** per decision 13's recommendation, anchor the sheet at `IconRail.Width + 24` (IconRail.cs:39) instead of centring it, and lower the scrim's alpha over the rail while it stays a raycast target.
  - **Lower-left brand line:** 'Same house. Different stories.' in the sheet's own margin over a vignette (decision 23).
- **Effort / risk:** S to M / low.
- **Tests and pins:**
  - The captions at SeasonReport.cs:60-63; the tabs (EndScreens.cs:113-121); ScrollHint (SeasonReportNavigation.cs:257).
  - Each way on appears once, on screen, outside the scroll (SeasonReportNavigation.cs:43-49). The content is still taller than a screen (:83, :261). The scrim takes the clicks at (8,8) and at the centre (:91-97). The keyboard ring compares sets (Keyboard.cs:388).
- **Kind:** presentation only.

### M6. Season Complete: the winner hero and the final jury vote

- **Serves:** 48
- **Closes:**
  - **Winner hero portrait:**
    - A `RectPortrait` of about 230×270 inside Pack 7 `winner_hero_frame_9slice`, over a dark-to-gold gradient. The name sits on `winner_nameplate_9slice`; add the constants for it and for `runnerup_nameplate_9slice`.
    - A few gold sparkles seeded from `state.seed`, twinkling only when reduced motion is off.
  - **Winner eyebrow and name.** 'WINNER' stays its own TMP_Text (EndScreens.cs:70). In front of it go 'SEASON {n}' and the crown. The name is uppercase at 48 to 52 pt with a gold gradient, auto-sizing down to 28.
  - **Trait chips.** One `EndScreenKit.Pill` per trait (`Localisation.Text`), plus a gold chip with `FinalArgument.Label` for a player winner. Do not pad to four.
  - **Hero stats column.** Four icon rows worded 'Competition wins', 'HoH wins', 'Veto wins', 'Nominations survived'. These avoid the pinned exact 'HOH WINS' and 'COMP WINS'. When the player won, follow decision 19.
  - **Runner-up and verdict folded into a 'Final jury vote' panel.** The juror strip is one card per ballot:
    - a `RectPortrait` of about 64 px;
    - the first name, or 'You';
    - a small 'VOTED' over `FinalistRead.FirstName(finalist)` in gold or steel.
    
    Wrap to two rows past about 8 jurors. Never write 'voted for' on these cards. The tally bar is a `SplitBar` about 28 px tall with 26 pt number tiles at each end and full names below; a tie draws equal halves. The crown line, the count lines and the tie lines move word for word. Either keep the panel inside the HeroName part or move the EndScreens pins (:70-74, :141-144) in the same commit. `How the jury voted`, with its reasons, stays as the JuryColumnName part lower down.
- **Data:** `state.votes` via `SeasonReport.JuryBallots`, `ContestantState.traits`, `FinalArgument`, `FinalistRead.Wins`, win counts.
- **Effort / risk:** M / medium, because of the pins that move.
- **Tests and pins:** the 'voted for {name}' counts (SeasonReport tests :200-215, EndScreens.cs:91); 'WINNER' under HeroName; FinaleExits.cs:92 reads the crown line anywhere on the report; `Report_DoesNotRepeatItselfWhenThePlayerWon` ('HOH WINS' count 1). Test with 6 and 14 jurors at both text sizes.
- **Kind:** presentation only.

### M7. The winner screen

- **Serves:** 49, 53
- **Depends on:** M4, M6, and M1's quote helper.
- **Closes:**
  - **A held winner screen.** A new HUD partial (`EpisodeHud.Winner.cs`) is drawn from the Finished branch at `EpisodeDirector.cs:1393`, the way the jury house and final case draw over the station panel. Keep the 'Winner: X. Runner-up: Y.' paragraph (read at FinaleReveal.cs:57), and keep the four `hud.Action` captions live once each on ModalRoot. When the jury reveal releases the HUD at Finished with no panel open, call `TryOpenPhasePanel` so the screen holds. Do not use a separate overlay canvas: one at sorting 105 would cover the notebook that `Review the season` opens and would break FinaleExits and Keyboard.
  - **The WINNER lockup:**
    - `RectPortrait` about 260×320 in Pack 3 `FinaleJury/winner_card_9slice` (new constant), with GlowGold behind and the crown above.
    - 'WINNER' at about 72 pt Bold with a gold gradient, and the name (WithYou) on `winner_nameplate_9slice`.
    - 53's line reads 'WINNER OF ' + `JuryReveal.ShowName`, under the head 'SEASON {n} FINALE'.
    - Laurels only if laurel PNGs are added through `ArtSource/ui-packs/tools/bb_ui_pack7.py` with catalogue entries and full metas; otherwise none.
  - **The count headline:** M4's labels.
  - **2nd place plate.** 'PlaceWord(2) + PLACE' and the name on `runnerup_nameplate_9slice`, with a RectPortrait of about 150×180 and '{n} JURY VOTES' in steel.
  - **Third place** per decisions 27 and 28. Read the third from the final power row's `evicteeId`, not from `Placement == 3`, which falls back to one shared place without a jury ledger.
  - **Quotes.** The first sentence of each finalist's final speech, labelled 'From the final speech', de-duplicated.
  - **THE FINAL JURY VOTE row.** RectPortrait cards of about 110×120 in `juror_card_voted_9slice`, with a `jury_vote_token_9slice` chip; 'You' for the player. The card width scales with the jury: fixed up to 7, then shrinking, then two rows past about 10.
  - **Season numbers panel:** '{contestants.Count} Houseguests', '{week} Weeks in the house', '{ledger.competitions.Count} Competitions held' (equal to the sum of `FinalistRead.Wins`), '{ballots} Jurors'. Do not use the word 'COMPETITIONS', which is Game Sense's caption.
  - **53's per-finalist cards,** built like `FinalistColumnIn`: RectPortrait, the place word tinted gold, steel or bronze, 'HoH wins', 'Veto wins', the final-part line, and 'Jury votes {n} / {m}'. The third's card reads 'Voted for {name}'. Use `FinalistRead.Record` only, never `FinalistRead.Read`, which carries leanings.
  - **Major moves as facts.** Up to two lines per finalist from power rows ('Week 3: held the house; Casey went home (4–1)'). For the player, use their three locked moments.
  - **Ways on:**
    - `Season report` is the primary, with a bar-chart glyph, a 'VIEW FINAL STATS' eyebrow and 'See the full numbers'.
    - `Main menu` gets a 'CONTINUE' eyebrow and a chevron.
    - `Start a new season` and `Review the season` sit in a quieter second row, with M4's two new controls.
  - **Backdrop:** a faint brand line (decision 10) and an optional text lockup in the lower left.
  - **49's live camera hero** per decision 26. If chosen, set a close two-shot `Shot` with a KeyLight on the winner and runner-up (the `FrameForIntroduction` pattern) before the panel opens, and leave a clear middle band.
- **Effort / risk:** L / medium. There are several pins, and the audited walk passes through this panel.
- **Tests and pins:**
  - FinaleExits `FinaleControl` on ModalRoot (:39-48, :66-89).
  - Keyboard's finale ring (:190-197).
  - PortVerification's exactly-one-live-button rule (Season.cs:201-204, 507-521).
  - Add `CaptureFraming("endgame-winner")` and a capture of the reveal's result in EpisodePlayModeTests.FinaleReveal, plus a tie fixture.
- **Kind:** presentation only.

### M8. Finalist cards: the Final 2 choice, and the juror's variant

- **Serves:** 59, 50
- **Closes (59, the shared card in `EpisodeHud.Finalists.cs` FinalistCard :113-168):**
  - **A gold hero head.** FinalTwoChoice (EpisodeDirector.FinalThree.cs:90-128) opens with `hud.ScreenHead` (FreeTime.cs:140), given a gold variant with KitIconCrown: 'CHOOSE WHO JOINS YOU IN THE FINAL 2' and a subtitle. The kept paragraph stays.
  - **Header row.** The portrait on the left. On the right: the name, the archetype on its own italic line in heading blue with age and job muted below, trait chips (`HudPrimitives.Chip` with `CastSelect.TraitTint`, as Decisions.cs:256-260 does), and the intro line in a quote box (`PackArt.SeasonQuote` or a KitCard).
  - **COMPETITION RECORD** stays as the eyebrow, with the grade and 'Confirmed' on its line. Under it: icon rows (KitIconCrown, veto-token, KitIconPerson) with counts, plus a 'Final HoH Part n' row.
  - **YOUR RELATIONSHIP.** The standing word, a bar labelled 'Your standing' (from `Score(player, x)`, the same value as the cast strip), and a blurb from the record only: 'Allied since week n' from the ledger alliance's startedWeek, kept deals, or TowardYou's acts.
  - **JURY READ (n JURORS).** Three count rows (Known support / Bitterness / Uncertain) with KitIconJury tinted Allied, Danger and Muted, and names small under each. The three labels stay in the card's words.
  - **IF YOU TAKE:** the IconTrophy glyph beside the heading. The evidence lines are unchanged.
  - **VS divider.** When the columns sit side by side, widen the gap and add an `ignoreLayout` overlay: a thin vertical glow and 'VS'.
  - **The choice.** Style the headline as a large 'Take {First}'. A SelectionRing glyph inside the `Evict {other}` button fills while the control is selected or hovered. A non-Button component ties each column's gold card edge (PanelSelected) to its button's select and pointer events. A single press still commits.
  - **Warning.** KitIconWarning before each column's warning. An optional derived line under both: '{cut} joins the jury; you and {take} face the jury's questions.'
  - **Fit on one screen.** Fold the certainty legend into a single footnote line and drop HouseStatus on this screen. At 900 tall the viewport is only about 530 units.
- **Closes (50, behind a `JurorCase` flag on FinalistColumn; the final-choice path is unchanged):**
  - A bleed portrait of about 150×200×s on the left and the name in caps at Bold 28.
  - A frame of Pack 3 `finalist_card_9slice` (new constant, with `pixelsPerUnitMultiplier` at card size), tinted Gold for column 0 and Accent for column 1.
  - The quote is the first sentence of the finalist's final speech (the EndScreenKit helper); fall back to the intro line, and show nothing when neither exists.
  - A stats row: Comp wins (`FinalistRead.Wins`), a middle tile per decision 32, and Times on block (`timesNominated`). Glyphs: trophy and people via `HudPrimitives.Glyph`, the shield via `UiTheme.Pack(PackArt.KitIconShield)`.
  - A compact 'YOU AND {NAME}' row (standing word · agreement), with its certainty.
  - The VS column carries 'TWO JOURNEYS. ONE WINNER.'.
  - A crown per decision 33.
- **Effort / risk:** L / medium. There are many pins and the card is shared.
- **Tests and pins:**
  - FinalThree.cs:33-117: the seven FactLabels, 'WHAT YOU KNOW', 'IF YOU TAKE X', headline then warning then control, the warning in view on open at both sizes, one interactable Button per column, side by side with equal widths, and the legend words on the panel.
  - JurorVote test :65-77: 'WHAT THEY DID TO YOU' and 'COMPETITION RECORD' in each card, and no Button inside a case card.
  - Capture the decision screen scrolled to the top.
- **Kind:** presentation only.

### M9. The juror's cases: facts from the record

- **Serves:** 50
- **Depends on:** M8.
- **Closes (pure helpers in `Simulation/FinalistRead.cs`, drawn in the JurorCase card):**
  - **`FinalistRead.Summary(s, id)`:** one or two factual sentences from the grade, wins, nominations survived and weeks holding the house, under a SEASON SUMMARY eyebrow (decision 34).
  - **`FinalistRead.KnownAlliances(s, id)`:**
    - An alliance **confirmed** to the player shows its name, first names and weeks. The player must be a member, or the alliance must be public. The weeks come from `ledger.alliances` startedWeek and endedWeek.
    - A **suspected** alliance shows only 'Heard: {X} and {Y} working together', taken from the fact's two names, with no era.
    - Up to two rows. The chevron is decorative, never a Button.
  - **`FinalistRead.Rivalries(s, id)` from power rows only** (decision 35):
    - people they nominated, or who nominated them, twice or more, or in both directions;
    - 'You' when StandingWord is Hostile or Wary, or TowardYou has acts.
    
    Drawn as chips with the 'target' glyph or KitIconWarning. No lightning art exists.
  - **MAJOR BETRAYALS.** A section that keeps the pinned 'WHAT THEY DID TO YOU' label, with a new PackArt constant for Kit6 `ic_broken_promise`. The rows are the TowardYou items, plus 'Nominated alliance-mate {X} in week N', shown only when the player was in, or publicly knows, an alliance both were in that week. Read membership at week N from the ledger, not from `alliance.members`. Never read NPC-to-NPC promises.
- **Effort / risk:** M / medium, because the knowledge gate is easy to leak.
- **Tests and pins:** EditMode tests for each helper, including a suspected alliance that must not list members and a nomination outside the alliance's weeks. JurorVote pins as in M8.
- **Kind:** presentation only. The helpers read saved state.

### M10. The juror ballot screen

- **Serves:** 50
- **Depends on:** M8. M9 is optional.
- **Closes (`EpisodeDirector.JurorVote.cs` JurorBallot :33-64):**
  - **Layout.** An ActivityLayout of its own: the full stage with no CapContent, like NominationsLayout. Then `BeginColumns(~340)`:
    - main: the cases (with the VS) and the vote card;
    - side: FINALE NOTES, WHAT MATTERS, YOUR QUESTIONS TONIGHT and a brand quote;
    - after `EndColumns`: the JuryRoster.
  - **Profile card.** A RectPortrait of the player, the name, 'Juror' in the accent colour, and 'Your diary persona: {persona} · n reflections' unquoted. The lines at :47-50 move here.
  - **The vote card:**
    - a 'JURY' eyebrow over the pinned 'YOUR VOTE';
    - the line '… Choose who you want to win GAMESIM Season {n}.', using the cached number;
    - a wide `BallotCards` style as a new parameter, since the eviction ballot shares the method: about 360×64, with a crown glyph, the full caption `Vote for {name} to win` at 20 pt SemiBold, tinted gold or accent, no portrait, inside BallotRowName;
    - the `Review final speeches` Disclosure directly under the votes, with KitIconChat or a chevron (there is no play icon);
    - the privacy line as a footnote with KitIconLock. Pass null as ScreenHead's line; the constant's text is unchanged.
  - **FINALE NOTES.** A pure `FinalistRead.FinaleNotes(s)`: the grades compared, who won in which half of the season (from power rows and the final parts), fixed guidance copy, and the season line. The certainty legend moves in here.
  - **WHAT MATTERS TO YOU.** One bar per persona score (Neutral excluded), filled score/max, with High, Medium or Low by thirds. Labels per decision 36; no 'Entertainment'.
  - **YOUR QUESTIONS TONIGHT** (decision 37). For each completed exchange: the finalist's portrait, the player's recorded question and the finalist's recorded answer, labelled by question number. Omitted when questioning was skipped. A player juror's exchanges are only their own two.
  - **JuryRoster,** with a name of its own, not JuryStripName. Portrait, first name, and '(You)' in gold, on Pack 3 `juror_card_9slice`. There is no 'has voted' state before the reveal.
  - **Brand quotes** drawn inside this screen, attributed per decision 10. The CastRail quote card is hidden under Stage.
  - **In-panel tabs** (decision 31): 'The finalists', 'Your vote', 'Final speeches', 'The jury', using `Mark` and `RequestScrollTo`. They hide nothing.
  - **Frame.** While the ballot is up, stand the week chip down the way `HideChromeForFullFrame` does. The brand, objective chip and pill stay.
- **Effort / risk:** M / medium.
- **Tests and pins:**
  - JurorVote test :65-77 and :105.
  - `Vote for {name} to win` at EpisodePlayModeTests.cs:701, Keyboard.cs:334, FinaleReveal.cs:74 and PortVerification.Season.cs:331.
  - Endgame.cs:113 pins the week chip only with no panel open.
  - AssertDecisionCopyFits at both sizes. Capture the top of the screen.
- **Kind:** presentation only.

### M11. The jury questioning panel

- **Serves:** 55
- **Closes (`EpisodeHud.cs` JuryQuestioning :913-986; `EpisodeHud.JuryLive.cs`):**
  - **Asker card.** A RectPortrait of about 150×185×s in a KitCard with `UiTheme.AddGlow`. Under it: the name, 'Juror · Question {n} of {N}' ('Finalist' on the player-juror path), and 'Leads with {trait}'. The heading and the 'asks you' line fold into this card with their words kept. 'You ask {finalist}' stays in the panel's words.
  - **Question panel.** Pack 3 `jury_question_panel_9slice` (new constant), Paper at 20 pt. At the resting text size the card and panel sit side by side, and the column widens to about 38%.
  - **Responses.** A 'YOUR RESPONSE' eyebrow replaces the paragraph (:966). A compact TileStyle: caption SemiBold 14, the line at 13 pt on one or two lines, the risk as a corner chip, a chevron, rows about 52×s. Four, or five on a call receipt, now fit.
  - **Receipt:**
    - a grey-tinted RectPortrait of the juror with a 'WEEK {n}' band (none for a comparison question);
    - '({a} - {b})' from `ledger.power[week].tally` in a sibling text beside 'Jury receipt', never inside it;
    - the receipt line bold via fontStyle;
    - highlights that repeat the receipt's week dropped (JuryLive.cs:163), leaving at most two.
  - **Kicker** (decision 39). A presentation-only line above the question built from the receipt ('Week 4 · you voted to evict them'). The saved question is not touched.
  - **Footer and notes:**
    - a framed footer with KitIconInfo reading 'Choose your response carefully. ' + `JuryHintWords`, with the public-questions disclaimer folded in;
    - 'The jury will react after your answer.' with KitIconJury under the receipt until the commit, then replaced by the reaction line;
    - `Skip remaining questions`, `Prepare your final case` and `The jury house` in one thin row after the live layout, in that order.
- **Effort / risk:** M / low to medium.
- **Tests and pins:**
  - JuryLive test :44-71: three columns at width ≥ 900; exactly one 'Jury question' in the asker column; hint contained; Skip after the live layout.
  - JuryLive :51-52 (A · / B ·) and :105.
  - FinalCase test :159-163 (the grid by name and count) and :169 (`Is.EqualTo(ReceiptLine)`).
  - The captions at JuryLive :57-59 and FinalCase :56-58.
- **Kind:** presentation only.

### M12. The jury house

- **Serves:** 56
- **Closes, in two commits.**
- **(a) Data, S** (`Simulation/JuryHouseRead.cs`, JuryHouseReadTests):
  - **Knows** becomes the exact complement of Missing, using week ≤ leftWeek: the player's competition wins (placement 1) and weeks as HoH or holding a used veto before the juror left, excluding the final-eviction row. For example, 'Your 2 wins and 1 week as Head of Household before they left.'
  - **The goodbye message.** A storyline with templateId `goodbye-message`, cast role EVICTEE equal to the juror, and path optionId classy, tell-why or rub-it-in (nothing for skip) adds 'They watched your goodbye: you kept it classy' to Knows and a dated Highlight. Never the effect it applied.
  - **`JuryHouseRead.Line(state, jurorId)`** per decision 41, in priority order:
    1. at the Final 2, their question to the player, with its recorded note once answered;
    2. their logged eviction plea, dated 'Week n, from the block';
    3. `read.reason`, unquoted.
  - **`FinalArgument.Value(theme)`** phrases for what-matters.
- **(b) Layout, M** (`EpisodeDirector.JuryHouse.cs`, `EpisodeHud.JuryHouse.cs`):
  - **Compact cards.** perRow = min(7, count), wrapping for 8 to 14. A 44 px portrait left of the name and band. One line per section plus 'and n more'. The visible text uses first names (full names where two jurors share one); the object names stay 'Juror · {full name}'.
  - **Status marks.** A 10 px Disc in BandTint before the band word, and KitIconQuestion for Unknown. Open changes from Paper to `UiTheme.Glow` or `Heading`, not Accent (Accent is the card's eyebrow colour). BandTint also colours the JuryLive receipt.
  - **Card sections.** 'MISSED' becomes 'MISSING'. A 14 px glyph goes before each eyebrow. Swayed-by condenses to '{Label} · answers that {caption}'.
  - **Head row.** A left-aligned head with the people glyph, 'SEVEN JURORS · ONE DECISION' (the count in words), and 'Relationships still matter.…'. 'THE JURY HOUSE' stays in ScreenHeadName.
  - **'Observe only'.** A non-interactive Chip with the eye glyph. When `FinalCaseAvailable`, add 'You can't sway the jury from here. Your final case can.'.
  - **Highlights side card.** `BeginColumns(320)`, with the side card 'JURY DISCUSSION HIGHLIGHTS': every juror's highlights merged, newest first, dated by week. At the Final 2, 'Tonight · {juror} asked about week n · took your answer well' rows go on top. Cap at about six plus 'and n more'. Per-card BETWEEN YOU is dropped. No 'View all'.
  - **What matters.** One icon row per theme present, most-held first: ThemeGlyph, the Value phrase, and an 'n of m' chip. Omit any value with no juror behind it.
  - **The 2D callout tableau.** `EpisodeHud.JuryTableau`: callouts on a U-shaped arc, named 'Juror callout · {name}', with no Button. Each has a 44 px portrait, first name, dot and band, one line, and a leader to a seat mark.
  - **Frame.** Lift the 1040 cap. Compact the station band: its title becomes the screen's name, and 'Panel control hint' is hidden as HouseEventLayout does.
- **Effort / risk:** S + M / low.
- **Tests and pins:**
  - EpisodePlayModeTests.JuryHouse.cs:20, 27-34: 'THE JURY HOUSE' in ScreenHeadName, 'Juror · {name}' exact, one CharacterPortraitBinding per card, no Button, the uppercase band word, AssertDecisionCopyFits.
  - Add `CaptureFraming("endgame-jury-house-three")` and a Final 2 capture with finale rules on.
- **Kind:** presentation only.

### M13. The final HoH screens

- **Serves:** 58
- **Closes:**
  - **Part tracker.** A new EpisodeHud partial, `FinalHoHBracket(state)`:
    - done parts: KitIconCheck, 'PART n', 'Winner: {First}' and a face;
    - the current part: '{A} vs {B}' with two faces, highlighted;
    - parts ahead: KitIconLock with 'Final round · Locked', or '{Part 1 winner} vs ?'.
    
    Drawn at the head of `CompetitionBriefing` and `SpectatorBriefing` (EpisodeDirector.Challenge.cs:190, :258). `CompetitionGameScreen.Show` gains an optional bracket model and draws it in the challenge card.
  - **"Winner advances" band.** A gold-edged foot inside the briefing sheet and on the game screen's legend row, with KitIconCrown and the true line for the part:
    - Part 1: 'Winner goes straight to Part 3';
    - Part 2: 'Winner advances to Part 3';
    - Part 3: 'Winner becomes the final Head of Household'.
    
    The explanation stays `FinalPartStakes`. The tagline box is decorative.
  - **Competitor cards.** For a two-entrant part, `BuildField` (CompetitionGameScreen.Chrome.cs:439) draws two cards: a RectPortrait of about 96×110, the name, 'IN THE COMPETITION', trait chips, and a blue or red identity (Accent / Danger). Keep the 'Competition field' label's text (CompetitionInput.cs:98).
  - **Spectator briefing.** The same two cards go under the hero. The hero shows the part's art instead of the spectator's own face. BriefingHeroName and the 'Competing:' paragraph stay.
  - **Challenge card.** Under decision 48A: the game's title and rules, plus 'Highest score wins · statistics and seeded rolls count'. Only the player's real progress bar is drawn.
  - **Live feed on the competition.** `TryGetLiveFeedSubject` (EpisodeDirector.LiveFeed.cs:77-131) gets a competition case: the stations' midpoint while the arena is staged. That view is exempt from the IsPanelOpen pause (:53-56). The card is captioned '{A} and {B} are battling for the Final HoH.'.
- **Effort / risk:** M / low to medium.
- **Tests and pins:** `Watch eligible housemates compete` and `Continue from competition results` each stay on one live button. BriefingHeroName (CompetitionBriefing.cs:64; HaveNots.cs:49, :110). 'Competing: ' (CompetitionBriefing.cs:87). Add a capture of a final part's game screen.
- **Kind:** presentation only.

### M14. The Final 3 house view

- **Serves:** 60
- **Closes:**
  - **Finalist entries.** In `CastRail.Build`, when `foldDeparted` is set and three or fewer chips remain, draw an entry about 300 wide at the same 96 height. The photo is about 72×86, today's words column is unchanged, and a second column lists the traits with TraitTint dots. Feed the width into WideRailWidth and Wide.
  - **Jury card.** At the endgame, CastRail's quote slot (290×88) holds 'The Jury (n)', 'They will decide the winner.' and one row of faces: about 30 px with first names up to 7 jurors, faces only past that. The objectives card's strip stays as the fallback on narrow canvases.
  - **Room names.** EpisodeTravelBeacons gets a non-raycast text pill, `RoomLabels.Title(room)`, faded with the beacon. The captions ('Travel to the kitchen') are unchanged.
  - **Name chips at map distance.** While `IsFinalThree` or `IsFinalTwo` and the camera is past HiddenBelow, draw projected non-raycast chips in the beacons' overlay, one per remaining housemate plus the player, anchored above the head bone rather than the root. The naming follows WithYou: 'Emma (You)'.
  - **Prompt hint.** `EpisodeHud.SetPrompt` takes an optional hint, drawn as a separate muted line under the Talk prompt: 'Talk, strategize, or spend time together.'. The prompt's caption is unchanged.
  - **Live feed caption.** `RoomCaption` names the occupants when a room holds three or fewer, from `WhoIsWhere()`: 'LIVING ROOM · YOU, ALEX, JORDAN'. It says 'talking' only through the existing witnessed path.
  - **Optional:** the pill's jury label reads 'Jury members'.
  - **The jury-house door (decision 42, carried from M3).** M3 did not build it. Build it here on the objectives card's jury strip, per the review correction in section 6: a caption of its own, never a second `The jury house` beside the free tile, and the panel flag F4 names.
- **Effort / risk:** M / low.
- **Tests and pins:** Endgame.cs:97-100 (one rail child per actor with its parts); 'Juror' count (:77, :120); beacon captions.
- **Kind:** presentation only.

### M15. The final case

- **Serves:** 57
- **Closes (a new `EpisodeHud.FinalCase.cs` partial; `EpisodeDirector.FinalCase.cs` FinalCaseScreen :107-158):**
  - **Layout.** `FinalCaseColumns` at about 30/34/36 (narratives, résumé, moment cards), then a full-width tray and Lock row. Lift the cap and compact the station band as in M12.
  - **Header.** Left-aligned with the crown glyph and 'Shape your story. Show the jury why you should win.'. It keeps 'PREPARE YOUR FINAL CASE' and, once locked, 'YOUR ARGUMENT IS LOCKED'.
  - **Header extras:**
    - a non-interactive 'FINAL 2 · Jury review' chip with the gavel glyph, which is not a door (leaving the jury house would not return here);
    - the brand quote in a `PackArt.SeasonQuote` frame, unattributed;
    - numbered `SectionHead` (FreeTime.cs:176) heads 1 to 4.
  - **Selected state.** `MoveTile` gains `Selected`. RowTile draws a PanelSelected edge and a right-hand radio ring, filled when chosen. 'Chosen' stays as the non-colour signal.
  - **Résumé:**
    - a RectPortrait of about 120×150, the name, and 'Final 2';
    - Pack 7 stat cards: Competition wins (`FinalistRead.Wins`), Nominations survived, Weeks in the house (decision 9), and optionally Weeks in power;
    - 'Major moves': up to three hoh:, call:, veto:, whip: or win: moments with their weeks;
    - Key alliances: '{name} with {first names} · weeks 2–5', never `AllianceRow.why`;
    - Betrayals: the counts, then up to three lines, never a break week;
    - Key weeks: up to four, ending 'Week {n} – Reached the Final 2';
    - the quote slot per decision 44.
  - **Moment cards,** about 96 px:
    - the subject's portrait from a new pure `FinalArgument.SubjectOf(state, reference)`;
    - a 'Week n' chip, or 'Season' at 0;
    - a bold title from a new pure `FinalArgument.Title(reference)`;
    - `moment.text` kept as the caption;
    - 'Backs {Label}' as a chip;
    - a radio.
    
    Every moment Button stays active in the page flow (no filtering, no nested ScrollRect), two per row.
  - **Tray.** 'YOUR FINAL {n} MOMENTS' with 'n / required selected'. Slots in selection order show portrait, week and title, never `moment.text`. Placeholders are dimmed. No remove X; if one is added, give it a unique caption. Hidden after the lock.
  - **Gold `Lock final argument`.** A Gold fill, a crown and a chevron. Capitals come from `FontStyles.UpperCase`, so `.text` is unchanged. Next to it: 'Next: deliver your final speech to the jury. Locking is final.'.
  - **Second commit (M): read-only case at three.** A `Prepare your final case` tile beside JuryHouseTile (FreeTimeScreen.cs:259) with its own availability check. The Lock is replaced by 'The lock opens at the Final 2.', and the choices are view state, dropped on close.
- **Effort / risk:** L + M / medium.
- **Tests and pins:** EpisodePlayModeTests.FinalCase.cs:56-60, 72-79 (parts found with IsChildOf), 75-76 (every moment Button active and scrolled into view by the panel), 93, 97-98, 103, 117-118. FindButton matches single TMP text.
- **Kind:** presentation only.

### M16. Season Complete: the new panels

- **Serves:** 48
- **Depends on:** M5, M6.
- **Closes (SeasonReport.Dashboard.cs; new pure classes in Simulation):**
  - **PlayerStatistics(board, state, portrait).** A RectPortrait of about 110, the name, and six icon rows of value and rank: Competition wins, HoH wins, Veto wins, Alliances formed, Betrayals, and Nominations survived (`nominationWeeks.Count`, minus 1 when the evicted week is one of them).
    - The counters and dense ranking go in a pure `SeasonStats` class. Ties print as 'T-' + PlaceWord.
    - By default (decision 17), alliances count only those the player knew (`AllianceCertainty != null`), and betrayals only deals and promises involving the player.
    - Scope the career tests' `Does.Contain("1st"/"2nd")` asserts (SeasonReport tests :236-237; Career.cs:78) to CareerStripName in the same commit.
    - Works for a spectator player.
  - **Score bars.** StatCards is rebuilt as a score list inside PlayerStatistics, still under GameSenseCardName. Each row: icon, the pinned caption, the value, and a bar filled to value/100 in the face's tint (CHANCES TAKEN as taken/offered). FaceLine becomes a second line. The bars in the mockup's slots are SOCIAL, STRATEGY, COMPETITIONS and GAME SENSE (gold, last), per decision 16.
  - **Relationships(board, state, portrait)** per decision 18. Pack 7 section cards, each with a RectPortrait of about 90, a cyan role label and the name. The pickers go in a pure class, tie-broken by name. The betrayal card is worded by direction. Under each card: that person's ballot reason ('Jury vote'), or their logged eviction plea dated 'From the block, week N' (read as WeeklyRecap.Ledger.cs:105-118 does, since `state.evictionSpeeches` is cleared weekly), or a fact line.
  - **Jury blurb.** One real ballot reason attributed by name: '— {Juror}, jury vote'.
  - **Analysts' slot** (decision 15). A `PackArt.SeasonQuote` card under the standings with `WinnerNote(champion)`, counting with `FinalistRead.Wins`, labelled 'From the season's record'.
  - **DiaryReflection.** Parse the last `diary-room` event: strip the prefix, the doubled quote marks and the persona suffix. Attribute it '— {name}, Diary Room, week {e.week}'. If the event has rolled off the 256-row cap, show 'Your last reflection: {persona}, week {n}' from `playerPersona.history`. With no reflections, omit the panel.
  - **SignatureMoments.** The three locked moments (`FinalArgument.Moments` resolving `finalArgument.momentRefs`), or Game Sense's top three. Each has a glyph by `ThemeOfReference` or its face. Add 'Earned {a} of {b} jury votes' for a finalist player. A moment's `said` line shows only when it appears in the committed final speech; otherwise the week. The detail list stays.
  - **Legacy panel.** CareerStrip is restyled under CareerStripName with `Career/career_summary_9slice` and SeasonTrophy. A fact line reads '{winner} is the champion of season {n}' with '{trait} · {trait}'. The five pinned cells and `career.Note()` stay, plus the brand stamp. Past champions per decision 22.
- **Effort / risk:** M to L / medium, because of the knowledge gate and the career pins.
- **Tests and pins:** GameSenseCardName's five captions (EndScreens.cs:80); the GameSense tests (:34-41, including MomentsHeading); the career captions (SeasonReport tests :224-238, Career.cs:77); `Report_DoesNotRepeatItselfWhenThePlayerWon`. EditMode tests for SeasonStats ties, the pickers and the diary parser.
- **Kind:** presentation only. It reads saved state and the career file.

### M17. Season Complete: the one-screen grid and the horizontal timeline

- **Serves:** 48
- **Depends on:** M6, M16.
- **Closes:**
  - **The grid.** Re-grid `Dashboard()` (Dashboard.cs:38-49):
    - left column: Standings;
    - centre: the winner hero, the jury strip and the timeline;
    - right: PlayerStatistics and Relationships;
    - bottom band: DiaryReflection, SignatureMoments and legacy.
    
    Keep every part's GameObject name. Keep the two-column fallback under 1150 wide. Where the grid still cannot fit (16 houseguests at 1.2 text), the dashboard scrolls. The detail sections and house table stay below in the same scroll, reached by the tabs.
  - **Final placements rows.** Rows sized from the cast: about 52 px with 40 px faces up to 10 houseguests, shrinking to about 36 px past that, or two sub-columns.
    - The place number sits in a filled tile tinted Gold, Steel or `UiTheme.Warning`.
    - Places 1 to 3 get a sub-label with its own name, never 'Name': 'WINNER', PlaceWord(2) + ' PLACE', PlaceWord(3) + ' PLACE'.
    - Rows 4 and below drop the pill or keep a small muted 'Jury'.
    - The heading reads 'Final placements'.
  - **Horizontal timeline** when the centre column is wide:
    - one node per week, or only weeks with a titled fact past 8 weeks; the finale node is gold;
    - node titles by the rule in decision 20;
    - facts from `WeeklyRecap.Build(state, week)` while the log holds the week, else from the ledger;
    - two RectPortraits per node (the week's HoH, and the evictee greyed) in `Timeline/timeline_shell_9slice`; the finale shows the winner on gold.
    
    Today's per-week lines stay under TimelineName as node sub-lines or in the detail section.
- **Effort / risk:** L / medium to high. There are many part pins and two text sizes.
- **Tests and pins:**
  - Part names HeroName, StandingsName, JuryColumnName, TimelineName, CareerStripName and GameSenseCardName (EndScreens.cs:67-101).
  - AssertDecisionCopyFits at 1.0 and 1.2.
  - The timeline strings 'HoH: {name}', 'Final HoH: …', 'Part 2: …', 'used on {name}', and no 'Not recorded' (EndScreens.cs:92-97).
  - The scroll holds more than a screen (SeasonReportNavigation.cs:83, :261).
  - Check houses of 3 and 16.
- **Kind:** presentation only.

### M18. The eviction vote board on the screen

- **Serves:** 61, shots 2 and 3. This is CEREMONY-CUTSCENES S8's roster and result block, pulled forward.
- **Closes (screen frame only; the HUD frame stays byte-identical):**
  - **Voter roster** (decision 5A). `VoteReveal.Ballot` gains a VoterId, set in `EpisodeDirector.EvictionBallots` (Ceremony.cs:196-207), presentation only. `Frame.OnScreen` (VoteReveal.cs:523-529) is re-flowed:
    - the faces and counts shrink to the sides (about 160);
    - a centre roster column is added, splitting into two columns past 7 rows (13 at a full house);
    - each row is `vote_reveal_strip_9slice`, with a UMA portrait, the name, and `vote_chip_evict_9slice` reading 'EVICT {target first name}', tinted by the nominee's side (fixed when the card opens);
    - rows appear on each VoteShown, all at once on a skip, and a gold tie-break row after TieCalled;
    - new children 'Ballot', 'Ballot voter', 'Ballot target';
    - 'LIVE EVICTION' stays the title, with 'THE VOTE' as a sub-header. The Pip row stays.
  - **Three-line result block.** At `Result()`, fade the faces, names, counts, VS disc and pips on the screen frame; the objects are kept. A 'Result block' fills the freed band:
    - 'BY A VOTE OF';
    - '{n} TO {m}', with the evictee's count in Danger and the other in its tint;
    - the first name in capitals at about 150 in Danger with a glow;
    - 'IS EVICTED.'
    
    Variants: tie-break 'BY THE HEAD OF HOUSEHOLD'S VOTE', sole vote '1 TO 0', the player 'YOU ARE EVICTED.'. The Host line and the banner stay at the foot with unchanged text. Every box is sized at 1.3 times its font.
- **Effort / risk:** M / medium, because it is a full re-flow of the frame.
- **Tests and pins:** the children 'Votes', 'Pip', 'Host', 'Result' and 'Title'; CeremonyRevealPlayModeTests.cs:219, 319-320, 355-358. Capture the vote on the living screen; none exists today.
- **Kind:** presentation only.

### M19. The eviction goodbye, the walk and the door (before the couches)

- **Serves:** 61, shots 3 to 6. This is CEREMONY-CUTSCENES S4's goodbye, close and hold, done on today's yard door.
- **Closes:**
  - **Result reactions** (CeremonyStage.cs:798-815):
    - move `StandUp(leaving)` into the new Goodbye step;
    - the evictee gets a 2 s LookDown;
    - the survivor stays seated without the Won fist pump;
    - voters whose public ballot named the survivor look down, and the rest turn to the evictee.
  - **Goodbye step,** about 4 s, between Closed and Release (:816-822):
    - `StandUp(evicted)` and SetFacing to the seated house's centroid;
    - every seated body LookAt them for 2 s;
    - `React(Cheered)` on the house, which plays the seated clap;
    - a SeatShot pushing in from 3.1 m to 2.4 m;
    - the goodbye line moves here, and a press skips.
    
    The step counts as Narrating, so the HUD stays held.
  - **Goodbye tone** (decision 6). `GoodbyeLine` (WalkOut.cs:157-170) and the last look are keyed to what the player knows: a live deal turns to you, the player's ballot or nomination against them is cold, and anything else is neutral.
  - **Full-bleed exit.** Keep the reveal hold while a walk-out started from a stage runs: add `walkingOutId != null` to TickCeremonies' revealing test. Release it at FinishWalkOut.
  - **Slow walk.** `RouteDeparture` takes a speed (1.5 m/s here), with a LookAtPoint mark 1.5 m ahead on the floor.
  - **Door closes and holds.** Call `OpeningDoorSet.Close` once the body is past the light plane. Hold 1.2 s on the shut door. Then run `FinishWalkOut` behind it, strike the set under a travel dip (not a same-frame Destroy, WalkOut.cs:134-137), release the camera and stand the house up. A press skips to the shut door.
  - **The evicted player takes a hot seat.** Assign's eviction loop (:331-332) also accepts `id == state.playerId`.
  - **Warm key light.** A warm spot on the hot seats' midpoint from above the screen side (the KeyLight recipe, Opening.cs:364-383), on from the arrival to the Goodbye step and destroyed in End.
  - **Optional:** Resources `eviction_night_card.png` on the sting during the summons wide.
- **Effort / risk:** M / medium.
- **Tests and pins:** extend the WalkOut tests (door shut at FinishWalkOut, the camera on the door). Add a PlayMode case where the player is the evicted nominee: both hot seats held when the card starts. Re-pin CeremonyTruthTests.cs:106-147.
- **Kind:** cut-scene, presentation only.

### M20. Couches, part 1 (S1): the living-room pieces imported

- **Serves:** 61, and later 52, 54 and 51
- **Closes:**
  - **Import.** Import `bb_set_lounge4`, `bb_set_lounge3` and `bb_set_wingback` on the D: copy, with importer-written metas, and commit them. The seating tests' preview scene is dressed with them.
  - **Art pass** inside each piece's own script, checked with bb_look renders:
    - the wingback: a bevelled, channel-tufted back, rolled arms and a velvet sheen. It must read red; Pack 6 has no red velvet, so use the script's own material;
    - the couches: bevelled arms and cushion edges, and a softer back.
  - **Upholstery.** Cream `fabric_cream_weave` through `HouseRoomFinish.Skin`. Optional teal accents at the U's front corners: `bb_set_armchair` or a `fabric_teal_weave` skin.
  - **Low round table.** A new piece or a variant of `bb_set_roundtable`: 0.8 to 1.0 m across, 0.45 m high, dark glass with a brass rim.
- **Coordination:** another session authored these files today. Agree who owns them first.
- **Effort / risk:** M / low.
- **Kind:** Blender art, no rebake.

### M21. Couches, part 2 (S2): the U, the red chairs, the table and the rug, and the rebake

- **Serves:** 61
- **Depends on:** M20, decisions 1 and 2. Tell the story session before this lands.
- **Closes (the scene; `HouseSetPieces`; `HouseRoomFinish.LivingRoom`):**
  - **The U.** Two lounge4 as the base at z -2.9 facing south, x ≈ -9.8 and -6.2, and a lounge3 on each arm facing in, x ≈ -11.06 and -4.94, z -3.8 to -6.5. That is 14 couch seats.
  - **Red wingbacks.** Two at (-8.55, -7.3) and (-7.45, -7.3), facing north into the U (under decision 1A). They are approached from their outer sides (x ≈ -9.5 and -6.5), because the table's baked edge overlaps the chairs' front erosion. Record this departure in CEREMONY-CUTSCENES 8.2.3.
  - **Table and lanes.** The low table at about (-8, -5.6) with a layer-8 proxy (it is above SolidHeight 0.40), keeping a 0.8 m lane to every approach. About 2.3 m behind the base and 4.5 m east of the east arm stay clear for traffic.
  - **Strike list:**
    - the prototype sofa, coffee table and TV boxes;
    - `rugSquare` → `bb_set_rug` at (-9.52, -8.0), which lies under the red-chair zone;
    - the prototype 'Living rug';
    - the swirl rug, via HideRugsUnder.
    
    Rekey the room-finish rug and cluster block (:270-294), which runs only when the sofa is present, to the U. Lay a Pack 6 navy rug of about 6 m under the U, and put the finish cluster and a plant on the table.
  - **The living-room screen**, authored on the chosen wall:
    - its own idle board from `display_idle_gamesim.png` with the house-geometry chevron above;
    - the gold linework frame;
    - the oak band and both Feature strips re-centred on it and re-tinted warm gold (the linework's 1.00, 0.85, 0.45);
    - a plant and a table lamp at each end, and a glow strip along the stage's front edge;
    - optionally, a baked, disabled `display_eviction` quad.
  - **Neon.** 'SAME HOUSE. DIFFERENT STORIES.' (Pack 5 neon mask) on the east partition or the south wall, clear of the memory wall (x -14, z -7.3 to -2.7) and the planned door (z -9.6 to -7.4).
  - **Markers.** Move the living marker (-5, 0, -7), the dance anchor and living-east-chat. The marker sits inside the east arm's bake erosion.
  - **Rebake and audits:** start-spot audit, 28 of 28 room pairs, no seat proxy backed off, and an anchor audit proving approaches at about 0.85 to 0.95 m.
  - **Jury placement fix.** `JuryPlaces` uses HasCapsuleClearance, which skips furniture. Test layer 8 explicitly so finale-night jurors are never placed inside a couch.
- **Effort / risk:** XL / high: parked agents on approaches, the story session's ring moving, and the audited pipeline.
- **Kind:** Blender set pieces, the scene and a NavMesh rebake.

*As built (M20-M21):* see CEREMONY-CUTSCENES-PLAN 8.2.3's as-built note. The arms sit clear of the
base's ends with an open corner. The chairs are approached from their outer sides. The pieces are
`bb_set_lounge4`, `bb_set_lounge3`, `bb_set_wingback` and `bb_set_lowtable`, the last its own script
per section 6 correction 8. The whole pass runs by `-executeMethod
Gamesim.Editor.HouseLivingGallery.BuildFromCommandLine`: the pieces, collision, resolve and NavMesh
bake, then the room finish, then the lighting and its bake.

### M22. Couches, part 3 (S3): the whole house seated for the eviction

- **Serves:** 61
- **Depends on:** M21, decision 3.
- **Closes:**
  - **CeremonySets as anchors only.** `CeremonySets.DressTheLivingRoom` loses the hot-seat clones (:294-312) and the sofa turn (:314-350). Living-mark rows are used only past 14.
  - **Venues.** One `gallery-seat` venue with 14 slots, filled centre-out on the base and then the arms. A `finalist-seat` venue with 2 slots on the wingbacks, shared by eviction nominees and, later, the finale's finalists.
  - **Assign's eviction case** (CeremonyStage.cs:322-360). The nominees go to the wingbacks by id: `departingId` and the player (from M19). Under decision 3B the HoH takes a gallery seat, StandingId stays null, and TieCalled/TieBroken cuts to the HoH's SeatShot.
  - **Shots:**
    - `Wide()` aims at the wingbacks' midpoint at 1.0 m from behind the U's base: about 5.5 m out, pitch 10 to 14°, 40° lens, depth of field on the chairs;
    - an arrival beat: a PairShot held 1.2 s when the last nominee settles, before StartCard;
    - an over-the-house screen shot (camera behind the base at about 1.3 m, depth of field on the board) on Opened, alternating with the head-on cut on each vote.
- **Effort / risk:** L / medium.
- **Tests and pins:** re-pin the full-house tests to fourteen on the U, two in red, and every line seated. Take two captures from above that match the storyboard's first frames.
- **Kind:** cut-scene, presentation only.

### M23. The exit door in the living room (S4)

- **Serves:** 61, shots 5 and 6, and later 51-6 and 52-1
- **Depends on:** M19, M21, decision 4.
- **Closes:**
  - **Door set origin.** `OpeningDoorSet.Build` gets an origin (root offset). DoorCentre, ApertureMinZ and ApertureMaxZ, VestibuleFarX, and the stage's marks and tests become origin-relative.
  - **Placement.** The door goes at DoorCentre (-12.6, 0, -8.5) on the west wall, with the aperture at z -9.6 to -7.4. Hide the speaker (-13.02, -7.6) and plantSmall2 (-12.04, -9.2). Probe the door mark's floor on D: first.
  - **Dressing:**
    - `UiTheme.Icon("crown")` replaces the eye emblem above the lintel;
    - warm gold glow strips on the jambs and a floor strip;
    - a clone of the scene's samehouse sign on the facade;
    - a plant each side and small emissive sconce quads;
    - a `TO THE JURY →` variant through BuildSign's parameters, used by M29.
  - **The walk.** The exit wide goes over the seated heads looking west, with heads following the body. The last look comes 1.4 m short of the door. `Open` plays without the flash, then the push-in, then M19's close and hold. The yard door is kept as a fallback behind a switch.
- **Effort / risk:** L / medium.
- **Tests and pins:** the walk-out tests extended; three captures; the audited walk's eviction frames.
- **Kind:** cut-scene with a renderer-only runtime set: no colliders, no rebake.

### M24. Nomination ceremony beats on today's set

- **Serves:** 63
- **Closes:**
  - **HoH shot.** On the Opened beat (CeremonyStage.cs:681-684), cut to `SeatShot(StandingId)`, or to an over-the-shoulder shot from the nearest seat. `SetTalking(true)` on the HoH for KeyIntro, then off. No subtitles (decision 66).
  - **Every safe key pumps.** Drop the `last` condition at :970.
  - **Block two-shot.** At the end of the last key's hold, after Relief, cut to `GroupShot(block)` with both nominees in their own seats. Seating stays independent of the outcome.
  - **Screen never blocked.** Clamp `ScreenSurface.Shot`'s distance to 0.35 m in front of the nearest standing place inside the frustum, or lay the head marks at least half the frame's width off the axis. Add the rule to S3's head-mark validation.
  - **Branded screen frame** (KeyCeremony, screen frame only): a navy ground in `display_idle_gamesim.png`'s colours, and the house glyph plus a small GAMESIM wordmark in place of the trophy. Keep the object named 'Title mark' and the 'NOMINATION CEREMONY' text. Dim `glow_screen` while a card is mounted.
  - **The nomination room's board.** An editor room-finish pass: swap `nomination_idle_display` for `display_idle_gamesim` at `HouseRoomFinish.cs:437`, tint `neon_frame` blue at placement, and add gold strips from `gold_linework_mask` flanking the stage.
- **Effort / risk:** M / low.
- **Tests and pins:** captures of `ceremony-stage-key-screen` and `-block` with no body in the screen cut.
- **Kind:** cut-scene, plus an editor pass that changes the scene file (no NavMesh change).

### M25. The veto meeting on today's set, and veto signage in the yard

- **Serves:** 62. This is an interim before S6.
- **Closes:**
  - **Meeting staged.** Route VetoKind through `TryBeginCeremonyStage` (EpisodeDirector.cs:818-830) with the generic card as playCard. `CeremonyStageRoom` gets a veto case, 'Nomination', so Assign's dormant VetoKind branch (:302-320) runs: the HoH on head 0 and the holder on head 1.
  - **Beats.** Subscribe the stage to `takeover.BeatReached`; CeremonyTakeover already raises Opened and Closed (:120, 308, 319). OnBeat gets a veto case: Opened cuts to the wide, then a pair shot of the pre-commit block; Closed releases.
  - **Pre-commit block.** Capture it as a List. `wasNominated` is a HashSet with no order.
  - **Yard signage at the veto.** While the veto arena is staged, hide the `bb_set_sign_hoh` renderers and swap the 'Yard display' to `display_veto_competition`; restore both in `EndCompetitionArena`. The Pack 5 displays live under Art/Packs, not Resources, so pass them as serialized references on EpisodeDirector or bake a disabled quad.
- **Effort / risk:** M / medium.
- **Tests and pins:** EpisodePlayModeTests.CameraPhase4.cs:66 (today's Game-room framing) moves, and the Live Feed label follows (LiveFeed.cs:115). Capture a staged meeting at both outcomes.
- **Kind:** cut-scene, presentation only.

### M26. Endgame reactions and the memory wall

- **Serves:** 51, 52, 54
- **Closes:**
  - **Crowning.** During the FinalHoHCrowned card, move to a medium shot of the HoH's body and return as FrameCeremonyRoutine does. A CeremonyTakeover hook attaches a seeded 3 s ConfettiBurst to its root for FinalHoHCrownedKind, never under reduced motion.
  - **FINALIST badge.** 'FINAL 2' at Ceremony.cs:77 becomes 'FINALIST'. Add `React(chosen, Won)`, and turn heads to the chosen, then to the third.
  - **Reveal reactions.** On finale night, `TurnHeads` counts the RunnerUp and the benched Jury bodies, with the winner as the target. At ShowingResult, `React(Cheered)` on the jurors and the runner-up; seated bodies clap.
  - **Memory wall states:**
    - `MemoryWall.Refresh(state, revealed)`: the Winner gold and the RunnerUp lit once the reveal has shown its result, dim before that;
    - out frames at about 0.55 grey, not near-black (MemoryWall.cs:33, :75). A true greyscale blit is optional; it must restore `RenderTexture.active` and re-blit on rebind;
    - `hoh_crown_wall.png` hung as a decal above the wall in `HouseRoomFinish.LivingRoom`.
  - **Part 1 winner celebrates.** For final parts, keep the arena up through the result and end it on the standings' Continue. `React(winner, Won)`, the others Cheered or a wired `Clap_loop`, TurnHeads, and a cut to the winner's station for the card's first 2 s.
  - **Stakes lines.** 'LAST SPOT STANDING' (Part 2) and 'THE FINAL SHOWDOWN' (Part 3) as fixed stakes lines under the real game title, never in the title slot.
  - **Memory-wall shot.** `FrameMemoryWall()` in EpisodeDirector.CeremonyFraming, framed from the cached MemoryWall component's lit frames, with the eye inside the living room clear of the planter (use `probe-lr-west.png`'s angle). It holds while the FinalThree card plays. Timing per decision 56.
- **Effort / risk:** M / low.
- **Tests and pins:** re-pin FinaleReveal.cs:132. `Finale_TheHouseDownToThreeOpensWithTheFinalThreeCard` (FinaleReveal.cs:88) is unchanged under the default. Add a memory-wall state test.
- **Kind:** cut-scene, presentation only, plus one decal in the editor pass.

### M27. Final parts in the yard: lane marks and the watch beat

- **Serves:** 54, 58
- **Depends on:** decision 52. M13 recommended first.
- **Closes:**
  - **Lane marks.** In `BeginCompetitionArena`, for a final part, try one mark per authored lane at the lane's x behind its stacking task, facing the house camera (HouseSetPieces.cs:740-754). Part 1 uses all three lanes, Parts 2 and 3 use red and blue, and the sitter-out goes to the lounger. Fall back to sampled spots. Verify with HouseYardProbe and a capture.
  - **Spectator arena mode.**
    - The arena gets a spectator branch: no player station, the field at their lanes, and an Active player who sits out on the lounger. A juror player stays unstaged, with the camera only.
    - The arena is staged while `SpectatorBriefing` is open; the full-frame layout already frames it.
    - Pressing `Watch eligible housemates compete` holds 6 to 10 s on the stations (a press skips), then commits Advance exactly as today.
    - Batch runs and reduced motion skip it.
- **Effort / risk:** M to L / medium. Commit timing must not change.
- **Tests and pins:** `Watch eligible housemates compete` and `Continue from competition results` each stay on one live button; the audited walk presses both. Capture the spectator path.
- **Kind:** cut-scene, presentation only.

### M28. Takeovers on the living-room screen, and the endgame gatherings

- **Serves:** 54, 51; a prerequisite for 62's S6
- **Closes:**
  - **Takeover screen path.** `CeremonyTakeover.Play(…, ScreenSurface)` gets an overload with a `Frame.OnScreen` (the VoteReveal.cs:523 and KeyCeremony.cs:167 patterns), per-face beats beside Opened and Closed, and a longer hold on a screen. `CeremonyStage.Subscribe` listens to it.
  - **Part 3 announced in the living room** (decision 58). The three are seated on sofa-seat 0 to 2 today, or on the U after M22. Shots: a wide over their shoulders to the screen, Screen.Shot on Opened, then a GroupShot.
  - **The Final Three card on the living screen,** with M26's memory-wall shot.
  - **Trust talks** (decision 57). A card-less timed stage mode, `final-three-gather`, mapped to Living. The three sit, and a GroupShot holds 6 to 8 s with SetTalking cycling, then Endgame Preparation opens. A press skips.
  - **Reflection** (decision 59). A skippable 6 s beat: cut to the HoH in the HoH suite, re-texture two 'HoH photo' decals per renderer with the candidates' portraits (the `MemoryWall.SetTexture` pattern), and push in slowly over the shoulder.
  - **Optional living-screen idle by phase.** `display_final_three` from the window at three through the final eviction; `display_winner` only after ShowingResult. Both need serialized references.
- **Effort / risk:** L / medium.
- **Kind:** cut-scene, presentation only.

### M29. The final eviction staged, and the walk to the jury

- **Serves:** 51, shots 4 to 6. This is ENDGAME F7.6 and F7.7.
- **Depends on:** M28, M22, M23.
- **Closes:**
  - **A staged branch for a kind with no reveal** in the ceremony dispatch (EpisodeDirector.cs:818-848): `TryBeginCeremonyStage(kind, committed, screen => takeover.Play(…, screen))`, falling back to `PlayGenericCeremonyCard`.
  - **Gates and Assign.** Narrow the FinaleNight gate (CeremonyStage.cs:97) for final-eviction, and map it to Living. An Assign case puts the HoH on the head mark (or per decision 3) and the two on marks in front of the screen, facing the HoH. OnBeat routes the takeover's beats.
  - **Shots:** PairShot, Screen.Shot, a SeatShot on the chosen, then the third.
  - **The walk to the jury.** Narrow the walk-out gate (WalkOut.cs:67-69) for the final eviction's juror only, with the `TO THE JURY →` sign. The line is neutral or 'dealt' only, never warm or glaring from the hidden score. The body stays off the bench until the finale walk-in.
- **Effort / risk:** L / medium.
- **Tests and pins:** re-pin `Finale_TheFinalHoHsChoiceIsACardAndTheNewJurorStaysForIt` (FinaleReveal.cs:108-156). Reduced motion keeps the straight move to the bench.
- **Kind:** cut-scene, presentation only.

### M30. Finale night on the set

- **Serves:** 52, 55. This is ENDGAME F7.8.
- **Depends on:** M29, M22, M23. Decisions 38, 61 and 62.
- **Closes:**
  - **A finale set that persists across commits.** A stage ends at every commit (CeremonyStage.cs:27-29).
    - At the first JuryQuestioning render, bind the jurors for the night and walk them in through the door to gallery seats in jury order. The ring bench is the fallback past the seats and under reduced motion.
    - The two finalists go to the wingbacks, facing the U.
    - After each commit, re-place and re-seat everyone, as StandTheJury does.
    - The player-juror's body per decision 61. The player's body gets a plate on finale night.
  - **Questioning in the room:**
    - SetTalking and a SeatShot on the questioner; on the answer's commit, a cut to the finalist;
    - the asker highlighted by focusing the rig on `BodyFor(questionerId)`, so `FollowedId` drives Spotlit and FollowRing. Clear the subject afterwards. `Spotlit` is rewritten every frame, so never write it directly;
    - a shot from behind the wingbacks toward the U.
  - **`JuryStage` lower-third layout** (decision 38). The modal is anchored to the bottom 40% with no scrim above, and a window offset like ArenaWindowOffset. The three columns are kept, with Skip and the doors in a thin row. Reduced motion and batch runs keep today's panel.
  - **Pleas from the chairs.** When each speech is in, and when the jury phase opens, a SeatShot on the speaker with SitTalk. The speech's first sentence goes on the strip under a new presentation-only sting kind, and the jury's heads turn to the speaker.
  - **Opening beat** (decision 62A). Each finalist's recorded meet-and-greet line on the strip.
- **Effort / risk:** XL / high.
- **Tests and pins:** re-pin `Jury_FinaleNightHasTheJuryInTheLivingRoom` (WalkOut tests :295). The JuryLive pins as in M11.
- **Kind:** cut-scene, presentation only.

### M31. The hidden vote and the reveal on the screen

- **Serves:** 52, shots 5 and 6
- **Depends on:** M30. The screen path alone can land after M28.
- **Closes:**
  - **The reveal on the living screen.** `JuryReveal.Play` gains a ScreenSurface and a `Frame.OnScreen`: the winner's portrait large, the name, and the count labels around the pinned ones. It raises `BeatReached` for Opened, VoteShown, LastVotePending, TieCalled, ResultShown and Closed. The finale stage cuts between the screen, the juror being read and the finalists.
  - **The hidden vote** (decision 63), a stage between the Jury commit and `juryReveal.Play`. Each juror, in JuryOrder, rises, walks to the box on the screen stage's end, faces it for 1 s, returns and sits, while the strip reads '{name} has voted.'. A press skips to the reveal. Start with a renderer-only primitive box; an authored `bb_set_ballotbox` comes later, and a floor box would need a layer-8 proxy and a rebake. Batch runs and reduced motion get a card of '{name} has voted.' lines.
  - **In-room celebration.** A world-space confetti burst over the living set with the card's seed and 3 s length, never under reduced motion. The screen's idle becomes the winner board after the result.
- **Effort / risk:** L / medium.
- **Tests and pins:** the Host, Progress and Result labels (JuryReveal.cs:452-463); CeremonyTruthTests' jury pacing unchanged.
- **Kind:** cut-scene, presentation only.

### M32. The nomination room's couch circle, and the crossing to the red chairs

- **Serves:** 63. This is the nomination half of S1 to S3, with a second rebake.
- **Depends on:** decisions 2, 64 and 68.
- **Closes:**
  - **Arc sofa.** Author `bb_set_loungearc3`: a 52.8° arc, three seats at 0.72 m, chord-box proxies.
  - **The circle.** Five arcs round (0.075, -14.5), with the seat ring at 2.4 m, the approach ring at 3.55 m, and a 96° mouth toward the screen. Teal `bb_set_armchair` accents go between them, with the coral cushion swapped.
  - **The table.** Lowered to 0.45 m with its proxy refitted. The top is a near-black gloss re-skin via `Skin` (`dark_marble_detail.png` is a pinstripe, not marble). Add a brass rim torus in `bb_set_roundtable.py`, and `bb_set_plantsmall` at the centre.
  - **Red chairs.** Two wingbacks in the mouth at (-1.2, -14.75) and (1.35, -14.75), approached from behind.
  - **Anchors and bake.** `CeremonySets.DressTheTable` becomes anchors only. Rebake.
  - **The crossing** (decision 68A). A stage tail after the card: the seats exit, the leases re-join the wingbacks, and a PairShot holds for up to 10 s.
- **Effort / risk:** XL / high.
- **Tests and pins:** re-pin the full-house table tests to fifteen seated round the circle.
- **Kind:** Blender set pieces, the scene and a NavMesh rebake; then a cut-scene.

### M33. The keys: keybox and walk-ups (S5)

- **Serves:** 63, and 62-1's draw box
- **Depends on:** M32. Decisions 65 and 67.
- **Closes:**
  - **Props.** A renderer-only keybox carried on the HoH's forearms, driven by one follower component. A gold key re-parented to the right hand, then to the lap.
  - **Walk-ups.** `RouteCeremonyActor` from chair to take mark to chair. `CeremonyPace.Staged`, and KeyCeremony `HoldBefore` capped at 4 s. A profile two-shot of the take. A walk-up column in the stage report.
  - **Gating.** Off under reduced motion, in batch runs and at the quick pace.
  - **Group raise.** The house raises its keys on LastKeyPending (S8).
  - **Draw box.** The same box as the veto draw box, per decision 72.
- **Effort / risk:** XL / high.
- **Kind:** cut-scene with renderer-only props.

### M34. The veto on the set (S6)

- **Serves:** 62
- **Depends on:** M33, M22, M28. Decisions 69 to 72.
- **Closes:**
  - **The lineup.** A `living-line` venue of six standing anchors in front of the living screen. The veto-selection commit goes through TryBeginCeremonyStage (Living), with the takeover on the screen and per-face beats. `display_veto_competition` is on the board for the reveal. The house sits on the U and plays Cheered as a seated clap. The living room is dressed at season start. The HoH holds the draw box only when a draw happens.
  - **The meeting:**
    - the pre-commit block in the wingbacks, the holder on head 1 between them, the HoH aside;
    - pleas as two-shots of each nominee and the holder, with no subtitle (decision 69).
  - **The veto necklace.** A transient piece built from ProceduralAccessories' Drape plus a key pendant, attached with GrownPiece under its own root name. It never goes through the saved Neckwear slot, so it is never saved and a re-dress never strips it. Re-attach it after a body rebuild. The winner wears it at the result.
  - **Box and outcome.** `bb_set_vetobox` and `bb_set_stand` on the screen stage's end. The necklace goes into the box, or onto the saved nominee. In a stage tail, the saved nominee walks to a seat and the replacement walks to the freed wingback.
  - **HoH crown pendant** per decision 71, using the same code.
- **Effort / risk:** L / medium.
- **Kind:** cut-scene with Blender props.

### M35. The yard set (S7)

- **Serves:** 58, 62, 54
- **Depends on:** decision 55. It takes its own rebake.
- **Closes:**
  - **Stations.** Either authored stations (a podium with a key icon tinted per lane; six on the podium row) or the existing podiums reused. Per-part stations (`bb_set_station_endurance`, `bb_set_station_console`) are enabled by the part's category. The arena binds stations by index, with sampling kept as the fallback above six.
  - **Yard screen.** A yard ScreenSurface: `TryFind` accepts a named board, or a ceremony-screen board goes on the backdrop. The bracket and crowning cards play on it with Screen.Shot cuts. It mirrors the clock (timed kinds only), the category and the title, and shows no NPC progress.
  - **Winner's beat in the arena.** The arena stays up until the result card closes, with Won and Cheered, 'VETO WINNER' or the crowning on the screen, and the key prop.
  - **Gantry.** A variant reading FINAL HOH, or a runtime crown sprite.
  - **Props.** Generate UVs on the station disc. Pass `buzzer_station_symbol` in by serialized reference.
- **Effort / risk:** XL / high.
- **Kind:** Blender set pieces, the scene and a NavMesh rebake; then a cut-scene.

### M36. Motion takes

- **Serves:** 61, 52, 51, 63
- **Closes (each through the mocap route: import on D:, TakeProbe, copy the clips back):**
  - 'Sitting Nervous' as the hot-seat idle. Until then, LookDown in the summons' last seconds.
  - A hands-to-face Shocked take (`bb_anim_React_shocked`, wiring ReactShocked) for the winner and the chosen finalist, before Won.
  - Tearful, for the third at the final eviction.
  - A seated lean-in or arm-round, on seat neighbours or random pairs, never chosen by NPC relationships.
  - Hand on heart for the goodbye. A puzzle take for the stations. A heavy walk.
- **Effort / risk:** M each / low.
- **Kind:** animation, presentation only.

---

## 3. Conflicts

These mockup elements break a rule. Each has a stand-in, and the slice that builds it is in brackets.

| Mockup | Element | Rule it breaks | What stands in |
|---|---|---|---|
| 48 | Rail relabelled: Season Results, Season Legacy, Episode Archive, The Jury; Review Season Story, Watch Final Episode, Export Share Card | Captions; no source for the archive, share card or final-episode replay | The six-row rail kept, dimmed, with the sheet beside it (M5). Those three wait on decisions 24 and 25. |
| 48 | 'Alliances. Betrayals. A Winner. A Legacy.' | Knowledge gate: not true of every season | The factual summary line, plus a line built from counts that drops any zero clause, or 'Same house. Different stories.' (M5, decision 10) |
| 48, 49, 53, 56, 57, 59, 60 | '28 Days', 'Day 28' | Numbers need a source | Weeks (decision 9) |
| 48 | '12 Episodes' | The game has no episode unit | 'Competitions held', or jurors (decision 14) |
| 48 | Quote signed 'The Gamesim Analysts' | Invented speaker and quote | WinnerNote from the winner's record, 'From the season's record' (M16) |
| 48 | Today's runner-up and verdict cards, which the mockup lacks | Pinned by tests | Folded into the Final jury vote panel, lines moved word for word (M6) |
| 48 | 'Emotional Control', 'Legacy Score' | No source | Game Sense's Competitions and overall scores (decision 16) |
| 48 | Blurb signed 'THE JURY' | Invented collective quote | One real ballot reason, '— {Juror}, jury vote' (M16) |
| 48 | Quotes under the relationship cards | Invented quotes | That person's ballot reason, their logged plea with its week, or a fact line (M16) |
| 48 | 'A strategic mastermind, a social force' | Invented praise | '{winner} is the champion of season {n}' and their traits (M16) |
| 48 | Quotes under the signature moments | Invented quotes | A moment's line only when it survives in the committed final speech; otherwise the week (M16) |
| 48 | 'Continue to Legacy' | No legacy screen; caption contract | The pinned `Main menu` wearing that face (decision 21) |
| 48 | The same numbers in the hero and Player Statistics when the player won | The report's no-repeat rule and its test | One block gives way (decision 19) |
| 48 | Timeline titles 'First Betrayal', 'Power Shift', 'Double Eviction' | Invented; no double eviction is ever recorded | Titles by rule from records (decision 20, M17) |
| 48 | Scene stills in the timeline | UMA only; no stills are recorded | Two UMA portraits per week (M17) |
| 48 | Night exterior of the house | No such set; no photo art | A brand line over a vignette (decision 23) |
| 48 | The mockup's own numbers | Inconsistent: 8 houseguests but 7 jurors plus 2nd and 3rd place | Copy nothing numeric |
| 49 | '7 Major Twists' | No season twist in state | Dropped, or a real count (decision 12) |
| 49, 53 | Taglines, 'Countless Relationships', the closing quote signed GAMESIM | Brand copy dressed as fact | Real counts ('{n} Alliances formed', '{n} Evictions'); brand lines only as approved decoration (decision 10) |
| 49 | 'View Final Stats' | The caption is `Season report` | Kept, with a 'VIEW FINAL STATS' eyebrow and subtitle (M7) |
| 49 | 'Continue' | The caption is `Main menu` | Kept, with a 'CONTINUE' eyebrow and a chevron (M7) |
| 49, 53 | Only three ways on (49) or one (53) | FinaleExits, Keyboard and the audited walk need all four on the panel | All four kept; two in a quieter row (M7) |
| 53 | Three finalists, 'JORDAN 0 JURY VOTES', '3 Finalists' | Game format: the third is a juror and votes | '3RD PLACE · Sent to the jury by {HoH}, week N · Voted for {name}' (decision 27) |
| 53 | Third place's farewell quote | No farewell is recorded | None, or their logged plea with its week (decision 28) |
| 53 | 'Major moves' prose | Invented characterisation | Up to two factual lines from power rows; the player's locked moments (M7) |
| 53 | A finale-specific sidebar | Captions | The rail kept; finale destinations on the winner screen's second row (M7) |
| 53 | 'View jury roundtable' | Invented NPC-to-NPC talk | `The jury's questions`, from juryExchanges (M4) |
| 53 | 'Continue to Season Recap' | No such caption | `Season report`, decorated (decision 29) |
| 49, 53 | A live audience | UMA only, no stand-ins | The jury is the crowd (decision 30) |
| 50 | The juror's own quote | No recorded source | 'Your diary persona: X · n reflections', unquoted (M10) |
| 50 | A finale navigation rail | Captions | The rail kept; in-panel tabs (decision 31) |
| 50 | '21 Days Survived' | No source | A public count (decision 32) |
| 50 | 'Vote for Emma' | The caption is `Vote for {name} to win` | Wide buttons carrying the full caption (M10) |
| 50 | 'Recent jury discussion', with '2m ago' | Invented quotes, true sentiment, NPC-to-NPC talk; no data | The player's own questions and the finalists' recorded answers, by number (decision 37) |
| 50 | Cases without relationship, agreement or certainty | ENDGAME principle 2 | Kept, compactly: 'YOU AND {NAME}' and a certainty tag per section (M8) |
| 50 | An evaluative season summary | Invented | Templated factual sentences (decision 34) |
| 50 | 'Rivalry with Riley: broke trust in Week 4' | Grudges are private | Rivalries from public ceremonies (decision 35) |
| 50 | 'Voted out Quinn after promising safety' | NPC-to-NPC promises are private | Acts against the player, plus a nominated alliance-mate for confirmed alliances only (M9) |
| 50 | Alliance members and era for any alliance | A heard-of alliance's membership is private | Full rows only when confirmed; 'Heard: X and Y working together' otherwise (M9) |
| 50 | An 'Entertainment' value bar | No source | Dropped (decision 36) |
| 50 | A crown over one finalist | Reads as the favourite | Only on the final HoH, captioned (decision 33) |
| 55 | Receipt words bolded inside the question | The saved question is validated word for word (schema 21) | A separate kicker above the question (decision 39) |
| 55 | 'A big move that shifted the power in the house.' | Invented quote | The week's recap headline, unquoted, only when it adds (M11) |
| 56 | Callout quotes ('Still upset about how they were evicted.') | Invented quotes; true sentiment | Recorded words or the band's reason (decision 41) |
| 56 | 'Leaning' | ENDGAME decision 5 | 'Wavering', amber, as built |
| 56 | Highlights of NPC chatter and feelings stamped 'Day 28 8:14 PM' | NPC-to-NPC events, sentiment, no clock | Recorded lines dated by week, and 'Tonight' for the finale (M12) |
| 56, 57, 60 | Rails relabelled ('The jury', 'Final HOH', 'Prepare final case') | Captions | Rails kept; doors on cards and tiles (decisions 42, 49) |
| 57 | Narratives 'Adaptability', 'Underdog Survival' | Captions, saved theme keys, the jury term | The five built narratives, with the mockup's look (decision 43) |
| 57 | 'Adapt, connect, survive. That's my game.' | Invented quote | The claim unquoted, then the locked speech's opening (decision 44) |
| 57 | Moment photos | UMA only; no frames are recorded | The moment's subject's UMA portrait (M15) |
| 57 | 'Turned on Jordan' as a signature moment | Widens the committed schema-21 validator and the jury score | Not offered (decision 45) |
| 58 | Live NPC progress bars '3 / 5' | NPC scores exist only after the commit | The player's real bar; NPC scores on the result card (decision 53) |
| 58 | 'Press your luck, first to 5' | Engine scoring | The authored game's card (decision 48) |
| 58 | 'Choose who sits in the final chair' copy for Part 2 | Untrue for Part 2 | `FinalPartStakes` per part (M13) |
| 58 | HUD kept around a centred yard | The competition layout is full-frame by design | Full-frame kept (decision 54) |
| 58 | '7 Houseguests' at the Final 3 | Mockup error | Ignored |
| 59, 60 | Objective chip line 'Last competition. Last decision.' | The 'Next stop:' line is pinned | Next stop kept; the tagline under the objectives heading (M3) |
| 59 | 'Take Alex' / 'Take Jordan' buttons | The caption is `Evict {name}` | Kept, under a large 'Take {First}' headline (decision 47) |
| 59 | 'Leaning' in the jury read | ENDGAME decision 5 | Known support / Bitterness / Uncertain (decision 50) |
| 59 | 'Trust level' bar, 'loyal to you' | Implies the NPC's hidden view | A 'Your standing' bar and record lines (M8) |
| 59 | Predictive 'IF YOU TAKE' bullets | Evidence, never prediction | Evidence lines kept (M8) |
| 59 | 'Both finalists will go to the jury and the game will be over.' | Untrue | '{cut} joins the jury; you and {take} face the jury's questions.' (M8) |
| 59 | The finalists' quotes | Invented | Each finalist's recorded intro line (M8) |
| 60 | 'Explain why you should win $500,000.' | No prize in state | 'At the Final 2' kept (decision 11) |
| 51, 61 | The goodbye's warmth or glare | Reads the new juror's hidden score during play | Keyed to what the player knows (decision 6) |
| 52 | Opening statements | No record; a record needs schema 22 | The intro line as an opener; the pleas are the statements (decision 62) |
| 52 | The winner covering her mouth | No take | Won, until M36's Shocked |
| 54 | 'LAST SPOT STANDING', 'THE FINAL SHOWDOWN' | A conflict only if shown as the game's title | Fixed stakes lines under the real title (M26) |
| 61 | EVICT/KEEP chips, and a nominee listed as a voter | Spoils the count; nominees never vote | 'EVICT {name}' per voter (decision 5) |
| 61 | The evictee's duffel bag | No prop; the plan dropped it | Dropped (decision 7) |
| 63 | 'A short speech sets the tone' | No speech is recorded | A talk loop with no subtitles (decision 66) |
| 63 | The two without keys seated together before the block | Seating by outcome spoils the block | Cast-order seats; a two-shot after the last key (M24) |
| 62 | Per-station progress on the yard scoreboard | Invented | The clock, category and title only (M35) |
| 62 | Nominee pleas to the holder | No recorded pleas | Body language; the player's own recorded lobby by its ask (decision 69) |
| 62 | The HoH holding a draw box | Untrue when nobody was drawn | The box only when a draw happens (decision 72) |

---

## 4. Owner decisions

Each question lists its options and a recommendation, and names what it gates. Numbers in brackets refer to the existing plans.

**Answered by the owner on 2026-09-29:** 1 A (the screen on the south wall), 3 B (the Head of
Household seated in the U), 4 A (the exit door on the living room's west wall), and 10 as
recommended (the existing brand lines and '— GAMESIM' only; a new line only when the owner approves
it). Every other decision takes its recommendation unless the owner rules otherwise.

### A. Answer these first: they gate the eviction-room couches

1. **Where does the living room's screen go?** (CEREMONY 8.4.3) (A) the south wall's west half, with the feature wall moving with it; (B) the north wall, shifted east. **Recommended: A.** Every seat, lane and wingback position in 8.2.3, and in this plan's M21, is worked out for A. The storyboard's slat panels and warm strips are rebuilt around the new screen in M21. *Gates M21.*
2. **Which room gets its couches first?** (A) the living room's U now (M20 to M22), with the nomination circle later on a second rebake (M32); (B) both rooms in one batch with one rebake, as CEREMONY §8.3 planned. **Recommended: A.** It is what you asked for, and three of its pieces are already exported. *Gates M21.*
3. **Does the Head of Household sit with the house at evictions?** (CEREMONY 8.4.4) (A) standing beside the screen, as built; (B) seated in the U. **Recommended: B.** You said the houseguests "all sit there together". At 16 the HoH fills the fourteenth couch seat. The plan had recommended A before your message. *Gates M22.*
4. **The exit door.** (CEREMONY 8.4.2) (A) the opening's door rebuilt on the living room's west wall, with the yard as a fallback; (B) the yard's front door only; (C) both. **Recommended: A,** as the storyboard draws it. *Gates M23, M29.*
5. **The vote board.** (CEREMONY 8.4.6) (A) a roster of voters, each row naming the nominee that voter evicts; (B) the two-face tally as built. Never EVICT/KEEP against the eventual evictee. **Recommended: A.** *Gates M18.*
6. **The evictee's goodbye tone.** Today the warm look or the glare comes from their hidden feeling toward you, and they are about to join the jury. (A) key it to what you know: a deal, your vote or nomination against them; (B) let the exit show their true feeling. **Recommended: A.** *Gates M19, M29.*
7. **The bag.** (CEREMONY 8.4.10) (A) dropped; (B) a prop later, with a carry take. **Recommended: A.**
8. **When you are the one evicted,** should your own goodbye and walk-out play? (A) yes, reusing the Goodbye step and the door; (B) straight on to the jury, as today. **Recommended: A, after M23.** Until then, as today.

### B. Numbers and brand copy

9. **Days.** (ENDGAME 1) (A) weeks on every strip and tile; (B) a derived day (seven a week), never saved. **Recommended: A.**
10. **Brand copy.**
    - May the existing brand lines ('Same house. Different stories.', 'Good people / Bigger stories') appear as decoration on the end screens?
    - May brand quotes carry '— GAMESIM' (never a houseguest's name)?
    - May new lines appear ('Two journeys. One winner.', 'A legacy forever', 'In the end, it's more than a game…')?
    - For Season Complete's second line, a line built from the season's counts, or the fixed 'Alliances. Betrayals. A Winner. A Legacy.'?
    
    **Recommended:** existing lines yes, '— GAMESIM' yes. New lines only as unattributed decoration you approve one by one. The Season Complete line built from counts.
11. **A grand prize amount?** (A) none; (B) one fixed amount as a content constant, never saved, used everywhere. **Recommended: A** unless you want one.
12. **'Major twists'.** Drop the row, or count house events, storylines or production removals? **Recommended: drop.**

### C. Season Complete (48)

13. **The rail when the season ends.** (A) the normal rail, dimmed beside the sheet; (B) new rail entries (Season results, Season legacy, The jury), with new captions and tests. **Recommended: A.**
14. **The third header tile.** (A) competitions held; (B) jurors; (C) none. **Recommended: A.**
15. **The analysts' quote slot.** (A) a line from the winner's record, 'From the season's record'; (B) empty. **Recommended: A.**
16. **'Legacy Score' and 'Emotional Control'.** (A) use Game Sense's overall and Competitions scores; (B) define new formulas (presentation-only if they read saved state; schema 22 if they track anything new). **Recommended: A.**
17. **Post-season ranks.** May the alliance and betrayal counts include NPC-only alliances and NPC-to-NPC broken promises the player never saw? (A) no, only what involved or was known to the player; (B) yes, after the season. **Recommended: A.**
18. **The relationship cards.**
    - (a) Ally and rival from your own score, already shown as 'Your trust', or from the mutual score?
    - (b) 'Betrayed': who betrayed you, or whom you betrayed?
    - (c) May a juror's view of you when they left ('Lasting respect') be revealed?
    
    **Recommended:** (a) your own score; (b) show both, worded by direction; (c) leave that card out until you rule.
19. **When you won,** your four counts appear in both the hero and Player Statistics. (A) the hero shows them, and Player Statistics shows only ranks, alliances and betrayals; (B) both show them, as the mockup does. **Recommended: A.** It keeps the report's no-repeat rule.
20. **Timeline node titles.** (A) storyline titles plus a fixed set by rule ('Alliance formed', 'Deal broken', 'Blindside', 'Final 3'), else 'HoH: {name}'; (B) the week's HoH only. **Recommended: A.**
21. **'Continue to Legacy'.** (A) a decorated `Main menu`, whose screen carries your career line; (B) a new control with its own career view. **Recommended: A.**
22. **The legacy panel.** (A) this season's champion line only; (B) also past champions from your career record. **Recommended: A** now.
23. **The night exterior.** (A) the brand line over a vignette; (B) a new exterior set piece and night render (a separate XL art job). **Recommended: A.**

### D. The winner and the finale (49, 53)

24. **Watch season recap.** (ENDGAME 10) (A) a control of its own, `Watch season recap`, stepping through the week recaps; (B) the recaps behind `Review the season`; (C) none. **Recommended: A.**
25. **Share card and episode archive.** (ENDGAME 8) (A) out of scope; (B) a share card exported as a PNG. **Recommended: A.**
26. **The winner screen's hero.** (A) 49's live camera on the winner's body, with a lower-third lockup; (B) 53's portrait cards; (C) cards now, and the camera shot added when a body is ready. **Recommended: C.**
27. **Third place on the winner screen.** (A) shown as 3rd place and juror, with the vote they cast; (B) left off, as 49 does. **Recommended: A.**
28. **Third place's quote.** (A) none; (B) their logged eviction plea with its week, when the log still holds it. **Recommended: A.**
29. **53's 'Continue to Season Recap'.** Does it mean Season Complete (`Season report`) or the week recaps (decision 24)? **Recommended:** `Season report`.
30. **The winner's seat and the crowd in 3D.** (A) the red wingback, with the jury as the crowd; (B) a new dark throne, and extra UMA bodies as an audience. **Recommended: A.**

### E. The jury screens (50, 55, 56, 57)

31. **The juror screen's navigation.** (A) the house rail kept, with in-panel tabs; (B) a finale rail set, with new caption contracts. **Recommended: A.**
32. **The tile in place of 'Days Survived'.** (A) eviction votes survived; (B) times the veto saved them; (C) none. **Recommended: A.**
33. **The crown on a finalist card.** (A) only on the final Head of Household, captioned; (B) dropped. **Recommended: A.**
34. **The season summary.** (A) templated factual sentences from the record; (B) none. **Recommended: A.**
35. **Rivalries.** (A) defined from public ceremonies (who put whom on the block); (B) none. **Recommended: A.**
36. **'What matters to you' bars.** (A) the diary personas' own names; (B) mapped to the mockup's value words. 'Entertainment' is dropped either way. **Recommended: A.**
37. **'Recent jury discussion'.** (A) your own questions and the finalists' recorded answers; (B) leave the region out. **Recommended: A.**
38. **Questioning as a lower third over the room.** (A) now, while the jurors stand in rings; (B) with the seated set (M30). **Recommended: B.**
39. **History questions.** (A) the saved wording kept, with a bold receipt kicker above it; (B) questions that name the week and act (a validator change; schema 22). **Recommended: A.**
40. **Where is the jury house?** (A) an off-site lounge set (XL); (B) the living room on finale night, with the window at three staying 2D; (C) a 2D tableau only. **Recommended: C now, B with M30.**
41. **Jury house callouts.** (A) the juror's recorded words: their question and its recorded note, or their plea, dated; (B) the band's reason only. **Recommended: A,** never worded as how they feel about you.
42. **A jury-house door on the objectives card's jury strip** (it needs a panel flag), or keep the tile at three and the row at the Final 2? **Recommended: the strip door.**
43. **Final case narratives.** (A) keep the five built ones and borrow the look; (B) re-theme to Adaptability and Underdog Survival (the trait table, saved theme keys, the jury term and every pin change). **Recommended: A.**
44. **The résumé's quote slot.** (A) the chosen claim unquoted, then the locked speech's opening after the lock; (B) your creator bio. **Recommended: A.**
45. **A broken promise as a signature moment?** (A) no; (B) yes (it changes the committed validator and scores in the jury term). **Recommended: A.**
46. **The Final 2 objectives card.** (A) under finale rules, 'Prepare your final case' leads and replaces 'Answer the jury's questions', as the mockup shows; (B) a fourth row. **Recommended: A.**

### F. The Final 3 and the Final HoH (58, 59, 60)

47. **'Take Alex' wording.** (ENDGAME 2) (A) the pinned `Evict {name}` under a 'Take {name}' headline; (B) rename the caption and every pin. **Recommended: A.**
48. **The final part games.** (ENDGAME 3) (A) dress the three authored games; (B) rules version 5 with a buzzer or press-your-luck final part. **Recommended: A.**
49. **The rail at the endgame.** (A) doors on the endgame cards and tiles; (B) swap the rail's pages. **Recommended: A.**
50. **The jury read's words.** (A) Known support / Bitterness / Uncertain; (B) other words. Never 'Leaning'. **Recommended: A.**
51. **The pill at the final decision.** (A) the jury's size; (B) 'You · HoH'. **Recommended: A.**
52. **Stage the two competitors in the yard** when you sit a final part out, with a short watch before the result? **Recommended: yes.**
53. **May a staged view animate committed scores as a race?** **Recommended: no.** Scores appear on the result card.
54. **Watching a part.** (A) the full-frame sheet and arena; (B) the house HUD kept around a centred yard. **Recommended: A.**
55. **The yard set.** (CEREMONY 8.4.8) (A) later, as S7; (B) this round. And: new buzzer pedestals or the existing podiums, and should the gantry read FINAL HOH at the endgame? **Recommended: A,** then the podiums first, and yes.

### G. Ceremonies and staging

56. **The Final 3 memory-wall shot.** (A) with the card at Part 1's start, as built; (B) when the window at three opens (a re-pin). **Recommended: A.**
57. **Trust talks.** (A) a wordless gather of the three on the couch; (B) a story-session scene with recorded lines. **Recommended: A.**
58. **Part 3 announced** in the living room, or in the yard at its screen? **Recommended: the living room.** The yard screen is S7.
59. **The reflection.** (A) the HoH suite with the two candidates' portraits, as the storyboard draws; (B) the memory wall (ENDGAME F7.5). And a cut or a walk? **Recommended: A, with a cut.**
60. **The Final 2 decision gathering.** (A) only as the ceremony after the commit; (B) the three gather before the choice. **Recommended: A.**
61. **A player juror's body during the finale.** (A) seated on the U with the jury; (B) at the station. **Recommended: A.**
62. **Opening statements.** (ENDGAME 7) (A) none: the pleas are the statements, with the intro line as an opener; (B) a second speech record (schema 22). **Recommended: A.**
63. **The ballot box.** Confirm it on the screen stage's end (ENDGAME F6). Does every juror walk up in a large jury? **Recommended:** yes to both, with a press skipping to the reveal.
64. **The nomination circle.** (A) curved arc sofas, with teal armchairs between them, as drawn; (B) straight three-seaters set as a polygon. **Recommended: A.**
65. **The keybox.** (CEREMONY 8.4.9) (A) carried on the HoH's forearms; (B) on a stand. **Recommended: A.**
66. **The HoH's opening.** (A) a silent talk loop; (B) one fixed format line on the screen. **Recommended: A.**
67. **The key walk-ups.** (CEREMONY 8.4.1, 8.4.5) May the card wait up to 4 s at a key boundary, and should walk-ups happen at every house size (about 91 s at sixteen) or only up to twelve keys? **Recommended: A and A,** as the plan recommends.
68. **The nominees' crossing to the red chairs.** (CEREMONY 8.4.7) (A) a stage tail after the card; (B) the card holds until both are seated. **Recommended: A.**
69. **Veto pleas.** (A) body language only, plus your own recorded ask when it was you; (B) also a factual caption for NPC nominees. **Recommended: A.**
70. **The veto's emblem.** (A) the medallion stays in the UI, and the golden key is the 3D prop; (B) the golden key everywhere (new icon art, six code sites, overriding the packs). **Recommended: A.**
71. **An HoH crown pendant?** If yes, worn in ceremonies only, never saved. **Recommended:** yes, with the veto necklace (M34).
72. **The HoH holding the draw box at the veto reveal,** only when a draw really happens (houses over six)? **Recommended: yes.**

---

## 5. Not planned

- **Invented copy of every kind:** the analysts', jury and collective quotes, juror quotes, NPC chatter, timestamps, evaluative praise, drama titles for weeks, and predictive lines. Section 3 lists what stands in.
- **Numbers with no source:** days (under decision 9A), episodes, major twists, and a prize amount (unless decision 11B).
- **New scores:** 'Emotional Control' and 'Legacy Score' formulas, unless decision 16B.
- **Other jurors' discussion as data.** Nothing records it, and recording it would need schema 22.
- **Opening statements as a saved record** (decision 62A), **and history questions that name the week** (decision 39A). Both would need schema 22.
- **Broken promises as signature moments,** and re-themed narratives (decisions 45 and 43).
- **Rules version 5, the buzzer game and a live NPC progress race** (decisions 48 and 53).
- **Rail swaps** for the finished season, the endgame or the finale (decisions 13, 31 and 49).
- **The share card, the episode archive and recorded scene stills** (decision 25A). Each would be the game's first capture and file write outside port verification.
- **The night house exterior** (decision 23A), **a throne set piece and a live audience** (decision 30A). The audience would need non-cast UMA bodies.
- **The off-site jury house set** (decision 40).
- **The duffel bag** (decision 7A).
- **Laurels,** unless laurel art is added to Pack 7 through `bb_ui_pack7.py` with catalogue entries and full metas.
- **Greyscale juror photos on the winner screen:** optional and low priority. No grayscale UI material exists.
- **A logo image for the lower-left lockup:** no logo art exists. A text lockup is optional in M7.
- **The career record's COMP WINS including final parts.** `CareerSeason` stores only HoH and veto wins, so this would be a career-file schema change (1 to 2). The report's own cells move in M1.
- **A separate overlay canvas for the winner screen.** It was rejected because it would cover the notebook and break FinaleExits and Keyboard (M7 draws inside the Finished panel instead).
- **The Diary Room's jury number** (ENDGAME decision 12). It sits next to the jury house but is not in these mockups, and stays with ENDGAME F4.
- **Anything numeric from the mockups themselves.** Their counts are inconsistent: for example, 8 houseguests beside 7 jurors plus a 2nd and 3rd place.

## 6. Review corrections

A completeness critic read this plan against the verified gap map and the code. Its corrections
stand beside the slices they name and win over the slice text where they disagree.

- **M8: the headline change breaks a pinned test (captions and pins).** `EpisodePlayModeTests.FinalThree.cs:84` asserts `headline.text == "Take " + take.name + " to the Final 2"`. So "Style the headline as a large 'Take {First}'" would fail if the text changes. Keep the pinned text and make it large with style only, or draw 'Take {First}' as a separate label and leave the pinned headline in place (smaller or muted).
- **M5: the rail offset uses the wrong units.** The HUD's reference size is 1600×900 (`EpisodeHud.cs:229`). SeasonReport's is 1920×1080 at resting text (`SeasonReport.cs:177, 200`). The rail's ground spans 8 to 220 HUD units, and `LeftColumnX` = 226 (`Chrome.cs:454-455`, `EpisodeHud.cs:240`). `IconRail.Width + 24` gives 224 report units, which is about 187 HUD units, so the sheet would sit over the rail at resting text. Use `EpisodeHud.LeftColumnX × (report reference width / 1600)`, or measure the rail ground's screen rect, and check at both text sizes.
- **M21 and M22 must land in one PR (ordering).** `CeremonySets.DressTheLivingRoom` clones the nomination screen to the north side (z ≈ -1.25) whenever no object is named `LivingScreenName` (`CeremonySets.cs:267-282`). It then places the hot seats 2 m in front of that screen, at about (-5.55, -3.25) and (-4.45, -3.25), which is inside the east lounge4 of M21's U base at z -2.9. After M21 alone you get a second screen and hot seats inside a couch. Either merge the two slices, or have M21 name its authored screen `LivingScreenName` and strike the hot-seat clone path at the same time.
- **M26, M35 and M27 keep the arena up, and that blocks stages (ordering).** `competitionArenaStaging` stays true for the arena's whole life. While it is true, `TryBeginCeremonyStage` refuses (`CeremonyStage.cs:95`), `TryBeginWalkOut` refuses (`WalkOut.cs:65`), and the NPC world ticks only the arena (`NpcSocial.cs:161`). If M26 keeps the Part 2 arena through the standings, M28's Part 3 announcement in the living room, which plays at that same commit, can never stage. Say in M26 or M28 that the arena is struck, or the stage deferred, before the Part 3 gather. M27 should also strike the spectator arena when the briefing closes (Esc or Close) without a watch.
- **M29 depends on M30 for one step.** "The body stays off the bench until the finale walk-in" needs M30's walk-in. The final eviction lands on finale night, `BenchTheJury` runs at once, and `Jury_FinaleNightHasTheJuryInTheLivingRoom` expects every juror in the living room. Until M30 lands, M29 must put the juror back on the bench (`StandTheJury`) after `FinishWalkOut`.
- **M26 and M28 give the Final Three card two cameras.** M26 holds the memory-wall shot while the card plays; M28 plays the same card on the living screen with a `Screen.Shot`. Pick an order, for example the wall for the first 2 s and then the screen.
- **Dependency summary is incomplete.** Add M3 before M5, M7 and M10 (the cached season number). Add M1 before M7 (the quote helper). Add M22 before M28, or state that M22 must move any sofa-seat 0-2 users (M28's gathers) to the gallery venue, because M22 removes the sofa turn and its anchors.
- **M20: the living-room table needs its own script (3D rule).** "A new piece or a variant of `bb_set_roundtable`" would clash with M32, which edits `bb_set_roundtable.py` for the nomination room (0.45 m height, brass rim). One script per export means a new script, for example `bb_set_lowtable.py`.
- **M20: pins and test copies.** Add `AuthoredAssetImportTests` to M20's pins; they iterate every model under `Art/Authored`, including the floor and bb-name check. Until M20 lands, D: test copies for M1-M19 should leave out the three untracked FBX, or Unity imports them without owner consent and those tests run on them.
- **M34: the veto box and stand need collision (3D rule).** `bb_set_vetobox` and `bb_set_stand` on the screen stage's end have no collision plan. A stand above 0.40 m needs a layer-8 proxy and a rebake (fold it into M21's or M32's bake), or it must stay renderer-only on a spot no route crosses, checked by capture.
- **M19: the house cheers at a goodbye before the couches exist.** Standing `React(Cheered)` plays `Cheer_loop` (`HumanoidClipWiring.cs:161`); only seated bodies get the clap (`CharacterPresentation` seated branch). With 11 of 16 still standing, the house would cheer the evictee out. Play `Cheered` only on seated bodies and use `LookAt` for everyone standing.
- **M31: a new card in batch runs changes the audited walk (cut-scene rule).** "Batch runs and reduced motion get a card of '{name} has voted.' lines" adds a card before the reveal. Either put the lines inside `JuryReveal`'s own opening and re-measure CeremonyTruthTests' jury pacing, or skip the card in batch runs.
- **M7: decision 24A has no slice.** `Watch season recap` is recommended, but nothing builds it. Add it to M7 or a new slice (M, feature): its own caption, week 1 opened past the season-ending gate, a next-week control on the recap screen, and a ledger power line for weeks the event log has lost. Add it to the finale focus ring in `Keyboard.cs`.
- **M3 or M14: decision 42 has no slice, and its caption must not repeat.** No slice builds the jury-house door on the objectives card's jury strip. `FindButton` uses `.Single` over the director's buttons (`EpisodePlayModeTests.cs:446-448`), so a strip door captioned `The jury house` next to the free tile at three would break the jury-house tests. Give it a unique caption or keep one door live at a time. It also needs the panel flag F4 names.
- **M3 (or M15): decision 46 has no slice.** Nothing builds the Final 2 objectives swap. Under finale rules, 'Prepare your final case' leads the card (status Open or Locked, done when `finalArgument != null`). Add a `finaleRules:true` assert, because the existing pin at `Endgame.cs:117-118` runs with the rules off.
- **M23 or a new slice: decision 8A has no slice.** The player's own goodbye and exit when they are evicted is recommended "after M23", but no slice builds it (L: an activity move through the Goodbye step and the door, then a hand-back to the juror flow).
- **M13 to M27: the competition live feed has nowhere to show before M27.** The competition layout stands the chrome down, feed included (`Activities.cs:265`), and the game screen hides the HUD. Move the item to M27, which stages the arena while the spectator watches, and name where the card goes (inside the briefing sheet).
- **M30: 55's frame.** Add standing down the week chip and the icon rail while the JuryStage lower third is up, the way `HideChromeForFullFrame` does, as the gap map asks.
- **M11: the conflicts table and the slice disagree.** The table gives "(M11)" for the unquoted recap headline under the receipt, but M11's list does not include it. Add it (only when it adds to the receipt) or drop the reference.
- **M7 (optional): 53's "The final speech".** Put `Review final speeches` in the winner screen's second row, reading `finalSpeeches`; its caption is not live elsewhere at Finished. A crowning-card replay for 'Final HOH' is also possible.
- **M1 and M16: two COMP WINS counts on one report.** After M1, the season cells count the final parts and the career cell does not, so a one-season career shows two different COMP WINS numbers. Add a muted sub-label under the career cell ("HoH and veto") without changing the pinned caption, whose count of 3 is at `SeasonReport` tests :238.
- **M3 and M5 both set the HUD strap at Finished.** M3 writes 'SEASON FINALE' while `FinaleNight` (which includes Finished) and M5 reuses the season number there. State which wins, for example 'SEASON {n} FINALE'.
- **Where things stand: the blank finale capture is runner-wide.** Every capture under `D:\GamesimVerify\*` is 18,959 bytes, not only `finale.png`; the runs never wrote a real frame, apart from two settings captures per folder. Reword the note so nobody debugs the finale; M7's PlayMode capture is the right evidence.
- **Mockup numbering.** Add 50 = ENDGAME 30 and 55 = ENDGAME 35 (`ENDGAME-PLAN.md:136-151`).
- **Section 5 or M17: Pack 7's motion is neither planned nor declined.** The README asks for a gold glow on the winner, staggered stat cards, jury rows one at a time and timeline nodes left to right. List it under Not planned, or add it to M17 with the reduced-motion rule. Also say explicitly that 60's 'THE HOUSE | FINAL 3 SEASON' strap is declined; the gap map recommends leaving it.
- **Spot-checks that hold:**
  - M1: the veto field uses the four-argument `Play` (`EpisodeDirector.cs:787-788`).
  - M1: raw phase text reaches only the status line; Recent Events and the Story page filter out `phase` events (`Chrome.cs:1012-1015`).
  - M2: disabling the cloned idle renderer survives cards, because Mount and Unmount re-enable only what they hid (`ScreenSurface.cs:185-204`).
  - M3: the pill shows the jury cell only while nobody holds the house (`Chrome.cs:379-383`).
  - M7: `ledger.competitions.Count` equals the sum of `FinalistRead.Wins` (one row and one win per resolved competition: `EpisodeEngine.cs:577-599`, `EpisodePreparation.cs:59-67`).
  - M7: opening the panel automatically is safe for the audited walk, because `OpenSeasonStation` returns early when a panel is open (`PortVerification.Season.cs:442-444`).
  - M25: the dormant veto branch seats the HoH on head 0 and the holder on head 1, and handles a holder who is also the HoH (`CeremonyStage.cs:302-320`).
  - M6: 'HoH wins' does not collide with the pinned 'HOH WINS', which is compared exactly (`SeasonReport` tests :100, :124).
