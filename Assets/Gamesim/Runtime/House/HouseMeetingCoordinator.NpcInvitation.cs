using System;
using UnityEngine;

namespace Gamesim.House
{
    /// <summary>A bounded, coordinator-owned visit to the player's current position; never saved.</summary>
    public sealed class HouseNpcInvitationLease
    {
        public string NpcId { get; }
        public string Generation { get; }
        public Vector3 Destination { get; }
        public string RoomId { get; }
        public bool Released { get; internal set; }
        internal string Token { get; }
        internal Vector3 PlayerOrigin { get; }

        internal HouseNpcInvitationLease(string npcId, string generation, string token,
            Vector3 destination, string roomId, Vector3 playerOrigin)
        {
            NpcId = npcId; Generation = generation; Token = token;
            Destination = destination; RoomId = roomId; PlayerOrigin = playerOrigin;
        }
    }

    public sealed partial class HouseMeetingCoordinator
    {
        private sealed class NpcInvitationEntry
        {
            public HouseNpcInvitationLease lease;
            public Actor actor;
            public HousePlayerController player;
            public CapsuleCollider body;
            public float expiresAt;
        }

        private const float NpcInvitationDistance = 1.8f;
        private const float NpcInvitationPlayerDrift = .75f;
        private const float NpcInvitationLifetime = 25f;
        private NpcInvitationEntry npcInvitation;

        /// <summary>
        /// Reserve a complete, clear route beside the player. Existing conversations, activities and
        /// staged acts are never displaced. A stroll may yield after a route is proved. The player
        /// keeps control and may walk away, which invalidates this bounded visit rather than dragging
        /// them back or chasing indefinitely.
        /// </summary>
        public bool TryReserveNpcInvitation(string npcId, HousePlayerController player,
            out HouseNpcInvitationLease lease, out string reason)
        {
            lease = null;
            if (disposed || !IsReady || NpcInvitationStageOwnsHouse || talk != null || npcInvitation != null)
                return Fail(out reason, "The house is not free for a visit.");
            if (player == null || !player.isActiveAndEnabled || player.gameObject.scene != rooms.Scene
                || player.HasActivityOwner || player.Agent == null || !player.Agent.enabled || !player.Agent.isOnNavMesh
                || npcId == null || !eligible.Contains(npcId) || !actors.TryGetValue(npcId, out var actor)
                || actor.npc == null || actor.motion == null || !actor.motion.IsBound)
                return Fail(out reason, "The player and houseguest must be bound to this house.");
            if (actor.motion.LeaseId != null && !WanderOwnsMotion(actor.motion))
                return Fail(out reason, "The houseguest is busy.");
            var body = actor.npc.GetComponent<CapsuleCollider>();
            if (body == null || !rooms.TryLocate(player.transform.position, player.Agent.radius, out var playerRoom))
                return Fail(out reason, "The visit requires a clear house floor and a bound body.");

            Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f) forward = Vector3.forward;
            Vector3 best = default; float shortest = float.PositiveInfinity; bool found = false;
            // Fixed angular order keeps this presentation deterministic and allocates no candidate list.
            for (int index = 0; index < 12; index++)
            {
                Vector3 requested = player.transform.position
                    + Quaternion.Euler(0f, index * 30f, 0f) * forward * NpcInvitationDistance;
                if (!rooms.TrySampleFloor(requested, body.radius, filter, .25f, out var point, out var room)
                    || room != playerRoom || !ClearsDestinations(point)
                    || HorizontalSquared(point, player.transform.position) < Mathf.Pow(body.radius + player.Agent.radius + .2f, 2)
                    || !rooms.HasCapsuleClearance(point, body.radius, body.height, actor.npc.transform)
                    || !MeasurePath(actor.motion.Agent, point, firstPath, out float length)) continue;
                if (length >= shortest - .0001f) continue;
                best = point; shortest = length; found = true;
            }
            if (!found) return Fail(out reason, "No clear complete route reaches the player.");

            // Direct motion reservations belong to this coordinator and are recognized by Reconcile.
            YieldWander(npcId);
            if (actor.motion.LeaseId != null) return Fail(out reason, "The houseguest acquired another activity.");
            actor.motion.SetPaused(false);
            string token = "invitation:" + Guid.NewGuid().ToString("N");
            if (!actor.motion.TryReserveAndPath(token, best))
            {
                actor.motion.SetPaused(paused);
                return Fail(out reason, "The houseguest cannot walk to the player.");
            }
            lease = new HouseNpcInvitationLease(npcId, Generation, token, best, playerRoom, player.transform.position);
            npcInvitation = new NpcInvitationEntry { lease = lease, actor = actor, player = player,
                body = body, expiresAt = Time.unscaledTime + NpcInvitationLifetime };
            LastFailure = null; reason = null;
            return true;
        }

        public bool NpcInvitationValid(HouseNpcInvitationLease lease)
        {
            if (disposed || lease == null || lease.Released || npcInvitation == null
                || !ReferenceEquals(npcInvitation.lease, lease) || lease.Generation != Generation
                || NpcInvitationStageOwnsHouse || Time.unscaledTime >= npcInvitation.expiresAt
                || !eligible.Contains(lease.NpcId) || !ValidActor(npcInvitation.actor, lease.Token)) return false;
            var visitor = npcInvitation.actor.npc;
            var player = npcInvitation.player;
            if (visitor == null || npcInvitation.body == null || player == null || !player.isActiveAndEnabled
                || player.gameObject.scene != rooms.Scene || player.HasActivityOwner || player.Agent == null
                || !player.Agent.enabled || !player.Agent.isOnNavMesh
                || HorizontalSquared(player.transform.position, lease.PlayerOrigin) > NpcInvitationPlayerDrift * NpcInvitationPlayerDrift
                || Mathf.Abs(player.transform.position.y - lease.PlayerOrigin.y) > .5f
                || !rooms.TryLocate(player.transform.position, player.Agent.radius, out var currentRoom) || currentRoom != lease.RoomId
                || !rooms.TrySampleFloor(lease.Destination, npcInvitation.body.radius, filter, .05f, out var floor, out var room)
                || room != lease.RoomId || (floor - lease.Destination).sqrMagnitude > .0025f
                || !rooms.HasCapsuleClearance(floor, npcInvitation.body.radius, npcInvitation.body.height, visitor.transform)) return false;
            return (npcInvitation.actor.motion.ReservedDestination - lease.Destination).sqrMagnitude <= .0025f;
        }

        /// <summary>Only the physically arrived, close, visible speaker can display their invitation.</summary>
        public bool NpcInvitationCanSpeak(HouseNpcInvitationLease lease, out Vector3 worldAnchor)
        {
            worldAnchor = default;
            if (!NpcInvitationValid(lease) || !npcInvitation.actor.motion.HasArrivedAt(lease.Token)) return false;
            var player = npcInvitation.player;
            var visitor = npcInvitation.actor.npc;
            if ((visitor.transform.position - player.transform.position).sqrMagnitude > 2.8f * 2.8f
                || !rooms.HasClearSight(player.transform, visitor.transform)) return false;
            worldAnchor = visitor.transform.position + Vector3.up * 1.95f;
            return true;
        }

        public void ReleaseNpcInvitation(HouseNpcInvitationLease lease)
        {
            if (lease != null && npcInvitation != null && ReferenceEquals(npcInvitation.lease, lease)) EndNpcInvitation();
        }

        private bool NpcInvitationStageOwnsHouse => HasCompetitionStage || HasOpeningStage
            || HasCeremonyStage || HasSceneStage || departing != null;
        private bool NpcInvitationHoldsActor(string id) => npcInvitation != null && npcInvitation.actor.id == id;
        private bool NpcInvitationOwnsMotion(HouseNpcMotion motion) => npcInvitation != null
            && npcInvitation.actor.motion == motion && motion.LeaseId == npcInvitation.lease.Token;
        private void RetireInvalidNpcInvitation()
        {
            if (npcInvitation != null && !NpcInvitationValid(npcInvitation.lease)) EndNpcInvitation();
        }
        private void EndNpcInvitation()
        {
            if (npcInvitation == null) return;
            var entry = npcInvitation; npcInvitation = null; entry.lease.Released = true;
            var motion = entry.actor.motion;
            if (motion == null) return;
            if (!motion.Release(entry.lease.Token)) return;
            string id = entry.actor.id;
            motion.SetPaused(paused && !OpeningHoldsActor(id) && !CeremonyHoldsActor(id)
                && !DepartureHoldsActor(id) && !WanderHoldsActor(id) && !TalkHoldsActor(id)
                && !ActHoldsActor(id) && !StormHoldsActor(id));
        }
    }
}
