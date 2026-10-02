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
            /// <summary>The engine's composite score, which orders the rows; the card never draws it (UI-UX-PASS-PLAN decision 12).</summary>
            public readonly double Score;
            public readonly bool IsWinner, IsPlayer;
            public readonly Texture Portrait;
            public readonly ContestantState Character;
            /// <summary>A word beside the name - "Threw" on a thrown row - or null.</summary>
            public readonly string Note;
            public Standing(string name,double score,bool isWinner,bool isPlayer,Texture portrait,ContestantState character=null,string note=null)
            { Name=name;Score=score;IsWinner=isWinner;IsPlayer=isPlayer;Portrait=portrait;Character=character;Note=note; }
        }

        private CanvasGroup group;
        private RectTransform column, scrim, standingsPanel, detailsPanel;
        private float elapsed;
        private bool playing, reduced;
        private int dismissedFrame = -10;
        private Button continueButton, detailsButton;
        private bool showingDetails;
        // What the card was asked to show, so a change of frame can draw it again at the new size.
        private string playedAward, playedCategory, playedExplanation, playedAttempt;
        private int playedWeek;
        private IList<Standing> playedStandings;
        private Vector2 builtFor;
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

        /// <summary>
        /// Shows the committed standings. <paramref name="attempt"/> is the player's own attempt in
        /// the game's own measure ("7 / 9 targets hit · performance 78%"), when there is one to say.
        /// </summary>
        public bool Play(string award,string category,int week,IList<Standing> standings,bool reducedMotion,string explanation=null,string attempt=null)
        {
            if(standings==null||standings.Count==0)return false;
            playedAward=award;playedCategory=category;playedWeek=week;playedStandings=standings;playedExplanation=explanation;playedAttempt=attempt;
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

        /// <summary>
        /// A new frame shape - a resize, a resolution change, a review capture through a camera -
        /// draws the card again at the new size, on the same page and with the same control lit.
        /// </summary>
        private void LateUpdate()
        {
            if(!playing||playedStandings==null)return;
            var frame=FrameSize();
            if(Mathf.Abs(frame.x-builtFor.x)<=1f&&Mathf.Abs(frame.y-builtFor.y)<=1f)return;
            bool details=showingDetails;
            var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            string selectedName=selected!=null&&selected.transform.IsChildOf(transform)?selected.name:null;
            Build(playedAward,playedCategory,playedWeek,playedStandings,playedExplanation);
            column.gameObject.SetActive(true);
            if(details){showingDetails=true;standingsPanel.gameObject.SetActive(false);detailsPanel.gameObject.SetActive(true);
                detailsButton.GetComponentInChildren<TMP_Text>().text="Back to standings";}
            if(EventSystem.current!=null&&selectedName!=null)
                EventSystem.current.SetSelectedGameObject(selectedName==detailsButton.name?detailsButton.gameObject:continueButton.gameObject);
        }

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
        /// <summary>The margin the card keeps from the frame's edge.</summary>
        private const float FrameEdge = 24f;
        /// <summary>The ink under the glass: near opaque, so nothing of the HUD reads through the card.</summary>
        private const float CardGroundAlpha = .92f;
        /// <summary>How much the card is scaled to fill the frame; everything on it scales together.</summary>
        private float cardScale = 1f;

        /// <summary>
        /// The frame in reference units, from the screen and the scaler's settings: a card attached
        /// and played in the same frame sees the canvas before the scaler has run, in raw pixels.
        /// </summary>
        private Vector2 FrameSize()
        {
            var scaler=GetComponent<CanvasScaler>();
            var canvas=GetComponent<Canvas>();
            float pixelsWide=Screen.width,pixelsHigh=Screen.height;
            // A canvas drawn through a camera - a review capture - is the size of that camera's target.
            if(canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay&&canvas.worldCamera!=null)
            {pixelsWide=canvas.worldCamera.pixelWidth;pixelsHigh=canvas.worldCamera.pixelHeight;}
            if(scaler!=null&&scaler.uiScaleMode==CanvasScaler.ScaleMode.ScaleWithScreenSize&&pixelsWide>0&&pixelsHigh>0)
            {
                var reference=scaler.referenceResolution;
                float log=Mathf.Lerp(Mathf.Log(pixelsWide/reference.x,2f),Mathf.Log(pixelsHigh/reference.y,2f),scaler.matchWidthOrHeight);
                float scale=Mathf.Pow(2f,log);
                return new Vector2(pixelsWide/scale,pixelsHigh/scale);
            }
            var rect=((RectTransform)transform).rect;
            return new Vector2(rect.width>0?rect.width:1600f,rect.height>0?rect.height:900f);
        }

        /// <summary>
        /// The result as a broadcast card over the room (mockup-05's language): the award under a
        /// trophy, the winner lit in gold, the committed standings as rows of faces and names (never
        /// the engine's numbers: UI-UX-PASS-PLAN decision 12), and the two controls -
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
            // The result is the screen until Continue: the card is drawn at its own proportions and
            // then scaled to fill the frame's height - a six-player result about half as big again
            // as it was - and down, for the biggest fields, so that it always fits.
            // The larger text grows the card's rows and bands; the card then scales to fit the frame
            // as it always has, so the words get bigger and the card never runs off it.
            float fs=Mathf.Max(1f,FontScale);
            textScale=fs;
            float rowHeight=RowHeight*fs,headerHeight=HeaderHeight*fs,footerHeight=FooterHeight*fs;
            float natural=Mathf.Max(MinimumHeight,headerHeight+standings.Count*rowHeight+footerHeight+74f);
            var frame=FrameSize();
            builtFor=frame;
            float frameWidth=frame.x, frameHeight=frame.y;
            cardScale=Mathf.Clamp(Mathf.Min((frameWidth-2f*FrameEdge)/CardWidth,(frameHeight-2f*FrameEdge)/natural),.75f,1.6f);
            float scale=cardScale;
            float width=CardWidth*scale, inner=width-64f*scale;
            float height=natural*scale;
            column=new GameObject("Card",typeof(RectTransform)).GetComponent<RectTransform>();column.SetParent(transform,false);
            column.anchorMin=column.anchorMax=new Vector2(.5f,.5f);column.pivot=new Vector2(.5f,.5f);
            column.sizeDelta=new Vector2(width,height);
            // An opaque ground under the glass, in ink: through the glass alone the HUD's own text
            // read through the card's foot (UI-UX-PASS-PLAN C0; the sweeps' rows 28 and 50). The
            // room still shows round the card through the scrim; the glass keeps its edge and glow.
            var ground=HudPrimitives.Fill("Card ground",column,new Color(UiTheme.Ink.r,UiTheme.Ink.g,UiTheme.Ink.b,CardGroundAlpha),UiTheme.GlassRadius);
            ground.anchorMin=Vector2.zero;ground.anchorMax=Vector2.one;ground.offsetMin=ground.offsetMax=Vector2.zero;
            var glass=HudPrimitives.Fill("Card glass",column,UiTheme.GlassFill,UiTheme.GlassRadius);
            glass.anchorMin=Vector2.zero;glass.anchorMax=Vector2.one;glass.offsetMin=glass.offsetMax=Vector2.zero;
            UiTheme.Glass(glass,UiTheme.GlassRadius);

            // "Head of Household · Pressure Cooker": the award joins the week line, and the game's own
            // name is the heading - the same name the briefing and the game gave it.
            string awardName=award??"Competition",gameName=null;
            int split=awardName.IndexOf(" \u00b7 ",System.StringComparison.Ordinal);
            if(split>0){gameName=awardName.Substring(split+3);awardName=awardName.Substring(0,split);}
            var weekLine=Label("Week",column,"WEEK "+Mathf.Max(1,week)+" \u00b7 "+(gameName!=null?awardName.ToUpperInvariant()+" \u00b7 ":"")
                +(category??"").ToUpperInvariant(),11,32,20*fs,inner/scale,18*fs,UiTheme.Muted);
            weekLine.characterSpacing=8f;
            // The final parts carry the part in the award ("FINAL HOH, PART 1 OF 3"): the line may
            // shrink to fit rather than wrap out of its one-line box.
            Fit(weekLine,9);
            float titleX=32f;
            var trophy=UiTheme.Icon("trophy");
            if(trophy!=null)
            {
                var mark=new GameObject("Award mark",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
                mark.rectTransform.SetParent(column,false);Place(mark.rectTransform,32*scale,42*fs*scale,28*fs*scale,28*fs*scale);
                mark.sprite=trophy;mark.color=UiTheme.Gold;mark.preserveAspect=true;mark.raycastTarget=false;
                titleX=36f+32f*fs;
            }
            var title=Label("Award",column,(gameName??awardName).ToUpperInvariant(),24,titleX,40*fs,inner/scale-titleX+32,34*fs,UiTheme.Paper);
            var bold=UiTheme.Font(UiTheme.Weight.Bold);if(bold!=null)title.font=bold;title.characterSpacing=2f;
            Fit(title,14);

            var winner=standings[0];foreach(var row in standings)if(row.IsWinner)winner=row;
            // The winner, lit: gold is the colour of power in this house, and this is where it is won.
            var halo=UiTheme.Pack(PackArt.GlowGold);
            if(halo!=null)
            {
                var light=new GameObject("Winner glow",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
                light.rectTransform.SetParent(column,false);Place(light.rectTransform,14*scale,(64*fs+(fs-1)*10)*scale,110*scale,110*scale);
                light.sprite=halo;light.color=new Color(1f,1f,1f,.6f);light.preserveAspect=true;light.raycastTarget=false;
            }
            var portrait=HudPrimitives.Portrait(column,winner.Portrait,UiTheme.Gold,64*scale,3*scale,false,winner.Character);
            Place(portrait,34*scale,(84*fs+(fs-1)*10)*scale,70*scale,70*scale);
            var line=Label("Winner",column,winner.IsPlayer?"You win!":winner.Name+" wins!",24,120,92*fs,inner/scale-90,36*fs,UiTheme.Gold);
            var semibold=UiTheme.Font(UiTheme.Weight.SemiBold);if(semibold!=null)line.font=semibold;
            Fit(line,14);
            // The player's own attempt, in the game's own measure, where the player played one.
            if(!string.IsNullOrEmpty(playedAttempt))
            {
                var attempt=Label("Player attempt",column,playedAttempt,14,120,(92+38)*fs,inner/scale-90,22*fs,UiTheme.Glow);
                Fit(attempt,10);
            }
            showingDetails=false;
            standingsPanel=new GameObject("Competition standings",typeof(RectTransform)).GetComponent<RectTransform>();
            standingsPanel.SetParent(column,false);standingsPanel.anchorMin=Vector2.zero;standingsPanel.anchorMax=Vector2.one;
            standingsPanel.offsetMin=standingsPanel.offsetMax=Vector2.zero;
            var heading=Label("Standings heading",standingsPanel,CompetitionWords.StandingsHeading,11,32,headerHeight-24*fs,inner/scale,18*fs,UiTheme.Muted);
            heading.characterSpacing=4f;Fit(heading,9);
            // The rows say the order, a face and a name, and the winner's word; never the engine's
            // composite score or a bar proportioned to it (UI-UX-PASS-PLAN decision 12). The one
            // number the card shows is the player's own attempt, above, in the game's own measure.
            // The winner's badge on their face: the crown, or the veto's medal, as the game screen
            // reads the award.
            var winnersMark=awardName.ToUpperInvariant().Contains("VETO")?HudPrimitives.RoleMark.VetoHolder:HudPrimitives.RoleMark.HeadOfHousehold;
            float face=(rowHeight-10f);
            const float WordWidth=96f;
            for(int i=0;i<standings.Count;i++)
            {
                var entry=standings[i];float y=headerHeight+i*rowHeight;
                var row=HudPrimitives.Fill("Standing "+(i+1),standingsPanel,entry.IsWinner
                    ?new Color(UiTheme.Gold.r,UiTheme.Gold.g,UiTheme.Gold.b,.16f):new Color(UiTheme.SurfaceRaised.r,UiTheme.SurfaceRaised.g,UiTheme.SurfaceRaised.b,.55f),6);
                Place(row,32*scale,y*scale,inner,(rowHeight-4)*scale);
                // The winner in gold; you, if you did not win, in the accent - found at a glance.
                if(entry.IsWinner)UiTheme.AddBorder(row,6,new Color(UiTheme.Gold.r,UiTheme.Gold.g,UiTheme.Gold.b,.55f));
                else if(entry.IsPlayer)UiTheme.AddBorder(row,6,UiTheme.Edge(UiTheme.Emphasis.Active));
                Label("Rank",row,(i+1).ToString(),14,10,0,30,rowHeight-4,UiTheme.Muted).alignment=TextAlignmentOptions.MidlineLeft;
                var rim=HudPrimitives.Portrait(row,entry.Portrait,entry.IsWinner?UiTheme.Gold:entry.IsPlayer?UiTheme.Accent:UiTheme.Outline,
                    face*scale,1.5f*scale,false,entry.Character);
                rim.name="Row portrait";
                rim.anchorMin=rim.anchorMax=new Vector2(0f,.5f);rim.pivot=new Vector2(.5f,.5f);
                rim.anchoredPosition=new Vector2((40f+face*.5f)*scale,0f);
                if(entry.IsWinner)HudPrimitives.AddRoleMark(rim,winnersMark,face*scale);
                float nameWidth=inner/scale-(48+face)-(entry.IsWinner?WordWidth+16f:16f);
                var name=Label("Name",row,HudPrimitives.WithYou(entry.Name,entry.IsPlayer)+(string.IsNullOrEmpty(entry.Note)?"":"  ·  "+entry.Note),
                    15,48+face,0,nameWidth,rowHeight-4,entry.IsWinner?UiTheme.Gold:UiTheme.Paper);
                name.alignment=TextAlignmentOptions.MidlineLeft;Fit(name,11);
                if(entry.IsWinner)
                {
                    var word=Label("Winner word",row,"WINNER",11,inner/scale-WordWidth-12f,0,WordWidth,rowHeight-4,UiTheme.Gold);
                    word.alignment=TextAlignmentOptions.MidlineRight;word.characterSpacing=3f;
                    if(semibold!=null)word.font=semibold;
                }
            }
            float footer=height/scale-footerHeight;
            // One line in the player's words where the engine's used to be (UI-UX-PASS-PLAN D0).
            var note=Label("Performance explanation",standingsPanel,CompetitionWords.ResultFooter,12,32,footer-4,inner/scale,36*fs,UiTheme.Muted);
            Fit(note,9);
            detailsPanel=new GameObject("Competition score details",typeof(RectTransform)).GetComponent<RectTransform>();
            detailsPanel.SetParent(column,false);detailsPanel.anchorMin=Vector2.zero;detailsPanel.anchorMax=Vector2.one;
            detailsPanel.offsetMin=detailsPanel.offsetMax=Vector2.zero;
            // The details in words: how the player entered, what they brought, and that the rest
            // was the day (CompetitionWords.Explanation) - never the engine's arithmetic.
            var detailsHeading=Label("Score details heading",detailsPanel,CompetitionWords.DetailsHeading,14,32,headerHeight-28*fs,inner/scale,22*fs,UiTheme.Heading);
            if(semibold!=null)detailsHeading.font=semibold;
            var full=Label("Full performance explanation",detailsPanel,explanation??CompetitionWords.DetailsFallback,
                16,32,headerHeight,inner/scale,footer-headerHeight-12,UiTheme.Paper);
            Fit(full,10);
            detailsPanel.gameObject.SetActive(false);

            // Secondary and primary, as the packs draw them.
            var detailsRect=HudPrimitives.Fill("Review competition score details",column,UiTheme.SurfaceRaised,8);
            float buttonY=footer+36f*fs,buttonHeight=44f*fs;
            Place(detailsRect,32*scale,buttonY*scale,220*scale,buttonHeight*scale);detailsRect.GetComponent<Image>().raycastTarget=true;
            UiTheme.PackSliced(detailsRect.GetComponent<Image>(),PackArt.ButtonSecondary,12f*scale);
            detailsButton=detailsRect.gameObject.AddComponent<Button>();detailsButton.targetGraphic=detailsRect.GetComponent<Image>();
            detailsButton.onClick.AddListener(ToggleDetails);
            var detailsText=Label("Label",detailsRect,"Score details",16,8,0,204,buttonHeight,UiTheme.Paper);detailsText.alignment=TextAlignmentOptions.Center;
            // Continue is the one thing to press next: the pack's primary, as the game's own is drawn.
            var buttonRect=HudPrimitives.Fill("Continue from competition results",column,UiTheme.ActionBlue,8);
            Place(buttonRect,width-(32+240)*scale,buttonY*scale,240*scale,buttonHeight*scale);buttonRect.GetComponent<Image>().raycastTarget=true;
            UiTheme.PackSliced(buttonRect.GetComponent<Image>(),PackArt.ButtonPrimary,14f*scale);
            continueButton=buttonRect.gameObject.AddComponent<Button>();continueButton.targetGraphic=buttonRect.GetComponent<Image>();
            continueButton.onClick.AddListener(Dismiss);
            foreach(var control in new[]{detailsRect,buttonRect})
            { UiTheme.AddBorder(control,8,UiTheme.Edge(UiTheme.Emphasis.Interactive)); HudEmphasis.Promote(control,UiTheme.Emphasis.Interactive); }
            continueButton.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=detailsButton,selectOnRight=detailsButton,selectOnUp=detailsButton,selectOnDown=detailsButton};
            detailsButton.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=continueButton,selectOnRight=continueButton,selectOnUp=continueButton,selectOnDown=continueButton};
            var buttonText=Label("Label",buttonRect,"Continue",17,8,0,224,buttonHeight,Color.white);buttonText.alignment=TextAlignmentOptions.Center;
            if(semibold!=null)buttonText.font=semibold;
            var hint=Label("Dismissal hint",column,"Results stay until you continue.",12,264,buttonY,width/scale-264-32-240-8,buttonHeight,UiTheme.Muted);
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
        // The larger text's factor, as the card is being built.
        private float textScale=1f;

        private TMP_Text Label(string name,Transform parent,string text,float size,float x,float y,float width,float height,Color colour)
        {
            float scale=cardScale;
            var label=HudPrimitives.Label(name,parent,size*scale*textScale,colour,TextAlignmentOptions.Left);
            label.text=text;label.textWrappingMode=TextWrappingModes.Normal;
            Place(label.rectTransform,x*scale,y*scale,width*scale,height*scale);return label;
        }
        private static void Place(RectTransform rect,float x,float y,float width,float height)
        {rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);}
    }
}
