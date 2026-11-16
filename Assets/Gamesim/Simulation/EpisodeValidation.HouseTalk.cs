using System;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The house talks back (plan 32, "The house talks back", §L): what each of its levels may leave in a season, and what a
    /// season below that level may not hold. Nothing here is a saved field: each slice writes its records into structures that
    /// exist (the story's cooldown table, the ledger, the log) and validates them here, refused below the level that writes
    /// them. Created by S1; S2a, S3, S4, S5a, S5b and S7 append, each in its own section below.
    /// </summary>
    public static partial class EpisodeValidation
    {
        private static bool TryValidateHouseTalk(EpisodeState s, out string error)
        {
            error = null;
            return TryValidateHouseTalkManners(s, out error);
        }

        // ---------------------------------------------------------------- S1: manners, candour, tells (story rules 12)

        /// <summary>
        /// The answer's records (§G1, §G4, §F): <c>said:{week}:{voter}:{stance}:{manner}</c>, <c>tell:{voter}:{manner}</c> and
        /// <c>h2h:{week}:{kind}:{runnerUpId}</c> cooldown keys. A season below story rules 12 holds none; at 12 each names a
        /// houseguest of the season, a known stance and manner (a tell's manner is a tell), and a competition's kind.
        /// </summary>
        private static bool TryValidateHouseTalkManners(EpisodeState s, out string error)
        {
            error = null;
            var keys = s.story?.cooldowns;
            if (keys == null) return true;
            bool At(string key, string prefix) => key != null && key.StartsWith(prefix, StringComparison.Ordinal);
            var records = keys.Where(c => At(c?.key, EpisodeEngine.SaidPrefix) || At(c?.key, EpisodeEngine.TellPrefix) || At(c?.key, Regard.HeadToHeadPrefix)).ToList();
            if (records.Count == 0) return true;
            if (!EpisodeEngine.StoryAt(s, StoryRules.Manners))
                return Fail(out error, "A season below the house talk's manners has none of its records.");
            bool Seated(string id) => s.contestants.Any(c => c.id == id);
            foreach (var row in records)
            {
                var parts = row.key.Split(':');
                bool ok;
                if (At(row.key, EpisodeEngine.SaidPrefix))
                    ok = parts.Length == 5 && int.TryParse(parts[1], out int said) && said >= 1 && said <= s.week && Seated(parts[2])
                         && Candour.IsStance(parts[3]) && Manner.IsKnown(parts[4])
                         && (parts[3] == Candour.Neutral) == (parts[4] == Manner.NonAnswer || parts[4] == Manner.Deflect);
                else if (At(row.key, EpisodeEngine.TellPrefix))
                    ok = parts.Length == 3 && Seated(parts[1]) && Manner.IsTell(parts[2]);
                else
                    ok = parts.Length == 4 && int.TryParse(parts[1], out int won) && won >= 1 && won <= s.week && (parts[2] == "hoh" || parts[2] == "veto")
                         && Seated(parts[3]) && parts[3] != s.playerId;
                if (!ok) return Fail(out error, "Invalid house talk records.");
            }
            return true;
        }
    }
}
