using System;
using System.Numerics;
using Gamesim.Episode;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The conversation's soft key (LT6, <see cref="ConversationKeyLight"/>): while the two-shot holds, an
    /// unshadowed spot on the camera's side of the pair, aimed at the middle of their heads, its even part
    /// covering both faces, reaching no further than four metres, easing in and out over a third of a
    /// second and cutting under reduced motion. Plain C#: it runs in the Unity-free suite and under the
    /// editor's Mono. Units are metres, y up.
    /// </summary>
    public sealed class ConversationKeyLightTests
    {
        /// <summary>Pairs as a two-shot sees them: two heads and the camera's eye, 4.6 m back and pitched down.</summary>
        private static readonly (string What, Vector3 First, Vector3 Second, Vector3 Eye)[] Pairs =
        {
            ("Standing, across the frame", new Vector3(-.7f, 1.62f, 0f), new Vector3(.7f, 1.58f, 0f), new Vector3(.3f, 3.5f, -4.2f)),
            ("Standing, the camera to their side", new Vector3(2f, 1.6f, 1.4f), new Vector3(2f, 1.7f, 2.6f), new Vector3(6.2f, 3.4f, 2.2f)),
            ("One seated, one standing, on a diagonal", new Vector3(-3f, 1.05f, 5f), new Vector3(-2.1f, 1.65f, 5.9f), new Vector3(-6.4f, 3.2f, 8.7f)),
            ("Two metres apart", new Vector3(-1f, 1.6f, 0f), new Vector3(1f, 1.6f, 0f), new Vector3(0f, 3.5f, -4.2f)),
        };

        [Test]
        public void TheKeyStandsOnTheCamerasSideOfThePairAndAimsAtTheMiddleOfTheirHeads()
        {
            foreach (var pair in Pairs)
            {
                var placed = ConversationKeyLight.Place(pair.First, pair.Second, pair.Eye);
                var middle = (pair.First + pair.Second) * .5f;
                var toEye = pair.Eye - middle;
                var fromMiddle = placed.Position - middle;
                Assert.That(Vector3.Dot(fromMiddle, toEye), Is.GreaterThan(0f), pair.What + ": the key is on the camera's side of the pair.");
                var flatToEye = Vector3.Normalize(new Vector3(toEye.X, 0f, toEye.Z));
                Assert.That(Vector3.Dot(new Vector3(fromMiddle.X, 0f, fromMiddle.Z), flatToEye), Is.EqualTo(ConversationKeyLight.Toward).Within(1e-3f),
                    pair.What + ": it stands 1.5 m from the middle of the heads toward the camera, along the floor,");
                Assert.That(fromMiddle.Y, Is.EqualTo(ConversationKeyLight.Lift).Within(1e-3f), pair.What + ": a little above the heads,");
                var cameraRight = Vector3.Cross(Vector3.UnitY, -flatToEye);
                Assert.That(Vector3.Dot(fromMiddle, cameraRight), Is.EqualTo(ConversationKeyLight.Side).Within(1e-3f), pair.What + ": and to the camera's right.");
                Assert.That(placed.Forward.Length(), Is.EqualTo(1f).Within(1e-4f), pair.What + ": its aim is a direction,");
                Assert.That(ConversationKeyLight.AngleBetween(placed.Forward, middle - placed.Position), Is.LessThan(.05f),
                    pair.What + ": aimed at the middle of the two heads.");
            }
        }

        [Test]
        public void BothFacesSitInsideTheEvenPartOfTheCone()
        {
            foreach (var pair in Pairs)
            {
                var placed = ConversationKeyLight.Place(pair.First, pair.Second, pair.Eye);
                foreach (var head in new[] { pair.First, pair.Second })
                {
                    var to = head - placed.Position;
                    float face = (float)(Math.Atan(ConversationKeyLight.FaceRadius / to.Length()) * 180.0 / Math.PI);
                    Assert.That(ConversationKeyLight.AngleBetween(placed.Forward, to) + face, Is.LessThanOrEqualTo(placed.InnerSpotAngle * .5f + 1e-3f),
                        pair.What + ": a face, edge to edge, is inside the cone's even part (" + placed.InnerSpotAngle + " degrees).");
                }
                Assert.That(placed.SpotAngle, Is.EqualTo(placed.InnerSpotAngle + ConversationKeyLight.Softness).Within(1e-3f),
                    pair.What + ": with a soft edge outside it,");
                Assert.That(placed.SpotAngle, Is.LessThanOrEqualTo(ConversationKeyLight.MaxSpotAngle), pair.What + ": no wider than the widest cone.");
            }
        }

        [Test]
        public void ItReachesBothFacesAndNeverPastFourMetres()
        {
            foreach (var pair in Pairs)
            {
                var placed = ConversationKeyLight.Place(pair.First, pair.Second, pair.Eye);
                float farther = Math.Max((pair.First - placed.Position).Length(), (pair.Second - placed.Position).Length());
                Assert.That(placed.Range, Is.GreaterThanOrEqualTo(farther + 1f), pair.What + ": it reaches past the farther face,");
                Assert.That(placed.Range, Is.LessThanOrEqualTo(ConversationKeyLight.MaxRange), pair.What + ": and no further than four metres.");
            }
            var apart = ConversationKeyLight.Place(new Vector3(-3f, 1.6f, 0f), new Vector3(3f, 1.6f, 0f), new Vector3(0f, 3.5f, -4.2f));
            Assert.That(apart.Range, Is.EqualTo(ConversationKeyLight.MaxRange), "A pair six metres apart: the reach stops at four metres.");
            var overhead = ConversationKeyLight.Place(new Vector3(-.7f, 1.6f, 0f), new Vector3(.7f, 1.6f, 0f), new Vector3(0f, 6f, 0f));
            Assert.That(float.IsNaN(overhead.Position.X) || float.IsNaN(overhead.Forward.X) || float.IsNaN(overhead.SpotAngle), Is.False,
                "A camera straight overhead still places the key,");
            Assert.That(overhead.Range, Is.LessThanOrEqualTo(ConversationKeyLight.MaxRange), "within four metres.");
        }

        [Test]
        public void ItEasesInAndOutOverAThirdOfASecondAndCutsUnderReducedMotion()
        {
            float weight = 0f;
            weight = ConversationKeyLight.Step(weight, true, .1f, false);
            weight = ConversationKeyLight.Step(weight, true, .1f, false);
            Assert.That(weight, Is.GreaterThan(.5f).And.LessThan(1f), "A fifth of a second into the two-shot the key is on its way up,");
            weight = ConversationKeyLight.Step(weight, true, .1f, false);
            weight = ConversationKeyLight.Step(weight, true, .1f, false);
            Assert.That(weight, Is.EqualTo(1f), "and full once the third of a second is past.");
            Assert.That(ConversationKeyLight.Step(weight, true, .1f, false), Is.EqualTo(1f), "Held, it stays full.");
            weight = ConversationKeyLight.Step(weight, false, .1f, false);
            weight = ConversationKeyLight.Step(weight, false, .1f, false);
            Assert.That(weight, Is.GreaterThan(0f).And.LessThan(.5f), "The two-shot gone, a fifth of a second on it is on its way down,");
            weight = ConversationKeyLight.Step(weight, false, .1f, false);
            weight = ConversationKeyLight.Step(weight, false, .1f, false);
            Assert.That(weight, Is.EqualTo(0f), "and out once the third of a second is past.");
            Assert.That(ConversationKeyLight.Step(.4f, true, -1f, false), Is.EqualTo(.4f), "A frame that runs backwards moves nothing.");

            Assert.That(ConversationKeyLight.Step(0f, true, .016f, true), Is.EqualTo(1f), "Under reduced motion it cuts on,");
            Assert.That(ConversationKeyLight.Step(1f, false, .016f, true), Is.EqualTo(0f), "and cuts off.");

            Assert.That(ConversationKeyLight.IntensityAt(0f), Is.EqualTo(0f), "Out, it gives no light;");
            Assert.That(ConversationKeyLight.IntensityAt(1f), Is.EqualTo(1.6f).Within(1e-5f), "full, a soft key of 1.6;");
            Assert.That(ConversationKeyLight.IntensityAt(.5f), Is.EqualTo(.8f).Within(1e-5f), "halfway, half;");
            float last = 0f;
            for (int step = 1; step <= 20; step++)
            {
                float now = ConversationKeyLight.IntensityAt(step / 20f);
                Assert.That(now, Is.GreaterThan(last), "and it only rises on the way up.");
                last = now;
            }
        }
    }
}
