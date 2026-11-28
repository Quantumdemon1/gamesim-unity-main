using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// Dialogue rules 5's HoH grant (DIALOGUE-PLAN §4, slice D5b-1), written as the nominations open with the player as Head
        /// of Household: one conversation on the house, for the HoH consultation only. One a week: a week that already holds it
        /// gets no second, and nothing writes one for a replacement nomination. It records the crowning that wrote it and draws
        /// nothing. Nothing in a season below rules 5.
        /// </summary>
        internal static void GrantTheHoH(EpisodeState s)
        {
            if (!DialogueRules.HoHConsultOn(s) || s.phase != EpisodePhase.Nomination || s.hohId != s.playerId
                || s.nominees == null || s.nominees.Count != 0 || s.Find(s.playerId)?.status != ContestantStatus.Active) return;
            string id = DialogueRules.GrantId(s.week, DialogueRules.HoHRole);
            if (s.dialogue.grants.Any(g => g != null && g.id == id)) return;
            s.dialogue.grants.Add(new DialogueRoleGrantState
            {
                id = id, ownerId = s.playerId, week = s.week, role = DialogueRules.HoHRole, sourceKind = DialogueRules.FromCrowning,
                status = DialogueRules.GrantOpen, sourceRevision = s.revision, sourceSequence = s.nextSequence - 1,
                sourcePhase = s.phase, sourceNominees = new List<string>(),
            });
        }

        /// <summary>
        /// Dialogue rules 5's half of the pending dialogue command (DIALOGUE-PLAN §4.1 and §5.1, slice D5b-1): the HoH
        /// consultation, inside the same reserved-receipt transaction as every version. It draws the answer and the reaction on
        /// the occasion's keyed coins and the reaction's +3 or -3 through <see cref="ChangeKeyed"/>, and nothing from the season's
        /// stream. It writes the conversation, its events and lines, at most one relationship change (the player's act toward
        /// the houseguest, never a pair of NPCs) and, on a promise, the existing promise of safety - never a second one to them
        /// that week, and under the role moments the answer to their pitch on the board.
        /// </summary>
        internal sealed partial class PendingDialogueCommand
        {
            private bool HoHConsultCommand => request.kind == EpisodeCommandKind.StartDialogue
                ? DialogueRules.KindNow(original) == DialogueHoHConsult.Kind
                : originalEpisode?.occasionKind == DialogueHoHConsult.Kind;

            // The constructor's half for a consultation's choice: the node is the conversation's current one and the option is on it.
            private void PrepareHoHConsultChoice(StorylineState cycle)
            {
                var template = DialogueRules.Rules5Template(cycle.templateId, originalEpisode.contentVersion);
                Require(template != null && request.text == cycle.beatId && cycle.beatId != DialogueHoHConsult.Open
                    && template.Beat(cycle.beatId) != null, "The conversation changed. Refresh this choice.");
                Require(template.Beat(cycle.beatId).Option(request.secondTargetId) != null, "Choose one of this conversation's options.");
            }

            private void ResolveHoHConsult(EpisodeState state)
            {
                if (request.kind == EpisodeCommandKind.StartDialogue) StartHoHConsult(state); else ChooseHoHConsult(state);
            }

            private void StartHoHConsult(EpisodeState state)
            {
                Require(state.nextSequence == original.nextSequence, "The dialogue reservation changed before entry.");
                var template = DialogueRules.Rules5Template(request.text, DialogueHoHConsult.ContentVersion);
                Require(template != null && template.id == DialogueRules.Rules3EntryTemplate(original), "The conversation's content pair is not supported.");
                int sequence = state.nextSequence;
                var cycle = new StorylineState { id = Mint(state, "story"), templateId = template.id,
                    title = template.title, category = template.eyebrow ?? template.lane, lane = template.lane,
                    week = state.week, beatId = DialogueHoHConsult.Open, status = StorylineStatus.Active,
                    cast = new List<StoryRoleState> { new StoryRoleState { role = DialogueCampaignV1.SpeakerRole, contestantId = request.targetId } } };
                state.storylines.Add(cycle);
                var opening = AddEvent(state, cycle, DialogueHoHConsult.Open, DialogueHoHConsult.ContentVersion);
                const int window = Windows.AfterHoH;
                int before = original.windowActions[window];
                string occasion = DialogueRules.HoHConsultOccasion(state.week, request.targetId);
                // The HoH grant, while it is open and the week's free conversations allow, pays in place of the window's action.
                var grant = DialogueRules.GrantFor(original, DialogueHoHConsult.Kind);
                var episode = new DialogueEpisodeState { id = cycle.id, npcId = request.targetId, occasionKind = DialogueHoHConsult.Kind,
                    occasionId = occasion, contentVersion = DialogueHoHConsult.ContentVersion,
                    week = state.week, window = window, startedPhase = original.phase,
                    startedRevision = revision, startedSequence = sequence, entryEventId = opening.id,
                    entry = grant != null ? GrantEntry(grant, before)
                        : new DialogueEntryReceiptState { kind = DialogueRules.ActionEntry, revision = revision,
                            windowActionBefore = before, windowActionAfter = checked(before + 1) } };
                state.dialogue.episodes.Add(episode); expected.episodes.Add(episode.Clone());
                future.Add(episode, DialogueRevisionField.Started); future.Add(episode.entry, DialogueRevisionField.Entry);
                // The houseguest answers at once, on the open's own keyed coin, from the source as it stood when they were called in.
                string answer = DialogueRules.HoHConsultAnswer(original, request.targetId, occasion);
                Step(state, episode, cycle, DialogueHoHConsult.OpenCase, answer, false, DialogueHoHConsult.ReplyBeat(answer));
                if (grant != null) UseGrant(state, grant.id, episode.id); else SpendSocialAction(state);
            }

            private void ChooseHoHConsult(EpisodeState state)
            {
                var episode = state.dialogue.episodes.Single(x => x.id == originalEpisode.id);
                var cycle = state.storylines.Single(x => x.id == episode.id);
                string option = request.secondTargetId;
                if (DialogueHoHConsult.AnswerOf(cycle.beatId) != null && option != DialogueHoHConsult.Leave)
                {
                    int at = cycle.path.Count;
                    // The houseguest reacts once, on the reply's own coin.
                    string reaction = DialogueRules.HoHConsultReaction(original, episode.occasionId, at, option);
                    Require(reaction != null, "Choose one of this conversation's options.");
                    int delta = DialogueHoHConsult.Delta(reaction);
                    // The one relationship change a conversation makes: the player's act toward them, through the engine's reducer,
                    // its reciprocal draw from the conversation's own key (§5.2, §5.3). Never a pair of NPCs.
                    if (delta != 0)
                        ChangeKeyed(state, state.playerId, episode.npcId, delta, DialogueRules.Key(episode.occasionId, at) + ":change",
                            delta > 0 ? "A word before the nominations: they warmed to you." : "A word before the nominations: it landed badly.");
                    Step(state, episode, cycle, option, reaction, false, DialogueHoHConsult.FinishBeat(reaction));
                    return;
                }
                if (option == DialogueHoHConsult.PromiseSafety)
                {
                    Require(DialogueHoHConsult.ReactionOf(cycle.beatId) != null, "Choose one of this conversation's options.");
                    // The conversation ends with the player's word, then the word is the existing promise of safety (§4's table).
                    bool fresh = PromiseTheirSafetyIsNew(state, episode.npcId);
                    Step(state, episode, cycle, option, fresh ? DialogueHoHConsult.Promised : DialogueHoHConsult.Reaffirmed, false);
                    PromiseTheirSafety(state, episode.npcId);
                    return;
                }
                string response = option == DialogueHoHConsult.Leave ? DialogueHoHConsult.RespectfulExit
                    : option == DialogueHoHConsult.NoPromise ? DialogueHoHConsult.NoPromiseMade : null;
                Require(response != null, "Choose one of this conversation's options.");
                Step(state, episode, cycle, option, response, false);
            }

            /// <summary>Whether a promise of safety to this houseguest would be a new one: none from the player stands yet.</summary>
            private static bool PromiseTheirSafetyIsNew(EpisodeState state, string npcId)
            {
                Require(HoHPitches.CanPromiseSafety(state, npcId), "Your promise record is full.");
                return !HoHPitches.HasSafetyPromise(state, npcId);
            }

            /// <summary>
            /// The consultation's promise (§4's table): under the role moments, with their pitch on the board, the pitch's own
            /// Promise safety answer; otherwise the existing promise of safety the PromiseSafety act writes, paid by the
            /// conversation's entry. Never a second safety row to them: a promise already standing is reaffirmed, as the pitch's
            /// answer reaffirms one. Draws nothing.
            /// </summary>
            private static void PromiseTheirSafety(EpisodeState state, string npcId)
            {
                var pitch = DialogueRules.PitchOnTheBoard(state, npcId);
                if (pitch != null) { AnswerHoHPitch(state, pitch, HoHPitches.PromiseKey); return; }
                if (HoHPitches.HasSafetyPromise(state, npcId))
                    Log(state, "promise", "You reaffirmed your existing safety promise to " + Name(state, npcId) + ".", state.playerId, npcId);
                else MakePromise(state, npcId, PromiseKind.Safety, null);
            }

            /// <summary>
            /// The consultation's end (DIALOGUE-PLAN §4.1: "when the original nominations commit"): a consultation still open lapses
            /// through its own exit as the nominations are made, before anybody is named. Draws nothing. Called only for the
            /// nominations' own command, which the pending command owns while a consultation is open.
            /// </summary>
            internal void LapseHoHConsult(EpisodeState state)
            {
                Require(Bound(state) && !resolved && !sealedDelta && state.phase == EpisodePhase.Nomination
                    && originalEpisode?.occasionKind == DialogueHoHConsult.Kind, "The consultation's lapse is no longer owned.");
                var episode = state.dialogue.episodes.Single(x => x.id == originalEpisode.id);
                var cycle = state.storylines.Single(x => x.id == episode.id);
                Step(state, episode, cycle, DialogueHoHConsult.Leave,
                    state.Find(episode.npcId)?.status == ContestantStatus.Active ? DialogueHoHConsult.OccasionClosed : null, true);
                resolved = true; Seal(state);
            }
        }
    }
}
