using UnityEngine;
using UnityEngine.EventSystems;

namespace Gamesim.Presentation
{
    /// <summary>
    /// A board card's focus ring: shown while the keyboard or the pad is on the card, or the
    /// pointer is over it, and gone otherwise. A ring outside the tile rather than a tint on it -
    /// a tint is one more colour on a card whose colours already say face down, face up, no match
    /// and matched.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompetitionCardFocus : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public GameObject Ring;
        private bool focused, hovered;

        public void OnSelect(BaseEventData data) { focused = true; Apply(); }
        public void OnDeselect(BaseEventData data) { focused = false; Apply(); }
        public void OnPointerEnter(PointerEventData data) { hovered = true; Apply(); }
        public void OnPointerExit(PointerEventData data) { hovered = false; Apply(); }

        private void OnDisable() { focused = false; hovered = false; Apply(); }

        private void Apply() { if (Ring != null) Ring.SetActive(focused || hovered); }
    }
}
