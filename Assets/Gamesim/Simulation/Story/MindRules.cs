namespace Gamesim.Simulation
{
    /// <summary>
    /// The housemates' minds: the on/off ladder for how the housemates decide and what they show of it, one saved level
    /// a season starts on, <c>story.mindRules</c>, in the pattern of <see cref="TalkRules"/>. Level 0 is off. Its levels:
    /// 1, decisions you can see (the veto and the replacement, with a spoken reason); 2, nomination plans and a playing
    /// style per housemate; 3, they remember what you did; 4, betrayal on purpose and house storylines; 5, vote counts and
    /// blindsides.
    ///
    /// <para><see cref="Latest"/> is the bound validation holds a season to, never a gate and never what a season is
    /// started on. Every level is defined here before any rule reads it, and admitted only from the schema that introduced
    /// it (<see cref="Admits"/>: every level from 31). A fresh season's level is written in
    /// <see cref="ShippedRules.ApplyFresh(EpisodeState)"/> and only there, as <see cref="Shipped"/>; no level is enrolled
    /// yet. Load, migration, recovery and the importer never infer a level: an old save migrates at 0. Nothing in this
    /// build reads the level to change play; a rule that does reads the level that introduced it as a lower bound
    /// (<see cref="At"/>), never <see cref="Latest"/>. What the minds remember rides state the game already saves.</para>
    /// </summary>
    public static class MindRules
    {
        /// <summary>No minds: every season before schema 31, and every season the game starts until level 1's enrolment.</summary>
        public const int Off = 0;

        /// <summary>Decisions you can see: the veto and the replacement, with a spoken reason.</summary>
        public const int Decisions = 1;

        /// <summary>Nomination plans and a playing style per housemate.</summary>
        public const int Plans = 2;

        /// <summary>They remember what you did.</summary>
        public const int Memory = 3;

        /// <summary>Betrayal on purpose and house storylines.</summary>
        public const int Storylines = 4;

        /// <summary>Vote counts and blindsides.</summary>
        public const int Blindsides = 5;

        /// <summary>The last level this build defines: a bound, never a behaviour.</summary>
        public const int Latest = Blindsides;

        /// <summary>
        /// The level a season the game starts plays under (<see cref="ShippedRules.ApplyFresh(EpisodeState)"/>): off, until
        /// a level is enrolled after its balance measurement. Never <see cref="Latest"/>.
        /// </summary>
        public const int Shipped = Off;

        /// <summary>The schema that introduced the ladder and every level on it.</summary>
        public const int Schema = 31;

        /// <summary>The closed set this build defines: 0 to <see cref="Latest"/>.</summary>
        public static bool IsDefined(int level) => level >= Off && level <= Latest;

        /// <summary>The schema that introduced a level: 31 for every level from 1. Level 0 is every schema's.</summary>
        public static int SchemaFor(int level) => level >= Decisions ? Schema : 0;

        /// <summary>Whether a season at <paramref name="schema"/> may hold mind level <paramref name="level"/>.</summary>
        public static bool Admits(int level, int schema) => IsDefined(level) && schema >= SchemaFor(level);

        /// <summary>Whether a season plays a level's rules: its saved level is that level or later. A lower bound.</summary>
        public static bool At(EpisodeState s, int level) => s?.story != null && s.story.mindRules >= level && level >= Decisions;
    }
}
