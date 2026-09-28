using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The season's record of chances and reads, kept for the verdict at the end and for the vote
    /// read along the way (STRATEGY-LOOP-PLAN.md §6). Every list is bounded like the event log,
    /// oldest dropped and counted, and a row is only ever appended: the ledger says what happened,
    /// never what should have.
    ///
    /// <para>Two sides write it. This side writes reads, claims, ballots, competitions, power and
    /// alliances; the story side writes a play's offer, steps and outcome as an opportunity row.</para>
    /// </summary>
    [Serializable]
    public sealed class SeasonLedger
    {
        /// <summary>Rows kept per kind. A twelve-week season writes a few dozen of each.</summary>
        public const int MostRows = 512;

        public List<OpportunityRow> opportunities = new List<OpportunityRow>();
        public List<CompetitionRow> competitions = new List<CompetitionRow>();
        public List<PowerRow> power = new List<PowerRow>();
        public List<BallotRow> ballots = new List<BallotRow>();
        public List<ClaimRow> claims = new List<ClaimRow>();
        public List<AllianceRow> alliances = new List<AllianceRow>();
        public List<StandingRow> standings = new List<StandingRow>();
        /// <summary>Rows the caps dropped, oldest first, so a long season still says how much it forgot.</summary>
        public int dropped;

        public SeasonLedger Clone()
        {
            var copy = (SeasonLedger)MemberwiseClone();
            copy.opportunities = opportunities.Select(x => x.Clone()).ToList();
            copy.competitions = competitions.Select(x => x.Clone()).ToList();
            copy.power = power.Select(x => x.Clone()).ToList();
            copy.ballots = ballots.Select(x => x.Clone()).ToList();
            copy.claims = claims.Select(x => x.Clone()).ToList();
            copy.alliances = alliances.Select(x => x.Clone()).ToList();
            copy.standings = standings.Select(x => x.Clone()).ToList();
            return copy;
        }

        /// <summary>Appends a row, dropping the oldest of its kind past the cap and counting the loss.</summary>
        internal static void Append<T>(SeasonLedger ledger, List<T> rows, T row)
        {
            rows.Add(row);
            if (rows.Count <= MostRows) return;
            rows.RemoveAt(0);
            ledger.dropped++;
        }
    }

    /// <summary>A chance offered to the player: a play, a window, a deal, a vote, a competition, a read.</summary>
    [Serializable]
    public sealed class OpportunityRow
    {
        public string id, kind, anchor, source, response, outcome, currency, note;
        public int week;
        public double payoff;
        public List<OpportunityStep> steps = new List<OpportunityStep>();
        public OpportunityRow Clone()
        {
            var copy = (OpportunityRow)MemberwiseClone();
            copy.steps = steps.Select(x => x.Clone()).ToList();
            return copy;
        }
    }

    /// <summary>One step of a chance: what was chosen, and whether it was the option the read favoured.</summary>
    [Serializable]
    public sealed class OpportunityStep
    {
        public string at, choice;
        public bool matchedRead;
        public OpportunityStep Clone() => (OpportunityStep)MemberwiseClone();
    }

    /// <summary>One competition the player was in: how it was entered, where they placed, and the odds they had.</summary>
    [Serializable]
    public sealed class CompetitionRow
    {
        public int week, field, placement;
        public string kind, entry;
        public double performance, expectedWin;
        public CompetitionRow Clone() => (CompetitionRow)MemberwiseClone();
    }

    /// <summary>One week's power: who held it, how the veto went, who went out and by what tally.</summary>
    [Serializable]
    public sealed class PowerRow
    {
        public int week;
        public string hohId, vetoHolderId, savedId, replacementId, evicteeId, backdoorTargetId, backdoorResult;
        public bool vetoUsed;
        public List<string> nominees = new List<string>();
        public List<int> tally = new List<int>();
        public PowerRow Clone()
        {
            var copy = (PowerRow)MemberwiseClone();
            copy.nominees = new List<string>(nominees);
            copy.tally = new List<int>(tally);
            return copy;
        }
    }

    /// <summary>The player's own eviction ballot, and the read it was cast on: who the whip count said would go.</summary>
    [Serializable]
    public sealed class BallotRow
    {
        public int week;
        public string voterId, targetId, readBefore;
        public bool correct;
        public BallotRow Clone() => (BallotRow)MemberwiseClone();
    }

    /// <summary>
    /// What somebody said their vote was, or what was overheard of it, before the reveal judged it.
    /// A claim is the read's direct evidence and the reveal's receipt: kept or lied.
    /// </summary>
    [Serializable]
    public sealed class ClaimRow
    {
        public int week;
        public string voterId, targetId, source, status = ClaimStatus.Open;
        public ClaimRow Clone() => (ClaimRow)MemberwiseClone();
    }

    /// <summary>An alliance's life, for the verdict: when it began, when it ended and why.</summary>
    [Serializable]
    public sealed class AllianceRow
    {
        public string id, why;
        public int startedWeek, endedWeek;
        public AllianceRow Clone() => (AllianceRow)MemberwiseClone();
    }

    /// <summary>
    /// A standing the player learned: how <c>fromId</c> stood toward <c>toId</c> when it was learned.
    /// Their view of the player from a read; a pair's from an overheard conversation or an account.
    /// </summary>
    [Serializable]
    public sealed class StandingRow
    {
        public int week;
        public string fromId, toId, source;
        public double score;
        public StandingRow Clone() => (StandingRow)MemberwiseClone();
    }

    /// <summary>Where a claim or a standing came from.</summary>
    public static class ClaimSource
    {
        public const string Told = "told", Overheard = "overheard", Ally = "ally", Read = "read";
        public static readonly string[] All = { Told, Overheard, Ally, Read };
        public static bool IsKnown(string source) => source != null && Array.IndexOf(All, source) >= 0;
    }

    /// <summary>What the reveal made of a claim.</summary>
    public static class ClaimStatus
    {
        public const string Open = "open", Kept = "kept", Lied = "lied";
        public static readonly string[] All = { Open, Kept, Lied };
        public static bool IsKnown(string status) => status != null && Array.IndexOf(All, status) >= 0;
    }

    public static class OpportunityKinds
    {
        public const string Play = "play", Lobby = "lobby", Plea = "plea", Deal = "deal", Alliance = "alliance",
            Vote = "vote", Comp = "comp", Read = "read";
        public static readonly string[] All = { Play, Lobby, Plea, Deal, Alliance, Vote, Comp, Read };
        public static bool IsKnown(string kind) => kind != null && Array.IndexOf(All, kind) >= 0;
    }

    public static class OpportunityResponse
    {
        public const string Taken = "taken", Declined = "declined", Ignored = "ignored", Expired = "expired";
        public static readonly string[] All = { Taken, Declined, Ignored, Expired };
        public static bool IsKnown(string response) => response != null && Array.IndexOf(All, response) >= 0;
    }

    public static class OpportunityOutcome
    {
        public const string Won = "won", Part = "part", Lost = "lost", NotApplicable = "na";
        public static readonly string[] All = { Won, Part, Lost, NotApplicable };
        public static bool IsKnown(string outcome) => outcome != null && Array.IndexOf(All, outcome) >= 0;
    }

    public static class PayoffCurrency
    {
        public const string Trust = "trust", Intel = "intel", Alliance = "alliance", Power = "power";
        public static readonly string[] All = { Trust, Intel, Alliance, Power };
        public static bool IsKnown(string currency) => currency != null && Array.IndexOf(All, currency) >= 0;
    }

    public static class CompetitionEntry
    {
        public const string Played = "played", Assist = "assist", Simulated = "simulated", Thrown = "thrown", Watched = "watched";
        public static readonly string[] All = { Played, Assist, Simulated, Thrown, Watched };
        public static bool IsKnown(string entry) => entry != null && Array.IndexOf(All, entry) >= 0;
    }
}
