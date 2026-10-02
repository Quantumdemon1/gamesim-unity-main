using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
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
    /// now has the skip pill's ground as a sibling behind its words, at both text sizes, and every
    /// word of them fits its box.
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

        /// <summary>What a node's canvas groups leave of its alpha: every group above it, up to one that ignores its parents.</summary>
        private static float GroupAlpha(Transform node)
        {
            float alpha = 1f;
            for (var at = node; at != null; at = at.parent)
            {
                var group = at.GetComponent<CanvasGroup>();
                if (group == null) continue;
                alpha *= group.alpha;
                if (group.ignoreParentGroups) break;
            }
            return alpha;
        }

        /// <summary>
        /// Fails unless <paramref name="words"/> has a ground beside it: a sibling named
        /// <see cref="OpeningSequence.GroundName"/>, drawn, behind the words, and reaching around them;
        /// and, when <paramref name="shown"/>, faded all the way in under every group above it - the
        /// count's chip fades in on a group of its own, which the ground's own colour does not show.
        /// </summary>
        private static void AssertOnAGround(Transform words, string what, bool shown = false)
        {
            Assert.That(words, Is.Not.Null, what + ": there are no words.");
            var ground = words.parent != null ? words.parent.Find(OpeningSequence.GroundName) : null;
            Assert.That(ground, Is.Not.Null, what + " has no ground beside it.");
            Assert.That(ground.GetSiblingIndex(), Is.LessThan(words.GetSiblingIndex()), what + ": the ground is drawn behind the words.");
            var image = ground.GetComponent<Image>();
            Assert.That(image != null && image.enabled && image.color.a > 0.99f, Is.True, what + ": the ground is drawn, opaque.");
            Assert.That(Inside((RectTransform)ground, (RectTransform)words), Is.True,
                what + ": the words " + SequenceWorldRect((RectTransform)words) + " stand on the ground " + SequenceWorldRect((RectTransform)ground) + ".");
            if (shown)
                Assert.That(GroupAlpha(ground), Is.GreaterThanOrEqualTo(0.99f),
                    what + ": the ground is faded in, not at " + GroupAlpha(ground).ToString("F2") + " under its groups.");
        }

        /// <summary>
        /// The colour a capture drew six pixels inside a rect's left edge, halfway up it and
        /// <paramref name="up"/> pixels above that. Only inside a capture's inspection, while the
        /// overlays are drawn through the view camera.
        /// </summary>
        private Color InsideLeftEdge(Texture2D frame, Transform rect, float up = 0f)
        {
            var area = CaptureRectOf(rect);
            int x = Mathf.Clamp(Mathf.RoundToInt(area.xMin + 6f), 0, frame.width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(area.center.y + up), 0, frame.height - 1);
            return frame.GetPixel(x, y);
        }

        /// <summary>
        /// Fails unless the capture drew <paramref name="ground"/>: six pixels inside its left edge
        /// the frame reads, within .06 a channel, what it reads six pixels inside
        /// <paramref name="reference"/>'s - a control filled with the same raised colour, drawn
        /// through the same camera in the same frame (the skip pill on the show's beats, an
        /// unselected choice row on the introductions) - and the same, within .04, four pixels above
        /// and below: one fill, not the house behind it. A frame drawn through the view camera is
        /// tonemapped and its depth of field reaches the overlays, so the raised colour is not its
        /// authored value there; a reference through the same camera is.
        /// </summary>
        private void AssertGroundIsDrawn(Texture2D frame, Transform ground, Transform reference, string what)
        {
            Assert.That(ground, Is.Not.Null, what + ": there is no ground.");
            Assert.That(reference, Is.Not.Null, what + ": there is nothing to compare the ground with.");
            string Hex(Color colour) => ColorUtility.ToHtmlStringRGB(colour);
            var got = InsideLeftEdge(frame, ground);
            var want = InsideLeftEdge(frame, reference);
            Assert.That(Mathf.Max(want.r, want.g, want.b), Is.LessThan(0.4f),
                what + ": " + reference.name + " reads " + Hex(want) + " six pixels in, not the raised fill it is drawn with.");
            foreach (var (channel, a, b) in new[] { ("r", got.r, want.r), ("g", got.g, want.g), ("b", got.b, want.b) })
                Assert.That(Mathf.Abs(a - b), Is.LessThanOrEqualTo(0.06f),
                    what + ": six pixels inside the ground the frame reads " + Hex(got) + " where " + reference.name + " reads " + Hex(want)
                    + " (" + channel + "): the ground is not drawn.");
            foreach (float up in new[] { -4f, 4f })
            {
                var near = InsideLeftEdge(frame, ground, up);
                float spread = Mathf.Max(Mathf.Abs(near.r - got.r), Mathf.Abs(near.g - got.g), Mathf.Abs(near.b - got.b));
                Assert.That(spread, Is.LessThanOrEqualTo(0.04f),
                    what + ": the ground is one fill, not the house behind it (" + Hex(got) + " beside " + Hex(near) + ").");
            }
        }

        /// <summary>
        /// Every hint the stage lays over the house has a ground behind its words, at both text
        /// sizes, and every word of them fits its box: the Continue hint, on the skip pill's row at
        /// its left and at its height, clear of the doorway in the middle of the frame; the reveal's
        /// count, in paper on a chip the hint does not reach, uncut for every houseguest of a house of
        /// sixteen ("16 OF 16"); the house-entry card's season line - the longest SeasonBuilder
        /// writes, an All-Stars house's - in the card under the count, uncut, in a box three Inter
        /// lines tall and clear of the hint; and the introductions' heading and count, on a header
        /// ground that stops short of the card.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_EveryHintOnTheStageStandsOnAGround()
        {
            var season = SequenceSeason();
            var full = FullHouse(17, EpisodeValidation.MaximumCast);
            Assert.That(full.Active.Count(), Is.EqualTo(EpisodeValidation.MaximumCast), "A house of sixteen.");
            // An All-Stars house asked for at sixteen: the builder seats the roster's twelve, and its
            // line carries the All-Stars clause, the longest it writes.
            var allStars = SeasonBuilder.Create(new SeasonBuilder.Choice { Roster = CastTemplates.Roster.AllStars, HouseSize = EpisodeValidation.MaximumCast }, 5u);
            string longest = allStars.events.Last(entry => entry.kind == "arrival").text;
            Assert.That(longest, Does.Contain("played before"), "The All-Stars line, the builder's longest.");
            var sequence = Opening();
            foreach (float scale in new[] { 1f, 1.2f })
            {
                sequence.FontScale = scale;
                string at = " at text scale " + scale;

                // The reveals of a house of sixteen: the Continue hint, and every count.
                var cast = full.Active.OrderBy(person => person.isPlayer ? 0 : 1).ToList();
                sequence.Play(new string[0], SequencePlan(new List<string>(), full));
                for (int i = 0; i < cast.Count; i++)
                {
                    var person = cast[i];
                    yield return SequenceStep(sequence, () => sequence.CurrentGuestId == person.id);
                    Assert.That(sequence.CurrentGuestId, Is.EqualTo(person.id), person.name + "'s reveal is up" + at + ".");
                    yield return null;
                    var third = SequenceNode(sequence, "Lower third");
                    var counter = SequenceLabels(third).Single(label => label.name == "Counter");
                    counter.ForceMeshUpdate();
                    Assert.That(counter.text, Is.EqualTo((i + 1) + " of " + cast.Count));
                    Assert.That(counter.isTextTruncated, Is.False, "\"" + counter.text.ToUpperInvariant() + "\" fits its chip" + at + ".");
                    if (i > 0 && i < cast.Count - 1) continue;
                    AssertOnAGround(counter.transform, "The reveal's count " + counter.text + at, shown: true);
                    Assert.That(counter.color, Is.EqualTo(UiTheme.Paper), "The count is paper on its chip, not muted on the yard" + at + ".");
                    Assert.That(counter.rectTransform.rect.height, Is.GreaterThanOrEqualTo(counter.fontSize * 1.3f), "in a box Inter draws into" + at + ".");

                    var hint = (RectTransform)SequenceNode(sequence, "Hint");
                    Assert.That(hint, Is.Not.Null, "The Continue hint is on the reveal" + at + ".");
                    AssertOnAGround(hint.Find("Label"), "The Continue hint's words" + at, shown: true);
                    AssertOnAGround(hint.Find("Key"), "The Continue hint's key" + at, shown: true);
                    var words = hint.GetComponentsInChildren<TMP_Text>().Single(label => label.name == "Label");
                    words.ForceMeshUpdate();
                    Assert.That(words.text, Is.EqualTo(OpeningSequence.ContinueCaption), "The hint's words are the caption" + at + ".");
                    Assert.That(words.isTextTruncated, Is.False, "and they fit" + at + ".");
                    var pill = (RectTransform)SequenceButtons(sequence, OpeningSequence.SkipCaption).Single(button => button.IsActive()).transform;
                    Assert.That(hint.anchoredPosition.y, Is.EqualTo(pill.anchoredPosition.y), "The hint stands on the skip pill's row" + at + ",");
                    Assert.That(hint.sizeDelta.y, Is.EqualTo(pill.sizeDelta.y), "at the pill's height" + at + ",");
                    Assert.That(SequenceWorldRect(hint).xMax, Is.LessThan(SequenceWorldRect(pill).xMin),
                        "to its left, clear of the doorway in the middle of the frame" + at + ".");
                    Assert.That(Inside(hint, counter.rectTransform), Is.False, "The count and the hint stand apart" + at + ".");
                }
                sequence.Skip();
                yield return null;

                // The house entry: the season's longest line in the card, under the count, uncut and clear of the hint.
                var plan = SequencePlan(new List<string>(), full);
                plan.ArrivalLine = longest;
                sequence.Play(new[] { OpeningBeat.Intro }, plan);
                yield return SequenceWait(() => sequence.CurrentBeat == OpeningBeat.HouseEntry, 2f, 200);
                Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.HouseEntry), "The house entry is up" + at + ".");
                yield return null;
                var card = (RectTransform)SequenceNode(sequence, "Card");
                var line = SequenceLabels(sequence).Single(label => label.name == "Line");
                var entered = SequenceLabels(card).Single(label => label.name == "Entered");
                Assert.That(line.text, Is.EqualTo(longest), "The line is the season's" + at + ",");
                Assert.That(line.transform.IsChildOf(card), Is.True, "in the card" + at + ",");
                Assert.That(Inside(card, line.rectTransform), Is.True, "inside it" + at + ",");
                Assert.That(SequenceWorldRect(line.rectTransform).yMax, Is.LessThanOrEqualTo(SequenceWorldRect(entered.rectTransform).yMin + 1f),
                    "under the count" + at + ",");
                // Inter draws nothing into a box under about 1.21 times its size: three lines need 3 x 1.21 of it.
                Assert.That(line.rectTransform.rect.height, Is.GreaterThanOrEqualTo(3f * 1.21f * line.fontSize), "in a box three lines tall" + at + ",");
                line.ForceMeshUpdate();
                Assert.That(line.isTextTruncated, Is.False, "and every word of it drawn" + at + ".");
                var entryHint = (RectTransform)SequenceNode(sequence, "Hint");
                AssertOnAGround(entryHint.Find("Label"), "The Continue hint on the house entry" + at, shown: true);
                Assert.That(SequenceWorldRect(entryHint).Overlaps(SequenceWorldRect(line.rectTransform)), Is.False, "The line no longer crowds the hint" + at + ".");
                sequence.Skip();
                yield return null;

                // The introductions: the heading and the count on the header's ground.
                var meet = SequencePlan(new List<string>(), season);
                meet.Introduce = SequenceIntroductions(season, new List<string>());
                sequence.Play(SequenceShowBeats, meet);
                yield return SequenceWait(() => sequence.IsMeeting && SequenceNode(sequence, "Card") != null, 2f, 200);
                Assert.That(sequence.IsMeeting, Is.True, "The introductions are up" + at + ".");
                yield return null;
                var column = SequenceNode(sequence, "Introductions");
                Assert.That(column, Is.Not.Null);
                var heading = SequenceLabels(column).Single(label => label.name == "Heading");
                var count = SequenceLabels(column).Single(label => label.name == "Count");
                AssertOnAGround(heading.transform, "The introductions' heading" + at, shown: true);
                AssertOnAGround(count.transform, "The introductions' count" + at, shown: true);
                var ground = (RectTransform)column.Find(OpeningSequence.GroundName);
                var meetCard = (RectTransform)SequenceNode(column, "Card");
                Assert.That(SequenceWorldRect(ground).yMin, Is.GreaterThanOrEqualTo(SequenceWorldRect(meetCard).yMax - 1f),
                    "The header's ground stops short of the card" + at + ".");
                Assert.That(sequence.IntroductionsReach, Is.Not.Null.And.InRange(0.2f, 0.75f),
                    "The column's reach across the frame is read while it is up" + at + ".");
                sequence.Skip();
                yield return null;
                Assert.That(sequence.IntroductionsReach, Is.Null, "and not once it has gone" + at + ".");
            }
            sequence.FontScale = 1f;
        }

        /// <summary>
        /// The default season's arrival line - the catalogue's, frozen as the replay witness
        /// recorded it - says "housemates", and the opening's card says "houseguests" (UI-UX-PASS-PLAN
        /// S0). The house's lists read it in the house's word too: the Recent events card's row and
        /// the notebook's story; the welcome says houseguests. The event keeps its text.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_TheArrivalLineSaysHouseguestsWhereverItIsRead()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            yield return Frames(2);
            var state = director.Snapshot;
            var arrival = state.events.LastOrDefault(entry => entry.kind == "arrival");
            Assert.That(arrival, Is.Not.Null, "The default season logs its arrival.");
            Assert.That(arrival.text, Does.StartWith("Six housemates"), "The catalogue's line keeps its text: the replay witness recorded it.");
            Assert.That(EpisodeDirector.EventLine(state, arrival), Does.StartWith("Six houseguests").And.Not.Contain("housemates"),
                "Read, it is in the house's word.");
            Assert.That(director.StatusMessage ?? string.Empty, Does.Not.Contain("housemates"), "The welcome says houseguests: " + director.StatusMessage);

            Canvas.ForceUpdateCanvases();
            var feed = ActiveRect(EpisodeHud.RecentEventsCardName);
            Assert.That(feed, Is.Not.Null, "The Recent events card is up.");
            var rows = feed.GetComponentsInChildren<TMP_Text>(true).Select(label => label.text ?? string.Empty).ToList();
            Assert.That(rows.Any(text => text.StartsWith("Six houseguests")), Is.True, "The arrival row says houseguests: " + string.Join(" | ", rows));
            Assert.That(rows.Any(text => text.StartsWith("Six housemates")), Is.False, "and never housemates.");

            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Story);
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            var story = director.GetComponentsInChildren<TMP_Text>(false).Select(label => label.text ?? string.Empty).ToList();
            Assert.That(story.Any(text => text.StartsWith("Six houseguests")), Is.True, "The story reads it in the house's word too.");
            Assert.That(story.Any(text => text.StartsWith("Six housemates")), Is.False, "and never in the engine's.");
            director.ClosePanels();
            yield return null;
        }
    }
}
