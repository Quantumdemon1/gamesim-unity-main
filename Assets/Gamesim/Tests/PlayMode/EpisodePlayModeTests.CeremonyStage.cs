using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The ceremonies as cut scenes (CEREMONY-CUTSCENES-PLAN): a nomination gathers the house to the
    /// table, stands the Head of Household at its head, plays the keys on the set's screen once the
    /// seats have filled, and lets everybody go when the card is down; an eviction seats the
    /// nominees in the hot seats and hands the evicted to the walk-out. A batch run stages nothing
    /// unless asked, as the walk-out is.
    ///
    /// <para>Every wait is bounded in real seconds: the stage, the cards and the seats run on the
    /// unscaled clock. Bodies are measured at the hips where a seat is asserted, because a seat moves
    /// only the visual body and leaves the navigation root at the chair's approach.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The nomination ceremony about to be decided: the first houseguest is Head of Household, nobody named yet.</summary>
        private static void AtNomination(EpisodeState state)
        {
            var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            state.phase = EpisodePhase.Nomination;
            state.hohId = npcs[0];
            state.nominees = new List<string>();
        }

        /// <summary>
        /// Where each of <paramref name="ids"/> got to on the stage: their place, whether the
        /// coordinator still holds their route, whether they arrived, and what their agent says -
        /// the report a seat that never filled leaves behind.
        /// </summary>
        private string StageReport(IEnumerable<string> ids)
        {
            var meetings = (HouseMeetingCoordinator)typeof(EpisodeDirector)
                .GetField("npcMeetings", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(director);
            var lines = new List<string> { "Ceremony stage report (" + director.CeremonyStageKind + ", " + director.CeremonyStagePhase
                + ", seated " + director.CeremonyStageSeated + "):" };
            foreach (var id in ids)
            {
                var npc = SceneComponents<HouseNpc>().FirstOrDefault(each => each.Id == id && each.gameObject.activeInHierarchy);
                if (npc == null) { lines.Add("  " + id + ": no active body"); continue; }
                var place = meetings != null ? meetings.CeremonyPlace(id) : null;
                var motion = npc.GetComponent<HouseNpcMotion>();
                var agent = motion != null ? motion.Agent : null;
                var seat = npc.GetComponent<HouseSeatPresentation>();
                lines.Add("  " + id + " at " + npc.transform.position.ToString("F2")
                    + (place != null ? " -> " + place.VenueId + " " + place.Slot + " approach " + place.Approach.ToString("F2")
                        + " (" + Flat(npc.transform.position, place.Approach).ToString("F2") + " m away)" : " -> no place")
                    + (meetings != null ? " holds=" + meetings.CeremonyActorHolds(id) + " arrived=" + meetings.CeremonyActorArrived(id) : "")
                    + (motion != null ? " lease=" + (motion.LeaseId ?? "none") + " bound=" + motion.IsBound + " state=" + motion.State
                        + (motion.FailureReason != null ? " failure='" + motion.FailureReason + "'" : "") : " no motion")
                    + (agent == null ? " no agent"
                        : !agent.isActiveAndEnabled ? " agent: disabled"
                        : !agent.isOnNavMesh ? " agent: off the NavMesh"
                        : " agent: hasPath=" + agent.hasPath + " status=" + agent.pathStatus
                            + " remaining=" + agent.remainingDistance.ToString("F2") + " stopped=" + agent.isStopped)
                    + " seat=" + (seat == null ? "none" : seat.Active ? "active" : "idle"));
            }
            return string.Join("\n", lines);
        }

        /// <summary>Eviction night: the first houseguest is Head of Household, the next two on the block, the house about to vote.</summary>
        private static void AtEviction(EpisodeState state)
        {
            var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            state.phase = EpisodePhase.Eviction;
            state.hohId = npcs[0];
            state.nominees = new List<string> { npcs[1], npcs[2] };
            state.vetoHolderId = npcs[3];
            state.vetoPlayers = state.Active.Select(actor => actor.id).Take(EpisodeEngine.VetoPlayerCount(state.Active.Count())).ToList();
            if (!state.vetoPlayers.Contains(state.vetoHolderId)) state.vetoPlayers[state.vetoPlayers.Count - 1] = state.vetoHolderId;
            state.vetoResolved = true;
        }

        /// <summary>
        /// A season shaped for a ceremony, with the house's world built for it and the stages asked
        /// for: a fixture installed past the first social phase has no world of its own, a batch run
        /// stages nothing unless asked, and reduced motion - which the stage never plays under - is
        /// switched off on the director and the rig the way the motion tests do.
        /// </summary>
        private IEnumerator InstallStagedSeason(uint seed, System.Action<EpisodeState> shape)
        {
            HoldTheHouseForTheFixture();
            yield return InstallStrategySeason(seed, shape);
            director.BuildNpcWorldForDiagnostics();
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
            director.StagesInBatchRuns = true;
            director.WalkOutsInBatchRuns = true;
            typeof(EpisodeDirector).GetField("reducedMotion", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(director, false);
            cameraRig.SetReducedMotion(false);
            foreach (var visual in SceneComponents<CharacterPresentation>()) visual.SetReducedMotion(false);
            yield return null;
        }

        /// <summary>Submits the house's next legal decisions until a ceremony is staged, or the cards play unstaged.</summary>
        private IEnumerator PlayUntilTheCeremony(int steps = 12)
        {
            for (int i = 0; i < steps && !director.IsCeremonyStaged; i++)
            {
                var keys = SceneComponents<KeyCeremony>().SingleOrDefault();
                var vote = SceneComponents<VoteReveal>().SingleOrDefault();
                if ((keys != null && keys.IsPlaying) || (vote != null && vote.IsPlaying)) yield break;
                var result = director.Submit(NextCommand(director.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
                yield return null;
            }
        }

        private static Transform HipsOf(Component body)
        {
            var animator = body.GetComponentsInChildren<Animator>().FirstOrDefault(rig => rig.isHuman && rig.isActiveAndEnabled);
            return animator != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
        }

        [UnityTest]
        public IEnumerator CeremonyStage_TheNominationGathersTheHouseToTheTableAndPlaysTheKeysOnTheScreen()
        {
            yield return InstallStagedSeason(51, AtNomination);
            var state = director.Snapshot;
            string hoh = state.hohId;
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The nomination is staged in the house.");
            Assert.That(director.CeremonyStageKind, Is.EqualTo(CeremonySting.NominationKind));
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Summons), "The house is summoned first.");
            Assert.That(director.CeremonyStageStanding, Is.EqualTo(hoh), "The Head of Household stands at the head of the table.");
            var keys = SceneComponents<KeyCeremony>().Single();
            Assert.That(keys.IsPlaying, Is.False, "The card waits for the house to gather.");
            Assert.That(Hud.IsHeldForReveal, Is.True, "The chrome steps aside from the summons: it is drawn from the committed result.");
            Assert.That(cameraRig.HasShot, Is.True, "The camera is on the stage's wide.");

            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing,
                EpisodeDirector.SummonsHardSeconds(CeremonyPace.Suspenseful) + 6f, "the card plays once the seats have filled or the summons has run its course");
            Assert.That(keys.IsPlaying, Is.True, "The key ceremony plays.");
            var screen = director.CeremonyStageScreen;
            Assert.That(screen, Is.Not.Null, "The stage has the nomination room's screen.");
            Assert.That(keys.Surface, Is.SameAs(screen), "The card plays on the set's screen, not the HUD.");
            var canvas = keys.GetComponent<Canvas>();
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace), "The card is a world-space canvas on the screen's face.");
            Assert.That(Vector3.Distance(canvas.transform.position, screen.Centre), Is.LessThan(0.1f), "hung on the face");
            Assert.That(keys.GetComponentsInChildren<RectTransform>(true).Count(rect => rect.name.StartsWith("Key ")),
                Is.EqualTo(state.Active.Count() - 1 - 2), "with a key for everyone who draws one.");

            // Seated, at the hips: a seat moves the visual body onto the chair and leaves the root at the approach.
            yield return WaitFor(() => director.CeremonyStageSeated >= 3, 10f, "the house sits down as it arrives");
            var seats = CeremonySeating.Anchors(director.gameObject.scene, CeremonySeating.NominationSeat);
            Assert.That(seats.Count, Is.GreaterThanOrEqualTo(state.Active.Count() - 1), "A chair for everyone who draws a key.");
            var sitter = SceneComponents<HouseNpc>().FirstOrDefault(npc => npc.gameObject.activeInHierarchy && npc.Id != hoh
                && npc.GetComponent<HouseSeatPresentation>() != null && npc.GetComponent<HouseSeatPresentation>().Active);
            Assert.That(sitter, Is.Not.Null, "Somebody is in a chair.");
            yield return WaitFor(() => sitter.GetComponent<HouseSeatPresentation>().Settled, 3f, "and settled in it");
            var hips = HipsOf(sitter);
            var at = hips != null ? hips.position : sitter.GetComponent<HouseSeatPresentation>().VisualFeet;
            Assert.That(seats.Min(seat => FlatDistance(at, seat.Position)), Is.LessThan(0.5f), sitter.Id + " sits on a chair at the table.");
            var head = SceneComponents<HouseNpc>().First(npc => npc.Id == hoh);
            var headVisual = head.GetComponent<CharacterPresentation>();
            Assert.That(headVisual.IsSeated, Is.False, "The Head of Household does not sit.");

            // The card down: the house is let go, the camera and the player are theirs again.
            yield return SkipReveals();
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the stage ends with the card");
            yield return Frames(2);
            Assert.That(SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy)
                .All(npc => npc.GetComponent<HouseSeatPresentation>() == null || !npc.GetComponent<HouseSeatPresentation>().Active
                    || npc.GetComponent<HouseSeatPresentation>().IsExiting), Is.True, "The chairs empty.");
            Assert.That(player.HasActivityOwner, Is.False, "The player has their body back.");
            Assert.That(Hud.IsHeldForReveal, Is.False, "The chrome is back.");
        }

        [UnityTest]
        public IEnumerator CeremonyStage_TheEvictionSeatsTheNomineesInTheHotSeatsAndHandsTheEvictedToTheWalkOut()
        {
            yield return InstallStagedSeason(52, AtEviction);
            var state = director.Snapshot;
            var block = state.nominees.ToList();
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The eviction is staged in the living room.");
            Assert.That(director.CeremonyStageKind, Is.EqualTo(CeremonySting.EvictionKind));
            var vote = SceneComponents<VoteReveal>().Single();
            Assert.That(vote.IsPlaying, Is.False, "The reveal waits for the house to take its seats.");

            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing,
                EpisodeDirector.SummonsHardSeconds(CeremonyPace.Suspenseful) + 6f, "the reveal plays once the nominees have taken the hot seats");
            Assert.That(vote.IsPlaying, Is.True);
            Assert.That(vote.Surface, Is.Not.Null, "on the living room's screen");
            Assert.That(vote.Surface.Room, Is.EqualTo("Living"));

            var hot = CeremonySeating.Anchors(director.gameObject.scene, CeremonySeating.HotSeat);
            Assert.That(hot.Count, Is.EqualTo(2), "Two hot seats face the screen.");
            // The nominees may start at the far end of the house: the stage allows them the
            // summons and then some, and so does this. On a miss, say where each of them got to.
            bool BothSeated() => block.All(id =>
            {
                var npc = SceneComponents<HouseNpc>().FirstOrDefault(each => each.Id == id && each.gameObject.activeInHierarchy);
                var seat = npc != null ? npc.GetComponent<HouseSeatPresentation>() : null;
                return seat != null && seat.Active;
            });
            float seatsBy = Time.realtimeSinceStartup + 8f;
            while (!BothSeated() && Time.realtimeSinceStartup < seatsBy) yield return null;
            if (!BothSeated()) Debug.Log(StageReport(block));
            Assert.That(BothSeated(), Is.True, "both nominees sit in the hot seats");
            foreach (var id in block)
            {
                var npc = SceneComponents<HouseNpc>().First(each => each.Id == id);
                var seat = npc.GetComponent<HouseSeatPresentation>();
                yield return WaitFor(() => seat.Settled, 3f, id + " settles");
                var hips = HipsOf(npc);
                var at = hips != null ? hips.position : seat.VisualFeet;
                Assert.That(hot.Min(chair => FlatDistance(at, chair.Position)), Is.LessThan(0.5f), id + " is in a hot seat.");
            }

            string evicted = vote.EvictedId;
            Assert.That(evicted, Is.Not.Null.And.Not.Empty);
            yield return SkipReveals();
            yield return Frames(3);
            // The card down: the house keeps its seats while the evicted walk out through the front door.
            Assert.That(director.WalkingOutId, Is.EqualTo(evicted), "The evicted walk out.");
            Assert.That(director.IsCeremonyStaged, Is.True, "The house keeps its seats while they go.");
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Release));
            Assert.That(Hud.IsHeldForReveal, Is.False, "The chrome is back once the card is down.");
            yield return WaitFor(() => director.WalkingOutId == null, EpisodeDirector.WalkOutSeconds + 2f, "the walk-out ends");
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "and the house gets up");
        }

        [UnityTest]
        public IEnumerator CeremonyStage_ABatchRunStagesNothingUnlessAsked()
        {
            HoldTheHouseForTheFixture();
            yield return InstallStrategySeason(53, AtNomination);
            director.BuildNpcWorldForDiagnostics();
            Assert.That(director.StagesInBatchRuns, Is.False, "The default: stages are for play, as walk-outs are.");
            yield return PlayUntilTheCeremony();
            var keys = SceneComponents<KeyCeremony>().Single();
            if (Application.isBatchMode)
            {
                Assert.That(director.IsCeremonyStaged, Is.False, "A batch run stages nothing unless asked.");
                Assert.That(keys.IsPlaying, Is.True, "The keys play on the HUD at once.");
                Assert.That(keys.Surface, Is.Null);
                Assert.That(keys.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            }
            yield return SkipReveals();
        }

        /// <summary>The look sheet's frames of the staged keys: the screen shot and the block, for the owner to judge the sizes by.</summary>
        [UnityTest]
        public IEnumerator CeremonyStage_CapturesTheKeysOnTheScreenAndTheBlock()
        {
            if (!Application.isBatchMode) yield break;
            yield return InstallStagedSeason(51, AtNomination);
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True);
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing,
                EpisodeDirector.SummonsHardSeconds(CeremonyPace.Suspenseful) + 6f, "the card plays");
            var keys = SceneComponents<KeyCeremony>().Single();
            yield return WaitFor(() => keys.KeysShown >= 1, 12f, "the first key is out");
            yield return CaptureFraming("ceremony-stage-key-screen", settle: false);
            yield return WaitFor(() => keys.ShowingBlock, 40f, "the block is up");
            yield return CaptureFraming("ceremony-stage-block", settle: false);
            yield return SkipReveals();
        }
    }
}
