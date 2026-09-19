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
                    CharacterPortraits.Get(
                        CharacterPresentation.AppearanceId(actor, ContentCatalog.CanonicalId(actor.id)))));
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

        private void StartChallenge(EpisodeState state)
        {
            challengeOrigin = state; challengeActive = true; challengeHits = 0; challengeTotal = 0; challengeStarted = Time.unscaledTime;
            FrameCompetition();
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
            var hit = cameraRig != null ? cameraRig.Actions.Hit : null;
            if (hit != null)
            {
                // Edge-triggered rather than level-triggered, so the panel's hold and release
                // buttons keep working when a keyboard is attached: polling isPressed every
                // frame overwrote a grip taken with the button on the very next tick.
                if (challengeRun.Kind == CompetitionMiniGames.Kind.Endurance)
                {
                    if (hit.WasPressedThisFrame()) challengeRun.SetHolding(true);
                    else if (hit.WasReleasedThisFrame()) challengeRun.SetHolding(false);
                }
                else if (challengeRun.Kind == CompetitionMiniGames.Kind.Reaction && hit.WasPressedThisFrame())
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
            if (cameraRig != null) cameraRig.ReleaseShot(CompetitionShotSeconds);
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
            if (cameraRig != null) cameraRig.ReleaseShot(CompetitionShotSeconds);
        }
    }
}
