using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The competition result can be read (UI-UX plan D12), on the built screens: Enter and the pad's
    /// A skip a practice's 3-2-1 - the Submit the focused Pause also hears pauses nothing - and never
    /// a ranked board's; the standings card draws the attempt's headline over its measure under the
    /// winner, above "HOW THEY PLACED", every label in a box of at least 1.3 times its type.
    /// </summary>
    public sealed partial class CompetitionSurfacePlayModeTests
    {
        [UnityTest]
        public IEnumerator EnterOrA_SkipsAPracticesCount_AndNeverARankedOnes()
        {
            CreateInput();
            foreach (var press in new[] { "Enter", "A" })
            {
                var run = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 77, 3);
                screen.Show(run, "Competition", "Player", true, i => {}, () => {}, d => {}, () => {}, () => screen.Hide());
                yield return null;
                Assert.That(screen.AdvanceReady(.01f), Is.False);
                Assert.That(screen.IsPlaying, Is.False, press + ": a practice opens on its count.");
                if (press == "Enter") yield return KeyPress(Key.Enter); else yield return PadPress(GamepadButton.South);
                Assert.That(screen.Paused, Is.False, press + " skips the count; the focused Pause does not take it as well.");
                Assert.That(screen.AdvanceReady(.01f), Is.False, press + ": GO's frame is no play.");
                Assert.That(screen.IsPlaying, Is.True, press + " skipped the practice's count.");
                Assert.That(run.Elapsed, Is.Zero, press + ": the attempt's clock starts after GO.");
                screen.Hide();
                yield return null;
            }

            var ranked = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 77, 3);
            screen.Show(ranked, "Head of Household", "Player\nMaya", false, i => {}, () => {}, d => {}, () => {}, () => screen.Hide());
            yield return null;
            Assert.That(screen.AdvanceReady(.01f), Is.False);
            yield return KeyPress(Key.Enter);
            Assert.That(screen.SkipPracticeCount(), Is.False, "A ranked board's count is never skipped.");
            Assert.That(screen.AdvanceReady(.01f), Is.False);
            Assert.That(screen.IsPlaying, Is.False, "Enter on a ranked board's count starts nothing early.");
            Assert.That(ranked.Elapsed, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TheStandingsCard_DrawsTheAttemptsHeadlineOverItsMeasure()
        {
            CreateInput(); screen.Hide();
            foreach (float scale in TextScales.All)
            {
                var result = CompetitionResult.Attach(owner); result.FontScale = scale;
                try
                {
                    var rows = new[]
                    {
                        new CompetitionResult.Standing("Maya", 6.6, true, false, null),
                        new CompetitionResult.Standing("You", 6.4, false, true, null),
                        new CompetitionResult.Standing("Riley", 5.9, false, false, null),
                    };
                    string line = "Your attempt  ·  0 / 8 pairs  ·  performance 40%";
                    result.Play("Head of Household · Memory Wall", "Mental", 2, rows, true, null, CompetitionResult.JoinAttempt("TIME", line));
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    var texts = result.GetComponentsInChildren<TMP_Text>();
                    var headline = texts.Single(text => text.name == "Attempt headline");
                    var attempt = texts.Single(text => text.name == "Player attempt");
                    var heading = texts.Single(text => text.name == "Standings heading");
                    Assert.That(headline.text, Is.EqualTo("TIME"), "The finish plate's headline, at " + scale + ".");
                    Assert.That(attempt.text, Is.EqualTo(line), "The measure, without the headline.");
                    Assert.That(heading.text, Is.EqualTo("HOW THEY PLACED"));
                    float headlineBottom = WorldBottom(headline.rectTransform), attemptTop = WorldTop(attempt.rectTransform);
                    Assert.That(headlineBottom, Is.GreaterThanOrEqualTo(attemptTop - .5f), "The headline sits over the measure at " + scale + ".");
                    Assert.That(WorldBottom(texts.Single(text => text.name == "Winner").rectTransform), Is.GreaterThanOrEqualTo(WorldTop(headline.rectTransform) - .5f),
                        "under the winner's line,");
                    Assert.That(WorldBottom(attempt.rectTransform), Is.GreaterThanOrEqualTo(WorldTop(heading.rectTransform) - .5f),
                        "and above the ranking's heading.");
                    foreach (var label in new[] { headline, attempt, heading })
                    {
                        label.ForceMeshUpdate();
                        Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.fontSize * 1.3f - .01f),
                            "'" + label.name + "' has a box of at least 1.3 times its type at " + scale + ".");
                        Assert.That(label.textInfo.characterCount, Is.GreaterThan(0), "'" + label.name + "' draws at " + scale + ".");
                    }

                    result.Play("Head of Household · Memory Wall", "Mental", 2, rows, true, null, line);
                    yield return null;
                    texts = result.GetComponentsInChildren<TMP_Text>();
                    Assert.That(texts.Any(text => text.name == "Attempt headline"), Is.False, "An attempt without a headline is one line, as it was.");
                    Assert.That(texts.Single(text => text.name == "Player attempt").text, Is.EqualTo(line));
                }
                finally { Object.Destroy(result.gameObject); }
                yield return null;
            }
        }

        /// <summary>A rect's top and bottom in world space, y up: the bottom of the one above is at or over the top of the one below.</summary>
        private static float WorldTop(RectTransform rect) { var corners = new Vector3[4]; rect.GetWorldCorners(corners); return corners[1].y; }
        private static float WorldBottom(RectTransform rect) { var corners = new Vector3[4]; rect.GetWorldCorners(corners); return corners[0].y; }
    }
}
