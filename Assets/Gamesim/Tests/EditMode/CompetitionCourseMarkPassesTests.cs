using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using Pass = Gamesim.Presentation.CompetitionCourseMarkPasses.Pass;
using Refusal = Gamesim.Presentation.CompetitionCourseMarkPasses.Refusal;
using Spot = Gamesim.Presentation.CompetitionCourseLegs.Spot;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The course's turn marks looked for in passes (competition show edges, after the built house's
    /// second run, where the player's own houseguest still ran no leg): every mark the course found
    /// before is found again in its first pass; a runner with none is looked for again with the bodies
    /// on their way to a stage place set aside; the player's own houseguest once more on the walking
    /// map alone; and the yard's plan - the floor, the legs, the spacing, the deck - is never relaxed.
    /// Also the walk out's stragglers tried again after a refusal. Pure presentation: no Unity type,
    /// so the subset runs it too.
    /// </summary>
    public sealed class CompetitionCourseMarkPassesTests
    {
        /// <summary>Three posts in the back row of a 28 by 10 m yard, the player's in the middle.</summary>
        private static CompetitionCourseLegs Yard()
        {
            var legs = new CompetitionCourseLegs(-14f, 14f, 10f, 20f, .6f, .3f, 0f);
            foreach (var (name, x) in new[] { ("left", -1.8f), ("you", .6f), ("right", 2.4f) })
                legs.AddPost(name, x, 18.3f, CompetitionCourseLegs.Box.Station(x, 18.3f, .3f, 1.05f, .51f));
            return legs;
        }

        private static IEnumerable<Spot> Offered(CompetitionCourseLegs legs, string id) => legs.EveryWayCandidates(id);

        [Test]
        public void AMarkFoundAsBeforeIsTakenInTheFirstPassAndNoOtherIsLooked()
        {
            var legs = Yard();
            var tally = new CompetitionCourseMarkPasses.Tally();
            var first = legs.Candidates("left").First();
            bool found = CompetitionCourseMarkPasses.TryChoose(legs, "left", false, () => legs.Candidates("left"),
                (Spot spot, Pass pass, out Spot taken) => { taken = spot; return Refusal.None; }, tally, out var mark, out var takenIn);
            Assert.That(found, Is.True);
            Assert.That(takenIn, Is.EqualTo(Pass.Strict), "Every check as it always was.");
            Assert.That(mark.X, Is.EqualTo(first.X)); Assert.That(mark.Z, Is.EqualTo(first.Z), "The same mark the course took before.");
            Assert.That(legs.Marks.ContainsKey("left"), Is.True, "and accepted on the plan.");
            Assert.That(tally.Tried(Pass.Strict), Is.EqualTo(1));
            Assert.That(tally.Looked(Pass.MoversAside), Is.False, "No later pass is looked at.");
        }

        [Test]
        public void ARunnerWhoseMarksMeetBodiesOnTheirWayToAStagePlaceGetsOneWithThoseBodiesSetAside()
        {
            // As the built house's run read it: the field still stands where the house left it as the marks
            // are chosen, behind the middle post; every mark meets a body then.
            foreach (bool player in new[] { false, true })
            {
                var legs = Yard();
                string id = player ? "you" : "left";
                var tally = new CompetitionCourseMarkPasses.Tally();
                bool found = CompetitionCourseMarkPasses.TryChoose(legs, id, player, () => Offered(legs, id),
                    (Spot spot, Pass pass, out Spot taken) =>
                    {
                        taken = spot;
                        return CompetitionCourseMarkPasses.CountsMovers(pass) ? Refusal.Body : Refusal.None;
                    }, tally, out _, out var takenIn);
                Assert.That(found, Is.True, id + " runs a leg.");
                Assert.That(takenIn, Is.EqualTo(Pass.MoversAside), id + ": found with the bodies on their way set aside.");
                Assert.That(tally.Refused(Pass.Strict, Refusal.Body), Is.EqualTo(tally.Tried(Pass.Strict)), "Every first-pass mark met a body.");
                Assert.That(tally.Tried(Pass.Strict), Is.GreaterThan(0));
                Assert.That(tally.ToString(), Does.Contain("Strict: tried").And.Contain("MoversAside: tried 1"));
            }
        }

        [Test]
        public void YourHouseguestRunsALegOnTheWalkingMapWhenEveryBodyAndColliderCheckRefusesAndTheOthersDoNot()
        {
            CompetitionCourseMarkPasses.Check bodiesEverywhere = (Spot spot, Pass pass, out Spot taken) =>
            {
                taken = spot;
                return CompetitionCourseMarkPasses.ReadsBodies(pass) ? Refusal.Body : Refusal.None;
            };
            var legs = Yard();
            bool yours = CompetitionCourseMarkPasses.TryChoose(legs, "you", true, () => Offered(legs, "you"), bodiesEverywhere, null, out var mark, out var takenIn);
            Assert.That(yours, Is.True, "The player's own houseguest always runs a leg.");
            Assert.That(takenIn, Is.EqualTo(Pass.OnTheWalkingMap));
            Assert.That(legs.Marks["you"].X, Is.EqualTo(mark.X));
            Assert.That(CompetitionCourseMarkPasses.For(true), Is.EqualTo(new[] { Pass.Strict, Pass.MoversAside, Pass.OnTheWalkingMap }));
            var tally = new CompetitionCourseMarkPasses.Tally();
            bool theirs = CompetitionCourseMarkPasses.TryChoose(legs, "right", false, () => Offered(legs, "right"), bodiesEverywhere, tally, out _, out _);
            Assert.That(theirs, Is.False, "A housemate whose every mark meets something still runs no leg, as before.");
            Assert.That(tally.Looked(Pass.OnTheWalkingMap), Is.False);
            Assert.That(legs.Marks.ContainsKey("right"), Is.False);
        }

        [Test]
        public void TheYardsPlanIsNeverRelaxed()
        {
            foreach (var why in new[] { Refusal.Floor, Refusal.Legs, Refusal.Spacing, Refusal.Footprint })
            {
                var legs = Yard();
                var tally = new CompetitionCourseMarkPasses.Tally();
                bool found = CompetitionCourseMarkPasses.TryChoose(legs, "you", true, () => Offered(legs, "you"),
                    (Spot spot, Pass pass, out Spot taken) => { taken = spot; return why; }, tally, out _, out _);
                Assert.That(found, Is.False, why + " refuses in every pass, the player's own included.");
                foreach (var pass in CompetitionCourseMarkPasses.For(true))
                {
                    Assert.That(tally.Looked(pass), Is.True);
                    Assert.That(tally.Refused(pass, why), Is.EqualTo(tally.Tried(pass)).And.GreaterThan(0), pass + ": every mark refused for " + why + ".");
                }
                Assert.That(legs.Marks, Is.Empty);
            }
        }

        [Test]
        public void AStragglerRefusedAtTheWalkOutsLastMomentIsPutOnTheirStationOnALaterTry()
        {
            var stationed = new[] { "a", "b", "c" };
            var placed = new List<string>();
            int tries = 0;
            System.Func<string, bool> arrived = id => id == "a";
            // "c" is paused for a frame: refused at the first try, put there at the next.
            System.Func<string, bool> place = id => id == "b" || id == "c" && ++tries > 1;
            Assert.That(CompetitionShowPlan.PlaceStragglers(stationed, arrived, place, placed), Is.EqualTo(1));
            Assert.That(placed, Is.EqualTo(new[] { "b" }));
            Assert.That(CompetitionShowPlan.PlaceStragglers(stationed, arrived, place, placed), Is.EqualTo(0), "Tried again, nobody is still on the way.");
            Assert.That(placed, Is.EqualTo(new[] { "b", "c" }), "Nobody is put there twice.");
            var holding = new List<string>(); var leftOut = new List<string>();
            CompetitionShowPlan.SplitAtHold(stationed, arrived, placed, holding, leftOut);
            Assert.That(leftOut, Is.Empty, "The show starts with its whole field.");
            Assert.That(holding, Is.EquivalentTo(stationed));
        }
    }
}
