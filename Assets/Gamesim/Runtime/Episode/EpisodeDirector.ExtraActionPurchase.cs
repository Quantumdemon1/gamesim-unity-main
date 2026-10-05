using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeDirector
    {
        public const string BurnBridgeTitle = "CHOOSE WHO PAYS", BurnBridgeCancelCaption = "Cancel action purchase";
        public static string BurnBridgeTargetCaption(string name) => "Buy an action at " + name + "'s expense";
        public static string BurnBridgeCostCopy => "Gain one action by burning one bridge. Base cost: "
            + Mathf.Abs((int)WebSocialVocabulary.BurnOneCost)
            + " goodwill with your chosen housemate. Nothing is spent until you choose.";

        // View state only, with a distinct identity for each opening. A retained callback from an
        // earlier opening cannot spend after cancel/reopen, even at the same simulation revision.
        private ExtraActionPurchaseChoice extraActionChoice;
        private EpisodeState extraActionOrigin;
        private long extraActionGeneration;
        private bool extraActionFocusCancel;
        public bool IsChoosingActionPurchase => extraActionChoice != null;

        private void ForgetActionPurchase()
        {
            extraActionChoice = null;
            extraActionOrigin = null;
            extraActionFocusCancel = false;
        }

        private void OpenActionPurchase(EpisodeState origin)
        {
            if (this == null || !isActiveAndEnabled || !phaseOpen || challengeActive || !IsCurrentDiaryRevision(origin)) return;
            var choice = ExtraActionPurchaseChoice.Open(origin);
            if (choice == null) return;
            extraActionChoice = choice;
            extraActionOrigin = origin;
            extraActionGeneration = loadGeneration;
            extraActionFocusCancel = true;
            Render();
            hud.HoldPointerOffPanel(EpisodeHud.PointerHoldSeconds);
        }

        private void CancelActionPurchase(ExtraActionPurchaseChoice choice)
        {
            if (this == null || !isActiveAndEnabled || !ReferenceEquals(extraActionChoice, choice)) return;
            ForgetActionPurchase();
            Render();
            hud.HoldPointerOffPanel(EpisodeHud.PointerHoldSeconds);
            hud.FocusWhenWired(EpisodeHud.BuyBurnOneCaption);
        }

        private void ChooseActionPurchase(ExtraActionPurchaseChoice choice, string target)
        {
            if (this == null || !isActiveAndEnabled || !phaseOpen || challengeActive || !ReferenceEquals(extraActionChoice, choice)
                || extraActionGeneration != loadGeneration
                || !IsCurrentDiaryRevision(extraActionOrigin) || !choice.CanChoose(projected, target)) return;
            var origin = extraActionOrigin;
            // Consume view authority before the durable command, including a refused disk write.
            // Retry is a fresh deliberate choice; no UI preview directly mutates the simulation.
            ForgetActionPurchase();
            Commit(origin, EpisodeCommandKind.BuyActionPoint, target, text: WebSocialVocabulary.BurnOne);
            hud.HoldPointerOffPanel(EpisodeHud.PointerHoldSeconds);
            hud.FocusWhenWired(EpisodeHud.BuyBurnOneCaption);
        }

        private bool RenderActionPurchase(EpisodeState state)
        {
            var choice = extraActionChoice;
            if (choice == null) return false;
            if (!phaseOpen || challengeActive || extraActionGeneration != loadGeneration
                || !choice.IsCurrent(state) || !IsCurrentDiaryRevision(extraActionOrigin))
            {
                ForgetActionPurchase();
                return false;
            }
            // The common scrollable stage works for the board, a houseguest's screen, campaign
            // and legacy/final-three free time. Cancel stays pinned even with fifteen candidates.
            hud.SetActivityLayout(EpisodeHud.ActivityLayout.Stage);
            hud.ScreenHead(BurnBridgeTitle, "ONE MORE ACTION", BurnBridgeCostCopy);
            foreach (string target in choice.Targets)
            {
                if (!choice.CanChoose(state, target)) continue;
                hud.ActionFor(target, BurnBridgeTargetCaption(state.Find(target).name), () => ChooseActionPurchase(choice, target));
            }
            hud.PinnedAction(BurnBridgeCancelCaption, () => CancelActionPurchase(choice));
            hud.KeepPointerHold();
            if (extraActionFocusCancel) hud.FocusWhenWired(BurnBridgeCancelCaption);
            else hud.KeepFocusAsked();
            extraActionFocusCancel = false;
            return true;
        }
    }
}
