using System.Collections;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator RestPlaces_ThirdLoungerUsesARealRouteAndFloorClickGetsUpInTheOriginalOutfit()
        {
            var suppress=typeof(EpisodeDirector).GetField("npcApproachDiagnosticsSuppressed",BindingFlags.NonPublic|BindingFlags.Instance);
            bool prior=(bool)suppress.GetValue(director);suppress.SetValue(director,true);
            var coordinator=(HouseMeetingCoordinator)typeof(EpisodeDirector).GetField("npcMeetings",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(director);
            var anchor=PlacesFor(HouseFurnitureActivity.Rest).Single(a=>a.VenueId==HouseFurniture.LoungerAnchor && a.Slot==2);
            HouseMeetingLease meeting=null;
            try
            {
                coordinator.ReleaseAll();coordinator.ReleaseActivities();
                float deadline=Time.realtimeSinceStartup+20;
                while(!coordinator.IsReady && Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(coordinator.IsReady,Is.True,coordinator.LastFailure);
                var ids=SceneComponents<HouseNpc>().Where(n=>n.isActiveAndEnabled && n.GetComponent<HouseNpcMotion>()!=null
                    && n.GetComponent<HouseNpcMotion>().LeaseId==null).Select(n=>n.Id).Take(2).ToArray();
                Assert.That(ids,Has.Length.EqualTo(2));
                Assert.That(coordinator.TryReserveAtVenue("third-lounger-home-chat",ids[0],ids[1],HouseFurniture.LoungerAnchor,out meeting,out var reason),Is.True,reason);
                Assert.That(meeting.SpotId,Is.EqualTo(HouseFurniture.LoungerAnchor),"The unchanged Home pair occupies slots zero and one.");
                Assert.That(coordinator.ActivityAnchorAvailable(anchor),Is.True,"The Home chat must leave the physically separate third lounger free.");
                yield return AssertRestPlaceArrivalAndFloorExit(anchor);
                Assert.That(coordinator.TryGetLease(meeting.Token,out var stillHeld),Is.True,"Rest and its floor exit do not release somebody else's conversation.");
                Assert.That(stillHeld,Is.SameAs(meeting));
            }
            finally{if(meeting!=null)coordinator.Release(meeting);suppress.SetValue(director,prior);}
        }

        [UnityTest]
        public IEnumerator RestPlaces_AnIndependentCouchCushionUsesARealRouteAndFloorClickGetsUpInTheOriginalOutfit()
        {
            var coordinator=(HouseMeetingCoordinator)typeof(EpisodeDirector).GetField("npcMeetings",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(director);
            var anchor=PlacesFor(HouseFurnitureActivity.Rest).Where(a=>a.VenueId==HouseFurniture.LoungeAnchor && coordinator.ActivityAnchorAvailable(a))
                .OrderBy(a=>Vector3.Distance(a.Approach,player.transform.position)).First();
            yield return AssertRestPlaceArrivalAndFloorExit(anchor);
        }

        [UnityTest]
        public IEnumerator RestPlaces_BothHoHBenchSeatsUseRealSouthRoutesAndFloorClicksRestoreTheOriginalOutfit()
        {
            yield return InstallTalkingHouse(8,true,state=>state.hohId=state.playerId);
            var anchors=PlacesFor(HouseFurnitureActivity.Rest).Where(a=>a.VenueId==HouseFurniture.HoHBenchAnchor).OrderBy(a=>a.Slot).ToArray();
            Assert.That(anchors,Has.Length.EqualTo(2),"The authored HoH bench and its imported cream cushion must be present for this acceptance case.");
            foreach(var anchor in anchors)
            {
                Assert.That(anchor.RoomId,Is.EqualTo("HoH"));
                Assert.That(Vector3.Dot(anchor.transform.forward,-anchor.transform.parent.forward),Is.GreaterThan(.999f));
                Assert.That(Vector3.Dot(anchor.Approach-anchor.Position,-anchor.transform.parent.forward),Is.GreaterThan(.85f));
                Assert.That(HouseConversationSpots.OnFurniture(anchor),Is.True,"The actual imported seat contact must sit on its authored renderer.");
                yield return AssertRestPlaceArrivalAndFloorExit(anchor);
            }
        }

        [UnityTest]
        public IEnumerator RestPlaces_HoHBenchPreservesTheSuitesExistingPrivateAccessRule()
        {
            yield return InstallTalkingHouse(8,true);
            Assert.That(director.Snapshot.hohId,Is.Not.EqualTo(director.Snapshot.playerId));
            var anchor=PlacesFor(HouseFurnitureActivity.Rest).FirstOrDefault(a=>a.VenueId==HouseFurniture.HoHBenchAnchor);
            Assert.That(anchor,Is.Not.Null,"The actual bench is required; missing imported furniture cannot silently skip this acceptance case.");
            director.StartActivityInHouse(anchor,HouseFurnitureActivity.Rest);
            Assert.That(director.PlayerActivity,Is.Null);
            Assert.That(director.StatusMessage,Does.Contain("Head of Household").And.Contain("furniture"));
            var coordinator=(HouseMeetingCoordinator)typeof(EpisodeDirector).GetField("npcMeetings",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(director);
            Assert.That(coordinator.TryGetActivity("player",out _),Is.False,"Denied access cannot reserve either bench cushion.");
        }

        private IEnumerator AssertRestPlaceArrivalAndFloorExit(HouseInteractionAnchor anchor)
        {
            director.ClosePanels();yield return null;
            var presentation=player.GetComponent<CharacterPresentation>();
            float deadline=Time.realtimeSinceStartup+20;
            while((presentation.IsBodyAssembling || presentation.IsChangingOutfit) && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(presentation.IsBodyAssembling || presentation.IsChangingOutfit,Is.False);
            string originalAppearance=presentation.AppearanceKey;
            string savedAppearance=director.Snapshot.Find(director.Snapshot.playerId).appearance?.ContentKey();
            WarpPlayer(OnFootFrom(anchor.Approach,2f,8f));
            var start=player.transform.position;player.Agent.speed=20;player.Agent.acceleration=100;
            presentation.SetReducedMotion(false);
            director.StartActivityInHouse(anchor,HouseFurnitureActivity.Rest);
            Assert.That(director.PlayerActivity,Is.EqualTo(HouseFurnitureActivity.Rest),director.StatusMessage);
            Assert.That(Vector3.Distance(start,player.transform.position),Is.LessThan(.001f),"Starting Rest acquires a complete path without placing the actor on its cushion.");
            var pose=player.GetComponent<HouseFurniturePose>();
            var seat=player.GetComponent<HouseSeatPresentation>();
            deadline=Time.realtimeSinceStartup+15;
            while(!(pose.IsPerforming && seat!=null && seat.Settled) && Time.realtimeSinceStartup<deadline)
            {yield return null;seat=player.GetComponent<HouseSeatPresentation>();}
            Assert.That(pose.IsPerforming && seat!=null && seat.Settled,Is.True,"The actual Rest route and body must finish.");
            Assert.That(pose.Anchor,Is.SameAs(anchor),"The requested third lounger/cushion is the owned place, not a nearby substitute.");
            Assert.That(seat.Mode,Is.EqualTo(HouseAnchorPose.Seat));
            Assert.That(Vector3.Distance(player.transform.position,anchor.Approach),Is.LessThan(.55f));
            Assert.That(new Vector2(seat.VisualFeet.x-anchor.Position.x,seat.VisualFeet.z-anchor.Position.z).magnitude,Is.LessThan(.2f));
            if(CharacterBodySource.Provider!=null)
            {
                var hips=PlayerAnimator()?.GetBoneTransform(HumanBodyBones.Hips);
                Assert.That(hips,Is.Not.Null,"The provider's seated body must have its actual humanoid contact.");
                Assert.That(new Vector2(hips.position.x-anchor.SeatContact.x,hips.position.z-anchor.SeatContact.z).magnitude,Is.LessThan(.2f));
                Assert.That(hips.position.y,Is.InRange(anchor.SeatContact.y+.035f,anchor.SeatContact.y+.4f),"The hip joint sits just above the cushion; feet may lift the fit onto the floor.");
            }
            var coordinator=(HouseMeetingCoordinator)typeof(EpisodeDirector).GetField("npcMeetings",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(director);
            Assert.That(coordinator.TryGetActivity("player",out var held),Is.True);
            Assert.That(held.Anchor,Is.SameAs(anchor));
            Assert.That(coordinator.ActivityAnchorAvailable(anchor),Is.False,"Its exact cushion remains privately leased.");
            if(anchor.VenueId==HouseFurniture.LoungeAnchor || anchor.VenueId==HouseFurniture.HoHBenchAnchor)
                Assert.That(HouseFurniture.InScene(player.gameObject.scene).Any(a=>a.VenueId==anchor.VenueId && a!=anchor && coordinator.ActivityAnchorAvailable(a)),Is.True,
                    "Another physical cushion remains available independently.");
            if(Application.isBatchMode)
            {
                string name=anchor.VenueId==HouseFurniture.LoungerAnchor?"house-rest-third-lounger"
                    :anchor.VenueId==HouseFurniture.HoHBenchAnchor?"house-rest-hoh-bench-"+anchor.Slot:"house-rest-couch";
                yield return CaptureFraming(name,settle:false,width:1280,height:720);
                if(anchor.VenueId==HouseFurniture.HoHBenchAnchor)
                    yield return CaptureFraming(name+"-1080p",settle:false,width:1920,height:1080);
            }
            cameraRig.ClearSubject();yield return null;
            var rect=cameraRig.ViewCamera.pixelRect;Vector2 screen=default;Vector3 target=default;bool found=false;
            for(int x=2;x<14 && !found;x++)for(int y=3;y<13 && !found;y++)
            {
                var point=new Vector2(rect.xMin+rect.width*x/16f,rect.yMin+rect.height*y/16f);
                if(!IsBareFloorAt(point,out var hit) || Vector3.Distance(hit.point,player.transform.position)<2.5f || !player.TryMeasureRoute(hit.point,out _))continue;
                screen=point;target=hit.point;found=true;
            }
            Assert.That(found,Is.True,"The real house shot must expose reachable floor to click.");
            var mouse=InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});yield return null;
                yield return ClickAt(mouse,screen);
                deadline=Time.realtimeSinceStartup+4;
                while((director.IsPlayerHouseActivityActive || seat.Active || presentation.IsChangingOutfit) && Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(director.IsPlayerHouseActivityActive || pose.Active || seat.Active,Is.False,"The click stood up and released both the body pose and private lease.");
                Assert.That(held.Released,Is.True);
                Assert.That(coordinator.TryGetActivity("player",out _),Is.False);
                Assert.That(player.Agent.hasPath,Is.True,"The same native floor click continues after stand-up.");
                Assert.That(Vector3.Distance(player.Agent.destination,target),Is.LessThan(.8f));
                Assert.That(presentation.AppearanceKey,Is.EqualTo(originalAppearance),"Rest returns the same original outfit through the activity lifecycle.");
                Assert.That(director.Snapshot.Find(director.Snapshot.playerId).appearance?.ContentKey(),Is.EqualTo(savedAppearance),"Presentation never edits the saved wardrobe.");
            }
            finally{InputSystem.RemoveDevice(mouse);}
        }
    }
}
