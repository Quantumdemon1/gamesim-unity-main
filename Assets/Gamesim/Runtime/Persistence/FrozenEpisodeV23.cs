using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// The actual schema-23 extension, including its later pitch/speech vocabulary. This forward
    /// contract is deliberately not wired into migration yet. Only independently checked pitch
    /// cards and widened lobby rows are removed from a validation clone before the frozen v22
    /// contract checks its remainder. The original payload, ledger and events are never rewritten.
    /// Unchanged story/ledger semantics still require the current validator after migration, as
    /// in v16-v22; a future schema must not broaden those legacy meanings through this overlay.
    /// </summary>
    internal static class FrozenEpisodeV23
    {
        private const int Social = 0, Nomination = 2, VetoMeeting = 5, Campaign = 6, Eviction = 7;
        private const string Pitch = "pitch", Inspection = "feel-out", SpeechPrefix = "block-speech:", Quiet = "quiet";
        private static readonly string[] CardKinds = { "confrontation", "gossip", "plea", Pitch };
        private static readonly string[] Asks = { "spare", "target", "save", "keep", "vote" };
        private static readonly string[] Approaches = { "emotional", "strategic", "deal", "pressure" };
        private static readonly string[] Responses = { "receptive", "open", "skeptical", "hostile" };

#pragma warning disable 0649
        private sealed class Lobby
        {
            public int week, phase;
            public string deciderId, ask, subjectId, approach, response;
            public double influence;
        }
        private sealed class Card { public string id; public int week; public string kind, fromId, aboutId; }
        private sealed class Reply
        {
            public int week;
            public string cardId, kind, fromId, listenerId, replyKey;
            public bool promised;
            public double toThem;
        }
        private sealed class Event
        {
            public int sequence, week, phase;
            public string kind, text;
            public List<string> audienceIds;
        }
        private sealed class Speech { public string speakerId, text; public int week; public bool isPlayerAuthored; }
#pragma warning restore 0649

        internal static void Validate(JObject original)
        {
            try { ValidatedProjection(original); }
            catch (Exception error) when (error is JsonException || error is OverflowException)
            { throw new InvalidDataException("Historical schema 23 exceeds its frozen numeric contract.", error); }
        }

        // Private so a caller cannot accidentally install this validation-only projection as a save.
        private static JObject ValidatedProjection(JObject original)
        {
            if (original == null || Number(original["schemaVersion"], 23, 23) != 23) throw Invalid();
            int week = Number(original["week"], 1, 100), phase = Number(original["phase"], 0, 15);
            int economy = Number(original["economyRulesVersion"], 0, 1);
            int debit = Number(original["moveInExtrasSpent"], 0, 24);
            int windowStart = Number(original["weekRulesStartWeek"], 0, Math.Min(101, week + 1));
            int strategyStart = Number(original["strategyRulesStartWeek"], 0, Math.Min(101, week + 1));
            int agencyStart = Number(original["agencyRulesStartWeek"], 0, Math.Min(101, week + 1));
            int leverStart = Number(original["leverRulesStartWeek"], 0, Math.Min(101, week + 1));
            int stage = Number(original["evictionStage"], 0, 4);
            int nextSequence = Number(original["nextSequence"], 1, 1000000);
            string player = Key(original["playerId"], 100), hoh = NullableText(original["hohId"], 100);
            bool resolved = Boolean(original["evictionResolved"]);
            bool firstNight = phase == Social && week == 1 && string.IsNullOrEmpty(hoh) && !resolved;
            if (debit != 0 && (economy == 0 || !On(windowStart, week) || week != 1 || firstNight)) throw Invalid();

            var people = Array(original["contestants"], 16);
            if (people.Count < 3) throw Invalid();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var active = new List<string>();
            int playerCount = 0;
            foreach (var token in people)
            {
                if (!(token is JObject person)) throw Invalid();
                string id = Key(person["id"], 100);
                if (!ids.Add(id)) throw Invalid();
                if (Number(person["status"], 0, 5) == 0) active.Add(id);
                if (Boolean(person["isPlayer"])) { if (id != player) throw Invalid(); playerCount++; }
            }
            if (playerCount != 1 || !ids.Contains(player) || (!string.IsNullOrEmpty(hoh) && !ids.Contains(hoh))) throw Invalid();
            var nominees = Identifiers(original["nominees"], ids, 2, true);
            var lobbies = Rows<Lobby>(original["lobbies"], 32, "lobbies");
            var cards = Rows<Card>(original["replyCards"], 24, "replyCards");
            if (!(original["ledger"] is JObject ledger)) throw Invalid();
            var replies = Rows<Reply>(ledger["replies"], 512, "ledger.replies");
            var events = Rows<Event>(original["events"], 256, "events");
            var speeches = Rows<Speech>(original["evictionSpeeches"], 2, "evictionSpeeches");

            // Check complete original lists, including shared caps and duplicate identities, BEFORE
            // taking out anything the old validator does not understand. Do not tighten the old
            // lobby's accepted ask/phase combinations: v23's validator accepted their cross-product.
            foreach (var lobby in lobbies)
                if (lobby.week != week || (lobby.phase != Nomination && lobby.phase != VetoMeeting && lobby.phase != Campaign)
                    || !ids.Contains(lobby.deciderId ?? "") || lobby.deciderId == player
                    || !Asks.Contains(lobby.ask) || !Approaches.Contains(lobby.approach) || !Responses.Contains(lobby.response)
                    || (lobby.subjectId != null && !ids.Contains(lobby.subjectId)) || !Bound(lobby.influence, 100)) throw Invalid();

            foreach (var reply in replies)
            {
                if (!Text(reply.cardId, 160) || !OptionalText(reply.kind, 64) || !ids.Contains(reply.fromId ?? "")
                    || (!string.IsNullOrEmpty(reply.listenerId) && !ids.Contains(reply.listenerId))
                    || !OptionalText(reply.replyKey, 64) || reply.week < 1 || reply.week > week || !Bound(reply.toThem, 100)) throw Invalid();
                if (reply.kind != Pitch) continue; // Historical non-pitch reply keys are bounded prose, not a closed enum.
                if (economy != 1 || reply.week < windowStart || reply.fromId == player || reply.promised
                    || reply.listenerId == reply.fromId || reply.listenerId == player || !PitchPayoff(reply.replyKey, reply.toThem)) throw Invalid();
            }
            var pitchReplies = replies.Where(r => r.kind == Pitch).ToArray();
            if (pitchReplies.GroupBy(r => r.week).Any(g => g.Count() > 48)
                || pitchReplies.GroupBy(r => new { r.week, r.fromId, inspected = r.replyKey == Inspection }).Any(g => g.Count() > 1)) throw Invalid();

            foreach (var card in cards)
            {
                if (!Text(card.id, 160) || card.week != week || !CardKinds.Contains(card.kind)
                    || !ids.Contains(card.fromId ?? "") || card.fromId == player
                    || (card.aboutId != null && !ids.Contains(card.aboutId))) throw Invalid();
                if (card.kind != Pitch) { if (phase != Social && phase != Campaign) throw Invalid(); continue; }
                if (economy != 1 || !On(windowStart, week) || !On(strategyStart, week) || !On(agencyStart, week)
                    || phase != Nomination || nominees.Count != 0 || hoh != player || !active.Contains(player)
                    || !active.Contains(card.fromId)
                    || (card.aboutId != null && (card.aboutId == card.fromId || card.aboutId == player || !active.Contains(card.aboutId)))
                    || pitchReplies.Any(r => r.week == week && r.fromId == card.fromId && r.replyKey != Inspection)) throw Invalid();
            }
            if (cards.Select(c => c.id).Distinct(StringComparer.Ordinal).Count() != cards.Count
                || cards.Where(c => c.kind == Pitch).GroupBy(c => c.fromId).Any(g => g.Count() > 1)
                || (strategyStart == 0 && (lobbies.Count > 0 || cards.Count > 0))) throw Invalid();

            foreach (var speech in speeches)
                if (!ids.Contains(speech.speakerId ?? "") || speech.text == null || speech.text.Length > 4000
                    || speech.week < 1 || speech.week > week || speech.isPlayerAuthored != (speech.speakerId == player)) throw Invalid();
            if (speeches.GroupBy(s => s.speakerId).Any(g => g.Count() > 1)) throw Invalid();
            foreach (var e in events)
                if (e.sequence < 1 || e.sequence >= nextSequence || e.week < 1 || e.week > week || e.phase < 0 || e.phase > 15
                    || !Text(e.kind, 100) || !Text(e.text, 4000) || e.audienceIds == null
                    || e.audienceIds.Any(id => id == null || !ids.Contains(id))) throw Invalid();
            if (events.GroupBy(e => e.sequence).Any(g => g.Count() > 1)) throw Invalid();
            var receipts = events.Where(e => e.kind.StartsWith(SpeechPrefix, StringComparison.Ordinal)).ToArray();
            foreach (var e in receipts)
            {
                string approach = e.kind.Substring(SpeechPrefix.Length);
                if (economy != 1 || (approach != Quiet && !Approaches.Contains(approach)) || e.phase != Eviction
                    || windowStart < 1 || e.week < windowStart || strategyStart < 1 || e.week < strategyStart
                    || agencyStart < 1 || e.week < agencyStart || leverStart < 1 || e.week < leverStart
                    || e.audienceIds.Count < 3 || e.audienceIds.Count > 16
                    || e.audienceIds.Distinct(StringComparer.Ordinal).Count() != e.audienceIds.Count
                    || !e.audienceIds.Contains(player) || (approach == Quiet && e.text != "No speech was given.")) throw Invalid();
                if (e.week != week) continue; // History survives the cleared current-week speech and nominee rows.
                string speaker = e.audienceIds[0];
                var speech = speeches.FirstOrDefault(s => s.week == e.week && s.speakerId == speaker);
                if ((phase != Eviction && !(phase == Social && resolved)) || (phase == Eviction && stage == 0)
                    || speech == null || !nominees.Contains(speaker)
                    || e.text != (string.IsNullOrWhiteSpace(speech.text) ? "No speech was given." : speech.text)
                    || (approach == Quiet) != string.IsNullOrWhiteSpace(speech.text)) throw Invalid();
                var audience = new[] { speaker }.Concat(active.Where(id => id != speaker))
                    .Concat(active.Contains(player) ? new string[0] : new[] { player }).Distinct(StringComparer.Ordinal);
                if (!resolved && phase == Eviction && !e.audienceIds.SequenceEqual(audience)) throw Invalid();
            }
            if (receipts.GroupBy(e => new { e.week, speaker = e.audienceIds[0] }).Any(g => g.Count() > 1)
                || receipts.GroupBy(e => e.week).Any(g => g.Count() > 2)) throw Invalid();

            var previous = (JObject)original.DeepClone();
            foreach (var row in ((JArray)previous["replyCards"]).OfType<JObject>().Where(r => (string)r["kind"] == Pitch).ToArray()) row.Remove();
            foreach (var row in ((JArray)previous["lobbies"]).OfType<JObject>()
                .Where(r => (int)r["phase"] == Campaign || (string)r["ask"] == "vote").ToArray()) row.Remove();
            previous.Remove("economyRulesVersion"); previous.Remove("moveInExtrasSpent"); previous["schemaVersion"] = 22;
            FrozenEpisodeV22.Validate(previous);
            return previous;
        }

        private static List<T> Rows<T>(JToken token, int max, string name)
        {
            var list = Array(token, max);
            var result = new List<T>();
            foreach (var row in list)
            {
                if (!(row is JObject)) throw Invalid();
                SaveJson.CheckDtoShape(row, typeof(T), "state(v23)." + name + "[]");
                result.Add(row.ToObject<T>(SaveJson.Serializer()));
            }
            return result;
        }
        private static JArray Array(JToken token, int max)
        { if (!(token is JArray array) || array.Count > max) throw Invalid(); return array; }
        private static List<string> Identifiers(JToken token, HashSet<string> ids, int max, bool distinct)
        {
            var values = Array(token, max).Select(t => Key(t, 100)).ToList();
            if (values.Any(id => !ids.Contains(id)) || (distinct && values.Distinct(StringComparer.Ordinal).Count() != values.Count)) throw Invalid();
            return values;
        }
        private static int Number(JToken token, int min, int max)
        {
            if (token?.Type != JTokenType.Integer || !long.TryParse(token.ToString(), NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out long value) || value < min || value > max) throw Invalid();
            return (int)value;
        }
        private static bool Boolean(JToken token)
        { if (token?.Type != JTokenType.Boolean) throw Invalid(); return (bool)token; }
        private static string Key(JToken token, int max)
        { if (token?.Type != JTokenType.String || !Text((string)token, max)) throw Invalid(); return (string)token; }
        private static string NullableText(JToken token, int max)
        {
            if (token == null || (token.Type != JTokenType.Null && (token.Type != JTokenType.String || ((string)token).Length > max))) throw Invalid();
            return token.Type == JTokenType.Null ? null : (string)token;
        }
        private static bool Text(string value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max;
        private static bool OptionalText(string value, int max) => value == null || value.Length <= max;
        private static bool Bound(double value, double max) => !double.IsNaN(value) && !double.IsInfinity(value) && Math.Abs(value) <= max;
        private static bool On(int start, int week) => start >= 1 && week >= start;
        private static bool PitchPayoff(string key, double value) => key == Inspection ? value == 0
            : key == "promise-safety" ? value == 8 : key == "hear" ? value == 0 : key == "turn-down" && value == -5;
        private static InvalidDataException Invalid() => new InvalidDataException("Invalid historical schema 23 data.");
    }
}
