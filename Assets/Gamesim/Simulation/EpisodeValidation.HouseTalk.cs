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
            return TryValidateHouseTalkManners(s, out error) && TryValidateHouseTalkOverheard(s, out error);
        }

        // ---------------------------------------------------------------- S1: manners, candour, tells (story rules 12)

        /// <summary>
        /// The answer's records (§G1, §G4, §F): <c>said:{week}:{voter}:{stance}:{manner}</c>, <c>tell:{voter}:{manner}</c> and
        /// <c>h2h:{week}:{kind}:{runnerUpId}</c> cooldown keys. A season below story rules 12 holds none; at 12 each names a
        /// houseguest of the season, a known stance and manner (a tell's manner is a tell), and a competition's kind. The
        /// <c>line:{week}:{voter}:{manner}:{index}</c> record of the line an answer took names a week of the season, a
        /// houseguest of it, a known manner and one of the slot's lines.
        /// </summary>
        private static bool TryValidateHouseTalkManners(EpisodeState s, out string error)
        {
            error = null;
            var keys = s.story?.cooldowns;
            if (keys == null) return true;
            bool At(string key, string prefix) => key != null && key.StartsWith(prefix, StringComparison.Ordinal);
            var records = keys.Where(c => At(c?.key, EpisodeEngine.SaidPrefix) || At(c?.key, EpisodeEngine.TellPrefix) || At(c?.key, Regard.HeadToHeadPrefix)
                                          || At(c?.key, EpisodeEngine.LinePrefix)).ToList();
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
                else if (At(row.key, EpisodeEngine.LinePrefix))
                {
                    var line = HouseAnswers.ParseLine(row.key);
                    ok = line != null && line.week >= 1 && line.week <= s.week && Seated(line.voterId);
                }
                else
                    ok = parts.Length == 4 && int.TryParse(parts[1], out int won) && won >= 1 && won <= s.week && (parts[2] == "hoh" || parts[2] == "veto")
                         && Seated(parts[3]) && parts[3] != s.playerId;
                if (!ok) return Fail(out error, "Invalid house talk records.");
            }
            return true;
        }

        // ---------------------------------------------------------------- S2a: overheard (story rules 12)

        /// <summary>
        /// Overhearing's records (§I, §L): a <see cref="ClaimSource.Seen"/> standing, a <see cref="ClaimSource.Missed"/> standing
        /// between two houseguests (a caught Listen's), the <see cref="EpisodeEngine.HunchEventKind"/> line and the
        /// <c>heard:{sequence}</c> and <c>pact-heard:{week}</c> cooldown keys. A season below story rules 12 holds none of them.
        /// At 12 a <c>Seen</c> row is a standing between two houseguests whose score is a <see cref="HunchTone"/>, never a vote
        /// claim; the hunch line is the player's alone; a <c>heard:</c> key names a conversation the season has numbered and a
        /// <c>pact-heard:</c> key a week of the season.
        /// </summary>
        private static bool TryValidateHouseTalkOverheard(EpisodeState s, out string error)
        {
            error = null;
            var standings = s.ledger?.standings;
            var claims = s.ledger?.claims;
            bool At(string key, string prefix) => key != null && key.StartsWith(prefix, StringComparison.Ordinal);
            bool Between(StandingRow r) => r.fromId != s.playerId && r.toId != s.playerId;
            var seen = standings == null ? new System.Collections.Generic.List<StandingRow>()
                : standings.Where(r => r != null && r.source == ClaimSource.Seen).ToList();
            bool caughtRows = standings != null && standings.Any(r => r != null && r.source == ClaimSource.Missed && Between(r));
            bool seenClaims = claims != null && claims.Any(k => k != null && k.source == ClaimSource.Seen);
            bool lines = s.events != null && s.events.Any(e => e != null && e.kind == EpisodeEngine.HunchEventKind);
            var keys = s.story?.cooldowns == null ? new System.Collections.Generic.List<StoryCooldownState>()
                : s.story.cooldowns.Where(c => At(c?.key, EpisodeEngine.HeardPrefix) || At(c?.key, EpisodeEngine.PactHeardPrefix)).ToList();
            if (seen.Count == 0 && !caughtRows && !seenClaims && !lines && keys.Count == 0) return true;
            if (!EpisodeEngine.StoryAt(s, StoryRules.Manners))
                return Fail(out error, "A season below the house talk's overhearing has none of its records.");
            if (seenClaims || seen.Any(r => !Between(r) || !HunchTone.IsKnown(r.score))
                || s.events.Any(e => e != null && e.kind == EpisodeEngine.HunchEventKind
                    && (e.audienceIds == null || e.audienceIds.Count != 1 || e.audienceIds[0] != s.playerId)))
                return Fail(out error, "Invalid overheard records.");
            long numbered = s.npcSocial?.nextConversationSequence ?? 0;
            foreach (var row in keys)
            {
                bool pact = At(row.key, EpisodeEngine.PactHeardPrefix);
                string rest = row.key.Substring(pact ? EpisodeEngine.PactHeardPrefix.Length : EpisodeEngine.HeardPrefix.Length);
                bool digits = rest.Length > 0 && rest.All(ch => ch >= '0' && ch <= '9');
                bool ok = pact
                    ? digits && int.TryParse(rest, out int week) && week >= 1 && week <= s.week
                    : digits && long.TryParse(rest, out long sequence) && sequence >= 1 && sequence < numbered;
                if (!ok) return Fail(out error, "Invalid overheard records.");
            }
            return true;
        }
    }
}
