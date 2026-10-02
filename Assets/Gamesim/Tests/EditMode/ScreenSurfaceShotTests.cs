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

        /// <summary>
        /// The set's screen as a board: 2.6 by 1.5, a hand thick, its face toward the room at +z, its
        /// middle <paramref name="height"/> off the floor - 1.75 hung on a wall, lower on a stand.
        /// </summary>
        private ScreenSurface Screen(float height = 1.75f)
        {
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            props.Add(board);
            board.name = "Shot board";
            board.transform.position = new Vector3(0f, height, -18.9f);
            board.transform.localScale = new Vector3(2.6f, 1.5f, 0.1f);
            var screen = ScreenSurface.Measure(board.transform, "Nomination", new Vector3(0f, 0f, -15f));
            Assert.That(screen, Is.Not.Null);
            Assert.That(Vector3.Dot(screen.Normal, Vector3.forward), Is.GreaterThan(.99f), "The face looks into the room.");
            return screen;
        }

        /// <summary>The way the card's right runs on the face, as the shot's lens sees it head-on.</summary>
        private static Vector3 Right(ScreenSurface screen) => Vector3.Cross(Vector3.up, -screen.Normal).normalized;

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
            var against = ScreenSurface.Body.Standing(floor - screen.Normal * 0.05f);
            Assert.That(screen.InTheShot(plain, against), Is.False, "A body on or behind the face's plane is not in front of the card.");
            Assert.That(ScreenSurface.Eye(plain), Is.EqualTo(eye));
        }

        /// <summary>
        /// A body is as tall as its head and as wide as its shoulders: its radius reaches sideways,
        /// not above the top of its head. A seated houseguest 1.2 m before a screen hung on a wall
        /// sits under the lens's line to the card's foot and is clear of it; before a screen on a
        /// stand, the same body is in the card.
        /// </summary>
        [Test]
        public void ASeatedHeadUnderTheLinesToTheCardIsClearOfItAndOneInFrontOfALowScreenIsNot()
        {
            ScreenSurface.Body Seated(ScreenSurface screen)
            {
                var feet = new Vector3(screen.Centre.x, 0f, screen.Centre.z) + screen.Normal * 1.2f + Right(screen) * 0.6f;
                return new ScreenSurface.Body(feet, feet + Vector3.up * 1.2f);
            }
            var wall = Screen();
            Assert.That(wall.InTheShot(wall.Shot(), Seated(wall)), Is.False,
                "A seated head at 1.2 m sits under the lens's line to the foot of a card hung at 1.75: no radius above the head.");
            var stand = Screen(1.3f);
            Assert.That(stand.InTheShot(stand.Shot(), Seated(stand)), Is.True, "The same seat before a card on a stand at 1.3 is in it.");
        }

        /// <summary>
        /// UI-UX-PASS-PLAN K0's review, M1: a raised or swung lens sees the card's far side deeper
        /// than its near corner, and a body in front of the face is still in front of the card -
        /// judged against the face's plane, not the nearest corner's depth.
        /// </summary>
        [Test]
        public void ABodyInFrontOfTheFaceIsInTheShotFromARaisedOrSwungLens()
        {
            var screen = Screen();
            var floor = new Vector3(screen.Centre.x, 0f, screen.Centre.z);
            var right = Right(screen);
            var offAxis = ScreenSurface.Body.Standing(floor + screen.Normal * 0.6f + right * 0.6f);
            foreach (float yaw in new[] { 18f, -18f })
                Assert.That(screen.InTheShot(screen.Framing(14f, yaw), offAxis), Is.True,
                    "A body 0.6 m out and 0.6 m off the axis covers the card from a lens raised 14 and swung " + yaw + ".");
            var onAxis = ScreenSurface.Body.Standing(floor + screen.Normal * 0.8f);
            foreach (float yaw in new[] { 40f, -40f })
                Assert.That(screen.InTheShot(screen.Framing(8f, yaw), onAxis), Is.True,
                    "A body 0.8 m out on the axis covers the card from a lens raised 8 and swung " + yaw + ".");
        }

        /// <summary>
        /// Close to the lens the card is a thin slice of the body's height, and no point the shot
        /// tests may fall in it: a body whose points stand either side of it crosses it, and is in
        /// the shot.
        /// </summary>
        [Test]
        public void ABodyCrossingTheCardsThinSliceNearTheLensIsInTheShot()
        {
            var screen = Screen();
            var plain = screen.Shot();
            var feet = new Vector3(screen.Centre.x, 0f, screen.Centre.z) + screen.Normal * (plain.Distance - 0.25f);
            // At a quarter of a metre the card is 1.67 to 1.83 m high; a 1.9 m body's points stand at 1.58 and 1.9.
            var tall = new ScreenSurface.Body(feet, feet + Vector3.up * 1.9f);
            Assert.That(screen.InTheShot(plain, tall), Is.True, "A body crossing the card's slice a quarter of a metre from the lens is in it.");
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
