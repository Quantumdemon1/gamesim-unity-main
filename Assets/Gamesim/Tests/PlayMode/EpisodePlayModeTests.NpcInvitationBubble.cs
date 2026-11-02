using System.Collections;
using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator NpcInvitationBubble_HearAndDeferAreDistinctOneUsePresentationCallbacks()
        {
            director.SuspendNpcAutonomyForDiagnostics();
            director.ClosePanels(); yield return null;
            var bubble = director.gameObject.AddComponent<HouseNpcInvitationBubble>();
            int heard = 0, deferred = 0;
            var before = director.Snapshot;
            try
            {
                Vector3 anchor = SceneComponents<Gamesim.House.HousePlayerController>()[0].transform.position + Vector3.up * 1.95f;
                bubble.Show("Morgan", "Can we talk about keeping each other off the block?", 1f, anchor,
                    () => heard++, () => deferred++);
                yield return null; Canvas.ForceUpdateCanvases();
                Assert.That(bubble.IsVisible, Is.True);
                Assert.That(bubble.Focus(), Is.True);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(bubble.HearControl.gameObject));
                Assert.That(bubble.HearControl.navigation.selectOnDown, Is.SameAs(bubble.NotNowControl));
                bubble.NotNowControl.onClick.Invoke(); bubble.NotNowControl.onClick.Invoke();
                Assert.That(deferred, Is.EqualTo(1)); Assert.That(heard, Is.Zero);
                Assert.That(bubble.IsVisible, Is.False);
                bubble.Show("Morgan", "Can we talk about keeping each other off the block?", 1f, anchor,
                    () => heard++, () => deferred++);
                yield return null;
                bubble.HearControl.onClick.Invoke(); bubble.HearControl.onClick.Invoke();
                Assert.That(heard, Is.EqualTo(1)); Assert.That(deferred, Is.EqualTo(1));
                AssertEquivalent(before, director.Snapshot);
            }
            finally { Object.Destroy(bubble); }
        }

        [UnityTest]
        public IEnumerator NpcInvitationBubble_OnlyVisibleWordsCanActivateAndHideRemovesOldAuthority()
        {
            director.SuspendNpcAutonomyForDiagnostics();
            director.ClosePanels(); yield return null;
            var bubble = director.gameObject.AddComponent<HouseNpcInvitationBubble>(); int answered = 0;
            try
            {
                var camera = Camera.main;
                Assert.That(camera, Is.Not.Null);
                Vector3 visible = SceneComponents<Gamesim.House.HousePlayerController>()[0].transform.position + Vector3.up * 1.95f;
                bubble.Show("Avery O'Neil", "Can we talk? I need your help with the veto.", TextSizes.Scale(TextSize.Largest), visible,
                    () => answered++, () => answered++);
                yield return null; Canvas.ForceUpdateCanvases();
                Assert.That(bubble.IsVisible, Is.True);
                foreach (var label in bubble.HearControl.transform.parent.GetComponentsInChildren<TMP_Text>())
                    Assert.That(label.isTextOverflowing, Is.False, label.text);
                if (Application.isBatchMode)
                {
                    yield return CaptureFraming("dialogue-invitation-bubble-largest");
                    var hud = director.GetComponent<EpisodeHud>();
                    bool previousCompact = hud.Compact;
                    try
                    {
                        hud.Compact = true;
                        yield return CaptureFraming("dialogue-invitation-bubble-largest-compact", width: 1024, height: 768);
                    }
                    finally { hud.Compact = previousCompact; }
                }
                bubble.Hide(); bubble.HearControl.onClick.Invoke();
                Assert.That(answered, Is.Zero);
                bubble.Show("Avery O'Neil", "Can we talk? I need your help with the veto.", 1f,
                    camera.transform.position - camera.transform.forward * 3f, () => answered++, () => answered++);
                yield return null;
                Assert.That(bubble.IsVisible, Is.False, "An anchor behind the camera cannot show a mirrored invitation.");
                bubble.HearControl.onClick.Invoke();
                Assert.That(answered, Is.Zero);
                bubble.Show("Avery O'Neil", "Can we talk?", 1f,
                    camera.ViewportToWorldPoint(new Vector3(1.5f, .5f, 5f)), () => answered++, () => answered++);
                yield return null;
                Assert.That(bubble.IsVisible, Is.False, "A speaker outside the camera view cannot display an invitation clamped on screen.");
                bubble.HearControl.onClick.Invoke();
                Assert.That(answered, Is.Zero);
            }
            finally { Object.Destroy(bubble); }
        }

        /// <summary>
        /// The line is placed on the view its owner names (OFFERS-COME-TO-YOU-PLAN slice 3: the director names its camera
        /// rig's): a speaker behind that view shows nothing though the main camera sees them, and one the view sees shows
        /// though the main camera cannot. With no view named, or the view gone, the main camera places it, as it always did.
        /// </summary>
        [UnityTest]
        public IEnumerator NpcInvitationBubble_PlacesTheLineOnItsViewAndElseOnTheMainCamera()
        {
            director.SuspendNpcAutonomyForDiagnostics();
            director.ClosePanels(); yield return null;
            var bubble = director.gameObject.AddComponent<HouseNpcInvitationBubble>();
            GameObject view = null;
            try
            {
                var main = Camera.main;
                Assert.That(main, Is.Not.Null);
                Assert.That(bubble.ViewCamera, Is.Null, "No view named yet.");
                Vector3 seen = SceneComponents<Gamesim.House.HousePlayerController>()[0].transform.position + Vector3.up * 1.95f;
                bubble.Show("Morgan", "Can we talk?", 1f, seen, () => { }, () => { });
                yield return null;
                Assert.That(bubble.IsVisible, Is.True, "With no view named the main camera places the line.");

                // A view behind the speaker, looking away: placed for it, the line does not show.
                view = new GameObject("Invitation test view", typeof(Camera));
                var camera = view.GetComponent<Camera>();
                camera.enabled = false;
                view.transform.SetPositionAndRotation(seen + main.transform.forward * 4f, Quaternion.LookRotation(main.transform.forward));
                bubble.ViewCamera = camera;
                yield return null;
                Assert.That(bubble.IsVisible, Is.False, "Placed on its view: a speaker behind that view shows no line, though the main camera sees them.");

                // A speaker the main camera cannot see, in front of the view: the line shows.
                Vector3 hidden = main.transform.position - main.transform.forward * 3f;
                view.transform.SetPositionAndRotation(hidden - main.transform.forward * 4f, Quaternion.LookRotation(main.transform.forward));
                bubble.Show("Morgan", "Can we talk?", 1f, hidden, () => { }, () => { });
                yield return null;
                Assert.That(bubble.IsVisible, Is.True, "A speaker in front of its view shows, though the main camera cannot see them.");

                // The view gone: the main camera again, which cannot see this speaker. Destroy would wait for the end of
                // the frame, after this frame's LateUpdate had placed the line on the view once more; DestroyImmediate
                // leaves the bubble holding Unity's fake null for that LateUpdate, which is what Place must see through.
                Object.DestroyImmediate(view); view = null;
                Assert.That(bubble.ViewCamera == null, Is.True, "A destroyed view reads as Unity's null.");
                Assert.That(ReferenceEquals(bubble.ViewCamera, null), Is.False, "The bubble still holds the destroyed view, not an unset one.");
                yield return null;
                Assert.That(bubble.IsVisible, Is.False, "With its view gone the main camera places the line again.");
            }
            finally
            {
                if (view != null) Object.Destroy(view);
                Object.Destroy(bubble);
            }
        }
    }
}
