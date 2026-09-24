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
        // A ranked attempt's finish plate stays up this long before it commits, so the player reads
        // their own result before the standings replace it. Reading time, so reduced motion keeps it.
        private const float RankedFinishSeconds = .9f;
        private float challengeFinishHold;
        private string pendingAttemptLine;
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

        /// <summary>
        /// The competition's briefing, as the style guide's modal draws it: the hero card (the
        /// player's face, the discipline, the competition's name, the stakes), how it is played in
        /// a short paragraph, four facts in a row, practice as the one primary action beside the
        /// full rules, and the ways to compete for real under them. Every line is the game's own:
        /// the paragraphs the briefing used to be are the full rules, and what Simulate and Throw
        /// mean is said on their cards rather than in paragraphs above them.
        /// </summary>
        private void CompetitionBriefing(EpisodeState state)
        {
            hud.SetActivityLayout(EpisodeHud.ActivityLayout.Competition);
            FrameBriefing();
            var game = CompetitionMiniGames.For(EpisodeEngine.CompetitionCategory(state));
            var field = EpisodeEngine.CompetitionPlayers(state).ToArray();
            var definition = CompetitionDefinitions.For(state);
            string stakes = state.phase == EpisodePhase.Veto ? "At stake: the power to save a nominee from eviction."
                : state.phase == EpisodePhase.HoH ? "At stake: Head of Household safety and nomination power."
                : "At stake: progress toward the final Head of Household decision.";
            hud.CompetitionHero(state.Find(state.playerId), EpisodeEngine.CompetitionCategory(state),
                definition?.Title ?? CompetitionMiniGames.DisplayName(game), stakes);
            hud.CompetitionBrief(CompetitionMiniGames.Brief(game, state.competitionRulesVersion));
            hud.CompetitionFacts(BriefingFacts(state, game, definition));

            // Who sits this one out, and why: the strip shows everyone, not who is playing.
            var excluded = state.Active.Where(c => !field.Any(p => p.id == c.id)).ToArray();
            foreach (var c in excluded)
                hud.Aside(c.name + (state.phase == EpisodePhase.Veto ? ": not drawn for this veto field."
                    : state.phase == EpisodePhase.FinalHoHPart2 ? ": already qualified by winning part one."
                    : state.phase == EpisodePhase.FinalHoHPart3 ? ": did not qualify for this round."
                    : ": outgoing HoH is ineligible this week."));

            var rules = new List<string>
            {
                "Competing: " + string.Join(", ", field.Select(c => c.name)) + ".",
                definition?.Summary,
                "Rules " + state.competitionRulesVersion + ". Practice cannot alter your season. Ranked attempts use the same board after cancel or reload. "
                    + "Pause stops the clock, including when this window loses focus.",
                state.phase == EpisodePhase.FinalHoHPart1
                    ? "Performance adds 0–2 effective endurance points, capped at 10, for this competition only. Statistics and seeded survival rolls still matter; full marks do not guarantee a win."
                    : "Performance adds a 0–2 point bonus. Character statistics and seeded rolls determine the remaining score; full marks do not guarantee a win.",
            };
            if (state.competitionRulesVersion >= 3)
                rules.Add("Every entry route keeps the same earned bonuses: preparation " + state.playerStudyBonus
                    + ", event " + state.phaseEventCompBonus + ", storyline " + Storylines.CompetitionBonus(state)
                    + ". Playing or the accessible alternative adds performance on top; preparation is retained.");
            hud.CompetitionActions("Practice this competition", () => StartChallenge(state, true), rules);

            hud.Section("COMPETE FOR REAL");
            hud.OptionCard(CompetitionMiniGames.EnterCaption(game),
                "Ranked. The same board after a cancel or a reload; pause stops the clock.", "trophy",
                () => StartChallenge(state), compact: true);
            hud.OptionCard("Accessible alternative: steady 1-point bonus",
                "No timing needed: a steady 1-point performance bonus in place of the minigame.", "star",
                () => Commit(state, EpisodeCommandKind.Compete, performance: .5), compact: true);
            if (state.phase == EpisodePhase.HoH || state.phase == EpisodePhase.Veto)
            {
                hud.OptionCard(EpisodeHud.SimulateCompetitionCaption, state.competitionRulesVersion >= 3
                        ? "The same statistics, earned bonuses and seeded rolls as playing, with zero performance bonus."
                        : "Weighted statistics plus preparation " + state.playerStudyBonus + "/5 and event bonus "
                            + state.phaseEventCompBonus + ". No minigame performance bonus; preparation is retained for later weeks.",
                    "dumbbell", () => SimulateCompetition(state), compact: true);
                hud.OptionCard(EpisodeHud.ThrowCompetitionCaption,
                    "Zero performance bonus. Your statistics and seeded rolls still count, so you may still win.", "exit",
                    () => ThrowCompetition(state), compact: true);
            }
        }

        /// <summary>
        /// The competition for a houseguest who is not in its field: the same full-screen briefing -
        /// the arena beside the sheet, what is at stake, how it is played, who is competing - with
        /// the one thing there is to do, which is to watch it.
        /// </summary>
        private void SpectatorBriefing(EpisodeState state)
        {
            hud.SetActivityLayout(EpisodeHud.ActivityLayout.Competition);
            FrameBriefing();
            var game = CompetitionMiniGames.For(EpisodeEngine.CompetitionCategory(state));
            var field = EpisodeEngine.CompetitionPlayers(state).ToArray();
            var definition = CompetitionDefinitions.For(state);
            string stakes = state.phase == EpisodePhase.Veto ? "At stake: the power to save a nominee from eviction."
                : state.phase == EpisodePhase.HoH ? "At stake: Head of Household safety and nomination power."
                : "At stake: progress toward the final Head of Household decision.";
            hud.CompetitionHero(state.Find(state.playerId), EpisodeEngine.CompetitionCategory(state),
                definition?.Title ?? CompetitionMiniGames.DisplayName(game), stakes);
            hud.CompetitionBrief(CompetitionMiniGames.Brief(game, state.competitionRulesVersion));
            hud.Paragraph("Competing: " + string.Join(", ", field.Select(c => c.name)) + ". You are not in this field.");
            hud.Action("Watch eligible housemates compete", () => Commit(state, EpisodeCommandKind.Advance));
        }

        /// <summary>
        /// The briefing's four facts: the game's twist where its variant has one (otherwise what
        /// the board is), the clock, pausing, and what performance is worth - each from the rules
        /// the game runs, never a number written for the card.
        /// </summary>
        private static IList<EpisodeHud.BriefingFact> BriefingFacts(EpisodeState state, CompetitionMiniGames.Kind game,
            CompetitionDefinition definition)
        {
            var facts = new List<EpisodeHud.BriefingFact>();
            double seconds = definition?.Duration ?? CompetitionMiniGames.TimeLimit(game);
            var target = UiTheme.Icon("target");
            if (definition != null && definition.PreviewSeconds > 0)
                facts.Add(new EpisodeHud.BriefingFact(UiTheme.Icon("eye"), definition.PreviewSeconds.ToString("0") + " second preview", "Study the cards"));
            else if (definition != null && definition.Pattern == CompetitionPattern.AlternatingWindows)
                facts.Add(new EpisodeHud.BriefingFact(target, "Changing windows", "0.65 or 1.15 seconds"));
            else if (definition != null && definition.Pattern == CompetitionPattern.PressureWaves)
                facts.Add(new EpisodeHud.BriefingFact(UiTheme.Pack("Pack4_Presentation/Icons_PNG/fire"), "Pressure waves", "1.5 seconds, every 6"));
            else switch (game)
            {
                case CompetitionMiniGames.Kind.Memory:
                    facts.Add(new EpisodeHud.BriefingFact(UiTheme.Pack("Pack1_Foundation/Icons_PNG/grid"), "Eight pairs", "A 4 by 4 board")); break;
                case CompetitionMiniGames.Kind.Reaction:
                    facts.Add(new EpisodeHud.BriefingFact(target, "Every target", "Hit it before it goes")); break;
                case CompetitionMiniGames.Kind.Endurance:
                    facts.Add(new EpisodeHud.BriefingFact(UiTheme.Icon("dumbbell"), "Grip and recover", "Hold to earn time")); break;
                default:
                    facts.Add(new EpisodeHud.BriefingFact(target, "Three stops", "Near the centre")); break;
            }
            var clock = UiTheme.Pack("Pack3_Systems/Icons_PNG/history");
            if (seconds > 0)
            {
                string goal = game == CompetitionMiniGames.Kind.Memory ? "Match eight pairs"
                    : game == CompetitionMiniGames.Kind.Reaction ? "Hit the targets"
                    : state.competitionRulesVersion >= CompetitionMiniGames.ImprovedRules
                        ? "Hold " + (seconds * .65).ToString("0.#") + " for full marks" : "Hold as long as you can";
                facts.Add(new EpisodeHud.BriefingFact(clock, seconds.ToString("0") + " seconds", goal));
                facts.Add(EpisodeHud.BriefingFact.PauseMark("Pause any time", "Stops the clock"));
            }
            else
            {
                facts.Add(new EpisodeHud.BriefingFact(clock, "No time limit", "Three attempts"));
                facts.Add(EpisodeHud.BriefingFact.PauseMark("Escape cancels", "Nothing is committed"));
            }
            facts.Add(new EpisodeHud.BriefingFact(UiTheme.Icon("star"), "Performance bonus",
                state.phase == EpisodePhase.FinalHoHPart1 ? "Adds 0–2 endurance" : "Adds 0–2 points"));
            return facts;
        }

        private void StartChallenge(EpisodeState state) => StartChallenge(state, false);

        private void StartChallenge(EpisodeState state, bool practice)
        {
            if (!phaseOpen || challengeActive || !IsCurrentDiaryRevision(state) || state.competitionResolved
                || !EpisodeEngine.IsCompetition(state.phase)
                || !EpisodeEngine.CompetitionPlayers(state).Any(c => c.isPlayer)) return;
            challengeOrigin = state; challengeActive = true; challengeHits = 0; challengeTotal = 0;
            challengeStarted = Time.unscaledTime; challengePractice = practice; challengeResultShown = false; challengeFinishHold = 0f;
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
                    // The screen's own beats - the count, GO, the last seconds, a wave - on the house's audio.
                    competitionScreen.CueRequested += cue => audioBed.PlayCue(cue);
                }
                competitionScreen.FontScale = largeText ? 1.2f : 1f;
                competitionScreen.ReducedMotion = reducedMotion;
                var field = EpisodeEngine.CompetitionPlayers(state).ToList();
                competitionScreen.Show(challengeRun, CompetitionTitle(state),
                    string.Join("\n", field.Select(c => HudPrimitives.WithYou(c.name, c.isPlayer))),
                    practice, FlipCard, TapTarget, TapDirection, ToggleChallengeGrip, CancelChallenge, MissReactionTarget, !reducedMotion,
                    field.Select(c => new CompetitionEntrant(c.id, c.name, c.isPlayer, CharacterPortraits.Get(c), c)).ToList());
            }
            // The start sting belongs to GO now, when input goes live; opening the attempt is a panel.
            audioBed.PlayCue(challengeRun != null ? HouseAudio.Cue.PanelOpen : HouseAudio.Cue.CompetitionStart); Render();
            // The competition is the whole screen for the whole attempt - the walk to the stations
            // and the game after it - so the house's HUD stands down until it commits or is left.
            competitionAssemblyHudHidden = competitionScreen != null && competitionScreen.IsShowing;
            if (competitionAssemblyHudHidden) hud.SetVisible(false);
        }

        private void ToggleChallengeGrip()
        {
            if (competitionScreen != null && competitionScreen.IsPlaying && challengeRun != null)
                challengeRun.SetHolding(!challengeRun.Holding);
        }

        private void TickMiniGame()
        {
            if (challengeRun == null || competitionScreen == null) return;
            if (challengeFinishHold > 0f)
            {
                challengeFinishHold -= Time.unscaledDeltaTime;
                if (challengeFinishHold <= 0f) { challengeFinishHold = 0f; CommitMiniGame(); }
                return;
            }
            if (challengeResultShown) return;
            competitionScreen.SetArenaStatus(competitionArenaStatus);
            if (competitionScreen.IsAssembling)
            { competitionScreen.AdvanceAssembly(Time.unscaledDeltaTime, CompetitionArenaReady); return; }
            // The arena gates the start, not a game under way: a houseguest stepping off their mark
            // mid-attempt used to freeze the clock while taps still scored.
            if (!CompetitionArenaReady && !competitionScreen.IsPlaying) { competitionScreen.HoldReady("Houseguests are taking their places"); return; }
            if (!competitionScreen.AdvanceReady(Time.unscaledDeltaTime)) return;
            var keyboard = Keyboard.current;
            if (challengeRun.Kind == CompetitionMiniGames.Kind.Endurance) competitionScreen.SyncHoldKey();
            else if (challengeRun.Kind == CompetitionMiniGames.Kind.Reaction
                && challengeRun.RulesVersion == CompetitionMiniGames.LegacyRules
                && keyboard != null && keyboard.spaceKey.wasPressedThisFrame) TapTarget();
            int expired = challengeRun.ExpiredTargets;
            challengeRun.Tick(Time.unscaledDeltaTime);
            if (challengeRun.ExpiredTargets > expired) audioBed.PlayCue(HouseAudio.Cue.SocialDown);
            competitionScreen.Refresh();
            if (!challengeRun.Finished) return;
            challengeResultShown = true;
            if (challengePractice)
                competitionScreen.ShowFinished("Practice complete. No competition result or season state was changed.", "Return to briefing", CancelChallenge, hideCancel: true);
            else
            {
                competitionScreen.ShowRankedFinish("Your result goes to the standings in a moment. Scores commit once.");
                challengeFinishHold = RankedFinishSeconds;
            }
        }

        public void TapTarget()
        {
            if (!challengeActive || challengeRun == null || competitionScreen == null || !competitionScreen.IsPlaying) return;
            int early = challengeRun.FalseStarts, wrong = challengeRun.WrongDirections;
            bool hit = challengeRun.RulesVersion >= CompetitionMiniGames.ImprovedRules
                ? challengeRun.Tap(challengeRun.TargetDirection) : challengeRun.Tap();
            ReactionCue(hit, early, wrong);
            competitionScreen.Refresh();
        }

        private void TapDirection(MiniGameRun.Direction direction)
        {
            if (!challengeActive || challengeRun == null || competitionScreen == null || !competitionScreen.IsPlaying) return;
            int early = challengeRun.FalseStarts, wrong = challengeRun.WrongDirections;
            ReactionCue(challengeRun.Tap(direction), early, wrong);
            competitionScreen.Refresh();
        }

        /// <summary>A hit rises, a miss falls, an early press ticks; a press the rules ignore is silent.</summary>
        private void ReactionCue(bool hit, int earlyBefore, int wrongBefore)
        {
            if (hit) audioBed.PlayCue(HouseAudio.Cue.SocialUp);
            else if (challengeRun.FalseStarts > earlyBefore) audioBed.PlayCue(HouseAudio.Cue.Hover);
            else if (challengeRun.WrongDirections > wrongBefore) audioBed.PlayCue(HouseAudio.Cue.SocialDown);
        }

        private void MissReactionTarget()
        {
            if (!challengeActive || challengeRun == null || competitionScreen == null || !competitionScreen.IsPlaying) return;
            int early = challengeRun.FalseStarts, missed = challengeRun.PointerMisses;
            challengeRun.MissPointer();
            if (challengeRun.PointerMisses > missed) audioBed.PlayCue(HouseAudio.Cue.SocialDown);
            else if (challengeRun.FalseStarts > early) audioBed.PlayCue(HouseAudio.Cue.Hover);
            competitionScreen.Refresh();
        }

        public void FlipCard(int index)
        {
            if (!challengeActive || challengeRun == null || competitionScreen == null || !competitionScreen.IsPlaying) return;
            int pairs = challengeRun.MatchedPairs, mistakes = challengeRun.WrongFlips;
            if (challengeRun.Flip(index))
                // A pair rises, a miss falls, a first card is just a card.
                audioBed.PlayCue(challengeRun.MatchedPairs > pairs ? HouseAudio.Cue.SocialUp
                    : challengeRun.WrongFlips > mistakes ? HouseAudio.Cue.SocialDown : HouseAudio.Cue.Button);
            competitionScreen.Refresh();
            // The frame loop owns completion, so a submit cannot dismiss the result it created.
        }

        private void CommitMiniGame()
        {
            if (challengeRun == null || !challengeRun.Finished || challengePractice || challengeCommitting) return;
            if (!IsCurrentDiaryRevision(challengeOrigin))
            {
                competitionScreen.ShowFinished("The episode changed during this attempt. Return to the briefing to refresh.", "Return to briefing", CancelChallenge, hideCancel: true);
                return;
            }
            challengeCommitting = true;
            // Said on the standings card the commit opens, then forgotten.
            pendingAttemptLine = CompetitionGameScreen.AttemptLine(challengeRun);
            EndCompetitionArena();
            challengeActive = false;
            var result = Submit(new EpisodeCommand { id = challengeCommandId, actorId = challengeOrigin.playerId,
                expectedPhase = challengeOrigin.phase, expectedRevision = challengeOrigin.revision,
                kind = EpisodeCommandKind.Compete, performance = challengeRun.Performance });
            challengeCommitting = false;
            pendingAttemptLine = null;
            if (!result.accepted)
            {
                challengeActive = true;
                competitionScreen.ShowFinished("Result was not committed: " + result.reason, "Retry saving this result", CommitMiniGame);
                Render(); return;
            }
            competitionScreen.Hide(); challengeRun = null; challengeOrigin = null;
            RestoreCompetitionAssemblyHud();
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
            challengeActive = false; challengeResultShown = false; challengeCommitting = false; challengeFinishHold = 0f;
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

        /// <summary>
        /// How far left of the arena the briefing's shot looks, in metres: the set stands in the
        /// free area right of the briefing's card rather than behind it.
        /// </summary>
        public const float BriefingShotShift = 3.75f;
        private bool briefingFramed;

        /// <summary>Whether the camera is on the arena for the briefing, for a test.</summary>
        public bool IsFramingBriefing => briefingFramed && phaseOpen && !challengeActive && cameraRig != null && cameraRig.HasShot;

        /// <summary>
        /// The briefing looks at the competition's set, as the mockup's frames the arena beside its
        /// card: the competition's own shot, moved over. Taken once a briefing, let go with the panel
        /// as every panel's shot is, and replaced by the competition's own when an attempt starts.
        /// </summary>
        private void FrameBriefing()
        {
            if (cameraRig == null || cameraRig.ReducedMotion || !phaseOpen || challengeActive) return;
            if (briefingFramed && cameraRig.HasShot) return;
            var station = StationPosition;
            if (float.IsInfinity(station.x)) return;
            briefingFramed = true;
            // Stand the arena in the middle of the frame the briefing's sheet leaves: its offset
            // from the frame's centre, in half-heights, is metres at the shot's distance through
            // the vertical field of view. The fixed shift is the fallback when the layout gave none.
            float offset = hud != null ? hud.ArenaWindowOffset : 0f;
            float shift = offset > 0f
                ? offset * CompetitionShotDistance * Mathf.Tan(CompetitionShotFieldOfView * .5f * Mathf.Deg2Rad)
                : BriefingShotShift;
            cameraRig.MoveTo(new HouseCameraRig.Shot
            {
                Focus = station + Vector3.up - Vector3.right * shift, Distance = CompetitionShotDistance,
                Pitch = CompetitionShotPitch, Yaw = 0f, FieldOfView = CompetitionShotFieldOfView, Seconds = CompetitionShotSeconds,
            });
        }

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
            // While a game is being played its screen is the decision: the panel and the column
            // step aside rather than sit, empty, under a competition that no longer hides them.
            if (challengeRun != null) { hud.StandAsideForPlay(); return; }
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
