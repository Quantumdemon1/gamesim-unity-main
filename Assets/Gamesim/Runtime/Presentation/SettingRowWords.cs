using System.Collections.Generic;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The Sound, Display, Accessibility and Controls rows of Settings in words (menu slice 4, part b):
    /// each setting's NAME, which is its row's caption - what tests, screen readers and the standalone
    /// verify find it by - and the words its value reads. A name says what the setting is ("Music"),
    /// never what a press would do ("Turn music off"): the value beside it ("On") says how it stands.
    ///
    /// <para>Plain C#, so the Unity-free suite holds the words the screens draw. Every setting keeps the
    /// preference key and the values it always had; only how it reads changed.</para>
    /// </summary>
    public static class SettingRowWords
    {
        // ------------------------------------------------------------------ Sound

        /// <summary>The master volume's slider (the name it always had, which its object carries).</summary>
        public const string MasterVolumeRow = "Master volume";
        /// <summary>Every sound at once: On, or Off when the game is muted.</summary>
        public const string AllSoundRow = "All sound";
        /// <summary>The music: On or Off.</summary>
        public const string MusicRow = "Music";

        // ------------------------------------------------------------------ Display

        public const string CompactHudRow = "Compact HUD";
        public const string QualityRow = "Quality";
        public const string VSyncRow = "VSync";
        public const string FrameRateRow = "Frame rate";
        public const string DisplayModeRow = "Display mode";
        public const string ResolutionRow = "Resolution";
        /// <summary>Shown only with more than one display, as before: a choice of one is a dead control.</summary>
        public const string MonitorRow = "Monitor";
        public const string HouseViewRow = "House view";
        /// <summary>Shown only when more than one language ships, as before.</summary>
        public const string LanguageRow = "Language";

        // ------------------------------------------------------------------ Accessibility

        public const string ReducedMotionRow = "Reduced motion";
        public const string ReducedSoundRow = "Reduced sound";
        public const string TextSizeRow = "Text size";
        public const string CaptionTimeRow = "Caption time";
        public const string CeremonyPaceRow = "Ceremony pace";

        // ------------------------------------------------------------------ Controls

        public const string CameraSpeedRow = "Camera speed";
        public const string CameraTiltRow = "Camera tilt";
        public const string EdgePanningRow = "Edge panning";

        /// <summary>Every setting row's name in the four sections, in the order they are drawn.</summary>
        public static readonly IReadOnlyList<string> Rows = new[]
        {
            AllSoundRow, MasterVolumeRow, MusicRow,
            CompactHudRow, QualityRow, VSyncRow, FrameRateRow, DisplayModeRow, ResolutionRow, MonitorRow, HouseViewRow, LanguageRow,
            ReducedMotionRow, ReducedSoundRow, TextSizeRow, CaptionTimeRow, CeremonyPaceRow,
            CameraSpeedRow, CameraTiltRow, EdgePanningRow,
        };

        // ------------------------------------------------------------------ values

        /// <summary>The text sizes as the row reads them, smallest first (the order of <see cref="TextSizes.All"/>).</summary>
        public static readonly IReadOnlyList<string> TextSizeValues = new[] { "Standard", "Larger", "Largest" };

        /// <summary>What the text size row reads for a size.</summary>
        public static string TextSizeValue(TextSize size) => TextSizeValues[IndexOf(size)];

        /// <summary>A size's place in <see cref="TextSizeValues"/>.</summary>
        public static int IndexOf(TextSize size) => size == TextSize.Largest ? 2 : size == TextSize.Larger ? 1 : 0;

        /// <summary>The caption times as the row reads them: the standard, then the longer.</summary>
        public static readonly IReadOnlyList<string> CaptionTimeValues = new[] { "Standard", "Longer" };

        /// <summary>The caption time at a place in <see cref="CaptionTimeValues"/>.</summary>
        public static CaptionTime CaptionTimeAt(int index) => index == 1 ? CaptionTime.Longer : CaptionTime.Standard;

        /// <summary>The ceremonies' paces as the row reads them: suspenseful (the default), then quick.</summary>
        public static readonly IReadOnlyList<string> CeremonyPaceValues = new[] { "Suspenseful", "Quick" };

        /// <summary>The pace at a place in <see cref="CeremonyPaceValues"/>.</summary>
        public static CeremonyPace CeremonyPaceAt(int index) => index == 1 ? CeremonyPace.Quick : CeremonyPace.Suspenseful;

        /// <summary>The house view's two readings: one room at a time (the default), or the whole house.</summary>
        public static readonly IReadOnlyList<string> HouseViewValues = new[] { "One room", "Whole house" };

        /// <summary>The camera's tilt: as built, or the other way.</summary>
        public static readonly IReadOnlyList<string> CameraTiltValues = new[] { "Normal", "Inverted" };

        /// <summary>What the monitor row reads: "2 of 3".</summary>
        public static string MonitorValue(int number, int count) => number + " of " + count;

        // ------------------------------------------------------------------ why a row cannot be used

        /// <summary>The quality row on a computer that offers a single level.</summary>
        public const string OneQualityLevelLine = "Only one quality level is available here.";

        /// <summary>The tour's row from the main menu's Settings: the tour plays over the house.</summary>
        public const string TourFromTheHouseLine = "Only from Settings in the house.";
        /// <summary>The tour's row while a save cannot be read.</summary>
        public const string TourInRecoveryLine = "Not while a save needs recovering.";
        /// <summary>The tour's row while the opening plays.</summary>
        public const string TourInTheOpeningLine = "Not during the opening.";

        /// <summary>
        /// Why the tour cannot be replayed, or null when it can: from the main menu, in blocked
        /// recovery, during the opening (the order the director asks them in).
        /// </summary>
        public static string TourWhyNot(bool fromMainMenu, bool blockedRecovery, bool openingPlays) =>
            fromMainMenu ? TourFromTheHouseLine : blockedRecovery ? TourInRecoveryLine : openingPlays ? TourInTheOpeningLine : null;

        /// <summary>The words a setting's name may not begin with: a name says what it is, not what a press does.</summary>
        public static readonly IReadOnlyList<string> ActionWords = new[]
        {
            "Turn", "Use", "Make", "Mute", "Show", "Hide", "Stop", "Let", "Reduce ", "Enable", "Disable", "Invert", "Restore", "Play", "Change",
        };
    }
}
