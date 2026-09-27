using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
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
        /// Walking up to someone during a ceremony opens a card sized to what it says (Refinement
        /// Kit 6's preview 01): the person, their mood and your trust as two separate pills, what
        /// they said once, and why there is nothing to choose. No budget, no dial - and nothing in
        /// the save moves for having been told so.
        /// </summary>
        [UnityTest]
        public IEnumerator Conversation_OutsideFreeTimeIsACardThatSpendsNothing()
        {
            yield return SettleCast();
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            var before = director.Snapshot;
            Assert.That(before.phase, Is.EqualTo(EpisodePhase.HoH));

            var npc = SceneComponents<HouseNpc>().First(actor => actor.gameObject.activeInHierarchy);
            yield return OpenNearbyNpc(npc);
            Assert.That(director.TalkingToId, Is.EqualTo(npc.Id));
            yield return null;
            Canvas.ForceUpdateCanvases();

            var card = ActiveRect(EpisodeHud.ConversationNoticeName);
            Assert.That(card, Is.Not.Null, "A ceremony conversation is the notice card.");
            var actor = before.Find(npc.Id);
            var words = card.GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToArray();
            Assert.That(words, Does.Contain(actor.name));
            Assert.That(words, Does.Contain("Mood: " + actor.mood), "Mood is its own pill.");
            Assert.That(words, Does.Contain("Your trust: " + EpisodeDirector.TrustFigure(before.Score(before.playerId, actor.id))),
                "and your trust is another.");
            Assert.That(ActiveRect(EpisodeHud.ConversationLockName).GetComponentsInChildren<TMP_Text>().Single().text,
                Is.EqualTo(EpisodeDirector.ConversationUnavailableLine));

            var visible = director.GetComponentsInChildren<TMP_Text>().Where(t => t.gameObject.activeInHierarchy).ToArray();
            var spoken = visible.Where(t => t.name == "NPC spoken dialogue").ToArray();
            Assert.That(spoken, Has.Length.EqualTo(1), "They say it once.");
            Assert.That(spoken[0].text, Does.Contain(HouseDialogue.Greeting(before, npc.Id)));
            Assert.That(string.Join("\n", visible.Select(t => t.text)), Does.Not.Match(@"\d+ actions? left"),
                "No budget on a card that spends none of it.");
            Assert.That(director.GetComponentsInChildren<Button>().Any(b => b.IsActive()
                && b.GetComponentsInChildren<TMP_Text>().Any(t => t.text == EpisodeHud.SmallTalkCaption)), Is.False,
                "and nothing to choose.");

            // Sized to what it holds, not to the screen.
            var panel = card.GetComponentsInParent<RectTransform>().First(r => r.name == "Episode panel");
            float need = card.GetComponent<LayoutElement>().preferredHeight;
            Assert.That(panel.rect.height, Is.LessThanOrEqualTo(need + 80f), "The card is as tall as what it says.");
            Assert.That(panel.rect.height, Is.GreaterThanOrEqualTo(need), "and it all fits.");
            foreach (var label in card.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False, "'" + label.text + "' fits its box.");
            }
            if (Application.isBatchMode) yield return CaptureFraming("conversation-unavailable");

            ButtonWithCaption("Close  [Esc]").onClick.Invoke();
            yield return null;
            Assert.That(director.TalkingToId, Is.Null);
            AssertEquivalent(before, director.Snapshot);
        }
    }
}
