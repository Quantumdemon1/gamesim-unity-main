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
    }
}
