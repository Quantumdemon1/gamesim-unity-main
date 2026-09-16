using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Uma
{
    /// <summary>
    /// One houseguest's UMA recipe: which race to build, which wardrobe pieces to layer on it, and
    /// the shared colours that make the result read as that person.
    ///
    /// Every recipe named here ships with UMA 3. A name that does not resolve is skipped rather than
    /// failing the build, so a partial content install still produces a dressed character.
    /// </summary>
    public sealed class UmaCastLook
    {
        public string Race = UmaCastLibrary.FemaleRace;

        /// <summary>Wardrobe recipe asset names, applied in order. UMA derives each slot from the recipe.</summary>
        public string[] Wardrobe = System.Array.Empty<string>();

        public Color Skin = new Color(0.91f, 0.77f, 0.63f);
        public Color Hair = new Color(0.13f, 0.10f, 0.09f);
        public Color Brows = new Color(0.13f, 0.10f, 0.09f);
        public Color Eyes = new Color(0.31f, 0.40f, 0.36f);

        /// <summary>
        /// Per-persona DNA, merged over <see cref="UmaCastLibrary.HouseProportions"/>. Values run
        /// 0 to 1 with 0.5 as the race default.
        /// </summary>
        public Dictionary<string, float> Dna = new Dictionary<string, float>();
    }

    /// <summary>
    /// Persona to UMA look. Keyed by the appearance IDs
    /// <see cref="Gamesim.Presentation.CharacterPresentation.AppearanceId"/> resolves to, so imported
    /// houseguests inherit a native look through the same trait mapping the primitive rig uses.
    /// </summary>
    public static class UmaCastLibrary
    {
        public const string FemaleRace = "Human Female 3.0";
        public const string MaleRace = "Human Male 3.0";

        /// <summary>
        /// The proportions that make a UMA houseguest belong in a Kenney-furnished house.
        ///
        /// Flattening the materials was never going to be enough. UMA ships realistic proportions,
        /// and against chunky low-poly furniture a realistic figure reads as a small thin smudge at
        /// the game's camera height — which is exactly how the first UMA cast looked in the house.
        /// Stylised characters are built the other way: a noticeably larger head, a shorter body, and
        /// thicker limbs and extremities. That is a proportion problem, so it is solved with DNA.
        ///
        /// Values run 0 to 1 with 0.5 as the race default, so every entry here is a deliberate
        /// departure from anatomical correctness in the direction of the set.
        /// </summary>
        public static readonly Dictionary<string, float> HouseProportions = new Dictionary<string, float>
        {
            ["headSize"] = 0.72f,        // the single strongest stylisation cue
            ["headWidth"] = 0.58f,
            ["eyeSize"] = 0.62f,
            ["neckThickness"] = 0.60f,
            ["height"] = 0.12f,          // shorter, so the head reads larger still
            ["upperMuscle"] = 0.58f,
            ["lowerMuscle"] = 0.58f,
            ["armWidth"] = 0.62f,
            ["forearmWidth"] = 0.62f,
            ["legsSize"] = 0.62f,
            ["handsSize"] = 0.68f,       // chunky extremities, like the furniture
            ["feetSize"] = 0.66f,
        };

        // Kenney's palette is bright and low-saturation; skin tones are lifted to sit beside it
        // rather than against it. These are the primitive rig's tones raised into that range.
        private static readonly Color Umber = Hex("C9A079");
        private static readonly Color Tan = Hex("E8C4A0");
        private static readonly Color Fair = Hex("FFDCBE");
        private static readonly Color Porcelain = Hex("FFECD2");
        private static readonly Color Deep = Hex("B8855E");

        private static readonly Dictionary<string, UmaCastLook> Looks = new Dictionary<string, UmaCastLook>
        {
            // The diplomat: pulled-back hair, layered knit, tall boots.
            ["maya-hassan"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "Hair_Bun_Recipe",
                    "Eyebrows_Arched_Average",
                    "underwear_bra_white_Recipe",
                    "underwear_white_granit_bottom_Recipe",
                    "sportswear_sweater_granit_Recipe",
                    "tights_gray_Recipe",
                    "shoes_tall_white_Recipe",
                },
                Skin = Umber,
                Hair = Hex("2A1D17"),
                Brows = Hex("2A1D17"),
                Eyes = Hex("4A3524"),
                // Poised and upright: the tallest of the women, least exaggerated.
                Dna = new Dictionary<string, float> { ["height"] = 0.20f, ["upperMuscle"] = 0.52f },
            },

            // The athlete: ponytail, training kit, low trainers.
            ["taylor-kim"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "HairPonytail_Recipe",
                    "Eyebrows_Average_Average",
                    "underwear_bra_white_Recipe",
                    "underwear_white_granit_bottom_Recipe",
                    "sportswear_top_Recipe",
                    "sportwear_pants_granit_Recipe",
                    "shoe_low_white_Recipe",
                },
                Skin = Tan,
                Hair = Hex("1B1512"),
                Brows = Hex("1B1512"),
                Eyes = Hex("3A2C22"),
                // The athlete reads through build rather than height.
                Dna = new Dictionary<string, float> { ["upperMuscle"] = 0.70f, ["lowerMuscle"] = 0.70f, ["waist"] = 0.42f },
            },

            // The caregiver: soft bob, simple tee, flats.
            ["jamie-roberts"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "Hair_Bob_Recipe",
                    "Eyebrows_Thin_Average",
                    "underwear_bra_white_Recipe",
                    "underwear_white_granit_bottom_Recipe",
                    "tshirt_turquoise_Recipe",
                    "tights_gray_Recipe",
                    "shoe_low_white_Recipe",
                },
                Skin = Tan,
                Hair = Hex("5D4226"),
                Brows = Hex("4A3420"),
                Eyes = Hex("42603F"),
                // Softer and shorter; the warmest silhouette in the house.
                Dna = new Dictionary<string, float> { ["height"] = 0.06f, ["headSize"] = 0.76f, ["upperWeight"] = 0.60f },
            },

            // The wildcard: unruly hair, tee and shorts.
            ["casey-wilson"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "Hair_Poofy",
                    "Eyebrows_Bushy_Average",
                    "male_underwear_tighty_Recipe",
                    "male_tshirt_white_Recipe",
                    "male_shorts_hive_Recipe",
                    "male_shoe_low_white.001_Recipe",
                },
                Skin = Fair,
                Hair = Hex("8A6239"),
                Brows = Hex("74512E"),
                Eyes = Hex("3E5C6B"),
                // Wiry and slight, with the biggest head of the six.
                Dna = new Dictionary<string, float> { ["height"] = 0.04f, ["headSize"] = 0.80f, ["armWidth"] = 0.54f },
            },

            // The analyst: side part, hoodie, sweatpants.
            ["riley-johnson"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "Hair_MessyRightPart_Recipe",
                    "Eyebrows_Average_Average",
                    "male_underwear_tighty_Recipe",
                    "male_hoodie_grey_Recipe",
                    "male_sweatpants_black_Recipe",
                    "male_shoes_tall_Recipe",
                },
                Skin = Porcelain,
                Hair = Hex("241B16"),
                Brows = Hex("241B16"),
                Eyes = Hex("3B4A5C"),
                // Tallest and leanest, so the cast is not one body six times.
                Dna = new Dictionary<string, float> { ["height"] = 0.26f, ["upperMuscle"] = 0.46f, ["legsSize"] = 0.56f },
            },

            // The player. Deliberately the plainest look in the house: it is the one the
            // customisation screen will overwrite first.
            [Gamesim.Simulation.ContentCatalog.PlayerId] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "Hair_LeftPart_Recipe",
                    "Eyebrows_Average_Average",
                    "male_underwear_tighty_Recipe",
                    "male_tshirt_white_Recipe",
                    "male_sportpants_grey_Recipe",
                    "male_shoe_low_white.001_Recipe",
                },
                Skin = Deep,
                Hair = Hex("1A130F"),
                Brows = Hex("1A130F"),
                Eyes = Hex("30251C"),
            },
        };

        /// <summary>Every appearance ID with an authored look, for previews and content checks.</summary>
        public static IEnumerable<string> AppearanceIds => Looks.Keys;

        public static bool TryGet(string appearanceId, out UmaCastLook look) =>
            Looks.TryGetValue(appearanceId ?? string.Empty, out look);

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }
    }
}

