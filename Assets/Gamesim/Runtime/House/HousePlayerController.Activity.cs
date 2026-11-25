using System.Collections.Generic;
using Gamesim.Presentation;
using UnityEngine;
using UnityEngine.AI;

namespace Gamesim.House
{
    public sealed partial class HousePlayerController
    {
        private object activityOwner;
        private Vector3 activityDestination, previousDestination;
        private bool activityPaused, previousHadPath;

        /// <summary>One transient movement owner, with a real complete route and no root teleport.</summary>
        public bool TryBeginActivityMove(object owner,Vector3 desired,out string reason)
            => TryBeginActivityMove(owner, desired, null, out reason);

        internal bool TryBeginActivityMove(object owner, Vector3 desired, StageArrival arrival, out string reason)
        {
            reason=null;
            if(owner==null || activityOwner!=null || !isActiveAndEnabled || Agent==null || !Agent.enabled || !Agent.isOnNavMesh)
            {reason="The player already has a movement owner or cannot reach the house floor.";return false;}
            bool hadPath=Agent.hasPath;var destination=hadPath ? Agent.destination : transform.position;
            if (arrival != null)
            {
                if (!arrival.TryRoute(Agent, desired, transform, Agent.radius, Agent.height, out float metres))
                {
                    if (hadPath && !Agent.hasPath) TrySetReachablePath(destination, destinationSampleRadius);
                    reason = "There is no short, clear approach to this activity.";
                    return false;
                }
                RouteMetres = metres;
                floorWalkEnd = null;
                Agent.isStopped = false;
            }
            else if(!TrySetReachablePath(desired,Mathf.Min(.2f,destinationSampleRadius)))
            {reason="There is no complete route to this activity.";return false;}
            previousHadPath=hadPath;previousDestination=destination;
            activityDestination=Agent.destination;activityOwner=owner;activityPaused=false;
            // At the gait the route deserves, as every other trip is chosen: a walk to the next
            // chair, a run across the house to the pool.
            ApplyGait(arrival == null && RouteMetres > RunRouteMetres);
            ApplyPauseState();return true;
        }

        /// <summary>
        /// Sets the gait of the activity move <paramref name="owner"/> holds: a competition course's leg
        /// is run whatever its length, where a trip of a few metres would be walked. A run is only set
        /// for the owner of the move; the walk is put back for the owner, or once nobody owns the move
        /// (the show let the player go before its runners were stood down). False when it was refused.
        /// </summary>
        public bool SetActivityGait(object owner,bool run)
        {
            if(owner==null)return false;
            if(run ? !ReferenceEquals(owner,activityOwner) : activityOwner!=null && !ReferenceEquals(owner,activityOwner))return false;
            ApplyGait(run);return true;
        }

        /// <summary>
        /// Puts the player on the destination of the activity move <paramref name="owner"/> holds, now,
        /// through the agent's own warp onto the walking map: a competition show's walk out at its last
        /// moment, so the player starts the show at their station (competition show edges). The move
        /// stays the owner's and counts as arrived (<see cref="ActivityHasArrived"/>); the gait goes back
        /// to the walk. False, and nothing moved, for another owner or an agent off the walking map.
        /// </summary>
        public bool TryPlaceAtActivityDestination(object owner)
        {
            if(owner==null || !ReferenceEquals(owner,activityOwner) || !isActiveAndEnabled || Agent==null || !Agent.enabled || !Agent.isOnNavMesh)return false;
            if(!Agent.Warp(activityDestination))return false;
            Agent.ResetPath();Agent.velocity=Vector3.zero;
            RouteMetres=0f;ApplyGait(false);
            Physics.SyncTransforms();
            return true;
        }

        /// <summary>Whether something - an activity, the diary chair, a competition station - owns the player's movement now.</summary>
        public bool HasActivityOwner => activityOwner!=null;

        public bool IsActivityMoveValid(object owner)
            => ReferenceEquals(owner,activityOwner) && owner!=null && isActiveAndEnabled && Agent!=null
                && Agent.enabled && Agent.isOnNavMesh && (Agent.hasPath || NearActivityDestination());

        public bool ActivityHasArrived(object owner)
            => IsActivityMoveValid(owner) && NearActivityDestination() && !Agent.pathPending
                && Agent.velocity.sqrMagnitude<.04f;

        public bool PauseActivityMove(object owner,bool paused)
        {
            if(!ReferenceEquals(owner,activityOwner) || owner==null)return false;
            activityPaused=paused;ApplyPauseState();
            if(paused && Agent!=null && Agent.enabled && Agent.isOnNavMesh)Agent.velocity=Vector3.zero;
            return true;
        }

        public bool ReleaseActivityMove(object owner)
        {
            if(owner==null || !ReferenceEquals(owner,activityOwner))return false;
            activityOwner=null;activityPaused=false;
            if(Agent!=null && Agent.enabled && Agent.isOnNavMesh)
            {
                Agent.ResetPath();Agent.velocity=Vector3.zero;
                if(previousHadPath)TrySetReachablePath(previousDestination,destinationSampleRadius);
            }
            previousHadPath=false;
            // SetInputEnabled calls made by a newly opened panel win over the old input state.
            ApplyPauseState();return true;
        }

        private bool NearActivityDestination()
            => Vector3.Distance(transform.position,activityDestination)<=Mathf.Max(.25f,Agent.stoppingDistance+.1f);

        private void ValidateActivityOwner()
        {
            if(activityOwner is Object unityOwner && unityOwner==null)ReleaseActivityMove(activityOwner);
        }

        private void OnDisable()
        {
            if(activityOwner!=null)ReleaseActivityMove(activityOwner);
        }
    }

    /// <summary>
    /// A stage's transient ingress. It searches only a small neighbourhood of the assigned mark
    /// and prefers a start outside the live shot. A covered cut can use a nearby in-frame start
    /// when the shot or floor leaves no offscreen route.
    /// </summary>
    internal sealed class StageArrival
    {
        internal const float MaxMetres = 4.5f;
        private const float MinMetres = 1f;
        private static readonly float[] Rings = { 1.25f, 2f, 2.75f, 3.5f, 4.2f };
        private readonly HouseRoomQuery rooms;
        private readonly Camera camera;
        private readonly System.Action conceal;
        private readonly Plane[] outgoingFrustum = new Plane[6];
        private readonly Plane[] incomingFrustum = new Plane[6];
        private readonly NavMeshPath path = new NavMeshPath();
        private readonly Vector3[] corners = new Vector3[32];
        private readonly List<Vector3> protectedMarks = new List<Vector3>();
        private readonly List<Renderer> renderers = new List<Renderer>();

        internal void Protect(Vector3 mark) => protectedMarks.Add(mark);

        internal StageArrival(HouseRoomQuery rooms, Camera camera, System.Action conceal)
        {
            this.rooms = rooms;
            this.camera = camera;
            this.conceal = conceal;
            if (camera != null) GeometryUtility.CalculateFrustumPlanes(camera, outgoingFrustum);
        }

        internal bool TryRoute(NavMeshAgent agent, Vector3 desired, Transform body, float radius, float height, out float metres)
        {
            metres = 0f;
            if (rooms == null || agent == null || !agent.enabled || !agent.isOnNavMesh || !agent.updatePosition)
                return false;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!rooms.TrySampleFloor(desired, radius, filter, .25f, out var mark, out _)
                || !rooms.HasCapsuleClearance(mark, radius, height, body))
                return false;

            Vector3 origin = body.position;
            if (camera != null) GeometryUtility.CalculateFrustumPlanes(camera, incomingFrustum);
            var bodyBounds = BodyBounds(body, radius, height);
            bool sourceOutside = Outside(outgoingFrustum, bodyBounds);
            bool canRelocate = conceal != null || sourceOutside;
            bool canStartInView = conceal != null;
            Vector3? coveredStart = null;

            if (canRelocate)
            {
                foreach (float ring in Rings)
                {
                    for (int i = 0; i < 16; i++)
                    {
                        float angle = i * (Mathf.PI / 8f);
                        var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring;
                        if (!NavMesh.SamplePosition(mark + offset, out var hit, .24f, filter)
                            || Mathf.Abs(hit.position.y - mark.y) > .2f)
                            continue;
                        var entranceBounds = bodyBounds;
                        entranceBounds.center += hit.position - origin;
                        bool outside = Outside(incomingFrustum, entranceBounds);
                        if (!outside && (!canStartInView || coveredStart.HasValue)) continue;
                        if (!ClearsMarks(hit.position, radius) || !rooms.HasCapsuleClearance(hit.position, radius, height, body)
                            || !Plan(hit.position, mark, filter, out float length) || length < MinMetres)
                            continue;
                        if (outside && Install(agent, origin, hit.position, length, !sourceOutside, out metres)) return true;
                        if (!outside) coveredStart = hit.position;
                    }
                }
            }

            // A blocked or unusually wide shot can still reveal an actual nearby walk under the cut.
            if (coveredStart.HasValue && Plan(coveredStart.Value, mark, filter, out float coveredLength))
                if (Install(agent, origin, coveredStart.Value, coveredLength, true, out metres)) return true;

            // Never replace an unavailable entrance with a whole-house walk. A close actor can
            // simply stay in the shot and finish the same native route.
            if (!Plan(origin, mark, filter, out float localLength) || !agent.SetPath(path)) return false;
            metres = localLength;
            return true;
        }

        private bool ClearsMarks(Vector3 at, float radius)
        {
            float spacing = radius * 2f + .15f;
            foreach (var mark in protectedMarks)
                if ((mark - at).sqrMagnitude < spacing * spacing) return false;
            return true;
        }

        private Bounds BodyBounds(Transform body, float radius, float height)
        {
            float width = (radius + .25f) * 2f;
            var bounds = new Bounds(body.position + Vector3.up * (height * .5f), new Vector3(width, height + .3f, width));
            var visual = body.GetComponent<CharacterPresentation>()?.VisualRoot;
            if (visual == null) return bounds;
            visual.GetComponentsInChildren(false, renderers);
            foreach (var renderer in renderers)
                if (renderer != null && renderer.enabled) bounds.Encapsulate(renderer.bounds);
            renderers.Clear();
            return bounds;
        }

        private bool Outside(Plane[] frustum, Bounds bounds)
            => camera != null && !GeometryUtility.TestPlanesAABB(frustum, bounds);

        private bool Plan(Vector3 start, Vector3 mark, NavMeshQueryFilter filter, out float metres)
        {
            metres = 0f;
            if (Vector3.Distance(start, mark) > MaxMetres
                || !NavMesh.CalculatePath(start, mark, filter, path) || path.status != NavMeshPathStatus.PathComplete)
                return false;
            int count = path.GetCornersNonAlloc(corners);
            if (count == 0 || count == corners.Length) return false;
            metres = Vector3.Distance(start, corners[0]);
            for (int i = 1; i < count; i++) metres += Vector3.Distance(corners[i - 1], corners[i]);
            return metres <= MaxMetres && Vector3.Distance(corners[count - 1], mark) < .15f;
        }

        private bool Install(NavMeshAgent agent, Vector3 origin, Vector3 start, float length, bool cover, out float metres)
        {
            metres = 0f;
            if (!agent.Warp(start)) return false;
            if (!agent.SetPath(path))
            {
                agent.Warp(origin);
                return false;
            }
            if (cover) conceal?.Invoke();
            agent.velocity = Vector3.zero;
            Physics.SyncTransforms();
            metres = length;
            return true;
        }
    }
}
