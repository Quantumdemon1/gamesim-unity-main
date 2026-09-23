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
    }
}
