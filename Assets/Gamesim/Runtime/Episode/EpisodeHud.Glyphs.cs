using System.Collections.Generic;
using Gamesim.House;
using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The hints that follow the device (PLAN A, A4). On a pad, a control whose caption names a key
    /// - "Close  [Esc]", "Save now  [F5]", "Notebook [J]", "Go to diary room [R]" - wears a chip at
    /// its corner with the pad's button for the same action, read off the actions map; the caption
    /// keeps every one of its words, because the caption is the control's name to the tests and to a
    /// screen reader. A key or a click takes the chips away again. The help card's words swap the
    /// same way, in place.
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The chip's name, so a test can find it beside its control.</summary>
        public const string PadGlyphName = "Pad glyph";

        private bool padHints, glyphsShown;
        private TMP_Text helpText;
        private readonly List<Button> glyphScan = new List<Button>();

        /// <summary>Whether the hints are in the pad's words: the device last pressed on was a pad.</summary>
        public bool PadHints => padHints;

        /// <summary>The device last pressed on, as the director hears it: the help card is reworded at once, the chips at the frame's end.</summary>
        public void SetPadHints(bool pad)
        {
            if (pad == padHints) return;
            padHints = pad;
            if (helpText != null) helpText.text = Localisation.Text(InputGlossary.HelpCard(pad));
        }

        /// <summary>
        /// Every frame a pad is the device, and once more when it stops being: each live control whose
        /// caption names a key has its chip, shown on a pad and hidden otherwise. Controls a render
        /// built since the last frame get theirs here, whatever built them.
        /// </summary>
        private void SyncGlyphChips()
        {
            if ((!padHints && !glyphsShown) || canvas == null) return;
            canvas.GetComponentsInChildren(false, glyphScan);
            foreach (var button in glyphScan)
            {
                if (button == null) continue;
                string action = InputGlossary.CaptionAction(button.name);
                if (action == null) continue;
                var chip = button.transform.Find(PadGlyphName);
                if (chip != null) { if (chip.gameObject.activeSelf != padHints) chip.gameObject.SetActive(padHints); continue; }
                if (!padHints) continue;
                string glyph = HouseCameraActions.BindingWords(HouseInput.Actions.Asset.FindAction(action), HouseCameraActions.GamepadScheme);
                if (!string.IsNullOrEmpty(glyph)) GlyphChip(button, glyph);
            }
            glyphScan.Clear();
            glyphsShown = padHints;
        }

        /// <summary>
        /// A chip at the control's top-right corner, over its border and clear of its words, that
        /// takes no pointer and no layout: the control and its caption are exactly what they were.
        ///
        /// <para>It stands mostly above the control's top edge - 7 of its 18 inside - because every
        /// control that wears one centres its words on its height, and the shortest of them, the
        /// screens' compact Close (34 tall, its words 15, centred), keeps only about the top 8 of
        /// its height clear of them. Standing 12 inside, as it first did, the chip came within a
        /// pixel or two of the bracket of "[Esc]" there, by the font's measures, and of the rail's
        /// "Go to diary room [R]", which is fitted to its row. The PlayMode frames of A3 and A4
        /// (EpisodePlayModeTests.InputFrames.cs) hold it clear of every word around it.
        /// Its right edge stands 3 past the control's, inside the 8 a panel's column keeps from
        /// its viewport's mask, so a wide chip - Select - grows to the left rather than off the
        /// column.</para>
        /// </summary>
        private void GlyphChip(Button button, string glyph)
        {
            float s = FontScale;
            var size = new Vector2(Mathf.Max(22f, glyph.Length * 8f + 12f) * s, 18f * s);
            var chip = Panel(PadGlyphName, button.transform, new Color(Accent.r, Accent.g, Accent.b, .92f));
            Anchor(chip, new Vector2(1f, 1f), new Vector2(1f, .5f), new Vector2(3f * s, 2f * s), size);
            chip.GetComponent<Image>().raycastTarget = false;
            chip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            // A 12 in a box 18 tall: the 1.3 TMP needs, and more.
            var label = FixedText(chip, glyph, 12, UiTheme.Ink, Vector2.zero, size);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero; label.rectTransform.offsetMax = Vector2.zero;
            chip.SetAsLastSibling();
        }
    }
}
