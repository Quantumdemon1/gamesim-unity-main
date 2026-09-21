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
            scrim.GetComponent<Image>().color=new Color(0,0,0,.08f);
            assemblyPanel=HudPrimitives.Fill("Visible competition assembly",transform,new Color(.035f,.055f,.085f,.96f),16);
            assemblyPanel.anchorMin=assemblyPanel.anchorMax=new Vector2(.5f,0);assemblyPanel.pivot=new Vector2(.5f,0);
            assemblyPanel.anchoredPosition=new Vector2(0,30);assemblyPanel.sizeDelta=new Vector2(1420,190);
            assemblyPanel.GetComponent<Image>().raycastTarget=true;
            Label("Assembly title",assemblyPanel,title+" · TAKING YOUR PLACES",26,30,15,1360,45,UiTheme.Gold);
            assemblyStatus=Label("Assembly status",assemblyPanel,"Walking to reserved competition stations. The attempt clock has not started.",18,30,64,1360,48,UiTheme.Paper);
            assemblyContinue=Button("Continue to competition",assemblyPanel,"Skip assembly view",30,126,450,48,FinishAssembly);
            assemblyPause=Button("Pause assembly",assemblyPanel,"Pause",502,126,310,48,TogglePause);
            assemblyCancel=Button("Cancel assembly",assemblyPanel,"Back to briefing",834,126,556,48,()=>cancelAction?.Invoke());
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
            scrim.GetComponent<Image>().color=new Color(0,0,0,.55f);shownFrame=Time.frameCount;
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
            if(scrim!=null)scrim.GetComponent<Image>().color=new Color(0,0,0,.55f);
        }
    }
}