using System.Collections.Generic;
using Gamesim.Presentation;
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

        /// <summary>
        /// The colour each garment is dyed, by wardrobe slot ("Chest", "Legs", "Feet"): the house's
        /// clothes are few, and the same tee in the colour a photo shows is most of a likeness.
        /// </summary>
        public Dictionary<string, Color> Fabric = new Dictionary<string, Color>();
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
    /// <para>Every look is matched to that houseguest's glamour photo - the web game's portrait, the
    /// one the cast screen shows: skin from the shared palette's depth and undertone, hair in its
    /// shape and shade (grown hair where UMA has none: an afro, coils, a fade, a buzz), brows, eyes,
    /// beards, the glasses, earrings, cap and necklace the photo shows, and the clothes it shows in
    /// its colours. Colours are named from <see cref="CharacterPalettes"/>, so a look cannot drift
    /// off the palette the creator offers.</para>
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

        /// <summary>A skin tone, hair colour or eye colour from the shared palettes, by its name there.</summary>
        private static Color Skin(string name) => CharacterPalettes.Named(CharacterPalettes.Skin, name);
        private static Color Hair(string name) => CharacterPalettes.Named(CharacterPalettes.Hair, name);
        private static Color Eyes(string name) => CharacterPalettes.Named(CharacterPalettes.Eyes, name);

        private static readonly Dictionary<string, UmaCastLook> Looks = new Dictionary<string, UmaCastLook>
        {
            // ------------------------------------------------------------------ regular season

            // The Mastermind. East Asian, light skin, black hair swept to the side, a black tee.
            ["alex-chen"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "Hair_LeftPart_Recipe",
                    "Eyebrows_Average_Average",
                    "male_underwear_tighty_Recipe",
                    "male_tshirt_white_Recipe",
                    "male_sportpants_alt_black_Recipe",
                    "male_shoes_tall_Recipe",
                },
                Skin = Skin("Golden"),
                Hair = Hair("Jet black"),
                Brows = Hair("Jet black"),
                Eyes = Eyes("Dark brown"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("1E2126") },
                Dna = new Dictionary<string, float> { ["height"] = .18f, ["upperMuscle"] = .64f, ["jawsSize"] = .56f, ["eyeRotation"] = .56f, ["noseFlatten"] = .56f, ["cheekPronounced"] = .55f },
            },

            // The Scientist. Her card: warm brown skin, long straight silver hair, the chrome visor and the red
            // bow tie, and a white lab coat over it all.
            ["emma-brown"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "Hair_Straight_Recipe",
                    "Eyebrows_Thin_Average",
                    "underwear_bra_white_Recipe",
                    "underwear_white_granit_bottom_Recipe",
                    "jacket_hive.001_Recipe",
                    "tights_gray_Recipe",
                    "shoe_low_white_Recipe",
                    "gs-acc-visor",
                    "gs-acc-bowtie",
                },
                Skin = Skin("Tan"),
                Hair = Hair("Silver"),
                Brows = Hair("Dark brown"),
                Eyes = Eyes("Dark brown"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("F2F2EE") },
                Dna = new Dictionary<string, float> { ["height"] = .16f, ["upperMuscle"] = .5f, ["cheekSize"] = .56f, ["lipsSize"] = .58f },
            },

            // The Charmer. A brown quiff, a short full beard, a grey tee.
            ["jordan-taylor"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "Hair_MessyPomp_Recipe",
                    "Eyebrows_Average_Average",
                    "Beard_Trimmed",
                    "male_underwear_whiteBlue_Recipe",
                    "male_tshirt_white_Recipe",
                    "male_sportpants_alt_black_Recipe",
                    "male_shoe_low_white.001_Recipe",
                },
                Skin = Skin("Light beige"),
                Hair = Hair("Medium brown"),
                Brows = Hair("Dark brown"),
                Eyes = Eyes("Brown"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("5A5F69") },
                Dna = new Dictionary<string, float> { ["height"] = .21f, ["upperMuscle"] = .64f, ["mouthSize"] = .6f, ["jawsSize"] = .58f },
            },

            // The Party Animal. Deep brown skin, a full natural afro, stud earrings, a blue-striped shirt.
            // Her afro is Tyler's full curls in black for now: the grown afro showed scalp through it.
            ["casey-wilson"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "Hair_Poofy",
                    "Eyebrows_Arched_Average",
                    "underwear_bra_white_Recipe",
                    "underwear_white_granit_bottom_Recipe",
                    "tshirt_turquoise_Recipe",
                    "tights_gray_Recipe",
                    "shoe_low_white_Recipe",
                    "gs-acc-studs",
                },
                Skin = Skin("Mahogany"),
                Hair = Hair("Black"),
                Brows = Hair("Black"),
                Eyes = Eyes("Dark brown"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("A9C8EA") },
                Dna = new Dictionary<string, float> { ["height"] = .08f, ["headSize"] = .78f, ["waist"] = .44f, ["lipsSize"] = .62f, ["noseWidth"] = .58f },
            },

            // The Brainiac. South Asian, medium-brown skin, neat black hair, black frames, a thin
            // moustache and a charcoal tee.
            ["riley-johnson"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "Hair_MessyRightPart_Recipe",
                    "Eyebrows_Average_Bushy",
                    "Mustache_Small",
                    "male_underwear_tighty_Recipe",
                    "male_tshirt_white_Recipe",
                    "male_sweatpants_black_Recipe",
                    "male_shoes_tall_Recipe",
                    "gs-acc-glasses",
                },
                Skin = Skin("Bronze"),
                Hair = Hair("Jet black"),
                Brows = Hair("Jet black"),
                Eyes = Eyes("Black-brown"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("2E3238") },
                Dna = new Dictionary<string, float> { ["height"] = .26f, ["upperMuscle"] = .46f, ["legsSize"] = .56f, ["noseSize"] = .56f },
            },

            // The Caregiver. Fair, copper hair to the shoulders, a grey knit.
            ["jamie-roberts"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "Hair_CurveUnder_Recipe",
                    "Eyebrows_Thin_Average",
                    "underwear_bra_white_Recipe",
                    "underwear_white_granit_bottom_Recipe",
                    "sportswear_sweater_granit_Recipe",
                    "tights_gray_Recipe",
                    "shoe_low_white_Recipe",
                },
                Skin = Skin("Warm ivory"),
                Hair = Hair("Copper"),
                Brows = Hair("Auburn"),
                Eyes = Eyes("Hazel"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("8A8F99") },
                Dna = new Dictionary<string, float> { ["height"] = .06f, ["headSize"] = .76f, ["upperWeight"] = .6f },
            },

            // The Influencer. Latina, a dark messy updo, a burgundy turtleneck.
            ["quinn-martinez"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "Hair_UpwardBun_Recipe",
                    "Eyebrows_Arched_Bushy",
                    "underwear_bra_white_Recipe",
                    "underwear_white_granit_bottom_Recipe",
                    "sportswear_sweater_granit_Recipe",
                    "tights_gray_Recipe",
                    "shoes_tall_white_Recipe",
                },
                Skin = Skin("Honey"),
                Hair = Hair("Dark brown"),
                Brows = Hair("Black"),
                Eyes = Eyes("Dark brown"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("6E1E2A") },
                Dna = new Dictionary<string, float> { ["height"] = .17f, ["eyeSize"] = .68f, ["lipsSize"] = .62f },
            },

            // The Protector. Deep brown skin, a close crop, a goatee, a navy polo.
            ["avery-thompson"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "gs-hair-fade",
                    "Eyebrows_Bushy_Average",
                    "Beard_Goatee",
                    "male_underwear_tighty_Recipe",
                    "male_tshirt_white_Recipe",
                    "male_sportpants_alt_black_Recipe",
                    "male_shoes_tall_Recipe",
                },
                Skin = Skin("Cocoa"),
                Hair = Hair("Black"),
                Brows = Hair("Black"),
                Eyes = Eyes("Black-brown"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("1E2A44") },
                Dna = new Dictionary<string, float> { ["height"] = .24f, ["armWidth"] = .74f, ["upperMuscle"] = .78f, ["lowerMuscle"] = .72f, ["noseWidth"] = .58f, ["lipsSize"] = .58f },
            },

            // The Firebrand. East Asian, a high black ponytail, a charcoal racer tank.
            ["taylor-kim"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "HairPonytail_Recipe",
                    "Eyebrows_Average_Average",
                    "underwear_bra_white_Recipe",
                    "sports_underwear_bottoms_Recipe",
                    "sportswear_top_Recipe",
                    "sportwear_pants_granit_Recipe",
                    "shoe_low_white_Recipe",
                },
                Skin = Skin("Light beige"),
                Hair = Hair("Jet black"),
                Brows = Hair("Black"),
                Eyes = Eyes("Dark brown"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("1E2126") },
                Dna = new Dictionary<string, float> { ["upperMuscle"] = .7f, ["lowerMuscle"] = .7f, ["waist"] = .42f, ["eyeRotation"] = .57f, ["noseFlatten"] = .56f },
            },

            // The Leader. Latino, tan, black hair in a side part, a short beard, a light-blue shirt.
            ["sam-williams"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "Hair_LeftPart_Recipe",
                    "Eyebrows_Average_Bushy",
                    "Beard_Trimmed",
                    "male_underwear_tighty_Recipe",
                    "male_tshirt_white_Recipe",
                    "male_sportpants_grey_Recipe",
                    "male_shoe_low_white.001_Recipe",
                },
                Skin = Skin("Caramel"),
                Hair = Hair("Black"),
                Brows = Hair("Black"),
                Eyes = Eyes("Brown"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("A9C8EA") },
                Dna = new Dictionary<string, float> { ["height"] = .22f, ["upperMuscle"] = .66f, ["chinSize"] = .58f },
            },

            // The Shadow. Swept dark hair, green eyes, a charcoal tee.
            ["blake-peterson"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "HairMessyUp_Recipe",
                    "Eyebrows_Average_Average",
                    "male_underwear_tighty_Recipe",
                    "male_tshirt_white_Recipe",
                    "male_sportpants_alt_black_Recipe",
                    "male_shoes_tall_Recipe",
                },
                Skin = Skin("Sand"),
                Hair = Hair("Dark brown"),
                Brows = Hair("Dark brown"),
                Eyes = Eyes("Grey-green"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("2E3238") },
                Dna = new Dictionary<string, float> { ["height"] = .19f, ["upperMuscle"] = .52f, ["noseSize"] = .56f, ["cheekPronounced"] = .58f },
            },

            // The Diplomat. Light olive skin, black hair slicked back from a centre part, gold hoops, a
            // black V-neck.
            ["maya-hassan"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "Hair_PulledBack_Recipe",
                    "Eyebrows_Arched_Average",
                    "underwear_bra_white_Recipe",
                    "underwear_white_granit_bottom_Recipe",
                    "tshirt_turquoise_Recipe",
                    "tights_gray_Recipe",
                    "shoes_tall_white_Recipe",
                    "gs-acc-hoops",
                },
                Skin = Skin("Olive"),
                Hair = Hair("Jet black"),
                Brows = Hair("Black"),
                Eyes = Eyes("Brown"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("1E2126") },
                Dna = new Dictionary<string, float> { ["height"] = .2f, ["upperMuscle"] = .52f, ["cheekPronounced"] = .58f },
            },

            // ------------------------------------------------------------------ all-stars

            // The Funeral Director. Spiky gelled dark hair, a black shirt.
            ["dan-gheesling"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "Hair_Pointy_Recipe",
                    "Eyebrows_Average_Average",
                    "male_underwear_tighty_Recipe",
                    "male_tshirt_white_Recipe",
                    "male_sportpants_grey_Recipe",
                    "male_shoe_low_white.001_Recipe",
                },
                Skin = Skin("Golden"),
                Hair = Hair("Dark brown"),
                Brows = Hair("Dark brown"),
                Eyes = Eyes("Brown"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("1E2126") },
                // His face: fitted with the game's own face sliders (18 November 2026). On 20 November 2026
                // lipsSize .25 -> .38 so the lips stay closed. Head size and the rest are the house's.
                Dna = new Dictionary<string, float>
                {
                    ["height"] = .20f,
                    ["upperWeight"] = .58f,
                    ["BrowPosition"] = .70f, ["cheekPosition"] = .51f, ["cheekPronounced"] = .57f, ["cheekSize"] = .65f,
                    ["chinPosition"] = .25f, ["chinPronounced"] = .75f, ["chinSize"] = .25f, ["eyeRotation"] = .35f,
                    ["eyeSize"] = .25f, ["foreheadPosition"] = .54f, ["foreheadSize"] = .25f, ["headWidth"] = .85f,
                    ["jawsPosition"] = .52f, ["jawsSize"] = .70f, ["lipsSize"] = .38f, ["lowCheekPosition"] = .43f,
                    ["lowCheekPronounced"] = .53f, ["mandibleSize"] = .57f, ["mouthSize"] = .39f, ["noseBroken"] = .45f,
                    ["noseCurve"] = .53f, ["noseInclination"] = .44f, ["nosePosition"] = .75f, ["nosePronounced"] = .46f,
                    ["noseSize"] = .25f, ["noseWidth"] = .73f,
                },
            },

            // The Puppet Master. Fair, dark hair swept back, a brown shirt.
            ["dr-will-kirby"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "Hair_StraigntPulledBack_Recipe",
                    "Eyebrows_Arched_Average",
                    "male_underwear_tighty_Recipe",
                    "male_tshirt_white_Recipe",
                    "male_sportpants_alt_black_Recipe",
                    "male_shoes_tall_Recipe",
                },
                Skin = Skin("Sand"),
                Hair = Hair("Soft black"),
                Brows = Hair("Soft black"),
                Eyes = Eyes("Dark brown"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("5A4636") },
                // His face: fitted with the game's own face sliders, then built in the game and compared
                // (12 November 2026); mandibleSize put back at the house's and lipsSize .34 -> .49 (20 November 2026: his
                // lips stay closed). The fit's 25 changed values that remain; head size and the rest are the house's.
                Dna = new Dictionary<string, float>
                {
                    ["height"] = .21f,
                    ["BrowPosition"] = .56f, ["cheekPosition"] = .52f, ["cheekPronounced"] = .49f, ["cheekSize"] = .34f,
                    ["chinPosition"] = .36f, ["chinPronounced"] = .58f, ["chinSize"] = .46f,
                    ["foreheadPosition"] = .40f, ["foreheadSize"] = .41f, ["headWidth"] = .91f,
                    ["jawsPosition"] = .29f, ["jawsSize"] = .72f, ["lipsSize"] = .49f, ["lowCheekPosition"] = .53f,
                    ["lowCheekPronounced"] = .52f, ["mouthSize"] = .53f, ["noseBroken"] = .51f, ["noseCurve"] = .38f,
                    ["noseInclination"] = .27f, ["nosePosition"] = .75f, ["nosePronounced"] = .57f, ["noseSize"] = .37f,
                    ["noseWidth"] = .60f,
                    // His eye size and tilt (20 November 2026), in place of the fit's .13 and .53.
                    ["eyeSize"] = .40f, ["eyeRotation"] = .49f,
                },
            },

            // The Undercover Boss. Fair, a sandy buzz cut, blue eyes, stocky, a blue plaid shirt.
            ["derrick-levasseur"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "gs-hair-buzz",
                    "Eyebrows_Bushy_Average",
                    "male_underwear_tighty_Recipe",
                    "male_hoodie_blueWhite_Recipe",
                    "male_sportpants_blueWhite_Recipe",
                    "male_shoe_low_white.001_Recipe",
                },
                Skin = Skin("Rose"),
                Hair = Hair("Dark blonde"),
                Brows = Hair("Dark blonde"),
                Eyes = Eyes("Blue"),
                // His face: fitted with the game's own face sliders (19 November 2026). On 20 November 2026
                // mandibleSize .47 -> .50 so the lips stay closed. Head size and the rest are the house's.
                Dna = new Dictionary<string, float>
                {
                    ["height"] = .18f,
                    ["armWidth"] = .70f,
                    ["upperMuscle"] = .68f,
                    ["upperWeight"] = .62f,
                    ["BrowPosition"] = .25f, ["cheekPosition"] = .25f, ["cheekPronounced"] = .75f, ["cheekSize"] = .28f,
                    ["chinPosition"] = .64f, ["chinPronounced"] = .67f, ["chinSize"] = .75f,
                    ["foreheadPosition"] = .26f, ["foreheadSize"] = .63f, ["headWidth"] = 1f, ["jawsPosition"] = .25f,
                    ["jawsSize"] = .75f, ["mandibleSize"] = .50f, ["mouthSize"] = .25f, ["noseBroken"] = .53f,
                    ["noseCurve"] = .46f, ["noseInclination"] = .54f, ["nosePosition"] = .56f, ["nosePronounced"] = .47f,
                    ["noseSize"] = .40f, ["noseWidth"] = .75f,
                    // His eye size and tilt (20 November 2026), the tilt in place of the fit's .41.
                    ["eyeSize"] = .36f, ["eyeRotation"] = .53f,
                },
            },

            // The Comp Queen. Platinum hair worn long and down, a coral top, a beaded necklace.
            ["janelle-pierzina"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "bb_female_hair_Recipe",
                    "Eyebrows_Arched_Average",
                    "underwear_bra_white_Recipe",
                    "underwear_white_granit_bottom_Recipe",
                    "tanktop_yellow_Recipe",
                    "tights_gray_Recipe",
                    "shoe_low_white_Recipe",
                    "gs-acc-beads",
                },
                Skin = Skin("Warm ivory"),
                Hair = Hair("Platinum"),
                Brows = Hair("Honey blonde"),
                Eyes = Eyes("Blue"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("F08A80") },
                // Her eye tilt (20 November 2026).
                Dna = new Dictionary<string, float> { ["height"] = .23f, ["upperMuscle"] = .68f, ["lowerMuscle"] = .66f, ["eyeRotation"] = .65f },
            },

            // The Hitman's Partner. Tousled brown hair, a blue V-neck.
            ["cody-calafiore"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "HairAnimeMessy_Recipe",
                    "Eyebrows_Average_Bushy",
                    "male_underwear_tighty_Recipe",
                    "male_tshirt_white_Recipe",
                    "male_shorts_hive_Recipe",
                    "male_shoe_low_white.001_Recipe",
                },
                Skin = Skin("Light beige"),
                Hair = Hair("Medium brown"),
                Brows = Hair("Medium brown"),
                Eyes = Eyes("Hazel"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("3F78C8") },
                // His face: fitted with the game's own face sliders (20 November 2026). These are
                // the fit set's 10 changed values; head size and the rest are the house's.
                Dna = new Dictionary<string, float>
                {
                    ["height"] = .24f,
                    ["upperMuscle"] = .72f,
                    ["waist"] = .40f,
                    ["cheekPosition"] = .25f, ["cheekPronounced"] = .29f, ["cheekSize"] = .75f, ["chinPosition"] = .40f,
                    ["foreheadPosition"] = .25f, ["foreheadSize"] = .25f, ["jawsPosition"] = .25f, ["jawsSize"] = .65f,
                    ["lowCheekPronounced"] = .68f, ["mandibleSize"] = .55f,
                    // His eye size and tilt (20 November 2026).
                    ["eyeSize"] = .36f, ["eyeRotation"] = .43f,
                },
            },

            // The Black Widow. Medium-deep brown skin, shoulder-length dark hair flipped out at the ends, studs, a
            // cream sleeveless top.
            ["danielle-reyes"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "Hair_Bob_Recipe",
                    "Eyebrows_Thin_Average",
                    "underwear_bra_white_Recipe",
                    "underwear_white_granit_bottom_Recipe",
                    "tanktop_whiteBlackStraps_Recipe",
                    "tights_gray_Recipe",
                    "shoes_tall_white_Recipe",
                    "gs-acc-studs",
                },
                Skin = Skin("Sienna"),
                Hair = Hair("Dark brown"),
                Brows = Hair("Soft black"),
                Eyes = Eyes("Dark brown"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("E7D9C0") },
                // Her face: fitted with the game's own face sliders (19 November 2026). These are
                // the fit set's 22 changed values; head size and the rest are the house's.
                Dna = new Dictionary<string, float>
                {
                    ["height"] = .13f,
                    ["eyeSpacing"] = .56f,
                    ["lipsSize"] = .60f,
                    ["cheekPosition"] = .51f, ["cheekPronounced"] = .46f, ["cheekSize"] = .53f, ["chinPosition"] = .54f,
                    ["chinPronounced"] = .54f, ["chinSize"] = .41f, ["foreheadPosition"] = .57f,
                    ["foreheadSize"] = .25f, ["headWidth"] = .62f, ["jawsPosition"] = .37f, ["jawsSize"] = .53f,
                    ["lowCheekPosition"] = .53f, ["lowCheekPronounced"] = .47f, ["mandibleSize"] = .48f, ["noseBroken"] = .46f,
                    ["noseCurve"] = .28f, ["noseInclination"] = .48f, ["nosePosition"] = .43f, ["nosePronounced"] = .46f,
                    ["noseSize"] = .42f, ["noseWidth"] = .74f,
                    // Her eye tilt (20 November 2026), in place of the fit's .64.
                    ["eyeRotation"] = .46f,
                },
            },

            // The Poker Player. Long straight blonde hair under a black trucker cap, a black blazer.
            ["vanessa-rousso"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "bb_female_hair_Recipe",
                    "Eyebrows_Average_Average",
                    "underwear_bra_white_Recipe",
                    "underwear_white_granit_bottom_Recipe",
                    "jacket_hive.001_Recipe",
                    "tights_gray_Recipe",
                    "shoes_tall_white_Recipe",
                    "gs-acc-cap",
                },
                Skin = Skin("Warm ivory"),
                Hair = Hair("Golden blonde"),
                Brows = Hair("Light brown"),
                Eyes = Eyes("Brown"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("1E2126") },
                // Her face: fitted with the game's own face sliders (19 November 2026). These are
                // the fit set's 20 changed values; head size and the rest are the house's.
                Dna = new Dictionary<string, float>
                {
                    ["height"] = .15f,
                    ["lipsSize"] = .44f,
                    ["foreheadSize"] = .58f,
                    ["BrowPosition"] = .25f, ["cheekPosition"] = .29f, ["cheekPronounced"] = .31f, ["cheekSize"] = .58f,
                    ["chinPosition"] = .48f, ["chinPronounced"] = .32f, ["chinSize"] = .39f, ["headWidth"] = .53f,
                    ["jawsPosition"] = .65f, ["jawsSize"] = .25f, ["lowCheekPosition"] = .75f, ["lowCheekPronounced"] = .75f,
                    ["mandibleSize"] = .54f, ["noseBroken"] = .45f, ["noseCurve"] = .38f, ["noseInclination"] = .43f,
                    ["nosePosition"] = .34f, ["nosePronounced"] = .61f, ["noseSize"] = .64f, ["noseWidth"] = .66f,
                },
            },

            // The Surfer Strategist. Sun-bleached curls, a white island shirt.
            ["tyler-crispen"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "Hair_Poofy",
                    "Eyebrows_Thin_Average",
                    "male_underwear_tighty_Recipe",
                    "male_tshirt_white_Recipe",
                    "male_swimwear_granit_Recipe",
                    "male_shoe_low_white.001_Recipe",
                },
                Skin = Skin("Golden"),
                Hair = Hair("Golden blonde"),
                Brows = Hair("Light brown"),
                Eyes = Eyes("Blue"),
                // His face: fitted with the game's own face sliders (18 November 2026). These are
                // the fit set's 22 changed values; head size and the rest are the house's.
                Dna = new Dictionary<string, float>
                {
                    ["height"] = .22f,
                    ["upperMuscle"] = .64f,
                    ["belly"] = .38f,
                    ["BrowPosition"] = .35f, ["cheekPosition"] = .51f, ["cheekPronounced"] = .59f, ["cheekSize"] = .38f,
                    ["chinPosition"] = .35f, ["chinPronounced"] = .75f, ["chinSize"] = .56f,
                    ["foreheadPosition"] = .46f, ["foreheadSize"] = .40f, ["headWidth"] = 1f, ["jawsPosition"] = .68f,
                    ["jawsSize"] = .64f, ["lowCheekPosition"] = .27f, ["lowCheekPronounced"] = .55f, ["mandibleSize"] = .52f,
                    ["mouthSize"] = .45f, ["noseCurve"] = .54f, ["noseInclination"] = .33f, ["nosePosition"] = .65f,
                    ["nosePronounced"] = .64f, ["noseWidth"] = .58f,
                    // His eye size and tilt (20 November 2026), the tilt in place of the fit's .28.
                    ["eyeSize"] = .41f, ["eyeRotation"] = .44f,
                },
            },

            // The Assassin. Deep brown skin, long dark waves worn down, gold hoops, a hot-pink tank.
            ["chelsie-baham"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "Hair_LongSwept_Recipe",
                    "Eyebrows_Average_Average",
                    "underwear_bra_white_Recipe",
                    "underwear_white_granit_bottom_Recipe",
                    "tanktop_yellow_Recipe",
                    "tights_gray_Recipe",
                    "shoe_low_white_Recipe",
                    "gs-acc-hoops",
                },
                Skin = Skin("Umber"),
                Hair = Hair("Soft black"),
                Brows = Hair("Black"),
                Eyes = Eyes("Dark brown"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("E23D8A") },
                // Her face: fitted with the game's own face sliders (20 November 2026). These are
                // the fit set's 21 changed values; the rest are the house's.
                Dna = new Dictionary<string, float>
                {
                    ["height"] = .14f,
                    ["headSize"] = .74f,
                    // Her mouth width (20 November 2026).
                    ["mouthSize"] = .37f,
                    ["lipsSize"] = .60f,
                    ["cheekPosition"] = .49f, ["cheekPronounced"] = .52f, ["cheekSize"] = .54f, ["chinPosition"] = .49f,
                    ["chinPronounced"] = .60f, ["chinSize"] = .55f, ["eyeRotation"] = .57f, ["foreheadPosition"] = .52f,
                    ["foreheadSize"] = .33f, ["headWidth"] = .60f, ["jawsPosition"] = .45f, ["jawsSize"] = .57f,
                    ["lowCheekPosition"] = .49f, ["lowCheekPronounced"] = .51f, ["mandibleSize"] = .55f, ["noseBroken"] = .47f,
                    ["noseCurve"] = .36f, ["noseInclination"] = .47f, ["nosePronounced"] = .47f, ["noseSize"] = .46f,
                    ["noseWidth"] = .55f,
                },
            },

            // The Vegas Showgirl. Deep auburn waves, blue eyes, a red dress, a gold collar.
            ["rachel-reilly"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "Hair_CurveUnder_Recipe",
                    "Eyebrows_Arched_Bushy",
                    "underwear_bra_white_Recipe",
                    "underwear_white_granit_bottom_Recipe",
                    "dress_tennis_granit_Recipe",
                    "tights_gray_Recipe",
                    "shoes_tall_turquoise.001_Recipe",
                    "gs-acc-collar",
                },
                Skin = Skin("Ivory"),
                Hair = Hair("Auburn"),
                Brows = Hair("Auburn"),
                Eyes = Eyes("Blue"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("B0201E") },
                // Her face: fitted with the game's own face sliders (20 November 2026). These are
                // the fit set's 11 changed values; head size and the rest are the house's.
                Dna = new Dictionary<string, float>
                {
                    ["height"] = .18f,
                    // Her eye size (20 November 2026).
                    ["eyeSize"] = .56f,
                    ["lipsSize"] = .64f,
                    ["BrowPosition"] = .48f, ["cheekPronounced"] = .26f, ["chinPosition"] = .25f, ["chinPronounced"] = .61f,
                    ["chinSize"] = .25f, ["foreheadPosition"] = .37f, ["foreheadSize"] = .25f, ["jawsPosition"] = .37f,
                    ["jawsSize"] = .51f, ["lowCheekPosition"] = .75f, ["lowCheekPronounced"] = .52f,
                },
            },

            // The Cookout Captain. Deep brown skin, a shaved head, a full short beard, an orange polo.
            ["xavier-prather"] = new UmaCastLook
            {
                Race = MaleRace,
                Wardrobe = new[]
                {
                    "Eyebrows_Average_Average",
                    "Beard_Trimmed",
                    "male_underwear_tighty_Recipe",
                    "male_tshirt_white_Recipe",
                    "male_sportpants_alt_black_Recipe",
                    "male_shoes_tall_Recipe",
                },
                Skin = Skin("Espresso"),
                Hair = Hair("Black"),
                Brows = Hair("Black"),
                Eyes = Eyes("Black-brown"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("E8892B") },
                // His eye tilt (20 November 2026).
                Dna = new Dictionary<string, float> { ["height"] = .25f, ["armWidth"] = .68f, ["upperMuscle"] = .62f, ["noseWidth"] = .58f, ["eyeRotation"] = .44f },
            },

            // The Floater Queen. East Asian, long reddish-brown hair, drop earrings, a black halter.
            ["jun-song"] = new UmaCastLook
            {
                Race = FemaleRace,
                Wardrobe = new[]
                {
                    "Hair_Straight_Recipe",
                    "Eyebrows_Thin_Bushy",
                    "underwear_bra_white_Recipe",
                    "underwear_white_granit_bottom_Recipe",
                    "tanktop_whiteBlackStraps_Recipe",
                    "sportwear_pants_granit_Recipe",
                    "shoe_low_white_Recipe",
                    "gs-acc-drops",
                },
                Skin = Skin("Light beige"),
                Hair = Hair("Chestnut"),
                Brows = Hair("Dark brown"),
                Eyes = Eyes("Dark brown"),
                Fabric = new Dictionary<string, Color> { ["Chest"] = Hex("1E2126") },
                Dna = new Dictionary<string, float> { ["height"] = .09f, ["upperWeight"] = .58f, ["cheekPosition"] = .44f, ["eyeRotation"] = .56f, ["noseFlatten"] = .56f },
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
                Skin = Skin("Caramel"),
                Hair = Hair("Jet black"),
                Brows = Hair("Jet black"),
                Eyes = Eyes("Dark brown"),
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

        /// <summary>The id <see cref="Resolve"/> finds the look by (the character's own first), or null when neither has one.</summary>
        public static string ResolveId(string characterId, string appearanceId)
            => TryGet(characterId, out _) ? characterId : TryGet(appearanceId, out _) ? appearanceId : null;

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }
    }
}
