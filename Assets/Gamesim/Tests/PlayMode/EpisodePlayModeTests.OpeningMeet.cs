using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The introductions brought into line with the reference build's meet and greet: the arming
    /// driven through real input, the question that goes once it is answered, Next under the reply,
    /// one card at a time, straight quotes, the click, and a flame that reads as one.
    ///
    /// <para>The arming is tested through the events a real click and a real Enter deliver.
    /// <c>onClick.Invoke</c> ignores <see cref="Selectable.interactable"/> entirely, so a test that
    /// pressed that way could never see a disarmed control refuse a press.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>A left click as the EventSystem delivers one, which a disarmed button refuses.</summary>
        private static void MeetClick(Button button) =>
            ExecuteEvents.Execute(button.gameObject,
                new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },
                ExecuteEvents.pointerClickHandler);

        /// <summary>Enter or the pad's South on a focused control, as the EventSystem delivers it.</summary>
        private static void MeetSubmit(Button button) =>
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);

        private static Vector3[] MeetCorners(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners;
        }

        /// <summary>
        /// How large the visible part of an icon is drawn, in canvas units: its sprite's opaque
        /// pixels as a share of the sprite, times the box the Image draws the sprite into. Read back
        /// through the GPU so a texture the importer made unreadable can be measured as well.
        /// </summary>
        private static Vector2 MeetGlyphSize(Image image)
        {
            var sprite = image.sprite;
            var texture = sprite.texture;
            var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            var read = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                read.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                read.Apply();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }

            var area = sprite.textureRect;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = Mathf.FloorToInt(area.yMin); y < Mathf.CeilToInt(area.yMax); y++)
                for (int x = Mathf.FloorToInt(area.xMin); x < Mathf.CeilToInt(area.xMax); x++)
                {
                    if (read.GetPixel(x, y).a < 0.5f) continue;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
            UnityEngine.Object.Destroy(read);
            if (maxX < 0) return Vector2.zero;

            var box = image.rectTransform.rect.size;
            float aspect = area.width / area.height;
            var drawn = !image.preserveAspect ? box
                : box.x / box.y > aspect ? new Vector2(box.y * aspect, box.y) : new Vector2(box.x, box.x / aspect);
            return new Vector2(drawn.x * (maxX - minX + 1) / area.width, drawn.y * (maxY - minY + 1) / area.height);
        }

        /// <summary>
        /// The fixture's plan with the movement on and the arming at play's 0.35 s: the card swap
        /// as a player who has not asked for reduced motion sees it, headless.
        /// </summary>
        private static OpeningSequence.Settings MeetMotionPlan(EpisodeState season, List<string> calls)
        {
            var plan = SequencePlan(new List<string>(), season);
            plan.Introduce = SequenceIntroductions(season, calls);
            plan.ReducedMotion = false;
            // Headless runs skip the movement, and the swap with it; these ask for it.
            plan.MotionInBatchmode = true;
            plan.ArmSeconds = 0.35f;
            return plan;
        }

        /// <summary>
        /// The first card answered through real input and its Next brought live: the card's arming
        /// waited out, Warm clicked, and Next's arming waited out, with the keyboard left on Next.
        /// </summary>
        private static IEnumerator MeetToALiveNext(OpeningSequence sequence, List<string> calls)
        {
            yield return SequenceWait(() => sequence.IsMeeting && SequenceNode(sequence, "Warm") != null, 2f, 2000);
            Assert.That(sequence.IsMeeting, Is.True, "The introductions are up.");
            yield return new WaitForSecondsRealtime(0.5f);
            MeetClick(SequenceButtons(sequence, "Warm").Single());
            Assert.That(calls, Has.Count.EqualTo(1), "A click on the armed first card commits.");
            yield return new WaitForSecondsRealtime(0.5f);
            var next = SequenceButtons(sequence, OpeningSequence.NextCaption).Single();
            Assert.That(next.IsInteractable(), Is.True, "Next is live once armed.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(next.gameObject), "and the keyboard is on it.");
        }

        /// <summary>
        /// The first card's choices, and the skip with them, cannot be pressed for the arming's
        /// 0.35 s: a click and an Enter delivered by the EventSystem both come to nothing. Once armed,
        /// the keyboard is on Warm and a click commits. Next is disarmed the same way after the
        /// answer.
        ///
        /// <para>Under reduced motion - the fixture's - the second card is armed too. Nothing leaves
        /// between the two: the card before is dropped and the next stands in its place on the
        /// frame Next is pressed, its Warm row right where Next was. The second click of a
        /// double-click on Next, or a second Enter, used to commit Warm for a houseguest nobody had
        /// read, and an introduction cannot be taken back.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_TheFirstCardIsArmedAgainstRealInput()
        {
            var season = SequenceSeason();
            var guests = SequenceGuests(season);
            var calls = new List<string>();
            var plan = SequencePlan(new List<string>(), season);
            plan.Introduce = SequenceIntroductions(season, calls);
            plan.ArmSeconds = 0.35f;
            var sequence = Opening();
            sequence.Play(SequenceShowBeats, plan);
            yield return SequenceWait(() => sequence.IsMeeting && SequenceNode(sequence, "Warm") != null, 2f, 2000);
            Assert.That(sequence.IsMeeting, Is.True);
            Assert.That(EventSystem.current, Is.Not.Null, "The scene has an EventSystem to deliver input through.");

            // The frame straight after the card was built: no stall can have used the window up yet.
            var choices = new[] { "Warm", "Calculated", "Bold" }.Select(caption => SequenceButtons(sequence, caption).Single()).ToArray();
            var skip = SequenceButtons(sequence, OpeningSequence.SkipIntroductionsCaption).Single();
            foreach (var choice in choices) Assert.That(choice.IsInteractable(), Is.False, choice.name + " is disarmed as the card appears.");
            Assert.That(skip.IsInteractable(), Is.False, "Skip Introductions is disarmed with them, so the ring has nowhere to fall.");
            MeetClick(choices[0]);
            MeetSubmit(choices[1]);
            MeetClick(skip);
            MeetSubmit(skip);
            yield return null;
            Assert.That(calls, Is.Empty, "A click and an Enter on a disarmed choice commit nothing.");
            Assert.That(sequence.IsMeeting, Is.True, "and one on the disarmed skip forfeits nobody.");
            Assert.That(sequence.CurrentGuestId, Is.EqualTo(guests[0].id));

            yield return new WaitForSecondsRealtime(0.5f);
            foreach (var choice in choices) Assert.That(choice.IsInteractable(), Is.True, choice.name + " is live once armed.");
            Assert.That(skip.IsInteractable(), Is.True, "and so is the skip.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(choices[0].gameObject), "The keyboard lands on Warm once it is live.");
            MeetClick(choices[1]);
            CollectionAssert.AreEqual(new[] { guests[0].id + "/" + WebIntroductions.Calculated }, calls, "A real click on a live choice commits it.");

            var next = SequenceButtons(sequence, OpeningSequence.NextCaption).Single();
            Assert.That(next.IsInteractable(), Is.False, "Next is disarmed as it appears.");
            MeetClick(next);
            MeetSubmit(next);
            yield return null;
            Assert.That(sequence.CurrentGuestId, Is.EqualTo(guests[0].id), "A press on a disarmed Next moves nobody on.");

            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(next.IsInteractable(), Is.True, "Next is live once armed.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(next.gameObject), "and the keyboard is on it.");
            MeetSubmit(next);
            yield return SequenceWait(() => sequence.CurrentGuestId == guests[1].id && SequenceNode(sequence, "Warm") != null, 2f, 2000);
            Assert.That(sequence.CurrentGuestId, Is.EqualTo(guests[1].id), "Enter on a live Next moves on to the next houseguest.");
            // A press on the frame a card is built belongs to the card before; the next frame is
            // this one's, so what is refused here is refused by the arming alone.
            yield return null;

            var second = new[] { "Warm", "Calculated", "Bold" }.Select(caption => SequenceButtons(sequence, caption).Single()).ToArray();
            foreach (var choice in second)
                Assert.That(choice.IsInteractable(), Is.False, choice.name + " on the second card is disarmed as it appears: under reduced motion nothing left before it.");
            Assert.That(skip.IsInteractable(), Is.False, "and the skip with them.");
            MeetClick(second[0]);
            MeetSubmit(second[0]);
            MeetSubmit(skip);
            yield return null;
            Assert.That(calls, Has.Count.EqualTo(1), "The second click of a double-click on Next, or a second Enter, commits nothing on the next card.");
            Assert.That(sequence.IsMeeting, Is.True, "and forfeits nobody.");

            yield return new WaitForSecondsRealtime(0.5f);
            foreach (var choice in second) Assert.That(choice.IsInteractable(), Is.True, choice.name + " on the second card is live once armed.");
            Assert.That(skip.IsInteractable(), Is.True, "and so is the skip.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(second[0].gameObject), "The keyboard lands on the second card's Warm once it is live.");
            MeetClick(second[0]);
            CollectionAssert.AreEqual(new[] { guests[0].id + "/" + WebIntroductions.Calculated, guests[1].id + "/" + WebIntroductions.Warm }, calls,
                "A real click on it then commits.");

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// With the movement on, the card that leaves is the gap: 0.35 s out before the next comes
        /// in, the reference build's AnimatePresence in "wait" mode. The arriving card is not armed
        /// again on top of it - its choices are live, and the keyboard on Warm, as soon as it is
        /// there. Arming it as well left the choices dead for twice as long.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_WithMotionTheArrivingCardIsNotArmedAgain()
        {
            var season = SequenceSeason();
            var guests = SequenceGuests(season);
            var calls = new List<string>();
            var sequence = Opening();
            sequence.Play(SequenceShowBeats, MeetMotionPlan(season, calls));
            yield return MeetToALiveNext(sequence, calls);

            MeetSubmit(SequenceButtons(sequence, OpeningSequence.NextCaption).Single());
            yield return SequenceWait(() => sequence.CurrentGuestId == guests[1].id && SequenceNode(sequence, "Warm") != null, 3f, 100000);
            Assert.That(SequenceNode(sequence, "Warm"), Is.Not.Null, guests[1].name + "'s card arrives once the card before has gone.");
            // A press on the frame a card is built belongs to the card before; the next frame is this one's.
            yield return null;

            var choices = new[] { "Warm", "Calculated", "Bold" }.Select(caption => SequenceButtons(sequence, caption).Single()).ToArray();
            foreach (var choice in choices)
                Assert.That(choice.IsInteractable(), Is.True, choice.name + " is live as the card arrives: the swap was the gap, and it is not armed again on top of it.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(choices[0].gameObject), "The keyboard is on Warm.");
            MeetClick(choices[0]);
            CollectionAssert.AreEqual(new[] { guests[0].id + "/" + WebIntroductions.Warm, guests[1].id + "/" + WebIntroductions.Warm }, calls,
                "and a real click on it commits.");

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// "Skip Introductions" is out of the keyboard ring for the whole card swap. Next goes dead
        /// as its card leaves, the EventSystem lets go of it, and the HUD used to hand the keyboard
        /// to the only live control left under the opening - the skip - for the 0.35 s of the leave,
        /// where a second Enter forfeited every houseguest not yet met. Now the keyboard is never
        /// on it during the leave, a press on it comes to nothing, and it is back once the new card
        /// has the keyboard.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_TheSkipIsOutOfReachWhileTheCardsSwap()
        {
            var season = SequenceSeason();
            var guests = SequenceGuests(season);
            var calls = new List<string>();
            var sequence = Opening();
            sequence.Play(SequenceShowBeats, MeetMotionPlan(season, calls));
            yield return MeetToALiveNext(sequence, calls);

            var column = SequenceNode(sequence, "Introductions");
            Assert.That(column, Is.Not.Null);
            var skip = SequenceButtons(sequence, OpeningSequence.SkipIntroductionsCaption).Single();
            Assert.That(skip.IsInteractable(), Is.True, "The skip is live before the swap.");
            MeetSubmit(SequenceButtons(sequence, OpeningSequence.NextCaption).Single());

            bool sawLeaving = false, skipWasLive = false, skipHadTheKeyboard = false, pressedDuringLeave = false;
            Transform arriving = null;
            float until = Time.realtimeSinceStartup + 3f;
            for (int frame = 0; frame < 100000 && sequence.IsMeeting && Time.realtimeSinceStartup < until; frame++)
            {
                yield return null;
                var children = column.Cast<Transform>().Where(child => child.gameObject.activeInHierarchy).ToList();
                arriving = children.FirstOrDefault(child => child.name == "Card" && SequenceTexts(child, "Name").Contains(guests[1].name));
                if (arriving != null) break;
                // Read a frame after the HUD's LateUpdate has had its turn at the keyboard.
                skipHadTheKeyboard |= EventSystem.current.currentSelectedGameObject == skip.gameObject;
                if (!children.Any(child => child.name == "Leaving")) continue;
                sawLeaving = true;
                skipWasLive |= skip.IsInteractable();
                if (pressedDuringLeave) continue;
                pressedDuringLeave = true;
                MeetSubmit(skip);
                MeetClick(skip);
            }

            Assert.That(sawLeaving, Is.True, "The answered card is seen leaving.");
            Assert.That(skipWasLive, Is.False, "Skip Introductions is out of reach for the whole leave.");
            Assert.That(skipHadTheKeyboard, Is.False, "and the keyboard never falls onto it while the card leaves.");
            Assert.That(sequence.IsMeeting, Is.True, "An Enter and a click on the skip during the leave forfeit nobody.");
            Assert.That(arriving, Is.Not.Null, guests[1].name + "'s card arrives.");
            Assert.That(sequence.CurrentGuestId, Is.EqualTo(guests[1].id));
            Assert.That(skip.IsInteractable(), Is.True, "The skip is back in the ring once the new card is up.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(SequenceButtons(arriving, "Warm").Single().gameObject),
                "with the keyboard on the new card's Warm, not on the skip.");

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// Once answered, the question goes (the reference build's MeetAndGreetPhase.tsx:539-543), the
        /// reply stands where it was, Next is right-aligned directly under the reply (563-581), and
        /// the card closes up to fit instead of leaving the choices' space empty under them. The
        /// houseguest's line and the reply are in straight quotes, as the reference build prints
        /// them (533, 570).
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_TheQuestionGoesAndNextStandsUnderTheReply()
        {
            var season = SequenceSeason();
            var alex = season.Find("alex-chen");
            var plan = SequencePlan(new List<string>(), season);
            plan.Introduce = SequenceIntroductions(season, new List<string>());
            var sequence = Opening();
            sequence.Play(SequenceShowBeats, plan);
            yield return SequenceWait(() => sequence.IsMeeting && SequenceNode(sequence, "Warm") != null, 2f, 2000);
            yield return null;

            var card = (RectTransform)SequenceNode(sequence, "Card");
            Assert.That(card, Is.Not.Null);
            CollectionAssert.AreEqual(new[] { WebIntroductions.Prompt }, SequenceTexts(card, "Prompt"), "The question is asked.");
            CollectionAssert.AreEqual(new[] { "\"" + WebIntroductions.IntroLine(plan.Seed, alex.id, alex.name, alex.traits) + "\"" },
                SequenceTexts(card, "Quote"), "The houseguest's line is in straight quotes.");
            float asked = card.rect.height;
            var prompt = card.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == "Prompt").rectTransform;
            float promptTop = MeetCorners(prompt)[1].y;

            SequenceButtons(sequence, "Warm").Single().onClick.Invoke();
            yield return null;

            Assert.That(SequenceTexts(sequence, "Prompt"), Is.Empty, "The question goes once it is answered.");
            Assert.That(SequenceNode(card, "Choices"), Is.Null, "and the choices with it.");
            string reaction = WebIntroductions.Reaction(plan.Seed, alex.id, alex.name, alex.traits, WebIntroductions.Find(WebIntroductions.Warm));
            var replies = SequenceLabels(sequence).Where(label => label.name == "Reaction").ToArray();
            Assert.That(replies, Has.Length.EqualTo(1));
            Assert.That(replies[0].text, Is.EqualTo(alex.name + ": \"" + reaction + "\""), "The reply is Name: \"...\", in straight quotes.");
            Assert.That(SequenceLabels(sequence).Where(label => label.text != null && (label.text.Contains("“") || label.text.Contains("”"))), Is.Empty,
                "No curly quote is left anywhere on the introductions.");

            float unit = sequence.transform.lossyScale.y;
            var reply = MeetCorners(replies[0].rectTransform);
            var next = MeetCorners((RectTransform)SequenceButtons(sequence, OpeningSequence.NextCaption).Single().transform);
            var closed = MeetCorners(card);
            Assert.That(Mathf.Abs(reply[1].y - promptTop) / unit, Is.LessThan(1f), "The reply stands where the question was.");
            Assert.That(next[1].y, Is.LessThanOrEqualTo(reply[0].y + 0.01f), "Next is below the reply.");
            Assert.That((reply[0].y - next[1].y) / unit, Is.LessThanOrEqualTo(24f), "directly below it, not down at the foot of the card.");
            Assert.That(Mathf.Abs(next[2].x - reply[2].x) / unit, Is.LessThan(1f), "and right-aligned with it.");
            Assert.That(card.rect.height, Is.LessThan(asked - 60f), "The card closes up once the choices have gone.");
            Assert.That((next[0].y - closed[0].y) / unit, Is.InRange(0f, 24f), "to just under Next.");

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// One card at a time: the card that has been answered leaves, its controls dead from the
        /// first frame, and only when it has gone - 0.35 s on - does the next come in (the reference
        /// build's AnimatePresence mode="wait", MeetAndGreetPhase.tsx:496-502). The two used to
        /// overlap, the new one arriving while the old was still sliding off over it.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_OneCardLeavesBeforeTheNextArrives()
        {
            var season = SequenceSeason();
            var guests = SequenceGuests(season);
            var plan = SequencePlan(new List<string>(), season);
            plan.Introduce = SequenceIntroductions(season, new List<string>());
            plan.ReducedMotion = false;
            // Headless runs skip the movement, and the swap with it; this one asks for it.
            plan.MotionInBatchmode = true;
            var sequence = Opening();
            sequence.Play(SequenceShowBeats, plan);
            yield return SequenceWait(() => sequence.IsMeeting && SequenceNode(sequence, "Warm") != null, 2f, 2000);
            yield return null;

            SequenceButtons(sequence, "Warm").Single().onClick.Invoke();
            yield return null;
            var column = SequenceNode(sequence, "Introductions");
            Assert.That(column, Is.Not.Null);
            float pressed = Time.realtimeSinceStartup;
            SequenceButtons(sequence, OpeningSequence.NextCaption).Single().onClick.Invoke();

            int most = 0;
            bool sawLeaving = false, leavingWasLive = false;
            float arrived = -1f, arrivingAlpha = -1f;
            float until = Time.realtimeSinceStartup + 3f;
            for (int frame = 0; frame < 100000 && Time.realtimeSinceStartup < until; frame++)
            {
                var cards = column.Cast<Transform>()
                    .Where(child => child.gameObject.activeInHierarchy && (child.name == "Card" || child.name == "Leaving")).ToList();
                most = Mathf.Max(most, cards.Count);
                foreach (var leaving in cards.Where(child => child.name == "Leaving"))
                {
                    sawLeaving = true;
                    leavingWasLive |= leaving.GetComponentsInChildren<Selectable>().Any(control => control.IsInteractable());
                }
                var arriving = cards.FirstOrDefault(child => child.name == "Card" && SequenceTexts(child, "Name").Contains(guests[1].name));
                if (arriving != null)
                {
                    arrived = Time.realtimeSinceStartup;
                    arrivingAlpha = arriving.GetComponent<CanvasGroup>().alpha;
                    break;
                }
                yield return null;
            }

            Assert.That(arrived, Is.GreaterThan(0f), guests[1].name + "'s card arrives.");
            Assert.That(most, Is.LessThanOrEqualTo(1), "Never two cards on screen at once.");
            Assert.That(sawLeaving, Is.True, "The answered card is seen leaving.");
            Assert.That(leavingWasLive, Is.False, "with its controls already dead.");
            Assert.That(arrived - pressed, Is.GreaterThanOrEqualTo(0.3f), "The next card waits for the last to have gone.");
            Assert.That(arrivingAlpha, Is.LessThan(0.9f), "and then comes in, rather than appearing whole.");

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// The house's click plays once for each introduction that is committed - the reference
        /// build clicks on a choice - and not for one the game refused, nor for pressing an answered
        /// choice again.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_ClicksOnceForEachAcceptedIntroduction()
        {
            var season = SequenceSeason();
            var guests = SequenceGuests(season);
            var calls = new List<string>();
            int clicks = 0;
            var plan = SequencePlan(new List<string>(), season);
            var judged = SequenceIntroductions(season, new List<string>());
            plan.Introduce = (id, approach) =>
            {
                calls.Add(id + "/" + approach);
                return calls.Count == 1
                    ? new OpeningSequence.Introduction { Accepted = false, Reason = "The save needs attention." }
                    : judged(id, approach);
            };
            plan.Click = () => clicks++;
            var sequence = Opening();
            sequence.Play(SequenceShowBeats, plan);
            yield return SequenceWait(() => sequence.IsMeeting && SequenceNode(sequence, "Warm") != null, 2f, 2000);
            yield return null;

            var press = SequenceButtons(sequence, "Calculated").Single().onClick;
            press.Invoke();
            Assert.That(calls, Has.Count.EqualTo(1));
            Assert.That(clicks, Is.Zero, "A refused introduction makes no click.");
            press.Invoke();
            Assert.That(calls, Has.Count.EqualTo(2));
            Assert.That(clicks, Is.EqualTo(1), "An accepted one clicks once.");
            yield return null;
            press.Invoke();
            Assert.That(clicks, Is.EqualTo(1), "Pressing the answered choice again does not click again.");

            SequenceButtons(sequence, OpeningSequence.NextCaption).Single().onClick.Invoke();
            yield return SequenceWait(() => sequence.CurrentGuestId == guests[1].id && SequenceNode(sequence, "Warm") != null, 2f, 2000);
            yield return null;
            SequenceButtons(sequence, "Warm").Single().onClick.Invoke();
            Assert.That(clicks, Is.EqualTo(2), "The next houseguest's introduction clicks once more.");

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// Bold's flame is drawn at the size of the row's other icons. The pack's fire stood in the
        /// middle of a large transparent canvas and drew as a dot about half the height of the heart
        /// beside it (capture op-09-meet-card).
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_TheBoldFlameIsDrawnAtIconSize()
        {
            var season = SequenceSeason();
            var plan = SequencePlan(new List<string>(), season);
            plan.Introduce = SequenceIntroductions(season, new List<string>());
            var sequence = Opening();
            sequence.Play(SequenceShowBeats, plan);
            yield return SequenceWait(() => sequence.IsMeeting && SequenceNode(sequence, "Warm") != null, 2f, 2000);
            yield return null;

            var card = SequenceNode(sequence, "Card");
            var warmIcon = SequenceNode(card, "Warm").Find("Icon");
            var boldIcon = SequenceNode(card, "Bold").Find("Icon");
            Assert.That(warmIcon, Is.Not.Null, "Warm carries its heart.");
            Assert.That(boldIcon, Is.Not.Null, "Bold carries an icon.");
            var heart = warmIcon.GetComponent<Image>();
            var flame = boldIcon.GetComponent<Image>();
            Assert.That(flame.sprite, Is.Not.Null);

            var box = flame.rectTransform.rect.size;
            var drawnFlame = MeetGlyphSize(flame);
            var drawnHeart = MeetGlyphSize(heart);
            Assert.That(drawnFlame.y, Is.GreaterThanOrEqualTo(0.65f * box.y), "The flame stands most of the row's icon box tall, not a dot in the middle of it.");
            Assert.That(drawnFlame.y, Is.GreaterThanOrEqualTo(0.85f * drawnHeart.y), "It stands about as tall as the heart beside it.");
            Assert.That(drawnFlame.x, Is.GreaterThanOrEqualTo(0.5f * box.x), "and is wide enough to read as a shape.");
            Assert.That(drawnFlame.y, Is.LessThanOrEqualTo(box.y + 0.01f), "It fits its box.");

            sequence.Skip();
            yield return null;
        }
    }
}
