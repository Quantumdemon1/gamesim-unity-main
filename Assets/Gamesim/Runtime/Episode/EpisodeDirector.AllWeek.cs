using System;
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
    /// The house's acts where the player can see them (WAVE-D-NPC-PACTS-PLAN §4.4, D2-S3). Under the all-week
    /// rules the engine keeps each of the week's acts in a room; the director stages the oldest open ones - two at
    /// most, nobody in two (<see cref="EpisodeEngine.StageableActs"/>) - walking their two to that room
    /// (<see cref="HouseMeetingCoordinator.TryStageAct"/>), and watches for the player seeing them: the three in
    /// the act's room, the two within <see cref="WalkInPairMetres"/> of each other and the player within
    /// <see cref="WalkInReachMetres"/> of one of them, held for <see cref="ActHoldSeconds"/> of free roam, looked at
    /// twice a second. Then it tells the engine (<see cref="EpisodeCommandKind.WitnessNpcAct"/>), as the walk-in
    /// watch does; the engine decides.
    ///
    /// <para>Presentation only, never saved: <see cref="UnityEngine.Random"/> picks the places, never the
    /// season's generator. A reload stages the act again while it is still open, and its sighting is said once
    /// (<see cref="NpcActState.sighted"/>). The stage gives way to the house's own claims - a saved conversation,
    /// a ceremony, a scene, the arena, the opening, the walk out, the player's talk - and a batch run plays none
    /// of it unless a test asks (<see cref="ActsInBatchRuns"/>), as it plays no ceremony stage.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>For tests: stage the house's acts and watch for the player seeing them even in a batch run, where both are otherwise off.</summary>
        public bool ActsInBatchRuns { get; set; }

        /// <summary>How often the house looks, how long the player must hold a sighting, and half the room two staged people stand apart.</summary>
        private const float ActWatchSeconds = .5f, ActHoldSeconds = 2f, ActStandApart = .7f;

        private float actWatchElapsed, actWatchHeld;
        private string actWatched;
        private HouseRoomMarker[] actMarkers;
        private HouseRoomQuery actFloors;

        /// <summary>The acts the house has staged right now. A read for tests.</summary>
        public IReadOnlyList<string> StagedActs => npcMeetings == null ? new string[0] : npcMeetings.StagedActIds.ToArray();

        /// <summary>The act whose sighting the watch is holding, or null. A read for tests.</summary>
        public string WatchedAct => actWatched;

        /// <summary>Whether the acts play here at all: the house's world up, the rules on, and a batch run asking.</summary>
        private bool ActsPlay => npcMeetings != null && npcMeetings.IsReady && !npcWorldFailed && !npcDiagnosticsSuspended
            && projected != null && EpisodeEngine.AllWeekOn(projected) && (!Application.isBatchMode || ActsInBatchRuns);

        /// <summary>Whether the acts may hold people now: nothing that outranks them has the house.</summary>
        private bool ActsStageable => ActsPlay && IsReady && !blockedRecovery && !competitionArenaStaging
            && (openingStage == null || !openingStage.Active) && !IsCeremonyStaged && walkingOutId == null && !npcMeetings.HasSceneStage;

        /// <summary>
        /// The watch's guards (§4.5): the stroll's own (<see cref="WanderAllowed"/>) but its phase, and no overview,
        /// no save under way, no blocked recovery, no diary, and a window open. The overview's map gives no sightings.
        /// </summary>
        private bool ActWatchAllowed => ActsStageable && !IsPanelOpen && !challengeActive && !CeremonyOverlays.OnScreen && !TourIsUp
            && playerIsActive && !overviewOpen && !durableCommitInProgress && !diaryOpen && EpisodeEngine.Window(projected) != Windows.None;

        private void TickAllWeekActs(float delta)
        {
            if (npcMeetings == null) return;
            if (!ActsStageable) { ReleaseStagedActs(); return; }
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0) return;
            actWatchElapsed += delta;
            if (actWatchElapsed < ActWatchSeconds) return;
            float elapsed = Mathf.Min(actWatchElapsed, 1f);
            actWatchElapsed = 0f;
            StageActs();
            WatchActs(elapsed);
        }

        /// <summary>Stages the acts the engine offers and lets go of every other: seen, closed, or with somebody gone.</summary>
        private void StageActs()
        {
            npcMeetings.ReleaseInvalidActs();
            var state = projected;
            var wanted = EpisodeEngine.StageableActs(state);
            var ids = new HashSet<string>(wanted.Select(act => act.id), StringComparer.Ordinal);
            foreach (string id in npcMeetings.StagedActIds.ToArray()) if (!ids.Contains(id)) npcMeetings.ReleaseAct(id);
            // A saved conversation's two and the player's talk are never borrowed.
            var busy = new HashSet<string>(state.npcSocial.pending.SelectMany(row => new[] { row.firstId, row.secondId }), StringComparer.Ordinal);
            if (talkSpot != null) busy.Add(talkSpot.NpcId);
            foreach (var act in wanted)
            {
                if (npcMeetings.IsActStaged(act.id) || busy.Contains(act.actorId) || busy.Contains(act.partnerId)) continue;
                if (TryActPlaces(act, out var first, out var second))
                    npcMeetings.TryStageAct(act.id, act.actorId, act.partnerId, first, second);
            }
        }

        /// <summary>
        /// Whether the player is seeing a staged act, held long enough: then the engine is told. A sighting that
        /// stops - somebody walks off, the player leaves - starts again from nothing.
        /// </summary>
        private void WatchActs(float elapsed)
        {
            if (!ActWatchAllowed) { actWatched = null; actWatchHeld = 0f; return; }
            var state = projected;
            NpcActState seen = null;
            foreach (string id in npcMeetings.StagedActIds)
            {
                var act = state.npcSocial.acts.FirstOrDefault(a => a != null && a.id == id);
                if (act == null || EpisodeEngine.WitnessRefusal(state, act.id, act.actorId, act.partnerId) != null || !Witnessing(act)) continue;
                seen = act;
                break;
            }
            if (seen == null || seen.id != actWatched)
            {
                actWatched = seen?.id;
                actWatchHeld = 0f;
                return;
            }
            actWatchHeld += elapsed;
            if (actWatchHeld + .0001f < ActHoldSeconds) return;
            actWatched = null;
            actWatchHeld = 0f;
            Submit(new EpisodeCommand
            {
                id = Guid.NewGuid().ToString("N"), actorId = state.playerId, kind = EpisodeCommandKind.WitnessNpcAct,
                expectedPhase = state.phase, expectedRevision = state.revision,
                targetId = seen.actorId, secondTargetId = seen.partnerId, text = seen.id,
            });
        }

        /// <summary>The player sees the act: the three in its room, the two together, the player close to one of them.</summary>
        private bool Witnessing(NpcActState act)
        {
            var one = BodyFor(act.actorId);
            var two = BodyFor(act.partnerId);
            if (one == null || two == null || player == null) return false;
            var at = player.transform.position;
            if (RoomOf(one.position) != act.room || RoomOf(two.position) != act.room || RoomOf(at) != act.room) return false;
            if (Across(one.position, two.position) > WalkInPairMetres) return false;
            return Mathf.Min(Across(at, one.position), Across(at, two.position)) <= WalkInReachMetres;
        }

        /// <summary>Which room a point is in: its nearest room marker's, as the house's occupancy reads it.</summary>
        private string RoomOf(Vector3 position)
        {
            var markers = ActMarkers();
            HouseRoomMarker nearest = null;
            float best = float.MaxValue;
            foreach (var marker in markers)
            {
                if (marker == null) continue;
                float distance = (marker.transform.position - position).sqrMagnitude;
                if (distance >= best) continue;
                best = distance; nearest = marker;
            }
            return nearest != null ? nearest.RoomName : null;
        }

        private HouseRoomMarker[] ActMarkers() => actMarkers ?? (actMarkers = gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<HouseRoomMarker>(true))
            .Where(marker => !string.IsNullOrEmpty(marker.RoomName)).ToArray());

        /// <summary>
        /// Two places to stand in the act's room, near its marker, a little over a metre apart, each on floor the
        /// house can bind with room for a body; false when the room has none to give.
        /// </summary>
        private bool TryActPlaces(NpcActState act, out Vector3 first, out Vector3 second)
        {
            first = second = default;
            var marker = ActMarkers().FirstOrDefault(m => m != null && m.RoomName == act.room);
            if (marker == null) return false;
            if (actFloors == null && !HouseRoomQuery.TryCreate(gameObject.scene, out actFloors, out _)) return false;
            var one = BodyFor(act.actorId);
            var body = one != null ? one.GetComponent<CapsuleCollider>() : null;
            float radius = body != null ? body.radius : .35f, height = body != null ? body.height : 1.9f;
            var agent = player != null ? player.Agent : null;
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = agent != null ? agent.agentTypeID : 0,
                areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas,
            };
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var offset = UnityEngine.Random.insideUnitCircle * 1.5f;
                var centre = marker.transform.position + new Vector3(offset.x, 0f, offset.y);
                var turn = UnityEngine.Random.insideUnitCircle.normalized;
                if (turn.sqrMagnitude < .5f) turn = Vector2.right;
                var apart = new Vector3(turn.x, 0f, turn.y) * ActStandApart;
                if (!Place(centre + apart, out first) || !Place(centre - apart, out second)) continue;
                if (Across(first, second) < radius * 2f + .2f || Across(first, second) > WalkInPairMetres - .5f) continue;
                if (RoomOf(first) != act.room || RoomOf(second) != act.room) continue;
                return true;
            }
            return false;

            bool Place(Vector3 wanted, out Vector3 place)
            {
                place = default;
                if (!NavMesh.SamplePosition(wanted, out var hit, 1.5f, filter)) return false;
                if (!actFloors.TrySampleFloor(hit.position, radius, filter, .25f, out place, out _)) return false;
                return actFloors.HasCapsuleClearance(place, radius, height, one);
            }
        }

        private void ReleaseStagedActs()
        {
            if (npcMeetings != null && npcMeetings.StagedActCount > 0) npcMeetings.EndActStaging();
            actWatched = null;
            actWatchHeld = 0f;
            actWatchElapsed = 0f;
        }
    }
}
