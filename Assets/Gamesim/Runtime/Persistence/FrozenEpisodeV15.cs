using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Schema 15 was schema 14 plus the strategy windows: a rules boundary, the week's pleas and the
    /// reply cards waiting on the player. Their checks are copied here as they stood, the pleas' and
    /// the cards' vocabularies included, so a later change to the live rules cannot change what a
    /// schema 15 save was allowed to hold.
    /// </summary>
    internal static class FrozenEpisodeV15
    {
#pragma warning disable 0649
        private sealed class Lobby
        {
            public int week;
            public int phase;
            public string deciderId, ask, subjectId, approach, response;
            public double influence;
        }

        private sealed class ReplyCard
        {
            public string id;
            public int week;
            public string kind, fromId, aboutId;
        }
#pragma warning restore 0649

        private static readonly string[] Fields = { "strategyRulesStartWeek", "lobbies", "replyCards" };

        /// <summary>What schema 15 knew: the asks, approaches and answers of a plea, and the kinds of card.</summary>
        private static readonly string[] Asks = { "spare", "target", "save", "keep" };
        private static readonly string[] Approaches = { "emotional", "strategic", "deal", "pressure" };
        private static readonly string[] Responses = { "receptive", "open", "skeptical", "hostile" };
        private static readonly string[] Kinds = { "confrontation", "gossip", "plea" };

        /// <summary>The most a plea could move a decision, either way.</summary>
        private const double MostInfluence = 100;

        /// <summary>EpisodePhase as schema 15 stored it: free time, the nominations, the veto meeting, the campaign.</summary>
        private const int Social = 0, Nomination = 2, VetoMeeting = 5, Campaign = 6;

        internal static void Validate(JObject original)
        {
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer || (long)original["schemaVersion"] != 15)
                throw new InvalidDataException("Expected simulation schema version 15.");
            var previous = (JObject)original.DeepClone();
            if (Fields.Any(name => previous.Property(name) == null)) throw new InvalidDataException("Missing schema 15 field.");
            if (previous["week"]?.Type != JTokenType.Integer || previous["phase"]?.Type != JTokenType.Integer
                || previous["playerId"]?.Type != JTokenType.String || !(previous["contestants"] is JArray people)) throw Invalid();
            long week = (long)previous["week"];
            long phase = (long)previous["phase"];
            string player = (string)previous["playerId"];
            var ids = new HashSet<string>(people.OfType<JObject>().Select(person => person["id"])
                .Where(id => id?.Type == JTokenType.String).Select(id => (string)id), StringComparer.Ordinal);

            var start = previous["strategyRulesStartWeek"];
            if (start.Type != JTokenType.Integer || (long)start < 0 || (long)start > Math.Min(101, week + 1)) throw Invalid();

            if (!(previous["lobbies"] is JArray lobbies) || lobbies.Count > 32) throw Invalid();
            foreach (var token in lobbies)
            {
                SaveJson.CheckDtoShape(token, typeof(Lobby), "state(v15).lobbies[]");
                try
                {
                    var plea = token.ToObject<Lobby>(SaveJson.Serializer());
                    if (plea.week != week || (plea.phase != Nomination && plea.phase != VetoMeeting)
                        || plea.deciderId == null || !ids.Contains(plea.deciderId) || plea.deciderId == player
                        || Array.IndexOf(Asks, plea.ask) < 0 || Array.IndexOf(Approaches, plea.approach) < 0
                        || Array.IndexOf(Responses, plea.response) < 0
                        || (plea.subjectId != null && !ids.Contains(plea.subjectId))
                        || double.IsNaN(plea.influence) || double.IsInfinity(plea.influence) || Math.Abs(plea.influence) > MostInfluence)
                        throw Invalid();
                }
                catch (Exception error) when (error is JsonException || error is OverflowException)
                { throw new InvalidDataException("Historical schema 15 plea exceeds its frozen contract.", error); }
            }

            if (!(previous["replyCards"] is JArray cards) || cards.Count > 24) throw Invalid();
            var cardIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in cards)
            {
                SaveJson.CheckDtoShape(token, typeof(ReplyCard), "state(v15).replyCards[]");
                try
                {
                    var card = token.ToObject<ReplyCard>(SaveJson.Serializer());
                    if (string.IsNullOrWhiteSpace(card.id) || card.id.Length > 160 || !cardIds.Add(card.id) || card.week != week
                        || Array.IndexOf(Kinds, card.kind) < 0 || card.fromId == null || !ids.Contains(card.fromId) || card.fromId == player
                        || (card.aboutId != null && !ids.Contains(card.aboutId)))
                        throw Invalid();
                }
                catch (Exception error) when (error is JsonException || error is OverflowException)
                { throw new InvalidDataException("Historical schema 15 reply card exceeds its frozen contract.", error); }
            }
            // A card waits for the phase it arrived in: free time or the campaign.
            if (cards.Count > 0 && phase != Social && phase != Campaign) throw Invalid();
            // A season without the strategy windows held none of their records.
            if ((long)start == 0 && (lobbies.Count > 0 || cards.Count > 0)) throw Invalid();

            foreach (var name in Fields) previous.Remove(name);
            previous["schemaVersion"] = 14;
            FrozenEpisodeV14.Validate(previous);
        }

        private static InvalidDataException Invalid() => new InvalidDataException("Invalid historical schema 15 data.");
    }
}
