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
    /// Persona to UMA look: one entry per <see cref="Gamesim.Simulation.CastTemplates"/> template,
    /// plus the player's.
    ///
    /// <para>Keyed by the houseguest's own id — which is what
    /// <see cref="Gamesim.Presentation.CharacterPresentation.CharacterId"/> carries and, for the six
    /// personas the primitive rig also knows, what
    /// <see cref="Gamesim.Presentation.CharacterPresentation.AppearanceId"/> resolves to.
    /// <see cref="Resolve"/> prefers the id over the appearance, so casting a season from the
    /// template table gives twenty-four different people rather than five faces repeated; a
    /// houseguest with no entry of their own still falls back to the appearance the trait mapping
    /// picked, so an imported identity is still dressed.</para>
    ///
    /// <para>The eight of the mockups' cards — Alex, Emma, Jordan, Casey, Riley, Jamie, Taylor and
    /// Maya (ArtSource/reference/mockups/mockup-02, mockup-09) — are matched to their cards: hair
    /// shape and colour, skin, and an outfit in the silhouette the card shows. The rest are built
    /// from their archetype and their table row, which is all the direction there is for them.</para>
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
        // rather than against it. These are the primitive rig's tones raised into that range, with
        // four more added so twenty-four people are not five tones repeated.
        private static readonly Color Ebony = Hex("9A6A4C");
        private static readonly Color Deep = Hex("B8855E");
        private static readonly Color Umber = Hex("C9A079");
        private static readonly Color Sienna = Hex("D6AE86");
        private static readonly Color Olive = Hex("DEBE94");
        private static readonly Color Tan = Hex("E8C4A0");
        private static readonly Color Fair = Hex("FFDCBE");
        private static readonly Color Porcelain = Hex("FFECD2");

        // Hair, likewise: one row of the house's hair palette, named so a look reads as a sentence.
        private static readonly Color Ink = Hex("1A130F");
        private static readonly Color Jet = Hex("241B16");
        private static readonly Color Espresso = Hex("2A1D17");
        private static readonly Color Coffee = Hex("3B2A1E");
        private static readonly Color Chestnut = Hex("5D4226");
        private static readonly Color Auburn = Hex("6E3A24");
        private static readonly Color Honey = Hex("8A6239");
        private static readonly Color Wheat = Hex("B08A55");
        private static readonly Color Flax = Hex("D8B87A");
        private static readonly Color Ash = Hex("6E6A63");
        private static readonly Color Silver = Hex("B9B3A8");

        private static readonly Color BrownEye = Hex("4A3524");
        private static readonly Color DarkEye = Hex("3A2C22");
        private static readonly Color HazelEye = Hex("6B5433");
        private static readonly Color GreenEye = Hex("42603F");
        private static readonly Color BlueEye = Hex("3B4A5C");
        private static readonly Color SlateEye = Hex("4A5A66");
        private static readonly Color AmberEye = Hex("7A5A2E");

        private static readonly Dictionary<string, UmaCastLook> Looks = new Dictionary<string, UmaCastLook>
        {
            // ------------------------------------------------------------------ regular season

            // The Mastermind. The card's dark jacket over a dark tee: the only houseguest who
            // arrives dressed to be photographed.
            ["alex-chen"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "Hair_LeftPart_Recipe",
                    "Eyebrows_Average_Average",
                    "male_underwear_tighty_Recipe",
                    "male_jacket_hive_Recipe",
                    "male_sportpants_alt_black_Recipe",
                    "male_shoes_tall_Recipe",
                },
                Skin = Umber,
                Hair = Ink,
                Brows = Ink,
                Eyes = DarkEye,
                // Composed and square: build rather than height does the talking.
                Dna = new Dictionary<string, float> { ["height"] = 0.18f, ["upperMuscle"] = 0.64f, ["jawsSize"] = 0.58f },
            },

            // The Scientist. Long light hair and a plain top, exactly as the card has her.
            ["emma-brown"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "Hair_LongSwept_Recipe",
                    "Eyebrows_Thin_Average",
                    "underwear_bra_white_Recipe",
                    "underwear_white_granit_bottom_Recipe",
                    "colors_top_Recipe",
                    "tights_gray_Recipe",
                    "shoe_low_white_Recipe",
                },
                Skin = Porcelain,
                Hair = Flax,
                Brows = Wheat,
                Eyes = GreenEye,
                Dna = new Dictionary<string, float> { ["height"] = 0.16f, ["upperMuscle"] = 0.50f, ["cheekSize"] = 0.56f },
            },

            // The Charmer. Short curls and a cream tee; the warmest smile on the grid.
            ["jordan-taylor"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "Hair_Poofy",
                    "Eyebrows_Average_Average",
                    "male_underwear_whiteBlue_Recipe",
                    "male_tshirt_white_Recipe",
                    "male_shorts_black_cotton_Recipe",
                    "male_shoe_low_white.001_Recipe",
                },
                Skin = Deep,
                Hair = Auburn,
                Brows = Espresso,
                Eyes = BrownEye,
                Dna = new Dictionary<string, float> { ["height"] = 0.21f, ["upperMuscle"] = 0.64f, ["mouthSize"] = 0.60f },
            },

            // The Party Animal. She is she/her in every table this project keeps and a woman on her
            // card; the first UMA pass built her on the male race, which was simply wrong.
            ["casey-wilson"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "Hair_CurveUnder_Recipe",
                    "Eyebrows_Arched_Average",
                    "underwear_bra_purple_Recipe",
                    "underwear_purple_Recipe",
                    "tanktop_zebra_Recipe",
                    "shorts_turquoise_Recipe",
                    "shoe_low_white_Recipe",
                },
                Skin = Sienna,
                Hair = Espresso,
                Brows = Espresso,
                Eyes = BrownEye,
                // The loudest silhouette in the house, and the shortest of the women.
                Dna = new Dictionary<string, float> { ["height"] = 0.08f, ["headSize"] = 0.78f, ["waist"] = 0.44f },
            },

            // The Brainiac: side part, hoodie, sweatpants.
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
                Hair = Jet,
                Brows = Jet,
                Eyes = BlueEye,
                // Tallest and leanest, so the cast is not one body six times.
                Dna = new Dictionary<string, float> { ["height"] = 0.26f, ["upperMuscle"] = 0.46f, ["legsSize"] = 0.56f },
            },

            // The Caregiver. The card gives her long dark hair rather than the bob the first pass
            // guessed at; everything else about her was already right.
            ["jamie-roberts"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "Hair_Straight_Recipe",
                    "Eyebrows_Thin_Average",
                    "underwear_bra_white_Recipe",
                    "underwear_white_granit_bottom_Recipe",
                    "tshirt_turquoise_Recipe",
                    "tights_gray_Recipe",
                    "shoe_low_white_Recipe",
                },
                Skin = Tan,
                Hair = Coffee,
                Brows = Chestnut,
                Eyes = HazelEye,
                // Softer and shorter; the warmest silhouette in the house.
                Dna = new Dictionary<string, float> { ["height"] = 0.06f, ["headSize"] = 0.76f, ["upperWeight"] = 0.60f },
            },

            // The Influencer: hair up, a jacket, and the only heels on the grid.
            ["quinn-martinez"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "Hair_UpwardBun_Recipe",
                    "Eyebrows_Arched_Bushy",
                    "underwear_bra_yellow_Recipe",
                    "underwear_whiteStriped_Recipe",
                    "jacket_hive.001_Recipe",
                    "skirt_turquoise_Recipe",
                    "shoes_tall_turquoise.001_Recipe",
                },
                Skin = Olive,
                Hair = Jet,
                Brows = Jet,
                Eyes = DarkEye,
                Dna = new Dictionary<string, float> { ["height"] = 0.17f, ["eyeSize"] = 0.68f, ["lipsSize"] = 0.62f },
            },

            // The Protector: cropped hair, a trimmed beard, and the broadest build in the house.
            ["avery-thompson"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "bb_Male_Military_Hair_Recipe",
                    "Eyebrows_Bushy_Average",
                    "Beard_Trimmed",
                    "male_underpants_blue_Recipe",
                    "male_tanktop_yellow_Recipe",
                    "male_sportpants_blueWhite_Recipe",
                    "male_shoes_tall_Recipe",
                },
                Skin = Deep,
                Hair = Ink,
                Brows = Ink,
                Eyes = DarkEye,
                Dna = new Dictionary<string, float>
                {
                    ["height"] = 0.24f, ["armWidth"] = 0.74f, ["upperMuscle"] = 0.78f, ["lowerMuscle"] = 0.72f,
                },
            },

            // The Firebrand: ponytail, training kit, low trainers.
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
                Eyes = DarkEye,
                // The athlete reads through build rather than height.
                Dna = new Dictionary<string, float> { ["upperMuscle"] = 0.70f, ["lowerMuscle"] = 0.70f, ["waist"] = 0.42f },
            },

            // The Leader: a wave of light hair over a tank; the card's easiest face to pick out.
            ["sam-williams"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "Hair_MessyPomp_Recipe",
                    "Eyebrows_Average_Bushy",
                    "male_underwear_hive_Recipe",
                    "male_tanktop_yellow_Recipe",
                    "male_sportpants_grey_Recipe",
                    "male_shoe_low_white.001_Recipe",
                },
                Skin = Fair,
                Hair = Wheat,
                Brows = Honey,
                Eyes = BlueEye,
                Dna = new Dictionary<string, float> { ["height"] = 0.22f, ["upperMuscle"] = 0.66f, ["chinSize"] = 0.58f },
            },

            // The Shadow: dark hair pushed up, stubble, and nothing on him you would describe later.
            ["blake-peterson"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "HairMessyUp_Recipe",
                    "Eyebrows_Average_Average",
                    "Beard_Trimmed_Bushy",
                    "male_underpants_granit_Recipe",
                    "male_hoodie_blue_Recipe",
                    "male_tights_black_Recipe",
                    "male_shoes_tall_turquoise_Recipe",
                },
                Skin = Fair,
                Hair = Espresso,
                Brows = Espresso,
                Eyes = SlateEye,
                Dna = new Dictionary<string, float> { ["height"] = 0.19f, ["upperMuscle"] = 0.52f, ["noseSize"] = 0.56f },
            },

            // The Diplomat: pulled-back hair, layered knit, tall boots.
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
                Hair = Espresso,
                Brows = Espresso,
                Eyes = BrownEye,
                // Poised and upright: the tallest of the women, least exaggerated.
                Dna = new Dictionary<string, float> { ["height"] = 0.20f, ["upperMuscle"] = 0.52f },
            },

            // ------------------------------------------------------------------ all-stars

            // The Funeral Director: a coach's crop and the house's plainest grey, which is the point.
            ["dan-gheesling"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "bb_male_haircut_Recipe",
                    "Eyebrows_Average_Average",
                    "male_underwear_tighty_Recipe",
                    "male_hoodie_grey_Recipe",
                    "male_sportpants_grey_Recipe",
                    "male_shoe_low_white.001_Recipe",
                },
                Skin = Fair,
                Hair = Coffee,
                Brows = Coffee,
                Eyes = HazelEye,
                Dna = new Dictionary<string, float> { ["height"] = 0.20f, ["upperWeight"] = 0.58f, ["jawsSize"] = 0.60f },
            },

            // The Puppet Master: the oldest silhouette in the house, and the best dressed of the men.
            ["dr-will-kirby"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "Hair_Pointy_Recipe",
                    "Eyebrows_Arched_Average",
                    "male_underwear_fishes_Recipe",
                    "male_jacket_hive_Recipe",
                    "male_sportpants_grey_Recipe",
                    "male_shoes_tall_Recipe",
                },
                Skin = Tan,
                Hair = Ink,
                Brows = Ink,
                Eyes = DarkEye,
                Dna = new Dictionary<string, float> { ["height"] = 0.21f, ["cheekPronounced"] = 0.62f, ["noseCurve"] = 0.56f },
            },

            // The Undercover Boss: cropped, unremarkable, and never the first person you look at.
            ["derrick-levasseur"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "bb_Male_Military_Hair_Recipe",
                    "Eyebrows_Bushy_Average",
                    "male_underwear_whiteBlue_Recipe",
                    "male_hoodie_blueWhite_Recipe",
                    "male_shorts_white_Recipe",
                    "male_shoe_low_white.001_Recipe",
                },
                Skin = Ebony,
                Hair = Ink,
                Brows = Ink,
                Eyes = DarkEye,
                Dna = new Dictionary<string, float> { ["height"] = 0.18f, ["armWidth"] = 0.70f, ["upperMuscle"] = 0.68f },
            },

            // The Comp Queen: blonde, pulled back for a competition she intends to win.
            ["janelle-pierzina"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "Hair_StraigntPulledBack_Recipe",
                    "Eyebrows_Arched_Average",
                    "underwear_bra_white_Recipe",
                    "sports_underwear_bottoms_Recipe",
                    "sportswear_top_granit_Recipe",
                    "tights_stripe_Recipe",
                    "shoe_low_white_Recipe",
                },
                Skin = Fair,
                Hair = Flax,
                Brows = Wheat,
                Eyes = BlueEye,
                Dna = new Dictionary<string, float> { ["height"] = 0.23f, ["upperMuscle"] = 0.68f, ["lowerMuscle"] = 0.66f },
            },

            // The Hitman's Partner: a swimmer's shoulders and a haircut that took some deciding.
            ["cody-calafiore"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "HairAnimeMessy_Recipe",
                    "Eyebrows_Average_Bushy",
                    "male_underwear_hive_Recipe",
                    "male_tshirt_white_Recipe",
                    "male_shorts_hive_Recipe",
                    "male_shoe_low_white.001_Recipe",
                },
                Skin = Olive,
                Hair = Coffee,
                Brows = Coffee,
                Eyes = HazelEye,
                Dna = new Dictionary<string, float> { ["height"] = 0.24f, ["upperMuscle"] = 0.72f, ["waist"] = 0.40f },
            },

            // The Black Widow: a bob, dark and exact, and a face that gives nothing back.
            ["danielle-reyes"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "Hair_Bob_Recipe",
                    "Eyebrows_Thin_Average",
                    "underwear_Bra_white_granit_top_Recipe",
                    "underwear_white_granit_bottom_Recipe",
                    "Hoodie_turquoise_Recipe",
                    "tights_purple_Recipe",
                    "shoes_tall_white_Recipe",
                },
                Skin = Umber,
                Hair = Jet,
                Brows = Jet,
                Eyes = BrownEye,
                Dna = new Dictionary<string, float> { ["height"] = 0.13f, ["cheekSize"] = 0.58f, ["eyeSpacing"] = 0.56f },
            },

            // The Poker Player: hair down, nothing that moves, nothing that tells.
            ["vanessa-rousso"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "bb_female_hair_Recipe",
                    "Eyebrows_Average_Average",
                    "underwear_bra_green_Recipe",
                    "underwear_greenPalettes_Recipe",
                    "tanktop_whiteBlackStraps_Recipe",
                    "green_tights_Recipe",
                    "shoes_tall_white_Recipe",
                },
                Skin = Fair,
                Hair = Chestnut,
                Brows = Chestnut,
                Eyes = GreenEye,
                Dna = new Dictionary<string, float> { ["height"] = 0.15f, ["lipsSize"] = 0.44f, ["foreheadSize"] = 0.58f },
            },

            // The Surfer Strategist: sun-bleached, barely dressed, and the least threatening man here.
            ["tyler-crispen"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "HairTornado_Recipe",
                    "Eyebrows_Thin_Average",
                    "male_underpants_blue_Recipe",
                    "male_tanktop_yellow_Recipe",
                    "male_swimwear_granit_Recipe",
                    "male_shoe_low_white.001_Recipe",
                },
                Skin = Olive,
                Hair = Wheat,
                Brows = Honey,
                Eyes = SlateEye,
                Dna = new Dictionary<string, float> { ["height"] = 0.22f, ["upperMuscle"] = 0.64f, ["belly"] = 0.38f },
            },

            // The Assassin: hair back, a quiet top, and no edge on show until there is one.
            ["chelsie-baham"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "Hair_PulledBack_Recipe",
                    "Eyebrows_Average_Average",
                    "underwear_bra_white_Recipe",
                    "underwear_whiteLace_Recipe",
                    "tanktop_yellow_Recipe",
                    "colors_top_bottom_Recipe",
                    "shoe_low_white_Recipe",
                },
                Skin = Deep,
                Hair = Ink,
                Brows = Ink,
                Eyes = DarkEye,
                Dna = new Dictionary<string, float> { ["height"] = 0.14f, ["headSize"] = 0.74f, ["mouthSize"] = 0.54f },
            },

            // The Vegas Showgirl: the biggest hair in the house, in the reddest colour it comes in.
            ["rachel-reilly"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "Hair_Poofy",
                    "Eyebrows_Arched_Bushy",
                    "underwear_bra_purple_Recipe",
                    "underwear_purple_Recipe",
                    "dress_tennis_granit_Recipe",
                    "tights_purple_Recipe",
                    "shoes_tall_turquoise.001_Recipe",
                },
                Skin = Fair,
                Hair = Auburn,
                Brows = Chestnut,
                Eyes = AmberEye,
                Dna = new Dictionary<string, float> { ["height"] = 0.18f, ["eyeSize"] = 0.70f, ["lipsSize"] = 0.64f },
            },

            // The Cookout Captain: tall, level, and never the loudest person in a room he is running.
            ["xavier-prather"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "bb_male_haircut_Recipe",
                    "Eyebrows_Average_Average",
                    "Beard_Goatee",
                    "male_underwear_tighty_Recipe",
                    "male_hoodie_blue_Recipe",
                    "male_sportpants_alt_black_Recipe",
                    "male_shoes_tall_Recipe",
                },
                Skin = Ebony,
                Hair = Ink,
                Brows = Ink,
                Eyes = DarkEye,
                Dna = new Dictionary<string, float> { ["height"] = 0.25f, ["armWidth"] = 0.68f, ["upperMuscle"] = 0.62f },
            },

            // The Floater Queen: the oldest woman in the house, greying on purpose, dressed to cook.
            ["jun-song"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "Hair_Bob_Recipe",
                    "Eyebrows_Thin_Bushy",
                    "underwear_bra_green_Recipe",
                    "underwear_white_granit_bottom_Recipe",
                    "tanktop_zebra_Recipe",
                    "F_Wrapped Pants_Recipe",
                    "shoe_low_white_Recipe",
                },
                Skin = Olive,
                Hair = Silver,
                Brows = Ash,
                Eyes = DarkEye,
                Dna = new Dictionary<string, float> { ["height"] = 0.09f, ["upperWeight"] = 0.58f, ["cheekPosition"] = 0.44f },
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
                Hair = Ink,
                Brows = Ink,
                Eyes = Hex("30251C"),
            },
        };

        /// <summary>Every appearance ID with an authored look, for previews and content checks.</summary>
        public static IEnumerable<string> AppearanceIds => Looks.Keys;

        public static bool TryGet(string appearanceId, out UmaCastLook look) =>
            Looks.TryGetValue(appearanceId ?? string.Empty, out look);

        /// <summary>
        /// The look a houseguest should be built with.
        ///
        /// <para>Their own id wins. <see cref="Gamesim.Presentation.CharacterPresentation.AppearanceId"/>
        /// answers a narrower question — which of the handful of primitive-rig recipes this person
        /// looks most like — and its trait fallback would collapse the template roster onto five
        /// faces. A houseguest with no entry of their own still gets that answer, which is what
        /// keeps an imported identity dressed.</para>
        /// </summary>
        public static bool Resolve(string characterId, string appearanceId, out UmaCastLook look)
            => TryGet(characterId, out look) || TryGet(appearanceId, out look);

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }
    }
}
