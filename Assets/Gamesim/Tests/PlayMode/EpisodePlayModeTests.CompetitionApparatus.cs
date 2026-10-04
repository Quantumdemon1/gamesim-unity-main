using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private CompetitionApparatus PlayerInstrument()=>SceneComponents<CompetitionApparatus>()
            .Single(instrument=>instrument.ActorId==director.Snapshot.playerId);

        private IEnumerator EnterInstrumentAttempt(string category,bool ranked=false,bool stopAtReady=false,int houseSize=0)
        {
            if(houseSize==0)yield return InstallRules4AtFirstHoH(SeedOpeningWith(category));
            else
            {
                var initial=SeasonBuilder.Create(new SeasonBuilder.Choice{HouseSize=houseSize},SeedOpeningWith(category));
                Assert.That(houseSize,Is.LessThanOrEqualTo(EpisodeValidation.MaximumCast));
                // The regular production registry seats twelve. Valid stored seasons can seat
                // sixteen; extend only this fixture with distinct legal contestants, as the
                // existing CastSizeTests does, then validate/save/reload the resulting season.
                while(initial.contestants.Count<houseSize)
                {
                    int index=initial.contestants.Count;
                    initial.contestants.Add(new ContestantState
                    {
                        id="competition-extra-"+index,name="Competition Guest "+(index+1),pronouns="they/them",homeRoom="Living",
                        motive="Stored large-cast competition fixture.",status=ContestantStatus.Active,
                        traits=new List<string>{"Social"},stats=new ContestantStats(),
                        appearance=initial.contestants[1+(index%4)].appearance?.Clone(),
                    });
                }
                Assert.That(EpisodeValidation.TryValidate(initial,out var reason),Is.True,reason);
                new EpisodeSaveStore(director.SavePath).Save(initial);yield return ReloadEpisode();yield return WaitForNpcRuntimeBinding();
                Assert.That(director.Snapshot.contestants,Has.Count.EqualTo(houseSize),"The valid stored fixture survived the actual save/reload.");
                WarpPlayer(director.StationPosition);Assert.That(director.TryOpenPhasePanel(),Is.True);
                ButtonWithCaption("Begin the next competition").onClick.Invoke();yield return null;
                WarpPlayer(director.StationPosition);Assert.That(director.TryOpenPhasePanel(),Is.True);yield return null;
            }
            yield return SettleCast();
            typeof(Gamesim.Episode.EpisodeDirector).GetField("reducedMotion",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(director,false);
            cameraRig.SetReducedMotion(false);
            var before=director.Snapshot;
            if(houseSize>0)InstallRepresentativeYardSolids();
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
            if(stopAtReady)
            {
                var attempt=ChallengeRun();
                Assert.That(attempt,Is.Not.Null,"Native assembly must retain the actual attempt. "+CompetitionFieldDiagnostic());
                Assert.That(attempt.Elapsed,Is.Zero,"Native arrivals precede attempt time.");
            }
            else Assert.That(screen.IsPlaying,Is.True,"The actual native arrivals and ready count permit play. "+CompetitionFieldDiagnostic());
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

        private GameObject InstallRepresentativeYardSolids()
        {
            var floor=SceneComponents<BoxCollider>().Single(collider=>collider.name=="Competition yard floor");var bounds=floor.bounds;
            var owner=new GameObject("Representative yard solid fixtures");SceneManager.MoveGameObjectToScene(owner,director.gameObject.scene);
            Action<string,Vector3,Vector3> put=(name,position,size)=>
            {
                var prop=GameObject.CreatePrimitive(PrimitiveType.Cube);prop.name=name;prop.layer=HouseLayers.Furniture;
                prop.transform.SetParent(owner.transform,false);prop.transform.position=position;prop.transform.localScale=size;
            };
            put("Rear left light tower volume",new Vector3(bounds.center.x-bounds.size.x*.28f,bounds.max.y+1.5f,bounds.center.z+bounds.size.z*.43f),new Vector3(1,3,1));
            put("Rear right light tower volume",new Vector3(bounds.center.x+bounds.size.x*.28f,bounds.max.y+1.5f,bounds.center.z+bounds.size.z*.43f),new Vector3(1,3,1));
            put("Studio camera volume",new Vector3(bounds.center.x-bounds.size.x*.43f,bounds.max.y+.85f,bounds.center.z-bounds.size.z*.25f),new Vector3(.95f,1.7f,.95f));
            // This front console-height prop clears an actor centred on a grid station while
            // obstructing the former .8m apparatus projection into its native forward route.
            put("Console-only clearance challenge",new Vector3(bounds.center.x,bounds.max.y+1,bounds.min.z+2.6f),new Vector3(.50f,2,.50f));
            Physics.SyncTransforms();return owner;
        }

        [UnityTest]
        public IEnumerator Apparatus_InHouseLargestRegularFieldReservesWholeFamiliesAroundYardSolidsAndNeighbours()
        {
            int largest=SeasonBuilder.LargestHouse(CastTemplates.Roster.Regular);Assert.That(largest,Is.EqualTo(12));
            yield return AssertFullCompetitionField(largest);
        }

        [UnityTest]
        public IEnumerator Apparatus_InHouseStoredMaximumFieldReservesWholeFamiliesAroundYardSolidsAndNeighbours()
        {
            Assert.That(EpisodeValidation.MaximumCast,Is.EqualTo(16));
            yield return AssertFullCompetitionField(EpisodeValidation.MaximumCast);
            AssertDestroyedCompetitionScreenDoesNotInterruptDirectorCleanup();
        }

        private void AssertDestroyedCompetitionScreenDoesNotInterruptDirectorCleanup()
        {
            var screen=SceneComponents<CompetitionGameScreen>().Single();
            var focus=new GameObject("Surviving director-cleanup focus",typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(focus,director.gameObject.scene);
            var events=EventSystem.current;var previous=events!=null?events.currentSelectedGameObject:null;
            bool wasEnabled=director.enabled;
            try
            {
                Assert.That(events,Is.Not.Null);
                events.SetSelectedGameObject(focus);
                Object.DestroyImmediate(screen.gameObject);
                Assert.That(screen==null,Is.True,"The native screen is gone before its owner's OnDisable.");
                Assert.That(ReferenceEquals(screen,null),Is.False,"Its CLR wrapper still exists, so ?. would call Hide.");
                Assert.That(events.currentSelectedGameObject,Is.EqualTo(focus),"A live focus exercises Hide's transform access.");
                director.enabled=false;
                Assert.That(NpcRead<MiniGameRun>("challengeRun"),Is.Null);
                Assert.That(NpcRead<bool>("challengeActive"),Is.False);
                Assert.That(NpcRead<HashSet<Transform>>("competitionRouteOwners"),Is.Empty);
                Assert.That(NpcRead<HouseMeetingCoordinator>("npcMeetings"),Is.Null,
                    "The director's native world disposal still completes after its screen was destroyed.");
            }
            finally
            {
                if(events!=null)events.SetSelectedGameObject(previous!=null?previous:null);
                Object.DestroyImmediate(focus);
                if(director!=null)director.enabled=wasEnabled;
            }
        }

        private IEnumerator AssertFullCompetitionField(int houseSize)
        {
            foreach(string category in new[]{"Mental","Endurance","Luck"})
            {
                yield return EnterInstrumentAttempt(category,houseSize:houseSize);var before=director.Snapshot;
                if(category=="Endurance")yield return HoldTheActualGrip();
                var instruments=SceneComponents<CompetitionApparatus>().ToArray();
                Assert.That(EpisodeEngine.CompetitionPlayers(before).Count(),Is.EqualTo(houseSize));
                Assert.That(instruments,Has.Length.EqualTo(houseSize),category+": every eligible entrant must have a native, occupied instrument station in the "+houseSize+"-person field. "+CompetitionFieldDiagnostic());
                var floor=SceneComponents<BoxCollider>().Single(collider=>collider.name=="Competition yard floor");var scratch=new Collider[256];
                var reservations=director.CompetitionFootprints.ToArray();
                Physics.SyncTransforms();
                foreach(var instrument in instruments)
                {
                    Assert.That(instrument.gameObject.activeInHierarchy,Is.True);
                    var footprint=director.CompetitionFootprints[instrument.ActorId];
                    Assert.That(footprint.FitsOn(floor.bounds),Is.True);
                    Assert.That(footprint.HasStaticClearance(director.gameObject.scene.GetPhysicsScene(),floor,scratch,NpcRead<HashSet<Transform>>("competitionRouteOwners")),Is.True,category+": furniture/tower volume must clear the whole reserved apparatus.");
                    Assert.That(instrument.Fits(footprint),Is.True,category+": actual fitted meshes stay inside their reserved envelope.");
                    foreach(var other in reservations.Where(other=>other.Key!=instrument.ActorId))Assert.That(footprint.Overlaps(other.Value),Is.False,category+": adjacent station/actor "+other.Key);
                }
                if(Application.isBatchMode)yield return CaptureCompetitionFullField(category,houseSize);
                yield return CancelInstrumentAttempt(before);
                var props=SceneComponents<Transform>().Single(transform=>transform.name=="Representative yard solid fixtures");Object.Destroy(props.gameObject);yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Apparatus_InHouseUnleasedForwardHouseguestCancelsFittedSceneryAndReleasesEveryNativeOwner()
        {
            yield return EnterInstrumentAttempt("Mental");var before=director.Snapshot;var instrument=PlayerInstrument();
            Transform anchor=instrument.transform.parent;
            var bystander=InstallUnleasedForwardHouseguest(anchor);var capsule=bystander.GetComponent<CapsuleCollider>();
            Assert.That(bystander.GetComponent<HouseNpcMotion>(),Is.Null);
            var playerCapsule=player.GetComponent<CapsuleCollider>();
            Assert.That(Vector3.Distance(capsule.ClosestPoint(player.transform.position+Vector3.up*.95f),player.transform.position+Vector3.up*.95f),Is.GreaterThan(playerCapsule.radius),"This is a forward apparatus obstruction while the actor capsule remains clear.");
            Assert.That(NpcRead<HashSet<Transform>>("competitionRouteOwners").Contains(bystander.transform),Is.False);
            yield return null;yield return null;
            Assert.That(SceneComponents<CompetitionApparatus>(),Is.Empty,"The actual late fit gate cancels obstructed scenery instead of overlapping the unleased body.");
            Assert.That(player.HasActivityOwner,Is.False);Assert.That(NpcRead<HouseMeetingCoordinator>("npcMeetings").HasCompetitionStage,Is.False);
            Assert.That(NpcRead<HashSet<Transform>>("competitionRouteOwners"),Is.Empty);
            Assert.That(director.Snapshot.revision,Is.EqualTo(before.revision));Assert.That(director.Snapshot.randomState,Is.EqualTo(before.randomState));
            var contact=player.GetComponent<CompetitionInstrumentPose>();Assert.That(contact==null || !contact.HasContact,Is.True);
            Object.Destroy(bystander);director.ClosePanels();yield return null;
            Assert.That(player.InputEnabled,Is.True);Assert.That(cameraRig.ControlsEnabled,Is.True);
        }

        private GameObject InstallUnleasedForwardHouseguest(Transform anchor)
        {
            var bystander=new GameObject("Actual unleased forward houseguest",typeof(HouseNpc),typeof(CapsuleCollider));
            SceneManager.MoveGameObjectToScene(bystander,director.gameObject.scene);
            bystander.GetComponent<HouseNpc>().Configure("unleased-forward","Unleased houseguest");
            bystander.transform.position=anchor.position+anchor.forward*.95f;
            var capsule=bystander.GetComponent<CapsuleCollider>();capsule.center=Vector3.up*.95f;capsule.radius=.20f;capsule.height=1.9f;
            Physics.SyncTransforms();return bystander;
        }

        private IEnumerator PauseActualVetoAudience()
        {
            yield return SetWordAttemptPaused(false);yield return SetWordAttemptPaused(true);
            double elapsed=ChallengeRun().Elapsed;yield return new WaitForSecondsRealtime(.2f);
            Assert.That(ChallengeRun().Elapsed,Is.EqualTo(elapsed));
            Assert.That(NpcRead<HouseMeetingCoordinator>("npcMeetings").CompetitionArrivals,Is.Zero,"Paused native motion withholds fresh arrival proof; the fit gate must not forge it.");
            foreach(var instrument in SceneComponents<CompetitionApparatus>())Assert.That(instrument.gameObject.activeInHierarchy,Is.True,"A stationary, proven audience keeps the already occupied field visible while paused.");
        }

        [UnityTest]
        public IEnumerator Apparatus_InHouseVetoAudienceRetainsOnlyProvenStationaryFieldOnActualPause()
        {
            uint seed=1;while(seed<500 && CompetitionRules.Category(EpisodePhase.Veto,1,seed)!="Mental")seed++;
            Assert.That(seed,Is.LessThan(500));var initial=FullHouse(seed,12);
            initial.phase=EpisodePhase.Veto;initial.hohId=initial.playerId;
            initial.nominees=initial.Active.Where(actor=>!actor.isPlayer).Take(2).Select(actor=>actor.id).ToList();
            initial.vetoPlayers=initial.Active.Take(EpisodeEngine.VetoPlayerCount(initial.Active.Count())).Select(actor=>actor.id).ToList();
            Assert.That(EpisodeValidation.TryValidate(initial,out var reason),Is.True,reason);
            new EpisodeSaveStore(director.SavePath).Save(initial);yield return ReloadEpisode();director.StagesInBatchRuns=true;
            yield return WaitForNpcRuntimeBinding();yield return SettleCast();
            typeof(Gamesim.Episode.EpisodeDirector).GetField("reducedMotion",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(director,false);cameraRig.SetReducedMotion(false);
            WarpPlayer(director.StationPosition);Assert.That(director.TryOpenPhasePanel(),Is.True);
            var before=director.Snapshot;ButtonWithCaption("Practice this competition").onClick.Invoke();yield return null;
            var screen=SceneComponents<CompetitionGameScreen>().Single();if(screen.IsAssembling)yield return PressKey(Key.Enter);
            float deadline=Time.realtimeSinceStartup+40f;
            while(screen.IsShowing && !screen.IsPlaying && Time.realtimeSinceStartup<deadline)
            {if(screen.Paused)yield return PressKey(Key.P);else yield return null;}
            Assert.That(screen.IsPlaying,Is.True,CompetitionFieldDiagnostic());
            Assert.That(SceneComponents<CompetitionApparatus>().Count(),Is.EqualTo(6),CompetitionFieldDiagnostic());
            var anchors=NpcRead<Dictionary<string,HouseInteractionAnchor>>("competitionArenaActors");
            Assert.That(anchors,Has.Count.EqualTo(11),"The six-member veto field has six actual audience members in the twelve-person house.");
            var contestants=new HashSet<string>(EpisodeEngine.CompetitionPlayers(before).Select(actor=>actor.id));
            Assert.That(anchors.Keys.Count(id=>!contestants.Contains(id)),Is.EqualTo(6));
            if(Application.isBatchMode)yield return CaptureActualInstrument("veto-audience-paused",PauseActualVetoAudience,false,false);
            else yield return PauseActualVetoAudience();
            yield return CancelInstrumentAttempt(before);
            Assert.That(NpcRead<HashSet<Transform>>("competitionRouteOwners"),Is.Empty);
        }

        private string CompetitionFieldDiagnostic()
        {
            var meetings=NpcRead<HouseMeetingCoordinator>("npcMeetings");
            return "Arena: "+NpcRead<string>("competitionArenaStatus")+" Audience: "+NpcRead<string>("competitionAudienceStatus")
                +" Native stage: "+(meetings!=null?meetings.CompetitionArrivals+"/"+meetings.CompetitionStageCount+" ready="+meetings.IsReady:"missing")
                +" Motions: "+string.Join("; ",SceneComponents<HouseNpcMotion>().Select(motion=>motion.BoundNpcId+" "+motion.State
                +" lease="+(motion.LeaseId??"none")+" failure="+(motion.FailureReason??motion.LastRouteFailure??"none")
                +" arrival="+(motion.ArrivalFailure??"proved")+" at="+motion.transform.position.ToString("F3")
                +" destination="+motion.ReservedDestination.ToString("F3")))+" Message: "+NpcRead<string>("message");
        }

        private IEnumerator CaptureCompetitionFullField(string category,int houseSize)
        {
            var eye=cameraRig.ViewCamera;var lens=new CaptureLens(eye,1600,900);Texture2D frame=null;
            var canvases=SceneComponents<Canvas>().Where(canvas=>canvas.name!=CaptureLens.GuardName)
                .Select(canvas=>(Canvas:canvas,Enabled:canvas.enabled)).ToArray();
            var overlays=SceneComponents<CompetitionApparatus>().SelectMany(instrument=>instrument.OverlayRenderers)
                .Select(renderer=>(Renderer:renderer,Enabled:renderer.enabled)).ToArray();
            Vector3 position=eye.transform.position;Quaternion rotation=eye.transform.rotation;float fov=eye.fieldOfView;bool orthographic=eye.orthographic;
            try
            {
                yield return null;yield return lens.MakeSureTheCanvasesAreDrawn();
                var screen=SceneComponents<CompetitionGameScreen>().Single();if(screen.Paused)yield return PressKey(Key.P);
                if(category=="Endurance")yield return HoldTheActualGrip();
                else if(category=="Luck")yield return RollTheActualDice();
                yield return null;
                foreach(var pair in canvases)if(pair.Canvas!=null)pair.Canvas.enabled=false;
                foreach(var pair in overlays)if(pair.Renderer!=null)pair.Renderer.enabled=true;
                var floor=SceneComponents<BoxCollider>().Single(collider=>collider.name=="Competition yard floor");
                Vector3 focus=floor.bounds.center+Vector3.up*1.1f;
                eye.transform.position=focus+Vector3.back*Mathf.Max(20f,floor.bounds.size.x*.8f)+Vector3.up*9f;
                eye.transform.LookAt(focus);eye.fieldOfView=50;eye.orthographic=false;
                foreach(var instrument in SceneComponents<CompetitionApparatus>())
                {
                    var view=eye.WorldToViewportPoint(instrument.transform.parent.position+Vector3.up);
                    Assert.That(view.z,Is.GreaterThan(0));Assert.That(view.x,Is.InRange(.03f,.97f));Assert.That(view.y,Is.InRange(.03f,.97f));
                    Assert.That(instrument.Fits(director.CompetitionFootprints[instrument.ActorId]),Is.True,"The actual captured fit must remain reserved.");
                }
                AssertCapturedCompetitionField(houseSize,category);
                frame=lens.Read();
                AssertCapturedCompetitionField(houseSize,category);
                AssertNotBlank(frame,"full-field "+category);
                string path=Path.GetFullPath(Path.Combine(Application.dataPath,"..","competition-apparatus-full-field-"+houseSize+"-"+category.ToLowerInvariant()+".png"));
                File.WriteAllBytes(path,frame.EncodeToPNG());Debug.Log("[Gamesim W3] actual "+houseSize+"-entrant "+category+" world capture -> "+path);
            }
            finally
            {
                eye.transform.SetPositionAndRotation(position,rotation);eye.fieldOfView=fov;eye.orthographic=orthographic;
                foreach(var pair in canvases)if(pair.Canvas!=null)pair.Canvas.enabled=pair.Enabled;
                foreach(var pair in overlays)if(pair.Renderer!=null)pair.Renderer.enabled=pair.Enabled;
                lens.Dispose();if(frame!=null)Object.Destroy(frame);
            }
            yield return null;
        }

        private void AssertCapturedCompetitionField(int houseSize,string category)
        {
            var eligible=EpisodeEngine.CompetitionPlayers(director.Snapshot).Select(actor=>actor.id).ToArray();
            var instruments=SceneComponents<CompetitionApparatus>().ToArray();
            var owned=NpcRead<Dictionary<string,CompetitionApparatus>>("competitionInstruments");
            var owners=NpcRead<HashSet<Transform>>("competitionRouteOwners");
            var meetings=NpcRead<HouseMeetingCoordinator>("npcMeetings");
            var screen=SceneComponents<CompetitionGameScreen>().Single();
            string diagnostic=category+": captured current full field. "+CompetitionFieldDiagnostic();
            Assert.That(eligible,Has.Length.EqualTo(houseSize),diagnostic);
            Assert.That(instruments,Has.Length.EqualTo(houseSize),diagnostic);
            Assert.That(instruments.Select(instrument=>instrument.ActorId),Is.EquivalentTo(eligible),diagnostic);
            Assert.That(owned.Keys,Is.EquivalentTo(eligible),"Only current owned instruments count, including during the synchronous read. "+diagnostic);
            Assert.That(meetings.HasCompetitionStage,Is.True,diagnostic);
            Assert.That(meetings.CompetitionStageCount,Is.EqualTo(houseSize-1),diagnostic);
            // A rendering stall may pause the real game. Its existing native-arrival
            // cache permits only owned roots proven arrived before pause and still
            // within .05m; do not substitute a fresh stationary capsule for a lease.
            var ready=typeof(Gamesim.Episode.EpisodeDirector).GetProperty("CompetitionPresentationReady",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(ready,Is.Not.Null);
            Assert.That((bool)ready.GetValue(director),Is.True,diagnostic);
            if(!screen.Paused)Assert.That(meetings.CompetitionArrivals,Is.EqualTo(houseSize-1),diagnostic);
            foreach(var instrument in instruments)
            {
                Assert.That(owned[instrument.ActorId],Is.SameAs(instrument),diagnostic);
                Assert.That(instrument.gameObject.activeInHierarchy,Is.True,diagnostic);
                var root=instrument.ActorId==director.Snapshot.playerId?player.transform
                    :SceneComponents<HouseNpc>().Single(npc=>npc.Id==instrument.ActorId).transform;
                Assert.That(owners.Contains(root),Is.True,diagnostic);
                if(root!=player.transform)
                {
                    var motion=root.GetComponent<HouseNpcMotion>();
                    Assert.That(motion,Is.Not.Null,diagnostic);
                    Assert.That(motion.IsBound,Is.True,diagnostic);
                    Assert.That(motion.LeaseId,Is.Not.Null.And.StartsWith("competition:"),diagnostic);
                }
            }
        }

        private IEnumerator ClickInstrumentControl(Button button,Action onReleaseInput=null)
        {
            if(testMouse==null)testMouse=InputSystem.AddDevice<Mouse>();
            Canvas.ForceUpdateCanvases();
            var point=ScreenBox((RectTransform)button.transform).center;
            InputSystem.QueueStateEvent(testMouse,new MouseState{position=point});yield return null;
            Canvas.ForceUpdateCanvases();point=ScreenBox((RectTransform)button.transform).center;
            InputSystem.QueueStateEvent(testMouse,new MouseState{position=point}.WithButton(MouseButton.Left));yield return null;
            int queuedReleaseFrame=Time.frameCount,releaseInputFrame=-1;
            Exception releaseError=null;
            Action observeRelease=()=>
            {
                if(releaseInputFrame>=0 || InputState.currentUpdateType!=InputUpdateType.Dynamic
                    || !testMouse.leftButton.wasReleasedThisFrame)return;
                releaseInputFrame=Time.frameCount;
                try { onReleaseInput?.Invoke(); }
                catch(Exception error) { releaseError=error; }
            };
            try
            {
                // QueueStateEvent is processed by the next native input update. Installing an
                // obstruction now would let this frame's LateUpdate cancel before Keep receives
                // its release. Observe the processed release before that frame's UI/late fit.
                if(onReleaseInput!=null)InputSystem.onAfterUpdate+=observeRelease;
                InputSystem.QueueStateEvent(testMouse,new MouseState{position=point});yield return null;
                if(onReleaseInput!=null)
                {
                    Assert.That(releaseInputFrame,Is.GreaterThan(queuedReleaseFrame),
                        "The obstruction is installed on the processed native pointer-release frame, after queue frame "+queuedReleaseFrame+"; observed "+releaseInputFrame+".");
                    if(releaseError!=null)throw new InvalidOperationException("The native pointer-release observation failed.",releaseError);
                    Debug.Log("[Gamesim W3] "+button.name+" pointer release: queued frame "+queuedReleaseFrame+", processed frame "+releaseInputFrame);
                }
            }
            finally { if(onReleaseInput!=null)InputSystem.onAfterUpdate-=observeRelease; }
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
            GameObject bystander=null;
            // Place the unleased body on the real Keep pointer-release frame. This exercises
            // the completed result/finish window without relying on a 0.9-second CPU deadline.
            yield return ClickInstrumentControl(screen.GetComponentsInChildren<Button>().Single(button=>button.name=="Keep roll"),
                ()=>bystander=InstallUnleasedForwardHouseguest(instrument.transform.parent));
            Assert.That(run.Finished,Is.True,"The real Keep control commits this roll.");
            deadline=Time.realtimeSinceStartup+6f;
            while(director.Snapshot.revision==before.revision && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(director.Snapshot.revision,Is.EqualTo(before.revision+1));
            Assert.That(SceneComponents<CompetitionApparatus>(),Is.Empty,"The finished result releases all apparatus.");
            Object.Destroy(bystander);
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
            if(!paused)
            {
                // Native motion reacquires its own arrival proof after a resume. Wait for the
                // actual fit gate to expose the console, rather than forging that proof.
                float deadline=Time.realtimeSinceStartup+2f;
                while(!PlayerInstrument().gameObject.activeInHierarchy && Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(PlayerInstrument().gameObject.activeInHierarchy,Is.True,"The resumed native arrivals expose the actual word console.");
            }
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

        private IEnumerator ShowReadyWords()
        {
            yield return SetWordAttemptPaused(false);
            Assert.That(SceneComponents<CompetitionGameScreen>().Single().IsPlaying,Is.False,"This capture is the real ready countdown, before GO.");
            Assert.That(ChallengeRun().Elapsed,Is.Zero);AssertWordInstrumentHidden(true);
        }

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
            // A GPU read may have triggered the game's frame-delay pause. Resume it first so
            // this fixture proves a new, actual pointer pause rather than inheriting that state.
            yield return SetWordAttemptPaused(false);
            yield return SetWordAttemptPaused(true);double elapsed=ChallengeRun().Elapsed;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(ChallengeRun().Elapsed,Is.EqualTo(elapsed),"Paused time cannot buy puzzle-solving time.");
            Assert.That(PlayerInstrument().gameObject.activeInHierarchy,Is.True,"Already-proven, stationary owned arrivals keep the actual paused word console in the world.");
            AssertWordInstrumentHidden(true);
        }

        [UnityTest]
        public IEnumerator Apparatus_InHouseWordsHideOnReadyAndPauseAndResumeThroughActualControls()
        {
            yield return EnterInstrumentAttempt("Social",stopAtReady:true);var before=director.Snapshot;
            Assert.That(PlayerInstrument().Instrument,Is.EqualTo(CompetitionApparatus.Family.WordConsole));
            if(Application.isBatchMode)yield return CaptureActualInstrument("words-ready",ShowReadyWords,false,false);
            else yield return ShowReadyWords();
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
                // The old rear-side offset put a far-row station's eye inside the saved
                // backdrop. Inspect from the approach side, without moving an actor or
                // hiding any world geometry. Both native reads remain in this same frame.
                var view=SelectCompetitionInspectionView(eye,instrument);
                Canvas.ForceUpdateCanvases();
                AssertCompetitionInspectionView(eye,instrument,view,name+" before world read");
                frame=lens.Read();AssertNotBlank(frame,name+" actual instrument");
                AssertCompetitionInspectionView(eye,instrument,view,name+" after world read");
                string path=Path.GetFullPath(Path.Combine(Application.dataPath,"..","competition-apparatus-"+name+"-world.png"));
                File.WriteAllBytes(path,frame.EncodeToPNG());
                Debug.Log("[Gamesim W3] actual "+name+" instrument capture -> "+path+"; "+CompetitionApparatus.Readout(ChallengeRun())
                    +"; humanoid="+(humanoid!=null)+"; handContact="+(pose!=null && pose.HasContact)
                    +"; inspectionCandidate="+view.Candidate+"; eye="+view.Eye.ToString("F4")+"; focus="+view.Focus.ToString("F4")
                    +"; bodyBounds="+view.Body+"; instrumentBounds="+view.Instrument+"; clearTargets="+view.Targets.Length);
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

        private sealed class CompetitionInspectionView
        {
            public Vector3 Eye,Focus;
            public Quaternion Rotation;
            public Bounds Body,Instrument;
            public Vector3[] Targets;
            public Renderer[] Solids;
            public Vector3 PlayerPosition,InstrumentPosition;
            public Quaternion PlayerRotation,InstrumentRotation;
            public int Candidate;
        }

        private CompetitionInspectionView SelectCompetitionInspectionView(Camera eye,CompetitionApparatus instrument)
        {
            var presentation=player.GetComponent<CharacterPresentation>();
            Assert.That(presentation,Is.Not.Null,"The native inspection player has a body presentation.");
            var visual=presentation.VisualRoot;
            Assert.That(visual,Is.Not.Null,"The inspection uses the actual arrived player body.");
            var body=CompetitionInspectionBounds(visual);
            var apparatus=CompetitionInspectionBounds(instrument.transform);
            var combined=body;combined.Encapsulate(apparatus);
            var floor=SceneComponents<BoxCollider>().Single(collider=>collider.name=="Competition yard floor").bounds;
            var view=new CompetitionInspectionView {Body=body,Instrument=apparatus,Focus=combined.center,
                PlayerPosition=player.transform.position,PlayerRotation=player.transform.rotation,
                InstrumentPosition=instrument.transform.position,InstrumentRotation=instrument.transform.rotation,
                Targets=CompetitionInspectionTargets(body).Concat(CompetitionInspectionTargets(apparatus)).ToArray(),
                Solids=SceneComponents<Renderer>().Where(renderer=>renderer.enabled && renderer.gameObject.activeInHierarchy
                    && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) && renderer.GetComponent<TMP_Text>()==null
                    && !renderer.transform.IsChildOf(player.transform) && !renderer.transform.IsChildOf(instrument.transform)
                    && renderer.sharedMaterials.Any(material=>material!=null && material.renderQueue<3000)).ToArray()};
            var anchor=instrument.transform.parent;
            var failures=new List<string>();int candidate=0;
            eye.fieldOfView=42;eye.orthographic=false;
            foreach(float distance in new[]{4.2f,4.8f,5.6f})
            foreach(float lift in new[]{1.0f,1.55f})
            foreach(float side in new[]{.7f,-.7f,1.1f,-1.1f,0f})
            {
                view.Candidate=++candidate;
                view.Eye=view.Focus+(-anchor.forward+anchor.right*side).normalized*distance+Vector3.up*lift;
                if(view.Eye.x<floor.min.x+.25f || view.Eye.x>floor.max.x-.25f
                    || view.Eye.z<floor.min.z+.25f || view.Eye.z>floor.max.z-.25f)
                {failures.Add(candidate+": outside saved yard");continue;}
                view.Rotation=Quaternion.LookRotation(view.Focus-view.Eye,Vector3.up);
                eye.transform.SetPositionAndRotation(view.Eye,view.Rotation);
                if(CompetitionInspectionVisible(eye,instrument,view,out string failure))return view;
                failures.Add(candidate+": "+failure);
            }
            Assert.Fail("No clear approach-side inspection eye in the bounded30 candidates. Body="+body+"; apparatus="+apparatus
                +"; "+string.Join("; ",failures));
            return null;
        }

        private static Bounds CompetitionInspectionBounds(Transform root)
        {
            var renderers=root.GetComponentsInChildren<Renderer>().Where(renderer=>renderer.enabled && renderer.gameObject.activeInHierarchy
                && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) && renderer.GetComponent<TMP_Text>()==null).ToArray();
            Assert.That(renderers,Is.Not.Empty,"The native inspection subject must have actual visible meshes: "+root.name);
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
            Assert.That(CompetitionInspectionFinite(bounds.min) && CompetitionInspectionFinite(bounds.max) && bounds.size.sqrMagnitude>.01f,Is.True,
                "The native inspection subject needs finite nonempty rendered bounds: "+root.name+" "+bounds);
            return bounds;
        }

        private static bool CompetitionInspectionFinite(Vector3 point)=>!float.IsNaN(point.x) && !float.IsInfinity(point.x)
            && !float.IsNaN(point.y) && !float.IsInfinity(point.y) && !float.IsNaN(point.z) && !float.IsInfinity(point.z);

        private static Vector3[] CompetitionInspectionTargets(Bounds bounds)=>new[]{bounds.center,
            new Vector3(bounds.center.x,bounds.max.y-.12f,bounds.center.z),
            new Vector3(bounds.center.x,bounds.min.y+bounds.size.y*.22f,bounds.center.z),
            bounds.center+Vector3.right*bounds.extents.x*.6f,bounds.center-Vector3.right*bounds.extents.x*.6f};

        private bool CompetitionInspectionVisible(Camera eye,CompetitionApparatus instrument,CompetitionInspectionView view,out string failure)
        {
            foreach(var bounds in new[]{view.Body,view.Instrument})
            for(int corner=0;corner<8;corner++)
            {
                var point=new Vector3((corner&1)==0?bounds.min.x:bounds.max.x,(corner&2)==0?bounds.min.y:bounds.max.y,
                    (corner&4)==0?bounds.min.z:bounds.max.z);
                var projected=eye.WorldToViewportPoint(point);
                if(projected.z<=eye.nearClipPlane || projected.x<.055f || projected.x>.945f || projected.y<.055f || projected.y>.945f)
                {failure="subject corner outside inspection frame: "+projected.ToString("F4");return false;}
            }
            foreach(var collider in Physics.OverlapSphere(view.Eye,.12f,HouseLayers.Pick,QueryTriggerInteraction.Ignore))
                if(CompetitionInspectionObstacle(collider.transform,instrument))
                {failure="eye inside saved/world solid: "+collider.name;return false;}
            foreach(var target in view.Targets)
            {
                Vector3 to=target-view.Eye;var ray=new Ray(view.Eye,to.normalized);
                foreach(var hit in Physics.RaycastAll(ray,to.magnitude-.03f,HouseLayers.Pick,QueryTriggerInteraction.Ignore))
                    if(CompetitionInspectionObstacle(hit.transform,instrument))
                    {failure="subject sight blocked by collider: "+hit.collider.name;return false;}
                // The backdrop and authored props can have no collider. Conservative opaque
                // renderer bounds also reject an eye behind their visible solid envelope.
                foreach(var solid in view.Solids)
                    if(solid!=null && solid.bounds.IntersectRay(ray,out float hitDistance) && hitDistance<to.magnitude-.03f)
                    {failure="subject sight blocked by opaque mesh bounds: "+solid.name;return false;}
            }
            failure=null;return true;
        }

        private bool CompetitionInspectionObstacle(Transform root,CompetitionApparatus instrument)=>root.gameObject.scene==player.gameObject.scene
            && !root.IsChildOf(player.transform) && !root.IsChildOf(instrument.transform);

        private void AssertCompetitionInspectionView(Camera eye,CompetitionApparatus instrument,CompetitionInspectionView view,string moment)
        {
            Assert.That(instrument!=null && instrument.gameObject.activeInHierarchy && instrument.ActorId==director.Snapshot.playerId,Is.True,
                moment+": the actual owned player apparatus must still be active.");
            Assert.That(Vector3.Distance(eye.transform.position,view.Eye),Is.LessThan(.00001f),moment+": the capture retained its selected eye.");
            Assert.That(Quaternion.Angle(eye.transform.rotation,view.Rotation),Is.LessThan(.001f),moment+": the capture retained its selected facing.");
            Assert.That(eye.orthographic,Is.False);Assert.That(eye.fieldOfView,Is.EqualTo(42f).Within(.0001f));
            Assert.That(Vector3.Distance(player.transform.position,view.PlayerPosition),Is.LessThan(.00001f),moment+": the actual player root stayed native and stationary.");
            Assert.That(Quaternion.Angle(player.transform.rotation,view.PlayerRotation),Is.LessThan(.001f),moment+": the actual player root kept its native facing.");
            Assert.That(Vector3.Distance(instrument.transform.position,view.InstrumentPosition),Is.LessThan(.00001f),moment+": the owned apparatus stayed at its fitted place.");
            Assert.That(Quaternion.Angle(instrument.transform.rotation,view.InstrumentRotation),Is.LessThan(.001f),moment+": the owned apparatus kept its fitted facing.");
            var body=CompetitionInspectionBounds(player.GetComponent<CharacterPresentation>().VisualRoot);
            var apparatus=CompetitionInspectionBounds(instrument.transform);
            Assert.That(Vector3.Distance(body.min,view.Body.min)+Vector3.Distance(body.max,view.Body.max),Is.LessThan(.001f),
                moment+": the current rendered body bounds match the selected native pose.");
            Assert.That(Vector3.Distance(apparatus.min,view.Instrument.min)+Vector3.Distance(apparatus.max,view.Instrument.max),Is.LessThan(.001f),
                moment+": the current rendered apparatus bounds match the fitted geometry.");
            var current=new CompetitionInspectionView {Eye=view.Eye,Body=body,Instrument=apparatus,Solids=view.Solids,
                Targets=CompetitionInspectionTargets(body).Concat(CompetitionInspectionTargets(apparatus)).ToArray()};
            Assert.That(CompetitionInspectionVisible(eye,instrument,current,out string failure),Is.True,moment+": "+failure);
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
