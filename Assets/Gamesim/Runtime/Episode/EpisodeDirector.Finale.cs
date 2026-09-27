using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The finale on screen: the jury's vote read one juror at a time (<see cref="JuryReveal"/>).
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        private JuryReveal juryReveal;

        /// <summary>Whether the jury's vote is being read.</summary>
        private bool JuryRevealPlaying => juryReveal != null && juryReveal.IsPlaying;

        /// <summary>The two finalists, in cast order, with their faces: the order the engine counts them in.</summary>
        private List<JuryReveal.Finalist> JuryFinalists(EpisodeState state) =>
            state?.contestants == null ? new List<JuryReveal.Finalist>()
                : state.contestants.Where(c => c.status == ContestantStatus.Winner || c.status == ContestantStatus.RunnerUp)
                    .Select(c => new JuryReveal.Finalist(c.id, c.name, CharacterPortraits.Get(c), c, c.isPlayer)).ToList();

        /// <summary>
        /// The jury's committed ballots, in the order they are read: dealt from the seed, the same way
        /// every time for the same season, as the keys are. A juror who is the player reads as "You".
        /// </summary>
        private List<JuryReveal.Juror> JuryVotes(EpisodeState state)
        {
            var ballots = new List<JuryReveal.Juror>();
            if (state?.votes == null) return ballots;
            var finalists = new HashSet<string>(state.contestants
                .Where(c => c.status == ContestantStatus.Winner || c.status == ContestantStatus.RunnerUp).Select(c => c.id));
            var cast = state.votes.Where(v => finalists.Contains(v.targetId) && !finalists.Contains(v.voterId))
                .GroupBy(v => v.voterId).Select(g => g.First()).ToDictionary(v => v.voterId);
            foreach (var id in JuryOrder(cast.Keys, state.seed))
            {
                var juror = state.Find(id);
                if (juror == null) continue;
                ballots.Add(new JuryReveal.Juror(juror.id, juror.isPlayer ? "You" : juror.name, cast[id].targetId,
                    CharacterPortraits.Get(juror), juror));
            }
            return ballots;
        }

        /// <summary>
        /// The order the jury is read in: shuffled from the seed and nothing else, so a reload reads it
        /// the same way. The engine records the ballots in cast order, and reading them that way would
        /// read the player's own first whenever they sat on the jury.
        /// </summary>
        public static List<string> JuryOrder(IEnumerable<string> ids, uint seed) =>
            (ids ?? Enumerable.Empty<string>())
                .Select((id, index) => (id, index, rank: JuryRank(seed, id)))
                .OrderBy(entry => entry.rank).ThenBy(entry => entry.index)
                .Select(entry => entry.id).ToList();

        private static uint JuryRank(uint seed, string id)
        {
            unchecked
            {
                uint hash = 2166136261u;
                void Mix(uint value)
                {
                    for (int shift = 0; shift < 32; shift += 8) { hash ^= (value >> shift) & 0xFF; hash *= 16777619u; }
                }
                Mix(seed); Mix(0x6A757279u); // "jury"
                foreach (char c in id ?? string.Empty) Mix(c);
                return hash;
            }
        }
    }
}
