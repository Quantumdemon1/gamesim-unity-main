using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The complete internal prospective saved-Safety validator, not a writable projection or
    /// public activation. Positive scenarios select the flags on an asserted raw-Safety-empty
    /// real factory and progress the actual private Execute and post-command owners. They never
    /// assign an HoH, contestant status, nomination, settlement, source ID or artificial PowerRow.
    /// This detached source progression deliberately does not manufacture public command receipts
    /// or revisions. Public construction/validation remain closed; native saves and enabled public
    /// transactions require their own later gates. Negative controls corrupt rows; the explicitly
    /// labeled retained-archive projection omits a real live fact, without claiming a capacity-pruner
    /// run. The weaker-alias diagnostic invokes the real fact core and observed-spread owners, not an
    /// ordinary nomination emission or a public enabled transaction. Wording diagnostics project an
    /// actually eligible receipt into a played pending exchange, not a naturally selected category.
    /// </summary>
    public sealed class UnifiedSafetyWholeStateTests
    {
        [TestCase(false, false)] [TestCase(false, true)]
        [TestCase(true, false)] [TestCase(true, true)]
        public void RealFreshFactoriesValidateInternallyButCannotActivatePublicly(bool builtRoster, bool hearing)
        {
            var s = Fresh(2505, hearing, builtRoster ? 8 : 6, builtRoster);
            Assert.That(s.contestants, Has.Count.EqualTo(builtRoster ? 8 : 6));
            Assert.That(s.unifiedCommitments, Is.Empty);
            AcceptedUnchanged(s);
            PublicRefusedUnchanged(s);
        }

        [TestCase(true, false)] [TestCase(true, true)]
        [TestCase(false, false)] [TestCase(false, true)]
        public void ActualHoHNominationKeepsTheFullStateAndSelectedSourceFactPolicy(bool promise, bool hearing)
        {
            var owned = NominationBreach(promise, hearing);
            var row = owned.State.unifiedCommitments.Single(r => r.id == owned.Id);
            Assert.That(owned.State.hohId, Is.EqualTo(owned.State.playerId));
            Assert.That(owned.State.competitionScores.Any(r => r.contestantId == owned.State.playerId), Is.True);
            Assert.That(row.status, Is.EqualTo(DealStatus.Broken));
            Assert.That(row.brokenById, Is.EqualTo(owned.State.playerId));
            Assert.That(owned.State.Find(owned.Other).nominationWeeks, Does.Contain(row.settledWeek));
            AssertEmission(owned, promise, hearing);
            AcceptedUnchanged(owned.State);
            PublicRefusedUnchanged(owned.State);
        }

        [TestCase(false)] [TestCase(true)]
        public void ActualCompletedVetoProducesItsOwnSparedDealAndDurablePowerEvidence(bool hearing)
        {
            var s = HoHContext(hearing);
            var target = UnprotectedOther(s);
            var owned = WritePolicy(s, false, target);
            var nominees = EpisodeEngine.NominationCandidates(owned.State).Where(c => c.id != target).Take(2).ToArray();
            var command = Command(owned.State, EpisodeCommandKind.Nominate);
            command.targetId = nominees[0].id; command.secondTargetId = nominees[1].id;
            owned.State = Step(owned.State, command);
            owned.State = Reach(owned.State, x => x.phase == EpisodePhase.VetoMeeting, 12);
            owned.State = ResolveCurrentVeto(owned.State, target);
            var row = owned.State.unifiedCommitments.Single(r => r.id == owned.Id);
            Assert.That(row.status, Is.EqualTo(DealStatus.Fulfilled));
            Assert.That(row.settledWeek, Is.EqualTo(owned.State.week));
            Assert.That(owned.State.vetoResolved, Is.True);
            var power = owned.State.ledger.power.Single(p => p.week == row.settledWeek);
            Assert.That(power.hohId, Is.EqualTo(owned.State.playerId));
            Assert.That(power.vetoHolderId, Is.EqualTo(owned.State.vetoHolderId));
            Assert.That(owned.State.Find(target).nominationWeeks, Has.No.Member(row.settledWeek));
            Assert.That(owned.State.story.facts.Where(f => f.kind == FactKinds.BrokenWord && f.refId == row.id), Is.Empty);
            AcceptedUnchanged(owned.State);
        }

        [TestCase(true, false)] [TestCase(true, true)]
        [TestCase(false, false)] [TestCase(false, true)]
        public void ActualVetoReplacementBreakUsesTheCompletedVetoRatherThanAnInjectedRole(bool promise, bool hearing)
        {
            Owned owned = null;
            for (uint seed = 2505; seed < 2537 && owned == null; seed++)
            {
                var s = HoHContext(hearing, seed);
                if (s == null) continue;
                var target = UnprotectedOther(s);
                var attempt = WritePolicy(s, promise, target);
                var nominees = EpisodeEngine.NominationCandidates(attempt.State).Where(c => c.id != target).Take(2).ToArray();
                var nominate = Command(attempt.State, EpisodeCommandKind.Nominate);
                nominate.targetId = nominees[0].id; nominate.secondTargetId = nominees[1].id;
                attempt.State = Step(attempt.State, nominate);
                attempt.State = Reach(attempt.State, x => x.phase == EpisodePhase.VetoMeeting, 12);
                if (attempt.State.vetoHolderId != attempt.State.playerId) continue;
                var veto = Command(attempt.State, EpisodeCommandKind.ResolveVeto);
                veto.useVeto = true; veto.targetId = attempt.State.nominees[0]; veto.secondTargetId = target;
                attempt.State = Step(attempt.State, veto);
                owned = attempt;
            }
            Assert.That(owned, Is.Not.Null, "A bounded real-seed search must produce a genuinely player-won veto; never stage its role.");
            var row = owned.State.unifiedCommitments.Single(r => r.id == owned.Id);
            var power = owned.State.ledger.power.Single(p => p.week == row.settledWeek);
            Assert.That(power.vetoUsed, Is.True);
            Assert.That(power.replacementId, Is.EqualTo(owned.Other));
            Assert.That(row.settlementEffectKey, Does.Contain("replacement"));
            AssertEmission(owned, promise, hearing);
            AcceptedUnchanged(owned.State);
        }

        [TestCase(true, false)] [TestCase(true, true)]
        [TestCase(false, false)] [TestCase(false, true)]
        public void ActualWeekTurnPreservesHistoricalBreachEvidenceWhenTheCurrentHoHChanges(bool promise, bool hearing)
        {
            var owned = NominationBreach(promise, hearing);
            int settled = owned.State.unifiedCommitments.Single(r => r.id == owned.Id).settledWeek;
            owned.State = Reach(owned.State, x => x.week > settled, 80);
            var power = owned.State.ledger.power.Single(p => p.week == settled);
            Assert.That(power.hohId, Is.EqualTo(owned.State.playerId));
            Assert.That(owned.State.hohId, Is.Null, "The actual week-turn owner clears the current role.");
            Assert.That(owned.State.unifiedCommitments.Single(r => r.id == owned.Id).settledWeek, Is.EqualTo(settled));
            AcceptedUnchanged(owned.State);
        }

        [TestCase("null-input")] [TestCase("schema")] [TestCase("canonical-off")]
        [TestCase("canonical-unknown")] [TestCase("hearing-unknown")]
        [TestCase("canonical-null")] [TestCase("canonical-null-row")] [TestCase("canonical-policy")]
        [TestCase("evidence-null")] [TestCase("receipts-null")] [TestCase("commitments-off")]
        [TestCase("cast-null")] [TestCase("cast-null-row")] [TestCase("cast-duplicate")]
        [TestCase("player-unknown")] [TestCase("stats-null")] [TestCase("mood")]
        [TestCase("relationships-null")] [TestCase("relationship-null-row")] [TestCase("relationship-nan")]
        [TestCase("promises-null")] [TestCase("deals-null")] [TestCase("events-null")]
        [TestCase("event-null-row")] [TestCase("event-unconsumed")] [TestCase("event-audience")]
        [TestCase("ledger-null")] [TestCase("power-null")] [TestCase("opportunities-null")]
        [TestCase("story-null")] [TestCase("facts-null")] [TestCase("house-events-null")]
        [TestCase("window-counts-null")] [TestCase("phase")] [TestCase("week")]
        public void SharedWholeCoreRejectsMalformedStateWithoutClearingFieldsOrThrowing(string defect)
        {
            // An unsettled ordinary source promise is legal before C0. This particular negative
            // disables the authority required by an actual completed nomination settlement.
            var owned = defect == "commitments-off" ? NominationBreach(true, false)
                : WritePolicy(Fresh(2505, false), true);
            var s = owned.State;
            AcceptedUnchanged(s);
            switch (defect)
            {
                case "null-input": s = null; break;
                case "schema": s.schemaVersion = 24; break;
                case "canonical-off": s.unifiedCommitmentRulesVersion = 0; break;
                case "canonical-unknown": s.unifiedCommitmentRulesVersion = 2; break;
                case "hearing-unknown": s.unifiedHearingRulesVersion = 2; break;
                case "canonical-null": s.unifiedCommitments = null; break;
                case "canonical-null-row": s.unifiedCommitments.Add(null); break;
                case "canonical-policy": s.unifiedCommitments[0].sourcePolicy = "invented"; break;
                case "evidence-null": s.unifiedHearingEvidence = null; break;
                case "receipts-null": s.unifiedHearingReceipts = null; break;
                case "commitments-off": s.commitmentRulesStartWeek = 0; break;
                case "cast-null": s.contestants = null; break;
                case "cast-null-row": s.contestants.Add(null); break;
                case "cast-duplicate": s.contestants.Add(s.contestants[0].Clone()); break;
                case "player-unknown": s.playerId = "not-in-this-cast"; break;
                case "stats-null": s.contestants[0].stats = null; break;
                case "mood": s.contestants[0].mood = "Unsupported"; break;
                case "relationships-null": s.relationships = null; break;
                case "relationship-null-row": s.relationships.Add(null); break;
                case "relationship-nan": s.relationships[0].score = double.NaN; break;
                case "promises-null": s.promises = null; break;
                case "deals-null": s.deals = null; break;
                case "events-null": s.events = null; break;
                case "event-null-row": s.events.Add(null); break;
                case "event-unconsumed": s.events[0].sequence = s.nextSequence; break;
                case "event-audience": s.events[0].audienceIds.Add("not-in-this-cast"); break;
                case "ledger-null": s.ledger = null; break;
                case "power-null": s.ledger.power = null; break;
                case "opportunities-null": s.ledger.opportunities = null; break;
                case "story-null": s.story = null; break;
                case "facts-null": s.story.facts = null; break;
                case "house-events-null": s.houseEvents = null; break;
                case "window-counts-null": s.windowActions = null; break;
                case "phase": s.phase = (EpisodePhase)999; break;
                case "week": s.week = 101; break;
                default: Assert.Fail("Unknown corruption control: " + defect); break;
            }
            RefusedUnchanged(s);
        }

        [TestCase(true)] [TestCase(false)]
        public void AReadProjectionDeliberatelyInsertedAsARawMirrorCannotPassWholeValidation(bool promise)
        {
            var owned = WritePolicy(Fresh(2505, false), promise);
            AcceptedUnchanged(owned.State);
            if (promise) owned.State.promises.Add(CommitmentReferences.FindPromise(owned.State, owned.Id));
            else owned.State.deals.Add(CommitmentReferences.FindDeal(owned.State, owned.Id));
            RefusedUnchanged(owned.State);
        }

        [TestCase(false)] [TestCase(true)]
        public void ActualPlayerDealOpportunityCanLagUntilTheRealRevealReconcilesIt(bool hearing)
        {
            var owned = PlayerDeal(hearing);
            var chance = owned.State.ledger.opportunities.Single(o => o.id == owned.Id);
            Assert.That(chance.response, Is.EqualTo(OpportunityResponse.Taken));
            Assert.That(chance.outcome, Is.EqualTo(OpportunityOutcome.NotApplicable));
            Assert.That(chance.source, Is.Null);
            Assert.That(chance.note, Is.Null);
            AcceptedUnchanged(owned.State);
            owned.State = NominateOwned(owned);
            chance = owned.State.ledger.opportunities.Single(o => o.id == owned.Id);
            Assert.That(owned.State.unifiedCommitments.Single(r => r.id == owned.Id).status, Is.EqualTo(DealStatus.Broken));
            Assert.That(chance.outcome, Is.EqualTo(OpportunityOutcome.NotApplicable), "Nomination settles the deal, not the delayed opportunity summary.");
            AcceptedUnchanged(owned.State);
            owned.State = Reach(owned.State, x => x.phase == EpisodePhase.Eviction && x.evictionResolved, 60);
            chance = owned.State.ledger.opportunities.Single(o => o.id == owned.Id);
            Assert.That(chance.response, Is.EqualTo(OpportunityResponse.Taken));
            Assert.That(chance.outcome, Is.EqualTo(OpportunityOutcome.Lost));
            Assert.That(chance.source, Is.EqualTo(DealKind.SafetyAgreement));
            AcceptedUnchanged(owned.State);
        }

        [TestCase("kind")] [TestCase("week")] [TestCase("source")]
        [TestCase("target-source")] [TestCase("won-active")] [TestCase("lost-active")]
        [TestCase("part")] [TestCase("promise-family")]
        public void SavedCanonicalOpportunitiesMustKeepTheirActualFamilyAndMeaning(string defect)
        {
            var owned = PlayerDeal(false);
            if (defect == "promise-family")
            {
                var promise = WritePolicy(owned.State, true);
                owned.State = promise.State;
                owned.State.ledger.opportunities.Single(o => o.id == owned.Id).id = promise.Id;
            }
            else
            {
                var chance = owned.State.ledger.opportunities.Single(o => o.id == owned.Id);
                switch (defect)
                {
                    case "kind": chance.kind = OpportunityKinds.Read; break;
                    case "week": chance.week = owned.State.week + 1; break;
                    case "source": chance.source = DealKind.FinalTwo; break;
                    case "target-source": chance.source = DealKind.SafetyAgreement + ":" + owned.Other; break;
                    case "won-active": chance.outcome = OpportunityOutcome.Won; break;
                    case "lost-active": chance.outcome = OpportunityOutcome.Lost; break;
                    case "part": chance.outcome = OpportunityOutcome.Part; break;
                    default: Assert.Fail("Unknown opportunity corruption: " + defect); break;
                }
            }
            RefusedUnchanged(owned.State);
        }

        [TestCase("kind")] [TestCase("actor")] [TestCase("subject")]
        [TestCase("before-settlement")] [TestCase("future")] [TestCase("fact-sequence")]
        [TestCase("private")] [TestCase("actor-audience")] [TestCase("subject-audience")]
        [TestCase("duplicate-leaf")] [TestCase("promise-ref")] [TestCase("removed-owner")]
        public void ActualLiveBrokenWordCannotLoseItsCanonicalProvenanceEvenWithHearingDisabled(string defect)
        {
            var owned = NominationBreach(false, false);
            var s = owned.State;
            var fact = s.story.facts.Single(f => f.kind == FactKinds.BrokenWord && f.refId == owned.Id);
            switch (defect)
            {
                case "kind": fact.kind = FactKinds.Secret; break;
                case "actor": fact.actorId = owned.Other; break;
                case "subject": fact.subjectId = s.Active.First(c => c.id != s.playerId && c.id != owned.Other).id; break;
                case "before-settlement": fact.week = s.week - 1; break;
                case "future": fact.week = s.week + 1; break;
                case "fact-sequence": fact.id = "fact-" + s.nextSequence; break;
                case "private": fact.visibility = FactVisibility.Private; break;
                case "actor-audience": fact.knowers.Remove(s.playerId); break;
                case "subject-audience": fact.knowers.Remove(owned.Other); break;
                case "duplicate-leaf": s.story.facts.Add(fact.Clone()); break;
                case "promise-ref":
                    var promise = WritePolicy(s, true);
                    s = promise.State;
                    s.story.facts.Single(f => f.id == fact.id).refId = promise.Id;
                    break;
                case "removed-owner": s.unifiedCommitments.RemoveAll(r => r.id == owned.Id); break;
                default: Assert.Fail("Unknown fact corruption: " + defect); break;
            }
            RefusedUnchanged(s);
        }

        [TestCase("archive-and-initial")] [TestCase("initial")]
        [TestCase("initial-as-spread")] [TestCase("archive")]
        [TestCase("archive-identity")]
        public void SurvivingActualSelectedDealEmissionRequiresItsAtomicArchiveAndInitial(string defect)
        {
            var owned = NominationBreach(false, true);
            AssertEmission(owned, false, true);
            var s = owned.State;
            var fact = s.story.facts.Single(f => f.kind == FactKinds.BrokenWord && f.refId == owned.Id);
            var archive = s.unifiedHearingEvidence.Single(e => e.fact.id == fact.id);
            var initial = s.unifiedHearingReceipts.Single(r => r.factId == fact.id);
            Assert.That(initial.heardWeek, Is.EqualTo(s.unifiedCommitments.Single(r => r.id == owned.Id).settledWeek));
            Assert.That(archive.fact.knowers, Is.EqualTo(fact.knowers));
            AcceptedUnchanged(s);
            switch (defect)
            {
                case "archive-and-initial":
                    s.unifiedHearingEvidence.Remove(archive);
                    s.unifiedHearingReceipts.Remove(initial);
                    break;
                case "initial": s.unifiedHearingReceipts.Remove(initial); break;
                case "initial-as-spread": initial.kind = UnifiedCommitmentHearings.Spread; break;
                case "archive": s.unifiedHearingEvidence.Remove(archive); break;
                case "archive-identity": archive.fact.id = "fact-" + s.nextSequence; break;
                default: Assert.Fail("Unknown atomic-emission corruption: " + defect); break;
            }
            if (defect == "archive-and-initial" || defect == "initial" || defect == "initial-as-spread")
                Assert.That(UnifiedCommitmentHearings.ValidateStorage(s, out string storageError), Is.True, storageError,
                    "The hearing writer's intermediate storage validator is intentionally weaker than completed whole-state validation.");
            Assert.That(s.story.facts.Single(f => f.id == fact.id), Is.SameAs(fact), "The actual live source fact still survives.");
            RefusedUnchanged(s);
        }

        [TestCase("initial")] [TestCase("initial-as-spread")]
        [TestCase("actor-audience")] [TestCase("subject-audience")]
        public void RetainedArchiveProjectionAfterLiveFactPruningCannotLoseItsInitialOrSeedParties(string defect)
        {
            var owned = NominationBreach(false, true);
            AssertEmission(owned, false, true);
            var emitted = owned.State.story.facts.Single(f => f.kind == FactKinds.BrokenWord && f.refId == owned.Id);
            string original = Json(owned.State);
            var s = owned.State.Clone();
            // Compatibility projection only: the genuine Knowledge.BrokenWord -> RecordInitial
            // writer has already run. Omitting its live copy models a retained archived save; it
            // does not claim the 128-kept-fact capacity branch ran in a normally played season.
            Assert.That(s.story.facts.RemoveAll(f => f.id == emitted.id), Is.EqualTo(1));
            Assert.That(Json(owned.State), Is.EqualTo(original));
            Assert.That(s.unifiedHearingEvidence.Single(e => e.fact.id == emitted.id).fact.refId, Is.EqualTo(owned.Id));
            AcceptedUnchanged(s);
            var archive = s.unifiedHearingEvidence.Single(e => e.fact.id == emitted.id);
            var initial = s.unifiedHearingReceipts.Single(r => r.factId == emitted.id);
            switch (defect)
            {
                case "initial": s.unifiedHearingReceipts.Remove(initial); break;
                case "initial-as-spread": initial.kind = UnifiedCommitmentHearings.Spread; break;
                case "actor-audience": archive.fact.knowers.Remove(s.playerId); break;
                case "subject-audience": archive.fact.knowers.Remove(owned.Other); break;
                default: Assert.Fail("Unknown retained-archive corruption: " + defect); break;
            }
            if (defect == "initial" || defect == "initial-as-spread")
                Assert.That(UnifiedCommitmentHearings.ValidateStorage(s, out string storageError), Is.True, storageError);
            Assert.That(s.story.facts.Any(f => f.id == emitted.id), Is.False);
            RefusedUnchanged(s);
        }

        [TestCase(false, false)] [TestCase(false, true)]
        [TestCase(true, false)] [TestCase(true, true)]
        public void ActualSelectedDealIncidentCannotEraseAllDurableHearingLineage(bool historical, bool prunedProjection)
        {
            var owned = NominationBreach(false, true);
            AssertEmission(owned, false, true);
            var emitted = owned.State.story.facts.Single(f => f.kind == FactKinds.BrokenWord && f.refId == owned.Id);
            int settled = owned.State.unifiedCommitments.Single(r => r.id == owned.Id).settledWeek;
            if (historical) owned.State = Reach(owned.State, x => x.week > settled, 80);
            AcceptedUnchanged(owned.State);
            string original = Json(owned.State);
            var s = owned.State.Clone();
            if (prunedProjection)
            {
                // Detached retained-archive projection after the real initial-emission writer;
                // this is not a claim that the source's capacity-pruner branch executed.
                Assert.That(s.story.facts.RemoveAll(f => f.id == emitted.id), Is.EqualTo(1));
                AcceptedUnchanged(s);
            }
            Assert.That(Json(owned.State), Is.EqualTo(original));
            var owner = s.unifiedCommitments.Single(r => r.id == owned.Id);
            Assert.That(owner.sourcePolicy, Is.EqualTo(UnifiedCommitments.DealPolicy));
            Assert.That(owner.status, Is.EqualTo(DealStatus.Broken));
            Assert.That(owner.brokenById, Is.EqualTo(s.playerId));
            Assert.That(owner.settledWeek, Is.EqualTo(settled));
            string retainedOwner = JsonConvert.SerializeObject(owner);
            var incident = UnifiedCommitmentHistory.Breaches(s).Single(i => i.EffectOwnerId == owned.Id);
            Assert.That(s.unifiedHearingEvidence.Any(e => e.incidentKey == incident.EffectKey && e.fact.id == emitted.id), Is.True);
            Assert.That(s.unifiedHearingReceipts.Any(r => r.incidentKey == incident.EffectKey && r.factId == emitted.id
                && r.kind == UnifiedCommitmentHearings.Initial && r.listenerId == owned.Other && r.heardWeek == settled), Is.True);
            Assert.That(s.story.facts.RemoveAll(f => f.id == emitted.id), Is.EqualTo(prunedProjection ? 0 : 1));
            Assert.That(s.unifiedHearingEvidence.RemoveAll(e => e.incidentKey == incident.EffectKey), Is.GreaterThan(0));
            Assert.That(s.unifiedHearingReceipts.RemoveAll(r => r.incidentKey == incident.EffectKey), Is.GreaterThan(0));
            Assert.That(UnifiedCommitmentHearings.ValidateStorage(s, out string storageError), Is.True, storageError,
                "Empty intermediate hearing storage is not proof that a completed source-owned incident never emitted.");
            Assert.That(JsonConvert.SerializeObject(s.unifiedCommitments.Single(r => r.id == owned.Id)), Is.EqualTo(retainedOwner));
            Assert.That(s.unifiedHearingRulesVersion, Is.EqualTo(UnifiedCommitmentHearings.ProspectiveVersion));
            RefusedUnchanged(s);
        }

        [TestCase(false, false)] [TestCase(true, false)] [TestCase(true, true)]
        public void ActualHistoricalEmissionCannotMoveItsStoryBoundaryPastSettlement(bool hearing, bool archiveOnly)
        {
            var owned = NominationBreach(false, hearing);
            AssertEmission(owned, false, hearing);
            var emitted = owned.State.story.facts.Single(f => f.kind == FactKinds.BrokenWord && f.refId == owned.Id);
            int settled = owned.State.unifiedCommitments.Single(r => r.id == owned.Id).settledWeek;
            owned.State = Reach(owned.State, x => x.week > settled, 80);
            AcceptedUnchanged(owned.State);
            string original = Json(owned.State);
            var s = owned.State.Clone();
            if (archiveOnly)
            {
                // Retained-archive projection after a genuine emission, not a capacity-pruner run.
                Assert.That(s.story.facts.RemoveAll(f => f.id == emitted.id), Is.EqualTo(1));
                Assert.That(s.unifiedHearingEvidence.Any(e => e.fact.id == emitted.id), Is.True);
                AcceptedUnchanged(s);
            }
            Assert.That(Json(owned.State), Is.EqualTo(original));
            Assert.That(s.story.rulesStartWeek, Is.LessThanOrEqualTo(settled));
            // Only the recorded boundary is corrupted. Today's reader stays on, but the
            // genuine fact could not have been emitted under this later historical boundary.
            s.story.rulesStartWeek = settled + 1;
            Assert.That(s.story.rulesStartWeek, Is.LessThanOrEqualTo(s.week));
            Assert.That(YourWord.On(s), Is.True);
            Assert.That(UnifiedSafetySaveReferences.TryValidate(s, out string referenceError), Is.True, referenceError);
            Assert.That(UnifiedCommitmentHearings.ValidateStorage(s, out string storageError), Is.True, storageError);
            RefusedUnchanged(s);
        }

        [Test]
        public void ActualPreStoryBoundaryBreachDoesNotAcquireFactsWhenKnowledgeLaterStarts()
        {
            var s = HoHContext(false);
            int storyBegins = s.week + 1;
            // Use the real boundary setter before the source deal and nomination writers;
            // do not switch hearing rules or erase an already emitted fact to fake this control.
            EpisodeEngine.EnableStory(s, storyBegins);
            Assert.That(YourWord.On(s), Is.False);
            AcceptedUnchanged(s);
            var owned = WritePolicy(s, false);
            owned.State = NominateOwned(owned);
            var row = owned.State.unifiedCommitments.Single(r => r.id == owned.Id);
            Assert.That(row.status, Is.EqualTo(DealStatus.Broken));
            Assert.That(row.brokenById, Is.EqualTo(owned.State.playerId));
            Assert.That(row.settledWeek, Is.LessThan(storyBegins));
            Assert.That(owned.State.Find(owned.Other).nominationWeeks, Does.Contain(row.settledWeek));
            Assert.That(YourWord.On(owned.State), Is.False);
            Assert.That(owned.State.story.facts.Any(f => f.kind == FactKinds.BrokenWord && f.refId == owned.Id), Is.False);
            Assert.That(owned.State.unifiedHearingEvidence, Is.Empty);
            Assert.That(owned.State.unifiedHearingReceipts, Is.Empty);
            AcceptedUnchanged(owned.State);
            string permanentOwner = JsonConvert.SerializeObject(row);
            owned.State = Reach(owned.State, x => x.week >= storyBegins, 80);
            Assert.That(YourWord.On(owned.State), Is.True);
            Assert.That(owned.State.story.rulesStartWeek, Is.EqualTo(storyBegins));
            Assert.That(owned.State.unifiedHearingRulesVersion, Is.Zero);
            Assert.That(JsonConvert.SerializeObject(owned.State.unifiedCommitments.Single(r => r.id == owned.Id)), Is.EqualTo(permanentOwner));
            Assert.That(owned.State.ledger.power.Single(p => p.week == row.settledWeek).hohId, Is.EqualTo(owned.State.playerId));
            Assert.That(owned.State.story.facts.Any(f => f.kind == FactKinds.BrokenWord && f.refId == owned.Id), Is.False);
            Assert.That(owned.State.unifiedHearingEvidence, Is.Empty);
            Assert.That(owned.State.unifiedHearingReceipts, Is.Empty);
            AcceptedUnchanged(owned.State);
        }

        [Test]
        public void DiagnosticActualWeakerDealObservationDoesNotAcquireTheSelectedOwnersInitial()
        {
            EpisodeState observed = null;
            string aliasId = null, selectedDealId = null, promiseId = null, listener = null;
            for (uint seed = 2505; seed < 2537 && observed == null; seed++)
            {
                var s = HoHContext(true, seed);
                if (s == null) continue;
                var promise = WritePolicy(s, true);
                var alias = WritePolicy(promise.State, false, promise.Other);
                var selectedDeal = WritePolicy(alias.State, false);
                var nominate = Command(selectedDeal.State, EpisodeCommandKind.Nominate);
                nominate.targetId = promise.Other; nominate.secondTargetId = selectedDeal.Other;
                s = Step(selectedDeal.State, nominate);
                var grouped = UnifiedCommitmentHistory.Breaches(s).Single(i => i.EvidenceIds.Contains(alias.Id));
                Assert.That(grouped.EffectOwnerId, Is.EqualTo(promise.Id));
                Assert.That(grouped.EvidenceIds, Does.Contain(promise.Id));
                Assert.That(s.story.facts.Any(f => f.refId == promise.Id || f.refId == alias.Id), Is.False,
                    "The real grouped nomination selected PromisePolicy and emitted neither a Promise nor weaker Deal fact.");
                AssertEmission(new Owned { State = s, Id = selectedDeal.Id, Other = selectedDeal.Other }, false, true);
                // Bounded observation diagnostic, not an ordinary gateway emission: the actual
                // installed private fact core creates a real weaker Deal leaf. Its source identity,
                // seeded audience and date are not fabricated and hearing flags never change.
                var audible = (HouseFactState)Call(typeof(Knowledge), "BrokenWordCore", s,
                    CommitmentReferences.FindDeal(s, alias.Id), s.playerId, promise.Other);
                Assert.That(audible.refId, Is.EqualTo(alias.Id));
                Assert.That(s.unifiedHearingEvidence.Any(e => e.fact.id == audible.id), Is.False);
                AcceptedUnchanged(s);
                foreach (string anchor in StoryAnchors.All.Where(a => a != StoryAnchors.Conversation))
                {
                    var attempt = s.Clone();
                    string source = Json(s);
                    var told = Knowledge.Spread(attempt, anchor);
                    // Preserve the real owner's ordering: the whole gossip pass finishes before
                    // any HeardOfYourWord relationship/arc effects are applied.
                    foreach (var heard in told.Where(t => t.listener != attempt.playerId && YourWord.IsYours(attempt, t.fact)))
                        Call(typeof(EpisodeEngine), "HeardOfYourWord", attempt, heard.fact, heard.listener);
                    Assert.That(Json(s), Is.EqualTo(source));
                    var actualAliasHearing = told.FirstOrDefault(t => t.fact.refId == alias.Id && t.listener != attempt.playerId);
                    if (actualAliasHearing.fact == null) continue;
                    observed = attempt; aliasId = alias.Id; selectedDealId = selectedDeal.Id;
                    promiseId = promise.Id; listener = actualAliasHearing.listener;
                    break;
                }
            }
            Assert.That(observed, Is.Not.Null, "A bounded real-seed/real-anchor search must produce a genuine observed weaker Deal leaf.");
            var incident = UnifiedCommitmentHistory.Breaches(observed).Single(i => i.EvidenceIds.Contains(aliasId));
            var weakArchive = observed.unifiedHearingEvidence.Single(e => e.fact.refId == aliasId);
            Assert.That(incident.EffectOwnerId, Is.EqualTo(promiseId));
            Assert.That(weakArchive.incidentKey, Is.EqualTo(incident.EffectKey));
            Assert.That(observed.unifiedHearingReceipts.Single(r => r.incidentKey == incident.EffectKey && r.listenerId == listener).kind,
                Is.EqualTo(UnifiedCommitmentHearings.Spread));
            Assert.That(observed.unifiedHearingReceipts.Any(r => r.incidentKey == incident.EffectKey && r.kind == UnifiedCommitmentHearings.Initial), Is.False);
            Assert.That(observed.unifiedHearingEvidence.Any(e => e.fact.refId == promiseId), Is.False);
            var realSelectedFact = observed.story.facts.Single(f => f.refId == selectedDealId && f.kind == FactKinds.BrokenWord);
            Assert.That(observed.unifiedHearingEvidence.Single(e => e.fact.id == realSelectedFact.id).fact.refId, Is.EqualTo(selectedDealId));
            Assert.That(observed.unifiedHearingReceipts.Single(r => r.factId == realSelectedFact.id && r.kind == UnifiedCommitmentHearings.Initial).listenerId,
                Is.EqualTo(realSelectedFact.subjectId));
            AcceptedUnchanged(observed);
            PublicRefusedUnchanged(observed);
        }

        [TestCase("reverse-promise")] [TestCase("npc-pair")]
        [TestCase("deal-as-promise")] [TestCase("promise-as-deal")] [TestCase("unsettled-accountability")]
        public void SavedJuryQuestionCannotClaimAnUnrelatedOrWrongFamilyCanonicalReceipt(string defect)
        {
            var s = JuryContext(false);
            var q = s.juryExchanges[s.juryQuestionIndex];
            string juror = q.questionerId;
            var reverse = s.unifiedCommitments.Single(r => r.origin == UnifiedCommitments.StoryPromise && r.makerId == juror && r.beneficiaryId == s.playerId);
            var deal = s.unifiedCommitments.Single(r => r.origin == UnifiedCommitments.StoryDeal && r.makerId == s.playerId && r.beneficiaryId == juror);
            var npcPair = s.unifiedCommitments.Single(r => r.origin == UnifiedCommitments.NpcPromise);
            q.category = FinaleQuestions.Accountability;
            q.tone = FinaleQuestions.Tone(q.category);
            q.question = FinaleQuestions.Questions(q.category)[0];
            q.receiptKind = defect == "promise-as-deal" || defect == "unsettled-accountability"
                ? FinaleQuestions.DealReceipt : FinaleQuestions.PromiseReceipt;
            q.receiptId = defect == "npc-pair" ? npcPair.id
                : defect == "deal-as-promise" || defect == "unsettled-accountability" ? deal.id : reverse.id;
            RefusedUnchanged(s);
        }

        [Test]
        public void DiagnosticRealNominationReceiptCannotClaimItSentTheJurorHome()
        {
            var s = SourceQualifiedQuestionDiagnostic(false, out var captured);
            Assert.That(captured, Is.Not.Null);
            var q = s.juryExchanges[s.juryQuestionIndex];
            var receipt = captured.Receipt;
            Assert.That(captured.JurorId, Is.EqualTo(q.questionerId));
            Assert.That(q.category, Is.EqualTo(receipt.category));
            Assert.That(q.receiptKind, Is.EqualTo(receipt.kind));
            Assert.That(q.receiptId, Is.EqualTo(receipt.id));
            Assert.That(receipt.category, Is.EqualTo(FinaleQuestions.Ownership));
            Assert.That(receipt.kind, Is.EqualTo(FinaleQuestions.PowerReceipt));
            var power = s.ledger.power.Single(p => p.week == receipt.week);
            Assert.That(JsonConvert.SerializeObject(power), Is.EqualTo(JsonConvert.SerializeObject(captured.RevealedPower)),
                "The complete permanent row still equals the actual completed reveal that returned this receipt.");
            Assert.That(power.hohId, Is.EqualTo(s.playerId));
            Assert.That(power.vetoHolderId, Is.Not.Null.And.Not.Empty);
            Assert.That(s.Find(power.vetoHolderId), Is.Not.Null);
            Assert.That(power.nominees, Has.Count.EqualTo(2));
            Assert.That(power.nominees, Does.Contain(q.questionerId));
            Assert.That(power.tally, Has.Count.EqualTo(power.nominees.Count));
            Assert.That(power.evicteeId, Is.Not.EqualTo(q.questionerId));
            Assert.That(s.Find(power.evicteeId).status, Is.EqualTo(ContestantStatus.Jury));
            Assert.That(s.Find(q.questionerId).status, Is.EqualTo(ContestantStatus.Jury));
            Assert.That(q.finalistId, Is.EqualTo(s.playerId));
            Assert.That(s.Find(q.finalistId).status, Is.EqualTo(ContestantStatus.Active));
            Assert.That(q.completed, Is.False);
            Assert.That(FinaleQuestions.ReceiptWeek(s, q), Is.EqualTo(receipt.week));
            Assert.That(FinaleQuestions.QuestionsFor(s, q.category, receipt, q.questionerId),
                Is.EqualTo(new[] { FinaleQuestions.Questions(FinaleQuestions.Ownership)[1] }));
            AcceptedUnchanged(s);
            // Only the overclaim changes. The real receipt remains eligible and source-owned;
            // a question category projected from it is not claimed as the RNG-selected question.
            q.question = FinaleQuestions.Questions(FinaleQuestions.Ownership)[0];
            RefusedUnchanged(s);
        }

        [Test]
        public void DiagnosticRealSharedAllianceReceiptCannotClaimAKeptPromise()
        {
            var s = SourceQualifiedQuestionDiagnostic(true, out _);
            var q = s.juryExchanges[s.juryQuestionIndex];
            var receipt = FinaleQuestions.Receipts(s, q.questionerId).Single(r => r.category == q.category
                && r.kind == q.receiptKind && r.id == q.receiptId);
            Assert.That(receipt.category, Is.EqualTo(FinaleQuestions.Personal));
            Assert.That(receipt.kind, Is.EqualTo(FinaleQuestions.AllianceReceipt));
            var alliance = s.alliances.Single(a => a.id == receipt.id);
            Assert.That(alliance.members, Does.Contain(s.playerId).And.Contain(q.questionerId));
            Assert.That(FinaleQuestions.QuestionsFor(s, q.category, receipt, q.questionerId),
                Is.EqualTo(new[] { FinaleQuestions.Questions(FinaleQuestions.Personal)[1] }));
            AcceptedUnchanged(s);
            q.question = FinaleQuestions.Questions(FinaleQuestions.Personal)[0];
            RefusedUnchanged(s);
        }

        [TestCase("unknown")] [TestCase("deal-as-promise")]
        [TestCase("reverse-promise")] [TestCase("npc-pair")] [TestCase("unsupported-kind")]
        public void LockedActualFinalArgumentStillRequiresTypedOwnRecordReferences(string defect)
        {
            var s = LockActualArgument(JuryContext(false));
            Assert.That(s.finalArgument.momentRefs, Is.Not.Empty);
            var deal = s.unifiedCommitments.First(r => r.origin == UnifiedCommitments.StoryDeal);
            var reverse = s.unifiedCommitments.First(r => r.origin == UnifiedCommitments.StoryPromise);
            var npcPair = s.unifiedCommitments.Single(r => r.origin == UnifiedCommitments.NpcPromise);
            s.finalArgument.momentRefs[0] = defect switch
            {
                "unknown" => "deal:not-an-installed-record",
                "deal-as-promise" => "promise:" + deal.id,
                "reverse-promise" => "promise:" + reverse.id,
                "npc-pair" => "promise:" + npcPair.id,
                "unsupported-kind" => "safety:" + deal.id,
                _ => throw new ArgumentException("Unknown argument corruption: " + defect),
            };
            RefusedUnchanged(s);
        }

        [TestCase(false)] [TestCase(true)]
        public void ActualFinalThreeQuestioningArgumentAndTerminalOwnersKeepAWholeValidState(bool hearing)
        {
            var s = LockActualArgument(JuryContext(hearing));
            var locked = s.finalArgument.momentRefs.ToArray();
            s = Reach(s, x => x.phase == EpisodePhase.Finished, 60);
            Assert.That(s.Active, Is.Empty);
            Assert.That(s.finalArgument.momentRefs, Is.EqualTo(locked));
            Assert.That(s.finalArgument.momentRefs.All(r => FinalArgument.Resolves(s, r)), Is.True);
            Assert.That(s.juryExchanges.All(q => q.completed), Is.True);
            AcceptedUnchanged(s);
            PublicRefusedUnchanged(s);
        }

        [TestCase(true)] [TestCase(false)]
        public void OwnReferenceResolutionIsNotTheCurrentMomentEligibilityTest(bool promise)
        {
            var owned = WritePolicy(Fresh(2505, false), promise);
            string reference = (promise ? "promise:" : "deal:") + owned.Id;
            string before = Json(owned.State);
            Assert.That(FinalArgument.Resolves(owned.State, reference), Is.True);
            Assert.That(FinalArgument.Moments(owned.State).Any(m => m.reference == reference), Is.False);
            Assert.That(Json(owned.State), Is.EqualTo(before));
            // No invented lock: the actual source record resolves while active, but the lock's
            // command would not offer it. A saved-lock provenance check must not become that menu.
            Assert.That(owned.State.finalArgument, Is.Null);
            AcceptedUnchanged(owned.State);
        }

        private sealed class Owned
        {
            public EpisodeState State;
            public string Id, Other;
        }

        private sealed class CapturedOwnershipReceipt
        {
            public string JurorId;
            public FinaleQuestions.Receipt Receipt;
            public PowerRow RevealedPower;
        }

        private static EpisodeState Fresh(uint seed, bool hearing, int size = 6, bool builtRoster = false)
        {
            var s = builtRoster || size != 6
                ? SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed)
                : ContentCatalog.Create(seed);
            s.competitionRulesVersion = CompetitionRules.Current;
            s.haveNotRulesStartWeek = 1; s.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableStory(s); EpisodeEngine.EnableRead(s); EpisodeEngine.EnableLevers(s);
            EpisodeEngine.EnableWeek(s); EpisodeEngine.EnableEconomy(s); EpisodeEngine.EnableAgency(s);
            EpisodeEngine.EnableFinale(s); EpisodeEngine.EnableCommitments(s);
            Assert.That(EpisodeValidation.TryValidate(s, out string initialError), Is.True, initialError);
            Assert.That(s.unifiedCommitmentRulesVersion, Is.Zero);
            Assert.That(s.unifiedCommitments, Is.Empty);
            Assert.That(s.unifiedHearingRulesVersion, Is.Zero);
            Assert.That(s.unifiedHearingEvidence, Is.Empty);
            Assert.That(s.unifiedHearingReceipts, Is.Empty);
            Assert.That(s.promises.Any(p => p.kind == PromiseKind.Safety), Is.False);
            Assert.That(s.deals.Any(d => d.type == DealKind.SafetyAgreement), Is.False);
            s.unifiedCommitmentRulesVersion = UnifiedCommitments.ProspectiveVersion;
            s.unifiedHearingRulesVersion = hearing ? UnifiedCommitmentHearings.ProspectiveVersion : 0;
            AcceptedUnchanged(s);
            return s;
        }

        private static EpisodeState HoHContext(bool hearing, uint? onlySeed = null)
        {
            uint start = onlySeed ?? 2505, end = onlySeed.HasValue ? start + 1 : start + 32;
            for (uint seed = start; seed < end; seed++)
            {
                var s = Fresh(seed, hearing);
                s = Step(s, Command(s, EpisodeCommandKind.Advance));
                Assert.That(s.phase, Is.EqualTo(EpisodePhase.HoH));
                var compete = Command(s, EpisodeCommandKind.Compete); compete.performance = 1;
                s = Step(s, compete);
                if (s.hohId != s.playerId) continue;
                s = Step(s, Command(s, EpisodeCommandKind.Advance));
                Assert.That(s.phase, Is.EqualTo(EpisodePhase.Nomination));
                Assert.That(s.nominees, Is.Empty);
                return s;
            }
            if (onlySeed.HasValue) return null;
            Assert.Fail("No genuine player HoH was found within the bounded seed controls.");
            return null;
        }

        private static string UnprotectedOther(EpisodeState s) => s.Active.First(c => !c.isPlayer
            && UnifiedCommitments.Binding(s, s.playerId, c.id).Count == 0).id;

        private static Owned WritePolicy(EpisodeState s, bool promise, string other = null)
        {
            other = other ?? UnprotectedOther(s);
            var before = new HashSet<string>(s.unifiedCommitments.Select(r => r.id), StringComparer.Ordinal);
            EpisodeState next;
            if (promise)
            {
                var command = Command(s, EpisodeCommandKind.PromiseSafety); command.targetId = other;
                next = Step(s, command);
            }
            else
            {
                next = s.Clone();
                Call(typeof(EpisodeEngine), "StoryDeal", next, next.Find(next.playerId), next.Find(other), null, DealKind.SafetyAgreement);
                AcceptedUnchanged(next);
            }
            var row = next.unifiedCommitments.Single(r => !before.Contains(r.id)
                && r.sourcePolicy == (promise ? UnifiedCommitments.PromisePolicy : UnifiedCommitments.DealPolicy)
                && r.makerId == next.playerId && r.beneficiaryId == other);
            return new Owned { State = next, Id = row.id, Other = other };
        }

        private static Owned NominationBreach(bool promise, bool hearing)
        {
            var owned = WritePolicy(HoHContext(hearing), promise);
            owned.State = NominateOwned(owned);
            return owned;
        }

        private static EpisodeState NominateOwned(Owned owned)
        {
            var command = Command(owned.State, EpisodeCommandKind.Nominate);
            command.targetId = owned.Other;
            command.secondTargetId = EpisodeEngine.NominationCandidates(owned.State).First(c => c.id != owned.Other).id;
            return Step(owned.State, command);
        }

        private static Owned PlayerDeal(bool hearing)
        {
            for (uint seed = 2505; seed < 2537; seed++)
            {
                var s = HoHContext(hearing, seed);
                if (s == null) continue;
                string other = UnprotectedOther(s);
                var existing = new HashSet<string>(s.unifiedCommitments.Select(r => r.id), StringComparer.Ordinal);
                var command = Command(s, EpisodeCommandKind.ProposeDeal);
                command.targetId = other; command.text = DealKind.SafetyAgreement;
                s = Step(s, command);
                var row = s.unifiedCommitments.SingleOrDefault(r => !existing.Contains(r.id) && r.origin == UnifiedCommitments.PlayerDeal);
                if (row != null) return new Owned { State = s, Id = row.id, Other = other };
            }
            Assert.Fail("A bounded real-seed search must produce an actual accepted player proposal.");
            return null;
        }

        private static EpisodeState ResolveCurrentVeto(EpisodeState s, string protectedId = null)
        {
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.VetoMeeting));
            Assert.That(s.vetoResolved, Is.False);
            var command = Next(s, protectedId);
            var next = Step(s, command);
            Assert.That(next.vetoResolved, Is.True);
            return next;
        }

        private static EpisodeState JuryContext(bool hearing)
        {
            for (uint seed = 2505; seed < 2537; seed++)
            {
                var s = Fresh(seed, hearing, 3, true);
                var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToArray();
                Call(typeof(NpcPromises), "Give", s, npcs[0], npcs[1], PromiseKind.Safety);
                foreach (string npc in npcs)
                {
                    Call(typeof(EpisodeEngine), "StoryPromise", s, s.Find(npc), s.Find(s.playerId), "Safety");
                    Call(typeof(EpisodeEngine), "StoryDeal", s, s.Find(s.playerId), s.Find(npc), null, DealKind.SafetyAgreement);
                }
                AcceptedUnchanged(s);
                s = Reach(s, x => x.phase == EpisodePhase.JuryQuestioning, 50);
                if (s.Active.Any(c => c.isPlayer)) return s;
            }
            Assert.Fail("A real three-person season must reach the player-finalist branch in the bounded seed controls.");
            return null;
        }

        private static EpisodeState SourceQualifiedQuestionDiagnostic(bool personal, out CapturedOwnershipReceipt captured)
        {
            captured = null;
            for (uint seed = 2505; seed < 2537; seed++)
            {
                var s = Fresh(seed, false, personal ? 3 : 6, personal);
                var ownership = new List<CapturedOwnershipReceipt>();
                if (personal)
                {
                    // The actual three-person story-pact writer, not a synthetic saved alliance
                    // or a forced trust/consent roll. Its own ledger/fact writers retain lineage.
                    var npcs = s.Active.Where(c => !c.isPlayer).ToArray();
                    Call(typeof(EpisodeEngine), "StoryAlliance", s, s.Find(s.playerId), npcs[0], npcs[1], "whole-wording-story-pact");
                    Assert.That(s.alliances.Any(a => a.active && a.members.Contains(s.playerId)
                        && npcs.All(n => a.members.Contains(n.id))), Is.True);
                    AcceptedUnchanged(s);
                }
                if (personal) s = Reach(s, x => x.phase == EpisodePhase.JuryQuestioning, 512);
                else
                {
                    // Retain an actual source-returned receipt after a real reveal, before a
                    // later ceremony can become the menu's latest. It remains a qualified
                    // saved reference, not a claim about today's menu or RNG-selected question.
                    for (int step = 0; step < 512 && s.phase != EpisodePhase.JuryQuestioning; step++)
                    {
                        var before = s;
                        var command = Next(s);
                        // This diagnostic preserves a real finalist path by using an actually held
                        // legal veto on the player nominee, rather than Next's deliberate decline.
                        // The NPC HoH's source writer still chooses the replacement; no result,
                        // role, history, RNG, receipt, or question is forced by this policy.
                        if (s.pendingDiary == null && s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved
                            && s.vetoHolderId == s.playerId && s.nominees.Contains(s.playerId)
                            && !EpisodeEngine.VetoIsLockedAtFinalFour(s) && EpisodeEngine.ReplacementCandidates(s).Any())
                        {
                            command.useVeto = true; command.targetId = s.playerId; command.secondTargetId = null;
                        }
                        s = Step(s, command);
                        CaptureRealOwnershipReceipt(before, s, ownership);
                    }
                    Assert.That(s.phase, Is.EqualTo(EpisodePhase.JuryQuestioning),
                        "Actual source progression did not reach the requested phase within its bound.");
                }
                if (!s.Active.Any(c => c.isPlayer)) continue;
                for (int step = 0; step < 32 && s.phase == EpisodePhase.JuryQuestioning; step++)
                {
                    var q = s.juryExchanges[s.juryQuestionIndex];
                    if (!q.completed && q.finalistId == s.playerId)
                    {
                        var witness = personal ? null : ownership.FirstOrDefault(r => r.JurorId == q.questionerId);
                        var receipt = personal
                            ? FinaleQuestions.Receipts(s, q.questionerId).FirstOrDefault(r => r.category == FinaleQuestions.Personal
                                && r.kind == FinaleQuestions.AllianceReceipt)
                            : witness?.Receipt;
                        if (receipt != null)
                        {
                            string original = Json(s);
                            var diagnostic = s.Clone();
                            var saved = diagnostic.juryExchanges[diagnostic.juryQuestionIndex];
                            // Detached semantic saved-question projection from an actually eligible
                            // receipt. This does not force the juror's theme/RNG selection, status,
                            // phase, power record, question order or source commitment authority.
                            saved.category = receipt.category; saved.receiptKind = receipt.kind;
                            saved.receiptId = receipt.id; saved.tone = FinaleQuestions.Tone(receipt.category);
                            saved.question = FinaleQuestions.QuestionsFor(diagnostic, receipt.category, receipt, saved.questionerId)[0];
                            Assert.That(Json(s), Is.EqualTo(original));
                            AcceptedUnchanged(diagnostic);
                            captured = witness;
                            return diagnostic;
                        }
                    }
                    s = Step(s, Next(s));
                }
            }
            Assert.Fail("A bounded actual-seed/source-phase search must produce the requested real eligible jury receipt.");
            return null;
        }

        private static void CaptureRealOwnershipReceipt(EpisodeState before, EpisodeState revealed,
            List<CapturedOwnershipReceipt> captures)
        {
            if (before.phase != EpisodePhase.Eviction || before.evictionResolved || !revealed.evictionResolved
                || revealed.evictionStage != EvictionStage.Results) return;
            var power = revealed.ledger.power.Single(p => p.week == revealed.week);
            if (power.hohId != revealed.playerId) return;
            Assert.That(revealed.vetoResolved, Is.True);
            Assert.That(power.hohId, Is.EqualTo(revealed.hohId));
            Assert.That(power.vetoHolderId, Is.EqualTo(revealed.vetoHolderId));
            Assert.That(power.nominees, Is.EqualTo(revealed.nominees));
            Assert.That(power.nominees, Has.Count.EqualTo(2));
            Assert.That(power.tally, Has.Count.EqualTo(power.nominees.Count));
            Assert.That(power.nominees, Does.Contain(power.evicteeId));
            Assert.That(revealed.Find(power.evicteeId).status, Is.EqualTo(ContestantStatus.Jury));
            string survivor = power.nominees.Single(id => id != power.evicteeId);
            Assert.That(revealed.Find(survivor).status, Is.EqualTo(ContestantStatus.Active));
            Assert.That(survivor, Is.Not.EqualTo(revealed.playerId));
            string unchanged = Json(revealed);
            var receipt = FinaleQuestions.Receipts(revealed, survivor).Single(r => r.category == FinaleQuestions.Ownership
                && r.kind == FinaleQuestions.PowerReceipt && r.week == power.week);
            Assert.That(Json(revealed), Is.EqualTo(unchanged));
            captures.Add(new CapturedOwnershipReceipt { JurorId = survivor, Receipt = receipt, RevealedPower = power.Clone() });
        }

        private static EpisodeState LockActualArgument(EpisodeState s)
        {
            var command = Command(s, EpisodeCommandKind.LockFinalArgument);
            command.secondTargetId = FinalArgument.Emotional;
            command.text = FinalArgument.JoinReferences(FinalArgument.Moments(s).Take(FinalArgument.Required(s)).Select(m => m.reference));
            s = Step(s, command);
            Assert.That(s.finalArgument, Is.Not.Null);
            AcceptedUnchanged(s);
            return s;
        }

        private static EpisodeState Reach(EpisodeState s, Func<EpisodeState, bool> reached, int bound)
        {
            for (int i = 0; i < bound && !reached(s); i++) s = Step(s, Next(s));
            Assert.That(reached(s), Is.True, "Actual source progression did not reach the requested phase within its bound.");
            return s;
        }

        private static EpisodeCommand Next(EpisodeState s, string protectedId = null)
        {
            var command = Command(s, EpisodeCommandKind.Advance);
            if (s.pendingDiary != null)
            { command.kind = EpisodeCommandKind.SkipDiary; command.targetId = s.pendingDiary.id; }
            else if (EpisodeEngine.IsCompetition(s.phase) && !s.competitionResolved && EpisodeEngine.CompetitionPlayers(s).Any(c => c.isPlayer))
            { command.kind = EpisodeCommandKind.Compete; command.performance = 1; }
            else if (s.phase == EpisodePhase.Nomination && s.nominees.Count == 0 && s.hohId == s.playerId)
            {
                var candidates = EpisodeEngine.NominationCandidates(s).Take(2).ToArray();
                command.kind = EpisodeCommandKind.Nominate; command.targetId = candidates[0].id; command.secondTargetId = candidates[1].id;
            }
            else if (s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved)
            {
                string saved = s.vetoHolderId == s.playerId ? null : EpisodeEngine.NpcVetoSave(s);
                if (s.vetoHolderId == s.playerId || (s.hohId == s.playerId && saved != null))
                {
                    command.kind = EpisodeCommandKind.ResolveVeto;
                    command.useVeto = saved != null; command.targetId = saved;
                    command.secondTargetId = saved == null ? null : EpisodeEngine.ReplacementCandidates(s).First(c => c.id != protectedId).id;
                }
            }
            else if (s.phase == EpisodePhase.Eviction && s.evictionStage == EvictionStage.Speeches && s.nominees.Contains(s.playerId)
                && !s.evictionSpeeches.Any(e => e.speakerId == s.playerId))
            { command.kind = EpisodeCommandKind.SubmitEvictionSpeech; command.text = "I own the game I played."; }
            else if (s.phase == EpisodePhase.Eviction && !s.evictionResolved
                && (s.evictionStage == EvictionStage.Voting || s.evictionStage == EvictionStage.Tiebreaker)
                && !s.votes.Any(v => v.voterId == s.playerId)
                && (EpisodeEngine.Voters(s).Any(c => c.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(s)))
            { command.kind = EpisodeCommandKind.CastVote; command.targetId = s.nominees[0]; }
            else if (s.phase == EpisodePhase.FinalEviction && s.hohId == s.playerId)
            { command.kind = EpisodeCommandKind.FinalEvict; command.targetId = s.Active.First(c => !c.isPlayer).id; }
            else if (s.phase == EpisodePhase.JuryQuestioning && !s.juryExchanges[s.juryQuestionIndex].completed)
            {
                var q = s.juryExchanges[s.juryQuestionIndex];
                command.kind = EpisodeCommandKind.AnswerJury;
                command.targetId = q.finalistId == s.playerId ? q.questionerId : q.finalistId;
                command.secondTargetId = q.finalistId == s.playerId
                    ? FinaleQuestions.Offered(q.category, q.receiptKind)[0]
                    : WebJuryQuestioning.GetJurorQuestionOptions(s.juryQuestionIndex)[0].tone;
            }
            else if (s.phase == EpisodePhase.FinalSpeeches && s.Active.Any(c => c.isPlayer) && !s.finalSpeeches.Any(e => e.speakerId == s.playerId))
            { command.kind = EpisodeCommandKind.SubmitSpeech; command.text = FinalArgument.Speech(s); }
            else if (s.phase == EpisodePhase.Jury && !s.Active.Any(c => c.isPlayer) && !s.votes.Any(v => v.voterId == s.playerId))
            { command.kind = EpisodeCommandKind.CastVote; command.targetId = s.Active.First().id; }
            return command;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = "whole-safety-source-" + s.nextSequence, kind = kind, actorId = s.playerId,
            expectedRevision = s.revision, expectedPhase = s.phase,
        };

        private static EpisodeState Step(EpisodeState s, EpisodeCommand command)
        {
            string before = Json(s);
            var next = s.Clone();
            Call(typeof(EpisodeEngine), "Execute", next, command);
            Call(typeof(EpisodeEngine), "CancelInvalidNpcConversations", next);
            EpisodeEngine.ReconcileAllianceRows(next);
            Assert.That(Json(s), Is.EqualTo(before), "Actual source progression owns a detached copy.");
            Assert.That(next.revision, Is.EqualTo(s.revision), "This helper does not invent a public transaction receipt.");
            Assert.That(next.acceptedCommandIds, Is.EqualTo(s.acceptedCommandIds));
            Assert.That(next.promises.Any(p => p.kind == PromiseKind.Safety), Is.False);
            Assert.That(next.deals.Any(d => d.type == DealKind.SafetyAgreement), Is.False);
            AcceptedUnchanged(next);
            return next;
        }

        private static void AssertEmission(Owned owned, bool promise, bool hearing)
        {
            var facts = owned.State.story.facts.Where(f => f.kind == FactKinds.BrokenWord && f.refId == owned.Id).ToArray();
            Assert.That(facts, Has.Length.EqualTo(promise ? 0 : 1));
            var evidence = owned.State.unifiedHearingEvidence.Where(e => e.fact.refId == owned.Id).ToArray();
            Assert.That(evidence, Has.Length.EqualTo(!promise && hearing ? 1 : 0));
            if (!promise && hearing)
            {
                var receipt = owned.State.unifiedHearingReceipts.Single(r => r.factId == facts[0].id);
                Assert.That(receipt.kind, Is.EqualTo(UnifiedCommitmentHearings.Initial));
                Assert.That(receipt.listenerId, Is.EqualTo(owned.Other));
                Assert.That(evidence[0].fact.id, Is.EqualTo(facts[0].id));
            }
        }

        private static void AcceptedUnchanged(EpisodeState s)
        {
            string before = Json(s);
            Assert.That(Prospective(s, out string error), Is.True, error);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        private static void RefusedUnchanged(EpisodeState s)
        {
            string before = Json(s);
            bool accepted = true; string error = null;
            Assert.DoesNotThrow(() => accepted = Prospective(s, out error));
            Assert.That(accepted, Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(Json(s), Is.EqualTo(before), "Refusal must retain even corrupt/null fields, not normalize or replace them.");
        }

        private static void PublicRefusedUnchanged(EpisodeState s)
        {
            string before = Json(s);
            Assert.That(EpisodeValidation.TryValidate(s, out _), Is.False);
            Assert.Throws<ArgumentException>(() => new EpisodeEngine(s));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        private static bool Prospective(EpisodeState s, out string error)
        {
            var args = new object[] { s, null };
            bool accepted = (bool)Call(typeof(EpisodeValidation), "TryValidateProspectiveUnifiedSafety", args);
            error = (string)args[1];
            return accepted;
        }

        private static object Call(Type owner, string name, params object[] args)
        {
            var method = owner.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, owner.Name + "." + name + " is the actual production owner.");
            return method.Invoke(null, args);
        }

        private static string Json(EpisodeState s) => s == null ? "null" : JsonConvert.SerializeObject(s.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance).ToDictionary(f => f.Name, f => f.GetValue(s)));
    }
}
