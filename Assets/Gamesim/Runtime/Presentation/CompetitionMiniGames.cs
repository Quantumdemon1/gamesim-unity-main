using System;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Versioned minigame rules on a 0-10 score scale, adapted to Compete's 0-1 performance.
    /// The original overloads preserve web parity for existing seasons. New seasons explicitly
    /// select version 4; version 2 introduced effort/recovery and monotonic memory scoring,
    /// version 3 gives reaction targets a fixed schedule and equal pointer error rules, and version
    /// 4 adds the reference's luck and social games (<see cref="Kind.Dice"/>, <see cref="Kind.Words"/>).
    /// All cosmetic randomness belongs to the attempt and never advances the season generator.
    /// </summary>
    public static class CompetitionMiniGames
    {
        public const int LegacyRules = 1;
        public const int ImprovedRules = 2;
        /// <summary>The fixed reaction schedule and equal pointer errors. Every version after keeps them.</summary>
        public const int ScheduledRules = 3;
        /// <summary>The luck and social games (competition rules 4).</summary>
        public const int WidenedRules = 4;
        /// <summary>The newest rules an attempt may be played under. A bound, never a behaviour: each
        /// rule is checked against the version that introduced it, so a new version changes nothing old.</summary>
        public const int CurrentRules = WidenedRules;

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
            EnduranceScore(heldSeconds, EnduranceTarget(limitSeconds, rulesVersion));

        /// <summary>The effort time that earns full marks: 65% of the clock from version 2, all of it before.</summary>
        public static double EnduranceTarget(double limitSeconds, int rulesVersion) =>
            rulesVersion >= ImprovedRules ? limitSeconds * .65 : limitSeconds;

        /// <summary>
        /// The grip's rate of change at a moment, in percent a second, for the board to show: the
        /// same terms <see cref="MeterAfter(double,double,double,double,bool,int)"/> steps by, with a
        /// pressure wave's multiplier on the drain while holding. Display only; the run steps the meter.
        /// </summary>
        public static double MeterRatePerSecond(double elapsedSeconds, double limitSeconds, bool holding, double pressure, int rulesVersion)
        {
            if (limitSeconds <= 0) return 0;
            double progress = Math.Max(0, Math.Min(1, elapsedSeconds / limitSeconds));
            if (rulesVersion < ImprovedRules)
                return holding ? Math.Max(15, 40 - progress * 25) : -(30 + progress * 10);
            return holding ? -(12 + 8 * progress) * Math.Max(1, pressure) : 28;
        }

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
                    if (rulesVersion >= ScheduledRules)
                        return "Click the visible target or press its named direction (arrow / D-pad). Each press counts once. "
                            + "Wrong directions and clicks outside the target miss; early presses or clicks reduce accuracy. "
                            + "Every target gets its full named response window. Tab / shoulders: controls; P / Start: pause.";
                    return "Hit the visible target or press its named direction (arrow key or D-pad). "
                        + "Wrong directions and expired targets miss. Pressing before a target appears reduces accuracy. Space does not aim for you.";
                case Kind.Memory:
                    return "Match eight pairs on the 4 by 4 board. Arrows or D-pad move; Enter/A flips. "
                        + "Each pair earns progress, mistakes cost 0.15, and finishing quickly adds up to one point. Completing another pair never lowers your score.";
                case Kind.Dice:
                    return "Roll three dice, up to three times. Every re-roll replaces the roll you have, so keep a good one when you see it: "
                        + "your total, 3 to 18, is your score. Arrows or D-pad choose Roll or Keep; Enter/A presses it. P / Start: pause.";
                case Kind.Words:
                    return "Spell each word by choosing its letters in order: click them, type them, or press A on one. "
                        + "A wrong spelling clears; Backspace or X takes back a letter; Tab / shoulders reach Skip word. "
                        + "Longer words score more. Esc / Start pauses: typing P spells a P.";
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
        public enum Kind { Precision, Endurance, Reaction, Memory, Dice, Words }

        /// <summary>
        /// The game a category plays: the reference's five, one each.
        ///
        /// <para>Luck and social arrived with competition rules 4, which widened the season's
        /// rotation to produce them (<c>CompetitionRules</c>). Before that the rotation only ever
        /// produced skill, mental and endurance, so a dice game and a word game would have been
        /// screens no player could reach.</para>
        /// </summary>
        public static Kind For(string category)
        {
            switch (category)
            {
                case "Endurance": return Kind.Endurance;
                case "Skill": return Kind.Reaction;
                case "Mental": return Kind.Memory;
                case "Luck": return Kind.Dice;
                case "Social": return Kind.Words;
                default: return Kind.Precision;
            }
        }

        /// <summary>The category a game plays for; null for the timing bar, which plays for any.</summary>
        public static string CategoryOf(Kind kind)
        {
            switch (kind)
            {
                case Kind.Endurance: return "Endurance";
                case Kind.Reaction: return "Skill";
                case Kind.Memory: return "Mental";
                case Kind.Dice: return "Luck";
                case Kind.Words: return "Social";
                default: return null;
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
                case Kind.Dice: return 30;
                case Kind.Words: return 30;
                default: return 0;   // The timing bar has no clock; it takes three attempts.
            }
        }

        // ---------------------------------------------------------------- dice

        /// <summary>
        /// A kept total out of ten, as the reference's Dice Roll Derby scales it: 3 is nothing, 18
        /// full marks. No roll kept - the whistle before the first - scores nothing.
        /// </summary>
        public static double DiceScore(int total) => total < 3 ? 0 : Round(Math.Min(10, (total - 3) / 15.0 * 10));

        // ---------------------------------------------------------------- words

        /// <summary>
        /// The reference's Word Scramble list, kept to words of five letters or more as it keeps
        /// them: a shorter word scrambles into itself too easily.
        /// </summary>
        public static readonly string[] BigBrotherWords =
        {
            "EVICTION", "ALLIANCE", "NOMINEE", "BACKDOOR", "FLOATER",
            "SHOWMANCE", "BLINDSIDE", "TARGET", "CEREMONY", "BLOCK",
            "POWER", "TWIST", "FINAL", "THRONE",
            "HOUSEGUEST", "NOMINATION", "COMPETITION", "STRATEGY", "ENDURANCE",
        };

        /// <summary>A game needs at least this many words; a house of short names is topped up from the list.</summary>
        public const int FewestScrambleWords = 6;

        /// <summary>The shortest and longest words the board deals: a tile for every letter, and none too easy to scramble.</summary>
        public const int ShortestScrambleWord = 4, LongestScrambleWord = 12;

        /// <summary>A word as the tiles spell it: the first word of a name, in capitals, letters only.</summary>
        public static string ScrambleForm(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string first = text.Trim().Split(' ')[0];
            var letters = new System.Text.StringBuilder();
            foreach (char c in first) if (char.IsLetter(c) && c < 128) letters.Append(char.ToUpperInvariant(c));
            return letters.ToString();
        }

        /// <summary>What a word is worth, the reference's table: longer is more.</summary>
        public static double WordPoints(int length) => length >= 9 ? 3 : length >= 7 ? 2.5 : length >= 5 ? 2 : 1.5;

        /// <summary>The points scored, capped at ten, as the reference caps them.</summary>
        public static double WordsScore(double points) => Round(Math.Max(0, Math.Min(10, points)));

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
                    // The rules strip already says which rules a season plays; the brief says the game.
                    return "HOUSE SIGNALS: match all " + MemoryPairs + " pairs. Unfinished boards score matched pairs out of ten; "
                        + "completion scores 8 plus up to 2 for remaining time. A late completion can score less than seven pairs. A wrong flip costs 0.15.";
                // Only rules 4 reaches these two; the rule-numbered overload above has their words.
                case Kind.Dice:
                case Kind.Words:
                    return Brief(kind, WidenedRules);
                default:
                    return "HOUSE SIGNALS: stop the marker near the center three times.";
            }
        }

        /// <summary>
        /// The game's name for the briefing's title where a competition has no authored title of
        /// its own (rules before version 3): each is the game's own instruction, from its brief.
        /// </summary>
        public static string DisplayName(Kind kind)
        {
            switch (kind)
            {
                case Kind.Endurance: return "Hold On";
                case Kind.Reaction: return "Hit Every Target";
                case Kind.Memory: return "Match the Pairs";
                case Kind.Dice: return "Roll the Dice";
                case Kind.Words: return "Unscramble the Words";
                default: return "Stop the Marker";
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
                case Kind.Dice: return "Enter luck challenge";
                case Kind.Words: return "Enter word challenge";
                default: return "Enter precision challenge";
            }
        }
    }
}
