using System;
using Gamesim.House;
using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gamesim.Episode
{
    /// <summary>
    /// A speaker-anchored invitation with two presentation callbacks. The owner supplies only
    /// published words after physical arrival/visibility proof; this component has no state or command access.
    /// </summary>
    public sealed class HouseNpcInvitationBubble : MonoBehaviour
    {
        public const string PanelName = "NPC invitation bubble";
        public const string LineName = "NPC invitation line";
        public const string HearCaption = "Hear them out";
        public const string NotNowCaption = "Not now";
        private GameObject root;
        private RectTransform canvasRect, panel, tail;
        private TMP_Text speaker, words;
        private UnityEngine.UI.Button hearButton, deferButton;
        private EpisodeHud hud;
        private object boundView;
        private Vector3 anchor;
        private float scale = 1f, nextBoundsAt;
        private Rect safe;
        private Vector2 lastFrame;

        public bool IsVisible => root != null && root.activeSelf && panel != null && panel.gameObject.activeSelf;
        public string CurrentText => IsVisible ? words.text : "";
        public UnityEngine.UI.Button HearControl => hearButton;
        public UnityEngine.UI.Button NotNowControl => deferButton;

        public void Show(string npcName, string line, float fontScale, Vector3 worldAnchor,
            Action hearThemOut, Action defer, object view = null)
        {
            if (string.IsNullOrWhiteSpace(npcName) || string.IsNullOrWhiteSpace(line)
                || !HouseRoomQuery.Finite(worldAnchor) || hearThemOut == null || defer == null)
            { Hide(); return; }
            if (root == null) Create();
            float requestedScale = Mathf.Clamp(fontScale, 1f, TextSizes.Scale(TextSize.Largest));
            bool changed = speaker.text != npcName || words.text != line || !Mathf.Approximately(scale, requestedScale);
            speaker.text = npcName; words.text = line; scale = requestedScale;
            object nextView = view ?? (boundView != null && !changed ? boundView : new object());
            if (!ReferenceEquals(boundView, nextView)) BindActions(nextView, hearThemOut, defer);
            anchor = worldAnchor;
            if (!root.activeSelf) nextBoundsAt = 0;
            root.SetActive(true);
            Place(changed);
        }

        public void Hide()
        {
            boundView = null;
            if (root == null) return;
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null
                && events.currentSelectedGameObject.transform.IsChildOf(root.transform)) events.SetSelectedGameObject(null);
            root.SetActive(false);
        }

        /// <summary>Optional owner entry point for keyboard/pad focus; showing an invitation never steals it.</summary>
        public bool Focus()
        {
            if (!IsVisible || EventSystem.current == null) return false;
            EventSystem.current.SetSelectedGameObject(hearButton.gameObject);
            return true;
        }

        private void LateUpdate()
        {
            if (root == null || !root.activeSelf) return;
            Place(false);
            var events = EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            // A separate world canvas owns its own two-control Tab ring while selected. Entry into
            // that ring remains the owner's input decision, so the house is otherwise fully playable.
            if (selected != null && selected.transform.IsChildOf(root.transform) && IsVisible
                && HouseInput.TabStep(out _))
                events.SetSelectedGameObject(selected == hearButton.gameObject ? deferButton.gameObject : hearButton.gameObject);
        }

        private void Place(bool textChanged)
        {
            var camera = Camera.main;
            if (camera == null || canvasRect == null) { panel.gameObject.SetActive(false); return; }
            var viewport = camera.WorldToViewportPoint(anchor);
            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
            { panel.gameObject.SetActive(false); return; }
            var projected = camera.WorldToScreenPoint(anchor);
            panel.gameObject.SetActive(true);
            var frame = canvasRect.rect.size;
            bool frameChanged = frame != lastFrame;
            if (frameChanged || Time.unscaledTime >= nextBoundsAt)
            {
                if (hud == null) hud = GetComponent<EpisodeHud>();
                safe = hud != null ? hud.WorldCaptionSafeBounds : Rect.MinMaxRect(24, 100, frame.x - 24, frame.y - 80);
                lastFrame = frame; nextBoundsAt = Time.unscaledTime + .5f;
            }
            if (textChanged || frameChanged || panel.sizeDelta.x > safe.width) SizeForText();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, projected, null, out var local)) return;
            float half = panel.sizeDelta.x * .5f;
            float x = Mathf.Clamp(local.x + frame.x * .5f, safe.xMin + half, Mathf.Max(safe.xMin + half, safe.xMax - half));
            float y = Mathf.Clamp(local.y + frame.y * .5f + 28f, safe.yMin, Mathf.Max(safe.yMin, safe.yMax - panel.sizeDelta.y));
            panel.anchoredPosition = new Vector2(x, y);
            tail.anchoredPosition = new Vector2(Mathf.Clamp(local.x + frame.x * .5f - x, -half + 20f, half - 20f), 2f);
        }

        private void SizeForText()
        {
            float width = Mathf.Max(160f, Mathf.Min(440f, safe.width));
            speaker.fontSize = 17f * scale; words.fontSize = 19f * scale;
            var hearLabel = hearButton.GetComponentInChildren<TMP_Text>();
            var deferLabel = deferButton.GetComponentInChildren<TMP_Text>();
            hearLabel.fontSize = deferLabel.fontSize = 16f * scale;
            float firstWidth = hearLabel.GetPreferredValues(HearCaption, float.PositiveInfinity, float.PositiveInfinity).x + 28f;
            float secondWidth = deferLabel.GetPreferredValues(NotNowCaption, float.PositiveInfinity, float.PositiveInfinity).x + 28f;
            bool stacked = firstWidth + secondWidth + 10f > width - 32f;
            float buttonHeight = 40f * scale, footerHeight = stacked ? buttonHeight * 2f + 10f : buttonHeight;
            float nameHeight = speaker.GetPreferredValues(speaker.text, width - 32f, float.PositiveInfinity).y;
            float lineHeight = words.GetPreferredValues(words.text, width - 32f, float.PositiveInfinity).y;
            panel.sizeDelta = new Vector2(width, nameHeight + lineHeight + footerHeight + 50f);
            SetTopText(speaker.rectTransform, 16f, nameHeight);
            SetTopText(words.rectTransform, 24f + nameHeight, lineHeight);
            SetButton(hearButton, new Vector2(16f, 12f + (stacked ? buttonHeight + 10f : 0f)),
                new Vector2(stacked ? width - 32f : firstWidth, buttonHeight));
            SetButton(deferButton, new Vector2(stacked ? 16f : 26f + firstWidth, 12f),
                new Vector2(stacked ? width - 32f : secondWidth, buttonHeight));
        }

        private static void SetTopText(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1f);
            rect.offsetMin = new Vector2(16f, -top - height); rect.offsetMax = new Vector2(-16f, -top);
        }
        private static void SetButton(UnityEngine.UI.Button button, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = Vector2.zero;
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }

        private void Create()
        {
            root = new GameObject("NPC invitation", typeof(RectTransform), typeof(Canvas), typeof(FrameScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 69;
            var scaler = root.GetComponent<FrameScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            canvasRect = (RectTransform)root.transform;
            var ground = new GameObject(PanelName, typeof(RectTransform), typeof(UnityEngine.UI.Image)); ground.transform.SetParent(root.transform, false);
            panel = (RectTransform)ground.transform; panel.anchorMin = panel.anchorMax = Vector2.zero; panel.pivot = new Vector2(.5f, 0f);
            UiTheme.Style(ground.GetComponent<UnityEngine.UI.Image>(), UiTheme.Ink, UiTheme.PanelRadius);
            var tailObject = new GameObject("Invitation tail", typeof(RectTransform), typeof(UnityEngine.UI.Image)); tailObject.transform.SetParent(panel, false);
            tail = (RectTransform)tailObject.transform; tail.anchorMin = tail.anchorMax = new Vector2(.5f, 0f);
            tail.sizeDelta = new Vector2(18f, 18f); tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tailObject.GetComponent<UnityEngine.UI.Image>().color = UiTheme.Ink;
            tailObject.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            speaker = Text("Invitation speaker", UiTheme.Accent); words = Text(LineName, Color.white);
            hearButton = Button(HearCaption, true);
            deferButton = Button(NotNowCaption, false);
            var navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit,
                selectOnLeft = deferButton, selectOnRight = deferButton, selectOnUp = deferButton, selectOnDown = deferButton };
            hearButton.navigation = navigation;
            navigation.selectOnLeft = navigation.selectOnRight = navigation.selectOnUp = navigation.selectOnDown = hearButton;
            deferButton.navigation = navigation;
        }

        private TMP_Text Text(string name, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); item.transform.SetParent(panel, false);
            var label = item.GetComponent<TextMeshProUGUI>(); label.font = UiTheme.Font(UiTheme.Weight.Regular);
            label.color = color; label.richText = false; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal; label.enableAutoSizing = false;
            return label;
        }

        private void BindActions(object view, Action hearThemOut, Action defer)
        {
            boundView = view;
            // Replace the event object, not just its listeners. A previously retained event keeps
            // its old token and cannot invoke the newly shown record on this reused control.
            hearButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            deferButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            hearButton.onClick.AddListener(() =>
            {
                if (!IsVisible || !ReferenceEquals(boundView, view)) return;
                Hide(); hearThemOut();
            });
            deferButton.onClick.AddListener(() =>
            {
                if (!IsVisible || !ReferenceEquals(boundView, view)) return;
                Hide(); defer();
            });
        }

        private UnityEngine.UI.Button Button(string caption, bool primary)
        {
            var item = new GameObject(caption, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            item.transform.SetParent(panel, false);
            var image = item.GetComponent<UnityEngine.UI.Image>(); UiTheme.Style(image, primary ? UiTheme.Accent : new Color(.08f, .15f, .21f), 12);
            var button = item.GetComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var labelObject = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI)); labelObject.transform.SetParent(item.transform, false);
            var rect = (RectTransform)labelObject.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10f, 4f); rect.offsetMax = new Vector2(-10f, -4f);
            var label = labelObject.GetComponent<TextMeshProUGUI>(); label.font = UiTheme.Font(UiTheme.Weight.Regular);
            label.text = caption; label.color = primary ? UiTheme.Ink : Color.white; label.alignment = TextAlignmentOptions.Center;
            label.richText = false; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap;
            return button;
        }
        private void OnDisable() => Hide();
        private void OnDestroy() { if (root != null) Destroy(root); }
    }
}
