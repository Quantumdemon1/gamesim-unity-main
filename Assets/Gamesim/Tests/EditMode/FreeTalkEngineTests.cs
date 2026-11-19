using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The free "Just talk" command (talk rules 1, slice S1): what it costs, its cap, the season's stream, its gains, and the
    /// seasons it must leave alone. On the tests' own table (<see cref="FreeTalkFixture"/>), never the shipped tables.
    /// Simulation only, so the Unity-free subset runs it.
    /// </summary>
    public sealed class FreeTalkEngineTests
    {
        // ------------------------------------------------------------------ the cost and the cap

        [Test]
        public void AFreeTalkSpendsNoActionAndCountsOncePerHousemateAWindow()
        {
            using (TalkBank.Use(FreeTalkFixture.Table()))
            {
                var engine = new EpisodeEngine(FreeTalkFixture.Fresh());
                var before = engine.Snapshot;
                var npcs = FreeTalkFixture.Npcs(before).Select(n => n.id).ToList();
                Assert.That(FreeTalk.WindowKey(before), Is.EqualTo(1), "Move-in night is window key 1.");

                var first = FreeTalkFixture.Talk(engine, npcs[0], TalkRules.Warm);
                Assert.That(first.accepted, Is.True, first.reason);
                var after = engine.Snapshot;
                Assert.That(after.socialActions, Is.EqualTo(before.socialActions));
                Assert.That(after.windowActions, Is.EqualTo(before.windowActions));
                Assert.That(after.moveInExtrasSpent, Is.EqualTo(before.moveInExtrasSpent));
                var contact = FreeTalk.Contact(after, npcs[0]);
                Assert.That(contact.talkWindow, Is.EqualTo(1));
                Assert.That(contact.talks, Is.EqualTo(1));
                Assert.That(contact.spentTalks.Count, Is.EqualTo(1));
                Assert.That(contact.spentTalks[0], Is.EqualTo(TalkRules.Spent(FreeTalk.PoolOf(after.Find(npcs[0])) + ".test.first", TalkRules.Warm)));

                Assert.That(FreeTalk.Offer(after, npcs[0]).Refusal, Is.EqualTo(TalkRefusal.TalkedThisWindow));
                var second = FreeTalkFixture.Talk(engine, npcs[0], TalkRules.Warm);
                Assert.That(second.accepted, Is.False, "A second counted talk in the window.");
                Assert.That(second.reason, Is.EqualTo(FreeTalk.TalkedReason));
                // Farming by naming the talk outright is refused the same way.
                var named = engine.Apply(FreeTalkFixture.Command(engine.Snapshot, npcs[0], contact.spentTalks[0].Split(':')[0], TalkRules.Warm));
                Assert.That(named.accepted, Is.False);

                var other = FreeTalkFixture.Talk(engine, npcs[1], TalkRules.Warm);
                Assert.That(other.accepted, Is.True, "The cap is per housemate: " + other.reason);
            }
        }

        [Test]
        public void MoveInNightAndWeekOnesAfterEvictionWindowHaveTheirOwnKeys()
        {
            var s = FreeTalkFixture.Fresh();
            Assert.That(EpisodeEngine.IsFirstNight(s), Is.True);
            Assert.That(FreeTalk.WindowKey(s), Is.EqualTo(1));
            var afterEviction = s.Clone();
            afterEviction.hohId = FreeTalkFixture.Npcs(s)[0].id;
            Assert.That(EpisodeEngine.IsFirstNight(afterEviction), Is.False);
            Assert.That(FreeTalk.WindowKey(afterEviction), Is.EqualTo(1 * 8 + Windows.AfterEviction + 2));
            var inCompetition = s.Clone();
            inCompetition.phase = EpisodePhase.HohCompetition;
            Assert.That(FreeTalk.WindowKey(inCompetition), Is.Zero);
            Assert.That(FreeTalk.Offer(inCompetition, FreeTalkFixture.Npcs(s)[0].id).Refusal, Is.EqualTo(TalkRefusal.NoWindow));
        }

        [Test]
        public void ANewWindowAllowsOneMoreTalk()
        {
            using (TalkBank.Use(FreeTalkFixture.Table()))
            {
                var engine = new EpisodeEngine(FreeTalkFixture.Fresh());
                string npc = FreeTalkFixture.Npcs(engine.Snapshot)[0].id;
                Assert.That(FreeTalkFixture.Talk(engine, npc, TalkRules.Warm).accepted, Is.True);
                FreeTalkFixture.WalkUntil(engine, s => FreeTalk.WindowKey(s) > 1);
                var next = engine.Snapshot;
                Assert.That(FreeTalk.WindowKey(next), Is.EqualTo(next.week * 8 + EpisodeEngine.Window(next) + 2));
                var again = FreeTalkFixture.Talk(engine, npc, TalkRules.Warm);
                Assert.That(again.accepted, Is.True, again.reason);
                Assert.That(FreeTalk.Contact(engine.Snapshot, npc).talks, Is.EqualTo(2));
            }
        }

        // ------------------------------------------------------------------ the season's stream

        [Test]
        public void EveryFreeTalkLeavesTheSeasonsStreamByteEqual()
        {
            using (TalkBank.Use(FreeTalkFixture.Table()))
            {
                var engine = new EpisodeEngine(FreeTalkFixture.Fresh());
                AssertEachTalkKeepsTheStream(engine);
                // A window under the all-week rules, where every other command makes the house catch up.
                FreeTalkFixture.WalkUntil(engine, s => FreeTalk.WindowKey(s) > 1 && EpisodeEngine.AllWeekOn(s));
                AssertEachTalkKeepsTheStream(engine);
            }
        }

        private static void AssertEachTalkKeepsTheStream(EpisodeEngine engine)
        {
            foreach (var npc in FreeTalkFixture.Npcs(engine.Snapshot))
                foreach (var tone in new[] { TalkRules.Warm, TalkRules.CallOut })
                {
                    var before = engine.Snapshot;
                    var result = FreeTalkFixture.Talk(engine, npc.id, tone);
                    if (!result.accepted) { Assert.That(result.reason, Is.EqualTo(FreeTalk.TalkedReason)); continue; }
                    var after = engine.Snapshot;
                    Assert.That(after.randomState, Is.EqualTo(before.randomState), npc.id + " " + tone);
                    Assert.That(after.npcSocial.beatsFired, Is.EqualTo(before.npcSocial.beatsFired), "No beat fires on a free talk.");
                }
        }

        // ------------------------------------------------------------------ the gains (ruling R5)

        [TestCase(1)] [TestCase(0)] [TestCase(-1)]
        public void WarmthMovesByTheLandingAndRapportByItsTenths(int landing)
        {
            using (TalkBank.Use(FreeTalkFixture.Table()))
            {
                var s = FreeTalkFixture.Fresh();
                foreach (var other in FreeTalkFixture.Npcs(s)) { FreeTalkFixture.SetScore(s, s.playerId, other.id, 0); FreeTalkFixture.SetScore(s, other.id, s.playerId, 0); }
                Assert.That(FreeTalkFixture.TryFind(s, landing, null, out string npc, out string tone), Is.True, "A tone that lands at " + landing);
                var engine = new EpisodeEngine(s);
                var offer = FreeTalk.Offer(engine.Snapshot, npc);
                var expected = FreeTalk.Result(engine.Snapshot, npc, offer.Talk.Id, tone);
                Assert.That(expected.Landing, Is.EqualTo(landing));
                Assert.That(expected.Warmth, Is.EqualTo(landing));
                Assert.That(expected.Outcome, Is.EqualTo(landing > 0 ? FreeTalk.Landed : landing < 0 ? FreeTalk.WrongNote : FreeTalk.NothingMuchMoved));
                Assert.That(FreeTalkFixture.Talk(engine, npc, tone).accepted, Is.True);
                var after = engine.Snapshot;
                double mine = after.Score(after.playerId, npc), theirs = after.Score(npc, after.playerId);
                if (landing == 0) { Assert.That(mine, Is.Zero); Assert.That(theirs, Is.Zero); }
                else
                {
                    Assert.That(Math.Sign(mine), Is.EqualTo(landing));
                    Assert.That(Math.Sign(theirs), Is.EqualTo(landing));
                    Assert.That(Math.Abs(mine), Is.LessThanOrEqualTo(1.1 + 1e-9));
                    Assert.That(Math.Abs(theirs), Is.InRange(0.8 - 1e-9, 1.2 + 1e-9));
                }
                int tenths = FreeTalk.Tenths(landing, false, FreeTalk.ClassOf(after.Find(npc)));
                var contact = FreeTalk.Contact(after, npc);
                Assert.That(contact.rapport * 10 + contact.talkTenths, Is.EqualTo(tenths));
                Assert.That(contact.agreement, Is.EqualTo(landing));
                Assert.That(contact.lastTone, Is.EqualTo(tone));
                Assert.That(contact.priorTone, Is.EqualTo(""));
            }
        }

        [TestCase(1, false, TalkPools.Spark, 10)] [TestCase(1, false, TalkPools.Steady, 8)] [TestCase(1, false, TalkPools.Guarded, 6)]
        [TestCase(0, false, TalkPools.Spark, 6)] [TestCase(0, false, TalkPools.Steady, 5)] [TestCase(0, false, TalkPools.Guarded, 4)]
        [TestCase(-1, false, TalkPools.Spark, 2)] [TestCase(-1, false, TalkPools.Steady, 2)] [TestCase(-1, false, TalkPools.Guarded, 2)]
        [TestCase(1, true, TalkPools.Spark, 6)] [TestCase(1, true, TalkPools.Steady, 5)] [TestCase(1, true, TalkPools.Guarded, 4)]
        public void TenthsFollowTheLandingAndTheListenersClass(int landing, bool repeat, string listener, int tenths) =>
            Assert.That(FreeTalk.Tenths(landing, repeat, listener), Is.EqualTo(tenths));

        [TestCase(true)] [TestCase(false)]
        public void APositiveGainStopsWhenEitherSideIsAt25(bool theirSide)
        {
            using (TalkBank.Use(FreeTalkFixture.Table()))
            {
                var s = FreeTalkFixture.Fresh();
                foreach (var other in FreeTalkFixture.Npcs(s)) { FreeTalkFixture.SetScore(s, s.playerId, other.id, 0); FreeTalkFixture.SetScore(s, other.id, s.playerId, 0); }
                Assert.That(FreeTalkFixture.TryFind(s, 1, null, out string npc, out string tone), Is.True);
                if (theirSide) FreeTalkFixture.SetScore(s, npc, s.playerId, 25); else FreeTalkFixture.SetScore(s, s.playerId, npc, 25);
                var engine = new EpisodeEngine(s);
                Assert.That(FreeTalkFixture.Talk(engine, npc, tone).accepted, Is.True);
                var after = engine.Snapshot;
                Assert.That(after.Score(after.playerId, npc), Is.EqualTo(theirSide ? 0 : 25));
                Assert.That(after.Score(npc, after.playerId), Is.EqualTo(theirSide ? 25 : 0));
                Assert.That(FreeTalk.Contact(after, npc).talkTenths + FreeTalk.Contact(after, npc).rapport * 10, Is.GreaterThan(0), "Rapport still grows.");
            }
        }

        [Test]
        public void TenthsCarryIntoRapport()
        {
            using (TalkBank.Use(FreeTalkFixture.Table()))
            {
                var s = FreeTalkFixture.Fresh();
                string npc = FreeTalkFixture.Npcs(s)[0].id;
                var contact = FreeTalkFixture.ContactOf(s, npc);
                contact.rapport = 2;
                contact.talkTenths = 9;
                var engine = new EpisodeEngine(s);
                var offer = FreeTalk.Offer(engine.Snapshot, npc);
                var result = FreeTalk.Result(engine.Snapshot, npc, offer.Talk.Id, TalkRules.Warm);
                Assert.That(FreeTalkFixture.Talk(engine, npc, TalkRules.Warm).accepted, Is.True);
                var after = FreeTalk.Contact(engine.Snapshot, npc);
                Assert.That(after.rapport, Is.EqualTo(3));
                Assert.That(after.talkTenths, Is.EqualTo((9 + result.Tenths) % 10));
                Assert.That(result.RapportAfter, Is.EqualTo(3));
            }
        }

        [TestCase(1)] [TestCase(-1)]
        public void FreeTalkStepsNoArcWritesNoEventAndOpensNoOath(int landing)
        {
            using (TalkBank.Use(FreeTalkFixture.Table()))
            {
                var s = FreeTalkFixture.Fresh();
                foreach (var other in FreeTalkFixture.Npcs(s)) { FreeTalkFixture.SetScore(s, s.playerId, other.id, 0); FreeTalkFixture.SetScore(s, other.id, s.playerId, 0); }
                Assert.That(FreeTalkFixture.TryFind(s, landing, null, out string npc, out string tone), Is.True);
                var engine = new EpisodeEngine(s);
                var before = engine.Snapshot;
                Assert.That(FreeTalkFixture.Talk(engine, npc, tone).accepted, Is.True);
                var after = engine.Snapshot;
                Assert.That(after.Score(after.playerId, npc), Is.Not.Zero, "The control: the score moved.");
                Assert.That(JsonConvert.SerializeObject(after.relationshipArcs), Is.EqualTo(JsonConvert.SerializeObject(before.relationshipArcs)));
                Assert.That(after.oathOpportunities, Is.EqualTo(before.oathOpportunities));
                Assert.That(after.shownOathMilestones, Is.EqualTo(before.shownOathMilestones));
                Assert.That(after.relationships.Sum(r => r.events.Count), Is.EqualTo(before.relationships.Sum(r => r.events.Count)));
                Assert.That(after.relationships.Sum(r => r.notes.Count), Is.EqualTo(before.relationships.Sum(r => r.notes.Count)));
            }
        }

        // ------------------------------------------------------------------ "the usual" never runs dry (ruling R9)

        [Test]
        public void OnceEverythingIsSpentTheUsualComesRoundAgainWithTheSmallGain()
        {
            using (TalkBank.Use(FreeTalkFixture.Table("first", "u1")))
            {
                var engine = new EpisodeEngine(FreeTalkFixture.Fresh());
                string npc = FreeTalkFixture.Npcs(engine.Snapshot)[0].id;
                for (int talk = 0; talk < 3; talk++)
                {
                    if (talk > 0)
                    {
                        int key = FreeTalk.WindowKey(engine.Snapshot);
                        FreeTalkFixture.WalkUntil(engine, s => FreeTalk.WindowKey(s) != 0 && FreeTalk.WindowKey(s) != key
                            || s.Find(npc).status != ContestantStatus.Active);
                        Assert.That(engine.Snapshot.Find(npc).status, Is.EqualTo(ContestantStatus.Active), "The control: still in the house.");
                    }
                    var offer = FreeTalk.Offer(engine.Snapshot, npc);
                    Assert.That(offer.Available, Is.True, offer.Reason);
                    Assert.That(offer.Repeat, Is.EqualTo(talk == 2));
                    if (talk == 2)
                    {
                        Assert.That(offer.Talk.Usual, Is.True);
                        var result = FreeTalk.Result(engine.Snapshot, npc, offer.Talk.Id, TalkRules.Warm);
                        Assert.That(result.Warmth, Is.LessThanOrEqualTo(0));
                        Assert.That(result.Tenths, Is.LessThanOrEqualTo(FreeTalk.Tenths(0, false, FreeTalk.ClassOf(engine.Snapshot.Find(npc)))));
                    }
                    Assert.That(FreeTalkFixture.Talk(engine, npc, TalkRules.Warm).accepted, Is.True);
                }
                var contact = FreeTalk.Contact(engine.Snapshot, npc);
                Assert.That(contact.talks, Is.EqualTo(3));
                Assert.That(contact.spentTalks.Count, Is.EqualTo(2), "A repeat is never spent twice.");
            }
        }

        // ------------------------------------------------------------------ seasons it leaves alone

        [Test]
        public void AtTalkRulesZeroTheCommandIsRefusedAndTheSeasonDoesNotMove()
        {
            using (TalkBank.Use(FreeTalkFixture.Table()))
            {
                var fresh = FreeTalkFixture.Fresh(TalkRules.Off);
                var tried = new EpisodeEngine(fresh);
                var control = new EpisodeEngine(fresh);
                for (int step = 0; step < 120; step++)
                {
                    var s = tried.Snapshot;
                    string npc = FreeTalkFixture.Npcs(s).FirstOrDefault()?.id;
                    Assert.That(FreeTalk.Offer(s, npc).Refusal, Is.EqualTo(TalkRefusal.Off));
                    Assert.That(TalkNotes.WhatYouKnow(s, npc), Is.Empty);
                    var refused = tried.Apply(FreeTalkFixture.Command(s, npc, TalkPools.Steady + ".test.first", TalkRules.Warm));
                    Assert.That(refused.accepted, Is.False);
                    Assert.That(refused.reason, Is.EqualTo(FreeTalk.OffReason));
                    Assert.That(JsonConvert.SerializeObject(tried.Snapshot), Is.EqualTo(JsonConvert.SerializeObject(s)));
                    if (s.phase == EpisodePhase.Finished) break;
                    Assert.That(tried.Apply(EpisodeEngineTests.NextCommand(s)).accepted, Is.True);
                    Assert.That(control.Apply(EpisodeEngineTests.NextCommand(control.Snapshot)).accepted, Is.True);
                }
                Assert.That(JsonConvert.SerializeObject(tried.Snapshot), Is.EqualTo(JsonConvert.SerializeObject(control.Snapshot)));
            }
        }

        [Test]
        public void ASaveTakenAfterATalkValidatesAndLoadsWithTheCapHeld()
        {
            using (TalkBank.Use(FreeTalkFixture.Table()))
            {
                var engine = new EpisodeEngine(FreeTalkFixture.Fresh());
                string npc = FreeTalkFixture.Npcs(engine.Snapshot)[0].id;
                Assert.That(FreeTalkFixture.Talk(engine, npc, TalkRules.Warm).accepted, Is.True);
                string json = JsonConvert.SerializeObject(engine.Snapshot);
                var loaded = JsonConvert.DeserializeObject<EpisodeState>(json,
                    new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
                Assert.That(EpisodeValidation.TryValidate(loaded, out string reason), Is.True, reason);
                var reloaded = new EpisodeEngine(loaded);
                Assert.That(JsonConvert.SerializeObject(reloaded.Snapshot), Is.EqualTo(json));
                Assert.That(FreeTalk.Offer(reloaded.Snapshot, npc).Refusal, Is.EqualTo(TalkRefusal.TalkedThisWindow));
                Assert.That(FreeTalkFixture.Talk(reloaded, npc, TalkRules.Warm).accepted, Is.False);
            }
        }

        [Test]
        public void JustTalkIsAppendedAfterChooseDialogue()
        {
            Assert.That((int)EpisodeCommandKind.JustTalk, Is.EqualTo((int)EpisodeCommandKind.ChooseDialogue + 1));
            Assert.That(FreeTalk.TryParse(FreeTalk.CommandText("steady.l1.bunk", TalkRules.Warm), out string id, out string tone), Is.True);
            Assert.That(id, Is.EqualTo("steady.l1.bunk"));
            Assert.That(tone, Is.EqualTo(TalkRules.Warm));
            Assert.That(FreeTalk.TryParse("no-bar", out _, out _), Is.False);
        }
    }
}
