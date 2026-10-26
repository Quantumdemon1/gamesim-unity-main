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

        private CompetitionApparatus Make(CompetitionDefinition definition, bool inactive = false)
        {
            owner=new GameObject("Apparatus clearance anchor");
            owner.transform.SetPositionAndRotation(new Vector3(4,2,6),Quaternion.Euler(0,37,0));
            owner.SetActive(!inactive);
            return CompetitionApparatus.Create(owner.transform,definition,definition.Category,"entrant",UiTheme.Gold,null);
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
            float Channel(float c)=>c<=.04045f?c/12.92f:Mathf.Pow((c+.055f)/1.055f,2.4f);
            float Luminance(Color c)=>.2126f*Channel(c.r)+.7152f*Channel(c.g)+.0722f*Channel(c.b);
            float x=Luminance(a),y=Luminance(b);
            return (Mathf.Max(x,y)+.05f)/(Mathf.Min(x,y)+.05f);
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
