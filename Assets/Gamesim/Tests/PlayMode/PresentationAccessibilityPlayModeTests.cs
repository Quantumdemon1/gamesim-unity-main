using System.Collections;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed class PresentationAccessibilityPlayModeTests
    {
        private GameObject fixture;
        private HouseCameraRig rig;
        private Transform player, npc;

        [SetUp]
        public void CreateCameraFixture()
        {
            fixture = new GameObject("Presentation accessibility fixture");
            var rigRoot = new GameObject("Test camera rig");
            rigRoot.transform.SetParent(fixture.transform, false);
            var cameraRoot = new GameObject("Test camera");
            cameraRoot.transform.SetParent(rigRoot.transform, false);
            cameraRoot.AddComponent<Camera>().enabled = false;
            player = new GameObject("Test player").transform;
            player.SetParent(fixture.transform, false);
            player.position = new Vector3(7f, 0f, 4f);
            npc = new GameObject("Test housemate").transform;
            npc.SetParent(fixture.transform, false);
            npc.position = new Vector3(9f, 0f, 6f);
            rig = rigRoot.AddComponent<HouseCameraRig>();
            rig.Configure(player);
            rig.ControlsEnabled = false;
        }

        [UnityTearDown]
        public IEnumerator DestroyOnlyFixture()
        {
            if (fixture != null) Object.Destroy(fixture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReducedMotion_DialogueKeepsCurrentViewOnEntryAndExit()
        {
            rig.SetReducedMotion(true);
            var position = rig.transform.position;
            var cameraPosition = rig.ViewCamera.transform.localPosition;
            var rotation = rig.transform.rotation;
            rig.SetConversationFocus(player, npc);
            Assert.That(rig.IsConversationFocused, Is.True, "Dialogue focus remains an input-lock state.");
            yield return null;
            yield return null;
            AssertViewUnchanged(position, cameraPosition, rotation);
            npc.position += Vector3.right;
            yield return null;
            yield return null;
            AssertViewUnchanged(position, cameraPosition, rotation);
            rig.EndConversation();
            yield return null;
            yield return null;
            Assert.That(rig.IsConversationFocused, Is.False);
            AssertViewUnchanged(position, cameraPosition, rotation);
        }

        [UnityTest]
        public IEnumerator DefaultCamera_RetainsAutomaticConversationFraming()
        {
            Assert.That(rig.ReducedMotion, Is.False);
            var startPosition = rig.transform.position;
            var startCameraZ = rig.ViewCamera.transform.localPosition.z;
            rig.SetConversationFocus(player, npc);
            yield return null;
            yield return null;
            Assert.That(Vector3.Distance(startPosition, rig.transform.position), Is.GreaterThan(0.001f));
            Assert.That(rig.ViewCamera.transform.localPosition.z, Is.GreaterThan(startCameraZ + 0.001f));
            // The framing is the two-shot (V5): it eases toward the shot's boom and never inside it.
            Assert.That(rig.HasShot, Is.True);
            Assert.That(rig.ViewCamera.transform.localPosition.z, Is.LessThanOrEqualTo(-HouseCameraRig.TwoShotDistance + 0.001f));
        }

        [UnityTest]
        public IEnumerator EnablingReducedMotion_StopsInFlightAutomaticReframing()
        {
            rig.SetConversationFocus(player, npc);
            yield return null;
            yield return null;
            rig.SetReducedMotion(true);
            var position = rig.transform.position;
            var cameraPosition = rig.ViewCamera.transform.localPosition;
            var rotation = rig.transform.rotation;
            yield return null;
            yield return null;
            AssertViewUnchanged(position, cameraPosition, rotation);
            rig.SetReducedMotion(false);
            yield return null;
            yield return null;
            Assert.That(Vector3.Distance(position, rig.transform.position), Is.GreaterThan(0.001f));
        }

        private void AssertViewUnchanged(Vector3 position, Vector3 cameraPosition, Quaternion rotation)
        {
            Assert.That(Vector3.Distance(rig.transform.position, position), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(rig.ViewCamera.transform.localPosition, cameraPosition), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(rig.transform.rotation, rotation), Is.LessThan(0.001f));
        }
    }
}
