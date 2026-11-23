using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The controls chip at the foot of every ceremony card (UI-UX plan D3b): one glass pill with a
    /// hairline edge, centred under the card, holding the card's controls line - the presses that
    /// move it on, in the words of the device last used. The takeover, the key ceremony, the live
    /// eviction, the jury's vote and the veto draw all build it here, so it looks, sits and reads the
    /// same on each (<see cref="CeremonyChipMetrics"/>).
    ///
    /// <para>The line keeps the name and the caption each card gave it before - "Controls",
    /// "Takeover dismiss", "Draw dismiss" - so the tests and screen readers that find it by either
    /// still do. The chip takes no pointer: a card never takes a click meant for the house, and its
    /// presses are read off the devices.</para>
    ///
    /// <para>It sizes itself around its words as drawn (a layout group and a size fitter), so a
    /// device change or the large-text preference grows it from the middle and never clips it.</para>
    /// </summary>
    public sealed class CeremonyControlsChip
    {
        /// <summary>The chip's rect, under the card's column.</summary>
        public const string ChipName = "Controls chip";

        private readonly HorizontalLayoutGroup row;
        private readonly LayoutElement box;
        private float padY;

        /// <summary>The chip's rect: anchored at the top centre of the card's column, sized around its words.</summary>
        public RectTransform Rect { get; }

        /// <summary>The controls line itself.</summary>
        public TMP_Text Label { get; }

        /// <summary>The chip's height at its current size.</summary>
        public float Height => CeremonyChipMetrics.Height(Label.fontSize, padY);

        private CeremonyControlsChip(RectTransform rect, TMP_Text label, HorizontalLayoutGroup row, LayoutElement box)
        {
            Rect = rect; Label = label; this.row = row; this.box = box;
        }

        /// <summary>Builds the chip under <paramref name="parent"/>, its line named <paramref name="labelName"/>, at the HUD's standard size.</summary>
        public static CeremonyControlsChip Build(RectTransform parent, string labelName)
        {
            var rect = HudPrimitives.Fill(ChipName, parent, UiTheme.GlassFill, UiTheme.ControlRadius);
            UiTheme.AddBorder(rect, UiTheme.ControlRadius, UiTheme.Hairline);
            // The edge stretches over the chip; the layout lays out the line alone.
            foreach (Transform child in rect) child.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1f);

            var row = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            var fit = rect.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var label = HudPrimitives.Label(labelName, rect, CeremonyChipMetrics.HudPt, UiTheme.Paper, TextAlignmentOptions.Center);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            var box = label.gameObject.AddComponent<LayoutElement>();

            foreach (var graphic in rect.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            var chip = new CeremonyControlsChip(rect, label, row, box);
            chip.Size(CeremonyChipMetrics.HudSize(1f), CeremonyChipMetrics.PadX, CeremonyChipMetrics.PadY);
            return chip;
        }

        /// <summary>The chip at the HUD's size for the large-text preference's <paramref name="scale"/>.</summary>
        public void SizeForHud(float scale) =>
            Size(CeremonyChipMetrics.HudSize(scale), CeremonyChipMetrics.PadX * scale, CeremonyChipMetrics.PadY * scale);

        /// <summary>The chip at a set's screen's size: the frame's own row, no padding above or below.</summary>
        public void SizeForScreen() => Size(CeremonyChipMetrics.ScreenPt, CeremonyChipMetrics.PadX * 2f, 0f);

        /// <summary>Words of <paramref name="pt"/> points in a box 1.35 times them, with the padding given.</summary>
        public void Size(float pt, float padX, float padY)
        {
            Label.fontSize = pt;
            float line = CeremonyChipMetrics.Line(pt);
            box.minHeight = line;
            box.preferredHeight = line;
            // A layout group pads in whole units.
            int x = Mathf.RoundToInt(padX), y = Mathf.RoundToInt(padY);
            this.padY = y;
            row.padding = new RectOffset(x, x, y, y);
            // The height is known now; the width follows the words as they are drawn.
            Rect.sizeDelta = new Vector2(Rect.sizeDelta.x, line + 2f * y);
            Fit();
        }

        /// <summary>Hangs the chip with its top <paramref name="top"/> under the top of the card's column (a negative offset, as the cards place their rows).</summary>
        public void PlaceAt(float top) => Rect.anchoredPosition = new Vector2(0f, top);

        /// <summary>Says <paramref name="caption"/> and fits the chip around it.</summary>
        public void Say(string caption)
        {
            Label.text = caption;
            Fit();
        }

        /// <summary>The chip shows or not, as a whole.</summary>
        public void Show(bool show) => Rect.gameObject.SetActive(show);

        // Laid out at once when the chip can be, so a test or a measurement in the same frame reads
        // the drawn size; otherwise the canvas lays it out as it next draws.
        private void Fit()
        {
            if (Rect.gameObject.activeInHierarchy) LayoutRebuilder.ForceRebuildLayoutImmediate(Rect);
        }
    }
}
