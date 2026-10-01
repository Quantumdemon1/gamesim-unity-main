using System;
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
    /// over a ballot's cards, and its words are one label named <see cref="BreachWarningName"/>, so a
    /// test finds it wherever a screen puts it - except in a footer strip that already warns, where
    /// the breach leads the one warning line and the line keeps the name it had. Nothing here
    /// moves, renames or covers a control a walk presses, and nothing here commits anything.</para>
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

        /// <summary>
        /// The line a breach warning put in the footer strip's warning slot, and the line that slot
        /// held before it, which the breach gives back exactly. The slot is the breach's only while it
        /// still holds that very line: a rebuild empties the slots, and a later warning takes it.
        /// </summary>
        private FooterLine breachLine, breachRest;

        /// <summary>
        /// Puts a breach warning in the strategy stage's footer strip, or takes it down, in place and
        /// without a render: the nomination picker lights its picks without rebuilding anything, so the
        /// strip changes with them the same way. Null or empty words take it down.
        ///
        /// <para>The breach is a line of the warning's rank (<see cref="FooterRank.Warning"/>), so it
        /// takes the strip alone: what comes next and what moving on costs are hidden under it, and
        /// come back when it goes. A warning already in that slot - what committing lets pass - is
        /// never taken away: the one line says the breach and then that warning, under the resting
        /// warning's own name, so it is found where it always was. When that line cannot be said in
        /// the strip even at its smallest text, the slot says <paramref name="brief"/> instead, the
        /// short form that counts them. Taking the breach down gives the slot back exactly the line it
        /// held and draws the strip again from its lines; a strip with none left - one the breach
        /// built - is hidden again. Off the strategy stage, nothing.</para>
        /// </summary>
        public void FooterBreachWarning(string words, string brief = null)
        {
            if (modal == null || activityLayout != ActivityLayout.Strategy) return;
            int slot = (int)FooterRank.Warning;
            if (breachLine != null && !ReferenceEquals(footerLines[slot], breachLine)) breachLine = breachRest = null;
            if (string.IsNullOrEmpty(words))
            {
                TakeDownBreach();
                return;
            }
            if (breachLine == null) breachRest = footerLines[slot];
            string said = Localisation.Text(words);
            PutBreach(breachRest != null
                ? new FooterLine(said + " " + Localisation.Text(breachRest.Words), breachRest.Name, true)
                : new FooterLine(said, BreachWarningName, true));
            if (!string.IsNullOrEmpty(brief) && Overflows(footerWords))
                PutBreach(new FooterLine(Localisation.Text(brief), breachLine.Name, true));
        }

        /// <summary>
        /// A breach's line in the strip's warning slot, drawn as <see cref="PinnedNote(string, string, FooterRank, bool)"/>
        /// draws a line: the strip from its lines, the footer's row, the scroll's foot. A strip an
        /// earlier breach built and hid again shows again.
        /// </summary>
        private void PutBreach(FooterLine line)
        {
            breachLine = footerLines[(int)FooterRank.Warning] = line;
            DrawFooterStrip();
            LayoutStrategyFooter();
            ApplyPinnedInset();
            if (footerStrip != null) footerStrip.gameObject.SetActive(true);
        }

        /// <summary>
        /// The breach comes down: the warning slot gets back exactly the line it held, the strip is
        /// drawn again from its lines - what comes next and what moving on costs come back with
        /// them - and a strip with no line left, as one the breach built has none, is hidden again.
        /// </summary>
        private void TakeDownBreach()
        {
            if (breachLine == null) return;
            footerLines[(int)FooterRank.Warning] = breachRest;
            breachLine = breachRest = null;
            DrawFooterStrip();
            LayoutStrategyFooter();
            ApplyPinnedInset();
            if (footerStrip != null && Array.TrueForAll(footerLines, line => line == null)) footerStrip.gameObject.SetActive(false);
        }

        /// <summary>
        /// Whether a label's words run past its box even at the smallest size its auto-size allows,
        /// measured now rather than at the canvas's next pass. The box must be where it will stand.
        /// </summary>
        private static bool Overflows(TMP_Text label)
        {
            if (label == null) return false;
            label.ForceMeshUpdate(true);
            return label.isTextOverflowing;
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

        /// <summary>
        /// A breach warning over a ballot's cards, under the ballot's line (<see cref="BallotCards"/>):
        /// the warning colour, centred, as tall as its words need at the column's width. Returns
        /// the height it takes in the column, its gap included, so the cards can give it the room
        /// and Confirm still stands in view under them; 0 for no warning.
        /// </summary>
        private float BallotWarningLine(string words)
        {
            if (content == null || string.IsNullOrEmpty(words)) return 0f;
            var line = FlowText(words, 15, UiTheme.Warning);
            line.name = BreachWarningName;
            line.alignment = TextAlignmentOptions.Center;
            var column = content.GetComponent<VerticalLayoutGroup>();
            float gap = column != null ? column.spacing : 12f;
            return Mathf.Max(Mathf.Ceil(line.GetPreferredValues(line.text, ContentWidth(), 0f).y), 15f * FontScale + 8f) + gap;
        }

        // ------------------------------------------------------------ the page

        /// <summary>
        /// A door to another notebook page in this page's head, at its right end on the title's
        /// line - the notes page's "Your word", and the way back from it - as wide as its caption.
        /// A control named and captioned by <paramref name="caption"/> like any other, outside the
        /// page's column, so it is never a filter among the filters and never the control the page
        /// opens on. Nothing, and null, on a page without the notebook's head.
        /// </summary>
        public Button PageDoor(string caption, Action open)
        {
            if (modal == null || string.IsNullOrEmpty(caption) || open == null) return null;
            // The head this render built: the panel is new each render, so its child is the live one.
            RectTransform head = null;
            foreach (Transform child in modal)
                if (child.name == NotebookHeaderName) head = (RectTransform)child;
            if (head == null) return null;
            float s = FontScale, height = 34f * s;
            var rect = Chrome(caption, head, UiTheme.Emphasis.Interactive);
            HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
            var button = FinishButton(rect, caption, open, 14f, 14f);
            var label = button.GetComponentInChildren<TMP_Text>();
            float width = 120f * s;
            if (label != null)
            {
                label.fontSize = Mathf.RoundToInt(15 * s);
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                AutoSize(label, 12);
                width = Mathf.Max(width, Mathf.Ceil(label.GetPreferredValues(label.text).x) + 28f + 8f * s);
            }
            // On the title's line at the head's right end - short of Close, which the head stops
            // short of - and over the page's subtitle.
            Anchor(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -36f * s), new Vector2(width, height));
            return button;
        }

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
