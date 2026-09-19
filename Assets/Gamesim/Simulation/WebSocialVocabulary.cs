using System;

namespace Gamesim.Simulation
{
    /// <summary>
    /// What each way of talking to somebody is worth.
    ///
    /// <para>This port has one <c>Talk</c> where the reference has five conversations, and one
    /// <c>ShareInformation</c> where it has two. Every conversation here moved a flat four points,
    /// so choosing how to approach somebody was a choice with no consequences — which is the same as
    /// not being a choice.</para>
    ///
    /// <para>Ported from <c>player-action-reducer.ts</c>, numbers and all. The functions take their
    /// draws rather than making them, so the ranges can be pinned in a test and the engine can spend
    /// the season's own generator — a committed command's rolls are recorded and must replay.</para>
    ///
    /// <para><b>The order of the draws is part of the port.</b> A risky conversation draws twice, for
    /// the gate and then the amount, exactly as the source does. Drawing once and reusing it would
    /// correlate "did it work" with "how well", which is a different game.</para>
    /// </summary>
    public static class WebSocialVocabulary
    {
        // ---------------------------------------------------------------- the safe conversations

        /// <summary>Passing the time. Three to five points, and it cannot go wrong.</summary>
        public static double SmallTalk(double roll) => Step(roll, 3) + 3;

        /// <summary>Telling them something about yourself. Five to eight.</summary>
        public static double PersonalChat(double roll) => Step(roll, 4) + 5;

        /// <summary>
        /// Time spent rather than words exchanged. Five to twelve — the strongest safe option, and
        /// the source's own note says it beats an ordinary conversation.
        /// </summary>
        public static double RelationshipBuilding(double roll) => Step(roll, 8) + 5;

        /// <summary>
        /// Talking tactics. Two to five: the least warmth of any conversation, because it is not
        /// really about liking each other.
        /// </summary>
        public static double StrategicDiscussion(double roll) => Step(roll, 4) + 2;

        // ---------------------------------------------------------------- the risky ones

        /// <summary>The chance each risky conversation goes well. The source's own thresholds.</summary>
        public const double DiscussGameFloor = 0.3, ShareSecretFloor = 0.35, VentFloor = 0.4;

        /// <summary>What each costs when it does not.</summary>
        public const double DiscussGameBackfire = -5, ShareSecretBackfire = -15, VentBackfire = -10;

        /// <summary>
        /// Talking game. Four to ten when it lands, minus five when they decide you are scheming.
        /// </summary>
        public static double DiscussGame(double gate, double roll) =>
            gate > DiscussGameFloor ? Step(roll, 7) + 4 : DiscussGameBackfire;

        /// <summary>
        /// The most either side can gain or lose in one conversation: ten to eighteen when they keep
        /// it, minus fifteen when they decide to use it.
        /// </summary>
        public static double ShareSecret(double gate, double roll) =>
            gate > ShareSecretFloor ? Step(roll, 9) + 10 : ShareSecretBackfire;

        // ---------------------------------------------------------------- the house-wide ones

        /// <summary>What a whispered rumour does to the pair it is whispered between.</summary>
        public static double WhisperDamage(double roll) => -(Step(roll, 6) + 4);

        /// <summary>What saying it out loud does, per person who hears it.</summary>
        public static double CalloutDamage(double roll) => -(Step(roll, 8) + 5);

        /// <summary>How many people a public call-out reaches: two or three.</summary>
        public static int CalloutAudience(double roll) => Step(roll, 2) + 2;

        /// <summary>What a rumour costs its subject when it comes back to them.</summary>
        public const double WhisperBackfire = -6, CalloutBackfire = -12;

        /// <summary>A failed house meeting, per houseguest: minus two to minus five.</summary>
        public static double MeetingFailure(double roll) => -(Step(roll, 4) + 2);

        /// <summary>A rallying meeting, for everyone but the one sceptic: plus three to plus eight.</summary>
        public static double MeetingRally(double roll) => Step(roll, 6) + 3;

        /// <summary>What the sceptic in the room thinks of it.</summary>
        public const double MeetingSceptic = -3;

        /// <summary>
        /// Airing everything, per houseguest. No middle: they either agree with you or they do not,
        /// which is what makes it the approach that can end a game in one afternoon.
        /// </summary>
        public static double MeetingAiring(double roll) => roll > 0.5 ? 8 : -10;

        /// <summary>The chance a house meeting goes the way it was meant to.</summary>
        public const double MeetingFloor = 0.35;

        /// <summary>The chance a rumour lands rather than reaching its subject.</summary>
        public const double RumourFloor = 0.35;

        // ---------------------------------------------------------------- buying a turn

        /// <summary>What burning one bridge for an extra action costs that person.</summary>
        public const double BurnOneCost = -8;

        /// <summary>What spreading the cost costs everybody.</summary>
        public const double SpreadAllCost = -3;

        /// <summary>
        /// How many extra actions a player may buy in one phase.
        ///
        /// <para>Not the reference's — it has no ceiling, because its budget is a counter it can
        /// decrement indefinitely. A ceiling exists here because the save format bounds everything
        /// it stores, and an unbounded purchase count is a season that eventually will not load.
        /// Six is generous: at three points of damage each, spending it costs the whole house
        /// eighteen points of warmth.</para>
        /// </summary>
        public const int PurchaseCeiling = 6;

        /// <summary>The two ways of paying, spelled as the reference spells them.</summary>
        public const string BurnOne = "random_one", SpreadAll = "spread_all";

        public static bool IsKnownCost(string cost) => cost == BurnOne || cost == SpreadAll;

        // ---------------------------------------------------------------- the shape of a draw

        /// <summary>
        /// The source's <c>Math.floor(Math.random() * n)</c>, which is an integer in 0..n−1.
        ///
        /// <para>Guarded at the top because a roll of exactly one would step outside the range the
        /// source can produce. <see cref="EpisodeEngine.Roll"/> returns values below one, so this is
        /// belt and braces rather than a live case — but a range that is right by luck is a range
        /// that stops being right when the generator changes.</para>
        /// </summary>
        private static int Step(double roll, int n)
        {
            if (double.IsNaN(roll) || roll < 0) return 0;
            int value = (int)(roll * n);
            return value >= n ? n - 1 : value;
        }
    }
}
