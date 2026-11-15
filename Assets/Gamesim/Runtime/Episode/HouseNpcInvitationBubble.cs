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
    ///
    /// <para>What a player reads goes through the localisation layer - the name, the line and the two captions - while
    /// <see cref="CurrentText"/> keeps the line as the owner gave it and each button keeps its English caption as its
    /// name (Localisation.cs). Every text box is at least <see cref="LabelBoxHeight"/> of its font, and the line is placed
    /// on <see cref="ViewCamera"/>, the view the owner names, or the main camera while it names none.</para>
    /// </summary>
    public sealed class HouseNpcInvitationBubble : MonoBehaviour
    {
        public const string PanelName = "NPC invitation bubble";
        public const string LineName = "NPC invitation line";
        // Captions of their own (D18): the Story Pull's card says "Hear them out" and "Not now", and a
        // caption names one control. These never shipped, so no contract changes.
        public const string HearCaption = "Hear their offer";
        public const string NotNowCaption = "Not right now";
        // A reply card's pair (offers slice 4a): a confrontation, gossip or a plea is not an offer. Captions of their own,
        // none of the Pull's and none of the offer's.
        public const string ReplyHearCaption = "Hear what they want";
        public const string ReplyNotNowCaption = "Not this minute";
        // A caption's box sits this far inside its button, above and below.
        private const float CaptionInset = 4f;
        private GameObject root;
        private RectTransform canvasRect, panel, tail;
        private TMP_Text speaker, words, hearLabel, deferLabel;
        private UnityEngine.UI.Button hearButton, deferButton;
        private EpisodeHud hud;
        private object boundView;
        private Vector3 anchor;
        private float scale = 1f, nextBoundsAt;
        private Rect safe;
        private Vector2 lastFrame;
        // The line as the owner gave it, before the localisation layer: the change test and CurrentText read it.
        private string rawLine;
        // The two captions as the owner gave them: each button's name, its identity, is the raw caption.
        private string rawHear = HearCaption, rawNotNow = NotNowCaption;

        public bool IsVisible => root != null && root.activeSelf && panel != null && panel.gameObject.activeSelf;
        /// <summary>The line on screen as the owner gave it - never its translation - or empty while nothing shows.</summary>
        public string CurrentText => IsVisible ? rawLine ?? "" : "";
        public UnityEngine.UI.Button HearControl => hearButton;
        public UnityEngine.UI.Button NotNowControl => deferButton;

        /// <summary>
        /// Whether the speaker's body is drawn (ROOM-SCOPE-PLAN §3.2 e, RS4): the bubble is over a body
        /// the main view draws, or not up at all. Withheld, its panel is down - so it is not visible and
        /// not a prompt - and the offer it carries stands, to show the moment the speaker is drawn. The
        /// director sets it from the room scope, late in the frame, and it is taken at once.
        /// </summary>
        public bool SpeakerDrawn
        {
            get => speakerDrawn;
            set
            {
                if (speakerDrawn == value) return;
                speakerDrawn = value;
                if (root == null || !root.activeSelf || panel == null) return;
                if (value) { Place(false); return; }
                LetGoOfFocus();
                panel.gameObject.SetActive(false);
            }
        }

        private bool speakerDrawn = true;
        /// <summary>
        /// New words or a new size the panel has not been fitted to yet: a placement that returned
        /// before fitting - the speaker withheld (RS4), the anchor off the screen - leaves it owed to
        /// the next one that draws, so the panel never comes up at the last invitation's size.
        /// </summary>
        private bool sizeOwed;

        /// <summary>
        /// The camera the line is placed for: the director's view (its camera rig's), set by the owner. While it is
        /// unset, or gone, the main camera, as the bubble always used.
        /// </summary>
        public Camera ViewCamera { get; set; }

        /// <summary>
        /// The least height of a text box for a font of <paramref name="fontSize"/>: ceil(1.32 x the font) + 1, so a box
        /// never falls under 1.3x its font, where Inter draws a truncated line as nothing; more when the text needs it
        /// (<paramref name="preferredHeight"/>). Pure, so EditMode can drive it.
        /// </summary>
        public static float LabelBoxHeight(float fontSize, float preferredHeight) =>
            Mathf.Max(preferredHeight, Mathf.Ceil(fontSize * 1.32f) + 1f);

        /// <summary>The offer's bubble: <see cref="HearCaption"/> and <see cref="NotNowCaption"/>, as it has always been.</summary>
        public void Show(string npcName, string line, float fontScale, Vector3 worldAnchor,
            Action hearThemOut, Action defer, object view = null) =>
            Show(npcName, line, fontScale, worldAnchor, hearThemOut, defer, HearCaption, NotNowCaption, view);

        /// <summary>
        /// The bubble with a source's own two captions (OFFERS-COME-TO-YOU-PLAN §C.4): the same two Button objects, their
        /// navigation and focus unchanged, each named for its raw caption and labelled with its localised words. A blank
        /// caption hides the bubble, as a blank line does.
        /// </summary>
        public void Show(string npcName, string line, float fontScale, Vector3 worldAnchor,
            Action hearThemOut, Action defer, string hearCaption, string notNowCaption, object view = null)
        {
            if (string.IsNullOrWhiteSpace(npcName) || string.IsNullOrWhiteSpace(line)
                || string.IsNullOrWhiteSpace(hearCaption) || string.IsNullOrWhiteSpace(notNowCaption)
                || !HouseRoomQuery.Finite(worldAnchor) || hearThemOut == null || defer == null)
            { Hide(); return; }
            if (root == null) Create();
            float requestedScale = Mathf.Clamp(fontScale, 1f, TextSizes.Scale(TextSize.Largest));
            // The player reads the localised words; the raw line is what changed and what CurrentText says.
            string shownName = Localisation.Text(npcName), shownLine = Localisation.Text(line);
            string hear = Localisation.Text(hearCaption), notNow = Localisation.Text(notNowCaption);
            bool changed = rawLine != line || speaker.text != shownName || words.text != shownLine
                || rawHear != hearCaption || rawNotNow != notNowCaption
                || hearLabel.text != hear || deferLabel.text != notNow || !Mathf.Approximately(scale, requestedScale);
            rawLine = line; speaker.text = shownName; words.text = shownLine; scale = requestedScale;
            if (rawHear != hearCaption) { rawHear = hearCaption; hearButton.gameObject.name = hearCaption; }
            if (rawNotNow != notNowCaption) { rawNotNow = notNowCaption; deferButton.gameObject.name = notNowCaption; }
            hearLabel.text = hear; deferLabel.text = notNow;
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
            LetGoOfFocus();
            root.SetActive(false);
        }

        /// <summary>A selected control of the bubble's lets go: the keyboard never stays on a control that is not drawn.</summary>
        private void LetGoOfFocus()
        {
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null
                && events.currentSelectedGameObject.transform.IsChildOf(root.transform)) events.SetSelectedGameObject(null);
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
            if (textChanged) sizeOwed = true;
            // Explicitly: a camera that has gone is Unity's fake null, which ?? would keep.
            var camera = ViewCamera != null ? ViewCamera : Camera.main;
            // A speaker the room scope does not draw has no bubble over them (RS4).
            if (camera == null || canvasRect == null || !speakerDrawn) { panel.gameObject.SetActive(false); return; }
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
            if (sizeOwed || frameChanged || panel.sizeDelta.x > safe.width) { SizeForText(); sizeOwed = false; }
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
            hearLabel.fontSize = deferLabel.fontSize = 16f * scale;
            // The captions as shown, so a translated caption fits its button.
            var first = hearLabel.GetPreferredValues(hearLabel.text, float.PositiveInfinity, float.PositiveInfinity);
            var second = deferLabel.GetPreferredValues(deferLabel.text, float.PositiveInfinity, float.PositiveInfinity);
            float firstWidth = first.x + 28f, secondWidth = second.x + 28f;
            bool stacked = firstWidth + secondWidth + 10f > width - 32f;
            // Every box at least 1.3x its font: a caption's inside its button, the name's and the line's on the panel.
            float captionHeight = LabelBoxHeight(hearLabel.fontSize, Mathf.Max(first.y, second.y));
            float buttonHeight = Mathf.Max(40f * scale, captionHeight + 2f * CaptionInset);
            float footerHeight = stacked ? buttonHeight * 2f + 10f : buttonHeight;
            float nameHeight = LabelBoxHeight(speaker.fontSize, speaker.GetPreferredValues(speaker.text, width - 32f, float.PositiveInfinity).y);
            float lineHeight = LabelBoxHeight(words.fontSize, words.GetPreferredValues(words.text, width - 32f, float.PositiveInfinity).y);
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
            hearButton = Button(rawHear, true);
            deferButton = Button(rawNotNow, false);
            hearLabel = hearButton.GetComponentInChildren<TMP_Text>();
            deferLabel = deferButton.GetComponentInChildren<TMP_Text>();
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

        // The button is named for its English caption, its identity; its label shows the caption as the player reads it.
        private UnityEngine.UI.Button Button(string caption, bool primary)
        {
            var item = new GameObject(caption, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            item.transform.SetParent(panel, false);
            var image = item.GetComponent<UnityEngine.UI.Image>(); UiTheme.Style(image, primary ? UiTheme.Accent : new Color(.08f, .15f, .21f), 12);
            var button = item.GetComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var labelObject = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI)); labelObject.transform.SetParent(item.transform, false);
            var rect = (RectTransform)labelObject.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10f, CaptionInset); rect.offsetMax = new Vector2(-10f, -CaptionInset);
            var label = labelObject.GetComponent<TextMeshProUGUI>(); label.font = UiTheme.Font(UiTheme.Weight.Regular);
            label.text = Localisation.Text(caption); label.color = primary ? UiTheme.Ink : Color.white; label.alignment = TextAlignmentOptions.Center;
            label.richText = false; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap;
            return button;
        }
        private void OnDisable() => Hide();
        private void OnDestroy() { if (root != null) Destroy(root); }
    }
}
