using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// What stands in the yard when (competition yard, slice 2): the competition's equipment is put
    /// away in everyday life and the television set's dressing stays, each piece with its reason, and
    /// a node is read by the name its authoring gives it. Unity-free: the subset runs it too.
    /// </summary>
    public sealed class CompetitionYardPiecesTests
    {
        [Test]
        public void TheCompetitionEquipmentIsPutAwayAndTheTelevisionSetStays()
        {
            string[] away =
            {
                "bb_set_comp_stack", "bb_set_comp_crate", "bb_set_comp_lane", "Start line", "Finish line", "bb_set_comp_gate",
                "bb_set_sign_hoh", "bb_set_sign_pillars", "bb_set_sign_samehouse", "Competition podium",
            };
            string[] stays =
            {
                "bb_set_comp_backdrop", "Yard display", "Flight case", "bb_set_entrancearch", "bb_set_stanchion",
                "bb_set_velvetrope", "bb_set_lighttower", "bb_set_studiocam",
            };
            Assert.That(CompetitionYardPieces.All.Where(piece => piece.State == CompetitionYardPieces.Everyday.PutAway).Select(piece => piece.Name),
                Is.EquivalentTo(away), "The stacks and their crates, the lanes and their markings, the gates, the lettering and the podiums are put away.");
            Assert.That(CompetitionYardPieces.All.Where(piece => piece.State == CompetitionYardPieces.Everyday.Stays).Select(piece => piece.Name),
                Is.EquivalentTo(stays), "The backdrop with the public display, the road cases, the entrance, the light towers and the camera stay.");
            Assert.That(CompetitionYardPieces.Find(CompetitionYardPieces.Stack).Count, Is.EqualTo(3), "The tower of Hanoi is three stacks.");
            Assert.That(CompetitionYardPieces.Find(CompetitionYardPieces.Stack).Collides, Is.True,
                "The stacks carry fitted collision, so their floor is a baked hole while they are away.");
        }

        [Test]
        public void EveryPieceIsListedOnceWithItsPlaceAndAReason()
        {
            Assert.That(CompetitionYardPieces.All.Select(piece => piece.Name), Is.Unique);
            foreach (var piece in CompetitionYardPieces.All)
            {
                Assert.That(piece.Reason, Is.Not.Empty, piece.Name);
                Assert.That(piece.Where, Is.Not.Empty, piece.Name);
                Assert.That(piece.Count, Is.GreaterThan(0), piece.Name);
                Assert.That(CompetitionYardPieces.Find(piece.Name), Is.SameAs(piece), piece.Name + " is found by its own name.");
            }
        }

        [TestCase("Competition podium 2 (set)", "Competition podium")]
        [TestCase("Competition podium 3", "Competition podium")]
        [TestCase("bb_set_comp_stack (2)", "bb_set_comp_stack")]
        [TestCase("Ring 2.0", "Ring 2.0")]
        [TestCase("Seg_00", "Seg_00")]
        public void ANodeIsReadByTheNameItsAuthoringGives(string node, string stem)
        {
            Assert.That(CompetitionYardPieces.Stem(node), Is.EqualTo(stem));
        }

        [Test]
        public void TheYardsEverydayPiecesAndAnythingUnlistedAreNeverPutAway()
        {
            foreach (string name in new[] { "bb_set_pool", "bb_set_hottub", "bb_set_lounger", "bb_set_ph_plant", "bb_set_compring",
                "Competition yard floor", "Competition yard", "bb_set_comp_stack_LOD0", "Competition Circle", "bb_set_comp_backdrop" })
                Assert.That(CompetitionYardPieces.PutAwayInEveryday(name), Is.False, name);
            foreach (string name in new[] { "bb_set_comp_stack", "bb_set_comp_gate (1)", "Competition podium 1 (set)", "Competition podium 1", "Finish line" })
                Assert.That(CompetitionYardPieces.PutAwayInEveryday(name), Is.True, name);
        }
    }
}
