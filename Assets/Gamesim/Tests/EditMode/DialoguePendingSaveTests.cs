#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Native persistence/capability qualification. The fixture is played through public Apply;
    /// the inspected candidate is then created by the actual private factory and Execute after
    /// binding the same detached-candidate field Apply binds. This is NOT an observation made
    /// inside public Apply's stack, and does not claim its NPC catch-up/publication path ran.
    /// No handle is constructed, reconstructed, copied, serialized or added to the saved DTO.
    /// </summary>
    public sealed class DialoguePendingSaveTests
    {
        private const string Template = "dialogue-campaign-v1";
        private EpisodeState campaign;
        private string campaignImage, voter;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly Type PendingType = typeof(EpisodeEngine).GetNestedType("PendingDialogueCommand", BindingFlags.NonPublic);
        private static readonly FieldInfo Applying = typeof(EpisodeEngine).GetField("applyingDialogueCandidate", Instance);
        private static readonly FieldInfo Registered = typeof(EpisodeEngine).GetField("pendingDialogue", Instance);

        [OneTimeSetUp]
        public void PlayToAnActualNominatedCampaign()
        {
            Assert.That(PendingType, Is.Not.Null);
            Assert.That(Applying, Is.Not.Null);
            Assert.That(Registered, Is.Not.Null);
            for (uint seed = 1; seed <= 24; seed++)
            {
                var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(fresh);
                Assert.That(fresh.dialogueRulesVersion, Is.Zero, "The unpublished pilot is not a shipping default.");
                fresh.dialogueRulesVersion = 1;
                Valid(fresh);
                var engine = new EpisodeEngine(fresh);
                for (int step = 0; step < 96; step++)
                {
                    var s = engine.Snapshot;
                    if (s.week != 1 || s.phase == EpisodePhase.Finished) break;
                    if (s.phase == EpisodePhase.Campaign)
                    {
                        if (!s.nominees.Contains(s.playerId)) break;
                        CloseExistingAsks(engine);
                        s = engine.Snapshot;
                        string eligible = EpisodeEngine.Voters(s).Where(c => !c.isPlayer).Select(c => c.id)
                            .FirstOrDefault(id => DialogueRules.CanStart(s, id, out _));
                        if (eligible == null) break;
                        campaign = s.Clone(); campaignImage = Json(campaign); voter = eligible;
                        Assert.That(campaign.unifiedCommitmentRulesVersion, Is.EqualTo(2));
                        Assert.That(campaign.allWeekRulesStartWeek, Is.EqualTo(1));
                        Assert.That(campaign.story.rulesVersion, Is.EqualTo(9));
                        TestContext.Out.WriteLine("Pending-save public fixture: seed " + seed + ", revision " + s.revision + ".");
                        return;
                    }
                    Accepted(engine, ModeTwoReaderSweep.Next(s));
                }
            }
            Assert.Fail("No eligible actual nominated Campaign in 24 seeds x 96 public commands.");
        }

        [TearDown]
        public void OriginalPlayedFixtureIsUnchanged()
        {
            if (campaign != null) Assert.That(Json(campaign), Is.EqualTo(campaignImage));
        }

        [TestCase("entry")]
        [TestCase("middle")]
        [TestCase("terminal")]
        public void ExactLiveFactoryCandidatePassesPendingCoreButCannotBeSaved(string stage)
        {
            using var pending = MakePending(stage);
            using var slot = new OwnedSlot();
            slot.Store.Save(pending.Source);
            slot.Store.Save(pending.Source);
            var saved = slot.Image();
            Assert.That(saved.Keys, Is.EquivalentTo(new[] { "episode.json", "episode.json.backup" }));
            Assert.That(slot.Store.TryLoad(out var loaded, out string loadError), Is.True, loadError);
            Assert.That(Json(loaded), Is.EqualTo(Json(pending.Source)));
            slot.AssertImage(saved);

            string candidateImage = Json(pending.Candidate);
            Assert.That(pending.Candidate.revision, Is.EqualTo(pending.Source.revision));
            var episode = pending.Candidate.dialogue.episodes.Single();
            Assert.That(episode.responses.Last().revision, Is.EqualTo(pending.Source.revision + 1));
            if (stage == "entry")
            {
                Assert.That(episode.startedRevision, Is.EqualTo(pending.Source.revision + 1));
                Assert.That(episode.entry.revision, Is.EqualTo(pending.Source.revision + 1));
            }
            if (stage == "terminal") Assert.That(episode.time.revision, Is.EqualTo(pending.Source.revision + 1));
            AssertPending(pending.Candidate, pending.Handle, true);
            Assert.That(EpisodeValidation.TryValidate(pending.Candidate, out string publicError), Is.False);
            Assert.That(publicError, Is.Not.Null.And.Not.Empty);
            AssertPublicAdmissionStaysStrict(pending, publicError);
            var refusal = Assert.Throws<InvalidDataException>(() => slot.Store.Save(pending.Candidate));
            Assert.That(refusal.Message, Is.EqualTo(publicError), "Save must use strict public validation, not discover a live engine handle.");
            Assert.That(Json(pending.Candidate), Is.EqualTo(candidateImage));
            Assert.That(Registered.GetValue(pending.Engine), Is.SameAs(pending.Handle));
            AssertPending(pending.Candidate, pending.Handle, true);
            pending.AssertSourceUnchanged();
            slot.AssertImage(saved);
        }

        private void AssertPublicAdmissionStaysStrict(PendingHarness pending, string publicError)
        {
            // Admission-only drafts, not a claim that the catalog authored or consented to a
            // Story promise/deal. The actual public APIs must admit each detached draft on the
            // strict played source before the same draft tests the owned candidate boundary.
            var source = pending.Source;
            string sourceImage = Json(source), candidateImage = Json(pending.Candidate);
            var promise = new PromiseState
            {
                id = "promise-" + source.nextSequence, fromId = source.playerId, toId = voter,
                kind = PromiseKind.Vote, status = PromiseStatus.Active,
                week = source.week, expiresWeek = source.week, impact = DealTrust.Medium,
            };
            var deal = PlayerDeals.Draft(source, voter, DealKind.VoteTogether, null, "deal-story-" + source.nextSequence);
            deal.status = DealStatus.Active;
            string promiseImage = Json(promise), dealImage = Json(deal);
            Assert.That(Registered.GetValue(pending.Engine), Is.SameAs(pending.Handle));
            AssertPending(pending.Candidate, pending.Handle, true);

            Assert.That(UnifiedVoteAdmission.TryPromise(source, promise, UnifiedCommitments.StoryPromise,
                out var admittedPromise, out string promiseError), Is.True, promiseError);
            Assert.That(promiseError, Is.Null);
            Assert.That(admittedPromise, Is.Not.Null);
            Assert.That(admittedPromise.id, Is.EqualTo(promise.id));
            Assert.That(admittedPromise.origin, Is.EqualTo(UnifiedCommitments.StoryPromise));
            Assert.That(UnifiedVoteAdmission.TryDeal(source, deal, UnifiedCommitments.StoryDeal,
                out var admittedDeal, out string dealError), Is.True, dealError);
            Assert.That(dealError, Is.Null);
            Assert.That(admittedDeal, Is.Not.Null);
            Assert.That(admittedDeal.id, Is.EqualTo(deal.id));
            Assert.That(admittedDeal.origin, Is.EqualTo(UnifiedCommitments.StoryDeal));
            string admittedPromiseImage = Json(admittedPromise), admittedDealImage = Json(admittedDeal);
            Assert.That(Json(source), Is.EqualTo(sourceImage));
            Assert.That(Json(promise), Is.EqualTo(promiseImage));
            Assert.That(Json(deal), Is.EqualTo(dealImage));

            Assert.That(UnifiedVoteAdmission.TryPromise(pending.Candidate, promise, UnifiedCommitments.StoryPromise,
                out var refusedPromise, out promiseError), Is.False);
            Assert.That(refusedPromise, Is.Null);
            Assert.That(promiseError, Is.EqualTo(publicError), "Public admission must use strict Ready, not find the live handle.");
            Assert.That(UnifiedVoteAdmission.TryDeal(pending.Candidate, deal, UnifiedCommitments.StoryDeal,
                out var refusedDeal, out dealError), Is.False);
            Assert.That(refusedDeal, Is.Null);
            Assert.That(dealError, Is.EqualTo(publicError));
            Assert.That(Json(pending.Candidate), Is.EqualTo(candidateImage));
            Assert.That(Json(source), Is.EqualTo(sourceImage));
            Assert.That(Json(promise), Is.EqualTo(promiseImage));
            Assert.That(Json(deal), Is.EqualTo(dealImage));
            Assert.That(Registered.GetValue(pending.Engine), Is.SameAs(pending.Handle));
            AssertPending(pending.Candidate, pending.Handle, true);

            Assert.That(UnifiedVoteAdmission.TryPromise(source, promise, UnifiedCommitments.StoryPromise,
                out var readmittedPromise, out promiseError), Is.True, promiseError);
            Assert.That(UnifiedVoteAdmission.TryDeal(source, deal, UnifiedCommitments.StoryDeal,
                out var readmittedDeal, out dealError), Is.True, dealError);
            Assert.That(Json(readmittedPromise), Is.EqualTo(admittedPromiseImage));
            Assert.That(Json(readmittedDeal), Is.EqualTo(admittedDealImage));
            Assert.That(Json(source), Is.EqualTo(sourceImage));
            Assert.That(Json(promise), Is.EqualTo(promiseImage));
            Assert.That(Json(deal), Is.EqualTo(dealImage));
            pending.AssertSourceUnchanged();
        }

        [Test]
        public void FactoryOwnedAdvanceLapseIsPendingOnlyBeforeTheCampaignPhaseChanges()
        {
            // Deliberately stop at the actual private LapseAt seam, not Execute/Apply. This
            // proves no authored neighboring Story effect or public-stack observation.
            using var pending = MakePending("advance-lapse");
            using var slot = new OwnedSlot();
            slot.Store.Save(pending.Source); slot.Store.Save(pending.Source);
            var saved = slot.Image();
            Assert.That(saved.Keys, Is.EquivalentTo(new[] { "episode.json", "episode.json.backup" }));
            var episode = pending.Candidate.dialogue.episodes.Single();
            var cycle = pending.Candidate.storylines.Single(c => c.id == episode.id);
            Assert.That(pending.Source.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(pending.Candidate.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(cycle.endingId, Is.EqualTo("lapsed"));
            Assert.That(cycle.path.Last().result, Is.EqualTo(StoryResults.Lapsed));
            Assert.That(episode.time.revision, Is.EqualTo(pending.Source.revision + 1));
            AssertPending(pending.Candidate, pending.Handle, true);
            Assert.That(EpisodeValidation.TryValidate(pending.Candidate, out string strictError), Is.False);
            string before = Json(pending.Candidate);
            Assert.That(Assert.Throws<InvalidDataException>(() => slot.Store.Save(pending.Candidate)).Message, Is.EqualTo(strictError));
            Assert.That(Json(pending.Candidate), Is.EqualTo(before));
            AssertPending(pending.Candidate.Clone(), pending.Handle, false);
            try
            {
                Registered.SetValue(pending.Engine, null);
                AssertPending(pending.Candidate, pending.Handle, false);
            }
            finally { Registered.SetValue(pending.Engine, pending.Handle); }
            AssertPending(pending.Candidate, pending.Handle, true);
            pending.AssertSourceUnchanged(); slot.AssertImage(saved);
        }

        [Test]
        public void EquivalentDetachedCloneCannotBorrowTheLiveCandidateAuthority()
        {
            using var pending = MakePending("entry");
            var clone = pending.Candidate.Clone();
            Assert.That(Json(clone), Is.EqualTo(Json(pending.Candidate)));
            Assert.That(clone, Is.Not.SameAs(pending.Candidate));
            AssertPending(clone, pending.Handle, false);
            AssertPending(pending.Candidate, pending.Handle, true);
            pending.AssertSourceUnchanged();
        }

        [Test]
        public void AnotherEnginesRealFactoryHandleCannotAuthorizeThisCandidate()
        {
            using var first = MakePending("entry");
            using var second = MakePending("entry");
            Assert.That(first.Engine, Is.Not.SameAs(second.Engine));
            Assert.That(first.Handle, Is.Not.SameAs(second.Handle));
            Assert.That(Json(first.Candidate), Is.EqualTo(Json(second.Candidate)));
            AssertPending(first.Candidate, second.Handle, false);
            AssertPending(second.Candidate, first.Handle, false);
            AssertPending(first.Candidate, first.Handle, true);
            AssertPending(second.Candidate, second.Handle, true);
            first.AssertSourceUnchanged(); second.AssertSourceUnchanged();
        }

        [Test]
        public void RemovingTheActualOwnerRegistrationRefusesUntilThatRegistrationIsRestored()
        {
            using var pending = MakePending("entry");
            Assert.That(Registered.GetValue(pending.Engine), Is.SameAs(pending.Handle));
            try
            {
                Registered.SetValue(pending.Engine, null);
                AssertPending(pending.Candidate, pending.Handle, false);
            }
            finally { Registered.SetValue(pending.Engine, pending.Handle); }
            AssertPending(pending.Candidate, pending.Handle, true);
            pending.AssertSourceUnchanged();
        }

        [Test]
        public void RetiringTheRealHandlePermanentlyRemovesItsPendingAuthority()
        {
            using var pending = MakePending("entry");
            string before = Json(pending.Candidate);
            pending.Retire();
            Assert.That(Registered.GetValue(pending.Engine), Is.Null);
            AssertPending(pending.Candidate, pending.Handle, false);
            Assert.That(EpisodeValidation.TryValidate(pending.Candidate, out _), Is.False);
            Assert.That(Json(pending.Candidate), Is.EqualTo(before));
            pending.AssertSourceUnchanged();
        }

        [TestCase("entry")]
        [TestCase("response")]
        [TestCase("time")]
        public void EqualValuedForeignFutureReceiptIsNotTheFactoryRegisteredObject(string receipt)
        {
            using var pending = MakePending(receipt == "time" ? "terminal" : receipt == "response" ? "middle" : "entry");
            var episode = pending.Candidate.dialogue.episodes.Single();
            var entry = episode.entry; var response = episode.responses.Last(); var time = episode.time;
            string before = Json(pending.Candidate);
            try
            {
                if (receipt == "entry") episode.entry = entry.Clone();
                else if (receipt == "response") episode.responses[episode.responses.Count - 1] = response.Clone();
                else episode.time = time.Clone();
                Assert.That(Json(pending.Candidate), Is.EqualTo(before), "Only object identity changed, not the wire value.");
                Assert.That(Verify(pending.Candidate, pending.Handle, out string error), Is.True, error);
                AssertPending(pending.Candidate, pending.Handle, false);
            }
            finally
            {
                episode.entry = entry; episode.responses[episode.responses.Count - 1] = response; episode.time = time;
            }
            AssertPending(pending.Candidate, pending.Handle, true);
            pending.AssertSourceUnchanged();
        }

        [Test]
        public void EditingASealedResponseRefusesAndTheExactOriginalRestoresTheControl()
        {
            using var pending = MakePending("entry");
            var response = pending.Candidate.dialogue.episodes.Single().responses.Last();
            string original = response.responseKey;
            try
            {
                response.responseKey = original + "-tampered";
                Assert.That(Verify(pending.Candidate, pending.Handle, out _), Is.False);
                AssertPending(pending.Candidate, pending.Handle, false);
            }
            finally { response.responseKey = original; }
            AssertPending(pending.Candidate, pending.Handle, true);
            pending.AssertSourceUnchanged();
        }

        [Test]
        public void PendingReceiptPermissionDoesNotBypassTheUnrelatedCompleteEpisodeCore()
        {
            using var pending = MakePending("terminal");
            var edge = pending.Candidate.relationships.First();
            double score = edge.score;
            try
            {
                edge.score = double.NaN;
                Assert.That(Verify(pending.Candidate, pending.Handle, out string ownerError), Is.True, ownerError);
                Assert.That(CheckPending(pending.Candidate, pending.Handle, out string coreError), Is.False);
                Assert.That(coreError, Is.EqualTo("Invalid directed relationship graph."));
            }
            finally { edge.score = score; }
            AssertPending(pending.Candidate, pending.Handle, true);
            pending.AssertSourceUnchanged();
        }

        [Test]
        public void PendingReceiptPermissionCannotHideCorruptionOfAnActualSafetyPromise()
        {
            AssertActualCanonicalRefusal(UnifiedCommitments.Safety, "Invalid canonical safety identity.");
        }

        [Test]
        public void PendingReceiptPermissionCannotHideCorruptionOfAnActualVotePromise()
        {
            AssertActualCanonicalRefusal(UnifiedVoteTogether.Vote,
                "A Vote promise retains its unilateral source and native Story-null policy.");
        }

        private void AssertActualCanonicalRefusal(string family, string expectedError)
        {
            using var pending = MakePendingWithActualPromise(family, out string id);
            using var slot = new OwnedSlot();
            slot.Store.Save(pending.Source); slot.Store.Save(pending.Source);
            var saved = slot.Image();
            Assert.That(saved.Keys, Is.EquivalentTo(new[] { "episode.json", "episode.json.backup" }));
            var row = pending.Candidate.unifiedCommitments.Single(r => r.id == id);
            var sourceRow = pending.Source.unifiedCommitments.Single(r => r.id == id);
            Assert.That(row, Is.Not.SameAs(sourceRow));
            Assert.That(Json(row), Is.EqualTo(Json(sourceRow)));
            Assert.That(row.kind, Is.EqualTo(family));
            Assert.That(row.sourcePolicy, Is.EqualTo(UnifiedCommitments.PromisePolicy));
            Assert.That(row.origin, Is.EqualTo(UnifiedCommitments.NpcPromise));
            Assert.That(row.status, Is.EqualTo(DealStatus.Active));
            Assert.That(row.id, Does.StartWith("promise-npc-"));
            Assert.That(row.makerId, Is.Not.EqualTo(pending.Source.playerId));
            Assert.That(row.beneficiaryId, Is.Not.EqualTo(pending.Source.playerId));
            if (family == UnifiedVoteTogether.Vote)
                Assert.That(pending.Source.npcSocial.acts.Any(a => a.kind == NpcActKinds.Promise
                    && a.actorId == row.makerId && a.partnerId == row.beneficiaryId), Is.True,
                    "The retained Vote row has a real public-gameplay NPC Promise act.");
            // Safety's public Campaign-opening settlement has no NpcAct. Its stronger, captured
            // transition + native-ID + paired-receipt proof is required before this harness exists.
            string candidateImage = Json(pending.Candidate);
            string originalTarget = row.targetId, originalTrust = row.trustImpact;
            object handle = pending.Handle;
            var candidate = pending.Candidate;
            Assert.That(Verify(candidate, handle, out string beforeError), Is.True, beforeError);
            AssertPending(candidate, handle, true);
            try
            {
                if (family == UnifiedCommitments.Safety)
                {
                    Assert.That(originalTarget, Is.Null);
                    row.targetId = pending.Source.playerId;
                }
                else
                {
                    Assert.That(originalTrust, Is.EqualTo(DealTrust.Medium));
                    row.trustImpact = DealTrust.High;
                }
                string corruptedImage = Json(candidate);
                Assert.That(Verify(candidate, handle, out string ownerError), Is.True, ownerError);
                Assert.That(CheckPending(candidate, handle, out string coreError), Is.False);
                Assert.That(coreError, Is.EqualTo(expectedError),
                    "The live receipt permission must not hide this exact canonical-family corruption.");
                Assert.That(Json(candidate), Is.EqualTo(corruptedImage), "Both checks are read-only.");
                Assert.That(EpisodeValidation.TryValidate(candidate, out string strictError), Is.False);
                Assert.That(strictError, Is.Not.Null.And.Not.Empty);
                Assert.That(Assert.Throws<InvalidDataException>(() => slot.Store.Save(candidate)).Message,
                    Is.EqualTo(strictError), "Saved-state validation remains strict even for a live owned candidate.");
                Assert.That(Json(candidate), Is.EqualTo(corruptedImage));
                pending.AssertSourceUnchanged(); slot.AssertImage(saved);
            }
            finally { row.targetId = originalTarget; row.trustImpact = originalTrust; }

            Assert.That(pending.Candidate, Is.SameAs(candidate));
            Assert.That(pending.Handle, Is.SameAs(handle));
            Assert.That(Registered.GetValue(pending.Engine), Is.SameAs(handle));
            Assert.That(Json(candidate), Is.EqualTo(candidateImage), "Only the deliberately corrupted scalar was restored.");
            Assert.That(Verify(candidate, handle, out string restoredError), Is.True, restoredError);
            AssertPending(candidate, handle, true);
            Assert.That(slot.Store.TryLoad(out var loaded, out string loadError), Is.True, loadError);
            Assert.That(Json(loaded), Is.EqualTo(Json(pending.Source)));
            pending.AssertSourceUnchanged(); slot.AssertImage(saved);
        }

        private PendingHarness MakePendingWithActualPromise(string family, out string rowId)
        {
            // The row must pre-exist in a strict, publicly committed state. Safety must be new
            // in the actual public Campaign-opening transition, before its later NPC catch-up;
            // Vote retains the separately qualified real Promise-act witness after paid entry.
            // This is canonical-corruption coverage, NOT a sequential pending Safety-writer proof.
            // No promise, act, plan, AP, RNG, nominee or history row is manufactured.
            int nominatedCampaigns = 0, entries = 0;
            for (uint seed = 1; seed <= 24; seed++)
            {
                var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(fresh);
                Assert.That(fresh.dialogueRulesVersion, Is.Zero);
                fresh.dialogueRulesVersion = 1;
                Valid(fresh);
                string freshImage = Json(fresh);
                var engine = new EpisodeEngine(fresh);
                EpisodeState campaignBefore = null;
                CommandResult campaignTransition = null;
                for (int step = 0; step < 96; step++)
                {
                    var s = engine.Snapshot;
                    if (s.week != 1 || s.phase == EpisodePhase.Finished) break;
                    if (s.phase != EpisodePhase.Campaign)
                    {
                        var next = ModeTwoReaderSweep.Next(s);
                        if (family == UnifiedCommitments.Safety && s.phase == EpisodePhase.VetoMeeting && s.vetoResolved)
                        {
                            string beforeImage = Json(s), commandImage = Json(next);
                            var transition = engine.Apply(next);
                            Assert.That(transition.accepted, Is.True, next.kind + ": " + transition.reason);
                            Assert.That(transition.duplicate, Is.False);
                            Valid(transition.state);
                            Assert.That(Json(s), Is.EqualTo(beforeImage));
                            Assert.That(Json(next), Is.EqualTo(commandImage));
                            if (transition.state.phase == EpisodePhase.Campaign)
                            { campaignBefore = s; campaignTransition = transition; }
                        }
                        else Accepted(engine, next);
                        continue;
                    }
                    if (!s.nominees.Contains(s.playerId)) break;
                    nominatedCampaigns++;
                    CloseExistingAsks(engine);
                    s = engine.Snapshot;
                    string target = EpisodeEngine.Voters(s).Where(c => !c.isPlayer).Select(c => c.id)
                        .FirstOrDefault(id => DialogueRules.CanStart(s, id, out _));
                    if (target == null) break;
                    string sourceImage = Json(s);
                    var start = Command(s, EpisodeCommandKind.StartDialogue);
                    start.targetId = target; start.secondTargetId = "open-case"; start.text = Template;
                    var entered = Accepted(engine, start);
                    entries++;
                    Assert.That(Json(s), Is.EqualTo(sourceImage));
                    Assert.That(Json(fresh), Is.EqualTo(freshImage));
                    var row = entered.unifiedCommitments.FirstOrDefault(r => r.kind == family
                        && r.sourcePolicy == UnifiedCommitments.PromisePolicy
                        && r.origin == UnifiedCommitments.NpcPromise && r.status == DealStatus.Active
                        && r.id.StartsWith("promise-npc-", StringComparison.Ordinal)
                        && r.makerId != entered.playerId && r.beneficiaryId != entered.playerId
                        && (family == UnifiedCommitments.Safety
                            ? IsNewCampaignSettlementSafety(campaignBefore, campaignTransition, r)
                            : entered.npcSocial.acts.Any(a => a.week == entered.week && a.kind == NpcActKinds.Promise
                                && a.actorId == r.makerId && a.partnerId == r.beneficiaryId)));
                    if (row == null) break; // A different family or no proven row is not a passing witness.
                    if (family == UnifiedCommitments.Safety)
                    {
                        Assert.That(IsNewCampaignSettlementSafety(campaignBefore, campaignTransition, row), Is.True,
                            "A newly committed Campaign-opening Safety row retains both actual Give receipts.");
                        TestContext.Out.WriteLine("Safety settlement provenance: public revision "
                            + campaignBefore.revision + " -> " + campaignTransition.state.revision
                            + ", before next sequence " + campaignBefore.nextSequence
                            + ", catch-up starts " + campaignTransition.beatsFromSequence + ".");
                    }
                    rowId = row.id;
                    TestContext.Out.WriteLine("Canonical pending control: family " + family + ", seed " + seed
                        + ", actual entry revision " + entered.revision + ", row " + rowId + ".");
                    return new PendingHarness(engine, Choice(entered, "appeal-personally"), false);
                }
                Assert.That(Json(fresh), Is.EqualTo(freshImage));
            }
            rowId = null;
            Assert.Fail("No genuine active NPC " + family + " promise in 24 first weeks x 96 public commands"
                + ", at most 24 old-ask neutral replies and one actual paid entry per season. Nominated campaigns="
                + nominatedCampaigns + ", paid entries=" + entries + ". No synthetic row or skip substitutes for this witness.");
            return null;
        }

        private static bool IsNewCampaignSettlementSafety(EpisodeState before, CommandResult transition,
            UnifiedCommitmentState retained)
        {
            // Apply exposes the first sequence catch-up could write. Give mints its promise ID
            // before its sequence-free canonical store, then Record writes two directed receipts.
            // Thus p/p+1 before beatsFromSequence identify Execute's Campaign-opening settlement,
            // not a later beat, without inventing an NpcAct for a writer that does not create one.
            if (before == null || transition == null || retained == null || !transition.accepted || transition.duplicate
                || before.phase != EpisodePhase.VetoMeeting || !before.vetoResolved || !EpisodeEngine.AllWeekOn(before)
                || transition.state.phase != EpisodePhase.Campaign || transition.state.week != before.week
                || transition.beatsFromSequence <= before.nextSequence
                || retained.kind != UnifiedCommitments.Safety
                || retained.sourcePolicy != UnifiedCommitments.PromisePolicy
                || retained.origin != UnifiedCommitments.NpcPromise || retained.status != DealStatus.Active
                || retained.createdWeek != before.week || retained.expiresWeek != before.week + 1
                || retained.targetId != null || retained.trustImpact != DealTrust.Medium
                || !retained.id.StartsWith("promise-npc-", StringComparison.Ordinal)
                || !int.TryParse(retained.id.Substring("promise-npc-".Length),
                    System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int p)
                || p < before.nextSequence || (long)p + 1 >= transition.beatsFromSequence
                || before.unifiedCommitments.Any(r => r.id == retained.id)) return false;
            var after = transition.state;
            var written = after.unifiedCommitments.SingleOrDefault(r => r.id == retained.id);
            if (written == null || Json(written) != Json(retained)) return false;
            var maker = after.Find(retained.makerId); var beneficiary = after.Find(retained.beneficiaryId);
            if (maker == null || beneficiary == null || maker.isPlayer || beneficiary.isPlayer
                || maker.status != ContestantStatus.Active || beneficiary.status != ContestantStatus.Active) return false;
            var phase = after.events.Where(e => e.kind == "phase" && e.phase == EpisodePhase.Campaign
                && e.sequence >= before.nextSequence && e.sequence < transition.beatsFromSequence).ToList();
            if (phase.Count != 1 || p <= phase[0].sequence
                || before.relationships.SelectMany(r => r.events).Any(e => e.sequence == p || e.sequence == p + 1))
                return false;
            string description = maker.name + " gave " + beneficiary.name + " their word";
            bool Receipt(string from, string to, int sequence) => after.relationships
                .Where(r => r.fromId == from && r.toId == to).SelectMany(r => r.events)
                .Count(e => e.sequence == sequence && e.week == after.week && e.type == "promise-made"
                    && e.impactScore == 15 && !e.decayable && e.description == description) == 1;
            return Receipt(maker.id, beneficiary.id, p) && Receipt(beneficiary.id, maker.id, p + 1);
        }

        private PendingHarness MakePending(string stage)
        {
            Assert.That(Json(campaign), Is.EqualTo(campaignImage));
            var engine = new EpisodeEngine(campaign);
            if (stage != "entry")
            {
                var entered = Accepted(engine, Start(engine.Snapshot));
                if (stage == "terminal") Accepted(engine, Choice(entered, "appeal-personally"));
            }
            var before = engine.Snapshot;
            var command = stage == "entry" ? Start(before) : stage == "advance-lapse" ? Command(before, EpisodeCommandKind.Advance)
                : Choice(before, stage == "middle" ? "appeal-personally" : "make-plea");
            return new PendingHarness(engine, command, stage == "advance-lapse");
        }

        private EpisodeCommand Start(EpisodeState s)
        {
            var c = Command(s, EpisodeCommandKind.StartDialogue);
            c.targetId = voter; c.secondTargetId = "open-case"; c.text = Template; return c;
        }

        private static EpisodeCommand Choice(EpisodeState s, string option)
        {
            var episode = s.dialogue.episodes.Single();
            var c = Command(s, EpisodeCommandKind.ChooseDialogue);
            c.targetId = episode.id; c.secondTargetId = option;
            c.text = s.storylines.Single(x => x.id == episode.id).beatId; return c;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
        { id = "pending-save-" + s.revision + "-" + kind, actorId = s.playerId, kind = kind, expectedRevision = s.revision, expectedPhase = s.phase };

        private static EpisodeState Accepted(EpisodeEngine engine, EpisodeCommand command)
        {
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + ": " + result.reason);
            Assert.That(result.duplicate, Is.False); Valid(result.state); return result.state;
        }

        private static void CloseExistingAsks(EpisodeEngine engine)
        {
            for (int i = 0; i < 24; i++)
            {
                var s = engine.Snapshot;
                var item = s.houseEvents.FirstOrDefault(e => !e.resolved && HouseEventKind.Asks(e.kind));
                if (item == null) return;
                Assert.That(item.contentId == null || !item.contentId.StartsWith(Template + ":", StringComparison.Ordinal), Is.True);
                var c = Command(s, item.IsStory ? EpisodeCommandKind.ProgressStoryline : EpisodeCommandKind.ResolveHouseEvent);
                c.targetId = item.id;
                if (item.IsStory)
                {
                    var lapse = item.choices.Single(o => o.optionId == item.lapseOptionId);
                    Assert.That(lapse.locked || lapse.costsAction || lapse.pickPerson, Is.False);
                    c.secondTargetId = lapse.optionId;
                }
                else c.text = item.choices.First().label;
                Accepted(engine, c);
            }
            Assert.Fail("Old asks did not close within 24 public neutral replies; no history was cleared.");
        }

        private static void AssertPending(EpisodeState s, object handle, bool expected)
        {
            string before = Json(s);
            Assert.That(CheckPending(s, handle, out string error), Is.EqualTo(expected), error);
            if (!expected) Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(Json(s), Is.EqualTo(before), "Admission is read-only even on refusal.");
        }

        private static bool CheckPending(EpisodeState s, object handle, out string error)
        {
            var args = new object[] { s, handle, null };
            bool result = (bool)Invoke(Method(typeof(EpisodeValidation), "TryValidatePendingVote", Static,
                typeof(EpisodeState), PendingType, typeof(string).MakeByRefType()), null, args);
            error = (string)args[2]; return result;
        }

        private static bool Verify(EpisodeState s, object handle, out string error)
        {
            var args = new object[] { s, null };
            bool result = (bool)Invoke(Method(PendingType, "Verify", Instance, typeof(EpisodeState), typeof(string).MakeByRefType()), handle, args);
            error = (string)args[1]; return result;
        }

        private static MethodInfo Method(Type type, string name, BindingFlags flags, params Type[] args)
        {
            var method = type.GetMethod(name, flags, null, args, null);
            Assert.That(method, Is.Not.Null, type.Name + "." + name); return method;
        }

        private static object Invoke(MethodInfo method, object owner, object[] args)
        {
            try { return method.Invoke(owner, args); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }

        private static string Json(object value) => JsonConvert.SerializeObject(value, Formatting.None);
        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);

        private sealed class PendingHarness : IDisposable
        {
            internal readonly EpisodeEngine Engine;
            internal readonly EpisodeState Source, Candidate;
            internal object Handle { get; private set; }
            private readonly EpisodeCommand command;
            private readonly string sourceImage, commandImage;

            internal PendingHarness(EpisodeEngine engine, EpisodeCommand request, bool lapseOnly)
            {
                Engine = engine; command = request; Source = engine.Snapshot; Candidate = Source.Clone();
                sourceImage = Json(Source); commandImage = Json(request);
                Assert.That(Applying.GetValue(engine), Is.Null);
                Assert.That(Registered.GetValue(engine), Is.Null);
                Applying.SetValue(engine, Candidate);
                try
                {
                    Handle = Invoke(Method(typeof(EpisodeEngine), "PreparePaidCampaignCommand", Instance,
                        typeof(EpisodeState), typeof(EpisodeCommand)), engine, new object[] { Candidate, request });
                    Assert.That(Handle, Is.Not.Null);
                    Assert.That(Registered.GetValue(engine), Is.SameAs(Handle));
                    if (lapseOnly)
                    {
                        var episode = Candidate.dialogue.episodes.Single();
                        var cycle = Candidate.storylines.Single(c => c.id == episode.id);
                        var item = Candidate.houseEvents.Single(e => e.id == cycle.eventId && !e.resolved);
                        Assert.That((bool)Invoke(Method(PendingType, "LapseAt", Instance, typeof(EpisodeState), typeof(HouseEventState)),
                            Handle, new object[] { Candidate, item }), Is.True);
                    }
                    else Invoke(Method(typeof(EpisodeEngine), "Execute", Static, typeof(EpisodeState), typeof(EpisodeCommand), PendingType),
                        null, new[] { (object)Candidate, request, Handle });
                    Assert.That(Applying.GetValue(engine), Is.SameAs(Candidate));
                    AssertPending(Candidate, Handle, true);
                    AssertSourceUnchanged();
                }
                catch { Dispose(); throw; }
            }

            internal void AssertSourceUnchanged()
            {
                Assert.That(Json(Source), Is.EqualTo(sourceImage));
                Assert.That(Json(Engine.Snapshot), Is.EqualTo(sourceImage), "The private harness never publishes its candidate.");
                Assert.That(Json(command), Is.EqualTo(commandImage));
            }

            internal void Retire()
            {
                if (Handle != null) Invoke(Method(PendingType, "Retire", Instance, Type.EmptyTypes), Handle, Array.Empty<object>());
            }

            public void Dispose()
            {
                try { Retire(); }
                finally { Applying.SetValue(Engine, null); }
            }
        }

        private sealed class OwnedSlot : IDisposable
        {
            private readonly string leaf = "GamesimDialoguePending-" + Guid.NewGuid().ToString("N");
            private readonly string root;
            internal readonly EpisodeSaveStore Store;

            internal OwnedSlot()
            {
                root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), leaf));
                Guard(); Assert.That(Directory.Exists(root) || File.Exists(root), Is.False);
                Directory.CreateDirectory(root); Store = new EpisodeSaveStore(Path.Combine(root, "episode.json"));
            }

            internal Dictionary<string, byte[]> Image()
            {
                Guard(); return Directory.GetFiles(root).ToDictionary(Path.GetFileName, File.ReadAllBytes, StringComparer.Ordinal);
            }

            internal void AssertImage(Dictionary<string, byte[]> expected)
            {
                var actual = Image();
                Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), "Refusal must not leave a pending/recovery file or change slot membership.");
                foreach (var file in expected) Assert.That(actual[file.Key], Is.EqualTo(file.Value), file.Key);
            }

            private void Guard()
            {
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Assert.That(Path.GetDirectoryName(root), Is.EqualTo(temp));
                Assert.That(Path.GetFileName(root), Is.EqualTo(leaf));
                for (string at = root; !string.IsNullOrEmpty(at); at = Path.GetDirectoryName(at))
                    if (Directory.Exists(at) || File.Exists(at))
                        Assert.That(File.GetAttributes(at) & FileAttributes.ReparsePoint, Is.EqualTo((FileAttributes)0));
                if (!Directory.Exists(root)) return;
                Assert.That(Directory.GetDirectories(root), Is.Empty);
                foreach (string file in Directory.GetFiles(root))
                    Assert.That(File.GetAttributes(file) & FileAttributes.ReparsePoint, Is.EqualTo((FileAttributes)0));
            }

            public void Dispose()
            {
                Guard();
                if (!Directory.Exists(root)) return;
                foreach (string file in Directory.GetFiles(root)) File.Delete(file);
                Directory.Delete(root);
            }
        }
    }
}
#endif
