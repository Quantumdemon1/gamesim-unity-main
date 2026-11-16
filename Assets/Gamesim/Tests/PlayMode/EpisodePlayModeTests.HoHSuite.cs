using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The Head of Household waits in the HoH suite (plan HR-1). While nominations are open, and at the veto meeting
    /// while a replacement could be needed, an NPC Head of Household goes up to the suite and stands there, and the
    /// window line says where to find them; the case the player makes there is the plea they make today. A panel stops
    /// everybody, the Head too, and the trip starts again the frame it closes; a staged act comes first; the stroll
    /// never takes them out; the names said, the suite lets them go; a player who is Head of Household changes nothing.
    /// The house's world is built past free time (BuildNpcWorldForDiagnostics), asks are off, and the clock is pinned
    /// where the bodies walk (Time.captureDeltaTime): the stroll and the trip look on the real clock, the agents move on
    /// the game's.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>Game time per frame while the suite tests walk: a batch frame is far shorter than a played one.</summary>
        private const float SuiteFrameSeconds = 1f / 30f;

        /// <summary>The bound a trip from the farthest room keeps, in frames of <see cref="SuiteFrameSeconds"/>: thirty seconds of game time.</summary>
        private const int SuiteTripFrameBound = 900;

        /// <summary>
        /// The game time a jog up from the farthest room keeps (stack O2's fixes): the walk took 19.1 s on 14 Nov, the lead's
        /// line is about 12 s, and the jog's 4 m/s makes it about 11 s; the bound leaves a little for the turns.
        /// </summary>
        private const float SuiteJogTripSeconds = 14f;

        /// <summary>Nominations with the first houseguest as Head of Household, the names not yet said.</summary>
        private static void SuiteAtNominations(EpisodeState state)
        {
            state.phase = EpisodePhase.Nomination;
            state.hohId = state.Active.First(actor => !actor.isPlayer).id;
        }

        private static HouseNpc SuiteBody(string id) =>
            SceneComponents<HouseNpc>().FirstOrDefault(npc => npc.Id == id && npc.gameObject.activeInHierarchy);

        private HouseMeetingCoordinator SuiteHouse() => NpcRead<HouseMeetingCoordinator>("npcMeetings");

        /// <summary>Builds the house's world and waits, on the real clock, until its people are bound.</summary>
        private IEnumerator SuiteWorld()
        {
            director.AsksEnabled = false;
            Application.runInBackground = true;
            Time.captureDeltaTime = SuiteFrameSeconds;
            director.BuildNpcWorldForDiagnostics();
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
            yield return WaitFor(() => SuiteHouse() != null && SuiteHouse().IsReady, 10f, "The house's world is bound.");
        }

        /// <summary>Waits, a frame at a time, until the Head stands at the suite's spot in the suite; the frames it took.</summary>
        private IEnumerator SuiteArrival(string hoh, System.Action<int> took)
        {
            var house = SuiteHouse();
            var body = SuiteBody(hoh);
            int frames = 0;
            while (frames < 2 * SuiteTripFrameBound
                && !(director.SuiteTripForDiagnostics && house.WanderArrived(hoh) && RoomAt(body.transform) == EpisodeDirector.SuiteRoom))
            {
                frames++;
                yield return null;
            }
            took(frames);
        }

        /// <summary>Whether a body standing where it stands is in a piece of furniture (HouseLayers.Furniture).</summary>
        private static bool SuiteInFurniture(Transform body)
        {
            var capsule = body.GetComponent<CapsuleCollider>();
            float radius = capsule != null ? capsule.radius : .35f, height = capsule != null ? capsule.height : 1.9f;
            var feet = body.position;
            return Physics.CheckCapsule(feet + Vector3.up * (radius + .05f), feet + Vector3.up * (height - radius), radius,
                1 << HouseLayers.Furniture, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Whether a houseguest sits: their body in a seat, or a seat's presentation holding them.</summary>
        private static bool SuiteSeated(HouseNpc npc)
        {
            var visual = npc.GetComponent<CharacterPresentation>();
            var seat = npc.GetComponent<HouseSeatPresentation>();
            return (visual != null && visual.IsSeated) || (seat != null && seat.Active);
        }

        /// <summary>The room whose marker stands farthest from the suite's, the diary room and the suite left out, and a floor point in it.</summary>
        private Vector3 SuiteFarthestFloor(float radius, out string room)
        {
            var suite = ActiveRoomMarkers().First(marker => marker.RoomName == EpisodeDirector.SuiteRoom).transform.position;
            foreach (var marker in ActiveRoomMarkers().Where(m => m.RoomName != EpisodeDirector.SuiteRoom && m.RoomName != "Private")
                .OrderByDescending(m => FlatDistance(m.transform.position, suite)))
                for (int direction = 0; direction < 17; direction++)
                {
                    float angle = direction * Mathf.PI / 8f, reach = direction == 0 ? 0f : direction <= 8 ? 1f : 2f;
                    var candidate = marker.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach;
                    if (!NavMesh.SamplePosition(candidate, out var hit, 1.5f, player.Agent.areaMask)) continue;
                    if (RoomAt(hit.position, radius) != marker.RoomName) continue;
                    room = marker.RoomName;
                    return hit.position;
                }
            Assert.Fail("No floor in any room but the suite.");
            room = null;
            return default;
        }

        [UnityTest]
        public IEnumerator HoHSuite_TheWindowLineSendsThePlayerToTheHoHRoom()
        {
            // One decider: the Head of Household before the nominations, in the suite.
            yield return InstallStrategySeason(41, SuiteAtNominations);
            director.SuspendNpcAutonomyForDiagnostics();
            var state = director.Snapshot;
            string hoh = state.Find(state.hohId).name;
            Assert.That(EpisodeDirector.SuiteHeadId(state), Is.EqualTo(state.hohId));
            string line = EpisodeDirector.WindowLine(state);
            Assert.That(line, Does.StartWith("Nominations are " + hoh + "'s call. Find " + hoh + " in the HoH room for a word before it happens: you have "),
                "The line says where the Head waits.");
            Assert.That(line, Does.Not.Contain("in the house"));
            yield return OpenStation();
            Assert.That(ShownText(), Does.Contain(line), "The station points the player up to the suite.");
            director.ClosePanels();
            yield return null;

            // Two deciders at the veto meeting: the holder in the house, the Head (a replacement to name) in the suite.
            yield return InstallStrategySeason(42, s => AtVetoMeeting(s, false));
            director.SuspendNpcAutonomyForDiagnostics();
            state = director.Snapshot;
            Assert.That(StrategyRules.Deciders(state), Is.EqualTo(new[] { state.vetoHolderId, state.hohId }));
            line = EpisodeDirector.WindowLine(state);
            Assert.That(line, Does.StartWith("The veto meeting is next. Find " + state.Find(state.vetoHolderId).name + " in the house or "
                + state.Find(state.hohId).name + " in the HoH room for a word before it happens: you have "), "Each decider is placed.");

            // The player holds the veto: the Head alone, in the suite.
            yield return InstallStrategySeason(43, s => AtVetoMeeting(s, true));
            director.SuspendNpcAutonomyForDiagnostics();
            state = director.Snapshot;
            line = EpisodeDirector.WindowLine(state);
            Assert.That(line, Does.StartWith("The veto meeting is next. Find " + state.Find(state.hohId).name + " in the HoH room for a word"));

            // The player as Head of Household: nobody to find, nobody in the suite.
            yield return InstallStrategySeason(44, s => { s.phase = EpisodePhase.Nomination; s.hohId = s.playerId; });
            director.SuspendNpcAutonomyForDiagnostics();
            Assert.That(EpisodeDirector.WindowLine(director.Snapshot), Is.Null);
            Assert.That(EpisodeDirector.SuiteHeadId(director.Snapshot), Is.Null);
        }

        [UnityTest]
        public IEnumerator HoHSuite_UnderAPanelNobodyMovesAndTheHeadSetsOffTheFrameItCloses()
        {
            yield return InstallStrategySeason(41, SuiteAtNominations);
            string hoh = director.Snapshot.hohId;
            try
            {
                // The window is open before the house can move, and a panel is up when it can.
                director.AsksEnabled = false;
                Application.runInBackground = true;
                Time.captureDeltaTime = SuiteFrameSeconds;
                director.BuildNpcWorldForDiagnostics();
                Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
                director.OpenJournal();
                Assert.That(director.IsPanelOpen, Is.True);
                yield return WaitFor(() => SuiteHouse() != null && SuiteHouse().IsReady, 10f, "The house's world is bound under the panel.");
                var house = SuiteHouse();
                var bodies = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy).ToList();
                var held = bodies.ToDictionary(npc => npc.Id, npc => npc.transform.position);
                yield return RealSeconds(1f);
                Assert.That(house.WanderingCount, Is.Zero, "A panel stops everybody, the Head too.");
                Assert.That(director.SuiteTripForDiagnostics, Is.False, "No trip under the panel.");
                var drifted = bodies.FirstOrDefault(npc => FlatDistance(npc.transform.position, held[npc.Id]) > .3f);
                Assert.That(drifted, Is.Null, "Nobody moves" + (drifted != null ? ": " + drifted.Id + " did." : "."));

                // The stroll's next look is far off as the panel closes, so a trip on its cadence could not start next frame
                // (the review's finding on HR-1: the trip keeps no 1.5 s cadence).
                NpcWrite("nextWanderCheck", Time.unscaledTime + 10f);
                director.ClosePanels();
                yield return null;
                Assert.That(director.SuiteHeadForDiagnostics, Is.EqualTo(hoh));
                Assert.That(director.SuiteTripForDiagnostics, Is.True, "The frame the panel is gone the Head sets off for the suite,");
                Assert.That(house.IsWandering(hoh), Is.True, "on a stroll's lease,");
                Assert.That(RoomAt(director.SuiteSpotForDiagnostics, .35f), Is.EqualTo(EpisodeDirector.SuiteRoom), "to a spot in the suite.");
            }
            finally { Time.captureDeltaTime = 0f; }
        }

        [UnityTest]
        public IEnumerator HoHSuite_FromTheFarthestRoomTheHeadReachesTheSuiteStandsAndStays()
        {
            yield return InstallStrategySeason(41, SuiteAtNominations);
            string hoh = director.Snapshot.hohId;
            try
            {
                // Before the world binds, the Head stands in the room farthest from the suite.
                var body = SuiteBody(hoh);
                Assert.That(body, Is.Not.Null);
                var capsule = body.GetComponent<CapsuleCollider>();
                var far = SuiteFarthestFloor(capsule != null ? capsule.radius : .35f, out string farRoom);
                var agent = body.GetComponent<NavMeshAgent>();
                if (agent != null && agent.enabled) { Assert.That(agent.Warp(far), Is.True); agent.ResetPath(); }
                else body.transform.position = far;
                Physics.SyncTransforms();

                yield return SuiteWorld();
                var house = SuiteHouse();
                yield return WaitFor(() => director.SuiteTripForDiagnostics, 3f, "The Head sets off for the suite.");
                var from = body.transform.position;
                string fromRoom = RoomAt(body.transform);
                Assert.That(fromRoom, Is.Not.EqualTo(EpisodeDirector.SuiteRoom), "They start outside it.");
                // A jog up, for this trip alone: the Head's pace and run take, and nobody else's.
                var headMotion = body.GetComponent<HouseNpcMotion>();
                var headVisual = body.GetComponent<CharacterPresentation>();
                Assert.That(headMotion.Speed, Is.EqualTo(EpisodeDirector.SuiteTripSpeed).Within(.001f), "The Head jogs up to the suite,");
                Assert.That(director.SuiteJoggingForDiagnostics(hoh), Is.True);
                if (headVisual != null) Assert.That(headVisual.IsRunning, Is.True, "on the run take.");
                foreach (var other in SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy && npc.Id != hoh))
                {
                    var otherMotion = other.GetComponent<HouseNpcMotion>();
                    if (otherMotion != null) Assert.That(otherMotion.Speed, Is.EqualTo(HouseNpcMotion.DefaultSpeed).Within(.001f), other.Id + " walks at the house's pace.");
                    var otherVisual = other.GetComponent<CharacterPresentation>();
                    if (otherVisual != null) Assert.That(otherVisual.IsRunning, Is.False, other.Id + " does not run.");
                }
                int frames = -1;
                yield return SuiteArrival(hoh, took => frames = took);
                float seconds = frames * SuiteFrameSeconds;
                TestContext.Out.WriteLine("HoH suite trip: from the " + fromRoom + " (farthest: " + farRoom + "), "
                    + FlatDistance(from, director.SuiteSpotForDiagnostics).ToString("0.0") + " m straight, "
                    + frames + " frames = " + seconds.ToString("0.0") + " s of game time.");
                Assert.That(frames, Is.LessThanOrEqualTo(SuiteTripFrameBound),
                    "The Head is in the suite within " + SuiteTripFrameBound * SuiteFrameSeconds + " s of game time; took " + seconds + " s.");
                // At the jog the farthest room is about 12 s away (the walk took 19.1 s on 14 Nov): the Head is up there soon.
                Assert.That(seconds, Is.LessThanOrEqualTo(SuiteJogTripSeconds),
                    "At a jog the Head is in the suite within " + SuiteJogTripSeconds + " s of game time; took " + seconds.ToString("0.0") + " s.");
                yield return null;
                Assert.That(director.SuiteJoggingForDiagnostics(hoh), Is.False, "Arrived, they stand: the jog is over");
                if (headVisual != null) Assert.That(headVisual.IsRunning, Is.False, "and the run take off.");

                // Standing: no seat, no activity, and not in the suite's furniture.
                Assert.That(SuiteSeated(body), Is.False, "The Head stands.");
                Assert.That(house.TryGetActivity(hoh, out _), Is.False, "and uses nothing,");
                Assert.That(SuiteInFurniture(body.transform), Is.False, "clear of the furniture.");

                // The stroll looks every frame and is told everybody is rested, the Head included: it never takes them.
                var motion = body.GetComponent<HouseNpcMotion>();
                Assert.That(motion, Is.Not.Null);
                string lease = motion.LeaseId;
                Assert.That(lease, Is.Not.Null);
                var rest = NpcRead<Dictionary<string, float>>("wanderRestUntil");
                for (int frame = 0; frame < 120; frame++)
                {
                    NpcWrite("nextWanderCheck", 0f);
                    foreach (string id in rest.Keys.ToList()) rest[id] = 0f;
                    rest[hoh] = 0f;
                    yield return null;
                    Assert.That(motion.LeaseId, Is.EqualTo(lease), "The stroll never sends the Head anywhere (frame " + frame + ").");
                }
                Assert.That(RoomAt(body.transform), Is.EqualTo(EpisodeDirector.SuiteRoom), "They are still in the suite,");
                Assert.That(SuiteSeated(body), Is.False, "standing.");
                Assert.That(director.SuiteTripForDiagnostics, Is.True);
            }
            finally { Time.captureDeltaTime = 0f; }
        }

        [UnityTest]
        public IEnumerator HoHSuite_APleaInTheSuiteSendsTheCommandItSendsToday()
        {
            yield return InstallStrategySeason(41, SuiteAtNominations);
            string hoh = director.Snapshot.hohId;
            try
            {
                yield return SuiteWorld();
                int frames = -1;
                yield return SuiteArrival(hoh, took => frames = took);
                Assert.That(RoomAt(SuiteBody(hoh).transform), Is.EqualTo(EpisodeDirector.SuiteRoom), "The Head waits in the suite.");

                // The player goes up and has the word they have today: the plea, its command unchanged.
                yield return TalkTo(hoh);
                Assert.That(director.IsConversationOpen, Is.True);
                Assert.That(RoomAt(player.transform), Is.EqualTo(EpisodeDirector.SuiteRoom), "The player is up in the suite with them.");
                var state = director.Snapshot;
                string name = state.Find(hoh).name;
                int revision = state.revision;
                yield return EnterConversationCategory(EpisodeDirector.GameCategoryCaption);
                Assert.That(ShownText(), Does.Contain(EpisodeDirector.LobbyHeading(state)));
                ButtonWithCaption(EpisodeHud.LobbyAskCaption(state, name, LobbyAsk.Spare, state.playerId)).onClick.Invoke();
                yield return null;
                Assert.That(director.Snapshot.lobbies, Is.Empty, "Choosing what to ask commits nothing.");
                ButtonWithCaption(EpisodeHud.LobbyApproachCaption(LobbyApproach.Emotional, true)).onClick.Invoke();
                yield return null; yield return null;
                var after = director.Snapshot;
                var plea = after.lobbies.Single();
                Assert.That((plea.deciderId, plea.ask, plea.subjectId, plea.approach),
                    Is.EqualTo((hoh, LobbyAsk.Spare, state.playerId, LobbyApproach.Emotional)), "The plea it is today.");
                Assert.That(after.revision, Is.GreaterThan(revision));
            }
            finally { Time.captureDeltaTime = 0f; }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator HoHSuite_AStagedActComesFirstAndTheTripStartsTheFrameItLetsGo()
        {
            NpcActState act = null;
            yield return InstallAllWeekFixture(s => AtTheNominations(s) && s.hohId != s.playerId, s =>
            {
                int window = EpisodeEngine.Window(s);
                s.npcSocial.acts.RemoveAll(a => a.window == window);
                string partner = s.Active.First(c => !c.isPlayer && c.id != s.hohId).id;
                act = new NpcActState
                {
                    id = s.week + "-" + window + "-91", kind = NpcActKinds.Talk, actorId = s.hohId, partnerId = partner, room = "Kitchen",
                    week = s.week, window = window, firedTick = EpisodeEngine.WindowTick(s, window), sighted = false,
                };
                s.npcSocial.acts.Add(act);
            }, "the nominations' window under a houseguest's HoH");
            string hoh = director.Snapshot.hohId;
            director.AsksEnabled = false;
            PlayTheActs();
            try
            {
                var house = SuiteHouse();
                yield return WaitForTheActStaged(act);
                Assert.That(director.SuiteHeadForDiagnostics, Is.EqualTo(hoh), "The window is open: the suite wants the Head,");
                Assert.That(house.ActHoldsActor(hoh), Is.True, "but the act has them,");
                Assert.That(director.SuiteTripForDiagnostics, Is.False, "so there is no trip,");
                Assert.That(house.IsWandering(hoh), Is.False, "and nothing walks them off.");
                Assert.That(RoomAt(ActBody(hoh)), Is.EqualTo("Kitchen"));

                // Seen, the act lets them go; the next frame they are on their way up.
                WarpBeside(act);
                yield return WaitUntilSeen(act, 20f);
                Assert.That(ActNow(act.id).sighted, Is.True);
                yield return WaitFor(() => !house.ActHoldsActor(hoh), 10f, "The act lets the Head go.");
                yield return null;
                Assert.That(director.SuiteTripForDiagnostics, Is.True, "The trip to the suite starts the frame the act lets go.");
            }
            finally { StopPlayingTheActs(); }
        }

        [UnityTest]
        public IEnumerator HoHSuite_WhenTheNamesAreSaidTheSuiteLetsTheHeadGo()
        {
            yield return InstallStrategySeason(41, SuiteAtNominations);
            string hoh = director.Snapshot.hohId;
            try
            {
                yield return SuiteWorld();
                yield return WaitFor(() => director.SuiteTripForDiagnostics, 3f, "The Head sets off for the suite.");
                yield return OpenStation();
                director.ContinueEpisode();
                yield return WaitFor(() => director.Snapshot.nominees.Count == 2, 5f, "The Head of Household names two.");
                Assert.That(EpisodeDirector.WindowLine(director.Snapshot), Is.Null, "The window is closed,");
                Assert.That(EpisodeDirector.SuiteHeadId(director.Snapshot), Is.Null);
                yield return WaitFor(() => director.SuiteHeadForDiagnostics == null, 3f, "and the suite lets the Head go,");
                Assert.That(director.SuiteTripForDiagnostics, Is.False, "with no trip to it.");
                director.ClosePanels();
                yield return Frames(30);
                Assert.That(director.SuiteHeadForDiagnostics, Is.Null, "Nobody is sent up again.");
                Assert.That(director.SuiteTripForDiagnostics, Is.False);

                // Back to the usual rooms (the review's finding on HR-1): the stroll takes the Head like anybody else. It looks
                // every frame here, and everybody else it has sent stays resting, so its one send a look comes to the Head.
                var house = SuiteHouse();
                var rest = NpcRead<Dictionary<string, float>>("wanderRestUntil");
                float until = Time.realtimeSinceStartup + 20f;
                while (!house.IsWandering(hoh) && Time.realtimeSinceStartup < until)
                {
                    NpcWrite("nextWanderCheck", 0f);
                    foreach (string id in rest.Keys.ToList()) rest[id] = id == hoh ? 0f : Time.unscaledTime + 1000f;
                    yield return null;
                }
                Assert.That(house.IsWandering(hoh), Is.True, "Once the names are said the stroll takes the Head like anybody else,");
                Assert.That(director.SuiteTripForDiagnostics, Is.False, "on a stroll, not a trip to the suite,");
                Assert.That(director.SuiteJoggingForDiagnostics(hoh), Is.False, "and at a walk:");
                Assert.That(SuiteBody(hoh).GetComponent<HouseNpcMotion>().Speed, Is.EqualTo(HouseNpcMotion.DefaultSpeed).Within(.001f),
                    "the house's own pace.");
            }
            finally { Time.captureDeltaTime = 0f; }
        }

        private static readonly System.Reflection.MethodInfo SuiteSpotPicker = typeof(EpisodeDirector).GetMethod("TryPickSuiteSpot",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        /// <summary>The suite spot picker's answer for this body, called as the trip calls it; the spot, or null for none.</summary>
        private Vector3? PickSuiteSpot(HouseNpc npc)
        {
            var args = new object[] { npc, null };
            return (bool)SuiteSpotPicker.Invoke(director, args) ? (Vector3?)(Vector3)args[1] : null;
        }

        /// <summary>Whether a body of this size standing at <paramref name="feet"/> would stand in a piece of furniture (HouseLayers.Furniture).</summary>
        private static bool SuiteFeetInFurniture(Vector3 feet, float radius, float height) =>
            Physics.CheckCapsule(feet + Vector3.up * (radius + .05f), feet + Vector3.up * (height - radius), radius,
                1 << HouseLayers.Furniture, QueryTriggerInteraction.Ignore);

        /// <summary>
        /// A floor point in the suite where a body would stand in the suite's furniture (a bed, a chair): the floor the NavMesh
        /// finds nearest the middle of a furniture proxy in the suite. Fails when the suite has none.
        /// </summary>
        private static Vector3 SuiteFurnitureFloor(Vector3 suiteMarker, float radius, float height)
        {
            foreach (var proxy in Physics.OverlapSphere(suiteMarker, 8f, 1 << HouseLayers.Furniture, QueryTriggerInteraction.Ignore)
                .OrderBy(collider => FlatDistance(collider.bounds.center, suiteMarker)))
            {
                if (!NavMesh.SamplePosition(proxy.bounds.center, out var hit, 2f, NavMesh.AllAreas)) continue;
                if (RoomAt(hit.position, radius) != EpisodeDirector.SuiteRoom) continue;
                if (SuiteFeetInFurniture(hit.position, radius, height)) return hit.position;
            }
            Assert.Fail("No furniture in the suite stands over its floor.");
            return default;
        }

        /// <summary>
        /// The review's finding on HR-1: the spot the Head is sent to is on the suite's own floor and off its furniture by the
        /// picker's checks, not by where the suite's marker happens to stand. With the picker's one marker moved onto the
        /// suite's furniture, the marker's own spot (the first it tries) is refused and every spot it gives is clear; moved to
        /// the room farthest from the suite, it gives none, because nothing within its reach is the suite's floor.
        /// </summary>
        [UnityTest]
        public IEnumerator HoHSuite_TheSuiteSpotIsOnItsFloorAndOffItsFurniture()
        {
            yield return InstallStrategySeason(41, SuiteAtNominations);
            string hoh = director.Snapshot.hohId;
            HouseRoomMarker marker = null;
            Vector3 home = default;
            try
            {
                yield return SuiteWorld();
                Assert.That(SuiteSpotPicker, Is.Not.Null);
                var npc = SuiteBody(hoh);
                var capsule = npc.GetComponent<CapsuleCollider>();
                float radius = capsule != null ? capsule.radius : .35f, height = capsule != null ? capsule.height : 1.9f;
                marker = ActiveRoomMarkers().First(m => m.RoomName == EpisodeDirector.SuiteRoom);
                home = marker.transform.position;
                NpcWrite("suiteMarkers", new[] { marker });

                var furniture = SuiteFurnitureFloor(home, radius, height);
                marker.transform.position = new Vector3(furniture.x, home.y, furniture.z);
                int found = 0;
                for (int pick = 0; pick < 40; pick++)
                {
                    var spot = PickSuiteSpot(npc);
                    if (spot == null) continue;
                    found++;
                    Assert.That(RoomAt(spot.Value, radius), Is.EqualTo(EpisodeDirector.SuiteRoom), "Every spot is on the suite's floor (pick " + pick + ").");
                    Assert.That(SuiteFeetInFurniture(spot.Value, radius, height), Is.False, "and off its furniture (pick " + pick + ").");
                }
                Assert.That(found, Is.GreaterThan(0), "With the marker on the furniture the picker still finds the suite's clear floor.");

                var far = SuiteFarthestFloor(radius, out string farRoom);
                marker.transform.position = far;
                for (int pick = 0; pick < 20; pick++)
                    Assert.That(PickSuiteSpot(npc), Is.Null, "From the " + farRoom + " no spot within reach is the suite's (pick " + pick + ").");
            }
            finally
            {
                if (marker != null) marker.transform.position = home;
                NpcWrite("suiteMarkers", null);
                Time.captureDeltaTime = 0f;
            }
        }

        [UnityTest]
        public IEnumerator HoHSuite_APlayerHeadOfHouseholdsHouseStrollsAsBefore()
        {
            yield return InstallStrategySeason(44, s => { s.phase = EpisodePhase.Nomination; s.hohId = s.playerId; });
            var installed = director.Snapshot;
            try
            {
                yield return SuiteWorld();
                yield return RealSeconds(3f);
                Assert.That(EpisodeDirector.SuiteHeadId(director.Snapshot), Is.Null, "Nobody waits in the player's suite.");
                Assert.That(director.SuiteHeadForDiagnostics, Is.Null);
                Assert.That(director.SuiteTripForDiagnostics, Is.False, "Nobody is placed.");
                Assert.That(EpisodeDirector.WindowLine(director.Snapshot), Is.Null);
                AssertEquivalent(installed, director.Snapshot);
            }
            finally { Time.captureDeltaTime = 0f; }
        }
    }
}
