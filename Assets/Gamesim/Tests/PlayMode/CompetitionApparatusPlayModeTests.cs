using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Presentation;
using Gamesim.House;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>The instruments have physical clearance and read an attempt without playing it.</summary>
    public sealed class CompetitionApparatusPlayModeTests
    {
        private GameObject owner;
        private Scene footprintScene;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if(owner!=null)Object.Destroy(owner);
            yield return null;yield return null;
            if(footprintScene.IsValid() && footprintScene.isLoaded)yield return SceneManager.UnloadSceneAsync(footprintScene);
        }

        private CompetitionApparatus Make(CompetitionDefinition definition, bool inactive = false, CompetitionApparatusMeshKit kit = null)
        {
            owner=new GameObject("Apparatus clearance anchor");
            owner.transform.SetPositionAndRotation(new Vector3(4,2,6),Quaternion.Euler(0,37,0));
            owner.SetActive(!inactive);
            return CompetitionApparatus.Create(owner.transform,definition,definition.Category,"entrant",UiTheme.Gold,kit);
        }

        [UnityTest]
        public IEnumerator Apparatus_AllDefinitionsUseDistinctInstrumentsThatClearTheActorAndOwnTheirResources()
        {
            foreach(var definition in CompetitionDefinitions.All)
            {
                var houseFont=UiTheme.Font(UiTheme.Weight.Regular);
                Assert.That(houseFont,Is.Not.Null,"The installed house font is required for native instrument readouts.");
                var originalFontMaterial=houseFont.material;
                float originalCulling=originalFontMaterial.GetFloat("_CullMode");
                var instrument=Make(definition,inactive:true);
                var attempt=new MiniGameRun(CompetitionMiniGames.For(definition.Category),123,4,definition);
                Assert.That(owner.activeInHierarchy,Is.False);
                instrument.Sync(attempt,true,false,false,false);
                var labels=instrument.GetComponentsInChildren<TMP_Text>(true);
                Assert.That(labels.All(label=>label.font==houseFont),
                    Is.True,"Every instrument readout has the installed house font before its first activation.");
                var readoutMaterial=labels[0].fontSharedMaterial;
                Assert.That(readoutMaterial,Is.Not.SameAs(originalFontMaterial),"Front-face culling belongs to the stage, not the shared font.");
                Assert.That(labels.All(label=>label.fontSharedMaterial==readoutMaterial),Is.True,
                    "Both opposing readout planes share the single owned font material.");
                Assert.That(readoutMaterial.GetFloat("_CullMode"),Is.EqualTo((float)UnityEngine.Rendering.CullMode.Back));
                Assert.That(readoutMaterial.shader,Is.SameAs(originalFontMaterial.shader));
                Assert.That(readoutMaterial.GetTexture("_MainTex"),Is.SameAs(originalFontMaterial.GetTexture("_MainTex")));
                owner.SetActive(true);
                foreach(var label in labels)label.ForceMeshUpdate();
                Assert.That(labels.All(label=>label.GetComponent<Renderer>().sharedMaterial==readoutMaterial),Is.True,
                    "Native TMP activation and glyph generation retain the front-only material.");
                Assert.That(readoutMaterial.GetFloat("_CullMode"),Is.EqualTo((float)UnityEngine.Rendering.CullMode.Back),
                    "TMP activation must retain actual back-face culling, not just material identity.");
                Assert.That(houseFont.material,Is.SameAs(originalFontMaterial));
                Assert.That(originalFontMaterial.GetFloat("_CullMode"),Is.EqualTo(originalCulling),"The shared house font stays unchanged.");
                instrument.Sync(attempt,true,false,false,false);
                Assert.That(instrument.ProgressText,Is.EqualTo(CompetitionApparatus.Readout(attempt)),
                    "A reserved instrument can receive progress before its anchor becomes active.");
                var primary=labels.Single(label=>label.name=="Instrument progress");
                var audience=labels.Single(label=>label.name=="Instrument progress audience readout");
                var backings=instrument.GetComponentsInChildren<MeshFilter>().Where(part=>part.name.StartsWith("Progress ")).ToArray();
                Assert.That(backings,Has.Length.EqualTo(2),"Each opposing progress face owns a physical contrast backing.");
                foreach(var backing in backings)
                {
                    var material=backing.GetComponent<Renderer>().sharedMaterial;
                    Color color=material.GetColor("_BaseColor");
                    Assert.That(Mathf.Max(color.r,Mathf.Max(color.g,color.b)),Is.LessThan(.3f),
                        "Progress remains legible against an actual dark material, regardless of saved scenery behind it.");
                    var label=backing.name=="Progress front backing"?primary:audience;
                    var normal=label.transform.TransformDirection(Vector3.back);
                    var bounds=backing.sharedMesh.bounds;
                    for(int corner=0;corner<8;corner++)
                    {
                        var point=new Vector3((corner&1)==0?bounds.min.x:bounds.max.x,(corner&2)==0?bounds.min.y:bounds.max.y,
                            (corner&4)==0?bounds.min.z:bounds.max.z);
                        point=backing.transform.TransformPoint(point);
                        Assert.That(Vector3.Dot(label.transform.position-point,normal),Is.GreaterThan(.001f),
                            "The complete opaque backing is behind its own front-facing text plane.");
                    }
                }
                if(instrument.Instrument==CompetitionApparatus.Family.DiceTray)
                {
                    var pedestal=instrument.GetComponentsInChildren<MeshFilter>().Single(part=>part.name=="Tray pedestal");
                    float near=float.PositiveInfinity,far=float.NegativeInfinity;
                    foreach(var vertex in pedestal.sharedMesh.vertices)
                    {
                        float z=instrument.transform.InverseTransformPoint(pedestal.transform.TransformPoint(vertex)).z;
                        near=Mathf.Min(near,z);far=Mathf.Max(far,z);
                    }
                    Assert.That(instrument.transform.InverseTransformPoint(primary.transform.position).z,Is.LessThan(near-.01f),
                        "Actual front progress cannot sit inside the pedestal's native mesh.");
                    Assert.That(instrument.transform.InverseTransformPoint(audience.transform.position).z,Is.GreaterThan(far+.01f));
                }
                var at=owner.transform.position;var facing=owner.transform.rotation;
                Assert.That(instrument.DefinitionId,Is.EqualTo(definition.Id));
                Assert.That(instrument.Instrument,Is.EqualTo(Expected(definition.Category)),definition.Id);
                Assert.That(instrument.GetComponentsInChildren<Collider>(true),Is.Empty,"Scenery must not alter the reserved route.");
                // A native route stops near its anchor rather than teleporting exactly onto it.
                Vector3 bodyOffset=new Vector3(.13f,0,-.23f);
                if(instrument.Instrument==CompetitionApparatus.Family.GripRig)
                {
                    instrument.FitGrip(1.4f,bodyOffset.z+.04f,.60f,.32f,bodyOffset);
                    instrument.Sync(attempt,true,false,false,false);
                    // Measure the actual fitted scale, bar and ticks, rather than
                    // copying the readout's placement formula into an expectation.
                    var scaleParts=instrument.GetComponentsInChildren<MeshFilter>().Where(part=>part.name=="Grip scale track"
                        || part.name=="Grip remaining" || part.name.StartsWith("Grip scale tick ")).ToArray();
                    Assert.That(scaleParts,Has.Length.EqualTo(7));
                    float nearest=float.PositiveInfinity;
                    foreach(var part in scaleParts)foreach(var vertex in part.sharedMesh.vertices)
                        nearest=Mathf.Min(nearest,instrument.transform.InverseTransformPoint(part.transform.TransformPoint(vertex)).z);
                    float textDepth=instrument.transform.InverseTransformPoint(primary.transform.position).z;
                    Assert.That(textDepth,Is.LessThan(nearest-.01f),
                        "Native fitted Grip progress must remain in front of every scale face, not buried in its track.");
                    var backing=backings.Single(part=>part.name=="Progress front backing");
                    float backingFront=float.PositiveInfinity;
                    foreach(var vertex in backing.sharedMesh.vertices)
                        backingFront=Mathf.Min(backingFront,instrument.transform.InverseTransformPoint(backing.transform.TransformPoint(vertex)).z);
                    Assert.That(backingFront,Is.GreaterThan(textDepth+.001f),"The fitted contrast backing stays behind the glyphs.");
                    Assert.That(instrument.ProgressText,Is.EqualTo(CompetitionApparatus.Readout(attempt)));
                }
                else instrument.FitActorClearance(.40f,bodyOffset);
                var blocks=instrument.GetComponentsInChildren<MeshFilter>(true)
                    .Where(part=>part.sharedMesh!=null && part.sharedMesh.name=="Competition instrument block").ToArray();
                Assert.That(blocks.Length,Is.GreaterThan(7),definition.Id+" has an instrument, not a station marker alone.");
                foreach(var block in blocks)
                {
                    Assert.That(block.sharedMesh.vertices.All(v=>Finite(v.x)&&Finite(v.y)&&Finite(v.z)),Is.True,block.name);
                    if(block.name.Contains("stance rail"))continue;
                    var bounds=block.sharedMesh.bounds;
                    for(int corner=0;corner<8;corner++)
                    {
                        var p=new Vector3((corner&1)==0?bounds.min.x:bounds.max.x,(corner&2)==0?bounds.min.y:bounds.max.y,(corner&4)==0?bounds.min.z:bounds.max.z);
                        p=owner.transform.InverseTransformPoint(block.transform.TransformPoint(p))-bodyOffset;
                        Assert.That(p.z,Is.GreaterThanOrEqualTo(instrument.Instrument==CompetitionApparatus.Family.GripRig?.38f:.499f),
                            definition.Id+" / "+block.name+": raised apparatus clears the body and the approach behind it.");
                    }
                }
                Assert.That(owner.transform.position,Is.EqualTo(at));Assert.That(owner.transform.rotation,Is.EqualTo(facing));
                var mesh=blocks[0].sharedMesh;
                var materials=blocks.Select(part=>part.GetComponent<Renderer>().sharedMaterial).Distinct().ToArray();
                instrument.SetOverlaysVisible(false);
                Assert.That(instrument.OverlayRenderers.All(renderer=>!renderer.enabled),Is.True);
                Assert.That(blocks.All(part=>part.GetComponent<Renderer>().enabled),Is.True,"Physical instruments remain while words are gated under the board.");
                instrument.gameObject.SetActive(false);
                Assert.That(instrument.OverlayRenderers.All(CompetitionApparatus.IsOverlayRenderer),Is.True,"The gate recognizes not-yet-arrived stations too.");
                Object.Destroy(owner);owner=null;yield return null;yield return null;
                Assert.That(mesh==null,Is.True,"A released stage releases its owned mesh.");
                Assert.That(materials.All(material=>material==null),Is.True,"A released stage releases its owned materials.");
                Assert.That(readoutMaterial==null,Is.True,"A released stage releases its owned text material too.");
                Assert.That(originalFontMaterial!=null,Is.True,"The stage never owns the persistent house font material.");
                Assert.That(originalFontMaterial.GetFloat("_CullMode"),Is.EqualTo(originalCulling));
            }
        }

        private static bool Finite(float value)=>!float.IsInfinity(value)&&!float.IsNaN(value);
        private static CompetitionApparatus.Family Expected(string category)=>category=="Mental"?CompetitionApparatus.Family.PairConsole
            :category=="Endurance"?CompetitionApparatus.Family.GripRig:category=="Luck"?CompetitionApparatus.Family.DiceTray
            :category=="Social"?CompetitionApparatus.Family.WordConsole:CompetitionApparatus.Family.Signals;

        [UnityTest]
        public IEnumerator Apparatus_DiceClearanceIncludesRailsAndRollingCornersWithoutMovingTheReservedAnchor()
        {
            var definition=CompetitionDefinitions.All.First(item=>item.Category=="Luck");
            var instrument=Make(definition,inactive:true);
            Vector3 anchorPosition=owner.transform.position;Quaternion anchorRotation=owner.transform.rotation;
            var raised=instrument.GetComponentsInChildren<MeshFilter>(true)
                .Where(part=>part.sharedMesh!=null && part.sharedMesh.name=="Competition instrument block"
                    && !part.name.Contains("stance rail")).ToArray();
            var die=raised.First(part=>part.name=="Die 0");
            foreach(float radius in new[]{.40f,.32f,.55f})
            foreach(var bodyOffset in new[]{new Vector3(.13f,0,-.23f),new Vector3(-.16f,0,.19f)})
            {
                var run=new MiniGameRun(CompetitionMiniGames.Kind.Dice,123,4,definition);
                instrument.Sync(run,true,false,false,false);
                instrument.FitActorClearance(radius,bodyOffset);
                Vector3 fitted=instrument.transform.localPosition;
                Assert.That(fitted.x,Is.EqualTo(bodyOffset.x));
                AssertRaisedClearance(raised,bodyOffset,radius);
                Assert.That(run.Roll(),Is.True);
                for(int step=0;step<5;step++)
                {
                    if(step>0)run.Tick(.1125);
                    Assert.That(run.DieLanded(0),Is.False);
                    string before=Fingerprint(run);
                    // Match the native order: fit first, then read the attempt's new rotation.
                    instrument.FitActorClearance(radius,bodyOffset);
                    instrument.Sync(run,true,false,false,false);
                    Assert.That(instrument.transform.localPosition,Is.EqualTo(fitted),"Rolling must not push the instrument back and forth.");
                    AssertRaisedClearance(raised,bodyOffset,radius);
                    if(step==2)
                    {
                        Assert.That(Quaternion.Angle(Quaternion.identity,die.transform.localRotation),Is.EqualTo(45f).Within(.01f));
                        Assert.That(NearestRaisedVertex(raised,bodyOffset),Is.EqualTo(radius+.10f).Within(.001f),
                            "The rolling corner is the nearest actual solid, without excessive displacement.");
                    }
                    instrument.FitPress(new Vector3(.18f,1.53f,.08f),.62f);
                    AssertRaisedClearance(raised,bodyOffset,radius);
                    Assert.That(Fingerprint(run),Is.EqualTo(before),"Geometry fitting and progress reads leave the attempt unchanged.");
                }
                foreach(bool paused in new[]{true,false})
                {
                    instrument.Sync(run,true,false,paused,!paused);
                    Assert.That(die.transform.localRotation,Is.EqualTo(Quaternion.identity));
                    AssertRaisedClearance(raised,bodyOffset,radius);
                    Assert.That(instrument.transform.localPosition,Is.EqualTo(fitted));
                }
                Assert.That(instrument.Fits(CompetitionStageFootprint.Station(instrument.Instrument,anchorPosition,
                    anchorRotation.eulerAngles.y,radius,1.9f)),Is.True,"The corrected solids still fit the existing station reservation.");
                Assert.That(owner.transform.position,Is.EqualTo(anchorPosition));
                Assert.That(owner.transform.rotation,Is.EqualTo(anchorRotation));
            }
            owner.SetActive(true);
            yield return null;
        }

        private float NearestRaisedVertex(IEnumerable<MeshFilter> raised,Vector3 bodyOffset)
            =>raised.SelectMany(part=>part.sharedMesh.vertices.Select(vertex=>
                owner.transform.InverseTransformPoint(part.transform.TransformPoint(vertex)).z-bodyOffset.z)).Min();

        private void AssertRaisedClearance(IEnumerable<MeshFilter> raised,Vector3 bodyOffset,float radius)
            =>Assert.That(NearestRaisedVertex(raised,bodyOffset),Is.GreaterThanOrEqualTo(radius+.099f),
                "Every actual raised mesh vertex clears the capsule plus the existing ten-centimetre gap.");

        [UnityTest]
        public IEnumerator Apparatus_WholeFootprintRejectsFurnitureBeyondTheActorAndDifferentlyFacingNeighbours()
        {
            owner=new GameObject("Footprint test ownership");
            footprintScene=SceneManager.CreateScene("Competition footprint "+Guid.NewGuid().ToString("N"),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            SceneManager.MoveGameObjectToScene(owner,footprintScene);
            var floor=owner.AddComponent<BoxCollider>();floor.center=new Vector3(0,-.15f,0);floor.size=new Vector3(30,.3f,20);
            var obstacle=new GameObject("Rear-yard furniture beyond body",typeof(BoxCollider));obstacle.transform.SetParent(owner.transform,false);
            obstacle.layer=HouseLayers.Furniture;obstacle.transform.position=new Vector3(0,1.5f,.90f);
            obstacle.GetComponent<BoxCollider>().size=new Vector3(.5f,3,.5f);
            var footprint=CompetitionStageFootprint.Station(CompetitionApparatus.Family.WordConsole,Vector3.zero,0,.35f,1.9f);
            Physics.SyncTransforms();
            Assert.That(Vector3.Distance(obstacle.GetComponent<Collider>().ClosestPoint(Vector3.up),Vector3.up),Is.GreaterThan(.35f),"The solid is beyond the actor capsule, not inside its torso.");
            var hits=new Collider[64];
            Assert.That(footprint.HasStaticClearance(footprintScene.GetPhysicsScene(),floor,hits),Is.False,"Sight ignores Furniture; whole-apparatus placement must still reject it.");
            obstacle.transform.position=new Vector3(9,1.5f,8);Physics.SyncTransforms();
            Assert.That(footprint.HasStaticClearance(footprintScene.GetPhysicsScene(),floor,hits),Is.True);
            Assert.That(footprint.FitsOn(floor.bounds),Is.True);
            var offDeck=CompetitionStageFootprint.Station(CompetitionApparatus.Family.WordConsole,new Vector3(0,0,9.8f),0,.35f,1.9f);
            Assert.That(offDeck.FitsOn(floor.bounds),Is.False,"A capsule centre on the deck does not place the whole console on it.");
            var neighbour=CompetitionStageFootprint.Station(CompetitionApparatus.Family.PairConsole,new Vector3(0,0,1.8f),180,.35f,1.9f);
            Assert.That(Vector3.Distance(Vector3.zero,new Vector3(0,0,1.8f)),Is.GreaterThan(1.26f),"The former actor-spacing check would accept this pair.");
            Assert.That(footprint.Overlaps(neighbour),Is.True,"Opposite approach directions must not put their console backs through one another.");
            var clear=CompetitionStageFootprint.Station(CompetitionApparatus.Family.PairConsole,new Vector3(3,0,1.8f),37,.35f,1.9f);
            Assert.That(footprint.Overlaps(clear),Is.False);
            Assert.That(footprint.Overlaps(CompetitionStageFootprint.Actor(new Vector3(0,0,1.1f),.35f,1.9f)),Is.True,"An audience body reserves space too.");
            // Meets, a world box against the reservation: how the arena clears the yard's drawn scenery
            // (CompetitionStageScenery). This one spans x -.80..80, z -.64..1.34 and y 0..3.
            Assert.That(footprint.Meets(new Bounds(new Vector3(.89f,1,.35f),new Vector3(.2f,1,.2f))),Is.True,"A box a centimetre into its side meets it.");
            Assert.That(footprint.Meets(new Bounds(new Vector3(.91f,1,.35f),new Vector3(.2f,1,.2f))),Is.False,"A box a centimetre off its side does not.");
            Assert.That(footprint.Meets(new Bounds(new Vector3(0,2.95f,.35f),new Vector3(.4f,.1f,.4f))),Is.True,"A box under its top meets it.");
            Assert.That(footprint.Meets(new Bounds(new Vector3(0,3.1f,.35f),new Vector3(.4f,.1f,.4f))),Is.False,"A box over its top is a miss in height, wherever it stands in plan.");
            Assert.That(footprint.Meets(new Bounds(new Vector3(0,-.1f,.35f),new Vector3(.4f,.1f,.4f))),Is.False,"A box under the deck is a miss in height.");
            var turned=CompetitionStageFootprint.Station(CompetitionApparatus.Family.WordConsole,Vector3.zero,45,.35f,1.9f);
            Assert.That(turned.Meets(new Bounds(new Vector3(.707f,1,.707f),new Vector3(.2f,1,.2f))),Is.True,"A box on a turned reservation's own axis meets it.");
            Assert.That(turned.Meets(new Bounds(new Vector3(1.4f,1,1.4f),new Vector3(.2f,1,.2f))),Is.False,
                "A box inside a turned reservation's world bounds but past its front is clear: the test turns with it.");
            AssertGroundedContainmentBoundary();
            yield return null;
        }

        // The saved yard's translated/scaled hierarchy and the native failing target
        // exercise a ground-contact support, rather than relaxing furniture clearance.
        // Identity space tests the exact stored endpoint. The saved hierarchy tests a
        // grounded input inside the same one-millimetre margin: an exact endpoint has
        // no space for its world/local Transform roundtrip's float quantization.
        private void AssertGroundedContainmentBoundary()
        {
            var deck=new GameObject("Saved-yard containment hierarchy");deck.transform.SetParent(owner.transform,false);
            deck.transform.localPosition=new Vector3(0,-.15f,15);deck.transform.localScale=new Vector3(28,.3f,10);
            var stage=new GameObject("Scale-compensated containment stage");stage.transform.SetParent(deck.transform,false);
            stage.transform.localScale=new Vector3(1/28f,1/.3f,1/10f);
            var anchor=new GameObject("Grounded support anchor");anchor.transform.SetParent(stage.transform,false);
            var support=new GameObject("Ground-contact support",typeof(MeshFilter));support.transform.SetParent(anchor.transform,false);
            var mesh=new Mesh{name="Owned containment support box"};support.GetComponent<MeshFilter>().sharedMesh=mesh;
            try
            {
                var vertices=new List<Vector3>();
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                    vertices.Add(new Vector3(x*.5f,y*.5f,z*.5f));
                mesh.vertices=vertices.ToArray();mesh.RecalculateBounds();
                support.transform.localScale=new Vector3(.2f,.25f,.2f);
                // Keep the footprint's centre at exact zero Y, so the endpoint test
                // introduces neither a parent scale nor a world-translation roundtrip.
                var exact=CompetitionStageFootprint.Station(CompetitionApparatus.Family.PairConsole,
                    Vector3.down*((3f+.001f)*.5f),0,.4f,1.9f);
                anchor.transform.SetParent(owner.transform,false);
                anchor.transform.SetPositionAndRotation(exact.Center,exact.Rotation);
                Assert.That(anchor.transform.position.y,Is.Zero);
                var exactPosition=new Vector3(0,-(exact.HalfSize+Vector3.one*.001f).y+.125f,0);
                support.transform.localPosition=exactPosition;
                Assert.That(exact.Contains(support.GetComponent<MeshFilter>()),Is.True,
                    "The stored one-millimetre endpoint is accepted in identity space.");
                support.transform.localPosition=exactPosition+Vector3.down*.002f;
                Assert.That(exact.Contains(support.GetComponent<MeshFilter>()),Is.False,
                    "A real two-millimetre overrun beyond the exact endpoint remains outside.");
                anchor.transform.SetParent(stage.transform,false);
                foreach(float facing in new[]{0f,37f,180f})foreach(float feetY in new[]{0f,.041f,.077f})
                {
                    var reserved=CompetitionStageFootprint.Station(CompetitionApparatus.Family.PairConsole,
                        new Vector3(4.2f,feetY,13.83f),facing,.4f,1.9f);
                    anchor.transform.SetPositionAndRotation(reserved.Center,reserved.Rotation);
                    // Half the documented margin is a fixture input, not a new
                    // containment tolerance. The production margin remains exactly 1 mm.
                    const float groundInset=.0005f;
                    var limit=reserved.HalfSize+Vector3.one*.001f;
                    var lowerPosition=new Vector3(0,-limit.y+.125f+groundInset,0);
                    support.transform.localPosition=lowerPosition;
                    float minimumY=mesh.vertices.Min(vertex=>(Quaternion.Inverse(reserved.Rotation)*
                        (support.transform.TransformPoint(vertex)-reserved.Center)).y);
                    Assert.That(minimumY+limit.y,Is.InRange(groundInset-.00001f,groundInset+.00001f),
                        "The actual transformed support retains the explicit half-millimetre interior input.");
                    Assert.That(reserved.Contains(support.GetComponent<MeshFilter>()),Is.True,
                        "A grounded support stays inside, facing="+facing+" feetY="+feetY+" lowerY="+minimumY.ToString("F9")+" limit="+limit.y.ToString("F9"));
                    support.transform.localPosition=lowerPosition+Vector3.down*.002f;
                    Assert.That(reserved.Contains(support.GetComponent<MeshFilter>()),Is.False,"A genuine vertical overrun is still rejected.");
                    support.transform.localPosition=lowerPosition+Vector3.right*(reserved.HalfSize.x+.01f);
                    Assert.That(reserved.Contains(support.GetComponent<MeshFilter>()),Is.False,"The precision fix does not relax horizontal containment.");
                }
            }
            finally
            {
                support.GetComponent<MeshFilter>().sharedMesh=null;Object.DestroyImmediate(mesh);Object.DestroyImmediate(deck);
            }
        }

        [UnityTest]
        public IEnumerator Apparatus_UnleasedHouseguestBlocksForwardConsoleAndGripVolumesDespiteClearActorCapsules()
        {
            owner=new GameObject("Actor ownership footprint test");
            footprintScene=SceneManager.CreateScene("Competition actor ownership "+Guid.NewGuid().ToString("N"),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            SceneManager.MoveGameObjectToScene(owner,footprintScene);
            var floor=owner.AddComponent<BoxCollider>();floor.center=new Vector3(0,-.15f,0);floor.size=new Vector3(20,.3f,20);
            var participant=new GameObject("Explicit route-owned participant",typeof(HouseNpc),typeof(CapsuleCollider));participant.transform.SetParent(owner.transform,false);
            var ownCapsule=participant.GetComponent<CapsuleCollider>();ownCapsule.radius=.35f;ownCapsule.height=1.9f;ownCapsule.center=Vector3.up*.95f;
            var bystander=new GameObject("Unleased houseguest forward of instrument",typeof(HouseNpc),typeof(CapsuleCollider));bystander.transform.SetParent(owner.transform,false);
            bystander.GetComponent<HouseNpc>().Configure("unleased-forward","Unleased houseguest");bystander.transform.localPosition=Vector3.forward*.95f;
            var otherCapsule=bystander.GetComponent<CapsuleCollider>();otherCapsule.radius=.20f;otherCapsule.height=1.9f;otherCapsule.center=Vector3.up*.95f;
            Assert.That(bystander.GetComponent<HouseNpcMotion>(),Is.Null,"The actual HouseNpc collider has no native route lease.");
            Physics.SyncTransforms();
            Assert.That(Vector3.Distance(otherCapsule.ClosestPoint(Vector3.up*.95f),Vector3.up*.95f),Is.GreaterThan(ownCapsule.radius),"Its body clears the actor capsule while occupying the forward apparatus.");
            var routeOwners=new HashSet<Transform>{participant.transform};var hits=new Collider[64];
            foreach(var family in new[]{CompetitionApparatus.Family.PairConsole,CompetitionApparatus.Family.GripRig})
            {
                var footprint=CompetitionStageFootprint.Station(family,Vector3.zero,0,.35f,1.9f);
                Assert.That(footprint.HasStaticClearance(footprintScene.GetPhysicsScene(),floor,hits,routeOwners),Is.False,family+": an unleased NPC must count as a real obstruction.");
            }
            bystander.transform.localPosition=Vector3.right*5;Physics.SyncTransforms();
            var clear=CompetitionStageFootprint.Station(CompetitionApparatus.Family.PairConsole,Vector3.zero,0,.35f,1.9f);
            Assert.That(clear.HasStaticClearance(footprintScene.GetPhysicsScene(),floor,hits,routeOwners),Is.True,"The native participant's own body may occupy its reserved stance.");
            routeOwners.Clear();
            Assert.That(clear.HasStaticClearance(footprintScene.GetPhysicsScene(),floor,hits,routeOwners),Is.False,"Releasing route ownership immediately restores body occupancy.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Apparatus_WordsHideThePuzzleOnReadyAndPauseAndRestoreTheSamePickedTiles()
        {
            var definition=CompetitionDefinitions.All.First(item=>item.Category=="Social");
            var instrument=Make(definition);var run=new MiniGameRun(CompetitionMiniGames.Kind.Words,123,4,definition);
            var front=instrument.GetComponentsInChildren<TMP_Text>().Where(label=>label.name.StartsWith("Letter ") && !label.name.Contains("audience")).ToArray();
            Action<bool> assertLetters=hidden=>
            {
                foreach(var label in front)
                {
                    int index=int.Parse(label.name.Substring("Letter ".Length));
                    string expected=index>=run.Scrambled.Length?"":hidden?"?":run.Scrambled[index].ToString();
                    Assert.That(label.text,Is.EqualTo(expected));
                    Assert.That(instrument.GetComponentsInChildren<TMP_Text>().Single(copy=>copy.name==label.name+" audience readout").text,Is.EqualTo(expected));
                }
            };
            instrument.Sync(run,false,false,false,false);assertLetters(true);
            instrument.Sync(run,true,false,false,false);assertLetters(false);
            run.TypeLetter(run.Word[0]);Assert.That(run.Spelled.Length,Is.EqualTo(1));
            string before=Fingerprint(run);
            instrument.Sync(run,false,false,true,false);assertLetters(true);
            instrument.SetOverlaysVisible(true);assertLetters(true);
            Assert.That(instrument.ProgressText,Is.EqualTo("Paused"));
            Assert.That(Fingerprint(run),Is.EqualTo(before),"Hiding/restoring readouts neither advances nor changes the puzzle.");
            instrument.Sync(run,true,false,false,false);assertLetters(false);
            Assert.That(Fingerprint(run),Is.EqualTo(before));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Apparatus_MemoryShowsOnlyThePreviewAndCardsThePlayerActuallyRevealed()
        {
            var instrument=Make(CompetitionDefinitions.FirstImpressions);
            var run=new MiniGameRun(CompetitionMiniGames.Kind.Memory,7,4,CompetitionDefinitions.FirstImpressions);
            instrument.Sync(run,false,false,false,false);Assert.That(instrument.VisibleMemoryFaces,Is.Zero);
            instrument.Sync(run,false,true,false,false);Assert.That(instrument.VisibleMemoryFaces,Is.EqualTo(16));
            instrument.Sync(run,false,true,true,false);Assert.That(instrument.VisibleMemoryFaces,Is.Zero,"Pausing the preview must not extend it.");
            instrument.Sync(run,true,false,false,false);Assert.That(instrument.VisibleMemoryFaces,Is.Zero);
            run.Flip(0);instrument.Sync(run,true,false,false,false);Assert.That(instrument.VisibleMemoryFaces,Is.EqualTo(1));
            int pair=Enumerable.Range(1,15).First(index=>run.Faces[index]==run.Faces[0]);
            run.Flip(pair);instrument.Sync(run,true,false,false,false);
            Assert.That(instrument.VisibleMemoryFaces,Is.EqualTo(2));Assert.That(instrument.ProgressText,Is.EqualTo("1 / 8 pairs"));
            int other=Enumerable.Range(1,15).First(index=>index!=pair && run.Faces[index]!=run.Faces[0]);
            int wrong=Enumerable.Range(1,15).First(index=>index!=pair && index!=other && run.Faces[index]!=run.Faces[0] && run.Faces[index]!=run.Faces[other]);
            run.Flip(other);run.Flip(wrong);instrument.Sync(run,true,false,false,false);
            Assert.That(instrument.VisibleMemoryFaces,Is.EqualTo(4));
            run.Tick(MiniGameRun.FlipBackDelay+.05);instrument.Sync(run,true,false,false,false);
            Assert.That(instrument.VisibleMemoryFaces,Is.EqualTo(2),"A wrong pair goes down when its real attempt does.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Apparatus_RepeatedProgressReadsPreserveEverySeededAttemptAndItsOutcome()
        {
            foreach(var definition in CompetitionDefinitions.All)
            {
                var instrument=Make(definition);
                var kind=CompetitionMiniGames.For(definition.Category);
                var shown=new MiniGameRun(kind,123,4,definition);var control=new MiniGameRun(kind,123,4,definition);
                for(int frame=0;frame<330;frame++)
                {
                    Play(shown,frame);Play(control,frame);shown.Tick(.1);control.Tick(.1);
                    string before=Fingerprint(shown);
                    for(int read=0;read<4;read++)instrument.Sync(shown,true,false,false,(read&1)==0);
                    Assert.That(Fingerprint(shown),Is.EqualTo(before),definition.Id+": reading does not consume time, choices or random draws.");
                    Assert.That(Fingerprint(shown),Is.EqualTo(Fingerprint(control)),definition.Id+": the same seed and controls keep the same state.");
                }
                Assert.That(shown.Finished,Is.True,definition.Id);Assert.That(shown.Score,Is.EqualTo(control.Score));
                if(kind==CompetitionMiniGames.Kind.Endurance)Assert.That(instrument.GripFraction,Is.EqualTo((float)(shown.Meter/100)).Within(.0001));
                if(kind==CompetitionMiniGames.Kind.Dice)
                    for(int die=0;die<3;die++)Assert.That(instrument.GetComponentsInChildren<TMP_Text>().Single(label=>label.name=="Die face "+die).text,Is.EqualTo(shown.Face(die).ToString()));
                if(kind==CompetitionMiniGames.Kind.Reaction)Assert.That(instrument.LitSignal,Is.EqualTo(shown.TargetLive?(int)shown.TargetDirection:-1));
                Object.Destroy(owner);owner=null;yield return null;
            }
        }

        private static void Play(MiniGameRun run,int frame)
        {
            if(run.Finished)return;
            switch(run.Kind)
            {
                case CompetitionMiniGames.Kind.Endurance:run.SetHolding(frame%13<8);break;
                case CompetitionMiniGames.Kind.Reaction:if(run.TargetLive && frame%3!=0)run.Tap(run.TargetDirection);break;
                case CompetitionMiniGames.Kind.Memory:
                    if(frame%10==0 && run.FirstFlip<0)
                    {
                        int first=Enumerable.Range(0,16).FirstOrDefault(index=>!run.Matched[index]);
                        if(run.Matched[first])break;
                        int pair=Enumerable.Range(0,16).First(index=>index!=first && run.Faces[index]==run.Faces[first]);run.Flip(first);run.Flip(pair);
                    }
                    break;
                case CompetitionMiniGames.Kind.Dice:if(run.CanRoll && frame%20==0)run.Roll();break;
                case CompetitionMiniGames.Kind.Words:if(frame%10==0)foreach(char letter in run.Word)run.TypeLetter(letter);break;
            }
        }

        [UnityTest]
        public IEnumerator Apparatus_GripProgressStaysAboveItsBraceWithoutChangingTheAttemptOrReservation()
        {
            var definition=CompetitionDefinitions.All.First(item=>item.Category=="Endurance");
            var instrument=Make(definition,inactive:true);
            var labels=instrument.GetComponentsInChildren<TMP_Text>(true).Where(label=>label.name.StartsWith("Instrument progress")).ToArray();
            var backings=instrument.GetComponentsInChildren<MeshFilter>(true).Where(part=>part.name.StartsWith("Progress ")).ToArray();
            var brace=instrument.GetComponentsInChildren<MeshFilter>(true).Single(part=>part.name=="Rig top brace");
            Assert.That(labels,Has.Length.EqualTo(2));Assert.That(backings,Has.Length.EqualTo(2));
            Action check=()=>
            {
                float braceTop=brace.transform.localPosition.y+brace.transform.localScale.y*brace.sharedMesh.bounds.extents.y;
                foreach(var backing in backings)
                {
                    float bottom=backing.transform.localPosition.y-backing.transform.localScale.y*backing.sharedMesh.bounds.extents.y;
                    Assert.That(bottom,Is.GreaterThan(braceTop+.02f),"The actual opaque plate is above the brace and the gripping hands.");
                }
                foreach(var label in labels)Assert.That(label.transform.localPosition.y,Is.EqualTo(backings[0].transform.localPosition.y).Within(.0001f));
            };
            check();
            var run=new MiniGameRun(CompetitionMiniGames.Kind.Endurance,123,4,definition);run.SetHolding(true);run.Tick(.2);
            string before=Fingerprint(run);Vector3 position=owner.transform.position;Quaternion rotation=owner.transform.rotation;
            foreach(float shoulder in new[]{1.25f,1.65f})foreach(float reach in new[]{.44f,.70f})foreach(float radius in new[]{.32f,.40f})
            {
                instrument.FitGrip(shoulder,.04f,reach,radius,new Vector3(.13f,0,-.12f));
                instrument.Sync(run,true,false,false,false);check();
                Assert.That(labels.All(label=>label.text==CompetitionApparatus.Readout(run)),Is.True);
                Assert.That(Fingerprint(run),Is.EqualTo(before));
                Assert.That(instrument.Fits(CompetitionStageFootprint.Station(instrument.Instrument,position,rotation.eulerAngles.y,radius,1.9f)),Is.True);
                Assert.That(owner.transform.position,Is.EqualTo(position));Assert.That(owner.transform.rotation,Is.EqualTo(rotation));
            }
            owner.SetActive(true);yield return null;
        }

        [UnityTest]
        public IEnumerator Apparatus_ContactSolveReachesWithRealHandBonesWithoutStretchingOrMovingTheBody()
        {
            owner=new GameObject("Physical contact solver fixture");
            var pose=owner.AddComponent<CompetitionInstrumentPose>();
            var solve=typeof(CompetitionInstrumentPose).GetMethod("Reach",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(float scale in new[]{.76685f,1f})foreach(float yaw in new[]{0f,37f,171f})foreach(float handSpan in new[]{.11525f,.18371f})
            {
                owner.transform.SetPositionAndRotation(new Vector3(4,2,6),Quaternion.Euler(0,yaw,0));
                owner.transform.localScale=Vector3.one*scale;
                var upper=new GameObject("Upper arm").transform;upper.SetParent(owner.transform,false);upper.localPosition=new Vector3(.2f,1.3f,0);
                var lower=new GameObject("Lower arm").transform;lower.SetParent(upper,false);lower.localPosition=Vector3.right*.28f;
                var wrist=new GameObject("Wrist").transform;wrist.SetParent(lower,false);wrist.localPosition=Vector3.right*.2892f;
                var contact=new GameObject("Actual finger contact").transform;contact.SetParent(wrist,false);contact.localPosition=Vector3.right*handSpan;
                var bones=new[]{upper,lower,wrist,contact};var positions=bones.Select(b=>b.localPosition).ToArray();
                var scales=bones.Select(b=>b.localScale).ToArray();var rotations=bones.Select(b=>b.localRotation).ToArray();
                Vector3 feet=owner.transform.position;Quaternion facing=owner.transform.rotation;
                Vector3 target=upper.position+owner.transform.forward*(handSpan<.15f?.68f:.695f)*scale;
                float wristReach=Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,wrist.position);
                Assert.That(Vector3.Distance(upper.position,target),Is.GreaterThan(wristReach+.07f),"The wrist cannot honestly reach this target.");
                float error=(float)solve.Invoke(pose,new object[]{upper,lower,contact,target,owner.transform.right});
                Assert.That(error,Is.LessThan(.003f));Assert.That(Vector3.Distance(contact.position,target),Is.LessThan(.003f));
                Assert.That(Vector3.Distance(wrist.position,target),Is.GreaterThan(handSpan*scale*.95f),"A contact does not place the wrist inside the prop.");
                Assert.That(bones.Select(b=>b.localPosition),Is.EqualTo(positions));Assert.That(bones.Select(b=>b.localScale),Is.EqualTo(scales));
                Assert.That(owner.transform.position,Is.EqualTo(feet));Assert.That(owner.transform.rotation,Is.EqualTo(facing));
                pose.Release();
                for(int i=0;i<bones.Length;i++)Assert.That(Quaternion.Angle(bones[i].localRotation,rotations[i]),Is.LessThan(.01f),"Release restores only its borrowed rotations.");
                Object.DestroyImmediate(upper.gameObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Apparatus_ContactSolveCannotInventReachForAnUnreachableProp()
        {
            owner=new GameObject("Unreachable physical contact fixture");
            var pose=owner.AddComponent<CompetitionInstrumentPose>();
            var upper=new GameObject("Upper arm").transform;upper.SetParent(owner.transform,false);
            var lower=new GameObject("Lower arm").transform;lower.SetParent(upper,false);lower.localPosition=Vector3.right*.22f;
            var contact=new GameObject("Actual hand contact").transform;contact.SetParent(lower,false);contact.localPosition=Vector3.right*.30f;
            var target=Vector3.forward*1.5f;
            float error=(float)typeof(CompetitionInstrumentPose).GetMethod("Reach",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(pose,new object[]{upper,lower,contact,target,Vector3.right});
            Assert.That(error,Is.GreaterThan(.97f));Assert.That(error,Is.EqualTo(Vector3.Distance(contact.position,target)).Within(.0001f));
            Assert.That(lower.localPosition,Is.EqualTo(Vector3.right*.22f));Assert.That(contact.localPosition,Is.EqualTo(Vector3.right*.30f));
            Assert.That(owner.transform.position,Is.EqualTo(Vector3.zero));Assert.That(owner.transform.localScale,Is.EqualTo(Vector3.one));
            pose.Release();yield return null;
        }

        /// <summary>
        /// The dice read on both sides at every pedestal fit, the clamps included: each value is dark
        /// ink on its paper die (contrast at least WCAG's 4.5:1 for text), the audience's copy on that
        /// die's own rear face, turned to the audience, between the back rail's top and the die's top;
        /// the roll readout and its plates stay under the tray bed rather than inside it; and the
        /// press stops in front of a die's whole rolling sweep, at rest and mid-roll.
        /// "Above the back rail" is the copy's box bottom at or over the rail's top, which keeps the
        /// whole digit in view for a viewer whose eye is at or above the rail's top. The rail follows
        /// the contestant (its top 6.5 cm under the shoulder: 1.275 m at the default fit, 1.675 m at
        /// the high clamp), so for the tallest contestants a lower eye - 1.2 m at 1.5-3 m, say - loses
        /// part or all of the digit behind it. The case does not test a lower eye.
        /// </summary>
        [UnityTest]
        public IEnumerator Apparatus_DiceValuesReadOnTheirDiceAndTheReadoutAndPressClearTheTrayAtEveryFit()
        {
            var definition=CompetitionDefinitions.All.First(item=>item.Category=="Luck");
            var instrument=Make(definition);
            var parts=instrument.GetComponentsInChildren<MeshFilter>(true);
            MeshFilter Part(string name)=>parts.Single(part=>part.name==name);
            float Top(MeshFilter part)=>part.transform.localPosition.y+part.transform.localScale.y*part.sharedMesh.bounds.max.y;
            float Bottom(MeshFilter part)=>part.transform.localPosition.y+part.transform.localScale.y*part.sharedMesh.bounds.min.y;
            var labels=instrument.GetComponentsInChildren<TMP_Text>(true);
            var readouts=labels.Where(label=>label.name.StartsWith("Instrument progress",StringComparison.Ordinal)).ToArray();
            var plates=parts.Where(part=>part.name.StartsWith("Progress ",StringComparison.Ordinal)).ToArray();
            Assert.That(readouts,Has.Length.EqualTo(2));Assert.That(plates,Has.Length.EqualTo(2));
            var bed=Part("Dice tray bed");var rail=Part("Tray back rail");
            var run=new MiniGameRun(CompetitionMiniGames.Kind.Dice,123,4,definition);
            Assert.That(run.Roll(),Is.True);run.Tick(MiniGameRun.RollSeconds+.1);
            Assert.That(run.HasRoll,Is.True,"Precondition: the dice landed.");
            // Below the low clamp (shift -.40 at a .94 m shoulder), between, and above the high one.
            foreach(float shoulder in new[]{.80f,.94f,1.05f,1.20f,1.34f,1.53f,1.74f,1.90f})
            {
                instrument.FitPress(new Vector3(.18f,shoulder,.08f),.62f);
                instrument.Sync(run,true,false,false,false);
                string at=" (shoulder "+shoulder+")";
                for(int i=0;i<3;i++)
                {
                    var die=Part("Die "+i);
                    var material=die.GetComponent<Renderer>().sharedMaterial;
                    Color paper=material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):material.color;
                    var front=labels.Single(label=>label.name=="Die face "+i);
                    var back=labels.Single(label=>label.name=="Die face "+i+" audience readout");
                    Assert.That(front.text,Is.EqualTo(run.Face(i).ToString()));Assert.That(back.text,Is.EqualTo(front.text));
                    foreach(var face in new[]{front,back})
                        Assert.That(Contrast(face.color,paper),Is.GreaterThanOrEqualTo(4.5f),face.name+" reads on its die"+at);
                    Vector3 centre=die.transform.localPosition,copy=back.transform.localPosition;
                    float rear=centre.z+die.transform.localScale.z*die.sharedMesh.bounds.max.z;
                    Assert.That(copy.x,Is.EqualTo(centre.x).Within(.0001f),"Each value stays on its own die"+at);
                    Assert.That(copy.z-rear,Is.InRange(.001f,.012f),"on the die's rear face"+at);
                    Assert.That(Vector3.Dot(back.transform.forward,instrument.transform.forward),Is.LessThan(-.99f),"turned to the audience"+at);
                    float half=back.rectTransform.sizeDelta.y*.5f;
                    Assert.That(copy.y-half,Is.GreaterThanOrEqualTo(Top(rail)-.0005f),"above the back rail, which hides the face's lower half"+at);
                    Assert.That(copy.y+half,Is.LessThanOrEqualTo(Top(die)+.0005f),"and on the die, not over it"+at);
                }
                float underside=Bottom(bed);
                foreach(var plate in plates)
                    Assert.That(Top(plate),Is.LessThan(underside),plate.name+" stays under the tray bed, not inside it"+at);
                foreach(var readout in readouts)
                {
                    Assert.That(readout.transform.localPosition.y+readout.rectTransform.sizeDelta.y*.5f,Is.LessThan(underside),readout.name+at);
                    Assert.That(readout.transform.localPosition.y,Is.EqualTo(plates[0].transform.localPosition.y).Within(.0001f),"The words stay on their plates"+at);
                    Assert.That(readout.transform.localPosition.y-readout.rectTransform.sizeDelta.y*.5f,Is.GreaterThan(0f),"above the floor"+at);
                }
                AssertPressClearsTheDice(instrument,Part("Die 0"),Part("Die 2"),at);
            }
            // Mid-roll the dice turn; the press must still stop short of them.
            Assert.That(run.Roll(),Is.True);run.Tick(MiniGameRun.RollSeconds*.4);
            Assert.That(run.DieLanded(0),Is.False,"Precondition: the dice are rolling.");
            instrument.Sync(run,true,false,false,false);
            Assert.That(Quaternion.Angle(Quaternion.identity,Part("Die 0").transform.localRotation),Is.GreaterThan(1f),"Precondition: a die has turned.");
            AssertPressClearsTheDice(instrument,Part("Die 0"),Part("Die 2")," (rolling)");
            yield return null;
        }

        /// <summary>
        /// A console's key letters stand on their caps from both sides: each audience copy is on the
        /// face of its own audience keycap (mirrored in x, at the key's height), 5.5 mm off it, as the
        /// contestant's letter stands 5-5.5 mm off the contestant's cap. They stood on a shared plane
        /// 20-25 mm off the flush audience caps. The progress copy keeps its plane, in front of its backing.
        /// </summary>
        [UnityTest]
        public IEnumerator Apparatus_ConsoleKeyLettersStandOnTheirCapsOnBothSides()
        {
            foreach(string category in new[]{"Mental","Skill","Social"})
            {
                if(owner!=null){Object.Destroy(owner);owner=null;yield return null;}
                var definition=CompetitionDefinitions.All.First(item=>item.Category==category);
                var instrument=Make(definition);
                var caps=instrument.GetComponentsInChildren<MeshFilter>(true);
                var labels=instrument.GetComponentsInChildren<TMP_Text>(true);
                string family=instrument.Instrument.ToString();
                // The contestant's caps and their letters, in the order the console builds them.
                string capPrefix=instrument.Instrument==CompetitionApparatus.Family.PairConsole?"Memory tile ":instrument.Instrument==CompetitionApparatus.Family.Signals?"Signal target ":"Letter tile ";
                string letterPrefix=instrument.Instrument==CompetitionApparatus.Family.PairConsole?"Memory face ":instrument.Instrument==CompetitionApparatus.Family.Signals?"Signal direction ":"Letter ";
                var fronts=caps.Where(part=>part.name.StartsWith(capPrefix,StringComparison.Ordinal) && !part.name.EndsWith(" audience face",StringComparison.Ordinal)).ToArray();
                Assert.That(fronts,Is.Not.Empty,family+": its keycaps");
                foreach(var front in fronts)
                {
                    string suffix=front.name.Substring(capPrefix.Length);
                    var back=caps.Single(part=>part.name==front.name+" audience face");
                    var letter=labels.Single(label=>label.name==letterPrefix+suffix);
                    var copy=labels.Single(label=>label.name==letter.name+" audience readout");
                    float depth=front.transform.localScale.z;
                    float frontGap=(front.transform.localPosition.z-depth*.5f)-letter.transform.localPosition.z;
                    float backGap=copy.transform.localPosition.z-(back.transform.localPosition.z+depth*.5f);
                    string at=" ("+family+" "+front.name+")";
                    Assert.That(frontGap,Is.InRange(.0045f,.006f),"Precondition: the contestant's letter stands 5-5.5 mm off its cap"+at);
                    Assert.That(backGap,Is.EqualTo(.0055f).Within(.0005f),"The audience's letter stands 5.5 mm off its own cap's face"+at);
                    Assert.That(copy.transform.localPosition.x,Is.EqualTo(back.transform.localPosition.x).Within(.0001f),"on its own cap"+at);
                    Assert.That(copy.transform.localPosition.y,Is.EqualTo(back.transform.localPosition.y).Within(.0001f),"at its cap's height"+at);
                    Assert.That(Vector3.Dot(copy.transform.forward,instrument.transform.forward),Is.LessThan(-.99f),"turned to the audience"+at);
                }
                var progress=labels.Single(label=>label.name=="Instrument progress audience readout");
                var backing=caps.Single(part=>part.name=="Progress audience backing");
                Assert.That(progress.transform.localPosition.z-(backing.transform.localPosition.z+backing.transform.localScale.z*.5f),Is.GreaterThan(.001f),
                    family+": the progress copy stays in front of its own backing");
            }
            yield return null;
        }

        /// <summary>
        /// A tumbling die never swallows its own values. Through whole attempts - the wait before the
        /// first roll (the dice already turn then), each roll's tumble, every landing and both re-rolls -
        /// at every step of a 120 Hz clock, read in the native order (the director's read, then the pose's
        /// press fit), each die's contestant value and its audience copy are either hidden or wholly
        /// outside that die's oriented box, at the pedestal's low, default and high fits. A .17 m die's
        /// corners sweep .12 m round its centre; values left square 9 cm from it were inside it for
        /// most of every tumble. Landed values show, square on their faces.
        /// </summary>
        [UnityTest]
        public IEnumerator Apparatus_DiceValuesStayOutsideTheirTumblingDiceThroughWholeRolls()
        {
            var definition=CompetitionDefinitions.All.First(item=>item.Category=="Luck");
            var instrument=Make(definition);
            var parts=instrument.GetComponentsInChildren<MeshFilter>(true);
            var labels=instrument.GetComponentsInChildren<TMP_Text>(true);
            var dice=Enumerable.Range(0,MiniGameRun.DiceCount).Select(i=>(Die:parts.Single(part=>part.name=="Die "+i),
                Front:labels.Single(label=>label.name=="Die face "+i),Back:labels.Single(label=>label.name=="Die face "+i+" audience readout"))).ToArray();
            const double step=1/120.0;
            int turnedSteps=0,judged=0;
            foreach(float shoulder in new[]{.80f,1.34f,1.90f})
            {
                var run=new MiniGameRun(CompetitionMiniGames.Kind.Dice,123,4,definition);
                double rollAt=.5;int steps=0;
                while(!run.Finished && steps<2400)
                {
                    if(run.Elapsed>=rollAt && run.CanRoll){Assert.That(run.Roll(),Is.True);rollAt=run.Elapsed+MiniGameRun.RollSeconds+.25;}
                    run.Tick(step);steps++;
                    instrument.Sync(run,true,false,false,false);
                    instrument.FitPress(new Vector3(.18f,shoulder,.08f),.62f);
                    foreach(var (die,front,back) in dice)
                    {
                        float turn=Quaternion.Angle(Quaternion.identity,die.transform.localRotation);
                        if(turn>1f)turnedSteps++;
                        var solid=MeshBox(die);
                        foreach(var label in new[]{front,back})
                        {
                            if(LabelHidden(label))continue;
                            judged++;
                            Assert.That(BoxesMeet(LabelBox(label),solid),Is.False,label.name+" ('"+label.text+"') stands inside "+die.name
                                +", turned "+turn.ToString("F1")+" degrees, at "+run.Elapsed.ToString("F4")+" s of roll "+run.RollsUsed+" (shoulder "+shoulder+")");
                        }
                    }
                }
                Assert.That(run.Finished,Is.True,"Precondition: three rolls, the last one kept as it landed (shoulder "+shoulder+").");
                Assert.That(run.RollsUsed,Is.EqualTo(MiniGameRun.MaxRolls));
                for(int i=0;i<dice.Length;i++)
                {
                    Assert.That(dice[i].Die.transform.localRotation,Is.EqualTo(Quaternion.identity),"A landed die is square.");
                    foreach(var label in new[]{dice[i].Front,dice[i].Back})
                    {
                        Assert.That(LabelHidden(label),Is.False,label.name+" shows the landed value");
                        Assert.That(label.text,Is.EqualTo(run.Face(i).ToString()),label.name);
                    }
                }
            }
            Assert.That(turnedSteps,Is.GreaterThan(0),"Precondition: the dice tumbled.");
            Debug.Log("[Gamesim W3] dice values judged against their own dice "+judged+" times over "+turnedSteps+" turned die-steps.");
            yield return null;
        }

        /// <summary>
        /// The Signals response pad stands beside the direction cross, on the console. At every fitted
        /// hand height (shoulders .80-1.90 m, short to long arms, two body offsets and radii): its box
        /// meets no keycap of either side, no direction label of either side and neither progress
        /// readout; its back lies on the housing's front face, inside the housing's height, with at least
        /// half its width on the housing and no more than 2.5 cm past the housing's right edge; where it
        /// meets the recessed display (which stands proud of the housing, so its inner 7 cm sink 2.15 cm
        /// into the panel) it enters through the display's face, its front stands out of the panel and it
        /// sinks no deeper than the panel's own depth; from the contestant's eye (head over the shoulders, swaying
        /// 8 cm) no point of a contestant-side keycap face, a direction label or the progress glyphs is
        /// behind it; and the press lands on its top. It had floated 1.5 cm in front of RIGHT, 6.5 cm off
        /// the housing, hiding 'RIG' of RIGHT's label from the contestant.
        /// </summary>
        [UnityTest]
        public IEnumerator Apparatus_SignalsPadStandsBesideTheCrossOnTheHousingAtEveryFit()
        {
            var definition=CompetitionDefinitions.All.First(item=>item.Category=="Skill");
            var instrument=Make(definition);
            Assert.That(instrument.Instrument,Is.EqualTo(CompetitionApparatus.Family.Signals));
            var parts=instrument.GetComponentsInChildren<MeshFilter>(true);
            var labels=instrument.GetComponentsInChildren<TMP_Text>(true);
            MeshFilter Part(string name)=>parts.Single(part=>part.name==name);
            var pad=Part("Response pad");var housing=Part("Console housing");var display=Part("Recessed display");
            var keycaps=parts.Where(part=>part.name.StartsWith("Signal target ",StringComparison.Ordinal)).ToArray();
            var words=labels.Where(label=>label.name.StartsWith("Signal direction ",StringComparison.Ordinal)
                || label.name.StartsWith("Instrument progress",StringComparison.Ordinal)).ToArray();
            Assert.That(keycaps,Has.Length.EqualTo(8),"Four targets, each with its audience face.");
            Assert.That(words,Has.Length.EqualTo(10),"Four directions and the progress readout, each with its audience copy.");
            var frontCaps=keycaps.Where(part=>!part.name.EndsWith(" audience face",StringComparison.Ordinal)).ToArray();
            var frontWords=words.Where(label=>!label.name.EndsWith(" audience readout",StringComparison.Ordinal)).ToArray();
            var run=new MiniGameRun(CompetitionMiniGames.Kind.Reaction,123,4,definition);
            run.Tick(3);
            Vector3 Min(MeshFilter part)=>part.transform.localPosition+Vector3.Scale(part.transform.localScale,part.sharedMesh.bounds.min);
            Vector3 Max(MeshFilter part)=>part.transform.localPosition+Vector3.Scale(part.transform.localScale,part.sharedMesh.bounds.max);
            var anchor=owner.transform;int rays=0,fits=0,inDisplay=0;float deepest=0,overhang=float.MinValue;
            foreach(float radius in new[]{.32f,.40f})
            foreach(var body in new[]{new Vector3(.13f,0,-.23f),new Vector3(-.16f,0,.19f)})
            foreach(float shoulder in new[]{.80f,.95f,1.10f,1.25f,1.34f,1.53f,1.74f,1.90f})
            foreach(float reach in new[]{.44f,.62f,.70f})
            {
                string at=" (radius "+radius+", body "+body+", shoulder "+shoulder+", reach "+reach+")";
                instrument.FitActorClearance(radius,body);
                instrument.Sync(run,true,false,false,false);
                instrument.FitPress(instrument.transform.InverseTransformPoint(anchor.TransformPoint(body+new Vector3(.18f,shoulder,.04f))),reach);
                var padBox=MeshBox(pad);
                foreach(var keycap in keycaps)Assert.That(BoxesMeet(padBox,MeshBox(keycap)),Is.False,"The pad meets "+keycap.name+at);
                foreach(var word in words)Assert.That(BoxesMeet(padBox,LabelBox(word)),Is.False,"The pad meets "+word.name+at);
                Assert.That(pad.transform.localRotation,Is.EqualTo(Quaternion.identity));Assert.That(housing.transform.localRotation,Is.EqualTo(Quaternion.identity));
                Assert.That(Mathf.Abs(Max(pad).z-Min(housing).z),Is.LessThanOrEqualTo(.001f),"The pad's back lies on the housing's front face"+at);
                Assert.That(Min(pad).y,Is.GreaterThanOrEqualTo(Min(housing).y-.0005f),"within the housing's height"+at);
                Assert.That(Max(pad).y,Is.LessThanOrEqualTo(Max(housing).y+.0005f),"within the housing's height"+at);
                float onHousing=Mathf.Min(Max(pad).x,Max(housing).x)-Mathf.Max(Min(pad).x,Min(housing).x);
                Assert.That(onHousing,Is.GreaterThanOrEqualTo((Max(pad).x-Min(pad).x)*.5f),"with at least half its width on the housing"+at);
                overhang=Mathf.Max(overhang,Max(pad).x-Max(housing).x);
                Assert.That(Max(pad).x-Max(housing).x,Is.LessThanOrEqualTo(.0255f),"and no more than 2.5 cm past the housing's right edge"+at);
                // The recessed display stands proud of the housing's face (its back .35 cm into the housing),
                // so the pad's inner part sinks into its panel: through the face, never behind the panel.
                Assert.That(display.transform.localRotation,Is.EqualTo(Quaternion.identity));
                fits++;
                if(BoxesMeet(padBox,MeshBox(display)))
                {
                    float sunk=Max(pad).z-Min(display).z;
                    Assert.That(Min(pad).z,Is.LessThan(Min(display).z),"The pad meets the recessed display through its face, its front out of the panel"+at);
                    Assert.That(sunk,Is.LessThanOrEqualTo(Max(display).z-Min(display).z+.0005f),"The pad sinks into the recessed display no deeper than its panel"+at);
                    deepest=Mathf.Max(deepest,sunk);inDisplay++;
                }
                var targets=new List<(string Name,Vector3 Point)>();
                foreach(var keycap in frontCaps)
                {
                    var bounds=keycap.sharedMesh.bounds;
                    for(int u=-2;u<=2;u++)for(int v=-2;v<=2;v++)
                        targets.Add((keycap.name,keycap.transform.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(u*.45f,v*.45f,-1)))));
                }
                foreach(var word in frontWords)
                {
                    word.ForceMeshUpdate();
                    var box=word.name=="Instrument progress"?word.textBounds:new Bounds(word.rectTransform.rect.center,word.rectTransform.rect.size);
                    Assert.That(box.size.x,Is.GreaterThan(.01f),"Precondition: "+word.name+" has glyphs ('"+word.text+"')");
                    for(int u=-4;u<=4;u++)for(int v=-1;v<=1;v++)
                        targets.Add((word.name,word.transform.TransformPoint(new Vector3(box.center.x+box.extents.x*u*.25f,box.center.y+box.extents.y*v,0))));
                }
                foreach(float sway in new[]{-.08f,0f,.08f})
                {
                    Vector3 eye=anchor.TransformPoint(body+new Vector3(sway,shoulder+.25f,.08f));
                    foreach(var (name,point) in targets)
                    {
                        rays++;
                        Assert.That(SegmentMeetsBox(eye,point,padBox),Is.False,"From the contestant's eye the pad hides "+name+at);
                    }
                }
                var contact=pad.transform.InverseTransformPoint(instrument.HandContact(false));
                Assert.That(Mathf.Abs(contact.x)<=.5f && Mathf.Abs(contact.z)<=.5f,Is.True,"The press lands over the pad's own top: "+contact+at);
                Assert.That(contact.y*pad.transform.localScale.y,Is.InRange(pad.transform.localScale.y*.5f,pad.transform.localScale.y*.5f+.02f),
                    "just above its top"+at);
            }
            Debug.Log("[Gamesim W3] Signals pad: "+rays+" contestant sight lines clear of it; it meets the recessed display at "+inDisplay+" of "+fits
                +" fits, sunk at most "+deepest.ToString("F4")+" m into its "+(Max(display).z-Min(display).z).ToString("F4")+" m panel; it overhangs the housing's right edge by at most "
                +overhang.ToString("F4")+" m.");
            yield return null;
        }

        /// <summary>
        /// A Signals console's four direction labels, on both sides, share one font size: the largest
        /// that fits all four boxes, which is the smallest of the sizes TMP's own auto-size gives each word
        /// alone in its .18 x .12 box. Alone they come out at four sizes (UP the largest). A console
        /// reserved under an inactive anchor shares the same size on its first read once it is woken.
        /// </summary>
        [UnityTest]
        public IEnumerator Apparatus_SignalsDirectionsShareTheLargestSizeThatFitsAllFour()
        {
            var definition=CompetitionDefinitions.All.First(item=>item.Category=="Skill");
            float? first=null;
            foreach(bool reserved in new[]{false,true})
            {
                if(owner!=null){Object.Destroy(owner);owner=null;yield return null;}
                var instrument=Make(definition,inactive:reserved);
                if(reserved){owner.SetActive(true);instrument.Sync(null,false,false,false,false);}
                var labels=instrument.GetComponentsInChildren<TMP_Text>(true).Where(label=>label.name.StartsWith("Signal direction ",StringComparison.Ordinal)).ToArray();
                Assert.That(labels,Has.Length.EqualTo(8),"Four directions, each with its audience copy.");
                foreach(var label in labels)label.ForceMeshUpdate();
                float shared=labels[0].fontSize;
                foreach(var label in labels)
                {
                    Assert.That(label.enableAutoSizing,Is.False,label.name+" keeps the console's size rather than sizing itself");
                    Assert.That(label.fontSize,Is.EqualTo(shared).Within(.0001f),label.name+" has the console's one size (reserved="+reserved+")");
                }
                var probeRoot=new GameObject("Signal label size probe");probeRoot.SetActive(false);probeRoot.transform.SetParent(owner.transform,false);
                var probe=probeRoot.AddComponent<TextMeshPro>();
                probe.font=labels[0].font;probe.fontSharedMaterial=labels[0].fontSharedMaterial;
                probe.fontSize=1.2f;probe.enableAutoSizing=true;probe.fontSizeMin=.45f;probe.fontSizeMax=1.2f;
                probe.textWrappingMode=TextWrappingModes.NoWrap;probe.alignment=TextAlignmentOptions.Center;
                probeRoot.SetActive(true);
                var alone=new Dictionary<string,float>();
                foreach(var label in labels.Where(label=>!label.name.EndsWith(" audience readout",StringComparison.Ordinal)))
                {
                    probe.rectTransform.sizeDelta=label.rectTransform.sizeDelta;probe.text=label.text;
                    probe.ForceMeshUpdate();alone[label.text]=probe.fontSize;
                }
                string sizes=string.Join(", ",alone.Select(pair=>pair.Key+" "+pair.Value.ToString("F2")));
                Assert.That(alone.Values.Max(),Is.GreaterThan(alone.Values.Min()+.01f),"Precondition: alone, TMP sizes the four differently: "+sizes);
                Assert.That(shared,Is.EqualTo(alone.Values.Min()).Within(.0001f),"The one size is the largest that fits every direction: "+sizes);
                if(first==null)first=shared;
                else Assert.That(shared,Is.EqualTo(first.Value).Within(.0001f),"A reserved console shares the same size once woken.");
                Debug.Log("[Gamesim W3] Signals directions share "+shared.ToString("F2")+" (alone: "+sizes+"; reserved="+reserved+").");
            }
            yield return null;
        }

        /// <summary>
        /// Every face a memory tile shows draws whole inside its own keycap's flat face, on both faces: each
        /// of the eight face words a run deals (the preview turns all sixteen tiles over, each word on two)
        /// and the face-down '?' (at rest, and beside the one tile a started run has turned over). Each label
        /// and its audience copy draws one line, untruncated, every character, and every glyph's quad stands
        /// at least 5 mm inside the top face of the shipped kit's keycap, read from the cap's own mesh. The
        /// eight words share one size, the largest TMP fits the widest at alone in a face's box, and every '?'
        /// shares the size it takes alone, on a console built active and on one woken from a reserved anchor.
        /// Each face used to size its word alone down to a .45 floor, so TROPHY, TARGET, CROWN, SHIELD and
        /// HOUSE ran off their caps (TROPHY by 2 cm), and a lit tile's ink lost the overhang on the navy housing.
        /// </summary>
        [UnityTest]
        public IEnumerator Apparatus_EveryMemoryFaceWordDrawsWholeInsideItsCapOnBothFaces()
        {
            const float margin=.005f;
            var definition=CompetitionDefinitions.All.First(item=>item.Category=="Mental");
            var kit=Resources.Load<CompetitionApparatusMeshKit>(CompetitionApparatusMeshKit.ResourceName);
            Assert.That(kit,Is.Not.Null,"Precondition: the shipped kit, whose keycaps the words must fit.");
            Assert.That(kit.TryGetMesh(CompetitionApparatus.Family.PairConsole,CompetitionApparatusMeshKit.PartKind.MemoryTile,
                new Vector3(.16f,.13f,.025f),out Mesh keycap),Is.True,"Precondition: the kit's memory keycap.");
            // The keycap's flat face: its vertices on the plane nearest the contestant (a keycap's cap faces its part's -z).
            var points=MeshPoints(keycap);
            float capPlane=points.Min(point=>point.z);
            var flat=points.Where(point=>Mathf.Abs(point.z-capPlane)<1e-4f).ToArray();
            Assert.That(flat,Has.Length.GreaterThanOrEqualTo(3),"Precondition: the keycap has a flat top face.");
            Vector2 faceMin=new Vector2(flat.Min(point=>point.x),flat.Min(point=>point.y));
            Vector2 faceMax=new Vector2(flat.Max(point=>point.x),flat.Max(point=>point.y));
            Assert.That(faceMax.x-faceMin.x,Is.LessThan(keycap.bounds.size.x-.01f),"Precondition: that face is the cap's top, narrower than its base.");
            var words=Enumerable.Range(0,CompetitionMiniGames.MemoryPairs).Select(CompetitionGameScreen.MemoryFaceName).ToArray();
            Assert.That(words.Distinct().Count(),Is.EqualTo(8),"Precondition: a run deals eight face words: "+string.Join(", ",words));
            var shown=new HashSet<string>();int read=0;float tightest=float.PositiveInfinity;string tightestAt=null;
            float? firstWordSize=null;

            // Every key's glyph and its audience copy on its own cap: the state's text, one line, nothing cut, every
            // character drawn, and every glyph's quad at least the margin inside the cap's top face.
            void AssertFacesWhole(CompetitionApparatus instrument,Func<int,string> expected,string at)
            {
                var spills=new List<string>();
                foreach(var key in Keys(instrument))
                {
                    int tile=int.Parse(key.Name,System.Globalization.CultureInfo.InvariantCulture);
                    foreach(var (cap,label,side) in new[]{(key.Cap,key.Label,"contestant"),(key.AudienceCap,key.AudienceLabel,"audience")})
                    {
                        string where=label.name+" '"+label.text+"' ("+side+", "+at+")";
                        Assert.That(cap.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(keycap),"Precondition: "+cap.name+" is the kit's keycap ("+at+")");
                        Assert.That(label.text,Is.EqualTo(expected(tile)),where);
                        label.ForceMeshUpdate(true);
                        var info=label.textInfo;
                        Assert.That(info.lineCount,Is.EqualTo(1),where+" draws one line");
                        Assert.That(label.isTextTruncated,Is.False,where+" is not truncated");
                        Assert.That(info.characterCount,Is.EqualTo(label.text.Length),where+" draws every character");
                        Vector3 scale=cap.transform.lossyScale;
                        float inset=float.PositiveInfinity;
                        for(int i=0;i<info.characterCount;i++)
                        {
                            var glyph=info.characterInfo[i];
                            Assert.That(glyph.isVisible,Is.True,where+": '"+glyph.character+"' is drawn");
                            foreach(var corner in new[]{glyph.bottomLeft,glyph.topLeft,glyph.topRight,glyph.bottomRight})
                            {
                                Vector3 on=cap.transform.InverseTransformPoint(label.transform.TransformPoint(corner));
                                inset=Mathf.Min(inset,Mathf.Min(Mathf.Min((on.x-faceMin.x)*scale.x,(faceMax.x-on.x)*scale.x),
                                    Mathf.Min((on.y-faceMin.y)*scale.y,(faceMax.y-on.y)*scale.y)));
                            }
                        }
                        if(inset<tightest){tightest=inset;tightestAt=where;}
                        if(inset<margin)spills.Add(where+" at size "+label.fontSize.ToString("F2")+": "
                            +(inset<0?(-inset*1000).ToString("F1")+" mm past":(inset*1000).ToString("F1")+" mm inside")+" the cap's flat face");
                        if(label.text!="?")shown.Add(label.text);
                        read++;
                    }
                }
                Assert.That(spills,Is.Empty,at+": every glyph stands at least "+(margin*1000).ToString("F0")+" mm inside its cap's flat face: "+string.Join(" | ",spills));
            }

            // The console's own sizes: every face word on both faces at one size, every '?' at another, none sizing itself.
            void AssertFaceSizes(CompetitionApparatus instrument,string at)
            {
                var labels=Keys(instrument).SelectMany(key=>new[]{key.Label,key.AudienceLabel}).ToArray();
                foreach(var group in labels.GroupBy(label=>label.text=="?"))
                {
                    float size=group.First().fontSize;
                    foreach(var label in group)
                    {
                        Assert.That(label.enableAutoSizing,Is.False,label.name+" keeps the console's size rather than sizing itself ("+at+")");
                        Assert.That(label.fontSize,Is.EqualTo(size).Within(.0001f),label.name+" '"+label.text+"' has the console's one "
                            +(group.Key?"face-down":"word")+" size ("+at+")");
                    }
                }
            }

            string sizes=null;Vector3 capScale=Vector3.zero;
            foreach(bool reserved in new[]{false,true})
            {
                if(owner!=null){Object.Destroy(owner);owner=null;yield return null;}
                string pass=reserved?"woken from a reserved anchor":"built active";
                var instrument=Make(definition,inactive:reserved,kit:kit);
                instrument.Sync(null,false,false,false,false);
                if(reserved){owner.SetActive(true);instrument.Sync(null,false,false,false,false);}
                // Each text alone in a face's own box, with its own floor and ceiling: the sizes TMP picks for it.
                var face=Keys(instrument)[0].Label;capScale=Keys(instrument)[0].Cap.transform.lossyScale;
                var probeRoot=new GameObject("Memory face size probe");probeRoot.SetActive(false);probeRoot.transform.SetParent(owner.transform,false);
                var probe=probeRoot.AddComponent<TextMeshPro>();
                probe.font=face.font;probe.fontSharedMaterial=face.fontSharedMaterial;
                probe.fontSize=1.2f;probe.enableAutoSizing=true;probe.fontSizeMin=face.fontSizeMin;probe.fontSizeMax=face.fontSizeMax;
                probe.textWrappingMode=TextWrappingModes.NoWrap;probe.alignment=TextAlignmentOptions.Center;
                probe.rectTransform.sizeDelta=face.rectTransform.sizeDelta;
                probeRoot.SetActive(true);
                var alone=new Dictionary<string,float>();
                foreach(string text in words.Concat(new[]{"?"})){probe.text=text;probe.ForceMeshUpdate();alone[text]=probe.fontSize;}
                Object.Destroy(probeRoot);
                sizes=string.Join(", ",alone.Select(pair=>pair.Key+" "+pair.Value.ToString("F2")));

                AssertFacesWhole(instrument,tile=>"?",pass+", every tile face down");
                var run=new MiniGameRun(CompetitionMiniGames.Kind.Memory,123,4,definition);
                instrument.Sync(run,false,true,false,false);
                Assert.That(instrument.VisibleMemoryFaces,Is.EqualTo(16),"Precondition: the preview turns every tile over ("+pass+").");
                shown.Clear();
                AssertFacesWhole(instrument,tile=>CompetitionGameScreen.MemoryFaceName(run.Faces[tile]),pass+", the preview");
                Assert.That(shown,Is.EquivalentTo(words),"Precondition: the preview shows every face word ("+pass+").");
                AssertFaceSizes(instrument,pass+", the preview");
                float wordSize=Keys(instrument)[0].Label.fontSize;
                Assert.That(wordSize,Is.EqualTo(words.Min(word=>alone[word])).Within(.0001f),
                    "The words' one size is the largest at which the widest fits a face alone ("+pass+"): "+sizes);
                if(firstWordSize==null)firstWordSize=wordSize;
                else Assert.That(wordSize,Is.EqualTo(firstWordSize.Value).Within(.0001f),"A reserved console sizes its words the same once woken.");

                var attempt=new MiniGameRun(CompetitionMiniGames.Kind.Memory,123,4,definition);
                Assert.That(attempt.Flip(0),Is.True,"Precondition: tile 0 turns over.");
                instrument.Sync(attempt,true,false,false,false);
                Assert.That(instrument.VisibleMemoryFaces,Is.EqualTo(1),"Precondition: one face shows ("+pass+").");
                AssertFacesWhole(instrument,tile=>tile==0?CompetitionGameScreen.MemoryFaceName(attempt.Faces[0]):"?",pass+", tile 0 turned over");
                AssertFaceSizes(instrument,pass+", tile 0 turned over");
                instrument.Sync(null,false,false,false,false);
                AssertFacesWhole(instrument,tile=>"?",pass+", at rest after the attempt");
                AssertFaceSizes(instrument,pass+", at rest after the attempt");
                Assert.That(Keys(instrument)[0].Label.fontSize,Is.EqualTo(alone["?"]).Within(.0001f),
                    "A face-down tile's '?' keeps the size it takes alone ("+pass+"): "+sizes);
            }
            Debug.Log("[Gamesim W3] Memory faces: "+read+" faces read on both sides, words at "+firstWordSize.Value.ToString("F2")+" (alone: "+sizes
                +"); the cap's flat face "+((faceMax.x-faceMin.x)*capScale.x).ToString("F4")+" x "+((faceMax.y-faceMin.y)*capScale.y).ToString("F4")
                +" m; tightest "+(tightest*1000).ToString("F1")+" mm inside it ("+tightestAt+").");
            yield return null;
        }

        /// <summary>
        /// A lit Signals target's label and its audience copy are dark on the lit mint cap, on both
        /// faces: at least 4.5:1, computed from the colours the cap renderers and labels actually carry.
        /// An unlit target's label is paper, so a target that goes out takes its label back to paper.
        /// The labels used to stay paper on the lit cap (1.5:1), so the one key the contestant had to
        /// read was the faintest on the console.
        /// </summary>
        [UnityTest]
        public IEnumerator Apparatus_ALitSignalTargetsLabelIsDarkOnItsLitCapOnBothFaces()
        {
            var definition=CompetitionDefinitions.All.First(item=>item.Category=="Skill");
            var instrument=Make(definition);
            var keys=Keys(instrument);
            Assert.That(keys.Select(key=>key.Name),Is.EquivalentTo(SignalNames),"Four targets, each with its audience face and copy.");
            var run=new MiniGameRun(CompetitionMiniGames.Kind.Reaction,123,4,definition);
            var lit=new HashSet<string>();int litReads=0,wentOut=0;string before=null;float lowest=float.PositiveInfinity;
            for(int frame=0;frame<600 && !run.Finished;frame++)
            {
                run.Tick(.05);
                instrument.Sync(run,true,false,false,false);
                Assert.That(instrument.LitSignal,Is.EqualTo(run.TargetLive?(int)run.TargetDirection:-1));
                string now=null;
                foreach(var key in keys)
                {
                    bool on=Array.IndexOf(SignalNames,key.Name)==instrument.LitSignal;
                    if(on)now=key.Name;
                    foreach(var (cap,label) in new[]{(key.Cap,key.Label),(key.AudienceCap,key.AudienceLabel)})
                    {
                        Color face=CapColour(cap);
                        string at=" (frame "+frame+", "+label.color+" on "+face+")";
                        if(!on){Assert.That(label.color,Is.EqualTo(UiTheme.Paper),label.name+" is paper while its target is unlit"+at);continue;}
                        Assert.That(Near(face,UiTheme.Positive),Is.True,"Precondition: "+cap.name+" is lit"+at);
                        float contrast=Contrast(label.color,face);lowest=Mathf.Min(lowest,contrast);
                        Assert.That(RelativeLuminance(label.color),Is.LessThan(RelativeLuminance(face)),label.name+" is dark on its lit cap"+at);
                        Assert.That(contrast,Is.GreaterThanOrEqualTo(4.5f),label.name+" reads on its lit cap"+at);
                    }
                }
                if(now!=null){lit.Add(now);litReads++;}
                if(before!=null && before!=now)wentOut++;
                before=now;
            }
            Assert.That(lit.Count,Is.GreaterThanOrEqualTo(2),"Precondition: more than one target is lit in the run: "+string.Join(", ",lit));
            Assert.That(wentOut,Is.GreaterThanOrEqualTo(1),"Precondition: a lit target went out and its label was read after.");
            Debug.Log("[Gamesim W3] Lit Signals labels: "+litReads+" lit reads over "+string.Join(", ",lit)+", "+wentOut
                +" targets gone out; lowest lit contrast "+lowest.ToString("F2")+":1 on both faces.");
            yield return null;
        }

        /// <summary>
        /// The memory console and the letter tray follow the lit target's rule on both faces. A turned-over
        /// tile on the pale Accent cap, a matched tile on the mint and a picked letter on the mint each
        /// carry a glyph and an audience copy that are darker than the cap and at least 4.5:1 on it,
        /// computed from the colours the cap renderers and labels actually carry. A tile that turns back,
        /// a letter taken back and a console that rests take their glyphs back to paper. These glyphs
        /// used to stay paper on the light caps (1.4:1 on Accent, 1.5:1 on the mint), so the keys the
        /// contestant had just turned or picked were the faintest on the console.
        /// </summary>
        [UnityTest]
        public IEnumerator Apparatus_ATurnedOverTileOrPickedLetterIsDarkOnItsLitCapOnBothFaces()
        {
            int litFaces=0;float lowest=float.PositiveInfinity;
            // Every key on both faces: a key meant to be lit shows that cap under a darker glyph at 4.5:1 or better; any other key's glyph is paper.
            void AssertKeys(CompetitionApparatus instrument,Func<int,Color?> litCap,string at)
            {
                foreach(var key in Keys(instrument))
                {
                    Color? want=litCap(int.Parse(key.Name,System.Globalization.CultureInfo.InvariantCulture));
                    foreach(var (cap,label) in new[]{(key.Cap,key.Label),(key.AudienceCap,key.AudienceLabel)})
                    {
                        Color face=CapColour(cap);
                        string where=" ("+at+": "+label.color+" on "+face+")";
                        if(want==null){Assert.That(label.color,Is.EqualTo(UiTheme.Paper),label.name+" is paper while its key is unlit"+where);continue;}
                        Assert.That(Near(face,want.Value),Is.True,"Precondition: "+cap.name+" is lit"+where);
                        float contrast=Contrast(label.color,face);lowest=Mathf.Min(lowest,contrast);
                        Assert.That(RelativeLuminance(label.color),Is.LessThan(RelativeLuminance(face)),label.name+" is dark on its lit cap"+where);
                        Assert.That(contrast,Is.GreaterThanOrEqualTo(4.5f),label.name+" reads on its lit cap"+where);
                        litFaces++;
                    }
                }
            }

            var mental=CompetitionDefinitions.All.First(item=>item.Category=="Mental");
            var memory=Make(mental);
            var run=new MiniGameRun(CompetitionMiniGames.Kind.Memory,123,4,mental);
            int tiles=run.Faces.Count,first=0;
            int partner=Enumerable.Range(1,tiles-1).First(i=>run.Faces[i]==run.Faces[first]);
            int a=Enumerable.Range(1,tiles-1).First(i=>i!=partner);
            int b=Enumerable.Range(1,tiles-1).First(i=>i!=partner && run.Faces[i]!=run.Faces[a]);
            Color? Matched(int i)=>run.Matched[i]?UiTheme.Positive:(Color?)null;
            memory.Sync(run,true,false,false,false);
            AssertKeys(memory,i=>null,"Mental, every tile face down");
            Assert.That(run.Flip(first),Is.True,"Precondition: tile "+first+" turns over.");
            memory.Sync(run,true,false,false,false);
            Assert.That(memory.VisibleMemoryFaces,Is.EqualTo(1),"Precondition: one face shows.");
            AssertKeys(memory,i=>i==first?UiTheme.Accent:(Color?)null,"Mental, one tile turned over");
            Assert.That(run.Flip(partner),Is.True);
            Assert.That(run.MatchedPairs,Is.EqualTo(1),"Precondition: tiles "+first+" and "+partner+" match.");
            memory.Sync(run,true,false,false,false);
            AssertKeys(memory,Matched,"Mental, a pair matched");
            Assert.That(run.Flip(a) && run.Flip(b),Is.True);
            Assert.That(run.SecondFlip,Is.EqualTo(b),"Precondition: tiles "+a+" and "+b+" do not match and show while they are compared.");
            memory.Sync(run,true,false,false,false);
            Assert.That(memory.VisibleMemoryFaces,Is.EqualTo(4),"Precondition: the matched pair and the compared pair show.");
            AssertKeys(memory,i=>i==a || i==b?UiTheme.Accent:Matched(i),"Mental, a pair matched and a mismatched pair turned over");
            while(run.SecondFlip>=0 && !run.Finished)run.Tick(.05);
            Assert.That(run.Finished,Is.False,"Precondition: the mismatched pair turns back before the whistle.");
            memory.Sync(run,true,false,false,false);
            AssertKeys(memory,Matched,"Mental, the mismatched pair turned back");
            memory.Sync(null,false,false,false,false);
            AssertKeys(memory,i=>null,"Mental, at rest");

            Object.Destroy(owner);owner=null;yield return null;
            var social=CompetitionDefinitions.All.First(item=>item.Category=="Social");
            var tray=Make(social);
            var words=new MiniGameRun(CompetitionMiniGames.For("Social"),123,4,social);
            Assert.That(words.Kind,Is.EqualTo(CompetitionMiniGames.Kind.Words),"Precondition: the Social console is the letter tray.");
            Assert.That(words.Scrambled.Length,Is.GreaterThanOrEqualTo(2),"Precondition: one picked letter leaves the word open, so it can be taken back.");
            tray.Sync(words,true,false,false,false);
            AssertKeys(tray,i=>null,"Social, no letter picked");
            Assert.That(words.PickTile(0),Is.True,"Precondition: the first letter is picked.");
            tray.Sync(words,true,false,false,false);
            AssertKeys(tray,i=>i==0?UiTheme.Positive:(Color?)null,"Social, a letter picked");
            Assert.That(words.Undo(),Is.True,"Precondition: the letter is taken back.");
            tray.Sync(words,true,false,false,false);
            AssertKeys(tray,i=>null,"Social, the letter taken back");
            Assert.That(words.PickTile(1),Is.True,"Precondition: another letter is picked.");
            tray.Sync(words,true,false,false,false);
            AssertKeys(tray,i=>i==1?UiTheme.Positive:(Color?)null,"Social, another letter picked");
            tray.Sync(null,false,false,false,false);
            AssertKeys(tray,i=>null,"Social, at rest");
            Debug.Log("[Gamesim W3] Lit memory tiles and letters: "+litFaces+" lit key faces read (tiles "+first+"/"+partner+" matched, "
                +a+"/"+b+" compared), lowest lit contrast "+lowest.ToString("F2")+":1 on both faces.");
            yield return null;
        }

        /// <summary>
        /// Another entrant's console, synced with no attempt, shows the faces the player's console shows
        /// before its first input, on both sides. Every key's glyph has a colour of its own and reads on
        /// its cap at 3:1 or better, computed from the colours the renderers and labels actually carry.
        /// Nothing is lit or turned over: Signals shows its four directions, memory tiles and letter caps
        /// show '?', and the readout says 'Ready'. Each console is read twice. The first read comes after
        /// its first no-attempt sync, as another entrant's station is in the game, where no attempt ever
        /// reaches it; a reserved console is read there while its anchor is still inactive. The second
        /// read comes after an attempt (memory faces, a lit target, letters) and two more no-attempt
        /// syncs, so the console has gone back to rest. These keys used to be paper glyphs on paper caps
        /// (1:1), so the field's other boards looked switched off.
        /// </summary>
        [UnityTest]
        public IEnumerator Apparatus_AConsoleWithNoAttemptShowsReadableUnlitKeysWithNothingLit()
        {
            int read=0;float lowest=float.PositiveInfinity;
            // A console with no attempt, on both faces: the cap and glyph colours the player's console rests on, a glyph
            // colour unlike its face at 3:1 or better, the directions or '?', nothing lit or turned over, and 'Ready'.
            void AssertResting(CompetitionApparatus instrument,string category,(Color cap,Color glyph) rest,string place)
            {
                Assert.That(instrument.ProgressText,Is.EqualTo("Ready"),"The readout stays 'Ready'"+place);
                Assert.That(instrument.LitSignal,Is.EqualTo(-1),"Nothing is lit"+place);
                Assert.That(instrument.VisibleMemoryFaces,Is.Zero,"and nothing is turned over"+place);
                var keys=Keys(instrument);
                Assert.That(keys,Has.Length.EqualTo(category=="Mental"?16:category=="Skill"?4:12),place);
                foreach(var key in keys)
                {
                    string expected=category=="Skill"?key.Name:"?";
                    foreach(var (cap,label) in new[]{(key.Cap,key.Label),(key.AudienceCap,key.AudienceLabel)})
                    {
                        Color face=CapColour(cap);
                        string at=place+": "+label.color+" on "+face;
                        Assert.That(label.text,Is.EqualTo(expected),label.name+place);
                        Assert.That(Near(face,rest.cap),Is.True,cap.name+" shows the unlit cap the player's console rests on"+at);
                        Assert.That(label.color,Is.EqualTo(rest.glyph),label.name+" has the resting glyph colour"+at);
                        Assert.That(Near(label.color,face),Is.False,label.name+"'s colour differs from its face"+at);
                        float contrast=Contrast(label.color,face);lowest=Mathf.Min(lowest,contrast);
                        Assert.That(contrast,Is.GreaterThanOrEqualTo(3f),label.name+" reads on its cap"+at);
                        read++;
                    }
                }
            }
            foreach(string category in new[]{"Mental","Skill","Social"})
            {
                var definition=CompetitionDefinitions.All.First(item=>item.Category==category);
                var kind=CompetitionMiniGames.For(category);
                if(owner!=null){Object.Destroy(owner);owner=null;yield return null;}
                // The player's console before its first input: the faces another entrant's console shows.
                var player=Make(definition);
                player.Sync(new MiniGameRun(kind,123,4,definition),false,false,false,false);
                var resting=Keys(player).SelectMany(key=>new[]{(cap:CapColour(key.Cap),glyph:key.Label.color),
                    (cap:CapColour(key.AudienceCap),glyph:key.AudienceLabel.color)}).Distinct().ToArray();
                Assert.That(resting,Has.Length.EqualTo(1),"Precondition: the player's console rests on one cap and glyph colour ("+category+").");
                foreach(bool reserved in new[]{false,true})
                {
                    Object.Destroy(owner);owner=null;yield return null;
                    var instrument=Make(definition,inactive:reserved);
                    string place=" ("+category+(reserved?", reserved":"")+")";
                    // Another entrant's station: its first sync has no attempt, and in the game none ever comes.
                    instrument.Sync(null,false,false,false,false);
                    AssertResting(instrument,category,resting[0],place+", never attempted");
                    // Show an attempt (every memory face in the preview, a lit target, the letters), then rest
                    // again: the second rest puts back every face the attempt changed.
                    var run=new MiniGameRun(kind,123,4,definition);
                    if(category=="Skill")while(!run.TargetLive && !run.Finished)run.Tick(.05);
                    instrument.Sync(run,category!="Mental",category=="Mental",false,false);
                    Assert.That(category=="Mental"?instrument.VisibleMemoryFaces==16:category=="Skill"?instrument.LitSignal>=0
                        :Keys(instrument).Any(key=>key.Label.text!="?" && key.Label.text!=""),Is.True,
                        "Precondition: the attempt shows on the console before it rests again ("+category+").");
                    for(int pass=0;pass<2;pass++)instrument.Sync(null,false,false,false,false);
                    if(reserved)owner.SetActive(true);
                    AssertResting(instrument,category,resting[0],place+", after an attempt");
                }
            }
            Debug.Log("[Gamesim W3] Consoles with no attempt: "+read+" key faces read, lowest glyph contrast "+lowest.ToString("F2")+":1, none lit.");
            yield return null;
        }

        private static readonly string[] SignalNames={"UP","RIGHT","DOWN","LEFT"};

        private struct ConsoleKey { public string Name; public Renderer Cap,AudienceCap; public TMP_Text Label,AudienceLabel; }

        /// <summary>A console's keys: each cap and glyph on the contestant's side, with its audience face and copy.</summary>
        private static ConsoleKey[] Keys(CompetitionApparatus instrument)
        {
            string cap,glyph;
            switch(instrument.Instrument)
            {
                case CompetitionApparatus.Family.PairConsole:cap="Memory tile ";glyph="Memory face ";break;
                case CompetitionApparatus.Family.Signals:cap="Signal target ";glyph="Signal direction ";break;
                case CompetitionApparatus.Family.WordConsole:cap="Letter tile ";glyph="Letter ";break;
                default:throw new ArgumentException(instrument.Instrument+" has no console keys.");
            }
            var renderers=instrument.GetComponentsInChildren<Renderer>(true);
            var labels=instrument.GetComponentsInChildren<TMP_Text>(true);
            return renderers.Where(part=>part.name.StartsWith(cap,StringComparison.Ordinal) && !part.name.EndsWith(" audience face",StringComparison.Ordinal))
                .Select(front=>
                {
                    string name=front.name.Substring(cap.Length);
                    var label=labels.Single(text=>text.name==glyph+name);
                    return new ConsoleKey{Name=name,Cap=front,AudienceCap=renderers.Single(part=>part.name==front.name+" audience face"),
                        Label=label,AudienceLabel=labels.Single(text=>text.name==label.name+" audience readout")};
                }).ToArray();
        }

        /// <summary>The colour a cap draws: its tint if it has one, its material's otherwise.</summary>
        private static Color CapColour(Renderer cap)
        {
            var block=new MaterialPropertyBlock();cap.GetPropertyBlock(block);
            if(!block.isEmpty)return block.GetColor("_BaseColor");
            var material=cap.sharedMaterial;
            return material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):material.color;
        }

        private static bool Near(Color a,Color b)=>Mathf.Abs(a.r-b.r)<.002f && Mathf.Abs(a.g-b.g)<.002f && Mathf.Abs(a.b-b.b)<.002f;

        /// <summary>A mesh's vertices. The kit's imported parts are not CPU-readable; the editor reads them without a reimport.</summary>
        private static Vector3[] MeshPoints(Mesh mesh)
        {
#if UNITY_EDITOR
            using(var data=UnityEditor.MeshUtility.AcquireReadOnlyMeshData(mesh))
            using(var points=new Unity.Collections.NativeArray<Vector3>(data[0].vertexCount,Unity.Collections.Allocator.Temp))
            {
                data[0].GetVertices(points);
                return points.ToArray();
            }
#else
            return mesh.vertices;
#endif
        }

        private struct OrientedBox { public Vector3 Centre,X,Y,Z; }

        /// <summary>A part's oriented box: its mesh bounds in world space, as three half-axes.</summary>
        private static OrientedBox MeshBox(MeshFilter part)
        {
            var bounds=part.sharedMesh.bounds;var t=part.transform;
            return new OrientedBox{Centre=t.TransformPoint(bounds.center),X=t.TransformVector(new Vector3(bounds.extents.x,0,0)),
                Y=t.TransformVector(new Vector3(0,bounds.extents.y,0)),Z=t.TransformVector(new Vector3(0,0,bounds.extents.z))};
        }

        /// <summary>A label's own box: its rect in its plane, a tenth of a millimetre thick.</summary>
        private static OrientedBox LabelBox(TMP_Text label)
        {
            var rect=label.rectTransform.rect;var t=label.transform;
            Vector3 x=t.TransformVector(new Vector3(rect.width*.5f,0,0)),y=t.TransformVector(new Vector3(0,rect.height*.5f,0));
            return new OrientedBox{Centre=t.TransformPoint(rect.center),X=x,Y=y,Z=Vector3.Cross(x,y).normalized*.0001f};
        }

        private static bool LabelHidden(TMP_Text label)=>!label.enabled || !label.gameObject.activeInHierarchy
            || !label.GetComponent<Renderer>().enabled || string.IsNullOrWhiteSpace(label.text);

        /// <summary>Whether two oriented boxes share volume: no separating axis among the fifteen. Touching is not meeting.</summary>
        private static bool BoxesMeet(OrientedBox a,OrientedBox b)
        {
            var own=new[]{a.X,a.Y,a.Z};var other=new[]{b.X,b.Y,b.Z};
            var axes=new List<Vector3>(own.Concat(other));
            foreach(var u in own)foreach(var v in other)axes.Add(Vector3.Cross(u,v));
            Vector3 offset=b.Centre-a.Centre;
            foreach(var axis in axes)
            {
                if(axis.sqrMagnitude<1e-14f)continue;
                var n=axis.normalized;
                float reachA=Mathf.Abs(Vector3.Dot(a.X,n))+Mathf.Abs(Vector3.Dot(a.Y,n))+Mathf.Abs(Vector3.Dot(a.Z,n));
                float reachB=Mathf.Abs(Vector3.Dot(b.X,n))+Mathf.Abs(Vector3.Dot(b.Y,n))+Mathf.Abs(Vector3.Dot(b.Z,n));
                if(Mathf.Abs(Vector3.Dot(offset,n))>=reachA+reachB-.0001f)return false;
            }
            return true;
        }

        /// <summary>Whether the sight line from <paramref name="from"/> to just short of <paramref name="to"/> passes through the box.</summary>
        private static bool SegmentMeetsBox(Vector3 from,Vector3 to,OrientedBox box)
        {
            float enter=0,exit=1-1e-4f;
            foreach(var axis in new[]{box.X,box.Y,box.Z})
            {
                float scale=axis.sqrMagnitude;
                float start=Vector3.Dot(from-box.Centre,axis)/scale,delta=Vector3.Dot(to-from,axis)/scale;
                if(Mathf.Abs(delta)<1e-9f){if(Mathf.Abs(start)>1)return false;continue;}
                float near=(-1-start)/delta,far=(1-start)/delta;
                if(near>far){float swap=near;near=far;far=swap;}
                enter=Mathf.Max(enter,near);exit=Mathf.Min(exit,far);
                if(enter>exit)return false;
            }
            return true;
        }

        private static void AssertPressClearsTheDice(CompetitionApparatus instrument,MeshFilter left,MeshFilter right,string at)
        {
            foreach(var (die,isLeft) in new[]{(left,true),(right,false)})
            {
                Vector3 centre=die.transform.localPosition;
                Vector3 contact=instrument.transform.InverseTransformPoint(instrument.HandContact(isLeft));
                float sweep=new Vector2(die.transform.localScale.x*die.sharedMesh.bounds.extents.x,die.transform.localScale.z*die.sharedMesh.bounds.extents.z).magnitude;
                Assert.That(centre.z-contact.z,Is.GreaterThan(sweep+.02f),
                    "The press stops in front of "+die.name+"'s rolling sweep, the fingertip's two centimetres past its bone clear of it"+at);
                Assert.That(contact.x,Is.EqualTo(centre.x).Within(.001f));Assert.That(contact.y,Is.EqualTo(centre.y).Within(.001f));
            }
        }

        /// <summary>WCAG 2's contrast ratio between two sRGB colours.</summary>
        private static float Contrast(Color a,Color b)
        {
            float x=RelativeLuminance(a),y=RelativeLuminance(b);
            return (Mathf.Max(x,y)+.05f)/(Mathf.Min(x,y)+.05f);
        }

        /// <summary>WCAG 2's relative luminance of an sRGB colour.</summary>
        private static float RelativeLuminance(Color c)
        {
            float Channel(float v)=>v<=.04045f?v/12.92f:Mathf.Pow((v+.055f)/1.055f,2.4f);
            return .2126f*Channel(c.r)+.7152f*Channel(c.g)+.0722f*Channel(c.b);
        }

        private static string Fingerprint(MiniGameRun run)
        {
            var rng=(SeededRandom)typeof(MiniGameRun).GetField("random",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(run);
            return string.Join("/",run.Elapsed,run.Finished,run.Score,rng.State,run.Meter,run.Held,run.Holding,run.Hits,run.Spawned,
                run.TargetLive,run.TargetDirection,run.TargetX,run.TargetY,run.MatchedPairs,run.FirstFlip,run.SecondFlip,
                run.RollsUsed,run.RollTotal,run.KeptTotal,run.Word,run.Scrambled,run.Spelled,run.WordsSolved,run.WordPoints);
        }
    }
}
