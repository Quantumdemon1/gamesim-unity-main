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
        [UnityTest]
        public IEnumerator RelationshipWeb_LegalLongNamesKeepFullCastLabelsSeparateAndFullIdentityReadable()
        {
            RelationshipWeb.ClearSelection();
            var canvasObject=new GameObject("Long-name relationship canvas",typeof(RectTransform),typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var fixture=new GameObject("Long-name relationship viewport",typeof(RectTransform),typeof(VerticalLayoutGroup));
            fixture.transform.SetParent(canvasObject.transform,false);
            try
            {
                var viewport=(RectTransform)fixture.transform;viewport.sizeDelta=new Vector2(1160,490);
                var layout=fixture.GetComponent<VerticalLayoutGroup>();layout.childControlWidth=true;layout.childControlHeight=true;
                layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
                var state=SeasonBuilder.Create(new SeasonBuilder.Choice{HouseSize=12},711);
                int index=0;
                foreach(var actor in state.Active)
                {
                    var draft=CharacterDraft.Blank();draft.Name=(char)('A'+index++)+new string('W',CharacterDraft.NameLimit-1);
                    Assert.That(draft.TryValidate(out var error),Is.True,error);
                    actor.name=draft.Name;
                }
                var root=RelationshipWeb.Build(viewport,state,1.2f,TMP_Settings.defaultFontAsset,_=>null,_=>{},490f);
                yield return null;LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
                var graph=Under(root,RelationshipWeb.GraphName);
                var nodes=graph.GetComponentsInChildren<Button>();
                Assert.That(nodes.Select(node=>node.name),Is.EquivalentTo(state.Active.Select(actor=>RelationshipWeb.NodeName(actor.name))));
                var chips=graph.GetComponentsInChildren<RectTransform>().Where(rect=>rect.name=="Name chip").ToArray();
                Assert.That(chips.Length,Is.EqualTo(12));
                for(int i=0;i<chips.Length;i++)
                {
                    var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(graph,chips[i]);
                    Assert.That(bounds.min.x,Is.GreaterThanOrEqualTo(graph.rect.xMin));
                    Assert.That(bounds.max.x,Is.LessThanOrEqualTo(graph.rect.xMax));
                    var label=chips[i].GetComponentInChildren<TMP_Text>();label.ForceMeshUpdate(true);
                    Assert.That(label.fontSize,Is.EqualTo(14));
                    Assert.That(label.enableAutoSizing,Is.False,"Large text is preserved when a name is abbreviated.");
                    if(label.text!="YOU")Assert.That(label.isTextTruncated,Is.True,"A 100-character word must end in an ellipsis.");
                    for(int j=i+1;j<chips.Length;j++)
                        Assert.That(ScreenRect(chips[i]).Overlaps(ScreenRect(chips[j])),Is.False,"Long names must not merge adjacent houseguests.");
                }
                var selected=state.Active.Last();
                nodes.Single(node=>node.name==RelationshipWeb.NodeName(selected.name)).onClick.Invoke();
                Assert.That(RelationshipWeb.Selected,Is.EqualTo(selected.id));
                Object.Destroy(root.gameObject);yield return null;
                root=RelationshipWeb.Build(viewport,state,1.2f,TMP_Settings.defaultFontAsset,_=>null,_=>{},490f);
                yield return null;LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
                var column=Under(root,RelationshipWeb.ColumnName);
                var fullName=column.GetComponentsInChildren<TMP_Text>().Single(label=>label.text==selected.name);
                fullName.ForceMeshUpdate(true);
                Assert.That(fullName.fontSize,Is.EqualTo(20));
                Assert.That(fullName.enableAutoSizing,Is.False);
                Assert.That(fullName.isTextOverflowing,Is.False,"The detail view wraps the complete identity at the requested size.");
                Assert.That(fullName.textInfo.characterCount,Is.EqualTo(CharacterDraft.NameLimit));
                Assert.That(fullName.textInfo.lineCount,Is.GreaterThan(1));
                Assert.That(column.GetComponentInParent<ScrollRect>().vertical,Is.True,"Long identity details remain scrollable.");
            }
            finally{Object.Destroy(canvasObject);RelationshipWeb.ClearSelection();}
        }

        [UnityTest]
        public IEnumerator RelationshipWeb_ShortViewportKeepsFullCastAndEveryLegendLabelVisible()
        {
            var fixture=new GameObject("Short relationship viewport",typeof(RectTransform),typeof(VerticalLayoutGroup));
            try
            {
                var viewport=(RectTransform)fixture.transform;viewport.sizeDelta=new Vector2(1160,490);
                var layout=fixture.GetComponent<VerticalLayoutGroup>();layout.childControlWidth=true;layout.childControlHeight=true;
                layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
                var state=SeasonBuilder.Create(new SeasonBuilder.Choice{HouseSize=12},711);
                var root=RelationshipWeb.Build(viewport,state,1.2f,TMP_Settings.defaultFontAsset,_=>null,_=>{},490f);
                LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
                yield return null;
                LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
                var graph=Under(root,RelationshipWeb.GraphName);
                Assert.That(graph.rect.height,Is.LessThanOrEqualTo(viewport.rect.height));
                Assert.That(graph.GetComponentsInChildren<Button>().Length,Is.EqualTo(12));
                var legend=Under(root,RelationshipWeb.LegendName);
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(graph,legend);
                Assert.That(bounds.min.y,Is.GreaterThanOrEqualTo(graph.rect.yMin-1));
                Assert.That(bounds.max.x,Is.LessThanOrEqualTo(graph.rect.xMax+1));
                Assert.That(Copy(legend),Does.Contain("Friendship").And.Contain("Alliance").And.Contain("Rivalry")
                    .And.Contain("Distrust").And.Contain("Neutral").And.Contain(RelationshipWeb.PerspectiveCopy));
                var selected=root.GetComponentsInChildren<RectTransform>().Single(rect=>rect.name=="Selected relationship filter");
                Assert.That(selected.parent.name,Is.EqualTo("Filter relationships: All"),"Selected filters have a visible shape beyond color.");
            }
            finally{Object.Destroy(fixture);RelationshipWeb.ClearSelection();}
        }

        [UnityTest]
        public IEnumerator RelationshipWeb_DetailsScrollWithoutMovingTheGraphAndFiltersCommitNothing()
        {
            RelationshipWeb.ClearSelection();
            director.OpenJournal();yield return null;
            var state=director.Snapshot;
            for(int i=0;i<3;i++)state.memories.Add(new MemoryState{ownerId=state.playerId,week=99-i,
                text=string.Join(" ",Enumerable.Repeat("A remembered conversation explains this relationship.",18))});
            var hud=director.GetComponent<EpisodeHud>();
            hud.Begin(state,director.StatusMessage,false,true);
            hud.SocialGraphPanel(state);hud.Mark(EpisodeDirector.NotebookSection.Network);
            yield return null;yield return null;Canvas.ForceUpdateCanvases();
            var graph=Under(Section(),RelationshipWeb.GraphName);
            var before=ScreenRect(graph);
            var details=Under(Section(),RelationshipWeb.DetailsScrollName).GetComponent<ScrollRect>();
            Assert.That(details.content.rect.height,Is.GreaterThan(details.viewport.rect.height));
            var read=details.GetComponentsInChildren<Button>().Single(button=>button.name=="Read later details");
            read.onClick.Invoke();yield return null;Canvas.ForceUpdateCanvases();
            Assert.That(details.verticalNormalizedPosition,Is.LessThan(1));
            Assert.That(ScreenRect(graph),Is.EqualTo(before),"Reading long known history leaves the complete graph in place.");

            // Return to the real committed snapshot before checking the filter's normal callback.
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Network);yield return null;yield return null;
            var revision=director.Snapshot.revision;
            var filterButton=director.GetComponentsInChildren<Button>().Single(button=>button.name=="Filter relationships: Tension");
            filterButton.onClick.Invoke();yield return null;yield return null;
            Assert.That(director.Snapshot.revision,Is.EqualTo(revision));
            Assert.That(RelationshipWeb.CurrentFilter,Is.EqualTo(RelationshipWeb.Filter.Tension));
            graph=Under(Section(),RelationshipWeb.GraphName);
            Assert.That(graph.GetComponentsInChildren<Button>().Length,Is.EqualTo(RelationshipWeb.FilteredOthers(director.Snapshot).Count+1));
            director.ClosePanels();RelationshipWeb.ClearSelection();yield return null;
        }

        [UnityTest]
        public IEnumerator RelationshipWeb_WholeGraphAndLegendFitTheDedicatedViewportAtBothTextSizes()
        {
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                director.ShowNotebookSection(EpisodeDirector.NotebookSection.Network);
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var hud = director.GetComponent<EpisodeHud>();
                Assert.That(hud.CurrentActivityLayout, Is.EqualTo(EpisodeHud.ActivityLayout.Relationships));
                var graph = Under(Section(), RelationshipWeb.GraphName);
                var scroll = graph.GetComponentInParent<ScrollRect>();
                Assert.That(scroll, Is.Not.Null);
                var viewport = ScreenRect(scroll.viewport);
                var frame = ScreenRect(graph);
                Assert.That(frame.yMax, Is.LessThanOrEqualTo(viewport.yMax + 1f));
                Assert.That(frame.yMin, Is.GreaterThanOrEqualTo(viewport.yMin - 1f), "The complete graph and key must be visible together.");
                Assert.That(frame.xMin, Is.GreaterThanOrEqualTo(viewport.xMin - 1f));
                Assert.That(frame.xMax, Is.LessThanOrEqualTo(viewport.xMax + 1f));
                director.ClosePanels();
                yield return null;
            }
        }

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
