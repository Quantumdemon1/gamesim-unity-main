using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeHud
    {
        /// <summary>The story card's choices, so a test can find them the way it finds a named panel.</summary>
        public const string StoryChoicesName = "Story choices";

        /// <summary>The confirm on a rule break: a fixed caption, so a test and a screen reader can find it.</summary>
        public const string StoryConfirmCaption = "Do it";
        public const string StoryBackCaption = "Back";

        /// <summary>One option on a story card, as the director has worked it out.</summary>
        public struct StoryChoice
        {
            /// <summary>The option's own constant label: the control's name.</summary>
            public string Caption;
            public string Description;
            /// <summary>The risk word: low, some or high risk.</summary>
            public string Risk;
            /// <summary>"about 7 in 10", or null for an option that is certain.</summary>
            public string Odds;
            /// <summary>What else to know before pressing: it costs an action, it is a rule break, why it is locked.</summary>
            public string Note;
            public bool Locked;
            public Action Choose;
        }

        /// <summary>
        /// A story beat's header: the arc's eyebrow, the beat's title, and the moment in words with
        /// names in them - rendered now, from the beat's content id, so a save never carries a
        /// stale name.
        /// </summary>
        public void StoryBeatHeader(string eyebrow, string title, string narrative)
        {
            SetActivityLayout(ActivityLayout.HouseEvent);
            var mark = DecisionText(content, (eyebrow ?? "STORY").ToUpperInvariant(), 12, UiTheme.Joke);
            mark.characterSpacing = 8f;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            var heading = DecisionText(content, title, 22, Paper);
            if (semibold != null) heading.font = semibold;
            var story = DecisionText(content, narrative, 16, UiTheme.Muted);
            story.fontStyle = FontStyles.Italic;
        }

        /// <summary>
        /// The options as tiles, two to a row: the caption the control is known by, what it means,
        /// the risk as a coloured word, and a line for the odds and anything else to know first. A
        /// locked option is drawn and cannot be pressed, with its reason on it - an option the
        /// player cannot see is one they cannot learn exists.
        /// </summary>
        public void StoryChoices(IList<StoryChoice> choices)
        {
            DecisionText(content, "What do you do?", 16, UiTheme.Muted).alignment = TextAlignmentOptions.Center;
            int columns = choices.Count > 1 ? 2 : 1;
            float spacing = 10f * FontScale;
            float cellWidth = (ContentWidth() - spacing * (columns - 1)) / columns;
            // A caption, two lines of description and the odds line under them.
            float cellHeight = 96f * FontScale;
            int rows = Mathf.CeilToInt(choices.Count / (float)columns);
            var grid = new GameObject(StoryChoicesName, typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
            grid.SetParent(content, false);
            var layout = grid.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(cellWidth, cellHeight);
            layout.spacing = new Vector2(spacing, spacing);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;
            var size = grid.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = rows * cellHeight + Mathf.Max(0, rows - 1) * spacing;

            foreach (var choice in choices)
            {
                var emphasis = choice.Locked ? UiTheme.Emphasis.Resting : UiTheme.Emphasis.Interactive;
                var rect = Chrome(choice.Caption, grid, emphasis);
                HudEmphasis.Promote(rect, emphasis);
                var button = Pressable(rect, choice.Choose);
                button.interactable = !choice.Locked;
                var colours = button.colors;
                colours.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
                colours.selectedColor = colours.highlightedColor;
                button.colors = colours;

                float s = FontScale;
                var tile = Panel("Choice tile", rect, UiTheme.SurfaceRaised, 8);
                Anchor(tile, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12f * s, -12f * s), new Vector2(40f * s, 40f * s));
                tile.GetComponent<Image>().raycastTarget = false;
                HudPrimitives.Glyph("Choice mark", tile, choice.Locked ? "lock" : ChoiceGlyph(choice.Caption), Accent, new Vector2(8f * s, -8f * s), 24f * s);

                var riskWord = FixedText(rect, choice.Risk, 11, RiskTint(choice.Risk), Vector2.zero, new Vector2(78f * s, 16f * s));
                Anchor(riskWord.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10f * s, -8f * s), new Vector2(78f * s, 16f * s));
                riskWord.alignment = TextAlignmentOptions.Right;

                float text = 64f * s;
                var caption = NewText(rect, choice.Caption, 16, choice.Locked ? UiTheme.Muted : Paper);
                var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
                if (semibold != null) caption.font = semibold;
                AutoSize(caption, 11);
                Anchor(caption.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(text, -8f * s),
                    new Vector2(cellWidth - text - 92f * s, 22f * s));
                if (!string.IsNullOrEmpty(choice.Description))
                {
                    var line = NewText(rect, choice.Description, 13, UiTheme.Muted);
                    AutoSize(line, 10);
                    Anchor(line.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(text, -32f * s),
                        new Vector2(cellWidth - text - 12f * s, 38f * s));
                }
                // The odds and the note sit on their own line: never in the caption, which is how a
                // test and a screen reader find the control.
                string foot = string.Join("  ·  ", new[] { choice.Odds, choice.Note }.Where(x => !string.IsNullOrEmpty(x)));
                if (foot.Length > 0)
                {
                    var odds = NewText(rect, foot, 12, choice.Locked ? UiTheme.Muted : Accent);
                    AutoSize(odds, 9);
                    Anchor(odds.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(text, 8f * s),
                        new Vector2(cellWidth - text - 12f * s, 18f * s));
                    odds.rectTransform.pivot = new Vector2(0, 0);
                }
            }
        }

        /// <summary>A named line of body copy: a paragraph a test can find by its name rather than its words.</summary>
        public TMP_Text NamedParagraph(string name, string value, Color? colour = null)
        {
            var text = FlowText(value, BodySize, colour ?? Paper);
            text.name = name;
            return text;
        }

        /// <summary>A running story in the week's panel: its title and where it stands, one line.</summary>
        public void StoryLine(string title, string status)
        {
            var row = DecisionText(content, title + "  ·  " + status, 15, Paper);
            row.name = "Story line";
        }

        // ---------------------------------------------------------------- the Pull

        /// <summary>The Pull's card, and the fixed captions its controls are found by.</summary>
        public const string PullCardName = "Story pull card";
        public const string StepInCaption = "Step in";
        public const string StayOutCaption = "Stay out of it";
        public const string HearThemOutCaption = "Hear them out";
        public const string JoinMeetingCaption = "Join the meeting";
        public const string NotNowCaption = "Not now";
        /// <summary>A play's offer (plan 30): the engine's own option label, so the button and the option agree.</summary>
        public const string TakeItOnCaption = Gamesim.Simulation.PlayOptions.TakeItOnLabel;

        /// <summary>
        /// What the Pull offers, as the director has worked it out: the kind of moment and where, one
        /// line of stakes, and its two answers. <see cref="Key"/> changes when the offer does, and
        /// the card redraws its words only then.
        /// </summary>
        public struct StoryPull
        {
            public string Key;
            /// <summary><see cref="StepInCaption"/>, <see cref="HearThemOutCaption"/>, <see cref="JoinMeetingCaption"/> or <see cref="TakeItOnCaption"/>.</summary>
            public string Primary;
            /// <summary><see cref="StayOutCaption"/> or <see cref="NotNowCaption"/>.</summary>
            public string Secondary;
            public string Eyebrow, Title, Stakes;
            public Action Accept, Decline;
        }

        private RectTransform pullCard;
        private TMP_Text pullEyebrow, pullTitle, pullStakes;
        private readonly Dictionary<string, Button> pullButtons = new Dictionary<string, Button>();
        private Action pullAccept, pullDecline;
        private string pullKey;

        /// <summary>Whether the Pull is on screen.</summary>
        public bool PullShowing => pullCard != null && pullCard.gameObject.activeSelf;

        /// <summary>
        /// The Pull (plan §5.1): non-modal, in the week card's place like the Nearby card, and built
        /// hidden. A mark, the kind of moment and where it is, a line of stakes, and two answers. It
        /// names nobody: who is in it is for the player to find out by stepping in. Every caption
        /// the card can show is its own control, built once and switched on by mode, so a caption is
        /// never rewritten under a test or a screen reader that already found it.
        /// </summary>
        private void PullCard(Transform parent, float top)
        {
            float width = RightColumnWidth, height = 168f;
            // A rebuilt card starts blank: the next SetPull fills it whatever it showed before.
            pullKey = null; pullButtons.Clear();
            pullCard = Chrome(PullCardName, parent);
            Anchor(pullCard, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-RightColumnInset, -top), new Vector2(width, height));
            pullEyebrow = CardHeading(pullCard, "Happening now", UiTheme.Joke);
            pullEyebrow.characterSpacing = 2f;
            float x = 16f;
            var drama = UiTheme.Pack(PackArt.IconDrama);
            if (drama != null)
            {
                var mark = new GameObject("Pull mark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                mark.rectTransform.SetParent(pullCard, false);
                Anchor(mark.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14f, -40f), new Vector2(32f, 32f));
                mark.sprite = drama; mark.preserveAspect = true; mark.raycastTarget = false;
                x = 54f;
            }
            pullTitle = FixedText(pullCard, "", 15, Paper, new Vector2(x, -40f), new Vector2(width - x - 12f, 22f));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) pullTitle.font = semibold;
            pullStakes = FixedText(pullCard, "", 12, UiTheme.Muted, new Vector2(x, -62f), new Vector2(width - x - 12f, 32f));
            AutoSize(pullStakes, 10);

            float inner = width - 28f, gap = 8f, wide = Mathf.Round((inner - gap) * .56f), narrow = inner - gap - wide;
            foreach (var caption in new[] { StepInCaption, HearThemOutCaption, JoinMeetingCaption, TakeItOnCaption })
            {
                var button = FixedButton(pullCard, caption, new Vector2(14f, -104f), new Vector2(wide, 36f), () => pullAccept?.Invoke());
                button.GetComponent<Image>().color = new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .9f);
                UiTheme.AddBorder((RectTransform)button.transform, UiTheme.ControlRadius, UiTheme.Joke);
                PullWords(button, 15, semibold);
                pullButtons[caption] = button;
            }
            foreach (var caption in new[] { StayOutCaption, NotNowCaption })
            {
                var button = FixedButton(pullCard, caption, new Vector2(14f + wide + gap, -104f), new Vector2(narrow, 36f), () => pullDecline?.Invoke());
                PullWords(button, 13, null);
                pullButtons[caption] = button;
            }
            var foot = FixedText(pullCard, "Nothing happens unless you step in.", 11, UiTheme.Muted, new Vector2(14f, -146f), new Vector2(inner, 16f));
            foot.alignment = TextAlignmentOptions.Center;
            pullCard.gameObject.SetActive(false);
        }

        private static void PullWords(Button button, float size, TMP_FontAsset font)
        {
            var words = button.GetComponentInChildren<TMP_Text>();
            if (words == null) return;
            words.fontSize = size; words.fontSizeMax = size; words.alignment = TextAlignmentOptions.Center;
            if (font != null) words.font = font;
        }

        /// <summary>
        /// Puts the Pull up, changes what it offers, or takes it down - without a <c>Render()</c>,
        /// so a panel's state and the keyboard's place in it are never rebuilt for it. While it is
        /// up it has the week card's place; the Nearby card gives way to it.
        /// </summary>
        public void SetPull(StoryPull? pull)
        {
            if (pullCard == null) return;
            bool visible = pull.HasValue;
            if (visible)
            {
                var offer = pull.Value;
                pullAccept = offer.Accept; pullDecline = offer.Decline;
                if (offer.Key != pullKey)
                {
                    pullKey = offer.Key;
                    pullEyebrow.text = Localisation.Text(offer.Eyebrow ?? "Happening now");
                    pullTitle.text = Localisation.Text(offer.Title ?? "");
                    pullStakes.text = Localisation.Text(offer.Stakes ?? "");
                    foreach (var pair in pullButtons)
                        pair.Value.gameObject.SetActive(pair.Key == offer.Primary || pair.Key == offer.Secondary);
                    restoreSelection = true;
                }
            }
            else { pullKey = null; pullAccept = null; pullDecline = null; }
            if (pullCard.gameObject.activeSelf == visible) return;
            pullCard.gameObject.SetActive(visible);
            if (visible && nearbyCard != null && nearbyCard.gameObject.activeSelf) SetNearby(false);
            SyncWeekCard();
            // The ring is wired from what is on screen, so a card that comes and goes between
            // renders has to ask for it: every new control is on the ring (plan §5.6).
            restoreSelection = true;
        }

        /// <summary>The week card stands down while either card that borrows its place is up.</summary>
        private void SyncWeekCard()
        {
            var parent = pullCard != null ? pullCard.parent : nearbyCard != null ? nearbyCard.parent : null;
            // The last of that name: a rebuild in the same frame leaves the old copy until it is destroyed.
            Transform week = null;
            if (parent != null)
                for (int i = parent.childCount - 1; i >= 0 && week == null; i--)
                    if (parent.GetChild(i).name == HouseVibeCardName || parent.GetChild(i).name == ObjectivesCardName) week = parent.GetChild(i);
            if (week == null) return;
            bool borrowed = (pullCard != null && pullCard.gameObject.activeSelf) || (nearbyCard != null && nearbyCard.gameObject.activeSelf);
            week.gameObject.SetActive(!borrowed);
        }
    }
}
