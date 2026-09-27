using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Story words, rendered at display time.
    ///
    /// <para>A beat saves its content id, its cast and a name-free fallback sentence - never the
    /// composed English with names in it. Rendering happens here, when a surface asks, so the
    /// words can be corrected or translated without a single replay changing and a save never
    /// carries a stale name (20 §2.11).</para>
    ///
    /// <para>Tokens: <c>{ROLE}</c> is the name of whoever plays the role; <c>{ROLE.they}</c>,
    /// <c>{ROLE.them}</c>, <c>{ROLE.their}</c> and <c>{ROLE.themselves}</c> are their pronouns, and a
    /// capital first letter after the dot capitalises the pronoun (<c>{ROLE.They}</c>). Authored
    /// text avoids pronoun subjects, so no verb ever has to agree with one.</para>
    /// </summary>
    public static class StoryText
    {
        /// <summary>The catalogue beat a story event was drawn from, or null for a legacy event or a lost template.</summary>
        public static BeatTemplate BeatOf(HouseEventState item)
        {
            if (item == null || !item.IsStory || string.IsNullOrEmpty(item.contentId)) return null;
            int split = item.contentId.IndexOf(':');
            if (split <= 0) return null;
            return StoryCatalog.Find(item.contentId.Substring(0, split))?.Beat(item.contentId.Substring(split + 1));
        }

        /// <summary>The arc a story event belongs to, or null.</summary>
        public static ArcTemplate ArcOf(HouseEventState item)
        {
            if (item == null || !item.IsStory || string.IsNullOrEmpty(item.contentId)) return null;
            int split = item.contentId.IndexOf(':');
            return split <= 0 ? null : StoryCatalog.Find(item.contentId.Substring(0, split));
        }

        /// <summary>A beat's title: the catalogue's, else what was saved.</summary>
        public static string Title(HouseEventState item) => BeatOf(item)?.title ?? item?.title;

        /// <summary>The eyebrow over a beat's card: the arc's category.</summary>
        public static string Eyebrow(HouseEventState item) => ArcOf(item)?.eyebrow;

        /// <summary>
        /// The moment a beat closes at, as a sentence ends it: "the nominations", "eviction night".
        /// A beat that closes with the conversation it came up in, or at no anchor, says so plainly.
        /// </summary>
        public static string ClosesAt(string anchor)
        {
            switch (anchor)
            {
                case StoryAnchors.HohCrowned: return "the next Head of Household";
                case StoryAnchors.NomsSet: return "the nominations";
                case StoryAnchors.VetoWon: return "the veto competition";
                case StoryAnchors.BlockSet: return "the veto meeting";
                case StoryAnchors.EvictionEve: return "the eve of the eviction";
                case StoryAnchors.EvictionNight: return "eviction night";
                case StoryAnchors.SocialClose: return "the end of the day";
                case StoryAnchors.Conversation: return "the end of this conversation";
                default: return "the house moves on";
            }
        }

        /// <summary>A beat's narrative with names in it; the saved fallback when the template is gone.</summary>
        public static string Narrative(EpisodeState state, HouseEventState item)
        {
            var beat = BeatOf(item);
            if (beat == null || string.IsNullOrEmpty(beat.text)) return item?.narrative;
            return Fill(state, Phrasing(beat, item.week), item.cast);
        }

        /// <summary>An option's description with names in it.</summary>
        public static string Description(EpisodeState state, HouseEventState item, HouseEventChoice choice)
        {
            var option = BeatOf(item)?.Option(choice?.optionId);
            if (option == null || string.IsNullOrEmpty(option.description)) return choice?.description;
            return Fill(state, option.description, item.cast);
        }

        /// <summary>The phrasing for a week: the text the first week, then each alternate in turn. Never keyed on a roll.</summary>
        public static string Phrasing(BeatTemplate beat, int week)
        {
            if (beat.alternates == null || beat.alternates.Length == 0) return beat.text;
            int index = (Math.Max(1, week) - 1) % (beat.alternates.Length + 1);
            return index == 0 ? beat.text : beat.alternates[index - 1];
        }

        /// <summary>Puts names and pronouns where the tokens are.</summary>
        public static string Fill(EpisodeState state, string text, IEnumerable<StoryRoleState> cast)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            var roles = (cast ?? Enumerable.Empty<StoryRoleState>())
                .GroupBy(r => r.role).ToDictionary(g => g.Key, g => g.First().contestantId, StringComparer.Ordinal);
            return Replace(text, (role, form) =>
            {
                if (!roles.TryGetValue(role, out var id)) return null;
                var who = state?.Find(id);
                if (who == null) return form == null ? "someone" : Pronoun(null, form);
                return form == null ? who.name : Pronoun(who, form);
            });
        }

        /// <summary>
        /// The same text with nobody named: what is saved as a beat's fallback, and what an older
        /// reader shows. Names become "a houseguest"; pronouns become they, them and their.
        /// </summary>
        public static string Neutral(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            return Replace(text, (role, form) => form == null ? "a houseguest" : Pronoun(null, form));
        }

        private static string Pronoun(ContestantState who, string form)
        {
            var p = StoryPeople.Pronouns(who);
            string value;
            switch (form.ToLowerInvariant())
            {
                case "they": value = p.they; break;
                case "them": value = p.them; break;
                case "their": value = p.their; break;
                case "themselves": value = p.themselves; break;
                default: value = p.them; break;
            }
            return char.IsUpper(form[0]) ? char.ToUpperInvariant(value[0]) + value.Substring(1) : value;
        }

        private static string Replace(string text, Func<string, string, string> resolve)
        {
            var output = new StringBuilder(text.Length + 32);
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('{', i);
                if (open < 0) { output.Append(text, i, text.Length - i); break; }
                int close = text.IndexOf('}', open + 1);
                if (close < 0) { output.Append(text, i, text.Length - i); break; }
                output.Append(text, i, open - i);
                string token = text.Substring(open + 1, close - open - 1);
                int dot = token.IndexOf('.');
                string role = dot < 0 ? token : token.Substring(0, dot);
                string form = dot < 0 ? null : token.Substring(dot + 1);
                string value = role.Length > 0 && role.All(ch => char.IsUpper(ch) || ch == '-' || char.IsDigit(ch))
                    ? resolve(role, string.IsNullOrEmpty(form) ? null : form) : null;
                output.Append(value ?? text.Substring(open, close - open + 1));
                i = close + 1;
            }
            return output.ToString();
        }

        /// <summary>Every role token a text uses, for the lint.</summary>
        public static IEnumerable<string> Tokens(string text)
        {
            if (string.IsNullOrEmpty(text)) yield break;
            int i = 0;
            while (true)
            {
                int open = text.IndexOf('{', i);
                if (open < 0) yield break;
                int close = text.IndexOf('}', open + 1);
                if (close < 0) yield break;
                string token = text.Substring(open + 1, close - open - 1);
                int dot = token.IndexOf('.');
                yield return dot < 0 ? token : token.Substring(0, dot);
                i = close + 1;
            }
        }

        // ---------------------------------------------------------------- the log

        /// <summary>
        /// What a log line says, rendered: the named sentence for a story outcome, whose cycle step
        /// records the line's sequence, and the line's own text for everything else. Every surface
        /// that reads event text goes through here, so a story line never shows its name-free
        /// fallback while its cycle is still in the save.
        /// </summary>
        public static string Log(EpisodeState state, EpisodeEvent entry)
        {
            if (entry == null) return null;
            if (entry.kind != StoryLog.Outcome || state == null) return entry.text;
            foreach (var cycle in state.storylines)
            {
                if (cycle.path == null) continue;
                foreach (var step in cycle.path)
                {
                    if (step.logSequence != entry.sequence) continue;
                    var arc = StoryCatalog.Find(cycle.templateId);
                    var option = arc?.Beat(step.beatId)?.Option(step.optionId);
                    string outcome = step.result == StoryResults.Backfire
                        ? option?.backfireOutcome ?? option?.outcome : option?.outcome;
                    if (string.IsNullOrEmpty(outcome)) return entry.text;
                    string title = arc.Beat(step.beatId)?.title;
                    return (string.IsNullOrEmpty(title) ? "" : title + ": ") + Fill(state, outcome, cycle.cast);
                }
            }
            return entry.text;
        }
    }

    /// <summary>The kinds of log line the story system writes.</summary>
    public static class StoryLog
    {
        /// <summary>A beat put to the player. Name-free: the card carries the named text.</summary>
        public const string Offer = "story";
        /// <summary>A beat answered or lapsed. Rendered with names by <see cref="StoryText.Log"/>.</summary>
        public const string Outcome = "story-outcome";
        /// <summary>Something the player hears about an NPC thread. Name-free unless the player is a knower.</summary>
        public const string Whisper = "story-whisper";
        /// <summary>Production: warnings, and the ladder's steps before the penalty and removal have their own.</summary>
        public const string Production = "story-production";

        // Fallout (plan §5.1): the moments the house gives a ceremony card, each its own kind so the
        // card, the recap and the recent-events card can tell them apart from an ordinary line.

        /// <summary>A fight that becomes the house's: written by a beat whose template names it.</summary>
        public const string Blowup = "blowup";
        /// <summary>Production's second strike: a Have-Not week and no Head of Household competition.</summary>
        public const string Penalty = "penalty";
        /// <summary>Production removes a houseguest.</summary>
        public const string Expulsion = "expulsion";
        /// <summary>The whole house called together: written by a beat whose template names it.</summary>
        public const string HouseMeeting = "house-meeting";

        public static readonly string[] Fallout = { Blowup, Penalty, Expulsion, HouseMeeting };
        public static bool IsFallout(string kind) => kind != null && System.Array.IndexOf(Fallout, kind) >= 0;
    }
}
