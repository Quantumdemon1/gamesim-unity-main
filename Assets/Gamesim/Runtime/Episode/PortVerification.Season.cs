using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>Additional opt-in, isolated functional season run; never contributes profile samples.</summary>
    public sealed partial class PortVerification
    {
        private bool verifySeason, seasonRunning, seasonPassed;
        private EpisodeDirector seasonDirector;
        private HousePlayerController seasonPlayer;
        private SeasonReport seasonReport;
        private double seasonStarted;
        private readonly List<string> seasonErrors = new List<string>();
        private readonly RaycastHit[] seasonSightHits = new RaycastHit[32];
        private const double SeasonTimeoutSeconds = 600;
        private const int SeasonCommandLimit = 180;
        private const string VerificationSpeech = "This is an automated verification speech.\nThe recorded decisions, not this QA text, define the season.";

        private IEnumerator RunSeasonVerification(bool graphical)
        {
            seasonRunning = true;
            seasonStarted = Time.realtimeSinceStartupAsDouble;
            studyReport = verifyStudy ? new StudyReport() : null;
            blocReport = verifyBlocs ? new BlocReport() : null;
            seasonReport = new SeasonReport
            {
                status = "Running", startedUtc = DateTime.UtcNow.ToString("O"), graphical = graphical,
                artifactId = Guid.NewGuid().ToString("N"), saveDirectory = outputDirectory,
                workload = "One legally created six-person season using actual active uGUI buttons, real NavMesh routes, diary confirmations, eligible finale choices, and save/reload. Interaction activation uses the same director methods as E; floor movement is API-driven, not simulated mouse input. Assisted competitions are used. Not a performance sample, human playtest, pacing measure, or exhaustive role-branch test."
            };
            if (verifyStudy)
            {
                seasonReport.study = studyReport;
                seasonReport.workload = seasonReport.workload.Replace("Assisted competitions are used.",
                    "Study opt-in: eligible weekly competitions use weighted simulation; finale retains the assisted/watch route. Study coverage is reported separately.");
            }
            if (verifyBlocs)
            {
                seasonReport.blocs = blocReport;
                seasonReport.workload += " Bloc opt-in: observe source-round NPC targets, public-only reasons and marker persistence; optional actual alliance influence is reported only when reached.";
            }
            // Flatten nested iterators so a failed action cannot strand the standalone process.
            var stack = new Stack<IEnumerator>(); stack.Push(SeasonChecks(graphical));
            while (stack.Count > 0)
            {
                object current = null;
                bool moved = false, failed = false;
                try
                {
                    CheckSeasonDeadline();
                    moved = stack.Peek().MoveNext();
                    if (moved) current = stack.Peek().Current;
                    else { (stack.Pop() as IDisposable)?.Dispose(); }
                }
                catch (Exception error) { RecordSeasonError(error.ToString()); failed = true; }
                if (failed) break;
                if (!moved) continue;
                if (current is IEnumerator child) stack.Push(child); else yield return current;
            }
            while (stack.Count > 0)
            {
                try { (stack.Pop() as IDisposable)?.Dispose(); }
                catch (Exception error) { RecordSeasonError(error.ToString()); }
            }
            seasonReport.elapsedSeconds = Time.realtimeSinceStartupAsDouble - seasonStarted;
            seasonReport.finishedUtc = DateTime.UtcNow.ToString("O");
            FinishStudyVerification();
            FinishBlocVerification();
            FinishAutonomyVerification();
            seasonReport.errors = seasonErrors.ToArray();
            seasonPassed = seasonErrors.Count == 0 && seasonReport.finished && seasonReport.saveReloadChecks >= 3;
            seasonReport.status = seasonPassed ? "Passed" : "Failed";
            seasonReport.unreachedBranches = new[]
            {
                seasonReport.finalistAnswers == 0 ? "Player-finalist jury answers were not reached in this legal season." : null,
                seasonReport.jurorQuestions == 0 ? "Player-juror questions were not reached in this legal season." : null,
                !seasonReport.playerSpeechSubmitted ? "Player final speech was not required in this legal season." : null,
                seasonReport.diaryReflections == 0 ? "No active-player reflection became available in this legal season." : null,
                seasonReport.oathOutcome == "Not reached" ? "A loyalty opportunity was not reached within the optional legal conversation budget." : null,
                "Precision timing input and physical keyboard/mouse event synthesis are outside this runner's coverage."
            }.Where(value => value != null).ToArray();
            try
            {
                File.WriteAllText(Path.Combine(outputDirectory,"season-verification.json"),JsonUtility.ToJson(seasonReport,true));
                Debug.Log("Gamesim standalone functional season " + seasonReport.status + ": " + Path.Combine(outputDirectory,"season-verification.json"));
            }
            catch (Exception error) { seasonPassed = false; Debug.LogError("Could not write functional season report: " + error); }
            seasonRunning = false;
        }

        private IEnumerator SeasonChecks(bool graphical)
        {
            seasonDirector = FindAnyObjectByType<EpisodeDirector>();
            RequireSeason(seasonDirector != null && seasonDirector.IsReady,"A ready episode is required for the functional season.");
            seasonPlayer = seasonDirector.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HousePlayerController>(true)).Single();
            RequireSeason(EventSystem.current != null && EventSystem.current.isActiveAndEnabled,"The functional UI run requires the scene's active EventSystem.");
            RequireSeason(EpisodeDirector.SaveRootOverride != null && Path.GetFullPath(EpisodeDirector.SaveRootOverride)
                .Equals(Path.GetFullPath(outputDirectory),StringComparison.OrdinalIgnoreCase),"The functional run must retain explicit save isolation.");
            CheckSaveIsIsolated();
            yield return CloseSeasonPanel();
            yield return ClickSeasonButton("Settings");
            yield return ClickSeasonButton("Save now  [F5]");
            string previousSlot = seasonDirector.SavePath;
            var previousBytes = File.ReadAllBytes(previousSlot);
            int previousCast = seasonDirector.Snapshot.contestants.Count;
            // The control opens the cast screen now rather than building a season outright, so this
            // walks the screen the way a player does — and backing out of it is the case worth
            // checking hardest, because a cancel that has already written a slot is unrecoverable.
            yield return ClickSeasonButton("New season in a NEW slot (preserves this season)");
            RequireSeason(seasonDirector.SavePath == previousSlot,"Opening the cast screen must not change the active slot.");
            yield return ClickSeasonButton(CastSelect.CancelCaption);
            RequireSeason(seasonDirector.SavePath == previousSlot && File.Exists(previousSlot)
                && seasonDirector.Snapshot.contestants.Count == previousCast,"Cancelling the cast screen must leave the running season and its slot untouched.");

            yield return ClickSeasonButton("New season in a NEW slot (preserves this season)");
            yield return ClickSeasonButton(CastSelect.StartCaption);
            var fresh = seasonDirector.Snapshot;
            RequireSeason(seasonDirector.SavePath != previousSlot && File.Exists(previousSlot),"Creating the QA season must preserve the profile's previous save slot.");
            RequireSeason(fresh.phase == EpisodePhase.Social && fresh.week == 1
                && fresh.Active.Count() == SeasonBuilder.DefaultHouseSize
                && fresh.contestants.Count == SeasonBuilder.DefaultHouseSize
                && fresh.contestants.Count(actor => actor.isPlayer) == 1,"The cast screen must start a genuine default-size season.");
            CheckSaveIsIsolated();
            seasonReport.sessionId = fresh.sessionId; seasonReport.seed = fresh.seed.ToString();
            seasonReport.profileSavePath = previousSlot; seasonReport.seasonSavePath = seasonDirector.SavePath;
            seasonReport.navigationSpeed = seasonPlayer.Agent.speed;
            yield return SaveReloadSeason("fresh-season");
            if (verifyAutonomy) yield return ExerciseAutonomy(graphical);
            if (verifyStudy) yield return ExerciseSeasonStudy(graphical);
            yield return ExerciseOptionalDeal(graphical);
            yield return ExerciseOptionalOath(graphical);

            bool middleReload = false;
            while (seasonDirector.Snapshot.phase != EpisodePhase.Finished)
            {
                CheckSeasonDeadline();
                RequireSeason(seasonReport.commands < SeasonCommandLimit,"The season exceeded its bounded command count.");
                var state = seasonDirector.Snapshot;
                if (!seasonReport.phases.Contains(state.phase.ToString())) seasonReport.phases.Add(state.phase.ToString());
                yield return DismissWeeklyRecap(state,graphical);
                state = seasonDirector.Snapshot;
                if (state.pendingDiary != null) yield return CompleteSeasonReflection(state,graphical);
                else
                {
                    yield return OpenSeasonStation();
                    // Free-roam world seconds may commit while walking. Capture the
                    // player's decision origin only after the modal has paused them.
                    state = seasonDirector.Snapshot;
                    yield return PerformSeasonDecision(state,graphical);
                }
                var committed = seasonDirector.Snapshot;
                RequireSeason(committed.revision == state.revision + 1,"Exactly one authoritative decision must commit per season step: " + state.phase + ". " + seasonDirector.StatusMessage);
                RequireSeason(EpisodeValidation.TryValidate(committed,out var reason),"The projected season must remain valid: " + reason);
                if (verifyStudy) ObserveStudyTransition(state, committed);
                if (verifyBlocs) ObserveBlocTransition(state, committed);
                seasonReport.commands++;
                if (!middleReload && seasonReport.commands >= 8)
                { yield return SaveReloadSeason("mid-season"); middleReload = true; }
            }
            var finale = seasonDirector.Snapshot;
            RequireSeason(finale.contestants.Count(actor => actor.status == ContestantStatus.Jury) == 4
                && !string.IsNullOrEmpty(finale.winnerId) && !string.IsNullOrEmpty(finale.runnerUpId)
                && finale.winnerId != finale.runnerUpId,"The season must end with four jurors and distinct winner/runner-up.");
            seasonReport.phases.Add(EpisodePhase.Finished.ToString());
            seasonReport.winnerId = finale.winnerId; seasonReport.playerFinalStatus = finale.Find(finale.playerId).status.ToString();
            RecordSeasonSystems(finale);
            yield return OpenSeasonStation();
            yield return CaptureSeason("finale",graphical);
            yield return ClickSeasonButton("Review the season");
            yield return CaptureSeason("finale-notebook",graphical);
            yield return SaveReloadSeason("completed-finale");
            RequireSeason(seasonDirector.Snapshot.phase == EpisodePhase.Finished,"Reload must preserve the completed finale.");
            RequireSeason(File.ReadAllBytes(previousSlot).SequenceEqual(previousBytes),"The functional season must not overwrite the profile's preserved save slot.");
            seasonReport.profileSavePreserved = true;
            seasonReport.finished = true;
        }

        private IEnumerator PerformSeasonDecision(EpisodeState state,bool graphical)
        {
            if (state.phase == EpisodePhase.Social && HouseEvents.Pending(state) != null)
            { yield return ResolveSeasonHouseEvent(state,graphical); yield break; }
            if (EpisodeEngine.IsCompetition(state.phase))
            {
                if (!verifyStudy && !state.competitionResolved && seasonReport.minigameKind == null
                    && EpisodeEngine.CompetitionPlayers(state).Any(actor => actor.isPlayer))
                { yield return PlaySeasonMiniGame(state,graphical); yield break; }
                if (verifyStudy)
                {
                    CheckStudyCompetitionScope(state);
                    if (!state.competitionResolved && (state.phase == EpisodePhase.HoH || state.phase == EpisodePhase.Veto)
                        && EpisodeEngine.CompetitionPlayers(state).Any(actor => actor.isPlayer))
                    { yield return SimulateStudyWeeklyCompetition(state, graphical); yield break; }
                }
                string caption = state.competitionResolved ? "Continue to the next ceremony"
                    : EpisodeEngine.CompetitionPlayers(state).Any(actor => actor.isPlayer)
                        ? "Accessible alternative: steady 1-point bonus" : "Watch eligible housemates compete";
                yield return ClickSeasonButton(caption); yield break;
            }
            if (state.phase == EpisodePhase.Nomination && state.nominees.Count == 0 && state.hohId == state.playerId)
            {
                foreach (var candidate in EpisodeEngine.NominationCandidates(state).Take(2)) yield return ClickSeasonButton(candidate.name);
                yield return ClickSeasonButton("Commit nominations"); yield break;
            }
            if (state.phase == EpisodePhase.VetoMeeting && !state.vetoResolved
                && (state.vetoHolderId == state.playerId || state.hohId == state.playerId && EpisodeEngine.NpcVetoSave(state) != null))
            {
                var replacement = EpisodeEngine.ReplacementCandidates(state).FirstOrDefault();
                if (replacement == null) { yield return ClickSeasonButton("Do not use the veto"); yield break; }
                if (state.hohId == state.playerId) yield return ClickSeasonButton(replacement.name,true);
                else yield return ClickSeasonButton("Save " + state.Find(state.nominees[0]).name + " (HoH chooses replacement)");
                yield break;
            }
            if (state.phase == EpisodePhase.Eviction && !state.evictionResolved && !state.votes.Any(vote => vote.voterId == state.playerId)
                && (EpisodeEngine.Voters(state).Any(actor => actor.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(state)))
            { yield return ClickSeasonButton("Vote to evict " + state.Find(state.nominees[0]).name); yield break; }
            if (state.phase == EpisodePhase.FinalEviction && state.hohId == state.playerId)
            { yield return ClickSeasonButton("Evict " + state.Active.First(actor => !actor.isPlayer).name); yield break; }
            if (state.phase == EpisodePhase.JuryQuestioning)
            {
                if (!seasonReport.juryReloadVerified)
                {
                    yield return SaveReloadSeason("jury-before-answer");
                    yield return OpenSeasonStation();
                    seasonReport.juryReloadVerified = true;
                }
                var exchange = state.juryExchanges[state.juryQuestionIndex];
                if (exchange.completed) yield return ClickSeasonButton(EpisodeHud.JuryContinueCaption);
                else if (exchange.finalistId == state.playerId)
                {
                    if (seasonReport.finalistAnswers == 0) yield return CaptureSeason("jury-finalist-question",graphical);
                    yield return ClickSeasonButton("A · " + exchange.optionA); seasonReport.finalistAnswers++;
                }
                else
                {
                    RequireSeason(exchange.questionerId == state.playerId,"The presented juror question must belong to the player.");
                    var option = WebJuryQuestioning.GetJurorQuestionOptions(state.juryQuestionIndex).Single(item => item.tone == "neutral");
                    if (seasonReport.jurorQuestions == 0) yield return CaptureSeason("jury-player-question",graphical);
                    yield return ClickSeasonButton(option.tone + " · " + option.text); seasonReport.jurorQuestions++;
                }
                yield break;
            }
            if (state.phase == EpisodePhase.FinalSpeeches)
            {
                if (state.Active.Any(actor => actor.isPlayer) && !state.finalSpeeches.Any(speech => speech.speakerId == state.playerId))
                {
                    var field = seasonDirector.GetComponentsInChildren<InputField>().Single(input => input.isActiveAndEnabled && input.name == "Final speech draft");
                    field.Select(); field.ActivateInputField(); yield return null; yield return null;
                    foreach (char character in VerificationSpeech)
                        field.ProcessEvent(new Event { type = EventType.KeyDown, character = character, keyCode = character == '\n' ? KeyCode.Return : KeyCode.None });
                    field.ForceLabelUpdate();
                    RequireSeason(field.text == VerificationSpeech,"The actual uGUI speech field must retain the typed QA draft.");
                    yield return CaptureSeason("final-speech-draft",graphical);
                    yield return ClickSeasonButton(EpisodeHud.SpeechSubmitCaption);
                    RequireSeason(seasonDirector.Snapshot.finalSpeeches.Any(speech => speech.speakerId == state.playerId
                        && speech.text == VerificationSpeech && speech.isPlayerAuthored),"The speech button must commit the entered text.");
                    seasonReport.playerSpeechSubmitted = true;
                    yield return SaveReloadSeason("submitted-final-speech");
                }
                else yield return ClickSeasonButton(EpisodeHud.SpeechContinueCaption);
                yield break;
            }
            if (state.phase == EpisodePhase.Jury && !state.Active.Any(actor => actor.isPlayer) && !state.votes.Any(vote => vote.voterId == state.playerId))
            { yield return ClickSeasonButton("Vote for " + state.Active.First().name + " to win"); yield break; }
            // Eviction night pauses on the block for the nominees' speeches, and a nominated player
            // has to give theirs before the house will vote. There is no "continue" past it, which
            // is the point: the speech is a decision, not a presentation.
            if (state.phase == EpisodePhase.Eviction && state.evictionStage == EvictionStage.Speeches
                && state.nominees.Contains(state.playerId)
                && !state.evictionSpeeches.Any(speech => speech.speakerId == state.playerId))
            {
                yield return CaptureSeason("block-speech-draft",graphical);
                yield return ClickSeasonButton(EpisodeHud.EvictionSpeechCaption);
                RequireSeason(seasonDirector.Snapshot.evictionSpeeches.Any(speech => speech.speakerId == state.playerId),
                    "The block speech control must commit a speech.");
                seasonReport.blockSpeeches++;
                yield break;
            }
            yield return ClickSeasonButton(state.phase == EpisodePhase.Social ? "Begin the next competition"
                : state.phase == EpisodePhase.Campaign ? "Close campaigning and open voting" : "Continue episode");
        }

        private IEnumerator CompleteSeasonReflection(EpisodeState state,bool graphical)
        {
            if (HasSeasonButton(EpisodeHud.DiaryVisitReflectionCaption)) yield return ClickSeasonButton(EpisodeHud.DiaryVisitReflectionCaption);
            else yield return ClickSeasonButton(EpisodeHud.DiaryTravelCaption);
            yield return WaitSeasonWalk("diary",seasonDirector.DiaryPosition);
            RequireSeason(seasonDirector.TryOpenDiary(),"The physically reached diary room must open.");
            yield return null; yield return null;
            if (seasonReport.diaryReflections == 0) yield return CaptureSeason("diary-choices",graphical);
            var choice = EpisodeEngine.CurrentDiary(state).choices.First();
            yield return ClickSeasonButton(choice.persona + " · " + choice.text);
            RequireSeason(seasonDirector.HasDiaryDecisionDraft && seasonDirector.Snapshot.revision == state.revision,"Reviewing a reflection cannot commit it early.");
            if (seasonReport.diaryReflections == 0) yield return CaptureSeason("diary-review",graphical);
            yield return ClickSeasonButton(EpisodeHud.DiaryConfirmReflectionCaption);
            RequireSeason(seasonDirector.Snapshot.pendingDiary == null && seasonDirector.Snapshot.resolvedDiaryIds.Contains(state.pendingDiary.id),"The reflection confirmation must consume its exact pending receipt.");
            seasonReport.diaryReflections++;
            yield return SaveReloadSeason("after-diary-" + seasonReport.diaryReflections);
        }

        private IEnumerator ExerciseOptionalOath(bool graphical)
        {
            seasonReport.oathOutcome = "Not reached";
            var npc = seasonDirector.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HouseNpc>(true))
                .FirstOrDefault(actor => actor.Id == ContentCatalog.MayaId && actor.gameObject.activeInHierarchy);
            if (npc == null) { seasonReport.oathNote = "Maya was unavailable in the new native scene."; yield break; }
            yield return CloseSeasonPanel();
            if (!FindSeasonNpcApproach(npc,out var approach)) { seasonReport.oathNote = "No safe nearby Maya approach found; optional oath coverage was skipped."; yield break; }
            if (!seasonPlayer.TryMoveTo(approach)) { seasonReport.oathNote = "Optional Maya approach was not accepted; season ceremonies remain mandatory."; yield break; }
            yield return WaitSeasonWalk("optional Maya conversation",approach,false);
            if (!seasonPlayer.HasArrived || Vector3.Distance(seasonPlayer.transform.position,approach) >= 1.1f)
            { seasonReport.oathNote = "Optional Maya approach did not complete within its route deadline."; yield break; }
            if (!seasonDirector.TryOpenNpc(npc.Id)) { seasonReport.oathNote = "Maya was not interactable after approach."; yield break; }
            yield return null; yield return null;
            // The week's budget is half the active house, not a flat eighteen, so this optional
            // coverage may simply run out of actions before the milestone. It breaks out and records
            // a note rather than failing: the oath path is optional by design.
            int weeklyBudget = EpisodeEngine.SocialActionBudget(seasonDirector.Snapshot);
            for (int spent = 0; spent < weeklyBudget && !seasonDirector.Snapshot.oathOpportunities.Contains(npc.Id); spent++)
            {
                var before = seasonDirector.Snapshot;
                if (before.phase != EpisodePhase.Social
                    || EpisodeEngine.SocialActionsSpent(before) >= EpisodeEngine.SocialActionBudget(before)
                    || !HasSeasonButton("Spend time together")) break;
                // Plain +4 conversations climb slowly from a neutral start.
                // Use the same legal alliance opportunity as the live Play Mode fixture.
                string action = !before.Allied(before.playerId,npc.Id) && before.Score(npc.Id,before.playerId) >= 8
                    && HasSeasonButton("Propose an alliance") ? "Propose an alliance" : "Spend time together";
                yield return ClickSeasonButton(action);
                if (seasonDirector.Snapshot.revision != before.revision + 1) break;
                seasonReport.optionalSocialCommands++;
            }
            if (seasonDirector.Snapshot.oathOpportunities.Contains(npc.Id) && HasSeasonButton(EpisodeHud.OathDeclareCaption))
            {
                yield return CaptureSeason("oath-opportunity",graphical);
                var before = seasonDirector.Snapshot;
                yield return ClickSeasonButton(EpisodeHud.OathDeclareCaption);
                RequireSeason(seasonDirector.Snapshot.revision == before.revision + 1
                    && seasonDirector.Snapshot.loyaltyOaths.Any(oath => oath.playerId == before.playerId && oath.targetId == npc.Id),"An available oath button must commit the player's declaration.");
                seasonReport.oathOutcome = "Legally earned and declared";
                seasonReport.oathNote = "Earned through at most eighteen actual social actions, including an eligible alliance proposal; no fabricated role, score, seed, or oath state.";
                yield return CaptureSeason("oath-recorded",graphical);
            }
            else seasonReport.oathNote = verifyStudy
                ? "Five confirmed study actions left " + studyReport.socialActionsRemainingForOptionalOath + " social actions for this optional oath attempt; no opportunity was reached and oath coverage is not asserted as passed."
                : "No loyalty opportunity appeared within eighteen legal social actions; this optional branch is not asserted as passed.";
            yield return CloseSeasonPanel();
        }

        private bool FindSeasonNpcApproach(HouseNpc npc,out Vector3 destination)
        {
            destination = Vector3.zero; float bestDistance = float.PositiveInfinity;
            var filter = new NavMeshQueryFilter { agentTypeID = seasonPlayer.Agent.agentTypeID, areaMask = seasonPlayer.Agent.areaMask };
            var path = new NavMeshPath();
            for (int direction = 0; direction < 12; direction++)
            {
                float angle = direction * Mathf.PI / 6;
                var candidate = npc.transform.position + new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle)) * 1.8f;
                if (!NavMesh.SamplePosition(candidate,out var hit,.45f,filter) || Mathf.Abs(hit.position.y - npc.transform.position.y) > .3f
                    || !seasonPlayer.Agent.CalculatePath(hit.position,path) || path.status != NavMeshPathStatus.PathComplete) continue;
                var origin = hit.position + Vector3.up * 1.15f;
                var offset = npc.transform.position + Vector3.up * 1.15f - origin;
                int count = Physics.RaycastNonAlloc(origin,offset.normalized,seasonSightHits,offset.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                if (count == seasonSightHits.Length) continue;
                bool blocked = false;
                for (int index = 0; index < count; index++)
                    if (!seasonSightHits[index].transform.IsChildOf(seasonPlayer.transform) && !seasonSightHits[index].transform.IsChildOf(npc.transform)) blocked = true;
                if (blocked) continue;
                float distance = Vector3.Distance(seasonPlayer.transform.position,hit.position);
                if (distance < bestDistance) { bestDistance = distance; destination = hit.position; }
            }
            return !float.IsPositiveInfinity(bestDistance);
        }

        private IEnumerator OpenSeasonStation()
        {
            if (seasonDirector.IsPanelOpen) yield break;
            var before = seasonDirector.Snapshot;
            yield return ClickSeasonButton("Go to episode screen");
            if (before.Find(before.playerId).status == ContestantStatus.Active)
                yield return WaitSeasonWalk("episode screen: " + before.phase,seasonDirector.StationPosition);
            RequireSeason(seasonDirector.TryOpenPhasePanel(),"The current episode station must open after navigation.");
            yield return null; yield return null;
        }

        private IEnumerator WaitSeasonWalk(string label,Vector3 destination,bool required = true)
        {
            double started = Time.realtimeSinceStartupAsDouble;
            while (!seasonPlayer.HasArrived && Time.realtimeSinceStartupAsDouble - started < 35)
            { CheckSeasonDeadline(); yield return null; }
            bool completed = seasonPlayer.HasArrived && Vector3.Distance(seasonPlayer.transform.position,destination) < 1.1f;
            seasonReport.routes.Add(new SeasonRoute { label = label, seconds = Time.realtimeSinceStartupAsDouble - started,
                completed = completed, required = required });
            RequireSeason(completed || !required,"Required NavMesh route failed: " + label);
            yield return null;
        }

        private IEnumerator SaveReloadSeason(string checkpoint)
        {
            yield return CloseSeasonPanel();
            yield return ClickSeasonButton("Settings");
            yield return ClickSeasonButton("Save now  [F5]");
            var expected = JsonUtility.ToJson(seasonDirector.Snapshot);
            string expectedPath = seasonDirector.SavePath;
            CheckSaveIsIsolated();
            RequireSeason(File.Exists(expectedPath),"The isolated season save must exist at " + checkpoint);
            yield return ClickSeasonButton("Reload current slot");
            RequireSeason(seasonDirector.SavePath == expectedPath && JsonUtility.ToJson(seasonDirector.Snapshot) == expected,"Save/reload changed the authoritative snapshot at " + checkpoint);
            RequireSeason(!seasonDirector.IsPanelOpen && seasonDirector.StatusMessage.StartsWith("Local episode loaded and validated.",StringComparison.Ordinal),"Reload must report a validated install, not silently preserve an unsaved in-memory snapshot." + " Panel open: " + seasonDirector.IsPanelOpen + "; status: " + seasonDirector.StatusMessage);
            if (verifyBlocs)
            {
                RequireSeason(seasonDirector.Snapshot.blocRulesStartWeek == 1,"Fresh-season rule marker must survive the actual save/reload UI.");
                blocReport.markerReloadChecks++;
            }
            seasonReport.saveReloadChecks++; seasonReport.reloadCheckpoints.Add(checkpoint);
        }

        private IEnumerator CloseSeasonPanel()
        {
            if (seasonDirector.IsWeeklyRecapOpen) yield return ClickSeasonButton(WeeklyRecapScreen.ContinueCaption);
            else if (seasonDirector.IsPanelOpen) yield return ClickSeasonButton("Close  [Esc]");
        }

        private bool HasSeasonButton(string caption) => seasonDirector.GetComponentsInChildren<Button>()
            .Any(button => button.IsActive() && button.IsInteractable() && button.name == caption);

        private IEnumerator ClickSeasonButton(string caption,bool allowFirstEquivalent = false)
        {
            CheckSeasonDeadline();
            var buttons = seasonDirector.GetComponentsInChildren<Button>().Where(button => button.IsActive() && button.IsInteractable()
                && button.GetComponentsInChildren<TMPro.TMP_Text>().Any(label => label.text == caption)).ToArray();
            RequireSeason(buttons.Length > 0 && (allowFirstEquivalent || buttons.Length == 1),"Expected a reachable actual button: " + caption + " (found " + buttons.Length + ").");
            // Selection exercises the modal's keyboard-focus/scroll adapter before activation;
            // do not invoke an off-screen content button without bringing it into view first.
            buttons[0].Select(); yield return null; yield return null;
            RequireSeason(buttons[0] != null && buttons[0].IsActive() && buttons[0].IsInteractable(),"The selected button became unavailable: " + caption);
            var before = seasonDirector.Snapshot;
            buttons[0].onClick.Invoke();
            yield return null; yield return null;
            var after = seasonDirector.Snapshot;
            seasonReport.buttons.Add(new SeasonButton { caption = caption, phase = before.phase.ToString(), revisionBefore = before.revision,
                revisionAfter = after.revision, elapsedSeconds = Time.realtimeSinceStartupAsDouble - seasonStarted });
        }

        private IEnumerator CaptureSeason(string label,bool graphical)
        {
            if (!graphical) yield break;
            Canvas.ForceUpdateCanvases();
            for (int frame = 0; frame < 5; frame++) yield return null;
            string path = Path.Combine(outputDirectory,"season-" + seasonReport.artifactId + "-" + label + ".png");
            ScreenCapture.CaptureScreenshot(path);
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while ((!File.Exists(path) || new FileInfo(path).Length == 0) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            yield return null; yield return null;
            RequireSeason(File.Exists(path) && new FileInfo(path).Length > 0,"A requested graphical season capture was not written: " + label);
            seasonReport.screenshots.Add(path);
        }

        private void CheckSaveIsIsolated()
        {
            string root = Path.GetFullPath(outputDirectory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            RequireSeason(Path.GetFullPath(seasonDirector.SavePath).StartsWith(root,StringComparison.OrdinalIgnoreCase),"Season saves must remain inside the explicit verification directory.");
        }

        private void CheckSeasonDeadline()
        {
            RequireSeason(Time.realtimeSinceStartupAsDouble - seasonStarted <= SeasonTimeoutSeconds,"The functional season exceeded its separate ten-minute deadline.");
            RequireSeason(seasonErrors.Count == 0,"Unity reported an error during the functional season.");
        }
        private static void RequireSeason(bool condition,string message) { if (!condition) throw new InvalidOperationException(message); }
        private void RecordSeasonError(string message) { if (seasonErrors.Count < 30) seasonErrors.Add(message); }

        [Serializable] private sealed class SeasonRoute { public string label; public double seconds; public bool completed, required; }
        [Serializable] private sealed class SeasonButton { public string caption, phase; public int revisionBefore, revisionAfter; public double elapsedSeconds; }
        [Serializable] private sealed class SeasonReport
        {
            public string status, startedUtc, finishedUtc, workload, artifactId, sessionId, seed, saveDirectory,
                profileSavePath, seasonSavePath, winnerId, playerFinalStatus, oathOutcome, oathNote;
            public bool graphical, finished, playerSpeechSubmitted, juryReloadVerified, profileSavePreserved;
            public StudyReport study;
            public BlocReport blocs;
            public AutonomyReport autonomy;
            public double elapsedSeconds;
            public float navigationSpeed;
            public int commands, optionalSocialCommands, diaryReflections, finalistAnswers, jurorQuestions, saveReloadChecks, blockSpeeches;
            public int weeklyRecaps, houseEventsResolved, dealsProposed, dealsAnswered, minigameInputs, storylinesBegun, houseEventsSeen, dealsRecorded, modifiersCarried;
            public string minigameKind, dealKind, dealOutcome, dealNote;
            public double minigameScore;
            public List<string> houseEventKinds = new List<string>();
            public List<string> phases = new List<string>(), reloadCheckpoints = new List<string>(), screenshots = new List<string>();
            public List<SeasonRoute> routes = new List<SeasonRoute>();
            public List<SeasonButton> buttons = new List<SeasonButton>();
            public string[] errors, unreachedBranches;
        }
    }
}
