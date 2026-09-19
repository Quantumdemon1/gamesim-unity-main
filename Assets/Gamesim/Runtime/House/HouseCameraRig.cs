using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Gamesim.House
{
    /// <summary>Bounded dollhouse camera with orbit, zoom, pan, and conversation framing.</summary>
    [DisallowMultipleComponent]
    public sealed class HouseCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform playerTarget;
        [SerializeField] private Vector3 houseCenter = new Vector3(0f, 1.2f, 0f);
        [SerializeField] private Vector2 panHalfExtents = new Vector2(12f, 10f);
        [SerializeField, Range(45f, 70f)] private float pitch = 55f;
        [SerializeField] private float yaw = 45f;
        [SerializeField, Min(1f)] private float distance = 24f;
        // Four metres, not ten. Ten is still a room-wide shot: it cannot frame a face, so "zoom in
        // on a houseguest" had no setting that would do it even when the wheel was working.
        [SerializeField, Min(1f)] private float minimumDistance = 4f;
        [SerializeField, Min(1f)] private float maximumDistance = 34f;
        [SerializeField, Min(0.01f)] private float orbitSensitivity = 0.18f;
        [SerializeField, Min(0.01f)] private float panSpeed = 8f;
        [SerializeField, Min(0.01f)] private float smoothing = 10f;
        [SerializeField, Min(0.1f)] private float zoomStep = 2f;
        [SerializeField, Min(1f)] private float subjectDistance = 7f;
        [SerializeField] private float subjectHeight = 1.1f;
        // Phase 1 (MASTER-PLAN §3.E). The pitch follows the distance: steep from far away, where
        // the whole house has to read as a plan, shallower close in, where a face has to. A
        // right-drag sets an offset on top of that rather than an absolute angle, so zooming
        // never fights the orbit and the orbit survives the zoom.
        [SerializeField, Range(45f, 70f)] private float closePitch = 45f;
        [SerializeField, Range(45f, 70f)] private float farPitch = 62f;
        private float pitchOffset;
        // Occlusion: a wall or prop between the camera and its focus shortens the boom for the
        // frame and lets it back out when clear. It changes what is applied, never the distance
        // the player asked for, so nothing that waits on the rig arriving is affected.
        [SerializeField, Min(0f)] private float occlusionRadius = 0.3f;
        [SerializeField, Min(0f)] private float occlusionMargin = 0.25f;
        private float appliedDistance;
        private static readonly RaycastHit[] occlusionHits = new RaycastHit[16];
        // Phase 3 (MASTER-PLAN §3.E). Every control arrives through one Input Actions map, so a
        // mouse, the keyboard and a gamepad are the same code path and a test drives any of them
        // by queueing device state. Nothing assigned means the map built in code.
        [SerializeField] private InputActionAsset actionsAsset;
        [SerializeField, Min(1f)] private float gamepadOrbitSpeed = 120f;
        [SerializeField, Min(0.1f)] private float gamepadZoomSpeed = 8f;
        [SerializeField, Min(1f)] private float edgePanBand = 12f;
        [SerializeField] private bool edgePan = true;
        private HouseCameraActions actions;

        private Vector3 desiredFocus;
        private float desiredDistance;
        private Vector3 previousFocus;
        private float previousDistance;
        private Transform conversationPlayer;
        private Transform conversationNpc;
        private Transform subject;
        private bool initialized;
        private bool reducedMotion;

        public Camera ViewCamera
        {
            get
            {
                if (viewCamera == null)
                {
                    viewCamera = GetComponentInChildren<Camera>(true);
                }

                return viewCamera;
            }
        }

        public bool IsConversationFocused { get; private set; }
        public bool IsSubjectFocused => subject != null;
        public Transform FocusedSubject => subject;
        public bool ControlsEnabled { get; set; } = true;
        public float Pitch => pitch;
        public float Distance => distance;
        public float DesiredDistance => desiredDistance;
        public Vector3 DesiredFocus => desiredFocus;
        /// <summary>The boom length actually applied this frame, after occlusion.</summary>
        public float AppliedDistance => appliedDistance;
        /// <summary>The pitch the rig chooses for a distance before any orbit offset.</summary>
        public float PitchFor(float wantedDistance) =>
            Mathf.Lerp(closePitch, farPitch, Mathf.InverseLerp(minimumDistance, maximumDistance, wantedDistance));
        public bool ReducedMotion => reducedMotion;

        /// <summary>The rig's actions, for whoever else reads them - the director, for the shoulders.</summary>
        public HouseCameraActions Actions { get { EnsureActions(); return actions; } }

        /// <summary>
        /// Whether the screen's edges pan. On by default in a full-screen window, where the cursor
        /// cannot leave; in a window the cursor leaves through the very band that would pan, and
        /// the last position it reported sits inside it, so a windowed player has to opt in.
        /// </summary>
        public bool EdgePan { get => edgePan; set => edgePan = value; }
        public bool EdgePanInWindow { get; set; }
        public float EdgePanBand => edgePanBand;
        private bool EdgePanActive => edgePan && (Screen.fullScreen || EdgePanInWindow);

        private void EnsureActions()
        {
            if (actions != null) return;
            actions = new HouseCameraActions(actionsAsset);
            if (isActiveAndEnabled) actions.Enable();
        }

        private void OnEnable() { EnsureActions(); actions.Enable(); }
        private void OnDisable() { actions?.Disable(); }
        private void OnDestroy() { actions?.Dispose(); actions = null; }

        private void Awake()
        {
            Initialize();
        }

        public void Configure(Transform target)
        {
            playerTarget = target;
            Initialize();
            desiredFocus = houseCenter;
            transform.position = desiredFocus;
            ApplyCameraImmediately();
        }

        public void ConfigureBounds(Vector3 center, Vector2 halfExtents)
        {
            houseCenter = center;
            panHalfExtents = new Vector2(Mathf.Max(0f, halfExtents.x), Mathf.Max(0f, halfExtents.y));
            desiredFocus = ClampFocus(center);
            if (!IsConversationFocused)
            {
                transform.position = desiredFocus;
            }
        }

        /// <summary>
        /// Keeps dialogue at the current view and removes camera easing. Explicit camera input
        /// remains available outside dialogue; this preference never changes navigation or state.
        /// </summary>
        public void SetReducedMotion(bool value)
        {
            Initialize();
            if (reducedMotion == value) return;
            reducedMotion = value;
            if (value)
            {
                // Reduced motion stops every reframe in flight, the pitch's included: whatever angle
                // the camera is actually at becomes the offset, so the next frame changes nothing.
                float actual = transform.rotation.eulerAngles.x;
                if (actual > 180f) actual -= 360f;
                pitchOffset = actual - PitchFor(distance);
            }
            if (value)
            {
                // Freeze any in-flight automatic framing at the currently visible position.
                desiredFocus = transform.position;
                desiredDistance = distance;
            }
            else if (IsConversationFocused && conversationPlayer != null && conversationNpc != null)
            {
                desiredFocus = ConversationFocus();
                desiredDistance = Mathf.Clamp(12f, minimumDistance, maximumDistance);
            }
        }

        public void SetConversationFocus(Transform player, Transform npc)
        {
            Initialize();
            if (player == null || npc == null)
            {
                return;
            }

            if (!IsConversationFocused)
            {
                previousFocus = desiredFocus;
                previousDistance = desiredDistance;
            }

            conversationPlayer = player;
            conversationNpc = npc;
            IsConversationFocused = true;
            if (reducedMotion) return;
            desiredFocus = ConversationFocus();
            desiredDistance = Mathf.Clamp(12f, minimumDistance, maximumDistance);
        }

        /// <summary>
        /// Rides a houseguest: frames them close and follows them until something releases it.
        ///
        /// <para>Orbit and zoom stay live while a subject is held, because turning around a person
        /// and leaning in on them is the whole point. Panning releases, because a pan asks to look
        /// somewhere else and fighting the follow every frame would feel broken.</para>
        ///
        /// <para>Unlike conversation framing this is honoured under reduced motion. A viewer who
        /// clicked a houseguest asked for this shot, so suppressing it would withhold a result they
        /// requested; reduced motion removes the easing, not the outcome.</para>
        /// </summary>
        public void FocusSubject(Transform target)
        {
            Initialize();
            if (target == null)
            {
                ClearSubject();
                return;
            }

            subject = target;
            if (IsConversationFocused) return;
            desiredFocus = SubjectFocus();
            desiredDistance = Mathf.Clamp(subjectDistance, minimumDistance, maximumDistance);
        }

        /// <summary>
        /// Stops following, and leaves the camera exactly where it is.
        ///
        /// <para>It deliberately does not spring back to where the view was before. Snapping the
        /// shot away the moment you stop following reads as the camera being yanked; F recenters on
        /// the player when that is actually what someone wants.</para>
        /// </summary>
        public void ClearSubject()
        {
            if (subject == null) return;
            subject = null;
            desiredFocus = ClampFocus(transform.position);
            desiredDistance = distance;
        }

        /// <summary>
        /// Points the camera at somewhere in particular, for a scripted move.
        ///
        /// <para>A waypoint rather than a path: the caller walks the list and this eases toward each
        /// one using the same smoothing every other automatic move uses, so a scripted sweep and a
        /// conversation framing look like the same camera. The opening walk-in is the only caller
        /// today.</para>
        ///
        /// <para>Callers turn <see cref="ControlsEnabled"/> off for the duration. Leaving it on does
        /// not break anything, but a key held down would fight the sweep every frame and the player
        /// would be shown a camera that appears to be struggling.</para>
        ///
        /// <para>Under reduced motion the blend is already one, so the move lands immediately. That
        /// is the intended behaviour and not a degradation: the preference removes the sweep, not the
        /// place it was going.</para>
        /// </summary>
        public void MoveTo(Vector3 focus, float wantedDistance)
        {
            Initialize();
            ClearSubject();
            desiredFocus = ClampFocus(focus);
            desiredDistance = Mathf.Clamp(wantedDistance, minimumDistance, maximumDistance);
        }

        /// <summary>Whether the last <see cref="MoveTo"/> has effectively landed.</summary>
        public bool HasArrived(float tolerance = 0.35f) =>
            (transform.position - desiredFocus).sqrMagnitude <= tolerance * tolerance
            && Mathf.Abs(distance - desiredDistance) <= tolerance;

        /// <summary>Where the house is centred and how far the camera may pull back, for a caller
        /// composing a scripted move without having to know the rig's serialized fields.</summary>
        public Vector3 HouseCenter => houseCenter;
        public float FarthestDistance => maximumDistance;
        public float NearestDistance => minimumDistance;

        public void EndConversation()
        {
            if (!IsConversationFocused)
            {
                return;
            }

            IsConversationFocused = false;
            conversationPlayer = null;
            conversationNpc = null;
            if (reducedMotion)
            {
                desiredFocus = transform.position;
                desiredDistance = distance;
                return;
            }
            desiredFocus = ClampFocus(previousFocus);
            desiredDistance = Mathf.Clamp(previousDistance, minimumDistance, maximumDistance);
        }

        private void LateUpdate()
        {
            Initialize();
            if (ViewCamera == null)
            {
                return;
            }

            if (IsConversationFocused)
            {
                if (conversationPlayer == null || conversationNpc == null)
                {
                    EndConversation();
                }
                else if (!reducedMotion)
                {
                    desiredFocus = ConversationFocus();
                }
            }
            else
            {
                // Input first: panning releases the subject, and it has to do so before the follow
                // below would overwrite the pan it just asked for.
                ReadInput();

                if (subject != null)
                {
                    if (!subject.gameObject.activeInHierarchy) ClearSubject();
                    else desiredFocus = SubjectFocus();
                }
            }

            pitch = Mathf.Clamp(PitchFor(desiredDistance) + pitchOffset, 45f, 70f);
            var blend = reducedMotion ? 1f : 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredFocus, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(pitch, yaw, 0f), blend);
            distance = Mathf.Lerp(distance, desiredDistance, blend);
            appliedDistance = OccludedDistance(distance);
            viewCamera.transform.localPosition = new Vector3(0f, 0f, -appliedDistance);
            viewCamera.transform.localRotation = Quaternion.identity;
        }

        /// <summary>Where a screen point lands on the focus plane, for zooming toward the cursor.</summary>
        private bool TryCursorPoint(Vector2 screen, out Vector3 point)
        {
            point = desiredFocus;
            if (viewCamera == null) return false;
            var ray = viewCamera.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0f, desiredFocus.y, 0f));
            if (!plane.Raycast(ray, out float enter) || enter <= 0f) return false;
            point = ray.GetPoint(enter);
            return true;
        }

        /// <summary>
        /// The boom shortened to just in front of whatever stands between the focus and the camera.
        /// The tracked houseguests never count: the camera rides them, it does not hide from them.
        /// </summary>
        private float OccludedDistance(float wanted)
        {
            // Nothing nearer the focus than the player could zoom to counts: the sofa the focus
            // sits beside, or a low wall, must never yank the camera onto the floor. The pull-in
            // stops at that same limit, so the camera is never closer than the player could ask.
            float skip = minimumDistance;
            if (occlusionRadius <= 0f || wanted <= skip) return wanted;
            var back = transform.rotation * Vector3.back;
            int count = Physics.SphereCastNonAlloc(transform.position + back * skip, occlusionRadius, back,
                occlusionHits, wanted - skip, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = wanted;
            for (int i = 0; i < count; i++)
            {
                var hit = occlusionHits[i];
                if (hit.distance <= 0f || IsTracked(hit.transform)) continue;
                nearest = Mathf.Min(nearest, skip + hit.distance - occlusionMargin);
            }
            return Mathf.Max(Mathf.Min(wanted, nearest), skip);
        }

        private bool IsTracked(Transform hit)
        {
            if (playerTarget != null && hit.IsChildOf(playerTarget)) return true;
            if (subject != null && hit.IsChildOf(subject)) return true;
            if (conversationNpc != null && hit.IsChildOf(conversationNpc)) return true;
            return hit.GetComponentInParent<HouseNpc>() != null || hit.GetComponentInParent<HousePlayerController>() != null;
        }

        private void ReadInput()
        {
            if (!ControlsEnabled) return;
            EnsureActions();
            float dt = Time.unscaledDeltaTime;
            bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            var pointer = actions.Point.ReadValue<Vector2>();
            if (!pointerOverUi)
            {
                var orbit = actions.Orbit.ReadValue<Vector2>();
                if (orbit.sqrMagnitude > 0f) ApplyOrbit(orbit * orbitSensitivity);

                // Middle-drag: the ground that was under the cursor stays under it. The point under
                // the cursor a frame ago and the point under it now differ by exactly the move the
                // focus has to make, on the same plane the zoom uses.
                var drag = actions.Drag.ReadValue<Vector2>();
                if (drag.sqrMagnitude > 0f && TryCursorPoint(pointer - drag, out var was) && TryCursorPoint(pointer, out var now))
                {
                    ClearSubject();
                    desiredFocus = ClampFocus(desiredFocus + (was - now));
                }

                // Wheel deltas do not arrive in one unit. Windows reports 120 per notch, and other
                // Input System backends normalise to about 1. A single multiplier tuned for the
                // first is a hundredfold too small for the second — which is exactly what the wheel
                // did here: every notch moved the camera a eightieth of a metre, so the control the
                // on-screen hint advertised looked completely dead. Fold both onto a notch count
                // before applying the step, and the same code feels right either way.
                float wheel = actions.Zoom.ReadValue<float>();
                if (!Mathf.Approximately(wheel, 0f))
                {
                    float notches = Mathf.Abs(wheel) >= 20f ? wheel / 120f : wheel;
                    ZoomBy(notches * zoomStep, pointer, true);
                }

                if (EdgePanActive && Pointer.current != null)
                {
                    var edge = EdgeDirection(pointer);
                    if (edge.sqrMagnitude > 0f) PanBy(edge, dt);
                }
            }

            // A stick is a rate: degrees, metres and pan per second, times the frame - so its speed
            // is the same at any frame rate, which a per-frame mouse delta never has to be.
            var stick = actions.OrbitRate.ReadValue<Vector2>();
            if (stick.sqrMagnitude > 0f) ApplyOrbit(stick * (gamepadOrbitSpeed * dt));
            float zoomRate = actions.ZoomRate.ReadValue<float>();
            if (!Mathf.Approximately(zoomRate, 0f)) ZoomBy(zoomRate * gamepadZoomSpeed * dt, pointer, false);

            if (actions.Recenter.WasPressedThisFrame() && playerTarget != null)
            {
                ClearSubject();
                desiredFocus = ClampFocus(new Vector3(playerTarget.position.x, houseCenter.y, playerTarget.position.z));
            }

            var movement = actions.Pan.ReadValue<Vector2>();
            if (movement.sqrMagnitude > 0f) PanBy(Vector2.ClampMagnitude(movement, 1f), dt);
        }

        /// <summary>A drag in pixels, or a stick's share of a second, turned into yaw and tilt.</summary>
        private void ApplyOrbit(Vector2 amount)
        {
            yaw = Mathf.Repeat(yaw + amount.x, 360f);
            pitchOffset = Mathf.Clamp(pitchOffset - amount.y, -20f, 20f);
        }

        /// <summary>
        /// Shortens the boom by <paramref name="metres"/> (negative lengthens it). Toward the cursor
        /// when asked: the point under it on the focus plane draws the focus in by the fraction the
        /// boom shortened, so what was under the cursor stays under it; zooming out pushes it away by
        /// the same rule. A followed subject keeps the focus, and a stick has no cursor to zoom toward.
        /// </summary>
        private void ZoomBy(float metres, Vector2 pointer, bool towardCursor)
        {
            float before = desiredDistance;
            desiredDistance = Mathf.Clamp(desiredDistance - metres, minimumDistance, maximumDistance);
            if (towardCursor && subject == null && TryCursorPoint(pointer, out var under))
            {
                float fraction = 1f - desiredDistance / Mathf.Max(before, 0.001f);
                desiredFocus = ClampFocus(Vector3.LerpUnclamped(desiredFocus, under, fraction));
            }
        }

        /// <summary>Pans the focus along the camera's yaw. Panning releases a followed subject.</summary>
        private void PanBy(Vector2 direction, float dt)
        {
            ClearSubject();
            var flat = Quaternion.Euler(0f, yaw, 0f) * new Vector3(direction.x, 0f, direction.y);
            desiredFocus = ClampFocus(desiredFocus + flat * (panSpeed * desiredDistance / 24f) * dt);
        }

        /// <summary>
        /// Which way the screen's edge under the cursor pans, or zero away from every edge. A cursor
        /// outside the screen altogether counts as no edge: it is not on the game.
        /// </summary>
        public Vector2 EdgeDirection(Vector2 screen)
        {
            if (screen.x < 0f || screen.y < 0f || screen.x > Screen.width || screen.y > Screen.height) return Vector2.zero;
            var direction = Vector2.zero;
            if (screen.x <= edgePanBand) direction.x -= 1f;
            else if (screen.x >= Screen.width - edgePanBand) direction.x += 1f;
            if (screen.y <= edgePanBand) direction.y -= 1f;
            else if (screen.y >= Screen.height - edgePanBand) direction.y += 1f;
            return direction.normalized;
        }

        private Vector3 ConversationFocus()
        {
            return ClampFocus((conversationPlayer.position + conversationNpc.position) * 0.5f + Vector3.up);
        }

        /// <summary>
        /// Where the camera looks when riding a houseguest: their position, lifted to head height so
        /// a close shot frames a face rather than a pair of feet.
        /// </summary>
        private Vector3 SubjectFocus()
        {
            var focus = ClampFocus(subject.position);
            focus.y = subject.position.y + subjectHeight;
            return focus;
        }

        private Vector3 ClampFocus(Vector3 focus)
        {
            focus.x = Mathf.Clamp(focus.x, houseCenter.x - panHalfExtents.x, houseCenter.x + panHalfExtents.x);
            focus.z = Mathf.Clamp(focus.z, houseCenter.z - panHalfExtents.y, houseCenter.z + panHalfExtents.y);
            return focus;
        }

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            minimumDistance = Mathf.Max(1f, minimumDistance);
            maximumDistance = Mathf.Max(minimumDistance, maximumDistance);
            pitch = Mathf.Clamp(pitch, 45f, 70f);
            distance = Mathf.Clamp(distance, minimumDistance, maximumDistance);
            desiredDistance = distance;
            // The authored pitch is kept exactly, as the offset from what the distance would choose:
            // the first frame must not reframe a view somebody set up, reduced motion or not.
            pitchOffset = Mathf.Clamp(pitch - PitchFor(distance), -20f, 20f);
            desiredFocus = houseCenter;
            transform.position = desiredFocus;
            ApplyCameraImmediately();
        }

        private void ApplyCameraImmediately()
        {
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            if (ViewCamera != null)
            {
                appliedDistance = distance;
                viewCamera.transform.localPosition = new Vector3(0f, 0f, -distance);
                viewCamera.transform.localRotation = Quaternion.identity;
            }
        }
    }
}
