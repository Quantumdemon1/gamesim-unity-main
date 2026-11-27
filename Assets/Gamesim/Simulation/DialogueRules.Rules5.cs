using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Dialogue rules 5 (DIALOGUE-PLAN §2's ladder, slice D5b-1): the HoH consultation (§4, §4.1 and §5.1). The player as Head
    /// of Household, in the nominations before the original nominations commit, calls a houseguest in
    /// (<see cref="DialogueHoHConsult"/>): once a week for each houseguest, paid by the HoH grant while it is open, or else by
    /// one of the window's seats after the HoH, within <see cref="FreeConversationsAWeek"/> free conversations a week. A promise
    /// made in it is the existing promise of safety (under the role moments, with their pitch on the board, the pitch's own
    /// Promise safety answer), never a second safety row to that houseguest that week. It lapses when the nominations commit,
    /// and is not offered again for a replacement.
    ///
    /// <para>Built switched off: no season holds rules 5, and the cast screen's chip still writes <see cref="Offered"/> 4.
    /// Rules 5 keeps rules 4 whole and adds no saved field: the HoH grant is a <see cref="DialogueRoleGrantState"/>, at home
    /// since schema 30 (§6). Nothing here draws from the season's stream: the answer and the reaction are keyed coins, and the
    /// reaction's +3 or -3 goes through <c>ChangeKeyed</c>. Nothing here reads <see cref="Version"/> for behaviour.</para>
    /// </summary>
    public static partial class DialogueRules
    {
        /// <summary>The version that introduced the HoH consultation and the HoH grant.</summary>
        public const int Rules5 = 5;

        /// <summary>The role that grants a conversation at rules 5: the Head of Household (§4's table).</summary>
        public const string HoHRole = "hoh";

        /// <summary>What wrote an HoH grant: the player crowned Head of Household, as the nominations opened.</summary>
        public const string FromCrowning = "crowned";

        /// <summary>The words a consultation is refused in once the nominations are no longer ahead.</summary>
        public const string HoHConsultRefusal = "The nominations are no longer ahead of you.";

        /// <summary>The refusal when the player is not the Head of Household.</summary>
        public const string HoHConsultNotHoH = "Only the Head of Household calls a houseguest in.";

        /// <summary>The refusal when the houseguest is not another houseguest still in the house.</summary>
        public const string HoHConsultNotHere = "Call in a houseguest who is still in the house.";

        /// <summary>The refusal when this houseguest's case was already heard this week.</summary>
        public const string HoHConsultUsedRefusal = "You have already heard their case this week.";

        public static string HoHConsultOccasion(int week, string npcId) =>
            "week:" + week.ToString(CultureInfo.InvariantCulture) + ":" + DialogueHoHConsult.Kind + ":" + npcId;

        /// <summary>Whether a season's dialogue rules hold the HoH consultation and the HoH grant: rules 5 and later.</summary>
        public static bool HoHConsultOn(EpisodeState s) => On(s) && s.dialogueRulesVersion >= Rules5;

        internal static ArcTemplate Rules5Template(string id, int version) =>
            id == DialogueHoHConsult.TemplateId && version == DialogueHoHConsult.ContentVersion ? DialogueHoHConsult.CreateTemplate() : null;

        internal static bool IsRules5Template(string id) => id == DialogueHoHConsult.TemplateId;

        private static bool IsRules5Content(string id) => id != null && id.StartsWith(DialogueHoHConsult.TemplateId + ":", StringComparison.Ordinal);

        /// <summary>The role whose grant pays for a conversation of this kind: the HoH's for the consultation, the nominee's otherwise.</summary>
        internal static string GrantRoleFor(string kind) => kind == DialogueHoHConsult.Kind ? HoHRole : NomineeRole;

        /// <summary>
        /// Whether the consultation with this houseguest is still playable: the player is in the house and the Head of
        /// Household, the nominations are open and nobody is named yet, and the houseguest is somebody else still in the house.
        /// </summary>
        private static bool HoHConsultEligible(EpisodeState s, string npcId, out string error)
        {
            if (s.phase != EpisodePhase.Nomination || s.nominees == null || s.nominees.Count != 0
                || s.Find(s.playerId)?.status != ContestantStatus.Active)
                return Fail(out error, HoHConsultRefusal);
            if (s.hohId != s.playerId) return Fail(out error, HoHConsultNotHoH);
            if (string.IsNullOrEmpty(npcId) || npcId == s.playerId || s.Find(npcId)?.status != ContestantStatus.Active)
                return Fail(out error, HoHConsultNotHere);
            error = null;
            return true;
        }

        /// <summary>Whether this houseguest can be called in now: the consultation's entry can be built.</summary>
        public static bool CanCallIn(EpisodeState s, string npcId) =>
            HoHConsultOn(s) && KindNow(s) == DialogueHoHConsult.Kind && TryBuildEntryCommand(s, npcId, out var plan)
            && plan.text == DialogueHoHConsult.TemplateId;

        /// <summary>Whether a consultation with this houseguest, called in now, would be on the house: the HoH grant pays.</summary>
        public static bool CallInOnTheHouse(EpisodeState s, string npcId) => CanCallIn(s, npcId) && GrantPaysNow(s);

        /// <summary>
        /// The houseguest's answer to the open, on the occasion's own keyed coin at path index 0 (§5.2), weighed by how they see
        /// the player: one who sees them warmly pleads their case, one who does not keeps their cards close. Draws nothing from
        /// the season's stream.
        /// </summary>
        internal static string HoHConsultAnswer(EpisodeState s, string npcId, string occasionId) =>
            StoryRandom.Unit(s, Key(occasionId, 0)) < Candour(s, npcId) ? DialogueHoHConsult.Pleading : DialogueHoHConsult.Guarded;

        /// <summary>The houseguest's one reaction to a reply, on the reply's own keyed coin; null for an option that draws nothing.</summary>
        internal static string HoHConsultReaction(EpisodeState s, string occasionId, int pathIndex, string optionId)
        {
            if (!DialogueHoHConsult.TryOdds(optionId, out double warm, out double sour)) return null;
            double draw = StoryRandom.Unit(s, Key(occasionId, pathIndex));
            return draw < warm ? DialogueHoHConsult.Warmed : draw >= 1 - sour ? DialogueHoHConsult.Backfired : DialogueHoHConsult.Unmoved;
        }

        /// <summary>
        /// The houseguest's pitch waiting on the role moments' board (ROLE-MOMENTS-PLAN R5), or null: a promise made in the
        /// consultation is then the pitch's own Promise safety answer (§4's table).
        /// </summary>
        internal static ReplyCardState PitchOnTheBoard(EpisodeState s, string npcId) => !HoHPitches.OnTheBoard(s) ? null
            : RoleOffers.PitchBoardCards(s).FirstOrDefault(c => c.fromId == npcId && HoHPitches.ValidCard(s, c));

        // ---------------------------------------------------------------- the validator

        /// <summary>An HoH grant (rules 5): written as the nominations opened, with nobody named, to the week's Head of Household.</summary>
        private static bool HoHGrantSource(EpisodeState s, DialogueRoleGrantState g) =>
            s.dialogueRulesVersion >= Rules5 && g.role == HoHRole && g.sourceKind == FromCrowning
            && g.sourcePhase == EpisodePhase.Nomination && g.sourceNominees != null && g.sourceNominees.Count == 0
            && (g.week != s.week || s.hohId == s.playerId);

        /// <summary>
        /// One HoH consultation (§4.1, §5.1): the open, answered at once, the reply node, the reaction and the finish; it ends
        /// in a promise of safety, no promise, the exit, or the lapse at the nominations.
        /// </summary>
        private static bool EpisodeHoHConsult(EpisodeState s, DialogueEpisodeState e, EpisodeEngine.PendingDialogueCommand pending,
            HashSet<string> ticks, HashSet<int> logs, HashSet<string> ids, out string error)
        {
            error = null;
            const int steps = 3;
            int version = s.dialogueRulesVersion;
            var cycle = Cycle(s, e);
            var template = Rules5Template(cycle?.templateId, e.contentVersion);
            const string kind = DialogueHoHConsult.Kind;
            const EpisodePhase phase = EpisodePhase.Nomination;
            if (template == null || e.occasionKind != kind
                || e.week < 1 || e.week > s.week || e.window != Windows.AfterHoH || e.startedPhase != phase
                || e.startedSequence < 1 || e.startedSequence >= s.nextSequence
                || e.id != "story-" + e.startedSequence.ToString(CultureInfo.InvariantCulture)
                || string.IsNullOrEmpty(e.npcId) || e.npcId.Length > 160 || e.npcId == s.playerId
                || s.Find(e.npcId) == null || e.occasionId != HoHConsultOccasion(e.week, e.npcId)
                || !Revision(s, e.startedRevision, e, DialogueRevisionField.Started, pending))
                return Fail(out error, "Invalid consultation identity or start.");
            if (cycle.variant != 0 || cycle.week != e.week || cycle.title != template.title || cycle.category != template.eyebrow
                || cycle.lane != StoryLanes.Game || !Cast(cycle.cast, e.npcId) || cycle.vars == null || cycle.vars.Count != 0
                || cycle.nextWeek != 0 || cycle.nextAnchor != null || cycle.path == null || cycle.path.Count < 1 || cycle.path.Count > steps)
                return Fail(out error, "The dialogue must own its exact consultation cycle.");
            if (e.entry == null || !AcceptsFunding(version, e.entry.kind))
                return Fail(out error, "This version funds a consultation with the HoH grant or the window's action only.");
            string tick = EntryTick(e.entry, e.week, e.window);
            if (e.entry.kind == GrantEntry) { if (!GrantEntryRules4(s, e, pending, ticks, out error)) return false; }
            else if (tick == null || e.entry.grantId != null
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
            if (e.responses == null || e.responses.Count > steps || e.responses.Any(r => r == null)
                || e.evidence == null || e.evidence.Count != 0 || e.statement != null)
                return Fail(out error, "Unsupported consultation response, statement or evidence storage.");
            var items = s.houseEvents.Where(item => item.cycleId == e.id).ToArray();
            if (items.Length < 2 || items.Length > steps || items[0].id != e.entryEventId)
                return Fail(out error, "The consultation's event shelf does not match its entry.");
            string beatId = DialogueHoHConsult.Open, ending = null, answer = null;
            int previousSequence = e.startedSequence, previousRevision = e.startedRevision - 1, responseIndex = 0;
            for (int index = 0; index < items.Length; index++)
            {
                var item = items[index]; var beat = template.Beat(beatId);
                if (beat == null || !EventRules2(item, beat, e, template.id, out int eventSequence) || eventSequence <= previousSequence || eventSequence >= s.nextSequence)
                    return Fail(out error, "A retained consultation event differs from its canonical definition.");
                previousSequence = eventSequence;
                if (index >= cycle.path.Count)
                {
                    if (index != items.Length - 1 || item.resolved || item.chosenIndex != -1 || item.outcome != null)
                        return Fail(out error, "Only the current consultation node may remain unanswered.");
                    break;
                }
                var step = cycle.path[index];
                if (step == null || step.beatId != beatId || step.week != e.week || !item.resolved
                    || item.chosenIndex < 0 || item.chosenIndex >= item.choices.Count
                    || step.optionId != item.choices[item.chosenIndex].optionId
                    || step.logSequence <= eventSequence || step.logSequence >= s.nextSequence || !logs.Add(step.logSequence)
                    || s.storylines.Any(other => other.id != e.id && other.path != null && other.path.Any(p => p != null && p.logSequence == step.logSequence)))
                    return Fail(out error, "A consultation step does not own its resolved event and outcome.");
                var option = beat.Option(step.optionId);
                bool lapsed = step.result == StoryResults.Lapsed;
                if (index == 0 && (step.optionId != DialogueHoHConsult.OpenCase || step.result != StoryResults.Plain)
                    || step.result != StoryResults.Plain && !lapsed
                    || lapsed && (index != cycle.path.Count - 1 || step.optionId != DialogueHoHConsult.Leave)
                    || item.outcome != (lapsed ? DialogueCampaignV1.LapsedOutcome : option.outcome))
                    return Fail(out error, "The consultation must follow its exact authored edges.");
                previousSequence = step.logSequence;
                string actual = Suffix(e, responseIndex, template.id), suffix;
                if (lapsed) suffix = DialogueHoHConsult.OccasionClosed;
                else if (option.next == DialogueHoHConsult.ByAnswer)
                {
                    if (Array.IndexOf(DialogueHoHConsult.Answers, actual) < 0)
                        return Fail(out error, "The answer is not one this consultation can give.");
                    answer = suffix = actual;
                }
                else if (option.next == DialogueHoHConsult.ByReaction)
                {
                    if (Array.IndexOf(DialogueHoHConsult.Reactions, actual) < 0)
                        return Fail(out error, "The reaction is not one this consultation can give.");
                    suffix = actual;
                }
                else if (step.optionId == DialogueHoHConsult.PromiseSafety)
                {
                    if (actual != DialogueHoHConsult.Promised && actual != DialogueHoHConsult.Reaffirmed)
                        return Fail(out error, "A promise of safety must be made or reaffirmed.");
                    suffix = actual;
                }
                else suffix = step.optionId == DialogueHoHConsult.NoPromise ? DialogueHoHConsult.NoPromiseMade : DialogueHoHConsult.RespectfulExit;
                bool omitted = lapsed && responseIndex == e.responses.Count;
                if (!omitted)
                {
                    if (e.responses.Count <= responseIndex) return Fail(out error, "A resolved consultation step is missing its response.");
                    var response = e.responses[responseIndex++];
                    if (response.eventId != item.id || response.pathIndex != index || response.speakerId != e.npcId
                        || response.responseKey != template.id + "/" + suffix
                        || !Revision(s, response.revision, response, DialogueRevisionField.Response, pending)
                        || response.revision <= previousRevision || index == 0 && response.revision != e.entry.revision)
                        return Fail(out error, "A consultation response does not match its accepted step.");
                    previousRevision = response.revision;
                }
                var retainedLog = s.events.FirstOrDefault(row => row.sequence == step.logSequence);
                if (retainedLog != null && (retainedLog.kind != StoryLog.Outcome || retainedLog.week != e.week
                    || retainedLog.phase != phase || retainedLog.audienceIds == null
                    || !retainedLog.audienceIds.SequenceEqual(new[] { s.playerId, e.npcId })))
                    return Fail(out error, "A retained consultation outcome has the wrong owner or audience.");
                string next = lapsed ? "end:lapsed" : option.next;
                if (next.StartsWith("end:", StringComparison.Ordinal))
                {
                    ending = next.Substring(4);
                    if (index != cycle.path.Count - 1 || index != items.Length - 1)
                        return Fail(out error, "A terminal consultation cannot contain another node.");
                    break;
                }
                beatId = next == DialogueHoHConsult.ByAnswer ? DialogueHoHConsult.ReplyBeat(answer)
                    : next == DialogueHoHConsult.ByReaction ? DialogueHoHConsult.FinishBeat(suffix) : next;
            }
            if (responseIndex != e.responses.Count || cycle.eventId != items[items.Length - 1].id || cycle.beatId != beatId)
                return Fail(out error, "The consultation's path, current node and responses must agree.");
            if (ending == null)
            {
                if (cycle.status != StorylineStatus.Active || cycle.endedWeek != 0 || cycle.endingId != null
                    || e.time != null || items.Length != cycle.path.Count + 1)
                    return Fail(out error, "An active consultation must retain its funded unanswered node.");
                if (e.week != s.week) return Fail(out error, "An active conversation cannot survive its week.");
                if (!HoHConsultEligible(s, e.npcId, out error)) return false;
            }
            else
            {
                if (cycle.status != StorylineStatus.Completed || cycle.endedWeek != e.week || cycle.endingId != ending
                    || e.time == null || e.time.reason != (ending == "lapsed" ? DialogueHoHConsult.OccasionClosed : ending)
                    || e.time.window != e.window || !TimeReceiptHolds(s, template, cycle, e, ending == "lapsed")
                    || e.time.proposedDelta != 0 || e.time.appliedDelta != 0
                    || !Revision(s, e.time.revision, e.time, DialogueRevisionField.Time, pending)
                    || e.time.sequence != cycle.path[cycle.path.Count - 1].logSequence)
                    return Fail(out error, "A closed consultation requires its exact zero-time receipt.");
                bool omitted = responseIndex != cycle.path.Count;
                bool present = s.Find(e.npcId).status == ContestantStatus.Active;
                if (omitted && (ending != "lapsed" || e.time.revision <= previousRevision || present)
                    || !omitted && e.time.revision != previousRevision)
                    return Fail(out error, "The consultation's closure and its actual speaker response disagree.");
                // A lapse is the nominations' (§4.1): never while the houseguest could still be called in and heard.
                if (ending == "lapsed" && e.week == s.week && s.phase == phase
                    && HoHConsultEligible(s, e.npcId, out _) && e.time.revision <= s.revision)
                    return Fail(out error, "The current consultation has not closed.");
            }
            return true;
        }
    }
}
