// Frozen29 (SCH30): the schema-29 whole reader, carried from FrozenV28ReadOnly.Core.cs and then by exactly the live
// validators' changes from 515246bf to b208d46d (ALL-STAR-HISTORIES-PLAN 8.3). Provenance comments below
// describe the schema-28 image this copy started from.
// UNWIRED fixed29 semantic source transplant. No runtime/current Simulation dependencies.
// All source roots are the immutable 515246bf/status61 schema29 image.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Persistence.Frozen29Data;

namespace Gamesim.Persistence.Frozen29ReadOnly
{
    internal static class EpisodeSaveValidation
    {
        // SOURCE Gamesim.Persistence.EpisodeSaveValidation.Require 2449:2597
        private static void Require(bool condition, string message)
                {
                    if (!condition) throw new InvalidDataException(message);
                }

        // SOURCE Gamesim.Persistence.EpisodeSaveValidation.Validate 253:2439
        public static void Validate(EpisodeState state)
                {
                    if (!EpisodeValidation.TryValidate(state, out var reason)) throw new InvalidDataException(reason);
                    // DELTA b208d46d: schema 29.
                    Require(state.schemaVersion == 29, "Unsupported simulation schema version.");
                    // The shared complete validator owns mode dispatch, prerequisites, source provenance,
                    // canonical/mixed links and durable hearings. Never bypass it or rebuild raw mirrors.
                    Require(state.sessionId != null && state.sessionId.Length <= 256, "Session identifier is too long.");
                    Require(state.week <= 10000 && state.promises.Count <= 10000 && state.alliances.Count <= 1000
                        && state.memories.Count <= 100000 && state.events.Count <= 100000
                        && state.acceptedCommandIds.Count <= 100000, "Save exceeds supported collection limits.");
                    Require(state.contestants.All(actor => actor.id.Length <= 128 && actor.name.Length <= 128
                        && actor.traits.Count <= 64 && actor.traits.All(trait => trait != null && trait.Length <= 128)),
                        "Contestant text exceeds supported limits.");
                    Require(state.memories.All(memory => memory.text != null && memory.text.Length <= 16384)
                        && state.events.All(item => item.text != null && item.text.Length <= 16384),
                        "Saved history text is missing or too long.");
                    // Simulation validation owns phase/reference/enum semantics and exact
                    // per-record invariants. Persistence additionally bounds saved prose.
                    Require(state.relationships.All(edge => edge.notes.All(note => note.Length <= 16384)
                        && edge.events.All(item => item.description.Length <= 16384)), "Relationship history text is too long.");
                    Require(state.relationshipArcs.All(arc => arc.weeklyHistory.All(item => item.reason.Length <= 16384)),
                        "Relationship arc history text is too long.");
                    Require(state.finalSpeeches.All(speech => speech.text.Length <= 16384), "Final speech is too long.");
                    // randomState is deliberately not normalized: zero is a valid wrapped generator state.
                }
    }

    internal static class EpisodeValidation
    {
        // SOURCE FIELD Gamesim.Simulation.EpisodeValidation.MinimumCast,MaximumCast @ Assets/Gamesim/Simulation/EpisodeValidation.cs:741
        public const int MinimumCast = 3, MaximumCast = 16;

        // SOURCE FIELD Gamesim.Simulation.EpisodeValidation.MostActionsAWeekCanHold @ Assets/Gamesim/Simulation/EpisodeValidation.cs:43816
        private const int MostActionsAWeekCanHold =
                    EpisodeEngine.LegacySocialActionBudget + WebSocialVocabulary.PurchaseCeiling;

        // SOURCE Gamesim.Simulation.EpisodeValidation.BadChoice 42895:43350
        private static bool BadChoice(HouseEventChoice choice) =>
                    choice == null || !Text(choice.label, 120) || !ShortOrAbsent(choice.description, 500)
                    || !HouseEventRisk.IsKnown(choice.risk)
                    || !Finite(choice.trustChange) || Math.Abs(choice.trustChange) > 100
                    || choice.impacts == null || choice.impacts.Count > 32
                    || choice.impacts.Any(i => i == null || !Finite(i.amount) || Math.Abs(i.amount) > 100);

        // SOURCE Gamesim.Simulation.EpisodeValidation.BadEffect 12847:13371
        private static bool BadEffect(StoryEffectState e, Func<string, bool> optionalId) =>
                    e == null || !StoryEffects.IsKnown(e.kind)
                    || !(optionalId(e.fromId) || e.fromId == StoryEffects.Picked)
                    || !(optionalId(e.toId) || e.toId == StoryEffects.Picked)
                    || !(optionalId(e.thirdId) || e.thirdId == StoryEffects.Picked)
                    || !ShortOrAbsent(e.type, 60) || !ShortOrAbsent(e.text, 300)
                    || !Finite(e.amount) || Math.Abs(e.amount) > 200 || e.weeks < 0 || e.weeks > 20;

        // SOURCE Gamesim.Simulation.EpisodeValidation.Defined 44076:44169
        private static bool Defined<T>(T value) where T : struct => Enum.IsDefined(typeof(T), value);

        // SOURCE Gamesim.Simulation.EpisodeValidation.Fail 44281:44374
        private static bool Fail(out string error, string message) { error = message; return false; }

        // SOURCE Gamesim.Simulation.EpisodeValidation.Finite 44178:44272
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        // SOURCE Gamesim.Simulation.EpisodeValidation.SavedSafetySequence 18953:19344
        private static bool SavedSafetySequence(string id, string prefix, int next) => id != null
                    && id.StartsWith(prefix, StringComparison.Ordinal)
                    && int.TryParse(id.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int sequence)
                    && sequence >= 1 && sequence < next && id == prefix + sequence.ToString(CultureInfo.InvariantCulture);

        // SOURCE Gamesim.Simulation.EpisodeValidation.SavedSafetyWeek 19354:19622
        private static bool SavedSafetyWeek(string text, int current, out int week) =>
                    int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out week)
                    && week >= 1 && week <= current && text == week.ToString(CultureInfo.InvariantCulture);

        // SOURCE Gamesim.Simulation.EpisodeValidation.SavedSafetyWordWasOn 13779:14064
        private static bool SavedSafetyWordWasOn(EpisodeState s, int week) =>
                    s.commitmentRulesStartWeek >= 1 && s.commitmentRulesStartWeek <= week
                    && s.story.rulesStartWeek >= 1 && s.story.rulesStartWeek <= week
                    && s.story.rulesVersion >= StoryRules.Bonds;

        // SOURCE Gamesim.Simulation.EpisodeValidation.ShortOrAbsent 42329:42440
        private static bool ShortOrAbsent(string value, int max) => string.IsNullOrEmpty(value) || value.Length <= max;

        // SOURCE Gamesim.Simulation.EpisodeValidation.Text 43959:44067
        private static bool Text(string value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max;

        // SOURCE Gamesim.Simulation.EpisodeValidation.TryValidate 802:2230
        public static bool TryValidate(EpisodeState s, out string error)
                {
                    // Each supported canonical mode selects its own complete core. Legacy saves stay
                    // disabled; validation never activates, converts or clears any stored authority.
                    // Mode 2, the unified vote rules (vote family V6): a fresh season's since V6
                    // (ShippedRules.ApplyFresh), held to the complete Vote core - its prerequisites, the
                    // deal pass from the first week, every archive frame, row, ending and saved reference.
                    if (s != null && s.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version)
                        return TryValidateProspectiveUnifiedVote(s, out error);
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

        // SOURCE Gamesim.Simulation.EpisodeValidation.TryValidateCore 4449:4651
        private static bool TryValidateCore(EpisodeState s, bool prospectiveSafety, out string error) =>
                    TryValidateCore(s, prospectiveSafety ? UnifiedCommitments.ProspectiveVersion : 0, out error);

        // SOURCE Gamesim.Simulation.EpisodeValidation.TryValidateCore 4661:41943
        private static bool TryValidateCore(EpisodeState s, int canonicalMode, out string error)
                {
                    bool prospectiveVote = canonicalMode == UnifiedVoteFamilyValidation.Version;
                    bool prospectiveSafety = canonicalMode == UnifiedCommitments.ProspectiveVersion || prospectiveVote;
                    error = null;
                    // DELTA b208d46d (schema 29, 9d87b59e): the schema, the dialogue pilot's version and inert storage, and
                    // the dialogue domain's own validator, in the live order. A frozen reader holds no pending command.
                    if (s == null || s.schemaVersion != 29) return Fail(out error, "Unsupported episode schema.");
                    // The unpublished pilot has a complete local semantic owner. Unknown versions still
                    // refuse; default/legacy rules stay inert and keep their original storage diagnostics.
                    if (s.dialogueRulesVersion != 0 && s.dialogueRulesVersion != DialogueRules.Version)
                        return Fail(out error, "Dialogue rules are not enabled in this build.");
                    if (s.dialogueRulesVersion == 0 && (s.dialogue == null || s.dialogue.episodes == null || s.dialogue.grants == null
                        || s.dialogue.vetoOccasions == null || s.dialogue.episodes.Count != 0
                        || s.dialogue.grants.Count != 0 || s.dialogue.vetoOccasions.Count != 0
                        || (s.unifiedCommitments != null && s.unifiedCommitments.Any(row => row?.conditionalVeto != null))))
                        return Fail(out error, "Disabled dialogue storage must remain complete and inert.");
                    if (!DialogueRules.TryValidate(s, out error)) return false;
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
                            return Fail(out error, "The unified vote rules require bounded complete reveal evidence.");
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
                    // DELTA b208d46d (role moments R4): nor a keep-the-block deal, the role moments' own, by its kind or by
                    // its id, from a week they did not play in.
                    if (s.deals.Any(d => (DealKind.RolesOnly(d.type) || NpcDeals.IsKeepTheBlockId(d.id)) && !EpisodeEngine.RolesOnIn(s, d.week)))
                        return Fail(out error, "A season without the role moments has none of their records.");
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
                            != s.replyCards.Count(r => r.kind == ReplyCards.Pitch)
                        // DELTA b208d46d (role moments R5): under the role moments a nomination brings at most three
                        // pitchers to the player: those still waiting on the board and those already answered this week.
                        || (EpisodeEngine.RolesOn(s) && s.replyCards.Where(r => r.kind == ReplyCards.Pitch).Select(r => r.fromId)
                            .Concat(s.ledger?.replies?.Where(r => r != null && r.week == s.week && r.kind == ReplyCards.Pitch).Select(r => r.fromId)
                                ?? Enumerable.Empty<string>())
                            .Distinct(StringComparer.Ordinal).Count() > HoHPitches.BoardSize))
                        return Fail(out error, "Invalid reply card data.");
                    if (s.strategyRulesStartWeek == 0 && (s.lobbies.Count > 0 || s.replyCards.Count > 0))
                        return Fail(out error, "A season without the strategy windows has none of their records.");
                    if (s.openingBeatsSeen == null || s.openingBeatsSeen.Count > 16 ||
                        s.openingBeatsSeen.Any(beat => !Text(beat, 100)) ||
                        s.openingBeatsSeen.Distinct(StringComparer.Ordinal).Count() != s.openingBeatsSeen.Count)
                        return Fail(out error, "Invalid opening sequence progress.");
                    var activeCount = LiteralRows.Active(s).Count();
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
                    bool Live(string id) => LiteralRows.Active(s).Any(c => c.id == id);
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

        // SOURCE Gamesim.Simulation.EpisodeValidation.TryValidateLedger 472:9967
        private static bool TryValidateLedger(EpisodeState s, out string error)
                {
                    error = null;
                    var l = s.ledger;
                    if (l == null) return Fail(out error, "Missing season ledger.");
                    bool Id(string id) => id != null && s.contestants.Any(c => c.id == id);
                    bool OptionalId(string id) => string.IsNullOrEmpty(id) || Id(id);
                    bool Week(int week) => week >= 1 && week <= s.week;
                    int most = LedgerLimits.MostRows;
        
                    if (l.dropped < 0 || l.dropped > 1000000) return Fail(out error, "Invalid ledger drop count.");
                    if (l.claims == null || l.claims.Count > most || l.claims.Any(k => k == null || !Id(k.voterId) || !Id(k.targetId)
                            || k.voterId == k.targetId || !Week(k.week) || !ClaimSource.IsKnown(k.source) || !ClaimStatus.IsKnown(k.status)))
                        return Fail(out error, "Invalid vote claim data.");
                    if (l.standings == null || l.standings.Count > most || l.standings.Any(r => r == null || !Id(r.fromId) || !Id(r.toId)
                            || r.fromId == r.toId || !Week(r.week) || !ClaimSource.IsKnown(r.source) || !Finite(r.score) || r.score < -1000 || r.score > 1000))
                        return Fail(out error, "Invalid standing data.");
                    if (l.ballots == null || l.ballots.Count > most || l.ballots.Any(b => b == null || !Id(b.voterId) || !Id(b.targetId)
                            || b.voterId == b.targetId || !OptionalId(b.readBefore) || !Week(b.week)))
                        return Fail(out error, "Invalid ballot record data.");
                    if (l.opportunities == null || l.opportunities.Count > most || l.opportunities.Any(o => o == null || !Text(o.id, 160)
                            || !OpportunityKinds.IsKnown(o.kind) || !ShortOrAbsent(o.anchor, 64) || !ShortOrAbsent(o.source, 160)
                            || !OpportunityResponse.IsKnown(o.response) || !OpportunityOutcome.IsKnown(o.outcome)
                            || !(string.IsNullOrEmpty(o.currency) || PayoffCurrency.IsKnown(o.currency)) || !ShortOrAbsent(o.note, 1000)
                            || !Week(o.week) || !Finite(o.payoff) || o.steps == null || o.steps.Count > 32
                            || o.steps.Any(step => step == null || !ShortOrAbsent(step.at, 64) || !ShortOrAbsent(step.choice, 160)))
                        || l.opportunities.GroupBy(o => o.id).Any(g => g.Count() > 1))
                        return Fail(out error, "Invalid opportunity data.");
                    if (l.competitions == null || l.competitions.Count > most || l.competitions.Any(c => c == null || !Week(c.week)
                            || !ShortOrAbsent(c.kind, 64) || !CompetitionEntry.IsKnown(c.entry) || c.field < 0 || c.field > MaximumCast
                            || c.placement < 0 || c.placement > MaximumCast || !Finite(c.performance) || c.performance < 0 || c.performance > 1
                            || !Finite(c.expectedWin) || c.expectedWin < 0 || c.expectedWin > 1))
                        return Fail(out error, "Invalid competition record data.");
                    if (l.power == null || l.power.Count > most || l.power.Any(p => p == null || !Week(p.week) || !OptionalId(p.hohId)
                            || !OptionalId(p.vetoHolderId) || !OptionalId(p.savedId) || !OptionalId(p.replacementId) || !OptionalId(p.evicteeId)
                            || !OptionalId(p.backdoorTargetId) || !ShortOrAbsent(p.backdoorResult, 64) || p.nominees == null || p.nominees.Count > 2
                            || p.nominees.Any(id => !Id(id)) || p.tally == null || p.tally.Count > 2 || p.tally.Any(n => n < 0 || n > MaximumCast)))
                        return Fail(out error, "Invalid power record data.");
                    if (l.alliances == null || l.alliances.Count > most || l.alliances.Any(a => a == null || !Text(a.id, 160)
                            || !ShortOrAbsent(a.why, 160) || !Week(a.startedWeek) || (a.endedWeek != 0 && !Week(a.endedWeek))))
                        return Fail(out error, "Invalid alliance record data.");
                    if (l.replies == null || l.replies.Count > most || l.replies.Any(r => r == null || !Text(r.cardId, 160) || !ShortOrAbsent(r.kind, 64)
                            || !Id(r.fromId) || !OptionalId(r.listenerId) || !ShortOrAbsent(r.replyKey, 64) || !Week(r.week)
                            || !Finite(r.toThem) || r.toThem < -100 || r.toThem > 100
                            || (r.kind == ReplyCards.Pitch && (s.economyRulesVersion != 1 || r.week < s.weekRulesStartWeek
                                || r.fromId == s.playerId || r.promised || r.listenerId == r.fromId || r.listenerId == s.playerId
                                || (r.replyKey == HoHPitches.FeelOutKey ? r.toThem != 0
                                    : ReplyCards.Find(ReplyCards.Pitch, r.replyKey) == null
                                        // DELTA b208d46d (role moments R5): the answer's own number; in a week of the
                                        // role moments' pitch board what the board's answer moves.
                                        || !HoHPitches.RecordedImpact(s, r)))))
                        || l.replies.Where(r => r.kind == ReplyCards.Pitch).GroupBy(r => r.week).Any(g => g.Count() > 48)
                        // DELTA b208d46d (R5): the role moments' board brings a nomination at most three pitchers.
                        || l.replies.Where(r => r.kind == ReplyCards.Pitch && EpisodeEngine.RolesOnIn(s, r.week)).GroupBy(r => r.week)
                            .Any(g => g.Select(r => r.fromId).Distinct(System.StringComparer.Ordinal).Count() > HoHPitches.BoardSize)
                        || l.replies.Where(r => r.kind == ReplyCards.Pitch)
                            .GroupBy(r => new { r.week, r.fromId, inspected = r.replyKey == HoHPitches.FeelOutKey }).Any(g => g.Count() > 1))
                        return Fail(out error, "Invalid reply record data.");
                    if (l.calls == null || l.calls.Count > most || l.calls.Any(c => c == null || !Text(c.allianceId, 160) || !Id(c.callerId) || !Id(c.targetId)
                            || !Week(c.week) || c.followed == null || c.followed.Count > MaximumCast || c.followed.Any(id => !Id(id))
                            || c.defected == null || c.defected.Count > MaximumCast || c.defected.Any(id => !Id(id))))
                        return Fail(out error, "Invalid bloc call record data.");
                    // Schema 28: D3's war-room plans (WAVE-D-NPC-PACTS-PLAN §3.3), none while its start week is 0
                    // and none before it. These are the storage bounds; D3's engine slice adds the call links below.
                    if (l.plans == null || l.plans.Any(p => p == null))
                        return Fail(out error, "Invalid pact plan data.");
                    if (s.pactPlanRulesStartWeek == 0 && l.plans.Count != 0)
                        return Fail(out error, "A season without the war rooms has none of their plans.");
                    bool Ids(System.Collections.Generic.List<string> ids) => ids != null && ids.Count <= MaximumCast
                        && ids.All(Id) && ids.Distinct(System.StringComparer.Ordinal).Count() == ids.Count;
                    if (l.plans.Count > most || l.plans.Any(p => !Week(p.week) || p.week < s.pactPlanRulesStartWeek
                            || !Text(p.allianceId, 160) || !Id(p.throughId) || !PactPlanStance.IsKnown(p.stance)
                            || !Ids(p.present) || !Ids(p.cameRound) || !Ids(p.followed)
                            || p.cameRound.Any(id => !p.present.Contains(id)) || p.followed.Any(id => !p.present.Contains(id))
                            || p.says == null || p.says.Count > MaximumCast || p.says.Any(say => say == null || !Id(say.memberId) || !Id(say.targetId))
                            || p.says.Select(say => say.memberId).Distinct(System.StringComparer.Ordinal).Count() != p.says.Count
                            || !OptionalId(p.counterId) || !OptionalId(p.targetId) || !OptionalId(p.callerId)
                            // An open plan is this campaign's: Social is phase 0, so "at most Campaign" would admit the next week.
                            || (p.stance == PactPlanStance.Open && (s.phase != EpisodePhase.Campaign || p.week != s.week)))
                        || l.plans.GroupBy(p => new { p.week, p.allianceId }).Any(g => g.Count() > 1))
                        return Fail(out error, "Invalid pact plan data.");
                    // D3's engine slice: what each stance holds, and the call links. An open plan names no target,
                    // caller or counter and binds nobody yet; a void one names nobody either; every other settled one
                    // names its target and its caller. The player says nothing at the table and is never bound.
                    if (l.plans.Any(p => (p.stance == PactPlanStance.Open || p.stance == PactPlanStance.Void)
                            ? !string.IsNullOrEmpty(p.targetId) || !string.IsNullOrEmpty(p.callerId) || !string.IsNullOrEmpty(p.counterId)
                              || p.cameRound.Count != 0 || p.followed.Count != 0
                            : !Id(p.targetId) || !Id(p.callerId))
                        || l.plans.Any(p => p.followed.Contains(s.playerId) || p.cameRound.Contains(s.playerId) || p.says.Any(say => say.memberId == s.playerId)))
                        return Fail(out error, "Invalid pact plan data.");
                    // A plan the player backs is that week's call in their name: exactly one call row of the pact that
                    // week, its target and who is with it, nobody defected - at least one while the calls have never
                    // reached their cap. Every call row in a week a pact planned belongs to its plan.
                    foreach (var p in l.plans)
                    {
                        var calls = l.calls.Where(k => k.week == p.week && k.allianceId == p.allianceId).ToList();
                        bool backed = !string.IsNullOrEmpty(p.callerId) && p.callerId == s.playerId;
                        if (!backed ? calls.Count != 0
                                : calls.Count > 1 || (calls.Count == 0 && l.calls.Count < most)
                                  || calls.Any(k => k.callerId != s.playerId || k.targetId != p.targetId || k.defected.Count != 0
                                      || !k.followed.SequenceEqual(p.followed)))
                            return Fail(out error, "A pact plan the player backs is that week's call in their alliance, and only it.");
                    }
                    return true;
                }

        // SOURCE Gamesim.Simulation.EpisodeValidation.TryValidateNpcBeats 7220:10386
        private static bool TryValidateNpcBeats(EpisodeState state, out string error)
                {
                    error = null;
                    var social = state.npcSocial;
                    if (social.beatPlan == null || social.acts == null || social.acts.Any(act => act == null))
                        return Fail(out error, "Invalid NPC beat collections.");
                    if (state.allWeekRulesStartWeek == 0)
                    {
                        if (social.beatWeek != 0 || social.beatWindow != Windows.None || social.beatsFired != 0 || social.beatSeats != 0
                            || social.beatPlan.Count != 0 || social.acts.Count != 0)
                            return Fail(out error, "A season without the all-week rules has no NPC beats.");
                        return true;
                    }
                    bool Npc(string id) => state.contestants.Any(actor => actor.id == id && !actor.isPlayer);
                    bool Party(string id) => state.contestants.Any(actor => actor.id == id);
                    if (social.beatWindow < Windows.None || social.beatWindow >= Windows.Count
                        || social.beatsFired < 0 || social.beatSeats < 0 || social.beatSeats > MostActionsAWeekCanHold
                        || social.beatPlan.Count > MaximumCast - 1 || social.beatsFired > social.beatPlan.Count
                        || social.beatPlan.Any(id => !Npc(id)) || social.beatPlan.Distinct(StringComparer.Ordinal).Count() != social.beatPlan.Count
                        || (social.beatWindow == Windows.None
                            ? social.beatWeek != 0 || social.beatsFired != 0 || social.beatSeats != 0 || social.beatPlan.Count != 0
                            : social.beatWeek < state.allWeekRulesStartWeek || social.beatWeek > state.week))
                        return Fail(out error, "Invalid NPC beat plan.");
                    // This week's acts only: they are cleared as the week turns. A sighted act is one the
                    // player saw, at most three a window; an overheard one was seen too.
                    bool Pact(string id) => state.alliances.Any(alliance => alliance != null && alliance.id == id);
                    if (social.acts.Count > EpisodeEngine.MostActs(state)
                        || (social.acts.Count > 0 && EpisodeEngine.IsFirstNight(state))
                        || social.acts.Any(act => !Text(act.id, 160) || !NpcActKinds.IsKnown(act.kind) || !Party(act.actorId)
                            || (act.partnerId != null && !Party(act.partnerId))
                            || (act.kind == NpcActKinds.Meet ? !Pact(act.subjectId) : act.subjectId != null && !Party(act.subjectId))
                            || (act.room != null && !RoomWords.IsRoom(act.room)) || act.week != state.week || act.week < state.allWeekRulesStartWeek
                            || act.window < 0 || act.window >= Windows.Count || act.firedTick < 0 || act.firedTick > MostActionsAWeekCanHold
                            || (act.overheard && !act.sighted) || (act.sighted && act.room == null))
                        || social.acts.Select(act => act.id).Distinct(StringComparer.Ordinal).Count() != social.acts.Count
                        || social.acts.Where(act => act.sighted).GroupBy(act => act.window).Any(group => group.Count() > 3))
                        return Fail(out error, "Invalid NPC acts.");
                    return true;
                }

        // SOURCE Gamesim.Simulation.EpisodeValidation.TryValidateNpcSocial 129:6538
        private static bool TryValidateNpcSocial(EpisodeState state, out string error)
                {
                    error = null;
                    var social = state.npcSocial;
                    if (social == null || social.rulesStartWeek < 1 || social.rulesStartWeek > 101 || social.rulesStartWeek > state.week + 1)
                        return Fail(out error, "Invalid NPC social activation week.");
                    // Each clock commit advances one whole second; a tick can issue two disjoint starts.
                    // Bulk multi-tick commits are intentionally not part of this persisted contract.
                    if (social.clockTick < 0 || social.clockTick > state.revision || social.nextScanTick <= social.clockTick ||
                        social.nextScanTick > social.clockTick + 3 || social.nextScanTick % 3 != 0 ||
                        social.nextConversationSequence < 1 || social.nextConversationSequence - 1 > 2L * state.revision)
                        return Fail(out error, "Invalid NPC social clock or issued sequence.");
                    // One cooldown per NPC, and one memory row per ordered pair of NPCs. Both read as the
                    // literals 5 and 20 while a house held six, which is what those numbers are: five NPCs
                    // and their twenty directed pairs.
                    int npcs = Math.Max(0, state.contestants.Count - 1);
                    if (social.pending == null || social.pending.Count > 2 || social.pending.Any(row => row == null) ||
                        social.cooldowns == null || social.cooldowns.Count > npcs || social.cooldowns.Any(row => row == null) ||
                        social.pairMemory == null || social.pairMemory.Count > npcs * Math.Max(0, npcs - 1) || social.pairMemory.Any(row => row == null))
                        return Fail(out error, "Invalid NPC social collections.");
                    bool Npc(string id) => state.contestants.Any(actor => actor.id == id && !actor.isPlayer);
                    bool ActiveNpc(string id) => state.contestants.Any(actor => actor.id == id && !actor.isPlayer && actor.status == ContestantStatus.Active);
                    if (social.cooldowns.Any(row => !Npc(row.npcId) || row.untilTick < 0 || row.untilTick > social.clockTick + 15) ||
                        social.cooldowns.Select(row => row.npcId).Distinct(StringComparer.Ordinal).Count() != social.cooldowns.Count)
                        return Fail(out error, "Invalid NPC conversation cooldowns.");
                    if (social.pairMemory.Any(row => !Npc(row.fromId) || !Npc(row.toId) || row.fromId == row.toId ||
                        !NpcSocialRules.IsKnownTopic(row.lastTopic) || row.count < 1 || row.count >= social.nextConversationSequence ||
                        row.lastStartTick < 0 || row.lastStartTick > social.clockTick) ||
                        social.pairMemory.GroupBy(row => new { row.fromId, row.toId }).Any(group => group.Count() != 1))
                        return Fail(out error, "Invalid directed NPC conversation memory.");
                    foreach (var row in social.pairMemory)
                    {
                        var reverse = social.pairMemory.SingleOrDefault(other => other.fromId == row.toId && other.toId == row.fromId);
                        if (reverse == null || reverse.count != row.count || reverse.lastTopic != row.lastTopic || reverse.lastStartTick != row.lastStartTick)
                            return Fail(out error, "NPC conversation memory must preserve both recorded directions.");
                    }
                    if (social.pending.Count > 0 && !NpcSocialRules.IsEligible(state))
                        return Fail(out error, "Pending NPC conversations require eligible free time and an active player.");
                    long previousSequence = 0;
                    foreach (var row in social.pending)
                    {
                        if (row.sequence <= previousSequence || row.sequence >= social.nextConversationSequence ||
                            !ActiveNpc(row.firstId) || !ActiveNpc(row.secondId) || row.firstId == row.secondId ||
                            !NpcSocialRules.IsKnownTopic(row.topic) || !NpcSocialRules.IsKnownRendezvous(row.rendezvousId) ||
                            row.week != state.week || row.phase != state.phase || row.startedTick < 0 || row.startedTick > social.clockTick ||
                            !Finite(row.durationMs) || row.durationMs < 8000 || row.durationMs > 40000 ||
                            (social.clockTick - row.startedTick) * 1000L >= row.durationMs)
                            return Fail(out error, "Invalid, stale, or expired pending NPC conversation.");
                        if (((row.topic == "alliance_talk" || row.topic == "rivalry") && row.durationMs < 20000) ||
                            (row.topic == "nominations" && (row.durationMs < 15000 || row.durationMs > 30000)))
                            return Fail(out error, "NPC conversation duration does not match its source topic.");
                        var memory = social.pairMemory.SingleOrDefault(item => item.fromId == row.firstId && item.toId == row.secondId);
                        if (memory == null || memory.lastStartTick != row.startedTick || memory.lastTopic != row.topic ||
                            social.cooldowns.Any(item => (item.npcId == row.firstId || item.npcId == row.secondId) && item.untilTick > row.startedTick))
                            return Fail(out error, "Pending NPC conversation lacks its accepted start memory or violates cooldown.");
                        previousSequence = row.sequence;
                    }
                    var participants = social.pending.SelectMany(row => new[] { row.firstId, row.secondId }).ToArray();
                    if (participants.Distinct(StringComparer.Ordinal).Count() != participants.Length)
                        return Fail(out error, "An NPC cannot participate in two pending conversations.");
                    if (social.pending.Select(row => row.rendezvousId).Distinct(StringComparer.Ordinal).Count() != social.pending.Count)
                        return Fail(out error, "A two-seat rendezvous cannot host two pending conversations.");
                    if (state.week < social.rulesStartWeek && (social.clockTick != 0 || social.nextScanTick != 3 ||
                        social.nextConversationSequence != 1 || social.randomState != NpcSocialRules.InitialRandomState(state.seed) ||
                        social.pending.Count != 0 || social.cooldowns.Count != 0 || social.pairMemory.Count != 0))
                        return Fail(out error, "Deferred NPC social rules cannot contain retroactive activity.");
                    // Every uint state, including zero after generator wraparound, is legal once activated.
                    return TryValidateNpcBeats(state, out error);
                }

        // SOURCE Gamesim.Simulation.EpisodeValidation.TryValidateProspectiveUnifiedVote 3723:4346
        internal static bool TryValidateProspectiveUnifiedVote(EpisodeState s, out string error)
                {
                    if (s == null || s.unifiedCommitmentRulesVersion != UnifiedVoteFamilyValidation.Version
                        || !YourWord.On(s))
                        return Fail(out error, "The unified vote rules require their explicit version and active commitment/story rules.");
                    if (s.dealRulesStartWeek != 1)
                        return Fail(out error, "The unified vote rules require the house's deal pass from the first week.");
                    return TryValidateCore(s, UnifiedVoteFamilyValidation.Version, out error);
                }

        // SOURCE Gamesim.Simulation.EpisodeValidation.TryValidateStory 634:12837
        private static bool TryValidateStory(EpisodeState s, out string error)
                {
                    error = null;
                    var w = s.story;
                    if (w == null) return Fail(out error, "Missing story state.");
                    bool Id(string id) => id != null && s.contestants.Any(c => c.id == id);
                    bool OptionalId(string id) => string.IsNullOrEmpty(id) || Id(id);
                    int cast = s.contestants.Count;
        
                    if (w.rulesStartWeek != 0 && (w.rulesStartWeek < 1 || w.rulesStartWeek > Math.Min(101, s.week + 1)))
                        return Fail(out error, "A story rules boundary cannot be further off than next week.");
                    // DELTA b208d46d (role moments R0): the defined milestones, 0 to Threads and Roles (11), not a bound.
                    if (!StoryRules.IsDefined(w.rulesVersion) || (w.rulesStartWeek != 0 && w.rulesVersion < StoryRules.Spine))
                        return Fail(out error, "Unsupported story rules version.");
                    if (w.productionStrictness != StoryRules.Lenient && w.productionStrictness != StoryRules.Standard)
                        return Fail(out error, "Unsupported production strictness.");
        
                    if (w.grudges == null || w.grudges.Count > 256 || w.grudges.Any(g => g == null || !Id(g.holderId) || !Id(g.targetId)
                            || g.holderId == g.targetId || !GrudgeCauses.IsKnown(g.cause) || !Finite(g.severity) || g.severity < 0 || g.severity > 100
                            || g.originWeek < 1 || g.originWeek > s.week || g.count < 1 || g.count > 100)
                        || w.grudges.GroupBy(g => g.holderId + "|" + g.targetId).Any(x => x.Count() > 1))
                        return Fail(out error, "Invalid grudge data.");
        
                    if (w.facts == null || w.facts.Count > Knowledge.Ceiling || w.facts.Any(f => f == null || !Text(f.id, 160) || !FactKinds.IsKnown(f.kind)
                            || !OptionalId(f.actorId) || !OptionalId(f.subjectId) || !ShortOrAbsent(f.refId, 160) || !FactVisibility.IsKnown(f.visibility)
                            || f.week < 1 || f.week > s.week || f.knowers == null || f.knowers.Count > cast || f.knowers.Any(id => !Id(id))
                            || f.knowers.Distinct(StringComparer.Ordinal).Count() != f.knowers.Count)
                        || w.facts.GroupBy(f => f.id).Any(x => x.Count() > 1))
                        return Fail(out error, "Invalid house fact data.");
        
                    if (w.bonds == null || w.bonds.Count > 64 || w.bonds.Any(b => b == null || !Text(b.id, 160) || !BondKinds.IsKnown(b.kind)
                            || !Id(b.aId) || !Id(b.bId) || b.aId == b.bId || !BondStatus.IsKnown(b.status) || !ShortOrAbsent(b.cycleId, 160)
                            || b.sinceWeek < 1 || b.sinceWeek > s.week || (b.endedWeek != 0 && (b.endedWeek < b.sinceWeek || b.endedWeek > s.week)))
                        || w.bonds.GroupBy(b => b.id).Any(x => x.Count() > 1))
                        return Fail(out error, "Invalid bond data.");
        
                    if (w.hooks == null || w.hooks.Count > 32 || w.hooks.Any(h => h == null || !Text(h.id, 160) || !Id(h.holderId) || !Id(h.overId)
                            || h.holderId == h.overId || !ShortOrAbsent(h.factId, 160) || h.week < 1 || h.week > s.week)
                        || w.hooks.GroupBy(h => h.id).Any(x => x.Count() > 1))
                        return Fail(out error, "Invalid hook data.");
        
                    if (w.contacts == null || w.contacts.Count > cast || w.contacts.Any(c => c == null || !Id(c.npcId) || c.rapport < 0 || c.rapport > 100
                            || c.weekCount < 0 || c.weekCount > 100 || c.lastWeek < 0 || c.lastWeek > s.week)
                        || w.contacts.GroupBy(c => c.npcId).Any(x => x.Count() > 1))
                        return Fail(out error, "Invalid contact data.");
        
                    if (w.lore == null || w.lore.Count > cast || w.lore.Any(l => l == null || !Id(l.contestantId) || !Text(l.sheetKey, 100)
                            || !LoreSources.IsKnown(l.source) || l.factIds == null || l.factIds.Count > 16 || l.factIds.Any(f => !Text(f, 100))
                            || l.factIds.Distinct(StringComparer.Ordinal).Count() != l.factIds.Count)
                        || w.lore.GroupBy(l => l.contestantId).Any(x => x.Count() > 1))
                        return Fail(out error, "Invalid lore data.");
        
                    if (w.knownFacts == null || w.knownFacts.Count > 200 || w.knownFacts.Any(f => !Text(f, 100))
                        || w.knownFacts.Distinct(StringComparer.Ordinal).Count() != w.knownFacts.Count)
                        return Fail(out error, "Invalid learned lore.");
        
                    if (w.conduct == null || w.conduct.Count > cast || w.conduct.Any(c => c == null || !Id(c.contestantId) || c.strikes < 0 || c.strikes > 10
                            || c.lastStrikeWeek < 0 || c.lastStrikeWeek > s.week || c.cleanWeeks < 0 || c.cleanWeeks > 100
                            || c.sitsOutWeek < 0 || c.sitsOutWeek > s.week + 1 || c.pushedBack < 0 || c.pushedBack > 1
                            || c.reasons == null || c.reasons.Count > 10 || c.reasons.Any(r => !Text(r, 100)))
                        || w.conduct.GroupBy(c => c.contestantId).Any(x => x.Count() > 1))
                        return Fail(out error, "Invalid conduct data.");
        
                    if (w.removals == null || w.removals.Count > cast || w.removals.Any(r => r == null || !Id(r.contestantId) || !Text(r.reasonId, 100)
                            || r.week < 1 || r.week > s.week)
                        || w.removals.GroupBy(r => r.contestantId).Any(x => x.Count() > 1))
                        return Fail(out error, "Invalid removal data.");
                    // The status and the record are two accounts of one removal and must agree both ways.
                    if (s.contestants.Any(c => c.status == ContestantStatus.Expelled && !w.removals.Any(r => r.contestantId == c.id))
                        || w.removals.Any(r => s.Find(r.contestantId)?.status != ContestantStatus.Expelled))
                        return Fail(out error, "A removal and the houseguest's status disagree.");
        
                    if (!string.IsNullOrEmpty(w.pendingRemovalId) && s.Find(w.pendingRemovalId)?.status != ContestantStatus.Active)
                        return Fail(out error, "A pending removal must name somebody still in the house.");
        
                    if (w.cooldowns == null || w.cooldowns.Count > 256 || w.cooldowns.Any(c => c == null || !Text(c.key, 200)
                            || c.untilWeek < 1 || c.untilWeek > s.week + 100)
                        || w.cooldowns.GroupBy(c => c.key).Any(x => x.Count() > 1))
                        return Fail(out error, "Invalid story cooldowns.");
        
                    if (w.reckonings == null || w.reckonings.Count > 64 || w.reckonings.Any(r => r == null || !Id(r.npcId) || !Text(r.cause, 60)
                            || r.week < 1 || r.week > s.week))
                        return Fail(out error, "Invalid reckonings.");
        
                    foreach (var modifier in s.activeModifiers)
                        if (!string.IsNullOrEmpty(modifier.ownerId) && !Id(modifier.ownerId))
                            return Fail(out error, "A modifier names somebody who is not in the season.");
        
                    foreach (var story in s.storylines)
                    {
                        if (story.cast == null || story.path == null || story.vars == null)
                            return Fail(out error, "Invalid story cycle.");
                        if (story.beatId == null)
                        {
                            // A legacy one-chapter storyline: nothing of the cycle may be set.
                            if (story.cast.Count > 0 || story.path.Count > 0 || story.vars.Count > 0 || story.lane != null
                                || story.nextAnchor != null || story.endingId != null || story.nextWeek != 0 || story.variant != 0)
                                return Fail(out error, "A legacy storyline carries story-cycle state.");
                            continue;
                        }
                        if (!Text(story.beatId, 120) || !StoryLanes.IsKnown(story.lane) || story.variant < 0 || story.variant > 10
                            || story.cast.Count > 8 || story.cast.Any(r => r == null || !Text(r.role, 40) || !Id(r.contestantId))
                            || story.cast.GroupBy(r => r.role).Any(x => x.Count() > 1)
                            || story.path.Count > 24 || story.path.Any(p => p == null || !Text(p.beatId, 120) || !ShortOrAbsent(p.optionId, 60)
                                || !StoryResults.IsKnown(p.result) || p.week < 1 || p.week > s.week || p.logSequence < 0 || p.logSequence >= s.nextSequence)
                            || story.vars.Count > 16 || story.vars.Any(v => v == null || !Text(v.key, 40) || v.value < -1000 || v.value > 1000)
                            || story.vars.GroupBy(v => v.key).Any(x => x.Count() > 1)
                            || story.nextWeek < 0 || story.nextWeek > s.week + 20
                            || (story.nextAnchor != null && !StoryAnchors.IsKnown(story.nextAnchor))
                            || !ShortOrAbsent(story.endingId, 80))
                            return Fail(out error, "Invalid story cycle.");
                    }
        
                    foreach (var item in s.houseEvents)
                    {
                        if (item.cast == null) return Fail(out error, "Invalid story beat.");
                        foreach (var choice in item.choices)
                            if (choice.eligibleIds == null || choice.bonusTraits == null || choice.against == null
                                || choice.effects == null || choice.backfire == null)
                                return Fail(out error, "Invalid story option.");
                        if (!LiteralRows.IsStory(item))
                        {
                            // A legacy event carries none of a beat's state.
                            if (item.contentId != null || item.cycleId != null || item.closesAnchor != null || item.surface != null
                                || item.venue != null || item.lapseOptionId != null || item.cast.Count > 0
                                || item.choices.Any(c => c.optionId != null || c.effects.Count > 0 || c.backfire.Count > 0
                                                         || c.eligibleIds.Count > 0 || c.next != null || c.nextOnBackfire != null
                                                         || c.checkBase != -1 || c.lapse || c.conduct || c.pickPerson || c.locked
                                                         || c.costsAction || c.subjectId != null || c.glyph != null || c.lockReason != null
                                                         || c.approach != null
                                                         || c.bonusTraits.Count > 0 || c.against.Count > 0))
                                return Fail(out error, "A legacy house event carries story-beat state.");
                            continue;
                        }
                        if (!Text(item.contentId, 120) || !ShortOrAbsent(item.cycleId, 160)
                            || (item.closesAnchor != null && !StoryAnchors.IsKnown(item.closesAnchor))
                            || !StorySurfaces.IsKnown(item.surface) || !ShortOrAbsent(item.venue, 60) || !ShortOrAbsent(item.lapseOptionId, 60)
                            || item.cast.Count > 8 || item.cast.Any(r => r == null || !Text(r.role, 40) || !Id(r.contestantId)))
                            return Fail(out error, "Invalid story beat.");
                        if (item.choices.Any(c => c.optionId == null) || item.choices.GroupBy(c => c.optionId).Any(x => x.Count() > 1))
                            return Fail(out error, "Every story option needs its own id.");
                        foreach (var choice in item.choices)
                        {
                            if (!Text(choice.optionId, 60) || !ShortOrAbsent(choice.glyph, 40) || !ShortOrAbsent(choice.lockReason, 120)
                                || (choice.approach != null && !Personality.Approach.IsKnown(choice.approach))
                                || !(choice.checkBase == -1 || (Finite(choice.checkBase) && choice.checkBase >= 0 && choice.checkBase <= 100))
                                || !OptionalId(choice.subjectId)
                                || choice.eligibleIds.Count > 32 || choice.eligibleIds.Any(id => !Id(id))
                                || choice.bonusTraits.Count > 17 || choice.bonusTraits.Any(t => !Text(t, 40))
                                || choice.against.Count > 17 || choice.against.Any(t => !Text(t, 40))
                                || choice.effects.Count > 24 || choice.backfire.Count > 24
                                || choice.effects.Concat(choice.backfire).Any(e => BadEffect(e, OptionalId))
                                || !ShortOrAbsent(choice.next, 120) || !ShortOrAbsent(choice.nextOnBackfire, 120))
                                return Fail(out error, "Invalid story option.");
                        }
                    }
                    return true;
                }

        // SOURCE Gamesim.Simulation.EpisodeValidation.TryValidateUnifiedSafetyReferences 531:691
        private static bool TryValidateUnifiedSafetyReferences(EpisodeState s, out string error) =>
                    TryValidateUnifiedSafetyReferences(s, false, out error);

        // SOURCE Gamesim.Simulation.EpisodeValidation.TryValidateUnifiedSafetyReferences 701:9569
        private static bool TryValidateUnifiedSafetyReferences(EpisodeState s, bool prospectiveVote, out string error)
                {
                    error = null;
                    // Vote truth cannot become a Safety opportunity, BrokenWord source or hearing.
                    // Existing mode1 has already proved every row Safety, so its map is unchanged.
                    var canonical = s.unifiedCommitments.Where(row => row.kind == UnifiedCommitments.Safety)
                        .ToDictionary(row => row.id, StringComparer.Ordinal);
                    var deals = CommitmentReferences.Deals(s).ToDictionary(row => row.id, StringComparer.Ordinal);
                    foreach (var opportunity in s.ledger.opportunities)
                    {
                        bool own = canonical.TryGetValue(opportunity.id, out var record);
                        if (own && opportunity.kind != OpportunityKinds.Deal)
                            return Fail(out error, "A Safety opportunity must retain its actual Deal family.");
                        if (opportunity.kind != OpportunityKinds.Deal) continue;
                        if (!deals.TryGetValue(opportunity.id, out var deal)
                            || (deal.proposerId != s.playerId && deal.recipientId != s.playerId)
                            || opportunity.week != deal.week)
                            return Fail(out error, "A Deal opportunity must resolve its own player-party source and creation week.");
                        if (own && !ValidSavedSafetyOpportunity(s, record, deal, opportunity))
                            return Fail(out error, "The Safety opportunity is not an actual immediate or historical source projection.");
                    }
        
                    var incidents = UnifiedCommitmentHistory.Breaches(s);
                    foreach (var fact in s.story.facts.Where(f => f.kind == FactKinds.BrokenWord && f.refId != null))
                        // Generic story facts name their cycle (which may later be pruned), or nothing.
                        // Actual commitment-shaped BrokenWord provenance names a permanent Deal owner;
                        // erasing that owner must not turn it into an unrelated generic story fact.
                        if (!deals.ContainsKey(fact.refId) && (fact.refId.StartsWith("deal-", StringComparison.Ordinal)
                            || fact.refId.StartsWith("promise-", StringComparison.Ordinal)
                            || s.promises.Any(p => p.id == fact.refId)))
                            return Fail(out error, "A commitment-shaped BrokenWord fact lost its true Deal-family owner.");
                    var canonicalFacts = s.story.facts.Where(fact => canonical.ContainsKey(fact.refId ?? "")).ToArray();
                    foreach (var fact in canonicalFacts)
                        if (!ValidSavedSafetyFact(s, canonical[fact.refId], fact, incidents))
                            return Fail(out error, "A canonical fact must be its actual audible Deal-policy breach leaf.");
                    if (canonicalFacts.GroupBy(fact => fact.refId, StringComparer.Ordinal).Any(group => group.Count() > 1))
                        return Fail(out error, "A Safety source cannot have two live BrokenWord identities.");
                    if (!(prospectiveVote
                            ? UnifiedCommitmentHearings.TryValidateProspectiveVoteStorage(s, out error)
                            : UnifiedCommitmentHearings.ValidateStorage(s, out error))) return false;
                    // A lawful live-fact pruning keeps the same permanent source leaf. Archived-only
                    // facts need settlement-time knowledge eligibility too, not merely today's rules.
                    foreach (var evidence in s.unifiedHearingEvidence)
                        if (!canonical.TryGetValue(evidence.fact.refId, out var archivedOwner)
                            || !ValidSavedSafetyFact(s, archivedOwner, evidence.fact, incidents))
                            return Fail(out error, "An archived Safety fact must retain its actual audible settlement provenance.");
                    if (prospectiveVote ? s.unifiedHearingRulesVersion == UnifiedCommitmentHearings.ProspectiveVersion
                        : UnifiedCommitmentHearings.RulesOn(s))
                    {
                        // Under the fresh hearing-1 writer contract, every selected player Deal breach
                        // audible at its recorded settlement installed permanent archive/Initial lineage.
                        // Live facts can roll off; these bounded archives and receipts cannot. Iterating
                        // incidents also catches deletion of the fact, archive and Initial together.
                        // Promise owners, weaker aliases, NPC breakers and pre-knowledge settlements
                        // acquire no inferred emission or witness from this completed-state invariant.
                        foreach (var incident in incidents)
                        {
                            var owner = canonical[incident.EffectOwnerId];
                            if (incident.ActorId != s.playerId || owner.sourcePolicy != UnifiedCommitments.DealPolicy
                                || owner.status != DealStatus.Broken || !SavedSafetyWordWasOn(s, owner.settledWeek)) continue;
                            var evidence = s.unifiedHearingEvidence.FirstOrDefault(e => e.incidentKey == incident.EffectKey
                                && e.fact.refId == owner.id);
                            if (evidence == null || evidence.fact.actorId != incident.ActorId
                                || evidence.fact.subjectId != incident.WrongedId || evidence.fact.week != owner.settledWeek
                                || !s.unifiedHearingReceipts.Any(r => r.kind == UnifiedCommitmentHearings.Initial
                                    && r.incidentKey == incident.EffectKey && r.factId == evidence.fact.id
                                    && r.listenerId == incident.WrongedId && r.heardWeek == owner.settledWeek))
                                return Fail(out error, "The selected audible Deal incident lost its permanent emission lineage.");
                        }
                        // Knowledge.BrokenWord installs its selected Deal emission, archive and Initial
                        // receipt atomically. Check that completed writer invariant here, never inside
                        // ValidateStorage, which also validates the writer's legitimate intermediate state.
                        // A weaker audible alias proves no Initial and a Public fact proves no hearings.
                        foreach (var fact in canonicalFacts.Concat(s.unifiedHearingEvidence.Select(e => e.fact)))
                        {
                            var incident = incidents.FirstOrDefault(i => i.EvidenceIds.Contains(fact.refId));
                            if (incident == null || incident.EffectOwnerId != fact.refId
                                || canonical[fact.refId].sourcePolicy != UnifiedCommitments.DealPolicy) continue;
                            var evidence = s.unifiedHearingEvidence.FirstOrDefault(e => e.fact.id == fact.id);
                            if (evidence == null || evidence.incidentKey != incident.EffectKey
                                || !s.unifiedHearingReceipts.Any(r => r.kind == UnifiedCommitmentHearings.Initial
                                    && r.incidentKey == incident.EffectKey && r.factId == fact.id
                                    && r.listenerId == incident.WrongedId
                                    && r.heardWeek == canonical[fact.refId].settledWeek))
                                return Fail(out error, "The selected audible Deal emission lost its atomic archive or Initial receipt.");
                        }
                    }
        
                    var brokenOwners = new HashSet<string>(incidents.Select(i => i.EffectOwnerId), StringComparer.Ordinal);
                    var keptOwners = new HashSet<string>(UnifiedCommitmentHistory.Fulfillments(s)
                        .Select(receipt => receipt.EffectOwnerId), StringComparer.Ordinal);
                    foreach (var question in s.juryExchanges.Where(q => q.receiptKind != null))
                    {
                        if (!ValidSavedSafetyJuryReceipt(s, question, canonical, brokenOwners, keptOwners))
                            return Fail(out error, "The saved jury receipt does not resolve its actual category, parties and source evidence.");
                        // Source-qualified wording is narrower than the category's catalog. Resolve the
                        // actual source first; never use ReceiptLine's privacy-safe rendering as validation
                        // or regenerate today's preferred/latest receipt to replace a saved question.
                        var receipt = new FinaleQuestions.Receipt { category = question.category, kind = question.receiptKind,
                            id = question.receiptId, week = FinaleQuestions.ReceiptWeek(s, question) ?? 0 };
                        if (!FinaleQuestions.QuestionsFor(s, question.category, receipt, question.questionerId).Contains(question.question))
                            return Fail(out error, "The saved jury question claims more than its actual receipt proves.");
                    }
                    // TryValidateV2 already uses FinalArgument.Resolves: locks must still name their own
                    // family/party source, not still qualify as a selectable or strongest scoring moment.
                    return true;
                }

        // SOURCE Gamesim.Simulation.EpisodeValidation.TryValidateUnifiedVoteReferences 450:2359
        private static bool TryValidateUnifiedVoteReferences(EpisodeState s, out string error)
                {
                    error = null;
                    var canonical = s.unifiedCommitments.Where(row => row.kind == UnifiedVoteTogether.Vote)
                        .ToDictionary(row => row.id, StringComparer.Ordinal);
                    foreach (var opportunity in s.ledger.opportunities)
                    {
                        if (!canonical.TryGetValue(opportunity.id, out var row)) continue;
                        if (opportunity.kind != OpportunityKinds.Deal || row.sourcePolicy != UnifiedCommitments.DealPolicy
                            || (row.makerId != s.playerId && row.beneficiaryId != s.playerId)
                            || opportunity.week != row.createdWeek || !ValidSavedVoteOpportunity(s, row, opportunity))
                            return Fail(out error, "A Vote opportunity must retain its actual player-party Deal source and projection.");
                    }
                    // A private ballot truth is not a Safety nomination fact or hearing lineage.
                    // Other Story fact families keep the original complete Story validator's rules.
                    if (s.story.facts.Any(fact => fact.kind == FactKinds.BrokenWord
                            && fact.refId != null && canonical.ContainsKey(fact.refId))
                        || s.unifiedHearingEvidence.Any(evidence => canonical.ContainsKey(evidence.fact.refId ?? "")))
                        return Fail(out error, "Vote truth cannot be installed as a Safety BrokenWord fact or hearing.");
        
                    foreach (var question in s.juryExchanges)
                    {
                        if (question.receiptId == null || !canonical.TryGetValue(question.receiptId, out var row)) continue;
                        if (!ValidSavedVoteJuryReceipt(s, row, question))
                            return Fail(out error, "A Vote jury receipt must resolve its real source, category, parties and known decision.");
                    }
                    return true;
                }

        // SOURCE Gamesim.Simulation.EpisodeValidation.TryValidateV2 129:10495
        private static bool TryValidateV2(EpisodeState s, out string error)
                {
                    error = null;
                    bool Id(string id) => s.contestants.Any(c => c.id == id);
                    bool Finalist(string id) => s.contestants.Any(c => c.id == id && (c.status == ContestantStatus.Active || c.status == ContestantStatus.Winner || c.status == ContestantStatus.RunnerUp));
                    bool Juror(string id) => s.contestants.Any(c => c.id == id && (c.status == ContestantStatus.Jury || c.status == ContestantStatus.Evicted));
                    foreach (var actor in s.contestants)
                        if (!new[] { "Angry", "Upset", "Neutral", "Content", "Happy" }.Contains(actor.mood) ||
                            !new[] { "Relaxed", "Normal", "Tense", "Stressed", "Overwhelmed" }.Contains(actor.stressLevel))
                            return Fail(out error, "Unsupported mood or stress level.");
                    foreach (var edge in s.relationships)
                    {
                        if (edge.lastInteractionWeek < 0 || edge.lastInteractionWeek > s.week || edge.notes == null || edge.notes.Count > 256 || edge.notes.Any(n => !Text(n, 4000)) ||
                            edge.events == null || edge.events.Count > 512 || edge.events.Any(e => e == null || e.sequence < 1 || e.sequence >= s.nextSequence || e.week < 1 || e.week > s.week ||
                                !Text(e.type, 100) || !Text(e.description, 4000) || !Finite(e.impactScore) || Math.Abs(e.impactScore) > 200))
                            return Fail(out error, "Invalid relationship history.");
                    }
                    // One arc per houseguest the player has a history with, so the ceiling follows the
                    // cast. It read "> 5" while a house held six, which is the same number by coincidence
                    // and refuses a legal seven-person season the moment the player has met everyone.
                    int arcCeiling = Math.Max(0, s.contestants.Count - 1);
                    if (s.relationshipArcs == null || s.relationshipArcs.Count > arcCeiling || s.relationshipArcs.Any(a => a == null || !Id(a.npcId) || a.npcId == s.playerId || !Text(a.npcName, 100) ||
                        !new[] { "neutral", "friendship", "rivalry" }.Contains(a.arcType) || !Finite(a.intensity) || a.intensity < 0 || a.intensity > 100 ||
                        a.escalationLevel != WebRelationshipArcs.GetEscalationLevel(a.intensity) || a.weeklyHistory == null || a.weeklyHistory.Count > 2000 ||
                        a.weeklyHistory.Any(h => h == null || h.week < 1 || h.week > s.week || !Finite(h.delta) || Math.Abs(h.delta) > 200 || !Text(h.reason, 4000))) ||
                        s.relationshipArcs.GroupBy(a => a.npcId).Any(g => g.Count() > 1)) return Fail(out error, "Invalid relationship arcs.");
                    foreach (var arc in s.relationshipArcs)
                    {
                        double sentiment = arc.weeklyHistory.Sum(h => h.delta);
                        string type = sentiment <= -15 ? "rivalry" : sentiment >= 15 ? "friendship" : "neutral";
                        double intensity = Math.Min(100, arc.weeklyHistory.Sum(h => Math.Min(Math.Abs(h.delta) * 1.5, 15)));
                        if (type != arc.arcType || Math.Abs(intensity - arc.intensity) > 0.0000001) return Fail(out error, "Arc summary does not match its history.");
                    }
                    // Everyone who is out of the house questions the finalists, so this is the cast minus
                    // the two in the final two — "> 4" was that number for a house of six. The floor of two
                    // is the spectator branch, which asks two questions whatever the season's size.
                    int exchangeCeiling = Math.Max(2, s.contestants.Count - 2);
                    if (s.juryExchanges == null || s.juryExchanges.Count > exchangeCeiling || s.juryQuestionIndex < 0 || s.juryQuestionIndex > s.juryExchanges.Count ||
                        s.finalSpeeches == null || s.finalSpeeches.Count > 2 || s.finalSpeeches.Any(x => x == null || !Finalist(x.speakerId) || x.text == null || x.text.Length > 4000 ||
                            x.isPlayerAuthored != (x.speakerId == s.playerId)) || s.finalSpeeches.GroupBy(x => x.speakerId).Any(g => g.Count() > 1))
                        return Fail(out error, "Invalid finale records.");
                    bool finale = s.phase == EpisodePhase.JuryQuestioning || s.phase == EpisodePhase.FinalSpeeches || s.phase == EpisodePhase.Jury || s.phase == EpisodePhase.Finished;
                    if (!finale && (s.juryExchanges.Count != 0 || s.juryQuestionIndex != 0 || s.finalSpeeches.Count != 0 || s.finalArgument != null))
                        return Fail(out error, "Finale records cannot precede the final eviction.");
                    // Schema 21: the player finalist's argument, under the finale rules. Each reference must
                    // still name a row of theirs; whether it would still qualify is not asked, so nothing
                    // later can orphan it.
                    var argument = s.finalArgument;
                    if (argument != null && (!EpisodeEngine.FinaleOn(s) || !Finalist(s.playerId) || !FinalArgument.Themes.Contains(argument.theme)
                        || argument.momentRefs == null || argument.momentRefs.Count > FinalArgument.MomentCount
                        || argument.momentRefs.Distinct().Count() != argument.momentRefs.Count || argument.momentRefs.Any(r => !FinalArgument.Resolves(s, r))))
                        return Fail(out error, "Invalid final argument.");
                    bool rules = EpisodeEngine.FinaleOn(s);
                    bool playerFinalist = Finalist(s.playerId);
                    var questioners = s.contestants.Where(c => Juror(c.id)).Select(c => c.id).ToArray();
                    var finalists = s.contestants.Where(c => Finalist(c.id)).Select(c => c.id).ToArray();
                    int total = playerFinalist ? questioners.Length : 2;
                    if (s.juryExchanges.Count > total) return Fail(out error, "Too many jury exchanges.");
                    for (int i = 0; i < s.juryExchanges.Count; i++)
                    {
                        var q = s.juryExchanges[i];
                        if (q == null || !Juror(q.questionerId) || !Finalist(q.finalistId) ||
                            q.questionerId != (playerFinalist ? questioners[i] : s.playerId) ||
                            q.finalistId != (playerFinalist ? s.playerId : finalists[i]) || (!q.completed && i != s.juryQuestionIndex))
                            return Fail(out error, "Invalid jury exchange order or identities.");
                        if (playerFinalist && rules)
                        {
                            // A history question: its words are the category's, its receipt of a kind the
                            // category takes, and the answer one of the responses offered, in its own words.
                            if (!FinaleQuestions.Categories.Contains(q.category) || q.tone != FinaleQuestions.Tone(q.category)
                                || !FinaleQuestions.Questions(q.category).Contains(q.question)
                                || (q.receiptKind == null) != (q.category == FinaleQuestions.Comparison)
                                || (q.receiptKind == null ? q.receiptId != null
                                    : !FinaleQuestions.ReceiptKinds(q.category).Contains(q.receiptKind) || string.IsNullOrEmpty(q.receiptId) || !Text(q.receiptId, 200))
                                || q.optionA != null || q.optionB != null || q.correctChoice != null
                                || q.opponentAnswer != WebJuryQuestioning.GetOpponentAnswer(i)
                                || (q.completed && (!FinaleQuestions.Offered(q.category, q.receiptKind).Contains(q.answerChoice)
                                    || q.answer != FinaleQuestions.Line(q.category, q.answerChoice))))
                                return Fail(out error, "The saved finalist question does not match the finale rules.");
                        }
                        else if (q.category != null || q.receiptKind != null || q.receiptId != null)
                            return Fail(out error, "Only a history question saves a category or a receipt.");
                        else if (playerFinalist)
                        {
                            var saved = new WebJuryQuestion { tone = q.tone, question = q.question, optionA = q.optionA, optionB = q.optionB,
                                correctIs = q.correctChoice, opponentAnswer = q.opponentAnswer, trait = WebJuryQuestioning.GetPrimaryTrait(s.Find(q.questionerId).traits) };
                            if (!WebJuryQuestioning.MatchesSource(saved, s.Find(q.questionerId).traits, i) || (q.completed &&
                                ((q.answerChoice != "A" && q.answerChoice != "B") || q.answer != (q.answerChoice == "A" ? q.optionA : q.optionB))))
                                return Fail(out error, "The saved finalist question does not match its source catalogue.");
                        }
                        else
                        {
                            if (q.optionA != null || q.optionB != null || q.correctChoice != null || q.opponentAnswer != null)
                                return Fail(out error, "Player jurors cannot store finalist answer keys.");
                            if (q.completed && (!WebJuryQuestioning.GetJurorQuestionOptions(i).Any(o => o.tone == q.tone && o.text == q.question && q.answerChoice == o.tone) || !Text(q.answer, 4000)))
                                return Fail(out error, "Invalid saved juror question.");
                            if (!q.completed && (q.tone != null || q.question != null)) return Fail(out error, "Unasked juror questions must not have a selected tone.");
                        }
                        if (!q.completed && (q.answer != null || q.answerChoice != null)) return Fail(out error, "Pending jury answer is partially committed.");
                    }
                    if (s.phase == EpisodePhase.JuryQuestioning)
                    {
                        if (s.juryExchanges.Count != s.juryQuestionIndex + 1 || s.juryQuestionIndex >= total || s.finalSpeeches.Count != 0 || s.votes.Count != 0)
                            return Fail(out error, "Questioning requires exactly one current exchange and no early speeches or ballots.");
                    }
                    else if (s.juryQuestionIndex != s.juryExchanges.Count || s.juryExchanges.Any(q => !q.completed))
                        return Fail(out error, "Only questioning may retain an unanswered exchange.");
                    if (s.phase == EpisodePhase.FinalSpeeches && (s.votes.Count != 0 || LiteralRows.Active(s).Any(c => !c.isPlayer && !s.finalSpeeches.Any(x => x.speakerId == c.id))))
                        return Fail(out error, "Final speeches require the saved NPC speeches before voting.");
                    // Legacy v1 Jury/Finished saves legitimately have no questioning/speech records.
                    return true;
                }

        // SOURCE Gamesim.Simulation.EpisodeValidation.TryValidateV3 129:6421
        internal static bool TryValidateV3(EpisodeState s, out string error) => Gamesim.Persistence.Frozen29Components.FrozenV29SocialHistory.TryValidateV3(s, out error);

        // SOURCE Gamesim.Simulation.EpisodeValidation.TryValidateWaveD 582:2588
        private static bool TryValidateWaveD(EpisodeState s, out string error)
                {
                    error = null;
                    if (!WaveDStartWeek(s, s.allianceLeakRulesStartWeek) || !WaveDStartWeek(s, s.pactPlanRulesStartWeek)
                        || !WaveDStartWeek(s, s.allWeekRulesStartWeek))
                        return Fail(out error, "Wave D activation weeks must be within the saved season boundary.");
                    if (!WaveDLinesKept(s, s.allWeekRulesStartWeek, WaveDEventKinds.Sighting, WaveDEventKinds.Overheard))
                        return Fail(out error, "A season without the all-week rules has none of their lines.");
                    if (!WaveDLinesKept(s, s.pactPlanRulesStartWeek, WaveDEventKinds.PactPlan))
                        return Fail(out error, "A season without the war rooms has none of their lines.");
                    if (!WaveDLinesKept(s, s.allianceLeakRulesStartWeek, WaveDEventKinds.DoubleDealing))
                        return Fail(out error, "A season without the leak rules has none of their lines.");
                    // D4's receipt, held either way between any two houseguests.
                    if (s.relationships.Any(r => r.events.Any(e => e.type == StoryReceipts.DoubleDealt
                            && (s.allianceLeakRulesStartWeek == 0 || e.week < s.allianceLeakRulesStartWeek))))
                        return Fail(out error, "A season without the leak rules has none of their receipts.");
                    // D2's records of the house's talk (EpisodeEngine.CompletionChange) and of a veto's moves between two houseguests
                    // (EpisodeEngine.ChangeBetween), which feed trust between houseguests. A lie's and a scheme's records keep the types
                    // the engine's own path always wrote.
                    if (s.relationships.Any(r => r.events.Any(e => EpisodeEngine.AllWeekRecord(e.type)
                            && (s.allWeekRulesStartWeek == 0 || e.week < s.allWeekRulesStartWeek))))
                        return Fail(out error, "A season without the all-week rules has none of their records.");
                    // Both write them only between two houseguests: a change with the player keeps the engine's own path, which records
                    // none of them, as it has since each was introduced (the consolidation check's ARCS2-4), so no season the engine
                    // played holds one on an edge of the player's.
                    if (s.relationships.Any(r => (r.fromId == s.playerId || r.toId == s.playerId) && r.events.Any(e => EpisodeEngine.AllWeekRecord(e.type))))
                        return Fail(out error, "The all-week rules' records are only ever between two houseguests.");
                    return true;
                }

        // SOURCE Gamesim.Simulation.EpisodeValidation.ValidSavedSafetyFact 12666:13576
        private static bool ValidSavedSafetyFact(EpisodeState s, UnifiedCommitmentState record, HouseFactState fact,
                    IReadOnlyList<UnifiedCommitmentIncident> incidents)
                {
                    if (record.kind != UnifiedCommitments.Safety || record.sourcePolicy != UnifiedCommitments.DealPolicy || record.status != DealStatus.Broken
                        || !SavedSafetyWordWasOn(s, record.settledWeek) || fact.kind != FactKinds.BrokenWord || fact.actorId != s.playerId
                        || fact.week != record.settledWeek || fact.visibility == FactVisibility.Private
                        || !SavedSafetySequence(fact.id, "fact-", s.nextSequence)
                        || !fact.knowers.Contains(fact.actorId) || !fact.knowers.Contains(fact.subjectId)) return false;
                    return incidents.Any(i => i.ActorId == fact.actorId && i.WrongedId == fact.subjectId
                        && i.EvidenceIds.Contains(fact.refId));
                }

        // SOURCE Gamesim.Simulation.EpisodeValidation.ValidSavedSafetyJuryReceipt 14074:18943
        private static bool ValidSavedSafetyJuryReceipt(EpisodeState s, JuryExchangeState q,
                    IReadOnlyDictionary<string, UnifiedCommitmentState> canonical, ISet<string> brokenOwners, ISet<string> keptOwners)
                {
                    string player = s.playerId, juror = q.questionerId;
                    switch (q.receiptKind)
                    {
                        case FinaleQuestions.PromiseReceipt:
                            var promise = CommitmentReferences.FindPromise(s, q.receiptId);
                            if (promise == null || promise.fromId != player || promise.toId != juror) return false;
                            if (q.category == FinaleQuestions.Accountability)
                                return promise.status == PromiseStatus.Broken
                                    && (!canonical.ContainsKey(promise.id) || brokenOwners.Contains(promise.id));
                            return q.category == FinaleQuestions.Personal && promise.status == PromiseStatus.Fulfilled;
                        case FinaleQuestions.DealReceipt:
                            var deal = CommitmentReferences.FindDeal(s, q.receiptId);
                            if (deal == null || !((deal.proposerId == player && deal.recipientId == juror)
                                || (deal.proposerId == juror && deal.recipientId == player))) return false;
                            if (q.category == FinaleQuestions.Accountability)
                                return deal.status == DealStatus.Broken && FinalistRead.DealBreaker(s, deal) == player
                                    && (!canonical.ContainsKey(deal.id) || brokenOwners.Contains(deal.id));
                            return q.category == FinaleQuestions.Personal && deal.status == DealStatus.Fulfilled
                                && (!EpisodeEngine.CommitmentRulesOn(s) || KnownBallots.DealOutcomeKnown(s, deal))
                                && (!canonical.ContainsKey(deal.id) || keptOwners.Contains(deal.id));
                        case FinaleQuestions.PowerReceipt:
                            if (!SavedSafetyWeek(q.receiptId, s.week, out int powerWeek)) return false;
                            var power = s.ledger.power.FirstOrDefault(p => p.week == powerWeek);
                            if (power == null) return false;
                            bool final = power.tally.Count == 0 && power.evicteeId != null && power.vetoHolderId == null;
                            if (q.category == FinaleQuestions.Ownership)
                                return (power.hohId == player && (FinalistRead.PutUp(power, juror) || (final && power.evicteeId == juror)))
                                    || (power.vetoHolderId == player && power.vetoUsed && power.replacementId == juror);
                            if (q.category == FinaleQuestions.Strategy) return power.hohId == player;
                            return q.category == FinaleQuestions.Social && !final
                                && (power.nominees.Contains(player) || power.savedId == player)
                                && power.evicteeId != null && power.evicteeId != player;
                        case FinaleQuestions.BallotReceipt:
                            if (!SavedSafetyWeek(q.receiptId, s.week, out int ballotWeek)) return false;
                            return s.ledger.ballots.Any(b => b.week == ballotWeek && b.voterId == player
                                && (q.category == FinaleQuestions.Ownership ? b.targetId == juror
                                    : q.category == FinaleQuestions.Mistake && ((b.readBefore != null && !b.correct)
                                        || s.ledger.power.Any(p => p.week == b.week && p.evicteeId != null && p.evicteeId != b.targetId))));
                        case FinaleQuestions.ReplyReceipt:
                            return q.category == FinaleQuestions.JuryManagement && s.ledger.replies.Any(r => r.cardId == q.receiptId
                                && r.fromId == juror && r.kind == ReplyCards.Plea && r.replyKey == "refuse");
                        case FinaleQuestions.CallReceipt:
                            if (q.category != FinaleQuestions.Strategy) return false;
                            return s.ledger.calls.Any(c => q.receiptId == c.allianceId + ":" + c.week.ToString(CultureInfo.InvariantCulture)
                                && c.callerId == player && c.callerId != juror && !c.followed.Contains(juror) && !c.defected.Contains(juror)
                                && !s.alliances.Any(a => a.id == c.allianceId && a.members.Contains(juror)));
                        case FinaleQuestions.AllianceReceipt:
                            if (q.category != FinaleQuestions.Personal) return false;
                            int? left = JuryHouseRead.LeftWeek(s, juror);
                            return s.alliances.Any(a => a.id == q.receiptId && a.members.Contains(player) && a.members.Contains(juror)
                                && (a.active || s.ledger.alliances.Any(r => r.id == a.id && r.why != null
                                    && r.why.EndsWith("/left-house", StringComparison.Ordinal) && left != null && r.endedWeek == left)));
                        default: return false;
                    }
                }

        // SOURCE Gamesim.Simulation.EpisodeValidation.ValidSavedSafetyOpportunity 9579:12656
        private static bool ValidSavedSafetyOpportunity(EpisodeState s, UnifiedCommitmentState record,
                    DealState deal, OpportunityRow opportunity)
                {
                    if (record.kind != UnifiedCommitments.Safety || record.sourcePolicy != UnifiedCommitments.DealPolicy || opportunity.anchor != null
                        || opportunity.currency != null || opportunity.steps.Count != 0 || opportunity.payoff != 0) return false;
                    bool taken = opportunity.response == OpportunityResponse.Taken;
                    bool neutral = opportunity.outcome == OpportunityOutcome.NotApplicable;
                    bool answered = record.status == DealStatus.Active || record.status == DealStatus.Broken
                        || record.status == DealStatus.Fulfilled || record.status == DealStatus.Expired;
                    if (opportunity.source == null && opportunity.note == null)
                        return taken && neutral && answered && (record.origin == UnifiedCommitments.PlayerDeal
                            || record.origin == UnifiedCommitments.NpcOffer || record.origin == UnifiedCommitments.CounterDeal
                            || record.origin == UnifiedCommitments.CounterPrice);
                    if (opportunity.source != DealKind.SafetyAgreement || opportunity.note == null) return false;
                    string prefix = (deal.proposerId == s.playerId ? "put to " + deal.recipientId : "offered by " + deal.proposerId) + ", ";
                    if (!opportunity.note.StartsWith(prefix, StringComparison.Ordinal)) return false;
                    string observed = opportunity.note.Substring(prefix.Length);
                    // Responses are not eager snapshots. Accepting an NPC offer updates Taken only; it
                    // leaves the preceding Proposed source/note until the next actual reconciliation.
                    if (observed == DealStatus.Proposed)
                        return record.origin == UnifiedCommitments.NpcOffer && neutral
                            && ((opportunity.response == OpportunityResponse.Ignored
                                    && (record.status == DealStatus.Proposed || record.status == DealStatus.Declined
                                        || record.status == DealStatus.Expired))
                                || (taken && answered));
                    if (observed == DealStatus.Active)
                        return answered && taken && neutral;
                    if (observed == DealStatus.Declined)
                        return record.status == DealStatus.Declined && opportunity.response == OpportunityResponse.Declined && neutral;
                    if (observed == DealStatus.Expired)
                        return record.status == DealStatus.Expired && neutral
                            && (opportunity.response == OpportunityResponse.Expired || (taken && EpisodeEngine.CommitmentRulesOn(s)));
                    if (observed == DealStatus.Fulfilled)
                        return record.status == DealStatus.Fulfilled && taken && opportunity.outcome == OpportunityOutcome.Won;
                    if (observed == DealStatus.Broken)
                        return record.status == DealStatus.Broken && taken && opportunity.outcome == OpportunityOutcome.Lost;
                    return false;
                }

        // SOURCE Gamesim.Simulation.EpisodeValidation.ValidSavedVoteJuryReceipt 5746:8622
        private static bool ValidSavedVoteJuryReceipt(EpisodeState s, UnifiedCommitmentState row, JuryExchangeState question)
                {
                    var decision = UnifiedVoteHistory.FindDecision(s, row.id);
                    if (decision == null || decision.SettledWeek != row.settledWeek || decision.Status != row.status) return false;
                    // The lead's decision D7 (vote family V5e): a receipt is its Rule2 group's owner, as the questions choose one
                    // (FinaleQuestions.Receipts) - never a row that owned no consequence of its reveal. And the owner of the receipt's
                    // own direction (the pre-V6 save-contract review): an Accountability receipt is the owner of the player's breach
                    // of the juror, never of the juror's of the player, which a deal both of them broke can own alone; a Personal
                    // one the owner of a kept group between the two.
                    string player = s.playerId, juror = question.questionerId;
                    var groups = row.status == DealStatus.Broken ? UnifiedVoteHistory.Incidents(s) : UnifiedVoteHistory.Fulfillments(s);
                    bool accountability = question.category == FinaleQuestions.Accountability;
                    if (!groups.Any(group => group.OwnerId == row.id && (group.ActorId == player && group.WrongedId == juror
                            || !accountability && group.ActorId == juror && group.WrongedId == player)))
                        return false;
                    if (question.receiptKind == FinaleQuestions.PromiseReceipt)
                    {
                        if (row.sourcePolicy != UnifiedCommitments.PromisePolicy || row.makerId != player || row.beneficiaryId != juror)
                            return false;
                        return question.category == FinaleQuestions.Accountability
                            ? row.status == DealStatus.Broken && decision.ActorId == player
                            : question.category == FinaleQuestions.Personal && row.status == DealStatus.Fulfilled;
                    }
                    if (question.receiptKind != FinaleQuestions.DealReceipt || row.sourcePolicy != UnifiedCommitments.DealPolicy
                        || !((row.makerId == player && row.beneficiaryId == juror) || (row.makerId == juror && row.beneficiaryId == player)))
                        return false;
                    var deal = CommitmentReferences.FindDeal(s, row.id);
                    if (deal == null) return false;
                    // Agreement-local truth proves no grouped reward/incident owner. Keep the source's
                    // independent player-knowledge and actor reader; never read the archive as knowledge.
                    return question.category == FinaleQuestions.Accountability
                        ? row.status == DealStatus.Broken && FinalistRead.DealBreaker(s, deal) == player
                        : question.category == FinaleQuestions.Personal && row.status == DealStatus.Fulfilled
                            && KnownBallots.DealOutcomeKnown(s, deal);
                }

        // SOURCE Gamesim.Simulation.EpisodeValidation.ValidSavedVoteOpportunity 2369:5736
        private static bool ValidSavedVoteOpportunity(EpisodeState s, UnifiedCommitmentState row, OpportunityRow opportunity)
                {
                    if (opportunity.anchor != null || opportunity.currency != null || opportunity.steps.Count != 0
                        || opportunity.payoff != 0) return false;
                    bool taken = opportunity.response == OpportunityResponse.Taken;
                    bool neutral = opportunity.outcome == OpportunityOutcome.NotApplicable;
                    bool answered = row.voteBindingWeek > 0 && (row.status == DealStatus.Active || row.status == DealStatus.Broken
                        || row.status == DealStatus.Fulfilled || row.status == DealStatus.Expired);
                    // Actual immediate command-created opportunities precede reconciliation. Generic
                    // NPC/Story/Lobby creation does not fabricate an immediate Taken opportunity.
                    if (opportunity.source == null && opportunity.note == null)
                        return taken && neutral && answered && (row.origin == UnifiedCommitments.PlayerDeal
                            || row.origin == UnifiedCommitments.NpcOffer || row.origin == UnifiedCommitments.CounterDeal
                            || row.origin == UnifiedCommitments.CounterPrice || row.origin == UnifiedVoteFamilyValidation.VetoAskPrice
                            || row.origin == UnifiedVoteFamilyValidation.OwnVetoPrice);
                    string source = row.subtype + (row.targetId != null ? ":" + row.targetId : "");
                    if (opportunity.source != source || opportunity.note == null) return false;
                    string prefix = (row.makerId == s.playerId ? "put to " + row.beneficiaryId
                        : "offered by " + row.makerId) + ", ";
                    if (!opportunity.note.StartsWith(prefix, StringComparison.Ordinal)) return false;
                    string observed = opportunity.note.Substring(prefix.Length);
                    // A real yes changes Taken only until the next reconciliation. It does not replace
                    // the retained creation week, previous Proposed note or answering-week markers.
                    if (observed == DealStatus.Proposed)
                        return row.origin == UnifiedCommitments.NpcOffer && neutral
                            && ((opportunity.response == OpportunityResponse.Ignored && row.voteBindingWeek == 0
                                    && (row.status == DealStatus.Proposed || row.status == DealStatus.Declined || row.status == DealStatus.Expired))
                                || (taken && answered));
                    if (observed == DealStatus.Active) return taken && neutral && answered;
                    if (observed == DealStatus.Declined)
                        return row.status == DealStatus.Declined && row.voteBindingWeek == 0
                            && opportunity.response == OpportunityResponse.Declined && neutral;
                    if (observed == DealStatus.Expired)
                        return row.status == DealStatus.Expired && neutral && (opportunity.response == OpportunityResponse.Expired
                            || (taken && answered && EpisodeEngine.CommitmentRulesOn(s)));
                    if (observed == DealStatus.Fulfilled)
                        return row.status == DealStatus.Fulfilled && taken && opportunity.outcome == OpportunityOutcome.Won;
                    if (observed == DealStatus.Broken)
                        return row.status == DealStatus.Broken && taken && opportunity.outcome == OpportunityOutcome.Lost;
                    return false;
                }

        // SOURCE Gamesim.Simulation.EpisodeValidation.WaveDLinesKept 2923:3123
        private static bool WaveDLinesKept(EpisodeState s, int startWeek, params string[] kinds) =>
                    !s.events.Any(e => Array.IndexOf(kinds, e.kind) >= 0 && (startWeek == 0 || e.week < startWeek));

        // SOURCE Gamesim.Simulation.EpisodeValidation.WaveDStartWeek 2695:2806
        private static bool WaveDStartWeek(EpisodeState s, int week) => week >= 0 && week <= 101 && week <= s.week + 1;
    }
}

