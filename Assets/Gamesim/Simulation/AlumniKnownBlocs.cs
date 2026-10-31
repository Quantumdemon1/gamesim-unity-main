using System;
using System.Collections.Generic;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Alumni rules 3, <see cref="AlumniRules.Reputation"/> (ALL-STAR-HISTORIES-PLAN §14.C, §14.D): the owner's direction of
    /// 2026-10-29, "players also have reputations and their connections are known to other players". Two readings of an
    /// edition's public, on-show record (§3.1), the same for every evaluator:
    /// <list type="bullet">
    /// <item><b>Career reputation</b> (<see cref="CareerFor"/>): the edition's winner 4, finalist 3, played 2, for a
    /// houseguest on an identity-intact, unique alumni card (<see cref="AlumniHistories.Seated"/>'s rule). A newcomer has
    /// none: their reputation is what they do in the house.</item>
    /// <item><b>Known pairs</b> (<see cref="LivePairs"/>): the seated shared alliances of past games (a standing above 0:
    /// the Hitmen, the Committee), while both houseguests are in and neither has put the other up at a public ceremony.
    /// Each reads as a bloc of two to an evaluator outside it (<see cref="RiskFor"/>): 8 for a final-two partnership, 4
    /// for another. Pairs are counted as pairs and never joined into a group: the chain through Cody is two facts, and
    /// nothing here says the three were one.</item>
    /// </list>
    ///
    /// <para>From alumni rules 3 they reach two decisions: the threat a Head of Household and their pact-mates read at the
    /// nominations (<see cref="ThreatAssessment.TotalWithPastGames"/>, through <see cref="EpisodeEngine.ThreatTerm"/>),
    /// and the vote's threat (<see cref="VoteTerms"/>, carried as <see cref="WebVoteOptions.alumniTerms"/>). Never
    /// <see cref="ThreatAssessment.Total"/>, so the house's talk - the rumour subject, the pitch target, a broken word's
    /// grudge - is the same at every value.</para>
    ///
    /// <para>From alumni rules 4, <see cref="AlumniRules.CounterBlocs"/> (§14.E), the houseguests outside a known pair
    /// answer it "for numbers", and two more readings follow: how much two of them want each other as partners
    /// (<see cref="CounterDesire"/>, inside <see cref="NpcAlliances.Desire"/>), and whom an outside pact hunts
    /// (<see cref="CounterTarget"/>, in <see cref="NpcAgendas.Of"/>). Both count the live pairs neither houseguest
    /// stands near - not in, and not beside a partner who is - so a pair's members and their partners never read
    /// them. The hunt never names a pact-mate of the hunter's, nor one of the listener's the hunter can see, and it stops
    /// while the listener still rates its target at <see cref="HuntStopLine"/>, one hunt's cost, so on its own it never
    /// takes a score below nought. Neither is a turn, a draw or a write: what the house does
    /// with them is the agendas' and the pact pass's own, under their own caps.</para>
    ///
    /// <para>Private things never end a known pair in the house's eyes: scores, receipts, deals (the final two included),
    /// ballots, a pact's end. A departure does, and so does one of the two putting the other up
    /// (<see cref="AlumniLoyalty.PublicSplitWeek"/>, this week's block included). Pure readers of the season and the
    /// edition it names (<see cref="AlumniHistories.EditionOf"/>, never <see cref="AlumniEditions.Latest"/>): nothing is
    /// stored, cached, drawn or written, and every member that reads a season is silent below
    /// <see cref="AlumniRules.Reputation"/>. <see cref="Risk"/> reads no season: it is a fact's own value at every rules
    /// value, and the game passes it only the facts <see cref="LivePairs"/> returns.</para>
    /// </summary>
    public static class KnownBlocs
    {
        /// <summary>
        /// What one counted known pair adds to two outsiders' desire for each other (§14.E.1): half the source's price of
        /// a shared threat (fifteen, <see cref="NpcAlliances.Desire"/>), because a known pair is a bloc nobody has
        /// quarrelled with yet.
        /// </summary>
        public const double CounterBlocDesire = 7.5;

        /// <summary>The most known pairs that count toward <see cref="CounterDesire"/> (§14.E.1).</summary>
        public const int CounterBlocPairsCap = 2;

        /// <summary>
        /// The bloc hunt's stop (§14.E.2): the listener's view of a pair's member must be at least one hunt's cost
        /// (<see cref="EpisodeEngine.HuntImpact"/>), so the route alone never takes it below nought, far above
        /// <see cref="NpcDeals.CommonThreat"/>'s dislike, <see cref="NpcAlliances.DislikeLine"/> and the confrontation line.
        /// </summary>
        public const double HuntStopLine = -EpisodeEngine.HuntImpact;

        /// <summary>
        /// A houseguest's career reputation (§14.C): the edition's <see cref="AlumniEdition.CareerReputation"/> for the
        /// alumnus they are, the player on an intact card included. 0 below <see cref="AlumniRules.Reputation"/>, for a
        /// newcomer, and for a card that is renamed or held by two houseguests.
        /// </summary>
        public static int CareerFor(EpisodeState s, string id)
        {
            if (id == null || !AlumniRules.At(s, AlumniRules.Reputation)) return 0;
            var edition = AlumniHistories.EditionOf(s);
            if (edition == null || s.contestants == null) return 0;
            var who = s.Find(id);
            string templateId = TemplateOf(edition, who);
            return templateId != null && ReferenceEquals(SeatedOn(s, edition, templateId), who) ? edition.CareerReputation(templateId) : 0;
        }

        /// <summary>
        /// The known pairs still standing (§14.D.2): the seated pairs (<see cref="AlumniHistories.SeatedPairs"/>) whose fact
        /// is a shared alliance (<see cref="AlumniPairFact.Standing"/> above 0), both of whose houseguests are active, and
        /// neither of whom has put the other up (<see cref="AlumniLoyalty.PublicSplitWeek"/>, this week's included), in
        /// fact-id order. Empty below <see cref="AlumniRules.Reputation"/>, without the season's edition, and when no
        /// shared alliance's card is seated.
        /// </summary>
        public static List<AlumniHistories.SeatedPair> LivePairs(EpisodeState s)
        {
            var live = new List<AlumniHistories.SeatedPair>();
            if (!AlumniRules.At(s, AlumniRules.Reputation)) return live;
            var edition = AlumniHistories.EditionOf(s);
            if (edition == null || s.contestants == null || !SeatsASharedAlliance(s, edition)) return live;
            foreach (var fact in edition.Pairs)
            {
                if (fact.Standing <= 0) continue;
                var first = SeatedOn(s, edition, fact.FirstId);
                var second = first == null ? null : SeatedOn(s, edition, fact.SecondId);
                if (second == null || first.status != ContestantStatus.Active || second.status != ContestantStatus.Active) continue;
                if (AlumniLoyalty.PublicSplitWeek(s, first.id, second.id, includeThisWeek: true) != 0) continue;
                live.Add(new AlumniHistories.SeatedPair(fact, first.id, second.id));
            }
            return live;
        }

        /// <summary>
        /// A known pair's risk (§3.6, §14.D.2): <see cref="AlumniEditions.FinalTwoPairRisk"/> for a shared alliance that
        /// reached a final two, otherwise <see cref="AlumniEditions.AlliancePairRisk"/>; 0 for a fact that is no shared
        /// alliance, which never makes a known pair.
        /// </summary>
        public static int Risk(AlumniPairFact fact) =>
            fact == null || fact.Standing <= 0 ? 0
            : fact.Standing >= AlumniEditions.FinalTwoAllianceStanding ? AlumniEditions.FinalTwoPairRisk : AlumniEditions.AlliancePairRisk;

        /// <summary>
        /// The risk of the known pairs a target sits in, as one evaluator reads it (§14.D.2): the sum of <see cref="Risk"/>
        /// over the live pairs that hold the target and not the evaluator. A pair is passed over when an active pact the
        /// evaluator can see (<see cref="Knowledge.AllianceVisibleTo"/>) holds both of its houseguests: that pact is
        /// already a bloc in the evaluator's reading, so a re-formed partnership is never counted twice.
        /// </summary>
        public static int RiskFor(EpisodeState s, string evaluatorId, string targetId)
        {
            if (targetId == null) return 0;
            int risk = 0;
            foreach (var pair in LivePairs(s))
            {
                bool holdsTarget = pair.FirstId == targetId || pair.SecondId == targetId;
                bool holdsEvaluator = pair.FirstId == evaluatorId || pair.SecondId == evaluatorId;
                if (!holdsTarget || holdsEvaluator || AVisiblePactHolds(s, pair, evaluatorId)) continue;
                risk += Risk(pair.Fact);
            }
            return risk;
        }

        /// <summary>
        /// The vote's alumni terms for one voter (§14.D.4): for each nominee, the pair risk the voter reads
        /// (<see cref="RiskFor"/>) and the nominee's career (<see cref="CareerFor"/>), holding the nominees with either
        /// nonzero only, in block order. Empty below <see cref="AlumniRules.Reputation"/>. Derived for one ballot, never saved.
        /// </summary>
        public static List<WebVoteAlumniTerm> VoteTerms(EpisodeState s, string voterId)
        {
            var terms = new List<WebVoteAlumniTerm>();
            if (!AlumniRules.At(s, AlumniRules.Reputation) || s.nominees == null) return terms;
            foreach (string nomineeId in s.nominees)
            {
                int pairRisk = RiskFor(s, voterId, nomineeId), career = CareerFor(s, nomineeId);
                if (pairRisk != 0 || career != 0) terms.Add(new WebVoteAlumniTerm { nomineeId = nomineeId, pairRisk = pairRisk, career = career });
            }
            return terms;
        }

        /// <summary>
        /// What the known pairs add to one houseguest's desire to work with another (§14.E.1):
        /// <see cref="CounterBlocDesire"/> for each live pair neither of them stands near (<see cref="Counted"/>), at most
        /// <see cref="CounterBlocPairsCap"/> of them. Two outsiders facing the Hitmen read 7.5; facing the Hitmen and the
        /// Committee, 15, the owner's three "for numbers" counted as two pairs. A pair's members, and anyone beside one of
        /// their partners, read nought toward anybody, and anybody toward them. Nought below
        /// <see cref="AlumniRules.CounterBlocs"/>, and <see cref="NpcAlliances.Desire"/> adds it only where it is not.
        /// </summary>
        public static double CounterDesire(EpisodeState s, string npcId, string targetId)
        {
            if (!AlumniRules.At(s, AlumniRules.CounterBlocs)) return 0;
            int counted = Counted(LivePairs(s), npcId, targetId).Count;
            return counted == 0 ? 0 : CounterBlocDesire * Math.Min(CounterBlocPairsCap, counted);
        }

        /// <summary>
        /// Whom a pact's member hunts with one pact-mate as the listener (§14.E.2), or null: a member of a live pair neither
        /// of them stands near (<see cref="Counted"/>), still in the house, whom the listener still rates at
        /// <see cref="HuntStopLine"/> or more, and who is no pact-mate (<see cref="APactMate"/>: in no active pact with the
        /// hunter, nor in one with the listener that the hunter can see); of those, the one the hunter reads as the pair
        /// risk highest (<see cref="RiskFor"/>), so the hub of two pairs comes first, then by id. A hunt costs the listener
        /// <see cref="HuntStopLine"/> with its target, so the route ends while the listener is still warm to them and the
        /// agenda goes back to holding. Null below <see cref="AlumniRules.CounterBlocs"/>. The agenda never asks it with the
        /// player as the listener; a player on a pair's card can be its target, unless the player is in the pact.
        /// </summary>
        public static string CounterTarget(EpisodeState s, string hunterId, string mateId)
        {
            if (!AlumniRules.At(s, AlumniRules.CounterBlocs)) return null;
            string best = null;
            int bestRisk = 0;
            foreach (var pair in Counted(LivePairs(s), hunterId, mateId))
                foreach (string memberId in new[] { pair.FirstId, pair.SecondId })
                {
                    if (s.Find(memberId)?.status != ContestantStatus.Active || s.Score(mateId, memberId) < HuntStopLine
                        || APactMate(s, memberId, hunterId, mateId)) continue;
                    int risk = RiskFor(s, hunterId, memberId);
                    if (best == null || risk > bestRisk || (risk == bestRisk && string.CompareOrdinal(memberId, best) < 0))
                    {
                        best = memberId;
                        bestRisk = risk;
                    }
                }
            return best;
        }

        /// <summary>
        /// §14.E.1's <c>counted(a, b)</c>: the live pairs that hold nobody in <c>Near(a) ∪ Near(b)</c> (<see cref="Near"/>:
        /// a houseguest and their partners in the live pairs), in fact-id order.
        /// </summary>
        private static List<AlumniHistories.SeatedPair> Counted(List<AlumniHistories.SeatedPair> live, string a, string b)
        {
            var counted = new List<AlumniHistories.SeatedPair>();
            if (live.Count == 0) return counted;
            var near = Near(live, a);
            near.UnionWith(Near(live, b));
            foreach (var pair in live)
                if (!near.Contains(pair.FirstId) && !near.Contains(pair.SecondId)) counted.Add(pair);
            return counted;
        }

        /// <summary>§14.E.1's <c>Near(x)</c>: the houseguest and their partners in the live pairs.</summary>
        private static HashSet<string> Near(List<AlumniHistories.SeatedPair> live, string id)
        {
            var near = new HashSet<string>(StringComparer.Ordinal);
            if (id == null) return near;
            near.Add(id);
            foreach (var pair in live)
            {
                if (pair.FirstId == id) near.Add(pair.SecondId);
                else if (pair.SecondId == id) near.Add(pair.FirstId);
            }
            return near;
        }

        /// <summary>The template id a houseguest's card gives them in the edition (<see cref="AlumniHistories.Seated"/>'s rule: the template, and the table's name); null for a newcomer or a renamed card.</summary>
        private static string TemplateOf(AlumniEdition edition, ContestantState who)
        {
            if (who == null) return null;
            string templateId = who.sourceTemplateId ?? who.id;
            var identity = templateId == null ? null : edition.Identity(templateId);
            return identity != null && string.Equals(identity.Name, who.name, StringComparison.Ordinal) ? templateId : null;
        }

        /// <summary>The one houseguest on a card; null for nobody, and for two on one card, neither of whom is that alumnus.</summary>
        private static ContestantState SeatedOn(EpisodeState s, AlumniEdition edition, string templateId)
        {
            ContestantState seated = null;
            foreach (var who in s.contestants)
            {
                if (TemplateOf(edition, who) != templateId) continue;
                if (seated != null) return null;
                seated = who;
            }
            return seated;
        }

        /// <summary>Whether any houseguest holds an intact card of one of the edition's shared alliances: the early-out before the pair loops.</summary>
        private static bool SeatsASharedAlliance(EpisodeState s, AlumniEdition edition)
        {
            foreach (var who in s.contestants)
            {
                string templateId = TemplateOf(edition, who);
                if (templateId == null) continue;
                foreach (var fact in edition.Pairs)
                    if (fact.Standing > 0 && fact.Involves(templateId)) return true;
            }
            return false;
        }

        /// <summary>
        /// Whether a bloc hunt's candidate is a pact-mate (§14.E.2: pact-mates never hunt each other): an active pact holds
        /// the candidate and the hunter, or the candidate and the listener and the hunter can see it
        /// (<see cref="Knowledge.AllianceVisibleTo"/>), so a pact of the listener's the hunter has not heard of stays out of
        /// the hunter's reading.
        /// </summary>
        private static bool APactMate(EpisodeState s, string memberId, string hunterId, string mateId)
        {
            if (s.alliances == null) return false;
            foreach (var alliance in s.alliances)
                if (alliance != null && alliance.active && alliance.members != null && alliance.members.Contains(memberId)
                    && (alliance.members.Contains(hunterId)
                        || (alliance.members.Contains(mateId) && Knowledge.AllianceVisibleTo(s, alliance, hunterId))))
                    return true;
            return false;
        }

        /// <summary>Whether an active pact the evaluator can see holds both of a pair's houseguests.</summary>
        private static bool AVisiblePactHolds(EpisodeState s, AlumniHistories.SeatedPair pair, string evaluatorId)
        {
            if (s.alliances == null) return false;
            foreach (var alliance in s.alliances)
                if (alliance != null && alliance.active && alliance.members != null && alliance.members.Contains(pair.FirstId)
                    && alliance.members.Contains(pair.SecondId) && Knowledge.AllianceVisibleTo(s, alliance, evaluatorId))
                    return true;
            return false;
        }
    }
}
