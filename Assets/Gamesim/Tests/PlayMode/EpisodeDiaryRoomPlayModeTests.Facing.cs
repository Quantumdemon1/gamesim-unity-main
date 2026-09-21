using System.Collections;
using System.Linq;
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

            // And the body you can SEE has to be in the chair. Measure the visual root, not the
            // controller: HouseSeatPresentation deliberately leaves the transform standing at the
            // approach point and offsets the rendered body onto the cushion instead, so asking the
            // controller where it is returns the approach - 1.17 m out, exactly the authored
            // offset - and reads as a damning result about nothing at all.
            var visual = player.GetComponent<Gamesim.Presentation.CharacterPresentation>()?.VisualRoot;
            Assert.That(visual, Is.Not.Null, "A seated player still has a body to look at.");
            float aside = Vector3.ProjectOnPlane(visual.position - anchor.Position, Vector3.up).magnitude;
            Debug.Log("[Gamesim] Diary seat - facing " + away.ToString("0.0") + " degrees off the camera, "
                + "visual " + aside.ToString("0.00") + " m from the seat, visual y "
                + visual.position.y.ToString("0.00") + ", controller "
                + Vector3.ProjectOnPlane(body.position - anchor.Position, Vector3.up).magnitude.ToString("0.00")
                + " m out, seat contact y " + anchor.SeatContact.y.ToString("0.00") + ".");
            Assert.That(aside, Is.LessThan(0.45f),
                "The body on screen is " + aside.ToString("0.00") + " m from the chair it is sitting in.");

            // Where it stands and which way it looks are both right; the remaining way for a
            // confessional to look ridiculous is the POSE. If the animator never learns it is
            // seated, a correctly placed, correctly aimed body stands bolt upright through the
            // cushion with the arms of the chair passing through its legs.
            var animator = player.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                bool declares = animator.parameters.Any(p => p.name == "Seated"
                    && p.type == AnimatorControllerParameterType.Bool);
                var state = animator.GetCurrentAnimatorStateInfo(0);
                Debug.Log("[Gamesim] Diary pose - controller " + animator.runtimeAnimatorController.name
                    + ", declares Seated " + declares
                    + ", Seated=" + (declares && animator.GetBool("Seated"))
                    + ", state hash " + state.fullPathHash + ", length " + state.length.ToString("0.00") + ".");
                Assert.That(declares, Is.True,
                    "The cast's controller has no Seated parameter, so no chair in the house can pose a body.");
                Assert.That(animator.GetBool("Seated"), Is.True,
                    "The body is in the chair and the animator does not know it, so it is playing a standing pose.");
            }
            yield return null;
        }
    }
}
