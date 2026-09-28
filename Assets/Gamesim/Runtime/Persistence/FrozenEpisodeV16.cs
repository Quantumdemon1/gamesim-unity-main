using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Schema 16 was schema 15 plus the story bundle and the story fields on house events, their
    /// choices, storylines and modifiers. What is frozen here is the shape: the bundle's field set
    /// and each field's kind and bound, and the per-record story fields, as schema 16 wrote them.
    /// The rows' meanings are checked by the live story validator after migration, which is safe
    /// because the step to 17 does not touch them: it adds the season ledger and nothing else.
    /// </summary>
    internal static class FrozenEpisodeV16
    {
        private static readonly string[] Fields = { "story" };

        /// <summary>The story bundle's fields, as schema 16 wrote them: scalars first, then the lists.</summary>
        private static readonly string[] Scalars = { "rulesStartWeek", "rulesVersion", "productionStrictness", "romanceStorylines", "pendingRemovalId" };
        private static readonly string[] Lists =
            { "grudges", "facts", "bonds", "hooks", "contacts", "lore", "knownFacts", "conduct", "removals", "cooldowns", "reckonings" };
        private const int MostRows = 4096;

        private static readonly string[] EventFields = { "contentId", "cycleId", "closesAnchor", "surface", "venue", "lapseOptionId", "cast" };
        private static readonly string[] ChoiceFields =
        {
            "optionId", "glyph", "approach", "checkBase", "subjectId", "lapse", "costsAction", "conduct", "pickPerson", "eligibleIds",
            "locked", "lockReason", "bonusTraits", "against", "effects", "backfire", "next", "nextOnBackfire",
        };
        private static readonly string[] StorylineFields = { "beatId", "lane", "variant", "cast", "path", "vars", "nextWeek", "nextAnchor", "endingId" };
        private static readonly string[] ModifierFields = { "ownerId" };

        internal static void Validate(JObject original)
        {
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer || (long)original["schemaVersion"] != 16)
                throw new InvalidDataException("Expected simulation schema version 16.");
            var previous = (JObject)original.DeepClone();
            if (Fields.Any(name => previous.Property(name) == null)) throw new InvalidDataException("Missing schema 16 field.");
            if (previous["week"]?.Type != JTokenType.Integer) throw Invalid();
            long week = (long)previous["week"];

            if (!(previous["story"] is JObject story)) throw Invalid();
            var expected = Scalars.Concat(Lists).OrderBy(name => name, StringComparer.Ordinal);
            if (!story.Properties().Select(p => p.Name).OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(expected)) throw Invalid();
            foreach (var name in new[] { "rulesStartWeek", "rulesVersion", "productionStrictness" })
                if (story[name].Type != JTokenType.Integer) throw Invalid();
            if ((long)story["rulesStartWeek"] < 0 || (long)story["rulesStartWeek"] > Math.Min(101, week + 1)) throw Invalid();
            if (story["romanceStorylines"].Type != JTokenType.Boolean) throw Invalid();
            if (story["pendingRemovalId"].Type != JTokenType.Null && (story["pendingRemovalId"].Type != JTokenType.String || ((string)story["pendingRemovalId"]).Length > 160)) throw Invalid();
            foreach (var name in Lists)
                if (!(story[name] is JArray list) || list.Count > MostRows) throw Invalid();

            foreach (var item in Records(previous, "houseEvents"))
            {
                Present(item, EventFields);
                if (!(item["cast"] is JArray)) throw Invalid();
                foreach (var choice in Records(item, "choices"))
                {
                    Present(choice, ChoiceFields);
                    if (choice["checkBase"].Type != JTokenType.Float && choice["checkBase"].Type != JTokenType.Integer) throw Invalid();
                    foreach (var flag in new[] { "lapse", "costsAction", "conduct", "pickPerson", "locked" })
                        if (choice[flag].Type != JTokenType.Boolean) throw Invalid();
                    foreach (var list in new[] { "eligibleIds", "bonusTraits", "against", "effects", "backfire" })
                        if (!(choice[list] is JArray)) throw Invalid();
                }
            }
            foreach (var item in Records(previous, "storylines"))
            {
                Present(item, StorylineFields);
                foreach (var number in new[] { "variant", "nextWeek" }) if (item[number].Type != JTokenType.Integer) throw Invalid();
                foreach (var list in new[] { "cast", "path", "vars" }) if (!(item[list] is JArray)) throw Invalid();
            }
            foreach (var item in Records(previous, "activeModifiers"))
            {
                Present(item, ModifierFields);
                if (item["ownerId"].Type != JTokenType.String) throw Invalid();
            }

            // Strip what schema 16 added and hand the rest to the schema 15 contract.
            foreach (var name in Fields) previous.Remove(name);
            foreach (var item in Records(previous, "houseEvents"))
            {
                foreach (var name in EventFields) item.Remove(name);
                foreach (var choice in Records(item, "choices")) foreach (var name in ChoiceFields) choice.Remove(name);
            }
            foreach (var item in Records(previous, "storylines")) foreach (var name in StorylineFields) item.Remove(name);
            foreach (var item in Records(previous, "activeModifiers")) foreach (var name in ModifierFields) item.Remove(name);
            previous["schemaVersion"] = 15;
            FrozenEpisodeV15.Validate(previous);
        }

        private static JObject[] Records(JObject owner, string name)
        {
            if (!(owner[name] is JArray list)) throw Invalid();
            if (list.Any(item => !(item is JObject))) throw Invalid();
            return list.OfType<JObject>().ToArray();
        }

        private static void Present(JObject record, string[] names)
        {
            foreach (var name in names) if (record.Property(name) == null) throw Invalid();
        }

        private static InvalidDataException Invalid() => new InvalidDataException("Invalid historical schema 16 data.");
    }
}
