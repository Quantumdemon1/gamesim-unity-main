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
    ///
    /// <para>Unity-free, so the subset holds the numbers itself.</para>
    /// </summary>
    public static class CeremonyPacing
    {
        /// <summary>How much faster a reveal plays while the player has asked it to speed up.</summary>
        public const float SpeedUp = 3f;

        /// <summary>
        /// The speed a reveal starts at, from the one the player last left a reveal at in this session
        /// (UI-UX plan D2): sped up stays sped up, anything else is the reveal's own pace. The director
        /// keeps the last one in a field of its own and hands it to each Play; nothing keeps it longer
        /// than the session, and nothing saves it.
        /// </summary>
        public static float SessionSpeed(float lastSpeed) => lastSpeed > 1f ? SpeedUp : 1f;

        public const float FadeIn = 0.30f;
        public const float FadeOut = 0.45f;

        // ------------------------------------------------------------ the ceremony card

        /// <summary>What the generic ceremony card was written to hold, before its reading time is counted.</summary>
        public const float CardAuthoredHold = 2.6f;

        /// <summary>
        /// How long the generic ceremony card holds between its fades: its authored 2.6 seconds, or
        /// the time to read its title and its line, whichever is longer, at the caption setting's
        /// multiplier - as the ceremony strip holds (CaptionTiming). A long line stays up long enough
        /// to read; a short one holds as it always did.
        /// </summary>
        public static float CardHold(string title, string line, CaptionTime time) =>
            (float)CaptionTiming.Hold(CardAuthoredHold, (title ?? string.Empty) + " " + (line ?? string.Empty), time);

        // ------------------------------------------------------------ the veto draw

        /// <summary>The beat with every chip in the bag, before the first comes out.</summary>
        public static float DrawIntro(CeremonyPace pace) => pace == CeremonyPace.Quick ? 0.35f : 0.70f;

        /// <summary>How long each chip holds the stage: out of the bag, the slot lit, the face turned over.</summary>
        public static float PerChip(CeremonyPace pace) => pace == CeremonyPace.Quick ? 0.45f : 0.90f;

        /// <summary>How long a chip takes to leave the bag, and then its face to turn over.</summary>
        public static float ChipTurn(CeremonyPace pace) => pace == CeremonyPace.Quick ? 0.20f : 0.30f;

        /// <summary>How long the whole field holds once every chip is out.</summary>
        public static float FieldHold(CeremonyPace pace) => pace == CeremonyPace.Quick ? 1.6f : 2.6f;

        /// <summary>The whole draw at its own pace, fade to fade, when nobody speeds it up or skips it.</summary>
        public static float VetoDraw(CeremonyPace pace, int chips) =>
            FadeIn + DrawIntro(pace) + System.Math.Max(0, chips) * PerChip(pace) + FieldHold(pace) + FadeOut;

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

        // ------------------------------------------------------------ the jury's vote

        /// <summary>The finalists and how many votes win it, before the first juror's vote is read.</summary>
        public static float JuryIntro(CeremonyPace pace) => pace == CeremonyPace.Quick ? 1.0f : 2.6f;

        /// <summary>
        /// The time each juror's vote holds; shorter for a big jury. The reference reads one every
        /// two and a half seconds whatever the size, which a sixteen-houseguest season's fourteen
        /// jurors would make a thirty-five-second wait.
        /// </summary>
        public static float PerJuror(CeremonyPace pace, int jurors) =>
            pace == CeremonyPace.Quick ? 0.55f : jurors >= 8 ? 1.6f : 2.2f;

        /// <summary>
        /// The extra wait before a vote that could decide it: whenever a finalist is a vote from
        /// winning, whoever the next vote names. Held only where it could, never only where it does,
        /// so the pause itself gives nothing away.
        /// </summary>
        public static float DecidingBeat(CeremonyPace pace) => pace == CeremonyPace.Quick ? 0.3f : 2.0f;

        /// <summary>How long the winner holds the stage: the confetti's three seconds and time to read the count.</summary>
        public static float WinnerHold(CeremonyPace pace) => pace == CeremonyPace.Quick ? 3.2f : 5.2f;

        // ------------------------------------------------------------ the veto meeting

        /// <summary>The meeting's first page: the holder and the block, before the question.</summary>
        public static float VetoIntro(CeremonyPace pace) => pace == CeremonyPace.Quick ? 1.5f : 3.0f;

        /// <summary>
        /// The question before the decision, while the house cuts to each nominee in turn and then
        /// pushes in on the two of them: the meeting's beat before the last vote.
        /// </summary>
        public static float VetoQuestion(CeremonyPace pace) => pace == CeremonyPace.Quick ? 1.7f : 3.4f;

        /// <summary>The decision on the screen and the face it is about.</summary>
        public static float VetoDecision(CeremonyPace pace) => pace == CeremonyPace.Quick ? 1.6f : 3.2f;

        /// <summary>The Head of Household naming the replacement, when the veto was used.</summary>
        public static float VetoReplacement(CeremonyPace pace) => pace == CeremonyPace.Quick ? 1.6f : 3.2f;

        /// <summary>The block that goes to the vote, before the card fades.</summary>
        public static float VetoFinal(CeremonyPace pace) => pace == CeremonyPace.Quick ? 1.3f : 2.6f;

        /// <summary>
        /// The whole meeting at its own pace, fade to fade: about sixteen seconds when the veto is
        /// used and thirteen when it is not, suspensefully, and half that quickly.
        /// </summary>
        public static float VetoMeeting(CeremonyPace pace, bool used) =>
            FadeIn + VetoIntro(pace) + VetoQuestion(pace) + VetoDecision(pace) + (used ? VetoReplacement(pace) : 0f)
            + VetoFinal(pace) + FadeOut;
    }
}
