#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// How well the player plays each competition's minigame (BALANCE plan B4): the performance input a
    /// Compete command carries, from nought to full marks.
    ///
    /// <para><b>Fixed levels</b> (0, .25, .5, .75, 1) measure what the input is worth in real seasons; every
    /// harness before this sent .5, which is also what the accessible alternative is worth.</para>
    ///
    /// <para><b>Per-policy distributions</b>: a normal draw about the policy's mean, cut to [0, 1]. Most
    /// players are taken as average (.5, spread .15); the competition beast plays hard (.8, .12) and the
    /// novice is a first-timer (.35, .18). These are assumptions to be replaced by human data (B8), and the
    /// report says so.</para>
    ///
    /// <para><b>Never the season's generator.</b> A draw is keyed to the season's seed, the week and the
    /// phase alone (<see cref="SeededRandom.HashSeed"/>), so it consumes nothing of the season's stream, every
    /// policy meets the same luck at the same competition, and a replay draws the same.</para>
    /// </summary>
    internal static class PerformanceModel
    {
        /// <summary>The per-policy distributions.</summary>
        public const string ByPolicy = "policy";

        public static readonly double[] Levels = { 0, 0.25, 0.5, 0.75, 1 };

        public static string Fixed(double level) => "fixed-" + level.ToString("0.00", CultureInfo.InvariantCulture);

        public static bool IsFixed(string model, out double level)
        {
            level = 0;
            return model != null && model.StartsWith("fixed-", StringComparison.Ordinal)
                && double.TryParse(model.Substring(6), NumberStyles.Float, CultureInfo.InvariantCulture, out level) && level >= 0 && level <= 1;
        }

        /// <summary>Each policy's mean and spread; anything not listed plays as an average player.</summary>
        private static readonly Dictionary<string, (double mean, double spread)> Skill = new Dictionary<string, (double, double)>(StringComparer.Ordinal)
        {
            { BalancePolicies.Beast, (0.8, 0.12) },
            { BalancePolicies.Novice, (0.35, 0.18) },
        };

        public static (double mean, double spread) SkillOf(string policy) =>
            Skill.TryGetValue(policy ?? "", out var skill) ? skill : (0.5, 0.15);

        /// <summary>The performance for one competition.</summary>
        public static double Draw(string model, string policy, uint seed, int week, EpisodePhase phase)
        {
            if (IsFixed(model, out double level)) return level;
            if (model != ByPolicy) throw new ArgumentException("No such performance model: " + model, nameof(model));
            var (mean, spread) = SkillOf(policy);
            double u1 = Uniform(seed, week, phase, 1), u2 = Uniform(seed, week, phase, 2);
            double z = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
            return Math.Max(0, Math.Min(1, mean + spread * z));
        }

        /// <summary>A uniform draw in (0, 1) keyed to the competition, never to the season's stream.</summary>
        public static double Uniform(uint seed, int week, EpisodePhase phase, int salt) =>
            (SeededRandom.HashSeed("balance-lab/performance/" + seed.ToString(CultureInfo.InvariantCulture) + "/" + week.ToString(CultureInfo.InvariantCulture)
                + "/" + phase + "/" + salt.ToString(CultureInfo.InvariantCulture)) + 0.5) / 4294967296.0;
    }
}
#endif
