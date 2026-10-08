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
            var input = Gamesim.House.HouseInput.Actions;
            // The arrows and WASD, the D-pad and the left stick (the Competition map's four
            // directions): each a fresh press, never a hold.
            if (input.CompetitionUp.WasPressedThisFrame()) Pressed?.Invoke(MiniGameRun.Direction.Up);
            else if (input.CompetitionRight.WasPressedThisFrame()) Pressed?.Invoke(MiniGameRun.Direction.Right);
            else if (input.CompetitionDown.WasPressedThisFrame()) Pressed?.Invoke(MiniGameRun.Direction.Down);
            else if (input.CompetitionLeft.WasPressedThisFrame()) Pressed?.Invoke(MiniGameRun.Direction.Left);
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
