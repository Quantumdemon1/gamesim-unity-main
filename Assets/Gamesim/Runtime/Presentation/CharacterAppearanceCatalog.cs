using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Presentation
{
    public sealed class AppearanceBodyOption
    {
        public string Id, Label;
        public AppearanceBodyOption(string id, string label) { Id = id; Label = label; }
    }

    public sealed class AppearanceControl
    {
        public string Id, Label, Category;
        public string[] CompatibleBodies = Array.Empty<string>();
        public float Minimum, Maximum;
        public AppearanceControl(string id, string label, string category, float minimum = .25f, float maximum = .75f)
        { Id = id; Label = label; Category = category; Minimum = minimum; Maximum = maximum; }
        public bool Fits(string body) => CompatibleBodies.Length == 0 || CompatibleBodies.Contains(body);
    }

    public sealed class AppearanceItem
    {
        public string Id, Label, Slot, StyleGroup;
        public string[] Aliases = Array.Empty<string>();
        public string[] Tags = Array.Empty<string>();
        public int FallbackPriority = 100;
        public string[] CompatibleBodies = Array.Empty<string>();
        public string[] SuppressedSlots = Array.Empty<string>();
        public string[] Conflicts = Array.Empty<string>();
        public string[] ColorChannels = Array.Empty<string>();
        public Sprite Thumbnail;
        /// <summary>
        /// Worn only where a library look names it: never offered by a picker, by Randomize, by a
        /// reset or as a substitute (see <see cref="AppearanceEditing.Choices"/>). The item stays in
        /// the catalog, so a look or a save that names it finds it and wears it.
        /// </summary>
        public bool PickerHidden;
        public bool Fits(string body) => CompatibleBodies.Length == 0 || CompatibleBodies.Contains(body);
        public bool Matches(string id) => Id == id || Aliases.Contains(id);
    }

    /// <summary>Runtime capability contract; no UMA types escape into the creator or saves.</summary>
    public interface ICharacterAppearanceCatalog
    {
        IReadOnlyList<AppearanceBodyOption> Bodies { get; }
        IReadOnlyList<AppearanceControl> Controls { get; }
        IReadOnlyList<AppearanceItem> Items { get; }
        CharacterAppearance Materialize(CharacterAppearance appearance);
        CharacterAppearance ChangeBody(CharacterAppearance appearance, string bodyId);
    }

    public static class AppearanceEditing
    {
        public static AppearanceItem Find(ICharacterAppearanceCatalog catalog, string id) =>
            catalog?.Items.FirstOrDefault(item => item.Matches(id));

        /// <summary>
        /// The choices a row offers: the place's offered items that fit the body, plus any
        /// picker-hidden item named in <paramref name="keep"/> (the draft's own library look, and
        /// what is worn now). A place of two slots lists the code-built slot's items first, then its
        /// twin's; within a slot, catalog order. For one slot and no hidden item this is every item
        /// of the slot that fits, in catalog order, as the creator listed them before.
        /// </summary>
        public static List<AppearanceItem> Choices(ICharacterAppearanceCatalog catalog, IReadOnlyList<string> place, string bodyId,
            ICollection<string> keep = null) =>
            place.SelectMany(slot => catalog.Items.Where(item => item.Slot == slot && item.Fits(bodyId)
                && (!item.PickerHidden || (keep != null && keep.Any(item.Matches))))).ToList();

        /// <summary>
        /// The item ids of the library look <paramref name="appearance"/> starts from (its presetId,
        /// or its fallbackId), as the catalog would dress it: what a row keeps among its choices for
        /// this character even when picker-hidden. Empty without a catalog.
        /// </summary>
        public static List<string> LookItemIds(ICharacterAppearanceCatalog catalog, CharacterAppearance appearance)
        {
            if (catalog == null || appearance == null) return new List<string>();
            var look = catalog.Materialize(new CharacterAppearance { presetId = appearance.presetId, fallbackId = appearance.fallbackId });
            if (look?.outfits == null) return new List<string>();
            return look.outfits.SelectMany(outfit => outfit?.wardrobe ?? new List<AppearanceWardrobe>())
                .Where(worn => worn != null && !string.IsNullOrEmpty(worn.itemId)).Select(worn => worn.itemId).Distinct().ToList();
        }

        /// <summary>Compare every outfit, including clothes removed by a conflict or body change.</summary>
        public static string DescribeSubstitutions(CharacterAppearance before, CharacterAppearance after,
            ICharacterAppearanceCatalog catalog)
        {
            var changes = new List<string>();
            foreach (var outfit in before.outfits)
            {
                var result = after.outfits.FirstOrDefault(value => value.id == outfit.id);
                foreach (var worn in outfit.wardrobe)
                {
                    var replacement = result?.wardrobe.FirstOrDefault(value => value.slot == worn.slot);
                    var previousItem = Find(catalog, worn.itemId);
                    if (replacement?.itemId == worn.itemId || (replacement != null && previousItem?.Matches(replacement.itemId) == true)) continue;
                    string previous = previousItem?.Label ?? worn.itemId;
                    string next = replacement == null ? "removed" : Find(catalog, replacement.itemId)?.Label ?? replacement.itemId;
                    changes.Add(outfit.id + " — " + previous + " → " + next);
                }
            }
            return changes.Count == 0 ? "Changed body. All clothing fits."
                : "Changed clothing:\n" + string.Join("\n", changes) + "\nUndo restores every outfit.";
        }

        public static CharacterOutfit Outfit(CharacterAppearance appearance)
        {
            var outfit = appearance.outfits.FirstOrDefault(value => value.id == appearance.activeOutfit);
            if (outfit != null) return outfit;
            outfit = new CharacterOutfit { id = appearance.activeOutfit };
            appearance.outfits.Add(outfit);
            return outfit;
        }

        public static void SetValue(CharacterAppearance appearance, string id, float value)
        {
            var entry = appearance.dna.FirstOrDefault(item => item.id == id);
            if (entry == null) { entry = new AppearanceValue { id = id }; appearance.dna.Add(entry); }
            entry.value = value;
        }

        public static void SetColor(CharacterAppearance appearance, string id, Color value)
        {
            var entry = appearance.colors.FirstOrDefault(item => item.id == id);
            if (entry == null) { entry = new AppearanceColor { id = id }; appearance.colors.Add(entry); }
            entry.r = value.r; entry.g = value.g; entry.b = value.b; entry.a = value.a;
        }

        public static Color ColorValue(CharacterAppearance appearance, string id, Color fallback)
        {
            var color = appearance.colors.FirstOrDefault(item => item.id == id);
            return color == null ? fallback : new Color(color.r, color.g, color.b, color.a);
        }

        /// <summary>Conflicting choices are resolved in the draft, before any avatar is rebuilt.</summary>
        /// <summary>
        /// The slots that belong to the person rather than to the clothes: hair, brows, a beard, and
        /// the face's details - freckles, makeup, an older face.
        ///
        /// <para>They are stored per outfit like everything else, because an outfit is just a
        /// wardrobe list, but they are <em>written</em> to every outfit at once. Without that a
        /// houseguest changed hairstyle by changing clothes, and the Hair panel silently edited
        /// whichever set happened to be active without saying which one that was. The reset path
        /// already assumed this - it preserves these slots when it resets clothing - so this
        /// makes the write agree with the reset rather than introducing a new idea.</para>
        /// </summary>
        public static readonly string[] CharacterSlots = { "Hair", "Eyebrows", "Beard", "Face" };

        /// <summary>The Hair page's slots: the character slots but the face's details, which the Face page owns.</summary>
        public static readonly string[] HairSlots = { "Hair", "Eyebrows", "Beard" };

        /// <summary>
        /// The garments a fabric colour is offered for, whatever they are made of: the tint is laid
        /// over the garment's own texture, so it needs no colour channel of the garment's. Saved in
        /// the outfit's colours under <see cref="FabricChannel"/> - an entry in a list the save
        /// already carries, not a new field.
        /// </summary>
        public static readonly string[] FabricSlots = { "Chest", "Legs", "Feet" };
        public const string FabricPrefix = "Fabric-";
        public static string FabricChannel(string slot) => FabricPrefix + slot;
        public static bool IsFabricChannel(string id) => id != null && id.StartsWith(FabricPrefix, StringComparison.Ordinal);

        /// <summary>Tints the active outfit's <paramref name="slot"/> garment, or clears the tint.</summary>
        public static void SetFabric(CharacterAppearance appearance, string slot, Color? value)
        {
            var outfit = Outfit(appearance);
            string id = FabricChannel(slot);
            outfit.colors.RemoveAll(entry => entry.id == id);
            if (value.HasValue)
                outfit.colors.Add(new AppearanceColor { id = id, r = value.Value.r, g = value.Value.g, b = value.Value.b, a = 1f });
        }

        /// <summary>The tint on <paramref name="slot"/>'s garment in <paramref name="outfit"/>, if it has one.</summary>
        public static bool TryFabric(CharacterOutfit outfit, string slot, out Color value)
        {
            var entry = outfit?.colors?.FirstOrDefault(item => item != null && item.id == FabricChannel(slot));
            value = entry == null ? Color.white : new Color(entry.r, entry.g, entry.b, 1f);
            return entry != null;
        }

        public static bool IsCharacterSlot(string slot) => Array.IndexOf(CharacterSlots, slot) >= 0;

        public static void Wear(CharacterAppearance appearance, AppearanceItem item, ICharacterAppearanceCatalog catalog)
        {
            if (item == null || !item.Fits(appearance.bodyId)) return;
            if (IsCharacterSlot(item.Slot) && appearance.outfits != null && appearance.outfits.Count > 0)
            {
                foreach (var set in appearance.outfits) Put(set, item, catalog);
                return;
            }
            Put(Outfit(appearance), item, catalog);
        }

        private static void Put(CharacterOutfit outfit, AppearanceItem item, ICharacterAppearanceCatalog catalog)
        {
            // A code-built piece and a UMA item on the same place on the body (WardrobeSlots.Twins) are never
            // worn together: wearing either takes the other off.
            var twins = WardrobeSlots.Twins(item.Slot);
            outfit.wardrobe.RemoveAll(worn => worn.slot == item.Slot || item.SuppressedSlots.Contains(worn.slot)
                || Array.IndexOf(twins, worn.slot) >= 0
                || item.Conflicts.Any(conflict => Find(catalog, conflict)?.Matches(worn.itemId) == true || conflict == worn.itemId)
                || catalog.Items.Any(other => other.Matches(worn.itemId) &&
                    (other.SuppressedSlots.Contains(item.Slot) || other.Conflicts.Any(item.Matches))));
            outfit.wardrobe.Add(new AppearanceWardrobe { slot = item.Slot, itemId = item.Id });
        }
    }

}
