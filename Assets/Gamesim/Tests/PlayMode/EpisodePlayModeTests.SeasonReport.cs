using System.Collections;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The final stats screen's two missing sections: how the champion got there, and a table of the
    /// whole house you can look at from more than one angle.
    ///
    /// <para>The screen is a record of a finished season, so the thing worth checking hardest is that
    /// none of these controls change it. Sorting a column is a way of looking, not an edit.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private SeasonReport Report() => director.GetComponentInChildren<SeasonReport>(true);

        private Button[] ReportButtons(string caption) =>
            Report().GetComponentsInChildren<Button>(true)
                .Where(button => button.IsActive() && button.IsInteractable()
                    && button.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text == caption))
                .ToArray();

        private string[] ReportLabels() =>
            Report().GetComponentsInChildren<TMP_Text>(true)
                .Where(label => label.isActiveAndEnabled)
                .Select(label => label.text)
                .ToArray();

        /// <summary>A finished season, built rather than played, so the screen has something to show.</summary>
        private static EpisodeState Finished()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 3u);
            var cast = state.contestants.ToList();

            var champion = cast.First(c => !c.isPlayer);
            champion.status = ContestantStatus.Winner;
            champion.hohWins = 3;
            champion.vetoWins = 2;
            champion.timesNominated = 1;
            state.winnerId = champion.id;

            var runnerUp = cast.First(c => !c.isPlayer && c.id != champion.id);
            runnerUp.status = ContestantStatus.RunnerUp;
            state.runnerUpId = runnerUp.id;

            foreach (var other in cast.Where(c => c.id != champion.id && c.id != runnerUp.id))
                other.status = ContestantStatus.Jury;
            // Somebody has to have gone before the jury for that filter to mean anything.
            cast.Last(c => c.id != champion.id && c.id != runnerUp.id).status = ContestantStatus.Evicted;

            // Every juror's ballot, with the reason the engine would have recorded, and one for the
            // runner-up so the tally is not unanimous. The player's own carries the engine's
            // placeholder rather than a reason.
            var reasons = new[]
            {
                "They won when they had to. That is the game.",
                "They kept their word to me when it cost them something.",
                "We were in this together and I am not walking away from that now.",
            };
            int ballot = 0;
            foreach (var juror in cast.Where(c => c.status == ContestantStatus.Jury || c.status == ContestantStatus.Evicted))
            {
                state.votes.Add(new VoteState
                {
                    voterId = juror.id,
                    targetId = ballot == 1 ? runnerUp.id : champion.id,
                    reason = juror.isPlayer ? "Player's jury vote" : reasons[ballot % reasons.Length],
                });
                ballot++;
            }

            state.phase = EpisodePhase.Finished;
            return state;
        }

        private IEnumerator OpenReport()
        {
            Report().Show(Finished(), _ => null, null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Report_SaysHowTheChampionGotThere()
        {
            yield return OpenReport();

            var labels = ReportLabels();
            // Headings are drawn uppercase, so this matches the way a reader would say it rather
            // than the way the screen spells it.
            Assert.That(labels.Any(text => text.IndexOf("road to the end",
                    System.StringComparison.OrdinalIgnoreCase) >= 0), Is.True,
                "The winner's own journey is its own block.");
            Assert.That(labels.Count(text => text == "HOH WINS"), Is.EqualTo(2),
                "One for the champion and one for you — and no more when they are different people.");
        }

        /// <summary>
        /// A player who won it gets one card, not the same card twice. Two identical blocks in a row
        /// read as a bug rather than as emphasis.
        /// </summary>
        [UnityTest]
        public IEnumerator Report_DoesNotRepeatItselfWhenThePlayerWon()
        {
            var state = Finished();
            var champion = state.Find(state.winnerId);
            var you = state.Find(state.playerId);
            champion.status = ContestantStatus.Jury;
            you.status = ContestantStatus.Winner;
            state.winnerId = you.id;

            Report().Show(state, _ => null, null);
            yield return null;

            var labels = ReportLabels();
            Assert.That(labels.Any(text => text.IndexOf("road to the end",
                System.StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            Assert.That(labels.Count(text => text == "HOH WINS"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Report_TheHouseTableCanBeSortedByEachColumn()
        {
            yield return OpenReport();

            foreach (SeasonReport.CastSort by in System.Enum.GetValues(typeof(SeasonReport.CastSort)))
            {
                var control = ReportButtons(SeasonReport.SortCaption(by));
                Assert.That(control, Has.Length.EqualTo(1), SeasonReport.SortCaption(by));
                control[0].onClick.Invoke();
                yield return null;
                Assert.That(Report().IsShowing, Is.True, "Sorting must not close the screen.");
            }
        }

        [UnityTest]
        public IEnumerator Report_FilteringShowsOnlyThatPartOfTheHouse()
        {
            yield return OpenReport();

            ReportButtons(SeasonReport.FilterCaption(SeasonReport.CastFilter.Finalists))[0].onClick.Invoke();
            yield return null;

            var state = Finished();
            var evicted = state.contestants.Single(c => c.status == ContestantStatus.Evicted);
            var champion = state.Find(state.winnerId);

            // Against the table rather than the screen: the standings above it list everybody
            // whatever the table is showing, so a name being on screen says nothing about the filter.
            var table = Report().TableNames;
            Assert.That(table, Does.Contain(champion.name), "The champion is a finalist.");
            Assert.That(table, Does.Not.Contain(evicted.name), "Somebody evicted before jury is not.");
            Assert.That(table, Has.Count.EqualTo(2), "A final two is two people.");
        }

        /// <summary>
        /// The one thing that must not happen: a screen that reads a finished season changing it.
        /// </summary>
        [UnityTest]
        public IEnumerator Report_LookingAtTheSeasonDoesNotChangeIt()
        {
            // The house keeps talking on a real clock while the report is up: this test shows a
            // built season on top of a live one, and a houseguest conversation that resolves
            // mid-test commits a revision of its own. Only the report's controls are under test.
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var before = director.Snapshot;
            yield return OpenReport();

            foreach (SeasonReport.CastSort by in System.Enum.GetValues(typeof(SeasonReport.CastSort)))
            {
                ReportButtons(SeasonReport.SortCaption(by))[0].onClick.Invoke();
                yield return null;
            }
            foreach (SeasonReport.CastFilter which in System.Enum.GetValues(typeof(SeasonReport.CastFilter)))
            {
                ReportButtons(SeasonReport.FilterCaption(which))[0].onClick.Invoke();
                yield return null;
            }

            Assert.That(director.Snapshot.sessionId, Is.EqualTo(before.sessionId));
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision),
                "Not one command should have been committed by reading a report.");
        }

        [UnityTest]
        public IEnumerator Report_ListsEveryJurorWithTheirReason()
        {
            var state = Finished();
            Report().Show(state, _ => null, null);
            yield return null;

            var labels = ReportLabels();
            Assert.That(labels.Any(text => text.IndexOf("how the jury voted",
                    System.StringComparison.OrdinalIgnoreCase) >= 0), Is.True,
                "The jury's ballots are their own section.");
            var ballots = SeasonReport.JuryBallots(state);
            Assert.That(ballots.Count, Is.EqualTo(state.contestants.Count(
                    c => c.status == ContestantStatus.Jury || c.status == ContestantStatus.Evicted)),
                "One ballot per juror, the player included.");
            foreach (var vote in ballots.Where(b => !b.IsPlayer))
            {
                Assert.That(labels, Does.Contain(vote.Reason), vote.Juror + "'s reason is on the screen.");
                Assert.That(labels, Does.Contain("voted for " + vote.Finalist));
            }
            Assert.That(labels, Does.Contain("Your ballot"),
                "The player's own ballot is listed without a made-up reason.");
            var winner = state.Find(state.winnerId);
            Assert.That(labels.Count(text => text == "voted for " + winner.name),
                Is.EqualTo(ballots.Count(b => b.Finalist == winner.name)));
        }

        [UnityTest]
        public IEnumerator Report_ShowsTheCareerCardOnlyWhenThereIsACareer()
        {
            yield return OpenReport();
            Assert.That(ReportLabels(), Does.Not.Contain("SEASONS"),
                "No ledger, no card: a first season has no career to show yet.");

            var record = new CareerRecord();
            record.seasons.Add(new CareerSeason { sessionId = "a", placement = 1, outcome = "Winner", hohWins = 2 });
            record.seasons.Add(new CareerSeason { sessionId = "b", placement = 5, outcome = "Jury", vetoWins = 1 });
            record.seasons.Add(new CareerSeason { sessionId = "c", placement = 2, outcome = "Runner-up" });
            Report().Show(Finished(), _ => null, null, CareerSummary.Of(record));
            yield return null;

            var labels = ReportLabels();
            Assert.That(labels, Does.Contain("SEASONS"));
            Assert.That(labels, Does.Contain("MEDIAN FINISH"));
            Assert.That(labels, Does.Contain("2nd"), "Placements 1, 5 and 2 have a median of 2nd.");
            Assert.That(labels, Does.Contain("1st"), "And a best of 1st.");
            Assert.That(labels.Count(text => text == "COMP WINS"), Is.EqualTo(3),
                "The champion's, yours this season, and yours over the career.");
        }
    }
}
