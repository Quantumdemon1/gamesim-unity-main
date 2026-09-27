using System.Collections;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator HouseActivities_AuthoredCounterMenuWalksGesturesAndFinishesWithoutSeasonEffects()
        {
            Assert.That(HouseInteractionAnchors.TryFind(player.gameObject.scene,HouseFurniture.KitchenAnchor,0,out var counter),Is.True,
                "Run the isolated counter authoring step; runtime must not invent a counter anchor.");
            Assert.That(counter.transform.parent.name,Is.EqualTo("bb_set_kitchenrun"));
            director.OpenHouseActivities();yield return null;
            var before=director.Snapshot;
            var button=director.GetComponentsInChildren<Button>().Single(item=>item.IsActive() && item.IsInteractable()
                && item.name.StartsWith("Prepare a snack"));
            var origin=player.transform.position;
            player.GetComponent<CharacterPresentation>().SetReducedMotion(false);
            button.onClick.Invoke();
            Assert.That(director.IsPlayerHouseActivityActive,Is.True,ActiveDiaryText());
            Assert.That(Vector3.Distance(player.transform.position,origin),Is.LessThan(.001f),"Selecting the counter must schedule movement, not relocate the actor.");
            player.Agent.speed=20;player.Agent.acceleration=100;
            var pose=player.GetComponent<HouseFurniturePose>();
            float deadline=Time.realtimeSinceStartup+15f;
            while((!pose.IsPerforming || !pose.IsGestureApplied) && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(pose.IsPerforming && pose.IsGestureApplied,Is.True,"The actual arrived body must execute the counter gesture.");
            Assert.That(Vector3.Distance(player.transform.position,counter.Approach),Is.LessThan(.55f));
            Assert.That(player.Agent.enabled && player.Agent.isOnNavMesh,Is.True);
            AssertEquivalent(before,director.Snapshot);
            ButtonWithCaption("Finish activity").onClick.Invoke();
            deadline=Time.realtimeSinceStartup+3f;
            while(director.IsPlayerHouseActivityActive && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(director.IsPlayerHouseActivityActive || pose.Active || pose.IsGestureApplied,Is.False);
            Assert.That(player.InputEnabled,Is.False,"The still-open activity menu owns input after finishing.");
            AssertEquivalent(before,director.Snapshot);
            director.ClosePanels();Assert.That(player.InputEnabled,Is.True);
        }

        [UnityTest]
        public IEnumerator DiaryApproach_RemovingSeatPresentationCancelsItsMovementOwner()
        {
            WarpPlayer(director.DiaryPosition+Vector3.right*1.5f);
            Assert.That(director.TryOpenDiary(),Is.True);
            var visit=player.GetComponent<DiarySeatPose>();
            Assert.That(visit.Phase,Is.EqualTo(DiarySeatPose.VisitPhase.Approaching));
            Object.Destroy(player.GetComponent<HouseSeatPresentation>());
            float deadline=Time.realtimeSinceStartup+2f;
            while(director.IsDiaryOpen && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(director.IsDiaryOpen || visit.Active,Is.False,
                "Removing the seat helper during approach must cancel before alignment tries to use it.");
            Assert.That(player.InputEnabled && cameraRig.ControlsEnabled,Is.True);
            Assert.That(player.Agent.enabled && player.Agent.isOnNavMesh && player.Agent.updatePosition,Is.True);
            Assert.That(player.GetComponent<Collider>().enabled,Is.True);
        }

        [UnityTest]
        public IEnumerator HouseActivities_DestroyedPresentationReleasesPlayerMovementOwnership()
        {
            yield return AssertHouseActivityPresentationInterruption(true);
        }

        [UnityTest]
        public IEnumerator HouseActivities_DisabledSeatCannotRestartAndStrandItsOwner()
        {
            yield return AssertHouseActivityPresentationInterruption(false);
        }

        private IEnumerator AssertHouseActivityPresentationInterruption(bool destroyPresentation)
        {
            director.OpenHouseActivities();yield return null;
            var button=director.GetComponentsInChildren<Button>().First(item=>item.IsActive() && item.IsInteractable()
                && item.name.StartsWith("Sit at the dining table"));
            button.onClick.Invoke();
            player.Agent.speed=20;player.Agent.acceleration=100;
            float deadline=Time.realtimeSinceStartup+18f;
            while(player.GetComponent<HouseSeatPresentation>()?.Settled!=true && Time.realtimeSinceStartup<deadline)yield return null;
            var seat=player.GetComponent<HouseSeatPresentation>();
            Assert.That(seat!=null && seat.Settled,Is.True);
            if(destroyPresentation)Object.Destroy(player.GetComponent<HouseFurniturePose>());
            else seat.enabled=false;
            deadline=Time.realtimeSinceStartup+2f;
            while(director.IsPlayerHouseActivityActive && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(director.IsPlayerHouseActivityActive,Is.False,"Removing a presentation component must release its independent movement token.");
            Assert.That(seat.Active,Is.False,"A disabled seat must never be reactivated by another component's Begin call.");
            Assert.That(player.InputEnabled,Is.False,"The surviving modal retains input until explicitly closed.");
            director.ClosePanels();
            Assert.That(player.InputEnabled,Is.True);
            Assert.That(player.Agent.enabled && player.Agent.isOnNavMesh,Is.True);
        }

        [UnityTest]
        public IEnumerator HouseActivities_RealFurnitureMenuWalksSeatsAndFinishesWithoutSeasonEffects()
        {
            director.OpenJournal();yield return null;
            ButtonWithCaption("House activities").onClick.Invoke();yield return null;
            Assert.That(director.IsHouseActivityOpen,Is.True);
            var before=director.Snapshot;
            var options=director.GetComponentsInChildren<Button>().Where(button=>button.IsActive() && button.IsInteractable()
                && button.name.StartsWith("Sit at the dining table")).ToArray();
            Assert.That(options.Length,Is.GreaterThan(0),"The authored dining seats must be reachable through the actual menu.");
            var origin=player.transform.position;
            options[0].onClick.Invoke();
            Assert.That(director.IsPlayerHouseActivityActive,Is.True,ActiveDiaryText());
            Assert.That(player.InputEnabled,Is.False);
            Assert.That(Vector3.Distance(origin,player.transform.position),Is.LessThan(.001f));
            player.Agent.speed=20;player.Agent.acceleration=100;
            var pose=player.GetComponent<HouseFurniturePose>();
            float deadline=Time.realtimeSinceStartup+18f;
            while((pose==null || !pose.IsPerforming || player.GetComponent<HouseSeatPresentation>()?.Settled!=true)
                && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(pose!=null && pose.IsPerforming,Is.True);
            Assert.That(player.GetComponent<HouseSeatPresentation>().Settled,Is.True);
            Assert.That(player.GetComponent<CharacterPresentation>().IsSeated,Is.True);
            Assert.That(player.Agent.isOnNavMesh,Is.True);
            AssertEquivalent(before,director.Snapshot);
            ButtonWithCaption("Finish activity").onClick.Invoke();
            deadline=Time.realtimeSinceStartup+3f;
            while(director.IsPlayerHouseActivityActive && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(director.IsPlayerHouseActivityActive,Is.False);
            Assert.That(pose.Active,Is.False);
            Assert.That(player.InputEnabled,Is.False,"The still-open menu retains input ownership after standing.");
            AssertEquivalent(before,director.Snapshot);
            director.ClosePanels();
            Assert.That(player.InputEnabled,Is.True);
            Assert.That(player.Agent.enabled && player.Agent.isOnNavMesh,Is.True);
        }

        [UnityTest]
        public IEnumerator HouseActivities_DisablingOccupiedFurnitureRetiresItsLeaseWithoutRestoringAClosedOverInputState()
        {
            director.OpenHouseActivities();yield return null;
            var button=director.GetComponentsInChildren<Button>().First(button=>button.IsActive() && button.IsInteractable()
                && button.name.StartsWith("Rest on a lounger"));
            button.onClick.Invoke();yield return null;
            Assert.That(director.IsPlayerHouseActivityActive,Is.True,ActiveDiaryText());
            foreach(var anchor in HouseFurniture.InScene(player.gameObject.scene).Where(anchor=>anchor.VenueId=="yard-lounger-chat").ToArray())
                anchor.gameObject.SetActive(false);
            float deadline=Time.realtimeSinceStartup+2f;
            while(director.IsPlayerHouseActivityActive && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(director.IsPlayerHouseActivityActive,Is.False);
            Assert.That(player.GetComponent<HouseFurniturePose>().Active,Is.False);
            Assert.That(player.InputEnabled,Is.False,"Invalid furniture cannot enable input beneath the activity menu.");
            director.ClosePanels();
            Assert.That(player.InputEnabled,Is.True);
            Assert.That(player.Agent.enabled && player.Agent.isOnNavMesh,Is.True);
        }
    }
}
