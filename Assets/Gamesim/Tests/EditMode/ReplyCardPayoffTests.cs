using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class ReplyCardPayoffTests
    {
        private static string Json(object value) => JsonConvert.SerializeObject(value);
        private static IEnumerable<TestCaseData> Answers => ReplyCards.All.SelectMany(kind =>
            ReplyCards.Replies(kind).Select(reply => new TestCaseData(kind, reply.Key)));
        private static IEnumerable<TestCaseData> InformationCasts => Enumerable.Range(3, 14).SelectMany(size =>
            new[] { ReplyCards.Confrontation, ReplyCards.Gossip, ReplyCards.Plea }
                .Where(kind => kind != ReplyCards.Plea || size >= 4).Select(kind => new TestCaseData(size, kind)));
        private static EpisodeState House(string kind, int size = 8, uint seed = 4242)
        {
            var s = EconomyRulesTests.Fresh(size); s.randomState = seed;
            s.strategyRulesStartWeek = 1; EpisodeEngine.EnableCommitments(s);
            var npcs = s.Active.Where(c => !c.isPlayer).ToArray();
            string from = npcs[0].id, about = kind == ReplyCards.Gossip ? npcs[1].id : null;
            if (kind == ReplyCards.Plea)
            {
                s.phase = EpisodePhase.Campaign; s.hohId = npcs[0].id;
                s.nominees = new List<string> { npcs[1].id, npcs[2].id };
                s.vetoHolderId = s.playerId; s.vetoResolved = true;
                s.vetoPlayers = new[] { s.hohId, npcs[1].id, npcs[2].id, s.playerId }.Concat(s.Active.Select(c => c.id))
                    .Distinct().Take(EpisodeEngine.VetoPlayerCount(size)).ToList();
                from = npcs[1].id; about = npcs[2].id;
            }
            s.replyCards.Add(new ReplyCardState { id = "reply-payoff-fixture", week = s.week, kind = kind, fromId = from, aboutId = about });
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
            return s;
        }
        private static EpisodeCommand Command(EpisodeState s, string key) => new EpisodeCommand
        {
            id = "answer-" + s.revision, actorId = s.playerId, expectedRevision = s.revision, expectedPhase = s.phase,
            kind = EpisodeCommandKind.ReplyToHouseguest, targetId = s.replyCards[0].id, text = key,
        };
        private static EpisodeState Answer(EpisodeState s, string key)
        {
            var result = new EpisodeEngine(s).Apply(Command(s, key));
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(EpisodeValidation.TryValidate(result.state, out string error), Is.True, error);
            return result.state;
        }
        private static void Score(EpisodeState s, string from, string to, double score) => RelationshipLedger.Move(s, from, to, score - s.Score(from, to));
        private static string InformationKey(string kind) => kind == ReplyCards.Confrontation ? "deflect" : kind == ReplyCards.Gossip ? "confront" : "refuse";

        [TestCaseSource(nameof(Answers))]
        public void EachAnswerKeepsItsBaseRecordAndIsFreeEvenWhenTheWindowIsExhausted(string kind, string key)
        {
            var s = House(kind); var card = s.replyCards[0];
            int window = EpisodeEngine.Window(s); s.windowActions[window] = EpisodeEngine.SocialActionBudget(s);
            if (s.phase == EpisodePhase.Social) s.socialActions = s.windowActions[window]; else s.outOfPhaseSocialActions = s.windowActions[window];
            string before = Json(s); var after = Answer(s, key);
            Assert.That(after.replyCards, Is.Empty);
            Assert.That(after.ledger.replies.Single().toThem, Is.EqualTo(ReplyCards.Find(kind, key).ToThem));
            Assert.That(after.ledger.replies.Single().promised, Is.EqualTo(key == "promise"));
            Assert.That(after.socialActions, Is.EqualTo(s.socialActions));
            Assert.That(after.outOfPhaseSocialActions, Is.EqualTo(s.outOfPhaseSocialActions));
            Assert.That(Json(after.windowActions), Is.EqualTo(Json(s.windowActions)));
            Assert.That(Json(s), Is.EqualTo(before));
            Assert.That(after.memories.Any(m => m.ownerId == card.fromId && m.text == ReplyCards.Memory(kind, key, s.week)), Is.True);
            if (key == "promise") Assert.That(after.promises.Single().targetId, Is.EqualTo(card.aboutId));
            else Assert.That(after.promises, Is.Empty);
            if (kind != ReplyCards.Confrontation || key != "escalate") Assert.That(after.deals, Is.Empty);
        }

        [TestCaseSource(nameof(InformationCasts))]
        public void InformationRepliesRecordOnlyTheSpeakersOwnAssessmentAndKeepSourceConsequences(int size, string kind)
        {
            var s = House(kind, size); var card = s.replyCards[0]; string key = InformationKey(kind);
            var others = s.Active.Where(c => !c.isPlayer && c.id != card.fromId).OrderBy(c => c.id, StringComparer.Ordinal).ToArray();
            foreach (var other in others) Score(s, card.fromId, other.id, -40);
            string subject = kind == ReplyCards.Confrontation ? others[0].id : card.aboutId;
            var after = Answer(s, key); var row = after.ledger.standings.Single();
            Assert.That((row.fromId, row.toId, row.source, row.score), Is.EqualTo((card.fromId, subject, ClaimSource.Told, -40d)));
            var information = after.events.Single(e => e.kind == "information");
            Assert.That(information.audienceIds, Is.EqualTo(new[] { s.playerId, card.fromId }));
            Assert.That(information.text, Does.Contain(s.Find(subject).name).And.Contain("not a vote promise"));
            Assert.That(after.memories.Any(m => m.ownerId == s.playerId && m.subjectId == card.fromId && m.isPrivate && m.text == information.text), Is.True);
            Assert.That(after.ledger.claims, Is.Empty); Assert.That(after.ledger.ballots, Is.Empty);
            Assert.That(EpisodeEngine.AskedThisWeek(after, card.fromId), Is.False);
            Assert.That(EpisodeEngine.ReadThisWeek(after, card.fromId), Is.False);
            var old = s.Clone(); old.economyRulesVersion = 0; var historical = Answer(old, key);
            Assert.That(after.randomState, Is.EqualTo(historical.randomState));
            Assert.That(Json(after.relationships), Is.EqualTo(Json(historical.relationships)));
            Assert.That(Json(after.memories.Where(m => m.ownerId != s.playerId)), Is.EqualTo(Json(historical.memories.Where(m => m.ownerId != s.playerId))));
            Assert.That(KnownOdds.Band(after, card.fromId, subject), Is.EqualTo(KnownOdds.Cold));
        }

        [TestCaseSource(nameof(Answers))]
        public void DescriptionsAreDetachedAndIndependentOfHiddenPreferencesAndDraws(string kind, string key)
        {
            var s = House(kind); var card = s.replyCards[0]; var reply = ReplyCards.Find(kind, key);
            string before = Json(s), original = Json(reply);
            string description = ReplyCardPayoffs.Description(s, card, reply);
            Assert.That(description, Is.Not.Null.And.Not.Empty); Assert.That(description.Length, Is.LessThanOrEqualTo(90));
            Assert.That(Json(s), Is.EqualTo(before)); Assert.That(Json(reply), Is.EqualTo(original));
            foreach (var edge in s.relationships.Where(e => e.fromId != s.playerId)) edge.score = -edge.score + .123;
            s.randomState = 98371;
            Assert.That(ReplyCardPayoffs.Description(s, card, reply), Is.EqualTo(description));
            s.economyRulesVersion = 0;
            Assert.That(ReplyCardPayoffs.Description(s, card, reply), Is.EqualTo(reply.Description));
        }

        [TestCaseSource(nameof(Answers))]
        public void DisabledAndDelayedRulesKeepTheCompleteLegacyOutcome(string kind, string key)
        {
            var old = House(kind); old.economyRulesVersion = 0;
            var delayed = old.Clone(); delayed.economyRulesVersion = 1; delayed.weekRulesStartWeek = 2;
            old.weekRulesStartWeek = 2;
            var a = Answer(old, key); var b = Answer(delayed, key); b.economyRulesVersion = 0;
            Assert.That(Json(b), Is.EqualTo(Json(a)));
            Assert.That(a.ledger.standings, Is.Empty);
            Assert.That(a.deals, Is.Empty);
            Assert.That(ReplyCardPayoffs.Description(delayed, delayed.replyCards[0], ReplyCards.Find(kind, key)), Is.EqualTo(ReplyCards.Find(kind, key).Description));
        }

        [TestCaseSource(nameof(Answers))]
        public void ReplayingAStoredCardIsExactAndNoStaleAnswerCanEarnTwice(string kind, string key)
        {
            var s = House(kind); var command = Command(s, key); var engine = new EpisodeEngine(s);
            var after = engine.Apply(command); Assert.That(after.accepted, Is.True, after.reason);
            var restored = JsonConvert.DeserializeObject<EpisodeState>(Json(s), new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
            Assert.That(Json(new EpisodeEngine(restored).Apply(command).state), Is.EqualTo(Json(after.state)));
            Assert.That(engine.Apply(command).duplicate, Is.True);
            command.id += "-stale"; Assert.That(engine.Apply(command).accepted, Is.False);
            command.expectedRevision = after.state.revision; Assert.That(engine.Apply(command).accepted, Is.False, "The card itself was consumed.");
            Assert.That(Json(engine.Snapshot), Is.EqualTo(Json(after.state)));
        }

        [Test]
        public void TheSafetyProposalUsesTheExistingNpcConsentAndDealConsequencesRatherThanInventingAnAgreement()
        {
            int accepted = 0, refused = 0;
            for (uint seed = 1; seed <= 40; seed++)
            {
                var s = House(ReplyCards.Confrontation, seed: seed); string npc = s.replyCards[0].fromId;
                var actual = Answer(s, "escalate");
                var old = s.Clone(); old.economyRulesVersion = 0; var baseReply = Answer(old, "escalate");
                var expected = new EpisodeEngine(baseReply).Apply(new EpisodeCommand { id = "ordinary-proposal", actorId = s.playerId,
                    expectedRevision = baseReply.revision, expectedPhase = baseReply.phase, kind = EpisodeCommandKind.ProposeDeal,
                    targetId = npc, text = DealKind.SafetyAgreement });
                Assert.That(expected.accepted, Is.True, expected.reason);
                Assert.That(Json(actual.deals), Is.EqualTo(Json(expected.state.deals)));
                Assert.That(Json(actual.relationships), Is.EqualTo(Json(expected.state.relationships)));
                Assert.That(actual.randomState, Is.EqualTo(expected.state.randomState));
                Assert.That(actual.socialActions, Is.Zero, "The incoming reply, unlike a separately initiated proposal, is free.");
                if (actual.deals.Count > 0)
                {
                    accepted++; var deal = actual.deals.Single();
                    Assert.That((deal.type, deal.status, deal.proposerId, deal.recipientId, deal.expiresWeek),
                        Is.EqualTo((DealKind.SafetyAgreement, DealStatus.Active, s.playerId, npc, s.week)));
                }
                else refused++;
            }
            Assert.That(accepted, Is.GreaterThan(0)); Assert.That(refused, Is.GreaterThan(0));
            TestContext.WriteLine("Forty actual reply/proposal comparisons: " + accepted + " agreements, " + refused + " refusals. NPC consent is not guaranteed.");
        }

        [TestCase("full")] [TestCase("already")] [TestCase("not-this-week")]
        public void AnUnavailableSafetyProposalDoesNotBlockTheReplyOrInventAPayoff(string boundary)
        {
            var s = House(ReplyCards.Confrontation); string npc = s.replyCards[0].fromId;
            if (boundary == "not-this-week") s.dealRulesStartWeek = 2;
            else for (int i = 0; i < (boundary == "full" ? PlayerDeals.PlayerDealCeiling : 1); i++)
            {
                var d = PlayerDeals.Draft(s, npc, DealKind.SafetyAgreement, null, "deal-fixture-" + i);
                if (boundary == "full") d.status = DealStatus.Declined;
                s.deals.Add(d);
            }
            Assert.That(ReplyCardPayoffs.Description(s, s.replyCards[0], ReplyCards.Find(ReplyCards.Confrontation, "escalate")), Does.Contain("No new safety"));
            var after = Answer(s, "escalate"); var old = s.Clone(); old.economyRulesVersion = 0;
            var historical = Answer(old, "escalate");
            Assert.That(Json(after.deals), Is.EqualTo(Json(s.deals)));
            Assert.That(Json(after.relationships), Is.EqualTo(Json(historical.relationships)));
            Assert.That(after.randomState, Is.EqualTo(historical.randomState));
            Assert.That(after.events.Last().text, Does.Contain("No new safety proposal"));
        }

        [TestCase(ReplyCards.Gossip)] [TestCase(ReplyCards.Plea)]
        public void MissingSelfOrEvictedAssessmentSubjectsNeverBecomeHiddenSubstitutes(string kind)
        {
            foreach (string boundary in new[] { "none", "self", "player", "evicted" })
            {
                var s = House(kind); var card = s.replyCards[0];
                if (boundary == "none") card.aboutId = null;
                if (boundary == "self") card.aboutId = card.fromId;
                if (boundary == "player") card.aboutId = s.playerId;
                if (boundary == "evicted")
                {
                    // Keep a valid campaign's nominees; an unrelated evicted identity is not its other nominee.
                    var gone = s.Active.Last(c => !c.isPlayer && c.id != card.fromId && !s.nominees.Contains(c.id) && c.id != s.hohId);
                    gone.status = ContestantStatus.Evicted; card.aboutId = gone.id;
                }
                Assert.That(ReplyCardPayoffs.AssessmentAvailable(s, card), Is.False);
                var after = Answer(s, InformationKey(kind));
                Assert.That(after.ledger.standings, Is.Empty);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void PromisedSupportIsActuallyKeptOrBrokenByTheLaterPlayerBallot(bool keep)
        {
            var s = House(ReplyCards.Plea, 4); string nominee = s.replyCards[0].fromId, other = s.replyCards[0].aboutId;
            var after = Answer(s, "promise"); string promiseId = after.promises.Single().id;
            var engine = new EpisodeEngine(after); bool voted = false;
            for (int i = 0; i < 35 && engine.Snapshot.promises.Single(p => p.id == promiseId).status == PromiseStatus.Active; i++)
            {
                var before = engine.Snapshot; var command = EpisodeEngineTests.NextCommand(before);
                if (command.kind == EpisodeCommandKind.CastVote) { command.targetId = keep ? other : nominee; voted = true; }
                var result = engine.Apply(command); Assert.That(result.accepted, Is.True, result.reason);
            }
            Assert.That(voted, Is.True);
            Assert.That(engine.Snapshot.promises.Single(p => p.id == promiseId).status, Is.EqualTo(keep ? PromiseStatus.Fulfilled : PromiseStatus.Broken));
        }

        [Test]
        public void AnAcceptedReplyTruceIsARealObligationThatNominatingThePartnerBreaks()
        {
            EpisodeState agreed = null;
            for (uint seed = 1; seed <= 40 && agreed == null; seed++)
            {
                var result = Answer(House(ReplyCards.Confrontation, seed: seed), "escalate");
                if (result.deals.Count > 0) agreed = result;
            }
            Assert.That(agreed, Is.Not.Null);
            var deal = agreed.deals.Single(); string id = deal.id, partner = deal.recipientId;
            agreed.phase = EpisodePhase.Nomination; agreed.hohId = agreed.playerId;
            var engine = new EpisodeEngine(agreed);
            var broken = engine.Apply(new EpisodeCommand { id = "break-reply-truce", actorId = agreed.playerId,
                expectedRevision = agreed.revision, expectedPhase = agreed.phase, kind = EpisodeCommandKind.Nominate,
                targetId = partner, secondTargetId = agreed.Active.First(c => !c.isPlayer && c.id != partner).id });
            Assert.That(broken.accepted, Is.True, broken.reason);
            var record = broken.state.deals.Single(d => d.id == id);
            Assert.That(record.status, Is.EqualTo(DealStatus.Broken));
            Assert.That(record.brokenById, Is.EqualTo(agreed.playerId));
            Assert.That(record.settledWeek, Is.EqualTo(agreed.week));
        }

        [Test]
        public void InvalidAuthorityOrAnAnswerFromAnotherCardCannotAcquireAnAssessmentOrDeal()
        {
            foreach (string damage in new[] { "actor", "revision", "phase", "kind", "card", "text" })
            {
                var s = House(ReplyCards.Confrontation); var command = Command(s, "escalate");
                if (damage == "actor") command.actorId = s.replyCards[0].fromId;
                if (damage == "revision") command.expectedRevision++;
                if (damage == "phase") command.expectedPhase = EpisodePhase.Campaign;
                if (damage == "kind") command.text = "gossip-back";
                if (damage == "card") command.targetId = "unknown";
                if (damage == "text") command.text = new string('x', 2001);
                var engine = new EpisodeEngine(s); string before = Json(engine.Snapshot);
                Assert.That(engine.Apply(command).accepted, Is.False, damage);
                Assert.That(Json(engine.Snapshot), Is.EqualTo(before), damage);
            }
        }

        [Test]
        public void AssessmentRowsAndPrivateMemoriesUseTheirExistingCaps()
        {
            var s = House(ReplyCards.Gossip); var card = s.replyCards[0];
            for (int i = 0; i < SeasonLedger.MostRows; i++) s.ledger.standings.Add(new StandingRow
                { week = s.week, fromId = card.fromId, toId = card.aboutId, source = ClaimSource.Told, score = 0 });
            s.memories.RemoveAll(m => m.ownerId == s.playerId);
            for (int i = 0; i < 30; i++) s.memories.Add(new MemoryState { week = s.week, ownerId = s.playerId, subjectId = card.fromId, text = "old " + i, isPrivate = true });
            var after = Answer(s, "confront");
            Assert.That(after.ledger.standings.Count, Is.EqualTo(SeasonLedger.MostRows));
            Assert.That(after.ledger.dropped, Is.EqualTo(s.ledger.dropped + 1));
            Assert.That(after.memories.Count(m => m.ownerId == s.playerId), Is.EqualTo(30));
            Assert.That(after.memories.Any(m => m.ownerId == s.playerId && m.text == "old 0"), Is.False);
        }

        [Test]
        public void ActualFirstOpportunityReplyPayoffsHaveNoStrictlyDominatedAnswer()
        {
            // A witnessed trade-off, not invented point weights or a claim about optimal play.
            // Each component is read from actual committed outcomes: warmth, new information,
            // a negotiated truce, opposition reach, and freedom from new obligations.
            foreach (string kind in ReplyCards.All)
            {
                var vectors = new Dictionary<string, double[]>();
                foreach (var reply in ReplyCards.Replies(kind))
                {
                    var v = new double[5]; vectors.Add(reply.Key, v);
                    for (uint seed = 1; seed <= 80; seed++)
                    {
                        var s = House(kind, seed: seed); var card = s.replyCards[0];
                        var after = Answer(s, reply.Key);
                        v[0] += after.Score(s.playerId, card.fromId) - s.Score(s.playerId, card.fromId);
                        v[1] += after.ledger.standings.Count - s.ledger.standings.Count;
                        v[2] += after.deals.Count(d => d.type == DealKind.SafetyAgreement && d.status == DealStatus.Active);
                        if (kind == ReplyCards.Gossip) v[3] += s.Score(card.aboutId, card.fromId) - after.Score(card.aboutId, card.fromId);
                        v[4] -= after.promises.Count(p => p.status == PromiseStatus.Active) + after.deals.Count(d => d.status == DealStatus.Active);
                    }
                    for (int i = 0; i < v.Length; i++) v[i] /= 80;
                    TestContext.WriteLine(kind + "/" + reply.Key + ": " + string.Join(", ", v.Select(n => n.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))));
                }
                foreach (var a in vectors) foreach (var b in vectors.Where(b => b.Key != a.Key))
                {
                    bool atLeast = Enumerable.Range(0, 5).All(i => b.Value[i] >= a.Value[i] - .000001);
                    bool better = Enumerable.Range(0, 5).Any(i => b.Value[i] > a.Value[i] + .000001);
                    Assert.That(atLeast && better, Is.False, kind + "/" + b.Key + " dominates " + a.Key);
                }
            }
            TestContext.WriteLine("720 committed reply outcomes. Nominal first opportunities only: excludes already-known intel, unavailable deals, future outcomes and human utility. The whole social-verb/balance gate remains open.");
        }
    }
}
