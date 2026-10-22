using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
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
    /// The frames beyond 16:9 and 4:3 (PLAN A, A12): 16:10 (1280x800, the canvas 1518x949) is held to
    /// the same clip and overlap checks as the two before it, and so is 21:9 (2560x1080). Measured
    /// first, as the lead's decision 14 asked, 21:9 collided - its canvas was 1848x779, the shortest
    /// ever drawn, and the controls card came down on the vibe card and the left column on the cast
    /// strip - so A12b lays every canvas out by height on a frame wider than 16:9 (FrameScaler): the
    /// canvas is 2133x900 there, and 21:9 is gated like the rest.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>A frame the HUD is checked on: its size, its name's suffix, and whether a finding fails the test.</summary>
        private readonly struct ReviewFrame
        {
            public readonly int Width, Height;
            public readonly string Suffix, Shape;
            public readonly bool Gated;
            public ReviewFrame(int width, int height, string suffix, string shape, bool gated)
            { Width = width; Height = height; Suffix = suffix; Shape = shape; Gated = gated; }
        }

        /// <summary>
        /// 16:9 and 4:3, as <see cref="OnBothFrames"/> has always taken them; then 16:10 and 21:9, both
        /// gated (21:9 since A12b laid the wide frame's canvases out by height). A frame that is only
        /// measured - its findings written to the log, not failed - is still a frame this list can hold.
        /// </summary>
        private static readonly ReviewFrame[] ReviewFrames =
        {
            new ReviewFrame(1600, 900, "", "16:9", true),
            new ReviewFrame(1200, 900, "-4x3", "4:3", true),
            new ReviewFrame(1280, 800, "-16x10", "16:10", true),
            new ReviewFrame(2560, 1080, "-21x9", "21:9", true),
        };

        /// <summary>What the measured frames found, one line each: written to the log, not failed.</summary>
        private readonly List<string> measuredFindings = new List<string>();

        /// <summary>
        /// <see cref="OnBothFrames"/> on every review frame: in a batch run, photographs the screen as
        /// '<paramref name="name"/>' with each frame's suffix and checks it while laid out there. A
        /// gated frame's failure fails the test; a measured frame's is written down (decision 14). In
        /// the editor the game view is whatever shape it is: the check runs on it and nothing is written.
        /// </summary>
        private IEnumerator OnFrames(string name, string what, Action<string> check, Action arrange = null)
        {
            if (!Application.isBatchMode)
            {
                arrange?.Invoke();
                Canvas.ForceUpdateCanvases();
                yield return null;
                check(what);
                yield break;
            }
            foreach (var frame in ReviewFrames)
            {
                var shape = frame;
                string where = what + " on the " + shape.Shape + " frame";
                yield return CaptureFraming(name + shape.Suffix, inspect: _ =>
                {
                    if (shape.Gated) { check(where); return; }
                    try { check(where); measuredFindings.Add(where + ": clear."); }
                    catch (Exception finding) when (finding is AssertionException || finding is InvalidOperationException)
                    { measuredFindings.Add(where + ": " + finding.Message.Trim()); }
                }, width: shape.Width, height: shape.Height, arrange: arrange);
            }
        }

        /// <summary>Every live label whose copy runs out of its box, in words.</summary>
        private string[] ClippedCopy() => director.GetComponentsInChildren<TMP_Text>(true)
            .Where(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text))
            .Where(label => { label.ForceMeshUpdate(); return label.isTextOverflowing; })
            .Select(label => "'" + Excerpt(label.text) + "' in " + HierarchyPath(label.transform))
            .ToArray();

        /// <summary>
        /// The house's fixed chrome and the notebook's copy at 16:10 and 21:9, gated as at 16:9 and
        /// 4:3: at both text sizes, with nothing open, with the controls box open (the chrome alone,
        /// as the 16:9 chrome test holds it) and with the notebook open. A frame only measured would
        /// have its findings printed; none is, now that 21:9 is laid out by height (A12b).
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator Accessibility_TheHudHoldsAtSixteenTenAndTwentyOneNine()
        {
            if (!Application.isBatchMode) yield break;
            measuredFindings.Clear();
            // Nothing in the house moves: a conversation witnessed on the way through three sizes and
            // four frames swapped the vibe card and the status line for the Nearby card and its bar.
            HoldTheHouseForTheFixture();
            yield return SettleCast();
            foreach (var textSize in TextSizes.All)
            {
                string size = SizeSuffix(textSize);
                string text = TextSizes.Name(textSize);
                yield return ApplyTextSize(textSize);
                Assert.That(director.TextSize, Is.EqualTo(textSize));
                director.ClosePanels();
                yield return null;
                yield return OnFrames("frames-house" + size, "The house's chrome at " + text + " text",
                    where =>
                    {
                        AssertFixedChromeDoesNotOverlap(textSize);
                        var clipped = ClippedCopy();
                        Assert.That(clipped, Is.Empty, where + ": copy is clipped: " + string.Join(" | ", clipped));
                    });
                // And with the controls box open, where the floor is tightest, as the 16:9 chrome
                // test checks it (Accessibility_FixedChromeNeverOverlapsAtEitherTextSize).
                ButtonWithCaption(ExpandControlsCaption).onClick.Invoke();
                yield return null;
                yield return OnFrames("frames-help" + size, "The house's chrome with the controls open at " + text + " text",
                    where => AssertFixedChromeDoesNotOverlap(textSize));
                ButtonWithCaption(CollapseControlsCaption).onClick.Invoke();
                yield return null;
                yield return OpenNotebook();
                yield return OnFrames("frames-notebook" + size, "The notebook at " + text + " text",
                    where =>
                    {
                        var clipped = ClippedCopy();
                        Assert.That(clipped, Is.Empty, where + ": copy is clipped: " + string.Join(" | ", clipped));
                    });
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);
            Assert.That(measuredFindings, Is.Empty, "Every review frame is gated; none is only measured: " + string.Join(" | ", measuredFindings));
        }

        /// <summary>
        /// The canvas each review frame lays the HUD out on (A12b): on 16:9 and every narrower frame
        /// exactly the canvas the half-and-half match has always given - 1600x900 at 16:9 whatever the
        /// pixels, 1385x1039 at 4:3, 1518x949 at 16:10 - and on a frame wider than 16:9 the reference's
        /// 900 tall, the width whatever the frame leaves. The 21:9 canvas was 1848x779 before.
        /// </summary>
        [UnityTest]
        public IEnumerator Accessibility_AFrameWiderThanSixteenNineKeepsTheReferenceHeight()
        {
            var hud = director.GetComponentsInChildren<Canvas>(true).First(item => item.name == "Gamesim Episode HUD");
            var frames = new (int Width, int Height, float CanvasWidth, float CanvasHeight)[]
            {
                (1600, 900, 1600f, 900f), (1920, 1080, 1600f, 900f), (1280, 720, 1600f, 900f),
                (1200, 900, 1385.64f, 1039.23f), (1280, 800, 1517.89f, 948.68f),
                (2560, 1080, 2133.33f, 900f), (3440, 1440, 2150f, 900f),
            };
            foreach (var frame in frames)
            {
                var lens = new CaptureLens(cameraRig.ViewCamera, frame.Width, frame.Height);
                try
                {
                    yield return null; yield return null;
                    Canvas.ForceUpdateCanvases();
                    var rect = ((RectTransform)hud.transform).rect;
                    string at = frame.Width + "x" + frame.Height;
                    Assert.That(rect.width, Is.EqualTo(frame.CanvasWidth).Within(.5f), "The HUD's canvas is " + frame.CanvasWidth + " wide at " + at + ".");
                    Assert.That(rect.height, Is.EqualTo(frame.CanvasHeight).Within(.5f), "The HUD's canvas is " + frame.CanvasHeight + " tall at " + at + ".");
                    Assert.That(hud.GetComponent<CanvasScaler>().matchWidthOrHeight, Is.EqualTo(.5f),
                        "The scaler's own match is never written: the wide frame's is lent for the arithmetic alone.");
                }
                finally { lens.Dispose(); }
            }
            yield return null;
        }

        /// <summary>A frame's file-name suffix for a text size: none at the standard size, as the frames have always been named.</summary>
        private static string SizeSuffix(TextSize size) => size == TextSize.Largest ? "-largest" : size == TextSize.Larger ? "-large" : "";

        /// <summary>Whether a live control carries these words: a check that says no, where <see cref="FindButton"/> throws.</summary>
        private bool ShowsControl(string caption) => director.GetComponentsInChildren<Button>(true).Any(item => item.IsActive()
            && item.GetComponentsInChildren<TMP_Text>(true).Any(text => text.text == caption));

        /// <summary>
        /// The controls pill still opens its card over a docked panel on the 16:9 frame. With nothing
        /// open, the right column gives way to the open card there - Recent Events folds while it is
        /// open (EpisodeHud.RecentSlotsShown) - and the house lays the frame out again to do it. With a
        /// panel open, every render closes the card, so laying the frame out again closed the card the
        /// press had just opened, and on a 16:9 frame the pill did nothing at all (the fix lane's
        /// review, finding 1). A waiting reflection's quiet card is the docked panel: the Standard
        /// layout, which leaves the pill up.
        /// </summary>
        [UnityTest]
        public IEnumerator Accessibility_TheControlsOpenOverADockedPanelOnTheSixteenNineFrame()
        {
            yield return InstallDiaryFixture(state => state.pendingDiary != null, "a private reflection waiting, on a quiet episode screen");
            yield return PutAwayTheCards();
            director.ClosePanels();
            yield return null;
            var lens = new CaptureLens(cameraRig.ViewCamera, 1600, 900);
            try
            {
                // A frame for the scaler to take the lens's size, then the HUD laid out for it.
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                RenderHudForTheCurrentCanvas();
                yield return null;
                Canvas.ForceUpdateCanvases();
                var hudCanvas = director.GetComponentsInChildren<Canvas>(true).First(item => item.name == "Gamesim Episode HUD");
                Assert.That(((RectTransform)hudCanvas.transform).rect.height, Is.EqualTo(EpisodeHud.ReferenceHeight).Within(1f),
                    "The HUD is laid out on the 16:9 frame's 1600x900 canvas.");

                // With nothing open the card folds the column on this frame: the re-layout a press
                // with a panel open must not reach for.
                var events = ActiveRect(EpisodeHud.RecentEventsCardName);
                Assert.That(events, Is.Not.Null, "Recent Events is up with nothing open.");
                float whole = events.rect.height;
                FindButton(ExpandControlsCaption).onClick.Invoke();
                Assert.That(ShowsControl(CollapseControlsCaption), Is.True, "With nothing open, the press opens the controls card.");
                Assert.That(ActiveRect(EpisodeHud.RecentEventsCardName).rect.height, Is.LessThan(whole),
                    "On the 16:9 frame Recent Events gives way to the open card.");
                FindButton(CollapseControlsCaption).onClick.Invoke();
                Assert.That(ActiveRect(EpisodeHud.RecentEventsCardName).rect.height, Is.EqualTo(whole).Within(.01f),
                    "and takes its rows back as it closes.");
                yield return null;

                WarpPlayer(director.StationPosition);
                Assert.That(director.TryOpenPhasePanel(), Is.True, "The episode screen opens on the waiting reflection.");
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(director.GetComponentInChildren<EpisodeHud>().CurrentActivityLayout, Is.EqualTo(EpisodeHud.ActivityLayout.Standard),
                    "A waiting reflection is a quiet card, docked.");
                var pill = FindButton(ExpandControlsCaption);
                Assert.That(pill.IsInteractable(), Is.True, "The controls pill stays up over a docked card.");
                pill.onClick.Invoke();
                Assert.That(director.IsPanelOpen, Is.True, "Opening the controls leaves the panel open.");
                Assert.That(ShowsControl(CollapseControlsCaption), Is.True, "The press opens the controls card over the docked panel.");
                FindButton(CollapseControlsCaption).onClick.Invoke();
                Assert.That(ShowsControl(ExpandControlsCaption), Is.True, "and the next press closes it.");
                director.ClosePanels();
                yield return null;
            }
            finally { lens.Dispose(); }
        }

        /// <summary>The right column's cards and the chrome round them, whichever of them this state draws.</summary>
        private static readonly string[] ColumnNeighbours =
        {
            "Brand", "Navigation", "Objective", "Status", "House pill", "Live feed", CastRail.RootName, IconRail.RootName,
            EpisodeHud.HouseVibeCardName, EpisodeHud.RecentEventsCardName, EpisodeHud.RelationshipsCardName, EpisodeHud.ObjectivesCardName,
            EpisodeHud.NearbyCardName, EpisodeHud.PullCardName,
        };

        /// <summary>
        /// The panels named in <paramref name="required"/> are up and, with every other one of
        /// <see cref="ColumnNeighbours"/> that is, none overlaps another: the fixed chrome's check
        /// (<see cref="AssertFixedChromeDoesNotOverlap"/>) for a column that is not the vibe card under
        /// Recent Events - somebody in focus, or the endgame's objectives.
        /// </summary>
        private void AssertColumnClear(string where, params string[] required)
        {
            Canvas.ForceUpdateCanvases();
            var panels = new List<RectTransform>();
            foreach (var name in required.Concat(ColumnNeighbours).Distinct())
            {
                var rect = director.GetComponentsInChildren<RectTransform>(true).LastOrDefault(item => item.name == name && item.gameObject.activeInHierarchy);
                if (rect == null) { Assert.That(required, Has.No.Member(name), where + ": '" + name + "' is up."); continue; }
                panels.Add(rect);
            }
            for (int a = 0; a < panels.Count; a++)
            for (int b = a + 1; b < panels.Count; b++)
            {
                var first = ScreenRect(panels[a]);
                var second = ScreenRect(panels[b]);
                Assert.That(first.Overlaps(second), Is.False,
                    where + ": '" + panels[a].name + "' " + first + " overlaps '" + panels[b].name + "' " + second + ".");
            }
        }

        /// <summary>
        /// The two states the right column's fold did not reach (the fix lane's review, finding 2),
        /// with the controls card open on every review frame at every text size. Somebody in focus:
        /// the relationships card is seven rows and 400 tall, and it ended at 668 against the open
        /// card's 596 on 16:9; now it gives way a row at a time, the one in focus last. The Final 3
        /// with the tagline: the objectives are 224 tall, and at the larger text on 16:9 even Recent
        /// Events' heading alone ended 9 units into the card; now the live feed goes there too.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator Accessibility_TheColumnGivesWayToTheControlsWithSomeoneInFocusAndAtTheFinalThree()
        {
            if (!Application.isBatchMode) yield break;
            // The Final 3 first: its fixture is installed over the scene's own season, which a
            // twelve-house season started first would no longer be.
            yield return InstallTheFinalThree();
            foreach (var textSize in TextSizes.All)
            {
                string size = SizeSuffix(textSize), text = TextSizes.Name(textSize);
                bool larger = textSize != TextSize.Standard;
                yield return ApplyTextSize(textSize);
                Assert.That(director.TextSize, Is.EqualTo(textSize));
                director.ClosePanels();
                yield return null;
                Assert.That(ActiveChromePanel(EpisodeHud.ObjectivesTaglineName), Is.Not.Null, "A finalist's objectives carry the tagline at three.");
                bool feedUp = ActiveChromePanel(EpisodeDirector.LiveFeedCardName) != null;
                ButtonWithCaption(ExpandControlsCaption).onClick.Invoke();
                yield return null;
                yield return OnFrames("frames-final-three-help" + size, "The Final 3 with the controls open at " + text + " text",
                    where =>
                    {
                        AssertColumnClear(where, EpisodeHud.ObjectivesCardName, EpisodeHud.RecentEventsCardName, "Exploration controls");
                        // The one state the fold alone could not clear: the feed gives way there.
                        var canvas = director.GetComponentsInChildren<Canvas>(true).First(item => item.name == "Gamesim Episode HUD");
                        if (feedUp && larger && Mathf.Abs(((RectTransform)canvas.transform).rect.height - EpisodeHud.ReferenceHeight) < 1f)
                            Assert.That(ActiveChromePanel(EpisodeDirector.LiveFeedCardName), Is.Null,
                                where + ": the live feed gives way on a 900-tall canvas above the standard text.");
                    });
                ButtonWithCaption(CollapseControlsCaption).onClick.Invoke();
                yield return null;
                Assert.That(ActiveChromePanel(EpisodeDirector.LiveFeedCardName) != null, Is.EqualTo(feedUp), "The live feed comes back as the controls close.");
                // And somebody in focus at three: the objectives under the relationships card.
                var finalist = director.Snapshot.Active.First(actor => !actor.isPlayer);
                director.FollowHouseguest(finalist.id);
                director.ClosePanels();
                yield return null;
                Assert.That(ActiveChromePanel(EpisodeHud.RelationshipsCardName), Is.Not.Null, "A finalist in focus at three.");
                ButtonWithCaption(ExpandControlsCaption).onClick.Invoke();
                yield return null;
                yield return OnFrames("frames-final-three-focus-help" + size, "Somebody in focus at the Final 3 with the controls open at " + text + " text",
                    where => AssertColumnClear(where, EpisodeHud.RelationshipsCardName, EpisodeHud.ObjectivesCardName, "Exploration controls"));
                ButtonWithCaption(CollapseControlsCaption).onClick.Invoke();
                director.FollowHouseguest(finalist.id);
                yield return null;
            }

            // Twelve in the house, so the card holds the seven rows it holds on a 900-tall canvas -
            // the fixture's six-house season fills five, which cleared the open card on 16:9 anyway -
            // and nothing in the house moves meanwhile, so no witnessed talk swaps the vibe card out.
            director.StartSeason(new SeasonBuilder.Choice { HouseSize = 12 });
            yield return null; yield return null;
            HoldTheHouseForTheFixture();
            yield return PutAwayTheCards();
            yield return SettleCast();
            Assert.That(director.Snapshot.contestants, Has.Count.EqualTo(12), "A twelve-house season.");
            var npc = SceneComponents<HouseNpc>().First(actor => actor.gameObject.activeInHierarchy && director.Snapshot.Find(actor.Id) != null);
            foreach (var textSize in TextSizes.All)
            {
                string size = SizeSuffix(textSize), text = TextSizes.Name(textSize);
                yield return ApplyTextSize(textSize);
                Assert.That(director.TextSize, Is.EqualTo(textSize));
                // Following draws nothing by itself: the render that closing the panels makes does.
                if (director.FollowedId != npc.Id) director.FollowHouseguest(npc.Id);
                director.ClosePanels();
                yield return null;
                Assert.That(director.FollowedId, Is.EqualTo(npc.Id), "Somebody is in focus.");
                Assert.That(ActiveChromePanel(EpisodeHud.RelationshipsCardName), Is.Not.Null, "The column is the relationships card while somebody is in focus.");
                ButtonWithCaption(ExpandControlsCaption).onClick.Invoke();
                yield return null;
                yield return OnFrames("frames-focus-help" + size, "Somebody in focus with the controls open at " + text + " text",
                    where => AssertColumnClear(where, EpisodeHud.RelationshipsCardName, EpisodeHud.HouseVibeCardName, "Exploration controls"));
                // And the one in focus is the row the card keeps, whatever it gives way.
                var card = director.GetComponentsInChildren<RectTransform>(true).Last(item => item.name == EpisodeHud.RelationshipsCardName && item.gameObject.activeInHierarchy);
                Assert.That(card.GetComponentsInChildren<RectTransform>(true).Any(item => item.name == "Focus row"), Is.True, "The one in focus keeps their row.");
                ButtonWithCaption(CollapseControlsCaption).onClick.Invoke();
                director.FollowHouseguest(npc.Id);
                yield return null;
            }
            yield return ApplyTextSize(TextSize.Standard);
        }
    }
}
