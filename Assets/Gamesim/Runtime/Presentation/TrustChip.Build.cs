using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The trust chip's builder (menu slice 3): the shape, the signed figure and, where asked, the
    /// words "Your trust", side by side in one rectangle the caller places. Every reading page that
    /// shows your trust in someone draws it through here, so the figure, the bands and the colours
    /// are one. The words and bands are in <c>TrustChip.cs</c>.
    /// </summary>
    public static partial class TrustChip
    {
        /// <summary>The band's colour: the allied green above zero, the conflict red below, muted at zero. Never the only cue.</summary>
        public static Color Tint(Band band) =>
            band == Band.Above ? UiTheme.Allied : band == Band.Below ? UiTheme.Conflict : UiTheme.Muted;

        /// <summary>The smallest a chip's words are drawn, at any text size (the HUD's floor).</summary>
        public const float MinLabelSize = 14f;

        /// <summary>
        /// A chip for <paramref name="score"/> under <paramref name="parent"/>: its shape, the figure
        /// at <paramref name="fontSize"/> (already scaled to the text size), and with
        /// <paramref name="withLabel"/> the words "Your trust" after it. Nothing on it takes the
        /// pointer, so a row it stands in stays one press. The rectangle is sized to what it holds
        /// (every label box at least 1.3 times its font) and anchored at its left middle.
        /// </summary>
        public static RectTransform Build(Transform parent, double score, float fontSize, float scale, bool withLabel)
        {
            var band = BandOf(score);
            var tint = Tint(band);
            float s = scale > 0f ? scale : 1f;
            var chip = new GameObject(ChipName, typeof(RectTransform)).GetComponent<RectTransform>();
            chip.SetParent(parent, false);
            chip.anchorMin = chip.anchorMax = new Vector2(0f, .5f);
            chip.pivot = new Vector2(0f, .5f);

            float labelSize = Mathf.Max(MinLabelSize, Mathf.Round(fontSize * .62f));
            float height = Mathf.Ceil(Mathf.Max(fontSize, withLabel ? labelSize : 0f) * 1.3f) + 2f;
            float mark = Mathf.Round(Mathf.Max(10f, fontSize * .62f));
            float x = 0f;

            // The shape: a mark pointing up above zero, a level bar at zero, a mark pointing down below.
            var shape = new GameObject(ShapeName, typeof(RectTransform)).GetComponent<RectTransform>();
            shape.SetParent(chip, false);
            shape.anchorMin = shape.anchorMax = new Vector2(0f, .5f);
            shape.pivot = new Vector2(0f, .5f);
            shape.sizeDelta = new Vector2(mark, mark);
            shape.anchoredPosition = new Vector2(x, 0f);
            float thick = Mathf.Max(2f, Mathf.Round(mark * .2f));
            if (band == Band.Even)
            {
                Bar(shape, new Vector2(mark * .5f, 0f), new Vector2(mark * .82f, thick), 0f, tint);
            }
            else
            {
                // Two strokes meeting at the tip: "^" above zero, "v" below.
                float sign = band == Band.Above ? 1f : -1f, arm = mark * .62f;
                Bar(shape, new Vector2(mark * .5f - mark * .2f, 0f), new Vector2(arm, thick), 45f * sign, tint);
                Bar(shape, new Vector2(mark * .5f + mark * .2f, 0f), new Vector2(arm, thick), -45f * sign, tint);
            }
            x += mark + 6f * s;

            var figure = HudPrimitives.Label(FigureName, chip, fontSize, tint, TextAlignmentOptions.MidlineLeft);
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) figure.font = bold;
            figure.text = Figure(score);
            figure.textWrappingMode = TextWrappingModes.NoWrap;
            figure.overflowMode = TextOverflowModes.Overflow;
            float figureWidth = Mathf.Ceil(figure.text.Length * fontSize * .66f + 4f);
            Place(figure.rectTransform, x, figureWidth, height);
            x += figureWidth;

            if (withLabel)
            {
                x += 8f * s;
                var label = HudPrimitives.Label(LabelName, chip, labelSize, UiTheme.Muted, TextAlignmentOptions.MidlineLeft);
                var medium = UiTheme.Font(UiTheme.Weight.Medium);
                if (medium != null) label.font = medium;
                label.text = Localisation.Text(Label);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                float labelWidth = Mathf.Ceil(label.text.Length * labelSize * .6f + 4f);
                Place(label.rectTransform, x, labelWidth, height);
                x += labelWidth;
            }

            chip.sizeDelta = new Vector2(Mathf.Ceil(x), height);
            return chip;
        }

        private static void Place(RectTransform rect, float x, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
            rect.pivot = new Vector2(0f, .5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, 0f);
        }

        private static void Bar(RectTransform shape, Vector2 centre, Vector2 size, float angle, Color tint)
        {
            var bar = new GameObject("Stroke", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            bar.SetParent(shape, false);
            bar.anchorMin = bar.anchorMax = new Vector2(0f, .5f);
            bar.pivot = new Vector2(.5f, .5f);
            bar.sizeDelta = size;
            bar.anchoredPosition = centre;
            bar.localRotation = Quaternion.Euler(0f, 0f, angle);
            var image = bar.GetComponent<Image>();
            image.color = tint;
            image.raycastTarget = false;
        }
    }
}
