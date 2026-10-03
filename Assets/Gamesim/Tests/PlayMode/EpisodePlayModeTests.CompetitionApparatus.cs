using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private CompetitionApparatus PlayerInstrument()=>SceneComponents<CompetitionApparatus>()
            .Single(instrument=>instrument.ActorId==director.Snapshot.playerId);

        private IEnumerator EnterInstrumentAttempt(string category,bool ranked=false,bool stopAtReady=false)
        {
            yield return InstallRules4AtFirstHoH(SeedOpeningWith(category));
            yield return SettleCast();
            typeof(Gamesim.Episode.EpisodeDirector).GetField("reducedMotion",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(director,false);
            cameraRig.SetReducedMotion(false);
            var before=director.Snapshot;
            Vector3 start=player.transform.position;float radius=player.Agent.radius,height=player.Agent.height;
            ButtonWithCaption(ranked?CompetitionMiniGames.EnterCaption(CompetitionMiniGames.For(category)):"Practice this competition").onClick.Invoke();
            Assert.That(player.transform.position,Is.EqualTo(start),"Opening the apparatus claims a route and does not warp the body.");
            var instrument=PlayerInstrument();
            Assert.That(instrument.GetComponentsInChildren<Collider>(true),Is.Empty);
            yield return null;
            var screen=SceneComponents<CompetitionGameScreen>().Single();
            if(screen.IsAssembling)yield return PressKey(Key.Enter);
            float ready=Time.realtimeSinceStartup+40f;
            while(screen.IsShowing && (stopAtReady?!instrument.gameObject.activeInHierarchy:!screen.IsPlaying) && Time.realtimeSinceStartup<ready)
            {
                if(screen.Paused)yield return PressKey(Key.P);else yield return null;
            }
            if(stopAtReady)Assert.That(ChallengeRun().Elapsed,Is.Zero,"Native arrivals precede attempt time.");
            else Assert.That(screen.IsPlaying,Is.True,"The actual native arrivals and ready count permit play.");
            if(category=="Endurance")
            {
                yield return null;yield return null;
                float close=Time.realtimeSinceStartup+4f;
                while(Time.realtimeSinceStartup<close && (!cameraRig.HasArrived(.15f) || cameraRig.IsTravelling))
                {if(screen.Paused)yield return PressKey(Key.P);else yield return null;}
                Assert.That(cameraRig.HasArrived(.15f) && !cameraRig.IsTravelling,Is.True,"The native effort camera reaches its closer view.");
            }
            Assert.That(instrument.gameObject.activeInHierarchy,Is.True,"The instrument appears after routing finishes.");
            Assert.That(Vector3.Distance(player.transform.position,instrument.transform.parent.position),Is.LessThan(.35f));
            Assert.That(player.Agent.radius,Is.EqualTo(radius));Assert.That(player.Agent.height,Is.EqualTo(height));
            Assert.That(director.Snapshot.revision,Is.EqualTo(before.revision));
            Assert.That(director.Snapshot.randomState,Is.EqualTo(before.randomState),"Staging and countdown do not draw from the season.");
            foreach(var other in SceneComponents<CompetitionApparatus>().Where(other=>other.ActorId!=instrument.ActorId))
                Assert.That(other.ProgressText,Is.EqualTo("Ready"),"Another entrant's hidden performance is not invented or shown.");
        }

        private IEnumerator ClickInstrumentControl(Button button)
        {
            if(testMouse==null)testMouse=InputSystem.AddDevice<Mouse>();
            Canvas.ForceUpdateCanvases();
            var point=ScreenBox((RectTransform)button.transform).center;
            InputSystem.QueueStateEvent(testMouse,new MouseState{position=point});yield return null;
            Canvas.ForceUpdateCanvases();point=ScreenBox((RectTransform)button.transform).center;
            InputSystem.QueueStateEvent(testMouse,new MouseState{position=point}.WithButton(MouseButton.Left));yield return null;
            InputSystem.QueueStateEvent(testMouse,new MouseState{position=point});yield return null;
        }

        private IEnumerator FlipAnActualPair()
        {
            var run=ChallengeRun();var screen=SceneComponents<CompetitionGameScreen>().Single();
            if(screen.Paused)yield return PressKey(Key.P);
            int first=Enumerable.Range(0,16).First(index=>!run.Matched[index]);
            int pair=Enumerable.Range(0,16).First(index=>index!=first && run.Faces[index]==run.Faces[first]);
            var cards=screen.GetComponentsInChildren<Button>().Where(button=>button.name.StartsWith("Memory card ",StringComparison.Ordinal)).ToArray();
            yield return ClickInstrumentControl(cards.Single(button=>button.name=="Memory card "+(first+1)));
            yield return ClickInstrumentControl(cards.Single(button=>button.name=="Memory card "+(pair+1)));
            Assert.That(run.MatchedPairs,Is.EqualTo(1),"The two real pointer clicks matched the actual cards.");
        }

        private IEnumerator HoldTheActualGrip()
        {
            if(testKeyboard==null)testKeyboard=InputSystem.AddDevice<Keyboard>();
            var screen=SceneComponents<CompetitionGameScreen>().Single();
            if(screen.Paused)yield return PressKey(Key.P);
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.Space));
            yield return null;yield return null;yield return null;
            Assert.That(ChallengeRun().Holding,Is.True,"Space reaches the screen's real continuous grip control.");
        }

        private IEnumerator HitAnActualSignal()
        {
            var screen=SceneComponents<CompetitionGameScreen>().Single();var run=ChallengeRun();
            if(screen.Paused)yield return PressKey(Key.P);
            float deadline=Time.realtimeSinceStartup+5f;
            while(!run.TargetLive && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(run.TargetLive,Is.True);
            int hits=run.Hits;
            Key[] direction={Key.UpArrow,Key.RightArrow,Key.DownArrow,Key.LeftArrow};
            yield return PressKey(direction[(int)run.TargetDirection]);
            Assert.That(run.Hits,Is.EqualTo(hits+1),"The actual arrow key hits the live target.");
        }

        private IEnumerator RollTheActualDice()
        {
            var screen=SceneComponents<CompetitionGameScreen>().Single();var run=ChallengeRun();
            if(screen.Paused)yield return PressKey(Key.P);
            int rolls=run.RollsUsed;
            yield return ClickInstrumentControl(screen.GetComponentsInChildren<Button>().Single(button=>button.name=="Roll dice"));
            Assert.That(run.RollsUsed,Is.EqualTo(rolls+1),"The actual pointer rolls the director's attempt.");
        }

        [UnityTest]
        public IEnumerator Apparatus_InHouseMemoryUsesActualCardsAndReleasesItsStageOnCancel()
        {
            yield return EnterInstrumentAttempt("Mental");
            var before=director.Snapshot;var instrument=PlayerInstrument();
            Assert.That(instrument.Instrument,Is.EqualTo(CompetitionApparatus.Family.PairConsole));
            Assert.That(instrument.VisibleMemoryFaces,Is.Zero,"The timed attempt keeps its unseen pairs hidden.");
            if(Application.isBatchMode)yield return CaptureActualInstrument("mental",FlipAnActualPair,false);
            else yield return FlipAnActualPair();
            Assert.That(instrument.VisibleMemoryFaces,Is.EqualTo(2));
            var run=ChallengeRun();
            foreach(var label in instrument.GetComponentsInChildren<TMP_Text>().Where(label=>label.name.StartsWith("Memory face ",StringComparison.Ordinal) && !label.name.Contains("audience")))
            {
                int index=int.Parse(label.name.Substring("Memory face ".Length));
                Assert.That(label.text,Is.EqualTo(run.Matched[index]?CompetitionGameScreen.MemoryFaceName(run.Faces[index]):"?"));
            }
            yield return CancelInstrumentAttempt(before);
        }

        [UnityTest]
        public IEnumerator Apparatus_InHouseEnduranceFitsRaisedHandsToItsActualGrip()
        {
            yield return EnterInstrumentAttempt("Endurance");
            var before=director.Snapshot;var instrument=PlayerInstrument();
            Assert.That(instrument.Instrument,Is.EqualTo(CompetitionApparatus.Family.GripRig));
            if(Application.isBatchMode)yield return CaptureActualInstrument("endurance",HoldTheActualGrip,true);
            else yield return HoldTheActualGrip();
            yield return null;
            Assert.That(instrument.GripFraction,Is.EqualTo((float)(ChallengeRun().Meter/100)).Within(.001));
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());yield return null;yield return null;
            Assert.That(ChallengeRun().Holding,Is.False,"Releasing the actual hold also releases the effort.");
            yield return CancelInstrumentAttempt(before);
        }

        [UnityTest]
        public IEnumerator Apparatus_InHouseSignalsRespondToActualDirectionInput()
        {
            yield return EnterInstrumentAttempt("Skill");
            var before=director.Snapshot;var instrument=PlayerInstrument();
            Assert.That(instrument.Instrument,Is.EqualTo(CompetitionApparatus.Family.Signals));
            if(Application.isBatchMode)yield return CaptureActualInstrument("signals",HitAnActualSignal,true);
            else yield return HitAnActualSignal();
            Assert.That(ChallengeRun().Hits,Is.GreaterThan(0));
            yield return CancelInstrumentAttempt(before);
        }

        [UnityTest]
        public IEnumerator Apparatus_InHouseDiceKeepsTheActualRollAndShowsItsRealResult()
        {
            yield return EnterInstrumentAttempt("Luck",true);
            var before=director.Snapshot;var run=ChallengeRun();var instrument=PlayerInstrument();
            Assert.That(instrument.Instrument,Is.EqualTo(CompetitionApparatus.Family.DiceTray));
            if(Application.isBatchMode)yield return CaptureActualInstrument("dice",RollTheActualDice,true);
            else yield return RollTheActualDice();
            var screen=SceneComponents<CompetitionGameScreen>().Single();
            float deadline=Time.realtimeSinceStartup+6f;
            while(run.Rolling && Time.realtimeSinceStartup<deadline)
            {if(screen.Paused)yield return PressKey(Key.P);else yield return null;}
            Assert.That(run.HasRoll,Is.True);
            for(int die=0;die<3;die++)Assert.That(instrument.GetComponentsInChildren<TMP_Text>()
                .Single(label=>label.name=="Die face "+die).text,Is.EqualTo(run.Face(die).ToString()));
            if(screen.Paused)yield return PressKey(Key.P);
            int total=run.RollTotal;
            yield return ClickInstrumentControl(screen.GetComponentsInChildren<Button>().Single(button=>button.name=="Keep roll"));
            Assert.That(run.Finished,Is.True,"The real Keep control commits this roll.");
            deadline=Time.realtimeSinceStartup+6f;
            while(director.Snapshot.revision==before.revision && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(director.Snapshot.revision,Is.EqualTo(before.revision+1));
            Assert.That(SceneComponents<CompetitionApparatus>(),Is.Empty,"The finished result releases all apparatus.");
            var card=SceneComponents<CompetitionResult>().Single();
            Assert.That(Labelled(card,"Player attempt"),Does.Contain("kept "+total+" on roll 1 of 3"));
            if(Application.isBatchMode)yield return CaptureFraming("competition-apparatus-dice-result",false);
        }

        private IEnumerator SetWordAttemptPaused(bool paused)
        {
            var screen=SceneComponents<CompetitionGameScreen>().Single();
            if(screen.Paused!=paused)
                yield return ClickInstrumentControl(screen.GetComponentsInChildren<Button>().Single(button=>button.name=="Pause competition"));
            Assert.That(screen.Paused,Is.EqualTo(paused),"The real pause/resume pointer control owns the word clock.");
        }

        private void AssertWordInstrumentHidden(bool hidden)
        {
            var run=ChallengeRun();
            foreach(var label in PlayerInstrument().GetComponentsInChildren<TMP_Text>().Where(label=>label.name.StartsWith("Letter ",StringComparison.Ordinal)))
            {
                string suffix=label.name.Substring("Letter ".Length).Split(' ')[0];int index=int.Parse(suffix);
                Assert.That(label.text,Is.EqualTo(index>=run.Scrambled.Length?"":hidden?"?":run.Scrambled[index].ToString()),label.name);
            }
        }

        private IEnumerator FreezeReadyWords()
        {yield return SetWordAttemptPaused(true);Assert.That(ChallengeRun().Elapsed,Is.Zero);AssertWordInstrumentHidden(true);}

        private IEnumerator StartAndTypeAnActualWordLetter()
        {
            yield return SetWordAttemptPaused(false);
            var screen=SceneComponents<CompetitionGameScreen>().Single();float deadline=Time.realtimeSinceStartup+8f;
            while(!screen.IsPlaying && Time.realtimeSinceStartup<deadline)
            {if(screen.Paused)yield return SetWordAttemptPaused(false);else yield return null;}
            Assert.That(screen.IsPlaying,Is.True);
            var run=ChallengeRun();int before=run.Spelled.Length;
            yield return PressKey((Key)((int)Key.A+char.ToUpperInvariant(run.Word[before])-'A'));
            Assert.That(run.Spelled.Length,Is.EqualTo(before+1),"Actual keyboard input picks a word tile.");
            AssertWordInstrumentHidden(false);
        }

        private IEnumerator PauseActualWords()
        {
            yield return SetWordAttemptPaused(true);double elapsed=ChallengeRun().Elapsed;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(ChallengeRun().Elapsed,Is.EqualTo(elapsed),"Paused time cannot buy puzzle-solving time.");
            AssertWordInstrumentHidden(true);
        }

        [UnityTest]
        public IEnumerator Apparatus_InHouseWordsHideOnReadyAndPauseAndResumeThroughActualControls()
        {
            yield return EnterInstrumentAttempt("Social",stopAtReady:true);var before=director.Snapshot;
            Assert.That(PlayerInstrument().Instrument,Is.EqualTo(CompetitionApparatus.Family.WordConsole));
            if(Application.isBatchMode)yield return CaptureActualInstrument("words-ready",FreezeReadyWords,false,false);
            else yield return FreezeReadyWords();
            if(Application.isBatchMode)yield return CaptureActualInstrument("words-running",StartAndTypeAnActualWordLetter,false,false);
            else yield return StartAndTypeAnActualWordLetter();
            string puzzle=ChallengeRun().Scrambled,picked=ChallengeRun().Spelled;
            if(Application.isBatchMode)yield return CaptureActualInstrument("words-paused",PauseActualWords,false,false);
            else yield return PauseActualWords();
            Assert.That(ChallengeRun().Scrambled,Is.EqualTo(puzzle));Assert.That(ChallengeRun().Spelled,Is.EqualTo(picked));
            if(Application.isBatchMode)yield return CaptureActualInstrument("words-resumed",StartAndTypeAnActualWordLetter,false,false);
            else yield return StartAndTypeAnActualWordLetter();
            Assert.That(ChallengeRun().Scrambled,Is.EqualTo(puzzle));Assert.That(ChallengeRun().Spelled,Does.StartWith(picked));
            yield return CancelInstrumentAttempt(before);
        }

        /// <summary>
        /// The renderer photographs the actual input-created attempt and humanoid pose. Setting
        /// the lens never changes an actor, lease, attempt or simulation value; it only supplies a
        /// close side view for body/apparatus review in addition to the normal UI captures.
        /// </summary>
        private IEnumerator CaptureActualInstrument(string name,Func<IEnumerator> play,bool handContact,bool resumeBeforeInput=true)
        {
            var instrument=PlayerInstrument();var screen=SceneComponents<CompetitionGameScreen>().Single();
            var lens=new CaptureLens(cameraRig.ViewCamera,1600,900);
            var eye=cameraRig.ViewCamera;Texture2D frame=null,uiFrame=null;
            var canvases=SceneComponents<Canvas>().Where(canvas=>canvas.name!=CaptureLens.GuardName)
                .Select(canvas=>(Canvas:canvas,Enabled:canvas.enabled)).ToArray();
            Vector3 position=eye.transform.position;Quaternion rotation=eye.transform.rotation;
            float fov=eye.fieldOfView;bool orthographic=eye.orthographic;
            var overlays=instrument.OverlayRenderers.Select(renderer=>(Renderer:renderer,Enabled:renderer.enabled)).ToArray();
            try
            {
                yield return null;Canvas.ForceUpdateCanvases();yield return lens.MakeSureTheCanvasesAreDrawn();
                if(screen.Paused && resumeBeforeInput)yield return PressKey(Key.P);
                yield return play();yield return null;
                var humanoid=player.GetComponentsInChildren<Animator>().FirstOrDefault(animator=>animator.isHuman && animator.isActiveAndEnabled);
                var pose=player.GetComponent<CompetitionInstrumentPose>();
                if(handContact && humanoid!=null)
                {
                    Assert.That(pose,Is.Not.Null,"An arrived humanoid borrows the instrument's scoped hand pose.");
                    Assert.That(pose.HasContact,Is.True,name+": hands must contact the actual apparatus; left error "+pose.LeftHandError+", right error "+pose.RightHandError);
                }
                // Both reads are in the same frame, after real input. A GPU read can make the
                // following frame slow; it must not turn these captures into a paused screen.
                uiFrame=lens.Read();AssertNotBlank(uiFrame,name+" actual competition UI");
                var surface=screen.GetComponentsInChildren<RectTransform>().Single(rect=>rect.name=="Game surface");
                AssertRegionHasContent(uiFrame,ScreenBox(surface),name+" actual game surface");
                File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath,"..","competition-apparatus-"+name+"-ui.png")),uiFrame.EncodeToPNG());
                foreach(var pair in canvases)if(pair.Canvas!=null)pair.Canvas.enabled=false;
                instrument.SetOverlaysVisible(true);
                Transform anchor=instrument.transform.parent;
                Vector3 focus=player.transform.position+Vector3.up*1.10f+anchor.forward*.25f;
                eye.transform.position=focus+anchor.right*3.2f+anchor.forward*1.4f+Vector3.up*.80f;
                eye.transform.LookAt(focus);eye.fieldOfView=42;eye.orthographic=false;
                Canvas.ForceUpdateCanvases();
                frame=lens.Read();AssertNotBlank(frame,name+" actual instrument");
                string path=Path.GetFullPath(Path.Combine(Application.dataPath,"..","competition-apparatus-"+name+"-world.png"));
                File.WriteAllBytes(path,frame.EncodeToPNG());
                Debug.Log("[Gamesim W3] actual "+name+" instrument capture -> "+path+"; "+CompetitionApparatus.Readout(ChallengeRun())
                    +"; humanoid="+(humanoid!=null)+"; handContact="+(pose!=null && pose.HasContact));
            }
            finally
            {
                eye.transform.SetPositionAndRotation(position,rotation);eye.fieldOfView=fov;eye.orthographic=orthographic;
                foreach(var pair in canvases)if(pair.Canvas!=null)pair.Canvas.enabled=pair.Enabled;
                foreach(var pair in overlays)if(pair.Renderer!=null)pair.Renderer.enabled=pair.Enabled;
                lens.Dispose();if(frame!=null)Object.Destroy(frame);if(uiFrame!=null)Object.Destroy(uiFrame);
            }
            yield return null;
        }

        private IEnumerator CancelInstrumentAttempt(EpisodeState before)
        {
            yield return PressKey(Key.Escape);yield return null;yield return null;
            Assert.That(SceneComponents<CompetitionApparatus>(),Is.Empty,"Cancellation releases the stage's meshes and materials.");
            var pose=player.GetComponent<CompetitionInstrumentPose>();
            Assert.That(pose==null || !pose.HasContact,Is.True,"Cancellation releases the arms it borrowed.");
            Assert.That(director.Snapshot.revision,Is.EqualTo(before.revision));
            Assert.That(director.Snapshot.randomState,Is.EqualTo(before.randomState),"Practice and cancel preserve the seeded season.");
            director.ClosePanels();yield return null;
            Assert.That(player.InputEnabled,Is.True);Assert.That(cameraRig.ControlsEnabled,Is.True);
        }
    }
}
