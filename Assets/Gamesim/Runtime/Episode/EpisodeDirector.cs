using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Gamesim.Episode
{
    /// <summary>Unity interaction adapter. Only committed simulation snapshots drive presentation.</summary>
    [DisallowMultipleComponent]
    public sealed partial class EpisodeDirector : MonoBehaviour
    {
        [SerializeField] private HousePlayerController player;
        [SerializeField] private HouseCameraRig cameraRig;
        private FollowRing followRing;
        [SerializeField] private HouseNpc[] housemates;
        private readonly List<HouseNpc> spareHousemates = new List<HouseNpc>();
        public static string SaveRootOverride { get; set; }
        private EpisodeEngine engine;
        private EpisodeState projected;
        private EpisodeSaveStore saves;
        private EpisodeHud hud;
        private CeremonySting sting;
        private CeremonyTakeover takeover;
        private VoteReveal voteReveal;
        private CompetitionResult competitionCard;
        private KeyCeremony keyCeremony;
        private HouseTutorial tutorial;
        private OpeningSequence opening;
        private MemoryWall memoryWall;
        private SeasonReport seasonReport;
        private WeeklyRecapScreen weeklyRecap;
        private Coroutine recapWait;
        private CastSelect castSelect;
        private CharacterCreator characterCreator;
        private MainMenu mainMenu;
        private HouseAudio audioBed;
        private HouseNpc focusedNpc;
        // What the last social command actually moved, so the panel can say so.
        private double lastSocialDelta;
        private string saveRoot, message = "Welcome home. Meet the housemates, then visit the living-room screen.";
        private bool blockedRecovery, reducedMotion, muted, largeText, phaseOpen, settingsOpen, journalOpen;
        /// <summary>Which page of the notebook is showing. The rail picks it; Render obeys it.</summary>
        private string journalSection = NotebookSection.Network;
        private bool challengeActive;
        private int challengeHits;
        private double challengeTotal;
        private float challengeStarted, challengeValue, frameAverage;
        private EpisodeState challengeOrigin;
        // Null while the timing bar is running, which keeps its own value in challengeValue. The
        // three ported games hold their whole state here instead, so the director does not grow a
        // field per game and the behaviour stays drivable from a test.
        private MiniGameRun challengeRun;
        private Vector3 initialPlayerPosition;
        private Vector3[] initialNpcPositions;
        private Quaternion[] initialNpcRotations;
        private readonly RaycastHit[] sightHits = new RaycastHit[32];
        private bool playerIsActive;
        /// <summary>Master volume as a percentage, and whether the ambient bed plays. Both were
        /// reachable in HouseAudio but only mute was ever exposed, so a player could silence the
        /// game or leave it alone and nothing in between.</summary>
        private int volumePercent = 35;
        private bool musicOn = true;
        private HouseNpc promptNpc;
        private string npcPrompt;
        private EpisodeCommandKind? lastSocialAction;
        // Where the houseguest said they stood BEFORE the last accepted action, in their own words.
        // The reply to a Talk says it again only when it changed. Presentation only: never saved,
        // never rolled, and read only while lastSocialAction holds the action it belongs to.
        private string standingLineBefore;
        public EpisodeState Snapshot => engine?.Snapshot;
        public bool IsReady { get; private set; }
        // The weekly recap counts: it is a full-screen scrim, and a player who can still walk
        // the house behind it would be steering a character they cannot see.
        /// <summary>Who the conversation panel is open on, or null. A read, for tests and the world.</summary>
        public string TalkingToId => focusedNpc != null ? focusedNpc.Id : null;

        /// <summary>Who the player clicked and is walking towards, or null. A read, as above.</summary>
        public string WalkingToId => headingToNpcId;

        // The opening counts too: it owns the house while it plays, so the player cannot walk, the
        // houseguests do not tick and nothing underneath answers a key.
        public bool IsPanelOpen => blockedRecovery || focusedNpc != null || phaseOpen || settingsOpen || sceneCardOpen
                                   || journalOpen || diaryOpen || houseActivitiesOpen || juryHouseOverHouse || IsWeeklyRecapOpen || IsSeasonReportOpen
                                   || (competitionCard != null && competitionCard.IsPlaying) || OpeningOwnsHouse;
        /// <summary>Whether the season report is up: a full-screen card over the house, a panel by any reckoning.</summary>
        public bool IsSeasonReportOpen => seasonReport != null && seasonReport.IsShowing;
        /// <summary>Whether the episode screen is the panel open, rather than a conversation or the notebook.</summary>
        public bool IsPhasePanelOpen => phaseOpen;
        /// <summary>Whether a conversation with a houseguest is the panel open.</summary>
        public bool IsConversationOpen => focusedNpc != null;
        public bool IsChallengeActive => challengeActive;
        public float AverageFrameMilliseconds => frameAverage * 1000;
        public string SavePath => saves?.SavePath;
        public string StatusMessage => message;

        public void Configure(HousePlayerController controller, HouseCameraRig rig, HouseNpc[] npcs)
        { player = controller; cameraRig = rig; housemates = npcs; }

        private IEnumerator Start()
        {
            yield return null; // Surface and player navigation initialize before any saved episode is installed.
            // One authored body is enough. This used to demand exactly five, which was the scene's
            // shape rather than the game's rule, and it is what made the house a fixed six-person
            // set: the simulation could describe a larger cast and the set could not hold one. Any
            // houseguest the scene was not authored with is cloned from the first body below.
            if (player == null || cameraRig == null || housemates == null || housemates.Length < 1 || !player.Agent.isOnNavMesh)
            { Debug.LogError("Gamesim episode could not initialize its house wiring."); yield break; }
            initialPlayerPosition = player.transform.position;
            ResolveDiaryRoom();
            // Explicit launch-only isolation for QA; never changes the user's active slot preference.
            var launchArguments = Environment.GetCommandLineArgs();
            var rootArgument = Array.IndexOf(launchArguments, "--gamesim-save-root");
            if (SaveRootOverride == null && rootArgument >= 0)
            {
                if (rootArgument + 1 >= launchArguments.Length || !Path.IsPathRooted(launchArguments[rootArgument + 1]))
                { Debug.LogError("--gamesim-save-root requires an absolute directory path."); yield break; }
                SaveRootOverride = Path.GetFullPath(launchArguments[rootArgument + 1]);
            }
            saveRoot = SaveRootOverride ?? Path.Combine(Application.persistentDataPath, "Gamesim");
            var slot = SaveRootOverride == null ? PlayerPrefs.GetString("Gamesim.ActiveSave", "episode.json") : "episode.json";
            if (slot != Path.GetFileName(slot) || !slot.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) slot = "episode.json";
            saves = new EpisodeSaveStore(Path.Combine(saveRoot, slot));
            career = new CareerLedger(saveRoot);
            engine = new EpisodeEngine(ContentCatalog.Create(20260910));
            if (File.Exists(saves.SavePath) || File.Exists(saves.BackupPath))
            {
                if (saves.TryLoad(out var loaded, out var loadMessage)) { engine = new EpisodeEngine(loaded); message = loadMessage; RecordCareer(loaded); }
                else { blockedRecovery = true; message = loadMessage; }
            }
            // After the save is loaded, because the cast size is a property of the season being
            // played rather than of the scene, and a restored save may hold a different house from
            // the one a fresh season would create. The competition pendant comes down first: it hung
            // at head height over the anchor body slot 1 was authored on (EpisodeDirector.Seating.cs).
            StrikeCompetitionLamp();
            SeatCast(engine.Snapshot);

            audioBed = HouseAudio.Attach(gameObject);
            liveFeed = LiveFeed.Attach(gameObject);
            reducedMotion = SaveRootOverride == null && PlayerPrefs.GetInt("Gamesim.ReducedMotion", 0) == 1;
            reducedAudio = SaveRootOverride == null && PlayerPrefs.GetInt("Gamesim.ReducedAudio", 0) == 1;
            muted = SaveRootOverride == null && PlayerPrefs.GetInt("Gamesim.Muted", 0) == 1;
            largeText = SaveRootOverride == null && PlayerPrefs.GetInt("Gamesim.LargeText", 0) == 1;
            volumePercent = SaveRootOverride == null ? Mathf.Clamp(PlayerPrefs.GetInt("Gamesim.Volume", 35), 0, 100) : 35;
            musicOn = SaveRootOverride != null || PlayerPrefs.GetInt("Gamesim.Music", 1) == 1;
            ceremonyPace = SaveRootOverride == null && PlayerPrefs.GetInt("Gamesim.CeremonyPace", 0) == 1
                ? CeremonyPace.Quick : CeremonyPace.Suspenseful;
            LoadDisplayPreferences();
            hud = gameObject.AddComponent<EpisodeHud>(); hud.Initialize(this);
            sting = CeremonySting.Attach(gameObject);
            takeover = CeremonyTakeover.Attach(gameObject);
            voteReveal = VoteReveal.Attach(gameObject);
            // The reveals make their own sounds as they reach each beat - a vote, the result -
            // rather than the commit making the result's sound before the card has counted to it.
            voteReveal.CueRequested += cue => { if (audioBed != null) audioBed.PlayCue(cue); };
            juryReveal = JuryReveal.Attach(gameObject);
            juryReveal.CueRequested += cue => { if (audioBed != null) audioBed.PlayCue(cue); };
            competitionCard = CompetitionResult.Attach(gameObject);
            competitionCard.VisibilityChanged += SyncCompetitionResultInput;
            hud.RegisterOverlay(competitionCard.GetComponent<CanvasGroup>());
            keyCeremony = KeyCeremony.Attach(gameObject);
            keyCeremony.CueRequested += cue => { if (audioBed != null) audioBed.PlayCue(cue); };
            // The staged ceremonies' skip, on screen while one runs (EpisodeDirector.CeremonyStage).
            skipChip = CeremonySkipChip.Attach(gameObject);
            tutorial = HouseTutorial.Attach(gameObject);
            tutorial.RememberCompletion = SaveRootOverride == null;
            // The reference build's rising blip on every step of the tour.
            tutorial.StepSound = () => { if (audioBed != null) audioBed.PlayCue(HouseAudio.Cue.TutorialStep); };
            opening = OpeningSequence.Attach(gameObject);
            // Both take the keyboard while they are up: the opening's Continue and skip, and the
            // tour's Next, rather than a HUD control hidden underneath them.
            hud.RegisterOverlay(opening.GetComponent<CanvasGroup>());
            hud.RegisterOverlay(tutorial.GetComponent<CanvasGroup>());
            seasonReport = SeasonReport.Attach(gameObject);
            weeklyRecap = WeeklyRecapScreen.Attach(gameObject);
            hud.RegisterOverlay(seasonReport.GetComponent<CanvasGroup>());
            hud.RegisterOverlay(weeklyRecap.GetComponent<CanvasGroup>());
            castSelect = CastSelect.Attach(gameObject);
            characterCreator = CharacterCreator.Attach(gameObject);
            var profileStore = new CharacterProfileStore(SaveRootOverride == null ? null : Path.Combine(saveRoot, "Houseguests"));
            characterCreator.ConfigureProfiles(profileStore);
            castSelect.ConfigureProfiles(profileStore);
            castSelect.ConfigureCreator(characterCreator);
            mainMenu = MainMenu.Attach(gameObject);
            // The front door is a screen too: while any of these is up, it owns the keyboard.
            hud.RegisterOverlay(castSelect.GetComponent<CanvasGroup>());
            hud.RegisterOverlay(characterCreator.GetComponent<CanvasGroup>());
            hud.RegisterOverlay(mainMenu.GetComponent<CanvasGroup>());
            ApplyPreferences(); Project(); Render(); IsReady = true;
            // A real launch opens at the front door. A run with an explicit save root is a test or
            // the standalone verification driving the house directly, and a menu it never asked for
            // would block every one of them — so those keep the previous behaviour and reach the
            // menu through OpenMainMenu when they mean to.
            if (SaveRootOverride == null || launchArguments.Contains("--gamesim-front-door")) OpenMainMenu();
            // After the first Render, so the chrome the tour points at exists to be found.
            else PlayOpening();
            Debug.Log("Gamesim episode ready: " + engine.Snapshot.contestants.Count + " contestants, validated simulation, local recovery and accessible HUD connected.");
        }

        /// <summary>
        /// Gives every houseguest in the season a body, whatever size the house is.
        ///
        /// <para>The scene authors five, which is exactly right for the six-person scenario and
        /// wrong for any other. Rather than requiring a scene edit per cast size, any houseguest
        /// without a body gets one cloned from the first authored body — the same thing the editor
        /// setup pass does, done at runtime — and any spare body is switched off rather than left
        /// standing in the house as a nameless extra.</para>
        ///
        /// <para>Cloned bodies are placed by sampling the NavMesh outward from the template, so a
        /// larger cast does not spawn stacked inside one another or off the walkable surface.</para>
        /// </summary>
        private void FitHousematesToCast(EpisodeState state)
        {
            var cast = state.contestants.Where(c => !c.isPlayer).ToList();
            var bodies = housemates.Where(npc => npc != null).ToList();
            foreach (var spare in spareHousemates)
                if (spare != null && !bodies.Contains(spare)) bodies.Add(spare);
            spareHousemates.Clear();
            var template = bodies.FirstOrDefault();
            if (template == null || cast.Count == 0) return;

            for (int i = bodies.Count; i < cast.Count; i++)
            {
                var clone = CharacterPresentation.CloneUnbound(template.gameObject, template.transform.parent);
                // The template is usually a bound body. Its motion owner and the agent that owner
                // created are per-body runtime state: a copy of either is a component nobody owns,
                // and the coordinator refused every such clone its rebind - which is how a season
                // larger than the authored five stood still after its first reload. Strip them so the
                // clone gets its own, and give it back the carving obstacle a bound body has off.
                foreach (var owner in clone.GetComponents<HouseNpcMotion>()) DestroyImmediate(owner);
                foreach (var agent in clone.GetComponents<NavMeshAgent>()) DestroyImmediate(agent);
                var carving = clone.GetComponent<NavMeshObstacle>();
                if (carving != null) carving.enabled = true;
                clone.transform.SetPositionAndRotation(SpawnNear(template, i), template.transform.rotation);
                var body = clone.GetComponent<HouseNpc>();
                if (body == null) { Destroy(clone); break; }
                bodies.Add(body);
            }

            for (int i = cast.Count; i < bodies.Count; i++)
            {
                bodies[i].gameObject.SetActive(false);
                spareHousemates.Add(bodies[i]);
            }

            housemates = bodies.Take(cast.Count).ToArray();
            for (int i = 0; i < housemates.Length; i++)
            {
                var npc = housemates[i];
                npc.gameObject.SetActive(true);
                npc.Configure(cast[i].id, cast[i].name);
                npc.gameObject.name = cast[i].name;
                // Attach releases and rebuilds when the character id changed, so a recycled body
                // never keeps the previous houseguest's face.
                CharacterPresentation.Attach(npc.gameObject, CharacterOutfits.ForPhase(cast[i], state.phase), CastPalette.For(cast[i].id));
            }
            Physics.SyncTransforms();
        }

        /// <summary>
        /// Gives the season's cast bodies and keeps the authored-placement anchors describing them.
        ///
        /// <para>The anchors are read back by index when a season is installed, so they have to stay
        /// the same length as <see cref="housemates"/>. A body that was already in the house keeps
        /// the anchor it shipped with — a shorter season must still put the original five back where
        /// the scene author put them — and a body created for a larger house adopts the NavMesh spot
        /// it was just placed on, because it has no authored home to return to.</para>
        /// </summary>
        private void SeatCast(EpisodeState state)
        {
            FitHousematesToCast(state);
            if (housemates == null) return;
            if (initialNpcPositions != null && initialNpcPositions.Length == housemates.Length) return;

            var positions = new Vector3[housemates.Length];
            var rotations = new Quaternion[housemates.Length];
            for (int i = 0; i < housemates.Length; i++)
            {
                bool authored = initialNpcPositions != null && i < initialNpcPositions.Length;
                positions[i] = authored ? initialNpcPositions[i] : housemates[i].transform.position;
                rotations[i] = authored ? initialNpcRotations[i] : housemates[i].transform.rotation;
            }
            // The one anchor the scene put on the competition course moves beside it, before anything
            // reads the anchors: a load, the opening's put-back and the introductions' framing all agree.
            SeatOffTheCourse(positions, rotations);
            initialNpcPositions = positions;
            initialNpcRotations = rotations;
        }

        /// <summary>
        /// A walkable spot near the template, spiralling outward so bodies do not stack - and one the
        /// house can name. A bare NavMesh sample once put a twelfth houseguest in a doorway, between
        /// two floors, where the motion owner refuses to bind ("not inside one bound floor's safe
        /// interior"), so every candidate is checked the way binding will check it: on one floor's
        /// safe interior, and clear of every body already standing there.
        /// </summary>
        private Vector3 SpawnNear(HouseNpc template, int index)
        {
            var origin = template.transform.position;
            var body = template.GetComponent<CapsuleCollider>();
            float radius = body != null ? body.radius : 0.35f, height = body != null ? body.height : 1.9f;
            HouseRoomQuery.TryCreate(gameObject.scene, out var rooms, out _);
            var agent = player != null ? player.Agent : null;
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = agent != null ? agent.agentTypeID : 0,
                areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas,
            };
            var fallback = origin;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                int step = index + attempt * 5;
                float angle = step * 137.5f * Mathf.Deg2Rad;   // golden angle: no two early picks align
                float reach = 1.6f + (step % 9) * 0.7f;
                var wanted = origin + new Vector3(Mathf.Cos(angle) * reach, 0f, Mathf.Sin(angle) * reach);
                if (!NavMesh.SamplePosition(wanted, out var hit, 6f, NavMesh.AllAreas)) continue;
                if (attempt == 0) fallback = hit.position;
                if (rooms == null) return hit.position;
                if (rooms.TrySampleFloor(hit.position, radius, filter, .25f, out var sampled, out _)
                    && rooms.HasCapsuleClearance(sampled, radius, height, template.transform))
                    return sampled;
            }
            return fallback;
        }

        private int seenBodiesCompleted;

        private string lastFollowed;

        private void Update()
        {
            // First, and whatever else returns early: the dip is only a clock, and a frame that
            // skipped it would hold the house black for as long as a card or a load owned the frame.
            TickTravelDip();
            TickTravelBeacons();
            TickSleepLight();
            // The staged ceremony before the ceremonies' bookkeeping: its card starts here, and the
            // hold on the chrome reads whether it is still telling its story.
            TickCeremonyStage();
            TickCeremonies();
            // After both have moved on: the chip says what a press does while a stage runs.
            TickSkipChip();
            // The music follows what is on screen every frame, before anything can return early:
            // the opening's loading gate and its closing fade change in the middle of a beat, with
            // nothing rendering. A state the bed is already in costs a comparison.
            if (IsReady) ApplyMusic();
            if (IsReady) { TickCompanion(); TickActivityEffects(); TickRoomArrival(); }
            // The houseguest the player is with keeps their plate up at any distance.
            if (housemates != null)
            {
                string with = TalkingToId ?? FollowedId;
                foreach (var housemate in housemates)
                    if (housemate != null) housemate.Spotlit = with != null && housemate.Id == with;
            }
            // A story's Pull (plan §5.1), then the Nearby card (mockup-06), which is up exactly
            // while a conversation is being witnessed and no Pull has the week card's place.
            TickStoryPull();
            TickSceneStage();
            TickNearby();
            // ] and [ (or the shoulders) cycle who the camera follows, out in the house with no panel
            // open. Tab does the same only when no HUD control is focused - a mouse player who
            // clicked the house - because with one focused, Tab is the keyboard ring's, and the HUD
            // keeps a control focused whenever it can.
            if (IsReady && !IsPanelOpen && !challengeActive && cameraRig != null && !TourIsUp && !CeremonyOverlays.OnScreen)
            {
                var actions = cameraRig.Actions;
                bool nothingFocused = EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null;
                if (actions.Next.WasPressedThisFrame()) FollowNext(false);
                else if (actions.Previous.WasPressedThisFrame()) FollowNext(true);
                else if (nothingFocused && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                    FollowNext(Keyboard.current.shiftKey.isPressed);
                // G is your moves: the card over your own chip.
                else if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame && !OpeningOwnsHouse) ToggleEmoteMenu();
            }
            if (IsReady) { TickEmote(); TickAnswers(); }
            // The chip follows the subject however it was chosen: a click on a body sets the
            // camera without a render, so the chip is redrawn on its own when the name changes.
            if (IsReady && hud != null && FollowedName != lastFollowed)
            {
                lastFollowed = FollowedName;
                hud.ShowFollowing(lastFollowed);
            }
            if (cameraRig != null && followRing == null) followRing = FollowRing.Attach(cameraRig);
            // A body that has just finished assembling changes what the HUD can show - a houseguest
            // who was not yet drawn is now there to be framed, followed and pointed at - so the HUD
            // is redrawn rather than left waiting for something else to trigger a render.
            //
            // Not while a panel is open. Rebuilding one because a body finished loading throws away
            // the player's scroll position and keyboard focus mid-read, for a portrait they are not
            // looking at — and it rebuilt the layout underneath the clipping test between its own
            // canvas update and its assertion. The counter is left unread until the panel closes,
            // so the refresh happens then instead of being lost.
            if (IsReady && !IsPanelOpen && CharacterPresentation.BodiesCompleted != seenBodiesCompleted)
            {
                seenBodiesCompleted = CharacterPresentation.BodiesCompleted;
                Render();
            }

            if (!IsReady) return;
            // A dismissing key is consumed for the whole frame, even if the result updated first.
            if (competitionCard != null && competitionCard.OwnsInput) return;
            if (competitionInputSuspended) SyncCompetitionResultInput();
            TickNpcSocialRuntime(Time.unscaledDeltaTime);
            TickHouseActivities();
            TickProximityWatch(Time.unscaledDeltaTime);
            frameAverage = Mathf.Lerp(frameAverage, Time.unscaledDeltaTime, 0.03f);
            if (diaryOpen && (!CanUseDiary || diarySeat == null || !diarySeat.Active)) { ClosePanels(); return; }
            // The house's shortcuts come through the actions map's second page: each key has a
            // gamepad button beside it there, so a controller reaches every panel the keyboard does.
            var shortcuts = cameraRig != null ? cameraRig.Actions : null;
            TickRoomTone();
            TickOverview();
            TickLiveFeed();
            if (challengeActive && challengeRun != null) TickMiniGame();
            else if (challengeActive)
            {
                challengeValue = Mathf.PingPong((Time.unscaledTime - challengeStarted) * 0.75f, 1f);
                hud.SetChallenge(challengeValue, challengeHits);
                if (shortcuts != null && shortcuts.Hit.WasPressedThisFrame()) RecordChallengeHit();
            }
            if (shortcuts != null && shortcuts.Menu.WasPressedThisFrame())
            {
                // The game surface consumes Escape / Start itself, including the dismissal frame.
                if (competitionScreen != null && competitionScreen.OwnsMenuInput) return;
                if (challengeActive) { CancelChallenge(); return; }
                // The opening before anything: it draws over every screen, and Escape underneath it
                // used to close panels and release the shot it was holding.
                if (OpeningOwnsHouse) { OpeningMenuPressed(); return; }
                // A ceremony card reads Escape itself - it skips the reveal - so the press does not
                // also close the panels or open the settings underneath it.
                if (CeremonyOverlays.OnScreen) return;
                // The tour offered outside the opening - an imported season - dims the house and
                // takes the pointer; Escape closes it, as the tour's own card says.
                if (TourIsUp) { tutorial.Skip(); return; }
                // Topmost first. The main menu sits above the cast screen, which sits above the
                // HUD; closing a panel underneath either of them would leave a screen on top of the
                // house with nothing behind it. The menu itself ignores Escape when there is no
                // season to go back to, because there is nowhere for it to close to.
                if (mainMenu != null && mainMenu.IsShowing) { if (SeasonInProgress) CloseMainMenu(); }
                // The creator draws above the cast screen, so it takes Escape first — otherwise
                // the screen underneath would close out from under the form on top of it.
                else if (characterCreator != null && characterCreator.IsShowing) characterCreator.Dismiss();
                else if (castSelect != null && castSelect.IsShowing) castSelect.Dismiss();
                // The report draws over the finale panel; Escape closes it and leaves the panel.
                else if (IsSeasonReportOpen) seasonReport.Close();
                else
                {
                    // With nothing open, Escape does nothing - the keyboard has the HUD's own
                    // buttons - but a pad has no other way to the settings, so Start opens them.
                    bool wasOpen = IsPanelOpen;
                    ClosePanels();
                    var pressed = shortcuts.Menu.activeControl;
                    if (!wasOpen && pressed != null && pressed.device is Gamepad) OpenSettings();
                }
                return;
            }
            // Not while a ceremony card is up either: it reads the pad's face buttons itself - X
            // speeds a reveal up, and X is also Interact - so a press meant for the card went on to
            // act in the house underneath it.
            // Nor during a competition: the word game spells with every letter key, and J, E and
            // the rest would have opened the house's panels under the game.
            if (shortcuts != null && !hud.IsTyping && !OpeningOwnsHouse && !TourIsUp && !CeremonyOverlays.OnScreen && !challengeActive)
            {
                if (shortcuts.Notebook.WasPressedThisFrame()) OpenJournal();
                if (shortcuts.Save.WasPressedThisFrame()) SaveNow();
                if (shortcuts.Overview.WasPressedThisFrame() && !IsPanelOpen) ToggleOverview();
                if (shortcuts.Diary.WasPressedThisFrame() && !IsPanelOpen) GoToDiary();
                // Busy at a piece of furniture, E is getting up, before it is anything else.
                if (shortcuts.Interact.WasPressedThisFrame()) Interact();
            }
            // Nothing to press while a ceremony card is up: the card takes the pointer by reading the
            // mouse, not through a raycaster, and the click that dismissed it went on to press
            // whatever the prompt said underneath.
            if (!IsPanelOpen && !CeremonyOverlays.OnScreen)
            {
                // One decision, read twice. The prompt and the key used to run the same priority
                // chain in two places, which is two chances to disagree about what E does.
                var choice = ChooseInteraction(out var npc);
                if (npc != promptNpc) { promptNpc = npc; npcPrompt = npc != null ? "E  ·  Talk to " + npc.DisplayName : null; }
                string prompt = "", hint = null;
                if (IsPlayerHouseActivityActive && playerActivityInHouse) prompt = HouseFurniture.StopPrompt(playerActivityKind);
                else if (choice == InteractTarget.Diary) prompt = "E  ·  Enter private diary room";
                else if (choice == InteractTarget.Station) prompt = "E  ·  Open episode screen";
                else if (choice == InteractTarget.Talk)
                {
                    prompt = npcPrompt;
                    // At three, every conversation is with somebody the player may sit beside at
                    // the end or send to the jury: the line under the prompt says what one is for
                    // (MOCKUP-PASS M14, mockup 60).
                    if (EpisodeHud.IsFinalThree(projected)) hint = EpisodeHud.TalkHint;
                }
                else if (choice == InteractTarget.StepIn) prompt = "E  \u00b7  " + EpisodeHud.StepInCaption;
                hud.SetPrompt(prompt, hint);
            }
            else hud.SetPrompt("");
        }

        /// <summary>
        /// Whatever E does right now: get up from the furniture, enter the diary room, open the
        /// episode screen, talk to whoever is nearest. The key and the prompt's own button both come
        /// here, so a mouse reaches everything the key does and neither can disagree with the other.
        /// </summary>
        public void Interact()
        {
            if (!IsReady || IsPanelOpen) return;
            // Busy at a piece of furniture, E is getting up, before it is anything else.
            if (IsPlayerHouseActivityActive && playerActivityInHouse) { FinishPlayerHouseActivity(); return; }
            switch (ChooseInteraction(out var target))
            {
                case InteractTarget.Diary:
                    if (TryOpenDiary()) break;
                    if (target != null) TryOpenNpc(target.Id); else TryOpenPhasePanel();
                    break;
                case InteractTarget.Station:
                    if (!TryOpenPhasePanel() && target != null) TryOpenNpc(target.Id);
                    break;
                case InteractTarget.Talk:
                    // E says what a click on them says, so it asks for a talk spot as a click does.
                    TalkOnInteract(target);
                    break;
                case InteractTarget.StepIn:
                    StepIntoWalkIn();
                    break;
            }
        }

        /// <summary>
        /// "Friendly +18" - where the player stands with somebody, as a word and a signed number.
        ///
        /// <para>The word is the one the relationship graph already uses, not a second vocabulary:
        /// a houseguest the graph draws as Wary must not be described as anything else two panels
        /// away. The number goes with it because the word is banded and the bands are wide - +16
        /// and +39 are both "Friendly" - and the player is about to spend an action on the
        /// difference.</para>
        /// </summary>
        private static string Standing(EpisodeState state, string otherId)
        {
            string word = Presentation.RelationshipWeb.StandingWord(
                Presentation.RelationshipWeb.KindOf(state, otherId));
            return word + " " + state.Score(state.playerId, otherId).ToString("+0;-0;0");
        }

        /// <summary>The social actions the week has left, in the words the objective card uses.</summary>
        private static string ActionsLeft(EpisodeState state)
        {
            int left = Math.Max(0, EpisodeEngine.SocialActionBudget(state)
                - EpisodeEngine.SocialActionsSpent(state));
            return left + (left == 1 ? " action" : " actions");
        }

        /// <summary>Where a promise stands, in a word the player was not asked to learn.</summary>
        private static string PromiseStanding(PromiseStatus status)
        {
            switch (status)
            {
                case PromiseStatus.Fulfilled: return "kept";
                case PromiseStatus.Broken: return "broken";
                case PromiseStatus.Expired: return "expired";
                default: return "still standing";
            }
        }

        public Vector3 StationPosition => ResolveStationPosition();
        private bool CanUseStation() => !playerIsActive ||
            Vector3.Distance(player.transform.position, StationPosition) < 3;

        /// <summary>
        /// Whether the last thing you asked for was the episode screen.
        ///
        /// <para>The prompt used to be a fixed order - diary, then any houseguest, then the screen -
        /// and the two ranges make that order absolute: <c>NearestNpc</c> reaches 2.8 m and the
        /// screen needs you within 3 m, so standing close enough to use the screen almost guarantees
        /// somebody is close enough to outrank it. A houseguest idling by the screen did not make it
        /// awkward to reach, it made it unreachable, and there was no way to say "no, the screen".
        /// Asking for it is that way: having walked there on purpose, you get it until you use it or
        /// ask for something else.</para>
        /// </summary>
        private bool headingToStation;

        private enum InteractTarget { None, Diary, Talk, Station, StepIn }

        /// <summary>What the E key would do right now, and the houseguest it would do it to.</summary>
        private InteractTarget ChooseInteraction(out HouseNpc npc)
        {
            npc = NearestNpc();
            if (CanUseDiary) return InteractTarget.Diary;
            // Whoever was clicked outranks whoever happens to be standing closest, exactly as the
            // station does: two houseguests in one doorway are both inside the 2.8 m reach, and
            // without this the walk would end in a conversation with the wrong one.
            if (!string.IsNullOrEmpty(headingToNpcId))
            {
                var wanted = housemates.FirstOrDefault(actor => actor != null && actor.Id == headingToNpcId
                    && actor.gameObject.activeInHierarchy && CanTalk(actor));
                if (wanted != null) { npc = wanted; return InteractTarget.Talk; }
            }
            if (headingToStation && CanUseStation()) return InteractTarget.Station;
            // A walk-in's Pull takes the interact key (plan §5.6): the player is standing in it, and
            // stepping in only opens its card - the choice itself is still theirs to make.
            if (PullOffered == EpisodeHud.StepInCaption && walkInFirst != null) return InteractTarget.StepIn;
            if (npc != null) return InteractTarget.Talk;

            return CanUseStation() ? InteractTarget.Station : InteractTarget.None;
        }

        public bool TryOpenPhasePanel()
        {
            if (!IsReady || !CanUseStation()) return false;
            headingToStation = false;
            PauseNpcSocialForPanel();
            if (blockedRecovery) return false;
            ClosePanels(); phaseOpen = true; player.SetInputEnabled(false); cameraRig.ControlsEnabled = false; Render(); return true;
        }

        public void GoToStation()
        {
            // The last press put the player at the screen - a warp lands them there with "E to
            // open" - so this press is the screen: the mouse's own way in, beside E and the prompt's
            // button, so no player is left at the screen with a key they did not know to press. Only
            // after a warp: a short trip on foot is a walk, chosen afresh, and the screen opens
            // from the prompt when they get there.
            bool pressedAgainAtTheScreen = headingToStation && LastTravel == TravelKind.Warp && CanUseStation();
            ClosePanels();
            // Whatever the player was walking to, this replaces it. Without this a click on a
            // houseguest outlived the button press and either overwrote the path to the screen or
            // opened a conversation on the way there, under a status line promising the screen.
            CancelTravel();
            EndDiaryVisit(true);CloseHouseActivities(true);
            if (projected.Find(projected.playerId).status != ContestantStatus.Active) { TryOpenPhasePanel(); return; }
            if (pressedAgainAtTheScreen && TryOpenPhasePanel()) return;
            if (!TryTravel(StationPosition)) message = "The episode screen is not reachable from here.";
            else
            {
                // TryTravel puts the camera on them, walking, running or warped.
                headingToStation = true;
                // Shorter than it was, because it no longer has to narrate the camera. It used to
                // read "Walk to the highlighted room, then press E to open the episode screen" - a
                // full sentence of instructions for a walk you can now watch happen.
                message = LastTravel == TravelKind.Warp ? "At the episode screen  ·  E to open"
                    : "Heading to the episode screen  ·  E to open";
            }
            Render();
        }

        public void ClosePanels() => ClosePanelsInternal(true);

        private void ClosePanelsInternal(bool render)
        {
            CloseHouseActivities(!render);
            if (focusedNpc != null) focusedNpc.GetComponent<CharacterPresentation>()?.SetTalking(false);
            // What the player came into a conversation for goes with the conversation; a houseguest's
            // screen over free time goes with the panel.
            if (focusedNpc != null) conversationIntent = null;
            moveScreenId = null; comparingFinalists = false; juryHouseOpen = false; juryHouseOverHouse = false; reviewingSpeeches = false; juryQuestionsOpen = false;
            finalCaseOpen = false; ForgetFinalCaseChoice();
            // The finale page's open reason goes with the panel too.
            ForgetFinaleView();
            // The nomination's view goes with the panel, which opens again on the week's current step,
            // and so does free time's: its board opens again on its root and its first page.
            ForgetNominationView();
            ForgetFreeTimeView();
            focusedNpc = null; lastSocialDelta = 0d; phaseOpen = false; settingsOpen = false; journalOpen = false; challengeActive = false;
            // A chip's card goes with everything else Escape closes; the campaign opens folded.
            castMenuFor = null; emoteMenuOpen = false; campaignMore = false;
            ClearLobbyDraft();
            if (sceneCardOpen) { sceneCardOpen = false; sceneCardCycle = null; ClearStoryStep(); }
            // Escape cancels without committing, so the run goes with the panel. Leaving it would
            // let a competition keep ticking behind a closed screen and commit itself later.
            challengeRun = null;
            CloseCompetitionPresentation();
            // Results persist until dismissed, so replacing the panel or loading a season must
            // retire them too. A new competition result is shown after commit-time panel changes.
            if (competitionCard != null) competitionCard.Cancel();
            EndDiaryVisit(!render);
            // The line said where the player was; once they have left, it says so. Anything the
            // visit put there since - a result, a discarded choice - stays.
            if (diaryOpen && message == DiaryInsideMessage) message = "You left the private diary room.";
            diaryOpen = false; diaryDraft = null;
            lastSocialAction = null;
            // The recap is a panel by IsPanelOpen's reckoning, so closing panels has to close it —
            // otherwise the scrim stays up while everything behind it believes it is dismissed.
            if (weeklyRecap != null) weeklyRecap.Hide();
            // So is the season report, for the same reason.
            if (seasonReport != null) seasonReport.Hide();
            // Any panel opening ends the overview: the shot it took goes with it. A conversation's
            // two-shot ends with the conversation; the diary's chair shot is released here, which
            // covers Escape and the walk-away close alike.
            if (overviewOpen) { LeaveOverview(); if (cameraRig != null) cameraRig.ReleaseShot(OverviewSeconds); }
            // Not while the opening plays: it is holding the camera and the player, and gives both
            // back when it ends.
            if (cameraRig != null && !OpeningOwnsHouse) { cameraRig.EndConversation(); cameraRig.ReleaseShot(DiaryShotSeconds); cameraRig.ControlsEnabled = !blockedRecovery; }
            if (projected != null && player != null && !OpeningOwnsHouse) player.SetInputEnabled(!blockedRecovery && projected.Find(projected.playerId).status == ContestantStatus.Active);
            if (render && hud != null) Render();
        }

        public void OpenSettings() { PauseNpcSocialForPanel(); ClosePanels(); settingsOpen = true; player.SetInputEnabled(false); cameraRig.ControlsEnabled = false; Render(); }
        /// <summary>The notebook, on its own page: your notes on each houseguest. The rail's rows are the other pages.</summary>
        public void OpenJournal() => OpenNotebookAt(NotebookSection.Notes, scroll: false);

        /// <summary>
        /// The status line for the one event a commit lets the player see: its words, or a phase
        /// change in the words the week chip uses. The engine logs a phase by its enum's own name
        /// ("Week 4 · FinalHoHPart2"), and the saved log keeps that text as it is; only the line
        /// on the screen is worded. Null when there is no event to report.
        /// </summary>
        public static string StatusLine(EpisodeState state, EpisodeEvent visible)
        {
            if (visible == null) return null;
            // The finale's commit says the season is complete (UI-UX-PASS-PLAN decision 13); the
            // engine's "Gamesim winner: X!" stays as logged, and the page says who won.
            if (visible.kind == "winner") return SeasonCompleteToast;
            return visible.kind == "phase" ? PhaseLine(visible.phase, visible.week) : StoryText.Log(state, visible);
        }

        /// <summary>
        /// A phase as the status line announces it: "Week 4 · Final HoH, Part 2 of 3", "Week 4 · The
        /// final decision", "Jury questioning". The finale's phases have no week of their own worth
        /// naming; every other phase is the week chip's short word under its week.
        /// </summary>
        public static string PhaseLine(EpisodePhase phase, int week)
        {
            string part = FinalHoHPartLabel(phase);
            if (part != null) return "Week " + week + " · Final HoH, " + part;
            switch (phase)
            {
                case EpisodePhase.FinalEviction: return "Week " + week + " · The final decision";
                case EpisodePhase.JuryQuestioning: return "Jury questioning";
                case EpisodePhase.FinalSpeeches: return "Final speeches";
                case EpisodePhase.Jury: return "The jury votes";
                case EpisodePhase.Finished: return "The season is over";
                default: return "Week " + week + " · " + EpisodeHud.PhaseShort(phase);
            }
        }

        public CommandResult Submit(EpisodeCommand command)
        {
            if (durableCommitInProgress) return new CommandResult { reason = "A save transaction is already running.", state = Snapshot };
            if (blockedRecovery) return new CommandResult { reason = "Recover the save or start a new slot before playing.", state = Snapshot };
            bool wasYard = EpisodeEngine.IsCompetition(projected.phase);
            // A single command can append several events. Remembering where the log ended lets the
            // ceremony card look at everything this commit produced rather than only its last line.
            int knownEvents = engine.Snapshot.events.Count;
            // Who was still playing before this command. An eviction event says what happened
            // but not to whom, and diffing is more reliable than parsing the sentence back.
            var wasPhase = engine.Snapshot.phase;
            var wasWeek = engine.Snapshot.week;
            // Trust toward whoever the player is talking to, before this command. The
            // engine adjusts relationships by social stat and reciprocal rolls, so the
            // committed difference is the only honest number to report.
            double trustBefore = focusedNpc != null
                ? engine.Snapshot.Score(engine.Snapshot.playerId, focusedNpc.Id)
                : 0d;
            string lineBefore = focusedNpc != null ? HouseDialogue.Response(engine.Snapshot, focusedNpc.Id) : null;
            var wasActive = new HashSet<string>(engine.Snapshot.contestants
                .Where(actor => actor.status == ContestantStatus.Active).Select(actor => actor.id));
            // Who was on the block before this command: a veto ceremony is the difference. The same
            // block in the order it was named seats a staged veto meeting's red chairs; the
            // snapshot is the engine's copy already, so it is read once.
            var blockBefore = engine.Snapshot.nominees ?? new List<string>();
            var wasNominated = new HashSet<string>(blockBefore);
            // Player and NPC candidates share durable publication ordering. A phase
            // transition cancels invalid NPC activity inside this same saved candidate.
            var candidate = new EpisodeEngine(engine.Snapshot);
            var result = candidate.Apply(command);
            if (result.accepted)
            {
                if (TryPersistCandidate(result.state, out var installed, out var failure))
                { engine = new EpisodeEngine(installed); result.state = engine.Snapshot; }
                else
                {
                    message = failure;
                    PauseNpcSocialForPanel();
                    Render();
                    return new CommandResult { reason = failure, state = Snapshot };
                }
            }
            // The one event the player is entitled to see drives both the status line and the
            // ceremony card, so a title card can never announce something the notebook withholds.
            var visible = result.accepted
                ? result.state.events.LastOrDefault(e => e.audienceIds.Count == 0 || e.audienceIds.Contains(result.state.playerId))
                : null;
            message = result.accepted ? StatusLine(result.state, visible) ?? "Decision committed." : result.reason;
            if (result.accepted)
            {
                diaryDraft = null; // A draft never survives a different committed revision.
                if (focusedNpc != null)
                {
                    lastSocialAction = command.kind;
                    lastSocialDelta = result.state.Score(result.state.playerId, focusedNpc.Id) - trustBefore;
                    standingLineBefore = lineBefore;
                    // The conversation answers at its head, whatever was said: an open person picker
                    // shuts, as it does when one of its people is chosen (EpisodeDirector.ConversationGroups.cs).
                    conversationPick = null;
                }
                // The evicted houseguest stays in the room while the card narrates their eviction: the
                // house reacts to them, and they go when the card does (TickCeremonies).
                var evictedNow = EvictedThisCommit(result.state, wasActive);
                departingId = evictedNow != null && evictedNow != result.state.playerId ? evictedNow : null;
                message += "  ·  Saved locally."; Project();
                // A finale joins the career record the moment it is durable, and not before.
                RecordCareer(result.state);
                if (phaseOpen && wasYard != EpisodeEngine.IsCompetition(result.state.phase)) ClosePanels();
                // The commit's sound, from everything it appended (CommitCue). It plays once the beat's
                // cards are known: a key ceremony and a live eviction make their own sounds as they
                // reach the block and the result, and a commit that played the result's sound first
                // announced it before the card had counted to it. A beat of the opening is
                // bookkeeping and makes none; an introduction clicks through the opening's own hook.
                var commitCue = CommitCue(result.state.events.Skip(knownEvents)
                    .Where(entry => entry.audienceIds.Count == 0 || entry.audienceIds.Contains(result.state.playerId))
                    .Select(entry => entry.kind));
                bool revealed = false, vetoStaged = false;
                // The ceremony is not always the last thing a commit writes — an eviction is followed
                // by the events that open the next week, which is why keying off the final line
                // meant the eviction card never played at all. Search everything this command
                // appended, and only what the player is entitled to see: the cue is a sound, but the
                // card carries words.
                // A competition is not a CeremonySting kind — it has no strip — so it is searched
                // for separately. Same audience rule: the standings name everyone who competed.
                var competition = result.state.events.Skip(knownEvents)
                    .LastOrDefault(entry => entry.kind == "competition"
                        && (entry.audienceIds.Count == 0 || entry.audienceIds.Contains(result.state.playerId)));
                if (competition != null)
                {
                    // The standings are the one authority on who won: the card reads them and so
                    // does the body that cheers, so a houseguest can never celebrate a result the
                    // card puts second. The player cheers on the same terms as anyone else.
                    var standings = CompetitionStandings(result.state);
                    // The result is the beat now. A ceremony card the last beat left up is over, or
                    // it plays on under the standings: the veto draw's field sat behind the veto's
                    // result, its players and its "press to continue" showing through the glass.
                    EndCeremonyCards();
                    if (competitionCard != null)
                        competitionCard.Play(CompetitionTitle(result.state),
                            EpisodeEngine.CompetitionCategory(wasPhase, wasWeek, result.state.competitionRulesVersion, result.state.seed), result.state.week,
                            standings, reducedMotion, CompetitionPerformanceExplanation(result.state), pendingAttemptLine ?? ThrowAttemptLine(result.state),
                            WinnersMark(wasPhase));
                    React(CompetitionWinnerId(result.state, standings), CharacterPresentation.Reaction.Cheered);
                }

                // The veto field. Previously the one phase the episode passed through in silence.
                var field = result.state.events.Skip(knownEvents)
                    .LastOrDefault(entry => entry.kind == CeremonyTakeover.VetoSelectionKind
                        && (entry.audienceIds.Count == 0 || entry.audienceIds.Contains(result.state.playerId)));
                if (field != null && takeover != null)
                {
                    EndCeremonyCards(includingResult: competition == null);
                    // The draw itself when there was one and this house plays it: the chips turning
                    // into the faces they drew (EpisodeDirector.VetoDraw.cs). Otherwise the field's
                    // card: a house bigger than the field gets a line that says who plays and why;
                    // the card's own says everyone does (VetoFieldLine is null when that is true).
                    if (!PlayVetoDrawReveal(result.state))
                        takeover.Play(CeremonyTakeover.VetoSelectionKind, result.state.week,
                            VetoField(result.state), reducedMotion, null, VetoFieldLine(result.state));
                }

                var ceremony = result.state.events.Skip(knownEvents)
                    .LastOrDefault(entry => CeremonySting.IsCeremony(entry.kind)
                        && (entry.audienceIds.Count == 0 || entry.audienceIds.Contains(result.state.playerId)));
                if (ceremony != null)
                {
                    // A new ceremony replaces whatever card is still up. Each ends on its own timer,
                    // but a player who commits the next beat inside that time got the old card
                    // over the new one: the nomination's keys sit at sort 110 over the takeover's
                    // 100, so the veto field played underneath a finished nomination ceremony.
                    if (field == null) EndCeremonyCards(includingResult: competition == null);
                    EndCeremonyStage();
                    var committed = result.state;
                    string kind = ceremony.kind;
                    string text = ceremony.text;
                    // The takeover opens the scene and the sting reports the result, so they play
                    // together rather than instead of each other: the card is over by the time the
                    // strip has finished its own entrance.
                    // An eviction gets the vote reveal instead of the generic card: it is the only
                    // beat whose outcome is not already inferable, and the commit resolves every
                    // ballot in one frame. If the reveal declines the shape — a block that is not
                    // two, or no ballots — the generic card still plays, so the beat is never silent.
                    // The nomination gets the key ceremony for the same reason the eviction gets the
                    // vote reveal: the engine decides it in one commit, and the order is the beat.
                    // Either reveal plays on a screen when the house stages the ceremony
                    // (EpisodeDirector.CeremonyStage.cs): the stage gathers the house first and
                    // plays the card on the set's screen once the seats have filled, so the card is a
                    // function of the screen it is given - null being the HUD, as it always was.
                    Func<ScreenSurface, bool> reveal = null;
                    if (kind == CeremonySting.EvictionKind && voteReveal != null)
                        reveal = screen => voteReveal.Play(committed.week, EvictionBlock(committed),
                            EvictionBallots(committed), evictedNow, reducedMotion, ceremonyPace,
                            NameOf(committed, committed.hohId), committed.hohId == committed.playerId,
                            evictedNow != null && evictedNow == committed.playerId, screen);
                    else if (kind == CeremonySting.NominationKind && keyCeremony != null)
                        reveal = screen => keyCeremony.Play(committed.week, NameOf(committed, committed.hohId),
                            committed.hohId == committed.playerId,
                            SafeHouseguests(committed), NominatedHouseguests(committed), reducedMotion, ceremonyPace, screen,
                            RosterHouseguests(committed));
                    // The veto meeting is staged in the living room and plays on its screen
                    // (EpisodeDirector.CeremonyStageVeto.cs); unstaged it is the generic card below,
                    // exactly as it was. Its commit still makes the veto's sound, the same for
                    // either outcome: no card of its own makes one.
                    if (kind == CeremonySting.VetoKind && TryStageVetoMeeting(committed, text, wasActive, wasNominated, blockBefore))
                        revealed = vetoStaged = true;
                    else if (reveal != null && TryBeginCeremonyStage(kind, committed,
                            screen => reveal(screen) || PlayGenericCeremonyCard(committed, kind, text, wasActive, wasNominated)))
                        revealed = true;
                    else if (reveal != null)
                    {
                        revealed = reveal(null);
                        // The bodies act the beat out in the house while the card reports it, and the
                        // camera goes to the room the ceremony happens in (Phase 4 presets).
                        if (revealed) { ReactToCeremony(committed, kind, wasActive, wasNominated); FrameCeremony(kind); }
                        else PlayGenericCeremonyCard(committed, kind, text, wasActive, wasNominated);
                    }
                    else
                    {
                        // And the season's last beat gets the jury read one juror at a time: the engine
                        // decides every ballot and the winner in one commit, and the card that named the
                        // winner at once gave the finale away.
                        revealed = ceremony.kind == CeremonySting.WinnerKind
                            && juryReveal != null
                            && juryReveal.Play(JuryFinalists(result.state), JuryVotes(result.state), result.state.winnerId,
                                reducedMotion, ceremonyPace, unchecked((int)result.state.seed));
                        if (!revealed) PlayGenericCeremonyCard(result.state, ceremony.kind, ceremony.text, wasActive, wasNominated);
                        else
                        {
                            ReactToCeremony(result.state, ceremony.kind, wasActive, wasNominated);
                            FrameCeremony(ceremony.kind);
                        }
                    }
                    lastCeremonyKind = ceremony.kind;
                    // The week's recap, once the beats that narrate the eviction have had their say.
                    // It waits rather than opening now because the reveal outlives its own strip by
                    // seconds and the two canvases share a sorting order — a recap that appeared
                    // immediately would cover the tally it is summarising.
                    if (ceremony.kind == CeremonySting.EvictionKind) QueueWeeklyRecap(wasWeek);
                }
                else
                {
                    // Fallout (plan §5.1): a story's ceremony, when the show's own has not taken the screen.
                    var fallout = result.state.events.Skip(knownEvents)
                        .LastOrDefault(entry => StoryFallout.IsFallout(entry.kind)
                            && (entry.audienceIds.Count == 0 || entry.audienceIds.Contains(result.state.playerId)));
                    if (fallout != null) PlayFallout(result.state, fallout, wasActive);
                    // The house down to three opens the finale with a card of its own: the engine logs no
                    // ceremony for it, so it is read from the phase the commit arrived in. A removal that
                    // took the house from four to three keeps the screen instead: its card names who left,
                    // and the finale's shows only the three still in it.
                    else if (wasPhase != EpisodePhase.FinalHoHPart1 && result.state.phase == EpisodePhase.FinalHoHPart1
                        && takeover != null)
                    {
                        EndCeremonyCards();
                        takeover.Play(CeremonyTakeover.FinalThreeKind, result.state.week,
                            CeremonySubjects(result.state, CeremonyTakeover.FinalThreeKind, wasActive, wasNominated), reducedMotion);
                        // The card has the frame to itself: the chrome stands aside until it is down.
                        HoldHudForReveal(redraw: false);
                    }
                    // The final Head of Household's bracket as each part opens, and its crowning as
                    // the final eviction does (EpisodeDirector.FinalHoH.cs; ENDGAME-PLAN F3).
                    else PlayFinalHoHCard(result.state, wasPhase, wasActive);
                }
                if (revealed) HoldHudForReveal();
                if ((!revealed || vetoStaged) && command.kind != EpisodeCommandKind.MarkOpeningBeat && command.kind != EpisodeCommandKind.Introduce)
                    audioBed.PlayCue(commitCue);
            }
            Render(); return result;
        }

        /// <summary>
        /// The generic card for a ceremony: the takeover opens the scene, the strip reports the
        /// result, the bodies act the beat out where they stand and the camera goes to the room.
        /// What every ceremony got before the reveals, and what a reveal that declines its shape -
        /// or a stage that could not gather the house - falls back to. Always true: the beat is
        /// never silent.
        /// </summary>
        private bool PlayGenericCeremonyCard(EpisodeState state, string kind, string text, HashSet<string> wasActive, HashSet<string> wasNominated)
        {
            // The veto meeting's card says which way it went, from the block before this commit and
            // after it; every other card keeps its kind's own line.
            string line = kind == CeremonySting.VetoKind ? VetoMeetingLine(wasNominated, state.nominees) : null;
            if (takeover != null) takeover.Play(kind, state.week, CeremonySubjects(state, kind, wasActive, wasNominated), reducedMotion, null, line);
            // The final Head of Household's choice is an endgame card: the chrome stands aside for it.
            if (takeover != null && kind == CeremonySting.FinalEvictionKind) HoldHudForReveal(redraw: false);
            if (sting != null) sting.Play(kind, text, reducedMotion);
            // Under a stage - a reveal that declined the set's screen - the stage has the camera
            // and the bodies, and the card only reports: framing the room as well put two hands on
            // the camera (PACK8-PASS-PLAN C1).
            if (IsCeremonyStaged) return true;
            ReactToCeremony(state, kind, wasActive, wasNominated);
            FrameCeremony(kind);
            return true;
        }

        /// <summary>
        /// Takes down every ceremony card still on screen, before the next one plays - and, when
        /// <paramref name="includingResult"/>, a competition's standings from an earlier beat. The
        /// standings wait for Continue, and in play they hold the input until they get it; but a
        /// commit that arrives past them (the walkthrough's do) carried the Head of Household's
        /// result through the nominations, the veto draw and the veto itself, every later card
        /// stacked on it. A commit that produced the result it is showing keeps it.
        /// </summary>
        private void EndCeremonyCards(bool includingResult = false)
        {
            if (takeover != null) takeover.Cancel();
            if (voteReveal != null) voteReveal.Cancel();
            if (juryReveal != null) juryReveal.Cancel();
            if (keyCeremony != null) keyCeremony.Cancel();
            CancelVetoDrawReveal();
            if (includingResult && competitionCard != null) competitionCard.Cancel();
        }

        /// <summary>
        /// The houseguest behind the top line of the standings, or null when the field is empty.
        /// The standings already know who won - they are ordered best first and flag the winner -
        /// but they carry a name for the card rather than an id, so the committed scores the same
        /// row was built from are read back for it. Nothing here decides the result a second time.
        /// </summary>
        private static string CompetitionWinnerId(EpisodeState state, List<CompetitionResult.Standing> standings)
        {
            if (state?.competitionScores == null || standings == null || standings.Count == 0) return null;
            var top = standings[0];
            if (!top.IsWinner) return null;
            foreach (var entry in state.competitionScores)
            {
                var actor = state.Find(entry.contestantId);
                if (actor != null && actor.name == top.Name && entry.score == top.Score) return actor.id;
            }
            return null;
        }

        /// <summary>How often the house checks whether the player has walked in on anything.</summary>
        private const float ProximityWatchSeconds = 4f;
        private float proximityWatch;

        /// <summary>Resolves a HUD chrome panel by name, live, for the tour to stand beside.</summary>
        private RectTransform FindChrome(string name) =>
            gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                .FirstOrDefault(rect => rect.name == name && rect.gameObject.activeInHierarchy);

        /// <summary>The notebook's addressable sections, shared with the icon rail.</summary>
        public static class NotebookSection
        {
            public const string People = "Section · people";
            public const string Network = "Section · network";
            public const string Rooms = "Section · rooms";
            public const string Votes = "Section · votes";
            public const string Story = "Section · story";
            /// <summary>The notebook's own page: what you have on each houseguest (EpisodeDirector.Notes.cs). Not a rail row; "Notebook [J]" opens it.</summary>
            public const string Notes = "Section · notes";
            /// <summary>Your pacts and the ones you know of (EpisodeDirector.Alliances.cs). Not a rail row; the relationship web's door opens it.</summary>
            public const string Alliances = "Section · alliances";
            /// <summary>Your word: every commitment you are a party to (EpisodeDirector.YourWord.cs). Not a rail row; the notes page opens it.</summary>
            public const string Word = "Section · word";
        }

        /// <summary>
        /// Which rail item the player is on, or null when they are on none of them.
        ///
        /// <para>The rail fills the active one, and it cannot know that on its own: the overview is
        /// a camera mode the director owns and the sections are a field it owns. Exposing the answer
        /// is cheaper than the rail keeping a second copy of the same two facts.</para>
        /// </summary>
        // The page on screen first: the notebook and the overview no longer stand together, but if
        // they ever did the rail must light what the player is reading.
        public string ActiveSection => journalOpen ? journalSection
            : IsOverview ? OverviewSection : null;

        /// <summary>Whether the week's recap is on screen. Read by the HUD's own open-panel test.</summary>
        public bool IsWeeklyRecapOpen => weeklyRecap != null && weeklyRecap.IsOpen;

        private void Commit(EpisodeState origin, EpisodeCommandKind kind, string target = null, string second = null, bool veto = false, double performance = 0, string text = null)
        {
            if (!IsCurrentDiaryRevision(origin)) return;
            Submit(new EpisodeCommand { id = Guid.NewGuid().ToString("N"), actorId = origin.playerId, expectedPhase = origin.phase,
                expectedRevision = origin.revision, kind = kind, targetId = target, secondTargetId = second, useVeto = veto, performance = performance, text = text });
        }

        public void ContinueEpisode() { if (phaseOpen) Commit(projected, EpisodeCommandKind.Advance); }

        public void SkipQuestioning() { if (phaseOpen) Commit(projected, EpisodeCommandKind.SkipQuestioning); }
        public void SubmitSpeech(string text) { if (phaseOpen) Commit(projected, EpisodeCommandKind.SubmitSpeech, text: text); }
        /// <summary>
        /// Commits a nominee's speech from the block. Reachable from the episode screen and from
        /// the diary room, because the block speech is offered in both.
        /// </summary>
        public void SubmitEvictionSpeech(string text)
        {
            if (phaseOpen || diaryOpen) Commit(projected, EpisodeCommandKind.SubmitEvictionSpeech, text: text);
        }
        public void AnswerJury(string choice)
        {
            if (!phaseOpen || projected.phase != EpisodePhase.JuryQuestioning) return;
            var exchange = projected.juryExchanges[projected.juryQuestionIndex];
            Commit(projected, EpisodeCommandKind.AnswerJury,
                exchange.finalistId == projected.playerId ? exchange.questionerId : exchange.finalistId, choice);
        }

        private void Project()
        {
            var state = engine.Snapshot;
            projected = state;
            playerIsActive = state.Find(state.playerId).status == ContestantStatus.Active;
            promptNpc = null; npcPrompt = null;
            // Finale night brings the jury back into the living room; their places are chosen once.
            BenchTheJury(state);
            var npcStates = state.contestants.Where(c => !c.isPlayer).ToArray();
            for (int i = 0; i < housemates.Length; i++)
            {
                var npc = housemates[i];
                // Imported IDs are rebound by saved slot, never guessed from display names.
                var model = npcStates[i];
                npc.Configure(model.id, model.name);
                npc.gameObject.SetActive(KeepsBody(state, model));
                // The phase's clothes - or, for the player's company in the hot tub, swimwear, kept
                // through a render and changed back behind the body when they get out.
                DressHousemate(model.id);
                var label = npc.GetComponentInChildren<TextMesh>(); if (label != null) label.text = model.name;
            }
            DressPlayer(state);
            player.SetInputEnabled(!IsPanelOpen && state.Find(state.playerId).status == ContestantStatus.Active);
            cameraRig.ControlsEnabled = !IsPanelOpen;
            ReconcileNpcSocialWorld();
            // After the house's world has let the jurors go: nobody it routes is ever placed.
            StandTheJury(state);
        }

        /// <summary>
        /// Whether a houseguest's body is in the house: everyone still playing, the winner and the
        /// runner-up, the jury on finale night, and the evicted while a card narrates their eviction
        /// and while they walk out. <see cref="Project"/> switches every other body off.
        /// </summary>
        public static bool KeepsBody(ContestantStatus status, bool departing, bool walkingOut, bool onJuryBench) =>
            status == ContestantStatus.Active || status == ContestantStatus.Winner || status == ContestantStatus.RunnerUp
            || departing || walkingOut || onJuryBench;

        /// <summary>
        /// Whether the house's world routes a houseguest: everyone still playing, whoever is walking
        /// out, and whoever a staged ceremony holds a place for - the evicted in the hot seat - but
        /// only ever a body <see cref="KeepsBody(ContestantStatus, bool, bool, bool)"/> keeps. Both
        /// read the same terms, so a body switched off is never one the world still counts on. A
        /// stage that held the evicted past the end of their walk out once did exactly that: the
        /// walk out switched the body off, the coordinator failed the whole house with "An eligible
        /// NPC root is inactive.", and every ceremony after the first staged eviction played on the
        /// HUD. An unstaged eviction still lets the evicted go at the commit: the card narrates them
        /// in the room, but nothing routes them until they walk out.
        /// </summary>
        public static bool RoutedByTheHouse(ContestantStatus status, bool departing, bool walkingOut, bool onJuryBench, bool stageHolds) =>
            KeepsBody(status, departing, walkingOut, onJuryBench) && (status == ContestantStatus.Active || walkingOut || stageHolds);

        private bool KeepsBody(EpisodeState state, ContestantState model) =>
            KeepsBody(model.status, model.id == departingId, model.id == walkingOutId, OnJuryBench(state, model));

        private bool RoutedByTheHouse(EpisodeState state, ContestantState model) =>
            RoutedByTheHouse(model.status, model.id == departingId, model.id == walkingOutId, OnJuryBench(state, model),
                CeremonyStageHolds(model.id));


        /// <summary>The season as it has been lived: weeks, mood, promises, oaths, memories.</summary>
        private void RenderNotebookStory(EpisodeState state)
        {
            // Every week that has closed, reachable again. The recap opens itself once when a
            // week ends and is then gone; the notebook is where the player already comes to
            // check what happened, so it is where the record of a finished week belongs.
            var played = WeeklyRecap.Season(state).Where(w => w.evicted != null).ToList();
            if (played.Count > 0)
            {
                hud.Heading("WEEKS SO FAR");
                foreach (var week in played)
                {
                    int number = week.week;
                    hud.Paragraph(week.Headline);
                    hud.Action(EpisodeHud.ReviewWeekCaption(number), () => ReviewWeek(number));
                }
            }
            hud.Paragraph("Your mood: " + state.Find(state.playerId).mood + " · Stress: " + state.Find(state.playerId).stressLevel);
            // Aggregate source arcs have no participant/knowledge provenance.
            // NPC-only conversations must not masquerade as the player's bonds.
            foreach (var promise in state.promises.Where(p => p.fromId == state.playerId || p.toId == state.playerId))
            {
                // Was the raw enum on both ends: "AllianceLoyalty - Dana -> You - Active". The
                // vocabulary the player was given when they made the promise already exists.
                bool mine = promise.fromId == state.playerId;
                string who = mine ? state.Find(promise.toId).name : state.Find(promise.fromId).name;
                hud.Paragraph((mine ? "You promised " + who : who + " promised you")
                    + " " + Presentation.RelationshipWeb.PromiseWord(promise.kind)
                    + "  ·  " + PromiseStanding(promise.status));
            }
            foreach (var alliance in state.alliances.Where(a => a.members.Contains(state.playerId))) hud.Paragraph(alliance.name + (alliance.active ? " · active" : " · ended"));
            hud.Heading("YOUR LOYALTY DECLARATIONS");
            // Said as the conversation says it (X12): no promise from them, and binding both ways.
            foreach (var oath in state.loyaltyOaths.Where(oath => oath.playerId == state.playerId || oath.targetId == state.playerId))
                hud.Paragraph("Week " + oath.week + ": " + (oath.playerId == state.playerId
                    ? "You declared loyalty to " + state.Find(oath.targetId).name
                    : state.Find(oath.playerId).name + " declared loyalty to you") + ". " + OathNotebookNote);
            RenderDiaryRecord(state);
            // The player's memories as they may know them: one that tells a ballot the reveal kept
            // private waits for the ballot (KnownBallots.PlayerMemories; decision 4).
            foreach (var memory in KnownBallots.PlayerMemories(state)) hud.Paragraph("Week " + memory.week + ": " + memory.text);
            RenderStorySoFar(state);
        }

        private static Color Palette(int i)
        {
            var colors = new[] { new Color(.2f,.55f,.65f), new Color(.85f,.34f,.26f), new Color(.6f,.4f,.7f), new Color(.88f,.68f,.26f), new Color(.3f,.48f,.7f) };
            return colors[i % colors.Length];
        }

        private void Render()
        {
            if (hud == null || engine == null) return;
            // Which screen is up decides what the music does, and a render is exactly the moment
            // that changed. SetMusic ignores a state it is already in, so this costs nothing.
            ApplyMusic();
            var state = projected ?? engine.Snapshot;
            // Repainted from committed state on every render rather than on the eviction event, so a
            // wall restored from a save shows the same thing as one that watched the vote.
            if (memoryWall == null)
                memoryWall = gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MemoryWall>(true))
                    .FirstOrDefault();
            // The committed snapshot, not the projection: a projected eviction is not a fact
            // yet, and the set must never show an outcome the save does not hold.
            if (memoryWall != null) memoryWall.Refresh(engine.Snapshot);
            // While a competition is being played the strip is its entrant strip (mockup-05): who
            // is in the engine's field, and who is sitting it out.
            CastRail.CompetitionField = challengeRun != null && competitionScreen != null && competitionScreen.IsShowing
                ? new HashSet<string>(EpisodeEngine.CompetitionPlayers(state).Select(actor => actor.id)) : null;
            // And the player's own chip carries their progress as it happens: theirs is the one
            // score that exists before the result commits.
            var run = challengeRun;
            CastRail.PlayerProgress = CastRail.CompetitionField != null && run != null ? () => ProgressWord(run) : (System.Func<string>)null;
            // The jury house the strip opened goes the moment the house can no longer open it from
            // there - a load, the vote begun - and takes the pause it put on the house with it.
            if (juryHouseOverHouse && !JuryStripDoorAvailable(state)) ClosePanelsInternal(false);
            hud.Begin(state, message, blockedRecovery, phaseOpen || focusedNpc != null || settingsOpen || journalOpen || diaryOpen || houseActivitiesOpen || sceneCardOpen || juryHouseOverHouse);
            // The Pull's card and the Nearby card are rebuilt hidden with the rest of the chrome, and a
            // render that Update orders (a body finishing assembly) comes after this frame's ticks: put
            // them back now, or they are gone for the rest of the frame and a press on one lands on
            // nothing. The Pull first, as in Update: it outranks the Nearby card for the week card's place.
            TickStoryPull();
            TickNearby();
            // Committed state, not the projection: a projected eviction is not a fact, and telling
            // someone they are out of the game is the last claim that should run ahead of the save.
            if (Spectating(engine.Snapshot)) hud.SpectatorNote(SpectatorDetail(engine.Snapshot));
            if (settingsOpen || blockedRecovery) { Settings(state); return; }
            if (diaryOpen) { RenderDiary(state); return; }
            if (houseActivitiesOpen) { RenderHouseActivities(); return; }
            if (JuryHouseOverHouseIfOpen(state)) return;
            if (journalOpen)
            {
                // Every page of the notebook takes the notebook's own frame, not only the web: the
                // others were rendered into the docked panel, whose viewport is about 174 units
                // tall - the room map showed its header and nothing of the map.
                hud.SetActivityLayout(EpisodeHud.ActivityLayout.Relationships);
                // The notebook is a place to read, not a beat of the week: a slim head of its own
                // in place of the phase band, so each page's title - the web's RELATIONSHIP WEB
                // above all (mockup-07) - is the first thing at the top of the frame.
                // The redesigned pages (Refinement Kit 6) carry their own title in the head and the
                // house activities in their foot; the others keep the notebook's title and the
                // command at their top, where the web's layout and the activities tests expect it.
                bool kitPage = journalSection == NotebookSection.Rooms || journalSection == NotebookSection.People
                    || journalSection == NotebookSection.Votes || journalSection == NotebookSection.Notes
                    || journalSection == NotebookSection.Alliances
                    || journalSection == NotebookSection.Word;
                if (kitPage)
                {
                    var head = NotebookPageHead(journalSection);
                    hud.ScreenHeader(EpisodeHud.NotebookHeaderName, EpisodeHud.NotebookEyebrowCopy, head.Title, null, head.Subtitle);
                }
                else
                {
                    hud.ScreenHeader(EpisodeHud.NotebookHeaderName, EpisodeHud.NotebookEyebrowCopy, null, null);
                    hud.PanelTitle("YOUR NOTEBOOK", "Private information is limited to what your character knows.");
                    // A command rather than a section, so it stays put whichever page you are on.
                    hud.Action("House activities",OpenHouseActivities);
                }
                // ONE section at a time. The rail's four buttons were four scroll positions in a
                // single document: every render emitted the relationship web, the house map, every
                // houseguest, every vote, every finished week, mood, promises, alliances, oaths, the
                // diary record, every memory and the whole story into one 900x300 panel whose
                // viewport is about 174 units tall. Four buttons, one view, and the only difference
                // between them a scroll nudge that silently does nothing when its section was not
                // emitted - which is why they all looked identical.
                if (journalSection == NotebookSection.Rooms)
                {
                    RenderNotebookRooms(state);
                }
                else if (journalSection == NotebookSection.Votes)
                {
                    RenderNotebookVotes(state);
                }
                else if (journalSection == NotebookSection.Story)
                {
                    RenderNotebookStory(state);
                }
                else if (journalSection == NotebookSection.People)
                {
                    RenderNotebookPeople(state);
                }
                else if (journalSection == NotebookSection.Notes)
                {
                    RenderNotebookNotes(state);
                }
                else if (journalSection == NotebookSection.Alliances)
                {
                    RenderNotebookAlliances(state);
                }
                else if (journalSection == NotebookSection.Word)
                {
                    RenderNotebookWord(state);
                }
                else
                {
                    // Just the graph. The roster used to be printed underneath it - every
                    // houseguest, their status and a trust number, then a second line of biography
                    // each - which made the one page the rail opens by default the longest page in
                    // the notebook, in a viewport about 174 units tall. It is its own page now, and
                    // this one answers the question its own icon asks.
                    //
                    // The graph carries the caveat in its own legend, so repeating it here would be
                    // the same sentence twice within one screen.
                    hud.SocialGraphPanel(state);
                    hud.Mark(NotebookSection.Network);
                    // Mockup-07's bar: the player, and where they stand by their own reading.
                    int allies = RelationshipWeb.Allies(state).Count, rivals = RelationshipWeb.Rivals(state).Count;
                    int known = KnownBallots.PlayerMemories(state).Count();
                    hud.SpeechBar(state.playerId, EpisodeHud.SelfTitle(state.Find(state.playerId)),
                        "By your own reading: " + allies + (allies == 1 ? " ally, " : " allies, ") + rivals
                        + (rivals == 1 ? " rival, " : " rivals, ") + known + (known == 1 ? " thing" : " things") + " you know.", true);
                }
                hud.ApplyPendingScroll();
                return;
            }
            if (focusedNpc != null)
            {
                var npc = state.Find(focusedNpc.Id);
                // The two-shot keeps its usual place unless the conversation's own screen says where
                // it has left room for the pair (below, once the stage is laid out).
                if (cameraRig != null) cameraRig.ConversationWindowOffset = 0f;
                // Outside free time the house cannot talk, so there is nothing to choose: a card
                // sized to the one thing it says (Refinement Kit 6), not the drawer cut short. Mood
                // and your trust are two pills - the drawer's "Neutral -9" was a band and a number
                // that read as a mood - and the week's budget is not on it, because nothing here
                // spends it. The exception is a strategy window: whoever is deciding has time.
                bool window = state.phase != EpisodePhase.Social && state.phase != EpisodePhase.Campaign;
                // Under the week's windows every houseguest has time for a word wherever a window is open;
                // the notice is for the phases with no seats, and for the strategy windows before them.
                if (window && !StrategyRules.IsDecider(state, npc.id) && !(EpisodeEngine.WeekRulesOn(state) && EpisodeEngine.Window(state) != Windows.None))
                {
                    double trust = state.Score(state.playerId, npc.id);
                    var identity = new List<string>();
                    if (!string.IsNullOrEmpty(npc.pronouns)) identity.Add(npc.pronouns);
                    if (npc.traits != null && npc.traits.Count > 0) identity.Add(string.Join(" / ", npc.traits));
                    hud.ConversationNotice(npc, string.Join(" · ", identity),
                        MoodLine(state, npc),
                        "Your trust: " + TrustFigure(trust), TrustTint(trust),
                        HouseDialogue.Greeting(state, npc.id),
                        StrategyRules.WindowOpen(state) ? StrategyRules.WindowRefusal(state, npc.id, EpisodeCommandKind.Talk) : ConversationUnavailableLine);
                    return;
                }
                // Where you stand with them, and what you have left to spend on them, in the header
                // of the panel that spends it.
                //
                // Neither number was reachable from here. RelationshipWeb has had a five-band
                // vocabulary since it was written - Allied, Friendly, Neutral, Wary, Hostile, on
                // published thresholds and locked by its own tests - and StandingWord had exactly
                // one call site in the whole game, a stat tile on the notebook's network page. So
                // the panel where the player decides how to treat somebody was the one place that
                // never said how they were being treated. The remaining-actions chip sits in the
                // objective card, which SetActivityLayout hides for a conversation: it was on
                // screen right up until the moment it mattered.
                // A story beat this conversation raised is answered in it (plan §5.1): while one is
                // open with them it is the conversation, and the dial comes back once it is answered.
                if (ConversationBeat(state, npc.id)) return;
                hud.SpeakerTitle(npc.id, npc.name.ToUpperInvariant(),
                    npc.pronouns + " · " + string.Join(" / ", npc.traits));
                if (cameraRig != null) cameraRig.ConversationWindowOffset = hud.ConversationWindowOffset;
                // On its own line, not appended to the identity one. Four facts in a fixed-width
                // subtitle fitted at the standard text size and was cut off at the larger one -
                // which is the whole reason this panel now has a clipped-copy guard, and the guard
                // caught it on the first run. Who somebody is and where you stand with them are
                // two different questions anyway.
                // And their mood, when it is not the everyday one, with whom it is about.
                string mood = npc.mood != "Neutral" ? MoodLine(state, npc) : null;
                hud.Paragraph(Standing(state, npc.id) + (mood != null ? "  ·  " + mood.Substring("Mood: ".Length) : "")
                    + "  ·  " + ActionsLeft(state) + " left");
                hud.NpcDialogue(state, npc.id, lastSocialAction, standingLineBefore);
                if (lastSocialAction.HasValue) hud.OutcomeChips(lastSocialDelta);
                // What the player came for, first (their screen's "Ask for information" or "Pitch a
                // deal"): the rows it names are drawn here and not again below.
                bool cameToAsk = conversationIntent == IntentAsk, cameToDeal = conversationIntent == IntentDeal;
                if (cameToAsk)
                {
                    hud.Heading("WHAT YOU CAME TO ASK");
                    AskRows(state, npc, window);
                }
                else if (cameToDeal)
                {
                    hud.Heading("WHAT YOU CAME TO PUT TO THEM");
                    DealRows(state, npc, allied: state.Allied(state.playerId, npc.id));
                    FoldedDealPanel(state, npc);
                }
                // The declaration binds the two of them, whoever made it (X12): the copy says what the
                // engine does (EpisodeDirector.ConversationGroups.cs, OathOfferLine).
                if (state.oathOpportunities.Contains(npc.id))
                {
                    hud.Heading("A PERSONAL LOYALTY DECLARATION");
                    hud.Paragraph(OathOfferLine(npc.name));
                    hud.Tag(hud.Action(EpisodeHud.OathDeclareCaption, () => Commit(state, EpisodeCommandKind.SwearLoyalty, npc.id)),
                        Category(EpisodeCommandKind.SwearLoyalty));
                    hud.Tag(hud.Action(EpisodeHud.OathDeclineCaption, () => Commit(state, EpisodeCommandKind.DeclineLoyalty, npc.id)),
                        Category(EpisodeCommandKind.DeclineLoyalty));
                }
                else if (state.loyaltyOaths.Any(oath => oath.playerId == state.playerId && oath.targetId == npc.id))
                    hud.Paragraph(OathRecordedLine(npc.name));
                if (window || StrategyRules.CanBeAskedForTheirVote(state, npc.id)) LobbyPanel(state, npc);
                // The category is a chip pinned to the button, never part of its caption. Baking it
                // into the label broke every test that finds a control by the words on it — and the
                // web build draws it as a separate pill anyway, so the caption was the wrong place.
                bool allied = state.Allied(state.playerId, npc.id);
                // The dial (mockup-06, mockup-12): the speaker's face at the hub, and seven petals
                // around it in the mockups' order and colours - chat, strategize, flirt, more,
                // reassure, gossip, joke, clockwise from the top. The captions on them are the
                // build's own, unshortened, because that is what a test and a screen reader identify
                // a control by; the mockup's single words survive as the glyph and the tint.
                //
                // The petals are the six openings a conversation actually has here. Everything else
                // - the promises, the alliance, the rumours, the deals - is beneath the dial in the
                // group it belongs to, and the seventh petal moves the keyboard to the first row.
                hud.ConversationRadial(npc.id, 7);
                hud.Tag(hud.Petal(EpisodeHud.SmallTalkCaption, "chat", UiTheme.Accent,
                        () => Commit(state, EpisodeCommandKind.SmallTalk, npc.id)),
                    Category(EpisodeCommandKind.SmallTalk), EpisodeHud.TagSeat.CardFoot);
                hud.Tag(hud.Petal(EpisodeHud.StrategicDiscussionCaption, "bulb", UiTheme.Strategic,
                        () => Commit(state, EpisodeCommandKind.StrategicDiscussion, npc.id)),
                    Category(EpisodeCommandKind.StrategicDiscussion), EpisodeHud.TagSeat.CardFoot);
                hud.Tag(hud.Petal(EpisodeHud.PersonalChatCaption, "heart", UiTheme.Flirt,
                        () => Commit(state, EpisodeCommandKind.PersonalChat, npc.id)),
                    Category(EpisodeCommandKind.PersonalChat), EpisodeHud.TagSeat.CardFoot);
                hud.Petal(EpisodeHud.MorePetalCaption, "journal", UiTheme.Muted, hud.RevealBeyondRadial);
                hud.Tag(hud.Petal(EpisodeHud.RelationshipBuildingCaption, "handshake", UiTheme.Allied,
                        () => Commit(state, EpisodeCommandKind.RelationshipBuilding, npc.id)),
                    Category(EpisodeCommandKind.RelationshipBuilding), EpisodeHud.TagSeat.CardFoot);
                hud.Tag(hud.Petal(EpisodeHud.ShareSecretCaption, "gossip", UiTheme.Strategic,
                        () => Commit(state, EpisodeCommandKind.ShareSecret, npc.id)),
                    Category(EpisodeCommandKind.ShareSecret), EpisodeHud.TagSeat.CardFoot);
                hud.Tag(hud.Petal("Spend time together", "star", UiTheme.Joke,
                        () => Commit(state, EpisodeCommandKind.Talk, npc.id)),
                    Category(EpisodeCommandKind.Talk), EpisodeHud.TagSeat.CardFoot);
                // Everything else, under the dial in four groups by what it is for - bond, learn,
                // scheme, bargain - with each verb aimed at a third houseguest one row that opens its
                // people (EpisodeDirector.ConversationGroups.cs).
                ConversationGroups(state, npc, window, allied, cameToAsk, cameToDeal);
                return;
            }
            if (sceneCardOpen) { SceneCard(state); return; }
            if (!phaseOpen) return;
            // The episode screen is a decision screen: it takes the stage, the frame from the rail
            // to the right edge. A quiet beat - "Continue episode" under the house's status, or the
            // reflection prompt - stays a card sized to its few lines. A dedicated layout (the
            // briefing, the nominations, a house event) sizes itself instead.
            // Except the week's ceremonies, which are screens even with nothing to decide
            // (EpisodeDirector.CeremonyScreen). The week's four strategy screens take the taller
            // strategy stage (PACK8-PASS-PLAN A3).
            if (QuietBeat(state) && !CeremonyScreenBeat(state)) hud.FitPanelToContent();
            else if (StrategyScreenBeat(state))
            {
                hud.StrategyStage(StrategyShell(state));
                // The veto's draw is laid out for the frame's whole width (EpisodeDirector.VetoDraw.cs).
                if (VetoDrawBeat(state)) hud.StrategyWholeWidth();
            }
            // Free time is a board on the strategy stage too (EpisodeDirector.FreeTimeBoard.cs), on
            // the panel's own glass: Pack 8 has no free-time shell.
            else if (FreeTimeBoardBeat(state)) hud.StrategyStage(null);
            // And so is the finale page, in the campaign's neutral shell: Pack 9 brings cards, not a
            // shell of its own (EpisodeDirector.FinalePage.cs).
            else if (FinalePageBeat(state)) hud.StrategyStage(PackArt.Pack8CampaignShell);
            else hud.SetActivityLayout(EpisodeHud.ActivityLayout.Stage);
            // The phase and week now live in the panel's fixed header band, which stays on screen
            // while this content scrolls. Repeating them as the first line of the scroll was the
            // same sentence twice, six lines apart.
            if (state.pendingDiary != null)
            {
                hud.Heading("A PRIVATE REFLECTION IS READY");
                hud.Paragraph("Visit the diary room to answer, or explicitly skip this reflection before beginning another competition.");
                hud.Action(EpisodeHud.DiaryVisitReflectionCaption, GoToDiary).interactable = HasDiaryRoom && playerIsActive;
                hud.Action(EpisodeHud.DiarySkipReflectionCaption, SkipDiary);
                return;
            }
            if (challengeActive) { ChallengePanel(); return; }
            // The jury house over the Final 2's panel when it is open, and its door after the
            // panel's own controls (EpisodeDirector.JuryHouse.cs; ENDGAME-PLAN F4).
            // The final case's door and the jury house's, after the panel's own controls (ENDGAME-PLAN F4):
            // during the questioning, in its thin row of ways on after Skip (MOCKUP-PASS M11).
            if (state.phase == EpisodePhase.JuryQuestioning)
            { if (FinalCaseIfOpen(state) || JuryHouseIfOpen(state)) return; hud.JuryQuestioning(state, () => { FinalCaseDoor(state); JuryHouseDoor(state); }); return; }
            if (state.phase == EpisodePhase.FinalSpeeches)
            { if (FinalCaseIfOpen(state) || JuryHouseIfOpen(state)) return; hud.FinalSpeech(state); FinalCaseDoor(state); JuryHouseDoor(state); return; }
            // The season over: the finale page on the strategy stage, or the jury's questions as its
            // step (EpisodeDirector.FinalePage.cs; UI-UX-PASS-PLAN F0).
            if (state.phase == EpisodePhase.Finished) { FinalePage(state); return; }
            if (EpisodeEngine.IsCompetition(state.phase))
            {
                if (!state.competitionResolved)
                {
                    if (EpisodeEngine.CompetitionPlayers(state).Any(c => c.isPlayer))
                    {
                        CompetitionBriefing(state);
                    }
                    else SpectatorBriefing(state);
                }
                else
                {
                    hud.Action("Review competition results", () => ReviewCompetitionResult(state));
                    // Ranked as the engine ranks them, as rows: a face, a name, the winner's badge -
                    // the crown, or the veto's medal - and no score (UI-UX-PASS-PLAN C0). The rows
                    // are the result card's own standings, so the two never disagree.
                    hud.Section("FINAL STANDINGS");
                    hud.CompetitionStandings(CompetitionStandings(state), WinnersMark(state.phase));
                    // The way on, pinned: it used to sit under the standings, well past the fold.
                    AdvanceWarning(state);
                    hud.PinnedAction("Continue to the next ceremony", () => Commit(state, EpisodeCommandKind.Advance));
                }
                return;
            }
            // The nomination is a screen of steps on the strategy stage - its status cards, its
            // tracker, one step's body and a footer - in place of everything below (PACK8-PASS-PLAN
            // B1, EpisodeDirector.NominationScreen.cs).
            if (NominationScreen(state)) return;
            // Free time is a board of its own - what is waiting on the player beside the budget, the
            // story in a line, the house as cards and the other ways to spend the time - with its own
            // footer, in place of everything below (EpisodeDirector.FreeTimeBoard.cs).
            if (FreeTimeBoard(state)) return;
            // Who holds what this week, on one line, before whatever there is to decide: the stage
            // stands the strip and its badges down, so this is where the roles are read. The final
            // Head of Household's choice opens on its own gold head instead: the house is down to
            // the three, and the two it is between are on its cards (MOCKUP-PASS M8). Not over a
            // view at three either (the comparison, the final case, the jury house): those are
            // screens of their own, and the roles they would sit under are the final-four week's,
            // which stay in state until the window closes and can name a juror as a nominee.
            bool finalChoice = state.phase == EpisodePhase.FinalEviction && state.hohId == state.playerId;
            if (finalChoice) FinalTwoHead();
            // The veto meeting says it in a strip across the stage's header (PACK8-PASS-PLAN B3).
            else if (!VetoMeetingStatus(state))
            {
                // Not over the campaign, whose situation card says each of these on a row of its own.
                string houseStatus = ViewOverPreparation(state) || state.phase == EpisodePhase.Campaign ? null : HouseStatus(state);
                if (houseStatus != null) hud.Paragraph(houseStatus);
            }
            // A story beat waiting on the player comes before anything else they could do: it
            // closes with the week's next beat, and a card buried under the ordinary controls is a
            // card the player never sees. It never blocks the decision under it.
            PendingStoryBeats(state);
            if (RenderPlayerDecision(state, false)) return;
            CeremonyScreen(state);
            if (finalChoice)
            {
                hud.Paragraph("You won the final HoH. Choose who to evict; the other housemate joins you in the final two.");
                // The two of them side by side as cards, with what the player knows about each and
                // what taking each means, over the controls that decide it (ENDGAME-PLAN F2).
                FinalTwoChoice(state);
                return;
            }
            // The player on the jury votes on a screen of their own (EpisodeDirector.JurorVote.cs;
            // ENDGAME-PLAN F6). A player production removed has no vote: the ballot it used to draw
            // for them could not be pressed and left no way on, so they watch and continue.
            if (JurorVotes(state)) { JurorBallot(state); return; }
            if (state.phase == EpisodePhase.Jury && state.Find(state.playerId)?.status == ContestantStatus.Expelled)
                hud.Paragraph(RemovedAtTheVoteLine);
            if (state.phase == EpisodePhase.Campaign) CampaignScreen(state);
            // Free time as a screen: the house as cards and the moves as tiles (EpisodeDirector.FreeTimeScreen.cs).
            else if (state.phase == EpisodePhase.Social) FreeTimeScreen(state);
            if (state.phase == EpisodePhase.Jury) hud.Paragraph(JuryLine(state));
            string pointer = WindowLine(state);
            if (pointer != null) hud.Paragraph(pointer);
            string advance = state.phase == EpisodePhase.Social ? "Begin the next competition"
                : state.phase == EpisodePhase.Campaign ? "Close campaigning and open voting" : "Continue episode";
            AdvanceWarning(state);
            MovingOnCosts(state);
            // Pinned under the scroll, where it is always seen - except under a house event, whose
            // choices keep the panel and the priority; the way on stays inline after them there.
            if (hud.CurrentActivityLayout == EpisodeHud.ActivityLayout.Standard || hud.CurrentActivityLayout == EpisodeHud.ActivityLayout.Stage
                || hud.CurrentActivityLayout == EpisodeHud.ActivityLayout.Strategy)
            {
                var wayOn = hud.PinnedAction(advance, () => Commit(state, EpisodeCommandKind.Advance));
                // The veto's draw wears the draw it makes, beside the caption (EpisodeDirector.VetoDraw.cs).
                if (VetoDrawBeat(state)) VetoDrawWayOn(state, wayOn);
                // What free time's way on costs, under it: the actions that go unused.
                string unused = UnusedActionsNote(state);
                if (unused != null) hud.PinnedNote(unused);
            }
            else hud.Action(advance, () => Commit(state, EpisodeCommandKind.Advance));
            // The veto meeting's row of cards, in the height the step leaves it, and its words on the way on.
            VetoMeetingWayOn(state);
        }

        /// <summary>What a Have-Not player reads beside their interactions: what it costs, and until when.</summary>
        public const string HaveNotLine = "You are a Have-Not until the next Head of Household: slop, cold showers, "
            + "one fewer conversation, and a point off your score in the veto.";

        /// <summary>Whether there is a season on screen worth going back to from the menu.</summary>
        public bool SeasonInProgress => engine != null && !blockedRecovery;

        private void OnDisable()
        {
            if (!IsReady) return;
            // Sibling scene roots may already be destroyed during unloading. Never rebuild UI here.
            ClosePanelsInternal(false); if (hud != null) hud.SetVisible(false);
            if (sting != null) sting.Cancel();
            if (takeover != null) takeover.Cancel();
            if (voteReveal != null) voteReveal.Cancel();
            if (juryReveal != null) juryReveal.Cancel();
            if (competitionCard != null) competitionCard.Cancel();
            if (keyCeremony != null) keyCeremony.Cancel();
            CancelVetoDrawReveal();
            EndCeremonyStage();
            DisposeNpcSocialWorld();
        }
        private void OnEnable()
        {
            if (!IsReady) return;
            hud?.SetVisible(true); ClosePanels();
        }

        // The sting is a scene root rather than a child, so it has to be taken down explicitly.
        /// <summary>The player's progress in the game being played, in the game's own measure.</summary>
        private static string ProgressWord(MiniGameRun run)
        {
            switch (run.Kind)
            {
                case CompetitionMiniGames.Kind.Memory: return run.MatchedPairs + " / " + run.Pairs + " pairs";
                case CompetitionMiniGames.Kind.Reaction: return run.Hits + (run.Hits == 1 ? " hit" : " hits");
                case CompetitionMiniGames.Kind.Endurance: return run.Held.ToString("0.0") + "s held";
                default: return "Score " + run.Score.ToString("0");
            }
        }

        private void OnDestroy()
        {
            CastRail.CompetitionField = null;
            CastRail.PlayerProgress = null;
            EndDiaryVisit(true);
            DisposeNpcSocialWorld();
            if (hud != null) Destroy(hud);
            if (sting != null) { Destroy(sting.gameObject); sting = null; }
            if (takeover != null) { Destroy(takeover.gameObject); takeover = null; }
            if (voteReveal != null) { Destroy(voteReveal.gameObject); voteReveal = null; }
            if (juryReveal != null) { Destroy(juryReveal.gameObject); juryReveal = null; }
            if (competitionCard != null)
            { competitionCard.VisibilityChanged -= SyncCompetitionResultInput; Destroy(competitionCard.gameObject); competitionCard = null; }
            if (competitionScreen != null) { Destroy(competitionScreen.gameObject); competitionScreen = null; }
            if (keyCeremony != null) { Destroy(keyCeremony.gameObject); keyCeremony = null; }
            DestroyVetoDrawReveal();
            // The skip chip is a scene root as well: left behind, the next director attached a second.
            if (skipChip != null) { Destroy(skipChip.gameObject); skipChip = null; }
            if (tutorial != null) { Destroy(tutorial.gameObject); tutorial = null; }
            if (opening != null) { Destroy(opening.gameObject); opening = null; }
        }

        /// <summary>
        /// A phase's title as the panel's header prints it. The final Head of Household is one
        /// title over its three parts, which the header's line under it tells apart; the finale's
        /// two jury beats say they are the finale's (MOCKUP-PASS M3).
        /// </summary>
        public static string PhaseTitle(EpisodePhase phase)
        {
            switch (phase)
            {
                case EpisodePhase.Social: return "FREE TIME";
                case EpisodePhase.HoH: return "HEAD OF HOUSEHOLD";
                case EpisodePhase.VetoSelection: return "VETO PLAYER SELECTION";
                case EpisodePhase.VetoMeeting: return "VETO CEREMONY";
                case EpisodePhase.FinalHoHPart1:
                case EpisodePhase.FinalHoHPart2:
                case EpisodePhase.FinalHoHPart3: return "FINAL HEAD OF HOUSEHOLD";
                case EpisodePhase.FinalEviction: return "CHOOSE YOUR FINAL TWO";
                case EpisodePhase.JuryQuestioning: return "FINALE · FACE THE JURY";
                case EpisodePhase.FinalSpeeches: return "MAKE YOUR FINAL CASE";
                case EpisodePhase.Jury: return "FINALE · JURY VOTE";
                case EpisodePhase.Finished: return "SEASON FINALE";
                default: return phase.ToString().ToUpperInvariant();
            }
        }

        /// <summary>
        /// The title for where the season stands, which the phase alone cannot always say: the final
        /// eviction is the player's to choose only when they hold the house, and otherwise it is
        /// the final eviction they watch.
        /// </summary>
        public static string PhaseTitle(EpisodeState state)
        {
            if (state == null) return string.Empty;
            if (state.phase == EpisodePhase.FinalEviction && state.hohId != state.playerId) return "THE FINAL EVICTION";
            return PhaseTitle(state.phase);
        }
    }
}
