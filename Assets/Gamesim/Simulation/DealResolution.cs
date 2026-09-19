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
            string selectedId = null)
        {
            var verdicts = new List<Verdict>();
            if (state == null) return verdicts;

            foreach (var deal in state.deals.Where(d => d.status == DealStatus.Active).ToList())
            {
                string outcome = null;
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
                        break;
                    case Selects:
                        outcome = FinalSelection(deal, actorId, selectedId);
                        break;
                }
                if (outcome == null) continue;
                verdicts.Add(new Verdict
                {
                    deal = deal, status = outcome,
                    // A voting block is decided by both of them at once, so neither is the one who
                    // acted; everything else has somebody whose choice settled it.
                    actorId = action == Votes ? null : actorId,
                });
            }
            return verdicts;
        }

        public const string Nominates = "nominate", Vetoes = "veto", Votes = "vote", Selects = "final-selection";

        /// <summary>One deal, how it ended, and whose doing that was.</summary>
        public sealed class Verdict
        {
            public DealState deal;
            public string status;

            /// <summary>Whoever's choice settled it, or null where both parties settled it at once.</summary>
            public string actorId;
        }
    }
}
