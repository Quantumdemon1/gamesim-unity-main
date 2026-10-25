using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    internal enum DialogueRevisionField { Started, Entry, Response, Time, EvidenceCaptured }

    /// <summary>
    /// Closed dialogue semantics, not a substitute for complete EpisodeValidation.
    /// An engine-owned pending command can admit only its exact sealed future receipt rows.
    /// </summary>
    public static class DialogueRules
    {
        public const int Version = 1, MaximumEpisodes = 100, MaximumEvents = 400, MaximumSteps = 3;
        private const int MaximumAction = EpisodeEngine.LegacySocialActionBudget + WebSocialVocabulary.PurchaseCeiling;

        /// <summary>
        /// The dialogue rules the cast screen's preview chip starts a season on (DIALOGUE-PLAN §2, D1): a pinned
        /// literal, frozen the day a shipped path could first write it, and moved by hand only when a later stack
        /// brings the chip a new version. Never <see cref="Version"/>, the bound: a season keeps the rules it began
        /// on, so a chip that wrote the bound would change the rules of every opted-in season the day it moved.
        /// Tests that mean rules 1 write the literal 1; only the chip's own tests read this, and pin its value.
        /// </summary>
        public const int Offered = 1;

        // Schema 29 introduced the dialogue store, and a later schema carries it as it is (DIALOGUE-PLAN §6, item 1):
        // a lower bound, so the next schema's bump cannot switch every rules-1 season off.
        public static bool On(EpisodeState s) => s != null && s.schemaVersion >= 29 && s.dialogueRulesVersion == Version;

        public static StorylineState Cycle(EpisodeState s, DialogueEpisodeState episode)
        {
            var matches = s?.storylines?.Where(c => c != null && c.id == episode?.id).Take(2).ToArray();
            return matches != null && matches.Length == 1 ? matches[0] : null;
        }

        public static DialogueEpisodeState Active(EpisodeState s)
        {
            var matches = s?.dialogue?.episodes?.Where(e => e != null && Cycle(s, e)?.status == StorylineStatus.Active).Take(2).ToArray();
            return matches != null && matches.Length == 1 ? matches[0] : null;
        }

        /// <summary>Unallocated events reserved by the exact active three-step path.</summary>
        public static int ReservedEvents(EpisodeState s)
        {
            var active = Active(s);
            return active == null || s.houseEvents == null ? 0
                : Math.Max(0, MaximumSteps - s.houseEvents.Count(e => e != null && e.cycleId == active.id));
        }

        public static bool CanStart(EpisodeState s, string npcId, out string error)
        {
            if (!TryValidate(s, out error)) return false;
            if (!On(s)) return Fail(out error, "Dialogue rules are not active.");
            if (!Eligible(s, npcId, out error)) return false;
            if (Active(s) != null) return Fail(out error, "Finish the active dialogue first.");
            if (s.dialogue.episodes.Any(e => e.occasionId == Occasion(s.week, npcId)))
                return Fail(out error, "This campaign occasion has already been used.");
            if (s.dialogue.episodes.Count >= MaximumEpisodes || s.storylines.Count >= MaximumEpisodes
                || s.houseEvents.Count > MaximumEvents - MaximumSteps)
                return Fail(out error, "The complete dialogue path cannot fit.");
            if (s.houseEvents.Any(e => !e.resolved && HouseEventKind.Asks(e.kind)))
                return Fail(out error, "Answer the existing house moment first.");
            if (EpisodeEngine.SocialActionsSpent(s) >= EpisodeEngine.SocialActionBudget(s))
                return Fail(out error, "You have no conversations left this week.");
            return true;
        }

        public static string Occasion(int week, string npcId) => "week:" + week.ToString(CultureInfo.InvariantCulture) + ":campaign:" + npcId;

        /// <summary>Presentation-only command plans. They convey no pending authority; Apply
        /// independently checks the captured revision, actor, phase, exact episode and node.</summary>
        public static bool TryBuildEntryCommand(EpisodeState s, string npcId, out EpisodeCommand command)
        {
            command = null;
            if (!CanStart(s, npcId, out _) || !EpisodeValidation.TryValidate(s, out _)) return false;
            command = ViewCommand(s, EpisodeCommandKind.StartDialogue, npcId,
                DialogueCampaignV1.OpenCase, EntryTemplateId(s, npcId));
            return true;
        }

        public static bool TryBuildChoiceCommand(EpisodeState s, string eventId, string optionId, out EpisodeCommand command)
        {
            command = null;
            if (!On(s) || !EpisodeValidation.TryValidate(s, out _)) return false;
            var episode = Active(s);
            if (episode == null) return false;
            var cycle = Cycle(s, episode);
            var item = s.houseEvents.SingleOrDefault(e => e.id == eventId);
            if (item == null || item.resolved || item.cycleId != episode.id
                || item.contentId != cycle.templateId + ":" + cycle.beatId
                || !item.choices.Any(c => c.optionId == optionId)) return false;
            command = ViewCommand(s, EpisodeCommandKind.ChooseDialogue, episode.id, optionId, cycle.beatId);
            return true;
        }

        private static EpisodeCommand ViewCommand(EpisodeState s, EpisodeCommandKind kind, string target, string option, string text) =>
            new EpisodeCommand { actorId = s.playerId, expectedRevision = s.revision, expectedPhase = s.phase,
                kind = kind, targetId = target, secondTargetId = option, text = text };

        public static bool TryValidate(EpisodeState s, out string error) => TryValidate(s, null, out error);

        internal static bool TryValidate(EpisodeState s, EpisodeEngine.PendingDialogueCommand pending, out string error)
        {
            error = null;
            if (pending != null && !pending.Verify(s, out error)) return false;
            if (s == null || s.schemaVersion < 29) return Fail(out error, "Unsupported dialogue schema.");
            if (s.dialogue == null || s.dialogue.episodes == null || s.dialogue.grants == null || s.dialogue.vetoOccasions == null)
                return Fail(out error, "Dialogue storage must be complete.");
            if (s.dialogueRulesVersion == 0)
            {
                if (s.dialogue.episodes.Count != 0 || s.dialogue.grants.Count != 0 || s.dialogue.vetoOccasions.Count != 0
                    || s.unifiedCommitments?.Any(r => r?.conditionalVeto != null) == true
                    || s.storylines?.Any(c => c != null && IsCampaignTemplate(c.templateId)) == true
                    || s.houseEvents?.Any(e => IsCampaignContent(e?.contentId)) == true)
                    return Fail(out error, "Disabled dialogue storage must remain complete and inert.");
                return true;
            }
            if (s.dialogueRulesVersion != Version) return Fail(out error, "Unsupported dialogue rules version.");
            if (!Prerequisites(s)) return Fail(out error, "Dialogue requires the complete active-from-week-one rules.");
            if (s.unifiedCommitments == null || s.unifiedCommitments.Any(r => r == null || r.conditionalVeto != null))
                return Fail(out error, "Conditional dialogue obligations are not supported by this pilot.");
            if (s.week < 1 || s.week > 100 || s.revision < 0 || s.nextSequence < 1 || s.contestants == null
                || s.contestants.Any(c => c == null) || s.Find(s.playerId) == null
                || s.storylines == null || s.storylines.Count > MaximumEpisodes || s.storylines.Any(c => c == null)
                || s.houseEvents == null || s.houseEvents.Count > MaximumEvents || s.houseEvents.Any(e => e == null)
                || s.windowActions == null || s.windowActions.Count != Windows.Count || s.windowActions.Any(n => n < 0)
                || s.lobbies == null || s.lobbies.Any(l => l == null) || s.events == null || s.events.Any(e => e == null))
                return Fail(out error, "Dialogue requires complete bounded episode owners.");
            if (s.dialogue.episodes.Count > MaximumEpisodes || s.dialogue.episodes.Any(e => e == null)
                || s.dialogue.grants.Count != 0 || s.dialogue.vetoOccasions.Count != 0)
                return Fail(out error, "Only ordinary-funded campaign episodes are supported.");
            if (s.storylines.Any(c => c.templateId?.StartsWith("dialogue-", StringComparison.Ordinal) == true
                    && !IsCampaignTemplate(c.templateId))
                || s.houseEvents.Any(e => e.contentId?.StartsWith("dialogue-", StringComparison.Ordinal) == true
                    && !IsCampaignContent(e.contentId)))
                return Fail(out error, "Unknown dialogue content is not supported by this pilot.");
            if (s.dialogue.episodes.GroupBy(e => e.id).Any(g => g.Count() != 1)
                || s.dialogue.episodes.GroupBy(e => e.occasionId).Any(g => g.Count() != 1)
                || s.dialogue.episodes.GroupBy(e => e.entryEventId).Any(g => g.Count() != 1)
                || s.storylines.GroupBy(c => c.id).Any(g => g.Count() != 1)
                || s.houseEvents.GroupBy(e => e.id).Any(g => g.Count() != 1))
                return Fail(out error, "Dialogue identities must be unique.");
            var episodeIds = new HashSet<string>(s.dialogue.episodes.Select(e => e.id), StringComparer.Ordinal);
            if (s.storylines.Any(c => IsCampaignTemplate(c.templateId) && !episodeIds.Contains(c.id))
                || s.houseEvents.Any(e => IsCampaignContent(e.contentId) && !episodeIds.Contains(e.cycleId)))
                return Fail(out error, "A campaign cycle or event has no dialogue owner.");
            var ticks = new HashSet<string>(StringComparer.Ordinal);
            var logs = new HashSet<int>();
            var evidenceIds = new HashSet<string>(StringComparer.Ordinal);
            DialogueEpisodeState previous = null;
            int activeCount = 0;
            foreach (var episode in s.dialogue.episodes)
            {
                if (previous != null && (previous.time == null || episode.startedRevision <= previous.time.revision
                    || episode.startedSequence <= previous.time.sequence))
                    return Fail(out error, "Dialogue episodes must retain their nonoverlapping chronology.");
                if (!Episode(s, episode, pending, ticks, logs, evidenceIds, out error)) return false;
                if (Cycle(s, episode).status == StorylineStatus.Active) activeCount++;
                previous = episode;
            }
            if (activeCount > 1) return Fail(out error, "Only one dialogue may be active.");
            if (s.houseEvents.Count + ReservedEvents(s) > MaximumEvents)
                return Fail(out error, "The active dialogue's remaining event capacity is reserved.");
            return true;
        }

        private static bool Episode(EpisodeState s, DialogueEpisodeState e, EpisodeEngine.PendingDialogueCommand pending,
            HashSet<string> ticks, HashSet<int> logs, HashSet<string> evidenceIds, out string error)
        {
            error = null;
            var cycle = Cycle(s, e);
            var template = CampaignTemplate(cycle?.templateId, e.contentVersion);
            if (template == null || e.occasionKind != DialogueCampaignV1.Kind
                || e.week < 1 || e.week > s.week || e.window != Windows.AfterVeto || e.startedPhase != EpisodePhase.Campaign
                || e.startedSequence < 1 || e.startedSequence >= s.nextSequence
                || e.id != "story-" + e.startedSequence.ToString(CultureInfo.InvariantCulture)
                || string.IsNullOrEmpty(e.npcId) || e.npcId.Length > 160 || e.npcId == s.playerId
                || s.Find(e.npcId) == null || e.occasionId != Occasion(e.week, e.npcId)
                || !Revision(s, e.startedRevision, e, DialogueRevisionField.Started, pending))
                return Fail(out error, "Invalid campaign episode identity or start.");
            if (cycle == null || cycle.variant != 0
                || cycle.week != e.week || cycle.title != template.title || cycle.category != template.eyebrow
                || cycle.lane != StoryLanes.Game || !Cast(cycle.cast, e.npcId) || cycle.vars == null || cycle.vars.Count != 0
                || cycle.nextWeek != 0 || cycle.nextAnchor != null || cycle.path == null || cycle.path.Count < 1 || cycle.path.Count > MaximumSteps)
                return Fail(out error, "The dialogue must own its exact campaign cycle.");
            if (e.entry == null || e.entry.kind != DialogueCampaignV1.EntryKind || e.entry.grantId != null
                || e.entry.revision != e.startedRevision || !Revision(s, e.entry.revision, e.entry, DialogueRevisionField.Entry, pending)
                || e.entry.windowActionBefore < 0 || e.entry.windowActionAfter > MaximumAction
                || (long)e.entry.windowActionAfter != (long)e.entry.windowActionBefore + 1
                || e.week == s.week && e.entry.windowActionAfter > s.windowActions[e.window]
                || !ticks.Add(e.week + ":" + e.window + ":" + e.entry.windowActionAfter))
                return Fail(out error, "Invalid ordinary dialogue entry receipt.");
            var earlier = s.dialogue.episodes.TakeWhile(row => !ReferenceEquals(row, e))
                .LastOrDefault(row => row.week == e.week && row.window == e.window);
            if (earlier != null && e.entry.windowActionBefore < earlier.entry.windowActionAfter)
                return Fail(out error, "Dialogue entry ticks must retain their spending order.");
            if (e.responses == null || e.responses.Count > MaximumSteps || e.responses.Any(r => r == null)
                || e.evidence == null || e.evidence.Count > (e.contentVersion == DialogueCampaignV2.ContentVersion ? 1 : 0)
                || e.evidence.Any(row => row == null) || e.statement != null)
                return Fail(out error, "Unsupported campaign response or evidence storage.");
            var items = s.houseEvents.Where(item => item.cycleId == e.id).ToArray();
            if (items.Length < 2 || items.Length > MaximumSteps || items[0].id != e.entryEventId)
                return Fail(out error, "The campaign event shelf does not match its entry.");
            if (e.contentVersion == DialogueCampaignV2.ContentVersion && PublicRescueSource(s, e.npcId, e.week) == null)
                return Fail(out error, "The public-record campaign requires its retained completed rescue.");
            string beatId = DialogueCampaignV1.Open, ending = null;
            int previousSequence = e.startedSequence, previousRevision = e.startedRevision - 1, responseIndex = 0;
            for (int index = 0; index < items.Length; index++)
            {
                var item = items[index]; var beat = template.Beat(beatId);
                if (beat == null || !Event(item, beat, e, template.id, out int eventSequence) || eventSequence <= previousSequence || eventSequence >= s.nextSequence)
                    return Fail(out error, "A retained campaign event differs from its canonical definition.");
                previousSequence = eventSequence;
                if (index >= cycle.path.Count)
                {
                    if (index != items.Length - 1 || item.resolved || item.chosenIndex != -1 || item.outcome != null)
                        return Fail(out error, "Only the current campaign node may remain unanswered.");
                    break;
                }
                var step = cycle.path[index];
                if (step == null || step.beatId != beatId || step.week != e.week || !item.resolved
                    || item.chosenIndex < 0 || item.chosenIndex >= item.choices.Count
                    || step.optionId != item.choices[item.chosenIndex].optionId
                    || step.logSequence <= eventSequence || step.logSequence >= s.nextSequence || !logs.Add(step.logSequence)
                    || s.storylines.Any(other => other.id != e.id && other.path != null && other.path.Any(p => p != null && p.logSequence == step.logSequence)))
                    return Fail(out error, "A campaign step does not own its resolved event and outcome.");
                var option = beat.Option(step.optionId);
                bool lapsed = step.result == StoryResults.Lapsed;
                if (index == 0 && (step.optionId != DialogueCampaignV1.OpenCase || step.result != StoryResults.Plain)
                    || step.result != StoryResults.Plain && !lapsed
                    || lapsed && (index != cycle.path.Count - 1 || step.optionId != DialogueCampaignV1.Leave)
                    || item.outcome != (lapsed ? DialogueCampaignV1.LapsedOutcome : option.outcome))
                    return Fail(out error, "The campaign path must follow its exact authored edges.");
                previousSequence = step.logSequence;
                string suffix = Response(step.optionId, lapsed);
                if (suffix == "vote")
                {
                    var response = e.responses.Count > responseIndex ? e.responses[responseIndex] : null;
                    suffix = response?.responseKey?.Substring(Math.Min(response.responseKey.Length, template.id.Length + 1));
                    if (!VoteResponse(s, e, cycle, suffix)) return Fail(out error, e.contentVersion == DialogueCampaignV1.ContentVersion
                        ? "An answered campaign must retain its actual Emotional vote response."
                        : "An answered campaign must retain its actual authored vote response.");
                }
                bool omitted = lapsed && responseIndex == e.responses.Count;
                if (!omitted)
                {
                    if (e.responses.Count <= responseIndex) return Fail(out error, "A resolved campaign step is missing its response.");
                    var response = e.responses[responseIndex++];
                    if (response.eventId != item.id || response.pathIndex != index || response.speakerId != e.npcId
                        || response.responseKey != template.id + "/" + suffix
                        || !Revision(s, response.revision, response, DialogueRevisionField.Response, pending)
                        || response.revision <= previousRevision || index == 0 && response.revision != e.entry.revision)
                        return Fail(out error, "A campaign response does not match its accepted step.");
                    previousRevision = response.revision;
                }
                var retainedLog = s.events.FirstOrDefault(row => row.sequence == step.logSequence);
                if (retainedLog != null && (retainedLog.kind != StoryLog.Outcome || retainedLog.week != e.week
                    || retainedLog.phase != EpisodePhase.Campaign || retainedLog.audienceIds == null
                    || !retainedLog.audienceIds.SequenceEqual(new[] { s.playerId, e.npcId })))
                    return Fail(out error, "A retained campaign outcome has the wrong owner or audience.");
                ending = lapsed ? "lapsed" : option.next.StartsWith("end:", StringComparison.Ordinal) ? option.next.Substring(4) : null;
                if (ending != null)
                {
                    if (index != cycle.path.Count - 1 || index != items.Length - 1)
                        return Fail(out error, "A terminal campaign cannot contain another node.");
                    break;
                }
                beatId = option.next;
            }
            if (responseIndex != e.responses.Count || cycle.eventId != items[items.Length - 1].id || cycle.beatId != beatId)
                return Fail(out error, "The campaign path, current node and responses must agree.");
            if (!Evidence(s, e, cycle, items, pending, evidenceIds, out error)) return false;
            if (ending == null)
            {
                if (cycle.status != StorylineStatus.Active || cycle.endedWeek != 0 || cycle.endingId != null
                    || e.time != null || items.Length != cycle.path.Count + 1)
                    return Fail(out error, "An active campaign must retain its funded unanswered node.");
                if (e.week != s.week) return Fail(out error, "An active campaign cannot survive its week.");
                if (!Eligible(s, e.npcId, out error)) return false;
            }
            else
            {
                if (cycle.status != StorylineStatus.Completed || cycle.endedWeek != e.week || cycle.endingId != ending
                    || e.time == null || e.time.reason != (ending == "lapsed" ? "occasion-closed" : ending)
                    || e.time.window != Windows.AfterVeto || e.time.proposedDelta != 0 || e.time.appliedDelta != 0
                    || e.time.spentRevision != 0 || e.time.spentSequence != 0 || e.time.spentWindowAction != 0
                    || !Revision(s, e.time.revision, e.time, DialogueRevisionField.Time, pending)
                    || e.time.sequence != cycle.path[cycle.path.Count - 1].logSequence)
                    return Fail(out error, "A closed campaign requires its exact zero-time receipt.");
                bool omitted = responseIndex != cycle.path.Count;
                bool currentClosure = e.time.revision == s.revision || e.time.revision > s.revision;
                bool present = s.Find(e.npcId).status == ContestantStatus.Active;
                if (omitted && (ending != "lapsed" || e.time.revision <= previousRevision || present)
                    || !omitted && (e.time.revision != previousRevision || ending == "lapsed" && currentClosure && !present))
                    return Fail(out error, "The campaign closure and its actual speaker response disagree.");
                if (ending == "lapsed" && currentClosure && e.week == s.week && s.phase == EpisodePhase.Campaign
                    && Eligible(s, e.npcId, out _) && pending?.OwnsCampaignClosure(s, e) != true)
                    return Fail(out error, "The current campaign occasion has not closed.");
            }
            return true;
        }

        private static bool Event(HouseEventState item, BeatTemplate beat, DialogueEpisodeState episode, string templateId, out int sequence)
        {
            sequence = 0;
            if (item.id == null || !item.id.StartsWith("house-event-", StringComparison.Ordinal)
                || !int.TryParse(item.id.Substring(12), NumberStyles.None, CultureInfo.InvariantCulture, out sequence)
                || sequence < 1 || item.id != "house-event-" + sequence.ToString(CultureInfo.InvariantCulture)
                || item.kind != HouseEventKind.Story || item.week != episode.week || item.cycleId != episode.id
                || item.contentId != templateId + ":" + beat.id || item.title != beat.title || item.narrative != beat.summary
                || item.closesAnchor != beat.closes || item.surface != beat.surface || item.venue != beat.venue || item.lapseOptionId != beat.lapse
                || !Cast(item.cast, episode.npcId) || item.involvedIds == null || !item.involvedIds.SequenceEqual(new[] { episode.npcId })
                || item.choices == null || item.choices.Count != beat.options.Length) return false;
            for (int index = 0; index < beat.options.Length; index++)
            {
                var c = item.choices[index]; var o = beat.options[index];
                if (c == null || c.optionId != o.id || c.label != o.label || c.description != o.description || c.risk != HouseEventRisk.Low
                    || c.glyph != null || c.approach != null || c.checkBase != -1 || c.subjectId != null || c.lapse != (o.id == beat.lapse)
                    || c.costsAction || c.conduct || c.pickPerson || c.locked || c.lockReason != null || c.trustChange != 0
                    || c.next != o.next || c.nextOnBackfire != null || c.impacts == null || c.impacts.Count != 0
                    || c.eligibleIds == null || c.eligibleIds.Count != 0 || c.bonusTraits == null || c.bonusTraits.Count != 0
                    || c.against == null || c.against.Count != 0 || c.effects == null || c.effects.Count != 0 || c.backfire == null || c.backfire.Count != 0) return false;
            }
            return true;
        }

        private static string Response(string option, bool lapsed) => lapsed ? "occasion-closed"
            : option == DialogueCampaignV1.OpenCase ? "hear-your-case" : option == DialogueCampaignV1.Appeal ? "hear-personal-case"
            : option == DialogueCampaignV2.Cite ? "explain-that-record"
            : option == DialogueCampaignV1.Plea ? "vote" : option == DialogueCampaignV1.KeepOpen ? "no-promise-made" : "respectful-exit";

        private static bool VoteResponse(EpisodeState s, DialogueEpisodeState e, StorylineState cycle, string suffix)
        {
            string response = suffix == "vote-" + LobbyResponse.Receptive ? LobbyResponse.Receptive
                : suffix == "vote-" + LobbyResponse.Open ? LobbyResponse.Open
                : suffix == "vote-" + LobbyResponse.Skeptical ? LobbyResponse.Skeptical
                : suffix == "vote-" + LobbyResponse.Hostile ? LobbyResponse.Hostile : null;
            if (response == null) return false;
            var rows = s.lobbies.Where(l => l.week == e.week && l.phase == EpisodePhase.Campaign && l.deciderId == e.npcId).ToArray();
            string approach = e.contentVersion == DialogueCampaignV2.ContentVersion
                && cycle.path.Any(step => step.beatId == DialogueCampaignV2.Case && step.optionId == DialogueCampaignV2.Cite)
                ? LobbyApproach.Strategic : LobbyApproach.Emotional;
            return (e.week < s.week && rows.Length == 0) || rows.Length == 1 && rows[0].ask == LobbyAsk.Vote
                && rows[0].subjectId == s.playerId && rows[0].approach == approach && rows[0].response == response;
        }


        internal static bool IsCampaignTemplate(string id) =>
            id == DialogueCampaignV1.TemplateId || id == DialogueCampaignV2.TemplateId;

        private static bool IsCampaignContent(string id) => id != null
            && (id.StartsWith(DialogueCampaignV1.TemplateId + ":", StringComparison.Ordinal)
                || id.StartsWith(DialogueCampaignV2.TemplateId + ":", StringComparison.Ordinal));

        // Deliberately closed pairs: neither a mutable catalog nor an unknown future version.
        internal static ArcTemplate CampaignTemplate(string id, int version) =>
            id == DialogueCampaignV1.TemplateId && version == DialogueCampaignV1.ContentVersion ? DialogueCampaignV1.CreateTemplate()
            : id == DialogueCampaignV2.TemplateId && version == DialogueCampaignV2.ContentVersion ? DialogueCampaignV2.CreateTemplate() : null;

        // Callers admit the complete source once. This local selector never calls EpisodeValidation
        // (including from this validator) and never treats an incomplete/final-three owner as rescue.
        private static PowerRow PublicRescueSource(EpisodeState s, string npcId, int beforeWeek)
        {
            if (!On(s) || beforeWeek < 1 || beforeWeek > s.week || beforeWeek > 100
                || s.Find(npcId) == null || npcId == s.playerId || s.ledger?.power == null
                || s.ledger.power.Count > SeasonLedger.MostRows || s.ledger.power.Any(row => row == null)
                || s.ledger.power.Select(row => row.week).Distinct().Count() != s.ledger.power.Count
                || s.unifiedVoteReveals == null || s.unifiedVoteReveals.Count > UnifiedVoteRevealArchive.MaximumFrames)
                return null;
            var selected = s.ledger.power.Where(row => row.week >= 1 && row.week < beforeWeek
                && row.vetoUsed && row.vetoHolderId == npcId && row.savedId == s.playerId)
                .OrderByDescending(row => row.week).FirstOrDefault();
            if (selected == null) return null;
            var frames = s.unifiedVoteReveals.Where(frame => frame != null && frame.week == selected.week).Take(2).ToArray();
            return frames.Length == 1 && UnifiedVoteCompletedReveal.TryValidate(s, frames[0], out _) ? selected : null;
        }

        internal static string EntryTemplateId(EpisodeState admittedSource, string npcId) =>
            PublicRescueSource(admittedSource, npcId, admittedSource.week) == null
                ? DialogueCampaignV1.TemplateId : DialogueCampaignV2.TemplateId;

        internal static PowerRow CapturePublicRescue(EpisodeState admittedSource, string npcId, int episodeWeek) =>
            PublicRescueSource(admittedSource, npcId, episodeWeek)?.Clone();

        /// <summary>Detached allowlisted public fact, not a PowerRow, ballot or authority token.</summary>
        public sealed class PublicRescueView
        {
            public int Week { get; }
            public string HolderId { get; }
            public string SavedId { get; }
            public string ReplacementId { get; }
            internal PublicRescueView(PowerRow row)
            { Week = row.week; HolderId = row.vetoHolderId; SavedId = row.savedId; ReplacementId = row.replacementId; }
        }

        public static bool TryReadPublicRescue(EpisodeState s, string eventId, out PublicRescueView view)
        {
            view = null;
            if (!On(s) || !EpisodeValidation.TryValidate(s, out _)) return false;
            var episode = Active(s);
            var cycle = episode == null ? null : Cycle(s, episode);
            if (episode == null || CampaignTemplate(cycle?.templateId, episode.contentVersion)?.id != DialogueCampaignV2.TemplateId
                || cycle.eventId != eventId) return false;
            var selected = PublicRescueSource(s, episode.npcId, episode.week);
            if (selected == null) return false;
            view = new PublicRescueView(selected);
            return true;
        }

        private static bool SamePower(PowerRow a, PowerRow b) => a != null && b != null
            && a.week == b.week && a.hohId == b.hohId && a.vetoHolderId == b.vetoHolderId && a.vetoUsed == b.vetoUsed
            && a.savedId == b.savedId && a.replacementId == b.replacementId && a.evicteeId == b.evicteeId
            && a.backdoorTargetId == b.backdoorTargetId && a.backdoorResult == b.backdoorResult
            && a.nominees != null && b.nominees != null && a.nominees.SequenceEqual(b.nominees)
            && a.tally != null && b.tally != null && a.tally.SequenceEqual(b.tally);

        private static bool Evidence(EpisodeState s, DialogueEpisodeState episode, StorylineState cycle, HouseEventState[] items,
            EpisodeEngine.PendingDialogueCommand pending, HashSet<string> ids, out string error)
        {
            error = null;
            bool cited = episode.contentVersion == DialogueCampaignV2.ContentVersion && cycle.path.Count > 1
                && cycle.path[1].beatId == DialogueCampaignV2.Case && cycle.path[1].optionId == DialogueCampaignV2.Cite;
            if (!cited) return episode.evidence.Count == 0 || Fail(out error, "Only the accepted public-record path may retain evidence.");
            if (episode.evidence.Count != 1) return Fail(out error, "A citation requires exactly one source-owned evidence row.");
            var row = episode.evidence[0];
            var response = episode.responses.FirstOrDefault(r => r.pathIndex == 1);
            int sequence = cycle.path[1].logSequence - 1;
            if (row.kind != "power" || row.memory != null || row.statementId != null || row.pathIndex != 1
                || row.eventId != items[1].id || response == null || row.capturedRevision != response.revision
                || response.responseKey != DialogueCampaignV2.TemplateId + "/explain-that-record"
                || !Revision(s, row.capturedRevision, row, DialogueRevisionField.EvidenceCaptured, pending)
                || sequence <= 0 || row.id != "dialogue-evidence-" + sequence.ToString(CultureInfo.InvariantCulture)
                || !ids.Add(row.id) || !int.TryParse(items[1].id.Substring(12), out int eventSequence) || sequence <= eventSequence
                || s.events.Any(e => e.sequence == sequence)
                || s.houseEvents.Any(e => e.id == "house-event-" + sequence.ToString(CultureInfo.InvariantCulture))
                || s.storylines.Any(c => c.id == "story-" + sequence.ToString(CultureInfo.InvariantCulture))
                || !SamePower(row.power, PublicRescueSource(s, episode.npcId, episode.week)))
                return Fail(out error, "The citation must retain its exact owned revision, event, identity and complete public-rescue source.");
            return true;
        }

        private static bool Revision(EpisodeState s, int revision, object row, DialogueRevisionField field, EpisodeEngine.PendingDialogueCommand pending) =>
            revision > 0 && (revision <= s.revision || (long)revision == (long)s.revision + 1 && pending != null && pending.Allows(s, row, field));

        private static bool Cast(List<StoryRoleState> cast, string npcId) => cast != null && cast.Count == 1
            && cast[0] != null && cast[0].role == DialogueCampaignV1.SpeakerRole && cast[0].contestantId == npcId;

        private static bool Eligible(EpisodeState s, string npcId, out string error)
        {
            if (s.phase != EpisodePhase.Campaign || s.evictionResolved || s.Find(s.playerId)?.status != ContestantStatus.Active
                || s.nominees == null || !s.nominees.Contains(s.playerId) || s.Find(npcId)?.status != ContestantStatus.Active || npcId == s.playerId)
                return Fail(out error, "The campaign occasion is no longer playable.");
            return StrategyRules.CanLobby(s, npcId, LobbyAsk.Vote, s.playerId, out error);
        }

        // Threads or any later story milestone: enrolling one (ROLE-MOMENTS-PLAN R7) must not switch the pilot off.
        private static bool Prerequisites(EpisodeState s) => s.unifiedCommitmentRulesVersion == 2 && s.unifiedHearingRulesVersion == 1
            && s.economyRulesVersion == 1 && s.story != null && s.story.rulesVersion >= StoryRules.Threads
            && s.readRulesStartWeek == 1 && s.leverRulesStartWeek == 1 && s.weekRulesStartWeek == 1
            && s.strategyRulesStartWeek == 1 && s.commitmentRulesStartWeek == 1 && s.agencyRulesStartWeek == 1
            && s.allWeekRulesStartWeek == 1 && s.dealRulesStartWeek == 1 && s.storyRulesStartWeek == 1
            && s.story.rulesStartWeek == 1 && s.npcSocial != null && s.npcSocial.rulesStartWeek == 1;

        private static bool Fail(out string error, string reason) { error = reason; return false; }
    }
}
