using System.Collections;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Faces (MASTER-PLAN §3.B): the director hands every houseguest's mood and stress to their
    /// body on each render, where the face reads them. A UMA body's expression player reads them
    /// every frame (UmaExpressions, tested in the Uma suite); here, what they are handed.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Faces_TheDirectorPushesEveryHouseguestsMoodToTheirBody()
        {
            yield return SettleCast();
            var state = director.Snapshot;
            foreach (var someone in state.contestants.Where(c => !c.isPlayer && c.status == Simulation.ContestantStatus.Active))
            {
                var npc = SceneComponents<HouseNpc>().First(n => n.Id == someone.id);
                var visual = npc.GetComponent<CharacterPresentation>();
                Assert.That(visual, Is.Not.Null, someone.id);
                Assert.That(visual.Mood, Is.EqualTo(someone.mood), someone.id + "'s body wears the simulation's mood.");
                Assert.That(visual.Stress, Is.EqualTo(someone.stressLevel), someone.id + "'s body wears its stress.");
            }
        }
    }
}
