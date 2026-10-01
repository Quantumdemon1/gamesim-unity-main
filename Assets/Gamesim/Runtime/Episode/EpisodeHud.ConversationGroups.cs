using System;
using System.Collections.Generic;
using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The conversation's rows grouped by what they are for, and the person picker that folds a verb's
    /// people under one row (ACTIONS-DEALS-ALLIANCES-PLAN V5).
    ///
    /// <para>A conversation drew four rows for every other houseguest - vent, lie, whisper, call out -
    /// and a target agreement for each of them besides: fifty controls in a house of eight and ninety in
    /// a house of sixteen, in a column about six hundred units tall. Each of those verbs is now one row,
    /// and pressing it opens its people beneath it as compact buttons, one verb at a time.</para>
    ///
    /// <para>Every person keeps the caption it always had. A button in the picker carries the full words
    /// - "Vent about Jo", not "Jo" - because those words are how a test, the audited walk and a screen
    /// reader find it, and a picker whose buttons said only a name would be six controls all called
    /// something else.</para>
    ///
    /// <para>The picker is a scope, not a list the director builds: every <see cref="ActionFor"/> row
    /// drawn between <see cref="BeginPersonPicker"/> and <see cref="EndPersonPicker"/> whose caption the
    /// scope takes becomes one of its people. That is what lets the deal table's target agreements fold
    /// without a line of the deal table changing: the table is drawn inside a scope that takes only
    /// those rows, and its odds still come from the table.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>A group's head is named this and its title, so a test can find where each group starts.</summary>
        public const string ConversationGroupPrefix = "Conversation group · ";
        /// <summary>The open picker's grid is named this and the verb row's caption.</summary>
        public const string PersonPickerPrefix = "Person picker · ";
        /// <summary>What a row the closed picker folded away is handed back as: never shown, never pressed.</summary>
        public const string FoldedRowsName = "Folded person rows";

        // A person's button at the resting text size: the face, a gap, then the words.
        private const float PickerFace = 34f, PickerGap = 8f, PickerPad = 9f, PickerCellMin = 200f;
        private const int PickerCaptionSize = 15, PickerTagSize = 12;

        /// <summary>The picker being drawn, between its Begin and its End; null outside one.</summary>
        private PickerScope picker;

        private sealed class PickerScope
        {
            public string Verb, Tag;
            public bool Open;
            public Action Toggle, Chosen;
            public Func<string, bool> Takes;
            public Button Row, Folded;
            public RectTransform Grid, Content;
            public int Columns;
            public float CellWidth, TextWidth;
            public readonly List<Button> Cells = new List<Button>();
            public readonly Dictionary<Button, TMP_Text> CaptionOf = new Dictionary<Button, TMP_Text>();
            public readonly Dictionary<Button, TMP_Text> TagOf = new Dictionary<Button, TMP_Text>();
        }

        /// <summary>
        /// A group's head: its glyph, its name in the heading blue, and a line saying what the group is
        /// for. The line wraps rather than clips, and the head is as tall as what it says at the text
        /// size the player chose.
        /// </summary>
        public RectTransform ConversationGroup(string title, string glyph, string line)
        {
            if (content == null) return null;
            float s = FontScale, width = ContentWidth();
            var row = new GameObject(ConversationGroupPrefix + title, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            // A hairline over the head, so the groups read as four blocks rather than one list.
            var rule = Panel("Group rule", row, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .8f), 0);
            Anchor(rule, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(width, Mathf.Max(1f, s)));
            rule.GetComponent<Image>().raycastTarget = false;

            float top = 8f * s, indent = 4f;
            var mark = HudPrimitives.Glyph("Group mark", row, glyph, Accent, new Vector2(2f, -top), 22f * s);
            if (mark != null) indent = 32f * s;
            // Boxes at 1.3 times their text: Inter draws nothing in a box under 1.21 of it.
            float titleHeight = Mathf.Ceil(17f * s * 1.3f);
            var name = FixedText(row, title, 17, UiTheme.Heading, new Vector2(indent, -top), new Vector2(width - indent, titleHeight));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            name.characterSpacing = 4f;
            float height = top + titleHeight;
            if (!string.IsNullOrEmpty(line))
            {
                var words = NewText(row, line, 14, UiTheme.Muted);
                words.alignment = TextAlignmentOptions.TopLeft;
                float wordsHeight = Mathf.Max(Mathf.Ceil(14f * s * 1.3f), Mathf.Ceil(words.GetPreferredValues(words.text, width - indent, 0f).y) + 4f * s);
                Anchor(words.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(indent, -height), new Vector2(width - indent, wordsHeight));
                height += wordsHeight;
            }
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height + 2f * s;
            return row;
        }

        /// <summary>
        /// Opens a person picker. Every <see cref="ActionFor"/> row until <see cref="EndPersonPicker"/>
        /// whose caption <paramref name="takes"/> accepts - every one, when it is null - is one of its
        /// people. The first of them draws the verb row in its place, captioned
        /// <paramref name="verbCaption"/> and tagged <paramref name="tag"/>, which runs
        /// <paramref name="toggle"/> when pressed; while <paramref name="open"/>, the people follow it as
        /// compact buttons, and pressing one runs <paramref name="chosen"/> before its own action.
        /// A verb with nobody to aim it at draws nothing at all.
        /// </summary>
        public void BeginPersonPicker(string verbCaption, string tag, bool open, Action toggle, Action chosen, Func<string, bool> takes = null)
        {
            EndPersonPicker();
            if (content == null) return;
            picker = new PickerScope { Verb = verbCaption, Tag = tag, Open = open, Toggle = toggle, Chosen = chosen, Takes = takes, Content = content };
        }

        /// <summary>Closes the picker begun last: its people are laid out at one height, the tallest's.</summary>
        public void EndPersonPicker()
        {
            var scope = picker;
            picker = null;
            if (scope == null || scope.Grid == null || scope.Cells.Count == 0) return;
            float s = FontScale, gap = PickerGap * s, tallest = 0f;
            foreach (var cell in scope.Cells)
            {
                if (!scope.CaptionOf.TryGetValue(cell, out var caption) || caption == null) continue;
                float tag = 0f;
                if (scope.TagOf.TryGetValue(cell, out var line) && line != null)
                {
                    // The tag line stands on the cell's foot, as tall as what it says, and the words
                    // stand over it.
                    float lineHeight = Mathf.Max(Mathf.Ceil(line.fontSize * 1.3f),
                        Mathf.Ceil(line.GetPreferredValues(line.text, scope.TextWidth, 0f).y) + 2f * s);
                    var foot = line.rectTransform;
                    foot.offsetMax = new Vector2(foot.offsetMax.x, foot.offsetMin.y + lineHeight);
                    tag = lineHeight + 2f * s;
                }
                var words = caption.rectTransform;
                words.offsetMin = new Vector2(words.offsetMin.x, PickerPad * s + tag);
                tallest = Mathf.Max(tallest, CaptionHeight(caption, scope.TextWidth) + tag);
                // Measured at full size first, then allowed to shrink: a guard against a name longer
                // than any the measure saw, never the plan. Auto-sizing during the measure would
                // have measured the shrunken size instead.
                AutoSize(caption, 12);
            }
            float height = Mathf.Max((PickerFace + 6f) * s, tallest) + 2f * PickerPad * s;
            scope.Grid.GetComponent<GridLayoutGroup>().cellSize = new Vector2(scope.CellWidth, height);
            int rows = Mathf.CeilToInt(scope.Cells.Count / (float)scope.Columns);
            var element = scope.Grid.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = rows * height + (rows - 1) * gap;
        }

        /// <summary>
        /// Whether a person picker is drawing, and takes this caption as one of its people. Only a
        /// picker begun on this render's panel: one left open by a render that failed part-way
        /// belongs to a panel that is gone, and takes nothing.
        /// </summary>
        private bool PickerTakes(string caption) =>
            picker != null && content != null && picker.Content == content && (picker.Takes == null || picker.Takes(caption));

        /// <summary>One of the picker's people: the verb row first if it is not drawn yet, then the person's button, or the folded stand-in while the picker is shut.</summary>
        private Button PickerChoice(string contestantId, string caption, Action action)
        {
            var scope = picker;
            if (scope.Row == null) DrawPickerRow(scope);
            if (!scope.Open) return Folded(scope);
            var cell = PickerCell(scope, contestantId, caption, () =>
            {
                scope.Chosen?.Invoke();
                // The keyboard stays where the player was: on the verb, which the render that follows
                // draws again under the same name. The person's button goes with the picker.
                if (EventSystem.current != null && scope.Row != null) EventSystem.current.SetSelectedGameObject(scope.Row.gameObject);
                action();
            });
            scope.Cells.Add(cell);
            return cell;
        }

        private void DrawPickerRow(PickerScope scope)
        {
            scope.Row = Action(scope.Verb, () => scope.Toggle?.Invoke());
            if (!string.IsNullOrEmpty(scope.Tag)) Tag(scope.Row, scope.Tag);
            if (!scope.Open) return;
            // Open, the row's chevron turns to point at what it opened.
            if (scope.Row.transform.Find("Chevron") is RectTransform chevron)
            {
                chevron.pivot = new Vector2(.5f, .5f);
                chevron.anchoredPosition = new Vector2(-16f - chevron.sizeDelta.x * .5f, 0f);
                chevron.localRotation = Quaternion.Euler(0f, 0f, -90f);
            }
            float s = FontScale, width = ContentWidth(), gap = PickerGap * s;
            scope.Columns = Mathf.Clamp(Mathf.FloorToInt((width + gap) / (PickerCellMin * s + gap)), 1, 3);
            scope.CellWidth = (width - (scope.Columns - 1) * gap) / scope.Columns;
            scope.TextWidth = Mathf.Max(40f, scope.CellWidth - TextLeft() - PickerPad * s);
            scope.Grid = new GameObject(PersonPickerPrefix + scope.Verb, typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement))
                .GetComponent<RectTransform>();
            scope.Grid.SetParent(content, false);
            var layout = scope.Grid.GetComponent<GridLayoutGroup>();
            layout.spacing = new Vector2(gap, gap);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = scope.Columns;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.cellSize = new Vector2(scope.CellWidth, (PickerFace + 6f + 2f * PickerPad) * s);
        }

        /// <summary>Where a person's words start: past the face and its gap.</summary>
        private float TextLeft() => (PickerPad + PickerFace + 4f + PickerGap) * FontScale;

        /// <summary>The stand-in a shut picker hands back for its rows: one inactive control, so a caller that styles or tags its row still has one.</summary>
        private Button Folded(PickerScope scope)
        {
            if (scope.Folded != null) return scope.Folded;
            // Inactive before it is a control at all, so it never joins the selectables, even for a frame.
            var stand = new GameObject(FoldedRowsName, typeof(RectTransform));
            stand.SetActive(false);
            stand.transform.SetParent(content, false);
            scope.Folded = stand.AddComponent<Button>();
            scope.Folded.interactable = false;
            return scope.Folded;
        }

        /// <summary>
        /// A person's button: their face in a ring the colour of the player's own standing with them,
        /// the week's badge on it, and the row's full caption beside it. Named and captioned as the
        /// row it stands for was.
        /// </summary>
        private Button PickerCell(PickerScope scope, string contestantId, string caption, Action action)
        {
            float s = FontScale;
            var rect = Chrome(caption, scope.Grid, UiTheme.Emphasis.Interactive);
            HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
            var button = Pressable(rect, action);

            var state = director != null ? director.Snapshot : null;
            var person = state != null ? state.Find(contestantId) : null;
            if (person != null && !person.isPlayer)
            {
                double trust = state.Score(state.playerId, contestantId);
                // The player's own reading of them, as the row's portrait ring always said it.
                var ring = state.Allied(state.playerId, contestantId) || trust > 5 ? UiTheme.Allied
                    : trust < -5 ? UiTheme.Conflict : UiTheme.Muted;
                var rim = HudPrimitives.Portrait(rect, CharacterPortraits.Get(person), ring, PickerFace * s, 2f * s, false, person);
                rim.gameObject.name = "Portrait";
                rim.anchorMin = rim.anchorMax = new Vector2(0f, .5f);
                rim.pivot = new Vector2(0f, .5f);
                rim.anchoredPosition = new Vector2(PickerPad * s, 0f);
                HudPrimitives.AddRoleMark(rim, RoleOf(state, contestantId), rim.sizeDelta.x * .82f);
            }

            var words = NewText(rect, caption, PickerCaptionSize, Paper);
            words.alignment = TextAlignmentOptions.MidlineLeft;
            words.rectTransform.anchorMin = Vector2.zero;
            words.rectTransform.anchorMax = Vector2.one;
            words.rectTransform.offsetMin = new Vector2(TextLeft(), PickerPad * s);
            words.rectTransform.offsetMax = new Vector2(-PickerPad * s, -PickerPad * s);
            scope.CaptionOf[button] = words;
            return button;
        }

        /// <summary>
        /// A tag on one of the picker's people goes on the button's foot, under its words; a second tag
        /// on the same button joins the first. A tag on a folded row goes nowhere. False for anything
        /// that is not the picker's.
        /// </summary>
        private bool PickerTag(Button target, string text)
        {
            if (picker == null || target == null) return false;
            if (target == picker.Folded) return true;
            if (!picker.CaptionOf.ContainsKey(target)) return false;
            if (picker.TagOf.TryGetValue(target, out var line) && line != null)
            {
                line.text = line.text + " · " + Localisation.Text(text);
                return true;
            }
            float s = FontScale;
            var label = NewText(target.transform, text, PickerTagSize, UiTheme.Muted);
            label.name = "Tag";
            label.alignment = TextAlignmentOptions.BottomLeft;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = new Vector2(1f, 0f);
            label.rectTransform.pivot = new Vector2(0f, 0f);
            label.rectTransform.offsetMin = new Vector2(TextLeft(), PickerPad * s);
            label.rectTransform.offsetMax = new Vector2(-PickerPad * s, PickerPad * s + Mathf.Ceil(PickerTagSize * s * 1.3f));
            picker.TagOf[target] = label;
            return true;
        }

        /// <summary>How tall a person's words stand at the cell's width, never under 1.3 of their size.</summary>
        private float CaptionHeight(TMP_Text caption, float width) =>
            Mathf.Max(Mathf.Ceil(caption.fontSize * 1.3f), Mathf.Ceil(caption.GetPreferredValues(caption.text, width, 0f).y) + 4f * FontScale);
    }
}
