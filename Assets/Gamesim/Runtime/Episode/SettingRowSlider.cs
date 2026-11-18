using Gamesim.House;
using Gamesim.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// A Settings slider row's control (menu slice 4): the mouse drags it, and left and right - the
    /// arrows, the pad's d-pad and stick - move it one step (<see cref="SettingsWords.SliderStep"/>)
    /// and the slider's own change keeps the value at once (the row's listener: no drag is held). A
    /// plain slider steps by one percent, a press nobody can hear.
    /// </summary>
    public sealed class SettingRowSlider : Slider
    {
        /// <summary>The row's name and value together, for a screen reader ("Music volume: 70%").</summary>
        public string Description { get; set; }

        public override void OnMove(AxisEventData eventData)
        {
            // While a rebinding listens, left and right are its press, not a step (menu slice 4, part b).
            if ((eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right) && HouseBindings.Busy)
            {
                eventData.Use();
                return;
            }
            if (IsActive() && IsInteractable()
                && (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right))
            {
                int now = Mathf.RoundToInt(value);
                int next = SettingsWords.SliderStepped(now, eventData.moveDir == MoveDirection.Left ? -1 : 1);
                eventData.Use();
                if (next != now) value = next;
                return;
            }
            base.OnMove(eventData);
        }
    }
}
