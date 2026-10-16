using System;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeDirector
    {
        private ConversationExchange conversationExchange;
        private string exchangeSession;
        private int exchangeRevision, exchangeStep;
        private CharacterPresentation exchangePlayerBody, exchangeNpcBody;
        private bool acceptedExchangeWasPresented;
        private string conversationNarration;
        private bool commandNotice;
        private string systemNotice;
        private string persistenceNotice;

        /// <summary>
        /// A piece of furniture's line while it is the notice on the status line (D10, S2A-R1): kept
        /// apart from the message, which the house goes on narrating into while the player sits - a
        /// housemate joining the hot tub - so neither the narration nor the line it leaves empty
        /// takes the notice's place, and getting up lets the notice go whatever the message says.
        /// Any newer notice lets go of it as it begins.
        /// </summary>
        private string activityNotice;
        private float saveFeedbackUntil;

        public bool IsConversationExchangePlaying => conversationExchange != null;
        public string ConversationExchangeSpeakerId => conversationExchange == null ? null
            : exchangeStep == 0 ? conversationExchange.PlayerId : conversationExchange.NpcId;

        // A committed action owns one transient exchange. Playback never submits another command.
        private void BeginConversationExchange(EpisodeState before, CommandResult result, EpisodeCommand command)
        {
            CancelConversationExchange(false);
            acceptedExchangeWasPresented = false;
            conversationNarration = null;
            if (!result.accepted)
            {
                lastSocialAction = null;
                standingLineBefore = null;
                lastSocialDelta = 0d;
                return;
            }
            if (focusedNpc == null || blockedRecovery || npcSaveSuspended) return;
            var state = result.state;
            var visible = state.events.Where(e => e.sequence >= before.nextSequence
                && (result.beatsFromSequence <= 0 || e.sequence < result.beatsFromSequence)
                && (e.audienceIds.Count == 0 || e.audienceIds.Contains(state.playerId))
                && !KnownBallots.TellsAnUnknownBallot(state, e)).ToList();
            var outcome = visible.LastOrDefault(e => !EpisodeEngine.IsScaffolding(e.kind));
            if (outcome != null) conversationNarration = EventLine(state, outcome);
            var exchange = ConversationExchange.Create(before, state, command, visible);
            if (exchange == null || exchange.NpcId != focusedNpc.Id) return;
            conversationExchange = exchange;
            acceptedExchangeWasPresented = true;
            exchangeSession = state.sessionId;
            exchangeRevision = state.revision;
            exchangeStep = 0;
            exchangePlayerBody = player != null ? player.GetComponent<CharacterPresentation>() : null;
            exchangeNpcBody = focusedNpc.GetComponent<CharacterPresentation>();
            ApplyConversationSpeaker();
        }

        private bool ExchangeOwns(EpisodeState state) => conversationExchange != null && state != null
            && !blockedRecovery && !npcSaveSuspended && focusedNpc != null
            && focusedNpc.Id == conversationExchange.NpcId
            && state.sessionId == exchangeSession && state.revision == exchangeRevision;

        private bool RenderConversationExchangeIfOpen(EpisodeState state)
        {
            if (conversationExchange == null) return false;
            if (!ExchangeOwns(state)) { CancelConversationExchange(false); return false; }
            string session = exchangeSession;
            int revision = exchangeRevision, step = exchangeStep;
            bool self = step == 0;
            hud.RenderConversationExchange(self ? conversationExchange.PlayerId : conversationExchange.NpcId,
                self ? conversationExchange.PlayerLine : conversationExchange.NpcLine,
                self, conversationExchange.IsDramatic,
                () => AdvanceConversationExchange(session, revision, step),
                () => SkipConversationExchange(session, revision, step));
            if (cameraRig != null) cameraRig.ConversationWindowOffset = hud.ConversationWindowOffset;
            return true;
        }

        private bool ExchangeControlIsCurrent(string session, int revision, int step) =>
            ExchangeOwns(projected) && exchangeSession == session && exchangeRevision == revision && exchangeStep == step;

        private void AdvanceConversationExchange(string session, int revision, int step)
        {
            if (!ExchangeControlIsCurrent(session, revision, step)) return;
            if (exchangeStep == 0) { exchangeStep = 1; ApplyConversationSpeaker(); }
            else CancelConversationExchange(true);
            Render();
        }

        private void SkipConversationExchange(string session, int revision, int step)
        {
            if (!ExchangeControlIsCurrent(session, revision, step)) return;
            CancelConversationExchange(true);
            Render();
        }

        private void ApplyConversationSpeaker()
        {
            bool self = conversationExchange != null && exchangeStep == 0;
            if (exchangePlayerBody != null) { exchangePlayerBody.SetSpeaking(self); exchangePlayerBody.SetTalking(self); }
            if (exchangeNpcBody != null) { exchangeNpcBody.SetSpeaking(!self); exchangeNpcBody.SetTalking(!self); }
        }

        private void CancelConversationExchange(bool resumeConversation)
        {
            if (exchangePlayerBody != null) { exchangePlayerBody.SetSpeaking(false); exchangePlayerBody.SetTalking(false); }
            if (exchangeNpcBody != null)
            {
                exchangeNpcBody.SetSpeaking(resumeConversation);
                exchangeNpcBody.SetTalking(resumeConversation);
            }
            conversationExchange = null;
            exchangeSession = null;
            exchangeStep = 0;
            exchangePlayerBody = exchangeNpcBody = null;
        }

        private void TickConversationPresentation()
        {
            // The house's pose pass runs first. The focused pair then hands over the floor.
            if (conversationExchange != null)
            {
                if (ExchangeOwns(projected)) ApplyConversationSpeaker();
                else CancelConversationExchange(false);
            }
            if (saveFeedbackUntil > 0f && Time.unscaledTime >= saveFeedbackUntil)
            { saveFeedbackUntil = 0f; hud?.HideSaveFeedback(); }
        }

        private void MarkSaveFeedback()
        {
            commandNotice = false;
            systemNotice = null;
            activityNotice = null;
            persistenceNotice = null;
            saveFeedbackUntil = Time.unscaledTime + 4f;
        }

        private void BeginPersistenceNotice(string failure)
        {
            persistenceNotice = failure;
            BeginSystemNotice(failure);
        }

        private void BeginSystemNotice(string displayNotice = null)
        {
            CancelConversationExchange(false);
            acceptedExchangeWasPresented = false;
            conversationNarration = null;
            lastSocialAction = null;
            standingLineBefore = null;
            lastSocialDelta = 0d;
            commandNotice = true;
            systemNotice = displayNotice;
            activityNotice = null;
            saveFeedbackUntil = 0f;
        }

        /// <summary>
        /// Lets go of a notice that only said where the player was - a piece of furniture's line once
        /// they are up - so the phase's own guidance comes back rather than an empty line.
        /// </summary>
        private void EndSystemNotice()
        {
            commandNotice = false;
            systemNotice = null;
            activityNotice = null;
        }

        /// <summary>State-derived guidance; conversation prose and save suffixes have other homes.</summary>
        private string HudStatusMessage(EpisodeState state)
        {
            // A piece of furniture's line comes after a stopped save's notice, as the message did
            // before it was kept apart, and before whatever the message says while they sit.
            if (blockedRecovery || commandNotice)
                return NarrativeDisplay.Render(state, systemNotice ?? (npcSaveSuspended ? persistenceNotice : null)
                    ?? (commandNotice ? activityNotice : null) ?? WithoutSaveSuffix(message));
            if (npcSaveSuspended)
                return persistenceNotice ?? WithoutSaveSuffix(message);
            if (npcWorldFailed)
                return "Housemate activity is paused: " + npcWorldFailure + " Your episode and saved history remain available.";
            if (focusedNpc != null) return string.Empty;
            string guidance = PhaseLine(state.phase, state.week);
            if (state.Find(state.playerId)?.status == ContestantStatus.Active
                && (state.phase == EpisodePhase.Social || state.phase == EpisodePhase.Campaign
                    || (EpisodeEngine.WeekRulesOn(state) && EpisodeEngine.Window(state) != Windows.None)))
                return guidance + "  ·  " + ActionsLeft(state) + " remaining";
            return guidance;
        }

        private static string WithoutSaveSuffix(string line)
        {
            const string suffix = "  ·  Saved locally.";
            while (!string.IsNullOrEmpty(line) && line.EndsWith(suffix, StringComparison.Ordinal))
                line = line.Substring(0, line.Length - suffix.Length);
            return line ?? string.Empty;
        }
    }
}
