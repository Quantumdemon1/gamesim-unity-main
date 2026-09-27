using System.Collections;
using System.Linq;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The house mills about between its ceremonies (playtest, 2026-09-27). Outside free time the
        /// house was paused and the cast stood wherever the last thing had left them. With nothing on
        /// screen somebody sets off within a few seconds and walks somewhere; a panel stops everybody
        /// where they stand.
        /// </summary>
        [UnityTest]
        public IEnumerator Wander_TheHouseMillsAboutBetweenItsCeremoniesAndStandsStillForAPanel()
        {
            yield return InstallStrategySeason(44, state => AtVetoMeeting(state, false));
            // A fixture installed past the season's first social phase has no world of its own.
            director.BuildNpcWorldForDiagnostics();
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
            yield return null;
            var bodies = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy).ToList();
            var start = bodies.ToDictionary(npc => npc.Id, npc => npc.transform.position);
            var house = NpcRead<HouseMeetingCoordinator>("npcMeetings");
            Assert.That(house, Is.Not.Null, "The house has its world.");

            HouseNpc walked = null;
            float until = Time.realtimeSinceStartup + 15f;
            while (walked == null && Time.realtimeSinceStartup < until)
            {
                walked = bodies.FirstOrDefault(npc => FlatDistance(npc.transform.position, start[npc.Id]) > 1f);
                yield return null;
            }
            Assert.That(house.WanderingCount, Is.GreaterThan(0), "Somebody is up and about between the ceremonies.");
            Assert.That(walked, Is.Not.Null, "and has walked somewhere.");

            director.OpenJournal();
            yield return null;
            Assert.That(house.WanderingCount, Is.Zero, "A panel stops everybody where they are.");
            var held = bodies.ToDictionary(npc => npc.Id, npc => npc.transform.position);
            float settled = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < settled) yield return null;
            var drifted = bodies.FirstOrDefault(npc => FlatDistance(npc.transform.position, held[npc.Id]) > .3f);
            Assert.That(drifted, Is.Null, "and they stay where they stopped" + (drifted != null ? ": " + drifted.Id + " went on." : "."));
            director.ClosePanels();
            yield return null;
        }

        private static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }
    }
}
