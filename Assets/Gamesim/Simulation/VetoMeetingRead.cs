using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The veto meeting as the living room's screen tells it (PACK8-PASS-PLAN C1): who held the
    /// veto, the block as it stood before the meeting, whether the veto was used and on whom, who
    /// was named in their place, and the block that goes to the vote, with the lines the card says
    /// them in.
    ///
    /// <para>Read from the committed state and the block captured before the commit, never from
    /// the event the commit logged. That sentence is the engine's, and an NPC holder who saved
    /// themselves came out as "Emma Brown saves Emma Brown"; a card that showed it would put those
    /// words on the screen. Every fact here is public by the time the card plays, because the
    /// meeting is held in front of the house. The lines narrate what happened; none of them is a
    /// quote, and none of them guesses why.</para>
    ///
    /// <para>Pure and read-only: it neither changes the state nor draws from its generator, and it
    /// lives in the simulation so the Unity-free subset tests it.</para>
    /// </summary>
    public static class VetoMeetingRead
    {
        /// <summary>The meeting's outcome lines, as the mockup's VETO USED and VETO NOT USED say them.</summary>
        public const string UsedLine = "Veto used: a nominee is removed and a replacement is named.",
            NotUsedLine = "Veto not used: the nominations stay the same.";

        /// <summary>The headlines the card's pages carry.</summary>
        public const string UsedHeadline = "VETO USED", NotUsedHeadline = "VETO NOT USED",
            ReplacementHeadline = "REPLACEMENT", FinalHeadline = "FINAL NOMINEES";

        /// <summary>One veto meeting, read: the people in it by id and the card's lines.</summary>
        public sealed class Meeting
        {
            public int week;
            public string holderId, hohId;
            /// <summary>Who came off the block, or null when the veto was not used.</summary>
            public string savedId;
            /// <summary>Who went up in their place, or null when the veto was not used.</summary>
            public string replacementId;
            public bool used;
            /// <summary>Whether the holder sat on the block before the meeting.</summary>
            public bool holderOnTheBlock;
            /// <summary>The block before the meeting, in the order it was named.</summary>
            public List<string> blockBefore = new List<string>();
            /// <summary>The block that goes to the vote.</summary>
            public List<string> finalBlock = new List<string>();

            /// <summary>The card's opening line: who holds the veto.</summary>
            public string introLine;
            /// <summary>The question the card asks before the decision. Narration, not a quote.</summary>
            public string questionLine;
            public string decisionHeadline, decisionLine;
            /// <summary>Who the Head of Household names, or null when nobody is replaced.</summary>
            public string replacementLine;
            /// <summary><see cref="UsedLine"/> or <see cref="NotUsedLine"/>.</summary>
            public string outcomeLine;

            /// <summary>Whether the card has a page for the replacement: the veto was used and somebody went up.</summary>
            public bool HasReplacement => used && replacementId != null;

            /// <summary>Everyone the card shows a face for, each once.</summary>
            public IEnumerable<string> People =>
                new[] { holderId, hohId }.Concat(blockBefore).Concat(finalBlock).Where(id => !string.IsNullOrEmpty(id)).Distinct();

            /// <summary>Every line the card can show, for a test to read them all.</summary>
            public IEnumerable<string> Lines =>
                new[] { introLine, questionLine, decisionHeadline, decisionLine, replacementLine, outcomeLine }.Where(line => line != null);
        }

        /// <summary>
        /// The meeting the commit just decided. <paramref name="blockBefore"/> is the block as it
        /// stood before the commit; without it the committed block stands in, and the meeting reads
        /// as a veto that was not used, because nothing says otherwise.
        /// </summary>
        public static Meeting Read(EpisodeState s, IReadOnlyList<string> blockBefore)
        {
            var meeting = new Meeting { week = s.week };
            var after = (s.nominees ?? new List<string>()).Where(id => s.Find(id) != null).Distinct().ToList();
            var before = (blockBefore != null && blockBefore.Count > 0 ? blockBefore.AsEnumerable() : after)
                .Where(id => s.Find(id) != null).Distinct().ToList();
            meeting.blockBefore = before;
            meeting.finalBlock = after;
            meeting.holderId = s.Find(s.vetoHolderId) != null ? s.vetoHolderId : null;
            meeting.hohId = s.Find(s.hohId) != null ? s.hohId : null;
            meeting.savedId = before.FirstOrDefault(id => !after.Contains(id));
            meeting.replacementId = meeting.savedId != null ? after.FirstOrDefault(id => !before.Contains(id)) : null;
            meeting.used = meeting.savedId != null;
            meeting.holderOnTheBlock = meeting.holderId != null && before.Contains(meeting.holderId);

            string holder = meeting.holderId;
            bool youHold = holder != null && holder == s.playerId;
            // The holder at the start of a sentence: "You" for the player, their name otherwise.
            string Holder() => youHold ? "You" : Name(s, holder);

            if (holder == null)
            {
                meeting.introLine = "The veto meeting is in session.";
                meeting.questionLine = "Will the veto be used?";
            }
            else
            {
                meeting.introLine = youHold ? "You hold the Golden Power of Veto." : Holder() + " holds the Golden Power of Veto.";
                meeting.questionLine = youHold ? "Will you use the veto?" : "Will " + Holder() + " use the veto?";
            }

            if (meeting.used)
            {
                meeting.decisionHeadline = UsedHeadline;
                meeting.outcomeLine = UsedLine;
                string saved = meeting.savedId;
                // The holder who saves themselves is said with the reflexive their pronouns give, never
                // by their name twice; the player saved by somebody else is "you".
                string target = saved == holder
                    ? (youHold ? "yourself" : StoryPeople.Pronouns(s.Find(holder)).themselves)
                    : saved == s.playerId ? "you" : Name(s, saved);
                meeting.decisionLine = holder == null
                    ? Capitalised(target == "you" ? "you are" : target + " is") + " saved with the veto."
                    : (youHold ? "You use" : Holder() + " uses") + " the veto on " + target + ".";
                meeting.replacementLine = ReplacementLine(s, meeting.hohId, meeting.replacementId);
            }
            else
            {
                meeting.decisionHeadline = NotUsedHeadline;
                meeting.outcomeLine = NotUsedLine;
                meeting.decisionLine = holder == null ? "The veto is not used."
                    : youHold ? "You do not use the veto." : Holder() + " does not use the veto.";
            }
            return meeting;
        }

        /// <summary>Who the Head of Household names in the saved nominee's place, in the player's person where it is theirs.</summary>
        private static string ReplacementLine(EpisodeState s, string hoh, string replacement)
        {
            if (replacement == null) return null;
            string named = replacement == s.playerId ? "you" : Name(s, replacement);
            if (hoh == null) return replacement == s.playerId ? "You are the replacement nominee." : named + " is the replacement nominee.";
            return (hoh == s.playerId ? "You name " : Name(s, hoh) + " names ") + named + " as the replacement nominee.";
        }

        private static string Name(EpisodeState s, string id) => s.Find(id)?.name ?? "A houseguest";

        private static string Capitalised(string text) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
