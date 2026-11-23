using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Dialogue rules 4 (DIALOGUE-PLAN §2's ladder, slice D4): the veto question and the conditional promise (§4.2,
    /// decision 3). A nominated player, after the veto players are drawn and before the competition is played, asks a veto
    /// player "If you win the veto, will you use it on me?" (<see cref="DialogueVetoQuestion"/>). The answer is drawn on a
    /// keyed coin and kept as the conversation's statement; after a yes, "Can we make that a promise?" writes one
    /// <see cref="DealKind.VetoIf"/> deal, <c>deal-ifwin-&lt;n&gt;</c>. The veto meeting's opening writes the week's
    /// <see cref="DialogueVetoOccasionState"/> and lapses an unmet promise; the house's veto decision settles a met one in
    /// its own step (EpisodeEngine.Dialogue.Rules4.cs).
    ///
    /// <para>Rules 4 keeps rules 3 whole and adds no saved field: every row it writes has a home at schema 30 (§6). It is
    /// defined and not offered: <see cref="Offered"/> stays 3 until the stack's last slice moves the chip. Nothing here reads
    /// <see cref="Version"/> for behaviour.</para>
    /// </summary>
    public static partial class DialogueRules
    {
        /// <summary>The version that introduced the veto question and the conditional promise.</summary>
        public const int Rules4 = 4;

        /// <summary>The words the veto question is refused in once it has nothing left to ask.</summary>
        public const string VetoQuestionRefusal = "The veto competition is no longer ahead of you.";

        /// <summary>The refusal when the player is not on the block: only a nominee has the veto question to ask.</summary>
        public const string VetoQuestionNotNominated = "You can ask about the veto only while you are on the block.";

        /// <summary>The refusal when the houseguest asked is not another veto player still in the house.</summary>
        public const string VetoQuestionNotAVetoPlayer = "Ask a houseguest who is playing in this veto.";

        /// <summary>The prefix of a conditional promise's id. No other deal prefix begins this way (§4.2 point 2).</summary>
        public const string IfWinPrefix = "deal-ifwin-";

        /// <summary>Why a promise was not given: each refusal in its own words, the statement standing either way (§4.2).</summary>
        public const string PromiseConflictRefusal = "\"I can't promise that. I've already given my word on the veto this week.\"",
            PromiseCapacityRefusal = "\"I've already made one promise about the veto this week.\"",
            PromiseRoomRefusal = "\"You already have more arrangements than you can keep track of.\"";

        public static string VetoQuestionOccasion(int week, string npcId) =>
            "week:" + week.ToString(CultureInfo.InvariantCulture) + ":" + DialogueVetoQuestion.Kind + ":" + npcId;

        /// <summary>Whether a season's dialogue rules hold the veto question: rules 4 and later.</summary>
        public static bool VetoQuestionOn(EpisodeState s) => On(s) && s.dialogueRulesVersion >= Rules4;

        public static string IfWinId(int sequence) => IfWinPrefix + sequence.ToString(CultureInfo.InvariantCulture);

        internal static ArcTemplate Rules4Template(string id, int version) =>
            id == DialogueVetoQuestion.TemplateId && version == DialogueVetoQuestion.ContentVersion ? DialogueVetoQuestion.CreateTemplate() : null;

        internal static bool IsRules4Template(string id) => id == DialogueVetoQuestion.TemplateId;

        private static bool IsRules4Content(string id) => id != null && id.StartsWith(DialogueVetoQuestion.TemplateId + ":", StringComparison.Ordinal);

        /// <summary>
        /// Whether the veto question to this houseguest is still playable: the player is in the house and nominated, the veto
        /// players are drawn and the competition is not yet played, and the houseguest is another veto player still in the house.
        /// </summary>
        private static bool VetoQuestionEligible(EpisodeState s, string npcId, out string error)
        {
            if (s.phase != EpisodePhase.Veto || s.competitionResolved || s.Find(s.playerId)?.status != ContestantStatus.Active
                || s.vetoPlayers == null)
                return Fail(out error, VetoQuestionRefusal);
            if (s.nominees == null || !s.nominees.Contains(s.playerId))
                return Fail(out error, VetoQuestionNotNominated);
            if (string.IsNullOrEmpty(npcId) || npcId == s.playerId || !s.vetoPlayers.Contains(npcId)
                || s.Find(npcId)?.status != ContestantStatus.Active)
                return Fail(out error, VetoQuestionNotAVetoPlayer);
            error = null;
            return true;
        }

        /// <summary>Whether the veto question can be asked of this houseguest now: the conversation's entry can be built.</summary>
        public static bool CanAskVetoQuestion(EpisodeState s, string npcId) =>
            VetoQuestionOn(s) && KindNow(s) == DialogueVetoQuestion.Kind && TryBuildEntryCommand(s, npcId, out var plan)
            && plan.text == DialogueVetoQuestion.TemplateId;

        /// <summary>
        /// The veto player's answer (§4.2 point 1), on the occasion's own keyed coin at path index 0 (§5.2), weighed by how
        /// they see the player; a fellow nominee is less ready to give a yes. It records what was said, not proof of a plan.
        /// Nothing is drawn from the season's stream.
        /// </summary>
        internal static string VetoAnswer(EpisodeState s, string npcId, string occasionId)
        {
            double warmth = Candour(s, npcId);
            bool fellowNominee = s.nominees != null && s.nominees.Contains(npcId);
            double yes = (fellowNominee ? 0.3 : 0.6) * warmth, condition = fellowNominee ? 0.1 : 0.15, notCommitting = fellowNominee ? 0.3 : 0.25;
            double draw = StoryRandom.Unit(s, Key(occasionId, 0));
            return draw < yes ? DialogueVetoQuestion.Yes
                : draw < yes + condition ? DialogueVetoQuestion.Condition
                : draw < yes + condition + notCommitting ? DialogueVetoQuestion.NotCommitting : DialogueVetoQuestion.No;
        }

        /// <summary>
        /// Why "Can we make that a promise?" is refused now, or null when it can be given (§4.2 point 2): the maker gives at most
        /// one a week; not while they are party to a binding veto commitment or keep-the-block deal this week, with anybody;
        /// and not with the player's arrangements at their ceiling, which counts every deal row.
        /// </summary>
        internal static string PromiseRefusal(EpisodeState s, string makerId)
        {
            var deals = UnifiedVoteStore.Deals(s);
            if (deals.Any(d => d != null && d.type == DealKind.VetoIf && d.proposerId == makerId && d.week == s.week))
                return PromiseCapacityRefusal;
            if (deals.Any(d => d != null && (d.type == DealKind.VetoUse || d.type == DealKind.VetoHold) && d.week == s.week
                    && DealStatus.Binds(d.status) && (d.proposerId == makerId || d.recipientId == makerId)))
                return PromiseConflictRefusal;
            if (UnifiedVoteStore.DealCount(s) >= PlayerDeals.PlayerDealCeiling) return PromiseRoomRefusal;
            return null;
        }

        /// <summary>
        /// Whether this deal is a conditional promise whose condition is met and which still stands: its maker holds the veto at
        /// this week's meeting, before the decision (§4.2 point 5). The holder's pull counts it once with a veto commitment.
        /// </summary>
        public static bool IsMetVetoIf(EpisodeState s, DealState d, string holderId) =>
            s != null && d != null && d.type == DealKind.VetoIf && d.status == DealStatus.Active && d.week == s.week
            && !string.IsNullOrEmpty(holderId) && d.proposerId == holderId && s.vetoHolderId == holderId
            && s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved;

        /// <summary>Whether this holder's met conditional promise stands: the holder's own offer (R8) is refused meanwhile (§4.2 point 5).</summary>
        public static bool HoldsMetVetoIf(EpisodeState s, string holderId) =>
            s?.deals != null && s.deals.Any(d => IsMetVetoIf(s, d, holderId));

        // ---------------------------------------------------------------- the validator

        /// <summary>
        /// The conditional promises a season holds, judged for every season (EpisodeValidation): none below dialogue rules 4,
        /// by kind or by id; from rules 4 each is exactly the row its veto question's promise step wrote.
        /// </summary>
        public static bool TryValidateConditionalVeto(EpisodeState s, out string error)
        {
            error = null;
            var rows = s?.deals?.Where(d => d != null && (d.type == DealKind.VetoIf
                || d.id != null && d.id.StartsWith(IfWinPrefix, StringComparison.Ordinal))).ToList();
            if (rows == null || rows.Count == 0) return true;
            if (!VetoQuestionOn(s) || s.dialogue?.episodes == null || s.dialogue.vetoOccasions == null || s.storylines == null)
                return Fail(out error, "A season below dialogue rules 4 has no conditional veto promise.");
            foreach (var deal in rows)
            {
                string digits = deal.id == null || deal.id.Length <= IfWinPrefix.Length ? null : deal.id.Substring(IfWinPrefix.Length);
                if (deal.type != DealKind.VetoIf || digits == null || !deal.id.StartsWith(IfWinPrefix, StringComparison.Ordinal)
                    || !int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int n) || n < 1 || IfWinId(n) != deal.id
                    || deal.proposerId == s.playerId || deal.recipientId != s.playerId || deal.targetId != s.playerId
                    || deal.expiresWeek != deal.week || deal.trustImpact != DealKind.DefaultTrust(DealKind.VetoIf) || deal.linkedDealId != null
                    || (deal.status != DealStatus.Active && deal.status != DealStatus.Expired
                        && deal.status != DealStatus.Fulfilled && deal.status != DealStatus.Broken))
                    return Fail(out error, "A conditional veto promise must keep its exact shape.");
                var episodes = s.dialogue.episodes.Where(e => e != null && e.occasionKind == DialogueVetoQuestion.Kind
                    && e.week == deal.week && e.npcId == deal.proposerId).Take(2).ToArray();
                var cycle = episodes.Length == 1 ? Cycle(s, episodes[0]) : null;
                if (cycle?.path == null || cycle.path.Count != 2 || cycle.path[1] == null || cycle.path[1].optionId != DialogueVetoQuestion.AskPromise
                    || cycle.path[1].logSequence != n || episodes[0].responses == null || episodes[0].responses.Count != 2
                    || episodes[0].responses[1]?.responseKey != DialogueVetoQuestion.TemplateId + "/" + DialogueVetoQuestion.PromiseMade)
                    return Fail(out error, "A conditional veto promise must be the promise its own conversation made.");
                var occasion = s.dialogue.vetoOccasions.FirstOrDefault(o => o != null && o.week == deal.week);
                bool met = occasion != null && occasion.holderId == deal.proposerId && occasion.nomineesAtResult != null
                    && occasion.nomineesAtResult.Contains(deal.targetId);
                if (deal.status == DealStatus.Active && (deal.week != s.week || occasion != null && !met
                        || !(s.phase == EpisodePhase.Veto || s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved)))
                    return Fail(out error, "A conditional veto promise stands only until its veto is decided.");
                if ((deal.status == DealStatus.Fulfilled || deal.status == DealStatus.Broken)
                    && (!met || occasion.decision == null
                        || (deal.status == DealStatus.Fulfilled) != (occasion.decision.used && occasion.decision.savedId == deal.targetId)))
                    return Fail(out error, "A settled conditional veto promise must match its veto decision.");
            }
            return true;
        }

        /// <summary>The week's veto occasions (§4.2 point 3): at most one a week, each the result as the veto meeting opened, and the house's decision once made.</summary>
        private static bool VetoOccasionsRules4(EpisodeState s, out string error)
        {
            error = null;
            var rows = s.dialogue.vetoOccasions;
            if (rows.Any(o => o == null) || rows.GroupBy(o => o.week).Any(g => g.Count() != 1))
                return Fail(out error, "Veto occasions must be one a week.");
            bool beforeTheResult = s.phase == EpisodePhase.HoH || s.phase == EpisodePhase.Nomination
                || s.phase == EpisodePhase.VetoSelection || s.phase == EpisodePhase.Veto;
            foreach (var o in rows)
            {
                if (o.week < 1 || o.week > s.week || o.week == s.week && beforeTheResult
                    || string.IsNullOrEmpty(o.holderId) || s.Find(o.holderId) == null || string.IsNullOrEmpty(o.hohId) || s.Find(o.hohId) == null
                    || o.participants == null || o.participants.Count == 0 || !o.participants.Contains(o.holderId)
                    || o.participants.Any(id => string.IsNullOrEmpty(id) || s.Find(id) == null) || o.participants.Distinct().Count() != o.participants.Count
                    || o.nomineesAtResult == null || o.nomineesAtResult.Any(id => string.IsNullOrEmpty(id) || s.Find(id) == null)
                    || o.nomineesAtResult.Distinct().Count() != o.nomineesAtResult.Count
                    || o.resultRevision < 1 || o.resultRevision > s.revision || o.resultSequence < 1 || o.resultSequence >= s.nextSequence)
                    return Fail(out error, "A veto occasion must keep the result it recorded.");
                var d = o.decision;
                if (d != null && (d.revision < o.resultRevision || d.revision > s.revision || d.sequence <= o.resultSequence || d.sequence >= s.nextSequence
                    || d.activeIds == null || d.nominees == null || d.replacementCandidates == null
                    || (d.used ? d.savedId == null || !d.nominees.Contains(d.savedId) || d.replacementId == null || !d.replacementCandidates.Contains(d.replacementId)
                        : d.savedId != null || d.replacementId != null)))
                    return Fail(out error, "A veto occasion's decision must keep what was decided.");
            }
            return true;
        }

        /// <summary>
        /// One veto question (§4.2 point 1, §5.1): the open, answered at once with the conversation's statement, then the reply
        /// node; it ends in the promise asked for, thanks, the exit, or the lapse at the competition's result.
        /// </summary>
        private static bool EpisodeVetoQuestion(EpisodeState s, DialogueEpisodeState e, EpisodeEngine.PendingDialogueCommand pending,
            HashSet<string> ticks, HashSet<int> logs, HashSet<string> ids, out string error)
        {
            error = null;
            var cycle = Cycle(s, e);
            var template = Rules4Template(cycle?.templateId, e.contentVersion);
            const string kind = DialogueVetoQuestion.Kind;
            const EpisodePhase phase = EpisodePhase.Veto;
            if (template == null || e.occasionKind != kind
                || e.week < 1 || e.week > s.week || e.window != Windows.AfterNominations || e.startedPhase != phase
                || e.startedSequence < 1 || e.startedSequence >= s.nextSequence
                || e.id != "story-" + e.startedSequence.ToString(CultureInfo.InvariantCulture)
                || string.IsNullOrEmpty(e.npcId) || e.npcId.Length > 160 || e.npcId == s.playerId
                || s.Find(e.npcId) == null || e.occasionId != VetoQuestionOccasion(e.week, e.npcId)
                || !Revision(s, e.startedRevision, e, DialogueRevisionField.Started, pending))
                return Fail(out error, "Invalid veto question identity or start.");
            if (cycle.variant != 0 || cycle.week != e.week || cycle.title != template.title || cycle.category != template.eyebrow
                || cycle.lane != StoryLanes.Game || !Cast(cycle.cast, e.npcId) || cycle.vars == null || cycle.vars.Count != 0
                || cycle.nextWeek != 0 || cycle.nextAnchor != null || cycle.path == null || cycle.path.Count < 1 || cycle.path.Count > 2)
                return Fail(out error, "The dialogue must own its exact veto question cycle.");
            if (e.entry == null || !AcceptsFunding(s.dialogueRulesVersion, e.entry.kind) || e.entry.kind != ActionEntry)
                return Fail(out error, "The veto question is paid for with the window's action only.");
            string tick = EntryTick(e.entry, e.week, e.window);
            if (tick == null || e.entry.grantId != null
                || e.entry.revision != e.startedRevision || !Revision(s, e.entry.revision, e.entry, DialogueRevisionField.Entry, pending)
                || e.entry.windowActionBefore < 0 || e.entry.windowActionAfter > MaximumAction
                || (long)e.entry.windowActionAfter != (long)e.entry.windowActionBefore + 1
                || e.week == s.week && e.entry.windowActionAfter > s.windowActions[e.window]
                || !ticks.Add(tick))
                return Fail(out error, "Invalid ordinary dialogue entry receipt.");
            var earlier = s.dialogue.episodes.TakeWhile(row => !ReferenceEquals(row, e))
                .LastOrDefault(row => row.week == e.week && row.window == e.window && row.entry?.kind == ActionEntry);
            if (earlier != null && e.entry.windowActionBefore < earlier.entry.windowActionAfter)
                return Fail(out error, "Dialogue entry ticks must retain their spending order.");
            if (e.responses == null || e.responses.Count > 2 || e.responses.Any(r => r == null)
                || e.evidence == null || e.evidence.Count != 0)
                return Fail(out error, "Unsupported veto question response or evidence storage.");
            var items = s.houseEvents.Where(item => item.cycleId == e.id).ToArray();
            if (items.Length != 2 || items[0].id != e.entryEventId)
                return Fail(out error, "The veto question's event shelf does not match its entry.");
            string beatId = DialogueVetoQuestion.Open, ending = null, answer = null;
            int previousSequence = e.startedSequence, previousRevision = e.startedRevision - 1, responseIndex = 0;
            for (int index = 0; index < items.Length; index++)
            {
                var item = items[index]; var beat = template.Beat(beatId);
                if (beat == null || !EventRules2(item, beat, e, template.id, out int eventSequence) || eventSequence <= previousSequence || eventSequence >= s.nextSequence)
                    return Fail(out error, "A retained veto question event differs from its canonical definition.");
                previousSequence = eventSequence;
                if (index >= cycle.path.Count)
                {
                    if (index != items.Length - 1 || item.resolved || item.chosenIndex != -1 || item.outcome != null)
                        return Fail(out error, "Only the current veto question node may remain unanswered.");
                    break;
                }
                var step = cycle.path[index];
                if (step == null || step.beatId != beatId || step.week != e.week || !item.resolved
                    || item.chosenIndex < 0 || item.chosenIndex >= item.choices.Count
                    || step.optionId != item.choices[item.chosenIndex].optionId
                    || step.logSequence <= eventSequence || step.logSequence >= s.nextSequence || !logs.Add(step.logSequence)
                    || s.storylines.Any(other => other.id != e.id && other.path != null && other.path.Any(p => p != null && p.logSequence == step.logSequence)))
                    return Fail(out error, "A veto question step does not own its resolved event and outcome.");
                var option = beat.Option(step.optionId);
                bool lapsed = step.result == StoryResults.Lapsed;
                if (index == 0 && (step.optionId != DialogueVetoQuestion.OpenCase || step.result != StoryResults.Plain)
                    || step.result != StoryResults.Plain && !lapsed
                    || lapsed && (index != 1 || step.optionId != DialogueVetoQuestion.Leave)
                    || item.outcome != (lapsed ? DialogueCampaignV1.LapsedOutcome : option.outcome))
                    return Fail(out error, "The veto question must follow its exact authored edges.");
                previousSequence = step.logSequence;
                string actual = Suffix(e, responseIndex, template.id), suffix;
                if (lapsed) suffix = DialogueVetoQuestion.OccasionClosed;
                else if (index == 0)
                {
                    if (Array.IndexOf(DialogueVetoQuestion.Answers, actual) < 0)
                        return Fail(out error, "The answer is not one the veto question can give.");
                    answer = suffix = actual;
                }
                else if (step.optionId == DialogueVetoQuestion.AskPromise)
                {
                    if (actual != DialogueVetoQuestion.PromiseMade && actual != DialogueVetoQuestion.PromiseRefused)
                        return Fail(out error, "A promise asked for must be given or refused.");
                    bool made = s.deals != null && s.deals.Any(d => d != null && d.id == IfWinId(step.logSequence));
                    if (made != (actual == DialogueVetoQuestion.PromiseMade))
                        return Fail(out error, "A promise given must hold its own conditional veto deal, and only then.");
                    suffix = actual;
                }
                else suffix = step.optionId == DialogueVetoQuestion.Thank ? DialogueVetoQuestion.Thanked : DialogueVetoQuestion.RespectfulExit;
                bool omitted = lapsed && responseIndex == e.responses.Count;
                if (!omitted)
                {
                    if (e.responses.Count <= responseIndex) return Fail(out error, "A resolved veto question step is missing its response.");
                    var response = e.responses[responseIndex++];
                    if (response.eventId != item.id || response.pathIndex != index || response.speakerId != e.npcId
                        || response.responseKey != template.id + "/" + suffix
                        || !Revision(s, response.revision, response, DialogueRevisionField.Response, pending)
                        || response.revision <= previousRevision || index == 0 && response.revision != e.entry.revision)
                        return Fail(out error, "A veto question response does not match its accepted step.");
                    previousRevision = response.revision;
                }
                var retainedLog = s.events.FirstOrDefault(row => row.sequence == step.logSequence);
                if (retainedLog != null && (retainedLog.kind != StoryLog.Outcome || retainedLog.week != e.week
                    || retainedLog.phase != phase || retainedLog.audienceIds == null
                    || !retainedLog.audienceIds.SequenceEqual(new[] { s.playerId, e.npcId })))
                    return Fail(out error, "A retained veto question outcome has the wrong owner or audience.");
                string next = lapsed ? "end:lapsed" : index == 0 ? DialogueVetoQuestion.ByAnswer : option.next;
                if (index == 0 && option.next != DialogueVetoQuestion.ByAnswer)
                    return Fail(out error, "The veto question must follow its exact authored edges.");
                if (next.StartsWith("end:", StringComparison.Ordinal))
                {
                    ending = next.Substring(4);
                    if (index != cycle.path.Count - 1 || index != items.Length - 1)
                        return Fail(out error, "A terminal veto question cannot contain another node.");
                    break;
                }
                beatId = DialogueVetoQuestion.ReplyBeat(answer);
            }
            if (responseIndex != e.responses.Count || cycle.eventId != items[items.Length - 1].id || cycle.beatId != beatId)
                return Fail(out error, "The veto question's path, current node and responses must agree.");
            if (!StatementVetoQuestion(s, e, cycle, items, answer, pending, ids, out error)) return false;
            if (ending == null)
            {
                if (cycle.status != StorylineStatus.Active || cycle.endedWeek != 0 || cycle.endingId != null
                    || e.time != null || items.Length != cycle.path.Count + 1)
                    return Fail(out error, "An active veto question must retain its funded unanswered node.");
                if (e.week != s.week) return Fail(out error, "An active conversation cannot survive its week.");
                if (!VetoQuestionEligible(s, e.npcId, out error)) return false;
            }
            else
            {
                if (cycle.status != StorylineStatus.Completed || cycle.endedWeek != e.week || cycle.endingId != ending
                    || e.time == null || e.time.reason != (ending == "lapsed" ? DialogueVetoQuestion.OccasionClosed : ending)
                    || e.time.window != e.window || e.time.proposedDelta != 0 || e.time.appliedDelta != 0
                    || e.time.spentRevision != 0 || e.time.spentSequence != 0 || e.time.spentWindowAction != 0
                    || !Revision(s, e.time.revision, e.time, DialogueRevisionField.Time, pending)
                    || e.time.sequence != cycle.path[cycle.path.Count - 1].logSequence)
                    return Fail(out error, "A closed veto question requires its exact zero-time receipt.");
                bool omitted = responseIndex != cycle.path.Count;
                bool present = s.Find(e.npcId).status == ContestantStatus.Active;
                if (omitted && (ending != "lapsed" || e.time.revision <= previousRevision || present)
                    || !omitted && e.time.revision != previousRevision)
                    return Fail(out error, "The veto question's closure and its actual speaker response disagree.");
                // A lapse is the result's (§4.1): never while the question could still be asked and answered.
                if (ending == "lapsed" && e.week == s.week && s.phase == phase && !s.competitionResolved
                    && VetoQuestionEligible(s, e.npcId, out _) && e.time.revision <= s.revision)
                    return Fail(out error, "The current veto question has not closed.");
            }
            return true;
        }

        // The question's one statement (§6): what the veto player said, copied the moment they answered.
        private static bool StatementVetoQuestion(EpisodeState s, DialogueEpisodeState e, StorylineState cycle, HouseEventState[] items,
            string answer, EpisodeEngine.PendingDialogueCommand pending, HashSet<string> ids, out string error)
        {
            error = null;
            var row = e.statement;
            var response = e.responses.FirstOrDefault(r => r.pathIndex == 0);
            int sequence = cycle.path[0].logSequence - 1;
            if (row == null || response == null || answer == null || !Sequence(items[0], out int asked) || sequence <= asked || !FreeSequence(s, sequence)
                || row.id != "dialogue-statement-" + sequence.ToString(CultureInfo.InvariantCulture) || !ids.Add(row.id) || row.sequence != sequence
                || row.eventId != items[0].id || row.pathIndex != 0 || row.speakerId != e.npcId || row.targetId != s.playerId
                || row.conditionKind != DialogueVetoQuestion.ConditionKind || row.answer != answer || row.week != e.week
                || row.revision != response.revision || !Revision(s, row.revision, row, DialogueRevisionField.Statement, pending)
                || row.audienceIds == null || !row.audienceIds.SequenceEqual(new[] { s.playerId }))
                return Fail(out error, "A veto question must hold its exact answer as its statement.");
            return true;
        }
    }
}
