// Frozen30 (talk S0, schema 31's step 1): dialogue rules 2, the version schema 30 introduced (DIALOGUE-PLAN §2, slice
// D2). A frozen copy of 64d52f59's DialogueRules.Rules2.cs as far as validation reads it - the ladder's bounds, the
// closed content registry, entry funding and the exact validator of a rules-2 conversation - and of
// Story/DialogueCampaignWord.cs's closed tree, over Frozen30's data-only snapshot. No pending command exists for a
// saved payload, so every pending branch is its null case, written out (ALL-STAR-HISTORIES-PLAN 8.2).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Persistence.Frozen30Data;

namespace Gamesim.Persistence.Frozen30ReadOnly
{
    internal static partial class DialogueRules
    {
        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.Rules1,Rules2,StoreSchema,Rules2Schema,GrantFundingFrom
        public const int Rules1 = 1, Rules2 = 2;
        public const int StoreSchema = 29, Rules2Schema = 30;
        public const int GrantFundingFrom = 4;

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.ActionEntry,GrantEntry
        public const string ActionEntry = DialogueCampaignV1.EntryKind, GrantEntry = "grant";

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.Rules2Steps,Rules2Conversations,Rules2StorylineCeiling,Rules2EvidenceRows
        public const int Rules2Steps = 5, Rules2Conversations = 32, Rules2StorylineCeiling = 92, Rules2EvidenceRows = 3;

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.VoteClaim,VoteOut
        public const string VoteClaim = "vote-claim", VoteOut = "vote-out";

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.IsDefined
        public static bool IsDefined(int version) => version >= 0 && version <= Version;

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.SchemaFor
        public static int SchemaFor(int version) => version >= Rules2 ? Rules2Schema : StoreSchema;

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.Admits
        public static bool Admits(int version, int schema) => IsDefined(version) && schema >= SchemaFor(version);

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.StepsFor
        public static int StepsFor(int version) => !IsDefined(version) || version < Rules1 ? 0 : version == Rules1 ? MaximumSteps : Rules2Steps;

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.ConversationBudget
        public static int ConversationBudget(int version) =>
            !IsDefined(version) || version < Rules1 ? 0 : version == Rules1 ? MaximumEpisodes : Rules2Conversations;

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.EvidenceRows
        public static int EvidenceRows(int version) => !IsDefined(version) || version < Rules1 ? 0 : version == Rules1 ? 1 : Rules2EvidenceRows;

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.AcceptsFunding
        public static bool AcceptsFunding(int version, string kind) => IsDefined(version) && version >= Rules1
            && (kind == ActionEntry || kind == GrantEntry && version >= GrantFundingFrom);

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.EntryTick
        public static string EntryTick(DialogueEntryReceiptState entry, int week, int window) => entry?.kind == ActionEntry
            ? week.ToString(CultureInfo.InvariantCulture) + ":" + window.ToString(CultureInfo.InvariantCulture) + ":"
              + entry.windowActionAfter.ToString(CultureInfo.InvariantCulture)
            : null;

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.Rules2Template
        internal static ArcTemplate Rules2Template(string id, int version) =>
            id == DialogueCampaignWord.TemplateId && version == DialogueCampaignWord.ContentVersion ? DialogueCampaignWord.CreateTemplate() : null;

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.IsRules2Template
        internal static bool IsRules2Template(string id) => id == DialogueCampaignWord.TemplateId;

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.IsRules2Content
        private static bool IsRules2Content(string id) => id != null && id.StartsWith(DialogueCampaignWord.TemplateId + ":", StringComparison.Ordinal);

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.LiePrefix
        private static string LiePrefix(EpisodeState s, string npcId, int week) =>
            (s.Find(npcId)?.name ?? "\u0000") + " told me in week " + week.ToString(CultureInfo.InvariantCulture) + " they would vote out ";

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.TryValidateRules2, with no pending command.
        private static bool TryValidateRules2(EpisodeState s, out string error)
        {
            error = null;
            int version = s.dialogueRulesVersion;
            if (s.schemaVersion < SchemaFor(version))
                return Fail(out error, "Dialogue rules version " + version.ToString(CultureInfo.InvariantCulture) + " needs schema "
                    + SchemaFor(version).ToString(CultureInfo.InvariantCulture) + " or later.");
            if (!Prerequisites(s)) return Fail(out error, "Dialogue requires the complete active-from-week-one rules.");
            if (s.unifiedCommitments == null || s.unifiedCommitments.Any(r => r == null || r.conditionalVeto != null))
                return Fail(out error, "Conditional dialogue obligations are not supported by this version.");
            if (s.week < 1 || s.week > 100 || s.revision < 0 || s.nextSequence < 1 || s.contestants == null
                || s.contestants.Any(c => c == null) || s.Find(s.playerId) == null
                || s.storylines == null || s.storylines.Count > MaximumEpisodes || s.storylines.Any(c => c == null)
                || s.houseEvents == null || s.houseEvents.Count > MaximumEvents || s.houseEvents.Any(e => e == null)
                || s.windowActions == null || s.windowActions.Count != Windows.Count || s.windowActions.Any(n => n < 0)
                || s.lobbies == null || s.lobbies.Any(l => l == null) || s.events == null || s.events.Any(e => e == null)
                || s.memories == null || s.memories.Any(m => m == null))
                return Fail(out error, "Dialogue requires complete bounded episode owners.");
            if (s.dialogue.episodes.Count > ConversationBudget(version) || s.dialogue.episodes.Any(e => e == null)
                || s.dialogue.grants.Count != 0 || s.dialogue.vetoOccasions.Count != 0)
                return Fail(out error, "Only action-funded campaign conversations are supported by this version.");
            if (s.storylines.Any(c => c.templateId?.StartsWith("dialogue-", StringComparison.Ordinal) == true && !IsRules2Template(c.templateId))
                || s.houseEvents.Any(e => e.contentId?.StartsWith("dialogue-", StringComparison.Ordinal) == true && !IsRules2Content(e.contentId)))
                return Fail(out error, "Unknown dialogue content is not supported by this version.");
            if (s.dialogue.episodes.GroupBy(e => e.id).Any(g => g.Count() != 1)
                || s.dialogue.episodes.GroupBy(e => e.occasionId).Any(g => g.Count() != 1)
                || s.dialogue.episodes.GroupBy(e => e.entryEventId).Any(g => g.Count() != 1)
                || s.storylines.GroupBy(c => c.id).Any(g => g.Count() != 1)
                || s.houseEvents.GroupBy(e => e.id).Any(g => g.Count() != 1))
                return Fail(out error, "Dialogue identities must be unique.");
            var episodeIds = new HashSet<string>(s.dialogue.episodes.Select(e => e.id), StringComparer.Ordinal);
            if (s.storylines.Any(c => IsRules2Template(c.templateId) && !episodeIds.Contains(c.id))
                || s.houseEvents.Any(e => IsRules2Content(e.contentId) && !episodeIds.Contains(e.cycleId)))
                return Fail(out error, "A campaign cycle or event has no dialogue owner.");
            var ticks = new HashSet<string>(StringComparer.Ordinal);
            var logs = new HashSet<int>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            DialogueEpisodeState previous = null;
            int activeCount = 0;
            foreach (var episode in s.dialogue.episodes)
            {
                if (previous != null && (previous.time == null || episode.startedRevision <= previous.time.revision
                    || episode.startedSequence <= previous.time.sequence))
                    return Fail(out error, "Dialogue episodes must retain their nonoverlapping chronology.");
                if (!EpisodeRules2(s, episode, ticks, logs, ids, out error)) return false;
                if (Cycle(s, episode).status == StorylineStatus.Active) activeCount++;
                previous = episode;
            }
            if (activeCount > 1) return Fail(out error, "Only one dialogue may be active.");
            if (s.houseEvents.Count + ReservedEvents(s) > MaximumEvents)
                return Fail(out error, "The active dialogue's remaining event capacity is reserved.");
            return true;
        }

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.EpisodeRules2, with no pending command.
        private static bool EpisodeRules2(EpisodeState s, DialogueEpisodeState e,
            HashSet<string> ticks, HashSet<int> logs, HashSet<string> ids, out string error)
        {
            error = null;
            int version = s.dialogueRulesVersion, steps = StepsFor(version);
            var cycle = Cycle(s, e);
            var template = Rules2Template(cycle?.templateId, e.contentVersion);
            if (template == null || e.occasionKind != DialogueCampaignV1.Kind
                || e.week < 1 || e.week > s.week || e.window != Windows.AfterVeto || e.startedPhase != EpisodePhase.Campaign
                || e.startedSequence < 1 || e.startedSequence >= s.nextSequence
                || e.id != "story-" + e.startedSequence.ToString(CultureInfo.InvariantCulture)
                || string.IsNullOrEmpty(e.npcId) || e.npcId.Length > 160 || e.npcId == s.playerId
                || s.Find(e.npcId) == null || e.occasionId != Occasion(e.week, e.npcId)
                || !Revision(s, e.startedRevision))
                return Fail(out error, "Invalid campaign episode identity or start.");
            if (cycle.variant != 0 || cycle.week != e.week || cycle.title != template.title || cycle.category != template.eyebrow
                || cycle.lane != StoryLanes.Game || !Cast(cycle.cast, e.npcId) || cycle.vars == null || cycle.vars.Count != 0
                || cycle.nextWeek != 0 || cycle.nextAnchor != null || cycle.path == null || cycle.path.Count < 1 || cycle.path.Count > steps)
                return Fail(out error, "The dialogue must own its exact campaign cycle.");
            if (e.entry == null || !AcceptsFunding(version, e.entry.kind))
                return Fail(out error, "This version funds a conversation with the window's action only.");
            string tick = EntryTick(e.entry, e.week, e.window);
            if (tick == null || e.entry.grantId != null
                || e.entry.revision != e.startedRevision || !Revision(s, e.entry.revision)
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
                || e.evidence == null || e.evidence.Count > EvidenceRows(version) || e.evidence.Any(row => row == null))
                return Fail(out error, "Unsupported campaign response or evidence storage.");
            var items = s.houseEvents.Where(item => item.cycleId == e.id).ToArray();
            if (items.Length < 2 || items.Length > steps || items[0].id != e.entryEventId)
                return Fail(out error, "The campaign event shelf does not match its entry.");
            var rescue = PublicRescueSource(s, e.npcId, e.week);
            string beatId = DialogueCampaignWord.Open, ending = null, answer = null, reaction = null, cited = null;
            int previousSequence = e.startedSequence, previousRevision = e.startedRevision - 1, responseIndex = 0;
            for (int index = 0; index < items.Length; index++)
            {
                var item = items[index]; var beat = template.Beat(beatId);
                if (beat == null || !EventRules2(item, beat, e, template.id, out int eventSequence) || eventSequence <= previousSequence || eventSequence >= s.nextSequence)
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
                if (index == 0 && (step.optionId != DialogueCampaignWord.OpenCase || step.result != StoryResults.Plain)
                    || step.result != StoryResults.Plain && !lapsed
                    || lapsed && (index != cycle.path.Count - 1 || step.optionId != DialogueCampaignWord.Leave)
                    || item.outcome != (lapsed ? DialogueCampaignV1.LapsedOutcome : option.outcome))
                    return Fail(out error, "The campaign path must follow its exact authored edges.");
                previousSequence = step.logSequence;
                string suffix = lapsed ? "occasion-closed"
                    : index == 0 || option.next == DialogueCampaignWord.ByReaction || step.optionId == DialogueCampaignWord.Plea ? null
                    : step.optionId == DialogueCampaignWord.KeepOpen ? "no-promise-made" : "respectful-exit";
                if (suffix == null)
                {
                    string actual = Suffix(e, responseIndex, template.id);
                    if (index == 0)
                    {
                        if (Array.IndexOf(DialogueCampaignWord.Answers, actual) < 0)
                            return Fail(out error, "The voter's answer is not one this conversation can give.");
                        answer = actual;
                    }
                    else if (option.next == DialogueCampaignWord.ByReaction)
                    {
                        if (Array.IndexOf(DialogueCampaignWord.Reactions, actual) < 0)
                            return Fail(out error, "The voter's reaction is not one this conversation can give.");
                        reaction = actual; cited = DialogueCampaignWord.EvidenceOf(step.optionId);
                    }
                    else if (!VoteResponseRules2(s, e, actual, cited != null))
                        return Fail(out error, "An answered campaign must retain its actual vote response.");
                    suffix = actual;
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
                string next = lapsed ? "end:lapsed" : option.next;
                if (next.StartsWith("end:", StringComparison.Ordinal))
                {
                    ending = next.Substring(4);
                    if (index != cycle.path.Count - 1 || index != items.Length - 1)
                        return Fail(out error, "A terminal campaign cannot contain another node.");
                    break;
                }
                if (next == DialogueCampaignWord.ByAnswer)
                {
                    string drawn = index + 1 < items.Length ? BeatOf(items[index + 1], template.id) : null;
                    if (drawn == null || DialogueCampaignWord.AnswerOf(drawn) != answer
                        || DialogueCampaignWord.OffersRescue(drawn) != (answer != DialogueCampaignWord.Word && rescue != null))
                        return Fail(out error, "The reply node must follow the voter's answer and the facts the player holds.");
                    beatId = drawn;
                }
                else if (next == DialogueCampaignWord.ByReaction) beatId = DialogueCampaignWord.FinishBeat(reaction);
                else beatId = next;
            }
            if (responseIndex != e.responses.Count || cycle.eventId != items[items.Length - 1].id || cycle.beatId != beatId)
                return Fail(out error, "The campaign path, current node and responses must agree.");
            if (!StatementRules2(s, e, cycle, items, answer, ids, out error)) return false;
            if (!EvidenceRules2(s, e, cycle, items, cited, rescue, ids, out error)) return false;
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
                // No pending command: pending?.OwnsCampaignClosure(s, e) != true is true.
                if (ending == "lapsed" && currentClosure && e.week == s.week && s.phase == EpisodePhase.Campaign
                    && Eligible(s, e.npcId, out _))
                    return Fail(out error, "The current campaign occasion has not closed.");
            }
            return true;
        }

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.Suffix
        private static string Suffix(DialogueEpisodeState e, int index, string templateId)
        {
            string key = e.responses.Count > index ? e.responses[index].responseKey : null;
            return key != null && key.StartsWith(templateId + "/", StringComparison.Ordinal) ? key.Substring(templateId.Length + 1) : null;
        }

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.BeatOf
        private static string BeatOf(HouseEventState item, string templateId) =>
            item?.contentId != null && item.contentId.StartsWith(templateId + ":", StringComparison.Ordinal)
                ? item.contentId.Substring(templateId.Length + 1) : null;

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.Sequence
        private static bool Sequence(HouseEventState item, out int sequence)
        {
            sequence = 0;
            return item.id != null && item.id.StartsWith("house-event-", StringComparison.Ordinal)
                && int.TryParse(item.id.Substring(12), NumberStyles.None, CultureInfo.InvariantCulture, out sequence);
        }

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.EventRules2
        private static bool EventRules2(HouseEventState item, BeatTemplate beat, DialogueEpisodeState episode, string templateId, out int sequence)
        {
            sequence = 0;
            if (!Sequence(item, out sequence) || sequence < 1 || item.id != "house-event-" + sequence.ToString(CultureInfo.InvariantCulture)
                || item.kind != HouseEventKind.Story || item.week != episode.week || item.cycleId != episode.id
                || item.contentId != templateId + ":" + beat.id || item.title != beat.title || item.narrative != beat.summary
                || item.closesAnchor != beat.closes || item.surface != beat.surface || item.venue != beat.venue || item.lapseOptionId != beat.lapse
                || !Cast(item.cast, episode.npcId) || item.involvedIds == null || !item.involvedIds.SequenceEqual(new[] { episode.npcId })
                || item.choices == null || item.choices.Count != beat.options.Length) return false;
            for (int index = 0; index < beat.options.Length; index++)
            {
                var c = item.choices[index]; var o = beat.options[index];
                if (c == null || c.optionId != o.id || c.label != o.label || c.description != o.description || c.risk != o.risk
                    || c.glyph != null || c.approach != null || c.checkBase != -1 || c.subjectId != null || c.lapse != (o.id == beat.lapse)
                    || c.costsAction || c.conduct || c.pickPerson || c.locked || c.lockReason != null || c.trustChange != 0
                    || c.next != o.next || c.nextOnBackfire != null || c.impacts == null || c.impacts.Count != 0
                    || c.eligibleIds == null || c.eligibleIds.Count != 0 || c.bonusTraits == null || c.bonusTraits.Count != 0
                    || c.against == null || c.against.Count != 0 || c.effects == null || c.effects.Count != 0 || c.backfire == null || c.backfire.Count != 0) return false;
            }
            return true;
        }

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.VoteResponseRules2
        private static bool VoteResponseRules2(EpisodeState s, DialogueEpisodeState e, string suffix, bool cited)
        {
            string response = suffix == "vote-" + LobbyResponse.Receptive ? LobbyResponse.Receptive
                : suffix == "vote-" + LobbyResponse.Open ? LobbyResponse.Open
                : suffix == "vote-" + LobbyResponse.Skeptical ? LobbyResponse.Skeptical
                : suffix == "vote-" + LobbyResponse.Hostile ? LobbyResponse.Hostile : null;
            if (response == null) return false;
            var rows = s.lobbies.Where(l => l.week == e.week && l.phase == EpisodePhase.Campaign && l.deciderId == e.npcId).ToArray();
            string approach = cited ? LobbyApproach.Strategic : LobbyApproach.Emotional;
            return (e.week < s.week && rows.Length == 0) || rows.Length == 1 && rows[0].ask == LobbyAsk.Vote
                && rows[0].subjectId == s.playerId && rows[0].approach == approach && rows[0].response == response;
        }

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.FreeSequence
        private static bool FreeSequence(EpisodeState s, int sequence) => sequence > 0 && !s.events.Any(x => x.sequence == sequence)
            && !s.houseEvents.Any(x => x.id == "house-event-" + sequence.ToString(CultureInfo.InvariantCulture))
            && !s.storylines.Any(c => c.id == "story-" + sequence.ToString(CultureInfo.InvariantCulture));

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.StatementRules2, with no pending command.
        private static bool StatementRules2(EpisodeState s, DialogueEpisodeState e, StorylineState cycle, HouseEventState[] items, string answer,
            HashSet<string> ids, out string error)
        {
            error = null;
            bool told = answer == DialogueCampaignWord.Word || answer == DialogueCampaignWord.Against;
            if (!told) return e.statement == null || Fail(out error, "Only a voter who gave their word holds a statement.");
            var row = e.statement;
            var response = e.responses.FirstOrDefault(r => r.pathIndex == 0);
            int sequence = cycle.path[0].logSequence - 1;
            if (row == null || response == null || !Sequence(items[0], out int opened) || sequence <= opened || !FreeSequence(s, sequence)
                || row.id != "dialogue-statement-" + sequence.ToString(CultureInfo.InvariantCulture) || !ids.Add(row.id) || row.sequence != sequence
                || row.eventId != items[0].id || row.pathIndex != 0 || row.speakerId != e.npcId
                || row.conditionKind != VoteClaim || row.answer != VoteOut || row.week != e.week
                || row.revision != response.revision || !Revision(s, row.revision)
                || row.audienceIds == null || !row.audienceIds.SequenceEqual(new[] { s.playerId })
                || string.IsNullOrEmpty(row.targetId) || s.Find(row.targetId) == null || row.targetId == e.npcId
                || (answer == DialogueCampaignWord.Against) != (row.targetId == s.playerId)
                || e.week == s.week && s.ledger?.claims?.Any(k => k != null && k.week == e.week && k.voterId == e.npcId
                    && k.source == ClaimSource.Told && k.targetId == row.targetId) != true)
                return Fail(out error, "A voter's word must be its exact copied vote claim.");
            return true;
        }

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.EvidenceRules2, with no pending command.
        private static bool EvidenceRules2(EpisodeState s, DialogueEpisodeState e, StorylineState cycle, HouseEventState[] items, string cited,
            PowerRow rescue, HashSet<string> ids, out string error)
        {
            error = null;
            if (cited == null) return e.evidence.Count == 0 || Fail(out error, "Only a citing reply may retain evidence.");
            if (e.evidence.Count != 1) return Fail(out error, "A citation requires exactly one source-owned evidence row.");
            var row = e.evidence[0];
            var response = e.responses.FirstOrDefault(r => r.pathIndex == 1);
            int sequence = cycle.path[1].logSequence - 1;
            bool owned = row.kind == cited && row.pathIndex == 1 && row.eventId == items[1].id && response != null
                && row.capturedRevision == response.revision
                && Revision(s, row.capturedRevision)
                && Sequence(items[1], out int replied) && sequence > replied && FreeSequence(s, sequence)
                && row.id == "dialogue-evidence-" + sequence.ToString(CultureInfo.InvariantCulture) && ids.Add(row.id);
            bool source = cited == DialogueCampaignWord.StatementEvidence
                    ? row.statementId != null && row.statementId == e.statement?.id && row.power == null && row.memory == null
                : cited == DialogueCampaignWord.PowerEvidence
                    ? row.statementId == null && row.memory == null && SamePower(row.power, rescue)
                : cited == DialogueCampaignWord.MemoryEvidence && row.statementId == null && row.power == null && CaughtLieCopy(s, e, row.memory);
            if (!owned || !source)
                return Fail(out error, "The citation must retain its exact owned revision, event, identity and source.");
            return true;
        }

        // SOURCE 64d52f59 Gamesim.Simulation.DialogueRules.CaughtLieCopy
        private static bool CaughtLieCopy(EpisodeState s, DialogueEpisodeState e, MemoryState m) => m != null
            && m.ownerId == s.playerId && m.subjectId == e.npcId && m.isPrivate && m.week >= 1 && m.week < e.week
            && m.text != null && m.text.Length <= 2000 && m.text.StartsWith(LiePrefix(s, e.npcId, m.week), StringComparison.Ordinal);
    }

    // SOURCE 64d52f59 Gamesim.Simulation.DialogueCampaignWord (Story/DialogueCampaignWord.cs): rules 2's closed tree, the
    // fields and lookups the validator reads.
    internal static class DialogueCampaignWord
    {
        public const string TemplateId = "dialogue-campaign-word";
        public const int ContentVersion = 1;
        public const string Open = "open", ReplyPrefix = "reply-", FinishPrefix = "finish-";
        public const string Word = "word", Against = "against", Guarded = "guarded", Concern = "concern";
        public static readonly string[] Answers = { Word, Against, Guarded, Concern };
        public const string Warmed = "warmed", Unmoved = "unmoved", Backfired = "backfired";
        public static readonly string[] Reactions = { Warmed, Unmoved, Backfired };
        public const string ByAnswer = "by-answer", ByReaction = "by-reaction";
        public const string OpenCase = DialogueCampaignV1.OpenCase, Leave = DialogueCampaignV1.Leave;
        public const string Plea = DialogueCampaignV1.Plea, KeepOpen = DialogueCampaignV1.KeepOpen;
        public const string CiteWord = "hold-to-their-word", CiteRescue = "remind-of-the-rescue", CiteLie = "bring-up-the-lie";
        public const string Appeal = "make-it-personal", AskConcern = "ask-what-worries-them", Press = "press-them";
        public const string CiteWordLabel = "Hold them to their word", CiteRescueLabel = "Remind them of the rescue",
            CiteLieLabel = "Bring up the last lie", AppealLabel = "Make it personal", AskConcernLabel = "Ask what worries them",
            PressLabel = "Press them";
        public const string StatementEvidence = "statement", PowerEvidence = "power", MemoryEvidence = "memory";

        public static string ReplyBeat(string answer, bool rescue, bool lie) =>
            ReplyPrefix + answer + (rescue && answer != Word ? "-rescue" : "") + (lie ? "-lie" : "");

        public static string FinishBeat(string reaction) => FinishPrefix + reaction;

        public static IEnumerable<string> ReplyBeats()
        {
            foreach (string answer in Answers)
                foreach (bool rescue in answer == Word ? new[] { false } : new[] { false, true })
                    foreach (bool lie in new[] { false, true })
                        yield return ReplyBeat(answer, rescue, lie);
        }

        public static string AnswerOf(string beatId)
        {
            if (beatId == null || !beatId.StartsWith(ReplyPrefix, StringComparison.Ordinal)) return null;
            string rest = beatId.Substring(ReplyPrefix.Length);
            int dash = rest.IndexOf('-');
            string answer = dash < 0 ? rest : rest.Substring(0, dash);
            return Array.IndexOf(Answers, answer) >= 0 && ReplyBeats().Contains(beatId) ? answer : null;
        }

        public static bool OffersRescue(string beatId) => AnswerOf(beatId) != null && beatId.Contains("-rescue");
        public static bool OffersLie(string beatId) => AnswerOf(beatId) != null && beatId.EndsWith("-lie", StringComparison.Ordinal);

        public static string EvidenceOf(string optionId) =>
            optionId == CiteWord ? StatementEvidence : optionId == CiteRescue ? PowerEvidence : optionId == CiteLie ? MemoryEvidence : null;

        internal static ArcTemplate CreateTemplate()
        {
            var beats = new List<BeatTemplate>
            {
                Beat(Open, DialogueCampaignV1.OpenTitle, "There is time to ask where they stand.",
                    Option(OpenCase, DialogueCampaignV1.OpenLabel, "Ask where their vote is, and make your case.",
                        "You asked where their vote is.", ByAnswer), Exit())
            };
            foreach (string beat in ReplyBeats()) beats.Add(Reply(beat));
            beats.Add(Finish(Warmed, "They warmed to you", "That landed. Their vote is still theirs to give."));
            beats.Add(Finish(Unmoved, "They heard you", "Nothing moved yet. Their vote is still theirs to give."));
            beats.Add(Finish(Backfired, "That landed badly", "It went wrong. Their vote is still theirs to give."));
            return new ArcTemplate
            {
                id = TemplateId, eyebrow = DialogueCampaignV1.Category, title = DialogueCampaignV1.Title,
                beats = beats.ToArray()
            };
        }

        private static BeatTemplate Reply(string beatId)
        {
            string answer = AnswerOf(beatId);
            var options = new List<OptionTemplate>();
            if (answer == Word)
                options.Add(Option(CiteWord, CiteWordLabel, "Ask them to keep the vote they told you about.",
                    "You held them to their word.", ByReaction));
            else if (OffersRescue(beatId))
                options.Add(Option(CiteRescue, CiteRescueLabel, "Point to the week they used the veto to save you.",
                    "You reminded them of the rescue.", ByReaction));
            options.Add(answer == Concern
                ? Option(AskConcern, AskConcernLabel, "Let them say what they are afraid of, and answer it.",
                    "You asked what worries them.", ByReaction)
                : Option(Appeal, AppealLabel, "Tell them what staying means to you.", "You made it personal.", ByReaction));
            options.Add(OffersLie(beatId)
                ? Option(CiteLie, CiteLieLabel, "Remind them they lied to you about a vote before. It can backfire.",
                    "You brought up the last lie.", ByReaction, HouseEventRisk.Medium)
                : Option(Press, PressLabel, "Push them for a straight answer. It can backfire.", "You pressed them.",
                    ByReaction, HouseEventRisk.High));
            options.Add(Exit());
            string title = answer == Word ? "They give you their word" : answer == Against ? "They tell you straight"
                : answer == Concern ? "They have a concern" : "They keep it close";
            string summary = answer == Word ? "They say they are not voting you out."
                : answer == Against ? "They say they are voting you out."
                : answer == Concern ? "They worry what you would do if you stayed." : "They won't say where their vote is.";
            return Beat(beatId, title, summary, options.ToArray());
        }

        private static BeatTemplate Finish(string reaction, string title, string summary) => Beat(FinishBeat(reaction), title, summary,
            Option(Plea, DialogueCampaignV1.PleaLabel, DialogueCampaignV1.PleaDescription, DialogueCampaignV1.PleaOutcome, "end:answered"),
            Option(KeepOpen, DialogueCampaignV1.KeepLabel, DialogueCampaignV1.KeepDescription, DialogueCampaignV1.KeepOutcome, "end:neutral"),
            Exit());

        private static BeatTemplate Beat(string id, string title, string summary, params OptionTemplate[] options) => new BeatTemplate
        {
            id = id, title = title, summary = summary,
            closes = StoryAnchors.EvictionEve, surface = StorySurfaces.Conversation, venue = StoryVenues.Living,
            lapse = Leave, options = options
        };

        private static OptionTemplate Option(string id, string label, string description, string outcome, string next,
            string risk = HouseEventRisk.Low) => new OptionTemplate
        { id = id, label = label, description = description, outcome = outcome, next = next, risk = risk };

        private static OptionTemplate Exit() => Option(Leave, DialogueCampaignV1.LeaveLabel, DialogueCampaignV1.LeaveDescription,
            DialogueCampaignV1.LeaveOutcome, "end:neutral");
    }
}
