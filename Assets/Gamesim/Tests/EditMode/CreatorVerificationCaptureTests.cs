using System.Reflection;
using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    public sealed class CreatorVerificationCaptureTests
    {
        [TestCase(9, 9, true)]
        [TestCase(6, 6, false)]
        [TestCase(8, 9, false)]
        [TestCase(9, 8, false)]
        [TestCase(10, 9, false)]
        [TestCase(9, 10, false)]
        public void CompleteMeansEveryFrameAtEverySupportedTextSize(int detailed, int quick, bool complete)
        {
            Assert.That(TextSizes.All, Is.EqualTo(new[] { TextSize.Standard, TextSize.Larger, TextSize.Largest }));
            foreach (string name in new[] { "CreatorDetailedFrames", "CreatorQuickFrames" })
            {
                var field = typeof(PortVerification).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, name);
                var frames = (Vector2Int[])field.GetValue(null);
                Assert.That(frames.Length, Is.EqualTo(3));
                Assert.That(frames, Is.Unique, name);
            }
            var check = typeof(PortVerification).GetMethod("CreatorResolutionCapturesComplete", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(check, Is.Not.Null);
            Assert.That((bool)check.Invoke(null, new object[] { detailed, quick }), Is.EqualTo(complete));
        }
    }
}
