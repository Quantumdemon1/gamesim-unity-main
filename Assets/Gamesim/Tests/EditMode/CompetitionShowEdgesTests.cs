using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The competition show's rough edges (competition show edges): the walk out waits for its whole
    /// field however long the routes and however quick the ceremony; the course's legs run through
    /// no station's equipment and cross no other leg, in a field of one row or two; and a runner
    /// late home still finishes their leg in their place before the card. Every cast size from a
    /// pair to sixteen, at both paces. Pure presentation: no Unity type, so the subset runs it too.
    /// </summary>
    public sealed class CompetitionShowEdgesTests
    {
        // ------------------------------------------------------------ (a) the walk out

        /// <summary>
        /// A walk out to the yard, read as the director reads it: every quarter second each walker
        /// still on their way moves the walk out's time on if they cannot make it at the planned pace,
        /// then is paced to make it. The routes run from the yard's edge (12 m) to the far end of the
        /// house (35 m); everybody passes the yard's door, 6 m before their station, one at a time a
        /// quarter second apart. Returns when each arrived, and the walk out's time, from the start.
        /// </summary>
        private static float[] WalkOut(int field, bool quick, bool extend, out float walkOutBy)
        {
            const float dt = .02f, door = 6f, headway = .25f;
            var remaining = new float[field];
            var arrived = new float[field];
            var pace = new float[field];
            for (int i = 0; i < field; i++)
            {
                remaining[i] = 12f + 23f * i / Math.Max(1, field - 1);
                arrived[i] = float.NaN;
                pace[i] = CompetitionShowPlan.WalkPace;
            }
            walkOutBy = CompetitionShowPlan.WalkOutSeconds(quick);
            float nextRead = 0f, lastThroughDoor = float.NegativeInfinity;
            for (float t = 0f; t < 30f; t += dt)
            {
                if (t >= nextRead - 1e-4f)
                {
                    nextRead = t + CompetitionShowPlan.WalkOutRereadSeconds;
                    if (extend)
                        for (int i = 0; i < field; i++)
                            if (float.IsNaN(arrived[i])) walkOutBy = CompetitionShowPlan.ExtendWalkOut(walkOutBy, t, 0f, quick, remaining[i]);
                    for (int i = 0; i < field; i++) pace[i] = CompetitionShowPlan.WalkOutPace(remaining[i], walkOutBy - t);
                }
                foreach (int i in Enumerable.Range(0, field).OrderBy(k => remaining[k]).ToList())
                {
                    if (!float.IsNaN(arrived[i])) continue;
                    float step = pace[i] * dt, before = remaining[i] - door;
                    if (before > 0f && before - step <= 0f)
                    {
                        if (t - lastThroughDoor < headway) { remaining[i] = door + 1e-4f; continue; }
                        lastThroughDoor = t;
                    }
                    remaining[i] -= step;
                    // Stopping at the station takes a moment more.
                    if (remaining[i] <= 0f) arrived[i] = t + .3f;
                }
                if (arrived.All(at => !float.IsNaN(at)) || t >= CompetitionShowPlan.AssemblyBy(walkOutBy, quick)) break;
            }
            return arrived;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EveryCompetitorReachesTheFieldBeforeTheShowStartsForEveryCastSize(bool quick)
        {
            for (int field = 2; field <= 16; field++)
            {
                var arrived = WalkOut(field, quick, true, out float walkOutBy);
                float assemblyBy = CompetitionShowPlan.AssemblyBy(walkOutBy, quick);
                for (int i = 0; i < field; i++)
                    Assert.That(arrived[i], Is.LessThanOrEqualTo(assemblyBy),
                        field + " competitors, " + (quick ? "quick" : "normal") + ": walker " + i + " stands at their station before the wait for the field is up.");
                Assert.That(walkOutBy, Is.LessThanOrEqualTo(CompetitionShowPlan.MostWalkOutSeconds(quick)), "The walk out keeps its bound.");
                Assert.That(walkOutBy, Is.GreaterThanOrEqualTo(CompetitionShowPlan.WalkOutSeconds(quick)), "and never ends sooner than it did.");
                Assert.That(CompetitionShowPlan.PrincipalBy(walkOutBy, quick), Is.GreaterThan(assemblyBy), "The wait for the winner still follows the wait for the field.");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TheOldFixedWaitLeftTheFarEndOfTheHouseOut(bool quick)
        {
            // The defect the owner saw: a fixed walk out left the far walkers short of the field at a quick wait.
            var arrived = WalkOut(16, quick, false, out float walkOutBy);
            Assert.That(walkOutBy, Is.EqualTo(CompetitionShowPlan.WalkOutSeconds(quick)));
            Assert.That(arrived.Any(at => float.IsNaN(at) || at > CompetitionShowPlan.AssemblyBy(walkOutBy, quick)), Is.True,
                "Without the walk out moving on, somebody is still walking when the show starts.");
        }

        [Test]
        public void TheWalkOutOnlyEverMovesLaterAndNeverPastItsBound()
        {
            foreach (bool quick in new[] { false, true })
            {
                float least = CompetitionShowPlan.WalkOutSeconds(quick), most = CompetitionShowPlan.MostWalkOutSeconds(quick);
                Assert.That(CompetitionShowPlan.ExtendWalkOut(least, 0f, 0f, quick, 10f), Is.EqualTo(least), "A short route moves nothing.");
                Assert.That(CompetitionShowPlan.ExtendWalkOut(least, 0f, 0f, quick, 30f), Is.EqualTo(30f / CompetitionShowPlan.WalkOutPlanPace).Within(1e-4f),
                    "A long route is given its time at the planned pace.");
                Assert.That(CompetitionShowPlan.ExtendWalkOut(least, 2f, 0f, quick, 500f), Is.EqualTo(most), "Never past the bound.");
                Assert.That(CompetitionShowPlan.ExtendWalkOut(9f, 1f, 0f, quick, 3f), Is.EqualTo(Math.Min(9f, most)), "Never earlier than it was.");
                Assert.That(CompetitionShowPlan.ExtendWalkOut(float.NaN, 0f, 0f, quick, float.PositiveInfinity), Is.EqualTo(least), "An unknown route moves nothing.");
                Assert.That(CompetitionShowPlan.ExtendWalkOut(least, 0f, 0f, quick, -1f), Is.EqualTo(least), "Nor does no route.");
                Assert.That(CompetitionShowPlan.ExtendWalkOut(least, 0f, 0f, quick, 30f, 0f), Is.EqualTo(least), "Nor a body with no pace.");
                Assert.That(CompetitionShowPlan.ExtendWalkOut(least, 0f, 5f, quick, 1f), Is.EqualTo(5f + least), "From the show's own start.");
                Assert.That(CompetitionShowPlan.AssemblyBy(least, quick), Is.EqualTo(CompetitionShowPlan.AssemblySeconds(quick)).Within(1e-5f),
                    "A walk out on time keeps the wait for the field as it was");
                Assert.That(CompetitionShowPlan.PrincipalBy(least, quick), Is.EqualTo(CompetitionShowPlan.PrincipalSeconds(quick)).Within(1e-5f), "and the wait for the winner.");
            }
        }

        // ------------------------------------------------------------ (b) and (e) the course's legs

        private const float DeckMinX = -14f, DeckMaxX = 14f, DeckMinZ = 10f, DeckMaxZ = 20f, Reach = 1.05f, HalfWidth = .51f;

        /// <summary>
        /// The posts the arena lays out for a field of <paramref name="field"/> (EpisodeDirector.CompetitionArena):
        /// rows the deepest station's depth apart from the back wall toward the house, places 1.8 m apart
        /// from 2 m inside the west edge, the back rows first and each row from the middle out.
        /// </summary>
        private static List<CompetitionCourseLegs.Spot> Posts(int field, float radius, float arrival = .35f)
        {
            float back = radius + arrival + .04f, front = 1.25f + arrival + .04f, stride = back + front + .06f;
            float backRow = DeckMaxZ - front - .15f, frontLimit = Math.Max(DeckMinZ + 1.5f, DeckMinZ + radius + arrival + .14f);
            var places = new List<CompetitionCourseLegs.Spot>();
            for (float z = backRow; z > frontLimit; z -= stride)
                for (float x = DeckMinX + 2f; x < DeckMaxX - 2f; x += 1.8f) places.Add(new CompetitionCourseLegs.Spot(x, z));
            var posts = places.OrderByDescending(p => p.Z).ThenBy(p => Math.Abs(p.X)).ThenBy(p => p.X).Take(field).ToList();
            Assert.That(posts.Count, Is.EqualTo(field), "The yard holds a field of " + field + ".");
            return posts;
        }

        /// <summary>Lays the legs out as the arena does, taking the first mark offered to each post in the plan's order.</summary>
        private static CompetitionCourseLegs Legs(List<CompetitionCourseLegs.Spot> posts, float radius)
        {
            var legs = new CompetitionCourseLegs(DeckMinX, DeckMaxX, DeckMinZ, DeckMaxZ, radius + .33f, radius, 0f);
            for (int i = 0; i < posts.Count; i++)
                legs.AddPost("c" + i, posts[i].X, posts[i].Z, CompetitionCourseLegs.Box.Station(posts[i].X, posts[i].Z, radius, Reach, HalfWidth));
            foreach (var id in legs.Order)
                foreach (var spot in legs.Candidates(id)) { legs.Accept(id, spot); break; }
            return legs;
        }

        [TestCase(.3f)]
        [TestCase(.35f)]
        [TestCase(.4f)]
        public void NoLegRunsThroughAStationOrCrossesAnotherLegForEveryCastSize(float radius)
        {
            for (int field = 2; field <= 16; field++)
            {
                var posts = Posts(field, radius);
                var legs = Legs(posts, radius);
                Assert.That(legs.Marks.Count, Is.EqualTo(field), field + " competitors: every runner has a leg to run.");
                for (int i = 0; i < field; i++)
                {
                    string id = "c" + i;
                    var from = posts[i];
                    var to = legs.Marks[id];
                    float length = (float)Math.Sqrt((to.X - from.X) * (to.X - from.X) + (to.Z - from.Z) * (to.Z - from.Z));
                    Assert.That(length, Is.GreaterThanOrEqualTo(CompetitionCourseLegs.Distances.Min() - 1e-3f), id + "'s leg is a run, not a step.");
                    Assert.That(to.X, Is.InRange(DeckMinX + radius + .33f, DeckMaxX - radius - .33f), id + "'s mark is on the deck.");
                    Assert.That(to.Z, Is.InRange(DeckMinZ + radius + .33f, DeckMaxZ - radius - .33f));
                    for (int j = 0; j < field; j++)
                    {
                        if (j == i) continue;
                        var station = CompetitionCourseLegs.Box.Station(posts[j].X, posts[j].Z, radius, Reach, HalfWidth);
                        Assert.That(CompetitionCourseLegs.SegmentBoxDistance(from, to, station), Is.GreaterThanOrEqualTo(radius + CompetitionCourseLegs.EquipmentGap - 1e-4f),
                            field + " competitors: " + id + "'s leg " + from + " to " + to + " runs clear of c" + j + "'s console " + station + ".");
                        if (j < i) continue;
                        Assert.That(CompetitionCourseLegs.SegmentDistance(from, to, posts[j], legs.Marks["c" + j]),
                            Is.GreaterThanOrEqualTo(2f * radius + CompetitionCourseLegs.LegGap - 1e-4f), field + " competitors: " + id + "'s and c" + j + "'s legs never meet.");
                    }
                }
            }
        }

        [Test]
        public void OneRowRunsStraightBackAsItAlwaysDid()
        {
            for (int field = 2; field <= 14; field++)
            {
                var posts = Posts(field, .3f);
                var legs = Legs(posts, .3f);
                for (int i = 0; i < field; i++)
                {
                    var mark = legs.Marks["c" + i];
                    Assert.That(mark.X, Is.EqualTo(posts[i].X).Within(1e-4f), field + ": straight back toward the house.");
                    Assert.That(mark.Z, Is.EqualTo(posts[i].Z - CompetitionCourseLegs.Distances[0]).Within(1e-4f), "the whole 4.5 m.");
                }
            }
        }

        [TestCase(15)]
        [TestCase(16)]
        public void OverFourteenTheOldStraightLegsRanThroughTheFrontRow(int field)
        {
            // The defect the owner saw: straight back from the back row ran through the second row's consoles.
            const float radius = .3f;
            var posts = Posts(field, radius);
            bool crossed = false;
            for (int i = 0; i < field && !crossed; i++)
            {
                var to = new CompetitionCourseLegs.Spot(posts[i].X, posts[i].Z - CompetitionCourseLegs.Distances[0]);
                for (int j = 0; j < field; j++)
                    if (j != i && CompetitionCourseLegs.SegmentBoxDistance(posts[i], to, CompetitionCourseLegs.Box.Station(posts[j].X, posts[j].Z, radius, Reach, HalfWidth)) < radius)
                    { crossed = true; break; }
            }
            Assert.That(crossed, Is.True, "A straight leg from the back row meets a front-row station.");
            var legs = Legs(posts, radius);
            Assert.That(legs.Order.Take(field - 14).All(id => posts[int.Parse(id.Substring(1))].Z > posts.Min(p => p.Z)), Is.True,
                "The hemmed-in back-row posts are settled first.");
            Assert.That(legs.Marks.Count(pair => Math.Abs(pair.Value.X - posts[int.Parse(pair.Key.Substring(1))].X) > 1e-3f), Is.GreaterThan(0),
                "and lean away from the middle.");
        }

        [Test]
        public void TheLegsKeepClearOfTheAudienceAndOfAMarkTheArenaMoved()
        {
            const float radius = .3f;
            var posts = Posts(4, radius);
            var legs = new CompetitionCourseLegs(DeckMinX, DeckMaxX, DeckMinZ, DeckMaxZ, radius + .33f, radius, 0f);
            for (int i = 0; i < posts.Count; i++)
                legs.AddPost("c" + i, posts[i].X, posts[i].Z, CompetitionCourseLegs.Box.Station(posts[i].X, posts[i].Z, radius, Reach, HalfWidth));
            // Somebody watching stands right where c0's straight leg would end.
            var watcher = CompetitionCourseLegs.Box.Body(posts[0].X, posts[0].Z - 4.5f, radius);
            legs.AddObstacle(watcher);
            var first = legs.Candidates("c0").First();
            Assert.That(CompetitionCourseLegs.SegmentBoxDistance(posts[0], first, watcher), Is.GreaterThanOrEqualTo(radius + CompetitionCourseLegs.EquipmentGap - 1e-4f),
                "The leg goes short of, or around, the audience.");
            legs.Accept("c0", first);
            Assert.That(legs.Clears("c1", first), Is.False, "Another leg may not end on a mark taken.");
            Assert.That(legs.Candidates("c0"), Is.Empty, "A post with its mark is offered no other.");
            Assert.That(legs.Clears("nobody", first), Is.False);
            Assert.That(legs.Clears("c1", new CompetitionCourseLegs.Spot(DeckMaxX + 1f, posts[1].Z)), Is.False, "Off the deck is never offered.");
        }

        [Test]
        public void TheGeometryMeasuresSegmentsAndBoxes()
        {
            var a = new CompetitionCourseLegs.Spot(0f, 0f); var b = new CompetitionCourseLegs.Spot(2f, 0f);
            Assert.That(CompetitionCourseLegs.SegmentDistance(a, b, new CompetitionCourseLegs.Spot(1f, -1f), new CompetitionCourseLegs.Spot(1f, 1f)), Is.EqualTo(0f).Within(1e-5f), "Crossing.");
            Assert.That(CompetitionCourseLegs.SegmentDistance(a, b, new CompetitionCourseLegs.Spot(0f, 1f), new CompetitionCourseLegs.Spot(2f, 1f)), Is.EqualTo(1f).Within(1e-5f), "Parallel, a metre apart.");
            Assert.That(CompetitionCourseLegs.SegmentDistance(a, b, new CompetitionCourseLegs.Spot(3f, 0f), new CompetitionCourseLegs.Spot(5f, 0f)), Is.EqualTo(1f).Within(1e-5f), "In line, end to end.");
            var box = new CompetitionCourseLegs.Box(1f, 2f, 1f, 2f);
            Assert.That(CompetitionCourseLegs.SegmentBoxDistance(a, new CompetitionCourseLegs.Spot(3f, 3f), box), Is.EqualTo(0f).Within(1e-5f), "Through the box.");
            Assert.That(CompetitionCourseLegs.SegmentBoxDistance(a, b, box), Is.EqualTo(1f).Within(1e-5f), "Under it.");
            Assert.That(CompetitionCourseLegs.SegmentBoxDistance(new CompetitionCourseLegs.Spot(0f, 3f), new CompetitionCourseLegs.Spot(3f, 0f), box), Is.EqualTo(0f).Within(1e-5f), "Corner to corner across it.");
            Assert.That(CompetitionCourseLegs.SegmentDistance(a, b, new CompetitionCourseLegs.Spot(1f, 0f), new CompetitionCourseLegs.Spot(1f, 3f)), Is.EqualTo(0f), "An end on the other leg touches it.");
            Assert.That(CompetitionCourseLegs.SegmentBoxDistance(new CompetitionCourseLegs.Spot(1.5f, 1.5f), new CompetitionCourseLegs.Spot(9f, 9f), box), Is.EqualTo(0f).Within(1e-5f), "From inside it.");
            var station = CompetitionCourseLegs.Box.Station(0f, 0f, .3f, 1.05f, .51f);
            Assert.That(station.MinX, Is.EqualTo(-.51f).Within(1e-5f)); Assert.That(station.MaxZ, Is.EqualTo(1.05f).Within(1e-5f));
            Assert.That(station.MinZ, Is.EqualTo(-.3f).Within(1e-5f), "The console stands ahead of the body, toward the back wall.");
        }

        // ------------------------------------------------------------ (d) a late runner finishes

        /// <summary>
        /// Plays a course's homecomings as the director does, frame by frame: everybody home a leg's run
        /// after their turn, except <paramref name="late"/>, home <paramref name="lateBy"/> past their
        /// own deadline (infinity: never). Returns who finished in order, who did not, and when the field
        /// turned to the winner.
        /// </summary>
        private static (List<string> finished, List<string> didNotFinish, float turned) Homecomings(CompetitionShowTimeline timeline, string late, float lateBy)
        {
            const float dt = 1f / 60f, leg = 4.5f / 3.8f;
            var finished = new List<string>(); var didNotFinish = new List<string>();
            int next = 0; float turned = float.NaN;
            for (float t = 0f; t < 40f && float.IsNaN(turned); t += dt)
            {
                while (next < timeline.Standings.Count)
                {
                    string id = timeline.Standings[next];
                    float homeAt = id == late ? timeline.FinishBy(id) + lateBy : timeline.TurnAt(id) + leg;
                    bool homeward = t >= timeline.TurnAt(id);
                    var call = timeline.CallFor(id, t, t >= homeAt, homeward);
                    if (call == CompetitionShowTimeline.CourseCall.Finish) { finished.Add(id); next++; continue; }
                    if (call == CompetitionShowTimeline.CourseCall.DidNotFinish) { didNotFinish.Add(id); next++; continue; }
                    break;
                }
                if (!timeline.CardWaits(next)) turned = t;
            }
            return (finished, didNotFinish, turned);
        }

        private static List<string> Field(int size) => Enumerable.Range(0, size).Select(i => "runner" + i).ToList();

        [TestCase(false)]
        [TestCase(true)]
        public void ALateRunnerFinishesTheirLegInTheirPlaceBeforeTheCard(bool quick)
        {
            for (int size = 2; size <= 16; size++)
            {
                var timeline = CompetitionShowTimeline.For(CompetitionShowFamily.Course, Field(size), quick);
                foreach (string late in new[] { timeline.Standings[0], timeline.Standings[size / 2], timeline.Standings[size - 1] })
                {
                    var (finished, didNotFinish, turned) = Homecomings(timeline, late, CompetitionShowTimeline.LateGraceSeconds - .5f);
                    Assert.That(didNotFinish, Is.Empty, size + " runners, " + late + " late: everybody finishes.");
                    Assert.That(finished, Is.EqualTo(timeline.Standings), "in the standings' order, the late runner in their place.");
                    Assert.That(turned, Is.GreaterThanOrEqualTo(timeline.FinishBy(late)), "The field turns to the winner only once the late runner is home");
                    Assert.That(turned + CompetitionShowTimeline.WinnerHoldSeconds(quick), Is.LessThanOrEqualTo(timeline.LatestCardAt + 1e-3f), "and the card still plays by its latest.");
                }
            }
        }

        [Test]
        public void ARunnerWhoNeverGetsHomeDoesNotFinishAndTheRestStillDo()
        {
            foreach (bool quick in new[] { false, true })
            {
                var timeline = CompetitionShowTimeline.For(CompetitionShowFamily.Course, Field(8), quick);
                string stuck = timeline.Standings[3];
                var (finished, didNotFinish, turned) = Homecomings(timeline, stuck, float.PositiveInfinity);
                Assert.That(didNotFinish, Is.EqualTo(new[] { stuck }), "Past their grace they do not finish");
                Assert.That(finished, Is.EqualTo(timeline.Standings.Where(id => id != stuck).ToList()), "and the rest finish in order.");
                Assert.That(turned + CompetitionShowTimeline.WinnerHoldSeconds(quick), Is.LessThanOrEqualTo(timeline.LatestCardAt + 1e-3f));
            }
        }

        [Test]
        public void TheCallsReadTheGraceAndOnlyTheCourseWaits()
        {
            var timeline = CompetitionShowTimeline.For(CompetitionShowFamily.Course, Field(5), false);
            string id = timeline.Standings[2];
            float by = timeline.FinishBy(id), turn = timeline.TurnAt(id);
            Assert.That(timeline.CallFor(id, by + 1f, false, true), Is.EqualTo(CompetitionShowTimeline.CourseCall.Wait), "A second past the deadline a runner on their way is still waited for.");
            Assert.That(timeline.CallFor(id, by + 1f, true, true), Is.EqualTo(CompetitionShowTimeline.CourseCall.Finish), "and finishes on reaching home.");
            Assert.That(timeline.CallFor(id, timeline.LateBy(id) + .01f, false, true), Is.EqualTo(CompetitionShowTimeline.CourseCall.DidNotFinish));
            Assert.That(timeline.CallFor(id, turn + .5f, true, false), Is.EqualTo(CompetitionShowTimeline.CourseCall.Wait), "One who never left finishes no sooner than a second after their turn");
            Assert.That(timeline.CallFor(id, turn + 1f, true, false), Is.EqualTo(CompetitionShowTimeline.CourseCall.Finish));
            Assert.That(timeline.LatestCardAt, Is.EqualTo(timeline.CardAt + CompetitionShowTimeline.LateGraceSeconds).Within(1e-5f));
            Assert.That(timeline.CardWaits(4), Is.True); Assert.That(timeline.CardWaits(5), Is.False);
            Assert.That(float.IsNaN(timeline.TurnAt("nobody")), Is.True);
            foreach (var family in new[] { CompetitionShowFamily.Podium, CompetitionShowFamily.Table })
            {
                var other = CompetitionShowTimeline.For(family, Field(5), false);
                Assert.That(other.CardWaits(0), Is.False, family + " never waits on a runner.");
                Assert.That(other.LatestCardAt, Is.EqualTo(other.CardAt), family + "'s card is where it was.");
            }
        }
    }
}
