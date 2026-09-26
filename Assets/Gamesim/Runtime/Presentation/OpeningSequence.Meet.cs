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
    /// <para>The card is the reference build's; the house behind it is the addition. The camera
    /// goes to each houseguest where they stand, they turn to face it, and they take the
    /// introduction the way they take anything else in the house - a cheer, a word, a scowl.</para>
    /// </summary>
    public sealed partial class OpeningSequence
    {
        private bool meetSkipped, meetNext, answered;
        private int cardFrame;
        private TMP_Text promptLabel;
        private readonly List<Button> choiceButtons = new List<Button>();

        /// <summary>
        /// How long a new card's choices, and each Next, stay out of reach of real input after they
        /// appear. A second click of a double-click, or a second press of Enter, used to land on the
        /// next card's choices before anybody could read it - and an introduction cannot be taken
        /// back. Only real input: nothing in a headless run presses anything.
        /// </summary>
        private const float ArmSeconds = 0.35f;
        private RectTransform meetColumn;
        private RectTransform choicesSlot;
        private RectTransform progressFill;
        private Button skipIntroductions;

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

            Scrim(new Color(0f, 0f, 0f, 0.7f));
            IsMeeting = true;
            meetSkipped = false;
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
        /// skip under it all. Left of centre, so the houseguest the camera is on stands clear of it.
        /// </summary>
        private void BuildColumn(int count)
        {
            meetColumn = new GameObject("Introductions", typeof(RectTransform)).GetComponent<RectTransform>();
            meetColumn.SetParent(stage, false);
            meetColumn.anchorMin = meetColumn.anchorMax = new Vector2(0f, 0.5f);
            meetColumn.pivot = new Vector2(0f, 0.5f);
            meetColumn.sizeDelta = new Vector2(560f, 560f);
            meetColumn.anchoredPosition = new Vector2(120f, 0f);

            var heading = HudPrimitives.Label("Heading", meetColumn, 16f, UiTheme.Heading, TextAlignmentOptions.Left);
            heading.text = Localisation.Text(MeetHeading);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) heading.font = semibold;
            Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(360f, 22f));

            var track = HudPrimitives.Fill("Progress", meetColumn, new Color(1f, 1f, 1f, 0.12f), 3);
            Place(track, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -30f), new Vector2(560f, 6f));
            track.GetComponent<Image>().raycastTarget = false;
            progressFill = HudPrimitives.Fill("Fill", track, UiTheme.Heading, 3);
            progressFill.anchorMin = new Vector2(0f, 0f); progressFill.anchorMax = new Vector2(0f, 1f);
            progressFill.pivot = new Vector2(0f, 0.5f);
            progressFill.sizeDelta = Vector2.zero;
            progressFill.GetComponent<Image>().raycastTarget = false;

            var skip = HudPrimitives.Fill(SkipIntroductionsCaption, meetColumn, new Color(1f, 1f, 1f, 0.04f), 6);
            Place(skip, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(220f, 30f));
            // An edge that lights when the keyboard is on it: with nothing to see, Escape put the
            // focus on an invisible control, and the next Enter forfeited every introduction left.
            UiTheme.AddBorder(skip, 6, UiTheme.Edge(UiTheme.Emphasis.Resting));
            var skipImage = skip.GetComponent<Image>();
            skipImage.raycastTarget = true;
            var skipLabel = HudPrimitives.Label("Label", skip, 13f, UiTheme.Muted, TextAlignmentOptions.Center);
            skipLabel.text = SkipIntroductionsCaption;
            Stretch(skipLabel.rectTransform);
            skipIntroductions = skip.gameObject.AddComponent<Button>();
            skipIntroductions.targetGraphic = skipImage;
            skipIntroductions.onClick.AddListener(SkipIntroductions);
            Focusable(skipIntroductions, skip, UiTheme.Emphasis.Resting);
        }

        /// <summary>One houseguest's card, waiting until the player has answered and moved on, or skipped the rest.</summary>
        private IEnumerator MeetOne(ContestantState person, int index, int count)
        {
            CurrentGuestId = person.id;
            answered = false;
            meetNext = false;
            settings.FrameGuest?.Invoke(person.id);

            // Each card has its own count, under the heading on the right.
            foreach (Transform child in meetColumn)
                if (child.name == "Count" || child.name == "Card") SlideOut(child as RectTransform);
            var tally = HudPrimitives.Label("Count", meetColumn, 16f, UiTheme.Muted, TextAlignmentOptions.Right);
            tally.text = Localisation.Format("{0} of {1}", index + 1, count);
            Place(tally.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(200f, 22f));
            SetProgress(index / (float)count);

            var card = Card(person, index, count);
            // The skip stays last in the ring, after the choices on the new card.
            skipIntroductions.transform.SetAsLastSibling();
            cardFrame = Time.frameCount;
            StartCoroutine(Arm(choiceButtons.ToList(), choiceButtons.FirstOrDefault()));

            while (!meetNext && !meetSkipped) yield return null;
        }

        /// <summary>
        /// The reference build's card: face, name and every trait as a chip - reading them is how
        /// the player picks - their own line, and the three ways to introduce yourself.
        /// </summary>
        private RectTransform Card(ContestantState person, int index, int count)
        {
            var card = HudPrimitives.Glass("Card", meetColumn);
            Place(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -48f), new Vector2(560f, 440f));
            var fader = Fader(card, interactive: true);
            Animate(card, 0f, 0.35f, t =>
            {
                fader.alpha = t;
                card.anchoredPosition = new Vector2(40f * (1f - t), -48f);
            }, Settle);

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

            var quote = HudPrimitives.Label("Quote", card, 16f, UiTheme.Muted, TextAlignmentOptions.TopLeft);
            quote.text = "“" + WebIntroductions.IntroLine(settings.Seed, person.id, person.name, person.traits) + "”";
            quote.fontStyle = FontStyles.Italic;
            Place(quote.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -134f), new Vector2(520f, 60f));

            var prompt = HudPrimitives.Label("Prompt", card, 13f, UiTheme.Muted, TextAlignmentOptions.Left);
            prompt.text = Localisation.Text(WebIntroductions.Prompt);
            promptLabel = prompt;
            choiceButtons.Clear();
            Place(prompt.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -202f), new Vector2(520f, 20f));

            choicesSlot = new GameObject("Choices", typeof(RectTransform)).GetComponent<RectTransform>();
            choicesSlot.SetParent(card, false);
            Place(choicesSlot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -228f), new Vector2(520f, 196f));
            float y = 0f;
            var icons = new[] { UiTheme.Icon("heart"), UiTheme.Icon("target"), UiTheme.Pack(PackArt.IconFire) };
            for (int i = 0; i < WebIntroductions.Approaches.Count; i++)
            {
                var approach = WebIntroductions.Approaches[i];
                Choice(choicesSlot, approach, icons[i], y, person, index, count);
                y -= 60f;
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
            Place(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(520f, 52f));
            UiTheme.AddBorder(row, 10, UiTheme.Outline);
            var image = row.GetComponent<Image>();
            image.raycastTarget = true;

            if (icon != null)
            {
                var mark = Picture("Icon", row, icon, UiTheme.Heading, new Vector2(18f, 18f));
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
        /// The player's answer: committed once, however many times it is pressed, then the
        /// houseguest's reply where the choices were and a Next that takes the keyboard - left to
        /// itself, the ring would have fallen through to "Skip Introductions". An answer the game
        /// could not commit keeps the choices, with the reason over them, so it can be tried again.
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
            var slot = choicesSlot;
            foreach (Transform child in slot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            choiceButtons.Clear();
            string line = Localisation.Format("{0}: “{1}”", person.name,
                WebIntroductions.Reaction(settings.Seed, person.id, person.name, person.traits, approach));
            var reply = HudPrimitives.Label("Reaction", slot, 15f, UiTheme.Paper, TextAlignmentOptions.TopLeft);
            reply.text = line;
            reply.fontStyle = FontStyles.Italic;
            Place(reply.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -8f), new Vector2(520f, 44f));
            Animate(reply, 0f, 0.3f, t =>
            {
                reply.alpha = t;
                reply.rectTransform.anchoredPosition = new Vector2(0f, -8f - 10f * (1f - t));
            });

            settings.GuestReacts?.Invoke(person.id, result.Outcome);
            SetProgress((index + 1) / (float)count);

            bool last = index == count - 1;
            string caption = last ? LetsPlayCaption : NextCaption;
            var next = HudPrimitives.Fill(caption, slot, UiTheme.ActionBlue, 8);
            // Below where the next card's rows will stand, so a second click cannot land on one.
            Place(next, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -176f), new Vector2(150f, 36f));
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
            StartCoroutine(Arm(new List<Button> { button }, button));
        }

        /// <summary>
        /// Holds new controls out of reach of real input for <see cref="ArmSeconds"/> - the skip with
        /// them, so the keyboard ring has nowhere to fall - then gives the keyboard to
        /// <paramref name="focus"/>. The buttons themselves, never a group over them: a group made
        /// non-interactable leaves its controls greyed for good.
        /// </summary>
        private IEnumerator Arm(List<Button> controls, Selectable focus)
        {
            float wait = Application.isBatchMode ? 0f : ArmSeconds;
            if (wait > 0f)
            {
                foreach (var control in controls) if (control != null) control.interactable = false;
                if (skipIntroductions != null) skipIntroductions.interactable = false;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                while (wait > 0f) { wait -= Time.unscaledDeltaTime; yield return null; }
                foreach (var control in controls) if (control != null) control.interactable = true;
                if (skipIntroductions != null) skipIntroductions.interactable = true;
            }
            if (focus != null && IsMeeting) Select(focus.gameObject);
        }

        private void SetProgress(float share)
        {
            if (progressFill == null) return;
            float from = progressFill.sizeDelta.x;
            float to = 560f * Mathf.Clamp01(share);
            var fill = progressFill;
            Animate(fill, 0f, 0.3f, t => fill.sizeDelta = new Vector2(Mathf.Lerp(from, to, t), 0f));
        }

        /// <summary>The card leaving: out to the left and gone, as the reference build's does.</summary>
        private void SlideOut(RectTransform rect)
        {
            if (rect == null) return;
            rect.name = "Leaving";
            foreach (var control in rect.GetComponentsInChildren<Selectable>()) control.interactable = false;
            if (Motionless) { rect.gameObject.SetActive(false); Destroy(rect.gameObject); return; }
            var fader = Fader(rect);
            var from = rect.anchoredPosition;
            Animate(rect, 0f, 0.3f, t =>
            {
                fader.alpha = 1f - t;
                rect.anchoredPosition = from + new Vector2(-40f * t, 0f);
            });
            Destroy(rect.gameObject, 0.35f);
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
