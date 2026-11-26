using System.Collections;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The Episode button and a decision of the player's own (UI-UX plan Q2; menu review change 7).
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// With the player's own nominations waiting, the Episode button opens the episode screen where
        /// the player stands - in the yard, across the house from the nomination room - with no trip and
        /// no walk left over. With nothing of theirs waiting (a houseguest's nominations) the same press
        /// still takes them to the nomination room and opens nothing by itself, as it always has.
        /// </summary>
        [UnityTest]
        public IEnumerator EpisodeButton_AWaitingDecisionOpensWhereYouStandAndNothingWaitingStillTravels()
        {
            var yard = new Vector3(0f, 0f, 14f);

            yield return InstallNomination(8, true, NominationBlock.Open);
            Assert.That(NominationSteps.Picking(director.Snapshot), Is.True, "The fixture: the player's nominations wait.");
            director.ClosePanels();
            WarpPlayer(yard);
            yield return null;
            var stood = player.transform.position;
            Assert.That(Vector3.Distance(stood, director.StationPosition), Is.GreaterThan(3f), "The fixture: the player stands well away from the screen.");
            ButtonWithCaption(EpisodeHud.EpisodeTravelCaption).onClick.Invoke();
            Assert.That(director.IsPhasePanelOpen, Is.True, "The press opens the waiting nominations at once.");
            Assert.That(director.LastTravel, Is.EqualTo(EpisodeDirector.TravelKind.None), "with no trip to the nomination room.");
            yield return Frames(2);
            Assert.That(director.IsPhasePanelOpen, Is.True, "The screen stays open.");
            Assert.That(director.GetComponentInChildren<EpisodeHud>().CurrentActivityLayout, Is.EqualTo(EpisodeHud.ActivityLayout.Strategy),
                "It is the nomination screen, on the strategy stage.");
            Assert.That(Vector3.Distance(player.transform.position, stood), Is.LessThan(0.5f), "The player is where they stood,");
            Assert.That(player.Agent.hasPath, Is.False, "and is going nowhere.");
            director.ClosePanels();
            yield return null;

            yield return InstallNomination(8, false, NominationBlock.Open);
            director.ClosePanels();
            WarpPlayer(yard);
            yield return null;
            ButtonWithCaption(EpisodeHud.EpisodeTravelCaption).onClick.Invoke();
            Assert.That(director.LastTravel, Is.Not.EqualTo(EpisodeDirector.TravelKind.None), "With nothing of theirs waiting, the press still travels.");
            yield return Frames(2);
            Assert.That(director.IsPhasePanelOpen, Is.False, "and arriving opens nothing by itself.");
        }
    }
}
