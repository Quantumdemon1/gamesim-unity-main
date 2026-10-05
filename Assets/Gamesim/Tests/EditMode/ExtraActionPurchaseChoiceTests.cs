using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class ExtraActionPurchaseChoiceTests
    {
        private static IEnumerable<int> Sizes => Enumerable.Range(3, 14);

        [TestCaseSource(nameof(Sizes))]
        public void OpeningListsEveryActiveHousemateWithoutMutatingOrDrawingARoll(int size)
        {
            var s = EconomyRulesTests.Fresh(size);
            string before = JsonConvert.SerializeObject(s);
            var choice = ExtraActionPurchaseChoice.Open(s);
            Assert.That(choice, Is.Not.Null);
            Assert.That(choice.Targets, Is.EqualTo(s.Active.Where(c => !c.isPlayer).Select(c => c.id)));
            Assert.That(choice.Targets.Count, Is.EqualTo(size - 1));
            Assert.That(choice.IsCurrent(s), Is.True);
            foreach (string target in choice.Targets) Assert.That(choice.CanChoose(s, target), Is.True);
            Assert.That(choice.CanChoose(s, s.playerId), Is.False);
            Assert.That(choice.CanChoose(s, "missing"), Is.False);
            Assert.That(choice.CanChoose(s, null), Is.False);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)choice.Targets).Add("forged"));
            Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(before));
            Assert.That(ExtraActionPurchaseChoice.Open(s), Is.Not.SameAs(choice), "A canceled/reopened view gets fresh identity.");
        }

        [Test]
        public void AllOtherPhasesAndExhaustedOrNonPlayingStatesOfferNoPurchase()
        {
            Assert.That(ExtraActionPurchaseChoice.Open(null), Is.Null);
            var s = EconomyRulesTests.Fresh();
            foreach (EpisodePhase phase in Enum.GetValues(typeof(EpisodePhase)))
            {
                s.phase = phase;
                Assert.That(ExtraActionPurchaseChoice.Open(s) != null,
                    Is.EqualTo(phase == EpisodePhase.Social || phase == EpisodePhase.Campaign), phase.ToString());
            }
            s.phase = EpisodePhase.Social;
            s.boughtActionPoints = WebSocialVocabulary.PurchaseCeiling;
            Assert.That(ExtraActionPurchaseChoice.Open(s), Is.Null);
            s.boughtActionPoints = 0;
            foreach (ContestantStatus status in Enum.GetValues(typeof(ContestantStatus)))
            {
                s.Find(s.playerId).status = status;
                Assert.That(ExtraActionPurchaseChoice.Open(s) != null, Is.EqualTo(status == ContestantStatus.Active));
            }
            s.Find(s.playerId).status = ContestantStatus.Active;
            foreach (var npc in s.Active.Where(c => !c.isPlayer).ToList()) npc.status = ContestantStatus.Jury;
            Assert.That(ExtraActionPurchaseChoice.Open(s), Is.Null);
        }

        [TestCase("session")]
        [TestCase("revision")]
        [TestCase("phase")]
        [TestCase("player")]
        [TestCase("ceiling")]
        [TestCase("status")]
        public void AChoiceCannotTransferItsAuthorityToChangedState(string change)
        {
            var s = EconomyRulesTests.Fresh();
            var choice = ExtraActionPurchaseChoice.Open(s);
            string target = choice.Targets[0];
            switch (change)
            {
                case "session": s.sessionId = "another-season"; break;
                case "revision": s.revision++; break;
                case "phase": s.phase = EpisodePhase.Campaign; break;
                case "player": s.playerId = target; break;
                case "ceiling": s.boughtActionPoints = WebSocialVocabulary.PurchaseCeiling; break;
                case "status": s.Find(s.playerId).status = ContestantStatus.Jury; break;
            }
            Assert.That(choice.IsCurrent(s), Is.False);
            Assert.That(choice.CanChoose(s, target), Is.False);
            Assert.That(choice.IsCurrent(null), Is.False);
        }

        [TestCase(ContestantStatus.Evicted)]
        [TestCase(ContestantStatus.Jury)]
        [TestCase(ContestantStatus.Expelled)]
        public void ATargetWhoIsNoLongerActiveIsNeverReplacedWithSomebodyRandom(ContestantStatus status)
        {
            var s = EconomyRulesTests.Fresh();
            var choice = ExtraActionPurchaseChoice.Open(s);
            string target = choice.Targets[0];
            s.Find(target).status = status;
            Assert.That(choice.CanChoose(s, target), Is.False);
            Assert.That(choice.CanChoose(s, choice.Targets[1]), Is.True);
            var reopened = ExtraActionPurchaseChoice.Open(s);
            Assert.That(reopened.Targets, Does.Not.Contain(target));
        }

        [TestCase(3)]
        [TestCase(8)]
        [TestCase(16)]
        public void EachOfferedTargetUsesTheExistingBoundedEnginePurchaseAndIsTheOnlyChargedPerson(int size)
        {
            var original = EconomyRulesTests.Fresh(size);
            foreach (string target in ExtraActionPurchaseChoice.Open(original).Targets)
            {
                var engine = new EpisodeEngine(original);
                var before = engine.Snapshot;
                var choice = ExtraActionPurchaseChoice.Open(before);
                Assert.That(choice.CanChoose(before, target), Is.True);
                var command = new EpisodeCommand { id = "chosen-bridge", actorId = before.playerId,
                    expectedPhase = before.phase, expectedRevision = before.revision,
                    kind = EpisodeCommandKind.BuyActionPoint, targetId = target, text = WebSocialVocabulary.BurnOne };
                var result = engine.Apply(command);
                Assert.That(result.accepted, Is.True, result.reason);
                var after = engine.Snapshot;
                Assert.That(after.boughtActionPoints, Is.EqualTo(before.boughtActionPoints + 1));
                Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before)));
                foreach (string other in choice.Targets)
                    Assert.That(after.Score(after.playerId, other), other == target
                        ? Is.LessThan(before.Score(before.playerId, other)) : Is.EqualTo(before.Score(before.playerId, other)), other);
                Assert.That(choice.CanChoose(after, target), Is.False);
                string committed = JsonConvert.SerializeObject(after);
                engine.Apply(command);
                Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(committed), "A durable command replay never charges twice.");
            }
        }
    }
}
