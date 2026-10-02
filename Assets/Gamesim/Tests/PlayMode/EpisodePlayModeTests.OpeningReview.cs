using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// What the review of the opening found and fixed: an introduction the game could not commit,
    /// a meet resumed with a gap in it, the house still walking home when the cards start, a
    /// keyboard nobody could see, and heads left turned to a camera that had gone.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// An introduction the game could not commit - a save that failed, say - keeps the three
        /// choices on the card with the reason over them, so it can be made again. It used to take
        /// the choices away and offer only Next, and the houseguest was lost for the session.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_ARefusedIntroductionKeepsTheChoices()
        {
            var season = SequenceSeason();
            var recorded = new List<string>();
            var calls = new List<string>();
            var plan = SequencePlan(recorded, season);
            var judged = SequenceIntroductions(season, new List<string>());
            plan.Introduce = (id, approach) =>
            {
                calls.Add(id + "/" + approach);
                return calls.Count == 1
                    ? new OpeningSequence.Introduction { Accepted = false, Reason = "The save needs attention." }
                    : judged(id, approach);
            };
            var sequence = Opening();
            sequence.Play(OpeningBeat.InOrder.Take(4), plan);
            yield return SequenceWait(() => sequence.IsMeeting);
            yield return null;

            SequenceButtons(sequence, "Calculated").Single().onClick.Invoke();
            yield return null;
            Assert.That(calls, Has.Count.EqualTo(1));
            foreach (var caption in new[] { "Warm", "Calculated", "Bold" })
                Assert.That(SequenceButtons(sequence, caption), Has.Length.EqualTo(1), caption + " is still offered after the refusal.");
            Assert.That(SequenceTexts(sequence, "Prompt"), Does.Contain("The save needs attention."), "The reason is shown over the choices.");
            Assert.That(SequenceButtons(sequence, OpeningSequence.NextCaption), Is.Empty, "and there is no Next to walk past them with.");

            SequenceButtons(sequence, "Calculated").Single().onClick.Invoke();
            yield return null;
            Assert.That(calls, Has.Count.EqualTo(2), "The second try is committed.");
            Assert.That(SequenceButtons(sequence, OpeningSequence.NextCaption), Has.Length.EqualTo(1), "and the card moves on as usual.");
            Assert.That(SequenceTexts(sequence, "Prompt"), Is.Empty, "The reason goes with the question once the introduction is accepted.");
            sequence.SkipIntroductions();
            yield return SequenceWait(() => !sequence.IsPlaying);
        }

        /// <summary>
        /// A resumed meet passes over everybody already met, not only the ones before the first
        /// stranger: with the first and third already introduced, the cards are the second, then the
        /// fourth. The third used to get a card whose every answer was refused.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_AResumedMeetSkipsEveryoneAlreadyMet()
        {
            var season = SequenceSeason();
            var guests = SequenceGuests(season);
            var met = new HashSet<string> { guests[0].id, guests[2].id };
            var plan = SequencePlan(new List<string>(), season);
            plan.Introduce = SequenceIntroductions(season, new List<string>());
            plan.Introduced = id => met.Contains(id);
            var sequence = Opening();
            sequence.Play(OpeningBeat.InOrder.Take(4), plan);
            yield return SequenceWait(() => sequence.IsMeeting);
            yield return null;
            Assert.That(sequence.CurrentGuestId, Is.EqualTo(guests[1].id));

            SequenceButtons(sequence, "Warm").Single().onClick.Invoke();
            yield return null;
            SequenceButtons(sequence, OpeningSequence.NextCaption).Single().onClick.Invoke();
            yield return SequenceWait(() => sequence.CurrentGuestId != guests[1].id);
            Assert.That(sequence.CurrentGuestId, Is.EqualTo(guests[3].id), "The third houseguest, already met, is passed over.");
            Assert.That(SequenceTexts(sequence, "Count").LastOrDefault(), Is.EqualTo("4 of 7"), "and the count still says where they stand.");
            sequence.SkipIntroductions();
            yield return SequenceWait(() => !sequence.IsPlaying);
        }

        /// <summary>
        /// The introductions start with everybody home. A walk-in moved on by Continue hands over
        /// with the house still walking, and the cards framed spots people were walking out of; now
        /// anybody not home is put there first, behind black, as a skip does it.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_TheHouseIsPutHomeBeforeTheIntroductions()
        {
            var season = SequenceSeason();
            var plan = SequencePlan(new List<string>(), season);
            plan.Introduce = SequenceIntroductions(season, new List<string>());
            var stage = new FakeStage(SequenceGuests(season).Count);
            stage.TryPlace();
            var sequence = Opening();
            stage.Opaque = () => sequence.GetComponentsInChildren<Image>(false)
                .Any(image => image.name == "Scrim" && image.enabled && image.color.a >= 0.99f);
            stage.CardUp = () => SequenceNode(sequence, "Card") != null;
            plan.Stage = stage;
            Assert.That(stage.AllHome, Is.False, "The house is out in the yard, as a walk-in cut short leaves it.");

            sequence.Play(OpeningBeat.InOrder.Take(4), plan);
            yield return SequenceWait(() => sequence.IsMeeting);
            Assert.That(stage.Count("RestoreHome"), Is.EqualTo(1), "Everybody is put home before the first card.");
            Assert.That(stage.RestoredBehindOpaque, Has.All.True, "behind black.");
            Assert.That(stage.Count("StrikeSet"), Is.GreaterThanOrEqualTo(1), "and the front door is down.");
            AssertHeldBeforeTheFirstCard(stage);
            sequence.SkipIntroductions();
            yield return SequenceWait(() => !sequence.IsPlaying);
        }

        /// <summary>
        /// The keyboard can be seen on the introductions: every choice and the skip brighten and
        /// raise their edge when focused. Unity's default darkened a raised fill by four per cent,
        /// and the skip had nothing to draw a focus on at all.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_TheKeyboardCanBeSeenOnTheIntroductions()
        {
            var season = SequenceSeason();
            var plan = SequencePlan(new List<string>(), season);
            plan.Introduce = SequenceIntroductions(season, new List<string>());
            var sequence = Opening();
            sequence.Play(OpeningBeat.InOrder.Take(4), plan);
            yield return SequenceWait(() => sequence.IsMeeting);
            yield return null;

            foreach (var caption in new[] { "Warm", "Calculated", "Bold", OpeningSequence.SkipIntroductionsCaption })
            {
                var button = SequenceButtons(sequence, caption).Single();
                Assert.That(button.colors.selectedColor.grayscale, Is.GreaterThan(button.colors.normalColor.grayscale * 1.15f),
                    caption + " is visibly brighter when the keyboard is on it.");
                Assert.That(button.GetComponent<HudEmphasis>(), Is.Not.Null, caption + " raises its edge when focused.");
            }
            sequence.SkipIntroductions();
            yield return SequenceWait(() => !sequence.IsPlaying);
        }

        /// <summary>
        /// When the opening ends, every head it turned is let go: an introduction held a houseguest's
        /// head on the lens for half a minute, through the cards after it and into free time.
        /// </summary>
        [UnityTest]
        public IEnumerator Director_IntroducedHeadsAreLetGoWhenTheOpeningEnds()
        {
            yield return ToTheIntroductions();
            var guest = director.Opening.CurrentGuestId;
            var body = SceneComponents<House.HouseNpc>().Single(npc => npc.Id == guest).GetComponent<CharacterPresentation>();
            Assert.That(body, Is.Not.Null);
            // The fixture runs reduced motion, under which heads do not turn; the target is still set.
            Assert.That(body.LookTarget, Is.Not.Null, "The houseguest being introduced looks at the lens.");
            director.Opening.SkipIntroductions();
            yield return Frames(3);
            Assert.That(director.Opening.IsPlaying, Is.False);
            foreach (var visual in SceneComponents<CharacterPresentation>())
                Assert.That(visual.LookTarget, Is.Null, visual.CharacterId + " is not left looking at the camera.");
        }
    }
}
