using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The standalone profile's performance verdict and the arithmetic under it
    /// (<see cref="VerificationPerformance"/>). Unity-free, so Tools/SimulationTests runs the rule the
    /// player reports by. The verdict is "Met" or "Not met" against p95 &lt;= 16.7 ms and p99 &lt;= 33.3 ms
    /// only for a graphical, windowed, uncapped, non-batchmode 1920x1080 run of at least 300 s; every
    /// other run is "Not assessed", with its reasons - however good its numbers look.
    /// </summary>
    public sealed class VerificationPerformanceTests
    {
        /// <summary>A run that measured exactly the owner's target: five minutes at 1920x1080, windowed, uncapped.</summary>
        private static VerificationPerformance.Evidence Target(double p95 = 10, double p99 = 20) => new VerificationPerformance.Evidence
        {
            Graphical = true, BatchMode = false, MeasuredSeconds = 300, Frames = 18000, DisplaySamples = 18000, P95Ms = p95, P99Ms = p99,
        };

        private static string Verdict(VerificationPerformance.Evidence evidence) => VerificationPerformance.Verdict(evidence, out _);

        // ------------------------------------------------------------------ the verdict

        [Test]
        public void AFullTargetRunUnderBothLimitsIsMet()
        {
            Assert.That(Verdict(Target()), Is.EqualTo(VerificationPerformance.Met));
            Assert.That(VerificationPerformance.WhyNotAssessed(Target()), Is.Empty);
        }

        [Test]
        public void ExactlyAtBothLimitsIsMet()
            => Assert.That(Verdict(Target(16.7f, 33.3f)), Is.EqualTo(VerificationPerformance.Met), "The limits are inclusive, at the frame times' own precision.");

        [TestCase(16.71f, 20f)]
        [TestCase(10f, 33.31f)]
        [TestCase(40f, 60f)]
        public void OverEitherLimitIsNotMet(float p95, float p99)
            => Assert.That(Verdict(Target(p95, p99)), Is.EqualTo(VerificationPerformance.NotMet));

        [Test]
        public void AFastP95CannotHideAHitchingP99()
            => Assert.That(Verdict(Target(5, 40)), Is.EqualTo(VerificationPerformance.NotMet), "The tail is judged as well as the bulk.");

        [Test]
        public void TheLimitsAreTheOwnersTarget()
        {
            Assert.That(VerificationPerformance.P95LimitMs, Is.EqualTo(16.7f));
            Assert.That(VerificationPerformance.P99LimitMs, Is.EqualTo(33.3f));
            Assert.That(VerificationPerformance.MinimumSeconds, Is.EqualTo(300));
            Assert.That(VerificationPerformance.TargetWidth, Is.EqualTo(1920));
            Assert.That(VerificationPerformance.TargetHeight, Is.EqualTo(1080));
        }

        [TestCase("batch", "-batchmode")]
        [TestCase("nographics", "no graphics device")]
        [TestCase("development", "a development build, not the shipping player")]
        [TestCase("short", "ran 299.9 s of the 300 s required")]
        [TestCase("unmeasured", "ran NaN s")]
        [TestCase("resolution", "1 measured frames were not 1920x1080")]
        [TestCase("window", "1 measured frames were not windowed")]
        [TestCase("cap", "1 measured frames ran capped or with VSync")]
        [TestCase("evidence", "17999 of 18000 measured frames carry")]
        [TestCase("noframes", "no frames were measured")]
        [TestCase("requested900p", "requested 1600x900, not 1920x1080")]
        [TestCase("requestedVSync", "requested a frame cap or VSync")]
        [TestCase("requestedCap", "requested a frame cap or VSync")]
        [TestCase("requestedFullscreen", "did not request a window")]
        public void ARunThatDidNotMeasureTheTargetIsNotAssessedHoweverFastItWas(string condition, string reason)
        {
            var evidence = Target(1, 2);
            switch (condition)
            {
                case "batch": evidence.BatchMode = true; break;
                case "nographics": evidence.Graphical = false; break;
                case "development": evidence.DevelopmentBuild = true; break;
                case "short": evidence.MeasuredSeconds = 299.9; break;
                case "unmeasured": evidence.MeasuredSeconds = double.NaN; break;
                case "resolution": evidence.ResolutionMismatches = 1; break;
                case "window": evidence.DisplayModeMismatches = 1; break;
                case "cap": evidence.FrameCapMismatches = 1; break;
                case "evidence": evidence.DisplaySamples = 17999; break;
                case "noframes": evidence.Frames = 0; evidence.DisplaySamples = 0; break;
                case "requested900p": evidence.RequestedWidth = 1600; evidence.RequestedHeight = 900; break;
                case "requestedVSync": evidence.RequestedVSyncCount = 1; break;
                case "requestedCap": evidence.RequestedFrameCap = 60; break;
                case "requestedFullscreen": evidence.RequestedWindowed = false; break;
                default: Assert.Fail("Unknown condition " + condition); break;
            }
            string verdict = Verdict(evidence);
            Assert.That(verdict, Does.StartWith(VerificationPerformance.NotAssessed));
            Assert.That(verdict, Does.Contain(reason));
            Assert.That(verdict, Is.Not.EqualTo(VerificationPerformance.Met).And.Not.EqualTo(VerificationPerformance.NotMet));
        }

        [Test]
        public void EveryUnmetConditionIsNamedNotOnlyTheFirst()
        {
            var evidence = Target(1, 2);
            evidence.BatchMode = true; evidence.MeasuredSeconds = 11; evidence.ResolutionMismatches = 3;
            string verdict = Verdict(evidence);
            Assert.That(verdict, Does.StartWith("Not assessed: the player ran with -batchmode; "));
            Assert.That(verdict, Does.Contain("3 measured frames were not 1920x1080"));
            Assert.That(verdict, Does.Contain("ran 11.0 s of the 300 s required"));
            Assert.That(verdict, Does.EndWith("."));
            Assert.That(VerificationPerformance.WhyNotAssessed(evidence), Has.Count.EqualTo(3));
        }

        [Test]
        public void ASlowRunThatDidNotMeasureTheTargetIsNotAssessedRatherThanNotMet()
            => Assert.That(Verdict(new VerificationPerformance.Evidence { Graphical = true, MeasuredSeconds = 300, Frames = 10, DisplaySamples = 10, P95Ms = 90, P99Ms = 90, ResolutionMismatches = 10 }),
                Does.StartWith(VerificationPerformance.NotAssessed), "A 900p or fullscreen run says nothing about the target, fast or slow.");

        [Test]
        public void TheBasisNamesBothPercentilesTheirLimitsAndTheSample()
        {
            VerificationPerformance.Verdict(Target(12.346, 20), out string basis);
            Assert.That(basis, Is.EqualTo("p95 12.35 ms (limit 16.70 ms), p99 20.00 ms (limit 33.30 ms), 18000 frames over 300.0 s"));
        }

        // ------------------------------------------------------------------ the distribution

        [Test]
        public void SlowFramesAreCountedStrictlyOverEachLine()
        {
            var frames = new List<float> { 10f, 16.7f, 16.8f, 33.3f, 33.4f, 50f, 50.1f, 200f };
            var summary = VerificationFrameSummary.Of("steady", frames);
            Assert.That(summary.name, Is.EqualTo("steady"));
            Assert.That(summary.frames, Is.EqualTo(8));
            Assert.That(summary.framesOver16_7Ms, Is.EqualTo(6));
            Assert.That(summary.framesOver33_3Ms, Is.EqualTo(4));
            Assert.That(summary.framesOver50Ms, Is.EqualTo(2));
            Assert.That(summary.maxMs, Is.EqualTo(200));
            Assert.That(summary.minMs, Is.EqualTo(10));
            Assert.That(summary.frameSeconds, Is.EqualTo(frames.Sum(ms => (double)ms) / 1000d).Within(1e-9));
            Assert.That(summary.meanMs, Is.EqualTo(frames.Sum(ms => (double)ms) / 8d).Within(1e-9));
        }

        [Test]
        public void PercentilesAreNearestRankAsTheProfileHasAlwaysReportedThem()
        {
            var frames = Enumerable.Range(1, 100).Select(i => (float)i).Reverse().ToList();
            var summary = VerificationFrameSummary.Of("steady", frames);
            Assert.That(summary.medianMs, Is.EqualTo(50));
            Assert.That(summary.p90Ms, Is.EqualTo(90));
            Assert.That(summary.p95Ms, Is.EqualTo(95));
            Assert.That(summary.p99Ms, Is.EqualTo(99));
            Assert.That(summary.p999Ms, Is.EqualTo(100));
        }

        [Test]
        public void SummarisingLeavesTheRawFramesInTheOrderMeasured()
        {
            var frames = new List<float> { 30f, 10f, 20f };
            VerificationFrameSummary.Of("steady", frames);
            Assert.That(frames, Is.EqualTo(new[] { 30f, 10f, 20f }), "The raw distribution is written in measured order after the report.");
        }

        [Test]
        public void AnEmptyStretchSummarisesToZeros()
        {
            var summary = VerificationFrameSummary.Of("bootstrap", new float[0]);
            Assert.That(summary.frames, Is.Zero);
            Assert.That(new[] { summary.meanMs, summary.maxMs, summary.p95Ms, summary.p99Ms, summary.frameSeconds }, Is.All.EqualTo(0d));
            Assert.That(VerificationFrameSummary.Of("none", null).frames, Is.Zero);
        }

        [Test]
        public void TheHistogramPartitionsEveryFrameWithInclusiveTops()
        {
            var frames = new[] { 1f, 4.2f, 4.3f, 16.7f, 16.71f, 33.3f, 49f, 100f, 100.5f, 400f };
            var counts = VerificationPerformance.Histogram(frames);
            Assert.That(counts, Has.Length.EqualTo(VerificationPerformance.HistogramUpperMs.Length + 1));
            Assert.That(counts.Sum(), Is.EqualTo(frames.Length));
            int Bucket(float top) => Array.IndexOf(VerificationPerformance.HistogramUpperMs, top);
            Assert.That(counts[Bucket(4.2f)], Is.EqualTo(2), "1 and 4.2 are at most 4.2 ms.");
            Assert.That(counts[Bucket(8.3f)], Is.EqualTo(1));
            Assert.That(counts[Bucket(16.7f)], Is.EqualTo(1), "16.7 is a 60 fps frame.");
            Assert.That(counts[Bucket(20f)], Is.EqualTo(1), "16.71 is not.");
            Assert.That(counts[Bucket(33.3f)], Is.EqualTo(1));
            Assert.That(counts[Bucket(50f)], Is.EqualTo(1));
            Assert.That(counts[Bucket(100f)], Is.EqualTo(1));
            Assert.That(counts[counts.Length - 1], Is.EqualTo(2), "Everything over 100 ms is the last bucket.");
        }

        // ------------------------------------------------------------------ memory

        [Test]
        public void TheMemoryPeakIsEachFieldsHighWaterMark()
        {
            var a = new VerificationMemorySample { atSeconds = 10, totalAllocatedBytes = 500, totalReservedBytes = 900, monoUsedBytes = 40, monoHeapBytes = 64, gfxDriverBytes = 0, systemUsedBytes = 2000, gfxUsedBytes = 300 };
            var b = new VerificationMemorySample { atSeconds = 11, totalAllocatedBytes = 450, totalReservedBytes = 950, monoUsedBytes = 70, monoHeapBytes = 64, gfxDriverBytes = 0, systemUsedBytes = 1900, gfxUsedBytes = 310 };
            var peak = VerificationMemorySample.Peak(new[] { a, null, b });
            Assert.That(peak.totalAllocatedBytes, Is.EqualTo(500));
            Assert.That(peak.totalReservedBytes, Is.EqualTo(950));
            Assert.That(peak.monoUsedBytes, Is.EqualTo(70));
            Assert.That(peak.monoHeapBytes, Is.EqualTo(64));
            Assert.That(peak.gfxDriverBytes, Is.Zero, "A release player reports no driver estimate, and the peak does not invent one.");
            Assert.That(peak.systemUsedBytes, Is.EqualTo(2000));
            Assert.That(peak.gfxUsedBytes, Is.EqualTo(310));
            Assert.That(peak.atSeconds, Is.EqualTo(-1), "A field-wise peak is no single moment.");
        }

        [Test]
        public void NoMemorySamplesReadAsUnavailable()
        {
            var peak = VerificationMemorySample.Peak(new VerificationMemorySample[0]);
            Assert.That(new[] { peak.totalAllocatedBytes, peak.totalReservedBytes, peak.monoUsedBytes, peak.monoHeapBytes, peak.gfxDriverBytes, peak.systemUsedBytes, peak.gfxUsedBytes },
                Is.All.EqualTo(-1L));
        }

        // ------------------------------------------------------------------ the stress-roster switch

        private static string[] Args(params string[] extra) => new[] { "Gamesim.exe", "--gamesim-verify", "--gamesim-save-root", "D:\\qa" }.Concat(extra).ToArray();

        [TestCase(0)]
        [TestCase(12)]
        [TestCase(16)]
        public void WithoutTheFlagNothingIsRefused(int houseSize)
        {
            Assert.That(VerificationPerformance.StressRosterRefusal(Args(), houseSize), Is.Null);
            Assert.That(VerificationPerformance.StressRosterRefusal(new[] { "Gamesim.exe" }, houseSize), Is.Null, "Normal play never reads it.");
        }

        [TestCase(0)]
        [TestCase(6)]
        [TestCase(12)]
        [TestCase(17)]
        public void TheFlagWithAHouseARosterSeatsIsRefused(int houseSize)
            => Assert.That(VerificationPerformance.StressRosterRefusal(Args(VerificationPerformance.StressRosterArgument), houseSize),
                Does.Contain("--gamesim-house-size from 13 to 16"));

        [TestCase(13)]
        [TestCase(16)]
        public void TheFlagWithAHouseAboveEveryRosterIsAdmitted(int houseSize)
            => Assert.That(VerificationPerformance.StressRosterRefusal(Args(VerificationPerformance.StressRosterArgument), houseSize), Is.Null);

        [Test]
        public void TheFlagWithoutVerificationIsRefused()
            => Assert.That(VerificationPerformance.StressRosterRefusal(new[] { "Gamesim.exe", VerificationPerformance.StressRosterArgument }, 16),
                Does.Contain("requires --gamesim-verify"));

        [TestCase("--gamesim-verify-creator")]
        [TestCase("--gamesim-look-sheet")]
        public void TheFlagWithAModeThatBuildsItsOwnSeasonIsRefused(string mode)
            => Assert.That(VerificationPerformance.StressRosterRefusal(Args(VerificationPerformance.StressRosterArgument, mode), 16),
                Does.Contain("profile workload"));

        // ------------------------------------------------------------------ the size asked for, and the season it starts

        [TestCase("40", 40)]
        [TestCase("16", 16)]
        [TestCase("-2", -2)]
        public void TheHouseSizeIsReadAsWrittenNeverClamped(string value, int expected)
        {
            Assert.That(VerificationPerformance.TryReadHouseSize(Args(VerificationPerformance.HouseSizeArgument, value), out int requested), Is.True);
            Assert.That(requested, Is.EqualTo(expected));
        }

        [TestCase("sixteen")]
        [TestCase("(absent)")]
        public void AMissingOrUnreadableHouseSizeIsNoRequest(string value)
        {
            var args = value == "(absent)" ? Args() : Args(VerificationPerformance.HouseSizeArgument, value);
            Assert.That(VerificationPerformance.TryReadHouseSize(args, out int requested), Is.False);
            Assert.That(requested, Is.Zero);
            Assert.That(VerificationPerformance.TryReadHouseSize(Args(VerificationPerformance.HouseSizeArgument), out _), Is.False, "The argument with no value after it.");
        }

        [Test]
        public void AStressRequestAboveSixteenIsRefusedNotRunAtSixteen()
        {
            var args = Args(VerificationPerformance.StressRosterArgument, VerificationPerformance.HouseSizeArgument, "40");
            VerificationPerformance.TryReadHouseSize(args, out int requested);
            Assert.That(VerificationPerformance.StressRosterRefusal(args, requested), Does.Contain("from 13 to 16").And.Contain("requested 40."),
                "The launch judges the size as written; a clamp before it would admit forty as sixteen.");
        }

        [TestCase(0, false, VerificationProfileSeason.Scene)]
        [TestCase(6, false, VerificationProfileSeason.Director)]
        [TestCase(12, false, VerificationProfileSeason.Director)]
        [TestCase(16, false, VerificationProfileSeason.Director)]
        [TestCase(16, true, VerificationProfileSeason.Stress)]
        public void EverySizeAskedForIsASeasonTheDirectorStarts(int houseSize, bool stress, VerificationProfileSeason expected)
            => Assert.That(VerificationPerformance.ProfileSeason(houseSize, stress), Is.EqualTo(expected),
                "The scene's own six plays with every rule off; a six-person profile measures a started season like the twelve and the sixteen.");

        [TestCase(60000u, 1000u, 60d)]
        [TestCase(143856u, 1000u, 143.856d)]
        [TestCase(0u, 0u, -1d)]
        [TestCase(60u, 0u, -1d)]
        public void ARefreshRateWithNoDenominatorIsUnavailableNotNaN(uint numerator, uint denominator, double expected)
        {
            double hz = VerificationPerformance.RefreshHz(numerator, denominator);
            Assert.That(double.IsNaN(hz) || double.IsInfinity(hz), Is.False, "A NaN in the report stops the profile runner's JSON reader.");
            Assert.That(hz, Is.EqualTo(expected).Within(1e-9));
        }
    }
}
