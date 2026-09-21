using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;

namespace Gamesim.Uma
{
    /// <summary>Capabilities come from the installed assets; saved selections remain plain IDs.</summary>
    public sealed class UmaAppearanceCatalog : ICharacterAppearanceCatalog
    {
        private readonly List<AppearanceBodyOption> bodies = new List<AppearanceBodyOption>();
        private readonly List<AppearanceItem> items = new List<AppearanceItem>();
        private readonly List<AppearanceControl> controls = new List<AppearanceControl>();
        private readonly Dictionary<string, string> recipeNames = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string[]> dnaByBody = new Dictionary<string, string[]>(StringComparer.Ordinal);
        private readonly List<string> diagnostics = new List<string>();
        public IReadOnlyList<string> Diagnostics => diagnostics;
        public IReadOnlyList<AppearanceBodyOption> Bodies => bodies;
        public IReadOnlyList<AppearanceItem> Items => items;
        public IReadOnlyList<AppearanceControl> Controls => controls;

        public UmaAppearanceCatalog(IEnumerable<UmaWardrobeCatalog> content = null)
        {
            var index = UMAAssetIndexer.Instance;
            if (index == null) return;
            AddBody(index, UmaCastLibrary.FemaleRace, "Body A");
            AddBody(index, UmaCastLibrary.MaleRace, "Body B");
            foreach (var body in bodies) dnaByBody[body.Id] = index.GetRace(body.Id).GetDNANames().Distinct().ToArray();
            foreach (var control in new[]
            {
                new AppearanceControl("height", "Height", "Body", .08f, .34f),
                new AppearanceControl("headSize", "Head size", "Body", .5f, .8f),
                new AppearanceControl("upperWeight", "Upper body fullness", "Body"),
                new AppearanceControl("lowerWeight", "Lower body fullness", "Body"),
                new AppearanceControl("upperMuscle", "Upper body definition", "Body"),
                new AppearanceControl("lowerMuscle", "Lower body definition", "Body"),
                new AppearanceControl("waist", "Waist", "Body"),
                new AppearanceControl("armWidth", "Arm width", "Body"),
                new AppearanceControl("legsSize", "Leg width", "Body"),
                new AppearanceControl("headWidth", "Face width", "Face"),
                new AppearanceControl("jawSize", "Jaw", "Face"),
                new AppearanceControl("chinSize", "Chin", "Face"),
                new AppearanceControl("cheekSize", "Cheeks", "Face"),
                new AppearanceControl("cheekPosition", "Cheek height", "Face"),
                new AppearanceControl("noseSize", "Nose size", "Face"),
                new AppearanceControl("noseWidth", "Nose width", "Face"),
                new AppearanceControl("noseCurve", "Nose profile", "Face"),
                new AppearanceControl("eyeSize", "Eye size", "Face"),
                new AppearanceControl("eyeSpacing", "Eye spacing", "Face"),
                new AppearanceControl("mouthSize", "Mouth width", "Face"),
                new AppearanceControl("lipsSize", "Lip fullness", "Face"),
                new AppearanceControl("earsSize", "Ear size", "Face"),
            })
            {
                control.CompatibleBodies = bodies.Where(body => dnaByBody[body.Id].Contains(control.Id)).Select(body => body.Id).ToArray();
                if (control.CompatibleBodies.Length > 0) controls.Add(control);
            }

            var metadata = new Dictionary<string, UmaWardrobeEntry>(StringComparer.Ordinal);
            var knownKeys = new HashSet<string>(StringComparer.Ordinal);
            var catalogs = (content ?? Resources.LoadAll<UmaWardrobeCatalog>("Gamesim/CharacterCatalog")).Where(value => value != null).ToArray();
            foreach (var asset in catalogs.OrderBy(value => value.name, StringComparer.Ordinal))
            {
                if (asset.version != 1) { diagnostics.Add("Unsupported wardrobe catalog version: " + asset.name); continue; }
                foreach (var entry in asset.entries)
                {
                    if (entry == null || !ValidId(entry.id) || string.IsNullOrWhiteSpace(entry.recipeName))
                    { diagnostics.Add("Invalid wardrobe entry in " + asset.name); continue; }
                    var keys = new[] { entry.id, entry.recipeName }.Concat(entry.aliases ?? new List<string>()).Distinct().ToArray();
                    if (keys.Any(key => !ValidId(key) || knownKeys.Contains(key)) || metadata.ContainsKey(entry.recipeName))
                    { diagnostics.Add("Duplicate wardrobe ID or alias: " + entry.id); continue; }
                    foreach (string key in keys) { knownKeys.Add(key); recipeNames[key] = entry.recipeName; }
                    metadata.Add(entry.recipeName, entry);
                }
            }

            var slots = new HashSet<string> { "Hair", "Beard", "Eyebrows", "Chest", "Legs", "Feet", "BottomUnderlayer", "TopUnderlayer" };
            foreach (var recipe in index.GetAllAssets<UMAWardrobeRecipe>().OrderBy(recipe => recipe.name, StringComparer.Ordinal))
            {
                if (recipe == null || !slots.Contains(recipe.wardrobeSlot)) continue;
                metadata.TryGetValue(recipe.name, out var entry);
                // Authored catalogs are an allowlist. Optional UMA installations with no catalog retain discovery.
                if (catalogs.Length > 0 && (entry == null || !entry.available)) continue;
                var compatible = bodies.Where(body => recipe.compatibleRaces.Contains(body.Id)).Select(body => body.Id).ToArray();
                if (compatible.Length == 0) continue;
                // Fantasy/creature pieces are not part of the human house wardrobe.
                string name = recipe.name.ToLowerInvariant();
                if (entry == null && new[] { "orc", "elf", "horn", "armor", "armour", "robot", "zombie" }.Any(name.Contains)) continue;
                var packed = recipe.PackedLoad();
                var dyes = packed.fColors != null
                    ? packed.fColors.Take(packed.sharedColorCount).Select(color => color.name)
                    : (packed.colors ?? Array.Empty<UMAPackedRecipeBase.PackedOverlayColorDataV2>()).Take(packed.sharedColorCount).Select(color => color.name);
                items.Add(new AppearanceItem
                {
                    Id = entry?.id ?? recipe.name, Slot = recipe.wardrobeSlot,
                    Aliases = new[] { recipe.name }.Concat(entry?.aliases ?? new List<string>()).Distinct().ToArray(),
                    StyleGroup = entry?.styleGroup,
                    Tags = entry?.tags?.ToArray() ?? Array.Empty<string>(),
                    FallbackPriority = entry?.fallbackPriority ?? 100,
                    Label = !string.IsNullOrWhiteSpace(entry?.label) ? entry.label
                        : string.IsNullOrWhiteSpace(recipe.DisplayValue) ? Label(recipe.name) : recipe.DisplayValue,
                    CompatibleBodies = compatible,
                    SuppressedSlots = recipe.suppressWardrobeSlots.ToArray(),
                    Conflicts = recipe.IncompatibleRecipes.Where(other => other != null).Select(other => other.name).ToArray(),
                    ColorChannels = dyes.Where(channel => !string.IsNullOrEmpty(channel)).Distinct().ToArray(),
                    Thumbnail = recipe.GetWardrobeRecipeThumbFor(compatible[0]),
                });
            }
        }

        private void AddBody(UMAAssetIndexer index, string id, string label)
        { if (index.GetRace(id) != null) bodies.Add(new AppearanceBodyOption(id, label)); }

        private static string Label(string name) => Regex.Replace(name.Replace("_Recipe", "").Replace("_", " "), "([a-z])([A-Z])", "$1 $2");
        private static bool ValidId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 128
            && id.IndexOfAny(new[] { '/', '\\', ':' }) < 0 && !id.Any(char.IsControl);

        public string ResolveRecipeName(string id) => id != null && recipeNames.TryGetValue(id, out var recipe) ? recipe : id;

        public AppearanceItem FindEquivalent(string itemId, string slot, string bodyId)
        {
            var original = AppearanceEditing.Find(this, itemId);
            return items.Where(item => item.Slot == slot && item.Fits(bodyId))
                .OrderByDescending(item => original != null && !string.IsNullOrEmpty(original.StyleGroup) && item.StyleGroup == original.StyleGroup)
                .ThenByDescending(item => original == null ? 0 : item.Tags.Intersect(original.Tags).Count())
                .ThenBy(item => item.FallbackPriority).ThenBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault();
        }

        public CharacterAppearance Materialize(CharacterAppearance source)
        {
            var appearance = source?.Clone() ?? new CharacterAppearance();
            if (!string.IsNullOrEmpty(appearance.bodyId)) return appearance;
            var template = CastTemplates.Find(appearance.presetId);
            if (template != null)
                appearance.fallbackId = CharacterPresentation.AppearanceId(CastTemplates.ToContestant(template, false), template.Id);
            if (!UmaCastLibrary.Resolve(appearance.presetId, appearance.fallbackId, out var look))
                UmaCastLibrary.TryGet(ContentCatalog.PlayerId, out look);
            appearance.provider = "uma";
            appearance.bodyId = look.Race;
            // Persist every control on this base, not only the differences from today's preset.
            var index = UMAAssetIndexer.Instance;
            var race = index?.GetRace(look.Race);
            var names = race?.GetDNANames() ?? new List<string>();
            foreach (string id in names)
                if (!appearance.dna.Any(value => value.id == id))
                    AppearanceEditing.SetValue(appearance, id, look.Dna.TryGetValue(id, out var value) ? value
                        : UmaCastLibrary.HouseProportions.TryGetValue(id, out var house) ? house
                        : race != null && race.useNewDNA && race.DNACollection.dnaDictionary.TryGetValue(id, out var dna)
                            ? dna.defaultValue : .5f);
            if (!appearance.colors.Any(value => value.id == "Skin")) AppearanceEditing.SetColor(appearance, "Skin", look.Skin);
            if (!appearance.colors.Any(value => value.id == "Hair")) AppearanceEditing.SetColor(appearance, "Hair", look.Hair);
            if (!appearance.colors.Any(value => value.id == "Brows")) AppearanceEditing.SetColor(appearance, "Brows", look.Brows);
            if (!appearance.colors.Any(value => value.id == "Eyes")) AppearanceEditing.SetColor(appearance, "Eyes", look.Eyes);
            var outfit = AppearanceEditing.Outfit(appearance);
            if (outfit.wardrobe.Count == 0)
                foreach (string id in look.Wardrobe)
                {
                    var recipe = index?.GetAsset<UMAWardrobeRecipe>(id);
                    if (recipe != null) outfit.wardrobe.Add(new AppearanceWardrobe { slot = recipe.wardrobeSlot, itemId = AppearanceEditing.Find(this, id)?.Id ?? id });
                    // Missing selections retain their ID too; installing the content later restores them.
                    else outfit.wardrobe.Add(new AppearanceWardrobe { slot = "Missing-" + outfit.wardrobe.Count, itemId = id });
                }
            return appearance;
        }

        public CharacterAppearance ChangeBody(CharacterAppearance source, string bodyId)
        {
            var appearance = Materialize(source);
            if (!bodies.Any(body => body.Id == bodyId)) return appearance;
            appearance.bodyId = bodyId;
            var race = UMAAssetIndexer.Instance.GetRace(bodyId);
            foreach (string id in dnaByBody[bodyId])
                if (!appearance.dna.Any(value => value.id == id))
                    AppearanceEditing.SetValue(appearance, id, race.useNewDNA && race.DNACollection.dnaDictionary.TryGetValue(id, out var dna) ? dna.defaultValue : .5f);
            foreach (var outfit in appearance.outfits)
                for (int i = 0; i < outfit.wardrobe.Count; i++)
                {
                    var worn = outfit.wardrobe[i];
                    var item = AppearanceEditing.Find(this, worn.itemId);
                    if (item == null || item.Fits(bodyId)) continue;
                    var substitute = FindEquivalent(worn.itemId, worn.slot, bodyId);
                    if (substitute != null) worn.itemId = substitute.Id;
                    else { outfit.wardrobe.RemoveAt(i); i--; }
                }
            return appearance;
        }
    }
}
