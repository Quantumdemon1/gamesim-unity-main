using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The three shapes the broadcast overlays are built from: a disc, a themed panel, and a label.
    ///
    /// <para>Extracted at the third copy. <see cref="CastRail"/>, <see cref="CeremonyTakeover"/> and
    /// <see cref="VoteReveal"/> each need a circular portrait frame and a non-raycasting label, and
    /// three private copies is where they start to disagree — one forgets <c>raycastTarget</c>, one
    /// forgets the font fallback, and the bug shows up as an overlay quietly eating a click.</para>
    ///
    /// <para>Everything here is inert by construction: no graphic takes raycasts. Overlays that sit
    /// over the set during the most consequential seconds of the episode must not be able to swallow
    /// input, and making that a property of the primitives is stronger than remembering it at each
    /// call site.</para>
    /// </summary>
    public static class HudPrimitives
    {
        /// <summary>A filled circle. Used for portrait frames, status rings and ceremony marks.</summary>
        public static RectTransform Disc(string name, Transform parent, Color colour)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var image = rect.GetComponent<Image>();
            image.sprite = UiTheme.Circle();
            image.type = Image.Type.Simple;
            image.color = colour;
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>
        /// A themed rounded rectangle. Always routed through <see cref="UiTheme.Style"/>: an Image
        /// with a bare colour and no sprite does not draw, which cost a debugging pass when a
        /// full-screen scrim silently rendered nothing.
        /// </summary>
        public static RectTransform Fill(string name, Transform parent, Color colour, int radius)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var image = rect.GetComponent<Image>();
            UiTheme.Style(image, colour, Mathf.Max(1, radius));
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>A non-interactive label, with the project's font fallback already applied.</summary>
        public static TMP_Text Label(string name, Transform parent, float size, Color colour,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var holder = new GameObject(name, typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            var label = holder.AddComponent<TextMeshProUGUI>();
            var font = TMP_Settings.defaultFontAsset != null
                ? TMP_Settings.defaultFontAsset
                : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null) label.font = font;
            label.fontSize = size;
            label.color = colour;
            // Event and cast text is authored copy, never markup; the HUD reads it literally too.
            label.richText = false;
            label.raycastTarget = false;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Truncate;
            return label;
        }

        /// <summary>
        /// A circular portrait, masked, with a status ring behind it and a graceful hole when the
        /// persona has no authored art.
        /// </summary>
        public static RectTransform Portrait(
            Transform parent, Texture face, Color ring, float diameter, float ringWidth, bool dim)
        {
            var rim = Disc("Ring", parent, ring);
            rim.sizeDelta = new Vector2(diameter + ringWidth * 2f, diameter + ringWidth * 2f);

            var frame = Disc("Frame", rim, dim ? new Color(1f, 1f, 1f, .45f) : Color.white);
            frame.anchorMin = new Vector2(.5f, .5f);
            frame.anchorMax = new Vector2(.5f, .5f);
            frame.pivot = new Vector2(.5f, .5f);
            frame.anchoredPosition = Vector2.zero;
            frame.sizeDelta = new Vector2(diameter, diameter);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            if (face != null)
            {
                var raw = new GameObject("Face", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                raw.rectTransform.SetParent(frame, false);
                raw.rectTransform.anchorMin = Vector2.zero;
                raw.rectTransform.anchorMax = Vector2.one;
                raw.rectTransform.offsetMin = Vector2.zero;
                raw.rectTransform.offsetMax = Vector2.zero;
                raw.texture = face;
                raw.raycastTarget = false;
                raw.color = dim ? new Color(.6f, .65f, .7f, 1f) : Color.white;
            }
            else
            {
                var hole = Disc("Initial", frame, UiTheme.SurfaceRaised);
                hole.anchorMin = Vector2.zero; hole.anchorMax = Vector2.one;
                hole.offsetMin = Vector2.zero; hole.offsetMax = Vector2.zero;
            }
            return rim;
        }
    }
}
