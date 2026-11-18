using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Menu slice 3, part b, ruling N6 iii: trust is drawn ONE way on every reading page - one chip,
    /// from one helper (Presentation/TrustChip.cs), on the value and the bands the houseguests' table
    /// always used: your character's score toward them, whole and signed, above zero, zero or below
    /// zero. The bands and the words are pinned here, and the source is scanned so that each reading
    /// page that showed trust its own way (the table's figure, the notes' five-band word, the web's
    /// tiles and its allies' and rivals' words, the alliances' readings) draws the chip. Unity-free,
    /// so the dotnet subset runs it; the chip's drawing is pinned in EditMode only, below.
    /// </summary>
    public sealed class MenuNotebookBTrustChipTests
    {
        [TestCase(0.0, TrustChip.Band.Even)]
        [TestCase(0.4, TrustChip.Band.Even)]
        [TestCase(-0.4, TrustChip.Band.Even)]
        [TestCase(0.5, TrustChip.Band.Above)]
        [TestCase(-0.5, TrustChip.Band.Below)]
        [TestCase(12.0, TrustChip.Band.Above)]
        [TestCase(-7.6, TrustChip.Band.Below)]
        [TestCase(100.0, TrustChip.Band.Above)]
        [TestCase(-100.0, TrustChip.Band.Below)]
        public void TheBands_AreTheWholeFiguresSign(double score, TrustChip.Band band)
        {
            Assert.That(TrustChip.BandOf(score), Is.EqualTo(band), "The band of " + score.ToString(CultureInfo.InvariantCulture));
        }

        [TestCase(0.0, "0")]
        [TestCase(-0.4, "0")]
        [TestCase(0.4, "0")]
        [TestCase(12.4, "+12")]
        [TestCase(12.5, "+13")]
        [TestCase(-7.5, "-8")]
        [TestCase(-15.0, "-15")]
        [TestCase(100.0, "+100")]
        [TestCase(-100.0, "-100")]
        public void TheFigure_IsWholeAndSigned_AndNeverMinusZero(double score, string figure)
        {
            Assert.That(TrustChip.Figure(score), Is.EqualTo(figure));
        }

        [Test]
        public void TheFigure_IsTheTablesOwn_AndItsSignIsTheBand()
        {
            // The houseguests' table printed score.ToString("+0;-0;0"): the chip keeps its words (values away from a half).
            foreach (double score in new[] { -100.0, -40.4, -40.0, -15.0, -14.6, -1.0, 0.0, 1.0, 14.6, 15.0, 39.9, 99.4 })
            {
                Assert.That(TrustChip.Figure(score), Is.EqualTo(score.ToString("+0;-0;0", CultureInfo.InvariantCulture)), "The figure for " + score);
                string figure = TrustChip.Figure(score);
                var band = TrustChip.BandOf(score);
                Assert.That(band == TrustChip.Band.Above, Is.EqualTo(figure.StartsWith("+", StringComparison.Ordinal)), figure);
                Assert.That(band == TrustChip.Band.Below, Is.EqualTo(figure.StartsWith("-", StringComparison.Ordinal)), figure);
                Assert.That(band == TrustChip.Band.Even, Is.EqualTo(figure == "0"), figure);
            }
        }

        [Test]
        public void TheChip_SaysItsStateInWords_NotByColourAlone()
        {
            Assert.That(TrustChip.Label, Is.EqualTo("Your trust"));
            Assert.That(TrustChip.Spoken(22), Is.EqualTo("Your trust +22, above zero"));
            Assert.That(TrustChip.Spoken(-9), Is.EqualTo("Your trust -9, below zero"));
            Assert.That(TrustChip.Spoken(0.2), Is.EqualTo("Your trust 0, at zero"));
            var words = Enum.GetValues(typeof(TrustChip.Band)).Cast<TrustChip.Band>().Select(TrustChip.ShapeWords).ToList();
            Assert.That(words, Is.Unique, "Each band's shape is said in its own words.");
        }

        /// <summary>The reading pages draw trust through the chip, and no longer each in their own words.</summary>
        [Test]
        public void EveryReadingPageThatShowsTrust_DrawsTheChip()
        {
            string roster = Read("Episode/EpisodeHud.Roster.cs");
            Assert.That(roster, Does.Contain("TrustChip.Build(row, entry.TrustScore"), "The houseguests' table's YOUR TRUST column.");
            Assert.That(roster, Does.Contain("TrustChip.Build(trust, view.TrustScore.Value"), "A profile's YOUR TRUST card.");
            Assert.That(roster, Does.Not.Contain("entry.TrustTint"), "The table's figure is no longer tinted text of its own.");

            string notes = Region(Read("Episode/EpisodeHud.FreeTime.cs"), "public RectTransform NotesCard(");
            Assert.That(notes, Does.Contain("TrustChip.Build(card, state.Score(state.playerId, actor.id)"), "A notes card's standing is the chip.");
            Assert.That(notes, Does.Not.Contain("standingWord +"), "and not the five-band word beside the status.");

            string web = Read("Presentation/RelationshipWeb.cs");
            Assert.That(web, Does.Contain("TrustChip.Build(row, state.Score(state.playerId, focus.id)"), "The web's stats for a chosen houseguest.");
            Assert.That(web, Does.Contain("TrustChip.Build(row, state.Score(state.playerId, actor.id)"), "The web's allies and rivals.");
            Assert.That(web, Does.Not.Contain("\"Standing\", EdgeColour(kind)"), "The web's Standing tile said the same value in a second way.");
            Assert.That(web, Does.Not.Contain("AllyWord(state, ally.id)").And.Not.Contain("RivalWord(state, rival.id)"),
                "The allies' and rivals' rows said it in words of their own.");

            string alliances = Read("Episode/EpisodeHud.Alliances.cs");
            Assert.That(alliances, Does.Contain("TrustChip.Build(holder, snapshot.Score(snapshot.playerId, face.Actor.id)"),
                "A pact member's reading is the chip.");

            Assert.That(Read("Episode/EpisodeDirector.Journal.cs"), Does.Contain("TrustFigure(double score) => TrustChip.Figure(score)"),
                "The notebook's figure is the chip's, wherever it is printed.");
        }

        private static string Region(string code, string start)
        {
            int at = code.IndexOf(start, StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), start);
            int next = code.IndexOf("        public ", at + start.Length, StringComparison.Ordinal);
            return next < 0 ? code.Substring(at) : code.Substring(at, next - at);
        }

        private static string Read(string file) => File.ReadAllText(Path.Combine(RuntimeRoot(), file));

        private static string RuntimeRoot()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Runtime"));
#else
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "Gamesim", "Runtime");
                if (Directory.Exists(candidate)) return Path.GetFullPath(candidate);
            }
            Assert.Fail("Assets/Gamesim/Runtime was not found above " + AppContext.BaseDirectory);
            return null;
#endif
        }

#if UNITY_5_3_OR_NEWER
        /// <summary>The chip as drawn: its figure, its shape (one level bar at zero, two strokes otherwise), its words only where asked, its colour as a second cue, and nothing that takes the pointer.</summary>
        [TestCase(14.0, false)]
        [TestCase(-22.0, true)]
        [TestCase(0.0, true)]
        public void TheChip_IsDrawnFromItsWords(double score, bool withLabel)
        {
            var root = new UnityEngine.GameObject("Chip test", typeof(UnityEngine.RectTransform));
            try
            {
                var chip = TrustChip.Build(root.transform, score, 22f, 1f, withLabel);
                Assert.That(chip.name, Is.EqualTo(TrustChip.ChipName));
                // The EditMode assembly has no TextMesh Pro reference: the labels are read as Graphics,
                // their words and size through the text component's own properties.
                var texts = chip.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Where(g => g.GetType().GetProperty("text") != null).ToArray();
                string Words(UnityEngine.UI.Graphic g) => (string)g.GetType().GetProperty("text").GetValue(g, null);
                float Size(UnityEngine.UI.Graphic g) => (float)g.GetType().GetProperty("fontSize").GetValue(g, null);
                var figure = texts.Single(t => t.name == TrustChip.FigureName);
                Assert.That(Words(figure), Is.EqualTo(TrustChip.Figure(score)));
                Assert.That(figure.color, Is.EqualTo(TrustChip.Tint(TrustChip.BandOf(score))));
                Assert.That(figure.rectTransform.sizeDelta.y, Is.GreaterThanOrEqualTo(Size(figure) * 1.3f), "The figure's box holds its line.");
                var label = texts.SingleOrDefault(t => t.name == TrustChip.LabelName);
                Assert.That(label != null, Is.EqualTo(withLabel), "The words only where asked.");
                if (label != null)
                {
                    Assert.That(Words(label), Is.EqualTo(TrustChip.Label));
                    Assert.That(Size(label), Is.GreaterThanOrEqualTo(TrustChip.MinLabelSize), "No label under the HUD's floor.");
                    Assert.That(label.rectTransform.sizeDelta.y, Is.GreaterThanOrEqualTo(Size(label) * 1.3f));
                }
                var shape = chip.Find(TrustChip.ShapeName);
                Assert.That(shape, Is.Not.Null, "A shape stands beside the figure.");
                Assert.That(shape.childCount, Is.EqualTo(TrustChip.BandOf(score) == TrustChip.Band.Even ? 1 : 2), "A level bar at zero, a mark otherwise.");
                Assert.That(chip.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Any(g => g.raycastTarget), Is.False,
                    "Nothing on the chip takes the pointer: a row it stands in stays one press.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void TheColours_AreTheTablesOld_AndTheNotebooksFigureIsTheChips()
        {
            Assert.That(TrustChip.Tint(TrustChip.Band.Above), Is.EqualTo(UiTheme.Allied));
            Assert.That(TrustChip.Tint(TrustChip.Band.Below), Is.EqualTo(UiTheme.Conflict));
            Assert.That(TrustChip.Tint(TrustChip.Band.Even), Is.EqualTo(UiTheme.Muted));
            foreach (double score in new[] { -61.2, -0.3, 0.0, 7.5, 44.0 })
                Assert.That(Gamesim.Episode.EpisodeDirector.TrustFigure(score), Is.EqualTo(TrustChip.Figure(score)));
        }
#endif
    }
}
