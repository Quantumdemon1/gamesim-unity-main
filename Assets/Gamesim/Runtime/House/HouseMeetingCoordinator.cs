using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Gamesim.House
{
    public enum HouseMeetingStatus { Travelling, Arrived, Paused, Released, Invalid }

    /// <summary>World-only lease. No topic, social effect, private reasoning or saved simulation reference.</summary>
    public sealed class HouseMeetingLease
    {
        public string Token { get; }
        public string Generation { get; }
        public string FirstId { get; }
        public string SecondId { get; }
        public string VenueId { get; }
        public string RoomId { get; }
        public Vector3 FirstSlot { get; }
        public Vector3 SecondSlot { get; }
        /// <summary>True at a venue whose slots are seats: the pair sits for the conversation.</summary>
        public bool Seated { get; }
        /// <summary>
        /// The heading (yaw, degrees) each actor settles on once arrived: into the seat at a seated
        /// venue, toward the other actor at a standing one. Presentation only; nothing here moves.
        /// </summary>
        public float FirstFacing { get; }
        public float SecondFacing { get; }
        public HouseMeetingStatus Status { get; internal set; }
        public string FailureReason { get; internal set; }
        internal HouseMeetingLease(string token, string generation, string first, string second,
            string venue, string room, Vector3 a, Vector3 b, bool seated, float firstFacing, float secondFacing)
        {
            Token = token; Generation = generation; FirstId = first; SecondId = second;
            VenueId = venue; RoomId = room; FirstSlot = a; SecondSlot = b;
            Seated = seated; FirstFacing = firstFacing; SecondFacing = secondFacing;
            Status = HouseMeetingStatus.Travelling;
        }
    }

    /// <summary>
    /// Explicit paired world rendezvous only. Caller owns eligibility, logical clock,
    /// timeout, source rules, persistence and observed text. Tick never starts a new pair.
    /// </summary>
    public sealed class HouseMeetingCoordinator : IDisposable
    {
        private sealed class Actor
        {
            public HouseNpc npc;
            public HouseNpcMotion motion;
            public string id;
            public int castIndex;
        }
        private sealed class Venue
        {
            public string id, room;
            public Vector3 first, second;
            public bool seated;
            public float firstYaw, secondYaw;
            public Venue(string key, string roomId, Vector3 a, Vector3 b, bool seats = false, float yawA = float.NaN, float yawB = float.NaN)
            { id = key; room = roomId; first = a; second = b; seated = seats; firstYaw = yawA; secondYaw = yawB; }
        }
        private static readonly Venue[] Venues =
        {
            new Venue("living-east-chat", "Living", new Vector3(-6.2f,0,-4), new Vector3(-4.8f,0,-4)),
            new Venue("kitchen-west-chat", "Kitchen", new Vector3(2.3f,0,-3), new Vector3(3.7f,0,-3)),
            // Clear both Riley's saved-scene spawn and the preserved U02 room
            // destination at (-7,0,2), including its 1.25 m keep-clear radius.
            new Venue("bedroom-south-chat", "Bedroom", new Vector3(-8.7f,0,3.5f), new Vector3(-7.3f,0,3.5f)),
            new Venue("yard-south-chat", "Yard", new Vector3(3.3f,0,13.5f), new Vector3(4.7f,0,13.5f)),
            // Seated venues. The slots are the floor positions of two authored seats - a pair of
            // chairs facing each other across the kitchen's long table, and two loungers at the
            // yard's east end - and the facing is the seat's own yaw, so a houseguest who arrives
            // sits in the chair rather than beside it. HouseSeatedVenueTests pins each slot to the
            // placed prop; the simulation's IsKnownRendezvous lists both ids. Appended, so the
            // authored order the tie rule relies on is unchanged for the four above.
            new Venue("kitchen-table-chat", "Kitchen", new Vector3(7.2f,0,-7.22f), new Vector3(7.2f,0,-8.78f), true, 180f, 0f),
            new Venue("yard-lounger-chat", "Yard", new Vector3(9.4f,0,11f), new Vector3(10.8f,0,11f), true, 0f, 0f)
        };

        /// <summary>One slot of a seated venue, for audits and tests.</summary>
        public readonly struct VenueSlot
        {
            public readonly string VenueId;
            public readonly Vector3 Position;
            public readonly float Yaw;
            public VenueSlot(string venueId, Vector3 position, float yaw) { VenueId = venueId; Position = position; Yaw = yaw; }
        }

        /// <summary>Every slot of every seated venue, in authored order.</summary>
        public static IEnumerable<VenueSlot> SeatedSlots
        {
            get
            {
                foreach (var venue in Venues)
                {
                    if (!venue.seated) continue;
                    yield return new VenueSlot(venue.id, venue.first, venue.firstYaw);
                    yield return new VenueSlot(venue.id, venue.second, venue.secondYaw);
                }
            }
        }

        public static bool IsSeatedVenue(string venueId)
        {
            foreach (var venue in Venues) if (venue.id == venueId) return venue.seated;
            return false;
        }
        private readonly HouseRoomQuery rooms;
        private readonly NavMeshQueryFilter filter;
        private readonly List<Actor> cast = new List<Actor>(5);
        private readonly Dictionary<string, Actor> actors = new Dictionary<string, Actor>(StringComparer.Ordinal);
        private readonly HashSet<string> eligible = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, HouseMeetingLease> leases = new Dictionary<string, HouseMeetingLease>(StringComparer.Ordinal);
        private readonly List<HouseMeetingLease> leaseBuffer = new List<HouseMeetingLease>(2);
        private readonly List<HouseRoomMarker> keepClear = new List<HouseRoomMarker>(5);
        private readonly NavMeshPath firstPath = new NavMeshPath(), secondPath = new NavMeshPath();
        private readonly Vector3[] cornerBuffer = new Vector3[64];
        private readonly Comparison<HouseMeetingLease> leaseComparison;
        private bool paused, disposed;
        public string Generation { get; private set; }
        public string LastFailure { get; private set; }
        public bool IsPaused => paused;
        public bool IsDisposed => disposed;
        public int LeaseCount => leases.Count;
        public bool IsReady
        {
            get
            {
                if (disposed || Generation == null) return false;
                foreach (var id in eligible)
                    if (!actors.TryGetValue(id, out var actor) || actor.motion == null || !actor.motion.IsBound
                        || actor.npc == null || actor.npc.Id != id || !actor.npc.gameObject.activeInHierarchy
                        || actor.npc.gameObject.scene != rooms.Scene) return false;
                return true;
            }
        }

        private HouseMeetingCoordinator(HouseRoomQuery query, NavMeshQueryFilter navFilter)
        { rooms = query; filter = navFilter; leaseComparison = CompareLeases; }

        public static bool TryCreate(HouseRoomQuery query, NavMeshQueryFilter navFilter,
            out HouseMeetingCoordinator coordinator, out string reason)
        {
            coordinator = null; reason = null;
            if (!Application.isPlaying || query == null || navFilter.agentTypeID < 0 || navFilter.areaMask == 0
                || NavMesh.GetSettingsByID(navFilter.agentTypeID).agentTypeID == -1)
            { reason = "A world coordinator requires Play Mode and a compatible bound house query/filter."; return false; }
            if (!query.TryValidateScene(out reason)) return false;
            var candidate = new HouseMeetingCoordinator(query, navFilter);
            foreach (var root in query.Scene.GetRootGameObjects())
                candidate.keepClear.AddRange(root.GetComponentsInChildren<HouseRoomMarker>(true));
            var roomNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var marker in candidate.keepClear)
                if (!roomNames.Add(marker.RoomName)) { reason = "Room destinations must have unique IDs."; return false; }
            foreach (string expected in new[] { "Living", "Kitchen", "Bedroom", "Private", "Yard" })
                if (!roomNames.Contains(expected)) { reason = "A protected room destination is missing."; return false; }
            coordinator = candidate;
            return true;
        }

        public bool Reconcile(string generation, IReadOnlyList<HouseNpc> castSlots, IReadOnlyList<string> castIds,
            IEnumerable<string> eligibleIds, out string reason)
        {
            reason = null;
            if (disposed || string.IsNullOrWhiteSpace(generation) || castSlots == null || castIds == null || eligibleIds == null
                // The bound keeps the binding finite; it is not a statement that a house holds
                // six. Five was the scene's authored NPC count, and a season can now cast more.
                || castSlots.Count != castIds.Count || castSlots.Count > Gamesim.Simulation.EpisodeValidation.MaximumCast - 1)
                return Fail(out reason, "Invalid world generation or bounded cast binding.");
            var nextIds = new HashSet<string>(StringComparer.Ordinal);
            var nextRoots = new HashSet<HouseNpc>();
            for (int i = 0; i < castSlots.Count; i++)
            {
                var npc = castSlots[i]; string id = castIds[i];
                if (npc == null || npc.gameObject.scene != rooms.Scene || string.IsNullOrWhiteSpace(id)
                    || id.Length > 128 || npc.Id != id || !nextIds.Add(id) || !nextRoots.Add(npc))
                    return Fail(out reason, "Cast IDs must match distinct local NPC roots exactly.");
                var existingMotion = npc.GetComponent<HouseNpcMotion>();
                if (existingMotion != null && (!existingMotion.CanClaim(this)
                    || existingMotion.LeaseId != null && !OwnsMotionLease(existingMotion)))
                    return Fail(out reason, "A different coordinator or direct reservation already owns this NPC.");
            }
            var nextEligible = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in eligibleIds)
                if (!nextIds.Contains(id) || !nextEligible.Add(id))
                    return Fail(out reason, "Eligibility contains an unknown or repeated NPC ID.");
            for (int i = 0; i < castSlots.Count; i++)
                if (nextEligible.Contains(castIds[i]) && !castSlots[i].gameObject.activeInHierarchy)
                    return Fail(out reason, "An eligible NPC root is inactive.");
            if (!rooms.TryValidateScene(out reason)) return false;

            bool remap = Generation != generation || cast.Count != castSlots.Count;
            for (int i = 0; i < cast.Count && i < castSlots.Count; i++)
                remap |= cast[i].npc != castSlots[i] || cast[i].id != castIds[i];
            if (remap)
            {
                ReleaseAll();
                foreach (var previous in cast)
                    if (previous.motion != null) { previous.motion.Unbind(); previous.motion.ReleaseClaim(this); }
                cast.Clear(); actors.Clear(); Generation = generation;
                for (int i = 0; i < castSlots.Count; i++)
                {
                    var entry = new Actor { npc = castSlots[i], id = castIds[i], castIndex = i,
                        motion = castSlots[i].GetComponent<HouseNpcMotion>() };
                    cast.Add(entry); actors.Add(entry.id, entry);
                }
            }
            eligible.Clear(); eligible.UnionWith(nextEligible);
            // Release invalid pairs before changing any participant's collision owner.
            leaseBuffer.Clear(); leaseBuffer.AddRange(leases.Values);
            foreach (var lease in leaseBuffer)
                if (!eligible.Contains(lease.FirstId) || !eligible.Contains(lease.SecondId))
                    Retire(lease, HouseMeetingStatus.Invalid, "A participant is no longer eligible.");
            foreach (var actor in cast)
            {
                if (actor.motion != null && !actor.motion.TryClaim(this))
                    return Fail(out reason, "The NPC acquired a different coordinator owner.");
                if (!eligible.Contains(actor.id)) { if (actor.motion != null) actor.motion.Unbind(); continue; }
                if (actor.motion == null)
                {
                    if (!HouseNpcMotion.TryCreate(actor.npc, rooms, filter, actor.castIndex, out actor.motion, out reason))
                    {
                        if (actor.motion != null) actor.motion.TryClaim(this);
                        return Fail(out reason, reason ?? "The NPC could not acquire movement ownership.");
                    }
                    actor.motion.TryClaim(this);
                }
                else if (actor.motion.BoundNpcId != actor.id)
                {
                    actor.motion.Unbind();
                    if (!actor.motion.RebindIdentity(out reason, actor.castIndex)) return Fail(out reason, reason);
                }
                else if (actor.motion.State == HouseNpcMotionState.Unbound)
                {
                    if (!actor.motion.BeginBinding(out reason)) return Fail(out reason, reason);
                }
                else if (actor.motion.State == HouseNpcMotionState.Failed)
                    return Fail(out reason, actor.motion.FailureReason ?? "NPC binding failed; explicit recovery is required.");
                actor.motion.SetPaused(paused);
            }
            LastFailure = null;
            return true;
        }

        public bool TryGetMotion(string id, out HouseNpcMotion motion)
        {
            motion = null;
            if (disposed || id == null || !actors.TryGetValue(id, out var actor)) return false;
            motion = actor.motion;
            return motion != null;
        }
        public bool TryGetLease(string token, out HouseMeetingLease lease)
        { lease = null; return !disposed && token != null && leases.TryGetValue(token, out lease); }

        public bool TryReservePair(string token, string firstId, string secondId, out HouseMeetingLease lease, out string reason)
            => Reserve(token, firstId, secondId, null, out lease, out reason);
        public bool TryReserveAtVenue(string token, string firstId, string secondId, string venueId,
            out HouseMeetingLease lease, out string reason)
        {
            if (string.IsNullOrEmpty(venueId)) { lease = null; return Fail(out reason, "A saved venue ID is required for reunion."); }
            return Reserve(token, firstId, secondId, venueId, out lease, out reason);
        }

        private bool Reserve(string token, string firstId, string secondId, string requiredVenue,
            out HouseMeetingLease lease, out string reason)
        {
            lease = null; reason = null;
            if (disposed || paused || !IsReady || string.IsNullOrWhiteSpace(token) || token.Length > 128
                || firstId == null || secondId == null || firstId == secondId || !eligible.Contains(firstId) || !eligible.Contains(secondId))
                return Fail(out reason, "A pair requires two distinct eligible bound NPCs in an unpaused world.");
            if (leases.TryGetValue(token, out var existing))
            {
                if (existing.FirstId != firstId || existing.SecondId != secondId
                    || requiredVenue != null && existing.VenueId != requiredVenue)
                    return Fail(out reason, "The token already identifies a different pair or venue.");
                lease = existing; return true;
            }
            var first = actors[firstId]; var second = actors[secondId];
            if (leases.Count >= 2 || first.motion.LeaseId != null || second.motion.LeaseId != null)
                return Fail(out reason, "An actor is already reserved or both pair slots are occupied.");
            Venue best = null; float bestLength = float.PositiveInfinity;
            Vector3 bestFirst = default, bestSecond = default;
            foreach (var venue in Venues)
            {
                if (requiredVenue != null && venue.id != requiredVenue || VenueInUse(venue.id)) continue;
                if (!MeasureVenue(venue, first, second, out var a, out var b, out var length)) continue;
                if (length >= bestLength - .0001f) continue; // Stable authored order on ties.
                best = venue; bestFirst = a; bestSecond = b; bestLength = length;
            }
            if (best == null) return Fail(out reason, "No unoccupied protected-room venue has two complete safe paths.");
            // Revalidate both before reserving either. Motion then independently checks
            // its own SetPath; a second failure rolls back the first in the same call.
            if (!MeasureVenue(best, first, second, out bestFirst, out bestSecond, out _))
                return Fail(out reason, "The selected paired paths changed before reservation.");
            if (!first.motion.TryReserveAndPath(token, bestFirst)) return Fail(out reason, "The first NPC could not reserve its route.");
            bool secondReserved = false;
            try { secondReserved = second.motion.TryReserveAndPath(token, bestSecond); }
            finally { if (!secondReserved) first.motion.Release(token); }
            if (!secondReserved) return Fail(out reason, "The second route failed; the first reservation was released.");
            // The final native path owners may sample a few millimetres differently
            // than the preliminary pair query. Expose their accepted endpoints.
            var firstAt = first.motion.ReservedDestination; var secondAt = second.motion.ReservedDestination;
            lease = new HouseMeetingLease(token, Generation, firstId, secondId, best.id, best.room, firstAt, secondAt, best.seated,
                best.seated ? best.firstYaw : YawToward(firstAt, secondAt), best.seated ? best.secondYaw : YawToward(secondAt, firstAt));
            leases.Add(token, lease); LastFailure = null;
            return true;
        }

        private bool MeasureVenue(Venue venue, Actor first, Actor second, out Vector3 a, out Vector3 b, out float length)
        {
            a = default; b = default; length = 0;
            var firstBody = first.npc.GetComponent<CapsuleCollider>(); var secondBody = second.npc.GetComponent<CapsuleCollider>();
            if (firstBody == null || secondBody == null
                || !rooms.TrySampleFloor(venue.first, firstBody.radius, filter, .25f, out a, out var firstRoom)
                || !rooms.TrySampleFloor(venue.second, secondBody.radius, filter, .25f, out b, out var secondRoom)
                || firstRoom != venue.room || secondRoom != venue.room || !ClearsDestinations(a) || !ClearsDestinations(b)
                || HorizontalSquared(a,b) < Mathf.Pow(firstBody.radius + secondBody.radius + .20f,2)
                || HorizontalSquared(a,b) >= 16
                || !rooms.HasCapsuleClearance(a,firstBody.radius,firstBody.height,first.npc.transform)
                || !rooms.HasCapsuleClearance(b,secondBody.radius,secondBody.height,second.npc.transform)) return false;
            if (!MeasurePath(first.motion.Agent, a, firstPath, out var firstLength)
                || !MeasurePath(second.motion.Agent, b, secondPath, out var secondLength)) return false;
            length = firstLength + secondLength;
            return HouseRoomQuery.Finite(length);
        }

        private bool MeasurePath(NavMeshAgent agent, Vector3 endpoint, NavMeshPath path, out float length)
        {
            length = 0;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh || !agent.CalculatePath(endpoint,path)
                || path.status != NavMeshPathStatus.PathComplete) return false;
            int count = path.GetCornersNonAlloc(cornerBuffer);
            if (count == 0 || count == cornerBuffer.Length || Vector3.Distance(cornerBuffer[count-1],endpoint) > .05f) return false;
            for (int i = 0; i < count; i++)
            {
                if (!HouseRoomQuery.Finite(cornerBuffer[i])) return false;
                length += Vector3.Distance(i == 0 ? agent.transform.position : cornerBuffer[i-1],cornerBuffer[i]);
            }
            return HouseRoomQuery.Finite(length);
        }

        public bool ValidateArrivedPair(HouseMeetingLease lease, out string reason)
        {
            reason = null;
            if (disposed || paused || !Current(lease) || !ValidActors(lease, out var first, out var second))
                return Fail(out reason, "The pair is stale, paused, inactive or no longer owns both actors.");
            if (!first.motion.HasArrivedAt(lease.Token) || !second.motion.HasArrivedAt(lease.Token))
                return Fail(out reason, "Both actors have not physically arrived at their reserved slots.");
            var a = first.npc.transform; var b = second.npc.transform;
            var bodyA = a.GetComponent<CapsuleCollider>(); var bodyB = b.GetComponent<CapsuleCollider>();
            if (bodyA == null || bodyB == null || !bodyA.enabled || !bodyB.enabled || bodyA.isTrigger || bodyB.isTrigger
                || !rooms.TryLocate(a.position,bodyA.radius,out var roomA) || !rooms.TryLocate(b.position,bodyB.radius,out var roomB)
                || roomA != lease.RoomId || roomB != lease.RoomId || HorizontalSquared(a.position,b.position) >= 16
                || !rooms.HasClearSight(a,b)
                || !rooms.HasCapsuleClearance(a.position,bodyA.radius,bodyA.height,a)
                || !rooms.HasCapsuleClearance(b.position,bodyB.radius,bodyB.height,b))
                return Fail(out reason, "The paired room, distance, sight or capsule proof failed.");
            LastFailure = null;
            return true;
        }

        public bool CanWitness(HousePlayerController player, HouseMeetingLease lease)
        {
            if (player == null || !player.gameObject.activeInHierarchy || player.gameObject.scene != rooms.Scene
                || player.Agent == null || !ValidateArrivedPair(lease,out _)) return false;
            var a = actors[lease.FirstId].npc.transform; var b = actors[lease.SecondId].npc.transform;
            var midpoint = (a.position + b.position) * .5f;
            if (HorizontalSquared(player.transform.position,midpoint) >= 36
                || !rooms.TryLocate(player.transform.position,player.Agent.radius,out var room) || room != lease.RoomId) return false;
            return rooms.HasClearSight(player.transform,a) && rooms.HasClearSight(player.transform,b);
        }

        public void Tick()
        {
            if (disposed) return;
            leaseBuffer.Clear(); leaseBuffer.AddRange(leases.Values); leaseBuffer.Sort(leaseComparison);
            foreach (var lease in leaseBuffer)
            {
                if (!ValidActors(lease,out _,out _)) { Retire(lease,HouseMeetingStatus.Invalid,"An actor or binding became invalid."); continue; }
                lease.Status = paused ? HouseMeetingStatus.Paused
                    : ValidateArrivedPair(lease,out _) ? HouseMeetingStatus.Arrived : HouseMeetingStatus.Travelling;
            }
        }

        public void SetPaused(bool value)
        {
            if (disposed || paused == value) return;
            paused = value;
            foreach (var actor in cast) if (actor.motion != null) actor.motion.SetPaused(value);
            foreach (var lease in leases.Values) lease.Status = value ? HouseMeetingStatus.Paused : HouseMeetingStatus.Travelling;
        }
        public bool Release(HouseMeetingLease lease) => Current(lease) && Retire(lease,HouseMeetingStatus.Released,null);
        public void ReleaseAll()
        {
            leaseBuffer.Clear(); leaseBuffer.AddRange(leases.Values);
            foreach (var lease in leaseBuffer) Retire(lease,HouseMeetingStatus.Released,null);
        }
        public void UnbindAll()
        { ReleaseAll(); foreach (var actor in cast) if (actor.motion != null) actor.motion.Unbind(); }
        public void Dispose()
        {
            if (disposed) return;
            UnbindAll();
            foreach (var actor in cast) if (actor.motion != null) actor.motion.ReleaseClaim(this);
            disposed = true; cast.Clear(); actors.Clear(); eligible.Clear();
        }

        private bool Current(HouseMeetingLease lease) => !disposed && lease != null && lease.Generation == Generation
            && leases.TryGetValue(lease.Token,out var current) && ReferenceEquals(current,lease);
        private bool ValidActors(HouseMeetingLease lease, out Actor first, out Actor second)
        {
            first = null; second = null;
            return Current(lease) && actors.TryGetValue(lease.FirstId,out first) && actors.TryGetValue(lease.SecondId,out second)
                && eligible.Contains(first.id) && eligible.Contains(second.id) && ValidActor(first,lease.Token) && ValidActor(second,lease.Token);
        }
        private bool ValidActor(Actor actor, string token) => actor.npc != null && actor.npc.gameObject.activeInHierarchy
            && actor.npc.gameObject.scene == rooms.Scene && actor.npc.Id == actor.id && actor.motion != null && actor.motion.IsBound
            && actor.motion.BoundNpcId == actor.id && actor.motion.LeaseId == token;
        private bool OwnsMotionLease(HouseNpcMotion motion)
        {
            if (motion.LeaseId == null || !leases.TryGetValue(motion.LeaseId, out var lease)) return false;
            return actors.TryGetValue(lease.FirstId, out var first) && first.motion == motion
                || actors.TryGetValue(lease.SecondId, out var second) && second.motion == motion;
        }
        private bool Retire(HouseMeetingLease lease, HouseMeetingStatus status, string reason)
        {
            if (!Current(lease)) return false;
            if (actors.TryGetValue(lease.FirstId,out var first) && first.motion != null) first.motion.Release(lease.Token);
            if (actors.TryGetValue(lease.SecondId,out var second) && second.motion != null) second.motion.Release(lease.Token);
            leases.Remove(lease.Token); lease.Status = status; lease.FailureReason = reason;
            return true;
        }
        private bool VenueInUse(string venue) { foreach (var lease in leases.Values) if (lease.VenueId == venue) return true; return false; }
        private bool ClearsDestinations(Vector3 point)
        {
            foreach (var marker in keepClear)
                if (marker == null || !marker.gameObject.activeInHierarchy || marker.gameObject.scene != rooms.Scene
                    || HorizontalSquared(point,marker.transform.position) < 1.25f * 1.25f) return false;
            return true;
        }
        private int CompareLeases(HouseMeetingLease a, HouseMeetingLease b)
        {
            int first = actors[a.FirstId].castIndex.CompareTo(actors[b.FirstId].castIndex);
            return first != 0 ? first : string.Compare(a.Token,b.Token,StringComparison.Ordinal);
        }
        private static float HorizontalSquared(Vector3 a, Vector3 b) { var delta = a-b; return delta.x*delta.x + delta.z*delta.z; }
        private static float YawToward(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
        private bool Fail(out string reason, string message) { reason = message; LastFailure = message; return false; }
    }
}
