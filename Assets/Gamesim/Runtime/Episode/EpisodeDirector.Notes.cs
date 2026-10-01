using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The notebook's own page (playtest, 2026-09-28): "Notebook [J]" opened the relationships,
    /// which the rail already had a row for, so the notebook was a second door to the same room.
    /// Now it opens on your notes: what your character has on each houseguest, gathered from the
    /// season's own records (<see cref="HouseguestNotes"/>) - their word, what they told you about
    /// the vote and whether it held, what you read of them, what they put to you - which was spread
    /// across the log, the vote page's one tab and each profile, and readable nowhere at a glance.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>Which notes the page lists. View state, like the other pages' filters.</summary>
        private enum NotesFilter { Everyone, InTheHouse, TheirWord, TheVote, YourReads }
        private NotesFilter notesFilter;

        /// <summary>How many lines a houseguest's card shows before it counts the rest.</summary>
        private const int NotesShown = 6;

        private void RenderNotebookNotes(EpisodeState state)
        {
            var tabs = new List<(string, bool, Action)>
            {
                ("Everyone", notesFilter == NotesFilter.Everyone, () => { notesFilter = NotesFilter.Everyone; Render(); }),
                ("In the house", notesFilter == NotesFilter.InTheHouse, () => { notesFilter = NotesFilter.InTheHouse; Render(); }),
                ("Their word", notesFilter == NotesFilter.TheirWord, () => { notesFilter = NotesFilter.TheirWord; Render(); }),
                // Not "The vote": that is the rail row's caption, and a control is found by its caption.
                ("Their vote", notesFilter == NotesFilter.TheVote, () => { notesFilter = NotesFilter.TheVote; Render(); }),
                ("Your reads", notesFilter == NotesFilter.YourReads, () => { notesFilter = NotesFilter.YourReads; Render(); }),
            };
            hud.FilterRow("Notes filters", tabs);
            // A page of its own, not a filter: a door in the head to every commitment you are a party to (EpisodeDirector.YourWord.cs).
            hud.PageDoor(YourWordCaption, () => ShowNotebookSection(NotebookSection.Word));
            // The mark exists in every state: it is what the rail scrolls to.
            hud.Mark(NotebookSection.Notes);

            int shown = 0;
            foreach (var actor in state.contestants.Where(c => !c.isPlayer))
            {
                if (notesFilter == NotesFilter.InTheHouse && actor.status != ContestantStatus.Active) continue;
                var notes = HouseguestNotes.For(state, actor.id).Where(Keeps).ToList();
                // A kind's filter lists only those with something of that kind; the two roster
                // filters list everybody, with an empty card saying so.
                if (notes.Count == 0 && notesFilter != NotesFilter.Everyone && notesFilter != NotesFilter.InTheHouse) continue;
                shown++;
                var kind = RelationshipWeb.KindOf(state, actor.id);
                var lines = notes.Take(NotesShown).Select(n => "Week " + n.week + " · " + n.text).ToList();
                if (lines.Count == 0) lines.Add("Nothing on them yet. Ask them straight, read them, listen in, and hear what they put to you.");
                hud.NotesCard(actor, StatusWord(actor.status), RelationshipWeb.StandingWord(kind),
                    kind == RelationshipWeb.Kind.Neutral ? UiTheme.Muted : RelationshipWeb.StandingColour(kind),
                    lines, Math.Max(0, notes.Count - NotesShown));
            }
            if (shown == 0)
                hud.EmptyState("No notes", PackArt.KitEmptyPrivate, "Nothing of that kind on anyone yet.",
                    "Ask them straight, read them, listen in, and hear what they put to you: it lands here as it happens.");
            hud.NotebookFooter("What your character has on each houseguest, from the season's own record. Nothing you have not learned.",
                "House activities", OpenHouseActivities);
        }

        private bool Keeps(HouseguestNotes.Note note)
        {
            switch (notesFilter)
            {
                case NotesFilter.TheirWord: return HouseguestNotes.IsTheirWord(note);
                case NotesFilter.TheVote: return HouseguestNotes.IsTheVote(note);
                case NotesFilter.YourReads: return HouseguestNotes.IsYourRead(note);
                default: return true;
            }
        }
    }
}
