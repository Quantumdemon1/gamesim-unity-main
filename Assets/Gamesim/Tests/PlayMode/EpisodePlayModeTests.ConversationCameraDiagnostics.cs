using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator CameraConversation_NativeEReachesATalkSpotAndRecordsTheRenderedPair()
            => DiagnoseNativeConversationCamera("native-e",false,false);

        [UnityTest]
        public IEnumerator CameraConversation_NativeClickReachesATalkSpotAndRecordsTheRenderedPair()
            => DiagnoseNativeConversationCamera("native-click",true,false);

        [UnityTest]
        public IEnumerator CameraConversation_NativeSecondEPressRecordsTheImplicatedStartingGeometry()
            => DiagnoseNativeConversationCamera("native-second-e",false,true);

        /// <summary>Input, actual routes and the authored starting positions; no actor is placed for a successful shot.</summary>
        private IEnumerator DiagnoseNativeConversationCamera(string route,bool mouse,bool secondPress)
        {
            var report=new ConversationCameraReport {route=route,startedUtc=DateTime.UtcNow.ToString("o"),unityVersion=Application.unityVersion,
                bodyProvider=CharacterBodySource.Provider?.GetType().FullName??"Primitive",actorsPlacedByFixture=false};
            bool oldSpots=director.TalkSpotsInBatchRuns;
            bool oldReduced=NpcRead<bool>("reducedMotion"),oldRigReduced=cameraRig.ReducedMotion;
            bool oldSuppressed=NpcRead<bool>("npcApproachDiagnosticsSuppressed");
            float oldAmbient=NpcRead<float>("nextAmbientActivity");
            var visuals=SceneComponents<CharacterPresentation>();var oldBodyReduced=visuals.Select(v=>v.ReducedMotion).ToArray();
            string stem="conversation-camera-"+route;
            try
            {
                director.ClosePanels();
                // Hold only new optional NPC errands. Existing runtime binding and the native
                // player/NPC talk route remain live; no saved deadline or arrival is fabricated.
                NpcWrite("npcApproachDiagnosticsSuppressed",true);NpcWrite("nextAmbientActivity",float.PositiveInfinity);
                AskForTheTalkSpots();
                yield return WaitForNpcRuntimeBinding();
                var coordinator=NpcRead<HouseMeetingCoordinator>("npcMeetings");
                coordinator.ReleaseAll();coordinator.ReleaseActivities();
                var npc=SceneComponents<HouseNpc>().Single(n=>n.Id==ContentCatalog.MayaId && n.gameObject.activeInHierarchy);
                float deadline=Time.realtimeSinceStartup+20f;
                bool BodiesReady()=>new[]{player.GetComponent<CharacterPresentation>(),npc.GetComponent<CharacterPresentation>()}
                    .All(v=>v!=null && v.VisualRoot!=null && !v.IsBodyAssembling && !v.IsChangingOutfit);
                while(!BodiesReady() && Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(BodiesReady(),Is.True,"The real pair must have its current bodies before visibility is measured.");
                report.frames.Add(ReadConversationCameraFrame("before-native-input",npc));
                if(mouse)
                {
                    // Only the camera follows the target for pointer aim. The pair stays where the
                    // actual scene/runtime put them, and the press goes through the real controller.
                    cameraRig.FocusSubject(npc.transform);yield return SettleCamera();
                    yield return ClickHouseguest(TestMouse(),npc);
                }
                else
                {
                    var choice=new object[]{null};
                    Assert.That(NpcInvoke("ChooseInteraction",choice).ToString(),Is.EqualTo("Talk"),"E must mean Talk at the unplaced scene start.");
                    Assert.That(choice[0],Is.SameAs(npc),"The starting E prompt must choose the implicated Maya pair.");
                    yield return PressKey(Key.E);
                }
                var spot=director.CurrentTalkSpot;
                Assert.That(spot,Is.Not.Null,"Native input must acquire a real talk spot: "+director.StatusMessage);
                Assert.That(spot.NpcId,Is.EqualTo(npc.Id));
                report.frames.Add(ReadConversationCameraFrame("first-native-input",npc));
                if(secondPress)
                {
                    Assert.That(director.TalkingToId,Is.Null,"A second E press is exercised during the actual walk, before its conversation opens.");
                    yield return PressKey(Key.E);
                    Assert.That(director.CurrentTalkSpot,Is.Null,"The native second press lets the chosen place go.");
                    Assert.That(spot.Released && coordinator.Talk==null,Is.True);
                    Assert.That(director.WalkingToId,Is.Null);
                }
                else
                {
                    deadline=Time.realtimeSinceStartup+45f;
                    while(director.TalkingToId==null && director.CurrentTalkSpot==spot && Time.realtimeSinceStartup<deadline)yield return null;
                    Assert.That(director.CurrentTalkSpot,Is.SameAs(spot),"The normal route must open at its actual spot, rather than silently using the fallback.");
                }
                Assert.That(director.TalkingToId,Is.EqualTo(npc.Id),"Native routing must open on Maya: "+director.StatusMessage);
                deadline=Time.realtimeSinceStartup+6f;
                while(!(cameraRig.HasArrived(.1f) && Mathf.Abs(cameraRig.ViewCamera.fieldOfView-HouseCameraRig.TwoShotFieldOfView)<.3f)
                    && Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(cameraRig.HasShot && cameraRig.IsConversationFocused,Is.True);
                Assert.That(cameraRig.HasArrived(.1f),Is.True,"The requested shot must settle before its actual eye is diagnosed.");
                yield return CaptureFraming(stem,inspect:_=>
                {
                    var frame=ReadConversationCameraFrame("captured-native-conversation",npc);
                    report.frames.Add(frame);
                    frame.visibility=ProbeConversationPairVisibility(npc,stem);
                    Debug.Log("[Gamesim] native conversation camera diagnostic: "+JsonUtility.ToJson(frame));
                });
                var captured=report.frames.Last();
                Assert.That(captured.visibility.playerReferencePixels,Is.GreaterThan(128),"The unoccluded player silhouette must occupy the frame.");
                Assert.That(captured.visibility.npcReferencePixels,Is.GreaterThan(128),"The unoccluded Maya silhouette must occupy the frame.");
                Assert.That(captured.visibility.playerHeadReferencePixels,Is.GreaterThanOrEqualTo(64),"The unoccluded player's head region must be inside the frame.");
                Assert.That(captured.visibility.npcHeadReferencePixels,Is.GreaterThanOrEqualTo(64),"The unoccluded Maya head region must be inside the frame.");
                Assert.That(captured.visibility.playerHeadVisiblePixels,Is.GreaterThanOrEqualTo(16),
                    "The world hides the player's entire head region in a native conversation. See "+stem+"-diagnostic.json and the ordinary capture.");
                Assert.That(captured.visibility.npcHeadVisiblePixels,Is.GreaterThanOrEqualTo(16),
                    "The world hides Maya's entire head region in a native conversation. See "+stem+"-diagnostic.json and the ordinary capture.");
                report.completed=true;
            }
            finally
            {
                director.ClosePanels();director.TalkSpotsInBatchRuns=oldSpots;
                NpcWrite("npcApproachDiagnosticsSuppressed",oldSuppressed);NpcWrite("nextAmbientActivity",oldAmbient);
                NpcWrite("reducedMotion",oldReduced);cameraRig.SetReducedMotion(oldRigReduced);
                for(int i=0;i<visuals.Length;i++)if(visuals[i]!=null)visuals[i].SetReducedMotion(oldBodyReduced[i]);
                report.finishedUtc=DateTime.UtcNow.ToString("o");
                File.WriteAllText(ConversationCameraArtifact(stem+"-diagnostic.json"),JsonUtility.ToJson(report,true));
            }
        }

        private ConversationCameraFrame ReadConversationCameraFrame(string moment,HouseNpc npc)
        {
            var view=cameraRig.ViewCamera;
            var frame=new ConversationCameraFrame {moment=moment,realtime=Time.realtimeSinceStartup,player=ConversationCameraActor(player.transform),
                npc=ConversationCameraActor(npc.transform),eye=view.transform.position,forward=view.transform.forward,rigFocus=cameraRig.transform.position,
                desiredFocus=cameraRig.DesiredFocus,distance=cameraRig.Distance,appliedDistance=cameraRig.AppliedDistance,pitch=cameraRig.Pitch,
                yaw=cameraRig.Yaw,fieldOfView=view.fieldOfView,nearClip=view.nearClipPlane,windowOffset=cameraRig.ConversationWindowOffset,
                talkingTo=director.TalkingToId,walkingTo=director.WalkingToId,talkSpot=director.TalkSpotId,talkSpotSeated=director.TalkSpotSeated};
            var active=typeof(HouseCameraRig).GetField("activeShot",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(cameraRig);
            if(active is HouseCameraRig.Shot shot)
            {
                frame.hasShot=true;
                frame.shot=new ConversationCameraShotFrame {focus=shot.Focus,distance=shot.Distance,pitch=shot.Pitch,yaw=shot.Yaw,
                    fieldOfView=shot.FieldOfView,seconds=shot.Seconds,depthOfFieldWeight=shot.DepthOfFieldWeight,
                    keepYaw=shot.KeepYaw,orthographic=shot.Orthographic,orthographicSize=shot.OrthographicSize};
            }
            frame.furnitureAtEye=Physics.OverlapSphere(frame.eye,.18f,1<<HouseLayers.Furniture,QueryTriggerInteraction.Ignore)
                .Where(c=>c.gameObject.scene==player.gameObject.scene).Select(c=>ConversationCameraObstacle(c,frame.eye)).OrderBy(c=>c.path).ToArray();
            frame.playerHeadObstacles=ConversationCameraHeadObstacles(player.transform,npc.transform);
            frame.npcHeadObstacles=ConversationCameraHeadObstacles(npc.transform,player.transform);
            return frame;
        }

        private static ConversationCameraActorFrame ConversationCameraActor(Transform actor)
        {
            var seat=actor.GetComponent<HouseSeatPresentation>();var visual=actor.GetComponent<CharacterPresentation>();
            var owned=visual?.VisualRoot;
            var head=actor.GetComponentsInChildren<Animator>().FirstOrDefault(a=>a.isHuman && a.isActiveAndEnabled)?.GetBoneTransform(HumanBodyBones.Head);
            return new ConversationCameraActorFrame {name=actor.name,root=actor.position,visualRoot=owned!=null?owned.position:actor.position,
                head=head!=null?head.position:seat!=null && seat.Active?seat.VisualFocus:actor.position+Vector3.up*1.5f,
                seated=seat!=null && seat.Active,seatSettled=seat!=null && seat.Settled,appearanceKey=visual?.AppearanceKey};
        }

        private ConversationCameraObstacleFrame[] ConversationCameraHeadObstacles(Transform actor,Transform partner)
        {
            var eye=cameraRig.ViewCamera.transform.position;var to=ConversationCameraActor(actor).head-eye;
            return Physics.RaycastAll(eye,to.normalized,to.magnitude,HouseLayers.Pick,QueryTriggerInteraction.Ignore)
                .Where(h=>h.collider.gameObject.scene==actor.gameObject.scene && !h.transform.IsChildOf(actor) && !h.transform.IsChildOf(partner))
                .OrderBy(h=>h.distance).Select(h=>ConversationCameraObstacle(h.collider,eye,h.distance)).ToArray();
        }

        private static ConversationCameraObstacleFrame ConversationCameraObstacle(Collider collider,Vector3 eye,float distance=-1f)
            => new ConversationCameraObstacleFrame {path=ConversationCameraPath(collider.transform),kind=collider.GetType().Name,layer=collider.gameObject.layer,
                bounds=collider.bounds,rayDistance=distance,containsEye=(collider.ClosestPoint(eye)-eye).sqrMagnitude<.000001f};

        private static string ConversationCameraPath(Transform node)
        {
            var names=new List<string>();for(var at=node;at!=null;at=at.parent)names.Add(at.name);
            names.Reverse();return string.Join("/",names);
        }

        /// <summary>
        /// The actual camera/pose, with UI temporarily hidden: unique unlit silhouettes are read
        /// first through the world, then with only the other renderers disabled. The ratio measures
        /// rendered world obstruction, including surfaces without colliders; it does not grade art.
        /// All materials, renderer/canvas states, post-processing and readback targets are restored.
        /// </summary>
        private ConversationCameraVisibility ProbeConversationPairVisibility(HouseNpc npc,string stem)
        {
            var view=cameraRig.ViewCamera;var target=view.targetTexture;
            Assert.That(target,Is.Not.Null,"The probe runs inside the ordinary capture's established lens.");
            var roots=new[]{player.GetComponent<CharacterPresentation>().VisualRoot,npc.GetComponent<CharacterPresentation>().VisualRoot};
            var pair=roots.Select(root=>root.GetComponentsInChildren<Renderer>().Where(r=>r.enabled && r.gameObject.activeInHierarchy).ToArray()).ToArray();
            Assert.That(pair.All(renderers=>renderers.Length>0),Is.True,"Both owned visuals need renderers.");
            var saved=pair.SelectMany(renderers=>renderers).Distinct().Select(r=>(Renderer:r,Materials:r.sharedMaterials)).ToArray();
            var others=SceneComponents<Renderer>().Where(r=>r.enabled && !saved.Any(owned=>owned.Renderer==r)).ToArray();
            var canvases=Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(c=>(Canvas:c,Enabled:c.enabled)).ToArray();
            var data=view.GetComponent<UniversalAdditionalCameraData>();bool post=data!=null && data.renderPostProcessing;
            var anti=data!=null?data.antialiasing:AntialiasingMode.None;
            var active=RenderTexture.active;Material pink=null,green=null;Texture2D baseline=null,mask=null,reference=null;
            try
            {
                var shader=Shader.Find("Universal Render Pipeline/Unlit");Assert.That(shader,Is.Not.Null,"The project's URP unlit shader is required for diagnostic silhouettes.");
                pink=new Material(shader){name="Temporary conversation player mask"};pink.SetColor("_BaseColor",Color.magenta);
                green=new Material(shader){name="Temporary conversation NPC mask"};green.SetColor("_BaseColor",Color.green);
                foreach(var state in canvases)if(state.Canvas!=null)state.Canvas.enabled=false;
                if(data!=null){data.renderPostProcessing=false;data.antialiasing=AntialiasingMode.None;}
                baseline=ReadConversationCameraProbe(view,target);
                for(int i=0;i<pair.Length;i++)foreach(var renderer in pair[i])renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>i==0?pink:green).ToArray();
                mask=ReadConversationCameraProbe(view,target);
                foreach(var renderer in others)if(renderer!=null)renderer.enabled=false;
                reference=ReadConversationCameraProbe(view,target);
                var before=baseline.GetPixels32();var seen=mask.GetPixels32();var expected=reference.GetPixels32();
                var result=new ConversationCameraVisibility {probeWidth=target.width,probeHeight=target.height,
                    playerHeadRegion=ConversationCameraHeadRegion(view,ConversationCameraActor(player.transform).head,target),
                    npcHeadRegion=ConversationCameraHeadRegion(view,ConversationCameraActor(npc.transform).head,target),
                    maskPath=ConversationCameraArtifact(stem+"-world-mask.png"),referencePath=ConversationCameraArtifact(stem+"-unoccluded-reference.png")};
                for(int pixel=0;pixel<seen.Length;pixel++)
                {
                    var point=new Vector2(pixel%target.width,pixel/target.width);
                    bool myHead=result.playerHeadRegion.Contains(point),theirHead=result.npcHeadRegion.Contains(point);
                    if(ConversationCameraMask(expected[pixel],true)){result.playerReferencePixels++;if(myHead)result.playerHeadReferencePixels++;}
                    if(ConversationCameraMask(expected[pixel],false)){result.npcReferencePixels++;if(theirHead)result.npcHeadReferencePixels++;}
                    int changed=Math.Abs(seen[pixel].r-before[pixel].r)+Math.Abs(seen[pixel].g-before[pixel].g)+Math.Abs(seen[pixel].b-before[pixel].b);
                    if(changed<80)continue;
                    if(ConversationCameraMask(seen[pixel],true)){result.playerVisiblePixels++;if(myHead)result.playerHeadVisiblePixels++;}
                    if(ConversationCameraMask(seen[pixel],false)){result.npcVisiblePixels++;if(theirHead)result.npcHeadVisiblePixels++;}
                }
                result.playerVisibleFraction=result.playerReferencePixels==0?0:(float)result.playerVisiblePixels/result.playerReferencePixels;
                result.npcVisibleFraction=result.npcReferencePixels==0?0:(float)result.npcVisiblePixels/result.npcReferencePixels;
                result.playerHeadVisibleFraction=result.playerHeadReferencePixels==0?0:(float)result.playerHeadVisiblePixels/result.playerHeadReferencePixels;
                result.npcHeadVisibleFraction=result.npcHeadReferencePixels==0?0:(float)result.npcHeadVisiblePixels/result.npcHeadReferencePixels;
                File.WriteAllBytes(result.maskPath,mask.EncodeToPNG());File.WriteAllBytes(result.referencePath,reference.EncodeToPNG());
                return result;
            }
            finally
            {
                foreach(var state in saved)if(state.Renderer!=null)state.Renderer.sharedMaterials=state.Materials;
                foreach(var renderer in others)if(renderer!=null)renderer.enabled=true;
                foreach(var state in canvases)if(state.Canvas!=null)state.Canvas.enabled=state.Enabled;
                if(data!=null){data.renderPostProcessing=post;data.antialiasing=anti;}
                RenderTexture.active=active;
                foreach(var texture in new[]{baseline,mask,reference})if(texture!=null)Object.Destroy(texture);
                if(pink!=null)Object.Destroy(pink);if(green!=null)Object.Destroy(green);
            }
        }

        private static bool ConversationCameraMask(Color32 pixel,bool playerMask)
            => playerMask?pixel.r>180 && pixel.b>180 && pixel.g<70:pixel.g>180 && pixel.r<70 && pixel.b<70;

        private static Rect ConversationCameraHeadRegion(Camera view,Vector3 head,RenderTexture target)
        {
            var centre=view.WorldToViewportPoint(head);if(centre.z<=0)return default;
            var right=view.WorldToViewportPoint(head+view.transform.right*.24f);
            var up=view.WorldToViewportPoint(head+view.transform.up*.24f);
            float x=centre.x*target.width,y=centre.y*target.height;
            float halfWidth=Mathf.Abs(right.x-centre.x)*target.width,halfHeight=Mathf.Abs(up.y-centre.y)*target.height;
            return Rect.MinMaxRect(x-halfWidth,y-halfHeight,x+halfWidth,y+halfHeight);
        }

        private static Texture2D ReadConversationCameraProbe(Camera view,RenderTexture target)
        {
            view.Render();RenderTexture.active=target;
            var readback=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            try {readback.ReadPixels(new Rect(0,0,target.width,target.height),0,0);readback.Apply();return readback;}
            catch {Object.Destroy(readback);throw;}
        }

        private static string ConversationCameraArtifact(string name)=>Path.GetFullPath(Path.Combine(Application.dataPath,"..",name));

        [Serializable] private sealed class ConversationCameraReport
        {
            public int schema=1;
            public string route,startedUtc,finishedUtc,unityVersion,bodyProvider;
            public bool actorsPlacedByFixture,completed;
            public List<ConversationCameraFrame> frames=new List<ConversationCameraFrame>();
        }
        [Serializable] private sealed class ConversationCameraFrame
        {
            public string moment,talkingTo,walkingTo,talkSpot;
            public float realtime,distance,appliedDistance,pitch,yaw,fieldOfView,nearClip,windowOffset;
            public Vector3 eye,forward,rigFocus,desiredFocus;
            public bool hasShot,talkSpotSeated;
            public ConversationCameraShotFrame shot;
            public ConversationCameraActorFrame player,npc;
            public ConversationCameraObstacleFrame[] furnitureAtEye,playerHeadObstacles,npcHeadObstacles;
            public ConversationCameraVisibility visibility;
        }
        [Serializable] private sealed class ConversationCameraShotFrame
        {
            public Vector3 focus;
            public float distance,pitch,yaw,fieldOfView,seconds,depthOfFieldWeight,orthographicSize;
            public bool keepYaw,orthographic;
        }
        [Serializable] private sealed class ConversationCameraActorFrame
        {
            public string name,appearanceKey;
            public Vector3 root,visualRoot,head;
            public bool seated,seatSettled;
        }
        [Serializable] private sealed class ConversationCameraObstacleFrame
        {
            public string path,kind;
            public int layer;
            public Bounds bounds;
            public float rayDistance;
            public bool containsEye;
        }
        [Serializable] private sealed class ConversationCameraVisibility
        {
            public int probeWidth,probeHeight,playerVisiblePixels,npcVisiblePixels,playerReferencePixels,npcReferencePixels;
            public int playerHeadVisiblePixels,npcHeadVisiblePixels,playerHeadReferencePixels,npcHeadReferencePixels;
            public float playerVisibleFraction,npcVisibleFraction,playerHeadVisibleFraction,npcHeadVisibleFraction;
            public Rect playerHeadRegion,npcHeadRegion;
            public string maskPath,referencePath;
        }
    }
}
