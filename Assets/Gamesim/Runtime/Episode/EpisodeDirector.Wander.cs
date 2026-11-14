using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.AI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The house between its conversations (playtest, 2026-09-27; HouseMeetingCoordinator.Wander).
    /// Outside free time and campaigning the house is paused for the week's business, and between
    /// competitions and ceremonies the cast stood wherever the last thing had left them. Now, while
    /// nothing on screen needs them still, a houseguest strolls to a spot in one of the house's
    /// rooms, stands there a while, and goes on - one setting off at a time, so the house never
    /// moves in step.
    ///
    /// <para>Presentation only: <see cref="Random"/> picks the spots, never the season's generator,
    /// and nothing is saved. A panel, a card, a challenge, the opening or a walk out stops everybody
    /// where they stand; free time hands them back to the house's own conversations.</para>
    ///
    /// <para>One exception (plan HR-1): a Head of Household whose decision is open goes up to the HoH suite and waits
    /// there, standing, for the player to come and make their case (<see cref="KeepHeadInSuite"/>).</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>How often the house looks for somebody to set off, and how long an arrival stands before going again.</summary>
        private const float WanderCheckSeconds = 1.5f, WanderRestMin = 5f, WanderRestMax = 14f;

        /// <summary>Where a stroll goes: the rooms anybody uses. Not the diary room, which is private, or the Head of Household's suite.</summary>
        private static readonly string[] WanderRooms = { "Living", "Kitchen", "Bedroom", "Yard", "Nomination" };

        private float nextWanderCheck;
        private readonly Dictionary<string, float> wanderRestUntil = new Dictionary<string, float>();
        private HouseRoomQuery wanderFloors;
        private HouseRoomMarker[] wanderMarkers;

        /// <summary>Whether the house may mill about now: paused for the week's business, with nothing on screen that needs it still.</summary>
        private bool WanderAllowed =>
            IsReady && npcMeetings != null && npcMeetings.IsReady && !npcWorldFailed && !npcDiagnosticsSuspended
            && !IsPanelOpen && !challengeActive && !competitionArenaStaging && !CeremonyOverlays.OnScreen && !TourIsUp
            && (openingStage == null || !openingStage.Active) && !IsCeremonyStaged && walkingOutId == null && playerIsActive && projected != null
            && projected.phase != EpisodePhase.Social && projected.phase != EpisodePhase.Campaign
            && projected.phase != EpisodePhase.Jury && projected.phase != EpisodePhase.JuryQuestioning
            && projected.phase != EpisodePhase.FinalSpeeches && projected.phase != EpisodePhase.Finished;

        private void TickWandering()
        {
            if (npcMeetings == null) return;
            // Who the suite holds follows the window under any pause, so a window that closes behind a card lets go at once.
            string head = SuiteHeadNow();
            if (!WanderAllowed)
            {
                StopWandering();
                return;
            }
            KeepHeadInSuite(head);
            if (Time.unscaledTime < nextWanderCheck) return;
            nextWanderCheck = Time.unscaledTime + WanderCheckSeconds;
            bool setOff = false;
            foreach (var npc in housemates)
            {
                if (npc == null || !npc.gameObject.activeInHierarchy) continue;
                string id = npc.Id;
                // A Head of Household waiting in the suite is the suite's, never the stroll's (KeepHeadInSuite).
                if (id == head) continue;
                if (projected.Find(id)?.status != ContestantStatus.Active) continue;
                // Somebody a staged act holds stands where it happens (EpisodeDirector.AllWeek).
                if (npcMeetings.ActHoldsActor(id)) continue;
                if (npcMeetings.IsWandering(id))
                {
                    if (!npcMeetings.WanderArrived(id)) continue;
                    // Arrived: stand a while before going on.
                    if (!wanderRestUntil.TryGetValue(id, out float rest))
                    { wanderRestUntil[id] = Time.unscaledTime + Random.Range(WanderRestMin, WanderRestMax); continue; }
                    if (Time.unscaledTime < rest) continue;
                }
                // One setting off each look, so the house never moves in step.
                if (setOff) continue;
                if (!TryPickWanderSpot(npc, out var spot) || !npcMeetings.TryWander(id, spot)) continue;
                wanderRestUntil.Remove(id);
                setOff = true;
            }
        }

        private void StopWandering()
        {
            if (npcMeetings != null && npcMeetings.WanderingCount > 0) npcMeetings.EndWandering();
            wanderRestUntil.Clear();
            // Whoever was on the way to the suite stopped with everybody: sent again the first frame the house may move.
            suiteSent = false;
        }

        // ------------------------------------------------------------ the Head of Household's suite (HR-1)

        /// <summary>The room a Head of Household waits in while their decision is open (HouseRoomMarker, HouseRoomQuery).</summary>
        public const string SuiteRoom = "HoH";

        /// <summary>How long a trip to the suite that found no spot or no route waits before it looks again.</summary>
        private const float SuiteRetrySeconds = .5f;

        /// <summary>The Head of Household the suite holds, the spot they were sent to, and whether that trip is still theirs.</summary>
        private string suiteHeadId;
        private Vector3 suiteSpot;
        private bool suiteSpotChosen, suiteSent;
        private float suiteRetryAt;
        private HouseRoomMarker[] suiteMarkers;
        private readonly Collider[] suiteFurnitureHits = new Collider[4];

        /// <summary>
        /// The houseguest who waits in the HoH suite now: the Head of Household while the player can have a word with
        /// them before their decision (<see cref="StrategyRules.IsDecider"/>: before the nominations, and at the veto
        /// meeting while a replacement could be needed). Null when the player is Head of Household, when no window is
        /// open, and outside the strategy rules: then nobody is placed and the house strolls as it always did.
        /// </summary>
        public static string SuiteHeadId(EpisodeState state) =>
            state != null && state.hohId != null && state.hohId != state.playerId && StrategyRules.IsDecider(state, state.hohId)
                ? state.hohId : null;

        /// <summary>The Head of Household the suite holds and whether their trip there is under way or done, for tests.</summary>
        public string SuiteHeadForDiagnostics => suiteHeadId;
        public bool SuiteTripForDiagnostics => suiteSent && suiteHeadId != null && npcMeetings != null && npcMeetings.IsWandering(suiteHeadId);
        public Vector3 SuiteSpotForDiagnostics => suiteSpot;

        /// <summary>
        /// Who the suite holds now, kept up to date under any pause: when the window closes, or moves to somebody
        /// else, the one it held is let go to the stroll, which takes them on at its next look.
        /// </summary>
        private string SuiteHeadNow()
        {
            string head = SuiteHeadId(projected);
            if (head == suiteHeadId) return head;
            if (suiteHeadId != null && npcMeetings.IsWandering(suiteHeadId)) wanderRestUntil[suiteHeadId] = Time.unscaledTime;
            suiteHeadId = head;
            suiteSpotChosen = suiteSent = false;
            suiteRetryAt = 0f;
            return head;
        }

        /// <summary>
        /// Plan HR-1: a Head of Household with a decision open goes up to the HoH suite and stands there until it is made,
        /// so the window line can say where to find them (<see cref="WindowLine"/>). Called every frame the house may
        /// mill about, never on the stroll's cadence: the trip starts the frame the window opens with no panel up, or
        /// the first frame after the panel that was (a panel stops everybody, the Head too, as it always has). Nothing
        /// but the suite sends them anywhere while it holds them; once there they stand until the window closes, and
        /// then the stroll has them again at its next look. A staged act and the Head's own walk-up to the player come
        /// first: the trip waits while either holds them and starts again the frame it lets go. Standing only: the trip
        /// is a stroll's lease to a clear spot on the suite's floor, never a seat, so the suite's furniture stays the
        /// Head's own to use (<see cref="MayUse"/>). Presentation only, as the stroll is: <see cref="Random"/> picks the
        /// spot, and nothing is saved.
        /// </summary>
        private void KeepHeadInSuite(string head)
        {
            if (head == null) return;
            // The trip is still theirs: on the way, or standing at the spot.
            if (suiteSent && npcMeetings.IsWandering(head)) return;
            suiteSent = false;
            if (projected.Find(head)?.status != ContestantStatus.Active) return;
            // A staged act holds them where it happens (EpisodeDirector.AllWeek), and their own walk-up has them.
            if (npcMeetings.ActHoldsActor(head)) return;
            if ((askNow != null && askNow.askerId == head) || (npcInvitationNow != null && npcInvitationNow.NpcId == head)) return;
            if (Time.unscaledTime < suiteRetryAt) return;
            var npc = housemates.FirstOrDefault(actor => actor != null && actor.Id == head && actor.gameObject.activeInHierarchy);
            if (npc == null) return;
            // The spot chosen for this window, kept for the window; a fresh one only if it no longer takes them.
            if (suiteSpotChosen && npcMeetings.TryWander(head, suiteSpot)) { suiteSent = true; return; }
            if (TryPickSuiteSpot(npc, out var spot) && npcMeetings.TryWander(head, spot))
            {
                suiteSpot = spot;
                suiteSpotChosen = suiteSent = true;
                return;
            }
            suiteRetryAt = Time.unscaledTime + SuiteRetrySeconds;
            return;
        }

        /// <summary>
        /// A spot to stand in the HoH suite: on the suite's own floor (the room query says HoH), clear of anybody's
        /// body and of the furniture's layer, so nobody stands in a bed or a chair.
        /// </summary>
        private bool TryPickSuiteSpot(HouseNpc npc, out Vector3 spot)
        {
            spot = default;
            if (suiteMarkers == null)
                suiteMarkers = gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<HouseRoomMarker>(true))
                    .Where(marker => marker.RoomName == SuiteRoom).ToArray();
            if (suiteMarkers.Length == 0) return false;
            if (wanderFloors == null && !HouseRoomQuery.TryCreate(gameObject.scene, out wanderFloors, out _)) return false;
            var body = npc.GetComponent<CapsuleCollider>();
            float radius = body != null ? body.radius : .35f, height = body != null ? body.height : 1.9f;
            var agent = player != null ? player.Agent : null;
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = agent != null ? agent.agentTypeID : 0,
                areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas,
            };
            for (int attempt = 0; attempt < 12; attempt++)
            {
                var marker = suiteMarkers[Random.Range(0, suiteMarkers.Length)];
                if (marker == null) continue;
                var offset = attempt == 0 ? Vector2.zero : Random.insideUnitCircle * 3f;
                var wanted = marker.transform.position + new Vector3(offset.x, 0f, offset.y);
                if (!NavMesh.SamplePosition(wanted, out var hit, 2f, filter)) continue;
                if (!wanderFloors.TrySampleFloor(hit.position, radius, filter, .25f, out var sampled, out string room) || room != SuiteRoom
                    || !wanderFloors.HasCapsuleClearance(sampled, radius, height, npc.transform)
                    || OnSuiteFurniture(sampled, radius, height)) continue;
                spot = sampled;
                return true;
            }
            return false;
        }

        /// <summary>Whether a body standing at <paramref name="feet"/> would stand in a piece of furniture (HouseLayers.Furniture).</summary>
        private bool OnSuiteFurniture(Vector3 feet, float radius, float height) =>
            gameObject.scene.GetPhysicsScene().OverlapCapsule(feet + Vector3.up * (radius + .05f), feet + Vector3.up * (height - radius),
                radius, suiteFurnitureHits, 1 << HouseLayers.Furniture, QueryTriggerInteraction.Ignore) > 0;

        /// <summary>A spot in one of the house's rooms, somewhere other than where they are standing, on floor the house can bind.</summary>
        private bool TryPickWanderSpot(HouseNpc npc, out Vector3 spot)
        {
            spot = default;
            if (wanderMarkers == null)
                wanderMarkers = gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<HouseRoomMarker>(true))
                    .Where(marker => System.Array.IndexOf(WanderRooms, marker.RoomName) >= 0).ToArray();
            if (wanderMarkers.Length == 0) return false;
            if (wanderFloors == null && !HouseRoomQuery.TryCreate(gameObject.scene, out wanderFloors, out _)) return false;
            var body = npc.GetComponent<CapsuleCollider>();
            float radius = body != null ? body.radius : .35f, height = body != null ? body.height : 1.9f;
            var agent = player != null ? player.Agent : null;
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = agent != null ? agent.agentTypeID : 0,
                areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas,
            };
            for (int attempt = 0; attempt < 6; attempt++)
            {
                var marker = wanderMarkers[Random.Range(0, wanderMarkers.Length)];
                if (marker == null) continue;
                var offset = Random.insideUnitCircle * 2.5f;
                var wanted = marker.transform.position + new Vector3(offset.x, 0f, offset.y);
                if ((wanted - npc.transform.position).sqrMagnitude < 4f) continue;
                if (!NavMesh.SamplePosition(wanted, out var hit, 2f, filter)) continue;
                if (!wanderFloors.TrySampleFloor(hit.position, radius, filter, .25f, out var sampled, out _)
                    || !wanderFloors.HasCapsuleClearance(sampled, radius, height, npc.transform)) continue;
                spot = sampled;
                return true;
            }
            return false;
        }
    }
}
