#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Prospective native tests from ONE retained genuine public-route source, not a new search.
    /// Its carrier is diagnostic public-field DTO bytes, never a native Store envelope. The ONLY
    /// construction edit enables the unpublished dialogue pilot, as the existing pilot tests do.
    /// Real Store operations below are separate runtime observations on uniquely owned temp slots.
    /// </summary>
    public sealed class DialoguePublicRescueV2Tests
    {
        private const string OriginalSha = "D3E0C73F5A10BCD85ED37766AE462B6435E588DA08D7E2FFAADF7C70F5BD4D71";
        private const string V2 = "dialogue-campaign-v2";
        private EpisodeState source;
        private string sourceImage, voter;
        private static readonly JsonSerializerSettings Fields = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None, DateParseHandling = DateParseHandling.None,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            FloatParseHandling = FloatParseHandling.Double, MaxDepth = 64, ContractResolver = new PublicFieldsOnly()
        };
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly Type PendingType = typeof(EpisodeEngine).GetNestedType("PendingDialogueCommand", BindingFlags.NonPublic);
        private static readonly FieldInfo Applying = typeof(EpisodeEngine).GetField("applyingDialogueCandidate", Instance);
        private static readonly FieldInfo Registered = typeof(EpisodeEngine).GetField("pendingDialogue", Instance);

        [OneTimeSetUp]
        public void LoadActualRetainedSourceWithoutPlayingOrInventingItsHistory()
        {
            string path = Path.Combine(Application.dataPath, "Gamesim/Tests/EditMode/Fixtures/DialoguePublicRescueV2/preentry-carrier.json");
            Assert.That(File.Exists(path), Is.True, "The reviewed immutable carrier must be explicitly admitted; no generation fallback.");
            var carrier = JObject.Parse(File.ReadAllText(path, new UTF8Encoding(false, true)));
            Assert.That((string)carrier["kind"], Is.EqualTo("immutable-diagnostic-public-field-dto-carrier-not-savewire"));
            byte[] bytes = Convert.FromBase64String((string)carrier["base64"]);
            Assert.That(bytes.Length, Is.EqualTo(173132));
            Assert.That(Sha(bytes), Is.EqualTo(OriginalSha));
            Assert.That((string)carrier["sha256"], Is.EqualTo(OriginalSha));
            Assert.That(Convert.ToBase64String(bytes), Is.EqualTo((string)carrier["base64"]));
            string original = new UTF8Encoding(false, true).GetString(bytes);
            var actual = JsonConvert.DeserializeObject<EpisodeState>(original, Fields);
            Assert.That(Image(actual), Is.EqualTo(original), "Exact typed original round trip BEFORE any declared rule change.");
            Assert.That(actual.week, Is.EqualTo(5)); Assert.That(actual.revision, Is.EqualTo(79));
            Assert.That(actual.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(actual.dialogueRulesVersion, Is.Zero, "The retained original is not silently reinterpreted as shipping-v2.");
            Valid(actual);
            source = actual.Clone();
            Assert.That(Image(source), Is.EqualTo(original), "The clone itself may not alter any original field or number.");
            source.dialogueRulesVersion = 1; // Declared pilot test activation ONLY; no role/AP/RNG/history edit.
            var allowed = Parse(original); allowed["dialogueRulesVersion"] = 1;
            Assert.That(ExactCapturedJson.Equal(Parse(Image(source)), allowed), Is.True,
                "Complete ordered/type/value equality after the SOLE declared pilot activation.");
            Valid(source); sourceImage = Image(source);
            var voters = EpisodeEngine.Voters(source).Where(p => !p.isPlayer).ToArray();
            Assert.That(voters.Length, Is.EqualTo(1)); voter = voters[0].id;
            Assert.That(voters[0].name, Does.StartWith("Alex"));
            Assert.That(Ap(source), Is.EqualTo(2));
            Assert.That(source.ledger.power.Single(p => p.week == 2).vetoHolderId, Is.EqualTo(voter));
            Assert.That(source.ledger.power.Single(p => p.week == 2).savedId, Is.EqualTo(source.playerId));
        }

        [TearDown]
        public void OriginalRetainedInputIsNeverMutated()
        { if (source != null) Assert.That(Image(source), Is.EqualTo(sourceImage)); }

        [Test]
        public void ClosedV2EntryIsOrdinaryFundedAndNeverJoinsTheAmbientCatalog()
        {
            Assert.That(StoryCatalog.All.Any(t => t.id == V2 || t.id == DialogueCampaignV1.TemplateId), Is.False);
            var first = StoryCatalog.Find(V2); var second = StoryCatalog.Find(V2);
            Assert.That(first, Is.Not.Null.And.Not.SameAs(second));
            first.title = "not authority";
            var engine = new EpisodeEngine(source);
            var entered = Enter(engine);
            var episode = Active(entered); var cycle = Cycle(entered, episode);
            Assert.That(cycle.templateId, Is.EqualTo(V2)); Assert.That(episode.contentVersion, Is.EqualTo(2));
            Assert.That(cycle.title, Is.EqualTo(DialogueCampaignV1.Title));
            Assert.That(episode.occasionId, Is.EqualTo(DialogueRules.Occasion(5, voter)));
            Assert.That(Ap(entered), Is.EqualTo(1));
            Assert.That(entered.outOfPhaseSocialActions, Is.EqualTo(source.outOfPhaseSocialActions + 1));
            Assert.That(entered.windowActions[Windows.AfterVeto], Is.EqualTo(source.windowActions[Windows.AfterVeto] + 1));
            Assert.That(episode.evidence, Is.Empty); Assert.That(cycle.path.Count, Is.EqualTo(1));
            Assert.That(entered.houseEvents.Count(e => e.cycleId == episode.id), Is.EqualTo(2));
            Assert.That(entered.schemaVersion, Is.EqualTo(29));
        }

        [TestCase("crossed-v1")]
        [TestCase("unknown-version")]
        [TestCase("unknown-template")]
        [TestCase("disabled")]
        [TestCase("orphan-cycle")]
        [TestCase("orphan-event")]
        public void ClosedPairDisabledAndOrphanReadersRejectWithoutMutation(string corruption)
        {
            var engine = new EpisodeEngine(source); var state = Enter(engine); var episode = Active(state); var cycle = Cycle(state, episode);
            switch (corruption)
            {
                case "crossed-v1": episode.contentVersion = 1; break;
                case "unknown-version": episode.contentVersion = 3; break;
                case "unknown-template": cycle.templateId = "dialogue-campaign-v3"; break;
                case "disabled": state.dialogueRulesVersion = 0; break;
                case "orphan-cycle": state.dialogue.episodes.Clear(); break;
                case "orphan-event": state.houseEvents.Single(e => e.id == cycle.eventId).cycleId = "missing-dialogue"; break;
                default: Assert.Fail("Unknown fixed case."); break;
            }
            string before = Image(state);
            Assert.That(EpisodeValidation.TryValidate(state, out string error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty); Assert.That(Image(state), Is.EqualTo(before));
        }

        [Test]
        public void AllThreeGenericStoryEntrypointsRefuseV2WithoutWriting()
        {
            var state = source.Clone(); string before = Image(state);
            Assert.That(EpisodeEngine.StartStory(state, V2, StoryAnchors.BlockSet), Is.False);
            Assert.That(Image(state), Is.EqualTo(before));
            var startCycle = typeof(EpisodeEngine).GetMethod("StartCycle", Static);
            Assert.That(startCycle, Is.Not.Null);
            Assert.That(Invoke(startCycle, null, new object[] { state, StoryCatalog.Find(V2), null, StoryAnchors.BlockSet, null, null }), Is.Null);
            Assert.That(Image(state), Is.EqualTo(before));
            var engine = new EpisodeEngine(state); var entered = Enter(engine); var episode = Active(entered); var cycle = Cycle(entered, episode);
            var request = Command(entered, EpisodeCommandKind.ProgressStoryline);
            request.targetId = cycle.eventId; request.secondTargetId = DialogueCampaignV2.Cite;
            RefusedUnchanged(engine, request);
        }

        [Test]
        public void PublicProjectionIsDetachedFourFieldFactAndBrowsingNeverSpendsOrRolls()
        {
            var engine = new EpisodeEngine(source); var state = Enter(engine); var episode = Active(state); var cycle = Cycle(state, episode);
            string before = Image(state);
            Assert.That(DialogueRules.TryReadPublicRescue(state, cycle.eventId, out var fact), Is.True);
            Assert.That(fact.Week, Is.EqualTo(2)); Assert.That(fact.HolderId, Is.EqualTo(voter));
            Assert.That(fact.SavedId, Is.EqualTo(state.playerId));
            Assert.That(fact.ReplacementId, Is.EqualTo(state.ledger.power.Single(p => p.week == 2).replacementId));
            Assert.That(fact.GetType().GetProperties().Select(p => p.Name), Is.EquivalentTo(new[] { "Week", "HolderId", "SavedId", "ReplacementId" }));
            Assert.That(fact.GetType().GetFields(), Is.Empty);
            Assert.That(fact.GetType().GetProperties().Any(p => p.CanWrite), Is.False);
            Assert.That(DialogueRules.TryReadPublicRescue(state, cycle.eventId, out var nextFact), Is.True);
            Assert.That(nextFact, Is.Not.SameAs(fact)); Assert.That(Image(state), Is.EqualTo(before));
            Assert.That(Image(engine.Snapshot), Is.EqualTo(before));
        }

        [Test]
        public void ActualCitationAtAp0CapturesOneDeepProofWithoutBonusOrExtraTickAndResumes()
        {
            var engine = new EpisodeEngine(source); var entered = Enter(engine);
            var talk = Command(entered, EpisodeCommandKind.Talk);
            talk.targetId = entered.Active.First(p => !p.isPlayer && p.id != voter).id;
            var exhausted = Accepted(engine, talk);
            Assert.That(Ap(exhausted), Is.Zero, "One actual legal Talk, not counter mutation.");
            string before = Image(exhausted); uint main = exhausted.randomState, npc = exhausted.npcSocial.randomState;
            var cited = Accepted(engine, Choice(exhausted, DialogueCampaignV2.Cite));
            var episode = Active(cited); var row = episode.evidence.Single();
            Assert.That(row.kind, Is.EqualTo("power")); Assert.That(row.pathIndex, Is.EqualTo(1));
            Assert.That(row.capturedRevision, Is.EqualTo(cited.revision));
            Assert.That(row.eventId, Is.EqualTo(cited.houseEvents.Where(e => e.cycleId == episode.id).ElementAt(1).id));
            Assert.That(row.memory, Is.Null); Assert.That(row.statementId, Is.Null);
            var originalPower = exhausted.ledger.power.Single(p => p.week == 2);
            Assert.That(Image(row.power), Is.EqualTo(Image(originalPower)));
            Assert.That(row.power, Is.Not.SameAs(originalPower));
            Assert.That(row.power.nominees, Is.Not.SameAs(originalPower.nominees));
            Assert.That(row.power.tally, Is.Not.SameAs(originalPower.tally));
            Assert.That(cited.randomState, Is.EqualTo(main)); Assert.That(cited.npcSocial.randomState, Is.EqualTo(npc));
            Assert.That(cited.windowActions, Is.EqualTo(exhausted.windowActions));
            Assert.That(cited.outOfPhaseSocialActions, Is.EqualTo(exhausted.outOfPhaseSocialActions));
            Assert.That(Image(cited.relationships), Is.EqualTo(Image(exhausted.relationships)));
            Assert.That(Image(cited.relationshipArcs), Is.EqualTo(Image(exhausted.relationshipArcs)));
            Assert.That(Image(cited.memories), Is.EqualTo(Image(exhausted.memories)));
            Assert.That(cited.lobbies.Count, Is.EqualTo(exhausted.lobbies.Count));
            Assert.That(Image(exhausted), Is.EqualTo(before));
            using var slot = new OwnedSlot(); slot.Store.Save(cited); slot.Store.Save(cited);
            Assert.That(slot.Image().Keys, Is.EquivalentTo(new[] { "episode.json", "episode.json.backup" }));
            Assert.That(slot.Store.TryLoad(out var restored, out string loadError), Is.True, loadError);
            Assert.That(Image(restored), Is.EqualTo(Image(cited)), "This is an ACTUAL Store round trip, not the diagnostic carrier.");
            var resumed = new EpisodeEngine(restored);
            var answered = Accepted(resumed, Choice(restored, DialogueCampaignV1.Plea));
            var finished = answered.dialogue.episodes.Single();
            Assert.That(finished.evidence.Count, Is.EqualTo(1)); Assert.That(Image(finished.evidence.Single()), Is.EqualTo(Image(row)));
            Assert.That(finished.time.proposedDelta, Is.Zero); Assert.That(finished.time.appliedDelta, Is.Zero);
            Assert.That(answered.windowActions, Is.EqualTo(restored.windowActions));
            Assert.That(answered.outOfPhaseSocialActions, Is.EqualTo(restored.outOfPhaseSocialActions));
            Assert.That(answered.lobbies.Last().approach, Is.EqualTo(LobbyApproach.Strategic));
            Assert.That(answered.houseEvents.Count(e => e.cycleId == finished.id), Is.EqualTo(3));
            Assert.That(Active(answered), Is.Null);
        }

        /// <summary>
        /// The owner's check (OWNER-DECISIONS-2026-10-22 §4): a public Cite at AP 0 must not deliver a paid plea's effect
        /// for free. At AP 0 the paid plea itself, a vote plea to the same voter in any approach, is refused for want of a
        /// conversation. The Cite is accepted there and writes only its command, conversation, event and log line: no plea
        /// record, relationship, arc, memory, ballot, deal, promise or commitment moves, no roll is drawn and nothing is
        /// spent. The one plea this conversation delivers is the one its paid entry funded, once, at the end: it closes the
        /// conversation, and a vote plea to the same voter is refused after it in every approach.
        /// </summary>
        [Test]
        public void PublicCiteAtAp0DeliversNoneOfAPaidPleasEffectForFree()
        {
            var engine = new EpisodeEngine(source); var entered = Enter(engine);
            var talk = Command(entered, EpisodeCommandKind.Talk);
            talk.targetId = entered.Active.First(p => !p.isPlayer && p.id != voter).id;
            var exhausted = Accepted(engine, talk);
            Assert.That(Ap(exhausted), Is.Zero, "One actual legal Talk, not counter mutation.");
            foreach (string approach in LobbyApproach.All)
            {
                Assert.That(new EpisodeEngine(exhausted).Apply(VotePlea(exhausted, approach)).reason, Is.EqualTo("You have no conversations left this week."),
                    approach + ": the plea is paid.");
                RefusedUnchanged(engine, VotePlea(exhausted, approach));
            }

            var cited = Accepted(engine, Choice(exhausted, DialogueCampaignV2.Cite));
            JObject before = Parse(Image(exhausted)), after = Parse(Image(cited));
            Assert.That(after.Properties().Select(p => p.Name), Is.EqualTo(before.Properties().Select(p => p.Name)));
            Assert.That(after.Properties().Where(p => !ExactCapturedJson.Equal(p.Value, before[p.Name])).Select(p => p.Name),
                Is.EquivalentTo(new[] { "revision", "nextSequence", "acceptedCommandIds", "events", "houseEvents", "storylines", "dialogue" }),
                "The Cite writes only its command, its own conversation, its event and its log line.");
            foreach (string field in new[] { "lobbies", "relationships", "relationshipArcs", "memories", "votes", "deals", "promises",
                         "unifiedCommitments", "randomState", "npcSocial", "windowActions", "outOfPhaseSocialActions", "socialActions" })
            {
                Assert.That(before.Property(field), Is.Not.Null, field);
                Assert.That(ExactCapturedJson.Equal(after[field], before[field]), Is.True, field + ": a paid plea's effect, delivered free by the Cite.");
            }
            Assert.That(Active(cited).responses.Last().responseKey, Is.EqualTo(V2 + "/explain-that-record"), "The voter answers the record, not a plea.");

            var answered = Accepted(engine, Choice(cited, DialogueCampaignV1.Plea));
            Assert.That(answered.lobbies.Count, Is.EqualTo(cited.lobbies.Count + 1), "The plea the paid entry funded, delivered once.");
            var lobby = answered.lobbies.Last();
            Assert.That(new[] { lobby.deciderId, lobby.ask, lobby.approach, lobby.subjectId },
                Is.EqualTo(new[] { voter, LobbyAsk.Vote, LobbyApproach.Strategic, answered.playerId }));
            Assert.That(answered.randomState, Is.Not.EqualTo(cited.randomState), "A plea's answer is a roll; the Cite drew none.");
            Assert.That(answered.windowActions, Is.EqualTo(cited.windowActions));
            Assert.That(answered.outOfPhaseSocialActions, Is.EqualTo(cited.outOfPhaseSocialActions));
            Assert.That(Active(answered), Is.Null, "The plea closes the conversation; nothing is left open to deliver another.");
            foreach (string approach in LobbyApproach.All) RefusedUnchanged(engine, VotePlea(answered, approach));
        }

        [TestCase("week")]
        [TestCase("hohId")]
        [TestCase("vetoHolderId")]
        [TestCase("savedId")]
        [TestCase("replacementId")]
        [TestCase("evicteeId")]
        [TestCase("backdoorTargetId")]
        [TestCase("backdoorResult")]
        [TestCase("vetoUsed")]
        [TestCase("nominees")]
        [TestCase("tally")]
        public void EveryRetainedPowerFieldAndOrderedListIsSourceBoundAndRestorable(string field)
        {
            var state = Cited(); var row = Active(state).evidence.Single(); var original = row.power.Clone();
            string good = Image(state);
            switch (field)
            {
                case "week": row.power.week = state.week; break;
                case "hohId": row.power.hohId = state.playerId; break;
                case "vetoHolderId": row.power.vetoHolderId = state.playerId; break;
                case "savedId": row.power.savedId = voter; break;
                case "replacementId": row.power.replacementId = state.playerId; break;
                case "evicteeId": row.power.evicteeId = state.playerId; break;
                case "backdoorTargetId": row.power.backdoorTargetId = "not-the-source"; break;
                case "backdoorResult": row.power.backdoorResult = "not-the-source"; break;
                case "vetoUsed": row.power.vetoUsed = false; break;
                case "nominees": row.power.nominees.Reverse(); break;
                case "tally": row.power.tally[0]++; break;
                default: Assert.Fail("Unknown exact field."); break;
            }
            Assert.That(Image(state), Is.Not.EqualTo(good));
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False);
            row.power = original; Assert.That(Image(state), Is.EqualTo(good)); Valid(state);
        }

        [TestCase("id")]
        [TestCase("event")]
        [TestCase("path")]
        [TestCase("kind")]
        [TestCase("memory")]
        [TestCase("statement")]
        [TestCase("duplicate")]
        [TestCase("missing-source")]
        public void CitationRejectsWrongIdentityArmsDuplicateAndMissingSourceWithoutDowngrade(string corruption)
        {
            var state = Cited(); var episode = Active(state); var row = episode.evidence.Single();
            switch (corruption)
            {
                case "id": row.id = "dialogue-evidence-1"; break;
                case "event": row.eventId = episode.entryEventId; break;
                case "path": row.pathIndex = 0; break;
                case "kind": row.kind = "memory"; break;
                case "memory": row.memory = new MemoryState(); break;
                case "statement": row.statementId = "not-a-source-statement"; break;
                case "duplicate": episode.evidence.Add(row.Clone()); break;
                case "missing-source": state.ledger.power.RemoveAll(p => p.week == row.power.week); break;
                default: Assert.Fail("Unknown exact corruption."); break;
            }
            string before = Image(state);
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False);
            Assert.That(Image(state), Is.EqualTo(before));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(1)]
        [TestCase(2)]
        public void CommittedEvidenceCannotForgeZeroNegativeOrFutureRevision(int delta)
        {
            var state = Cited(); var row = Active(state).evidence.Single();
            int original = row.capturedRevision;
            row.capturedRevision = delta <= 0 ? delta : state.revision + delta;
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False,
                "Only evidence changes; valid committed response/time rows cannot mask this check.");
            row.capturedRevision = original; Valid(state);
        }

        [Test]
        public void ExactLiveEvidenceNeedsObjectIdentityEvenWhenPrivateVerifyAcceptsEqualClone()
        {
            var engine = new EpisodeEngine(source); var entered = Enter(engine);
            using var pending = new PendingHarness(engine, Choice(entered, DialogueCampaignV2.Cite));
            var episode = Active(pending.Candidate); var evidence = episode.evidence.Single();
            string before = Image(pending.Candidate);
            Assert.That(CheckPending(pending.Candidate, pending.Handle, out _), Is.True);
            Assert.That(EpisodeValidation.TryValidate(pending.Candidate, out _), Is.False);
            using var slot = new OwnedSlot(); slot.Store.Save(pending.Source); slot.Store.Save(pending.Source); var saved = slot.Image();
            Assert.That(saved.Keys, Is.EquivalentTo(new[] { "episode.json", "episode.json.backup" }));
            Assert.Throws<InvalidDataException>(() => slot.Store.Save(pending.Candidate)); slot.AssertImage(saved);
            episode.evidence[0] = evidence.Clone();
            Assert.That(Image(pending.Candidate), Is.EqualTo(before));
            Assert.That(Verify(pending.Candidate, pending.Handle, out _), Is.True,
                "Value-sealed dialogue and exact future-object identity are deliberately separate checks.");
            Assert.That(CheckPending(pending.Candidate, pending.Handle, out _), Is.False);
            episode.evidence[0] = evidence;
            Assert.That(CheckPending(pending.Candidate, pending.Handle, out string restored), Is.True, restored);
            Assert.That(CheckPending(pending.Candidate.Clone(), pending.Handle, out _), Is.False);
            var foreignEngine = new EpisodeEngine(source); var foreignEntered = Enter(foreignEngine);
            using var foreign = new PendingHarness(foreignEngine, Choice(foreignEntered, DialogueCampaignV2.Cite));
            Assert.That(CheckPending(pending.Candidate, foreign.Handle, out _), Is.False);
            pending.AssertSourceUnchanged(); slot.AssertImage(saved);
            pending.Retire(); Assert.That(CheckPending(pending.Candidate, pending.Handle, out _), Is.False);
        }

        [Test]
        public void PrivateExpectedEvidenceIsNotAliasedToCandidateOrSourceAndTamperRestores()
        {
            var engine = new EpisodeEngine(source); var entered = Enter(engine);
            using var pending = new PendingHarness(engine, Choice(entered, DialogueCampaignV2.Cite));
            var row = Active(pending.Candidate).evidence.Single(); string original = row.power.backdoorResult;
            row.power.backdoorResult = "tampered-private-candidate";
            Assert.That(Verify(pending.Candidate, pending.Handle, out _), Is.False);
            Assert.That(CheckPending(pending.Candidate, pending.Handle, out _), Is.False);
            row.power.backdoorResult = original;
            Assert.That(Verify(pending.Candidate, pending.Handle, out _), Is.True);
            Assert.That(CheckPending(pending.Candidate, pending.Handle, out _), Is.True);
            var list = row.power.nominees; list.Reverse();
            Assert.That(Verify(pending.Candidate, pending.Handle, out _), Is.False);
            list.Reverse(); Assert.That(CheckPending(pending.Candidate, pending.Handle, out _), Is.True);
            pending.AssertSourceUnchanged();
        }

        [Test]
        public void MissingOrWrongReceiptDiscriminatorCannotAuthorizeEvidence()
        {
            var engine = new EpisodeEngine(source); var entered = Enter(engine);
            using var pending = new PendingHarness(engine, Choice(entered, DialogueCampaignV2.Cite));
            var evidence = Active(pending.Candidate).evidence.Single();
            var map = (IDictionary)PendingType.GetField("future", Instance).GetValue(pending.Handle);
            Assert.That(map.Contains(evidence), Is.True); object original = map[evidence];
            Assert.That(original.ToString(), Is.EqualTo("EvidenceCaptured"));
            map.Remove(evidence);
            Assert.That(Verify(pending.Candidate, pending.Handle, out _), Is.True);
            Assert.That(CheckPending(pending.Candidate, pending.Handle, out _), Is.False);
            map[evidence] = Enum.Parse(original.GetType(), "Entry");
            Assert.That(CheckPending(pending.Candidate, pending.Handle, out _), Is.False);
            map[evidence] = original; Assert.That(CheckPending(pending.Candidate, pending.Handle, out _), Is.True);
            pending.AssertSourceUnchanged();
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void NeutralExitAndAutomaticLapsePreserveOnlyPreviouslyAcceptedEvidence(bool cite, bool lapse)
        {
            var engine = new EpisodeEngine(source); var state = Enter(engine);
            if (cite) state = Accepted(engine, Choice(state, DialogueCampaignV2.Cite));
            var episode = Active(state); string evidence = Image(episode.evidence);
            int ticks = state.windowActions[Windows.AfterVeto], spent = state.outOfPhaseSocialActions;
            var closed = Accepted(engine, lapse ? Command(state, EpisodeCommandKind.Advance) : Choice(state, DialogueCampaignV1.Leave));
            var retained = closed.dialogue.episodes.Single();
            Assert.That(Image(retained.evidence), Is.EqualTo(evidence)); Assert.That(retained.time.appliedDelta, Is.Zero);
            Assert.That(retained.time.proposedDelta, Is.Zero); Assert.That(retained.time.spentRevision, Is.Zero);
            Assert.That(closed.houseEvents.Count(e => e.cycleId == retained.id), Is.EqualTo(cite ? 3 : 2));
            Assert.That(closed.windowActions[Windows.AfterVeto], Is.EqualTo(ticks));
            Assert.That(closed.outOfPhaseSocialActions, Is.EqualTo(spent));
            Assert.That(Active(closed), Is.Null); Valid(closed);
        }

        [TestCase("strategic", false)]
        [TestCase("strategic", true)]
        [TestCase("emotional", false)]
        [TestCase("emotional", true)]
        public void IndependentSameVoterLobbyClosesBeforeConsequencesAndKeepsPaidPolicy(string approach, bool cite)
        {
            var engine = new EpisodeEngine(source); var state = Enter(engine);
            if (cite) state = Accepted(engine, Choice(state, DialogueCampaignV2.Cite));
            var episode = Active(state); string evidence = Image(episode.evidence);
            var command = Command(state, EpisodeCommandKind.Lobby); command.targetId = voter;
            command.secondTargetId = state.playerId; command.text = LobbyAsk.Encode(LobbyAsk.Vote, approach);
            var result = Accepted(engine, command); var retained = result.dialogue.episodes.Single();
            Assert.That(Cycle(result, retained).endingId, Is.EqualTo("lapsed"));
            Assert.That(Image(retained.evidence), Is.EqualTo(evidence));
            Assert.That(result.outOfPhaseSocialActions, Is.EqualTo(state.outOfPhaseSocialActions + 1));
            Assert.That(result.windowActions[Windows.AfterVeto], Is.EqualTo(state.windowActions[Windows.AfterVeto] + 1));
            Assert.That(retained.time.sequence, Is.LessThan(result.events.Last(e => e.kind == "lobby").sequence));
            Assert.That(result.lobbies.Last().approach, Is.EqualTo(approach));
        }

        [Test]
        public void DuplicateStaleAndCrossVersionReplayCannotCaptureAnotherProofOrOccasion()
        {
            var engine = new EpisodeEngine(source); var state = Enter(engine); var command = Choice(state, DialogueCampaignV2.Cite);
            var cited = Accepted(engine, command); string before = Image(cited);
            var duplicate = engine.Apply(command); Assert.That(duplicate.duplicate, Is.True);
            Assert.That(Image(engine.Snapshot), Is.EqualTo(before));
            command.id += "-stale"; RefusedUnchanged(engine, command);
            var closed = Accepted(engine, Choice(engine.Snapshot, DialogueCampaignV1.Leave));
            Assert.That(DialogueRules.TryBuildEntryCommand(closed, voter, out _), Is.False);
            foreach (string template in new[] { V2, DialogueCampaignV1.TemplateId })
            {
                var replay = Command(closed, EpisodeCommandKind.StartDialogue);
                replay.id += template; replay.targetId = voter; replay.secondTargetId = "open-case"; replay.text = template;
                RefusedUnchanged(engine, replay);
            }
            Assert.That(engine.Snapshot.dialogue.episodes.Single().evidence.Count, Is.EqualTo(1));
        }

        [Test]
        public void PersonalPathCannotAcquireEvidenceAndRetainsEmotionalZeroTimeMeaning()
        {
            var engine = new EpisodeEngine(source); var state = Enter(engine);
            state = Accepted(engine, Choice(state, DialogueCampaignV1.Appeal));
            Assert.That(Active(state).evidence, Is.Empty);
            var wrong = Choice(state, DialogueCampaignV2.Cite); RefusedUnchanged(engine, wrong);
            state = Accepted(engine, Choice(state, DialogueCampaignV1.Plea));
            var episode = state.dialogue.episodes.Single(); Assert.That(episode.evidence, Is.Empty);
            Assert.That(state.lobbies.Last().approach, Is.EqualTo(LobbyApproach.Emotional));
            Assert.That(episode.time.appliedDelta, Is.Zero); Assert.That(episode.time.proposedDelta, Is.Zero);
            Assert.That(episode.responses.Last().responseKey, Does.StartWith(V2 + "/vote-"));
        }

        [Test]
        public void ActualRetainedV1PreentryStillStartsV1AndRejectsForcedV2()
        {
            string path = Path.Combine(Application.dataPath, "Gamesim/Tests/EditMode/Fixtures/DialogueV1/case.backup.json");
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(bytes.Length, Is.EqualTo(68513));
            Assert.That(Sha(bytes), Is.EqualTo("3C11591A815D1F789E4DE3FCB4CB32C83BA71FD14B14DDE94A2B6540EDE7DBFE"));
            using var slot = new OwnedSlot(); File.WriteAllBytes(slot.Store.SavePath, bytes);
            Assert.That(slot.Store.TryLoad(out var old, out string error), Is.True, error);
            Assert.That(old.dialogue.episodes, Is.Empty); Valid(old);
            string target = EpisodeEngine.Voters(old).First(p => !p.isPlayer && DialogueRules.CanStart(old, p.id, out _)).id;
            Assert.That(DialogueRules.TryBuildEntryCommand(old, target, out var entry), Is.True);
            Assert.That(entry.text, Is.EqualTo(DialogueCampaignV1.TemplateId));
            var forced = Command(old, EpisodeCommandKind.StartDialogue);
            forced.targetId = target; forced.secondTargetId = "open-case"; forced.text = V2;
            var engine = new EpisodeEngine(old); RefusedUnchanged(engine, forced);
            entry.id = "old-v1-still-v1"; var entered = Accepted(engine, entry);
            Assert.That(Active(entered).contentVersion, Is.EqualTo(1)); Assert.That(Active(entered).evidence, Is.Empty);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes), "Existing retained wire fixture remains unchanged.");
        }

        [Test]
        public void DeclaredDialogueOnlyShapeAllowsTerminalV1BesideActiveV2ButNotV1Evidence()
        {
            // A labelled dialogue-shape construction, NOT another historical writer/source witness.
            // Only the completed personal graph's content/owner week is transcribed to canonicalV1;
            // no power, archived ballot, contestant, AP, RNG, relationship or canonical row is invented.
            var engine = new EpisodeEngine(source); var state = Enter(engine);
            state = Accepted(engine, Choice(state, DialogueCampaignV1.Appeal));
            state = Accepted(engine, Choice(state, DialogueCampaignV1.KeepOpen));
            var episode = state.dialogue.episodes.Single(); var cycle = Cycle(state, episode);
            var template = StoryCatalog.Find(DialogueCampaignV1.TemplateId);
            cycle.templateId = template.id; cycle.week = 4; cycle.endedWeek = 4;
            episode.contentVersion = 1; episode.week = 4; episode.occasionId = DialogueRules.Occasion(4, voter);
            foreach (var step in cycle.path)
            {
                step.week = 4;
                var item = state.houseEvents.Single(e => e.cycleId == episode.id && e.contentId.EndsWith(":" + step.beatId, StringComparison.Ordinal));
                var beat = template.Beat(step.beatId);
                item.contentId = template.id + ":" + beat.id; item.week = 4; item.title = beat.title; item.narrative = beat.summary;
                item.choices = beat.options.Select(option => item.choices.Single(c => c.optionId == option.id)).ToList();
                item.chosenIndex = item.choices.FindIndex(c => c.optionId == step.optionId);
                var log = state.events.Single(e => e.sequence == step.logSequence);
                log.week = 4; log.text = item.title + ": " + item.outcome;
            }
            foreach (var response in episode.responses) response.responseKey = template.id + response.responseKey.Substring(V2.Length);
            Valid(state);
            var mixedEngine = new EpisodeEngine(state); var mixed = Enter(mixedEngine);
            Assert.That(mixed.dialogue.episodes.Count, Is.EqualTo(2));
            Assert.That(mixed.dialogue.episodes[0].contentVersion, Is.EqualTo(1));
            Assert.That(Active(mixed).contentVersion, Is.EqualTo(2));
            mixed.dialogue.episodes[0].evidence.Add(Cited().dialogue.episodes.Single().evidence.Single().Clone());
            Assert.That(EpisodeValidation.TryValidate(mixed, out _), Is.False);
            mixed.dialogue.episodes[0].evidence.Clear(); Valid(mixed);
        }

        [Test]
        public void EvidencePermissionDoesNotHideAnActualCanonicalVoteHistoryCorruption()
        {
            var engine = new EpisodeEngine(source); var entered = Enter(engine);
            using var pending = new PendingHarness(engine, Choice(entered, DialogueCampaignV2.Cite));
            var row = pending.Candidate.unifiedCommitments.Single(r => r.id == "promise-npc-54");
            Assert.That(row.kind, Is.EqualTo("vote")); Assert.That(row.status, Is.EqualTo("expired"));
            string maker = row.makerId; row.makerId = null;
            Assert.That(Verify(pending.Candidate, pending.Handle, out _), Is.True);
            Assert.That(CheckPending(pending.Candidate, pending.Handle, out _), Is.False);
            row.makerId = maker; Assert.That(CheckPending(pending.Candidate, pending.Handle, out _), Is.True);
            pending.AssertSourceUnchanged();
        }

        [Test]
        public void RetainedCompletedProofOutlivesActualWeekTurnAndHasNoLedgerPruningRisk()
        {
            var engine = new EpisodeEngine(Cited()); var before = engine.Snapshot;
            before = Accepted(engine, Choice(before, DialogueCampaignV1.Leave));
            string proof = Image(before.dialogue.episodes.Single().evidence.Single());
            for (int step = 0; step < 32 && engine.Snapshot.week == 5 && engine.Snapshot.phase != EpisodePhase.Finished; step++)
                Accepted(engine, ModeTwoReaderSweep.Next(engine.Snapshot));
            var after = engine.Snapshot;
            Assert.That(after.week, Is.GreaterThan(5), "One fixed legal route must actually cross the week; no edited week/status is accepted.");
            Assert.That(Image(after.dialogue.episodes.Single().evidence.Single()), Is.EqualTo(proof)); Valid(after);
            Assert.That(after.ledger.power.Select(p => p.week).Distinct().Count(), Is.EqualTo(after.ledger.power.Count));
            Assert.That(after.ledger.power.All(p => p.week >= 1 && p.week <= 100), Is.True);
            Assert.That(100, Is.LessThan(SeasonLedger.MostRows), "At most one actual power owner per supported week cannot reach trimming512.");
        }

        [Test]
        public void CompletedEvidenceRemainsValidAfterItsNpcActuallyDepartsOnOneBoundedLegalRoute()
        {
            // This case has its own carrier. On the shared one (week 5, final four) the cited NPC, Alex, is the only
            // voter: on every dialogue ending he evicts the player (by a margin of about 10, mostly the player's
            // threat score of -19), then reaches the final two and finishes RunnerUp, so no legal choice after the
            // citation can make him depart. departure-carrier.json comes from the shared carrier's own producer
            // (seed 13, 8 players, Avery, the same walk, overlays and public-field recorder), which on this source's
            // Simulation build reproduces the shared carrier byte for byte, with ONE player decision changed: at the
            // week-3 veto meeting the player, as HoH, names Alex as the replacement nominee (the plain walk's own
            // first choice; the producer's rescuer-protection overlay had named Riley). Alex survives that vote, and
            // the producer captures its witness at the week-4 Campaign: the player nominated, Alex one of two
            // voters, his week-2 rescue the record to cite. It was chosen offline from the single player-decision
            // variants of that walk; the route below is unchanged and fixed, and on this carrier it ends with
            // Quinn's final eviction of Alex. Nothing here searches or falls back at run time. What this carrier does
            // not reach: the player is evicted in week 4, a few commands after the citation, so the player-HoH final
            // choice below never runs on it (Alex leaves by an NPC HoH's Advance). Of the 62 variants only 4 give a
            // carrier on which Alex departs, every one with him on the week-3 block. The producer is not in this
            // repository, for either carrier: it is Codex's DiscoverSingle13Final4PlayerKeep01, kept with Codex's R2
            // work (r2-single13-final4-player-keep-next01). To regenerate this carrier, run it with its module pins
            // dropped and the one decision above changed; the carrier's playerDecisionChanged field records it.
            var departure = DepartureCarrier(out string voter); // This carrier's cited NPC; it shadows the shared carrier's on purpose.
            string departureImage = Image(departure);
            var cited = CitedOn(departure, voter); var citation = cited.dialogue.episodes.Single().evidence.Single();
            int citationRevision = citation.capturedRevision, sequenceAfterCitation = cited.nextSequence;
            var engine = new EpisodeEngine(cited); var state = Accepted(engine, Choice(engine.Snapshot, DialogueCampaignV1.Leave));
            string proof = Image(state.dialogue.episodes.Single().evidence.Single()); bool observedDeparture = false;
            for (int step = 0; step < 96 && engine.Snapshot.phase != EpisodePhase.Finished
                && engine.Snapshot.Find(voter).status == ContestantStatus.Active; step++)
            {
                state = engine.Snapshot; var command = ModeTwoReaderSweep.Next(state);
                // Only a genuine player-owned final choice may name this NPC; no NPC result is edited.
                if (state.phase == EpisodePhase.FinalEviction && state.hohId == state.playerId
                    && state.Find(voter).status == ContestantStatus.Active)
                    command.targetId = voter;
                var after = Accepted(engine, command);
                if (after.Find(voter).status != ContestantStatus.Active)
                {
                    Assert.That(after.Find(voter).status == ContestantStatus.Jury
                        || after.Find(voter).status == ContestantStatus.Evicted, Is.True,
                        "Winner/RunnerUp and unsupported production removal are NOT an actual eviction.");
                    Assert.That(after.revision, Is.GreaterThan(citationRevision));
                    Assert.That(state.revision, Is.GreaterThanOrEqualTo(citationRevision));
                    Assert.That(after.week, Is.EqualTo(state.week));
                    var row = after.ledger.power.Single(p => p.week == state.week);
                    var prior = state.ledger.power.SingleOrDefault(p => p.week == state.week);
                    Assert.That(prior == null || prior.evicteeId != voter, Is.True,
                        "The accepted command must actually add/change this NPC's departure owner.");
                    Assert.That(prior == null || Image(prior) != Image(row), Is.True);
                    Assert.That(row.evicteeId, Is.EqualTo(voter)); Assert.That(row.hohId, Is.EqualTo(state.hohId));
                    string eventKind;
                    if (state.phase == EpisodePhase.Eviction)
                    {
                        Assert.That(command.kind, Is.EqualTo(EpisodeCommandKind.Advance));
                        Assert.That(state.evictionResolved, Is.False); Assert.That(after.evictionResolved, Is.True);
                        Assert.That(after.evictionStage, Is.EqualTo(EvictionStage.Results));
                        Assert.That(state.nominees, Does.Contain(voter));
                        Assert.That(row.nominees, Is.EqualTo(state.nominees)); Assert.That(row.tally.Count, Is.EqualTo(2));
                        eventKind = "eviction"; // RecordReveal: exact real regular reveal, not a copied historic victim.
                    }
                    else
                    {
                        Assert.That(state.phase, Is.EqualTo(EpisodePhase.FinalEviction));
                        Assert.That(state.Active.Count(), Is.EqualTo(3)); Assert.That(state.hohId, Is.Not.EqualTo(voter));
                        Assert.That(command.kind, Is.EqualTo(state.hohId == state.playerId
                            ? EpisodeCommandKind.FinalEvict : EpisodeCommandKind.Advance));
                        Assert.That(row.nominees, Is.EqualTo(state.Active.Where(p => p.id != state.hohId).Take(2).Select(p => p.id)));
                        eventKind = "final-eviction"; // Actual RecordFinalEviction owner written in this command.
                    }
                    var receipt = after.events.Where(item => item.sequence >= state.nextSequence && item.kind == eventKind).Single();
                    Assert.That(receipt.sequence, Is.GreaterThanOrEqualTo(sequenceAfterCitation));
                    Assert.That(receipt.week, Is.EqualTo(state.week)); Assert.That(receipt.phase, Is.EqualTo(state.phase));
                    Assert.That(receipt.audienceIds, Is.Empty, "These two actual eviction producers publish a public event, not actor-ID proof.");
                    Assert.That(Image(after.dialogue.episodes.Single().evidence.Single()), Is.EqualTo(proof)); Valid(after);
                    observedDeparture = true;
                }
            }
            state = engine.Snapshot;
            Assert.That(observedDeparture, Is.True,
                "This ONE fixed public route must produce a new source-owned eviction after citation; no finalist, skip or fallback seed qualifies.");
            Assert.That(state.Find(voter).status == ContestantStatus.Jury
                || state.Find(voter).status == ContestantStatus.Evicted, Is.True);
            Assert.That(Image(state.dialogue.episodes.Single().evidence.Single()), Is.EqualTo(proof)); Valid(state);
            Assert.That(Image(departure), Is.EqualTo(departureImage), "The retained departure carrier is never mutated.");
        }

        // The departure case's own retained carrier, admitted as the shared one is: its declared bytes, an exact typed
        // round trip and the sole pilot activation. Returns its cited NPC, the week-2 rescuer.
        private static EpisodeState DepartureCarrier(out string cited)
        {
            string path = Path.Combine(Application.dataPath, "Gamesim/Tests/EditMode/Fixtures/DialoguePublicRescueV2/departure-carrier.json");
            Assert.That(File.Exists(path), Is.True, "The departure carrier must be explicitly admitted; no generation fallback.");
            var carrier = JObject.Parse(File.ReadAllText(path, new UTF8Encoding(false, true)));
            Assert.That((string)carrier["kind"], Is.EqualTo("immutable-diagnostic-public-field-dto-carrier-not-savewire"));
            byte[] bytes = Convert.FromBase64String((string)carrier["base64"]);
            Assert.That(bytes.Length, Is.EqualTo((int)carrier["bytes"]));
            Assert.That(Sha(bytes), Is.EqualTo((string)carrier["sha256"]));
            Assert.That(Convert.ToBase64String(bytes), Is.EqualTo((string)carrier["base64"]));
            string original = new UTF8Encoding(false, true).GetString(bytes);
            var actual = JsonConvert.DeserializeObject<EpisodeState>(original, Fields);
            Assert.That(Image(actual), Is.EqualTo(original), "Exact typed original round trip BEFORE any declared rule change.");
            Assert.That(actual.week, Is.EqualTo(4)); Assert.That(actual.revision, Is.EqualTo(62));
            Assert.That(actual.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(actual.dialogueRulesVersion, Is.Zero, "The retained original is not silently reinterpreted as shipping-v2.");
            Valid(actual);
            var departure = actual.Clone();
            Assert.That(Image(departure), Is.EqualTo(original), "The clone itself may not alter any original field or number.");
            departure.dialogueRulesVersion = 1; // Declared pilot test activation ONLY, as for the shared carrier.
            var allowed = Parse(original); allowed["dialogueRulesVersion"] = 1;
            Assert.That(ExactCapturedJson.Equal(Parse(Image(departure)), allowed), Is.True,
                "Complete ordered/type/value equality after the SOLE declared pilot activation.");
            Valid(departure);
            var rescue = departure.ledger.power.Single(p => p.week == 2);
            Assert.That(rescue.vetoUsed, Is.True); Assert.That(rescue.savedId, Is.EqualTo(departure.playerId));
            cited = rescue.vetoHolderId;
            Assert.That(departure.Find(cited).name, Does.StartWith("Alex"));
            Assert.That(departure.nominees, Does.Contain(departure.playerId));
            Assert.That(EpisodeEngine.Voters(departure).Select(p => p.id), Does.Contain(cited));
            return departure;
        }

        // The departure carrier's citation, made as Cited() makes the shared carrier's: Enter, then Cite.
        private static EpisodeState CitedOn(EpisodeState carrier, string npc)
        {
            var engine = new EpisodeEngine(carrier); var current = engine.Snapshot;
            Assert.That(DialogueRules.TryBuildEntryCommand(current, npc, out var command), Is.True);
            Assert.That(command.text, Is.EqualTo(V2)); command.id = "rescue-v2-entry-" + current.revision;
            return Accepted(engine, Choice(Accepted(engine, command), DialogueCampaignV2.Cite));
        }

        [TestCase("identical-complete-values")]
        [TestCase("adjacent-double")]
        [TestCase("nearby-double")]
        [TestCase("signed-zero")]
        [TestCase("huge-integer")]
        [TestCase("missing-member")]
        [TestCase("extra-member")]
        [TestCase("member-order")]
        [TestCase("array-order")]
        [TestCase("token-type")]
        [TestCase("string-value")]
        [TestCase("boolean-value")]
        [TestCase("nested-value")]
        [TestCase("null-type")]
        [TestCase("nonfinite-nan")]
        [TestCase("nonfinite-infinity")]
        [TestCase("unsupported-date")]
        public void StrictCapturedJsonComparisonPreservesCompleteOrderedValuesAndExactNumbers(string change)
        {
            JToken expected = Parse("{\"a\":1,\"b\":[true,null,\"exact\",{\"nested\":2}]}");
            JToken actual = expected.DeepClone(); bool equal = false;
            switch (change)
            {
                case "identical-complete-values":
                    expected = Parse("{\"a\":-9223372036854775808,\"b\":18446744073709551615,"
                        + "\"huge\":18446744073709551616000000000000000001,"
                        + "\"negativeHuge\":-18446744073709551616000000000000000001,"
                        + "\"values\":[true,false,null,\"exact\",{\"nested\":2},1.7976931348623157E+308,5E-324]}");
                    actual = expected.DeepClone(); equal = true; break;
                case "adjacent-double":
                    expected = Floating(1d); actual = Floating(BitConverter.Int64BitsToDouble(0x3ff0000000000001L)); break;
                case "nearby-double": expected = Floating(1d); actual = Floating(1.000000000000001d); break;
                case "signed-zero": expected = Floating(0d); actual = Floating(BitConverter.Int64BitsToDouble(long.MinValue)); break;
                case "huge-integer":
                    expected = Parse("{\"value\":18446744073709551616000000000000000001}");
                    actual = Parse("{\"value\":18446744073709551616000000000000000002}"); break;
                case "missing-member": ((JObject)actual).Remove("b"); break;
                case "extra-member": ((JObject)actual)["extra"] = 0; break;
                case "member-order": actual = Parse("{\"b\":[true,null,\"exact\",{\"nested\":2}],\"a\":1}"); break;
                case "array-order": actual = Parse("{\"a\":1,\"b\":[null,true,\"exact\",{\"nested\":2}]}"); break;
                case "token-type": expected = Parse("{\"value\":1}"); actual = Parse("{\"value\":1.0}"); break;
                case "string-value": actual["b"][2] = "Exact"; break;
                case "boolean-value": actual["b"][0] = false; break;
                case "nested-value": actual["b"][3]["nested"] = 3; break;
                case "null-type": actual["b"][1] = "null"; break;
                case "nonfinite-nan": expected = Floating(double.NaN); actual = Floating(double.NaN); break;
                case "nonfinite-infinity": expected = Floating(double.PositiveInfinity); actual = Floating(double.PositiveInfinity); break;
                case "unsupported-date":
                    expected = new JObject(new JProperty("value", new JValue(new DateTime(2026, 10, 20))));
                    actual = expected.DeepClone(); break;
                default: Assert.Fail("Unknown fixed comparer control."); break;
            }
            Assert.That(ExactCapturedJson.Equal(actual, expected), Is.EqualTo(equal), change);
            Assert.That(ExactCapturedJson.Equal(expected, actual), Is.EqualTo(equal), change + " / symmetric");
        }

        // Boxed on purpose: under Json.NET 13.0.2, JValue(double) routes through a cached boxed zero and
        // turns -0.0 into +0.0, so the signed-zero control would compare two positive zeros.
        // JValue(object) keeps the caller's bits. Never "simplify" this back to new JValue(value).
        private static JObject Floating(double value) => new JObject(new JProperty("value", new JValue((object)value)));

        private EpisodeState Cited()
        { var engine = new EpisodeEngine(source); return Accepted(engine, Choice(Enter(engine), DialogueCampaignV2.Cite)); }

        private EpisodeState Enter(EpisodeEngine engine)
        {
            var current = engine.Snapshot;
            Assert.That(DialogueRules.TryBuildEntryCommand(current, voter, out var command), Is.True);
            Assert.That(command.text, Is.EqualTo(V2)); command.id = "rescue-v2-entry-" + current.revision;
            return Accepted(engine, command);
        }

        private static DialogueEpisodeState Active(EpisodeState state) => DialogueRules.Active(state);
        private static StorylineState Cycle(EpisodeState state, DialogueEpisodeState episode) => state.storylines.Single(c => c.id == episode.id);
        private static int Ap(EpisodeState state) => EpisodeEngine.SocialActionBudget(state) - EpisodeEngine.SocialActionsSpent(state);
        private static EpisodeCommand Command(EpisodeState state, EpisodeCommandKind kind) => new EpisodeCommand
        { id = "rescue-v2-" + state.revision + "-" + kind, actorId = state.playerId, kind = kind, expectedRevision = state.revision, expectedPhase = state.phase };
        private EpisodeCommand VotePlea(EpisodeState state, string approach)
        {
            var command = Command(state, EpisodeCommandKind.Lobby); command.id += "-" + approach;
            command.targetId = voter; command.secondTargetId = state.playerId; command.text = LobbyAsk.Encode(LobbyAsk.Vote, approach); return command;
        }
        private static EpisodeCommand Choice(EpisodeState state, string option)
        {
            var episode = Active(state); var cycle = Cycle(state, episode); var command = Command(state, EpisodeCommandKind.ChooseDialogue);
            command.targetId = episode.id; command.secondTargetId = option; command.text = cycle.beatId; return command;
        }
        private static EpisodeState Accepted(EpisodeEngine engine, EpisodeCommand command)
        {
            string request = Image(command); var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + ": " + result.reason); Assert.That(result.duplicate, Is.False);
            Assert.That(Image(command), Is.EqualTo(request)); Valid(result.state); Assert.That(Image(engine.Snapshot), Is.EqualTo(Image(result.state)));
            Assert.That(result.state.revision, Is.EqualTo(command.expectedRevision + 1));
            return result.state;
        }
        private static void RefusedUnchanged(EpisodeEngine engine, EpisodeCommand command)
        {
            string before = Image(engine.Snapshot), request = Image(command); var result = engine.Apply(command);
            Assert.That(result.accepted, Is.False); Assert.That(result.duplicate, Is.False); Assert.That(result.reason, Is.Not.Null.And.Not.Empty);
            Assert.That(Image(engine.Snapshot), Is.EqualTo(before)); Assert.That(Image(result.state), Is.EqualTo(before));
            Assert.That(Image(command), Is.EqualTo(request));
        }
        private static void Valid(EpisodeState state) => Assert.That(EpisodeValidation.TryValidate(state, out string error), Is.True, error);
        private static JObject Parse(string json)
        {
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double, MaxDepth = 64 };
            var result = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            Assert.That(reader.Read(), Is.False, "Exactly one captured JSON object."); return result;
        }
        private static string Image(object value) => JsonConvert.SerializeObject(value, Formatting.None, Fields);
        private static string Sha(byte[] bytes) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""); }
        // Exact comparison of captured JSON values, never a new gameplay oracle.
        // Read DTOs have captured-value provenance, NOT separately retained original fragment bytes.
        private static class ExactCapturedJson
        {
            internal static bool Equal(JToken left, JToken right) => Equal(left, right, 0);
            private static bool Equal(JToken left, JToken right, int depth)
            {
                if (left == null || right == null || depth > 64 || left.Type != right.Type) return false;
                if (left.Type == JTokenType.Object)
                {
                    if (!(left is JObject a) || !(right is JObject b)) return false;
                    var first = a.Properties().ToArray(); var second = b.Properties().ToArray();
                    if (first.Length != second.Length) return false;
                    var names = new HashSet<string>(StringComparer.Ordinal);
                    for (int at = 0; at < first.Length; at++)
                        if (!names.Add(first[at].Name)
                            || !string.Equals(first[at].Name, second[at].Name, StringComparison.Ordinal)
                            || !Equal(first[at].Value, second[at].Value, depth + 1)) return false;
                    return true;
                }
                if (left.Type == JTokenType.Array)
                {
                    if (!(left is JArray a) || !(right is JArray b) || a.Count != b.Count) return false;
                    for (int at = 0; at < a.Count; at++) if (!Equal(a[at], b[at], depth + 1)) return false;
                    return true;
                }
                if (!(left is JValue x) || !(right is JValue y)) return false;
                switch (left.Type)
                {
                    case JTokenType.Integer:
                        return Integer(x.Value, out string firstInteger) && Integer(y.Value, out string secondInteger)
                            && string.Equals(firstInteger, secondInteger, StringComparison.Ordinal);
                    case JTokenType.Float:
                        return x.Value is double firstDouble && y.Value is double secondDouble
                            && !double.IsNaN(firstDouble) && !double.IsInfinity(firstDouble)
                            && !double.IsNaN(secondDouble) && !double.IsInfinity(secondDouble)
                            && BitConverter.DoubleToInt64Bits(firstDouble) == BitConverter.DoubleToInt64Bits(secondDouble);
                    case JTokenType.String:
                        return x.Value is string firstString && y.Value is string secondString
                            && string.Equals(firstString, secondString, StringComparison.Ordinal);
                    case JTokenType.Boolean:
                        return x.Value is bool firstBoolean && y.Value is bool secondBoolean && firstBoolean == secondBoolean;
                    case JTokenType.Null:
                        return x.Value == null && y.Value == null;
                    default:
                        return false; // No Date/Bytes/Raw/Undefined/constructor or numeric coercion.
                }
            }
            private static bool Integer(object value, out string exact)
            {
                exact = null;
                if (!(value is sbyte || value is byte || value is short || value is ushort
                    || value is int || value is uint || value is long || value is ulong || value is BigInteger)) return false;
                exact = ((IFormattable)value).ToString("D", CultureInfo.InvariantCulture);
                return true; // Canonical arbitrary-range decimal text; never narrowed through double/Int64.
            }
        }
        private sealed class PublicFieldsOnly : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization serialization) =>
                type.GetFields(BindingFlags.Instance | BindingFlags.Public).Select(f => base.CreateProperty(f, MemberSerialization.Fields)).ToList();
        }
        private static object Invoke(MethodInfo method, object owner, object[] args)
        {
            Assert.That(method, Is.Not.Null);
            try { return method.Invoke(owner, args); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
        private static bool Verify(EpisodeState state, object handle, out string error)
        { var args = new object[] { state, null }; bool result = (bool)Invoke(PendingType.GetMethod("Verify", Instance), handle, args); error = (string)args[1]; return result; }
        private static bool CheckPending(EpisodeState state, object handle, out string error)
        { var args = new object[] { state, handle, null }; bool result = (bool)Invoke(typeof(EpisodeValidation).GetMethod("TryValidatePendingVote", Static), null, args); error = (string)args[2]; return result; }

        private sealed class PendingHarness : IDisposable
        {
            internal readonly EpisodeEngine Engine;
            internal readonly EpisodeState Source, Candidate;
            internal readonly object Handle;
            private readonly string sourceImage, requestImage;
            private readonly EpisodeCommand request;
            internal PendingHarness(EpisodeEngine engine, EpisodeCommand command)
            {
                Engine = engine; request = command; Source = engine.Snapshot; Candidate = Source.Clone();
                sourceImage = Image(Source); requestImage = Image(request);
                Assert.That(Applying.GetValue(engine), Is.Null); Assert.That(Registered.GetValue(engine), Is.Null);
                Applying.SetValue(engine, Candidate);
                try
                {
                    Handle = Invoke(typeof(EpisodeEngine).GetMethod("PreparePaidCampaignCommand", Instance), engine, new object[] { Candidate, command });
                    Assert.That(Handle, Is.Not.Null); Assert.That(Registered.GetValue(engine), Is.SameAs(Handle));
                    Invoke(typeof(EpisodeEngine).GetMethod("Execute", Static), null, new object[] { Candidate, command, Handle });
                    Assert.That(CheckPending(Candidate, Handle, out string error), Is.True, error); AssertSourceUnchanged();
                }
                catch { Dispose(); throw; }
            }
            internal void AssertSourceUnchanged()
            { Assert.That(Image(Source), Is.EqualTo(sourceImage)); Assert.That(Image(Engine.Snapshot), Is.EqualTo(sourceImage)); Assert.That(Image(request), Is.EqualTo(requestImage)); }
            internal void Retire() { if (Handle != null) Invoke(PendingType.GetMethod("Retire", Instance), Handle, Array.Empty<object>()); }
            public void Dispose() { try { Retire(); } finally { Applying.SetValue(Engine, null); } }
        }

        private sealed class OwnedSlot : IDisposable
        {
            private readonly string leaf = "GamesimRescueV2-" + Guid.NewGuid().ToString("N"), root;
            internal readonly EpisodeSaveStore Store;
            internal OwnedSlot()
            {
                root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), leaf)); Guard();
                Assert.That(Directory.Exists(root) || File.Exists(root), Is.False);
                Directory.CreateDirectory(root); Store = new EpisodeSaveStore(Path.Combine(root, "episode.json"));
            }
            internal Dictionary<string, byte[]> Image() { Guard(); return Directory.GetFiles(root).ToDictionary(Path.GetFileName, File.ReadAllBytes, StringComparer.Ordinal); }
            internal void AssertImage(Dictionary<string, byte[]> expected)
            { var actual = Image(); Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys)); foreach (var pair in expected) Assert.That(actual[pair.Key], Is.EqualTo(pair.Value), pair.Key); }
            private void Guard()
            {
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Assert.That(Path.GetDirectoryName(root), Is.EqualTo(temp)); Assert.That(Path.GetFileName(root), Is.EqualTo(leaf));
                for (string at = root; !string.IsNullOrEmpty(at); at = Path.GetDirectoryName(at))
                    if (Directory.Exists(at) || File.Exists(at)) Assert.That(File.GetAttributes(at) & FileAttributes.ReparsePoint, Is.EqualTo((FileAttributes)0));
                if (!Directory.Exists(root)) return;
                Assert.That(Directory.GetDirectories(root), Is.Empty);
                foreach (string file in Directory.GetFiles(root)) Assert.That(File.GetAttributes(file) & FileAttributes.ReparsePoint, Is.EqualTo((FileAttributes)0));
            }
            public void Dispose()
            { Guard(); if (!Directory.Exists(root)) return; foreach (string file in Directory.GetFiles(root)) File.Delete(file); Directory.Delete(root); }
        }
    }
}
#endif
