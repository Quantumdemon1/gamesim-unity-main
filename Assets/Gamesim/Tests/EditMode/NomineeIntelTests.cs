using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class NomineeIntelTests
    {
        private static IEnumerable<int> Sizes => Enumerable.Range(4, 13);

        private static EpisodeState Campaign(int size = 8)
        {
            var s = EconomyRulesTests.Fresh(size);
            s.phase = EpisodePhase.Campaign;
            s.hohId = s.playerId;
            s.nominees = s.Active.Where(c => !c.isPlayer).Reverse().Take(2).Select(c => c.id).ToList();
            s.vetoHolderId = s.playerId;
            s.vetoPlayers = s.Active.Take(EpisodeEngine.VetoPlayerCount(size)).Select(c => c.id).ToList();
            s.vetoResolved = true;
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
            return s;
        }

        private static EpisodeCommand Ask(EpisodeState s, string listener, string about = null) => new EpisodeCommand
        {
            id = "nominee-intel-" + s.revision, actorId = s.playerId, expectedRevision = s.revision,
            expectedPhase = s.phase, kind = EpisodeCommandKind.AskForIntel, targetId = listener, secondTargetId = about
        };

        private static EpisodeState Accepted(EpisodeState s, string listener, string about = null)
        {
            var r = new EpisodeEngine(s).Apply(Ask(s, listener, about));
            Assert.That(r.accepted, Is.True, r.reason);
            return r.state;
        }

        [TestCaseSource(nameof(Sizes))]
        public void EveryOfferedNomineeIsHonouredForEveryListenerWithTheSameRollAndCost(int size)
        {
            var before = Campaign(size);
            foreach (var listener in before.Active.Where(c => !c.isPlayer))
            {
                var generic = Accepted(before, listener.id);
                foreach (string nominee in NomineeIntel.Targets(before, listener.id))
                {
                    var after = Accepted(before, listener.id, nominee);
                    var standing = after.ledger.standings.Last();
                    Assert.That((standing.fromId, standing.toId, standing.source), Is.EqualTo((listener.id, nominee, ClaimSource.Told)));
                    Assert.That(standing.score, Is.EqualTo(before.Score(listener.id, nominee)));
                    Assert.That(after.randomState, Is.EqualTo(generic.randomState), "No extra subject roll or reroll.");
                    Assert.That(JsonConvert.SerializeObject(after.relationships), Is.EqualTo(JsonConvert.SerializeObject(generic.relationships)),
                        "The existing two-to-four warmth and reciprocal draw are unchanged.");
                    Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before) + 1));
                    Assert.That(after.windowActions, Is.EqualTo(generic.windowActions));
                    Assert.That(after.ledger.claims, Is.EqualTo(before.ledger.claims), "An opinion is not a claim about their ballot.");
                    var added = after.memories.Skip(before.memories.Count).ToList();
                    Assert.That(added, Has.Count.EqualTo(2));
                    Assert.That(added.All(m => m.isPrivate && (m.ownerId == before.playerId || m.ownerId == listener.id)), Is.True);
                    Assert.That(added.Single(m => m.ownerId == before.playerId).text, Does.Contain(before.Find(nominee).name));
                    var line = after.events.Last();
                    Assert.That(line.kind, Is.EqualTo("information"));
                    Assert.That(line.audienceIds, Is.EquivalentTo(new[] { before.playerId, listener.id }));
                    Assert.That(line.text, Does.Contain("what they think of " + before.Find(nominee).name));
                    Assert.That(EpisodeValidation.TryValidate(after, out string error), Is.True, error);
                }
            }
        }

        [Test]
        public void TargetReaderIsReadOnlyPublicAndOnlyAvailableForTheCurrentBlock()
        {
            Assert.That(NomineeIntel.Targets(null, "nobody"), Is.Empty);
            var s = Campaign();
            string listener = s.Active.First(c => !c.isPlayer && !s.nominees.Contains(c.id)).id;
            string before = JsonConvert.SerializeObject(s);
            Assert.That(NomineeIntel.Targets(s, listener), Is.EqualTo(s.nominees));
            var returned = NomineeIntel.Targets(s, listener);
            returned.Clear();
            Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(before));
            foreach (var relation in s.relationships) relation.score = -relation.score;
            Assert.That(NomineeIntel.Targets(s, listener), Is.EqualTo(s.nominees), "No hidden relationship affects which question is offered.");
            foreach (EpisodePhase phase in Enum.GetValues(typeof(EpisodePhase)))
            {
                s.phase = phase;
                Assert.That(NomineeIntel.Targets(s, listener).Count > 0, Is.EqualTo(phase == EpisodePhase.VetoSelection
                    || phase == EpisodePhase.Veto || phase == EpisodePhase.VetoMeeting || phase == EpisodePhase.Campaign), phase.ToString());
            }
        }

        [Test]
        public void ReaderNeverOffersSelfListenerInactiveUnknownOrLegacyTargets()
        {
            var s = Campaign();
            string listener = s.nominees[0], other = s.nominees[1];
            Assert.That(NomineeIntel.Targets(s, listener), Is.EqualTo(new[] { other }));
            s.nominees.AddRange(new[] { s.playerId, "missing", other });
            Assert.That(NomineeIntel.Targets(s, listener), Is.EqualTo(new[] { other }));
            s.Find(other).status = ContestantStatus.Jury;
            Assert.That(NomineeIntel.Targets(s, listener), Is.Empty);
            s.Find(other).status = ContestantStatus.Active;
            Assert.That(NomineeIntel.Targets(s, s.playerId), Is.Empty);
            Assert.That(NomineeIntel.Targets(s, "missing"), Is.Empty);
            s.Find(listener).status = ContestantStatus.Evicted;
            Assert.That(NomineeIntel.Targets(s, listener), Is.Empty);
            s.Find(listener).status = ContestantStatus.Active;
            s.Find(s.playerId).status = ContestantStatus.Jury;
            Assert.That(NomineeIntel.Targets(s, listener), Is.Empty);
            s.Find(s.playerId).status = ContestantStatus.Active;
            s.economyRulesVersion = 0;
            Assert.That(NomineeIntel.Targets(s, listener), Is.Empty);
            s.economyRulesVersion = 1; s.weekRulesStartWeek = s.week + 1;
            Assert.That(NomineeIntel.Targets(s, listener), Is.Empty);
        }

        [Test]
        public void FinalThreeHasNoRegularBlockQuestion()
        {
            var s = EconomyRulesTests.Fresh(3);
            foreach (var listener in s.Active.Where(c => !c.isPlayer))
                Assert.That(NomineeIntel.Targets(s, listener.id), Is.Empty);
        }

        [TestCase(EpisodePhase.VetoSelection)]
        [TestCase(EpisodePhase.Veto)]
        [TestCase(EpisodePhase.VetoMeeting)]
        public void TheNominationWindowAlsoHonoursTheTargetAndItsSingleAction(EpisodePhase phase)
        {
            var s = Campaign();
            s.phase = phase; s.vetoResolved = false;
            if (phase == EpisodePhase.VetoSelection) { s.vetoPlayers.Clear(); s.vetoHolderId = null; }
            string listener = s.nominees[0], target = s.nominees[1];
            var after = Accepted(s, listener, target);
            Assert.That(after.ledger.standings.Last().toId, Is.EqualTo(target));
            Assert.That(after.windowActions[Windows.AfterNominations], Is.EqualTo(1));
            Assert.That(new EpisodeEngine(after).Apply(Ask(after, listener, target)).accepted, Is.False);
        }

        [TestCase("missing")]
        [TestCase("player")]
        [TestCase("listener")]
        [TestCase("not-nominated")]
        [TestCase(" ")]
        public void InvalidExplicitTargetsAreRejectedWithoutSpendingRollingOrChoosingAnother(string requested)
        {
            var s = Campaign();
            string listener = s.nominees[0];
            string target = requested == "player" ? s.playerId : requested == "listener" ? listener
                : requested == "not-nominated" ? s.Active.First(c => !c.isPlayer && !s.nominees.Contains(c.id)).id : requested;
            var engine = new EpisodeEngine(s);
            string before = JsonConvert.SerializeObject(engine.Snapshot);
            var result = engine.Apply(Ask(s, listener, target));
            Assert.That(result.accepted, Is.False);
            Assert.That(result.reason, Does.Contain("active nominee"));
            Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(before));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void OmittedTargetsKeepExistingBehaviourAndLegacyPayloadsStayIgnored(int delayedWeek)
        {
            var s = Campaign();
            if (delayedWeek == 0) s.economyRulesVersion = 0;
            else s.weekRulesStartWeek = s.week + 1;
            string listener = s.nominees[0];
            string expected = JsonConvert.SerializeObject(Accepted(s, listener));
            foreach (string oldPayload in new[] { s.nominees[1], s.playerId, "missing", " ", "" })
                Assert.That(JsonConvert.SerializeObject(Accepted(s, listener, oldPayload)), Is.EqualTo(expected));
        }

        [Test]
        public void ReplaysAndDuplicateDeliveryCannotRerollOrSpendTwiceAndTheBudgetStillApplies()
        {
            var s = Campaign();
            string listener = s.nominees[0], target = s.nominees[1];
            var engine = new EpisodeEngine(s);
            var command = Ask(s, listener, target);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            string committed = JsonConvert.SerializeObject(engine.Snapshot);
            Assert.That(engine.Apply(command).duplicate, Is.True);
            Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(committed));
            Assert.That(JsonConvert.SerializeObject(Accepted(s, listener, target)), Is.EqualTo(committed));
            var next = engine.Snapshot;
            Assert.That(engine.Apply(Ask(next, listener, target)).accepted, Is.True);
            next = engine.Snapshot;
            string exhausted = JsonConvert.SerializeObject(next);
            Assert.That(engine.Apply(Ask(next, listener, target)).accepted, Is.False);
            Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(exhausted));
        }

        [Test]
        public void StaleAndForeignCommandsDoNotGainInformation()
        {
            var s = Campaign();
            var engine = new EpisodeEngine(s);
            string listener = s.nominees[0], target = s.nominees[1], before = JsonConvert.SerializeObject(s);
            var command = Ask(s, listener, target);
            command.expectedRevision++;
            Assert.That(engine.Apply(command).accepted, Is.False);
            command = Ask(s, listener, target); command.actorId = listener;
            Assert.That(engine.Apply(command).accepted, Is.False);
            Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(before));
        }
    }
}
