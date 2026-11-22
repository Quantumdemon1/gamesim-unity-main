using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The notebook's season page in words: the heading over the finished weeks' recap buttons, and
    /// the line that says where the weeks the page leaves out are. That line used to point at
    /// "earlier weeks in the save", which the player cannot open; it names the buttons above it now.
    /// Plain C#, so the Unity-free suite reads the same words the page draws.
    /// </summary>
    public static class StorySoFarWords
    {
        /// <summary>The heading over the finished weeks, each with its recap button.</summary>
        public const string WeeksSoFarHeading = "WEEKS SO FAR";

        /// <summary>
        /// The line over the last <paramref name="shown"/> weeks, when the page leaves earlier weeks
        /// out. <paramref name="earlierRecaps"/> are the earlier weeks that have a recap button under
        /// <see cref="WeeksSoFarHeading"/>; <paramref name="caption"/> is that button's caption for a
        /// week. With none to name, the line only says what is shown.
        /// </summary>
        public static string EarlierWeeksLine(int shown, IEnumerable<int> earlierRecaps, Func<int, string> caption)
        {
            if (caption == null) throw new ArgumentNullException(nameof(caption));
            string line = "The last " + shown + " weeks are shown here.";
            var weeks = (earlierRecaps ?? Enumerable.Empty<int>()).Distinct().OrderBy(week => week).ToList();
            if (weeks.Count == 0) return line;
            if (weeks.Count == 1)
                return line + " Week " + weeks[0] + " is under " + WeeksSoFarHeading + " above: " + caption(weeks[0]) + ".";
            int first = weeks[0], last = weeks[weeks.Count - 1];
            return line + " Weeks " + first + " to " + last + " are under " + WeeksSoFarHeading + " above: "
                + caption(first) + " to " + caption(last) + ".";
        }
    }
}
