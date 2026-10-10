using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
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
    /// Colour-blind safety on the relationship web (PLAN A, A9): each kind of line has a stroke of its
    /// own as well as a colour, so a player who cannot tell the rivalry's red from the friendship's
    /// green still tells the lines apart - one bar for a friendship, an alliance or a neutral reading,
    /// two bars side by side for a rivalry, a run of dashes for distrust - and the key draws each
    /// sample the way the web draws its line. Still one <see cref="RelationshipWeb.EdgeName"/> per
    /// houseguest: the double line's two bars live inside the one edge.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest, Timeout(300000)]
        public IEnumerator RelationshipWeb_EachKindHasItsOwnStroke()
        {
            RelationshipWeb.ClearSelection();
            var canvasObject = new GameObject("Web stroke canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var fixture = new GameObject("Web stroke viewport", typeof(RectTransform), typeof(VerticalLayoutGroup));
            fixture.transform.SetParent(canvasObject.transform, false);
            try
            {
                var viewport = (RectTransform)fixture.transform; viewport.sizeDelta = new Vector2(1160, 490);
                var layout = fixture.GetComponent<VerticalLayoutGroup>(); layout.childControlWidth = true; layout.childControlHeight = true;
                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
                // Five houseguests, one of each kind of reading.
                var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 6 }, 711);
                var others = RelationshipWeb.Others(state);
                Assert.That(others, Has.Count.EqualTo(5));
                SetReading(state, others[0].id, 0);
                state.alliances.Add(new AllianceState { id = "stroke-test-pact", name = "The Stroke Test", members = new List<string> { state.playerId, others[0].id } });
                SetReading(state, others[1].id, RelationshipWeb.FriendThreshold + 15);
                SetReading(state, others[2].id, RelationshipWeb.RivalThreshold - 20);
                SetReading(state, others[3].id, RelationshipWeb.DistrustThreshold - 10);
                SetReading(state, others[4].id, 0);
                Assert.That(others.Select(actor => RelationshipWeb.KindOf(state, actor.id)), Is.EqualTo(new[]
                {
                    RelationshipWeb.Kind.Alliance, RelationshipWeb.Kind.Friendship, RelationshipWeb.Kind.Rivalry,
                    RelationshipWeb.Kind.Distrust, RelationshipWeb.Kind.Neutral,
                }), "The fixture's readings give every kind.");
                RelationshipWeb.SetFilter(state, RelationshipWeb.Filter.All);

                foreach (float scale in new[] { 1f, 1.2f })
                {
                    string at = " at text scale " + scale;
                    var root = RelationshipWeb.Build(viewport, state, scale, TMP_Settings.defaultFontAsset, _ => null, _ => { }, 490f);
                    yield return null; LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
                    var graph = Under(root, RelationshipWeb.GraphName);
                    AssertEdgeStrokes(graph, state, at);

                    // The key: Rivalry's sample is two bars, as wide as the solid samples, inside its 19-unit row.
                    var legend = Under(root, RelationshipWeb.LegendName);
                    var conflict = UiTheme.Conflict;
                    var rivalry = legend.GetComponentsInChildren<Image>(true)
                        .Where(image => image.name == RelationshipWeb.LegendSampleName && SameTint(image.color, conflict)
                            && Mathf.Abs(image.rectTransform.sizeDelta.x - 22f * scale) < .01f)
                        .Select(image => image.rectTransform).ToList();
                    Assert.That(rivalry, Has.Count.EqualTo(2), "The key's rivalry sample is a double line" + at + ".");
                    Assert.That(rivalry[0].anchoredPosition.x, Is.EqualTo(rivalry[1].anchoredPosition.x).Within(.01f), "side by side");
                    float top = Mathf.Max(rivalry[0].anchoredPosition.y, rivalry[1].anchoredPosition.y);
                    float bottom = Mathf.Min(rivalry[0].anchoredPosition.y - rivalry[0].sizeDelta.y, rivalry[1].anchoredPosition.y - rivalry[1].sizeDelta.y);
                    Assert.That(Mathf.Abs(rivalry[0].anchoredPosition.y - rivalry[1].anchoredPosition.y), Is.GreaterThan(rivalry[0].sizeDelta.y),
                        "with a gap between the bars" + at + ".");
                    Assert.That(top - bottom, Is.LessThan(19f * scale), "and the pair inside the key's 19-unit row" + at + ".");
                    Object.Destroy(root.gameObject);
                    yield return null;
                }
            }
            finally
            {
                Object.Destroy(canvasObject);
                RelationshipWeb.ClearSelection();
            }

            // And on the notebook's own page, over the house, photographed at both text sizes and on
            // both frames: a season whose player reads one houseguest as a friend, one as a rival and
            // one as distrusted.
            var seeded = ContentCatalog.Create(11);
            var readings = RelationshipWeb.Others(seeded);
            SetReading(seeded, readings[0].id, RelationshipWeb.FriendThreshold + 15);
            SetReading(seeded, readings[1].id, RelationshipWeb.RivalThreshold - 20);
            SetReading(seeded, readings[2].id, RelationshipWeb.DistrustThreshold - 10);
            Assert.That(EpisodeValidation.TryValidate(seeded, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(seeded);
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                RelationshipWeb.ClearSelection();
                RelationshipWeb.SetFilter(director.Snapshot, RelationshipWeb.Filter.All);
                director.ShowNotebookSection(EpisodeDirector.NotebookSection.Network);
                yield return Frames(2);
                string size = larger ? "-large" : "";
                yield return OnBothFrames("web-strokes" + size, "The relationship web" + (larger ? " at larger text" : ""), where =>
                {
                    var graph = Under(Section(), RelationshipWeb.GraphName);
                    Assert.That(graph, Is.Not.Null, where + ": the web is up.");
                    AssertEdgeStrokes(graph, director.Snapshot, " (" + where + ")");
                    var state = director.Snapshot;
                    Assert.That(RelationshipWeb.Others(state).Any(actor => RelationshipWeb.KindOf(state, actor.id) == RelationshipWeb.Kind.Rivalry), Is.True,
                        where + ": a rivalry is on the page to be drawn double.");
                });
                director.ClosePanels();
                yield return Frames(2);
            }
            yield return ApplyTextSize(false);
        }

        /// <summary>The player's own reading of a houseguest, set on a state that is not playing.</summary>
        private static void SetReading(EpisodeState state, string otherId, double score)
        {
            var reading = state.relationships.FirstOrDefault(item => item.fromId == state.playerId && item.toId == otherId);
            if (reading == null)
            {
                reading = new RelationshipState { fromId = state.playerId, toId = otherId };
                state.relationships.Add(reading);
            }
            reading.score = score;
        }

        private static bool SameTint(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < .002f && Mathf.Abs(a.g - b.g) < .002f && Mathf.Abs(a.b - b.b) < .002f;

        /// <summary>
        /// One edge per houseguest the web shows, in its order, each carrying its kind's stroke: one bar
        /// on the line's middle, two bars an equal distance either side of it, or a run of dashes on it.
        /// </summary>
        private static void AssertEdgeStrokes(RectTransform graph, EpisodeState state, string at)
        {
            var edges = graph.GetComponentsInChildren<RectTransform>(true).Where(rect => rect.name == RelationshipWeb.EdgeName).ToList();
            var shown = RelationshipWeb.FilteredOthers(state);
            Assert.That(edges, Has.Count.EqualTo(shown.Count), "One edge per houseguest" + at + ".");
            var signatures = new Dictionary<RelationshipWeb.Kind, string>();
            for (int i = 0; i < shown.Count; i++)
            {
                var kind = RelationshipWeb.KindOf(state, shown[i].id);
                var bars = edges[i].Cast<Transform>().Select(child => (RectTransform)child)
                    .Where(child => child.name == RelationshipWeb.SegmentName).ToList();
                string where = kind + "'s line to " + shown[i].name + at;
                switch (RelationshipWeb.StrokeOf(kind))
                {
                    case RelationshipWeb.Stroke.Solid:
                        Assert.That(bars, Has.Count.EqualTo(1), where + " is one bar.");
                        Assert.That(bars[0].anchoredPosition.y, Is.EqualTo(0f).Within(.01f), where + " on the line's middle.");
                        Assert.That(bars[0].sizeDelta.x, Is.EqualTo(edges[i].sizeDelta.x).Within(.01f), where + " the whole way.");
                        break;
                    case RelationshipWeb.Stroke.Double:
                        Assert.That(bars, Has.Count.EqualTo(2), where + " is two bars.");
                        Assert.That(bars[0].anchoredPosition.y, Is.EqualTo(-bars[1].anchoredPosition.y).Within(.01f), where + ": either side of the middle.");
                        Assert.That(Mathf.Abs(bars[0].anchoredPosition.y) * 2f, Is.GreaterThan(bars[0].sizeDelta.y), where + ": with a gap between them.");
                        Assert.That(bars.All(bar => Mathf.Abs(bar.sizeDelta.x - edges[i].sizeDelta.x) < .01f), Is.True, where + ": both the whole way.");
                        break;
                    case RelationshipWeb.Stroke.Dashed:
                        Assert.That(bars, Has.Count.GreaterThan(2), where + " is a run of dashes.");
                        Assert.That(bars.All(bar => Mathf.Abs(bar.anchoredPosition.y) < .01f && bar.sizeDelta.x < edges[i].sizeDelta.x), Is.True,
                            where + ": dashes on the line's middle, each shorter than the line.");
                        break;
                }
                string signature = bars.Count > 2 ? "dashed" : bars.Count + " bar(s) at " + string.Join(",", bars.Select(bar => Mathf.Round(bar.anchoredPosition.y * 10f) / 10f));
                if (!signatures.ContainsKey(kind)) signatures[kind] = signature;
            }
            TestContext.WriteLine("Web strokes" + at + ": " + string.Join("; ", signatures.Select(pair => pair.Key + " = " + pair.Value)));
            if (signatures.ContainsKey(RelationshipWeb.Kind.Rivalry) && signatures.ContainsKey(RelationshipWeb.Kind.Friendship))
                Assert.That(signatures[RelationshipWeb.Kind.Rivalry], Is.Not.EqualTo(signatures[RelationshipWeb.Kind.Friendship]),
                    "A rivalry and a friendship are told apart by their strokes" + at + ".");
        }
    }
}
