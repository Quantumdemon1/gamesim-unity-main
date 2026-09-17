using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// A houseguest's colour: the one their body wears in the house, and the one their card carries
    /// on the cast screen.
    ///
    /// <para>This lived as a private method on the episode director, which was fine while the only
    /// thing that needed a houseguest's colour was the body being dressed. The cast screen needs the
    /// same answer before any body exists, and a second table would drift — you would pick a card in
    /// one colour and meet that person wearing another.</para>
    ///
    /// <para>The five houseguests the project shipped with keep the colours they were authored in,
    /// so the original season looks exactly as it did. Everyone else gets a stable hue derived from
    /// their id, which spreads a cast of any size without anyone choosing sixteen colours by hand and
    /// gives the same person the same colour in every season they appear in.</para>
    /// </summary>
    public static class CastPalette
    {
        public static Color For(string id)
        {
            switch (ContentCatalog.CanonicalId(id))
            {
                case "maya-hassan":   return Hex("#476A88");
                case "taylor-kim":    return Hex("#CF6D52");
                case "jamie-roberts": return Hex("#7E9E87");
                case "casey-wilson":  return Hex("#C79C53");
                case "riley-johnson": return Hex("#807B9C");
            }
            uint hash = SeededRandom.HashSeed(id ?? string.Empty);
            return Color.HSVToRGB(hash % 360u / 360f, 0.34f, 0.62f);
        }

        private static Color Hex(string value)
            => ColorUtility.TryParseHtmlString(value, out var colour) ? colour : Color.grey;
    }
}
