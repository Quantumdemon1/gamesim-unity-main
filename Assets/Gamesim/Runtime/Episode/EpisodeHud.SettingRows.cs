using System;
using System.Collections.Generic;
using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The Settings row kit (menu slice 4, the review's change 2): a name on the left, its current
    /// value on the right, one control. A switch reads "On" or "Off" and flips on Enter, the pad's
    /// A, a click, left or right; a slider reads "70%" and moves a step on left or right (the
    /// mouse drags it); a choice reads its value and steps through its values on left and right
    /// (Enter steps on). The caption - what tests and screen readers find - is the setting's NAME,
    /// and the row's object is named by it too, so a render keeps the keyboard on it; the value is
    /// a label of its own (<see cref="SettingValueName"/>), never part of the caption. A row that
    /// cannot be used says why in a short muted line under it (<see cref="WhyDisabledName"/>).
    ///
    /// <para>A row's press only calls the caller's setter: the director keeps the value and draws
    /// the panel again, and the new row reads the new value.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The value label beside a row's name.</summary>
        public const string SettingValueName = "Setting value";
        /// <summary>The muted line under a row that cannot be used.</summary>
        public const string WhyDisabledName = "Why disabled";

        /// <summary>A switch: "Music" and "On". Enter, A, a click, left and right all flip it.</summary>
        public SettingRowButton SettingSwitch(string name, bool on, Action<bool> set, string whyDisabled = null)
        {
            var button = SettingRow(name, SettingsWords.SwitchValue(on), () => set?.Invoke(!on), whyDisabled);
            if (button != null) button.Step = _ => set?.Invoke(!on);
            return button;
        }

        /// <summary>
        /// A choice: "Text size" and "Large". Left and right step back and on through
        /// <paramref name="values"/> (wrapping), Enter, A and a click step on; <paramref name="choose"/>
        /// receives the index chosen.
        /// </summary>
        public SettingRowButton SettingChoice(string name, IReadOnlyList<string> values, int index, Action<int> choose, string whyDisabled = null)
        {
            int count = values != null ? values.Count : 0;
            int at = count > 0 ? Mathf.Clamp(index, 0, count - 1) : 0;
            string value = count > 0 ? values[at] : "";
            var button = SettingRow(name, value, () => choose?.Invoke(SettingsWords.ChoiceStepped(at, count, 1)), whyDisabled);
            if (button != null) button.Step = direction => choose?.Invoke(SettingsWords.ChoiceStepped(at, count, direction));
            return button;
        }

        /// <summary>
        /// A slider: "Music volume" and "70%". The mouse drags it (<paramref name="live"/> on every
        /// step of a drag, <paramref name="kept"/> once as it is let go); left and right move it by
        /// <see cref="SettingsWords.SliderStep"/> and keep the value at once.
        /// </summary>
        public SettingRowSlider SettingSlider(string name, int percent, Action<int> live, Action<int> kept, string whyDisabled = null)
        {
            if (content == null) return null;
            float s = FontScale;
            var row = Panel(name, content, Surface);
            var drag = row.gameObject.AddComponent<SliderSettle>();
            row.gameObject.AddComponent<LayoutElement>().minHeight = Mathf.RoundToInt(78 * s);
            var label = NewText(row, name, 18, Paper);
            var count = NewText(row, SettingsWords.Percent(percent), 18, Accent);
            count.name = SettingValueName;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) count.font = semibold;
            float top = 8 * s, line = Mathf.Ceil(18f * s * WayOut.LabelBoxRatio + 4f);
            label.rectTransform.anchorMin = new Vector2(0, 1); label.rectTransform.anchorMax = new Vector2(1, 1);
            label.rectTransform.offsetMin = new Vector2(16, -top - line); label.rectTransform.offsetMax = new Vector2(-110 * s, -top);
            count.alignment = TextAlignmentOptions.Right;
            count.rectTransform.anchorMin = new Vector2(1, 1); count.rectTransform.anchorMax = new Vector2(1, 1);
            count.rectTransform.pivot = new Vector2(1, 1);
            count.rectTransform.anchoredPosition = new Vector2(-16, -top);
            count.rectTransform.sizeDelta = new Vector2(90 * s, line);

            var track = Panel("Track", row, UiTheme.Outline, 3);
            track.anchorMin = new Vector2(0, 0); track.anchorMax = new Vector2(1, 0);
            track.offsetMin = new Vector2(28, 18 * s); track.offsetMax = new Vector2(-28, 26 * s);
            track.GetComponent<Image>().raycastTarget = false;
            var fill = Panel("Fill", track, Accent, 3);
            Stretch(fill, 0, 0, 0, 0); fill.GetComponent<Image>().raycastTarget = false;
            var handles = new GameObject("Handle area", typeof(RectTransform)).GetComponent<RectTransform>();
            handles.SetParent(row, false);
            handles.anchorMin = new Vector2(0, 0); handles.anchorMax = new Vector2(1, 0);
            handles.offsetMin = new Vector2(28, 8 * s); handles.offsetMax = new Vector2(-28, 36 * s);
            var handle = Panel("Handle", handles, Paper);
            handle.sizeDelta = new Vector2(24 * s, 0);
            var slider = row.gameObject.AddComponent<SettingRowSlider>();
            slider.targetGraphic = handle.GetComponent<Image>(); slider.handleRect = handle; slider.fillRect = fill;
            slider.direction = Slider.Direction.LeftToRight; slider.minValue = 0; slider.maxValue = 100; slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(Mathf.Clamp(percent, 0, 100));
            slider.Description = SettingsWords.Description(name, SettingsWords.Percent(percent), whyDisabled);
            drag.Settled = () => kept?.Invoke(Mathf.RoundToInt(slider.value));
            slider.onValueChanged.AddListener(value =>
            {
                int held = Mathf.RoundToInt(value);
                count.text = SettingsWords.Percent(held);
                slider.Description = SettingsWords.Description(name, count.text, whyDisabled);
                live?.Invoke(held);
                if (drag.Dragging) drag.Pending = true;
                else kept?.Invoke(held);
            });
            if (!string.IsNullOrEmpty(whyDisabled)) { slider.interactable = false; MutedLine(whyDisabled, WhyDisabledName); }
            return slider;
        }

        /// <summary>
        /// An action row: a caption that does something ("Save now", "Replay the tour"), drawn as the
        /// panel's other rows are, with the muted line saying why when it cannot be used.
        /// </summary>
        public Button SettingAction(string caption, Action action, string whyDisabled = null)
        {
            if (content == null) return null;
            var button = Action(caption, action);
            if (!string.IsNullOrEmpty(whyDisabled)) { button.interactable = false; MutedLine(whyDisabled, WhyDisabledName); }
            return button;
        }

        /// <summary>
        /// Pick, then confirm (the review's section 3): a card under the row that asked, with the
        /// question, the safe choice - which takes the keyboard - and the button that names the
        /// result. Drawn while the caller holds the pick; either button's action lets it go. Returns
        /// the safe choice's control.
        /// </summary>
        public Button SettingsConfirm(string question, string safeCaption, Action safe, string resultCaption, Action result)
        {
            if (content == null) return null;
            float s = FontScale;
            var card = Chrome(SettingsWords.ConfirmCardName, content, UiTheme.Emphasis.Active);
            var stack = card.gameObject.AddComponent<VerticalLayoutGroup>();
            int pad = Mathf.RoundToInt(14 * s);
            stack.padding = new RectOffset(pad, pad, pad, pad); stack.spacing = 10 * s;
            stack.childControlWidth = true; stack.childControlHeight = true;
            stack.childForceExpandWidth = true; stack.childForceExpandHeight = false;
            var words = NewText(card, question, 18, Paper);
            words.gameObject.AddComponent<LayoutElement>().minHeight = Mathf.Ceil(18f * s * WayOut.LabelBoxRatio);

            var pair = new GameObject("Confirm choices", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            pair.SetParent(card, false);
            var across = pair.GetComponent<HorizontalLayoutGroup>();
            across.spacing = 12 * s; across.childControlWidth = true; across.childControlHeight = true;
            across.childForceExpandWidth = true; across.childForceExpandHeight = false;
            pair.gameObject.AddComponent<LayoutElement>().minHeight = 52 * s;

            var keep = Chrome(safeCaption, pair, UiTheme.Emphasis.Interactive);
            keep.gameObject.AddComponent<LayoutElement>().minHeight = 52 * s;
            var safeButton = FinishButton(keep, safeCaption, safe, 16f, 16f);
            var go = Chrome(resultCaption, pair, UiTheme.Emphasis.Interactive);
            UiTheme.AddBorder(go, UiTheme.GlassRadius, UiTheme.Danger);
            go.gameObject.AddComponent<LayoutElement>().minHeight = 52 * s;
            FinishButton(go, resultCaption, result, 16f, 16f);
            // The safe choice has the keyboard: a second Enter keeps what is there. Escape and B take it too.
            FocusWhenWired(safeCaption);
            ConfirmCancelAction = safe;
            return safeButton;
        }

        /// <summary>A name-and-value row: the name (the caption) on the left, the value on the right, one control.</summary>
        private SettingRowButton SettingRow(string name, string value, Action press, string whyDisabled)
        {
            if (content == null) return null;
            float s = FontScale;
            var rect = Chrome(name, content, UiTheme.Emphasis.Interactive);
            HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
            rect.gameObject.AddComponent<LayoutElement>().minHeight = 57 * s;
            var button = PressableAs<SettingRowButton>(rect, press);
            float valueWidth = Mathf.Ceil(180f * s);
            // The caption first among the row's texts: tests and screen readers read it as the control's name.
            var label = NewText(rect, name, 18, Paper);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            Stretch(label.rectTransform, 16f, 5f, valueWidth + 24f, 5f);
            var shown = NewText(rect, value, 18, Accent);
            shown.name = SettingValueName;
            shown.alignment = TextAlignmentOptions.MidlineRight;
            shown.textWrappingMode = TextWrappingModes.NoWrap;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) shown.font = semibold;
            shown.rectTransform.anchorMin = new Vector2(1f, 0f); shown.rectTransform.anchorMax = new Vector2(1f, 1f);
            shown.rectTransform.pivot = new Vector2(1f, .5f);
            shown.rectTransform.sizeDelta = new Vector2(valueWidth, 0f);
            shown.rectTransform.anchoredPosition = new Vector2(-16f, 0f);
            AutoSize(shown, 14);
            button.Description = SettingsWords.Description(name, value, whyDisabled);
            if (!string.IsNullOrEmpty(whyDisabled)) { button.interactable = false; MutedLine(whyDisabled, WhyDisabledName); }
            return button;
        }
    }
}
