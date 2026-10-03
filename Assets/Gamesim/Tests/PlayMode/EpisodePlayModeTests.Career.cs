using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The career under the director: a finale is recorded once and survives a reload, the jury's
    /// reasons are on the finale panel before and after that reload, the main menu carries the
    /// career line, and the settings reset takes two clicks, deletes nothing, and is there for a
    /// record that could not be opened.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private CareerLedger Ledger() => new CareerLedger(Path.GetDirectoryName(director.SavePath));

        private string VisibleText() => string.Join("\n", director.GetComponentsInChildren<TMP_Text>()
            .Where(label => label.gameObject.activeInHierarchy).Select(label => label.text));

        [UnityTest]
        public IEnumerator Career_AFinaleIsRecordedOnce_AndTheJurysReasonsSurviveAReload()
        {
            Assert.That(File.Exists(director.CareerPath), Is.False, "An isolated root starts with no career.");

            int count = 0;
            while (director.Snapshot.phase != EpisodePhase.Finished && count++ < 150)
            {
                var before = director.Snapshot;
                var result = director.Submit(NextCommand(before));
                Assert.That(result.accepted, Is.True, before.phase + ": " + result.reason);
                yield return null;
            }
            var finale = director.Snapshot;
            Assert.That(finale.phase, Is.EqualTo(EpisodePhase.Finished));

            var record = Ledger().Load();
            Assert.That(record.seasons.Select(s => s.sessionId), Is.EqualTo(new[] { finale.sessionId }),
                "The finale joined the career record, once.");
            var you = finale.Find(finale.playerId);
            Assert.That(record.seasons[0].placement, Is.EqualTo(CareerLedger.Placement(finale, you)));
            Assert.That(record.seasons[0].outcome, Is.EqualTo(CareerLedger.Outcome(you.status)));
            Assert.That(record.seasons[0].winnerName, Is.EqualTo(finale.Find(finale.winnerId).name));
            Assert.That(record.seasons[0].houseSize, Is.EqualTo(finale.contestants.Count));
            Assert.That(director.StatusMessage, Does.Contain("Added to your career record"));
            // The finale's commit says the season is complete (UI-UX-PASS-PLAN decision 13), with both suffixes kept.
            Assert.That(director.StatusMessage, Does.StartWith(EpisodeDirector.SeasonCompleteToast).And.Contain("Saved locally."));

            var ballots = SeasonReport.JuryBallots(finale);
            Assert.That(ballots.Count, Is.EqualTo(finale.contestants.Count(
                    c => c.status == ContestantStatus.Jury || c.status == ContestantStatus.Evicted)),
                "Every juror's ballot carries a reason the finale can show.");
            // The finale's commit reads the jury on its card with the chrome aside; the faces are
            // pressed on the page under it once the card is done, as a player presses them.
            yield return SkipReveals();
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null; yield return null;
            Assert.That(VisibleText(), Does.Contain("HOW THE JURY VOTED"));
            // Each reason on the page, a press on its juror's face at a time (EpisodePlayModeTests.FinalePage.cs).
            yield return AssertEachJurorsReasonOnAPress(ballots, "Before the reload");
            director.ClosePanels();

            yield return ReloadEpisode();
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(Ledger().Load().seasons.Count, Is.EqualTo(1), "A reload is not another finished season.");
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null; yield return null;
            Assert.That(VisibleText(), Does.Contain("HOW THE JURY VOTED"), "The same heading after a reload,");
            yield return AssertEachJurorsReasonOnAPress(ballots, "After the reload");
            director.ClosePanels();

            director.ShowSeasonReport();
            yield return null;
            var labels = ReportLabels();
            foreach (var ballot in ballots.Where(b => !b.IsPlayer)) Assert.That(labels, Does.Contain(ballot.Reason));
            Assert.That(labels, Does.Contain("SEASONS"), "The report's career card reads the ledger.");
            Assert.That(labels, Does.Contain("1st").Or.Contain(CareerSummary.PlaceWord(record.seasons[0].placement)));
            if (UnityEngine.Application.isBatchMode) yield return CaptureFraming("season-report");
        }

        [UnityTest]
        public IEnumerator Career_TheMainMenuCarriesTheLineOnceThereIsARecord()
        {
            director.OpenMainMenu();
            yield return null;
            yield return null;
            Assert.That(Menu().GetComponentsInChildren<TMP_Text>(true).Select(t => t.text)
                .Any(text => text.StartsWith("Your career:")), Is.False, "No seasons, no line.");
            director.CloseMainMenu();
            yield return null;

            var finished = Finished();
            finished.sessionId = "menu-season";
            Assert.That(Ledger().Record(finished), Is.True);

            director.OpenMainMenu();
            yield return null;
            yield return null;
            var line = Menu().GetComponentsInChildren<TMP_Text>(true).Select(t => t.text)
                .FirstOrDefault(text => text.StartsWith("Your career:"));
            Assert.That(line, Is.Not.Null, "One finished season is a career.");
            Assert.That(line, Does.Contain("1 season"));
            Assert.That(CastButtons(MainMenu.NewSeasonCaption), Has.Length.EqualTo(1), "The buttons are unchanged.");
            director.CloseMainMenu();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Career_ResetFromSettingsTakesTwoClicksAndDeletesNothing()
        {
            director.OpenSettings();
            yield return null;
            Assert.That(director.GetComponentsInChildren<UnityEngine.UI.Button>(true).Any(button => button.IsActive()
                    && button.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text == EpisodeDirector.ResetCareerCaption)),
                Is.False, "Nothing to reset is nothing to offer.");
            director.ClosePanels();

            var finished = Finished();
            finished.sessionId = "settings-season";
            var ledger = Ledger();
            Assert.That(ledger.Record(finished), Is.True);

            director.OpenSettings();
            yield return null;
            Assert.That(VisibleText(), Does.Contain("1 season"));
            ButtonWithCaption(EpisodeDirector.ResetCareerCaption).onClick.Invoke();
            yield return null;
            Assert.That(File.Exists(ledger.FilePath), Is.True, "The first click only asks.");
            ButtonWithCaption(EpisodeDirector.KeepCareerCaption).onClick.Invoke();
            yield return null;
            Assert.That(File.Exists(ledger.FilePath), Is.True, "Keeping it keeps it.");
            Assert.That(ledger.Load().seasons.Count, Is.EqualTo(1));

            ButtonWithCaption(EpisodeDirector.ResetCareerCaption).onClick.Invoke();
            yield return null;
            ButtonWithCaption(EpisodeDirector.ConfirmResetCareerCaption).onClick.Invoke();
            yield return null;
            Assert.That(File.Exists(ledger.FilePath), Is.False, "The second click starts fresh.");
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(ledger.FilePath), "career.json.reset-*"),
                Has.Length.EqualTo(1), "The old record is set aside, not deleted.");
            Assert.That(ledger.Load().seasons, Is.Empty);
            Assert.That(director.StatusMessage, Does.Contain("set aside"));
            director.ClosePanels();
        }

        /// <summary>
        /// A record the game could not open loads as no seasons, and the panel used to take that for
        /// an empty career: "No finished seasons yet" and no reset, so a file that never opened again
        /// refused every finale with no way out in the game. Held here sharing nothing, as a backup
        /// client can hold it, the record is offered the reset all the same, under a line that says
        /// it could not be read rather than that it is empty.
        ///
        /// <para>Windows also refuses to move a file held that way, and the reset then says it could
        /// not set the record aside, not that there was none. Mono on Linux and macOS renames through
        /// any hold, so that step runs in the Windows editor only. Once the hold lifts, the reset sets
        /// the record aside whole in every editor.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator Career_ARecordThatCannotBeOpenedCanStillBeSetAsideFromSettings()
        {
            var finished = Finished();
            finished.sessionId = "held-season";
            var ledger = Ledger();
            Assert.That(ledger.Record(finished), Is.True);
            var before = File.ReadAllBytes(ledger.FilePath);
            var folder = Path.GetDirectoryName(ledger.FilePath);

            using (new FileStream(ledger.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                director.OpenSettings();
                yield return null;
                Assert.That(VisibleText(), Does.Contain(EpisodeDirector.UnreadCareerLine), "The record is there and could not be read,");
                Assert.That(VisibleText(), Does.Not.Contain("No finished seasons yet"), "which is not an empty career.");
                ButtonWithCaption(EpisodeDirector.ResetCareerCaption).onClick.Invoke();
                yield return null;
                if (UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WindowsEditor)
                {
                    ButtonWithCaption(EpisodeDirector.ConfirmResetCareerCaption).onClick.Invoke();
                    yield return null;
                    Assert.That(director.StatusMessage, Does.Contain("could not be set aside"), "The move was refused, and the player is told so.");
                    Assert.That(File.Exists(ledger.FilePath), Is.True, "The record is left as it is.");
                    Assert.That(Directory.GetFiles(folder, "career.json.reset-*"), Is.Empty);
                    ButtonWithCaption(EpisodeDirector.ResetCareerCaption).onClick.Invoke();
                    yield return null;
                }
            }
            ButtonWithCaption(EpisodeDirector.ConfirmResetCareerCaption).onClick.Invoke();
            yield return null;
            Assert.That(File.Exists(ledger.FilePath), Is.False, "Once the hold lifts, the reset goes through.");
            var archived = Directory.GetFiles(folder, "career.json.reset-*");
            Assert.That(archived, Has.Length.EqualTo(1), "The record is set aside, not deleted,");
            Assert.That(File.ReadAllBytes(archived[0]), Is.EqualTo(before), "and whole.");
            Assert.That(director.StatusMessage, Does.Contain("set aside as"));
            director.ClosePanels();
        }
    }
}
