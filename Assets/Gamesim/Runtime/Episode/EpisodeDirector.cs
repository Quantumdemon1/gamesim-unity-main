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
using UnityEngine.InputSystem;

namespace Gamesim.Episode
{
    /// <summary>Unity interaction adapter. Only committed simulation snapshots drive presentation.</summary>
    [DisallowMultipleComponent]
    public sealed partial class EpisodeDirector : MonoBehaviour
    {
        [SerializeField] private HousePlayerController player;
        [SerializeField] private HouseCameraRig cameraRig;
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
            muted = SaveRootOverride == null && PlayerPrefs.GetInt("Gamesim.Muted", 0) == 1;
            largeText = SaveRootOverride == null && PlayerPrefs.GetInt("Gamesim.LargeText", 0) == 1;
            volumePercent = SaveRootOverride == null ? Mathf.Clamp(PlayerPrefs.GetInt("Gamesim.Volume", 35), 0, 100) : 35;
            musicOn = SaveRootOverride != null || PlayerPrefs.GetInt("Gamesim.Music", 1) == 1;
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
            castSelect = CastSelect.Attach(gameObject);
            characterCreator = CharacterCreator.Attach(gameObject);
            mainMenu = MainMenu.Attach(gameObject);
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
                clone.transform.SetPositionAndRotation(
                    SpawnNear(template.transform.position, i), template.transform.rotation);
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

        /// <summary>A walkable spot near the template, spiralling outward so bodies do not stack.</summary>
        private static Vector3 SpawnNear(Vector3 origin, int index)
        {
            float angle = index * 137.5f * Mathf.Deg2Rad;   // golden angle: no two early picks align
            float radius = 1.6f + index * 0.7f;
            var wanted = origin + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            return NavMesh.SamplePosition(wanted, out var hit, 6f, NavMesh.AllAreas) ? hit.position : origin;
        }

        private int seenBodiesCompleted;

        private void Update()
        {
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
            frameAverage = Mathf.Lerp(frameAverage, Time.unscaledDeltaTime, 0.03f);
            if (diaryOpen && !CanUseDiary) { ClosePanels(); return; }
            if (challengeActive && challengeRun != null) TickMiniGame();
            else if (challengeActive)
            {
                challengeValue = Mathf.PingPong((Time.unscaledTime - challengeStarted) * 0.75f, 1f);
                hud.SetChallenge(challengeValue, challengeHits);
                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) RecordChallengeHit();
            }
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
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
                else ClosePanels();
                return;
            }
            if (keyboard != null && !hud.IsTyping)
            {
                if (keyboard.jKey.wasPressedThisFrame) OpenJournal();
                if (keyboard.f5Key.wasPressedThisFrame) SaveNow();
                if (keyboard.rKey.wasPressedThisFrame && !IsPanelOpen) GoToDiary();
                if (keyboard.eKey.wasPressedThisFrame && !IsPanelOpen)
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

        private HouseNpc NearestNpc()
        {
            if (!playerIsActive) return null;
            HouseNpc nearest = null; float nearestSquared = 2.8f * 2.8f;
            foreach (var npc in housemates)
            {
                if (npc == null || !npc.gameObject.activeInHierarchy) continue;
                float squared = (player.transform.position - npc.transform.position).sqrMagnitude;
                if (squared <= nearestSquared && CanTalk(npc)) { nearest = npc; nearestSquared = squared; }
            }
            return nearest;
        }

        private bool CanTalk(HouseNpc npc)
        {
            var origin = player.transform.position + Vector3.up * 1.15f;
            var offset = npc.transform.position + Vector3.up * 1.15f - origin;
            if (offset.magnitude > 2.8f) return false;
            int count = Physics.RaycastNonAlloc(origin, offset.normalized, sightHits, offset.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false; // Fail closed if a crowded ray overflows the reusable buffer.
            for (int i = 0; i < count; i++)
                if (!sightHits[i].transform.IsChildOf(player.transform) && !sightHits[i].transform.IsChildOf(npc.transform)) return false;
            return true;
        }

        public Vector3 StationPosition => EpisodeEngine.IsCompetition(projected?.phase ?? EpisodePhase.Social) ? new Vector3(0, 0, 14) : new Vector3(-5, 0, -7);
        private bool CanUseStation() => !playerIsActive ||
            Vector3.Distance(player.transform.position, StationPosition) < 3;

        public bool TryOpenNpc(string id)
        {
            if (!IsReady || blockedRecovery || challengeActive || projected.Find(projected.playerId).status != ContestantStatus.Active || projected.Find(id)?.status != ContestantStatus.Active) return false;
            var npc = housemates.FirstOrDefault(n => n.Id == id && n.gameObject.activeInHierarchy);
            if (npc == null || !CanTalk(npc)) return false;
            PauseNpcSocialForPanel();
            if (blockedRecovery) return false;
            ClosePanels(); focusedNpc = npc; player.SetInputEnabled(false); cameraRig.SetConversationFocus(player.transform, npc.transform);
            npc.GetComponent<CharacterPresentation>()?.SetTalking(true); Render(); return true;
        }

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
                    // The week's recap, once the beats that narrate the eviction have had their say.
                    // It waits rather than opening now because the reveal outlives its own strip by
                    // seconds and the two canvases share a sorting order — a recap that appeared
                    // immediately would cover the tally it is summarising.
                    if (ceremony.kind == CeremonySting.EvictionKind) QueueWeeklyRecap(wasWeek);
                }
            }
            Render(); return result;
        }

        /// <summary>
        /// The faces a ceremony card should show, read from committed state rather than from the
        /// event sentence.
        ///
        /// <para>Every subject is checked against the same audience rule the card text already
        /// passes. A portrait is a stronger disclosure than a name — it says unambiguously who,
        /// where prose can be vague — so the rail and this list stay inside what the player is
        /// entitled to know.</para>
        /// </summary>
        private List<CeremonyTakeover.Subject> CeremonySubjects(
            EpisodeState state, string kind, HashSet<string> wasActive)
        {
            var subjects = new List<CeremonyTakeover.Subject>();
            if (state == null) return subjects;

            void Add(string id, string badge)
            {
                var actor = state.Find(id);
                if (actor == null) return;
                subjects.Add(new CeremonyTakeover.Subject(actor.name, badge,
                    CharacterPortraits.Get(
                        CharacterPresentation.AppearanceId(actor, ContentCatalog.CanonicalId(actor.id)))));
            }

            switch (kind)
            {
                case CeremonySting.NominationKind:
                case CeremonySting.VetoKind:
                    if (state.nominees != null)
                        foreach (var id in state.nominees) Add(id, "NOMINATED");
                    break;
                case CeremonySting.EvictionKind:
                    // Whoever stopped being active during this commit. Usually one person; the
                    // loop rather than a Single() because a double eviction would still be true.
                    foreach (var actor in state.contestants)
                        if (actor.status != ContestantStatus.Active && wasActive != null && wasActive.Contains(actor.id))
                            Add(actor.id, "EVICTED");
                    break;
                case CeremonySting.WinnerKind:
                    Add(state.winnerId, "WINNER");
                    Add(state.runnerUpId, "RUNNER-UP");
                    break;
            }
            return subjects;
        }

        /// <summary>
        /// What the competition was for, named from the phase the command was issued in.
        ///
        /// <para>Read from the pre-commit phase rather than parsed out of the event sentence: the
        /// committed phase has already advanced to whatever comes next, and picking the words back
        /// out of "Competition winner: X · Skill." would couple the card to copy.</para>
        /// </summary>
        private static string AwardTitle(EpisodePhase phase)
        {
            switch (phase)
            {
                case EpisodePhase.HoH: return "Head of Household";
                case EpisodePhase.Veto: return "Power of Veto";
                case EpisodePhase.FinalHoHPart1: return "Final HoH · Part 1";
                case EpisodePhase.FinalHoHPart2: return "Final HoH · Part 2";
                case EpisodePhase.FinalHoHPart3: return "Final HoH · Part 3";
                default: return "Competition";
            }
        }

        /// <summary>The scored field, best first, straight from committed state.</summary>
        private static List<CompetitionResult.Standing> CompetitionStandings(EpisodeState state)
        {
            var standings = new List<CompetitionResult.Standing>();
            if (state?.competitionScores == null) return standings;

            double best = double.MinValue;
            string winner = null;
            foreach (var entry in state.competitionScores)
                if (entry.score > best) { best = entry.score; winner = entry.contestantId; }

            foreach (var entry in state.competitionScores.OrderByDescending(x => x.score))
            {
                var actor = state.Find(entry.contestantId);
                if (actor == null) continue;
                standings.Add(new CompetitionResult.Standing(
                    actor.name, entry.score, actor.id == winner, actor.id == state.playerId,
                    CharacterPortraits.Get(
                        CharacterPresentation.AppearanceId(actor, ContentCatalog.CanonicalId(actor.id)))));
            }
            return standings;
        }

        /// <summary>
        /// Who is standing in which room, right now, by nearest room marker.
        ///
        /// <para>Scene-local, like the diary-room lookup: a second loaded house must not contribute
        /// markers to this one. Read from live transforms rather than from the simulation, because
        /// the simulation does not model position — where a houseguest is standing is a fact about
        /// the scene, and claiming otherwise would be inventing state.</para>
        /// </summary>
        /// <summary>
        /// The room the player is standing in and who else is in it — the web build's "Current
        /// Location" card.
        ///
        /// <para>Read from the same occupancy the notebook's house map uses, which derives each
        /// houseguest's room from where their body actually is rather than from a stored field. So
        /// this cannot disagree with the map, and it cannot claim someone is nearby who is not.</para>
        ///
        /// <para>It earns its place on the social screen because the decision being made there is
        /// who to talk to, and that was previously answerable only by opening the notebook or
        /// turning the camera.</para>
        /// </summary>
        private void CurrentLocation(EpisodeState state)
        {
            var here = HouseOccupancy(state)
                .FirstOrDefault(room => room.Occupants != null && room.Occupants.Any(person => person.IsPlayer));
            if (string.IsNullOrEmpty(here.Name)) return;

            var others = here.Occupants.Where(person => !person.IsPlayer).Select(person => person.Name).ToArray();
            hud.Heading("CURRENT LOCATION  ·  " + here.Name.ToUpperInvariant());
            hud.Paragraph(others.Length == 0
                ? "You have this room to yourself."
                : others.Length + (others.Length == 1 ? " houseguest here: " : " houseguests here: ") + string.Join(", ", others));
        }

        /// <summary>
        /// A social action's category, drawn as a pill beside the control.
        ///
        /// <para>The grouping is real rather than invented: these commands already divide by what
        /// they commit. Talking and sharing information move a relationship and nothing else;
        /// promises and alliances write a binding record that comes due later; studying the house
        /// banks a competition bonus and touches no one. Unlike the confession "risk" badges, which
        /// have no counterpart in this simulation at all, this is a name for structure that is
        /// already there.</para>
        /// </summary>
        private static string Category(EpisodeCommandKind kind)
        {
            switch (kind)
            {
                case EpisodeCommandKind.Talk:
                case EpisodeCommandKind.ShareInformation:
                case EpisodeCommandKind.AskForIntel:
                case EpisodeCommandKind.VentAbout:
                    return "social";
                // Their own category on purpose. These are the actions that can rebound on you, and
                // the chip is the only warning before you spend an action on one.
                case EpisodeCommandKind.Eavesdrop:
                case EpisodeCommandKind.SpreadLie:
                case EpisodeCommandKind.SchemeAgainst:
                    return "risky";
                case EpisodeCommandKind.SetBackdoorPlan:
                case EpisodeCommandKind.ProposeDeal:
                case EpisodeCommandKind.RespondToDeal:
                    return "strategic";
                case EpisodeCommandKind.PromiseSafety:
                case EpisodeCommandKind.PromiseVote:
                case EpisodeCommandKind.PromiseFinalTwo:
                case EpisodeCommandKind.FormAlliance:
                case EpisodeCommandKind.LeaveAlliance:
                case EpisodeCommandKind.SwearLoyalty:
                    return "strategic";
                case EpisodeCommandKind.StudyHouse:
                    return "preparation";
                default:
                    return null;
            }
        }

        /// <summary>
        /// "The Diplomat · 31 · Mediator", or as much of it as the save actually holds.
        /// </summary>
        public static string CardLine(ContestantState actor)
        {
            if (actor == null) return null;
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(actor.archetype)) parts.Add(actor.archetype);
            if (actor.age > 0) parts.Add(actor.age.ToString());
            if (!string.IsNullOrEmpty(actor.occupation)) parts.Add(actor.occupation);
            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }

        private List<HouseMap.Room> HouseOccupancy(EpisodeState state)
        {
            var rooms = new List<HouseMap.Room>();
            if (state == null) return rooms;

            var markers = gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseRoomMarker>(true))
                .Where(marker => !string.IsNullOrEmpty(marker.RoomName))
                .OrderBy(marker => marker.RoomName, StringComparer.Ordinal)
                .ToArray();
            if (markers.Length == 0) return rooms;

            var occupants = new Dictionary<string, List<HouseMap.Occupant>>();
            foreach (var marker in markers) occupants[marker.RoomName] = new List<HouseMap.Occupant>();

            foreach (var visual in gameObject.scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<CharacterPresentation>(true)))
            {
                var actor = state.Find(visual.CharacterId);
                if (actor == null || actor.status != ContestantStatus.Active) continue;

                HouseRoomMarker nearest = null;
                float best = float.MaxValue;
                foreach (var marker in markers)
                {
                    float distance = (marker.transform.position - visual.transform.position).sqrMagnitude;
                    if (distance >= best) continue;
                    best = distance; nearest = marker;
                }
                if (nearest == null) continue;

                occupants[nearest.RoomName].Add(new HouseMap.Occupant(actor.name,
                    CharacterPortraits.Get(
                        CharacterPresentation.AppearanceId(actor, ContentCatalog.CanonicalId(actor.id))),
                    actor.id == state.playerId));
            }

            foreach (var marker in markers) rooms.Add(new HouseMap.Room(marker.RoomName, occupants[marker.RoomName]));
            return rooms;
        }

        /// <summary>
        /// Starts the first-run tour, once, for a player who has never seen it.
        ///
        /// <para>Never in batchmode. The tour is the only overlay that waits for a click, so it is
        /// the only one that could hold up an automated season — and a headless run is by definition
        /// not a first-time player. Tests drive <c>HouseTutorial.Show</c> directly instead.</para>
        /// </summary>
        public void OfferTutorial()
        {
            if (tutorial == null || Application.isBatchMode || HouseTutorial.Seen) return;
            tutorial.Show(FindChrome);
        }

        /// <summary>
        /// Plays whichever opening beats this season has not seen.
        ///
        /// <para>The tour used to be started directly from both of these call sites. It is now the
        /// fourth of five beats, so the sequence owns it and both sites hand over here — which is
        /// also what makes the intro land before the tour rather than after it.</para>
        ///
        /// <para>Never in batchmode, for the reason the tour was never offered there: a sequence that
        /// waits is the only thing that can hold up an automated season, and a headless run is by
        /// definition not seeing any of this. Tests drive <see cref="OpeningSequence.Play"/> directly
        /// instead.</para>
        /// </summary>
        public void PlayOpening()
        {
            if (opening == null || Application.isBatchMode) { OfferTutorial(); return; }
            if (opening.IsPlaying) return;

            var state = projected;
            opening.Play(state.openingBeatsSeen, new OpeningSequence.Settings
            {
                MarkBeat = MarkOpeningBeat,
                Rig = cameraRig,
                RoomStops = RoomStops(),
                RunTutorial = done =>
                {
                    OfferTutorial();
                    if (tutorial == null || !tutorial.IsShowing) { done(); return; }
                    StartCoroutine(WaitForTutorial(done));
                },
                Finished = Render,
                ReducedMotion = reducedMotion,
                Cast = state.Active.ToList(),
                ArrivalLine = state.events
                    .Where(entry => entry.kind == "arrival")
                    .Select(entry => entry.text)
                    .LastOrDefault(),
            });
        }

        private System.Collections.IEnumerator WaitForTutorial(Action done)
        {
            while (tutorial != null && tutorial.IsShowing) yield return null;
            done();
        }

        /// <summary>
        /// Records a finished beat, through the engine like any other decision.
        ///
        /// <para>It is a command rather than a field the presentation writes because the record has
        /// to survive a reload, and the only thing here that survives a reload is the season. The
        /// meet and greet also has a rules consequence — the player stops being a stranger — and a
        /// consequence belongs to the engine wherever it is triggered from.</para>
        /// </summary>
        private void MarkOpeningBeat(string beat)
        {
            if (string.IsNullOrEmpty(beat) || projected.openingBeatsSeen.Contains(beat)) return;
            Commit(projected, EpisodeCommandKind.MarkOpeningBeat, target: beat);
        }

        /// <summary>
        /// Where the walk-in stops: every room marker in the scene, in the order the house lists
        /// them, which is the order the memory wall and the map already use.
        /// </summary>
        private List<KeyValuePair<string, Vector3>> RoomStops() =>
            gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseRoomMarker>(true))
                .Where(marker => !string.IsNullOrEmpty(marker.RoomName))
                .OrderBy(marker => marker.RoomName, StringComparer.Ordinal)
                .Select(marker => new KeyValuePair<string, Vector3>(marker.RoomName, marker.transform.position))
                .ToList();

        /// <summary>
        /// Decides what the music bed should be doing, from what is on screen.
        ///
        /// <para>The reference's rule, from <c>GameSim-Game-Flow_v2.md</c>: a theme over the opening,
        /// then background music through the season, and silence on the screens that are not the game
        /// — setup and the final stats. Expressed here as a question about state rather than a set of
        /// commands scattered through the code, so there is one place that decides and no way for two
        /// screens to disagree about what is playing.</para>
        ///
        /// <para>Safe to call from anywhere and often: <see cref="HouseAudio.SetMusic"/> ignores a
        /// state it is already in, so this does not restart the track on every render.</para>
        /// </summary>
        private void ApplyMusic()
        {
            if (audioBed == null) return;
            if (!musicOn) { audioBed.SetMusic(HouseAudio.Music.Silent); return; }

            bool setup = (mainMenu != null && mainMenu.IsShowing)
                         || (castSelect != null && castSelect.IsShowing)
                         || (characterCreator != null && characterCreator.IsShowing)
                         || (seasonReport != null && seasonReport.IsShowing);
            if (setup) { audioBed.SetMusic(HouseAudio.Music.Silent); return; }

            // The opening carries the theme, and the season takes over when it ends.
            bool titles = opening != null && opening.IsPlaying;
            audioBed.SetMusic(titles ? HouseAudio.Music.Theme : HouseAudio.Music.Season);
        }

        /// <summary>Resolves a HUD chrome panel by name, live, for the tour to stand beside.</summary>
        private RectTransform FindChrome(string name) =>
            gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                .FirstOrDefault(rect => rect.name == name && rect.gameObject.activeInHierarchy);

        /// <summary>Who is playing for the veto, badged by what they are defending.</summary>
        private List<CeremonyTakeover.Subject> VetoField(EpisodeState state)
        {
            var field = new List<CeremonyTakeover.Subject>();
            if (state?.vetoPlayers == null) return field;
            foreach (var id in state.vetoPlayers)
            {
                var actor = state.Find(id);
                if (actor == null) continue;
                string badge = actor.id == state.hohId ? "HOH"
                    : state.nominees != null && state.nominees.Contains(actor.id) ? "NOMINATED"
                    : null;
                field.Add(new CeremonyTakeover.Subject(actor.name, badge,
                    CharacterPortraits.Get(
                        CharacterPresentation.AppearanceId(actor, ContentCatalog.CanonicalId(actor.id)))));
            }
            return field;
        }

        /// <summary>
        /// The season so far, grouped by week and read forwards.
        ///
        /// <para>The record was a flat reverse-chronological list of the last thirty-five committed
        /// lines. That is a log, and a log is the right thing for debugging and the wrong thing for
        /// remembering a story: it opens on the most recent line, gives no indication which week
        /// anything belongs to, and reads backwards, so cause follows effect down the page.</para>
        ///
        /// <para>Grouped and forwards, it reads as what happened. Nothing is invented and nothing is
        /// paraphrased — every line is the text the simulation committed, filtered by the same
        /// audience rule as everywhere else.</para>
        /// </summary>
        private void RenderStorySoFar(EpisodeState state)
        {
            hud.Heading("THE STORY SO FAR", UiTheme.Gold);
            hud.Eyebrow("PREVIOUSLY ON BIG BROTHER", UiTheme.Gold);
            hud.Mark(NotebookSection.Story);

            var visible = state.events
                .Where(e => e.audienceIds.Count == 0 || e.audienceIds.Contains(state.playerId))
                // Phase markers are scaffolding for the engine, not events in the story.
                .Where(e => e.kind != "phase")
                .ToList();
            if (visible.Count == 0) { hud.Paragraph("Nothing has happened yet."); return; }

            // The most recent weeks, oldest first inside each. A long season would otherwise push
            // this week off the bottom of a panel that opens at the top.
            const int weeks = 3;
            int newest = visible.Max(e => e.week);
            int oldest = Mathf.Max(1, newest - (weeks - 1));
            if (oldest > 1) hud.Paragraph("Earlier weeks are in the save; the last " + weeks + " are shown here.");

            for (int week = oldest; week <= newest; week++)
            {
                var entries = visible.Where(e => e.week == week).ToList();
                if (entries.Count == 0) continue;
                hud.Heading(week == newest ? "Week " + week + " · this week" : "Week " + week, UiTheme.Gold);
                foreach (var entry in entries) hud.Paragraph(entry.text);
            }
        }

        /// <summary>The notebook's addressable sections, shared with the icon rail.</summary>
        public static class NotebookSection
        {
            public const string Network = "Section · network";
            public const string Rooms = "Section · rooms";
            public const string Votes = "Section · votes";
            public const string Story = "Section · story";
        }

        /// <summary>Opens the notebook, if needed, and scrolls to a section.</summary>
        public void ShowNotebookSection(string section)
        {
            journalOpen = true;
            phaseOpen = false; settingsOpen = false; diaryOpen = false;
            hud.RequestScrollTo(section);
            Render();
        }

        /// <summary>
        /// The visual root of a houseguest whose body is generated at runtime, or null.
        ///
        /// <para>Only generated bodies are offered. An authored prefab photographs better from the
        /// portrait rig — isolated, unlit, framed — than it does standing in a dark house, so there
        /// is nothing to gain by capturing it live and a lit-by-the-room portrait to lose.</para>
        /// </summary>
        public Transform LiveBody(string contestantId)
        {
            if (CharacterBodySource.Provider == null) return null;
            var canonical = ContentCatalog.CanonicalId(contestantId);
            foreach (var visual in gameObject.scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<CharacterPresentation>(true)))
            {
                if (ContentCatalog.CanonicalId(visual.CharacterId) != canonical) continue;
                return visual.transform.Find("Gamesim Character Visual");
            }
            return null;
        }

        private static string NameOf(EpisodeState state, string id) => state?.Find(id)?.name;

        /// <summary>
        /// Everyone who draws a key and keeps it: still playing, not the Head of Household, not on
        /// the block. The HoH does not draw for their own safety.
        /// </summary>
        private List<KeyCeremony.Person> SafeHouseguests(EpisodeState state)
        {
            var people = new List<KeyCeremony.Person>();
            if (state?.contestants == null) return people;
            foreach (var actor in state.contestants)
            {
                if (actor.status != ContestantStatus.Active) continue;
                if (actor.id == state.hohId) continue;
                if (state.nominees != null && state.nominees.Contains(actor.id)) continue;
                people.Add(Person(state, actor.id));
            }
            return people;
        }

        private List<KeyCeremony.Person> NominatedHouseguests(EpisodeState state)
        {
            var people = new List<KeyCeremony.Person>();
            if (state?.nominees == null) return people;
            foreach (var id in state.nominees) if (state.Find(id) != null) people.Add(Person(state, id));
            return people;
        }

        /// <summary>
        /// Whether the player is out of the game and the season is running on without them.
        ///
        /// <para>The web game stores this as a sticky <c>isSpectatorMode</c> flag set when the
        /// evicted houseguest is the player. Here it is derived from the player's status instead,
        /// which is equivalent — a juror never returns to the house — and avoids adding a field to
        /// the save schema and a migration to go with it.</para>
        ///
        /// <para>The finale is excluded: once the season is over everyone is a spectator, and the
        /// report carries its own badge for someone who watched from the jury.</para>
        /// </summary>
        public static bool Spectating(EpisodeState state)
        {
            if (state == null || state.phase == EpisodePhase.Finished) return false;
            var you = state.Find(state.playerId);
            return you != null && you.status != ContestantStatus.Active;
        }

        /// <summary>The line under the spectator caption: when they went, and what is left.</summary>
        private static string SpectatorDetail(EpisodeState state)
        {
            var you = state.Find(state.playerId);
            int week = you != null && you.nominationWeeks != null && you.nominationWeeks.Count > 0
                ? you.nominationWeeks[you.nominationWeeks.Count - 1]
                : state.week;
            string seat = you != null && you.status == ContestantStatus.Jury
                ? "You are on the jury, and you will vote for the winner."
                : "You were evicted before jury, so you have no vote in the finale.";
            return "You were evicted in week " + week + ". " + seat
                + " The house plays on; you can still watch every ceremony and read the notebook.";
        }

        /// <summary>
        /// Opens the season report on the committed season.
        ///
        /// <para>It reads <see cref="Snapshot"/> rather than the projection, for the reason every
        /// other presentation surface here does: a projected result is not a fact, and the last
        /// screen of a season is the worst possible place to show an outcome the save does not
        /// hold.</para>
        /// </summary>
        /// <summary>
        /// Opens the week's recap once the eviction has finished being narrated.
        ///
        /// <para>A season that has just ended does not get one: the finale plays, and
        /// <see cref="SeasonReport"/> is the screen that closes it. A recap in front of the winner
        /// would be a summary of the week interrupting the end of the season.</para>
        /// </summary>
        private void QueueWeeklyRecap(int week)
        {
            if (weeklyRecap == null || week < 1) return;
            if (recapWait != null) StopCoroutine(recapWait);
            recapWait = StartCoroutine(OpenWeeklyRecap(week));
        }

        private IEnumerator OpenWeeklyRecap(int week)
        {
            // Nothing here is timed. It waits on the cards' own state, so reduced motion and
            // batchmode — where those beats collapse to nothing — cost exactly one frame.
            yield return null;
            while ((voteReveal != null && voteReveal.IsPlaying)
                   || (takeover != null && takeover.IsPlaying))
                yield return null;

            recapWait = null;
            var committed = Snapshot;
            if (committed.phase == EpisodePhase.Finished) yield break;
            if (seasonReport != null && seasonReport.IsShowing) yield break;
            // ClosePanels is what dismissing does: it puts the player back in the house and
            // repaints, which hiding the screen on its own would not.
            OpenRecap(() => weeklyRecap.Show(committed, ClosePanels));
        }

        /// <summary>
        /// Puts the recap up the way every other full-screen panel goes up.
        ///
        /// <para>Taking the house away is explicit here, not a consequence of rendering: a render
        /// redraws the HUD and nothing else, and the one call that gates movement on
        /// <see cref="IsPanelOpen"/> lives in <c>Project</c>, which only runs when a command
        /// commits. Showing a scrim without this leaves the player walking around behind it.</para>
        /// </summary>
        private void OpenRecap(Action show)
        {
            PauseNpcSocialForPanel();
            // Without a render: the panel being cleared is replaced in the same breath, and it
            // also hides the recap, so a repaint here would draw a screen about to be reopened.
            ClosePanelsInternal(false);
            show();
            if (player != null) player.SetInputEnabled(false);
            if (cameraRig != null) cameraRig.ControlsEnabled = false;
            Render();
        }

        /// <summary>Whether the week's recap is on screen. Read by the HUD's own open-panel test.</summary>
        public bool IsWeeklyRecapOpen => weeklyRecap != null && weeklyRecap.IsOpen;

        /// <summary>
        /// Opens a played week's recap for review. The player's own choice, not a beat.
        ///
        /// <para>Dismissing puts them back where they came from. Reached from the notebook it
        /// reopens the notebook, because a control that closes the screen it was pressed on makes
        /// the player navigate back to it every time they check a second week.</para>
        /// </summary>
        public void ReviewWeek(int week)
        {
            if (weeklyRecap == null) return;
            bool fromNotebook = journalOpen;
            var committed = Snapshot;
            OpenRecap(() => weeklyRecap.Review(committed, week,
                fromNotebook ? (Action)OpenJournal : ClosePanels));
        }

        public void ShowSeasonReport()
        {
            if (seasonReport == null) return;
            var committed = Snapshot;
            seasonReport.Show(committed, id =>
            {
                var actor = committed.Find(id);
                return actor == null ? null : CharacterPortraits.Get(
                    CharacterPresentation.AppearanceId(actor, ContentCatalog.CanonicalId(actor.id)));
            }, OpenJournal);
        }

        private static KeyCeremony.Person Person(EpisodeState state, string id)
        {
            var actor = state.Find(id);
            return new KeyCeremony.Person(actor.id, actor.name,
                CharacterPortraits.Get(
                    CharacterPresentation.AppearanceId(actor, ContentCatalog.CanonicalId(actor.id))));
        }

        /// <summary>The two people on the block, with their faces, for the eviction reveal.</summary>
        private List<VoteReveal.Nominee> EvictionBlock(EpisodeState state)
        {
            var block = new List<VoteReveal.Nominee>();
            if (state?.nominees == null) return block;
            foreach (var id in state.nominees)
            {
                var actor = state.Find(id);
                if (actor == null) continue;
                block.Add(new VoteReveal.Nominee(actor.id, actor.name,
                    CharacterPortraits.Get(
                        CharacterPresentation.AppearanceId(actor, ContentCatalog.CanonicalId(actor.id)))));
            }
            return block;
        }

        /// <summary>
        /// The committed ballots, in the order the house cast them.
        ///
        /// <para>Read from state rather than re-derived, so the card counts to the same total the
        /// save holds. It carries who voted and for whom — both already public at the reveal, which
        /// is the moment the engine logs them as <c>vote-reveal</c> events.</para>
        /// </summary>
        private static List<VoteReveal.Ballot> EvictionBallots(EpisodeState state)
        {
            var ballots = new List<VoteReveal.Ballot>();
            if (state?.votes == null) return ballots;
            foreach (var vote in state.votes)
            {
                var voter = state.Find(vote.voterId);
                ballots.Add(new VoteReveal.Ballot(voter?.name ?? "A housemate", vote.targetId));
            }
            return ballots;
        }

        /// <summary>Whoever stopped being active during this commit, or null.</summary>
        private static string EvictedThisCommit(EpisodeState state, HashSet<string> wasActive)
        {
            if (state?.contestants == null || wasActive == null) return null;
            foreach (var actor in state.contestants)
                if (actor.status != ContestantStatus.Active && wasActive.Contains(actor.id)) return actor.id;
            return null;
        }

        private void Commit(EpisodeState origin, EpisodeCommandKind kind, string target = null, string second = null, bool veto = false, double performance = 0, string text = null)
        {
            if (!IsCurrentDiaryRevision(origin)) return;
            Submit(new EpisodeCommand { id = Guid.NewGuid().ToString("N"), actorId = origin.playerId, expectedPhase = origin.phase,
                expectedRevision = origin.revision, kind = kind, targetId = target, secondTargetId = second, useVeto = veto, performance = performance, text = text });
        }

        public void ContinueEpisode() { if (phaseOpen) Commit(projected, EpisodeCommandKind.Advance); }
        public void SimulateCompetition() => SimulateCompetition(projected);
        private void SimulateCompetition(EpisodeState state)
        {
            if (!phaseOpen || challengeActive || !IsCurrentDiaryRevision(state) || state.competitionResolved
                || (state.phase != EpisodePhase.HoH && state.phase != EpisodePhase.Veto)
                || !EpisodeEngine.CompetitionPlayers(state).Any(contestant => contestant.isPlayer)) return;
            Commit(state, EpisodeCommandKind.SimulateCompetition);
        }
        /// <summary>
        /// Enters the competition at the floor.
        ///
        /// <para>A throw is a <see cref="EpisodeCommandKind.Compete"/> with no precision rather than
        /// a command of its own: the houseguest does compete, their stats still count, and the
        /// result is as binding as any other. Modelling it as a refusal to enter would have made it
        /// a way to opt out of a committed result, which is exactly what it is not.</para>
        /// </summary>
        public void ThrowCompetition() => ThrowCompetition(projected);
        private void ThrowCompetition(EpisodeState state)
        {
            if (!phaseOpen || challengeActive || !IsCurrentDiaryRevision(state) || state.competitionResolved
                || !EpisodeEngine.CompetitionPlayers(state).Any(contestant => contestant.isPlayer)) return;
            Commit(state, EpisodeCommandKind.Compete, performance: 0d);
        }

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


        private void StartChallenge(EpisodeState state)
        {
            challengeOrigin = state; challengeActive = true; challengeHits = 0; challengeTotal = 0; challengeStarted = Time.unscaledTime;
            var kind = CompetitionMiniGames.For(EpisodeEngine.CompetitionCategory(state.phase, state.week));
            // The board's shuffle and the targets' placement come from a generator this run owns,
            // seeded from the wall clock rather than from the season. A minigame's draws are not
            // part of the committed command, so spending the season's randomState on them would
            // re-roll everything that follows — the same reason the cast is not shuffled.
            challengeRun = kind == CompetitionMiniGames.Kind.Precision
                ? null
                : new MiniGameRun(kind, (uint)Environment.TickCount);
            audioBed.PlayCue(HouseAudio.Cue.CompetitionStart); Render();
        }

        /// <summary>
        /// Advances the running minigame and commits it when it ends.
        ///
        /// <para>Input is read here rather than from the panel's buttons because two of the three
        /// are held or timed: an endurance grip has to know the frame the key came up, and a
        /// reaction tap is only worth anything while a target is live. The panel still carries
        /// equivalent controls, so nothing here is keyboard-only.</para>
        /// </summary>
        private void TickMiniGame()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (challengeRun.Kind == CompetitionMiniGames.Kind.Endurance)
                    challengeRun.SetHolding(keyboard.spaceKey.isPressed);
                else if (challengeRun.Kind == CompetitionMiniGames.Kind.Reaction
                         && keyboard.spaceKey.wasPressedThisFrame)
                    TapTarget();
            }

            // What the panel currently says, before the frame moves anything.
            var was = PanelShape();
            challengeRun.Tick(Time.unscaledDeltaTime);

            // The meter updates in place, every frame. A full Render rebuilds the panel's controls,
            // so doing it per frame would destroy and recreate the buttons sixty times a second —
            // which is both wasteful and a good way to swallow the click that is being made on one.
            hud.SetChallenge((float)MiniGameMeter(), challengeRun.Kind == CompetitionMiniGames.Kind.Reaction
                ? challengeRun.Hits : challengeRun.MatchedPairs);

            if (challengeRun.Finished) { CommitMiniGame(); return; }
            // Redraw only when the panel would actually read differently: a target appearing or
            // going, a grip taken or let go, a pair turning back over.
            if (PanelShape() != was) Render();
        }

        /// <summary>
        /// A signature of everything the panel's words and controls depend on.
        ///
        /// <para>Deliberately not the clock: a countdown that ticked in text would force a rebuild
        /// every frame for the sake of one number, and the meter already carries the same
        /// information without one.</para>
        /// </summary>
        private string PanelShape()
        {
            if (challengeRun == null) return "none";
            switch (challengeRun.Kind)
            {
                case CompetitionMiniGames.Kind.Endurance:
                    return "hold:" + challengeRun.Holding;
                case CompetitionMiniGames.Kind.Reaction:
                    return "target:" + challengeRun.TargetLive + ":" + challengeRun.Hits + "/" + challengeRun.Spawned;
                default:
                    return "board:" + challengeRun.MatchedPairs + ":" + challengeRun.WrongFlips
                           + ":" + challengeRun.FirstFlip + ":" + challengeRun.SecondFlip;
            }
        }

        /// <summary>What the HUD's single meter shows, which differs per game.</summary>
        private double MiniGameMeter()
        {
            switch (challengeRun.Kind)
            {
                case CompetitionMiniGames.Kind.Endurance:
                    return challengeRun.Meter / CompetitionMiniGames.MeterFull;
                case CompetitionMiniGames.Kind.Memory:
                    return challengeRun.Pairs == 0 ? 0 : (double)challengeRun.MatchedPairs / challengeRun.Pairs;
                default:
                    return challengeRun.TimeLimit <= 0 ? 0 : challengeRun.Remaining / challengeRun.TimeLimit;
            }
        }

        /// <summary>Hitting the target that is up, if one is. Harmless when none is.</summary>
        public void TapTarget()
        {
            if (!challengeActive || challengeRun == null) return;
            if (challengeRun.Tap()) audioBed.PlayCue(HouseAudio.Cue.Button);
        }

        /// <summary>
        /// Turning a card over.
        ///
        /// <para>This one redraws immediately rather than waiting for the next tick, because the
        /// card the player just pressed has to show its face before they look away from it.</para>
        /// </summary>
        public void FlipCard(int index)
        {
            if (!challengeActive || challengeRun == null) return;
            if (challengeRun.Flip(index)) audioBed.PlayCue(HouseAudio.Cue.Button);
            if (challengeRun.Finished) CommitMiniGame(); else Render();
        }

        private void CommitMiniGame()
        {
            var run = challengeRun;
            challengeRun = null;
            challengeActive = false;
            Commit(challengeOrigin, EpisodeCommandKind.Compete, performance: run.Performance);
        }
        /// <summary>
        /// The deal table for one houseguest: what they have put to you, and what you can put to them.
        ///
        /// <para>The chance is drawn as a tag rather than folded into the caption, for the same
        /// reason the action category is: the words on a button are how tests and screen readers
        /// find it, and a number that moves every time the relationship does would make the control
        /// unfindable. Showing it at all is the reference's choice — it puts the odds on the screen
        /// rather than making the player guess.</para>
        /// </summary>
        private void DealPanel(EpisodeState state, ContestantState npc)
        {
            var waiting = NpcDeals.Pending(state).Where(d => d.proposerId == npc.id).ToList();
            foreach (var offer in waiting)
            {
                string id = offer.id;
                hud.Heading("AN OFFER FROM " + npc.name.ToUpperInvariant());
                hud.Paragraph(DealSentence(state, offer));
                hud.Tag(hud.ActionFor(id, EpisodeHud.DealAcceptCaption,
                        () => Commit(state, EpisodeCommandKind.RespondToDeal, id, text: EpisodeEngine.AcceptDeal)),
                    Category(EpisodeCommandKind.RespondToDeal));
                hud.ActionFor(id, EpisodeHud.DealDeclineCaption,
                    () => Commit(state, EpisodeCommandKind.RespondToDeal, id, text: "decline"));
            }

            var offers = PlayerDeals.Available(state, npc.id);
            if (offers.Count == 0) return;
            hud.Heading("WHAT YOU COULD PUT TO " + npc.name.ToUpperInvariant());
            foreach (string type in offers)
            {
                string kind = type;
                // A target agreement is about a third person, so it is offered per subject rather
                // than as one control that would have to ask "about whom?" after being clicked —
                // the same shape the vent and lie controls already use.
                if (kind == DealKind.TargetAgreement)
                {
                    foreach (var subject in state.Active.Where(c => !c.isPlayer && c.id != npc.id))
                    {
                        string about = subject.id;
                        if (!PlayerDeals.CanPropose(state, npc.id, kind, about, out _)) continue;
                        hud.Tag(hud.ActionFor(about, EpisodeHud.DealProposeCaption(
                                    DealKind.Title(kind).ToLowerInvariant() + " against " + subject.name),
                                () => Commit(state, EpisodeCommandKind.ProposeDeal, npc.id, about, text: kind)),
                            Category(EpisodeCommandKind.ProposeDeal) + " · " + Chance(state, npc.id, kind, about));
                    }
                    continue;
                }
                hud.Tag(hud.ActionFor(kind, EpisodeHud.DealProposeCaption(DealKind.Title(kind).ToLowerInvariant()),
                        () => Commit(state, EpisodeCommandKind.ProposeDeal, npc.id, text: kind)),
                    Category(EpisodeCommandKind.ProposeDeal) + " · " + Chance(state, npc.id, kind, null));
            }
        }

        /// <summary>
        /// "about even" rather than "51%", so the chip reads as a judgement and not a promise.
        ///
        /// <para>It rides beside the action category rather than replacing it — every other control
        /// in this panel says what kind of move it is, and a deal button should not be the one that
        /// stops. And it stays out of the caption, because the caption is how a test and a screen
        /// reader find the button, and a number that moves with the relationship would make it
        /// unfindable.</para>
        /// </summary>
        private static string Chance(EpisodeState state, string npcId, string type, string about)
        {
            double chance = PlayerDeals.AcceptanceChance(state, npcId, type, about);
            if (chance >= 75) return "likely";
            if (chance >= 55) return "favourable";
            if (chance >= 45) return "about even";
            if (chance >= 25) return "a stretch";
            return "unlikely";
        }

        /// <summary>What the houseguest is actually asking for, in words.</summary>
        private static string DealSentence(EpisodeState state, DealState offer)
        {
            string who = state.Find(offer.proposerId)?.name ?? "They";
            string about = offer.targetId == null ? null : state.Find(offer.targetId)?.name;
            switch (offer.type)
            {
                case DealKind.VetoUse:
                    return who + " is on the block and wants your word that you will use the veto on them.";
                case DealKind.VoteSave:
                    return who + " wants your vote to keep " + (about ?? "them") + " in the house this week.";
                case DealKind.VoteEvict:
                    return who + " wants your vote against " + (about ?? "the other nominee") + " this week.";
                case DealKind.VoteTogether:
                    return who + " wants the two of you to vote as a block this week.";
                case DealKind.TargetAgreement:
                    return who + " thinks " + (about ?? "somebody") + " is getting too strong and wants to work together on it.";
                case DealKind.SafetyAgreement:
                    return who + " is proposing that neither of you puts the other up.";
                case DealKind.InformationSharing:
                    return who + " wants to trade whatever the two of you hear around the house.";
                case DealKind.FinalTwo:
                    return who + " wants to sit beside you at the end.";
                case DealKind.AllianceInvite:
                    return who + " thinks it is time the two of you made it official.";
                default:
                    return who + " wants to partner up properly.";
            }
        }

        private void ChallengePanel()
        {
            if (challengeRun == null)
            {
                hud.Paragraph("Press Space or STOP when the marker is near the center. Three attempts; no time limit. Escape cancels without committing.");
                hud.ChallengeMeter(); hud.Action("STOP marker  [Space]", RecordChallengeHit);
                return;
            }

            hud.Paragraph(CompetitionMiniGames.Brief(challengeRun.Kind)
                + "  You have " + challengeRun.TimeLimit.ToString("0")
                + " seconds. Escape cancels without committing.");
            hud.ChallengeMeter();

            switch (challengeRun.Kind)
            {
                case CompetitionMiniGames.Kind.Endurance:
                    hud.Paragraph("Grip: " + challengeRun.Meter.ToString("0")
                        + "%  ·  held " + challengeRun.Held.ToString("0.0") + "s of "
                        + challengeRun.TimeLimit.ToString("0") + "s");
                    // Holding is a key, so the button is a toggle rather than a second way to hold:
                    // a control you have to keep the mouse down on is not usable with a keyboard,
                    // a switch, or one hand.
                    hud.Action(challengeRun.Holding ? EpisodeHud.ReleaseGripCaption : EpisodeHud.HoldGripCaption,
                        () => challengeRun?.SetHolding(!challengeRun.Holding));
                    break;

                case CompetitionMiniGames.Kind.Reaction:
                    hud.Paragraph(challengeRun.TargetLive
                        ? "A target is up. Hit it."
                        : "Wait for the next target.");
                    hud.Paragraph("Hit " + challengeRun.Hits + " of " + challengeRun.Spawned + ".");
                    hud.Action(EpisodeHud.TapTargetCaption, TapTarget);
                    break;

                case CompetitionMiniGames.Kind.Memory:
                    hud.Paragraph("Matched " + challengeRun.MatchedPairs + " of " + challengeRun.Pairs
                        + (challengeRun.WrongFlips > 0 ? "  ·  " + challengeRun.WrongFlips + " wrong flips" : ""));
                    MemoryBoard();
                    break;
            }
        }

        /// <summary>
        /// The board, as one control per card.
        ///
        /// <para>A card says what it is when it is face up and says so in words — "Card 3: star" —
        /// rather than only in colour. The screen is the only place the board exists, so a player
        /// using a screen reader has no other way to know what they just turned over.</para>
        /// </summary>
        private void MemoryBoard()
        {
            for (int index = 0; index < challengeRun.Faces.Count; index++)
            {
                int card = index;
                bool up = challengeRun.Matched[card] || card == challengeRun.FirstFlip
                          || card == challengeRun.SecondFlip;
                var button = hud.Action(EpisodeHud.CardCaption(card, up ? MemoryFace(challengeRun.Faces[card]) : null),
                    () => FlipCard(card));
                if (button != null) button.interactable = !challengeRun.Matched[card];
                if (challengeRun.Matched[card]) hud.Tag(button, "matched");
            }
        }

        /// <summary>A name per face, so a card reads as something rather than as an index.</summary>
        private static string MemoryFace(int face)
        {
            string[] names = { "star", "key", "crown", "eye", "flame", "anchor", "clover", "moon" };
            return face >= 0 && face < names.Length ? names[face] : "symbol " + face;
        }
        public void RecordChallengeHit()
        {
            if (!challengeActive) return;
            challengeTotal += Math.Max(0, 1 - Math.Abs(challengeValue - .5) * 2); challengeHits++;
            audioBed.PlayCue(HouseAudio.Cue.Button);
            if (challengeHits < 3) return;
            challengeActive = false; Commit(challengeOrigin, EpisodeCommandKind.Compete, performance:challengeTotal / 3);
        }

        private void Settings(EpisodeState state)
        {
            hud.PanelTitle(blockedRecovery ? "SAVE RECOVERY" : "SETTINGS & SAVES", "Offline play is available. No credentials or online connection are required.");
            hud.Paragraph("Slot: " + saves.SavePath);
            hud.Paragraph(message);
            hud.Action("Save now  [F5]", SaveNow);
            hud.Action("Reload current slot", LoadNow);
            hud.Action("Recover validated backup (preserve current file)", RecoverBackup);
            hud.Action("New season in a NEW slot (preserves this season)", NewSeason);
            hud.Action("Main menu", OpenMainMenu);
            hud.Meter("Master volume", volumePercent, 100, muted ? UiTheme.Muted : UiTheme.Accent);
            hud.Action("Volume down", () => { volumePercent = Mathf.Max(0, volumePercent - 10); ApplyPreferences(); Render(); });
            hud.Action("Volume up", () => { volumePercent = Mathf.Min(100, volumePercent + 10); ApplyPreferences(); Render(); });
            hud.Action(muted ? "Turn sound on" : "Mute sound", () => { muted = !muted; ApplyPreferences(); Render(); });
            hud.Action(musicOn ? "Turn music off" : "Turn music on", () => { musicOn = !musicOn; ApplyPreferences(); Render(); });
            hud.Action(reducedMotion ? "Enable character motion" : "Reduce character motion", () => { reducedMotion = !reducedMotion; ApplyPreferences(); Render(); });
            hud.Action(largeText ? "Use standard text" : "Use larger text", () => { largeText = !largeText; ApplyPreferences(); Render(); });
            hud.Paragraph("All dialogue and ceremony information is captioned. Mouse buttons and keyboard alternatives are available; precision competitions have an untimed assisted option.");
            hud.Heading("Import a supported web save");
            hud.Paragraph("Supports receipt-free, six-active-cast social snapshots. Complex in-progress web saves are rejected and archived unchanged, never silently simplified.");
            hud.PathInput("Full path to exported JSON", ImportFile);
            hud.Paragraph("Optional cloud login and generated AI dialogue are not configured. The local episode never waits for those services.");
        }

        public void SaveNow()
        {
            if (!blockedRecovery && !durableCommitInProgress)
            {
                // An explicit retry can save an unchanged session after an I/O failure.
                // A NEW failed tick must never fall through to a second, older write.
                if (npcSaveSuspended) TryAutosave();
                else if (FlushNpcWholeTicks() && !blockedRecovery && !npcSaveSuspended) TryAutosave();
            }
            Render();
        }
        private void TryAutosave()
        {
            if (TryPersistCandidate(engine.Snapshot, out _, out var failure)) message += "  ·  Saved locally.";
            else { message = failure; PauseNpcSocialForPanel(); }
        }
        public void LoadNow()
        {
            if (durableCommitInProgress) return;
            SuspendNpcWorldWithoutSaving();
            if (saves.TryLoad(out var state, out var result)) Install(state);
            message = result; Render();
        }
        public void RecoverBackup()
        {
            if (durableCommitInProgress) return;
            SuspendNpcWorldWithoutSaving();
            if (saves.TryRecoverBackup(out var state, out var result)) Install(state);
            message = result; Render();
        }
        /// <summary>Whether there is a season on screen worth going back to from the menu.</summary>
        public bool SeasonInProgress => engine != null && !blockedRecovery;

        /// <summary>
        /// Opens the front door.
        ///
        /// <para>Continue is offered from the <b>disk</b>, not from the fact that a season object
        /// exists: the director always has one in memory — it builds the authored scenario before it
        /// looks for a save — so asking the engine would offer "continue" on a fresh install and
        /// then continue a season the player never played.</para>
        /// </summary>
        public void OpenMainMenu()
        {
            if (mainMenu == null || durableCommitInProgress) return;
            PauseNpcSocialForPanel();
            ClosePanelsInternal(false);
            if (player != null) player.SetInputEnabled(false);
            if (cameraRig != null) cameraRig.ControlsEnabled = false;
            bool canContinue = saves != null && (File.Exists(saves.SavePath) || File.Exists(saves.BackupPath));
            mainMenu.Show(canContinue, blockedRecovery ? message : null,
                CloseMainMenu, NewSeason, OpenSettingsFromMenu, QuitGame);
            Render();
        }

        /// <summary>Leaves the menu and hands the house back. Only reachable with a season running.</summary>
        public void CloseMainMenu()
        {
            if (mainMenu == null) return;
            mainMenu.Hide();
            ClosePanels();
            // Deferred from bootstrap: the opening's tour points at HUD chrome, and pointing at it
            // from behind a full-screen menu would have been a tour of something nobody could see.
            PlayOpening();
        }

        private void OpenSettingsFromMenu()
        {
            if (mainMenu != null) mainMenu.Hide();
            OpenSettings();
        }

        /// <summary>
        /// Leaves the game.
        ///
        /// <para>In the editor this stops play instead of closing the application, because
        /// <see cref="Application.Quit"/> does nothing there and a Quit button that visibly does
        /// nothing is indistinguishable from a broken one.</para>
        /// </summary>
        public void QuitGame()
        {
            SuspendNpcWorldWithoutSaving();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Opens the cast screen. Nothing is created or written until the player commits there, so
        /// backing out leaves the running season and its slot exactly as they were.
        /// </summary>
        public void NewSeason()
        {
            if (durableCommitInProgress) return;
            if (castSelect == null) { StartSeason(null); return; }
            // The cast screen draws below the menu, so the menu has to step aside rather than sit on
            // top of it — and backing out has to land wherever the player came from, which is the
            // menu when they arrived through it and the settings panel when they did not.
            bool fromMenu = mainMenu != null && mainMenu.IsShowing;
            if (fromMenu) mainMenu.Hide();
            Action back = fromMenu ? (Action)OpenMainMenu : Render;
            castSelect.Show(StartSeason, back, characterCreator == null ? (Action<SeasonBuilder.Choice, CharacterDraft>)null
                : (choice, draft) => characterCreator.Show(choice, draft, StartSeason, castSelect.Resume));
        }

        /// <summary>
        /// Stages and installs a season. A null choice builds the authored six-person scenario,
        /// which is what a headless caller and the pre-cast-screen behaviour both get.
        /// </summary>
        public void StartSeason(SeasonBuilder.Choice choice)
        {
            if (durableCommitInProgress) return;
            SuspendNpcWorldWithoutSaving();
            try
            {
                var seed = unchecked((uint)DateTime.UtcNow.Ticks);
                var nextStore = new EpisodeSaveStore(Path.Combine(saveRoot, "episode-" + Guid.NewGuid().ToString("N") + ".json"));
                var fresh = choice == null ? ContentCatalog.Create(seed) : SeasonBuilder.Create(choice, seed);
                fresh.sessionId = Guid.NewGuid().ToString("N");
                nextStore.Save(fresh); // Stage and validate on disk before replacing the current in-memory session.
                saves = nextStore; Install(fresh);
                if (SaveRootOverride == null) { PlayerPrefs.SetString("Gamesim.ActiveSave", Path.GetFileName(saves.SavePath)); PlayerPrefs.Save(); }
                message = SeasonMessage(fresh, choice);
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException)
            { message = "New season could not be saved. Your current session and slot were preserved. " + error.Message; }
            Render();
        }

        private static string SeasonMessage(EpisodeState fresh, SeasonBuilder.Choice choice)
        {
            string head = "New season started in a new slot. Previous saves were retained.";
            if (choice == null) return head;
            var you = fresh.Find(fresh.playerId);
            return head + "  ·  " + fresh.contestants.Count + " houseguests, "
                   + CastTemplates.RosterName(choice.Roster).ToLowerInvariant()
                   + (you != null && choice.Authored != null
                       ? ". You built " + you.name + "."
                       : you != null && !string.IsNullOrEmpty(choice.PlayerTemplateId)
                           ? ". You are playing as " + you.name + "." : ".");
        }
        public void ImportFile(string path)
        {
            if (durableCommitInProgress) return;
            SuspendNpcWorldWithoutSaving();
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > 8 * 1024 * 1024) { message = "Choose an existing JSON file smaller than eight MiB."; Render(); return; }
                if (WebSaveImporter.TryImport(File.ReadAllText(path), Path.Combine(saveRoot,"Imports"), out var state, out var result))
                {
                    var importedSlot = new EpisodeSaveStore(Path.Combine(saveRoot,"import-" + Guid.NewGuid().ToString("N") + ".json"));
                    importedSlot.Save(state); saves = importedSlot; Install(state);
                    if (SaveRootOverride == null) { PlayerPrefs.SetString("Gamesim.ActiveSave", Path.GetFileName(saves.SavePath)); PlayerPrefs.Save(); }
                }
                message = result;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException) { message = "Import did not change your current slot: " + error.Message; }
            Render();
        }
        private void Install(EpisodeState state)
        {
            ResetNpcSocialForLoad();
            ClosePanels(); engine = new EpisodeEngine(state); blockedRecovery = false;
            // Before the placement loop below, which indexes the anchor arrays by body: the season
            // being installed can hold a different house from the one on screen, and until seasons
            // could differ in size this loop was indexing an array that always happened to match.
            SeatCast(state);
            // Load-time authored placement only, never a travel/pathfinding fallback.
            // Pending conversations walk back to their saved venue without new draws.
            if (initialNpcPositions != null)
                for (int i = 0; i < housemates.Length; i++)
                    if (housemates[i] != null) housemates[i].transform.SetPositionAndRotation(initialNpcPositions[i], initialNpcRotations[i]);
            if (player.Agent.isOnNavMesh) { player.Agent.Warp(initialPlayerPosition); player.Agent.ResetPath(); }
            Project();
        }
        private void ApplyPreferences()
        {
            audioBed?.SetMuted(muted);
            audioBed?.SetVolume(volumePercent / 100f);
            audioBed?.SetAmbienceEnabled(musicOn);
            ApplyMusic();
            cameraRig?.SetReducedMotion(reducedMotion);
            if (hud != null) { hud.FontScale = largeText ? 1.2f : 1; hud.ReducedMotion = reducedMotion; }
            if (sting != null) sting.FontScale = largeText ? 1.2f : 1;
            if (takeover != null) takeover.FontScale = largeText ? 1.2f : 1;
            if (voteReveal != null) voteReveal.FontScale = largeText ? 1.2f : 1;
            if (competitionCard != null) competitionCard.FontScale = largeText ? 1.2f : 1;
            if (keyCeremony != null) keyCeremony.FontScale = largeText ? 1.2f : 1;
            if (tutorial != null) tutorial.FontScale = largeText ? 1.2f : 1;
            if (opening != null) opening.FontScale = largeText ? 1.2f : 1;
            if (seasonReport != null) seasonReport.FontScale = largeText ? 1.2f : 1;
            if (castSelect != null) castSelect.FontScale = largeText ? 1.2f : 1;
            if (characterCreator != null) characterCreator.FontScale = largeText ? 1.2f : 1;
            if (mainMenu != null) mainMenu.FontScale = largeText ? 1.2f : 1;
            foreach (var visual in FindObjectsByType<CharacterPresentation>()) visual.SetReducedMotion(reducedMotion);
            if (SaveRootOverride == null)
            { PlayerPrefs.SetInt("Gamesim.Muted", muted ? 1 : 0); PlayerPrefs.SetInt("Gamesim.ReducedMotion", reducedMotion ? 1 : 0); PlayerPrefs.SetInt("Gamesim.LargeText", largeText ? 1 : 0); PlayerPrefs.SetInt("Gamesim.Volume", volumePercent); PlayerPrefs.SetInt("Gamesim.Music", musicOn ? 1 : 0); PlayerPrefs.Save(); }
        }

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
