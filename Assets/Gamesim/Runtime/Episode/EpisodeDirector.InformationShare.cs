using System;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeDirector
    {
        public const string ShareInformationCaption = "Share something I know", ShareInformationTitle = "CHOOSE WHAT TO SHARE",
            ShareInformationCancelCaption = "Cancel sharing", ShareInformationConfirmCaption = "Share this memory · one action",
            ShareInformationBackCaption = "Choose another memory", ShareInformationNextCaption = "Next memories",
            ShareInformationPreviousCaption = "Previous memories", ShareMemoryTextName = "Memory to share",
            ShareReceiptTextName = "Memory the recipient will keep";
        public const int ShareInformationPageSize = 6;
        public static string ShareMemoryReviewCaption(int number, string subject) => "Review memory " + number + " · about " + subject;

        private InformationShareChoice informationShareChoice;
        private EpisodeState informationShareOrigin;
        private long informationShareGeneration;
        private object informationShareControls, informationShareOpener;
        private string informationShareReview;
        private int informationSharePage;
        private bool informationShareFocusCancel;
        public bool IsChoosingSharedInformation => informationShareChoice != null;

        private void ForgetInformationShare()
        {
            informationShareChoice = null; informationShareOrigin = null; informationShareReview = null;
            informationShareControls = null; informationShareOpener = null;
            informationSharePage = 0; informationShareFocusCancel = false;
        }

        private void InformationShareRow(EpisodeState state, ContestantState npc)
        {
            if (!EpisodeEngine.EconomyRulesOn(state))
            {
                hud.Tag(hud.Action(ShareInformationCaption, () => Commit(state, EpisodeCommandKind.ShareInformation, npc.id)), Category(EpisodeCommandKind.ShareInformation));
                return;
            }
            object opener = new object();
            informationShareOpener = opener;
            long generation = loadGeneration;
            hud.Tag(hud.Action(ShareInformationCaption, () =>
            {
                if (this == null || !isActiveAndEnabled || !ReferenceEquals(informationShareOpener, opener)
                    || generation != loadGeneration || focusedNpc == null || focusedNpc.Id != npc.id || !IsCurrentDiaryRevision(state)) return;
                var choice = InformationShareChoice.Open(state, npc.id);
                if (choice == null) return;
                informationShareChoice = choice; informationShareOrigin = state; informationShareGeneration = generation;
                informationShareReview = null; informationSharePage = 0; informationShareFocusCancel = true;
                Render(); hud.HoldPointerOffPanel(EpisodeHud.PointerHoldSeconds);
            }), Category(EpisodeCommandKind.ShareInformation));
        }

        private bool CurrentInformationShare(InformationShareChoice choice, object controls) => this != null && isActiveAndEnabled
            && choice != null && ReferenceEquals(choice, informationShareChoice) && ReferenceEquals(controls, informationShareControls)
            && informationShareGeneration == loadGeneration && IsCurrentDiaryRevision(informationShareOrigin)
            && focusedNpc != null && focusedNpc.Id == choice.RecipientId && choice.IsCurrent(projected);

        private void RefreshInformationShare()
        {
            informationShareFocusCancel = true;
            Render(); hud.HoldPointerOffPanel(EpisodeHud.PointerHoldSeconds);
        }

        private bool RenderInformationShare(EpisodeState state)
        {
            var choice = informationShareChoice;
            if (choice == null) return false;
            if (informationShareGeneration != loadGeneration || !IsCurrentDiaryRevision(informationShareOrigin)
                || focusedNpc == null || focusedNpc.Id != choice.RecipientId || !choice.IsCurrent(state))
            { ForgetInformationShare(); return false; }
            object controls = new object();
            informationShareControls = controls;
            hud.SetActivityLayout(EpisodeHud.ActivityLayout.Stage);
            hud.ScreenHead(ShareInformationTitle, "WITH " + state.Find(choice.RecipientId).name.ToUpperInvariant(),
                "Sharing costs one action. Review what they will learn, then confirm. Nothing is spent while you choose.");
            InformationShareChoice.Option selected = null;
            foreach (var option in choice.Options)
                if (option.Reference == informationShareReview) { selected = option; break; }
            if (selected == null)
            {
                int start = informationSharePage * ShareInformationPageSize;
                int end = Math.Min(start + ShareInformationPageSize, choice.Options.Count);
                if (choice.Options.Count == 0) hud.Paragraph("You have no eligible personal memories to share with them yet.");
                else hud.Aside("Memories " + (start + 1) + "–" + end + " of " + choice.Options.Count + " · newest first");
                for (int i = start; i < end; i++)
                {
                    var option = choice.Options[i];
                    hud.Action(ShareMemoryReviewCaption(i + 1, state.Find(option.SubjectId).name), () =>
                    {
                        if (!CurrentInformationShare(choice, controls) || !choice.CanChoose(projected, option.Reference)) return;
                        informationShareReview = option.Reference; RefreshInformationShare();
                    });
                    string preview = option.Text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
                    if (preview.Length > 140)
                    {
                        int length = char.IsHighSurrogate(preview[139]) && char.IsLowSurrogate(preview[140]) ? 139 : 140;
                        preview = preview.Substring(0, length) + "…";
                    }
                    hud.Aside("Week " + option.Week + " · " + preview);
                }
                if (start > 0) hud.Action(ShareInformationPreviousCaption, () =>
                {
                    if (!CurrentInformationShare(choice, controls)) return;
                    informationSharePage--; RefreshInformationShare();
                });
                if (end < choice.Options.Count) hud.Action(ShareInformationNextCaption, () =>
                {
                    if (!CurrentInformationShare(choice, controls)) return;
                    informationSharePage++; RefreshInformationShare();
                });
            }
            else
            {
                var selectedOption = selected;
                hud.Heading("YOUR MEMORY · WEEK " + selected.Week + " · ABOUT " + state.Find(selected.SubjectId).name.ToUpperInvariant());
                hud.NamedParagraph(ShareMemoryTextName, selected.Text);
                hud.Heading("WHAT THEY WILL REMEMBER");
                if (selected.Shortened) hud.Aside("This long memory is shared as the exact excerpt below. Your original memory is unchanged.");
                hud.NamedParagraph(ShareReceiptTextName, selected.ReceivedText);
                hud.Action(ShareInformationConfirmCaption, () =>
                {
                    if (!CurrentInformationShare(choice, controls) || informationShareReview != selectedOption.Reference
                        || !choice.CanChoose(projected, selectedOption.Reference)) return;
                    var origin = informationShareOrigin;
                    ForgetInformationShare();
                    Commit(origin, EpisodeCommandKind.ShareInformation, choice.RecipientId, selectedOption.Reference);
                    hud.HoldPointerOffPanel(EpisodeHud.PointerHoldSeconds); hud.FocusWhenWired(ShareInformationCaption);
                });
                hud.Action(ShareInformationBackCaption, () =>
                {
                    if (!CurrentInformationShare(choice, controls)) return;
                    informationShareReview = null; RefreshInformationShare();
                });
            }
            hud.PinnedAction(ShareInformationCancelCaption, () =>
            {
                if (!CurrentInformationShare(choice, controls)) return;
                ForgetInformationShare(); Render();
                hud.HoldPointerOffPanel(EpisodeHud.PointerHoldSeconds); hud.FocusWhenWired(ShareInformationCaption);
            });
            hud.KeepPointerHold();
            if (informationShareFocusCancel) hud.FocusWhenWired(ShareInformationCancelCaption);
            else hud.KeepFocusAsked();
            informationShareFocusCancel = false;
            return true;
        }
    }
}
