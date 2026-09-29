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
            if (!WanderAllowed)
            {
                StopWandering();
                return;
            }
            if (Time.unscaledTime < nextWanderCheck) return;
            nextWanderCheck = Time.unscaledTime + WanderCheckSeconds;
            bool setOff = false;
            foreach (var npc in housemates)
            {
                if (npc == null || !npc.gameObject.activeInHierarchy) continue;
                string id = npc.Id;
                if (projected.Find(id)?.status != ContestantStatus.Active) continue;
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
        }

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
