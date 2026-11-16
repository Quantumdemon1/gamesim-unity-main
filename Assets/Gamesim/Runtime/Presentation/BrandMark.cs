using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The one brand mark (the menu review's quick win 17). The opening's title card drew an eye
    /// and every other screen - the HUD's top bar, the front door, the cast screen and the
    /// creator - a house. Every place a mark shows takes it from here, so it is the same picture
    /// everywhere and changing it is one constant: <see cref="Current"/>.
    /// </summary>
    public static class BrandMark
    {
        public enum Kind { House, Eye }

        /// <summary>The mark every screen shows. The house, the one most screens drew; the owner may choose the eye.</summary>
        public const Kind Current = Kind.House;

        /// <summary>The glyph name of the mark in the house's icon set, the fallback when the packs are not installed.</summary>
        public static string Glyph => Current == Kind.Eye ? "eye" : "house";

        /// <summary>The mark's picture: the pack's outline where the packs are installed, else the icon set's glyph; null with neither.</summary>
        public static Sprite Sprite()
        {
            var pack = UiTheme.Pack(Current == Kind.Eye ? PackArt.KitIconEye : PackArt.IconHome);
            return pack != null ? pack : UiTheme.Icon(Glyph);
        }

        /// <summary>
        /// The mark as an image on <paramref name="parent"/>, in <paramref name="colour"/>, kept to
        /// its aspect and deaf to clicks; null when there is no picture to draw.
        /// </summary>
        public static Image Draw(string name, Transform parent, Color colour)
        {
            var sprite = Sprite();
            if (sprite == null) return null;
            var mark = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            mark.rectTransform.SetParent(parent, false);
            mark.sprite = sprite; mark.color = colour; mark.preserveAspect = true; mark.raycastTarget = false;
            return mark;
        }
    }
}
