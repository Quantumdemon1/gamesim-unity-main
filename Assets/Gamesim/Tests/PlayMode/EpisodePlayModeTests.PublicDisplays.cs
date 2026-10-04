using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator PublicDisplays_RealCompetitionProjectsTheCurrentHoHAndClearsOldPortraits()
        {
            director.ClosePanels();yield return Frames(3);
            Assert.That(director.IsHouseUnderChrome,Is.False,"The initial public displays belong to the ordinary house view.");
            var displays=SceneComponents<HousePublicDisplays>().Single();
            var yard=SceneComponents<Renderer>().Single(r=>r.name==HousePublicDisplays.YardName);
            var yardLabel=yard.GetComponentInChildren<TMP_Text>();
            var yardCanvas=yardLabel.GetComponentInParent<Canvas>();
            var course=SceneComponents<Transform>().Single(t=>t.name=="Competition course");
            var lettering=new[]{"bb_set_sign_hoh","bb_set_sign_pillars","bb_set_sign_samehouse"}
                .SelectMany(name=>course.Find(name).GetComponentsInChildren<Renderer>(true))
                .Select(renderer=>(Renderer:renderer,Enabled:renderer.enabled)).ToArray();
            var scenery=course.Find("bb_set_comp_backdrop").GetComponentsInChildren<Renderer>(true)
                .Select(renderer=>(Renderer:renderer,Enabled:renderer.enabled)).ToArray();
            var photos=SceneComponents<Renderer>().Where(r=>r.name==HousePublicDisplays.PhotoName).ToArray();
            Assert.That(photos,Has.Length.EqualTo(4));
            AssertPublicHoHMount(photos);
            var mount=photos[0].transform.parent.GetComponentsInChildren<Renderer>(true)
                .Where(r=>r.name==HousePublicDisplays.PhotoName || r.name=="HoH photo frame" || r.name==HousePublicDisplays.PlaqueName)
                .Select(r=>(Renderer:r,Position:r.transform.position)).ToArray();
            Assert.That(displays.HeadOfHouseholdId,Is.EqualTo(director.Snapshot.hohId));
            Assert.That(displays.YardText,Does.Contain("Week "+director.Snapshot.week));
            Assert.That(photos.All(p=>PublicPhotoTexture(p)==null),Is.True,"No current HoH means no placeholder family is presented as their portrait.");
            AssertPublicPhotoEmission(photos,null);
            for(int guard=0;guard<100 && director.Snapshot.hohId==null;guard++)
            {
                var result=director.Submit(NextCommand(director.Snapshot));
                Assert.That(result.accepted,Is.True,result.reason);
                yield return null;
            }
            var state=director.Snapshot;var hoh=state.Find(state.hohId);
            Assert.That(hoh,Is.Not.Null,"A real competition must commit its HoH.");
            var resultCard=SceneComponents<CompetitionResult>().Single(card=>card.IsPlaying);
            Assert.That(CompetitionGameScreen.AnyDrawn,Is.False,"The committed result has replaced the attempt board.");
            yield return Frames(3);
            Assert.That(director.IsHouseUnderChrome,Is.True,"The real result still owns the house view.");
            AssertYardPublicWords(yardCanvas,lettering,scenery,true,"under the committed result, with no attempt board drawn");
            Assert.That(displays.HeadOfHouseholdId,Is.EqualTo(hoh.id));
            Assert.That(displays.HeadOfHouseholdName,Is.EqualTo(hoh.name));
            Assert.That(displays.YardText,Does.Contain(hoh.name));
            foreach(var surface in SceneComponents<Renderer>().Where(r=>r.name==HousePublicDisplays.YardName || r.name==HousePublicDisplays.PlaqueName))
            {
                var label=surface.GetComponentInChildren<TMP_Text>();
                Assert.That(label,Is.Not.Null,surface.name+" must carry a real world-space label.");
                Assert.That(label.text,Does.Contain(hoh.name));
                Assert.That(label.richText || label.raycastTarget,Is.False,"Public names are literal and the display cannot intercept house input.");
            }
            Assert.That(yardLabel.text,Is.EqualTo(HousePublicDisplays.PublicYardText(state)),
                "Hidden public projection already names the actual committed HoH.");
            if(CharacterBodySource.Provider is IModularCharacterBodyProvider)
            {
                Texture expected=null;float deadline=Time.realtimeSinceStartup+60;
                while(expected==null && Time.realtimeSinceStartup<deadline){expected=CharacterPortraits.Get(hoh);yield return null;}
                Assert.That(expected,Is.Not.Null,"The current HoH portrait must use the shared appearance pipeline.");
                deadline=Time.realtimeSinceStartup+2;
                while(photos.Any(p=>PublicPhotoTexture(p)!=expected) && Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(photos.All(p=>PublicPhotoTexture(p)==expected),Is.True,"Every HoH frame follows the exact same current person, not a prior winner.");
                AssertPublicPhotoEmission(photos,expected);
            }
            else Assert.That(photos.All(p=>PublicPhotoTexture(p)==null),Is.True,"NoUMA draws no incorrect face.");
            // Dismiss the actual persistent result through its native pointer control. A flag or
            // a substituted winner cannot make a world display photograph look correct.
            yield return new WaitForSecondsRealtime(.3f);
            yield return ClickInstrumentControl(resultCard.GetComponentsInChildren<Button>()
                .Single(button=>button.name=="Continue from competition results"));
            yield return Frames(3);
            Assert.That(resultCard.IsPlaying,Is.False,"The real Continue pointer release dismisses the result.");
            if(director.IsPanelOpen){yield return PressKey(Key.Escape);yield return Frames(3);}
            Assert.That(director.IsHouseUnderChrome,Is.False,"Native dismissal returns the actual world display to the house.");
            AssertYardPublicWords(yardCanvas,lettering,scenery,false,"after native result/panel dismissal");
            Assert.That(yardLabel.text,Is.EqualTo(HousePublicDisplays.PublicYardText(state)),"The latest committed words return with the canvas.");
            Assert.That(director.Snapshot.hohId,Is.EqualTo(state.hohId));
            Assert.That(director.Snapshot.revision,Is.EqualTo(state.revision));
            Assert.That(director.Snapshot.randomState,Is.EqualTo(state.randomState),"Presentation dismissal cannot advance the seeded season.");
            if(Application.isBatchMode)
            {
                // Frame one actual middle portrait and its closest real HoH plaque. Averaging all
                // photos could point between unrelated assemblies if the saved house changes.
                var portrait=photos.OrderBy(p=>p.bounds.center.x).ElementAt(photos.Length/2);
                var plaque=SceneComponents<Renderer>().Where(r=>r.name==HousePublicDisplays.PlaqueName)
                    .OrderBy(r=>(r.bounds.center-portrait.bounds.center).sqrMagnitude).First();
                Assert.That(Vector3.Distance(portrait.bounds.center,plaque.bounds.center),Is.LessThan(1.5f),
                    "The portrait witness must share an actual nearby HoH display assembly.");
                foreach(var dimensions in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080)})
                {
                    yield return CapturePublicSurfaces("house-public-hoh-"+dimensions.x+"x"+dimensions.y,dimensions,
                        new[]{portrait,plaque},plaque,state.hohId);
                    yield return CapturePublicSurfaces("house-public-yard-"+dimensions.x+"x"+dimensions.y,dimensions,
                        new[]{yard},yard,state.hohId);
                }
            }
            Assert.That(director.Snapshot.hohId,Is.EqualTo(state.hohId));
            Assert.That(director.Snapshot.revision,Is.EqualTo(state.revision));
            Assert.That(director.Snapshot.randomState,Is.EqualTo(state.randomState));
            AssertPublicHoHMount(photos);
            foreach(var member in mount)Assert.That(member.Renderer.transform.position,Is.EqualTo(member.Position),
                "Committed projection updates identity without drifting the physical mount.");
            var publicReset=state.Clone();publicReset.hohId=null;
            HousePublicDisplays.Project(player.gameObject.scene,publicReset);yield return null;
            Assert.That(displays.HeadOfHouseholdName,Is.Null);
            Assert.That(photos.All(p=>PublicPhotoTexture(p)==null),Is.True,"A week with no HoH must clear the previous portrait immediately.");
            AssertPublicPhotoEmission(photos,null);
            int labels=SceneComponents<TMP_Text>().Count(t=>t.name=="Public text");
            HousePublicDisplays.Project(player.gameObject.scene,publicReset);yield return null;
            Assert.That(SceneComponents<TMP_Text>().Count(t=>t.name=="Public text"),Is.EqualTo(labels),"Repeated projection reuses the actual surfaces.");
            AssertPublicHoHMount(photos);
            foreach(var member in mount)Assert.That(member.Renderer.transform.position,Is.EqualTo(member.Position),
                "Clearing and repeating the real public projection cannot creep any of the nine mounted objects.");
            HousePublicDisplays.Project(player.gameObject.scene,state);
        }

        private IEnumerator CapturePublicSurfaces(string name,Vector2Int dimensions,Renderer[] surfaces,Renderer labelled,string hohId)
        {
            var bounds=surfaces[0].bounds;
            foreach(var surface in surfaces.Skip(1))bounds.Encapsulate(surface.bounds);
            var face=surfaces[0].transform;
            var corners=surfaces.SelectMany(PublicSurfaceCorners).ToArray();
            float halfWidth=corners.Max(point=>Mathf.Abs(Vector3.Dot(point-bounds.center,face.right)));
            float halfHeight=corners.Max(point=>Mathf.Abs(Vector3.Dot(point-bounds.center,face.up)));
            const float fov=40f;
            // Place the real front-facing rig shot so the complete physical faces fit inside
            // the central 80% of the frame; this is framing, never visibility through an obstacle.
            float distance=Mathf.Max(.8f,Mathf.Max(halfHeight,halfWidth*dimensions.y/dimensions.x)
                /(Mathf.Tan(fov*.5f*Mathf.Deg2Rad)*.8f));
            CaptureLens lens=null;Texture2D frame=null;
            var canvasStates=new (Canvas Canvas,bool Enabled)[0];
            try
            {
                bool found=false;string rejected=null;int candidate=0;
                // Keep a useful front view first. These twelve modest physical alternatives
                // may avoid a live houseguest, but never accept sight through a solid face.
                foreach(float factor in new[]{1f,1.5f})
                {
                    foreach(float pitch in new[]{0f,20f})
                    {
                        foreach(float yawOffset in new[]{0f,-20f,20f})
                        {
                            candidate++;
                            cameraRig.MoveTo(new HouseCameraRig.Shot{Focus=bounds.center,Distance=distance*factor,Pitch=pitch,
                                Yaw=face.eulerAngles.y+yawOffset,FieldOfView=fov,Seconds=.01f});
                            float deadline=Time.realtimeSinceStartup+4f;
                            while(Time.realtimeSinceStartup<deadline && (cameraRig.IsTravelling || !cameraRig.HasArrived(.05f)
                                || Mathf.Abs(cameraRig.ViewCamera.fieldOfView-fov)>.5f))yield return null;
                            Assert.That(cameraRig.IsTravelling || !cameraRig.HasArrived(.05f),Is.False,"The real display shot must arrive.");
                            Assert.That(Mathf.Abs(cameraRig.ViewCamera.fieldOfView-fov),Is.LessThanOrEqualTo(.5f));
                            if(PublicSurfacesVisible(surfaces,cameraRig.ViewCamera,out rejected)){found=true;break;}
                            Debug.Log("[Gamesim] public display candidate "+candidate+" rejected: "+rejected);
                        }
                        if(found)break;
                    }
                    if(found)break;
                }
                Assert.That(found,Is.True,"No clear physical public display view in twelve bounded candidates: "+rejected);
                lens=new CaptureLens(cameraRig.ViewCamera,dimensions.x,dimensions.y);
                Canvas.ForceUpdateCanvases();yield return null;
                var preparation=lens.MakeSureTheCanvasesAreDrawn();
                try { while(preparation.MoveNext())yield return preparation.Current; }
                finally { (preparation as System.IDisposable)?.Dispose(); }
                // Suppress only screen UI for the synchronous world read. Actual world-space
                // public text, portraits, actors and saved scenery retain their real state.
                canvasStates=SceneComponents<Canvas>().Where(canvas=>canvas.name!=CaptureLens.GuardName
                    && canvas.renderMode!=RenderMode.WorldSpace).Select(canvas=>(Canvas:canvas,Enabled:canvas.enabled)).ToArray();
                foreach(var item in canvasStates)item.Canvas.enabled=false;
                Canvas.ForceUpdateCanvases();
                Assert.That(SceneComponents<CompetitionResult>().Any(card=>card.IsPlaying),Is.False);
                Assert.That(SceneComponents<Canvas>().Where(canvas=>canvas.name!=CaptureLens.GuardName
                    && canvas.renderMode!=RenderMode.WorldSpace).Any(canvas=>canvas.enabled),Is.False);
                var label=labelled.GetComponentInChildren<TMP_Text>();
                Assert.That(label,Is.Not.Null);Assert.That(label.GetComponentInParent<Canvas>().enabled,Is.True);
                Assert.That(label.text,Does.Contain(director.Snapshot.Find(hohId).name));
                Assert.That(director.Snapshot.hohId,Is.EqualTo(hohId));
                var eye=cameraRig.ViewCamera;
                Assert.That(PublicSurfacesVisible(surfaces,eye,out var obstruction),Is.True,
                    "The actual surfaces remain visible at synchronous read: "+obstruction);
                if(labelled.name==HousePublicDisplays.YardName)AssertOldWideYardTextIsBlocked(label,eye,surfaces);
                AssertPublicGlyphPixels(label,eye,dimensions);
                frame=lens.Read();
                string path=Path.GetFullPath(Path.Combine(Application.dataPath,"..",name+".png"));
                File.WriteAllBytes(path,frame.EncodeToPNG());
                AssertNotBlank(frame,name);
                var labelBox=PublicSurfaceBox(labelled,eye,dimensions);
                AssertRegionHasContent(frame,labelBox,name+": the actual public information face");
                Debug.Log("[Gamesim] public world capture -> "+path+"; actual HoH "+hohId+"; faces "+
                    string.Join(", ",surfaces.Select(surface=>surface.name+" @ "+surface.bounds.center))+
                    "; eye "+eye.transform.position+"; bounded candidate "+candidate+"; collider/opaque faces clear; information pixels "+labelBox);
            }
            finally
            {
                foreach(var item in canvasStates)if(item.Canvas!=null)item.Canvas.enabled=item.Enabled;
                lens?.Dispose();if(frame!=null)Object.Destroy(frame);
                cameraRig.ReleaseShot(0f);
                Canvas.ForceUpdateCanvases();
            }
        }

        private static Vector3[] PublicSurfaceCorners(Renderer surface)
        {
            var mesh=surface.GetComponent<MeshFilter>().sharedMesh.bounds;
            return new[]{new Vector3(mesh.min.x,mesh.min.y,mesh.center.z),new Vector3(mesh.min.x,mesh.max.y,mesh.center.z),
                new Vector3(mesh.max.x,mesh.min.y,mesh.center.z),new Vector3(mesh.max.x,mesh.max.y,mesh.center.z)}
                .Select(surface.transform.TransformPoint).ToArray();
        }

        private bool PublicSurfacesVisible(Renderer[] surfaces,Camera eye,out string failure)
        {
            var witnesses=surfaces.SelectMany(PublicSurfaceWitnesses).ToArray();
            var corridor=new Bounds(eye.transform.position,Vector3.zero);
            foreach(var witness in witnesses)corridor.Encapsulate(witness.Point);
            corridor.Expand(.02f);
            var geometry=new CompetitionInspectionGeometry(SceneComponents<Renderer>().Where(renderer=>renderer.enabled
                && renderer.gameObject.activeInHierarchy && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                && renderer.GetComponent<TMP_Text>()==null && !surfaces.Contains(renderer)
                && renderer.sharedMaterials.Any(CompetitionInspectionOpaque) && corridor.Intersects(renderer.bounds)).ToArray());
            var colliders=SceneComponents<Collider>().Where(collider=>collider.enabled && !collider.isTrigger
                && collider.gameObject.activeInHierarchy).ToArray();
            Assert.That(colliders.Length,Is.LessThanOrEqualTo(512),"Physical public sight collider inventory stays bounded.");
            foreach(var witness in witnesses)
            {
                var surface=witness.Surface;var point=witness.Point;
                var view=eye.WorldToViewportPoint(point);
                if(view.z<=eye.nearClipPlane || view.x<.02f || view.x>.98f || view.y<.02f || view.y>.98f)
                {failure=surface.name+" leaves the actual physical frame: "+view;return false;}
                var line=point-eye.transform.position;
                var ray=new Ray(eye.transform.position,line.normalized);
                foreach(var collider in colliders)
                {
                    if(collider.transform.IsChildOf(surface.transform))continue;
                    if(collider.bounds.Contains(eye.transform.position)
                        && (collider.ClosestPoint(eye.transform.position)-eye.transform.position).sqrMagnitude<1e-10f)
                    {failure="Actual eye is inside "+PublicHierarchy(collider.transform);return false;}
                    if(collider.Raycast(ray,out var hit,line.magnitude-.01f))
                    {failure=surface.name+" is physically blocked by "+PublicHierarchy(collider.transform)+" at "+hit.point;return false;}
                }
                if(geometry.Blocked(ray,line.magnitude-.01f,out failure))return false;
            }
            failure=null;return true;
        }

        private static IEnumerable<(Renderer Surface,Vector3 Point)> PublicSurfaceWitnesses(Renderer surface)
        {
            foreach(var point in PublicSurfaceCorners(surface).Append(surface.bounds.center))yield return (surface,point);
            var label=surface.GetComponentInChildren<TMP_Text>();
            if(label==null)yield break;
            label.ForceMeshUpdate();
            Assert.That(label.isTextTruncated,Is.False,"Public information must not lose any committed words to ellipsis.");
            int count=label.textInfo.characterCount;
            Assert.That(count,Is.LessThanOrEqualTo(512),"The actual public glyph witness inventory is bounded.");
            Assert.That(label.textInfo.characterInfo.Take(count).Any(character=>character.isVisible),Is.True);
            for(int i=0;i<count;i++)
            {
                var character=label.textInfo.characterInfo[i];if(!character.isVisible)continue;
                foreach(var point in new[]{character.bottomLeft,character.topLeft,character.topRight,character.bottomRight,
                    (character.bottomLeft+character.topRight)*.5f})
                    yield return (surface,label.transform.TransformPoint(point));
            }
        }

        private static void AssertPublicGlyphPixels(TMP_Text label,Camera eye,Vector2Int dimensions)
        {
            label.ForceMeshUpdate();
            Assert.That(label.isTextTruncated,Is.False);
            Assert.That(label.textBounds.size.x,Is.LessThanOrEqualTo(label.rectTransform.rect.width+.01f));
            Assert.That(label.textBounds.size.y,Is.LessThanOrEqualTo(label.rectTransform.rect.height+.01f));
            foreach(var character in label.textInfo.characterInfo.Take(label.textInfo.characterCount))
            {
                if(!character.isVisible || !char.IsLetterOrDigit(character.character))continue;
                var a=eye.WorldToViewportPoint(label.transform.TransformPoint(character.bottomLeft));
                var b=eye.WorldToViewportPoint(label.transform.TransformPoint(character.topLeft));
                Assert.That(Mathf.Abs(b.y-a.y)*dimensions.y,Is.GreaterThanOrEqualTo(8f),
                    "Every actual public letter/digit must retain eight native vertical pixels: "+character.character);
            }
        }

        private void AssertOldWideYardTextIsBlocked(TMP_Text label,Camera eye,Renderer[] surfaces)
        {
            string current=label.text;var rect=label.rectTransform;
            var min=rect.offsetMin;var max=rect.offsetMax;
            var state=director.Snapshot;
            Assert.That(state.phase,Is.EqualTo(EpisodePhase.HoH));Assert.That(state.competitionResolved,Is.True);
            string hoh=state.Find(state.hohId).name;
            try
            {
                // Reproduce both parts of the retained native defect on the same actual
                // hardware/eye and real committed identities, without taking a fake image.
                rect.offsetMin=new Vector2(32,min.y);rect.offsetMax=new Vector2(-32,max.y);
                label.text="Week "+state.week+"\n"+EpisodeDirector.CompetitionTitleFor(state)+"\nWinner: "+hoh
                    +"\nHead of Household: "+hoh+"\nPower of Veto: "+(state.Find(state.vetoHolderId)?.name??"Not yet decided");
                Canvas.ForceUpdateCanvases();label.ForceMeshUpdate();
                Assert.That(PublicSurfacesVisible(surfaces,eye,out var blocker),Is.False,
                    "The original wide rectangle plus long role/name lines must reproduce an actual glyph obstruction.");
                Assert.That(blocker,Does.Contain("opaque mesh face: bb_set_comp_gate"),
                    "The control must fail on the real retained gate, not on a looser viewport or missing-text predicate.");
            }
            finally
            {
                rect.offsetMin=min;rect.offsetMax=max;label.text=current;
                Canvas.ForceUpdateCanvases();label.ForceMeshUpdate();
            }
            Assert.That(PublicSurfacesVisible(surfaces,eye,out var restored),Is.True,
                "The real current public glyphs must again clear the actual opaque geometry after control cleanup: "+restored);
            Assert.That(label.text,Is.EqualTo(HousePublicDisplays.PublicYardText(state)));
        }

        private static string PublicHierarchy(Transform transform)=>transform.parent==null?transform.name
            :PublicHierarchy(transform.parent)+"/"+transform.name;

        private void AssertPublicHoHMount(Renderer[] photos)
        {
            var members=photos[0].transform.parent.GetComponentsInChildren<Renderer>(true).Where(r=>
                r.name==HousePublicDisplays.PhotoName || r.name=="HoH photo frame" || r.name==HousePublicDisplays.PlaqueName).ToArray();
            Assert.That(members,Has.Length.EqualTo(9));
            var bounds=members[0].bounds;foreach(var member in members.Skip(1))bounds.Encapsulate(member.bounds);
            var cabinet=SceneComponents<Transform>().Where(t=>t.name=="cabinetTelevision")
                .OrderBy(t=>(t.GetComponentsInChildren<Renderer>()[0].bounds.center-bounds.center).sqrMagnitude).First();
            var physical=cabinet.GetComponentsInChildren<Renderer>()[0].bounds;
            foreach(var renderer in cabinet.GetComponentsInChildren<Renderer>().Skip(1))physical.Encapsulate(renderer.bounds);
            foreach(var collider in cabinet.GetComponentsInChildren<Collider>())if(collider.enabled && !collider.isTrigger)physical.Encapsulate(collider.bounds);
            Assert.That(bounds.min.y>=physical.max.y+.029f || bounds.max.x<=physical.min.x-.029f || bounds.min.x>=physical.max.x+.029f,Is.True,
                "The complete actual portrait/frame/plaque group must clear the saved television and its physical proxy.");
            var wall=SceneComponents<Transform>().Single(t=>t.name=="South wing south wall");
            var floor=SceneComponents<Transform>().Single(t=>t.name=="HoH floor");
            Bounds WorldBounds(Transform t){var local=t.GetComponent<MeshFilter>().sharedMesh.bounds;
                var world=new Bounds(t.TransformPoint(local.center),Vector3.zero);for(int i=0;i<8;i++)world.Encapsulate(t.TransformPoint(new Vector3(
                    (i&1)==0?local.min.x:local.max.x,(i&2)==0?local.min.y:local.max.y,(i&4)==0?local.min.z:local.max.z)));return world;}
            var supporting=WorldBounds(wall);var room=WorldBounds(floor);
            Assert.That(bounds.min.y,Is.GreaterThanOrEqualTo(supporting.min.y));
            Assert.That(bounds.max.y,Is.LessThanOrEqualTo(supporting.max.y));
            Assert.That(bounds.min.x,Is.GreaterThanOrEqualTo(Mathf.Max(supporting.min.x,room.min.x)));
            Assert.That(bounds.max.x,Is.LessThanOrEqualTo(Mathf.Min(supporting.max.x,room.max.x)));
            float roomSide=room.center.z>=supporting.center.z?1f:-1f;
            float wallPlane=roomSide>0?supporting.max.z:supporting.min.z;
            float nearDepth=roomSide>0?bounds.min.z:-bounds.max.z;
            Assert.That(nearDepth-roomSide*wallPlane,Is.GreaterThanOrEqualTo(.0409f),
                "All nine owned faces must retain the 35 mm authored trim plus the 6 mm finish lift, within 0.1 mm bounds precision.");
            // Independently test the actual imported opaque shell, not just its proxy or
            // a duplicated mount formula. Each physical face must be on the room side;
            // the original plaque's upper corners intersected the real cap triangle10.
            var shell=SceneComponents<MeshRenderer>().Single(renderer=>renderer.name=="bb_shell_house");
            var geometry=new CompetitionInspectionGeometry(new Renderer[]{shell});
            var normal=Vector3.forward*roomSide;
            foreach(var member in members)
            foreach(var point in PublicSurfaceCorners(member).Append(member.bounds.center))
                Assert.That(geometry.Blocked(new Ray(point+normal*.1f,-normal),.099f,out var blocker),Is.False,
                    member.name+" must be in front of the actual opaque supporting shell: "+blocker);
        }

        private static Rect PublicSurfaceBox(Renderer surface,Camera eye,Vector2Int dimensions)
        {
            var points=PublicSurfaceCorners(surface).Select(eye.WorldToViewportPoint).ToArray();
            // ReadPixels is bottom-left, matching viewport coordinates.
            return Rect.MinMaxRect(points.Min(point=>point.x)*dimensions.x,points.Min(point=>point.y)*dimensions.y,
                points.Max(point=>point.x)*dimensions.x,points.Max(point=>point.y)*dimensions.y);
        }

        private static Texture PublicPhotoTexture(Renderer renderer)
        {
            var material=renderer.material;
            return material.HasProperty("_BaseMap")?material.GetTexture("_BaseMap")
                :material.HasProperty("_MainTex")?material.GetTexture("_MainTex"):null;
        }

        private static void AssertPublicPhotoEmission(Renderer[] photos,Texture expected)
        {
            foreach(var photo in photos)
                if(photo.material.HasProperty("_EmissionMap"))
                    Assert.That(photo.material.GetTexture("_EmissionMap"),Is.SameAs(expected),"The emissive finish must show the current portrait or none, never its static placeholder.");
        }
    }
}
