using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

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
    }
}
