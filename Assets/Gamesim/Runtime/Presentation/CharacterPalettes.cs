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
    /// range of real skin evenly, deep tones as carefully as light ones - with golden, olive and
    /// rosy variants between them, because two people of the same depth can differ most in their
    /// undertone. The deepest are kept warm: a near-neutral brown reads as grey on a lit face.</para>
    ///
    /// <para>Hair is natural shades first, black through white, with the blacks split finely because
    /// most of the world's hair is one of them; dyed colours are a row of their own. Eyes run dark to
    /// light, weighted to the browns most people have.</para>
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

        public static readonly Group[] Skin =
        {
            new Group("Fair & light",
                new Swatch("F6EDE4", "Porcelain"), new Swatch("F3E3D3", "Ivory"), new Swatch("F5E1C4", "Warm ivory"),
                new Swatch("F0D2C0", "Rose"), new Swatch("EDD5B3", "Sand"), new Swatch("E8CDAA", "Light beige")),
            new Group("Medium",
                new Swatch("E3C096", "Golden"), new Swatch("D7BD96", "Beige"), new Swatch("D8AA80", "Honey"),
                new Swatch("C9A27C", "Olive"), new Swatch("C49870", "Tan"), new Swatch("B98A60", "Caramel")),
            new Group("Tan & brown",
                new Swatch("A67B5B", "Amber"), new Swatch("A07A52", "Bronze"), new Swatch("94684A", "Chestnut"),
                new Swatch("8A6042", "Sienna"), new Swatch("7E573D", "Cinnamon"), new Swatch("74503A", "Mocha")),
            // Red running no more than about 1.45 times the green: past that, a lit deep brown turns maroon.
            new Group("Deep",
                new Swatch("6A4A34", "Umber"), new Swatch("5C3F2D", "Mahogany"), new Swatch("4F3627", "Espresso"),
                new Swatch("443026", "Cocoa"), new Swatch("38281F", "Ebony"), new Swatch("2B1F18", "Deep ebony")),
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
    }
}
