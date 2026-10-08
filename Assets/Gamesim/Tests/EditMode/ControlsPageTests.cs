using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The settings' CONTROLS section (PLAN A, A4) is rendered from the maps: one line for every
    /// action of the episode's maps, each saying what it does and what its action is bound to now.
    /// </summary>
    public sealed class ControlsPageTests
    {
        [Test]
        public void ThePage_ListsEveryActionOfTheEpisodesMaps()
        {
            using (var actions = new HouseCameraActions())
            {
                var lines = ControlsPage.Lines(actions);
                var expected = actions.Asset.actionMaps.Where(map => map.name != HouseCameraActions.DialogueMapName)
                    .SelectMany(map => map.actions.Select(action => map.name + "/" + action.name)).ToArray();
                Assert.That(lines.Select(line => line.Map + "/" + line.Action), Is.EqualTo(expected), "Every action, in the map's order.");
                Assert.That(lines.Select(line => line.Name).Distinct().Count(), Is.EqualTo(lines.Count), "Each line is named for its action.");
                Assert.That(lines.Any(line => line.Map == HouseCameraActions.DialogueMapName), Is.False, "The prototype's dialogue is not the episode's.");
                foreach (var line in lines)
                {
                    var row = InputGlossary.Find(line.Map, line.Action);
                    Assert.That(row, Is.Not.Null, line.Name + " has its glossary row.");
                    Assert.That(line.Context, Is.EqualTo(row.Context));
                    Assert.That(line.Hint, Is.EqualTo(row.Hint));
                    Assert.That(line.Keys + line.Pad, Is.Not.Empty, line.Name + " can be pressed.");
                    Assert.That(line.Text, Does.StartWith(row.Hint + ":  "), line.Name);
                    if (line.Keys.Length > 0) Assert.That(line.Text, Does.Contain(line.Keys));
                    if (line.Pad.Length > 0) Assert.That(line.Text, Does.Contain("Pad " + line.Pad));
                }
            }
        }

        [Test]
        public void ThePage_AlsoSaysThePressesNoActionHolds_UnderTheirOwnHeadings()
        {
            using (var actions = new HouseCameraActions())
            {
                var lines = ControlsPage.Lines(actions);
                var page = ControlsPage.Page(actions);
                var outside = InputGlossary.Rows.Where(row => row.OutsideTheAsset).ToArray();
                Assert.That(outside, Is.Not.Empty);
                Assert.That(page, Has.Count.EqualTo(lines.Count + outside.Length), "Every action, and every press outside the asset.");
                Assert.That(page.Where(line => line.Map != InputGlossary.InterfaceMap && line.Map != InputGlossary.PointerMap && line.Map != InputGlossary.TypingMap)
                    .Select(line => line.Name), Is.EqualTo(lines.Select(line => line.Name)), "The actions keep the map's order.");
                Assert.That(page.Select(line => line.Name).Distinct().Count(), Is.EqualTo(page.Count), "Each line is named once.");
                foreach (var row in outside)
                {
                    var matches = page.Where(item => item.Map == row.Map && item.Action == row.Action).ToArray();
                    Assert.That(matches, Has.Length.EqualTo(1), row + " is on the page, once.");
                    Assert.That(matches[0].Name, Is.EqualTo(ControlsPage.RowPrefix + row.Map + "/" + row.Action), row.ToString());
                    Assert.That(matches[0].Text, Is.EqualTo(ControlsPage.Words(row.Hint, row.Keyboard, row.Pad)), row.ToString());
                }
                Assert.That(page.Any(line => line.Text == "Walk to the floor clicked:  Left click"), Is.True, "The click on the floor.");
                Assert.That(page.Any(line => line.Text == "Press the focused control:  Enter   ·   Pad A"), Is.True, "Enter and A on a panel.");

                // A heading is said once: each context's lines stand together.
                var seen = new System.Collections.Generic.List<string>();
                string current = null;
                foreach (var line in page)
                {
                    if (line.Context == current) continue;
                    Assert.That(seen.Contains(line.Context), Is.False, line.Context + " comes back after another heading at " + line.Name + ".");
                    current = line.Context;
                    seen.Add(current);
                }
            }
        }

        [Test]
        public void ALine_ReadsWhatItDoesThenTheKeysThenThePad()
        {
            Assert.That(ControlsPage.Words("Open the notebook", "J", "Select"), Is.EqualTo("Open the notebook:  J   ·   Pad Select"));
            Assert.That(ControlsPage.Words("Close the top panel", "", "B"), Is.EqualTo("Close the top panel:  Pad B"));
            Assert.That(ControlsPage.Words("Move the opening on", "Space", ""), Is.EqualTo("Move the opening on:  Space"));
        }

        [Test]
        public void TheTourLine_IsTheGlossarysOnTheKeyboard()
        {
            Assert.That(HouseTutorial.ControlsLine, Is.EqualTo(InputGlossary.TourLine(false)),
                "The tour's line is the keyboard's words, as it always read.");
        }
    }
}
