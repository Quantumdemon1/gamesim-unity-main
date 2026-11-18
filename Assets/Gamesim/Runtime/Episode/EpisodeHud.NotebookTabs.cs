using System;
using System.Collections.Generic;
using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The notebook's tab strip (menu slice 3, the review's change 6): "Notes | Deals &amp; promises |
    /// Alliances" in a fixed band under the reading frame's head, on each of the three pages. The tabs
    /// are siblings, so none of the three has a way back to another; the lit tab is the page on screen.
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>A focused tab's thin line, inside the tab and above the lit bar, so focus never reads as a second lit tab.</summary>
        public const string TabFocusName = TabStep.FocusName;

        /// <summary>
        /// Draws the strip into the reading frame's band (<see cref="ReadingBand"/>), call it after the
        /// head. Each tab is a control named and captioned by its caption; the lit one stands raised,
        /// in the heavier weight, with a bar under it (<see cref="NotebookStrip.LitMarkName"/>): a shape
        /// and a weight as well as a colour. Left and Right move along it (<see cref="TabStep"/>), Up,
        /// Down and Tab walk it with the rest of the panel, and a click or Enter or the pad's A
        /// presses a tab. Null without a panel up.
        /// </summary>
        public RectTransform NotebookTabStrip(IList<(string Caption, bool Lit, Action Open)> tabs)
        {
            if (modal == null || tabs == null || tabs.Count == 0) return null;
            float s = FontScale, height = 40f * s;
            var band = ReadingBand(NotebookStrip.StripName, height + 6f * s);
            if (band == null) return null;
            // Next after the head in the panel's order, so the keyboard reaches the tabs before the page.
            band.SetSiblingIndex(Mathf.Min(1, modal.childCount - 1));

            var rule = HudPrimitives.Fill("Strip rule", band, UiTheme.Edge(UiTheme.Emphasis.Interactive), 1);
            rule.anchorMin = new Vector2(0f, 0f); rule.anchorMax = new Vector2(1f, 0f); rule.pivot = new Vector2(.5f, 0f);
            rule.anchoredPosition = Vector2.zero; rule.sizeDelta = new Vector2(0f, 1f);
            rule.GetComponent<Image>().raycastTarget = false;

            float x = 0f;
            foreach (var tab in tabs)
            {
                var ground = tab.Lit ? new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .95f)
                    : new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .30f);
                var rect = Panel(tab.Caption, band, ground, 8);
                var label = NewText(rect, tab.Caption, 16, tab.Lit ? Paper : new Color(Paper.r, Paper.g, Paper.b, .82f));
                var weight = UiTheme.Font(tab.Lit ? UiTheme.Weight.SemiBold : UiTheme.Weight.Medium);
                if (weight != null) label.font = weight;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                float width = Mathf.Ceil(label.GetPreferredValues(label.text).x) + 40f * s;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(x, 0f);
                rect.sizeDelta = new Vector2(width, height);
                Stretch(label.rectTransform, 12f * s, 2f, 12f * s, 6f * s);

                if (tab.Lit)
                {
                    var bar = HudPrimitives.Fill(NotebookStrip.LitMarkName, rect, UiTheme.Accent, 2);
                    bar.anchorMin = new Vector2(0f, 0f); bar.anchorMax = new Vector2(1f, 0f); bar.pivot = new Vector2(.5f, 0f);
                    bar.anchoredPosition = Vector2.zero; bar.sizeDelta = new Vector2(0f, 4f * s);
                    bar.GetComponent<Image>().raycastTarget = false;
                }
                var focus = HudPrimitives.Fill(TabFocusName, rect, Paper, 1);
                focus.anchorMin = new Vector2(0f, 0f); focus.anchorMax = new Vector2(1f, 0f); focus.pivot = new Vector2(.5f, 0f);
                focus.anchoredPosition = new Vector2(0f, 6f * s); focus.sizeDelta = new Vector2(-24f * s, 2f);
                focus.GetComponent<Image>().raycastTarget = false;
                focus.gameObject.SetActive(false);

                var button = Pressable(rect, tab.Open);
                // The keyboard's focus is the thin line, never a brighter ground: a focused tab must
                // not look like the lit one (the creator's steps do the same).
                var colours = button.colors;
                colours.selectedColor = colours.normalColor;
                button.colors = colours;
                rect.gameObject.AddComponent<TabStep>();
                x += width + 8f * s;
            }
            return band;
        }
    }
}
