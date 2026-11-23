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
    /// <summary>
    /// The show's walk out keeps pace with its wait: from where a fresh season's first Head of
    /// Household competition leaves the cast - all over the house, three of five about 27 m from the
    /// yard's stations, 12.5 s at the house's walk against a 9 s wait - the field is hurried from its
    /// remaining routes to stand at its stations by the walk out's time, so nobody is left out and the
    /// hold begins inside the wait; and once the hold begins, and after the show, everybody walks at
    /// the house's pace again and nobody keeps the run gait. Waits are bounded in real seconds (the
    /// show runs on the unscaled clock); shows are asked for, as a batch run stages none.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>Every houseguest's motion in the house, with their id.</summary>
        private static HouseNpcMotion[] WalkOutMotions()
            => SceneComponents<HouseNpcMotion>().Where(motion => motion != null && motion.isActiveAndEnabled).ToArray();

        /// <summary>
        /// Presses the simulate control at the first Head of Household's briefing and follows the show
        /// of <paramref name="family"/> to its card, watching the walk out: a raised pace on the way,
        /// the house's pace and walk from the hold on, the whole field stationed in time.
        /// </summary>
        private IEnumerator CommitAndFollowTheWalkOut(string kind, CompetitionShowFamily family)
        {
            var before = director.Snapshot;
            var field = EpisodeEngine.CompetitionPlayers(before).Select(actor => actor.id).ToList();
            Assert.That(field.Contains(before.playerId), Is.True, "The fixture puts the player in the field.");
            Assert.That(field.Count, Is.GreaterThanOrEqualTo(5), "A full field: the player and every houseguest.");
            ButtonWithCaption(EpisodeHud.SimulateCompetitionCaption).onClick.Invoke();
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1), kind + ": the result commits at once, as it always has.");
            Assert.That(director.IsCompetitionShowRunning, Is.True, kind + ": the committed result is staged in the yard.");
            Assert.That(director.CompetitionShowFamilyRunning, Is.EqualTo(family));
            float started = Time.realtimeSinceStartup, deadline = started + 60f;
            float fastestOnTheWay = 0f;
            bool holdChecked = false;
            while (director.IsCompetitionShowRunning && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(director.Snapshot.revision, Is.EqualTo(after.revision), "The show commits nothing.");
                if (director.CompetitionShowPhase == EpisodeDirector.CompetitionShowStep.Assembling)
                    foreach (var motion in WalkOutMotions()) fastestOnTheWay = Mathf.Max(fastestOnTheWay, motion.Speed);
                else if (!holdChecked)
                {
                    holdChecked = true;
                    // The first frame of the hold: the walk out is over, and its hurry with it.
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
            Assert.That(record.CardPlayed, Is.True, "The show ends on its card.");
            Assert.That(record.Skipped, Is.False);
            Assert.That(record.Stationed, Is.EquivalentTo(field), "Every competitor of the committed field took a station.");
            Assert.That(holdChecked, Is.True, "The hold began: the winner and the field were at their stations.");
            Assert.That(record.LeftOut, Is.Empty, "Nobody is left out: the whole field reached its stations before the hold.");
            Assert.That(record.AssembledSeconds, Is.LessThan(record.AssemblyWaitSeconds),
                "The field assembled inside the wait for it: " + record.AssembledSeconds.ToString("0.00") + " s.");
            Assert.That(record.Hurried, Is.Not.Empty, "The far end of the house was hurried to the yard.");
            Assert.That(record.Hurried.Contains(before.playerId), Is.False, "The player's own walk keeps the gait its route deserves.");
            Assert.That(record.Hurried, Is.SubsetOf(field));
            Assert.That(record.RanOut, Is.SubsetOf(record.Hurried), "Only a hurried walker runs.");
            Assert.That(fastestOnTheWay, Is.GreaterThan(HouseNpcMotion.DefaultSpeed + .05f), "The hurry reached the walkers' routes.");
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
