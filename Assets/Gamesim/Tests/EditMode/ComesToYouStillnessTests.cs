using Gamesim.Episode;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The player's stillness, which a houseguest's ask waits on (OFFERS-COME-TO-YOU-PLAN §C.2 step 4,
    /// <see cref="ComesToYouStillness"/>): time spent within a small radius of one spot with the walk arrived, the same at
    /// any frame rate. Counting frames without moving, as a first reading of "still" would, gives a walker at 240 fps a
    /// still second between every two strides. EditMode only: the helper is in Runtime.
    /// </summary>
    public sealed class ComesToYouStillnessTests
    {
        [Test]
        public void StillnessIsTheSameAtAnyFrameRate()
        {
            // The same path at 30 and at 240 frames a second: a walk of 0.5 m at 2.2 m/s, then 1.5 s standing.
            float Run(int fps)
            {
                float dt = 1f / fps, still = 0f;
                var anchor = new Vector3(float.NaN, float.NaN, float.NaN);
                var at = Vector3.zero;
                const float speed = 2.2f, walk = .5f, stand = 1.5f;
                for (float t = 0f; t < walk / speed + stand; t += dt)
                {
                    bool walking = t < walk / speed;
                    at = new Vector3(Mathf.Min(walk, t * speed), 0f, 0f);
                    still = ComesToYouStillness.Step(ref anchor, still, at, !walking, dt);
                }
                return still;
            }
            float slow = Run(30), fast = Run(240);
            Assert.That(slow, Is.GreaterThan(1f), "Standing 1.5 s is still past a second.");
            Assert.That(Mathf.Abs(slow - fast), Is.LessThanOrEqualTo(1f / 30f + 1e-4f), "The same still time to within one frame: " + slow + " and " + fast);
        }

        [Test]
        public void AWalkWhoseAgentReadsArrivedNeverAccruesASecond()
        {
            // 2.2 m/s at 240 fps is 0.0092 m a frame: well inside the radius each frame, out of it within a few dozen.
            float dt = 1f / 240f, still = 0f, most = 0f;
            var anchor = new Vector3(float.NaN, float.NaN, float.NaN);
            for (int frame = 0; frame < 240 * 4; frame++)
            {
                var at = new Vector3(frame * dt * 2.2f, 0f, 0f);
                still = ComesToYouStillness.Step(ref anchor, still, at, true, dt);
                most = Mathf.Max(most, still);
            }
            Assert.That(most, Is.LessThan(EpisodeDirector.AskStillSeconds), "A walker is never still for a second: " + most);
            Assert.That(most, Is.LessThan(.06f), "It leaves the radius within 0.05 s.");
        }

        [Test]
        public void AStalledFrameAddsAtMostItsAllowance()
        {
            var anchor = Vector3.zero;
            float still = ComesToYouStillness.Step(ref anchor, 0f, Vector3.zero, true, 30f);
            Assert.That(still, Is.EqualTo(EpisodeDirector.AskFrameAllowanceSeconds), "A 30 s stall is a quarter of a second of stillness.");
            Assert.That(ComesToYouStillness.FrameTime(-1f), Is.Zero);
            Assert.That(ComesToYouStillness.Step(ref anchor, still, Vector3.zero, false, .1f), Is.Zero, "A walk under way is never still.");
        }
    }
}
