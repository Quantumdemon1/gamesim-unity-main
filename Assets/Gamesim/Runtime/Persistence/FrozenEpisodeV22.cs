using System;
using System.IO;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// The schema-22 commitment fields and vocabulary, detached from growing live DTOs. Strip only
    /// those additions from a clone and validate the remainder as v21. Final-three deals are valid
    /// here: the older shape holds a string, not an older live enum. Reference/settlement semantics
    /// are checked again by the current validator after migration, which does not change them.
    /// </summary>
    internal static class FrozenEpisodeV22
    {
        private static readonly string[] DealKinds = { "target_agreement", "safety_agreement", "vote_together",
            "vote_save", "vote_evict", "veto_use", "information_sharing", "final_two", "partnership", "alliance_invite", "final_three" };
        private static readonly string[] DealStatuses = { "proposed", "accepted", "active", "fulfilled", "broken", "declined", "expired" };
        private static readonly string[] TrustWeights = { "low", "medium", "high", "critical" };

        internal static void Validate(JObject original)
        {
            if (original == null || !Integer(original["schemaVersion"], 22, 22))
                throw new InvalidDataException("Expected simulation schema version 22.");
            if (!Integer(original["week"], 1, 100)) throw Invalid();
            int week = (int)original["week"];
            if (!Integer(original["commitmentRulesStartWeek"], 0, week + 1)) throw Invalid();
            if (!(original["windowActions"] is JArray windows) || windows.Count != 4 || windows.Any(n => !Integer(n, 0, 24))) throw Invalid();

            var previous = (JObject)original.DeepClone();
            foreach (string name in new[] { "deals", "promises" })
            {
                if (!(previous[name] is JArray rows) || rows.Count > 200) throw Invalid();
                foreach (var token in rows)
                {
                    if (!(token is JObject row) || !NullableKey(row["brokenById"], 100) || !Integer(row["settledWeek"], 0, week)) throw Invalid();
                    if (name == "deals")
                    {
                        if (!NullableKey(row["linkedDealId"], 160) || !Known(row["type"], DealKinds)
                            || !Known(row["status"], DealStatuses) || !Known(row["trustImpact"], TrustWeights)) throw Invalid();
                        row.Remove("linkedDealId");
                    }
                    row.Remove("brokenById"); row.Remove("settledWeek");
                }
            }
            if (!(previous["alliances"] is JArray alliances) || alliances.Count > 100) throw Invalid();
            foreach (var token in alliances)
            {
                if (!(token is JObject alliance) || alliance["playerJoined"]?.Type != JTokenType.Boolean) throw Invalid();
                alliance.Remove("playerJoined");
            }
            previous.Remove("commitmentRulesStartWeek");
            previous["schemaVersion"] = 21;
            FrozenEpisodeV21.Validate(previous);
        }

        private static bool Integer(JToken value, long min, long max) => value?.Type == JTokenType.Integer
            && long.TryParse(value.ToString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long number)
            && number >= min && number <= max;
        private static bool NullableKey(JToken value, int max) => value != null && (value.Type == JTokenType.Null
            || (value.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)value) && ((string)value).Length <= max));
        private static bool Known(JToken value, string[] allowed) => value?.Type == JTokenType.String && allowed.Contains((string)value);
        private static InvalidDataException Invalid() => new InvalidDataException("Invalid historical schema 22 data.");
    }
}
