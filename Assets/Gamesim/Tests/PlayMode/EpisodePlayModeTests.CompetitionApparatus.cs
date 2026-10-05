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
using Unity.Collections;
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

        [UnityTest]
        public IEnumerator Apparatus_InHouseFullFieldResumeWaitsForNativeArrivalAndLateFitBeforeCapture()
        {
            const int houseSize=12;
            yield return EnterInstrumentAttempt("Mental",houseSize:houseSize);
            var before=director.Snapshot;
            yield return WaitForFittedCompetitionField(houseSize,"Mental before pause",requireFreshArrivals:true);
            var screen=SceneComponents<CompetitionGameScreen>().Single();
            yield return SetWordAttemptPaused(true);
            var meetings=NpcRead<HouseMeetingCoordinator>("npcMeetings");
            float deadline=Time.realtimeSinceStartup+5f;
            while(meetings.CompetitionArrivals!=0 && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(screen.Paused,Is.True);
            Assert.That(meetings.CompetitionArrivals,Is.Zero,"The real pause must revoke fresh native arrival proof. "+CompetitionFieldDiagnostic());
            foreach(var instrument in SceneComponents<CompetitionApparatus>())
                Assert.That(instrument.gameObject.activeInHierarchy,Is.True,"An already proven stationary field remains visible while paused: "+instrument.ActorId);
            double elapsed=ChallengeRun().Elapsed;
            yield return null;
            Assert.That(ChallengeRun().Elapsed,Is.EqualTo(elapsed),"A paused attempt does not advance while its leased field remains visible.");
            // Deliberately resume through the real pointer control, rather than relying on an
            // incidental slow render to create the arrival/late-fit handoff exercised below.
            yield return ClickInstrumentControl(screen.GetComponentsInChildren<Button>().Single(button=>button.name=="Pause competition"));
            Assert.That(screen.Paused,Is.False,"The actual resume control restarts native motion proof.");
            yield return WaitForFittedCompetitionField(houseSize,"Mental after explicit resume",requireFreshArrivals:true);
            Assert.That(screen.Paused,Is.False);
            Assert.That(meetings.CompetitionArrivals,Is.EqualTo(houseSize-1),"Every owned NPC independently reacquires native arrival proof after the real resume.");
            AssertCapturedCompetitionField(houseSize,"Mental after explicit resume");
            yield return CaptureCompetitionFullField("Mental",houseSize);
            Assert.That(director.Snapshot.revision,Is.EqualTo(before.revision));
            Assert.That(director.Snapshot.randomState,Is.EqualTo(before.randomState));
            yield return CancelInstrumentAttempt(before);
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
            var screen=SceneComponents<CompetitionGameScreen>().FirstOrDefault();
            return "Frame: "+Time.frameCount+" paused="+(screen!=null && screen.Paused)
                +" playerArrived="+(player!=null && player.ActivityHasArrived(NpcRead<object>("competitionPlayerOwner")))
                +" Arena: "+NpcRead<string>("competitionArenaStatus")+" Audience: "+NpcRead<string>("competitionAudienceStatus")
                +" Native stage: "+(meetings!=null?meetings.CompetitionArrivals+"/"+meetings.CompetitionStageCount+" ready="+meetings.IsReady:"missing")
                +" Apparatus: "+string.Join("; ",SceneComponents<CompetitionApparatus>().Select(instrument=>instrument.ActorId
                    +" activeSelf="+instrument.gameObject.activeSelf+" activeInHierarchy="+instrument.gameObject.activeInHierarchy))
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
                yield return WaitForFittedCompetitionField(houseSize,category);
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

        private IEnumerator WaitForFittedCompetitionField(int houseSize,string category,bool requireFreshArrivals=false)
        {
            var screen=SceneComponents<CompetitionGameScreen>().Single();
            var ready=typeof(Gamesim.Episode.EpisodeDirector).GetProperty("CompetitionPresentationReady",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(ready,Is.Not.Null);
            float deadline=Time.realtimeSinceStartup+5f;
            while(screen.IsShowing && Time.realtimeSinceStartup<deadline)
            {
                bool arrived=(bool)ready.GetValue(director);
                var owned=NpcRead<Dictionary<string,CompetitionApparatus>>("competitionInstruments");
                // A resume resets native arrival proof. Motion.Update can prove the last
                // arrival after this frame's director Bind cached fitReady=false. Only the
                // subsequent native late fit gate may activate the owned apparatus: readiness
                // or one yield alone is not proof that its clearance/geometry gate has run.
                if(arrived && (!requireFreshArrivals || !screen.Paused) && owned.Count==houseSize
                    && owned.Values.All(instrument=>instrument!=null && instrument.gameObject.activeInHierarchy))
                    yield break;
                // A slow read can pause again before arrivals recover. Use normal input;
                // never manufacture arrival proof, invoke a fit gate, or force visibility.
                if(screen.Paused && (!arrived || requireFreshArrivals))yield return PressKey(Key.P);
                else yield return null;
            }
            Assert.Fail(category+": the current full field did not pass its native late fit gates within 5 seconds. "+CompetitionFieldDiagnostic());
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
                Assert.That(instrument.gameObject.activeInHierarchy,Is.True,"Inactive actor apparatus: "+instrument.ActorId+". "+diagnostic);
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
            AssertCompetitionInspectionUsesFacesRatherThanJoinedBounds();
            AssertCompetitionInspectionUsesReadableUnobscuredGlyphs();
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
                    +"; bodyBounds="+view.Body+"; instrumentBounds="+view.Instrument+"; clearTargets="+view.Targets.Length
                    +"; actualFrontReadouts="+view.Readouts.Length+"; progressGlyphsOpaqueClear=true; semanticReadoutsOpaqueClearFraction>=0.8");
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
            public CompetitionInspectionGeometry Geometry;
            public CompetitionInspectionGeometry BodyGeometry;
            public CompetitionInspectionGeometry ApparatusGeometry;
            public CompetitionInspectionReadout[] Readouts;
            public Vector3 PlayerPosition,InstrumentPosition;
            public Quaternion PlayerRotation,InstrumentRotation;
            public int Candidate;
        }

        private sealed class CompetitionInspectionReadout
        {
            public TMP_Text Label;
            public string Text;
            public Matrix4x4 Matrix;
            public Bounds GlyphBounds;
            public Vector3 Front,Left,Right,Bottom,Top;
            public Vector3[] GlyphCenters;
        }

        private static bool CompetitionInspectionOpaque(Material material)=>material!=null && material.renderQueue<3000;

        private sealed class CompetitionInspectionGeometry
        {
            private sealed class Solid
            {
                public Renderer Renderer;
                public Matrix4x4 Matrix;
                public Bounds Bounds;
                public Vector3[] Points;
                public int[][] Faces;
            }
            private readonly List<Solid> solids=new List<Solid>();
            private int triangleTests;

            public CompetitionInspectionGeometry(Renderer[] renderers)
            {
                Assert.That(renderers.Length,Is.LessThanOrEqualTo(2048),"The inspection mesh inventory is bounded.");
                int vertices=0,triangles=0;
                foreach(var renderer in renderers)
                {
                    Mesh baked=null;
                    try
                    {
                        Mesh mesh;
                        if(renderer is SkinnedMeshRenderer skin)
                        {
                            // Bake only into this newly owned CPU snapshot. Other participants
                            // remain sight blockers; their renderer, pose and shared mesh stay native.
                            baked=new Mesh {hideFlags=HideFlags.HideAndDontSave};
                            skin.BakeMesh(baked,false);mesh=baked;
                        }
                        else
                        {
                            var filter=renderer.GetComponent<MeshFilter>();
                            Assert.That(filter!=null && filter.sharedMesh!=null,Is.True,
                                "An opaque inspection renderer needs its actual mesh: "+renderer.name);
                            mesh=filter.sharedMesh;
                        }
                        var solid=new Solid {Renderer=renderer,Matrix=renderer.localToWorldMatrix,Bounds=renderer.bounds};
                        var materials=renderer.sharedMaterials;
#if UNITY_EDITOR
                        // Imported shell/props have isReadable=false. This editor API reads their
                        // existing data without changing importer settings or reimporting an asset.
                        using(var data=UnityEditor.MeshUtility.AcquireReadOnlyMeshData(mesh))
#else
                        using(var data=Mesh.AcquireReadOnlyMeshData(mesh))
#endif
                        {
                            Assert.That(data.Length,Is.EqualTo(1));
                            vertices+=data[0].vertexCount;
                            Assert.That(vertices,Is.LessThanOrEqualTo(1000000),"Inspection vertex snapshot exceeds its bound.");
                            using(var points=new NativeArray<Vector3>(data[0].vertexCount,Allocator.Temp))
                            {
                                data[0].GetVertices(points);solid.Points=points.ToArray();
                            }
                            for(int i=0;i<solid.Points.Length;i++)
                            {
                                solid.Points[i]=solid.Matrix.MultiplyPoint3x4(solid.Points[i]);
                                if(!CompetitionInspectionFinite(solid.Points[i]))Assert.Fail("Inspection mesh vertex must be finite.");
                            }
                            solid.Faces=new int[data[0].subMeshCount][];
                            for(int sub=0;sub<solid.Faces.Length;sub++)
                            {
                                Assert.That(sub,Is.LessThan(materials.Length),"Each actual submesh needs its rendered material.");
                                bool opaque=CompetitionInspectionOpaque(materials[sub]);
                                // Unity draws extra material slots on the last submesh again.
                                if(sub==solid.Faces.Length-1)
                                    for(int extra=solid.Faces.Length;extra<materials.Length;extra++)opaque|=CompetitionInspectionOpaque(materials[extra]);
                                if(!opaque)continue;
                                var descriptor=data[0].GetSubMesh(sub);
                                Assert.That(descriptor.topology,Is.EqualTo(UnityEngine.MeshTopology.Triangles));
                                Assert.That(descriptor.indexCount%3,Is.Zero);
                                triangles+=descriptor.indexCount/3;
                                Assert.That(triangles,Is.LessThanOrEqualTo(1000000),"Inspection triangle snapshot exceeds its bound.");
                                using(var indices=new NativeArray<int>(descriptor.indexCount,Allocator.Temp))
                                {
                                    data[0].GetIndices(indices,sub,true);solid.Faces[sub]=indices.ToArray();
                                }
                                Assert.That(solid.Faces[sub].All(index=>index>=0 && index<solid.Points.Length),Is.True,
                                    "Inspection triangle indices refer to actual retained vertices.");
                            }
                        }
                        solids.Add(solid);
                    }
                    finally {if(baked!=null)Object.DestroyImmediate(baked);}
                }
                Debug.Log("[Gamesim W3] inspection opaque geometry snapshot: "+solids.Count+" renderers, "+vertices+" vertices, "+triangles+" triangles.");
            }

            public bool Blocked(Ray ray,float length,out string failure)
            {
                foreach(var solid in solids)
                {
                    Assert.That(solid.Renderer!=null && solid.Renderer.enabled && solid.Renderer.gameObject.activeInHierarchy,Is.True,
                        "The synchronous inspection retains its opaque renderer.");
                    Assert.That(solid.Renderer.localToWorldMatrix,Is.EqualTo(solid.Matrix),
                        "An inspection blocker moved after the native geometry snapshot: "+solid.Renderer.name);
                    if(!solid.Bounds.IntersectRay(ray,out float entry) || entry>length)continue;
                    for(int sub=0;sub<solid.Faces.Length;sub++)
                    {
                        var faces=solid.Faces[sub];if(faces==null)continue;
                        for(int i=0;i<faces.Length;i+=3)
                        {
                            if(++triangleTests>20000000)Assert.Fail("Inspection segment/triangle work exceeds its per-capture bound.");
                            if(!CompetitionInspectionTriangleHit(ray,solid.Points[faces[i]],solid.Points[faces[i+1]],solid.Points[faces[i+2]],out float distance)
                                || distance>=length)continue;
                            failure="subject sight blocked by opaque mesh face: "+solid.Renderer.name+"; submesh="+sub
                                +"; triangle="+(i/3)+"; distance="+distance.ToString("F5")
                                +"; actualWorldA="+solid.Points[faces[i]].ToString("R")
                                +"; actualWorldB="+solid.Points[faces[i+1]].ToString("R")
                                +"; actualWorldC="+solid.Points[faces[i+2]].ToString("R")
                                +"; actualWorldHit="+ray.GetPoint(distance).ToString("R");
                            // Diagnostics read the same retained face that rejected the view.
                            // Include every actual draw material on the final physical submesh,
                            // where Unity may render additional material passes.
                            var materials=solid.Renderer.sharedMaterials;
                            int materialEnd=sub==solid.Faces.Length-1?materials.Length:sub+1;
                            for(int materialIndex=sub;materialIndex<materialEnd;materialIndex++)
                            {
                                var material=materials[materialIndex];
                                failure+="; drawMaterial["+materialIndex+"]="+(material!=null?material.name:"null");
                                if(material==null)continue;
                                failure+=", shader="+(material.shader!=null?material.shader.name:"null")
                                    +", queue="+material.renderQueue+", renderType="+material.GetTag("RenderType",false,"");
                                foreach(string property in new[]{"_Cull","_CullMode","_RenderFace","_AlphaClip"})
                                    failure+=", "+property+"="+(material.HasProperty(property)?material.GetFloat(property).ToString("R"):"missing");
                            }
                            return true;
                        }
                    }
                }
                failure=null;return false;
            }
        }

        private static bool CompetitionInspectionTriangleHit(Ray ray,Vector3 a,Vector3 b,Vector3 c,out float distance)
        {
            distance=0;Vector3 edge1=b-a,edge2=c-a,p=Vector3.Cross(ray.direction,edge2);
            float determinant=Vector3.Dot(edge1,p);if(Mathf.Abs(determinant)<1e-8f)return false;
            float inverse=1/determinant;Vector3 from=ray.origin-a;float u=Vector3.Dot(from,p)*inverse;
            if(u<0 || u>1)return false;
            Vector3 q=Vector3.Cross(from,edge1);float v=Vector3.Dot(ray.direction,q)*inverse;
            if(v<0 || u+v>1)return false;
            distance=Vector3.Dot(edge2,q)*inverse;return distance>=0;
        }

        private static void AssertCompetitionInspectionUsesFacesRatherThanJoinedBounds()
        {
            var root=new GameObject("Owned inspection joined-wall fixture");Mesh mesh=null;Material opaque=null,transparent=null;
            try
            {
                root.hideFlags=HideFlags.HideAndDontSave;
                root.transform.SetPositionAndRotation(new Vector3(73,19,-47),Quaternion.Euler(0,37,0));
                root.transform.localScale=new Vector3(1.4f,.8f,1.2f);
                mesh=new Mesh {hideFlags=HideFlags.HideAndDontSave};
                mesh.vertices=new[]{new Vector3(-2,-1,1),new Vector3(-1,-1,1),new Vector3(-1,1,1),new Vector3(-2,1,1),
                    new Vector3(1,-1,1),new Vector3(2,-1,1),new Vector3(2,1,1),new Vector3(1,1,1),
                    new Vector3(-.5f,-1,1),new Vector3(.5f,-1,1),new Vector3(.5f,1,1),new Vector3(-.5f,1,1)};
                mesh.subMeshCount=2;mesh.SetTriangles(new[]{0,1,2,0,2,3,4,5,6,4,6,7},0);
                mesh.SetTriangles(new[]{8,9,10,8,10,11},1);mesh.RecalculateBounds();
                root.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=root.AddComponent<MeshRenderer>();
                var shader=Shader.Find("Universal Render Pipeline/Lit");Assert.That(shader,Is.Not.Null);
                opaque=new Material(shader) {hideFlags=HideFlags.HideAndDontSave,renderQueue=2000};
                transparent=new Material(shader) {hideFlags=HideFlags.HideAndDontSave,renderQueue=3000};
                renderer.sharedMaterials=new[]{opaque,transparent};
#if UNITY_EDITOR
                mesh.UploadMeshData(true);Assert.That(mesh.isReadable,Is.False,"The imported shell also disables CPU read/write.");
#endif
                var geometry=new CompetitionInspectionGeometry(new Renderer[]{renderer});
                var direction=root.transform.TransformDirection(Vector3.forward);
                var clearRay=new Ray(root.transform.TransformPoint(Vector3.zero),direction);
                Assert.That(renderer.bounds.IntersectRay(clearRay),Is.True,"The joined bounds intersects a physically open sight line.");
                Assert.That(geometry.Blocked(clearRay,3,out _),Is.False,"Actual opaque faces leave the opening clear; the transparent submesh cannot close it.");
                var blockedRay=new Ray(root.transform.TransformPoint(new Vector3(1.5f,0,0)),direction);
                Assert.That(geometry.Blocked(blockedRay,3,out string obstruction),Is.True,"A genuine wall face remains a blocker.");
                Assert.That(obstruction,Does.Contain("submesh=0"));Assert.That(obstruction,Does.Contain("triangle="));
                Assert.That(geometry.Blocked(blockedRay,.5f,out _),Is.False,"A face beyond the target segment cannot block it.");
                var backRay=new Ray(root.transform.TransformPoint(new Vector3(1.5f,0,2)),-direction);
                Assert.That(geometry.Blocked(backRay,3,out _),Is.True,"The same opaque wall is detected from its opposite side.");
            }
            finally
            {
                Object.DestroyImmediate(root);if(mesh!=null)Object.DestroyImmediate(mesh);
                if(opaque!=null)Object.DestroyImmediate(opaque);if(transparent!=null)Object.DestroyImmediate(transparent);
            }
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
                Readouts=CompetitionInspectionReadouts(instrument),
                BodyGeometry=new CompetitionInspectionGeometry(visual.GetComponentsInChildren<Renderer>().Where(renderer=>renderer.enabled
                    && renderer.gameObject.activeInHierarchy && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                    && renderer.GetComponent<TMP_Text>()==null && renderer.sharedMaterials.Any(CompetitionInspectionOpaque)).ToArray()),
                ApparatusGeometry=new CompetitionInspectionGeometry(instrument.GetComponentsInChildren<Renderer>().Where(renderer=>renderer.enabled
                    && renderer.gameObject.activeInHierarchy && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                    && renderer.GetComponent<TMP_Text>()==null && renderer.sharedMaterials.Any(CompetitionInspectionOpaque)).ToArray()),
                Geometry=new CompetitionInspectionGeometry(SceneComponents<Renderer>().Where(renderer=>renderer.enabled && renderer.gameObject.activeInHierarchy
                    && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) && renderer.GetComponent<TMP_Text>()==null
                    && !renderer.transform.IsChildOf(player.transform) && !renderer.transform.IsChildOf(instrument.transform)
                    && renderer.sharedMaterials.Any(CompetitionInspectionOpaque)).ToArray())};
            var anchor=instrument.transform.parent;
            var failures=new List<string>();int candidate=0;
            eye.fieldOfView=42;eye.orthographic=false;
            foreach(float distance in new[]{4.2f,4.8f,5.6f})
            foreach(float lift in new[]{1.0f,1.55f})
            foreach(float side in new[]{1.4f,-1.4f,1.9f,-1.9f,0f})
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

        private static void AssertCompetitionInspectionUsesReadableUnobscuredGlyphs()
        {
            var root=new GameObject("Owned readout visibility fixture");Material opaque=null;
            try
            {
                root.hideFlags=HideFlags.HideAndDontSave;
                root.transform.position=new Vector3(73,19,-47);
                var anchor=new GameObject("Native readout anchor");anchor.transform.SetParent(root.transform,false);
                var definition=CompetitionDefinitions.All.First(value=>value.Category=="Luck");
                var instrument=CompetitionApparatus.Create(anchor.transform,definition,definition.Category,"inspection-fixture",UiTheme.Gold);
                // Match the native actor-radius fit before posing any readout. The cube
                // below has the same 320 mm lateral body radius and remains stationary.
                instrument.FitActorClearance(.32f);
                var cameraObject=new GameObject("Owned readout eye");cameraObject.transform.SetParent(root.transform,false);
                var eye=cameraObject.AddComponent<Camera>();eye.enabled=false;eye.fieldOfView=42;eye.aspect=1600f/900f;
                var readouts=CompetitionInspectionReadouts(instrument);
                var progress=readouts.Single(readout=>readout.Label.name=="Instrument progress");
                var focus=(progress.Left+progress.Right)*.5f;
                var view=new CompetitionInspectionView {Readouts=readouts,BodyGeometry=new CompetitionInspectionGeometry(Array.Empty<Renderer>()),
                    ApparatusGeometry=new CompetitionInspectionGeometry(Array.Empty<Renderer>()),Geometry=new CompetitionInspectionGeometry(Array.Empty<Renderer>())};
                Action<Vector3> point=local=>{view.Eye=root.transform.TransformPoint(local);eye.transform.SetPositionAndRotation(view.Eye,Quaternion.LookRotation(focus-view.Eye,Vector3.up));};
                point(new Vector3(0,1.3f,-2));
                Assert.That(CompetitionInspectionReadoutsVisible(eye,view,out var clear),Is.True,"Actual front glyphs are readable: "+clear);
                point(new Vector3(0,1.3f,3));
                Assert.That(CompetitionInspectionReadoutsVisible(eye,view,out var back),Is.False,"The same glyph mesh is mirrored from behind.");
                Assert.That(back,Does.Contain("progress front/legibility"));
                var body=GameObject.CreatePrimitive(PrimitiveType.Cube);body.name="Owned player-sized occluder";body.transform.SetParent(root.transform,false);
                body.transform.localPosition=new Vector3(0,1.01f,0);body.transform.localScale=new Vector3(.64f,1.92f,.40f);
                opaque=new Material(Shader.Find("Universal Render Pipeline/Lit")) {hideFlags=HideFlags.HideAndDontSave,renderQueue=2000};
                var renderer=body.GetComponent<Renderer>();renderer.sharedMaterial=opaque;
                view.BodyGeometry=new CompetitionInspectionGeometry(new[]{renderer});
                point(new Vector3(0,1.3f,-2));
                Assert.That(CompetitionInspectionReadoutsVisible(eye,view,out var hidden),Is.False,
                    "Clear external scenery alone cannot accept a player covering the actual progress glyphs.");
                Assert.That(hidden,Does.Contain("Owned player-sized occluder"));
                // A guessed oblique eye still grazed this actual cube at some glyphs.
                // Prove an independent clear box segment for every current glyph before
                // asking the mesh-face witness to accept the same unchanged body.
                Vector3 clearEye=Vector3.zero;bool found=false;
                foreach(float distance in new[]{1f,1.5f,2f,3f})
                {
                    foreach(float sideDistance in new[]{2f,3f,4f,5f})
                    {
                        var candidate=new Vector3(sideDistance,1.3f,-distance);
                        var world=root.transform.TransformPoint(candidate);
                        var direction=(world-focus).normalized;
                        if(Vector3.Dot(progress.Front,direction)<.42f)continue;
                        if(progress.GlyphCenters.Any(target=>renderer.bounds.IntersectRay(new Ray(world,(target-world).normalized),out float entry)
                            && entry<Vector3.Distance(world,target)))continue;
                        clearEye=candidate;found=true;break;
                    }
                    if(found)break;
                }
                Assert.That(found,Is.True,"The bounded synthetic eyes need a proven clear box segment for every actual progress glyph.");
                point(clearEye);
                Debug.Log("[Gamesim W3] synthetic actual readout box-proof eye="+clearEye.ToString("F4")+"; glyphs="+progress.GlyphCenters.Length);
                Assert.That(CompetitionInspectionReadoutsVisible(eye,view,out var side),Is.True,
                    "A physically clear oblique eye can show the same native glyphs without moving the body: "+side);
                // The instrument's own pedestal/backings and genuine external geometry
                // must participate in the glyph rays, not just coarse subject targets.
                view.ApparatusGeometry=new CompetitionInspectionGeometry(instrument.GetComponentsInChildren<Renderer>()
                    .Where(part=>part.enabled && part.GetComponent<TMP_Text>()==null && part.sharedMaterials.Any(CompetitionInspectionOpaque)).ToArray());
                Assert.That(CompetitionInspectionReadoutsVisible(eye,view,out var supported),Is.True,
                    "The corrected front plane is clear of its own native pedestal and contrast backing: "+supported);
                var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.name="Owned external glyph blocker";obstacle.transform.SetParent(root.transform,false);
                obstacle.transform.position=Vector3.Lerp(view.Eye,progress.GlyphCenters[progress.GlyphCenters.Length/2],.5f);
                obstacle.transform.localScale=Vector3.one*.16f;
                obstacle.GetComponent<Renderer>().sharedMaterial=opaque;
                view.Geometry=new CompetitionInspectionGeometry(new[]{obstacle.GetComponent<Renderer>()});
                Assert.That(CompetitionInspectionReadoutsVisible(eye,view,out var external),Is.False,"A saved opaque face across a real glyph ray stays obstructed.");
                Assert.That(external,Does.Contain("Owned external glyph blocker"));
            }
            finally {Object.DestroyImmediate(root);if(opaque!=null)Object.DestroyImmediate(opaque);}
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

        private static CompetitionInspectionReadout[] CompetitionInspectionReadouts(CompetitionApparatus instrument)
        {
            var readouts=new List<CompetitionInspectionReadout>();
            foreach(var label in instrument.GetComponentsInChildren<TMP_Text>().Where(label=>label.enabled
                && label.gameObject.activeInHierarchy && !label.name.EndsWith(" audience readout",StringComparison.Ordinal)))
            {
                // Refresh only the existing native text mesh, after the UI read. No pose,
                // progress value, material, actor or gameplay camera is changed here.
                label.ForceMeshUpdate();
                var glyphs=label.textInfo.characterInfo.Take(label.textInfo.characterCount).Where(character=>character.isVisible).ToArray();
                if(glyphs.Length==0 || !label.text.Any(character=>char.IsLetterOrDigit(character) || character=='?'))continue;
                var bounds=label.textBounds;var center=bounds.center;
                var first=glyphs[0];
                Vector3 front=Vector3.Cross(first.topLeft-first.bottomLeft,first.bottomRight-first.bottomLeft).normalized;
                readouts.Add(new CompetitionInspectionReadout {Label=label,Text=label.text,Matrix=label.transform.localToWorldMatrix,GlyphBounds=bounds,
                    Front=label.transform.TransformDirection(front).normalized,
                    Left=label.transform.TransformPoint(new Vector3(bounds.min.x,center.y,center.z)),
                    Right=label.transform.TransformPoint(new Vector3(bounds.max.x,center.y,center.z)),
                    Bottom=label.transform.TransformPoint(new Vector3(center.x,bounds.min.y,center.z)),
                    Top=label.transform.TransformPoint(new Vector3(center.x,bounds.max.y,center.z)),
                    GlyphCenters=glyphs.Select(character=>label.transform.TransformPoint((character.bottomLeft+character.topRight)*.5f)).ToArray()});
            }
            Assert.That(readouts.Any(readout=>readout.Label.name=="Instrument progress"),Is.True,"Actual progress glyph geometry must be present.");
            return readouts.ToArray();
        }

        private static bool CompetitionInspectionReadoutsVisible(Camera eye,CompetitionInspectionView view,out string failure)
        {
            int visible=0;
            foreach(var readout in view.Readouts)
            {
                if(readout.Label==null || readout.Label.text!=readout.Text || readout.Label.transform.localToWorldMatrix!=readout.Matrix)
                {failure="an actual readout changed during synchronous inspection";return false;}
                var currentGlyphs=readout.Label.textInfo.characterInfo.Take(readout.Label.textInfo.characterCount).Where(character=>character.isVisible).ToArray();
                if(readout.Label.textBounds!=readout.GlyphBounds || currentGlyphs.Length!=readout.GlyphCenters.Length
                    || currentGlyphs.Where((character,index)=>Vector3.Distance(readout.Label.transform.TransformPoint((character.bottomLeft+character.topRight)*.5f),
                        readout.GlyphCenters[index])>.00001f).Any())
                {failure="actual glyph geometry changed during synchronous inspection: "+readout.Label.name;return false;}
                var center=(readout.Left+readout.Right)*.5f;
                float front=Vector3.Dot(readout.Front,(view.Eye-center).normalized);
                var left=eye.WorldToViewportPoint(readout.Left);var right=eye.WorldToViewportPoint(readout.Right);
                var bottom=eye.WorldToViewportPoint(readout.Bottom);var top=eye.WorldToViewportPoint(readout.Top);
                float pixels=(top.y-bottom.y)*900f;
                bool readable=front>=.4f && left.z>eye.nearClipPlane && right.x>left.x && pixels>=8f;
                int clear=0;string blocker=null;
                if(readable)foreach(var target in readout.GlyphCenters)
                {
                    var to=target-view.Eye;
                    var ray=new Ray(view.Eye,to.normalized);float length=to.magnitude-.001f;
                    bool blocked=view.BodyGeometry.Blocked(ray,length,out var obstruction)
                        || view.ApparatusGeometry.Blocked(ray,length,out obstruction)
                        || view.Geometry.Blocked(ray,length,out obstruction);
                    if(!blocked)clear++;
                    else if(blocker==null)blocker=obstruction;
                }
                bool enough=readable && clear>=Mathf.CeilToInt(readout.GlyphCenters.Length*.8f);
                if(readout.Label.name=="Instrument progress" && (!readable || clear!=readout.GlyphCenters.Length))
                {failure="progress front/legibility/opaque occlusion: front="+front.ToString("F3")+", heightPixels="+pixels.ToString("F2")
                    +", clearGlyphs="+clear+"/"+readout.GlyphCenters.Length+", firstOpaqueBlocker="+blocker;return false;}
                if(enough)visible++;
            }
            if(visible<Mathf.CeilToInt(view.Readouts.Length*.8f))
            {failure="opaque geometry obscures actual front readouts: visible="+visible+"/"+view.Readouts.Length+" (80% required)";return false;}
            failure=null;return true;
        }

        private bool CompetitionInspectionVisible(Camera eye,CompetitionApparatus instrument,CompetitionInspectionView view,out string failure)
        {
            if(!CompetitionInspectionReadoutsVisible(eye,view,out failure))return false;
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
                // bb_shell_house joins distant walls into one house-wide bounds. Its AABB is
                // only a broad phase; retain genuine opaque faces without closing its openings.
                if(view.Geometry.Blocked(ray,to.magnitude-.03f,out failure))return false;
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
            var current=new CompetitionInspectionView {Eye=view.Eye,Body=body,Instrument=apparatus,Geometry=view.Geometry,
                Readouts=view.Readouts,BodyGeometry=view.BodyGeometry,ApparatusGeometry=view.ApparatusGeometry,
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
