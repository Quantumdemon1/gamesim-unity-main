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
    /// The strategy stage (PACK8-PASS-PLAN A3): the week's four strategy screens - the nomination, the
    /// veto's draw, the veto meeting and the campaign - take one taller frame, from under the top bar
    /// to the frame's foot, with the status line moved under the rail. The owner's screenshots 66 to
    /// 80 were these screens scrolling in the stage's 638 units, or in a house event's 403 when a
    /// story beat or a plea was up. Each keeps the rail and the status line clear, and pins exactly
    /// one way on.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The strategy screen now open is on the strategy stage: most of the frame, the whole of it
        /// from under the top bar to the foot and from the rail's column to the edge, clear of the
        /// rail and of the status line it moved under the rail, which says all of its words there;
        /// the way on pinned once and nothing else pinned beside it but the footer's
        /// <paramref name="secondary"/> when the screen has one (free time's "Stay in the house");
        /// the scroll clear of them; and the hidden hint still true of the scroll.
        /// </summary>
        private void AssertOnTheStrategyStage(string wayOn, string where, string secondary = null)
        {
            Canvas.ForceUpdateCanvases();
            var hud = director.GetComponentInChildren<EpisodeHud>();
            Assert.That(hud.CurrentActivityLayout, Is.EqualTo(EpisodeHud.ActivityLayout.Strategy), where + " takes the strategy stage.");
            var panel = ActiveRect("Episode panel");
            AssertOnTheStage(panel);
            var frame = ((RectTransform)panel.parent).rect;
            Assert.That(panel.rect.height, Is.EqualTo(frame.height - IconRail.Top - EpisodeHud.StrategyFoot).Within(1f),
                where + " runs from under the top bar to the frame's foot.");
            Assert.That(panel.rect.width, Is.EqualTo(frame.width - EpisodeHud.LeftColumnX - EpisodeHud.StrategyEdge).Within(1f),
                where + " runs from the rail's column to the frame's edge.");

            var stage = ScreenRect(panel);
            foreach (var name in new[] { IconRail.RootName, "Rail ground", "Status" })
            {
                var chrome = ActiveRect(name);
                Assert.That(chrome, Is.Not.Null, name + " stays up beside " + where + ".");
                Assert.That(stage.Overlaps(ScreenRect(chrome)), Is.False, where + " covers '" + name + "'.");
            }
            var status = ScreenRect(ActiveRect("Status"));
            Assert.That(status.Overlaps(ScreenRect(ActiveRect("Rail ground"))), Is.False, "The status line stands under the rail, not on it.");
            Assert.That(status.xMin >= -.5f && status.yMin >= -.5f, Is.True, "and on the frame: " + status + ".");
            // The gutter is a fraction of the width the line had over the strip: it grows up towards
            // the rail rather than cutting off a commit's outcome after two short lines.
            var said = ActiveRect("Status").GetComponentsInChildren<TMP_Text>().Single();
            said.ForceMeshUpdate(true);
            Assert.That(said.isTextTruncated, Is.False,
                "The status line says all of '" + said.text + "', at " + said.fontSize.ToString("0.#") + " in a box " + said.rectTransform.rect.size + ".");

            var pinned = panel.Cast<Transform>().Select(child => child.GetComponent<Button>())
                .Where(button => button != null && button.IsActive() && button.name != "Close  [Esc]")
                .Select(button => button.name).ToArray();
            Assert.That(pinned, Is.EquivalentTo(secondary == null ? new[] { wayOn } : new[] { wayOn, secondary }),
                where + " pins one way on" + (secondary == null ? ", and only it." : " and its secondary, and only them."));
            var onward = ButtonWithCaption(wayOn);
            Assert.That(onward.transform.parent, Is.SameAs(panel), "'" + wayOn + "' is pinned to the panel, not in its scroll.");
            AssertInside(stage, (RectTransform)onward.transform, "'" + wayOn + "'");
            AssertScrollClearOfPinned(panel, onward);
            if (secondary != null)
            {
                var aside = ButtonWithCaption(secondary);
                Assert.That(aside.transform.parent, Is.SameAs(panel), "'" + secondary + "' is pinned to the panel, not in its scroll.");
                AssertInside(stage, (RectTransform)aside.transform, "'" + secondary + "'");
                AssertScrollClearOfPinned(panel, aside);
                Assert.That(ScreenRect((RectTransform)aside.transform).Overlaps(ScreenRect((RectTransform)onward.transform)), Is.False,
                    "'" + secondary + "' and '" + wayOn + "' share the footer's row without meeting.");
            }

            var content = ActiveRect("Episode content");
            var hint = panel.Find(EpisodeHud.PanelHintName);
            Assert.That(hint.gameObject.activeSelf, Is.False, "The control hint stands down on the strategy stage,");
            Assert.That(hint.GetComponent<TMP_Text>().text, Is.EqualTo(content.rect.height > ((RectTransform)content.parent).rect.height + .5f
                ? EpisodeHud.PanelHintScrollCopy : EpisodeHud.PanelHintCopy), "and still says \"Scroll for more\" exactly when there is more.");
        }

        /// <summary>Opens the station's panel at the resting text size, then at the larger, running <paramref name="check"/> at each.</summary>
        private IEnumerator AtBothTextSizes(System.Action<bool> check)
        {
            foreach (bool larger in new[] { false, true })
            {
                if (larger)
                {
                    director.ClosePanels();
                    yield return ApplyTextSize(true);
                }
                yield return OpenStation();
                yield return null;
                check(larger);
            }
            director.ClosePanels();
            yield return ApplyTextSize(false);
        }

        /// <summary>
        /// The nomination with a Head of Household's word waiting (the owner's screenshots 66 to 69):
        /// the beat is drawn inside the stage, as the step it is, rather than turning the panel into a
        /// house event's band with "Continue episode" at the foot of its scroll; the way on stays
        /// pinned, and what moving on lets pass stands in the footer's strip beside it.
        /// </summary>
        [UnityTest]
        public IEnumerator StrategyStage_TheNominationTakesTheFrameWithItsBeatInside()
        {
            yield return InstallStoryPullFixture();
            var before = director.Snapshot;
            Assert.That(EpisodeEngine.OpenStoryBeats(before), Has.Count.EqualTo(1), "The fixture has a beat waiting.");
            bool lapses = EpisodeEngine.LapsingOnAdvance(before).Count > 0;
            yield return AtBothTextSizes(larger =>
            {
                string where = "The nomination" + (larger ? " at the larger text" : "");
                AssertOnTheStrategyStage("Continue episode", where);
                var choices = ActiveRect(EpisodeHud.StoryChoicesName);
                Assert.That(choices, Is.Not.Null, "The beat's choices are on the screen.");
                Assert.That(choices.IsChildOf(ActiveRect("Episode content")), Is.True, "inside the stage's column, not a band of their own.");
                var strip = ActiveRect(EpisodeHud.StrategyStripName);
                if (!lapses) { Assert.That(strip, Is.Null, "Nothing lapses, so the footer has no strip."); return; }
                Assert.That(strip, Is.Not.Null, "What moving on lets pass is in the footer.");
                Assert.That(strip.parent, Is.SameAs(ActiveRect("Episode panel")), "beside the way on, outside the scroll,");
                var warning = strip.GetComponentsInChildren<TMP_Text>().Single(label => label.name == EpisodeDirector.AdvanceWarningName);
                Assert.That(warning.text, Does.StartWith("Moving on lets "), "under the name a test has always found it by,");
                Assert.That(warning.color, Is.EqualTo(UiTheme.Warning), "in the warning colour.");
                var onward = ScreenRect((RectTransform)FindButton("Continue episode").transform);
                Assert.That(ScreenRect(strip).Overlaps(onward), Is.False, "The strip and the way on share the row without meeting.");
                AssertEveryLabelDraws(strip, where + "'s strip");
            });
            Assert.That(EpisodeEngine.OpenStoryBeats(director.Snapshot), Has.Count.EqualTo(1), "Opening the screen answers nothing.");
            if (Application.isBatchMode)
            {
                yield return OpenStation();
                yield return CaptureFraming("strategy-nomination");
                director.ClosePanels();
                yield return null;
            }
        }

        /// <summary>The veto's draw on the strategy stage: the house as faces, the bag, and "Continue episode" pinned once.</summary>
        [UnityTest]
        public IEnumerator StrategyStage_TheVetoDrawTakesTheFrame()
        {
            yield return InstallStrategySeason(45, state =>
            {
                var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
                state.phase = EpisodePhase.VetoSelection;
                state.hohId = npcs[0];
                state.nominees = new List<string> { npcs[1], npcs[2] };
            });
            yield return AtBothTextSizes(larger =>
            {
                AssertOnTheStrategyStage("Continue episode", "The veto's draw" + (larger ? " at the larger text" : ""));
                Assert.That(ActiveRect(EpisodeHud.ChipBagName), Is.Not.Null, "The draw keeps its bag.");
                Assert.That(ActiveRect(EpisodeHud.CeremonyTitleName).GetComponent<TMP_Text>().text, Is.EqualTo("Power of Veto Player Selection"));
            });
            if (Application.isBatchMode)
            {
                yield return OpenStation();
                yield return CaptureFraming("strategy-veto-draw");
                director.ClosePanels();
                yield return null;
            }
        }

        /// <summary>The veto meeting with a houseguest holding the veto: on the strategy stage, the way on pinned once.</summary>
        [UnityTest]
        public IEnumerator StrategyStage_TheVetoMeetingTakesTheFrame()
        {
            yield return InstallStrategySeason(47, state => AtVetoMeeting(state, false));
            yield return AtBothTextSizes(larger =>
            {
                AssertOnTheStrategyStage("Continue episode", "The veto meeting" + (larger ? " at the larger text" : ""));
                Assert.That(ActiveRect(EpisodeHud.CeremonyTitleName).GetComponent<TMP_Text>().text, Is.EqualTo("Veto Meeting"));
            });
            if (Application.isBatchMode)
            {
                yield return OpenStation();
                yield return CaptureFraming("strategy-veto-meeting");
                director.ClosePanels();
                yield return null;
            }
        }

        /// <summary>The campaign on the strategy stage: the votes as cards, "Close campaigning and open voting" pinned once.</summary>
        [UnityTest]
        public IEnumerator StrategyStage_TheCampaignTakesTheFrame()
        {
            yield return InstallStrategySeason(46, state =>
            {
                AtVetoMeeting(state, false);
                state.phase = EpisodePhase.Campaign;
                state.vetoResolved = true;
            });
            yield return AtBothTextSizes(larger =>
            {
                AssertOnTheStrategyStage("Close campaigning and open voting", "The campaign" + (larger ? " at the larger text" : ""));
                Assert.That(ActiveRect(EpisodeHud.CampaignVotersName), Is.Not.Null, "The campaign keeps its votes as cards.");
            });
            if (Application.isBatchMode)
            {
                yield return OpenStation();
                yield return CaptureFraming("strategy-campaign");
                director.ClosePanels();
                yield return null;
            }
        }

        /// <summary>
        /// The stage's shared pieces, drawn onto the veto's draw as the four screens will draw them:
        /// status cards and a tracker in the fixed header, outside the scroll and above it, where
        /// only a step that opens something is a control; a secondary slot and an Up next strip on
        /// the footer's row beside the way on, a warning outranking the Up next line; and faces
        /// fitted to the height a step leaves them, under the names a card always carries, and ten
        /// of them at the larger text in the one row their width was solved for. Each part is built
        /// and read in one frame, so no render can replace it under the test.
        /// </summary>
        [UnityTest]
        public IEnumerator StrategyStage_ItsHeaderAndFooterStandOutsideTheScroll()
        {
            string hoh = null;
            yield return InstallStrategySeason(45, state =>
            {
                var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
                state.phase = EpisodePhase.VetoSelection;
                hoh = state.hohId = npcs[0];
                state.nominees = new List<string> { npcs[1], npcs[2] };
            });
            yield return OpenStation();
            yield return null;
            var hud = director.GetComponentInChildren<EpisodeHud>();
            var panel = ActiveRect("Episode panel");
            var state = director.Snapshot;

            // The header: a row of four cards and a tracker, one step of which opens something.
            var cards = hud.StrategyHeaderRow("Status row", hud.StatusCardRowHeight(true));
            Assert.That(cards, Is.Not.Null, "The strategy stage has a header.");
            string[] labels = { "HOH", "Phase", "Conversations left", "Objective" };
            for (int i = 0; i < labels.Length; i++)
                hud.StatusCard(cards, i, labels.Length, labels[i], "Probe value " + i, "Probe line " + i,
                    PackArt.Pack8StatusHoh, PackArt.Pack8IconHoh, "crown", UiTheme.Gold);
            var steps = hud.StrategyHeaderRow("Tracker row", hud.StepTrackerHeight);
            bool opened = false;
            hud.StepTracker(steps, new[]
            {
                new EpisodeHud.TrackerStep("Probe done", "Complete", EpisodeHud.TrackerState.Done),
                new EpisodeHud.TrackerStep("Probe waiting", "Waiting", EpisodeHud.TrackerState.Current, () => opened = true),
                new EpisodeHud.TrackerStep("Probe next", "Next", EpisodeHud.TrackerState.Next),
            }, UiTheme.Gold);
            Canvas.ForceUpdateCanvases();

            var header = panel.Find(EpisodeHud.StrategyHeaderName) as RectTransform;
            Assert.That(header, Is.Not.Null, "The header is the panel's own child,");
            Assert.That(header.IsChildOf(ActiveRect("Episode content")), Is.False, "never a row of the scroll,");
            var viewport = (RectTransform)ActiveRect("Episode content").parent;
            Assert.That(ScreenRect(viewport).yMax, Is.LessThanOrEqualTo(ScreenRect(header).yMin + .5f), "and the scroll starts under it.");
            Assert.That(header.GetComponentsInChildren<Selectable>().Select(control => control.name), Is.EqualTo(new[] { "Probe waiting" }),
                "Only a step that opens something is a control in the header.");
            // Found by its caption among every live control; pressed without a frame between, since
            // a graphic built this frame has no depth for a raycast to find until the canvas draws.
            FindButton("Probe waiting").onClick.Invoke();
            Assert.That(opened, Is.True, "and pressing it opens what it names.");
            Assert.That(cards.Cast<Transform>().Count(child => child.name.StartsWith("Status card · ")), Is.EqualTo(4), "Four cards across the row.");
            foreach (var label in header.GetComponentsInChildren<TMP_Text>())
                Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.fontSizeMax * 1.21f - .5f),
                    "'" + label.text + "' has a box Inter draws in.");
            AssertEveryLabelDraws(header, "The strategy header");

            // The footer: the way on, a secondary slot on its left, and the strip in what is left.
            var onward = FindButton("Continue episode");
            hud.PinnedSecondary("Probe way back", () => { });
            hud.PinnedNote("Up next: the probe's next step.", null, false);
            Canvas.ForceUpdateCanvases();
            var strip = (RectTransform)panel.Find(EpisodeHud.StrategyStripName);
            var back = ScreenRect((RectTransform)FindButton("Probe way back").transform);
            var way = ScreenRect((RectTransform)onward.transform);
            var line = ScreenRect(strip);
            Assert.That(line.xMax, Is.LessThanOrEqualTo(back.xMin + .5f), "The strip, then the secondary slot,");
            Assert.That(back.xMax, Is.LessThanOrEqualTo(way.xMin + .5f), "then the way on,");
            Assert.That(Mathf.Abs(line.yMin - way.yMin) < 1f && Mathf.Abs(back.yMin - way.yMin) < 1f, Is.True, "on one row,");
            foreach (var part in new[] { line, back, way }) Assert.That(ScreenRect(panel).Contains(part.center), Is.True, "inside the panel,");
            Assert.That(ScreenRect(viewport).yMin, Is.GreaterThanOrEqualTo(Mathf.Max(line.yMax, back.yMax, way.yMax) - .5f), "and the scroll clear of it.");
            var words = strip.GetComponentsInChildren<TMP_Text>().Single();
            Assert.That(words.name, Is.EqualTo(EpisodeHud.UpNextName));
            Assert.That(words.color, Is.EqualTo(UiTheme.Accent), "Up next is in the accent,");
            hud.PinnedNote("Moving on lets the probe pass.", EpisodeDirector.AdvanceWarningName, true);
            hud.PinnedNote("Up next: something else.", null, false);
            Assert.That(words.name, Is.EqualTo(EpisodeDirector.AdvanceWarningName), "a warning takes the strip under its own name,");
            Assert.That(words.color, Is.EqualTo(UiTheme.Warning), "in the warning colour,");
            Assert.That(words.text, Is.EqualTo("Moving on lets the probe pass."), "and an Up next line never covers it.");

            // Faces fitted to a height: every one of them, under the names a card always carries.
            var house = new List<EpisodeHud.CeremonyFace> { new EpisodeHud.CeremonyFace(hoh, "HOH", UiTheme.Gold) };
            house.AddRange(state.Active.Where(actor => actor.id != hoh).Select(actor => new EpisodeHud.CeremonyFace(actor.id, null, UiTheme.Muted)));
            hud.CeremonyFaces("Probe faces", house, 150f, 140f);
            Canvas.ForceUpdateCanvases();
            var grid = ActiveRect("Episode content").Cast<Transform>().Last(child => child.name == EpisodeHud.CeremonyFacesName);
            Assert.That(grid.GetComponent<LayoutElement>().preferredHeight, Is.LessThanOrEqualTo(140f + .5f), "The faces fit the height asked.");
            foreach (var actor in state.Active)
            {
                var card = grid.Find("Face · " + actor.name);
                Assert.That(card, Is.Not.Null, actor.name + " keeps their card's name.");
                Assert.That(card.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "Photo"), Is.True, actor.name + " keeps their photo.");
            }
            var crown = grid.Find("Face · " + state.Find(hoh).name).GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Role");
            Assert.That(crown.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("HOH"), "and the Head of Household their pill.");

            // Ten faces at the larger text in a step one row of them fills on either canvas, where
            // the column's width sets the card: dividing the column by that width again floored one
            // short on the 16:9 frame, and two rows ran past the height (FaceGridTests has the sweep).
            director.ClosePanels();
            yield return ApplyTextSize(true);
            yield return OpenStation();
            yield return null;
            var ten = Enumerable.Range(0, 10).Select(i => house[i % house.Count]).ToList();
            hud.CeremonyFaces("Probe ten", ten, 150f, 250f);
            Canvas.ForceUpdateCanvases();
            var row = ActiveRect("Episode content").Cast<Transform>().Last(child => child.name == EpisodeHud.CeremonyFacesName);
            Assert.That(row.GetComponent<LayoutElement>().preferredHeight, Is.LessThanOrEqualTo(250f + .5f),
                "Ten faces at the larger text fit the height asked, in " + row.GetComponent<GridLayoutGroup>().constraintCount + " to a row.");
            Assert.That(row.GetComponent<GridLayoutGroup>().constraintCount, Is.EqualTo(10), "one row of them, the row their width was solved for.");
            director.ClosePanels();
            yield return ApplyTextSize(false);
        }

        /// <summary>
        /// The status line under the rail with a line as long as a story's outcome with "Saved
        /// locally." after it, at both text sizes: it grows up towards the rail, stays clear of the
        /// rail and of the stage, and says every word. It was a box 148 units wide and two short
        /// lines deep, and the end of what a commit did was cut off.
        /// </summary>
        [UnityTest]
        public IEnumerator StrategyStage_TheStatusLineUnderTheRailSaysALongLineWhole()
        {
            const string line = "The episode changed. Review a new diary choice before confirming.  ·  Saved locally.";
            yield return InstallStrategySeason(47, state => AtVetoMeeting(state, false));
            foreach (bool larger in new[] { false, true })
            {
                if (larger)
                {
                    director.ClosePanels();
                    yield return ApplyTextSize(true);
                }
                // The director's line, as a commit leaves it, and then the panel rendered over it; a
                // frame after, so the stage is drawn as a player sees it.
                NpcWrite("message", line);
                yield return OpenStation();
                yield return null;
                Assert.That(director.StatusMessage, Is.EqualTo(line), "Nothing has said anything else since.");
                AssertOnTheStrategyStage("Continue episode", "The veto meeting under a long status line" + (larger ? " at the larger text" : ""));
                var status = ActiveRect("Status");
                Assert.That(status.GetComponentsInChildren<TMP_Text>().Single().text, Is.EqualTo(line), "The line is the one the director holds,");
                Assert.That(status.rect.height, Is.GreaterThan(50f * (larger ? 1.2f : 1f) + .5f), "and it grew to hold it.");
            }
            director.ClosePanels();
            yield return ApplyTextSize(false);
        }
    }
}
