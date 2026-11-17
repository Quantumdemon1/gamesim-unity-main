using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Left and Right along a row of tabs (menu slice 3, the notebook's strip): the arrow keys and the
    /// pad's stick or d-pad move the focus to the tab beside, as the creator's steps and the cast
    /// screen's tabs take them, and Enter or the pad's A presses the tab. Up, Down and Tab stay the
    /// HUD's keyboard ring's, which walks every control of the panel in order.
    ///
    /// <para>A handler beside the button rather than the button's own navigation, because the HUD
    /// rewires every control's navigation each time its panel is rebuilt and leaves Left and Right
    /// empty; the button's own move handler then does nothing on Left or Right, and this one moves.
    /// The strip does not wrap: Left on the first tab stays on it.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TabStep : MonoBehaviour, IMoveHandler, ISelectHandler, IDeselectHandler
    {
        /// <summary>The tab's child that shows while it has the keyboard's focus (a thin line, never a second lit look).</summary>
        public const string FocusName = "Tab focus";

        public void OnSelect(BaseEventData eventData) => ShowFocus(true);
        public void OnDeselect(BaseEventData eventData) => ShowFocus(false);

        private void ShowFocus(bool on)
        {
            var line = transform.Find(FocusName);
            if (line != null && line.gameObject.activeSelf != on) line.gameObject.SetActive(on);
        }

        public void OnMove(AxisEventData eventData)
        {
            if (eventData == null) return;
            int direction = eventData.moveDir == MoveDirection.Left ? -1 : eventData.moveDir == MoveDirection.Right ? 1 : 0;
            if (direction == 0 || transform.parent == null) return;
            var row = transform.parent;
            int count = row.childCount, at = transform.GetSiblingIndex();
            // The nearest pressable tab that way, past any that is not.
            for (int index = at + direction; index >= 0 && index < count; index += direction)
            {
                var tab = row.GetChild(index).GetComponent<Selectable>();
                if (tab == null || tab.GetComponent<TabStep>() == null || !tab.IsActive() || !tab.IsInteractable()) continue;
                eventData.selectedObject = tab.gameObject;
                eventData.Use();
                return;
            }
        }
    }
}
