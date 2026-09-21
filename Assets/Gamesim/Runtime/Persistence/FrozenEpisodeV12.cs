using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    /// <summary>Schema 12 was schema 11 plus exactly these three story fields.</summary>
    internal static class FrozenEpisodeV12
    {
#pragma warning disable 0649
        private sealed class Storyline
        { public string id, templateId, category, title, eventId, status; public int week, endedWeek; }
        private sealed class Modifier
        { public string id, name, description; public int weeksLeft; public double competitionBonus, socialBonus; }
        private sealed class StoryData
        { public List<Storyline> storylines; public List<Modifier> activeModifiers; public int storyRulesStartWeek; }
#pragma warning restore 0649

        internal static void Validate(JObject original)
        {
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer || (long)original["schemaVersion"] != 12)
                throw new InvalidDataException("Expected simulation schema version 12.");
            var previous = (JObject)original.DeepClone();
            var stories = new JObject();
            foreach (var field in new[] { "storylines", "activeModifiers", "storyRulesStartWeek" })
            {
                if (previous.Property(field) == null) throw new InvalidDataException("Missing schema 12 story field: " + field);
                stories.Add(field, previous[field].DeepClone()); previous.Remove(field);
            }
            previous["schemaVersion"] = 11;
            // Schema 12 accepted 18 legacy actions plus six purchased actions. Validate that
            // historical bound here, then project only these two fields into v11's narrower
            // scalar contract. The original payload and v11 validator remain untouched.
            foreach (string field in new[] { "socialActions", "outOfPhaseSocialActions" })
            {
                if (original[field]?.Type != JTokenType.Integer || (long)original[field] < 0 || (long)original[field] > 24)
                    throw new InvalidDataException("Invalid schema 12 action counter: " + field);
                previous[field] = Math.Min(18, (int)original[field]);
            }
            FrozenEpisodeV11.Validate(previous);
            SaveJson.CheckDtoShape(stories, typeof(StoryData), "state(v12).stories");
            try
            {
                int week = (int)original["week"];
                var data = stories.ToObject<StoryData>(SaveJson.Serializer());
                if (data.storyRulesStartWeek < 1 || data.storyRulesStartWeek > Math.Min(101, week + 1)) throw Invalid();
                if (data.storylines == null || data.storylines.Count > 100 || data.storylines.Any(x => x == null
                    || !Text(x.id, 160) || !Text(x.templateId, 160) || !Text(x.title, 200)
                    || !Optional(x.category, 80) || !Optional(x.eventId, 160)
                    || (x.status != "active" && x.status != "completed" && x.status != "abandoned")
                    || x.week < 1 || x.week > week
                    || (x.status == "active" ? x.endedWeek != 0 : x.endedWeek < x.week || x.endedWeek > week))
                    || data.storylines.GroupBy(x => x.id).Any(g => g.Count() > 1)) throw Invalid();
                if (data.activeModifiers == null || data.activeModifiers.Count > 40 || data.activeModifiers.Any(x => x == null
                    || !Text(x.id, 160) || !Text(x.name, 120) || !Optional(x.description, 500)
                    || x.weeksLeft < 1 || x.weeksLeft > 20 || !Bound(x.competitionBonus, 20) || !Bound(x.socialBonus, 100))) throw Invalid();
            }
            catch (Exception error) when (error is JsonException || error is OverflowException)
            { throw new InvalidDataException("Historical schema 12 data exceeds its frozen numeric contract.", error); }
        }
        private static bool Text(string value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max;
        private static bool Optional(string value, int max) => value == null || value.Length <= max;
        private static bool Bound(double value, double max) => !double.IsNaN(value) && !double.IsInfinity(value) && Math.Abs(value) <= max;
        private static InvalidDataException Invalid() => new InvalidDataException("Invalid historical schema 12 story data.");
    }
}
