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

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The notebook's alliances page in the house (ACTIONS-DEALS-ALLIANCES-PLAN V3): the player's
    /// pacts as cards and the pacts they have evidence of, at both text sizes with none, one and three
    /// pacts on it - and in every fixture one more pact between two others that the player has no
    /// evidence of, which never shows: not as a card, not by name, not as a line on the web.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The pact in every fixture that the player has no evidence of.</summary>
        private const string UnheardPactName = "The Unheard Pact";

        /// <summary>
        /// A legal history in free time after the first eviction, the player still in the house, with
        /// the pacts the page is about written into it (<see cref="WritePacts"/>), saved to this
        /// test's slot and loaded the way a player loads one.
        /// </summary>
        private IEnumerator InstallAlliancesFixture(int pacts)
        {
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 40 && fixture == null; seed++)
            {
                var engine = new EpisodeEngine(ContentCatalog.Create(seed));
                for (int guard = 0; guard < 300; guard++)
                {
                    var current = engine.Snapshot;
                    if (current.week >= 2 && current.phase == EpisodePhase.Social && current.pendingDiary == null
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
            WritePacts(fixture, pacts);
            Assert.That(EpisodeValidation.TryValidate(fixture, out var reason), Is.True, reason);
            director.SuspendNpcAutonomyForDiagnostics();
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
            AssertEquivalent(fixture, director.Snapshot);
            // A body finishing assembly orders a render of its own; let the cast land first.
            yield return SettleCast();
        }

        /// <summary>
        /// The pacts, written through the records the engine keeps: the alliances, their ledger rows
        /// and calls, their facts and the lines the player read. <paramref name="pacts"/> of them are
        /// on the page: none; the player's own with a call and a deal; or that, one the player left
        /// behind when its member went home, and one between two others leaked to the player by a
        /// play. Always one more between two others, known to them alone.
        /// </summary>
        private static void WritePacts(EpisodeState s, int pacts)
        {
            // Whatever the walk itself formed, or told the player of one, is not this test's subject.
            s.alliances.Clear();
            s.ledger.alliances.Clear();
            s.ledger.calls.Clear();
            s.story.facts.RemoveAll(f => f.kind == FactKinds.Alliance);
            s.events.RemoveAll(e => e.kind == "alliance"
                || ((e.kind == StoryLog.Receipt || e.kind == StoryLog.Whisper) && e.text != null && e.text.EndsWith(" are working together.", System.StringComparison.Ordinal)));
            var npcs = s.Active.Where(c => !c.isPlayer).ToList();
            var gone = s.contestants.First(c => !c.isPlayer && c.status != ContestantStatus.Active);
            int week = s.week;

            var unheard = PactRecord(s, "alliance-npc-", UnheardPactName, true, npcs[0].id, npcs[2].id);
            Knowledge.AllianceFormed(s, unheard);
            s.ledger.alliances.Add(new AllianceRow { id = unheard.id, why = "npc", startedWeek = 1 });

            if (pacts >= 1)
            {
                var core = PactRecord(s, "alliance-", "The " + FinalistRead.FirstName(npcs[0].name) + " Pact", true, s.playerId, npcs[0].id, npcs[1].id);
                Knowledge.AllianceFormed(s, core);
                s.ledger.alliances.Add(new AllianceRow { id = core.id, why = "player", startedWeek = 1 });
                s.ledger.calls.Add(new BlocCallRow
                {
                    week = 1, allianceId = core.id, callerId = s.playerId, targetId = gone.id,
                    followed = new List<string> { npcs[0].id }, defected = new List<string> { npcs[1].id },
                });
                s.deals.Add(new DealState
                {
                    id = "deal-alliances-page", type = DealKind.FinalTwo, proposerId = s.playerId, recipientId = npcs[0].id,
                    status = DealStatus.Active, week = 1, trustImpact = DealKind.DefaultTrust(DealKind.FinalTwo),
                });
            }
            if (pacts >= 3)
            {
                var ended = PactRecord(s, "alliance-", "The " + FinalistRead.FirstName(gone.name) + " Pact", false, s.playerId, gone.id);
                Knowledge.AllianceFormed(s, ended);
                s.ledger.alliances.Add(new AllianceRow { id = ended.id, why = "player/left-house", startedWeek = 1, endedWeek = week });
                PactLine(s, week, "alliance", gone.name + " has left the house, and your alliance has ended.", s.playerId, gone.id);

                var leaked = PactRecord(s, "alliance-npc-", "The Leaked Pact", true, npcs[2].id, npcs[3].id);
                Knowledge.AllianceFormed(s, leaked);
                var fact = Knowledge.Of(s, FactKinds.Alliance, leaked.id);
                Knowledge.AddKnower(s, fact, s.playerId);
                Knowledge.MakeKnown(s, fact, FactVisibility.Whispered);
                s.ledger.alliances.Add(new AllianceRow { id = leaked.id, why = "npc", startedWeek = 1 });
                PactLine(s, week, StoryLog.Receipt, AllianceRead.LearnedLine(s, leaked), s.playerId);
            }
            while (s.events.Count > 256) s.events.RemoveAt(0);
        }

        private static AllianceState PactRecord(EpisodeState s, string prefix, string name, bool active, params string[] members)
        {
            var alliance = new AllianceState { id = prefix + s.nextSequence++, name = name, active = active, members = members.ToList() };
            s.alliances.Add(alliance);
            return alliance;
        }

        private static void PactLine(EpisodeState s, int week, string kind, string text, params string[] audience) =>
            s.events.Add(new EpisodeEvent { sequence = s.nextSequence++, week = week, phase = s.phase, kind = kind, text = text, audienceIds = audience.ToList() });

        /// <summary>Every word under one part of the screen.</summary>
        private static string CopyUnder(RectTransform root) =>
            root == null ? string.Empty : string.Join("\n", root.GetComponentsInChildren<TMP_Text>().Select(text => text.text));

        /// <summary>How many live cards carry a name that starts so.</summary>
        private int CardsNamed(string prefix) => director.GetComponentsInChildren<RectTransform>()
            .Count(rect => rect.gameObject.activeInHierarchy && rect.name.StartsWith(prefix, System.StringComparison.Ordinal));

        [UnityTest]
        public IEnumerator Alliances_ThePageHoldsAtBothTextSizesWithNoneOneAndThreePactsAndNeverShowsAnUnheardOne()
        {
            RelationshipWeb.ClearSelection();
            foreach (int pacts in new[] { 0, 1, 3 })
            {
                yield return InstallAlliancesFixture(pacts);
                var state = director.Snapshot;
                var page = AllianceRead.Read(state);
                Assert.That(page.yours.Count + page.suspected.Count, Is.EqualTo(pacts), "The fixture puts " + pacts + " pacts on the page.");
                var unheard = state.alliances.Single(a => a.name == UnheardPactName);
                string unheardCard = EpisodeHud.SuspectedCardPrefix
                    + string.Join(" & ", state.contestants.Where(c => unheard.members.Contains(c.id)).Select(c => c.name));

                foreach (bool larger in new[] { false, true })
                {
                    yield return ApplyTextSize(larger);
                    string where = pacts + " pacts at " + (larger ? "larger" : "standard") + " text";
                    director.ShowNotebookSection(EpisodeDirector.NotebookSection.Alliances);
                    yield return null; yield return null;
                    // Taken with the page open: opening a panel may bank the house's clock as a commit
                    // of its own. Reading the page is what must commit nothing.
                    int revision = director.Snapshot.revision;
                    Canvas.ForceUpdateCanvases();

                    Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Alliances), where);
                    Assert.That(LastActive(EpisodeDirector.NotebookSection.Alliances), Is.Not.Null, "The page's mark: " + where);
                    Assert.That(CopyUnder(LastActive(EpisodeHud.NotebookHeaderName)), Does.Contain("Alliances"), "The page names itself: " + where);
                    foreach (var pact in page.yours)
                        Assert.That(LastActive(EpisodeHud.AllianceCardPrefix + pact.name), Is.Not.Null, pact.name + "'s card: " + where);
                    Assert.That(CardsNamed(EpisodeHud.AllianceCardPrefix), Is.EqualTo(page.yours.Count), "A card a pact of the player's: " + where);
                    Assert.That(CardsNamed(EpisodeHud.SuspectedCardPrefix), Is.EqualTo(page.suspected.Count), "A card a pact they know of: " + where);
                    Assert.That(LastActive(EpisodeHud.NoAlliancesName) != null, Is.EqualTo(page.yours.Count == 0), where);
                    Assert.That(LastActive(EpisodeHud.NoSuspectedName) != null, Is.EqualTo(page.suspected.Count == 0), where);

                    string words = NotebookText();
                    if (page.yours.Count == 0) Assert.That(words, Does.Contain(EpisodeDirector.NoAlliancesCopy), where);
                    if (page.suspected.Count == 0) Assert.That(words, Does.Contain(EpisodeDirector.NoSuspectedCopy), where);
                    if (pacts >= 1)
                    {
                        var core = page.yours.Single(p => p.active);
                        string card = CopyUnder(LastActive(EpisodeHud.AllianceCardPrefix + core.name));
                        string followed = FinalistRead.FirstName(state.Find(core.calls.Single().followed.Single()).name);
                        string ignored = FinalistRead.FirstName(state.Find(core.calls.Single().defected.Single()).name);
                        Assert.That(card, Does.Contain("You called it: evict " + state.Find(core.calls.Single().targetId).name + ". "
                            + followed + " followed; " + ignored + " didn't."), "The call, who followed and who did not: " + where);
                        Assert.That(card, Does.Contain("Final Two Deal with " + followed), "The deal with a member: " + where);
                        Assert.That(card, Does.Contain(core.formed), "How it began: " + where);
                    }
                    if (pacts >= 3)
                    {
                        var ended = page.yours.Single(p => !p.active);
                        Assert.That(CopyUnder(LastActive(EpisodeHud.AllianceCardPrefix + ended.name)), Does.Contain(ended.ended).And.Contain("left the house"),
                            "When and why it ended: " + where);
                        var known = page.suspected.Single();
                        string knownCard = EpisodeHud.SuspectedCardPrefix + string.Join(" & ", known.memberIds.Select(id => state.Find(id).name));
                        Assert.That(CopyUnder(LastActive(knownCard)), Does.Contain("You learned: "), "The evidence, as it was shown: " + where);
                    }

                    // The pact the player never heard of: no card of its people, and never its name.
                    Assert.That(LastActive(unheardCard), Is.Null, "No card for a pact without evidence: " + where);
                    Assert.That(words, Does.Not.Contain(UnheardPactName), where);

                    // Every label fits its box and draws its copy.
                    var panel = LastActive("Episode panel");
                    Assert.That(panel, Is.Not.Null);
                    var clipped = panel.GetComponentsInChildren<TMP_Text>()
                        .Where(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text))
                        .Where(label => { label.ForceMeshUpdate(); return label.isTextOverflowing; })
                        .Select(label => "'" + Excerpt(label.text) + "'")
                        .ToArray();
                    Assert.That(clipped, Is.Empty, "Copy cut off on the alliances page with " + where + ": " + string.Join(" | ", clipped));
                    AssertEveryLabelDraws(panel, "The alliances page with " + where);
                    if (Application.isBatchMode && pacts == 3 && !larger) yield return CaptureFraming("alliances-page");
                    Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Reading the page commits nothing: " + where);

                    if (!larger)
                    {
                        // The web marks a pact the player knows of between its members, and nothing else.
                        director.ShowNotebookSection(EpisodeDirector.NotebookSection.Network);
                        yield return null; yield return null;
                        Canvas.ForceUpdateCanvases();
                        var graph = LastActive(RelationshipWeb.GraphName);
                        Assert.That(graph, Is.Not.Null);
                        int marks = graph.GetComponentsInChildren<RectTransform>(true).Count(rect => rect.name == RelationshipWeb.SuspectedMarkerName);
                        Assert.That(marks, Is.EqualTo(pacts >= 3 ? 1 : 0), "One line for the leaked pair, none for the unheard one: " + where);
                        Assert.That(AllianceRead.SuspectedPairs(state).Any(pair => unheard.members.Contains(pair.first) && unheard.members.Contains(pair.second)),
                            Is.False, where);
                        Assert.That(graph.GetComponentsInChildren<RectTransform>(true).Count(rect => rect.name == RelationshipWeb.EdgeName),
                            Is.EqualTo(state.Active.Count() - 1), "The player's own edges are as they were: " + where);
                    }
                    director.ClosePanels();
                    yield return null;
                }
                yield return ApplyTextSize(false);
            }
        }

        [UnityTest]
        public IEnumerator Alliances_TheWebsDoorOpensThePageAndTurningToItCommitsNothing()
        {
            RelationshipWeb.ClearSelection();
            yield return InstallAlliancesFixture(3);
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Network);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            if (Application.isBatchMode) yield return CaptureFraming("alliances-web");
            int revision = director.Snapshot.revision;

            ButtonWithCaption(RelationshipWeb.AlliancesDoorCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.IsPanelOpen, Is.True, "The notebook stays open.");
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Alliances), "The door opens the alliances page.");
            Assert.That(LastActive(EpisodeDirector.NotebookSection.Alliances), Is.Not.Null);
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Turning a page commits nothing.");

            // A houseguest's own column has no door: the page is the player's.
            var someone = director.Snapshot.Active.First(c => !c.isPlayer);
            RelationshipWeb.Select(director.Snapshot, someone.id);
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Network);
            yield return null; yield return null;
            Assert.That(director.GetComponentsInChildren<UnityEngine.UI.Button>().Any(button => button.IsActive()
                && button.GetComponentsInChildren<TMP_Text>(true).Any(text => text.text == RelationshipWeb.AlliancesDoorCaption)), Is.False);
            RelationshipWeb.ClearSelection();
            director.ClosePanels();
            yield return null;
        }
    }
}
