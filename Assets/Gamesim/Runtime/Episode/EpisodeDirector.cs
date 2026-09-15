using System;
using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
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
        private HouseAudio audioBed;
        private HouseNpc focusedNpc;
        private string saveRoot, message = "Welcome home. Meet the housemates, then visit the living-room screen.";
        private bool blockedRecovery, reducedMotion, muted, largeText, phaseOpen, settingsOpen, journalOpen;
        private bool challengeActive;
        private int challengeHits;
        private double challengeTotal;
        private float challengeStarted, challengeValue, frameAverage;
        private EpisodeState challengeOrigin;
        private Vector3 initialPlayerPosition;
        private Vector3[] initialNpcPositions;
        private Quaternion[] initialNpcRotations;
        private readonly RaycastHit[] sightHits = new RaycastHit[32];
        private bool playerIsActive;
        private HouseNpc promptNpc;
        private string npcPrompt;
        private EpisodeCommandKind? lastSocialAction;
        public EpisodeState Snapshot => engine?.Snapshot;
        public bool IsReady { get; private set; }
        public bool IsPanelOpen => blockedRecovery || focusedNpc != null || phaseOpen || settingsOpen || journalOpen || diaryOpen;
        public bool IsChallengeActive => challengeActive;
        public float AverageFrameMilliseconds => frameAverage * 1000;
        public string SavePath => saves?.SavePath;
        public string StatusMessage => message;

        public void Configure(HousePlayerController controller, HouseCameraRig rig, HouseNpc[] npcs)
        { player = controller; cameraRig = rig; housemates = npcs; }

        private IEnumerator Start()
        {
            yield return null; // Surface and player navigation initialize before any saved episode is installed.
            if (player == null || cameraRig == null || housemates == null || housemates.Length != 5 || !player.Agent.isOnNavMesh)
            { Debug.LogError("Gamesim episode could not initialize its house wiring."); yield break; }
            initialPlayerPosition = player.transform.position;
            initialNpcPositions = housemates.Select(npc => npc.transform.position).ToArray();
            initialNpcRotations = housemates.Select(npc => npc.transform.rotation).ToArray();
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
            audioBed = HouseAudio.Attach(gameObject);
            reducedMotion = SaveRootOverride == null && PlayerPrefs.GetInt("Gamesim.ReducedMotion", 0) == 1;
            muted = SaveRootOverride == null && PlayerPrefs.GetInt("Gamesim.Muted", 0) == 1;
            largeText = SaveRootOverride == null && PlayerPrefs.GetInt("Gamesim.LargeText", 0) == 1;
            hud = gameObject.AddComponent<EpisodeHud>(); hud.Initialize(this);
            ApplyPreferences(); Project(); Render(); IsReady = true;
            Debug.Log("Gamesim episode ready: six contestants, validated simulation, local recovery and accessible HUD connected.");
        }

        private void Update()
        {
            if (!IsReady) return;
            TickNpcSocialRuntime(Time.unscaledDeltaTime);
            frameAverage = Mathf.Lerp(frameAverage, Time.unscaledDeltaTime, 0.03f);
            if (diaryOpen && !CanUseDiary) { ClosePanels(); return; }
            if (challengeActive)
            {
                challengeValue = Mathf.PingPong((Time.unscaledTime - challengeStarted) * 0.75f, 1f);
                hud.SetChallenge(challengeValue, challengeHits);
                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) RecordChallengeHit();
            }
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { ClosePanels(); return; }
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
            focusedNpc = null; phaseOpen = false; settingsOpen = false; journalOpen = false; challengeActive = false;
            diaryOpen = false; diaryDraft = null;
            lastSocialAction = null;
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
            message = result.accepted ? result.state.events.LastOrDefault(e => e.audienceIds.Count == 0 || e.audienceIds.Contains(result.state.playerId))?.text ?? "Decision committed." : result.reason;
            if (result.accepted)
            {
                diaryDraft = null; // A draft never survives a different committed revision.
                if (focusedNpc != null) lastSocialAction = command.kind;
                message += "  ·  Saved locally."; Project();
                if (phaseOpen && wasYard != EpisodeEngine.IsCompetition(result.state.phase)) ClosePanels();
                var kind = result.state.events.LastOrDefault()?.kind;
                audioBed.PlayCue(kind == "winner" ? HouseAudio.Cue.Finale : kind == "eviction" ? HouseAudio.Cue.Eviction :
                    kind == "competition" ? HouseAudio.Cue.CompetitionWin : kind == "nomination" ? HouseAudio.Cue.Nomination :
                    kind == "veto" ? HouseAudio.Cue.Veto : HouseAudio.Cue.Button);
            }
            Render(); return result;
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
        public void SkipQuestioning() { if (phaseOpen) Commit(projected, EpisodeCommandKind.SkipQuestioning); }
        public void SubmitSpeech(string text) { if (phaseOpen) Commit(projected, EpisodeCommandKind.SubmitSpeech, text: text); }
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
            var state = projected ?? engine.Snapshot;
            hud.Begin(state, message, blockedRecovery, phaseOpen || focusedNpc != null || settingsOpen || journalOpen || diaryOpen);
            if (settingsOpen || blockedRecovery) { Settings(state); return; }
            if (diaryOpen) { RenderDiary(state); return; }
            if (journalOpen)
            {
                hud.PanelTitle("YOUR NOTEBOOK", "Private information is limited to what your character knows.");
                hud.Paragraph("Relationships are your own perspective; another housemate may feel differently.");
                foreach (var c in state.contestants.Where(c => !c.isPlayer)) hud.Paragraph(c.name + " · " + c.status + " · Your trust " + state.Score(state.playerId, c.id).ToString("0"));
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
                hud.Heading("Episode record");
                foreach (var entry in state.events.Where(e => e.audienceIds.Count == 0 || e.audienceIds.Contains(state.playerId)).Reverse().Take(35)) hud.Paragraph(entry.text);
                return;
            }
            if (focusedNpc != null)
            {
                var npc = state.Find(focusedNpc.Id);
                hud.PanelTitle(npc.name.ToUpperInvariant(), npc.pronouns + " · " + string.Join(" / ", npc.traits));
                hud.NpcDialogue(state, npc.id, lastSocialAction);
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
                hud.Action("Spend time together", () => Commit(state, EpisodeCommandKind.Talk, npc.id));
                hud.Action("Promise safety", () => Commit(state, EpisodeCommandKind.PromiseSafety, npc.id));
                hud.Action("Propose a final-two promise", () => Commit(state, EpisodeCommandKind.PromiseFinalTwo, npc.id));
                hud.Action(state.Allied(state.playerId, npc.id) ? "Leave our alliance" : "Propose an alliance",
                    () => Commit(state, state.Allied(state.playerId, npc.id) ? EpisodeCommandKind.LeaveAlliance : EpisodeCommandKind.FormAlliance, npc.id));
                hud.Action("Share something I know", () => Commit(state, EpisodeCommandKind.ShareInformation, npc.id));
                if (state.phase == EpisodePhase.Campaign)
                    foreach (var nominee in state.nominees) { string id = nominee; hud.Action("Promise to evict " + state.Find(id).name, () => Commit(state, EpisodeCommandKind.PromiseVote, npc.id, id)); }
                return;
            }
            if (!phaseOpen) return;
            hud.PanelTitle(PhaseTitle(state.phase), "WEEK " + state.week + " · "
                + (state.phase == EpisodePhase.Finished ? "Season complete" : state.Active.Count() + " houseguests remain"));
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
                hud.Action("Review the season", OpenJournal); return;
            }
            if (EpisodeEngine.IsCompetition(state.phase))
            {
                if (!state.competitionResolved)
                {
                    if (EpisodeEngine.CompetitionPlayers(state).Any(c => c.isPlayer))
                    {
                        hud.Paragraph(state.phase == EpisodePhase.FinalHoHPart1
                            ? "HOUSE SIGNALS: stop the marker near the center three times. Precision adds 0–2 effective endurance points (capped at 10) for the survival challenge; stored stats are unchanged."
                            : "HOUSE SIGNALS: stop the marker near the center three times. Precision supplies a 0–2 point bonus; housemate stats and the saved seed determine the rest.");
                        hud.Action("Enter precision challenge", () => StartChallenge(state));
                        hud.Action("Accessible alternative: steady 1-point bonus", () => Commit(state, EpisodeCommandKind.Compete, performance: .5));
                        if (state.phase == EpisodePhase.HoH || state.phase == EpisodePhase.Veto)
                        {
                            hud.Paragraph("Or simulate this weekly competition using weighted rules. Preparation: "
                                + state.playerStudyBonus + "/5; event bonus: " + state.phaseEventCompBonus
                                + ". These boost only your simulated score, with no precision bonus. Preparation is kept for later weeks and does not boost final HoH.");
                            hud.Action(EpisodeHud.SimulateCompetitionCaption, () => SimulateCompetition(state));
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
                foreach (var candidate in state.Active.Where(c => !c.isPlayer)) { string id = candidate.id; hud.Action("Evict " + candidate.name, () => Commit(state, EpisodeCommandKind.FinalEvict, id)); }
                return;
            }
            if (state.phase == EpisodePhase.Jury && !state.Active.Any(c => c.isPlayer) && !state.votes.Any(v => v.voterId == state.playerId))
            {
                hud.Paragraph("As a juror, choose who deserves to win.");
                foreach (var candidate in state.Active) { string id = candidate.id; hud.Action("Vote for " + candidate.name + " to win", () => Commit(state, EpisodeCommandKind.CastVote, id)); }
                return;
            }
            if (state.nominees.Count > 0) hud.Paragraph("Nominees: " + string.Join(" and ", state.nominees.Select(id => state.Find(id).name)));
            if (state.hohId != null) hud.Paragraph("HoH: " + state.Find(state.hohId).name);
            if (state.vetoHolderId != null) hud.Paragraph("Veto holder: " + state.Find(state.vetoHolderId).name);
            if (state.phase == EpisodePhase.Social || state.phase == EpisodePhase.Campaign)
                hud.Paragraph("Explore and talk freely before continuing. Social actions used: " + state.socialActions + "/18. You can finish the window whenever you choose.");
            if (state.phase == EpisodePhase.Jury) hud.Paragraph("Four jurors choose the winner. The source game's tie rule awards a tied jury to the second finalist in cast order.");
            hud.Action(state.phase == EpisodePhase.Social ? "Begin the next competition" : state.phase == EpisodePhase.Campaign ? "Close campaigning and open voting" : "Continue episode", () => Commit(state, EpisodeCommandKind.Advance));
        }


        private void StartChallenge(EpisodeState state)
        {
            challengeOrigin = state; challengeActive = true; challengeHits = 0; challengeTotal = 0; challengeStarted = Time.unscaledTime;
            audioBed.PlayCue(HouseAudio.Cue.CompetitionStart); Render();
        }
        private void ChallengePanel()
        {
            hud.Paragraph("Press Space or STOP when the marker is near the center. Three attempts; no time limit. Escape cancels without committing.");
            hud.ChallengeMeter(); hud.Action("STOP marker  [Space]", RecordChallengeHit);
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
            hud.Action(muted ? "Turn sound on" : "Mute sound", () => { muted = !muted; ApplyPreferences(); Render(); });
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
        public void NewSeason()
        {
            if (durableCommitInProgress) return;
            SuspendNpcWorldWithoutSaving();
            try
            {
                var nextStore = new EpisodeSaveStore(Path.Combine(saveRoot, "episode-" + Guid.NewGuid().ToString("N") + ".json"));
                var fresh = ContentCatalog.Create(unchecked((uint)DateTime.UtcNow.Ticks)); fresh.sessionId = Guid.NewGuid().ToString("N");
                nextStore.Save(fresh); // Stage and validate on disk before replacing the current in-memory session.
                saves = nextStore; Install(fresh);
                if (SaveRootOverride == null) { PlayerPrefs.SetString("Gamesim.ActiveSave", Path.GetFileName(saves.SavePath)); PlayerPrefs.Save(); }
                message = "New season started in a new slot. Previous saves were retained.";
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException)
            { message = "New season could not be saved. Your current session and slot were preserved. " + error.Message; }
            Render();
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
            cameraRig?.SetReducedMotion(reducedMotion);
            if (hud != null) hud.FontScale = largeText ? 1.2f : 1;
            foreach (var visual in FindObjectsByType<CharacterPresentation>()) visual.SetReducedMotion(reducedMotion);
            if (SaveRootOverride == null)
            { PlayerPrefs.SetInt("Gamesim.Muted", muted ? 1 : 0); PlayerPrefs.SetInt("Gamesim.ReducedMotion", reducedMotion ? 1 : 0); PlayerPrefs.SetInt("Gamesim.LargeText", largeText ? 1 : 0); PlayerPrefs.Save(); }
        }

        private void OnDisable()
        {
            if (!IsReady) return;
            // Sibling scene roots may already be destroyed during unloading. Never rebuild UI here.
            ClosePanelsInternal(false); if (hud != null) hud.SetVisible(false);
            DisposeNpcSocialWorld();
        }
        private void OnEnable()
        {
            if (!IsReady) return;
            hud?.SetVisible(true); ClosePanels();
        }

        private void OnDestroy() { DisposeNpcSocialWorld(); if (hud != null) Destroy(hud); }

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
