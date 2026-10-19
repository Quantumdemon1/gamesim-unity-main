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
    }
}
