using System;
using System.Collections.Generic;
using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The strategy stage (PACK8-PASS-PLAN A3): one layout for the week's four strategy screens - the
    /// nomination, the veto's draw, the veto meeting and the campaign. They were the stage, which
    /// keeps the hidden cast strip's band under it and the control hint over it, so a screen that
    /// held two story beats, a ceremony and a row of faces was 638 units tall at the resting text
    /// size and scrolled (the owner's screenshots 66 to 80).
    ///
    /// <para>The frame runs from under the top bar to the frame's foot and from the rail's column to
    /// 14 short of the right edge: Pack 8's shells, 1360x800 at the 1600x900 reference. The status
    /// line moves into the foot of the left gutter, under the rail, out of its way. Inside it are
    /// three regions: a fixed header under the phase band for status cards and a tracker (children
    /// of the panel, never of 'Episode content', holding no control but a tracker's step that opens
    /// something); the scroll, which holds one step; and a footer - the way on, pinned, a secondary
    /// slot on its left, and a strip for what comes next, or for the storylines moving on lets pass.</para>
    ///
    /// <para>Free time, the diary room, a Social house event's band and every other layout are not
    /// this one and do not change with it.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>How far short of the frame's right edge the stage stops: the rail's own margin, mirrored.</summary>
        public const float StrategyEdge = 14f;
        /// <summary>How far above the frame's foot the stage stops, and the status line with it.</summary>
        public const float StrategyFoot = 20f;
        /// <summary>The stage's fixed regions and the footer's strip, by the names a test finds them by.</summary>
        public const string StrategyHeaderName = "Strategy header", StrategyStripName = "Footer strip", StepTrackerName = "Step tracker";
        /// <summary>The name the footer strip's words carry when the caller gives them none.</summary>
        public const string UpNextName = "Up next";

        /// <summary>Where the header starts: under the 62-unit phase band and a gap. The control hint's row, which the stage stands down.</summary>
        private const float StrategyBandFoot = 72f;
        private const float StrategyGap = 10f;
        /// <summary>The narrowest the way on and the secondary slot stand when the footer's row is shared, at the resting text size.</summary>
        private const float StrategyPrimaryWidth = 320f, StrategySecondaryWidth = 200f;
        /// <summary>A shell's corner in canvas units: a frame, so it does not grow with the text.</summary>
        private const float StrategyShellBorder = 16f;

        private RectTransform strategyHeader, footerStrip, footerSecondary;
        private float strategyHeaderUsed;
        private TMP_Text footerWords;
        private FooterRank footerRank;

        /// <summary>
        /// What the footer strip may say, lowest first: what comes next; what moving on costs (the
        /// offers it lets lapse, the window's unused actions); and what moving on lets pass, the
        /// storylines' warning. The strip says one line a render: a line never takes it from a
        /// higher rank, and a later line of the same rank does.
        /// </summary>
        public enum FooterRank { UpNext, Notice, Warning }

        /// <summary>What a rebuild throws away with the panel.</summary>
        private void ForgetStrategyStage()
        {
            strategyHeader = footerStrip = footerSecondary = null;
            strategyHeaderUsed = 0f;
            footerWords = null;
            footerRank = FooterRank.UpNext;
        }

        /// <summary>
        /// Takes the strategy stage for this render, framed in <paramref name="shell"/> - one of Pack
        /// 8's three shells - when the pack is there; the panel's own glass when it is not, rather
        /// than a second, drawn card over the first.
        /// </summary>
        public void StrategyStage(string shell)
        {
            SetActivityLayout(ActivityLayout.Strategy);
            if (modal == null || activityLayout != ActivityLayout.Strategy || UiTheme.Pack(shell) == null) return;
            EndScreenKit.Frame(modal, shell, StrategyShellBorder, new Color(Surface.r, Surface.g, Surface.b, .92f));
        }

        /// <summary>The frame: under the top bar to the foot, the rail's column to the edge; above the status band when the gutter cannot take the status line.</summary>
        private Rect StrategyRect(float canvasWidth, float canvasHeight, bool toTheFoot)
        {
            float top = canvasHeight - ActivityHeadroom;
            float foot = toTheFoot ? StrategyFoot : ModalLift;
            float width = Mathf.Max(420f, canvasWidth - LeftColumnX - StrategyEdge);
            return new Rect(LeftColumnX, foot, width, Mathf.Max(300f, top - foot));
        }

        private void StrategyLayout(float canvasWidth, float canvasHeight)
        {
            bool toTheFoot = StatusIntoGutter(canvasHeight);
            var frame = StrategyRect(canvasWidth, canvasHeight, toTheFoot);
            modal.anchorMin = modal.anchorMax = Vector2.zero;
            modal.pivot = Vector2.zero;
            modal.anchoredPosition = frame.position;
            modal.sizeDelta = frame.size;
            var ground = modal.GetComponent<Image>();
            if (ground != null) { var c = ground.color; ground.color = new Color(c.r, c.g, c.b, Mathf.Max(c.a, .97f)); }
            HideStageChrome();
            // The stage's reading column, as wide as it was: the screens are taller, not wider, until
            // each is laid out for the frame (PACK8-PASS-PLAN wave B).
            CapContent(StageColumnWidth * FontScale);
            // The hint stands down, as over a house event; FitStandardPanel still says whether it scrolls.
            var hint = modal.Find(PanelHintName);
            if (hint != null) hint.gameObject.SetActive(false);
            if (modalScroll != null)
            {
                Stretch((RectTransform)modalScroll.transform, 20f, StrategyBandFoot + strategyHeaderUsed, 20f, 22f);
                modalScroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>
        /// Moves the status line into the foot of the left gutter, under the rail, so the stage can
        /// run to the frame's foot and the line is still where a commit's outcome and a rejected
        /// commit's reason are said. The last child of each name, not Find's first: Begin's rebuild
        /// leaves the last render's chrome under the canvas, inactive, until the frame ends, ahead of
        /// the new. False, with the line left where it was, when the gutter under the rail is too
        /// short to hold it.
        ///
        /// <para>The gutter is a fraction of the width the line had over the strip, where it grew to
        /// fit its words, and an outcome with "Saved locally." after it is several of the gutter's
        /// lines. So the line grows upward instead: as tall as its words need at their own size, no
        /// taller than the gutter under the rail, where the auto-size takes them down a size or two.
        /// Only words the gutter cannot hold even then end in an ellipsis, never mid-word.</para>
        /// </summary>
        private bool StatusIntoGutter(float canvasHeight)
        {
            RectTransform status = null, rail = null;
            foreach (Transform child in canvas.transform)
            {
                if (child.name == "Status") status = (RectTransform)child;
                else if (child.name == "Rail ground") rail = (RectTransform)child;
            }
            if (status == null) return true;
            float height = StatusHeight * FontScale;
            // The rail's ground hangs from the frame's top: its foot is how far down it starts plus its height.
            float railFoot = rail != null ? -rail.anchoredPosition.y + rail.sizeDelta.y : IconRail.Top + IconRail.Height(6, FontScale);
            float room = canvasHeight - railFoot - FrameGapUnits - StrategyFoot;
            if (room < height) return false;
            var caption = status.GetComponentInChildren<TMP_Text>();
            if (caption != null)
            {
                // The caption is stretched over the line, inset on every side (Begin), so it follows
                // the line's height; it is measured at its own size, not the one the auto-size picked
                // for the box it was built in.
                var box = caption.rectTransform;
                float inner = IconRail.Width - box.offsetMin.x + box.offsetMax.x, ends = box.offsetMin.y - box.offsetMax.y;
                bool autoSize = caption.enableAutoSizing; float size = caption.fontSize;
                caption.enableAutoSizing = false; caption.fontSize = caption.fontSizeMax;
                float need = Mathf.Ceil(caption.GetPreferredValues(caption.text, inner, 0f).y) + ends;
                caption.fontSize = size; caption.enableAutoSizing = autoSize;
                height = Mathf.Clamp(need, height, room);
                caption.overflowMode = TextOverflowModes.Ellipsis;
            }
            // Anchored at its foot, so a taller line grows up towards the rail.
            Anchor(status, Vector2.zero, Vector2.zero, new Vector2(14f, StrategyFoot), new Vector2(IconRail.Width, height));
            return true;
        }

        // ------------------------------------------------------------ the header

        /// <summary>
        /// A fixed row of the stage's header, under the phase band: <paramref name="height"/> units
        /// of the frame's width that never scroll. Each row goes under the last and the scroll starts
        /// under them all. Null off the strategy stage. Nothing put in a row may be a control but a
        /// tracker's step that opens something: the panel opens on its column, not on its header.
        /// </summary>
        public RectTransform StrategyHeaderRow(string name, float height)
        {
            if (modal == null || activityLayout != ActivityLayout.Strategy || height <= 0f) return null;
            if (strategyHeader == null)
            {
                strategyHeader = new GameObject(StrategyHeaderName, typeof(RectTransform)).GetComponent<RectTransform>();
                strategyHeader.SetParent(modal, false);
                strategyHeader.anchorMin = new Vector2(0f, 1f); strategyHeader.anchorMax = new Vector2(1f, 1f);
                strategyHeader.pivot = new Vector2(.5f, 1f);
            }
            float side = PinnedSide(), top = strategyHeaderUsed;
            var row = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(strategyHeader, false);
            row.anchorMin = new Vector2(0f, 1f); row.anchorMax = new Vector2(1f, 1f); row.pivot = new Vector2(.5f, 1f);
            row.offsetMin = new Vector2(side, -(top + height));
            row.offsetMax = new Vector2(-side, -top);
            strategyHeaderUsed += height + StrategyGap * FontScale;
            strategyHeader.offsetMin = new Vector2(0f, -(StrategyBandFoot + strategyHeaderUsed));
            strategyHeader.offsetMax = new Vector2(0f, -StrategyBandFoot);
            if (modalScroll != null)
            {
                var scroll = (RectTransform)modalScroll.transform;
                scroll.offsetMax = new Vector2(scroll.offsetMax.x, -(StrategyBandFoot + strategyHeaderUsed));
            }
            return row;
        }

        /// <summary>
        /// The height a row of status cards takes: a label, a value and, at the resting text size, a
        /// line under them, each in a box 1.3 times its type - Inter draws nothing in a box under 1.21.
        /// The larger text drops the line, which is what lets the row stay one row.
        /// </summary>
        public float StatusCardRowHeight(bool withLine) =>
            ((12f + 17f + (withLine && FontScale <= 1.05f ? 12f : 0f)) * 1.3f + 12f) * FontScale;

        /// <summary>
        /// One of a header row's status cards (mockup 72), the <paramref name="index"/>th of
        /// <paramref name="count"/> across it: a glyph, a small label in the card's tint, the value,
        /// and a line under it at the resting text size. Every word is the caller's, read from what
        /// the house knows or the player's own state; the card only frames it. Not a control.
        /// </summary>
        public RectTransform StatusCard(RectTransform row, int index, int count, string label, string value, string line,
            string frame, string icon, string fallbackIcon, Color tint)
        {
            if (row == null || count <= 0) return null;
            float s = FontScale, gap = 12f * s;
            float width = (row.rect.width - (count - 1) * gap) / count, height = row.rect.height;
            var card = EndScreenKit.Box("Status card · " + label, row, index * (width + gap), 0f, width, height);
            EndScreenKit.Frame(card, frame, 10f * s, new Color(Surface.r, Surface.g, Surface.b, .92f), new Color(tint.r, tint.g, tint.b, .55f));
            float pad = 10f * s, glyph = Mathf.Min(34f * s, height - 2f * pad), left = pad;
            if (EndScreenKit.Picture("Icon", card, icon, fallbackIcon, tint, new Vector2(pad + glyph * .5f, -height * .5f), glyph) != null)
                left += glyph + 10f * s;
            float inner = Mathf.Max(40f, width - left - pad);
            bool withLine = !string.IsNullOrEmpty(line) && s <= 1.05f;
            float labelBox = 12f * 1.3f * s, valueBox = 17f * 1.3f * s, lineBox = withLine ? 12f * 1.3f * s : 0f;
            float y = Mathf.Max(0f, (height - labelBox - valueBox - lineBox) * .5f);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            var caption = FixedText(card, label, 12, tint, new Vector2(left, -y), new Vector2(inner, labelBox));
            caption.name = "Label";
            caption.characterSpacing = 1f;
            if (semibold != null) caption.font = semibold;
            AutoSize(caption, 10);
            y += labelBox;
            var big = FixedText(card, value, 17, Paper, new Vector2(left, -y), new Vector2(inner, valueBox));
            big.name = "Value";
            if (semibold != null) big.font = semibold;
            AutoSize(big, 12);
            y += valueBox;
            if (withLine)
            {
                var under = FixedText(card, line, 12, UiTheme.Muted, new Vector2(left, -y), new Vector2(inner, lineBox));
                under.name = "Line";
                AutoSize(under, 10);
            }
            return card;
        }

        /// <summary>Where a tracker's step stands this week.</summary>
        public enum TrackerState { Done, Current, Next }

        /// <summary>
        /// One step of a tracker: its title, the word for where it stands, and what pressing it opens.
        /// Only a step that opens something is a control, and then its title is its caption.
        /// </summary>
        public struct TrackerStep
        {
            public string Title, Status;
            public TrackerState State;
            public Action Open;

            public TrackerStep(string title, string status, TrackerState state, Action open = null)
            {
                Title = title; Status = status; State = state; Open = open;
            }
        }

        /// <summary>A tracker's height: a title over its status word, each in a box 1.3 times its type.</summary>
        public float StepTrackerHeight => 48f * FontScale;

        /// <summary>
        /// The week's steps across a header row (mockup 72): each on Pack 8's step frame for where it
        /// stands - the three share one body, so a step that moves on changes its sprite, never its
        /// rect - with a glyph and a word as well as the colour, because a state carried by colour
        /// alone is one some players never receive. The steps are the caller's, built from what has
        /// happened this week; a step with nothing to open is not a control.
        /// </summary>
        public RectTransform StepTracker(RectTransform row, IReadOnlyList<TrackerStep> steps, Color current)
        {
            if (row == null || steps == null || steps.Count == 0) return null;
            float s = FontScale, gap = 8f * s, height = row.rect.height;
            float width = (row.rect.width - (steps.Count - 1) * gap) / steps.Count;
            var tracker = EndScreenKit.Box(StepTrackerName, row, 0f, 0f, row.rect.width, height);
            bool stacked = s <= 1.05f;
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                Color tint = step.State == TrackerState.Done ? UiTheme.Positive : step.State == TrackerState.Current ? current : UiTheme.Muted;
                RectTransform cell;
                if (step.Open != null)
                {
                    // A control carries its caption as its name, as every row of the panel does.
                    cell = Chrome(step.Title, tracker, UiTheme.Emphasis.Interactive);
                    EndScreenKit.Place(cell, i * (width + gap), 0f, width, height);
                }
                else cell = EndScreenKit.Box("Step · " + step.Title, tracker, i * (width + gap), 0f, width, height);
                string frame = step.State == TrackerState.Done ? PackArt.Pack8PhaseComplete
                    : step.State == TrackerState.Current ? PackArt.Pack8PhaseCurrent : PackArt.Pack8PhaseNext;
                EndScreenKit.Frame(cell, frame, 10f * s, new Color(Surface.r, Surface.g, Surface.b, .9f), new Color(tint.r, tint.g, tint.b, .5f));
                float pad = 10f * s, side = Mathf.Min(26f * s, height - 12f * s);
                string icon = step.State == TrackerState.Done ? PackArt.KitIconCheck
                    : step.State == TrackerState.Current ? PackArt.Pack8IconTarget : PackArt.KitTimelineRing;
                var glyph = EndScreenKit.Picture("Step mark", cell, icon, step.State == TrackerState.Current ? "target" : null, tint,
                    new Vector2(pad + side * .5f, -height * .5f), side);
                // Kit 6's parts are white and tinted where they are used; Pack 8's target bakes its own colour.
                if (glyph != null && icon.StartsWith("Kit6_", StringComparison.Ordinal)) glyph.color = tint;
                float left = pad + (glyph != null ? side + 8f * s : 0f), inner = Mathf.Max(40f, width - left - pad);
                float titleBox = 15f * 1.3f * s, statusBox = 12f * 1.3f * s;
                var title = FixedText(cell, step.Title, 15, step.State == TrackerState.Current ? tint : Paper,
                    new Vector2(left, -(stacked ? (height - titleBox - statusBox) * .5f : (height - titleBox) * .5f)),
                    new Vector2(stacked ? inner : inner * .62f, titleBox));
                title.textWrappingMode = TextWrappingModes.NoWrap;
                title.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(title, 11);
                if (string.IsNullOrEmpty(step.Status)) { if (step.Open != null) Pressable(cell, step.Open); continue; }
                // Under the title at the resting text size; beside it at the larger, in the row's height.
                var word = stacked
                    ? FixedText(cell, step.Status, 12, tint, new Vector2(left, -((height - titleBox - statusBox) * .5f + titleBox)), new Vector2(inner, statusBox))
                    : FixedText(cell, step.Status, 12, tint, new Vector2(left + inner * .62f + 6f * s, -(height - statusBox) * .5f), new Vector2(inner * .38f - 6f * s, statusBox));
                word.name = "Step status";
                AutoSize(word, 10);
                if (step.Open != null) Pressable(cell, step.Open);
            }
            return tracker;
        }

        // ------------------------------------------------------------ the footer

        /// <summary>
        /// The footer's secondary slot, left of the way on: a real way back when the screen has one,
        /// never a generic Back (PACK8-PASS-PLAN decision 2). One a render; a second, or one off the
        /// strategy stage, is an ordinary row, as a second pinned action is.
        /// </summary>
        public Button PinnedSecondary(string caption, Action action)
        {
            if (modal == null || modalScroll == null || activityLayout != ActivityLayout.Strategy || footerSecondary != null)
                return Action(caption, action);
            var rect = Chrome(caption, modal);
            var button = FinishButton(rect, caption, action);
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) { label.alignment = TextAlignmentOptions.Center; AutoSize(label, 14); }
            footerSecondary = rect;
            LayoutStrategyFooter();
            ApplyPinnedInset();
            return button;
        }

        /// <summary>
        /// The footer's strip on the strategy stage (mockup 72's Up next): a line in the accent of
        /// what comes next or, when <paramref name="warning"/>, what moving on lets pass in the warning
        /// colour - which outranks it, so an Up next line never covers a warning. The words are one
        /// label named <paramref name="name"/>, so the warning keeps the name a test finds it by. Off
        /// the strategy stage the line is a paragraph in the column, where such a line always was.
        /// </summary>
        public void PinnedNote(string words, string name, bool warning) =>
            PinnedNote(words, name, warning ? FooterRank.Warning : FooterRank.UpNext, warning);

        /// <summary>
        /// A line for the footer's strip at its <paramref name="rank"/> (<see cref="FooterRank"/>):
        /// it takes the strip unless a line of a higher rank already holds it this render, in the
        /// warning colour when <paramref name="warningColour"/>, in the accent otherwise.
        /// </summary>
        public void PinnedNote(string words, string name, FooterRank rank, bool warningColour)
        {
            if (string.IsNullOrEmpty(words)) return;
            if (modal == null || activityLayout != ActivityLayout.Strategy)
            {
                NamedParagraph(name ?? UpNextName, words, warningColour ? UiTheme.Warning : Accent);
                return;
            }
            if (footerStrip != null && rank < footerRank) return;
            float s = FontScale, height = PinnedHeight * s;
            if (footerStrip == null)
            {
                footerStrip = new GameObject(StrategyStripName, typeof(RectTransform)).GetComponent<RectTransform>();
                footerStrip.SetParent(modal, false);
                EndScreenKit.Frame(footerStrip, PackArt.Pack8InfoStrip, 10f * s, new Color(Surface.r, Surface.g, Surface.b, .92f));
                float side = Mathf.Min(26f * s, height - 16f * s);
                var glyph = EndScreenKit.Picture("Icon", footerStrip, PackArt.Pack8IconInfo, "bulb", Accent,
                    new Vector2(14f * s + side * .5f, -height * .5f), side);
                footerWords = NewText(footerStrip, words, 13, Accent);
                footerWords.alignment = TextAlignmentOptions.MidlineLeft;
                AutoSize(footerWords, 11);
                Stretch(footerWords.rectTransform, 14f * s + (glyph != null ? side + 12f * s : 0f), 6f * s, 14f * s, 6f * s);
            }
            footerWords.text = Localisation.Text(words);
            footerWords.color = warningColour ? UiTheme.Warning : Accent;
            footerWords.name = name ?? UpNextName;
            footerRank = rank;
            LayoutStrategyFooter();
            ApplyPinnedInset();
        }

        /// <summary>
        /// Lays the footer's row out right to left: the way on, then the secondary slot, then the
        /// strip in whatever is left. Alone, the way on keeps the column's width it has always had.
        /// </summary>
        private void LayoutStrategyFooter()
        {
            if (modal == null || activityLayout != ActivityLayout.Strategy) return;
            if (footerStrip == null && footerSecondary == null) return;
            float s = FontScale, side = PinnedSide(), gap = 12f * s, height = PinnedHeight * s;
            float column = Mathf.Max(0f, modal.sizeDelta.x - 2f * side), right = side;
            if (pinnedAction != null)
            {
                // As wide as its caption needs past the chevron, and never under the mockup's width.
                var caption = pinnedAction.GetComponentInChildren<TMP_Text>();
                float wanted = caption != null ? caption.GetPreferredValues(caption.text).x + 16f + 16f + 28f * s : 0f;
                float width = Mathf.Min(Mathf.Max(StrategyPrimaryWidth * s, wanted), column * .5f);
                FootAt(pinnedAction, right, width, height);
                right += width + gap;
            }
            if (footerSecondary != null)
            {
                var caption = footerSecondary.GetComponentInChildren<TMP_Text>();
                float wanted = caption != null ? caption.GetPreferredValues(caption.text).x + 32f : 0f;
                float width = Mathf.Min(Mathf.Max(StrategySecondaryWidth * s, wanted), column * .3f);
                FootAt(footerSecondary, right, width, height);
                right += width + gap;
            }
            if (footerStrip != null)
            {
                footerStrip.anchorMin = new Vector2(0f, 0f); footerStrip.anchorMax = new Vector2(1f, 0f);
                footerStrip.pivot = new Vector2(.5f, 0f);
                footerStrip.offsetMin = new Vector2(side, PinnedMargin);
                footerStrip.offsetMax = new Vector2(-right, PinnedMargin + height);
            }
        }

        /// <summary>Stands a footer part on the row, <paramref name="right"/> in from the panel's right edge.</summary>
        private static void FootAt(RectTransform rect, float right, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-right, PinnedMargin);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
