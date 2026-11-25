using System.Collections.Generic;
using Gamesim.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using FingerTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using Phase = UnityEngine.InputSystem.TouchPhase;

namespace Gamesim.House
{
    // Admit a gesture only if all its fingers began in the playable world. A menu may close
    // on release, so a raycast on the last frame alone cannot tell who owned the gesture.
    [DefaultExecutionOrder(-1200)] // The installed uGUI EventSystem processes its actions at -1000.
    [DisallowMultipleComponent]
    public sealed class HouseTouchInput : MonoBehaviour
    {
        private HousePlayerController player;
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private PointerEventData pointer;
        private Vector2 start, previous, pairCentre;
        private float pairDistance;
        private Finger first, second;
        private bool blocked, dragged, paired, suspended, unfocused;

        private void Awake() => player = GetComponent<HousePlayerController>();
        private void OnEnable() => EnhancedTouchSupport.Enable();
        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
            ResetGesture();
        }
        private void OnApplicationFocus(bool focused) { unfocused = !focused; if (unfocused) blocked = true; }
        private void OnApplicationPause(bool paused) { suspended = paused; if (paused) blocked = true; }

        internal bool HasContacts => isActiveAndEnabled && FingerTouch.activeTouches.Count > 0;

        private void ResetGesture()
        {
            first = second = null;
            blocked = dragged = paired = false;
        }

        private bool InWorld(FingerTouch touch)
        {
            var events = EventSystem.current;
            if (events == null || player == null || !player.TouchAllowed(touch.screenPosition, touch.displayIndex)) return false;
            if (pointer == null || pointer.currentInputModule != events.currentInputModule)
                pointer = new PointerEventData(events);
            pointer.position = touch.screenPosition;
            pointer.pointerId = touch.touchId;
            pointer.displayIndex = touch.displayIndex;
            hits.Clear();
            events.RaycastAll(pointer, hits);
            foreach (var hit in hits)
                if (hit.module is GraphicRaycaster) return false;
            return true;
        }

        // A quick UI press and release may dismiss a dialog in one EventSystem.Update.
        // Remember who owned the down while its UI is still present.
        private void Update()
        {
            var touches = FingerTouch.activeTouches;
            if (touches.Count == 0) { ResetGesture(); return; }
            if (touches.Count == 1 && touches[0].phase == Phase.Began) ResetGesture();
            if (suspended || unfocused || touches.Count > 2) { blocked = true; return; }
            for (int i = 0; i < touches.Count; i++)
                if (touches[i].phase == Phase.Began && !InWorld(touches[i])) blocked = true;
        }

        private void LateUpdate()
        {
            var touches = FingerTouch.activeTouches;
            if (touches.Count == 0) { ResetGesture(); return; }
            if (blocked || touches.Count > 2) { blocked = true; return; }
            for (int i = 0; i < touches.Count; i++)
                if (touches[i].phase == Phase.Canceled || !InWorld(touches[i])) { blocked = true; return; }

            var a = touches[0];
            if (touches.Count == 2)
            {
                var b = touches[1];
                if (a.phase == Phase.Ended || b.phase == Phase.Ended) { blocked = true; return; }
                if (first == null && (a.phase != Phase.Began || b.phase != Phase.Began)) { blocked = true; return; }
                if (first != null && first != a.finger && first != b.finger) { blocked = true; return; }
                var centre = (a.screenPosition + b.screenPosition) * .5f;
                float distance = Vector2.Distance(a.screenPosition, b.screenPosition);
                if (paired)
                {
                    if (second != a.finger && second != b.finger) { blocked = true; return; }
                    player.TouchCamera?.MoveWithFingers(pairCentre, centre, pairDistance, distance);
                }
                else
                {
                    var added = first == a.finger ? b : a;
                    if (added.phase != Phase.Began) { blocked = true; return; }
                    first = a.finger;
                    second = b.finger;
                    paired = true;
                }
                pairCentre = centre;
                pairDistance = distance;
                return;
            }

            if (paired) { blocked = true; return; }
            if (a.phase == Phase.Began)
            {
                first = a.finger;
                start = previous = a.screenPosition;
                return;
            }
            if (a.finger != first) return;
            float units = PhoneDisplay.PixelsPerUnit(Screen.width, Screen.height);
            if (!dragged && Vector2.Distance(start, a.screenPosition) >= units * 8f) dragged = true;
            if (dragged)
                player.TouchCamera?.OrbitWithFinger((a.screenPosition - previous) / units);
            else if (a.phase == Phase.Ended)
                player.HandleWorldPointer(a.screenPosition);
            previous = a.screenPosition;
            if (a.phase == Phase.Ended) blocked = true;
        }
    }
}
