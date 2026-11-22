using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// "Start a new season" on the finale page closes the page under the cast screen.
    ///
    /// <para>It opened the cast screen with the finale page still open underneath it: the episode
    /// panel drawn and holding the house behind a screen that had replaced it. The page closes now
    /// (FinaleAside); backing out of the cast screen puts it back as it was, the juror's reason
    /// that was open still open; and a season started there leaves no panel, report or finale of
    /// the old season up.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest, Timeout(300000)]
        public IEnumerator Finale_NewSeasonClosesThePageAndBackingOutPutsItBackAsItWas()
        {
            yield return InstallFinishedSeason();
            director.SuspendNpcAutonomyForDiagnostics();
            var finale = director.Snapshot;
            var ballots = SeasonReport.JuryBallots(finale);
            Assert.That(ballots, Is.Not.Empty, "The finished season has a jury.");
            var juror = finale.Find(ballots.First().JurorId);
            Assert.That(juror, Is.Not.Null);

            yield return OpenFinalePanel();
            JurorFace(juror).onClick.Invoke();
            yield return null;
            Assert.That(director.FinalePressedJuror, Is.EqualTo(juror.id), juror.name + "'s reason is open on the page.");

            FinaleControl(SeasonReport.NewSeasonCaption).onClick.Invoke();
            yield return null;
            Assert.That(CastScreenOf().IsShowing, Is.True, "'Start a new season' opens the cast screen.");
            Assert.That(director.IsPhasePanelOpen, Is.False, "and the finale page closes under it.");
            Assert.That(director.IsPanelOpen, Is.False, "No panel stays open behind the cast screen.");
            yield return null;
            Assert.That(director.IsPhasePanelOpen, Is.False, "It stays closed while the cast screen is up.");

            CastScreenOf().Dismiss();
            yield return null;
            Assert.That(director.IsPhasePanelOpen, Is.True, "Backing out puts the finale page back.");
            Assert.That(director.FinalePressedJuror, Is.EqualTo(juror.id), "as it was: the same juror's reason open.");
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(finale.sessionId), "and nothing was built.");

            FinaleControl(SeasonReport.NewSeasonCaption).onClick.Invoke();
            yield return null;
            var start = CastButtons(FrontDoorWords.StartAsNewcomer);
            Assert.That(start, Has.Length.EqualTo(1), "The cast screen offers its one start.");
            start[0].onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(director.Snapshot.sessionId, Is.Not.EqualTo(finale.sessionId), "A new season started.");
            Assert.That(director.Snapshot.phase, Is.Not.EqualTo(EpisodePhase.Finished));
            Assert.That(CastScreenOf().IsShowing, Is.False, "The cast screen has gone,");
            Assert.That(director.IsPanelOpen, Is.False, "and no panel of the old season is open,");
            Assert.That(director.IsSeasonReportOpen, Is.False, "nor its report,");
            Assert.That(director.FinalePressedJuror, Is.Null, "nor anything of its finale page.");
        }
    }
}
