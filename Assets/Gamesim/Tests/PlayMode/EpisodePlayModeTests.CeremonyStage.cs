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
using UnityEngine.SceneManagement;
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
            AskForTheStages();
            yield return null;
        }

        /// <summary>
        /// The stages and the walk outs asked for in a batch run, and reduced motion - which never
        /// stages - switched off on the director, the rig and the bodies the way the motion tests do.
        /// </summary>
        private void AskForTheStages(bool reduced = false)
        {
            director.StagesInBatchRuns = true;
            director.WalkOutsInBatchRuns = true;
            typeof(EpisodeDirector).GetField("reducedMotion", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(director, reduced);
            cameraRig.SetReducedMotion(reduced);
            foreach (var visual in SceneComponents<CharacterPresentation>()) visual.SetReducedMotion(reduced);
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

        /// <summary>The houseguests in the house whose name plate the director has left up, by id.</summary>
        private static string[] PlatesUp() => SceneComponents<HouseNpc>()
            .Where(npc => npc.gameObject.activeInHierarchy && !npc.PlateSuppressed).Select(npc => npc.Id).ToArray();

        /// <summary>The houseguests in the house whose name plate the director has taken down, by id.</summary>
        private static string[] PlatesDown() => SceneComponents<HouseNpc>()
            .Where(npc => npc.gameObject.activeInHierarchy && npc.PlateSuppressed).Select(npc => npc.Id).ToArray();

        /// <summary>The houseguests whose plate is drawn at all, by id: a plate taken down or faded out by distance is not.</summary>
        private static string[] PlatesDrawn() => SceneComponents<HouseNpc>()
            .Where(npc => npc.gameObject.activeInHierarchy)
            .Where(npc =>
            {
                var plate = Plate(npc);
                var group = plate != null ? plate.GetComponent<CanvasGroup>() : null;
                return group != null && group.alpha > 0.01f;
            })
            .Select(npc => npc.Id).ToArray();

        /// <summary>The selection disc under the player, as the scene builds it.</summary>
        private Renderer PlayerDisc() => player.GetComponentsInChildren<Transform>(true)
            .Where(part => part.name == EpisodeDirector.PlayerMarkerName)
            .Select(part => part.GetComponent<Renderer>()).SingleOrDefault(disc => disc != null);

        /// <summary>A rect's lowest and highest points, as x and y, in another rect's own space.</summary>
        private static Vector2 VerticalSpan(RectTransform space, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float low = float.MaxValue, high = float.MinValue;
            foreach (var corner in corners)
            {
                float y = space.InverseTransformPoint(corner).y;
                low = Mathf.Min(low, y);
                high = Mathf.Max(high, y);
            }
            return new Vector2(low, high);
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

        /// <summary>
        /// The card plays to a clean frame (MOCKUP-PASS-PLAN M2). The name plates and the player's
        /// disc are up through the summons, so the player can find a seat by them; they go down as
        /// the card starts on the screen, where a plate hung over every face the camera cut to; and
        /// they come back once the house is let go.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_TheCardPlaysWithNoPlateUpAndTheHouseGetsThemBack()
        {
            yield return InstallStagedSeason(51, AtNomination);
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The nomination is staged in the house.");
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Summons), "The house is summoned first.");
            var disc = PlayerDisc();
            Assert.That(disc, Is.Not.Null, "The scene gives the player a selection disc.");
            Assert.That(PlatesDown(), Is.Empty, "The plates are up through the summons: the player finds a seat by them.");
            Assert.That(disc.enabled, Is.True, "and so is the disc under the player.");

            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing,
                EpisodeDirector.SummonsHardSeconds(CeremonyPace.Suspenseful) + 6f, "the card plays once the seats have filled or the summons has run its course");
            // The director takes the plates down in its Update and each plate draws what it was told
            // in its own LateUpdate, which the frame the card started in has not reached yet.
            yield return null;
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Playing), "The card is still up.");
            Assert.That(PlatesUp(), Is.Empty, "Every plate is down once the card plays on the screen.");
            Assert.That(PlatesDrawn(), Is.Empty, "No plate is drawn over the faces the camera cuts to.");
            Assert.That(disc.enabled, Is.False, "nor the disc at the player's feet.");

            yield return SkipReveals();
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the stage ends with the card");
            yield return Frames(2);
            Assert.That(PlatesDown(), Is.Empty, "The plates come back with the house.");
            Assert.That(disc.enabled, Is.True, "and so does the disc.");
        }

        /// <summary>
        /// The SAFE chip on the set's screen sits inside the photo it labels (MOCKUP-PASS-PLAN M2).
        /// Pinned across the photo's top edge, as on the HUD's board, at the screen's scale it covered
        /// the foot of the Head of Household's line above. The keys are played straight onto the
        /// nomination room's screen - the screen's own frame, with nobody summoned.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_TheSafeChipOnTheScreenClearsTheHeadOfHouseholdsLine()
        {
            Assert.That(ScreenSurface.TryFind(director.gameObject.scene, CeremonySets.NominationRoom, out var screen), Is.True,
                "The nomination room has its ceremony screen.");
            var state = director.Snapshot;
            // Houseguests first, so the Head of Household and the block are theirs and the player draws a key.
            var people = state.Active.OrderBy(actor => actor.isPlayer ? 1 : 0)
                .Select(actor => new KeyCeremony.Person(actor.id, actor.name, null, actor)).ToList();
            Assert.That(people.Count, Is.GreaterThanOrEqualTo(4), "A Head of Household, two on the block, and a key to hand out.");
            var keys = SceneComponents<KeyCeremony>().Single();
            Assert.That(keys.Play(state.week, people[0].Name, false, people.Skip(3).ToList(), people.Skip(1).Take(2).ToList(),
                true, CeremonyPace.Quick, screen), Is.True, "The keys play.");
            Assert.That(keys.Surface, Is.SameAs(screen), "on the screen's frame.");
            yield return WaitFor(() => keys.KeysShown >= 1, 12f, "the first key is out");
            yield return null;
            Canvas.ForceUpdateCanvases();

            var space = (RectTransform)keys.transform;
            var hoh = keys.GetComponentsInChildren<TMPro.TMP_Text>().Single(label => label.name == "HoH");
            var line = VerticalSpan(space, hoh.rectTransform);
            // Every chip up, the one a new key is replacing included: they share the key's place.
            var chips = keys.GetComponentsInChildren<RectTransform>().Where(rect => rect.name == "Badge").ToList();
            Assert.That(chips, Is.Not.Empty, "The key's holder wears the SAFE chip.");
            foreach (var chip in chips)
                Assert.That(VerticalSpan(space, chip).y, Is.LessThanOrEqualTo(line.x + 0.5f),
                    "The chip's top stands under the foot of \"" + hoh.text + "\": the chip spans " + VerticalSpan(space, chip)
                    + " and the line " + line + " on the screen's frame.");
            yield return SkipReveals();
        }

        /// <summary>
        /// A scene unloaded under a card that is still playing - a failed assertion's teardown, a
        /// reload - destroys the stage's anchors before the director's OnDisable cancels the card,
        /// and the cancel releases the stage, which logs its report. The report says the place is
        /// gone instead of throwing (endgame-f34b, 2026-09-29: a MissingReferenceException from
        /// the anchor's approach, in TearDown). The anchor is destroyed and the card cancelled in
        /// one frame, as the unload does, so no tick reads the anchor in between.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_TheReleaseReportsAPlaceDestroyedUnderThePlayingCard()
        {
            yield return InstallStagedSeason(51, AtNomination);
            string hoh = director.Snapshot.hohId;
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The nomination is staged in the house.");
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing,
                EpisodeDirector.SummonsHardSeconds(CeremonyPace.Suspenseful) + 6f, "the card plays");
            var keys = SceneComponents<KeyCeremony>().Single();
            Assert.That(keys.IsPlaying, Is.True, "The key ceremony plays.");

            var head = CeremonySeating.Anchors(director.gameObject.scene, CeremonySeating.NominationHead).First();
            Object.DestroyImmediate(head.gameObject);
            Assert.That(StageReport(), Does.Contain("  " + hoh + " -> " + CeremonySeating.NominationHead + " 0 (standing) :: the place is gone"),
                "The report names the place whose anchor is gone.");
            // The director's OnDisable cancels the card this way at an unload; the release logs its
            // report on the way out. Not LogAssert.NoUnexpectedReceived, which counts the stage's
            // ordinary logs too: an exception here fails the test by itself.
            string released = null;
            void Capture(string message, string stack, LogType type)
            {
                if (message.StartsWith("Ceremony stage report (nomination, Release, release)")) released = message;
            }
            Application.logMessageReceived += Capture;
            try { keys.Cancel(); }
            finally { Application.logMessageReceived -= Capture; }
            Assert.That(released, Does.Contain("  " + hoh + " -> " + CeremonySeating.NominationHead + " 0 (standing) :: the place is gone"),
                "The release's report names the place whose anchor is gone.");
            Assert.That(director.IsCeremonyStaged, Is.False, "The cancelled card released the stage and let the house go.");
            yield return Frames(2);
        }

        /// <summary>
        /// A body on its place is there, whoever is against it (endgame-f34b, 2026-09-29): at a full
        /// table a walker going round was held against a houseguest waiting, not yet seated, on an
        /// approach, and the walker's capsule against theirs withheld the arrival that would have
        /// seated them and parked their agent - so neither ever moved again. Here a body's worth
        /// of collider stands against one approach from the moment the house is sent, where the
        /// walker stood, and the houseguest bound for it sits down all the same.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_AHouseguestOnTheirPlaceSitsWithSomebodyAgainstThem()
        {
            yield return InstallStagedSeason(51, AtNomination);
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The nomination is staged in the house.");
            var seat = CeremonySeating.Anchors(director.gameObject.scene, CeremonySeating.NominationSeat)[2];
            string line = StageReport().Split('\n').FirstOrDefault(each => each.Contains(" -> " + CeremonySeating.NominationSeat + " 2 "));
            Assert.That(line, Is.Not.Null, "Somebody is placed at the third chair:\n" + StageReport());
            string id = line.Trim().Split(' ')[0];
            Assert.That(id, Is.Not.EqualTo(director.Snapshot.playerId), "The third chair is a houseguest's.");

            // Along the ring from the approach, 0.55 m: two bodies' capsules overlap under 0.7.
            var outward = seat.Approach - seat.Position; outward.y = 0f;
            var along = Vector3.Cross(Vector3.up, outward.normalized);
            var against = new GameObject("Somebody against the approach");
            SceneManager.MoveGameObjectToScene(against, director.gameObject.scene);
            against.transform.position = seat.Approach + along * 0.55f;
            var body = against.AddComponent<CapsuleCollider>();
            body.radius = 0.35f; body.height = 1.9f; body.center = Vector3.up * 0.95f;
            Physics.SyncTransforms();

            bool Seated()
            {
                var npc = SceneComponents<HouseNpc>().FirstOrDefault(each => each.Id == id && each.gameObject.activeInHierarchy);
                var chair = npc != null ? npc.GetComponent<HouseSeatPresentation>() : null;
                return chair != null && chair.Active;
            }
            float by = Time.realtimeSinceStartup + 20f;
            while (!Seated() && Time.realtimeSinceStartup < by) yield return null;
            Assert.That(Seated(), Is.True, id + " sits although somebody stands against their approach:\n" + StageReport());
            Object.Destroy(against);
            yield return SkipReveals();
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
            Assert.That(StageReport(), Does.Not.Contain("The house is not free"), "The summons sent the house; nobody waited for the retry.");

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
            // The whole house sits on the gallery (MOCKUP-PASS-PLAN M22), the Head of Household
            // included: fourteen couch seats and the two red chairs, no standing marks.
            yield return WaitFor(() => director.CeremonyStageSeated >= director.CeremonyStagePlaces, 8f,
                "everyone sits down, the Head of Household with the house");
            Assert.That(director.CeremonyStageStanding, Is.Null, "Nobody stands at the head at an eviction.");
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
            // The card down: the evicted stand to say goodbye to the house in its seats
            // (MOCKUP-PASS-PLAN M19), and only then walk out through the front door.
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Goodbye), "The evicted say goodbye first,");
            Assert.That(director.WalkingOutId, Is.Null, "before they walk.");
            Assert.That(Hud.IsHeldForReveal, Is.True, "The chrome stays aside through the goodbye,");
            Assert.That(PlatesUp(), Is.Empty, "and the plates stay down.");
            yield return WaitFor(() => director.WalkingOutId != null, EpisodeDirector.GoodbyeSeconds + 1f, "the goodbye gives way to the walk out");
            Assert.That(director.WalkingOutId, Is.EqualTo(evicted), "The evicted walk out.");
            Assert.That(director.IsCeremonyStaged, Is.True, "The house keeps its seats while they go.");
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Release));
            Assert.That(Hud.IsHeldForReveal, Is.True, "The chrome stays aside for the walk out too: the exit is full-bleed to the shut door.");
            // MOCKUP-PASS-PLAN M2: the walk out is the card's last beat, and its frame is as clean.
            Assert.That(PlatesUp(), Is.Empty, "The plates stay down while the evicted walk out.");
            yield return WaitFor(() => director.WalkingOutId == null, EpisodeDirector.WalkOutSeconds + 2f, "the walk-out ends");
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "and the house gets up");
            yield return null;
            Assert.That(PlatesDown(), Is.Empty, "The plates come back once the house is let go.");
            Assert.That(Hud.IsHeldForReveal, Is.False, "and so does the chrome, once the door is shut behind them.");
            // The stage ends itself on a stopped world, so "the house gets up" held on a dead one
            // (PACK8-PASS-PLAN A1): the walk out has to leave the house's world running.
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "The house's world outlives the walk out.");
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
                // MOCKUP-PASS-PLAN M2: a plate showed through the card's scrim on the HUD frame.
                Assert.That(PlatesUp(), Is.Empty, "No plate is up under the keys on the HUD frame.");
                // PACK8-PASS-PLAN A1: the skip chip is a staged ceremony's; the card on the HUD
                // frame says what moves it on in its own lines.
                yield return null;
                Assert.That(director.CeremonySkipShowing, Is.False, "No skip chip over the keys on the HUD frame.");
            }
            yield return SkipReveals();
        }

        /// <summary>
        /// The look sheet's frames of the staged keys: the screen shot with a key out, and the block,
        /// for the owner to judge the sizes by - each with the rig put back on the screen's own shot,
        /// since the block's beat has already scheduled its cuts to the nominees' seats by the frame
        /// it turns ShowingBlock on (the old block frame was a three-shot of chairs). In both, no
        /// body's hips stand in the card: the Head of Household's mark used to put them 0.8 m in front
        /// of the lens, their head over forty percent of the card (UI-UX-PASS-PLAN N1).
        /// </summary>
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
            var screen = keys.Surface;
            Assert.That(screen, Is.Not.Null, "The keys play on the set's screen.");
            yield return WaitFor(() => keys.KeysShown >= 1, 12f, "the first key is out");
            // The house given a moment to reach its places, so the frame is the one the player sees;
            // the frame is taken either way, and the report says where everybody got to.
            float by = Time.realtimeSinceStartup + 4f;
            while (director.CeremonyStageInPlace < director.CeremonyStagePlaces && Time.realtimeSinceStartup < by) yield return null;
            yield return CaptureTheScreen(screen, "ceremony-stage-key-screen", frame => AssertNoBodyStandsInTheCard(keys, "ceremony-stage-key-screen"));
            yield return WaitFor(() => keys.ShowingBlock, 40f, "the block is up");
            yield return CaptureTheScreen(screen, "ceremony-stage-block", frame => AssertNoBodyStandsInTheCard(keys, "ceremony-stage-block"));
            yield return SkipReveals();
        }

        /// <summary>
        /// Where <paramref name="world"/> lands in the frame the camera draws, as a share of its width
        /// and height from the bottom-left, through the projection the frame is drawn with - the
        /// inverse of <see cref="HousePlayerController.ScreenRay"/>, since the rig's blended lens is
        /// a hand-set matrix a screen-point call would ignore. Null behind the lens.
        /// </summary>
        private static Vector2? InTheFrame(Camera camera, Vector3 world)
        {
            var view = camera.worldToCameraMatrix.MultiplyPoint(world);
            if (view.z >= 0f) return null;
            var clip = camera.projectionMatrix * new Vector4(view.x, view.y, view.z, 1f);
            if (Mathf.Abs(clip.w) < 1e-6f) return null;
            return new Vector2((clip.x / clip.w + 1f) * 0.5f, (clip.y / clip.w + 1f) * 0.5f);
        }

        /// <summary>
        /// No body stands in the card on the screen's shot: every active houseguest's and the
        /// player's hips, projected through the view camera as the frame was, fall outside the card's
        /// rect in that frame. The hips, because a seat moves the visual body and leaves the root at
        /// the chair's approach; the root where a body has no humanoid rig.
        /// </summary>
        private void AssertNoBodyStandsInTheCard(KeyCeremony keys, string name)
        {
            var camera = cameraRig.ViewCamera;
            var card = keys.GetComponentsInChildren<RectTransform>(true).First(rect => rect.name == "Card");
            var corners = new Vector3[4];
            card.GetWorldCorners(corners);
            float xMin = 1f, xMax = 0f, yMin = 1f, yMax = 0f;
            foreach (var corner in corners)
            {
                var at = InTheFrame(camera, corner);
                Assert.That(at.HasValue, Is.True, name + ": the card's corner is behind the lens.");
                xMin = Mathf.Min(xMin, at.Value.x); xMax = Mathf.Max(xMax, at.Value.x);
                yMin = Mathf.Min(yMin, at.Value.y); yMax = Mathf.Max(yMax, at.Value.y);
            }
            var cardRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            Assert.That(cardRect.width, Is.GreaterThan(0.3f), name + ": the card fills the frame " + cardRect + ".");

            var bodies = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy)
                .Select(npc => (npc.Id, (Component)npc)).Concat(new[] { ("the player", (Component)player) });
            var inside = new List<string>();
            foreach (var (id, body) in bodies)
            {
                var hips = HipsOf(body);
                var at = hips != null ? hips.position : body.transform.position + Vector3.up * 0.95f;
                var point = InTheFrame(camera, at);
                if (point.HasValue && cardRect.Contains(point.Value))
                    inside.Add(id + " at " + at.ToString("F2") + " -> " + point.Value.ToString("F2"));
            }
            Assert.That(inside, Is.Empty, name + ": a body stands in the card " + cardRect + " on the screen's shot: "
                + string.Join("; ", inside) + "\n" + StageReport(name));
        }

        /// <summary>
        /// The title's mark on a live card hangs wholly left of the title's words as drawn, in the
        /// card's own space: the week-two defect (UI-UX-PASS-PLAN 1.2) put it over the T, measured
        /// from a label that had not run Awake.
        /// </summary>
        private static void AssertTheTitleMarkClearsTheTitle(KeyCeremony keys, string where)
        {
            Canvas.ForceUpdateCanvases();
            var rects = keys.GetComponentsInChildren<RectTransform>(true);
            var space = rects.First(rect => rect.name == "Card");
            var title = keys.GetComponentsInChildren<TMPro.TMP_Text>(true).First(label => label.name == "Title");
            var mark = rects.FirstOrDefault(rect => rect.name == "Title mark");
            Assert.That(mark, Is.Not.Null, where + ": the card has no 'Title mark'.");
            title.ForceMeshUpdate();
            Rect OnTheCard(Vector3 a, Vector3 b)
            {
                var low = space.InverseTransformPoint(a);
                var high = space.InverseTransformPoint(b);
                return Rect.MinMaxRect(Mathf.Min(low.x, high.x), Mathf.Min(low.y, high.y), Mathf.Max(low.x, high.x), Mathf.Max(low.y, high.y));
            }
            var bounds = title.textBounds;
            var words = OnTheCard(title.rectTransform.TransformPoint(bounds.min), title.rectTransform.TransformPoint(bounds.max));
            var corners = new Vector3[4];
            mark.GetWorldCorners(corners);
            var glyph = OnTheCard(corners[0], corners[2]);
            Assert.That(words.width, Is.GreaterThan(100f), where + ": the title was measured as drawn (" + words + ").");
            Assert.That(glyph.Overlaps(words), Is.False, where + ": the mark " + glyph + " crosses the title's words " + words + ".");
            Assert.That(glyph.xMax, Is.LessThan(words.xMin), where + ": the mark hangs left of the whole title.");
        }

        /// <summary>
        /// The look sheet's frames of the vote on the living room's screen (MOCKUP-PASS-PLAN M18): the
        /// roster with votes on it, and the result read in lines once the board has given way. The
        /// stage cuts to the hot seats between votes, so each frame puts the rig on the screen's own
        /// shot first.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_CapturesTheVoteOnTheLivingScreenAndItsResult()
        {
            if (!Application.isBatchMode) yield break;
            yield return InstallStagedSeason(52, AtEviction);
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The eviction is staged in the living room.");
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing,
                EpisodeDirector.SummonsHardSeconds(CeremonyPace.Suspenseful) + 6f, "the reveal plays");
            var vote = SceneComponents<VoteReveal>().Single();
            Assert.That(vote.Surface, Is.Not.Null, "on the living room's screen");
            var screen = vote.Surface;
            yield return WaitFor(() => vote.VotesShown >= 2, 20f, "two votes are on the board");
            // The living room's real screen names nobody (UI-UX-PASS-PLAN B0): no voter's child, and
            // the Head of Household only on the deciding row.
            var staged = director.Snapshot;
            AssertBoardNamesNobody(vote, staged.hohId != null && staged.hohId != staged.playerId ? staged.Find(staged.hohId)?.name : null);
            yield return CaptureTheScreen(screen, "ceremony-stage-vote-screen");
            yield return WaitFor(() => vote.ShowingResult, 60f, "the result is read");
            // The board hands over to the result block over the result's first moment.
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(vote.IsPlaying, Is.True, "The result is still up to be photographed.");
            yield return CaptureTheScreen(screen, "ceremony-stage-vote-result");
            yield return SkipReveals();
            yield return WaitFor(() => !director.StagedExitRunning, EpisodeDirector.GoodbyeSeconds + EpisodeDirector.WalkOutSeconds + 2f,
                "the goodbye and the walk-out end");
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "and the house gets up");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "The house's world outlives the walk out.");
        }

        /// <summary>
        /// A frame of a card on its screen: the faces the card has bound given their time to land
        /// (bounded, as CaptureFraming's own settle is - a frame of empty discs says nothing about the
        /// screen), then the rig on the screen's own shot for two frames, then the capture, handed to
        /// <paramref name="inspect"/> while the camera still holds it. The rig is put on the shot
        /// after the wait, since the stage's cuts move it between beats.
        /// </summary>
        private IEnumerator CaptureTheScreen(ScreenSurface screen, string name, System.Action<Texture2D> inspect = null)
        {
            float until = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < until && AnyBoundFaceIsStillMissing()) yield return null;
            cameraRig.MoveTo(screen.Shot());
            yield return Frames(2);
            yield return CaptureFraming(name, settle: false, inspect: inspect);
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
            // D1: the card waited for the house, the stuck were re-sent, the lost seats retaken -
            // everyone but the Head of Household is in a chair eight seconds into the keys.
            Assert.That(director.CeremonyStageInPlace, Is.EqualTo(director.CeremonyStagePlaces),
                "Everyone has reached their place eight seconds into the card:\n" + StageReport("the places"));
            Assert.That(director.CeremonyStageSeated, Is.EqualTo(state.Active.Count() - 1),
                "Everyone but the Head of Household is in a chair eight seconds into the card:\n" + StageReport("the seats"));
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
            var placed = start.Split('\n').Where(line => line.Contains(" -> ")).ToList();
            Assert.That(placed.Count(line => line.Contains(" -> " + CeremonySeating.GallerySeat + " ")), Is.EqualTo(EpisodeValidation.MaximumCast - 2),
                "Fourteen on the U's couches:\n" + start);
            Assert.That(placed.Count(line => line.Contains(" -> " + CeremonySeating.HotSeat + " ")), Is.EqualTo(2), "two in the red chairs,");
            Assert.That(placed.Any(line => line.Contains(" -> " + CeremonySeating.LivingMark + " ")), Is.False, "and nobody on a standing mark.");
            // D1: the summons sends the house although the evicted is still binding on that frame.
            Assert.That(start, Does.Not.Contain("The house is not free"), "The summons sent the house; nobody waited for the retry.");
            Debug.Log("Full-house eviction: " + (start.Split('\n').Length - 1) + " places for " + state.Active.Count() + " houseguests.");
            yield return CaptureLivingRoom("ceremony-stage-full-house-living");
            yield return LetTheHouseSettle();
            var later = StageReport("eight seconds into the card");
            Debug.Log(later);
            // Everyone is in their place, and every place is a seat: the whole house sits together.
            Assert.That(director.CeremonyStageInPlace, Is.EqualTo(director.CeremonyStagePlaces), "Everyone has reached their place eight seconds into the card:\n" + later);
            foreach (var line in later.Split('\n').Where(each => each.Contains(" -> ")))
                Assert.That(line.Trim(), Does.EndWith(":: seated"), "Every place is sat in: " + line.Trim());
            yield return CaptureLivingRoom("ceremony-stage-full-house-living-later");
            yield return SkipReveals();
            yield return WaitFor(() => !director.StagedExitRunning, EpisodeDirector.GoodbyeSeconds + EpisodeDirector.WalkOutSeconds + 2f,
                "the goodbye and the walk-out end");
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "and the house gets up");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "The house's world outlives the walk out at a full house too.");
        }
    }
}
