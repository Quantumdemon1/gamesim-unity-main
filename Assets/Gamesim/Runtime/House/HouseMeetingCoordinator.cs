using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

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
        /// <summary>The saved rendezvous: one of the six venues a save can name, whichever of its places the pair was sent to.</summary>
        public string VenueId { get; }
        /// <summary>
        /// The place itself: the venue's own pair, or one of its talk spots (HouseConversationSpots).
        /// Runtime only, never saved: a load sends the pair to a free place of the same venue.
        /// </summary>
        public string SpotId { get; }
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
        internal Vector3 FirstAnchorPosition { get; }
        internal Vector3 SecondAnchorPosition { get; }
        internal Vector3 FirstAnchorApproach { get; }
        internal Vector3 SecondAnchorApproach { get; }
        internal HouseMeetingLease(string token, string generation, string first, string second,
            string venue, string spot, string room, Vector3 a, Vector3 b, bool seated, float firstFacing, float secondFacing,
            Vector3 anchorA, Vector3 anchorB,Vector3 approachA,Vector3 approachB)
        {
            Token = token; Generation = generation; FirstId = first; SecondId = second;
            VenueId = venue; SpotId = spot; RoomId = room; FirstSlot = a; SecondSlot = b;
            Seated = seated; FirstFacing = firstFacing; SecondFacing = secondFacing;
            FirstAnchorPosition=anchorA; SecondAnchorPosition=anchorB;
            FirstAnchorApproach=approachA;SecondAnchorApproach=approachB;
            Status = HouseMeetingStatus.Travelling;
        }
    }

    /// <summary>
    /// Explicit paired world rendezvous only. Caller owns eligibility, logical clock,
    /// timeout, source rules, persistence and observed text. Tick never starts a new pair.
    /// </summary>
    public sealed partial class HouseMeetingCoordinator : IDisposable
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
            /// <summary>The anchors' own venue id; <see cref="family"/> is the saved one, the same for a venue's own pair.</summary>
            public readonly string id, room, family;
            public readonly bool seated;
            public readonly HouseInteractionAnchor a,b;
            public Vector3 first => a.Position;
            public Vector3 second => b.Position;
            public Vector3 firstApproach => a.Approach;
            public Vector3 secondApproach => b.Approach;
            public float firstYaw => a.Facing;
            public float secondYaw => b.Facing;
            /// <summary>Whether this is the venue's own authored pair rather than one of its talk spots.</summary>
            public bool Home => id == family;
            public Venue(HouseInteractionAnchor first,HouseInteractionAnchor second,string familyId)
            { a=first; b=second; id=a.VenueId; room=a.RoomId; seated=a.Seated; family=familyId; }
            public bool Valid(Scene scene) => a!=null && b!=null && a.isActiveAndEnabled && b.isActiveAndEnabled
                && a.gameObject.scene==scene && b.gameObject.scene==scene && a.VenueId==id && b.VenueId==id
                && a.RoomId==room && b.RoomId==room && a.Seated==seated && b.Seated==seated;
        }
        private readonly List<Venue> venues = new List<Venue>();

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
                foreach (var venue in HouseInteractionAnchors.Meetings)
                {
                    if (!venue.Seated) continue;
                    yield return new VenueSlot(venue.Id, venue.First, venue.FirstYaw);
                    yield return new VenueSlot(venue.Id, venue.Second, venue.SecondYaw);
                }
            }
        }

        public static bool IsSeatedVenue(string venueId)
        {
            foreach (var venue in HouseInteractionAnchors.Meetings) if (venue.Id == venueId) return venue.Seated;
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
            HouseInteractionAnchors.EnsureDefaults(query.Scene);
            var candidate = new HouseMeetingCoordinator(query, navFilter);
            foreach (var definition in HouseInteractionAnchors.Meetings)
            {
                if (!HouseInteractionAnchors.TryFind(query.Scene,definition.Id,0,out var a)
                    || !HouseInteractionAnchors.TryFind(query.Scene,definition.Id,1,out var b)
                    || a.RoomId!=definition.Room || b.RoomId!=definition.Room || a.Seated!=definition.Seated || b.Seated!=definition.Seated)
                {
                    // Unfurnished fixtures or a deliberately removed seat do not disable every
                    // standing venue. A saved meeting naming the missing seat remains pending.
                    if(definition.Seated)continue;
                    reason="Each standing venue requires two unique compatible scene-local interaction anchors."; return false;
                }
                candidate.venues.Add(new Venue(a,b,definition.Id));
            }
            // The talk spots (PACK8-PASS-PLAN C3): more places for the same six venues, on the
            // pieces the house has. A house without them has the six and nothing more.
            HouseConversationSpots.Ensure(query.Scene);
            foreach (var spot in HouseConversationSpots.InScene(query.Scene))
                candidate.venues.Add(new Venue(spot.First,spot.Second,spot.Family));
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
            // Somebody walking out who is no longer the house's to route is let go before unbinding.
            if (departing != null && !eligible.Contains(departing.id)) EndDeparture();
            RetireInvalidActivities();
            RetireInvalidTalk();
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
                        if (DropFailedCandidate(actor)) { reason = null; continue; }
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
                    if (!actor.motion.BeginBinding(out reason))
                    {
                        if (DropFailedCandidate(actor)) { reason = null; continue; }
                        return Fail(out reason, reason);
                    }
                }
                else if (actor.motion.State == HouseNpcMotionState.Failed)
                {
                    if (DropFailedCandidate(actor)) continue;
                    return Fail(out reason, actor.motion.FailureReason ?? "NPC binding failed; explicit recovery is required.");
                }
                // The opening's cast walks while the house is paused around it, and so does a ceremony's,
                // and so does somebody the player has asked over to talk.
                actor.motion.SetPaused(paused && !OpeningHoldsActor(actor.id) && !CeremonyHoldsActor(actor.id)
                    && !DepartureHoldsActor(actor.id) && !WanderHoldsActor(actor.id) && !TalkHoldsActor(actor.id) && !ActHoldsActor(actor.id));
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
            // Full before anybody is disturbed: a pair that cannot be reserved used to take both of
            // them off their furniture first, and a friend in the hot tub was pulled out and put
            // back every few seconds by pairings that never happened.
            if (leases.Count >= 2) return Fail(out reason, "An actor is already reserved or both pair slots are occupied.");
            YieldActivity(firstId);YieldActivity(secondId);YieldWander(firstId);YieldWander(secondId);YieldAct(firstId);YieldAct(secondId);
            var first = actors[firstId]; var second = actors[secondId];
            if (first.motion.LeaseId != null || second.motion.LeaseId != null)
                return Fail(out reason, "An actor is already reserved or both pair slots are occupied.");
            Venue best = null; float bestScore = float.PositiveInfinity;
            Vector3 bestFirst = default, bestSecond = default;
            foreach (var venue in venues)
            {
                if (!venue.Valid(rooms.Scene) || requiredVenue != null && venue.family != requiredVenue || MeetingVenueTaken(venue)) continue;
                if (!MeasureVenue(venue, first, second, out var a, out var b, out var length)) continue;
                // A saved conversation goes back to its venue's own pair first, as it always did -
                // which of the venue's places it was held at is not saved - and to another of the
                // venue's spots only while that pair is taken. A new one takes the shortest walk,
                // a walk to two seats counted as three metres shorter: people sit to talk when they
                // can (PACK8-PASS-PLAN C3).
                float score = requiredVenue != null && venue.Home ? float.NegativeInfinity : SpotScore(length, venue.seated);
                if (score >= bestScore - .0001f) continue; // Stable authored order on ties.
                best = venue; bestFirst = a; bestSecond = b; bestScore = score;
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
            lease = new HouseMeetingLease(token, Generation, firstId, secondId, best.family, best.id, best.room, firstAt, secondAt, best.seated,
                best.seated ? best.firstYaw : YawToward(firstAt, secondAt), best.seated ? best.secondYaw : YawToward(secondAt, firstAt),
                best.first,best.second,best.firstApproach,best.secondApproach);
            leases.Add(token, lease); LastFailure = null;
            return true;
        }

        private bool MeasureVenue(Venue venue, Actor first, Actor second, out Vector3 a, out Vector3 b, out float length)
        {
            a = default; b = default; length = 0;
            var firstBody = first.npc.GetComponent<CapsuleCollider>(); var secondBody = second.npc.GetComponent<CapsuleCollider>();
            if (firstBody == null || secondBody == null
                || !rooms.TrySampleFloor(venue.firstApproach, firstBody.radius, filter, .25f, out a, out var firstRoom)
                || !rooms.TrySampleFloor(venue.secondApproach, secondBody.radius, filter, .25f, out b, out var secondRoom)
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
            if (!VenueUnchanged(lease)) return Fail(out reason,"The reserved interaction anchors moved or became unavailable.");
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
            => CanWitness(player,lease,out _);

        public bool CanWitness(HousePlayerController player,HouseMeetingLease lease,out Vector3 visibleMidpoint)
        {
            visibleMidpoint=default;
            if (player == null || !player.gameObject.activeInHierarchy || player.gameObject.scene != rooms.Scene
                || player.Agent == null || !ValidateArrivedPair(lease,out _)) return false;
            var a = actors[lease.FirstId].npc.transform; var b = actors[lease.SecondId].npc.transform;
            if(!WitnessPoint(a,lease.Seated,out var first) || !WitnessPoint(b,lease.Seated,out var second)
                || !WitnessPoint(player.transform,false,out var observer))return false;
            var midpoint = (first+second)*.5f;
            if (HorizontalSquared(observer,midpoint) >= 36
                || !rooms.TryLocate(player.transform.position,player.Agent.radius,out var room) || room != lease.RoomId) return false;
            if(!rooms.HasClearSight(player.transform,a,observer,first) || !rooms.HasClearSight(player.transform,b,observer,second))return false;
            visibleMidpoint=midpoint;return true;
        }

        private static bool WitnessPoint(Transform actor,bool seatedRequired,out Vector3 point)
        {
            point=default;
            var seat=actor.GetComponent<HouseSeatPresentation>();
            if(seatedRequired || seat!=null && seat.Active)
                return seat!=null && seat.TryGetWitnessPoint(out point);
            point=actor.position+Vector3.up*1.15f;
            return HouseRoomQuery.Finite(point);
        }

        public void Tick()
        {
            if (disposed) return;
            RetireInvalidActivities();
            RetireInvalidTalk();
            leaseBuffer.Clear(); leaseBuffer.AddRange(leases.Values); leaseBuffer.Sort(leaseComparison);
            foreach (var lease in leaseBuffer)
            {
                if (!ValidActors(lease,out _,out _)) { Retire(lease,HouseMeetingStatus.Invalid,"An actor or binding became invalid."); continue; }
                if (!VenueUnchanged(lease)) { Retire(lease,HouseMeetingStatus.Invalid,"The reserved interaction anchors moved or became unavailable."); continue; }
                lease.Status = paused ? HouseMeetingStatus.Paused
                    : ValidateArrivedPair(lease,out _) ? HouseMeetingStatus.Arrived : HouseMeetingStatus.Travelling;
            }
        }

        public void SetPaused(bool value)
        {
            if (disposed || paused == value) return;
            paused = value;
            foreach (var actor in cast) if (actor.motion != null) actor.motion.SetPaused(value && !OpeningHoldsActor(actor.id) && !CeremonyHoldsActor(actor.id) && !DepartureHoldsActor(actor.id) && !WanderHoldsActor(actor.id) && !TalkHoldsActor(actor.id) && !ActHoldsActor(actor.id));
            foreach (var lease in leases.Values) lease.Status = value ? HouseMeetingStatus.Paused : HouseMeetingStatus.Travelling;
        }
        public bool Release(HouseMeetingLease lease) => Current(lease) && Retire(lease,HouseMeetingStatus.Released,null);
        public void ReleaseAll()
        {
            ReleaseActivities();
            EndCompetitionStage();
            EndOpeningStage();
            EndCeremonyStage();
            EndSceneStage();
            EndDeparture();
            EndWandering();
            EndTalk();
            EndActStaging();
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
            if (ActivityOwnsMotion(motion)) return true;
            if (CompetitionOwnsMotion(motion)) return true;
            if (OpeningOwnsMotion(motion)) return true;
            if (CeremonyOwnsMotion(motion)) return true;
            if (SceneOwnsMotion(motion)) return true;
            if (DepartureOwnsMotion(motion)) return true;
            if (WanderOwnsMotion(motion)) return true;
            if (TalkOwnsMotion(motion)) return true;
            if (ActOwnsMotion(motion)) return true;
            if (motion.LeaseId == null || !leases.TryGetValue(motion.LeaseId, out var lease)) return false;
            return actors.TryGetValue(lease.FirstId, out var first) && first.motion == motion
                || actors.TryGetValue(lease.SecondId, out var second) && second.motion == motion;
        }
        private bool Retire(HouseMeetingLease lease, HouseMeetingStatus status, string reason)
        {
            if (!Current(lease)) return false;
            if (actors.TryGetValue(lease.FirstId,out var first) && first.motion != null)
            {StandUp(first.motion);first.motion.Release(lease.Token);}
            if (actors.TryGetValue(lease.SecondId,out var second) && second.motion != null)
            {StandUp(second.motion);second.motion.Release(lease.Token);}
            leases.Remove(lease.Token); lease.Status = status; lease.FailureReason = reason;
            return true;
        }

        /// <summary>
        /// A conversation that ends gets its pair up at their chairs before they go back to their
        /// roots: the seat's own stand-up, which outlives the lease (PACK8-PASS-PLAN A2). Ending the
        /// seat here snapped both bodies from the chairs to the approaches while they still sat.
        /// </summary>
        private static void StandUp(HouseNpcMotion motion)
        {
            var seat = motion.GetComponent<HouseSeatPresentation>();
            if (seat != null) seat.RequestExit();
        }
        /// <summary>Whether anybody holds the place with these anchors: a conversation at it, an activity on it, or the player's talk.</summary>
        private bool VenueInUse(string venue)
        {
            foreach(var lease in leases.Values)if(lease.SpotId==venue)return true;
            foreach(var entry in activities)if(entry.lease.Anchor!=null && entry.lease.Anchor.VenueId==venue)return true;
            return talk!=null && talk.venue.id==venue;
        }

        /// <summary>
        /// Whether a new or reunited conversation may not be sent here. A venue holds one
        /// conversation at a time whichever of its places it is at - the simulation refuses a second
        /// at the same rendezvous - and a place is not free while anybody holds it or stands close
        /// enough to share its floor (<see cref="PlaceTaken"/>).
        /// </summary>
        private bool MeetingVenueTaken(Venue venue)
        {
            foreach(var lease in leases.Values)if(lease.VenueId==venue.family)return true;
            return PlaceTaken(venue);
        }

        /// <summary>
        /// Whether a place is held, or crowded: two places whose seats are under a cushion apart, or
        /// whose roots would wait closer than two bodies, are one patch of floor. The venues' own
        /// pairs were laid apart and are judged by their anchors alone, as they always were; a talk
        /// spot is measured against everything held near it.
        /// </summary>
        private bool PlaceTaken(Venue venue)
        {
            foreach(var lease in leases.Values)
            {
                if(lease.SpotId==venue.id)return true;
                if(venue.Home && lease.SpotId==lease.VenueId)continue;
                if(Crowds(venue,lease.FirstAnchorPosition,lease.FirstAnchorApproach)
                    || Crowds(venue,lease.SecondAnchorPosition,lease.SecondAnchorApproach))return true;
            }
            foreach(var entry in activities)
            {
                var anchor=entry.lease.Anchor;
                if(anchor==null)continue;
                if(anchor.VenueId==venue.id && !HouseFurniture.IndependentRest(anchor))return true;
                if((!venue.Home || HouseFurniture.IndependentRest(anchor)) && Crowds(venue,anchor.Position,anchor.Approach))return true;
            }
            if(talk!=null)
            {
                if(talk.venue==venue)return true;
                if((!venue.Home || !talk.venue.Home) && (Crowds(venue,talk.venue.first,talk.venue.firstApproach)
                    || Crowds(venue,talk.venue.second,talk.venue.secondApproach)))return true;
            }
            return false;
        }

        /// <summary>Whether a place - a seat or a mark, and the root that waits for it - shares the floor with either of this venue's.</summary>
        private static bool Crowds(Venue venue,Vector3 place,Vector3 approach)
        {
            float seats=HouseConversationSpots.SeatsApart*HouseConversationSpots.SeatsApart;
            float roots=HouseConversationSpots.RootsApart*HouseConversationSpots.RootsApart;
            return HorizontalSquared(venue.first,place)<seats || HorizontalSquared(venue.second,place)<seats
                || HorizontalSquared(venue.firstApproach,approach)<roots || HorizontalSquared(venue.secondApproach,approach)<roots;
        }

        /// <summary>
        /// Whether a held talk spot - a conversation at one, or the player's talk at one - shares
        /// the floor with an activity's place. The venues' own pairs answer by their anchors
        /// (<see cref="VenueInUse(string)"/>), as they always did.
        /// </summary>
        private bool SpotCrowds(HouseInteractionAnchor anchor)
        {
            if(anchor==null)return false;
            foreach(var venue in venues)
            {
                if((venue.Home && !HouseFurniture.IndependentRest(anchor)) || !venue.Valid(rooms.Scene))continue;
                bool held=talk!=null && talk.venue==venue;
                if(!held)foreach(var lease in leases.Values)if(lease.SpotId==venue.id){held=true;break;}
                if(held && Crowds(venue,anchor.Position,anchor.Approach))return true;
            }
            return false;
        }

        /// <summary>
        /// A place's score for a pair: the walk, in metres along both routes, less
        /// <see cref="HouseConversationSpots.SeatedBonus"/> when the place is two seats. Lower is better.
        /// </summary>
        public static float SpotScore(float walk,bool seated) => walk-(seated ? HouseConversationSpots.SeatedBonus : 0f);

        private bool VenueUnchanged(HouseMeetingLease lease)
        {
            foreach (var venue in venues)
                if (venue.id==lease.SpotId)
                    return venue.Valid(rooms.Scene) && Vector3.Distance(venue.first,lease.FirstAnchorPosition)<.05f
                        && Vector3.Distance(venue.second,lease.SecondAnchorPosition)<.05f
                        && Vector3.Distance(venue.firstApproach,lease.FirstAnchorApproach)<.05f
                        && Vector3.Distance(venue.secondApproach,lease.SecondAnchorApproach)<.05f
                        && (!lease.Seated || Mathf.Abs(Mathf.DeltaAngle(venue.firstYaw,lease.FirstFacing))<1f
                            && Mathf.Abs(Mathf.DeltaAngle(venue.secondYaw,lease.SecondFacing))<1f);
            return false;
        }
        public bool TryGetSeat(string actorId,out HouseInteractionAnchor seat)
        {
            foreach(var lease in leases.Values)
                if(lease.Seated && (lease.FirstId==actorId || lease.SecondId==actorId))
                    foreach(var venue in venues)
                        if(venue.id==lease.SpotId && venue.Valid(rooms.Scene))
                        {seat=lease.FirstId==actorId ? venue.a : venue.b;return true;}
            seat=null;return false;
        }
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
