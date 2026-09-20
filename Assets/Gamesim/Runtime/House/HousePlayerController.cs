using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Gamesim.House
{
    /// <summary>Selection and validated click-to-move for the playable housemate.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed partial class HousePlayerController : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private bool isSelected = true;
        [SerializeField, Min(0.05f)] private float destinationSampleRadius = 0.6f;

        private NavMeshAgent agent;
        private NavMeshPath candidatePath;
        private HouseCameraRig cameraRig;

        /// <summary>
        /// The rig the view camera hangs under. Resolved through the camera rather than serialised,
        /// so this needs no scene wiring and cannot come back null on a scene that was authored
        /// before click-to-focus existed.
        /// </summary>
        private HouseCameraRig CameraRig => cameraRig != null
            ? cameraRig
            : cameraRig = viewCamera != null ? viewCamera.GetComponentInParent<HouseCameraRig>() : null;

        private bool requestedInputEnabled = true;
        public bool InputEnabled => requestedInputEnabled && activityOwner == null;
        public bool IsSelected => isSelected;
        public NavMeshAgent Agent => agent != null ? agent : agent = GetComponent<NavMeshAgent>();
        public event System.Action<HouseInteractionAnchor> FurnitureSelected;

        public bool HasArrived
        {
            get
            {
                var currentAgent = Agent;
                return currentAgent != null && currentAgent.enabled && currentAgent.isOnNavMesh
                    && !currentAgent.pathPending
                    && (!currentAgent.hasPath
                        || (currentAgent.remainingDistance <= currentAgent.stoppingDistance + 0.1f
                            && currentAgent.velocity.sqrMagnitude < 0.04f));
            }
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            candidatePath = new NavMeshPath();
        }

        private void Start()
        {
            // The scene stores the agent disabled. NavMeshSurface registers its data in
            // OnEnable; all scene OnEnable calls finish before Start in a fresh player.
            var currentAgent = Agent;
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = currentAgent.agentTypeID,
                areaMask = currentAgent.areaMask
            };
            if (!NavMesh.SamplePosition(transform.position, out var spawn, destinationSampleRadius, filter))
            {
                requestedInputEnabled = false;
                Debug.LogError("Gamesim player spawn has no compatible baked NavMesh.", this);
                return;
            }

            transform.position = spawn.position;
            currentAgent.enabled = true;
            ApplyPauseState();
        }

        public void Configure(Camera camera)
        {
            viewCamera = camera;
        }

        public void SetInputEnabled(bool enabled)
        {
            requestedInputEnabled = enabled;
            ApplyPauseState();
        }

        /// <summary>
        /// Accepts only reachable destinations. Failed commands leave the current path intact.
        /// </summary>
        public bool TryMoveTo(Vector3 position)
            => InputEnabled && TrySetReachablePath(position,destinationSampleRadius);

        private bool TrySetReachablePath(Vector3 position,float sampleRadius)
        {
            var currentAgent = Agent;
            if (currentAgent == null || !currentAgent.enabled || !currentAgent.isOnNavMesh
                || !IsFinite(position))
            {
                return false;
            }

            var filter = new NavMeshQueryFilter
            {
                agentTypeID = currentAgent.agentTypeID,
                areaMask = currentAgent.areaMask
            };
            if (!NavMesh.SamplePosition(position, out var hit, sampleRadius, filter))
            {
                return false;
            }

            if (candidatePath == null)
            {
                candidatePath = new NavMeshPath();
            }

            if (!currentAgent.CalculatePath(hit.position, candidatePath)
                || candidatePath.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            if (!currentAgent.SetPath(candidatePath))
            {
                return false;
            }

            currentAgent.isStopped = false;
            return true;
        }

        private void Update()
        {
            ValidateActivityOwner();
            ApplyPauseState();
            var mouse = Mouse.current;
            if (!InputEnabled || viewCamera == null || mouse == null
                || !mouse.leftButton.wasPressedThisFrame
                || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            {
                return;
            }

            var ray = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
            if(HouseSeatPresentation.TryPickNpc(gameObject.scene,ray,out var seatedGuest))
            {
                CameraRig?.FocusSubject(seatedGuest.transform);
                return;
            }
            if (!Physics.Raycast(ray, out var hit, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            if (hit.collider.GetComponentInParent<HousePlayerController>() == this)
            {
                isSelected = true;
                CameraRig?.FocusSubject(transform);
                return;
            }

            // Clicking a houseguest rides them. Previously this fell straight through to the walk
            // check, failed it because a person is not a walkable surface, and did nothing at all —
            // so the one gesture people try first had no effect and no feedback.
            var houseguest = hit.collider.GetComponentInParent<HouseNpc>();
            if (houseguest != null)
            {
                CameraRig?.FocusSubject(houseguest.transform);
                return;
            }

            var furniture=HouseFurniture.AtProp(gameObject.scene,hit.transform);
            if(furniture!=null && FurnitureSelected!=null)
            {FurnitureSelected(furniture);return;}

            if (isSelected && hit.collider.GetComponentInParent<HouseWalkable>() != null)
            {
                // Sending the player somewhere means you want to watch them go, not keep staring at
                // whoever you were following.
                CameraRig?.ClearSubject();
                TryMoveTo(hit.point);
            }
        }

        private void ApplyPauseState()
        {
            var currentAgent = Agent;
            bool mayMove = activityOwner != null ? !activityPaused : requestedInputEnabled;
            if (currentAgent != null && currentAgent.enabled && currentAgent.isOnNavMesh
                && currentAgent.isStopped == mayMove)
            {
                // isStopped preserves the path so closing dialogue can resume the same walk.
                currentAgent.isStopped = !mayMove;
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
