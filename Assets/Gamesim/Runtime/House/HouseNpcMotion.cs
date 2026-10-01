using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Gamesim.House
{
    public enum HouseNpcMotionState { Unbound, Binding, Idle, Walking, Arrived, Paused, Failed }

    /// <summary>
    /// Sole movement owner for an explicitly bound house NPC. No scheduler, clock,
    /// topics, simulation writes, persistence, fallback teleport or automatic patrol.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HouseNpcMotion : MonoBehaviour
    {
        /// <summary>
        /// The highest cast slot a houseguest can bind from. This was 4 for the six-person slice and
        /// stayed there after the cast screen learned to seat sixteen, so a new season whose
        /// houseguests landed on roots beyond the first five was refused its rebind and every
        /// houseguest stood still - which is what the standalone verifier saw after a reload.
        /// </summary>
        public const int MaxCastIndex = 15;

        /// <summary>The house's walk, metres a second: the pace every route is walked at unless its borrower slows it.</summary>
        public const float DefaultSpeed = 2.2f;

        private HouseNpc npc;
        private HouseRoomQuery rooms;
        private CapsuleCollider capsule;
        private NavMeshObstacle obstacle;
        private NavMeshAgent ownedAgent;
        private NavMeshQueryFilter filter;
        private bool originalObstacleEnabled, capturedOwnership, paused;
        private string boundId, leaseId, destinationRoom;
        private Vector3 bindOrigin, destination, requestedDestination;
        private int bindingFrame, arrivalFrames;
        private float bindingDeadline;
        private HouseNpcMotionState state;
        private NavMeshPath candidatePath;
        private object coordinatorOwner;
        private readonly List<Component> rootComponents = new List<Component>(12);
        private readonly List<NavMeshAgent> agentBuffer = new List<NavMeshAgent>(2);
        private readonly List<Animator> animatorBuffer = new List<Animator>(2);
        private readonly List<NavMeshObstacle> obstacleBuffer = new List<NavMeshObstacle>(2);

        public HouseNpcMotionState State => state;
        public string FailureReason { get; private set; }
        public string BoundNpcId => boundId;
        public string LeaseId => leaseId;
        public Vector3 ReservedDestination => destination;
        public NavMeshAgent Agent => ownedAgent;
        public bool IsBound => isActiveAndEnabled && npc != null && npc.Id == boundId
            && ownedAgent != null && ownedAgent.enabled && ownedAgent.isOnNavMesh && obstacle != null && !obstacle.enabled
            && state != HouseNpcMotionState.Unbound && state != HouseNpcMotionState.Binding && state != HouseNpcMotionState.Failed;

        private void Awake() { candidatePath = new NavMeshPath(); }

        // A plain coordinator must release its claim through Dispose, not rely on
        // a Mono callback. Direct primitive users remain valid while unclaimed.
        internal bool CanClaim(object owner) => owner != null && (coordinatorOwner == null || ReferenceEquals(coordinatorOwner, owner));
        internal bool TryClaim(object owner)
        { if (!CanClaim(owner)) return false; coordinatorOwner = owner; return true; }
        internal void ReleaseClaim(object owner)
        { if (ReferenceEquals(coordinatorOwner, owner)) coordinatorOwner = null; }

        public static bool TryCreate(HouseNpc character, HouseRoomQuery query, NavMeshQueryFilter navFilter,
            int castIndex, out HouseNpcMotion motion, out string reason)
        {
            motion = null;
            reason = null;
            if (!Application.isPlaying || character == null || !character.gameObject.activeInHierarchy || query == null
                || character.gameObject.scene != query.Scene || castIndex < 0 || castIndex > MaxCastIndex
                || navFilter.agentTypeID < 0 || NavMesh.GetSettingsByID(navFilter.agentTypeID).agentTypeID == -1 || navFilter.areaMask == 0)
            { reason = "Motion creation requires Play Mode, a local active NPC, room query and cast index 0–4."; return false; }
            if (character.GetComponent<HouseNpcMotion>() != null)
            { reason = "This NPC already has a motion owner; reuse it rather than adding another."; return false; }
            if (!ValidateComponents(character, null, null, out var body, out var carving, out reason)) return false;
            if (!query.TryValidateScene(out reason)) return false;

            // Never AddComponent<NavMeshAgent> to the active carved actor: OnEnable
            // could bind it before callers can disable it. This is an explicit short
            // initial lifecycle transition; no identity/pose/scene asset is replaced.
            var root = character.gameObject;
            root.SetActive(false);
            try
            {
                motion = root.AddComponent<HouseNpcMotion>();
                motion.npc = character; motion.rooms = query; motion.capsule = body; motion.obstacle = carving;
                motion.originalObstacleEnabled = carving.enabled;
                motion.capturedOwnership = true; motion.boundId = character.Id; motion.filter = navFilter;
                motion.ownedAgent = root.AddComponent<NavMeshAgent>();
                var agent = motion.ownedAgent;
                agent.enabled = false;
                agent.agentTypeID = navFilter.agentTypeID; agent.areaMask = navFilter.areaMask;
                agent.radius = body.radius; agent.height = body.height;
                agent.speed = DefaultSpeed; agent.acceleration = 12f; agent.angularSpeed = 360f;
                agent.stoppingDistance = .15f; agent.avoidancePriority = 60 + castIndex;
                agent.autoTraverseOffMeshLink = false;
                agent.updatePosition = true; agent.updateRotation = true;
            }
            finally { if (root != null) root.SetActive(true); }
            return motion.BeginBinding(out reason);
        }

        public bool BeginBinding(out string reason)
        {
            reason = null;
            if (!isActiveAndEnabled || npc == null || rooms == null || ownedAgent == null
                || !capturedOwnership || ownedAgent.enabled || state == HouseNpcMotionState.Binding)
            { reason = "This motion owner is not available for binding."; return false; }
            if (npc.Id != boundId || !ValidateOwnedComponents(out reason) || !rooms.TryValidateScene(out reason))
            { reason = reason ?? "The NPC identity changed; create an explicitly reviewed new binding."; Fail(reason); return false; }
            if (!rooms.TryLocate(transform.position, capsule.radius, out _))
            { reason = rooms.LastFailure; Fail(reason); return false; }
            paused = false; leaseId = null; arrivalFrames = 0;
            bindOrigin = transform.position;
            bindingFrame = Time.frameCount; bindingDeadline = Time.unscaledTime + 2f;
            obstacle.enabled = false;
            state = HouseNpcMotionState.Binding; FailureReason = null;
            return true;
        }

        /// <summary>
        /// Explicit import/new-session identity boundary. The caller must Unbind
        /// first; same-ID presentation updates must not call this method.
        /// </summary>
        public bool RebindIdentity(out string reason, int castIndex = -1)
        {
            reason = null;
            if (!isActiveAndEnabled || state != HouseNpcMotionState.Unbound || ownedAgent == null || ownedAgent.enabled
                || npc == null || rooms == null || string.IsNullOrWhiteSpace(npc.Id) || castIndex < -1 || castIndex > MaxCastIndex)
            { reason = "Identity rebinding requires an explicitly unbound active owner, disabled owned agent and valid identity/index."; return false; }
            if (!ValidateOwnedComponents(out reason) || !rooms.TryValidateScene(out reason)) return false;
            boundId = npc.Id;
            if (castIndex >= 0) ownedAgent.avoidancePriority = 60 + castIndex;
            return BeginBinding(out reason);
        }

        public bool TryReserveAndPath(string reservationId, Vector3 requested)
        {
            if (!IsBound || paused || string.IsNullOrWhiteSpace(reservationId) || reservationId.Length > 128
                || !HouseRoomQuery.Finite(requested) || !ValidateOwnedComponents(out _)) return false;
            if (leaseId != null)
                return leaseId == reservationId && (requested - requestedDestination).sqrMagnitude <= .000001f;
            if (!rooms.TrySampleFloor(requested, capsule.radius, filter, .25f, out var sampled, out var room))
            { LastRouteFailure = "no floor within 0.25 m of " + requested.ToString("F2") + (rooms.LastFailure != null ? " (" + rooms.LastFailure + ")" : ""); return false; }
            if (!rooms.HasCapsuleClearance(sampled, capsule.radius, capsule.height, transform))
            { LastRouteFailure = "no room to stand at " + sampled.ToString("F2") + (rooms.LastFailure != null ? " (" + rooms.LastFailure + ")" : ""); return false; }
            if (!ownedAgent.CalculatePath(sampled, candidatePath) || candidatePath.status != NavMeshPathStatus.PathComplete)
            { LastRouteFailure = "no complete path from " + transform.position.ToString("F2") + " to " + sampled.ToString("F2") + " (" + candidatePath.status + ")"; return false; }
            if (!ownedAgent.SetPath(candidatePath)) { LastRouteFailure = "the agent refused the path"; return false; }
            LastRouteFailure = null;
            leaseId = reservationId; destination = sampled; requestedDestination = requested; destinationRoom = room;
            arrivalFrames = 0; state = HouseNpcMotionState.Walking;
            ownedAgent.updateRotation = true; ownedAgent.isStopped = false;
            return true;
        }

        public bool HasArrivedAt(string reservationId) => reservationId != null && reservationId == leaseId
            && !paused && state == HouseNpcMotionState.Arrived && PhysicalArrival();

        /// <summary>
        /// Whether this body stands on the destination its reservation walked it to - within the
        /// arrival test's reach, stopped, on the destination's floor - whoever's capsule is against
        /// it. <see cref="HasArrivedAt"/> also asks for the clearance, and a body passing close
        /// enough to touch withholds it; a ceremony's places are the body's own, so the ceremony
        /// asks this instead (measured 2026-09-29, endgame-f34b: a walker held against a body
        /// waiting on its approach refused that body's arrival, the body was never seated and
        /// parked, and so the walker was never let past).
        /// </summary>
        public bool IsOnMark(string reservationId) => reservationId != null && reservationId == leaseId
            && !paused && PhysicalArrival(false);

        /// <summary>How far the route this body holds still runs, in metres along its path, or -1 when it holds none.</summary>
        public float RouteLength
        {
            get
            {
                if (!IsBound || leaseId == null || ownedAgent == null || !ownedAgent.hasPath) return -1f;
                var corners = ownedAgent.path.corners;
                float total = 0f;
                for (int i = 1; i < corners.Length; i++) total += Vector3.Distance(corners[i - 1], corners[i]);
                return total;
            }
        }

        private bool parked;
        private ObstacleAvoidanceType unparkedAvoidance = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
        /// <summary>A parked agent's radius: a point in the crowd, not a body.</summary>
        public const float ParkedRadius = 0.05f;

        /// <summary>
        /// Parks the agent while the visual body sits. A seat moves only the visual body, so the
        /// root stays on its approach as a stopped agent, and at its full radius it blocks the
        /// approaches beside it: measured 2026-09-28, the sofa's third seat, 0.52 m from the
        /// second, never filled. Parked, the agent is a point nobody has to avoid, and the root
        /// stands on the furniture layer, which the sight queries leave out: an arrival is refused
        /// while the root overlaps another actor's capsule (<see cref="PhysicalArrival"/>), two
        /// roots half a metre apart always overlap at the bodies' radius, and the capsule itself
        /// must stay enabled, upright and whole for the motion to keep its binding
        /// (<see cref="ValidateComponents"/>). Unparked, it is a body again. The visual body on
        /// the seat is picked by <see cref="HouseSeatPresentation.TryPickNpc"/>, not by the root.
        /// </summary>
        public void SetParked(bool value)
        {
            if (parked == value) return;
            parked = value;
            if (value)
            {
                unparkedLayer = gameObject.layer;
                gameObject.layer = HouseLayers.Furniture;
                if (ownedAgent != null)
                {
                    unparkedAvoidance = ownedAgent.obstacleAvoidanceType;
                    ownedAgent.radius = ParkedRadius;
                    ownedAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                }
            }
            else
            {
                gameObject.layer = unparkedLayer;
                if (ownedAgent != null)
                {
                    ownedAgent.radius = capsule != null ? capsule.radius : ownedAgent.radius;
                    ownedAgent.obstacleAvoidanceType = unparkedAvoidance;
                }
            }
        }

        private int unparkedLayer;

        /// <summary>Whether the agent is parked under a seated body.</summary>
        public bool IsParked => parked;

        /// <summary>Why the last route was refused, for the coordinator's refusal and the stage's report; null after a route was taken.</summary>
        public string LastRouteFailure { get; private set; }

        /// <summary>
        /// Why this body does not count as arrived right now, in the arrival test's own terms
        /// (<see cref="PhysicalArrival"/>), or null when it would: the stage's report prints it for
        /// a body standing on its mark that the coordinator still calls walking.
        /// </summary>
        public string ArrivalFailure
        {
            get
            {
                if (!IsBound) return "not bound";
                if (!ValidateOwnedComponents(out var invalid)) return invalid;
                if (leaseId == null) return "no lease";
                if (ownedAgent.pathPending) return "the path is pending";
                if (ownedAgent.isOnOffMeshLink) return "on an off-mesh link";
                if (!HouseRoomQuery.Finite(ownedAgent.velocity) || ownedAgent.velocity.sqrMagnitude >= .04f) return "moving at " + ownedAgent.velocity.magnitude.ToString("F2") + " m/s";
                if (!HouseRoomQuery.Finite(transform.position)) return "no position";
                var offset = transform.position - destination;
                if (offset.x * offset.x + offset.z * offset.z > .25f * .25f) return new Vector2(offset.x, offset.z).magnitude.ToString("F2") + " m from the destination " + destination.ToString("F2");
                if (Mathf.Abs(offset.y) > .20f) return offset.y.ToString("F2") + " m above or below the destination";
                if (ownedAgent.hasPath)
                {
                    if (ownedAgent.pathStatus != NavMeshPathStatus.PathComplete) return "the path is " + ownedAgent.pathStatus;
                    if (!HouseRoomQuery.Finite(ownedAgent.remainingDistance)) return "the remaining distance is not finite";
                    if (!HouseRoomQuery.Finite(ownedAgent.destination) || Vector3.Distance(ownedAgent.destination, destination) > .05f)
                        return "the agent's destination drifted to " + ownedAgent.destination.ToString("F2");
                    if (ownedAgent.remainingDistance > ownedAgent.stoppingDistance + .10f) return ownedAgent.remainingDistance.ToString("F2") + " m of path remain";
                }
                if (!rooms.TryLocate(transform.position, capsule.radius, out var room)) return "no floor under the body" + (rooms.LastFailure != null ? " (" + rooms.LastFailure + ")" : "");
                if (room != destinationRoom) return "in the " + room + " room, bound for the " + destinationRoom;
                if (!rooms.HasCapsuleClearance(transform.position, capsule.radius, capsule.height, transform)) return "no clearance" + (rooms.LastFailure != null ? " (" + rooms.LastFailure + ")" : "");
                return null;
            }
        }

        public void SetPaused(bool value)
        {
            if (paused == value) return; // Repeated coordinator ticks must not reset arrival proof.
            paused = value;
            if (!IsBound) return;
            arrivalFrames = 0;
            ownedAgent.velocity = Vector3.zero;
            ownedAgent.isStopped = value || leaseId == null;
            state = value ? HouseNpcMotionState.Paused
                : leaseId == null ? HouseNpcMotionState.Idle : HouseNpcMotionState.Walking;
        }

        /// <summary>How fast this body walks its route now, metres a second.</summary>
        public float Speed => ownedAgent != null ? ownedAgent.speed : DefaultSpeed;

        /// <summary>
        /// Walks the route at another pace, metres a second, or at <see cref="DefaultSpeed"/> again
        /// for zero or less: the evicted's slow walk out (PACK8-PASS-PLAN C2). Releasing the route
        /// and failing both put the house's pace back, so no slow walk outlives the borrow that
        /// asked for it.
        /// </summary>
        public void SetSpeed(float metresPerSecond)
        {
            if (ownedAgent != null) ownedAgent.speed = metresPerSecond > 0f ? metresPerSecond : DefaultSpeed;
        }

        public bool Release(string reservationId)
        {
            if (reservationId == null || reservationId != leaseId) return false;
            leaseId = null; destinationRoom = null; arrivalFrames = 0;
            if (ownedAgent != null) ownedAgent.speed = DefaultSpeed;
            if (ownedAgent != null && ownedAgent.enabled && ownedAgent.isOnNavMesh)
            {
                // Clearing the native path can reset its stopped state. Establish
                // idle ownership after that operation, including residual velocity.
                ownedAgent.ResetPath(); ownedAgent.isStopped = true;
                ownedAgent.velocity = Vector3.zero; ownedAgent.updateRotation = true;
            }
            if (IsBound) state = paused ? HouseNpcMotionState.Paused : HouseNpcMotionState.Idle;
            return true;
        }

        /// <summary>Reservation release keeps the agent bound; Unbind restores the static obstacle owner.</summary>
        public void Unbind()
        {
            RestoreStaticOwner();
            state = HouseNpcMotionState.Unbound;
        }

        private void Update()
        {
            if (state == HouseNpcMotionState.Unbound || state == HouseNpcMotionState.Failed) return;
            string reason = null;
            if (npc == null || npc.Id != boundId || !ValidateOwnedComponents(out reason))
            { Fail(reason ?? "The bound NPC identity changed."); return; }
            if (!rooms.TryValidateScene(out reason)) { Fail(reason); return; }
            if (state == HouseNpcMotionState.Binding)
            {
                if (Time.frameCount <= bindingFrame) return;
                if ((transform.position - bindOrigin).sqrMagnitude > .0001f)
                { Fail("Another owner moved the NPC during binding."); return; }
                if (rooms.TrySampleFloor(bindOrigin, capsule.radius, filter, .25f, out var sample, out _)
                    && rooms.HasCapsuleClearance(sample, capsule.radius, capsule.height, transform))
                {
                    // The sole permitted direct position adjustment, <=.25 m and same
                    // floor. This is initial NavMesh binding, never a travel fallback.
                    transform.position = sample;
                    ownedAgent.enabled = true;
                    if (!ownedAgent.isOnNavMesh)
                    { Fail("The sampled NPC floor could not bind its compatible agent."); return; }
                    ownedAgent.isStopped = true;
                    state = paused ? HouseNpcMotionState.Paused : HouseNpcMotionState.Idle;
                    return;
                }
                if (Time.frameCount - bindingFrame >= 60 || Time.unscaledTime >= bindingDeadline)
                {
                    // Say who, where, and which half of the check refused. This message has been
                    // the whole of the evidence twice now - once when obstacle carving took
                    // seventeen tests down, once when the furniture got collision - and both times
                    // it named the symptom and nothing else, so the search started from scratch.
                    Fail("NPC carving did not clear to a safe floor binding within the bounded wait."
                         + " " + (npc != null ? npc.Id : "?") + " at " + bindOrigin.ToString("0.00")
                         + " · " + (rooms.LastFailure ?? "the floor sampled but the body did not fit"));
                }
                return;
            }
            if (!IsBound) { Fail("The owned NPC agent lost its NavMesh binding."); return; }
            // isStopped retains the route; explicit zero velocity also removes
            // residual crowd/acceleration drift while the modal owns the pause.
            if (paused || leaseId == null) { ownedAgent.velocity = Vector3.zero; return; }
            if (PhysicalArrival())
            {
                arrivalFrames = Mathf.Min(2, arrivalFrames + 1);
                if (arrivalFrames >= 2)
                { ownedAgent.isStopped = true; state = HouseNpcMotionState.Arrived; }
            }
            else
            {
                arrivalFrames = 0;
                if (state == HouseNpcMotionState.Arrived)
                { state = HouseNpcMotionState.Walking; ownedAgent.isStopped = false; }
            }
        }

        private bool PhysicalArrival(bool clearance = true)
        {
            if (!IsBound || !ValidateOwnedComponents(out _) || leaseId == null || ownedAgent.pathPending || ownedAgent.isOnOffMeshLink
                || !HouseRoomQuery.Finite(ownedAgent.velocity) || ownedAgent.velocity.sqrMagnitude >= .04f
                || !HouseRoomQuery.Finite(transform.position)) return false;
            var offset = transform.position - destination;
            if (offset.x * offset.x + offset.z * offset.z > .25f * .25f || Mathf.Abs(offset.y) > .20f) return false;
            if (ownedAgent.hasPath && (ownedAgent.pathStatus != NavMeshPathStatus.PathComplete
                || !HouseRoomQuery.Finite(ownedAgent.remainingDistance)
                || !HouseRoomQuery.Finite(ownedAgent.destination) || Vector3.Distance(ownedAgent.destination, destination) > .05f
                || ownedAgent.remainingDistance > ownedAgent.stoppingDistance + .10f)) return false;
            return rooms.TryLocate(transform.position, capsule.radius, out var room) && room == destinationRoom
                && (!clearance || rooms.HasCapsuleClearance(transform.position, capsule.radius, capsule.height, transform));
        }

        private bool ValidateOwnedComponents(out string reason)
        {
            if (npc == null || rooms == null || npc.gameObject.scene != rooms.Scene)
            { reason = "The NPC moved outside its bound house scene."; return false; }
            if (!ValidateComponents(npc, this, ownedAgent, out var body, out var carving, out reason)) return false;
            if (body != capsule || carving != obstacle)
            { reason = "The original collision components changed."; return false; }
            if (obstacle.enabled && state != HouseNpcMotionState.Unbound && state != HouseNpcMotionState.Failed)
            { reason = "Another owner enabled the NPC obstacle while its motion adapter was active."; return false; }
            return true;
        }

        private static bool ValidateComponents(HouseNpc character, HouseNpcMotion owner, NavMeshAgent allowedAgent,
            out CapsuleCollider body, out NavMeshObstacle carving, out string reason)
        {
            body = null; carving = null; reason = null;
            if (character == null) { reason = "The NPC is missing."; return false; }
            var root = character.gameObject;
            if (root.GetComponentInChildren<Rigidbody>(true) != null || root.GetComponentInChildren<CharacterController>(true) != null)
            { reason = "A physics/controller mover already owns this NPC."; return false; }
            var components = owner != null ? owner.rootComponents : new List<Component>(12);
            components.Clear(); root.GetComponents(components);
            foreach (var component in components)
            {
                if (component is CapsuleCollider capsule)
                {
                    if (body != null) { reason = "The NPC has more than one root capsule."; return false; }
                    body = capsule;
                }
                else if (component is NavMeshObstacle obstacle)
                {
                    if (carving != null) { reason = "The NPC has more than one obstacle."; return false; }
                    carving = obstacle;
                }
                else if (component is Collider)
                { reason = "An additional root collider requires explicit body-ownership review."; return false; }
                else if (component is MonoBehaviour behaviour && behaviour != character && behaviour != owner
                    // Reviewed visual-only seating offsets CharacterPresentation.VisualRoot, never
                    // this navigation root, its agent, capsule or obstacle.
                    && !(behaviour is HouseSeatPresentation)
                    && !(behaviour is HouseFurniturePose)
                    && behaviour.GetType().FullName != "Gamesim.Presentation.CharacterPresentation")
                { reason = "An unrecognized root behaviour requires explicit movement-ownership review."; return false; }
            }
            var animators = owner != null ? owner.animatorBuffer : new List<Animator>(2);
            animators.Clear(); root.GetComponentsInChildren(true, animators);
            foreach (var animator in animators)
                if (animator.enabled && animator.applyRootMotion)
                { reason = "Root-motion animation already owns this NPC."; return false; }
            var agents = owner != null ? owner.agentBuffer : new List<NavMeshAgent>(2);
            agents.Clear(); root.GetComponentsInChildren(true, agents);
            if (agents.Count != (allowedAgent != null ? 1 : 0) || allowedAgent != null && agents[0] != allowedAgent)
            { reason = "A pre-existing or additional NavMeshAgent has unknown ownership."; return false; }
            var obstacles = owner != null ? owner.obstacleBuffer : new List<NavMeshObstacle>(2);
            obstacles.Clear(); root.GetComponentsInChildren(true, obstacles);
            if (obstacles.Count != 1 || obstacles[0] != carving)
            { reason = "An additional or nested obstacle has unknown movement ownership."; return false; }
            var scale = root.transform.lossyScale;
            if (body == null || carving == null || !body.enabled || body.isTrigger || body.direction != 1
                || !HouseRoomQuery.Finite(body.radius) || !HouseRoomQuery.Finite(body.height) || body.radius <= 0 || body.radius > 1
                || body.height < body.radius * 2 || body.height > 3 || !HouseRoomQuery.Finite(body.center)
                || Mathf.Abs(body.center.x) > .001f || Mathf.Abs(body.center.z) > .001f
                || Mathf.Abs(body.center.y - body.height * .5f) > .001f
                || !HouseRoomQuery.Finite(scale) || !HouseRoomQuery.Finite(root.transform.position) || !HouseRoomQuery.Finite(root.transform.up)
                || (scale - Vector3.one).sqrMagnitude > .000001f || Vector3.Dot(root.transform.up, Vector3.up) < .999f)
            { reason = "NPC motion requires its enabled, upright, unit-scale, feet-centered root capsule and existing obstacle."; return false; }
            return true;
        }

        private void Fail(string reason)
        {
            FailureReason = reason;
            if (ownedAgent != null) ownedAgent.speed = DefaultSpeed;
            RestoreStaticOwner();
            state = HouseNpcMotionState.Failed;
        }

        private void RestoreStaticOwner()
        {
            leaseId = null; destinationRoom = null; arrivalFrames = 0; paused = false;
            if (ownedAgent != null) ownedAgent.enabled = false;
            if (capturedOwnership && obstacle != null)
            {
                // Never disable an unknown agent owned by somebody else, and never
                // create an obstacle/agent conflict while reporting that violation.
                bool competingAgent = false;
                agentBuffer.Clear(); GetComponentsInChildren(true, agentBuffer);
                foreach (var agent in agentBuffer)
                    if (agent != ownedAgent && agent.enabled) competingAgent = true;
                obstacle.enabled = originalObstacleEnabled && !competingAgent;
                if (competingAgent) FailureReason = "An unknown enabled agent prevents safe obstacle restoration; explicit ownership review is required.";
            }
        }

        private void OnDisable() { Unbind(); }
        private void OnDestroy()
        {
            RestoreStaticOwner();
            if (ownedAgent != null) Destroy(ownedAgent);
        }
    }
}
