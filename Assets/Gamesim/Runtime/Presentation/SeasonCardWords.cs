using System;
using System.Collections.Generic;
using System.Globalization;
using Gamesim.Persistence;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The words of the main menu's season card: which season "Continue your season" opens, as
    /// "Week 4 · 7 houseguests left · saved 2 hours ago". Plain text from a <see cref="SaveHeader"/>
    /// and the clock; no engine and no screen, so the Unity-free suite reads them.
    /// </summary>
    public static class SeasonCardWords
    {
        /// <summary>The card's heading. The card is not a control; "Continue your season" stays the control.</summary>
        public const string Heading = "Your saved season";
        public const string Separator = " · ";

        /// <summary>
        /// The card's line of facts, or null when the header has none (a save that is held, missing or
        /// unreadable): the card then shows its heading alone.
        /// </summary>
        public static string Facts(SaveHeader header, DateTime nowUtc)
        {
            if (header == null || !header.HasFacts) return null;
            var parts = new List<string> { "Week " + header.Week.Value };
            int left = header.Left.Value;
            parts.Add(left == 0 ? "season finished" : left == 1 ? "1 houseguest left" : left + " houseguests left");
            if (header.SavedUtc.HasValue) parts.Add(Saved(header.SavedUtc.Value, nowUtc));
            return string.Join(Separator, parts);
        }

        /// <summary>"saved just now", "saved 5 minutes ago", "saved 2 hours ago", "saved yesterday", "saved 3 days ago", or the date.</summary>
        public static string Saved(DateTime savedUtc, DateTime nowUtc)
        {
            var since = nowUtc - savedUtc;
            // A save stamped ahead of this clock (a changed clock, another machine) is not "in the future".
            if (since < TimeSpan.FromMinutes(1)) return "saved just now";
            if (since < TimeSpan.FromHours(1)) return Count((int)since.TotalMinutes, "minute");
            if (since < TimeSpan.FromDays(1)) return Count((int)since.TotalHours, "hour");
            if (since < TimeSpan.FromDays(2)) return "saved yesterday";
            if (since < TimeSpan.FromDays(30)) return Count((int)since.TotalDays, "day");
            return "saved on " + savedUtc.ToLocalTime().ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
        }

        private static string Count(int value, string unit) =>
            "saved " + value + " " + unit + (value == 1 ? "" : "s") + " ago";
    }
}
