using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private RectTransform EpisodePanel() => director.GetComponentsInChildren<RectTransform>()
            .FirstOrDefault(rect => rect.name == "Episode panel" && rect.gameObject.activeInHierarchy);

        /// <summary>
        /// A panel opened from closed rises into place - the place its activity's layout gave it.
        /// The reveal read its rest position the moment the panel was built, before the layout had
        /// moved it, and carried every activity panel back to the docked panel's offset: the
        /// settings column landed at the canvas's left edge, the conversation's frame over the rail.
        /// </summary>
        [UnityTest]
        public IEnumerator Reveal_AnActivityPanelSettlesWhereItsLayoutPutsIt()
        {
            director.ClosePanels();
            yield return null; yield return null;

            // Landed at once, as a caller that needs it settled lands it.
            director.OpenSettings();
            var panel = EpisodePanel();
            var laidOut = panel.anchoredPosition;
            var reveal = panel.GetComponent<HudReveal>();
            Assert.That(reveal, Is.Not.Null, "This fixture needs a panel that is revealed, not one that simply appears.");
            reveal.Finish();
            Assert.That(Vector2.Distance(panel.anchoredPosition, laidOut), Is.LessThan(0.5f),
                "The landed panel sits at " + panel.anchoredPosition + ", not where its layout put it (" + laidOut + ").");
            director.ClosePanels();
            yield return null; yield return null;

            // And left to rise on its own.
            director.OpenSettings();
            laidOut = EpisodePanel().anchoredPosition;
            float settled = Time.realtimeSinceStartup + 0.3f;
            while (Time.realtimeSinceStartup < settled) yield return null;
            panel = EpisodePanel();
            Assert.That(Vector2.Distance(panel.anchoredPosition, laidOut), Is.LessThan(0.5f),
                "The revealed panel came to rest at " + panel.anchoredPosition + ", not where its layout put it (" + laidOut + ").");
            Canvas.ForceUpdateCanvases();
            Assert.That(ScreenRect(panel).Overlaps(ScreenRect(ActiveRect(IconRail.RootName))), Is.False,
                "The settings panel came to rest over the rail.");
            director.ClosePanels();
            yield return null;
        }
    }
}
