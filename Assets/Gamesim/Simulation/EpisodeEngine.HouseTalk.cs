using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The house talks back, in the rules (plan 32, Stack A, story rules 12 <see cref="StoryRules.Manners"/>; S1): the vote
    /// answer in a voice and a manner, chosen by <see cref="Candour"/> and <see cref="Manner"/>; the hidden records it leaves
    /// in the story's cooldown table (<c>said:</c>, <c>tell:</c>, <c>h2h:</c>); the read's word on a claim; and the reveal
    /// teaching a tell. Every entry point is reached only from a season at 12 or above, so a season at 11 or below plays
    /// exactly as before.
    ///
    /// <para>The records are cooldown keys, free text the engine writes (never an effect), so nothing new is saved
    /// (plan 32 §L): <c>said:{week}:{voter}:{stance}:{manner}</c> until next week; <c>tell:{voter}:{manner}</c> for the
    /// season; <c>h2h:{week}:{kind}:{runnerUpId}</c> for three weeks.</para>
    ///
    /// <para>And the line an answer took (<see cref="HouseAnswers.VoteLineIndex"/>): <c>line:{week}:{voter}:{manner}:{index}</c>,
    /// one a voter, until the week after next, so next week's answer can leave it out.</para>
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        public const string SaidPrefix = "said:", TellPrefix = "tell:";
        public const int SaidWeeks = 1, TellWeeks = 100, HeadToHeadWeeks = 3;
        /// <summary>The record of the line a vote answer took, kept through next week (<see cref="HouseAnswers.VoteLineIndex"/>).</summary>
        public const string LinePrefix = "line:";
        public const int LineWeeks = 2;
        /// <summary>The read's chance to doubt a claim this week: after a lie, after the truth (§G4).</summary>
        public const double ReadDoubtAfterLie = 0.6, ReadDoubtAfterTruth = 0.2;

        /// <summary>What a voter said this week, from its record: the stance and the manner; null where nothing was said.</summary>
        public sealed class SaidRecord
        {
            public int week;
            public string voterId, stance, manner;
        }

        /// <summary>The record of a voter's answer in a week, read from its <c>said:</c> key; null when there is none.</summary>
        public static SaidRecord Said(EpisodeState s, string voterId, int week)
        {
            string prefix = SaidPrefix + week + ":" + voterId + ":";
            var key = s?.story?.cooldowns?.FirstOrDefault(c => c.key.StartsWith(prefix, StringComparison.Ordinal))?.key;
            if (key == null) return null;
            var parts = key.Substring(prefix.Length).Split(':');
            if (parts.Length != 2 || !Candour.IsStance(parts[0]) || !Manner.IsKnown(parts[1])) return null;
            return new SaidRecord { week = week, voterId = voterId, stance = parts[0], manner = parts[1] };
        }

        /// <summary>The manners a voter has been caught lying in, from the reveal's <c>tell:</c> keys, in the table's order.</summary>
        public static List<string> TellsOf(EpisodeState s, string voterId)
        {
            string prefix = TellPrefix + voterId + ":";
            return (s?.story?.cooldowns ?? new List<StoryCooldownState>())
                .Where(c => c.key.StartsWith(prefix, StringComparison.Ordinal) && c.untilWeek > s.week)
                .Select(c => c.key.Substring(prefix.Length)).Where(Manner.IsTell).ToList();
        }

        /// <summary>
        /// "Where's your head at on the vote?" at 12 (§G1). The requirements and the week's ask are AskVote's. The main
        /// stream's roll is drawn exactly where rules 11 draws it - not for an ally, not on the deflect branch - and its value
        /// is ignored, so the season's stream stays aligned with 11's. The stance is <see cref="Candour"/>'s (a C6 ally is
        /// Honest), the manner <see cref="Manner"/>'s, and both write today's records: a Told claim for a stated name, a
        /// Deflected standing for a Neutral answer. The line is the voter's voice in that manner, logged as the answer.
        /// </summary>
        private static void AnswerTheVote(EpisodeState s, ContestantState target, double view, bool ally)
        {
            bool deflects = !ally && target.traits.Contains("Strategic") && !target.traits.Contains("Loyal") && view < 25;
            if (!ally && !deflects) Roll(s);
            var truth = ProjectBallot(s, target.id);
            // Free talk's rules 2 (T7): a Friend or closer answers honestly at least 0.8 of the time, on the same keyed coin.
            string stance = TalkFriendship.AtLeast(s, target.id, FriendStage.Friend)
                ? Candour.Choose(s, target.id, Candour.Vote, null, TalkFriendship.FriendHonesty)
                : Candour.Choose(s, target.id, Candour.Vote, null);
            string other = s.nominees.FirstOrDefault(id => id != truth.selectedNomineeId);
            if (stance == Candour.Dishonest && other == null) stance = Candour.Honest;
            string stated = stance == Candour.Honest ? truth.selectedNomineeId : stance == Candour.Dishonest ? other : null;
            if (stated != null)
                SeasonLedger.Append(s.ledger, s.ledger.claims, new ClaimRow { week = s.week, voterId = target.id, targetId = stated, source = ClaimSource.Told });
            else AddStanding(s, target.id, s.playerId, ClaimSource.Deflected, 0);
            string manner = Manner.Choose(s, target, Candour.Vote, stance, truth.margin < VoteRead.LeaningMargin, false);
            // The line: one no earlier answer in this voice and manner gave this week, nor this voter's last week, while the
            // slot has one. On the record before the said: key, the voter's last one replaced, so every read quotes it.
            int index = HouseAnswers.VoteLineIndex(s, s.week, target, manner);
            s.story.cooldowns.RemoveAll(c => HouseAnswers.ParseLine(c?.key)?.voterId == target.id);
            Cool(s, HouseAnswers.LineRecordKey(s.week, target.id, manner, index), s.week + LineWeeks);
            Cool(s, SaidPrefix + s.week + ":" + target.id + ":" + stance + ":" + manner, s.week + SaidWeeks);
            string line = HouseAnswers.Fill(s, HouseAnswers.VoteLine(Voice.Of(target, false), manner, index), stated);
            Log(s, "vote-read", "You asked " + target.name + " where their head is at. \"" + line + "\"", s.playerId, target.id);
        }

        /// <summary>
        /// The read's word on what a voter told the player this week (§G4), at 12, on a hit before the reveal: a keyed coin of
        /// the story's stream, never the main one, doubts a lie at 0.6 and the truth at 0.2. It never reads the ballot.
        /// </summary>
        private static void ReadTheClaim(EpisodeState s, ContestantState target)
        {
            if (s.evictionResolved) return;
            var said = Said(s, target.id, s.week);
            if (said == null || said.stance == Candour.Neutral) return;
            double doubt = said.stance == Candour.Dishonest ? ReadDoubtAfterLie : ReadDoubtAfterTruth;
            bool doubts = StoryRandom.Unit(s, "w" + s.week + ":read-claim:" + target.id) < doubt;
            Log(s, "vote-read", doubts ? HouseAnswers.ReadDoubt : HouseAnswers.ReadHolds, s.playerId, target.id);
        }

        /// <summary>
        /// The reveal teaches a tell (§G4), at 12: only when the voter's answer this week was a lie in a manner that is a tell.
        /// A changed mind - an honest answer judged a lie by the ballot - teaches nothing.
        /// </summary>
        private static void LearnTell(EpisodeState s, string voterId)
        {
            var said = Said(s, voterId, s.week);
            if (said == null || said.stance != Candour.Dishonest || !Manner.IsTell(said.manner)) return;
            Cool(s, TellPrefix + voterId + ":" + said.manner, s.week + TellWeeks);
        }

        /// <summary>
        /// The head-to-head record (§F), at 12, as a Head of Household is crowned or the veto is won: the runner-up of a win by
        /// the player, from the committed standings. Draws nothing.
        /// </summary>
        private static void RecordHeadToHead(EpisodeState s, string anchor)
        {
            if (!StoryAt(s, StoryRules.Manners) || s.competitionScores == null || s.competitionScores.Count < 2) return;
            var order = s.competitionScores.OrderByDescending(x => x.score).Select(x => x.contestantId).ToList();
            if (order[0] != s.playerId) return;
            string kind = anchor == StoryAnchors.HohCrowned ? "hoh" : "veto";
            Cool(s, Regard.HeadToHeadPrefix + s.week + ":" + kind + ":" + order[1], s.week + HeadToHeadWeeks);
        }
    }
}
