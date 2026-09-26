using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// A control on the season report: a button in every way but one. While a pad's right stick is
    /// scrolling the report, a lean of that stick is not a step round the keyboard ring.
    ///
    /// <para>The input module walks the ring on either stick - its navigation reads both - so the
    /// stick that scrolls the season also stepped the focus from control to control, down onto the
    /// sort chips at the report's foot, and the report followed the focus there: the page ran away
    /// from the reader the moment they leaned on it. The left stick, the d-pad, the arrows and Tab
    /// still walk the ring as they always have.</para>
    /// </summary>
    public sealed class SeasonReportControl : Button
    {
        public override void OnMove(AxisEventData eventData)
        {
            if (SeasonReport.StickIsScrolling())
            {
                eventData.Use();
                return;
            }
            base.OnMove(eventData);
        }
    }
}
