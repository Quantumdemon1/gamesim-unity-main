using System;
using Gamesim.House;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// A Settings row's control, and a section's entry in the Settings list (menu slice 4): a button
    /// that also answers left and right. The HUD wires up and down through the panel's ring and
    /// leaves left and right empty, so they are this control's own: a switch flips, a choice steps
    /// through its values, a section entry moves the keyboard to its neighbour. With no
    /// <see cref="Step"/> it is a plain button. A pad's d-pad and stick send the same moves.
    /// </summary>
    public sealed class SettingRowButton : Button
    {
        /// <summary>What a press of left (-1) or right (+1) does; null leaves the move to the button.</summary>
        public Action<int> Step { get; set; }

        /// <summary>The row's name and value together, for a screen reader ("Music: On").</summary>
        public string Description { get; set; }

        public override void OnMove(AxisEventData eventData)
        {
            // While a rebinding listens, left and right are its press, not a step (menu slice 4, part b).
            if (Step != null && (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right) && HouseBindings.Busy)
            {
                eventData.Use();
                return;
            }
            if (Step != null && IsActive() && IsInteractable()
                && (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right))
            {
                Step(eventData.moveDir == MoveDirection.Left ? -1 : 1);
                eventData.Use();
                return;
            }
            base.OnMove(eventData);
        }
    }
}
