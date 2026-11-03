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

    /// <summary>
    /// Why <c>HouseMeetingCoordinator.TryReserveNpcInvitation</c> refused a visit: the asker's own reasons (busy, no
    /// route of their own, not bound), or the house's, the player's or the player's spot, which every asker would meet
    /// the same way. Presentation only; never saved.
    /// </summary>
    public enum HouseNpcInvitationRefusal
    {
        /// <summary>Not refused.</summary>
        None,
        /// <summary>The house is not free for a visit: a stage, a conversation or another visit holds it.</summary>
        House,
        /// <summary>The player is not bound to the house's floor, or is busy at a piece of furniture.</summary>
        Player,
        /// <summary>The houseguest is not bound to this house, or has no body to stand with.</summary>
        Asker,
        /// <summary>The houseguest is doing something else: seated, in a conversation, staged.</summary>
        Busy,
        /// <summary>No point beside the player's spot clears the floor, the room, the spacing and the clearance tests.</summary>
        PlayerSpot,
        /// <summary>Points beside the player clear, but the houseguest has no complete route to any of them.</summary>
        NoRoute,
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
            // A visit that answers from arrival: its window starts again, once, when the houseguest gets there.
            public bool answerFromArrival, arrived;
        }

        private const float NpcInvitationDistance = 1.8f;
        private const float NpcInvitationPlayerDrift = .75f;
        /// <summary>
        /// How long a visit lasts, in unscaled seconds: from its dispatch, or - for one that answers from arrival - from
        /// the moment the houseguest gets there.
        /// </summary>
        public const float NpcInvitationLifetime = 25f;
        /// <summary>
        /// A visit that answers from arrival walks on a budget of its own route, in unscaled seconds: the route at
        /// <see cref="HouseNpcMotion.DefaultSpeed"/> times <see cref="NpcInvitationTravelFactor"/>, plus
        /// <see cref="NpcInvitationTravelSlack"/>, kept between <see cref="NpcInvitationTravelMin"/> and
        /// <see cref="NpcInvitationTravelMax"/>. A walk across the house no longer spends the time the player has to answer.
        /// </summary>
        public const float NpcInvitationTravelFactor = 1.5f, NpcInvitationTravelSlack = 3f,
            NpcInvitationTravelMin = 6f, NpcInvitationTravelMax = 30f;
        private NpcInvitationEntry npcInvitation;

        /// <summary>
        /// The travel budget of a visit that answers from arrival, for a route of <paramref name="routeMetres"/>:
        /// clamp(route / <see cref="HouseNpcMotion.DefaultSpeed"/> x <see cref="NpcInvitationTravelFactor"/> +
        /// <see cref="NpcInvitationTravelSlack"/>, <see cref="NpcInvitationTravelMin"/>, <see cref="NpcInvitationTravelMax"/>).
        /// A route that is not a finite length gets the most. Pure, so EditMode can drive it.
        /// </summary>
        public static float NpcInvitationTravelBudget(float routeMetres)
        {
            if (float.IsNaN(routeMetres) || float.IsInfinity(routeMetres)) return NpcInvitationTravelMax;
            return Mathf.Clamp(routeMetres / HouseNpcMotion.DefaultSpeed * NpcInvitationTravelFactor + NpcInvitationTravelSlack,
                NpcInvitationTravelMin, NpcInvitationTravelMax);
        }

        /// <summary>
        /// Reserve a complete, clear route beside the player. Existing conversations, activities and
        /// staged acts are never displaced. A stroll may yield after a route is proved. The player
        /// keeps control and may walk away, which invalidates this bounded visit rather than dragging
        /// them back or chasing indefinitely.
        /// </summary>
        public bool TryReserveNpcInvitation(string npcId, HousePlayerController player,
            out HouseNpcInvitationLease lease, out string reason) =>
            TryReserveNpcInvitation(npcId, player, false, out lease, out reason, out _);

        /// <summary>
        /// The same visit, saying why a refusal was made (<paramref name="refusal"/>): the asker's own reasons (busy,
        /// no route, not bound) are charged to that asker's ask; the house's, the player's and the player's spot are
        /// every asker's. The same tests in the same order, the same reasons and the same lease.
        /// </summary>
        public bool TryReserveNpcInvitation(string npcId, HousePlayerController player,
            out HouseNpcInvitationLease lease, out string reason, out HouseNpcInvitationRefusal refusal) =>
            TryReserveNpcInvitation(npcId, player, false, out lease, out reason, out refusal);

        /// <summary>
        /// The same visit; with <paramref name="answerFromArrival"/>, it walks on a budget of its own route
        /// (<see cref="NpcInvitationTravelBudget"/>) and its <see cref="NpcInvitationLifetime"/> starts again from the
        /// moment the houseguest has arrived, so the player has the whole window to answer however far they came. False is
        /// the visit as it always was: <see cref="NpcInvitationLifetime"/> from dispatch. Either way the lease never
        /// follows the player: walking away still ends it.
        /// </summary>
        public bool TryReserveNpcInvitation(string npcId, HousePlayerController player, bool answerFromArrival,
            out HouseNpcInvitationLease lease, out string reason, out HouseNpcInvitationRefusal refusal)
        {
            lease = null;
            refusal = HouseNpcInvitationRefusal.House;
            if (disposed || !IsReady || NpcInvitationStageOwnsHouse || talk != null || npcInvitation != null)
                return Fail(out reason, "The house is not free for a visit.");
            refusal = HouseNpcInvitationRefusal.Player;
            if (player == null || !player.isActiveAndEnabled || player.gameObject.scene != rooms.Scene
                || player.HasActivityOwner || player.Agent == null || !player.Agent.enabled || !player.Agent.isOnNavMesh)
                return Fail(out reason, "The player and houseguest must be bound to this house.");
            refusal = HouseNpcInvitationRefusal.Asker;
            if (npcId == null || !eligible.Contains(npcId) || !actors.TryGetValue(npcId, out var actor)
                || actor.npc == null || actor.motion == null || !actor.motion.IsBound)
                return Fail(out reason, "The player and houseguest must be bound to this house.");
            refusal = HouseNpcInvitationRefusal.Busy;
            if (actor.motion.LeaseId != null && !WanderOwnsMotion(actor.motion))
                return Fail(out reason, "The houseguest is busy.");
            var body = actor.npc.GetComponent<CapsuleCollider>();
            refusal = HouseNpcInvitationRefusal.Asker;
            if (body == null) return Fail(out reason, "The visit requires a clear house floor and a bound body.");
            refusal = HouseNpcInvitationRefusal.PlayerSpot;
            if (!rooms.TryLocate(player.transform.position, player.Agent.radius, out var playerRoom))
                return Fail(out reason, "The visit requires a clear house floor and a bound body.");

            Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f) forward = Vector3.forward;
            Vector3 best = default; float shortest = float.PositiveInfinity; bool found = false, cleared = false;
            // Fixed angular order keeps this presentation deterministic and allocates no candidate list.
            for (int index = 0; index < 12; index++)
            {
                Vector3 requested = player.transform.position
                    + Quaternion.Euler(0f, index * 30f, 0f) * forward * NpcInvitationDistance;
                if (!rooms.TrySampleFloor(requested, body.radius, filter, .25f, out var point, out var room)
                    || room != playerRoom || !ClearsDestinations(point)
                    || HorizontalSquared(point, player.transform.position) < Mathf.Pow(body.radius + player.Agent.radius + .2f, 2)
                    || !rooms.HasCapsuleClearance(point, body.radius, body.height, actor.npc.transform)) continue;
                // A point beside the player stands clear; whether the houseguest can reach it is theirs.
                cleared = true;
                if (!MeasurePath(actor.motion.Agent, point, firstPath, out float length)) continue;
                if (length >= shortest - .0001f) continue;
                best = point; shortest = length; found = true;
            }
            refusal = cleared ? HouseNpcInvitationRefusal.NoRoute : HouseNpcInvitationRefusal.PlayerSpot;
            if (!found) return Fail(out reason, "No clear complete route reaches the player.");

            // Direct motion reservations belong to this coordinator and are recognized by Reconcile.
            YieldWander(npcId);
            refusal = HouseNpcInvitationRefusal.Busy;
            if (actor.motion.LeaseId != null) return Fail(out reason, "The houseguest acquired another activity.");
            actor.motion.SetPaused(false);
            string token = "invitation:" + Guid.NewGuid().ToString("N");
            if (!actor.motion.TryReserveAndPath(token, best))
            {
                actor.motion.SetPaused(paused);
                refusal = HouseNpcInvitationRefusal.NoRoute;
                return Fail(out reason, "The houseguest cannot walk to the player.");
            }
            lease = new HouseNpcInvitationLease(npcId, Generation, token, best, playerRoom, player.transform.position);
            npcInvitation = new NpcInvitationEntry { lease = lease, actor = actor, player = player,
                body = body, answerFromArrival = answerFromArrival,
                expiresAt = Time.unscaledTime + (answerFromArrival ? NpcInvitationTravelBudget(shortest) : NpcInvitationLifetime) };
            LastFailure = null; reason = null;
            refusal = HouseNpcInvitationRefusal.None;
            return true;
        }

        public bool NpcInvitationValid(HouseNpcInvitationLease lease)
        {
            if (disposed || lease == null || lease.Released || npcInvitation == null
                || !ReferenceEquals(npcInvitation.lease, lease) || lease.Generation != Generation
                || NpcInvitationStageOwnsHouse) return false;
            NoteNpcInvitationArrival(npcInvitation);
            if (Time.unscaledTime >= npcInvitation.expiresAt
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

        /// <summary>
        /// When this visit lapses on the unscaled clock, unless the player walks away or the house takes it first: its
        /// travel budget until the houseguest arrives, for a visit that answers from arrival, and its lifetime from then.
        /// NaN for a lease that is not the visit in flight. A read for tests.
        /// </summary>
        public float NpcInvitationExpiresAt(HouseNpcInvitationLease lease) =>
            lease != null && npcInvitation != null && ReferenceEquals(npcInvitation.lease, lease) ? npcInvitation.expiresAt : float.NaN;

        /// <summary>
        /// A visit that answers from arrival, its houseguest there for the first time: its lifetime starts now. Once a
        /// visit; a visit that does not answer from arrival keeps the lifetime it was dispatched with.
        /// </summary>
        private static void NoteNpcInvitationArrival(NpcInvitationEntry entry)
        {
            if (entry == null || !entry.answerFromArrival || entry.arrived) return;
            var motion = entry.actor != null ? entry.actor.motion : null;
            if (motion == null || !motion.HasArrivedAt(entry.lease.Token)) return;
            entry.arrived = true;
            entry.expiresAt = Time.unscaledTime + NpcInvitationLifetime;
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
