namespace Gamesim.Presentation
{
    /// <summary>
    /// The UI pack sprites the HUD draws with, by path under <c>Resources/Packs</c> and without the
    /// extension - what <see cref="UiTheme.Pack"/> takes.
    ///
    /// <para>Named here rather than typed at the call site so one test can load every one of them:
    /// a misspelt path is not an error at runtime, it is the procedural fallback drawn quietly
    /// where the art should be, which reads as "the packs look like this" rather than as a bug.
    /// <c>PackArtTests</c> holds each name to a sprite and to the importer's catalogue.</para>
    ///
    /// <para>What is NOT here, on purpose (ASSET-PACKS.md has the measurements): the relationship
    /// <c>line_*</c> sprites, which are arrows with inverted colour meanings; the conversation
    /// <c>wheel_petal_*</c> sprites, which are pentagon arrows, not the mockups' round petals; and
    /// the <c>Meters/*_fill</c> sprites, which bake a 60 % fill into an unsliced track.</para>
    /// </summary>
    public static class PackArt
    {
        // Foundation.
        public const string GlowCyan = "Pack1_Foundation/Glows/glow_cyan";
        public const string GlowGold = "Pack1_Foundation/Glows/glow_gold";
        public const string GlowPurple = "Pack1_Foundation/Glows/glow_purple";
        public const string GlowRed = "Pack1_Foundation/Glows/glow_red";
        public const string SelectionRing = "Pack1_Foundation/Rings/selection_ring_cyan";
        public const string DividerSoftBlue = "Pack1_Foundation/Dividers/divider_soft_blue";
        public const string ButtonPrimary = "Pack1_Foundation/Buttons/button_primary_9slice";
        public const string ButtonSecondary = "Pack1_Foundation/Buttons/button_secondary_9slice";
        public const string PanelResting = "Pack1_Foundation/Panels/panel_resting_9slice";
        public const string PanelSelected = "Pack1_Foundation/Panels/panel_selected_9slice";
        public const string PanelDanger = "Pack1_Foundation/Panels/panel_danger_9slice";
        public const string BadgeSelected = "Pack1_Foundation/Badges/badge_selected";
        public const string IconHome = "Pack1_Foundation/Icons_PNG/home";
        public const string IconUser = "Pack1_Foundation/Icons_PNG/user";
        public const string IconGrid = "Pack1_Foundation/Icons_PNG/grid";
        public const string IconStar = "Pack1_Foundation/Icons_PNG/star";
        public const string IconTrophy = "Pack1_Foundation/Icons_PNG/trophy";
        public const string IconSave = "Pack1_Foundation/Icons_PNG/save";
        public const string BackgroundNavy = "Pack1_Foundation/Backgrounds/background_dark_navy_1920x1080";

        // Gameplay.
        public const string LeftNavRail = "Pack2_Gameplay/HUD/left_nav_rail_9slice";
        public const string TopStatusBar = "Pack2_Gameplay/HUD/top_status_bar_9slice";
        public const string ObjectiveCardActive = "Pack2_Gameplay/HUD/objective_card_active_9slice";
        public const string CastCardResting = "Pack2_Gameplay/CharacterStates/houseguest_card_resting_9slice";
        public const string CastCardSelected = "Pack2_Gameplay/CharacterStates/houseguest_card_selected_9slice";
        public const string CastCardNominee = "Pack2_Gameplay/CharacterStates/houseguest_card_nominee_9slice";
        public const string WheelHub = "Pack2_Gameplay/ConversationWheel/wheel_center_hub";
        public const string WheelRing = "Pack2_Gameplay/ConversationWheel/wheel_base_ring";
        public const string SpeechBubble = "Pack2_Gameplay/Dialog/speech_bubble_9slice";
        public const string WhisperBubble = "Pack2_Gameplay/Dialog/secret_whisper_bubble_9slice";
        public const string Nameplate = "Pack2_Gameplay/Dialog/npc_nameplate_9slice";
        public const string NodeSelected = "Pack2_Gameplay/RelationshipWeb/node_selected";
        public const string SideInfoPanel = "Pack2_Gameplay/RelationshipWeb/side_info_panel_9slice";
        public const string Nominee = "Pack2_Gameplay/Ceremony/nominee_slot_9slice";
        public const string EvictionCandidate = "Pack2_Gameplay/Ceremony/eviction_candidate_frame_9slice";
        public const string IconAlliance = "Pack2_Gameplay/Icons_PNG/alliance";
        public const string IconCalendar = "Pack2_Gameplay/Icons_PNG/calendar";
        public const string IconChat = "Pack2_Gameplay/Icons_PNG/chat";
        public const string IconDiary = "Pack2_Gameplay/Icons_PNG/diary";
        public const string IconLock = "Pack2_Gameplay/Icons_PNG/lock";
        public const string IconTarget = "Pack2_Gameplay/Icons_PNG/target";
        public const string IconVote = "Pack2_Gameplay/Icons_PNG/vote";

        // Presentation.
        public const string Vignette = "Pack4_Presentation/Transitions/cinematic_vignette";
        public const string IconFire = "Pack4_Presentation/Icons_PNG/fire";
        /// <summary>A story moment in the house: the Pull's mark (plan §5.1).</summary>
        public const string IconDrama = "Pack4_Presentation/SocialEvents/icon_drama";
        /// <summary>A voter's row on the eviction's board on the living room's screen (MOCKUP-PASS-PLAN M18).</summary>
        public const string VoteRevealStrip = "Pack4_Presentation/CeremonyCinematics/vote_reveal_strip_9slice";
        /// <summary>The chip on that row naming the nominee the voter evicts. Its keep twin is not used: see VoteReveal.</summary>
        public const string VoteChipEvict = "Pack4_Presentation/CeremonyCinematics/vote_chip_evict_9slice";

        // Refinement Kit 6: white, tintable parts. A fill, a resting edge and a focus edge share one
        // size and one border per family, so a state changes a tint or a layer, never a rect.
        public const string KitCardFill = "Kit6_Refinement/Chrome/card_fill";
        public const string KitCardEdge = "Kit6_Refinement/Chrome/card_edge_rest";
        public const string KitCardEdgeFocus = "Kit6_Refinement/Chrome/card_edge_focus";
        public const string KitPillFill = "Kit6_Refinement/Chrome/pill_fill";
        public const string KitPillEdge = "Kit6_Refinement/Chrome/pill_edge";
        public const string KitButtonFill = "Kit6_Refinement/Chrome/button_fill";
        public const string KitButtonEdge = "Kit6_Refinement/Chrome/button_edge_rest";
        public const string KitPanelFocusHalo = "Kit6_Refinement/Chrome/panel_focus_halo";
        public const string KitMeterTrack = "Kit6_Refinement/Widgets/meter_track";
        public const string KitMeterFillRect = "Kit6_Refinement/Widgets/meter_fill_rect";
        public const string KitMeterZeroTick = "Kit6_Refinement/Widgets/meter_zero_tick";
        public const string KitTimelineRing = "Kit6_Refinement/Widgets/timeline_ring";
        public const string KitIconShield = "Kit6_Refinement/Icons/ic_shield";
        public const string KitIconQuestion = "Kit6_Refinement/Icons/ic_question";
        public const string KitIconCheck = "Kit6_Refinement/Icons/ic_check";
        public const string KitIconCross = "Kit6_Refinement/Icons/ic_cross";
        public const string KitIconClock = "Kit6_Refinement/Icons/ic_clock";
        public const string KitIconWarning = "Kit6_Refinement/Icons/ic_warning";
        public const string KitIconHeart = "Kit6_Refinement/Icons/ic_heart";
        public const string KitIconRefresh = "Kit6_Refinement/Icons/ic_refresh";
        public const string KitIconSave = "Kit6_Refinement/Icons/ic_save";
        public const string KitIconCalendar = "Kit6_Refinement/Icons/ic_calendar";
        public const string KitIconMore = "Kit6_Refinement/Icons/ic_more";
        public const string KitIconEye = "Kit6_Refinement/Icons/ic_eye";
        public const string KitIconChat = "Kit6_Refinement/Icons/ic_chat";
        public const string KitIconHome = "Kit6_Refinement/Icons/ic_home";
        public const string KitIconStory = "Kit6_Refinement/Icons/ic_story";
        public const string KitIconArchive = "Kit6_Refinement/Icons/ic_archive";
        // The character creator's own pieces (Pack 2): steps, category tiles, sliders, swatches, thumbnails.
        public const string CreatorStepActive = "Pack2_Gameplay/CharacterCreator/creator_step_active_9slice";
        public const string CreatorStepInactive = "Pack2_Gameplay/CharacterCreator/creator_step_inactive_9slice";
        public const string CreatorCategoryTile = "Pack2_Gameplay/CharacterCreator/creator_category_tile_9slice";
        public const string CreatorSliderTrack = "Pack2_Gameplay/CharacterCreator/creator_slider_track_9slice";
        public const string CreatorSliderFill = "Pack2_Gameplay/CharacterCreator/creator_slider_fill_9slice";
        public const string CreatorSliderHandle = "Pack2_Gameplay/CharacterCreator/creator_slider_handle";
        public const string CreatorSwatchesPanel = "Pack2_Gameplay/CharacterCreator/creator_swatches_panel_9slice";
        public const string CreatorThumbnail = "Pack2_Gameplay/CharacterCreator/creator_thumbnail_frame_9slice";
        public const string CreatorThumbnailSelected = "Pack2_Gameplay/CharacterCreator/creator_thumbnail_selected_9slice";
        // The competition art Pack 2 and Pack 3 drew for the games.
        public const string AnswerTile = "Pack2_Gameplay/Competition/answer_tile_9slice";
        public const string StaminaPanel = "Pack3_Systems/CompetitionHUD/endurance_stamina_panel_9slice";
        /// <summary>The panel a juror's question sits in, beside the asker's card (MOCKUP-PASS M11).</summary>
        public const string JuryQuestionPanel = "Pack3_Systems/FinaleJury/jury_question_panel_9slice";
        public const string KitSelectionStripe = "Kit6_Refinement/Widgets/selection_stripe";
        public const string KitDivider = "Kit6_Refinement/Widgets/divider_h";
        public const string KitIconBed = "Kit6_Refinement/Icons/ic_bed";
        public const string KitIconSofa = "Kit6_Refinement/Icons/ic_sofa";
        public const string KitIconKitchen = "Kit6_Refinement/Icons/ic_kitchen";
        public const string KitIconGamepad = "Kit6_Refinement/Icons/ic_gamepad";
        public const string KitIconCrown = "Kit6_Refinement/Icons/ic_crown";
        public const string KitIconDiary = "Kit6_Refinement/Icons/ic_diary";
        public const string KitIconLocation = "Kit6_Refinement/Icons/ic_location";
        public const string KitIconLocationUnknown = "Kit6_Refinement/Icons/ic_location_unknown";
        public const string KitIconInfo = "Kit6_Refinement/Icons/ic_info";
        public const string KitIconPeople = "Kit6_Refinement/Icons/ic_people";
        public const string KitIconSearch = "Kit6_Refinement/Icons/ic_search";
        public const string KitIconChevronRight = "Kit6_Refinement/Icons/ic_chevron_right";
        public const string KitIconArrowBack = "Kit6_Refinement/Icons/ic_arrow_back";
        public const string KitIconBallot = "Kit6_Refinement/Icons/ic_ballot";
        public const string KitIconLock = "Kit6_Refinement/Icons/ic_lock";
        public const string KitIconBook = "Kit6_Refinement/Icons/ic_book";
        public const string KitIconPerson = "Kit6_Refinement/Icons/ic_person";
        public const string KitIconJury = "Kit6_Refinement/Icons/ic_jury";
        public const string KitIconNote = "Kit6_Refinement/Icons/ic_note";
        public const string KitIconChevronDown = "Kit6_Refinement/Icons/ic_chevron_down";
        public const string KitEmptyVotes = "Kit6_Refinement/EmptyStates/votes_no_records";
        public const string KitEmptyPrivate = "Kit6_Refinement/EmptyStates/private_no_decision";

        // The finale's card (Pack 3): the juror's two cases on the vote (MOCKUP-PASS M8, mockup 50).
        public const string FinalistCard = "Pack3_Systems/FinaleJury/finalist_card_9slice";

        // Season Complete Pack 7: the season's end and the week's (SeasonReport, WeeklyRecapScreen;
        // ASSET-PACKS.md, Pack 7). The chart strips (Charts/bar_*, jury_vote_stacked_bar) are not
        // here on purpose: each bakes a fill into its pixels, and a bar drawn from one would show
        // a number the season never had. Bars are drawn from the real counts instead.
        public const string SeasonSection = "Pack7_SeasonComplete/Shells/section_panel_9slice";
        /// <summary>The report's title card (MOCKUP-PASS M5): the section frame lit, with a 38 px glow.</summary>
        public const string SeasonSectionSelected = "Pack7_SeasonComplete/Shells/section_panel_selected_9slice";
        public const string SeasonQuote = "Pack7_SeasonComplete/Shells/quote_panel_9slice";
        public const string SeasonWinnerHero = "Pack7_SeasonComplete/WinnerHero/winner_hero_frame_9slice";
        public const string SeasonRunnerUpHero = "Pack7_SeasonComplete/WinnerHero/runnerup_hero_frame_9slice";
        /// <summary>The plates the finalists' names sit on in the hero (MOCKUP-PASS M6); the winner's has a 38 px glow.</summary>
        public const string SeasonWinnerNameplate = "Pack7_SeasonComplete/WinnerHero/winner_nameplate_9slice";
        public const string SeasonRunnerUpNameplate = "Pack7_SeasonComplete/WinnerHero/runnerup_nameplate_9slice";
        public const string SeasonWinnerCrown = "Pack7_SeasonComplete/WinnerHero/winner_crown";
        public const string SeasonTrophy = "Pack7_SeasonComplete/WinnerHero/winner_trophy";
        public const string SeasonStatNeutral = "Pack7_SeasonComplete/StatCards/stat_card_neutral_9slice";
        public const string SeasonStatGameSense = "Pack7_SeasonComplete/StatCards/stat_game_sense_9slice";
        public const string SeasonStatCompetitions = "Pack7_SeasonComplete/StatCards/stat_competitions_9slice";
        public const string SeasonStatStrategy = "Pack7_SeasonComplete/StatCards/stat_strategy_9slice";
        public const string SeasonStatSocial = "Pack7_SeasonComplete/StatCards/stat_social_9slice";
        public const string SeasonStatChances = "Pack7_SeasonComplete/StatCards/stat_chances_9slice";
        public const string SeasonIconGameSense = "Pack7_SeasonComplete/Icons/stat_game_sense_icon";
        public const string SeasonIconCompetitions = "Pack7_SeasonComplete/Icons/stat_competitions_icon";
        public const string SeasonIconStrategy = "Pack7_SeasonComplete/Icons/stat_strategy_icon";
        public const string SeasonIconSocial = "Pack7_SeasonComplete/Icons/stat_social_icon";
        public const string SeasonIconChances = "Pack7_SeasonComplete/Icons/stat_chances_icon";
        public const string SeasonStandingWinner = "Pack7_SeasonComplete/Standings/standing_winner_9slice";
        public const string SeasonStandingRunnerUp = "Pack7_SeasonComplete/Standings/standing_runnerup_9slice";
        public const string SeasonStandingThird = "Pack7_SeasonComplete/Standings/standing_third_9slice";
        public const string SeasonStandingJury = "Pack7_SeasonComplete/Standings/standing_jury_9slice";
        public const string SeasonStandingPreJury = "Pack7_SeasonComplete/Standings/standing_prejury_9slice";
        public const string SeasonJuryRowWinner = "Pack7_SeasonComplete/JuryVotes/jury_vote_row_winner_9slice";
        public const string SeasonJuryRowRunnerUp = "Pack7_SeasonComplete/JuryVotes/jury_vote_row_runnerup_9slice";
        public const string SeasonJuryRowNeutral = "Pack7_SeasonComplete/JuryVotes/jury_vote_row_neutral_9slice";
        public const string SeasonJurySummary = "Pack7_SeasonComplete/JuryVotes/jury_vote_summary_bar_9slice";
        public const string SeasonWeekRow = "Pack7_SeasonComplete/Timeline/week_row_9slice";
        public const string SeasonWeekRowPower = "Pack7_SeasonComplete/Timeline/week_row_power_9slice";
        public const string SeasonWeekRowEviction = "Pack7_SeasonComplete/Timeline/week_row_eviction_9slice";
        public const string SeasonNodeNormal = "Pack7_SeasonComplete/Timeline/timeline_node_normal";
        public const string SeasonNodePower = "Pack7_SeasonComplete/Timeline/timeline_node_power";
        public const string SeasonNodeEviction = "Pack7_SeasonComplete/Timeline/timeline_node_eviction";
        public const string SeasonCareerStrip = "Pack7_SeasonComplete/Career/career_strip_9slice";
        public const string SeasonCareerCell = "Pack7_SeasonComplete/Career/career_neutral_9slice";
        public const string SeasonCareerBest = "Pack7_SeasonComplete/Career/career_best_9slice";
        public const string SeasonFilterActive = "Pack7_SeasonComplete/FiltersTabs/filter_active_9slice";
        public const string SeasonFilterInactive = "Pack7_SeasonComplete/FiltersTabs/filter_inactive_9slice";
        public const string SeasonTabInactive = "Pack7_SeasonComplete/FiltersTabs/tab_inactive_9slice";
        public const string SeasonButtonPrimary = "Pack7_SeasonComplete/Buttons/button_new_season_9slice";
        public const string SeasonButton = "Pack7_SeasonComplete/Buttons/button_close_9slice";
        /// <summary>
        /// The report's footer faces (MOCKUP-PASS M5): the notebook's, and the gold 'Continue to
        /// Legacy' face the pinned <c>Main menu</c> wears (decision 21). The pack's
        /// <c>button_main_menu</c> is not named: it is pixel-identical to <see cref="SeasonButton"/>.
        /// </summary>
        public const string SeasonButtonReview = "Pack7_SeasonComplete/Buttons/button_review_season_9slice";
        public const string SeasonButtonLegacy = "Pack7_SeasonComplete/Buttons/button_continue_legacy_9slice";
        public const string SeasonBadgeJury = "Pack7_SeasonComplete/Badges/badge_jury";
        public const string SeasonBadgeRunnerUp = "Pack7_SeasonComplete/Badges/badge_runnerup";

        // Campaign, Veto and Nomination Pack 8: the week's four strategy screens (PACK8-PASS-PLAN;
        // ASSET-PACKS.md, Pack 8). One name per distinct image. Not here on purpose:
        //  - Campaign/Relationship/relationship_*, which bake a fill of 18 to 78 % into the bar; a
        //    standing drawn from one would show a number the player never earned.
        //  - Icons/*, byte for byte the seven Common/icon_* files named below.
        //  - The pack's own twins: Nomination/up_next_strip (Common/info_strip), status_card_objective
        //    (status_card_phase), lobby_listen_pitch and stay_off_block_take_it_on
        //    (Campaign/choice_promise_support), lobby_counter_lobby (Campaign/choice_counter_lobby),
        //    stay_off_block_not_now (Campaign/choice_stay_noncommittal) and intel_row (goal_row).
        //  - Common/modal_shell (pixel for pixel Pack 4's memory wall shell), Common/status_card (the
        //    plain twin of the Nomination cards), Common/toast_saved (the status line's geometry is
        //    pinned), eligible_player_hover (the draw's faces are not controls), arrow_left (the
        //    mockup points both chevrons right) and draw_chip_2 to 6 (the mockup's chips are chip 1's).
        //  - Campaign/houseguest_card_high_value, which would say a voter matters more than the
        //    player has any way of knowing.
        public const string Pack8ButtonPrimary = "Pack8_CampaignVetoNomination/Common/primary_button_9slice";
        public const string Pack8ButtonGold = "Pack8_CampaignVetoNomination/Common/gold_button_9slice";
        public const string Pack8ButtonSecondary = "Pack8_CampaignVetoNomination/Common/secondary_button_9slice";
        public const string Pack8ButtonDanger = "Pack8_CampaignVetoNomination/Common/danger_button_9slice";
        public const string Pack8HeaderStrip = "Pack8_CampaignVetoNomination/Common/header_strip_9slice";
        /// <summary>The strip an info line and the footer's Up next line sit on.</summary>
        public const string Pack8InfoStrip = "Pack8_CampaignVetoNomination/Common/info_strip_9slice";
        public const string Pack8Section = "Pack8_CampaignVetoNomination/Common/section_shell_9slice";
        public const string Pack8IconHoh = "Pack8_CampaignVetoNomination/Common/icon_hoh";
        public const string Pack8IconPeople = "Pack8_CampaignVetoNomination/Common/icon_people";
        public const string Pack8IconChat = "Pack8_CampaignVetoNomination/Common/icon_chat";
        public const string Pack8IconTarget = "Pack8_CampaignVetoNomination/Common/icon_target";
        public const string Pack8IconVeto = "Pack8_CampaignVetoNomination/Common/icon_veto";
        public const string Pack8IconInfo = "Pack8_CampaignVetoNomination/Common/icon_info";
        /// <summary>An OPEN padlock; a closed one is <see cref="KitIconLock"/>.</summary>
        public const string Pack8IconLock = "Pack8_CampaignVetoNomination/Common/icon_lock";
        /// <summary>The nomination's frame: its body is the strategy stage at the 1600x900 reference, 1360x800.</summary>
        public const string Pack8NominationShell = "Pack8_CampaignVetoNomination/Nomination/nomination_shell_9slice";
        public const string Pack8NominationSummary = "Pack8_CampaignVetoNomination/Nomination/nomination_summary_panel_9slice";
        public const string Pack8StatusHoh = "Pack8_CampaignVetoNomination/Nomination/status_card_hoh_9slice";
        /// <summary>The red-edged status card: the Phase card's and the Objective's, which the pack draws alike.</summary>
        public const string Pack8StatusPhase = "Pack8_CampaignVetoNomination/Nomination/status_card_phase_9slice";
        public const string Pack8StatusActions = "Pack8_CampaignVetoNomination/Nomination/status_card_actions_9slice";
        /// <summary>A tracker's steps. The three share one 330x80 body, so a step changes its sprite and never its rect.</summary>
        public const string Pack8PhaseComplete = "Pack8_CampaignVetoNomination/Nomination/phase_complete_9slice";
        public const string Pack8PhaseCurrent = "Pack8_CampaignVetoNomination/Nomination/phase_current_9slice";
        public const string Pack8PhaseNext = "Pack8_CampaignVetoNomination/Nomination/phase_next_9slice";
        public const string Pack8HouseguestHoh = "Pack8_CampaignVetoNomination/Nomination/houseguest_hoh_9slice";
        public const string Pack8HouseguestNeutral = "Pack8_CampaignVetoNomination/Nomination/houseguest_neutral_9slice";
        public const string Pack8HouseguestNominee = "Pack8_CampaignVetoNomination/Nomination/houseguest_nominee_9slice";
        public const string Pack8HouseguestSelected = "Pack8_CampaignVetoNomination/Nomination/houseguest_selected_9slice";
        public const string Pack8HouseguestUnavailable = "Pack8_CampaignVetoNomination/Nomination/houseguest_unavailable_9slice";
        public const string Pack8NomRiskLow = "Pack8_CampaignVetoNomination/Nomination/nom_risk_low_9slice";
        public const string Pack8NomRiskSome = "Pack8_CampaignVetoNomination/Nomination/nom_risk_some_9slice";
        public const string Pack8NomRiskHigh = "Pack8_CampaignVetoNomination/Nomination/nom_risk_high_9slice";
        public const string Pack8ContinueButton = "Pack8_CampaignVetoNomination/Nomination/continue_episode_button_9slice";
        /// <summary>
        /// A choice's face, by the kind of answer rather than by the arc that asks it: next week's
        /// beat is a different arc with the same three kinds of answer.
        /// </summary>
        public const string Pack8ChoiceWarm = "Pack8_CampaignVetoNomination/Campaign/choice_promise_support_9slice";
        public const string Pack8ChoiceBold = "Pack8_CampaignVetoNomination/Campaign/choice_counter_lobby_9slice";
        public const string Pack8ChoiceQuiet = "Pack8_CampaignVetoNomination/Campaign/choice_stay_noncommittal_9slice";
        public const string Pack8ChoiceRefuse = "Pack8_CampaignVetoNomination/Campaign/choice_refuse_9slice";
        public const string Pack8VetoShell = "Pack8_CampaignVetoNomination/VetoSelection/veto_selection_shell_9slice";
        public const string Pack8VetoAutoHoh = "Pack8_CampaignVetoNomination/VetoSelection/auto_player_card_hoh_9slice";
        public const string Pack8VetoAutoNominee = "Pack8_CampaignVetoNomination/VetoSelection/auto_player_card_nominee_9slice";
        public const string Pack8VetoEligible = "Pack8_CampaignVetoNomination/VetoSelection/eligible_player_card_9slice";
        public const string Pack8DrawArea = "Pack8_CampaignVetoNomination/VetoSelection/draw_area_9slice";
        public const string Pack8VetoBag = "Pack8_CampaignVetoNomination/VetoSelection/veto_bag_icon";
        public const string Pack8DrawChip = "Pack8_CampaignVetoNomination/VetoSelection/draw_chip_1";
        /// <summary>
        /// The draw's slots. Named _9slice and sliced on import, but they are circles with a stretch
        /// of 12 to 14 px: sliced, one draws a pill. Draw them whole, through <see cref="EndScreenKit.Whole"/>.
        /// </summary>
        public const string Pack8DrawSlotEmpty = "Pack8_CampaignVetoNomination/VetoSelection/draw_slot_empty_9slice";
        public const string Pack8DrawSlotFilled = "Pack8_CampaignVetoNomination/VetoSelection/draw_slot_filled_9slice";
        public const string Pack8DrawnPlayer = "Pack8_CampaignVetoNomination/VetoSelection/drawn_player_card_9slice";
        public const string Pack8RevealButton = "Pack8_CampaignVetoNomination/VetoSelection/reveal_draw_button_9slice";
        public const string Pack8VetoOutcome = "Pack8_CampaignVetoNomination/VetoSelection/veto_outcome_strip_9slice";
        public const string Pack8ArrowRight = "Pack8_CampaignVetoNomination/VetoSelection/arrow_right";
        public const string Pack8CampaignShell = "Pack8_CampaignVetoNomination/Campaign/campaign_shell_9slice";
        public const string Pack8CampaignHero = "Pack8_CampaignVetoNomination/Campaign/nominee_hero_9slice";
        public const string Pack8CampaignHeroDanger = "Pack8_CampaignVetoNomination/Campaign/nominee_hero_danger_9slice";
        public const string Pack8Situation = "Pack8_CampaignVetoNomination/Campaign/situation_panel_9slice";
        /// <summary>A tab, lit and resting. The two share one 260x66 body, so lighting a tab moves no rect.</summary>
        public const string Pack8TabActive = "Pack8_CampaignVetoNomination/Campaign/tab_active_9slice";
        public const string Pack8TabInactive = "Pack8_CampaignVetoNomination/Campaign/tab_inactive_9slice";
        public const string Pack8VoterCard = "Pack8_CampaignVetoNomination/Campaign/houseguest_card_resting_9slice";
        public const string Pack8VoterDanger = "Pack8_CampaignVetoNomination/Campaign/houseguest_card_danger_9slice";
        public const string Pack8VoterSelected = "Pack8_CampaignVetoNomination/Campaign/houseguest_card_selected_9slice";
        public const string Pack8TalkButton = "Pack8_CampaignVetoNomination/Campaign/talk_button_9slice";
        public const string Pack8ActionsLeft = "Pack8_CampaignVetoNomination/Campaign/action_left_card_9slice";
        public const string Pack8GoalPanel = "Pack8_CampaignVetoNomination/Campaign/campaign_goal_panel_9slice";
        public const string Pack8IntelPanel = "Pack8_CampaignVetoNomination/Campaign/recent_intel_panel_9slice";
        /// <summary>A goal's row and an intel row, which the pack draws alike.</summary>
        public const string Pack8CampaignRow = "Pack8_CampaignVetoNomination/Campaign/goal_row_9slice";
        public const string Pack8ProTip = "Pack8_CampaignVetoNomination/Campaign/pro_tip_panel_9slice";
        public const string Pack8RiskLow = "Pack8_CampaignVetoNomination/Campaign/risk_low_9slice";
        public const string Pack8RiskMedium = "Pack8_CampaignVetoNomination/Campaign/risk_medium_9slice";
        public const string Pack8RiskHigh = "Pack8_CampaignVetoNomination/Campaign/risk_high_9slice";
        public const string Pack8TypeStrategic = "Pack8_CampaignVetoNomination/Campaign/type_strategic_9slice";
        /// <summary>The grounds of a voter's read, never its words: the words stay the read's own.</summary>
        public const string Pack8VoteLikelyKeep = "Pack8_CampaignVetoNomination/Campaign/Voting/vote_likely_keep_9slice";
        public const string Pack8VoteLeanKeep = "Pack8_CampaignVetoNomination/Campaign/Voting/vote_lean_keep_9slice";
        public const string Pack8VoteUndecided = "Pack8_CampaignVetoNomination/Campaign/Voting/vote_undecided_9slice";
        public const string Pack8VoteLeanEvict = "Pack8_CampaignVetoNomination/Campaign/Voting/vote_lean_evict_9slice";
        public const string Pack8VoteLikelyEvict = "Pack8_CampaignVetoNomination/Campaign/Voting/vote_likely_evict_9slice";
        public const string Pack8VoteUnknown = "Pack8_CampaignVetoNomination/Campaign/Voting/vote_unknown_9slice";
    }
}
