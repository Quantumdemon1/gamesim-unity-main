using System.Collections.Generic;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// Your word (ACTIONS-DEALS-ALLIANCES-PLAN V1) on the HUD: the notebook page's cards - a
    /// houseguest's face and name over every commitment with them - and the breach warnings the
    /// decision screens carry.
    ///
    /// <para>A warning is a label, never a control. It stands in the strategy footer's strip, in a
    /// strip of its own in a step's column, as a line over a finalist's control or as a paragraph
    /// under a ballot, and its words are always one label named <see cref="BreachWarningName"/>, so
    /// a test finds it wherever a screen puts it. Nothing here moves, renames or covers a control a
    /// walk presses, and nothing here commits anything.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The name a breach warning's words carry, on every screen that shows one.</summary>
        public const string BreachWarningName = "Breach warning";
        /// <summary>The strip a step's column carries a breach warning in.</summary>
        public const string BreachStripName = "Breach strip";
        /// <summary>A Your word card's name: its section's prefix and the houseguest's full name.</summary>
        public const string WordOpenCardPrefix = "Your word · open · ", WordSettledCardPrefix = "Your word · settled · ";

        // ------------------------------------------------------------ the footer strip, in place

        /// <summary>The strip a breach warning holds this render, and what the strip said before it took it.</summary>
        private RectTransform breachStrip;
        private string breachRestText, breachRestName;
        private Color breachRestColour;
        private bool breachRestWarns, breachMadeStrip;

        /// <summary>
        /// Puts a breach warning in the strategy stage's footer strip, or takes it down, in place and
        /// without a render: the nomination picker lights its picks without rebuilding anything, so the
        /// strip changes with them the same way. The strip carries one warning at a time; a breach
        /// outranks what comes next and what moving on lets pass, because it is about the choice
        /// about to be committed, and taking it down gives the strip back exactly what it said - its
        /// words, colour and name. Null or empty words take it down. Off the strategy stage, nothing.
        /// </summary>
        public void FooterBreachWarning(string words)
        {
            if (modal == null || activityLayout != ActivityLayout.Strategy) return;
            // A rebuilt footer is a new strip: what this held belonged to the render before.
            bool holding = breachStrip != null && breachStrip == footerStrip;
            if (string.IsNullOrEmpty(words))
            {
                if (holding && footerWords != null)
                {
                    if (breachMadeStrip) footerStrip.gameObject.SetActive(false);
                    else
                    {
                        footerWords.text = breachRestText;
                        footerWords.color = breachRestColour;
                        footerWords.name = breachRestName;
                        footerWarns = breachRestWarns;
                    }
                }
                breachStrip = null;
                return;
            }
            if (!holding)
            {
                breachMadeStrip = footerStrip == null;
                if (breachMadeStrip) PinnedNote(words, BreachWarningName, true);
                else if (footerWords != null)
                {
                    breachRestText = footerWords.text;
                    breachRestColour = footerWords.color;
                    breachRestName = footerWords.name;
                    breachRestWarns = footerWarns;
                }
                breachStrip = footerStrip;
            }
            if (footerStrip == null || footerWords == null) return;
            footerStrip.gameObject.SetActive(true);
            footerWords.text = Localisation.Text(words);
            footerWords.color = UiTheme.Warning;
            footerWords.name = BreachWarningName;
            footerWarns = true;
        }

        // ------------------------------------------------------------ the column

        /// <summary>
        /// A breach warning in a strip of its own across a step's column, fronted by the kit's
        /// warning mark: as tall as its words need at the column's width, so it is measured into the
        /// step like the meeting's own strips and the row of cards gives way to it. Null words draw
        /// nothing and return null.
        /// </summary>
        public TMP_Text BreachStrip(string words)
        {
            if (content == null || string.IsNullOrEmpty(words)) return null;
            float s = FontScale, pad = 10f * s, glyph = 20f * s, side = 14f * s;
            var strip = new GameObject(BreachStripName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            strip.SetParent(content, false);
            EndScreenKit.Frame(strip, PackArt.Pack8InfoStrip, 10f * s, new Color(Surface.r, Surface.g, Surface.b, .92f),
                new Color(UiTheme.Warning.r, UiTheme.Warning.g, UiTheme.Warning.b, .55f));
            float left = side + glyph + 10f * s, inner = Mathf.Max(80f, ContentWidth() - left - side);
            var label = NewText(strip, words, 14, UiTheme.Warning);
            label.name = BreachWarningName;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            float need = Mathf.Max(Mathf.Ceil(label.GetPreferredValues(label.text, inner, 0f).y), 14f * 1.3f * s);
            float height = need + 2f * pad;
            var element = strip.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;
            Stretch(label.rectTransform, left, pad, side, pad);
            AutoSize(label, 11);
            // Kit 6's parts are white and take the colour of where they are used.
            var mark = EndScreenKit.Picture("Icon", strip, PackArt.KitIconWarning, "target", UiTheme.Warning,
                new Vector2(side + glyph * .5f, -height * .5f), glyph);
            if (mark != null) mark.color = UiTheme.Warning;
            return label;
        }

        /// <summary>
        /// A breach warning as a line in a finalist's column, over the choice's headline: the warning
        /// colour, centred, wrapping in the column. The card above takes up its height, so the
        /// headline, the warning and the control stay level with the other column's.
        /// </summary>
        private void BreachLine(string words)
        {
            if (content == null || string.IsNullOrEmpty(words)) return;
            var line = FlowText(words, 13, UiTheme.Warning);
            line.name = BreachWarningName;
            line.alignment = TextAlignmentOptions.Center;
        }

        // ------------------------------------------------------------ the page

        /// <summary>One line of a Your word card: the words and the colour of how it ended.</summary>
        public struct WordLine
        {
            public string Words;
            public Color Ink;

            public WordLine(string words, Color ink) { Words = words; Ink = ink; }
        }

        /// <summary>
        /// One houseguest's commitments on the Your word page: their face, their name and where
        /// they are, then a line for each commitment - what it is, what it binds, until when and how
        /// it ended, in that ending's colour as well as its words - and how many more the card is
        /// not showing. Named by its section's prefix and the houseguest's full name. Not a control.
        /// </summary>
        public RectTransform WordCard(string prefix, ContestantState actor, string status, IList<WordLine> lines, int more)
        {
            if (content == null || actor == null) return null;
            float s = FontScale, width = ContentWidth(), pad = 20f * s, face = 44f * s;
            float left = pad + face + 14f * s, inner = width - left - pad;
            var card = HudPrimitives.KitCard(prefix + actor.name, content, false, 14f);
            float y = 16f * s;
            var rim = HudPrimitives.Portrait(card, CharacterPortraits.Get(actor), UiTheme.Outline, face, 2f * s, false, actor);
            rim.name = "Face";
            Anchor(rim, new Vector2(0, 1), new Vector2(0, 1), new Vector2(pad, -y), rim.sizeDelta);
            var name = FixedText(card, actor.name, 18, Paper, new Vector2(left, -y), new Vector2(inner * .6f, 18f * 1.3f * s));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(name, 12);
            if (!string.IsNullOrEmpty(status))
            {
                var side = FixedText(card, status, 13, UiTheme.Muted, new Vector2(-pad, -(y + 4f * s)), new Vector2(inner * .4f, 13f * 1.3f * s));
                side.alignment = TextAlignmentOptions.Right;
                side.rectTransform.anchorMin = side.rectTransform.anchorMax = side.rectTransform.pivot = new Vector2(1, 1);
            }
            y += 30f * s;
            if (lines != null)
                foreach (var line in lines)
                    y = PlacedCopy(card, line.Words, 15, UiTheme.Weight.Regular, line.Ink, left, y, inner) + 4f * s;
            if (more > 0)
                y = PlacedCopy(card, "and " + more + " more with them in the season's record", 13,
                    UiTheme.Weight.Regular, UiTheme.Muted, left, y, inner) + 2f * s;
            y = Mathf.Max(y, 16f * s + face + 8f * s);
            var size = card.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = y + 12f * s;
            return card;
        }
    }
}
