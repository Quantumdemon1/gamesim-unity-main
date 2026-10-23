using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Gamesim.Simulation
{
    public sealed partial class EpisodeEngine
    {
        private EpisodeState applyingDialogueCandidate;
        private PendingDialogueCommand pendingDialogue;

        private PendingDialogueCommand PreparePaidCampaignCommand(EpisodeState candidate, EpisodeCommand command)
        {
            bool choice = command.kind == EpisodeCommandKind.StartDialogue || command.kind == EpisodeCommandKind.ChooseDialogue;
            var active = DialogueRules.Active(current);
            bool advance = active != null && command.kind == EpisodeCommandKind.Advance && current.phase == EpisodePhase.Campaign;
            bool lobby = active != null && command.kind == EpisodeCommandKind.Lobby && command.targetId == active.npcId
                && LobbyAsk.TryDecode(command.text, out string ask, out _) && ask == LobbyAsk.Vote;
            if (!choice && !advance && !lobby) return null;
            Require(DialogueRules.On(current), "Paid dialogue is not enabled for this season.");
            Require(pendingDialogue == null && ReferenceEquals(candidate, applyingDialogueCandidate)
                && !ReferenceEquals(candidate, current), "Dialogue requires its own detached command candidate.");
            Require(Valid(current, out string error), error);
            var pending = new PendingDialogueCommand(this, candidate, command);
            pendingDialogue = pending;
            return pending;
        }

        // Public readers never discover this field. Only the exact explicitly threaded handle can
        // admit its source-derived receipts before Apply advances the real root revision.
        internal sealed class PendingDialogueCommand
        {
            private readonly EpisodeEngine owner;
            private readonly EpisodeState source, original, candidate;
            private readonly EpisodeCommand request;
            private readonly DialogueState expected;
            private readonly DialogueEpisodeState originalEpisode;
            private readonly PowerRow citationSource;
            private readonly Dictionary<object, DialogueRevisionField> future = new Dictionary<object, DialogueRevisionField>();
            private readonly int revision;
            private bool sealedDelta, retired, resolved;

            internal PendingDialogueCommand(EpisodeEngine owner, EpisodeState candidate, EpisodeCommand command)
            {
                this.owner = owner; this.candidate = candidate; source = owner.current;
                original = source.Clone(); expected = original.dialogue.Clone();
                revision = checked(source.revision + 1);
                request = new EpisodeCommand { id = command.id, actorId = command.actorId, targetId = command.targetId,
                    secondTargetId = command.secondTargetId, text = command.text, expectedRevision = command.expectedRevision,
                    expectedPhase = command.expectedPhase, kind = command.kind, useVeto = command.useVeto, performance = command.performance };
                Require(Same(source, candidate), "The dialogue candidate differs from its source.");
                originalEpisode = DialogueRules.Active(original);
                if (request.kind == EpisodeCommandKind.StartDialogue)
                {
                    ClosedPayload();
                    Require(request.text == DialogueRules.EntryTemplateId(original, request.targetId) && request.secondTargetId == "open-case",
                        "Choose the offered campaign opening.");
                    Require(DialogueRules.CanStart(original, request.targetId, out string error), error);
                }
                else if (request.kind == EpisodeCommandKind.ChooseDialogue)
                {
                    ClosedPayload();
                    Require(originalEpisode != null && request.targetId == originalEpisode.id, "That dialogue has already ended.");
                    var cycle = original.storylines.Single(x => x.id == originalEpisode.id);
                    var template = DialogueRules.CampaignTemplate(cycle.templateId, originalEpisode.contentVersion);
                    Require(template != null && request.text == cycle.beatId
                        && (cycle.beatId == "case" || cycle.beatId == "finish-personal" || cycle.beatId == "finish-record"),
                        "The conversation changed. Refresh this choice.");
                    Require(template.Beat(cycle.beatId).Option(request.secondTargetId) != null,
                        "Choose one of this conversation's options.");
                    if (request.secondTargetId == DialogueCampaignV2.Cite)
                    {
                        Require(template.id == DialogueCampaignV2.TemplateId && cycle.beatId == DialogueCampaignV2.Case,
                            "That path does not accept a public record.");
                        citationSource = DialogueRules.CapturePublicRescue(original, originalEpisode.npcId, originalEpisode.week);
                        Require(citationSource != null, "The source no longer owns that completed public rescue.");
                    }
                }
                else
                {
                    Require(originalEpisode != null, "No campaign conversation is open.");
                    if (request.kind == EpisodeCommandKind.Lobby)
                    {
                        Require(request.targetId == originalEpisode.npcId && LobbyAsk.TryDecode(request.text, out string ask, out _)
                            && ask == LobbyAsk.Vote, "This is not the open conversation's voter.");
                        Require(StrategyRules.CanLobby(original, request.targetId, LobbyAsk.Vote, request.secondTargetId, out string error), error);
                        Require(SocialActionsSpent(original) < SocialActionBudget(original), "You have no conversations left this week.");
                    }
                    else Require(request.kind == EpisodeCommandKind.Advance && original.phase == EpisodePhase.Campaign,
                        "Only the campaign boundary can lapse this conversation.");
                }
            }

            private void ClosedPayload() => Require(!request.useVeto && request.performance == 0,
                "Dialogue does not accept veto or competition input.");

            private bool Bound(EpisodeState state) => !retired && ReferenceEquals(owner.pendingDialogue, this)
                && ReferenceEquals(owner.current, source) && ReferenceEquals(owner.applyingDialogueCandidate, candidate)
                && ReferenceEquals(state, candidate) && state.revision == original.revision
                && state.sessionId == original.sessionId && state.playerId == original.playerId && state.week == original.week
                && (state.phase == original.phase || request.kind == EpisodeCommandKind.Advance && sealedDelta
                    && state.phase == EpisodePhase.Eviction && state.evictionStage == EvictionStage.Speeches)
                && source.revision == original.revision;

            internal bool Verify(EpisodeState state, out string error)
            {
                error = null;
                if (!Bound(state) || !sealedDelta || !Same(source, original) || !Same(state.dialogue, expected))
                { error = "The pending dialogue command does not own this exact candidate and receipt delta."; return false; }
                return true;
            }

            internal bool Allows(EpisodeState state, object row, DialogueRevisionField field)
            {
                if (!Bound(state) || !sealedDelta || row == null || !future.TryGetValue(row, out var allowed) || allowed != field) return false;
                return field == DialogueRevisionField.Started && row is DialogueEpisodeState episode && episode.startedRevision == revision
                    || field == DialogueRevisionField.Entry && row is DialogueEntryReceiptState entry && entry.revision == revision
                    || field == DialogueRevisionField.Response && row is DialogueResponseState response && response.revision == revision
                    || field == DialogueRevisionField.Time && row is DialogueTimeReceiptState time && time.revision == revision
                    || field == DialogueRevisionField.EvidenceCaptured && row is DialogueEvidenceState evidence && evidence.capturedRevision == revision;
            }

            internal bool Authorizes(EpisodeState state, string origin)
            {
                if (!Verify(state, out _)) return false;
                if (request.kind == EpisodeCommandKind.Advance)
                    return origin == UnifiedCommitments.StoryPromise || origin == UnifiedCommitments.StoryDeal;
                if (request.kind == EpisodeCommandKind.Lobby)
                    return origin == UnifiedVoteFamilyValidation.VoteLobby || origin == UnifiedCommitments.NpcPromise;
                return origin == UnifiedCommitments.NpcPromise;
            }

            internal PendingDialogueCommand SealedFor(EpisodeState state) => sealedDelta && Bound(state) ? this : null;

            // Advance seals this lapse at its original position in the ordered Story list, before
            // phase changes to Eviction. Only that exact source-owned interval may still look like
            // an otherwise eligible Campaign. A saved/public reader never receives this handle.
            internal bool OwnsCampaignClosure(EpisodeState state, DialogueEpisodeState episode) =>
                request.kind == EpisodeCommandKind.Advance && originalEpisode != null && resolved
                && episode != null && episode.id == originalEpisode.id && episode.time != null
                && state.dialogue.episodes.Any(row => ReferenceEquals(row, episode))
                && future.TryGetValue(episode.time, out var field) && field == DialogueRevisionField.Time
                && Verify(state, out _);

            internal void ResolveChoice(EpisodeState state)
            {
                Require(Bound(state) && !resolved && !sealedDelta && Same(source, original), "The paid choice is no longer owned.");
                Require(request.kind == EpisodeCommandKind.StartDialogue || request.kind == EpisodeCommandKind.ChooseDialogue,
                    "This command does not own a paid choice.");
                DialogueEpisodeState episode;
                StorylineState cycle;
                if (request.kind == EpisodeCommandKind.StartDialogue)
                {
                    Require(state.nextSequence == original.nextSequence, "The dialogue reservation changed before entry.");
                    int contentVersion = request.text == DialogueCampaignV2.TemplateId ? DialogueCampaignV2.ContentVersion : DialogueCampaignV1.ContentVersion;
                    var template = DialogueRules.CampaignTemplate(request.text, contentVersion);
                    Require(template != null, "The campaign content pair is not supported.");
                    int sequence = state.nextSequence;
                    cycle = new StorylineState { id = Mint(state, "story"), templateId = template.id,
                        title = template.title, category = template.eyebrow ?? template.lane, lane = template.lane,
                        week = state.week, beatId = "open", status = StorylineStatus.Active,
                        cast = new List<StoryRoleState> { new StoryRoleState { role = DialogueCampaignV1.SpeakerRole, contestantId = request.targetId } } };
                    state.storylines.Add(cycle);
                    var opening = AddEvent(state, cycle, "open", contentVersion);
                    int before = original.windowActions[Windows.AfterVeto];
                    episode = new DialogueEpisodeState { id = cycle.id, npcId = request.targetId, occasionKind = DialogueCampaignV1.Kind,
                        occasionId = DialogueRules.Occasion(state.week, request.targetId), contentVersion = contentVersion,
                        week = state.week, window = Windows.AfterVeto, startedPhase = EpisodePhase.Campaign,
                        startedRevision = revision, startedSequence = sequence, entryEventId = opening.id,
                        entry = new DialogueEntryReceiptState { kind = DialogueCampaignV1.EntryKind, revision = revision,
                            windowActionBefore = before, windowActionAfter = checked(before + 1) } };
                    state.dialogue.episodes.Add(episode); expected.episodes.Add(episode.Clone());
                    future.Add(episode, DialogueRevisionField.Started); future.Add(episode.entry, DialogueRevisionField.Entry);
                    Step(state, episode, cycle, "open-case", "hear-your-case", false);
                    SpendSocialAction(state);
                }
                else
                {
                    episode = state.dialogue.episodes.Single(x => x.id == originalEpisode.id);
                    cycle = state.storylines.Single(x => x.id == episode.id);
                    string response = request.secondTargetId == "appeal-personally" ? "hear-personal-case"
                        : request.secondTargetId == "leave" ? "respectful-exit" : "no-promise-made";
                    if (request.secondTargetId == DialogueCampaignV2.Cite)
                    {
                        Require(citationSource != null && episode.evidence.Count == 0, "The citation is not owned or was already captured.");
                        var evidence = new DialogueEvidenceState { id = Mint(state, "dialogue-evidence"), kind = "power",
                            eventId = cycle.eventId, pathIndex = 1, capturedRevision = revision, power = citationSource.Clone() };
                        // Build the independent private expected clone BEFORE sealing, never from a finished candidate.
                        var expectedEpisode = expected.episodes.Single(x => x.id == episode.id);
                        Require(expectedEpisode.evidence.Count == 0, "The reserved citation was already consumed.");
                        episode.evidence.Add(evidence); expectedEpisode.evidence.Add(evidence.Clone());
                        future.Add(evidence, DialogueRevisionField.EvidenceCaptured);
                        response = "explain-that-record";
                    }
                    if (request.secondTargetId == "make-plea")
                        response = "vote-" + (cycle.beatId == DialogueCampaignV2.FinishRecord
                            ? ResolveStrategicVotePlea(state, episode.npcId) : ResolveEmotionalVotePlea(state, episode.npcId));
                    Step(state, episode, cycle, request.secondTargetId, response, false);
                }
                resolved = true; Seal(state);
            }

            private static HouseEventState AddEvent(EpisodeState state, StorylineState cycle, string beatId, int contentVersion)
            {
                Require(state.houseEvents.Count < 400, "The house's event history is full.");
                var template = DialogueRules.CampaignTemplate(cycle.templateId, contentVersion);
                Require(template != null, "The campaign content pair is not supported.");
                cycle.beatId = beatId;
                var item = DrawBeat(state, new StoryCycle(cycle, template), template.Beat(beatId), StoryAnchors.BlockSet);
                state.houseEvents.Add(item); cycle.eventId = item.id;
                Log(state, StoryLog.Offer, item.title + ". " + item.narrative, state.playerId);
                return item;
            }

            private void Step(EpisodeState state, DialogueEpisodeState episode, StorylineState cycle,
                string optionId, string responseKey, bool lapsed)
            {
                var item = state.houseEvents.Single(x => x.id == cycle.eventId);
                var template = DialogueRules.CampaignTemplate(cycle.templateId, episode.contentVersion);
                Require(template != null, "The campaign content pair is not supported.");
                var beat = template.Beat(cycle.beatId);
                var option = beat.Option(optionId);
                int index = item.choices.FindIndex(x => x.optionId == optionId);
                Require(!item.resolved && option != null && index >= 0, "The conversation choice is no longer open.");
                item.resolved = true; item.chosenIndex = index;
                item.outcome = lapsed ? DialogueCampaignV1.LapsedOutcome : option.outcome;
                int logSequence = state.nextSequence;
                Log(state, StoryLog.Outcome, item.title + ": " + item.outcome, state.playerId, episode.npcId);
                int pathIndex = cycle.path.Count;
                AddStep(cycle, beat.id, optionId, lapsed ? StoryResults.Lapsed : StoryResults.Plain, state.week, logSequence);
                var expectedEpisode = expected.episodes.Single(x => x.id == episode.id);
                if (responseKey != null)
                {
                    var response = new DialogueResponseState { eventId = item.id, speakerId = episode.npcId,
                        responseKey = cycle.templateId + "/" + responseKey, pathIndex = pathIndex, revision = revision };
                    episode.responses.Add(response); expectedEpisode.responses.Add(response.Clone());
                    future.Add(response, DialogueRevisionField.Response);
                }
                if (!lapsed && !option.next.StartsWith("end:", StringComparison.Ordinal))
                { AddEvent(state, cycle, option.next, episode.contentVersion); return; }
                string ending = lapsed ? "lapsed" : option.next.Substring(4);
                cycle.status = StorylineStatus.Completed; cycle.endedWeek = state.week; cycle.endingId = ending;
                cycle.nextAnchor = null; cycle.nextWeek = 0;
                var time = new DialogueTimeReceiptState { revision = revision, sequence = logSequence,
                    window = Windows.AfterVeto, reason = lapsed ? "occasion-closed" : ending };
                Require(episode.time == null && expectedEpisode.time == null, "This conversation already has its ending receipt.");
                episode.time = time; expectedEpisode.time = time.Clone(); future.Add(time, DialogueRevisionField.Time);
            }

            internal bool LapseAt(EpisodeState state, HouseEventState item)
            {
                if (request.kind != EpisodeCommandKind.Advance || originalEpisode == null || item.cycleId != originalEpisode.id) return false;
                Require(Bound(state) && !resolved && !sealedDelta, "The campaign lapse is no longer owned.");
                var episode = state.dialogue.episodes.Single(x => x.id == originalEpisode.id);
                var cycle = state.storylines.Single(x => x.id == episode.id);
                Require(item.id == cycle.eventId && state.phase == EpisodePhase.Campaign, "The campaign lapse changed its event.");
                Step(state, episode, cycle, "leave", state.Find(episode.npcId)?.status == ContestantStatus.Active ? "occasion-closed" : null, true);
                resolved = true; Seal(state); return true;
            }

            internal void CloseForLobby(EpisodeState state, string voterId)
            {
                if (request.kind != EpisodeCommandKind.Lobby || originalEpisode == null || voterId != originalEpisode.npcId) return;
                Require(Bound(state) && !resolved && !sealedDelta && state.lobbies.Count == original.lobbies.Count + 1,
                    "The independent plea does not own this closure.");
                var actual = state.lobbies.Last();
                Require(actual.deciderId == voterId && actual.week == original.week && actual.phase == EpisodePhase.Campaign
                    && actual.ask == LobbyAsk.Vote && actual.subjectId == original.playerId
                    && LobbyAsk.TryDecode(request.text, out _, out string approach) && actual.approach == approach,
                    "The independent plea changed its source.");
                var episode = state.dialogue.episodes.Single(x => x.id == originalEpisode.id);
                Step(state, episode, state.storylines.Single(x => x.id == episode.id), "leave", "occasion-closed", true);
                resolved = true; Seal(state);
            }

            private void Seal(EpisodeState state)
            {
                Require(Bound(state) && !sealedDelta && Same(source, original) && Same(state.dialogue, expected),
                    "The command's exact dialogue delta did not match its source reservation.");
                sealedDelta = true;
            }

            internal void Retire()
            {
                retired = true;
                if (ReferenceEquals(owner.pendingDialogue, this)) owner.pendingDialogue = null;
            }

            // DTO fields only, never properties or serialized authority. This compares the immutable
            // source and privately built expected delta; it neither filters nor rewrites a candidate.
            private static bool Same(object left, object right)
            {
                if (ReferenceEquals(left, right)) return true;
                if (left == null || right == null || left.GetType() != right.GetType()) return false;
                var type = left.GetType();
                if (type.IsValueType || left is string) return left.Equals(right);
                if (left is IList a && right is IList b)
                {
                    if (a.Count != b.Count) return false;
                    for (int i = 0; i < a.Count; i++) if (!Same(a[i], b[i])) return false;
                    return true;
                }
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
                return fields.Length > 0 && fields.All(field => Same(field.GetValue(left), field.GetValue(right)));
            }
        }
    }
}
