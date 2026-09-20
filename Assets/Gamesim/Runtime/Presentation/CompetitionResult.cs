using System.Collections.Generic;
using TMPro;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>Committed standings remain readable until explicitly dismissed.</summary>
    [DisallowMultipleComponent]
    public sealed class CompetitionResult : MonoBehaviour
    {
        public readonly struct Standing
        {
            public readonly string Name;
            public readonly double Score;
            public readonly bool IsWinner, IsPlayer;
            public readonly Texture Portrait;
            public readonly ContestantState Character;
            public Standing(string name,double score,bool isWinner,bool isPlayer,Texture portrait,ContestantState character=null)
            { Name=name;Score=score;IsWinner=isWinner;IsPlayer=isPlayer;Portrait=portrait;Character=character; }
        }

        private CanvasGroup group;
        private RectTransform column, scrim, standingsPanel, detailsPanel;
        private float elapsed;
        private bool playing, reduced;
        private int dismissedFrame = -10;
        private Button continueButton, detailsButton;
        private bool showingDetails;
        public float FontScale { get; set; } = 1f;
        public bool IsPlaying => playing;
        public bool OwnsInput => playing || Time.frameCount <= dismissedFrame + 1;
        public event System.Action VisibilityChanged;

        public static CompetitionResult Attach(GameObject owner)
        {
            var root=new GameObject("Gamesim Competition Result",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),
                typeof(CanvasGroup),typeof(GraphicRaycaster));
            if(owner!=null&&owner.scene.IsValid())SceneManager.MoveGameObjectToScene(root,owner.scene);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=105;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            var card=root.AddComponent<CompetitionResult>();card.group=root.GetComponent<CanvasGroup>();card.Cancel();
            card.dismissedFrame=-10;return card;
        }

        public bool Play(string award,string category,int week,IList<Standing> standings,bool reducedMotion,string explanation=null)
        {
            if(standings==null||standings.Count==0)return false;
            Build(award,category,week,standings,explanation);
            reduced=reducedMotion;elapsed=0;playing=true;
            group.alpha=reduced?1:.01f;group.interactable=true;group.blocksRaycasts=true;
            column.gameObject.SetActive(true);scrim.gameObject.SetActive(true);
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
            VisibilityChanged?.Invoke();
            return true;
        }

        public void Cancel()
        {
            bool wasPlaying = playing;
            if(playing)dismissedFrame=Time.frameCount;
            playing=false;
            if(group!=null){group.alpha=0;group.interactable=false;group.blocksRaycasts=false;}
            if(column!=null)column.gameObject.SetActive(false);
            if(scrim!=null)scrim.gameObject.SetActive(false);
            if(EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject!=null
                && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
                EventSystem.current.SetSelectedGameObject(null);
            if (wasPlaying) VisibilityChanged?.Invoke();
        }

        private void Dismiss(){if(playing&&elapsed>=.25f)Cancel();}

        private void Update()
        {
            if(!playing)return;
            elapsed+=Time.unscaledDeltaTime;group.alpha=reduced?1:Mathf.Clamp01(elapsed/.3f);
            var keyboard=Keyboard.current;var pad=Gamepad.current;
            bool back=(keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame)||(pad!=null&&pad.buttonEast.wasPressedThisFrame);
            if(back){if(showingDetails)ToggleDetails();else Dismiss();return;}
            var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            if((selected==null||selected==continueButton.gameObject)
                && ((keyboard!=null&&keyboard.enterKey.wasPressedThisFrame)||(pad!=null&&pad.buttonSouth.wasPressedThisFrame)))Dismiss();
        }

        private void Build(string award,string category,int week,IList<Standing> standings,string explanation)
        {
            if(column!=null){column.gameObject.SetActive(false);Destroy(column.gameObject);}
            if(scrim==null)
            {
                scrim=HudPrimitives.Fill("Scrim",transform,new Color(.025f,.04f,.07f,.97f),1);
                scrim.anchorMin=Vector2.zero;scrim.anchorMax=Vector2.one;scrim.offsetMin=scrim.offsetMax=Vector2.zero;
                scrim.GetComponent<Image>().raycastTarget=true;
            }
            column=new GameObject("Card",typeof(RectTransform)).GetComponent<RectTransform>();column.SetParent(transform,false);
            column.anchorMin=column.anchorMax=new Vector2(.5f,.5f);column.pivot=new Vector2(.5f,.5f);
            column.sizeDelta=new Vector2(1180,780);
            var glass=HudPrimitives.Fill("Card glass",column,UiTheme.GlassFill,UiTheme.GlassRadius);
            glass.anchorMin=Vector2.zero;glass.anchorMax=Vector2.one;glass.offsetMin=glass.offsetMax=Vector2.zero;
            UiTheme.Glass(glass,UiTheme.GlassRadius);
            Label("Week",column,"WEEK "+Mathf.Max(1,week)+" · "+category,16,32,18,1116,26,UiTheme.Muted);
            Label("Award",column,(award??"Competition").ToUpperInvariant(),30,32,48,1116,48,UiTheme.Paper);
            var winner=standings[0];foreach(var row in standings)if(row.IsWinner)winner=row;
            var portrait=HudPrimitives.Portrait(column,winner.Portrait,UiTheme.Gold,68,3,false,winner.Character);Place(portrait,32,107,68,68);
            Label("Winner",column,winner.IsPlayer?winner.Name+" · You win!":winner.Name+" wins!",27,119,112,998,54,UiTheme.Gold);
            showingDetails=false;
            standingsPanel=new GameObject("Competition standings",typeof(RectTransform)).GetComponent<RectTransform>();
            standingsPanel.SetParent(column,false);standingsPanel.anchorMin=Vector2.zero;standingsPanel.anchorMax=Vector2.one;
            standingsPanel.offsetMin=standingsPanel.offsetMax=Vector2.zero;
            Label("Standings heading",standingsPanel,"COMMITTED STANDINGS     ·     Scores are relative to this competition",16,32,185,1116,28,UiTheme.Muted);
            double best=0;foreach(var entry in standings)best=System.Math.Max(best,entry.Score);if(best<=0)best=1;
            // Twelve fixed rows fit at both supported text sizes without hiding the footer.
            for(int i=0;i<standings.Count;i++)
            {
                var entry=standings[i];float y=224+i*34;
                var row=HudPrimitives.Fill("Standing "+(i+1),standingsPanel,entry.IsWinner?new Color(.28f,.23f,.10f):UiTheme.Surface,5);
                Place(row,32,y,1116,30);
                Label("Rank",row,(i+1).ToString(),17,10,0,34,30,UiTheme.Muted);
                Label("Name",row,entry.Name+(entry.IsPlayer?" (You)":""),18,52,0,430,30,entry.IsWinner?UiTheme.Gold:UiTheme.Paper);
                var track=HudPrimitives.Fill("Track",row,UiTheme.Ink,3);Place(track,490,11,516,8);
                var bar=HudPrimitives.Fill("Bar",row,entry.IsWinner?UiTheme.Gold:UiTheme.Accent,3);
                Place(bar,490,11,Mathf.Max(2,516*Mathf.Clamp01((float)(entry.Score/best))),8);
                Label("Score",row,entry.Score.ToString("0.00"),17,1024,0,82,30,UiTheme.Paper);
            }
            Label("Performance explanation",standingsPanel,"Character statistics, modifiers and seeded rolls determine placement. Choose Score details to review the available explanation. Perfect performance does not guarantee a win.",
                17,32,646,1116,74,UiTheme.Muted);
            detailsPanel=new GameObject("Competition score details",typeof(RectTransform)).GetComponent<RectTransform>();
            detailsPanel.SetParent(column,false);detailsPanel.anchorMin=Vector2.zero;detailsPanel.anchorMax=Vector2.one;
            detailsPanel.offsetMin=detailsPanel.offsetMax=Vector2.zero;
            Label("Score details heading",detailsPanel,"SCORING EXPLANATION · Committed result",20,32,190,1116,42,UiTheme.Gold);
            Label("Full performance explanation",detailsPanel,explanation??"Character statistics, modifiers and seeded rolls determine placement. Minigame performance provides a bounded bonus, so perfect play does not guarantee a win.",
                20,32,248,1116,440,UiTheme.Paper);
            detailsPanel.gameObject.SetActive(false);
            var detailsRect=HudPrimitives.Fill("Review competition score details",column,UiTheme.AccentDeep,8);
            Place(detailsRect,32,726,368,42);detailsRect.GetComponent<Image>().raycastTarget=true;
            detailsButton=detailsRect.gameObject.AddComponent<Button>();detailsButton.targetGraphic=detailsRect.GetComponent<Image>();
            detailsButton.onClick.AddListener(ToggleDetails);
            var detailsText=Label("Label",detailsRect,"Score details",20,8,3,352,36,UiTheme.Paper);detailsText.alignment=TextAlignmentOptions.Center;
            var buttonRect=HudPrimitives.Fill("Continue from competition results",column,UiTheme.AccentDeep,8);
            Place(buttonRect,780,726,368,42);buttonRect.GetComponent<Image>().raycastTarget=true;
            continueButton=buttonRect.gameObject.AddComponent<Button>();continueButton.targetGraphic=buttonRect.GetComponent<Image>();
            continueButton.onClick.AddListener(Dismiss);
            continueButton.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=detailsButton,selectOnRight=detailsButton,selectOnUp=detailsButton,selectOnDown=detailsButton};
            detailsButton.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=continueButton,selectOnRight=continueButton,selectOnUp=continueButton,selectOnDown=continueButton};
            var buttonText=Label("Label",buttonRect,"Continue",20,8,3,352,36,UiTheme.Paper);buttonText.alignment=TextAlignmentOptions.Center;
            Label("Dismissal hint",column,"Results stay until you continue.",16,426,725,324,44,UiTheme.Muted);
        }

        private void ToggleDetails()
        {
            if(!playing || elapsed<.25f)return;
            showingDetails=!showingDetails;standingsPanel.gameObject.SetActive(!showingDetails);detailsPanel.gameObject.SetActive(showingDetails);
            detailsButton.GetComponentInChildren<TMP_Text>().text=showingDetails?"Back to standings":"Score details";
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(detailsButton.gameObject);
        }

        private TMP_Text Label(string name,Transform parent,string text,float size,float x,float y,float width,float height,Color colour)
        {
            var label=HudPrimitives.Label(name,parent,size*Mathf.Clamp(FontScale,.8f,1.25f),colour,TextAlignmentOptions.Left);
            label.text=text;label.textWrappingMode=TextWrappingModes.Normal;Place(label.rectTransform,x,y,width,height);return label;
        }
        private static void Place(RectTransform rect,float x,float y,float width,float height)
        {rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);}
    }
}
