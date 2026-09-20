using System.Collections;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator RepeatedCastExpansionReusesBodiesAndKeepsOneVisualPerActor()
        {
            director.StartSeason(new SeasonBuilder.Choice { HouseSize=12 });
            yield return SettleCast();
            var firstRoots=SceneComponents<HouseNpc>().Select(n=>n.gameObject).ToArray();
            Assert.That(firstRoots,Has.Length.EqualTo(11));
            AssertSingleOwnedCastVisuals();

            director.StartSeason(new SeasonBuilder.Choice { HouseSize=6 });
            yield return SettleCast();
            Assert.That(SceneComponents<HouseNpc>().Count(n=>n.gameObject.activeInHierarchy),Is.EqualTo(5));
            Assert.That(SceneComponents<HouseNpc>(),Has.Length.EqualTo(11),"The bounded pool keeps the six unused bodies for reuse.");

            director.StartSeason(new SeasonBuilder.Choice { HouseSize=12 });
            yield return SettleCast();
            Assert.That(SceneComponents<HouseNpc>().Select(n=>n.gameObject),Is.EquivalentTo(firstRoots),"Re-expanding must not accumulate more avatar roots.");
            AssertSingleOwnedCastVisuals();
            director.SaveNow();
            director.LoadNow();
            yield return SettleCast();
            Assert.That(SceneComponents<HouseNpc>().Select(n=>n.gameObject),Is.EquivalentTo(firstRoots));
            AssertSingleOwnedCastVisuals();
        }

        private static void AssertSingleOwnedCastVisuals()
        {
            foreach(var body in SceneComponents<CharacterPresentation>().Where(p=>p.gameObject.activeInHierarchy))
            {
                var visuals=body.transform.Cast<Transform>().Where(t=>t.name=="Gamesim Character Visual").ToArray();
                Assert.That(visuals,Has.Length.EqualTo(1),body.CharacterId+" must have one owned visual hierarchy.");
                Assert.That(visuals[0],Is.SameAs(body.VisualRoot));
            }
        }
    }
}
