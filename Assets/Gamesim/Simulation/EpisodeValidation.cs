using System;
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
            error = null;
            if (s == null || s.schemaVersion != 13) return Fail(out error, "Unsupported episode schema.");
            if (s.competitionRulesVersion < 1 || s.competitionRulesVersion > 3)
                return Fail(out error, "Unsupported competition rules version.");
            if (!Text(s.sessionId, 160) || s.week < 1 || s.week > 100 || s.revision < 0 || s.revision > 1000000 ||
                s.nextSequence < 1 || s.nextSequence > 1000000 || s.socialActions < 0
                || s.socialActions > MostActionsAWeekCanHold || !Defined(s.phase))
                return Fail(out error, "Invalid session counters or phase.");
            if (s.blocRulesStartWeek < 1 || s.blocRulesStartWeek > 101 || s.blocRulesStartWeek > s.week + 1)
                return Fail(out error, "Voting-bloc activation week must be within the saved season boundary.");
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
            if (s.storyRulesStartWeek < 1 || s.storyRulesStartWeek > Math.Min(101, s.week + 1))
                return Fail(out error, "A storyline rules boundary cannot be further off than next week.");
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
            return TryValidateV2(s, out error) && TryValidateV3(s, out error) && TryValidateNpcSocial(s, out error);
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
