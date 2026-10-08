using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The Vote family's settlement and endings under the prospective mode 2 (vote family V4): the regular reveal
    /// decides the canonical Vote rows, runs their source effects once under the Rule2 plan
    /// (<see cref="UnifiedVoteSettlement"/>) and publishes its archive frame; the week's turn, the house's deal
    /// pass, a departure, a production removal and a voided price end them. Only mode 2 reaches anything here -
    /// every entry returns at once otherwise - and mode 2 is still reached only through the internal engine seam
    /// (<see cref="ProspectiveVote"/>), so recorded seasons draw, mint and write exactly as before.
    ///
    /// <para>Everything here runs inside a command's detached candidate, sometimes at a moment the source leaves
    /// unfinished (the reveal between its verdicts and its record, the final eviction before its departure
    /// ending). So nothing here validates the whole state mid-step: the reveal checks the family once before it
    /// decides anything and once as it publishes, and the command's candidate is held to the complete core.</para>
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        // ---------------------------------------------------------------- the reveal

        /// <summary>
        /// The reveal's plan, before any verdict moves anything: the box complete, the eviction decided. A state
        /// the family core refuses here is the whole command's refusal, never a reveal that settles part of it.
        /// </summary>
        private static UnifiedVoteRevealPlan BeginUnifiedVoteReveal(EpisodeState s)
        {
            if (!UnifiedVoteFamilyValidation.TryValidate(s, s.unifiedVoteReveals, out string error))
                throw new RuleException("The prospective Vote state fails its core as the reveal settles: " + error);
            try { return UnifiedVoteSettlement.Plan(s); }
            catch (ArgumentException malformed) { throw new RuleException("The reveal cannot judge a Vote row: " + malformed.Message); }
        }

        /// <summary>
        /// The promise lane at one ballot: the voter's decided promises, in the order the source settles them, each
        /// stamped, and each that owns a consequence carrying its own recipe. Runs where the source settles the
        /// voter's raw promises, before that ballot's oath plan.
        /// </summary>
        private static void SettleUnifiedVotePromises(EpisodeState s, UnifiedVoteRevealPlan plan, string voterId)
        {
            foreach (var verdict in plan.PromisesOf(voterId).ToList())
            {
                StampUnifiedVote(s, plan, verdict);
                if (verdict.Owns) UnifiedVotePromiseRecipe(s, verdict);
            }
        }

        /// <summary>
        /// The deal lane: the raw verdicts (a partnership the vote tests) and the decided canonical deals, met in the
        /// one order mode 1's single list met them in (<see cref="UnifiedVoteSettlement.Occurrence"/>). A raw verdict
        /// settles as it always has; a canonical deal is stamped and, if it owns a consequence, runs its recipe.
        /// </summary>
        private static void SettleUnifiedVoteDeals(EpisodeState s, UnifiedVoteRevealPlan plan, List<DealResolution.Verdict> raw)
        {
            var lane = raw.Select(verdict => (key: UnifiedVoteSettlement.Occurrence(verdict.deal.id, false, s.deals.IndexOf(verdict.deal)),
                    raw: verdict, canonical: (UnifiedVoteVerdict)null))
                .Concat(plan.Deals.Select(verdict => (key: UnifiedVoteSettlement.Occurrence(verdict.Row.id, true,
                    s.unifiedCommitments.FindIndex(row => row.id == verdict.Row.id)), raw: (DealResolution.Verdict)null, canonical: verdict)))
                .OrderBy(item => item.key).ToList();
            foreach (var item in lane)
            {
                if (item.raw != null) { SettleDeals(s, new List<DealResolution.Verdict> { item.raw }); continue; }
                StampUnifiedVote(s, plan, item.canonical);
                if (!item.canonical.Owns) continue;
                if (item.canonical.Collective) UnifiedVoteCollectiveRecipe(s, item.canonical);
                else UnifiedVoteNamedRecipe(s, item.canonical);
            }
        }

        /// <summary>
        /// After the reveal's record (RecordReveal): every decided row stamped exactly once, the current frame
        /// projected from the actual private box and appended, and the family core holding the result - the
        /// rows' first deciding frame now the one published with them. Nothing is published early.
        /// </summary>
        private static void PublishUnifiedVoteReveal(EpisodeState s, UnifiedVoteRevealPlan plan)
        {
            Require(plan.Week == s.week && plan.Stamped.Count == plan.Verdicts.Count
                && plan.Verdicts.All(verdict => plan.Stamped.Contains(verdict.Row.id)), "The reveal left a decided Vote row unsettled.");
            if (!UnifiedVoteRevealArchive.TryProjectCurrent(s, s.unifiedVoteReveals, out var archive, out string error))
                throw new RuleException("The reveal cannot publish its frame: " + error);
            s.unifiedVoteReveals = archive;
            if (!UnifiedVoteFamilyValidation.TryValidate(s, s.unifiedVoteReveals, out error))
                throw new RuleException("The reveal's settlement does not hold: " + error);
        }

        /// <summary>
        /// The decided row's terminal stamp, at its occurrence: its verdict, this week, the breaker the source names
        /// (none for a keeper, none for a collective breach) and a breach's identity. Every decided row is stamped,
        /// owner or not: a row that owns no consequence is still its own terminal receipt.
        /// </summary>
        private static void StampUnifiedVote(EpisodeState s, UnifiedVoteRevealPlan plan, UnifiedVoteVerdict verdict)
        {
            int index = s.unifiedCommitments.FindIndex(row => row.id == verdict.Row.id);
            Require(index >= 0 && s.unifiedCommitments[index].status == DealStatus.Active && plan.Stamped.Add(verdict.Row.id),
                "A decided Vote row must still be active, and is settled once.");
            var settled = s.unifiedCommitments[index].Clone();
            bool broken = verdict.Status == DealStatus.Broken;
            settled.status = verdict.Status;
            settled.settledWeek = s.week;
            settled.brokenById = broken ? verdict.ActorId : null;
            settled.settlementEffectKey = broken ? UnifiedVoteHistory.Key(settled, s.week) : null;
            s.unifiedCommitments[index] = settled;
        }

        // ---------------------------------------------------------------- the source recipes

        /// <summary>
        /// A decided Vote promise, as <see cref="SettlePromise"/> settles it under the commitment rules - the
        /// beneficiary's view of the maker, their permanent one-way record, both memories, the maker's line, and
        /// for a breach the Story hook and the witness loop - writing only the atoms the plan selected.
        /// </summary>
        private static void UnifiedVotePromiseRecipe(EpisodeState s, UnifiedVoteVerdict verdict)
        {
            var row = verdict.Row;
            bool broken = !verdict.Kept;
            double delta = verdict.Nominal;
            if (verdict.Scores[0].Selected) WriteScore(s, row.beneficiaryId, row.makerId, delta);
            string text = Name(s, row.makerId) + (broken ? " broke" : " fulfilled") + " a " + PromiseKind.Vote + " promise.";
            if (verdict.Ledger[0].Selected)
                RelationshipLedger.RecordOneWay(s, row.beneficiaryId, row.makerId, broken ? "promise-broken" : "promise-kept", delta, text, permanent: true);
            Remember(s, row.beneficiaryId, row.makerId, text, true); Remember(s, row.makerId, row.beneficiaryId, text, true);
            // A vote promise's outcome is the promiser's ballot: the line goes to them alone.
            Log(s, "promise-outcome", text, row.makerId);
            if (!broken) return;
            StoryWordBroken(s, row.beneficiaryId, row.makerId, GrudgeCauses.PromiseBroken, 60);
            foreach (var witness in s.Active.Where(c => c.id != row.makerId && c.id != row.beneficiaryId))
            {
                double chance = s.Allied(witness.id, row.beneficiaryId) ? 0.8 : s.Score(witness.id, row.beneficiaryId) > 50 ? 0.6
                    : s.Score(witness.id, row.makerId) > 50 ? 0.3 : 0.2;
                if (Roll(s) >= chance) continue;
                Remember(s, witness.id, row.makerId, text, true); WriteScore(s, witness.id, row.makerId, WebRules.JsRound(delta * 0.4));
            }
        }

        /// <summary>
        /// A decided vote deal with a sole keeper or breaker, as <see cref="SettleDeals"/> settles a ballot's verdict
        /// under the commitment rules - the partner's view of the actor (never the player's own), the record, the
        /// partner's memory, the actor's line, and for a breach the Story hook (not on the player wronged by a ballot)
        /// and the betrayal spread - writing only the atoms the plan selected. A ballot's breach voids no price.
        /// </summary>
        private static void UnifiedVoteNamedRecipe(EpisodeState s, UnifiedVoteVerdict verdict)
        {
            var deal = verdict.Deal();
            string actor = verdict.ActorId, wronged = DealResolution.Partner(deal, actor);
            bool kept = verdict.Kept;
            double delta = verdict.Nominal;
            string title = DealKind.Title(deal.type).ToLowerInvariant();
            bool keepPlayersView = CommitmentRulesOn(s) && KnownBallots.SettledByABallot(deal);
            string text = Name(s, actor) + (kept ? " honoured a " : " broke a ") + title + " with " + Name(s, wronged) + ".";
            if (verdict.Scores[0].Selected && !(keepPlayersView && wronged == s.playerId)) WriteScore(s, wronged, actor, delta);
            WriteUnifiedVoteLedger(s, verdict, kept ? "deal_fulfilled" : "deal_broken", text, permanent: !kept);
            Remember(s, wronged, actor, text, true);
            Log(s, "deal-outcome", text, actor);
            if (kept) return;
            if (!(keepPlayersView && wronged == s.playerId))
                StoryWordBroken(s, wronged, actor, GrudgeCauses.DealBroken, 60);
            SpreadBetrayal(s, deal, actor);
        }

        /// <summary>
        /// A decided vote deal both parties decided at once, as <see cref="SettleDeals"/> settles it: both views (never
        /// the player's own), both directions of the record, the pair's line without the player. No memory, Story
        /// hook or spread: nobody in particular acted. Only the atoms the plan selected are written.
        /// </summary>
        private static void UnifiedVoteCollectiveRecipe(EpisodeState s, UnifiedVoteVerdict verdict)
        {
            var deal = verdict.Deal();
            bool kept = verdict.Kept;
            double delta = verdict.Nominal;
            string title = DealKind.Title(deal.type).ToLowerInvariant();
            bool keepPlayersView = CommitmentRulesOn(s) && KnownBallots.SettledByABallot(deal);
            string text = Name(s, deal.proposerId) + " and " + Name(s, deal.recipientId)
                + (kept ? " held to their " : " fell out over their ") + title + ".";
            if (verdict.Scores[0].Selected && !(keepPlayersView && deal.proposerId == s.playerId)) WriteScore(s, deal.proposerId, deal.recipientId, delta);
            if (verdict.Scores[1].Selected && !(keepPlayersView && deal.recipientId == s.playerId)) WriteScore(s, deal.recipientId, deal.proposerId, delta);
            WriteUnifiedVoteLedger(s, verdict, kept ? "deal_fulfilled" : "deal_broken", text, permanent: !kept);
            var pair = new[] { deal.proposerId, deal.recipientId };
            Log(s, "deal-outcome", text, keepPlayersView ? pair.Where(id => id != s.playerId).ToArray() : pair);
        }

        /// <summary>The selected directions of a deal's record, in the source's order: RelationshipLedger.Record's two, or RecordBreach's.</summary>
        private static void WriteUnifiedVoteLedger(EpisodeState s, UnifiedVoteVerdict verdict, string type, string text, bool permanent)
        {
            foreach (var atom in verdict.Ledger.Where(atom => atom.Selected))
                RelationshipLedger.RecordOneWay(s, atom.HolderId, atom.AboutId, type, atom.Delta, text, permanent);
        }

        // ---------------------------------------------------------------- the endings

        /// <summary>
        /// The Vote family's own endings, at the source's boundaries, under mode 2 only: a promise's week turns
        /// (an active promise whose week has passed), the house's deal pass (a deal still binding past its week),
        /// a departure (every deal still binding its evictee as a party or its target - a vote promise waits for
        /// its own week's turn, as the source's does), and a production removal (every active promise of theirs,
        /// every deal still binding them). An ending is no verdict: no view, record, line or draw - the raw loops'
        /// own rule, which run beside this for the families still raw.
        /// </summary>
        internal static void ResolveUnifiedVoteExpiry(EpisodeState s, UnifiedCommitmentExpiry boundary, string departedId = null)
        {
            if (!UnifiedVoteStore.On(s)) return;
            for (int index = 0; index < s.unifiedCommitments.Count; index++)
            {
                var row = s.unifiedCommitments[index];
                if (row == null || row.kind != UnifiedVoteTogether.Vote) continue;
                bool promise = row.sourcePolicy == UnifiedCommitments.PromisePolicy;
                bool party = departedId != null && (row.makerId == departedId || row.beneficiaryId == departedId);
                bool ends;
                switch (boundary)
                {
                    case UnifiedCommitmentExpiry.PromiseWeekTurn:
                        ends = promise && row.status == DealStatus.Active && row.expiresWeek > 0 && row.expiresWeek < s.week; break;
                    case UnifiedCommitmentExpiry.DealPass:
                        ends = !promise && DealStatus.Binds(row.status) && row.expiresWeek > 0 && row.expiresWeek < s.week; break;
                    case UnifiedCommitmentExpiry.Departure:
                        ends = !promise && DealStatus.Binds(row.status) && (party || departedId != null && row.targetId == departedId); break;
                    case UnifiedCommitmentExpiry.Expulsion:
                        ends = promise ? row.status == DealStatus.Active && party
                            : DealStatus.Binds(row.status) && (party || departedId != null && row.targetId == departedId); break;
                    default: throw new ArgumentOutOfRangeException(nameof(boundary));
                }
                if (!ends) continue;
                var ended = row.Clone();
                ended.status = DealStatus.Expired;
                s.unifiedCommitments[index] = ended;
            }
        }

        /// <summary>
        /// A canonical Vote price voided (<see cref="VoidThePrice"/>): the one it was owed to broke what it bought, by
        /// an act. Its own row lapses - nobody broke it, so no view, record or draw - and the caller says the line.
        /// False, writing nothing, when the price is not a canonical Vote row that still binds, linked to that bought
        /// deal and owed to that breaker.
        /// </summary>
        private static bool VoidUnifiedVotePrice(EpisodeState s, string priceId, string boughtId, string breakerId)
        {
            int index = s.unifiedCommitments.FindIndex(row => row.id == priceId);
            var price = index < 0 ? null : s.unifiedCommitments[index];
            if (price == null || price.kind != UnifiedVoteTogether.Vote || price.sourcePolicy != UnifiedCommitments.DealPolicy
                || !DealStatus.Binds(price.status) || price.linkedCommitmentId != boughtId || price.beneficiaryId != breakerId)
                return false;
            var voided = price.Clone();
            voided.status = DealStatus.Expired;
            s.unifiedCommitments[index] = voided;
            return true;
        }
    }
}
