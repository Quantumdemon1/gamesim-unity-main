using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Free talk slice 2 (talk rules 2): the friendship stages (spec 3.8, rulings T3 and T5), their stage talks and the Close
    /// friend's favour (T6), the All-Stars' star pool (R7), the season's stream and the seasons it leaves alone (T8). On the
    /// tests' own table (<see cref="FreeTalkS2Fixture"/>). Simulation only.
    /// </summary>
    public sealed class FreeTalkStageTests
    {
        private static List<EpisodeEvent> NewEvents(EpisodeState before, EpisodeState after, string kind) =>
            after.events.Where(e => e.sequence >= before.nextSequence && e.kind == kind).ToList();

        // ------------------------------------------------------------------ the stages, derived

        [TestCase(TalkPools.Steady, 0, 0, 1, FriendStage.Stranger)]
        [TestCase(TalkPools.Steady, 1, 0, 1, FriendStage.Acquainted)]
        [TestCase(TalkPools.Steady, 3, 14, 1, FriendStage.Acquainted)]
        [TestCase(TalkPools.Steady, 3, 15, 1, FriendStage.Friendly)]
        [TestCase(TalkPools.Steady, 6, 35, 1, FriendStage.Friendly)]
        [TestCase(TalkPools.Steady, 5, 35, 2, FriendStage.Friendly)]
        [TestCase(TalkPools.Steady, 6, 35, 2, FriendStage.Friend)]
        [TestCase(TalkPools.Steady, 9, 60, 3, FriendStage.Friend)]
        [TestCase(TalkPools.Steady, 10, 60, 2, FriendStage.Friend)]
        [TestCase(TalkPools.Steady, 10, 59, 3, FriendStage.Friend)]
        [TestCase(TalkPools.Steady, 10, 60, 3, FriendStage.CloseFriend)]
        [TestCase(TalkPools.Guarded, 1, 0, 1, FriendStage.Acquainted)]
        [TestCase(TalkPools.Guarded, 4, 15, 1, FriendStage.Acquainted)]
        [TestCase(TalkPools.Guarded, 5, 15, 1, FriendStage.Friendly)]
        [TestCase(TalkPools.Guarded, 7, 35, 2, FriendStage.Friendly)]
        [TestCase(TalkPools.Guarded, 8, 35, 2, FriendStage.Friend)]
        [TestCase(TalkPools.Guarded, 11, 60, 3, FriendStage.Friend)]
        [TestCase(TalkPools.Guarded, 12, 60, 3, FriendStage.CloseFriend)]
        public void EachStageHoldsAtItsNumbersAndIsNeverSaved(string pool, int rapport, int view, int week, FriendStage expected)
        {
            var s = FreeTalkFixture.Fresh(TalkRules.Chemistry);
            string npc = FreeTalkFixture.Npcs(s)[0].id;
            FreeTalkS2Fixture.ClassAs(s, npc, pool);
            FreeTalkFixture.ContactOf(s, npc).rapport = rapport;
            FreeTalkS2Fixture.Views(s, npc, view, 0);
            s.week = week;
            Assert.That(TalkFriendship.TableStage(s, npc), Is.EqualTo(expected));
            Assert.That(TalkFriendship.Stage(s, npc), Is.EqualTo(expected));
            Assert.That(TalkNotes.BondWith(s, npc).StageLine, Is.EqualTo("Friendship: " + TalkFriendship.Word(expected)));
            Assert.That(FreeTalk.Contact(s, npc).stageReached, Is.Zero, "Derived: nothing is saved.");
        }

        [Test]
        public void TheOathIsRideOrDieAndTalkRulesOneHasNoStage()
        {
            var s = FreeTalkFixture.Fresh(TalkRules.Chemistry);
            string npc = FreeTalkFixture.Npcs(s)[0].id;
            s.loyaltyOaths.Add(new WebOathRecord { playerId = s.playerId, targetId = npc, week = 1 });
            Assert.That(TalkFriendship.Stage(s, npc), Is.EqualTo(FriendStage.RideOrDie));
            Assert.That(TalkFriendship.Word(FriendStage.RideOrDie), Is.EqualTo("Ride or die"));
            Assert.That(TalkFriendship.AtLeast(s, npc, FriendStage.Friend), Is.True);
            FreeTalkFixture.ContactOf(s, npc).rapport = 10;
            FreeTalkS2Fixture.Views(s, npc, 80, 80);
            s.story.talkRules = TalkRules.Talk;
            Assert.That(TalkFriendship.Stage(s, npc), Is.EqualTo(FriendStage.Stranger));
            Assert.That(TalkFriendship.AtLeast(s, npc, FriendStage.Acquainted), Is.False);
            Assert.That(TalkNotes.BondWith(s, npc), Is.Null);
        }

        // ------------------------------------------------------------------ the stage talk (T5)

        [Test]
        public void AStageTalkComesFirstOnceEachLowerFirstWithOneEventRowUnderTheCap()
        {
            using (TalkBank.Use(FreeTalkS2Fixture.Table()))
            {
                var s = FreeTalkFixture.Fresh(TalkRules.Chemistry);
                string npc = FreeTalkFixture.Npcs(s)[0].id;
                FreeTalkS2Fixture.ClassAs(s, npc, TalkPools.Steady);
                FreeTalkFixture.ContactOf(s, npc).rapport = 3;
                FreeTalkS2Fixture.Views(s, npc, 30, 0);
                var engine = new EpisodeEngine(s);
                Assert.That(TalkFriendship.TableStage(engine.Snapshot, npc), Is.EqualTo(FriendStage.Friendly), "Two stages ahead.");

                foreach (var stage in new[] { FriendStage.Acquainted, FriendStage.Friendly })
                {
                    var before = engine.Snapshot;
                    var offer = FreeTalk.Offer(before, npc);
                    Assert.That(offer.Available, Is.True, offer.Reason);
                    Assert.That(offer.Talk.Kind, Is.EqualTo(TalkKind.Stage), "The stage talk is ahead of every other.");
                    Assert.That(offer.Talk.Stage, Is.EqualTo(stage), "The lower stage first.");
                    Assert.That(TalkFriendship.StageTalkDue(before, npc), Is.True);
                    var result = FreeTalk.Result(before, npc, offer.Talk.Id, TalkRules.Warm);
                    Assert.That(result.StageReached, Is.EqualTo(stage));
                    Assert.That(FreeTalkFixture.Talk(engine, npc, TalkRules.Warm).accepted, Is.True);
                    var after = engine.Snapshot;
                    var rows = NewEvents(before, after, TalkFriendship.StageEventKind);
                    Assert.That(rows.Count, Is.EqualTo(1), "One event row.");
                    Assert.That(rows[0].text, Is.EqualTo(TalkFriendship.EventLine(stage, after.Find(npc).name)));
                    Assert.That(after.randomState, Is.EqualTo(before.randomState));
                    Assert.That(FreeTalkFixture.Talk(engine, npc, TalkRules.Warm).accepted, Is.False, "One counted talk a window, the stage talk included.");
                    int key = FreeTalk.WindowKey(engine.Snapshot);
                    FreeTalkFixture.WalkUntil(engine, x => FreeTalk.WindowKey(x) != 0 && FreeTalk.WindowKey(x) != key || x.Find(npc).status != ContestantStatus.Active);
                    Assert.That(engine.Snapshot.Find(npc).status, Is.EqualTo(ContestantStatus.Active), "The control: still in the house.");
                }

                var later = engine.Snapshot;
                var next = FreeTalk.Offer(later, npc);
                Assert.That(next.Available, Is.True, next.Reason);
                Assert.That(next.Talk.Kind, Is.Not.EqualTo(TalkKind.Stage), "Each stage talk once.");
                Assert.That(later.events.Count(e => e.kind == TalkFriendship.StageEventKind), Is.EqualTo(2));
                Assert.That(FreeTalk.Contact(later, npc).stageReached, Is.Zero, "Nothing but slice 1's fields and the lean is written.");
                Assert.That(FreeTalk.Contact(later, npc).talkFlags, Is.Empty);

                // A save after the stage talks validates and loads.
                string json = JsonConvert.SerializeObject(later);
                var loaded = JsonConvert.DeserializeObject<EpisodeState>(json, new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
                Assert.That(EpisodeValidation.TryValidate(loaded, out string reason), Is.True, reason);
                Assert.That(JsonConvert.SerializeObject(new EpisodeEngine(loaded).Snapshot), Is.EqualTo(json));
            }
        }

        [Test]
        public void AnAllStarIsNeverGivenAClassPoolStageTalk()
        {
            var s = FreeTalkFixture.Fresh(TalkRules.Chemistry, CastTemplates.Roster.AllStars);
            var stars = FreeTalkFixture.Npcs(s).Where(FreeTalk.IsAllStar).ToList();
            Assert.That(stars, Is.Not.Empty, "The control: an All-Star cast.");
            foreach (var star in stars)
            {
                FreeTalkFixture.ContactOf(s, star.id).rapport = 3;
                FreeTalkS2Fixture.Views(s, star.id, 30, 0);
            }
            using (TalkBank.Use(FreeTalkFixture.Table().Concat(FreeTalkS2Fixture.Stages(TalkPools.Spark, TalkPools.Steady, TalkPools.Guarded))))
                foreach (var star in stars)
                {
                    Assert.That(TalkFriendship.DueTalk(s, star.id), Is.Null, star.id);
                    Assert.That(FreeTalk.Offer(s, star.id).Talk?.Kind, Is.Not.EqualTo(TalkKind.Stage), star.id);
                }
            using (TalkBank.Use(FreeTalkS2Fixture.Table()))
                foreach (var star in stars)
                {
                    var due = TalkFriendship.DueTalk(s, star.id);
                    Assert.That(due, Is.Not.Null, star.id);
                    Assert.That(due.Pool, Is.EqualTo(TalkPools.Star), star.id);
                }
        }

        // ------------------------------------------------------------------ the favour (T6)

        [TestCase(true)] [TestCase(false)]
        public void TheFavourIsOneActPastTheStopWithNoOathArcOrHistory(bool keep)
        {
            using (TalkBank.Use(FreeTalkS2Fixture.Table()))
            {
                var s = FreeTalkS2Fixture.AtWeek(2).Snapshot;
                string npc = FreeTalkFixture.Npcs(s)[0].id;
                var contact = FreeTalkS2Fixture.Kindred(s, npc);
                contact.rapport = 12;
                string pool = FreeTalk.PoolOf(s.Find(npc));
                foreach (var stage in new[] { FriendStage.Acquainted, FriendStage.Friendly, FriendStage.Friend })
                    contact.spentTalks.Add(TalkRules.Spent(FreeTalkS2Fixture.StageId(pool, stage), TalkRules.Warm));
                FreeTalkS2Fixture.Views(s, npc, 74, 74);
                FreeTalkS2Fixture.AssertValid(s);
                var engine = new EpisodeEngine(s);
                var before = engine.Snapshot;
                Assert.That(TalkFriendship.TableStage(before, npc), Is.EqualTo(FriendStage.CloseFriend), "The control: kindred brings week 3's gate to week 2.");
                var offer = FreeTalk.Offer(before, npc);
                Assert.That(offer.Available, Is.True, offer.Reason);
                Assert.That(offer.Talk.Stage, Is.EqualTo(FriendStage.CloseFriend));

                string tone = keep ? TalkRules.Warm : TalkRules.Straight;
                var result = FreeTalk.Result(before, npc, offer.Talk.Id, tone);
                Assert.That(result.Favour, Is.EqualTo(keep ? TalkFavour.Keep : TalkFavour.Refuse));
                Assert.That(result.Warmth, Is.EqualTo(keep ? 2 : -2), "Past the stop at 25: their view is 74.");
                Assert.That(FreeTalkFixture.Talk(engine, npc, tone).accepted, Is.True);
                var after = engine.Snapshot;

                Assert.That(after.randomState, Is.EqualTo(before.randomState));
                if (keep)
                {
                    Assert.That(after.Score(npc, after.playerId), Is.GreaterThan(74));
                    Assert.That(after.Score(after.playerId, npc), Is.GreaterThanOrEqualTo(75), "The control: the player's view crossed the oath's 75.");
                }
                else
                {
                    Assert.That(after.Score(npc, after.playerId), Is.LessThan(74));
                    Assert.That(after.Score(after.playerId, npc), Is.LessThan(74));
                }
                Assert.That(after.oathOpportunities, Is.EqualTo(before.oathOpportunities), "No oath opportunity.");
                Assert.That(after.shownOathMilestones, Is.EqualTo(before.shownOathMilestones));
                Assert.That(JsonConvert.SerializeObject(after.relationshipArcs), Is.EqualTo(JsonConvert.SerializeObject(before.relationshipArcs)), "No arc step, no history row.");
                Assert.That(after.relationships.Sum(r => r.events.Count), Is.EqualTo(before.relationships.Sum(r => r.events.Count)));
                Assert.That(NewEvents(before, after, TalkFriendship.StageEventKind).Count, Is.EqualTo(1));
                Assert.That(FreeTalk.Offer(after, npc).Refusal, Is.EqualTo(TalkRefusal.TalkedThisWindow));
            }
        }

        // ------------------------------------------------------------------ the stream and the seasons it leaves alone

        [Test]
        public void EveryTalkAtTalkRulesTwoLeavesTheSeasonsStreamByteEqual()
        {
            using (TalkBank.Use(FreeTalkS2Fixture.Table()))
            {
                var s = FreeTalkFixture.Fresh(TalkRules.Chemistry);
                foreach (var npc in FreeTalkFixture.Npcs(s)) FreeTalkFixture.ContactOf(s, npc.id).rapport = 1;
                var engine = new EpisodeEngine(s);
                int stageTalks = TalkEach(engine);
                FreeTalkFixture.WalkUntil(engine, x => FreeTalk.WindowKey(x) > 1 && EpisodeEngine.AllWeekOn(x));
                stageTalks += TalkEach(engine);
                Assert.That(stageTalks, Is.GreaterThan(0), "The control: stage talks were played.");
            }
        }

        private static int TalkEach(EpisodeEngine engine)
        {
            int stageTalks = 0;
            foreach (var npc in FreeTalkFixture.Npcs(engine.Snapshot))
                foreach (var tone in new[] { TalkRules.Tease, TalkRules.Warm })
                {
                    var before = engine.Snapshot;
                    bool stage = FreeTalk.Offer(before, npc.id).Talk?.Kind == TalkKind.Stage;
                    var result = FreeTalkFixture.Talk(engine, npc.id, tone);
                    if (!result.accepted) { Assert.That(result.reason, Is.EqualTo(FreeTalk.TalkedReason)); continue; }
                    if (stage) stageTalks++;
                    var after = engine.Snapshot;
                    Assert.That(after.randomState, Is.EqualTo(before.randomState), npc.id + " " + tone);
                    Assert.That(after.npcSocial.beatsFired, Is.EqualTo(before.npcSocial.beatsFired), "No beat fires on a free talk.");
                }
            return stageTalks;
        }

        [Test]
        public void AtTalkRulesOneTheStageTalksChangeNothingAndTheLeanStaysZero()
        {
            var fresh = FreeTalkFixture.Fresh(TalkRules.Talk);
            foreach (var npc in FreeTalkFixture.Npcs(fresh))
            {
                FreeTalkFixture.ContactOf(fresh, npc.id).rapport = 10;
                FreeTalkS2Fixture.Views(fresh, npc.id, 70, 70);
            }
            string Played(IEnumerable<TalkRow> table)
            {
                using (TalkBank.Use(table))
                {
                    var engine = new EpisodeEngine(fresh);
                    foreach (var npc in FreeTalkFixture.Npcs(engine.Snapshot))
                    {
                        Assert.That(FreeTalk.Offer(engine.Snapshot, npc.id).Talk?.Kind, Is.Not.EqualTo(TalkKind.Stage));
                        Assert.That(TalkNotes.BondWith(engine.Snapshot, npc.id), Is.Null);
                        Assert.That(FreeTalkFixture.Talk(engine, npc.id, TalkRules.Tease).accepted, Is.True);
                    }
                    var after = engine.Snapshot;
                    Assert.That(after.story.talkLean, Is.Zero);
                    Assert.That(after.events.Any(e => e.kind == TalkFriendship.StageEventKind), Is.False);
                    Assert.That(TalkNotes.HouseReadsYou(after), Is.Null);
                    return JsonConvert.SerializeObject(after);
                }
            }
            Assert.That(Played(FreeTalkS2Fixture.Table()), Is.EqualTo(Played(FreeTalkFixture.Table())));
        }
    }
}
