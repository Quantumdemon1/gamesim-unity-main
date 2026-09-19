using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// MASTER-PLAN §3.D step 6: motion and feedback. A closing panel fades out, a pressed button
    /// dips, a meter's fill travels - and every one of them is nothing at all under reduced motion,
    /// which is what keeps D4 true.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Motion_AClosingPanelLeavesAFadingGhostThatOwnsNoControls()
        {
            var hud = Object.FindFirstObjectByType<EpisodeHud>();
            Assert.That(hud, Is.Not.Null);
            hud.ReducedMotion = false;
            director.OpenSettings();
            yield return null;
            Assert.That(director.IsPanelOpen, Is.True);
            director.ClosePanels();
            var ghost = GameObject.Find(HudFade.GhostName);
            Assert.That(ghost, Is.Not.Null, "The panel that just closed fades out on a ghost canvas.");
            Assert.That(ghost.GetComponent<CanvasGroup>().blocksRaycasts, Is.False, "and blocks nothing behind it");
            Assert.That(ghost.GetComponent<CanvasGroup>().interactable, Is.False, "nor takes a press");
            // Its controls go at the end of the frame, not during the press that closed the panel.
            yield return null;
            Assert.That(ghost.GetComponentsInChildren<Selectable>(true), Is.Empty, "The ghost owns no controls.");
            yield return null;
            Assert.That(ghost == null || ghost.GetComponent<CanvasGroup>().alpha < 1f, Is.True, "It is fading.");
            float deadline = Time.realtimeSinceStartup + 1f;
            while (GameObject.Find(HudFade.GhostName) != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(GameObject.Find(HudFade.GhostName), Is.Null, "and gone within a second.");

            hud.ReducedMotion = true;
            try
            {
                director.OpenSettings();
                yield return null;
                director.ClosePanels();
                Assert.That(GameObject.Find(HudFade.GhostName), Is.Null, "Reduced motion: the panel simply closes.");
            }
            finally { hud.ReducedMotion = false; }
        }

        [UnityTest]
        public IEnumerator Motion_APressedButtonDipsUnlessMotionIsReduced()
        {
            var hud = Object.FindFirstObjectByType<EpisodeHud>();
            hud.ReducedMotion = false;
            director.OpenSettings();
            yield return null;
            var button = director.GetComponentsInChildren<Button>().First(b => b.GetComponent<HudPress>() != null);
            var pointer = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            Assert.That(button.transform.localScale.x, Is.LessThan(1f), "The pressed button dips.");
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            Assert.That(button.transform.localScale.x, Is.EqualTo(1f).Within(0.0001f), "and comes back on release.");

            hud.ReducedMotion = true;
            try
            {
                director.ClosePanels(); director.OpenSettings();
                yield return null;
                button = director.GetComponentsInChildren<Button>().First(b => b.GetComponent<HudPress>() != null);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                Assert.That(button.transform.localScale.x, Is.EqualTo(1f).Within(0.0001f), "Reduced motion: no dip.");
            }
            finally { hud.ReducedMotion = false; director.ClosePanels(); }
        }

        [UnityTest]
        public IEnumerator Motion_AMeterFillTravelsToItsNewValue()
        {
            var go = new GameObject("Meter fill under test", typeof(RectTransform));
            try
            {
                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(0.2f, 1f);
                go.AddComponent<HudFill>().Play(0.2f, 0.8f);
                Assert.That(rect.anchorMax.x, Is.EqualTo(0.2f).Within(0.0001f), "It starts where the meter was.");
                yield return null; yield return null;
                Assert.That(rect.anchorMax.x, Is.GreaterThan(0.2f).And.LessThan(0.8f), "A frame later it is on its way.");
                float deadline = Time.realtimeSinceStartup + 1f;
                while (go.GetComponent<HudFill>() != null && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(rect.anchorMax.x, Is.EqualTo(0.8f).Within(0.0001f), "and it lands exactly.");
                Assert.That(go.GetComponent<HudFill>(), Is.Null, "The traveller removes itself.");
            }
            finally { Object.Destroy(go); }
        }
    }
}
