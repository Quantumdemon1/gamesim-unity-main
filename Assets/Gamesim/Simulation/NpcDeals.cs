using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Houseguests striking bargains with each other.
    ///
    /// <para><b>The eviction vote has been weighing deals since it was written, and there have never
    /// been any.</b> <see cref="WebEvictionVoting"/> asks each voter what deals oblige them, scores
    /// an active <c>vote_save</c> at +35 and a broken one at −35, and carries a whole
    /// <c>PairDealValue</c> table running from an information swap at 10 to a final two at 50. It
    /// has read an empty list every time, because nothing in this project could make a deal. The
    /// most substantial and best-tested voting machinery here has been running on a system that did
    /// not exist — the same shape alliances and promises were in before Phase C.</para>
    ///
    /// <para>Ported from <c>src/systems/ai/npc-deal-proposals.ts</c>. The decision is a ladder, and
    /// the order is the rule, exactly as it is for promises: a nominee bargains from the block
    /// before anybody negotiates comfortably, a shared enemy outranks ordinary warmth, and the
    /// endgame only speaks late.</para>
    ///
    /// <para>Like the alliance and promise passes and unlike the social-action pass, this spends no
    /// randomness: every term is a function of state, and the ledger is written symmetrically rather
    /// than through the engine's relationship path, which rolls for a reciprocal delta.</para>
    /// </summary>
    public static class NpcDeals
    {
        /// <summary>Below this adjusted warmth nobody deals at all. The source's own floor.</summary>
        public const double DealingFloor = 10;

        /// <summary>What each broken deal costs a houseguest's standing when anybody sizes them up.</summary>
        public const double BrokenDealPenalty = 15;

        /// <summary>The ladder's rungs, in the source's order and with its numbers.</summary>
        public const double VoteSaveWarmth = 15;
        public const double CommonThreatWarmth = 25;
        public const double InformationWarmth = 30;
        public const double SafetyWarmth = 35;
        public const double PartnershipWarmth = 40;
        public const double UpgradeWarmth = 40;
        public const double FinalTwoWarmth = 55;

        /// <summary>Trust a houseguest wants before trading information. The source's number.</summary>
        public const double InformationTrust = 40;

        /// <summary>The house size at which a final two stops being presumptuous.</summary>
        public const int EndgameSize = 6;

        /// <summary>What validation lets a season hold, so the pass stops short of it.</summary>
        public const int DealCeiling = 200;

        // ---------------------------------------------------------------- reading the room

        /// <summary>
        /// How warmly one houseguest reads another once their record is taken into account.
        ///
        /// <para>The source's <c>adjustedRelationship</c>: the plain relationship less fifteen for
        /// every deal the other has already broken. Somebody who breaks their word twice is read
        /// thirty points colder than the same relationship would otherwise suggest, which is what
        /// stops a serial deal-breaker from bargaining their way through a whole season.</para>
        /// </summary>
        public static double Adjusted(EpisodeState state, string readerId, string aboutId) =>
            state.Score(readerId, aboutId) - BrokenDealPenalty * BrokenDeals(state, aboutId);

        public static int BrokenDeals(EpisodeState state, string whoId) =>
            state.deals.Count(d => d.status == DealStatus.Broken
                                   && (d.proposerId == whoId || d.recipientId == whoId));

        /// <summary>Deals currently binding these two, in either direction.</summary>
        public static List<DealState> Between(EpisodeState state, string a, string b) =>
            state.deals.Where(d => d.status == DealStatus.Active
                                   && ((d.proposerId == a && d.recipientId == b)
                                       || (d.proposerId == b && d.recipientId == a)))
                .ToList();

        private static bool Has(EpisodeState state, string a, string b, string kind) =>
            Between(state, a, b).Any(d => d.type == kind);

        /// <summary>
        /// Somebody they would both be glad to see go.
        ///
        /// <para>Two ways to qualify, both the source's: a houseguest they each dislike — below −10
        /// for one and −5 for the other — or a houseguest who has won two competitions and is not
        /// warm with at least one of them. The second is why a comp beast draws a coalition even
        /// when nobody has fallen out with them.</para>
        /// </summary>
        public static string CommonThreat(EpisodeState state, string a, string b) =>
            state.Active
                .Where(other => other.id != a && other.id != b)
                .FirstOrDefault(other =>
                    (state.Score(a, other.id) < -10 && state.Score(b, other.id) < -5)
                    || (other.hohWins + other.vetoWins >= 2
                        && (state.Score(a, other.id) < 20 || state.Score(b, other.id) < 20)))
                ?.id;

        // ---------------------------------------------------------------- the ladder

        /// <summary>
        /// What one houseguest would put to another, if anything.
        ///
        /// <para>The source's order, and the order is the rule. A pair already working together and
        /// warm enough make it formal; a nominee bargains from the block; a shared enemy outranks
        /// ordinary warmth; then safety, partnership, the endgame, and finally an information swap
        /// that only the sneaky and the strategic think to offer.</para>
        /// </summary>
        public static string Offer(EpisodeState state, string npcId, string targetId)
        {
            var npc = state.Find(npcId);
            var target = state.Find(targetId);
            if (npc == null || target == null || npcId == targetId) return null;
            if (npc.status != ContestantStatus.Active || target.status != ContestantStatus.Active) return null;

            double warmth = Adjusted(state, npcId, targetId);
            if (warmth < DealingFloor) return null;

            bool allied = state.Allied(npcId, targetId);
            bool partnership = Has(state, npcId, targetId, DealKind.Partnership);
            bool safety = Has(state, npcId, targetId, DealKind.SafetyAgreement);

            // Already partners and already safe: make it an alliance.
            if (partnership && safety && warmth > UpgradeWarmth && !allied)
                return DealKind.AllianceInvite;

            // On the block, which outranks every comfortable arrangement below.
            if (!state.evictionResolved && state.nominees.Contains(npcId) && !state.nominees.Contains(targetId))
            {
                if (state.vetoHolderId == targetId) return DealKind.VetoUse;
                if (warmth > VoteSaveWarmth) return DealKind.VoteSave;
            }

            if (warmth > CommonThreatWarmth && CommonThreat(state, npcId, targetId) != null)
                return DealKind.TargetAgreement;

            // Courting whoever holds the power, and only worth doing once.
            if (warmth > SafetyWarmth && !safety && !allied && state.hohId == targetId
                && !state.evictionResolved)
                return DealKind.SafetyAgreement;

            if (warmth > PartnershipWarmth && !partnership && !allied)
                return DealKind.Partnership;

            if (warmth > FinalTwoWarmth && state.Active.Count() <= EndgameSize
                && !Has(state, npcId, targetId, DealKind.FinalTwo))
                return DealKind.FinalTwo;

            // The last rung is the only one personality decides.
            bool schemer = npc.traits.Any(t => string.Equals(t, "Sneaky", StringComparison.OrdinalIgnoreCase)
                                            || string.Equals(t, "Strategic", StringComparison.OrdinalIgnoreCase));
            if (schemer && warmth > InformationWarmth
                && ThreatAssessment.TrustScore(state, targetId, npcId) > InformationTrust
                && !Has(state, npcId, targetId, DealKind.InformationSharing))
                return DealKind.InformationSharing;

            return null;
        }

        // ---------------------------------------------------------------- the pass

        /// <summary>
        /// The weekly pass: each houseguest may strike one bargain.
        ///
        /// <para><b>Both sides have to want it</b>, the same simplification the alliance pass makes
        /// and for the same reason: the source scores a proposer and handles acceptance separately,
        /// and requiring the offer to be mutual keeps the pass deterministic where a one-sided offer
        /// would need an acceptance roll. Marked as a simplification there and here.</para>
        ///
        /// <para><b>Never with the player.</b> A deal the player never agreed to is not a deal, and
        /// until a houseguest can put one to them and hear an answer, an entry in this list bearing
        /// their name would be a decision taken on their behalf. The alliance and promise passes
        /// leave them out for exactly this reason.</para>
        /// </summary>
        public static void Settle(EpisodeState state)
        {
            if (state?.npcSocial == null || state.week < state.dealRulesStartWeek) return;
            Expire(state);

            foreach (var npc in state.contestants
                         .Where(c => c.status == ContestantStatus.Active && !c.isPlayer)
                         .ToList())
            {
                if (state.deals.Count >= DealCeiling) return;

                var struck = state.contestants
                    .Where(other => other.status == ContestantStatus.Active && !other.isPlayer
                                    && other.id != npc.id)
                    .Select(other => new { other.id, kind = Offer(state, npc.id, other.id) })
                    .Where(row => row.kind != null && Offer(state, row.id, npc.id) != null)
                    .OrderByDescending(row => Adjusted(state, npc.id, row.id))
                    .ThenBy(row => row.id, StringComparer.Ordinal)
                    .FirstOrDefault();

                if (struck == null) continue;
                Strike(state, npc.id, struck.id, struck.kind);
            }
        }

        /// <summary>How many offers the player is shown in one week. The reference's ceiling.</summary>
        public const int ProposalsPerWeek = 3;

        /// <summary>
        /// Houseguests putting something to the player.
        ///
        /// <para>The counterpart to <see cref="Settle"/>, which leaves the player out. What is filed
        /// here is a <see cref="DealStatus.Proposed"/> row, not an active one — it is the question,
        /// and nothing reads it as an obligation until the player answers. The eviction vote weighs
        /// only <c>active</c>, <c>broken</c> and <c>fulfilled</c>, so an unanswered offer is inert.
        /// </para>
        ///
        /// <para>Same ladder, same order, same numbers as the NPC-to-NPC pass, because the reference
        /// runs the player through the same generator its houseguests use. What differs is the
        /// selection: the reference shows at most three, sorted so a nominee begging outranks
        /// somebody proposing a comfortable arrangement, and prefers variety in both the type and
        /// the houseguest so the player is not asked the same thing three times.</para>
        ///
        /// <para>Roll-free, like the rest of this file. Which offers arrive is a function of state;
        /// the roll is in the answer, and the answer belongs to the player.</para>
        /// </summary>
        public static void Propose(EpisodeState state)
        {
            if (state?.npcSocial == null || state.week < state.dealRulesStartWeek) return;
            var player = state.Find(state.playerId);
            if (player == null || player.status != ContestantStatus.Active) return;

            // One round of offers per week. The test is whether anything was PUT to the player this
            // week, not whether anything is still waiting — otherwise clearing the table would
            // refill it, and a player who answers promptly would be asked more than one who does not.
            if (state.deals.Any(d => d.recipientId == state.playerId && d.week == state.week
                                     && d.id.StartsWith("deal-ask-", StringComparison.Ordinal))) return;

            var offers = state.contestants
                .Where(npc => npc.status == ContestantStatus.Active && !npc.isPlayer)
                .Select(npc => new { npc, kind = Offer(state, npc.id, state.playerId) })
                .Where(row => row.kind != null)
                .OrderByDescending(row => Adjusted(state, row.npc.id, state.playerId) + Urgency(row.kind))
                .ThenBy(row => row.npc.id, StringComparer.Ordinal)
                .ToList();

            var types = new HashSet<string>(StringComparer.Ordinal);
            int room = Math.Min(ProposalsPerWeek, Math.Max(0, DealCeiling - state.deals.Count));
            foreach (var offer in offers)
            {
                if (types.Count >= room) break;
                // Variety, the reference's rule: the first offer always stands, and after that a
                // repeat of a type already on the table is skipped in favour of something new.
                if (!types.Add(offer.kind)) continue;
                state.deals.Add(new DealState
                {
                    id = "deal-ask-" + state.nextSequence,
                    type = offer.kind,
                    proposerId = offer.npc.id,
                    recipientId = state.playerId,
                    targetId = DealKind.NamesATarget(offer.kind)
                        ? TargetFor(state, offer.npc.id, state.playerId, offer.kind) : null,
                    status = DealStatus.Proposed,
                    week = state.week,
                    // The OFFER lapses at the end of the week it was made, even where the deal it
                    // would become is open-ended. An unanswered question left standing all season is
                    // a list that only grows; accepting recomputes the term from the deal's own rule.
                    expiresWeek = state.week,
                    trustImpact = DealKind.DefaultTrust(offer.kind),
                });
                RelationshipLedger.Record(state, offer.npc.id, state.playerId, "deal_proposed", 0,
                    offer.npc.name + " put a " + DealKind.Title(offer.kind).ToLowerInvariant() + " to you.");
            }
        }

        /// <summary>
        /// How loudly an offer asks to be heard first. The reference's <c>URGENCY_SCORES</c>.
        ///
        /// <para>Somebody on the block begging for the veto is not competing on warmth with somebody
        /// suggesting a partnership, and the reference sorts on warmth plus urgency so it does not
        /// have to.</para>
        /// </summary>
        public static double Urgency(string kind)
        {
            switch (kind)
            {
                case DealKind.VetoUse: return 200;
                case DealKind.VoteSave: return 150;
                case DealKind.VoteEvict: return 140;
                case DealKind.VoteTogether: return 100;
                default: return 0;
            }
        }

        /// <summary>Offers still waiting on the player, newest first.</summary>
        public static List<DealState> Pending(EpisodeState state) =>
            state?.deals
                .Where(d => d.status == DealStatus.Proposed && d.recipientId == state.playerId)
                .OrderByDescending(d => d.week)
                .ThenByDescending(d => Urgency(d.type))
                .ThenBy(d => d.id, StringComparer.Ordinal)
                .ToList() ?? new List<DealState>();

        /// <summary>
        /// Deals whose week has passed stop binding.
        ///
        /// <para>Expiry is not a break. Nobody failed anybody when a one-week voting block reaches
        /// the end of its week, so this writes nothing to the ledger — the same reasoning that keeps
        /// a soured alliance from being recorded as a betrayal.</para>
        /// </summary>
        private static void Expire(EpisodeState state)
        {
            foreach (var deal in state.deals)
                if (DealStatus.Binds(deal.status) && deal.expiresWeek > 0 && deal.expiresWeek < state.week)
                    deal.status = DealStatus.Expired;
        }

        /// <summary>Writes the bargain, both sides of the ledger, and no roll.</summary>
        private static void Strike(EpisodeState state, string from, string to, string kind)
        {
            string target = DealKind.NamesATarget(kind) ? TargetFor(state, from, to, kind) : null;
            state.deals.Add(new DealState
            {
                id = "deal-npc-" + state.nextSequence,
                type = kind,
                proposerId = from,
                recipientId = to,
                targetId = target,
                // Struck between two houseguests who both wanted it, so it is live immediately —
                // there is no proposal for anybody to answer.
                status = DealStatus.Active,
                week = state.week,
                // A final two and a partnership are open-ended; the rest are about this week.
                expiresWeek = kind == DealKind.FinalTwo || kind == DealKind.Partnership
                              || kind == DealKind.AllianceInvite || kind == DealKind.InformationSharing
                    ? 0 : state.week,
                trustImpact = DealKind.DefaultTrust(kind),
            });

            RelationshipLedger.Record(state, from, to, "deal_accepted", 18,
                state.Find(from).name + " and " + state.Find(to).name + " agreed a "
                + DealKind.Title(kind).ToLowerInvariant());
        }

        /// <summary>
        /// Who a deal of this kind is about, when it is about somebody.
        ///
        /// <para>The two vote deals point opposite ways and it matters which. A <c>vote_save</c> is
        /// asked for <i>by</i> a nominee, so it names <b>the person asking</b> — that is who the
        /// voter is being asked to keep, and it is the id <see cref="WebEvictionVoting"/> scores at
        /// +35 when it decides whether a voter is obliged. A <c>vote_evict</c> names the other
        /// nominee instead, which the same evaluator scores at −35.</para>
        /// </summary>
        private static string TargetFor(EpisodeState state, string from, string to, string kind)
        {
            if (kind == DealKind.TargetAgreement) return CommonThreat(state, from, to);
            if (kind == DealKind.VoteSave) return from;
            return state.nominees.FirstOrDefault(id => id != from && id != to);
        }
    }
}
