using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The levers (STRATEGY-LOOP-PLAN.md §3): what the player can do to move a vote, and the rule
    /// boundary they run under. A vote deal with a voter enters their ballot as an obligation, sized
    /// so a deal and a standing flip a torn voter and never a firm one; a plea from the block enters
    /// it as the voter's answer; the reveal judges every vote deal; a call in an alliance points the
    /// bloc's pressure where the player says; bought time is the week's, not the season's; a lie
    /// sounds like one to the person told; and every lever says what it moved, in the read's terms.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>An obligation's size before their view of the player scales it, and the view at which it is whole.</summary>
        public const double ObligationBase = 8, ObligationFullView = 50;
        /// <summary>A Loyal voter's word is worth half as much again. A Sneaky voter's is worth nothing.</summary>
        public const double LoyalObligation = 1.5;
        /// <summary>What a plea's influence is worth in the ballot: a receptive hearing (40-60) is +8 to +12, a hostile one −6 to −10.</summary>
        public const double PleaWeight = 0.2;

        /// <summary>
        /// Switches the levers on for a season from <paramref name="fromWeek"/>. The director calls
        /// this for every season it starts; the save migration and the importer do the same from the
        /// week after a save's, so the week it was in plays under the rules it was played under.
        /// </summary>
        public static void EnableLevers(EpisodeState s, int fromWeek = 1)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            s.leverRulesStartWeek = Math.Max(1, Math.Min(fromWeek, s.week + 1));
        }

        /// <summary>Whether the levers run this week.</summary>
        public static bool LeverRulesOn(EpisodeState s) => s != null && s.leverRulesStartWeek >= 1 && s.week >= s.leverRulesStartWeek;

        /// <summary>Every lever term of a voter's ballot, obligations and pleas: empty before the levers, and on an empty store.</summary>
        public static List<WebVoteObligation> LeverTerms(EpisodeState s, string voterId)
        {
            var terms = Obligations(s, voterId);
            terms.AddRange(Pleas(s, voterId));
            return terms;
        }

        /// <summary>
        /// What a voter owes the player on this vote: every active vote deal between them that names
        /// somebody on the block, as a term on that nominee. Base 8, scaled by their view of the player
        /// (nothing at a view of zero or less, whole at fifty), Loyal x1.5, Sneaky x0: a friend keeps
        /// their word, a stranger does not, and a Sneaky voter's word was never worth anything. Sized
        /// so a deal and a standing flip a torn voter and never a firm one; relationship and alliance
        /// still dominate, which is the point. It sits on top of the web's own deal term, which the
        /// deal's target now reaches (3.5 after weighting).
        /// </summary>
        public static List<WebVoteObligation> Obligations(EpisodeState s, string voterId)
        {
            var terms = new List<WebVoteObligation>();
            if (!LeverRulesOn(s) || string.IsNullOrEmpty(voterId) || voterId == s.playerId) return terms;
            var voter = s.Find(voterId);
            if (voter == null) return terms;
            double scale = Math.Max(0, Math.Min(1, s.Score(voterId, s.playerId) / ObligationFullView));
            double word = voter.traits.Contains("Sneaky") ? 0 : voter.traits.Contains("Loyal") ? LoyalObligation : 1;
            foreach (var deal in s.deals.Where(d => d.status == DealStatus.Active
                         && (d.type == DealKind.VoteSave || d.type == DealKind.VoteEvict)
                         && d.targetId != null && s.nominees.Contains(d.targetId)
                         && ((d.proposerId == s.playerId && d.recipientId == voterId) || (d.proposerId == voterId && d.recipientId == s.playerId))))
            {
                var term = terms.FirstOrDefault(t => t.nomineeId == deal.targetId);
                if (term == null) terms.Add(term = new WebVoteObligation { nomineeId = deal.targetId, code = "obligation" });
                term.value += ObligationBase * scale * word * (deal.type == DealKind.VoteSave ? 1 : -1);
                term.evidenceIds.Add(deal.id);
            }
            return terms;
        }

        /// <summary>
        /// A plea heard this week: the voter's answer to "keep me", as a term on the player's own
        /// nomination. A receptive hearing is worth +8 to +12, an open one +3, a skeptical one −1, a
        /// hostile one −6 to −10: a plea can backfire, which is what makes it a play.
        /// </summary>
        public static List<WebVoteObligation> Pleas(EpisodeState s, string voterId)
        {
            var terms = new List<WebVoteObligation>();
            if (!LeverRulesOn(s) || string.IsNullOrEmpty(voterId) || voterId == s.playerId || !s.nominees.Contains(s.playerId)) return terms;
            foreach (var plea in s.lobbies.Where(l => l.week == s.week && l.ask == LobbyAsk.Vote && l.deciderId == voterId && l.subjectId == s.playerId))
            {
                var term = terms.FirstOrDefault(t => t.nomineeId == s.playerId);
                if (term == null) terms.Add(term = new WebVoteObligation { nomineeId = s.playerId, code = "plea" });
                term.value += plea.influence * PleaWeight;
                term.evidenceIds.Add("lobby:" + plea.week + ":" + plea.deciderId);
            }
            return terms;
        }

        /// <summary>The call the player made in an alliance this week, where the levers run; null otherwise.</summary>
        public static BlocCallRow CallThisWeek(EpisodeState s, string allianceId) =>
            LeverRulesOn(s) ? s.ledger.calls.LastOrDefault(k => k.week == s.week && k.allianceId == allianceId) : null;

        /// <summary>
        /// Calling the vote in an alliance (STRATEGY-LOOP-PLAN.md §3): through an ally, the player
        /// names the bloc's target, and every member who votes decides now, by the round's own
        /// compliance rule with the player as the caller, whether to follow. Those who follow vote as
        /// one at the reveal, where the bloc's pressure lands where the player pointed it; those who
        /// do not are named, so the player has just learned who in their alliance is theirs. One call
        /// per alliance per week; it costs a conversation, like the meeting it happens in.
        /// </summary>
        private static void CallTheVote(EpisodeState s, ContestantState ally, EpisodeCommand c)
        {
            Require(LeverRulesOn(s), "The house is not taking calls this season.");
            Require(s.phase == EpisodePhase.Campaign && VoteRead.Available(s), "Call the vote during the campaign.");
            var alliance = s.alliances.FirstOrDefault(a => a.active && a.id == (c.text ?? "") && a.members.Contains(s.playerId) && a.members.Contains(ally.id))
                ?? s.alliances.FirstOrDefault(a => a.active && a.members.Contains(s.playerId) && a.members.Contains(ally.id));
            Require(alliance != null, "Call the vote through somebody in your alliance.");
            Require(s.nominees.Contains(c.secondTargetId ?? "") && c.secondTargetId != s.playerId, "Name somebody on the block.");
            Require(!s.ledger.calls.Any(k => k.week == s.week && k.allianceId == alliance.id), "You have already called this week's vote in " + alliance.name + ".");
            var members = alliance.members.Where(id => id != s.playerId).Select(id => s.Find(id))
                .Where(m => m != null && m.status == ContestantStatus.Active && !s.nominees.Contains(m.id) && m.id != s.hohId).ToList();
            Require(members.Count > 0, "Nobody in " + alliance.name + " votes this week.");

            var snapshot = WebVotingBlocs.FromNative(s);
            var pact = snapshot.alliances.First(a => a.id == alliance.id);
            var trust = WebVotingBlocs.ProxyTrust(snapshot);
            var row = new BlocCallRow { week = s.week, allianceId = alliance.id, callerId = s.playerId, targetId = c.secondTargetId };
            foreach (var member in members)
            {
                double loyalty = WebVotingBlocs.Loyalty(snapshot, pact, member.id, s.playerId, c.secondTargetId, trust);
                (WebVotingBlocs.Complies(loyalty, member.traits, () => Roll(s)) ? row.followed : row.defected).Add(member.id);
            }
            SeasonLedger.Append(s.ledger, s.ledger.calls, row);
            string with = row.followed.Count == 0 ? "Nobody is with you"
                : Names(s, row.followed) + (row.followed.Count == 1 ? " is" : " are") + " with you";
            string against = row.defected.Count == 0 ? "." : "; " + Names(s, row.defected) + (row.defected.Count == 1 ? " isn't." : " aren't.");
            Log(s, "lever", "You called it in " + alliance.name + ": evict " + Name(s, c.secondTargetId) + ". " + with + against,
                new[] { s.playerId }.Concat(alliance.members.Where(id => id != s.playerId)).ToArray());
        }

        /// <summary>"Riley", "Riley and Sam", "Riley, Sam and Alex".</summary>
        private static string Names(EpisodeState s, IList<string> ids)
        {
            var names = ids.Select(id => Name(s, id)).ToList();
            if (names.Count <= 1) return names.FirstOrDefault() ?? "";
            return string.Join(", ", names.Take(names.Count - 1)) + " and " + names.Last();
        }

        /// <summary>A seeded Fisher-Yates, for the audience a call-out reaches under the levers: whoever happens to be there, not the first by id.</summary>
        public static void Shuffle<T>(IList<T> list, Func<double> roll)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Math.Min(i, (int)(roll() * (i + 1)));
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>The nominee the player wants out under a vote deal: the one it names for a vote to evict, the other one for a vote to keep.</summary>
        private static string WantsOut(EpisodeState s, string type, string aboutId) =>
            type == DealKind.VoteEvict ? aboutId : s.nominees.FirstOrDefault(id => id != aboutId);

        /// <summary>A voter's read as a lever finds it, or null where there is no vote to read or they do not vote.</summary>
        private static VoteRead.VoterRead LeverRead(EpisodeState s, string voterId) =>
            VoteRead.Available(s) && Voters(s).Any(v => v.id == voterId && !v.isPlayer) ? VoteRead.ReadVoter(s, voterId, ProjectBallot(s, voterId)) : null;

        /// <summary>
        /// A lever says what it moved, in the read's terms: "Riley: torn → leaning evict Jo (+9,
        /// your deal)". The number is how far the voter's known lean moved toward evicting
        /// <paramref name="wantsOutId"/>. Where nothing of the voter is known yet the read cannot
        /// move, so the line says what the lever is worth to them instead.
        /// </summary>
        private static void LeverLine(EpisodeState s, string voterId, VoteRead.VoterRead before, string wantsOutId, string why)
        {
            if (before == null || wantsOutId == null || !VoteRead.Available(s)) return;
            var after = VoteRead.ReadVoter(s, voterId, ProjectBallot(s, voterId));
            string line;
            if (after.confidence == VoteRead.Unknown)
            {
                var terms = LeverTerms(s, voterId);
                double worth = terms.Where(t => t.nomineeId == wantsOutId).Sum(t => -t.value) + terms.Where(t => t.nomineeId != wantsOutId).Sum(t => t.value);
                line = Name(s, voterId) + ": no read yet; " + why + " is worth " + Signed(worth) + " toward evicting " + Name(s, wantsOutId) + ".";
            }
            else
            {
                double moved = Toward(after, wantsOutId) - Toward(before, wantsOutId);
                line = Name(s, voterId) + ": " + VoteRead.Describe(s, before) + " → " + VoteRead.Describe(s, after) + " (" + Signed(moved) + ", " + why + ")";
            }
            Log(s, "lever", line, s.playerId, voterId);
        }

        private static string Signed(double value) => (value >= 0 ? "+" : "") + value.ToString("0");

        private static double Toward(VoteRead.VoterRead read, string wantsOutId) =>
            read.leaningId == null ? 0 : read.leaningId == wantsOutId ? read.knownMargin : -read.knownMargin;
    }
}
