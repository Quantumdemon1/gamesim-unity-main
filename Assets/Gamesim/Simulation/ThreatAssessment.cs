using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// How dangerous one houseguest looks to another, and how far they trust them.
    ///
    /// <para>Ported from the reference build's threat assessment: a composite of five <b>capped</b>
    /// components. The capping is the design, not an implementation detail — a competition beast
    /// maxes out at forty and still needs social capital and a voting bloc behind them to read as an
    /// extreme threat. Without the caps a single dimension would decide every nomination.</para>
    ///
    /// <para><b>Two of the source's own numbers do not mean what they say, and are kept anyway.</b>
    /// Its potential component is capped at ten but its terms sum to at most seven, so that cap can
    /// never bind; and it labels the total 0–100 while its five caps add to 112. Both are faithful
    /// here. Tightening the potential cap to its real maximum, or clamping the total to 100, would
    /// change what this port produces relative to the build it is copying — and the caps that
    /// actually bind are the four that do the work.</para>
    ///
    /// <para>Both of these are pure functions of state that already exists, so nothing here is
    /// stored and no save format changes. They are read by decisions rather than recorded by
    /// them.</para>
    ///
    /// <para><b>Threat is evaluated by somebody, about somebody.</b> Four of the five components are
    /// the same whoever is asking; reputation is not, because it reads the asker's own trust and
    /// their own history with the target. Two houseguests can rate the same person differently, and
    /// that is the point.</para>
    /// </summary>
    public static class ThreatAssessment
    {
        /// <summary>Where trust sits before anybody has done anything.</summary>
        public const double NeutralTrust = 50;

        public readonly struct Breakdown
        {
            public readonly double Competition, Social, Alliance, Potential, Reputation;
            public double Total => Competition + Social + Alliance + Potential + Reputation;

            public Breakdown(double competition, double social, double alliance, double potential, double reputation)
            {
                Competition = competition; Social = social;
                Alliance = alliance; Potential = potential; Reputation = reputation;
            }
        }

        public static Breakdown Assess(EpisodeState state, string evaluatorId, string targetId)
        {
            var target = state?.Find(targetId);
            if (target == null) return new Breakdown(0, 0, 0, 0, 0);
            return new Breakdown(
                CompetitionThreat(target),
                SocialThreat(state, target),
                AllianceThreat(state, targetId),
                PotentialThreat(target),
                ReputationThreat(state, evaluatorId, targetId));
        }

        public static double Total(EpisodeState state, string evaluatorId, string targetId) =>
            Assess(state, evaluatorId, targetId).Total;

        /// <summary>
        /// The house ranked by how dangerous the evaluator finds them, most first.
        ///
        /// <para>Ties break on id so the ranking is stable. A ranking that reshuffled equal threats
        /// between calls would make an NPC's target wander for no reason a player could read.</para>
        /// </summary>
        public static List<string> RankedTargets(EpisodeState state, string evaluatorId) =>
            state.Active.Where(c => c.id != evaluatorId)
                .OrderByDescending(c => Total(state, evaluatorId, c.id))
                .ThenBy(c => c.id, StringComparer.Ordinal)
                .Select(c => c.id)
                .ToList();

        // ---------------------------------------------------------------- components

        /// <summary>Wins, capped at forty. Head of Household counts for more than veto: it is power.</summary>
        private static double CompetitionThreat(ContestantState target) =>
            Math.Min(40, target.hohWins * 8 + target.vetoWins * 6);

        /// <summary>
        /// How the rest of the house feels about them, capped at thirty.
        ///
        /// <para>The source's own mapping: −100 reads as 0, neutral reads as 15, +100 reads as 30.
        /// Being liked is a threat, which is the whole social game in one line.</para>
        /// </summary>
        private static double SocialThreat(EpisodeState state, ContestantState target)
        {
            var others = state.Active.Where(c => c.id != target.id).ToList();
            if (others.Count == 0) return 15;
            double average = others.Average(c => state.Score(c.id, target.id));
            return Math.Min(30, Math.Max(0, (average + 100) * 0.15));
        }

        /// <summary>Every bloc they sit in, four points a head, capped at twenty.</summary>
        private static double AllianceThreat(EpisodeState state, string targetId) =>
            Math.Min(20, state.alliances
                .Where(a => a.active && a.members.Contains(targetId))
                .Sum(a => a.members.Count * 4));

        /// <summary>
        /// What they might still do. Capped at ten by the source, though its terms reach only seven:
        /// three for competition, two for strategy, two for the combination.
        /// </summary>
        private static double PotentialThreat(ContestantState target)
        {
            double threat = target.stats.competition / 10 * 3 + target.stats.strategic / 10 * 2;
            // The jury combination: good with people and good at plotting is the profile that wins.
            if (target.stats.social >= 7 && target.stats.strategic >= 7) threat += 2;
            return Math.Min(10, threat);
        }

        /// <summary>
        /// Where social history becomes strategic danger, capped at fifteen and floored at zero.
        ///
        /// <para>Serial promise-breakers read as dangerous however much anybody likes them, and a
        /// hot rivalry makes a rival feel more threatening than their record alone suggests. This is
        /// the one component that differs by who is asking.</para>
        /// </summary>
        private static double ReputationThreat(EpisodeState state, string evaluatorId, string targetId)
        {
            double threat = Math.Min(8, state.promises
                .Count(p => p.status == PromiseStatus.Broken && (p.fromId == targetId || p.toId == targetId)) * 3);

            double trust = TrustScore(state, targetId, evaluatorId);
            if (trust < 35) threat += 5;
            else if (trust > 70) threat -= 3;

            var arc = state.relationshipArcs.FirstOrDefault(a => a.npcId == targetId);
            if (arc != null && arc.intensity >= 50)
            {
                if (arc.arcType == "rivalry") threat += Math.Min(7, Math.Floor(arc.intensity / 15));
                else if (arc.arcType == "friendship") threat -= Math.Min(5, Math.Floor(arc.intensity / 20));
            }
            return Math.Max(0, Math.Min(15, threat));
        }

        // ---------------------------------------------------------------- trust

        /// <summary>
        /// How far one houseguest trusts another, from 0 to 100, neutral at 50.
        ///
        /// <para><b>The range and its consumers are the source's; this curve is authored.</b> The
        /// reference document specifies that trust runs 0–100, defaults to 50, and feeds alliance
        /// desire and reputation threat at stated thresholds — but it never quotes the function that
        /// produces it. Rather than invent a parallel tracker, this reads the ledger the house
        /// already keeps: what somebody has actually done to you, with betrayals still counting in
        /// full and ordinary friction having faded.</para>
        ///
        /// <para>Scaled so the source's own thresholds are reachable by its own numbers: an
        /// alliance betrayal is −50 on its interaction table, which lands trust at 33 — below the 35
        /// it treats as dangerous. At a flatter scale a betrayal would stop exactly on the line, and
        /// the band is strictly below it, so the heaviest act in the game would have triggered
        /// nothing.</para>
        /// </summary>
        public static double TrustScore(EpisodeState state, string ofId, string byId)
        {
            if (state == null || ofId == byId) return NeutralTrust;
            double ledger = RelationshipLedger.WeightedImpact(state, byId, ofId);
            return Math.Max(0, Math.Min(100, NeutralTrust + ledger * TrustPerImpactPoint));
        }

        /// <summary>
        /// What one point of ledger impact is worth in trust.
        ///
        /// <para>One third, so the source's heaviest single entry clears its own threshold rather
        /// than stopping on it. See the note on <see cref="TrustScore"/>.</para>
        /// </summary>
        public const double TrustPerImpactPoint = 1.0 / 3.0;
    }
}
