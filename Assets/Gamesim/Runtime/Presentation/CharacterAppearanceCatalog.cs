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
        /// The slots that belong to the person rather than to the clothes: hair, brows and a beard.
        ///
        /// <para>They are stored per outfit like everything else, because an outfit is just a
        /// wardrobe list, but they are <em>written</em> to every outfit at once. Without that a
        /// houseguest changed hairstyle by changing clothes, and the Hair panel silently edited
        /// whichever set happened to be active without saying which one that was. The reset path
        /// already assumed this - it preserves these three slots when it resets clothing - so this
        /// makes the write agree with the reset rather than introducing a new idea.</para>
        /// </summary>
        public static readonly string[] CharacterSlots = { "Hair", "Eyebrows", "Beard" };

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
            outfit.wardrobe.RemoveAll(worn => worn.slot == item.Slot || item.SuppressedSlots.Contains(worn.slot)
                || item.Conflicts.Any(conflict => Find(catalog, conflict)?.Matches(worn.itemId) == true || conflict == worn.itemId)
                || catalog.Items.Any(other => other.Matches(worn.itemId) &&
                    (other.SuppressedSlots.Contains(item.Slot) || other.Conflicts.Any(item.Matches))));
            outfit.wardrobe.Add(new AppearanceWardrobe { slot = item.Slot, itemId = item.Id });
        }
    }

}
