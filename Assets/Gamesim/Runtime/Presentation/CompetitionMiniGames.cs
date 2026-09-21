using System;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Versioned minigame rules on a 0-10 score scale, adapted to Compete's 0-1 performance.
    /// The original overloads preserve web parity for existing seasons. New seasons explicitly
    /// select version 3; version 2 introduced effort/recovery and monotonic memory scoring,
    /// while version 3 gives reaction targets a fixed schedule and equal pointer error rules.
    /// All cosmetic randomness belongs to the attempt and never advances the season generator.
    /// </summary>
    public static class CompetitionMiniGames
    {
        public const int LegacyRules = 1;
        public const int ImprovedRules = 2;
        public const int CurrentRules = 3;

        /// <summary>A ranked attempt is identical after cancel/reload and never spends season RNG.</summary>
        public static uint AttemptSeed(uint seasonSeed, int week, int phase, int rulesVersion, bool practice)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (uint value in new[] { seasonSeed, (uint)week, (uint)phase, (uint)rulesVersion, practice ? 0x70726163u : 0x72616e6bu })
                    hash = (hash ^ value) * 16777619u;
                return hash == 0 ? 1u : hash;
            }
        }

        public static double EnduranceScore(double heldSeconds, double limitSeconds, int rulesVersion) =>
            EnduranceScore(heldSeconds, rulesVersion >= ImprovedRules ? limitSeconds * .65 : limitSeconds);

        /// <summary>Version 2: effort consumes grip; recovery restores it but earns no effort time.</summary>
        public static double MeterAfter(double meter, double elapsedSeconds, double limitSeconds,
            double deltaSeconds, bool holding, int rulesVersion)
        {
            if (rulesVersion < ImprovedRules) return MeterAfter(meter, elapsedSeconds, limitSeconds, deltaSeconds, holding);
            if (limitSeconds <= 0 || deltaSeconds <= 0) return meter;
            double progress = Math.Max(0, Math.Min(1, elapsedSeconds / limitSeconds));
            return Math.Max(MeterEmpty, Math.Min(MeterFull,
                meter + deltaSeconds * (holding ? -(12 + 8 * progress) : 28)));
        }

        public static double ReactionScore(int hits, int spawned, int falseStarts, int rulesVersion) =>
            ReactionScore(hits, spawned + (rulesVersion >= ImprovedRules ? Math.Max(0, falseStarts) : 0));

        public static double MemoryScore(int matched, int pairs, int wrongFlips,
            double secondsLeft, double limitSeconds, int rulesVersion)
        {
            if (rulesVersion < ImprovedRules) return MemoryScore(matched, pairs, wrongFlips, secondsLeft, limitSeconds);
            if (pairs <= 0) return 0;
            matched = Math.Max(0, Math.Min(pairs, matched));
            double progress = 9.0 * matched / pairs;
            double bonus = matched == pairs && limitSeconds > 0 ? Math.Max(0, Math.Min(1, secondsLeft / limitSeconds)) : 0;
            return Round(Math.Max(0, progress + bonus - Math.Min(Math.Max(0, wrongFlips) * .15, 5)));
        }

        public static string Brief(Kind kind, int rulesVersion)
        {
            if (rulesVersion < ImprovedRules) return Brief(kind);
            switch (kind)
            {
                case Kind.Endurance:
                    return "Balance effort and recovery. Hold to earn effort time and spend grip; release to recover. "
                        + "An empty grip ends your attempt. Hold for 65% of the clock for full marks. Space/right trigger holds; the on-screen button toggles.";
                case Kind.Reaction:
                    if (rulesVersion >= CurrentRules)
                        return "Click the visible target or press its named direction (arrow / D-pad). Each press counts once. "
                            + "Wrong directions and clicks outside the target miss; early presses or clicks reduce accuracy. "
                            + "Every target gets its full named response window. Tab / shoulders: controls; P / Start: pause.";
                    return "Hit the visible target or press its named direction (arrow key or D-pad). "
                        + "Wrong directions and expired targets miss. Pressing before a target appears reduces accuracy. Space does not aim for you.";
                case Kind.Memory:
                    return "Match eight pairs on the 4 by 4 board. Arrows or D-pad move; Enter/A flips. "
                        + "Each pair earns progress, mistakes cost 0.15, and finishing quickly adds up to one point. Completing another pair never lowers your score.";
                default: return Brief(kind);
            }
        }
        /// <summary>
        /// Which minigame a competition plays.
        ///
        /// <para><see cref="Kind.Precision"/> is the timing bar this port already had. It stays as
        /// the fallback rather than being removed, because a category nobody has written a game for
        /// should still be playable — and because it is what every existing test drives.</para>
        /// </summary>
        public enum Kind { Precision, Endurance, Reaction, Memory }

        /// <summary>
        /// The game a category plays.
        ///
        /// <para><b>Three, not the reference's five.</b> <c>EpisodeEngine.CompetitionCategory</c>
        /// only ever produces Skill, Mental and Endurance. <c>WebRules</c> also weights Physical and
        /// Crapshoot, and nothing asks for them — so a word game and a dice game would be screens no
        /// player could reach. Widening the rotation to produce those two is a rules change that
        /// re-rolls every seeded season, which is a separate decision with a fixture cost, not
        /// something to smuggle in behind a presentation change.</para>
        /// </summary>
        public static Kind For(string category)
        {
            switch (category)
            {
                case "Endurance": return Kind.Endurance;
                case "Skill": return Kind.Reaction;
                case "Mental": return Kind.Memory;
                default: return Kind.Precision;
            }
        }

        /// <summary>The reference's <c>TIME_LIMITS</c>, in seconds.</summary>
        public static double TimeLimit(Kind kind)
        {
            switch (kind)
            {
                case Kind.Reaction: return 20;
                case Kind.Endurance: return 30;
                case Kind.Memory: return 30;
                default: return 0;   // The timing bar has no clock; it takes three attempts.
            }
        }

        /// <summary>What the <c>Compete</c> command wants, out of a minigame's 0–10 score.</summary>
        public static double Performance(double score) => Math.Max(0, Math.Min(1, score / 10));

        /// <summary>The reference rounds every score to two places before handing it over.</summary>
        public static double Round(double score) => Math.Round(score * 100) / 100;

        // ---------------------------------------------------------------- endurance

        /// <summary>How many pairs a memory board holds. The reference's desktop count.</summary>
        public const int MemoryPairs = 8;

        /// <summary>The meter a hold starts with, and the value at which it gives out.</summary>
        public const double MeterFull = 100, MeterEmpty = 0;

        /// <summary>
        /// What holding on is worth: the fraction of the clock spent holding, out of ten.
        ///
        /// <para>Time spent holding, not time survived — letting go to save the meter costs score,
        /// which is the whole tension of the thing.</para>
        /// </summary>
        public static double EnduranceScore(double heldSeconds, double limitSeconds)
        {
            if (limitSeconds <= 0) return 0;
            return Round(Math.Min(10, Math.Max(0, heldSeconds / limitSeconds) * 10));
        }

        /// <summary>
        /// The grip meter after one frame.
        ///
        /// <para>Holding refills it and letting go drains it, and both get worse as the competition
        /// wears on: the fill rate decays from 40 a second to 15, the drain climbs from 30 to 40.
        /// That is the reference's stamina curve, and it is why a hold that works in the first ten
        /// seconds does not work in the last ten.</para>
        /// </summary>
        public static double MeterAfter(double meter, double elapsedSeconds, double limitSeconds,
            double deltaSeconds, bool holding)
        {
            if (limitSeconds <= 0 || deltaSeconds <= 0) return meter;
            double progress = Math.Max(0, Math.Min(1, elapsedSeconds / limitSeconds));
            if (holding)
            {
                double fill = Math.Max(15, 40 - progress * 25);
                return Math.Min(MeterFull, meter + deltaSeconds * fill);
            }
            double drain = 30 + progress * 10;
            return Math.Max(MeterEmpty, meter - deltaSeconds * drain);
        }

        // ---------------------------------------------------------------- reaction

        /// <summary>
        /// What tapping targets is worth: the share of them you actually hit, out of ten.
        ///
        /// <para>Accuracy, not speed. A target that times out counts against you exactly as much as
        /// one you missed, so hesitating is the same as failing — which is what makes it a reaction
        /// test rather than a clicking test.</para>
        /// </summary>
        public static double ReactionScore(int hits, int spawned)
        {
            if (spawned <= 0) return 0;
            double accuracy = Math.Max(0, Math.Min(1, (double)hits / spawned));
            return Round(Math.Min(10, accuracy * 10));
        }

        // ---------------------------------------------------------------- memory

        /// <summary>
        /// What a memory board is worth, which depends on whether you finished it.
        ///
        /// <para>Two different formulas, both the reference's. An unfinished board scores the
        /// fraction matched out of ten and cannot fall below one. A <b>finished</b> board starts at
        /// eight and adds up to two for the time left — so clearing it slowly can score below matching
        /// seven pairs. This historical crossover is retained only for version 1.</para>
        ///
        /// <para>A wrong flip costs 0.15 either way, capped at five, so guessing is punished without
        /// ever being fatal.</para>
        /// </summary>
        public static double MemoryScore(int matched, int pairs, int wrongFlips,
            double secondsLeft, double limitSeconds)
        {
            if (pairs <= 0) return 0;
            matched = Math.Max(0, Math.Min(pairs, matched));
            double penalty = Math.Min(Math.Max(0, wrongFlips) * 0.15, 5);

            if (matched < pairs)
                return Round(Math.Max(1, (double)matched / pairs * 10 - penalty));

            double timeBonus = limitSeconds <= 0 ? 0
                : Math.Max(0, Math.Min(1, secondsLeft / limitSeconds)) * 2;
            return Round(Math.Min(10, Math.Max(3, 8 + timeBonus - penalty)));
        }

        // ---------------------------------------------------------------- what the player is told

        /// <summary>The one-line brief above the game, so the player knows what is being asked.</summary>
        public static string Brief(Kind kind)
        {
            switch (kind)
            {
                case Kind.Endurance:
                    return "HOUSE SIGNALS: hold on. Your grip refills while you hold and drains while you "
                        + "let go, and both get harder as the clock runs down. Score is how much of the "
                        + "clock you spent holding.";
                case Kind.Reaction:
                    return "HOUSE SIGNALS: hit every target before it goes. A target you let expire counts "
                        + "against you exactly as much as one you miss. Score is the share you hit.";
                case Kind.Memory:
                    return "HOUSE SIGNALS · LEGACY RULES: match all " + MemoryPairs + " pairs. Unfinished boards score matched pairs out of ten; "
                        + "completion scores 8 plus up to 2 for remaining time. A late completion can score less than seven pairs. A wrong flip costs 0.15.";
                default:
                    return "HOUSE SIGNALS: stop the marker near the center three times.";
            }
        }

        /// <summary>The words on the control that starts a game. Captions are a contract.</summary>
        public static string EnterCaption(Kind kind)
        {
            switch (kind)
            {
                case Kind.Endurance: return "Enter endurance challenge";
                case Kind.Reaction: return "Enter reaction challenge";
                case Kind.Memory: return "Enter memory challenge";
                default: return "Enter precision challenge";
            }
        }
    }
}
