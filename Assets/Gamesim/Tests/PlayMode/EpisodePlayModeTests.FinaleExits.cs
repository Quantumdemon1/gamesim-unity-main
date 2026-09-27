using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The finale's ways on, pressed through the director on a season that was played to its end.
    ///
    /// <para>The report's own tests show it with lambdas of their own, so they pass whatever the
    /// director hands it: were "Start a new season" and "Main menu" swapped in ShowSeasonReport, or
    /// the finale panel's rows wired to the wrong screens, every one of them would still pass.
    /// These press the real controls - the panel's at the episode screen, and the report's that
    /// panel opens - and look at the screen each one puts up.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The finale panel's control that opens the report.</summary>
        private const string SeasonReportRowCaption = "Season report";

        /// <summary>
        /// A season played to its finale by the rules and loaded from its save. The diary fixture
        /// is the one that already plays a legal season to a state of the test's choosing.
        /// </summary>
        private IEnumerator InstallFinishedSeason() =>
            InstallDiaryFixture(state => state.phase == EpisodePhase.Finished, "a finished season");

        /// <summary>
        /// The finale panel's control carrying these words, and a player could press it. Looked for
        /// on the panel itself: a report or menu hidden over the house keeps controls of the same
        /// words.
        /// </summary>
        private Button FinaleControl(string caption)
        {
            var panel = ActiveRect(ModalRoot);
            Assert.That(panel, Is.Not.Null, "The finale's panel is open.");
            var button = panel.GetComponentsInChildren<Button>().FirstOrDefault(control => control.IsActive()
                && control.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text == caption));
            Assert.That(button, Is.Not.Null, "The finale offers '" + caption + "'.");
            ScrollIntoView(button);
            AssertPressable(button, caption);
            return button;
        }

        private CastSelect CastScreenOf() => director.GetComponentInChildren<CastSelect>(true);
        private MainMenu MainMenuOf() => director.GetComponentInChildren<MainMenu>(true);

        [UnityTest]
        public IEnumerator Finale_EachWayOnFromThePanelAndFromTheReportLeadsWhereItSays()
        {
            yield return InstallFinishedSeason();
            director.SuspendNpcAutonomyForDiagnostics();
            var finale = director.Snapshot;
            var winner = finale.Find(finale.winnerId);
            var runnerUp = finale.Find(finale.runnerUpId);
            Assert.That(winner, Is.Not.Null, "The season has a winner.");
            Assert.That(runnerUp, Is.Not.Null, "and a runner-up.");

            // The panel's own rows.
            yield return OpenFinalePanel();
            FinaleControl(SeasonReport.NewSeasonCaption).onClick.Invoke();
            yield return null;
            Assert.That(CastScreenOf().IsShowing, Is.True, "The panel's 'Start a new season' opens the cast screen.");
            Assert.That(director.IsSeasonReportOpen, Is.False, "and not the report.");
            CastScreenOf().Dismiss();
            yield return null;
            Assert.That(director.IsPhasePanelOpen, Is.True, "Backing out of the cast screen lands on the panel it was reached from.");
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(finale.sessionId), "and nothing was built.");

            FinaleControl(SeasonReport.ReviewCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Network),
                "The panel's 'Review the season' opens the notebook.");

            yield return OpenFinalePanel();
            FinaleControl(SeasonReport.MainMenuCaption).onClick.Invoke();
            yield return null;
            Assert.That(MainMenuOf().IsShowing, Is.True, "The panel's 'Main menu' opens the main menu.");
            Assert.That(CastScreenOf().IsShowing, Is.False, "and not the cast screen.");
            MainMenuOf().Hide();

            yield return OpenFinalePanel();
            FinaleControl(SeasonReportRowCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.IsSeasonReportOpen, Is.True, "The panel's 'Season report' opens the report.");
            Assert.That(ReportLabels(), Does.Contain(winner.name + " beat " + runnerUp.name + " in the jury vote."),
                "on the season that was played.");

            // The report's own row, as the director wires it.
            ReportButtons(SeasonReport.NewSeasonCaption)[0].onClick.Invoke();
            yield return null;
            Assert.That(director.IsSeasonReportOpen, Is.False, "Leaving closes the report.");
            Assert.That(CastScreenOf().IsShowing, Is.True, "The report's 'Start a new season' opens the cast screen.");
            Assert.That(MainMenuOf().IsShowing, Is.False, "and not the main menu.");
            CastScreenOf().Dismiss();
            yield return null;
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(finale.sessionId), "Backing out builds nothing.");

            director.ShowSeasonReport();
            yield return null;
            ReportButtons(SeasonReport.MainMenuCaption)[0].onClick.Invoke();
            yield return null;
            Assert.That(director.IsSeasonReportOpen, Is.False, "Leaving closes the report.");
            Assert.That(MainMenuOf().IsShowing, Is.True, "The report's 'Main menu' opens the main menu.");
            Assert.That(CastScreenOf().IsShowing, Is.False, "and not the cast screen.");
            MainMenuOf().Hide();

            yield return OpenFinalePanel();
            director.ShowSeasonReport();
            yield return null;
            ReportButtons(SeasonReport.ReviewCaption)[0].onClick.Invoke();
            yield return null;
            Assert.That(director.IsSeasonReportOpen, Is.False, "Leaving closes the report.");
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Network),
                "The report's 'Review the season' opens the notebook.");

            yield return OpenFinalePanel();
            director.ShowSeasonReport();
            yield return null;
            ReportButtons(SeasonReport.CloseCaption)[0].onClick.Invoke();
            yield return null;
            Assert.That(director.IsSeasonReportOpen, Is.False, "Close closes the report.");
            Assert.That(director.IsPhasePanelOpen, Is.True, "and leaves the finale's panel under it.");
            director.ClosePanels();
        }

        /// <summary>
        /// Once the season is over the objective names the control the player has to use, and that
        /// control leads to the report. It said "Next stop: season report", which is the name of no
        /// rail row, room icon or beacon in the house: the report is a row inside the episode
        /// screen, and only once the screen is open.
        /// </summary>
        [UnityTest]
        public IEnumerator Finale_TheObjectiveNamesTheControlThatLeadsToTheReport()
        {
            yield return InstallFinishedSeason();
            director.ClosePanels();
            yield return null;

            var rail = ControlCarrying("Go to episode screen");
            Assert.That(rail, Is.Not.Null, "The rail offers the episode screen.");
            var objective = ActiveRect("Objective");
            Assert.That(objective, Is.Not.Null, "The objective is on screen.");
            var line = objective.GetComponentsInChildren<TMP_Text>().FirstOrDefault(label => label.text.StartsWith("Next stop:"));
            Assert.That(line, Is.Not.Null, "The objective says where to go next.");
            Assert.That(line.text, Does.Contain("episode screen"),
                "It names the rail's control, 'Go to episode screen': '" + line.text + "'.");
            Assert.That(line.text, Does.Contain("season report"), "and what waits there.");
            line.ForceMeshUpdate();
            Assert.That(line.isTextOverflowing, Is.False, "and it fits its chip: '" + line.text + "'.");

            rail.onClick.Invoke();
            yield return null;
            Assert.That(director.IsPhasePanelOpen, Is.True, "The control it names opens the episode screen.");
            FinaleControl(SeasonReportRowCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.IsSeasonReportOpen, Is.True, "and the report is one press from there.");
            director.ClosePanels();
        }
    }
}
