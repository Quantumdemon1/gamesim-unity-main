using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The performance evidence the standalone profile (<c>--gamesim-verify</c>) reports, and the one
    /// rule that turns it into a verdict. Unity-free on purpose: the frame lists are milliseconds the
    /// runner measured and everything here is arithmetic on them, so Tools/SimulationTests runs the
    /// same rule the player does.
    ///
    /// <para>The owner's target (OWNER_COMPLETION_DECISIONS, 2026-10-04): 1920x1080 at 60 fps on the
    /// GTX 1060 machine. The acceptance thresholds: the steady sample's 95th percentile frame at or
    /// under 16.7 ms and its 99th at or under 33.3 ms, with the raw distribution kept beside the
    /// report. A verdict is given only for a run that measured that target - the shipping (not a
    /// development) player, graphical, not <c>-batchmode</c>, windowed, uncapped with VSync off,
    /// 1920x1080 on every measured frame, for at least 300 s. Anything else is <see cref="NotAssessed"/> with its reasons. The report's own
    /// <c>status</c> is the run's execution checks and is never this verdict.</para>
    /// </summary>
    public static class VerificationPerformance
    {
        public const int TargetWidth = 1920, TargetHeight = 1080;
        public const double MinimumSeconds = 300;
        public const float P95LimitMs = 16.7f, P99LimitMs = 33.3f;

        /// <summary>The slow-frame lines the report counts against: a missed 60 Hz frame, a missed 30 Hz frame, a hitch.</summary>
        public const float SlowFrameMs = 16.7f, VerySlowFrameMs = 33.3f, HitchFrameMs = 50f;

        public const string Met = "Met", NotMet = "Not met", NotAssessed = "Not assessed: ";

        /// <summary>The explicit flag that, with a house size above any roster's, profiles the verification-only stress house.</summary>
        public const string StressRosterArgument = "--gamesim-stress-roster";

        /// <summary>The profile's house-size argument: how many houseguests the measured season seats.</summary>
        public const string HouseSizeArgument = "--gamesim-house-size";

        /// <summary>The frame-time histogram's bucket tops, in milliseconds; one more bucket counts everything over the last.</summary>
        public static readonly float[] HistogramUpperMs = { 4.2f, 8.3f, 11.1f, 16.7f, 20f, 25f, 33.3f, 50f, 100f };

        /// <summary>What the verdict is read from: the run's conditions and the steady sample's two percentiles.</summary>
        public sealed class Evidence
        {
            public bool Graphical, BatchMode;
            /// <summary>A development player (Debug.isDebugBuild): its checks and profiler hooks make it no shipping executable.</summary>
            public bool DevelopmentBuild;
            public int RequestedWidth = TargetWidth, RequestedHeight = TargetHeight, RequestedFrameCap = -1, RequestedVSyncCount;
            public bool RequestedWindowed = true;
            public double MeasuredSeconds;
            public int Frames, DisplaySamples, ResolutionMismatches, FrameCapMismatches, DisplayModeMismatches;
            public double P95Ms, P99Ms;
        }

        /// <summary>
        /// <see cref="Met"/> or <see cref="NotMet"/> against the two percentiles for a run that measured
        /// the target, otherwise <see cref="NotAssessed"/> followed by every reason it did not.
        /// </summary>
        public static string Verdict(Evidence evidence, out string basis)
        {
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));
            basis = "p95 " + Ms(evidence.P95Ms) + " ms (limit " + Ms(P95LimitMs) + " ms), p99 " + Ms(evidence.P99Ms)
                + " ms (limit " + Ms(P99LimitMs) + " ms), " + evidence.Frames.ToString(CultureInfo.InvariantCulture)
                + " frames over " + evidence.MeasuredSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
            var reasons = WhyNotAssessed(evidence);
            if (reasons.Count > 0) return NotAssessed + string.Join("; ", reasons) + ".";
            return (float)evidence.P95Ms <= P95LimitMs && (float)evidence.P99Ms <= P99LimitMs ? Met : NotMet;
        }

        /// <summary>Every condition of the target the run did not meet, in words; empty when the run can be judged.</summary>
        public static List<string> WhyNotAssessed(Evidence e)
        {
            var why = new List<string>();
            if (e.BatchMode) why.Add("the player ran with -batchmode");
            if (!e.Graphical) why.Add("no graphics device presented the frames");
            if (e.DevelopmentBuild) why.Add("a development build, not the shipping player");
            if (e.RequestedWidth != TargetWidth || e.RequestedHeight != TargetHeight)
                why.Add("the profile requested " + e.RequestedWidth + "x" + e.RequestedHeight + ", not " + TargetWidth + "x" + TargetHeight);
            if (!e.RequestedWindowed) why.Add("the profile did not request a window");
            if (e.RequestedFrameCap != -1 || e.RequestedVSyncCount != 0) why.Add("the profile requested a frame cap or VSync");
            if (e.Frames <= 0) why.Add("no frames were measured");
            else if (e.DisplaySamples != e.Frames)
                why.Add(Count(e.DisplaySamples) + " of " + Count(e.Frames) + " measured frames carry resolution, cap and window evidence");
            if (e.ResolutionMismatches > 0) why.Add(Count(e.ResolutionMismatches) + " measured frames were not " + TargetWidth + "x" + TargetHeight);
            if (e.DisplayModeMismatches > 0) why.Add(Count(e.DisplayModeMismatches) + " measured frames were not windowed");
            if (e.FrameCapMismatches > 0) why.Add(Count(e.FrameCapMismatches) + " measured frames ran capped or with VSync");
            if (!(e.MeasuredSeconds >= MinimumSeconds))
                why.Add("the steady sample ran " + e.MeasuredSeconds.ToString("0.0", CultureInfo.InvariantCulture)
                    + " s of the " + MinimumSeconds.ToString("0", CultureInfo.InvariantCulture) + " s required");
            return why;
        }

        /// <summary>
        /// Nearest-rank percentile of an ascending list: the value at rank ceil(n p), as the profile
        /// has always reported its median, p95 and p99. Zero for an empty list.
        /// </summary>
        public static double Percentile(IReadOnlyList<float> sorted, double p)
            => sorted == null || sorted.Count == 0 ? 0
                : sorted[Math.Max(0, Math.Min(sorted.Count - 1, (int)Math.Ceiling(sorted.Count * p) - 1))];

        /// <summary>How many frames took longer than <paramref name="limitMs"/>.</summary>
        public static int CountOver(IEnumerable<float> frames, float limitMs) => frames == null ? 0 : frames.Count(ms => ms > limitMs);

        /// <summary>Frames per bucket of <see cref="HistogramUpperMs"/>: bucket i holds frames over the previous top and at most its own; the last holds everything over 100 ms.</summary>
        public static int[] Histogram(IEnumerable<float> frames)
        {
            var counts = new int[HistogramUpperMs.Length + 1];
            if (frames == null) return counts;
            foreach (float ms in frames)
            {
                int bucket = 0;
                while (bucket < HistogramUpperMs.Length && ms > HistogramUpperMs[bucket]) bucket++;
                counts[bucket]++;
            }
            return counts;
        }

        /// <summary>
        /// Why a stress-roster request cannot run, or null when it can (or was not made). The stress
        /// house is a profile workload: it needs the verification switch, a house size above every
        /// roster's largest, and none of the modes that build their own season.
        /// </summary>
        public static string StressRosterRefusal(ICollection<string> args, int requestedHouseSize)
        {
            if (args == null || !args.Contains(StressRosterArgument)) return null;
            if (!args.Contains("--gamesim-verify"))
                return StressRosterArgument + " requires --gamesim-verify and an isolated absolute --gamesim-save-root.";
            if (args.Contains("--gamesim-verify-creator") || args.Contains("--gamesim-look-sheet"))
                return StressRosterArgument + " is a profile workload; the creator and look-sheet modes build their own seasons.";
            if (requestedHouseSize <= SeasonBuilder.LargestRosterHouse || requestedHouseSize > SeasonBuilder.LargestStressHouse)
                return StressRosterArgument + " requires " + HouseSizeArgument + " from " + (SeasonBuilder.LargestRosterHouse + 1) + " to "
                    + SeasonBuilder.LargestStressHouse + " (a roster seats " + SeasonBuilder.LargestRosterHouse + "); requested "
                    + (requestedHouseSize != 0 ? requestedHouseSize.ToString(CultureInfo.InvariantCulture) : "none") + ".";
            return null;
        }

        /// <summary>
        /// The house size the command line asked for, exactly as written: false (and 0) when the argument
        /// is missing or not a whole number. Never clamped here - <see cref="StressRosterRefusal"/> judges
        /// the request as made, so a stress request for forty is refused rather than run at sixteen; the
        /// runner clamps an ordinary profile's size afterwards.
        /// </summary>
        public static bool TryReadHouseSize(IList<string> args, out int requested)
        {
            requested = 0;
            if (args == null) return false;
            int at = -1;
            for (int i = 0; i < args.Count; i++) if (args[i] == HouseSizeArgument) { at = i; break; }
            return at >= 0 && at + 1 < args.Count
                && int.TryParse(args[at + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out requested);
        }

        /// <summary>
        /// Which season a profile measures. A size asked for is always a season the director starts - the
        /// stress house above every roster, or a roster's own at that size - so the six, the twelve and the
        /// sixteen all play under the rules a started season has from week one. The scene's own season (the
        /// bootstrap six, every rule off) is measured only when no size is asked for, as before sizes existed.
        /// </summary>
        public static VerificationProfileSeason ProfileSeason(int houseSize, bool stressRoster)
            => houseSize <= 0 ? VerificationProfileSeason.Scene
                : stressRoster ? VerificationProfileSeason.Stress : VerificationProfileSeason.Director;

        /// <summary>A display's refresh rate in hertz, or -1 when the player reports none (a zero denominator would be NaN, which a JSON reader rejects).</summary>
        public static double RefreshHz(uint numerator, uint denominator)
            => denominator == 0 ? -1 : (double)numerator / denominator;

        private static string Ms(double ms) => ms.ToString("0.00", CultureInfo.InvariantCulture);
        private static string Count(int n) => n.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The season a standalone profile measures (<see cref="VerificationPerformance.ProfileSeason"/>).</summary>
    public enum VerificationProfileSeason
    {
        /// <summary>The scene's own bootstrap season: no size was asked for.</summary>
        Scene,
        /// <summary>A season the director starts at the size asked for, as the cast screen would.</summary>
        Director,
        /// <summary>The verification-only stress house (<c>--gamesim-stress-roster</c>).</summary>
        Stress,
    }

    /// <summary>One stretch of frames, summarised: the steady sample, or a stage of the startup before it.</summary>
    [Serializable]
    public sealed class VerificationFrameSummary
    {
        public string name;
        public int frames;
        /// <summary>The frames' own time added up, in seconds: what the stretch cost, not the wall clock around it.</summary>
        public double frameSeconds;
        public double meanMs, minMs, medianMs, p90Ms, p95Ms, p99Ms, p999Ms, maxMs;
        public int framesOver16_7Ms, framesOver33_3Ms, framesOver50Ms;

        public static VerificationFrameSummary Of(string name, IEnumerable<float> frames)
        {
            var sorted = frames == null ? new List<float>() : new List<float>(frames);
            sorted.Sort();
            double total = 0;
            foreach (float ms in sorted) total += ms;
            return new VerificationFrameSummary
            {
                name = name, frames = sorted.Count, frameSeconds = total / 1000d,
                meanMs = sorted.Count == 0 ? 0 : total / sorted.Count,
                minMs = sorted.Count == 0 ? 0 : sorted[0], maxMs = sorted.Count == 0 ? 0 : sorted[sorted.Count - 1],
                medianMs = VerificationPerformance.Percentile(sorted, .5), p90Ms = VerificationPerformance.Percentile(sorted, .9),
                p95Ms = VerificationPerformance.Percentile(sorted, .95), p99Ms = VerificationPerformance.Percentile(sorted, .99),
                p999Ms = VerificationPerformance.Percentile(sorted, .999),
                framesOver16_7Ms = VerificationPerformance.CountOver(sorted, VerificationPerformance.SlowFrameMs),
                framesOver33_3Ms = VerificationPerformance.CountOver(sorted, VerificationPerformance.VerySlowFrameMs),
                framesOver50Ms = VerificationPerformance.CountOver(sorted, VerificationPerformance.HitchFrameMs),
            };
        }
    }

    /// <summary>
    /// The process's memory at one moment, in bytes. A field the player could not read is -1; the
    /// graphics driver's estimate is 0 in a release player, where Unity does not report it.
    /// </summary>
    [Serializable]
    public sealed class VerificationMemorySample
    {
        /// <summary>Seconds since the player started; -1 on a peak, which is field-wise and no single moment.</summary>
        public double atSeconds = -1;
        public long totalAllocatedBytes = -1, totalReservedBytes = -1, monoUsedBytes = -1, monoHeapBytes = -1;
        public long gfxDriverBytes = -1, systemUsedBytes = -1, gfxUsedBytes = -1;

        /// <summary>Each field's largest value over the samples: the high-water marks, not one sample.</summary>
        public static VerificationMemorySample Peak(IEnumerable<VerificationMemorySample> samples)
        {
            var peak = new VerificationMemorySample();
            if (samples == null) return peak;
            foreach (var s in samples)
            {
                if (s == null) continue;
                peak.totalAllocatedBytes = Math.Max(peak.totalAllocatedBytes, s.totalAllocatedBytes);
                peak.totalReservedBytes = Math.Max(peak.totalReservedBytes, s.totalReservedBytes);
                peak.monoUsedBytes = Math.Max(peak.monoUsedBytes, s.monoUsedBytes);
                peak.monoHeapBytes = Math.Max(peak.monoHeapBytes, s.monoHeapBytes);
                peak.gfxDriverBytes = Math.Max(peak.gfxDriverBytes, s.gfxDriverBytes);
                peak.systemUsedBytes = Math.Max(peak.systemUsedBytes, s.systemUsedBytes);
                peak.gfxUsedBytes = Math.Max(peak.gfxUsedBytes, s.gfxUsedBytes);
            }
            return peak;
        }
    }
}
