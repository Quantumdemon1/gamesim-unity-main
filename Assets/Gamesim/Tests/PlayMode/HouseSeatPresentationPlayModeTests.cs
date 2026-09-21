using System.Collections;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed class HouseSeatPresentationPlayModeTests
    {
        [UnityTest]
        public IEnumerator SeatedBodyRebuildPickingAndInterruptionPreserveTheNavigationRoot()
        {
            var actor=new GameObject("Seated presentation actor");
            actor.transform.position=new Vector3(1000,0,1000);
            var labelObject=new GameObject("Name");labelObject.transform.SetParent(actor.transform,false);
            labelObject.transform.localPosition=Vector3.up*2.2f;var label=labelObject.AddComponent<TextMesh>();
            var npc=actor.AddComponent<HouseNpc>();npc.Configure("seated-test","Seated Test");
            var capsule=actor.AddComponent<CapsuleCollider>();capsule.height=1.9f;capsule.radius=.35f;capsule.center=Vector3.up*.95f;
            var furniture=new GameObject("Test chair");furniture.transform.position=actor.transform.position+Vector3.right;
            var anchor=HouseInteractionAnchor.Create(furniture.transform,"test-seat","Living",0,furniture.transform.position,0,true);
            var provider=new SeatBodyProvider();CharacterBodySource.Register(provider);
            GameObject wall=null;
            try
            {
                var person=ContentCatalog.Create(91).Find(ContentCatalog.PlayerId);
                person.appearance=CharacterAppearance.Preset("player");
                var visual=CharacterPresentation.Attach(actor,person,Color.white);visual.SetReducedMotion(true);
                yield return null;
                var origin=actor.transform.position;var rootCenter=capsule.center;bool owns=true;
                var helper=actor.AddComponent<HouseSeatPresentation>();helper.Begin(anchor,()=>owns);
                yield return null;yield return null;
                Assert.That(helper.Settled,Is.True);
                Assert.That(Vector3.Distance(helper.VisualFeet,anchor.Position),Is.LessThan(.001f));
                Assert.That(Vector3.Distance(label.transform.position,helper.VisualFocus+Vector3.up*.35f),Is.LessThan(.001f));
                var ray=new Ray(helper.VisualFocus+Vector3.forward*3,Vector3.back);
                Physics.SyncTransforms();
                Assert.That(HouseSeatPresentation.TryPickNpc(actor.scene,ray,out var picked),Is.True);
                Assert.That(picked,Is.SameAs(npc));
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=ray.GetPoint(1);
                wall.transform.localScale=new Vector3(2,3,.1f);Physics.SyncTransforms();
                Assert.That(HouseSeatPresentation.TryPickNpc(actor.scene,ray,out _),Is.False,"A visible-body hit cannot select through a wall.");
                wall.SetActive(false);
                var before=visual.VisualRoot;
                person.appearance.dna.Add(new AppearanceValue{id="height",value=.6f});
                CharacterPresentation.Attach(actor,person,Color.white);
                yield return null;yield return null;
                Assert.That(ReferenceEquals(before,visual.VisualRoot),Is.False,"The recipe change actually rebuilt the body.");
                Assert.That(helper.Active && visual.IsSeated,Is.True);
                Assert.That(Vector3.Distance(helper.VisualFeet,anchor.Position),Is.LessThan(.001f),"The new visual acquires exactly one seat offset.");
                Assert.That(actor.transform.position,Is.EqualTo(origin));Assert.That(capsule.center,Is.EqualTo(rootCenter));
                owns=false;yield return null;yield return null;
                Assert.That(helper.Active || visual.IsSeated,Is.False);
                Assert.That(visual.VisualRoot.localPosition,Is.EqualTo(Vector3.zero));
                Assert.That(label.transform.localPosition,Is.EqualTo(Vector3.up*2.2f));
                Assert.That(actor.GetComponentsInChildren<Collider>(),Has.Length.EqualTo(1));
                Assert.That(actor.transform.position,Is.EqualTo(origin));
            }
            finally
            {
                CharacterBodySource.Unregister(provider);
                Object.Destroy(actor);Object.Destroy(furniture);if(wall!=null)Object.Destroy(wall);
            }
            yield return null;
        }

        private sealed class SeatBodyProvider : IModularCharacterBodyProvider
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
