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
        public bool ReducedMotion => reducedMotion;

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

            var blend = reducedMotion ? 1f : 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredFocus, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(pitch, yaw, 0f), blend);
            distance = Mathf.Lerp(distance, desiredDistance, blend);
            viewCamera.transform.localPosition = new Vector3(0f, 0f, -distance);
            viewCamera.transform.localRotation = Quaternion.identity;
        }

        private void ReadInput()
        {
            if (!ControlsEnabled) return;
            var mouse = Mouse.current;
            bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (mouse != null && !pointerOverUi)
            {
                if (mouse.rightButton.isPressed)
                {
                    var delta = mouse.delta.ReadValue();
                    yaw = Mathf.Repeat(yaw + delta.x * orbitSensitivity, 360f);
                    pitch = Mathf.Clamp(pitch - delta.y * orbitSensitivity, 45f, 70f);
                }

                // Wheel deltas do not arrive in one unit. Windows reports 120 per notch, and other
                // Input System backends normalise to about 1. A single multiplier tuned for the
                // first is a hundredfold too small for the second — which is exactly what the wheel
                // did here: every notch moved the camera a eightieth of a metre, so the control the
                // on-screen hint advertised looked completely dead. Fold both onto a notch count
                // before applying the step, and the same code feels right either way.
                float wheel = mouse.scroll.ReadValue().y;
                if (!Mathf.Approximately(wheel, 0f))
                {
                    float notches = Mathf.Abs(wheel) >= 20f ? wheel / 120f : wheel;
                    desiredDistance = Mathf.Clamp(desiredDistance - notches * zoomStep,
                        minimumDistance, maximumDistance);
                }
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.fKey.wasPressedThisFrame && playerTarget != null)
            {
                ClearSubject();
                desiredFocus = ClampFocus(new Vector3(playerTarget.position.x, houseCenter.y, playerTarget.position.z));
            }

            var movement = Vector2.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) movement.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) movement.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) movement.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) movement.y += 1f;
            if (movement.sqrMagnitude > 0f)
            {
                ClearSubject();
                movement.Normalize();
                var direction = Quaternion.Euler(0f, yaw, 0f) * new Vector3(movement.x, 0f, movement.y);
                desiredFocus = ClampFocus(desiredFocus + direction * (panSpeed * desiredDistance / 24f) * Time.unscaledDeltaTime);
            }
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
            desiredFocus = houseCenter;
            transform.position = desiredFocus;
            ApplyCameraImmediately();
        }

        private void ApplyCameraImmediately()
        {
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            if (ViewCamera != null)
            {
                viewCamera.transform.localPosition = new Vector3(0f, 0f, -distance);
                viewCamera.transform.localRotation = Quaternion.identity;
            }
        }
    }
}
