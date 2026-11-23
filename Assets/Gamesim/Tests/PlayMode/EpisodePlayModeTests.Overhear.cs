using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Overheard, on screen (plan 32 §I4, slice S2b): in a season at story rules 12 the relationship web draws the player's
    /// hunch as a line of its own with its confidence, the side column says it, every label draws at Standard and at
    /// Largest, and the "Hunches" chip hides and shows the lines without committing anything. Installed through a saved
    /// fixture (<see cref="InstallHouseTalkSeason"/>), since no season the game starts reaches 12 before A-ON.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static int HunchLines(RectTransform graph) =>
            graph.GetComponentsInChildren<RectTransform>(true).Count(rect => rect.name == RelationshipWeb.HunchMarkerName);

        [UnityTest]
        public IEnumerator Overhear_HunchLinesAndTheirChipDrawOnTheWebAtTwelve()
        {
            yield return SettleCast();
            var pair = director.Snapshot.contestants.Where(c => !c.isPlayer && c.status == ContestantStatus.Active).Select(c => c.id).Take(2).ToArray();
            yield return InstallHouseTalkSeason(StoryRules.Manners, state => state.ledger.standings.Add(new StandingRow
                { week = state.week, fromId = pair[0], toId = pair[1], source = ClaimSource.Seen, score = HunchTone.Warm }));
            var installed = director.Snapshot;
            Assert.That(Overhearing.Hunches(installed).Count, Is.EqualTo(1), "The sighting came back as it was written.");
            string line = Overhearing.Line(installed, Overhearing.Hunches(installed)[0]);
            RelationshipWeb.ClearSelection();
            RelationshipWeb.SetFilter(installed, RelationshipWeb.Filter.All);
            if (!RelationshipWeb.HunchesShown(installed)) RelationshipWeb.ToggleHunches(installed);

            foreach (var size in TextSizes.All)
            {
                yield return ApplyTextSize(size);
                director.ShowNotebookSection(EpisodeDirector.NotebookSection.Network);
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var graph = Under(Section(), RelationshipWeb.GraphName);
                Assert.That(HunchLines(graph), Is.EqualTo(1), size + ": one hunch, one line.");
                var label = graph.GetComponentsInChildren<TMP_Text>().Single(text => text.name == RelationshipWeb.HunchLabelName);
                Assert.That(label.text, Is.EqualTo("maybe"), size + ": one sighting reads as a maybe.");
                var column = Under(Section(), RelationshipWeb.ColumnName);
                Assert.That(column.GetComponentsInChildren<TMP_Text>().Select(text => text.text), Has.Some.EqualTo(line), size + ": the side column says it.");
                AssertEveryLabelDraws(Section(), "The relationship web with a hunch at " + size);
            }

            // Read here, with the web open: what the open itself commits is not what this case is about.
            int revision = director.Snapshot.revision;
            director.GetComponentsInChildren<Button>().Single(button => button.name == RelationshipWeb.HunchesChipName).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(HunchLines(Under(Section(), RelationshipWeb.GraphName)), Is.EqualTo(0), "The chip hides the lines.");
            director.GetComponentsInChildren<Button>().Single(button => button.name == RelationshipWeb.HunchesChipName).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(HunchLines(Under(Section(), RelationshipWeb.GraphName)), Is.EqualTo(1), "And shows them again.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "The chip commits nothing.");
            yield return ApplyTextSize(TextSize.Standard);
            director.ClosePanels();
            yield return null;
        }
    }
}
