using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The frames beyond 16:9 and 4:3 (PLAN A, A12): 16:10 (1280x800, the canvas 1518x949) is held to
    /// the same clip and overlap checks as the two before it; 21:9 (2560x1080, the canvas 1848x779,
    /// the shortest ever drawn) is photographed and measured, and what it finds is written to the log
    /// rather than failed - the lead's decision 14: measure first, and only if chrome collides take
    /// the separate slice (A12b) that switches the 1600x900 canvases to match height when wider than
    /// 16:9.
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

        /// <summary>16:9 and 4:3, as <see cref="OnBothFrames"/> has always taken them; then 16:10, gated; then 21:9, measured.</summary>
        private static readonly ReviewFrame[] ReviewFrames =
        {
            new ReviewFrame(1600, 900, "", "16:9", true),
            new ReviewFrame(1200, 900, "-4x3", "4:3", true),
            new ReviewFrame(1280, 800, "-16x10", "16:10", true),
            new ReviewFrame(2560, 1080, "-21x9", "21:9", false),
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
        /// The house's fixed chrome and the notebook's copy at 16:10, gated as at 16:9 and 4:3, and at
        /// 21:9, photographed and measured: at both text sizes, with nothing open, with the controls box
        /// open (the chrome alone, as the 16:9 chrome test holds it) and with the notebook open. The
        /// measurement is printed whatever it says, so the 21:9 verdict is in the run's log.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator Accessibility_TheHudHoldsAtSixteenTenAndIsMeasuredAtTwentyOneNine()
        {
            if (!Application.isBatchMode) yield break;
            measuredFindings.Clear();
            yield return SettleCast();
            foreach (bool larger in new[] { false, true })
            {
                string size = larger ? "-large" : "";
                string text = larger ? "larger" : "standard";
                yield return ApplyTextSize(larger);
                director.ClosePanels();
                yield return null;
                yield return OnFrames("frames-house" + size, "The house's chrome at " + text + " text",
                    where =>
                    {
                        AssertFixedChromeDoesNotOverlap(larger);
                        var clipped = ClippedCopy();
                        Assert.That(clipped, Is.Empty, where + ": copy is clipped: " + string.Join(" | ", clipped));
                    });
                // And with the controls box open, where the floor is tightest, as the 16:9 chrome
                // test checks it (Accessibility_FixedChromeNeverOverlapsAtEitherTextSize).
                ButtonWithCaption(ExpandControlsCaption).onClick.Invoke();
                yield return null;
                yield return OnFrames("frames-help" + size, "The house's chrome with the controls open at " + text + " text",
                    where => AssertFixedChromeDoesNotOverlap(larger));
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
            string report = "21:9 measurement (decision 14):\n  " + string.Join("\n  ", measuredFindings);
            TestContext.WriteLine(report);
            Debug.Log("[Gamesim] " + report);
            Assert.That(measuredFindings, Has.Count.EqualTo(6),
                "The 21:9 frame was measured six times: two sizes, the house with the controls closed and open, and the notebook.");
        }

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
    }
}
