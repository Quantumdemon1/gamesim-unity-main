using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// One card clock and one controls chip on every ceremony card (UI-UX plan D3a and D3b). The
    /// clock's rules - the frames that are not the card's, the read delay, Speed up, the device it
    /// names, the fade - and the chip's numbers are pinned here without Unity, and the cards' sources
    /// are held to owning the clock and building the chip. What a built card shows is checked in
    /// PlayMode (CeremonyControlsChipPlayModeTests).
    ///
    /// <para>Unity-free, so the subset runs it too; the editor finds the scripts through
    /// <c>Application.dataPath</c>, the subset by walking up from its own binary.</para>
    /// </summary>
    public sealed class CeremonyCardClockTests
    {
        private const float Frame = 1f / 60f;

        [Test]
        public void NeitherThePlayedFrameNorTheNext_IsTheCardsTime()
        {
            var clock = new CeremonyCardClock();
            clock.Start(100, 1f, false);
            Assert.That(clock.Counts(100), Is.False, "The frame the card was played in is not its time,");
            Assert.That(clock.Counts(101), Is.False, "nor the next, whose delta is the frame it was played in;");
            Assert.That(clock.Counts(102), Is.True, "the one after is.");
            Assert.That(clock.Elapsed, Is.EqualTo(0f));
            Assert.That(clock.UpFor, Is.EqualTo(0f));
        }

        [Test]
        public void APressCountsOnlyOnceTheCardHasBeenRead_AndSpeedUpToggles()
        {
            var clock = new CeremonyCardClock();
            clock.Start(0, 1f, false);
            float readAt = CeremonyPacing.FadeIn + CeremonyCardClock.ReadDelay;
            Assert.That(readAt, Is.EqualTo(0.65f).Within(1e-6f), "The read delay every card had: its fade and 0.35 seconds.");

            var early = clock.Step(0.6f, null, true, true);
            Assert.That(early.Skip || early.SpeedChanged, Is.False, "A press in flight as the card appears is not one.");
            Assert.That(clock.Speed, Is.EqualTo(1f));

            var faster = clock.Step(0.1f, null, true, false);
            Assert.That(faster.SpeedChanged, Is.True, "Once read, Speed up counts,");
            Assert.That(clock.Speed, Is.EqualTo(CeremonyPacing.SpeedUp), "and runs the card three times as fast;");
            Assert.That(clock.Step(Frame, null, true, false).SpeedChanged && clock.Speed == 1f, Is.True, "a second press puts it back at its own pace.");
            Assert.That(clock.Step(Frame, null, false, true).Skip, Is.True, "A readable Skip is the card's next step.");

            var meeting = new CeremonyCardClock();
            meeting.Start(0, 1f, false);
            Assert.That(meeting.Step(0.3f + 0.3f, null, false, true, 0.3f).Skip, Is.False, "The fade given is the one read past,");
            Assert.That(meeting.Step(0.1f, null, false, true, 0.3f).Skip, Is.True, "and once past it and the read, a press counts.");
        }

        [Test]
        public void TheCardsTimeRunsAtItsSpeed_FromTheSpeedItWasHanded()
        {
            var clock = new CeremonyCardClock();
            Assert.That(clock.Speed, Is.EqualTo(1f), "A clock never started runs at the card's own pace.");
            clock.Start(0, CeremonyPacing.SessionSpeed(CeremonyPacing.SpeedUp), false);
            Assert.That(clock.SpedUp, Is.True, "A card handed the session's speed-up starts fast,");
            Assert.That(clock.Run(0.5f), Is.EqualTo(1.5f).Within(1e-6f), "and its own time runs three times the real time;");
            clock.Elapsed = 4f;
            clock.SetSpeed(1f);
            Assert.That(clock.Run(0.5f), Is.EqualTo(4.5f).Within(1e-6f), "a skip's jump holds, and its own pace is the real time.");
            clock.Start(9, 0f, true);
            Assert.That(clock.Elapsed + clock.UpFor, Is.EqualTo(0f), "A new play starts the clock again,");
            Assert.That(clock.Speed, Is.EqualTo(1f), "at the card's own pace when handed none.");
        }

        [Test]
        public void TheCardFollowsTheDeviceLastPressed()
        {
            var clock = new CeremonyCardClock();
            clock.Start(0, 1f, false);
            Assert.That(clock.Step(Frame, null, false, false).DeviceChanged, Is.False, "No press, no change;");
            Assert.That(clock.Step(Frame, false, false, false).DeviceChanged, Is.False, "a key on a keyboard card, none;");
            Assert.That(clock.Step(Frame, true, false, false).DeviceChanged && clock.OnPad, Is.True, "a pad press turns the card's words to the pad's,");
            Assert.That(clock.Step(Frame, false, false, false).DeviceChanged && !clock.OnPad, Is.True, "and a key turns them back,");
            Assert.That(clock.Step(Frame, true, false, false).DeviceChanged, Is.True, "before the card has been read too: the words are not a press.");
            clock.Start(5, 1f, true);
            Assert.That(clock.OnPad, Is.True, "A card opens in the device it was handed.");
        }

        [Test]
        public void TheFade_IsTheOneEveryCardHad()
        {
            const float end = 5f;
            float fadeIn = CeremonyPacing.FadeIn, fadeOut = CeremonyPacing.FadeOut;
            foreach (float t in new[] { 0f, 0.1f, 0.29f, 0.3f, 2f, 4.99f, 5f, 5.2f, 5.44f, 5.45f, 6f })
            {
                // The fade the key ceremony, the vote reveal, the jury's vote, the veto draw and the
                // meeting each wrote out for themselves.
                bool wasUp;
                float was;
                if (t < fadeIn) { was = Old(t / fadeIn); wasUp = true; }
                else if (t < end) { was = 1f; wasUp = true; }
                else
                {
                    float exit = (t - end) / fadeOut;
                    wasUp = exit < 1f;
                    was = wasUp ? 1f - Old(exit) : 0f;
                }
                bool up = CeremonyCardClock.Fade(t, end, false, out float alpha);
                Assert.That(up, Is.EqualTo(wasUp), "Up at " + t + " s as before,");
                Assert.That(alpha, Is.EqualTo(was).Within(1e-6f), "at the alpha it had at " + t + " s.");

                bool reducedUp = CeremonyCardClock.Fade(t, end, true, out float reducedAlpha);
                Assert.That(reducedUp, Is.EqualTo(t < end + fadeOut), "Reduced motion keeps the time on screen, " + t + " s,");
                Assert.That(reducedAlpha, Is.EqualTo(1f), "with no fade.");
            }
            Assert.That(CeremonyCardClock.Eased(0.5f), Is.EqualTo(0.75f).Within(1e-6f), "The ease out: quick to arrive, settling.");
        }

        [Test]
        public void TheChipReadsAt13Points_InABoxThatDraws()
        {
            Assert.That(CeremonyChipMetrics.HudPt, Is.GreaterThanOrEqualTo(13f), "The chip's words are never under 13 points on the HUD (the key ceremony's were 11).");
            foreach (float scale in new[] { 0.5f, 1f, 1.15f, 1.25f, 1.4f, 2f })
            {
                float pt = CeremonyChipMetrics.HudSize(scale);
                Assert.That(CeremonyChipMetrics.Line(pt), Is.GreaterThanOrEqualTo(1.3f * pt), "A box 1.3 times its type at text size " + scale + ",");
                Assert.That(CeremonyChipMetrics.HudHeight(scale), Is.GreaterThan(CeremonyChipMetrics.Line(pt)), "inside a padded chip.");
            }
            Assert.That(CeremonyChipMetrics.HudHeight(1f), Is.EqualTo(26f), "26 tall at the standard size,");
            Assert.That(CeremonyChipMetrics.Line(CeremonyChipMetrics.ScreenPt), Is.GreaterThanOrEqualTo(1.3f * CeremonyChipMetrics.ScreenPt),
                "and on a set's screen,");
            Assert.That(CeremonyChipMetrics.ScreenHeight, Is.EqualTo(36f), "where it fills the screen frames' 36-unit row and no more.");

            var problems = new List<string>();
            foreach (var card in new[] { "CeremonyTakeover.cs", "KeyCeremony.cs", "VoteReveal.cs", "JuryReveal.cs", "VetoDrawReveal.cs" })
                if (Count(Read(card), "CeremonyControlsChip.Build(") != 1) problems.Add(card + " does not build the one controls chip");
            if (Read("CeremonySting.cs").Contains("CeremonyControlsChip")) problems.Add("the sting takes no press and names none");
            Assert.That(problems, Is.Empty, "Every card a press moves on closes with the same chip.");
        }

        [Test]
        public void EveryCeremonyCard_RunsOnTheOneClock()
        {
            var problems = new List<string>();
            foreach (var card in new[] { "CeremonyTakeover.cs", "CeremonySting.cs", "KeyCeremony.cs", "VoteReveal.cs", "JuryReveal.cs", "VetoDrawReveal.cs" })
            {
                string source = Read(card);
                if (Count(source, "private CeremonyCardClock clock;") != 1) problems.Add(card + " owns no card clock");
                if (!source.Contains("clock.Counts(Time.frameCount)")) problems.Add(card + " counts its first frames itself");
                foreach (var own in new[] { "ReadDelay = ", "private int playedFrame", "private static float Eased(", "1f - (1f - t) * (1f - t)", "private float upFor", "usingPad" })
                    if (source.Contains(own)) problems.Add(card + " keeps its own '" + own.Trim() + "'");
            }
            Assert.That(problems, Is.Empty, "One clock for every card: " + string.Join("; ", problems));
        }

        private static float Old(float t) => 1f - (1f - t) * (1f - t);

        private static int Count(string source, string text)
        {
            int count = 0;
            for (int at = source.IndexOf(text, StringComparison.Ordinal); at >= 0; at = source.IndexOf(text, at + 1, StringComparison.Ordinal)) count++;
            return count;
        }

        private static string Read(string card) => File.ReadAllText(Path.Combine(RuntimeRoot(), "Presentation", card));

        private static string RuntimeRoot()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Runtime"));
#else
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "Gamesim", "Runtime");
                if (Directory.Exists(candidate)) return Path.GetFullPath(candidate);
            }
            Assert.Fail("Assets/Gamesim/Runtime was not found above " + AppContext.BaseDirectory);
            return null;
#endif
        }
    }
}
