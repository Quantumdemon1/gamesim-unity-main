using System.Collections.Generic;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The walk-in's eye kept clear of the house (LT4): the push-in that opens the walk-in follows the
    /// player along an authored path, and <see cref="WalkInEye"/> keeps that path exactly while it sees
    /// the player's head, moves to the nearest place around it that does when something stands in the
    /// way, stays there rather than swinging back, and eases rather than jumps. The numbers are the
    /// walk-in's own: the player's head over the walk-in mark at (0.65, 13.1), the eye 2.55 m behind it
    /// and 0.56 m above, at x -0.72. Plain geometry, so it runs under Unity's Mono harness too.
    /// </summary>
    public sealed class WalkInEyeTests
    {
        private const float Frame = 1f / 30f;
        private static readonly Vector3 Head = new Vector3(.65f, 1.6f, 13.1f);

        private static Vector3 AuthoredEye(Vector3 head) => new Vector3(Mathf.Min(-.72f, head.x - .25f), head.y + .56f, head.z - 2.55f);

        /// <summary>A box of <paramref name="size"/> metres centred a fraction <paramref name="along"/> of the way from the eye to the head.</summary>
        private static Bounds Across(Vector3 eye, Vector3 head, float along, float size) =>
            new Bounds(Vector3.Lerp(eye, head, along), Vector3.one * size);

        private static readonly List<Bounds> FarAway = new List<Bounds> { new Bounds(new Vector3(9f, 1f, 2f), Vector3.one) };

        [Test]
        public void ACrossingIsTheSegmentEnteringTheBox()
        {
            var box = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(.4f, .4f, .4f));
            Assert.That(WalkInEye.Crosses(box, new Vector3(-2f, 1f, 0f), new Vector3(2f, 1f, 0f)), Is.True, "Straight through.");
            Assert.That(WalkInEye.Crosses(box, new Vector3(-2f, 1.5f, 0f), new Vector3(2f, 1.5f, 0f)), Is.False, "Over the top.");
            Assert.That(WalkInEye.Crosses(box, new Vector3(-2f, 1f, 0f), new Vector3(-.3f, 1f, 0f)), Is.False, "Stopping short of it.");
            Assert.That(WalkInEye.Crosses(box, new Vector3(0f, 1f, 0f), new Vector3(2f, 1f, 2f)), Is.True, "Starting inside it.");
            Assert.That(WalkInEye.Crosses(box, new Vector3(-2f, .5f, -2f), new Vector3(2f, 1.5f, 2f)), Is.True, "Diagonally through its middle.");
            Assert.That(WalkInEye.Crosses(box, new Vector3(-2f, 1f, 1f), new Vector3(2f, 1f, 1f)), Is.False, "Level beside it.");
        }

        [Test]
        public void AClearViewKeepsTheAuthoredPathExactly()
        {
            var eye = new WalkInEye();
            eye.Reset();
            var sight = new List<Bounds>(FarAway) { new Bounds(new Vector3(-1.4f, .75f, 10f), new Vector3(.24f, 1.5f, .24f)) };
            for (int frame = 0; frame < 40; frame++)
            {
                var head = Head + new Vector3(-.01f, 0f, -.03f) * frame;
                var authored = AuthoredEye(head);
                Assert.That(eye.Place(authored, head, sight, Frame), Is.EqualTo(authored), "Frame " + frame + ": the path sees the head, so it is the eye.");
                Assert.That(eye.Chosen, Is.EqualTo(0));
            }
        }

        [Test]
        public void ABlockedEyeTakesTheNearestPlaceThatSeesTheHead()
        {
            for (int i = 1; i < WalkInEye.Candidates.Length; i++)
                Assert.That(WalkInEye.Candidates[i].magnitude, Is.GreaterThanOrEqualTo(WalkInEye.Candidates[i - 1].magnitude - 1e-5f),
                    "The places are tried nearest first.");
            var authored = AuthoredEye(Head);
            var sight = new List<Bounds>(FarAway) { Across(authored, Head, .25f, .3f) };
            Assert.That(WalkInEye.Blockers(sight, authored, Head), Is.EqualTo(1), "The box stands across the authored view.");
            int nearest = -1;
            for (int i = 0; i < WalkInEye.Candidates.Length && nearest < 0; i++)
                if (WalkInEye.Blockers(sight, WalkInEye.Candidate(authored, Head, i), Head) == 0) nearest = i;
            Assert.That(nearest, Is.GreaterThan(0), "Some place near the authored eye sees past the box.");

            var eye = new WalkInEye();
            eye.Reset();
            var placed = eye.Place(authored, Head, sight, Frame);
            Assert.That(eye.Chosen, Is.EqualTo(nearest), "The nearest place that sees the head is taken.");
            Assert.That(Vector3.Distance(placed, WalkInEye.Candidate(authored, Head, nearest)), Is.LessThan(1e-5f),
                "The first frame, under the house-entry card, is placed there at once.");
            Assert.That(WalkInEye.Blockers(sight, placed, Head), Is.EqualTo(0), "and sees the head.");
        }

        [Test]
        public void AViewBlockedMidWalkEasesAsideWithoutAJump()
        {
            var eye = new WalkInEye();
            eye.Reset();
            var head = Head;
            var authored = AuthoredEye(head);
            Vector3 offset = eye.Place(authored, head, FarAway, Frame) - authored;
            Assert.That(offset, Is.EqualTo(Vector3.zero), "Clear to begin with.");
            // A prop comes to stand across the view on the tenth frame.
            var blocked = new List<Bounds>(FarAway) { Across(authored, head, .25f, .3f) };
            for (int frame = 1; frame < 60; frame++)
            {
                var sight = frame < 10 ? FarAway : blocked;
                var now = eye.Place(authored, head, sight, Frame) - authored;
                Assert.That((now - offset).magnitude, Is.LessThanOrEqualTo(WalkInEye.MaxSpeed * Frame + 1e-5f),
                    "Frame " + frame + ": the eye moves no faster than a walk, never a cut.");
                if (frame < 10) Assert.That(now, Is.EqualTo(Vector3.zero), "Frame " + frame + ": still the authored eye.");
                offset = now;
            }
            Assert.That(eye.Chosen, Is.GreaterThan(0), "The eye has left the blocked path.");
            Assert.That(WalkInEye.Blockers(blocked, authored + offset, head), Is.EqualTo(0), "A second and a half on it sees the head.");
            Assert.That(Vector3.Distance(authored + offset, WalkInEye.Candidate(authored, head, eye.Chosen)), Is.LessThan(.01f), "It has settled on its new place.");
        }

        [Test]
        public void OnceMovedTheEyeStaysWhileItSeesClearly()
        {
            var eye = new WalkInEye();
            eye.Reset();
            var authored = AuthoredEye(Head);
            var sight = new List<Bounds>(FarAway) { Across(authored, Head, .25f, .3f) };
            eye.Place(authored, Head, sight, Frame);
            int moved = eye.Chosen;
            Assert.That(moved, Is.GreaterThan(0));
            var kept = WalkInEye.Candidate(authored, Head, moved);
            // The prop goes again: the authored eye is clear, and the eye does not swing back to it.
            for (int frame = 0; frame < 30; frame++)
            {
                Assert.That(Vector3.Distance(eye.Place(authored, Head, FarAway, Frame), kept), Is.LessThan(1e-5f), "Frame " + frame + ": it holds the place it moved to.");
                Assert.That(eye.Chosen, Is.EqualTo(moved));
            }
        }

        [Test]
        public void WithNowhereBetterTheAuthoredEyeStays()
        {
            var eye = new WalkInEye();
            eye.Reset();
            var authored = AuthoredEye(Head);
            // Something the head itself stands in spoils every place alike.
            var sight = new List<Bounds> { new Bounds(Head, new Vector3(.5f, .5f, .5f)) };
            for (int frame = 0; frame < 10; frame++)
            {
                Assert.That(eye.Place(authored, Head, sight, Frame), Is.EqualTo(authored), "Frame " + frame + ": no place is better, so none is taken.");
                Assert.That(eye.Chosen, Is.EqualTo(0));
            }
        }
    }
}
