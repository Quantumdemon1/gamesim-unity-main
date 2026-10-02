using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The introductions: the reference build's meet and greet, one houseguest at a time in the
    /// season's order. Each says their own line; the player reads their traits and introduces
    /// themselves Warm, Calculated or Bold; they answer. Nothing says how well it went but the
    /// answer - the reference build shows no number and no verdict, only what they say back.
    ///
    /// <para>The card is the reference build's, laid out as it lays it out: the question goes once
    /// it is answered, the houseguest's reply stands where the question was with Next right under
    /// it, and the card closes up to fit. One card leaves before the next comes in - its
    /// AnimatePresence in "wait" mode, 0.35 s out and 0.35 s in on its own ease - and that gap is
    /// what keeps a double-click on Next off the next houseguest's choices, whose first row comes
    /// in right where Next was.</para>
    ///
    /// <para>Under reduced motion there is no leave to wait for: the card before goes at once and
    /// the next stands in its place on the frame Next is pressed. So that card is armed the way
    /// the first one is, and the arming stands in for the gap. Either way "Skip Introductions" is
    /// out of the keyboard ring for the whole swap: Next goes dead as its card goes, and a ring
    /// with the skip as its only live control handed it the keyboard, where a second Enter
    /// forfeited everybody not yet met.</para>
    ///
    /// <para>The house behind it is the addition. The camera goes to each houseguest where they
    /// stand, they turn to face it, and they take the introduction the way they take anything else
    /// in the house - a dance, a word, a scowl.</para>
    /// </summary>
    public sealed partial class OpeningSequence
    {
        private bool meetSkipped, meetNext, answered, firstCard;
        private int cardFrame, armGeneration;
        private TMP_Text promptLabel;
        private readonly List<Button> choiceButtons = new List<Button>();

        /// <summary>
        /// How long the first card's choices - every card's, under reduced motion - and each Next,
        /// stay out of reach of real input after they appear, when <see cref="Settings.ArmSeconds"/>
        /// does not say. A second click of a double-click, or a second press of Enter, used to land
        /// on a card's choices before anybody could read it - and an introduction cannot be taken
        /// back. Only real input: nothing in a headless run presses anything.
        /// </summary>
        private const float ArmDefault = 0.35f;

        /// <summary>How long a card takes to leave, and the next to arrive: the reference build's 0.35 s each way.</summary>
        private const float SwapSeconds = 0.35f;

        // The card's layout, in canvas units: the reference build's max-w-md column at this canvas's
        // scale. The question and the reply share a line, 202 down; the rows start under it.
        private const float ColumnWidth = 560f;
        private const float CardTop = 48f;
        private const float CardPad = 20f;
        private const float InnerWidth = ColumnWidth - 2f * CardPad;
        private const float PromptTop = 202f;
        private const float ChoicesTop = 228f;
        private const float RowHeight = 52f;
        private const float RowGap = 8f;
        private const float AnswerGap = 12f;
        private const float NextHeight = 36f;
        private const float SkipGap = 20f;
        private const float FullCard = ChoicesTop + 3f * RowHeight + 2f * RowGap + CardPad;
        /// <summary>The header's ground: how far it reaches past the column's edges, and how far down it runs under the progress bar (which ends at 36), short of the card at <see cref="CardTop"/>.</summary>
        private const float HeaderPad = 12f, HeaderBottom = 42f;

        private RectTransform meetColumn;
        private RectTransform choicesSlot;
        private RectTransform progressFill;
        private RectTransform liveCard;
        private TMP_Text tally;
        private Button skipIntroductions;

        /// <summary>
        /// How far across the frame the introductions' column reaches, as a share of the frame's
        /// width from its left edge, while they are on screen; null when they are not. Read in the
        /// canvas's own space, so it holds for an overlay and for a capture drawn through a camera
        /// alike. The director keeps the houseguest it frames to the right of it: at 4:3 and the
        /// larger text the card reaches nearly half way across, and a body behind it is not seen.
        /// </summary>
        public float? IntroductionsReach
        {
            get
            {
                if (!IsMeeting || meetColumn == null) return null;
                var root = (RectTransform)transform;
                var frame = root.rect;
                if (frame.width <= 0f) return null;
                var corners = new Vector3[4];
                meetColumn.GetWorldCorners(corners);
                float right = root.InverseTransformPoint(corners[2]).x;
                return Mathf.Clamp01((right - frame.xMin) / frame.width);
            }
        }

        /// <summary>Ends the introductions: whoever has not been met gets nothing from them, as in the reference build.</summary>
        public void SkipIntroductions()
        {
            if (!IsMeeting) return;
            meetSkipped = true;
        }

        /// <summary>
        /// What Escape does during the introductions: puts the keyboard on "Skip Introductions"
        /// without pressing it. A key that forfeits a choice in one press is too easy to hit by
        /// habit on the way past a card.
        /// </summary>
        public void FocusSkipIntroductions()
        {
            if (IsMeeting && skipIntroductions != null) Select(skipIntroductions.gameObject);
        }

        /// <summary>
        /// How long new controls stay disarmed: the plan's number when it gives one, otherwise the
        /// rule for play - <see cref="ArmDefault"/> for real input and none in batchmode. Read while
        /// the plan is still there: finishing the sequence lets go of it.
        /// </summary>
        private float ArmFor()
        {
            if (settings == null) return 0f;
            return Mathf.Max(0f, settings.ArmSeconds ?? (Application.isBatchMode ? 0f : ArmDefault));
        }

        private IEnumerator MeetAndGreet()
        {
            Clear();
            var guests = Guests().ToList();
            if (settings.Introduce == null || guests.Count == 0)
            {
                // Nothing to commit an introduction through: a card that hands over to the house.
                Section(Night);
                var heading = Text(stage, "Title", Localisation.Text(MeetHeading), 48f, UiTheme.Heading, 1500f, new Vector2(0f, 60f), UiTheme.Weight.Bold);
                heading.characterSpacing = 2f;
                Text(stage, "Line", Localisation.Text("First impressions, before anybody has done anything worth remembering."),
                    19f, UiTheme.Muted, 1500f, new Vector2(0f, -10f));
                yield return Hold(HandOffHold);
                yield break;
            }

            int start = guests.FindIndex(guest => !Met(guest.id));
            if (start < 0) yield break;

            // Everybody where the season starts them before anyone is framed: a walk-in moved on by
            // Continue hands over with people still walking home, and a card framed on a spot they
            // are walking out of shows nobody. Under black, as a skip does it.
            var world = settings.Stage;
            if (world != null && world.Placed && !world.AllHome)
            {
                Scrim(Color.black);
                yield return null;
                world.StrikeSet();
                world.RestoreHome();
                yield return Pause(0.3f);
                Clear();
            }
            // Everybody held where they stand: the stage's walks end and nobody is sent anywhere,
            // so a houseguest framed on their spot is still on it when they answer (UI-UX-PASS-PLAN
            // S0, sweep-show 24: the reaction's shot held on an empty corner).
            if (world != null && world.Placed) world.HoldForIntroductions();

            Scrim(new Color(0f, 0f, 0f, 0.7f));
            IsMeeting = true;
            meetSkipped = false;
            firstCard = true;
            liveCard = null;
            BuildColumn(guests.Count);
            for (int i = start; i < guests.Count && !meetSkipped; i++)
            {
                // Anybody met before a reload keeps that meeting; the count still says where they stand.
                if (Met(guests[i].id)) continue;
                yield return MeetOne(guests[i], i, guests.Count);
            }
            IsMeeting = false;
            CurrentGuestId = null;
        }

        /// <summary>
        /// The column the cards stand in: the heading and the count, the progress bar, and the
        /// skip under the card. Left of centre, so the houseguest the camera is on stands clear of it.
        /// </summary>
        private void BuildColumn(int count)
        {
            meetColumn = new GameObject("Introductions", typeof(RectTransform)).GetComponent<RectTransform>();
            meetColumn.SetParent(stage, false);
            meetColumn.anchorMin = meetColumn.anchorMax = new Vector2(0f, 0.5f);
            meetColumn.pivot = new Vector2(0f, 0.5f);
            meetColumn.sizeDelta = new Vector2(ColumnWidth, 560f);
            meetColumn.anchoredPosition = new Vector2(120f, 0f);

            // The heading, the count and the progress bar on a ground of their own, a sibling behind
            // them: laid on the scrim they read through the yard's sign (UI-UX-PASS-PLAN S0,
            // sweep-show 24). It reaches HeaderPad past the column on every side and stops short of
            // the card.
            var ground = Ground(meetColumn, 12);
            Place(ground, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(-HeaderPad, HeaderPad),
                new Vector2(ColumnWidth + 2f * HeaderPad, HeaderPad + HeaderBottom));

            var heading = HudPrimitives.Label("Heading", meetColumn, 16f, UiTheme.Heading, TextAlignmentOptions.Left);
            heading.text = Localisation.Text(MeetHeading);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) heading.font = semibold;
            Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(360f, 22f));

            // One count, outside the cards, moved on as Next is pressed - the reference build's
            // header, which changes at the click while the card it counted is still leaving.
            tally = HudPrimitives.Label("Count", meetColumn, 16f, UiTheme.Muted, TextAlignmentOptions.Right);
            Place(tally.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(200f, 22f));

            var track = HudPrimitives.Fill("Progress", meetColumn, new Color(1f, 1f, 1f, 0.12f), 3);
            Place(track, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -30f), new Vector2(ColumnWidth, 6f));
            track.GetComponent<Image>().raycastTarget = false;
            progressFill = HudPrimitives.Fill("Fill", track, UiTheme.Heading, 3);
            progressFill.anchorMin = new Vector2(0f, 0f); progressFill.anchorMax = new Vector2(0f, 1f);
            progressFill.pivot = new Vector2(0f, 0.5f);
            progressFill.sizeDelta = Vector2.zero;
            progressFill.GetComponent<Image>().raycastTarget = false;

            SkipLink();
            PlaceSkip(FullCard);
        }

        /// <summary>
        /// "Skip Introductions" as the reference build draws it: a muted line of text with a
        /// skip-forward mark beside it, centred under the card - not a button to aim for. The
        /// caption keeps a label of its own and the mark is drawn beside it, never into it.
        ///
        /// <para>It is still a control the keyboard can land on and be seen on: Escape puts the focus
        /// here, and an invisible focus was how the next Enter used to forfeit every introduction
        /// left. The hairline edge is the button's target graphic, clear at rest and while disarmed,
        /// and drawn when the keyboard or the pointer is on it.</para>
        /// </summary>
        private void SkipLink()
        {
            const float pad = 10f, gap = 6f, height = 28f, size = 13f;
            var skip = HudPrimitives.Fill(SkipIntroductionsCaption, meetColumn, new Color(1f, 1f, 1f, 0f), 8);
            var ground = skip.GetComponent<Image>();
            ground.raycastTarget = true;
            UiTheme.AddBorder(skip, 8, UiTheme.Edge(UiTheme.Emphasis.Resting));
            var edge = skip.Find("Border").GetComponent<Image>();

            var mark = SkipGlyph(skip, UiTheme.Muted, 11f);
            mark.anchorMin = mark.anchorMax = new Vector2(0f, 0.5f);
            mark.pivot = new Vector2(0f, 0.5f);
            mark.anchoredPosition = new Vector2(pad, 0f);

            var label = HudPrimitives.Label("Label", skip, size, UiTheme.Muted, TextAlignmentOptions.Left);
            label.text = SkipIntroductionsCaption;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            float words = Mathf.Ceil(label.GetPreferredValues(SkipIntroductionsCaption).x) + 4f;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(pad + mark.sizeDelta.x + gap, 0f);
            label.rectTransform.offsetMax = new Vector2(-pad, 0f);

            skip.anchorMin = skip.anchorMax = new Vector2(0.5f, 1f);
            skip.pivot = new Vector2(0.5f, 1f);
            skip.sizeDelta = new Vector2(pad + mark.sizeDelta.x + gap + words + pad, height);

            skipIntroductions = skip.gameObject.AddComponent<Button>();
            skipIntroductions.targetGraphic = edge;
            skipIntroductions.onClick.AddListener(SkipIntroductions);
            Focusable(skipIntroductions, skip, UiTheme.Emphasis.Resting);
            var colours = skipIntroductions.colors;
            colours.normalColor = new Color(1f, 1f, 1f, 0f);
            colours.disabledColor = new Color(1f, 1f, 1f, 0f);
            skipIntroductions.colors = colours;
            // Clear from the first frame: a colour block set after the button woke fades to it over
            // a tenth of a second, and an instant cross-fade stops that fade where it starts.
            edge.CrossFadeColor(colours.normalColor, 0f, true, true);
        }

        /// <summary>
        /// A skip-forward mark, two chevrons and a bar, in a box of its own: the reference build's
        /// SkipForward icon, drawn from the HUD's own chevron because the shipped fonts have no glyph
        /// for it. Decoration only; what the control does is in its caption.
        /// </summary>
        private static RectTransform SkipGlyph(Transform parent, Color tint, float side)
        {
            var glyph = new GameObject("Skip glyph", typeof(RectTransform)).GetComponent<RectTransform>();
            glyph.SetParent(parent, false);
            // A chevron's two arms, 0.62 of its box long at 45 degrees, reach 0.22 of the box either
            // side of its middle; the marks are placed by what they draw, not by their boxes.
            float reach = side * 0.62f * 0.354f;
            float bar = Mathf.Max(1.5f, side * 0.13f);
            float height = side * 0.62f * 1.21f;
            float second = reach * 3.4f;
            float width = second + reach + reach * 0.6f + bar;
            glyph.sizeDelta = new Vector2(width, side);
            foreach (float middle in new[] { reach, second })
            {
                var chevron = HudPrimitives.Chevron(glyph, tint, side);
                chevron.anchorMin = chevron.anchorMax = new Vector2(0f, 0.5f);
                chevron.pivot = new Vector2(0.5f, 0.5f);
                chevron.anchoredPosition = new Vector2(middle, 0f);
            }
            var stop = HudPrimitives.Fill("Bar", glyph, tint, 1);
            stop.anchorMin = stop.anchorMax = new Vector2(0f, 0.5f);
            stop.pivot = new Vector2(1f, 0.5f);
            stop.anchoredPosition = new Vector2(width, 0f);
            stop.sizeDelta = new Vector2(bar, height);
            return glyph;
        }

        /// <summary>Stands the skip under a card of this height, a gap below its foot, as the reference build's column does.</summary>
        private void PlaceSkip(float cardHeight)
        {
            if (skipIntroductions == null) return;
            ((RectTransform)skipIntroductions.transform).anchoredPosition = new Vector2(0f, -(CardTop + cardHeight + SkipGap));
        }

        /// <summary>
        /// One houseguest's card, waiting until the player has answered and moved on, or skipped the
        /// rest. The count moves on and the camera sets off for them at once; the card before goes
        /// first, and only then does theirs come in.
        /// </summary>
        private IEnumerator MeetOne(ContestantState person, int index, int count)
        {
            CurrentGuestId = person.id;
            answered = false;
            meetNext = false;
            // A card is armed whenever nothing leaves before it: the first, and every card under
            // reduced motion. With the movement on, the card before takes 0.35 s to go, and that
            // leave is the gap a double-click or a second Enter would need to cross - as it is in
            // the reference build, whose AnimatePresence waits for it - so arming on top of it
            // would leave the choices dead for twice as long. Under reduced motion the card before
            // is dropped and this one stands where Next was on the same frame; the arming is the
            // only gap there is.
            float arm = (firstCard || Motionless) ? ArmFor() : 0f;
            firstCard = false;
            settings.FrameGuest?.Invoke(person.id);
            if (tally != null) tally.text = Localisation.Format("{0} of {1}", index + 1, count);
            SetProgress(index / (float)count);

            if (liveCard != null)
            {
                // The skip is out of the ring for the whole swap. Next goes dead as its card goes,
                // the EventSystem lets go of it, and the HUD hands the keyboard to the first live
                // control under the opening: with the card's controls all dead that was the skip,
                // and a second Enter on it forfeited everybody not yet met. The new card's arming
                // puts it back once the keyboard is on the new choices. A new arming generation
                // also keeps one still counting down on the card that goes from handing its
                // controls, or the skip, back.
                armGeneration++;
                if (skipIntroductions != null) skipIntroductions.interactable = false;
                if (Motionless) DropCard(liveCard);
                else yield return CardLeaves(liveCard);
            }
            liveCard = null;
            if (meetSkipped || !IsMeeting) yield break;

            var card = Card(person, index, count);
            liveCard = card;
            PlaceSkip(FullCard);
            // The skip stays last in the ring, after the choices on the new card.
            skipIntroductions.transform.SetAsLastSibling();
            cardFrame = Time.frameCount;
            StartCoroutine(Arm(choiceButtons.ToList(), choiceButtons.FirstOrDefault(), arm));

            while (!meetNext && !meetSkipped) yield return null;
        }

        /// <summary>
        /// The reference build's card: face, name and every trait as a chip - reading them is how
        /// the player picks - their own line, and the three ways to introduce yourself.
        /// </summary>
        private RectTransform Card(ContestantState person, int index, int count)
        {
            var card = HudPrimitives.Glass("Card", meetColumn);
            Place(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -CardTop), new Vector2(ColumnWidth, FullCard));
            var fader = Fader(card, interactive: true);
            Animate(card, 0f, SwapSeconds, t =>
            {
                fader.alpha = t;
                card.anchoredPosition = new Vector2(40f * (1f - t), -CardTop);
            }, CardEase);

            var band = HudPrimitives.Fill("Band", card, new Color(UiTheme.Heading.r, UiTheme.Heading.g, UiTheme.Heading.b, 0.10f), 12);
            band.anchorMin = new Vector2(0f, 1f);
            band.anchorMax = new Vector2(1f, 1f);
            band.pivot = new Vector2(0.5f, 1f);
            band.anchoredPosition = Vector2.zero;
            band.sizeDelta = new Vector2(0f, 120f);
            band.GetComponent<Image>().raycastTarget = false;

            var face = HudPrimitives.Portrait(card, CharacterPortraits.Get(person), new Color(UiTheme.Heading.r, UiTheme.Heading.g, UiTheme.Heading.b, 0.5f), 80f, 2f, false, person);
            face.name = "Portrait";
            Place(face, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(62f, -60f), new Vector2(84f, 84f));
            face.pivot = new Vector2(0.5f, 0.5f);

            var name = HudPrimitives.Label("Name", card, 26f, UiTheme.Paper, TextAlignmentOptions.Left);
            name.text = person.name;
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) name.font = bold;
            Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, -24f), new Vector2(420f, 36f));

            float x = 120f;
            foreach (var trait in person.traits ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(trait)) continue;
                float width = Mathf.Max(56f, trait.Length * 8f + 28f);
                var chip = HudPrimitives.Chip("Trait", card, trait, UiTheme.Hex("9AD4FF"), width, 24f);
                Place(chip, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -68f), new Vector2(width, 24f));
                chip.GetComponent<Image>().raycastTarget = false;
                x += width + 6f;
            }

            // Straight quotes, as the reference build prints the line.
            var quote = HudPrimitives.Label("Quote", card, 16f, UiTheme.Muted, TextAlignmentOptions.TopLeft);
            quote.text = "\"" + WebIntroductions.IntroLine(settings.Seed, person.id, person.name, person.traits) + "\"";
            quote.fontStyle = FontStyles.Italic;
            Place(quote.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(CardPad, -134f), new Vector2(InnerWidth, 60f));

            var prompt = HudPrimitives.Label("Prompt", card, 13f, UiTheme.Muted, TextAlignmentOptions.Left);
            prompt.text = Localisation.Text(WebIntroductions.Prompt);
            promptLabel = prompt;
            choiceButtons.Clear();
            Place(prompt.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(CardPad, -PromptTop), new Vector2(InnerWidth, 20f));

            choicesSlot = new GameObject("Choices", typeof(RectTransform)).GetComponent<RectTransform>();
            choicesSlot.SetParent(card, false);
            Place(choicesSlot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(CardPad, -ChoicesTop),
                new Vector2(InnerWidth, 3f * RowHeight + 2f * RowGap));
            float y = 0f;
            var icons = new[] { UiTheme.Icon("heart"), UiTheme.Icon("target"), Flame() };
            for (int i = 0; i < WebIntroductions.Approaches.Count; i++)
            {
                var approach = WebIntroductions.Approaches[i];
                Choice(choicesSlot, approach, icons[i], y, person, index, count);
                y -= RowHeight + RowGap;
            }
            return card;
        }

        /// <summary>
        /// One way to introduce yourself. The caption is the approach's name alone, in a label of
        /// its own; what the player says goes in a second label beside it, never into the caption
        /// the tests and screen readers know the control by.
        /// </summary>
        private void Choice(RectTransform parent, WebIntroductions.Approach approach, Sprite icon, float y,
            ContestantState person, int index, int count)
        {
            var row = HudPrimitives.Fill(approach.Label, parent, UiTheme.SurfaceRaised, 10);
            Place(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(InnerWidth, RowHeight));
            UiTheme.AddBorder(row, 10, UiTheme.Outline);
            var image = row.GetComponent<Image>();
            image.raycastTarget = true;

            if (icon != null)
            {
                var mark = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                mark.rectTransform.SetParent(row, false);
                mark.sprite = icon;
                mark.color = UiTheme.Heading;
                mark.raycastTarget = false;
                mark.preserveAspect = true;
                Place(mark.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(18f, 18f));
            }

            var label = HudPrimitives.Label("Label", row, 15f, UiTheme.Paper, TextAlignmentOptions.Left);
            label.text = approach.Label;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) label.font = semibold;
            Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(110f, 22f));

            var sublabel = HudPrimitives.Label("Sublabel", row, 13f, UiTheme.Muted, TextAlignmentOptions.Left);
            sublabel.text = approach.Line;
            Place(sublabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(156f, 0f), new Vector2(356f, 20f));

            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => Choose(person, approach, index, count));
            Focusable(button, row, UiTheme.Emphasis.Interactive);
            choiceButtons.Add(button);
        }

        /// <summary>
        /// The player's answer: committed once, however many times it is pressed, with the house's
        /// click. The question and the choices go; the houseguest's reply stands where the question
        /// was, Next right-aligned directly under it, and the card closes up to fit - the reference
        /// build's layout. Next takes the keyboard: left to itself, the ring would have fallen
        /// through to "Skip Introductions". An answer the game could not commit keeps the choices,
        /// with the reason where the question was, so it can be tried again - and makes no click.
        /// </summary>
        private void Choose(ContestantState person, WebIntroductions.Approach approach, int index, int count)
        {
            if (answered || !IsMeeting || Time.frameCount == cardFrame) return;
            answered = true;
            var result = settings.Introduce(person.id, approach.Id);
            if (!result.Accepted)
            {
                answered = false;
                if (promptLabel != null) { promptLabel.text = result.Reason; promptLabel.color = UiTheme.Warning; }
                return;
            }
            // The commit can end the sequence under us - a reload, a replaced season.
            var card = liveCard;
            if (settings == null || card == null) return;
            settings.Click?.Invoke();
            float arm = ArmFor();

            if (promptLabel != null) promptLabel.gameObject.SetActive(false);
            if (choicesSlot != null)
            {
                foreach (Transform child in choicesSlot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                choicesSlot.gameObject.SetActive(false);
            }
            choiceButtons.Clear();

            var answer = new GameObject("Answer", typeof(RectTransform)).GetComponent<RectTransform>();
            answer.SetParent(card, false);
            // Straight quotes, as the reference build prints the reply.
            string line = Localisation.Format("{0}: \"{1}\"", person.name,
                WebIntroductions.Reaction(settings.Seed, person.id, person.name, person.traits, approach));
            var reply = HudPrimitives.Label("Reaction", answer, 15f, UiTheme.Paper, TextAlignmentOptions.TopLeft);
            reply.fontStyle = FontStyles.Italic;
            reply.text = line;
            // As tall as the reply runs - an All-Star's can take two lines - and never under the
            // height Inter needs to draw a line at all.
            float replyHeight = Mathf.Max(Mathf.Ceil(15f * 1.34f), Mathf.Ceil(reply.GetPreferredValues(line, InnerWidth, 0f).y) + 2f);
            Place(reply.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(InnerWidth, replyHeight));

            bool last = index == count - 1;
            string caption = last ? LetsPlayCaption : NextCaption;
            var next = HudPrimitives.Fill(caption, answer, UiTheme.ActionBlue, 8);
            Place(next, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -(replyHeight + AnswerGap)), new Vector2(150f, NextHeight));
            var image = next.GetComponent<Image>();
            image.raycastTarget = true;
            var label = HudPrimitives.Label("Label", next, 15f, UiTheme.OnColor(UiTheme.ActionBlue), TextAlignmentOptions.Center);
            label.text = caption;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) label.font = semibold;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8f, 0f);
            label.rectTransform.offsetMax = new Vector2(-22f, 0f);
            var chevron = HudPrimitives.Chevron(next, UiTheme.OnColor(UiTheme.ActionBlue), 12f);
            chevron.anchoredPosition = new Vector2(-10f, 0f);
            var button = next.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => { if (IsMeeting) meetNext = true; });
            Focusable(button, null, UiTheme.Emphasis.Interactive);

            // The reply and its Next come up together, ten units, as the reference build's do.
            float answerHeight = replyHeight + AnswerGap + NextHeight;
            Place(answer, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(CardPad, -PromptTop), new Vector2(InnerWidth, answerHeight));
            var fade = Fader(answer, interactive: true);
            Animate(answer, 0f, 0.3f, t =>
            {
                fade.alpha = t;
                answer.anchoredPosition = new Vector2(CardPad, -PromptTop - 10f * (1f - t));
            });
            FitCard(card, PromptTop + answerHeight + CardPad);

            settings.GuestReacts?.Invoke(person.id, result.Outcome);
            SetProgress((index + 1) / (float)count);
            StartCoroutine(Arm(new List<Button> { button }, button, arm));
        }

        /// <summary>The card closing up to its content, and the skip coming up under it; at once when nothing moves.</summary>
        private void FitCard(RectTransform card, float height)
        {
            if (card == null) return;
            float from = card.sizeDelta.y;
            Animate(card, 0f, 0.3f, t =>
            {
                card.sizeDelta = new Vector2(card.sizeDelta.x, Mathf.Lerp(from, height, t));
                if (card == liveCard) PlaceSkip(card.sizeDelta.y);
            });
        }

        /// <summary>
        /// Holds new controls out of reach of real input for <paramref name="seconds"/> - the skip with
        /// them, so the keyboard ring has nowhere to fall - then gives the keyboard to
        /// <paramref name="focus"/>. The buttons themselves, never a group over them: a group made
        /// non-interactable leaves its controls greyed for good. The frame that built them counts
        /// for nothing - its time passed before they were on screen - and a card that has started
        /// to leave keeps its controls disabled.
        ///
        /// <para>With nothing to wait for, the keyboard goes to <paramref name="focus"/> at once and
        /// the skip comes back into the ring with it: a card swap takes the skip out, and an arrival
        /// with the movement on is not armed again, so this is where it returns. Either way only
        /// the newest arming hands it back.</para>
        /// </summary>
        private IEnumerator Arm(List<Button> controls, Selectable focus, float seconds)
        {
            int generation = ++armGeneration;
            if (seconds > 0f)
            {
                var card = liveCard;
                foreach (var control in controls) if (control != null) control.interactable = false;
                if (skipIntroductions != null) skipIntroductions.interactable = false;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                yield return null;
                float wait = seconds;
                while (wait > 0f) { wait -= Time.unscaledDeltaTime; yield return null; }
                // A later arming owns the skip now, and the keyboard.
                if (generation != armGeneration) yield break;
                bool current = card != null && card == liveCard;
                if (current) foreach (var control in controls) if (control != null) control.interactable = true;
                if (skipIntroductions != null) skipIntroductions.interactable = true;
                if (!current) yield break;
            }
            else if (skipIntroductions != null && generation == armGeneration) skipIntroductions.interactable = true;
            if (focus != null && IsMeeting) Select(focus.gameObject);
        }

        private void SetProgress(float share)
        {
            if (progressFill == null) return;
            float from = progressFill.sizeDelta.x;
            float to = ColumnWidth * Mathf.Clamp01(share);
            var fill = progressFill;
            Animate(fill, 0f, 0.3f, t => fill.sizeDelta = new Vector2(Mathf.Lerp(from, to, t), 0f));
        }

        /// <summary>
        /// The card leaving: out to the left and gone over <see cref="SwapSeconds"/> on the reference
        /// build's ease, its controls dead from the first frame - a second click of a double-click
        /// lands on nothing. The next card waits for this.
        /// </summary>
        private IEnumerator CardLeaves(RectTransform rect)
        {
            if (rect == null) yield break;
            rect.name = "Leaving";
            foreach (var control in rect.GetComponentsInChildren<Selectable>()) control.interactable = false;
            var fader = rect.GetComponent<CanvasGroup>();
            if (fader == null) fader = rect.gameObject.AddComponent<CanvasGroup>();
            fader.blocksRaycasts = false;
            var from = rect.anchoredPosition;
            float elapsed = 0f;
            while (rect != null && elapsed < SwapSeconds && !meetSkipped)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = CardEase(Mathf.Clamp01(elapsed / SwapSeconds));
                fader.alpha = 1f - t;
                rect.anchoredPosition = from + new Vector2(-40f * t, 0f);
                yield return null;
            }
            DropCard(rect);
        }

        /// <summary>A card gone at once: hidden this frame, destroyed at its end.</summary>
        private static void DropCard(RectTransform rect)
        {
            if (rect == null) return;
            rect.name = "Leaving";
            rect.gameObject.SetActive(false);
            Destroy(rect.gameObject);
        }

        /// <summary>
        /// The reference build's card ease, its cubic-bezier(0.16, 1, 0.3, 1), solved as a browser
        /// solves one: the curve's parameter for the time, then the curve's height there. A fast
        /// start that settles, with no overshoot.
        /// </summary>
        private static float CardEase(float t) => CubicBezier(0.16f, 1f, 0.3f, 1f, t);

        private static float CubicBezier(float x1, float y1, float x2, float y2, float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            // With both handles' x inside 0 to 1 the curve's x only ever rises, so halving finds it.
            float low = 0f, high = 1f;
            for (int i = 0; i < 24; i++)
            {
                float middle = (low + high) * 0.5f;
                if (BezierAxis(x1, x2, middle) < t) low = middle; else high = middle;
            }
            return BezierAxis(y1, y2, (low + high) * 0.5f);
        }

        /// <summary>One axis of a cubic Bezier from 0 to 1 with its handles at <paramref name="a"/> and <paramref name="b"/>.</summary>
        private static float BezierAxis(float a, float b, float s)
        {
            float u = 1f - s;
            return 3f * a * u * u * s + 3f * b * u * s * s + s * s * s;
        }

        private static Sprite flame;

        /// <summary>How much of its box the flame stands in: as tall as the heart beside it.</summary>
        private const float FlameShare = 0.88f;

        /// <summary>
        /// The Bold choice's flame, drawn here rather than taken from the pack.
        ///
        /// <para>The pack's "fire" (<see cref="PackArt.IconFire"/>) was a dot on the card. It is a
        /// low-poly shape standing in the middle 43 by 57 per cent of a 128-pixel canvas, so the
        /// row's 18-unit box drew it about 8 by 10 units - 6 by 8 pixels at 1600 x 900 - and, with
        /// no mipmaps, its two notches sampled away into a round blob. Even drawn large it barely
        /// reads as fire.</para>
        ///
        /// <para>This one fills its box the way the heart and the target fill theirs, and carries
        /// mipmaps, so it keeps its shape at icon size: a main tongue leaning into its tip, a smaller
        /// one beside it, and the hollow at its heart that makes it fire rather than a drop - the
        /// reference build's lucide Flame, as a solid glyph like the other two. Generated once.</para>
        /// </summary>
        private static Sprite Flame()
        {
            if (flame != null) return flame;
            const int size = 64, samples = 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "Introductions flame", hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int covered = 0;
                    for (int sy = 0; sy < samples; sy++)
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float u = (x + (sx + 0.5f) / samples) / size;
                            float v = (y + (sy + 0.5f) / samples) / size;
                            float margin = (1f - FlameShare) * 0.5f;
                            if (InFlame((u - 0.5f) / FlameShare + 0.5f, (v - margin) / FlameShare)) covered++;
                        }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * covered / (samples * samples)));
                }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            flame = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            flame.name = "Introductions flame";
            flame.hideFlags = HideFlags.DontSave;
            return flame;
        }

        /// <summary>Whether a point of the unit box, up from its foot, is fire: two tongues, less the hollow in the big one.</summary>
        private static bool InFlame(float u, float v)
        {
            bool main = Tongue(u, v, 0.50f, 0.36f, 0.34f, 1.00f, 0.10f, 1.25f);
            bool side = Tongue(u, v, 0.30f, 0.40f, 0.17f, 0.80f, -0.08f, 1.20f);
            bool hollow = Tongue(u, v, 0.52f, 0.28f, 0.14f, 0.64f, -0.05f, 1.30f);
            return (main || side) && !hollow;
        }

        /// <summary>
        /// One tongue of flame: a disc at its foot, narrowing up to a tip that leans over by
        /// <paramref name="lean"/>. Sides that swell a little before they taper are what make it
        /// a flame rather than a cone.
        /// </summary>
        private static bool Tongue(float u, float v, float x, float foot, float radius, float tip, float lean, float taper)
        {
            if (v <= foot) return (u - x) * (u - x) + (v - foot) * (v - foot) <= radius * radius;
            if (v >= tip) return false;
            float t = (v - foot) / (tip - foot);
            float half = radius * Mathf.Pow(1f - t, taper) * (1f + 0.6f * t * (1f - t));
            return Mathf.Abs(u - (x + lean * t * t)) <= half;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
