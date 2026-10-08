using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Presentation;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Native Edit Mode coverage of the real presentation builder. These prospective reader/source
    /// fixtures are neither a pure-runner substitute nor production rule-one/save acceptance.
    /// </summary>
    public sealed class UnifiedSafetyWeeklyRecapTests
    {
        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void ACanonicalOwnAgreementCarriesThroughTheActualClosedWeekBuilder(bool promise, bool reverse)
        {
            var s = State(); s.unifiedCommitments.Add(Row(s, promise, "standing",
                reverse ? Other(s) : s.playerId, reverse ? s.playerId : Other(s)));
            ReadOnly(s, () =>
            {
                var recap = WeeklyRecap.Build(s, s.week);
                Assert.That(recap.closed, Is.True); Assert.That(recap.finalDecision, Is.False);
                Assert.That(recap.evictedId, Is.EqualTo(Other(s, 4)));
                Assert.That((recap.against, recap.others, recap.remaining), Is.EqualTo((2, 1, 5)));
                Assert.That(Carry(recap), Is.EqualTo(new[] { "You carry one promise or deal into the week." }));
            });
        }

        [TestCase(false)] [TestCase(true)]
        public void CombinedSourceInventoriesRetainEveryActiveOwnAgreement(bool legacy)
        {
            var s = State(); s.unifiedCommitments.Add(Row(s, true, "word"));
            s.unifiedCommitments.Add(Row(s, false, "deal", Other(s), s.playerId));
            if (legacy)
            {
                s.promises.Add(LegacyPromise(s, "information", PromiseKind.Information, s.playerId, Other(s, 1)));
                s.deals.Add(LegacyDeal(s, "final-two", DealKind.FinalTwo, Other(s, 1), s.playerId));
            }
            ReadOnly(s, () => Assert.That(Carry(WeeklyRecap.Build(s, 3)),
                Is.EqualTo(new[] { "You carry " + (legacy ? 4 : 2) + " promises and deals into the week." })));
        }

        [TestCase(DealStatus.Accepted)] [TestCase(DealStatus.Proposed)] [TestCase(DealStatus.Declined)]
        [TestCase(DealStatus.Fulfilled)] [TestCase(DealStatus.Broken)] [TestCase(DealStatus.Expired)]
        public void CanonicalDealsRetainTheOriginalActiveOnlyCarryPredicate(string status)
        {
            var s = State(); var row = Row(s, false, "not-active"); s.unifiedCommitments.Add(row);
            if (status == DealStatus.Broken) Break(s, s.playerId, Other(s));
            else if (status == DealStatus.Fulfilled) Spare(s, s.playerId);
            else row.status = status;
            ReadOnly(s, () =>
            {
                Assert.That(s.unifiedCommitments.Single().status, Is.EqualTo(status));
                Assert.That(Carry(WeeklyRecap.Build(s, 3)), Is.Empty, "Accepted is not widened to Active by a read.");
            });
        }

        [TestCase(false)] [TestCase(true)]
        public void BrokenOrExpiredPromisesDoNotBecomeStandingWord(bool expired)
        {
            var s = State(); var row = Row(s, true, "word"); s.unifiedCommitments.Add(row);
            if (expired) row.status = DealStatus.Expired; else Break(s, s.playerId, Other(s));
            ReadOnly(s, () => Assert.That(Carry(WeeklyRecap.Build(s, 3)), Is.Empty));
        }

        [TestCase(false)] [TestCase(true)]
        public void TheRecapDoesNotCarryAnotherPairsPrivateAgreement(bool promise)
        {
            var s = State(); s.unifiedCommitments.Add(Row(s, promise, "private", Other(s), Other(s, 1)));
            ReadOnly(s, () =>
            {
                var recap = WeeklyRecap.Build(s, 3);
                Assert.That(Carry(recap), Is.Empty); Assert.That(recap.deals, Is.Empty);
                Assert.That(recap.moments, Is.Empty, "A durable private row is not invented player-facing news.");
            });
        }

        [TestCase(1)] [TestCase(3)] [TestCase(5)]
        public void CarryInventoryPreservesAgreementProvenanceRatherThanFutureIncidentDeduplication(int copies)
        {
            var s = State();
            for (int i = 0; i < copies; i++) s.unifiedCommitments.Add(Row(s, false, "agreement-" + i,
                i % 2 == 0 ? Other(s) : s.playerId, i % 2 == 0 ? s.playerId : Other(s)));
            ReadOnly(s, () =>
            {
                Assert.That(UnifiedCommitmentHistory.Breaches(s), Is.Empty);
                Assert.That(Carry(WeeklyRecap.Build(s, 3)), Is.EqualTo(new[] { copies == 1
                    ? "You carry one promise or deal into the week." : "You carry " + copies + " promises and deals into the week." }));
                Assert.That(s.unifiedCommitments, Has.Count.EqualTo(copies));
            });
        }

        [Test]
        public void NominalExpiryDoesNotRewriteTheExistingSourceActivePredicateDuringARead()
        {
            var s = State(); var canonical = Row(s, false, "awaiting-pass"); canonical.createdWeek = 2; canonical.expiresWeek = 2;
            s.unifiedCommitments.Add(canonical);
            var legacy = LegacyDeal(s, "legacy-awaiting-pass", DealKind.InformationSharing, s.playerId, Other(s));
            legacy.week = 2; legacy.expiresWeek = 2; s.deals.Add(legacy);
            ReadOnly(s, () => Assert.That(Carry(WeeklyRecap.Build(s, 3)),
                Is.EqualTo(new[] { "You carry 2 promises and deals into the week." })));
        }

        [TestCase("open")] [TestCase("historical")] [TestCase("final")]
        public void AnOpenHistoricalOrFinalRecapDoesNotInventCurrentCarryWord(string context)
        {
            var s = State(); s.unifiedCommitments.Add(Row(s, true, "standing", s.playerId, Other(s, 3))); int readWeek = 3;
            if (context == "open")
            {
                s.events.Clear(); s.ledger.power.Single().evicteeId = null; s.ledger.power.Single().tally.Clear();
                s.Find(Other(s, 4)).status = ContestantStatus.Active; s.evictionResolved = false;
            }
            else if (context == "historical") readWeek = 2;
            else
            {
                var power = s.ledger.power.Single(); power.hohId = Other(s, 3); power.vetoHolderId = null; power.tally.Clear();
                power.nominees = new List<string> { s.playerId, Other(s, 4) }; s.hohId = power.hohId;
                s.nominees = power.nominees.ToList(); s.vetoHolderId = null;
                for (int i = 0; i < 3; i++) s.Find(Other(s, i)).status = ContestantStatus.Jury;
                s.events.Single().kind = "final-eviction";
                s.events.Single().text = s.Find(power.hohId).name + " takes " + s.Find(s.playerId).name
                    + " to the final two. " + s.Find(Other(s, 4)).name + " joins the jury.";
            }
            ReadOnly(s, () =>
            {
                var recap = WeeklyRecap.Build(s, readWeek); Assert.That(Carry(recap), Is.Empty);
                Assert.That(recap.closed, Is.EqualTo(context != "open"));
                Assert.That(recap.finalDecision, Is.EqualTo(context == "final"));
                if (context == "open" || context == "final") Assert.That(recap.whatsNext, Is.Empty);
                else Assert.That(recap.whatsNext, Is.EqualTo(new[] {
                    "Week 3: " + s.Find(s.ledger.power.Single().hohId).name + " won Head of Household.",
                    s.Find(Other(s, 4)).name + " left the house that week." }));
            });
        }

        [TestCase(ContestantStatus.Evicted)] [TestCase(ContestantStatus.Jury)] [TestCase(ContestantStatus.Expelled)]
        [TestCase(ContestantStatus.Winner)] [TestCase(ContestantStatus.RunnerUp)]
        public void APlayerNoLongerActiveDoesNotCarryWordAsAnActiveHouseguest(ContestantStatus status)
        {
            var s = State(); s.unifiedCommitments.Add(Row(s, false, "standing")); s.Find(s.playerId).status = status;
            ReadOnly(s, () =>
            {
                var recap = WeeklyRecap.Build(s, 3); Assert.That(recap.closed, Is.True);
                Assert.That(recap.whatsNext, Is.Not.Empty); Assert.That(Carry(recap), Is.Empty);
            });
        }

        [Test]
        public void SourceExpiryRemovesCarryWithoutManufacturingAKeptOrBrokenReceipt()
        {
            var s = State(); s.week = 2; s.unifiedCommitments.Add(Row(s, true, "word"));
            s.week = 4; Apply(s, UnifiedCommitments.Expire(s, UnifiedCommitmentExpiry.PromiseWeekTurn));
            var power = s.ledger.power.Single(); power.week = 4; s.events.Single().week = 4;
            foreach (string id in power.nominees) { s.Find(id).nominationWeeks.Remove(3); s.Find(id).nominationWeeks.Add(4); }
            ReadOnly(s, () =>
            {
                Assert.That(s.unifiedCommitments.Single().status, Is.EqualTo(DealStatus.Expired));
                Assert.That(UnifiedCommitmentHistory.Breaches(s), Is.Empty);
                Assert.That(UnifiedCommitmentHistory.Fulfillments(s), Is.Empty);
                Assert.That(Carry(WeeklyRecap.Build(s, 4)), Is.Empty);
                Assert.That(s.events.Count(e => e.kind == "promise-outcome" || e.kind == "deal-outcome"), Is.Zero);
            });
        }

        [TestCase(false)] [TestCase(true)]
        public void AnActualNominationGatewayLeavesNoBrokenAliasInTheStandingCarryCount(bool promise)
        {
            var s = State(false); s.hohId = s.playerId; s.phase = EpisodePhase.Nomination;
            s.ledger.power.Single().hohId = s.playerId;
            for (int i = 0; i < 3; i++) s.unifiedCommitments.Add(Row(s, false, "deal-" + i, s.playerId, Other(s, 3)));
            if (promise) s.unifiedCommitments.Add(Row(s, true, "word", s.playerId, Other(s, 3)));
            Invoke("ResolveUnifiedSafetyNomination", s, "nomination", s.playerId, s.nominees.ToArray());
            Assert.That(s.events.Any(e => e.kind == "promise-outcome" || e.kind == "deal-outcome"), Is.True);
            s.unifiedCommitments.Add(Row(s, true, "still-standing", s.playerId, Other(s)));
            Close(s);
            ReadOnly(s, () =>
            {
                var incident = UnifiedCommitmentHistory.Breaches(s).Single();
                Assert.That((incident.ActorId, incident.WrongedId), Is.EqualTo((s.playerId, Other(s, 3))));
                Assert.That(incident.EvidenceIds, Has.Count.EqualTo(promise ? 4 : 3));
                Assert.That(Carry(WeeklyRecap.Build(s, 3)), Is.EqualTo(new[] { "You carry one promise or deal into the week." }));
                Assert.That(s.promises, Is.Empty); Assert.That(s.deals, Is.Empty);
            });
        }

        [Test]
        public void RulesZeroKeepsTheExactLegacyWhatsNextCopyAndIgnoresAnUnenabledStore()
        {
            var s = State(); s.unifiedCommitmentRulesVersion = 0;
            s.promises.Add(LegacyPromise(s, "safety-promise", PromiseKind.Safety, Other(s), s.playerId));
            s.deals.Add(LegacyDeal(s, "safety-deal", DealKind.SafetyAgreement, s.playerId, Other(s)));
            var accepted = LegacyDeal(s, "not-active", DealKind.SafetyAgreement, s.playerId, Other(s));
            accepted.status = DealStatus.Accepted; s.deals.Add(accepted);
            s.promises.Add(LegacyPromise(s, "private", PromiseKind.Safety, Other(s), Other(s, 1)));
            s.unifiedCommitments.Add(new UnifiedCommitmentState { id = "disabled-malformed-row" });
            ReadOnly(s, () => Assert.That(WeeklyRecap.Build(s, 3).whatsNext, Is.EqualTo(new[] {
                "Week 4 begins with the Head of Household competition.",
                s.Find(Other(s, 2)).name + " held the house this week and cannot compete for it.",
                "5 houseguests remain.", "You carry 2 promises and deals into the week." })));
        }

        [Test]
        public void TheRealBuilderRejectsMalformedEnabledCarryIdentityWithoutChangingState()
        {
            var s = State(); var row = Row(s, false, "bad"); row.id = ""; s.unifiedCommitments.Add(row);
            ReadOnly(s, () => Assert.Throws<ArgumentException>(() => WeeklyRecap.Build(s, 3)));
        }

        [TestCase(0)] [TestCase(-1)]
        public void InvalidWeekGuardReturnsNoCarryWithoutReadingAnEnabledStore(int week)
        {
            var s = State(); s.unifiedCommitments.Add(new UnifiedCommitmentState { id = "not-reached" });
            ReadOnly(s, () =>
            {
                var recap = WeeklyRecap.Build(s, week); Assert.That(recap.week, Is.EqualTo(week));
                Assert.That(recap.whatsNext, Is.Empty); Assert.That(recap.closed, Is.False);
            });
        }

        [Test]
        public void NullStateRetainsTheExistingEmptyNativeBuilderGuard()
        {
            var recap = WeeklyRecap.Build(null, 3);
            Assert.That(recap.week, Is.EqualTo(3)); Assert.That(recap.whatsNext, Is.Empty); Assert.That(recap.closed, Is.False);
        }

        private static EpisodeState State(bool closed = true)
        {
            var s = ContentCatalog.Create(17); s.week = 3; s.phase = EpisodePhase.Eviction;
            s.unifiedCommitmentRulesVersion = 1; s.commitmentRulesStartWeek = 1; s.strategyRulesStartWeek = 1;
            s.story.rulesVersion = 0; s.story.rulesStartWeek = 0; s.agencyRulesStartWeek = 0;
            s.promises.Clear(); s.deals.Clear(); s.alliances.Clear(); s.events.Clear(); s.memories.Clear(); s.houseEvents.Clear();
            s.storylines.Clear(); s.ledger.power.Clear(); s.ledger.alliances.Clear(); s.ledger.ballots.Clear(); s.ledger.claims.Clear();
            foreach (var person in s.contestants) person.traits.Clear();
            foreach (var edge in s.relationships) { edge.score = 0; edge.events.Clear(); }
            s.hohId = Other(s, 2); s.vetoHolderId = Other(s, 1); s.vetoResolved = true;
            s.nominees = new List<string> { Other(s, 3), Other(s, 4) };
            foreach (string id in s.nominees) s.Find(id).nominationWeeks.Add(s.week);
            s.ledger.power.Add(new PowerRow { week = 3, hohId = s.hohId, vetoHolderId = s.vetoHolderId,
                nominees = s.nominees.ToList(), vetoUsed = false });
            if (closed) Close(s); return s;
        }

        private static void Close(EpisodeState s)
        {
            var power = s.ledger.power.Single(); power.evicteeId = Other(s, 4); power.tally = new List<int> { 1, 2 };
            s.Find(Other(s, 4)).status = ContestantStatus.Jury; s.evictionResolved = true; s.phase = EpisodePhase.Eviction;
            s.events.Add(new EpisodeEvent { sequence = s.nextSequence++, week = s.week, phase = EpisodePhase.Eviction, kind = "eviction",
                text = s.Find(Other(s, 4)).name + " is evicted and joins the jury." });
        }

        private static string Other(EpisodeState s, int index = 0) => s.contestants.Where(c => c.id != s.playerId).Skip(index).First().id;

        private static UnifiedCommitmentState Row(EpisodeState s, bool promise, string id, string maker = null, string beneficiary = null) => new UnifiedCommitmentState {
            id = id, kind = UnifiedCommitments.Safety, sourcePolicy = promise ? UnifiedCommitments.PromisePolicy : UnifiedCommitments.DealPolicy,
            origin = promise ? UnifiedCommitments.StoryPromise : UnifiedCommitments.StoryDeal,
            makerId = maker ?? s.playerId, beneficiaryId = beneficiary ?? Other(s), reciprocal = !promise,
            createdWeek = s.week, expiresWeek = s.week + (promise ? 1 : 0), status = DealStatus.Active, trustImpact = DealTrust.Medium,
        };

        private static PromiseState LegacyPromise(EpisodeState s, string id, PromiseKind kind, string maker, string beneficiary) => new PromiseState {
            id = id, kind = kind, fromId = maker, toId = beneficiary, status = PromiseStatus.Active, week = s.week, expiresWeek = s.week + 1,
        };

        private static DealState LegacyDeal(EpisodeState s, string id, string kind, string maker, string beneficiary) => new DealState {
            id = id, type = kind, proposerId = maker, recipientId = beneficiary, status = DealStatus.Active,
            week = s.week, expiresWeek = s.week, trustImpact = DealTrust.Medium,
        };

        private static List<string> Carry(WeeklyRecap.Week recap) => recap.whatsNext.Where(line => line.StartsWith("You carry ", StringComparison.Ordinal)).ToList();

        private static void Break(EpisodeState s, string actor, string wronged)
        {
            s.hohId = actor; s.ledger.power.Single().hohId = actor;
            s.nominees = new List<string> { wronged, Other(s, 4) }; s.ledger.power.Single().nominees = s.nominees.ToList();
            foreach (var person in s.contestants)
            {
                person.nominationWeeks.Remove(s.week);
                if (s.nominees.Contains(person.id)) person.nominationWeeks.Add(s.week);
            }
            Apply(s, UnifiedCommitments.EvaluateNomination(s, "nomination", actor, new[] { wronged }));
        }

        private static void Spare(EpisodeState s, string actor)
        {
            s.hohId = actor; s.ledger.power.Single().hohId = actor;
            Apply(s, UnifiedCommitments.EvaluateFinalVetoSpared(s, actor, new[] { Other(s, 3) }));
        }

        private static void Apply(EpisodeState s, UnifiedCommitmentEvaluation evaluation)
        {
            Assert.That(evaluation.Changes, Is.Not.Empty);
            foreach (var change in evaluation.Changes)
                s.unifiedCommitments[s.unifiedCommitments.FindIndex(row => row.id == change.Record.id)] = change.Record.Clone();
        }

        private static void Invoke(string name, params object[] arguments)
        {
            var method = typeof(EpisodeEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, name);
            try { method.Invoke(null, arguments); } catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        private static void ReadOnly(EpisodeState s, Action assertions)
        {
            string before = Json(s); assertions(); Assert.That(Json(s), Is.EqualTo(before), "The native builder must preserve all public state and RNG.");
        }

        private static string Json(EpisodeState s) => JsonConvert.SerializeObject(s.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToDictionary(field => field.Name, field => field.GetValue(s), StringComparer.Ordinal));
    }
}
