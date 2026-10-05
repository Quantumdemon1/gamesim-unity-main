using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>E1 is an opt-in native economy, not a change to the source-backed legacy pool.</summary>
    public sealed class EconomyRulesTests
    {
        internal static EpisodeState Fresh(int size = 8, bool enable = true)
        {
            var s = ContentCatalog.Create(4242);
            while (s.contestants.Count > size)
            {
                string id = s.contestants.Last().id;
                s.contestants.RemoveAt(s.contestants.Count - 1);
                s.relationships.RemoveAll(r => r.fromId == id || r.toId == id);
                s.memories.RemoveAll(m => m.ownerId == id || m.subjectId == id);
                foreach (var e in s.events) e.audienceIds.Remove(id);
            }
            while (s.contestants.Count < size)
            {
                int i = s.contestants.Count;
                s.contestants.Add(new ContestantState { id = "extra-" + i, name = "Extra " + i, pronouns = "they/them",
                    homeRoom = "Living", motive = "Economy test guest", status = ContestantStatus.Active,
                    traits = new List<string> { "Social" }, stats = new ContestantStats() });
            }
            EpisodeEngine.EnableWeek(s);
            EpisodeEngine.EnableLevers(s);
            if (enable) EpisodeEngine.EnableEconomy(s);
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
            return s;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null, string text = null) =>
            new EpisodeCommand { id = "economy-" + s.revision + "-" + kind, kind = kind, actorId = s.playerId,
                expectedRevision = s.revision, expectedPhase = s.phase, targetId = target, text = text };
        private static void Apply(EpisodeEngine engine, EpisodeCommandKind kind, string target = null, string text = null)
        {
            var r = engine.Apply(Command(engine.Snapshot, kind, target, text));
            Assert.That(r.accepted, Is.True, r.reason);
        }
        private static void Talk(EpisodeEngine engine) => Apply(engine, EpisodeCommandKind.Talk, engine.Snapshot.Active.First(c => !c.isPlayer).id);
        private static void Reach(EpisodeEngine engine, Func<EpisodeState, bool> reached)
        {
            for (int i = 0; i < 300 && !reached(engine.Snapshot); i++)
            {
                var r = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(r.accepted, Is.True, r.reason);
            }
            Assert.That(reached(engine.Snapshot), Is.True, "The actual command flow did not reach its checkpoint.");
        }

        [TestCaseSource(nameof(Sizes))]
        public void EachStoredCastGetsTwoIndependentOpeningSeatsAndTheNewLaterFloor(int size)
        {
            var s = Fresh(size);
            Assert.That(s.Active.Count(), Is.EqualTo(size));
            Assert.That(EpisodeEngine.SocialActionBudget(s), Is.EqualTo(2));
            s.openingBeatsSeen.AddRange(OpeningBeat.InOrder);
            Assert.That(EpisodeEngine.SocialActionBudget(s), Is.EqualTo(2), "Presentation progress never closes move-in night.");
            s.hohId = s.Active.First(c => !c.isPlayer).id;
            s.evictionResolved = true;
            Assert.That(EpisodeEngine.WindowSeats(s, Windows.AfterEviction), Is.EqualTo(Math.Max(2, (size + 1) / 2 - 3)));
            s.week = 2;
            Assert.That(EpisodeEngine.WindowSeats(s, Windows.AfterEviction), Is.EqualTo(Math.Max(2, (size + 1) / 2 - 3)));
            Assert.That(EpisodeEngine.WindowSeats(s, Windows.AfterHoH), Is.EqualTo(2));
            Assert.That(EpisodeEngine.WindowSeats(s, Windows.AfterNominations), Is.EqualTo(1));
            Assert.That(EpisodeEngine.WindowSeats(s, Windows.AfterVeto), Is.EqualTo(2));
        }
        private static IEnumerable<int> Sizes => Enumerable.Range(3, 14);

        [TestCaseSource(nameof(Sizes))]
        public void LegacyWindowsAndAnUnactivatedWeekKeepTheirOldAllowances(int size)
        {
            var legacy = Fresh(size, false);
            Assert.That(EpisodeEngine.SocialActionBudget(legacy), Is.EqualTo(Math.Max(1, (size + 1) / 2 - 3)));
            var delayed = Fresh(size);
            delayed.weekRulesStartWeek = 2;
            legacy.weekRulesStartWeek = 2;
            Assert.That(EpisodeEngine.EconomyRulesOn(delayed), Is.False);
            Assert.That(EpisodeEngine.SocialActionBudget(delayed), Is.EqualTo(EpisodeEngine.SocialActionBudget(legacy)));
        }

        [TestCase(0, 0)] [TestCase(1, 0)] [TestCase(2, 0)] [TestCase(3, 1)] [TestCase(4, 2)]
        public void OpeningClosureKeepsOnlyUnspentExtrasAndNeverResetsThePurchaseCount(int spent, int debit)
        {
            var engine = new EpisodeEngine(Fresh());
            Apply(engine, EpisodeCommandKind.BuyActionPoint, text: WebSocialVocabulary.SpreadAll);
            Apply(engine, EpisodeCommandKind.BuyActionPoint, text: WebSocialVocabulary.SpreadAll);
            for (int i = 0; i < spent; i++) Talk(engine);
            var before = engine.Snapshot;
            Assert.That(EpisodeEngine.SocialActionBudget(before), Is.EqualTo(4));
            Assert.That(WaitingOnYou.ActionsLostOnAdvance(before), Is.EqualTo(Math.Max(0, 2 - spent)));
            var close = Command(before, EpisodeCommandKind.Advance);
            Assert.That(engine.Apply(close).accepted, Is.True);
            var after = engine.Snapshot;
            Assert.That(after.week, Is.EqualTo(1));
            Assert.That(after.moveInExtrasSpent, Is.EqualTo(debit));
            Assert.That(after.boughtActionPoints, Is.EqualTo(2));
            Assert.That(after.windowActions, Is.EqualTo(new[] { 0, 0, 0, 0 }));
            string once = JsonConvert.SerializeObject(after);
            Assert.That(engine.Apply(close).duplicate, Is.True);
            Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(once), "Replaying closure cannot debit twice.");
            Reach(engine, s => s.phase == EpisodePhase.Nomination);
            Assert.That(EpisodeEngine.SocialActionBudget(engine.Snapshot), Is.EqualTo(4 - debit));
            // The next real week clears both the debit and the normal purchase counter.
            Reach(engine, s => s.week == 2);
            Assert.That(engine.Snapshot.moveInExtrasSpent, Is.Zero);
            Assert.That(engine.Snapshot.boughtActionPoints, Is.Zero);
        }

        [Test]
        public void AnExhaustedOpeningCanBuyMoreTimeWithoutRefundingEarlierPurchases()
        {
            var engine = new EpisodeEngine(Fresh());
            Talk(engine); Talk(engine);
            Assert.That(engine.Apply(Command(engine.Snapshot, EpisodeCommandKind.Talk, engine.Snapshot.Active.First(c => !c.isPlayer).id)).accepted, Is.False);
            Apply(engine, EpisodeCommandKind.BuyActionPoint, text: WebSocialVocabulary.SpreadAll);
            Talk(engine);
            Apply(engine, EpisodeCommandKind.BuyActionPoint, text: WebSocialVocabulary.SpreadAll);
            Apply(engine, EpisodeCommandKind.Advance);
            Reach(engine, s => s.phase == EpisodePhase.Nomination);
            Assert.That(engine.Snapshot.moveInExtrasSpent, Is.EqualTo(1));
            Assert.That(engine.Snapshot.boughtActionPoints, Is.EqualTo(2));
            Assert.That(EpisodeEngine.SocialActionBudget(engine.Snapshot), Is.EqualTo(3));
        }

        [Test]
        public void PositiveExtrasShareTheDebitWhileNegativePenaltiesStayInTheirOwnBranch()
        {
            var s = Fresh();
            s.phase = EpisodePhase.Nomination; s.hohId = s.Active.First(c => !c.isPlayer).id;
            s.moveInExtrasSpent = 2;
            s.boughtActionPoints = 1;
            s.activeModifiers.Add(new StoryModifierState { id = "economy-credit", name = "More time", description = "Test reward", weeksLeft = 1, socialBonus = 20 });
            Assert.That(Storylines.SocialActions(s), Is.EqualTo(2));
            Assert.That(EpisodeEngine.SocialActionBudget(s), Is.EqualTo(3));
            s.windowActions[Windows.AfterVeto] = 3; // two base actions, one shared extra
            Assert.That(EpisodeEngine.SocialActionBudget(s), Is.EqualTo(2));
            s.activeModifiers.Clear(); // already-spent rewards may expire; no negative debt is minted
            Assert.That(EpisodeEngine.SocialActionBudget(s), Is.EqualTo(2));
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
            s.boughtActionPoints = 0; s.haveNotRulesStartWeek = 1; s.haveNots.Add(s.playerId);
            Assert.That(EpisodeEngine.WeeklyExtras(s), Is.EqualTo(-1));
            Assert.That(EpisodeEngine.SocialActionBudget(s), Is.EqualTo(1));
            s.phase = EpisodePhase.Campaign;
            Assert.That(EpisodeEngine.SocialActionBudget(s), Is.EqualTo(2));
        }

        [Test]
        public void AccountingReadsDoNotSpendRngOrChangeAnySavedField()
        {
            var s = Fresh();
            string before = JsonConvert.SerializeObject(s);
            for (int i = 0; i < 10; i++) { EpisodeEngine.SocialActionBudget(s); WaitingOnYou.ActionsLostOnAdvance(s); EpisodeEngine.WeeklyExtras(s); }
            Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(before));
            Assert.That(s.Clone().economyRulesVersion, Is.EqualTo(1));
        }

        [Test]
        public void StaleOrUnauthorizedClosureCannotChargeOrMutateTheSeason()
        {
            var s = Fresh(); s.boughtActionPoints = 2; s.windowActions[Windows.AfterEviction] = 4;
            var engine = new EpisodeEngine(s);
            string before = JsonConvert.SerializeObject(engine.Snapshot);
            var stale = Command(s, EpisodeCommandKind.Advance); stale.expectedRevision++;
            Assert.That(engine.Apply(stale).accepted, Is.False);
            var unauthorized = Command(s, EpisodeCommandKind.Advance); unauthorized.actorId = s.Active.First(c => !c.isPlayer).id;
            Assert.That(engine.Apply(unauthorized).accepted, Is.False);
            Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(before));
        }

        [Test]
        public void ActivationCannotSwitchRulesOnAPlayedSeason()
        {
            var s = Fresh(enable: false); s.revision = 1;
            Assert.Throws<ArgumentException>(() => EpisodeEngine.EnableEconomy(s));
            Assert.That(s.economyRulesVersion, Is.Zero);
            s.revision = 0; s.windowActions[Windows.AfterEviction] = 1;
            Assert.Throws<ArgumentException>(() => EpisodeEngine.EnableEconomy(s));
            s.windowActions[Windows.AfterEviction] = 0; s.week = 2;
            Assert.Throws<ArgumentException>(() => EpisodeEngine.EnableEconomy(s));
        }

        [TestCase(-1, 0, 1, false)] [TestCase(2, 0, 1, false)] [TestCase(0, 1, 1, false)]
        [TestCase(1, -1, 1, false)] [TestCase(1, 25, 1, false)] [TestCase(1, 1, 2, false)]
        [TestCase(1, 1, 1, true)]
        public void InvalidEconomyOrDebitStatesAreRejected(int rules, int debit, int week, bool firstNight)
        {
            var s = Fresh(); s.economyRulesVersion = rules; s.moveInExtrasSpent = debit; s.week = week;
            if (!firstNight) { s.phase = EpisodePhase.Nomination; s.hohId = s.Active.First(c => !c.isPlayer).id; }
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.False);
            Assert.That(error, Does.Contain("economy"));
        }
    }
}
