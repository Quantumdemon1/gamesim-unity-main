using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The finale page set aside while the cast screen is up, and whether backing out of the cast
    /// screen brings it back.
    ///
    /// <para>"Start a new season" on the finale page opened the cast screen over the page and left
    /// the page open under it: a panel by every reckoning, drawn and holding the house, behind a
    /// screen that had replaced it. The page now closes as the cast screen opens. Backing out
    /// without starting puts the page back as it was - the same season's finale, with the juror
    /// whose reason was open still open - and a season started there leaves nothing of it.</para>
    ///
    /// <para>It comes back only over the season it was set aside from, still finished: another
    /// season installed meanwhile (a start, a load) gets no finale page of the old one. Nothing
    /// here is saved and nothing reads the season's generator.</para>
    /// </summary>
    public readonly struct FinaleAside
    {
        /// <summary>Whether a finale page was set aside. The default is none.</summary>
        public readonly bool IsSet;
        /// <summary>The season whose finale page it was.</summary>
        public readonly string SessionId;
        /// <summary>The juror whose reason was open on the page, or null.</summary>
        public readonly string PressedJurorId;

        private FinaleAside(string sessionId, string pressedJurorId)
        {
            IsSet = true;
            SessionId = sessionId;
            PressedJurorId = pressedJurorId;
        }

        /// <summary>
        /// The page set aside now, from the committed season, when the finished season's page is
        /// the panel on screen (<paramref name="pageIsThePanel"/>); none otherwise - from the main
        /// menu, the settings or the house, the cast screen goes back where it always went.
        /// </summary>
        public static FinaleAside For(EpisodeState committed, bool pageIsThePanel, string pressedJurorId) =>
            pageIsThePanel && committed != null && committed.phase == EpisodePhase.Finished
                ? new FinaleAside(committed.sessionId, pressedJurorId) : default;

        /// <summary>Whether backing out of the cast screen puts the page back over <paramref name="committed"/>.</summary>
        public bool ReturnsOver(EpisodeState committed) =>
            IsSet && committed != null && committed.phase == EpisodePhase.Finished && committed.sessionId == SessionId;
    }
}
