using System;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The week (STRATEGY-LOOP-PLAN.md §4): four windows with a budget each, sitting between the
    /// story's anchors. The campaign is never spent in the free time before it, what is not spent
    /// does not carry, and before the week's rules the pool is what it always was.
    /// </summary>
    public sealed class WeekRulesTests
    {
        private static EpisodeEngine Reach(uint seed, Func<EpisodeState, bool> until, int limit = 600)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            for (int i = 0; i < limit && !until(engine.Snapshot); i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            Assert.That(until(engine.Snapshot), Is.True, "The season never reached the state the test needs.");
            return engine;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string targetId = null) => new EpisodeCommand
        {
            id = "week-" + kind + "-" + s.revision + "-" + Guid.NewGuid().ToString("N"), actorId = s.playerId, kind = kind, targetId = targetId,
            expectedRevision = s.revision, expectedPhase = s.phase,
        };

        [Test]
        public void EveryPhaseSitsInAWindowOrInNone()
        {
            var s = ContentCatalog.Create(31);
            void Expect(EpisodePhase phase, int window) { s.phase = phase; Assert.That(EpisodeEngine.Window(s), Is.EqualTo(window), phase.ToString()); }
            Expect(EpisodePhase.Nomination, Windows.AfterHoH);
            Expect(EpisodePhase.VetoSelection, Windows.AfterNominations);
            Expect(EpisodePhase.Veto, Windows.AfterNominations);
            Expect(EpisodePhase.VetoMeeting, Windows.AfterNominations);
            Expect(EpisodePhase.Campaign, Windows.AfterVeto);
            Expect(EpisodePhase.Social, Windows.AfterEviction);
            foreach (var closed in new[] { EpisodePhase.HoH, EpisodePhase.Eviction, EpisodePhase.FinalHoHPart1, EpisodePhase.Jury, EpisodePhase.Finished })
                Expect(closed, Windows.None);
        }

        [Test]
        public void TheSeatsAreTheWindowsOwnAndScaleWithTheHouseAfterTheEviction()
        {
            var s = ContentCatalog.Create(31);
            Assert.That(EpisodeEngine.WindowSeats(s, Windows.AfterHoH), Is.EqualTo(2));
            Assert.That(EpisodeEngine.WindowSeats(s, Windows.AfterNominations), Is.EqualTo(1));
            Assert.That(EpisodeEngine.WindowSeats(s, Windows.AfterVeto), Is.EqualTo(2));
            Assert.That(EpisodeEngine.WindowSeats(s, Windows.AfterEviction), Is.EqualTo(1), "A six-house: ceil(6/2) + 2 - 5, floored at one.");
            var big = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 3u);
            Assert.That(EpisodeEngine.WindowSeats(big, Windows.AfterEviction), Is.EqualTo(2), "A ten-house: ceil(10/2) + 2 - 5.");
            Assert.That(EpisodeEngine.WindowSeats(s, Windows.None), Is.Zero);
        }

        [Test]
        public void TheCampaignIsNeverSpentInTheFreeTimeBeforeIt()
        {
            var engine = Reach(31, x => x.phase == EpisodePhase.Social && x.week >= 1);
            var s = engine.Snapshot;
            EpisodeEngine.EnableWeek(s);
            s.socialActions = 0; s.outOfPhaseSocialActions = 0;
            engine = new EpisodeEngine(s);
            s = engine.Snapshot;
            Assert.That(EpisodeEngine.Window(s), Is.EqualTo(Windows.AfterEviction));
            int free = EpisodeEngine.SocialActionBudget(s);
            Assert.That(free, Is.EqualTo(Math.Max(1, EpisodeEngine.WindowSeats(s, Windows.AfterEviction) + Storylines.SocialActions(s) - HaveNots.ActionCost(s))));
            // Spend the free time out.
            for (int i = 0; i < free; i++)
            {
                var npc = engine.Snapshot.Active.First(c => !c.isPlayer);
                var result = engine.Apply(Command(engine.Snapshot, EpisodeCommandKind.Talk, npc.id));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            Assert.That(EpisodeEngine.SocialActionsSpent(engine.Snapshot), Is.EqualTo(free));
            var refused = engine.Apply(Command(engine.Snapshot, EpisodeCommandKind.Talk, engine.Snapshot.Active.First(c => !c.isPlayer).id));
            Assert.That(refused.accepted, Is.False, "The window is full.");
            // On to the campaign: its own two seats, untouched by the free time.
            for (int i = 0; i < 80 && engine.Snapshot.phase != EpisodePhase.Campaign; i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var campaign = engine.Snapshot;
            Assert.That(campaign.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(EpisodeEngine.SocialActionsSpent(campaign), Is.Zero, "The campaign starts with its seats.");
            Assert.That(EpisodeEngine.SocialActionBudget(campaign), Is.GreaterThanOrEqualTo(2));
            // The free time after an eviction ends its week: the turn on the way to this campaign reset every window.
            Assert.That(campaign.windowActions[Windows.AfterEviction], Is.Zero, "and the week turned on the way, so every window is fresh.");
        }

        [Test]
        public void TheWindowAfterTheHoHIsOpenToEverybodyWithTwoSeats()
        {
            var engine = Reach(31, x => x.phase == EpisodePhase.Nomination && x.nominees.Count == 0 && x.hohId != x.playerId);
            var s = engine.Snapshot;
            EpisodeEngine.EnableWeek(s); s.strategyRulesStartWeek = 1;
            engine = new EpisodeEngine(s); s = engine.Snapshot;
            Assert.That(EpisodeEngine.Window(s), Is.EqualTo(Windows.AfterHoH));
            Assert.That(EpisodeEngine.SocialActionBudget(s), Is.EqualTo(2));
            var someone = s.Active.First(c => !c.isPlayer && c.id != s.hohId);
            var talk = engine.Apply(Command(s, EpisodeCommandKind.Talk, someone.id));
            Assert.That(talk.accepted, Is.True, "Free roam is open in the window: " + talk.reason);
            Assert.That(EpisodeEngine.SocialActionsSpent(engine.Snapshot), Is.EqualTo(1));
            var listen = engine.Apply(Command(engine.Snapshot, EpisodeCommandKind.Eavesdrop));
            Assert.That(listen.accepted, Is.False, "but listening in is not a word with anybody, and waits for the free time.");
        }

        [Test]
        public void TheWeekTurnsAndEveryWindowIsFreshAndNothingCarries()
        {
            var engine = Reach(31, x => x.phase == EpisodePhase.Campaign && x.nominees.Count == 2);
            var s = engine.Snapshot;
            EpisodeEngine.EnableWeek(s);
            s.windowActions[Windows.AfterHoH] = 2; s.windowActions[Windows.AfterVeto] = 1;
            engine = new EpisodeEngine(s);
            int week = s.week;
            for (int i = 0; i < 80 && engine.Snapshot.week == week; i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var next = engine.Snapshot;
            Assert.That(next.week, Is.EqualTo(week + 1));
            Assert.That(next.windowActions, Is.EqualTo(new[] { 0, 0, 0, 0 }), "The week turned and every window is fresh.");
        }

        [Test]
        public void BeforeTheWeeksRulesThePoolIsWhatItAlwaysWas()
        {
            var s = Reach(31, x => x.phase == EpisodePhase.Campaign && x.nominees.Count == 2).Snapshot;
            Assume.That(EpisodeEngine.WeekRulesOn(s), Is.False);
            Assert.That(EpisodeEngine.SocialActionBudget(s), Is.EqualTo(Math.Max(1, EpisodeEngine.EarnedSocialActionBudget(s) + Math.Max(0, s.boughtActionPoints)
                + Storylines.SocialActions(s) - HaveNots.ActionCost(s))));
            Assert.That(EpisodeEngine.SocialActionsSpent(s), Is.EqualTo(s.socialActions + s.outOfPhaseSocialActions));
            s.weekRulesStartWeek = s.week + 1;
            Assert.That(EpisodeEngine.WeekRulesOn(s), Is.False, "A migrated save keeps its pool through the week it is in.");
        }
    }
}
