using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Caption reading time (PLAN A, A10): the settings' "Caption time" reaches the ceremony's strip
    /// and the witnessed conversation's line, and that line, once shown, stays up for its reading
    /// time when the pair walks out of sight - while a panel, a load or the world pausing still takes
    /// it down at once. The reveals the player paces are not on this clock.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Captions_TheCaptionTimeSettingReachesTheStripAndIsAControlInTheSettings()
        {
            var sting = SceneComponents<CeremonySting>().Single();
            Assert.That(director.CaptionTimeSetting, Is.EqualTo(CaptionTime.Standard), "A fresh session reads at the standard time.");
            Assert.That(sting.CaptionTime, Is.EqualTo(CaptionTime.Standard));
            director.OpenSettings();
            yield return null; yield return null;
            ButtonWithCaption(CaptionTiming.StandardCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.CaptionTimeSetting, Is.EqualTo(CaptionTime.Longer), "The control moves to the longer time,");
            Assert.That(sting.CaptionTime, Is.EqualTo(CaptionTime.Longer), "and the strip reads at it.");
            Assert.That(ShowsControl(CaptionTiming.LongerCaption), Is.True, "The control names the time in use.");
            Assert.That(ShowsControl(CaptionTiming.StandardCaption), Is.False);
            ButtonWithCaption(CaptionTiming.LongerCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.CaptionTimeSetting, Is.EqualTo(CaptionTime.Standard), "and back.");
            Assert.That(sting.CaptionTime, Is.EqualTo(CaptionTime.Standard));
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The witnessed line holds for its reading time once shown: let go while it is still being
        /// read, it stays; past its time it goes; a hide takes it at once. Under "Longer" it holds half
        /// as long again. A line of its own, so the house's world tick cannot show or hide it meanwhile.
        /// </summary>
        [UnityTest]
        public IEnumerator Captions_AWitnessedLineStaysUpForItsReadingTimeOnceShown()
        {
            var holder = new GameObject("Caption time probe");
            try
            {
                var caption = holder.AddComponent<HouseConversationCaption>();
                caption.Show("Maya Hassan", "Riley Johnson", "strategy", 1f);
                // "Maya Hassan and Riley Johnson are discussing the game.": nine words, 3.9 s.
                Assert.That(caption.HoldSeconds, Is.EqualTo(1.2f + 0.3f * 9).Within(1e-4f));
                caption.Release();
                yield return null;
                Assert.That(caption.CurrentText, Is.Not.Empty, "Let go at once, the line stays to be read.");
                Assert.That(caption.IsReleasing, Is.True);
                yield return new WaitForSecondsRealtime(caption.HoldSeconds - 1f);
                Assert.That(caption.CurrentText, Is.Not.Empty, "Still up a second before its time is out.");
                yield return new WaitForSecondsRealtime(1.3f);
                Assert.That(caption.CurrentText, Is.Empty, "and gone once it has been read.");

                // Shown again, a release after its time is up takes it at once.
                caption.Show("Maya Hassan", "Riley Johnson", "gossip", 1f);
                yield return new WaitForSecondsRealtime(caption.HoldSeconds + .2f);
                Assert.That(caption.CurrentText, Is.Not.Empty, "Still witnessed, it stays as long as the pair is in sight.");
                caption.Release();
                Assert.That(caption.CurrentText, Is.Empty, "Read already: let go, it goes.");

                // A hide is at once, read or not: a panel, a load or a paused house.
                caption.Show("Maya Hassan", "Riley Johnson", "strategy", 1f);
                caption.Release();
                caption.Hide();
                Assert.That(caption.CurrentText, Is.Empty, "A hide never waits.");
                Assert.That(caption.IsReleasing, Is.False);

                // Half as long again under "Longer", from the next line shown.
                caption.CaptionTime = CaptionTime.Longer;
                caption.Show("Maya Hassan", "Riley Johnson", "strategy", 1f);
                Assert.That(caption.HoldSeconds, Is.EqualTo((1.2f + 0.3f * 9) * 1.5f).Within(1e-4f));
                caption.Hide();
            }
            finally { Object.Destroy(holder); }
            yield return null;
        }
    }
}
