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

        /// <summary>How long the last accepted route was, in metres along the path.</summary>
        public float RouteMetres { get; private set; }

        /// <summary>
        /// Walking pace and running pace.
        ///
        /// <para>Walking matches the authored take and the houseguests' own 2.2 m/s. The scene's
        /// authored agent speed was 4, which is a run, and it is why everybody covered the house at
        /// a sprint whatever they were doing - the clip was a borrowed run and the speed agreed
        /// with it.</para>
        /// </summary>
        private const float WalkSpeed = 2.2f;
        private const float RunSpeed = 4f;

        /// <summary>Far enough to be worth running: about the length of the house's long side.</summary>
        private const float RunRouteMetres = 8f;

        private const float DoubleClickSeconds = .35f;
        private const float DoubleClickPixels = 24f;
        private float lastFloorClickTime = float.NegativeInfinity;
        private Vector2 lastFloorClickScreen;

        /// <summary>
        /// Whether this controller still owns the agent's speed.
        ///
        /// <para>Nine PlayMode fixtures raise the player to 20 or 25 m/s in setup so a whole season
        /// finishes inside a deadline. Writing a gait over that would turn every one of those walks
        /// into a timeout, so the first time the speed is found to be something this controller did
        /// not write, it stops writing it. The animator still hears the gait; only the speed is
        /// conceded.</para>
        /// </summary>
        private bool ownsSpeed = true;
        private float appliedSpeed = float.NaN;

        /// <summary>Whether the player is covering ground rather than crossing a room.</summary>
        public bool IsRunning { get; private set; }

        private void ApplyGait(bool run)
        {
            IsRunning = run;
            var visual = GetComponent<Gamesim.Presentation.CharacterPresentation>();
            if (visual != null) visual.SetRunning(run);

            var currentAgent = Agent;
            if (currentAgent == null) return;
            if (ownsSpeed && !float.IsNaN(appliedSpeed)
                && !Mathf.Approximately(currentAgent.speed, appliedSpeed)) ownsSpeed = false;
            if (!ownsSpeed) return;
            currentAgent.speed = run ? RunSpeed : WalkSpeed;
            appliedSpeed = currentAgent.speed;
        }
        public bool InputEnabled => requestedInputEnabled && activityOwner == null;
        public bool IsSelected => isSelected;
        public NavMeshAgent Agent => agent != null ? agent : agent = GetComponent<NavMeshAgent>();
        public event System.Action<HouseInteractionAnchor> FurnitureSelected;

        /// <summary>
        /// A click on a houseguest.
        ///
        /// <para>Raised rather than acted on here, for the same reason the furniture click is: this
        /// controller knows where the mouse landed and nothing about whether a conversation is
        /// allowed, whose turn it is or what the season thinks. With nobody listening the click
        /// falls back to following them, which is what it did for its whole life before there was
        /// anywhere else for it to go.</para>
        /// </summary>
        public event System.Action<HouseNpc> HouseguestSelected;

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
            // Walking is the default gait. The scene authored this agent at 4 m/s, which is a run.
            ApplyGait(false);
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

        /// <summary>
        /// Go there on an errand, at the gait the route deserves: a walk across a room, a run
        /// across the house.
        ///
        /// <para>The floor click has always chosen its gait this way; the buttons that send the
        /// player somewhere did not, and inherited whatever the last move left behind. After a
        /// run - a chase cut short, a long floor click - that was a run to a screen three metres
        /// away, and after anything else it was a stroll from the far end of the yard.</para>
        /// </summary>
        public bool TryTravelTo(Vector3 position)
        {
            if (!TryMoveTo(position)) return false;
            ApplyGait(RouteMetres > RunRouteMetres);
            return true;
        }

        /// <summary>
        /// Go there at a run, whatever the distance, and keep running.
        ///
        /// <para>Chasing is not the same problem as travelling, and deciding the gait from the
        /// route length gets it exactly backwards. A houseguest walks at 2.2 m/s and so does the
        /// player, so a chase at walking pace never converges at all; and a chase that picks its
        /// gait from the REMAINING route drops back to a walk the moment that route falls under
        /// <see cref="RunRouteMetres"/> - which is to say precisely when the gap still has eight
        /// metres to close. The player then paces the target forever at eight metres and the walk
        /// times out. So while the player is chasing somebody, they run.</para>
        /// </summary>
        public bool TryRunTo(Vector3 position)
        {
            if (!TryMoveTo(position)) return false;
            ApplyGait(true);
            return true;
        }

        /// <summary>
        /// Stop here and stand still, at a walk.
        ///
        /// <para>Disabling input pauses an agent without forgetting where it was going, which is
        /// what makes a paused walk resume when a panel closes. That is right for a panel the player
        /// opened mid-walk and wrong for the walk that CAUSED the panel: arriving to talk to
        /// somebody and then wandering two metres past them the moment the conversation ends is not
        /// a resumption, it is the leftovers of an errand already run.</para>
        /// </summary>
        public void StopHere()
        {
            var currentAgent = Agent;
            if (currentAgent != null && currentAgent.enabled && currentAgent.isOnNavMesh)
                currentAgent.ResetPath();
            RouteMetres = 0f;
            ApplyGait(false);
        }

        /// <summary>
        /// Past this many metres of route, an errand is not a trip worth watching: the player is
        /// simply there (<see cref="TryWarpTo"/>). About two rooms and a corridor - the kitchen to
        /// the bedroom is a run, the game room to the yard is not.
        /// </summary>
        public const float WarpRouteMetres = 20f;

        /// <summary>
        /// How far it is to walk there, by the route rather than the crow's flight, without going.
        /// False when there is no complete route, or no floor near enough to the point.
        /// </summary>
        public bool TryMeasureRoute(Vector3 position, out float metres)
            => TryPlanPath(position, destinationSampleRadius, out _, out metres);

        /// <summary>
        /// Puts the player at <paramref name="position"/> now: the far end of an errand, with no
        /// trip in between.
        ///
        /// <para>Refused while anything else owns the player's movement - an activity, the diary
        /// chair, the competition staging - exactly as a walk is, and refused where no route
        /// reaches: somewhere the player could not walk to is somewhere they cannot be put. The
        /// agent's speed is left alone; whatever it was, it still is.</para>
        /// </summary>
        public bool TryWarpTo(Vector3 position)
        {
            if (!InputEnabled || !TryPlanPath(position, destinationSampleRadius, out var landing, out _)) return false;
            var currentAgent = Agent;
            if (!currentAgent.Warp(landing)) return false;
            currentAgent.ResetPath();
            currentAgent.velocity = Vector3.zero;
            RouteMetres = 0f;
            IsRunning = false;
            var visual = GetComponent<Gamesim.Presentation.CharacterPresentation>();
            if (visual != null) visual.SetRunning(false);
            Physics.SyncTransforms();
            return true;
        }

        /// <summary>
        /// A complete route to the nearest walkable point, left in <see cref="candidatePath"/>.
        /// </summary>
        private bool TryPlanPath(Vector3 position, float sampleRadius, out Vector3 landing, out float metres)
        {
            landing = default;
            metres = 0f;
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

            // The route, not the crow's flight: a destination three metres away through two
            // doorways is a long walk, and that is the distinction "far" has to make.
            var corners = candidatePath.corners;
            for (int i = 0; i + 1 < corners.Length; i++)
                metres += Vector3.Distance(corners[i], corners[i + 1]);
            landing = hit.position;
            return true;
        }

        private bool TrySetReachablePath(Vector3 position,float sampleRadius)
        {
            if (!TryPlanPath(position, sampleRadius, out _, out float metres)) return false;
            var currentAgent = Agent;
            if (!currentAgent.SetPath(candidatePath))
            {
                return false;
            }

            RouteMetres = metres;
            currentAgent.isStopped = false;
            return true;
        }

        /// <summary>
        /// A click on the house while an activity owns the player's movement, with no panel open.
        ///
        /// <para>Raised instead of acted on: lying on a bed or swimming a length, the player is
        /// still in the house, and clicking somewhere else in it is the plainest way to say "get
        /// up". The owner lets go - with the get-up it has - and hands the same click back through
        /// <see cref="DispatchClick"/>, so one click gets the player up and takes them there.</para>
        /// </summary>
        public event System.Action<Ray, Vector2> ActivityInterruptRequested;

        /// <summary>
        /// The piece of furniture under the pointer, or null, and where the pointer is. Raised as
        /// the pointer moves, so whoever listens can say what a click there would do before it is
        /// made. The same pick the click makes, so the words and the click cannot disagree.
        /// </summary>
        public event System.Action<HouseInteractionAnchor, Vector2> FurnitureHovered;
        private Vector2 lastHover = new Vector2(float.NaN, float.NaN);

        private void TickHover(Mouse mouse)
        {
            if (FurnitureHovered == null || viewCamera == null || mouse == null) return;
            var screen = mouse.position.ReadValue();
            if (screen == lastHover) return;
            lastHover = screen;
            HouseInteractionAnchor under = null;
            if (!Gamesim.Presentation.CeremonyOverlays.OnScreen
                && !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                && Physics.Raycast(ScreenRay(viewCamera, screen), out var hit, 500f, HouseLayers.Pick, QueryTriggerInteraction.Ignore))
                under = HouseFurniture.AtProp(gameObject.scene, hit.transform, hit.point);
            FurnitureHovered(under, screen);
        }

        private void Update()
        {
            ValidateActivityOwner();
            ApplyPauseState();
            var mouse = Mouse.current;
            TickHover(mouse);
            if (viewCamera == null || mouse == null
                || !mouse.leftButton.wasPressedThisFrame
                // A ceremony card is near-opaque and takes no input by design, so the click that
                // dismisses one is still unclaimed when it arrives here - and the house is directly
                // underneath. Clicking a card you cannot see through used to walk the player to
                // whatever floor was behind it.
                || Gamesim.Presentation.CeremonyOverlays.OnScreen
                || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            {
                return;
            }

            var screen = mouse.position.ReadValue();
            if (!InputEnabled)
            {
                if (activityOwner != null && requestedInputEnabled && ActivityInterruptRequested != null)
                    ActivityInterruptRequested(ScreenRay(viewCamera, screen), screen);
                return;
            }
            DispatchClick(ScreenRay(viewCamera, screen), screen);
        }

        /// <summary>
        /// The ray under a screen point, through the projection the frame is actually drawn with.
        ///
        /// <para><c>Camera.ScreenPointToRay</c> builds its ray from the camera's field of view and
        /// takes no account of a projection matrix set by hand - and the overview sets one, a blend
        /// from perspective to orthographic. Under it, a click on the kitchen floor walked the
        /// player to a patch of house somewhere else. Inverting the projection and view the frame
        /// was rendered through gives the ray the click actually lies on, whatever the lens.</para>
        /// </summary>
        public static Ray ScreenRay(Camera camera, Vector2 screen)
        {
            var frame = camera.pixelRect;
            float x = (screen.x - frame.x) / Mathf.Max(1f, frame.width) * 2f - 1f;
            float y = (screen.y - frame.y) / Mathf.Max(1f, frame.height) * 2f - 1f;
            // Unity's projection matrix is always the OpenGL convention, near plane at z = -1.
            var inverse = (camera.projectionMatrix * camera.worldToCameraMatrix).inverse;
            var near = inverse.MultiplyPoint(new Vector3(x, y, -1f));
            var far = inverse.MultiplyPoint(new Vector3(x, y, 1f));
            var along = far - near;
            return along.sqrMagnitude > 1e-8f ? new Ray(near, along.normalized) : camera.ScreenPointToRay(screen);
        }

        /// <summary>
        /// Does what a click on the house does: picks a houseguest, a piece of furniture or the
        /// player, or walks to the floor. Refused, and nothing done, while input is off.
        /// </summary>
        public void DispatchClick(Ray ray, Vector2 screen)
        {
            if (!InputEnabled) return;
            if(HouseSeatPresentation.TryPickNpc(gameObject.scene,ray,out var seatedGuest))
            {
                if (!Select(seatedGuest)) CameraRig?.FocusSubject(seatedGuest.transform);
                return;
            }
            // Pick, not Sight: a click is meant to hit the thing under the cursor, furniture included.
            if (!Physics.Raycast(ray, out var hit, 500f, HouseLayers.Pick, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            if (hit.collider.GetComponentInParent<HousePlayerController>() == this)
            {
                isSelected = true;
                CameraRig?.FocusSubject(transform);
                return;
            }

            // Clicking a houseguest talks to them. It used to fall straight through to the walk
            // check, fail it because a person is not a walkable surface, and do nothing at all; then
            // it followed them with the camera, which was better but still not what anyone clicking
            // on a person is asking for. Following is what the cast strip's own portraits do.
            var houseguest = hit.collider.GetComponentInParent<HouseNpc>();
            if (houseguest != null)
            {
                if (!Select(houseguest)) CameraRig?.FocusSubject(houseguest.transform);
                return;
            }

            var furniture=HouseFurniture.AtProp(gameObject.scene,hit.transform,hit.point);
            if(furniture!=null && FurnitureSelected!=null)
            {FurnitureSelected(furniture);return;}

            if (isSelected && hit.collider.GetComponentInParent<HouseWalkable>() != null)
            {
                // Sending the player somewhere means you want to watch them go, not keep staring at
                // whoever you were following. This used to say that and then call ClearSubject(),
                // which only does the second half - it stops following and, by its own summary,
                // "leaves the camera exactly where it is". The player then walked out of a frozen
                // frame and you had to chase them by hand. Follow them instead, without reframing:
                // the shot you were looking at is the shot you keep.
                // Walk unless it is worth running: a second click on the same spot means hurry,
                // and a route long enough to cross the house is a run whether or not you asked.
                // The first click is acted on immediately either way - a double-click that waited
                // to see whether a second was coming would put a delay on every move in the game.
                bool again = Time.unscaledTime - lastFloorClickTime <= DoubleClickSeconds
                    && (screen - lastFloorClickScreen).sqrMagnitude <= DoubleClickPixels * DoubleClickPixels;
                lastFloorClickTime = Time.unscaledTime;
                lastFloorClickScreen = screen;

                CameraRig?.FocusSubject(transform, false);
                if (TryMoveTo(hit.point))
                {
                    ApplyGait(again || RouteMetres > RunRouteMetres);
                    // Raised only once the move is actually taken. A click on a patch of floor with
                    // no route to it changes nothing the player can see, and an errand let go of on
                    // the strength of a move that never happened would leave them standing where
                    // they already were with nothing to show for it - the errand cancelled and no
                    // destination in its place. The houseguest click keeps the same rule.
                    DestinationChosen?.Invoke();
                }
            }
        }

        /// <summary>
        /// The player chose their own destination.
        ///
        /// <para>Raised so that whoever sent them on an errand can let go of it. Clicking the floor
        /// is the plainest possible statement that the player wants to be somewhere else, and an
        /// errand that outlives it drags them back.</para>
        /// </summary>
        public event System.Action DestinationChosen;

        /// <summary>Hands a clicked houseguest to whoever is listening; false when nobody is.</summary>
        private bool Select(HouseNpc npc)
        {
            if (npc == null || HouseguestSelected == null) return false;
            HouseguestSelected(npc);
            return true;
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
