using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The canvas scaler every screen canvas in the game is built with: Unity's, except that a frame
    /// wider than 16:9 is laid out matching height (<see cref="FrameMatch"/>, PLAN A, A12b). Its own
    /// <see cref="CanvasScaler.matchWidthOrHeight"/> is never written - a frame of 16:9 or narrower
    /// scales exactly as a plain scaler with the same settings does - and the wide frame's match is
    /// lent to Unity's own arithmetic for the one call that sizes the canvas, so the canvas is right
    /// the pass the frame changes, with no frame of the old shape in between.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FrameScaler : CanvasScaler
    {
        private Canvas own;

        protected override void HandleScaleWithScreenSize()
        {
            if (own == null) own = GetComponent<Canvas>();
            // The size Unity's own scaler reads: the canvas's display, or another display's.
            Vector2 size = own != null ? own.renderingDisplaySize : new Vector2(Screen.width, Screen.height);
            int display = own != null ? own.targetDisplay : 0;
            if (display > 0 && display < Display.displays.Length)
                size = new Vector2(Display.displays[display].renderingWidth, Display.displays[display].renderingHeight);
            float authored = m_MatchWidthOrHeight;
            m_MatchWidthOrHeight = FrameMatch.For(size.x, size.y, authored);
            try { base.HandleScaleWithScreenSize(); }
            finally { m_MatchWidthOrHeight = authored; }
        }
    }
}
