using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Schema 21 was schema 20 plus the finale rules (ENDGAME-PLAN §3): their boundary, the player
    /// finalist's final argument, and three fields on every jury exchange - the category, receipt
    /// kind and receipt id of a history question. What is frozen here is their shape, as schema 21
    /// wrote it: the boundary's bound, an argument that is null or exactly a theme and its moment
    /// references, and three fields that are null or bounded text. Their meanings are checked by
    /// the live finale validator after migration, which is safe because the step to 22 does not touch
    /// them: it adds the commitment rules' boundary, two fields on every promise and three on every deal
    /// (who broke it, when it was settled, and the deal it is linked to), so the rest is handed to the
    /// schema 20 contract unchanged, and a v21 save carrying any of the schema 22 fields is refused down
    /// the chain, where the deal's and the promise's shapes are checked.
    /// </summary>
    internal static class FrozenEpisodeV21
    {
        private static readonly string[] Fields = { "finaleRulesStartWeek", "finalArgument" };
        private static readonly string[] ExchangeFields = { "category", "receiptKind", "receiptId" };
        private static readonly string[] ArgumentFields = { "momentRefs", "theme" };

        /// <summary>Schema 21's own numbers: three moments back an argument; a moment's reference and a receipt id are short keys.</summary>
        private const int MostMoments = 3, MostKeyLength = 200;

        internal static void Validate(JObject original)
        {
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer || (long)original["schemaVersion"] != 21)
                throw new InvalidDataException("Expected simulation schema version 21.");
            var previous = (JObject)original.DeepClone();
            if (Fields.Any(name => previous.Property(name) == null)) throw new InvalidDataException("Missing schema 21 field.");
            if (previous["week"]?.Type != JTokenType.Integer) throw Invalid();
            long week = (long)previous["week"];

            if (previous["finaleRulesStartWeek"].Type != JTokenType.Integer) throw Invalid();
            long finale = (long)previous["finaleRulesStartWeek"];
            if (finale < 0 || finale > Math.Min(101, week + 1)) throw Invalid();

            var argument = previous["finalArgument"];
            if (argument.Type != JTokenType.Null)
            {
                if (!(argument is JObject held)
                    || !held.Properties().Select(p => p.Name).OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(ArgumentFields)
                    || !Key(held["theme"])
                    || !(held["momentRefs"] is JArray refs) || refs.Count > MostMoments || refs.Any(item => !Key(item)))
                    throw Invalid();
            }

            if (!(previous["juryExchanges"] is JArray exchanges)) throw Invalid();
            foreach (var token in exchanges)
            {
                // A null row is the schema 20 contract's to refuse; only the rows this version gave fields to are read here.
                if (!(token is JObject exchange)) continue;
                foreach (var name in ExchangeFields)
                {
                    var value = exchange[name];
                    if (exchange.Property(name) == null || (value.Type != JTokenType.Null && !Key(value))) throw Invalid();
                }
            }

            // Strip what schema 21 added and hand the rest to the schema 20 contract.
            foreach (var name in Fields) previous.Remove(name);
            foreach (var exchange in exchanges.OfType<JObject>())
                foreach (var name in ExchangeFields) exchange.Remove(name);
            previous["schemaVersion"] = 20;
            FrozenEpisodeV20.Validate(previous);
        }

        private static bool Key(JToken value) =>
            value != null && value.Type == JTokenType.String && ((string)value).Length > 0 && ((string)value).Length <= MostKeyLength;

        private static InvalidDataException Invalid() => new InvalidDataException("Invalid historical schema 21 data.");
    }
}
