using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The builders: the only way a way-out control is made. One look everywhere - the glass of a
    /// secondary button in the theme's colours, a hairline edge, and a focus ring that shows only
    /// while the control is selected, so focus never reads as a second selection - at the top left
    /// for a way back and the top right for Close, on a full screen and on a HUD panel alike.
    /// </summary>
    public static partial class WayOut
    {
        /// <summary>
        /// Adds a way back at the top left of <paramref name="frame"/>. <paramref name="caption"/> is
        /// made by <see cref="BackTo"/> or <see cref="BackToKeeping"/> (anything else throws). The
        /// object is named <see cref="BackObjectName"/>; its first text is the caption.
        /// </summary>
        public static Button AddBack(RectTransform frame, string caption, Action action, float textScale = 1f, string keyHint = null)
        {
            if (!IsWayBack(caption)) throw new ArgumentException("A way back is worded \"Back to <place>\": use WayOut.BackTo.", nameof(caption));
            return Make(frame, BackObjectName, caption, action, textScale, keyHint, left: true);
        }

        /// <summary>
        /// Adds the one Close at the top right of <paramref name="frame"/>, captioned
        /// <see cref="CloseCaption"/> and named <see cref="CloseObjectName"/>.
        /// </summary>
        public static Button AddClose(RectTransform frame, Action action, float textScale = 1f, string keyHint = null)
            => Make(frame, CloseObjectName, CloseCaption, action, textScale, keyHint, left: false);

        /// <summary>The caption a way out carries: its first text, the one tests and screen readers find it by.</summary>
        public static string CaptionOf(Button control)
        {
            var text = control != null ? control.GetComponentInChildren<TMP_Text>(true) : null;
            return text != null ? text.text : null;
        }

        /// <summary>The caption's size and the least auto-size may shrink it to (0, 0 when there is no caption).</summary>
        public static Vector2 CaptionSizes(Button control)
        {
            var text = control != null ? control.GetComponentInChildren<TMP_Text>(true) : null;
            return text != null ? new Vector2(text.fontSize, text.enableAutoSizing ? text.fontSizeMin : text.fontSize) : Vector2.zero;
        }

        /// <summary>The key hint drawn beside the caption ("Esc"), or null when it shows none.</summary>
        public static string KeyHintOf(Button control)
        {
            var hint = control != null ? control.transform.Find(KeyHintObjectName) : null;
            var text = hint != null ? hint.GetComponent<TMP_Text>() : null;
            return text != null ? text.text : null;
        }

        private static Button Make(RectTransform frame, string objectName, string caption, Action action, float textScale, string keyHint, bool left)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            float s = Scale(textScale), font = FontSize(textScale), height = Height(textScale), inset = Inset(textScale);
            var arrow = left ? UiTheme.Pack(PackArt.KitIconArrowBack) : null;
            float width = Width(caption, textScale, arrow != null, keyHint);

            var box = HudPrimitives.Fill(objectName, frame, UiTheme.SurfaceRaised, 10);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(left ? 0f : 1f, 1f);
            box.sizeDelta = new Vector2(width, height);
            box.anchoredPosition = new Vector2(left ? inset : -inset, -inset);
            var image = box.GetComponent<Image>();
            image.raycastTarget = true;
            if (!UiTheme.PackSliced(image, PackArt.ButtonSecondary, 12f)) image.color = UiTheme.SurfaceRaised;
            UiTheme.AddBorder(box, 10, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .55f));

            float padLeft = 14f * s, padRight = 14f * s;
            if (arrow != null)
            {
                float side = BaseIcon * s;
                var art = new GameObject("Glyph", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                art.rectTransform.SetParent(box, false);
                art.sprite = arrow; art.color = UiTheme.Paper; art.preserveAspect = true; art.raycastTarget = false;
                art.rectTransform.anchorMin = art.rectTransform.anchorMax = art.rectTransform.pivot = new Vector2(0f, .5f);
                art.rectTransform.sizeDelta = new Vector2(side, side);
                art.rectTransform.anchoredPosition = new Vector2(padLeft, 0f);
                padLeft += side + 10f * s;
            }
            if (!string.IsNullOrEmpty(keyHint))
            {
                // Beside the caption, never in it: the caption stays the control's name.
                var hint = HudPrimitives.Label(KeyHintObjectName, box, MinFontSize * Math.Max(1f, s), UiTheme.Muted, TextAlignmentOptions.MidlineRight);
                hint.text = keyHint; hint.textWrappingMode = TextWrappingModes.NoWrap;
                float hintWidth = keyHint.Length * MinFontSize * Math.Max(1f, s) * 0.62f;
                hint.rectTransform.anchorMin = new Vector2(1f, 0f); hint.rectTransform.anchorMax = new Vector2(1f, 1f);
                hint.rectTransform.pivot = new Vector2(1f, .5f);
                hint.rectTransform.sizeDelta = new Vector2(hintWidth, 0f);
                hint.rectTransform.anchoredPosition = new Vector2(-padRight, 0f);
                padRight += hintWidth + 8f * s;
            }

            // The caption first among the texts: tests and screen readers read it as the control's name.
            var label = HudPrimitives.Label("Label", box, font, UiTheme.Paper, arrow != null ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center);
            var drawnHint = box.Find(KeyHintObjectName);
            if (drawnHint != null) drawnHint.SetAsLastSibling();
            var weight = UiTheme.Font(UiTheme.Weight.SemiBold); if (weight != null) label.font = weight;
            label.text = Localisation.Text(caption);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true; label.fontSizeMax = font; label.fontSizeMin = MinFontSize;
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padLeft, 0f); rect.offsetMax = new Vector2(-padRight, 0f);

            // The screen the frame is on makes the control and dresses it as it does its other buttons
            // (IWayOutHost): the season's HUD gives it its hover tick, press dip and guarded press, and the
            // season report its own kind of button. A screen that is no host gets a plain Button.
            var host = HostOf(frame);
            var button = host != null ? host.AddControl(box.gameObject) : null;
            if (button == null) button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colours = button.colors;
            colours.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colours.selectedColor = colours.highlightedColor;
            button.colors = colours;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            var press = host != null ? host.Dress(button, action) : action;
            if (press != null) button.onClick.AddListener(() => press());

            // The focus ring: the theme's glow, outside the edge, lit only while selected.
            UiTheme.AddBorder(box, 10, UiTheme.Glow);
            var ring = box.GetChild(box.childCount - 1).GetComponent<Image>();
            ring.name = "Focus ring";
            ring.rectTransform.offsetMin = new Vector2(-4f, -4f); ring.rectTransform.offsetMax = new Vector2(4f, 4f);
            ring.raycastTarget = false;
            ring.enabled = false;
            box.gameObject.AddComponent<WayOutFocus>().Ring = ring;
            return button;
        }

        /// <summary>The object a way out on <paramref name="frame"/> asks to make and dress it, or null: the nearest above the frame.</summary>
        public static IWayOutHost HostOf(RectTransform frame) => frame != null ? frame.GetComponentInParent<IWayOutHost>(true) : null;

        /// <summary>Lights the ring while the control is selected - by keyboard, pad or pointer - and only then.</summary>
        internal sealed class WayOutFocus : MonoBehaviour, ISelectHandler, IDeselectHandler
        {
            public Image Ring;

            public void OnSelect(BaseEventData eventData) { if (Ring != null) Ring.enabled = true; }
            public void OnDeselect(BaseEventData eventData) { if (Ring != null) Ring.enabled = false; }

            private void OnEnable()
            {
                if (Ring != null) Ring.enabled = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            }
        }
    }

    /// <summary>
    /// A screen whose buttons are dressed in its own way, so that its way out is dressed as they are
    /// (the review of menu slice 2: the panel's Close had lost the HUD's hover tick). <see cref="WayOut"/>
    /// asks the nearest host above the frame; the look, place, caption, key hint and focus ring stay the
    /// helper's.
    /// </summary>
    public interface IWayOutHost
    {
        /// <summary>Adds the control to <paramref name="box"/>: a Button, or the screen's own kind of button.</summary>
        Button AddControl(GameObject box);

        /// <summary>
        /// Gives <paramref name="control"/> what the screen's other buttons carry (a hover, a press) and
        /// returns what a press runs: <paramref name="action"/>, or the screen's guard around it.
        /// </summary>
        Action Dress(Button control, Action action);
    }
}
