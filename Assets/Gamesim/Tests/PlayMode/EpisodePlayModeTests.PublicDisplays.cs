using System.Collections;
using System.IO;
using System.Linq;
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
            var publicReset=state.Clone();publicReset.hohId=null;
            HousePublicDisplays.Project(player.gameObject.scene,publicReset);yield return null;
            Assert.That(displays.HeadOfHouseholdName,Is.Null);
            Assert.That(photos.All(p=>PublicPhotoTexture(p)==null),Is.True,"A week with no HoH must clear the previous portrait immediately.");
            AssertPublicPhotoEmission(photos,null);
            int labels=SceneComponents<TMP_Text>().Count(t=>t.name=="Public text");
            HousePublicDisplays.Project(player.gameObject.scene,publicReset);yield return null;
            Assert.That(SceneComponents<TMP_Text>().Count(t=>t.name=="Public text"),Is.EqualTo(labels),"Repeated projection reuses the actual surfaces.");
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
                cameraRig.MoveTo(new HouseCameraRig.Shot{Focus=bounds.center,Distance=distance,Pitch=0,
                    Yaw=face.eulerAngles.y,FieldOfView=fov,Seconds=.01f});
                float deadline=Time.realtimeSinceStartup+4f;
                while(Time.realtimeSinceStartup<deadline && (cameraRig.IsTravelling || !cameraRig.HasArrived(.05f)
                    || Mathf.Abs(cameraRig.ViewCamera.fieldOfView-fov)>.5f))yield return null;
                Assert.That(cameraRig.IsTravelling || !cameraRig.HasArrived(.05f),Is.False,"The real display shot must arrive.");
                Assert.That(Mathf.Abs(cameraRig.ViewCamera.fieldOfView-fov),Is.LessThanOrEqualTo(.5f));
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
                foreach(var surface in surfaces)AssertPublicSurfaceSight(surface,eye);
                frame=lens.Read();
                string path=Path.GetFullPath(Path.Combine(Application.dataPath,"..",name+".png"));
                File.WriteAllBytes(path,frame.EncodeToPNG());
                AssertNotBlank(frame,name);
                var labelBox=PublicSurfaceBox(labelled,eye,dimensions);
                AssertRegionHasContent(frame,labelBox,name+": the actual public information face");
                Debug.Log("[Gamesim] public world capture -> "+path+"; actual HoH "+hohId+"; faces "+
                    string.Join(", ",surfaces.Select(surface=>surface.name+" @ "+surface.bounds.center))+
                    "; eye "+eye.transform.position+"; information pixels "+labelBox);
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

        private static void AssertPublicSurfaceSight(Renderer surface,Camera eye)
        {
            foreach(var point in PublicSurfaceCorners(surface).Append(surface.bounds.center))
            {
                var view=eye.WorldToViewportPoint(point);
                Assert.That(view.z,Is.GreaterThan(eye.nearClipPlane));
                Assert.That(view.x,Is.InRange(.02f,.98f),surface.name+" must fit inside the physical frame.");
                Assert.That(view.y,Is.InRange(.02f,.98f),surface.name+" must fit inside the physical frame.");
                var line=point-eye.transform.position;
                foreach(var collider in surface.gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Collider>()))
                    if(collider.enabled && !collider.isTrigger && !collider.transform.IsChildOf(surface.transform)
                        && collider.Raycast(new Ray(eye.transform.position,line.normalized),out var hit,line.magnitude-.01f))
                        Assert.Fail(surface.name+" is physically blocked by "+collider.name+" at "+hit.point+".");
            }
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
