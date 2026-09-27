using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Schema 14 was schema 13 plus the Have-Nots and the veto's prizes: a rules boundary, three lists
    /// of houseguests and the record of every prize and punishment handed out. Their checks are copied
    /// here as they stood, prize ids included, so a later change to the live rules cannot change what a
    /// schema 14 save was allowed to hold.
    /// </summary>
    internal static class FrozenEpisodeV14
    {
#pragma warning disable 0649
        private sealed class Prize { public int week; public string contestantId, prizeId; }
#pragma warning restore 0649

        /// <summary>The prizes and punishments schema 14 knew.</summary>
        private static readonly string[] PrizeIds = { "cash", "have-not-pass", "luxury-night", "have-not", "the-alarm", "costume" };

        private static readonly string[] Fields = { "haveNotRulesStartWeek", "haveNots", "haveNotPasses", "punishedHaveNots", "vetoPrizes" };

        internal static void Validate(JObject original)
        {
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer || (long)original["schemaVersion"] != 14)
                throw new InvalidDataException("Expected simulation schema version 14.");
            var previous = (JObject)original.DeepClone();
            if (Fields.Any(name => previous.Property(name) == null)) throw new InvalidDataException("Missing schema 14 field.");
            if (previous["week"]?.Type != JTokenType.Integer || !(previous["contestants"] is JArray people)) throw Invalid();
            long week = (long)previous["week"];
            var ids = new HashSet<string>(people.OfType<JObject>().Select(person => person["id"])
                .Where(id => id?.Type == JTokenType.String).Select(id => (string)id), StringComparer.Ordinal);

            var start = previous["haveNotRulesStartWeek"];
            if (start.Type != JTokenType.Integer || (long)start < 0 || (long)start > Math.Min(101, week + 1)) throw Invalid();
            int named = Houseguests(previous["haveNots"], ids) + Houseguests(previous["haveNotPasses"], ids)
                + Houseguests(previous["punishedHaveNots"], ids);

            if (!(previous["vetoPrizes"] is JArray prizes) || prizes.Count > 300) throw Invalid();
            foreach (var token in prizes)
            {
                SaveJson.CheckDtoShape(token, typeof(Prize), "state(v14).vetoPrizes[]");
                try
                {
                    var prize = token.ToObject<Prize>(SaveJson.Serializer());
                    if (prize.week < 1 || prize.week > week || prize.contestantId == null || !ids.Contains(prize.contestantId)
                        || Array.IndexOf(PrizeIds, prize.prizeId) < 0) throw Invalid();
                }
                catch (Exception error) when (error is JsonException || error is OverflowException)
                { throw new InvalidDataException("Historical schema 14 prize exceeds its frozen contract.", error); }
            }
            // A season without Have-Nots held none of them.
            if ((long)start == 0 && (named > 0 || prizes.Count > 0)) throw Invalid();

            foreach (var name in Fields) previous.Remove(name);
            previous["schemaVersion"] = 13;
            FrozenEpisodeV13.Validate(previous);
        }

        /// <summary>How many a list names, having checked they are distinct houseguests of this season.</summary>
        private static int Houseguests(JToken token, HashSet<string> ids)
        {
            if (!(token is JArray list) || list.Count > 16 || list.Any(id => id.Type != JTokenType.String || !ids.Contains((string)id))
                || list.Select(id => (string)id).Distinct(StringComparer.Ordinal).Count() != list.Count) throw Invalid();
            return list.Count;
        }

        private static InvalidDataException Invalid() => new InvalidDataException("Invalid historical schema 14 data.");
    }
}
