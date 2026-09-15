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
    public sealed class HousePlayerController : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private bool isSelected = true;
        [SerializeField, Min(0.05f)] private float destinationSampleRadius = 0.6f;

        private NavMeshAgent agent;
        private NavMeshPath candidatePath;

        public bool InputEnabled { get; private set; } = true;
        public bool IsSelected => isSelected;
        public NavMeshAgent Agent => agent != null ? agent : agent = GetComponent<NavMeshAgent>();

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
                InputEnabled = false;
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
            InputEnabled = enabled;
            ApplyPauseState();
        }

        /// <summary>
        /// Accepts only reachable destinations. Failed commands leave the current path intact.
        /// </summary>
        public bool TryMoveTo(Vector3 position)
        {
            var currentAgent = Agent;
            if (!InputEnabled || currentAgent == null || !currentAgent.enabled || !currentAgent.isOnNavMesh
                || !IsFinite(position))
            {
                return false;
            }

            var filter = new NavMeshQueryFilter
            {
                agentTypeID = currentAgent.agentTypeID,
                areaMask = currentAgent.areaMask
            };
            if (!NavMesh.SamplePosition(position, out var hit, destinationSampleRadius, filter))
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
            ApplyPauseState();
            var mouse = Mouse.current;
            if (!InputEnabled || viewCamera == null || mouse == null
                || !mouse.leftButton.wasPressedThisFrame
                || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            {
                return;
            }

            var ray = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            if (hit.collider.GetComponentInParent<HousePlayerController>() == this)
            {
                isSelected = true;
                return;
            }

            if (isSelected && hit.collider.GetComponentInParent<HouseWalkable>() != null)
            {
                TryMoveTo(hit.point);
            }
        }

        private void ApplyPauseState()
        {
            var currentAgent = Agent;
            if (currentAgent != null && currentAgent.enabled && currentAgent.isOnNavMesh
                && currentAgent.isStopped == InputEnabled)
            {
                // isStopped preserves the path so closing dialogue can resume the same walk.
                currentAgent.isStopped = !InputEnabled;
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
