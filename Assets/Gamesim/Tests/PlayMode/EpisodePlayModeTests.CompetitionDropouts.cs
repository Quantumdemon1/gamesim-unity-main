using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Competition drop-outs and the tower show in the built yard. In the ranked endurance game the
    /// housemates at the grip rigs let go around the player, and who let go - before the player and with
    /// them - is exactly the committed standings under the player, from the bottom up; and a ring tower
    /// that is simulated is staged at the yard's stacks, the field finishing in the committed order and
    /// the rings worked to the end on the finish of the one at the stacks. The agreement with the
    /// standings is pinned over many seeds in the Unity-free CompetitionDropoutsTests; these two read
    /// the built yard.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest, Timeout(300000)]
        public IEnumerator CompetitionDropouts_TheFieldLetsGoAroundYouAsTheCommittedStandingsSay()
        {
            yield return EnterInstrumentAttempt("Endurance", ranked: true);
            var before = director.Snapshot;
            var run = ChallengeRun();
            Assert.That(run, Is.Not.Null);
            Assert.That(director.LastCompetitionGameDrops, Is.Not.Null, "A ranked endurance game previews its field.");
            Assert.That(director.LastCompetitionGameDrops.Live, Is.True, "A weekly hold lets its field go while the player holds on.");
            // Hold on as a player does: grip while there is grip, recover when it runs low, for twelve seconds.
            float until = Time.realtimeSinceStartup + 12f;
            while (!run.Finished && Time.realtimeSinceStartup < until)
            {
                run.SetHolding(run.Holding ? run.Meter > 30 : run.Meter > 60);
                yield return null;
            }
            var beforeYou = director.LastCompetitionGameDrops.BeforeYou.ToList();
            var shownOut = beforeYou.Concat(director.LastCompetitionGameDrops.WithYou).ToList();
            foreach (var rig in SceneComponents<CompetitionApparatus>().Where(rig => rig.ActorId != before.playerId && rig.gameObject.activeInHierarchy))
                Assert.That(rig.ProgressText == CompetitionApparatus.ShowOutWord, Is.EqualTo(shownOut.Contains(rig.ActorId)),
                    rig.ActorId + ": a rig reads out exactly when its houseguest has let go.");
            Assert.That(director.Snapshot.randomState, Is.EqualTo(before.randomState), "Letting go draws nothing from the season.");
            if (!run.Finished) run.Finish();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (director.Snapshot.revision == before.revision && Time.realtimeSinceStartup < deadline) yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1), "The attempt commits once.");
            var standings = CommittedStandings(after);
            var under = standings.Skip(standings.IndexOf(after.playerId) + 1).Reverse().ToList();
            var record = director.LastCompetitionGameDrops;
            Debug.Log("[Gamesim competition drop-outs test] before you: " + string.Join(", ", record.BeforeYou) + "; with you: " + string.Join(", ", record.WithYou));
            Assert.That(record.BeforeYou, Is.EqualTo(beforeYou), "Nobody else let go before the player's attempt ended.");
            Assert.That(record.BeforeYou.Concat(record.WithYou).ToList(), Is.EqualTo(under),
                "Everyone shown letting go is ranked under the player, and they let go from the bottom of the standings up.");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator CompetitionDropouts_ASimulatedRingTowerIsShownAtTheStacksInTheCommittedOrder()
        {
            yield return InstallRingTowerAtFirstHoH();
            AskForTheShow();
            yield return BuildTheWorldForTheShow();
            var before = director.Snapshot;
            ButtonWithCaption(EpisodeHud.SimulateCompetitionCaption).onClick.Invoke();
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1), "The result commits at once, as it always has.");
            Assert.That(director.IsCompetitionShowRunning, Is.True, "The simulated tower is staged in the yard.");
            Assert.That(director.CompetitionShowFamilyRunning, Is.EqualTo(CompetitionShowFamily.Tower));
            Assert.That(CompetitionCardPlaying(), Is.False, "The standings card waits for the show.");
            Assert.That(director.CompetitionRingTowerShown, Is.Not.Null, "The stacks stand with their rings.");
            var standings = CommittedStandings(after);
            float deadline = Time.realtimeSinceStartup + 60f;
            while (director.IsCompetitionShowRunning && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(CompetitionCardPlaying(), Is.False, "No card over the show.");
                yield return null;
            }
            Assert.That(director.IsCompetitionShowRunning, Is.False, "The show ends within its bound.");
            var show = director.LastCompetitionShow;
            var record = director.LastCompetitionShowFamily;
            Assert.That(show.CardPlayed, Is.True); Assert.That(show.Skipped, Is.False);
            Assert.That(show.Standings, Is.EqualTo(standings), "The show plays the committed standings.");
            Assert.That(record.Family, Is.EqualTo(CompetitionShowFamily.Tower));
            var staged = standings.Where(id => !show.LeftOut.Contains(id)).ToList();
            Assert.That(record.Staged, Is.EqualTo(staged));
            Assert.That(record.Finished, Is.EqualTo(staged.Take(staged.Count - 1).ToList()), "They finish in the committed order, the winner first.");
            Assert.That(record.TimeCalledOn, Is.EqualTo(staged.Last()), "Time is called on the last place.");
            Assert.That(show.Cheered, Is.EqualTo(standings[0]), "The winner cheers.");
            Assert.That(record.TowerSubject, Is.EqualTo(staged.Contains(after.playerId) ? after.playerId : standings[0]),
                "The tower at the stacks is the player's when they compete.");
            Assert.That(record.TowerMovesShown, Is.GreaterThan(0), "The rings move.");
            Assert.That(record.TowerSolved, Is.EqualTo(record.TowerSubject != staged.Last()), "Solved on its finisher's finish; unsolved when time is called on them.");
            Assert.That(CompetitionCardPlaying(), Is.True, "The standings card follows the show.");
            AssertTowerPutAway("after the tower show");
            Assert.That(director.Snapshot.randomState, Is.EqualTo(after.randomState), "The show draws nothing from the season.");
        }
    }
}
