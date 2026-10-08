using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The eviction's exit, staged (PACK8-PASS-PLAN C2, MOCKUP-PASS-PLAN M19): the result taken in
    /// the chairs, the evicted standing to say goodbye to the house, the house watching them walk
    /// out slowly, the door opened without its flare, shut behind them and held on before their
    /// body goes, and the chrome aside the whole way. A press goes straight to the shut door, and
    /// nothing the commit decided changes.
    ///
    /// <para>Every wait is bounded in real seconds, as the stage's own tests are; a span the code
    /// measures - the hold on the shut door - is measured on its own unscaled clock.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static HouseNpc HouseguestBody(string id) => SceneComponents<HouseNpc>().Single(npc => npc.Id == id);

        private static CharacterPresentation Presentation(Component body) => body != null ? body.GetComponent<CharacterPresentation>() : null;

        /// <summary>
        /// The result is taken in the chairs, and the goodbye comes between the card and the walk:
        /// the evicted stand and face the house, every seated head turns to them, whoever sits
        /// claps, the camera is on their face, their line is on the strip, the chrome is aside and
        /// the warm key light on the red chairs - and only once it is over do they walk, slowly.
        /// </summary>
        [UnityTest]
        public IEnumerator StagedExit_TheGoodbyeComesBetweenTheCardAndTheWalk()
        {
            yield return InstallStagedSeason(52, AtEviction);
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The eviction is staged in the living room.");
            Assert.That(director.CeremonyStageKind, Is.EqualTo(CeremonySting.EvictionKind));
            var committed = director.Snapshot;
            string evicted = director.DepartingId;
            Assert.That(evicted, Is.Not.Null, "A houseguest is evicted.");
            string survivor = committed.nominees.Single(id => id != evicted);
            // Not skipped: the card waits for the nominees in red, and the result is taken there.
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing,
                EpisodeDirector.SummonsHardSeconds(CeremonyPace.Suspenseful) + 6f, "the vote plays once the nominees have taken the hot seats");
            Assert.That(director.EvictionKeyLightShowing, Is.True, "A warm key light is on the red chairs from the card's start.");
            bool Seated(string id)
            {
                var seat = HouseguestBody(id).GetComponent<HouseSeatPresentation>();
                return seat != null && seat.Active;
            }
            yield return WaitFor(() => Seated(evicted) && Seated(survivor), 8f, "both nominees sit in red");

            // The result read, and its cue played: the room takes it where it sits.
            var vote = SceneComponents<VoteReveal>().Single();
            var leaving = HouseguestBody(evicted);
            var staying = HouseguestBody(survivor);
            float readAt = Time.unscaledTime;
            float cueDelay = Mathf.Min(1.5f, vote.ResultHoldSeconds * 0.4f) / Mathf.Max(1f, vote.SpeedMultiplier);
            float observeBy = Time.realtimeSinceStartup + vote.ResultHoldSeconds;
            bool wasHeld = vote.Held;
            // A wall-clock sleep could finish before the director's next unscaled cue tick, or
            // after the result had already closed on one slow frame. Hold the card's reading
            // frame while observing the actual reaction; the stage's cues keep their own clock.
            // Restore its clock before testing the card-down -> goodbye -> walk handover below.
            vote.Held = true;
            try
            {
                vote.SkipToResult();
                bool TookTheResult()
                {
                    var look = Presentation(leaving).LookTarget;
                    return look != null && look.name.StartsWith("Ceremony look mark");
                }
                while (!TookTheResult() && vote.IsPlaying
                    && director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing
                    && Time.realtimeSinceStartup < observeBy) yield return null;
            }
            finally { vote.Held = wasHeld; }
            Assert.That(vote.IsPlaying && vote.ShowingResult, Is.True, "The result is still up.");
            var leavingLook = Presentation(leaving).LookTarget;
            Assert.That(leavingLook != null && leavingLook.name.StartsWith("Ceremony look mark"), Is.True,
                "The evicted take the result head down, in the chair: looking at " + (leavingLook != null ? leavingLook.name : "nothing")
                + "; stage clock advanced " + (Time.unscaledTime - readAt).ToString("F2") + " s, cue due after " + cueDelay.ToString("F2")
                + " s. " + director.CeremonyStageReport("seated result reaction"));
            Assert.That(Time.unscaledTime - readAt, Is.GreaterThanOrEqualTo(cueDelay - 0.01f),
                "The seated reaction follows the result's reading beat, rather than firing before its cue.");
            var survivorSeat = staying.GetComponent<HouseSeatPresentation>();
            Assert.That(survivorSeat != null && survivorSeat.Active, Is.True, "The survivor stays seated,");
            Assert.That(Presentation(staying).LastReaction, Is.Not.EqualTo(CharacterPresentation.Reaction.Won),
                "with no fist pump in front of the one going.");
            // Nobody looks down for a ballot they cast: the result never says who voted to keep the
            // evicted (UI-UX-PASS-PLAN B0). Every other houseguest turns to the one going, whatever
            // their ballot was.
            foreach (var npc in SceneComponents<HouseNpc>().Where(each => each.gameObject.activeInHierarchy && each.Id != evicted && each.Id != survivor))
            {
                var look = Presentation(npc).LookTarget;
                Assert.That(look, Is.SameAs(leaving.transform),
                    npc.Id + " turns to the one going, whatever they voted: looking at " + (look != null ? look.name : "nothing") + ".");
            }

            // The card down: the goodbye.
            float closedAt = Time.unscaledTime;
            vote.Cancel();
            yield return null;
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Goodbye), "The evicted stand to say goodbye,");
            Assert.That(director.WalkingOutId, Is.Null, "and nobody walks yet.");
            Assert.That(Hud.IsHeldForReveal, Is.True, "The chrome stays aside,");
            Assert.That(PlatesUp(), Is.Empty, "the plates stay down,");
            Assert.That(director.CeremonySkipShowing, Is.True, "the chip names the press that moves it on,");
            Assert.That(director.EvictionKeyLightShowing, Is.True, "and the key light is still on.");
            Assert.That(Presentation(leaving).IsSeated, Is.False, "They are up out of the chair.");
            foreach (var npc in SceneComponents<HouseNpc>().Where(each => each.gameObject.activeInHierarchy && each.Id != evicted))
            {
                var visual = Presentation(npc);
                if (!visual.IsSeated) continue;
                Assert.That(visual.LookTarget, Is.SameAs(leaving.transform), npc.Id + " looks at them from the seat,");
                if (visual.SupportsSeated(CharacterPresentation.Reaction.Cheered))
                    Assert.That(visual.LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Cheered), npc.Id + " and claps them out.");
            }
            foreach (var npc in SceneComponents<HouseNpc>().Where(each => each.gameObject.activeInHierarchy && each.Id != evicted))
                if (!Presentation(npc).IsSeated)
                    Assert.That(Presentation(npc).LastReaction, Is.Not.EqualTo(CharacterPresentation.Reaction.Cheered), npc.Id + " stands, and only looks.");
            Assert.That(Presentation(player).LastReaction, Is.Not.EqualTo(CharacterPresentation.Reaction.Cheered), "The player's body is the player's.");
            Assert.That(survivorSeat.Active, Is.True, "The survivor is still in red.");
            var face = leaving.transform.position + Vector3.up * 1.55f;
            Assert.That(cameraRig.HasShot, Is.True, "The camera is the goodbye's,");
            Assert.That(Vector3.Distance(cameraRig.DesiredFocus, face), Is.LessThan(0.3f), "on the evicted's face.");
            var strip = SceneComponents<CeremonySting>().Single().GetComponentsInChildren<TMP_Text>(true).Select(text => text.text).ToList();
            string standing = EpisodeDirector.GoodbyeLine(director.Snapshot, evicted, EpisodeDirector.GoodbyeMoment.Standing);
            Assert.That(strip, Has.Some.EqualTo(standing), "The goodbye line plays here, not at the door,");
            Assert.That(standing, Does.Not.Contain("door"), "worded for where they are - standing before the house - not for the doorway (UI-UX-PASS-PLAN W0).");
            Assert.That(strip, Has.None.EqualTo(EpisodeDirector.GoodbyeLine(director.Snapshot, evicted)), "The doorway's words wait for a body at the door.");

            // Then the walk, and not before the goodbye is over.
            yield return WaitFor(() => director.WalkingOutId != null, EpisodeDirector.GoodbyeSeconds + 1f, "the goodbye gives way to the walk out");
            float goodbye = Time.unscaledTime - closedAt;
            Assert.That(goodbye, Is.InRange(EpisodeDirector.GoodbyeSeconds - 0.1f, EpisodeDirector.GoodbyeSeconds + 0.5f),
                "The goodbye holds about four seconds.");
            Assert.That(director.WalkingOutId, Is.EqualTo(evicted));
            Assert.That(director.WalkOutIsStaged, Is.True, "The walk out is the stage's,");
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Release), "the house keeps its seats to watch,");
            Assert.That(Hud.IsHeldForReveal, Is.True, "the chrome is still aside,");
            Assert.That(director.EvictionKeyLightShowing, Is.False, "and the key light went with the goodbye.");
            var motion = leaving.GetComponent<HouseNpcMotion>();
            yield return WaitFor(() => motion.LeaseId != null && motion.LeaseId.StartsWith("departure:"), 3f, "the walk out takes its first route");
            Assert.That(motion.Speed, Is.LessThanOrEqualTo(EpisodeDirector.StagedWalkSpeed + 0.05f), "They walk slowly while the house watches.");

            director.SkipWalkOut();
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the house gets up");
            yield return null;
            Assert.That(Hud.IsHeldForReveal, Is.False, "The chrome is back once the door is shut behind them,");
            Assert.That(director.transform.Find(EpisodeDirector.EvictionKeyLightName), Is.Null, "the key light is gone,");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "and the house's world runs on.");
            Assert.That(motion.Speed, Is.EqualTo(HouseNpcMotion.DefaultSpeed), "The pace is the house's own again.");
        }

        /// <summary>
        /// The door opens on the door shot, shuts behind them while their body is still in the
        /// house, and the shot holds on the shut door for its whole hold; only then does their body
        /// go, behind a dip, and the door with it. The chrome is aside until then. The yard's door,
        /// the fallback: it is built only at the cut to it, where the living room's stands closed
        /// from the goodbye (StagedExit_TheLivingRoomsDoorLetsThemOut).
        /// </summary>
        [UnityTest]
        public IEnumerator StagedExit_TheDoorShutsBehindThemBeforeTheyGo()
        {
            yield return InstallStagedSeason(52, AtEviction);
            director.WalkOutThrough = EpisodeDirector.WalkOutDoor.Yard;
            yield return PlayTheStagedEvictionToItsWalkOut();
            string leaving = stagedLeaving;
            var body = HouseguestBody(leaving);
            Assert.That(director.WalkOutIsStaged, Is.True, "The walk out is the stage's.");
            Assert.That(DoorSetUp(), Is.False, "The door is not standing in the yard while the house watches them go.");

            float widest = 0f, shutAt = 0f;
            bool sawOpen = false, onTheDoor = false, shut = false, inTheHouseWhenShut = false, heldThrough = true;
            float by = Time.realtimeSinceStartup + EpisodeDirector.WalkOutSeconds + 2f;
            while (director.WalkingOutId != null && Time.realtimeSinceStartup < by)
            {
                var root = SceneRoot(OpeningDoorSet.RootName);
                var door = root != null ? root.GetComponent<OpeningDoorSet>() : null;
                if (door != null)
                {
                    if (!sawOpen && door.IsOpen)
                    {
                        sawOpen = true;
                        // The opening's door shot, whose focus the push-in keeps.
                        onTheDoor = Vector3.Distance(cameraRig.DesiredFocus, DoorShotFocusForTests) < 0.2f;
                    }
                    widest = Mathf.Max(widest, door.Openness);
                    if (!shut && widest >= 0.35f && !door.IsOpen && door.Openness <= 0.01f)
                    {
                        shut = true;
                        shutAt = Time.unscaledTime;
                        inTheHouseWhenShut = body.gameObject.activeInHierarchy;
                    }
                }
                heldThrough &= Hud.IsHeldForReveal;
                yield return null;
            }
            Assert.That(director.WalkingOutId, Is.Null, "The walk out ends");
            Assert.That(director.IsTravelDipShowing, Is.True, "behind a dip.");
            float held = Time.unscaledTime - shutAt;
            Assert.That(sawOpen, Is.True, "The door opened for them,");
            Assert.That(onTheDoor, Is.True, "with the camera on the door,");
            Assert.That(widest, Is.GreaterThanOrEqualTo(0.35f), "wide enough to walk through,");
            Assert.That(shut, Is.True, "and shut again behind them");
            Assert.That(inTheHouseWhenShut, Is.True, "while their body was still in the house:");
            Assert.That(held, Is.GreaterThanOrEqualTo(EpisodeDirector.DoorHoldSeconds - 0.05f), "the shot held on the shut door before they went.");
            Assert.That(heldThrough, Is.True, "The chrome was aside for the whole walk.");
            Assert.That(body.gameObject.activeInHierarchy, Is.False, "They are gone,");
            yield return null;
            Assert.That(DoorSetUp(), Is.False, "and the door with them.");
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the house gets up");
            yield return null;
            Assert.That(Hud.IsHeldForReveal, Is.False, "The chrome is back.");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "The house's world runs on.");
        }

        /// <summary>
        /// A press on the walk goes straight to the door shut behind them, as one on the goodbye
        /// does (CeremonyStage_TheSkipStepsThroughAnEvictionAndItsWalkOut): the body goes under the
        /// dip, the door with it, and nothing the commit decided changes.
        /// </summary>
        [UnityTest]
        public IEnumerator StagedExit_APressOnTheWalkGoesStraightToTheShutDoor()
        {
            yield return InstallStagedSeason(52, AtEviction);
            yield return PlayTheStagedEvictionToItsWalkOut();
            string leaving = stagedLeaving;
            var body = HouseguestBody(leaving);
            var committed = director.Snapshot;
            // Past the guard that keeps the press already in flight from ending the walk.
            yield return RealSeconds(0.5f);
            Assert.That(director.WalkingOutId, Is.EqualTo(leaving), "They are walking.");
            yield return PressKey(Key.Enter);
            Assert.That(director.WalkingOutId, Is.Null, "A press ends the walk out:");
            Assert.That(director.DepartingId, Is.Null, "nobody is leaving any more,");
            Assert.That(director.StagedExitRunning, Is.False, "the exit is over,");
            Assert.That(body.gameObject.activeInHierarchy, Is.False, "their body is gone,");
            Assert.That(director.IsTravelDipShowing, Is.True, "behind a dip.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(committed.revision), "Skipping commits nothing:");
            Assert.That(director.Snapshot.Find(leaving).status, Is.EqualTo(committed.Find(leaving).status), "the outcome is the committed one.");
            yield return null;
            Assert.That(DoorSetUp(), Is.False, "The door is struck.");
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the house gets up");
            yield return null;
            Assert.That(Hud.IsHeldForReveal, Is.False, "The chrome is back.");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
        }

        /// <summary>Eviction night with the player on the block beside the second houseguest; the first is Head of Household.</summary>
        private static void AtEvictionWithThePlayerOnTheBlock(EpisodeState state)
        {
            AtEviction(state);
            var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            state.nominees = new List<string> { state.playerId, npcs[1] };
            // The night already past its speeches, with every regular voter's ballot against the
            // player, so the vote the engine counts evicts them. A seed search over the fixture as it
            // was found no season from 52 that did, even with the house's scores turned against the
            // player (measured 2026-10-01): the ballot is not the scores alone. The test is about the
            // exit, not the vote.
            foreach (var speaker in state.nominees)
                state.evictionSpeeches.Add(new EvictionSpeechState
                {
                    speakerId = speaker, week = state.week, isPlayerAuthored = speaker == state.playerId,
                    text = "I'd like to stay.",
                });
            state.evictionStage = EvictionStage.Voting;
            foreach (var voter in EpisodeEngine.Voters(state))
                state.votes.Add(new VoteState { voterId = voter.id, targetId = state.playerId, reason = "The fixture's ballot." });
        }

        /// <summary>
        /// The first season from <paramref name="from"/> on, shaped by <paramref name="shape"/>, in
        /// which the house's next legal decisions evict the player at this eviction. Played on the
        /// engine alone, as <see cref="SeasonThePlayerSurvives"/> is; the director plays the same
        /// decisions, and the house's world is paused on eviction night.
        /// </summary>
        private static uint SeasonThePlayerIsEvicted(uint from, System.Action<EpisodeState> shape)
        {
            for (uint seed = from; seed < from + 60; seed++)
            {
                var engine = new EpisodeEngine(StrategySeason(seed, shape));
                int week = engine.Snapshot.week;
                for (int guard = 0; guard < 16; guard++)
                {
                    var state = engine.Snapshot;
                    if (state.Find(state.playerId).status != ContestantStatus.Active) return seed;
                    if (state.phase != EpisodePhase.Eviction || state.week != week) break;
                    if (!engine.Apply(NextCommand(state)).accepted) break;
                }
            }
            Assert.Fail("No season from seed " + from + " evicts the player on the block.");
            return from;
        }

        /// <summary>
        /// The player as the evicted nominee: both red chairs are held at the card's start, the
        /// player's among them, and their eviction is the season's turn to the jury as it was - no
        /// goodbye, and no walk out.
        /// </summary>
        [UnityTest]
        public IEnumerator StagedExit_TheEvictedPlayerHasNoGoodbyeAndNoWalk()
        {
            uint seed = SeasonThePlayerIsEvicted(52, AtEvictionWithThePlayerOnTheBlock);
            yield return InstallStagedSeason(seed, AtEvictionWithThePlayerOnTheBlock);
            var block = director.Snapshot.nominees.ToList();
            Assert.That(block, Does.Contain(director.Snapshot.playerId), "The player is on the block.");
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The eviction is staged in the living room.");
            Assert.That(director.Snapshot.Find(director.Snapshot.playerId).status, Is.Not.EqualTo(ContestantStatus.Active),
                "The player is evicted, as the engine alone played season " + seed + ".");
            Assert.That(director.DepartingId, Is.Null, "The player is never the house's to walk out.");
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing,
                EpisodeDirector.SummonsHardSeconds(CeremonyPace.Suspenseful) + 6f, "the vote plays");
            var start = StageReport("card start, read by the test");
            foreach (var id in block)
                Assert.That(start, Does.Contain("  " + id + " -> " + CeremonySeating.HotSeat + " "), id + " holds a red chair at the card's start:\n" + start);

            yield return SkipReveals();
            bool saidGoodbye = false, walked = false;
            float by = Time.realtimeSinceStartup + 3f;
            while (director.IsCeremonyStaged && Time.realtimeSinceStartup < by)
            {
                saidGoodbye |= director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Goodbye;
                walked |= director.WalkingOutId != null;
                yield return null;
            }
            Assert.That(director.IsCeremonyStaged, Is.False, "The house gets up once the card is down.");
            Assert.That(saidGoodbye, Is.False, "There is no goodbye,");
            Assert.That(walked, Is.False, "and no walk out.");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
        }

        /// <summary>The look sheet's frames of the exit: the goodbye in the living room, the door open with the evicted at it, and the door shut behind them. Batch runs only.</summary>
        [UnityTest]
        public IEnumerator StagedExit_CapturesTheGoodbyeTheDoorAndTheShutDoor()
        {
            if (!Application.isBatchMode) yield break;
            yield return InstallStagedSeason(52, AtEviction);
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The eviction is staged in the living room.");
            director.SkipCeremonySummons();
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing, 3f, "the vote plays");
            yield return SkipReveals();
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Goodbye), "The goodbye.");
            string evicted = director.DepartingId;
            var sting = SceneComponents<CeremonySting>().Single();
            // Past the push-in (2.05 s), inside the goodbye. The strip says what the picture shows -
            // the evicted standing before the house - and not "from the doorway" (UI-UX-PASS-PLAN
            // W0, sweep-show 10). Read as the wait ends, before the capture's own frames: the strip
            // runs its 2.6 s on the unscaled clock, which a capture's long frame spends.
            yield return RealSeconds(2.1f);
            Assert.That(sting.IsPlaying, Is.True, "The strip is up on the goodbye's frame,");
            var words = sting.GetComponentsInChildren<TMP_Text>(true).Select(text => text.text).ToList();
            Assert.That(words, Has.Some.EqualTo("GOODBYE"), "saying goodbye,");
            Assert.That(words, Has.Some.EqualTo(EpisodeDirector.GoodbyeLine(director.Snapshot, evicted, EpisodeDirector.GoodbyeMoment.Standing)),
                "with the words for where they are: standing, not at the door.");
            yield return CaptureFraming("walk-out-goodbye", settle: false);
            yield return WaitFor(() => director.WalkOutAtTheDoor, EpisodeDirector.GoodbyeSeconds + EpisodeDirector.WalkOutSeconds, "the door opens for them");
            yield return RealSeconds(0.6f);
            yield return CaptureFraming("walk-out-door", settle: false);
            OpeningDoorSet Door() { var root = SceneRoot(OpeningDoorSet.RootName); return root != null ? root.GetComponent<OpeningDoorSet>() : null; }
            yield return WaitFor(() => Door() == null || (!Door().IsOpen && Door().Openness <= 0.01f), EpisodeDirector.WalkOutSeconds, "the door shuts behind them");
            Assert.That(Door(), Is.Not.Null, "The shot holds on the shut door.");
            yield return CaptureFraming("walk-out-shut", settle: false);
            yield return WaitFor(() => !director.StagedExitRunning, EpisodeDirector.WalkOutSeconds, "the exit ends");
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the house gets up");
        }

        // ---------------------------------------------------------------- the living room's door (M23)

        /// <summary>
        /// The prototype's planter, if it still stands in the living room's exit doorway: the scene
        /// edit (HouseLivingGallery.StrikeTheDoorway) and the rebake have not run on this copy.
        /// </summary>
        private static Transform PlanterInTheLivingDoorway() => SceneComponents<Transform>().FirstOrDefault(node =>
            node.gameObject.activeInHierarchy && (node.name == "Planter" || node.name == "Foliage")
            && Mathf.Abs(node.position.x + 12f) < 0.6f && Mathf.Abs(node.position.z + 8f) < 0.6f);

        /// <summary>
        /// The probe of the living room's exit doorway (PACK8-PASS-PLAN C2, M23), run on the D:
        /// copy before the door is switched on. It measures the floor as the walk out would use it
        /// and logs every measure: at each mark, the NavMesh's height, the room, the capsule's
        /// clearance, what a body there would overlap and the nearest edge; how far west the mesh
        /// runs; both red chairs' routes to the vestibule, sampled every 0.1 m for a rise, the
        /// planter's bed or a collider; and what stands in the leaves' swing. It asserts the marks
        /// on the living room's floor, the mesh to x -13.05, both routes complete and the vestibule
        /// flat - once the prototype planter is struck and the house rebaked; until then it says so.
        /// </summary>
        [UnityTest]
        public IEnumerator StagedExit_TheLivingRoomsDoorwayIsClearAndFlat()
        {
            yield return InstallStagedSeason(52, AtEviction);
            var scene = director.gameObject.scene;
            Assert.That(HouseRoomQuery.TryCreate(scene, out var rooms, out var why), Is.True, why);
            var filter = new NavMeshQueryFilter { agentTypeID = player.Agent.agentTypeID, areaMask = player.Agent.areaMask };
            int solid = (1 << 0) | (1 << HouseLayers.Furniture);
            var physics = scene.GetPhysicsScene();
            var overlaps = new Collider[8];
            Physics.SyncTransforms();
            var report = new List<string> { "Living room exit door probe:" };
            var failures = new List<string>();
            string Touching(Vector3 feet)
            {
                int count = physics.OverlapCapsule(feet + Vector3.up * 0.35f, feet + Vector3.up * (1.9f - 0.35f), 0.35f, overlaps, solid, QueryTriggerInteraction.Ignore);
                return count == 0 ? "nothing" : string.Join(", ", overlaps.Take(count).Select(hit => hit.name + " (layer " + hit.gameObject.layer + ")"));
            }

            var marks = new[]
            {
                new Vector3(-13.15f, 0f, -8.5f), new Vector3(-13.15f, 0f, -9.0f), new Vector3(-11.2f, 0f, -9.0f),
                new Vector3(-11.2f, 0f, -8.5f), new Vector3(-12.6f, 0f, -8.5f),
            };
            foreach (var mark in marks)
            {
                string line = "  " + mark.ToString("F2") + ":";
                if (NavMesh.SamplePosition(mark, out var onMesh, 0.25f, filter)) line += " NavMesh y " + onMesh.position.y.ToString("F3");
                else { line += " no NavMesh within 0.25 m"; failures.Add(mark.ToString("F2") + " has no NavMesh within 0.25 m"); }
                if (rooms.TrySampleFloor(mark, 0.35f, filter, 0.25f, out var floor, out var room))
                {
                    bool clear = rooms.HasCapsuleClearance(floor, 0.35f, 1.9f, player.transform);
                    line += ", floor " + floor.ToString("F3") + " in the " + room + " room, clearance " + clear
                        + (clear ? "" : " (" + rooms.LastFailure + ")") + ", overlapping " + Touching(floor);
                    if (NavMesh.FindClosestEdge(floor, out var edge, filter)) line += ", nearest edge " + edge.distance.ToString("F2") + " m away";
                    if (room != "Living") failures.Add(mark.ToString("F2") + " is in the " + room + " room");
                }
                else
                {
                    line += " no floor (" + rooms.LastFailure + ")";
                    failures.Add(mark.ToString("F2") + " has no floor to stand on");
                }
                report.Add(line);
            }

            // How far west the mesh runs along the doorway's middle.
            if (NavMesh.SamplePosition(new Vector3(-11.2f, 0f, -8.5f), out var inside, 0.5f, filter))
            {
                NavMesh.Raycast(inside.position, new Vector3(-14.2f, inside.position.y, -8.5f), out var westEdge, filter);
                report.Add("  the mesh's west edge along z -8.5: x " + westEdge.position.x.ToString("F2"));
                if (westEdge.position.x > -13.05f) failures.Add("the mesh stops at x " + westEdge.position.x.ToString("F2") + ", east of -13.05");
            }
            else
            {
                report.Add("  the mesh's west edge: not measured, (-11.20, -8.50) is off the NavMesh");
                failures.Add("the floor in front of the door is off the NavMesh");
            }

            // Both red chairs' routes to the vestibule.
            var vestibuleMark = new Vector3(-13.15f, 0f, -8.5f);
            bool vestibuleOnMesh = NavMesh.SamplePosition(vestibuleMark, out var vestibule, 0.25f, filter);
            if (vestibuleOnMesh && vestibule.position.y >= 0.06f)
                failures.Add("the vestibule's floor stands " + vestibule.position.y.ToString("F3") + " m up: a body there would float");
            var planterBed = new Bounds(new Vector3(-12f, 0.3f, -8f), new Vector3(1f, 0.6f, 1f));
            foreach (var seat in CeremonySeating.Anchors(scene, CeremonySeating.HotSeat))
            {
                string from = "  from " + seat.VenueId + " " + seat.Slot + "'s approach";
                if (!vestibuleOnMesh || !NavMesh.SamplePosition(seat.Approach, out var start, 0.5f, filter))
                { report.Add(from + ": no route measured"); failures.Add(from.Trim() + " has no route measured"); continue; }
                var path = new NavMeshPath();
                NavMesh.CalculatePath(start.position, vestibule.position, filter, path);
                var corners = path.corners;
                float length = 0f;
                var flags = new List<string>();
                for (int i = 1; i < corners.Length; i++)
                {
                    float leg = Vector3.Distance(corners[i - 1], corners[i]);
                    length += leg;
                    for (float along = 0f; along <= leg; along += 0.1f)
                    {
                        var at = Vector3.Lerp(corners[i - 1], corners[i], leg > 0f ? along / leg : 0f);
                        if (NavMesh.SamplePosition(at, out var under, 0.3f, filter) && under.position.y > 0.06f) flags.Add("rises to y " + under.position.y.ToString("F2") + " at " + at.ToString("F2"));
                        if (planterBed.Contains(new Vector3(at.x, 0.3f, at.z))) flags.Add("crosses the planter's bed at " + at.ToString("F2"));
                        string touching = Touching(at);
                        if (touching != "nothing") flags.Add("touches " + touching + " at " + at.ToString("F2"));
                    }
                }
                report.Add(from + " " + start.position.ToString("F2") + ": " + path.status + ", " + length.ToString("F2") + " m, corners "
                    + string.Join(" ", corners.Select(corner => corner.ToString("F2"))));
                foreach (var flag in flags.Distinct().Take(12)) report.Add("    " + flag);
                if (path.status != NavMeshPathStatus.PathComplete) failures.Add(from.Trim() + " to the vestibule is " + path.status);
            }

            // What stands in the leaves' swing, a quarter-disc in front of each hinge.
            var living = DoorLayout.Living;
            float sweepX = (living.FacadeFrontX + living.SweepFrontX) * 0.5f, sweepDepth = living.SweepFrontX - living.FacadeFrontX;
            foreach (var (fromZ, toZ) in new[] { (living.ApertureMinZ, living.DoorCentre.z), (living.DoorCentre.z, living.ApertureMaxZ) })
            {
                var swing = new Bounds(new Vector3(sweepX, 1.3f, (fromZ + toZ) * 0.5f), new Vector3(sweepDepth, 2.6f, toZ - fromZ));
                var props = SceneComponents<Renderer>().Where(prop => prop.enabled && prop.gameObject.activeInHierarchy
                    && prop.bounds.size.x <= 3f && prop.bounds.size.z <= 3f && swing.Intersects(prop.bounds)).Select(prop => prop.name).Distinct().ToList();
                report.Add("  in the leaves' swing from z " + fromZ.ToString("F2") + " to " + toZ.ToString("F2") + ": "
                    + (props.Count == 0 ? "nothing drawn" : string.Join(", ", props)));
            }
            string measured = string.Join("\n", report);
            Debug.Log(measured);

            var planter = PlanterInTheLivingDoorway();
            if (planter != null)
                Assert.Ignore("The living room's doorway still has the prototype's " + planter.name + " in it: HouseLivingGallery.BuildFromCommandLine strikes it"
                    + " and rebakes (PACK8-PASS-PLAN C2), and the door waits for that.\n" + measured
                    + (failures.Count > 0 ? "\nIt would fail on: " + string.Join("; ", failures) : ""));
            Assert.That(failures, Is.Empty, measured);
        }

        /// <summary>
        /// The living room's door, switched on (MOCKUP-PASS-PLAN M23): it goes up closed at the
        /// living room's origin for the goodbye, with the props in its doorway hidden; the evicted
        /// walk to it, through it into the vestibule, and it shuts behind them while their body is
        /// still in the house; their body goes behind it, and the props come back with the strike.
        /// </summary>
        [UnityTest]
        public IEnumerator StagedExit_TheLivingRoomsDoorLetsThemOut()
        {
            yield return InstallStagedSeason(52, AtEviction);
            director.WalkOutThrough = EpisodeDirector.WalkOutDoor.Living;
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The eviction is staged in the living room.");
            string evicted = director.DepartingId;
            Assert.That(evicted, Is.Not.Null, "A houseguest is evicted.");
            director.SkipCeremonySummons();
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing, 3f, "the vote plays");
            yield return SkipReveals();
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Goodbye), "The goodbye.");
            if (director.WalkOutDoorInUse != EpisodeDirector.WalkOutDoor.Living)
            {
                var planter = PlanterInTheLivingDoorway();
                if (planter != null)
                    Assert.Ignore("The living room's door waits for its doorway to be cleared and rebaked: the prototype's " + planter.name
                        + " stands in it (StagedExit_TheLivingRoomsDoorwayIsClearAndFlat measures it).");
                Assert.Fail("The living room's door was refused although its doorway is clear; the log names why.");
            }
            var root = SceneRoot(OpeningDoorSet.RootName);
            Assert.That(root, Is.Not.Null, "The living room's door is up for the goodbye,");
            Assert.That(Vector3.Distance(root.transform.position, DoorLayout.Living.Origin), Is.LessThan(0.001f), "at the living room's origin,");
            var door = root.GetComponent<OpeningDoorSet>();
            Assert.That(door.Layout.Name, Is.EqualTo(DoorLayout.Living.Name));
            Assert.That(door.IsOpen || door.Openness > 0f, Is.False, "closed.");
            var hidden = director.WalkOutDoorHides.ToList();
            foreach (var prop in hidden) Assert.That(prop.enabled, Is.False, prop.name + " is hidden while the door stands in its place.");

            yield return WaitFor(() => director.WalkingOutId != null, EpisodeDirector.GoodbyeSeconds + 2f, "the goodbye gives way to the walk out");
            var body = HouseguestBody(evicted);
            float deepest = float.MaxValue, widest = 0f;
            bool shut = false, inTheHouseWhenShut = false;
            float by = Time.realtimeSinceStartup + EpisodeDirector.WalkOutSeconds + 2f;
            while (director.WalkingOutId != null && Time.realtimeSinceStartup < by)
            {
                deepest = Mathf.Min(deepest, body.transform.position.x);
                if (door != null)
                {
                    widest = Mathf.Max(widest, door.Openness);
                    if (!shut && widest >= 0.35f && !door.IsOpen && door.Openness <= 0.01f)
                    {
                        shut = true;
                        inTheHouseWhenShut = body.gameObject.activeInHierarchy;
                    }
                }
                yield return null;
            }
            Assert.That(director.WalkingOutId, Is.Null, "The walk out ends");
            Assert.That(widest, Is.GreaterThanOrEqualTo(0.35f), "The door opened for them,");
            Assert.That(deepest, Is.LessThanOrEqualTo(-12.85f), "they walked through it into the vestibule,");
            Assert.That(shut, Is.True, "and it shut behind them");
            Assert.That(inTheHouseWhenShut, Is.True, "before their body went.");
            Assert.That(body.gameObject.activeInHierarchy, Is.False, "They are gone.");
            yield return null;
            Assert.That(DoorSetUp(), Is.False, "The door is struck,");
            foreach (var prop in hidden) Assert.That(prop != null && prop.enabled, Is.True, "and what it hid is back.");
            Assert.That(director.WalkOutDoorHides, Is.Empty);
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the house gets up");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
        }

        /// <summary>
        /// The living room's door asked for with its doorway taken: the probe at the goodbye refuses
        /// it, nothing goes up in the living room, and the walk goes by the yard's front door.
        /// </summary>
        [UnityTest]
        public IEnumerator StagedExit_TheLivingRoomsDoorFallsBackToTheYards()
        {
            yield return InstallStagedSeason(52, AtEviction);
            director.WalkOutThrough = EpisodeDirector.WalkOutDoor.Living;
            // Somebody's worth of furniture standing in the vestibule.
            var blocker = new GameObject("Something in the doorway");
            SceneManager.MoveGameObjectToScene(blocker, director.gameObject.scene);
            blocker.layer = HouseLayers.Furniture;
            blocker.transform.position = new Vector3(-13.15f, 0f, -8.5f);
            var capsule = blocker.AddComponent<CapsuleCollider>();
            capsule.radius = 0.35f; capsule.height = 1.9f; capsule.center = Vector3.up * 0.95f;
            Physics.SyncTransforms();
            // Once the doorway is cleared and rebaked, the refusal is the blocker's. Until then the
            // vestibule's raised floor refuses the door before the blocker is asked, and the probe
            // (StagedExit_TheLivingRoomsDoorwayIsClearAndFlat) is the test that reports it.
            if (PlanterInTheLivingDoorway() == null)
                LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("the living room's door is refused - no room to stand at "));
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The eviction is staged in the living room.");
            director.SkipCeremonySummons();
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing, 3f, "the vote plays");
            yield return SkipReveals();
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Goodbye), "The goodbye.");
            Assert.That(director.WalkOutDoorInUse, Is.EqualTo(EpisodeDirector.WalkOutDoor.Yard), "The living room's door is refused with its doorway taken,");
            Assert.That(DoorSetUp(), Is.False, "and nothing goes up in the living room.");
            Object.Destroy(blocker);

            yield return WaitFor(() => director.WalkingOutId != null, EpisodeDirector.GoodbyeSeconds + 2f, "the goodbye gives way to the walk out");
            Assert.That(director.WalkOutIsStaged, Is.True, "The walk is the stage's,");
            yield return WaitFor(DoorSetUp, EpisodeDirector.WalkOutSeconds, "the yard's door goes up at the dip");
            Assert.That(SceneRoot(OpeningDoorSet.RootName).transform.position, Is.EqualTo(DoorLayout.Yard.Origin), "at the yard's front door.");
            Assert.That(director.WalkOutDoorInUse, Is.EqualTo(EpisodeDirector.WalkOutDoor.Yard));
            director.SkipWalkOut();
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the house gets up");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
        }

        /// <summary>An unstaged walk out goes by the yard's front door whatever the switch says: the living room's is the staged exit's alone.</summary>
        [UnityTest]
        public IEnumerator StagedExit_AnUnstagedWalkOutGoesByTheYardWhateverTheSwitch()
        {
            director.WalkOutsInBatchRuns = true;
            director.WalkOutThrough = EpisodeDirector.WalkOutDoor.Living;
            yield return PlayUntilAHouseguestLeaves();
            string leaving = director.DepartingId;
            SceneComponents<VoteReveal>().Single().Cancel();
            yield return Frames(3);
            Assert.That(director.WalkingOutId, Is.EqualTo(leaving), "The evicted walk out,");
            Assert.That(director.WalkOutIsStaged, Is.False, "unstaged,");
            Assert.That(director.WalkOutDoorInUse, Is.EqualTo(EpisodeDirector.WalkOutDoor.Yard), "by the yard's door,");
            yield return WaitFor(DoorSetUp, 5f, "which goes up for them");
            Assert.That(SceneRoot(OpeningDoorSet.RootName).transform.position, Is.EqualTo(DoorLayout.Yard.Origin), "in the yard.");
            director.SkipWalkOut();
            yield return null;
            Assert.That(DoorSetUp(), Is.False);
        }
    }
}
