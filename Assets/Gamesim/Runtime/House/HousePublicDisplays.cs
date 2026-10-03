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
        public string HeadOfHouseholdId { get; private set; }
        public string HeadOfHouseholdName { get; private set; }
        public string YardText { get; private set; }
        public int PortraitCount=>photos.Count;

        public static void Project(Scene scene,EpisodeState state)
        {
            if(!scene.IsValid() || !scene.isLoaded || state==null)return;
            var display=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<HousePublicDisplays>(true)).FirstOrDefault();
            if(display==null)
            {
                var holder=new GameObject("House public displays");SceneManager.MoveGameObjectToScene(holder,scene);
                display=holder.AddComponent<HousePublicDisplays>();
            }
            display.Refresh(state);
        }

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
            }
            foreach(var target in labels.Keys.Where(r=>r==null).ToArray())labels.Remove(target);
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
