using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>Durable canonical outcome readers; no production activation or persistence acceptance.</summary>
    public sealed class UnifiedSafetySummaryTests
    {
        [TestCase(1, false)] [TestCase(1, true)]
        [TestCase(3, false)] [TestCase(3, true)]
        [TestCase(5, false)] [TestCase(5, true)]
        public void FulfillmentNormalizesThePairAndRetainsEveryAgreement(int copies, bool reversed)
        {
            var s = State(); string other = Other(s);
            for (int i = 0; i < copies; i++)
            {
                var row = Deal(s, "kept-" + i, reversed && i % 2 == 0 ? other : s.playerId,
                    reversed && i % 2 == 0 ? s.playerId : other);
                Fulfill(s, row);
            }
            string before = Json(s); var receipt = UnifiedCommitmentHistory.Fulfillments(s).Single();
            Assert.That(receipt.SettledWeek, Is.EqualTo(3));
            Assert.That(receipt.FirstId, Is.EqualTo(new[] { s.playerId, other }.OrderBy(id => id, StringComparer.Ordinal).First()));
            Assert.That(receipt.SecondId, Is.EqualTo(new[] { s.playerId, other }.OrderBy(id => id, StringComparer.Ordinal).Last()));
            Assert.That(receipt.EvidenceIds, Is.EqualTo(Enumerable.Range(0, copies).Select(i => "kept-" + i)));
            Assert.That(receipt.EffectOwnerId, Is.EqualTo("kept-0"));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(DealTrust.Medium)] [TestCase(DealTrust.Critical)]
        public void FulfillmentOwnerUsesTheStrongestPositiveSourceBeforeIdentity(string trust)
        {
            var s = State(); var weak = Deal(s, "a-weak"); weak.trustImpact = DealTrust.Low; Fulfill(s, weak);
            var strong = Deal(s, "z-strong"); strong.trustImpact = trust; Fulfill(s, strong);
            string before = Json(s);
            Assert.That(UnifiedCommitmentHistory.Fulfillments(s).Single().EffectOwnerId, Is.EqualTo("z-strong"));
            s.unifiedCommitments.Reverse();
            Assert.That(UnifiedCommitmentHistory.Fulfillments(s).Single().EffectOwnerId, Is.EqualTo("z-strong"));
            s.unifiedCommitments.Reverse(); Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void EqualPositiveSourcesUseOrdinalIdentityNotStoredOrder()
        {
            var s = State(); Fulfill(s, Deal(s, "z-last")); Fulfill(s, Deal(s, "a-first"));
            Assert.That(UnifiedCommitmentHistory.Fulfillments(s).Single().EffectOwnerId, Is.EqualTo("a-first"));
        }

        [Test]
        public void FulfillmentSeparatesDifferentWeeksAndDifferentPairs()
        {
            var s = State(); s.week = 2; Fulfill(s, Deal(s, "old")); s.week = 3;
            Fulfill(s, Deal(s, "new")); Fulfill(s, Deal(s, "other-pair", s.playerId, Other(s, 1)));
            var receipts = UnifiedCommitmentHistory.Fulfillments(s);
            Assert.That(receipts.Count, Is.EqualTo(3));
            Assert.That(receipts.Select(r => r.SettledWeek), Is.EqualTo(new[] { 2, 3, 3 }));
            Assert.That(receipts.SelectMany(r => r.EvidenceIds), Is.EquivalentTo(new[] { "old", "new", "other-pair" }));
        }

        [Test]
        public void FulfillmentCollectionsCannotRewriteTheAuthoritativeRecords()
        {
            var s = State(); Fulfill(s, Deal(s, "kept")); string before = Json(s);
            var receipts = UnifiedCommitmentHistory.Fulfillments(s);
            Assert.Throws<NotSupportedException>(() => ((IList<UnifiedCommitmentFulfillment>)receipts).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<string>)receipts[0].EvidenceIds).Clear());
            UnifiedCommitmentHistory.Records(s)[0].status = DealStatus.Active;
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void LegacyFulfilledDealsDoNotInventCanonicalFulfillmentGroups()
        {
            var s = State(); s.unifiedCommitmentRulesVersion = 0;
            s.deals.Add(new DealState { id = "old", type = DealKind.SafetyAgreement, proposerId = s.playerId,
                recipientId = Other(s), status = DealStatus.Fulfilled, week = 1 });
            string before = Json(s);
            Assert.That(UnifiedCommitmentHistory.Fulfillments(s), Is.Empty);
            Assert.That(CommitmentReferences.ReceiptWeek(s, "old", 1), Is.EqualTo(1));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void AUnilateralPromiseCannotPretendToBeASparedPair()
        {
            var s = State(); var row = Promise(s, "invalid"); row.status = DealStatus.Fulfilled;
            row.settledWeek = 3; s.unifiedCommitments.Add(row); string before = Json(s);
            Assert.Throws<ArgumentException>(() => UnifiedCommitmentHistory.Fulfillments(s));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(1, false)] [TestCase(1, true)] [TestCase(4, false)] [TestCase(4, true)]
        public void WeeklyBreachIsOneActualIncidentWithItsActorAndAllEvidence(int deals, bool playerBreaks)
        {
            var s = State(); string partner = Other(s), actor = playerBreaks ? s.playerId : partner;
            string wronged = playerBreaks ? partner : s.playerId; s.hohId = actor;
            s.unifiedCommitments.Add(Promise(s, "promise", actor, wronged));
            for (int i = 0; i < deals; i++) s.unifiedCommitments.Add(Deal(s, "deal-" + i, actor, wronged));
            Break(s, "nomination", wronged); string before = Json(s);
            var line = YourWeek.Build(s, 3).word.Single();
            Assert.That((line.kind, line.verdict, line.aboutId, line.byId),
                Is.EqualTo((YourWeek.Kinds.Promise, YourWeek.Verdicts.Broken, partner, actor)));
            Assert.That(line.evidenceIds.Count, Is.EqualTo(deals + 1));
            Assert.That(line.effectKey, Is.EqualTo(UnifiedCommitmentHistory.Breaches(s).Single().EffectKey));
            Assert.That(line.text, Does.Contain(playerBreaks ? "You broke" : "broke the promise"));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(1)] [TestCase(3)] [TestCase(5)]
        public void WeeklyKeptPairIsOneLineAndNeverInventsAKeepingActor(int copies)
        {
            var s = State();
            for (int i = 0; i < copies; i++) s.unifiedCommitments.Add(Deal(s, "deal-" + i));
            Invoke("ResolveUnifiedSafetySpared", s, s.playerId, new[] { Other(s, 1) });
            string before = Json(s); var line = YourWeek.Build(s, 3).word.Single();
            Assert.That((line.kind, line.verdict, line.aboutId), Is.EqualTo((YourWeek.Kinds.Deal, YourWeek.Verdicts.Kept, Other(s))));
            Assert.That(line.byId, Is.Null); Assert.That(line.effectKey, Is.Null);
            Assert.That(line.evidenceIds.Count, Is.EqualTo(copies));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void WeeklyCanonicalOutcomesOutlastLogsMemoriesAndRelationshipOutcomeRows(bool kept)
        {
            var s = State(); s.week = 2;
            s.unifiedCommitments.Add(kept ? Deal(s, "word") : Promise(s, "word"));
            if (kept) Invoke("ResolveUnifiedSafetySpared", s, s.playerId, new[] { Other(s, 1) });
            else Invoke("ResolveUnifiedSafetyNomination", s, "nomination", s.playerId, new[] { Other(s) });
            s.week = 3; s.events.Clear(); s.memories.Clear(); s.ledger.power.Clear();
            foreach (var relationship in s.relationships) relationship.events.Clear();
            string before = Json(s);
            Assert.That(YourWeek.Build(s, 2).word.Single().verdict, Is.EqualTo(kept ? YourWeek.Verdicts.Kept : YourWeek.Verdicts.Broken));
            Assert.That(YourWeek.Build(s, 3).word, Is.Empty);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void ActualSourceOutcomeIsNotRepeatedBesideTheCanonicalSummary(bool promise)
        {
            var s = State(); s.unifiedCommitments.Add(Deal(s, "deal"));
            if (promise) s.unifiedCommitments.Add(Promise(s, "promise"));
            Invoke("ResolveUnifiedSafetyNomination", s, "nomination", s.playerId, new[] { Other(s) });
            Assert.That(s.events.Any(e => e.kind == "promise-outcome")
                || s.relationships.Any(r => r.events.Any(e => e.type == YourWeek.DealBroken)), Is.True,
                "The real source routine must have emitted an outcome before suppression is tested.");
            string before = Json(s); var lines = YourWeek.Build(s, 3).word;
            Assert.That(lines.Count, Is.EqualTo(1));
            Assert.That(lines[0].evidenceIds, Is.EquivalentTo(promise ? new[] { "deal", "promise" } : new[] { "deal" }));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void WeeklySummaryDoesNotRevealAnNpcToNpcAgreement()
        {
            var s = State(); string actor = Other(s), wronged = Other(s, 1); s.hohId = actor;
            s.unifiedCommitments.Add(Promise(s, "private-pair", actor, wronged)); Break(s, "nomination", wronged);
            string before = Json(s);
            Assert.That(YourWeek.Build(s, 3).word, Is.Empty);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void WeeklySummaryKeepsDistinctNominationAndReplacementIncidents()
        {
            var s = State(); s.unifiedCommitments.Add(Promise(s, "first")); Break(s, "nomination", Other(s));
            s.unifiedCommitments.Add(Promise(s, "second")); Break(s, "replacement", Other(s));
            var lines = YourWeek.Build(s, 3).word;
            Assert.That(lines.Count, Is.EqualTo(2));
            Assert.That(lines.Select(l => l.effectKey).Distinct().Count(), Is.EqualTo(2));
            Assert.That(lines.SelectMany(l => l.evidenceIds), Is.EquivalentTo(new[] { "first", "second" }));
        }

        [TestCase(DealStatus.Active, 2)] [TestCase(DealStatus.Expired, 2)]
        [TestCase(DealStatus.Fulfilled, 3)] [TestCase(DealStatus.Broken, 3)]
        public void ReceiptDateIsActualSettlementWhileProjectionKeepsItsCreationWeek(string status, int expected)
        {
            var s = State(); var row = Deal(s, "receipt"); row.origin = UnifiedCommitments.Lobby;
            row.createdWeek = 2; row.expiresWeek = 3; s.unifiedCommitments.Add(row);
            if (status == DealStatus.Broken) Break(s, "nomination", Other(s));
            else { row.status = status; if (status == DealStatus.Fulfilled) row.settledWeek = 3; }
            string before = Json(s);
            Assert.That(CommitmentReferences.FindDeal(s, "receipt").week, Is.EqualTo(2));
            Assert.That(CommitmentReferences.ReceiptWeek(s, "receipt", 2), Is.EqualTo(expected));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(1, false)] [TestCase(1, true)] [TestCase(4, false)] [TestCase(4, true)]
        public void GameSenseScoresOneBreachAndOnlyChargesItsActualBreaker(int copies, bool playerBreaks)
        {
            var s = State(); string actor = playerBreaks ? s.playerId : Other(s), wronged = playerBreaks ? Other(s) : s.playerId;
            s.hohId = actor;
            for (int i = 0; i < copies; i++)
            {
                s.unifiedCommitments.Add(Deal(s, "deal-" + i, actor, wronged));
                Chance(s, "deal-" + i, OpportunityOutcome.Lost);
            }
            Break(s, "nomination", wronged); string before = Json(s);
            var notes = DealNotes(s);
            Assert.That(notes.Count, Is.EqualTo(1)); Assert.That(notes.Single().rowId, Is.EqualTo("deal-0"));
            Assert.That(notes.Single().points, Is.EqualTo(playerBreaks ? -5 : 0));
            Assert.That(notes.Single().known, Is.True);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(1)] [TestCase(4)]
        public void GameSenseScoresOneRewardForOverlappingFulfilledAliases(int copies)
        {
            var s = State();
            for (int i = 0; i < copies; i++) { Fulfill(s, Deal(s, "kept-" + i)); Chance(s, "kept-" + i, OpportunityOutcome.Won); }
            string before = Json(s); var notes = DealNotes(s);
            Assert.That(notes.Count, Is.EqualTo(1)); Assert.That(notes.Single().points, Is.EqualTo(4));
            Assert.That(notes.Single().rowId, Is.EqualTo("kept-0"));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void GameSensePrefersTheEffectOwnerAndUsesStableFallbackWhenItHasNoChance(bool includeOwner)
        {
            var s = State(); s.unifiedCommitments.Add(Deal(s, "b-alias")); s.unifiedCommitments.Add(Deal(s, "a-alias"));
            var owner = Deal(s, "z-owner"); owner.trustImpact = DealTrust.Critical; s.unifiedCommitments.Add(owner);
            Chance(s, "b-alias", OpportunityOutcome.Lost); Chance(s, "a-alias", OpportunityOutcome.Lost);
            if (includeOwner) Chance(s, "z-owner", OpportunityOutcome.Lost);
            Break(s, "nomination", Other(s));
            Assert.That(UnifiedCommitmentHistory.Breaches(s).Single().EffectOwnerId, Is.EqualTo("z-owner"));
            Assert.That(DealNotes(s).Single().rowId, Is.EqualTo(includeOwner ? "z-owner" : "a-alias"));
            Assert.That(DealNotes(s).Single().points, Is.EqualTo(-5));
        }

        [Test]
        public void LegacyGameSenseRetainsSeparateDealOpportunityRewards()
        {
            var s = State(); s.unifiedCommitmentRulesVersion = 0;
            Chance(s, "first", OpportunityOutcome.Won); Chance(s, "second", OpportunityOutcome.Won);
            string before = Json(s); var notes = DealNotes(s);
            Assert.That(notes.Count, Is.EqualTo(2)); Assert.That(notes.Sum(n => n.points), Is.EqualTo(8));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void CreatingDistinctDealsStillHasItsOwnHistoryBeforeAnySettlement()
        {
            var s = State();
            foreach (string id in new[] { "first", "second" }) { s.unifiedCommitments.Add(Deal(s, id)); Chance(s, id, OpportunityOutcome.NotApplicable); }
            Assert.That(DealNotes(s).Count, Is.EqualTo(2)); Assert.That(DealNotes(s).Sum(n => n.points), Is.EqualTo(2));
        }

        [TestCase(false)] [TestCase(true)]
        public void AudibleAliasFactsHaveOneUnionedAudienceOrTheActuallyPublicAudience(bool publiclyKnown)
        {
            var s = State(); s.unifiedCommitments.Add(Deal(s, "one")); s.unifiedCommitments.Add(Deal(s, "two"));
            Break(s, "nomination", Other(s));
            Fact(s, "fact-one", "one", s.playerId, Other(s), FactVisibility.Whispered, s.playerId, Other(s));
            Fact(s, "fact-two", "two", s.playerId, Other(s), publiclyKnown ? FactVisibility.Public : FactVisibility.Whispered, Other(s, 1));
            string before = Json(s); var grouped = YourWord.Breaches(s).Single();
            Assert.That(grouped.knowers, Is.EquivalentTo(new[] { s.playerId, Other(s), Other(s, 1) }));
            Assert.That(YourWord.Hearers(s).Count, Is.EqualTo(publiclyKnown ? s.contestants.Count - 1 : 2));
            int hearings = publiclyKnown ? Math.Min(YourWord.WholeHouse, s.contestants.Count - 1) : 2;
            Assert.That(YourWord.Hearings(s), Is.EqualTo(hearings));
            Assert.That(YourWord.Cost(s), Is.EqualTo(hearings * YourWord.PerHearing));
            Assert.That(YourWord.Lines(s).Count, Is.EqualTo(1)); Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void PrivateAliasKnowledgeIsNotAddedToAnAudibleIncident()
        {
            var s = State(); s.unifiedCommitments.Add(Deal(s, "audible")); s.unifiedCommitments.Add(Deal(s, "private")); Break(s, "nomination", Other(s));
            Fact(s, "public-evidence", "audible", s.playerId, Other(s), FactVisibility.Whispered, Other(s));
            Fact(s, "private-evidence", "private", s.playerId, Other(s), FactVisibility.Private, Other(s, 1), Other(s, 2));
            Assert.That(YourWord.Breaches(s).Single().knowers, Is.EqualTo(new[] { Other(s) }));
            Assert.That(YourWord.Hearers(s), Is.EqualTo(new[] { Other(s) })); Assert.That(YourWord.Hearings(s), Is.EqualTo(1));
        }

        [Test]
        public void GroupedFactViewsAreDetachedIncludingTheirAudienceList()
        {
            var s = State(); s.unifiedCommitments.Add(Deal(s, "word")); Break(s, "nomination", Other(s));
            Fact(s, "fact", "word", s.playerId, Other(s), FactVisibility.Whispered, Other(s));
            string before = Json(s); var grouped = YourWord.Breaches(s).Single();
            grouped.refId = "reader-cannot-rewrite"; grouped.visibility = FactVisibility.Public; grouped.knowers.Clear();
            Assert.That(Json(s), Is.EqualTo(before)); Assert.That(YourWord.Hearings(s), Is.EqualTo(1));
        }

        [Test]
        public void AnotherActorsBrokenWordDoesNotBecomeThePlayersReputation()
        {
            var s = State(); s.hohId = Other(s); s.unifiedCommitments.Add(Deal(s, "their-word", Other(s), s.playerId));
            Break(s, "nomination", s.playerId);
            Fact(s, "fact", "their-word", Other(s), s.playerId, FactVisibility.Public, s.playerId);
            Assert.That(YourWord.Breaches(s), Is.Empty); Assert.That(YourWord.Cost(s), Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void AudibleCanonicalReferenceUsesItsActualPromiseOrDealWording(bool promise)
        {
            var s = State(); s.unifiedCommitments.Add(promise ? Promise(s, "word") : Deal(s, "word")); Break(s, "nomination", Other(s));
            var fact = Fact(s, "fact", "word", s.playerId, Other(s), FactVisibility.Whispered, Other(s));
            Assert.That(YourWord.HeardLine(s, fact, Other(s, 1)), Does.Contain(promise ? "promise of safety" : "safety deal"));
            Assert.That(YourWord.Lines(s).Single(), Does.Contain(promise ? "promise of safety" : "safety deal"));
        }

        [Test]
        public void DistinctActualIncidentsRetainDistinctHearingCosts()
        {
            var s = State(); s.unifiedCommitments.Add(Deal(s, "one")); Break(s, "nomination", Other(s));
            s.unifiedCommitments.Add(Deal(s, "two")); Break(s, "replacement", Other(s));
            Fact(s, "fact-one", "one", s.playerId, Other(s), FactVisibility.Whispered, Other(s));
            Fact(s, "fact-two", "two", s.playerId, Other(s), FactVisibility.Whispered, Other(s));
            Assert.That(YourWord.Breaches(s).Count, Is.EqualTo(2)); Assert.That(YourWord.Hearings(s), Is.EqualTo(2));
        }

        [TestCase(1)] [TestCase(3)] [TestCase(5)]
        public void FinalArgumentOffersOnlyTheCanonicalKeptOwnerButEveryAliasStillResolves(int copies)
        {
            var s = State();
            for (int i = 0; i < copies; i++) Fulfill(s, Deal(s, "kept-" + i));
            string before = Json(s); var moments = FinalArgument.Moments(s).Where(m => m.reference.StartsWith("deal:", StringComparison.Ordinal)).ToList();
            Assert.That(moments.Count, Is.EqualTo(1)); Assert.That(moments.Single().reference, Is.EqualTo("deal:kept-0"));
            Assert.That(moments.Single().theme, Is.EqualTo(FinalArgument.Emotional));
            for (int i = 0; i < copies; i++)
            {
                Assert.That(FinalArgument.Resolves(s, "deal:kept-" + i), Is.True);
                Assert.That(FinalArgument.SubjectOf(s, "deal:kept-" + i), Is.EqualTo(Other(s)));
            }
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(DealStatus.Active)] [TestCase(DealStatus.Expired)]
        [TestCase(DealStatus.Fulfilled)] [TestCase(DealStatus.Broken)]
        public void StableDealReferenceResolutionDoesNotDependOnMomentEligibility(string status)
        {
            var s = State(); var row = Deal(s, "stable"); s.unifiedCommitments.Add(row);
            if (status == DealStatus.Broken) Break(s, "nomination", Other(s));
            else { row.status = status; if (status == DealStatus.Fulfilled) row.settledWeek = 3; }
            string before = Json(s);
            Assert.That(FinalArgument.Resolves(s, "deal:stable"), Is.True);
            Assert.That(FinalArgument.SubjectOf(s, "deal:stable"), Is.EqualTo(Other(s)));
            Assert.That(FinalArgument.Moments(s).Count(m => m.reference == "deal:stable"), Is.EqualTo(status == DealStatus.Fulfilled ? 1 : 0));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void FinalArgumentCannotResolveAnNpcPairAsThePlayersMoment()
        {
            var s = State(); Fulfill(s, Deal(s, "other-pair", Other(s), Other(s, 1)));
            Assert.That(FinalArgument.Resolves(s, "deal:other-pair"), Is.False);
            Assert.That(FinalArgument.SubjectOf(s, "deal:other-pair"), Is.Null);
            Assert.That(FinalArgument.Moments(s).Any(m => m.reference == "deal:other-pair"), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void AccountabilitySelectsTheActualStrongestSourceOwnerNotAnArbitraryAlias(bool promise)
        {
            var s = State(); s.unifiedCommitments.Add(Deal(s, "a-weak"));
            var strong = Deal(s, "z-strong"); strong.trustImpact = DealTrust.Critical; s.unifiedCommitments.Add(strong);
            if (promise) s.unifiedCommitments.Add(Promise(s, "promise"));
            Break(s, "nomination", Other(s)); string before = Json(s);
            var receipt = FinaleQuestions.Receipts(s, Other(s)).Single(r => r.category == FinaleQuestions.Accountability);
            Assert.That(receipt.id, Is.EqualTo(promise ? "promise" : "z-strong"));
            Assert.That(receipt.kind, Is.EqualTo(promise ? FinaleQuestions.PromiseReceipt : FinaleQuestions.DealReceipt));
            Assert.That(receipt.week, Is.EqualTo(3)); Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void AccountabilityChoosesLatestSettlementRatherThanLatestCreation()
        {
            var s = State(); s.week = 2; s.unifiedCommitments.Add(Promise(s, "created-later")); Break(s, "nomination", Other(s));
            s.week = 3; var deal = Deal(s, "settled-later", Other(s), s.playerId); deal.origin = UnifiedCommitments.NpcOffer;
            deal.createdWeek = 1; deal.expiresWeek = 3;
            s.unifiedCommitments.Add(deal); Break(s, "replacement", Other(s));
            var receipt = FinaleQuestions.Receipts(s, Other(s)).Single(r => r.category == FinaleQuestions.Accountability);
            Assert.That((receipt.kind, receipt.id, receipt.week), Is.EqualTo((FinaleQuestions.DealReceipt, "settled-later", 3)));
        }

        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void LaterSettlementScoresTheActualOutcomeWeekNotItsOldOfferWeek(bool acceptedOffer, bool kept)
        {
            var s = State(); var row = acceptedOffer ? Deal(s, "old-offer", Other(s), s.playerId) : Deal(s, "old-offer");
            row.origin = acceptedOffer ? UnifiedCommitments.NpcOffer : UnifiedCommitments.Lobby;
            row.createdWeek = acceptedOffer ? 1 : 2; row.expiresWeek = 3; s.unifiedCommitments.Add(row);
            if (kept) { row.status = DealStatus.Fulfilled; row.settledWeek = 3; }
            else Break(s, "nomination", Other(s));
            EpisodeEngine.ReconcileOpportunities(s); string before = Json(s);
            var opportunity = s.ledger.opportunities.Single(o => o.id == row.id);
            Assert.That(opportunity.week, Is.EqualTo(row.createdWeek), "Opportunity provenance still names when the offer was made.");
            Assert.That(DealNotes(s).Single().week, Is.EqualTo(3));
            Assert.That(DealNotes(s).Single().text, Does.StartWith("Week 3:"));
            Assert.That(YourWeek.Build(s, row.createdWeek).sense.rows.Any(n => n.rowId == row.id), Is.False);
            Assert.That(YourWeek.Build(s, row.createdWeek).sense.strategy, Is.EqualTo(50));
            Assert.That(YourWeek.Build(s, 3).sense.rows.Single(n => n.rowId == row.id).points, Is.EqualTo(kept ? 4 : -5));
            Assert.That(YourWeek.Build(s, 3).sense.strategy, Is.EqualTo(kept ? 54 : 45));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(DealStatus.Active, OpportunityResponse.Taken, OpportunityOutcome.NotApplicable)]
        [TestCase(DealStatus.Proposed, OpportunityResponse.Ignored, OpportunityOutcome.NotApplicable)]
        [TestCase(DealStatus.Declined, OpportunityResponse.Declined, OpportunityOutcome.NotApplicable)]
        [TestCase(DealStatus.Expired, OpportunityResponse.Expired, OpportunityOutcome.NotApplicable)]
        [TestCase(DealStatus.Fulfilled, OpportunityResponse.Taken, OpportunityOutcome.Won)]
        [TestCase(DealStatus.Broken, OpportunityResponse.Taken, OpportunityOutcome.Lost)]
        public void ActualReconciliationWritesOnePlayerOpportunityWithoutMakingSafetyMirrors(string status, string response, string outcome)
        {
            var s = State(); var row = Deal(s, "own"); s.unifiedCommitments.Add(row);
            if (status == DealStatus.Broken) Break(s, "nomination", Other(s));
            else { row.status = status; if (status == DealStatus.Fulfilled) row.settledWeek = 3; }
            s.unifiedCommitments.Add(Deal(s, "npc-only", Other(s, 1), Other(s, 2)));
            s.unifiedCommitments.Add(Promise(s, "promise-only"));
            string authority = JsonConvert.SerializeObject(s.unifiedCommitments);
            EpisodeEngine.ReconcileOpportunities(s);
            var chance = s.ledger.opportunities.Single(o => o.kind == OpportunityKinds.Deal);
            Assert.That((chance.id, chance.response, chance.outcome), Is.EqualTo(("own", response, outcome)));
            Assert.That(s.deals, Is.Empty); Assert.That(s.promises, Is.Empty);
            Assert.That(JsonConvert.SerializeObject(s.unifiedCommitments), Is.EqualTo(authority));
            string after = Json(s); EpisodeEngine.ReconcileOpportunities(s); Assert.That(Json(s), Is.EqualTo(after));
        }

        [Test]
        public void AnExpiredCanonicalDealPreviouslyTakenStaysTakenAfterReconciliation()
        {
            var s = State(); var row = Deal(s, "taken"); row.status = DealStatus.Expired; s.unifiedCommitments.Add(row);
            Chance(s, row.id, OpportunityOutcome.NotApplicable);
            EpisodeEngine.ReconcileOpportunities(s);
            var chance = s.ledger.opportunities.Single(o => o.id == row.id);
            Assert.That(chance.response, Is.EqualTo(OpportunityResponse.Taken));
            Assert.That(chance.outcome, Is.EqualTo(OpportunityOutcome.NotApplicable));
        }

        [TestCase(false)] [TestCase(true)]
        public void AFinalArgumentUsesOneFulfilledIncidentEvenWhenLockedAliasesHaveAnotherOrder(bool reverse)
        {
            var s = State(); s.finaleRulesStartWeek = 1; s.Find(Other(s)).traits = new List<string> { "Emotional" };
            foreach (string id in new[] { "alias-a", "alias-b", "owner" })
            {
                var row = Deal(s, id); if (id == "owner") row.trustImpact = DealTrust.Critical; Fulfill(s, row);
            }
            var references = new List<string> { "deal:alias-b", "deal:alias-a", "deal:owner" };
            if (reverse) references.Reverse();
            s.finalArgument = new FinalArgumentState { theme = FinalArgument.Emotional, momentRefs = references };
            string before = Json(s);
            Assert.That(FinalArgument.Term(s, Other(s), s.playerId), Is.EqualTo(FinalArgument.PerMoment));
            string keptLine = FinalArgument.Moments(s).Single(moment => moment.reference == "deal:owner").said;
            Assert.That(FinalArgument.Speech(s).Split(new[] { "\n\n" }, StringSplitOptions.None)
                .Count(line => line == keptLine), Is.EqualTo(1));
            Assert.That(FinalArgument.Resolves(s, "deal:alias-a"), Is.True);
            Assert.That(FinalArgument.SubjectOf(s, "deal:alias-b"), Is.EqualTo(Other(s)));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(DealStatus.Active)] [TestCase(DealStatus.Expired)] [TestCase(DealStatus.Broken)]
        public void AnUnkeptCanonicalAliasCannotBackTheFinaleBonusOrSpeech(string status)
        {
            var s = State(); s.finaleRulesStartWeek = 1; s.Find(Other(s)).traits = new List<string> { "Emotional" };
            var row = Deal(s, "unkept"); s.unifiedCommitments.Add(row);
            if (status == DealStatus.Broken) Break(s, "nomination", Other(s)); else row.status = status;
            s.finalArgument = new FinalArgumentState { theme = FinalArgument.Emotional, momentRefs = new List<string> { "deal:unkept" } };
            string before = Json(s);
            Assert.That(FinalArgument.Resolves(s, "deal:unkept"), Is.True);
            Assert.That(FinalArgument.Term(s, Other(s), s.playerId), Is.Zero);
            Assert.That(FinalArgument.Speech(s), Does.Not.Contain("kept our " + DealKind.Title(DealKind.SafetyAgreement).ToLowerInvariant()));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void AnNpcPairCannotBackThePlayersFinalArgumentEvenIfItsAliasWasLocked()
        {
            var s = State(); s.finaleRulesStartWeek = 1; s.Find(Other(s)).traits = new List<string> { "Emotional" };
            Fulfill(s, Deal(s, "not-yours", Other(s), Other(s, 1)));
            s.finalArgument = new FinalArgumentState { theme = FinalArgument.Emotional, momentRefs = new List<string> { "deal:not-yours" } };
            Assert.That(FinalArgument.Term(s, Other(s), s.playerId), Is.Zero);
            Assert.That(FinalArgument.Speech(s), Does.Not.Contain("kept our " + DealKind.Title(DealKind.SafetyAgreement).ToLowerInvariant()));
        }

        [Test]
        public void LegacyFinalArgumentKeepsItsOriginalReferenceBonusPolicy()
        {
            var s = State(); s.unifiedCommitmentRulesVersion = 0; s.finaleRulesStartWeek = 1;
            s.Find(Other(s)).traits = new List<string> { "Emotional" };
            foreach (string id in new[] { "one", "two", "three" }) s.deals.Add(new DealState {
                id = id, type = DealKind.SafetyAgreement, proposerId = s.playerId, recipientId = Other(s), week = 1, status = DealStatus.Fulfilled });
            s.finalArgument = new FinalArgumentState { theme = FinalArgument.Emotional,
                momentRefs = new List<string> { "deal:one", "deal:two", "deal:three" } };
            string before = Json(s);
            Assert.That(FinalArgument.Term(s, Other(s), s.playerId), Is.EqualTo(FinalArgument.Cap));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void FinalArgumentKeptClaimNamesTheActualReceiptWeekNotTheProposalWeek()
        {
            var s = State(); var row = Deal(s, "late"); row.origin = UnifiedCommitments.Lobby;
            row.createdWeek = 2; row.expiresWeek = 3; Fulfill(s, row);
            var moment = FinalArgument.Moments(s).Single(m => m.reference == "deal:late");
            Assert.That(moment.week, Is.EqualTo(3)); Assert.That(moment.text, Does.StartWith("Week 3"));
            Assert.That(moment.said, Does.StartWith("In week 3")); Assert.That(moment.said, Does.Not.Contain("made a"));
        }

        [TestCase(false)] [TestCase(true)]
        public void MixedAudiblePoliciesUseAUnionedGenericPageCaptionButKeepSpecificActualGossip(bool promiseFirst)
        {
            var s = State(); s.unifiedCommitments.Add(Promise(s, "promise")); s.unifiedCommitments.Add(Deal(s, "deal"));
            Break(s, "nomination", Other(s));
            var promise = new HouseFactState { id = "promise-fact", kind = FactKinds.BrokenWord, refId = "promise",
                actorId = s.playerId, subjectId = Other(s), week = 2, visibility = FactVisibility.Whispered,
                knowers = new List<string> { Other(s) } };
            var deal = new HouseFactState { id = "deal-fact", kind = FactKinds.BrokenWord, refId = "deal",
                actorId = s.playerId, subjectId = Other(s), week = 3, visibility = FactVisibility.Whispered,
                knowers = new List<string> { Other(s, 1) } };
            s.story.facts.Add(promiseFirst ? promise : deal); s.story.facts.Add(promiseFirst ? deal : promise);
            string before = Json(s); var grouped = YourWord.Breaches(s).Single();
            Assert.That(grouped.id, Is.EqualTo(promiseFirst ? promise.id : deal.id));
            Assert.That(grouped.refId, Is.EqualTo(promiseFirst ? promise.refId : deal.refId));
            Assert.That(grouped.week, Is.EqualTo(3)); Assert.That(grouped.knowers, Is.EquivalentTo(new[] { Other(s), Other(s, 1) }));
            Assert.That(YourWord.Lines(s).Single(), Does.Contain("word of safety to"));
            Assert.That(YourWord.Lines(s).Single(), Does.Contain("broken in week 3"));
            Assert.That(YourWord.Lines(s).Single(), Does.Not.Contain("promise of safety"));
            Assert.That(YourWord.HeardLine(s, promise, Other(s, 2)), Does.Contain("promise of safety"));
            Assert.That(YourWord.HeardLine(s, deal, Other(s, 2)), Does.Contain("safety deal"));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void APrivateOwnerPromiseDoesNotChangeTheWeakerAudibleDealCaptionOrAudience()
        {
            var s = State(); s.unifiedCommitments.Add(Promise(s, "owner")); s.unifiedCommitments.Add(Deal(s, "heard"));
            Break(s, "nomination", Other(s));
            Fact(s, "heard-fact", "heard", s.playerId, Other(s), FactVisibility.Whispered, Other(s, 1));
            Fact(s, "private-fact", "owner", s.playerId, Other(s), FactVisibility.Private, Other(s));
            string before = Json(s);
            Assert.That(YourWord.Breaches(s).Single().refId, Is.EqualTo("heard"));
            Assert.That(YourWord.Hearers(s), Is.EqualTo(new[] { Other(s, 1) }));
            Assert.That(YourWord.Lines(s).Single(), Does.Contain("safety deal"));
            Assert.That(YourWord.Lines(s).Single(), Does.Not.Contain("promise"));
            Assert.That(YourWord.Lines(s).Single(), Does.Not.Contain("word of safety"));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void CanonicalReceiptAliasesResolveTheirActualSettlementAfterStateClone(bool promise)
        {
            var s = State(); s.week = 2;
            var row = promise ? Promise(s, "stable") : Deal(s, "stable");
            if (!promise) { row.origin = UnifiedCommitments.Lobby; row.expiresWeek = 3; }
            s.unifiedCommitments.Add(row); s.week = 3; Break(s, "nomination", Other(s)); s = s.Clone();
            var exchange = Exchange(s, "stable", promise, Other(s)); string before = Json(s);
            Assert.That(FinaleQuestions.ReceiptLine(s, exchange), Does.StartWith("Week 3"));
            Assert.That(FinaleQuestions.ReceiptWeek(s, exchange), Is.EqualTo(3));
            Assert.That(JuryHouseRead.ReceiptWeek(s, exchange), Is.EqualTo(3));
            Assert.That(FinaleQuestions.Kicker(s, exchange), Does.StartWith("Week 3"));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void CanonicalReceiptCannotRevealAnNpcPairOrAnotherJurorsAgreement(bool promise, bool npcOnly)
        {
            var s = State(); string maker = npcOnly ? Other(s) : s.playerId, beneficiary = npcOnly ? Other(s, 1) : Other(s);
            s.unifiedCommitments.Add(promise ? Promise(s, "private", maker, beneficiary) : Deal(s, "private", maker, beneficiary));
            string unauthorized = npcOnly ? Other(s, 1) : Other(s, 2);
            var exchange = Exchange(s, "private", promise, unauthorized); string before = Json(s);
            Assert.That(FinaleQuestions.ReceiptLine(s, exchange), Is.Null);
            Assert.That(FinaleQuestions.ReceiptWeek(s, exchange), Is.Null);
            Assert.That(FinaleQuestions.Kicker(s, exchange), Is.Null);
            Assert.That(JuryHouseRead.ReceiptWeek(s, exchange), Is.Null);
            Assert.That(FinaleQuestions.Receipts(s, unauthorized).Any(r => r.id == "private"), Is.False);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void JuryStandingCoolsOnlyForThePlayersLaterBreachNotTheWrongedPlayer(bool playerBreaks)
        {
            var s = State(); string juror = Other(s);
            s.ledger.standings.Add(new StandingRow { week = 1, source = ClaimSource.Read, fromId = juror, toId = s.playerId, score = 70 });
            s.hohId = playerBreaks ? s.playerId : juror;
            s.unifiedCommitments.Add(Promise(s, "word", s.hohId, playerBreaks ? juror : s.playerId));
            Break(s, "nomination", playerBreaks ? juror : s.playerId); string before = Json(s);
            var read = JuryHouseRead.ReadJuror(s, juror);
            Assert.That(read.band, Is.EqualTo(playerBreaks ? JuryHouseRead.Wavering : JuryHouseRead.Supportive));
            if (playerBreaks) Assert.That(read.reason, Does.Contain("you broke a promise to them since"));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void JuryKnowledgeRetainsEachOwnAgreementButNotAnotherPairsProvenance()
        {
            var s = State(); string juror = Other(s);
            s.unifiedCommitments.Add(Deal(s, "deal", s.playerId, juror));
            s.unifiedCommitments.Add(Promise(s, "promise", s.playerId, juror));
            s.unifiedCommitments.Add(Deal(s, "not-theirs", s.playerId, Other(s, 1)));
            string before = Json(s); var read = JuryHouseRead.ReadJuror(s, juror);
            Assert.That(read.knows.Count, Is.EqualTo(2));
            Assert.That(read.knows.Any(line => line.StartsWith("You promised them", StringComparison.Ordinal)), Is.True);
            Assert.That(read.knows.Any(line => line.StartsWith(DealKind.Title(DealKind.SafetyAgreement), StringComparison.Ordinal)), Is.True);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void JuryMissingHistoryCountsActualAgreementsStruckAfterTheyLeft()
        {
            var s = State(); string juror = Other(s);
            s.ledger.power.Add(new PowerRow { week = 1, hohId = Other(s, 2), evicteeId = juror, tally = new List<int> { 3, 1 } });
            foreach (string status in new[] { DealStatus.Active, DealStatus.Fulfilled, DealStatus.Proposed, DealStatus.Declined })
            {
                var row = Deal(s, status, s.playerId, Other(s, 1)); row.status = status;
                if (status == DealStatus.Fulfilled) row.settledWeek = 3;
                s.unifiedCommitments.Add(row);
            }
            var ownJuror = Deal(s, "with-juror", s.playerId, juror); s.unifiedCommitments.Add(ownJuror);
            string before = Json(s); var read = JuryHouseRead.ReadJuror(s, juror);
            Assert.That(read.missing, Does.Contain("2 deals you struck in the weeks after they left."));
            Assert.That(read.missing.Count(line => line.Contains("deal")), Is.EqualTo(1));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        private static EpisodeState State()
        {
            var s = ContentCatalog.Create(17); s.week = 3; s.phase = EpisodePhase.Nomination; s.hohId = s.playerId;
            s.strategyRulesStartWeek = 1; s.commitmentRulesStartWeek = 1; s.unifiedCommitmentRulesVersion = 1;
            s.agencyRulesStartWeek = 0; s.story.rulesStartWeek = 1; s.story.rulesVersion = StoryRules.Current;
            return s;
        }

        private static string Other(EpisodeState s, int index = 0) => s.contestants.Where(c => c.id != s.playerId).Skip(index).First().id;

        private static UnifiedCommitmentState Promise(EpisodeState s, string id, string maker = null, string beneficiary = null) => new UnifiedCommitmentState {
            id = id, kind = UnifiedCommitments.Safety, sourcePolicy = UnifiedCommitments.PromisePolicy, origin = UnifiedCommitments.StoryPromise,
            makerId = maker ?? s.playerId, beneficiaryId = beneficiary ?? Other(s), createdWeek = s.week,
            expiresWeek = s.week + 1, status = DealStatus.Active, trustImpact = DealTrust.Medium,
        };

        private static UnifiedCommitmentState Deal(EpisodeState s, string id, string maker = null, string beneficiary = null) => new UnifiedCommitmentState {
            id = id, kind = UnifiedCommitments.Safety, sourcePolicy = UnifiedCommitments.DealPolicy, origin = UnifiedCommitments.StoryDeal,
            makerId = maker ?? s.playerId, beneficiaryId = beneficiary ?? Other(s), createdWeek = s.week,
            expiresWeek = s.week, status = DealStatus.Active, reciprocal = true, trustImpact = DealTrust.Medium,
        };

        private static void Fulfill(EpisodeState s, UnifiedCommitmentState row)
        {
            row.status = DealStatus.Fulfilled; row.settledWeek = s.week; s.unifiedCommitments.Add(row);
        }

        private static void Break(EpisodeState s, string decision, string wronged)
        {
            var evaluation = UnifiedCommitments.EvaluateNomination(s, decision, s.hohId, new[] { wronged });
            Assert.That(evaluation.Changes, Is.Not.Empty);
            foreach (var change in evaluation.Changes)
                s.unifiedCommitments[s.unifiedCommitments.FindIndex(row => row.id == change.Record.id)] = change.Record.Clone();
        }

        private static void Chance(EpisodeState s, string id, string outcome) => s.ledger.opportunities.Add(new OpportunityRow {
            id = id, kind = OpportunityKinds.Deal, week = s.week, source = UnifiedCommitments.Safety + ":" + Other(s),
            response = OpportunityResponse.Taken, outcome = outcome,
        });

        private static List<GameSense.Note> DealNotes(EpisodeState s) => GameSense.Evaluate(s).notes
            .Where(n => n.face == GameSense.Strategy && n.rowKind == "opportunity").ToList();

        private static HouseFactState Fact(EpisodeState s, string id, string reference, string actor, string subject,
            string visibility, params string[] knowers)
        {
            var fact = new HouseFactState { id = id, kind = FactKinds.BrokenWord, refId = reference, actorId = actor,
                subjectId = subject, week = s.week, visibility = visibility, knowers = knowers.ToList() };
            s.story.facts.Add(fact); return fact;
        }

        private static JuryExchangeState Exchange(EpisodeState s, string id, bool promise, string juror) => new JuryExchangeState {
            questionerId = juror, finalistId = s.playerId, category = FinaleQuestions.Accountability,
            receiptKind = promise ? FinaleQuestions.PromiseReceipt : FinaleQuestions.DealReceipt, receiptId = id,
        };

        private static void Invoke(string name, params object[] arguments)
        {
            var method = typeof(EpisodeEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, name);
            try { method.Invoke(null, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        private static string Json(EpisodeState s) => JsonConvert.SerializeObject(s.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToDictionary(field => field.Name, field => field.GetValue(s), StringComparer.Ordinal));
    }
}
