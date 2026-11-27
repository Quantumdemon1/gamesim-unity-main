using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Menu slice 2 on the creator: the step marks, My Houseguests as a panel over the step with its
    /// own Close, the card that asks before a saved houseguest replaces a draft with unsaved
    /// changes, and the warning that is a link to what is missing. Nothing here is saved: the marks
    /// and the "unsaved" test live for this visit only.
    /// </summary>
    public sealed partial class CharacterCreator
    {
        private bool libraryOpen;
        private Action pendingReplace;
        private string pendingReplaceName;
        private readonly HashSet<string> visitedSteps = new HashSet<string>();
        private readonly HashSet<string> passedSteps = new HashSet<string>();

        /// <summary>The draft as last loaded, saved or started; null when it holds work never saved.</summary>
        private string baselineKey;

        /// <summary>An unsaved draft taken back to the cast: if the cast screen hands it back, it is still unsaved.</summary>
        private string keptUnsavedKey;

        /// <summary>The houseguest being changed, for the title ("EDIT ROBIN"); null while making a new one.</summary>
        private string editingName;

        /// <summary>The name the footer's link to what is missing has, for tests and focus.</summary>
        public const string ProblemLinkName = "Go to what is missing";

        /// <summary>The replace card, for tests.</summary>
        public const string ReplaceCardName = "Replace draft card";

        /// <summary>Whether My Houseguests is open over the step.</summary>
        public bool LibraryOpen => libraryOpen;

        /// <summary>Whether the card asking to replace the draft is up.</summary>
        public bool AsksToReplace => pendingReplace != null;

        /// <summary>Whether the draft holds changes the player has not saved in My Houseguests.</summary>
        public bool HasUnsavedChanges => baselineKey == null || DraftKey(draft) != baselineKey;

        /// <summary>A step's mark as the row shows it.</summary>
        public CreatorSteps.Mark MarkOf(string step) => CreatorSteps.MarkOf(visitedSteps.Contains(step), passedSteps.Contains(step),
            CreatorSteps.Fails(draft, step, entryMode == EntryMode.Quick));

        private void BeginVisit()
        {
            libraryOpen = false;
            pendingReplace = null;
            pendingReplaceName = null;
            visitedSteps.Clear();
            passedSteps.Clear();
            editingName = null;
            string key = DraftKey(draft);
            baselineKey = keptUnsavedKey != null && key == keptUnsavedKey ? null : key;
        }

        /// <summary>The step on screen is seen; the steps before it in the row are passed.</summary>
        private void MarkVisited()
        {
            var steps = ActivePages;
            int at = Array.IndexOf(steps, studioPage);
            if (at < 0) return;
            visitedSteps.Add(studioPage);
            for (int i = 0; i < at; i++) passedSteps.Add(steps[i]);
        }

        /// <summary>Everything the player can change, the look as the catalogue completes it.</summary>
        private string DraftKey(CharacterDraft value)
        {
            if (value == null) return string.Empty;
            var text = new StringBuilder();
            void Add(string item) { item = item ?? string.Empty; text.Append(item.Length).Append(':').Append(item).Append(';'); }
            Add(value.Name); Add(value.Occupation); Add(value.Hometown); Add(value.Bio); Add(value.Pronouns);
            Add(value.Archetype); Add(value.HomeRoom); Add(value.Motive); Add(value.SourceTemplateId);
            Add(value.Age.ToString(CultureInfo.InvariantCulture));
            Add(value.PreserveStats ? "kept" : "built");
            Add(value.Spent.ToString(CultureInfo.InvariantCulture));
            Add(string.Join(",", value.Traits));
            foreach (string stat in WebTraits.StatNames)
                Add(value.Stats == null ? string.Empty : WebTraits.Get(value.Stats, stat).ToString("R", CultureInfo.InvariantCulture));
            var look = value.Appearance;
            if (look != null && Catalog != null) look = Catalog.Materialize(look);
            Add(look?.ContentKey());
            return text.ToString();
        }

        private void MarkSaved() => baselineKey = DraftKey(draft);

        // ---------------------------------------------------------------- the library panel

        private void OpenLibrary()
        {
            if (libraryOpen) { FocusNamed("New houseguest"); return; }
            libraryOpen = true;
            pendingDeleteProfile = null;
            Rebuild();
            FocusNamed("New houseguest");
        }

        /// <summary>The panel's Close, Escape and B: the step under it comes back as it was.</summary>
        private void CloseLibrary()
        {
            if (!libraryOpen) return;
            libraryOpen = false;
            pendingDeleteProfile = null;
            Rebuild();
            FocusNamed(CreatorSteps.Library);
        }

        // ---------------------------------------------------------------- the replace card

        /// <summary>
        /// Replaces the draft at once when nothing in it would be lost; otherwise asks first, on a card
        /// whose safe answer has the focus.
        /// </summary>
        private void AskToReplace(string name, Action replace)
        {
            if (!HasUnsavedChanges) { replace(); return; }
            pendingReplace = replace;
            pendingReplaceName = name;
            Rebuild();
            FocusNamed(CreatorSteps.KeepCaption);
        }

        private void KeepDraft()
        {
            if (pendingReplace == null) return;
            pendingReplace = null;
            pendingReplaceName = null;
            Rebuild();
            if (libraryOpen) FocusNamed("New houseguest");
        }

        private void ReplaceDraft()
        {
            var replace = pendingReplace;
            pendingReplace = null;
            pendingReplaceName = null;
            replace?.Invoke();
        }

        private void BuildReplaceCard(RectTransform scrim)
        {
            var dim = HudPrimitives.Fill(ReplaceCardName, scrim, new Color(0f, 0f, 0f, .62f), 1);
            Stretch(dim);
            dim.GetComponent<Image>().raycastTarget = true;
            const float width = 700f, height = 290f;
            var card = Panel("Replace draft", dim, new Rect((frame.x - width) * .5f, (frame.y - height) * .5f, width, height), true);
            var question = Words(card, CreatorSteps.ReplaceQuestion(pendingReplaceName), 26f, Color.white, new Rect(32f, 30f, width - 64f, 76f),
                TextAlignmentOptions.TopLeft, "Replace question");
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) question.font = bold;
            question.textWrappingMode = TextWrappingModes.Normal;
            question.enableAutoSizing = true; question.fontSizeMax = 26f; question.fontSizeMin = 18f;
            question.overflowMode = TextOverflowModes.Ellipsis;
            Words(card, CreatorSteps.ReplaceWarning, 16f, UiTheme.Muted, new Rect(32f, 116f, width - 64f, 48f), name: "Replace warning")
                .textWrappingMode = TextWrappingModes.Normal;
            // The safe answer at the right, where the way on stands, with the focus; replacing is the
            // red one: it throws the draft's unsaved changes away.
            const float buttonWidth = 260f, buttonHeight = 64f;
            var replace = Pill(card, CreatorSteps.ReplaceCaption, new Rect(32f, height - buttonHeight - 28f, buttonWidth, buttonHeight), Tone.Danger, ReplaceDraft, size: 18f);
            replace.name = CreatorSteps.ReplaceCaption;
            var keep = Pill(card, CreatorSteps.KeepCaption, new Rect(width - 32f - buttonWidth, height - buttonHeight - 28f, buttonWidth, buttonHeight), Tone.Secondary, KeepDraft, size: 18f);
            keep.name = CreatorSteps.KeepCaption;
            // The keyboard and the pad stay on the card while it asks.
            replace.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = keep };
            keep.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = replace };
        }

        // ---------------------------------------------------------------- the step row's marks

        /// <summary>
        /// A step tab's number at its left and its mark at its right: a tick for done, a warning sign
        /// for needs attention, an empty ring for not visited yet - each its own shape and colour, with
        /// its words beside it where the tab has room and as its text always. The caption stays the
        /// step's name, the tab's first text.
        /// </summary>
        private void StepDecorations(Button tab, int number, CreatorSteps.Mark mark, bool lit, float tabWidth)
        {
            var box = (RectTransform)tab.transform;
            var label = box.Find("Label").GetComponent<TMP_Text>();

            var disc = HudPrimitives.Disc("Step number", box, lit ? new Color(1f, 1f, 1f, .22f)
                : new Color(UiTheme.AccentDeep.r, UiTheme.AccentDeep.g, UiTheme.AccentDeep.b, .8f));
            disc.anchorMin = disc.anchorMax = new Vector2(0f, .5f); disc.pivot = new Vector2(0f, .5f);
            disc.sizeDelta = new Vector2(32f, 32f); disc.anchoredPosition = new Vector2(12f, 0f);
            disc.GetComponent<Image>().raycastTarget = false;
            var digit = HudPrimitives.Label("Step number text", disc, 17f, Color.white, TextAlignmentOptions.Center);
            digit.text = number.ToString(CultureInfo.InvariantCulture);
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) digit.font = bold;
            Stretch(digit.rectTransform);

            var shape = new GameObject("Step mark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            shape.rectTransform.SetParent(box, false);
            shape.sprite = MarkSprite(mark);
            shape.color = MarkColour(mark);
            shape.preserveAspect = true; shape.raycastTarget = false;
            shape.rectTransform.anchorMin = shape.rectTransform.anchorMax = new Vector2(1f, .5f);
            shape.rectTransform.pivot = new Vector2(1f, .5f);
            shape.rectTransform.sizeDelta = new Vector2(22f, 22f);
            shape.rectTransform.anchoredPosition = new Vector2(-14f, 0f);

            var word = HudPrimitives.Label("Step mark word", box, 14f, lit ? Color.white : MarkColour(mark), TextAlignmentOptions.MidlineRight);
            word.text = Localisation.Text(CreatorSteps.Word(mark));
            word.textWrappingMode = TextWrappingModes.NoWrap;
            float wordWidth = Mathf.Ceil(word.GetPreferredValues(word.text).x) + 2f;
            word.rectTransform.anchorMin = new Vector2(1f, 0f); word.rectTransform.anchorMax = new Vector2(1f, 1f);
            word.rectTransform.pivot = new Vector2(1f, .5f);
            word.rectTransform.sizeDelta = new Vector2(wordWidth, 0f);
            word.rectTransform.anchoredPosition = new Vector2(-14f - 22f - 6f, 0f);

            label.alignment = TextAlignmentOptions.MidlineLeft;
            float left = 12f + 32f + 12f, rightWithWord = 14f + 22f + 6f + wordWidth + 10f, rightBare = 14f + 22f + 10f;
            float captionWidth = label.GetPreferredValues(label.text).x;
            // The words show where the step's name still fits whole beside them; the shape always does.
            bool room = left + captionWidth + rightWithWord <= tabWidth;
            word.enabled = room;
            label.rectTransform.offsetMin = new Vector2(left, label.rectTransform.offsetMin.y);
            label.rectTransform.offsetMax = new Vector2(-(room ? rightWithWord : rightBare), label.rectTransform.offsetMax.y);
        }

        private static Sprite MarkSprite(CreatorSteps.Mark mark) =>
            mark == CreatorSteps.Mark.Done ? UiTheme.Pack(PackArt.KitIconCheck)
            : mark == CreatorSteps.Mark.NeedsAttention ? UiTheme.Pack(PackArt.KitIconWarning)
            : UiTheme.Ring();

        private static Color MarkColour(CreatorSteps.Mark mark) =>
            mark == CreatorSteps.Mark.Done ? UiTheme.Positive : mark == CreatorSteps.Mark.NeedsAttention ? UiTheme.Warning : UiTheme.Muted;

        // ---------------------------------------------------------------- the warning that is a link

        /// <summary>"Name needed - go to Identity": pressing it goes to the step with the cursor in the field.</summary>
        private void ProblemLink(RectTransform footer, CreatorSteps.Problem problem, Rect r)
        {
            var box = HudPrimitives.Fill(ProblemLinkName, footer, new Color(UiTheme.GlassFill.r, UiTheme.GlassFill.g, UiTheme.GlassFill.b, .55f), 10);
            Place(box, r.x, r.y, r.width, r.height);
            var image = box.GetComponent<Image>(); image.raycastTarget = true;
            UiTheme.AddBorder(box, 10, new Color(UiTheme.Warning.r, UiTheme.Warning.g, UiTheme.Warning.b, .7f));
            var label = HudPrimitives.Label("Label", box, 16f, UiTheme.Warning, TextAlignmentOptions.MidlineLeft);
            label.text = Localisation.Text(CreatorSteps.LinkCaption(problem));
            label.fontStyle = FontStyles.Underline;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.enableAutoSizing = true; label.fontSizeMax = 16f; label.fontSizeMin = 14f;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(44f, 4f); label.rectTransform.offsetMax = new Vector2(-12f, -4f);
            // The warning sign beside it: the line is told by its shape and words, not its colour alone.
            Glyph(box, PackArt.KitIconWarning, UiTheme.Warning, new Rect(12f, (r.height - 22f) * .5f, 22f, 22f), "Warning glyph");
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var target = problem;
            button.onClick.AddListener(() => ControlSounds.Press(button, () => GoToProblem(target)));
            Seen(button, 10);
        }

        private void GoToProblem(CreatorSteps.Problem problem)
        {
            if (problem == null) return;
            GoTo(problem.Step);
            if (problem.Field != null) FocusNamed(problem.Field);
        }

        /// <summary>Puts the keyboard on the control of that name, if the screen has it.</summary>
        private void FocusNamed(string name)
        {
            var events = EventSystem.current;
            if (events == null || events.alreadySelecting || name == null) return;
            var target = GetComponentsInChildren<Selectable>().FirstOrDefault(item => item.name == name && item.IsActive() && item.IsInteractable());
            if (target != null) events.SetSelectedGameObject(target.gameObject);
        }
    }
}
