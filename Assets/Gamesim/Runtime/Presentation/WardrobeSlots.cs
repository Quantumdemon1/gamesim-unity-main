using System;
using System.Linq;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The wardrobe's slot lists, in one place: which UMA slots the catalog turns into creator items,
    /// which slots belong to the Accessories page, and which code-built piece and UMA item share a
    /// place on the body (so that only one of the two is ever worn and shown).
    ///
    /// <para>Plain data with no engine type, so the creator, the outfits, the catalog and the body
    /// build all read the same lists, with and without UMA installed. Slot names are UMA's wardrobe
    /// slot names and the code-built pieces' own, compared ordinally.</para>
    /// </summary>
    public static class WardrobeSlots
    {
        /// <summary>The UMA slots the game wore before UMA accessories: hair, face and clothes.</summary>
        public static readonly string[] Garments = { "Hair", "Beard", "Eyebrows", "Face", "Chest", "Legs", "Feet", "BottomUnderlayer", "TopUnderlayer" };

        /// <summary>
        /// UMA's accessory slots the game wears: earrings (Ears), a cap or headband (Helmet), a
        /// necklace (Shoulders), a bracelet or wristband (Arms) and a watch (Hands). Not Waist. The
        /// one list to change to open or close a UMA accessory slot.
        /// </summary>
        public static readonly string[] UmaAccessories = { "Ears", "Helmet", "Shoulders", "Arms", "Hands" };

        /// <summary>The code-built pieces' own slots (see ProceduralAccessories, whose Slots is this array).</summary>
        public static readonly string[] CodeBuiltAccessories = { "Eyewear", "Headwear", "Earrings", "Neckwear" };

        /// <summary>Every UMA slot the catalog turns into a creator item.</summary>
        public static readonly string[] Uma = Garments.Concat(UmaAccessories).ToArray();

        /// <summary>Every slot the Accessories page owns: what its reset, Randomise and the Clothing reset leave to it.</summary>
        public static readonly string[] Accessories = CodeBuiltAccessories.Concat(UmaAccessories).ToArray();

        /// <summary>
        /// A code-built slot and the UMA slot on the same place on the body: one is worn, never both.
        /// The one table every reader goes through, by <see cref="Twins"/> and <see cref="Place"/>.
        /// </summary>
        private static readonly string[][] SamePlace =
        {
            new[] { "Earrings", "Ears" },
            new[] { "Headwear", "Helmet" },
            new[] { "Neckwear", "Shoulders" },
        };

        /// <summary>The Accessories page's rows: the code-built places, then each UMA accessory slot that has no twin, in list order.</summary>
        public static readonly string[] AccessoryRows = CodeBuiltAccessories
            .Concat(UmaAccessories.Where(slot => !SamePlace.Any(pair => pair[1] == slot))).ToArray();

        /// <summary>The other slot of <paramref name="slot"/>'s place, or none (also for null or an unknown slot).</summary>
        public static string[] Twins(string slot)
        {
            if (slot == null) return Array.Empty<string>();
            foreach (var pair in SamePlace)
            {
                if (string.Equals(pair[0], slot, StringComparison.Ordinal)) return new[] { pair[1] };
                if (string.Equals(pair[1], slot, StringComparison.Ordinal)) return new[] { pair[0] };
            }
            return Array.Empty<string>();
        }

        /// <summary>The slots one creator row shows: the code-built slot first, then its UMA twin; any other slot alone.</summary>
        public static string[] Place(string slot)
        {
            if (slot == null) return Array.Empty<string>();
            foreach (var pair in SamePlace)
                if (string.Equals(pair[0], slot, StringComparison.Ordinal) || string.Equals(pair[1], slot, StringComparison.Ordinal))
                    return new[] { pair[0], pair[1] };
            return new[] { slot };
        }

        /// <summary>Whether <paramref name="slot"/> is one the Accessories page owns.</summary>
        public static bool IsAccessory(string slot) => slot != null && Array.IndexOf(Accessories, slot) >= 0;
    }
}
