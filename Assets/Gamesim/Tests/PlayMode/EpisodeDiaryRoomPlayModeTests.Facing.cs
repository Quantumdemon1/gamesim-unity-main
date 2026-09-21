using System.Collections;
using Gamesim.House;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// A seated body faces the camera that frames it.
        ///
        /// <para>Nothing asserted this, anywhere, for any seat. The test whose name promises it -
        /// <c>NpcRuntime_ASavedTableMeetingReunitesSeatedAndFacingTheChairs</c> - checks the lease,
        /// the arrival and the seating and never looks at a rotation, so a body could sit backwards
        /// in every chair in the house and the whole suite would stay green. The diary is where it
        /// shows, because the diary is the only close-up: a forty-degree shot on somebody's face,
        /// or on the back of their head.</para>
        ///
        /// <para>The check is deliberately loose. It does not care about a few degrees of lean or
        /// how the chair was authored; it asks the one question the shot depends on - is the face
        /// pointing at the lens or away from it - so it catches a half-turn and stays quiet about
        /// everything else.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator DiaryRoom_TheSeatedBodyFacesTheCameraRatherThanTheBackOfTheChair()
        {
            yield return InstallDiaryFixture(
                state => state.Find(state.playerId).status == ContestantStatus.Active,
                "an active player who can sit for a confessional");
            yield return OpenDiaryFixturePanel();

            Assert.That(HouseInteractionAnchors.TryFind(player.gameObject.scene,
                HouseInteractionAnchors.DiaryVenue, 0, out var anchor), Is.True,
                "The private room must carry its authored diary anchor.");

            var body = player.transform;
            var forward = body.forward; forward.y = 0f;
            var toCamera = anchor.CameraPosition - body.position; toCamera.y = 0f;
            Assert.That(forward.sqrMagnitude, Is.GreaterThan(0.0001f));
            Assert.That(toCamera.sqrMagnitude, Is.GreaterThan(0.0001f));

            float away = Vector3.Angle(forward, toCamera);
            Assert.That(away, Is.LessThan(90f),
                "The sitter is turned " + away.ToString("0") + " degrees away from the diary camera at "
                + anchor.CameraPosition.ToString("0.00") + ". Past ninety they are showing it the back "
                + "of their head, which is the whole of the shot.");

            // And they have to be IN the chair, not standing beside it. Facing and seating fail
            // differently and look the same from a written description - "the model faces the back
            // of the seat" fits a body turned around and a body standing upright through the
            // cushion equally well - so measure both and let the failure say which.
            float aside = Vector3.ProjectOnPlane(body.position - anchor.Position, Vector3.up).magnitude;
            Debug.Log("[Gamesim] Diary seat - facing " + away.ToString("0.0") + " degrees off the camera, "
                + aside.ToString("0.00") + " m from the seat, body y " + body.position.y.ToString("0.00")
                + ", seat contact y " + anchor.SeatContact.y.ToString("0.00") + ".");
            Assert.That(aside, Is.LessThan(0.45f),
                "The sitter is " + aside.ToString("0.00") + " m from the chair it is supposed to be in.");
            yield return null;
        }
    }
}
