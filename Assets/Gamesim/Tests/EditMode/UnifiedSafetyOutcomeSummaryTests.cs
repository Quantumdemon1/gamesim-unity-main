using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>Prospective read contracts, not production rule-one activation or enabled-save acceptance.</summary>
    public sealed class UnifiedSafetyOutcomeSummaryTests
    {
        [TestCase(DealStatus.Accepted, false)] [TestCase(DealStatus.Accepted, true)]
        [TestCase(DealStatus.Active, false)] [TestCase(DealStatus.Active, true)]
        [TestCase(DealStatus.Fulfilled, false)] [TestCase(DealStatus.Fulfilled, true)]
        [TestCase(DealStatus.Broken, false)] [TestCase(DealStatus.Broken, true)]
        public void AllianceInventoryReadsTheReciprocalDealSourceInBothDirections(string status, bool reverse)
        {
            var s = State(); string partner = Other(s); Pact(s, partner);
            var row = Row(s, false, "safety", reverse ? partner : s.playerId, reverse ? s.playerId : partner);
            s.unifiedCommitments.Add(row);
            if (status == DealStatus.Broken) Break(s, s.playerId, partner);
            else if (status == DealStatus.Fulfilled) Spare(s, s.playerId, Other(s, 1));
            else row.status = status;
            ReadOnly(s, () =>
            {
                var deal = AllianceRead.Yours(s).Single().deals.Single();
                Assert.That((deal.week, deal.withId, deal.type, deal.status), Is.EqualTo((3, partner, DealKind.SafetyAgreement, status)));
                Assert.That(deal.text, Is.EqualTo(DealKind.Title(DealKind.SafetyAgreement) + " with "
                    + FinalistRead.FirstName(s.Find(partner).name) + " · " + HouseguestNotes.DealStanding(status, reverse)));
            });
        }

        [TestCase(DealStatus.Proposed)] [TestCase(DealStatus.Declined)] [TestCase(DealStatus.Expired)]
        public void OffersAndLapsedAgreementsAreNotAnAlliancesAgreedDealInventory(string status)
        {
            var s = State(); Pact(s, Other(s)); var row = Row(s, false, "not-agreed"); row.status = status;
            s.unifiedCommitments.Add(row);
            ReadOnly(s, () => Assert.That(AllianceRead.Yours(s).Single().deals, Is.Empty));
        }

        [TestCase(DealStatus.Active)] [TestCase(DealStatus.Broken)] [TestCase(DealStatus.Expired)]
        public void AUnilateralPromiseNeverMasqueradesAsAnAlliancesReciprocalDeal(string status)
        {
            var s = State(); Pact(s, Other(s)); var row = Row(s, true, "promise"); s.unifiedCommitments.Add(row);
            if (status == DealStatus.Broken) Break(s, s.playerId, Other(s)); else row.status = status;
            ReadOnly(s, () => Assert.That(AllianceRead.Yours(s).Single().deals, Is.Empty));
        }

        [TestCase(false)] [TestCase(true)]
        public void AnAlliancesInventoryDoesNotRevealAnNpcsOtherAgreements(bool npcPair)
        {
            var s = State(); Pact(s, Other(s));
            s.unifiedCommitments.Add(Row(s, false, "elsewhere", npcPair ? Other(s) : s.playerId, Other(s, 1)));
            ReadOnly(s, () => Assert.That(AllianceRead.Yours(s).Single().deals, Is.Empty));
        }

        [TestCase(1, false)] [TestCase(1, true)] [TestCase(3, false)] [TestCase(3, true)]
        [TestCase(5, false)] [TestCase(5, true)]
        public void AllianceAgreementProvenanceIsNotCollapsedIntoAnIncidentCount(int copies, bool ended)
        {
            var s = State(); Pact(s, Other(s)).active = !ended;
            for (int i = 0; i < copies; i++) s.unifiedCommitments.Add(Row(s, false, "alias-" + i));
            Break(s, s.playerId, Other(s));
            ReadOnly(s, () =>
            {
                Assert.That(UnifiedCommitmentHistory.Breaches(s), Has.Count.EqualTo(1));
                var pact = AllianceRead.Yours(s).Single(); Assert.That(pact.active, Is.EqualTo(!ended));
                Assert.That(pact.deals, Has.Count.EqualTo(copies));
                Assert.That(pact.deals.All(d => d.status == DealStatus.Broken && d.withId == Other(s)), Is.True);
                Assert.That(s.unifiedCommitments.Select(r => r.id), Is.EqualTo(Enumerable.Range(0, copies).Select(i => "alias-" + i)));
            });
        }

        [Test]
        public void AllianceInventoryRetainsCreationChronologyAndSavedTieOrderAcrossSources()
        {
            var s = State(); Pact(s, Other(s), Other(s, 1), Other(s, 2));
            var later = Row(s, false, "a-later-id", s.playerId, Other(s, 2)); s.unifiedCommitments.Add(later);
            var earlier = Row(s, false, "z-earlier-id", s.playerId, Other(s, 1));
            earlier.createdWeek = 2; earlier.expiresWeek = 2; s.unifiedCommitments.Add(earlier);
            var legacy = Legacy(s, "legacy", DealKind.InformationSharing, DealStatus.Active, s.playerId, Other(s));
            legacy.week = 2; legacy.expiresWeek = 3; s.deals.Add(legacy);
            var tied = Row(s, false, "b-tied", s.playerId, Other(s)); tied.createdWeek = 2; tied.expiresWeek = 2;
            s.unifiedCommitments.Add(tied);
            ReadOnly(s, () =>
            {
                var deals = AllianceRead.Yours(s).Single().deals;
                Assert.That(deals.Select(d => d.week), Is.EqualTo(new[] { 2, 2, 2, 3 }));
                Assert.That(deals.Select(d => d.withId), Is.EqualTo(new[] { Other(s), Other(s, 1), Other(s), Other(s, 2) }));
                Assert.That(deals.Select(d => d.type), Is.EqualTo(new[] { DealKind.InformationSharing,
                    DealKind.SafetyAgreement, DealKind.SafetyAgreement, DealKind.SafetyAgreement }));
            });
        }

        [Test]
        public void ACarriedNpcOfferKeepsItsProposalWeekWithoutInventingItsSettlementWeek()
        {
            var s = State(); Pact(s, Other(s));
            var row = Row(s, false, NpcDeals.OfferPrefix + "safety", Other(s), s.playerId);
            row.origin = UnifiedCommitments.NpcOffer; row.createdWeek = 2; row.expiresWeek = 3;
            s.unifiedCommitments.Add(row); Break(s, Other(s), s.playerId);
            ReadOnly(s, () =>
            {
                Assert.That(row.createdWeek, Is.EqualTo(2));
                Assert.That(s.unifiedCommitments.Single().settledWeek, Is.EqualTo(3));
                Assert.That(AllianceRead.Yours(s).Single().deals.Single().week, Is.EqualTo(2));
                Assert.That(FinalCaseResume.Read(s).brokenAgainstYou, Is.EqualTo(1));
            });
        }

        [TestCase(false)] [TestCase(true)]
        public void CanonicalInventoryDoesNotBypassTheOtherFamiliesSecretBallotStanding(bool known)
        {
            var s = State(); string partner = Other(s, 3); Pact(s, partner); SecretVote(s, partner, known);
            s.deals.Add(Legacy(s, "bloc", DealKind.VoteTogether, DealStatus.Broken, s.playerId, partner, 1));
            s.unifiedCommitments.Add(Row(s, false, "safety", s.playerId, partner));
            ReadOnly(s, () =>
            {
                Assert.That(KnownBallots.DealOutcomeKnown(s, s.deals.Single()), Is.EqualTo(known));
                var deals = AllianceRead.Yours(s).Single().deals;
                Assert.That(deals, Has.Count.EqualTo(2));
                Assert.That(deals[0].text, Does.EndWith(" · " + (known
                    ? HouseguestNotes.DealStanding(DealStatus.Broken, false) : KnownBallots.Unresolved)));
                Assert.That(deals[1].type, Is.EqualTo(DealKind.SafetyAgreement));
                Assert.That(deals[1].text, Does.EndWith(" · " + HouseguestNotes.DealStanding(DealStatus.Active, false)));
            });
        }

        [TestCase(1, false, false)] [TestCase(1, false, true)] [TestCase(1, true, false)] [TestCase(1, true, true)]
        [TestCase(3, false, false)] [TestCase(3, false, true)] [TestCase(3, true, false)] [TestCase(3, true, true)]
        [TestCase(5, false, false)] [TestCase(5, false, true)] [TestCase(5, true, false)] [TestCase(5, true, true)]
        public void ResumeCountsOneActualActorPairIncidentAndSelectsItsSourceCaption(int copies, bool promise, bool playerActs)
        {
            var s = State(); string actor = playerActs ? s.playerId : Other(s), wronged = playerActs ? Other(s) : s.playerId;
            for (int i = 0; i < copies; i++) s.unifiedCommitments.Add(Row(s, false, "deal-" + i,
                i % 2 == 0 ? actor : wronged, i % 2 == 0 ? wronged : actor));
            if (promise) s.unifiedCommitments.Add(Row(s, true, "promise", actor, wronged));
            s.unifiedCommitments.Reverse(); Break(s, actor, wronged);
            ReadOnly(s, () =>
            {
                var incident = UnifiedCommitmentHistory.Breaches(s).Single();
                Assert.That(incident.EvidenceIds, Has.Count.EqualTo(copies + (promise ? 1 : 0)));
                Assert.That((incident.ActorId, incident.WrongedId), Is.EqualTo((actor, wronged)));
                Assert.That(incident.EffectOwnerId, Is.EqualTo(promise ? "promise" : "deal-0"));
                var resume = FinalCaseResume.Read(s);
                Assert.That((resume.brokenByYou, resume.brokenAgainstYou), Is.EqualTo(playerActs ? (1, 0) : (0, 1)));
                Assert.That(resume.betrayals, Is.EqualTo(new[] { Betrayal(s, promise, playerActs, Other(s)) }));
                Assert.That(resume.betrayals.Any(line => line.IndexOf("week", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            });
        }

        [TestCase(1, false)] [TestCase(1, true)] [TestCase(3, false)] [TestCase(3, true)]
        public void PromiseOnlyAliasesAreOneUnilateralBetrayalInTheResume(int copies, bool playerActs)
        {
            var s = State(); string actor = playerActs ? s.playerId : Other(s), wronged = playerActs ? Other(s) : s.playerId;
            for (int i = 0; i < copies; i++) s.unifiedCommitments.Add(Row(s, true, "promise-" + i, actor, wronged));
            Break(s, actor, wronged);
            ReadOnly(s, () =>
            {
                var resume = FinalCaseResume.Read(s);
                Assert.That(resume.brokenByYou, Is.EqualTo(playerActs ? 1 : 0));
                Assert.That(resume.brokenAgainstYou, Is.EqualTo(playerActs ? 0 : 1));
                Assert.That(resume.betrayals, Is.EqualTo(new[] { Betrayal(s, true, playerActs, Other(s)) }));
            });
        }

        [Test]
        public void ResumeExcludesPrivateNpcPairIncidentsButRetainsThePlayersActualOwnBreach()
        {
            var s = State(); s.unifiedCommitments.Add(Row(s, false, "private", Other(s, 1), Other(s, 2)));
            Break(s, Other(s, 1), Other(s, 2));
            s.unifiedCommitments.Add(Row(s, true, "own")); Break(s, s.playerId, Other(s));
            ReadOnly(s, () =>
            {
                Assert.That(UnifiedCommitmentHistory.Breaches(s), Has.Count.EqualTo(2));
                var resume = FinalCaseResume.Read(s);
                Assert.That((resume.brokenByYou, resume.brokenAgainstYou), Is.EqualTo((1, 0)));
                Assert.That(resume.betrayals, Is.EqualTo(new[] { Betrayal(s, true, true, Other(s)) }));
                Assert.That(FinalistRead.RelationshipLine(s, Other(s, 1)), Is.Null);
            });
        }

        [Test]
        public void ResumeOrdersCanonicalBreaksByActualSettlementWithoutRedatingLegacyRows()
        {
            var s = State(); s.week = 2;
            var carried = Row(s, false, NpcDeals.OfferPrefix + "carried", Other(s), s.playerId);
            carried.origin = UnifiedCommitments.NpcOffer; carried.expiresWeek = 2; s.unifiedCommitments.Add(carried);
            s.week = 3; s.unifiedCommitments.Add(Row(s, false, "middle", s.playerId, Other(s, 1)));
            Break(s, s.playerId, Other(s, 1));
            s.week = 4; carried.expiresWeek = 4; Break(s, Other(s), s.playerId);
            var legacy = Legacy(s, "legacy", DealKind.InformationSharing, DealStatus.Broken, s.playerId, Other(s, 2), 1);
            legacy.settledWeek = 4; s.deals.Add(legacy);
            ReadOnly(s, () =>
            {
                var resume = FinalCaseResume.Read(s);
                Assert.That((resume.brokenByYou, resume.brokenAgainstYou), Is.EqualTo((2, 1)));
                Assert.That(resume.betrayals, Is.EqualTo(new[] { Betrayal(s, false, false, Other(s)),
                    Betrayal(s, false, true, Other(s, 1)), "You broke your " + DealKind.Title(DealKind.InformationSharing).ToLowerInvariant()
                        + " with " + s.Find(Other(s, 2)).name + "." }));
            });
        }

        [Test]
        public void ResumeKeepsThreeLatestCaptionsWithoutDroppingTheTotalActualIncidents()
        {
            var s = State();
            for (int week = 2; week <= 5; week++)
            {
                s.week = week; s.unifiedCommitments.Add(Row(s, true, "word-" + week, s.playerId, Other(s, week - 2)));
                Break(s, s.playerId, Other(s, week - 2));
            }
            ReadOnly(s, () =>
            {
                var resume = FinalCaseResume.Read(s);
                Assert.That((resume.brokenByYou, resume.brokenAgainstYou), Is.EqualTo((4, 0)));
                Assert.That(resume.betrayals, Is.EqualTo(new[] { Betrayal(s, true, true, Other(s, 3)),
                    Betrayal(s, true, true, Other(s, 2)), Betrayal(s, true, true, Other(s, 1)) }));
                Assert.That(resume.betrayals, Has.Count.EqualTo(FinalCaseResume.MostBetrayals));
            });
        }

        [TestCase(false)] [TestCase(true)]
        public void ActualNominationGatewayEvidenceSurvivesCappedNarrativeAndPowerRecords(bool promise)
        {
            var s = State(); s.unifiedCommitments.Add(Row(s, false, "deal"));
            if (promise) s.unifiedCommitments.Add(Row(s, true, "promise"));
            Invoke("ResolveUnifiedSafetyNomination", s, "replacement", s.playerId, new[] { Other(s) });
            Assert.That(s.unifiedCommitments.All(row => row.status == DealStatus.Broken), Is.True);
            Assert.That(s.events.Count + s.relationships.Sum(edge => edge.events.Count), Is.GreaterThan(0));
            s.events.Clear(); s.memories.Clear(); s.ledger.power.Clear();
            foreach (var edge in s.relationships) edge.events.Clear();
            ReadOnly(s, () =>
            {
                var resume = FinalCaseResume.Read(s);
                Assert.That((resume.brokenByYou, resume.brokenAgainstYou), Is.EqualTo((1, 0)));
                Assert.That(resume.betrayals, Is.EqualTo(new[] { Betrayal(s, promise, true, Other(s)) }));
                Assert.That(s.promises, Is.Empty); Assert.That(s.deals, Is.Empty);
            });
        }

        [TestCase("identity")] [TestCase("effect")]
        public void ResumeRejectsMalformedCanonicalBetrayalEvidenceWithoutMutation(string fault)
        {
            var s = State(); s.unifiedCommitments.Add(Row(s, false, "safety")); Break(s, s.playerId, Other(s));
            if (fault == "identity") s.unifiedCommitments.Single().id = "";
            else s.unifiedCommitments.Single().settlementEffectKey = "safety:not-an-actual-decision";
            ReadOnly(s, () => Assert.Throws<ArgumentException>(() => FinalCaseResume.Read(s)));
        }

        [TestCase(1, false)] [TestCase(1, true)] [TestCase(3, false)] [TestCase(3, true)]
        [TestCase(5, false)] [TestCase(5, true)]
        public void FinalistKeptLineCountsTheActualPairWeekReceiptRatherThanItsAliases(int copies, bool reverse)
        {
            var s = State();
            for (int i = 0; i < copies; i++) s.unifiedCommitments.Add(Row(s, false, "kept-" + i,
                reverse && i % 2 == 0 ? Other(s) : s.playerId, reverse && i % 2 == 0 ? s.playerId : Other(s)));
            Spare(s, s.playerId, Other(s, 1));
            ReadOnly(s, () =>
            {
                var receipt = UnifiedCommitmentHistory.Fulfillments(s).Single();
                Assert.That(receipt.SettledWeek, Is.EqualTo(3)); Assert.That(receipt.EvidenceIds, Has.Count.EqualTo(copies));
                Assert.That(FinalistRead.RelationshipLine(s, Other(s)), Is.EqualTo("Kept a deal with you"));
                Assert.That(FinalistRead.TowardYou(s, Other(s)).certainty, Is.EqualTo(FinalistRead.Unknown),
                    "A reciprocal keeping receipt invents no nominee, breaker or keeping actor.");
            });
        }

        [Test]
        public void DifferentSettlementWeeksGiveTwoActualKeptReceiptsEvenWithMultipleAgreements()
        {
            var s = State(); s.week = 2;
            for (int i = 0; i < 3; i++) s.unifiedCommitments.Add(Row(s, false, "first-" + i));
            Spare(s, s.playerId, Other(s, 1));
            s.week = 3;
            for (int i = 0; i < 2; i++) s.unifiedCommitments.Add(Row(s, false, "second-" + i));
            Spare(s, s.playerId, Other(s, 1));
            ReadOnly(s, () =>
            {
                Assert.That(UnifiedCommitmentHistory.Fulfillments(s).Select(r => r.SettledWeek), Is.EqualTo(new[] { 2, 3 }));
                Assert.That(FinalistRead.RelationshipLine(s, Other(s)), Is.EqualTo("Kept 2 deals with you"));
            });
        }

        [TestCase(false)] [TestCase(true)]
        public void AnotherPairCannotSupplyTheFinalistsKeptLine(bool npcPair)
        {
            var s = State(); string actor = npcPair ? Other(s) : s.playerId;
            s.unifiedCommitments.Add(Row(s, false, "other-pair", actor, Other(s, 1)));
            Spare(s, actor, Other(s, 2));
            ReadOnly(s, () =>
            {
                Assert.That(UnifiedCommitmentHistory.Fulfillments(s), Has.Count.EqualTo(1));
                Assert.That(FinalistRead.RelationshipLine(s, Other(s)), Is.Null);
                if (npcPair) Assert.That(FinalistRead.RelationshipLine(s, Other(s, 1)), Is.Null);
                else Assert.That(FinalistRead.RelationshipLine(s, Other(s, 1)), Is.EqualTo("Kept a deal with you"));
            });
        }

        [TestCase(DealStatus.Active)] [TestCase(DealStatus.Accepted)] [TestCase(DealStatus.Proposed)]
        [TestCase(DealStatus.Declined)] [TestCase(DealStatus.Expired)] [TestCase(DealStatus.Broken)]
        public void NonFulfilledDealStatesDoNotBecomeAKeptLine(string status)
        {
            var s = State(); var row = Row(s, false, "not-kept"); s.unifiedCommitments.Add(row);
            if (status == DealStatus.Broken) Break(s, s.playerId, Other(s)); else row.status = status;
            ReadOnly(s, () =>
            {
                Assert.That(UnifiedCommitmentHistory.Fulfillments(s), Is.Empty);
                Assert.That(FinalistRead.RelationshipLine(s, Other(s)), Is.Null);
            });
        }

        [TestCase(false)] [TestCase(true)]
        public void AStandingOrExpiredUnilateralPromiseDoesNotBecomeADealKeptAtFinalVeto(bool expired)
        {
            var s = State(); var promise = Row(s, true, "promise"); s.unifiedCommitments.Add(promise);
            s.hohId = s.playerId;
            Assert.That(UnifiedCommitments.EvaluateFinalVetoSpared(s, s.playerId, new[] { Other(s, 1) }).Changes, Is.Empty);
            if (expired)
            {
                s.week = 5; Apply(s, UnifiedCommitments.Expire(s, UnifiedCommitmentExpiry.PromiseWeekTurn));
                Assert.That(s.unifiedCommitments.Single().status, Is.EqualTo(DealStatus.Expired));
            }
            ReadOnly(s, () => Assert.That(FinalistRead.RelationshipLine(s, Other(s)), Is.Null));
        }

        [Test]
        public void KeptLineAddsLegacyOtherFamilyReceiptsWithoutCountingCanonicalAliasesAgain()
        {
            var s = State(); s.deals.Add(Legacy(s, "information", DealKind.InformationSharing, DealStatus.Fulfilled, s.playerId, Other(s)));
            s.deals.Add(Legacy(s, "target", DealKind.TargetAgreement, DealStatus.Fulfilled, Other(s), s.playerId));
            for (int i = 0; i < 3; i++) s.unifiedCommitments.Add(Row(s, false, "safety-" + i));
            Spare(s, s.playerId, Other(s, 1));
            ReadOnly(s, () => Assert.That(FinalistRead.RelationshipLine(s, Other(s)), Is.EqualTo("Kept 3 deals with you")));
        }

        [TestCase(false)] [TestCase(true)]
        public void KeptLineStillRequiresKnownOutcomeForLegacyVoteDeals(bool known)
        {
            var s = State(); string partner = Other(s, 3); SecretVote(s, partner, known, sameVote: true);
            s.deals.Add(Legacy(s, "bloc", DealKind.VoteTogether, DealStatus.Fulfilled, s.playerId, partner, 1));
            s.unifiedCommitments.Add(Row(s, false, "kept", s.playerId, partner)); Spare(s, s.playerId, Other(s, 1));
            ReadOnly(s, () =>
            {
                Assert.That(KnownBallots.DealOutcomeKnown(s, s.deals.Single()), Is.EqualTo(known));
                if (known) Assert.That(KnownBallots.TargetOf(s, 1, partner), Is.EqualTo(KnownBallots.TargetOf(s, 1, s.playerId)),
                    "A kept voting bloc is the same ballot, not a contradictory fulfilled label.");
                Assert.That(FinalistRead.RelationshipLine(s, partner), Is.EqualTo(known ? "Kept 2 deals with you" : "Kept a deal with you"));
            });
        }

        [Test]
        public void KeptLinePreservesKnownAllianceAndExistingCanonicalTowardYouIncidentBranches()
        {
            var s = State(); s.week = 2; var pact = Pact(s, Other(s));
            s.ledger.alliances.Add(new AllianceRow { id = pact.id, startedWeek = 1, why = "player/formed" });
            s.unifiedCommitments.Add(Row(s, false, "kept")); Spare(s, s.playerId, Other(s, 1));
            s.week = 3; s.unifiedCommitments.Add(Row(s, false, "broken"));
            s.unifiedCommitments.Add(Row(s, true, "word", Other(s), s.playerId)); Break(s, Other(s), s.playerId);
            ReadOnly(s, () =>
            {
                Assert.That(FinalistRead.TowardYou(s, Other(s)).value, Is.EqualTo("Broke a safety commitment to you"));
                Assert.That(FinalistRead.RelationshipLine(s, Other(s)),
                    Is.EqualTo("Allied since week 1 · Kept a deal with you · Broke a safety commitment to you"));
                Assert.That(FinalCaseResume.Read(s).brokenAgainstYou, Is.EqualTo(1));
            });
        }

        [TestCase(false)] [TestCase(true)]
        public void ActualFinalVetoGatewayReceiptRemainsReadableAfterItsPublicNarrativeIsCapped(bool npcHoh)
        {
            var s = State(); s.hohId = npcHoh ? Other(s) : s.playerId;
            for (int i = 0; i < 3; i++) s.unifiedCommitments.Add(Row(s, false, "kept-" + i));
            Invoke("ResolveUnifiedSafetySpared", s, s.hohId, new[] { Other(s, 1) });
            Assert.That(s.unifiedCommitments.All(row => row.status == DealStatus.Fulfilled && row.settledWeek == 3), Is.True);
            Assert.That(s.relationships.Sum(edge => edge.events.Count), Is.GreaterThan(0));
            s.events.Clear(); s.memories.Clear(); s.ledger.power.Clear();
            foreach (var edge in s.relationships) edge.events.Clear();
            ReadOnly(s, () =>
            {
                Assert.That(FinalistRead.RelationshipLine(s, Other(s)), Is.EqualTo("Kept a deal with you"));
                Assert.That(s.promises, Is.Empty); Assert.That(s.deals, Is.Empty);
            });
        }

        [Test]
        public void RulesZeroRetainsItsExactLegacyListCountsOrderAndUndatedCopy()
        {
            var s = State(); s.unifiedCommitmentRulesVersion = 0; var pact = Pact(s, Other(s));
            s.ledger.alliances.Add(new AllianceRow { id = pact.id, startedWeek = 1, why = "player/formed" });
            s.promises.Add(new PromiseState { id = "own-word", fromId = s.playerId, toId = Other(s), kind = PromiseKind.Safety,
                status = PromiseStatus.Broken, week = 3, expiresWeek = 4 });
            s.promises.Add(new PromiseState { id = "their-word", fromId = Other(s), toId = s.playerId, kind = PromiseKind.Safety,
                status = PromiseStatus.Broken, week = 2, expiresWeek = 3 });
            s.deals.Add(Legacy(s, "kept-one", DealKind.SafetyAgreement, DealStatus.Fulfilled, s.playerId, Other(s), 1));
            s.deals.Add(Legacy(s, "kept-two", DealKind.SafetyAgreement, DealStatus.Fulfilled, Other(s), s.playerId, 2));
            s.unifiedCommitments.Add(new UnifiedCommitmentState { id = "disabled-malformed" });
            ReadOnly(s, () =>
            {
                Assert.That(AllianceRead.Yours(s).Single().deals.Select(d => d.week), Is.EqualTo(new[] { 1, 2 }));
                Assert.That(FinalistRead.RelationshipLine(s, Other(s)),
                    Is.EqualTo("Allied since week 1 · Kept 2 deals with you · Broke a promise to you"));
                var resume = FinalCaseResume.Read(s);
                Assert.That((resume.brokenByYou, resume.brokenAgainstYou), Is.EqualTo((1, 1)));
                Assert.That(resume.betrayals, Is.EqualTo(new[] { Betrayal(s, true, true, Other(s)), Betrayal(s, true, false, Other(s)) }));
            });
        }

        [Test]
        public void ExistingEmptyAndInvalidSubjectGuardsDoNotInventSummaryEvidence()
        {
            var s = State(); ReadOnly(s, () =>
            {
                Assert.That(AllianceRead.Yours(null), Is.Empty);
                Assert.That(FinalCaseResume.Read(null).betrayals, Is.Empty);
                Assert.That(FinalCaseResume.Read(s).brokenByYou + FinalCaseResume.Read(s).brokenAgainstYou, Is.Zero);
                Assert.That(FinalistRead.RelationshipLine(s, null), Is.Null);
                Assert.That(FinalistRead.RelationshipLine(s, ""), Is.Null);
                Assert.That(FinalistRead.RelationshipLine(s, "absent-person"), Is.Null);
                Assert.That(FinalistRead.RelationshipLine(s, s.playerId), Is.Null);
            });
        }

        private static EpisodeState State()
        {
            var s = ContentCatalog.Create(17); s.week = 3; s.hohId = s.playerId; s.phase = EpisodePhase.Nomination;
            s.unifiedCommitmentRulesVersion = 1; s.commitmentRulesStartWeek = 1; s.strategyRulesStartWeek = 1;
            s.story.rulesVersion = 0; s.story.rulesStartWeek = 0; s.agencyRulesStartWeek = 0;
            s.promises.Clear(); s.deals.Clear(); s.alliances.Clear(); s.events.Clear(); s.memories.Clear();
            s.ledger.power.Clear(); s.ledger.alliances.Clear(); s.ledger.ballots.Clear(); s.ledger.claims.Clear();
            foreach (var person in s.contestants) person.traits.Clear();
            foreach (var edge in s.relationships) { edge.score = 0; edge.events.Clear(); }
            return s;
        }

        private static string Other(EpisodeState s, int index = 0) => s.contestants.Where(c => c.id != s.playerId).Skip(index).First().id;

        private static UnifiedCommitmentState Row(EpisodeState s, bool promise, string id, string maker = null, string beneficiary = null) => new UnifiedCommitmentState {
            id = id, kind = UnifiedCommitments.Safety, sourcePolicy = promise ? UnifiedCommitments.PromisePolicy : UnifiedCommitments.DealPolicy,
            origin = promise ? UnifiedCommitments.StoryPromise : UnifiedCommitments.StoryDeal,
            makerId = maker ?? s.playerId, beneficiaryId = beneficiary ?? Other(s), reciprocal = !promise,
            createdWeek = s.week, expiresWeek = s.week + (promise ? 1 : 0), status = DealStatus.Active, trustImpact = DealTrust.Medium,
        };

        private static AllianceState Pact(EpisodeState s, params string[] members)
        {
            var pact = new AllianceState { id = "ours", name = "The Core", members = new[] { s.playerId }.Concat(members).ToList() };
            s.alliances.Add(pact); return pact;
        }

        private static DealState Legacy(EpisodeState s, string id, string type, string status, string maker, string beneficiary, int week = 3) => new DealState {
            id = id, type = type, status = status, proposerId = maker, recipientId = beneficiary,
            week = week, expiresWeek = week, settledWeek = status == DealStatus.Broken || status == DealStatus.Fulfilled ? week : 0,
            brokenById = status == DealStatus.Broken ? maker : null, trustImpact = DealTrust.Medium,
        };

        private static void SecretVote(EpisodeState s, string partner, bool known, bool sameVote = false)
        {
            s.ledger.power.Add(new PowerRow { week = 1, hohId = Other(s), nominees = new List<string> { Other(s, 1), Other(s, 2) },
                evicteeId = Other(s, 1), tally = new List<int> { 2, 1 } });
            s.ledger.ballots.Add(new BallotRow { week = 1, voterId = s.playerId, targetId = Other(s, 1) });
            if (known) s.ledger.claims.Add(new ClaimRow { week = 1, voterId = partner, targetId = Other(s, sameVote ? 1 : 2),
                source = ClaimSource.Told, status = ClaimStatus.Kept });
        }

        private static void Break(EpisodeState s, string actor, params string[] wronged)
        {
            s.hohId = actor; var evaluation = UnifiedCommitments.EvaluateNomination(s, "nomination", actor, wronged);
            Assert.That(evaluation.Changes, Is.Not.Empty); Apply(s, evaluation);
        }

        private static void Spare(EpisodeState s, string actor, params string[] block)
        {
            s.hohId = actor; var evaluation = UnifiedCommitments.EvaluateFinalVetoSpared(s, actor, block);
            Assert.That(evaluation.Changes, Is.Not.Empty); Apply(s, evaluation);
        }

        private static void Apply(EpisodeState s, UnifiedCommitmentEvaluation evaluation)
        {
            foreach (var change in evaluation.Changes)
                s.unifiedCommitments[s.unifiedCommitments.FindIndex(row => row.id == change.Record.id)] = change.Record.Clone();
        }

        private static string Betrayal(EpisodeState s, bool promise, bool playerActs, string other)
        {
            string title = DealKind.Title(DealKind.SafetyAgreement).ToLowerInvariant(), name = s.Find(other).name;
            return playerActs ? (promise ? "You broke your word to " + name + "." : "You broke your " + title + " with " + name + ".")
                : (promise ? name + " broke their word to you." : name + " broke your " + title + ".");
        }

        private static void Invoke(string name, params object[] arguments)
        {
            var method = typeof(EpisodeEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, name);
            try { method.Invoke(null, arguments); } catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        private static void ReadOnly(EpisodeState s, Action assertions)
        {
            string before = Json(s); assertions(); Assert.That(Json(s), Is.EqualTo(before), "Reading must preserve all public state and RNG.");
        }

        private static string Json(EpisodeState s) => JsonConvert.SerializeObject(s.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToDictionary(field => field.Name, field => field.GetValue(s), StringComparer.Ordinal));
    }
}
