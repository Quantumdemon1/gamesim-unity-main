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
    /// The Saves section's season cards (menu slice 4, the review's change 2): one card per saved
    /// season - whose it is, its week and who is left, when it was saved - with its controls along
    /// the foot. The running season's card says "Playing now" in words, not by colour alone. What
    /// the cards are and what their controls do is the director's (EpisodeDirector.SettingsSaves.cs).
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>One control along a season card's foot.</summary>
        public struct SeasonControl
        {
            /// <summary>The words on it: what tests and screen readers find it by, with <see cref="Name"/>.</summary>
            public string Caption;
            /// <summary>Its object's name, unique on the screen: "Load: episode-1a2b".</summary>
            public string Name;
            /// <summary>What a screen reader says for it: "Load Ava, week 4".</summary>
            public string Description;
            /// <summary>What a press does.</summary>
            public Action Press;
            /// <summary>Whether it discards something (Delete): it wears the danger edge.</summary>
            public bool Danger;
        }

        /// <summary>
        /// A season card: its <paramref name="title"/> (the player's houseguest, or that it cannot be
        /// opened), "Playing now" beside it when <paramref name="playing"/>, its
        /// <paramref name="details"/> line and its muted <paramref name="saved"/> line, then
        /// <paramref name="controls"/> side by side. Left and right move the keyboard along them; up
        /// and down walk the panel's ring as everywhere. Returns the card's controls in order.
        /// </summary>
        public IReadOnlyList<SettingRowButton> SeasonCard(string key, string title, string details, string saved, bool playing, IList<SeasonControl> controls)
        {
            var made = new List<SettingRowButton>();
            if (content == null) return made;
            float s = FontScale;
            var card = Chrome(SavesWords.CardName(key), content, playing ? UiTheme.Emphasis.Active : UiTheme.Emphasis.Resting);
            var stack = card.gameObject.AddComponent<VerticalLayoutGroup>();
            int pad = Mathf.RoundToInt(14 * s);
            stack.padding = new RectOffset(pad, pad, pad, pad); stack.spacing = 6 * s;
            stack.childControlWidth = true; stack.childControlHeight = true;
            stack.childForceExpandWidth = true; stack.childForceExpandHeight = false;

            var head = new GameObject("Season head", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            head.SetParent(card, false);
            var across = head.GetComponent<HorizontalLayoutGroup>();
            across.spacing = 10 * s; across.childControlWidth = true; across.childControlHeight = true;
            across.childForceExpandWidth = false; across.childForceExpandHeight = false;
            across.childAlignment = TextAnchor.MiddleLeft;
            float titleLine = Mathf.Ceil(20f * s * WayOut.LabelBoxRatio);
            head.gameObject.AddComponent<LayoutElement>().minHeight = titleLine;
            var titleText = NewText(head, title, 20, Paper);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) titleText.font = semibold;
            titleText.textWrappingMode = TextWrappingModes.NoWrap;
            var nameSize = titleText.gameObject.AddComponent<LayoutElement>();
            nameSize.minHeight = titleLine; nameSize.flexibleWidth = 1f;
            if (playing)
            {
                // The mark is words on a lit pill: the running season is told by what it says.
                var mark = Chrome(SavesWords.MarkName, head, UiTheme.Emphasis.Active);
                var ground = mark.GetComponent<Image>();
                if (ground != null) ground.color = new Color(Accent.r, Accent.g, Accent.b, .24f);
                var words = NewText(mark, SavesWords.PlayingNow, 15, Color.white);
                words.alignment = TextAlignmentOptions.Center;
                words.textWrappingMode = TextWrappingModes.NoWrap;
                Stretch(words.rectTransform, 10f * s, 2f, 10f * s, 2f);
                var markSize = mark.gameObject.AddComponent<LayoutElement>();
                markSize.minHeight = Mathf.Ceil(15f * s * WayOut.LabelBoxRatio + 6f);
                markSize.preferredWidth = Mathf.Ceil(words.GetPreferredValues(words.text).x + 24f * s);
            }

            if (!string.IsNullOrEmpty(details))
            {
                var line = NewText(card, details, 17, Paper);
                line.gameObject.AddComponent<LayoutElement>().minHeight = Mathf.Ceil(17f * s * WayOut.LabelBoxRatio);
            }
            if (!string.IsNullOrEmpty(saved))
            {
                var when = NewText(card, saved, 15, UiTheme.Muted);
                when.name = SavesWords.SavedLineName;
                when.gameObject.AddComponent<LayoutElement>().minHeight = Mathf.Ceil(15f * s * WayOut.LabelBoxRatio);
            }

            if (controls == null || controls.Count == 0) return made;
            var foot = new GameObject("Season controls", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            foot.SetParent(card, false);
            var row = foot.GetComponent<HorizontalLayoutGroup>();
            row.spacing = 12 * s; row.childControlWidth = true; row.childControlHeight = true;
            row.childForceExpandWidth = true; row.childForceExpandHeight = false;
            foot.gameObject.AddComponent<LayoutElement>().minHeight = 52 * s;
            foreach (var control in controls)
            {
                var rect = Chrome(string.IsNullOrEmpty(control.Name) ? control.Caption : control.Name, foot, UiTheme.Emphasis.Interactive);
                HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
                if (control.Danger) UiTheme.AddBorder(rect, UiTheme.GlassRadius, UiTheme.Danger);
                var size = rect.gameObject.AddComponent<LayoutElement>();
                size.minHeight = 52 * s; size.flexibleWidth = 1f;
                var press = control.Press;
                var button = PressableAs<SettingRowButton>(rect, () => press?.Invoke());
                var words = NewText(rect, control.Caption, 18, Paper);
                words.alignment = TextAlignmentOptions.Center;
                words.textWrappingMode = TextWrappingModes.NoWrap;
                Stretch(words.rectTransform, 12f, 4f, 12f, 4f);
                AutoSize(words, 14);
                button.Description = string.IsNullOrEmpty(control.Description) ? control.Caption : control.Description;
                made.Add(button);
            }
            // Left and right move along the card's foot; the ends stay put.
            for (int i = 0; i < made.Count; i++)
            {
                int at = i;
                made[i].Step = direction =>
                {
                    int next = at + Math.Sign(direction);
                    if (next < 0 || next >= made.Count || made[next] == null || EventSystem.current == null) return;
                    EventSystem.current.SetSelectedGameObject(made[next].gameObject);
                };
            }
            return made;
        }
    }
}
