using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The largest text reaches the house's menus and side cards (U9, backlog item 23): the cast
        /// menu's rows were drawn at 16 whatever the size, and Listen in at 15 in a 36-unit button.
        /// At the largest size the rows ask for 22 and Listen in for 21, each in a box 1.3 times its
        /// words, and the Nearby card stays no taller than the week card whose place it takes.
        /// TextSizeFitTests holds the arithmetic at every size; this is the drawn screen.
        /// </summary>
        [UnityTest]
        public IEnumerator LargestText_TheCastMenuAndTheNearbyCardGrowWithIt()
        {
            yield return null;
            director.SetTextSize(TextSize.Largest);
            yield return null; yield return null;
            try
            {
                var hud = director.GetComponentInChildren<EpisodeHud>();
                Assert.That(hud.FontScale, Is.EqualTo(1.4f).Within(1e-4f), "The HUD is at the largest text.");

                var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
                string first = maya.DisplayName.Split(' ')[0];
                Button Chip()
                {
                    var rail = director.GetComponentsInChildren<RectTransform>().Last(rect => rect.name == CastRail.RootName);
                    return rail.Cast<Transform>().Select(child => child.GetComponent<Button>()).Last(button => button != null && button.name == maya.DisplayName);
                }
                Chip().onClick.Invoke();
                yield return null;
                var menu = ActiveRect(EpisodeHud.CastMenuName);
                Assert.That(menu, Is.Not.Null, "The chip opens its houseguest's card.");
                foreach (var caption in new[] { EpisodeHud.CastTalkCaption(first), EpisodeHud.CastProfileCaption(first) })
                {
                    var row = ButtonWithCaption(caption);
                    Assert.That(row.transform.IsChildOf(menu), Is.True, "'" + caption + "' is on the card.");
                    var words = row.GetComponentInChildren<TMP_Text>();
                    Assert.That(words.fontSizeMax, Is.EqualTo(Mathf.RoundToInt(16 * 1.4f)), "'" + caption + "' asks for the largest size.");
                    Assert.That(words.rectTransform.rect.height, Is.GreaterThanOrEqualTo(words.fontSizeMax * 1.3f - .5f), "'" + caption + "' has a box 1.3 times its words.");
                }
                Chip().onClick.Invoke();
                yield return null;
                Assert.That(ActiveRect(EpisodeHud.CastMenuName), Is.Null, "The same chip again closes it.");

                var caption2 = SceneComponents<HouseConversationCaption>().First();
                caption2.Show("Maya Hassan", "Riley Johnson", "strategy", director.TextScale);
                yield return null; yield return null;
                var card = ActiveRect(EpisodeHud.NearbyCardName);
                Assert.That(card, Is.Not.Null, "A witnessed conversation brings up the Nearby card.");
                var listen = ButtonWithCaption(EpisodeHud.ListenInCaption);
                Assert.That(listen.transform.IsChildOf(card), Is.True, "Listen in is on the card.");
                var label = listen.GetComponentInChildren<TMP_Text>();
                Assert.That(label.fontSizeMax, Is.EqualTo(Mathf.RoundToInt(15 * 1.4f)), "Listen in asks for the largest size.");
                Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.fontSizeMax * 1.3f - .5f), "Listen in has a box 1.3 times its words.");
                Assert.That(card.rect.height, Is.LessThanOrEqualTo(TextSizeFit.WeekCardRoom + .5f), "The card stays no taller than the week card.");
                caption2.Hide();
                yield return null;
            }
            finally { director.SetTextSize(TextSize.Standard); }
        }
    }
}
