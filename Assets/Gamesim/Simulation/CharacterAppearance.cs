using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Gamesim.Simulation
{
    /// <summary>A provider-neutral, saved recipe. Asset identifiers are catalog keys, never paths.</summary>
    [Serializable]
    public sealed class CharacterAppearance
    {
        public int version = 1;
        public string provider = "auto", presetId = "player", bodyId = "", fallbackId = "player";
        public string activeOutfit = "Everyday";
        public List<AppearanceValue> dna = new List<AppearanceValue>();
        public List<AppearanceColor> colors = new List<AppearanceColor>();
        public List<CharacterOutfit> outfits = new List<CharacterOutfit>();

        public static CharacterAppearance Preset(string id) => new CharacterAppearance
        { presetId = string.IsNullOrEmpty(id) ? "player" : id, fallbackId = string.IsNullOrEmpty(id) ? "player" : id };

        public CharacterAppearance Clone()
        {
            var copy = (CharacterAppearance)MemberwiseClone();
            copy.dna = dna?.Select(x => x?.Clone()).ToList();
            copy.colors = colors?.Select(x => x?.Clone()).ToList();
            copy.outfits = outfits?.Select(x => x?.Clone()).ToList();
            return copy;
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (version != 1 || !Key(provider) || !Key(presetId, true) || !Key(bodyId, true)
                || !Key(fallbackId) || !Key(activeOutfit)) return Fail(out error, "Invalid appearance identity or version.");
            if (dna == null || dna.Count > 128 || dna.Any(x => x == null || !Key(x.id) || !Unit(x.value))
                || dna.Select(x => x.id).Distinct(StringComparer.Ordinal).Count() != dna.Count)
                return Fail(out error, "Appearance sliders must have unique names and finite values between zero and one.");
            if (!ValidColors(colors)) return Fail(out error, "Invalid appearance colors.");
            if (outfits == null || outfits.Count > 8 || outfits.Any(x => x == null || !Key(x.id))
                || outfits.Select(x => x.id).Distinct(StringComparer.Ordinal).Count() != outfits.Count)
                return Fail(out error, "An appearance supports up to eight uniquely named outfits.");
            foreach (var outfit in outfits)
                if (!ValidColors(outfit.colors) || outfit.wardrobe == null || outfit.wardrobe.Count > 32
                    || outfit.wardrobe.Any(x => x == null || !Key(x.slot) || !Key(x.itemId, true))
                    || outfit.wardrobe.Select(x => x.slot).Distinct(StringComparer.Ordinal).Count() != outfit.wardrobe.Count)
                    return Fail(out error, "Outfit items must occupy unique slots with valid catalog identifiers.");
            if (outfits.Count > 0 && !outfits.Any(x => x.id == activeOutfit))
                return Fail(out error, "The active outfit is missing.");
            return true;
        }

        /// <summary>Stable across list order and cultures; changes with every visible recipe input.</summary>
        public string ContentKey()
        {
            var text = new StringBuilder();
            void Add(string value) { value = value ?? ""; text.Append(value.Length).Append(':').Append(value).Append(';'); }
            void Number(float value) => Add(value.ToString("R", CultureInfo.InvariantCulture));
            void Colors(IEnumerable<AppearanceColor> values)
            {
                var ordered = (values ?? Enumerable.Empty<AppearanceColor>()).Where(x => x != null).OrderBy(x => x.id, StringComparer.Ordinal).ToArray();
                Add("color-list"); Add(ordered.Length.ToString(CultureInfo.InvariantCulture));
                foreach (var c in ordered)
                { Add(c.id); Number(c.r); Number(c.g); Number(c.b); Number(c.a); }
            }
            Add(version.ToString(CultureInfo.InvariantCulture)); Add(provider); Add(presetId); Add(bodyId); Add(fallbackId); Add(activeOutfit);
            Add("dna-list"); Add((dna?.Count ?? 0).ToString(CultureInfo.InvariantCulture));
            foreach (var d in (dna ?? new List<AppearanceValue>()).Where(x => x != null).OrderBy(x => x.id, StringComparer.Ordinal))
            { Add(d.id); Number(d.value); }
            Add("colors"); Colors(colors);
            Add("outfit-list"); Add((outfits?.Count ?? 0).ToString(CultureInfo.InvariantCulture));
            foreach (var o in (outfits ?? new List<CharacterOutfit>()).Where(x => x != null).OrderBy(x => x.id, StringComparer.Ordinal))
            {
                Add("outfit"); Add(o.id);
                Add("wardrobe-list"); Add((o.wardrobe?.Count ?? 0).ToString(CultureInfo.InvariantCulture));
                foreach (var w in (o.wardrobe ?? new List<AppearanceWardrobe>()).Where(x => x != null).OrderBy(x => x.slot, StringComparer.Ordinal))
                { Add(w.slot); Add(w.itemId); }
                Add("colors"); Colors(o.colors);
            }
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
        }

        private static bool ValidColors(List<AppearanceColor> values) => values != null && values.Count <= 64
            && values.All(x => x != null && Key(x.id) && Unit(x.r) && Unit(x.g) && Unit(x.b) && Unit(x.a))
            && values.Select(x => x.id).Distinct(StringComparer.Ordinal).Count() == values.Count;
        private static bool Unit(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= 1;
        private static bool Key(string value, bool empty = false) => value != null && value.Length <= 128
            && (empty || !string.IsNullOrWhiteSpace(value)) && !value.Any(char.IsControl)
            && value.IndexOfAny(new[] { '/', '\\', ':' }) < 0;
        private static bool Fail(out string error, string message) { error = message; return false; }
    }

    [Serializable] public sealed class AppearanceValue
    { public string id; public float value; public AppearanceValue Clone() => (AppearanceValue)MemberwiseClone(); }
    [Serializable] public sealed class AppearanceColor
    { public string id; public float r, g, b, a = 1; public AppearanceColor Clone() => (AppearanceColor)MemberwiseClone(); }
    [Serializable] public sealed class AppearanceWardrobe
    { public string slot, itemId; public AppearanceWardrobe Clone() => (AppearanceWardrobe)MemberwiseClone(); }
    [Serializable] public sealed class CharacterOutfit
    {
        public string id = "Everyday";
        public List<AppearanceWardrobe> wardrobe = new List<AppearanceWardrobe>();
        public List<AppearanceColor> colors = new List<AppearanceColor>();
        public CharacterOutfit Clone() => new CharacterOutfit
        { id = id, wardrobe = wardrobe?.Select(x => x?.Clone()).ToList(), colors = colors?.Select(x => x?.Clone()).ToList() };
    }
}
