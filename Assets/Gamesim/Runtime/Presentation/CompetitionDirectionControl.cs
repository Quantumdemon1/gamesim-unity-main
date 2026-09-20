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
            if ((keyboard != null && keyboard.upArrowKey.wasPressedThisFrame) || (pad != null && pad.dpad.up.wasPressedThisFrame))
                Pressed?.Invoke(MiniGameRun.Direction.Up);
            else if ((keyboard != null && keyboard.rightArrowKey.wasPressedThisFrame) || (pad != null && pad.dpad.right.wasPressedThisFrame))
                Pressed?.Invoke(MiniGameRun.Direction.Right);
            else if ((keyboard != null && keyboard.downArrowKey.wasPressedThisFrame) || (pad != null && pad.dpad.down.wasPressedThisFrame))
                Pressed?.Invoke(MiniGameRun.Direction.Down);
            else if ((keyboard != null && keyboard.leftArrowKey.wasPressedThisFrame) || (pad != null && pad.dpad.left.wasPressedThisFrame))
                Pressed?.Invoke(MiniGameRun.Direction.Left);
        }

        public override void OnMove(AxisEventData data)
        {
            if (!IsActive() || !IsInteractable()) return;
            data.Use();
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (!IsActive() || !IsInteractable() || data.button != PointerEventData.InputButton.Left) return;
            Missed?.Invoke();
        }
    }
}
