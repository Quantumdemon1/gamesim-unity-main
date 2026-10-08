using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The Vote family's storage writer under the prospective mode 2 (unifiedCommitmentRulesVersion 2),
    /// and the creators' own reads of what already stands. Callable only from simulation command owners
    /// running inside a command's detached candidate; public mode 2 stays refused, so only the internal
    /// engine seam (<see cref="EpisodeEngine.ProspectiveVote"/>) reaches a mode-2 write.
    ///
    /// <para>Every write goes through <see cref="UnifiedVoteAdmission"/>, which requires the complete
    /// prospective core on the state it is given and the source owner's own prerequisites, duplicates
    /// and capacities, and returns detached rows. Nothing here spends, rolls, mints an id, logs, settles
    /// or publishes an effect: the command owner keeps all of those, in their source order. A refusal
    /// leaves the state as it was. Under modes 0 and 1 nothing here writes, and every view returns
    /// exactly what the expression it replaced returned, so recorded seasons read, draw and mint as before.</para>
    ///
    /// <para>A write's refusal is the draft's - a duplicate, a full shelf, a prerequisite - which its owner
    /// answers as its source does, by refusing the command or skipping the row; or it is the state's, one
    /// the complete core already refuses in the middle of the command. That one is never the owner's to
    /// skip past: it is thrown as the whole command's refusal (<see cref="Refused"/>), so a failing state
    /// cannot silently drop a house pass's or a story's row.</para>
    ///
    /// <para>Settlement, the reveal's archive, endings and the readers are not this writer's: under mode 2
    /// a written row stays as written until those slices land.</para>
    /// </summary>
    internal static class UnifiedVoteStore
    {
        /// <summary>Whether the season runs the prospective Vote authority: exactly mode 2, no other value.</summary>
        internal static bool On(EpisodeState s) => s != null && s.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version;

        internal static bool IsVote(string dealType) => UnifiedVoteFamilyValidation.IsVoteType(dealType);

        // ---------------------------------------------------------------- what already stands

        /// <summary>Every promise a creator's gate reads: in mode 2 the raw rows and the canonical ones; otherwise as before.</summary>
        internal static IReadOnlyList<PromiseState> Promises(EpisodeState s) =>
            On(s) ? UnifiedVoteReferences.PromisesUnchecked(s) : UnifiedCommitments.RulesOn(s) ? CommitmentReferences.Promises(s) : s.promises;

        /// <summary>Every deal a creator's gate reads: in mode 2 the raw rows and the canonical ones; otherwise as before.</summary>
        internal static IReadOnlyList<DealState> Deals(EpisodeState s) =>
            On(s) ? UnifiedVoteReferences.DealsUnchecked(s) : UnifiedCommitments.RulesOn(s) ? CommitmentReferences.Deals(s) : s.deals;

        /// <summary>For a gate that read only the raw promises: in mode 2 the canonical ones too; otherwise the raw list itself.</summary>
        internal static IReadOnlyList<PromiseState> RawOrModeTwoPromises(EpisodeState s) =>
            On(s) ? UnifiedVoteReferences.PromisesUnchecked(s) : s.promises;

        /// <summary>For a gate that read only the raw deals: in mode 2 the canonical ones too; otherwise the raw list itself.</summary>
        internal static IReadOnlyList<DealState> RawOrModeTwoDeals(EpisodeState s) =>
            On(s) ? UnifiedVoteReferences.DealsUnchecked(s) : s.deals;

        /// <summary>The promise-policy rows that count toward the 200: all history, raw and canonical.</summary>
        internal static int PromiseCount(EpisodeState s) => On(s)
            ? s.promises.Count + s.unifiedCommitments.Count(row => row.sourcePolicy == UnifiedCommitments.PromisePolicy)
            : UnifiedCommitments.RulesOn(s) ? CommitmentReferences.PromiseCount(s) : s.promises.Count;

        /// <summary>The deal-policy rows that count toward the 200 and the player's 40: all history, raw and canonical.</summary>
        internal static int DealCount(EpisodeState s) => On(s)
            ? s.deals.Count + s.unifiedCommitments.Count(row => row.sourcePolicy == UnifiedCommitments.DealPolicy)
            : UnifiedCommitments.RulesOn(s) ? CommitmentReferences.DealCount(s) : s.deals.Count;

        // ---------------------------------------------------------------- the writes

        /// <summary>Reservation only: whether this promise draft would be admitted now. Writes nothing.</summary>
        internal static bool CanAddPromise(EpisodeState s, PromiseState draft, string origin, out string error) =>
            UnifiedVoteAdmission.TryPromise(s, draft, origin, out _, out error);

        /// <summary>Admits the promise draft and appends its canonical row, or refuses and writes nothing.</summary>
        internal static bool TryAddPromise(EpisodeState s, PromiseState draft, string origin, out string error)
        {
            if (!UnifiedVoteAdmission.TryPromise(s, draft, origin, out var row, out error)) return Refused(s);
            s.unifiedCommitments.Add(row);
            return true;
        }

        /// <summary>Reservation only: whether this immediate or proposed deal draft would be admitted now. Writes nothing.</summary>
        internal static bool CanAddDeal(EpisodeState s, DealState draft, string origin, out string error) =>
            UnifiedVoteAdmission.TryDeal(s, draft, origin, out _, out error);

        /// <summary>Admits the deal draft and appends its canonical row, or refuses and writes nothing.</summary>
        internal static bool TryAddDeal(EpisodeState s, DealState draft, string origin, out string error)
        {
            if (!UnifiedVoteAdmission.TryDeal(s, draft, origin, out var row, out error)) return Refused(s);
            s.unifiedCommitments.Add(row);
            return true;
        }

        /// <summary>
        /// The player's answer to a genuine still-pending canonical NPC offer. A yes binds it now - its
        /// creation identity and week kept, its term and binding the answering week, its first reveal the
        /// owner's floor; a no changes only its consent. Anything else is refused and nothing changes.
        /// </summary>
        internal static bool TryAnswerOffer(EpisodeState s, string id, bool accept, out string error)
        {
            int index = s.unifiedCommitments.FindIndex(row => row.id == id);
            if (accept)
            {
                if (!UnifiedVoteAdmission.TryAnswerOffer(s, id, out var answered, out error)) return Refused(s);
                s.unifiedCommitments[index] = answered;
                return true;
            }
            if (!EpisodeValidation.TryValidateProspectiveUnifiedVote(s, out error)) return Refused(s);
            var pending = index < 0 ? null : s.unifiedCommitments[index];
            if (pending == null || pending.kind != UnifiedVoteTogether.Vote || pending.origin != UnifiedCommitments.NpcOffer
                || pending.status != DealStatus.Proposed || pending.beneficiaryId != s.playerId
                || s.Find(pending.makerId)?.status != ContestantStatus.Active || s.Find(s.playerId)?.status != ContestantStatus.Active)
                return Refuse(out error, "Answer the genuine still-pending offer between its actual active parties.");
            var declined = pending.Clone();
            declined.status = DealStatus.Declined;
            var candidate = s.Clone();
            candidate.unifiedCommitments[index] = declined.Clone();
            if (!EpisodeValidation.TryValidateProspectiveUnifiedVote(candidate, out error)) return Refused(s);
            s.unifiedCommitments[index] = declined;
            return true;
        }

        /// <summary>
        /// An accepted counter's two deals, reserved and written together: each Vote or Safety member as a
        /// canonical row, any other member raw, both or neither. The command still re-derives the counter
        /// and owns its consent, keyed draw, ids and lines.
        /// </summary>
        internal static bool TryAddCounter(EpisodeState s, DealState bought, DealState price, out string error)
        {
            if (!UnifiedVoteAdmission.TryCounterBundle(s, bought, price, out var plan, out error)) return Refused(s);
            Install(s, plan);
            return true;
        }

        /// <summary>Reservation only: the player's own veto for a Vote price, both deals at once. Writes nothing.</summary>
        internal static bool CanAddOwnVetoPrice(EpisodeState s, DealState veto, DealState price, out string error) =>
            UnifiedVoteAdmission.TryOwnVetoPrice(s, veto, price, out _, out error);

        /// <summary>The player's own veto (raw) and the nominee's Vote price for it (canonical), both or neither.</summary>
        internal static bool TryAddOwnVetoPrice(EpisodeState s, DealState veto, DealState price, out string error)
        {
            if (!UnifiedVoteAdmission.TryOwnVetoPrice(s, veto, price, out var plan, out error)) return Refused(s);
            Install(s, plan);
            return true;
        }

        /// <summary>
        /// Reservation only, on the validated state before the player's yes moves anything: the nominee's
        /// still-pending veto ask and the Vote price it carries. Writes nothing.
        /// </summary>
        internal static bool CanAddAskPrice(EpisodeState s, string askId, DealState price, out string error) =>
            UnifiedVoteAdmission.TryAcceptedVetoAskPrice(s, askId, price, out _, out error);

        /// <summary>
        /// The accepted ask's Vote price, written where the source strikes it: after the yes, its warmth,
        /// memory and line, with the sequence the season has reached by then. <see cref="CanAddAskPrice"/>
        /// reserved it before the yes moved anything; the struck row is admitted again as it is written
        /// (<see cref="UnifiedVoteAdmission.TryStruckAskPrice"/>), its answered ask linked to it. The ask
        /// itself stays raw and names its price, as the source links it.
        /// </summary>
        internal static bool TryInstallAskPrice(EpisodeState s, DealState ask, DealState price, out string error)
        {
            error = null;
            if (!On(s) || ask == null || !s.deals.Contains(ask))
                return Refuse(out error, "Strike only the Vote price of the veto ask this command answered.");
            if (!UnifiedVoteAdmission.TryStruckAskPrice(s, ask, price, out var row, out error)) return Refused(s);
            s.unifiedCommitments.Add(row);
            ask.linkedDealId = price.id;
            return true;
        }

        // Every row of the plan is detached and already admitted together; nothing between these
        // appends can refuse, so the pair is written whole, in the source's own order.
        private static void Install(EpisodeState s, UnifiedVoteAdmissionPlan plan)
        {
            s.unifiedCommitments.AddRange(plan.CanonicalAdditions.Select(row => row.Clone()));
            s.deals.AddRange(plan.RawAdditions.Select(row => row.Clone()));
        }

        /// <summary>
        /// A write's refusal, classified (vote family V3's review): the draft's is returned for its owner to
        /// answer; the state's - the complete core already refuses the state the write was given - is thrown as
        /// the whole command's refusal, naming the core's reason. Only a refusal pays for the second check.
        /// </summary>
        private static bool Refused(EpisodeState s)
        {
            if (!EpisodeValidation.TryValidateProspectiveUnifiedVote(s, out var stateError))
                throw EpisodeEngine.Refusal("The prospective Vote state fails its core in the middle of the command: " + stateError);
            return false;
        }

        private static bool Refuse(out string error, string reason) { error = reason; return false; }
    }
}
