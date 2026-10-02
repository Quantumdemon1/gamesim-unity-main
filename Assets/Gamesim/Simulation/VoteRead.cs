using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The vote read (STRATEGY-LOOP-PLAN.md §2): each voter's leaning, built from the vote
    /// evaluator's own terms and stripped of the ones the player has not learned. A projection of
    /// the ballot as the state stands now, never the ballot itself: the house votes at the vote.
    ///
    /// <para>A term is known when the player learned it. Their view of a nominee: a standing the
    /// player holds for that pair, from a read, an overheard conversation or an account, within
    /// <see cref="StandingShelfLife"/> weeks. An alliance: one the player can see. A deal or promise:
    /// one the player is party to. A grudge or bond: one aimed at the player. The bloc: when its
    /// alliance is known. Threat, personality, memory and the rest are never shown; they are the
    /// unknowns, counted so the read says how much it cannot see.</para>
    ///
    /// <para>What somebody said their vote was (a claim) sits beside the computed read, with its
    /// source; a claim can be a lie, and the reveal judges it.</para>
    ///
    /// <para>Under the commitment rules an ally's own commitment to the player drives their alliance
    /// and bloc terms (ACTIONS-DEALS-ALLIANCES-PLAN C3), and that commitment is theirs: the read
    /// counts those terms as unknown until the player knows where the ally stands - a current read of
    /// how they see the player, or a betrayal of theirs the player can know
    /// (<see cref="CommitmentHidden"/>). Reading an ally is how the read learns whether they are
    /// still with you; knowing the pact is not.</para>
    /// </summary>
    public static class VoteRead
    {
        public const string Firm = "firm", Leaning = "leaning", Torn = "torn", Unknown = "unknown";
        /// <summary>How long a learned standing stays current, in weeks: this week and the two before.</summary>
        public const int StandingShelfLife = 2;
        /// <summary>The evaluator's own thresholds: decisive at 25, leaning at 10, a toss-up under.</summary>
        public const double FirmMargin = 25, LeaningMargin = 10;

        /// <summary>The terms the read never shows as numbers.</summary>
        private static readonly string[] Hidden = { "threat", "strategicValue", "personality", "memory", "persona" };

        public sealed class VoterRead
        {
            public string voterId;
            /// <summary>The nominee the known terms say they evict, or null when the read is unknown.</summary>
            public string leaningId;
            public string confidence = Unknown;
            /// <summary>The known terms' margin between the two nominees.</summary>
            public double knownMargin;
            /// <summary>Whether the margin can be shown as a number: their view of both nominees and their alliances are known.</summary>
            public bool exact;
            /// <summary>Distinct terms the read cannot see for this voter.</summary>
            public int unknownTerms;
            public List<string> knownTerms = new List<string>();
            /// <summary>What they said, or what was overheard, this week: the latest claim's target, or null.</summary>
            public string saysId, saysSource;
            public List<ClaimRow> claims = new List<ClaimRow>();
        }

        public sealed class Sheet
        {
            public bool available;
            public List<string> nomineeIds = new List<string>();
            public List<VoterRead> voters = new List<VoterRead>();
            /// <summary>Known leanings toward evicting the first and the second nominee, and the rest.</summary>
            public int evictFirst, evictSecond, unknown;
            /// <summary>Who the whip count says goes, or null when it says nothing.</summary>
            public string predictedEvicteeId;
        }

        /// <summary>A read exists from the campaign to the reveal, while two nominees stand.</summary>
        public static bool Available(EpisodeState s) =>
            s != null && s.nominees != null && s.nominees.Count == 2
            && (s.phase == EpisodePhase.Campaign || (s.phase == EpisodePhase.Eviction && !s.evictionResolved));

        public static Sheet Read(EpisodeState s)
        {
            var sheet = new Sheet { available = Available(s) };
            if (!sheet.available) return sheet;
            sheet.nomineeIds = new List<string>(s.nominees);
            foreach (var voter in EpisodeEngine.Voters(s).Where(v => !v.isPlayer))
                sheet.voters.Add(ReadVoter(s, voter.id, EpisodeEngine.ProjectBallot(s, voter.id)));
            foreach (var read in sheet.voters)
            {
                string leaning = read.confidence != Unknown ? read.leaningId : read.saysId;
                if (leaning == sheet.nomineeIds[0]) sheet.evictFirst++;
                else if (leaning == sheet.nomineeIds[1]) sheet.evictSecond++;
                else sheet.unknown++;
            }
            // The player's own ballot, once cast, is the one leaning they are sure of.
            var own = s.votes.FirstOrDefault(v => v.voterId == s.playerId);
            if (own != null)
            {
                if (own.targetId == sheet.nomineeIds[0]) sheet.evictFirst++;
                else if (own.targetId == sheet.nomineeIds[1]) sheet.evictSecond++;
            }
            sheet.predictedEvicteeId = sheet.evictFirst > sheet.evictSecond ? sheet.nomineeIds[0]
                : sheet.evictSecond > sheet.evictFirst ? sheet.nomineeIds[1] : null;
            return sheet;
        }

        public static VoterRead ReadVoter(EpisodeState s, string voterId, WebVoteEvaluation evaluation)
        {
            var read = new VoterRead { voterId = voterId };
            var known = new Dictionary<string, double>(StringComparer.Ordinal);
            var unknownCodes = new HashSet<string>(StringComparer.Ordinal);
            var knownCodes = new HashSet<string>(StringComparer.Ordinal);
            bool relationshipKnownForBoth = true, alliancesKnown = true;
            foreach (var nominee in evaluation.nomineeEvaluations)
            {
                double sum = 0;
                foreach (var factor in nominee.factors)
                {
                    if (FactorKnown(s, voterId, nominee.nomineeId, factor)) { sum += factor.value; knownCodes.Add(factor.code); continue; }
                    if (factor.code == "relationship") relationshipKnownForBoth = false;
                    if (factor.code == "alliance" || factor.code == "blocPressure") alliancesKnown = false;
                    // A term an ally's hidden commitment decides is counted whatever it came to: a count
                    // that skipped it at nothing would say the ally had lapsed.
                    if (factor.code == "relationship" || Math.Abs(factor.value) > 0.01
                        || ((factor.code == "alliance" || factor.code == "blocPressure") && CommitmentHidden(s, voterId)))
                        unknownCodes.Add(factor.code);
                }
                known[nominee.nomineeId] = sum;
            }
            read.knownTerms = knownCodes.OrderBy(code => code, StringComparer.Ordinal).ToList();
            read.unknownTerms = unknownCodes.Count;
            read.claims = s.ledger.claims.Where(k => k.voterId == voterId && k.week == s.week && k.status == ClaimStatus.Open).ToList();
            var latest = read.claims.LastOrDefault();
            if (latest != null) { read.saysId = latest.targetId; read.saysSource = latest.source; }

            var pair = evaluation.nomineeEvaluations.Select(n => n.nomineeId).ToArray();
            bool anyRelationshipKnown = evaluation.nomineeEvaluations.Any(n => n.factors.Any(f => f.code == "relationship" && FactorKnown(s, voterId, n.nomineeId, f)));
            if (!anyRelationshipKnown) return read;
            read.knownMargin = Math.Abs(known[pair[0]] - known[pair[1]]);
            // The evaluator evicts the lower score; a dead heat is a toss-up, not a lean.
            read.leaningId = known[pair[0]] < known[pair[1]] ? pair[0] : known[pair[1]] < known[pair[0]] ? pair[1] : null;
            read.confidence = read.leaningId == null ? Torn : read.knownMargin >= FirmMargin ? Firm : read.knownMargin >= LeaningMargin ? Leaning : Torn;
            read.exact = relationshipKnownForBoth && alliancesKnown;
            return read;
        }

        /// <summary>A read in a few words, for a lever's line: no read, torn, or the lean and how sure it is.</summary>
        public static string Describe(EpisodeState s, VoterRead read)
        {
            if (read == null || read.confidence == Unknown) return "no read";
            if (read.confidence == Torn) return "torn";
            return (read.confidence == Firm ? "firm evict " : "leaning evict ") + (s.Find(read.leaningId)?.name ?? read.leaningId);
        }

        /// <summary>Whether the player has learned this term of a voter's evaluation of a nominee.</summary>
        public static bool FactorKnown(EpisodeState s, string voterId, string nomineeId, WebVoteFactor factor)
        {
            if (factor == null) return false;
            switch (factor.code)
            {
                case "relationship": return StandingKnown(s, voterId, nomineeId);
                case "history": return true;
                case "obligation": return true;
                case "plea": return true;
                case "alliance":
                    if (nomineeId == s.playerId && CommitmentHidden(s, voterId)) return false;
                    return factor.evidenceIds.All(id => AllianceKnown(s, id));
                case "blocPressure":
                    if (CommitmentHidden(s, voterId)) return false;
                    return Math.Abs(factor.value) < 0.01 || factor.evidenceIds.All(id => AllianceKnown(s, id));
                case "deal":
                    return factor.evidenceIds.All(id => PartyTo(s, id));
                case "grudge":
                case "bond":
                    return nomineeId == s.playerId;
                default:
                    return Array.IndexOf(Hidden, factor.code) < 0 && Math.Abs(factor.value) < 0.01;
            }
        }

        /// <summary>
        /// An alliance the player has learned of: one they are in, or one whose fact they hold. The
        /// evaluators' legacy rule (an alliance with no fact is known to everyone) is the voters',
        /// not the player's: nothing ever showed the player a fact-less pact, so the read does not
        /// either. Evidence naming no alliance still standing hides nothing.
        /// </summary>
        public static bool AllianceKnown(EpisodeState s, string allianceId)
        {
            var alliance = s.alliances.FirstOrDefault(a => a.id == allianceId);
            if (alliance == null) return true;
            if (alliance.members.Contains(s.playerId)) return true;
            return Knowledge.Knows(Knowledge.Of(s, FactKinds.Alliance, alliance.id), s.playerId);
        }

        /// <summary>
        /// Whether an ally's own commitment to the player is hidden from the player, under the
        /// commitment rules (C3): the two share a standing pact, so the ally's alliance and bloc terms
        /// are theirs to give, and the player has neither a current read of how they see them nor a
        /// betrayal of theirs to know. Never without the rules, nor for somebody outside the player's pacts.
        /// </summary>
        public static bool CommitmentHidden(EpisodeState s, string voterId)
        {
            if (!EpisodeEngine.CommitmentRulesOn(s) || string.IsNullOrEmpty(voterId) || voterId == s.playerId
                || !s.Allied(voterId, s.playerId)) return false;
            if (StandingKnown(s, voterId, s.playerId)) return false;
            int week = Allegiance.BetrayalWeek(s, voterId);
            return !(week > 0 && Allegiance.Betrayed(s, voterId) && Allegiance.KnownBetrayals(s, voterId).Any(e => e.week == week));
        }

        /// <summary>A standing the player holds for the pair, learned recently enough to still be current.</summary>
        public static bool StandingKnown(EpisodeState s, string fromId, string toId) =>
            s.ledger.standings.Any(row => row.fromId == fromId && row.toId == toId && !ClaimSource.IsAttempt(row.source) && row.week >= s.week - StandingShelfLife);

        /// <summary>The player is party to a deal by its id, or to a promise by the evaluator's "promise:from:to:type" evidence.</summary>
        private static bool PartyTo(EpisodeState s, string evidence)
        {
            if (string.IsNullOrEmpty(evidence)) return true;
            if (evidence.StartsWith("promise:", StringComparison.Ordinal))
            {
                var parts = evidence.Split(':');
                return parts.Length >= 3 && (parts[1] == s.playerId || parts[2] == s.playerId);
            }
            var deal = s.deals.FirstOrDefault(d => d.id == evidence);
            return deal != null && (deal.proposerId == s.playerId || deal.recipientId == s.playerId);
        }
    }
}
