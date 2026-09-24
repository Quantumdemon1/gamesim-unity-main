using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    public sealed partial class CompetitionGameScreen
    {
        // The frame's layout, in reference units: the margin at the frame's edge, the gap between
        // cards, the header row, the right-hand column, and the board the games were drawn for.
        private const float FrameEdge = 24f, FrameGap = 12f, HeaderHeight = 176f, TimerWidth = 250f;
        private const float SideWidth = 272f, ControlsHeight = 172f, LegendHeight = 30f;
        private const float BaseBoardWidth = 880f, BaseBoardHeight = 430f;
        // Denser glass than the house's: the stage behind is lit and lettered, and a board over it
        // at .72 let the lettering read through the targets.
        private const float BoardAlpha = .84f, CardAlpha = .94f;
        private float surfaceWidth = BaseBoardWidth, surfaceHeight = BaseBoardHeight;
        private Vector2 laidOutFor;
        private Vector2Int screenFor;

        /// <summary>The band across the board's top: the game's own chips on the left, the last
        /// thing that happened on the right. It takes no clicks.</summary>
        private float BandHeight => 44f * FontScale;
        /// <summary>The play field: the board under the band, where every game draws.</summary>
        private Rect PlayField => new Rect(0f, BandHeight, surfaceWidth, Mathf.Max(120f, surfaceHeight - BandHeight));

        /// <summary>
        /// The frame in the canvas's reference units, from the pixels it is drawn into and the
        /// scaler's own settings rather than from the canvas rect: the first competition builds its
        /// canvas and lays it out in the same frame, before the scaler has run, when the rect is
        /// still the raw screen - which drew the board at less than half its size. A canvas drawn
        /// through a camera (a review capture) is sized by that camera's target instead.
        /// </summary>
        private Vector2 CurrentFrame()
        {
            var canvas = GetComponent<Canvas>();
            var scaler = GetComponent<CanvasScaler>();
            float width = Screen.width, height = Screen.height;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null)
            { width = canvas.worldCamera.pixelWidth; height = canvas.worldCamera.pixelHeight; }
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize && width > 0 && height > 0)
            {
                var reference = scaler.referenceResolution;
                float log = Mathf.Lerp(Mathf.Log(width / reference.x, 2f), Mathf.Log(height / reference.y, 2f), scaler.matchWidthOrHeight);
                float scale = Mathf.Pow(2f, log);
                return new Vector2(width / scale, height / scale);
            }
            var rect = ((RectTransform)transform).rect;
            return new Vector2(rect.width > 0 ? rect.width : 1600f, rect.height > 0 ? rect.height : 900f);
        }

        /// <summary>
        /// Follows the frame: a resize, Alt+Enter, a resolution change or a switch to a camera
        /// re-places everything; nothing is rebuilt. A real change of the screen while the clock
        /// could run pauses first - the player's hands were on the window, not the game.
        /// </summary>
        private void LateUpdate()
        {
            if (!IsShowing || panel == null) return;
            var frame = CurrentFrame();
            if (Mathf.Abs(frame.x - laidOutFor.x) > 1f || Mathf.Abs(frame.y - laidOutFor.y) > 1f) LayoutForFrame(frame);
            var screenNow = new Vector2Int(Screen.width, Screen.height);
            if (screenNow == screenFor) return;
            screenFor = screenNow;
            if ((playing || previewStarted) && !Paused && run != null && !run.Finished) OnScreenResized();
        }

        /// <summary>The window changed size while the clock could run: pause, and say why.</summary>
        public void OnScreenResized()
        {
            if (!IsShowing || Paused || run == null || run.Finished) return;
            TogglePause();
            SetOverlayDetail("The window changed size. " + LegendKey(LegendAction.Pause) + " resumes; no attempt time was lost.");
        }

        /// <summary>
        /// Places every card and control for a frame of <paramref name="frame"/> reference units.
        /// Every size is taken from the frame, so the board is as big as the screen allows at any
        /// aspect ratio; the text sizes come from <see cref="FontScale"/>, and at the larger text the
        /// cards grow and the board gives way, never the words.
        /// </summary>
        public void LayoutForFrame(Vector2 frame)
        {
            if (panel == null || run == null) return;
            laidOutFor = frame;
            float frameWidth = frame.x, frameHeight = frame.y;
            float left = FrameEdge, sideX = frameWidth - FrameEdge - SideWidth, mainWidth = Mathf.Max(480f, sideX - FrameGap - left);
            float challengeWidth = mainWidth - TimerWidth - FrameGap;
            float header = HeaderFor(challengeWidth);
            float legend = LegendHeight * FontScale;
            surfaceWidth = mainWidth;
            surfaceHeight = Mathf.Max(360f, frameHeight - (FrameEdge + header + FrameGap) - (FrameEdge + legend + 8f));

            LayoutChallenge(left, challengeWidth, header);
            LayoutTimer(left + challengeWidth + FrameGap, header);
            Place(playArea, left, FrameEdge + header + FrameGap, surfaceWidth, surfaceHeight);
            LayoutBoardHeader();
            LayoutLegend(left, frameHeight - FrameEdge - legend, surfaceWidth, legend);
            float fieldHeight = frameHeight - 2f * FrameEdge - ControlsHeight - FrameGap;
            LayoutField(fieldHeight);
            LayoutFooter(frameHeight);
            switch (run.Kind)
            {
                case CompetitionMiniGames.Kind.Memory: PlaceMemory(); break;
                case CompetitionMiniGames.Kind.Reaction: PlaceReaction(); break;
                case CompetitionMiniGames.Kind.Endurance: PlaceEndurance(); break;
            }
            LayoutOverlay();
            LayoutFinishPlate();
            if (IsShowing) Refresh();
        }

        /// <summary>How much larger than the board the games were drawn for this one is.</summary>
        /// <para>Taken from the frame with the standard text size's chrome, so the larger text -
        /// which grows the header and shrinks the board - never shrinks the target. A smaller
        /// target at large text would be a harder game.</para>
        private float BoardScale => Mathf.Clamp(Mathf.Min(surfaceWidth / BaseBoardWidth,
            (laidOutFor.y - (FrameEdge + HeaderHeight + FrameGap) - (FrameEdge + LegendHeight + 8f) - 44f) / (BaseBoardHeight - 44f)), 1f, 1.6f);
    }
}
