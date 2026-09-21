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
    /// career line, and the settings reset takes two clicks and deletes nothing.
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

            var ballots = SeasonReport.JuryBallots(finale);
            Assert.That(ballots.Count, Is.EqualTo(finale.contestants.Count(
                    c => c.status == ContestantStatus.Jury || c.status == ContestantStatus.Evicted)),
                "Every juror's ballot carries a reason the finale can show.");
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null;
            var shown = VisibleText();
            Assert.That(shown, Does.Contain("HOW THE JURY VOTED"));
            foreach (var ballot in ballots) Assert.That(shown, Does.Contain(ballot.Line));
            director.ClosePanels();

            yield return ReloadEpisode();
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(Ledger().Load().seasons.Count, Is.EqualTo(1), "A reload is not another finished season.");
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null;
            var again = VisibleText();
            foreach (var ballot in ballots) Assert.That(again, Does.Contain(ballot.Line), "The same lines after a reload.");
            director.ClosePanels();

            director.ShowSeasonReport();
            yield return null;
            var labels = ReportLabels();
            foreach (var ballot in ballots.Where(b => !b.IsPlayer)) Assert.That(labels, Does.Contain(ballot.Reason));
            Assert.That(labels, Does.Contain("SEASONS"), "The report's career card reads the ledger.");
            Assert.That(labels, Does.Contain("1st").Or.Contain(CareerSummary.PlaceWord(record.seasons[0].placement)));
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
    }
}
