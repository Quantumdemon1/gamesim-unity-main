using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Accessibility;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    // The two columns scroll independently so long text never pushes a command under the home indicator.
    public sealed class PhoneMenuPage : MonoBehaviour
    {
        private ScrollRect summary, choices;
        private RectTransform safeContent;
        private RectTransform backRect, closeRect;
        private float preferredBackWidth;
        private float textScale;
        private readonly List<Entry> entries = new List<Entry>();
        private AccessibilityHierarchy hierarchy;
        private readonly Dictionary<Selectable, AccessibilityNode> controlNodes = new Dictionary<Selectable, AccessibilityNode>();
        private TMP_InputField editingField;
        private bool framesDirty;
        private CanvasGroup owner;
        private string returnFocus, readerFocus;
        private GameObject selectedRow;
        private bool focusPending;
        private readonly Vector3[] corners = new Vector3[4];

        private struct Entry
        {
            public string Label;
            public string StaticFocusId;
            public string FocusId => StaticFocusId ?? Rect.name;
            public RectTransform Rect;
            public Button Control;
            public TMP_InputField Field;
            public AccessibilityRole Role;
            public ScrollRect Scroll;
            public bool Selected;
        }

        public static PhoneMenuPage Create(Transform parent, string title, float fontScale)
        {
            var scrim = HudPrimitives.Fill("Phone menu", parent, UiTheme.Background, 1);
            Stretch(scrim);
            scrim.GetComponent<Image>().raycastTarget = true;
            var page = scrim.gameObject.AddComponent<PhoneMenuPage>();
            page.owner = parent.GetComponentInParent<CanvasGroup>();
            page.textScale = fontScale;
            var safe = new GameObject("Safe content", typeof(RectTransform));
            safe.SetActive(false);
            safe.transform.SetParent(scrim, false);
            var bounds = (RectTransform)safe.transform;
            Stretch(bounds);
            page.safeContent = bounds;
            page.RefreshSafeArea();
            safe.SetActive(true);
            page.summary = Column(bounds, "Season information", 0f, .42f);
            page.choices = Column(bounds, "Actions", .42f, 1f);
            page.summary.onValueChanged.AddListener(_ => page.framesDirty = true);
            page.choices.onValueChanged.AddListener(_ => page.framesDirty = true);
            page.Copy(title, true);
            return page;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static ScrollRect Column(RectTransform parent, string name, float left, float right)
        {
            var viewport = HudPrimitives.Fill(name, parent, UiTheme.Surface, 10);
            viewport.anchorMin = new Vector2(left, 0);
            viewport.anchorMax = new Vector2(right, 1);
            viewport.offsetMin = new Vector2(6, 8);
            viewport.offsetMax = new Vector2(-6, -8);
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 6;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return scroll;
        }

        public void Copy(string text, bool heading = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            var label = HudPrimitives.Label(heading ? "Heading" : "Description", summary.content,
                (heading ? 25f : 16f) * textScale, heading ? UiTheme.Heading : UiTheme.Paper, TextAlignmentOptions.MidlineLeft);
            label.text = Localisation.Text(text);
            label.textWrappingMode = TextWrappingModes.Normal;
            // Keep repeated paragraphs distinct even when a redraw interleaves action rows.
            int occurrence = 0;
            foreach (var item in entries)
                if (item.StaticFocusId != null && item.Label == label.text) occurrence++;
            entries.Add(new Entry { Label = label.text, StaticFocusId = "copy/" + occurrence + "/" + label.text,
                Rect = label.rectTransform, Scroll = summary,
                Role = heading ? AccessibilityRole.Header : AccessibilityRole.StaticText });
        }

        public RawImage Portrait()
        {
            var box = new GameObject("Portrait", typeof(RectTransform), typeof(LayoutElement));
            box.transform.SetParent(summary.content, false);
            box.GetComponent<LayoutElement>().preferredHeight = 108f;
            var picture = new GameObject("Photo", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            picture.rectTransform.SetParent(box.transform, false);
            picture.rectTransform.sizeDelta = new Vector2(108, 108);
            picture.raycastTarget = false;
            return picture;
        }

        // A status stays readable as a field changes. A correction makes the same line
        // focusable without replacing the input the player is editing.
        internal Action<string, bool> StatusLink(string name, Action follow)
        {
            var label = HudPrimitives.Label(name, summary.content, 16f * textScale, UiTheme.Paper, TextAlignmentOptions.MidlineLeft);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = true;
            label.gameObject.AddComponent<LayoutElement>().minHeight = 48f;
            var button = label.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = label;
            button.onClick.AddListener(() => { if (CanAct(button)) follow(); });
            int index = entries.Count;
            entries.Add(new Entry { Rect = label.rectTransform, Control = button, Scroll = summary });
            return (message, navigable) =>
            {
                string words = Localisation.Text(message);
                if (label.text == words && button.interactable == navigable) return;
                label.text = words;
                label.color = navigable ? UiTheme.Warning : UiTheme.Paper;
                label.fontStyle = navigable ? FontStyles.Underline : FontStyles.Normal;
                button.interactable = navigable;
                var entry = entries[index];
                entry.Label = words;
                entry.Role = navigable ? AccessibilityRole.Button : AccessibilityRole.StaticText;
                entries[index] = entry;
                if (controlNodes.TryGetValue(button, out var node))
                {
                    node.label = words;
                    node.role = entry.Role;
                }
                framesDirty = true;
            };
        }

        public TMP_InputField Search(string caption, string current, Action<string> edit, Action<string> submit)
            => Input(caption, caption, current, 100, false, edit, submit);

        public TMP_InputField LabelledInput(string name, string caption, string current, int limit, bool multiline, Action<string> edit)
        {
            var label = HudPrimitives.Label("Field label", choices.content, 16f * textScale, UiTheme.Heading, TextAlignmentOptions.MidlineLeft);
            label.text = Localisation.Text(caption);
            label.textWrappingMode = TextWrappingModes.Normal;
            var field = Input(name, caption, current, limit, multiline, edit, null);
            field.shouldActivateOnSelect = false;
            field.shouldHideSoftKeyboard = false;
            field.shouldHideMobileInput = false;
            field.restoreOriginalTextOnEscape = false;
            field.onSelect.AddListener(_ => editingField = field);
            return field;
        }

        private TMP_InputField Input(string name, string caption, string current, int limit, bool multiline,
            Action<string> edit, Action<string> submit)
        {
            var box = HudPrimitives.Fill(name, choices.content, UiTheme.SurfaceRaised, 8);
            box.GetComponent<Image>().raycastTarget = true;
            box.gameObject.AddComponent<RectMask2D>();
            box.gameObject.AddComponent<LayoutElement>().preferredHeight = Mathf.Max(48f, textScale * (multiline ? 86f : 36f));
            var field = box.gameObject.AddComponent<Gamesim.Episode.EpisodeSpeechInputField>();
            var align = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
            var text = HudPrimitives.Label("Search text", box, 17f * textScale, UiTheme.Paper, align);
            var hint = HudPrimitives.Label("Placeholder", box, 17f * textScale, UiTheme.Muted, align);
            foreach (var label in new[] { text, hint })
            {
                Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(10, multiline ? 6 : 0);
                label.rectTransform.offsetMax = new Vector2(-10, multiline ? -6 : 0);
            }
            hint.text = Localisation.Text(caption);
            field.textViewport = box;
            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            field.richText = false;
            field.characterLimit = limit;
            field.text = current;
            field.onValueChanged.AddListener(value =>
            {
                if (!Shown || !field.isActiveAndEnabled || !field.IsInteractable()) return;
                edit(value);
                if (controlNodes.TryGetValue(field, out var node)) node.value = value;
            });
            if (submit != null)
                field.onSubmit.AddListener(value => { if (Shown && field.isActiveAndEnabled && field.IsInteractable()) submit(value); });
            entries.Add(new Entry { Label = hint.text, Rect = box, Scroll = choices, Role = AccessibilityRole.TextField, Field = field });
            return field;
        }

        public Button ActionRow(string caption, Action action, bool primary = false, bool selected = false, bool enabled = true,
            Gamesim.Simulation.CharacterProfile profile = null)
        {
            var row = HudPrimitives.Fill(caption, choices.content, primary ? UiTheme.ActionBlue : UiTheme.SurfaceRaised, 8);
            var graphic = row.GetComponent<Image>();
            graphic.raycastTarget = true;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 7, 7);
            layout.childControlWidth = layout.childControlHeight = true;
            row.gameObject.AddComponent<LayoutElement>().minHeight = 48f;
            if (profile != null)
            {
                var cell = new GameObject("Houseguest portrait", typeof(RectTransform), typeof(LayoutElement));
                cell.transform.SetParent(row, false);
                var size = cell.GetComponent<LayoutElement>();
                size.minWidth = size.preferredWidth = 42;
                size.flexibleWidth = 0;
                layout.childForceExpandWidth = false;
                layout.spacing = 8;
                CharacterProfileBrowser.Thumbnail(cell.transform, profile, 0);
            }
            var label = HudPrimitives.Label("Label", row, 17f * textScale, UiTheme.Paper, TextAlignmentOptions.MidlineLeft);
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            label.text = selected ? Localisation.Text("Selected") + " · " + Localisation.Text(caption) : Localisation.Text(caption);
            label.textWrappingMode = TextWrappingModes.Normal;
            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.interactable = enabled && action != null;
            button.onClick.AddListener(() => { if (CanAct(button)) action(); });
            entries.Add(new Entry { Label = label.text, Rect = row, Scroll = choices, Control = button,
                Role = AccessibilityRole.Button, Selected = selected });
            return button;
        }

        public Func<bool> OwnsInput { get; set; }

        internal Button AddBack(string caption, Action action) =>
            AddCorner((frame, press) => WayOut.AddBack(frame, caption, press, textScale), action);

        internal Button AddClose(Action action) =>
            AddCorner((frame, press) => WayOut.AddClose(frame, press, textScale), action);

        internal Button AddCorner(Func<RectTransform, Action, Button> build, Action action)
        {
            Button button = null;
            void Press() { if (CanAct(button)) action(); }
            button = build(safeContent, Press);
            var rect = (RectTransform)button.transform;
            if (rect.anchorMin.x == 1f) closeRect = rect;
            else
            {
                backRect = rect;
                preferredBackWidth = rect.sizeDelta.x;
                button.GetComponentInChildren<TMP_Text>(true).textWrappingMode = TextWrappingModes.Normal;
            }
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(48f, rect.rect.height));
            float top = rect.anchoredPosition.y - rect.rect.height - 8f;
            summary.viewport.offsetMax = new Vector2(summary.viewport.offsetMax.x, top);
            choices.viewport.offsetMax = new Vector2(choices.viewport.offsetMax.x, top);
            FitWayBack();
            entries.Add(new Entry { Label = WayOut.CaptionOf(button), Rect = rect,
                Control = button, Role = AccessibilityRole.Button });
            return button;
        }

        private void FitWayBack()
        {
            if (backRect == null) return;
            float room = safeContent.rect.width - 2f * WayOut.Inset(textScale);
            if (closeRect != null) room -= closeRect.rect.width + 8f;
            float width = Mathf.Clamp(preferredBackWidth, 48f, Mathf.Max(48f, room));
            if (backRect.sizeDelta.x != width) backRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        public void SetActionEnabled(Button control, bool enabled)
        {
            if (control.interactable == enabled) return;
            control.interactable = enabled;
            if (!controlNodes.TryGetValue(control, out var node)) return;
            if (enabled) node.state &= ~AccessibilityState.Disabled;
            else node.state |= AccessibilityState.Disabled;
        }
        private bool Shown => this != null && isActiveAndEnabled && (OwnsInput == null || OwnsInput()) && (owner == null || owner.interactable && owner.blocksRaycasts && owner.alpha > 0);
        private bool CanAct(Button button) => Shown && button != null && button.IsActive() && button.IsInteractable();

        public float ScrollPosition => choices.verticalNormalizedPosition;
        internal float SummaryPosition => summary.verticalNormalizedPosition;

        internal bool HasEnabledAction(string name)
        {
            foreach (var entry in entries)
                if (entry.Rect.name == name && entry.Control != null && entry.Control.IsInteractable()) return true;
            return false;
        }
        internal bool HasFocusTarget(string id)
        {
            foreach (var entry in entries)
                if (entry.FocusId == id && (entry.Control == null || entry.Control.IsInteractable())) return true;
            return false;
        }

        internal string CurrentFocus
        {
            get
            {
                // An avatar rebuild can redraw the form twice before LateUpdate lands the requested focus.
                if (focusPending) return returnFocus;
                if (readerFocus != null && hierarchy != null) return readerFocus;
                var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                return selected != null && selected.transform.IsChildOf(transform) ? selected.name : null;
            }
        }

        public void Restore(float position, string focus = null, float summaryPosition = 1f)
        {
            Canvas.ForceUpdateCanvases();
            summary.verticalNormalizedPosition = Mathf.Clamp01(summaryPosition);
            choices.verticalNormalizedPosition = Mathf.Clamp01(position);
            returnFocus = focus;
            focusPending = focus != null;
        }

        internal void FocusControl(string name)
        {
            foreach (var entry in entries)
            {
                if (entry.FocusId != name) continue;
                returnFocus = readerFocus = name;
                focusPending = true;
                Selectable control = entry.Field != null ? (Selectable)entry.Field : entry.Control;
                if (control == null) return;
                control.Select();
                if (entry.Field != null) entry.Field.ActivateInputField();
                if (controlNodes.TryGetValue(control, out var node))
                    AssistiveSupport.notificationDispatcher.SendLayoutChanged(node);
                return;
            }
        }

        public static Rect NormalizedSafeArea(Rect pixels, int width, int height)
        {
            if (width <= 0 || height <= 0 || pixels.width <= 0 || pixels.height <= 0) return new Rect(0, 0, 1, 1);
            return Rect.MinMaxRect(Mathf.Clamp01(pixels.xMin / width), Mathf.Clamp01(pixels.yMin / height),
                Mathf.Clamp01(pixels.xMax / width), Mathf.Clamp01(pixels.yMax / height));
        }

        private void RefreshSafeArea()
        {
            var insets = NormalizedSafeArea(UnityEngine.Device.Screen.safeArea,
                UnityEngine.Device.Screen.width, UnityEngine.Device.Screen.height);
            if (editingField != null && editingField.isFocused && TouchScreenKeyboard.visible)
            {
                var keyboard = TouchScreenKeyboard.area;
                float height = Mathf.Max(1, UnityEngine.Device.Screen.height);
                if (keyboard.height > 0 && keyboard.yMin <= insets.yMin * height && keyboard.yMax < insets.yMax * height)
                    insets.yMin = Mathf.Max(insets.yMin, keyboard.yMax / height);
            }
            if (safeContent.anchorMin == insets.min && safeContent.anchorMax == insets.max) return;
            safeContent.anchorMin = insets.min;
            safeContent.anchorMax = insets.max;
            framesDirty = true;
            if (editingField != null && editingField.isFocused)
            {
                Canvas.ForceUpdateCanvases();
                Reveal((RectTransform)editingField.transform, choices);
            }
        }

        private void LateUpdate()
        {
            RefreshSafeArea();
            FitWayBack();
            if (focusPending && Shown)
            {
                focusPending = false;
                foreach (var entry in entries)
                {
                    if (entry.FocusId != returnFocus || entry.Control == null && entry.Field == null) continue;
                    if (entry.Control != null && !entry.Control.IsInteractable()) continue;
                    Reveal(entry.Rect, entry.Scroll);
                    var events = EventSystem.current;
                    if (events != null && !events.alreadySelecting) events.SetSelectedGameObject(entry.Rect.gameObject);
                    break;
                }
            }
            var selection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selection != selectedRow)
            {
                selectedRow = selection;
                if (selection != null && selection.transform.IsChildOf(transform))
                    foreach (var entry in entries)
                        if (entry.Rect.gameObject == selection) { Reveal(entry.Rect, entry.Scroll); break; }
            }
            if (!PhoneDisplay.Active) return;
            if (!Shown || !AssistiveSupport.isScreenReaderEnabled)
            {
                ReleaseReader();
                return;
            }
            if (hierarchy != null)
            {
                if (framesDirty) hierarchy.RefreshNodeFrames();
                framesDirty = false;
                return;
            }
            hierarchy = new AccessibilityHierarchy();
            AccessibilityNode destination = null;
            foreach (var entry in entries)
            {
                var node = hierarchy.AddNode(entry.Label);
                node.role = entry.Role;
                if (entry.Selected) node.state |= AccessibilityState.Selected;
                if (entry.Control != null)
                {
                    controlNodes.Add(entry.Control, node);
                    if (entry.Role == AccessibilityRole.Button && !entry.Control.IsInteractable()) node.state |= AccessibilityState.Disabled;
                    node.invoked += () =>
                    {
                        if (!CanAct(entry.Control)) return false;
                        entry.Control.onClick.Invoke();
                        return true;
                    };
                }
                if (entry.Field != null)
                {
                    controlNodes.Add(entry.Field, node);
                    node.value = entry.Field.text;
                    node.invoked += () =>
                    {
                        if (!Shown || !entry.Field.IsInteractable()) return false;
                        entry.Field.Select();
                        entry.Field.ActivateInputField();
                        return true;
                    };
                }
                node.frameGetter = () => Frame(entry.Rect);
                node.focusChanged += (_, focused) =>
                {
                    if (!focused || !Shown) return;
                    readerFocus = entry.FocusId;
                    Reveal(entry.Rect, entry.Scroll);
                };
                if (entry.FocusId == returnFocus) destination = node;
            }
            AssistiveSupport.activeHierarchy = hierarchy;
            if (destination != null) AssistiveSupport.notificationDispatcher.SendLayoutChanged(destination);
        }

        private Rect Frame(RectTransform rect)
        {
            if (rect == null) return Rect.zero;
            rect.GetWorldCorners(corners);
            Vector2 bottom = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 top = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return Rect.MinMaxRect(bottom.x, Screen.height - top.y, top.x, Screen.height - bottom.y);
        }

        private static void Reveal(RectTransform item, ScrollRect scroll)
        {
            if (item == null || scroll == null) return;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, item);
            var window = scroll.viewport.rect;
            float overflow = scroll.content.rect.height - window.height;
            if (overflow <= 0) return;
            float dy = bounds.max.y > window.yMax ? bounds.max.y - window.yMax
                : Mathf.Min(0, bounds.min.y - window.yMin);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + dy / overflow);
        }

        public void ReleaseReader()
        {
            if (hierarchy == null) return;
            if (AssistiveSupport.activeHierarchy == hierarchy) AssistiveSupport.activeHierarchy = null;
            hierarchy = null;
            controlNodes.Clear();
        }

        private void OnRectTransformDimensionsChange() => framesDirty = true;

        private void OnDisable() => ReleaseReader();
    }
}
