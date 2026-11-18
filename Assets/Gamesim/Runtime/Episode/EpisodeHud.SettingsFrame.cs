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
    /// The Settings frame's shell (menu slice 4, the review's change 2): the list of sections at the
    /// frame's left - a strip across its top where the frame is too narrow for a column - with
    /// exactly one lit, and the game menu card the house's door opens first. The rows each section
    /// shows are the row kit's (EpisodeHud.SettingRows.cs); what they are is the director's.
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The list of sections, so a test finds it the way it finds a named panel.</summary>
        public const string SettingsSectionsName = "Settings sections";
        /// <summary>The bar beside the lit section's name: the lit entry is marked by more than its colour.</summary>
        public const string LitMarkName = "Lit mark";
        /// <summary>The game menu card's head.</summary>
        public const string GameMenuHeaderName = "Game menu header";
        /// <summary>How the game menu card's rows are named: "Game menu: Resume" (their captions are the words alone).</summary>
        public const string GameMenuRowPrefix = "Game menu: ";

        /// <summary>The frame keeps a column for the list while the rows beside it keep at least this much width (at standard text).</summary>
        private const float SettingsRowsLeastWidth = 560f;
        /// <summary>The list's column width at standard text.</summary>
        private const float SettingsListWidth = 200f;

        /// <summary>The section entries this render, in the list's order.</summary>
        private readonly List<SettingRowButton> settingsTabs = new List<SettingRowButton>();

        /// <summary>Whether this render's list is a strip across the top (the frame too narrow for a column).</summary>
        public bool SettingsListIsStrip { get; private set; }

        /// <summary>
        /// The list of sections, beside the rows or over them. Each entry is captioned with the
        /// section's name and named <see cref="SettingsWords.TabName"/>; the lit one has the
        /// accent ground, the heavier type and a bar (<see cref="LitMarkName"/>). Enter, the pad's
        /// A or a click shows a section; up and down walk the panel's ring as everywhere, and left and
        /// right move the keyboard to the neighbouring entry. Call after the frame's head is drawn.
        /// </summary>
        public IReadOnlyList<SettingRowButton> SettingsSections(IReadOnlyList<SettingsSection> sections, SettingsSection lit, Action<SettingsSection> choose)
        {
            settingsTabs.Clear();
            SettingsListIsStrip = false;
            if (modal == null || modalScroll == null || sections == null || sections.Count == 0) return settingsTabs;
            float s = FontScale;
            var scroll = (RectTransform)modalScroll.transform;
            float top = -scroll.offsetMax.y, left = scroll.offsetMin.x, right = -scroll.offsetMax.x, bottom = scroll.offsetMin.y;
            float frameWidth = modal.sizeDelta.x;
            float tabHeight = Mathf.Ceil(Mathf.Max(44f * s, 17f * s * WayOut.LabelBoxRatio + 14f));
            float gap = Mathf.Round(6f * s);
            float column = Mathf.Ceil(SettingsListWidth * s);
            bool strip = frameWidth - left - right - column - 14f * s < SettingsRowsLeastWidth * s;
            SettingsListIsStrip = strip;

            var list = new GameObject(SettingsSectionsName, typeof(RectTransform)).GetComponent<RectTransform>();
            list.SetParent(modal, false);
            // Before the rows in the hierarchy, so the panel's ring reaches the list first.
            list.SetSiblingIndex(scroll.GetSiblingIndex());

            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            var built = new List<(RectTransform rect, float width)>();
            foreach (var section in sections)
            {
                bool on = section == lit;
                var rect = Chrome(SettingsWords.TabName(section), list, on ? UiTheme.Emphasis.Active : UiTheme.Emphasis.Interactive);
                var ground = rect.GetComponent<Image>();
                if (ground != null && on) ground.color = new Color(Accent.r, Accent.g, Accent.b, .24f);
                var label = NewText(rect, SettingsWords.Name(section), 17, on ? Color.white : Paper);
                var weight = on ? semibold : medium;
                if (weight != null) label.font = weight;
                label.alignment = strip ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                Stretch(label.rectTransform, (strip ? 12f : 20f) * s, 2f, 12f * s, 2f);
                if (on)
                {
                    // The lit entry's bar: at its left in a column, along its foot in a strip.
                    var mark = Panel(LitMarkName, rect, Accent, 2);
                    mark.GetComponent<Image>().raycastTarget = false;
                    if (strip) { mark.anchorMin = new Vector2(0f, 0f); mark.anchorMax = new Vector2(1f, 0f); mark.pivot = new Vector2(.5f, 0f); mark.offsetMin = new Vector2(10f * s, 3f); mark.offsetMax = new Vector2(-10f * s, 3f + 4f * s); }
                    else { mark.anchorMin = new Vector2(0f, 0f); mark.anchorMax = new Vector2(0f, 1f); mark.pivot = new Vector2(0f, .5f); mark.offsetMin = new Vector2(6f, 8f * s); mark.offsetMax = new Vector2(6f + 5f * s, -8f * s); }
                }
                var shown = section;
                var tab = PressableAs<SettingRowButton>(rect, () => choose?.Invoke(shown));
                tab.Description = SettingsWords.Name(section) + (on ? ", shown" : "");
                settingsTabs.Add(tab);
                float words = Mathf.Ceil(label.GetPreferredValues(label.text).x);
                built.Add((rect, strip ? Mathf.Max(90f * s, words + 30f * s) : column));
            }
            // Left and right on an entry move the keyboard along the list.
            for (int i = 0; i < settingsTabs.Count; i++)
            {
                int at = i;
                settingsTabs[i].Step = direction =>
                {
                    var next = settingsTabs[((at + direction) % settingsTabs.Count + settingsTabs.Count) % settingsTabs.Count];
                    if (next != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(next.gameObject);
                };
            }

            if (!strip)
            {
                list.anchorMin = new Vector2(0f, 0f); list.anchorMax = new Vector2(0f, 1f); list.pivot = new Vector2(0f, 1f);
                list.offsetMin = new Vector2(left, bottom); list.offsetMax = new Vector2(left + column, -top);
                float y = 0f;
                foreach (var part in built)
                {
                    Anchor(part.rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(column, tabHeight));
                    y += tabHeight + gap;
                }
                scroll.offsetMin = new Vector2(left + column + 14f * s, bottom);
            }
            else
            {
                // A strip: entries flow left to right and wrap onto a second line at the largest sizes.
                float room = Mathf.Max(120f, frameWidth - left - right);
                float x = 0f, y = 0f;
                foreach (var part in built)
                {
                    float width = Mathf.Min(part.width, room);
                    if (x > 0f && x + width > room) { x = 0f; y += tabHeight + gap; }
                    Anchor(part.rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, tabHeight));
                    x += width + gap;
                }
                float height = y + tabHeight;
                list.anchorMin = new Vector2(0f, 1f); list.anchorMax = new Vector2(1f, 1f); list.pivot = new Vector2(.5f, 1f);
                list.offsetMin = new Vector2(left, -top - height); list.offsetMax = new Vector2(-right, -top);
                scroll.offsetMax = new Vector2(scroll.offsetMax.x, -(top + height + 10f * s));
            }
            // The rows' column, centred in what the list leaves, as wide as Settings' reading column.
            UncapContent();
            float viewport = frameWidth - scroll.offsetMin.x + scroll.offsetMax.x - 18f;
            float cap = SettingsColumnWidth * s;
            int extra = Mathf.Max(0, Mathf.RoundToInt((viewport - 16f - cap) * .5f));
            var layout = content != null ? content.GetComponent<VerticalLayoutGroup>() : null;
            if (layout != null) layout.padding = new RectOffset(8 + extra, 8 + extra, layout.padding.top, layout.padding.bottom);
            return settingsTabs;
        }

        /// <summary>
        /// Takes the panel's Close away for this render: Settings the main menu opened go back there
        /// by their "Back to the main menu", and a Close beside it would drop the player into a house
        /// they never chose to enter. Escape and B press the way back.
        /// </summary>
        public void HidePanelClose()
        {
            if (panelClose == null) return;
            panelClose.gameObject.SetActive(false);
            panelClose = null;
        }

        /// <summary>
        /// The game menu card (menu slice 4): a small card over the middle of the house, in place of
        /// the tall Settings frame, under its own head - "PAUSED" over the title only while the
        /// house's clock really is held (the caller says). Close stays at its top right and does what
        /// Resume does. Call after the Settings layout is set; the rows go in with <see cref="GameMenuRow"/>.
        /// </summary>
        public void GameMenuCard(bool paused)
        {
            if (modal == null) return;
            var bounds = canvas != null ? ((RectTransform)canvas.transform).rect : new Rect(0f, 0f, 1600f, 900f);
            var stage = StageRect(bounds.width > 0 ? bounds.width : 1600f, bounds.height > 0 ? bounds.height : 900f);
            float s = FontScale;
            float width = Mathf.Min(stage.width, Mathf.Ceil(480f * s));
            float height = Mathf.Min(stage.height, Mathf.Ceil(470f * s + 40f));
            modal.anchorMin = modal.anchorMax = Vector2.zero;
            modal.pivot = Vector2.zero;
            modal.anchoredPosition = stage.position + new Vector2((stage.width - width) * .5f, (stage.height - height) * .5f);
            modal.sizeDelta = new Vector2(width, height);
            UncapContent();
            ScreenHeader(GameMenuHeaderName, paused ? SettingsWords.PausedLabel : "", SettingsWords.GameMenuTitle, UiTheme.Icon("settings"));
        }

        /// <summary>
        /// A row of the game menu card, captioned <paramref name="caption"/> and named
        /// <see cref="GameMenuRowPrefix"/> + caption. The one <paramref name="primary"/> row has the
        /// bright main-button look; the others are the panel's usual rows.
        /// </summary>
        public Button GameMenuRow(string caption, Action action, bool primary = false)
        {
            if (content == null) return null;
            float s = FontScale;
            var rect = Chrome(GameMenuRowPrefix + caption, content, UiTheme.Emphasis.Interactive);
            rect.gameObject.AddComponent<LayoutElement>().minHeight = 57 * s;
            if (primary) EndScreenKit.Frame(rect, PackArt.Pack8ButtonPrimary, 10f * s, UiTheme.ActionBlue, UiTheme.Glow);
            else HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
            var button = FinishButton(rect, caption, action, 16f, 16f);
            var words = button.GetComponentInChildren<TMP_Text>();
            if (words != null && primary)
            {
                words.alignment = TextAlignmentOptions.Center;
                var bold = UiTheme.Font(UiTheme.Weight.SemiBold);
                if (bold != null) words.font = bold;
            }
            return button;
        }

        /// <summary>A muted line on the card or in a section: what a row keeps, when the season was saved, why a row cannot be used.</summary>
        public TMP_Text MutedLine(string value, string objectName = null)
        {
            if (content == null || string.IsNullOrEmpty(value)) return null;
            var text = FlowText(value, 15, UiTheme.Muted);
            if (!string.IsNullOrEmpty(objectName)) text.gameObject.name = objectName;
            return text;
        }

        /// <summary>The shared button's behaviour on a control of a given kind (the row kit's switches, choices and section entries).</summary>
        private T PressableAs<T>(RectTransform rect, Action action) where T : Button
        {
            var button = rect.gameObject.AddComponent<T>();
            var colours = button.colors;
            colours.highlightedColor = new Color(1.35f, 1.35f, 1.35f);
            colours.selectedColor = colours.highlightedColor;
            colours.pressedColor = new Color(.85f, .85f, .85f);
            button.colors = colours;
            var press = rect.gameObject.AddComponent<HudPress>();
            press.ReducedMotion = ReducedMotion;
            press.Hovered = () => Foley(HouseAudio.Cue.Hover);
            button.onClick.AddListener(() =>
            {
                if (button == null || !button.IsActive() || !button.IsInteractable()) return;
                action?.Invoke();
            });
            return button;
        }
    }
}
