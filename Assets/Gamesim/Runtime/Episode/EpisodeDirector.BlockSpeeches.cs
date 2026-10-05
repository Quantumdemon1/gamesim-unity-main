using System;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeDirector
    {
        public const string BlockSpeechSaveLine = "Deliver your speech and Say nothing save immediately. Your speech addresses the house; leaving this view does not deliver it.";
        public const string BlockSpeechInfluenceLine = "Choose how to put your speech. This approach, not a hidden interpretation of your typed words, may slightly sway undecided NPC voters who hear it. Their response is uncertain; no vote is promised and your own ballot remains yours.";

        private object blockSpeechView;
        private string blockSpeechSession, blockSpeechSpeaker, blockSpeechDraft = "", blockSpeechApproach = LobbyApproach.Emotional;
        private int blockSpeechWeek;

        private static bool BlockSpeechPending(EpisodeState state) => state != null
            && state.phase == EpisodePhase.Eviction && state.evictionStage == EvictionStage.Speeches
            && state.Find(state.playerId)?.status == ContestantStatus.Active && state.nominees.Contains(state.playerId)
            && !state.evictionSpeeches.Any(speech => speech.speakerId == state.playerId);

        // A block speech is its own draft, not the finale's. Closing a panel, walking between the
        // station and diary, or rebuilding a view preserves it; a different season/week does not.
        private void BindBlockSpeechDraft(EpisodeState state)
        {
            if (state == null || (blockSpeechSession == state.sessionId && blockSpeechSpeaker == state.playerId
                && blockSpeechWeek == state.week)) return;
            blockSpeechSession = state.sessionId; blockSpeechSpeaker = state.playerId; blockSpeechWeek = state.week;
            blockSpeechDraft = ""; blockSpeechApproach = LobbyApproach.Emotional;
        }

        private bool BlockSpeechViewCurrent(EpisodeState state, object view, long generation, bool privateRoom) =>
            this != null && isActiveAndEnabled && !blockedRecovery && !challengeActive
            && ReferenceEquals(blockSpeechView, view) && loadGeneration == generation
            && IsCurrentDiaryRevision(state) && state.week == projected.week && BlockSpeechPending(projected)
            && (privateRoom ? diaryOpen && CanUseDiary && IsDiarySettled : phaseOpen && !diaryOpen);

        private void CommitBlockSpeech(EpisodeState state, string text, string approach)
        {
            string key = BlockSpeeches.RulesOn(state)
                ? string.IsNullOrWhiteSpace(text) ? BlockSpeeches.Quiet : approach : null;
            Commit(state, EpisodeCommandKind.SubmitEvictionSpeech, second: key, text: text);
        }

        private void RenderBlockSpeech(EpisodeState state, bool privateRoom)
        {
            BindBlockSpeechDraft(state);
            object view = blockSpeechView = new object();
            long generation = loadGeneration;
            hud.Heading("YOUR SPEECH FROM THE BLOCK");
            hud.Paragraph("The house votes after this. Say what you want them to have heard, or say nothing.");
            if (!privateRoom) hud.Aside(BlockSpeechSaveLine);
            if (BlockSpeeches.RulesOn(state))
            {
                hud.Paragraph(BlockSpeechInfluenceLine);
                hud.FilterRow(EpisodeHud.BlockSpeechApproachesName, BlockSpeeches.Approaches.Select(key =>
                {
                    string selected = key;
                    return (BlockSpeeches.Label(key), key == blockSpeechApproach, (Action)(() =>
                    {
                        if (!BlockSpeechViewCurrent(state, view, generation, privateRoom)) return;
                        blockSpeechView = null;
                        blockSpeechApproach = selected;
                        Render();
                    }));
                }).ToList());
                hud.Paragraph("Selected approach: " + BlockSpeeches.Label(blockSpeechApproach));
                hud.Aside(BlockSpeeches.Description(blockSpeechApproach));
            }
            void Deliver(bool quiet)
            {
                if (!BlockSpeechViewCurrent(state, view, generation, privateRoom)) return;
                blockSpeechView = null; // Even a refused write retires this activation before it tries to save.
                CommitBlockSpeech(state, quiet ? "" : blockSpeechDraft, blockSpeechApproach);
            }
            hud.EvictionSpeech(blockSpeechDraft, value =>
            {
                if (BlockSpeechViewCurrent(state, view, generation, privateRoom)) blockSpeechDraft = value;
            }, () => Deliver(false), () => Deliver(true));
        }

        private bool RenderBlockSpeechReadback(EpisodeState state)
        {
            if (state == null || state.phase != EpisodePhase.Eviction) return false;
            var speeches = state.evictionSpeeches.Where(speech => speech.week == state.week).ToArray();
            if (speeches.Length == 0) return false;
            hud.Heading(EpisodeHud.BlockSpeechReadbackHeading);
            hud.Aside("The public words on the record. Hearing a speech does not reveal anyone's private vote.");
            foreach (var speech in speeches)
            {
                string approach = BlockSpeeches.Approach(state, speech);
                hud.Heading(state.Find(speech.speakerId)?.name ?? "Houseguest");
                if (approach != null) hud.Paragraph("Approach: " + BlockSpeeches.Label(approach));
                hud.Paragraph(BlockSpeeches.ReceiptText(speech));
            }
            return true;
        }
    }
}
