using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The room directory as Refinement Kit 6 draws it: a card a room, every houseguest in the
        /// house in exactly one of them, the count over them agreeing with the house, and no card
        /// glowing at rest - every row used to glow, the empty rooms too.
        /// </summary>
        [UnityTest]
        public IEnumerator NotebookRooms_EveryoneIsInOneRoomCardAndNoCardGlows()
        {
            yield return SettleCast();
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Rooms);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var state = director.Snapshot;

            var head = ActiveRect(EpisodeHud.NotebookHeaderName);
            Assert.That(head, Is.Not.Null);
            var headWords = head.GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToArray();
            Assert.That(headWords, Does.Contain("Who is where"), "The page says what it is in its head.");
            Assert.That(string.Join("\n", headWords), Does.Contain("YOUR NOTEBOOK"));

            var map = ActiveRect(HouseMap.RootName);
            Assert.That(map, Is.Not.Null);
            var cards = map.Cast<Transform>().Where(t => t.name.StartsWith(HouseMap.CardPrefix)).ToArray();
            Assert.That(cards, Has.Length.EqualTo(8), "Every room has its card under All rooms.");
            foreach (var card in cards)
                Assert.That(card.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Glow"), Is.False,
                    card.name + " glows at rest.");

            var placed = map.GetComponentsInChildren<RectTransform>()
                .Where(t => t.name.StartsWith("Occupant · ")).Select(t => t.name.Substring("Occupant · ".Length)).ToArray();
            var inHouse = state.Active.Select(c => c.name).ToArray();
            Assert.That(placed, Is.EquivalentTo(inHouse), "Everyone in the house appears once, and only they do.");

            // The filter row is the page's section mark, the rect the rail scrolls to.
            var summary = ActiveRect(EpisodeDirector.NotebookSection.Rooms).GetComponentsInChildren<TMP_Text>()
                .Single(t => t.text.Contains("in the house")).text;
            Assert.That(summary, Does.StartWith(inHouse.Length + " in the house"), "The count agrees with the house.");

            foreach (var label in map.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False, "'" + label.text + "' fits its box.");
            }

            // "Occupied" leaves the empty rooms out and nothing else; it is view state only.
            int revision = state.revision;
            ButtonWithCaption("Occupied").onClick.Invoke();
            yield return null;
            map = ActiveRect(HouseMap.RootName);
            int occupied = map.Cast<Transform>().Count(t => t.name.StartsWith(HouseMap.CardPrefix));
            Assert.That(occupied, Is.EqualTo(director.WhoIsWhere().Count(room => room.Occupants.Count > 0)));
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "A filter commits nothing.");
            ButtonWithCaption("All rooms").onClick.Invoke();
            yield return null;

            Assert.That(ButtonWithCaption("House activities"), Is.Not.Null, "The page's secondary action is in its foot.");
            if (Application.isBatchMode) yield return CaptureFraming("notebook-rooms");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// A page opened from the rail while the overview is up closes the overview, and the rail
        /// lights the page on screen. It used to keep "Overview" lit over the Houseguests page, with
        /// the overview's shot and room chips still up behind it.
        /// </summary>
        [UnityTest]
        public IEnumerator Rail_APageOpenedFromTheOverviewIsTheOneLit()
        {
            yield return null;
            Assert.That(director.ShowOverview(), Is.True);
            yield return null;
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.People);
            yield return null;
            Assert.That(director.IsOverview, Is.False, "Opening a page ends the overview.");
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.People));
            var rail = ActiveRect(IconRail.RootName);
            var lit = rail.GetComponentsInChildren<RectTransform>().Where(t => t.name == IconRail.ActiveMarkName).ToArray();
            Assert.That(lit, Has.Length.EqualTo(1));
            Assert.That(lit[0].parent.name, Is.EqualTo("Houseguests"), "The rail lights the page on screen.");
            Assert.That(director.GetComponentsInChildren<Canvas>(true).Any(c => c.name.EndsWith(" label")), Is.False,
                "and the overview's room chips are gone.");
            Assert.That(player.InputEnabled, Is.False, "The house is paused under a page, as under every panel.");
            director.ClosePanels();
            yield return null;
        }
    }
}
