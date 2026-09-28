using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Schema 17 was schema 16 plus the season ledger and the read rules' boundary. What is frozen
    /// here is the shape: the ledger's seven lists and its drop count, each list bounded by the cap
    /// schema 17 wrote, and the boundary's bound. The rows' meanings are checked by the live ledger
    /// validator after migration, which is safe because the step to 18 does not touch them: it adds
    /// two lists to the ledger and the levers' boundary, and nothing else.
    /// </summary>
    internal static class FrozenEpisodeV17
    {
        private static readonly string[] Fields = { "ledger", "readRulesStartWeek" };
        private static readonly string[] Lists = { "opportunities", "competitions", "power", "ballots", "claims", "alliances", "standings" };
        private const int MostRows = 512;

        internal static void Validate(JObject original)
        {
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer || (long)original["schemaVersion"] != 17)
                throw new InvalidDataException("Expected simulation schema version 17.");
            var previous = (JObject)original.DeepClone();
            if (Fields.Any(name => previous.Property(name) == null)) throw new InvalidDataException("Missing schema 17 field.");
            if (previous["week"]?.Type != JTokenType.Integer) throw Invalid();
            long week = (long)previous["week"];

            if (previous["readRulesStartWeek"].Type != JTokenType.Integer) throw Invalid();
            long read = (long)previous["readRulesStartWeek"];
            if (read < 0 || read > Math.Min(101, week + 1)) throw Invalid();

            if (!(previous["ledger"] is JObject ledger)) throw Invalid();
            var expected = Lists.Concat(new[] { "dropped" }).OrderBy(name => name, StringComparer.Ordinal);
            if (!ledger.Properties().Select(p => p.Name).OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(expected)) throw Invalid();
            if (ledger["dropped"].Type != JTokenType.Integer || (long)ledger["dropped"] < 0) throw Invalid();
            foreach (var name in Lists)
                if (!(ledger[name] is JArray list) || list.Count > MostRows || list.Any(item => !(item is JObject))) throw Invalid();

            // Strip what schema 17 added and hand the rest to the schema 16 contract.
            foreach (var name in Fields) previous.Remove(name);
            previous["schemaVersion"] = 16;
            FrozenEpisodeV16.Validate(previous);
        }

        private static InvalidDataException Invalid() => new InvalidDataException("Invalid historical schema 17 data.");
    }
}
