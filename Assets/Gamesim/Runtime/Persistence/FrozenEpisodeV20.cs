using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Schema 20 was schema 19 plus the agency boundary. What is frozen here is that boundary's
    /// bound. The step to 21 adds the finale rules' boundary, the final argument and three fields on
    /// every jury exchange, so the rest is handed to the schema 19 contract unchanged; a v20 save
    /// carrying any of them is refused down the chain, where the exchange's shape is checked.
    /// </summary>
    internal static class FrozenEpisodeV20
    {
        private static readonly string[] Fields = { "agencyRulesStartWeek" };

        internal static void Validate(JObject original)
        {
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer || (long)original["schemaVersion"] != 20)
                throw new InvalidDataException("Expected simulation schema version 20.");
            var previous = (JObject)original.DeepClone();
            if (Fields.Any(name => previous.Property(name) == null)) throw new InvalidDataException("Missing schema 20 field.");
            if (previous["week"]?.Type != JTokenType.Integer) throw Invalid();
            long week = (long)previous["week"];

            if (previous["agencyRulesStartWeek"].Type != JTokenType.Integer) throw Invalid();
            long agency = (long)previous["agencyRulesStartWeek"];
            if (agency < 0 || agency > Math.Min(101, week + 1)) throw Invalid();

            // Strip what schema 20 added and hand the rest to the schema 19 contract.
            foreach (var name in Fields) previous.Remove(name);
            previous["schemaVersion"] = 19;
            FrozenEpisodeV19.Validate(previous);
        }

        private static InvalidDataException Invalid() => new InvalidDataException("Invalid historical schema 20 data.");
    }
}
