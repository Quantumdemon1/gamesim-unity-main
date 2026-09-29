using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
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
        /// The stage's own report of where everybody it placed got to - the report a seat that never
        /// filled leaves behind - as the stage logs it at its card's start and its release.
        /// </summary>
        private string StageReport(string moment = "read by the test") => director.CeremonyStageReport(moment) ?? "No ceremony is staged.";

        /// <summary>
        /// A house of <paramref name="houseguests"/> at <paramref name="phase"/>, the cast screen's
        /// own season at the roster's largest and houseguests added beyond it the way the cast-size
        /// tests add them, so a stage can be measured at the house's full size
        /// (<see cref="EpisodeValidation.MaximumCast"/>), which no roster reaches on its own.
        /// </summary>
        private static EpisodeState FullHouse(uint seed, int houseguests)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = houseguests }, seed);
            string[] rooms = { "Living", "Kitchen", "Bedroom", "Yard" };
            while (state.contestants.Count < houseguests)
            {
                int index = state.contestants.Count;
                var extra = new ContestantState
                {
                    id = "extra-" + index,
                    name = "Extra " + index,
                    pronouns = "they/them",
                    homeRoom = rooms[index % rooms.Length],
                    motive = "Added by a test to fill the house to its largest size.",
                    status = ContestantStatus.Active,
                    archetype = "The Newcomer", age = 30, occupation = "Houseguest",
                    traits = new List<string> { "Social" },
                    stats = new ContestantStats(),
                };
                foreach (var other in state.contestants)
                {
                    state.relationships.Add(new RelationshipState { fromId = extra.id, toId = other.id, score = 0 });
                    state.relationships.Add(new RelationshipState { fromId = other.id, toId = extra.id, score = 0 });
                }
                state.contestants.Add(extra);
            }
            return state;
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
        private IEnumerator InstallStagedSeason(uint seed, System.Action<EpisodeState> shape, int houseguests = 0)
        {
            HoldTheHouseForTheFixture();
            if (houseguests > 0)
            {
                var state = FullHouse(seed, houseguests);
                shape?.Invoke(state);
                Assert.That(EpisodeValidation.TryValidate(state, out var invalid), Is.True, invalid);
                new EpisodeSaveStore(director.SavePath).Save(state);
                yield return ReloadEpisode();
            }
            else yield return InstallStrategySeason(seed, shape);
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
            if (!BothSeated()) Debug.Log(StageReport());
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

        // ---------------------------------------------------------------- the full house (CEREMONY-CUTSCENES-PLAN §7.1)

        /// <summary>A frame of a set from above, for the look sheet: the rig put on the shot for two frames, then the capture. Batch runs only.</summary>
        private IEnumerator CaptureSet(string name, Vector3 focus, float distance, float pitch, float yaw)
        {
            if (!Application.isBatchMode) yield break;
            cameraRig.MoveTo(new HouseCameraRig.Shot { Focus = focus, Distance = distance, Pitch = pitch, Yaw = yaw, FieldOfView = 45f, Seconds = 0.01f });
            yield return Frames(2);
            yield return CaptureFraming(name, settle: false);
        }

        /// <summary>The nomination table from over its head, the whole ring in frame.</summary>
        private IEnumerator CaptureTable(string name) => CaptureSet(name, new Vector3(0.075f, 0.9f, -14.6f), 8.5f, 40f, 180f);

        /// <summary>The living room from over its north wall, looking at the faces the screen looks at.</summary>
        private IEnumerator CaptureLivingRoom(string name) => CaptureSet(name, new Vector3(-5f, 1.0f, -4.5f), 9f, 35f, 180f);

        /// <summary>The house given its time past the card's start: eight seconds, in which everyone who will sit has.</summary>
        private IEnumerator LetTheHouseSettle()
        {
            float by = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < by && director.IsCeremonyStaged) yield return null;
        }

        /// <summary>
        /// The measurement in front of the seating fixes (CEREMONY-CUTSCENES-PLAN §7.1, D0): a house
        /// of sixteen gathered for the keys, the stage's report read at the card's start and once the
        /// house has had its time, and the table from above for the look sheet. It asserts the
        /// report, not the seating - a place for everyone and a line for every place - because what
        /// the report says is what D1 is built on, and a fault it names is the finding, not a failure.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_AFullHouseNominationReportsEveryPlaceAndCapturesTheTable()
        {
            yield return InstallStagedSeason(61, AtNomination, EpisodeValidation.MaximumCast);
            var state = director.Snapshot;
            Assert.That(state.Active.Count(), Is.EqualTo(EpisodeValidation.MaximumCast), "The house is at its largest.");
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The nomination is staged at a full house.");
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing,
                EpisodeDirector.SummonsHardSeconds(CeremonyPace.Suspenseful) + 6f, "the card plays");
            var start = StageReport("card start, read by the test");
            Debug.Log(start);
            Assert.That(start.Split('\n').Length - 1, Is.EqualTo(state.Active.Count()), "A place for everyone, and a line for every place.");
            var seats = CeremonySeating.Anchors(director.gameObject.scene, CeremonySeating.NominationSeat);
            Assert.That(seats.Count, Is.EqualTo(state.Active.Count() - 1), "A chair for everyone who draws a key.");
            yield return CaptureTable("ceremony-stage-full-house-table");
            yield return LetTheHouseSettle();
            Debug.Log(StageReport("eight seconds into the card"));
            yield return CaptureTable("ceremony-stage-full-house-table-later");
            yield return SkipReveals();
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the stage ends with the card");
        }

        /// <summary>
        /// The same measurement on eviction night at a full house: the hot seats are the nominees'
        /// by id whatever their bodies did - the report's lines say so - and the living room is
        /// captured from over its north wall, at the card's start and once the house has settled.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_AFullHouseEvictionReportsTheHotSeatsAndCapturesTheRoom()
        {
            yield return InstallStagedSeason(62, AtEviction, EpisodeValidation.MaximumCast);
            var state = director.Snapshot;
            var block = state.nominees.ToList();
            Assert.That(state.Active.Count(), Is.EqualTo(EpisodeValidation.MaximumCast), "The house is at its largest.");
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The eviction is staged at a full house.");
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing,
                EpisodeDirector.SummonsHardSeconds(CeremonyPace.Suspenseful) + 6f, "the reveal plays");
            var start = StageReport("card start, read by the test");
            Debug.Log(start);
            foreach (var id in block)
                Assert.That(start, Does.Contain("  " + id + " -> " + CeremonySeating.HotSeat + " "), id + " holds a hot seat's place, whoever is sitting where.");
            Assert.That(start.Split('\n').Length - 1, Is.GreaterThanOrEqualTo(6), "The nominees, the head, and the sofa's three are placed at the least.");
            Debug.Log("Full-house eviction: " + (start.Split('\n').Length - 1) + " places for " + state.Active.Count() + " houseguests.");
            yield return CaptureLivingRoom("ceremony-stage-full-house-living");
            yield return LetTheHouseSettle();
            Debug.Log(StageReport("eight seconds into the card"));
            yield return CaptureLivingRoom("ceremony-stage-full-house-living-later");
            yield return SkipReveals();
            yield return WaitFor(() => director.WalkingOutId == null, EpisodeDirector.WalkOutSeconds + 2f, "the walk-out ends");
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "and the house gets up");
        }
    }
}
