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

        /// <summary>
        /// The final three deal's rung (ACTIONS-DEALS-ALLIANCES-PLAN C9), this port's own: under the final
        /// two's, since it asks less of the two - to reach the end together, not to sit there together.
        /// </summary>
        public const double FinalThreeWarmth = 45;

        /// <summary>The final three: the house size at which a final three deal is kept, and past which none is put.</summary>
        public const int FinalThreeSize = 3;

        /// <summary>
        /// Whether the final four's block is set (C9, the review's M4): four in the house, and this week's
        /// veto meeting over while its eviction is still to come. From then a final three deal could never
        /// be broken - nobody is left to nominate anybody - and would always be kept, so none is put, none
        /// is taken and an offer of one lapses. The final four's own free time, after the final five's
        /// eviction, is still open.
        /// </summary>
        public static bool FinalFourBlockSet(EpisodeState state) =>
            state != null && state.Active.Count() == FinalThreeSize + 1 && state.vetoResolved && !state.evictionResolved;

        /// <summary>Trust a houseguest wants before trading information. The source's number.</summary>
        public const double InformationTrust = 40;

        /// <summary>The house size at which a final two stops being presumptuous.</summary>
        public const int EndgameSize = 6;

        /// <summary>What validation lets a season hold, so the pass stops short of it.</summary>
        public const int DealCeiling = 200;

        /// <summary>
        /// How the id of a question put to the player begins: a weekly offer (<see cref="Propose"/>)
        /// or a nominee's veto ask (<see cref="AskForTheVeto"/>). Only the player's answer makes one
        /// bind, so a deal with either that is or was binding is one the player accepted
        /// (<see cref="DealResolution.AcceptedOffer"/>).
        /// </summary>
        public const string OfferPrefix = "deal-ask-", VetoAskPrefix = "deal-veto-";

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

        /// <summary>
        /// The broken deals held against somebody: under the commitment rules the ones they broke
        /// (ACTIONS-DEALS-ALLIANCES-PLAN C0, X3), before them every broken deal they were either side
        /// of (<see cref="Breaches.CountsAgainst(EpisodeState, DealState, string)"/>). The player's own
        /// count is what the acceptance roll and its line read too.
        /// </summary>
        public static int BrokenDeals(EpisodeState state, string whoId)
        {
            // Mode 2 (vote family V5b): mode 1's raw list, its Vote deals counted once per Rule2 incident (D1).
            var incidents = UnifiedVoteHistory.Breaches(state);
            int legacy = CommitmentReferences.RawDeals(state).Count(d => Breaches.CountsAgainst(state, d, whoId)
                && (!UnifiedVoteHistory.ByIncident(state, d) || incidents.Any(i => i.DealId == d.id && i.ActorId == whoId)));
            if (!UnifiedCommitments.SafetyAuthorityOn(state)) return legacy;
            // This source term measures broken deals, not every kind of word. A promise-only
            // incident stays outside it; any number of reciprocal Safety deal aliases counts
            // once, against the actual actor and never against the person they wronged.
            var deals = new HashSet<string>(UnifiedCommitmentHistory.Records(state)
                .Where(row => row.sourcePolicy == UnifiedCommitments.DealPolicy).Select(row => row.id), StringComparer.Ordinal);
            return legacy + UnifiedCommitmentHistory.Breaches(state)
                .Count(incident => incident.ActorId == whoId && incident.EvidenceIds.Any(deals.Contains));
        }

        /// <summary>Deals currently binding these two, in either direction.</summary>
        public static List<DealState> Between(EpisodeState state, string a, string b) =>
            UnifiedVoteStore.Deals(state).Where(d => d.status == DealStatus.Active
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

            // Already partners and already safe: make it an alliance - unless, under the commitment
            // rules, the player it would be put to holds as many pacts as they may (C4, decision 10),
            // and could only turn it down. The agency rung below counts them through WouldPropose.
            if (partnership && safety && warmth > UpgradeWarmth && !allied
                && !(target.isPlayer && EpisodeEngine.InvitationPastPactCap(state, npcId)))
                return DealKind.AllianceInvite;

            // Under agency a houseguest looking for a partner asks the one they want, when that is
            // the player (NPC-AGENCY-PLAN.md §5.3); between houseguests the alliance pass does the
            // asking. The same bar the pass sets: warm enough, wanting it enough, no grudge.
            if (target.isPlayer && !allied && EpisodeEngine.AgencyOn(state)
                && NpcAgendas.Of(state, npcId) is NpcAgenda agenda && agenda.kind == Agendas.Build && agenda.partnerId == targetId
                && NpcAlliances.WouldPropose(state, npcId, targetId))
                return DealKind.AllianceInvite;

            // On the block, which outranks every comfortable arrangement below.
            if (!state.evictionResolved && state.nominees.Contains(npcId) && !state.nominees.Contains(targetId))
            {
                // Nobody asks for the veto once it is decided - from the strategy windows. Before
                // them a nominee asked the holder anyway, at the campaign, which is after it.
                if (state.vetoHolderId == targetId && !(StrategyRules.Apply(state) && state.vetoResolved)) return DealKind.VetoUse;
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

            // Under the commitment rules (C9) the endgame has a rung below the final two: from the final
            // six to the final four, two warm enough take each other to the final three, unless a final
            // two or a final three already binds them. Without the rules the ladder is the reference's.
            if (EpisodeEngine.CommitmentRulesOn(state) && warmth > FinalThreeWarmth
                && state.Active.Count() <= EndgameSize && state.Active.Count() > FinalThreeSize && !FinalFourBlockSet(state)
                && !Has(state, npcId, targetId, DealKind.FinalTwo) && !Has(state, npcId, targetId, DealKind.FinalThree))
                return DealKind.FinalThree;

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
                if (UnifiedVoteStore.DealCount(state) >= DealCeiling) return;

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
            if (UnifiedVoteStore.Deals(state).Any(d => d.recipientId == state.playerId && d.week == state.week
                                     && d.id.StartsWith(OfferPrefix, StringComparison.Ordinal))) return;

            var offers = state.contestants
                .Where(npc => npc.status == ContestantStatus.Active && !npc.isPlayer)
                .Select(npc => new { npc, kind = Offer(state, npc.id, state.playerId) })
                .Where(row => row.kind != null)
                .OrderByDescending(row => Adjusted(state, row.npc.id, state.playerId) + Urgency(row.kind))
                .ThenBy(row => row.npc.id, StringComparer.Ordinal)
                .ToList();

            var types = new HashSet<string>(StringComparer.Ordinal);
            int room = Math.Min(ProposalsPerWeek, Math.Max(0, DealCeiling - UnifiedVoteStore.DealCount(state)));
            foreach (var offer in offers)
            {
                if (types.Count >= room) break;
                // Variety, the reference's rule: the first offer always stands, and after that a
                // repeat of a type already on the table is skipped in favour of something new.
                if (!types.Add(offer.kind)) continue;
                var proposed = new DealState
                {
                    id = OfferPrefix + state.nextSequence,
                    type = offer.kind,
                    proposerId = offer.npc.id,
                    recipientId = state.playerId,
                    targetId = DealKind.NamesATarget(offer.kind)
                        ? TargetFor(state, offer.npc.id, state.playerId, offer.kind) : null,
                    status = DealStatus.Proposed,
                    week = state.week,
                    // The OFFER is this week's, even where the deal it would become is open-ended.
                    // Expire writes it off at the first Settle of a later week, and the first of
                    // those runs as next week's campaign opens: an unanswered offer outlives the
                    // week's turn and next week's nominations and veto, and goes as that campaign
                    // opens. One filed in the final four's week - at its campaign, or as the Final
                    // 3's window opens - is never written off: no Settle runs in a later week. An
                    // unanswered question left standing all season is a list that only grows;
                    // accepting recomputes the term from the deal's own rule.
                    expiresWeek = state.week,
                    trustImpact = DealKind.DefaultTrust(offer.kind),
                };
                if (UnifiedCommitments.SafetyAuthorityOn(state) && offer.kind == DealKind.SafetyAgreement)
                {
                    if (!UnifiedCommitmentStore.TryAddDeal(state, proposed, UnifiedCommitments.NpcOffer, out _))
                    {
                        types.Remove(offer.kind);
                        continue;
                    }
                }
                // Mode 2 (vote family V3): a vote offer is a canonical Proposed row, refused - and skipped,
                // as a refused safety offer is - before its ledger line spends the sequence.
                else if (UnifiedVoteStore.On(state) && UnifiedVoteStore.IsVote(offer.kind))
                {
                    if (!UnifiedVoteStore.TryAddDeal(state, proposed, UnifiedCommitments.NpcOffer, out _))
                    {
                        types.Remove(offer.kind);
                        continue;
                    }
                }
                else state.deals.Add(proposed);
                RelationshipLedger.Record(state, offer.npc.id, state.playerId, "deal_proposed", 0,
                    offer.npc.name + " put a " + DealKind.Title(offer.kind).ToLowerInvariant() + " to you.");
            }
        }

        /// <summary>
        /// Nominees asking the player for the veto, before the meeting - from the strategy windows.
        ///
        /// <para>The one bargain the weekly round can never ask in time: it is struck as the social
        /// week opens and at the campaign, and the veto is decided between the two. So a nominee warm
        /// enough to bargain at all asks the player as soon as the competition is over, while the
        /// answer can still change something, and says so where the player will see it. Outside the
        /// weekly round, so it does not use up the week's three offers; roll-free, like the rest of
        /// this file.</para>
        /// </summary>
        public static void AskForTheVeto(EpisodeState state)
        {
            if (!StrategyRules.Apply(state) || state.week < state.dealRulesStartWeek) return;
            if (state.vetoHolderId != state.playerId || state.vetoResolved || !StrategyRules.VetoCanBeUsed(state)) return;
            if (state.Find(state.playerId)?.status != ContestantStatus.Active) return;
            var asking = state.nominees.Select(state.Find)
                .Where(npc => npc != null && !npc.isPlayer && npc.status == ContestantStatus.Active
                              && Adjusted(state, npc.id, state.playerId) >= DealingFloor
                              && !state.deals.Any(d => DealStatus.Binds(d.status) && d.type == DealKind.VetoUse
                                                       && d.proposerId == npc.id && d.recipientId == state.playerId))
                .OrderByDescending(npc => Adjusted(state, npc.id, state.playerId))
                .ThenBy(npc => npc.id, StringComparer.Ordinal)
                .ToList();
            foreach (var npc in asking)
            {
                if (UnifiedVoteStore.DealCount(state) >= DealCeiling) return;
                state.deals.Add(new DealState
                {
                    id = VetoAskPrefix + state.nextSequence,
                    type = DealKind.VetoUse,
                    proposerId = npc.id,
                    recipientId = state.playerId,
                    status = DealStatus.Proposed,
                    week = state.week,
                    expiresWeek = state.week,
                    trustImpact = DealKind.DefaultTrust(DealKind.VetoUse),
                });
                RelationshipLedger.Record(state, npc.id, state.playerId, "deal_proposed", 0,
                    npc.name + " put a " + DealKind.Title(DealKind.VetoUse).ToLowerInvariant() + " to you.");
                EpisodeEngine.Log(state, "deal", npc.name + " is on the block and wants your word on the veto.", state.playerId, npc.id);
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

        /// <summary>
        /// Offers still waiting on the player, newest first: wherever canonical Safety is the authority, its offers
        /// too, and in the prospective mode 2 the canonical Vote offers (vote family V5a).
        /// </summary>
        public static List<DealState> Pending(EpisodeState state) =>
            (state == null ? null : UnifiedCommitments.SafetyAuthorityOn(state) ? CommitmentReferences.Deals(state) : state.deals)
                ?.Where(d => d.status == DealStatus.Proposed && d.recipientId == state.playerId)
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
            // Mode 2 (vote family V4): the canonical vote deals and offers past their week end here too - first, so
            // the safety gateway's check of the whole commitment storage meets them already ended.
            EpisodeEngine.ResolveUnifiedVoteExpiry(state, UnifiedCommitmentExpiry.DealPass);
            if (UnifiedCommitments.SafetyAuthorityOn(state))
                EpisodeEngine.ResolveUnifiedSafetyExpiry(state, UnifiedCommitmentExpiry.DealPass);
            foreach (var deal in state.deals)
                if (DealStatus.Binds(deal.status) && deal.expiresWeek > 0 && deal.expiresWeek < state.week)
                    deal.status = DealStatus.Expired;
        }

        /// <summary>Writes the bargain, both sides of the ledger, and no roll.</summary>
        private static void Strike(EpisodeState state, string from, string to, string kind)
        {
            string target = DealKind.NamesATarget(kind) ? TargetFor(state, from, to, kind) : null;
            var deal = new DealState
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
                // A final two and a partnership are open-ended, and so is a final three deal (C9), which
                // runs until the house is down to three; the rest are about this week.
                expiresWeek = kind == DealKind.FinalTwo || kind == DealKind.Partnership
                              || kind == DealKind.AllianceInvite || kind == DealKind.InformationSharing
                              || kind == DealKind.FinalThree
                    ? 0 : state.week,
                trustImpact = DealKind.DefaultTrust(kind),
            };
            if (UnifiedCommitments.SafetyAuthorityOn(state) && kind == DealKind.SafetyAgreement)
            {
                if (!UnifiedCommitmentStore.TryAddDeal(state, deal, UnifiedCommitments.NpcDeal, out _)) return;
            }
            // Mode 2 (vote family V3): a nominee's vote bargain is a canonical row; refused, nothing is
            // written for it, as a refused safety bargain writes nothing.
            else if (UnifiedVoteStore.On(state) && UnifiedVoteStore.IsVote(kind))
            {
                if (!UnifiedVoteStore.TryAddDeal(state, deal, UnifiedCommitments.NpcDeal, out _)) return;
            }
            else state.deals.Add(deal);

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
