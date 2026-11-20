using System;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The colour a body's eyes are given, from the eye colour a look or a save names (its swatch).
    ///
    /// <para>UMA draws the iris as a grey texture multiplied by the eye colour. Given the swatch itself, a brown
    /// iris drew at about a seventh of the swatch's brightness, near black, and every dark-eyed houseguest had the
    /// same black disc. So the body is given the swatch divided by the texture's grey, in linear light (where the
    /// multiply happens): the iris then draws at the swatch. A light swatch whose quotient would pass 1 in a channel
    /// is scaled down whole, so it keeps its hue and draws as bright as the texture lets it.</para>
    ///
    /// <para>The saved appearance and the palette keep the swatch; only the colour handed to the body changes.
    /// Plain numbers in and out, no engine type, so the dotnet subset tests it.</para>
    /// </summary>
    public static class IrisTint
    {
        /// <summary>
        /// The grey of UMA's iris texture (UMA30_Eyes_Iris): its median opaque texel is 106 of 255 in sRGB, 0.1441 in
        /// linear light. The eye colour multiplies this grey, so dividing by it gives back the swatch.
        /// </summary>
        public const float IrisGreySrgb = 106f / 255f;

        /// <summary><see cref="IrisGreySrgb"/> in linear light: 0.1441.</summary>
        public static readonly float IrisGreyLinear = ToLinear(IrisGreySrgb);

        /// <summary>
        /// The eye colour to give the body for the swatch <paramref name="r"/>, <paramref name="g"/>, <paramref name="b"/>
        /// (sRGB, 0 to 1); the answer is sRGB too.
        /// </summary>
        public static void FromSwatch(float r, float g, float b, out float tintR, out float tintG, out float tintB)
        {
            float lr = ToLinear(Clamp01(r)) / IrisGreyLinear;
            float lg = ToLinear(Clamp01(g)) / IrisGreyLinear;
            float lb = ToLinear(Clamp01(b)) / IrisGreyLinear;
            float most = Math.Max(lr, Math.Max(lg, lb));
            if (most > 1f) { lr /= most; lg /= most; lb /= most; }
            tintR = ToSrgb(lr);
            tintG = ToSrgb(lg);
            tintB = ToSrgb(lb);
        }

        /// <summary>The sRGB curve's inverse: an sRGB value (0 to 1) in linear light.</summary>
        public static float ToLinear(float value) =>
            value <= .04045f ? value / 12.92f : (float)Math.Pow((value + .055) / 1.055, 2.4);

        /// <summary>The sRGB curve: a linear value (0 to 1) in sRGB.</summary>
        public static float ToSrgb(float value) =>
            value <= .0031308f ? value * 12.92f : (float)(1.055 * Math.Pow(value, 1 / 2.4) - .055);

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
