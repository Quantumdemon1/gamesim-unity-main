using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gamesim.House
{
    /// <summary>Public house information, projected once per committed state, on the actual authored display faces.</summary>
    public sealed class HousePublicDisplays : MonoBehaviour
    {
        public const string PhotoName="HoH photo", PlaqueName="Reward plaque", YardName="Yard display";
        private readonly Dictionary<Renderer,TMP_Text> labels=new Dictionary<Renderer,TMP_Text>();
        private readonly List<Renderer> photos=new List<Renderer>();
        private static readonly string[] CompetitionLetterNames={"bb_set_sign_hoh","bb_set_sign_pillars","bb_set_sign_samehouse"};
        private readonly Dictionary<Canvas,bool> hiddenYardCanvases=new Dictionary<Canvas,bool>();
        private readonly Dictionary<Renderer,bool> hiddenYardLetters=new Dictionary<Renderer,bool>();
        private bool competitionBoardDrawn;
        public string HeadOfHouseholdId { get; private set; }
        public string HeadOfHouseholdName { get; private set; }
        public string YardText { get; private set; }
        public int PortraitCount=>photos.Count;

        public static HousePublicDisplays Find(Scene scene)=>!scene.IsValid() || !scene.isLoaded?null
            :scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<HousePublicDisplays>(true)).FirstOrDefault();

        public static void Project(Scene scene,EpisodeState state)
        {
            if(!scene.IsValid() || !scene.isLoaded || state==null)return;
            var display=Find(scene);
            if(display==null)
            {
                var holder=new GameObject("House public displays");SceneManager.MoveGameObjectToScene(holder,scene);
                display=holder.AddComponent<HousePublicDisplays>();
            }
            display.Refresh(state);
        }

        /// <summary>Only the public yard words yield to the arena's board/result/panel gate; physical scenery stays.</summary>
        public void SetCompetitionBoardDrawn(bool drawn)
        {
            if(competitionBoardDrawn==drawn)return;
            competitionBoardDrawn=drawn;
            if(!drawn){RestoreYardWords();return;}
            foreach(var pair in labels)
                if(pair.Key!=null && pair.Key.name==YardName && pair.Value!=null)HideYardCanvas(pair.Value);
            var world=gameObject.scene.GetRootGameObjects().FirstOrDefault(root=>root.name=="House Architecture");
            var course=world!=null?world.transform.Find("Set Pieces/Competition course"):null;
            if(course==null)return;
            foreach(string name in CompetitionLetterNames)
            {
                var model=course.Find(name);
                if(model==null)continue;
                foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    hiddenYardLetters.Add(renderer,renderer.enabled);
                    renderer.enabled=false;
                }
            }
        }

        private void HideYardCanvas(TMP_Text label)
        {
            var canvas=label.GetComponentInParent<Canvas>();
            if(canvas==null || canvas.renderMode!=RenderMode.WorldSpace)return;
            if(!hiddenYardCanvases.ContainsKey(canvas))hiddenYardCanvases.Add(canvas,canvas.enabled);
            canvas.enabled=false;
        }
        private void RestoreYardWords()
        {
            foreach(var pair in hiddenYardCanvases)if(pair.Key!=null)pair.Key.enabled=pair.Value;
            foreach(var pair in hiddenYardLetters)if(pair.Key!=null)pair.Key.enabled=pair.Value;
            hiddenYardCanvases.Clear();hiddenYardLetters.Clear();
        }
        private void OnDisable(){competitionBoardDrawn=false;RestoreYardWords();}
        private void OnDestroy(){competitionBoardDrawn=false;RestoreYardWords();}

        /// <summary>Reads public identities and announced competition winners; never events, scores, relationships or strategic terms.</summary>
        public static string PublicYardText(EpisodeState state)
        {
            if(state==null)return "The house";
            string Name(string id)=>state.Find(id)?.name;
            string hoh=Name(state.hohId),veto=Name(state.vetoHolderId);
            string progress=PhaseCaption(state.phase);
            string winner=null;
            switch(state.phase)
            {
                case EpisodePhase.HoH:if(state.competitionResolved)winner=hoh;break;
                case EpisodePhase.Veto:if(state.vetoResolved)winner=veto;break;
                case EpisodePhase.FinalHoHPart1:winner=Name(state.finalPart1WinnerId);break;
                case EpisodePhase.FinalHoHPart2:winner=Name(state.finalPart2WinnerId);break;
                case EpisodePhase.FinalHoHPart3:if(state.competitionResolved)winner=hoh;break;
            }
            bool competition=state.phase==EpisodePhase.HoH || state.phase==EpisodePhase.Veto
                || state.phase==EpisodePhase.FinalHoHPart1 || state.phase==EpisodePhase.FinalHoHPart2 || state.phase==EpisodePhase.FinalHoHPart3;
            if(competition)
                progress=EpisodeDirector.CompetitionTitleFor(state)+"\n"+(winner==null?"Competition in progress":"Winner: "+winner);
            if(state.phase==EpisodePhase.Finished)
                return "Week "+state.week+"\nSeason complete\nWinner: "+(Name(state.winnerId)??"To be announced");
            return "Week "+state.week+"\n"+progress+"\nHead of Household: "+(hoh??"Not yet decided")
                +"\nPower of Veto: "+(veto??"Not yet decided");
        }

        private static string PhaseCaption(EpisodePhase phase)
        {
            switch(phase)
            {
                case EpisodePhase.Social:return "Time in the house";
                case EpisodePhase.Nomination:return "Nominations";
                case EpisodePhase.VetoSelection:return "Veto player selection";
                case EpisodePhase.VetoMeeting:return "Veto meeting";
                case EpisodePhase.Campaign:return "Campaigning";
                case EpisodePhase.Eviction:return "Eviction night";
                case EpisodePhase.FinalEviction:return "The final choice";
                case EpisodePhase.JuryQuestioning:return "Jury questions";
                case EpisodePhase.FinalSpeeches:return "Final speeches";
                case EpisodePhase.Jury:return "The jury vote";
                default:return "The house";
            }
        }

        private void Refresh(EpisodeState state)
        {
            var hoh=state.Find(state.hohId);
            HeadOfHouseholdId=hoh?.id;HeadOfHouseholdName=hoh?.name;YardText=PublicYardText(state);
            var renderers=gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Renderer>(true))
                .Where(r=>r.enabled && r.gameObject.activeInHierarchy && (r.name==PhotoName || r.name==PlaqueName || r.name==YardName)).ToArray();
            MountHoHAssembly(renderers);
            photos.Clear();
            foreach(var renderer in renderers)
            {
                if(renderer.name==PhotoName)
                {
                    photos.Add(renderer);
                    var binding=renderer.GetComponent<CharacterPortraitMaterialBinding>()??renderer.gameObject.AddComponent<CharacterPortraitMaterialBinding>();
                    if(hoh==null)
                    {
                        // With no current HoH, no family-placeholder or previous winner is a face.
                        var material=renderer.material;
                        if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",null);
                        if(material.HasProperty("_MainTex"))material.SetTexture("_MainTex",null);
                        if(material.HasProperty("_EmissionMap"))material.SetTexture("_EmissionMap",null);
                    }
                    binding.Set(hoh);
                    continue;
                }
                if(!labels.TryGetValue(renderer,out var label) || label==null)
                {
                    label=MountLabel(renderer);
                    if(label==null)continue;
                    labels[renderer]=label;
                }
                label.text=renderer.name==YardName?YardText:"HEAD OF HOUSEHOLD\n"+(hoh?.name??"Not yet decided");
                // Projection continues while the canvas is hidden; returning to the yard reads
                // the latest committed public state rather than the state before the board.
                if(competitionBoardDrawn && renderer.name==YardName)HideYardCanvas(label);
            }
            foreach(var target in labels.Keys.Where(r=>r==null).ToArray())labels.Remove(target);
        }

        private void MountHoHAssembly(Renderer[] publicFaces)
        {
            var portraits=publicFaces.Where(r=>r.name==PhotoName).ToArray();
            if(portraits.Length!=4 || portraits.Any(r=>r.transform.parent!=portraits[0].transform.parent))return;
            var parent=portraits[0].transform.parent;
            var members=parent.GetComponentsInChildren<Renderer>(true).Where(r=>r.transform.parent==parent
                && (r.name==PhotoName || r.name=="HoH photo frame" || r.name==PlaqueName)).ToArray();
            if(members.Count(r=>r.name==PhotoName)!=4 || members.Count(r=>r.name=="HoH photo frame")!=4
                || members.Count(r=>r.name==PlaqueName)!=1)return;
            var all=gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Transform>(true)).ToArray();
            var wall=all.FirstOrDefault(t=>t.name=="South wing south wall");
            var floor=all.FirstOrDefault(t=>t.name=="HoH floor");
            if(!PhysicalBounds(wall,out var wallBounds) || !PhysicalBounds(floor,out var floorBounds))return;
            var assembly=members[0].bounds;
            foreach(var member in members.Skip(1))assembly.Encapsulate(member.bounds);
            var cabinet=all.Where(t=>t.name=="cabinetTelevision")
                .Select(t=>new{Transform=t,Renderers=t.GetComponentsInChildren<Renderer>(true)})
                .Where(value=>value.Renderers.Length>0).OrderBy(value=>
                    (value.Renderers[0].bounds.center-assembly.center).sqrMagnitude).FirstOrDefault();
            if(cabinet==null)return;
            var obstruction=cabinet.Renderers[0].bounds;
            foreach(var renderer in cabinet.Renderers.Skip(1))obstruction.Encapsulate(renderer.bounds);
            foreach(var collider in cabinet.Transform.GetComponentsInChildren<Collider>())
                if(collider.enabled && !collider.isTrigger)obstruction.Encapsulate(collider.bounds);
            // The authored television and its conservative physical proxy are taller than
            // the old console-top assumption. Move only the nine owned display objects.
            // A cutaway wall may have no vertical space; a side mount keeps its real height.
            const float gap=.03f;
            bool xOverlap=assembly.min.x<obstruction.max.x+gap-.0001f && assembly.max.x>obstruction.min.x-gap+.0001f;
            bool yOverlap=assembly.min.y<obstruction.max.y+gap && assembly.max.y>obstruction.min.y-gap;
            Vector3 offset=Vector3.zero;
            if(xOverlap && yOverlap && Mathf.Abs(assembly.center.z-obstruction.center.z)<=1.5f)
            {
                float upward=obstruction.max.y+gap-assembly.min.y;
                offset=Vector3.up*upward;
                if(assembly.max.y+upward>wallBounds.max.y)
                {
                    float left=obstruction.min.x-gap-assembly.max.x;
                    float right=obstruction.max.x+gap-assembly.min.x;
                    float min=Mathf.Max(wallBounds.min.x,floorBounds.min.x)+gap;
                    float max=Mathf.Min(wallBounds.max.x,floorBounds.max.x)-gap;
                    if(assembly.min.x+left>=min)offset=Vector3.right*left;
                    else if(assembly.max.x+right<=max)offset=Vector3.right*right;
                    else return; // No supported wall space is preferable to an invented surface.
                }
            }
            // bb_shell.py's cap is 35 mm proud of this supporting wall proxy. Native
            // saved-house triangle10 proved its room face at z=-19.84, while the old
            // plaque lay at -19.859. Keep the complete mount beyond that local trim
            // with the finish pass's existing 6 mm lift; the joined shell's bounds
            // cannot supply a local wall face, and player meshes need not be readable.
            const float capProud=.035f, finishLift=.006f;
            float roomSide=floorBounds.center.z>=wallBounds.center.z?1f:-1f;
            float wallPlane=roomSide>0?wallBounds.max.z:wallBounds.min.z;
            float nearDepth=roomSide>0?assembly.min.z:-assembly.max.z;
            float inward=roomSide*wallPlane+capProud+finishLift-nearDepth;
            if(inward>.0001f)offset.z=roomSide*inward;
            if(offset==Vector3.zero)return;
            foreach(var member in members)member.transform.position+=offset;
        }

        private static bool PhysicalBounds(Transform target,out Bounds bounds)
        {
            bounds=default;
            var filter=target!=null?target.GetComponent<MeshFilter>():null;
            if(filter==null || filter.sharedMesh==null)return false;
            var local=filter.sharedMesh.bounds;
            bounds=new Bounds(target.TransformPoint(local.center),Vector3.zero);
            for(int i=0;i<8;i++)bounds.Encapsulate(target.TransformPoint(new Vector3(
                (i&1)==0?local.min.x:local.max.x,(i&2)==0?local.min.y:local.max.y,(i&4)==0?local.min.z:local.max.z)));
            return true;
        }

        private static TMP_Text MountLabel(Renderer target)
        {
            // The finish pass uses a quad with local x/y as width/height. Read its actual mesh,
            // including authored scale, rather than placing another guessed screen in the yard.
            var filter=target.GetComponent<MeshFilter>();var mesh=filter!=null?filter.sharedMesh:null;
            if(mesh==null)return null;
            float width=Mathf.Abs(mesh.bounds.size.x*target.transform.lossyScale.x);
            float height=Mathf.Abs(mesh.bounds.size.y*target.transform.lossyScale.y);
            if(width<.01f || height<.01f)return null;
            const float reference=960f;
            var root=new GameObject("Public display",typeof(RectTransform),typeof(Canvas));
            root.transform.SetParent(target.transform,false);
            var rect=(RectTransform)root.transform;
            rect.sizeDelta=new Vector2(reference,reference*height/width);
            var scale=target.transform.lossyScale;
            float unit=width/reference;
            rect.localScale=new Vector3(unit/Mathf.Abs(scale.x),unit/Mathf.Abs(scale.y),unit/Mathf.Abs(scale.z));
            rect.SetPositionAndRotation(target.transform.position-target.transform.forward*.008f,target.transform.rotation);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=null;
            var backing=new GameObject("Public information",typeof(RectTransform),typeof(Image));
            backing.transform.SetParent(rect,false);
            var backingRect=(RectTransform)backing.transform;backingRect.anchorMin=Vector2.zero;backingRect.anchorMax=Vector2.one;
            backingRect.offsetMin=backingRect.offsetMax=Vector2.zero;
            var image=backing.GetComponent<Image>();image.color=new Color(.025f,.045f,.065f,1f);image.raycastTarget=false;
            var textObject=new GameObject("Public text",typeof(RectTransform),typeof(TextMeshProUGUI));
            textObject.transform.SetParent(backing.transform,false);
            var textRect=(RectTransform)textObject.transform;textRect.anchorMin=Vector2.zero;textRect.anchorMax=Vector2.one;
            textRect.offsetMin=new Vector2(32,20);textRect.offsetMax=new Vector2(-32,-20);
            var text=textObject.GetComponent<TextMeshProUGUI>();text.font=UiTheme.Font(UiTheme.Weight.SemiBold);
            text.color=Color.white;text.richText=false;text.raycastTarget=false;text.alignment=TextAlignmentOptions.Center;
            text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Ellipsis;
            text.enableAutoSizing=true;text.fontSize=56;text.fontSizeMax=56;text.fontSizeMin=28;
            return text;
        }
    }
}
