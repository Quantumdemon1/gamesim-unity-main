using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Which of the notes card's lines a houseguest's profile repeats under "From your notes". The
    /// profile leaves out what its other blocks already say: the memories ("What you remember") and
    /// the promises between you ("Other records"). It used to leave out every note of their word, and
    /// with the promises went the alliance-call notes, which nothing else on the profile says: a bloc
    /// call of yours they followed or ignored, and a war room's plan you backed that they were with or
    /// not. Those are kept now, each once: an ignored call that a betrayal note of the same week
    /// already tells ("... ignored your call to evict Jordan despite ...") is not said twice.
    ///
    /// <para>Only a choice among <see cref="HouseguestNotes.For"/>'s own lines, in its order, so the
    /// profile can never say what the notes card does not. Plain C#: the Unity-free suite reads it.</para>
    /// </summary>
    public static class ProfileNotes
    {
        /// <summary>The briefs <see cref="HouseguestNotes.For"/> gives a promise between you and them.</summary>
        public const string TheirWordBrief = "Their word: ", YourWordBrief = "Your word: ";

        /// <summary>The words an ignored call is said with, on the call's note and on the betrayal it was.</summary>
        private static readonly string IgnoredCallWords = Allegiance.IgnoredCall("");

        /// <summary>The notes on someone a profile shows, newest first, as the notes card orders them.</summary>
        public static List<HouseguestNotes.Note> For(EpisodeState state, string id) => Shown(HouseguestNotes.For(state, id));

        /// <summary>The notes of a card a profile shows, in the card's order.</summary>
        public static List<HouseguestNotes.Note> Shown(IList<HouseguestNotes.Note> notes)
        {
            var shown = new List<HouseguestNotes.Note>();
            if (notes == null) return shown;
            foreach (var note in notes)
            {
                if (note == null || note.kind == HouseguestNotes.Kinds.Memory || IsPromise(note)) continue;
                if (note.kind == HouseguestNotes.Kinds.Word && ToldByABetrayal(note, notes)) continue;
                shown.Add(note);
            }
            return shown;
        }

        /// <summary>Whether a note is a promise between you and them, which a profile's records already say.</summary>
        public static bool IsPromise(HouseguestNotes.Note note) =>
            note != null && note.kind == HouseguestNotes.Kinds.Word && note.brief != null
            && (note.brief.StartsWith(TheirWordBrief, StringComparison.Ordinal) || note.brief.StartsWith(YourWordBrief, StringComparison.Ordinal));

        /// <summary>
        /// Whether an ignored call's note is already told by a betrayal note of the same week on the
        /// same card, which names the same call: the call's words, from "ignored your call to evict".
        /// </summary>
        private static bool ToldByABetrayal(HouseguestNotes.Note note, IList<HouseguestNotes.Note> notes)
        {
            if (string.IsNullOrEmpty(note.text)) return false;
            int at = note.text.IndexOf(IgnoredCallWords, StringComparison.Ordinal);
            if (at < 0) return false;
            string call = note.text.Substring(at);
            return notes.Any(other => other != null && other != note && other.kind == HouseguestNotes.Kinds.Pact && other.week == note.week
                && other.text != null && (other.text.Contains(call + " despite ") || other.text.EndsWith(call, StringComparison.Ordinal)));
        }
    }
}
