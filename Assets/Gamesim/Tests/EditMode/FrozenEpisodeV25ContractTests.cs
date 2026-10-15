using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Current schema28 mode0/1 compatibility with the complete fixed former25 contract.
    /// Every positive role, election, duty and hearing is factory/public-Apply produced. Only
    /// fresh rule selection precedes construction; later JSON corruption is explicitly negative.
    /// The explicit neutral bridge is NOT retained historical evidence; the separate immutable
    /// JSON-only corpus tests retain that evidence. These tests establish no disk/native acceptance.
    /// </summary>
    public sealed class FrozenEpisodeV25ContractTests
    {
        private const uint FirstSeed = 2505, EndSeed = 2537;
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly Type Contract = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.FrozenEpisodeV25", true);
        private static readonly Type JsonApi = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.SaveJson", true);
        private static readonly Dictionary<string, EpisodeState> Witnesses = new Dictionary<string, EpisodeState>(StringComparer.Ordinal);
        // Test-only, disabled by default. The bounded task-local harness supplies its own
        // case metadata and captures detached observations; ordinary NUnit performs no I/O.
        private static Action<string, JObject, string> CorpusObserver = null;

        [TestCase(0, 3)] [TestCase(0, 6)] [TestCase(0, 8)] [TestCase(0, 12)]
        [TestCase(1, 3)] [TestCase(1, 6)] [TestCase(1, 8)] [TestCase(1, 12)]
        [TestCase(2, 3)] [TestCase(2, 6)] [TestCase(2, 8)] [TestCase(2, 12)]
        public void RealFactoryCastAndAllThreeRecordedModesRemainExact(int mode, int size)
        {
            var state = Fresh(mode, FirstSeed, size);
            Assert.That(state.contestants, Has.Count.EqualTo(size));
            Accept(Payload(state));
            AssertMode(state, mode);
        }

        [TestCase(0, "promise")] [TestCase(1, "promise")] [TestCase(2, "promise")]
        [TestCase(1, "deal")] [TestCase(2, "deal")]
        [TestCase(1, "broken-promise")] [TestCase(2, "broken-promise")]
        [TestCase(1, "broken-deal")] [TestCase(2, "broken-deal")]
        [TestCase(1, "spared-deal")] [TestCase(2, "spared-deal")]
        [TestCase(2, "spread")] [TestCase(2, "archive-only")]
        [TestCase(2, "later-broken-deal")]
        [TestCase(1, "mixed-counter")] [TestCase(2, "mixed-counter")]
        [TestCase(1, "pitch-pending")] [TestCase(2, "pitch-assessed")] [TestCase(2, "pitch-promised")]
        [TestCase(0, "debit")] [TestCase(1, "debit")] [TestCase(2, "debit")]
        [TestCase(1, "block-speech")] [TestCase(2, "final-argument")]
        public void ActualPublicOwnersProduceCompleteFrozen25Checkpoints(int mode, string checkpoint)
        {
            var state = Witness(mode, checkpoint);
            Accept(Payload(state));
            AssertMode(state, mode);
            Assert.That(state.revision, Is.GreaterThan(0), "A checkpoint is actual command history, not an assigned phase.");
            Assert.That(state.acceptedCommandIds, Is.Not.Empty);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void EveryRealSeasonBoundaryAndItsFinishedStatePassWithoutMigrationOrActivation(int mode)
        {
            var engine = new EpisodeEngine(Fresh(mode, FirstSeed));
            var phases = new HashSet<EpisodePhase>();
            var weeks = new HashSet<int>();
            for (int step = 0; step < 512; step++)
            {
                var state = engine.Snapshot;
                Accept(Payload(state)); AssertMode(state, mode);
                phases.Add(state.phase); weeks.Add(state.week);
                if (state.phase == EpisodePhase.Finished) break;
                Commit(engine, Next(state));
            }
            var done = engine.Snapshot;
            Assert.That(done.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(done.winnerId, Is.Not.Empty);
            Assert.That(done.runnerUpId, Is.Not.EqualTo(done.winnerId));
            Assert.That(phases, Has.Member(EpisodePhase.Campaign).And.Member(EpisodePhase.Eviction));
            Assert.That(weeks.Count, Is.GreaterThan(1));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ValidationAndDetachedCallerCopiesCannotRewriteTheAcceptedWitness(int mode)
        {
            var state = Witness(mode, "promise");
            var original = Payload(state); string before = Text(original);
            var copy = (JObject)original.DeepClone();
            Accept(copy);
            copy["contestants"][0]["name"] = "A detached caller's name";
            ((JArray)copy["relationships"][0]["notes"]).Add("A detached caller's note");
            Assert.That(Text(original), Is.EqualTo(before));
            Accept(original);
            var input = state.Clone(); string originalState = Text(Payload(state));
            var engine = new EpisodeEngine(input);
            string engineBefore = Text(Payload(engine.Snapshot));
            input.contestants[0].name = "A detached constructor input";
            input.relationships.Clear();
            var returned = engine.Snapshot; returned.acceptedCommandIds.Clear(); returned.contestants.Clear();
            Assert.That(Text(Payload(engine.Snapshot)), Is.EqualTo(engineBefore));
            Assert.That(Text(Payload(state)), Is.EqualTo(originalState));
        }

        [TestCase("missing-schema")] [TestCase("null-schema")] [TestCase("fraction-schema")] [TestCase("text-schema")]
        [TestCase("old-schema")] [TestCase("future-schema")] [TestCase("unknown-root")]
        [TestCase("missing-unified-version")] [TestCase("null-unified-version")] [TestCase("text-unified-version")]
        [TestCase("fraction-unified-version")] [TestCase("missing-unified-list")] [TestCase("null-unified-list")]
        [TestCase("object-unified-list")] [TestCase("null-unified-row")]
        [TestCase("missing-hearing-version")] [TestCase("null-hearing-version")] [TestCase("text-hearing-version")]
        [TestCase("missing-evidence")] [TestCase("null-evidence")] [TestCase("missing-receipts")] [TestCase("null-receipts")]
        public void FrozenRootFieldsCannotBeDefaultedCoercedOrRelabelled(string defect)
        {
            var payload = Baseline(2, "broken-deal");
            switch (defect)
            {
                case "missing-schema": payload.Remove("schemaVersion"); break;
                case "null-schema": payload["schemaVersion"] = JValue.CreateNull(); break;
                case "fraction-schema": payload["schemaVersion"] = 25.0; break;
                case "text-schema": payload["schemaVersion"] = "25"; break;
                case "old-schema": payload["schemaVersion"] = 24; break;
                case "future-schema": payload["schemaVersion"] = 26; break;
                case "unknown-root": payload["futureAuthority"] = false; break;
                case "missing-unified-version": payload.Remove("unifiedCommitmentRulesVersion"); break;
                case "null-unified-version": payload["unifiedCommitmentRulesVersion"] = JValue.CreateNull(); break;
                case "text-unified-version": payload["unifiedCommitmentRulesVersion"] = "1"; break;
                case "fraction-unified-version": payload["unifiedCommitmentRulesVersion"] = 1.0; break;
                case "missing-unified-list": payload.Remove("unifiedCommitments"); break;
                case "null-unified-list": payload["unifiedCommitments"] = JValue.CreateNull(); break;
                case "object-unified-list": payload["unifiedCommitments"] = new JObject(); break;
                case "null-unified-row": ((JArray)payload["unifiedCommitments"]).Add(JValue.CreateNull()); break;
                case "missing-hearing-version": payload.Remove("unifiedHearingRulesVersion"); break;
                case "null-hearing-version": payload["unifiedHearingRulesVersion"] = JValue.CreateNull(); break;
                case "text-hearing-version": payload["unifiedHearingRulesVersion"] = "1"; break;
                case "missing-evidence": payload.Remove("unifiedHearingEvidence"); break;
                case "null-evidence": payload["unifiedHearingEvidence"] = JValue.CreateNull(); break;
                case "missing-receipts": payload.Remove("unifiedHearingReceipts"); break;
                case "null-receipts": payload["unifiedHearingReceipts"] = JValue.CreateNull(); break;
                default: Assert.Fail("Unknown root defect."); break;
            }
            Reject(payload);
        }

        [TestCase("commitments-off")] [TestCase("commitments-scheduled")]
        [TestCase("story-off")] [TestCase("story-scheduled")] [TestCase("pre-bonds")]
        [TestCase("unknown-unified")] [TestCase("negative-unified")]
        [TestCase("unknown-hearing")] [TestCase("negative-hearing")]
        [TestCase("disabled-authority")] [TestCase("disabled-hearing")]
        public void Former25MeansItsActualPublicPrerequisitesAndRecordedSupportedModes(string defect)
        {
            var payload = Baseline(2, "broken-deal");
            switch (defect)
            {
                case "commitments-off": payload["commitmentRulesStartWeek"] = 0; break;
                case "commitments-scheduled": payload["commitmentRulesStartWeek"] = (int)payload["week"] + 1; break;
                case "story-off": payload["story"]["rulesStartWeek"] = 0; break;
                case "story-scheduled": payload["story"]["rulesStartWeek"] = (int)payload["week"] + 1; break;
                case "pre-bonds": payload["story"]["rulesVersion"] = StoryRules.Bonds - 1; break;
                // 3, not 2: since vote family V6 mode 2 is the unified vote rules, which current validation judges by its own core;
                // a former25 payload claiming 2 is the fixed contract's to refuse (AFormer25PayloadClaimingModeTwoIsRefused).
                case "unknown-unified": payload["unifiedCommitmentRulesVersion"] = 3; break;
                case "negative-unified": payload["unifiedCommitmentRulesVersion"] = -1; break;
                case "unknown-hearing": payload["unifiedHearingRulesVersion"] = 2; break;
                case "negative-hearing": payload["unifiedHearingRulesVersion"] = -1; break;
                case "disabled-authority": payload["unifiedCommitmentRulesVersion"] = 0; break;
                case "disabled-hearing": payload["unifiedHearingRulesVersion"] = 0; break;
                default: Assert.Fail("Unknown mode defect."); break;
            }
            RejectSemantic(payload);
        }

        /// <summary>
        /// Vote family V6: mode 2 exists now, but never in a historical save. A former25 payload claiming it is refused by the fixed
        /// contract and never migrated, whatever current validation would make of the season it describes.
        /// </summary>
        [Test]
        public void AFormer25PayloadClaimingModeTwoIsRefused()
        {
            var payload = Baseline(2, "broken-deal");
            payload["unifiedCommitmentRulesVersion"] = UnifiedVoteFamilyValidation.Version;
            Reject(payload);
        }

        [TestCase("missing-field")] [TestCase("unknown-field")] [TestCase("null-id")]
        [TestCase("number-id")] [TestCase("text-reciprocal")] [TestCase("integer-reciprocal")]
        [TestCase("fraction-created-week")] [TestCase("text-settled-week")]
        [TestCase("object-trust")] [TestCase("array-key")]
        public void CanonicalRowShapeIsExactlyTheFormerFifteenScalars(string defect)
        {
            var payload = Baseline(1, "broken-deal"); var row = Canonical(payload, UnifiedCommitments.PlayerDeal);
            Assert.That(row.Properties().Count(), Is.EqualTo(15));
            switch (defect)
            {
                case "missing-field": row.Remove("linkedCommitmentId"); break;
                case "unknown-field": row["futurePolicy"] = JValue.CreateNull(); break;
                case "null-id": row["id"] = JValue.CreateNull(); break;
                case "number-id": row["id"] = 17; break;
                case "text-reciprocal": row["reciprocal"] = "true"; break;
                case "integer-reciprocal": row["reciprocal"] = 1; break;
                case "fraction-created-week": row["createdWeek"] = (double)(int)row["createdWeek"]; break;
                case "text-settled-week": row["settledWeek"] = row["settledWeek"].ToString(); break;
                case "object-trust": row["trustImpact"] = new JObject(); break;
                case "array-key": row["settlementEffectKey"] = new JArray(); break;
                default: Assert.Fail("Unknown row shape defect."); break;
            }
            Reject(payload);
        }

        [TestCase("future-kind")] [TestCase("future-policy")] [TestCase("wrong-origin")]
        [TestCase("wrong-maker")] [TestCase("same-parties")] [TestCase("one-way-deal")]
        [TestCase("wrong-trust")] [TestCase("accepted-status")] [TestCase("active-settled")]
        [TestCase("wrong-breaker")] [TestCase("missing-breaker")] [TestCase("undated-break")]
        [TestCase("future-settlement")] [TestCase("malformed-key")] [TestCase("future-id")]
        [TestCase("wrong-id-prefix")] [TestCase("duplicate-id")]
        public void ActualSafetyIdentityAttributionStatusAndSourcePolicyStayFrozen(string defect)
        {
            var payload = Baseline(1, "broken-deal"); var row = Canonical(payload, UnifiedCommitments.PlayerDeal);
            switch (defect)
            {
                case "future-kind": row["kind"] = "vote"; break;
                case "future-policy": row["sourcePolicy"] = "future"; break;
                case "wrong-origin": row["origin"] = UnifiedCommitments.NpcOffer; break;
                case "wrong-maker": row["makerId"] = row["beneficiaryId"].DeepClone(); break;
                case "same-parties": row["beneficiaryId"] = row["makerId"].DeepClone(); break;
                case "one-way-deal": row["reciprocal"] = false; break;
                case "wrong-trust": row["trustImpact"] = DealTrust.Medium; break;
                case "accepted-status": row["status"] = DealStatus.Accepted; break;
                case "active-settled": row["status"] = DealStatus.Active; break;
                case "wrong-breaker": row["brokenById"] = row["beneficiaryId"].DeepClone(); break;
                case "missing-breaker": row["brokenById"] = JValue.CreateNull(); break;
                case "undated-break": row["settledWeek"] = 0; break;
                case "future-settlement": row["settledWeek"] = (int)payload["week"] + 1; break;
                case "malformed-key": row["settlementEffectKey"] = "safety:1:nomination:invented"; break;
                case "future-id": row["id"] = "deal-player-" + (int)payload["nextSequence"]; break;
                case "wrong-id-prefix": row["id"] = "deal-story-1"; break;
                case "duplicate-id": ((JArray)payload["unifiedCommitments"]).Add(row.DeepClone()); break;
                default: Assert.Fail("Unknown source defect."); break;
            }
            RejectSemantic(payload);
        }

        [TestCase("evidence-missing-key")] [TestCase("evidence-unknown-field")]
        [TestCase("evidence-null-fact")] [TestCase("evidence-object-list")]
        [TestCase("fact-missing-field")] [TestCase("fact-unknown-field")]
        [TestCase("fact-number-id")] [TestCase("fact-text-week")]
        [TestCase("fact-object-knowers")] [TestCase("fact-numeric-knower")]
        [TestCase("receipt-missing-field")] [TestCase("receipt-unknown-field")]
        [TestCase("receipt-null-row")] [TestCase("receipt-fraction-week")]
        public void DurableHearingNestedFieldsAreNotDefaultedCoercedOrIgnored(string defect)
        {
            var payload = Baseline(2, "spread");
            var evidence = Evidence(payload); var fact = (JObject)evidence["fact"];
            var receipt = Receipt(payload, UnifiedCommitmentHearings.Initial);
            switch (defect)
            {
                case "evidence-missing-key": evidence.Remove("incidentKey"); break;
                case "evidence-unknown-field": evidence["sourceOwner"] = JValue.CreateNull(); break;
                case "evidence-null-fact": evidence["fact"] = JValue.CreateNull(); break;
                case "evidence-object-list": payload["unifiedHearingEvidence"] = new JObject(); break;
                case "fact-missing-field": fact.Remove("visibility"); break;
                case "fact-unknown-field": fact["privateOwner"] = JValue.CreateNull(); break;
                case "fact-number-id": fact["id"] = 17; break;
                case "fact-text-week": fact["week"] = fact["week"].ToString(); break;
                case "fact-object-knowers": fact["knowers"] = new JObject(); break;
                case "fact-numeric-knower": ((JArray)fact["knowers"]).Add(17); break;
                case "receipt-missing-field": receipt.Remove("heardWeek"); break;
                case "receipt-unknown-field": receipt["policy"] = JValue.CreateNull(); break;
                case "receipt-null-row": ((JArray)payload["unifiedHearingReceipts"]).Add(JValue.CreateNull()); break;
                case "receipt-fraction-week": receipt["heardWeek"] = (double)(int)receipt["heardWeek"]; break;
                default: Assert.Fail("Unknown hearing shape defect."); break;
            }
            Reject(payload);
        }

        [TestCase("missing-archive")] [TestCase("missing-initial")]
        [TestCase("all-lineage-erased")] [TestCase("private-archive")]
        [TestCase("wrong-fact-owner")] [TestCase("wrong-fact-actor")]
        [TestCase("wrong-fact-subject")] [TestCase("actor-no-longer-knows")]
        [TestCase("wronged-no-longer-knows")] [TestCase("duplicate-knower")]
        [TestCase("future-fact-id")] [TestCase("wrong-incident")]
        [TestCase("wrong-initial-listener")] [TestCase("wrong-initial-kind")]
        [TestCase("wrong-spread-fact")] [TestCase("duplicate-listener")]
        public void ActualAudibleSourceLineageAndOncePerListenerHistoryCannotBeForged(string defect)
        {
            var payload = Baseline(2, "spread");
            var evidence = Evidence(payload); var fact = (JObject)evidence["fact"];
            var initial = Receipt(payload, UnifiedCommitmentHearings.Initial);
            var spread = Receipt(payload, UnifiedCommitmentHearings.Spread);
            string reference = (string)fact["refId"];
            switch (defect)
            {
                case "missing-archive": evidence.Remove(); break;
                case "missing-initial": initial.Remove(); break;
                case "all-lineage-erased":
                    foreach (var item in ((JArray)payload["story"]["facts"]).OfType<JObject>().Where(f => (string)f["refId"] == reference).ToArray()) item.Remove();
                    foreach (var item in ((JArray)payload["unifiedHearingEvidence"]).OfType<JObject>().Where(e => (string)e["fact"]["refId"] == reference).ToArray()) item.Remove();
                    foreach (var item in ((JArray)payload["unifiedHearingReceipts"]).OfType<JObject>().Where(r => (string)r["incidentKey"] == (string)initial["incidentKey"]).ToArray()) item.Remove();
                    break;
                case "private-archive": fact["visibility"] = FactVisibility.Private; break;
                case "wrong-fact-owner": fact["refId"] = "deal-player-missing"; break;
                case "wrong-fact-actor": fact["actorId"] = fact["subjectId"].DeepClone(); break;
                case "wrong-fact-subject": fact["subjectId"] = fact["actorId"].DeepClone(); break;
                case "actor-no-longer-knows": RemoveKnower(fact, (string)fact["actorId"]); break;
                case "wronged-no-longer-knows": RemoveKnower(fact, (string)fact["subjectId"]); break;
                case "duplicate-knower": ((JArray)fact["knowers"]).Add(fact["knowers"][0].DeepClone()); break;
                case "future-fact-id": fact["id"] = "fact-" + (int)payload["nextSequence"]; break;
                case "wrong-incident": evidence["incidentKey"] = "safety:1:unrelated"; break;
                case "wrong-initial-listener": initial["listenerId"] = payload["playerId"].DeepClone(); break;
                case "wrong-initial-kind": initial["kind"] = "future"; break;
                case "wrong-spread-fact": spread["factId"] = "fact-missing"; break;
                case "duplicate-listener": ((JArray)payload["unifiedHearingReceipts"]).Add(spread.DeepClone()); break;
                default: Assert.Fail("Unknown audible lineage defect."); break;
            }
            RejectSemantic(payload);
        }

        [TestCase("live-fact-date")] [TestCase("archive-fact-date")]
        [TestCase("initial-date")] [TestCase("story-boundary")]
        [TestCase("wrong-historical-hoh")] [TestCase("missing-historical-power")]
        public void EarlierActualSettlementAndItsKnowledgeBoundaryDoNotBecomeTodaysIncident(string defect)
        {
            var payload = Baseline(2, "later-broken-deal"); var row = Canonical(payload, UnifiedCommitments.PlayerDeal);
            int settled = (int)row["settledWeek"], current = (int)payload["week"];
            Assert.That(current, Is.GreaterThan(settled));
            var evidence = Evidence(payload);
            switch (defect)
            {
                case "live-fact-date":
                    ((JArray)payload["story"]["facts"]).OfType<JObject>().Single(f => (string)f["refId"] == (string)row["id"])["week"] = current; break;
                case "archive-fact-date": evidence["fact"]["week"] = current; break;
                case "initial-date": Receipt(payload, UnifiedCommitmentHearings.Initial)["heardWeek"] = current; break;
                case "story-boundary": payload["story"]["rulesStartWeek"] = settled + 1; break;
                case "wrong-historical-hoh":
                    ((JArray)payload["ledger"]["power"]).OfType<JObject>().Single(p => (int)p["week"] == settled)["hohId"] = row["beneficiaryId"].DeepClone(); break;
                case "missing-historical-power":
                    ((JArray)payload["ledger"]["power"]).OfType<JObject>().Single(p => (int)p["week"] == settled).Remove(); break;
                default: Assert.Fail("Unknown historical defect."); break;
            }
            RejectSemantic(payload);
        }

        [TestCase("opportunity-missing-owner")] [TestCase("opportunity-wrong-family")]
        [TestCase("opportunity-wrong-date")] [TestCase("fact-missing-owner")]
        public void TypedHistoricalProjectionsStillResolveTheirTrueStoredSource(string defect)
        {
            var payload = Baseline(2, "broken-deal"); var row = Canonical(payload, UnifiedCommitments.PlayerDeal);
            var opportunity = ((JArray)payload["ledger"]["opportunities"]).OfType<JObject>().Single(o => (string)o["id"] == (string)row["id"]);
            switch (defect)
            {
                case "opportunity-missing-owner": opportunity["id"] = "deal-player-missing"; break;
                case "opportunity-wrong-family": opportunity["kind"] = OpportunityKinds.Comp; break;
                case "opportunity-wrong-date": opportunity["week"] = (int)payload["week"] + 1; break;
                case "fact-missing-owner":
                    ((JArray)payload["story"]["facts"]).OfType<JObject>().Single(f => (string)f["refId"] == (string)row["id"])["refId"] = "deal-player-missing"; break;
                default: Assert.Fail("Unknown typed source defect."); break;
            }
            RejectSemantic(payload);
        }

        [TestCase("missing-link")] [TestCase("unresolved-link")]
        [TestCase("nonreciprocal-link")] [TestCase("wrong-price-target")]
        public void ActualPublicMixedSafetyVoteCounterRetainsBothTrueOwners(string defect)
        {
            var payload = Baseline(2, "mixed-counter"); var bought = Canonical(payload, UnifiedCommitments.CounterDeal);
            var price = ((JArray)payload["deals"]).OfType<JObject>().Single(d => (string)d["id"] == (string)bought["linkedCommitmentId"]);
            Assert.That((string)price["type"], Is.EqualTo(DealKind.VoteSave));
            Assert.That((string)price["linkedDealId"], Is.EqualTo((string)bought["id"]));
            switch (defect)
            {
                case "missing-link": bought["linkedCommitmentId"] = JValue.CreateNull(); break;
                case "unresolved-link": bought["linkedCommitmentId"] = "deal-price-missing"; break;
                case "nonreciprocal-link": price["linkedDealId"] = JValue.CreateNull(); break;
                case "wrong-price-target": price["targetId"] = payload["playerId"].DeepClone(); break;
                default: Assert.Fail("Unknown mixed-link defect."); break;
            }
            RejectSemantic(payload);
        }

        [TestCase("unknown-key")] [TestCase("promise-flag")] [TestCase("wrong-standing-source")]
        public void ActualCourtAssessmentAndPitchPromiseKeepTheirReservedFormer25Receipts(string defect)
        {
            var payload = Baseline(2, "pitch-assessed");
            var reply = ((JArray)payload["ledger"]["replies"]).OfType<JObject>().Single(r => (string)r["kind"] == ReplyCards.Pitch && (string)r["replyKey"] == HoHPitches.FeelOutKey);
            switch (defect)
            {
                case "unknown-key": reply["replyKey"] = "future-answer"; break;
                case "promise-flag": reply["promised"] = true; break;
                case "wrong-standing-source":
                    ((JArray)payload["ledger"]["standings"]).OfType<JObject>().First(s => (string)s["source"] == ClaimSource.Told)["source"] = "future"; break;
                default: Assert.Fail("Unknown reserved pitch defect."); break;
            }
            RejectSemantic(payload);
        }

        [TestCase("unknown-appeal")] [TestCase("wrong-speaker")] [TestCase("fraction-speech-week")]
        public void ActualPublicBlockSpeechAndItsTypedBroadcastCannotBeRelabelled(string defect)
        {
            var payload = Baseline(1, "block-speech");
            var broadcast = ((JArray)payload["events"]).OfType<JObject>().Single(e => BlockSpeeches.IsReceiptKind((string)e["kind"])
                && (string)e["audienceIds"][0] == (string)payload["playerId"]);
            var speech = ((JArray)payload["evictionSpeeches"]).OfType<JObject>().Single(s => (string)s["speakerId"] == (string)payload["playerId"]);
            switch (defect)
            {
                case "unknown-appeal": broadcast["kind"] = BlockSpeeches.EventPrefix + "future-appeal"; break;
                case "wrong-speaker": speech["speakerId"] = payload["hohId"].DeepClone(); break;
                case "fraction-speech-week": speech["week"] = (double)(int)speech["week"]; break;
                default: Assert.Fail("Unknown speech defect."); break;
            }
            if (defect == "fraction-speech-week") Reject(payload); else RejectSemantic(payload);
        }

        [TestCase("missing-moment")] [TestCase("wrong-theme")]
        [TestCase("unknown-jury-kind")] [TestCase("missing-jury-receipt")]
        public void ActualPlayerFinalistLocksOnlyResolvableHistoryAndJurorsOwnSourceReceipts(string defect)
        {
            var payload = Baseline(2, "final-argument");
            var argument = (JObject)payload["finalArgument"];
            Assert.That((JArray)argument["momentRefs"], Is.Not.Empty);
            var question = ((JArray)payload["juryExchanges"]).OfType<JObject>().First(q => (string)q["finalistId"] == (string)payload["playerId"]
                && q["receiptKind"].Type != JTokenType.Null && q["receiptId"].Type != JTokenType.Null);
            switch (defect)
            {
                case "missing-moment": argument["momentRefs"][0] = "deal:missing-source"; break;
                case "wrong-theme": argument["theme"] = "future-theme"; break;
                case "unknown-jury-kind": question["receiptKind"] = "future-receipt"; break;
                case "missing-jury-receipt": question["receiptId"] = "missing-source"; break;
                default: Assert.Fail("Unknown finale reference defect."); break;
            }
            RejectSemantic(payload);
        }

        [TestCase("root-null-reveals")] [TestCase("root-empty-reveals")]
        [TestCase("row-null-target")] [TestCase("row-known-target")]
        [TestCase("row-null-subtype")] [TestCase("row-vote-subtype")]
        public void ProposedFutureVoteFieldsAreNotAcceptedEvenWhenDisabledNullOrEmpty(string defect)
        {
            var payload = Baseline(1, "promise"); var row = Canonical(payload, UnifiedCommitments.PlayerPromise);
            switch (defect)
            {
                case "root-null-reveals": payload["unifiedVoteReveals"] = JValue.CreateNull(); break;
                case "root-empty-reveals": payload["unifiedVoteReveals"] = new JArray(); break;
                case "row-null-target": row["targetId"] = JValue.CreateNull(); break;
                case "row-known-target": row["targetId"] = row["beneficiaryId"].DeepClone(); break;
                case "row-null-subtype": row["subtype"] = JValue.CreateNull(); break;
                case "row-vote-subtype": row["subtype"] = DealKind.VoteSave; break;
                default: Assert.Fail("Unknown future shape defect."); break;
            }
            Reject(payload);
        }

        [TestCase("canonical")] [TestCase("evidence")] [TestCase("receipts")]
        public void PermanentBoundedAuthorityCannotBeWidenedByTheFrozenContract(string family)
        {
            var payload = Baseline(2, "spread");
            string field = family == "canonical" ? "unifiedCommitments" : family == "evidence" ? "unifiedHearingEvidence" : "unifiedHearingReceipts";
            int capacity = family == "canonical" ? 400 : family == "evidence" ? 400 : 6000;
            var rows = (JArray)payload[field]; var actual = rows[0].DeepClone();
            while (rows.Count <= capacity) rows.Add(actual.DeepClone());
            RejectSemantic(payload);
        }

        [Test]
        public void NullInputIsRefusedWithoutAReplacementOrDefaultAuthority()
        {
            Assert.Throws<InvalidDataException>(() => Validate(null));
        }

        private static JsonSerializer Serializer() => (JsonSerializer)JsonApi.GetMethod("Serializer", Static).Invoke(null, null);
        private static JObject Payload(EpisodeState state) => JObject.FromObject(state, Serializer());
        private static string Text(JToken value) => value.ToString(Formatting.None);
        private static void ValidateCurrentShape(JObject payload)
        {
            try { JsonApi.GetMethod("CheckDtoShape", Static).Invoke(null, new object[] { payload, typeof(EpisodeState), "state" }); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private static void AcceptCurrent(JObject payload)
        {
            string original = Text(payload);
            Assert.That(payload["schemaVersion"]?.Type, Is.EqualTo(JTokenType.Integer));
            Assert.That((int)payload["schemaVersion"], Is.EqualTo(28));
            Assert.DoesNotThrow(() => ValidateCurrentShape(payload));
            var state = payload.ToObject<EpisodeState>(Serializer()); string beforeState = Text(Payload(state));
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
            Assert.DoesNotThrow(() => EpisodeSaveValidation.Validate(state));
            Assert.That(Text(Payload(state)), Is.EqualTo(beforeState));
            Assert.That(Text(payload), Is.EqualTo(original));
        }
        // Shape-only lift also supports semantic NEGATIVES. It never repairs the defect, clears
        // authority, converts a raw mirror, or assumes the lifted state is semantically accepted.
        private static JObject LiftFormer25(JObject former)
        {
            Assert.That(former["schemaVersion"]?.Type, Is.EqualTo(JTokenType.Integer));
            Assert.That((int)former["schemaVersion"], Is.EqualTo(25));
            Assert.DoesNotThrow(() => ValidateFrozenShape(former));
            string original = Text(former); var current = (JObject)former.DeepClone();
            foreach (JObject row in (JArray)current["unifiedCommitments"])
            {
                row.Add("targetId", JValue.CreateNull()); row.Add("subtype", JValue.CreateNull());
                row.Add("voteBindingWeek", 0); row.Add("voteFirstRevealWeek", 0);
            }
            current.Add("unifiedVoteReveals", new JArray());
            // Schema 28's inert Wave D storage: three zero start weeks, D2's empty cadence, D3's empty plans.
            foreach (string week in new[] { "allianceLeakRulesStartWeek", "pactPlanRulesStartWeek", "allWeekRulesStartWeek" }) current.Add(week, 0);
            var social = (JObject)current["npcSocial"];
            social.Add("beatWeek", 0); social.Add("beatWindow", -1); social.Add("beatsFired", 0); social.Add("beatSeats", 0);
            social.Add("beatPlan", new JArray()); social.Add("acts", new JArray());
            ((JObject)current["ledger"]).Add("plans", new JArray());
            current["schemaVersion"] = 28;
            Assert.That(Text(former), Is.EqualTo(original)); return current;
        }
        private static JObject NeutralFormer25(JObject current)
        {
            // Current validity and all neutral extensions are proved BEFORE removal. Payload()
            // stays raw current JSON, so existing clone/Apply/input assertions see every field.
            AcceptCurrent(current); string original = Text(current);
            Assert.That((int)current["unifiedCommitmentRulesVersion"], Is.InRange(0, 1));
            Assert.That((int)current["unifiedHearingRulesVersion"], Is.InRange(0, 1));
            Assert.That(current["unifiedVoteReveals"], Is.TypeOf<JArray>());
            Assert.That((JArray)current["unifiedVoteReveals"], Is.Empty);
            // The native current28->27->26 helpers prove actual full validation, fixed27 and fixed26
            // acceptance and the real additive migration inverses BEFORE removing anything.
            var former = (JObject)current.DeepClone(); PersistenceMigrationTests.StripSchema27(former);
            Assert.That((int)former["schemaVersion"], Is.EqualTo(26)); former.Remove("unifiedVoteReveals");
            foreach (JObject row in (JArray)former["unifiedCommitments"])
            {
                Assert.That((string)row["kind"], Is.EqualTo(UnifiedCommitments.Safety));
                Assert.That(row.Property("targetId"), Is.Not.Null); Assert.That(row["targetId"].Type, Is.EqualTo(JTokenType.Null));
                Assert.That(row.Property("subtype"), Is.Not.Null); Assert.That(row["subtype"].Type, Is.EqualTo(JTokenType.Null));
                row.Remove("targetId"); row.Remove("subtype");
            }
            former["schemaVersion"] = 25;
            Assert.That(JToken.DeepEquals(LiftFormer25(former), current), Is.True, "The neutral bridge has an exact inverse; no other historical field is dropped.");
            Assert.That(Text(current), Is.EqualTo(original)); return former;
        }
        private static void Validate(JObject payload)
        {
            var method = Contract.GetMethod("Validate", Static);
            Assert.That(method, Is.Not.Null, "The unused historical contract has the reviewed Validate(JObject) API.");
            try { method.Invoke(null, new object[] { payload }); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private static void ValidateFrozenShape(JObject payload)
        {
            var shape = Contract.Assembly.GetType("Gamesim.Persistence.FrozenV25Shape", true);
            var method = shape.GetMethod("Validate", Static);
            Assert.That(method, Is.Not.Null, "Semantic negatives must first pass the fixed former25 shape.");
            try { method.Invoke(null, new object[] { payload }); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private static void Notify(string kind, JObject payload, string reason = null)
        {
            var observer = CorpusObserver;
            if (observer != null) observer(kind, (JObject)payload.DeepClone(), reason);
        }
        private static string PayloadHash(string actualPayload)
        {
            using (var digest = SHA256.Create())
                return BitConverter.ToString(digest.ComputeHash(Encoding.UTF8.GetBytes(actualPayload)))
                    .Replace("-", "").ToLowerInvariant();
        }
        private static JObject Coordinates(EpisodeState state, string actualPayload) => new JObject {
            ["seed"] = state.seed, ["sessionId"] = state.sessionId, ["revision"] = state.revision,
            ["week"] = state.week, ["phase"] = (int)state.phase, ["payloadSha256"] = PayloadHash(actualPayload) };
        private static void Accept(JObject payload)
        {
            string original = Text(payload);
            JObject former;
            if (payload["schemaVersion"]?.Type == JTokenType.Integer && (int)payload["schemaVersion"] == 28)
                former = NeutralFormer25(payload);
            else
            {
                Assert.DoesNotThrow(() => Validate(payload));
                AcceptCurrent(LiftFormer25(payload)); former = payload;
            }
            Assert.DoesNotThrow(() => Validate(former));
            Assert.That(Text(payload), Is.EqualTo(original), "Frozen validation never repairs/defaults/reorders the caller's complete JSON tree.");
            Notify("accepted", former);
        }
        private static void Reject(JObject payload)
        {
            string original = Text(payload);
            Assert.Throws<InvalidDataException>(() => Validate(payload));
            Assert.That(Text(payload), Is.EqualTo(original), "Refusal leaves every original scalar, nested field and array order untouched.");
        }
        private static void RejectSemantic(JObject payload)
        {
            string original = Text(payload);
            Assert.That(payload["schemaVersion"].Type, Is.EqualTo(JTokenType.Integer));
            Assert.That((int)payload["schemaVersion"], Is.EqualTo(25));
            Assert.DoesNotThrow(() => ValidateFrozenShape(payload), "A semantic refusal cannot be merely an incompatible DTO shape.");
            var current = LiftFormer25(payload); string currentOriginal = Text(current);
            Assert.DoesNotThrow(() => ValidateCurrentShape(current));
            var state = current.ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.False, "The source-valid baseline has a real former25 invariant defect.");
            Assert.That(reason, Is.Not.Empty);
            Assert.That(reason, Does.Not.Contain("Unsupported episode schema"), "The companion's literal28 header must not substitute for the intended semantic defect.");
            Assert.That(Text(current), Is.EqualTo(currentOriginal));
            Assert.That(Text(payload), Is.EqualTo(original));
            Reject(payload);
            Notify("semantic-refused", payload, reason);
        }
        private static JObject Baseline(int mode, string checkpoint)
        {
            var payload = Payload(Witness(mode, checkpoint)); Accept(payload); return NeutralFormer25(payload);
        }
        private static JObject Canonical(JObject payload, string origin) => ((JArray)payload["unifiedCommitments"]).OfType<JObject>().Single(r => (string)r["origin"] == origin && (string)r["makerId"] == (string)payload["playerId"]);
        private static JObject Evidence(JObject payload)
        {
            string own = (string)Canonical(payload, UnifiedCommitments.PlayerDeal)["id"];
            return ((JArray)payload["unifiedHearingEvidence"]).OfType<JObject>().Single(e => (string)e["fact"]["refId"] == own);
        }
        private static JObject Receipt(JObject payload, string kind)
        {
            string key = (string)Evidence(payload)["incidentKey"];
            return ((JArray)payload["unifiedHearingReceipts"]).OfType<JObject>().First(r => (string)r["incidentKey"] == key && (string)r["kind"] == kind);
        }
        private static void RemoveKnower(JObject fact, string id)
        {
            foreach (var knower in ((JArray)fact["knowers"]).Where(k => (string)k == id).ToArray()) knower.Remove();
        }

        private static EpisodeState Witness(int mode, string checkpoint)
        {
            string key = mode + ":" + checkpoint;
            lock (Witnesses)
            {
                // Memoize only fixture-owned, publicly played witnesses. No mutable source catalog,
                // engine singleton, disk/native cache or shared writable returned state is involved.
                if (!Witnesses.TryGetValue(key, out var owned)) Witnesses.Add(key, owned = BuildWitness(mode, checkpoint));
                string original = Text(Payload(owned)); var copy = owned.Clone();
                Assert.That(Text(Payload(copy)), Is.EqualTo(original));
                Assert.That(Text(Payload(owned)), Is.EqualTo(original));
                return copy;
            }
        }

        private static EpisodeState BuildWitness(int mode, string checkpoint)
        {
            if (checkpoint == "promise")
            {
                var engine = new EpisodeEngine(Fresh(mode, FirstSeed));
                var command = Command(engine.Snapshot, EpisodeCommandKind.PromiseSafety);
                command.targetId = engine.Snapshot.Active.First(c => !c.isPlayer).id;
                Commit(engine, command); return engine.Snapshot;
            }
            if (checkpoint == "debit")
            {
                var engine = new EpisodeEngine(Fresh(mode, FirstSeed));
                for (int i = 0; i < 2; i++)
                { var buy = Command(engine.Snapshot, EpisodeCommandKind.BuyActionPoint); buy.text = WebSocialVocabulary.SpreadAll; Commit(engine, buy); }
                for (int i = 0; i < 3; i++)
                { var talk = Command(engine.Snapshot, EpisodeCommandKind.Talk); talk.targetId = engine.Snapshot.Active.First(c => !c.isPlayer).id; Commit(engine, talk); }
                Commit(engine, Command(engine.Snapshot, EpisodeCommandKind.Advance));
                Assert.That(engine.Snapshot.moveInExtrasSpent, Is.GreaterThan(0)); return engine.Snapshot;
            }
            if (checkpoint == "mixed-counter") return FindMixedCounter(mode);
            if (checkpoint.StartsWith("pitch-", StringComparison.Ordinal)) return FindPitch(mode, checkpoint);
            if (checkpoint == "block-speech")
            {
                var engine = FindBoundary(mode, s => s.phase == EpisodePhase.Eviction && s.evictionStage == EvictionStage.Speeches
                    && s.nominees.Contains(s.playerId) && !s.evictionSpeeches.Any(e => e.speakerId == s.playerId), false);
                var speech = Command(engine.Snapshot, EpisodeCommandKind.SubmitEvictionSpeech);
                speech.text = "I own my game. <Exact & public>\nMy word stays in the record."; speech.secondTargetId = "strategic";
                Commit(engine, speech);
                Assert.That(engine.Snapshot.events.Any(e => BlockSpeeches.IsReceiptKind(e.kind) && e.audienceIds[0] == engine.Snapshot.playerId), Is.True);
                return engine.Snapshot;
            }
            if (checkpoint == "final-argument")
            {
                var engine = FindBoundary(mode, s => s.phase == EpisodePhase.JuryQuestioning && s.Active.Any(c => c.isPlayer)
                    && s.juryExchanges.Any(q => q.finalistId == s.playerId && q.receiptKind != null && q.receiptId != null), true);
                var moments = FinalArgument.Moments(engine.Snapshot);
                Assert.That(moments, Is.Not.Empty);
                var command = Command(engine.Snapshot, EpisodeCommandKind.LockFinalArgument);
                command.secondTargetId = FinalArgument.Emotional;
                command.text = string.Join("\n", moments.Take(FinalArgument.Required(engine.Snapshot)).Select(m => m.reference));
                Commit(engine, command); return engine.Snapshot;
            }
            bool promise = checkpoint == "broken-promise";
            bool broken = promise || checkpoint == "broken-deal" || checkpoint == "spread" || checkpoint == "archive-only" || checkpoint == "later-broken-deal";
            for (uint seed = FirstSeed; seed < EndSeed; seed++)
            {
                var context = HoHContext(mode, seed); if (context == null) continue;
                foreach (var partner in EpisodeEngine.NominationCandidates(context).Where(c => UnifiedCommitments.Binding(context, context.playerId, c.id).Count == 0))
                {
                    var engine = new EpisodeEngine(context);
                    var create = Command(context, promise ? EpisodeCommandKind.PromiseSafety : EpisodeCommandKind.ProposeDeal);
                    create.targetId = partner.id; if (!promise) create.text = DealKind.SafetyAgreement;
                    Commit(engine, create);
                    var row = engine.Snapshot.unifiedCommitments.SingleOrDefault(r => !context.unifiedCommitments.Any(old => old.id == r.id)
                        && r.origin == (promise ? UnifiedCommitments.PlayerPromise : UnifiedCommitments.PlayerDeal)
                        && r.makerId == context.playerId && r.beneficiaryId == partner.id);
                    if (row == null) { Assert.That(promise, Is.False, "Only an actual proposal can return no agreement."); continue; }
                    if (checkpoint == "deal") return engine.Snapshot;
                    var nominate = Command(engine.Snapshot, EpisodeCommandKind.Nominate);
                    var names = EpisodeEngine.NominationCandidates(engine.Snapshot).Where(c => broken || c.id != partner.id).Take(2).Select(c => c.id).ToArray();
                    nominate.targetId = broken ? partner.id : names[0];
                    nominate.secondTargetId = broken ? EpisodeEngine.NominationCandidates(engine.Snapshot).First(c => c.id != partner.id).id : names[1];
                    Commit(engine, nominate);
                    if (!broken)
                    {
                        Reach(engine, s => s.phase == EpisodePhase.VetoMeeting, 16);
                        var s = engine.Snapshot; var veto = Command(s, EpisodeCommandKind.Advance);
                        string saved = s.vetoHolderId == s.playerId ? null : EpisodeEngine.NpcVetoSave(s);
                        if (s.vetoHolderId == s.playerId || (s.hohId == s.playerId && saved != null))
                        {
                            veto.kind = EpisodeCommandKind.ResolveVeto; veto.useVeto = saved != null; veto.targetId = saved;
                            if (saved != null)
                            {
                                var replacement = EpisodeEngine.ReplacementCandidates(s).FirstOrDefault(c => c.id != partner.id);
                                if (replacement == null) continue; // Do not invent a legal protected replacement.
                                veto.secondTargetId = replacement.id;
                            }
                        }
                        Commit(engine, veto);
                        if (engine.Snapshot.unifiedCommitments.Single(r => r.id == row.id).status != DealStatus.Fulfilled) continue;
                        Assert.That(checkpoint, Is.EqualTo("spared-deal")); return engine.Snapshot;
                    }
                    var result = engine.Snapshot; var settled = result.unifiedCommitments.Single(r => r.id == row.id);
                    Assert.That(settled.status, Is.EqualTo(DealStatus.Broken)); Assert.That(settled.brokenById, Is.EqualTo(result.playerId));
                    if (checkpoint == "spread" && !result.unifiedHearingReceipts.Any(r => r.incidentKey == settled.settlementEffectKey && r.kind == UnifiedCommitmentHearings.Spread)) continue;
                    if (checkpoint == "later-broken-deal")
                    { Reach(engine, s => s.week > settled.settledWeek, 160); result = engine.Snapshot; }
                    if (checkpoint == "archive-only")
                    {
                        // Explicit detached after-pruning data projection, not a claimed public pruning
                        // command. The actual public writer/archive/Initial remain exact and permanent.
                        Assert.That(result.unifiedHearingEvidence.Any(e => e.fact.refId == row.id), Is.True);
                        result.story.facts.RemoveAll(f => f.refId == row.id);
                        Assert.That(EpisodeValidation.TryValidate(result, out string reason), Is.True, reason);
                    }
                    return result;
                }
            }
            Assert.Fail("No actual public " + checkpoint + " witness in the fixed 32-seed/real-target controls."); return null;
        }

        private static EpisodeState FindPitch(int mode, string checkpoint)
        {
            for (uint seed = FirstSeed; seed < EndSeed; seed++)
            {
                var s = HoHContext(mode, seed); if (s == null) continue;
                var pending = ReplyCards.Pending(s); if (pending == null || pending.kind != ReplyCards.Pitch) continue;
                var engine = new EpisodeEngine(s);
                if (checkpoint != "pitch-pending")
                { var assess = Command(s, EpisodeCommandKind.ReplyToHouseguest); assess.targetId = pending.id; assess.text = HoHPitches.FeelOutKey; Commit(engine, assess); }
                if (checkpoint == "pitch-promised")
                {
                    var answer = Command(engine.Snapshot, EpisodeCommandKind.ReplyToHouseguest); answer.targetId = pending.id; answer.text = "promise-safety";
                    Commit(engine, answer);
                    Assert.That(engine.Snapshot.unifiedCommitments.Any(r => r.origin == UnifiedCommitments.HoHPitch), Is.True);
                }
                return engine.Snapshot;
            }
            Assert.Fail("No actual automatically courted public pitch in the bounded source controls."); return null;
        }

        private static EpisodeState FindMixedCounter(int mode)
        {
            for (uint seed = FirstSeed; seed < EndSeed; seed++)
            {
                var engine = new EpisodeEngine(Fresh(mode, seed));
                for (int step = 0; step < 160 && engine.Snapshot.phase != EpisodePhase.Finished; step++)
                {
                    var context = engine.Snapshot;
                    if (context.phase == EpisodePhase.Campaign && EpisodeEngine.Voters(context).Any(v => v.isPlayer))
                        foreach (string npc in context.nominees)
                        {
                            if (!PlayerDeals.CanPropose(context, npc, DealKind.SafetyAgreement, null, out _)) continue;
                            var attempt = new EpisodeEngine(context);
                            var proposal = Command(context, EpisodeCommandKind.ProposeDeal); proposal.targetId = npc; proposal.text = DealKind.SafetyAgreement;
                            Commit(attempt, proposal);
                            var offered = attempt.Snapshot; var counter = Negotiation.OpenCounter(offered, npc);
                            if (counter == null || counter.kind != DealKind.SafetyAgreement || counter.price?.kind != DealKind.VoteSave) continue;
                            var yes = Command(offered, EpisodeCommandKind.RespondToDeal); yes.targetId = npc; yes.text = EpisodeEngine.AcceptDeal;
                            Commit(attempt, yes);
                            var result = attempt.Snapshot; var bought = result.unifiedCommitments.Single(r => r.origin == UnifiedCommitments.CounterDeal);
                            var price = result.deals.Single(d => d.id == bought.linkedCommitmentId);
                            Assert.That(price.type, Is.EqualTo(DealKind.VoteSave)); Assert.That(price.linkedDealId, Is.EqualTo(bought.id));
                            Assert.That(price.targetId, Is.EqualTo(npc)); return result;
                        }
                    Commit(engine, Next(context));
                }
            }
            Assert.Fail("No genuine refused public proposal/standing mixed VoteSave counter in 32 played source seeds."); return null;
        }

        private static EpisodeEngine FindBoundary(int mode, Func<EpisodeState, bool> reached, bool survival)
        {
            for (uint seed = FirstSeed; seed < EndSeed; seed++)
            {
                var engine = new EpisodeEngine(Fresh(mode, seed));
                for (int step = 0; step < 512 && engine.Snapshot.phase != EpisodePhase.Finished; step++)
                {
                    var state = engine.Snapshot; if (reached(state)) return engine;
                    var command = Next(state, survival);
                    if (!survival && command.kind == EpisodeCommandKind.Compete) command.performance = 0;
                    Commit(engine, command);
                }
            }
            Assert.Fail("No actual public boundary in the fixed 32-seed/512-command source controls."); return null;
        }

        private static EpisodeState HoHContext(int mode, uint seed)
        {
            var engine = new EpisodeEngine(Fresh(mode, seed));
            Commit(engine, Command(engine.Snapshot, EpisodeCommandKind.Advance));
            var compete = Command(engine.Snapshot, EpisodeCommandKind.Compete); compete.performance = 1;
            Commit(engine, compete); if (engine.Snapshot.hohId != engine.Snapshot.playerId) return null;
            Commit(engine, Command(engine.Snapshot, EpisodeCommandKind.Advance));
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Nomination));
            Assert.That(engine.Snapshot.nominees, Is.Empty); return engine.Snapshot;
        }

        private static EpisodeState Fresh(int mode, uint seed, int size = 6)
        {
            var state = size == 6 ? ContentCatalog.Create(seed) : SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);
            Assert.That(state.unifiedCommitmentRulesVersion, Is.Zero); Assert.That(state.unifiedHearingRulesVersion, Is.Zero);
            Assert.That(state.unifiedCommitments, Is.Empty);
            Assert.That(state.promises.Any(p => p.kind == PromiseKind.Safety), Is.False);
            Assert.That(state.deals.Any(d => d.type == DealKind.SafetyAgreement), Is.False);
            state.competitionRulesVersion = CompetitionRules.Current; state.haveNotRulesStartWeek = state.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableStory(state); EpisodeEngine.EnableRead(state); EpisodeEngine.EnableLevers(state);
            EpisodeEngine.EnableWeek(state); EpisodeEngine.EnableEconomy(state); EpisodeEngine.EnableAgency(state);
            EpisodeEngine.EnableFinale(state); EpisodeEngine.EnableCommitments(state);
            // Test-only fresh selection. No existing record, phase, role, history, seed or random
            // stream is changed; actual StartSeason/disk activation has independent native tests.
            state.unifiedCommitmentRulesVersion = mode == 0 ? 0 : 1;
            state.unifiedHearingRulesVersion = mode == 2 ? 1 : 0;
            AssertMode(state, mode); return state;
        }

        private static void AssertMode(EpisodeState state, int mode)
        {
            Assert.That(state.schemaVersion, Is.EqualTo(28));
            Assert.That((int)NeutralFormer25(Payload(state))["schemaVersion"], Is.EqualTo(25));
            Assert.That(state.unifiedCommitmentRulesVersion, Is.EqualTo(mode == 0 ? 0 : 1));
            Assert.That(state.unifiedHearingRulesVersion, Is.EqualTo(mode == 2 ? 1 : 0));
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
            if (mode == 0) Assert.That(state.unifiedCommitments, Is.Empty);
            else
            {
                Assert.That(state.promises.Any(p => p.kind == PromiseKind.Safety), Is.False);
                Assert.That(state.deals.Any(d => d.type == DealKind.SafetyAgreement), Is.False);
            }
            if (mode != 2) { Assert.That(state.unifiedHearingEvidence, Is.Empty); Assert.That(state.unifiedHearingReceipts, Is.Empty); }
        }

        private static EpisodeCommand Command(EpisodeState state, EpisodeCommandKind kind) => new EpisodeCommand {
            id = "frozen25-public-" + state.revision + "-" + kind, actorId = state.playerId,
            expectedRevision = state.revision, expectedPhase = state.phase, kind = kind };

        private static CommandResult Commit(EpisodeEngine engine, EpisodeCommand command)
        {
            var before = engine.Snapshot; string original = Text(Payload(before));
            string commandBefore = JObject.FromObject(command, Serializer()).ToString(Formatting.None);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, "Seed " + before.seed + ", " + before.phase + ", " + command.kind + ": " + result.reason);
            Assert.That(result.duplicate, Is.False);
            Assert.That(result.state.revision, Is.EqualTo(before.revision + 1));
            Assert.That(result.state.acceptedCommandIds.Last(), Is.EqualTo(command.id));
            Assert.That(Text(Payload(before)), Is.EqualTo(original));
            Assert.That(JObject.FromObject(command, Serializer()).ToString(Formatting.None), Is.EqualTo(commandBefore));
            Assert.That(JToken.DeepEquals(Payload(result.state), Payload(engine.Snapshot)), Is.True);
            Assert.That(EpisodeValidation.TryValidate(result.state, out string reason), Is.True, reason);
            if (CorpusObserver != null)
            {
                // Actual accepted public Apply, not an inference from a history row. Helper
                // intermediates are current-validated; only separate accepted observations
                // establish complete frozen25 acceptance of a particular payload.
                var packet = new JObject {
                    ["command"] = JObject.Parse(commandBefore),
                    ["before"] = Coordinates(before, original),
                    ["after"] = Coordinates(result.state, Text(Payload(result.state))) };
                Notify("accepted-public-command", packet);
            }
            return result;
        }

        private static void Reach(EpisodeEngine engine, Func<EpisodeState, bool> reached, int bound)
        {
            for (int step = 0; step < bound && !reached(engine.Snapshot) && engine.Snapshot.phase != EpisodePhase.Finished; step++) Commit(engine, Next(engine.Snapshot));
            Assert.That(reached(engine.Snapshot), Is.True, "The real source commands must reach this boundary without assigning it.");
        }

        private static EpisodeCommand Next(EpisodeState state, bool survival = true)
        {
            var command = Command(state, EpisodeCommandKind.Advance);
            if (state.pendingDiary != null) { command.kind = EpisodeCommandKind.SkipDiary; command.targetId = state.pendingDiary.id; }
            else if (EpisodeEngine.IsCompetition(state.phase) && !state.competitionResolved && EpisodeEngine.CompetitionPlayers(state).Any(c => c.isPlayer))
            { command.kind = EpisodeCommandKind.Compete; command.performance = 1; }
            else if (state.phase == EpisodePhase.Nomination && state.nominees.Count == 0 && state.hohId == state.playerId)
            {
                var names = EpisodeEngine.NominationCandidates(state).Take(2).ToArray();
                command.kind = EpisodeCommandKind.Nominate; command.targetId = names[0].id; command.secondTargetId = names[1].id;
            }
            else if (state.phase == EpisodePhase.VetoMeeting && !state.vetoResolved)
            {
                string saved = state.vetoHolderId == state.playerId ? null : EpisodeEngine.NpcVetoSave(state);
                if (state.vetoHolderId == state.playerId || (state.hohId == state.playerId && saved != null))
                {
                    command.kind = EpisodeCommandKind.ResolveVeto; command.useVeto = saved != null; command.targetId = saved;
                    command.secondTargetId = saved == null ? null : EpisodeEngine.ReplacementCandidates(state).First().id;
                    if (survival && state.vetoHolderId == state.playerId && state.nominees.Contains(state.playerId)
                        && !EpisodeEngine.VetoIsLockedAtFinalFour(state) && EpisodeEngine.ReplacementCandidates(state).Any())
                    { command.useVeto = true; command.targetId = state.playerId; command.secondTargetId = null; }
                }
            }
            else if (state.phase == EpisodePhase.Eviction && state.evictionStage == EvictionStage.Speeches && state.nominees.Contains(state.playerId)
                && !state.evictionSpeeches.Any(e => e.speakerId == state.playerId))
            { command.kind = EpisodeCommandKind.SubmitEvictionSpeech; command.text = "I own the game I played."; }
            else if (state.phase == EpisodePhase.Eviction && !state.evictionResolved
                && (state.evictionStage == EvictionStage.Voting || state.evictionStage == EvictionStage.Tiebreaker)
                && !state.votes.Any(v => v.voterId == state.playerId)
                && (EpisodeEngine.Voters(state).Any(c => c.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(state)))
            { command.kind = EpisodeCommandKind.CastVote; command.targetId = state.nominees[0]; }
            else if (state.phase == EpisodePhase.FinalEviction && state.hohId == state.playerId)
            { command.kind = EpisodeCommandKind.FinalEvict; command.targetId = state.Active.First(c => !c.isPlayer).id; }
            else if (state.phase == EpisodePhase.JuryQuestioning && !state.juryExchanges[state.juryQuestionIndex].completed)
            {
                var q = state.juryExchanges[state.juryQuestionIndex]; command.kind = EpisodeCommandKind.AnswerJury;
                command.targetId = q.finalistId == state.playerId ? q.questionerId : q.finalistId;
                command.secondTargetId = q.finalistId == state.playerId ? FinaleQuestions.Offered(q.category, q.receiptKind)[0]
                    : WebJuryQuestioning.GetJurorQuestionOptions(state.juryQuestionIndex)[0].tone;
            }
            else if (state.phase == EpisodePhase.FinalSpeeches && state.Active.Any(c => c.isPlayer) && !state.finalSpeeches.Any(e => e.speakerId == state.playerId))
            { command.kind = EpisodeCommandKind.SubmitSpeech; command.text = FinalArgument.Speech(state); }
            else if (state.phase == EpisodePhase.Jury && !state.Active.Any(c => c.isPlayer) && !state.votes.Any(v => v.voterId == state.playerId))
            { command.kind = EpisodeCommandKind.CastVote; command.targetId = state.Active.First().id; }
            return command;
        }
    }
}
