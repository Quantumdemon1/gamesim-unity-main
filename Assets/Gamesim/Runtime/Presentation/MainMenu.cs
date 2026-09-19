using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The front door: continue a season, start a new one, open settings, or leave.
    ///
    /// <para>The project had no way in. You pressed Play on a scene and were standing in the house
    /// mid-season, which is the one part of the reference build's flow that had no counterpart here
    /// at all — everything else was a screen that existed and looked wrong, while this was a screen
    /// that did not exist.</para>
    ///
    /// <para>It is an overlay rather than a scene. A separate menu scene would mean loading and
    /// unloading the house around it, and the house is the expensive thing to build; opening in
    /// front of a house that is already standing costs nothing and cannot strand the player in a
    /// scene the episode director does not run.</para>
    ///
    /// <para><b>Continue is offered only when there is something to continue.</b> A fresh install
    /// has a save path but no file, and a button that loads nothing is worse than one that is not
    /// there — so the caller decides whether it is offered, from the disk rather than from the fact
    /// that a season object exists in memory.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenu : MonoBehaviour
    {
        private const float Width = 560f;

        /// <summary>Captions. Tests and screen readers identify these controls by their words.</summary>
        public const string ContinueCaption = "Continue your season";
        public const string NewSeasonCaption = "Start a new season";
        public const string SettingsCaption = "Settings and saves";
        public const string QuitCaption = "Quit the game";

        private CanvasGroup group;
        private CanvasScaler scaler;

        public bool IsShowing => group != null && group.alpha > 0f;

        /// <summary>
        /// The "larger text" setting, applied by magnifying the whole screen. The layout is fixed in
        /// reference pixels, so growing the type alone would push it out of boxes that did not grow.
        /// </summary>
        public float FontScale
        {
            set
            {
                if (scaler == null) return;
                float scale = Mathf.Clamp(value, 0.5f, 2f);
                scaler.referenceResolution = new Vector2(1920f / scale, 1080f / scale);
            }
        }

        /// <summary>Above the cast screen, which it opens; nothing is above this.</summary>
        public static MainMenu Attach(GameObject owner)
        {
            var root = new GameObject("Gamesim Main Menu",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(owner.transform, false);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 130;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var menu = root.AddComponent<MainMenu>();
            menu.group = root.GetComponent<CanvasGroup>();
            menu.scaler = scaler;
            menu.Hide();
            return menu;
        }

        public void Hide()
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        /// <summary>
        /// Opens the menu. <paramref name="canContinue"/> decides whether the first control is
        /// offered at all; <paramref name="note"/> is the one line under the title, which is where
        /// a failed load or a retained slot gets explained.
        /// </summary>
        public void Show(bool canContinue, string note,
            Action onContinue, Action onNewSeason, Action onSettings, Action onQuit,
            string career = null)
        {
            Rebuild(canContinue, note, onContinue, onNewSeason, onSettings, onQuit, career);
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        private void Rebuild(bool canContinue, string note,
            Action onContinue, Action onNewSeason, Action onSettings, Action onQuit, string career)
        {
            // Deactivated before Destroy, which runs at the end of the frame: otherwise a rebuild
            // leaves the previous controls live alongside the new ones for a frame.
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            var scrim = HudPrimitives.Fill("Scrim", transform, new Color(0.02f, 0.04f, 0.06f, 0.98f), 1);
            scrim.anchorMin = Vector2.zero;
            scrim.anchorMax = Vector2.one;
            scrim.sizeDelta = Vector2.zero;
            scrim.anchoredPosition = Vector2.zero;

            var column = new GameObject("Column", typeof(RectTransform)).GetComponent<RectTransform>();
            column.SetParent(scrim, false);
            column.anchorMin = new Vector2(0.5f, 0.5f);
            column.anchorMax = new Vector2(0.5f, 0.5f);
            column.pivot = new Vector2(0.5f, 0.5f);
            column.sizeDelta = new Vector2(Width, 640f);
            column.anchoredPosition = Vector2.zero;

            float cursor = 0f;

            // The eye, when the icon pass has been run; a drawn ring when it has not, so a clone
            // that has never generated the art still gets a mark rather than an empty gap.
            var mark = new GameObject("Mark", typeof(RectTransform)).GetComponent<RectTransform>();
            mark.SetParent(column, false);
            Place(mark, 96f, 96f, -cursor);
            var glyph = UiTheme.Icon("eye");
            if (glyph != null)
            {
                var image = mark.gameObject.AddComponent<Image>();
                image.sprite = glyph;
                image.color = UiTheme.Gold;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            else
            {
                var ring = HudPrimitives.Disc("Ring", mark, UiTheme.Gold);
                Centre(ring, 96f, 96f);
                var pupil = HudPrimitives.Disc("Pupil", mark, UiTheme.Ink);
                Centre(pupil, 40f, 40f);
            }
            cursor += 120f;

            Text(column, "BIG BROTHER", 40f, UiTheme.Gold, 52f, ref cursor);
            Text(column, "A house, a vote, and everyone watching.", 16f, UiTheme.Muted, 28f, ref cursor);
            if (!string.IsNullOrEmpty(note)) Text(column, note, 13f, UiTheme.Warning, 40f, ref cursor);
            cursor += 16f;

            if (canContinue) Button(column, ContinueCaption, true, onContinue, ref cursor);
            Button(column, NewSeasonCaption, !canContinue, onNewSeason, ref cursor);
            Button(column, SettingsCaption, false, onSettings, ref cursor);
            Button(column, QuitCaption, false, onQuit, ref cursor);

            // The career line, once there is a career: what the seasons so far add up to. It
            // sits under the buttons rather than the title because it is a fact about the
            // player, not a note about this launch.
            if (!string.IsNullOrEmpty(career)) Text(column, career, 13f, UiTheme.Accent, 24f, ref cursor);

            cursor += 10f;
            Text(column, "Plays offline. No account, no connection, and nothing is uploaded.",
                12f, UiTheme.Muted, 24f, ref cursor);

            column.sizeDelta = new Vector2(Width, cursor);
        }

        // ---------------------------------------------------------------- pieces

        private static void Button(Transform parent, string caption, bool primary, Action action, ref float cursor)
        {
            var panel = HudPrimitives.Fill(caption, parent,
                primary ? UiTheme.AccentDeep : UiTheme.SurfaceRaised, 10);
            Place(panel, Width, 54f, -cursor);
            UiTheme.AddBorder(panel, 10, primary ? UiTheme.Accent : UiTheme.Outline);

            var image = panel.GetComponent<Image>();
            image.raycastTarget = true;

            var label = HudPrimitives.Label("Label", panel, 18f,
                primary ? UiTheme.Paper : UiTheme.Muted, TextAlignmentOptions.Center);
            label.text = Localisation.Text(caption);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12f, 0f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);

            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (action != null) button.onClick.AddListener(() => action());
            cursor += 62f;
        }

        private static void Text(Transform parent, string value, float size, Color colour,
            float height, ref float cursor)
        {
            var label = HudPrimitives.Label("Text", parent, size, colour, TextAlignmentOptions.Center);
            label.text = Localisation.Text(value);
            Place(label.rectTransform, Width, height, -cursor);
            cursor += height;
        }

        private static void Place(RectTransform rect, float width, float height, float y)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(0f, y);
        }

        private static void Centre(RectTransform rect, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
