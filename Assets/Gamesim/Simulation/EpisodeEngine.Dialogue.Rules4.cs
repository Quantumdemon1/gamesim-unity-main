using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// Dialogue rules 4 at the veto meeting's opening (DIALOGUE-PLAN §4.2 point 3): the week's veto occasion - the holder,
        /// the Head of Household, the veto players and the nominees as the result stands - and every conditional promise whose
        /// condition the result did not meet lapses, neutrally: its maker did not win, or the player is off the block or out of
        /// the house. Expired, with no line, no score and no draw. Nothing in a season below rules 4.
        /// </summary>
        internal static void RecordVetoOccasion(EpisodeState s)
        {
            if (!DialogueRules.VetoQuestionOn(s) || s.phase != EpisodePhase.VetoMeeting || string.IsNullOrEmpty(s.vetoHolderId)
                || s.dialogue.vetoOccasions.Any(o => o != null && o.week == s.week)) return;
            s.dialogue.vetoOccasions.Add(new DialogueVetoOccasionState
            {
                week = s.week, resultRevision = s.revision, resultSequence = s.nextSequence - 1,
                hohId = s.hohId, holderId = s.vetoHolderId,
                participants = s.vetoPlayers.ToList(), nomineesAtResult = s.nominees.ToList(),
            });
            foreach (var deal in s.deals.Where(d => d.type == DealKind.VetoIf && d.status == DealStatus.Active && d.week == s.week))
                if (deal.proposerId != s.vetoHolderId || !s.nominees.Contains(deal.targetId)
                    || s.Find(deal.targetId)?.status != ContestantStatus.Active)
                    deal.status = DealStatus.Expired;
        }

        /// <summary>
        /// Dialogue rules 4's own settlement step, after the veto's existing verdicts (DIALOGUE-PLAN §4.2 point 4): the house's
        /// decision written to the week's occasion, then each met conditional promise kept or broken by
        /// <see cref="DealResolution.VetoIf"/>. A veto that could not legally be used lapses it neutrally. Kept, the player
        /// thinks the better of the holder (+24); broken, the worse (-45): the deal rules' numbers at the Critical weight. It
        /// writes the status, the player's view of the holder, the player's memory and reckoning, one line to the two of them,
        /// and under the commitment rules the settled week, who broke it and the breach the player holds. The promise was
        /// private: no spread, no draw, and no pair of houseguests moves. Nothing in a season below rules 4.
        /// </summary>
        internal static void SettleConditionalVeto(EpisodeState s, bool used, string saved, string replacement)
        {
            if (!DialogueRules.VetoQuestionOn(s) || !s.vetoResolved || string.IsNullOrEmpty(s.vetoHolderId)) return;
            var before = used ? s.nominees.Where(id => id != replacement).Append(saved).ToList() : s.nominees.ToList();
            bool legal = used || !VetoIsLockedAtFinalFour(s) && ReplacementCandidates(s).Any();
            var occasion = s.dialogue.vetoOccasions.LastOrDefault(o => o != null && o.week == s.week);
            if (occasion != null && occasion.decision == null)
                occasion.decision = new DialogueVetoDecisionState
                {
                    revision = s.revision, sequence = s.nextSequence - 1,
                    activeIds = s.Active.Select(c => c.id).ToList(),
                    nominees = before.OrderBy(id => id, System.StringComparer.Ordinal).ToList(),
                    replacementCandidates = s.Active.Select(c => c.id)
                        .Where(id => id != s.hohId && id != s.vetoHolderId && !before.Contains(id))
                        .OrderBy(id => id, System.StringComparer.Ordinal).ToList(),
                    used = used, savedId = used ? saved : null, replacementId = used ? replacement : null,
                };
            bool rules = CommitmentRulesOn(s);
            foreach (var deal in s.deals.Where(d => d.type == DealKind.VetoIf && d.status == DealStatus.Active && d.week == s.week).ToList())
            {
                string status = legal && before.Contains(deal.targetId) ? DealResolution.VetoIf(deal, s.vetoHolderId, saved, used) : null;
                if (status == null) { deal.status = DealStatus.Expired; continue; }
                deal.status = status;
                bool kept = status == DealStatus.Fulfilled;
                string holder = deal.proposerId, player = deal.targetId;
                double delta = DealResolution.Impact(deal, status);
                string text = Name(s, holder) + (kept ? " won the veto and kept their promise to use it on you."
                    : " won the veto and broke their promise to use it on you.");
                WriteScore(s, player, holder, delta);
                if (rules)
                {
                    deal.settledWeek = s.week;
                    deal.brokenById = kept ? null : holder;
                    if (!kept) RecordBreach(s, player, holder, delta, text);
                }
                Remember(s, player, holder, text, true);
                Log(s, "deal-outcome", text, holder, player);
                if (!kept) StoryWordBrokenBeforeSafetyEffects(s, player, holder, GrudgeCauses.DealBroken, 60, null);
            }
        }

        /// <summary>
        /// Dialogue rules 4's half of the pending dialogue command (DIALOGUE-PLAN §4.2, slice D4): the veto question, inside the
        /// same reserved-receipt transaction as every version. It draws the answer on the occasion's keyed coin and nothing from
        /// the season's stream; it writes the conversation, its events and lines, the statement, and after a yes the one
        /// conditional promise. It moves no relationship.
        /// </summary>
        internal sealed partial class PendingDialogueCommand
        {
            private bool VetoQuestionCommand => request.kind == EpisodeCommandKind.StartDialogue
                ? DialogueRules.KindNow(original) == DialogueVetoQuestion.Kind
                : originalEpisode?.occasionKind == DialogueVetoQuestion.Kind;

            // The constructor's half for a veto question's choice: the node is the conversation's current one and the option is on it.
            private void PrepareVetoQuestionChoice(StorylineState cycle)
            {
                var template = DialogueRules.Rules4Template(cycle.templateId, originalEpisode.contentVersion);
                Require(template != null && request.text == cycle.beatId && cycle.beatId != DialogueVetoQuestion.Open
                    && template.Beat(cycle.beatId) != null, "The conversation changed. Refresh this choice.");
                Require(template.Beat(cycle.beatId).Option(request.secondTargetId) != null, "Choose one of this conversation's options.");
            }

            private void StartVetoQuestion(EpisodeState state)
            {
                Require(state.nextSequence == original.nextSequence, "The dialogue reservation changed before entry.");
                var template = DialogueRules.Rules4Template(request.text, DialogueVetoQuestion.ContentVersion);
                Require(template != null && template.id == DialogueRules.Rules3EntryTemplate(original), "The conversation's content pair is not supported.");
                int sequence = state.nextSequence;
                var cycle = new StorylineState { id = Mint(state, "story"), templateId = template.id,
                    title = template.title, category = template.eyebrow ?? template.lane, lane = template.lane,
                    week = state.week, beatId = DialogueVetoQuestion.Open, status = StorylineStatus.Active,
                    cast = new List<StoryRoleState> { new StoryRoleState { role = DialogueCampaignV1.SpeakerRole, contestantId = request.targetId } } };
                state.storylines.Add(cycle);
                var opening = AddEvent(state, cycle, DialogueVetoQuestion.Open, DialogueVetoQuestion.ContentVersion);
                const int window = Windows.AfterNominations;
                int before = original.windowActions[window];
                string occasion = DialogueRules.VetoQuestionOccasion(state.week, request.targetId);
                var episode = new DialogueEpisodeState { id = cycle.id, npcId = request.targetId, occasionKind = DialogueVetoQuestion.Kind,
                    occasionId = occasion, contentVersion = DialogueVetoQuestion.ContentVersion,
                    week = state.week, window = window, startedPhase = original.phase,
                    startedRevision = revision, startedSequence = sequence, entryEventId = opening.id,
                    entry = new DialogueEntryReceiptState { kind = DialogueRules.ActionEntry, revision = revision,
                        windowActionBefore = before, windowActionAfter = checked(before + 1) } };
                state.dialogue.episodes.Add(episode); expected.episodes.Add(episode.Clone());
                future.Add(episode, DialogueRevisionField.Started); future.Add(episode.entry, DialogueRevisionField.Entry);
                // The veto player answers at once, on the occasion's own keyed coin, from the source as it stood when asked;
                // what they said is the conversation's statement (§4.2 point 1).
                string answer = DialogueRules.VetoAnswer(original, request.targetId, occasion);
                var statement = new DialogueStatementState { id = Mint(state, "dialogue-statement"), eventId = cycle.eventId,
                    pathIndex = 0, speakerId = request.targetId, targetId = state.playerId, answer = answer,
                    conditionKind = DialogueVetoQuestion.ConditionKind, week = state.week, revision = revision,
                    sequence = state.nextSequence - 1, audienceIds = new List<string> { state.playerId } };
                var expectedEpisode = expected.episodes.Single(x => x.id == episode.id);
                episode.statement = statement; expectedEpisode.statement = statement.Clone();
                future.Add(statement, DialogueRevisionField.Statement);
                Step(state, episode, cycle, DialogueVetoQuestion.OpenCase, answer, false, DialogueVetoQuestion.ReplyBeat(answer));
                SpendSocialAction(state);
            }

            private void ChooseVetoQuestion(EpisodeState state)
            {
                var episode = state.dialogue.episodes.Single(x => x.id == originalEpisode.id);
                var cycle = state.storylines.Single(x => x.id == episode.id);
                string option = request.secondTargetId;
                if (option == DialogueVetoQuestion.AskPromise)
                {
                    Require(DialogueVetoQuestion.AnswerOf(cycle.beatId) == DialogueVetoQuestion.Yes, "Choose one of this conversation's options.");
                    // The promise's id is the log sequence of its own step (§4.2 point 2): nothing is minted before that step logs.
                    string refusal = DialogueRules.PromiseRefusal(original, episode.npcId);
                    if (refusal == null)
                        state.deals.Add(new DealState
                        {
                            id = DialogueRules.IfWinId(state.nextSequence), type = DealKind.VetoIf,
                            proposerId = episode.npcId, recipientId = state.playerId, targetId = state.playerId,
                            status = DealStatus.Active, week = state.week, expiresWeek = state.week,
                            trustImpact = DealKind.DefaultTrust(DealKind.VetoIf),
                        });
                    Step(state, episode, cycle, option, refusal == null ? DialogueVetoQuestion.PromiseMade : DialogueVetoQuestion.PromiseRefused, false);
                    string name = Name(state, episode.npcId);
                    Log(state, "dialogue-promise", refusal == null
                        ? name + ": \"You have my word. If I win the veto, I use it on you.\""
                        : name + ": " + refusal, state.playerId, episode.npcId);
                    return;
                }
                string response = option == DialogueVetoQuestion.Thank ? DialogueVetoQuestion.Thanked
                    : option == DialogueVetoQuestion.Leave ? DialogueVetoQuestion.RespectfulExit : null;
                Require(response != null, "Choose one of this conversation's options.");
                Step(state, episode, cycle, option, response, false);
            }

            /// <summary>
            /// The veto question's end (DIALOGUE-PLAN §4.1: "at the result"): a question still open lapses through its own exit as
            /// the competition is played, before anything is scored. Draws nothing. Called only for the competition's own
            /// commands, which the pending command owns while a veto question is open.
            /// </summary>
            internal void LapseVetoQuestion(EpisodeState state)
            {
                Require(Bound(state) && !resolved && !sealedDelta && state.phase == EpisodePhase.Veto
                    && originalEpisode?.occasionKind == DialogueVetoQuestion.Kind, "The veto question's lapse is no longer owned.");
                var episode = state.dialogue.episodes.Single(x => x.id == originalEpisode.id);
                var cycle = state.storylines.Single(x => x.id == episode.id);
                Step(state, episode, cycle, DialogueVetoQuestion.Leave,
                    state.Find(episode.npcId)?.status == ContestantStatus.Active ? DialogueVetoQuestion.OccasionClosed : null, true);
                resolved = true; Seal(state);
            }
        }
    }
}
