using System;

namespace Gamesim.Simulation
{
    /// <summary>How the player and a housemate get on (slice S2, talk rules 2): a word, never a number.</summary>
    public enum ChemistryBand
    {
        /// <summary>-2 or less: "grating". Every rapport gate with them is 3 higher; tenths x0.8.</summary>
        Grating = -1,
        /// <summary>-1 to 1, and every housemate before their third counted talk: "neither". No effect.</summary>
        Neither = 0,
        /// <summary>2 to 5: "easy company". Tenths x1.1.</summary>
        Easy = 1,
        /// <summary>6 or more: "kindred". Every rapport gate with them 2 lower, every week gate 1 lower; tenths x1.25.</summary>
        Kindred = 2,
    }

    /// <summary>
    /// Chemistry with a housemate (slice S2, the lead's rulings R10, T1 and T2): round(agreement / 2) plus the housemates' trait
    /// affinity for the player, clamped to -3..3. Shown, and in effect, only at talk rules 2 or later and from the housemate's
    /// third counted talk (<c>contact.talks &gt;= 3</c>); before that it counts as <see cref="ChemistryBand.Neither"/>. Derived,
    /// never saved; pure, nothing drawn.
    ///
    /// <para>Every gate a talk, a layer or a stage reads goes through <see cref="RapportGate"/> and <see cref="WeekGate"/>, the one
    /// place kindred and grating shift them. Below talk rules 2 both return the gate unchanged.</para>
    /// </summary>
    public static class TalkChemistry
    {
        /// <summary>Counted talks with a housemate before their chemistry shows and takes effect (T1).</summary>
        public const int ShownAfterTalks = 3;

        /// <summary>The trait affinity's clamp (R10).</summary>
        public const int MostAffinity = 3;

        /// <summary>The bands' lower edges (spec 3.6): kindred 6, easy company 2; grating at -2 or less.</summary>
        public const int KindredFrom = 6, EasyFrom = 2, GratingTo = -2;

        /// <summary>The gate shifts (T2): kindred lowers rapport gates by 2 and week gates by 1; grating raises rapport gates by 3.</summary>
        public const int KindredRapport = 2, KindredWeek = 1, GratingRapport = 3;

        /// <summary>The words the screen shows.</summary>
        public const string KindredWord = "kindred", EasyWord = "easy company", NeitherWord = "neither", GratingWord = "grating";

        /// <summary>Whether the season plays slice 2's rules: free talk on, at talk rules 2 or later.</summary>
        public static bool On(EpisodeState s) => FreeTalk.On(s) && TalkRules.At(s, TalkRules.Chemistry);

        /// <summary>The value from an agreement (-10..10) and an affinity: round(agreement / 2), halves away from zero, plus the clamped affinity.</summary>
        public static int ValueOf(int agreement, int affinity)
        {
            int a = Math.Max(-TalkRules.MostAgreement, Math.Min(TalkRules.MostAgreement, agreement));
            int half = (int)Math.Round(a / 2.0, MidpointRounding.AwayFromZero);
            return half + Math.Max(-MostAffinity, Math.Min(MostAffinity, affinity));
        }

        /// <summary>The band of a value (spec 3.6's edges).</summary>
        public static ChemistryBand BandOf(int value) =>
            value >= KindredFrom ? ChemistryBand.Kindred : value >= EasyFrom ? ChemistryBand.Easy
            : value <= GratingTo ? ChemistryBand.Grating : ChemistryBand.Neither;

        /// <summary>A band's word.</summary>
        public static string Word(ChemistryBand band)
        {
            switch (band)
            {
                case ChemistryBand.Kindred: return KindredWord;
                case ChemistryBand.Easy: return EasyWord;
                case ChemistryBand.Grating: return GratingWord;
                default: return NeitherWord;
            }
        }

        /// <summary>The housemate's trait affinity for the player (<see cref="TraitAffinity.Compatibility(ContestantState, ContestantState)"/>, the housemate observing), unclamped.</summary>
        public static int Affinity(EpisodeState s, string npcId) =>
            TraitAffinity.Compatibility(s?.Find(npcId), s?.Find(s.playerId));

        /// <summary>The raw value now, from the saved agreement; 0 when slice 2 is off or they are not a housemate.</summary>
        public static int Value(EpisodeState s, string npcId)
        {
            if (!On(s)) return 0;
            var npc = s.Find(npcId);
            if (npc == null || npc.isPlayer) return 0;
            return ValueOf(FreeTalk.Contact(s, npcId)?.agreement ?? 0, Affinity(s, npcId));
        }

        /// <summary>Whether the chemistry shows and is in effect: slice 2 on and three counted talks with them (T1).</summary>
        public static bool Shown(EpisodeState s, string npcId) =>
            On(s) && (FreeTalk.Contact(s, npcId)?.talks ?? 0) >= ShownAfterTalks && s.Find(npcId) is ContestantState npc && !npc.isPlayer;

        /// <summary>The band in effect: <see cref="ChemistryBand.Neither"/> until <see cref="Shown"/>.</summary>
        public static ChemistryBand Band(EpisodeState s, string npcId) =>
            Shown(s, npcId) ? BandOf(Value(s, npcId)) : ChemistryBand.Neither;

        /// <summary>The word to show for a housemate, or null while it is not shown.</summary>
        public static string WordFor(EpisodeState s, string npcId) => Shown(s, npcId) ? Word(Band(s, npcId)) : null;

        /// <summary>The tenths multiplier in hundredths (T2): kindred 125, easy company 110, neither 100, grating 80.</summary>
        public static int TenthsTimes(ChemistryBand band) =>
            band == ChemistryBand.Kindred ? 125 : band == ChemistryBand.Easy ? 110 : band == ChemistryBand.Grating ? 80 : 100;

        /// <summary>
        /// A rapport gate with a housemate as their chemistry has it (T2): kindred 2 lower (not below 0), grating 3 higher,
        /// otherwise unchanged. Below talk rules 2, and before the third counted talk, the gate itself.
        /// </summary>
        public static int RapportGate(EpisodeState s, string npcId, int gate)
        {
            switch (Band(s, npcId))
            {
                case ChemistryBand.Kindred: return Math.Max(0, gate - KindredRapport);
                case ChemistryBand.Grating: return gate + GratingRapport;
                default: return gate;
            }
        }

        /// <summary>A week gate with a housemate as their chemistry has it (T2): kindred 1 lower, not below week 1; otherwise unchanged. 0 stays 0 (no gate).</summary>
        public static int WeekGate(EpisodeState s, string npcId, int week)
        {
            if (week <= 0) return week;
            return Band(s, npcId) == ChemistryBand.Kindred ? Math.Max(1, week - KindredWeek) : week;
        }
    }
}
