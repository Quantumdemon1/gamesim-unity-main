using System;
using System.Collections.Generic;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The notebook's groups, in words and order (menu slice 3, the review's change 6 and its rail):
    /// the tab strip "Notes | Deals &amp; promises | Alliances" that the three pages of one group share,
    /// and the left rail's Notebook group with Save and Settings at its foot.
    ///
    /// <para>Plain C#, so the Unity-free suite holds the words and the order the HUD draws. A page is
    /// known by its notebook section's name (<c>EpisodeDirector.NotebookSection</c>); the EditMode
    /// tests hold these names to the director's.</para>
    /// </summary>
    public static class NotebookStrip
    {
        /// <summary>The notebook's sections the strip and the rail name, as the director spells them.</summary>
        public const string NotesSection = "Section · notes", DealsSection = "Section · word", AlliancesSection = "Section · alliances";
        public const string PeopleSection = "Section · people", NetworkSection = "Section · network", RoomsSection = "Section · rooms";
        public const string VotesSection = "Section · votes", StorySection = "Section · story";

        /// <summary>The three tabs' captions: each the page's own name.</summary>
        public const string NotesTab = "Notes", DealsTab = "Deals & promises", AlliancesTab = "Alliances";

        /// <summary>The strip's object, under the reading frame's head; the lit tab carries <see cref="LitMarkName"/>.</summary>
        public const string StripName = "Notebook tabs";
        /// <summary>The bar under the lit tab: the shape that says which tab is lit, beside its colour and its weight.</summary>
        public const string LitMarkName = "Lit tab";

        /// <summary>A tab: its caption and the section it shows.</summary>
        public readonly struct Tab
        {
            public readonly string Caption, Section;
            public Tab(string caption, string section) { Caption = caption; Section = section; }
        }

        /// <summary>The strip, left to right.</summary>
        public static readonly IReadOnlyList<Tab> Tabs = new[]
        {
            new Tab(NotesTab, NotesSection),
            new Tab(DealsTab, DealsSection),
            new Tab(AlliancesTab, AlliancesSection),
        };

        /// <summary>Whether a section is one of the strip's pages (it draws the strip under its head).</summary>
        public static bool Carries(string section) => Lit(section) >= 0;

        /// <summary>Which tab a section lights, or -1 for a page that has no strip.</summary>
        public static int Lit(string section)
        {
            for (int i = 0; i < Tabs.Count; i++)
                if (string.Equals(Tabs[i].Section, section, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>
        /// Where a step along the strip lands from tab <paramref name="from"/>: one to the left for a
        /// negative <paramref name="direction"/>, one to the right otherwise, and the same tab at either
        /// end (the strip does not wrap: Left on the first tab stays there).
        /// </summary>
        public static int Step(int from, int direction, int count)
        {
            if (count <= 0) return -1;
            int to = from + (direction < 0 ? -1 : 1);
            return to < 0 || to >= count ? Math.Max(0, Math.Min(count - 1, from)) : to;
        }

        /// <summary>The rail's heading over the notebook's rows. The words as given; the style draws them in capitals.</summary>
        public const string RailHeading = "Notebook";

        /// <summary>The rail row that opens the notes: its caption since before the strip (a contract: the [J] is its key).</summary>
        public const string NotebookRow = "Notebook [J]";

        /// <summary>
        /// The rail's rows under its Notebook heading, top to bottom: the notes (with Deals &amp;
        /// promises beside them, the strip's other way in), then the people, how they stand to you,
        /// where they are, the vote and the season. Each row's caption and the section it opens.
        /// </summary>
        public static readonly IReadOnlyList<Tab> RailRows = new[]
        {
            new Tab(NotebookRow, NotesSection),
            new Tab(DealsTab, DealsSection),
            new Tab("Houseguests", PeopleSection),
            new Tab("Relationships", NetworkSection),
            new Tab("Who is where", RoomsSection),
            new Tab("The vote", VotesSection),
            new Tab("The story so far", StorySection),
        };

        /// <summary>The rows at the rail's foot, apart from the Notebook group.</summary>
        public static readonly IReadOnlyList<string> FootRows = new[] { "Save [F5]", "Settings" };
    }
}
