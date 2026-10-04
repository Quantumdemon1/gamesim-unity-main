using System.Collections;
using System.Linq;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        // The native second-E trace's pair, yaw, panel offset and blocking box, translated
        // together away from the house. No real actor, route or social visibility is changed.
        private static readonly Vector3 ConversationGeometryTranslation = new Vector3(100f, 20f, 100f);
        private const float NativeSecondEYaw = 343.94992065f;
        // The native pair continued its ordinary movement after opening the recorded shot.
        // At the captured roots, the perpendicular side is 339.803 degrees; 343.950 is the
        // recorded starting camera yaw, from which that side is still the preferred choice.
        private const float NativeSecondEPairSide = 339.80313335f;
        private const float NativeConversationPanelOffset = 1.0177778f;

        [UnityTest]
        public IEnumerator Camera_ConversationKeepsLowFurnitureAndChoosesTheClearSideOfTallFurniture()
        {
            var fixture = new GameObject("Independent conversation camera geometry");
            try
            {
                var rig = ConversationGeometryRig(fixture, out var mine, out var theirs);
                rig.ConversationWindowOffset = NativeConversationPanelOffset;
                rig.SetConversationFocus(mine, theirs);
                AssertConversationYaw(rig, NativeSecondEPairSide, "Both clear sides retain the preferred framing.");
                var preferredEye = ConversationGeometryEye(rig);
                var box = ConversationGeometryBox(fixture,
                    new Vector3(-8.0000057f, .4f, -8.8999996f), new Vector3(4f, .8f, 1.6f));
                Physics.SyncTransforms();
                Assert.That(ConversationGeometryFaceBlocked(preferredEye, mine, box), Is.False);
                Assert.That(ConversationGeometryFaceBlocked(preferredEye, theirs, box), Is.False);
                rig.SetConversationFocus(mine, theirs);
                AssertConversationYaw(rig, NativeSecondEPairSide, "An ordinary low solid beneath the head rays does not turn the shot.");

                // Same floor footprint, now the measured opaque screen's actual collider bounds.
                // Its arbitrary name deliberately carries no asset-specific camera exemption.
                box.transform.position = ConversationGeometryTranslation + new Vector3(-8.0000057f, 1.3f, -8.8999996f);
                box.transform.localScale = new Vector3(4f, 2.6f, 1.6f);
                Physics.SyncTransforms();
                Assert.That(box.bounds.Contains(preferredEye), Is.False, "The measured eye sits just above the solid; containment alone missed it.");
                Assert.That(ConversationGeometryFaceBlocked(preferredEye, mine, box), Is.True, "The preferred player head ray reproduces the real obstruction.");
                Assert.That(ConversationGeometryFaceBlocked(preferredEye, theirs, box), Is.True, "The preferred NPC head ray reproduces the real obstruction.");
                Assert.That(HouseLayers.Sight & (1 << HouseLayers.Furniture), Is.Zero, "Furniture cannot change social witnessing.");
                var toHead = mine.position + Vector3.up * 1.5f - preferredEye;
                Assert.That(box.Raycast(new Ray(preferredEye, toHead.normalized), out _, toHead.magnitude), Is.True);
                var sightObstacles = Physics.RaycastAll(preferredEye, toHead.normalized, toHead.magnitude, HouseLayers.Sight,
                    QueryTriggerInteraction.Ignore).Where(hit => !hit.transform.IsChildOf(mine) && !hit.transform.IsChildOf(theirs));
                Assert.That(sightObstacles, Is.Empty, "The existing social Sight query still ignores this solid.");

                rig.SetConversationFocus(mine, theirs);
                AssertConversationYaw(rig, NativeSecondEPairSide + 180f, "Only the alternate side gives both participants a clear face ray.");
                var clearEye = ConversationGeometryEye(rig);
                Assert.That(ConversationGeometryFaceBlocked(clearEye, mine, box), Is.False);
                Assert.That(ConversationGeometryFaceBlocked(clearEye, theirs, box), Is.False);
                Assert.That(rig.DesiredDistance, Is.EqualTo(HouseCameraRig.TwoShotDistance).Within(.001f), "The correction chooses a side instead of changing the boom.");

                // A solid on Default is the same obstruction to this camera query. Returning to
                // the measured initial yaw only affects this independent rig, never a live actor.
                box.gameObject.layer = 0;
                rig.MoveTo(new HouseCameraRig.Shot { Focus = rig.DesiredFocus, Distance = HouseCameraRig.TwoShotDistance,
                    Pitch = HouseCameraRig.TwoShotPitch, Yaw = NativeSecondEYaw, FieldOfView = HouseCameraRig.TwoShotFieldOfView });
                Physics.SyncTransforms();
                rig.SetConversationFocus(mine, theirs);
                AssertConversationYaw(rig, NativeSecondEPairSide + 180f, "Tall opaque solid geometry is handled consistently across camera-visible layers.");
                box.gameObject.layer = HouseLayers.Furniture;

                var middle = (mine.position + theirs.position) * .5f;
                var opposite = box.transform.position;
                opposite.x = middle.x * 2f - opposite.x;
                opposite.z = middle.z * 2f - opposite.z;
                var otherBox = ConversationGeometryBox(fixture, opposite - ConversationGeometryTranslation, new Vector3(4f, 2.6f, 1.6f));
                Physics.SyncTransforms();
                Assert.That(ConversationGeometryFaceBlocked(clearEye, mine, otherBox), Is.True);
                Assert.That(ConversationGeometryFaceBlocked(clearEye, theirs, otherBox), Is.True);
                rig.SetConversationFocus(mine, theirs);
                AssertConversationYaw(rig, NativeSecondEPairSide + 180f, "With both sides blocked, the current preferred side remains the fallback.");
                box.enabled = false; otherBox.enabled = false;
                Physics.SyncTransforms();
                rig.SetConversationFocus(mine, theirs);
                AssertConversationYaw(rig, NativeSecondEPairSide + 180f, "When both are clear again, the camera retains its preferred side.");
            }
            finally { Object.Destroy(fixture); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Camera_ConversationChecksTheShiftedEyeWhenThePanelPublishesItsWidth()
        {
            var fixture = new GameObject("Conversation panel camera geometry");
            try
            {
                var rig = ConversationGeometryRig(fixture, out var mine, out var theirs);
                // A narrow opaque upright at the native final eye is clear of the fallback eye,
                // then obstructs the actual shifted eye once the HUD publishes the panel width.
                var upright = ConversationGeometryBox(fixture,
                    new Vector3(-9.0095253f, 1.5f, -8.7584324f), new Vector3(.4f, 3f, .4f));
                Physics.SyncTransforms();
                rig.SetConversationFocus(mine, theirs);
                AssertConversationYaw(rig, NativeSecondEPairSide, "The opening fallback framing is clear before layout.");
                var initialEye = ConversationGeometryEye(rig);
                Assert.That(upright.bounds.Contains(initialEye), Is.False);
                Assert.That(ConversationGeometryFaceBlocked(initialEye, mine, upright), Is.False);
                Assert.That(ConversationGeometryFaceBlocked(initialEye, theirs, upright), Is.False);
                var initialMine = mine.position; var initialTheirs = theirs.position;

                rig.ConversationWindowOffset = NativeConversationPanelOffset;
                AssertConversationYaw(rig, NativeSecondEPairSide + 180f, "Panel layout must validate the eye that will actually be displayed.");
                var clearEye = ConversationGeometryEye(rig);
                Assert.That(upright.bounds.Contains(clearEye), Is.False);
                Assert.That(ConversationGeometryFaceBlocked(clearEye, mine, upright), Is.False);
                Assert.That(ConversationGeometryFaceBlocked(clearEye, theirs, upright), Is.False);
                Assert.That(mine.position, Is.EqualTo(initialMine));
                Assert.That(theirs.position, Is.EqualTo(initialTheirs), "Camera correction does not relocate the pair.");

                rig.SetReducedMotion(true);
                float frozenYaw = rig.Yaw;
                var frozenFocus = rig.DesiredFocus;
                rig.ConversationWindowOffset = .5f;
                Assert.That(rig.Yaw, Is.EqualTo(frozenYaw), "A layout update cannot reframe a reduced-motion conversation.");
                Assert.That(rig.DesiredFocus, Is.EqualTo(frozenFocus));
            }
            finally { Object.Destroy(fixture); }
            yield return null;
        }

        private static HouseCameraRig ConversationGeometryRig(GameObject fixture, out Transform mine, out Transform theirs)
        {
            mine = new GameObject("Geometry player").transform; mine.SetParent(fixture.transform);
            theirs = new GameObject("Geometry NPC").transform; theirs.SetParent(fixture.transform);
            mine.position = ConversationGeometryTranslation + new Vector3(-7.8720665f, .03333333f, -4.0320072f);
            theirs.position = ConversationGeometryTranslation + new Vector3(-9.3955965f, .03333333f, -4.5924625f);
            foreach (var actor in new[] { mine, theirs })
            {
                // Both head-ray endpoints are inside their own solid capsule. Participant
                // exclusion must work, rather than declaring both camera sides blocked by them.
                var collider = actor.gameObject.AddComponent<CapsuleCollider>();
                collider.height = 1.7f; collider.radius = .25f; collider.center = Vector3.up * .85f;
            }
            var pivot = new GameObject("Geometry rig"); pivot.transform.SetParent(fixture.transform);
            var eye = new GameObject("Geometry lens"); eye.transform.SetParent(pivot.transform);
            eye.AddComponent<Camera>().enabled = false;
            var rig = pivot.AddComponent<HouseCameraRig>();
            rig.enabled = false; rig.ControlsEnabled = false;
            rig.ConfigureBounds(ConversationGeometryTranslation, new Vector2(30f, 30f));
            rig.MoveTo(new HouseCameraRig.Shot { Focus = (mine.position + theirs.position) * .5f,
                Distance = HouseCameraRig.TwoShotDistance, Pitch = HouseCameraRig.TwoShotPitch,
                Yaw = NativeSecondEYaw, FieldOfView = HouseCameraRig.TwoShotFieldOfView });
            return rig;
        }

        private static BoxCollider ConversationGeometryBox(GameObject fixture, Vector3 center, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Opaque conversation fixture furniture";
            box.transform.SetParent(fixture.transform);
            box.transform.position = ConversationGeometryTranslation + center;
            box.transform.localScale = size;
            box.layer = HouseLayers.Furniture;
            return box.GetComponent<BoxCollider>();
        }

        private static Vector3 ConversationGeometryEye(HouseCameraRig rig)
            => rig.DesiredFocus + Quaternion.Euler(HouseCameraRig.TwoShotPitch, rig.Yaw, 0f)
                * Vector3.back * HouseCameraRig.TwoShotDistance;

        private static bool ConversationGeometryFaceBlocked(Vector3 eye, Transform actor, Collider obstacle)
        {
            var to = actor.position + Vector3.up * 1.5f - eye;
            return obstacle.Raycast(new Ray(eye, to.normalized), out _, to.magnitude);
        }

        private static void AssertConversationYaw(HouseCameraRig rig, float expected, string reason)
            => Assert.That(Mathf.Abs(Mathf.DeltaAngle(rig.Yaw, expected)), Is.LessThan(.05f), reason);
    }
}
