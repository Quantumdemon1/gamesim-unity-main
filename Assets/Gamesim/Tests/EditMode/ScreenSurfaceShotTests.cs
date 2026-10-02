using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The screen shot with the lens kept clear of bodies (UI-UX-PASS-PLAN K0): a body between the
    /// lens and the card is seen for what it is, a body beside the screen or behind the lens is
    /// not, and the cut raised or swung to clear one still frames the whole card. Pure geometry on
    /// a stand-in board the size of the set's screen, so it runs without a house.
    /// </summary>
    public sealed class ScreenSurfaceShotTests
    {
        private readonly List<GameObject> props = new List<GameObject>();

        [TearDown]
        public void StrikeTheSet()
        {
            foreach (var prop in props) if (prop != null) Object.DestroyImmediate(prop);
            props.Clear();
        }

        /// <summary>The set's screen as a board: 2.6 by 1.5, a hand thick, its face toward the room at +z.</summary>
        private ScreenSurface Screen()
        {
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            props.Add(board);
            board.name = "Shot board";
            board.transform.position = new Vector3(0f, 1.75f, -18.9f);
            board.transform.localScale = new Vector3(2.6f, 1.5f, 0.1f);
            var screen = ScreenSurface.Measure(board.transform, "Nomination", new Vector3(0f, 0f, -15f));
            Assert.That(screen, Is.Not.Null);
            Assert.That(Vector3.Dot(screen.Normal, Vector3.forward), Is.GreaterThan(.99f), "The face looks into the room.");
            return screen;
        }

        /// <summary>Where <paramref name="world"/> lands in a frame of <paramref name="aspect"/> from <paramref name="shot"/>, as a share of its width and height from the bottom-left; null behind the lens.</summary>
        private static Vector2? InTheFrame(HouseCameraRig.Shot shot, float aspect, Vector3 world)
        {
            var rotation = Quaternion.Euler(shot.Pitch, shot.Yaw, 0f);
            var view = Quaternion.Inverse(rotation) * (world - ScreenSurface.Eye(shot));
            if (view.z <= 0f) return null;
            float halfHeight = Mathf.Tan(shot.FieldOfView * 0.5f * Mathf.Deg2Rad), halfWidth = halfHeight * aspect;
            return new Vector2((view.x / view.z / halfWidth + 1f) * 0.5f, (view.y / view.z / halfHeight + 1f) * 0.5f);
        }

        [Test]
        public void TheHeadOnShotIsTheOneTheRigAlwaysHad()
        {
            var screen = Screen();
            var plain = screen.Shot();
            Assert.That(plain.Focus, Is.EqualTo(screen.Centre));
            Assert.That(plain.Pitch, Is.EqualTo(0f));
            Assert.That(plain.Yaw, Is.EqualTo(screen.LookYaw));
            Assert.That(plain.Distance, Is.EqualTo((screen.Height * 0.5f) / Mathf.Tan(ScreenSurface.FieldOfView * 0.5f * Mathf.Deg2Rad) * 1.08f).Within(1e-4f),
                "The face fills the frame's height with a little air round it.");
            var eye = ScreenSurface.Eye(plain);
            Assert.That((eye - (screen.Centre + screen.Normal * plain.Distance)).magnitude, Is.LessThan(1e-3f),
                "The lens stands the shot's distance in front of the face.");
            var cleared = screen.ShotClearOf(new List<ScreenSurface.Body>());
            Assert.That(cleared.Pitch, Is.EqualTo(plain.Pitch));
            Assert.That(cleared.Yaw, Is.EqualTo(plain.Yaw));
            Assert.That(cleared.Distance, Is.EqualTo(plain.Distance));
        }

        [Test]
        public void ABodyBetweenTheLensAndTheCardIsInTheShotAndOneBesideOrBehindIsNot()
        {
            var screen = Screen();
            var plain = screen.Shot();
            var eye = ScreenSurface.Eye(plain);
            var floor = new Vector3(screen.Centre.x, 0f, screen.Centre.z);
            // The Head of Household's old mark: 0.8 m in front of the lens, on the axis.
            var before = ScreenSurface.Body.Standing(floor + screen.Normal * (plain.Distance - 0.8f));
            Assert.That(screen.InTheShot(plain, before), Is.True, "A body 0.8 m before the lens stands in the card.");
            // The mark beside the screen (N1): a body's width past the face's edge, a step in front of it.
            var right = Vector3.Cross(Vector3.up, -screen.Normal).normalized;
            var beside = ScreenSurface.Body.Standing(floor + screen.Normal * CeremonySets.HeadMarkFromScreen
                + right * (screen.Width * 0.5f + CeremonySets.HeadMarkBesideScreen));
            Assert.That(screen.InTheShot(plain, beside), Is.False, "A body beside the screen is clear of the card.");
            var behind = ScreenSurface.Body.Standing(floor + screen.Normal * (plain.Distance + 1.5f));
            Assert.That(screen.InTheShot(plain, behind), Is.False, "A body behind the lens is not between it and the card.");
            var seated = new ScreenSurface.Body(floor + screen.Normal * 1.2f + right * 0.6f, floor + screen.Normal * 1.2f + right * 0.6f + Vector3.up * 1.2f);
            Assert.That(screen.InTheShot(plain, seated), Is.True, "A seated body in a hot seat before the face is in the card.");
            Assert.That(ScreenSurface.Eye(plain), Is.EqualTo(eye));
        }

        [Test]
        public void TheCutRaisesOrSwingsToClearABodyAndStillFramesTheWholeCard()
        {
            var screen = Screen();
            var plain = screen.Shot();
            var floor = new Vector3(screen.Centre.x, 0f, screen.Centre.z);
            var right = Vector3.Cross(Vector3.up, -screen.Normal).normalized;
            var bodies = new List<ScreenSurface.Body>
            {
                ScreenSurface.Body.Standing(floor + screen.Normal * (plain.Distance - 0.8f) + right * 0.2f),
            };
            var cut = screen.ShotClearOf(bodies);
            Assert.That(bodies.Any(body => screen.InTheShot(cut, body)), Is.False, "The cut clears the body.");
            Assert.That(Mathf.Abs(cut.Pitch) > 0f || Mathf.Abs(Mathf.DeltaAngle(cut.Yaw, plain.Yaw)) > 0f, Is.True, "It is raised or swung, not the head-on shot.");
            Assert.That(cut.Distance, Is.GreaterThanOrEqualTo(plain.Distance - 1e-4f), "Pulled back for the near edge, never pushed in past it.");
            Assert.That(cut.FieldOfView, Is.EqualTo(plain.FieldOfView), "The lens never changes: a cut between shots never eases it.");
            Assert.That(cut.Focus, Is.EqualTo(plain.Focus), "The face stays the shot's centre.");
            Assert.That(Mathf.Abs(cut.Pitch), Is.LessThanOrEqualTo(26f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(cut.Yaw, plain.Yaw)), Is.LessThanOrEqualTo(40f));
            // Every corner of the card inside the 16:9 frame, with a little air.
            foreach (var corner in screen.CardCorners())
            {
                var at = InTheFrame(cut, 16f / 9f, corner);
                Assert.That(at.HasValue, Is.True, "The card is in front of the lens.");
                Assert.That(at.Value.x, Is.InRange(0.01f, 0.99f), "The card's corner " + corner + " is inside the frame's width: " + at.Value);
                Assert.That(at.Value.y, Is.InRange(0.01f, 0.99f), "The card's corner " + corner + " is inside the frame's height: " + at.Value);
            }
        }

        [Test]
        public void ARoomfulOfBodiesBeforeTheFaceGetsTheFramingWithTheFewestInIt()
        {
            var screen = Screen();
            var plain = screen.Shot();
            var floor = new Vector3(screen.Centre.x, 0f, screen.Centre.z);
            var right = Vector3.Cross(Vector3.up, -screen.Normal).normalized;
            // A wall of bodies a metre before the lens, shoulder to shoulder across the whole card and
            // past it, two deep: nothing clears them all.
            var bodies = new List<ScreenSurface.Body>();
            for (int row = 0; row < 2; row++)
                for (int i = -6; i <= 6; i++)
                    bodies.Add(ScreenSurface.Body.Standing(floor + screen.Normal * (plain.Distance - 0.6f - row * 0.7f) + right * (i * 0.5f)));
            int InTheWay(HouseCameraRig.Shot shot) => bodies.Sum(body => screen.Intrusion(shot, body));
            var cut = screen.ShotClearOf(bodies);
            Assert.That(InTheWay(plain), Is.GreaterThan(0), "The wall stands in the head-on shot.");
            Assert.That(InTheWay(cut), Is.LessThanOrEqualTo(InTheWay(plain)), "The cut has no more of the wall in it than the head-on shot.");
            Assert.That(InTheWay(cut), Is.LessThanOrEqualTo(InTheWay(screen.Framing(8f, 0f))), "and no more than any other framing tried,");
            Assert.That(InTheWay(cut), Is.LessThanOrEqualTo(InTheWay(screen.Framing(0f, 24f))), "whichever way it is swung.");
        }
    }
}
