using System;
using System.Collections;
using System.Reflection;
using Gamesim.Episode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private Button PressableProbe(Action action)
        {
            var hud = director.GetComponent<EpisodeHud>();
            var rect = new GameObject("Pressable lifecycle probe", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(hud.transform, false);
            var method = typeof(EpisodeHud).GetMethod("Pressable", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (Button)method.Invoke(hud, new object[] { rect, action });
        }

        [UnityTest]
        public IEnumerator Pressable_DisabledInactiveAndDestroyedControlsCannotInvokeAnAction()
        {
            int calls = 0; var button = PressableProbe(() => calls++); var retained = button.onClick;
            button.interactable = false; retained.Invoke(); Assert.That(calls, Is.Zero);
            button.interactable = true; retained.Invoke(); Assert.That(calls, Is.EqualTo(1));
            button.gameObject.SetActive(false); retained.Invoke(); Assert.That(calls, Is.EqualTo(1));
            button.gameObject.SetActive(true); retained.Invoke(); Assert.That(calls, Is.EqualTo(2));
            Object.Destroy(button.gameObject); yield return null;
            Assert.DoesNotThrow(() => retained.Invoke()); Assert.That(calls, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator Pressable_AnAbsentActionIsInertEvenWhenItsEventIsInvokedDirectly()
        {
            var button = PressableProbe(null);
            Assert.DoesNotThrow(() => button.onClick.Invoke());
            Object.Destroy(button.gameObject); yield return null;
        }

        [UnityTest]
        public IEnumerator Pressable_NonInteractableCanvasGroupsCannotBypassEligibility()
        {
            int calls = 0; var button = PressableProbe(() => calls++);
            var group = button.gameObject.AddComponent<CanvasGroup>(); group.interactable = false;
            yield return null;
            button.onClick.Invoke(); Assert.That(calls, Is.Zero);
            group.interactable = true; yield return null;
            button.onClick.Invoke(); Assert.That(calls, Is.EqualTo(1));
            Object.Destroy(button.gameObject); yield return null;
        }
    }
}
