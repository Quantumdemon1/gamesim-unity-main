using System;
using Gamesim.Episode;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    public sealed class VerificationCaptureValidationTests
    {
        [Test]
        public void BlackNativeFrameIsRejected()
        {
            var frame = new VerificationFrameEvidence();
            Assert.That(VerificationCaptureValidation.Inspect(Png(_ => Color.black), 64, 64, frame), Is.False);
            Assert.That(frame.rendered, Is.False);
            Assert.That(frame.nonDarkSamples, Is.Zero);
            Assert.That(frame.failure, Does.Contain("blank"));
        }

        [Test]
        public void UniformBrightNativeFrameIsRejected()
        {
            var frame = new VerificationFrameEvidence();
            Assert.That(VerificationCaptureValidation.Inspect(Png(_ => Color.white), 64, 64, frame), Is.False);
            Assert.That(frame.nonDarkSamples, Is.EqualTo(256));
            Assert.That(frame.varyingSamples, Is.Zero);
            Assert.That(frame.rendered, Is.False);
        }

        [Test]
        public void TruncatedPngNeverReachesTheImageDecoder()
        {
            var complete = Png(Pattern);
            foreach (int length in new[] { 0, 8, complete.Length - 1, complete.Length - 12 })
            {
                var partial = new byte[length]; Array.Copy(complete, partial, length);
                var frame = new VerificationFrameEvidence();
                Assert.That(VerificationCaptureValidation.IsCompletePng(partial), Is.False);
                Assert.That(VerificationCaptureValidation.Inspect(partial, 64, 64, frame), Is.False);
                Assert.That(frame.failure, Does.Contain("complete PNG"));
                Assert.That(frame.width, Is.Zero);
            }
        }

        [Test]
        public void WrongViewportSizeIsRejectedEvenWhenTheFrameHasContent()
        {
            var frame = new VerificationFrameEvidence();
            Assert.That(VerificationCaptureValidation.Inspect(Png(Pattern), 128, 64, frame), Is.False);
            Assert.That(frame.width, Is.EqualTo(64));
            Assert.That(frame.failure, Does.Contain("dimensions"));
            Assert.That(frame.rendered, Is.False);
        }

        [Test]
        public void CompleteVariedFrameAtTheRequestedSizeIsRecordedWithoutClaimingVisualQuality()
        {
            var bytes = Png(Pattern);
            var frame = new VerificationFrameEvidence();
            Assert.That(VerificationCaptureValidation.IsCompletePng(bytes), Is.True);
            Assert.That(VerificationCaptureValidation.Inspect(bytes, 64, 64, frame), Is.True);
            Assert.That(frame.rendered, Is.True);
            Assert.That(frame.sampledPixels, Is.EqualTo(256));
            Assert.That(frame.nonDarkSamples, Is.EqualTo(128));
            Assert.That(frame.varyingSamples, Is.EqualTo(128));
            Assert.That(frame.failure, Is.Null);
        }

        private static Color Pattern(int index) => index % 64 < 32 ? Color.white : Color.black;

        private static byte[] Png(Func<int, Color> color)
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color[64 * 64];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = color(i);
                texture.SetPixels(pixels); texture.Apply();
                return texture.EncodeToPNG();
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
