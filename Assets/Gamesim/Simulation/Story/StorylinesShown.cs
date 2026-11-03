using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The running storylines the player may be told are running (ROLE-MOMENTS-PLAN §3.4's "still a tell"; the owner's
    /// quick fixes, 2026-11-03, "the backdoor leak"): the free-time panel's STORYLINES block and the Storylines tab list
    /// a storyline only once one of its beats has reached the player. A backdoor runs in every backdoor week, so listing
    /// every running storyline put "The Backdoor - still playing out" in front of a player who got no card, which only a
    /// planned backdoor gives. A storyline that has not reached the player stays off the list until it does.
    ///
    /// <para>A beat reaches the player as a card or a scene drawn for them - a house event of the storyline's own,
    /// waiting, answered or lapsed - or as a line of it they can read: a step's log line whose audience is everybody or
    /// names them. An NPC's beat is theirs (its line reaches only the houseguests in it), so it reaches nobody else. Every
    /// season reads it the same way: it is display, read from the state, and changes nothing.</para>
    /// </summary>
    public static class StorylinesShown
    {
        /// <summary>Whether a beat of <paramref name="cycle"/> has reached the player (a card, a scene, a line).</summary>
        public static bool Shown(EpisodeState s, StorylineState cycle)
        {
            if (s == null || cycle == null || string.IsNullOrEmpty(cycle.id)) return false;
            if (s.houseEvents != null && s.houseEvents.Any(e => e != null && e.cycleId == cycle.id)) return true;
            if (cycle.path == null || s.events == null) return false;
            foreach (var step in cycle.path)
            {
                if (step == null || step.logSequence <= 0) continue;
                var line = s.events.FirstOrDefault(e => e != null && e.sequence == step.logSequence);
                if (line != null && (line.audienceIds == null || line.audienceIds.Count == 0 || line.audienceIds.Contains(s.playerId))) return true;
            }
            return false;
        }

        /// <summary>
        /// The storylines the two views list as running, in their order (week, then id): every running story cycle that is
        /// not a play or a thread - those have blocks of their own - and that has reached the player.
        /// </summary>
        public static List<StorylineState> Running(EpisodeState s)
        {
            if (s?.storylines == null) return new List<StorylineState>();
            return s.storylines.Where(x => x != null && x.beatId != null && StorylineStatus.Running(x.status)
                                           && StoryCatalog.Find(x.templateId)?.play == null && x.lane != StoryLanes.Thread
                                           && Shown(s, x))
                .OrderBy(x => x.week).ThenBy(x => x.id, StringComparer.Ordinal).ToList();
        }
    }
}
