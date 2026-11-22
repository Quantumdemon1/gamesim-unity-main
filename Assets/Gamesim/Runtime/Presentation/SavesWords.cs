using System;
using System.Globalization;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The Saves and Advanced sections in words (menu slice 4, the review's change 2): the list of
    /// saved seasons as cards with Load and Delete, the confirm cards that ask first, and the tools
    /// that put back what is saved. Plain C#, so the Unity-free suite reads the same words the
    /// screens show. A caption is a contract: tests, screen readers and the standalone verify find a
    /// control by it.
    /// </summary>
    public static class SavesWords
    {
        // ------------------------------------------------------------------ the season cards

        /// <summary>The mark on the card of the season being played.</summary>
        public const string PlayingNow = "Playing now";
        /// <summary>
        /// The running season's save, as Settings has always captioned it: the key hint is part of the
        /// caption the pad's chip map and its tests key on (InputGlossary.CaptionAction).
        /// </summary>
        public const string SaveNowCaption = "Save now  [F5]";
        /// <summary>A card's load.</summary>
        public const string LoadCaption = "Load";
        /// <summary>A card's delete.</summary>
        public const string DeleteCaption = "Delete";
        /// <summary>The title of a card whose save cannot be read.</summary>
        public const string Unreadable = "This save cannot be opened";
        /// <summary>The title of a card whose save another program holds for a moment (a backup or sync client): it may be a good save.</summary>
        public const string InUseTitle = "This save is in use";
        /// <summary>The muted line under that card.</summary>
        public const string InUseLine = "Another program has it open for a moment. Try again shortly.";
        /// <summary>That card's one control: the list looks again.</summary>
        public const string TryAgainCaption = "Try again";
        /// <summary>What Saves says with no season at all.</summary>
        public const string NoSaves = "No saved seasons yet.";
        /// <summary>The muted line under the running season's card, which has no Delete.</summary>
        public const string PlayingLine = "This is the season you are playing.";
        /// <summary>The muted line under the save a blocked recovery could not open (it has no Delete either).</summary>
        public const string RecoveryLine = "This is the save the game could not open. Advanced has the tools that can mend it.";
        /// <summary>The running season's card before it was ever written.</summary>
        public const string NotSavedYet = "not saved yet";

        /// <summary>How a card's controls are named: "Load: episode-1a2b" (their captions are the words alone).</summary>
        public static string ControlName(string caption, string fileName) => caption + ": " + fileName;
        /// <summary>How a card is named: "Season card: episode-1a2b".</summary>
        public static string CardName(string fileName) => "Season card: " + fileName;
        /// <summary>The object name of a card's mark, its saved line and its failure line.</summary>
        public const string MarkName = "Season mark", SavedLineName = "Season saved line", FailureLineName = "Season failure line";

        /// <summary>"Week 4".</summary>
        public static string Week(int week) => "Week " + Math.Max(1, week);
        /// <summary>"7 houseguests left", "1 houseguest left".</summary>
        public static string Left(int count) => count + (count == 1 ? " houseguest left" : " houseguests left");
        /// <summary>A card's second line: "Week 4  ·  7 houseguests left".</summary>
        public static string Details(int week, int left) => Week(week) + "  ·  " + Left(left);
        /// <summary>Who a season belongs to on its card and in its questions, when the save names nobody.</summary>
        public const string NoName = "Your houseguest";

        /// <summary>
        /// When a season was saved, in plain words, from the file's time and now (both UTC): "saved just
        /// now", "saved 5 minutes ago", "saved 2 hours ago", "saved yesterday", "saved 3 days ago", or
        /// past a month "saved on 3 October 2026". A time ahead of now (a clock put back) is "just now".
        /// </summary>
        public static string SavedAgo(DateTime savedUtc, DateTime nowUtc)
        {
            var gone = nowUtc - savedUtc;
            if (gone < TimeSpan.FromMinutes(1)) return "saved just now";
            if (gone < TimeSpan.FromHours(1)) return "saved " + Count((int)gone.TotalMinutes, "minute") + " ago";
            if (gone < TimeSpan.FromDays(1)) return "saved " + Count((int)gone.TotalHours, "hour") + " ago";
            if (gone < TimeSpan.FromDays(2)) return "saved yesterday";
            if (gone < TimeSpan.FromDays(30)) return "saved " + Count((int)gone.TotalDays, "day") + " ago";
            return "saved on " + savedUtc.ToLocalTime().ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------------ load and delete

        /// <summary>The safe choice while a season is running: nothing is loaded.</summary>
        public const string KeepPlaying = "Keep playing";
        /// <summary>The load's result: the running season is saved, then the other one loaded.</summary>
        public const string SaveAndLoad = "Save and load";
        /// <summary>The delete's safe choice.</summary>
        public const string KeepIt = "Keep it";
        /// <summary>The delete's result.</summary>
        public const string DeleteThisSeason = "Delete this season";

        /// <summary>"Load Ava, week 4? Your current season is saved first."</summary>
        public static string LoadQuestion(string name, int week) => "Load " + Named(name) + ", week " + Math.Max(1, week) + "? Your current season is saved first.";
        /// <summary>"Delete Ava, week 4? This cannot be undone."</summary>
        public static string DeleteQuestion(string name, int week) => "Delete " + Named(name) + ", week " + Math.Max(1, week) + "? This cannot be undone.";
        /// <summary>The question for a save that cannot be read: it has no name or week to give.</summary>
        public const string DeleteUnreadableQuestion = "Delete this save? It cannot be opened, and this cannot be undone.";
        /// <summary>"Deleted Ava, week 4."</summary>
        public static string Deleted(string name, int week) => "Deleted " + Named(name) + ", week " + Math.Max(1, week) + ".";
        /// <summary>After a save that could not be read was deleted.</summary>
        public const string DeletedUnreadable = "Deleted the save that could not be opened.";
        /// <summary>A file of the season is held by another program: nothing at all was deleted.</summary>
        public const string InUse = "That save is in use. Nothing was deleted.";
        /// <summary>Any other refusal: the store deleted nothing.</summary>
        public const string NotDeleted = "That save could not be deleted. Nothing was deleted.";
        /// <summary>The season being played is never deleted.</summary>
        public const string PlayingNotDeleted = "That is the season you are playing. Nothing was deleted.";
        /// <summary>The running season could not be saved before a load: nothing was loaded.</summary>
        public const string CouldNotSave = "Your current season could not be saved, so nothing was loaded.";
        /// <summary>The season asked for could not be read as it loaded: nothing changed.</summary>
        public const string CouldNotLoad = "That season could not be loaded. Your current season is unchanged.";

        // ------------------------------------------------------------------ Advanced

        /// <summary>Advanced's first tool: what is saved is read again and replaces the session (the review's Reload).</summary>
        public const string GoBackCaption = "Go back to the last save";
        /// <summary>Its result.</summary>
        public const string GoBackResult = "Load the last save";
        /// <summary>Its question while a season is running: anything not yet written is what goes.</summary>
        public const string GoBackQuestion = "Go back to your last save? Anything since it was saved is lost.";
        /// <summary>Its question when no season is running (a save in recovery, or Settings from the main menu).</summary>
        public const string GoBackQuietQuestion = "Open your last save again?";
        /// <summary>The safe choice where no season is running.</summary>
        public const string NotNow = "Not now";
        /// <summary>The backup's question: the backup is the save before the last one, and the current file is kept.</summary>
        public const string RestoreQuestion = "Restore the backup? It is the save before your last one. Your current save file is kept beside it.";
        /// <summary>The backup's safe choice.</summary>
        public const string KeepMySave = "Keep my save";
        /// <summary>The backup's result.</summary>
        public const string RestoreResult = "Restore the backup";
        /// <summary>Why Recover backup cannot be used: there is no backup file yet.</summary>
        public const string NoBackupWhy = "There is no backup yet. The game keeps one once it has saved this season twice.";
        /// <summary>The career reset's question (its two captions are the career's own, EpisodeDirector.Career.cs).</summary>
        public const string ResetCareerQuestion = "Set your career record aside? It is kept as a dated file beside the saves, a fresh one starts, and nothing is deleted.";

        private static string Named(string name) => string.IsNullOrWhiteSpace(name) ? NoNameInASentence : name;

        /// <summary>The nameless houseguest inside a sentence, where a capital would be wrong.</summary>
        public const string NoNameInASentence = "your houseguest";
        private static string Count(int count, string unit) => count + " " + unit + (count == 1 ? "" : "s");
    }
}
