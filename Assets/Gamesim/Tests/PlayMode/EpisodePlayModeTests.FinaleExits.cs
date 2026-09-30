using System.Collections;
using System.Collections.Generic;
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
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Notes),
                "The panel's 'Review the season' opens the notebook, on your notes.");

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
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Notes),
                "The report's 'Review the season' opens the notebook, on your notes.");

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

        /// <summary>The finale's ways on, as its panel and its keyboard ring carry them: each once.</summary>
        private static readonly string[] FinaleWaysOn =
        {
            SeasonReportRowCaption, SeasonReport.NewSeasonCaption, SeasonReport.ReviewCaption, SeasonReport.MainMenuCaption,
            EpisodeDirector.WatchFinaleReplayCaption, EpisodeDirector.JuryQuestionsCaption,
        };

        /// <summary>How many live controls on the finale's panel carry these words.</summary>
        private int FinaleControlCount(string caption)
        {
            var panel = ActiveRect(ModalRoot);
            Assert.That(panel, Is.Not.Null, "The finale's panel is open.");
            return panel.GetComponentsInChildren<Button>().Count(control => control.IsActive() && control.IsInteractable()
                && control.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text == caption));
        }

        /// <summary>The words of the finale panel's labels carrying these names.</summary>
        private string[] FinalePanelLines(params string[] names) => ActiveRect(ModalRoot).GetComponentsInChildren<TMP_Text>()
            .Where(label => label.gameObject.activeInHierarchy && names.Contains(label.name)).Select(label => label.text).ToArray();

        /// <summary>
        /// The panel's replay reads the jury again from the committed ballots, in the season's own
        /// deal and with the chrome stepped aside as on the night; it commits nothing, and the panel
        /// is back when the card is done (MOCKUP-PASS-PLAN M4).
        /// </summary>
        [UnityTest]
        public IEnumerator Finale_WatchFinaleReplayReadsTheJuryAgainAndGivesThePanelBack()
        {
            yield return InstallFinishedSeason();
            director.SuspendNpcAutonomyForDiagnostics();
            var finale = director.Snapshot;
            yield return OpenFinalePanel();
            foreach (var caption in FinaleWaysOn)
                Assert.That(FinaleControlCount(caption), Is.EqualTo(1), "The finale's panel offers '" + caption + "' once.");
            var reveal = SceneComponents<JuryReveal>().Single();
            Assert.That(reveal.IsPlaying, Is.False, "A finished season loads with nothing playing.");

            FinaleControl(EpisodeDirector.WatchFinaleReplayCaption).onClick.Invoke();
            yield return Frames(2);
            Assert.That(reveal.IsPlaying, Is.True, "The jury is read again,");
            Assert.That(Hud.IsHeldForReveal, Is.True, "with the chrome stepped aside, as it was on the night,");
            var expected = EpisodeDirector.JuryOrder(SeasonReport.JuryBallots(finale).Select(ballot => ballot.JurorId), finale.seed)
                .Select(id => finale.Find(id)).Select(juror => juror.isPlayer ? "You" : juror.name.Split(' ')[0]).ToArray();
            Assert.That(reveal.GetComponentsInChildren<TMP_Text>(true).Where(label => label.name == "Juror").Select(label => label.text),
                Is.EqualTo(expected), "in the season's own deal.");

            yield return SkipReveals();
            Assert.That(director.Snapshot.revision, Is.EqualTo(finale.revision), "A replay commits nothing.");
            Assert.That(director.IsPhasePanelOpen, Is.True, "The finale's panel is back when the card is done,");
            Assert.That(FinaleControlCount(EpisodeDirector.WatchFinaleReplayCaption), Is.EqualTo(1), "with the replay on it once, to watch again.");
            director.ClosePanels();
        }

        /// <summary>
        /// The jury's questions, folded under the panel's ways on. Open, they read what each juror
        /// asked, both finalists' answers and each ballot's reason in full, from the record - and
        /// nothing a question was scored by: not the answer the player passed over, and not whether
        /// an answer landed (MOCKUP-PASS-PLAN M4).
        /// </summary>
        [UnityTest]
        public IEnumerator Finale_TheJurysQuestionsReadTheRecordAndNotTheAnswerKey()
        {
            yield return InstallFinishedSeason();
            director.SuspendNpcAutonomyForDiagnostics();
            var finale = director.Snapshot;
            var exchanges = finale.juryExchanges.Where(x => x.completed && !string.IsNullOrEmpty(x.question)).ToList();
            Assert.That(exchanges, Is.Not.Empty, "The finale put its questions.");
            yield return OpenFinalePanel();
            Assert.That(FinaleControlCount(EpisodeDirector.JuryQuestionsCaption), Is.EqualTo(1), "The finale's panel offers the jury's questions once,");
            Assert.That(FinalePanelLines(EpisodeDirector.JuryQuestionsQuestionName), Is.Empty, "folded until asked for.");

            FinaleControl(EpisodeDirector.JuryQuestionsCaption).onClick.Invoke();
            yield return Frames(2);
            Assert.That(FinaleControlCount(EpisodeDirector.JuryQuestionsCaption), Is.EqualTo(1), "Open, it is still one control.");
            var questions = FinalePanelLines(EpisodeDirector.JuryQuestionsQuestionName);
            var answers = FinalePanelLines(EpisodeDirector.JuryQuestionsAnswerName);
            var reasons = FinalePanelLines(EpisodeDirector.JuryQuestionsReasonName);
            string Quoted(string words) => "\u201C" + words + "\u201D";
            foreach (var exchange in exchanges)
            {
                Assert.That(questions.Any(line => line.EndsWith(Quoted(exchange.question))), Is.True,
                    "Each question as it was put: " + exchange.question + " (shown: " + string.Join(" | ", questions) + ")");
                if (!string.IsNullOrEmpty(exchange.answer))
                    Assert.That(answers.Any(line => line.EndsWith(Quoted(exchange.answer))), Is.True, "and the answer it was given: " + exchange.answer);
                if (!string.IsNullOrEmpty(exchange.opponentAnswer))
                    Assert.That(answers.Any(line => line.EndsWith(Quoted(exchange.opponentAnswer))), Is.True,
                        "and the other finalist's: " + exchange.opponentAnswer);
            }
            var ballots = SeasonReport.JuryBallots(finale);
            Assert.That(reasons.Length, Is.EqualTo(ballots.Count), "A line for each juror's ballot.");
            foreach (var ballot in ballots.Where(ballot => !string.IsNullOrEmpty(ballot.Reason)))
                Assert.That(reasons.Any(line => line.EndsWith(Quoted(ballot.Reason))), Is.True, "Each reason in full: " + ballot.Reason);

            // Nothing a question was scored by. The option the player passed over is not the
            // record, unless somebody said those words; and whether an answer landed is not here.
            var said = new HashSet<string>(exchanges.Select(x => x.answer).Concat(exchanges.Select(x => x.opponentAnswer)).Where(x => x != null));
            var passedOver = exchanges.Select(x => x.answerChoice == "A" ? x.optionB : x.answerChoice == "B" ? x.optionA : null)
                .Where(x => !string.IsNullOrEmpty(x) && !said.Contains(x)).ToList();
            string shown = string.Join("\n", FinalePanelLines(EpisodeDirector.JuryQuestionsJurorName, EpisodeDirector.JuryQuestionsQuestionName,
                EpisodeDirector.JuryQuestionsAnswerName, EpisodeDirector.JuryQuestionsReasonName));
            foreach (var option in passedOver) Assert.That(shown, Does.Not.Contain(option), "The answer passed over is not shown.");
            Assert.That(shown, Does.Not.Contain("impressed").And.Not.Contain("unconvinced"), "Nor whether an answer landed.");
            AssertEveryLabelDraws("The jury's questions, open");

            FinaleControl(EpisodeDirector.JuryQuestionsCaption).onClick.Invoke();
            yield return Frames(2);
            Assert.That(FinalePanelLines(EpisodeDirector.JuryQuestionsQuestionName), Is.Empty, "A second press folds them again.");
            director.ClosePanels();
        }
    }
}
