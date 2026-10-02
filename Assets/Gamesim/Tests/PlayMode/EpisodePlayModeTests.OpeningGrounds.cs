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
    /// The opening stage's hints stand on grounds (UI-UX-PASS-PLAN S0). Laid straight on the
    /// camera's picture, the Continue hint stood inside the lit doorway of every door frame, the
    /// reveal's count was dim grey on the dark yard, the house-entry card's season line ran edge to
    /// edge over the hint, and the introductions' header read through the yard's sign. Every hint
    /// now has the skip pill's ground as a sibling behind its words, at both text sizes.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>A rect's world corners as a rect: the opening's canvas scales with the screen, so these are in the frame's own units.</summary>
        private static Rect SequenceWorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        /// <summary>
        /// Fails unless <paramref name="words"/> has a ground beside it: a sibling named
        /// <see cref="OpeningSequence.GroundName"/>, drawn, behind the words, and reaching around them.
        /// </summary>
        private static void AssertOnAGround(Transform words, string what)
        {
            Assert.That(words, Is.Not.Null, what + ": there are no words.");
            var ground = words.parent != null ? words.parent.Find(OpeningSequence.GroundName) : null;
            Assert.That(ground, Is.Not.Null, what + " has no ground beside it.");
            Assert.That(ground.GetSiblingIndex(), Is.LessThan(words.GetSiblingIndex()), what + ": the ground is drawn behind the words.");
            var image = ground.GetComponent<Image>();
            Assert.That(image != null && image.enabled && image.color.a > 0.5f, Is.True, what + ": the ground is drawn.");
            Assert.That(Inside((RectTransform)ground, (RectTransform)words), Is.True,
                what + ": the words " + SequenceWorldRect((RectTransform)words) + " stand on the ground " + SequenceWorldRect((RectTransform)ground) + ".");
        }

        /// <summary>
        /// Every hint the stage lays over the house has a ground behind its words, at both text
        /// sizes: the Continue hint, on the skip pill's row at its left and at its height, clear of
        /// the doorway in the middle of the frame; the reveal's count, in paper on a chip the hint
        /// does not reach; the house-entry card's season line, in the card under the count with
        /// room for three lines and clear of the hint; and the introductions' heading and count, on
        /// a header ground that stops short of the card.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_EveryHintOnTheStageStandsOnAGround()
        {
            var season = SequenceSeason();
            var sequence = Opening();
            foreach (float scale in new[] { 1f, 1.2f })
            {
                sequence.FontScale = scale;
                string at = " at text scale " + scale;

                // The reveal: the Continue hint and the count.
                sequence.Play(new string[0], SequencePlan(new List<string>(), season));
                yield return SequenceStep(sequence, () => sequence.CurrentGuestId == season.playerId);
                Assert.That(sequence.CurrentGuestId, Is.EqualTo(season.playerId), "The player's reveal is up" + at + ".");
                yield return null;
                var hint = (RectTransform)SequenceNode(sequence, "Hint");
                Assert.That(hint, Is.Not.Null, "The Continue hint is on the reveal" + at + ".");
                AssertOnAGround(hint.Find("Label"), "The Continue hint's words" + at);
                AssertOnAGround(hint.Find("Key"), "The Continue hint's key" + at);
                Assert.That(hint.GetComponentsInChildren<TMP_Text>().Single(label => label.name == "Label").text, Is.EqualTo(OpeningSequence.ContinueCaption),
                    "The hint's words are the caption" + at + ".");
                var pill = (RectTransform)SequenceButtons(sequence, OpeningSequence.SkipCaption).Single(button => button.IsActive()).transform;
                Assert.That(hint.anchoredPosition.y, Is.EqualTo(pill.anchoredPosition.y), "The hint stands on the skip pill's row" + at + ",");
                Assert.That(hint.sizeDelta.y, Is.EqualTo(pill.sizeDelta.y), "at the pill's height" + at + ",");
                Assert.That(SequenceWorldRect(hint).xMax, Is.LessThan(SequenceWorldRect(pill).xMin),
                    "to its left, clear of the doorway in the middle of the frame" + at + ".");
                var third = SequenceNode(sequence, "Lower third");
                var counter = SequenceLabels(third).Single(label => label.name == "Counter");
                AssertOnAGround(counter.transform, "The reveal's count" + at);
                Assert.That(counter.color, Is.EqualTo(UiTheme.Paper), "The count is paper on its chip, not muted on the yard" + at + ".");
                Assert.That(counter.rectTransform.rect.height, Is.GreaterThanOrEqualTo(counter.fontSize * 1.3f), "in a box Inter draws into" + at + ".");
                Assert.That(Inside(hint, counter.rectTransform), Is.False, "The count and the hint stand apart" + at + ".");
                sequence.Skip();
                yield return null;

                // The house entry: the season's line in the card, under the count, clear of the hint.
                sequence.Play(new[] { OpeningBeat.Intro }, SequencePlan(new List<string>(), season));
                yield return SequenceWait(() => sequence.CurrentBeat == OpeningBeat.HouseEntry, 2f, 200);
                Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.HouseEntry), "The house entry is up" + at + ".");
                yield return null;
                var card = (RectTransform)SequenceNode(sequence, "Card");
                var line = SequenceLabels(sequence).Single(label => label.name == "Line");
                var entered = SequenceLabels(card).Single(label => label.name == "Entered");
                Assert.That(line.transform.IsChildOf(card), Is.True, "The season's line is in the card" + at + ",");
                Assert.That(Inside(card, line.rectTransform), Is.True, "inside it" + at + ",");
                Assert.That(SequenceWorldRect(line.rectTransform).yMax, Is.LessThanOrEqualTo(SequenceWorldRect(entered.rectTransform).yMin + 1f),
                    "under the count" + at + ",");
                Assert.That(line.rectTransform.rect.height, Is.GreaterThanOrEqualTo(line.fontSize * 3f), "with room for three lines" + at + ".");
                hint = (RectTransform)SequenceNode(sequence, "Hint");
                AssertOnAGround(hint.Find("Label"), "The Continue hint on the house entry" + at);
                Assert.That(SequenceWorldRect(hint).Overlaps(SequenceWorldRect(line.rectTransform)), Is.False, "The line no longer crowds the hint" + at + ".");
                sequence.Skip();
                yield return null;

                // The introductions: the heading and the count on the header's ground.
                var plan = SequencePlan(new List<string>(), season);
                plan.Introduce = SequenceIntroductions(season, new List<string>());
                sequence.Play(SequenceShowBeats, plan);
                yield return SequenceWait(() => sequence.IsMeeting && SequenceNode(sequence, "Card") != null, 2f, 200);
                Assert.That(sequence.IsMeeting, Is.True, "The introductions are up" + at + ".");
                yield return null;
                var column = SequenceNode(sequence, "Introductions");
                Assert.That(column, Is.Not.Null);
                var heading = SequenceLabels(column).Single(label => label.name == "Heading");
                var count = SequenceLabels(column).Single(label => label.name == "Count");
                AssertOnAGround(heading.transform, "The introductions' heading" + at);
                AssertOnAGround(count.transform, "The introductions' count" + at);
                var ground = (RectTransform)column.Find(OpeningSequence.GroundName);
                var meetCard = (RectTransform)SequenceNode(column, "Card");
                Assert.That(SequenceWorldRect(ground).yMin, Is.GreaterThanOrEqualTo(SequenceWorldRect(meetCard).yMax - 1f),
                    "The header's ground stops short of the card" + at + ".");
                sequence.Skip();
                yield return null;
            }
            sequence.FontScale = 1f;
        }
    }
}
