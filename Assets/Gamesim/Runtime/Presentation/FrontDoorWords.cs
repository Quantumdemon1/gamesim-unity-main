namespace Gamesim.Presentation
{
    /// <summary>
    /// The front door's words for the one way back and the one main button (menu slice 2, the
    /// review's changes 1 and 3): the cast screen's way back, which names where it really returns,
    /// its one start, "Edit &lt;name&gt;", and the tour's step back.
    ///
    /// <para>Plain C#, so the Unity-free suite reads the same words the screens draw. A caption is a
    /// contract: tests, screen readers and the standalone verify find a control by it.</para>
    /// </summary>
    public static class FrontDoorWords
    {
        /// <summary>Settings and saves, as a place a way back returns to: the cast screen opened from it goes back there.</summary>
        public const string SettingsPlace = "Settings";

        /// <summary>The season's last page, as a place: its "New season" opens the cast screen, which goes back there.</summary>
        public const string FinalePlace = "the finale";

        /// <summary>What leaving the cast screen keeps when a season is running: that season, untouched.</summary>
        public const string SeasonKept = "season";

        /// <summary>
        /// The cast screen's way back. On a first launch, from a main menu with no season to
        /// continue, there is nothing to keep: "Back to the main menu". Otherwise the running season
        /// is kept, and the words say so and name where leaving lands: <paramref name="place"/> - the
        /// main menu, Settings, the finale or the house - and the main menu when none is named.
        /// </summary>
        public static string CastBack(bool freshStart, string place) =>
            freshStart ? WayOut.BackTo(WayOut.MainMenu)
                : WayOut.BackToKeeping(string.IsNullOrWhiteSpace(place) ? WayOut.MainMenu : place, SeasonKept);

        /// <summary>How the cast screen's one start begins; the rest is who the season starts as.</summary>
        public const string StartAsPrefix = "Start as ";

        /// <summary>The start with nobody picked and nothing built: the season starts as an unaffiliated newcomer.</summary>
        public const string StartAsNewcomer = StartAsPrefix + "a newcomer";

        /// <summary>
        /// The one start, named by the first name the season will start as. A houseguest built with no
        /// name yet is "your houseguest", never a newcomer: a newcomer is the start with nobody at all.
        /// </summary>
        public static string StartAs(string firstName) =>
            StartAsPrefix + (string.IsNullOrWhiteSpace(firstName) ? "your houseguest" : firstName.Trim());

        /// <summary>Changing a houseguest: "Edit &lt;name&gt;", or "Edit houseguest" where no name is at hand (WORDS W1).</summary>
        public static string Edit(string firstName) =>
            string.IsNullOrWhiteSpace(firstName) ? "Edit houseguest" : "Edit " + firstName.Trim();

        /// <summary>
        /// The tour's step back to the card before. Not a way out of the tour - "Skip the tour" is -
        /// so it is not worded as one: a bare "Back" is an exit word the slice folds away.
        /// </summary>
        public const string TourPrevious = "Previous";
    }
}
