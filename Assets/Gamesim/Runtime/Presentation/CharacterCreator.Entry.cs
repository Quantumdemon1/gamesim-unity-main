using System;
using System.Linq;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Presentation
{
    public sealed partial class CharacterCreator
    {
        public enum EntryMode { Quick, Detailed }

        public const string QuickCaption = "Quick";
        public const string DetailedCaption = "Detailed";
        public const string QuickPageCaption = "Quick setup";
        private static readonly string[] QuickPages = { QuickPageCaption, "My Houseguests", "Review" };
        private EntryMode entryMode = EntryMode.Detailed;
        private string lastDetailedPage = "Appearance";
        private RectTransform quickControls;
        private float quickScrollY;

        public EntryMode Mode => entryMode;
        private string[] ActivePages => entryMode == EntryMode.Quick ? QuickPages : Pages;
        private string DefaultAppearancePage => entryMode == EntryMode.Quick ? QuickPageCaption : "Appearance";
        private static bool SharedPage(string page) => page == "My Houseguests" || page == "Review";

        // A presentation change on the same draft: no recipe materialization, history reset or callback replacement.
        private void SwitchMode(EntryMode mode)
        {
            if (entryMode == mode) return;
            if (entryMode == EntryMode.Detailed && !SharedPage(studioPage)) lastDetailedPage = studioPage;
            entryMode = mode;
            if (!SharedPage(studioPage)) studioPage = mode == EntryMode.Quick ? QuickPageCaption : lastDetailedPage;
            if (studioPage != "Appearance") comparingOriginal = false;
            Rebuild();
        }

        // Explicit library Edit/Rename actions open the requested detailed page even from Quick.
        private void SelectPage(string page)
        {
            if (!SharedPage(page) && Array.IndexOf(Pages, page) >= 0)
            {
                entryMode = EntryMode.Detailed;
                lastDetailedPage = page;
            }
            studioPage = page;
        }

        private void BuildEntryModes(RectTransform scrim)
        {
            var row = new GameObject("Creator modes", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(scrim, false);
            PlaceTop(row, 0f, 174f, 340f, 42f);
            Pill(row, QuickCaption, new Rect(0f, 0f, 164f, 42f), entryMode == EntryMode.Quick ? Tone.Selected : Tone.Secondary,
                () => SwitchMode(EntryMode.Quick), size: 16f);
            Pill(row, DetailedCaption, new Rect(176f, 0f, 164f, 42f), entryMode == EntryMode.Detailed ? Tone.Selected : Tone.Secondary,
                () => SwitchMode(EntryMode.Detailed), size: 16f);
        }

        private void BuildQuickPage(RectTransform scrim)
        {
            var area = PageArea;
            float form = Mathf.Min(880f, area.width * .57f);
            var box = Panel("Quick setup form", scrim, new Rect(area.x, area.y, form, area.height), true);
            BuildPreviewStage(scrim, new Rect(area.x + form + Gap, area.y, area.width - form - Gap, area.height), false);
            float width = form - 48f;
            float top = SectionHeading(box, 24f, 20f, width, PackArt.KitIconPerson, "QUICK SETUP");
            Words(box, "Choose a starting look and body, then introduce yourself. Detailed lets you edit features, outfits and personality.", 15f,
                UiTheme.Muted, new Rect(24f, top, width, 48f)).textWrappingMode = TMPro.TextWrappingModes.Normal;
            top += 54f;
            undoAppearanceButton = Pill(box, "Undo", new Rect(24f, top, 120f, 40f), Tone.Secondary, UndoAppearance, size: 15f);
            redoAppearanceButton = Pill(box, "Redo", new Rect(154f, top, 120f, 40f), Tone.Secondary, RedoAppearance, size: 15f);
            undoAppearanceButton.interactable = appearanceUndo.Count > 0;
            redoAppearanceButton.interactable = appearanceRedo.Count > 0;
            top += 52f;
            quickControls = ScrollArea(box, "Quick fields", new Rect(12f, top, form - 24f, area.height - top - 12f), 520f);
            float inner = form - 48f;
            Field(quickControls, "Name", draft.Name, CharacterDraft.NameLimit, false, new Rect(12f, 0f, inner, 76f), value => draft.Name = value);
            AgeRow(quickControls, new Rect(12f, 88f, inner, 80f));
            PronounRow(quickControls, new Rect(12f, 180f, inner, 100f));
            // Use the same preset controls and whole-look Undo contract as the detailed studio.
            studioControls = quickControls;
            studioWidth = form - 24f;
            studioCursor = 296f;
            StartingLooks();
            if (Catalog != null)
            {
                BodyChoices();
                StudioText("Body choice is independent of pronouns. Undo restores the body and clothing together.", 48f);
                if (!string.IsNullOrEmpty(appearanceNotice))
                    StudioText(appearanceNotice, 30f + appearanceNotice.Count(value => value == '\n') * 22f);
            }
            else StudioText("This installation offers complete preset bodies. Choose a starting look to change your body; pronouns remain yours.", 60f);
            quickControls.sizeDelta = new Vector2(0f, studioCursor + 16f);
            quickControls.anchoredPosition = new Vector2(0f,
                Mathf.Clamp(quickScrollY, 0f, Mathf.Max(0f, quickControls.rect.height - (area.height - top - 12f))));
            studioControls = null;
        }
    }
}
