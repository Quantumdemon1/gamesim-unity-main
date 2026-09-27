using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The colours a houseguest is made of: skin, hair, brows and eyes, named and grouped, for the
    /// creator's swatches and the cast's looks alike.
    ///
    /// <para>The skin tones are anchored on the Monk Skin Tone scale - ten tones chosen to cover the
    /// range of real skin evenly, deep tones as carefully as light ones - in four rows by depth.
    /// Along each row the undertone changes as well as the depth, because two people of the same
    /// depth can differ most in their undertone: golden, neutral, olive, rosy or red tones side by
    /// side. The deep rows get that choice too, not only a darker brown. There red runs no more than
    /// about 1.45 times the green, past which a lit deep brown turns maroon, so the undertone is
    /// carried by hue and saturation instead: a golden deep leans yellow, a red one takes its blue
    /// up towards its green, and a neutral one sits lower on red. Even the neutrals keep some warmth:
    /// a truly grey brown reads as grey on a lit face.</para>
    ///
    /// <para>Hair is natural shades first, black through white, with the blacks split finely because
    /// most of the world's hair is one of them; dyed colours are a row of their own. Eyes run dark to
    /// light. A random houseguest takes their skin first and then hair and eyes weighted by its depth
    /// (<see cref="RandomHair"/>, <see cref="RandomEyes"/>): the browns and blacks most people have,
    /// light eyes and fair or red hair rare on deep skin, grey and white hair rare at every depth.</para>
    /// </summary>
    public static class CharacterPalettes
    {
        public readonly struct Swatch
        {
            public readonly string Hex, Name;
            public Swatch(string hex, string name) { Hex = hex; Name = name; }

            public Color Colour
            {
                get { ColorUtility.TryParseHtmlString("#" + Hex, out var colour); colour.a = 1f; return colour; }
            }
        }

        public readonly struct Group
        {
            public readonly string Label;
            public readonly Swatch[] Swatches;
            public Group(string label, params Swatch[] swatches) { Label = label; Swatches = swatches; }
        }

        // The cast's looks name these tones, so a swatch keeps its name and its place in its row.
        public static readonly Group[] Skin =
        {
            // Neutral, neutral, golden, rosy, golden, neutral.
            new Group("Fair & light",
                new Swatch("F6EDE4", "Porcelain"), new Swatch("F3E3D3", "Ivory"), new Swatch("F5E1C4", "Warm ivory"),
                new Swatch("F0D2C0", "Rose"), new Swatch("EDD5B3", "Sand"), new Swatch("E8CDAA", "Light beige")),
            // Golden, olive-neutral, golden, olive, rosy, golden.
            new Group("Medium",
                new Swatch("E3C096", "Golden"), new Swatch("D7BD96", "Beige"), new Swatch("D8AA80", "Honey"),
                new Swatch("BFA67F", "Olive"), new Swatch("C69576", "Tan"), new Swatch("B98A60", "Caramel")),
            // Golden, olive, red, warm, golden, neutral.
            new Group("Tan & brown",
                new Swatch("A87E54", "Amber"), new Swatch("9C8260", "Bronze"), new Swatch("956755", "Chestnut"),
                new Swatch("865D43", "Sienna"), new Swatch("7C5A38", "Cinnamon"), new Swatch("6E5647", "Mocha")),
            // Two depths of golden, red and neutral. Red runs no more than about 1.45 times the
            // green: past that, a lit deep brown turns maroon.
            new Group("Deep",
                new Swatch("6B4E30", "Umber"), new Swatch("5E4136", "Mahogany"), new Swatch("4A3A33", "Espresso"),
                new Swatch("4A3522", "Cocoa"), new Swatch("3B2922", "Ebony"), new Swatch("2A211D", "Deep ebony")),
        };

        public static readonly Group[] Hair =
        {
            new Group("Natural",
                new Swatch("0C0B0E", "Jet black"), new Swatch("17120F", "Black"), new Swatch("241A15", "Soft black"),
                new Swatch("33241A", "Dark brown"), new Swatch("4A3322", "Brown"), new Swatch("5E412A", "Medium brown"),
                new Swatch("7A5638", "Light brown"), new Swatch("6A3A22", "Chestnut"), new Swatch("7B2E1A", "Auburn"),
                new Swatch("A14C24", "Copper"), new Swatch("C07B4E", "Strawberry"), new Swatch("8E6C43", "Dark blonde"),
                new Swatch("B38B55", "Honey blonde"), new Swatch("CFA768", "Golden blonde"), new Swatch("E6D8B4", "Platinum"),
                new Swatch("A9A7A2", "Silver"), new Swatch("E9E7E2", "White"), new Swatch("5C5955", "Salt and pepper")),
            new Group("Dyed",
                new Swatch("5E1224", "Burgundy"), new Swatch("B0201E", "Red"), new Swatch("D46A8E", "Rose pink"),
                new Swatch("E8A6C0", "Pastel pink"), new Swatch("5B3A8C", "Violet"), new Swatch("2E4F9C", "Blue"),
                new Swatch("1F7A7A", "Teal"), new Swatch("3F7A3A", "Green")),
        };

        public static readonly Group[] Eyes =
        {
            new Group("Eyes",
                new Swatch("1B120C", "Black-brown"), new Swatch("2E1D12", "Dark brown"), new Swatch("4A2F1C", "Brown"),
                new Swatch("6B4527", "Light brown"), new Swatch("94652B", "Amber"), new Swatch("7A6A3A", "Hazel"),
                new Swatch("4B6E3E", "Green"), new Swatch("6C7C66", "Grey-green"), new Swatch("3E6D9A", "Blue"),
                new Swatch("6E97BF", "Light blue"), new Swatch("70849A", "Grey-blue"), new Swatch("868C92", "Grey")),
        };

        /// <summary>Every swatch in a set of groups, in order: the order the creator numbers them in.</summary>
        public static IEnumerable<Swatch> All(Group[] groups)
        {
            foreach (var group in groups)
                foreach (var swatch in group.Swatches) yield return swatch;
        }

        /// <summary>A named colour from a palette; throws for a name the palette does not have, so a look cannot drift off it quietly.</summary>
        public static Color Named(Group[] groups, string name)
        {
            foreach (var swatch in All(groups))
                if (swatch.Name == name) return swatch.Colour;
            throw new ArgumentException("No swatch named '" + name + "'.", nameof(name));
        }

        /// <summary>The name of the swatch a colour is, when it is one (within an 8-bit step); null otherwise.</summary>
        public static string NameOf(Group[] groups, Color colour)
        {
            foreach (var swatch in All(groups))
            {
                var c = swatch.Colour;
                if (Mathf.Abs(c.r - colour.r) < .02f && Mathf.Abs(c.g - colour.g) < .02f && Mathf.Abs(c.b - colour.b) < .02f)
                    return swatch.Name;
            }
            return null;
        }

        // ---------------------------------------------------------------- a random houseguest

        /// <summary>
        /// How often a random houseguest has each natural hair shade, by the depth of their skin - the
        /// rows of <see cref="Skin"/>: fair, medium, tan and brown, deep. Relative weights, each
        /// depth's summing to about a hundred. Past fair, blacks and dark browns carry most of it; on
        /// tan and deep skin blonde and red hair come up about one roll in forty, and grey or white
        /// hair one in fifty or fewer at every depth. A shade missing here is never drawn.
        /// </summary>
        private static readonly Dictionary<string, float[]> HairWeights = new Dictionary<string, float[]>(StringComparer.Ordinal)
        {
            ["Jet black"] = new[] { 6f, 16f, 22f, 30f },
            ["Black"] = new[] { 7f, 18f, 26f, 30f },
            ["Soft black"] = new[] { 7f, 14f, 18f, 18f },
            ["Dark brown"] = new[] { 12f, 18f, 16f, 12f },
            ["Brown"] = new[] { 12f, 12f, 8f, 4f },
            ["Medium brown"] = new[] { 10f, 7f, 3f, 1.5f },
            ["Light brown"] = new[] { 8f, 4f, 1f, .5f },
            ["Chestnut"] = new[] { 6f, 3f, 1f, .5f },
            ["Auburn"] = new[] { 4f, 1.5f, .5f, .5f },
            ["Copper"] = new[] { 3f, 1f, .3f, .3f },
            ["Strawberry"] = new[] { 3f, .5f, .2f, .2f },
            ["Dark blonde"] = new[] { 7f, 2f, .5f, .5f },
            ["Honey blonde"] = new[] { 6f, 1.5f, .5f, .5f },
            ["Golden blonde"] = new[] { 5f, 1f, .3f, .3f },
            ["Platinum"] = new[] { 2f, .5f, .2f, .2f },
            ["Silver"] = new[] { .8f, .6f, .6f, .6f },
            ["White"] = new[] { .4f, .3f, .3f, .3f },
            ["Salt and pepper"] = new[] { .8f, .7f, .6f, .6f },
        };

        /// <summary>
        /// How often a random houseguest has each eye colour, by the depth of their skin, as
        /// <see cref="HairWeights"/>. Browns first at every depth; green, blue and grey eyes are two
        /// or three rolls in a hundred on tan and deep skin, and about two in five on fair.
        /// </summary>
        private static readonly Dictionary<string, float[]> EyeWeights = new Dictionary<string, float[]>(StringComparer.Ordinal)
        {
            ["Black-brown"] = new[] { 8f, 18f, 30f, 38f },
            ["Dark brown"] = new[] { 16f, 28f, 34f, 36f },
            ["Brown"] = new[] { 16f, 22f, 18f, 14f },
            ["Light brown"] = new[] { 8f, 10f, 8f, 6f },
            ["Amber"] = new[] { 3f, 5f, 3f, 2f },
            ["Hazel"] = new[] { 9f, 7f, 3f, 1.5f },
            ["Green"] = new[] { 8f, 3f, .8f, .5f },
            ["Grey-green"] = new[] { 6f, 2f, .6f, .4f },
            ["Blue"] = new[] { 11f, 2f, .5f, .4f },
            ["Light blue"] = new[] { 6f, .7f, .3f, .2f },
            ["Grey-blue"] = new[] { 5f, 1.3f, .5f, .3f },
            ["Grey"] = new[] { 4f, 1f, .3f, .2f },
        };

        /// <summary>
        /// The depth a skin colour is - the index of its row in <see cref="Skin"/>, 0 fair to 3 deep -
        /// by the nearest swatch, so a colour off the palette has one too.
        /// </summary>
        public static int DepthOf(Color skin)
        {
            int depth = 0;
            float nearest = float.MaxValue;
            for (int row = 0; row < Skin.Length; row++)
                foreach (var swatch in Skin[row].Swatches)
                {
                    var c = swatch.Colour;
                    float distance = (c.r - skin.r) * (c.r - skin.r) + (c.g - skin.g) * (c.g - skin.g) + (c.b - skin.b) * (c.b - skin.b);
                    if (distance < nearest) { nearest = distance; depth = row; }
                }
            return depth;
        }

        /// <summary>A skin tone for a random houseguest: every tone as likely as another, the deep as the fair.</summary>
        public static Swatch RandomSkin(System.Random random)
        {
            var tones = new List<Swatch>(All(Skin));
            return tones[random.Next(tones.Count)];
        }

        /// <summary>
        /// A natural hair shade for a random houseguest with this skin, weighted by its depth. Never a
        /// dyed one: a random houseguest is somebody, not a dye chart.
        /// </summary>
        public static Swatch RandomHair(Color skin, System.Random random) =>
            Weighted(Hair[0].Swatches, HairWeights, DepthOf(skin), random);

        /// <summary>An eye colour for a random houseguest with this skin, weighted by its depth.</summary>
        public static Swatch RandomEyes(Color skin, System.Random random) =>
            Weighted(Eyes[0].Swatches, EyeWeights, DepthOf(skin), random);

        private static Swatch Weighted(Swatch[] swatches, Dictionary<string, float[]> weights, int depth, System.Random random)
        {
            float Weight(Swatch swatch) =>
                weights.TryGetValue(swatch.Name, out var row) ? row[Mathf.Clamp(depth, 0, row.Length - 1)] : 0f;
            float total = 0f;
            foreach (var swatch in swatches) total += Weight(swatch);
            double roll = random.NextDouble() * total;
            Swatch last = swatches[swatches.Length - 1];
            foreach (var swatch in swatches)
            {
                float weight = Weight(swatch);
                if (weight <= 0f) continue;
                last = swatch;
                roll -= weight;
                if (roll < 0d) return swatch;
            }
            return last;
        }
    }
}
