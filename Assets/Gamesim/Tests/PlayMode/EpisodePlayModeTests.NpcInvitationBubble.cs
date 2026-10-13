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
    }
}
