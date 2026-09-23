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

        /// <summary>The card's width, and its rows', at the standard text size.</summary>
        private const float CardWidth = 760f;
        private const float RowHeight = 36f;
        private const float HeaderHeight = 196f;
        private const float FooterHeight = 104f;
        private const float MinimumHeight = 500f;

        /// <summary>
        /// The result as a broadcast card over the room (mockup-05's language): the award under a
        /// trophy, the winner lit in gold, the committed standings as bars, and the two controls -
        /// on a card sized to the field rather than a 1180-by-780 slab over a 97 % scrim, which put
        /// the whole house and every piece of chrome behind black for as long as the result stayed
        /// up. The result still owns input until Continue: the ground under it takes the clicks
        /// it always took, it just no longer paints the room out.
        /// </summary>
        private void Build(string award,string category,int week,IList<Standing> standings,string explanation)
        {
            if(column!=null){column.gameObject.SetActive(false);Destroy(column.gameObject);}
            if(scrim==null)
            {
                scrim=HudPrimitives.Fill("Scrim",transform,new Color(UiTheme.Ink.r,UiTheme.Ink.g,UiTheme.Ink.b,.45f),1);
                scrim.anchorMin=Vector2.zero;scrim.anchorMax=Vector2.one;scrim.offsetMin=scrim.offsetMax=Vector2.zero;
                scrim.GetComponent<Image>().raycastTarget=true;
                HudPrimitives.Vignette(scrim);
            }
            float scale=Mathf.Clamp(FontScale,.8f,1.25f);
            float width=CardWidth*scale, inner=width-64f*scale;
            float height=Mathf.Max(MinimumHeight*scale,(HeaderHeight+standings.Count*RowHeight+FooterHeight)*scale+74f*scale);
            column=new GameObject("Card",typeof(RectTransform)).GetComponent<RectTransform>();column.SetParent(transform,false);
            column.anchorMin=column.anchorMax=new Vector2(.5f,.5f);column.pivot=new Vector2(.5f,.5f);
            column.sizeDelta=new Vector2(width,height);
            var glass=HudPrimitives.Fill("Card glass",column,UiTheme.GlassFill,UiTheme.GlassRadius);
            glass.anchorMin=Vector2.zero;glass.anchorMax=Vector2.one;glass.offsetMin=glass.offsetMax=Vector2.zero;
            UiTheme.Glass(glass,UiTheme.GlassRadius);

            var weekLine=Label("Week",column,"WEEK "+Mathf.Max(1,week)+" \u00b7 "+(category??"").ToUpperInvariant(),11,32,20,inner/scale,18,UiTheme.Muted);
            weekLine.characterSpacing=8f;
            float titleX=32f;
            var trophy=UiTheme.Icon("trophy");
            if(trophy!=null)
            {
                var mark=new GameObject("Award mark",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
                mark.rectTransform.SetParent(column,false);Place(mark.rectTransform,32*scale,42*scale,28*scale,28*scale);
                mark.sprite=trophy;mark.color=UiTheme.Gold;mark.preserveAspect=true;mark.raycastTarget=false;
                titleX=68f;
            }
            var title=Label("Award",column,(award??"Competition").ToUpperInvariant(),24,titleX,40,inner/scale-titleX+32,34,UiTheme.Paper);
            var bold=UiTheme.Font(UiTheme.Weight.Bold);if(bold!=null)title.font=bold;title.characterSpacing=2f;
            Fit(title,14);

            var winner=standings[0];foreach(var row in standings)if(row.IsWinner)winner=row;
            // The winner, lit: gold is the colour of power in this house, and this is where it is won.
            var halo=UiTheme.Pack(PackArt.GlowGold);
            if(halo!=null)
            {
                var light=new GameObject("Winner glow",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
                light.rectTransform.SetParent(column,false);Place(light.rectTransform,14*scale,64*scale,110*scale,110*scale);
                light.sprite=halo;light.color=new Color(1f,1f,1f,.6f);light.preserveAspect=true;light.raycastTarget=false;
            }
            var portrait=HudPrimitives.Portrait(column,winner.Portrait,UiTheme.Gold,64*scale,3*scale,false,winner.Character);
            Place(portrait,34*scale,84*scale,70*scale,70*scale);
            var line=Label("Winner",column,winner.IsPlayer?winner.Name+" \u00b7 You win!":winner.Name+" wins!",24,120,100,inner/scale-90,36,UiTheme.Gold);
            var semibold=UiTheme.Font(UiTheme.Weight.SemiBold);if(semibold!=null)line.font=semibold;
            Fit(line,14);
            showingDetails=false;
            standingsPanel=new GameObject("Competition standings",typeof(RectTransform)).GetComponent<RectTransform>();
            standingsPanel.SetParent(column,false);standingsPanel.anchorMin=Vector2.zero;standingsPanel.anchorMax=Vector2.one;
            standingsPanel.offsetMin=standingsPanel.offsetMax=Vector2.zero;
            var heading=Label("Standings heading",standingsPanel,"COMMITTED STANDINGS  \u00b7  Scores are relative to this competition",11,32,HeaderHeight-24,inner/scale,18,UiTheme.Muted);
            heading.characterSpacing=4f;Fit(heading,9);
            double best=0;foreach(var entry in standings)best=System.Math.Max(best,entry.Score);if(best<=0)best=1;
            float barX=300f, barWidth=inner/scale-300f-80f;
            for(int i=0;i<standings.Count;i++)
            {
                var entry=standings[i];float y=HeaderHeight+i*RowHeight;
                var row=HudPrimitives.Fill("Standing "+(i+1),standingsPanel,entry.IsWinner
                    ?new Color(UiTheme.Gold.r,UiTheme.Gold.g,UiTheme.Gold.b,.16f):new Color(UiTheme.SurfaceRaised.r,UiTheme.SurfaceRaised.g,UiTheme.SurfaceRaised.b,.55f),6);
                Place(row,32*scale,y*scale,inner,(RowHeight-4)*scale);
                if(entry.IsWinner)UiTheme.AddBorder(row,6,new Color(UiTheme.Gold.r,UiTheme.Gold.g,UiTheme.Gold.b,.55f));
                Label("Rank",row,(i+1).ToString(),14,10,0,30,RowHeight-4,UiTheme.Muted).alignment=TextAlignmentOptions.MidlineLeft;
                var name=Label("Name",row,entry.Name+(entry.IsPlayer?" (You)":""),15,40,0,barX-50,RowHeight-4,entry.IsWinner?UiTheme.Gold:UiTheme.Paper);
                name.alignment=TextAlignmentOptions.MidlineLeft;Fit(name,11);
                var track=HudPrimitives.Fill("Track",row,new Color(UiTheme.Outline.r,UiTheme.Outline.g,UiTheme.Outline.b,.5f),3);
                Place(track,barX*scale,((RowHeight-4)*.5f-3)*scale,barWidth*scale,6*scale);
                var bar=HudPrimitives.Fill("Bar",row,entry.IsWinner?UiTheme.Gold:UiTheme.Glow,3);
                Place(bar,barX*scale,((RowHeight-4)*.5f-3)*scale,Mathf.Max(2,barWidth*scale*Mathf.Clamp01((float)(entry.Score/best))),6*scale);
                var score=Label("Score",row,entry.Score.ToString("0.00"),14,barX+barWidth+10,0,62,RowHeight-4,UiTheme.Paper);
                score.alignment=TextAlignmentOptions.MidlineRight;
            }
            float footer=height/scale-FooterHeight;
            var note=Label("Performance explanation",standingsPanel,"Character statistics, modifiers and seeded rolls determine placement. Choose Score details to review the available explanation. Perfect performance does not guarantee a win.",
                12,32,footer-4,inner/scale,36,UiTheme.Muted);
            Fit(note,9);
            detailsPanel=new GameObject("Competition score details",typeof(RectTransform)).GetComponent<RectTransform>();
            detailsPanel.SetParent(column,false);detailsPanel.anchorMin=Vector2.zero;detailsPanel.anchorMax=Vector2.one;
            detailsPanel.offsetMin=detailsPanel.offsetMax=Vector2.zero;
            var detailsHeading=Label("Score details heading",detailsPanel,"SCORING EXPLANATION \u00b7 Committed result",14,32,HeaderHeight-28,inner/scale,22,UiTheme.Heading);
            if(semibold!=null)detailsHeading.font=semibold;
            var full=Label("Full performance explanation",detailsPanel,explanation??"Character statistics, modifiers and seeded rolls determine placement. Minigame performance provides a bounded bonus, so perfect play does not guarantee a win.",
                16,32,HeaderHeight,inner/scale,footer-HeaderHeight-12,UiTheme.Paper);
            Fit(full,10);
            detailsPanel.gameObject.SetActive(false);

            // Secondary and primary, as the packs draw them.
            var detailsRect=HudPrimitives.Fill("Review competition score details",column,UiTheme.SurfaceRaised,8);
            Place(detailsRect,32*scale,(footer+40)*scale,220*scale,44*scale);detailsRect.GetComponent<Image>().raycastTarget=true;
            UiTheme.PackSliced(detailsRect.GetComponent<Image>(),PackArt.ButtonSecondary,12f*scale);
            detailsButton=detailsRect.gameObject.AddComponent<Button>();detailsButton.targetGraphic=detailsRect.GetComponent<Image>();
            detailsButton.onClick.AddListener(ToggleDetails);
            var detailsText=Label("Label",detailsRect,"Score details",16,8,0,204,44,UiTheme.Paper);detailsText.alignment=TextAlignmentOptions.Center;
            var buttonRect=HudPrimitives.Fill("Continue from competition results",column,UiTheme.AccentDeep,8);
            Place(buttonRect,width-(32+240)*scale,(footer+40)*scale,240*scale,44*scale);buttonRect.GetComponent<Image>().raycastTarget=true;
            continueButton=buttonRect.gameObject.AddComponent<Button>();continueButton.targetGraphic=buttonRect.GetComponent<Image>();
            continueButton.onClick.AddListener(Dismiss);
            continueButton.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=detailsButton,selectOnRight=detailsButton,selectOnUp=detailsButton,selectOnDown=detailsButton};
            detailsButton.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=continueButton,selectOnRight=continueButton,selectOnUp=continueButton,selectOnDown=continueButton};
            var buttonText=Label("Label",buttonRect,"Continue",17,8,0,224,44,UiTheme.Paper);buttonText.alignment=TextAlignmentOptions.Center;
            if(semibold!=null)buttonText.font=semibold;
            var hint=Label("Dismissal hint",column,"Results stay until you continue.",12,264,footer+40,width/scale-264-32-240-8,44,UiTheme.Muted);
            hint.alignment=TextAlignmentOptions.Center;Fit(hint,9);
        }

        private static void Fit(TMP_Text label,float floor)
        {
            label.enableAutoSizing=true;label.fontSizeMax=label.fontSize;label.fontSizeMin=Mathf.Min(floor,label.fontSize);
        }

        private void ToggleDetails()
        {
            if(!playing || elapsed<.25f)return;
            showingDetails=!showingDetails;standingsPanel.gameObject.SetActive(!showingDetails);detailsPanel.gameObject.SetActive(showingDetails);
            detailsButton.GetComponentInChildren<TMP_Text>().text=showingDetails?"Back to standings":"Score details";
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(detailsButton.gameObject);
        }

        /// <summary>A label placed in reference units, scaled with the card.</summary>
        private TMP_Text Label(string name,Transform parent,string text,float size,float x,float y,float width,float height,Color colour)
        {
            float scale=Mathf.Clamp(FontScale,.8f,1.25f);
            var label=HudPrimitives.Label(name,parent,size*scale,colour,TextAlignmentOptions.Left);
            label.text=text;label.textWrappingMode=TextWrappingModes.Normal;
            Place(label.rectTransform,x*scale,y*scale,width*scale,height*scale);return label;
        }
        private static void Place(RectTransform rect,float x,float y,float width,float height)
        {rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);}
    }
}
