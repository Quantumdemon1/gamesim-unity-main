using System;
using UnityEngine;

namespace Gamesim.Episode
{
    [Serializable]
    public sealed class VerificationFrameEvidence
    {
        public string path, failure;
        public int width, height, sampledPixels, nonDarkSamples, varyingSamples;
        public bool rendered;
    }

    /// <summary>Rejects incomplete, wrong-size and blank native captures; never establishes visual quality.</summary>
    public static class VerificationCaptureValidation
    {
        public static bool IsCompletePng(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 33) return false;
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            byte[] ending = { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
            for (int i = 0; i < signature.Length; i++) if (bytes[i] != signature[i]) return false;
            for (int i = 0; i < ending.Length; i++) if (bytes[bytes.Length - ending.Length + i] != ending[i]) return false;
            return true;
        }

        public static bool Inspect(byte[] bytes, int expectedWidth, int expectedHeight, VerificationFrameEvidence evidence)
        {
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));
            evidence.rendered = false; evidence.failure = null;
            evidence.width = evidence.height = evidence.sampledPixels = evidence.nonDarkSamples = evidence.varyingSamples = 0;
            if (!IsCompletePng(bytes)) return Reject(evidence, "Capture was not written as a complete PNG.");
            var pixels = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!pixels.LoadImage(bytes)) return Reject(evidence, "Capture could not be decoded as a PNG.");
                evidence.width = pixels.width; evidence.height = pixels.height;
                if (pixels.width != expectedWidth || pixels.height != expectedHeight)
                    return Reject(evidence, "Capture dimensions do not match the requested viewport.");
                var colors = pixels.GetPixels32();
                var first = colors[0];
                for (int i = 0; i < colors.Length; i += 16)
                {
                    var color = colors[i];
                    evidence.sampledPixels++;
                    if (Math.Max(color.r, Math.Max(color.g, color.b)) >= 32) evidence.nonDarkSamples++;
                    if (Math.Abs(color.r - first.r) >= 8 || Math.Abs(color.g - first.g) >= 8 || Math.Abs(color.b - first.b) >= 8)
                        evidence.varyingSamples++;
                }
                if (evidence.nonDarkSamples < 128 || evidence.varyingSamples < 128)
                    return Reject(evidence, "The native captured frame is blank or uniformly colored.");
                evidence.rendered = true;
                return true;
            }
            finally
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(pixels);
                else UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static bool Reject(VerificationFrameEvidence evidence, string reason)
        { evidence.failure = reason; return false; }
    }
}
