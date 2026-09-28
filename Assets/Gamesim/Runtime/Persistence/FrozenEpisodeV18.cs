using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Schema 18 was schema 17 plus the levers' boundary and two ledger lists (replies, calls). What
    /// is frozen here is the shape: the boundary's bound and the two lists, each bounded by the cap
    /// schema 18 wrote. The rows' meanings are checked by the live ledger validator after migration,
    /// which is safe because the step to 19 does not touch them: it adds the week's four counters and
    /// its boundary, and nothing else.
    /// </summary>
    internal static class FrozenEpisodeV18
    {
        private static readonly string[] Fields = { "leverRulesStartWeek" };
        private static readonly string[] LedgerLists = { "replies", "calls" };
        private const int MostRows = 512;

        internal static void Validate(JObject original)
        {
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer || (long)original["schemaVersion"] != 18)
                throw new InvalidDataException("Expected simulation schema version 18.");
            var previous = (JObject)original.DeepClone();
            if (Fields.Any(name => previous.Property(name) == null)) throw new InvalidDataException("Missing schema 18 field.");
            if (previous["week"]?.Type != JTokenType.Integer) throw Invalid();
            long week = (long)previous["week"];

            if (previous["leverRulesStartWeek"].Type != JTokenType.Integer) throw Invalid();
            long levers = (long)previous["leverRulesStartWeek"];
            if (levers < 0 || levers > Math.Min(101, week + 1)) throw Invalid();

            if (!(previous["ledger"] is JObject ledger)) throw Invalid();
            foreach (var name in LedgerLists)
                if (!(ledger[name] is JArray list) || list.Count > MostRows || list.Any(item => !(item is JObject))) throw Invalid();

            // Strip what schema 18 added and hand the rest to the schema 17 contract.
            foreach (var name in Fields) previous.Remove(name);
            foreach (var name in LedgerLists) ledger.Remove(name);
            previous["schemaVersion"] = 17;
            FrozenEpisodeV17.Validate(previous);
        }

        private static InvalidDataException Invalid() => new InvalidDataException("Invalid historical schema 18 data.");
    }
}
