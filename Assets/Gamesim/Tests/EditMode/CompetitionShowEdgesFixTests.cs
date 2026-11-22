using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The competition show's edges, after the built house's run (competition show edges fix): with
    /// sixteen at the quick pace two of the far end of the house were still walking when the show
    /// began and were left out, and the player's own houseguest ran no course leg. So whoever is
    /// still on the way at the walk out's last moment is now put on their station, and the player's
    /// leg is looked for every way. Every cast size from a pair to sixteen, at both paces, with routes
    /// longer than the walk out's cap and walkers held back in a queue. Pure presentation: no Unity
    /// type, so the subset runs it too.
    /// </summary>
    public sealed class CompetitionShowEdgesFixTests
    {
        private sealed class Field
        {
            public readonly List<string> Stationed = new List<string>();
            public readonly List<string> Placed = new List<string>();
            public readonly List<string> Holding = new List<string>();
            public readonly List<string> LeftOut = new List<string>();
            public float HoldAt = float.NaN, WalkOutBy;
        }

        /// <summary>
        /// A walk out read as the director reads it, every frame of 20 ms: each quarter second the walk
        /// out's time moves on for anybody who cannot make it (up to its cap) and each walker is paced
        /// to make it; the walkers cover only half the pace they are given (a queue at the yard's door),
        /// on routes from 30 m to 200 m, far past what the cap allows. When the walk out's last moment
        /// comes the stragglers are put on their stations by <paramref name="place"/>; past the wait for
        /// the field the hold begins with whoever is there.
        /// </summary>
        private static Field WalkOut(int size, bool quick, Func<string, bool> place)
        {
            const float dt = .02f, queue = .5f;
            var field = new Field();
            var remaining = new Dictionary<string, float>();
            var pace = new Dictionary<string, float>();
            for (int i = 0; i < size; i++)
            {
                string id = "c" + i;
                field.Stationed.Add(id);
                remaining[id] = 30f + 170f * i / Math.Max(1, size - 1);
                pace[id] = CompetitionShowPlan.WalkPace;
            }
            field.WalkOutBy = CompetitionShowPlan.WalkOutSeconds(quick);
            float nextRead = 0f;
            bool placedOnce = false;
            Func<string, bool> arrived = id => remaining[id] <= 0f;
            for (float now = 0f; now < 60f; now += dt)
            {
                if (now >= nextRead)
                {
                    nextRead = now + CompetitionShowPlan.WalkOutRereadSeconds;
                    foreach (var id in field.Stationed.Where(id => !arrived(id)))
                        field.WalkOutBy = CompetitionShowPlan.ExtendWalkOut(field.WalkOutBy, now, 0f, quick, remaining[id]);
                    foreach (var id in field.Stationed.Where(id => !arrived(id)))
                        pace[id] = CompetitionShowPlan.WalkOutPace(remaining[id], field.WalkOutBy - now);
                }
                bool ready = field.Stationed.All(id => arrived(id) || field.Placed.Contains(id));
                if (!ready && now >= CompetitionShowPlan.PlaceBy(field.WalkOutBy, quick))
                {
                    if (!placedOnce)
                    {
                        placedOnce = true;
                        CompetitionShowPlan.PlaceStragglers(field.Stationed, arrived, id =>
                        {
                            if (!place(id)) return false;
                            remaining[id] = 0f;
                            return true;
                        }, field.Placed);
                    }
                    ready = field.Stationed.All(id => arrived(id) || field.Placed.Contains(id));
                }
                if (ready || now >= CompetitionShowPlan.AssemblyBy(field.WalkOutBy, quick))
                {
                    field.HoldAt = now;
                    CompetitionShowPlan.SplitAtHold(field.Stationed, arrived, field.Placed, field.Holding, field.LeftOut);
                    return field;
                }
                foreach (var id in field.Stationed.Where(id => !arrived(id)))
                    remaining[id] -= pace[id] * queue * dt;
            }
            return field;
        }

        [TestCase(true), TestCase(false)]
        public void NobodyIsLeftOutAtAnyCastSizeHoweverLongTheRoutes(bool quick)
        {
            for (int size = 2; size <= 16; size++)
            {
                var field = WalkOut(size, quick, id => true);
                Assert.That(field.HoldAt, Is.Not.NaN, size + ": the hold began.");
                Assert.That(field.LeftOut, Is.Empty, size + ": nobody is left out.");
                Assert.That(field.Holding, Is.EquivalentTo(field.Stationed), size + ": the whole field holds on.");
                Assert.That(field.Placed, Is.Not.Empty, size + ": routes past the cap are put on their stations.");
                Assert.That(field.Placed, Is.SubsetOf(field.Stationed));
                Assert.That(field.WalkOutBy, Is.LessThanOrEqualTo(CompetitionShowPlan.MostWalkOutSeconds(quick) + 1e-3f), size + ": the walk out keeps its cap.");
                Assert.That(field.HoldAt, Is.LessThanOrEqualTo(CompetitionShowPlan.AssemblyBy(field.WalkOutBy, quick)), size + ": the hold begins inside the wait for the field.");
                Assert.That(field.HoldAt, Is.GreaterThanOrEqualTo(field.WalkOutBy), size + ": a walker in their last steps still had the walk out's time.");
            }
        }

        [TestCase(true), TestCase(false)]
        public void WithoutBeingPutThereTheFarEndWouldBeLeftOut(bool quick)
        {
            for (int size = 2; size <= 16; size++)
            {
                var field = WalkOut(size, quick, id => false);
                Assert.That(field.Placed, Is.Empty);
                Assert.That(field.LeftOut, Is.Not.Empty, size + ": routes this long leave the far end out unless they are put on their stations.");
                Assert.That(field.LeftOut, Does.Contain("c" + (size - 1)), size + ": the longest route among them.");
            }
        }

        [Test]
        public void OnlyABodyThatCannotBePutOnItsStationIsLeftOut()
        {
            var stationed = new[] { "a", "b", "c", "d" };
            var there = new HashSet<string> { "a" };
            var placed = new List<string>();
            int still = CompetitionShowPlan.PlaceStragglers(stationed, there.Contains, id => id != "c", placed);
            Assert.That(still, Is.EqualTo(1));
            Assert.That(placed, Is.EqualTo(new[] { "b", "d" }), "Those on the way are put there, in the field's order; one who arrived is not.");
            Assert.That(CompetitionShowPlan.PlaceStragglers(stationed, there.Contains, id => true, placed), Is.EqualTo(0));
            Assert.That(placed, Is.EqualTo(new[] { "b", "d", "c" }), "Nobody is put there twice.");
            var holding = new List<string>(); var leftOut = new List<string>();
            CompetitionShowPlan.SplitAtHold(stationed, there.Contains, new List<string> { "b", "d" }, holding, leftOut);
            Assert.That(holding, Is.EqualTo(new[] { "a", "b", "d" }));
            Assert.That(leftOut, Is.EqualTo(new[] { "c" }));
            foreach (bool quick in new[] { true, false })
            {
                Assert.That(CompetitionShowPlan.PlaceBy(10f, quick), Is.GreaterThan(10f));
                Assert.That(CompetitionShowPlan.PlaceBy(10f, quick), Is.LessThan(CompetitionShowPlan.AssemblyBy(10f, quick)));
            }
        }

        // ------------------------------------------------------------ the player's own leg

        /// <summary>A narrow yard whose edges leave no room for the course's own marks, 2 m and more away.</summary>
        private static CompetitionCourseLegs NarrowYard(out string id)
        {
            id = "you";
            var legs = new CompetitionCourseLegs(-1.3f, 1.3f, 2.1f, 3.6f, 0f, .3f, 0f);
            legs.AddPost(id, 0f, 3.5f, CompetitionCourseLegs.Box.Station(0f, 3.5f, .3f, .2f, .3f));
            return legs;
        }

        [Test]
        public void ThePlayersLegIsFoundEveryWayWhereTheCoursesOwnMarksAreBlocked()
        {
            var legs = NarrowYard(out var id);
            Assert.That(legs.Candidates(id), Is.Empty, "The course's own marks find nothing in this yard.");
            var every = legs.EveryWayCandidates(id).ToList();
            Assert.That(every, Is.Not.Empty, "Tried every way, the player's houseguest still has a leg.");
            var post = legs.PostOf(id);
            foreach (var spot in every)
            {
                Assert.That(legs.Clears(id, spot), Is.True, spot + " keeps the leg clear.");
                Assert.That(spot.Z, Is.LessThanOrEqualTo(post.Z + .3f + 1e-4f), spot + " does not run ahead into the station's own equipment.");
            }
            // The yard's own clear places are tried last, nearest first, and only clear ones are offered.
            var places = new[] { new CompetitionCourseLegs.Spot(-1.2f, 2.2f), new CompetitionCourseLegs.Spot(0f, 9f), new CompetitionCourseLegs.Spot(1f, 2.3f) };
            var withPlaces = legs.EveryWayCandidates(id, places).ToList();
            Assert.That(withPlaces.Take(every.Count), Is.EqualTo(every));
            Assert.That(withPlaces.Skip(every.Count), Is.EqualTo(new[] { places[2], places[0] }), "Nearest first; the place ahead of the post is not offered.");
        }

        [Test]
        public void EveryWayOffersTheCoursesOwnMarksFirstAndKeepsClearOfTheOthers()
        {
            var legs = new CompetitionCourseLegs(-14f, 14f, 10f, 20f, .6f, .3f, 0f);
            var posts = new[] { ("left", -1.8f), ("you", 0f), ("right", 1.8f) };
            foreach (var (name, x) in posts) legs.AddPost(name, x, 18f, CompetitionCourseLegs.Box.Station(x, 18f, .3f, .8f, .5f));
            // Something standing straight back from the middle post, as the course's middle stack may.
            legs.AddObstacle(CompetitionCourseLegs.Box.Body(0f, 15.5f, .5f));
            var own = legs.Candidates("you").ToList();
            var every = legs.EveryWayCandidates("you").ToList();
            Assert.That(own, Is.Not.Empty);
            Assert.That(every.Take(own.Count), Is.EqualTo(own), "The course's own marks come first, in their order.");
            Assert.That(every.Count, Is.GreaterThan(own.Count), "and more ways after them.");
            legs.Accept("left", legs.Candidates("left").First());
            foreach (var spot in legs.EveryWayCandidates("you")) Assert.That(legs.Clears("you", spot), Is.True, spot + " keeps clear of the leg taken.");
            legs.Accept("you", legs.EveryWayCandidates("you").First());
            Assert.That(legs.EveryWayCandidates("you"), Is.Empty, "A runner with a mark is offered no other.");
        }
    }
}
