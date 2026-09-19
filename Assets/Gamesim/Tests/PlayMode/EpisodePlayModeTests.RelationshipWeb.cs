using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The notebook's Network section: the web around the player, the column that reads one
    /// houseguest, and the press that points it at somebody else.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// One node per houseguest still in the house, one edge per node — never an edge between
        /// two NPCs, because the player cannot know what they think of each other — and the column
        /// reading the player until a portrait is pressed.
        /// </summary>
        [UnityTest]
        public IEnumerator RelationshipWeb_DrawsTheHouseAroundThePlayerAndReadsWhoeverIsPressed()
        {
            RelationshipWeb.ClearSelection();
            director.OpenJournal();
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();

            var state = director.Snapshot;
            var section = Section();
            var graph = Under(section, RelationshipWeb.GraphName);
            var column = Under(section, RelationshipWeb.ColumnName);
            Assert.That(graph, Is.Not.Null, "The section should carry the web.");
            Assert.That(column, Is.Not.Null, "The section should carry the column beside it.");

            var nodes = graph.GetComponentsInChildren<Button>(true);
            Assert.That(nodes.Select(node => node.name),
                Is.EquivalentTo(state.Active.Select(actor => RelationshipWeb.NodeName(actor.name))),
                "A node per houseguest in the house, the player included, each named for them.");
            var edges = graph.GetComponentsInChildren<RectTransform>(true).Count(rect => rect.name == RelationshipWeb.EdgeName);
            Assert.That(edges, Is.EqualTo(state.Active.Count() - 1),
                "An edge from the player to each houseguest, and none between two NPCs.");

            Assert.That(Copy(column), Does.Contain(state.Find(state.playerId).name), "The column reads the player by default.");
            Assert.That(Copy(column), Does.Contain(RelationshipWeb.AlliesHeading).And.Contain(RelationshipWeb.RivalsHeading)
                .And.Contain(RelationshipWeb.SecretsHeading));

            // Press Maya: the notebook stays open, the column reads her, and the keyboard's
            // selection comes back to the node it was on after the rebuild.
            var maya = state.Find(ContentCatalog.MayaId);
            var node = nodes.Single(button => button.name == RelationshipWeb.NodeName(maya.name));
            Assert.That(node.navigation.mode, Is.Not.EqualTo(Navigation.Mode.None), "A node is in the keyboard ring.");
            EventSystem.current.SetSelectedGameObject(node.gameObject);
            node.onClick.Invoke();
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();

            Assert.That(director.IsPanelOpen, Is.True, "Pressing a node keeps the notebook open.");
            Assert.That(RelationshipWeb.Selected, Is.EqualTo(maya.id));
            section = Section();
            column = Under(section, RelationshipWeb.ColumnName);
            Assert.That(Copy(column), Does.Contain(maya.name), "The column now reads Maya.");
            Assert.That(Copy(column), Does.Contain(RelationshipWeb.BetweenHeading).And.Contain(RelationshipWeb.KnownHeading)
                .And.Contain(RelationshipWeb.HistoryHeading));
            Assert.That(Copy(column), Does.Not.Contain(RelationshipWeb.AlliesHeading),
                "Maya's allies are her business; the column reads the player's record about her.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(RelationshipWeb.NodeName(maya.name)),
                "The rebuild restores the keyboard to the node that was pressed.");

            // The halo moves with the selection.
            graph = Under(section, RelationshipWeb.GraphName);
            var halo = graph.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Halo");
            Assert.That(halo.parent.name, Is.EqualTo(RelationshipWeb.NodeName(maya.name)));

            // Nothing in the section clips at the standard size; the accessibility suite covers
            // the larger one for the whole notebook.
            var clipped = section.GetComponentsInChildren<TMP_Text>(true)
                .Where(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text))
                .Where(label => { label.ForceMeshUpdate(); return label.isTextOverflowing; })
                .Select(label => "'" + label.text + "'")
                .ToArray();
            Assert.That(clipped, Is.Empty, "Copy is clipped: " + string.Join(" | ", clipped));

            director.ClosePanels();
            yield return null;
            RelationshipWeb.ClearSelection();
        }

        /// <summary>The rail's jump still lands on the section, with the web at the top of it.</summary>
        [UnityTest]
        public IEnumerator RelationshipWeb_IsWhereTheRailJumpsTo()
        {
            RelationshipWeb.ClearSelection();
            director.ClosePanels();
            yield return null;
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Network);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();

            var section = Section();
            Assert.That(Under(section, RelationshipWeb.GraphName), Is.Not.Null);
            Assert.That(Under(section, RelationshipWeb.LegendName), Is.Not.Null, "The key travels with the web.");
            Assert.That(Copy(section), Does.Contain(RelationshipWeb.PerspectiveCopy),
                "The caveat about perspective lives in the key.");

            director.ClosePanels();
            yield return null;
        }

        private RectTransform Section()
        {
            var section = director.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => rect.name == EpisodeDirector.NotebookSection.Network && rect.gameObject.activeInHierarchy);
            Assert.That(section, Is.Not.Null, "The notebook should carry the network section.");
            return section;
        }

        private static RectTransform Under(RectTransform root, string name) =>
            root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(rect => rect.name == name);

        private static string Copy(RectTransform root) =>
            string.Join("\n", root.GetComponentsInChildren<TMP_Text>(true).Select(label => label.text));
    }
}
