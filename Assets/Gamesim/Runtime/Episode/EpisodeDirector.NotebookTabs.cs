using System;
using System.Collections.Generic;
using Gamesim.Presentation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The notebook's one head and its tab strip (menu slice 3, the review's changes 5 and 6): every
    /// page opens in the reading frame under the same head, and the three pages of one group - your
    /// notes, Deals &amp; promises and alliances - share a strip under it. The strip's tabs are siblings:
    /// none of the three has a way back to another, and however a page of the group is reached (the
    /// rail, the strip, the relationship web's link) it is the same page with the same strip.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The season's page in the notebook's head (the rail's "The story so far").</summary>
        public const string StoryPageTitle = "The story so far";
        public const string StoryPageSubtitle = "The weeks gone by, your promises and declarations, your diary and what you remember";

        /// <summary>The notebook's footer notes on the two pages that had the old head (the web and the season).</summary>
        public const string WebFooterNote = "Drawn from your own reading of each houseguest, and from the pacts word has reached you about. Nothing your character has not seen or been told.";
        public const string StoryFooterNote = "The season as your character lived it, from the season's own record. Nothing you have not learned.";

        /// <summary>
        /// What a notebook page says of itself in the frame's head. The relationship web keeps the
        /// slim head: its own title stands over the graph, which takes the rest of the frame
        /// (mockup-07), so a second title above it would be the same words twice.
        /// </summary>
        private (string Title, string Subtitle) ReadingHead(string section)
        {
            if (section == NotebookSection.Story) return (StoryPageTitle, StoryPageSubtitle);
            if (section == NotebookSection.Network) return (null, null);
            return NotebookPageHead(section);
        }

        /// <summary>The strip under the head, on the three pages of its group; nothing on any other page.</summary>
        private void NotebookTabs(string section)
        {
            int lit = NotebookStrip.Lit(section);
            if (lit < 0) return;
            var tabs = new List<(string, bool, Action)>();
            for (int i = 0; i < NotebookStrip.Tabs.Count; i++)
            {
                string target = NotebookStrip.Tabs[i].Section;
                tabs.Add((NotebookStrip.Tabs[i].Caption, i == lit, () => ShowNotebookSection(target)));
            }
            hud.NotebookTabStrip(tabs);
        }
    }
}
