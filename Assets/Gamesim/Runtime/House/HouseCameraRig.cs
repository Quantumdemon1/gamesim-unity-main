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
        [SerializeField, Min(1f)] private float minimumDistance = 10f;
        [SerializeField, Min(1f)] private float maximumDistance = 34f;
        [SerializeField, Min(0.01f)] private float orbitSensitivity = 0.18f;
        [SerializeField, Min(0.01f)] private float panSpeed = 8f;
        [SerializeField, Min(0.01f)] private float smoothing = 10f;

        private Vector3 desiredFocus;
        private float desiredDistance;
        private Vector3 previousFocus;
        private float previousDistance;
        private Transform conversationPlayer;
        private Transform conversationNpc;
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
                ReadInput();
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

                desiredDistance = Mathf.Clamp(desiredDistance - mouse.scroll.ReadValue().y * 0.0125f,
                    minimumDistance, maximumDistance);
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.fKey.wasPressedThisFrame && playerTarget != null)
            {
                desiredFocus = ClampFocus(new Vector3(playerTarget.position.x, houseCenter.y, playerTarget.position.z));
            }

            var movement = Vector2.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) movement.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) movement.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) movement.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) movement.y += 1f;
            if (movement.sqrMagnitude > 0f)
            {
                movement.Normalize();
                var direction = Quaternion.Euler(0f, yaw, 0f) * new Vector3(movement.x, 0f, movement.y);
                desiredFocus = ClampFocus(desiredFocus + direction * (panSpeed * desiredDistance / 24f) * Time.unscaledDeltaTime);
            }
        }

        private Vector3 ConversationFocus()
        {
            return ClampFocus((conversationPlayer.position + conversationNpc.position) * 0.5f + Vector3.up);
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
