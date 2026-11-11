using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// A look wears the pieces a catalog entry's <c>worn-by:</c> tag names it for (<see cref="LookPieces.Apply"/>),
    /// and LibraryLooks.txt's line (<see cref="LookPieces.LibraryLine"/>). Plain strings: no UMA, no engine.
    /// </summary>
    public sealed class LookPiecesTests
    {
        private const string Female = "Human Female 3.0", Male = "Human Male 3.0";

        private static readonly Dictionary<string, string> Slots = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["hair-a"] = "Hair", ["brows-a"] = "Eyebrows", ["top-a"] = "Chest", ["top-a-recipe"] = "Chest", ["legs-a"] = "Legs",
            ["ears-a"] = "Ears", ["gs-hoops"] = "Earrings",
            ["piece-hair"] = "Hair", ["piece-face"] = "Face", ["piece-ears"] = "Ears", ["piece-top"] = "Chest",
            ["piece-a"] = "Hair", ["piece-b"] = "Hair", ["piece-c"] = "Hair", ["piece-outer"] = "Waist", ["piece-outer-b"] = "Waist",
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
            Assert.That(LookPieces.LibraryLine("x", Male, new Dictionary<string, float>()), Is.EqualTo("x\tHuman Male 3.0"));
            foreach (float value in new[] { 1f / 3f, .0000001f, float.NaN, float.PositiveInfinity })
                Assert.Throws<ArgumentException>(() => LookPieces.LibraryLine("x", Male, new Dictionary<string, float> { ["a"] = value }),
                    "A value that needs rounding is never written.");
            Assert.Throws<ArgumentException>(() => LookPieces.LibraryLine("x\ty", Male, new Dictionary<string, float>()));
        }
    }
}
