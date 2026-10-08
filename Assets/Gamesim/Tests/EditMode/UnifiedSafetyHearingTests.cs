using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>Prospective real fact/spread/effect owners, not permission to activate saved rule 1.</summary>
    public sealed class UnifiedSafetyHearingTests
    {
        [TestCase(true)] [TestCase(false)]
        public void ActualSelectedSourceKeepsZeroPromiseOrOneDealFactAndInitialHasNoHearingEffect(bool promise)
        {
            var s = State(); s.unifiedCommitments.Add(Row(s, "source", promise));
            var old = s.Clone(); old.unifiedHearingRulesVersion = 0;
            Resolve(s); Resolve(old);
            Assert.That(SourceImage(s), Is.EqualTo(SourceImage(old)), "Only explicitly versioned bookkeeping is new.");
            Assert.That(s.story.facts.Count(f => f.kind == FactKinds.BrokenWord), Is.EqualTo(promise ? 0 : 1));
            Assert.That(s.unifiedHearingEvidence.Count, Is.EqualTo(promise ? 0 : 1));
            Assert.That(s.unifiedHearingReceipts.Count, Is.EqualTo(promise ? 0 : 1));
            Assert.That(HearingEvents(s), Is.Empty);
            if (!promise)
            {
                var receipt = s.unifiedHearingReceipts.Single();
                Assert.That((receipt.listenerId, receipt.kind, receipt.heardWeek),
                    Is.EqualTo((Other(s), UnifiedCommitmentHearings.Initial, s.week)));
                Assert.That(YourWord.HeardBy(s, YourWord.Breaches(s).Single()), Is.EqualTo(new[] { Other(s) }));
            }
            AssertValid(s);
            string once = Json(s); Resolve(s); Assert.That(Json(s), Is.EqualTo(once));
        }

        [Test]
        public void StrongerPromiseOwnerDoesNotEmitADealAliasesFactOrInitialReceipt()
        {
            var s = State(); s.unifiedCommitments.Add(Row(s, "a-promise", true));
            s.unifiedCommitments.Add(Row(s, "b-deal", false, DealTrust.Low));
            Assert.That(UnifiedCommitments.EvaluateNomination(s, "nomination", s.playerId, new[] { Other(s) })
                .Breaches.Single().EffectOwnerId, Is.EqualTo("a-promise"));
            Resolve(s);
            Assert.That(s.story.facts.Where(f => f.kind == FactKinds.BrokenWord), Is.Empty);
            Assert.That(s.unifiedHearingEvidence, Is.Empty); Assert.That(s.unifiedHearingReceipts, Is.Empty);
            Assert.That(YourWord.Cost(s), Is.Zero); AssertValid(s);
        }

        [Test]
        public void InitialWrongedKnowledgeCannotBecomeASecondHearingImpact()
        {
            var s = Broken(); var fact = s.story.facts.Single(f => f.kind == FactKinds.BrokenWord);
            string before = Json(s);
            Hear(s, fact, Other(s));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void GenuineSpreadOwnsOneOneWayEffectAndWhisperAndRepeatIsByteExact()
        {
            var s = Broken(); var fact = s.story.facts.Single(f => f.kind == FactKinds.BrokenWord);
            string listener = Other(s, 1); uint random = s.randomState;
            SpreadTo(s, fact.id, listener);
            double towardPlayer = s.Score(listener, s.playerId), towardListener = s.Score(s.playerId, listener);
            Hear(s, fact = s.story.facts.Single(f => f.id == fact.id), listener);
            Assert.That(s.Score(listener, s.playerId), Is.EqualTo(WebRules.ClampScore(towardPlayer
                + WebRules.RelationshipDelta(YourWord.HeardImpact, s.Find(listener).stats.social, false))));
            Assert.That(s.Score(s.playerId, listener), Is.EqualTo(towardListener));
            Assert.That(HearingEvents(s), Has.Count.EqualTo(1));
            Assert.That(s.events.Last().text, Is.EqualTo(YourWord.HeardLine(s, fact, listener)));
            Assert.That(s.unifiedHearingReceipts.Single(r => r.listenerId == listener).kind, Is.EqualTo(UnifiedCommitmentHearings.Spread));
            Assert.That(s.randomState, Is.EqualTo(random)); AssertValid(s);
            string once = Json(s); Hear(s, fact, listener); Assert.That(Json(s), Is.EqualTo(once));
        }

        [TestCase(false)] [TestCase(true)]
        public void DetachedHearingInstallsTheCompleteExistingEffectAndArcMutationClosure(bool existingArc)
        {
            var s = Broken(); var fact = s.story.facts.Single(f => f.kind == FactKinds.BrokenWord);
            string listener = Other(s, 1); SpreadTo(s, fact.id, listener);
            fact = s.story.facts.Single(f => f.id == fact.id);
            if (existingArc)
            {
                s.relationshipArcs = WebRelationshipArcs.Update(s.relationshipArcs, listener,
                    s.Find(listener).name, -16, "Earlier source relationship", s.week).arcs;
                s.relationshipArcs = WebRelationshipArcs.Update(s.relationshipArcs, listener,
                    s.Find(listener).name, -6, "Later source relationship", s.week).arcs;
                Assert.That(s.relationshipArcs.Single(a => a.npcId == listener).intensity, Is.EqualTo(24));
            }
            var old = s.Clone(); old.unifiedHearingRulesVersion = 0;
            old.unifiedHearingEvidence.Clear(); old.unifiedHearingReceipts.Clear();
            int sequence = s.nextSequence, events = s.events.Count;
            var relation = s.relationships.Single(r => r.fromId == listener && r.toId == s.playerId);
            int notes = relation.notes.Count, relationshipEvents = relation.events.Count;
            uint random = s.randomState;
            Hear(old, old.story.facts.Single(f => f.id == fact.id), listener); Hear(s, fact, listener);
            Assert.That(SourceImage(s), Is.EqualTo(SourceImage(old)),
                "The staged owner must install every original HeardAbout/Arc/Log write, and no other surface.");
            relation = s.relationships.Single(r => r.fromId == listener && r.toId == s.playerId);
            Assert.That(relation.lastInteractionWeek, Is.EqualTo(s.week));
            Assert.That(relation.notes, Has.Count.EqualTo(notes + 1));
            Assert.That(relation.events, Has.Count.EqualTo(relationshipEvents + 1));
            Assert.That(s.events, Has.Count.EqualTo(events + 1)); Assert.That(s.nextSequence, Is.EqualTo(sequence + 2));
            var arc = s.relationshipArcs.Single(a => a.npcId == listener);
            Assert.That(arc.weeklyHistory, Has.Count.EqualTo(existingArc ? 3 : 1));
            Assert.That(arc.weeklyHistory.Last().week, Is.EqualTo(s.week));
            Assert.That(arc.weeklyHistory.Last().delta, Is.EqualTo(WebRules.RelationshipDelta(
                YourWord.HeardImpact, s.Find(listener).stats.social, false)));
            if (existingArc)
            {
                Assert.That(arc.arcType, Is.EqualTo("rivalry"));
                Assert.That(arc.escalationLevel, Is.EqualTo(1));
            }
            Assert.That(s.randomState, Is.EqualTo(random)); AssertValid(s);
        }

        [TestCase(false)] [TestCase(true)]
        public void AudibleAliasesKeepEveryActualLeafButChargeOnlyOneIncidentListener(bool reverse)
        {
            var s = State(); s.unifiedCommitments.Add(Row(s, "owner", false, DealTrust.High));
            s.unifiedCommitments.Add(Row(s, "alias", false, DealTrust.Low)); Resolve(s);
            var owner = s.story.facts.Single(f => f.kind == FactKinds.BrokenWord);
            var alias = Alias(s, "alias");
            if (reverse) s.story.facts.Reverse();
            string listener = Other(s, 1); WarmTellers(s, listener);
            string anchor = AnchorFor(s, t => t.Count(x => x.listener == listener && (x.fact.id == owner.id || x.fact.id == alias.id)) == 2);
            Invoke("StorySystemsAt", s, anchor);
            Assert.That(s.unifiedHearingEvidence.Select(e => e.fact.refId), Is.EquivalentTo(new[] { "owner", "alias" }));
            Assert.That(s.unifiedHearingReceipts.Count(r => r.listenerId == listener), Is.EqualTo(1));
            Assert.That(HearingEvents(s), Has.Count.EqualTo(1));
            Assert.That(s.unifiedHearingEvidence.All(e => e.fact.knowers.Contains(listener)), Is.True);
            Assert.That(YourWord.Breaches(s), Has.Count.EqualTo(1)); Assert.That(YourWord.Hearings(s), Is.EqualTo(2));
            AssertValid(s);
        }

        [Test]
        public void WeakerActuallyAudibleDealNotThePrivatePromiseOwnerSuppliesCaptionAfterPruning()
        {
            var s = State(); s.unifiedCommitments.Add(Row(s, "owner-promise", true));
            s.unifiedCommitments.Add(Row(s, "heard-deal", false, DealTrust.Low)); Resolve(s);
            var audible = Alias(s, "heard-deal");
            Knowledge.Create(s, FactKinds.BrokenWord, s.playerId, Other(s), FactVisibility.Private,
                new StorylineState { id = "owner-promise" });
            string listener = Other(s, 1); SpreadTo(s, audible.id, listener);
            audible = s.story.facts.Single(f => f.id == audible.id); Hear(s, audible, listener);
            Assert.That(s.unifiedHearingEvidence.Single().fact.refId, Is.EqualTo("heard-deal"));
            Assert.That(s.unifiedHearingReceipts.All(r => r.kind == UnifiedCommitmentHearings.Spread), Is.True,
                "Observation of an alias cannot reconstruct an initial selected-owner emission.");
            Assert.That(s.events.Last().text, Does.Contain("safety deal"));
            Assert.That(s.events.Last().text, Does.Not.Contain("promise"));
            string lines = Json(YourWord.Lines(s)); int hearings = YourWord.Hearings(s);
            s.story.facts.Clear();
            Assert.That(Json(YourWord.Lines(s)), Is.EqualTo(lines)); Assert.That(YourWord.Hearings(s), Is.EqualTo(hearings));
            Assert.That(YourWord.Breaches(s).Single().refId, Is.EqualTo("heard-deal")); AssertValid(s);
        }

        [Test]
        public void DurableInitialAndSpreadKnowledgeSurviveClonePruningLogsAndDeparture()
        {
            var s = Broken(); var fact = s.story.facts.Single(f => f.kind == FactKinds.BrokenWord);
            string listener = Other(s, 1); SpreadTo(s, fact.id, listener);
            fact = s.story.facts.Single(f => f.id == fact.id); Hear(s, fact, listener);
            var restored = JsonConvert.DeserializeObject<EpisodeState>(Json(s));
            string lines = Json(YourWord.Lines(restored)); double cost = YourWord.Cost(restored);
            restored.story.facts.Clear(); restored.events.Clear(); restored.memories.Clear();
            foreach (var relationship in restored.relationships) relationship.events.Clear();
            restored.Find(listener).status = ContestantStatus.Jury;
            Assert.That(Json(YourWord.Lines(restored)), Is.EqualTo(lines)); Assert.That(YourWord.Cost(restored), Is.EqualTo(cost));
            AssertValid(restored);
            var detached = restored.Clone(); detached.unifiedHearingEvidence[0].fact.knowers.Clear();
            detached.unifiedHearingReceipts[0].listenerId = "cannot-rewrite";
            Assert.That(Json(YourWord.Lines(restored)), Is.EqualTo(lines));
            var view = YourWord.Breaches(restored).Single(); view.refId = "cannot-rewrite"; view.knowers.Clear();
            Assert.That(Json(YourWord.Lines(restored)), Is.EqualTo(lines));
        }

        [TestCase(true, false)] [TestCase(false, true)] [TestCase(true, true)]
        public void PrunedSpreadArchiveCannotLoseEitherOriginalParty(bool loseActor, bool loseWronged)
        {
            var s = State(); s.unifiedCommitments.Add(Row(s, "private-owner", true));
            s.unifiedCommitments.Add(Row(s, "audible-deal", false, DealTrust.Low)); Resolve(s);
            var fact = Alias(s, "audible-deal"); string listener = Other(s, 1);
            SpreadTo(s, fact.id, listener); fact = s.story.facts.Single(f => f.id == fact.id);
            Hear(s, fact, listener); s.story.facts.Clear();
            var evidence = s.unifiedHearingEvidence.Single();
            Assert.That(s.unifiedHearingReceipts.Single().kind, Is.EqualTo(UnifiedCommitmentHearings.Spread),
                "There is no Initial receipt to supply a separate first-party check.");
            Assert.That(evidence.fact.knowers, Is.EquivalentTo(new[] { s.playerId, Other(s), listener }));
            AssertValid(s);
            if (loseActor) evidence.fact.knowers.Remove(s.playerId);
            if (loseWronged) evidence.fact.knowers.Remove(Other(s));
            Assert.That(evidence.fact.knowers, Does.Contain(listener));
            string before = Json(s);
            Assert.That(UnifiedCommitmentHearings.ValidateStorage(s, out string error), Is.False);
            Assert.That(error, Is.Not.Empty); Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void GenuinePublicWideningRefreshesArchiveWithoutInventingHearingImpacts()
        {
            var s = Broken(); var fact = s.story.facts.Single(f => f.kind == FactKinds.BrokenWord);
            Knowledge.MakeKnown(s, fact, FactVisibility.Public);
            Assert.That(s.unifiedHearingEvidence.Single().fact.visibility, Is.EqualTo(FactVisibility.Public));
            Assert.That(s.unifiedHearingReceipts, Has.Count.EqualTo(1)); Assert.That(HearingEvents(s), Is.Empty);
            int count = YourWord.Hearers(s).Count; s.story.facts.Clear();
            Assert.That(YourWord.Hearers(s).Count, Is.EqualTo(count)); AssertValid(s);
        }

        [Test]
        public void PruningRefreshesAlreadyObservedLiveAudienceBeforeItsFactLeaves()
        {
            var s = Broken(); var fact = s.story.facts.Single(f => f.kind == FactKinds.BrokenWord);
            Knowledge.AddKnower(s, fact, Other(s, 1));
            for (int i = 1; i < Knowledge.Ceiling; i++) s.story.facts.Add(new HouseFactState {
                id = "other-" + i, kind = FactKinds.BrokenWord, actorId = s.playerId, subjectId = Other(s),
                refId = "unrelated-" + i, visibility = FactVisibility.Private, week = s.week,
                knowers = new List<string> { s.playerId } });
            Knowledge.MakeRoom(s);
            Assert.That(s.story.facts.Any(f => f.id == fact.id), Is.False);
            Assert.That(YourWord.Hearers(s), Is.EqualTo(new[] { Other(s), Other(s, 1) }));
            Assert.That(s.unifiedHearingReceipts, Has.Count.EqualTo(1), "An observation is not proof of a spread impact.");
            AssertValid(s);
        }

        [Test]
        public void DistinctActualNominationAndReplacementIncidentsEachHaveTheirOwnHearing()
        {
            var s = Broken(); s.unifiedCommitments.Add(Row(s, "replacement", false));
            Invoke("ResolveUnifiedSafetyNomination", s, "replacement", s.playerId, new[] { Other(s) });
            string listener = Other(s, 1);
            foreach (string id in s.story.facts.Where(f => f.kind == FactKinds.BrokenWord).Select(f => f.id).ToArray())
            {
                SpreadTo(s, id, listener);
                Hear(s, s.story.facts.Single(f => f.id == id), listener);
            }
            Assert.That(s.unifiedHearingReceipts.Count(r => r.listenerId == listener), Is.EqualTo(2));
            Assert.That(HearingEvents(s), Has.Count.EqualTo(2)); Assert.That(YourWord.Breaches(s), Has.Count.EqualTo(2)); AssertValid(s);
        }

        [Test]
        public void TheEntireSpreadPassPrecedesEffectsAndRetainsSourceOrderingAndKeyedRandomness()
        {
            var s = Broken(); string listener = Other(s, 1); WarmTellers(s, listener);
            // A later fact's teller is the first hearing's listener. A premature effect changes this
            // teller's view of the player, so it must not influence that later fact's spread selection.
            var plan = Knowledge.Create(s, FactKinds.Plan, listener, null, FactVisibility.Whispered,
                new StorylineState { id = "later-plan" });
            Assert.That(plan.knowers, Is.EqualTo(new[] { listener }), "Only the intended teller may carry this causal control.");
            var broken = s.story.facts.Single(f => f.kind == FactKinds.BrokenWord);
            Assert.That(StringComparer.Ordinal.Compare(broken.id, plan.id), Is.LessThan(0),
                "The actual source pass must visit the hearing's fact before its later causal control.");
            foreach (var edge in s.relationships.Where(r => r.fromId == listener))
                edge.score = edge.toId == s.playerId ? 55 : edge.toId == Other(s, 3) ? 54 : -80;
            var old = s.Clone(); old.unifiedHearingRulesVersion = 0; old.unifiedHearingEvidence.Clear(); old.unifiedHearingReceipts.Clear();
            string anchor = AnchorFor(old, t => t.Any(x => x.listener == listener && x.fact.kind == FactKinds.BrokenWord)
                && t.Any(x => x.listener == old.playerId && x.fact.refId == "later-plan"));
            var premature = old.Clone();
            Invoke("HeardAbout", premature, listener, YourWord.HeardImpact, "Premature causal control", YourWord.HeardType);
            Assert.That(premature.Score(listener, premature.playerId), Is.LessThan(premature.Score(listener, Other(s, 3))));
            Assert.That(Knowledge.Spread(premature, anchor).Any(x => x.listener == premature.playerId
                && x.fact.refId == "later-plan"), Is.False,
                "An interleaved hearing would actually change this later fact's possible recipient.");
            uint random = s.randomState; Invoke("StorySystemsAt", old, anchor); Invoke("StorySystemsAt", s, anchor);
            Assert.That(SourceImage(s), Is.EqualTo(SourceImage(old)));
            Assert.That(s.story.facts.Single(f => f.refId == "later-plan").knowers, Does.Contain(s.playerId));
            Assert.That(s.randomState, Is.EqualTo(random)); AssertValid(s);
        }

        [TestCase(false)] [TestCase(true)]
        public void HearingZeroKeepsLegacyAndProspectiveSourceBehaviorWithoutArchiving(bool canonical)
        {
            var s = State(); s.unifiedHearingRulesVersion = 0;
            if (canonical) { s.unifiedCommitments.Add(Row(s, "source", false)); Resolve(s); }
            else
            {
                s.unifiedCommitmentRulesVersion = 0;
                Knowledge.BrokenWord(s, new DealState { id = "legacy", type = DealKind.Partnership }, s.playerId, Other(s));
            }
            var fact = s.story.facts.Single(f => f.kind == FactKinds.BrokenWord); string listener = Other(s, 1);
            SpreadTo(s, fact.id, listener); fact = s.story.facts.Single(f => f.id == fact.id);
            Hear(s, fact, listener); Hear(s, fact, listener);
            Assert.That(HearingEvents(s), Has.Count.EqualTo(2), "The new guard is explicitly versioned, not retroactive.");
            Assert.That(s.unifiedHearingEvidence, Is.Empty); Assert.That(s.unifiedHearingReceipts, Is.Empty);
        }

        [Test]
        public void UnrelatedLegacyFamilyRetainsItsExistingRepeatedEffectEvenUnderHearingOne()
        {
            var s = State(); var fact = Knowledge.BrokenWord(s, new DealState { id = "legacy-veto", type = DealKind.VetoUse }, s.playerId, Other(s));
            string listener = Other(s, 1); SpreadTo(s, fact.id, listener); fact = s.story.facts.Single(f => f.id == fact.id);
            Hear(s, fact, listener); Hear(s, fact, listener);
            Assert.That(HearingEvents(s), Has.Count.EqualTo(2)); Assert.That(s.unifiedHearingEvidence, Is.Empty);
            Assert.That(s.unifiedHearingReceipts, Is.Empty); AssertValid(s);
        }

        [TestCase("version")] [TestCase("disabled")] [TestCase("canonical-disabled")]
        [TestCase("missing-evidence")] [TestCase("missing-receipts")] [TestCase("null-fact")]
        [TestCase("duplicate-evidence")] [TestCase("duplicate-pair")] [TestCase("private")]
        [TestCase("actor")] [TestCase("subject")] [TestCase("ref")] [TestCase("incident")]
        [TestCase("fact-date-before")] [TestCase("fact-date-after")] [TestCase("fact-id")]
        [TestCase("fact-sequence")] [TestCase("duplicate-knower")] [TestCase("unknown-knower")]
        [TestCase("player-listener")] [TestCase("future-hearing")] [TestCase("old-hearing")]
        [TestCase("receipt-kind")] [TestCase("receipt-fact")] [TestCase("initial-listener")]
        [TestCase("live-identity")] [TestCase("live-audience")]
        public void StrictStorageRejectsMalformedProvenanceWithoutMutatingInput(string fault)
        {
            var s = Broken(); var evidence = s.unifiedHearingEvidence.Single(); var receipt = s.unifiedHearingReceipts.Single();
            switch (fault)
            {
                case "version": s.unifiedHearingRulesVersion = 2; break;
                case "disabled": s.unifiedHearingRulesVersion = 0; break;
                case "canonical-disabled": s.unifiedCommitmentRulesVersion = 0; break;
                case "missing-evidence": s.unifiedHearingEvidence = null; break;
                case "missing-receipts": s.unifiedHearingReceipts = null; break;
                case "null-fact": evidence.fact = null; break;
                case "duplicate-evidence": s.unifiedHearingEvidence.Add(evidence.Clone()); break;
                case "duplicate-pair": s.unifiedHearingReceipts.Add(receipt.Clone()); break;
                case "private": evidence.fact.visibility = FactVisibility.Private; break;
                case "actor": evidence.fact.actorId = Other(s, 1); break;
                case "subject": evidence.fact.subjectId = Other(s, 1); break;
                case "ref": evidence.fact.refId = "no-canonical-leaf"; break;
                case "incident": evidence.incidentKey += ":not-the-decision"; break;
                case "fact-date-before": evidence.fact.week--; break;
                case "fact-date-after": evidence.fact.week++; break;
                case "fact-id": evidence.fact.id = "fact-not-a-sequence"; break;
                case "fact-sequence": evidence.fact.id = "fact-" + s.nextSequence; break;
                case "duplicate-knower": evidence.fact.knowers.Add(Other(s)); break;
                case "unknown-knower": evidence.fact.knowers.Add("not-a-houseguest"); break;
                case "player-listener": receipt.listenerId = s.playerId; break;
                case "future-hearing": receipt.heardWeek = s.week + 1; break;
                case "old-hearing": receipt.heardWeek--; break;
                case "receipt-kind": receipt.kind = "invented"; break;
                case "receipt-fact": receipt.factId = "fact-absent"; break;
                case "initial-listener": receipt.listenerId = Other(s, 1); evidence.fact.knowers.Add(Other(s, 1));
                    s.story.facts.Single(f => f.id == evidence.fact.id).knowers.Add(Other(s, 1)); break;
                case "live-identity": s.story.facts.Single(f => f.id == evidence.fact.id).subjectId = Other(s, 1); break;
                case "live-audience": s.story.facts.Single(f => f.id == evidence.fact.id).knowers.Remove(Other(s)); break;
                default: throw new ArgumentException(fault);
            }
            string before = Json(s);
            Assert.That(UnifiedCommitmentHearings.ValidateStorage(s, out string error), Is.False);
            Assert.That(error, Is.Not.Empty); Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase("spread")] [TestCase("hearing")] [TestCase("emission")] [TestCase("anchor")]
        public void MalformedGuardRefusesBeforeAnyFactScoreRngOrReceiptWrite(string owner)
        {
            var s = Broken(); var fact = s.story.facts.Single(f => f.kind == FactKinds.BrokenWord);
            s.unifiedHearingReceipts.Single().heardWeek++;
            string before = Json(s);
            Assert.Throws<ArgumentException>(() => {
                if (owner == "spread") Knowledge.Spread(s, "bad-guard");
                else if (owner == "hearing") Hear(s, fact, Other(s));
                else if (owner == "emission") Knowledge.BrokenWord(s, CommitmentReferences.FindDeal(s, "source"), s.playerId, Other(s));
                else Invoke("StorySystemsAt", s, "bad-guard");
            });
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void ACollectionWithLaterForgedCanonicalFactCannotPartiallySpreadEarlierValidFacts()
        {
            var s = Broken(); var alias = Row(s, "bad-alias", false, DealTrust.Low);
            // Add the second leaf before its real replacement settlement so its history is genuine.
            s.unifiedCommitments.Add(alias); Invoke("ResolveUnifiedSafetyNomination", s, "replacement", s.playerId, new[] { Other(s) });
            var forged = s.story.facts.Single(f => f.refId == alias.id); forged.week--;
            s.unifiedHearingEvidence.RemoveAll(e => e.fact.refId == alias.id);
            s.unifiedHearingReceipts.RemoveAll(r => r.factId == forged.id);
            WarmTellers(s, Other(s, 1));
            string anchor = AnchorForUnchecked(s, t => t.Any(x => x.fact.id == forged.id));
            string before = Json(s);
            Assert.Throws<ArgumentException>(() => Knowledge.Spread(s, anchor));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void AGenericPromiseRefFactCannotClaimTheAbsentCanonicalPromiseEmission(bool archive)
        {
            var s = State(); s.unifiedCommitments.Add(Row(s, "promise-only", true)); Resolve(s);
            var fact = Alias(s, "promise-only");
            if (archive)
            {
                s.unifiedHearingEvidence.Add(new UnifiedHearingEvidenceState {
                    incidentKey = s.unifiedCommitments.Single().settlementEffectKey, fact = fact.Clone() });
                string before = Json(s); Assert.That(UnifiedCommitmentHearings.ValidateStorage(s, out _), Is.False);
                Assert.That(Json(s), Is.EqualTo(before));
            }
            else
            {
                WarmTellers(s, Other(s, 1));
                string anchor = AnchorForUnchecked(s, t => t.Any(x => x.fact.id == fact.id));
                string before = Json(s); Assert.Throws<ArgumentException>(() => Knowledge.Spread(s, anchor));
                Assert.That(Json(s), Is.EqualTo(before));
            }
        }

        [Test]
        public void BoundDerivationAndOverCapacityStorageAreExplicitRatherThanFifo()
        {
            Assert.That(UnifiedCommitmentHearings.EvidenceCapacity, Is.EqualTo(400));
            Assert.That(UnifiedCommitmentHearings.ReceiptCapacity, Is.EqualTo(6000));
            Assert.That(UnifiedCommitmentHearings.ReceiptCapacity, Is.EqualTo(
                UnifiedCommitments.FamilyCapacity * 2 * (EpisodeValidation.MaximumCast - 1)));
            var s = Broken(); var evidence = s.unifiedHearingEvidence.Single();
            s.unifiedHearingEvidence = Enumerable.Range(0, 401).Select(_ => evidence.Clone()).ToList();
            string before = Json(s); Assert.That(UnifiedCommitmentHearings.ValidateStorage(s, out _), Is.False);
            Assert.That(Json(s), Is.EqualTo(before));
            s = Broken(); var receipt = s.unifiedHearingReceipts.Single();
            s.unifiedHearingReceipts = Enumerable.Range(0, 6001).Select(_ => receipt.Clone()).ToList();
            before = Json(s); Assert.That(UnifiedCommitmentHearings.ValidateStorage(s, out _), Is.False);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        private static EpisodeState State()
        {
            var s = ContentCatalog.Create(17); s.week = 3; s.phase = EpisodePhase.Nomination; s.hohId = s.playerId;
            s.unifiedCommitmentRulesVersion = 1; s.unifiedHearingRulesVersion = 1;
            s.commitmentRulesStartWeek = s.strategyRulesStartWeek = 1;
            s.story.rulesStartWeek = 1; s.story.rulesVersion = StoryRules.Current;
            return s;
        }
        private static UnifiedCommitmentState Row(EpisodeState s, string id, bool promise, string trust = DealTrust.Medium) =>
            new UnifiedCommitmentState { id = id, kind = UnifiedCommitments.Safety,
                sourcePolicy = promise ? UnifiedCommitments.PromisePolicy : UnifiedCommitments.DealPolicy,
                origin = promise ? UnifiedCommitments.StoryPromise : UnifiedCommitments.StoryDeal,
                makerId = s.playerId, beneficiaryId = Other(s), reciprocal = !promise, createdWeek = s.week,
                expiresWeek = s.week + (promise ? 1 : 0), status = DealStatus.Active, trustImpact = trust };
        private static EpisodeState Broken()
        {
            var s = State(); s.unifiedCommitments.Add(Row(s, "source", false)); Resolve(s); AssertValid(s); return s;
        }
        private static string Other(EpisodeState s, int index = 0) => s.contestants.Where(c => c.id != s.playerId).Skip(index).First().id;
        private static void Resolve(EpisodeState s) => Invoke("ResolveUnifiedSafetyNomination", s, "nomination", s.playerId, new[] { Other(s) });
        private static HouseFactState Alias(EpisodeState s, string reference) => Knowledge.Create(s,
            FactKinds.BrokenWord, s.playerId, Other(s), FactVisibility.Whispered, new StorylineState { id = reference });
        private static List<RelationshipEventState> HearingEvents(EpisodeState s) => s.relationships
            .Where(r => r.toId == s.playerId).SelectMany(r => r.events).Where(e => e.type == YourWord.HeardType).ToList();
        private static void Hear(EpisodeState s, HouseFactState fact, string listener) => Invoke("HeardOfYourWord", s, fact, listener);
        private static void WarmTellers(EpisodeState s, string listener)
        {
            var tellers = s.story.facts.Where(f => f.kind == FactKinds.BrokenWord).SelectMany(f => f.knowers).Distinct().ToArray();
            foreach (var edge in s.relationships.Where(r => tellers.Contains(r.fromId)))
                edge.score = edge.toId == listener ? 90 : -80;
        }
        private static void SpreadTo(EpisodeState s, string factId, string listener)
        {
            var fact = s.story.facts.Single(f => f.id == factId);
            if (fact.knowers.Contains(listener)) return;
            WarmTellers(s, listener);
            string anchor = AnchorFor(s, t => t.Any(x => x.fact.id == factId && x.listener == listener));
            Assert.That(Knowledge.Spread(s, anchor).Any(x => x.fact.id == factId && x.listener == listener), Is.True);
        }
        private static string AnchorFor(EpisodeState s, Func<List<(HouseFactState fact, string listener)>, bool> accept)
        {
            for (int i = 0; i < 1024; i++)
            {
                string anchor = "hearing-fixture-" + i;
                if (accept(Knowledge.Spread(s.Clone(), anchor))) return anchor;
            }
            throw new AssertionException("No bounded actual source gossip pass supplied the requested fixture.");
        }
        private static string AnchorForUnchecked(EpisodeState s, Func<List<(HouseFactState fact, string listener)>, bool> accept)
        {
            var legacy = s.Clone(); legacy.unifiedHearingRulesVersion = 0;
            legacy.unifiedHearingEvidence.Clear(); legacy.unifiedHearingReceipts.Clear();
            return AnchorFor(legacy, accept);
        }
        private static void AssertValid(EpisodeState s) => Assert.That(UnifiedCommitmentHearings.ValidateStorage(s, out string error), Is.True, error);
        private static void Invoke(string name, params object[] arguments)
        {
            var method = typeof(EpisodeEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, name);
            try { method.Invoke(null, arguments); }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
        }
        private static string SourceImage(EpisodeState s)
        {
            var image = JObject.FromObject(s); image.Remove("unifiedHearingRulesVersion");
            image.Remove("unifiedHearingEvidence"); image.Remove("unifiedHearingReceipts");
            return image.ToString(Formatting.None);
        }
        private static string Json(object value) => JsonConvert.SerializeObject(value);
    }
}
