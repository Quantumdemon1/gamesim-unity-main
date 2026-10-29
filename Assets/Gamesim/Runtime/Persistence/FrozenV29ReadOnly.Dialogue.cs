// Frozen29 (SCH30): schema 29's dialogue domain, the one domain Frozen28 never saw. A frozen copy of b208d46d's
// DialogueRules.TryValidate and every member it reads (DialogueCampaignV1 and V2's closed content, the template
// fields validation compares, the campaign lobby test), over Frozen29's data-only snapshot. No pending command
// exists for a saved payload, so every pending branch is its null case, written out (ALL-STAR-HISTORIES-PLAN 8.2):
// dialogue rules 0 (the empty store) and 1 (the pilots' closed V1 and V2 pairs) and nothing else.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Persistence.Frozen29Data;

namespace Gamesim.Persistence.Frozen29ReadOnly
{
    internal static class DialogueRules
    {
        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.Version,MaximumEpisodes,MaximumEvents,MaximumSteps
        public const int Version = 1, MaximumEpisodes = 100, MaximumEvents = 400, MaximumSteps = 3;

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.MaximumAction
        private const int MaximumAction = EpisodeEngine.LegacySocialActionBudget + WebSocialVocabulary.PurchaseCeiling;

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.On: schema 29 exactly, as the live rule read it at 29.
        public static bool On(EpisodeState s) => s != null && s.schemaVersion == 29 && s.dialogueRulesVersion == Version;

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.Cycle
        public static StorylineState Cycle(EpisodeState s, DialogueEpisodeState episode)
        {
            var matches = s?.storylines?.Where(c => c != null && c.id == episode?.id).Take(2).ToArray();
            return matches != null && matches.Length == 1 ? matches[0] : null;
        }

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.Active
        public static DialogueEpisodeState Active(EpisodeState s)
        {
            var matches = s?.dialogue?.episodes?.Where(e => e != null && Cycle(s, e)?.status == StorylineStatus.Active).Take(2).ToArray();
            return matches != null && matches.Length == 1 ? matches[0] : null;
        }

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.ReservedEvents
        public static int ReservedEvents(EpisodeState s)
        {
            var active = Active(s);
            return active == null || s.houseEvents == null ? 0
                : Math.Max(0, MaximumSteps - s.houseEvents.Count(e => e != null && e.cycleId == active.id));
        }

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.Occasion
        public static string Occasion(int week, string npcId) => "week:" + week.ToString(CultureInfo.InvariantCulture) + ":campaign:" + npcId;

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.TryValidate(EpisodeState, PendingDialogueCommand, out string),
        // with no pending command.
        public static bool TryValidate(EpisodeState s, out string error)
        {
            error = null;
            if (s == null || s.schemaVersion != 29) return Fail(out error, "Unsupported dialogue schema.");
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
                if (!Episode(s, episode, ticks, logs, evidenceIds, out error)) return false;
                if (Cycle(s, episode).status == StorylineStatus.Active) activeCount++;
                previous = episode;
            }
            if (activeCount > 1) return Fail(out error, "Only one dialogue may be active.");
            if (s.houseEvents.Count + ReservedEvents(s) > MaximumEvents)
                return Fail(out error, "The active dialogue's remaining event capacity is reserved.");
            return true;
        }

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.Episode, with no pending command.
        private static bool Episode(EpisodeState s, DialogueEpisodeState e,
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
                || !Revision(s, e.startedRevision))
                return Fail(out error, "Invalid campaign episode identity or start.");
            if (cycle == null || cycle.variant != 0
                || cycle.week != e.week || cycle.title != template.title || cycle.category != template.eyebrow
                || cycle.lane != StoryLanes.Game || !Cast(cycle.cast, e.npcId) || cycle.vars == null || cycle.vars.Count != 0
                || cycle.nextWeek != 0 || cycle.nextAnchor != null || cycle.path == null || cycle.path.Count < 1 || cycle.path.Count > MaximumSteps)
                return Fail(out error, "The dialogue must own its exact campaign cycle.");
            if (e.entry == null || e.entry.kind != DialogueCampaignV1.EntryKind || e.entry.grantId != null
                || e.entry.revision != e.startedRevision || !Revision(s, e.entry.revision)
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
                        || !Revision(s, response.revision)
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
            if (!Evidence(s, e, cycle, items, evidenceIds, out error)) return false;
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
                    || !Revision(s, e.time.revision)
                    || e.time.sequence != cycle.path[cycle.path.Count - 1].logSequence)
                    return Fail(out error, "A closed campaign requires its exact zero-time receipt.");
                bool omitted = responseIndex != cycle.path.Count;
                bool currentClosure = e.time.revision == s.revision || e.time.revision > s.revision;
                bool present = s.Find(e.npcId).status == ContestantStatus.Active;
                if (omitted && (ending != "lapsed" || e.time.revision <= previousRevision || present)
                    || !omitted && (e.time.revision != previousRevision || ending == "lapsed" && currentClosure && !present))
                    return Fail(out error, "The campaign closure and its actual speaker response disagree.");
                // The live test also asks a pending command whether it owns this closure; a saved payload has none.
                if (ending == "lapsed" && currentClosure && e.week == s.week && s.phase == EpisodePhase.Campaign
                    && Eligible(s, e.npcId, out _))
                    return Fail(out error, "The current campaign occasion has not closed.");
            }
            return true;
        }

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.Event
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

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.Response
        private static string Response(string option, bool lapsed) => lapsed ? "occasion-closed"
            : option == DialogueCampaignV1.OpenCase ? "hear-your-case" : option == DialogueCampaignV1.Appeal ? "hear-personal-case"
            : option == DialogueCampaignV2.Cite ? "explain-that-record"
            : option == DialogueCampaignV1.Plea ? "vote" : option == DialogueCampaignV1.KeepOpen ? "no-promise-made" : "respectful-exit";

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.VoteResponse
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

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.IsCampaignTemplate
        internal static bool IsCampaignTemplate(string id) =>
            id == DialogueCampaignV1.TemplateId || id == DialogueCampaignV2.TemplateId;

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.IsCampaignContent
        private static bool IsCampaignContent(string id) => id != null
            && (id.StartsWith(DialogueCampaignV1.TemplateId + ":", StringComparison.Ordinal)
                || id.StartsWith(DialogueCampaignV2.TemplateId + ":", StringComparison.Ordinal));

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.CampaignTemplate: deliberately closed pairs.
        internal static ArcTemplate CampaignTemplate(string id, int version) =>
            id == DialogueCampaignV1.TemplateId && version == DialogueCampaignV1.ContentVersion ? DialogueCampaignV1.CreateTemplate()
            : id == DialogueCampaignV2.TemplateId && version == DialogueCampaignV2.ContentVersion ? DialogueCampaignV2.CreateTemplate() : null;

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.PublicRescueSource
        private static PowerRow PublicRescueSource(EpisodeState s, string npcId, int beforeWeek)
        {
            if (!On(s) || beforeWeek < 1 || beforeWeek > s.week || beforeWeek > 100
                || s.Find(npcId) == null || npcId == s.playerId || s.ledger?.power == null
                || s.ledger.power.Count > LedgerLimits.MostRows || s.ledger.power.Any(row => row == null)
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

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.SamePower
        private static bool SamePower(PowerRow a, PowerRow b) => a != null && b != null
            && a.week == b.week && a.hohId == b.hohId && a.vetoHolderId == b.vetoHolderId && a.vetoUsed == b.vetoUsed
            && a.savedId == b.savedId && a.replacementId == b.replacementId && a.evicteeId == b.evicteeId
            && a.backdoorTargetId == b.backdoorTargetId && a.backdoorResult == b.backdoorResult
            && a.nominees != null && b.nominees != null && a.nominees.SequenceEqual(b.nominees)
            && a.tally != null && b.tally != null && a.tally.SequenceEqual(b.tally);

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.Evidence, with no pending command.
        private static bool Evidence(EpisodeState s, DialogueEpisodeState episode, StorylineState cycle, HouseEventState[] items,
            HashSet<string> ids, out string error)
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
                || !Revision(s, row.capturedRevision)
                || sequence <= 0 || row.id != "dialogue-evidence-" + sequence.ToString(CultureInfo.InvariantCulture)
                || !ids.Add(row.id) || !int.TryParse(items[1].id.Substring(12), out int eventSequence) || sequence <= eventSequence
                || s.events.Any(e => e.sequence == sequence)
                || s.houseEvents.Any(e => e.id == "house-event-" + sequence.ToString(CultureInfo.InvariantCulture))
                || s.storylines.Any(c => c.id == "story-" + sequence.ToString(CultureInfo.InvariantCulture))
                || !SamePower(row.power, PublicRescueSource(s, episode.npcId, episode.week)))
                return Fail(out error, "The citation must retain its exact owned revision, event, identity and complete public-rescue source.");
            return true;
        }

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.Revision, with no pending command: the next revision is
        // admitted only for a pending command's own sealed row, so a saved row's revision is at most the state's.
        private static bool Revision(EpisodeState s, int revision) => revision > 0 && revision <= s.revision;

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.Cast
        private static bool Cast(List<StoryRoleState> cast, string npcId) => cast != null && cast.Count == 1
            && cast[0] != null && cast[0].role == DialogueCampaignV1.SpeakerRole && cast[0].contestantId == npcId;

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.Eligible
        private static bool Eligible(EpisodeState s, string npcId, out string error)
        {
            if (s.phase != EpisodePhase.Campaign || s.evictionResolved || s.Find(s.playerId)?.status != ContestantStatus.Active
                || s.nominees == null || !s.nominees.Contains(s.playerId) || s.Find(npcId)?.status != ContestantStatus.Active || npcId == s.playerId)
                return Fail(out error, "The campaign occasion is no longer playable.");
            return CampaignLobby.CanLobbyVote(s, npcId, s.playerId, out error);
        }

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.Prerequisites
        private static bool Prerequisites(EpisodeState s) => s.unifiedCommitmentRulesVersion == 2 && s.unifiedHearingRulesVersion == 1
            && s.economyRulesVersion == 1 && s.story != null && s.story.rulesVersion >= StoryRules.Threads
            && s.readRulesStartWeek == 1 && s.leverRulesStartWeek == 1 && s.weekRulesStartWeek == 1
            && s.strategyRulesStartWeek == 1 && s.commitmentRulesStartWeek == 1 && s.agencyRulesStartWeek == 1
            && s.allWeekRulesStartWeek == 1 && s.dealRulesStartWeek == 1 && s.storyRulesStartWeek == 1
            && s.story.rulesStartWeek == 1 && s.npcSocial != null && s.npcSocial.rulesStartWeek == 1;

        // SOURCE b208d46d Gamesim.Simulation.DialogueRules.Fail
        private static bool Fail(out string error, string reason) { error = reason; return false; }
    }

    // SOURCE b208d46d Gamesim.Simulation.StrategyRules.CanLobby for the one ask the dialogue validator makes:
    // CanLobby(s, npcId, LobbyAsk.Vote, s.playerId). Eligible calls it only in the Campaign phase, where
    // StrategyRules.Deciders is empty (it names a decider only in Nomination and VetoMeeting), so IsDecider is false
    // and the live test reduces to the vote ask's own: CanBeAskedForTheirVote, AlreadyAsked, the subject and the case.
    internal static class CampaignLobby
    {
        internal static bool CanLobbyVote(EpisodeState s, string deciderId, string subjectId, out string reason)
        {
            reason = null;
            if (s.phase != EpisodePhase.Campaign) return Refuse(out reason, "Nobody is deciding anything you could change right now.");
            if (!CanBeAskedForTheirVote(s, deciderId))
                return Refuse(out reason, "Nobody is deciding anything you could change right now.");
            if (AlreadyAsked(s, deciderId)) return Refuse(out reason, s.Find(deciderId).name + " has already heard you out.");
            var subject = subjectId == null ? null : s.Find(subjectId);
            if (subjectId != null && (subject == null || subject.status != ContestantStatus.Active))
                return Refuse(out reason, "Name somebody still in the house.");
            if (!CanBeAskedForTheirVote(s, deciderId)) return Refuse(out reason, "Only a voter can be asked to keep you, and only while you are on the block.");
            if (subjectId != s.playerId) return Refuse(out reason, "A plea for your vote is for yourself.");
            return true;
        }

        // SOURCE b208d46d Gamesim.Simulation.StrategyRules.CanBeAskedForTheirVote
        private static bool CanBeAskedForTheirVote(EpisodeState s, string voterId) =>
            s != null && EpisodeEngine.LeverRulesOn(s) && StrategyRules.Apply(s) && s.phase == EpisodePhase.Campaign && !s.evictionResolved
            && s.nominees.Contains(s.playerId) && s.Find(s.playerId)?.status == ContestantStatus.Active
            && Npc(s, voterId) && EpisodeEngine.Voters(s).Any(v => v.id == voterId);

        // SOURCE b208d46d Gamesim.Simulation.StrategyRules.AlreadyAsked
        private static bool AlreadyAsked(EpisodeState s, string deciderId) =>
            s.lobbies.Any(l => l.week == s.week && l.phase == s.phase && l.deciderId == deciderId);

        // SOURCE b208d46d Gamesim.Simulation.StrategyRules.Npc
        private static bool Npc(EpisodeState s, string id)
        {
            var who = s.Find(id);
            return who != null && !who.isPlayer && who.status == ContestantStatus.Active;
        }

        // SOURCE b208d46d Gamesim.Simulation.StrategyRules.Refuse
        private static bool Refuse(out string reason, string text) { reason = text; return false; }
    }

    // SOURCE b208d46d Gamesim.Simulation.StoryLog.Outcome (Story/StoryText.cs)
    internal static class StoryLog
    {
        public const string Outcome = "story-outcome";
    }

    // SOURCE b208d46d Gamesim.Simulation.StoryVenues.Living (Story/StoryState.cs)
    internal static class StoryVenues
    {
        public const string Living = "living-room";
    }

    // SOURCE b208d46d Gamesim.Simulation.ArcTemplate, BeatTemplate and OptionTemplate (Story/StoryTemplates.cs): only
    // the fields and lookups the dialogue validator reads. The closed content below fills exactly these.
    internal sealed class ArcTemplate
    {
        public string id, eyebrow, title;
        public BeatTemplate[] beats;
        public BeatTemplate Beat(string beatId) => beats.FirstOrDefault(b => b.id == beatId);
    }

    internal sealed class BeatTemplate
    {
        public string id, title, summary, closes, surface, venue, lapse;
        public OptionTemplate[] options;
        public OptionTemplate Option(string optionId) => options.FirstOrDefault(o => o.id == optionId);
    }

    internal sealed class OptionTemplate
    {
        public string id, label, description, outcome, next;
    }

    // SOURCE b208d46d Gamesim.Simulation.DialogueCampaignV1 (Story/DialogueCampaignV1.cs): the closed personal appeal.
    internal static class DialogueCampaignV1
    {
        public const string TemplateId = "dialogue-campaign-v1", Kind = "campaign", EntryKind = "action", SpeakerRole = "speaker";
        public const int ContentVersion = 1;
        public const string Title = "Make your case", Category = "Campaign";
        public const string Open = "open", Case = "case", Finish = "finish-personal";
        public const string OpenCase = "open-case", Appeal = "appeal-personally", Plea = "make-plea", Leave = "leave", KeepOpen = "keep-vote-open";
        public const string OpenTitle = "Make your case", CaseTitle = "A personal appeal", FinishTitle = "Ask for their vote";
        public const string OpenSummary = "There is time to make a case.", CaseSummary = "The conversation becomes personal.", FinishSummary = "A vote is still a choice.";
        public const string OpenLabel = "Make your case", AppealLabel = "Appeal personally", PleaLabel = "Make the plea", LeaveLabel = "Leave it there", KeepLabel = "Keep their vote open";
        public const string OpenDescription = "Ask to be heard before the vote.", AppealDescription = "Explain what staying means to you.", PleaDescription = "Ask for a vote to keep you.";
        public const string LeaveDescription = "End the conversation respectfully.", KeepDescription = "Leave their vote unpromised.";
        public const string OpenOutcome = "You opened your case.", AppealOutcome = "You made a personal appeal.", PleaOutcome = "You asked for their vote.";
        public const string LeaveOutcome = "You left the conversation respectfully.", KeepOutcome = "You left their vote open.";
        public const string LapsedOutcome = "The conversation ended when the occasion closed.";

        internal static ArcTemplate CreateTemplate() => new ArcTemplate
        {
            id = TemplateId, eyebrow = Category, title = Title,
            beats = new[]
            {
                Beat(Open, OpenTitle, OpenSummary,
                    Option(OpenCase, OpenLabel, OpenDescription, OpenOutcome, Case), Exit()),
                Beat(Case, CaseTitle, CaseSummary,
                    Option(Appeal, AppealLabel, AppealDescription, AppealOutcome, Finish), Exit()),
                Beat(Finish, FinishTitle, FinishSummary,
                    Option(Plea, PleaLabel, PleaDescription, PleaOutcome, "end:answered"),
                    Option(KeepOpen, KeepLabel, KeepDescription, KeepOutcome, "end:neutral"), Exit())
            }
        };

        private static BeatTemplate Beat(string id, string title, string summary, params OptionTemplate[] options) => new BeatTemplate
        {
            id = id, title = title, summary = summary,
            closes = StoryAnchors.EvictionEve, surface = StorySurfaces.Conversation, venue = StoryVenues.Living,
            lapse = Leave, options = options
        };

        private static OptionTemplate Option(string id, string label, string description, string outcome, string next) => new OptionTemplate
        {
            id = id, label = label, description = description, outcome = outcome, next = next
        };

        private static OptionTemplate Exit() => Option(Leave, LeaveLabel, LeaveDescription, LeaveOutcome, "end:neutral");
    }

    // SOURCE b208d46d Gamesim.Simulation.DialogueCampaignV2 (Story/DialogueCampaignV2.cs): the closed public record.
    internal static class DialogueCampaignV2
    {
        public const string TemplateId = "dialogue-campaign-v2";
        public const int ContentVersion = 2;
        public const string Open = "open", Case = "case", FinishRecord = "finish-record", FinishPersonal = "finish-personal";
        public const string Cite = "cite-public-rescue";
        public const string CaseTitle = "On the public record", CaseSummary = "You can point to what actually happened.";
        public const string RecordTitle = "Ask from the record", RecordSummary = "A public rescue is a fact, not a promise.";
        public const string CiteLabel = "Cite their public rescue", CiteDescription = "Point to the most recent completed week when they used the veto to save you.";
        public const string CiteOutcome = "You cited the public rescue.";

        internal static ArcTemplate CreateTemplate() => new ArcTemplate
        {
            id = TemplateId, eyebrow = DialogueCampaignV1.Category, title = DialogueCampaignV1.Title,
            beats = new[]
            {
                Beat(Open, DialogueCampaignV1.OpenTitle, DialogueCampaignV1.OpenSummary,
                    Option(DialogueCampaignV1.OpenCase, DialogueCampaignV1.OpenLabel, DialogueCampaignV1.OpenDescription,
                        DialogueCampaignV1.OpenOutcome, Case), Exit()),
                Beat(Case, CaseTitle, CaseSummary,
                    Option(Cite, CiteLabel, CiteDescription, CiteOutcome, FinishRecord),
                    Option(DialogueCampaignV1.Appeal, DialogueCampaignV1.AppealLabel, DialogueCampaignV1.AppealDescription,
                        DialogueCampaignV1.AppealOutcome, FinishPersonal), Exit()),
                Beat(FinishRecord, RecordTitle, RecordSummary,
                    Option(DialogueCampaignV1.Plea, DialogueCampaignV1.PleaLabel, DialogueCampaignV1.PleaDescription,
                        DialogueCampaignV1.PleaOutcome, "end:answered"),
                    Option(DialogueCampaignV1.KeepOpen, DialogueCampaignV1.KeepLabel, DialogueCampaignV1.KeepDescription,
                        DialogueCampaignV1.KeepOutcome, "end:neutral"), Exit()),
                Beat(FinishPersonal, DialogueCampaignV1.FinishTitle, DialogueCampaignV1.FinishSummary,
                    Option(DialogueCampaignV1.Plea, DialogueCampaignV1.PleaLabel, DialogueCampaignV1.PleaDescription,
                        DialogueCampaignV1.PleaOutcome, "end:answered"),
                    Option(DialogueCampaignV1.KeepOpen, DialogueCampaignV1.KeepLabel, DialogueCampaignV1.KeepDescription,
                        DialogueCampaignV1.KeepOutcome, "end:neutral"), Exit())
            }
        };

        private static BeatTemplate Beat(string id, string title, string summary, params OptionTemplate[] options) => new BeatTemplate
        {
            id = id, title = title, summary = summary,
            closes = StoryAnchors.EvictionEve, surface = StorySurfaces.Conversation, venue = StoryVenues.Living,
            lapse = DialogueCampaignV1.Leave, options = options
        };

        private static OptionTemplate Option(string id, string label, string description, string outcome, string next) => new OptionTemplate
        { id = id, label = label, description = description, outcome = outcome, next = next };

        private static OptionTemplate Exit() => Option(DialogueCampaignV1.Leave, DialogueCampaignV1.LeaveLabel,
            DialogueCampaignV1.LeaveDescription, DialogueCampaignV1.LeaveOutcome, "end:neutral");
    }
}
