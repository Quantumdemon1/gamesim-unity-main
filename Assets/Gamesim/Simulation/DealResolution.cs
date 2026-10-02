using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Whether a deal was kept.
    ///
    /// <para>Without this, <see cref="DealStatus.Fulfilled"/> and <see cref="DealStatus.Broken"/>
    /// are states nothing ever writes — and three separate systems already read them.
    /// <see cref="NpcDeals.Adjusted"/> docks fifteen points of standing per broken deal,
    /// <see cref="PlayerDeals.AcceptanceChance"/> docks twelve, and <see cref="WebEvictionVoting"/>
    /// scores a broken deal at −35 and a kept one at +5. Every one of those would have read zero
    /// forever. This file is the producer for all three, and its absence would have left the deal
    /// system in exactly the shape it was written to fix.</para>
    ///
    /// <para>Ported from <c>deal-action-rules.ts</c> and <c>deal-system.ts</c>'s
    /// <c>evaluateDealsForAction</c>. The rules below are pure — no state, no clock, no randomness,
    /// exactly as the source's own extraction is — so the engine can consult them and decide
    /// separately what to write.</para>
    ///
    /// <para><b>The reference resolves four actions and no others.</b> A <c>vote_save</c> is never
    /// marked kept or broken by the vote; it is <i>weighed</i> by the vote evaluator instead, at +35
    /// for the nominee it protects. That is the source's design rather than an omission here, and
    /// changing it would double-count the same promise.</para>
    ///
    /// <para><b>Under the commitment rules every deal does something</b> (ACTIONS-DEALS-ALLIANCES-PLAN
    /// C1): a partnership is judged at every vote that tests it and stands while it is kept
    /// (<see cref="PartnershipAtTheVote"/>), a safety pact is kept when the Head of Household it is
    /// with spares a partner they did not put up that week (<see cref="Spared"/>), a
    /// vote deal both parties broke names neither (<see cref="VoteDeal"/>), and the final choice
    /// passes over a final two with somebody no longer in the house (X4). An information deal does
    /// its work in the engine (<c>EpisodeEngine.PassTheReadings</c>), and a final two deal is weighed
    /// in the final Head of Household's choice before it is judged here. A season without the rules
    /// is judged as it always was.</para>
    /// </summary>
    public static class DealResolution
    {
        /// <summary>
        /// What keeping or breaking a deal moves, before its trust weight.
        ///
        /// <para>The reference's <c>applyDealOutcome</c>: <c>8 * multiplier</c> and
        /// <c>-15 * multiplier</c>. Breaking one costs nearly twice what keeping it earns, which is
        /// the same asymmetry the promise table already has at +25 and −40.</para>
        /// </summary>
        public const double FulfilledBase = 8, BrokenBase = -15;

        /// <summary>How likely a bystander is to hear that somebody broke their word.</summary>
        public const double BetrayalChance = 0.4;

        /// <summary>What hearing it costs the betrayer, from −5 to −15. The reference's range.</summary>
        public const double BetrayalFloor = -5, BetrayalSpread = -10;

        /// <summary>What a fulfilled or broken deal of this weight moves on the ledger.</summary>
        public static double Impact(DealState deal, string status) =>
            (status == DealStatus.Fulfilled ? FulfilledBase : BrokenBase) * DealTrust.Weight(deal.trustImpact);

        /// <summary>
        /// The same in a season: a breach weighs <see cref="BreachWeight"/>, which under the
        /// commitment rules is one step heavier for an offer the player accepted. A kept deal, and
        /// every deal in a season without the rules, weighs what <see cref="Impact(DealState, string)"/> says.
        /// </summary>
        public static double Impact(EpisodeState s, DealState deal, string status) =>
            status == DealStatus.Fulfilled ? FulfilledBase * DealTrust.Weight(deal.trustImpact) : BrokenBase * BreachWeight(s, deal);

        /// <summary>
        /// What a breach of this deal stakes, for every reader that weighs one - the ledger at the
        /// settlement, the jury (<see cref="WebJuryVoting.Obligations"/>): its own trust weight, and
        /// under the commitment rules one step heavier for an offer the player accepted
        /// (ACTIONS-DEALS-ALLIANCES-PLAN C1, decision 15), whoever breaks it. Accepting costs no action
        /// and earns +4 now, not +12 (<see cref="PlayerDeals.CommittedAcceptedImpact"/>); a yes is a
        /// commitment, and walking away from one is worse than walking away from a deal nobody was
        /// asked to take.
        /// </summary>
        public static double BreachWeight(EpisodeState s, DealState deal) =>
            deal == null ? 0 : DealTrust.Weight(AcceptedOffer(s, deal) ? DealTrust.Heavier(deal.trustImpact) : deal.trustImpact);

        /// <summary>
        /// Whether this deal is an offer the player accepted under the commitment rules: a question a
        /// houseguest put to them (<see cref="NpcDeals.OfferPrefix"/>, <see cref="NpcDeals.VetoAskPrefix"/>),
        /// which only the player's yes makes bind, and the price that question carried (C7) - part of the
        /// same bargain, struck by the same yes; or a counter the player took (C7,
        /// <see cref="Negotiation.FromACounter"/>), the deal they asked for at the houseguest's price and
        /// that price, which only their yes to the houseguest's offer struck - that binds or was settled,
        /// and was put no earlier than the rules' first week - so the yes was given under them too. A deal
        /// the player put to somebody and had agreed, one two houseguests struck, one a story made, and the
        /// veto the player named a price for, with that price, are not.
        /// </summary>
        public static bool AcceptedOffer(EpisodeState s, DealState deal) =>
            deal != null && s != null && EpisodeEngine.CommitmentRulesOn(s) && deal.week >= s.commitmentRulesStartWeek
            && deal.id != null
            && (PutToThePlayer(s, deal) || PutToThePlayer(s, Negotiation.BoughtWith(s, deal)) || Negotiation.FromACounter(s, deal))
            && (deal.status == DealStatus.Active || deal.status == DealStatus.Accepted || deal.status == DealStatus.Fulfilled || deal.status == DealStatus.Broken);

        /// <summary>A question a houseguest put to the player - an offer, or a nominee's veto ask - which only the player's yes makes bind.</summary>
        private static bool PutToThePlayer(EpisodeState s, DealState deal) =>
            deal != null && deal.id != null && deal.recipientId == s.playerId
            && (deal.id.StartsWith(NpcDeals.OfferPrefix, StringComparison.Ordinal) || deal.id.StartsWith(NpcDeals.VetoAskPrefix, StringComparison.Ordinal));

        /// <summary>The other party to a deal, from one party's point of view.</summary>
        public static string Partner(DealState deal, string whoId) =>
            deal.proposerId == whoId ? deal.recipientId
            : deal.recipientId == whoId ? deal.proposerId : null;

        // ---------------------------------------------------------------- the four rules

        /// <summary>
        /// A nomination, against the deals the nominator is party to.
        ///
        /// <para>Two types answer to a nomination. A <c>target_agreement</c> is kept when the person
        /// it names goes up and broken when the partner does — and <b>hitting the target wins</b>,
        /// even where the same ceremony also puts the partner on the block. The source marks that
        /// precedence explicitly, so it is preserved here rather than reasoned about again. A
        /// <c>safety_agreement</c> has no way to be kept by a nomination; it can only be broken.
        /// </para>
        ///
        /// <para>The nominee list describes <i>this action</i>, including a one-person replacement,
        /// not the block as it now stands. Passing a rebuilt block would mark a safety pact broken
        /// every time the veto reshuffled around it.</para>
        /// </summary>
        public static string Nomination(DealState deal, string nominatorId, IReadOnlyList<string> nomineeIds)
        {
            if (deal == null || nomineeIds == null) return null;
            string partner = Partner(deal, nominatorId);
            if (partner == null) return null;

            if (deal.type == DealKind.TargetAgreement && !string.IsNullOrEmpty(deal.targetId))
            {
                if (nomineeIds.Contains(deal.targetId)) return DealStatus.Fulfilled;
                if (nomineeIds.Contains(partner)) return DealStatus.Broken;
                return null;
            }
            if (deal.type == DealKind.SafetyAgreement && nomineeIds.Contains(partner))
                return DealStatus.Broken;
            return null;
        }

        /// <summary>
        /// A veto decision, against a commitment to use it.
        ///
        /// <para><paramref name="partnerIsNominated"/> is an observation the caller supplies rather
        /// than something inferred here, and the source is emphatic about why: saving the partner
        /// fulfils the deal <i>even after</i> their nomination flag has been cleared by the save
        /// itself. Reading the flag inside this branch would turn every honoured commitment into a
        /// no-op.</para>
        /// </summary>
        public static string Veto(DealState deal, string holderId, string savedId, bool used, bool partnerIsNominated)
        {
            if (deal == null || deal.type != DealKind.VetoUse) return null;
            string partner = Partner(deal, holderId);
            if (partner == null) return null;

            if (used && savedId == partner) return DealStatus.Fulfilled;
            if ((!used || savedId != partner) && partnerIsNominated) return DealStatus.Broken;
            return null;
        }

        /// <summary>
        /// The eviction vote, against an agreement to vote as a block.
        ///
        /// <para>The source tracks each partner's ballot on the deal itself and compares them once
        /// both have voted. Reading the recorded votes instead reaches the same answer without a
        /// second store: by the time the house has voted, <see cref="EpisodeState.votes"/> holds
        /// exactly what that tracker would have accumulated. A pair who did not both vote — one of
        /// them was the Head of Household, or on the block — agreed nothing either way.</para>
        /// </summary>
        public static string VoteTogether(DealState deal, IReadOnlyList<VoteState> votes)
        {
            if (deal == null || deal.type != DealKind.VoteTogether || votes == null) return null;
            var proposer = votes.FirstOrDefault(v => v.voterId == deal.proposerId);
            var recipient = votes.FirstOrDefault(v => v.voterId == deal.recipientId);
            if (proposer == null || recipient == null) return null;
            return proposer.targetId == recipient.targetId ? DealStatus.Fulfilled : DealStatus.Broken;
        }

        /// <summary>
        /// The eviction vote, against a vote deal (STRATEGY-LOOP-PLAN.md §3). A <c>vote_evict</c> is
        /// kept by a party who voted out the person it names and broken by one who voted to keep
        /// them; a <c>vote_save</c> the other way round. A party who did not vote (the Head of
        /// Household, somebody on the block, somebody already gone) decided nothing. Where one party
        /// broke it, they broke it; where nobody did, the first who voted kept it. A deal naming
        /// nobody on this block is not this vote's to judge.
        ///
        /// <para>Where both parties broke it, the first of them was named as its breaker, and the
        /// settlement held the second as the one wronged - a permanent grudge for a deal they had
        /// broken themselves. Under the commitment rules (<paramref name="bothNameNobody"/>,
        /// ACTIONS-DEALS-ALLIANCES-PLAN C1) it names nobody, as a voting bloc that fell apart does:
        /// both walked away from it, and each holds it against the other.</para>
        /// </summary>
        public static string VoteDeal(DealState deal, IReadOnlyList<VoteState> votes, IReadOnlyList<string> nominees, out string actorId,
            bool bothNameNobody = false)
        {
            actorId = null;
            if (deal == null || votes == null || nominees == null || deal.targetId == null || !nominees.Contains(deal.targetId)) return null;
            if (deal.type != DealKind.VoteSave && deal.type != DealKind.VoteEvict) return null;
            string kept = null, broke = null;
            foreach (string party in new[] { deal.proposerId, deal.recipientId })
            {
                var ballot = votes.FirstOrDefault(v => v.voterId == party);
                if (ballot == null) continue;
                bool evictedTarget = ballot.targetId == deal.targetId;
                if (deal.type == DealKind.VoteEvict ? !evictedTarget : evictedTarget)
                {
                    if (!bothNameNobody) { actorId = party; return DealStatus.Broken; }
                    // The second breaker: both of them broke it, so neither is the one who did.
                    if (broke != null) { actorId = null; return DealStatus.Broken; }
                    broke = party;
                    continue;
                }
                if (kept == null) kept = party;
            }
            if (broke != null) { actorId = broke; return DealStatus.Broken; }
            if (kept == null) return null;
            actorId = kept;
            return DealStatus.Fulfilled;
        }

        /// <summary>
        /// The eviction vote, against a partnership, under the commitment rules
        /// (ACTIONS-DEALS-ALLIANCES-PLAN C1): a partnership is tested when one of the two is on the
        /// block and the other casts a ballot - with the house, or as the Head of Household breaking
        /// a tie. Voting their partner out breaks it; voting the other nominee out keeps it, and a
        /// partnership kept goes on standing (<see cref="Verdict.stands"/>): it is judged again at
        /// every vote that tests it, and only a breach ends it. The voter decided it either way. A
        /// vote with neither of them on the block, or both, or with the one off it casting no ballot,
        /// does not test it.
        /// </summary>
        public static string PartnershipAtTheVote(DealState deal, IReadOnlyList<VoteState> votes, IReadOnlyList<string> nominees, out string actorId)
        {
            actorId = null;
            if (deal == null || deal.type != DealKind.Partnership || votes == null || nominees == null) return null;
            bool proposerUp = nominees.Contains(deal.proposerId), recipientUp = nominees.Contains(deal.recipientId);
            if (proposerUp == recipientUp) return null;
            string up = proposerUp ? deal.proposerId : deal.recipientId;
            string voter = proposerUp ? deal.recipientId : deal.proposerId;
            var ballot = votes.FirstOrDefault(v => v.voterId == voter);
            if (ballot == null) return null;
            actorId = voter;
            return ballot.targetId == up ? DealStatus.Broken : DealStatus.Fulfilled;
        }

        /// <summary>
        /// A Head of Household's nominating done for the week, against a safety pact, under the
        /// commitment rules (ACTIONS-DEALS-ALLIANCES-PLAN C1): once the veto meeting has named any
        /// replacement, a safety pact of theirs whose partner is not on the block is kept - they were
        /// spared. A partner they nominated broke it at the ceremony or the replacement
        /// (<see cref="Nomination"/>). Without the rules a safety pact could only ever be broken.
        /// The caller says whether the partner is still in the house to have been spared.
        /// </summary>
        public static string Spared(DealState deal, string hohId, IReadOnlyList<string> block)
        {
            if (deal == null || deal.type != DealKind.SafetyAgreement || block == null) return null;
            string partner = Partner(deal, hohId);
            if (partner == null) return null;
            return block.Contains(partner) ? null : DealStatus.Fulfilled;
        }

        /// <summary>The final Head of Household choosing who to sit beside.</summary>
        public static string FinalSelection(DealState deal, string selectorId, string selectedId)
        {
            if (deal == null || deal.type != DealKind.FinalTwo) return null;
            string partner = Partner(deal, selectorId);
            if (partner == null) return null;
            return selectedId == partner ? DealStatus.Fulfilled : DealStatus.Broken;
        }

        // ---------------------------------------------------------------- the sweep

        /// <summary>
        /// Every binding deal this action decides, with who decided it.
        ///
        /// <para>Returned rather than applied, because applying is the engine's business: the ledger
        /// writes and the betrayal rolls belong to a committed command, and this file stays pure so
        /// the rules can be tested without a season around them.</para>
        /// </summary>
        public static List<Verdict> Verdicts(EpisodeState state, string action, string actorId,
            IReadOnlyList<string> nominees = null, string savedId = null, bool used = false,
            string selectedId = null, bool voteDeals = false)
        {
            var verdicts = new List<Verdict>();
            if (state == null) return verdicts;
            // The commitment rules (ACTIONS-DEALS-ALLIANCES-PLAN C1): every deal does something. A
            // partnership is judged at the vote, a safety pact is kept by being spared, a vote deal
            // both parties broke names neither, and the final choice judges no deal with somebody no
            // longer in the house (X4). A season without them is judged exactly as it always was.
            bool rules = EpisodeEngine.CommitmentRulesOn(state);

            foreach (var deal in state.deals.Where(d => d.status == DealStatus.Active).ToList())
            {
                string outcome = null;
                bool stands = false;
                // A voting block is decided by both of them at once, so neither is the one who
                // acted; a vote deal is kept or broken by whichever party voted; everything else
                // has somebody whose choice settled it.
                string decidedBy = action == Votes ? null : actorId;
                switch (action)
                {
                    case Nominates:
                        outcome = Nomination(deal, actorId, nominees);
                        break;
                    case Vetoes:
                        string partner = Partner(deal, actorId);
                        // The flag as it stood BEFORE the save, which is what the caller passes in
                        // the nominee list: a partner already lifted off the block by this very
                        // decision must still read as nominated here.
                        bool nominated = partner != null && nominees != null && nominees.Contains(partner);
                        outcome = Veto(deal, actorId, savedId, used, nominated);
                        break;
                    case Votes:
                        outcome = VoteTogether(deal, state.votes);
                        if (outcome == null && voteDeals) outcome = VoteDeal(deal, state.votes, state.nominees, out decidedBy, bothNameNobody: rules);
                        if (outcome == null && rules)
                        {
                            outcome = PartnershipAtTheVote(deal, state.votes, state.nominees, out decidedBy);
                            stands = outcome == DealStatus.Fulfilled;
                        }
                        break;
                    case Selects:
                        // A final two with somebody already gone is no choice the final Head of
                        // Household made: their deals ended as they left, and one that did not end
                        // (a season that took the rules on later) is passed over, not broken.
                        if (rules && !InTheHouse(state, Partner(deal, actorId))) break;
                        outcome = FinalSelection(deal, actorId, selectedId);
                        break;
                    case Spares:
                        // Only under the rules, and only a partner still in the house was spared - and
                        // not one put up this week: a pact struck after the ceremony nominated them,
                        // their seat then saved by the veto, was not kept by sparing them.
                        string spared = Partner(deal, actorId);
                        if (!rules || !InTheHouse(state, spared) || state.Find(spared).nominationWeeks.Contains(state.week)) break;
                        outcome = Spared(deal, actorId, nominees);
                        break;
                }
                if (outcome == null) continue;
                verdicts.Add(new Verdict { deal = deal, status = outcome, actorId = decidedBy, stands = stands });
            }
            return verdicts;
        }

        private static bool InTheHouse(EpisodeState state, string id) =>
            !string.IsNullOrEmpty(id) && state.Find(id)?.status == ContestantStatus.Active;

        /// <summary>
        /// The actions a verdict answers to. <see cref="Spares"/> is the commitment rules' own: the
        /// Head of Household's nominating over for the week, the veto meeting's replacement named.
        /// </summary>
        public const string Nominates = "nominate", Vetoes = "veto", Votes = "vote", Selects = "final-selection", Spares = "spare";

        /// <summary>One deal, how it ended, and whose doing that was.</summary>
        public sealed class Verdict
        {
            public DealState deal;
            public string status;

            /// <summary>Whoever's choice settled it, or null where both parties settled it at once.</summary>
            public string actorId;

            /// <summary>
            /// Kept, and it stands: a partnership kept at a vote under the commitment rules (C1). Nothing
            /// is settled - the deal stays active, its status and record untouched - and the keep is a
            /// small kept record (<c>EpisodeEngine.SettleDeals</c>). False for every other verdict.
            /// </summary>
            public bool stands;
        }
    }
}
