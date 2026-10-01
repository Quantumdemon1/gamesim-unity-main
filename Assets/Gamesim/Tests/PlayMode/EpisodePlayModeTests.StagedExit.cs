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
using UnityEngine.InputSystem;
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
            vote.SkipToResult();
            yield return RealSeconds(1.8f);
            Assert.That(vote.IsPlaying && vote.ShowingResult, Is.True, "The result is still up.");
            var leaving = HouseguestBody(evicted);
            var staying = HouseguestBody(survivor);
            var leavingLook = Presentation(leaving).LookTarget;
            Assert.That(leavingLook != null && leavingLook.name.StartsWith("Ceremony look mark"), Is.True,
                "The evicted take the result head down, in the chair: looking at " + (leavingLook != null ? leavingLook.name : "nothing") + ".");
            var survivorSeat = staying.GetComponent<HouseSeatPresentation>();
            Assert.That(survivorSeat != null && survivorSeat.Active, Is.True, "The survivor stays seated,");
            Assert.That(Presentation(staying).LastReaction, Is.Not.EqualTo(CharacterPresentation.Reaction.Won),
                "with no fist pump in front of the one going.");
            var losers = committed.votes.Where(ballot => ballot.targetId == survivor).Select(ballot => ballot.voterId).ToList();
            foreach (var npc in SceneComponents<HouseNpc>().Where(each => each.gameObject.activeInHierarchy && each.Id != evicted && each.Id != survivor))
            {
                var look = Presentation(npc).LookTarget;
                if (losers.Contains(npc.Id))
                    Assert.That(look != null && look.name.StartsWith("Ceremony look mark"), Is.True,
                        npc.Id + " voted to keep them, and looks down: looking at " + (look != null ? look.name : "nothing") + ".");
                else
                    Assert.That(look, Is.SameAs(leaving.transform), npc.Id + " turns to the one going.");
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
            Assert.That(strip, Has.Some.EqualTo(EpisodeDirector.GoodbyeLine(director.Snapshot, evicted)), "The goodbye line plays here, not at the door.");

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
        /// go, behind a dip, and the door with it. The chrome is aside until then.
        /// </summary>
        [UnityTest]
        public IEnumerator StagedExit_TheDoorShutsBehindThemBeforeTheyGo()
        {
            yield return InstallStagedSeason(52, AtEviction);
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
            // Past the push-in, inside the goodbye.
            yield return RealSeconds(2.2f);
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
    }
}
