using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The names a pact of the player's may be given (ACTIONS-DEALS-ALLIANCES-PLAN C5, "Rename {pact}"):
    /// a preset list, never free text, so nothing the player types becomes a line the whole pact reads.
    ///
    /// <para>Where pact names come from today, in the order offered:</para>
    /// <list type="bullet">
    /// <item>the name the house would give these people now, by its own rule: "The Riley Pact" for one
    /// partner (the player's pact, <c>FormPact</c> and the invitation's), "The Riley and Jo Pact" for two
    /// (<see cref="NpcAlliances"/>' pairs), "The Riley, Jo and Sam Pact" for more (a story's pact,
    /// <see cref="NpcAlliances.FormFromStory"/>) - the first names of the members still in it, the player
    /// left out, in the pact's own order, founder first;</item>
    /// <item>and the web build's own names for a pact (<see cref="Web"/>).</item>
    /// </list>
    ///
    /// <para>A name the pact already has, or one another pact of the player's holds or held, is not on
    /// offer: the alliances page and the readers that find a pact's lines by its name tell two pacts
    /// apart by it (<see cref="AllianceRead"/>). Nor is one past the save's hundred characters.</para>
    ///
    /// <para>Pure and read-only: it neither mutates the state nor draws from its generator.</para>
    /// </summary>
    public static class PactNames
    {
        /// <summary>
        /// The web build's names for a pact: its fallback generator's (fallback-generator.ts: "Power
        /// Players", "The Outsiders", "Dream Team") and three its proposal dialog builds from its own
        /// words (AllianceProposalModal.tsx: The, Silent, Hidden, Golden; Circle, Council, Crew).
        /// </summary>
        public static readonly string[] Web =
        {
            "The Outsiders", "Dream Team", "Power Players", "The Silent Circle", "The Hidden Council", "The Golden Crew",
        };

        /// <summary>The longest name a save holds (<see cref="EpisodeValidation"/>).</summary>
        public const int LongestName = 100;

        /// <summary>
        /// The name the house would give a pact of these people now: "The Riley Pact", "The Riley and Jo
        /// Pact", "The Riley, Jo and Sam Pact" - their first names in the order given. Null for nobody.
        /// </summary>
        public static string HouseName(EpisodeState s, IList<string> ids)
        {
            var firsts = (ids ?? new List<string>()).Select(id => s?.Find(id)?.name).Where(name => !string.IsNullOrEmpty(name))
                .Select(name => name.Split(' ')[0]).ToList();
            if (firsts.Count == 0) return null;
            if (firsts.Count == 1) return "The " + firsts[0] + " Pact";
            return "The " + string.Join(", ", firsts.Take(firsts.Count - 1)) + " and " + firsts.Last() + " Pact";
        }

        /// <summary>
        /// The names on offer for this pact, in the order offered: the house's name for the members still
        /// in it other than the player, then the web's - less the pact's own name, any other pact of the
        /// player's name (standing or ended), and anything past <see cref="LongestName"/>. Empty for a pact
        /// that is not the player's.
        /// </summary>
        public static List<string> For(EpisodeState s, AllianceState pact)
        {
            var names = new List<string>();
            if (s?.alliances == null || pact?.members == null || !pact.members.Contains(s.playerId)) return names;
            var taken = new HashSet<string>(s.alliances.Where(a => a != null && a.members != null && a.members.Contains(s.playerId))
                .Select(a => a.name).Where(name => name != null), StringComparer.Ordinal);
            string house = HouseName(s, pact.members.Where(id => id != s.playerId && s.Find(id)?.status == ContestantStatus.Active).ToList());
            foreach (string name in new[] { house }.Concat(Web))
                if (!string.IsNullOrEmpty(name) && name.Length <= LongestName && !taken.Contains(name) && !names.Contains(name))
                    names.Add(name);
            return names;
        }
    }
}
