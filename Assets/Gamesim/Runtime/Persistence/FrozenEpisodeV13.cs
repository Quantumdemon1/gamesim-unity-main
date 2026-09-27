using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Schema 13 was schema 12 plus the competition rules version and each contestant's source
    /// template and appearance. Its rules ran from 1 to 4: rules 4 arrived without a schema change.
    /// The appearance's shape and checks are copied here as they stood, so a later change to
    /// <c>CharacterAppearance</c> cannot change what a schema 13 save was allowed to hold.
    /// </summary>
    internal static class FrozenEpisodeV13
    {
#pragma warning disable 0649
        private sealed class Appearance
        {
            public int version;
            public string provider, presetId, bodyId, fallbackId, activeOutfit;
            public List<Value> dna;
            public List<Colour> colors;
            public List<Outfit> outfits;
        }
        private sealed class Value { public string id; public float value; }
        private sealed class Colour { public string id; public float r, g, b, a; }
        private sealed class Wardrobe { public string slot, itemId; }
        private sealed class Outfit { public string id; public List<Wardrobe> wardrobe; public List<Colour> colors; }
#pragma warning restore 0649

        internal static void Validate(JObject original)
        {
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer || (long)original["schemaVersion"] != 13)
                throw new InvalidDataException("Expected simulation schema version 13.");
            var previous = (JObject)original.DeepClone();
            var rules = previous.Property("competitionRulesVersion");
            if (rules == null || rules.Value.Type != JTokenType.Integer || (long)rules.Value < 1 || (long)rules.Value > 4)
                throw new InvalidDataException("Invalid schema 13 competition rules version.");
            previous.Remove("competitionRulesVersion");
            if (!(previous["contestants"] is JArray people)) throw new InvalidDataException("Missing schema 13 contestants.");
            foreach (var token in people)
            {
                if (!(token is JObject person)) throw Invalid();
                var template = person.Property("sourceTemplateId");
                var appearance = person.Property("appearance");
                if (template == null || appearance == null) throw new InvalidDataException("Missing schema 13 contestant field.");
                if (template.Value.Type != JTokenType.Null
                    && (template.Value.Type != JTokenType.String || ((string)template.Value).Length > 100)) throw Invalid();
                if (appearance.Value.Type != JTokenType.Null) CheckAppearance(appearance.Value);
                person.Remove("sourceTemplateId");
                person.Remove("appearance");
            }
            previous["schemaVersion"] = 12;
            FrozenEpisodeV12.Validate(previous);
        }

        private static void CheckAppearance(JToken token)
        {
            SaveJson.CheckDtoShape(token, typeof(Appearance), "state(v13).appearance");
            try
            {
                var a = token.ToObject<Appearance>(SaveJson.Serializer());
                if (a.version != 1 || !Key(a.provider) || !Key(a.presetId, true) || !Key(a.bodyId, true)
                    || !Key(a.fallbackId) || !Key(a.activeOutfit)) throw Invalid();
                if (a.dna == null || a.dna.Count > 128 || a.dna.Any(x => x == null || !Key(x.id) || !Unit(x.value))
                    || a.dna.Select(x => x.id).Distinct(StringComparer.Ordinal).Count() != a.dna.Count) throw Invalid();
                if (!Colours(a.colors)) throw Invalid();
                if (a.outfits == null || a.outfits.Count > 8 || a.outfits.Any(x => x == null || !Key(x.id))
                    || a.outfits.Select(x => x.id).Distinct(StringComparer.Ordinal).Count() != a.outfits.Count) throw Invalid();
                foreach (var outfit in a.outfits)
                    if (!Colours(outfit.colors) || outfit.wardrobe == null || outfit.wardrobe.Count > 32
                        || outfit.wardrobe.Any(x => x == null || !Key(x.slot) || !Key(x.itemId, true))
                        || outfit.wardrobe.Select(x => x.slot).Distinct(StringComparer.Ordinal).Count() != outfit.wardrobe.Count) throw Invalid();
                if (a.outfits.Count > 0 && !a.outfits.Any(x => x.id == a.activeOutfit)) throw Invalid();
            }
            catch (Exception error) when (error is JsonException || error is OverflowException)
            { throw new InvalidDataException("Historical schema 13 appearance exceeds its frozen contract.", error); }
        }

        private static bool Colours(List<Colour> values) => values != null && values.Count <= 64
            && values.All(x => x != null && Key(x.id) && Unit(x.r) && Unit(x.g) && Unit(x.b) && Unit(x.a))
            && values.Select(x => x.id).Distinct(StringComparer.Ordinal).Count() == values.Count;
        private static bool Unit(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= 1;
        private static bool Key(string value, bool empty = false) => value != null && value.Length <= 128
            && (empty || !string.IsNullOrWhiteSpace(value)) && !value.Any(char.IsControl)
            && value.IndexOfAny(new[] { '/', '\\', ':' }) < 0;
        private static InvalidDataException Invalid() => new InvalidDataException("Invalid historical schema 13 data.");
    }
}
