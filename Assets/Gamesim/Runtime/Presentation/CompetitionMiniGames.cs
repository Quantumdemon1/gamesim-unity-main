using System;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The rules behind each competition's minigame.
    ///
    /// <para>Every competition in this port plays the same thing: a timing bar, three attempts. The
    /// simulation has told competitions apart since it was written — <c>WebRules</c> weights a
    /// houseguest's stats differently for an endurance competition than for a mental one — and the
    /// player has experienced all of them identically. The reference build routes a different
    /// minigame per type, which is what makes a mental competition feel mental.</para>
    ///
    /// <para>Ported from <c>src/components/mini-games/</c>. Scores are kept on the reference's own
    /// 0–10 scale so its numbers stay legible against its source, and <see cref="Performance"/>
    /// converts to the 0–1 the <c>Compete</c> command takes.</para>
    ///
    /// <para><b>Nothing here changes the simulation.</b> A minigame produces the same
    /// <c>performance</c> number the timing bar already produced, and the engine weighs it the same
    /// way — so this cannot alter a seeded season, and a save written before it loads unchanged.
    /// </para>
    /// </summary>
    public static class CompetitionMiniGames
    {
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
        /// eight and adds up to two for the time left — so clearing it slowly still beats matching
        /// most of it quickly, and clearing it fast is the only route to ten.</para>
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
                    return "HOUSE SIGNALS: match all " + MemoryPairs + " pairs. Clearing the board is worth "
                        + "more than matching most of it, and clearing it quickly is the only way to full "
                        + "marks. A wrong flip costs a little.";
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
