using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Aggregate read-only validation of the Vote family under mode 2, the unified vote rules. This is a leaf
    /// of the whole-episode core, which public and save validation dispatch a mode-2 season to since vote family
    /// V6; it is not historical command proof. Its aggregate leaf never changes authority, installs an archive or
    /// calls whole validation. Separate draft entries require the complete core before cloning unrelated state.
    /// <para>The save contract freezes here at schema 28 (the lead's decision D2): whoever bumps the schema to 29
    /// must freeze this whole mode-2 core into FrozenEpisodeV28, as players' schema-28 saves hold mode 2.</para>
    /// </summary>
    public static class UnifiedVoteFamilyValidation
    {
        public const int Version = 2;
        public const string VoteLobby = "vote-lobby", VetoAskPrice = "veto-ask-price", OwnVetoPrice = "own-veto-price";

        /// <summary>
        /// The live episode schema: the one a new <see cref="EpisodeState"/> is written at, which
        /// whole-episode validation also demands. The aggregate is a live-state leaf with no saved
        /// field of its own, so it reads the schema from the DTO instead of restating it as a
        /// literal: a schema bump carries this leaf with it, where a literal here would make every
        /// state of the bumped schema fail the aggregate and every mode 2 reader throw.
        /// </summary>
        private static readonly int LiveSchema = new EpisodeState().schemaVersion;

        public static bool TryValidate(EpisodeState s, IReadOnlyList<UnifiedVoteRevealState> archive, out string error)
            => TryValidateCore(s, archive, null, out error);

        /// <summary>
        /// The readers' check of mode 2's commitment storage (vote family V5a): what a reader needs to read the rows
        /// at all, and nothing that a command in its middle may not yet have made true. The Safety authority's own
        /// storage check (<see cref="UnifiedCommitments.ValidateSafetyAuthority"/>: every Safety row under its policy,
        /// no raw Safety mirror, identities unique across all three lists, the two 200-row capacities); no raw Vote
        /// mirror; and each Vote row's shape - its family, policy and origin, a deal's Vote subtype, a known installed
        /// status a promise can be read at, its source's trust weight (pre-V6 save-contract review), bounded
        /// identities, parties and a target that exist, and a link that resolves to a deal the season holds.
        /// <para>Never frames, ballots, endings, chronology or terminal decisions: those are the complete core's
        /// (<see cref="TryValidate"/>), which judges a command's candidate, a save and the reveal, never a reader in
        /// the middle of a command. Reads only the commitment lists and the cast.</para>
        /// </summary>
        public static bool TryValidateStorage(EpisodeState s, out string error)
        {
            if (s == null || s.unifiedCommitmentRulesVersion != Version)
                return Fail(out error, "Expected the explicit prospective Vote storage.");
            if (!UnifiedCommitments.ValidateSafetyAuthority(s, out error)) return false;
            if (s.promises.Any(row => row.kind == PromiseKind.Vote) || s.deals.Any(row => IsVoteType(row.type)))
                return Fail(out error, "Prospective Safety/Vote authority cannot have writable raw mirrors.");
            HashSet<string> deals = null;
            foreach (var row in s.unifiedCommitments)
            {
                if (row.kind == UnifiedCommitments.Safety) continue;
                bool promise = row.sourcePolicy == UnifiedCommitments.PromisePolicy;
                if (row.kind != UnifiedVoteTogether.Vote || !promise && row.sourcePolicy != UnifiedCommitments.DealPolicy
                    || (promise ? row.subtype != null || !IsPromiseOrigin(row.origin) : !IsVoteType(row.subtype) || !IsDealOrigin(row.origin))
                    || !DealStatus.IsKnown(row.status) || row.status == DealStatus.Accepted
                    || promise && (row.status == DealStatus.Proposed || row.status == DealStatus.Declined))
                    return Fail(out error, "A stored Vote row needs its family, policy, origin, subtype and an installed status.");
                // The weight every reader of a broken or kept row prices it by (DealResolution.Impact, a promise's
                // native impact): a promise's is medium and a deal's its subtype's default, as the core holds them.
                if (row.trustImpact != (promise ? DealTrust.Medium : DealKind.DefaultTrust(row.subtype)))
                    return Fail(out error, "A stored Vote row keeps its source's trust weight.");
                if (!Token(row.makerId) || !Token(row.beneficiaryId) || row.makerId == row.beneficiaryId
                    || s.Find(row.makerId) == null || s.Find(row.beneficiaryId) == null
                    || row.targetId != null && (!Token(row.targetId) || s.Find(row.targetId) == null))
                    return Fail(out error, "A stored Vote row's parties and target must be houseguests of this season.");
                // Whether it names a target at all, as the core holds it (the pre-V6 save-contract review): a story's word on the
                // vote and a bloc name none, every other word and deal one - the target every reader compares a ballot with.
                if ((row.targetId == null) != (promise ? row.origin == UnifiedCommitments.StoryPromise : row.subtype == DealKind.VoteTogether))
                    return Fail(out error, "A stored Vote row names a target exactly where its source does.");
                if (row.linkedCommitmentId == null) continue;
                deals ??= new HashSet<string>(s.deals.Select(item => item.id)
                    .Concat(s.unifiedCommitments.Where(item => item.sourcePolicy == UnifiedCommitments.DealPolicy).Select(item => item.id)),
                    StringComparer.Ordinal);
                if (promise || row.linkedCommitmentId == row.id || !deals.Contains(row.linkedCommitmentId))
                    return Fail(out error, "A stored Vote deal's link must resolve to another deal of this season.");
            }
            return true;
        }

        // A draft may use the current sequence, but no stored row may. The exception is an
        // explicit, bounded set of additions on a detached candidate, never a staging/save gate.
        internal static bool TryValidateDraftBundle(EpisodeState s, IReadOnlyList<UnifiedCommitmentState> additions,
            IReadOnlyList<DealState> rawAdditions, DealState rawReplacement, out string error)
            => EpisodeValidation.TryValidateProspectiveUnifiedVote(s, out error)
                && ValidateDraftBundle(s, additions, rawAdditions, rawReplacement, false, out error);

        // The admission's own entries (UnifiedVoteAdmission, vote family V4). Each is called straight after the
        // admission has held this very state to the complete prospective core (UnifiedVoteAdmission.Ready), with
        // nothing changed between, so the state is not validated a second time: an admitted write paid for two
        // whole-state validations. The candidate is still judged by the family's complete aggregate.
        internal static bool TryValidateAdmittedDraftBundle(EpisodeState s, IReadOnlyList<UnifiedCommitmentState> additions,
            IReadOnlyList<DealState> rawAdditions, DealState rawReplacement, out string error)
            => ValidateDraftBundle(s, additions, rawAdditions, rawReplacement, false, out error);

        internal static bool TryValidateAdmittedCounterDraftBundle(EpisodeState s, IReadOnlyList<UnifiedCommitmentState> additions,
            IReadOnlyList<DealState> rawAdditions, out string error)
            => ValidateDraftBundle(s, additions, rawAdditions, null, true, out error);

        private static bool ValidateDraftBundle(EpisodeState s, IReadOnlyList<UnifiedCommitmentState> additions,
            IReadOnlyList<DealState> rawAdditions, DealState rawReplacement, bool counter, out string error)
        {
            error = null;
            if (additions == null || rawAdditions == null || additions.Count < 1 || additions.Count > 2
                || rawAdditions.Count > 1 || additions.Count + rawAdditions.Count > 2 || s.nextSequence >= 1000000
                || additions.Any(row => row == null) || rawAdditions.Any(row => row == null))
                return Fail(out error, "A new authoring reservation must be one row or one bounded atomic pair.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in additions)
                if (!ids.Add(row.id ?? "") || row.createdWeek != s.week
                    || (row.status != DealStatus.Active && row.status != DealStatus.Proposed)
                    || !InstalledId(row.id, Prefix(row.origin), s.nextSequence + 1, out long sequence) || sequence != s.nextSequence)
                    return Fail(out error, "A detached new row must use its actual current authoring sequence.");
            foreach (var row in rawAdditions)
                if (!Token(row.id) || !ids.Add(row.id) || row.week != s.week || row.status != DealStatus.Active
                    || !InstalledId(row.id, counter ? (row.id.StartsWith(Negotiation.PricePrefix, StringComparison.Ordinal)
                        ? Negotiation.PricePrefix : Negotiation.CounterDealPrefix) : "deal-player-", s.nextSequence + 1, out long sequence)
                    || sequence != s.nextSequence)
                    return Fail(out error, "Only the genuine raw bought own-veto row belongs to this atomic draft.");
            var candidate = s.Clone();
            if (rawReplacement != null)
            {
                var index = candidate.deals.FindIndex(row => row.id == rawReplacement.id);
                var original = index < 0 ? null : candidate.deals[index];
                if (original == null || original.type != DealKind.VetoUse || original.status != DealStatus.Proposed
                    || rawReplacement.id != original.id || rawReplacement.type != original.type
                    || rawReplacement.week != original.week || rawReplacement.proposerId != original.proposerId
                    || rawReplacement.recipientId != original.recipientId || rawReplacement.targetId != original.targetId
                    || rawReplacement.trustImpact != original.trustImpact || rawReplacement.brokenById != original.brokenById
                    || rawReplacement.settledWeek != original.settledWeek || original.linkedDealId != null
                    || rawReplacement.status != DealStatus.Active || rawReplacement.expiresWeek != s.week
                    || rawReplacement.linkedDealId == null)
                    return Fail(out error, "An accepted ask changes only its actual answer status, current term and reciprocal price link.");
                candidate.deals[index] = rawReplacement.Clone();
            }
            candidate.unifiedCommitments.AddRange(additions.Select(row => row.Clone()));
            candidate.deals.AddRange(rawAdditions.Select(row => row.Clone()));
            return TryValidateCore(candidate, candidate.unifiedVoteReveals, ids, out error);
        }

        /// <summary>
        /// The accepted veto ask's Vote price as the source strikes it: after the command's yes has answered
        /// the ask (Active, this week's term, not yet linked), with the sequence the season has reached by then.
        /// The detached candidate links the ask to the row and admits the row as a draft, judged by the same
        /// complete aggregate a reservation's draft bundle is. An admission entry: its caller has just held the
        /// state to the complete core (UnifiedVoteAdmission.TryStruckAskPrice).
        /// </summary>
        internal static bool TryValidateAdmittedStruckAskPrice(EpisodeState s, string askId, UnifiedCommitmentState row, out string error)
        {
            error = null;
            int index = s.deals.FindIndex(item => item.id == askId);
            var ask = index < 0 ? null : s.deals[index];
            if (ask == null || ask.type != DealKind.VetoUse || ask.status != DealStatus.Active || ask.linkedDealId != null
                || ask.expiresWeek != s.week || ask.recipientId != s.playerId
                || row == null || row.origin != VetoAskPrice || row.linkedCommitmentId != askId || row.makerId != ask.proposerId
                || row.createdWeek != s.week || row.status != DealStatus.Active || s.nextSequence >= 1000000
                || !InstalledId(row.id, Prefix(row.origin), s.nextSequence + 1, out long sequence) || sequence != s.nextSequence)
                return Fail(out error, "Strike only the price of the ask this command answered, at the sequence it has reached.");
            var candidate = s.Clone();
            candidate.deals[index].linkedDealId = row.id;
            candidate.unifiedCommitments.Add(row.Clone());
            return TryValidateCore(candidate, candidate.unifiedVoteReveals, new HashSet<string>(StringComparer.Ordinal) { row.id }, out error);
        }

        internal static bool TryValidateAnswer(EpisodeState s, UnifiedCommitmentState answered, out string error)
            => EpisodeValidation.TryValidateProspectiveUnifiedVote(s, out error) && TryValidateAdmittedAnswer(s, answered, out error);

        /// <summary>An admission entry: its caller has just held the state to the complete core (UnifiedVoteAdmission.TryAnswerOffer).</summary>
        internal static bool TryValidateAdmittedAnswer(EpisodeState s, UnifiedCommitmentState answered, out string error)
        {
            error = null;
            if (answered == null || answered.origin != UnifiedCommitments.NpcOffer || answered.status != DealStatus.Active)
                return Fail(out error, "An answer requires its detached actual NPC offer.");
            var index = s.unifiedCommitments.FindIndex(row => row.id == answered.id);
            if (index < 0 || s.unifiedCommitments[index].status != DealStatus.Proposed)
                return Fail(out error, "Only an existing genuine unanswered offer can acquire binding chronology.");
            var original = s.unifiedCommitments[index];
            int floor = s.week + (s.unifiedVoteReveals.Any(frame => frame.week == s.week) ? 1 : 0);
            if (answered.kind != original.kind || answered.sourcePolicy != original.sourcePolicy
                || answered.origin != original.origin || answered.makerId != original.makerId
                || answered.beneficiaryId != original.beneficiaryId || answered.reciprocal != original.reciprocal
                || answered.createdWeek != original.createdWeek || answered.targetId != original.targetId
                || answered.subtype != original.subtype || answered.trustImpact != original.trustImpact
                || answered.linkedCommitmentId != original.linkedCommitmentId || answered.brokenById != original.brokenById
                || answered.settledWeek != original.settledWeek || answered.settlementEffectKey != original.settlementEffectKey
                || answered.expiresWeek != s.week || answered.voteBindingWeek != s.week || answered.voteFirstRevealWeek != floor)
                return Fail(out error, "A genuine answer changes only consent and its actual current term/reveal chronology.");
            var candidate = s.Clone(); candidate.unifiedCommitments[index] = answered.Clone();
            return TryValidateCore(candidate, candidate.unifiedVoteReveals, null, out error);
        }

        private static bool TryValidateCore(EpisodeState s, IReadOnlyList<UnifiedVoteRevealState> archive,
            HashSet<string> draftIds, out string error)
        {
            error = null;
            if (s == null || s.schemaVersion != LiveSchema || s.unifiedCommitmentRulesVersion != Version
                || !YourWord.On(s) || s.week < 1 || s.week > 100 || s.nextSequence < 1 || s.nextSequence > 1000000
                || s.unifiedCommitments == null || s.unifiedCommitments.Count > UnifiedCommitments.FamilyCapacity * 2
                || s.promises == null || s.deals == null || s.promises.Count > UnifiedCommitments.FamilyCapacity
                || s.deals.Count > UnifiedCommitments.FamilyCapacity)
                return Fail(out error, "Expected the explicit prospective Vote aggregate at the live episode schema.");
            // The archive leaf establishes bounded cast/status/departure/power ownership first.
            // Its current private-box proof is deliberately not a UI or listener knowledge grant.
            if (!UnifiedVoteCompletedReveal.TryContext(s, out var context, out error)
                || !context.Known(s.playerId) || s.contestants.Count(person => person.isPlayer) != 1
                || !s.contestants.Any(person => person.id == s.playerId && person.isPlayer))
                return Fail(out error, error ?? "Invalid aggregate player identity.");
            if (!RawScalars(s, context, out error)) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in s.promises.Select(row => row.id).Concat(s.deals.Select(row => row.id))
                         .Concat(s.unifiedCommitments.Select(row => row?.id)))
                if (!Token(id) || !ids.Add(id)) return Fail(out error, "Commitment full identities must be globally unique and bounded.");
            if (s.promises.Any(row => row.kind == PromiseKind.Safety || row.kind == PromiseKind.Vote)
                || s.deals.Any(row => row.type == DealKind.SafetyAgreement || IsVoteType(row.type)))
                return Fail(out error, "Prospective Safety/Vote authority cannot have writable raw mirrors.");
            if (!UnifiedVoteRevealArchive.TryValidateComplete(s, archive, out error)) return false;
            int promises = s.promises.Count, deals = s.deals.Count;
            foreach (var row in s.unifiedCommitments)
            {
                if (row.kind == UnifiedCommitments.Safety)
                {
                    if (!UnifiedCommitments.ValidateSafetyRowCore(s, row, out error)) return false;
                }
                else if (!TryValidateVoteRow(s, context, archive, row, out error)) return false;
                if (!InstalledId(row.id, Prefix(row.origin), s.nextSequence + (draftIds?.Contains(row.id) == true ? 1 : 0), out _))
                    return Fail(out error, "Canonical identity must belong to its consumed source sequence.");
                if (row.sourcePolicy == UnifiedCommitments.PromisePolicy) promises++; else deals++;
            }
            if (promises > UnifiedCommitments.FamilyCapacity || deals > UnifiedCommitments.FamilyCapacity)
                return Fail(out error, "Historical records occupy the separate 200-row source-policy capacities.");
            if (!ValidateBindingDuties(s, out error) || !ValidateLinks(s, draftIds, out error)) return false;
            var offers = s.deals.Where(item => item.id.StartsWith(NpcDeals.OfferPrefix, StringComparison.Ordinal))
                .Concat(s.unifiedCommitments.Where(item => item.sourcePolicy == UnifiedCommitments.DealPolicy
                    && item.origin == UnifiedCommitments.NpcOffer).Select(UnifiedVoteReferences.ProjectDeal));
            foreach (var round in offers.GroupBy(item => item.week))
                if (round.Count() > NpcDeals.ProposalsPerWeek || round.Select(item => item.type).Distinct(StringComparer.Ordinal).Count() != round.Count()
                    || round.Select(item => item.proposerId).Distinct(StringComparer.Ordinal).Count() != round.Count())
                    return Fail(out error, "An actual NPC offer round retains its three-row, type/houseguest variety policy.");
            foreach (var row in s.unifiedCommitments.Where(row => row.kind == UnifiedVoteTogether.Vote))
                if (!ValidateTerminal(s, archive, row, out error)) return false;
            return true;
        }

        internal static bool TryValidateVoteRow(EpisodeState s, UnifiedVoteCompletedReveal.Context context,
            IReadOnlyList<UnifiedVoteRevealState> archive, UnifiedCommitmentState row, out string error)
        {
            error = null;
            if (row == null || row.kind != UnifiedVoteTogether.Vote || !Token(row.id)
                || !context.Known(row.makerId) || !context.Known(row.beneficiaryId) || row.makerId == row.beneficiaryId
                || row.createdWeek < 1 || row.createdWeek > s.week || !DealStatus.IsKnown(row.status)
                || row.status == DealStatus.Accepted)
                return Fail(out error, "Invalid canonical Vote identity, creation week or installed status.");
            if (!context.Present(row.makerId, row.createdWeek) || !context.Present(row.beneficiaryId, row.createdWeek)
                || row.targetId != null && !context.Present(row.targetId, row.createdWeek))
                return Fail(out error, "Creation cannot occur after its actual party/target already departed in an earlier week.");
            bool promise = row.sourcePolicy == UnifiedCommitments.PromisePolicy;
            if (promise)
            {
                if (row.reciprocal || row.subtype != null || row.trustImpact != DealTrust.Medium
                    || row.linkedCommitmentId != null
                    || (row.origin != UnifiedCommitments.PlayerPromise && row.origin != UnifiedCommitments.NpcPromise
                        && row.origin != UnifiedCommitments.StoryPromise)
                    || (row.origin == UnifiedCommitments.StoryPromise ? row.targetId != null
                        : !context.Known(row.targetId) || row.targetId == row.makerId)
                    || row.status == DealStatus.Proposed || row.status == DealStatus.Declined)
                    return Fail(out error, "A Vote promise retains its unilateral source and native Story-null policy.");
            }
            else if (row.sourcePolicy != UnifiedCommitments.DealPolicy || !row.reciprocal || !IsVoteType(row.subtype)
                || row.trustImpact != DealKind.DefaultTrust(row.subtype)
                || (row.subtype == DealKind.VoteTogether ? row.targetId != null : !context.Known(row.targetId))
                || !IsDealOrigin(row.origin)
                || ((row.status == DealStatus.Proposed || row.status == DealStatus.Declined)
                    && row.origin != UnifiedCommitments.NpcOffer))
                return Fail(out error, "A Vote deal retains its reciprocal subtype, target and real source policy.");
            bool playerMaker = row.origin == UnifiedCommitments.PlayerPromise || row.origin == UnifiedCommitments.PlayerDeal
                || row.origin == UnifiedCommitments.CounterDeal || row.origin == UnifiedCommitments.CounterPrice;
            bool npcPair = row.origin == UnifiedCommitments.NpcPromise || row.origin == UnifiedCommitments.NpcDeal;
            bool playerPayee = row.origin == UnifiedCommitments.NpcOffer || row.origin == VoteLobby
                || row.origin == VetoAskPrice || row.origin == OwnVetoPrice;
            if (playerMaker && (row.makerId != s.playerId || row.beneficiaryId == s.playerId)
                || npcPair && (row.makerId == s.playerId || row.beneficiaryId == s.playerId)
                || playerPayee && (row.makerId == s.playerId || row.beneficiaryId != s.playerId))
                return Fail(out error, "Vote source attribution must preserve the actual directional parties.");
            if ((row.origin == UnifiedCommitments.NpcDeal || row.origin == UnifiedCommitments.NpcOffer)
                    && (row.subtype != DealKind.VoteSave || row.targetId != row.makerId)
                || row.origin == VoteLobby && (row.subtype != DealKind.VoteSave || row.targetId != s.playerId)
                || (row.origin == VetoAskPrice || row.origin == OwnVetoPrice)
                    && (row.subtype != DealKind.VoteSave || row.targetId != s.playerId)
                || row.origin == UnifiedCommitments.CounterPrice && row.subtype == DealKind.VoteEvict
                || row.origin == UnifiedCommitments.CounterPrice && row.subtype == DealKind.VoteSave && row.targetId != row.beneficiaryId)
                return Fail(out error, "The origin cannot manufacture another Vote subtype or price target.");
            bool linked = row.origin == UnifiedCommitments.CounterDeal || row.origin == UnifiedCommitments.CounterPrice
                || row.origin == VetoAskPrice || row.origin == OwnVetoPrice;
            if (linked != (row.linkedCommitmentId != null)
                || row.linkedCommitmentId != null && (!Token(row.linkedCommitmentId) || row.linkedCommitmentId == row.id))
                return Fail(out error, "Vote consideration belongs only to the actual linked source owners.");
            bool unbound = row.origin == UnifiedCommitments.NpcOffer && row.voteBindingWeek == 0;
            if (unbound)
            {
                if (row.voteFirstRevealWeek != 0 || row.expiresWeek != row.createdWeek
                    || (row.status != DealStatus.Proposed && row.status != DealStatus.Declined && row.status != DealStatus.Expired))
                    return Fail(out error, "An unanswered offer cannot acquire binding/reveal chronology.");
            }
            else
            {
                bool lateOwner = row.origin == UnifiedCommitments.NpcOffer || row.origin == VetoAskPrice
                    || row.origin == UnifiedCommitments.StoryPromise || row.origin == UnifiedCommitments.StoryDeal;
                if (row.voteBindingWeek < row.createdWeek || row.voteBindingWeek > s.week
                    || row.origin != UnifiedCommitments.NpcOffer && row.voteBindingWeek != row.createdWeek
                    || row.status == DealStatus.Proposed || row.status == DealStatus.Declined
                    || row.voteFirstRevealWeek < row.voteBindingWeek || row.voteFirstRevealWeek > 101
                    || row.voteFirstRevealWeek > row.voteBindingWeek + (lateOwner ? 1 : 0))
                    return Fail(out error, "Vote chronology must retain the actual binding boundary and source-specific first floor.");
                bool openPrice = row.origin == VetoAskPrice || row.origin == OwnVetoPrice;
                if (row.expiresWeek != (openPrice ? 0 : row.voteBindingWeek))
                    return Fail(out error, "A finite Vote term is not extended by a first-reveal floor; only actual veto prices are open.");
                if (!context.Present(row.makerId, row.voteBindingWeek) || !context.Present(row.beneficiaryId, row.voteBindingWeek)
                    || row.targetId != null && !context.Present(row.targetId, row.voteBindingWeek))
                    return Fail(out error, "Binding parties and named target must still belong to that week's house.");
                if (row.voteFirstRevealWeek != row.voteBindingWeek)
                {
                    var passed = archive?.FirstOrDefault(frame => frame != null && frame.week == row.voteBindingWeek);
                    var power = context.Powers.FirstOrDefault(item => item.week == row.voteBindingWeek);
                    if (passed == null || power == null || power.evicteeId == row.makerId || power.evicteeId == row.beneficiaryId
                        || power.evicteeId == row.targetId)
                        return Fail(out error, "A late floor needs a passed regular frame and lawful post-reveal membership.");
                    // Durable compatibility, NOT proof of the original answer/Story choice. The
                    // actual creator owns the marker; no pruned log is invented as a receipt.
                }
            }
            bool terminal = row.status == DealStatus.Fulfilled || row.status == DealStatus.Broken;
            if (terminal ? row.settledWeek < row.voteFirstRevealWeek || row.settledWeek > s.week
                    || row.settledWeek < s.commitmentRulesStartWeek || row.settledWeek < 1
                    || row.expiresWeek != 0 && row.settledWeek > row.expiresWeek
                : row.settledWeek != 0 || row.brokenById != null || row.settlementEffectKey != null)
                return Fail(out error, "Vote settlement chronology and terminal metadata must be coherent.");
            if (row.status == DealStatus.Broken)
            {
                if (promise ? row.brokenById != row.makerId
                    : row.brokenById != null && row.brokenById != row.makerId && row.brokenById != row.beneficiaryId)
                    return Fail(out error, "Only actual voting parties may own a Vote breach.");
                if (row.subtype == DealKind.VoteTogether && row.brokenById != null || !UnifiedVoteHistory.ExactKey(row))
                    return Fail(out error, "Vote betrayal identity must name its exact policy, duty, parties and actual week.");
            }
            else if (row.brokenById != null || row.settlementEffectKey != null)
                return Fail(out error, "Only an actual Vote breach carries a betrayal identity.");
            return true;
        }

        private static bool ValidateTerminal(EpisodeState s, IReadOnlyList<UnifiedVoteRevealState> archive,
            UnifiedCommitmentState row, out string error)
        {
            error = null;
            if (row.voteBindingWeek == 0)
            {
                if (row.status == DealStatus.Proposed && PublishedEnding(s, row))
                    return Fail(out error, "An unanswered binding proposal cannot survive its already-published source departure.");
                return row.status != DealStatus.Expired || CompatibleEnding(s, row)
                    || Fail(out error, "An unanswered expired offer needs a compatible real term or departure ending.");
            }
            if (!FirstDecision(s, archive, row, out var frame, out string status, out string actor, out error)) return false;
            bool terminal = row.status == DealStatus.Fulfilled || row.status == DealStatus.Broken;
            if (terminal)
                return frame != null && row.settledWeek == frame.week && row.status == status
                    && (row.status != DealStatus.Broken || row.brokenById == actor)
                    || Fail(out error, "A terminal Vote row must equal its FIRST actually deciding eligible regular frame.");
            if (frame != null) return Fail(out error, "An Active/Expired Vote row cannot hide an earlier mandatory verdict.");
            // Source week-turn always expires finite promises. DealPass has its own actual
            // scheduling/enable boundary: no query is allowed to manufacture its execution.
            if (row.status == DealStatus.Active && row.sourcePolicy == UnifiedCommitments.PromisePolicy
                && row.expiresWeek > 0 && row.expiresWeek < s.week)
                return Fail(out error, "An active Vote promise cannot survive its completed week turn.");
            if (row.status == DealStatus.Active && PublishedEnding(s, row))
                return Fail(out error, "An Active Vote row cannot survive an already-published source departure/void ending.");
            if (row.status == DealStatus.Expired && !CompatibleEnding(s, row))
                return Fail(out error, "An expired Vote row needs a compatible real term/departure/consideration ending.");
            return true;
        }

        internal static bool FirstDecision(EpisodeState s, IReadOnlyList<UnifiedVoteRevealState> archive,
            UnifiedCommitmentState row, out UnifiedVoteRevealState deciding, out string status, out string actor, out string error)
        {
            deciding = null; status = null; actor = null; error = null;
            if (row.voteBindingWeek == 0) return true;
            int cutoff = EndingCutoff(s, row);
            // This detached predecision calculation is never returned as a mutable installed row.
            var draft = row.Clone(); draft.status = DealStatus.Active;
            foreach (var frame in archive)
            {
                if (!Eligible(s, row, frame.week, cutoff)) continue;
                var power = s.ledger.power.First(item => item.week == frame.week);
                if (row.sourcePolicy == UnifiedCommitments.DealPolicy && row.subtype == DealKind.VoteTogether)
                {
                    if (!UnifiedVoteTogether.TryVerdict(draft, frame.ballots, out status, out error)) return false;
                    actor = null;
                }
                else
                {
                    if (!UnifiedVoteObligations.TryVerdict(draft, frame.ballots, power.nominees, out var verdict, out error)) return false;
                    status = verdict?.Status; actor = verdict?.ActorId;
                }
                if (status != null) { deciding = frame; return true; }
            }
            return true;
        }

        /// <summary>
        /// Whether a regular reveal in <paramref name="week"/> may decide this row at all: no earlier than its first
        /// reveal floor, no later than its ending <paramref name="cutoff"/> or its finite term, and - for a targeted
        /// Save/Evict deal only - at a week the levers were on. The one predicate the family validation proves a
        /// terminal row by (<see cref="FirstDecision"/>) and the reveal's settlement decides it by
        /// (<see cref="UnifiedVoteSettlement"/>).
        /// </summary>
        internal static bool Eligible(EpisodeState s, UnifiedCommitmentState row, int week, int cutoff)
        {
            if (week < row.voteFirstRevealWeek || week > cutoff || row.expiresWeek != 0 && week > row.expiresWeek) return false;
            // Engine454 passes LeverRulesOn at the ACTUAL reveal week. Only targeted
            // Deal Save/Evict uses that gate (DealResolution329); Promise and Together
            // status verdicts remain unconditional even before C0 metadata stamping.
            return !(row.sourcePolicy == UnifiedCommitments.DealPolicy
                && (row.subtype == DealKind.VoteSave || row.subtype == DealKind.VoteEvict)
                && (s.leverRulesStartWeek < 1 || week < s.leverRulesStartWeek));
        }

        /// <summary>The last week a regular reveal may still decide this row: its parties' or target's departure, or its price's void.</summary>
        internal static int EndingCutoff(EpisodeState s, UnifiedCommitmentState row)
        {
            int cutoff = s.week;
            bool promise = row.sourcePolicy == UnifiedCommitments.PromisePolicy;
            foreach (var removal in s.story.removals)
                if (removal.contestantId == row.makerId || removal.contestantId == row.beneficiaryId
                    || !promise && removal.contestantId == row.targetId) cutoff = Math.Min(cutoff, removal.week);
            if (!promise)
                foreach (var power in s.ledger.power)
                    if (power.evicteeId == row.makerId || power.evicteeId == row.beneficiaryId
                        || power.evicteeId != null && power.evicteeId == row.targetId) cutoff = Math.Min(cutoff, power.week);
            var bought = VoidingBreach(s, row);
            if (bought != null)
                // Actual regular Reveal settles Vote Verdicts BEFORE SettleVoteRead judges
                // InformationSharing lies (Engine454/475, VoteRead198). That same frame can
                // already decide the price, so even an Expired claim cannot erase it. Other
                // non-ballot bought actions settle before the regular reveal and cut it off.
                cutoff = Math.Min(cutoff, bought.settledWeek - (bought.type == DealKind.InformationSharing ? 0 : 1));
            return cutoff;
        }

        /// <summary>
        /// The breach that voids this row, when it is a price: what it bought, broken by the one the price is owed
        /// to, by an act and not a ballot, while the price still bound (EpisodeEngine.VoidThePrice's own rule).
        /// The source voids such a price in the command that breaks what it bought, so from then on the price has
        /// ended, whatever week it is: an Active one has outlived its ending, and an Expired one is that ending -
        /// even in the week an information deal's lie, judged after the reveal, ends it (vote family V4).
        /// </summary>
        private static DealState VoidingBreach(EpisodeState s, UnifiedCommitmentState row)
        {
            var price = row.sourcePolicy == UnifiedCommitments.DealPolicy ? UnifiedVoteReferences.ProjectDeal(row) : null;
            if (price == null || !Price(price) || row.linkedCommitmentId == null) return null;
            var bought = UnifiedVoteReferences.DealsUnchecked(s).FirstOrDefault(item => item.id == row.linkedCommitmentId);
            return bought != null && bought.status == DealStatus.Broken && bought.brokenById == price.recipientId
                && bought.settledWeek > 0 && !KnownBallots.SettledByABallot(bought)
                && (price.expiresWeek == 0 || bought.settledWeek <= price.expiresWeek) ? bought : null;
        }

        /// <summary>
        /// Whether an Expired row has an ending to have expired by: its term passed, or an ending the source has
        /// already published. At regular Results this week's evictee's departure is not one yet - the following
        /// Advance writes it - so a state at Results with the evictee's deal already Expired is refused: no command
        /// leaves one (vote family V5a, the lead's decision D4). Until V5a the readers checked the whole family in
        /// the middle of that Advance, after its departure ending and while the eviction still read Results, and
        /// this accepted such a state for them; they check the storage only now
        /// (<see cref="TryValidateStorage"/>), so the complete core is the save gate by itself.
        /// </summary>
        private static bool CompatibleEnding(EpisodeState s, UnifiedCommitmentState row)
        {
            if (row.expiresWeek > 0 && row.expiresWeek < s.week) return true;
            return Ending(s, row);
        }

        /// <summary>Whether a still-binding row has outlived an ending the source has already written.</summary>
        private static bool PublishedEnding(EpisodeState s, UnifiedCommitmentState row) => Ending(s, row);

        private static bool Ending(EpisodeState s, UnifiedCommitmentState row)
        {
            bool promise = row.sourcePolicy == UnifiedCommitments.PromisePolicy;
            if (s.story.removals.Any(item => item.contestantId == row.makerId || item.contestantId == row.beneficiaryId
                || !promise && item.contestantId == row.targetId)) return true;
            if (!promise && s.ledger.power.Any(item => (item.evicteeId == row.makerId || item.evicteeId == row.beneficiaryId
                || item.evicteeId != null && item.evicteeId == row.targetId)
                // At regular Results the ballot writer has marked the evictee Jury, but
                // EndWithTheEvictee still belongs to the subsequent real Advance command.
                && (item.week < s.week || s.phase != EpisodePhase.Eviction || !s.evictionResolved
                    || s.evictionStage != EvictionStage.Results))) return true;
            return EndingCutoff(s, row) < s.week || row.linkedCommitmentId != null
                && (EndingCutoff(s, row) < row.voteFirstRevealWeek || VoidingBreach(s, row) != null);
        }

        private static bool ValidateBindingDuties(EpisodeState s, out string error)
        {
            error = null;
            var duties = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in s.unifiedCommitments.Where(item => DealStatus.Binds(item.status)))
            {
                string first = row.makerId, second = row.beneficiaryId;
                if (row.reciprocal && string.CompareOrdinal(first, second) > 0) { first = row.beneficiaryId; second = row.makerId; }
                string key = Part(row.kind) + Part(row.sourcePolicy) + Part(row.subtype ?? "") + Part(row.targetId ?? "")
                    + Part(first) + Part(second) + ":" + row.expiresWeek + ":" + row.voteBindingWeek + ":" + row.voteFirstRevealWeek;
                if (!duties.Add(key)) return Fail(out error, "The same currently binding family/policy/directional duty and real term is duplicated.");
            }
            return true;
        }

        private static bool ValidateLinks(EpisodeState s, HashSet<string> draftIds, out string error)
        {
            error = null;
            var canonical = s.unifiedCommitments.ToDictionary(item => item.id, StringComparer.Ordinal);
            var deals = UnifiedVoteReferences.DealsUnchecked(s).ToDictionary(item => item.id, StringComparer.Ordinal);
            long Limit(string id) => s.nextSequence + (draftIds?.Contains(id) == true ? 1 : 0);
            foreach (var own in deals.Values)
            {
                bool price = Price(own);
                if (own.linkedDealId == null)
                {
                    if (price) return Fail(out error, "Every price must retain what it actually bought.");
                    continue;
                }
                if (!deals.TryGetValue(own.linkedDealId, out var other) || own.id == other.id
                    || other.linkedDealId != own.id || price == Price(other) || !SamePair(own, other))
                    return Fail(out error, "Consideration needs two reciprocal true owners, one price and the actual pair.");
                var bought = price ? other : own; var paid = price ? own : other;
                canonical.TryGetValue(bought.id, out var boughtRow); canonical.TryGetValue(paid.id, out var priceRow);
                if (boughtRow == null && priceRow == null)
                {
                    if (own.week != other.week) return Fail(out error, "Unmoved raw consideration retains its original same-week rule.");
                    continue;
                }
                if (priceRow?.origin == VetoAskPrice || priceRow?.origin == OwnVetoPrice)
                {
                    bool acceptedAsk = priceRow.origin == VetoAskPrice;
                    if (boughtRow != null || bought.type != DealKind.VetoUse || bought.targetId != null
                        || bought.trustImpact != DealKind.DefaultTrust(DealKind.VetoUse)
                        || (bought.status != DealStatus.Active && bought.status != DealStatus.Fulfilled
                            && bought.status != DealStatus.Broken && bought.status != DealStatus.Expired)
                        || bought.week > paid.week || bought.expiresWeek != paid.week
                        || paid.type != DealKind.VoteSave || paid.targetId != s.playerId || paid.expiresWeek != 0
                        || paid.proposerId == s.playerId || paid.recipientId != s.playerId
                        || paid.week < s.commitmentRulesStartWeek
                        || acceptedAsk && (bought.proposerId != paid.proposerId || bought.recipientId != s.playerId
                            || !InstalledId(bought.id, "deal-veto-", Limit(bought.id), out _))
                        || !acceptedAsk && (bought.proposerId != s.playerId || bought.recipientId != paid.proposerId
                            || bought.week != paid.week || !InstalledId(bought.id, "deal-player-", Limit(bought.id), out long n)
                            || !InstalledId(paid.id, Negotiation.PricePrefix, Limit(paid.id), out long p) || n != p))
                        return Fail(out error, "A veto price must retain its distinct accepted-ask or own-veto provenance, term and link.");
                    continue;
                }
                if (bought.week != paid.week || bought.week < s.commitmentRulesStartWeek
                    || !InstalledId(bought.id, Negotiation.CounterDealPrefix, Limit(bought.id), out long boughtSequence)
                    || !InstalledId(paid.id, Negotiation.PricePrefix, Limit(paid.id), out long paidSequence)
                    || boughtSequence != paidSequence || bought.proposerId != s.playerId || paid.proposerId != s.playerId
                    || bought.recipientId != paid.recipientId || bought.recipientId == s.playerId || bought.type == paid.type
                    || boughtRow != null && boughtRow.origin != UnifiedCommitments.CounterDeal
                    || priceRow != null && priceRow.origin != UnifiedCommitments.CounterPrice
                    || !CounterRecord(s, bought, false) || !CounterRecord(s, paid, true))
                    return Fail(out error, "A counter retains the same-command source identities and separate bought/price policies.");
            }
            return true;
        }

        private static bool CounterRecord(EpisodeState s, DealState row, bool price)
        {
            if (!DealKind.IsKnown(row.type) || row.trustImpact != DealKind.DefaultTrust(row.type)
                || (row.status != DealStatus.Active && row.status != DealStatus.Fulfilled
                    && row.status != DealStatus.Broken && row.status != DealStatus.Expired)) return false;
            if (price && row.type != DealKind.SafetyAgreement && row.type != DealKind.FinalTwo
                && row.type != DealKind.VoteSave && row.type != DealKind.VoteTogether) return false;
            int expiry = row.type == DealKind.FinalTwo || row.type == DealKind.Partnership || row.type == DealKind.AllianceInvite
                || row.type == DealKind.InformationSharing || row.type == DealKind.FinalThree ? 0 : row.week;
            if (row.expiresWeek != expiry || (DealKind.NamesATarget(row.type) ? row.targetId == null || s.Find(row.targetId) == null : row.targetId != null)
                || price && row.type == DealKind.VoteSave && row.targetId != row.recipientId) return false;
            bool settled = row.status == DealStatus.Broken || row.status == DealStatus.Fulfilled;
            if (!settled) return row.settledWeek == 0 && row.brokenById == null;
            if (row.settledWeek < row.week || row.settledWeek < s.commitmentRulesStartWeek || row.settledWeek > s.week) return false;
            return row.status == DealStatus.Fulfilled ? row.brokenById == null
                : row.brokenById == row.proposerId || row.brokenById == row.recipientId || row.brokenById == null && IsVoteType(row.type);
        }

        private static bool RawScalars(EpisodeState s, UnifiedVoteCompletedReveal.Context context, out string error)
        {
            error = null;
            bool Optional(string id) => string.IsNullOrEmpty(id) || context.Known(id);
            foreach (var row in s.promises)
                if (row == null || !Token(row.id) || !context.Known(row.fromId) || !context.Known(row.toId)
                    || row.fromId == row.toId || !Optional(row.targetId) || !Enum.IsDefined(typeof(PromiseKind), row.kind)
                    || !Enum.IsDefined(typeof(PromiseStatus), row.status)
                    || row.week < 1 || row.week > s.week || row.expiresWeek < 0 || row.expiresWeek > 101
                    || row.settledWeek < 0 || row.settledWeek > s.week
                    || row.brokenById != null && (row.status != PromiseStatus.Broken || row.brokenById != row.fromId || row.settledWeek == 0)
                    || row.settledWeek > 0 && (row.settledWeek < row.week || row.settledWeek < s.commitmentRulesStartWeek
                        || row.status != PromiseStatus.Fulfilled && row.status != PromiseStatus.Broken
                        || row.status == PromiseStatus.Broken && row.brokenById == null))
                    return Fail(out error, "Invalid raw promise scalar/reference data before aggregate lookup.");
            foreach (var row in s.deals)
                if (row == null || !Token(row.id) || !context.Known(row.proposerId) || !context.Known(row.recipientId)
                    || row.proposerId == row.recipientId || !Optional(row.targetId) || !DealKind.IsKnown(row.type)
                    || !DealStatus.IsKnown(row.status) || !DealTrust.IsKnown(row.trustImpact)
                    || row.week < 1 || row.week > s.week || row.expiresWeek < 0 || row.expiresWeek > 101
                    || row.linkedDealId != null && !Token(row.linkedDealId) || row.settledWeek < 0 || row.settledWeek > s.week
                    || row.brokenById != null && (row.status != DealStatus.Broken || row.settledWeek == 0
                        || row.brokenById != row.proposerId && row.brokenById != row.recipientId)
                    || row.settledWeek > 0 && (row.settledWeek < row.week || row.settledWeek < s.commitmentRulesStartWeek
                        || row.status != DealStatus.Fulfilled && row.status != DealStatus.Broken
                        || row.status == DealStatus.Broken && row.brokenById == null && !IsVoteType(row.type)))
                    return Fail(out error, "Invalid raw deal scalar/reference data before aggregate lookup.");
            return true;
        }

        internal static bool InstalledId(string id, string prefix, long nextSequence, out long sequence)
        {
            sequence = 0;
            if (prefix == null || !Token(id) || !id.StartsWith(prefix, StringComparison.Ordinal)) return false;
            string suffix = id.Substring(prefix.Length);
            return suffix.Length > 0 && suffix.All(ch => ch >= '0' && ch <= '9')
                && long.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out sequence)
                && sequence > 0 && sequence < nextSequence && suffix == sequence.ToString(CultureInfo.InvariantCulture);
        }
        internal static string Prefix(string origin) => origin switch {
            UnifiedCommitments.PlayerPromise or UnifiedCommitments.StoryPromise or UnifiedCommitments.HoHPitch => "promise-",
            UnifiedCommitments.NpcPromise => "promise-npc-", UnifiedCommitments.PlayerDeal => "deal-player-",
            UnifiedCommitments.NpcDeal => "deal-npc-", UnifiedCommitments.NpcOffer => NpcDeals.OfferPrefix,
            VoteLobby or UnifiedCommitments.Lobby => "deal-lobby-", UnifiedCommitments.StoryDeal => "deal-story-",
            UnifiedCommitments.CounterDeal => Negotiation.CounterDealPrefix,
            UnifiedCommitments.CounterPrice or VetoAskPrice or OwnVetoPrice => Negotiation.PricePrefix,
            _ => null,
        };
        internal static bool IsVoteType(string type) => type == DealKind.VoteTogether || type == DealKind.VoteSave || type == DealKind.VoteEvict;
        private static bool IsPromiseOrigin(string origin) => origin == UnifiedCommitments.PlayerPromise
            || origin == UnifiedCommitments.NpcPromise || origin == UnifiedCommitments.StoryPromise;
        private static bool IsDealOrigin(string origin) => origin == UnifiedCommitments.PlayerDeal || origin == UnifiedCommitments.NpcDeal
            || origin == UnifiedCommitments.NpcOffer || origin == VoteLobby || origin == UnifiedCommitments.StoryDeal
            || origin == UnifiedCommitments.CounterDeal || origin == UnifiedCommitments.CounterPrice || origin == VetoAskPrice || origin == OwnVetoPrice;
        private static bool Price(DealState row) => row.id.StartsWith(Negotiation.PricePrefix, StringComparison.Ordinal);
        private static bool SamePair(DealState a, DealState b) => a.proposerId == b.proposerId && a.recipientId == b.recipientId
            || a.proposerId == b.recipientId && a.recipientId == b.proposerId;
        private static string Part(string value) => value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;
        private static bool Token(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160 && !value.Any(char.IsControl);
        private static bool Fail(out string error, string reason) { error = reason; return false; }
    }
}
