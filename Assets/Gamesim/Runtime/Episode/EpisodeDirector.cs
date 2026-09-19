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
        public EpisodeState Snapshot => engine?.Snapshot;
        public bool IsReady { get; private set; }
        // The weekly recap counts: it is a full-screen scrim, and a player who can still walk
        // the house behind it would be steering a character they cannot see.
        public bool IsPanelOpen => blockedRecovery || focusedNpc != null || phaseOpen || settingsOpen
                                   || journalOpen || diaryOpen || IsWeeklyRecapOpen;
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
            engine = new EpisodeEngine(ContentCatalog.Create(20260910));
            if (File.Exists(saves.SavePath) || File.Exists(saves.BackupPath))
            {
                if (saves.TryLoad(out var loaded, out var loadMessage)) { engine = new EpisodeEngine(loaded); message = loadMessage; }
                else { blockedRecovery = true; message = loadMessage; }
            }
            // After the save is loaded, because the cast size is a property of the season being
            // played rather than of the scene, and a restored save may hold a different house from
            // the one a fresh season would create.
            SeatCast(engine.Snapshot);

            audioBed = HouseAudio.Attach(gameObject);
            reducedMotion = SaveRootOverride == null && PlayerPrefs.GetInt("Gamesim.ReducedMotion", 0) == 1;
            reducedAudio = SaveRootOverride == null && PlayerPrefs.GetInt("Gamesim.ReducedAudio", 0) == 1;
            muted = SaveRootOverride == null && PlayerPrefs.GetInt("Gamesim.Muted", 0) == 1;
            largeText = SaveRootOverride == null && PlayerPrefs.GetInt("Gamesim.LargeText", 0) == 1;
            volumePercent = SaveRootOverride == null ? Mathf.Clamp(PlayerPrefs.GetInt("Gamesim.Volume", 35), 0, 100) : 35;
            musicOn = SaveRootOverride != null || PlayerPrefs.GetInt("Gamesim.Music", 1) == 1;
            LoadDisplayPreferences();
            hud = gameObject.AddComponent<EpisodeHud>(); hud.Initialize(this);
            sting = CeremonySting.Attach(gameObject);
            takeover = CeremonyTakeover.Attach(gameObject);
            voteReveal = VoteReveal.Attach(gameObject);
            competitionCard = CompetitionResult.Attach(gameObject);
            keyCeremony = KeyCeremony.Attach(gameObject);
            tutorial = HouseTutorial.Attach(gameObject);
            opening = OpeningSequence.Attach(gameObject);
            seasonReport = SeasonReport.Attach(gameObject);
            weeklyRecap = WeeklyRecapScreen.Attach(gameObject);
            hud.RegisterOverlay(seasonReport.GetComponent<CanvasGroup>());
            hud.RegisterOverlay(weeklyRecap.GetComponent<CanvasGroup>());
            castSelect = CastSelect.Attach(gameObject);
            characterCreator = CharacterCreator.Attach(gameObject);
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
            if (SaveRootOverride == null) OpenMainMenu();
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
            var template = bodies.FirstOrDefault();
            if (template == null || cast.Count == 0) return;

            for (int i = bodies.Count; i < cast.Count; i++)
            {
                var clone = Instantiate(template.gameObject, template.transform.parent);
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

            for (int i = cast.Count; i < bodies.Count; i++) bodies[i].gameObject.SetActive(false);

            housemates = bodies.Take(cast.Count).ToArray();
            for (int i = 0; i < housemates.Length; i++)
            {
                var npc = housemates[i];
                npc.gameObject.SetActive(true);
                npc.Configure(cast[i].id, cast[i].name);
                npc.gameObject.name = cast[i].name;
                // Attach releases and rebuilds when the character id changed, so a recycled body
                // never keeps the previous houseguest's face.
                CharacterPresentation.Attach(npc.gameObject, cast[i], CastPalette.For(cast[i].id));
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
            // ] and [ (or the shoulders) cycle who the camera follows, out in the house with no panel
            // open. Tab does the same only when no HUD control is focused - a mouse player who
            // clicked the house - because with one focused, Tab is the keyboard ring's, and the HUD
            // keeps a control focused whenever it can.
            if (IsReady && !IsPanelOpen && !challengeActive && cameraRig != null)
            {
                var actions = cameraRig.Actions;
                bool nothingFocused = EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null;
                if (actions.Next.WasPressedThisFrame()) FollowNext(false);
                else if (actions.Previous.WasPressedThisFrame()) FollowNext(true);
                else if (nothingFocused && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                    FollowNext(Keyboard.current.shiftKey.isPressed);
            }
            // The chip follows the subject however it was chosen: a click on a body sets the
            // camera without a render, so the chip is redrawn on its own when the name changes.
            if (IsReady && hud != null && FollowedName != lastFollowed)
            {
                lastFollowed = FollowedName;
                hud.ShowFollowing(lastFollowed);
            }
            if (cameraRig != null && followRing == null) followRing = FollowRing.Attach(cameraRig);
            // A body that has just finished assembling changes what the HUD can show: its portraits
            // are rendered from the live character, and anything drawn before this point is holding
            // a fallback face until something else happens to trigger a render.
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
            TickNpcSocialRuntime(Time.unscaledDeltaTime);
            TickProximityWatch(Time.unscaledDeltaTime);
            frameAverage = Mathf.Lerp(frameAverage, Time.unscaledDeltaTime, 0.03f);
            if (diaryOpen && !CanUseDiary) { ClosePanels(); return; }
            // The house's shortcuts come through the actions map's second page: each key has a
            // gamepad button beside it there, so a controller reaches every panel the keyboard does.
            var shortcuts = cameraRig != null ? cameraRig.Actions : null;
            TickRoomTone();
            if (challengeActive && challengeRun != null) TickMiniGame();
            else if (challengeActive)
            {
                challengeValue = Mathf.PingPong((Time.unscaledTime - challengeStarted) * 0.75f, 1f);
                hud.SetChallenge(challengeValue, challengeHits);
                if (shortcuts != null && shortcuts.Hit.WasPressedThisFrame()) RecordChallengeHit();
            }
            if (shortcuts != null && shortcuts.Menu.WasPressedThisFrame())
            {
                // Topmost first. The main menu sits above the cast screen, which sits above the
                // HUD; closing a panel underneath either of them would leave a screen on top of the
                // house with nothing behind it. The menu itself ignores Escape when there is no
                // season to go back to, because there is nowhere for it to close to.
                if (mainMenu != null && mainMenu.IsShowing) { if (SeasonInProgress) CloseMainMenu(); }
                // The creator draws above the cast screen, so it takes Escape first — otherwise
                // the screen underneath would close out from under the form on top of it.
                else if (characterCreator != null && characterCreator.IsShowing) characterCreator.Dismiss();
                else if (castSelect != null && castSelect.IsShowing) castSelect.Dismiss();
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
            if (shortcuts != null && !hud.IsTyping)
            {
                if (shortcuts.Notebook.WasPressedThisFrame()) OpenJournal();
                if (shortcuts.Save.WasPressedThisFrame()) SaveNow();
                if (shortcuts.Diary.WasPressedThisFrame() && !IsPanelOpen) GoToDiary();
                if (shortcuts.Interact.WasPressedThisFrame() && !IsPanelOpen)
                {
                    if (!TryOpenDiary())
                    {
                        var npc = NearestNpc();
                        if (npc != null) TryOpenNpc(npc.Id); else TryOpenPhasePanel();
                    }
                }
            }
            if (!IsPanelOpen)
            {
                var npc = NearestNpc();
                if (npc != promptNpc) { promptNpc = npc; npcPrompt = npc != null ? "E  ·  Talk to " + npc.DisplayName : null; }
                string prompt = CanUseDiary ? "E  ·  Enter private diary room" : npc != null ? npcPrompt : CanUseStation() ? "E  ·  Open episode screen" : "";
                hud.SetPrompt(prompt);
            }
            else hud.SetPrompt("");
        }

        public Vector3 StationPosition => EpisodeEngine.IsCompetition(projected?.phase ?? EpisodePhase.Social) ? new Vector3(0, 0, 14) : new Vector3(-5, 0, -7);
        private bool CanUseStation() => !playerIsActive ||
            Vector3.Distance(player.transform.position, StationPosition) < 3;

        public bool TryOpenPhasePanel()
        {
            if (!IsReady || !CanUseStation()) return false;
            PauseNpcSocialForPanel();
            if (blockedRecovery) return false;
            ClosePanels(); phaseOpen = true; player.SetInputEnabled(false); cameraRig.ControlsEnabled = false; Render(); return true;
        }

        public void GoToStation()
        {
            ClosePanels();
            if (projected.Find(projected.playerId).status != ContestantStatus.Active) { TryOpenPhasePanel(); return; }
            if (!player.TryMoveTo(StationPosition)) message = "The episode screen is not reachable from here.";
            else message = "Walk to the highlighted room, then press E to open the episode screen.";
            Render();
        }

        public void ClosePanels() => ClosePanelsInternal(true);

        private void ClosePanelsInternal(bool render)
        {
            if (focusedNpc != null) focusedNpc.GetComponent<CharacterPresentation>()?.SetTalking(false);
            focusedNpc = null; lastSocialDelta = 0d; phaseOpen = false; settingsOpen = false; journalOpen = false; challengeActive = false;
            // Escape cancels without committing, so the run goes with the panel. Leaving it would
            // let a competition keep ticking behind a closed screen and commit itself later.
            challengeRun = null;
            diaryOpen = false; diaryDraft = null;
            lastSocialAction = null;
            // The recap is a panel by IsPanelOpen's reckoning, so closing panels has to close it —
            // otherwise the scrim stays up while everything behind it believes it is dismissed.
            if (weeklyRecap != null) weeklyRecap.Hide();
            if (cameraRig != null) { cameraRig.EndConversation(); cameraRig.ControlsEnabled = !blockedRecovery; }
            if (projected != null && player != null) player.SetInputEnabled(!blockedRecovery && projected.Find(projected.playerId).status == ContestantStatus.Active);
            if (render && hud != null) Render();
        }

        public void OpenSettings() { PauseNpcSocialForPanel(); ClosePanels(); settingsOpen = true; player.SetInputEnabled(false); cameraRig.ControlsEnabled = false; Render(); }
        public void OpenJournal() { PauseNpcSocialForPanel(); ClosePanels(); journalOpen = true; player.SetInputEnabled(false); cameraRig.ControlsEnabled = false; Render(); }

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
            var wasActive = new HashSet<string>(engine.Snapshot.contestants
                .Where(actor => actor.status == ContestantStatus.Active).Select(actor => actor.id));
            // Who was on the block before this command: a veto ceremony is the difference.
            var wasNominated = new HashSet<string>(engine.Snapshot.nominees ?? new List<string>());
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
            message = result.accepted ? visible?.text ?? "Decision committed." : result.reason;
            if (result.accepted)
            {
                diaryDraft = null; // A draft never survives a different committed revision.
                if (focusedNpc != null)
                {
                    lastSocialAction = command.kind;
                    lastSocialDelta = result.state.Score(result.state.playerId, focusedNpc.Id) - trustBefore;
                }
                message += "  ·  Saved locally."; Project();
                if (phaseOpen && wasYard != EpisodeEngine.IsCompetition(result.state.phase)) ClosePanels();
                var kind = result.state.events.LastOrDefault()?.kind;
                audioBed.PlayCue(kind == "winner" ? HouseAudio.Cue.Finale : kind == "eviction" ? HouseAudio.Cue.Eviction :
                    kind == "competition" ? HouseAudio.Cue.CompetitionWin : kind == "nomination" ? HouseAudio.Cue.Nomination :
                    kind == "veto" ? HouseAudio.Cue.Veto : HouseAudio.Cue.Button);
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
                if (competition != null && competitionCard != null)
                    competitionCard.Play(AwardTitle(wasPhase),
                        EpisodeEngine.CompetitionCategory(wasPhase, wasWeek), result.state.week,
                        CompetitionStandings(result.state), reducedMotion);

                // The veto field. Previously the one phase the episode passed through in silence.
                var field = result.state.events.Skip(knownEvents)
                    .LastOrDefault(entry => entry.kind == CeremonyTakeover.VetoSelectionKind
                        && (entry.audienceIds.Count == 0 || entry.audienceIds.Contains(result.state.playerId)));
                if (field != null && takeover != null)
                    takeover.Play(CeremonyTakeover.VetoSelectionKind, result.state.week,
                        VetoField(result.state), reducedMotion);

                var ceremony = result.state.events.Skip(knownEvents)
                    .LastOrDefault(entry => CeremonySting.IsCeremony(entry.kind)
                        && (entry.audienceIds.Count == 0 || entry.audienceIds.Contains(result.state.playerId)));
                if (ceremony != null)
                {
                    // The takeover opens the scene and the sting reports the result, so they play
                    // together rather than instead of each other: the card is over by the time the
                    // strip has finished its own entrance.
                    // An eviction gets the vote reveal instead of the generic card: it is the only
                    // beat whose outcome is not already inferable, and the commit resolves every
                    // ballot in one frame. If the reveal declines the shape — a block that is not
                    // two, or no ballots — the generic card still plays, so the beat is never silent.
                    bool revealed = ceremony.kind == CeremonySting.EvictionKind
                        && voteReveal != null
                        && voteReveal.Play(result.state.week, EvictionBlock(result.state),
                            EvictionBallots(result.state), EvictedThisCommit(result.state, wasActive), reducedMotion);

                    // The nomination gets the key ceremony for the same reason the eviction gets the
                    // vote reveal: the engine decides it in one commit, and the order is the beat.
                    revealed |= ceremony.kind == CeremonySting.NominationKind
                        && keyCeremony != null
                        && keyCeremony.Play(result.state.week, NameOf(result.state, result.state.hohId),
                            result.state.hohId == result.state.playerId,
                            SafeHouseguests(result.state), NominatedHouseguests(result.state), reducedMotion);
                    if (!revealed && takeover != null)
                        takeover.Play(ceremony.kind, result.state.week,
                            CeremonySubjects(result.state, ceremony.kind, wasActive), reducedMotion);
                    // The reveal narrates the eviction itself and outlives the strip by seconds, so
                    // the strip would only flash under it and vanish mid-tally. Everywhere else the
                    // two still pair up: card opens the scene, strip reports the result.
                    if (sting != null && !revealed) sting.Play(ceremony.kind, ceremony.text, reducedMotion);
                    // The bodies act the beat out in the house while the card and the strip report it.
                    ReactToCeremony(result.state, ceremony.kind, wasActive, wasNominated);
                    // The week's recap, once the beats that narrate the eviction have had their say.
                    // It waits rather than opening now because the reveal outlives its own strip by
                    // seconds and the two canvases share a sorting order — a recap that appeared
                    // immediately would cover the tally it is summarising.
                    if (ceremony.kind == CeremonySting.EvictionKind) QueueWeeklyRecap(wasWeek);
                }
            }
            Render(); return result;
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
            public const string Network = "Section · network";
            public const string Rooms = "Section · rooms";
            public const string Votes = "Section · votes";
            public const string Story = "Section · story";
        }

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
            var npcStates = state.contestants.Where(c => !c.isPlayer).ToArray();
            for (int i = 0; i < housemates.Length; i++)
            {
                var npc = housemates[i];
                // Imported IDs are rebound by saved slot, never guessed from display names.
                var model = npcStates[i];
                npc.Configure(model.id, model.name);
                npc.gameObject.SetActive(model.status == ContestantStatus.Active || model.status == ContestantStatus.Winner || model.status == ContestantStatus.RunnerUp);
                CharacterPresentation.Attach(npc.gameObject, model, Palette(i)).SetReducedMotion(reducedMotion);
                var label = npc.GetComponentInChildren<TextMesh>(); if (label != null) label.text = model.name;
            }
            CharacterPresentation.Attach(player.gameObject, state.Find(state.playerId), new Color(0.4f, 0.88f, 0.76f)).SetReducedMotion(reducedMotion);
            player.SetInputEnabled(!IsPanelOpen && state.Find(state.playerId).status == ContestantStatus.Active);
            cameraRig.ControlsEnabled = !IsPanelOpen;
            ReconcileNpcSocialWorld();
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
            hud.Begin(state, message, blockedRecovery, phaseOpen || focusedNpc != null || settingsOpen || journalOpen || diaryOpen);
            // Committed state, not the projection: a projected eviction is not a fact, and telling
            // someone they are out of the game is the last claim that should run ahead of the save.
            if (Spectating(engine.Snapshot)) hud.SpectatorNote(SpectatorDetail(engine.Snapshot));
            if (settingsOpen || blockedRecovery) { Settings(state); return; }
            if (diaryOpen) { RenderDiary(state); return; }
            if (journalOpen)
            {
                hud.PanelTitle("YOUR NOTEBOOK", "Private information is limited to what your character knows.");
                // The graph carries the caveat in its own legend, so repeating it here would be the
                // same sentence twice within one screen.
                hud.SocialGraphPanel(state);
                hud.Mark(NotebookSection.Network);
                hud.Heading("WHO IS WHERE");
                hud.Mark(NotebookSection.Rooms);
                hud.HouseMapPanel(HouseOccupancy(state));
                // Name, then who they are outside the game, then where you stand — the order the
                // reference build's houseguest list uses. The card line is omitted rather than left
                // blank when a save predates those fields.
                foreach (var c in state.contestants.Where(c => !c.isPlayer))
                {
                    hud.Paragraph(c.name + " · " + c.status + " · Your trust " + state.Score(state.playerId, c.id).ToString("0"));
                    string card = CardLine(c);
                    if (!string.IsNullOrEmpty(card)) hud.Paragraph(card);
                }
                // How the house voted, with the reason each voter committed. The engine has written
                // these to every ballot since the beginning and nothing has ever shown them — the
                // event log carries the sentence, but only the last line of it reaches the status
                // bar, so the "why" behind an eviction was effectively private.
                if (state.votes != null && state.votes.Count > 0)
                {
                    hud.Heading("HOW THE HOUSE VOTED");
                    hud.Mark(NotebookSection.Votes);
                    foreach (var vote in state.votes)
                    {
                        var voter = state.Find(vote.voterId);
                        var target = state.Find(vote.targetId);
                        if (voter == null || target == null) continue;
                        hud.PortraitRow(voter.id,
                            voter.name + " voted to evict " + (target.id == state.playerId ? "you" : target.name),
                            vote.reason);
                    }
                }
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
                    hud.Paragraph(promise.kind + " · " + state.Find(promise.fromId).name + " → " + state.Find(promise.toId).name + " · " + promise.status);
                foreach (var alliance in state.alliances.Where(a => a.members.Contains(state.playerId))) hud.Paragraph(alliance.name + (alliance.active ? " · active" : " · ended"));
                hud.Heading("YOUR LOYALTY DECLARATIONS");
                foreach (var oath in state.loyaltyOaths.Where(oath => oath.playerId == state.playerId || oath.targetId == state.playerId))
                    hud.Paragraph("Week " + oath.week + ": " + (oath.playerId == state.playerId
                        ? "You declared loyalty to " + state.Find(oath.targetId).name
                        : state.Find(oath.playerId).name + " declared loyalty to you") + ". A declaration is not a mutual guarantee.");
                RenderDiaryRecord(state);
                foreach (var memory in state.memories.Where(m => m.ownerId == state.playerId)) hud.Paragraph("Week " + memory.week + ": " + memory.text);
                RenderStorySoFar(state);
                hud.ApplyPendingScroll();
                return;
            }
            if (focusedNpc != null)
            {
                var npc = state.Find(focusedNpc.Id);
                hud.SpeakerTitle(npc.id, npc.name.ToUpperInvariant(), npc.pronouns + " · " + string.Join(" / ", npc.traits));
                hud.NpcDialogue(state, npc.id, lastSocialAction);
                if (lastSocialAction.HasValue) hud.OutcomeChips(lastSocialDelta);
                if (state.phase != EpisodePhase.Social && state.phase != EpisodePhase.Campaign)
                { hud.Paragraph("The next ceremony is waiting. We can catch up during free time or campaigning."); return; }
                if (state.oathOpportunities.Contains(npc.id))
                {
                    hud.Heading("A PERSONAL LOYALTY DECLARATION");
                    hud.Paragraph("This is your commitment, not " + npc.name + "'s consent or promise. Nominating or voting against them can break your oath.");
                    hud.Action(EpisodeHud.OathDeclareCaption, () => Commit(state, EpisodeCommandKind.SwearLoyalty, npc.id));
                    hud.Action(EpisodeHud.OathDeclineCaption, () => Commit(state, EpisodeCommandKind.DeclineLoyalty, npc.id));
                }
                else if (state.loyaltyOaths.Any(oath => oath.playerId == state.playerId && oath.targetId == npc.id))
                    hud.Paragraph("Your loyalty declaration is recorded. It does not bind " + npc.name + " to protect you.");
                // The category is a chip pinned to the button, never part of its caption. Baking it
                // into the label broke every test that finds a control by the words on it — and the
                // web build draws it as a separate pill anyway, so the caption was the wrong place.
                bool allied = state.Allied(state.playerId, npc.id);
                hud.Tag(hud.Action("Spend time together", () => Commit(state, EpisodeCommandKind.Talk, npc.id)),
                    Category(EpisodeCommandKind.Talk));
                // Five ways of having a conversation where there was one. Each says what it is for,
                // because the difference between them is the whole point: small talk is safe and
                // slight, a secret is the biggest swing either way in the game.
                hud.Tag(hud.Action(EpisodeHud.SmallTalkCaption, () => Commit(state, EpisodeCommandKind.SmallTalk, npc.id)),
                    Category(EpisodeCommandKind.SmallTalk));
                hud.Tag(hud.Action(EpisodeHud.PersonalChatCaption, () => Commit(state, EpisodeCommandKind.PersonalChat, npc.id)),
                    Category(EpisodeCommandKind.PersonalChat));
                hud.Tag(hud.Action(EpisodeHud.RelationshipBuildingCaption, () => Commit(state, EpisodeCommandKind.RelationshipBuilding, npc.id)),
                    Category(EpisodeCommandKind.RelationshipBuilding));
                hud.Tag(hud.Action(EpisodeHud.StrategicDiscussionCaption, () => Commit(state, EpisodeCommandKind.StrategicDiscussion, npc.id)),
                    Category(EpisodeCommandKind.StrategicDiscussion));
                hud.Tag(hud.Action(EpisodeHud.DiscussGameCaption, () => Commit(state, EpisodeCommandKind.DiscussGame, npc.id)),
                    Category(EpisodeCommandKind.DiscussGame));
                hud.Tag(hud.Action(EpisodeHud.ShareSecretCaption, () => Commit(state, EpisodeCommandKind.ShareSecret, npc.id)),
                    Category(EpisodeCommandKind.ShareSecret));
                hud.Tag(hud.Action("Promise safety", () => Commit(state, EpisodeCommandKind.PromiseSafety, npc.id)),
                    Category(EpisodeCommandKind.PromiseSafety));
                hud.Tag(hud.Action("Propose a final-two promise", () => Commit(state, EpisodeCommandKind.PromiseFinalTwo, npc.id)),
                    Category(EpisodeCommandKind.PromiseFinalTwo));
                hud.Tag(hud.Action(allied ? "Leave our alliance" : "Propose an alliance",
                        () => Commit(state, allied ? EpisodeCommandKind.LeaveAlliance : EpisodeCommandKind.FormAlliance, npc.id)),
                    Category(allied ? EpisodeCommandKind.LeaveAlliance : EpisodeCommandKind.FormAlliance));
                hud.Tag(hud.Action("Share something I know", () => Commit(state, EpisodeCommandKind.ShareInformation, npc.id)),
                    Category(EpisodeCommandKind.ShareInformation));
                hud.Tag(hud.Action("Ask what they have heard", () => Commit(state, EpisodeCommandKind.AskForIntel, npc.id)),
                    Category(EpisodeCommandKind.AskForIntel));
                // Both of these need a third person, so they are offered per subject rather than as
                // one control that would then have to ask "about whom?" after being clicked.
                foreach (var subject in state.Active.Where(c => !c.isPlayer && c.id != npc.id))
                {
                    string about = subject.id;
                    hud.Tag(hud.ActionFor(about, "Vent about " + subject.name,
                        () => Commit(state, EpisodeCommandKind.VentAbout, npc.id, about)),
                        Category(EpisodeCommandKind.VentAbout));
                    hud.Tag(hud.ActionFor(about, "Tell them something untrue about " + subject.name,
                        () => Commit(state, EpisodeCommandKind.SpreadLie, npc.id, about)),
                        Category(EpisodeCommandKind.SpreadLie));
                }
                // A rumour is about somebody but told to the house rather than to one person, so it
                // is offered per subject and not per listener.
                foreach (var subject in state.Active.Where(c => !c.isPlayer && c.id != npc.id))
                {
                    string about = subject.id;
                    hud.Tag(hud.ActionFor(about, EpisodeHud.WhisperCaption(subject.name),
                        () => Commit(state, EpisodeCommandKind.SpreadRumor, about, text: EpisodeEngine.WhisperCampaign)),
                        Category(EpisodeCommandKind.SpreadRumor));
                    hud.Tag(hud.ActionFor(about, EpisodeHud.CalloutCaption(subject.name),
                        () => Commit(state, EpisodeCommandKind.SpreadRumor, about, text: EpisodeEngine.PublicCallout)),
                        Category(EpisodeCommandKind.SpreadRumor));
                }
                hud.Tag(hud.Action("Work against them quietly", () => Commit(state, EpisodeCommandKind.SchemeAgainst, npc.id)),
                    Category(EpisodeCommandKind.SchemeAgainst));
                DealPanel(state, npc);
                if (state.phase == EpisodePhase.Campaign)
                    foreach (var nominee in state.nominees) { string id = nominee; hud.Tag(hud.ActionFor(id, "Promise to evict " + state.Find(id).name, () => Commit(state, EpisodeCommandKind.PromiseVote, npc.id, id)), Category(EpisodeCommandKind.PromiseVote)); }
                return;
            }
            if (!phaseOpen) return;
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
            if (state.phase == EpisodePhase.JuryQuestioning) { hud.JuryQuestioning(state); return; }
            if (state.phase == EpisodePhase.FinalSpeeches) { hud.FinalSpeech(state); return; }
            if (state.phase == EpisodePhase.Finished)
            {
                hud.Paragraph("Winner: " + state.Find(state.winnerId).name + ". Runner-up: " + state.Find(state.runnerUpId).name + ".");
                hud.Paragraph("Your choices and votes are preserved in the notebook. Start another season from Settings; the old save is retained.");
                hud.Action("Season report", ShowSeasonReport);
                hud.Action("Review the season", OpenJournal);
                return;
            }
            if (EpisodeEngine.IsCompetition(state.phase))
            {
                if (!state.competitionResolved)
                {
                    if (EpisodeEngine.CompetitionPlayers(state).Any(c => c.isPlayer))
                    {
                        var game = CompetitionMiniGames.For(
                            EpisodeEngine.CompetitionCategory(state.phase, state.week));
                        hud.Paragraph(CompetitionMiniGames.Brief(game));
                        hud.Paragraph(state.phase == EpisodePhase.FinalHoHPart1
                            ? "How you do adds 0–2 effective endurance points (capped at 10) for the survival challenge; stored stats are unchanged."
                            : "How you do supplies a 0–2 point bonus; housemate stats and the saved seed determine the rest.");
                        hud.Action(CompetitionMiniGames.EnterCaption(game), () => StartChallenge(state));
                        hud.Action("Accessible alternative: steady 1-point bonus", () => Commit(state, EpisodeCommandKind.Compete, performance: .5));
                        if (state.phase == EpisodePhase.HoH || state.phase == EpisodePhase.Veto)
                        {
                            hud.Paragraph("Or simulate this weekly competition using weighted rules. Preparation: "
                                + state.playerStudyBonus + "/5; event bonus: " + state.phaseEventCompBonus
                                + ". These boost only your simulated score, with no precision bonus. Preparation is kept for later weeks and does not boost final HoH.");
                            hud.Action(EpisodeHud.SimulateCompetitionCaption, () => SimulateCompetition(state));
                            hud.Paragraph("Or throw it. You still compete and the result still stands — "
                                + "you simply do not try, which is sometimes the safer week.");
                            hud.Action(EpisodeHud.ThrowCompetitionCaption, () => ThrowCompetition(state));
                        }
                    }
                    else hud.Action("Watch eligible housemates compete", () => Commit(state, EpisodeCommandKind.Advance));
                }
                else
                {
                    foreach (var score in state.competitionScores.OrderByDescending(x => x.score)) hud.Paragraph(state.Find(score.contestantId).name + "   " + score.score.ToString("0.00"));
                    hud.Action("Continue to the next ceremony", () => Commit(state, EpisodeCommandKind.Advance));
                }
                return;
            }
            if (RenderPlayerDecision(state, false)) return;
            if (state.phase == EpisodePhase.FinalEviction && state.hohId == state.playerId)
            {
                hud.Paragraph("You won the final HoH. Choose who to evict; the other housemate joins you in the final two.");
                foreach (var candidate in state.Active.Where(c => !c.isPlayer)) { string id = candidate.id; hud.ActionFor(id, "Evict " + candidate.name, () => Commit(state, EpisodeCommandKind.FinalEvict, id)); }
                return;
            }
            if (state.phase == EpisodePhase.Jury && !state.Active.Any(c => c.isPlayer) && !state.votes.Any(v => v.voterId == state.playerId))
            {
                hud.Paragraph("As a juror, choose who deserves to win.");
                foreach (var candidate in state.Active) { string id = candidate.id; hud.ActionFor(id, "Vote for " + candidate.name + " to win", () => Commit(state, EpisodeCommandKind.CastVote, id)); }
                return;
            }
            if (state.nominees.Count > 0) hud.Paragraph("Nominees: " + string.Join(" and ", state.nominees.Select(id => state.Find(id).name)));
            if (state.hohId != null) hud.Paragraph("HoH: " + state.Find(state.hohId).name);
            if (state.vetoHolderId != null) hud.Paragraph("Veto holder: " + state.Find(state.vetoHolderId).name);
            if (state.phase == EpisodePhase.Social || state.phase == EpisodePhase.Campaign)
            {
                CurrentLocation(state);
                // The web build draws this as a bar you can watch drain rather than a sentence you
                // have to read and subtract. The caption still carries the numbers.
                int budget = EpisodeEngine.SocialActionBudget(state);
                hud.Meter("Interactions available",
                    Mathf.Max(0, budget - EpisodeEngine.SocialActionsSpent(state)), budget, UiTheme.Accent);
                // Standing modifiers, shown beside the budget they apply to. The web build puts a
                // social-bonus chip on each action's result; here that would misattribute it,
                // because this bonus accrues from diary answers and story beats rather than from
                // the action it would be printed under. Shown as what it is: something you carry.
                if (state.phaseEventSocialBonus > 0)
                    hud.Paragraph("Carrying a +" + state.phaseEventSocialBonus + " social bonus from earlier choices.");
                if (state.playerStudyBonus > 0)
                    hud.Paragraph("Preparation banked for competitions: " + state.playerStudyBonus + "/5.");
                // Before anything the player chose to do: something has happened to them, and a
                // situation buried under the ordinary controls is a situation they will not see.
                PendingHouseEvent(state);
                HouseWideActions(state);
                hud.Paragraph("Explore and talk freely before continuing. You can finish the window whenever you choose. "
                    + "The house gives you half its number in actions each week, so the budget tightens as people leave.");
                // Listening in needs no one to talk to, so it sits here rather than in a conversation.
                if (state.Active.Count(c => !c.isPlayer) >= 2)
                {
                    hud.Paragraph("You can also try to overhear a conversation you are not part of. "
                        + "It works about seven times in ten; the rest of the time somebody notices.");
                    hud.Tag(hud.Action("Listen in on a conversation", () => Commit(state, EpisodeCommandKind.Eavesdrop)),
                        Category(EpisodeCommandKind.Eavesdrop));
                }
            }
            if (state.phase == EpisodePhase.Jury) hud.Paragraph("Four jurors choose the winner. The source game's tie rule awards a tied jury to the second finalist in cast order.");
            hud.Action(state.phase == EpisodePhase.Social ? "Begin the next competition" : state.phase == EpisodePhase.Campaign ? "Close campaigning and open voting" : "Continue episode", () => Commit(state, EpisodeCommandKind.Advance));
        }

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
            if (competitionCard != null) competitionCard.Cancel();
            if (keyCeremony != null) keyCeremony.Cancel();
            DisposeNpcSocialWorld();
        }
        private void OnEnable()
        {
            if (!IsReady) return;
            hud?.SetVisible(true); ClosePanels();
        }

        // The sting is a scene root rather than a child, so it has to be taken down explicitly.
        private void OnDestroy()
        {
            DisposeNpcSocialWorld();
            if (hud != null) Destroy(hud);
            if (sting != null) { Destroy(sting.gameObject); sting = null; }
            if (takeover != null) { Destroy(takeover.gameObject); takeover = null; }
            if (voteReveal != null) { Destroy(voteReveal.gameObject); voteReveal = null; }
            if (competitionCard != null) { Destroy(competitionCard.gameObject); competitionCard = null; }
            if (keyCeremony != null) { Destroy(keyCeremony.gameObject); keyCeremony = null; }
            if (tutorial != null) { Destroy(tutorial.gameObject); tutorial = null; }
            if (opening != null) { Destroy(opening.gameObject); opening = null; }
        }

        public static string PhaseTitle(EpisodePhase phase)
        {
            switch (phase)
            {
                case EpisodePhase.Social: return "MAKE YOUR NEXT MOVE";
                case EpisodePhase.HoH: return "HEAD OF HOUSEHOLD";
                case EpisodePhase.VetoSelection: return "VETO PLAYER SELECTION";
                case EpisodePhase.VetoMeeting: return "VETO CEREMONY";
                case EpisodePhase.FinalHoHPart1: return "FINAL HOH · ENDURANCE";
                case EpisodePhase.FinalHoHPart2: return "FINAL HOH · SKILL";
                case EpisodePhase.FinalHoHPart3: return "FINAL HOH · MENTAL";
                case EpisodePhase.FinalEviction: return "CHOOSE YOUR FINAL TWO";
                case EpisodePhase.JuryQuestioning: return "FACE THE JURY";
                case EpisodePhase.FinalSpeeches: return "MAKE YOUR FINAL CASE";
                case EpisodePhase.Finished: return "SEASON FINALE";
                default: return phase.ToString().ToUpperInvariant();
            }
        }
    }
}
