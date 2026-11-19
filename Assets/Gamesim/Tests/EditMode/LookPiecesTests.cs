using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// A look wears the pieces a catalog entry's <c>worn-by:</c> tag names it for (<see cref="LookPieces.Apply"/>),
    /// and LibraryLooks.txt's line (<see cref="LookPieces.LibraryLine"/>), and Will's, Dan's, Tyler's, Derrick's and Danielle's fitted faces in the file. Plain strings
    /// and one plain file read: no UMA, no engine.
    /// </summary>
    public sealed class LookPiecesTests
    {
        private const string Female = "Human Female 3.0", Male = "Human Male 3.0";

        private static readonly Dictionary<string, string> Slots = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["hair-a"] = "Hair", ["brows-a"] = "Eyebrows", ["top-a"] = "Chest", ["top-a-recipe"] = "Chest", ["legs-a"] = "Legs",
            ["ears-a"] = "Ears", ["gs-hoops"] = "Earrings",
            ["piece-hair"] = "Hair", ["piece-face"] = "Face", ["piece-ears"] = "Ears", ["piece-top"] = "Chest",
            ["piece-a"] = "Hair", ["piece-b"] = "Hair", ["piece-B"] = "Hair", ["piece-c"] = "Hair", ["piece-outer"] = "Waist", ["piece-outer-b"] = "Waist",
        };

        /// <summary>The one id a line and an over: tag are compared by: an alias reads as its item's id.</summary>
        private static string IdOf(string id) => id == "top-a-recipe" ? "top-a" : id;

        private static string SlotOf(string id) => Slots.TryGetValue(id, out var slot) ? slot : null;

        private static readonly string[] Look = { "hair-a", "brows-a", "top-a", "legs-a", "ears-a", "gs-hoops" };

        private static LookPieces.Piece Piece(string id, string race, params string[] tags) => new LookPieces.Piece
        { Id = id, Slot = SlotOf(id), Tags = tags, Races = race == null ? new[] { Female, Male } : new[] { race } };

        private static string[] Apply(string look, string[] wardrobe, List<string> warnings, params LookPieces.Piece[] pieces) =>
            LookPieces.Apply(look, Female, wardrobe, pieces, SlotOf, IdOf, warnings);

        [Test]
        public void E1_NothingTaggedForTheLookReturnsItsOwnWardrobe()
        {
            var warnings = new List<string>();
            Assert.That(Apply("look-a", Look, warnings), Is.SameAs(Look));
            Assert.That(Apply("look-a", Look, warnings, Piece("piece-hair", null, "worn-by:look-b", "long")), Is.SameAs(Look),
                "A piece for another look leaves this one as it is.");
            Assert.That(Apply("look-a", Look, warnings, Piece("piece-hair", null, "Worn-By:look-a"), Piece("piece-face", null, "worn-by: look-a"),
                Piece("piece-ears", null, "worn-by:look-a ")), Is.SameAs(Look), "The tag is exact, ordinal and lower case.");
            Assert.That(LookPieces.Apply(null, Female, Look, new[] { Piece("piece-hair", null, "worn-by:") }, SlotOf, IdOf, warnings), Is.SameAs(Look));
            Assert.That(Look, Is.EqualTo(new[] { "hair-a", "brows-a", "top-a", "legs-a", "ears-a", "gs-hoops" }), "Never written to.");
            Assert.That(warnings, Is.Empty);
        }

        [Test]
        public void E2_ATaggedPieceIsWornInItsSlot()
        {
            var warnings = new List<string>();
            var worn = Apply("look-a", Look, warnings, Piece("piece-hair", Female, "worn-by:look-a"));
            Assert.That(worn, Is.EqualTo(new[] { "piece-hair", "brows-a", "top-a", "legs-a", "ears-a", "gs-hoops" }));
            Assert.That(warnings, Is.Empty);
        }

        [Test]
        public void E3_ItReplacesTheLooksLineAndItsCodeBuiltTwin()
        {
            var warnings = new List<string>();
            var worn = Apply("look-a", Look, warnings, Piece("piece-ears", Female, "worn-by:look-a"));
            Assert.That(worn, Is.EqualTo(new[] { "hair-a", "brows-a", "top-a", "legs-a", "piece-ears" }));
            var twinFirst = new[] { "gs-hoops", "hair-a", "ears-a" };
            Assert.That(Apply("look-a", twinFirst, warnings, Piece("piece-ears", Female, "worn-by:look-a")), Is.EqualTo(new[] { "piece-ears", "hair-a" }),
                "In the place of the first line of that place on the body, the code-built twin too.");
            Assert.That(warnings, Is.Empty);
        }

        [Test]
        public void E4_ItIsAddedWhereTheLookWearsNothing()
        {
            var warnings = new List<string>();
            var worn = Apply("look-a", Look, warnings, Piece("piece-face", Female, "worn-by:look-a"));
            Assert.That(worn, Is.EqualTo(Look.Concat(new[] { "piece-face" }).ToArray()));
            Assert.That(warnings, Is.Empty);
        }

        [Test]
        public void E5_APieceNotCutForTheLooksRaceIsSkippedWithOneWarning()
        {
            var warnings = new List<string>();
            var worn = Apply("look-a", Look, warnings, Piece("piece-hair", Male, "worn-by:look-a"));
            Assert.That(worn, Is.EqualTo(Look));
            Assert.That(warnings, Has.Count.EqualTo(1));
            Assert.That(warnings[0], Does.Contain("look-a").And.Contain("Hair"));
        }

        [Test]
        public void E6_TwoForOneSlotWearTheLowestIdWithOneWarning()
        {
            var warnings = new List<string>();
            var worn = Apply("look-a", Look, warnings, Piece("piece-b", Female, "worn-by:look-a"), Piece("piece-a", Female, "worn-by:look-a"));
            Assert.That(worn[0], Is.EqualTo("piece-a"));
            Assert.That(worn.Skip(1), Is.EqualTo(Look.Skip(1)));
            Assert.That(warnings, Has.Count.EqualTo(1), "One warning for the look and the slot.");

            warnings.Clear();
            worn = Apply("look-a", Look, warnings, Piece("piece-c", Female, "worn-by:look-a"), Piece("piece-a", Male, "worn-by:look-a"),
                Piece("piece-b", Female, "worn-by:look-a"), Piece("piece-face", Female, "worn-by:look-a"));
            Assert.That(worn, Is.EqualTo(new[] { "piece-b", "brows-a", "top-a", "legs-a", "ears-a", "gs-hoops", "piece-face" }),
                "The lowest id that fits: a lower one cut for the other race is skipped.");
            Assert.That(warnings, Has.Count.EqualTo(1), "Still one for the Hair slot, none for the Face slot.");

            worn = Apply("look-a", Look, null, Piece("piece-a", Female, "worn-by:look-a"), Piece("piece-B", Female, "worn-by:look-a"));
            Assert.That(worn[0], Is.EqualTo("piece-B"), "Ordinal, not culture order: an upper-case B sorts before a lower-case a.");
        }

        [Test]
        public void E7_AnOuterPieceIsWornOnlyOverATopItNamesAsTheWardrobeStandsAfterTheOtherPieces()
        {
            var warnings = new List<string>();
            var outer = Piece("piece-outer", Female, "worn-by:look-a", "over:piece-top");
            Assert.That(Apply("look-a", Look, warnings, outer), Is.EqualTo(Look),
                "Over a top the look does not wear: left off.");
            Assert.That(Apply("look-a", Look, warnings, outer, Piece("piece-top", Female, "worn-by:look-a")),
                Is.EqualTo(new[] { "hair-a", "brows-a", "piece-top", "legs-a", "ears-a", "gs-hoops", "piece-outer" }),
                "Over its own top, itself a tagged piece: worn.");

            var overOld = Piece("piece-outer", Female, "worn-by:look-a", "over:top-a");
            Assert.That(Apply("look-a", Look, warnings, overOld), Is.EqualTo(Look.Concat(new[] { "piece-outer" }).ToArray()),
                "Over the look's own top: worn.");
            Assert.That(Apply("look-a", Look, warnings, overOld, Piece("piece-top", Female, "worn-by:look-a")),
                Is.EqualTo(new[] { "hair-a", "brows-a", "piece-top", "legs-a", "ears-a", "gs-hoops" }),
                "The top it names was replaced by a tagged top: left off.");

            var alias = new[] { "hair-a", "top-a-recipe" };
            Assert.That(Apply("look-a", alias, warnings, overOld), Is.EqualTo(new[] { "hair-a", "top-a-recipe", "piece-outer" }),
                "A line written as an alias (or recipe name) matches an over: tag naming its item's id.");
            Assert.That(Apply("look-a", Look, warnings, Piece("piece-outer", Female, "worn-by:look-a", "over:top-a-recipe")),
                Is.EqualTo(Look.Concat(new[] { "piece-outer" }).ToArray()), "A line written as the id matches an over: tag naming an alias.");
            var unknown = new[] { "hair-a", "top-x" };
            Assert.That(Apply("look-a", unknown, warnings, Piece("piece-outer", Female, "worn-by:look-a", "over:top-x")),
                Is.EqualTo(new[] { "hair-a", "top-x", "piece-outer" }), "Neither resolves, and the raw strings are equal: worn.");
            Assert.That(Apply("look-a", unknown, warnings, Piece("piece-outer", Female, "worn-by:look-a", "over:top-y")),
                Is.EqualTo(unknown), "Neither resolves, and the raw strings differ: left off.");
            Assert.That(Apply("look-a", Look, warnings, overOld, Piece("piece-outer-b", Female, "worn-by:look-a", "over:piece-outer")),
                Is.EqualTo(Look.Concat(new[] { "piece-outer" }).ToArray()), "No chains: one outer piece never makes way for another.");
            Assert.That(Apply("look-a", Look, warnings, Piece("piece-outer-b", Female, "worn-by:look-a", "over:top-z", "over:top-a"), overOld),
                Is.EqualTo(Look.Concat(new[] { "piece-outer" }).ToArray()), "Two outer pieces that both apply: the lowest id.");
            Assert.That(warnings, Has.Count.EqualTo(1), "Only the duplicate warned; leaving an outer piece off says nothing.");
        }

        [Test]
        public void E8_ASharedPieceDressesEachLookItNames()
        {
            var warnings = new List<string>();
            var shared = Piece("piece-face", null, "worn-by:look-a", "worn-by:look-b", "worn-by:nobody");
            foreach (string look in new[] { "look-a", "look-b" })
                Assert.That(LookPieces.Apply(look, look == "look-a" ? Female : Male, Look, new[] { shared }, SlotOf, IdOf, warnings).Last(),
                    Is.EqualTo("piece-face"), look);
            Assert.That(Apply("look-c", Look, warnings, shared), Is.SameAs(Look), "A look it does not name.");
            Assert.That(warnings, Is.Empty, "An id after worn-by: that no look asks for does nothing.");
        }

        [Test]
        public void E9_TheLibraryLineIsSortedTabbedAndExact()
        {
            var dna = new Dictionary<string, float> { ["height"] = 1f, ["headWidth"] = .58f, ["eyeSize"] = .5f };
            string line = LookPieces.LibraryLine("look-a", Female, dna);
            Assert.That(line, Is.EqualTo("look-a\tHuman Female 3.0\teyeSize=0.5\theadWidth=0.58\theight=1"));
            // The boundary hash: Python's hashlib.sha256(line.encode("utf-8")).hexdigest()[:16] on this line gave the literal.
            using (var sha = SHA256.Create())
            {
                string hex = string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(line)).Select(b => b.ToString("x2")));
                Assert.That(hex.Substring(0, 16), Is.EqualTo("1cce9966a2baf2d0"));
            }
            Assert.That(LookPieces.LibraryLine("x", Male, new Dictionary<string, float> { ["b"] = 0f, ["B"] = .09f, ["a"] = .44f, ["c"] = .125f }),
                Is.EqualTo("x\tHuman Male 3.0\tB=0.09\ta=0.44\tb=0\tc=0.125"), "Ordinal order, no exponent, the fewest decimals.");
            Assert.That(LookPieces.LibraryLine("x", Male, new Dictionary<string, float> { ["a"] = .0625f, ["b"] = -0f }),
                Is.EqualTo("x\tHuman Male 3.0\ta=0.0625\tb=0"), "Up to six decimals when exact, and a negative zero is written 0.");
            Assert.That(LookPieces.LibraryLine("x", Male, new Dictionary<string, float>()), Is.EqualTo("x\tHuman Male 3.0"));
            // 2^-12 = 0.000244140625 is exact as a float but needs twelve decimals: past the six, so it throws.
            foreach (float value in new[] { 1f / 3f, .0000001f, .000244140625f, float.NaN, float.PositiveInfinity })
                Assert.Throws<ArgumentException>(() => LookPieces.LibraryLine("x", Male, new Dictionary<string, float> { ["a"] = value }),
                    "A value that needs rounding is never written.");
            Assert.Throws<ArgumentException>(() => LookPieces.LibraryLine("x\ty", Male, new Dictionary<string, float>()));
        }

        // ------------------------------------------------------------------------- Will's face

        /// <summary>
        /// Will's face: fitted to his reference with the game's own face sliders, then built in the game and compared
        /// (12 November 2026). The fit set's 26 changed values, as UmaCastLibrary's look gives them.
        /// </summary>
        private static readonly Dictionary<string, float> WillFitted = new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["BrowPosition"] = .56f, ["cheekPosition"] = .52f, ["cheekPronounced"] = .49f, ["cheekSize"] = .34f,
            ["chinPosition"] = .36f, ["chinPronounced"] = .58f, ["chinSize"] = .46f, ["eyeRotation"] = .53f,
            ["eyeSize"] = .13f, ["foreheadPosition"] = .40f, ["foreheadSize"] = .41f, ["headWidth"] = .91f,
            ["jawsPosition"] = .29f, ["jawsSize"] = .72f, ["lipsSize"] = .34f, ["lowCheekPosition"] = .53f,
            ["lowCheekPronounced"] = .52f, ["mandibleSize"] = .43f, ["mouthSize"] = .53f, ["noseBroken"] = .51f,
            ["noseCurve"] = .38f, ["noseInclination"] = .27f, ["nosePosition"] = .75f, ["nosePronounced"] = .57f,
            ["noseSize"] = .37f, ["noseWidth"] = .60f,
        };

        /// <summary>His other merged values, as they were before the fit (main at 7a3038a9): the house's, and his own height.</summary>
        private static readonly Dictionary<string, float> WillKept = new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["armWidth"] = .62f, ["feetSize"] = .66f, ["forearmWidth"] = .62f, ["handsSize"] = .68f, ["headSize"] = .72f,
            ["height"] = .21f, ["legsSize"] = .62f, ["lowerMuscle"] = .58f, ["neckThickness"] = .6f, ["upperMuscle"] = .58f,
        };

        /// <summary>
        /// Will's line in LibraryLooks.txt holds his 26 fitted face values and, for every other name, the value it had
        /// before the fit, and no name besides. UmaAllStarPiecesPlayModeTests.LibraryLooksTxtIsTheLibraryByteForByte holds
        /// the file to the merge the game makes for his body (the house's values, then his look's), so the body gets these.
        /// Plain file reading: the editor finds the file through <c>Application.dataPath</c>, the subset by walking up
        /// from its own binary.
        /// </summary>
        [Test]
        public void E10_WillsLineCarriesHisFittedFaceAndKeepsHisOtherValues()
        {
            AssertFittedLine("dr-will-kirby", WillFitted, WillKept);
        }

        // ------------------------------------------------------------------------- Dan's face

        /// <summary>
        /// Dan's face: fitted to his reference with the game's own face sliders, then built in the game and compared
        /// (18 November 2026). The fit set's 26 changed values, as UmaCastLibrary's look gives them.
        /// </summary>
        private static readonly Dictionary<string, float> DanFitted = new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["BrowPosition"] = .70f, ["cheekPosition"] = .51f, ["cheekPronounced"] = .57f, ["cheekSize"] = .65f,
            ["chinPosition"] = .25f, ["chinPronounced"] = .75f, ["chinSize"] = .25f, ["eyeRotation"] = .35f,
            ["eyeSize"] = .25f, ["foreheadPosition"] = .54f, ["foreheadSize"] = .25f, ["headWidth"] = .85f,
            ["jawsPosition"] = .52f, ["jawsSize"] = .70f, ["lipsSize"] = .25f, ["lowCheekPosition"] = .43f,
            ["lowCheekPronounced"] = .53f, ["mandibleSize"] = .57f, ["mouthSize"] = .39f, ["noseBroken"] = .45f,
            ["noseCurve"] = .53f, ["noseInclination"] = .44f, ["nosePosition"] = .75f, ["nosePronounced"] = .46f,
            ["noseSize"] = .25f, ["noseWidth"] = .73f,
        };

        /// <summary>His other merged values, as they were before the fit (main at 8cb31666): the house's, and his own height and build.</summary>
        private static readonly Dictionary<string, float> DanKept = new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["armWidth"] = .62f, ["feetSize"] = .66f, ["forearmWidth"] = .62f, ["handsSize"] = .68f, ["headSize"] = .72f,
            ["height"] = .2f, ["legsSize"] = .62f, ["lowerMuscle"] = .58f, ["neckThickness"] = .6f, ["upperMuscle"] = .58f,
            ["upperWeight"] = .58f,
        };

        /// <summary>
        /// Dan's line in LibraryLooks.txt holds his 26 fitted face values and, for every other name, the value it had
        /// before the fit, and no name besides: the same check as Will's (E10).
        /// </summary>
        [Test]
        public void E12_DansLineCarriesHisFittedFaceAndKeepsHisOtherValues()
        {
            AssertFittedLine("dan-gheesling", DanFitted, DanKept);
        }

        // ------------------------------------------------------------------------- Tyler's face

        /// <summary>
        /// Tyler's face: fitted to his reference with the game's own face sliders, then built in the game and compared
        /// (18 November 2026). The fit set's 22 changed values, as UmaCastLibrary's look gives them.
        /// </summary>
        private static readonly Dictionary<string, float> TylerFitted = new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["BrowPosition"] = .35f, ["cheekPosition"] = .51f, ["cheekPronounced"] = .59f, ["cheekSize"] = .38f,
            ["chinPosition"] = .35f, ["chinPronounced"] = .75f, ["chinSize"] = .56f, ["eyeRotation"] = .28f,
            ["foreheadPosition"] = .46f, ["foreheadSize"] = .40f, ["headWidth"] = 1f, ["jawsPosition"] = .68f,
            ["jawsSize"] = .64f, ["lowCheekPosition"] = .27f, ["lowCheekPronounced"] = .55f, ["mandibleSize"] = .52f,
            ["mouthSize"] = .45f, ["noseCurve"] = .54f, ["noseInclination"] = .33f, ["nosePosition"] = .65f,
            ["nosePronounced"] = .64f, ["noseWidth"] = .58f,
        };

        /// <summary>His other merged values, as they were before the fit (main at 64d52f59): the house's, and his own height and build.</summary>
        private static readonly Dictionary<string, float> TylerKept = new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["armWidth"] = .62f, ["belly"] = .38f, ["eyeSize"] = .62f, ["feetSize"] = .66f, ["forearmWidth"] = .62f,
            ["handsSize"] = .68f, ["headSize"] = .72f, ["height"] = .22f, ["legsSize"] = .62f, ["lowerMuscle"] = .58f,
            ["neckThickness"] = .6f, ["upperMuscle"] = .64f,
        };

        /// <summary>
        /// Tyler's line in LibraryLooks.txt holds his 22 fitted face values and, for every other name, the value it had
        /// before the fit, and no name besides: the same check as Will's (E10).
        /// </summary>
        [Test]
        public void E13_TylersLineCarriesHisFittedFaceAndKeepsHisOtherValues()
        {
            AssertFittedLine("tyler-crispen", TylerFitted, TylerKept, 22);
        }

        // ------------------------------------------------------------------------- Derrick's face

        /// <summary>
        /// Derrick's face: fitted to his reference with the game's own face sliders, then built in the game and compared
        /// (19 November 2026). The fit set's 22 changed values, as UmaCastLibrary's look gives them.
        /// </summary>
        private static readonly Dictionary<string, float> DerrickFitted = new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["BrowPosition"] = .25f, ["cheekPosition"] = .25f, ["cheekPronounced"] = .75f, ["cheekSize"] = .28f,
            ["chinPosition"] = .64f, ["chinPronounced"] = .67f, ["chinSize"] = .75f, ["eyeRotation"] = .41f,
            ["foreheadPosition"] = .26f, ["foreheadSize"] = .63f, ["headWidth"] = 1f, ["jawsPosition"] = .25f,
            ["jawsSize"] = .75f, ["mandibleSize"] = .47f, ["mouthSize"] = .25f, ["noseBroken"] = .53f,
            ["noseCurve"] = .46f, ["noseInclination"] = .54f, ["nosePosition"] = .56f, ["nosePronounced"] = .47f,
            ["noseSize"] = .40f, ["noseWidth"] = .75f,
        };

        /// <summary>
        /// His other merged values, as they were before the fit (main at 64d52f59): the house's, and his own height and build.
        /// His old head width (.62) is not kept: headWidth is one of his fitted values.
        /// </summary>
        private static readonly Dictionary<string, float> DerrickKept = new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["armWidth"] = .7f, ["eyeSize"] = .62f, ["feetSize"] = .66f, ["forearmWidth"] = .62f, ["handsSize"] = .68f,
            ["headSize"] = .72f, ["height"] = .18f, ["legsSize"] = .62f, ["lowerMuscle"] = .58f, ["neckThickness"] = .6f,
            ["upperMuscle"] = .68f, ["upperWeight"] = .62f,
        };

        /// <summary>
        /// Derrick's line in LibraryLooks.txt holds his 22 fitted face values and, for every other name, the value it had
        /// before the fit, and no name besides: the same check as Will's (E10).
        /// </summary>
        [Test]
        public void E14_DerricksLineCarriesHisFittedFaceAndKeepsHisOtherValues()
        {
            AssertFittedLine("derrick-levasseur", DerrickFitted, DerrickKept, 22);
        }

        // ------------------------------------------------------------------------- Danielle's face

        /// <summary>
        /// Danielle's face: fitted to her reference with the game's own face sliders, then built in the game and compared
        /// (19 November 2026). The fit set's 22 changed values, as UmaCastLibrary's look gives them.
        /// </summary>
        private static readonly Dictionary<string, float> DanielleFitted = new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["cheekPosition"] = .51f, ["cheekPronounced"] = .46f, ["cheekSize"] = .53f, ["chinPosition"] = .54f,
            ["chinPronounced"] = .54f, ["chinSize"] = .41f, ["eyeRotation"] = .64f, ["foreheadPosition"] = .57f,
            ["foreheadSize"] = .25f, ["headWidth"] = .62f, ["jawsPosition"] = .37f, ["jawsSize"] = .53f,
            ["lowCheekPosition"] = .53f, ["lowCheekPronounced"] = .47f, ["mandibleSize"] = .48f, ["noseBroken"] = .46f,
            ["noseCurve"] = .28f, ["noseInclination"] = .48f, ["nosePosition"] = .43f, ["nosePronounced"] = .46f,
            ["noseSize"] = .42f, ["noseWidth"] = .74f,
        };

        /// <summary>
        /// Her other merged values, as they were before the fit: the house's, and her own height, eye spacing and lips.
        /// Her old cheek size (.58) and head width (.58) are not kept: both are fitted values.
        /// </summary>
        private static readonly Dictionary<string, float> DanielleKept = new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["armWidth"] = .62f, ["eyeSize"] = .62f, ["eyeSpacing"] = .56f, ["feetSize"] = .66f, ["forearmWidth"] = .62f,
            ["handsSize"] = .68f, ["headSize"] = .72f, ["height"] = .13f, ["legsSize"] = .62f, ["lipsSize"] = .6f,
            ["lowerMuscle"] = .58f, ["neckThickness"] = .6f, ["upperMuscle"] = .58f,
        };

        /// <summary>
        /// Danielle's line in LibraryLooks.txt, on the female race, holds her 22 fitted face values and, for every other
        /// name, the value it had before the fit, and no name besides: the same check as Will's (E10).
        /// </summary>
        [Test]
        public void E15_DaniellesLineCarriesHerFittedFaceAndKeepsHerOtherValues()
        {
            AssertFittedLine("danielle-reyes", DanielleFitted, DanielleKept, 22, Female);
        }

        private static void AssertFittedLine(string id, Dictionary<string, float> fitted, Dictionary<string, float> kept, int count = 26,
            string race = Male)
        {
            string line = File.ReadAllText(LibraryLooksPath(), Encoding.UTF8).Split('\n')
                .Single(text => text.StartsWith(id + "\t", StringComparison.Ordinal));
            string[] fields = line.Split('\t');
            Assert.That(fields[1], Is.EqualTo(race), id + "'s race.");
            var values = fields.Skip(2).Select(field => field.Split('=')).ToDictionary(pair => pair[0],
                pair => float.Parse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture), StringComparer.Ordinal);
            Assert.That(fitted, Has.Count.EqualTo(count), id + ": the fit set's " + count + " changed values.");
            Assert.That(fitted.Keys.Intersect(kept.Keys), Is.Empty, "A name is fitted or kept, not both.");
            Assert.That(values.Keys, Is.EquivalentTo(fitted.Keys.Concat(kept.Keys)), id + ": the names are the " + count + " fitted and the kept, no other.");
            foreach (var entry in fitted)
                Assert.That(values[entry.Key], Is.EqualTo(entry.Value), id + " fitted " + entry.Key);
            foreach (var entry in kept)
                Assert.That(values[entry.Key], Is.EqualTo(entry.Value), id + " kept " + entry.Key);
        }

        // ------------------------------------------------------------------------- The measured ranges (Part B)

        /// <summary>
        /// The face route's 34-slider fit set: the 39 face sliders except headSize, neckThickness, cheekWidth, EyePosition,
        /// Happy_DNA and ElfEars (33), plus headSize.
        /// </summary>
        private static readonly string[] FitSet =
        {
            "BrowPosition", "cheekPosition", "cheekPronounced", "cheekSize", "chinPosition", "chinPronounced", "chinSize",
            "earsPitch", "earsPosition", "earsRotation", "earsSize", "earsYaw", "eyeRotation", "eyeSize", "eyeSpacing",
            "foreheadPosition", "foreheadSize", "headSize", "headWidth", "jawsPosition", "jawsSize", "lipsSize",
            "lowCheekPosition", "lowCheekPronounced", "mandibleSize", "mouthSize", "noseBroken", "noseCurve", "noseFlatten",
            "noseInclination", "nosePosition", "nosePronounced", "noseSize", "noseWidth",
        };

        // The measured ranges, measured on the game's own export on 12 November 2026: the three sliders sampled across
        // their whole travel (eyeSize, headSize, headWidth) hold from 0 to 1; every other fit-set slider from .25 to .75.
        private static readonly string[] WideRange = { "eyeSize", "headSize", "headWidth" };
        private const float WideLow = 0f, WideHigh = 1f, MeasuredLow = .25f, MeasuredHigh = .75f;

        /// <summary>
        /// Every look's line in LibraryLooks.txt holds each fit-set value it sets inside that slider's measured range. A name
        /// of the fit set a look does not set is not checked; a name outside the fit set (height, the body) is not either.
        /// </summary>
        [Test]
        public void E11_EveryLooksFitSetValuesLieInTheirMeasuredRanges()
        {
            Assert.That(FitSet, Has.Length.EqualTo(34).And.Unique);
            Assert.That(FitSet, Is.SupersetOf(WideRange));
            string[] lines = File.ReadAllText(LibraryLooksPath(), Encoding.UTF8).Split('\n').Where(text => text.Length > 0).ToArray();
            Assert.That(lines, Is.Not.Empty);
            var outside = new List<string>();
            int checkedValues = 0;
            foreach (string line in lines)
            {
                string[] fields = line.Split('\t');
                foreach (string field in fields.Skip(2))
                {
                    string[] pair = field.Split('=');
                    if (!FitSet.Contains(pair[0], StringComparer.Ordinal)) continue;
                    float value = float.Parse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture);
                    bool wide = WideRange.Contains(pair[0], StringComparer.Ordinal);
                    float low = wide ? WideLow : MeasuredLow, high = wide ? WideHigh : MeasuredHigh;
                    checkedValues++;
                    if (value < low || value > high)
                        outside.Add(fields[0] + " " + pair[0] + "=" + pair[1] + " (measured range "
                            + low.ToString(CultureInfo.InvariantCulture) + " to " + high.ToString(CultureInfo.InvariantCulture) + ")");
                }
            }
            Assert.That(checkedValues, Is.GreaterThan(0), "The file's looks set fit-set values.");
            Assert.That(outside, Is.Empty, "A look's fit-set value lies outside its slider's measured range: " + string.Join("; ", outside));
        }

        private static string LibraryLooksPath()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Uma", "LibraryLooks.txt"));
#else
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "Gamesim", "Uma", "LibraryLooks.txt");
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }
            Assert.Fail("Assets/Gamesim/Uma/LibraryLooks.txt was not found above " + AppContext.BaseDirectory);
            return null;
#endif
        }
    }
}
