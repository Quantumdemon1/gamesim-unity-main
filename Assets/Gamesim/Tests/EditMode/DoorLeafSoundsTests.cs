using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The front door heard (AU-06): the clunk as its leaves leave the frame and the thud as they land
    /// in it, read from how open they stood before and after a step (DoorLeafSounds). Unity-free; the
    /// staged exit's PlayMode case holds the thud to the frame the walk out's hold on the shut door
    /// starts from.
    /// </summary>
    public sealed class DoorLeafSoundsTests
    {
        /// <summary>
        /// The leaves swung open at 3 a second and shut at 6, frame by frame as the set eases them
        /// (1 - e^(-rate dt)) at 60 frames a second: one clunk as they leave the frame and one thud as
        /// they land in it, and nothing on the way.
        /// </summary>
        [Test]
        public void AnEasedOpenAndCloseClunksOnceAndThudsOnce()
        {
            int swings = 0, shuts = 0, swingFrame = -1, shutFrame = -1;
            float openness = 0f, dt = 1f / 60f;
            for (int frame = 0; frame < 240; frame++)
            {
                bool opening = frame < 120;
                float target = opening ? 1f : 0f, rate = opening ? 3f : 6f;
                float before = openness;
                openness += (target - openness) * (1f - (float)System.Math.Exp(-rate * dt));
                if (System.Math.Abs(target - openness) < 0.0001f) openness = target;
                switch (DoorLeafSounds.Between(before, openness))
                {
                    case DoorLeafSound.Swing: swings++; swingFrame = frame; break;
                    case DoorLeafSound.Shut: shuts++; shutFrame = frame; break;
                }
            }
            Assert.That(swings, Is.EqualTo(1), "One clunk as the leaves give.");
            Assert.That(swingFrame, Is.EqualTo(0), "In the first frame of the swing.");
            Assert.That(shuts, Is.EqualTo(1), "One thud as they land.");
            Assert.That(shutFrame, Is.InRange(120, 239), "On the way back, not on the way out.");
            Assert.That(openness, Is.EqualTo(0f), "The leaves end shut.");
        }

        /// <summary>
        /// The thud comes as the leaves read shut - under a hundredth of their swing, where the walk
        /// out starts its hold on the shut door - not when the ease finally reaches nothing, which at
        /// the set's closing rate is most of a second later.
        /// </summary>
        [Test]
        public void TheThudLandsAsTheLeavesReadShut_NotWhenTheEaseReachesNothing()
        {
            Assert.That(DoorLeafSounds.ShutBelow, Is.EqualTo(0.01f));
            Assert.That(DoorLeafSounds.Between(0.0102f, 0.0098f), Is.EqualTo(DoorLeafSound.Shut), "Crossing a hundredth is landing.");
            Assert.That(DoorLeafSounds.Between(0.0098f, 0.0001f), Is.EqualTo(DoorLeafSound.None), "Easing on to nothing is not a second thud.");
            Assert.That(DoorLeafSounds.Between(0.0001f, 0f), Is.EqualTo(DoorLeafSound.None));
            Assert.That(DoorLeafSounds.IsShut(0.01f), Is.True, "A hundredth reads shut,");
            Assert.That(DoorLeafSounds.IsShut(0.0101f), Is.False, "and a little more does not.");
        }

        /// <summary>
        /// A door that turns back before it shut makes neither sound - its latch never caught - and
        /// neither does a rattle that never gave, nor a door standing still. Under reduced motion the
        /// leaves jump in one step, so the clunk comes with the open and the thud with the close.
        /// </summary>
        [Test]
        public void ATurnBackOrARattleIsSilent_AndAJumpUnderReducedMotionSoundsAtOnce()
        {
            Assert.That(DoorLeafSounds.Between(0.4f, 0.3f), Is.EqualTo(DoorLeafSound.None), "Closing, on the way.");
            Assert.That(DoorLeafSounds.Between(0.3f, 0.35f), Is.EqualTo(DoorLeafSound.None), "Turning back before it shut.");
            Assert.That(DoorLeafSounds.Between(0f, 0f), Is.EqualTo(DoorLeafSound.None), "A rattle: the leaves never left the frame.");
            Assert.That(DoorLeafSounds.Between(1f, 1f), Is.EqualTo(DoorLeafSound.None), "Standing open.");
            Assert.That(DoorLeafSounds.Between(0f, 1f), Is.EqualTo(DoorLeafSound.Swing), "Reduced motion: open at once, and heard at once.");
            Assert.That(DoorLeafSounds.Between(1f, 0f), Is.EqualTo(DoorLeafSound.Shut), "Reduced motion: shut at once, and heard at once.");
        }
    }
}
