using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>Directions are fresh physical presses, never the UI module's held-navigation repeats.</summary>
    public sealed class CompetitionDirectionControl : Selectable, IPointerClickHandler
    {
        public Action<MiniGameRun.Direction> Pressed;
        public Action Missed;
        private int selectedFrame = -1;

        public override void OnSelect(BaseEventData data)
        {
            base.OnSelect(data); selectedFrame = Time.frameCount;
        }

        private void Update()
        {
            if (!IsActive() || !IsInteractable() || Time.frameCount <= selectedFrame || EventSystem.current == null
                || EventSystem.current.currentSelectedGameObject != gameObject) return;
            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
            // The arrows and WASD, the D-pad and the left stick: each a fresh press, never a hold.
            if ((keyboard != null && (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame))
                || (pad != null && (pad.dpad.up.wasPressedThisFrame || pad.leftStick.up.wasPressedThisFrame)))
                Pressed?.Invoke(MiniGameRun.Direction.Up);
            else if ((keyboard != null && (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame))
                || (pad != null && (pad.dpad.right.wasPressedThisFrame || pad.leftStick.right.wasPressedThisFrame)))
                Pressed?.Invoke(MiniGameRun.Direction.Right);
            else if ((keyboard != null && (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame))
                || (pad != null && (pad.dpad.down.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame)))
                Pressed?.Invoke(MiniGameRun.Direction.Down);
            else if ((keyboard != null && (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame))
                || (pad != null && (pad.dpad.left.wasPressedThisFrame || pad.leftStick.left.wasPressedThisFrame)))
                Pressed?.Invoke(MiniGameRun.Direction.Left);
        }

        public override void OnMove(AxisEventData data)
        {
            if (!IsActive() || !IsInteractable()) return;
            data.Use();
        }

        /// <summary>Where the last click on the board landed, for the mark the board leaves there.</summary>
        public Vector2 LastClick { get; private set; }
        public Camera LastClickCamera { get; private set; }
        public bool HasClick { get; private set; }

        public void OnPointerClick(PointerEventData data)
        {
            if (!IsActive() || !IsInteractable() || data.button != PointerEventData.InputButton.Left) return;
            LastClick = data.position; LastClickCamera = data.pressEventCamera; HasClick = true;
            Missed?.Invoke();
        }
    }
}
