using System.Collections;
using System.Linq;
using System.Reflection;
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
    /// Leaks and double-dealing on the alliances page (WAVE-D-NPC-PACTS-PLAN D4-5): an ally who found out
    /// about the player's other pact shows on that pact's card as "Week N · Riley found out about it.", after
    /// its calls and deals, and a listen-in that heard a pair for what it is dates their card by its line -
    /// at both text sizes (FontScale 1.0 and 1.2), every label at least 1.3 times its words and drawn whole.
    /// The double-dealing line's kind is drawn in the conflict red. Photographed in a batch run on a 16:9
    /// frame and a 4:3 one as 'alliances-double-dealt' and 'alliances-overheard', '-large' at the larger
    /// text, '-4x3' on the 4:3 frame. The engine's half is AllianceLeakTests.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// A legal history in free time after the first eviction, the player in the house, with the leak
        /// rules on from week one and two of the player's pacts: one with the ally, and one the ally has
        /// found out about, by the engine's own line; and a pair the player heard for what they are, by the
        /// engine's own listen-in line. Saved to this test's slot and loaded as a player loads one.
        /// </summary>
        private IEnumerator InstallDoubleDealingFixture()
        {
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 40 && fixture == null; seed++)
            {
                var engine = new EpisodeEngine(ContentCatalog.Create(seed));
                for (int guard = 0; guard < 300; guard++)
                {
                    var current = engine.Snapshot;
                    if (current.phase == EpisodePhase.Social && current.pendingDiary == null
                        && current.Find(current.playerId).status == ContestantStatus.Active
                        && current.Active.Count(c => !c.isPlayer) >= 4
                        && current.contestants.Any(c => !c.isPlayer && c.status != ContestantStatus.Active))
                    {
                        fixture = current;
                        break;
                    }
                    if (current.phase == EpisodePhase.Finished) break;
                    var result = engine.Apply(NextCommand(current));
                    Assert.That(result.accepted, Is.True, result.reason);
                }
            }
            Assert.That(fixture, Is.Not.Null, "No bounded legal free time after an eviction with the player in the house.");
            WriteDoubleDealing(fixture);
            Assert.That(EpisodeValidation.TryValidate(fixture, out var reason), Is.True, reason);
            director.SuspendNpcAutonomyForDiagnostics();
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
            AssertEquivalent(fixture, director.Snapshot);
            yield return SettleCast();
        }

        /// <summary>The pacts and lines for <see cref="Alliances_DoubleDealingAndListenIn"/>, through the engine's own builders.</summary>
        private static void WriteDoubleDealing(EpisodeState s)
        {
            s.alliances.Clear();
            s.ledger.alliances.Clear();
            s.ledger.calls.Clear();
            s.story.facts.RemoveAll(f => f.kind == FactKinds.Alliance);
            s.events.RemoveAll(e => e.kind == "alliance" || e.kind == "eavesdrop" || e.kind == WaveDEventKinds.DoubleDealing
                || ((e.kind == StoryLog.Receipt || e.kind == StoryLog.Whisper) && e.text != null && e.text.EndsWith(" are working together.", System.StringComparison.Ordinal)));
            EpisodeEngine.EnableAllianceLeaks(s, 1);
            var npcs = s.Active.Where(c => !c.isPlayer).ToList();
            int week = s.week;

            var first = PactRecord(s, "alliance-", "The " + FinalistRead.FirstName(npcs[0].name) + " Pact", true, s.playerId, npcs[0].id);
            Knowledge.AllianceFormed(s, first);
            s.ledger.alliances.Add(new AllianceRow { id = first.id, why = "player", startedWeek = 1 });

            // The pact the ally found out about: out as a whisper, the ally among its knowers, and the line.
            var juggled = PactRecord(s, "alliance-", "The " + FinalistRead.FirstName(npcs[1].name) + " Pact", true, s.playerId, npcs[1].id, npcs[2].id);
            Knowledge.AllianceFormed(s, juggled);
            var fact = Knowledge.Of(s, FactKinds.Alliance, juggled.id);
            Knowledge.MakeKnown(s, fact, FactVisibility.Whispered);
            Knowledge.AddKnower(s, fact, npcs[0].id);
            s.ledger.alliances.Add(new AllianceRow { id = juggled.id, why = "player", startedWeek = 1 });
            PactLine(s, week, WaveDEventKinds.DoubleDealing, AllianceLeaks.Line(s, npcs[0].id, juggled), s.playerId, npcs[0].id);

            // A pair the player overheard for what they are: the player a knower, the pact still private.
            var overheard = PactRecord(s, "alliance-npc-", "The Overheard Pact", true, npcs[2].id, npcs[3].id);
            Knowledge.AllianceFormed(s, overheard);
            Knowledge.AddKnower(s, Knowledge.Of(s, FactKinds.Alliance, overheard.id), s.playerId);
            s.ledger.alliances.Add(new AllianceRow { id = overheard.id, why = "npc", startedWeek = 1 });
            PactLine(s, week, "eavesdrop", EpisodeEngine.EavesdropLine(npcs[2].name, npcs[3].name, "sounded close", "", null,
                AllianceLeaks.ListenInSentence(s, overheard)), s.playerId);
            while (s.events.Count > 256) s.events.RemoveAt(0);
        }

        /// <summary>
        /// The leak rules (D4-4's enable line) on a season the director starts, from its first week, and
        /// across a save. The war rooms (D3-S5) and the house's turns all week (D2-S4) start in week one beside them.
        /// </summary>
        [UnityTest]
        public IEnumerator AllianceLeaks_ASeasonTheDirectorStartsPlaysThem()
        {
            foreach (var choice in new[] { null, new SeasonBuilder.Choice() })
            {
                string which = choice == null ? "The quick start" : "The cast screen's season";
                director.StartSeason(choice);
                yield return SettleCast();
                Assert.That(director.Snapshot.allianceLeakRulesStartWeek, Is.EqualTo(1), which + " plays the leak rules from week one.");
                Assert.That(director.Snapshot.pactPlanRulesStartWeek, Is.EqualTo(1), which + ": D3's war rooms from week one too,");
                Assert.That(director.Snapshot.allWeekRulesStartWeek, Is.EqualTo(1), which + ": and D2's all-week beats.");
                Assert.That(AllianceLeaks.On(director.Snapshot), Is.True, which);
            }
            director.SaveNow();
            director.LoadNow();
            yield return SettleCast();
            Assert.That(AllianceLeaks.On(director.Snapshot), Is.True, "and they survive a save.");
        }

        /// <summary>Scrolls the notebook so a card is in view, as the HUD reveals a selection.</summary>
        private void ScrollNotebookTo(string card)
        {
            var row = LastActive(card);
            var scroll = row != null ? row.GetComponentInParent<ScrollRect>() : null;
            if (scroll != null) ScrollTo(scroll, row);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Alliances_DoubleDealingAndListenIn()
        {
            RelationshipWeb.ClearSelection();
            yield return InstallDoubleDealingFixture();
            var state = director.Snapshot;
            var page = AllianceRead.Read(state);
            var juggled = page.yours.Single(p => p.exposures.Count > 0);
            var ally = state.Find(state.events.Last(e => e.kind == WaveDEventKinds.DoubleDealing).audienceIds[1]);
            string exposure = "Week " + state.week + " · " + FinalistRead.FirstName(ally.name) + " found out about it.";
            var shared = page.yours.Single(p => p.exposures.Count == 0);
            var heard = page.suspected.Single();
            string heardCard = EpisodeHud.SuspectedCardPrefix + string.Join(" & ", heard.memberIds.Select(id => state.Find(id).name));
            string heardLine = state.events.Last(e => e.kind == "eavesdrop").text;
            Assert.That(heard.certainty, Is.EqualTo(FinalistRead.Suspected), "Heard, not seen.");
            Assert.That(heard.evidence.Select(e => e.text), Is.EqualTo(new[] { heardLine }), "The listen-in's line dates the card.");

            // The kind's glyph and tint on the events' list: the conflict red.
            var tint = typeof(EpisodeHud).GetMethod("EventTint", BindingFlags.NonPublic | BindingFlags.Static);
            var glyph = typeof(EpisodeHud).GetMethod("EventGlyph", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(tint, Is.Not.Null); Assert.That(glyph, Is.Not.Null);
            Assert.That((Color)tint.Invoke(null, new object[] { WaveDEventKinds.DoubleDealing }), Is.EqualTo(UiTheme.Conflict));
            Assert.That(WaveDEventKinds.All.Select(kind => (string)glyph.Invoke(null, new object[] { kind })),
                Is.EqualTo(new[] { "eye", "ear", "people", "handshake" }), "Each of Wave D's kinds has its own glyph from the set.");

            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                string where = "the alliances page at " + (larger ? "the larger text" : "the standard text");
                director.ShowNotebookSection(EpisodeDirector.NotebookSection.Alliances);
                yield return null; yield return null;
                int revision = director.Snapshot.revision;
                Canvas.ForceUpdateCanvases();

                string juggledCard = EpisodeHud.AllianceCardName(juggled.name, juggled.id);
                Assert.That(CopyUnder(LastActive(juggledCard)), Does.Contain(exposure), "Who found out, and when: " + where);
                Assert.That(CopyUnder(LastActive(EpisodeHud.AllianceCardName(shared.name, shared.id))), Does.Not.Contain("found out about it"),
                    "The pact they share with the player says nothing of it: " + where);
                Assert.That(CopyUnder(LastActive(heardCard)), Does.Contain(heardLine), "The listen-in's line on the pair's card: " + where);
                Assert.That(NotebookText(), Does.Not.Contain("Overheard Pact"), "Never the pair's pact's name: " + where);

                // Every label fits its box, stands in one at least 1.3 times its words, and draws its copy.
                var panel = LastActive("Episode panel");
                Assert.That(panel, Is.Not.Null);
                var clipped = panel.GetComponentsInChildren<TMP_Text>()
                    .Where(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text))
                    .Where(label => { label.ForceMeshUpdate(); return label.isTextOverflowing; })
                    .Select(label => "'" + Excerpt(label.text) + "'")
                    .ToArray();
                Assert.That(clipped, Is.Empty, "Copy cut off on " + where + ": " + string.Join(" | ", clipped));
                foreach (var label in panel.GetComponentsInChildren<TMP_Text>()
                             .Where(label => label.gameObject.activeInHierarchy && (label.text.Contains(exposure) || label.text.Contains(heardLine))))
                    AssertLineHasRoom(label, where);
                AssertEveryLabelDraws(panel, where);

                if (Application.isBatchMode)
                {
                    string size = larger ? "-large" : "";
                    ScrollNotebookTo(juggledCard);
                    yield return CaptureFraming("alliances-double-dealt" + size, arrange: () => ScrollNotebookTo(juggledCard));
                    yield return CaptureFraming("alliances-double-dealt" + size + "-4x3", arrange: () => ScrollNotebookTo(juggledCard), width: 1200, height: 900);
                    ScrollNotebookTo(heardCard);
                    yield return CaptureFraming("alliances-overheard" + size, arrange: () => ScrollNotebookTo(heardCard));
                    yield return CaptureFraming("alliances-overheard" + size + "-4x3", arrange: () => ScrollNotebookTo(heardCard), width: 1200, height: 900);
                }
                Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Reading the page commits nothing: " + where);
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);
        }
    }
}
