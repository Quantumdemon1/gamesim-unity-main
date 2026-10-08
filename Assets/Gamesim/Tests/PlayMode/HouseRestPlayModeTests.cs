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
        public IEnumerator Rest_IndependentSeatsRespectPhysicalChatOccupancyAndCeremonyOwnership()
        {
            var query=CreateNpcRoomQuery();var cast=CreateMeetingCast(4);
            var ids=cast.Select(n=>n.Id).ToArray();var coordinator=CreateMeetingCoordinator(query);
            try
            {
                Assert.That(coordinator.Reconcile("rest-test",cast,ids,ids,out var reason),Is.True,reason);
                yield return WaitForMeetingBinding(coordinator,cast);
                Assert.That(HouseInteractionAnchors.TryFind(query.Scene,"yard-south-chat",0,out var homeA),Is.True);
                Assert.That(HouseInteractionAnchors.TryFind(query.Scene,"yard-south-chat",1,out var homeB),Is.True);
                // Isolate ownership on known complete routes. The authored furniture/contact
                // test covers the real cushion geometry separately; no actor is teleported.
                var a=HouseInteractionAnchor.Create(homeA.transform.parent,HouseFurniture.LoungeAnchor,"Yard",0,homeA.Position,0,true);
                var b=HouseInteractionAnchor.Create(homeB.transform.parent,HouseFurniture.LoungeAnchor,"Yard",1,homeB.Position,0,true);
                var overlap=HouseInteractionAnchor.Create(homeA.transform.parent,HouseFurniture.LoungerAnchor,"Yard",2,homeA.Position+Vector3.right*.1f,0,true);
                Assert.That(coordinator.TryReserveActivity(ids[0],a,out var first,out reason),Is.True,reason);
                Assert.That(coordinator.TryReserveActivity(ids[1],b,out var second,out reason),Is.True,reason);
                Assert.That(coordinator.ActivityAnchorAvailable(overlap),Is.False,"Different IDs cannot lease the same physical cushion/root.");
                Assert.That(coordinator.TryReserveAtVenue("blocked-chat",ids[2],ids[3],"yard-south-chat",out _,out _),Is.False,
                    "A Home venue cannot take cushions held by somebody else.");
                Assert.That(first.Released || second.Released,Is.False);
                coordinator.ReleaseActivity(first);coordinator.ReleaseActivity(second);yield return null;
                Assert.That(coordinator.TryReserveAtVenue("held-chat",ids[0],ids[1],"yard-south-chat",out var meeting,out reason),Is.True,reason);
                Assert.That(coordinator.ActivityAnchorAvailable(a),Is.False,"Independent Rest cannot steal a Home conversation's physical place.");
                Assert.That(coordinator.TryReserveActivity(ids[2],overlap,out _,out _),Is.False);
                coordinator.Release(meeting);yield return null;
                Assert.That(coordinator.TryReserveActivity(ids[2],a,out first,out reason),Is.True,reason);
                Assert.That(coordinator.BeginCeremonyStage(new[]{ids[0],ids[1]},new[]{homeA,homeB},out reason),Is.EqualTo(2),reason);
                Assert.That(first.Released,Is.True,"Ceremony staging yields disposable Rest leases.");
                Assert.That(coordinator.ActivityAnchorAvailable(b),Is.False,"No new Rest lease may compete with the gathered ceremony.");
                coordinator.EndCeremonyStage();yield return null;
                Assert.That(coordinator.ActivityAnchorAvailable(b),Is.True,"The place returns when its formal stage lets go.");
            }
            finally{coordinator.Dispose();}
        }
    }
}
