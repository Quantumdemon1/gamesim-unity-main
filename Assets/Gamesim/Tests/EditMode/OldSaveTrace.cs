using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The digest an old-save fixture is held to (Assets/Plans/OLD-SAVE-ACCEPTANCE.md). One source, compiled twice:
    /// into this assembly, where <see cref="OldSaveAcceptanceTests"/> replays each fixture on the current build, and
    /// - copied byte for byte - into the scratch recorder that played the same fixture on the build that wrote it
    /// (or, for a real save, the oldest build that loads it with the current build's rules). So it touches nothing
    /// of the simulation's types and nothing a build added later: only System and Newtonsoft, over any object's
    /// public instance fields, in declaration order.
    ///
    /// <para><b>A checkpoint</b> is the state as the save format stores it (public fields, never a getter), taken
    /// to the reference build's schema by <see cref="Project"/>, written with sorted keys and no whitespace, and
    /// hashed three ways. <b>Blind</b> writes every string with whitespace in it as "~" (<see cref="Prose"/>): prose,
    /// which writing passes reword for every season on purpose. Ids, kinds, statuses, numbers, every list's length
    /// and order and the season's random state stay. <b>Season</b> is blind and leaves out the narrated log
    /// (<c>events</c>), whose lines and audiences presentation and privacy passes change for every season on purpose
    /// (the ballot privacy of UI-UX-PASS-PLAN B0 among them); everything the rules read and write stays, so two
    /// builds agree on it exactly when they played the same game. <b>Text</b> is the whole state verbatim.</para>
    ///
    /// <para><b>The digest</b> is the first sixteen hex of the SHA-256 of the checkpoint hashes, one a line, as
    /// the season digests write theirs: the loaded state first, then the state after every accepted command.</para>
    /// </summary>
    internal static class OldSaveTrace
    {
        /// <summary>The marker a blind checkpoint writes in place of a string that holds whitespace.</summary>
        internal const string Prose = "~";

        /// <summary>Public instance fields only, in declaration order: the save format's own view of a state.</summary>
        private sealed class FieldsOnly : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) =>
                type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Select(field => base.CreateProperty(field, MemberSerialization.Fields)).ToList();
        }

        private static readonly FieldsOnly Resolver = new FieldsOnly();

        private static JsonSerializer Serializer() => JsonSerializer.Create(new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ContractResolver = Resolver,
            DateParseHandling = DateParseHandling.None,
            MaxDepth = 64,
        });

        /// <summary>A state's fields as a detached JSON object.</summary>
        internal static JObject Fields(object state) => JObject.FromObject(state, Serializer());

        /// <summary>Sorted keys at every depth, no whitespace; <paramref name="blind"/> writes prose as <see cref="Prose"/>.</summary>
        internal static string Canonical(JToken token, bool blind)
        {
            JToken Ordered(JToken value)
            {
                if (value is JObject obj)
                    return new JObject(obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => new JProperty(p.Name, Ordered(p.Value))));
                if (value is JArray array) return new JArray(array.Select(Ordered));
                if (blind && value.Type == JTokenType.String && ((string)value).Any(char.IsWhiteSpace)) return new JValue(Prose);
                return value.DeepClone();
            }
            return Ordered(token).ToString(Formatting.None);
        }

        internal static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>The sixteen-hex digest of a run of checkpoint hashes.</summary>
        internal static string Digest(IEnumerable<string> checkpoints) => Sha256(string.Join("\n", checkpoints)).Substring(0, 16);

        /// <summary>
        /// One checkpoint's three hashes, of the state taken to <paramref name="schema"/>: the season's (blind, and
        /// without the narrated log <c>events</c>), the whole state's blind, and the whole state's verbatim.
        /// </summary>
        internal static (string season, string blind, string text) Checkpoint(object state, int schema)
        {
            var fields = Project(Fields(state), schema);
            var season = (JObject)fields.DeepClone();
            season.Remove("events");
            return (Sha256(Canonical(season, true)), Sha256(Canonical(fields, true)), Sha256(Canonical(fields, false)));
        }

        // ------------------------------------------------------------------ the projection

        /// <summary>
        /// What each schema from 21 on added, as its migration wrote it (EpisodeSaveMigrations.UpgradeV20ToV21 to
        /// UpgradeV27ToV28), with the literal value it wrote. Every one is a rule switched off or storage that stays
        /// empty while it is, so a season saved before it plays with each still at that value to its end: removing
        /// them gives back the schema the reference build stored. A value that moved is refused, never dropped.
        /// </summary>
        private static readonly (int schema, string path, JToken literal)[] Additions =
        {
            (21, "finaleRulesStartWeek", 0), (21, "finalArgument", JValue.CreateNull()),
            (21, "juryExchanges[].category", JValue.CreateNull()), (21, "juryExchanges[].receiptKind", JValue.CreateNull()),
            (21, "juryExchanges[].receiptId", JValue.CreateNull()),
            (22, "commitmentRulesStartWeek", 0),
            (22, "deals[].brokenById", JValue.CreateNull()), (22, "deals[].settledWeek", 0), (22, "deals[].linkedDealId", JValue.CreateNull()),
            (22, "promises[].brokenById", JValue.CreateNull()), (22, "promises[].settledWeek", 0),
            (22, "alliances[].playerJoined", false),
            (23, "economyRulesVersion", 0), (23, "moveInExtrasSpent", 0),
            (24, "unifiedCommitmentRulesVersion", 0), (24, "unifiedCommitments", new JArray()),
            (25, "unifiedHearingRulesVersion", 0), (25, "unifiedHearingEvidence", new JArray()), (25, "unifiedHearingReceipts", new JArray()),
            (26, "unifiedCommitments[].targetId", JValue.CreateNull()), (26, "unifiedCommitments[].subtype", JValue.CreateNull()),
            (26, "unifiedVoteReveals", new JArray()),
            (27, "unifiedCommitments[].voteBindingWeek", 0), (27, "unifiedCommitments[].voteFirstRevealWeek", 0),
            (28, "allianceLeakRulesStartWeek", 0), (28, "pactPlanRulesStartWeek", 0), (28, "allWeekRulesStartWeek", 0),
            (28, "npcSocial.beatWeek", 0), (28, "npcSocial.beatWindow", -1), (28, "npcSocial.beatsFired", 0), (28, "npcSocial.beatSeats", 0),
            (28, "npcSocial.beatPlan", new JArray()), (28, "npcSocial.acts", new JArray()), (28, "ledger.plans", new JArray()),
        };

        /// <summary>The newest schema <see cref="Project"/> knows: a state above it is refused rather than guessed at.</summary>
        internal const int Newest = 28;

        /// <summary>The oldest schema a projection reaches: the first whose later additions are all switched off (schema 20).</summary>
        internal const int Oldest = 20;

        /// <summary>
        /// <paramref name="fields"/> (a state's public fields at its own schema) taken down to <paramref name="schema"/>:
        /// every addition above it removed, after checking it still holds its migration's literal - or, for a row a
        /// later build's rules would fill, the literal on every row. The header is set to the target schema. A state
        /// already at the target is returned unchanged.
        /// </summary>
        internal static JObject Project(JObject fields, int schema)
        {
            if (fields == null) throw new ArgumentNullException(nameof(fields));
            int from = fields["schemaVersion"]?.Type == JTokenType.Integer ? (int)fields["schemaVersion"] : -1;
            if (from == schema) return fields;
            if (from < schema || from > Newest || schema < Oldest)
                throw new InvalidDataException("A projection runs from a known schema down to " + Oldest + " or later, never up: " + from + " to " + schema + ".");
            var result = (JObject)fields.DeepClone();
            foreach (var (added, path, literal) in Additions.Where(a => a.schema > schema && a.schema <= from).OrderByDescending(a => a.schema))
                Remove(result, path, literal);
            result["schemaVersion"] = schema;
            return result;
        }

        private static void Remove(JObject root, string path, JToken literal)
        {
            int dot = path.IndexOf('.');
            if (dot < 0)
            {
                Take(root, path, literal, path);
                return;
            }
            string head = path.Substring(0, dot), rest = path.Substring(dot + 1);
            if (head.EndsWith("[]", StringComparison.Ordinal))
            {
                string list = head.Substring(0, head.Length - 2);
                if (!(root[list] is JArray rows)) throw new InvalidDataException("Not a list: " + list);
                for (int i = 0; i < rows.Count; i++)
                {
                    if (!(rows[i] is JObject row)) throw new InvalidDataException("Not a row: " + list + "[" + i + "]");
                    Take(row, rest, literal, list + "[" + i + "]." + rest);
                }
                return;
            }
            if (!(root[head] is JObject child)) throw new InvalidDataException("Not an object: " + head);
            Remove(child, rest, literal);
        }

        private static void Take(JObject owner, string name, JToken literal, string where)
        {
            var property = owner.Property(name);
            if (property == null) throw new InvalidDataException("Missing addition: " + where);
            if (!JToken.DeepEquals(property.Value, literal))
                throw new InvalidDataException("A later schema's field left its switched-off value, so the season did not play as the reference build played it: "
                    + where + " = " + property.Value.ToString(Formatting.None) + ", not " + literal.ToString(Formatting.None) + ".");
            property.Remove();
        }
    }
}
