using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Schema 13 was schema 12 plus exactly three fields: <c>competitionRulesVersion</c>, and each
    /// contestant's <c>sourceTemplateId</c> and <c>appearance</c>. Frozen the moment schema 14 was
    /// cut: those three are validated against their own frozen shapes, and the rest is handed down
    /// to schema 12's contract.
    /// </summary>
    internal static class FrozenEpisodeV13
    {
#pragma warning disable 0649
        /// <summary>The appearance recipe as schema 13 stored it: frozen, not following the runtime type.</summary>
        private sealed class Appearance
        {
            public int version;
            public string provider, presetId, bodyId, fallbackId, activeOutfit;
            public List<Value> dna;
            public List<Color> colors;
            public List<Outfit> outfits;
        }
        private sealed class Value { public string id; public float value; }
        private sealed class Color { public string id; public float r, g, b, a; }
        private sealed class Wardrobe { public string slot, itemId; }
        private sealed class Outfit { public string id; public List<Wardrobe> wardrobe; public List<Color> colors; }
#pragma warning restore 0649

        internal static void Validate(JObject original)
        {
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer || (long)original["schemaVersion"] != 13)
                throw new InvalidDataException("Expected simulation schema version 13.");
            if (original["competitionRulesVersion"]?.Type != JTokenType.Integer
                || (long)original["competitionRulesVersion"] < 1 || (long)original["competitionRulesVersion"] > 3)
                throw new InvalidDataException("Invalid schema 13 competition rules version.");
            if (!(original["contestants"] is JArray people)) throw new InvalidDataException("Missing schema 13 contestants.");

            var previous = (JObject)original.DeepClone();
            previous.Remove("competitionRulesVersion");
            foreach (JObject person in ((JArray)previous["contestants"]).OfType<JObject>())
            {
                if (person.Property("sourceTemplateId") == null || person.Property("appearance") == null)
                    throw new InvalidDataException("Missing schema 13 contestant field.");
                person.Remove("sourceTemplateId");
                person.Remove("appearance");
            }
            previous["schemaVersion"] = 12;
            FrozenEpisodeV12.Validate(previous);

            try
            {
                foreach (JObject person in people.OfType<JObject>())
                {
                    var template = person["sourceTemplateId"];
                    if (template.Type != JTokenType.Null && (template.Type != JTokenType.String || ((string)template).Length > 160))
                        throw Invalid();
                    var appearance = person["appearance"];
                    if (appearance.Type == JTokenType.Null) continue;
                    SaveJson.CheckDtoShape(appearance, typeof(Appearance), "state(v13).contestants[].appearance");
                    var recipe = appearance.ToObject<Appearance>(SaveJson.Serializer());
                    if (recipe.version != 1 || !Key(recipe.provider) || !Key(recipe.presetId, true) || !Key(recipe.bodyId, true)
                        || !Key(recipe.fallbackId) || !Key(recipe.activeOutfit)) throw Invalid();
                    if (recipe.dna == null || recipe.dna.Count > 128 || recipe.dna.Any(x => x == null || !Key(x.id) || !Unit(x.value))
                        || recipe.dna.Select(x => x.id).Distinct(StringComparer.Ordinal).Count() != recipe.dna.Count) throw Invalid();
                    if (!Colors(recipe.colors)) throw Invalid();
                    if (recipe.outfits == null || recipe.outfits.Count > 8 || recipe.outfits.Any(o => o == null || !Key(o.id) || !Colors(o.colors)
                            || o.wardrobe == null || o.wardrobe.Count > 32 || o.wardrobe.Any(w => w == null || !Key(w.slot) || !Key(w.itemId, true))
                            || o.wardrobe.Select(w => w.slot).Distinct(StringComparer.Ordinal).Count() != o.wardrobe.Count)
                        || recipe.outfits.Select(o => o.id).Distinct(StringComparer.Ordinal).Count() != recipe.outfits.Count
                        || (recipe.outfits.Count > 0 && !recipe.outfits.Any(o => o.id == recipe.activeOutfit))) throw Invalid();
                }
            }
            catch (Exception error) when (error is JsonException || error is OverflowException)
            { throw new InvalidDataException("Historical schema 13 data exceeds its frozen contract.", error); }
        }

        private static bool Colors(List<Color> values) => values != null && values.Count <= 64
            && values.All(x => x != null && Key(x.id) && Unit(x.r) && Unit(x.g) && Unit(x.b) && Unit(x.a))
            && values.Select(x => x.id).Distinct(StringComparer.Ordinal).Count() == values.Count;
        private static bool Unit(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= 1;
        private static bool Key(string value, bool empty = false) => value != null && value.Length <= 128
            && (empty || !string.IsNullOrWhiteSpace(value)) && !value.Any(char.IsControl) && value.IndexOfAny(new[] { '/', '\\', ':' }) < 0;
        private static InvalidDataException Invalid() => new InvalidDataException("Invalid historical schema 13 contestant data.");
    }
}
