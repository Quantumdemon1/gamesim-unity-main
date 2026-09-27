namespace Gamesim.Presentation
{
    /// <summary>How the ceremony reveals are paced: the player's preference, kept with the other settings.</summary>
    public enum CeremonyPace
    {
        /// <summary>The broadcast's own tempo: a beat on every key and every vote, and a longer one before the last.</summary>
        Suspenseful,
        /// <summary>The brisk tempo the reveals had before: the order still plays, but it does not linger.</summary>
        Quick,
    }

    /// <summary>
    /// The timings of the key ceremony and the live eviction, in real seconds, for each pace.
    ///
    /// <para>The reveals spend a result the engine has already decided and saved, so their length is
    /// pure presentation: suspense on a key is the time between one name and the next. The reference
    /// build holds about seven seconds on a key and most of a second on a vote; the port had 0.62 s
    /// and 0.45 s, which read every result before it could land. Suspenseful sits between the two
    /// and shortens for a big house, so sixteen keys are still a scene and not a wait. Every reveal
    /// can be sped up while it plays (<see cref="SpeedUp"/>) or skipped to its result, and the
    /// settings can make every one quick.</para>
    ///
    /// <para>Reduced motion keeps these timings: they are reading time, not movement.</para>
    /// </summary>
    public static class CeremonyPacing
    {
        /// <summary>How much faster a reveal plays while the player has asked it to speed up.</summary>
        public const float SpeedUp = 3f;

        public const float FadeIn = 0.30f;
        public const float FadeOut = 0.45f;

        // ------------------------------------------------------------ the key ceremony

        /// <summary>"X has made their decision", before the first key.</summary>
        public static float KeyIntro(CeremonyPace pace) => pace == CeremonyPace.Quick ? 1.15f : 2.4f;

        /// <summary>The time each key holds the stage; shorter the more keys there are to hand out.</summary>
        public static float PerKey(CeremonyPace pace, int keys) =>
            pace == CeremonyPace.Quick ? 0.62f : keys >= 10 ? 1.4f : keys >= 7 ? 1.8f : 2.2f;

        /// <summary>The extra wait before the last key, when every name left is waiting on one.</summary>
        public static float LastKeyBeat(CeremonyPace pace) => pace == CeremonyPace.Quick ? 0f : 1.8f;

        /// <summary>How long the block holds once the keys are out.</summary>
        public static float BlockHold(CeremonyPace pace) => pace == CeremonyPace.Quick ? 2.1f : 3.6f;

        // ------------------------------------------------------------ the live eviction

        /// <summary>The card's title and the block, before the first vote.</summary>
        public static float VoteIntro(CeremonyPace pace) => pace == CeremonyPace.Quick ? 0.85f : 2.2f;

        /// <summary>The time each vote holds; shorter the more votes there are.</summary>
        public static float PerVote(CeremonyPace pace, int votes) =>
            pace == CeremonyPace.Quick ? 0.45f : votes >= 6 ? 1.4f : 1.8f;

        /// <summary>The extra wait before the house's last vote.</summary>
        public static float LastVoteBeat(CeremonyPace pace) => pace == CeremonyPace.Quick ? 0f : 1.6f;

        /// <summary>A tied vote announced, before the Head of Household breaks it.</summary>
        public static float TieBeat(CeremonyPace pace) => pace == CeremonyPace.Quick ? 1.2f : 2.4f;

        /// <summary>How long the result holds once it is read.</summary>
        public static float ResultHold(CeremonyPace pace) => pace == CeremonyPace.Quick ? 1.9f : 3.6f;
    }
}
