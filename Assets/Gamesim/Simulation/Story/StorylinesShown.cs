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
    /// planned backdoor gives. A storyline that has not reached the player stays off the list until it does. Every view
    /// of the running storylines reads it here: the two lists (<see cref="Rows"/>), the free-time board's THREADS strip,
    /// which counted the quiet backdoor as "1 storyline playing out" (<see cref="StripLine"/>), and the THREADS &amp;
    /// STORYLINES card's "nothing in play" line, which the quiet backdoor took away from an empty card (<see cref="InPlay"/>).
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

        /// <summary>Whether a card of <paramref name="cycle"/> waits on the player, unanswered.</summary>
        public static bool Waiting(EpisodeState s, StorylineState cycle) =>
            s?.houseEvents != null && cycle != null && s.houseEvents.Any(e => e != null && !e.resolved && e.cycleId == cycle.id);

        /// <summary>
        /// The rows the STORYLINES block and the Storylines tab draw for <see cref="Running"/>, in its order: the arc's title, and
        /// "waiting on you" while a card of it is unanswered, "still playing out" after.
        /// </summary>
        public static List<(string Title, string Line)> Rows(EpisodeState s) =>
            Running(s).Select(cycle => (StoryCatalog.Find(cycle.templateId)?.title ?? cycle.title,
                Waiting(s, cycle) ? "waiting on you" : "still playing out")).ToList();

        /// <summary>The line the story's views draw when they have nothing to say.</summary>
        public const string NothingInPlay = "Nothing in play yet. Threads start from what you do.";

        /// <summary>
        /// The free-time board's THREADS strip, in its few words: the threads still going, how many of <see cref="Running"/> play
        /// out with no card waiting (a waiting card is on the board itself), and what the stories left the player, each with the
        /// weeks it has left; <see cref="NothingInPlay"/> when there is none of them. A storyline that has not reached the player
        /// is not counted, so a quiet backdoor's week reads as any other.
        /// </summary>
        public static string StripLine(EpisodeState s)
        {
            var parts = EpisodeEngine.Threads(s).Where(thread => thread.ending == null).Select(thread => thread.label).ToList();
            int playing = Running(s).Count(x => !Waiting(s, x));
            if (playing > 0) parts.Add(playing + (playing == 1 ? " storyline playing out" : " storylines playing out"));
            foreach (var modifier in Modifiers(s))
                parts.Add(modifier.name + " (" + modifier.weeksLeft + (modifier.weeksLeft == 1 ? " week left)" : " weeks left)"));
            return parts.Count > 0 ? string.Join("  ·  ", parts) : NothingInPlay;
        }

        /// <summary>
        /// Whether the THREADS &amp; STORYLINES card has anything to list - a play or a thread still going, a storyline of
        /// <see cref="Running"/>, or a modifier of the player's - so that it draws <see cref="NothingInPlay"/> exactly when its
        /// blocks draw nothing.
        /// </summary>
        public static bool InPlay(EpisodeState s) =>
            EpisodeEngine.Plays(s).Any(p => p.ending == null) || EpisodeEngine.Threads(s).Any(t => t.ending == null)
            || Running(s).Count > 0 || Modifiers(s).Any();

        /// <summary>The stories' modifiers still running on the player (no owner), in the state's order.</summary>
        private static IEnumerable<StoryModifierState> Modifiers(EpisodeState s) =>
            s?.activeModifiers == null ? Enumerable.Empty<StoryModifierState>()
                : s.activeModifiers.Where(m => m != null && m.weeksLeft > 0 && string.IsNullOrEmpty(m.ownerId));
    }
}
