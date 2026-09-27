# GameSim UI Asset Packs 1–4 — Inventory & Unity Implementation Guide
This inventory is designed for the current **Unity uGUI + URP 17.6** project. It assumes the existing `UiTheme` semantic palette remains authoritative and the current caption/RectTransform-driven test suite should be preserved.
**Total files:** 313 across four packs  
**Reusable/source visual assets (excluding README + preview sheets):** 309
## Global rules for the mockup-quality look

1. **Quiet chrome, strong state.** Resting panels use muted borders. Cyan means *current focus/action*, gold means *power/achievement*, red means *danger/nomination*, green means *ally/positive relationship*, pink means *romance*, purple means *strategy/secrets*.
2. **Do not flatten dynamic UI into images.** Keep names, stats, portraits, timers, votes, traits, and copy as TMP/runtime content layered over reusable sprites.
3. **Keep existing uGUI controls.** Where tests depend on captions and rects, skin the existing `Image`/`Button`/`Slider` objects or add non-interactive child decoration instead of rebuilding hierarchy.
4. **Decorative images: `Raycast Target = false`.** This prevents glows, frames, dividers, overlays, and transitions from intercepting input.
5. **Use 9-slicing.** Any file ending in `_9slice.png` should normally import as `Sprite (2D and UI)` and use `Image Type = Sliced`.
6. **Limit simultaneous glow.** In a normal screen, usually only the selected/current-action element should glow. Power moments can add one gold focal glow.
7. **Animate hierarchy, not color meaning.** Use scale, alpha, position, and blur/glow intensity for hover/selection; do not multiply RGB in ways that change semantic hue.
8. **Prefer real portraits.** Use the original/approved character portraits with masks/rings; do not stylize them again unless explicitly intended.
9. **Screen-space composition matters more than effects.** The mockups work because the 3D world or portrait content gets most of the visual area; UI frames should support it rather than cover it.
10. **At 1080p, design for game-view readability.** Use TMP body text around 14–16 px equivalent minimum, controls around 18–20 px, section titles 24–32 px, major titles 36 px+.
### Recommended Unity import baseline

- **Panels / Buttons / Chips / Cards:** Texture Type `Sprite (2D and UI)`, Mesh Type `Full Rect`, Wrap `Clamp`, Filter `Bilinear`, Mip Maps `Off`, Alpha Is Transparency `On`, Image Type `Sliced`.
- **Rings / Badges / Icons / Pins:** Sprite `Simple`, Preserve Aspect `On`, Mip Maps `Off`.
- **Backgrounds / Full-screen transitions:** `Simple`, stretch/cover the canvas, `Raycast Target = false`.
- **Canvas Scaler:** retain your existing project value; if still undecided, a 1920×1080 reference with `Scale With Screen Size` is the usual target for these mockups.
- **Slicing starting point:** large shells 36–42 px; medium cards 28–32 px; buttons 28–32 px; tags/chips 20–26 px. Adjust only enough to preserve rounded corners.
## Pack summary
| Pack | Files | Main role |
|---|---:|---|
| **1** | 51 | Foundational visual grammar: panels, buttons, chips, rings, status badges, glow, basic icons, background. |
| **2** | 98 | Core gameplay UI: HUD, cast states, conversation, live feed, meters, relationship web, notebook, ceremonies, minimap, creator. |
| **3** | 87 | Deep systems and progression: main menu, saves, dossier, intelligence, competition HUD, recap, finale/jury. |
| **4** | 77 | Presentation layer: Diary Room, cinematic ceremonies, schedule, memory wall, Final Three, social alerts, transitions, broadcast overlays. |

# Pack 1
## Backgrounds — 1 files
**Purpose:** Base screen backdrop  
**Unity application:** Import as Sprite/Default; stretch or Aspect Fill behind all menu content; Raycast Target OFF.  
**Mockup-quality rule:** Keep the background dark and quiet. Do not add extra cyan frames around it; let portraits and current selection carry emphasis.
- `background_dark_navy_1920x1080.png`
## Badges — 4 files
**Purpose:** HOH, Veto, nominee, selected status  
**Unity application:** Simple Sprite, Preserve Aspect ON. Anchor to portrait/card corners as child decorative Images.  
**Mockup-quality rule:** Status badges should be small but high-contrast. Let the badge communicate state so the whole card can remain neutral.
- `badge_hoh.png`
- `badge_nominee.png`
- `badge_selected.png`
- `badge_veto.png`
## Buttons — 5 files
**Purpose:** Primary, secondary, success, danger and power actions  
**Unity application:** Sprite Type = Sliced. Put TMP text above the Image. Keep button Image as Target Graphic so existing uGUI behavior/tests stay intact.  
**Mockup-quality rule:** Use one strong primary action per screen. Secondary controls should be visually quiet. Avoid glowing every button.
- `button_danger_9slice.png`
- `button_gold_9slice.png`
- `button_primary_9slice.png`
- `button_secondary_9slice.png`
- `button_success_9slice.png`
## Chips — 10 files
**Purpose:** Filters, archetypes, state tags  
**Unity application:** Sprite Type = Sliced. Size around content using LayoutElement/ContentSizeFitter where already safe.  
**Mockup-quality rule:** Use semantic colors for content meaning: purple=strategy, green=social/ally, pink=romance, red=danger, gold=power. Neutral remains muted.
- `chip_blueviolet_9slice.png`
- `chip_cyan_9slice.png`
- `chip_danger_9slice.png`
- `chip_gold_9slice.png`
- `chip_neutral_9slice.png`
- `chip_orange_9slice.png`
- `chip_romance_9slice.png`
- `chip_selected_9slice.png`
- `chip_social_9slice.png`
- `chip_strategic_9slice.png`
## Dividers — 1 files
**Purpose:** Soft visual separation  
**Unity application:** Simple Sprite; horizontal stretch. Raycast Target OFF.  
**Mockup-quality rule:** Prefer dividers and spacing over extra bordered boxes.
- `divider_soft_blue.png`
## Glows — 4 files
**Purpose:** Reusable emphasis halos  
**Unity application:** Simple Sprite, Raycast Target OFF. Place behind the selected item with low alpha; consider CanvasGroup for animation.  
**Mockup-quality rule:** Use only on selected/current-action/power moments. Too many simultaneous glows destroys hierarchy.
- `glow_cyan.png`
- `glow_gold.png`
- `glow_purple.png`
- `glow_red.png`
## Icons_PNG — 8 files
**Purpose:** Basic runtime icons  
**Unity application:** Simple Sprite, Preserve Aspect ON, Raycast Target OFF unless the icon itself is the button graphic.  
**Mockup-quality rule:** Keep icon size and stroke weight consistent across menus.
- `crown.png`
- `grid.png`
- `home.png`
- `random.png`
- `save.png`
- `star.png`
- `trophy.png`
- `user.png`
## Icons_SVG — 8 files
**Purpose:** Vector source icons  
**Unity application:** Use as design/source assets or convert through Unity Vector Graphics if already in project.  
**Mockup-quality rule:** Use the vector source only if your pipeline already supports it; otherwise PNGs are safer for the existing uGUI project.
- `crown.svg`
- `grid.svg`
- `home.svg`
- `random.svg`
- `save.svg`
- `star.svg`
- `trophy.svg`
- `user.svg`
## Panels — 5 files
**Purpose:** Core containers / modal surfaces  
**Unity application:** Sprite Type = Sliced. Use resting as default, interactive only for hover/focus-capable regions, selected only for the current selection.  
**Mockup-quality rule:** The premium feel comes from low-emphasis resting chrome. Reserve selected/gold/danger variants for semantic state—not decoration.
- `panel_danger_9slice.png`
- `panel_gold_power_9slice.png`
- `panel_interactive_9slice.png`
- `panel_resting_9slice.png`
- `panel_selected_9slice.png`
## Rings — 3 files
**Purpose:** Portrait framing and selection/power states  
**Unity application:** Import as Simple Sprite; Preserve Aspect ON; overlay outside portrait mask. Raycast Target OFF.  
**Mockup-quality rule:** Gold ring is portrait/power framing, cyan ring is current selection. Do not use cyan on every portrait.
- `portrait_ring_gold.png`
- `power_ring_gold_glow.png`
- `selection_ring_cyan.png`

# Pack 2
## Ceremony — 5 files
**Purpose:** Nomination/Veto/Eviction decision frames  
**Unity application:** Sliced Sprites on dedicated ceremony Canvas. Portraits and copy remain dynamic.  
**Mockup-quality rule:** Ceremonies should temporarily reduce ordinary HUD chrome; let one large ceremonial decision frame dominate.
- `ceremony_banner_9slice.png`
- `eviction_candidate_frame_9slice.png`
- `nominee_slot_9slice.png`
- `replacement_nominee_frame_9slice.png`
- `veto_decision_panel_9slice.png`
## CharacterCreator — 9 files
**Purpose:** Slider, steps, thumbnails, categories, swatches  
**Unity application:** Sliced track/fills, Simple handle/swatches, dynamic thumbnails. Keep existing uGUI slider/button components.  
**Mockup-quality rule:** Let the 3D avatar be the hero. Controls should be grouped and quiet; selected thumbnails get cyan, not every option.
- `creator_category_tile_9slice.png`
- `creator_slider_fill_9slice.png`
- `creator_slider_handle.png`
- `creator_slider_track_9slice.png`
- `creator_step_active_9slice.png`
- `creator_step_inactive_9slice.png`
- `creator_swatches_panel_9slice.png`
- `creator_thumbnail_frame_9slice.png`
- `creator_thumbnail_selected_9slice.png`
## CharacterStates — 10 files
**Purpose:** Houseguest card states and corner overlays  
**Unity application:** Card backgrounds = Sliced; overlays = Simple. Keep names/portraits/TMP dynamic.  
**Mockup-quality rule:** Use neutral card at rest. Apply one state overlay/border at a time based on priority: current selection > power > nominee > relationship/event marker.
- `houseguest_card_hoh_9slice.png`
- `houseguest_card_hover_9slice.png`
- `houseguest_card_nominee_9slice.png`
- `houseguest_card_resting_9slice.png`
- `houseguest_card_selected_9slice.png`
- `overlay_event_corner.png`
- `overlay_hoh_corner.png`
- `overlay_nominee_corner.png`
- `overlay_romance_corner.png`
- `overlay_selected_corner.png`
## Competition — 8 files
**Purpose:** Base quiz/score/timer/progress assets  
**Unity application:** Sliced Sprites; timer/score values via TMP. Use selected answer tile for current input only.  
**Mockup-quality rule:** Competition HUD should feel broadcast-like but sparse: timer, objective, score, active contestant state.
- `answer_tile_9slice.png`
- `answer_tile_selected_9slice.png`
- `competition_podium_label_9slice.png`
- `competition_score_tile_9slice.png`
- `competition_timer_frame_9slice.png`
- `elimination_badge_9slice.png`
- `leader_marker_9slice.png`
- `progress_lane_9slice.png`
## ConversationWheel — 8 files
**Purpose:** Radial action menu  
**Unity application:** Petals = Simple/Sliced depending implementation; rotate petal GameObjects around center hub. Use a parent CanvasGroup for appear/disappear.  
**Mockup-quality rule:** Action color should remain meaningful on hover. Animate scale/alpha, not RGB multipliers that distort semantic color.
- `wheel_base_ring.png`
- `wheel_center_hub.png`
- `wheel_petal_danger.png`
- `wheel_petal_disabled.png`
- `wheel_petal_positive.png`
- `wheel_petal_resting.png`
- `wheel_petal_selected.png`
- `wheel_petal_strategy.png`
## Dialog — 4 files
**Purpose:** Speech, whisper, nameplate, unavailable-state UI  
**Unity application:** Sliced Sprites; TMP child text; optional tail as separate child.  
**Mockup-quality rule:** Use small contextual dialogue cards instead of large full-screen modals for ordinary social feedback.
- `npc_nameplate_9slice.png`
- `secret_whisper_bubble_9slice.png`
- `speech_bubble_9slice.png`
- `unavailable_action_callout_9slice.png`
## HUD — 5 files
**Purpose:** Persistent gameplay HUD shell  
**Unity application:** Sliced Sprites. Add as backgrounds to existing HUD containers rather than replacing tested controls.  
**Mockup-quality rule:** Keep world view dominant: top bar thin, left nav narrow, right feed readable, cast rail along bottom. Resting rails should not glow.
- `bottom_cast_strip_9slice.png`
- `left_nav_rail_9slice.png`
- `objective_card_active_9slice.png`
- `right_info_rail_9slice.png`
- `top_status_bar_9slice.png`
## Icons_PNG — 13 files
**Purpose:** Gameplay action icons  
**Unity application:** Simple Sprite; Preserve Aspect ON.  
**Mockup-quality rule:** Pair icons with text during onboarding. Once familiar, icons can carry more of the interaction load.
- `alliance.png`
- `calendar.png`
- `chat.png`
- `close.png`
- `diary.png`
- `filter.png`
- `flirt.png`
- `lock.png`
- `search.png`
- `sort.png`
- `strategy.png`
- `target.png`
- `vote.png`
## LiveFeed — 5 files
**Purpose:** Live badge, events, timestamps, toast  
**Unity application:** Sliced Sprites. Populate rows with portrait/icon + TMP + time. Keep Raycast Target OFF on decorative background.  
**Mockup-quality rule:** The right rail should be visually quiet; LIVE/urgent rows can carry saturation. Urgent red is exceptional, not permanent.
- `event_row_9slice.png`
- `event_row_urgent_9slice.png`
- `live_badge_9slice.png`
- `timestamp_chip_9slice.png`
- `toast_notification_9slice.png`
## Meters — 6 files
**Purpose:** Trust, alliance, conflict, influence, stamina  
**Unity application:** Use empty track as background and colored fill as masked/filled Image. Image Type = Filled or scale width from left.  
**Mockup-quality rule:** Avoid showing too many meters at once. Use semantic colors and label clearly; subtle animation on value change adds polish.
- `meter_allied_fill.png`
- `meter_conflict_fill.png`
- `meter_influence_fill.png`
- `meter_stamina_fill.png`
- `meter_track_empty.png`
- `meter_trust_fill.png`
## Minimap — 6 files
**Purpose:** Observation-map frame and pins  
**Unity application:** Frame = Simple/Sliced; pins = Simple. Render map separately; pins are UI overlays.  
**Mockup-quality rule:** Only show in observation mode or when needed. Keep it out of normal social gameplay.
- `minimap_frame_circle.png`
- `pin_event_alert.png`
- `pin_houseguest.png`
- `pin_player.png`
- `pin_room.png`
- `room_label_plate_9slice.png`
## Notebook — 8 files
**Purpose:** Deals/secrets/rumors/promises/vote intelligence  
**Unity application:** Rows/tags = Sliced; TMP for dynamic content. Build cards from reusable prefabs.  
**Mockup-quality rule:** Differentiate information certainty (confirmed/suspected/rumor) from sentiment. Don't overload the same color for both.
- `note_row_9slice.png`
- `tag_confirmed_9slice.png`
- `tag_promise_9slice.png`
- `tag_rumor_9slice.png`
- `tag_secret_9slice.png`
- `tag_suspected_9slice.png`
- `tag_target_9slice.png`
- `tag_vote_9slice.png`
## RelationshipWeb — 9 files
**Purpose:** Relationship graph nodes, lines, selected side panel  
**Unity application:** Nodes = Simple/Sliced; lines = Simple images stretched/rotated between nodes; side panel = Sliced.  
**Mockup-quality rule:** Give the graph most of the screen. Use color to encode relationship type while keeping nodes mostly neutral.
- `line_allied.png`
- `line_distrust.png`
- `line_friend.png`
- `line_rival.png`
- `line_romance.png`
- `node_player.png`
- `node_regular.png`
- `node_selected.png`
- `side_info_panel_9slice.png`

# Pack 3
## CompetitionHUD — 17 files
**Purpose:** Quiz/endurance/Veto-specific HUD  
**Unity application:** Sliced panels + Simple timer ring/badges. CanvasGroup for competition mode activation.  
**Mockup-quality rule:** Hide normal gameplay rails during a competition. Use large timer, big current prompt, compact standings.
- `answer_badge_A.png`
- `answer_badge_B.png`
- `answer_badge_C.png`
- `answer_badge_D.png`
- `eliminated_overlay_9slice.png`
- `endurance_stamina_panel_9slice.png`
- `leaderboard_first_9slice.png`
- `leaderboard_row_9slice.png`
- `power_awarded_panel_9slice.png`
- `quiz_answer_A_9slice.png`
- `quiz_answer_B_9slice.png`
- `quiz_answer_selected_9slice.png`
- `quiz_question_panel_9slice.png`
- `results_winner_banner_9slice.png`
- `timer_ring.png`
- `veto_player_active_9slice.png`
- `veto_player_card_9slice.png`
## Dossier — 10 files
**Purpose:** Houseguest profile/intelligence screen  
**Unity application:** Sliced shells/panels + threat gauge as Simple. Portrait, stats, histories dynamic.  
**Mockup-quality rule:** Use a 2–3 column information hierarchy: portrait/status, relationship/game stats, history/intel. Avoid debug-style grids.
- `dossier_alliance_badge_9slice.png`
- `dossier_comp_badge_9slice.png`
- `dossier_history_row_9slice.png`
- `dossier_nom_badge_9slice.png`
- `dossier_portrait_frame_9slice.png`
- `dossier_rival_badge_9slice.png`
- `dossier_secret_badge_9slice.png`
- `dossier_shell_9slice.png`
- `dossier_stat_panel_9slice.png`
- `threat_gauge.png`
## FinaleJury — 13 files
**Purpose:** Jury questioning, finalists, winner, placements  
**Unity application:** Sliced cards/panels; badges Simple; TMP for questions/votes.  
**Mockup-quality rule:** Gold becomes more prominent here because power/achievement is the subject. Still keep background chrome dark.
- `badge_jury.png`
- `badge_placement.png`
- `badge_season_star.png`
- `badge_vote.png`
- `badge_winner.png`
- `finalist_card_9slice.png`
- `juror_card_9slice.png`
- `juror_card_voted_9slice.png`
- `jury_question_panel_9slice.png`
- `jury_shell_9slice.png`
- `jury_vote_token_9slice.png`
- `placement_row_9slice.png`
- `winner_card_9slice.png`
## Icons_PNG — 10 files
**Purpose:** Menu/intel/finale icons  
**Unity application:** Simple Sprite.  
**Mockup-quality rule:** Keep these in the same icon size grid and stroke weight as Pack 1/2 icons.
- `continue.png`
- `history.png`
- `jury.png`
- `new.png`
- `quit.png`
- `save.png`
- `secret.png`
- `settings.png`
- `threat.png`
- `winner.png`
## MainMenu — 10 files
**Purpose:** Main menu, continue-season card and buttons  
**Unity application:** Sliced Sprites. Overlay on cinematic in-engine background/house render.  
**Mockup-quality rule:** The main menu should feel like entering a season: one strong Continue card, big environment backdrop, minimal ordinary HUD.
- `loading_bar_fill_9slice.png`
- `loading_bar_track_9slice.png`
- `main_menu_continue_card_9slice.png`
- `main_menu_primary_panel_9slice.png`
- `main_menu_secondary_card_9slice.png`
- `menu_primary_button_9slice.png`
- `menu_quit_button_9slice.png`
- `menu_secondary_button_9slice.png`
- `profile_ring_selected.png`
- `season_badge_9slice.png`
## NotebookIntel — 10 files
**Purpose:** Structured intelligence screen  
**Unity application:** Sliced cards/tags. Use scrollable content region if necessary, but keep tabs/header fixed.  
**Mockup-quality rule:** Emphasize actionable intel and certainty. Confirmed facts should read differently from rumors without making every card glow.
- `evidence_receipt_9slice.png`
- `intel_broken_promise_tag_9slice.png`
- `intel_card_9slice.png`
- `intel_confirmed_tag_9slice.png`
- `intel_rumor_tag_9slice.png`
- `intel_shell_9slice.png`
- `intel_suspected_tag_9slice.png`
- `intel_target_tag_9slice.png`
- `intel_vote_tag_9slice.png`
- `timeline_event_9slice.png`
## SaveSlots — 7 files
**Purpose:** Save cards, thumbnail frame, autosave  
**Unity application:** Sliced Sprites with dynamic screenshot thumbnail and TMP metadata.  
**Mockup-quality rule:** Never expose filesystem paths. Show Week/Day/HOH/remaining players/timestamp; autosave appears as a small toast.
- `autosave_toast_9slice.png`
- `save_meta_chip_9slice.png`
- `save_slot_autosave_9slice.png`
- `save_slot_corrupt_9slice.png`
- `save_slot_resting_9slice.png`
- `save_slot_selected_9slice.png`
- `save_thumbnail_frame_9slice.png`
## SeasonRecap — 8 files
**Purpose:** Week transition recap  
**Unity application:** Sliced shell/cards with dynamic portraits and highlight thumbnails.  
**Mockup-quality rule:** Treat recap as television packaging: HOH → nominees → Veto → eviction → relationship shifts. Use animation in that narrative order.
- `highlight_thumbnail_frame_9slice.png`
- `recap_eviction_card_9slice.png`
- `recap_hoh_card_9slice.png`
- `recap_nominee_card_9slice.png`
- `recap_veto_card_9slice.png`
- `relationship_change_negative_9slice.png`
- `relationship_change_positive_9slice.png`
- `week_recap_shell_9slice.png`

# Pack 4
## BroadcastOverlays — 6 files
**Purpose:** Lower thirds, LIVE bug, camera labels, event banners  
**Unity application:** Sliced Sprites on dedicated non-interactive overlay Canvas.  
**Mockup-quality rule:** Use for TV-show flavor during reveals/replays/confessionals. Keep them out of standard navigation screens.
- `breaking_event_banner_9slice.png`
- `camera_label_9slice.png`
- `icon_camera.png`
- `live_corner_badge_9slice.png`
- `lower_third_nameplate_9slice.png`
- `week_day_bug_9slice.png`
## CeremonyCinematics — 13 files
**Purpose:** Lower thirds, reaction frames, vote reveal UI  
**Unity application:** Sliced Sprites on a cinematic overlay Canvas. Animate in/out with CanvasGroup/anchored position.  
**Mockup-quality rule:** Use these temporarily during camera-driven ceremonies; they should not become persistent HUD.
- `ceremony_result_banner_9slice.png`
- `eviction_lower_third_9slice.png`
- `hoh_lower_third_9slice.png`
- `nomination_lower_third_9slice.png`
- `reaction_negative_9slice.png`
- `reaction_neutral_9slice.png`
- `reaction_positive_9slice.png`
- `reaction_shock_9slice.png`
- `replacement_nominee_banner_9slice.png`
- `veto_lower_third_9slice.png`
- `vote_chip_evict_9slice.png`
- `vote_chip_keep_9slice.png`
- `vote_reveal_strip_9slice.png`
## DiaryRoom — 9 files
**Purpose:** Confessional choices and memory tabs  
**Unity application:** Sliced shell/cards; mic/camera icons Simple. Place over 3D Diary Room camera feed.  
**Mockup-quality rule:** Make the character the hero. UI should occupy one side, with purple reserved for diary/strategy state.
- `diary_choice_card_9slice.png`
- `diary_choice_selected_9slice.png`
- `diary_confessional_banner_9slice.png`
- `diary_journal_tab_active_9slice.png`
- `diary_memory_card_9slice.png`
- `diary_shell_9slice.png`
- `diary_tab_inactive_9slice.png`
- `icon_diary_camera.png`
- `icon_diary_mic.png`
## FinalThree — 8 files
**Purpose:** Final 3 presentation and final cut/keep choice  
**Unity application:** Sliced slots/decision panels. Dynamic portraits/TMP.  
**Mockup-quality rule:** Reduce UI clutter drastically. The house should feel emptier; gold and red only mark the consequential final decision.
- `final_choice_cut_9slice.png`
- `final_choice_keep_9slice.png`
- `final_decision_panel_9slice.png`
- `final_three_shell_9slice.png`
- `finalist_slot_9slice.png`
- `finalist_slot_selected_9slice.png`
- `icon_final_three.png`
- `memory_wall_thumbnail_frame_9slice.png`
## Icons_PNG — 8 files
**Purpose:** Diary/camera/memory/endgame icons  
**Unity application:** Simple Sprite.  
**Mockup-quality rule:** Use consistently with the rest of the icon system.
- `calendar.png`
- `camera.png`
- `final3.png`
- `fire.png`
- `memory.png`
- `mic.png`
- `spark.png`
- `vote.png`
## MemoryWall — 7 files
**Purpose:** Active/evicted/finalist/winner portrait states  
**Unity application:** Sliced portrait frames + Simple eviction X overlay.  
**Mockup-quality rule:** Desaturate evictees; keep finalists/winner brighter. The emotional impact comes from contrast and emptying the wall over time.
- `evicted_overlay_x.png`
- `icon_memory.png`
- `memory_portrait_active_9slice.png`
- `memory_portrait_evicted_9slice.png`
- `memory_portrait_finalist_9slice.png`
- `memory_portrait_winner_9slice.png`
- `memory_wall_shell_9slice.png`
## ScheduleTimeline — 9 files
**Purpose:** Day schedule and event progression  
**Unity application:** Sliced cards; timeline rail Simple. Populate event list dynamically.  
**Mockup-quality rule:** Use color by event class while current event gets cyan focus. Avoid turning every scheduled item into a bright card.
- `icon_calendar.png`
- `schedule_day_header_9slice.png`
- `schedule_event_active_9slice.png`
- `schedule_event_card_9slice.png`
- `schedule_event_danger_9slice.png`
- `schedule_event_power_9slice.png`
- `schedule_event_social_9slice.png`
- `schedule_shell_9slice.png`
- `timeline_rail.png`
## SocialEvents — 8 files
**Purpose:** Drama, social, secret, romance alerts  
**Unity application:** Sliced alert cards. Queue no more than one or two visible simultaneously.  
**Mockup-quality rule:** These should feel like opportunities, not spam. Alert color communicates category; the rest stays neutral.
- `dismiss_button_9slice.png`
- `drama_alert_9slice.png`
- `icon_drama.png`
- `investigate_button_9slice.png`
- `location_chip_9slice.png`
- `romance_alert_9slice.png`
- `secret_alert_9slice.png`
- `social_alert_9slice.png`
## Transitions — 7 files
**Purpose:** Full-screen cinematic overlays  
**Unity application:** Simple Sprite stretched to full canvas; Raycast Target OFF; animate alpha only.  
**Mockup-quality rule:** Use very briefly (100–500 ms for flashes/washes, longer for vignette). Do not leave color overlays on gameplay.
- `broadcast_scanlines.png`
- `cinematic_vignette.png`
- `transition_cyan_overlay.png`
- `transition_gold_overlay.png`
- `transition_purple_overlay.png`
- `transition_red_overlay.png`
- `white_flash_overlay.png`

# Screen-by-screen assembly recipes
| Screen | Pull from | High-quality composition |
|---|---|---|
| **Choose Your Houseguest** | Pack 1: background, chips, rings, badges; Pack 2: houseguest states; Pack 3: menu buttons if needed | Dark background → title/filter row → 3–4 column portrait grid → one selected cyan card/ring → archetype chips → one primary action. Use real player portraits, SoftMask, gold portrait ring, cyan only on the selected player. |
| **Main Menu** | Pack 1 background; Pack 3 MainMenu + SaveSlots | Use an in-engine/cinematic house image as the real background, then one Continue Season card and quiet secondary actions. Suppress gameplay HUD behind the menu. |
| **Normal Gameplay HUD** | Pack 2 HUD + CharacterStates + LiveFeed + Meters; Pack 1 icons/chips | Thin top bar, narrow left nav, right Live Feed/Recent Events/House Vibe, cast strip at bottom. No bright cyan persistent borders. Cyan appears on current interaction/focus. |
| **Conversation** | Pack 2 ConversationWheel + Dialog + Icons | Open radial wheel near/facing the selected NPC; action petals colored by meaning. After choice, collapse to a small speech card rather than a blocking modal. |
| **Relationship Web** | Pack 2 RelationshipWeb + Meters + CharacterStates | Give graph ~70–75% of screen. Portrait nodes are large; lines carry relationship color; selected node uses cyan. Detail panel occupies the remaining side. |
| **Information Notebook** | Pack 2 Notebook; Pack 3 NotebookIntel | Use tabs/filters and large intel cards. Confirmed/suspected/rumor tags indicate certainty; targets/promises/votes encode content type. |
| **Houseguest Dossier** | Pack 3 Dossier + Pack 2 Meters + character state badges | Large portrait and status column, stats/threat in center, allies/rivals/promises/history on right/bottom. Use semantic chips rather than borders everywhere. |
| **Diary Room** | Pack 4 DiaryRoom + BroadcastOverlays | 3D Diary Room view with the avatar visible; choice cards stacked on one side. Purple is reserved for the diary/strategy context. Animate choice selection with subtle scale/glow. |
| **Nomination / Veto / Eviction** | Pack 2 Ceremony + Pack 4 CeremonyCinematics + Transitions | Hide most persistent HUD. Use Timeline/Cinemachine shots; lower thirds animate in, portraits/reactions punctuate the result, then transition back to gameplay. |
| **Quiz Competition** | Pack 2 Competition + Pack 3 CompetitionHUD | Large question, A/B choices, timer ring, compact contestant status. Selected answer cyan; elimination red; leader gold. |
| **Endurance Competition** | Pack 3 CompetitionHUD + Pack 2 Meters | Minimal HUD: stamina/fatigue, timer, remaining players. Let the 3D spectacle dominate. |
| **Veto Competition** | Pack 3 CompetitionHUD + Pack 2 CharacterStates | Six contestant cards, active-player cyan state, standings/progress, compact timer. Keep other HUD hidden. |
| **Week Recap** | Pack 3 SeasonRecap + Pack 4 BroadcastOverlays/Transitions | Animate HOH, nominees, veto, eviction, relationship shifts in sequence. Use brief gold/red highlights, then fade back to neutral. |
| **Schedule / Time** | Pack 4 ScheduleTimeline | Timeline rail on left/center, event cards on right. Current event cyan, power gold, danger red, social green; future/past muted. |
| **Memory Wall** | Pack 4 MemoryWall | Active portraits neutral, evicted portraits desaturated with X overlay, finalists/winner framed gold. Keep the wall itself mostly dark. |
| **Final Three / Finale** | Pack 4 FinalThree + Pack 3 FinaleJury | Use large finalist slots and dramatic negative space. Jury questions in neutral panels; vote/winner moments use gold; final cut decision uses red/green. |
| **Autonomous Drama Alert** | Pack 4 SocialEvents | One alert card at a time near the edge of the HUD. Location chip + short description + Investigate action. Auto-dismiss if ignored after a safe interval. |

# Layering order inside a typical uGUI prefab

Use this as the default layering pattern so the assets feel dimensional without interfering with interaction:

```text
Screen/Panel Root
├── Background Image                (Sliced, resting chrome)
├── Optional Glow                   (Simple, low alpha, Raycast OFF)
├── Content Mask / Portrait Mask
│   └── Portrait / Thumbnail
├── Decorative Ring / State Badge   (Raycast OFF)
├── TMP Labels / Values / Body Copy
├── Interactive Button / Toggle / Slider
│   ├── Icon
│   └── TMP Caption
├── Focus / Selected Overlay        (Raycast OFF)
└── Tooltip / Toast Layer
```

For tested buttons and controls, keep the **existing interactive GameObject** as the clickable target. Replace its sprite or add child visuals; do not move the caption into a new untested hierarchy unless necessary.

# Micro-animation recipe

To get the premium feel from the static sprites, use short, consistent motion:

- Hover/focus: `100–140 ms`, scale `1.00 → 1.015–1.025`, border/glow alpha rises slightly.
- Selection: `140–200 ms`, cyan edge/glow fades in; selected card can lift `4–8 px`.
- Panel open: `180–240 ms`, alpha `0→1` + anchored-position slide `12–24 px`.
- Toast: `160–220 ms` in, hold `2–4 s`, `120–180 ms` out.
- Ceremony lower-third: `200–300 ms` slide/fade, then remove after the reveal.
- Value changes: animate meter width over `250–400 ms`; avoid bouncing.
- Full-screen transition washes: usually `100–500 ms`; vignette may persist during cinematic camera shots.

Keep motion subtle. The mockups feel polished because **only the changing/important item moves**.

# Practical implementation sequence

1. **Apply Pack 1 globally** to establish the baseline look.
2. **Skin the persistent HUD with Pack 2** without changing tested layout behavior.
3. **Replace cast card states and portrait framing** so the people become the visual focus.
4. **Wire Conversation / Live Feed / Meters / Relationship Web** using Pack 2.
5. **Upgrade the main menu, saves, dossier, notebook, and competition screens** with Pack 3.
6. **Add Pack 4 only after core screens are visually stable**, because its cinematic overlays should sit above—not substitute for—the normal UI.
7. **Run screenshot/regression checks at 1080p and your supported aspect ratios.**
8. **Audit Accent/Cyan usage.** If more than a few resting elements are cyan at once, the hierarchy is drifting.
9. **Audit visual density.** If the world/portrait is less prominent than the UI frame on normal gameplay screens, reduce panel area/opacity.
10. **Add animation last.** Static hierarchy should already read correctly before DOTween/UIEffect polish.
