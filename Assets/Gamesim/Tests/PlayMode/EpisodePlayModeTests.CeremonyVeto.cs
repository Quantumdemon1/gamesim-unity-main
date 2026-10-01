using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The veto meeting, staged (PACK8-PASS-PLAN C1). It played as a three-second card on the HUD
    /// over a framing of the game room. Now the house takes its seats in the living room - the
    /// block before the meeting in the red chairs, the holder and the Head of Household on the U's
    /// two middle base seats facing them - and the meeting plays on the living room's screen page
    /// by page, the camera cutting with its beats. A skip gives up the order, never the outcome; a
    /// batch run and reduced motion keep the card on the HUD as it was.
    ///
    /// <para>Every wait is bounded in real seconds: the stage and the card run on the unscaled clock.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>Submits the house's next legal decisions until the veto meeting is committed: staged, or its card up on the HUD.</summary>
        private IEnumerator PlayUntilTheVetoMeeting(int steps = 6)
        {
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            for (int i = 0; i < steps && !director.IsCeremonyStaged; i++)
            {
                if (takeover.IsPlaying && takeover.PlayingKind == CeremonySting.VetoKind) yield break;
                var result = director.Submit(NextCommand(director.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
                yield return null;
            }
        }

        /// <summary>A staged veto meeting skipped as a player skips it - the summons, then the card - and the house running on after it.</summary>
        private IEnumerator SkipTheStagedVetoMeeting(string what)
        {
            Assert.That(director.CeremonyStageKind, Is.EqualTo(CeremonySting.VetoKind), what + ": the veto meeting is staged.");
            director.SkipCeremonySummons();
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing || !director.IsCeremonyStaged, 3f,
                what + ": the meeting plays once the summons is skipped");
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            if (takeover.PlayingMeeting) takeover.Cancel();
            yield return AssertTheHouseRunsOn(what);
        }

        /// <summary>The meeting's faces on its page: the live copies, a page turned in this frame leaving the old ones to be destroyed at its end.</summary>
        private static List<RectTransform> MeetingFacesUp(CeremonyTakeover takeover) => takeover.GetComponentsInChildren<RectTransform>()
            .Where(rect => rect.name == "Meeting face").ToList();

        [UnityTest]
        public IEnumerator CeremonyStage_TheVetoMeetingSeatsTheBlockInTheRedChairsAndPlaysOnTheLivingScreen()
        {
            yield return InstallStagedSeason(71, state => AtVetoMeeting(state, playerHolds: false));
            var before = director.Snapshot;
            var block = before.nominees.ToList();
            string holder = before.vetoHolderId, hoh = before.hohId;
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            yield return PlayUntilTheVetoMeeting();
            var committed = director.Snapshot;
            Assert.That(director.IsCeremonyStaged, Is.True, "The veto meeting is staged in the house.");
            Assert.That(director.CeremonyStageKind, Is.EqualTo(CeremonySting.VetoKind));
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Summons), "The house is summoned first.");
            Assert.That(takeover.IsPlaying, Is.False, "The card waits for the house to take its seats.");
            Assert.That(Hud.IsHeldForReveal, Is.True, "The chrome steps aside from the summons: it is drawn from the committed result.");
            Assert.That(director.CeremonyStageStanding, Is.Null, "Nobody stands at the head of a meeting held on the U.");

            // The red chairs are the block's as it stood before the meeting, whatever the meeting decided.
            string report = StageReport();
            foreach (var id in block)
                Assert.That(report, Does.Contain("  " + id + " -> " + CeremonySeating.HotSeat + " "), id + " has a red chair:\n" + report);
            // The two middle seats go by the side each comes from, so neither walks past the other's place.
            string middle0 = " -> " + CeremonySeating.GallerySeat + " 0 ", middle1 = " -> " + CeremonySeating.GallerySeat + " 1 ";
            Assert.That((report.Contains("  " + holder + middle0) && report.Contains("  " + hoh + middle1))
                || (report.Contains("  " + holder + middle1) && report.Contains("  " + hoh + middle0)), Is.True,
                "The holder and the Head of Household take the U's two middle base seats, facing the red chairs:\n" + report);
            Assert.That(report, Does.Not.Contain(" -> " + CeremonySeating.LivingMark + " "), "Nobody stands in a house the U seats.");

            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing,
                EpisodeDirector.SummonsHardSeconds(CeremonyPace.Suspenseful) + 6f, "the meeting plays once the seats have filled or the summons has run its course");
            Assert.That(takeover.PlayingMeeting, Is.True, "The meeting plays on a set's screen,");
            var screen = director.CeremonyStageScreen;
            Assert.That(takeover.Surface, Is.SameAs(screen), "the stage's own,");
            Assert.That(screen.Room, Is.EqualTo("Living"), "in the living room.");
            var canvas = takeover.GetComponent<Canvas>();
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace), "The card is a world-space canvas on the screen's face,");
            Assert.That(Vector3.Distance(canvas.transform.position, screen.Centre), Is.LessThan(0.1f), "hung on it.");
            Assert.That(takeover.Page, Is.EqualTo(CeremonyTakeover.MeetingPage.Intro), "It opens on the holder and the block,");
            Assert.That(MeetingFacesUp(takeover).Count, Is.EqualTo(3), "three faces: the holder and the two on the block.");

            // Seated, at the hips: a seat moves the visual body onto the chair and leaves the root at the approach.
            // One wait for the whole house and the block's settling, well inside the meeting's own
            // clock: an unused veto closes about 13 seconds after the card starts, and the close lets
            // every seat go, so checks that ran on past it would read an empty room. The stage is
            // part of the wait because its counts read zero of zero once it has ended.
            var hot = CeremonySeating.Anchors(director.gameObject.scene, CeremonySeating.HotSeat);
            var redChairs = block.Select(id => SceneComponents<HouseNpc>().First(each => each.Id == id && each.gameObject.activeInHierarchy)).ToList();
            bool SettledInTheRedChairs() => director.IsCeremonyStaged && director.CeremonyStageSeated >= director.CeremonyStagePlaces
                && redChairs.All(npc =>
                {
                    var seat = npc.GetComponent<HouseSeatPresentation>();
                    return seat != null && seat.Settled;
                });
            float seatsBy = Time.realtimeSinceStartup + 8f;
            while (!SettledInTheRedChairs() && Time.realtimeSinceStartup < seatsBy) yield return null;
            // On a miss, the report as it stood when the wait gave up: who never sat, and why.
            if (!SettledInTheRedChairs())
                Assert.Fail("Everyone sits down, the block settled in the red chairs, while the meeting is on:\n" + StageReport("when the wait gave up"));
            foreach (var npc in redChairs)
            {
                var seat = npc.GetComponent<HouseSeatPresentation>();
                var hips = HipsOf(npc);
                var at = hips != null ? hips.position : seat.VisualFeet;
                Assert.That(hot.Min(chair => FlatDistance(at, chair.Position)), Is.LessThan(0.5f), npc.Id + " sits in a red chair.");
            }

            // The card down: the house is let go, the screen is the room's again, nothing was written.
            takeover.Cancel();
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the stage ends with the card");
            yield return Frames(2);
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay), "The card is off the screen.");
            Assert.That(Hud.IsHeldForReveal, Is.False, "The chrome is back.");
            Assert.That(player.HasActivityOwner, Is.False, "The player has their body back.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(committed.revision), "The stage commits nothing,");
            Assert.That(director.Snapshot.nominees, Is.EqualTo(committed.nominees), "and the block is the committed one.");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "The house's world outlives the meeting.");
        }

        /// <summary>
        /// A holder on the block keeps their red chair (PACK8-PASS-PLAN C1): the red chairs are the
        /// block's as it stood before the meeting, the holder's included, and the Head of Household
        /// takes the U's middle base seat the holder had no need of. The replacement sits wherever
        /// cast order puts them, so the room gives nothing away. The card shows the holder once, and
        /// an NPC holder on the block saves themselves, so the decision is about the holder.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_AHolderOnTheBlockKeepsTheirRedChairAndTheHeadOfHouseholdTakesTheFirstSeat()
        {
            yield return InstallStagedSeason(76, state =>
            {
                AtVetoMeeting(state, playerHolds: false);
                // The first nominee won the veto they are up against.
                string former = state.vetoHolderId;
                state.vetoHolderId = state.nominees[0];
                if (!state.vetoPlayers.Contains(state.vetoHolderId)) state.vetoPlayers[state.vetoPlayers.IndexOf(former)] = state.vetoHolderId;
            });
            var before = director.Snapshot;
            var block = before.nominees.ToList();
            string holder = before.vetoHolderId, hoh = before.hohId;
            Assert.That(block, Does.Contain(holder), "The holder is on the block.");
            Assert.That(holder, Is.Not.EqualTo(before.playerId), "The holder is a houseguest the house decides for.");
            Assert.That(EpisodeEngine.NpcVetoSave(before), Is.EqualTo(holder), "A holder on the block saves themselves.");
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            var beats = new List<CeremonyBeat>();
            void Record(CeremonyBeat beat) => beats.Add(beat);
            takeover.BeatReached += Record;
            try
            {
                yield return PlayUntilTheVetoMeeting();
                var committed = director.Snapshot;
                Assert.That(director.IsCeremonyStaged, Is.True, "The veto meeting is staged in the house.");
                Assert.That(director.CeremonyStageKind, Is.EqualTo(CeremonySting.VetoKind));
                Assert.That(committed.nominees, Does.Not.Contain(holder), "The holder came off the block.");
                string replacement = committed.nominees.Except(block).Single();

                string report = StageReport();
                foreach (var id in block)
                    Assert.That(report, Does.Contain("  " + id + " -> " + CeremonySeating.HotSeat + " "), id + " has a red chair, the holder too:\n" + report);
                Assert.That(report, Does.Contain("  " + hoh + " -> " + CeremonySeating.GallerySeat + " 0 "),
                    "The Head of Household takes the U's middle base seat, facing the red chairs:\n" + report);
                Assert.That(report, Does.Not.Contain("  " + replacement + " -> " + CeremonySeating.HotSeat + " "),
                    "The replacement is not seated by the outcome:\n" + report);

                director.SkipCeremonySummons();
                yield return WaitFor(() => takeover.PlayingMeeting, 3f, "the meeting plays once the summons is skipped");
                Assert.That(takeover.Page, Is.EqualTo(CeremonyTakeover.MeetingPage.Intro), "It opens on the holder and the block,");
                Assert.That(MeetingFacesUp(takeover).Count, Is.EqualTo(2), "two faces: the holder is one of the two on the block.");
                float decisionBy = CeremonyPacing.FadeIn + CeremonyPacing.VetoIntro(takeover.MeetingPace) + CeremonyPacing.VetoQuestion(takeover.MeetingPace);
                yield return WaitFor(() => beats.Any(beat => beat.Kind == CeremonyBeatKind.VetoDecided), decisionBy + 3f, "the decision is on the screen");
                Assert.That(beats.Single(beat => beat.Kind == CeremonyBeatKind.VetoDecided).SubjectId, Is.EqualTo(holder),
                    "The decision is about the holder, who saved themselves.");

                takeover.Cancel();
                yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the stage ends with the card");
                Assert.That(director.Snapshot.revision, Is.EqualTo(committed.revision), "The meeting commits nothing.");
                Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "The house's world outlives the meeting.");
            }
            finally { takeover.BeatReached -= Record; }
        }

        /// <summary>
        /// The meeting's beats in their order at both outcomes, played to the end: the card up, the
        /// decision with whoever the veto saved (or nobody), the replacement only when there is
        /// one, the card down. The player holds the veto and decides. A frame of the decision on
        /// the screen for the look sheet, in a batch run.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_TheVetoMeetingActsOutTheDecisionAtBothOutcomes([Values(true, false)] bool used)
        {
            yield return InstallStagedSeason(72, state => AtVetoMeeting(state, playerHolds: true));
            var before = director.Snapshot;
            var block = before.nominees.ToList();
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            var beats = new List<CeremonyBeat>();
            void Record(CeremonyBeat beat) => beats.Add(beat);
            takeover.BeatReached += Record;
            try
            {
                var command = NextCommand(before);
                Assert.That(command.kind, Is.EqualTo(EpisodeCommandKind.ResolveVeto), "The player holds the veto and decides.");
                Assert.That(command.secondTargetId, Is.Not.Null, "A replacement is there to be named.");
                command.useVeto = used;
                command.targetId = used ? block[0] : null;
                var result = director.Submit(command);
                Assert.That(result.accepted, Is.True, result.reason);
                var committed = director.Snapshot;
                string saved = used ? block[0] : null;
                string replacement = used ? committed.nominees.Except(block).Single() : null;
                Assert.That(director.IsCeremonyStaged, Is.True, "The veto meeting is staged in the house.");

                director.SkipCeremonySummons();
                yield return WaitFor(() => takeover.PlayingMeeting, 3f, "the meeting plays once the summons is skipped");
                Assert.That(takeover.Surface.Room, Is.EqualTo("Living"));
                float decisionBy = CeremonyPacing.FadeIn + CeremonyPacing.VetoIntro(takeover.MeetingPace) + CeremonyPacing.VetoQuestion(takeover.MeetingPace);
                yield return WaitFor(() => takeover.Page == CeremonyTakeover.MeetingPage.Decision, decisionBy + 3f, "the decision is on the screen");
                if (Application.isBatchMode) yield return CaptureTheScreen(takeover.Surface, used ? "ceremony-stage-veto-used" : "ceremony-stage-veto-not-used");
                yield return WaitFor(() => !takeover.IsPlaying, takeover.MeetingDuration + 3f, "the meeting plays to its end");

                var expected = used
                    ? new[] { CeremonyBeatKind.Opened, CeremonyBeatKind.VetoDecided, CeremonyBeatKind.ReplacementNamed, CeremonyBeatKind.Closed }
                    : new[] { CeremonyBeatKind.Opened, CeremonyBeatKind.VetoDecided, CeremonyBeatKind.Closed };
                Assert.That(beats.Select(beat => beat.Kind), Is.EqualTo(expected), "The meeting's beats, in order: " + string.Join(", ", beats));
                Assert.That(beats.Single(beat => beat.Kind == CeremonyBeatKind.VetoDecided).SubjectId, Is.EqualTo(saved),
                    used ? "The decision is about whoever the veto saved." : "Nobody was saved.");
                if (used)
                    Assert.That(beats.Single(beat => beat.Kind == CeremonyBeatKind.ReplacementNamed).SubjectId, Is.EqualTo(replacement),
                        "The replacement is whoever the Head of Household named.");
                Assert.That(beats.Any(beat => beat.Skipped), Is.False, "Played to its end, nothing skipped.");

                yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the stage ends with the card");
                Assert.That(director.Snapshot.revision, Is.EqualTo(committed.revision), "The meeting commits nothing.");
                Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "The house's world outlives the meeting.");
            }
            finally { takeover.BeatReached -= Record; }
        }

        /// <summary>
        /// One press is one step (PACK8-PASS-PLAN A1) on the meeting too: a press during the summons
        /// starts the card at the block that goes to the vote, the decision and the replacement
        /// reported as skipped; the next press closes it, and the house is let go. The outcome is
        /// the committed one, and nothing is written.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_ASkippedVetoMeetingKeepsTheOutcomeAndLetsTheHouseGo()
        {
            yield return InstallStagedSeason(73, state => AtVetoMeeting(state, playerHolds: true));
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            var beats = new List<CeremonyBeat>();
            void Record(CeremonyBeat beat) => beats.Add(beat);
            takeover.BeatReached += Record;
            try
            {
                var command = NextCommand(director.Snapshot);
                Assert.That(command.kind, Is.EqualTo(EpisodeCommandKind.ResolveVeto), "The player holds the veto and decides.");
                Assert.That(command.useVeto, Is.True, "and uses it.");
                var result = director.Submit(command);
                Assert.That(result.accepted, Is.True, result.reason);
                var committed = director.Snapshot;
                Assert.That(director.IsCeremonyStaged, Is.True, "The veto meeting is staged in the house.");
                Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Summons), "The house is summoned.");
                yield return null;
                Assert.That(director.CeremonySkipShowing, Is.True, "The chip says a press moves the meeting on.");

                // Past the guard that keeps the press that committed the decision from skipping the summons.
                yield return RealSeconds(0.5f);
                Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Summons), "The house is still gathering.");
                yield return PressKey(Key.Enter);
                Assert.That(takeover.PlayingMeeting, Is.True, "One press starts the meeting on the screen,");
                Assert.That(takeover.Page, Is.EqualTo(CeremonyTakeover.MeetingPage.Final), "at the block that goes to the vote.");
                Assert.That(beats.Select(beat => beat.Kind), Is.EqualTo(new[]
                    { CeremonyBeatKind.Opened, CeremonyBeatKind.VetoDecided, CeremonyBeatKind.ReplacementNamed }),
                    "The decision and the replacement are reported on the way: " + string.Join(", ", beats));
                Assert.That(beats.Skip(1).All(beat => beat.Skipped), Is.True, "as skipped.");
                yield return Frames(2);
                Assert.That(takeover.IsPlaying, Is.True, "The same press does not close it as well.");

                // Once the card has been up long enough to be read, the next press closes it.
                yield return RealSeconds(1f);
                yield return PressKey(Key.Enter);
                Assert.That(takeover.IsPlaying, Is.False, "The next press closes it,");
                Assert.That(beats.Last().Kind, Is.EqualTo(CeremonyBeatKind.Closed));
                yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the stage lets the house go,");
                yield return Frames(2);
                Assert.That(director.CeremonySkipShowing, Is.False, "the chip goes with it,");
                Assert.That(Hud.IsHeldForReveal, Is.False, "and the chrome is back.");
                Assert.That(director.Snapshot.revision, Is.EqualTo(committed.revision), "Skipping commits nothing:");
                Assert.That(director.Snapshot.nominees, Is.EqualTo(committed.nominees), "the block is the committed one.");
                Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
            }
            finally { takeover.BeatReached -= Record; }
        }

        /// <summary>
        /// The meeting is staged after the first staged eviction too (PACK8-PASS-PLAN §1.1 and C1):
        /// week one's eviction is played through its walk out, week two's nomination is staged, and
        /// week two's veto meeting plays on the living room's screen. When it is not staged, the
        /// line the stage logs names the guard that declined it.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_TheVetoMeetingIsStagedInASecondWeek()
        {
            uint seed = SeasonThePlayerSurvives(52, AtEviction, 2);
            yield return InstallStagedSeason(seed, AtEviction);
            yield return PlayTheStagedEvictionToItsWalkOut();
            yield return WaitFor(() => director.WalkingOutId == null, EpisodeDirector.WalkOutSeconds + 2f, "week 1's walk-out ends");
            yield return AssertTheHouseRunsOn("Week 1's eviction");
            yield return CloseTheWeeklyRecap();
            yield return PlayOnToTheNextStagedCeremony("Week 2's nomination");
            Assert.That(stagedKind, Is.EqualTo(CeremonySting.NominationKind), "Week 2 stages its nomination first.");
            Assert.That(director.Snapshot.week, Is.EqualTo(2));

            string declined = null;
            void Capture(string message, string stack, LogType type)
            {
                if (message.StartsWith("Ceremony stage declined (" + CeremonySting.VetoKind + ")")) declined = message;
            }
            bool committed = false;
            Application.logMessageReceived += Capture;
            try
            {
                for (int step = 0; step < 30 && !committed; step++)
                {
                    yield return ContinueCompetitionResults(byKeyboard: false);
                    var before = director.Snapshot;
                    Assert.That(before.phase, Is.Not.EqualTo(EpisodePhase.Finished), "The season ended before week 2's veto meeting.");
                    var result = director.Submit(NextCommand(before));
                    Assert.That(result.accepted, Is.True, before.phase + ": " + result.reason);
                    committed = director.Snapshot.events.Skip(before.events.Count).Any(entry => entry.kind == CeremonySting.VetoKind
                        && (entry.audienceIds.Count == 0 || entry.audienceIds.Contains(before.playerId)));
                    if (!committed) yield return null;
                }
            }
            finally { Application.logMessageReceived -= Capture; }
            Assert.That(committed, Is.True, "Week 2's veto meeting was committed.");
            Assert.That(director.IsCeremonyStaged, Is.True, "Week 2's veto meeting is staged in the house, not played on the HUD"
                + (declined != null ? ": " + declined : "") + ". The house's world: " + (director.NpcAutonomyDiagnostic ?? "running") + ".");
            Assert.That(director.CeremonyStageKind, Is.EqualTo(CeremonySting.VetoKind));
            Assert.That(director.Snapshot.week, Is.EqualTo(2));
            director.SkipCeremonySummons();
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing, 3f, "the meeting plays");
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            Assert.That(takeover.PlayingMeeting && takeover.Surface != null && takeover.Surface.Room == "Living", Is.True,
                "on the living room's screen.");
            takeover.Cancel();
            yield return AssertTheHouseRunsOn("Week 2's veto meeting");
        }

        /// <summary>
        /// A batch run not asking for stages keeps the meeting's card exactly as it was: the
        /// takeover on the HUD frame, the strip carrying the committed text, no skip chip - and the
        /// camera framing the living room, where the meeting is held now.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_ABatchRunPlaysTheVetoMeetingOnTheHud()
        {
            HoldTheHouseForTheFixture();
            yield return InstallStrategySeason(74, state => AtVetoMeeting(state, playerHolds: false));
            director.BuildNpcWorldForDiagnostics();
            Assert.That(director.StagesInBatchRuns, Is.False, "The default: stages are for play.");
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            var sting = SceneComponents<CeremonySting>().Single();
            var before = director.Snapshot;
            var result = director.Submit(NextCommand(before));
            Assert.That(result.accepted, Is.True, result.reason);
            var meeting = director.Snapshot.events.Skip(before.events.Count).Last(entry => entry.kind == CeremonySting.VetoKind);
            if (Application.isBatchMode)
            {
                Assert.That(director.IsCeremonyStaged, Is.False, "A batch run stages nothing unless asked.");
                Assert.That(takeover.IsPlaying && takeover.PlayingKind == CeremonySting.VetoKind, Is.True, "The meeting's card plays at once,");
                Assert.That(takeover.PlayingMeeting, Is.False, "the generic one,");
                Assert.That(takeover.Surface, Is.Null, "on the HUD frame.");
                Assert.That(takeover.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(sting.IsPlaying && sting.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text == meeting.text), Is.True,
                    "The strip carries the committed text.");
                Assert.That(director.IsFramingCeremony, Is.True, "The camera goes to the meeting's room,");
                var living = SceneComponents<HouseRoomMarker>().First(room => room.RoomName == "Living");
                var focus = cameraRig.DesiredFocus;
                Assert.That(new Vector2(focus.x - living.transform.position.x, focus.z - living.transform.position.z).magnitude, Is.LessThan(0.5f),
                    "the living room.");
                yield return null;
                Assert.That(director.CeremonySkipShowing, Is.False, "No skip chip over the card on the HUD frame.");
            }
            takeover.Cancel();
            yield return Frames(2);
        }

        /// <summary>Under reduced motion the meeting is never staged: the generic card plays at once and the camera cuts to the living room.</summary>
        [UnityTest]
        public IEnumerator CeremonyStage_UnderReducedMotionTheVetoMeetingIsTheCardAndCutsToTheLivingRoom()
        {
            HoldTheHouseForTheFixture();
            yield return InstallStrategySeason(75, state => AtVetoMeeting(state, playerHolds: false));
            director.BuildNpcWorldForDiagnostics();
            AskForTheStages(reduced: true);
            yield return null;
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            var result = director.Submit(NextCommand(director.Snapshot));
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(director.IsCeremonyStaged, Is.False, "Reduced motion stages nothing,");
            Assert.That(takeover.IsPlaying && takeover.PlayingKind == CeremonySting.VetoKind && takeover.Surface == null, Is.True,
                "and the meeting's card plays on the HUD frame.");
            yield return Frames(2);
            Assert.That(director.IsFramingCeremony, Is.True, "The meeting is framed,");
            var living = SceneComponents<HouseRoomMarker>().First(room => room.RoomName == "Living");
            var focus = cameraRig.DesiredFocus;
            Assert.That(new Vector2(focus.x - living.transform.position.x, focus.z - living.transform.position.z).magnitude, Is.LessThan(0.5f),
                "in the living room,");
            Assert.That(cameraRig.HasArrived(), Is.True, "cut to: a 1.2 second move would still be under way two frames in.");
            Assert.That(director.CeremonySkipShowing, Is.False, "No skip chip over a card that is not staged.");
            takeover.Cancel();
            yield return Frames(2);
        }

        /// <summary>
        /// Stages the installed veto meeting through the director's own entry, with a card of the
        /// test's, before anything is committed: the stage a commit would begin, with nothing else
        /// holding the camera or a card. False when the house declined it.
        /// </summary>
        private bool BeginTheVetoStage(System.Func<ScreenSurface, bool> playCard)
        {
            var state = director.Snapshot;
            var begin = typeof(EpisodeDirector).GetMethod("TryBeginCeremonyStage", BindingFlags.Instance | BindingFlags.NonPublic);
            return (bool)begin.Invoke(director, new object[] { CeremonySting.VetoKind, state, playCard, state.nominees.ToList() });
        }

        /// <summary>The veto meeting's generic card as the director plays it for a block that stands: the card on the HUD frame, the strip, and the framing and reactions when nothing is staged.</summary>
        private bool PlayTheGenericVetoCard(EpisodeState state)
        {
            var play = typeof(EpisodeDirector).GetMethod("PlayGenericCeremonyCard", BindingFlags.Instance | BindingFlags.NonPublic);
            var active = new HashSet<string>(state.Active.Select(actor => actor.id));
            var block = new HashSet<string>(state.nominees);
            return (bool)play.Invoke(director, new object[] { state, CeremonySting.VetoKind, EpisodeDirector.VetoNotUsedLine, active, block });
        }

        /// <summary>
        /// The generic card is the meeting's fallback, and it waits for the house to be let go before
        /// it frames the room (PACK8-PASS-PLAN C1). Played under a stage it only reports: the stage
        /// has the camera and the bodies, so the card neither frames the meeting's room nor sets the
        /// house reacting. A meeting card that declines the living room's screen ends the stage
        /// before anything plays on it, and the generic card plays on the HUD instead, exactly once,
        /// with the framing its own by then. Nothing is committed.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_TheGenericVetoCardFramesTheRoomOnlyOnceTheStageHasLetTheHouseGo()
        {
            yield return InstallStagedSeason(77, state => AtVetoMeeting(state, playerHolds: false));
            yield return WaitFor(() => director.NpcAutonomyReady, 5f, "the house's world has bound its people");
            var installed = director.Snapshot;
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            int offeredTheScreen = 0, playedOnTheHud = 0;
            bool stagedWhenItPlayed = true;
            Assert.That(BeginTheVetoStage(screen =>
            {
                // The meeting's card declining the set's screen, as a meeting it cannot tell does.
                if (screen != null) { offeredTheScreen++; return false; }
                playedOnTheHud++;
                stagedWhenItPlayed = director.IsCeremonyStaged;
                return PlayTheGenericVetoCard(installed);
            }), Is.True, "The veto meeting is staged.");
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Summons), "The house is summoned.");
            Assert.That(director.IsFramingCeremony, Is.False, "Nothing has framed the meeting's room.");

            // Under the stage, the generic card only reports.
            var focus = cameraRig.DesiredFocus;
            float distance = cameraRig.DesiredDistance;
            Assert.That(PlayTheGenericVetoCard(installed), Is.True, "The generic card plays under a stage: the beat is never silent,");
            Assert.That(takeover.IsPlaying && takeover.PlayingKind == CeremonySting.VetoKind && takeover.Surface == null, Is.True, "on the HUD frame,");
            Assert.That(director.IsFramingCeremony, Is.False, "but it does not frame the meeting's room: the stage has the camera,");
            Assert.That(Vector3.Distance(cameraRig.DesiredFocus, focus), Is.LessThan(0.01f), "which stays where the stage put it.");
            Assert.That(cameraRig.DesiredDistance, Is.EqualTo(distance).Within(0.01f));
            Assert.That(director.IsCeremonyStaged, Is.True, "The stage still holds the house.");
            takeover.Cancel();

            // A meeting card that declines the screen: the stage ends, and the generic card plays on the HUD once.
            var beats = new List<CeremonyBeat>();
            void Record(CeremonyBeat beat) => beats.Add(beat);
            takeover.BeatReached += Record;
            try
            {
                director.SkipCeremonySummons();
                yield return WaitFor(() => offeredTheScreen > 0, 3f, "the card is offered the living room's screen once the summons is skipped");
                Assert.That(offeredTheScreen, Is.EqualTo(1), "It declines the screen,");
                Assert.That(director.IsCeremonyStaged, Is.False, "the stage ends,");
                Assert.That(playedOnTheHud, Is.EqualTo(1), "and the card plays on the HUD instead, the beat never silent,");
                Assert.That(stagedWhenItPlayed, Is.False, "once the house has been let go.");
                Assert.That(takeover.IsPlaying && takeover.PlayingKind == CeremonySting.VetoKind, Is.True, "The meeting's generic card is up,");
                Assert.That(takeover.PlayingMeeting, Is.False);
                Assert.That(takeover.Surface, Is.Null, "on the HUD frame,");
                Assert.That(director.IsFramingCeremony, Is.True, "and it frames the meeting's room, nothing else holding the camera now,");
                var living = SceneComponents<HouseRoomMarker>().First(room => room.RoomName == "Living");
                var framed = cameraRig.DesiredFocus;
                Assert.That(new Vector2(framed.x - living.transform.position.x, framed.z - living.transform.position.z).magnitude, Is.LessThan(0.5f),
                    "the living room.");

                yield return Frames(3);
                Assert.That(offeredTheScreen + playedOnTheHud, Is.EqualTo(2), "Nothing plays the card again.");
                Assert.That(beats.Count(beat => beat.Kind == CeremonyBeatKind.Opened), Is.EqualTo(1), "One card opened: " + string.Join(", ", beats));
                Assert.That(director.IsCeremonyStaged, Is.False);
                Assert.That(player.HasActivityOwner, Is.False, "The player has their body back.");
                Assert.That(director.Snapshot.revision, Is.EqualTo(installed.revision), "Nothing was written.");
                Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "The house's world outlives the stage.");
            }
            finally { takeover.BeatReached -= Record; }
            takeover.Cancel();
            yield return Frames(2);
        }

        /// <summary>
        /// The meeting's card on the living room's screen, played straight onto it with nobody
        /// summoned: a holder on the block is one face wearing both badges, every label is drawn in
        /// a box it fits, a skip turns to the block that goes to the vote, and the next card on the
        /// HUD frame plays there, off the screen. A meeting with no screen to play on is declined.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyTakeover_TheMeetingPlaysOnTheLivingScreenAndTheNextCardOnTheHud()
        {
            yield return null;
            var scene = director.gameObject.scene;
            var state = director.Snapshot;
            CeremonySeating.Ensure(scene, state.Active.Count());
            Assert.That(ScreenSurface.TryFind(scene, "Living", out var screen), Is.True, "The living room has its ceremony screen.");
            var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            var before = new List<string> { npcs[1], npcs[2] };
            state.hohId = npcs[0];
            state.vetoHolderId = npcs[1];
            state.nominees = new List<string> { npcs[2], npcs[4] };
            var script = EpisodeDirector.VetoMeetingScriptFor(state, before);
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            Assert.That(takeover.PlayVetoMeeting(script, false, CeremonyPace.Quick, null), Is.False, "No screen, no meeting: the caller's card plays on the HUD.");
            Assert.That(takeover.IsPlaying, Is.False);

            Assert.That(takeover.PlayVetoMeeting(script, false, CeremonyPace.Quick, screen), Is.True, "The meeting plays.");
            Assert.That(takeover.Surface, Is.SameAs(screen), "on the living room's screen,");
            var canvas = takeover.GetComponent<Canvas>();
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace), "a world-space canvas on its face.");
            yield return null;
            Canvas.ForceUpdateCanvases();
            var root = takeover.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Veto meeting");
            Assert.That(MeetingFacesUp(takeover).Count, Is.EqualTo(2), "A holder on the block is one face,");
            var badges = root.GetComponentsInChildren<TMP_Text>().Where(label => label.name == "Badge text").Select(label => label.text).ToList();
            Assert.That(badges.Count(text => text == CeremonyTakeover.VetoBadge), Is.EqualTo(1), "wearing the veto's badge");
            Assert.That(badges.Count(text => text == CeremonyTakeover.OnTheBlockBadge), Is.EqualTo(2), "and the block's, as the other nominee does.");
            var headline = root.GetComponentsInChildren<TMP_Text>().Single(label => label.name == "Meeting headline");
            Assert.That(headline.text, Is.EqualTo(script.Meeting.introLine), "The first page says who holds the veto.");
            foreach (var label in root.GetComponentsInChildren<TMP_Text>().Where(each => !string.IsNullOrWhiteSpace(each.text)))
            {
                float font = label.enableAutoSizing ? label.fontSizeMax : label.fontSize;
                Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(font * 1.3f - 0.01f),
                    "'" + label.text + "' (" + label.name + ") has a box " + label.rectTransform.rect.height.ToString("0.0") + " high for a " + font.ToString("0.#") + " font.");
            }
            AssertEveryLabelDraws(root, "The meeting's first page");

            takeover.SkipToResult();
            Assert.That(takeover.Page, Is.EqualTo(CeremonyTakeover.MeetingPage.Final), "A skip turns to the block that goes to the vote.");
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(headline.text, Is.EqualTo(VetoMeetingRead.FinalHeadline));
            Assert.That(MeetingFacesUp(takeover).Count, Is.EqualTo(state.nominees.Count), "Its faces are the final block.");
            AssertEveryLabelDraws(root, "The meeting's last page");

            // The next card on the HUD frame plays there, off the screen.
            takeover.Play(CeremonySting.VetoKind, state.week, new List<CeremonyTakeover.Subject>(), false);
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay), "The next card is on the HUD frame,");
            Assert.That(takeover.Surface, Is.Null);
            Assert.That(takeover.PlayingMeeting, Is.False);
            Assert.That(takeover.MeetingScreen, Is.Null, "and the meeting is forgotten.");
            Assert.That(root.gameObject.activeSelf, Is.False, "Its page is down.");
            takeover.Cancel();
            yield return Frames(2);
        }
    }
}
