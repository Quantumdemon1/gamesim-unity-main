using System.Collections;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class HouseNpcMotionPlayModeTests
    {
        [UnityTest]
        public IEnumerator Witness_SeatedBodyBarrierControlsVisibilityWithoutChangingArrivalProof()
        {
            var query=CreateNpcRoomQuery();var cast=CreateMeetingCast(2);var ids=cast.Select(npc=>npc.Id).ToArray();
            var provider=new WitnessBodyProvider();CharacterBodySource.Register(provider);
            var north=new GameObject("Witness north chair");var south=new GameObject("Witness south chair");
            SceneManager.MoveGameObjectToScene(north,query.Scene);SceneManager.MoveGameObjectToScene(south,query.Scene);
            var a=HouseInteractionAnchor.Create(north.transform,"kitchen-table-chat","Kitchen",0,new Vector3(7.2f,0,-7.22f),180,true,Vector3.back*.55f);
            var b=HouseInteractionAnchor.Create(south.transform,"kitchen-table-chat","Kitchen",1,new Vector3(7.2f,0,-8.78f),0,true,Vector3.back*.55f);
            HouseMeetingCoordinator owner=null;GameObject blocker=null;
            try
            {
                foreach(var npc in cast)
                {
                    var person=ContentCatalog.Create(91).Find(ContentCatalog.PlayerId).Clone();
                    person.id=npc.Id;person.appearance=CharacterAppearance.Preset("player");
                    CharacterPresentation.Attach(npc.gameObject,person,Color.white).SetReducedMotion(true);
                }
                yield return null; // Retire provider-only primitive colliders before binding navigation.
                owner=CreateMeetingCoordinator(query);
                Assert.That(owner.Reconcile("seated-witness:1",cast,ids,ids,out var reason),Is.True,reason);
                yield return WaitForMeetingBinding(owner,cast);
                Assert.That(owner.TryReserveAtVenue("seated-witness",ids[0],ids[1],"kitchen-table-chat",out var lease,out reason),Is.True,reason);
                float deadline=Time.realtimeSinceStartup+25f;
                while(!owner.ValidateArrivedPair(lease,out _) && Time.realtimeSinceStartup<deadline){owner.Tick();yield return null;}
                Assert.That(owner.ValidateArrivedPair(lease,out reason),Is.True,reason);
                WarpMeetingTestPlayer(query,new Vector3(4.6f,0,-8));
                Assert.That(owner.CanWitness(player,lease),Is.False,"A reserved seated venue is not visible-body evidence before presentation is ready.");
                var poseA=cast[0].gameObject.AddComponent<HouseSeatPresentation>();
                var poseB=cast[1].gameObject.AddComponent<HouseSeatPresentation>();
                poseA.Begin(a,()=>owner.ValidateArrivedPair(lease,out _));
                poseB.Begin(b,()=>owner.ValidateArrivedPair(lease,out _));
                deadline=Time.realtimeSinceStartup+4f;
                while((!poseA.TryGetWitnessPoint(out _) || !poseB.TryGetWitnessPoint(out _)) && Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(poseA.TryGetWitnessPoint(out var faceA),Is.True);
                Assert.That(poseB.TryGetWitnessPoint(out var faceB),Is.True);
                Assert.That(Vector3.Distance(poseA.VisualFeet,cast[0].transform.position),Is.GreaterThan(.3f),"The fixture must actually offset the rendered body from its navigation root.");
                Assert.That(owner.CanWitness(player,lease,out var midpoint),Is.True,query.LastFailure);
                Assert.That(Vector3.Distance(midpoint,(faceA+faceB)*.5f),Is.LessThan(.001f),"Caption placement uses the same visible endpoints as the witness proof.");
                var eye=player.transform.position+Vector3.up*1.15f;
                blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.name="Opaque visual-only witness barrier";SceneManager.MoveGameObjectToScene(blocker,query.Scene);
                blocker.transform.localScale=Vector3.one*.12f;
                blocker.transform.position=Vector3.Lerp(eye,faceA,.7f);Physics.SyncTransforms();
                Assert.That(query.HasClearSight(player.transform,cast[0].transform),Is.True,"The old approach ray misses this actual opaque barrier.");
                Assert.That(query.HasClearSight(player.transform,cast[1].transform),Is.True);
                Assert.That(owner.ValidateArrivedPair(lease,out reason),Is.True,reason);
                Assert.That(owner.CanWitness(player,lease,out midpoint),Is.False,"A barrier at the rendered face ray must suppress observation despite clear root rays.");
                Assert.That(midpoint,Is.EqualTo(Vector3.zero),"A rejected observation cannot supply a caption endpoint.");
                blocker.transform.position=Vector3.Lerp(eye,cast[0].transform.position+Vector3.up*1.15f,.7f);Physics.SyncTransforms();
                Assert.That(query.HasClearSight(player.transform,cast[0].transform),Is.False,"This barrier instead blocks only the old navigation-root ray.");
                Assert.That(owner.ValidateArrivedPair(lease,out reason),Is.True,reason);
                Assert.That(owner.CanWitness(player,lease),Is.True,"Clear rendered faces remain visible; arrival and capsule proof have not changed.");
                blocker.SetActive(false);
                a.transform.position+=Vector3.right*.2f;
                Assert.That(poseA.TryGetWitnessPoint(out _),Is.False,"Moved furniture immediately invalidates cached visible endpoints.");
                Assert.That(owner.CanWitness(player,lease),Is.False);
            }
            finally
            {
                owner?.Dispose();CharacterBodySource.Unregister(provider);
                Object.Destroy(north);Object.Destroy(south);if(blocker!=null)Object.Destroy(blocker);
            }
        }

        [UnityTest]
        public IEnumerator Witness_StandingBehaviorIsPreservedAndUnknownExplicitEndpointsFailClosed()
        {
            var query=CreateNpcRoomQuery();var cast=CreateMeetingCast(2);var ids=cast.Select(npc=>npc.Id).ToArray();
            var owner=CreateMeetingCoordinator(query);
            try
            {
                Assert.That(owner.Reconcile("standing-witness:1",cast,ids,ids,out var reason),Is.True,reason);
                yield return WaitForMeetingBinding(owner,cast);
                Assert.That(owner.TryReserveAtVenue("standing-witness",ids[0],ids[1],"living-east-chat",out var lease,out reason),Is.True,reason);
                float deadline=Time.realtimeSinceStartup+20f;
                while(!owner.ValidateArrivedPair(lease,out _) && Time.realtimeSinceStartup<deadline){owner.Tick();yield return null;}
                Assert.That(owner.ValidateArrivedPair(lease,out reason),Is.True,reason);
                WarpMeetingTestPlayer(query,new Vector3(-5.5f,0,-6));
                Assert.That(owner.CanWitness(player,lease,out var midpoint),Is.True,query.LastFailure);
                var original=(cast[0].transform.position+cast[1].transform.position)*.5f+Vector3.up*1.15f;
                Assert.That(Vector3.Distance(midpoint,original),Is.LessThan(.001f));
                var eye=player.transform.position+Vector3.up*1.15f;
                Assert.That(query.HasClearSight(player.transform,cast[0].transform,eye,new Vector3(float.NaN,1,0)),Is.False);
                Assert.That(query.HasClearSight(player.transform,cast[0].transform,eye,cast[0].transform.position+Vector3.up*100),Is.False);
                Assert.That(query.HasClearSight(null,cast[0].transform,eye,midpoint),Is.False);
                Assert.That(owner.CanWitness(player,lease),Is.True,"Invalid explicit queries do not mutate normal standing observation.");
            }
            finally{owner.Dispose();}
        }

        private sealed class WitnessBodyProvider : IModularCharacterBodyProvider
        {
            public ICharacterAppearanceCatalog Catalog=>null;
            public bool TryCreate(in CharacterBodyRequest request,Transform parent,Color badge,out CharacterBody body)
            {
                var root=GameObject.CreatePrimitive(PrimitiveType.Capsule);root.transform.SetParent(parent,false);
                root.transform.localPosition=Vector3.up;Object.Destroy(root.GetComponent<Collider>());
                var head=new GameObject("Head");head.transform.SetParent(root.transform,false);head.transform.localPosition=Vector3.up*.6f;
                body=new CharacterBody(root,null,false);return true;
            }
            public bool TryCreate(string id,Transform parent,Color colour,out CharacterBody body){body=default;return false;}
            public void SetWardrobeColor(in CharacterBody body,Color colour){}
        }
    }
}
