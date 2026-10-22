using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private const BindingFlags PortAdapterPrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        private PortVerification BindPortConversationVerifier(out GameObject owner)
        {
            // Start is the standalone workload and must never run inside this fixture.
            owner = new GameObject("Inert port conversation companion fixture");
            owner.SetActive(false);
            var runner = owner.AddComponent<PortVerification>();
            runner.enabled = false;
            var type = typeof(PortVerification);
            type.GetField("seasonDirector", PortAdapterPrivate).SetValue(runner, director);
            type.GetField("seasonPlayer", PortAdapterPrivate).SetValue(runner, player);
            type.GetField("seasonStarted", PortAdapterPrivate).SetValue(runner, Time.realtimeSinceStartupAsDouble);
            var reportType = type.GetNestedType("SeasonReport", BindingFlags.NonPublic);
            type.GetField("seasonReport", PortAdapterPrivate).SetValue(runner, Activator.CreateInstance(reportType, true));
            return runner;
        }

        private static IEnumerator PortConversationRoutine(PortVerification runner, string name, params object[] arguments)
        {
            var method = typeof(PortVerification).GetMethod(name, PortAdapterPrivate);
            Assert.That(method, Is.Not.Null, "Exercise the production verification companion: " + name);
            return (IEnumerator)method.Invoke(runner, arguments);
        }

        private static T PortConversationReport<T>(PortVerification runner, string field)
        {
            var report = typeof(PortVerification).GetField("seasonReport", PortAdapterPrivate).GetValue(runner);
            return (T)report.GetType().GetField(field).GetValue(report);
        }

        private byte[] PortConversationBackupBytes() =>
            File.Exists(new EpisodeSaveStore(director.SavePath).BackupPath)
                ? File.ReadAllBytes(new EpisodeSaveStore(director.SavePath).BackupPath) : null;

        private void AssertPortConversationUnchanged(EpisodeState before, byte[] primary, byte[] backup)
        {
            var after = director.Snapshot;
            Assert.That(JsonUtility.ToJson(after), Is.EqualTo(JsonUtility.ToJson(before)));
            Assert.That(after.randomState, Is.EqualTo(before.randomState));
            Assert.That(after.npcSocial.randomState, Is.EqualTo(before.npcSocial.randomState));
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before)));
            Assert.That(EpisodeEngine.SocialActionBudget(after), Is.EqualTo(EpisodeEngine.SocialActionBudget(before)));
            CollectionAssert.AreEqual(before.acceptedCommandIds, after.acceptedCommandIds);
            CollectionAssert.AreEqual(primary, File.ReadAllBytes(director.SavePath));
            var currentBackup = PortConversationBackupBytes();
            Assert.That(currentBackup == null, Is.EqualTo(backup == null));
            if (backup != null) CollectionAssert.AreEqual(backup, currentBackup);
        }

        private static CommandResult PortConversationPrehistoryCommand(EpisodeEngine engine,
            EpisodeCommandKind kind, string target, string text = null)
        {
            var before = engine.Snapshot;
            var result = engine.Apply(new EpisodeCommand
            {
                id = "port-conversation-prehistory-" + before.seed + "-" + before.revision,
                actorId = before.playerId, expectedRevision = before.revision, expectedPhase = before.phase,
                kind = kind, targetId = target, text = text,
            });
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.revision, Is.EqualTo(before.revision + 1));
            return result;
        }

        private static EpisodeState PortConversationWarmPrehistory(uint seed, int conversations)
        {
            // Authored six-person source scenario, not an old-owner-save fixture. Every
            // relationship and extra opportunity comes from the existing engine's real commands.
            var state = ContentCatalog.Create(seed);
            var engine = new EpisodeEngine(state);
            string payer = state.Active.First(person => !person.isPlayer && person.id != ContentCatalog.MayaId).id;
            for (int purchase = 0; purchase < WebSocialVocabulary.PurchaseCeiling; purchase++)
                PortConversationPrehistoryCommand(engine, EpisodeCommandKind.BuyActionPoint, payer, WebSocialVocabulary.BurnOne);
            for (int social = 0; social < conversations; social++)
                PortConversationPrehistoryCommand(engine, EpisodeCommandKind.RelationshipBuilding, ContentCatalog.MayaId);
            return engine.Snapshot;
        }

        private IEnumerator InstallPortConversationState(EpisodeState state)
        {
            HoldTheHouseForTheFixture();
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            player.Agent.speed = 25f;
            player.Agent.acceleration = 100f;
            yield return null;
        }

        private void AssertPortConversationMatrixCompletionContract(PortVerification runner)
        {
            // Unit-only, deliberately nonvisual sentinels. No report is published, no screenshot
            // is fabricated and none of these one-byte files is admitted as rendered evidence.
            var type = typeof(PortVerification);
            var reportType = type.GetNestedType("LookSheetReport", BindingFlags.NonPublic);
            var report = Activator.CreateInstance(reportType, true);
            var shotsField = reportType.GetField("shots");
            var shots = (IList)shotsField.GetValue(report);
            string folder = Path.Combine(temporaryDirectory, "ConversationMatrixCompletionContract");
            Directory.CreateDirectory(folder);
            for (int index = 0; index < 12; index++)
            {
                var shot = Activator.CreateInstance(shotsField.FieldType.GetGenericArguments()[0], true);
                string path = Path.Combine(folder, "route-sentinel-" + index);
                File.WriteAllBytes(path, new byte[] { 1 });
                shot.GetType().GetField("path").SetValue(shot, path);
                shot.GetType().GetField("reached").SetValue(shot, true);
                shots.Add(shot);
            }
            var layoutsField = reportType.GetField("layouts");
            var layouts = (IList)layoutsField.GetValue(report);
            for (int index = 0; index < 31; index++)
                layouts.Add(Activator.CreateInstance(layoutsField.FieldType.GetGenericArguments()[0], true));
            var rowsField = reportType.GetField("conversationLayouts");
            var rows = (IList)rowsField.GetValue(report);
            var frames = (IList)reportType.GetField("conversationFrames").GetValue(report);
            foreach (string view in new[] { "selected-game", "selected-connect", "exchange-player", "exchange-npc" })
            foreach (var viewport in new[] { new Vector2Int(1280,720), new Vector2Int(1600,900),
                new Vector2Int(1920,1080), new Vector2Int(1200,900), new Vector2Int(2560,1080) })
            foreach (Gamesim.Presentation.TextSize text in Gamesim.Presentation.TextSizes.All)
            foreach (bool compact in new[] { false, true })
            {
                var row = Activator.CreateInstance(rowsField.FieldType.GetGenericArguments()[0], true);
                string path = Path.Combine(folder, "conversation-sentinel-" + rows.Count);
                File.WriteAllBytes(path, new byte[] { 1 });
                var rowType = row.GetType();
                rowType.GetField("view").SetValue(row, view); rowType.GetField("path").SetValue(row, path);
                rowType.GetField("width").SetValue(row, viewport.x); rowType.GetField("height").SetValue(row, viewport.y);
                rowType.GetField("textSize").SetValue(row, text); rowType.GetField("compactHud").SetValue(row, compact);
                rowType.GetField("npcId").SetValue(row, "unit-sentinel-npc");
                rows.Add(row); frames.Add(path);
            }
            Assert.That(rows.Count, Is.EqualTo(120), "All four views, five aspects, three actual text sizes and two chrome sizes.");
            type.GetField("lookReport", PortAdapterPrivate).SetValue(runner, report);
            var complete = type.GetMethod("CompleteLookSheetReport", PortAdapterPrivate);
            void StatusIs(string expected)
            {
                Assert.That((int)complete.Invoke(runner, null), Is.EqualTo(12));
                Assert.That((string)reportType.GetField("status").GetValue(report), Is.EqualTo(expected));
            }
            StatusIs("Passed");
            object first = rows[0];
            rows.RemoveAt(0); StatusIs("Failed"); rows.Insert(0, first); StatusIs("Passed");
            rows[0] = rows[1]; StatusIs("Failed"); rows[0] = first; StatusIs("Passed");
            var viewField = first.GetType().GetField("view");
            object viewBefore = viewField.GetValue(first);
            viewField.SetValue(first, "invented-view"); StatusIs("Failed");
            viewField.SetValue(first, viewBefore); StatusIs("Passed");
        }

        [UnityTest]
        public IEnumerator PortConversationAdapters_CategoriesPreserveStateAndBothDurableSlotImages()
        {
            yield return InstallTalkingHouse(8, false);
            yield return TalkTo(Listener(director.Snapshot).id);
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                // TryOpenNpc already flushed/paused the NPC clock; navigation owns no clock tick.
                var before = director.Snapshot;
                var primary = File.ReadAllBytes(director.SavePath);
                var backup = PortConversationBackupBytes();
                foreach (string category in new[]
                {
                    EpisodeDirector.LearnCategoryCaption, EpisodeDirector.DealsCategoryCaption,
                    EpisodeDirector.ConnectCategoryCaption, EpisodeDirector.GameCategoryCaption,
                })
                {
                    yield return PortConversationRoutine(runner, "EnterSeasonConversationCategory", category);
                    Assert.That(director.CurrentConversationCategory, Is.EqualTo(category));
                    AssertPortConversationUnchanged(before, primary, backup);
                }
                yield return PortConversationRoutine(runner, "ReturnSeasonConversationTopics");
                Assert.That(director.CurrentConversationCategory, Is.Null);
                AssertPortConversationUnchanged(before, primary, backup);
                Assert.That(PortConversationReport<int>(runner, "conversationNavigations"), Is.GreaterThan(0));
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.Zero);
                AssertPortConversationMatrixCompletionContract(runner);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_SameCategoryRestoresNestedDealsAndRealInvitedOffer()
        {
            yield return InstallTalkingHouse(8, false);
            string listener = Listener(director.Snapshot).id;
            yield return TalkTo(listener);
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                yield return PortConversationRoutine(runner, "EnterSeasonConversationCategory", EpisodeDirector.DealsCategoryCaption);
                var before = director.Snapshot;
                var primary = File.ReadAllBytes(director.SavePath);
                var backup = PortConversationBackupBytes();
                var subjects = PlayerDeals.Subjects(before, listener).ToArray();
                Assert.That(subjects, Is.Not.Empty, "The existing authored house must offer a real target-agreement picker.");
                ButtonWithCaption(EpisodeDirector.TargetDealPickerCaption).onClick.Invoke();
                yield return null; yield return null;
                string targetCaption = EpisodeHud.DealProposeCaption(
                    DealKind.Title(DealKind.TargetAgreement).ToLowerInvariant() + " against " + before.Find(subjects[0]).name);
                Assert.That(ButtonWithCaptionOrNull(targetCaption), Is.Not.Null,
                    "Enter the actual visible child page, not a manufactured picker flag.");
                Assert.That(ButtonWithCaptionOrNull("Promise safety"), Is.Null,
                    "The selected target picker hides the ordinary Deals verbs.");
                AssertPortConversationUnchanged(before, primary, backup);
                int navigations = PortConversationReport<int>(runner, "conversationNavigations");

                yield return PortConversationRoutine(runner, "EnterSeasonConversationCategory", EpisodeDirector.DealsCategoryCaption);

                Assert.That(director.CurrentConversationCategory, Is.EqualTo(EpisodeDirector.DealsCategoryCaption));
                Assert.That(ButtonWithCaptionOrNull("Promise safety"), Is.Not.Null,
                    "The same-category helper must restore ordinary children, not report a hidden leaf as unavailable.");
                Assert.That(ButtonWithCaptionOrNull(targetCaption), Is.Null);
                Assert.That(PortConversationReport<int>(runner, "conversationNavigations"), Is.EqualTo(navigations + 2),
                    "One actual Back-to-topics and one actual category gesture; no hidden control invocation.");
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.Zero);
                AssertPortConversationUnchanged(before, primary, backup);

                // Exercise the unchanged production companion after normalization. A legitimate
                // proposal/refusal must be counted, rather than a hidden child becoming Not reached.
                yield return PortConversationRoutine(runner, "ExerciseOptionalDeal", false);
                Assert.That(PortConversationReport<int>(runner, "dealsProposed"), Is.EqualTo(1));
                Assert.That(PortConversationReport<string>(runner, "dealOutcome"), Is.Not.Null.And.Not.EqualTo("Not reached"));
                Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1));
                Assert.That(director.Snapshot.acceptedCommandIds.Count, Is.EqualTo(before.acceptedCommandIds.Count + 1));
                Assert.That(director.Snapshot.events.Any(entry => entry.sequence >= before.nextSequence && entry.kind == "deal"), Is.True);
                Assert.That(director.IsConversationExchangePlaying, Is.False);
                AssertIntentDurable(director.Snapshot);
            }
            finally { UnityEngine.Object.Destroy(owner); }

            // Reuse the existing authored incoming-offer fixture: the real NPC generator and
            // ordinary physical approach produce the invitation. No new score/offer/lease,
            // picker or invitation state is installed by this regression.
            yield return InstallNpcInvitationFlowHouse();
            string offerId = director.CurrentNpcInvitationRecordId;
            string proposer = director.CurrentNpcInvitationNpcId;
            var hear = ButtonWithCaption(HouseNpcInvitationBubble.HearCaption);
            Assert.That(hear, Is.SameAs(director.GetComponent<HouseNpcInvitationBubble>().HearControl));
            hear.onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.TalkingToId, Is.EqualTo(proposer));
            Assert.That(director.CurrentConversationCategory, Is.EqualTo(EpisodeDirector.DealsCategoryCaption));
            Assert.That(director.InvitedDealRecordId, Is.EqualTo(offerId));
            Assert.That(ButtonWithCaptionOrNull("Promise safety"), Is.Null,
                "The real invited offer initially owns this same category's surface.");
            runner = BindPortConversationVerifier(out owner);
            try
            {
                var before = director.Snapshot;
                var primary = File.ReadAllBytes(director.SavePath);
                var backup = PortConversationBackupBytes();
                Assert.That(NpcDeals.Pending(before).Any(offer => offer.id == offerId && offer.proposerId == proposer), Is.True);

                yield return PortConversationRoutine(runner, "EnterSeasonConversationCategory", EpisodeDirector.DealsCategoryCaption);

                Assert.That(director.CurrentConversationCategory, Is.EqualTo(EpisodeDirector.DealsCategoryCaption));
                Assert.That(director.InvitedDealRecordId, Is.Null,
                    "The actual Back/topic gestures clear only the invitation presentation, not its saved offer.");
                Assert.That(ButtonWithCaptionOrNull("Promise safety"), Is.Not.Null,
                    "Ordinary Deals verbs must be visible after returning from the invited child view.");
                Assert.That(NpcDeals.Pending(director.Snapshot).Any(offer => offer.id == offerId && offer.proposerId == proposer), Is.True,
                    "Navigation must not accept, decline or replace the exact pending offer.");
                Assert.That(PortConversationReport<int>(runner, "conversationNavigations"), Is.EqualTo(2));
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.Zero);
                AssertPortConversationUnchanged(before, primary, backup);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_ReadWalkReachesAskVoteAndReadPerson()
        {
            yield return InstallTalkingHouse(8, true, state => EpisodeEngine.EnableRead(state));
            player.Agent.speed = 25f; player.Agent.acceleration = 100f;
            Assert.That(VoteRead.Available(director.Snapshot), Is.True, "The fixture has a real campaign vote to read.");
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                int spent = EpisodeEngine.SocialActionsSpent(director.Snapshot);
                int commands = director.Snapshot.acceptedCommandIds.Count;
                yield return PortConversationRoutine(runner, "ExerciseSeasonRead", director.Snapshot, false);
                yield return PortConversationRoutine(runner, "ExerciseSeasonRead", director.Snapshot, false);
                Assert.That(PortConversationReport<int>(runner, "votesAsked"), Is.EqualTo(1));
                Assert.That(PortConversationReport<int>(runner, "peopleRead"), Is.EqualTo(1));
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.EqualTo(2));
                Assert.That(director.Snapshot.acceptedCommandIds.Count, Is.EqualTo(commands + 2));
                Assert.That(EpisodeEngine.SocialActionsSpent(director.Snapshot), Is.EqualTo(spent));
                Assert.That(director.Snapshot.events.Count(entry => entry.kind == "vote-read"), Is.GreaterThanOrEqualTo(1));
                Assert.That(director.IsConversationExchangePlaying, Is.False);
                AssertIntentDurable(director.Snapshot);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_ProposalWalkRecordsARealRefusal()
        {
            EpisodeState fixture = null;
            for (uint seed = 6301; seed < 6333 && fixture == null; seed++)
            {
                var candidate = ContentCatalog.Create(seed);
                string kind = PlayerDeals.Available(candidate, ContentCatalog.MayaId)
                    .FirstOrDefault(item => item != DealKind.TargetAgreement);
                Assert.That(kind, Is.Not.Null, "The ordinary generated house offers a legal proposal.");
                var engine = new EpisodeEngine(candidate);
                var answer = PortConversationPrehistoryCommand(engine, EpisodeCommandKind.ProposeDeal, ContentCatalog.MayaId, kind);
                if (answer.state.events.Where(entry => entry.sequence >= candidate.nextSequence).Any(entry => entry.kind == "deal")
                    && !CommitmentReferences.Deals(answer.state).Any(deal => deal.proposerId == candidate.playerId))
                    fixture = candidate;
            }
            Assert.That(fixture, Is.Not.Null, "A bounded actual source-command search must find a refused proposal; no refusal is fabricated.");
            yield return InstallPortConversationState(fixture);
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                var before = director.Snapshot;
                yield return PortConversationRoutine(runner, "ExerciseOptionalDeal", false);
                Assert.That(PortConversationReport<int>(runner, "dealsProposed"), Is.EqualTo(1));
                Assert.That(PortConversationReport<string>(runner, "dealOutcome"), Is.EqualTo("Refused"));
                Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1));
                Assert.That(director.Snapshot.acceptedCommandIds.Count, Is.EqualTo(before.acceptedCommandIds.Count + 1));
                Assert.That(director.Snapshot.events.Any(entry => entry.sequence >= before.nextSequence && entry.kind == "deal"), Is.True);
                Assert.That(director.IsConversationExchangePlaying, Is.False);
                AssertIntentDurable(director.Snapshot);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_PendingOfferWalkSettlesExactGeneratedOffer()
        {
            EpisodeState fixture = null;
            string offerId = null;
            for (uint seed = 6401; seed < 6433 && fixture == null; seed++)
            {
                var candidate = PortConversationWarmPrehistory(seed, 7);
                NpcDeals.Propose(candidate); // The actual NPC generator, not a hand-written offer.
                var offer = NpcDeals.Pending(candidate).FirstOrDefault(deal => deal.proposerId == ContentCatalog.MayaId);
                if (offer != null && EpisodeEngine.SocialActionsSpent(candidate) < EpisodeEngine.SocialActionBudget(candidate))
                { fixture = candidate; offerId = offer.id; }
            }
            Assert.That(fixture, Is.Not.Null, "Command-earned warmth must produce a genuine pending offer.");
            yield return InstallPortConversationState(fixture);
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                var before = director.Snapshot;
                yield return PortConversationRoutine(runner, "ExerciseOptionalDeal", false);
                Assert.That(PortConversationReport<int>(runner, "dealsAnswered"), Is.EqualTo(1));
                var answered = PortVerification.SeasonDeal(director.Snapshot, offerId);
                Assert.That(answered, Is.Not.Null);
                Assert.That(answered.status, Is.Not.EqualTo(DealStatus.Proposed));
                int proposals = PortConversationReport<int>(runner, "dealsProposed");
                Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1 + proposals));
                Assert.That(director.Snapshot.acceptedCommandIds.Count, Is.EqualTo(before.acceptedCommandIds.Count + 1 + proposals));
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.EqualTo(1 + proposals));
                Assert.That(EpisodeEngine.SocialActionsSpent(director.Snapshot),
                    Is.EqualTo(EpisodeEngine.SocialActionsSpent(before) + proposals), "The incoming acceptance stays free.");
                Assert.That(director.IsConversationExchangePlaying, Is.False);
                AssertIntentDurable(director.Snapshot);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_FreePendingOfferSettlesAtLegallyExhaustedAp()
        {
            EpisodeState fixture = null;
            string offerId = null;
            for (uint seed = 6401; seed < 6433 && fixture == null; seed++)
            {
                var engine = new EpisodeEngine(PortConversationWarmPrehistory(seed, 7));
                var warm = engine.Snapshot;
                string other = warm.Active.First(person => !person.isPlayer && person.id != ContentCatalog.MayaId).id;
                int remaining = EpisodeEngine.SocialActionBudget(warm) - EpisodeEngine.SocialActionsSpent(warm);
                Assert.That(remaining, Is.InRange(1, EpisodeEngine.LegacySocialActionBudget + WebSocialVocabulary.PurchaseCeiling),
                    "The command-earned source has a bounded positive allowance to exhaust, not assigned AP.");
                for (int paid = 0; paid < remaining; paid++)
                {
                    var prior = engine.Snapshot;
                    var paidResult = PortConversationPrehistoryCommand(engine, EpisodeCommandKind.SmallTalk, other);
                    Assert.That(paidResult.state.phase, Is.EqualTo(prior.phase));
                    Assert.That(EpisodeEngine.SocialActionBudget(paidResult.state),
                        Is.EqualTo(EpisodeEngine.SocialActionBudget(prior)));
                    Assert.That(EpisodeEngine.SocialActionsSpent(paidResult.state),
                        Is.EqualTo(EpisodeEngine.SocialActionsSpent(prior) + 1));
                    Assert.That(paidResult.state.acceptedCommandIds.Count, Is.EqualTo(prior.acceptedCommandIds.Count + 1));
                    CollectionAssert.AreEqual(prior.acceptedCommandIds,
                        paidResult.state.acceptedCommandIds.Take(prior.acceptedCommandIds.Count).ToArray());
                }
                var candidate = engine.Snapshot;
                Assert.That(EpisodeEngine.SocialActionsSpent(candidate), Is.EqualTo(EpisodeEngine.SocialActionBudget(candidate)),
                    "Exactly zero AP remains after real accepted paid commands.");
                NpcDeals.Propose(candidate); // The existing actual NPC generator; no authored offer or budget/RNG reset.
                var offer = NpcDeals.Pending(candidate).FirstOrDefault(deal => deal.proposerId == ContentCatalog.MayaId);
                if (offer != null) { fixture = candidate; offerId = offer.id; }
            }
            Assert.That(fixture, Is.Not.Null, "The existing bounded command-earned cohort must yield a genuine offer at zero AP.");
            yield return InstallPortConversationState(fixture);
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                var before = director.Snapshot;
                string savePath = director.SavePath;
                var primary = File.ReadAllBytes(savePath);
                var backup = PortConversationBackupBytes();
                int spent = EpisodeEngine.SocialActionsSpent(before), budget = EpisodeEngine.SocialActionBudget(before);
                Assert.That(spent, Is.EqualTo(budget));
                var originalOffer = NpcDeals.Pending(before).Single(deal => deal.id == offerId);
                Assert.That(originalOffer.proposerId, Is.EqualTo(ContentCatalog.MayaId));
                Assert.That(originalOffer.recipientId, Is.EqualTo(before.playerId));
                Assert.That(originalOffer.status, Is.EqualTo(DealStatus.Proposed));
                yield return TalkTo(ContentCatalog.MayaId);
                yield return PortConversationRoutine(runner, "EnterSeasonConversationCategory", EpisodeDirector.DealsCategoryCaption);
                var staleAccept = ButtonWithCaption(EpisodeHud.DealAcceptCaption).onClick;
                AssertPortConversationUnchanged(before, primary, backup);

                yield return PortConversationRoutine(runner, "ExerciseOptionalDeal", false);

                var after = director.Snapshot;
                var answered = PortVerification.SeasonDeal(after, offerId);
                Assert.That(PortConversationReport<int>(runner, "dealsAnswered"), Is.EqualTo(1),
                    "The free incoming answer must not be skipped by the paid-action budget guard.");
                Assert.That(PortConversationReport<int>(runner, "dealsProposed"), Is.Zero,
                    "An exhausted allowance still forbids the ordinary paid proposal.");
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.EqualTo(1));
                Assert.That(after.revision, Is.EqualTo(before.revision + 1));
                Assert.That(after.acceptedCommandIds.Count, Is.EqualTo(before.acceptedCommandIds.Count + 1));
                CollectionAssert.AreEqual(before.acceptedCommandIds,
                    after.acceptedCommandIds.Take(before.acceptedCommandIds.Count).ToArray());
                Assert.That(after.acceptedCommandIds.Last(), Is.Not.Empty);
                Assert.That(answered, Is.Not.Null);
                Assert.That(answered.id, Is.EqualTo(originalOffer.id));
                Assert.That(answered.proposerId, Is.EqualTo(originalOffer.proposerId));
                Assert.That(answered.recipientId, Is.EqualTo(originalOffer.recipientId));
                Assert.That(answered.type, Is.EqualTo(originalOffer.type));
                Assert.That(answered.status, Is.EqualTo(DealStatus.Active));
                Assert.That(NpcDeals.Pending(after).Any(deal => deal.id == offerId), Is.False);
                Assert.That(after.events.Any(entry => entry.sequence >= before.nextSequence && entry.kind == "deal"), Is.True);
                Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(spent));
                Assert.That(EpisodeEngine.SocialActionBudget(after), Is.EqualTo(budget));
                Assert.That(PortConversationReport<string>(runner, "dealKind"), Is.EqualTo(originalOffer.type));
                Assert.That(PortConversationReport<string>(runner, "dealOutcome"), Is.EqualTo(answered.status));
                Assert.That(director.IsConversationExchangePlaying, Is.False);
                Assert.That(director.SavePath, Is.EqualTo(savePath));
                AssertIntentDurable(after);
                CollectionAssert.AreEqual(primary, File.ReadAllBytes(new EpisodeSaveStore(savePath).BackupPath),
                    "One accepted response rotates the exact previous primary into the backup.");
                var committedPrimary = File.ReadAllBytes(savePath);
                var committedBackup = PortConversationBackupBytes();

                staleAccept.Invoke(); yield return null;
                yield return PortConversationRoutine(runner, "DrainSeasonConversationExchange", "free-offer-repeat", false, null, true);
                AssertPortConversationUnchanged(after, committedPrimary, committedBackup);
                director.LoadNow(); HoldTheHouseForTheFixture(); yield return null;
                AssertPortConversationUnchanged(after, committedPrimary, committedBackup);
                Assert.That(PortVerification.SeasonDeal(director.Snapshot, offerId).status, Is.EqualTo(DealStatus.Active));
                AssertIntentDurable(after);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_OathWalkEarnsAndPersistsDeclarationFromCommandOnlyPrehistory()
        {
            EpisodeState fixture = null;
            for (uint seed = 6501; seed < 6533 && fixture == null; seed++)
            {
                var candidate = PortConversationWarmPrehistory(seed, 7);
                double score = candidate.Score(candidate.playerId, ContentCatalog.MayaId);
                if (score >= 65 && score < 75 && !candidate.oathOpportunities.Contains(ContentCatalog.MayaId)
                    && !candidate.Allied(candidate.playerId, ContentCatalog.MayaId)
                    && EpisodeEngine.SocialActionBudget(candidate) - EpisodeEngine.SocialActionsSpent(candidate) >= 2)
                    fixture = candidate;
            }
            Assert.That(fixture, Is.Not.Null, "A bounded command-only prehistory must reach a real near-threshold relationship with time left.");
            yield return InstallPortConversationState(fixture);
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                var before = director.Snapshot;
                Assert.That(before.oathOpportunities, Does.Not.Contain(ContentCatalog.MayaId));
                yield return PortConversationRoutine(runner, "ExerciseOptionalOath", false);
                Assert.That(PortConversationReport<string>(runner, "oathOutcome"), Is.EqualTo("Legally earned and declared"));
                Assert.That(director.Snapshot.loyaltyOaths.Any(oath => oath.playerId == before.playerId
                    && oath.targetId == ContentCatalog.MayaId), Is.True);
                Assert.That(director.Snapshot.events.Any(entry => entry.sequence >= before.nextSequence && entry.kind == "relationship-milestone"), Is.True);
                Assert.That(director.Snapshot.alliances.Count, Is.EqualTo(before.alliances.Count + 1),
                    "The unchanged policy makes its one legal alliance proposal through Deals.");
                int social = PortConversationReport<int>(runner, "optionalSocialCommands");
                Assert.That(social, Is.GreaterThan(0));
                Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + social + 1));
                Assert.That(director.Snapshot.acceptedCommandIds.Count, Is.EqualTo(before.acceptedCommandIds.Count + social + 1));
                Assert.That(director.IsConversationExchangePlaying, Is.False);
                Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var loaded, out var reason), Is.True, reason);
                Assert.That(loaded.loyaltyOaths.Any(oath => oath.playerId == before.playerId && oath.targetId == ContentCatalog.MayaId), Is.True);
                AssertIntentDurable(director.Snapshot);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_OldControlsRemainRetiredAcrossNavigationCaptureRevisionAndLoad()
        {
            yield return InstallTalkingHouse(8, false);
            string listener = Listener(director.Snapshot).id;
            yield return TalkTo(listener);
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                var oldCategory = ButtonWithCaption(EpisodeDirector.DealsCategoryCaption).onClick;
                yield return PortConversationRoutine(runner, "EnterSeasonConversationCategory", EpisodeDirector.ConnectCategoryCaption);
                var oldPaid = ButtonWithCaption(EpisodeHud.SmallTalkCaption).onClick;
                yield return PortConversationRoutine(runner, "EnterSeasonConversationCategory", EpisodeDirector.LearnCategoryCaption);
                var before = director.Snapshot;
                var primary = File.ReadAllBytes(director.SavePath);
                var backup = PortConversationBackupBytes();
                oldCategory.Invoke(); oldPaid.Invoke(); yield return null;
                AssertPortConversationUnchanged(before, primary, backup);

                var preCaptureRead = ButtonWithCaption(EpisodeHud.ReadPersonCaption).onClick;
                yield return CaptureFraming("port-conversation-stale-callbacks", settle: false);
                preCaptureRead.Invoke(); yield return null;
                AssertPortConversationUnchanged(before, primary, backup);

                yield return PortConversationRoutine(runner, "EnterSeasonConversationCategory", EpisodeDirector.ConnectCategoryCaption);
                ButtonWithCaption(EpisodeHud.SmallTalkCaption).onClick.Invoke(); yield return null;
                Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1));
                var oldContinue = ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick;
                yield return PortConversationRoutine(runner, "DrainSeasonConversationExchange", "stale", false, null, false);
                before = director.Snapshot; primary = File.ReadAllBytes(director.SavePath); backup = PortConversationBackupBytes();
                oldCategory.Invoke(); oldPaid.Invoke(); preCaptureRead.Invoke(); oldContinue.Invoke(); yield return null;
                AssertPortConversationUnchanged(before, primary, backup);

                director.LoadNow(); HoldTheHouseForTheFixture(); yield return null;
                yield return TalkTo(listener);
                before = director.Snapshot; primary = File.ReadAllBytes(director.SavePath); backup = PortConversationBackupBytes();
                oldCategory.Invoke(); oldPaid.Invoke(); preCaptureRead.Invoke(); oldContinue.Invoke(); yield return null;
                AssertPortConversationUnchanged(before, primary, backup);
                Assert.That(director.CurrentConversationCategory, Is.Null);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest]
        public IEnumerator PortConversationAdapters_SkipPlaybackCannotRepeatTheAcceptedCommand()
        {
            yield return InstallTalkingHouse(8, false);
            yield return TalkTo(Listener(director.Snapshot).id);
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                yield return PortConversationRoutine(runner, "EnterSeasonConversationCategory", EpisodeDirector.ConnectCategoryCaption);
                ButtonWithCaption(EpisodeHud.SmallTalkCaption).onClick.Invoke(); yield return null;
                Assert.That(director.IsConversationExchangePlaying, Is.True);
                var before = director.Snapshot;
                var primary = File.ReadAllBytes(director.SavePath); var backup = PortConversationBackupBytes();
                yield return PortConversationRoutine(runner, "DrainSeasonConversationExchange", "skip", false, null, true);
                Assert.That(director.IsConversationExchangePlaying, Is.False);
                Assert.That(PortConversationReport<int>(runner, "conversationPlaybackControls"), Is.EqualTo(1));
                AssertPortConversationUnchanged(before, primary, backup);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        private static IEnumerator PortConversationGuardFailureProbe()
        {
            yield return null;
            throw new InvalidOperationException("Original nested guard probe failure");
        }

        private static IEnumerator PortConversationGuardPauseProbe()
        {
            yield return null; yield return null;
        }

        private static IEnumerator PortConversationGuardOwnerProbe(IEnumerator child, Action disposed, bool failCleanup = false)
        {
            try { yield return child; }
            finally
            {
                disposed();
                if (failCleanup) throw new InvalidOperationException("Nested guard probe cleanup failure");
            }
        }

        private static IEnumerator PortConversationMatrixOwnedFailureProbe(Action recordCleanup)
        {
            try
            {
                yield return null;
                throw new InvalidOperationException("Original matrix-owned probe failure");
            }
            finally { recordCleanup(); }
        }

        private IEnumerator AssertPortConversationGuardUnwindsNestedWork()
        {
            var type = typeof(PortVerification);
            var shotType = type.GetNestedType("LookShot", BindingFlags.NonPublic);
            var guardMethod = type.GetMethod("Guard", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (bool failCleanup in new[] { false, true })
            {
                var order = new List<string>();
                var shot = Activator.CreateInstance(shotType, true);
                shotType.GetField("reached").SetValue(shot, true);
                var inner = PortConversationGuardOwnerProbe(PortConversationGuardFailureProbe(),
                    () => order.Add("inner"), failCleanup);
                var outer = PortConversationGuardOwnerProbe(inner, () => order.Add("outer"));
                yield return (IEnumerator)guardMethod.Invoke(null, new object[] { outer, shot });
                CollectionAssert.AreEqual(new[] { "inner", "outer" }, order,
                    "A failed child must unwind every suspended parent in reverse ownership order.");
                Assert.That((bool)shotType.GetField("reached").GetValue(shot), Is.False);
                string reason = (string)shotType.GetField("reason").GetValue(shot);
                Assert.That(reason, Does.Contain("Original nested guard probe failure"));
                if (failCleanup) Assert.That(reason, Does.Contain("Nested guard probe cleanup failure"));
            }
            {
                var order = new List<string>();
                var shot = Activator.CreateInstance(shotType, true);
                shotType.GetField("reached").SetValue(shot, true);
                var inner = PortConversationGuardOwnerProbe(PortConversationGuardPauseProbe(), () => order.Add("inner"));
                var outer = PortConversationGuardOwnerProbe(inner, () => order.Add("outer"));
                var guarded = (IEnumerator)guardMethod.Invoke(null, new object[] { outer, shot });
                Assert.That(guarded.MoveNext(), Is.True, "Suspend the real guard with all nested owners active.");
                ((IDisposable)guarded).Dispose();
                CollectionAssert.AreEqual(new[] { "inner", "outer" }, order,
                    "Cancelling the guard must also dispose its suspended owners.");
                Assert.That((bool)shotType.GetField("reached").GetValue(shot), Is.False);
                Assert.That((string)shotType.GetField("reason").GetValue(shot), Does.Contain("cancelled"));
            }
        }

        [UnityTest]
        public IEnumerator PortConversationAdapters_UnknownCategoryFailsWithoutAutoNavigatingOrCommitting()
        {
            yield return InstallTalkingHouse(8, false);
            yield return TalkTo(Listener(director.Snapshot).id);
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                var before = director.Snapshot;
                var primary = File.ReadAllBytes(director.SavePath); var backup = PortConversationBackupBytes();
                var stack = new Stack<IEnumerator>();
                stack.Push(PortConversationRoutine(runner, "EnterSeasonConversationCategory", "Nonexistent topic"));
                Exception failure = null;
                for (int step = 0; stack.Count > 0 && step < 5000 && failure == null; step++)
                {
                    var top = stack.Peek();
                    bool moved = false;
                    try { moved = top.MoveNext(); }
                    catch (Exception error) { failure = error; }
                    if (failure != null) break;
                    if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (top.Current is IEnumerator child) stack.Push(child);
                    else yield return top.Current;
                }
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                Assert.That(failure, Is.TypeOf<InvalidOperationException>());
                Assert.That(failure.Message, Does.Contain("actual topic control is unavailable"));
                Assert.That(director.CurrentConversationCategory, Is.Null);
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.Zero);
                AssertPortConversationUnchanged(before, primary, backup);
                yield return AssertPortConversationGuardUnwindsNestedWork();
                var reportType = typeof(PortVerification).GetNestedType("LookSheetReport", BindingFlags.NonPublic);
                var report = Activator.CreateInstance(reportType, true);
                typeof(PortVerification).GetField("lookReport", PortAdapterPrivate).SetValue(runner, report);
                var shotType = typeof(PortVerification).GetNestedType("LookShot", BindingFlags.NonPublic);
                var shot = Activator.CreateInstance(shotType, true);
                shotType.GetField("reached").SetValue(shot, true);
                var recordRestore = typeof(PortVerification).GetMethod("RecordConversationRestoreFailure", PortAdapterPrivate);
                var ownedFailure = PortConversationMatrixOwnedFailureProbe(() =>
                    recordRestore.Invoke(runner, new object[] { "controlled unit probe",
                        new InvalidOperationException("Recorded cleanup probe failure") }));
                var guard = typeof(PortVerification).GetMethod("Guard", BindingFlags.Static | BindingFlags.NonPublic);
                yield return (IEnumerator)guard.Invoke(null, new object[] { ownedFailure, shot });
                Assert.That((string)shotType.GetField("reason").GetValue(shot),
                    Does.Contain("Original matrix-owned probe failure"));
                var restoreErrors = (IList)reportType.GetField("errors").GetValue(report);
                Assert.That(restoreErrors.Count, Is.EqualTo(1));
                Assert.That((string)restoreErrors[0], Does.Contain("Recorded cleanup probe failure"));
                AssertPortConversationUnchanged(before, primary, backup);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        // V-COV prospective additions. These never replace the ten preserved cases above.
        private sealed class PortRequiredLeafInterposition
        {
            public bool interposed;
            public Exception failure, cleanupFailure;
            public EpisodeState before;
            public string savePath, npcId;
            public byte[] primary, backup;
        }

        private void AssertPortRequiredOrdinaryOwner(EpisodeState state, string npcId)
        {
            Assert.That(director.IsChoosingSharedInformation, Is.False);
            Assert.That(EpisodeEngine.OpenStoryBeats(state).Any(item => item.surface == StorySurfaces.Conversation
                && (item.involvedIds.Contains(npcId) || item.cast.Any(role => role.contestantId == npcId))), Is.False,
                "A genuine story/share owner is a setup distinction, not a hidden-leaf regression.");
        }

        private IEnumerator PortInterposeRequiredLeaf(IEnumerator routine, string category, string caption,
            Action<EpisodeState, string> assertEligible, PortRequiredLeafInterposition result)
        {
            // Interpose ONLY after a complete child navigation returns to this actual production
            // parent. Never invoke an invisible leaf or add a production picker/authority hook.
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            UnityEngine.UI.Button hidden = null;
            bool returnedChild = false;
            try
            {
                for (int step = 0; stack.Count > 0 && step < 5000 && result.failure == null; step++)
                {
                    if (!result.interposed && returnedChild && ReferenceEquals(stack.Peek(), routine)
                        && director.IsConversationOpen && director.CurrentConversationCategory == category)
                    {
                        hidden = ButtonWithCaption(caption);
                        Assert.That(hidden.IsActive() && hidden.IsInteractable(), Is.True,
                            "The exact originally visible eligible leaf must exist before the test hides it.");
                        result.before = director.Snapshot; result.npcId = director.TalkingToId;
                        AssertPortRequiredOrdinaryOwner(result.before, result.npcId);
                        assertEligible(result.before, result.npcId);
                        result.savePath = director.SavePath;
                        result.primary = File.ReadAllBytes(result.savePath); result.backup = PortConversationBackupBytes();
                        hidden.gameObject.SetActive(false);
                        Assert.That(ButtonWithCaptionOrNull(caption), Is.Null);
                        AssertPortConversationUnchanged(result.before, result.primary, result.backup);
                        result.interposed = true;
                    }
                    returnedChild = false;
                    var top = stack.Peek(); bool moved = false;
                    try { moved = top.MoveNext(); }
                    catch (Exception error) { result.failure = error; }
                    if (result.failure != null) break;
                    if (!moved)
                    {
                        (stack.Pop() as IDisposable)?.Dispose();
                        returnedChild = stack.Count > 0;
                        continue;
                    }
                    if (top.Current is IEnumerator child) stack.Push(child);
                    else yield return top.Current;
                }
                Assert.That(stack.Count == 0 || result.failure != null, Is.True,
                    "The test-only owned coroutine traversal exceeded its fixed step bound.");
            }
            finally
            {
                while (stack.Count > 0)
                {
                    try { (stack.Pop() as IDisposable)?.Dispose(); }
                    catch (Exception error) { if (result.cleanupFailure == null) result.cleanupFailure = error; }
                }
                if (hidden != null) hidden.gameObject.SetActive(true);
            }
            Assert.That(result.cleanupFailure, Is.Null, "Every suspended test-owned iterator must unwind.");
        }

        private void AssertPortRequiredMissingLeaf(PortVerification runner, PortRequiredLeafInterposition result, string feature, string caption)
        {
            Assert.That(result.interposed, Is.True, "A setup/navigation miss is NOT the intended old-red evidence.");
            Assert.That(result.failure, Is.TypeOf<InvalidOperationException>(),
                "The existing helper must reject eligible missing UI, not quietly consume or fall back.");
            Assert.That(result.failure.Message, Does.Contain("Eligible conversation leaf is missing").And.Contain(feature).And.Contain(caption));
            Assert.That(director.SavePath, Is.EqualTo(result.savePath));
            AssertPortConversationUnchanged(result.before, result.primary, result.backup);
        }

        private static EpisodeState PortRequiredNearOathFixture()
        {
            for (uint seed = 6501; seed < 6533; seed++)
            {
                var state = PortConversationWarmPrehistory(seed, 7);
                double score = state.Score(state.playerId, ContentCatalog.MayaId);
                if (score >= 65 && score < 75 && state.Score(ContentCatalog.MayaId, state.playerId) >= 8
                    && !state.oathOpportunities.Contains(ContentCatalog.MayaId)
                    && !state.Allied(state.playerId, ContentCatalog.MayaId)
                    && EpisodeEngine.AllianceRefusal(state, ContentCatalog.MayaId) == null
                    && EpisodeEngine.SocialActionBudget(state) - EpisodeEngine.SocialActionsSpent(state) >= 2)
                    return state;
            }
            Assert.Fail("The existing finite command-earned cohort must provide the near-oath setup; never inject an opportunity.");
            return null;
        }

        private static EpisodeState PortRequiredOathAtZeroFixture()
        {
            var engine = new EpisodeEngine(PortRequiredNearOathFixture());
            var start = engine.Snapshot;
            int allowance = EpisodeEngine.SocialActionBudget(start) - EpisodeEngine.SocialActionsSpent(start);
            Assert.That(allowance, Is.InRange(2, EpisodeEngine.LegacySocialActionBudget + WebSocialVocabulary.PurchaseCeiling));
            for (int paid = 0; paid < allowance && !engine.Snapshot.oathOpportunities.Contains(ContentCatalog.MayaId); paid++)
                PortConversationPrehistoryCommand(engine, EpisodeCommandKind.RelationshipBuilding, ContentCatalog.MayaId);
            Assert.That(engine.Snapshot.oathOpportunities, Does.Contain(ContentCatalog.MayaId),
                "The actual relationship milestone must earn the opportunity through accepted commands.");
            string other = start.Active.First(person => !person.isPlayer && person.id != ContentCatalog.MayaId).id;
            int remaining = EpisodeEngine.SocialActionBudget(engine.Snapshot) - EpisodeEngine.SocialActionsSpent(engine.Snapshot);
            for (int paid = 0; paid < remaining; paid++)
            {
                var before = engine.Snapshot;
                var result = PortConversationPrehistoryCommand(engine, EpisodeCommandKind.SmallTalk, other);
                Assert.That(EpisodeEngine.SocialActionBudget(result.state), Is.EqualTo(EpisodeEngine.SocialActionBudget(before)));
                Assert.That(EpisodeEngine.SocialActionsSpent(result.state), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before) + 1));
            }
            var zero = engine.Snapshot;
            Assert.That(EpisodeEngine.SocialActionsSpent(zero), Is.EqualTo(EpisodeEngine.SocialActionBudget(zero)));
            Assert.That(zero.oathOpportunities, Does.Contain(ContentCatalog.MayaId));
            Assert.That(zero.loyaltyOaths.Any(o => o.playerId == zero.playerId && o.targetId == ContentCatalog.MayaId
                || o.targetId == zero.playerId && o.playerId == ContentCatalog.MayaId), Is.False);
            return zero;
        }

        private static EpisodeState PortRequiredNamedVoteFixture(out string voterId, out string subjectId, out string kind)
        {
            // New test-only, bounded fixture qualification. Actual ordinary SeasonBuilder input,
            // actual NextCommand prehistory and one detached source proposal select a positive UI
            // case. This is NOT an independent numerical oracle or a shipping outcome policy.
            for (uint seed = 4401; seed < 4417; seed++)
            {
                var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                // The actual shipping entry policy runs once, before any season command or engine.
                ShippedRules.ApplyFresh(fresh);
                Assert.That(EpisodeValidation.TryValidate(fresh, out var freshError), Is.True, freshError);
                var engine = new EpisodeEngine(fresh);
                Assert.That(engine.Snapshot.unifiedCommitmentRulesVersion, Is.EqualTo(UnifiedVoteFamilyValidation.Version));
                for (int step = 0; step < 32 && engine.Snapshot.phase != EpisodePhase.Campaign
                    && engine.Snapshot.phase != EpisodePhase.Finished
                    && engine.Snapshot.Find(engine.Snapshot.playerId).status == ContestantStatus.Active; step++)
                {
                    var result = engine.Apply(NextCommand(engine.Snapshot));
                    Assert.That(result.accepted, Is.True, "Actual normal first-Campaign prehistory: " + result.reason);
                }
                var state = engine.Snapshot;
                if (state.phase != EpisodePhase.Campaign || state.Find(state.playerId).status != ContestantStatus.Active
                    || !EpisodeEngine.LeverRulesOn(state)
                    || EpisodeEngine.SocialActionsSpent(state) >= EpisodeEngine.SocialActionBudget(state)) continue;
                string selectedVoter = null, selectedSubject = null, selectedKind = null;
                foreach (var voter in EpisodeEngine.Voters(state).Where(v => !v.isPlayer))
                {
                    foreach (string candidateKind in new[] { DealKind.VoteEvict, DealKind.VoteSave })
                    {
                        selectedSubject = state.nominees.FirstOrDefault(id => id != state.playerId
                            && PlayerDeals.CanPropose(state, voter.id, candidateKind, id, out _));
                        if (selectedSubject != null) { selectedVoter = voter.id; selectedKind = candidateKind; break; }
                    }
                    if (selectedVoter != null) break;
                }
                if (selectedVoter == null || EpisodeEngine.OpenStoryBeats(state).Any(item => item.surface == StorySurfaces.Conversation
                    && (item.involvedIds.Contains(selectedVoter) || item.cast.Any(role => role.contestantId == selectedVoter)))) continue;
                var probe = new EpisodeEngine(state).Apply(new EpisodeCommand
                {
                    id = "port-required-named-fixture-probe-" + seed, actorId = state.playerId,
                    expectedRevision = state.revision, expectedPhase = state.phase, kind = EpisodeCommandKind.ProposeDeal,
                    targetId = selectedVoter, secondTargetId = selectedSubject, text = selectedKind,
                });
                Assert.That(probe.accepted, Is.True, probe.reason);
                if (!probe.state.unifiedCommitments.Any(row => !state.unifiedCommitments.Any(old => old.id == row.id)
                    && row.origin == UnifiedCommitments.PlayerDeal && row.sourcePolicy == UnifiedCommitments.DealPolicy
                    && row.kind == UnifiedVoteTogether.Vote && row.subtype == selectedKind && row.status == DealStatus.Active
                    && row.makerId == state.playerId && row.beneficiaryId == selectedVoter && row.targetId == selectedSubject)) continue;
                voterId = selectedVoter; subjectId = selectedSubject; kind = selectedKind;
                return state; // untouched pre-proposal source, not the probe's result or RNG.
            }
            Assert.Fail("The declared16-seed/32-command fixture cohort must produce a real accepted canonical proposal; never extend, fabricate or skip.");
            voterId = subjectId = kind = null; return null;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_RequiredAskVoteLeafFailsBeforeConsumingEligibleMissing() =>
            PortRequiredReadMissing(false);

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_RequiredReadPersonLeafFailsBeforeConsumingEligibleMissing() =>
            PortRequiredReadMissing(true);

        private IEnumerator PortRequiredReadMissing(bool reading)
        {
            yield return InstallTalkingHouse(8, true, state => EpisodeEngine.EnableRead(state));
            player.Agent.speed = 25f; player.Agent.acceleration = 100f;
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                if (reading) yield return PortConversationRoutine(runner, "ExerciseSeasonRead", director.Snapshot, false);
                int commands = PortConversationReport<int>(runner, "commands");
                string flag = reading ? "seasonPersonRead" : "seasonVoteAsked";
                Assert.That((bool)typeof(PortVerification).GetField(flag, PortAdapterPrivate).GetValue(runner), Is.False);
                var interposition = new PortRequiredLeafInterposition();
                string caption = reading ? EpisodeHud.ReadPersonCaption : EpisodeHud.AskVoteCaption;
                yield return PortInterposeRequiredLeaf(PortConversationRoutine(runner, "ExerciseSeasonRead", director.Snapshot, false),
                    EpisodeDirector.LearnCategoryCaption, caption, (state, npc) =>
                    {
                        Assert.That(state.Find(state.playerId).status, Is.EqualTo(ContestantStatus.Active));
                        Assert.That(state.phase, Is.EqualTo(EpisodePhase.Campaign));
                        Assert.That(EpisodeEngine.Voters(state).Any(v => v.id == npc), Is.True);
                        Assert.That(reading ? EpisodeEngine.ReadThisWeek(state, npc) : EpisodeEngine.AskedThisWeek(state, npc), Is.False);
                        if (!reading) Assert.That(VoteRead.Available(state), Is.True);
                    }, interposition);
                AssertPortRequiredMissingLeaf(runner, interposition, reading ? "ReadPerson" : "AskVote", caption);
                Assert.That((bool)typeof(PortVerification).GetField(flag, PortAdapterPrivate).GetValue(runner), Is.False,
                    "An eligible-missing attempt must NOT be consumed.");
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.EqualTo(commands));
                Assert.That(PortConversationReport<int>(runner, reading ? "peopleRead" : "votesAsked"), Is.Zero);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_RequiredReadAlreadyAskedIsFiniteIneligible()
        {
            yield return InstallTalkingHouse(8, true, state => EpisodeEngine.EnableRead(state));
            var engine = new EpisodeEngine(director.Snapshot);
            var voters = EpisodeEngine.Voters(engine.Snapshot).Where(v => !v.isPlayer).Select(v => v.id).ToArray();
            Assert.That(voters, Is.Not.Empty);
            foreach (string voter in voters) PortConversationPrehistoryCommand(engine, EpisodeCommandKind.AskVote, voter);
            yield return InstallPortConversationState(engine.Snapshot);
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                var before = director.Snapshot;
                var primary = File.ReadAllBytes(director.SavePath); var backup = PortConversationBackupBytes();
                Assert.That(voters.All(id => EpisodeEngine.AskedThisWeek(before, id)), Is.True);
                yield return PortConversationRoutine(runner, "ExerciseSeasonRead", before, false);
                Assert.That((bool)typeof(PortVerification).GetField("seasonVoteAsked", PortAdapterPrivate).GetValue(runner), Is.True,
                    "A legitimate terminal ineligibility is finite scheduling, not successful coverage.");
                Assert.That(PortConversationReport<int>(runner, "votesAsked"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.Zero);
                Assert.That(PortConversationReport<string>(runner, "readNote"), Does.StartWith("Ineligible:"));
                AssertPortConversationUnchanged(before, primary, backup);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_RequiredOathMissingDeclarationAtZeroApFails()
        {
            yield return InstallPortConversationState(PortRequiredOathAtZeroFixture());
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                var interposition = new PortRequiredLeafInterposition();
                yield return PortInterposeRequiredLeaf(PortConversationRoutine(runner, "ExerciseOptionalOath", false),
                    EpisodeDirector.DealsCategoryCaption, EpisodeHud.OathDeclareCaption, (state, npc) =>
                    {
                        Assert.That(npc, Is.EqualTo(ContentCatalog.MayaId));
                        Assert.That(state.oathOpportunities, Does.Contain(npc));
                        Assert.That(state.Find(npc).status, Is.EqualTo(ContestantStatus.Active));
                        Assert.That(EpisodeEngine.SocialActionsSpent(state), Is.EqualTo(EpisodeEngine.SocialActionBudget(state)));
                    }, interposition);
                AssertPortRequiredMissingLeaf(runner, interposition, "Oath", EpisodeHud.OathDeclareCaption);
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "optionalSocialCommands"), Is.Zero);
                Assert.That(PortConversationReport<string>(runner, "oathOutcome"), Is.EqualTo("Not reached"));
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_RequiredOathSetupMissingAllianceFails()
        {
            yield return InstallPortConversationState(PortRequiredNearOathFixture());
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                var interposition = new PortRequiredLeafInterposition();
                yield return PortInterposeRequiredLeaf(PortConversationRoutine(runner, "ExerciseOptionalOath", false),
                    EpisodeDirector.DealsCategoryCaption, "Propose an alliance", (state, npc) =>
                    {
                        Assert.That(npc, Is.EqualTo(ContentCatalog.MayaId));
                        Assert.That(state.oathOpportunities, Does.Not.Contain(npc));
                        Assert.That(state.Allied(state.playerId, npc), Is.False);
                        Assert.That(state.Score(npc, state.playerId), Is.GreaterThanOrEqualTo(8));
                        Assert.That(EpisodeEngine.AllianceRefusal(state, npc), Is.Null);
                        Assert.That(EpisodeEngine.SocialActionsSpent(state), Is.LessThan(EpisodeEngine.SocialActionBudget(state)));
                    }, interposition);
                AssertPortRequiredMissingLeaf(runner, interposition, "OathSetup", "Propose an alliance");
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "optionalSocialCommands"), Is.Zero,
                    "Another visible Talk verb may not hide the disappeared eligible alliance.");
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_RequiredOathDeclaresAtLegallyExhaustedAp()
        {
            yield return InstallPortConversationState(PortRequiredOathAtZeroFixture());
            yield return TalkTo(ContentCatalog.MayaId);
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                yield return PortConversationRoutine(runner, "EnterSeasonConversationCategory", EpisodeDirector.DealsCategoryCaption);
                var stale = ButtonWithCaption(EpisodeHud.OathDeclareCaption).onClick;
                var before = director.Snapshot; var primary = File.ReadAllBytes(director.SavePath);
                yield return PortConversationRoutine(runner, "ExerciseOptionalOath", false);
                var after = director.Snapshot;
                AssertIntentCommand(before, EpisodeCommandKind.SwearLoyalty, ContentCatalog.MayaId);
                Assert.That(after.revision, Is.EqualTo(before.revision + 1));
                Assert.That(after.acceptedCommandIds.Count, Is.EqualTo(before.acceptedCommandIds.Count + 1));
                Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before)));
                Assert.That(EpisodeEngine.SocialActionBudget(after), Is.EqualTo(EpisodeEngine.SocialActionBudget(before)));
                Assert.That(after.oathOpportunities, Does.Not.Contain(ContentCatalog.MayaId));
                Assert.That(after.loyaltyOaths.Count(o => o.playerId == before.playerId && o.targetId == ContentCatalog.MayaId), Is.EqualTo(1));
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.EqualTo(1));
                Assert.That(PortConversationReport<int>(runner, "optionalSocialCommands"), Is.Zero);
                Assert.That(PortConversationReport<string>(runner, "oathOutcome"), Is.EqualTo("Legally earned and declared"));
                CollectionAssert.AreEqual(primary, File.ReadAllBytes(new EpisodeSaveStore(director.SavePath).BackupPath));
                var saved = File.ReadAllBytes(director.SavePath); var backup = PortConversationBackupBytes();
                stale.Invoke(); yield return null;
                yield return PortConversationRoutine(runner, "DrainSeasonConversationExchange", "required-oath-stale", false, null, true);
                AssertPortConversationUnchanged(after, saved, backup);
                director.LoadNow(); HoldTheHouseForTheFixture(); yield return null;
                AssertPortConversationUnchanged(after, saved, backup);
                AssertIntentDurable(after);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_CampaignNamedVoteCommitsCanonicalReceiptAndRetiresCallback()
        {
            var fixture = PortRequiredNamedVoteFixture(out string voter, out string subject, out string kind);
            yield return InstallPortConversationState(fixture);
            Assert.That(SceneComponents<HouseNpc>().Any(n => n.Id == voter && n.gameObject.activeInHierarchy), Is.True,
                "The canonical first candidate needs an actual native body; a setup miss is not coverage.");
            yield return TalkTo(voter);
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                Assert.That(director.Snapshot.unifiedCommitmentRulesVersion, Is.EqualTo(UnifiedVoteFamilyValidation.Version));
                Assert.That(subject, Is.Not.EqualTo(fixture.playerId));
                Assert.That(PlayerDeals.CanPropose(director.Snapshot, voter, kind, subject, out var reason), Is.True, reason);
                yield return PortConversationRoutine(runner, "EnterSeasonConversationCategory", EpisodeDirector.DealsCategoryCaption);
                string caption = EpisodeHud.VoteDealCaption(kind, fixture.Find(subject).name);
                var stale = ButtonWithCaption(caption).onClick;
                var before = director.Snapshot;
                var primary = File.ReadAllBytes(director.SavePath); var backupBefore = PortConversationBackupBytes();
                yield return PortConversationRoutine(runner, "EnterSeasonConversationCategory", EpisodeDirector.ConnectCategoryCaption);
                AssertPortConversationUnchanged(before, primary, backupBefore);
                yield return PortConversationRoutine(runner, "ExerciseSeasonNamedVoteDeal", false);
                var after = director.Snapshot;
                AssertIntentCommand(before, EpisodeCommandKind.ProposeDeal, voter, kind, subject);
                Assert.That(after.revision, Is.EqualTo(before.revision + 1));
                Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before) + 1));
                Assert.That(EpisodeEngine.SocialActionBudget(after), Is.EqualTo(EpisodeEngine.SocialActionBudget(before)));
                string receiptId = PortConversationReport<string>(runner, "namedVoteReceiptId");
                Assert.That(receiptId, Is.Not.Null.And.Not.Empty, "This strict positive must actually strike a canonical agreement, not merely emit an event.");
                var receipt = after.unifiedCommitments.Single(row => row.id == receiptId);
                Assert.That(receipt.kind, Is.EqualTo(UnifiedVoteTogether.Vote));
                Assert.That(receipt.sourcePolicy, Is.EqualTo(UnifiedCommitments.DealPolicy));
                Assert.That(receipt.origin, Is.EqualTo(UnifiedCommitments.PlayerDeal));
                Assert.That(receipt.subtype, Is.EqualTo(kind));
                Assert.That(receipt.makerId, Is.EqualTo(before.playerId));
                Assert.That(receipt.beneficiaryId, Is.EqualTo(voter));
                Assert.That(receipt.targetId, Is.EqualTo(subject));
                Assert.That(receipt.status, Is.EqualTo(DealStatus.Active));
                Assert.That(receipt.createdWeek, Is.EqualTo(before.week));
                Assert.That(receipt.expiresWeek, Is.EqualTo(before.week));
                Assert.That(receipt.voteBindingWeek, Is.EqualTo(before.week));
                Assert.That(receipt.voteFirstRevealWeek, Is.EqualTo(before.week));
                Assert.That(receipt.reciprocal, Is.True);
                Assert.That(before.unifiedCommitments.Any(row => row.id == receiptId), Is.False);
                Assert.That(after.deals.Any(row => row.id == receiptId), Is.False, "Mode2 never writes a raw mirror.");
                var projection = PortVerification.SeasonDeal(after, receiptId);
                Assert.That(projection, Is.Not.Null);
                Assert.That(projection.id, Is.EqualTo(receipt.id));
                Assert.That(projection.proposerId, Is.EqualTo(receipt.makerId));
                Assert.That(projection.recipientId, Is.EqualTo(receipt.beneficiaryId));
                Assert.That(projection.targetId, Is.EqualTo(receipt.targetId));
                Assert.That(projection.type, Is.EqualTo(receipt.subtype));
                Assert.That(projection.status, Is.EqualTo(receipt.status));
                Assert.That(PortConversationReport<int>(runner, "namedVoteProposals"), Is.EqualTo(1));
                Assert.That(PortConversationReport<int>(runner, "namedVoteReceipts"), Is.EqualTo(1));
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.EqualTo(1));
                Assert.That(PortConversationReport<string>(runner, "namedVoteDisposition"), Is.EqualTo("Committed"));
                Assert.That(PortConversationReport<string>(runner, "namedVoteNpcId"), Is.EqualTo(voter));
                Assert.That(PortConversationReport<string>(runner, "namedVoteSubjectId"), Is.EqualTo(subject));
                Assert.That(PortConversationReport<string>(runner, "namedVoteKind"), Is.EqualTo(kind));
                CollectionAssert.AreEqual(primary, File.ReadAllBytes(new EpisodeSaveStore(director.SavePath).BackupPath));
                var saved = File.ReadAllBytes(director.SavePath); var backup = PortConversationBackupBytes();
                // Used-attempt is bounded; repeat invocation and stale callback must both be inert.
                yield return PortConversationRoutine(runner, "ExerciseSeasonNamedVoteDeal", false);
                stale.Invoke(); yield return null;
                yield return PortConversationRoutine(runner, "DrainSeasonConversationExchange", "required-named-stale", false, null, true);
                AssertPortConversationUnchanged(after, saved, backup);
                director.LoadNow(); HoldTheHouseForTheFixture(); yield return null;
                AssertPortConversationUnchanged(after, saved, backup);
                AssertIntentDurable(after);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_CampaignNamedVoteMissingSelectedLeafFails()
        {
            var fixture = PortRequiredNamedVoteFixture(out string voter, out string subject, out string kind);
            yield return InstallPortConversationState(fixture);
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                string caption = EpisodeHud.VoteDealCaption(kind, fixture.Find(subject).name);
                var interposition = new PortRequiredLeafInterposition();
                yield return PortInterposeRequiredLeaf(PortConversationRoutine(runner, "ExerciseSeasonNamedVoteDeal", false),
                    EpisodeDirector.DealsCategoryCaption, caption, (state, npc) =>
                    {
                        Assert.That(npc, Is.EqualTo(voter));
                        Assert.That(state.unifiedCommitmentRulesVersion, Is.EqualTo(UnifiedVoteFamilyValidation.Version));
                        Assert.That(subject, Is.Not.EqualTo(state.playerId));
                        Assert.That(EpisodeEngine.Voters(state).Any(v => v.id == npc), Is.True);
                        Assert.That(PlayerDeals.CanPropose(state, npc, kind, subject, out var reason), Is.True, reason);
                        Assert.That(EpisodeEngine.SocialActionsSpent(state), Is.LessThan(EpisodeEngine.SocialActionBudget(state)));
                        Assert.That(ButtonWithCaptionOrNull("Promise safety"), Is.Not.Null,
                            "Another ordinary verb cannot rescue the missing selected named leaf.");
                    }, interposition);
                AssertPortRequiredMissingLeaf(runner, interposition, "NamedVote", caption);
                Assert.That((bool)typeof(PortVerification).GetField("seasonNamedVoteAttempted", PortAdapterPrivate).GetValue(runner), Is.False);
                Assert.That(PortConversationReport<int>(runner, "namedVoteProposals"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "namedVoteReceipts"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.Zero);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_CampaignNamedVoteAtZeroApIsFiniteIneligible()
        {
            var source = PortRequiredNamedVoteFixture(out _, out _, out _);
            var engine = new EpisodeEngine(source);
            string other = source.Active.First(person => !person.isPlayer).id;
            int remaining = EpisodeEngine.SocialActionBudget(source) - EpisodeEngine.SocialActionsSpent(source);
            Assert.That(remaining, Is.GreaterThan(0));
            for (int paid = 0; paid < remaining; paid++) PortConversationPrehistoryCommand(engine, EpisodeCommandKind.SmallTalk, other);
            yield return InstallPortConversationState(engine.Snapshot);
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                var before = director.Snapshot; var primary = File.ReadAllBytes(director.SavePath); var backup = PortConversationBackupBytes();
                Assert.That(EpisodeEngine.SocialActionsSpent(before), Is.EqualTo(EpisodeEngine.SocialActionBudget(before)));
                yield return PortConversationRoutine(runner, "ExerciseSeasonNamedVoteDeal", false);
                yield return PortConversationRoutine(runner, "ExerciseSeasonNamedVoteDeal", false);
                Assert.That((bool)typeof(PortVerification).GetField("seasonNamedVoteAttempted", PortAdapterPrivate).GetValue(runner), Is.True);
                Assert.That(PortConversationReport<string>(runner, "namedVoteDisposition"), Is.EqualTo("Ineligible"));
                Assert.That(PortConversationReport<int>(runner, "namedVoteProposals"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "namedVoteReceipts"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.Zero);
                AssertPortConversationUnchanged(before, primary, backup);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_KnownInformationOwnerBlocksWithoutAnsweringOrClaimingLeaves()
        {
            var fixture = PortRequiredNamedVoteFixture(out string voter, out _, out _);
            Assert.That(fixture.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(EpisodeValidation.TryValidate(fixture, out var fixtureError), Is.True, fixtureError);
            yield return InstallPortConversationState(fixture);
            yield return TalkTo(voter);
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                yield return PortConversationRoutine(runner, "EnterSeasonConversationCategory", EpisodeDirector.LearnCategoryCaption);
                var before = director.Snapshot;
                Assert.That(before.unifiedCommitmentRulesVersion, Is.EqualTo(UnifiedVoteFamilyValidation.Version));
                Assert.That(InformationShareChoice.Available(before, voter), Is.True,
                    "Open the real legal chooser; no manufactured owner/picker flag.");
                var primary = File.ReadAllBytes(director.SavePath); var backup = PortConversationBackupBytes();
                ButtonWithCaption(EpisodeDirector.ShareInformationCaption).onClick.Invoke();
                yield return null; yield return null;
                Assert.That(director.IsChoosingSharedInformation, Is.True);
                Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.ShareInformationCancelCaption), Is.Not.Null);
                AssertPortConversationUnchanged(before, primary, backup);
                yield return PortConversationRoutine(runner, "ExerciseSeasonRead", before, false);
                yield return PortConversationRoutine(runner, "ExerciseOptionalOath", false);
                yield return PortConversationRoutine(runner, "ExerciseSeasonNamedVoteDeal", false);
                Assert.That(director.IsChoosingSharedInformation, Is.True, "The companions must not dismiss or answer this legitimate owner.");
                Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.ShareInformationCancelCaption), Is.Not.Null);
                Assert.That(PortConversationReport<string>(runner, "readNote"), Does.StartWith("BlockedByOtherView:"));
                Assert.That(PortConversationReport<string>(runner, "oathNote"), Does.StartWith("BlockedByOtherView:"));
                Assert.That(PortConversationReport<string>(runner, "namedVoteDisposition"), Is.EqualTo("BlockedByOtherView"));
                Assert.That(PortConversationReport<int>(runner, "votesAsked"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "peopleRead"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "optionalSocialCommands"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "namedVoteProposals"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "namedVoteReceipts"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.Zero);
                var attempts = PortConversationReport<IList>(runner, "conversationAttempts");
                Assert.That(attempts.Count, Is.EqualTo(3));
                foreach (object attempt in attempts)
                    Assert.That((string)attempt.GetType().GetField("disposition").GetValue(attempt), Is.EqualTo("BlockedByOtherView"));
                AssertPortConversationUnchanged(before, primary, backup);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        // BEGIN GENERIC_PENDING_RESIDUAL01 — strictly additive; remove this block to recover all prior twenty.
        private sealed class PortResidualInterposition
        {
            public EpisodeState before;
            public string path, caption;
            public byte[] primary, backup;
            public bool interposed;
            public Exception failure, cleanupFailure;
        }

        private static string PortResidualFirstGeneric(EpisodeState state, string npc) =>
            PlayerDeals.Available(state, npc).FirstOrDefault(kind => kind != DealKind.TargetAgreement
                && !(EpisodeEngine.LeverRulesOn(state) && (kind == DealKind.VoteSave || kind == DealKind.VoteEvict)));

        private static EpisodeState PortResidualSpendToZero(EpisodeState source)
        {
            var engine = new EpisodeEngine(source);
            string other = source.Active.First(person => !person.isPlayer && person.id != ContentCatalog.MayaId).id;
            int remaining = EpisodeEngine.SocialActionBudget(source) - EpisodeEngine.SocialActionsSpent(source);
            Assert.That(remaining, Is.InRange(0, EpisodeEngine.LegacySocialActionBudget + WebSocialVocabulary.PurchaseCeiling));
            for (int paid = 0; paid < remaining; paid++)
            {
                var before = engine.Snapshot;
                var result = PortConversationPrehistoryCommand(engine, EpisodeCommandKind.SmallTalk, other);
                Assert.That(EpisodeEngine.SocialActionsSpent(result.state), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before) + 1));
                Assert.That(EpisodeEngine.SocialActionBudget(result.state), Is.EqualTo(EpisodeEngine.SocialActionBudget(before)));
                CollectionAssert.AreEqual(before.acceptedCommandIds, result.state.acceptedCommandIds.Take(before.acceptedCommandIds.Count).ToArray());
            }
            var zero = engine.Snapshot;
            Assert.That(EpisodeEngine.SocialActionsSpent(zero), Is.EqualTo(EpisodeEngine.SocialActionBudget(zero)));
            return zero;
        }

        private static EpisodeState PortResidualGeneratedOfferAtZero(out string offerId)
        {
            // Exactly the AP03 cohort, earned warmth, paid commands and real generator. No rewinds,
            // hand-written offers, AP/stat/role changes or expansion after finite absence.
            for (uint seed = 6401; seed < 6433; seed++)
            {
                var candidate = PortResidualSpendToZero(PortConversationWarmPrehistory(seed, 7));
                NpcDeals.Propose(candidate);
                var selected = NpcDeals.Pending(candidate).FirstOrDefault(row => row.proposerId == ContentCatalog.MayaId);
                if (selected == null) continue;
                Assert.That(EpisodeValidation.TryValidate(candidate, out var why), Is.True, why);
                Assert.That(EpisodeEngine.InvitationPastPactCap(candidate, selected.proposerId), Is.False);
                Assert.That(selected.type == DealKind.VetoUse ? EpisodeDirector.VetoPromisedTo(candidate, selected.proposerId) : null, Is.Null);
                offerId = selected.id; return candidate;
            }
            Assert.Fail("The unchanged32-source AP03 cohort must yield an actual free eligible offer at legally exhausted AP.");
            offerId = null; return null;
        }

        private IEnumerator PortResidualInterpose(IEnumerator routine, Func<EpisodeState, string, string> chooseCaption,
            Action<EpisodeState, string, UnityEngine.UI.Button> prove, string change, PortResidualInterposition result,
            Action<UnityEngine.UI.Button> extra = null)
        {
            var owned = new Stack<IEnumerator>(); owned.Push(routine);
            UnityEngine.UI.Button control = null;
            UnityEngine.UI.Button.ButtonClickedEvent originalClick = null;
            bool active = false, enabled = false, returned = false;
            try
            {
                for (int step = 0; owned.Count > 0 && result.failure == null && step < 5000; step++)
                {
                    if (!result.interposed && returned && ReferenceEquals(owned.Peek(), routine)
                        && director.IsConversationOpen && director.CurrentConversationCategory == EpisodeDirector.DealsCategoryCaption)
                    {
                        result.before = director.Snapshot; result.path = director.SavePath;
                        string npc = director.TalkingToId;
                        AssertPortRequiredOrdinaryOwner(result.before, npc);
                        result.caption = chooseCaption(result.before, npc);
                        var rows = SceneComponents<UnityEngine.UI.Button>().Where(button => button.IsActive()
                            && button.gameObject.activeInHierarchy && button.GetComponentsInChildren<TMPro.TMP_Text>()
                                .Any(label => label.text == result.caption)).ToArray();
                        Assert.That(rows.Length, Is.EqualTo(1), "The originally rendered exact row is a setup proof, not missing-UI evidence.");
                        control = rows[0]; prove(result.before, npc, control);
                        result.primary = File.ReadAllBytes(result.path); result.backup = PortConversationBackupBytes();
                        active = control.gameObject.activeSelf; enabled = control.interactable; originalClick = control.onClick;
                        if (change == "hide") control.gameObject.SetActive(false);
                        else if (change == "disable") control.interactable = false;
                        else if (change == "noop") control.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                        else Assert.That(change, Is.EqualTo("metadata"));
                        extra?.Invoke(control);
                        AssertPortConversationUnchanged(result.before, result.primary, result.backup);
                        result.interposed = true;
                    }
                    returned = false;
                    var top = owned.Peek(); bool moved = false;
                    try { moved = top.MoveNext(); } catch (Exception error) { result.failure = error; }
                    if (result.failure != null) break;
                    if (!moved) { (owned.Pop() as IDisposable)?.Dispose(); returned = owned.Count > 0; continue; }
                    if (top.Current is IEnumerator child) owned.Push(child); else yield return top.Current;
                }
                Assert.That(owned.Count == 0 || result.failure != null, Is.True, "Bounded5000-step owned traversal exhausted.");
            }
            finally
            {
                while (owned.Count > 0)
                    try { (owned.Pop() as IDisposable)?.Dispose(); }
                    catch (Exception error) { if (result.cleanupFailure == null) result.cleanupFailure = error; }
                if (control != null)
                {
                    control.onClick = originalClick; control.interactable = enabled; control.gameObject.SetActive(active);
                }
            }
            Assert.That(result.cleanupFailure, Is.Null, "Every suspended owned iterator must unwind.");
        }

        private void AssertPortResidualFailure(PortVerification runner, PortResidualInterposition result, string fragment)
        {
            Assert.That(result.interposed, Is.True, "Missing source/fixture/navigation is not a meaningful old-red.");
            Assert.That(result.failure, Is.TypeOf<InvalidOperationException>());
            Assert.That(result.failure.Message, Does.Contain(fragment));
            Assert.That(director.SavePath, Is.EqualTo(result.path));
            AssertPortConversationUnchanged(result.before, result.primary, result.backup);
            Assert.That(PortConversationReport<int>(runner, "commands"), Is.Zero);
            Assert.That(PortConversationReport<int>(runner, "dealsAnswered"), Is.Zero);
            Assert.That(PortConversationReport<int>(runner, "dealsProposed"), Is.Zero);
            if (fragment == "Eligible conversation leaf is missing")
            {
                var attempts = PortConversationReport<IList>(runner, "conversationAttempts");
                Assert.That(attempts.Cast<object>().Any(row => (string)row.GetType().GetField("disposition").GetValue(row)
                    == "EligibleMissing"), Is.True, "The missing eligible child is explicit failure coverage, not unavailability.");
            }
        }

        private static void PortResidualProveFree(EpisodeState state, string npc, UnityEngine.UI.Button control)
        {
            var offer = NpcDeals.Pending(state).First(row => row.proposerId == npc);
            Assert.That(offer.proposerId, Is.EqualTo(ContentCatalog.MayaId));
            Assert.That(offer.recipientId, Is.EqualTo(state.playerId));
            Assert.That(offer.status, Is.EqualTo(DealStatus.Proposed));
            Assert.That(EpisodeEngine.SocialActionsSpent(state), Is.EqualTo(EpisodeEngine.SocialActionBudget(state)));
            Assert.That(EpisodeEngine.InvitationPastPactCap(state, npc), Is.False);
            Assert.That(offer.type == DealKind.VetoUse ? EpisodeDirector.VetoPromisedTo(state, npc) : null, Is.Null);
            Assert.That(control.IsInteractable(), Is.True);
        }

        private static void PortResidualProveGeneric(EpisodeState state, string npc, UnityEngine.UI.Button control)
        {
            Assert.That(state.phase, Is.EqualTo(EpisodePhase.Social));
            Assert.That(EpisodeEngine.SocialActionsSpent(state), Is.LessThan(EpisodeEngine.SocialActionBudget(state)));
            Assert.That(NpcDeals.Pending(state).Any(row => row.proposerId == npc), Is.False);
            string kind = PortResidualFirstGeneric(state, npc);
            Assert.That(kind, Is.Not.Null);
            Assert.That(PlayerDeals.CanPropose(state, npc, kind, null, out var why), Is.True, why);
            Assert.That(control.IsInteractable(), Is.True);
        }

        // These four old-helper negatives use NO future metadata API or missing helper.
        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_GenericCanonicalFirstHiddenCannotFallBack()
        {
            yield return InstallPortConversationState(ContentCatalog.Create(6301));
            var runner = BindPortConversationVerifier(out var owner);
            var result = new PortResidualInterposition();
            try
            {
                yield return PortResidualInterpose(PortConversationRoutine(runner, "ExerciseOptionalDeal", false),
                    (state, npc) => EpisodeHud.DealProposeCaption(DealKind.Title(PortResidualFirstGeneric(state, npc)).ToLowerInvariant()),
                    (state, npc, control) =>
                    {
                        PortResidualProveGeneric(state, npc, control);
                        Assert.That(PlayerDeals.Available(state, npc).Where(kind => kind != DealKind.TargetAgreement
                            && kind != PortResidualFirstGeneric(state, npc)).Any(kind =>
                            ButtonWithCaptionOrNull(EpisodeHud.DealProposeCaption(DealKind.Title(kind).ToLowerInvariant())) != null),
                            Is.True, "Another real generic control remains visible; the canonical selected child cannot fall back to it.");
                    }, "hide", result);
                AssertPortResidualFailure(runner, result, "Eligible conversation leaf is missing");
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_GenericCanonicalFirstDisabledCannotFallBack()
        {
            yield return InstallPortConversationState(ContentCatalog.Create(6301));
            var runner = BindPortConversationVerifier(out var owner);
            var result = new PortResidualInterposition();
            try
            {
                yield return PortResidualInterpose(PortConversationRoutine(runner, "ExerciseOptionalDeal", false),
                    (state, npc) => EpisodeHud.DealProposeCaption(DealKind.Title(PortResidualFirstGeneric(state, npc)).ToLowerInvariant()),
                    (state, npc, control) =>
                    {
                        PortResidualProveGeneric(state, npc, control);
                        Assert.That(PlayerDeals.Available(state, npc).Where(kind => kind != DealKind.TargetAgreement
                            && kind != PortResidualFirstGeneric(state, npc)).Any(kind =>
                            ButtonWithCaptionOrNull(EpisodeHud.DealProposeCaption(DealKind.Title(kind).ToLowerInvariant())) != null),
                            Is.True, "Another real generic control remains visible; the canonical selected child cannot fall back to it.");
                    }, "disable", result);
                AssertPortResidualFailure(runner, result, "Eligible conversation leaf is missing");
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_EligibleIncomingHiddenAtZeroApCannotSkip()
        {
            string offer; yield return InstallPortConversationState(PortResidualGeneratedOfferAtZero(out offer));
            var runner = BindPortConversationVerifier(out var owner); var result = new PortResidualInterposition();
            try
            {
                yield return PortResidualInterpose(PortConversationRoutine(runner, "ExerciseOptionalDeal", false),
                    (state, npc) => EpisodeHud.DealAcceptCaption, PortResidualProveFree, "hide", result);
                AssertPortResidualFailure(runner, result, "Eligible conversation leaf is missing");
                Assert.That(NpcDeals.Pending(director.Snapshot).Any(row => row.id == offer), Is.True);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_EligibleIncomingDisabledAtZeroApIsNotLegalLock()
        {
            string offer; yield return InstallPortConversationState(PortResidualGeneratedOfferAtZero(out offer));
            var runner = BindPortConversationVerifier(out var owner); var result = new PortResidualInterposition();
            try
            {
                yield return PortResidualInterpose(PortConversationRoutine(runner, "ExerciseOptionalDeal", false),
                    (state, npc) => EpisodeHud.DealAcceptCaption, PortResidualProveFree, "disable", result);
                AssertPortResidualFailure(runner, result, "Eligible conversation leaf is missing");
                Assert.That(NpcDeals.Pending(director.Snapshot).Any(row => row.id == offer), Is.True);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        private static EpisodeState PortResidualAuthoredPactCap()
        {
            // Labelled authored load-boundary pacts/offers, reusing the existing explicit fixture.
            // They are NOT a naturally earned pact history. AP is subsequently spent legally.
            var state = ContentCatalog.Create(6401);
            // Match InstallTalkingHouse's authored prerequisite, not the fresh-season policy.
            state.strategyRulesStartWeek = 1;
            AtThreePacts(state);
            Assert.That(StrategyRules.Apply(state), Is.True, "The authored pact-cap source requires the strategy windows.");
            Assert.That(EpisodeValidation.TryValidate(state, out var authoredError), Is.True, authoredError);
            Assert.That(EpisodeEngine.InvitationPastPactCap(state, ContentCatalog.MayaId), Is.True);
            return PortResidualSpendToZero(state);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_LawfullyLockedIncomingHiddenCannotSkip()
        {
            yield return InstallPortConversationState(PortResidualAuthoredPactCap());
            var runner = BindPortConversationVerifier(out var owner); var result = new PortResidualInterposition();
            try
            {
                yield return PortResidualInterpose(PortConversationRoutine(runner, "ExerciseOptionalDeal", false),
                    (state, npc) => EpisodeHud.DealAcceptCaption,
                    (state, npc, control) =>
                    {
                        var offer = NpcDeals.Pending(state).First(row => row.proposerId == npc);
                        Assert.That(offer.type, Is.EqualTo(DealKind.AllianceInvite));
                        Assert.That(EpisodeEngine.InvitationPastPactCap(state, npc), Is.True);
                        Assert.That(control.IsInteractable(), Is.False);
                        Assert.That(ShownText(), Does.Contain(EpisodeDirector.PactCapOfferLine(state, npc)));
                        Assert.That(ButtonWithCaption(EpisodeHud.DealDeclineCaption).IsInteractable(), Is.True);
                        Assert.That(EpisodeEngine.SocialActionsSpent(state), Is.EqualTo(EpisodeEngine.SocialActionBudget(state)));
                    }, "hide", result);
                AssertPortResidualFailure(runner, result, "Eligible conversation leaf is missing");
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        // Post-adoption semantic-interface controls are deliberately NOT claimed meaningful old-red.
        private object PortResidualDescription(UnityEngine.UI.Button button)
        {
            var method = typeof(EpisodeHud).GetMethod("TryDescribeOfferControl", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "Post-adoption API readiness, not an old-negative outcome.");
            foreach (var hud in SceneComponents<EpisodeHud>())
            {
                var args = new object[] { button, null };
                if ((bool)method.Invoke(hud, args)) return args[1];
            }
            return null;
        }

        private T PortResidualFact<T>(object description, string property) =>
            (T)description.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public).GetValue(description);

        private UnityEngine.UI.Button PortResidualSemantic(string id, bool accept)
        {
            var rows = SceneComponents<UnityEngine.UI.Button>().Where(button =>
            {
                var row = PortResidualDescription(button);
                return row != null && PortResidualFact<string>(row, "OfferId") == id && PortResidualFact<bool>(row, "Accept") == accept;
            }).ToArray();
            Assert.That(rows.Length, Is.EqualTo(1), "One actual active current HUD control for the selected semantic identity.");
            return rows[0];
        }

        private void AssertPortResidualSemanticSource(UnityEngine.UI.Button button, EpisodeState state, DealState offer, bool accept)
        {
            var row = PortResidualDescription(button);
            Assert.That(row, Is.Not.Null);
            Assert.That(PortResidualFact<string>(row, "OfferId"), Is.EqualTo(offer.id));
            Assert.That(PortResidualFact<string>(row, "ProposerId"), Is.EqualTo(offer.proposerId));
            Assert.That(PortResidualFact<string>(row, "RecipientId"), Is.EqualTo(offer.recipientId));
            Assert.That(PortResidualFact<bool>(row, "Accept"), Is.EqualTo(accept));
            Assert.That(PortResidualFact<string>(row, "SessionId"), Is.EqualTo(state.sessionId));
            Assert.That(PortResidualFact<int>(row, "Revision"), Is.EqualTo(state.revision));
            Assert.That(PortResidualFact<EpisodePhase>(row, "Phase"), Is.EqualTo(state.phase));
            Assert.That(PortResidualFact<long>(row, "RenderGeneration"), Is.GreaterThan(0));
            CollectionAssert.AreEquivalent(new[] { "OfferId", "ProposerId", "RecipientId", "Accept", "SessionId", "Revision", "Phase", "RenderGeneration" },
                row.GetType().GetProperties().Select(property => property.Name).ToArray());
            Assert.That(row.GetType().GetProperties().All(property => property.SetMethod == null), Is.True,
                "Rendered facts have no mutable setters, delegate or command authority.");
            Assert.That(row.GetType().GetConstructors().Length, Is.Zero, "Public callers can read, not mint a rendered descriptor.");
            Assert.That(button.GetComponentsInChildren<TMPro.TMP_Text>().Any(label =>
                label.text == (accept ? EpisodeHud.DealAcceptCaption : EpisodeHud.DealDeclineCaption)), Is.True);
        }

        private static EpisodeState PortResidualAuthoredMultipleOffers()
        {
            // A deliberately authored noncanonical loaded-state counterexample, NOT generator history.
            var state = PortResidualSpendToZero(ContentCatalog.Create(6401));
            foreach (var kind in new[] { DealKind.Partnership, DealKind.InformationSharing })
                state.deals.Add(new DealState
                {
                    id = "residual-authored-load-" + kind, type = kind,
                    proposerId = ContentCatalog.MayaId, recipientId = state.playerId,
                    status = DealStatus.Proposed, week = state.week, expiresWeek = state.week,
                    trustImpact = DealKind.DefaultTrust(kind),
                });
            Assert.That(EpisodeValidation.TryValidate(state, out var why), Is.True, why);
            Assert.That(NpcDeals.Pending(state).Count(row => row.proposerId == ContentCatalog.MayaId), Is.EqualTo(2));
            return state;
        }

        private IEnumerator PortResidualAdmitAuthored(EpisodeState source)
        {
            Assert.That(EpisodeValidation.TryValidate(source, out var why), Is.True, why);
            var store = new EpisodeSaveStore(director.SavePath);
            HoldTheHouseForTheFixture(); store.Save(source);
            Assert.That(store.TryLoad(out var read, out why), Is.True, why);
            Assert.That(JsonUtility.ToJson(read), Is.EqualTo(JsonUtility.ToJson(source)),
                "Measure exact Store admission of the authored boundary before rendering it.");
            yield return ReloadEpisode(); HoldTheHouseForTheFixture(); yield return null;
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(JsonUtility.ToJson(source)));
            player.Agent.speed = 25f; player.Agent.acceleration = 100f;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_GenericCanonicalFirstCommitsOnePaidTupleDurably()
        {
            EpisodeState source = null;
            for (uint seed = 6301; seed < 6333 && source == null; seed++)
            {
                var candidate = ContentCatalog.Create(seed); string selected = PortResidualFirstGeneric(candidate, ContentCatalog.MayaId);
                var answer = PortConversationPrehistoryCommand(new EpisodeEngine(candidate),
                    EpisodeCommandKind.ProposeDeal, ContentCatalog.MayaId, selected);
                if (CommitmentReferences.Deals(answer.state).Any(row => row.proposerId == candidate.playerId
                    && row.recipientId == ContentCatalog.MayaId && row.type == selected && row.status == DealStatus.Active))
                    source = candidate; // untouched actual source, not the detached probe's mutated state/RNG.
            }
            Assert.That(source, Is.Not.Null, "The existing32-source cohort must yield an actual agreement, not a fabricated yes.");
            yield return InstallPortConversationState(source);
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                var before = director.Snapshot; string kind = PortResidualFirstGeneric(before, ContentCatalog.MayaId);
                string path = director.SavePath; var primary = File.ReadAllBytes(path);
                yield return PortConversationRoutine(runner, "ExerciseOptionalDeal", false);
                var after = director.Snapshot;
                AssertIntentCommand(before, EpisodeCommandKind.ProposeDeal, ContentCatalog.MayaId, kind);
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.EqualTo(1));
                Assert.That(PortConversationReport<int>(runner, "dealsProposed"), Is.EqualTo(1));
                Assert.That(PortConversationReport<string>(runner, "dealKind"), Is.EqualTo(kind));
                var newAgreements = CommitmentReferences.Deals(after).Where(row =>
                    !CommitmentReferences.Deals(before).Any(old => old.id == row.id) && row.proposerId == before.playerId
                    && row.recipientId == ContentCatalog.MayaId && row.type == kind).ToArray();
                Assert.That(newAgreements.Length, Is.EqualTo(1));
                Assert.That(newAgreements[0].status, Is.EqualTo(DealStatus.Active));
                Assert.That(PortConversationReport<string>(runner, "dealOutcome"), Is.EqualTo(DealStatus.Active));
                Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before) + 1));
                Assert.That(EpisodeEngine.SocialActionBudget(after), Is.EqualTo(EpisodeEngine.SocialActionBudget(before)));
                Assert.That(director.SavePath, Is.EqualTo(path)); AssertIntentDurable(after);
                CollectionAssert.AreEqual(primary, File.ReadAllBytes(new EpisodeSaveStore(path).BackupPath));
                director.LoadNow(); HoldTheHouseForTheFixture(); yield return null; AssertIntentDurable(after);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_GenericRealRefusalIsOnePaidProposalNotAnAgreement()
        {
            EpisodeState source = null;
            for (uint seed = 6301; seed < 6333 && source == null; seed++)
            {
                var candidate = ContentCatalog.Create(seed);
                string kind = PortResidualFirstGeneric(candidate, ContentCatalog.MayaId);
                var answer = PortConversationPrehistoryCommand(new EpisodeEngine(candidate),
                    EpisodeCommandKind.ProposeDeal, ContentCatalog.MayaId, kind);
                if (!CommitmentReferences.Deals(answer.state).Any(row => row.proposerId == candidate.playerId))
                    source = candidate;
            }
            Assert.That(source, Is.Not.Null, "Exactly the existing32-source actual-refusal cohort, not modified odds or RNG.");
            yield return InstallPortConversationState(source);
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                var before = director.Snapshot; var primary = File.ReadAllBytes(director.SavePath);
                yield return PortConversationRoutine(runner, "ExerciseOptionalDeal", false);
                Assert.That(PortConversationReport<string>(runner, "dealOutcome"), Is.EqualTo("Refused"));
                Assert.That(PortConversationReport<int>(runner, "dealsProposed"), Is.EqualTo(1));
                Assert.That(PortConversationReport<int>(runner, "dealsAnswered"), Is.Zero);
                Assert.That(CommitmentReferences.Deals(director.Snapshot).Any(row => row.proposerId == before.playerId), Is.False);
                Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1));
                Assert.That(EpisodeEngine.SocialActionsSpent(director.Snapshot), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before) + 1));
                AssertIntentDurable(director.Snapshot);
                CollectionAssert.AreEqual(primary, File.ReadAllBytes(new EpisodeSaveStore(director.SavePath).BackupPath));
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_LegalPactLockKeepsExactDeclineAndAllSlots()
        {
            yield return PortResidualAdmitAuthored(PortResidualAuthoredPactCap());
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                var before = director.Snapshot; string path = director.SavePath;
                var primary = File.ReadAllBytes(path); var backup = PortConversationBackupBytes();
                yield return TalkTo(ContentCatalog.MayaId);
                yield return PortConversationRoutine(runner, "EnterSeasonConversationCategory", EpisodeDirector.DealsCategoryCaption);
                var offer = NpcDeals.Pending(before).First(row => row.proposerId == ContentCatalog.MayaId);
                var yes = PortResidualSemantic(offer.id, true); var no = PortResidualSemantic(offer.id, false);
                AssertPortResidualSemanticSource(yes, before, offer, true); AssertPortResidualSemanticSource(no, before, offer, false);
                Assert.That(yes.IsInteractable(), Is.False); Assert.That(no.IsInteractable(), Is.True);
                yield return PortConversationRoutine(runner, "ExerciseOptionalDeal", false);
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "dealsAnswered"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "dealsProposed"), Is.Zero);
                Assert.That(NpcDeals.Pending(director.Snapshot).Any(row => row.id == offer.id), Is.True);
                Assert.That(director.SavePath, Is.EqualTo(path)); AssertPortConversationUnchanged(before, primary, backup);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_AuthoredAdmittedMultiOfferSelectsOnlyCanonicalSortedIdentity()
        {
            yield return PortResidualAdmitAuthored(PortResidualAuthoredMultipleOffers());
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                var before = director.Snapshot; var offers = NpcDeals.Pending(before).Where(row => row.proposerId == ContentCatalog.MayaId).ToArray();
                Assert.That(offers.Length, Is.EqualTo(2)); var selected = offers[0]; var other = JsonUtility.ToJson(offers[1]);
                yield return TalkTo(ContentCatalog.MayaId);
                yield return PortConversationRoutine(runner, "EnterSeasonConversationCategory", EpisodeDirector.DealsCategoryCaption);
                foreach (var offer in offers)
                {
                    AssertPortResidualSemanticSource(PortResidualSemantic(offer.id, true), before, offer, true);
                    AssertPortResidualSemanticSource(PortResidualSemantic(offer.id, false), before, offer, false);
                }
                var stale = PortResidualSemantic(selected.id, true); var staleClick = stale.onClick;
                yield return PortConversationRoutine(runner, "ExerciseOptionalDeal", false);
                var after = director.Snapshot;
                AssertIntentCommand(before, EpisodeCommandKind.RespondToDeal, selected.id, EpisodeEngine.AcceptDeal);
                Assert.That(PortVerification.SeasonDeal(after, selected.id).status, Is.EqualTo(DealStatus.Active));
                Assert.That(JsonUtility.ToJson(PortVerification.SeasonDeal(after, offers[1].id)), Is.EqualTo(other));
                Assert.That(PortConversationReport<int>(runner, "dealsAnswered"), Is.EqualTo(1));
                Assert.That(PortConversationReport<int>(runner, "dealsProposed"), Is.Zero);
                Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before)));
                Assert.That(PortResidualDescription(stale), Is.Null, "Retired rows never retain current semantic identity.");
                var primary = File.ReadAllBytes(director.SavePath); var backup = PortConversationBackupBytes();
                staleClick.Invoke(); yield return null;
                AssertPortConversationUnchanged(after, primary, backup);
                director.LoadNow(); HoldTheHouseForTheFixture(); yield return null;
                AssertPortConversationUnchanged(after, primary, backup); AssertIntentDurable(after);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_CurrentOfferMetadataRetiresAcrossRealTopicRebuild()
        {
            yield return PortResidualAdmitAuthored(PortResidualAuthoredMultipleOffers());
            yield return TalkTo(ContentCatalog.MayaId);
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                yield return PortConversationRoutine(runner, "EnterSeasonConversationCategory", EpisodeDirector.DealsCategoryCaption);
                var before = director.Snapshot; var offer = NpcDeals.Pending(before).First(row => row.proposerId == ContentCatalog.MayaId);
                var old = PortResidualSemantic(offer.id, true); var description = PortResidualDescription(old);
                long generation = PortResidualFact<long>(description, "RenderGeneration");
                var primary = File.ReadAllBytes(director.SavePath); var backup = PortConversationBackupBytes();
                yield return PortConversationRoutine(runner, "EnterSeasonConversationCategory", EpisodeDirector.DealsCategoryCaption);
                Assert.That(PortResidualDescription(old), Is.Null);
                var current = PortResidualSemantic(offer.id, true); AssertPortResidualSemanticSource(current, before, offer, true);
                Assert.That(PortResidualFact<long>(PortResidualDescription(current), "RenderGeneration"), Is.GreaterThan(generation));
                Assert.That(current, Is.Not.SameAs(old));
                AssertPortConversationUnchanged(before, primary, backup);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_MissingSelectedSemanticRowCannotUseSameCaptionSibling()
        {
            yield return PortResidualAdmitAuthored(PortResidualAuthoredMultipleOffers());
            var runner = BindPortConversationVerifier(out var owner); var result = new PortResidualInterposition();
            // This post-adoption control interposes at the completed production category boundary
            // by a child iterator: all duplicate captions remain real, only exact selected ID is hidden.
            var routine = PortConversationRoutine(runner, "ExerciseOptionalDeal", false);
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            UnityEngine.UI.Button hidden = null; bool returned = false;
            try
            {
                for (int step = 0; stack.Count > 0 && result.failure == null && step < 5000; step++)
                {
                    if (!result.interposed && returned && ReferenceEquals(stack.Peek(), routine)
                        && director.CurrentConversationCategory == EpisodeDirector.DealsCategoryCaption)
                    {
                        result.before = director.Snapshot; result.path = director.SavePath;
                        result.primary = File.ReadAllBytes(result.path); result.backup = PortConversationBackupBytes();
                        AssertPortRequiredOrdinaryOwner(result.before, director.TalkingToId);
                        var offers = NpcDeals.Pending(result.before).Where(row => row.proposerId == ContentCatalog.MayaId).ToArray();
                        Assert.That(offers.Length, Is.EqualTo(2));
                        hidden = PortResidualSemantic(offers[0].id, true); Assert.That(hidden.IsInteractable(), Is.True);
                        Assert.That(PortResidualSemantic(offers[1].id, true).IsInteractable(), Is.True);
                        hidden.gameObject.SetActive(false); result.interposed = true;
                    }
                    returned = false; var top = stack.Peek(); bool moved = false;
                    try { moved = top.MoveNext(); } catch (Exception error) { result.failure = error; }
                    if (result.failure != null) break;
                    if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); returned = stack.Count > 0; continue; }
                    if (top.Current is IEnumerator child) stack.Push(child); else yield return top.Current;
                }
                Assert.That(stack.Count == 0 || result.failure != null, Is.True);
                AssertPortResidualFailure(runner, result, "Eligible conversation leaf is missing");
            }
            finally
            {
                while (stack.Count > 0)
                    try { (stack.Pop() as IDisposable)?.Dispose(); }
                    catch (Exception error) { if (result.cleanupFailure == null) result.cleanupFailure = error; }
                if (hidden != null) hidden.gameObject.SetActive(true);
                UnityEngine.Object.Destroy(owner);
                Assert.That(result.cleanupFailure, Is.Null, "Every owned multirow iterator must unwind.");
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_RealEarnedInvitationCarriesExactSemanticIdentity()
        {
            string id; var source = PortResidualGeneratedOfferAtZero(out id);
            // Normal reload + overview + actual bubble/arrival, deliberately no HoldTheHouse until
            // after hearing it. The source's stats/AP/roles/RNG were earned, not the older shaped bubble fixture.
            Assert.That(EpisodeValidation.TryValidate(source, out var why), Is.True, why);
            new EpisodeSaveStore(director.SavePath).Save(source);
            yield return ReloadEpisode();
            Assert.That(director.ShowOverview(), Is.True);
            yield return WaitForVisibleNpcInvitation();
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(id));
            Assert.That(director.CurrentNpcInvitationNpcId, Is.EqualTo(ContentCatalog.MayaId));
            var hear = ButtonWithCaption(HouseNpcInvitationBubble.HearCaption);
            Assert.That(hear, Is.SameAs(director.GetComponent<HouseNpcInvitationBubble>().HearControl));
            hear.onClick.Invoke(); yield return null; yield return null;
            Assert.That(director.InvitedDealRecordId, Is.EqualTo(id));
            HoldTheHouseForTheFixture();
            Assert.That(director.InvitedDealRecordId, Is.EqualTo(id), "The real invited view remains the renderer owner.");
            var before = director.Snapshot; var offer = NpcDeals.Pending(before).Single(row => row.id == id);
            AssertPortResidualSemanticSource(PortResidualSemantic(id, true), before, offer, true);
            AssertPortResidualSemanticSource(PortResidualSemantic(id, false), before, offer, false);
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                yield return PortConversationRoutine(runner, "ExerciseOptionalDeal", false);
                Assert.That(PortConversationReport<int>(runner, "dealsAnswered"), Is.EqualTo(1));
                Assert.That(PortConversationReport<int>(runner, "dealsProposed"), Is.Zero);
                Assert.That(PortVerification.SeasonDeal(director.Snapshot, id).status, Is.EqualTo(DealStatus.Active));
                Assert.That(EpisodeEngine.SocialActionsSpent(director.Snapshot), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before)));
                AssertIntentDurable(director.Snapshot);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_GenericPaidNoOpCannotConsumeCoverage()
        {
            yield return InstallPortConversationState(ContentCatalog.Create(6301));
            var runner = BindPortConversationVerifier(out var owner); var result = new PortResidualInterposition();
            try
            {
                yield return PortResidualInterpose(PortConversationRoutine(runner, "ExerciseOptionalDeal", false),
                    (state, npc) => EpisodeHud.DealProposeCaption(DealKind.Title(PortResidualFirstGeneric(state, npc)).ToLowerInvariant()),
                    PortResidualProveGeneric, "noop", result);
                AssertPortResidualFailure(runner, result, "must commit exactly one fresh command");
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_PendingFreeNoOpCannotConsumeCoverage()
        {
            string offer; yield return InstallPortConversationState(PortResidualGeneratedOfferAtZero(out offer));
            var runner = BindPortConversationVerifier(out var owner); var result = new PortResidualInterposition();
            try
            {
                yield return PortResidualInterpose(PortConversationRoutine(runner, "ExerciseOptionalDeal", false),
                    (state, npc) => EpisodeHud.DealAcceptCaption, PortResidualProveFree, "noop", result);
                AssertPortResidualFailure(runner, result, "must commit exactly one fresh command");
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_GenericInformationOwnerIsNotDismissedOrAnswered()
        {
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 4401);
            ShippedRules.ApplyFresh(fresh); // Actual shipping policy, once on the genuinely fresh Social source.
            Assert.That(EpisodeValidation.TryValidate(fresh, out var freshError), Is.True, freshError);
            Assert.That(fresh.unifiedCommitmentRulesVersion, Is.EqualTo(UnifiedVoteFamilyValidation.Version));
            yield return InstallPortConversationState(fresh);
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.Social));
            string npc = Listener(director.Snapshot).id;
            yield return TalkTo(npc);
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                yield return PortConversationRoutine(runner, "EnterSeasonConversationCategory", EpisodeDirector.LearnCategoryCaption);
                var before = director.Snapshot;
                Assert.That(InformationShareChoice.Available(before, npc), Is.True);
                var primary = File.ReadAllBytes(director.SavePath); var backup = PortConversationBackupBytes();
                ButtonWithCaption(EpisodeDirector.ShareInformationCaption).onClick.Invoke(); yield return null; yield return null;
                Assert.That(director.IsChoosingSharedInformation, Is.True);
                yield return PortConversationRoutine(runner, "ExerciseOptionalDeal", false);
                Assert.That(director.IsChoosingSharedInformation, Is.True);
                Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.ShareInformationCancelCaption), Is.Not.Null);
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "dealsAnswered"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "dealsProposed"), Is.Zero);
                AssertPortConversationUnchanged(before, primary, backup);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        private static EpisodeState PortResidualNormalRoute(bool price, out string recordId)
        {
            // Same normal4401..4416 eight-person cohort, .5 competition input, week<=2 and64
            // TOTAL accepted commands, including every paid chat. No roles/AP/RNG/asks are authored.
            for (uint seed = 4401; seed < 4417; seed++)
            {
                var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(fresh); // Same fresh entry policy as the game's StartSeason.
                Assert.That(EpisodeValidation.TryValidate(fresh, out var freshError), Is.True, freshError);
                Assert.That(fresh.unifiedCommitmentRulesVersion, Is.EqualTo(UnifiedVoteFamilyValidation.Version));
                var engine = new EpisodeEngine(fresh);
                string storyTarget = null, storyAbout = null;
                bool storyPairSelected = false;
                string warmingNominee = null;
                int storyChats = 0, warmingWeek = 0, afterHoHChats = 0, beforeMeetingChats = 0;
                for (int step = 0; step < 64; step++)
                {
                    var state = engine.Snapshot;
                    if (state.week > 2 || state.phase == EpisodePhase.Finished || state.Find(state.playerId).status != ContestantStatus.Active) break;
                    if (!price && state.phase == EpisodePhase.Social)
                    {
                        var beat = EpisodeEngine.OpenStoryBeats(state).FirstOrDefault(item => item.surface == StorySurfaces.Conversation
                            && state.Active.Any(person => !person.isPlayer && (item.involvedIds.Contains(person.id)
                                || item.cast.Any(role => role.contestantId == person.id))));
                        if (beat != null) { recordId = beat.id; return state; }
                    }
                    // An owner is not permission to carry on hidden actions or answer it for this fixture.
                    // No InformationShare chooser is opened: this local engine route invokes no UI callback.
                    if (EpisodeEngine.OpenStoryBeats(state).Any(item => item.surface == StorySurfaces.Conversation)) break;
                    if (price && state.phase == EpisodePhase.VetoMeeting && !state.vetoResolved && state.vetoHolderId == state.playerId)
                    {
                        // Read the REAL AskForTheVeto result of the accepted Veto -> VetoMeeting Advance.
                        // Preserve the ordinary first-priced/then-promise-lock selector; do not synthesize asks.
                        var ask = NpcDeals.Pending(state).FirstOrDefault(row => row.type == DealKind.VetoUse
                            && row.week == state.week && Negotiation.AskPrice(state, row) != null);
                        if (ask != null && EpisodeDirector.VetoPromisedTo(state, ask.proposerId) == null)
                        {
                            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
                            recordId = ask.id; return state;
                        }
                    }
                    if (warmingWeek != state.week)
                    {
                        warmingWeek = state.week; warmingNominee = null;
                        afterHoHChats = 0; beforeMeetingChats = 0;
                    }
                    // Choose one CURRENT announced nominee in declared cast order, before any warmth result
                    // or future veto winner is known. Do not switch targets or inspect future outcomes.
                    if (price && warmingNominee == null && state.phase == EpisodePhase.Nomination && state.nominees.Count == 2)
                        warmingNominee = state.Active.FirstOrDefault(person => !person.isPlayer && state.nominees.Contains(person.id))?.id;
                    var command = NextCommand(state);
                    bool paid = false, storyChat = false, afterHoH = false, beforeMeeting = false;
                    string targetId = null;
                    var kind = EpisodeCommandKind.PersonalChat;
                    if (command.kind == EpisodeCommandKind.Advance && state.pendingDiary == null
                        && EpisodeEngine.SocialActionsSpent(state) < EpisodeEngine.SocialActionBudget(state))
                    {
                        // Replace, never add to, the SAME two paid Story actions in the FIRST
                        // post-eviction Social window: still stored week1, before the HoH2 Advance.
                        // Choose both real NPCs ONCE in cast order, never by a future roll or hostility.
                        if (!price && state.week == 1 && state.phase == EpisodePhase.Social && state.evictionResolved
                            && EpisodeEngine.Window(state) == Windows.AfterEviction && !EpisodeEngine.BeforeTheFirstAsk(state)
                            && EpisodeEngine.AirtimeFor(state, StorySurfaces.Conversation) && storyChats < 2)
                        {
                            if (!storyPairSelected)
                            {
                                storyPairSelected = true;
                                storyTarget = state.Active.FirstOrDefault(person => !person.isPlayer)?.id;
                                storyAbout = state.Active.FirstOrDefault(person => !person.isPlayer && person.id != storyTarget)?.id;
                            }
                            targetId = storyTarget; kind = EpisodeCommandKind.VentAbout; storyChat = true;
                        }
                        else if (price && state.phase == EpisodePhase.Nomination && state.nominees.Count == 2
                            && afterHoHChats < 2 && EpisodeEngine.Window(state) == Windows.AfterHoH)
                        {
                            targetId = warmingNominee; kind = EpisodeCommandKind.RelationshipBuilding; afterHoH = true;
                        }
                        else if (price && state.phase == EpisodePhase.Veto && state.competitionResolved
                            && state.vetoHolderId == state.playerId && beforeMeetingChats < 1
                            && EpisodeEngine.Window(state) == Windows.AfterNominations)
                        {
                            // This ONE seat is shared by VetoSelection/Veto/VetoMeeting. Its actual remaining
                            // budget, not a fresh phase or a future result, authorizes the pre-entry action.
                            targetId = warmingNominee; kind = EpisodeCommandKind.RelationshipBuilding; beforeMeeting = true;
                        }
                        var target = targetId == null ? null : state.Find(targetId);
                        if (target != null && !target.isPlayer && target.status == ContestantStatus.Active
                            && (!price || state.nominees.Contains(target.id))
                            && (!storyChat || state.Find(storyAbout) is ContestantState about
                                && !about.isPlayer && about.status == ContestantStatus.Active && about.id != target.id)
                            && EpisodeEngine.ConversationWindowRefusal(state, target.id, kind) == null)
                        {
                            command.kind = kind; command.targetId = target.id; paid = true;
                            if (storyChat) command.secondTargetId = storyAbout;
                        }
                    }
                    int spent = EpisodeEngine.SocialActionsSpent(state);
                    var result = engine.Apply(command); // Exactly ONE accepted command in this64-step iteration.
                    Assert.That(result.accepted, Is.True, "Bounded normal source route: " + result.reason);
                    if (paid)
                    {
                        Assert.That(result.state.revision, Is.EqualTo(state.revision + 1));
                        Assert.That(result.state.acceptedCommandIds.Count, Is.EqualTo(state.acceptedCommandIds.Count + 1));
                        Assert.That(result.state.acceptedCommandIds.Last(), Is.EqualTo(command.id));
                        Assert.That(EpisodeEngine.SocialActionsSpent(result.state), Is.EqualTo(spent + 1));
                        if (storyChat) storyChats++;
                        if (afterHoH) afterHoHChats++;
                        if (beforeMeeting) beforeMeetingChats++;
                    }
                }
            }
            Assert.Fail("The exact16-source/64-command/week2 route must qualify this real owner/price context; no expansion, forced roles or skip.");
            recordId = null; return null;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_CommandGeneratedConversationStoryBlocksGenericWithoutAnswer()
        {
            string id; yield return InstallPortConversationState(PortResidualNormalRoute(false, out id));
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                var before = director.Snapshot;
                Assert.That(before.phase, Is.EqualTo(EpisodePhase.Social));
                var beat = EpisodeEngine.OpenStoryBeats(before).Single(item => item.id == id && item.surface == StorySurfaces.Conversation);
                string npc = before.Active.First(person => !person.isPlayer
                    && (beat.involvedIds.Contains(person.id) || beat.cast.Any(role => role.contestantId == person.id))).id;
                var primary = File.ReadAllBytes(director.SavePath); var backup = PortConversationBackupBytes();
                yield return TalkTo(npc);
                Assert.That(director.TalkingToId, Is.EqualTo(npc));
                Assert.That(director.IsConversationOpen, Is.True);
                Assert.That(director.InvitedDealRecordId, Is.Null, "A real invitation owns rendering before Story; do not claim that other view as Story coverage.");
                Assert.That(director.IsChoosingSharedInformation, Is.False);
                Assert.That(director.IsConversationExchangePlaying, Is.False);
                Assert.That(director.CurrentConversationCategory, Is.Null);
                string ownerNote = "Conversation story " + id + " owns " + npc + ".";
                Assert.That(director.GetComponentsInChildren<TMPro.TMP_Text>(true)
                    .Any(label => label.isActiveAndEnabled && label.text == StoryText.Title(beat)), Is.True,
                    "The actual canonical Story header, not an ordinary topic or invitation, must own the open view.");
                AssertPortConversationUnchanged(before, primary, backup);
                yield return PortConversationRoutine(runner, "ExerciseOptionalDeal", false);
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "dealsAnswered"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "dealsProposed"), Is.Zero);
                var attempts = PortConversationReport<IList>(runner, "conversationAttempts");
                Assert.That(attempts.Count, Is.EqualTo(1), "The real current owner must be classified, not replaced by AP ineligibility or an unrelated unreachable body.");
                object attempt = attempts[0];
                Assert.That((string)attempt.GetType().GetField("feature").GetValue(attempt), Is.EqualTo("GenericDeal"));
                Assert.That((string)attempt.GetType().GetField("disposition").GetValue(attempt), Is.EqualTo("BlockedByOtherView"));
                Assert.That((string)attempt.GetType().GetField("npcId").GetValue(attempt), Is.EqualTo(npc));
                Assert.That((string)attempt.GetType().GetField("note").GetValue(attempt), Is.EqualTo(ownerNote));
                Assert.That(PortConversationReport<string>(runner, "dealNote"), Is.EqualTo(ownerNote));
                Assert.That(director.IsConversationOpen && director.TalkingToId == npc, Is.True);
                Assert.That(director.InvitedDealRecordId, Is.Null);
                Assert.That(director.IsChoosingSharedInformation || director.IsConversationExchangePlaying, Is.False);
                Assert.That(director.CurrentConversationCategory, Is.Null);
                Assert.That(director.GetComponentsInChildren<TMPro.TMP_Text>(true)
                    .Any(label => label.isActiveAndEnabled && label.text == StoryText.Title(beat)), Is.True);
                Assert.That(EpisodeEngine.OpenStoryBeats(director.Snapshot).Any(item => item.id == id), Is.True);
                AssertPortConversationUnchanged(before, primary, backup);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_RealVetoAskRendererKeepsIdentityAndActualLinkedPrice()
        {
            string id; yield return InstallPortConversationState(PortResidualNormalRoute(true, out id));
            yield return OpenStation(); yield return null; yield return null;
            var before = director.Snapshot; var ask = NpcDeals.Pending(before).Single(row => row.id == id);
            Assert.That(before.phase, Is.EqualTo(EpisodePhase.VetoMeeting));
            Assert.That(before.vetoHolderId, Is.EqualTo(before.playerId)); Assert.That(before.vetoResolved, Is.False);
            var actualPrice = Negotiation.AskPrice(before, ask); Assert.That(actualPrice, Is.Not.Null);
            var yes = PortResidualSemantic(id, true); var no = PortResidualSemantic(id, false);
            AssertPortResidualSemanticSource(yes, before, ask, true); AssertPortResidualSemanticSource(no, before, ask, false);
            Assert.That(yes.IsInteractable() && no.IsInteractable(), Is.True);
            var oldIds = CommitmentReferences.Deals(before).Select(row => row.id).ToArray();
            var primary = File.ReadAllBytes(director.SavePath);
            yes.Select(); yield return null; yield return null;
            var current = PortResidualSemantic(id, true); current.onClick.Invoke(); yield return null; yield return null;
            var after = director.Snapshot; var taken = PortVerification.SeasonDeal(after, id);
            AssertIntentCommand(before, EpisodeCommandKind.RespondToDeal, id, EpisodeEngine.AcceptDeal);
            Assert.That(taken.status, Is.EqualTo(DealStatus.Active));
            Assert.That(taken.linkedDealId, Is.Not.Null.And.Not.Empty);
            var price = PortVerification.SeasonDeal(after, taken.linkedDealId);
            Assert.That(price, Is.Not.Null);
            CollectionAssert.DoesNotContain(oldIds, price.id); Assert.That(price.linkedDealId, Is.EqualTo(id));
            Assert.That(price.type, Is.EqualTo(actualPrice.kind));
            Assert.That(price.targetId, Is.EqualTo(actualPrice.aboutId)); Assert.That(price.expiresWeek, Is.EqualTo(actualPrice.expiresWeek));
            Assert.That(price.proposerId, Is.EqualTo(ask.proposerId)); Assert.That(price.recipientId, Is.EqualTo(after.playerId));
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before)));
            Assert.That(after.vetoResolved, Is.False, "This control answers the actual ask, not the veto decision.");
            CollectionAssert.AreEqual(primary, File.ReadAllBytes(new EpisodeSaveStore(director.SavePath).BackupPath));
            AssertIntentDurable(after);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_GenericFiniteOutOfScopeAndNoBodyHaveZeroCoverage()
        {
            yield return InstallPortConversationState(PortResidualSpendToZero(ContentCatalog.Create(6401)));
            var runner = BindPortConversationVerifier(out var owner);
            var bodies = SceneComponents<HouseNpc>().Where(body => body.gameObject.activeSelf).ToArray();
            try
            {
                var before = director.Snapshot;
                var primary = File.ReadAllBytes(director.SavePath); var backup = PortConversationBackupBytes();
                foreach (var body in bodies) body.gameObject.SetActive(false);
                yield return PortConversationRoutine(runner, "ExerciseOptionalDeal", false);
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "dealsProposed"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "dealsAnswered"), Is.Zero);
                AssertPortConversationUnchanged(before, primary, backup);
            }
            finally
            {
                foreach (var body in bodies) if (body != null) body.gameObject.SetActive(true);
                UnityEngine.Object.Destroy(owner);
            }
            runner = BindPortConversationVerifier(out owner);
            try
            {
                var before = director.Snapshot;
                Assert.That(before.phase, Is.EqualTo(EpisodePhase.Social));
                Assert.That(EpisodeEngine.SocialActionsSpent(before), Is.EqualTo(EpisodeEngine.SocialActionBudget(before)));
                Assert.That(NpcDeals.Pending(before).Any(row => row.proposerId == ContentCatalog.MayaId), Is.False);
                var primary = File.ReadAllBytes(director.SavePath); var backup = PortConversationBackupBytes();
                yield return PortConversationRoutine(runner, "ExerciseOptionalDeal", false);
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "dealsProposed"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "dealsAnswered"), Is.Zero);
                var attempts = PortConversationReport<IList>(runner, "conversationAttempts");
                Assert.That(attempts.Cast<object>().Any(row => (string)row.GetType().GetField("feature").GetValue(row) == "GenericDeal"
                    && (string)row.GetType().GetField("disposition").GetValue(row) == "Ineligible"), Is.True);
                AssertPortConversationUnchanged(before, primary, backup);
            }
            finally { UnityEngine.Object.Destroy(owner); }
            string voter, subject, kind;
            yield return InstallPortConversationState(PortRequiredNamedVoteFixture(out voter, out subject, out kind));
            runner = BindPortConversationVerifier(out owner);
            try
            {
                var before = director.Snapshot; var primary = File.ReadAllBytes(director.SavePath); var backup = PortConversationBackupBytes();
                yield return PortConversationRoutine(runner, "ExerciseOptionalDeal", false);
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "dealsProposed"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "dealsAnswered"), Is.Zero);
                AssertPortConversationUnchanged(before, primary, backup);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_SelectedMissingWrongOrDuplicateMetadataFailsClosed()
        {
            foreach (string fault in new[] { "missing", "wrong-role", "duplicate" })
            {
                string id; yield return InstallPortConversationState(PortResidualGeneratedOfferAtZero(out id));
                var runner = BindPortConversationVerifier(out var owner); var result = new PortResidualInterposition();
                IDictionary registry = null; object acceptKey = null, declineKey = null, acceptValue = null, declineValue = null;
                try
                {
                    yield return PortResidualInterpose(PortConversationRoutine(runner, "ExerciseOptionalDeal", false),
                        (state, npc) => EpisodeHud.DealAcceptCaption, PortResidualProveFree, "metadata", result, button =>
                        {
                            var yes = PortResidualSemantic(id, true); var no = PortResidualSemantic(id, false);
                            Assert.That(yes, Is.SameAs(button));
                            var field = typeof(EpisodeHud).GetField("offerControls", BindingFlags.Instance | BindingFlags.NonPublic);
                            Assert.That(field, Is.Not.Null, "Post-adoption corruption control, never a meaningful old-red.");
                            var candidates = SceneComponents<EpisodeHud>().Select(hud => (IDictionary)field.GetValue(hud))
                                .Where(map => map.Contains(yes) && map.Contains(no)).ToArray();
                            Assert.That(candidates.Length, Is.EqualTo(1));
                            registry = candidates[0]; acceptKey = yes; declineKey = no;
                            acceptValue = registry[acceptKey]; declineValue = registry[declineKey];
                            // Test-owned metadata fault, not a mutable game/picker/command-authority hook.
                            if (fault == "missing") registry.Remove(acceptKey);
                            else if (fault == "wrong-role") registry[acceptKey] = declineValue;
                            else registry[declineKey] = acceptValue;
                        });
                    AssertPortResidualFailure(runner, result, "Eligible conversation leaf is missing");
                }
                finally
                {
                    if (registry != null) { registry[acceptKey] = acceptValue; registry[declineKey] = declineValue; }
                    UnityEngine.Object.Destroy(owner);
                }
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_ActualBubbleDrawsAuthoredPactLockWithLiveUnpressedDecline()
        {
            // Authored pact/offer load-boundary provenance, but the visible physical invitation
            // itself is real. No private invitation fields, Render calls, scores or AP assignment.
            var source = PortResidualAuthoredPactCap();
            Assert.That(EpisodeValidation.TryValidate(source, out var why), Is.True, why);
            new EpisodeSaveStore(director.SavePath).Save(source); yield return ReloadEpisode();
            Assert.That(director.ShowOverview(), Is.True); yield return WaitForVisibleNpcInvitation();
            string id = director.CurrentNpcInvitationRecordId;
            Assert.That(director.CurrentNpcInvitationNpcId, Is.EqualTo(ContentCatalog.MayaId));
            var hear = ButtonWithCaption(HouseNpcInvitationBubble.HearCaption);
            Assert.That(hear, Is.SameAs(director.GetComponent<HouseNpcInvitationBubble>().HearControl));
            hear.onClick.Invoke(); yield return null; yield return null;
            Assert.That(director.InvitedDealRecordId, Is.EqualTo(id));
            HoldTheHouseForTheFixture();
            Assert.That(director.InvitedDealRecordId, Is.EqualTo(id), "The real invited view remains the renderer owner.");
            var before = director.Snapshot; var offer = NpcDeals.Pending(before).Single(row => row.id == id);
            Assert.That(offer.type, Is.EqualTo(DealKind.AllianceInvite));
            Assert.That(EpisodeEngine.InvitationPastPactCap(before, offer.proposerId), Is.True);
            var yes = PortResidualSemantic(id, true); var no = PortResidualSemantic(id, false);
            AssertPortResidualSemanticSource(yes, before, offer, true); AssertPortResidualSemanticSource(no, before, offer, false);
            Assert.That(yes.IsInteractable(), Is.False); Assert.That(no.IsInteractable(), Is.True);
            var primary = File.ReadAllBytes(director.SavePath); var backup = PortConversationBackupBytes();
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                yield return PortConversationRoutine(runner, "ExerciseOptionalDeal", false);
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "dealsAnswered"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "dealsProposed"), Is.Zero);
                Assert.That(NpcDeals.Pending(director.Snapshot).Any(row => row.id == id), Is.True);
                AssertPortConversationUnchanged(before, primary, backup);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }
        // END GENERIC_PENDING_RESIDUAL01

        // BEGIN GENERIC_PENDING_PRESENTATION02 — additive over all forty candidate01 cases.
        // Post-adoption metadata/visibility controls, NOT meaningful old-runtime negatives.
        private sealed class PortOfferGroupImage
        {
            public CanvasGroup group;
            public bool created, enabled, interactable, raycasts, ignoreParents;
            public float alpha;

            public static PortOfferGroupImage Capture(CanvasGroup value, bool newComponent) => new PortOfferGroupImage
            {
                group = value, created = newComponent, enabled = value.enabled, alpha = value.alpha,
                interactable = value.interactable, raycasts = value.blocksRaycasts, ignoreParents = value.ignoreParentGroups,
            };

            public void Restore()
            {
                if (group == null) return;
                if (created)
                {
                    group.enabled = false; // Remove the test-owned effect immediately, then the component.
                    UnityEngine.Object.Destroy(group); return;
                }
                group.alpha = alpha; group.interactable = interactable; group.blocksRaycasts = raycasts;
                group.ignoreParentGroups = ignoreParents; group.enabled = enabled;
            }

            public void AssertRestored()
            {
                if (created) { Assert.That(group == null, Is.True, "Test-owned presentation group must be retired."); return; }
                Assert.That(group, Is.Not.Null);
                Assert.That(group.alpha, Is.EqualTo(alpha)); Assert.That(group.enabled, Is.EqualTo(enabled));
                Assert.That(group.interactable, Is.EqualTo(interactable));
                Assert.That(group.blocksRaycasts, Is.EqualTo(raycasts));
                Assert.That(group.ignoreParentGroups, Is.EqualTo(ignoreParents));
            }
        }

        private sealed class PortOfferPresentationImage
        {
            public EpisodeHud hud;
            public Canvas canvas;
            public bool canvasEnabled, changedCinematic, changedReveal;
            public readonly List<PortOfferGroupImage> groups = new List<PortOfferGroupImage>();

            public CanvasGroup At(Transform owner)
            {
                Assert.That(owner, Is.Not.Null);
                var group = owner.GetComponent<CanvasGroup>();
                bool created = group == null;
                if (created) group = owner.gameObject.AddComponent<CanvasGroup>();
                if (!groups.Any(image => image.group == group)) groups.Add(PortOfferGroupImage.Capture(group, created));
                group.enabled = true; return group;
            }

            public void Suppress(UnityEngine.UI.Button button, string fault)
            {
                Assert.That(hud.IsCinematic, Is.False); Assert.That(hud.IsHeldForReveal, Is.False);
                Assert.That(canvas.isActiveAndEnabled, Is.True);
                // Remember an existing owner group before the real API changes its public properties.
                var owner = canvas.GetComponent<CanvasGroup>();
                if (owner != null) groups.Add(PortOfferGroupImage.Capture(owner, false));
                if (fault == "cinematic" || fault == "ignored-parents-owner")
                {
                    if (fault == "ignored-parents-owner") At(button.transform).ignoreParentGroups = true;
                    changedCinematic = true;
                    hud.SetCinematic(fault == "cinematic", interactive: false);
                }
                else if (fault == "reveal") { changedReveal = true; hud.HoldForReveal(true); }
                else if (fault == "shown-noninteractive")
                { changedCinematic = true; hud.SetCinematic(false, interactive: false); }
                else if (fault == "ancestor-alpha") At(button.transform.parent).alpha = 0f;
                else if (fault == "ancestor-raycasts") At(button.transform.parent).blocksRaycasts = false;
                else if (fault == "ancestor-interactable") At(button.transform.parent).interactable = false;
                else if (fault == "composite-alpha")
                {
                    Assert.That(button.transform.parent.parent, Is.Not.SameAs(button.transform.parent));
                    At(button.transform.parent).alpha = .08f;
                    At(button.transform.parent.parent).alpha = .08f;
                }
                else if (fault == "canvas-disabled") canvas.enabled = false;
                else Assert.Fail("Only the fixed presentation-fault roster is permitted: " + fault);
                // SetCinematic/HoldForReveal can create their owner group; record its prior absence.
                owner = canvas.GetComponent<CanvasGroup>();
                if (owner != null && !groups.Any(image => image.group == owner))
                    groups.Add(PortOfferGroupImage.Capture(owner, true));
            }

            public void Restore()
            {
                if (hud != null && changedReveal) hud.HoldForReveal(false);
                if (hud != null && changedCinematic) hud.SetCinematic(false, interactive: true);
                if (canvas != null) canvas.enabled = canvasEnabled;
                for (int index = groups.Count - 1; index >= 0; index--) groups[index].Restore();
            }

            public void AssertRestored()
            {
                Assert.That(hud, Is.Not.Null); Assert.That(canvas, Is.Not.Null);
                Assert.That(hud.IsCinematic, Is.False); Assert.That(hud.IsHeldForReveal, Is.False);
                Assert.That(canvas.enabled, Is.EqualTo(canvasEnabled));
                foreach (var image in groups) image.AssertRestored();
            }
        }

        private IEnumerator PortOfferSuppressionDuringFocus(string fault, bool actualInvitation = false)
        {
            string id; var source = PortResidualGeneratedOfferAtZero(out id);
            if (!actualInvitation) yield return InstallPortConversationState(source);
            else
            {
                // Same real-bubble/Hear source and physical route as the preserved candidate01 case.
                Assert.That(EpisodeValidation.TryValidate(source, out var why), Is.True, why);
                new EpisodeSaveStore(director.SavePath).Save(source); yield return ReloadEpisode();
                Assert.That(director.ShowOverview(), Is.True); yield return WaitForVisibleNpcInvitation();
                Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(id));
                Assert.That(director.CurrentNpcInvitationNpcId, Is.EqualTo(ContentCatalog.MayaId));
                var hear = ButtonWithCaption(HouseNpcInvitationBubble.HearCaption);
                Assert.That(hear, Is.SameAs(director.GetComponent<HouseNpcInvitationBubble>().HearControl));
                hear.onClick.Invoke(); yield return null; yield return null;
                Assert.That(director.InvitedDealRecordId, Is.EqualTo(id));
                HoldTheHouseForTheFixture();
                Assert.That(director.InvitedDealRecordId, Is.EqualTo(id));
            }
            var runner = BindPortConversationVerifier(out var owner);
            var result = new PortResidualInterposition();
            var owned = new Stack<IEnumerator>();
            owned.Push(PortConversationRoutine(runner, "ExerciseOptionalDeal", false));
            PortOfferPresentationImage presentation = null;
            UnityEngine.UI.Button observed = null;
            UnityEngine.UI.Button.ButtonClickedEvent originalClick = null;
            int invocations = 0;
            UnityEngine.Events.UnityAction observer = () => invocations++;
            try
            {
                for (int step = 0; owned.Count > 0 && result.failure == null && step < 5000; step++)
                {
                    var top = owned.Peek(); bool moved = false;
                    try { moved = top.MoveNext(); } catch (Exception error) { result.failure = error; }
                    if (result.failure != null) break;
                    if (!moved) { (owned.Pop() as IDisposable)?.Dispose(); continue; }
                    if (top.Current is IEnumerator child) { owned.Push(child); continue; }
                    if (!result.interposed && top.GetType().Name.StartsWith("<ClickSeasonOfferControl>", StringComparison.Ordinal))
                    {
                        // The actual companion has already required its current exact row and called Select.
                        // Interpose at its FIRST yielded focus frame, before its own two-frame refind.
                        Assert.That(top.Current, Is.Null);
                        result.before = director.Snapshot; result.path = director.SavePath;
                        AssertPortRequiredOrdinaryOwner(result.before, director.TalkingToId);
                        var offer = NpcDeals.Pending(result.before).First(row => row.proposerId == director.TalkingToId);
                        Assert.That(offer.id, Is.EqualTo(id));
                        if (actualInvitation) Assert.That(director.InvitedDealRecordId, Is.EqualTo(id));
                        observed = PortResidualSemantic(id, true);
                        PortResidualProveFree(result.before, director.TalkingToId, observed);
                        AssertPortResidualSemanticSource(observed, result.before, offer, true);
                        AssertPortResidualSemanticSource(PortResidualSemantic(id, false), result.before, offer, false);
                        Assert.That(UnityEngine.EventSystems.EventSystem.current, Is.Not.Null);
                        Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject,
                            Is.SameAs(observed.gameObject), "Proof of the real selected control precedes suppression.");
                        result.primary = File.ReadAllBytes(result.path); result.backup = PortConversationBackupBytes();
                        Assert.That(PortConversationReport<int>(runner, "commands"), Is.Zero);
                        Assert.That(PortConversationReport<int>(runner, "dealsAnswered"), Is.Zero);
                        Assert.That(PortConversationReport<int>(runner, "dealsProposed"), Is.Zero);
                        var hud = observed.GetComponentInParent<EpisodeHud>();
                        var canvas = observed.GetComponentInParent<Canvas>();
                        Assert.That(hud, Is.Not.Null); Assert.That(canvas, Is.Not.Null);
                        Assert.That(hud.IsCinematic, Is.False); Assert.That(hud.IsHeldForReveal, Is.False);
                        presentation = new PortOfferPresentationImage
                        { hud = hud, canvas = canvas, canvasEnabled = canvas.enabled };
                        originalClick = observed.onClick; observed.onClick.AddListener(observer);
                        presentation.Suppress(observed, fault);
                        Assert.That(observed.onClick, Is.SameAs(originalClick), "No listener or authority is replaced.");
                        AssertPortConversationUnchanged(result.before, result.primary, result.backup);
                        result.interposed = true;
                    }
                    yield return top.Current;
                }
                Assert.That(owned.Count == 0 || result.failure != null, Is.True, "Bounded5000-step focus traversal exhausted.");
                AssertPortResidualFailure(runner, result, "Eligible conversation leaf is missing");
                Assert.That(invocations, Is.Zero, "Suppressed/non-owning rows must never reach onClick, even if a callback would refuse.");
                Assert.That(NpcDeals.Pending(director.Snapshot).Any(row => row.id == id), Is.True);
            }
            finally
            {
                while (owned.Count > 0)
                    try { (owned.Pop() as IDisposable)?.Dispose(); }
                    catch (Exception error) { if (result.cleanupFailure == null) result.cleanupFailure = error; }
                if (observed != null && originalClick != null) originalClick.RemoveListener(observer);
                try { presentation?.Restore(); }
                catch (Exception error) { if (result.cleanupFailure == null) result.cleanupFailure = error; }
                UnityEngine.Object.Destroy(owner);
            }
            Assert.That(result.cleanupFailure, Is.Null, "Suspended iterators and exact presentation must restore.");
            yield return null; // Complete retirement of any test-owned presentation-only components.
            presentation.AssertRestored();
            AssertPortConversationUnchanged(result.before, result.primary, result.backup);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_CinematicDuringOfferFocusNeverInvokes() =>
            PortOfferSuppressionDuringFocus("cinematic");

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_RevealDuringOfferFocusNeverInvokes() =>
            PortOfferSuppressionDuringFocus("reveal");

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_ShownNoninteractiveDuringOfferFocusNeverInvokes() =>
            PortOfferSuppressionDuringFocus("shown-noninteractive");

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_TransparentAncestorDuringOfferFocusNeverInvokes() =>
            PortOfferSuppressionDuringFocus("ancestor-alpha");

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_NonRaycastingAncestorDuringOfferFocusNeverInvokes() =>
            PortOfferSuppressionDuringFocus("ancestor-raycasts");

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_NoninteractiveAncestorDuringOfferFocusNeverInvokes() =>
            PortOfferSuppressionDuringFocus("ancestor-interactable");

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_CompositeOpacityDuringOfferFocusNeverInvokes() =>
            PortOfferSuppressionDuringFocus("composite-alpha");

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_DisabledCanvasDuringOfferFocusNeverInvokes() =>
            PortOfferSuppressionDuringFocus("canvas-disabled");

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_IgnoreParentsCannotBypassNonowningHudDuringOfferFocus() =>
            PortOfferSuppressionDuringFocus("ignored-parents-owner");

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_ActualInvitedOfferCinematicDuringFocusNeverInvokes() =>
            PortOfferSuppressionDuringFocus("cinematic", actualInvitation: true);

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_ActualInvitedOfferRevealDuringFocusNeverInvokes() =>
            PortOfferSuppressionDuringFocus("reveal", actualInvitation: true);

        [UnityTest, Timeout(180000)]
        public IEnumerator PortConversationAdapters_VisibleSourceLockMetadataSurvivesPresentationRestoration()
        {
            yield return InstallPortConversationState(PortResidualAuthoredPactCap());
            var runner = BindPortConversationVerifier(out var owner);
            PortOfferPresentationImage presentation = null;
            UnityEngine.UI.Button yes = null, no = null;
            int invocations = 0;
            UnityEngine.Events.UnityAction observer = () => invocations++;
            EpisodeState before = null; byte[] primary = null, backup = null;
            try
            {
                try
                {
                    yield return TalkTo(ContentCatalog.MayaId);
                    yield return PortConversationRoutine(runner, "EnterSeasonConversationCategory", EpisodeDirector.DealsCategoryCaption);
                    before = director.Snapshot; AssertPortRequiredOrdinaryOwner(before, ContentCatalog.MayaId);
                    var offer = NpcDeals.Pending(before).First(row => row.proposerId == ContentCatalog.MayaId);
                    Assert.That(offer.type, Is.EqualTo(DealKind.AllianceInvite));
                    Assert.That(EpisodeEngine.InvitationPastPactCap(before, offer.proposerId), Is.True);
                    yes = PortResidualSemantic(offer.id, true); no = PortResidualSemantic(offer.id, false);
                    AssertPortResidualSemanticSource(yes, before, offer, true);
                    AssertPortResidualSemanticSource(no, before, offer, false);
                    Assert.That(yes.IsInteractable(), Is.False); Assert.That(no.IsInteractable(), Is.True);
                    Assert.That(ShownText(), Does.Contain(EpisodeDirector.PactCapOfferLine(before, offer.proposerId)));
                    primary = File.ReadAllBytes(director.SavePath); backup = PortConversationBackupBytes();
                    yes.onClick.AddListener(observer); no.onClick.AddListener(observer);
                    var hud = yes.GetComponentInParent<EpisodeHud>(); var canvas = yes.GetComponentInParent<Canvas>();
                    Assert.That(hud, Is.Not.Null); Assert.That(canvas, Is.Not.Null);
                    presentation = new PortOfferPresentationImage { hud = hud, canvas = canvas, canvasEnabled = canvas.enabled };
                    presentation.Suppress(yes, "reveal");
                    Assert.That(PortResidualDescription(yes), Is.Null); Assert.That(PortResidualDescription(no), Is.Null);
                    yield return null; yield return null;
                    Assert.That(PortResidualDescription(yes), Is.Null); Assert.That(PortResidualDescription(no), Is.Null);
                    Assert.That(invocations, Is.Zero); AssertPortConversationUnchanged(before, primary, backup);
                }
                finally
                {
                    if (yes != null) yes.onClick.RemoveListener(observer);
                    if (no != null) no.onClick.RemoveListener(observer);
                    presentation?.Restore();
                }
                yield return null;
                presentation.AssertRestored();
                var restoredOffer = NpcDeals.Pending(before).First(row => row.proposerId == ContentCatalog.MayaId);
                AssertPortResidualSemanticSource(yes, before, restoredOffer, true);
                AssertPortResidualSemanticSource(no, before, restoredOffer, false);
                Assert.That(yes.IsInteractable(), Is.False); Assert.That(no.IsInteractable(), Is.True);
                Assert.That(invocations, Is.Zero); AssertPortConversationUnchanged(before, primary, backup);
                Assert.That(PortConversationReport<int>(runner, "commands"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "dealsAnswered"), Is.Zero);
                Assert.That(PortConversationReport<int>(runner, "dealsProposed"), Is.Zero);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }
        // END GENERIC_PENDING_PRESENTATION02
    }
}
