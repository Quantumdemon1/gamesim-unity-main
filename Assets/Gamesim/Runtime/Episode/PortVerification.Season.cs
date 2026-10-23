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
            InitializeSeasonReport(graphical);
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
                !seasonReport.finalArgumentLocked ? "The final argument's lock was not reached in this legal season." : null,
                seasonReport.diaryReflections == 0 ? "No active-player reflection became available in this legal season." : null,
                seasonReport.oathOutcome == "Not reached" ? "A loyalty opportunity was not reached within the optional legal conversation budget." : null,
                seasonReport.namedVoteProposals == 0 ? "No actual named vote proposal was committed; see its explicit attempt disposition." : null,
                seasonReport.namedVoteProposals > 0 ? "Only the selected named-vote kind was proposed; other eligible named kinds are not command coverage." : null,
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

        // Keep unrequested reports explicit: Unity's inline serializer can materialize null
        // nested classes using their field defaults, which formerly claimed Running/requested.
        private void InitializeSeasonReport(bool graphical)
        {
            studyReport = new StudyReport { requested = verifyStudy, status = verifyStudy ? "Not run" : "Not requested" };
            blocReport = new BlocReport { requested = verifyBlocs, status = verifyBlocs ? "Not run" : "Not requested" };
            autonomyReport = new AutonomyReport { requested = verifyAutonomy, status = verifyAutonomy ? "Not run" : "Not requested" };
            seasonReport = new SeasonReport
            {
                status = "Running", startedUtc = DateTime.UtcNow.ToString("O"), graphical = graphical, batchMode = Application.isBatchMode,
                artifactId = Guid.NewGuid().ToString("N"), saveDirectory = outputDirectory,
                workload = SeasonWorkload(0), study = studyReport, blocs = blocReport, autonomy = autonomyReport,
            };
        }

        private string SeasonWorkload(int actualCastSize)
        {
            string identity = actualCastSize > 0 ? "One legally created " + actualCastSize + "-person season" : "Planned legal season (not yet created)";
            string workload = identity + " using actual active uGUI buttons, real NavMesh routes, diary confirmations, eligible finale choices, and save/reload. Interaction activation uses the same director methods as E; floor movement is API-driven, not simulated mouse input. Assisted competitions are used. Not a performance sample, human playtest, pacing measure, or exhaustive role-branch test.";
            if (verifyStudy)
                workload = workload.Replace("Assisted competitions are used.",
                    "Study opt-in: eligible weekly competitions use weighted simulation; finale retains the assisted/watch route. Study coverage is reported separately.");
            if (verifyBlocs)
                workload += " Bloc opt-in: observe source-round NPC targets, public-only reasons and marker persistence; optional actual alliance influence is reported only when reached.";
            return workload;
        }

        private void RecordSeasonIdentity(EpisodeState fresh)
        {
            seasonReport.sessionId = fresh.sessionId; seasonReport.seed = fresh.seed.ToString();
            seasonReport.castSize = fresh.contestants.Count;
            seasonReport.workload = SeasonWorkload(seasonReport.castSize);
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
            // A lost EnableFinale would fall back to the catalogue's A and B and never walk the responses.
            RequireSeason(fresh.finaleRulesStartWeek == 1,"The cast screen's season must play the finale rules from its first week.");
            // A lost EnableCommitments would leave study free and a breach held against its victim.
            RequireSeason(fresh.commitmentRulesStartWeek == 1,"The cast screen's season must play the commitment rules from its first week.");
            // A lost EnableAllianceLeaks would keep every secret pact secret and the whisper's two names (WAVE-D D4).
            RequireSeason(fresh.allianceLeakRulesStartWeek == 1,"The cast screen's season must play the leak rules from its first week.");
            // A lost EnablePactPlans would leave a pact of three calling and meeting as a pair (WAVE-D D3).
            RequireSeason(fresh.pactPlanRulesStartWeek == 1,"The cast screen's season must play the war rooms from its first week.");
            // A lost EnableAllWeek would leave the house's turns all at the social week's opening (WAVE-D D2).
            RequireSeason(fresh.allWeekRulesStartWeek == 1,"The cast screen's season must play the house's turns all week from its first week.");
            CheckSaveIsIsolated();
            RecordSeasonIdentity(fresh);
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
                // A play offered on the Pull is taken on once a season, from free roam, where the
                // Pull is. Its own step, outside the one-decision accounting: the world may tick
                // a commit of its own while the Pull is pressed with no panel to pause it.
                else if (!seasonPlayTaken && state.phase == EpisodePhase.Social && !seasonDirector.IsPanelOpen
                         && HasSeasonButtonText(EpisodeHud.TakeItOnCaption))
                { yield return TakeSeasonPlay(graphical); continue; }
                // The read, once a season: ask a voter straight, then read them, in the campaign. Its
                // own step as well: the walk to the voter is free roam, where the world's own commits
                // land, so it keeps its own accounting from the moment the conversation is open.
                else if (SeasonReadDue(state))
                { yield return ExerciseSeasonRead(state, graphical); continue; }
                // Separate one-shot companion, after the free reads. It owns only its actual paid
                // proposal; walking can tick the world and must not enter one-decision accounting.
                else if (!seasonNamedVoteAttempted && state.phase == EpisodePhase.Campaign)
                { yield return ExerciseSeasonNamedVoteDeal(graphical); continue; }
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
            // Every evictee sits on the jury (the engine turns each eviction into a juror), so a
            // season ends with everyone but the two finalists there: four in a six-house, six in
            // the eight the builder seats by default.
            int jurors = finale.contestants.Count - 2;
            RequireSeason(finale.contestants.Count(actor => actor.status == ContestantStatus.Jury) == jurors
                && !string.IsNullOrEmpty(finale.winnerId) && !string.IsNullOrEmpty(finale.runnerUpId)
                && finale.winnerId != finale.runnerUpId,"The season must end with " + jurors + " jurors and distinct winner/runner-up.");
            seasonReport.phases.Add(EpisodePhase.Finished.ToString());
            seasonReport.winnerId = finale.winnerId; seasonReport.playerFinalStatus = finale.Find(finale.playerId).status.ToString();
            RecordSeasonSystems(finale);
            seasonReport.playNote = !seasonPlayTaken ? "No play was offered on the Pull this season."
                : finale.events.Any(e => e.kind == "story-play") ? "The play taken on ended, and the log says how."
                : finale.storylines.Any(x => EpisodeEngine.TakenOn(x)) ? "The play taken on was still running at the end."
                : "The play taken on left no ending and no running cycle.";
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
            // Somebody who came to the player is answered first, as the panel draws them first.
            if ((state.phase == EpisodePhase.Social || state.phase == EpisodePhase.Campaign || HoHPitches.Available(state))
                && ReplyCards.Pending(state) != null)
            { yield return AnswerSeasonReplyCard(state); yield break; }
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
                        ? EpisodeDirector.AccessibleCompetitionCaption(state.competitionRulesVersion) : "Watch eligible housemates compete";
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
                // At the final four a holder who is not on the block may not use the veto
                // (EpisodeEngine.VetoIsLockedAtFinalFour), and the meeting offers that holder only the
                // way to keep the block, under the lock's line. The replacement candidates do not know
                // the lock - the fourth houseguest is still one - so a player who was Head of
                // Household and won the veto at four was walked into a name the screen rightly never
                // drew (rc2, seed 2959573182). The PlayMode driver learned the lock when it came in
                // (EpisodePlayModeTests.NextCommand); this is the same rule. The walk presses what the
                // screen offers, and holds the screen to offering nothing else.
                bool locked = EpisodeEngine.VetoIsLockedAtFinalFour(state);
                if (locked)
                {
                    // Observe the meeting itself: a result overlay hides every meeting control.
                    yield return DismissSeasonCompetitionResult();
                    RequireSeason(HasSeasonButtonText("Do not use the veto"),
                        "A locked veto meeting must expose its keep-the-block control before checking its choices.");
                    RequireSeason(!SeasonVetoUseOffered(state),"A veto locked at the final four must offer no way to use it.");
                }
                if (replacement == null || locked) { yield return ClickSeasonButton("Do not use the veto"); yield break; }
                if (state.hohId == state.playerId) yield return ClickSeasonButton(replacement.name,true);
                else yield return ClickSeasonButton(EpisodeDirector.VetoSaveCaption(state, state.nominees[0]));
                yield break;
            }
            // The ballot is offered only once the night reaches the vote: the stages before it
            // (the speeches, and a player on the block delivering theirs) are walked further down,
            // and "Continue episode" is what moves the night from one stage to the next.
            if (state.phase == EpisodePhase.Eviction && !state.evictionResolved && !state.votes.Any(vote => vote.voterId == state.playerId)
                && (state.evictionStage == EvictionStage.Voting || state.evictionStage == EvictionStage.Tiebreaker)
                && (EpisodeEngine.Voters(state).Any(actor => actor.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(state)))
            { yield return ClickSeasonButton("Vote to evict " + state.Find(state.nominees[0]).name); yield break; }
            // The final choice's control says both halves of it, "Take Maya · evict Taylor" (UI-UX-PASS-PLAN Q0).
            if (state.phase == EpisodePhase.FinalEviction && state.hohId == state.playerId)
            { yield return ClickSeasonButton(FinalChoiceWords.CaptionToEvict(state, state.Active.First(actor => !actor.isPlayer).id)); yield break; }
            if (state.phase == EpisodePhase.JuryQuestioning)
            {
                if (!seasonReport.juryReloadVerified)
                {
                    yield return SaveReloadSeason("jury-before-answer");
                    yield return OpenSeasonStation();
                    seasonReport.juryReloadVerified = true;
                }
                var exchange = state.juryExchanges[state.juryQuestionIndex];
                // The final argument, once, before the first answer (ENDGAME-PLAN F4b): the door, a
                // narrative, the first moments, the lock - the step's one commit.
                if (EpisodeDirector.FinalCaseAvailable(state) && state.finalArgument == null)
                {
                    yield return ClickSeasonButton(EpisodeDirector.FinalCaseCaption);
                    yield return ClickSeasonButton(FinalArgument.Label(FinalArgument.Themes[0]));
                    foreach (var moment in FinalArgument.Moments(state).Take(FinalArgument.Required(state)).ToList())
                        yield return ClickSeasonButton(moment.text);
                    yield return CaptureSeason("final-case",graphical);
                    yield return ClickSeasonButton(EpisodeDirector.LockArgumentCaption);
                    RequireSeason(seasonDirector.Snapshot.finalArgument != null && seasonDirector.Snapshot.finalArgument.theme == FinalArgument.Themes[0],
                        "The lock must save the chosen narrative.");
                    seasonReport.finalArgumentLocked = true;
                    yield break;
                }
                if (exchange.completed) yield return ClickSeasonButton(EpisodeHud.JuryContinueCaption);
                else if (exchange.finalistId == state.playerId)
                {
                    if (seasonReport.finalistAnswers == 0) yield return CaptureSeason("jury-finalist-question",graphical);
                    // Under the finale rules a response the question always offers; the catalogue's A otherwise.
                    yield return ClickSeasonButton(EpisodeEngine.FinaleOn(state) ? FinaleQuestions.Caption(FinaleQuestions.Own) : "A · " + exchange.optionA);
                    seasonReport.finalistAnswers++;
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
                    // The HUD's speech field is an EpisodeSpeechInputField, a TMP_InputField. This looked
                    // for the legacy uGUI InputField, missed when the HUD moved to TextMeshPro (the PlayMode
                    // helper was caught then; this one only runs when the walk makes the player a finalist).
                    var field = seasonDirector.GetComponentsInChildren<TMPro.TMP_InputField>().Single(input => input.isActiveAndEnabled && input.name == "Final speech draft");
                    // The locked argument fills the editor; the walk checks it, then writes its own.
                    if (state.finalArgument != null)
                    {
                        RequireSeason(field.text == FinalArgument.Speech(state),"The locked final argument must fill the speech editor.");
                        field.text = "";
                    }
                    field.Select(); field.ActivateInputField(); yield return null; yield return null;
                    foreach (char character in VerificationSpeech)
                        field.ProcessEvent(new Event { type = EventType.KeyDown, character = character, keyCode = character == '\n' ? KeyCode.Return : KeyCode.None });
                    field.ForceLabelUpdate();
                    RequireSeason(field.text == VerificationSpeech,"The actual speech field must retain the typed QA draft.");
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
            // A juror's ballot; a player production removed has none, and continues.
            if (EpisodeDirector.JurorVotes(state))
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
            yield return WaitSeasonDiarySeating();
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
            var initial = seasonDirector.Snapshot;
            string owner = SeasonConversationBlocker(initial, seasonDirector.TalkingToId);
            if (owner != null)
            {
                seasonReport.oathNote = "BlockedByOtherView: " + owner;
                RecordSeasonConversationAttempt("Oath", "BlockedByOtherView", initial, seasonDirector.TalkingToId,
                    EpisodeHud.OathDeclareCaption, owner);
                yield break;
            }
            var npc = seasonDirector.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HouseNpc>(true))
                .FirstOrDefault(actor => actor.Id == ContentCatalog.MayaId && actor.gameObject.activeInHierarchy);
            if (npc == null)
            {
                seasonReport.oathNote = "Unreachable: Maya had no active body.";
                RecordSeasonConversationAttempt("Oath", "Unreachable", initial, ContentCatalog.MayaId, EpisodeHud.OathDeclareCaption, seasonReport.oathNote);
                yield break;
            }
            string ineligible = SeasonOathIneligible(initial, npc.Id, false);
            if (ineligible != null)
            {
                seasonReport.oathNote = "Ineligible: " + ineligible;
                RecordSeasonConversationAttempt("Oath", "Ineligible", initial, npc.Id, EpisodeHud.OathDeclareCaption, ineligible);
                yield break;
            }
            yield return CloseSeasonPanel();
            if (!FindSeasonNpcApproach(npc,out var approach))
            { seasonReport.oathNote = "Unreachable: no safe Maya approach."; }
            else if (!seasonPlayer.TryMoveTo(approach))
            { seasonReport.oathNote = "Unreachable: Maya approach was not accepted."; }
            else
            {
                yield return WaitSeasonWalk("optional Maya conversation",approach,false);
                if (!seasonPlayer.HasArrived || Vector3.Distance(seasonPlayer.transform.position,approach) >= 1.1f)
                    seasonReport.oathNote = "Unreachable: Maya approach exceeded its route deadline.";
                else if (!seasonDirector.TryOpenNpc(npc.Id))
                    seasonReport.oathNote = "Unreachable: Maya was not interactable after approach.";
            }
            if (!seasonDirector.IsConversationOpen || seasonDirector.TalkingToId != npc.Id)
            {
                RecordSeasonConversationAttempt("Oath", "Unreachable", seasonDirector.Snapshot, npc.Id,
                    EpisodeHud.OathDeclareCaption, seasonReport.oathNote);
                yield break;
            }
            yield return null; yield return null;
            yield return DrainSeasonConversationExchange("before-oath", false);
            // Preserve the original finite policy: this initial budget bounds setup, with at most
            // one real alliance proposal and otherwise the same paid relationship-building verb.
            int weeklyBudget = EpisodeEngine.SocialActionBudget(seasonDirector.Snapshot);
            bool proposed = false;
            for (int spent = 0; spent < weeklyBudget && !seasonDirector.Snapshot.oathOpportunities.Contains(npc.Id); spent++)
            {
                var before = seasonDirector.Snapshot;
                owner = SeasonConversationBlocker(before, npc.Id);
                ineligible = SeasonOathIneligible(before, npc.Id, false);
                if (owner != null || ineligible != null)
                {
                    seasonReport.oathNote = owner != null ? "BlockedByOtherView: " + owner : "Ineligible: " + ineligible;
                    RecordSeasonConversationAttempt("Oath", owner != null ? "BlockedByOtherView" : "Ineligible", before,
                        npc.Id, EpisodeHud.OathDeclareCaption, owner ?? ineligible);
                    yield return CloseSeasonPanel(); yield break;
                }
                if (before.phase != EpisodePhase.Social
                    || EpisodeEngine.SocialActionsSpent(before) >= EpisodeEngine.SocialActionBudget(before)) break;
                string action = "Spend time together";
                if (!proposed && !before.Allied(before.playerId,npc.Id) && before.Score(npc.Id,before.playerId) >= 8)
                {
                    yield return EnterSeasonConversationCategory(EpisodeDirector.DealsCategoryCaption);
                    string locked = EpisodeEngine.AllianceRefusal(before, npc.Id);
                    if (locked == null)
                    {
                        RequireSeasonConversationLeaf("OathSetup", npc.Id, EpisodeDirector.DealsCategoryCaption, "Propose an alliance");
                        action = "Propose an alliance";
                    }
                    else
                    {
                        // A lawful cap can use the old Talk fallback; disappearance of an eligible
                        // alliance row cannot. A locked row must actually be drawn locked.
                        RequireSeasonLockedConversationLeaf("Propose an alliance");
                        RecordSeasonConversationAttempt("OathSetup", "LegallyLocked", before, npc.Id, "Propose an alliance", locked);
                    }
                }
                if (action == "Spend time together")
                    yield return EnterSeasonConversationCategory(EpisodeDirector.ConnectCategoryCaption);
                RequireSeasonConversationLeaf("OathSetup", npc.Id, seasonDirector.CurrentConversationCategory, action);
                before = seasonDirector.Snapshot;
                var baseline = ConversationBaseline();
                yield return ClickSeasonButton(action);
                var after = seasonDirector.Snapshot;
                RequireSeasonConversationCommit(before, after, baseline, true, "OathSetup");
                proposed |= action == "Propose an alliance";
                RecordSeasonConversationAttempt("OathSetup", "Committed", before, npc.Id, action, "One paid setup command.", after);
                seasonReport.optionalSocialCommands++;
                seasonReport.commands++;
                yield return DrainSeasonConversationExchange("oath-social-" + spent, graphical);
            }
            var opportunity = seasonDirector.Snapshot;
            owner = SeasonConversationBlocker(opportunity, npc.Id);
            ineligible = SeasonOathIneligible(opportunity, npc.Id, true);
            if (owner != null || ineligible != null)
            {
                seasonReport.oathNote = owner != null ? "BlockedByOtherView: " + owner : "Ineligible: " + ineligible;
                RecordSeasonConversationAttempt("Oath", owner != null ? "BlockedByOtherView" : "Ineligible", opportunity,
                    npc.Id, EpisodeHud.OathDeclareCaption, owner ?? ineligible);
                yield return CloseSeasonPanel(); yield break;
            }
            // An actual unclaimed declaration is FREE, including when setup consumed the last AP.
            yield return EnterSeasonConversationCategory(EpisodeDirector.DealsCategoryCaption);
            RequireSeasonConversationLeaf("Oath", npc.Id, EpisodeDirector.DealsCategoryCaption, EpisodeHud.OathDeclareCaption);
            var beforeOath = seasonDirector.Snapshot;
            var oathBaseline = ConversationBaseline();
            yield return CaptureSeason("oath-opportunity",graphical);
            RequireConversationUnchanged(oathBaseline, "Capturing the available oath");
            yield return ClickSeasonButton(EpisodeHud.OathDeclareCaption);
            var declared = seasonDirector.Snapshot;
            RequireSeasonConversationCommit(beforeOath, declared, oathBaseline, false, "Oath");
            RequireSeason(!declared.oathOpportunities.Contains(npc.Id)
                && declared.loyaltyOaths.Count(oath => oath.playerId == beforeOath.playerId && oath.targetId == npc.Id) == 1
                && EpisodeDirector.CommittedEvents(declared, beforeOath.nextSequence).Any(e => e.kind == "loyalty-oath"),
                "An available oath must consume its actual opportunity and record the exact declaration.");
            RecordSeasonConversationAttempt("Oath", "Committed", beforeOath, npc.Id, EpisodeHud.OathDeclareCaption,
                "One free declaration, not a promise from the NPC.", declared);
            seasonReport.commands++;
            seasonReport.oathOutcome = "Legally earned and declared";
            seasonReport.oathNote = "Earned within the initial legal action budget, at most one alliance proposal; free declaration, including at zero AP.";
            var recordedBaseline = ConversationBaseline();
            yield return CaptureSeason("oath-recorded",graphical);
            RequireConversationUnchanged(recordedBaseline, "Capturing the recorded oath");
            yield return DrainSeasonConversationExchange("oath-declared", graphical);
            yield return CloseSeasonPanel();
        }

        private static string SeasonOathIneligible(EpisodeState state, string npcId, bool needsOpportunity)
        {
            if (state.Find(state.playerId)?.status != ContestantStatus.Active
                || (state.phase != EpisodePhase.Social && state.phase != EpisodePhase.Campaign))
                return "Loyalty declarations require active-player social time.";
            if (state.Find(npcId)?.status != ContestantStatus.Active || npcId == state.playerId)
                return "No active NPC recipient.";
            if (state.loyaltyOaths.Any(o => o.playerId == state.playerId && o.targetId == npcId
                || o.targetId == state.playerId && o.playerId == npcId)) return "This pair already has an oath.";
            return needsOpportunity && !state.oathOpportunities.Contains(npcId)
                ? "No unclaimed opportunity after the bounded legal setup; no oath coverage." : null;
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
                int count = Physics.RaycastNonAlloc(origin,offset.normalized,seasonSightHits,offset.magnitude,HouseLayers.Sight,QueryTriggerInteraction.Ignore);
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
                blocReport.status = "Running";
                RequireSeason(seasonDirector.Snapshot.blocRulesStartWeek == 1,"Fresh-season rule marker must survive the actual save/reload UI.");
                blocReport.markerReloadChecks++;
            }
            seasonReport.saveReloadChecks++; seasonReport.reloadCheckpoints.Add(checkpoint);
        }

        private IEnumerator WaitSeasonDiarySeating()
        {
            float deadline=Time.realtimeSinceStartup+20f;
            while(seasonDirector.IsDiaryOpen && !seasonDirector.IsDiarySettled && Time.realtimeSinceStartup<deadline)yield return null;
            RequireSeason(seasonDirector.IsDiarySettled,"The diary must complete its route, alignment and seated presentation before choices appear.");
            yield return null;
        }

        private IEnumerator CloseSeasonPanel()
        {
            yield return DismissSeasonCompetitionResult();
            if (seasonDirector.IsWeeklyRecapOpen) yield return ClickSeasonButton(WeeklyRecapScreen.ContinueCaption);
            else if (seasonDirector.IsPanelOpen) yield return ClickSeasonButton("Close  [Esc]");
            var seat=seasonPlayer.GetComponent<Gamesim.House.DiarySeatPose>();
            float deadline=Time.realtimeSinceStartup+3f;
            while(seat!=null && seat.Active && Time.realtimeSinceStartup<deadline)yield return null;
            RequireSeason(seat==null || !seat.Active,"Leaving the diary must release its movement owner.");
        }

        private bool HasSeasonButton(string caption) => VisibleSeasonButtons()
            .Any(button => button.IsActive() && button.IsInteractable() && button.name == caption);

        /// <summary>Whether the veto meeting offers any way to use the veto: a save, a pick of whom to save, or a replacement's name.</summary>
        private bool SeasonVetoUseOffered(EpisodeState state) =>
            state.nominees.Any(id => HasSeasonButtonText(EpisodeDirector.VetoSaveCaption(state, id))
                || HasSeasonButtonText(EpisodeDirector.VetoSavePickCaption(state.Find(id).name)))
            || EpisodeEngine.ReplacementCandidates(state).Any(candidate => HasSeasonButtonText(candidate.name));

        /// <summary>
        /// What the screen offered when a caption was missing: the phase, and the captions of the
        /// pressable controls the walk could see, so a failed step says what was there instead of
        /// only what was not (rc2's "found 0" took a save file to read).
        /// </summary>
        private string SeasonButtonsOnOffer()
        {
            var captions = VisibleSeasonButtons()
                .Select(button => button.GetComponentsInChildren<TMPro.TMP_Text>().Select(label => label.text)
                    .FirstOrDefault(text => !string.IsNullOrEmpty(text)) ?? button.name)
                .ToList();
            var state = seasonDirector != null ? seasonDirector.Snapshot : null;
            return " Phase " + (state != null ? state.phase + ", week " + state.week : "unknown") + "; on offer ("
                + captions.Count + "): " + string.Join(" | ", captions.Take(24).Select(text => text.Length > 60 ? text.Substring(0, 60) + "..." : text))
                + (captions.Count > 24 ? " | ..." : "") + ".";
        }

        private IEnumerator ClickSeasonButton(string caption,bool allowFirstEquivalent = false)
        {
            CheckSeasonDeadline();
            if(caption!="Continue")yield return DismissSeasonCompetitionResult();
            var buttons = SeasonButtonsCarrying(caption);
            if (buttons.Length == 0 || !allowFirstEquivalent && buttons.Length != 1)
                RequireSeason(false,"Expected a reachable actual button: " + caption + " (found " + buttons.Length + ")." + SeasonButtonsOnOffer());
            // Selection exercises the modal's keyboard-focus/scroll adapter before activation;
            // do not invoke an off-screen content button without bringing it into view first.
            buttons[0].Select(); yield return null; yield return null;
            // Then find the caption again. The HUD rebuilds itself for reasons that have nothing to
            // do with the press - a houseguest's body finishing its assembly is one - and the
            // control that was selected is destroyed by that rebuild. A player presses whatever is
            // on screen when they press, so this does too.
            buttons = SeasonButtonsCarrying(caption);
            if (buttons.Length == 0 || !allowFirstEquivalent && buttons.Length != 1)
                RequireSeason(false,"The selected button became unavailable: " + caption + " (found " + buttons.Length + ")." + SeasonButtonsOnOffer());
            var before = seasonDirector.Snapshot;
            buttons[0].onClick.Invoke();
            yield return null; yield return null;
            var after = seasonDirector.Snapshot;
            seasonReport.buttons.Add(new SeasonButton { caption = caption, phase = before.phase.ToString(), revisionBefore = before.revision,
                revisionAfter = after.revision, elapsedSeconds = Time.realtimeSinceStartupAsDouble - seasonStarted });
            ObserveSeasonEvictions(before, after);
        }

        /// <summary>Report the log at the actual decision boundary, not only its final retained length.</summary>
        private void ObserveSeasonEvictions(EpisodeState before, EpisodeState after)
        {
            foreach (var entry in after.events.Where(item => item.sequence >= before.nextSequence && item.kind == "eviction"))
            {
                if (seasonReport.evictions.Any(item => item.eventSequence == entry.sequence)) continue;
                seasonReport.evictions.Add(new SeasonEviction
                {
                    week = entry.week, eventSequence = entry.sequence,
                    revisionBefore = before.revision, revisionAfter = after.revision,
                    entriesBefore = before.events.Count, entriesAfter = after.events.Count
                });
                if (before.events.Count == 256) seasonReport.fullLogEvictions++;
            }
        }

        /// <summary>Every live, pressable control showing this caption.</summary>
        private Button[] SeasonButtonsCarrying(string caption) => VisibleSeasonButtons()
            .Where(button => button.IsActive() && button.IsInteractable()
                && button.GetComponentsInChildren<TMPro.TMP_Text>().Any(label => label.text == caption)).ToArray();

        private Button[] VisibleSeasonButtons()
        {
            var buttons=seasonDirector.gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Button>())
                .Where(b=>b.IsActive()&&b.IsInteractable()).ToArray();
            int order=buttons.Select(b=>b.GetComponentInParent<Canvas>()?.sortingOrder??0).DefaultIfEmpty(0).Max();
            return buttons.Where(b=>(b.GetComponentInParent<Canvas>()?.sortingOrder??0)==order).ToArray();
        }

        private IEnumerator DismissSeasonCompetitionResult()
        {
            var result=seasonDirector.gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<CompetitionResult>())
                .FirstOrDefault(r=>r.IsPlaying);
            if(result==null)yield break;
            // This is an explicit Continue gesture, not an assumption that readable results time out.
            yield return new WaitForSecondsRealtime(.3f);
            var button=result.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name=="Continue from competition results");
            RequireSeason(button!=null,"Competition results must offer Continue.");button.onClick.Invoke();
            yield return null;yield return null;
            RequireSeason(!result.IsPlaying,"Continue must dismiss the result without advancing the episode.");
        }

        private IEnumerator CaptureSeason(string label,bool graphical)
        {
            if (!graphical) yield break;
            Canvas.ForceUpdateCanvases();
            for (int frame = 0; frame < 5; frame++) yield return null;
            string path = Path.Combine(outputDirectory,"season-" + seasonReport.artifactId + "-" + label + ".png");
            yield return CaptureVerifiedFrame(path, seasonReport.capturedFrames.Add, reason => RequireSeason(false, reason));
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
        [Serializable] private sealed class SeasonEviction
        {
            public int week, eventSequence, revisionBefore, revisionAfter, entriesBefore, entriesAfter;
            public bool recapObserved;
        }
        [Serializable] private sealed class SeasonConversationAttempt
        {
            public string feature, disposition, phase, npcId, subjectId, kind, caption, note, canonicalId, outcome;
            public int week, unifiedMode, revisionBefore, revisionAfter, spentBefore, spentAfter, budgetBefore, budgetAfter;
        }
        [Serializable] private sealed class SeasonReport
        {
            public string status, startedUtc, finishedUtc, workload, artifactId, sessionId, seed, saveDirectory,
                profileSavePath, seasonSavePath, winnerId, playerFinalStatus, oathOutcome, oathNote;
            public bool graphical, batchMode, finished, playerSpeechSubmitted, juryReloadVerified, profileSavePreserved, finalArgumentLocked;
            public List<VerificationFrameEvidence> capturedFrames = new List<VerificationFrameEvidence>();
            public StudyReport study;
            public BlocReport blocs;
            public AutonomyReport autonomy;
            public double elapsedSeconds;
            public float navigationSpeed;
            // Zero means creation was not reached; populated only from the actual fresh snapshot.
            public int castSize;
            public int commands, optionalSocialCommands, diaryReflections, finalistAnswers, jurorQuestions, saveReloadChecks, blockSpeeches;
            public int weeklyRecaps, houseEventsResolved, dealsProposed, dealsAnswered, minigameInputs, storylinesBegun, houseEventsSeen, dealsRecorded, modifiersCarried;
            // Zero is missing coverage, not a failed season: the legal walk does not force a full log.
            public int fullLogEvictions, fullLogEvictionRecaps;
            public List<SeasonEviction> evictions = new List<SeasonEviction>();
            public int replyCardsAnswered;
            public int votesAsked, peopleRead;
            // Scheduling/dispositions are not feature passes. Only actual authoritative commits
            // increment these counters; an agreement additionally needs its real canonical row.
            public int namedVoteProposals, namedVoteReceipts;
            public string namedVoteDisposition = "Not attempted", namedVoteKind, namedVoteNpcId, namedVoteSubjectId, namedVoteReceiptId, namedVoteOutcome;
            public List<SeasonConversationAttempt> conversationAttempts = new List<SeasonConversationAttempt>();
            public int conversationNavigations, conversationPlaybackControls;
            public string readNote;
            public int playsTaken;
            public string playNote;
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
