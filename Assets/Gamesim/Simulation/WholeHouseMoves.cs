using System;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>Whether a whole-house move can be made now, and if not, which of the two reasons stops it.</summary>
    public enum HouseMoveLock
    {
        /// <summary>The engine would take the move now.</summary>
        Open,
        /// <summary>The right part of the week, but the window's actions are spent.</summary>
        NoActionsLeft,
        /// <summary>Not a part of the week the move belongs to (or nobody to make it with).</summary>
        NotNow,
    }

    /// <summary>
    /// The engine's own rule for the moves that name nobody - a house meeting (rallying the house or
    /// airing the laundry) and listening in - read before the move is pressed, so a screen can draw
    /// the move locked with the true reason rather than let it fail on the status line. A read only:
    /// it draws nothing from the season's stream and changes nothing.
    /// <para>It follows the engine's checks in the engine's order: the phase (free time or the
    /// campaign: <c>HouseMeeting</c>, and <c>ConversationWindowRefusal</c> for listening in), an
    /// active player, the window's actions, and somebody to call together or to overhear.</para>
    /// </summary>
    public static class WholeHouseMoves
    {
        /// <summary>Whether <paramref name="kind"/> - a house meeting or listening in - would be taken now.</summary>
        public static HouseMoveLock Lock(EpisodeState s, EpisodeCommandKind kind)
        {
            if (kind != EpisodeCommandKind.HouseMeeting && kind != EpisodeCommandKind.Eavesdrop)
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only the whole-house moves are read here.");
            if (s == null || (s.phase != EpisodePhase.Social && s.phase != EpisodePhase.Campaign)) return HouseMoveLock.NotNow;
            if (s.Find(s.playerId)?.status != ContestantStatus.Active) return HouseMoveLock.NotNow;
            if (EpisodeEngine.SocialActionsSpent(s) >= EpisodeEngine.SocialActionBudget(s)) return HouseMoveLock.NoActionsLeft;
            int others = s.Active.Count(c => !c.isPlayer);
            return others >= (kind == EpisodeCommandKind.Eavesdrop ? 2 : 1) ? HouseMoveLock.Open : HouseMoveLock.NotNow;
        }
    }
}
