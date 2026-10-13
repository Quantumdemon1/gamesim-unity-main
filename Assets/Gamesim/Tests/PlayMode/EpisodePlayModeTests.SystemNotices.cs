using System.Collections;
using System.IO;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private IEnumerator InstallSystemNoticeExchange()
        {
            yield return InstallExchangeFlowHouse();
            var before = director.Snapshot;
            var result = director.Submit(ExchangeFlowCommand(before, EpisodeCommandKind.SmallTalk));
            Assert.That(result.accepted, Is.True, result.reason);
            yield return null;
            Assert.That(director.IsConversationExchangePlaying, Is.True);
            Assert.That(director.StatusMessage, Does.Contain("Saved locally."));
        }

        private void AssertSystemNoticeVisible(string notice)
        {
            Assert.That(ActiveRect("Status"), Is.Not.Null, "A system failure must have a visible status strip.");
            Assert.That(ExchangeFlowStatusText(), Does.Contain(notice));
            Assert.That(ExchangeFlowStatusText(), Does.Not.Contain("Saved locally"));
            Assert.That(ActiveRect(EpisodeHud.SaveFeedbackName), Is.Null,
                "A failure cannot retain the previous successful save's footer.");
            Assert.That(director.IsConversationExchangePlaying, Is.False);
            Assert.That(director.ConversationExchangeSpeakerId, Is.Null);
            Assert.That(ActiveRect(EpisodeHud.ConversationExchangeLineName), Is.Null);
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ConversationContinueCaption), Is.Null);
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ConversationSkipCaption), Is.Null);
        }

        [UnityTest]
        public IEnumerator SystemNotices_FailedLoadKeepsItsErrorVisibleAndCancelsThePreviousExchange()
        {
            yield return InstallSystemNoticeExchange();
            var before = director.Snapshot;
            string path = director.SavePath;
            byte[] savedBytes = File.ReadAllBytes(path);
            var store = new EpisodeSaveStore(path);
            byte[] backupBytes = File.ReadAllBytes(store.BackupPath);
            var retainedContinue = ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick;
            var retainedSkip = ButtonWithCaption(EpisodeHud.ConversationSkipCaption).onClick;
            const string unreadable = "{ invalid test-owned save";
            try
            {
                // Only this fixture's isolated primary is damaged. Load must leave it and the
                // valid backup in place while the already committed in-memory season survives.
                File.WriteAllText(path, unreadable);
                Assert.That(store.TryLoad(out _, out string expectedNotice), Is.False);
                director.LoadNow();
                yield return null;
                Assert.That(director.StatusMessage, Is.EqualTo(expectedNotice));
                AssertSystemNoticeVisible(expectedNotice);
                AssertEquivalent(before, director.Snapshot);
                Assert.That(director.SavePath, Is.EqualTo(path));
                Assert.That(File.ReadAllText(path), Is.EqualTo(unreadable));
                CollectionAssert.AreEqual(backupBytes, File.ReadAllBytes(store.BackupPath));

                retainedContinue.Invoke(); retainedSkip.Invoke();
                yield return null;
                AssertSystemNoticeVisible(expectedNotice);
                AssertEquivalent(before, director.Snapshot);
            }
            finally { File.WriteAllBytes(path, savedBytes); }
            AssertExchangeFlowUnchanged(before, savedBytes);
        }

        [UnityTest]
        public IEnumerator SystemNotices_MissingImportFileKeepsItsValidationErrorVisible()
        {
            yield return InstallSystemNoticeExchange();
            var before = director.Snapshot;
            byte[] savedBytes = File.ReadAllBytes(director.SavePath);
            string missing = Path.Combine(Path.GetDirectoryName(director.SavePath), "missing-notice-import.json");
            Assert.That(File.Exists(missing), Is.False);

            director.ImportFile(missing);
            yield return null;
            const string notice = "Choose an existing JSON file smaller than eight MiB.";
            Assert.That(director.StatusMessage, Is.EqualTo(notice));
            AssertSystemNoticeVisible(notice);
            AssertExchangeFlowUnchanged(before, savedBytes);
        }

        [UnityTest]
        public IEnumerator SystemNotices_MalformedImportKeepsItsRejectionVisibleWithoutChangingTheReceipt()
        {
            yield return InstallSystemNoticeExchange();
            var before = director.Snapshot;
            byte[] savedBytes = File.ReadAllBytes(director.SavePath);
            string root = Path.GetDirectoryName(director.SavePath);
            string path = Path.Combine(root, "invalid-notice-import.json");
            const string invalid = "{ invalid test-owned import";
            File.WriteAllText(path, invalid);

            director.ImportFile(path);
            yield return null;
            Assert.That(director.StatusMessage, Does.StartWith("Web save was not installed:"));
            AssertSystemNoticeVisible(director.StatusMessage);
            AssertExchangeFlowUnchanged(before, savedBytes);
            Assert.That(File.ReadAllText(path), Is.EqualTo(invalid));
            var originals = Directory.GetFiles(Path.Combine(root, "Imports"), "web-original-*.json");
            Assert.That(originals, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(originals[0]), Is.EqualTo(invalid));
        }

        private static EpisodeState FinishedSystemNoticeSeason()
        {
            // A legal, bounded simulation history avoids hand-building a save's finale fields.
            var engine = new EpisodeEngine(ContentCatalog.Create(17u));
            int guard = 0;
            while (engine.Snapshot.phase != EpisodePhase.Finished && guard++ < 150)
            {
                var result = engine.Apply(NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            var finished = engine.Snapshot;
            Assert.That(finished.phase, Is.EqualTo(EpisodePhase.Finished));
            return finished;
        }

        [UnityTest]
        public IEnumerator SystemNotices_CareerWriteFailureDuringLoadSurvivesTheSuccessfulSeasonInstall()
        {
            HoldTheHouseForTheFixture();
            director.SaveNow();
            Assert.That(director.StatusMessage, Does.Contain("Saved locally."));
            var store = new EpisodeSaveStore(director.SavePath);
            store.Save(FinishedSystemNoticeSeason());
            Assert.That(store.TryLoad(out var finished, out string loadNotice), Is.True, loadNotice);
            byte[] savedBytes = File.ReadAllBytes(director.SavePath);

            string obstruction = Path.Combine(Path.GetDirectoryName(director.SavePath), "career-notice-write-obstruction");
            File.WriteAllText(obstruction, "test-owned file, not a career directory");
            var careerField = typeof(EpisodeDirector).GetField("career", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(careerField, Is.Not.Null);
            var originalCareer = careerField.GetValue(director);
            careerField.SetValue(director, new CareerLedger(obstruction));
            try
            {
                director.LoadNow(); HoldTheHouseForTheFixture();
                yield return null;
                // LoadNow has always replaced the raw message with its load result after Install.
                // The standalone HUD failure still takes priority over that successful load text.
                Assert.That(director.StatusMessage, Is.EqualTo(loadNotice));
                AssertSystemNoticeVisible("The career record could not be updated.");
                Assert.That(ExchangeFlowStatusText(), Does.StartWith("The career record could not be updated."));
                Assert.That(ExchangeFlowStatusText(), Does.Not.Contain(loadNotice));
                AssertEquivalent(finished, director.Snapshot);
                CollectionAssert.AreEqual(savedBytes, File.ReadAllBytes(director.SavePath));
                Assert.That(File.ReadAllText(obstruction), Is.EqualTo("test-owned file, not a career directory"));
                Assert.That(File.Exists(director.CareerPath), Is.False);
            }
            finally { if (director != null) careerField.SetValue(director, originalCareer); }
        }

        [UnityTest]
        public IEnumerator SystemNotices_WorldWarningSurvivesClosingAndSavingAndClearsAfterLoad()
        {
            yield return InstallSystemNoticeExchange();
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "The fixture starts with a valid house world.");
            var before = director.Snapshot;
            var store = new EpisodeSaveStore(director.SavePath);
            byte[] beforeBytes = File.ReadAllBytes(director.SavePath);
            const string reason = "Test-owned world navigation stoppage.";
            const string notice = "Housemate activity is paused: " + reason + " Your episode and saved history remain available.";
            var stop = typeof(EpisodeDirector).GetMethod("StopNpcWorld", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(stop, Is.Not.Null);
            LogAssert.Expect(LogType.Warning, "House world stopped: " + reason);
            stop.Invoke(director, new object[] { reason });

            director.ClosePanels();
            yield return null;
            AssertSystemNoticeVisible(notice);
            AssertExchangeFlowUnchanged(before, beforeBytes);

            director.SaveNow();
            yield return null;
            Assert.That(director.StatusMessage, Does.Contain("Saved locally."), "The explicit episode save still succeeds.");
            AssertSystemNoticeVisible(notice);
            Assert.That(director.NpcAutonomyDiagnostic, Is.EqualTo(reason));
            AssertEquivalent(before, director.Snapshot);
            Assert.That(store.TryLoad(out var saved, out string saveNotice), Is.True, saveNotice);
            AssertEquivalent(before, saved);
            // SaveNow intentionally rotates the envelope and refreshes its timestamp. Its state
            // payload is unchanged, and the previous durable receipt becomes the good backup.
            CollectionAssert.AreEqual(beforeBytes, File.ReadAllBytes(store.BackupPath));
            byte[] savedBytes = File.ReadAllBytes(director.SavePath);

            director.LoadNow(); HoldTheHouseForTheFixture();
            yield return null;
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
            Assert.That(ExchangeFlowStatusText(), Does.Not.Contain("Housemate activity is paused:"));
            Assert.That(ExchangeFlowStatusText(), Does.Not.Contain(reason));
            Assert.That(ActiveRect(EpisodeHud.SaveFeedbackName), Is.Null);
            Assert.That(director.IsConversationExchangePlaying, Is.False);
            AssertExchangeFlowUnchanged(before, savedBytes);
        }

        [UnityTest]
        public IEnumerator SystemNotices_FailedCareerResetIsVisibleAndAReleasedLockClearsItOnRetry()
        {
            // The existing career fixture documents that Windows refuses this move; Unix can
            // rename a held file and would exercise success instead of the required failure.
            if (Application.platform != RuntimePlatform.WindowsEditor)
                Assert.Ignore("A held career file refuses archive moves in the Windows Editor.");
            HoldTheHouseForTheFixture();
            director.SaveNow();
            var before = director.Snapshot;
            byte[] savedBytes = File.ReadAllBytes(director.SavePath);
            var ledger = Ledger();
            Assert.That(ledger.Record(Finished()), Is.True);
            byte[] careerBytes = File.ReadAllBytes(ledger.FilePath);
            string root = Path.GetDirectoryName(ledger.FilePath);
            const string failure = "The career record could not be set aside; it is left as it is.";

            using (new FileStream(ledger.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                director.OpenSettings();
                yield return null;
                ButtonWithCaption(EpisodeDirector.ResetCareerCaption).onClick.Invoke();
                yield return null;
                ButtonWithCaption(EpisodeDirector.ConfirmResetCareerCaption).onClick.Invoke();
                yield return null;
                Assert.That(director.StatusMessage, Is.EqualTo(failure));
                AssertSystemNoticeVisible(failure);
                AssertEquivalent(before, director.Snapshot);
                Assert.That(File.Exists(ledger.FilePath), Is.True);
                Assert.That(Directory.GetFiles(root, "career.json.reset-*"), Is.Empty);
            }
            CollectionAssert.AreEqual(careerBytes, File.ReadAllBytes(ledger.FilePath));

            ButtonWithCaption(EpisodeDirector.ResetCareerCaption).onClick.Invoke();
            yield return null;
            ButtonWithCaption(EpisodeDirector.ConfirmResetCareerCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.StatusMessage, Does.StartWith("Career record set aside as "));
            Assert.That(ExchangeFlowStatusText(), Does.Contain(director.StatusMessage));
            Assert.That(ExchangeFlowStatusText(), Does.Not.Contain(failure));
            Assert.That(ActiveRect(EpisodeHud.SaveFeedbackName), Is.Null);
            AssertExchangeFlowUnchanged(before, savedBytes);
            Assert.That(File.Exists(ledger.FilePath), Is.False);
            var archived = Directory.GetFiles(root, "career.json.reset-*");
            Assert.That(archived, Has.Length.EqualTo(1));
            CollectionAssert.AreEqual(careerBytes, File.ReadAllBytes(archived[0]));
            director.ClosePanels();
        }
    }
}
