namespace Gamesim.Simulation
{
    /// <summary>
    /// Who broke a commitment (ACTIONS-DEALS-ALLIANCES-PLAN C0, X3), for every reader that holds a
    /// breach against somebody: a houseguest's warmth (<see cref="NpcDeals.Adjusted"/>), the
    /// acceptance roll and its line (<see cref="PlayerDeals"/>), the odds the player is shown
    /// (<see cref="KnownOdds"/>), threat (<see cref="ThreatAssessment"/>), the jury
    /// (<see cref="WebJuryVoting.Obligations"/>), Game Sense, the jury read
    /// (<see cref="JuryHouseRead"/>) and the story's odds (<see cref="StoryOdds"/>).
    ///
    /// <para>Before the commitment rules every one of them counted a breach against both sides of
    /// it - the houseguest wronged read as colder, less trustworthy and more of a threat for having
    /// been wronged - and a season that plays without them still does, so its outcomes do not
    /// move. Under them a breach counts against whoever broke it (<see cref="CountsAgainst(EpisodeState, DealState, string)"/>).</para>
    ///
    /// <para>Pure and read-only: it neither mutates the state nor draws from its generator.</para>
    /// </summary>
    public static class Breaches
    {
        /// <summary>
        /// Who broke a broken deal: the settlement's record where it names somebody (schema 22); nobody
        /// where it settled under the commitment rules naming nobody - a voting bloc, which both
        /// parties settle at once, or a vote deal both of them broke (C1); and for a deal settled
        /// before the record existed,
        /// <see cref="FinalistRead.DealBreaker"/>'s rule, which reads the public record and the
        /// ballots the player knows, and names a breaker only for a deal the player was party to.
        /// Null for a deal that is not broken.
        /// </summary>
        public static string DealBreaker(EpisodeState s, DealState deal)
        {
            if (s == null || deal == null || deal.status != DealStatus.Broken) return null;
            if (!string.IsNullOrEmpty(deal.brokenById)) return deal.brokenById;
            if (deal.settledWeek > 0) return null;
            return FinalistRead.DealBreaker(s, deal);
        }

        /// <summary>
        /// Whether this houseguest broke this deal: its breaker, or either side of a deal both of them
        /// broke at once (<see cref="BrokenByBoth"/>) - each voted their own way, so each walked away
        /// from it.
        /// </summary>
        public static bool Broke(EpisodeState s, DealState deal, string whoId) =>
            deal != null && deal.status == DealStatus.Broken && !string.IsNullOrEmpty(whoId)
            && (deal.proposerId == whoId || deal.recipientId == whoId)
            && (BrokenByBoth(deal) || DealBreaker(s, deal) == whoId);

        /// <summary>
        /// Whether a broken deal was broken by both of its parties at once: a voting bloc that fell
        /// apart, and - under the commitment rules, on the record (ACTIONS-DEALS-ALLIANCES-PLAN C1) - a
        /// vote deal both of them voted against, which its settlement names nobody for. Before the
        /// rules such a vote deal named the first of the two as its breaker, and the second as the one
        /// wronged.
        /// </summary>
        public static bool BrokenByBoth(DealState deal) =>
            deal != null && deal.status == DealStatus.Broken
            && (deal.type == DealKind.VoteTogether
                || (deal.settledWeek > 0 && string.IsNullOrEmpty(deal.brokenById)
                    && (deal.type == DealKind.VoteSave || deal.type == DealKind.VoteEvict)));

        /// <summary>
        /// Whether a broken deal counts against this houseguest when anybody sizes them up: under the
        /// commitment rules only if they broke it; before them, as every reader always read it, if
        /// they were either side of it.
        /// </summary>
        public static bool CountsAgainst(EpisodeState s, DealState deal, string whoId) =>
            EpisodeEngine.CommitmentRulesOn(s)
                ? Broke(s, deal, whoId)
                : deal != null && deal.status == DealStatus.Broken && (deal.proposerId == whoId || deal.recipientId == whoId);

        /// <summary>
        /// Who broke a broken promise: the record where it names them (schema 22), else the one who
        /// made it - a promise is settled only by its maker's act (a nomination, a ballot, the final
        /// choice), so this is no guess. Null for a promise that is not broken.
        /// </summary>
        public static string PromiseBreaker(PromiseState promise) =>
            promise == null || promise.status != PromiseStatus.Broken ? null
            : !string.IsNullOrEmpty(promise.brokenById) ? promise.brokenById : promise.fromId;

        /// <summary>
        /// Whether a broken promise counts against this houseguest: under the commitment rules if they
        /// broke it; before them, as every reader always read it, if they were either side of it.
        /// </summary>
        public static bool CountsAgainst(EpisodeState s, PromiseState promise, string whoId) =>
            EpisodeEngine.CommitmentRulesOn(s)
                ? PromiseBreaker(promise) == whoId
                : promise != null && promise.status == PromiseStatus.Broken && (promise.fromId == whoId || promise.toId == whoId);

        /// <summary>
        /// Whether a broken deal broke after <paramref name="week"/>: by the week its settlement
        /// recorded (schema 22), and for one settled before that record, by the rule the jury read
        /// always used - made after the week, or a Final 2 agreement, which only the final eviction
        /// breaks.
        /// </summary>
        public static bool BrokeAfter(DealState deal, int week) =>
            deal != null && (deal.settledWeek > 0 ? deal.settledWeek > week : deal.week > week || deal.type == DealKind.FinalTwo);
    }
}
