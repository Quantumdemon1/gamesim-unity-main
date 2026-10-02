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
    /// <para>A name the player knows a pact by is not on offer (<see cref="Known"/>): the pact's own, one
    /// another pact of the player's holds or held, one of a pact the player walked out of that goes on
    /// without them, one of a pact of others the player knows of. Captions name the player's pacts, and
    /// the alliances page and the readers that find a pact's lines by its name tell two pacts apart by it
    /// (<see cref="AllianceRead"/>). Nor is one past the save's hundred characters. And under the
    /// commitment rules a new pact of the player's never takes the name of one they stand in
    /// (<see cref="Unique"/>): "The Riley Pact II" beside the Riley Pact C2's cut left going on without
    /// Riley.</para>
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
        /// in it other than the player, then the web's - less every name the player knows a pact by
        /// (<see cref="Known"/>, the pact's own among them) and anything past <see cref="LongestName"/>.
        /// Empty for a pact that is not the player's.
        /// </summary>
        public static List<string> For(EpisodeState s, AllianceState pact)
        {
            var names = new List<string>();
            if (s?.alliances == null || pact?.members == null || !pact.members.Contains(s.playerId)) return names;
            var taken = Known(s);
            string house = HouseName(s, pact.members.Where(id => id != s.playerId && s.Find(id)?.status == ContestantStatus.Active).ToList());
            foreach (string name in new[] { house }.Concat(Web))
                if (!string.IsNullOrEmpty(name) && name.Length <= LongestName && !taken.Contains(name) && !names.Contains(name))
                    names.Add(name);
            return names;
        }

        /// <summary>
        /// Every name the player knows a pact by: their own pacts', standing or ended; a pact of three or
        /// more they walked out of that goes on without them, by their own line while the log holds it
        /// (<see cref="EpisodeEngine.IsLeftGoesOnLine"/>); and a pact of others they know of
        /// (<see cref="FinalistRead.AllianceCertainty"/>). A pact the player cannot know of never counts,
        /// so what is on offer tells them nothing of one.
        /// </summary>
        public static HashSet<string> Known(EpisodeState s)
        {
            var known = new HashSet<string>(StringComparer.Ordinal);
            if (s?.alliances == null) return known;
            var seen = (s.events ?? new List<EpisodeEvent>())
                .Where(e => e != null && e.kind == "alliance" && e.text != null && e.audienceIds != null && e.audienceIds.Contains(s.playerId)).ToList();
            foreach (var pact in s.alliances.Where(a => a?.members != null && !string.IsNullOrEmpty(a.name)))
                if (pact.members.Contains(s.playerId) || FinalistRead.AllianceCertainty(s, pact) != null
                    || seen.Any(e => EpisodeEngine.IsLeftGoesOnLine(e.text, pact.name)))
                    known.Add(pact.name);
            return known;
        }

        /// <summary>
        /// A name for a new pact of the player's no pact they stand in already has: the name itself, or,
        /// where one of theirs has it - "The Riley Pact" C2's cut left going on without Riley - the next of
        /// "The Riley Pact II", "III" and so on, cut to fit the save. So no two of the player's standing
        /// pacts share a name, and no two captions that name them collide. Under the commitment rules only
        /// (the caller's to ask); a season without them names its pacts as it always did.
        /// </summary>
        public static string Unique(EpisodeState s, string name)
        {
            if (s?.alliances == null || string.IsNullOrEmpty(name)) return name;
            var standing = new HashSet<string>(s.alliances.Where(a => a != null && a.active && a.members != null && a.members.Contains(s.playerId))
                .Select(a => a.name).Where(n => n != null), StringComparer.Ordinal);
            if (!standing.Contains(name)) return name;
            string[] numerals = { "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
            for (int n = 0; n < 100; n++)
            {
                string suffix = " " + (n < numerals.Length ? numerals[n] : (n + 2).ToString(System.Globalization.CultureInfo.InvariantCulture));
                string candidate = (name.Length + suffix.Length > LongestName ? name.Substring(0, LongestName - suffix.Length) : name) + suffix;
                if (!standing.Contains(candidate)) return candidate;
            }
            return name;
        }
    }
}
