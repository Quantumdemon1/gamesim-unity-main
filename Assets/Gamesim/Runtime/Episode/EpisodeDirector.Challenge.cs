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
    /// <summary>The competitions: the timing bar and the three mini-games, from the start of a challenge to the commit of its result.</summary>
    public sealed partial class EpisodeDirector
    {
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
                    CharacterPortraits.Get(actor), actor));
            }
            return standings;
        }

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

        private CompetitionGameScreen competitionScreen;
        private bool challengePractice, challengeResultShown, challengeCommitting;
        private string challengeCommandId;
        private bool competitionInputSuspended, competitionAssemblyHudHidden;

        private void SyncCompetitionResultInput()
        {
            if (!isActiveAndEnabled || projected == null) return;
            competitionInputSuspended = competitionCard != null && competitionCard.OwnsInput;
            bool blocked = blockedRecovery || IsPanelOpen || competitionInputSuspended;
            if (player != null) player.SetInputEnabled(!blocked && projected.Find(projected.playerId).status == ContestantStatus.Active);
            if (cameraRig != null) cameraRig.ControlsEnabled = !blocked;
        }

        private static string CompetitionTitle(EpisodeState state)
        {
            var definition = CompetitionDefinitions.For(state);
            return AwardTitle(state.phase) + (definition != null ? " · " + definition.Title : "");
        }

        private void CompetitionBriefing(EpisodeState state)
        {
            hud.SetActivityLayout(EpisodeHud.ActivityLayout.Competition);
            var game = CompetitionMiniGames.For(EpisodeEngine.CompetitionCategory(state));
            var field = EpisodeEngine.CompetitionPlayers(state).ToArray();
            var definition = CompetitionDefinitions.For(state);
            hud.Heading(CompetitionTitle(state) + " · " + EpisodeEngine.CompetitionCategory(state));
            hud.Paragraph(state.phase == EpisodePhase.Veto ? "At stake: the power to save a nominee from eviction."
                : state.phase == EpisodePhase.HoH ? "At stake: Head of Household safety and nomination power."
                : "At stake: progress toward the final Head of Household decision.");
            hud.Paragraph("Competing: " + string.Join(", ", field.Select(c => c.name)) + ".");
            var excluded = state.Active.Where(c => !field.Any(p => p.id == c.id)).ToArray();
            foreach (var c in excluded)
                hud.Paragraph(c.name + (state.phase == EpisodePhase.Veto ? ": not drawn for this veto field."
                    : state.phase == EpisodePhase.FinalHoHPart2 ? ": already qualified by winning part one."
                    : state.phase == EpisodePhase.FinalHoHPart3 ? ": did not qualify for this round."
                    : ": outgoing HoH is ineligible this week."));
            hud.Paragraph(CompetitionMiniGames.Brief(game, state.competitionRulesVersion));
            if (definition != null) hud.Paragraph(definition.Summary);
            hud.Paragraph("Rules " + state.competitionRulesVersion + ". Practice cannot alter your season. Ranked attempts use the same board after cancel or reload. "
                + "Pause stops the clock, including when this window loses focus.");
            hud.Paragraph(state.phase == EpisodePhase.FinalHoHPart1
                ? "Performance adds 0–2 effective endurance points, capped at 10, for this competition only. Statistics and seeded survival rolls still matter; full marks do not guarantee a win."
                : "Performance adds a 0–2 point bonus. Character statistics and seeded rolls determine the remaining score; full marks do not guarantee a win.");
            if (state.competitionRulesVersion >= 3)
                hud.Paragraph("Every entry route keeps the same earned bonuses: preparation " + state.playerStudyBonus
                    + ", event " + state.phaseEventCompBonus + ", storyline " + Storylines.CompetitionBonus(state)
                    + ". Playing or the accessible alternative adds performance on top; preparation is retained.");
            hud.Action("Practice this competition", () => StartChallenge(state, true));
            hud.Action(CompetitionMiniGames.EnterCaption(game), () => StartChallenge(state));
            hud.Action("Accessible alternative: steady 1-point bonus", () => Commit(state, EpisodeCommandKind.Compete, performance: .5));
            if (state.phase == EpisodePhase.HoH || state.phase == EpisodePhase.Veto)
            {
                hud.Paragraph(state.competitionRulesVersion >= 3
                    ? "Simulate: the same statistics, earned bonuses and seeded rolls as playing, with zero performance bonus."
                    : "Simulate: weighted statistics plus preparation " + state.playerStudyBonus + "/5 and event bonus "
                        + state.phaseEventCompBonus + ". No minigame performance bonus. Preparation is retained for later weeks.");
                hud.Action(EpisodeHud.SimulateCompetitionCaption, () => SimulateCompetition(state));
                hud.Paragraph("Throw: receive zero performance bonus. Your statistics and seeded rolls still count, so you may still win.");
                hud.Action(EpisodeHud.ThrowCompetitionCaption, () => ThrowCompetition(state));
            }
        }

        private void StartChallenge(EpisodeState state) => StartChallenge(state, false);

        private void StartChallenge(EpisodeState state, bool practice)
        {
            if (!phaseOpen || challengeActive || !IsCurrentDiaryRevision(state) || state.competitionResolved
                || !EpisodeEngine.IsCompetition(state.phase)
                || !EpisodeEngine.CompetitionPlayers(state).Any(c => c.isPlayer)) return;
            challengeOrigin = state; challengeActive = true; challengeHits = 0; challengeTotal = 0;
            challengeStarted = Time.unscaledTime; challengePractice = practice; challengeResultShown = false;
            challengeCommandId = Guid.NewGuid().ToString("N");
            if (!BeginCompetitionArena(state))
            { challengeActive = false; challengeOrigin = null; message = competitionArenaStatus; Render(); return; }
            FrameCompetition();
            var kind = CompetitionMiniGames.For(EpisodeEngine.CompetitionCategory(state));
            challengeRun = kind == CompetitionMiniGames.Kind.Precision ? null : new MiniGameRun(kind,
                CompetitionMiniGames.AttemptSeed(state.seed, state.week, (int)state.phase, state.competitionRulesVersion, practice),
                state.competitionRulesVersion, CompetitionDefinitions.For(state));
            if (challengeRun != null)
            {
                if (competitionScreen == null)
                {
                    competitionScreen = CompetitionGameScreen.Attach(gameObject);
                    hud.RegisterOverlay(competitionScreen.GetComponent<CanvasGroup>());
                }
                competitionScreen.FontScale = largeText ? 1.2f : 1f;
                competitionScreen.Show(challengeRun, CompetitionTitle(state),
                    string.Join("\n", EpisodeEngine.CompetitionPlayers(state).Select(c => c.name + (c.isPlayer ? " (You)" : ""))),
                    practice, FlipCard, TapTarget, TapDirection, ToggleChallengeGrip, CancelChallenge, MissReactionTarget, !reducedMotion);
            }
            audioBed.PlayCue(HouseAudio.Cue.CompetitionStart); Render();
            competitionAssemblyHudHidden = competitionScreen != null && competitionScreen.IsAssembling;
            if (competitionAssemblyHudHidden) hud.SetVisible(false);
        }

        private void ToggleChallengeGrip()
        {
            if (competitionScreen != null && competitionScreen.IsPlaying && challengeRun != null)
                challengeRun.SetHolding(!challengeRun.Holding);
        }

        private void TickMiniGame()
        {
            if (challengeRun == null || competitionScreen == null || challengeResultShown) return;
            competitionScreen.SetArenaStatus(competitionArenaStatus);
            if (competitionScreen.IsAssembling)
            { competitionScreen.AdvanceAssembly(Time.unscaledDeltaTime, CompetitionArenaReady); return; }
            RestoreCompetitionAssemblyHud();
            if (!CompetitionArenaReady) { competitionScreen.HoldReady("Houseguests are taking their places"); return; }
            if (!competitionScreen.AdvanceReady(Time.unscaledDeltaTime)) return;
            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
            if (challengeRun.Kind == CompetitionMiniGames.Kind.Endurance)
            {
                if ((keyboard != null && keyboard.spaceKey.wasPressedThisFrame) || (pad != null && pad.rightTrigger.wasPressedThisFrame))
                    challengeRun.SetHolding(true);
                if ((keyboard != null && keyboard.spaceKey.wasReleasedThisFrame) || (pad != null && pad.rightTrigger.wasReleasedThisFrame))
                    challengeRun.SetHolding(false);
            }
            else if (challengeRun.Kind == CompetitionMiniGames.Kind.Reaction
                && challengeRun.RulesVersion == CompetitionMiniGames.LegacyRules
                && keyboard != null && keyboard.spaceKey.wasPressedThisFrame) TapTarget();
            challengeRun.Tick(Time.unscaledDeltaTime);
            competitionScreen.Refresh();
            if (!challengeRun.Finished) return;
            challengeResultShown = true;
            if (challengePractice)
                competitionScreen.ShowFinished("Practice complete. No competition result or season state was changed.", "Return to briefing", CancelChallenge);
            else CommitMiniGame();
        }

        public void TapTarget()
        {
            if (!challengeActive || challengeRun == null || competitionScreen == null || !competitionScreen.IsPlaying) return;
            bool hit = challengeRun.RulesVersion >= CompetitionMiniGames.ImprovedRules
                ? challengeRun.Tap(challengeRun.TargetDirection) : challengeRun.Tap();
            if (hit) audioBed.PlayCue(HouseAudio.Cue.Button);
            competitionScreen.Refresh();
        }

        private void TapDirection(MiniGameRun.Direction direction)
        {
            if (!challengeActive || challengeRun == null || competitionScreen == null || !competitionScreen.IsPlaying) return;
            if (challengeRun.Tap(direction)) audioBed.PlayCue(HouseAudio.Cue.Button);
            competitionScreen.Refresh();
        }

        private void MissReactionTarget()
        {
            if (!challengeActive || challengeRun == null || competitionScreen == null || !competitionScreen.IsPlaying) return;
            challengeRun.MissPointer(); competitionScreen.Refresh();
        }

        public void FlipCard(int index)
        {
            if (!challengeActive || challengeRun == null || competitionScreen == null || !competitionScreen.IsPlaying) return;
            if (challengeRun.Flip(index)) audioBed.PlayCue(HouseAudio.Cue.Button);
            competitionScreen.Refresh();
            // The frame loop owns completion, so a submit cannot dismiss the result it created.
        }

        private void CommitMiniGame()
        {
            if (challengeRun == null || !challengeRun.Finished || challengePractice || challengeCommitting) return;
            if (!IsCurrentDiaryRevision(challengeOrigin))
            {
                competitionScreen.ShowFinished("The episode changed during this attempt. Return to the briefing to refresh.", "Return to briefing", CancelChallenge);
                return;
            }
            challengeCommitting = true;
            EndCompetitionArena();
            challengeActive = false;
            var result = Submit(new EpisodeCommand { id = challengeCommandId, actorId = challengeOrigin.playerId,
                expectedPhase = challengeOrigin.phase, expectedRevision = challengeOrigin.revision,
                kind = EpisodeCommandKind.Compete, performance = challengeRun.Performance });
            challengeCommitting = false;
            if (!result.accepted)
            {
                challengeActive = true;
                competitionScreen.ShowFinished("Result was not committed: " + result.reason, "Retry saving this result", CommitMiniGame);
                Render(); return;
            }
            competitionScreen.Hide(); challengeRun = null; challengeOrigin = null;
            if (cameraRig != null) cameraRig.ReleaseShot(CompetitionShotSeconds);
        }

        private void CancelChallenge()
        {
            CloseCompetitionPresentation();
            if (cameraRig != null) cameraRig.ReleaseShot(CompetitionShotSeconds);
            Render();
        }

        private void RestoreCompetitionAssemblyHud()
        {
            if (!competitionAssemblyHudHidden) return;
            competitionAssemblyHudHidden = false; hud?.SetVisible(true);
        }

        private void CloseCompetitionPresentation()
        {
            RestoreCompetitionAssemblyHud();
            EndCompetitionArena();
            competitionScreen?.Hide(); challengeRun = null; challengeOrigin = null;
            challengeActive = false; challengeResultShown = false; challengeCommitting = false;
        }

        private string CompetitionPerformanceExplanation(EpisodeState state)
        {
            var explanation = state.events.LastOrDefault(e => e.week == state.week && e.phase == state.phase && e.kind == "competition-performance");
            if (explanation != null) return explanation.text;
            var result = state.events.LastOrDefault(e => e.week == state.week && e.phase == state.phase && e.kind == "competition");
            return result != null && result.text.Contains("(simulated)")
                ? "Simulated result: weighted statistics, preparation and event bonuses, and seeded rolls. No minigame bonus was used."
                : "Committed scores combine character statistics, competition modifiers and seeded rolls. Minigame performance adds up to two points; it does not guarantee a win.";
        }

        private void ReviewCompetitionResult(EpisodeState state)
        {
            if (competitionCard == null || !state.competitionResolved) return;
            competitionCard.Play(CompetitionTitle(state), EpisodeEngine.CompetitionCategory(state), state.week,
                CompetitionStandings(state), reducedMotion, CompetitionPerformanceExplanation(state));
        }
        /// <summary>
        /// The competition wide (V5): the yard from the house's side, over the wall line, the
        /// lanes and the ring in the middle of the frame. Taken when the minigame starts, let go
        /// when it commits or is abandoned; the result's card then frames the yard its own way.
        /// Reduced motion keeps the viewer's shot, as every automatic framing does.
        /// </summary>
        public const float CompetitionShotDistance = 12f;
        public const float CompetitionShotPitch = 18f;
        public const float CompetitionShotFieldOfView = 50f;
        public const float CompetitionShotSeconds = 1.2f;

        private void FrameCompetition()
        {
            if (cameraRig == null || cameraRig.ReducedMotion) return;
            cameraRig.MoveTo(new HouseCameraRig.Shot
            {
                Focus = StationPosition + Vector3.up, Distance = CompetitionShotDistance, Pitch = CompetitionShotPitch, Yaw = 0f,
                FieldOfView = CompetitionShotFieldOfView, Seconds = CompetitionShotSeconds,
            });
        }

        private void ChallengePanel()
        {
            if (challengeRun != null) return;
            hud.Paragraph("Press Space or STOP when the marker is near the center. Three attempts; no time limit. Escape cancels without committing.");
            hud.ChallengeMeter(); hud.Action("STOP marker  [Space]", RecordChallengeHit);
        }
        public void RecordChallengeHit()
        {
            if (!challengeActive || challengeRun != null) return;
            challengeTotal += Math.Max(0, 1 - Math.Abs(challengeValue - .5) * 2); challengeHits++;
            audioBed.PlayCue(HouseAudio.Cue.Button);
            if (challengeHits < 3) return;
            if (challengePractice) { message = "Practice complete. Your season is unchanged."; CancelChallenge(); return; }
            EndCompetitionArena();
            challengeActive = false; Commit(challengeOrigin, EpisodeCommandKind.Compete, performance:challengeTotal / 3);
            if (cameraRig != null) cameraRig.ReleaseShot(CompetitionShotSeconds);
        }
    }
}
