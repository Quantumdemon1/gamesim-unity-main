using System.Collections;
using System.Linq;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class HouseNpcMotionPlayModeTests
    {
        [UnityTest]
        public IEnumerator Activity_PlayerOwnerWalksWithoutTeleportAndForeignTokensCannotReleaseIt()
        {
            var owner=new object();var foreign=new object();
            var rooms=MotionSceneComponents<HouseRoomMarker>();
            var previous=rooms.Single(room=>room.RoomName=="Kitchen").transform.position;
            var destination=rooms.Single(room=>room.RoomName=="Yard").transform.position;
            player.SetInputEnabled(true);
            Assert.That(player.TryMoveTo(previous),Is.True);
            var previousDestination=player.Agent.destination;
            var before=player.transform.position;
            Assert.That(player.TryBeginActivityMove(owner,destination,out var reason),Is.True,reason);
            Assert.That(Vector3.Distance(player.transform.position,before),Is.LessThan(.001f));
            Assert.That(player.InputEnabled,Is.False);
            Assert.That(player.TryBeginActivityMove(foreign,destination,out _),Is.False);
            Assert.That(player.ReleaseActivityMove(foreign),Is.False);
            Assert.That(player.PauseActivityMove(foreign,true),Is.False);
            Assert.That(player.TryMoveTo(previous),Is.False);
            Assert.That(player.PauseActivityMove(owner,true),Is.True);
            float pauseUntil=Time.realtimeSinceStartup+.3f;
            while(Time.realtimeSinceStartup<pauseUntil)yield return null;
            Assert.That(Vector3.Distance(player.transform.position,before),Is.LessThan(.05f));
            player.Agent.speed=20;player.Agent.acceleration=100;
            Assert.That(player.PauseActivityMove(owner,false),Is.True);
            float deadline=Time.realtimeSinceStartup+12f;
            while(!player.ActivityHasArrived(owner) && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(player.ActivityHasArrived(owner),Is.True,"The owner must finish a real complete route.");
            Assert.That(Vector3.Distance(player.transform.position,destination),Is.LessThan(.55f));
            player.SetInputEnabled(false); // A panel opened after the lease began.
            var reached=player.transform.position;
            Assert.That(player.ReleaseActivityMove(owner),Is.True);
            Assert.That(player.InputEnabled,Is.False,"An explicit later modal lock wins over captured state.");
            Assert.That(player.Agent.isStopped,Is.True);
            Assert.That(Vector3.Distance(player.Agent.destination,previousDestination),Is.LessThan(.05f));
            Assert.That(Vector3.Distance(player.transform.position,reached),Is.LessThan(.001f));
            player.SetInputEnabled(true);
            Assert.That(player.InputEnabled && !player.Agent.isStopped,Is.True);
            Assert.That(player.TryBeginActivityMove(owner,new Vector3(1000,1000,1000),out _),Is.False);
            Assert.That(player.InputEnabled,Is.True,"An unreachable request must not acquire input ownership.");
        }

        [UnityTest]
        public IEnumerator Activity_NpcFurnitureCapacityYieldsToRealMeetingsAndMovedAnchorsReleaseMotion()
        {
            var query=CreateNpcRoomQuery();var cast=CreateMeetingCast(2);
            var ids=cast.Select(npc=>npc.Id).ToArray();var coordinator=CreateMeetingCoordinator(query);
            try
            {
                Assert.That(coordinator.Reconcile("activity-test:1",cast,ids,ids,out var reason),Is.True,reason);
                yield return WaitForMeetingBinding(coordinator,cast);
                Assert.That(HouseInteractionAnchors.TryFind(query.Scene,"yard-south-chat",0,out var a),Is.True);
                Assert.That(HouseInteractionAnchors.TryFind(query.Scene,"yard-south-chat",1,out var b),Is.True);
                var before=cast[0].transform.position;
                Assert.That(coordinator.TryReserveActivity(ids[0],a,out var activity,out reason),Is.True,reason);
                Assert.That(coordinator.ActivityValid(activity),Is.True);
                Assert.That(coordinator.ActivityAnchorAvailable(b),Is.False,"An activity cannot overbook the same conversation venue.");
                Assert.That(Vector3.Distance(cast[0].transform.position,before),Is.LessThan(.001f));
                Assert.That(coordinator.TryReserveActivity(ids[1],b,out _,out _),Is.False);
                Assert.That(coordinator.TryReserveAtVenue("real-conversation",ids[0],ids[1],"living-east-chat",out var meeting,out reason),Is.True,reason);
                Assert.That(activity.Released,Is.True,"A real conversation preempts a disposable visual routine.");
                Assert.That(cast[0].GetComponent<HouseNpcMotion>().LeaseId,Is.EqualTo(meeting.Token));
                coordinator.Release(meeting);yield return null;
                Assert.That(coordinator.TryReserveActivity(ids[0],a,out activity,out reason),Is.True,reason);
                a.transform.position+=Vector3.forward*.2f;
                coordinator.Tick();
                Assert.That(activity.Released,Is.True);
                Assert.That(cast[0].GetComponent<HouseNpcMotion>().LeaseId,Is.Null);
                Assert.That(coordinator.TryGetActivity(ids[0],out _),Is.False);
                Assert.That(coordinator.TryReserveActivity(ids[1],b,out activity,out reason),Is.True,reason);
                coordinator.Dispose();
                Assert.That(activity.Released,Is.True);
                Assert.That(cast.All(npc=>npc.GetComponent<HouseNpcMotion>().LeaseId==null),Is.True);
            }
            finally{coordinator.Dispose();}
        }
    }
}
