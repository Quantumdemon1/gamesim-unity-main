using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The objective's title in words (UI-UX plan U1): the one line at the top left that says what the
    /// week wants of the player now, "Name your nominees", "The HoH is naming nominees", "Your vote is
    /// in", in place of the "Current Objective" every regular week used to show. Unity-free, so the
    /// subset pins every phrase beside the read it is drawn from.
    ///
    /// <para>The order is the contract. A player who is not active watches, first, because an evicted
    /// id can linger in a role. Then the windows where a phase's work is done and the house waits for
    /// the next Advance (the phase stays until then), so the card never asks for a task already
    /// settled. Then the open task of the phase, by the player's role.</para>
    ///
    /// <para>The endgame's titles are not here: <c>EpisodeHud.ObjectiveTitle</c> names the Final 3,
    /// the Final 2 and the finished season before it asks this table, and one of those is longer than
    /// <see cref="MaxLength"/>.</para>
    /// </summary>
    public static class ObjectiveWords
    {
        /// <summary>The longest phrase the objective chip's title box holds at every text size.</summary>
        public const int MaxLength = 28;

        /// <summary>The title when no phrase applies: no state, or a phase this table does not name.</summary>
        public const string Fallback = "Current Objective";

        /// <summary>What the week wants of the player now, in at most <see cref="MaxLength"/> characters.</summary>
        public static string For(EpisodeState s)
        {
            if (s == null) return Fallback;
            string me = s.playerId;
            if (s.Find(me)?.status != ContestantStatus.Active) return "Watch the week unfold";

            bool hoh = s.hohId == me;
            bool nominee = s.nominees != null && s.nominees.Contains(me);
            bool holder = s.vetoHolderId == me;

            // The work is done and the phase waits for the next Advance.
            switch (s.phase)
            {
                case EpisodePhase.HoH when s.competitionResolved:
                    return hoh ? "You are Head of Household" : "The new HoH is crowned";
                case EpisodePhase.Veto when s.competitionResolved:
                    return holder ? "You hold the Power of Veto" : "The veto is won";
                case EpisodePhase.Nomination when s.nominees != null && s.nominees.Count == 2:
                    return nominee ? "You are nominated" : "Nominees are named";
                case EpisodePhase.VetoMeeting when s.vetoResolved:
                    return "The veto meeting is over";
                case EpisodePhase.Eviction when s.evictionResolved || s.evictionStage == EvictionStage.Results:
                    return "The house has voted";
                case EpisodePhase.Eviction when s.votes != null && s.votes.Any(v => v.voterId == me):
                    return "Your vote is in";
                case EpisodePhase.Eviction when s.evictionStage == EvictionStage.Tiebreaker && hoh:
                    return "Break the tie";
            }

            // The task still open.
            switch (s.phase)
            {
                case EpisodePhase.Social:
                    return "Make this week's moves";
                case EpisodePhase.HoH:
                    return s.previousHohId == me ? "Watch the HoH competition" : "Win Head of Household";
                case EpisodePhase.Nomination:
                    return hoh ? "Name your nominees" : "The HoH is naming nominees";
                case EpisodePhase.VetoSelection:
                    return "The veto players are drawn";
                case EpisodePhase.Veto:
                    return s.vetoPlayers != null && s.vetoPlayers.Contains(me) ? "Win the Power of Veto" : "Watch the veto competition";
                case EpisodePhase.VetoMeeting:
                    if (holder) return "Decide on the veto";
                    // A housemate's veto that saves a nominee leaves the HoH to name who goes up instead: the
                    // meeting waits on the player then (ruling L1a). NpcVetoSave is a pure read, no draw.
                    return hoh && !string.IsNullOrEmpty(s.vetoHolderId) && s.nominees != null && EpisodeEngine.NpcVetoSave(s) != null
                        ? "Name a replacement nominee" : "The veto holder decides";
                case EpisodePhase.Campaign:
                    return nominee ? "Campaign to stay" : hoh ? "Hear the campaign" : "Choose your vote";
                case EpisodePhase.Eviction:
                    return nominee ? "Face the house vote" : hoh ? "Break a tie if needed" : "Cast your eviction vote";
                default:
                    return Fallback;
            }
        }
    }
}
