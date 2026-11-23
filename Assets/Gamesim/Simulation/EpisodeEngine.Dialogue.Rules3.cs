using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// The ordinary Emotional save plea's consequences to the veto holder, without admission or funding: the plea a
        /// rules-3 holder appeal ends in (DIALOGUE-PLAN §5.1). Exactly what <c>Lobby</c> does for that plea, in its order -
        /// the chance, the roll, the row, the change, the memory, the line - less the action, which the conversation's
        /// entry paid, and less the closure hook, which only an independent plea owns.
        /// </summary>
        private static string ResolveEmotionalSavePlea(EpisodeState s, string holderId)
        {
            const string ask = LobbyAsk.Save, approach = LobbyApproach.Emotional;
            string subjectId = s.playerId;
            var decider = s.Find(holderId);
            double chance = StrategyRules.Chance(s, decider.id, ask, subjectId, approach);
            var answer = StrategyRules.Respond(chance, Roll(s), () => Roll(s));
            s.lobbies.Add(new LobbyState
            {
                week = s.week, phase = s.phase, deciderId = decider.id, ask = ask, subjectId = subjectId,
                approach = approach, response = answer.response,
                influence = Math.Max(-StrategyRules.MostInfluence, Math.Min(StrategyRules.MostInfluence, answer.influence)),
            });
            string plea = StrategyRules.Describe(s, ask, subjectId);
            if (answer.impact != 0)
                Change(s, s.playerId, decider.id, answer.impact, "Lobbying: asked " + plea + " (" + answer.response + ")", "lobby");
            Remember(s, decider.id, s.playerId, "Asked me " + plea + " in week " + s.week + ". I was " + answer.response + ".", true);
            Log(s, "lobby", "You asked " + decider.name + " " + plea + ": “" + StrategyRules.Pitch(s, ask, subjectId, approach)
                + "” " + decider.name + ": “" + StrategyRules.Answer(s, ask, subjectId, approach, answer.response) + "”",
                s.playerId, decider.id);
            return answer.response;
        }

        /// <summary>
        /// Dialogue rules 3's half of the pending dialogue command (DIALOGUE-PLAN §5, slice D3): the campaign conversation's
        /// four trees and the appeal to the veto holder, inside the same reserved-receipt transaction as rules 1 and 2.
        ///
        /// <para>What it draws: the answer (a keyed coin, or for the history the fact itself) and one reaction on a keyed
        /// coin, and the reaction's +3 or -3 through <see cref="ChangeKeyed"/>, whose reciprocal draw is keyed too. Nothing
        /// comes off the season's main stream except the final plea's own roll, the ordinary plea's.</para>
        /// <para>What it writes: the conversation, its events and lines, at most one statement (a vote claim the voter told
        /// the player, copied), at most one evidence row, at most one relationship change - the player's act toward the
        /// voter or holder, never a pair of NPCs - and on the plea the ordinary plea's records.</para>
        /// </summary>
        internal sealed partial class PendingDialogueCommand
        {
            private ClaimRow historySource;

            // The constructor's half for a rules-3 choice: the node is the conversation's current one, the option is on it,
            // and whatever the option stands on is still held, captured from the source state before anything moves.
            private void PrepareRules3Choice(StorylineState cycle, out MemoryState lie)
            {
                lie = null;
                var template = DialogueRules.Rules3Template(cycle.templateId, originalEpisode.contentVersion);
                Require(template != null && request.text == cycle.beatId && cycle.beatId != DialogueCampaignTrees.Open
                    && template.Beat(cycle.beatId) != null, "The conversation changed. Refresh this choice.");
                Require(template.Beat(cycle.beatId).Option(request.secondTargetId) != null, "Choose one of this conversation's options.");
                string option = request.secondTargetId;
                if (option == DialogueCampaignTrees.BringUp)
                {
                    historySource = DialogueRules.HistoryFact(original, originalEpisode.npcId, originalEpisode.week)?.Clone();
                    Require(historySource != null, "You no longer have that history with them to bring up.");
                }
                else if (option == DialogueCampaignTrees.AskStand
                    && DialogueRules.ToldClaim(original, originalEpisode.npcId, original.week) is ClaimRow told)
                    historySource = told.Clone();
                string evidence = template.id == DialogueCampaignTrees.TemplateId ? DialogueCampaignTrees.EvidenceOf(option) : null;
                if (evidence == DialogueCampaignTrees.StatementEvidence)
                    Require(originalEpisode.statement != null, "They gave you no word to count on.");
                else if (evidence == DialogueCampaignTrees.MemoryEvidence)
                {
                    lie = DialogueRules.CaughtLie(original, originalEpisode.npcId, originalEpisode.week)?.Clone();
                    Require(lie != null, "You no longer remember that lie well enough to bring it up.");
                }
            }

            private void ResolveRules3(EpisodeState state)
            {
                // Dialogue rules 4's veto question has its own half (EpisodeEngine.Dialogue.Rules4.cs).
                if (VetoQuestionCommand)
                {
                    if (request.kind == EpisodeCommandKind.StartDialogue) StartVetoQuestion(state); else ChooseVetoQuestion(state);
                    return;
                }
                if (request.kind == EpisodeCommandKind.StartDialogue) StartRules3(state);
                else ChooseRules3(state);
            }

            private void StartRules3(EpisodeState state)
            {
                Require(state.nextSequence == original.nextSequence, "The dialogue reservation changed before entry.");
                string kind = DialogueRules.KindNow(original);
                bool holder = kind == DialogueHolderAppeal.Kind;
                var template = DialogueRules.Rules3Template(request.text, holder ? DialogueHolderAppeal.ContentVersion : DialogueCampaignTrees.ContentVersion);
                Require(template != null && template.id == DialogueRules.Rules3EntryTemplate(original), "The conversation's content pair is not supported.");
                int sequence = state.nextSequence;
                var cycle = new StorylineState { id = Mint(state, "story"), templateId = template.id,
                    title = template.title, category = template.eyebrow ?? template.lane, lane = template.lane,
                    week = state.week, beatId = DialogueCampaignTrees.Open, status = StorylineStatus.Active,
                    cast = new List<StoryRoleState> { new StoryRoleState { role = DialogueCampaignV1.SpeakerRole, contestantId = request.targetId } } };
                state.storylines.Add(cycle);
                var opening = AddEvent(state, cycle, DialogueCampaignTrees.Open, template.id == DialogueHolderAppeal.TemplateId
                    ? DialogueHolderAppeal.ContentVersion : DialogueCampaignTrees.ContentVersion);
                int window = holder ? Windows.AfterNominations : Windows.AfterVeto;
                int before = original.windowActions[window];
                string occasion = DialogueRules.OccasionNow(original, request.targetId);
                var episode = new DialogueEpisodeState { id = cycle.id, npcId = request.targetId, occasionKind = kind,
                    occasionId = occasion, contentVersion = holder ? DialogueHolderAppeal.ContentVersion : DialogueCampaignTrees.ContentVersion,
                    week = state.week, window = window, startedPhase = original.phase,
                    startedRevision = revision, startedSequence = sequence, entryEventId = opening.id,
                    entry = new DialogueEntryReceiptState { kind = DialogueRules.ActionEntry, revision = revision,
                        windowActionBefore = before, windowActionAfter = checked(before + 1) } };
                state.dialogue.episodes.Add(episode); expected.episodes.Add(episode.Clone());
                future.Add(episode, DialogueRevisionField.Started); future.Add(episode.entry, DialogueRevisionField.Entry);
                if (holder)
                {
                    // The holder answers at once, on the open's own keyed coin, from the source as it stood when the player asked.
                    string answer = DialogueRules.HolderAnswer(original, request.targetId, occasion);
                    Step(state, episode, cycle, DialogueHolderAppeal.OpenCase, answer, false, DialogueHolderAppeal.ReplyBeat(answer));
                }
                else
                {
                    // The voter hears the player out; which approach node follows is fixed by the history the player holds now.
                    bool history = DialogueRules.HistoryFact(original, request.targetId, original.week) != null;
                    Step(state, episode, cycle, DialogueCampaignTrees.OpenCase, DialogueCampaignTrees.Listening, false,
                        history ? DialogueCampaignTrees.ApproachHistory : DialogueCampaignTrees.Approach);
                }
                SpendSocialAction(state);
            }

            private void ChooseRules3(EpisodeState state)
            {
                var episode = state.dialogue.episodes.Single(x => x.id == originalEpisode.id);
                var cycle = state.storylines.Single(x => x.id == episode.id);
                bool holder = cycle.templateId == DialogueHolderAppeal.TemplateId;
                string option = request.secondTargetId;
                if (!holder && DialogueCampaignTrees.IsApproach(cycle.beatId) && option != DialogueCampaignTrees.Leave)
                {
                    // The voter answers the approach the player chose, from the source as it stood.
                    string answer = DialogueRules.TreesAnswer(original, episode.npcId, episode.occasionId, option);
                    Require(answer != null, "Choose one of this conversation's options.");
                    if (DialogueCampaignTrees.WritesStatement(answer))
                    {
                        // The word is copied, because the ledger's claim has no id and may be pruned.
                        Require(historySource != null && (answer == DialogueCampaignTrees.Kept) == (historySource.week < original.week),
                            "The word they gave you is no longer on the record.");
                        var statement = new DialogueStatementState { id = Mint(state, "dialogue-statement"), eventId = cycle.eventId,
                            pathIndex = cycle.path.Count, speakerId = episode.npcId, targetId = historySource.targetId,
                            answer = DialogueRules.VoteOut, conditionKind = DialogueRules.VoteClaim, week = historySource.week,
                            revision = revision, sequence = state.nextSequence - 1,
                            audienceIds = new List<string> { state.playerId } };
                        var expectedEpisode = expected.episodes.Single(x => x.id == episode.id);
                        Require(episode.statement == null && expectedEpisode.statement == null, "This conversation already holds a statement.");
                        episode.statement = statement; expectedEpisode.statement = statement.Clone();
                        future.Add(statement, DialogueRevisionField.Statement);
                    }
                    Step(state, episode, cycle, option, answer, false, DialogueCampaignTrees.ReplyBeat(answer));
                    return;
                }
                bool reply = holder ? DialogueHolderAppeal.AnswerOf(cycle.beatId) != null : DialogueCampaignTrees.AnswerOf(cycle.beatId) != null;
                if (reply && option != DialogueCampaignTrees.Leave)
                {
                    int at = cycle.path.Count;
                    // The voter or holder reacts once, on the reply's own coin.
                    string reaction = DialogueRules.Rules3Reaction(original, episode.occasionKind, episode.occasionId, at, option);
                    Require(reaction != null, "Choose one of this conversation's options.");
                    int delta = DialogueCampaignTrees.Delta(reaction);
                    // The one relationship change a conversation makes: the player's act toward them, through the engine's
                    // reducer, its reciprocal draw from the conversation's own key (§5.2, §5.3). Never a pair of NPCs.
                    if (delta != 0)
                        ChangeKeyed(state, state.playerId, episode.npcId, delta, DialogueRules.Key(episode.occasionId, at) + ":change",
                            holder ? (delta > 0 ? "A talk about the veto: they warmed to you." : "A talk about the veto: it landed badly.")
                                : delta > 0 ? "A talk before the vote: they warmed to you." : "A talk before the vote: it landed badly.");
                    string kind = holder ? null : DialogueCampaignTrees.EvidenceOf(option);
                    if (kind != null)
                    {
                        // Minted last, just before the step's own line, as rules 2's citation is.
                        var evidence = new DialogueEvidenceState { id = Mint(state, "dialogue-evidence"), kind = kind,
                            eventId = cycle.eventId, pathIndex = at, capturedRevision = revision,
                            statementId = kind == DialogueCampaignTrees.StatementEvidence ? originalEpisode.statement?.id : null,
                            memory = kind == DialogueCampaignTrees.MemoryEvidence ? lieSource?.Clone() : null };
                        Require(evidence.statementId != null || evidence.memory != null, "The citation is not owned or was already captured.");
                        var expectedEpisode = expected.episodes.Single(x => x.id == episode.id);
                        Require(episode.evidence.Count == 0 && expectedEpisode.evidence.Count == 0, "The reserved citation was already consumed.");
                        episode.evidence.Add(evidence); expectedEpisode.evidence.Add(evidence.Clone());
                        future.Add(evidence, DialogueRevisionField.EvidenceCaptured);
                    }
                    Step(state, episode, cycle, option, reaction, false, DialogueCampaignTrees.FinishBeat(reaction));
                    return;
                }
                string response = option == DialogueCampaignTrees.Leave ? "respectful-exit"
                    : option == DialogueCampaignTrees.KeepOpen && !holder ? "no-promise-made"
                    : option == DialogueHolderAppeal.LeaveWithThem && holder ? DialogueHolderAppeal.NoAsk : null;
                // The plea is the ordinary plea's roll and records: for a vote, Strategic after a citation and Emotional
                // otherwise; for the veto, the Emotional save plea.
                if (!holder && option == DialogueCampaignTrees.Plea)
                    response = "vote-" + (episode.evidence.Count > 0 ? ResolveStrategicVotePlea(state, episode.npcId)
                        : ResolveEmotionalVotePlea(state, episode.npcId));
                else if (holder && option == DialogueHolderAppeal.AskToUse)
                {
                    Require(StrategyRules.CanLobby(state, episode.npcId, LobbyAsk.Save, state.playerId, out string refusal), refusal);
                    response = "save-" + ResolveEmotionalSavePlea(state, episode.npcId);
                }
                Require(response != null, "Choose one of this conversation's options.");
                Step(state, episode, cycle, option, response, false);
            }

            /// <summary>
            /// The holder appeal's end (DIALOGUE-PLAN §4.1: "at the decision"): the veto meeting's decision lapses an appeal
            /// still open, through its own exit, before anything is decided. Draws nothing and writes no plea. False when
            /// this command owns no open holder appeal.
            /// </summary>
            internal bool LapseHolderAppeal(EpisodeState state)
            {
                if (request.kind != EpisodeCommandKind.Advance || originalEpisode?.occasionKind != DialogueHolderAppeal.Kind) return false;
                Require(Bound(state) && !resolved && !sealedDelta && state.phase == EpisodePhase.VetoMeeting, "The veto appeal's lapse is no longer owned.");
                var episode = state.dialogue.episodes.Single(x => x.id == originalEpisode.id);
                var cycle = state.storylines.Single(x => x.id == episode.id);
                Step(state, episode, cycle, DialogueHolderAppeal.Leave,
                    state.Find(episode.npcId)?.status == ContestantStatus.Active ? "occasion-closed" : null, true);
                resolved = true; Seal(state); return true;
            }
        }
    }
}
