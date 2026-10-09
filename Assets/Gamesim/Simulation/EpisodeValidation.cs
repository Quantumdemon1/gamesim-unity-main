using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    public static partial class EpisodeValidation
    {
        /// <summary>
        /// The house size this format accepts.
        ///
        /// <para>This used to demand exactly six, which made the port a fixed demo rather than the
        /// game the web version is — that one defaults to eight and lets the player choose. Three is
        /// the floor because the Final Three is a real stage and a season cannot start already past
        /// it; sixteen is a ceiling on stored cast rather than a design statement, and exists so a
        /// corrupt save cannot describe a house of thousands.</para>
        /// </summary>
        public const int MinimumCast = 3, MaximumCast = 16;

        public static bool TryValidate(EpisodeState s, out string error)
        {
            // Only supported canonical mode selects the enabled complete core. Legacy saves stay
            // disabled; validation never activates, converts or clears any stored authority.
            if (s != null && s.unifiedCommitmentRulesVersion == UnifiedCommitments.ProspectiveVersion)
            {
                // Real new seasons start C0 and story knowledge together. An enabled save cannot
                // turn either authority off or schedule it for later and strand settlement owners.
                // Historical rows may still have been created before either rule's start week.
                if (!YourWord.On(s))
                    return Fail(out error, "Unified Safety requires active commitment and story knowledge rules.");
                return TryValidateCore(s, true, out error);
            }
            return TryValidateCore(s, false, out error);
        }

        /// <summary>
        /// Complete prospective saved-Safety validation for internal historical diagnostics. This
        /// also accepts data before public enabled-mode prerequisites are active; it does not
        /// activate authority, clear fields or install raw mirrors. Both entries share the complete
        /// episode invariants below; public validation additionally requires active C0 and knowledge.
        /// </summary>
        internal static bool TryValidateProspectiveUnifiedSafety(EpisodeState s, out string error) =>
            TryValidateCore(s, true, out error);

        /// <summary>
        /// Complete detached candidate validation only. No public command, constructor, load,
        /// migration or save path dispatches here or opts a recorded season into Vote authority.
        /// Current prerequisites must be active; historical rows retain their own source weeks.
        /// <para>The house's deal pass runs from the first week (vote family V5a, the lead's decision D5): a finite
        /// Vote deal past its week ends at that pass, which mode 1's settlement leaves to decide, so the two agree only
        /// where the pass has run every week.</para>
        /// </summary>
        internal static bool TryValidateProspectiveUnifiedVote(EpisodeState s, out string error)
        {
            if (s == null || s.unifiedCommitmentRulesVersion != UnifiedVoteFamilyValidation.Version
                || !YourWord.On(s))
                return Fail(out error, "Prospective Vote requires its explicit version and active commitment/story rules.");
            if (s.dealRulesStartWeek != 1)
                return Fail(out error, "Prospective Vote requires the house's deal pass from the first week.");
            return TryValidateCore(s, UnifiedVoteFamilyValidation.Version, out error);
        }

        // Existing public/historical mode0/1 entries retain their exact selection and body.
        private static bool TryValidateCore(EpisodeState s, bool prospectiveSafety, out string error) =>
            TryValidateCore(s, prospectiveSafety ? UnifiedCommitments.ProspectiveVersion : 0, out error);

        private static bool TryValidateCore(EpisodeState s, int canonicalMode, out string error)
        {
            bool prospectiveVote = canonicalMode == UnifiedVoteFamilyValidation.Version;
            bool prospectiveSafety = canonicalMode == UnifiedCommitments.ProspectiveVersion || prospectiveVote;
            error = null;
            if (s == null || s.schemaVersion != 28) return Fail(out error, "Unsupported episode schema.");
            if (prospectiveSafety)
            {
                if (s.unifiedCommitmentRulesVersion != canonicalMode
                    || s.unifiedCommitments == null || s.unifiedCommitments.Count > UnifiedCommitments.FamilyCapacity * 2
                    || s.unifiedHearingRulesVersion < 0 || s.unifiedHearingRulesVersion > UnifiedCommitmentHearings.ProspectiveVersion
                    || s.unifiedHearingEvidence == null || s.unifiedHearingReceipts == null
                    || s.unifiedHearingEvidence.Count > UnifiedCommitmentHearings.EvidenceCapacity
                    || s.unifiedHearingReceipts.Count > UnifiedCommitmentHearings.ReceiptCapacity
                    || (s.unifiedHearingRulesVersion == 0
                        && (s.unifiedHearingEvidence.Count != 0 || s.unifiedHearingReceipts.Count != 0)))
                    return Fail(out error, "Invalid prospective Safety or hearing storage.");
            }
            else
            {
                // Legacy disabled mode must stay empty. Unknown versions cannot opt in to a
                // partially integrated ruleset, nor silently discard canonical rows.
                if (s.unifiedCommitmentRulesVersion != 0 || s.unifiedCommitments == null || s.unifiedCommitments.Count != 0)
                    return Fail(out error, "Unified commitments are not enabled in this build.");
                // Durable hearing state is versioned independently, but requires canonical mode.
                if (s.unifiedHearingRulesVersion != 0 || s.unifiedHearingEvidence == null || s.unifiedHearingEvidence.Count != 0
                    || s.unifiedHearingReceipts == null || s.unifiedHearingReceipts.Count != 0)
                    return Fail(out error, "Unified hearing coordination is not enabled in this build.");
            }
            // Public0/1 storage remains inert, including its original refusal. Only the explicit
            // internal Vote core admits an archive, which must pass complete frame validation.
            // Missing evidence is never normalized or backfilled from the growing state.
            if (prospectiveVote)
            {
                if (s.unifiedVoteReveals == null || s.unifiedVoteReveals.Count > UnifiedVoteRevealArchive.MaximumFrames)
                    return Fail(out error, "Prospective Vote requires bounded complete reveal evidence.");
            }
            else if (s.unifiedVoteReveals == null || s.unifiedVoteReveals.Count != 0)
                return Fail(out error, "Unified Vote evidence is not enabled in this build.");
            if (s.competitionRulesVersion < 1 || s.competitionRulesVersion > CompetitionRules.Current)
                return Fail(out error, "Unsupported competition rules version.");
            if (!Text(s.sessionId, 160) || s.week < 1 || s.week > 100 || s.revision < 0 || s.revision > 1000000 ||
                s.nextSequence < 1 || s.nextSequence > 1000000 || s.socialActions < 0
                || s.socialActions > MostActionsAWeekCanHold || !Defined(s.phase))
                return Fail(out error, "Invalid session counters or phase.");
            if (s.blocRulesStartWeek < 1 || s.blocRulesStartWeek > 101 || s.blocRulesStartWeek > s.week + 1)
                return Fail(out error, "Voting-bloc activation week must be within the saved season boundary.");
            if (s.readRulesStartWeek < 0 || s.readRulesStartWeek > 101 || s.readRulesStartWeek > s.week + 1)
                return Fail(out error, "Read-rules activation week must be within the saved season boundary.");
            if (s.leverRulesStartWeek < 0 || s.leverRulesStartWeek > 101 || s.leverRulesStartWeek > s.week + 1)
                return Fail(out error, "Lever-rules activation week must be within the saved season boundary.");
            if (s.weekRulesStartWeek < 0 || s.weekRulesStartWeek > 101 || s.weekRulesStartWeek > s.week + 1)
                return Fail(out error, "Week-rules activation week must be within the saved season boundary.");
            if (s.windowActions == null || s.windowActions.Count != Windows.Count || s.windowActions.Any(n => n < 0 || n > MostActionsAWeekCanHold))
                return Fail(out error, "Invalid window action counts.");
            if (s.economyRulesVersion < 0 || s.economyRulesVersion > 1
                || s.moveInExtrasSpent < 0 || s.moveInExtrasSpent > MostActionsAWeekCanHold
                || (s.moveInExtrasSpent != 0 && (s.economyRulesVersion == 0 || !EpisodeEngine.WeekRulesOn(s)
                    || s.week != 1 || EpisodeEngine.IsFirstNight(s))))
                return Fail(out error, "Invalid economy rules or move-in extras debit.");
            if (s.agencyRulesStartWeek < 0 || s.agencyRulesStartWeek > 101 || s.agencyRulesStartWeek > s.week + 1)
                return Fail(out error, "Agency-rules activation week must be within the saved season boundary.");
            if (s.finaleRulesStartWeek < 0 || s.finaleRulesStartWeek > 101 || s.finaleRulesStartWeek > s.week + 1)
                return Fail(out error, "Finale-rules activation week must be within the saved season boundary.");
            if (s.commitmentRulesStartWeek < 0 || s.commitmentRulesStartWeek > 101 || s.commitmentRulesStartWeek > s.week + 1)
                return Fail(out error, "Commitment-rules activation week must be within the saved season boundary.");
            if (s.socialBudgetRulesStartWeek < 1 || s.socialBudgetRulesStartWeek > 101 || s.socialBudgetRulesStartWeek > s.week + 1)
                return Fail(out error, "Social-budget activation week must be within the saved season boundary.");
            if (s.playerStudyBonus < 0 || s.playerStudyBonus > 5) return Fail(out error, "Study preparation must be between zero and five.");
            if (s.contestants == null || s.contestants.Count < MinimumCast || s.contestants.Count > MaximumCast || s.contestants.Any(c => c == null))
                return Fail(out error, "A house holds between " + MinimumCast + " and " + MaximumCast + " contestants.");
            if (s.contestants.Select(c => c.id).Distinct(StringComparer.Ordinal).Count() != s.contestants.Count || s.contestants.Count(c => c.isPlayer) != 1 ||
                !s.contestants.Any(c => c.isPlayer && c.id == s.playerId)) return Fail(out error, "Cast/player identity is invalid.");
            foreach (var c in s.contestants)
            {
                if (!ShortOrAbsent(c.sourceTemplateId, 100)) return Fail(out error, "Invalid source template identity.");
                if (c.appearance != null && !c.appearance.TryValidate(out error)) return false;
                if (!Text(c.id, 100) || !Text(c.name, 100) || !Defined(c.status) || c.stats == null || c.traits == null ||
                    c.traits.Count > 20 || c.traits.Any(t => !Text(t, 100)) || c.nominationWeeks == null || c.nominationWeeks.Count > 100 ||
                    c.nominationWeeks.Any(w => w < 1 || w > s.week) || c.timesNominated < 0 || c.hohWins < 0 || c.vetoWins < 0 ||
                    c.timesNominated > 100 || c.hohWins > 100 || c.vetoWins > 100)
                    return Fail(out error, "Invalid contestant data.");
                // Card copy is optional, so it is bounded rather than required: an old save has none
                // of it and must stay valid.
                if (c.age < 0 || c.age > 120 || !ShortOrAbsent(c.occupation, 100) || !ShortOrAbsent(c.archetype, 100)
                    || !ShortOrAbsent(c.hometown, 100) || !ShortOrAbsent(c.bio, 1000))
                    return Fail(out error, "Invalid contestant card copy.");
                var stats = new[] { c.stats.physical, c.stats.mental, c.stats.endurance, c.stats.social, c.stats.luck, c.stats.competition, c.stats.strategic, c.stats.loyalty };
                if (stats.Any(x => !Finite(x) || x < 0 || x > 10)) return Fail(out error, "Stats must be finite in the supported 0–10 range.");
            }
            bool Id(string id) => s.contestants.Any(c => c.id == id);
            bool Optional(string id) => string.IsNullOrEmpty(id) || Id(id);
            if (!Optional(s.hohId) || !Optional(s.previousHohId) || !Optional(s.vetoHolderId) || !Optional(s.winnerId) ||
                !Optional(s.runnerUpId) || !Optional(s.finalPart1WinnerId) || !Optional(s.finalPart2WinnerId)) return Fail(out error, "Unknown role identity.");
            if (s.relationships == null || s.relationships.Count > s.contestants.Count * (s.contestants.Count - 1) || s.relationships.Any(r => r == null || !Id(r.fromId) || !Id(r.toId) || !Finite(r.score) || Math.Abs(r.score) > 100) ||
                s.relationships.GroupBy(r => new { r.fromId, r.toId }).Any(g => g.Count() > 1)) return Fail(out error, "Invalid directed relationship graph.");
            if (s.nominees == null || s.nominees.Count > 2 || s.nominees.Any(id => !Id(id)) || s.nominees.Distinct().Count() != s.nominees.Count ||
                s.vetoPlayers == null || s.vetoPlayers.Count > EpisodeEngine.VetoLineupSize || s.vetoPlayers.Any(id => !Id(id)) || s.vetoPlayers.Distinct().Count() != s.vetoPlayers.Count)
                return Fail(out error, "Invalid nomination or veto participant references.");
            if (s.promises == null || s.promises.Count > 200 || s.promises.Any(p => p == null || !Text(p.id, 160) || !Id(p.fromId) || !Id(p.toId) || p.fromId == p.toId ||
                !Optional(p.targetId) || !Defined(p.kind) || !Defined(p.status) || p.week < 1 || p.week > s.week || p.expiresWeek < 0 || p.expiresWeek > 101) ||
                s.promises.GroupBy(p => p.id).Any(g => g.Count() > 1)) return Fail(out error, "Invalid promise data.");
            // Schema 22 (C0): the week a kept or broken promise was settled - no earlier than it was
            // made, nor than the rules that write it - and, on a broken one, its maker, whose act
            // settles a promise: written together, at the settlement, or not at all. 0 and null on
            // one settled before the rules.
            if (s.promises.Any(p => (p.brokenById != null && (p.status != PromiseStatus.Broken || p.brokenById != p.fromId || p.settledWeek == 0))
                    || p.settledWeek < 0 || p.settledWeek > s.week
                    || (p.settledWeek > 0 && (p.settledWeek < p.week || p.settledWeek < s.commitmentRulesStartWeek
                        || (p.status != PromiseStatus.Fulfilled && p.status != PromiseStatus.Broken)
                        || (p.status == PromiseStatus.Broken && p.brokenById == null)))))
                return Fail(out error, "Invalid promise settlement.");
            if (s.alliances == null || s.alliances.Count > 100 || s.alliances.Any(a => a == null || !Text(a.id, 160) || !Text(a.name, 100) || a.members == null ||
                a.members.Count < 2 || a.members.Count > s.contestants.Count || a.members.Any(id => !Id(id)) || a.members.Distinct().Count() != a.members.Count) ||
                s.alliances.GroupBy(a => a.id).Any(g => g.Count() > 1)) return Fail(out error, "Invalid alliance data.");
            if (s.memories == null || s.memories.Count > 30 * s.contestants.Count || s.memories.Any(m => m == null || !Id(m.ownerId) || !Id(m.subjectId) || !Text(m.text, 2000) || m.week < 1 || m.week > s.week))
                return Fail(out error, "Invalid memory data.");
            if (s.votes == null || s.votes.Count > s.contestants.Count || s.votes.Any(v => v == null || !Id(v.voterId) || !Id(v.targetId) || v.voterId == v.targetId) ||
                s.votes.GroupBy(v => v.voterId).Any(g => g.Count() > 1)) return Fail(out error, "Invalid votes.");
            // Schema4 simulation may add the existing counter1000 + study5 + base12.5 + clutch5 (+ luck3).
            // Preserve legal v3 counters without clipping source bonus arithmetic at the former 1000 result ceiling.
            if (s.competitionScores == null || s.competitionScores.Count > s.contestants.Count || s.competitionScores.Any(c => c == null || !Id(c.contestantId) || !Finite(c.score) || c.score < 0 || c.score > 1030) ||
                s.competitionScores.GroupBy(c => c.contestantId).Any(g => g.Count() > 1)) return Fail(out error, "Invalid competition results.");
            if (s.events == null || s.events.Count > 256 || s.events.Any(e => e == null || e.sequence < 1 || e.sequence >= s.nextSequence || e.week < 1 || e.week > s.week ||
                !Defined(e.phase) || !Text(e.kind, 100) || !Text(e.text, 4000) || e.audienceIds == null || e.audienceIds.Any(id => !Id(id))) ||
                s.events.GroupBy(e => e.sequence).Any(g => g.Count() > 1)) return Fail(out error, "Invalid event history.");
            if (s.acceptedCommandIds == null || s.acceptedCommandIds.Count > 256 || s.acceptedCommandIds.Any(id => !Text(id, 160)) ||
                s.acceptedCommandIds.Distinct().Count() != s.acceptedCommandIds.Count) return Fail(out error, "Invalid command receipts.");
            if (!Defined(s.evictionStage)) return Fail(out error, "Unsupported eviction stage.");
            // A stage only means anything inside eviction night; anywhere else it must be the one a
            // fresh night opens on, so a stale stage cannot survive into next week.
            if (s.phase != EpisodePhase.Eviction && s.evictionStage != EvictionStage.Interaction)
                return Fail(out error, "An eviction stage cannot outlive eviction night.");
            // Interaction is the campaign phase, so a night that has actually begun is past it.
            // A migrated save may still carry it, which is why this only bites once a ballot exists
            // or the night has resolved — both covered by the two checks below.
            // The stage and the ballots are two records of the same night and must agree. Without
            // this a save can claim the house has not voted while holding its votes, and the only
            // symptom is the night appearing to rewind on load.
            if (s.phase == EpisodePhase.Eviction && s.evictionResolved && s.evictionStage != EvictionStage.Results)
                return Fail(out error, "A resolved eviction is at the results stage.");
            if (s.phase == EpisodePhase.Eviction && !s.evictionResolved && s.votes.Count > 0 &&
                s.evictionStage != EvictionStage.Voting && s.evictionStage != EvictionStage.Tiebreaker)
                return Fail(out error, "Ballots have been cast, so the night is past the speeches.");
            if (s.evictionSpeeches == null || s.evictionSpeeches.Count > 2 ||
                s.evictionSpeeches.Any(x => x == null || !Id(x.speakerId) || x.text == null || x.text.Length > 4000 ||
                    x.week < 1 || x.week > s.week || x.isPlayerAuthored != (x.speakerId == s.playerId)) ||
                s.evictionSpeeches.GroupBy(x => x.speakerId).Any(g => g.Count() > 1))
                return Fail(out error, "Invalid eviction speeches.");
            if (!BlockSpeeches.ValidateReceipts(s, out error)) return false;
            if (!Optional(s.backdoorTargetId) || (s.backdoorTargetId != null && s.backdoorTargetId == s.playerId))
                return Fail(out error, "Invalid backdoor plan.");
            // Bounded at the old flat ceiling rather than at the new budget: a season saved while
            // eighteen actions were legal is still a legal season, and validation may not
            // retroactively reject what the rules allowed when it was written. The budget is
            // enforced where an action is spent, which is the only place it can be.
            if (s.outOfPhaseSocialActions < 0 || s.outOfPhaseSocialActions > MostActionsAWeekCanHold)
                return Fail(out error, "Invalid out-of-phase social action count.");
            // Deals are bounded like promises and for the same reason: nothing legitimately makes
            // hundreds of them, and an unbounded list is a save that grows until it will not load.
            if (s.deals == null || s.deals.Count > 200 ||
                s.deals.Any(d => d == null || !Text(d.id, 160) || !Id(d.proposerId) || !Id(d.recipientId)
                                 || d.proposerId == d.recipientId || !Optional(d.targetId)
                                 || !DealKind.IsKnown(d.type) || !DealStatus.IsKnown(d.status)
                                 || !DealTrust.IsKnown(d.trustImpact)
                                 || d.week < 1 || d.week > s.week
                                 || d.expiresWeek < 0 || d.expiresWeek > 101) ||
                s.deals.GroupBy(d => d.id).Any(g => g.Count() > 1))
                return Fail(out error, "Invalid deal data.");
            // Schema 22 (C0): the week a kept or broken deal was settled - no earlier than it was struck,
            // nor than the rules that write it - and, on a broken one, who broke it, one of its two
            // sides: written together, at the settlement, or not at all. A voting bloc may name
            // nobody, since both walked away from it, and so may a vote deal, which both of its sides
            // can break at the one vote (C1); every other broken deal names somebody. 0 and null on
            // one settled before the rules.
            if (s.deals.Any(d => (d.brokenById != null && (d.status != DealStatus.Broken || d.settledWeek == 0
                        || (d.brokenById != d.proposerId && d.brokenById != d.recipientId)))
                    || d.settledWeek < 0 || d.settledWeek > s.week
                    || (d.settledWeek > 0 && (d.settledWeek < d.week || d.settledWeek < s.commitmentRulesStartWeek
                        || (d.status != DealStatus.Fulfilled && d.status != DealStatus.Broken)
                        || (d.status == DealStatus.Broken && d.brokenById == null
                            && d.type != DealKind.VoteTogether && d.type != DealKind.VoteSave && d.type != DealKind.VoteEvict)))))
                return Fail(out error, "Invalid deal settlement.");
            // Schema 22 (C7): a deal and the price paid for it name each other, both ways - the same two
            // houseguests, struck the same week, exactly one of the two a price (Negotiation.PricePrefix) -
            // and a price is never without what it bought.
            // Validate canonical identities/source shape before any mixed-link or finale reader
            // can project a row. No duplicate or malformed row may turn validation into a throw.
            // Vote's complete preflight follows the common Story container guards below.
            // Neither prospective family uses the legacy raw-only link reader.
            if (prospectiveSafety && !prospectiveVote && !UnifiedSafetySaveReferences.TryValidate(s, out error)) return false;
            foreach (var deal in prospectiveSafety ? Enumerable.Empty<DealState>() : s.deals)
            {
                bool price = Negotiation.IsPrice(deal);
                if (deal.linkedDealId == null)
                {
                    if (price) return Fail(out error, "Invalid deal link.");
                    continue;
                }
                var link = s.deals.FirstOrDefault(x => x.id == deal.linkedDealId);
                if (link == null || ReferenceEquals(link, deal) || link.linkedDealId != deal.id || link.week != deal.week
                    || !((deal.proposerId == link.proposerId && deal.recipientId == link.recipientId)
                         || (deal.proposerId == link.recipientId && deal.recipientId == link.proposerId))
                    || price == Negotiation.IsPrice(link))
                    return Fail(out error, "Invalid deal link.");
            }
            // The commitment rules write those records, so a season that never played them holds none.
            if (s.commitmentRulesStartWeek == 0 && (s.deals.Any(d => d.brokenById != null || d.settledWeek != 0 || d.linkedDealId != null)
                    || s.promises.Any(p => p.brokenById != null || p.settledWeek != 0) || s.alliances.Any(a => a.playerJoined)))
                return Fail(out error, "A season without the commitment rules has none of their records.");
            // Nor a final three deal, their own kind (C9), struck before they began.
            if (s.deals.Any(d => DealKind.CommitmentRulesOnly(d.type) && (s.commitmentRulesStartWeek == 0 || d.week < s.commitmentRulesStartWeek)))
                return Fail(out error, "A season without the commitment rules has none of their records.");
            if (s.dealRulesStartWeek < 1 || s.dealRulesStartWeek > Math.Min(101, s.week + 1))
                return Fail(out error, "A deal rules boundary cannot be further off than next week.");
            // Bought actions are bounded like everything else a player can accumulate: nothing
            // legitimately buys hundreds, and an unbounded counter is a save that stops loading.
            if (s.boughtActionPoints < 0 || s.boughtActionPoints > 100)
                return Fail(out error, "Invalid bought action point count.");
            if (s.houseEvents == null || s.houseEvents.Count > 400 ||
                s.houseEvents.Any(e => e == null || !Text(e.id, 160) || !HouseEventKind.IsKnown(e.kind)
                                       || !Text(e.title, 200) || !Text(e.narrative, 2000)
                                       || e.week < 1 || e.week > s.week
                                       || e.involvedIds == null || e.involvedIds.Count > 32
                                       || e.involvedIds.Any(id => !Id(id))
                                       || e.involvedIds.Distinct(StringComparer.Ordinal).Count() != e.involvedIds.Count
                                       || e.choices == null || e.choices.Count > 8
                                       || e.choices.Any(BadChoice)
                                       // An unresolved event has chosen nothing; a resolved one has
                                       // chosen something that exists. Anything else is a save that
                                       // says a decision was made and cannot say what it was.
                                       || (e.resolved
                                           ? e.chosenIndex < -1 || e.chosenIndex >= e.choices.Count
                                           : e.chosenIndex != -1)
                                       || !ShortOrAbsent(e.outcome, 2000)
                                       || e.choices.Any(c => c.impacts.Any(i => !Optional(i.targetId)))) ||
                s.houseEvents.GroupBy(e => e.id).Any(g => g.Count() > 1))
                return Fail(out error, "Invalid house event data.");
            if (s.eventRulesStartWeek < 1 || s.eventRulesStartWeek > Math.Min(101, s.week + 1))
                return Fail(out error, "An event rules boundary cannot be further off than next week.");
            if (s.storylines == null || s.storylines.Count > 100 ||
                s.storylines.Any(x => x == null || !Text(x.id, 160) || !Text(x.templateId, 160)
                                      || !Text(x.title, 200) || !ShortOrAbsent(x.category, 80)
                                      || !StorylineStatus.IsKnown(x.status)
                                      || !ShortOrAbsent(x.eventId, 160)
                                      || x.week < 1 || x.week > s.week
                                      // A running storyline has not ended; a finished one ended on a
                                      // week the season has actually reached, and never before it began.
                                      || (StorylineStatus.Running(x.status)
                                          ? x.endedWeek != 0
                                          : x.endedWeek < x.week || x.endedWeek > s.week)) ||
                s.storylines.GroupBy(x => x.id).Any(g => g.Count() > 1))
                return Fail(out error, "Invalid storyline data.");
            if (s.activeModifiers == null || s.activeModifiers.Count > 40 ||
                s.activeModifiers.Any(m => m == null || !Text(m.id, 160) || !Text(m.name, 120)
                                           || !ShortOrAbsent(m.description, 500)
                                           || m.weeksLeft < 1 || m.weeksLeft > 20
                                           || !Finite(m.competitionBonus) || Math.Abs(m.competitionBonus) > 20
                                           || !Finite(m.socialBonus) || Math.Abs(m.socialBonus) > 100))
                return Fail(out error, "Invalid story modifier data.");
            if (prospectiveVote)
            {
                // Story validation iterates houseEvents/storylines/modifiers: their common
                // scalar/list-element guards must precede it, including malformed-save paths.
                // The aggregate leaf uses detached unchecked projections, never this full core,
                // and therefore cannot recursively validate or install a candidate.
                if (!TryValidateStory(s, out error) || !TryValidateLedger(s, out error)
                    || !UnifiedVoteFamilyValidation.TryValidate(s, s.unifiedVoteReveals, out error)
                    || !UnifiedSafetySaveReferences.TryValidateProspectiveVote(s, out error)) return false;
            }
            if (s.storyRulesStartWeek < 1 || s.storyRulesStartWeek > Math.Min(101, s.week + 1))
                return Fail(out error, "A storyline rules boundary cannot be further off than next week.");
            // Schema 14: Have-Nots and the veto's prizes. 0 is a season that never plays them.
            if (s.haveNotRulesStartWeek < 0 || s.haveNotRulesStartWeek > Math.Min(101, s.week + 1))
                return Fail(out error, "A Have-Not rules boundary cannot be further off than next week.");
            bool Houseguests(List<string> ids, int most) => ids != null && ids.Count <= most
                && ids.All(id => s.Find(id) != null) && ids.Distinct(StringComparer.Ordinal).Count() == ids.Count;
            if (!Houseguests(s.haveNots, 16) || !Houseguests(s.haveNotPasses, 16) || !Houseguests(s.punishedHaveNots, 16))
                return Fail(out error, "Invalid Have-Not data.");
            if (s.vetoPrizes == null || s.vetoPrizes.Count > 300 ||
                s.vetoPrizes.Any(p => p == null || p.week < 1 || p.week > s.week || s.Find(p.contestantId) == null || HaveNots.Find(p.prizeId) == null))
                return Fail(out error, "Invalid veto prize data.");
            if (s.haveNotRulesStartWeek == 0 && (s.haveNots.Count > 0 || s.haveNotPasses.Count > 0 || s.punishedHaveNots.Count > 0 || s.vetoPrizes.Count > 0))
                return Fail(out error, "A season without Have-Nots has none.");
            // Schema 15: the strategy windows. 0 is a season that never plays them.
            if (s.strategyRulesStartWeek < 0 || s.strategyRulesStartWeek > Math.Min(101, s.week + 1))
                return Fail(out error, "A strategy rules boundary cannot be further off than next week.");
            // Both lists are this week's: the lobbying clears as the week turns, and a reply card is
            // answered in the phase it arrived in or not at all.
            if (s.lobbies == null || s.lobbies.Count > 32 || s.lobbies.Any(l => l == null || l.week != s.week
                    // A plea for a vote is the campaign's (STRATEGY-LOOP-PLAN.md section 3).
                    || (l.phase != EpisodePhase.Nomination && l.phase != EpisodePhase.VetoMeeting && l.phase != EpisodePhase.Campaign)
                    || s.Find(l.deciderId) == null || l.deciderId == s.playerId || !LobbyAsk.IsKnown(l.ask)
                    || !LobbyApproach.IsKnown(l.approach) || !LobbyResponse.IsKnown(l.response)
                    || (l.subjectId != null && s.Find(l.subjectId) == null)
                    || !Finite(l.influence) || Math.Abs(l.influence) > StrategyRules.MostInfluence))
                return Fail(out error, "Invalid lobbying data.");
            if (s.replyCards == null || s.replyCards.Count > 24 || s.replyCards.Any(r => r == null || !Text(r.id, 160)
                    || r.week != s.week || !ReplyCards.IsKnown(r.kind) || s.Find(r.fromId) == null || r.fromId == s.playerId
                    || (r.aboutId != null && s.Find(r.aboutId) == null)
                    || (r.kind == ReplyCards.Pitch ? !HoHPitches.ValidCard(s, r)
                        : s.phase != EpisodePhase.Social && s.phase != EpisodePhase.Campaign))
                || s.replyCards.Select(r => r.id).Distinct(StringComparer.Ordinal).Count() != s.replyCards.Count
                || s.replyCards.Where(r => r.kind == ReplyCards.Pitch).Select(r => r.fromId).Distinct(StringComparer.Ordinal).Count()
                    != s.replyCards.Count(r => r.kind == ReplyCards.Pitch))
                return Fail(out error, "Invalid reply card data.");
            if (s.strategyRulesStartWeek == 0 && (s.lobbies.Count > 0 || s.replyCards.Count > 0))
                return Fail(out error, "A season without the strategy windows has none of their records.");
            if (s.openingBeatsSeen == null || s.openingBeatsSeen.Count > 16 ||
                s.openingBeatsSeen.Any(beat => !Text(beat, 100)) ||
                s.openingBeatsSeen.Distinct(StringComparer.Ordinal).Count() != s.openingBeatsSeen.Count)
                return Fail(out error, "Invalid opening sequence progress.");
            var activeCount = s.Active.Count();
            if (s.phase == EpisodePhase.Finished)
            {
                if (activeCount != 0 || s.contestants.Count(c => c.status == ContestantStatus.Winner) != 1 || s.contestants.Count(c => c.status == ContestantStatus.RunnerUp) != 1 ||
                    s.Find(s.winnerId)?.status != ContestantStatus.Winner || s.Find(s.runnerUpId)?.status != ContestantStatus.RunnerUp) return Fail(out error, "Invalid terminal result.");
            }
            else if (activeCount < 2 || s.contestants.Any(c => c.status == ContestantStatus.Winner || c.status == ContestantStatus.RunnerUp)) return Fail(out error, "Invalid active cast.");
            if ((s.phase == EpisodePhase.Jury || s.phase == EpisodePhase.JuryQuestioning || s.phase == EpisodePhase.FinalSpeeches) && activeCount != 2)
                return Fail(out error, "Jury requires two finalists.");
            if (s.phase == EpisodePhase.Social && activeCount < 3) return Fail(out error, "Free time requires at least three active contestants.");
            if (s.phase == EpisodePhase.Nomination && s.nominees.Count == 1) return Fail(out error, "Nomination requires an empty or complete pair.");
            if ((s.phase == EpisodePhase.FinalHoHPart1 || s.phase == EpisodePhase.FinalHoHPart2 || s.phase == EpisodePhase.FinalHoHPart3 || s.phase == EpisodePhase.FinalEviction) && activeCount != 3)
                return Fail(out error, "Final HoH requires three contestants.");
            if ((s.phase == EpisodePhase.Nomination || s.phase == EpisodePhase.VetoSelection || s.phase == EpisodePhase.Veto || s.phase == EpisodePhase.VetoMeeting || s.phase == EpisodePhase.Campaign || s.phase == EpisodePhase.Eviction) && !Id(s.hohId))
                return Fail(out error, "This phase requires a HoH.");
            if ((s.phase == EpisodePhase.VetoSelection || s.phase == EpisodePhase.Veto || s.phase == EpisodePhase.VetoMeeting || s.phase == EpisodePhase.Campaign || s.phase == EpisodePhase.Eviction) && s.nominees.Count != 2)
                return Fail(out error, "This phase requires two nominees.");
            if ((s.phase == EpisodePhase.VetoMeeting || s.phase == EpisodePhase.Campaign || s.phase == EpisodePhase.Eviction) && !Id(s.vetoHolderId)) return Fail(out error, "This phase requires a veto holder.");
            bool Live(string id) => s.Active.Any(c => c.id == id);
            bool weekly = s.phase == EpisodePhase.HoH || s.phase == EpisodePhase.Nomination || s.phase == EpisodePhase.VetoSelection ||
                s.phase == EpisodePhase.Veto || s.phase == EpisodePhase.VetoMeeting || s.phase == EpisodePhase.Campaign || s.phase == EpisodePhase.Eviction;
            if (weekly && !(s.phase == EpisodePhase.Eviction && s.evictionResolved) && activeCount < 4) return Fail(out error, "Regular weeks require at least four active houseguests.");
            if (weekly && s.phase != EpisodePhase.HoH && !Live(s.hohId)) return Fail(out error, "The current HoH must be active.");
            if (weekly && s.nominees.Contains(s.hohId)) return Fail(out error, "The HoH cannot be nominated.");
            if (weekly && !s.evictionResolved && s.nominees.Any(id => !Live(id))) return Fail(out error, "Current nominees must be active.");
            if ((s.phase == EpisodePhase.Veto || s.phase == EpisodePhase.VetoMeeting || s.phase == EpisodePhase.Campaign || (s.phase == EpisodePhase.Eviction && !s.evictionResolved)) &&
                (s.vetoPlayers.Count != EpisodeEngine.VetoPlayerCount(activeCount) || s.vetoPlayers.Any(id => !Live(id)))) return Fail(out error, "The veto lineup must seat " + EpisodeEngine.VetoPlayerCount(activeCount) + " active houseguests.");
            if ((s.phase == EpisodePhase.VetoMeeting || s.phase == EpisodePhase.Campaign || s.phase == EpisodePhase.Eviction) && !s.vetoPlayers.Contains(s.vetoHolderId)) return Fail(out error, "The veto holder must have competed.");
            if ((s.phase == EpisodePhase.Campaign || s.phase == EpisodePhase.Eviction) && !s.vetoResolved) return Fail(out error, "Campaigning requires a committed veto decision.");
            if (s.competitionResolved && EpisodeEngine.IsCompetition(s.phase))
            {
                var participants = EpisodeEngine.CompetitionPlayers(s).Select(c => c.id).ToArray();
                if (participants.Length == 0 || s.competitionScores.Count != participants.Length || s.competitionScores.Any(c => !participants.Contains(c.contestantId))) return Fail(out error, "Resolved competition scores do not match the participants.");
                var winner = s.competitionScores.OrderByDescending(c => c.score).First().contestantId;
                string recorded = s.phase == EpisodePhase.Veto ? s.vetoHolderId : s.phase == EpisodePhase.FinalHoHPart1 ? s.finalPart1WinnerId : s.phase == EpisodePhase.FinalHoHPart2 ? s.finalPart2WinnerId : s.hohId;
                if (recorded != winner || !Live(recorded)) return Fail(out error, "Resolved competition is missing its coherent winner.");
            }
            if ((s.phase == EpisodePhase.FinalHoHPart2 || s.phase == EpisodePhase.FinalHoHPart3 || s.phase == EpisodePhase.FinalEviction) && !Live(s.finalPart1WinnerId)) return Fail(out error, "Final stage is missing the first-part winner.");
            if ((s.phase == EpisodePhase.FinalHoHPart3 || s.phase == EpisodePhase.FinalEviction) && (!Live(s.finalPart2WinnerId) || s.finalPart1WinnerId == s.finalPart2WinnerId)) return Fail(out error, "Final stage requires two distinct qualifying winners.");
            if (s.phase == EpisodePhase.FinalEviction && (!Live(s.hohId) || (s.hohId != s.finalPart1WinnerId && s.hohId != s.finalPart2WinnerId))) return Fail(out error, "Final HoH must be a qualified finalist.");
            if (s.phase == EpisodePhase.Eviction && s.votes.Any(v => !s.nominees.Contains(v.targetId) || s.nominees.Contains(v.voterId))) return Fail(out error, "Invalid eviction ballot eligibility.");
            if (s.phase == EpisodePhase.Eviction && s.votes.Any(v => !Live(v.voterId))) return Fail(out error, "Only active houseguests may cast eviction ballots.");
            if (s.phase == EpisodePhase.Eviction && s.votes.Any(v => v.voterId == s.hohId))
            {
                var regular = EpisodeEngine.Voters(s).ToArray();
                if (!regular.All(c => s.votes.Any(v => v.voterId == c.id)) || s.nominees.Select(id => s.votes.Count(v => v.voterId != s.hohId && v.targetId == id)).Distinct().Count() != 1)
                    return Fail(out error, "HoH ballots are valid only after a complete tied vote.");
            }
            if (s.phase == EpisodePhase.Jury && s.votes.Any(v => !Live(v.targetId) || Live(v.voterId))) return Fail(out error, "Invalid jury ballot eligibility.");
            // Prospective finale readers resolve canonical and ledger references. Validate their
            // containers before those readers run, rather than letting a malformed saved list throw.
            // The ordinary disabled entry keeps its existing validation order and behavior.
            if (prospectiveSafety && (!TryValidateStory(s, out error) || !TryValidateLedger(s, out error))) return false;
            return TryValidateV2(s, out error) && TryValidateV3(s, out error) && TryValidateNpcSocial(s, out error)
                && TryValidateStory(s, out error) && TryValidateLedger(s, out error) && TryValidateWaveD(s, out error)
                && (!prospectiveSafety || TryValidateUnifiedSafetyReferences(s, prospectiveVote, out error))
                && (!prospectiveVote || TryValidateUnifiedVoteReferences(s, out error));
        }

        /// <summary>
        /// Absent, or present and within bounds.
        ///
        /// <para>Not called <c>Optional</c>: <see cref="TryValidate"/> declares a local function of
        /// that name for optional <i>identities</i>, and a local function hides the enclosing type's
        /// methods outright rather than overloading them.</para>
        /// </summary>
        private static bool ShortOrAbsent(string value, int max) => string.IsNullOrEmpty(value) || value.Length <= max;

        /// <summary>
        /// Whether one way of answering an event is malformed.
        ///
        /// <para>Split out because the expression that walks the event list is already the longest
        /// condition in this file, and a choice has enough of its own shape to be worth naming. The
        /// target of an impact is checked by the caller, which is the only place the cast is in
        /// scope.</para>
        /// </summary>
        private static bool BadChoice(HouseEventChoice choice) =>
            choice == null || !Text(choice.label, 120) || !ShortOrAbsent(choice.description, 500)
            || !HouseEventRisk.IsKnown(choice.risk)
            || !Finite(choice.trustChange) || Math.Abs(choice.trustChange) > 100
            || choice.impacts == null || choice.impacts.Count > 32
            || choice.impacts.Any(i => i == null || !Finite(i.amount) || Math.Abs(i.amount) > 100);

        /// <summary>
        /// The most actions one counter can legally hold.
        ///
        /// <para>The old flat allowance plus everything a player may buy on top of it. A season
        /// still under the legacy boundary gets eighteen for free and may buy six more, and all
        /// twenty-four can land in the same phase — so a bound of eighteen would refuse a season
        /// that had done nothing wrong.</para>
        /// </summary>
        private const int MostActionsAWeekCanHold =
            EpisodeEngine.LegacySocialActionBudget + WebSocialVocabulary.PurchaseCeiling;

        private static bool Text(string value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max;
        private static bool Defined<T>(T value) where T : struct => Enum.IsDefined(typeof(T), value);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool Fail(out string error, string message) { error = message; return false; }
    }
}
