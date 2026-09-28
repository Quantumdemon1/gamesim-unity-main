using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Schema 19 was schema 18 plus the week's boundary and its four window counters. What is frozen
    /// here is the shape: the boundary's bound and a list of exactly four non-negative counts. The
    /// step to 20 adds the agency boundary and nothing else, so the rest is handed to the schema 18
    /// contract unchanged.
    /// </summary>
    internal static class FrozenEpisodeV19
    {
        private static readonly string[] Fields = { "weekRulesStartWeek", "windowActions" };

        internal static void Validate(JObject original)
        {
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer || (long)original["schemaVersion"] != 19)
                throw new InvalidDataException("Expected simulation schema version 19.");
            var previous = (JObject)original.DeepClone();
            if (Fields.Any(name => previous.Property(name) == null)) throw new InvalidDataException("Missing schema 19 field.");
            if (previous["week"]?.Type != JTokenType.Integer) throw Invalid();
            long week = (long)previous["week"];

            if (previous["weekRulesStartWeek"].Type != JTokenType.Integer) throw Invalid();
            long windows = (long)previous["weekRulesStartWeek"];
            if (windows < 0 || windows > Math.Min(101, week + 1)) throw Invalid();

            if (!(previous["windowActions"] is JArray counts) || counts.Count != 4
                || counts.Any(item => item.Type != JTokenType.Integer || (long)item < 0)) throw Invalid();

            // Strip what schema 19 added and hand the rest to the schema 18 contract.
            foreach (var name in Fields) previous.Remove(name);
            previous["schemaVersion"] = 18;
            FrozenEpisodeV18.Validate(previous);
        }

        private static InvalidDataException Invalid() => new InvalidDataException("Invalid historical schema 19 data.");
    }
}
