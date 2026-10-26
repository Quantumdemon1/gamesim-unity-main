using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// Dialogue rules 2's half of the pending dialogue command (DIALOGUE-PLAN §5, slice D2): the vote-word tree's
        /// entry, its reply and its finish, inside the same reserved-receipt transaction as rules 1.
        ///
        /// <para>What it draws: the voter's answer and their reaction, each on its own keyed coin
        /// (<see cref="DialogueRules.Key"/>), and the reaction's +3 or -3 through <see cref="ChangeKeyed"/>, whose
        /// reciprocal draw is keyed too. Nothing comes off the season's main stream except the final plea's own roll,
        /// which is the ordinary plea's, so starting, leaving or abandoning a conversation re-rolls nothing.</para>
        ///
        /// <para>What it writes: the conversation, its events and log lines, at most one statement (the voter's word,
        /// copied), at most one evidence row (the fact the player cited), one relationship change at most, and on the
        /// plea the ordinary plea's records. The entry is the window's action; every reply is free, and each is a step of
        /// a conversation that can happen once per voter and week, counted on the started row (§4).</para>
        /// </summary>
        internal sealed partial class PendingDialogueCommand
        {
            private readonly MemoryState lieSource;

            // The constructor's half for a choice: the node is the conversation's current one, the option is on it, and a
            // citation's source is still held, captured from the source state before anything moves.
            private void PrepareRules2Choice(StorylineState cycle, out PowerRow rescue, out MemoryState lie)
            {
                rescue = null; lie = null;
                var template = DialogueRules.Rules2Template(cycle.templateId, originalEpisode.contentVersion);
                Require(template != null && request.text == cycle.beatId && cycle.beatId != DialogueCampaignWord.Open
                    && template.Beat(cycle.beatId) != null, "The conversation changed. Refresh this choice.");
                Require(template.Beat(cycle.beatId).Option(request.secondTargetId) != null, "Choose one of this conversation's options.");
                string evidence = DialogueCampaignWord.EvidenceOf(request.secondTargetId);
                if (evidence == DialogueCampaignWord.StatementEvidence)
                    Require(originalEpisode.statement != null, "They gave you no word to hold them to.");
                else if (evidence == DialogueCampaignWord.PowerEvidence)
                {
                    rescue = DialogueRules.CapturePublicRescue(original, originalEpisode.npcId, originalEpisode.week);
                    Require(rescue != null, "The source no longer owns that completed public rescue.");
                }
                else if (evidence == DialogueCampaignWord.MemoryEvidence)
                {
                    lie = DialogueRules.CaughtLie(original, originalEpisode.npcId, originalEpisode.week)?.Clone();
                    Require(lie != null, "You no longer remember that lie well enough to bring it up.");
                }
            }

            private void ResolveRules2(EpisodeState state)
            {
                if (request.kind == EpisodeCommandKind.StartDialogue) StartRules2(state);
                else ChooseRules2(state);
            }

            private void StartRules2(EpisodeState state)
            {
                Require(state.nextSequence == original.nextSequence, "The dialogue reservation changed before entry.");
                var template = DialogueRules.Rules2Template(request.text, DialogueCampaignWord.ContentVersion);
                Require(template != null, "The campaign content pair is not supported.");
                int sequence = state.nextSequence;
                var cycle = new StorylineState { id = Mint(state, "story"), templateId = template.id,
                    title = template.title, category = template.eyebrow ?? template.lane, lane = template.lane,
                    week = state.week, beatId = DialogueCampaignWord.Open, status = StorylineStatus.Active,
                    cast = new List<StoryRoleState> { new StoryRoleState { role = DialogueCampaignV1.SpeakerRole, contestantId = request.targetId } } };
                state.storylines.Add(cycle);
                var opening = AddEvent(state, cycle, DialogueCampaignWord.Open, DialogueCampaignWord.ContentVersion);
                int before = original.windowActions[Windows.AfterVeto];
                string occasion = DialogueRules.Occasion(state.week, request.targetId);
                var episode = new DialogueEpisodeState { id = cycle.id, npcId = request.targetId, occasionKind = DialogueCampaignV1.Kind,
                    occasionId = occasion, contentVersion = DialogueCampaignWord.ContentVersion,
                    week = state.week, window = Windows.AfterVeto, startedPhase = EpisodePhase.Campaign,
                    startedRevision = revision, startedSequence = sequence, entryEventId = opening.id,
                    entry = new DialogueEntryReceiptState { kind = DialogueRules.ActionEntry, revision = revision,
                        windowActionBefore = before, windowActionAfter = checked(before + 1) } };
                state.dialogue.episodes.Add(episode); expected.episodes.Add(episode.Clone());
                future.Add(episode, DialogueRevisionField.Started); future.Add(episode.entry, DialogueRevisionField.Entry);

                // The voter's answer, from the source as it stood when the player asked: their word if they told the player
                // this week, otherwise a keyed coin. A word is copied, because the ledger's claim has no id and may be pruned.
                string answer = DialogueRules.VoterAnswer(original, request.targetId, occasion);
                if (answer == DialogueCampaignWord.Word || answer == DialogueCampaignWord.Against)
                {
                    var claim = DialogueRules.ToldClaim(original, request.targetId, original.week);
                    Require(claim != null, "The word they gave you is no longer on the record.");
                    int at = state.nextSequence;
                    var statement = new DialogueStatementState { id = Mint(state, "dialogue-statement"), eventId = opening.id, pathIndex = 0,
                        speakerId = request.targetId, targetId = claim.targetId, answer = DialogueRules.VoteOut,
                        conditionKind = DialogueRules.VoteClaim, week = claim.week, revision = revision, sequence = at,
                        audienceIds = new List<string> { state.playerId } };
                    var expectedEpisode = expected.episodes.Single(x => x.id == episode.id);
                    Require(episode.statement == null && expectedEpisode.statement == null, "This conversation already holds a statement.");
                    episode.statement = statement; expectedEpisode.statement = statement.Clone();
                    future.Add(statement, DialogueRevisionField.Statement);
                }
                string reply = DialogueCampaignWord.ReplyBeat(answer,
                    DialogueRules.CapturePublicRescue(original, request.targetId, original.week) != null,
                    DialogueRules.CaughtLie(original, request.targetId, original.week) != null);
                Step(state, episode, cycle, DialogueCampaignWord.OpenCase, answer, false, reply);
                SpendSocialAction(state);
            }

            private void ChooseRules2(EpisodeState state)
            {
                var episode = state.dialogue.episodes.Single(x => x.id == originalEpisode.id);
                var cycle = state.storylines.Single(x => x.id == episode.id);
                string option = request.secondTargetId;
                if (DialogueCampaignWord.AnswerOf(cycle.beatId) != null && option != DialogueCampaignWord.Leave)
                {
                    // The voter reacts once, on the reply's own coin.
                    string reaction = DialogueRules.VoterReaction(original, episode.occasionId, option);
                    Require(reaction != null, "Choose one of this conversation's options.");
                    int delta = DialogueCampaignWord.Delta(reaction);
                    // The one relationship change a conversation makes: the player's act toward the voter, through the
                    // engine's reducer, its reciprocal draw from the conversation's own key (§5.2, §5.3). Never a pair of NPCs.
                    if (delta != 0)
                        ChangeKeyed(state, state.playerId, episode.npcId, delta, DialogueRules.Key(episode.occasionId, 1) + ":change",
                            delta > 0 ? "A talk about the vote: they warmed to you." : "A talk about the vote: it landed badly.");
                    string kind = DialogueCampaignWord.EvidenceOf(option);
                    if (kind != null)
                    {
                        // Minted last, just before the step's own line, as rules 1's citation is.
                        var evidence = new DialogueEvidenceState { id = Mint(state, "dialogue-evidence"), kind = kind,
                            eventId = cycle.eventId, pathIndex = 1, capturedRevision = revision,
                            statementId = kind == DialogueCampaignWord.StatementEvidence ? originalEpisode.statement?.id : null,
                            power = kind == DialogueCampaignWord.PowerEvidence ? citationSource?.Clone() : null,
                            memory = kind == DialogueCampaignWord.MemoryEvidence ? lieSource?.Clone() : null };
                        Require(evidence.statementId != null || evidence.power != null || evidence.memory != null,
                            "The citation is not owned or was already captured.");
                        var expectedEpisode = expected.episodes.Single(x => x.id == episode.id);
                        Require(episode.evidence.Count == 0 && expectedEpisode.evidence.Count == 0, "The reserved citation was already consumed.");
                        episode.evidence.Add(evidence); expectedEpisode.evidence.Add(evidence.Clone());
                        future.Add(evidence, DialogueRevisionField.EvidenceCaptured);
                    }
                    Step(state, episode, cycle, option, reaction, false, DialogueCampaignWord.FinishBeat(reaction));
                    return;
                }
                string response = option == DialogueCampaignWord.Leave ? "respectful-exit"
                    : option == DialogueCampaignWord.KeepOpen ? "no-promise-made" : null;
                // The plea is the ordinary plea's roll and records: Strategic after a citation, Emotional otherwise.
                if (option == DialogueCampaignWord.Plea)
                    response = "vote-" + (episode.evidence.Count > 0 ? ResolveStrategicVotePlea(state, episode.npcId)
                        : ResolveEmotionalVotePlea(state, episode.npcId));
                Require(response != null, "Choose one of this conversation's options.");
                Step(state, episode, cycle, option, response, false);
            }
        }
    }
}
