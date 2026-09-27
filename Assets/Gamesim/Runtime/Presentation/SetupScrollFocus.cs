using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>Keeps keyboard/controller selection visible in setup grids and studio controls.</summary>
    [RequireComponent(typeof(ScrollRect))]
    public sealed class SetupScrollFocus : MonoBehaviour
    {
        private ScrollRect scroll;
        private GameObject previous;
        private void Awake() => scroll = GetComponent<ScrollRect>();
        private void LateUpdate()
        {
            var selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            if (selected == previous) return;
            previous = selected;
            if (selected == null || scroll == null || scroll.content == null || scroll.viewport == null
                || !selected.transform.IsChildOf(scroll.content)) return;
            Canvas.ForceUpdateCanvases();
            var viewport = scroll.viewport;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selected.transform);
            float offset = bounds.min.y < viewport.rect.yMin ? viewport.rect.yMin - bounds.min.y
                : bounds.max.y > viewport.rect.yMax ? viewport.rect.yMax - bounds.max.y : 0f;
            if (Mathf.Abs(offset) < .01f) return;
            var position = scroll.content.anchoredPosition;
            position.y = Mathf.Clamp(position.y + offset, 0f, Mathf.Max(0f, scroll.content.rect.height - viewport.rect.height));
            scroll.StopMovement(); scroll.content.anchoredPosition = position;
        }
    }
}
