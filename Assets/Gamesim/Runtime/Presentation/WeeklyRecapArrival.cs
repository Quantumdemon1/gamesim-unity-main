using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The week a queued weekly recap belongs to, and whether it is still due when the eviction's
    /// beats have finished.
    ///
    /// <para>The recap waits for the vote reveal, the walk-out and any staged ceremony before it
    /// opens. It used to be keyed to the season's revision at the moment it was queued, and
    /// anything that moved the revision while the eviction played dropped it for good: opening the
    /// notebook commits the house's due NPC tick, and so does a save. The player had pressed
    /// nothing, and the week ended with no recap at all.</para>
    ///
    /// <para>It is keyed to the week instead: the same season, still in the week whose eviction
    /// queued it, and no decision of the player's accepted since. A decision taken under the
    /// reveal still drops it, as it always did: a recap that arrives after the next decision is an
    /// interruption, not a summary. A load of the same season in the same week keeps it; a new
    /// season, another season's save or a week that has turned does not.</para>
    ///
    /// <para>Nothing here is saved and nothing reads the season's generator.</para>
    /// </summary>
    public readonly struct WeeklyRecapArrival
    {
        /// <summary>The season the recap was queued in.</summary>
        public readonly string SessionId;
        /// <summary>The week the recap summarises: the committed week the eviction left the season in.</summary>
        public readonly int Week;
        /// <summary>How many of the player's decisions the director had accepted when it was queued.</summary>
        public readonly int Decisions;

        public WeeklyRecapArrival(string sessionId, int week, int decisions)
        {
            SessionId = sessionId;
            Week = week;
            Decisions = decisions;
        }

        /// <summary>The recap for the week the committed season is in, queued now.</summary>
        public static WeeklyRecapArrival For(EpisodeState committed, int decisions) =>
            committed == null ? default : new WeeklyRecapArrival(committed.sessionId, committed.week, decisions);

        /// <summary>
        /// Whether the recap still opens over <paramref name="committed"/>, with
        /// <paramref name="decisionsNow"/> of the player's decisions accepted. The revision is not
        /// asked: the house's own ticks move it while nobody presses anything.
        /// </summary>
        public bool StillDue(EpisodeState committed, int decisionsNow) =>
            committed != null && Week >= 1
            && committed.sessionId == SessionId
            && committed.week == Week
            && decisionsNow == Decisions;
    }
}
