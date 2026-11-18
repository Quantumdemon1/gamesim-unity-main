using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The creator's steps and their words (menu slice 2, changes 1 and 4), in plain C# so the
    /// Unity-free suite runs them: which steps each mode has, numbered from 1; what is missing on a
    /// step and where it is put right; the done, needs-attention and not-visited marks; and the one
    /// name each thing the creator shows has. Nothing here is saved: the marks come from the draft and
    /// from the steps the creator saw this visit.
    /// </summary>
    public static class CreatorSteps
    {
        public const string Appearance = "Appearance";
        public const string Identity = "Identity";
        public const string Personality = "Personality";
        public const string Review = "Review";
        public const string QuickSetup = "Quick setup";

        /// <summary>The library: a header button and a panel over the step, never a step itself.</summary>
        public const string Library = "My Houseguests";

        /// <summary>Detailed's steps, in order: step 1 is the first.</summary>
        public static readonly string[] Detailed = { Appearance, Identity, Personality, Review };

        /// <summary>Quick setup's steps: the look and the name on one page, then Review.</summary>
        public static readonly string[] Quick = { QuickSetup, Review };

        public static string[] For(bool quick) => quick ? Quick : Detailed;

        /// <summary>A step's number as the row shows it, from 1; 0 for a page that is not a step.</summary>
        public static int Number(string step, bool quick) => Array.IndexOf(For(quick), step) + 1;

        // ------------------------------------------------------------------ marks

        public enum Mark { NotVisited, Done, NeedsAttention }

        public const string DoneWord = "Done";
        public const string AttentionWord = "Needs attention";
        public const string NotVisitedWord = "Not visited yet";

        /// <summary>The mark's readable words: it is never told by its colour alone.</summary>
        public static string Word(Mark mark) =>
            mark == Mark.Done ? DoneWord : mark == Mark.NeedsAttention ? AttentionWord : NotVisitedWord;

        /// <summary>
        /// Done: seen, and nothing on it fails. Needs attention: seen, or passed by going on to a
        /// later step, with something on it missing or invalid. Anything else is not visited yet.
        /// </summary>
        public static Mark MarkOf(bool visited, bool passed, bool fails)
        {
            if (fails) return visited || passed ? Mark.NeedsAttention : Mark.NotVisited;
            return visited ? Mark.Done : Mark.NotVisited;
        }

        /// <summary>Whether something on <paramref name="step"/> stops the draft. Review answers for the whole draft.</summary>
        public static bool Fails(CharacterDraft draft, string step, bool quick)
        {
            if (step == Review) return !draft.TryValidate(out _) || Problems(draft, quick).Count > 0;
            return Problems(draft, quick).Any(problem => problem.Step == step);
        }

        // ------------------------------------------------------------------ what is missing

        /// <summary>One thing the draft cannot start without: what, the step it is put right on, and the field to put the cursor in.</summary>
        public sealed class Problem
        {
            public string Words;
            public string Step;
            /// <summary>The field's object name ("Name field"), or null where the step has no one field for it.</summary>
            public string Field;

            public Problem(string words, string step, string field = null) { Words = words; Step = step; Field = field; }
        }

        public const string NameField = "Name field";
        public const string AgeField = "Age slider";

        /// <summary>
        /// Everything the draft is missing, in the order <see cref="CharacterDraft.TryValidate"/>
        /// checks it, so the first is the one its own error names. In Quick setup the look, the name and
        /// the age are on its one page; anything else is on the detailed step that holds it.
        /// </summary>
        public static List<Problem> Problems(CharacterDraft draft, bool quick)
        {
            var found = new List<Problem>();
            if (draft == null) return found;
            string look = quick ? QuickSetup : Appearance, who = quick ? QuickSetup : Identity;
            if ((draft.Appearance != null && !draft.Appearance.TryValidate(out _)) || Length(draft.SourceTemplateId) > 100)
                found.Add(new Problem("Look needs fixing", look));
            if (string.IsNullOrWhiteSpace(draft.Name)) found.Add(new Problem("Name needed", who, NameField));
            else if (draft.Name.Length > CharacterDraft.NameLimit) found.Add(new Problem("Shorter name needed", who, NameField));
            if (draft.Age < CharacterDraft.MinimumAge || draft.Age > CharacterDraft.MaximumAge)
                found.Add(new Problem("Age " + CharacterDraft.MinimumAge + " to " + CharacterDraft.MaximumAge + " needed", who, AgeField));
            if (Length(draft.Occupation) > CharacterDraft.ShortLimit) found.Add(new Problem("Shorter occupation needed", Identity, "Occupation field"));
            if (Length(draft.Hometown) > CharacterDraft.ShortLimit) found.Add(new Problem("Shorter hometown needed", Identity, "Hometown field"));
            if (Length(draft.Pronouns) > CharacterDraft.ShortLimit) found.Add(new Problem("Shorter pronouns needed", who));
            if (Length(draft.Archetype) > CharacterDraft.ShortLimit) found.Add(new Problem("Shorter archetype needed", Personality));
            if (Length(draft.Bio) > CharacterDraft.BioLimit) found.Add(new Problem("Shorter bio needed", Identity, "Bio field"));
            if (draft.Traits.Count > WebTraits.MaximumTraits || draft.Traits.Any(trait => !WebTraits.Known(trait)))
                found.Add(new Problem("Traits need fixing", Personality));
            if (draft.Spent < 0 || draft.Spent > CharacterDraft.SparePoints || draft.Stats == null
                || WebTraits.StatNames.Any(stat => double.IsNaN(WebTraits.Get(draft.Stats, stat)) || double.IsInfinity(WebTraits.Get(draft.Stats, stat))
                    || WebTraits.Get(draft.Stats, stat) < WebTraits.Minimum || WebTraits.Get(draft.Stats, stat) > WebTraits.Maximum))
                found.Add(new Problem("Stats need fixing", Personality));
            return found;
        }

        /// <summary>The first thing missing, the one the footer's link shows; null when the draft can start.</summary>
        public static Problem First(CharacterDraft draft, bool quick) => Problems(draft, quick).FirstOrDefault();

        /// <summary>The footer's warning, which is a link: "Name needed - go to Identity".</summary>
        public static string LinkCaption(Problem problem) => problem.Words + " - go to " + problem.Step;

        // ------------------------------------------------------------------ one name per thing

        /// <summary>Making a new houseguest, wherever it is offered.</summary>
        public const string CreateCaption = "Create a houseguest";

        /// <summary>Changing one with no name at hand.</summary>
        public const string EditHouseguest = "Edit houseguest";

        public static string EditCaption(string name) => string.IsNullOrWhiteSpace(name) ? EditHouseguest : "Edit " + name.Trim();

        /// <summary>The creator's title: making one, or changing the one named.</summary>
        public static string Title(string editing) =>
            editing == null ? CreateCaption.ToUpperInvariant() : EditCaption(editing).ToUpperInvariant();

        // ------------------------------------------------------------------ the replace card

        public const string ReplaceCaption = "Replace my draft";
        public const string KeepCaption = "Keep my draft";

        /// <summary>What replacing a draft with no name yet puts in its place.</summary>
        public const string NewHouseguest = "a new houseguest";

        public static string ReplaceQuestion(string name) =>
            "Replace your draft with " + (string.IsNullOrWhiteSpace(name) ? NewHouseguest : name.Trim()) + "?";

        public const string ReplaceWarning = "Changes you have not saved in My Houseguests will be lost.";

        /// <summary>
        /// The library's own messages in the screen's words: the store says "profile" for a saved
        /// houseguest, and the screen says houseguest (one name per thing).
        /// </summary>
        public static string LibraryWords(string message)
        {
            if (string.IsNullOrEmpty(message)) return message;
            string text = message
                .Replace("houseguest profile", "saved houseguest")
                .Replace("Houseguest profile", "Saved houseguest")
                .Replace("the profile size limit", "the size limit")
                .Replace("The profile file", "The file")
                .Replace("profile", "houseguest")
                .Replace("Profile", "Houseguest");
            return text;
        }

        private static int Length(string value) => value?.Length ?? 0;
    }
}
