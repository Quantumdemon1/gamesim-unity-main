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
            return CompetitionApparatus.Create(owner.transform,definition,definition.Category,"entrant",UiTheme.Gold);
        }

        [UnityTest]
        public IEnumerator Apparatus_AllDefinitionsUseDistinctInstrumentsThatClearTheActorAndOwnTheirResources()
        {
            foreach(var definition in CompetitionDefinitions.All)
            {
                var instrument=Make(definition,inactive:true);
                var attempt=new MiniGameRun(CompetitionMiniGames.For(definition.Category),123,4,definition);
                Assert.That(owner.activeInHierarchy,Is.False);
                instrument.Sync(attempt,true,false,false,false);
                owner.SetActive(true);
                instrument.Sync(attempt,true,false,false,false);
                Assert.That(instrument.ProgressText,Is.EqualTo(CompetitionApparatus.Readout(attempt)),
                    "A reserved instrument can receive progress before its anchor becomes active.");
                var at=owner.transform.position;var facing=owner.transform.rotation;
                Assert.That(instrument.DefinitionId,Is.EqualTo(definition.Id));
                Assert.That(instrument.Instrument,Is.EqualTo(Expected(definition.Category)),definition.Id);
                Assert.That(instrument.GetComponentsInChildren<Collider>(true),Is.Empty,"Scenery must not alter the reserved route.");
                // A native route stops near its anchor rather than teleporting exactly onto it.
                Vector3 bodyOffset=new Vector3(.13f,0,-.23f);
                if(instrument.Instrument==CompetitionApparatus.Family.GripRig)instrument.FitGrip(1.4f,bodyOffset.z+.04f,.60f,.32f,bodyOffset);
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
            }
        }

        private static bool Finite(float value)=>!float.IsInfinity(value)&&!float.IsNaN(value);
        private static CompetitionApparatus.Family Expected(string category)=>category=="Mental"?CompetitionApparatus.Family.PairConsole
            :category=="Endurance"?CompetitionApparatus.Family.GripRig:category=="Luck"?CompetitionApparatus.Family.DiceTray
            :category=="Social"?CompetitionApparatus.Family.WordConsole:CompetitionApparatus.Family.Signals;

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
            yield return null;
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

        private static string Fingerprint(MiniGameRun run)
        {
            var rng=(SeededRandom)typeof(MiniGameRun).GetField("random",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(run);
            return string.Join("/",run.Elapsed,run.Finished,run.Score,rng.State,run.Meter,run.Held,run.Holding,run.Hits,run.Spawned,
                run.TargetLive,run.TargetDirection,run.TargetX,run.TargetY,run.MatchedPairs,run.FirstFlip,run.SecondFlip,
                run.RollsUsed,run.RollTotal,run.KeptTotal,run.Word,run.Scrambled,run.Spelled,run.WordsSolved,run.WordPoints);
        }
    }
}
