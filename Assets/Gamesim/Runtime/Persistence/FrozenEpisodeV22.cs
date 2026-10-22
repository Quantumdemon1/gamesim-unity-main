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

        /// <summary>
        /// Schema 22 has two shapes. Its first builds (50ebd57c, 2026-10-02) saved no <c>linkedDealId</c> on a deal - C7
        /// added it at c55a4535 that afternoon - and no <c>playerJoined</c> on an alliance - the C5 review fix added it at
        /// d44bf492 the next morning - and neither change took a new version: the v21-to-v22 step was rewritten to add
        /// both, so a file those first builds wrote, holding a deal or an alliance, was refused as "Invalid historical
        /// schema 22 data" (Assets/Plans/OLD-SAVE-ACCEPTANCE.md, the s22 early fixture).
        ///
        /// <para>This gives such a file the two fields, as hand-written literals: a deal no negotiation linked (null),
        /// and a pact nobody joined after its founder (false) - what the build that wrote it had no field for, read
        /// as it read it, the order of a pact's members saying who founded it. Only where a list holds the older shape
        /// whole: a list where every row lacks the field gains it on every row; one where any row carries it is left as
        /// it is, so a mix still fails <see cref="Validate"/>. The input is never changed; the result is a clone.</para>
        /// </summary>
        internal static JObject WithLaterFields(JObject original)
        {
            if (original == null) throw new InvalidDataException("Expected simulation schema version 22.");
            var result = (JObject)original.DeepClone();
            Complete(result["deals"] as JArray, "linkedDealId", JValue.CreateNull());
            Complete(result["alliances"] as JArray, "playerJoined", new JValue(false));
            return result;
        }

        private static void Complete(JArray rows, string field, JValue literal)
        {
            if (rows == null || rows.Count == 0 || !rows.All(row => row is JObject o && o.Property(field) == null)) return;
            foreach (JObject row in rows) row.Add(field, literal.DeepClone());
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
