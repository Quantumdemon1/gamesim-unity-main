using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gamesim.Episode
{
    /// <summary>
    /// Whether a slider is being dragged by a pointer, and the value it was let go at. A slider's
    /// own event fires for every step of a drag; what a drag chooses is kept once, when the pointer
    /// comes up - or when the slider goes away mid-drag, a render rebuilding its panel (PR-10).
    ///
    /// <para>Only the pointer's press and release: a component on the slider that handled scroll or
    /// drag events as well would keep them from the panel's scroll view behind it.</para>
    /// </summary>
    public sealed class SliderSettle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>Whether a pointer is holding the slider now.</summary>
        public bool Dragging { get; private set; }

        /// <summary>Whether the value moved during this drag and is still to be kept.</summary>
        public bool Pending { get; set; }

        /// <summary>Keeps the value the drag ended at.</summary>
        public Action Settled { get; set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Dragging = true;
            Pending = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Settle();
        }

        private void OnDisable() => Settle();

        private void Settle()
        {
            bool keep = Dragging && Pending;
            Dragging = false;
            Pending = false;
            if (keep) Settled?.Invoke();
        }
    }
}
