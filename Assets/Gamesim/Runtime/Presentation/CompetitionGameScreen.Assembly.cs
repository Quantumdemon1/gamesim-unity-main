using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    public sealed partial class CompetitionGameScreen
    {
        private RectTransform assemblyPanel;
        private TMP_Text assemblyStatus;
        private Button assemblyContinue, assemblyPause, assemblyCancel;
        private float assemblyElapsed;
        public bool IsAssembling { get; private set; }

        private void BeginAssembly(string title)
        {
            IsAssembling=true;assemblyElapsed=0;panel.gameObject.SetActive(false);

            // The competition's own glass card, as its challenge and timer are drawn (mockup-05):
            // the award under its trophy, what is happening, and one control lit - the one that
            // moves on. It was a near-black slab the width of the frame with three equal blue
            // buttons and the title in gold, which is the colour of the win, not of the walk to it.
            assemblyPanel=HudPrimitives.Fill("Visible competition assembly",transform,new Color(UiTheme.GlassFill.r,UiTheme.GlassFill.g,UiTheme.GlassFill.b,.94f),UiTheme.GlassRadius);
            assemblyPanel.anchorMin=assemblyPanel.anchorMax=new Vector2(.5f,0);assemblyPanel.pivot=new Vector2(.5f,0);
            assemblyPanel.anchoredPosition=new Vector2(0,30);assemblyPanel.sizeDelta=new Vector2(1060,178);
            assemblyPanel.GetComponent<Image>().raycastTarget=true;
            UiTheme.Glass(assemblyPanel,UiTheme.GlassRadius);
            float titleX=30f;
            var trophy=UiTheme.Icon("trophy");
            if(trophy!=null)
            {
                var mark=new GameObject("Assembly mark",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
                mark.rectTransform.SetParent(assemblyPanel,false);Place(mark.rectTransform,30,18,30,30);
                mark.sprite=trophy;mark.color=UiTheme.Gold;mark.preserveAspect=true;mark.raycastTarget=false;
                titleX=72f;
            }
            var heading=Label("Assembly title",assemblyPanel,title+" · TAKING YOUR PLACES",24,titleX,15,1000-titleX,40,UiTheme.Paper);
            var semibold=UiTheme.Font(UiTheme.Weight.SemiBold);if(semibold!=null)heading.font=semibold;
            heading.characterSpacing=1f;Fit(heading,15);
            assemblyStatus=Label("Assembly status",assemblyPanel,"Walking to reserved competition stations. The attempt clock has not started.",17,30,60,1000,44,UiTheme.Muted);
            assemblyContinue=Button("Continue to competition",assemblyPanel,"Skip assembly view",30,112,380,48,FinishAssembly);
            assemblyPause=Button("Pause assembly",assemblyPanel,"Pause",426,112,240,48,TogglePause);
            assemblyCancel=Button("Cancel assembly",assemblyPanel,"Back to briefing",682,112,348,48,()=>cancelAction?.Invoke());
            Secondary(assemblyPause);Secondary(assemblyCancel);
            var ring=new[]{assemblyContinue,assemblyPause,assemblyCancel};
            for(int i=0;i<ring.Length;i++)
                ring[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=ring[(i+2)%3],selectOnRight=ring[(i+1)%3],
                    selectOnUp=ring[(i+2)%3],selectOnDown=ring[(i+1)%3]};
            Select(assemblyContinue);Canvas.ForceUpdateCanvases();
        }

        /// <summary>The visible assembly consumes no gameplay time and ends only after real arrivals.
        /// Skipping changes the view; it never teleports actors or bypasses the arrival gate.</summary>
        public void AdvanceAssembly(float delta,bool arenaReady)
        {
            if(!IsShowing || !IsAssembling || Paused || Time.frameCount<=shownFrame)return;
            assemblyElapsed+=Mathf.Max(0,delta);
            if(arenaReady && assemblyElapsed>=1.2f)FinishAssembly();
        }

        private void FinishAssembly()
        {
            if(!IsAssembling)return;
            IsAssembling=false;assemblyPanel.gameObject.SetActive(false);panel.gameObject.SetActive(true);
            shownFrame=Time.frameCount;
            pause.GetComponentInChildren<TMP_Text>().text=Paused?"Resume":"Pause";
            countdown.gameObject.SetActive(true);countdown.text=Paused?"PAUSED":"3";
            Select(pause);Canvas.ForceUpdateCanvases();
        }

        private void ToggleAssemblyPause()
        {
            Paused=!Paused;run.SetHolding(false);
            assemblyPause.GetComponentInChildren<TMP_Text>().text=Paused?"Resume":"Pause";
            Select(assemblyPause);
        }

        private void MoveAssemblyFocus(bool reverse)
        {
            var ring=new[]{assemblyContinue,assemblyPause,assemblyCancel};
            var selected=EventSystem.current.currentSelectedGameObject;
            int index=System.Array.FindIndex(ring,b=>b.gameObject==selected);
            Select(ring[(index+(reverse?2:1)+3)%3]);
        }

        private void ClearAssembly()
        {
            IsAssembling=false;
            if(assemblyPanel!=null){assemblyPanel.gameObject.SetActive(false);Destroy(assemblyPanel.gameObject);}
            assemblyPanel=null;assemblyStatus=null;

        }
    }
}