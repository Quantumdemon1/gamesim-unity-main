using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>Native approaches and station readiness for a six-person simulated show.</summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>Every houseguest's motion in the house, with their id.</summary>
        private static HouseNpcMotion[] WalkOutMotions()
            => SceneComponents<HouseNpcMotion>().Where(motion => motion != null && motion.isActiveAndEnabled).ToArray();

        /// <summary>
        /// Presses the simulate control at the first Head of Household's briefing and follows the show
        /// of <paramref name="family"/> to its card, with a short walk and the whole field stationed in time.
        /// </summary>
        private IEnumerator CommitAndFollowTheWalkOut(string kind, CompetitionShowFamily family)
        {
            var before = director.Snapshot;
            var field = EpisodeEngine.CompetitionPlayers(before).Select(actor => actor.id).ToList();
            Assert.That(field.Contains(before.playerId), Is.True, "The fixture puts the player in the field.");
            Assert.That(field.Count, Is.GreaterThanOrEqualTo(5), "A full field: the player and every houseguest.");
            var view = cameraRig.ViewCamera;
            float originalAspect = view.aspect;
            ButtonWithCaption(EpisodeHud.SimulateCompetitionCaption).onClick.Invoke();
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1), kind + ": the result commits at once, as it always has.");
            Assert.That(director.IsCompetitionShowRunning, Is.True, kind + ": the committed result is staged in the yard.");
            Assert.That(director.CompetitionShowFamilyRunning, Is.EqualTo(family));
            AssertShortStageWalk(player.Agent, "simulated player's entrance", allowCoveredStart: true);
            Assert.That(view.aspect, Is.EqualTo(originalAspect).Within(.001f), "The stock viewport is unchanged.");
            Assert.That(StageBodyOutsideViewport(view, player.Agent), Is.True,
                "The whole featured player starts outside the stock simulated-show entrance shot.");
            float entranceDistance = cameraRig.DesiredDistance;
            var entrancePosition = player.transform.position;
            var station = player.Agent.destination;
            foreach (var motion in WalkOutMotions())
                if (field.Contains(motion.BoundNpcId))
                    AssertShortStageWalk(motion.GetComponent<UnityEngine.AI.NavMeshAgent>(), motion.BoundNpcId, allowCoveredStart: true);
            float started = Time.realtimeSinceStartup, deadline = started + 60f;
            float fastestOnTheWay = 0f;
            bool holdChecked = false;
            bool enteredView = false;
            while (director.IsCompetitionShowRunning && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(director.Snapshot.revision, Is.EqualTo(after.revision), "The show commits nothing.");
                if (director.CompetitionShowPhase == EpisodeDirector.CompetitionShowStep.Assembling)
                {
                    foreach (var motion in WalkOutMotions()) fastestOnTheWay = Mathf.Max(fastestOnTheWay, motion.Speed);
                    var torso = view.WorldToViewportPoint(player.transform.position + Vector3.up * 1.1f);
                    bool visibleWalk = !director.IsTravelDipShowing && torso.z > view.nearClipPlane
                        && torso.x > .03f && torso.x < .97f && torso.y > .03f && torso.y < .97f
                        && player.Agent.velocity.sqrMagnitude > .04f && Vector3.Distance(player.transform.position, station) > .35f;
                    if (!enteredView && visibleWalk)
                    {
                        Assert.That(view.aspect, Is.EqualTo(originalAspect).Within(.001f));
                        Assert.That(cameraRig.IsTravelling, Is.False);
                        Assert.That(cameraRig.DesiredDistance, Is.EqualTo(entranceDistance).Within(.01f),
                            "The moving player enters the same shot before the camera widens.");
                        Assert.That(player.IsRunning, Is.False);
                        enteredView = true;
                        Debug.LogFormat("[Gamesim short arrivals] simulated {0} walks into stock aspect {1:F2} after {2:F2} s and {3:F2} m",
                            family, originalAspect, Time.realtimeSinceStartup - started,
                            Vector3.Distance(entrancePosition, player.transform.position));
                        if (Application.isBatchMode && family == CompetitionShowFamily.Hold)
                        {
                            using var lens = new CaptureLens(view, Mathf.RoundToInt(900 * originalAspect), 900);
                            var frame = lens.Read();
                            try
                            {
                                string output = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "short-arrival-endurance.png"));
                                System.IO.File.WriteAllBytes(output, frame.EncodeToPNG());
                                Debug.Log("[Gamesim short arrivals] stock approach -> " + output);
                            }
                            finally { Object.Destroy(frame); }
                        }
                    }
                }
                else if (!holdChecked)
                {
                    holdChecked = true;
                    Assert.That(enteredView, Is.True, "The uncovered moving player entered before the hold.");
                    Assert.That(cameraRig.DesiredDistance, Is.EqualTo(EpisodeDirector.CompetitionShotDistance).Within(.01f));
                    Assert.That(cameraRig.DesiredFieldOfView, Is.EqualTo(EpisodeDirector.CompetitionShotFieldOfView).Within(.01f));
                    Assert.That(cameraRig.IsTravelling, Is.False, "The hold cuts back to its authored wide.");
                    // The first frame of the hold: the entrances are clear and the field is on its marks.
                    foreach (var motion in WalkOutMotions())
                        Assert.That(motion.Speed, Is.EqualTo(HouseNpcMotion.DefaultSpeed).Within(1e-3f), motion.BoundNpcId + " walks at the house's pace once the hold begins.");
                    foreach (var npc in SceneComponents<HouseNpc>().Where(npc => npc != null && field.Contains(npc.Id)))
                    {
                        var visual = npc.GetComponent<CharacterPresentation>();
                        if (visual != null) Assert.That(visual.IsRunning, Is.False, npc.Id + " walks once the hold begins.");
                    }
                }
                yield return null;
            }
            Assert.That(director.IsCompetitionShowRunning, Is.False, "The show ends within its bound.");
            var record = director.LastCompetitionShow;
            Assert.That(record, Is.Not.Null);
            Debug.LogFormat("[Gamesim short arrivals] {0} stations ready after {1:F2} s", family, record.AssembledSeconds);
            Assert.That(record.CardPlayed, Is.True, "The show ends on its card.");
            Assert.That(record.Skipped, Is.False);
            Assert.That(record.Stationed, Is.EquivalentTo(field), "Every competitor of the committed field took a station.");
            Assert.That(holdChecked, Is.True, "The hold began: the winner and the field were at their stations.");
            Assert.That(record.LeftOut, Is.Empty, "Nobody is left out: the whole field reached its stations before the hold.");
            Assert.That(record.AssembledSeconds, Is.LessThan(record.AssemblyWaitSeconds),
                "The field assembled inside the wait for it: " + record.AssembledSeconds.ToString("0.00") + " s.");
            Assert.That(record.Hurried, Is.Empty, "The short arrival never hurries anybody across the house.");
            Assert.That(record.Hurried.Contains(before.playerId), Is.False, "The player's own walk keeps the gait its route deserves.");
            Assert.That(record.Hurried, Is.SubsetOf(field));
            Assert.That(record.RanOut, Is.Empty, "The initial arrival never uses the run gait.");
            Assert.That(fastestOnTheWay, Is.EqualTo(HouseNpcMotion.DefaultSpeed).Within(.01f), "The short routes use the house walk.");
            Assert.That(fastestOnTheWay, Is.LessThanOrEqualTo(CompetitionShowPlan.TopWalkOutPace + 1e-3f), "and never past the top pace.");
            // The show is over: everybody at the house's pace, nobody in the run gait.
            foreach (var motion in WalkOutMotions())
                Assert.That(motion.Speed, Is.EqualTo(HouseNpcMotion.DefaultSpeed).Within(1e-3f), motion.BoundNpcId + " walks at the house's pace after the show.");
            foreach (var npc in SceneComponents<HouseNpc>().Where(npc => npc != null))
            {
                var visual = npc.GetComponent<CharacterPresentation>();
                if (visual != null) Assert.That(visual.IsRunning, Is.False, npc.Id + " keeps no run gait after the show.");
            }
            Assert.That(NpcRead<HouseMeetingCoordinator>("npcMeetings").HasCompetitionStage, Is.False, "Every houseguest is let go.");
            Assert.That(director.Snapshot.randomState, Is.EqualTo(after.randomState), "The show draws nothing from the season.");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator CompetitionWalkOut_APodiumShowHasItsWholeFieldAtTheConsolesInsideTheWait()
        {
            yield return InstallShowAtFirstHoH("Mental");
            yield return CommitAndFollowTheWalkOut("Mental", CompetitionShowFamily.Podium);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator CompetitionWalkOut_AnEnduranceHoldHasItsWholeFieldAtTheRigsInsideTheWait()
        {
            yield return InstallShowAtFirstHoH("Endurance");
            yield return CommitAndFollowTheWalkOut("Endurance", CompetitionShowFamily.Hold);
        }
    }
}
