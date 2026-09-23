using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private string[] PanelWords(RectTransform panel) =>
            panel.GetComponentsInChildren<TMP_Text>().Where(t => t.gameObject.activeInHierarchy).Select(t => t.text).ToArray();

        private void AssertInside(Rect outer, RectTransform inner, string what)
        {
            var rect = ScreenRect(inner);
            Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(outer.xMin - .5f), what + " runs out of the panel's left edge.");
            Assert.That(rect.xMax, Is.LessThanOrEqualTo(outer.xMax + .5f), what + " runs out of the panel's right edge.");
            Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(outer.yMin - .5f), what + " runs out of the panel's foot.");
            Assert.That(rect.yMax, Is.LessThanOrEqualTo(outer.yMax + .5f), what + " runs out of the panel's head.");
        }

        /// <summary>The scroll's foot stands clear of the pinned action, so no row is drawn under it.</summary>
        private void AssertScrollClearOfPinned(RectTransform panel, Button pinned)
        {
            var viewport = (RectTransform)ActiveRect("Episode content").parent;
            Assert.That(ScreenRect(viewport).yMin, Is.GreaterThanOrEqualTo(ScreenRect((RectTransform)pinned.transform).yMax - .5f),
                "The scroll runs under the pinned '" + pinned.name + "'.");
        }

        /// <summary>A screen that takes the stage covers most of the frame, and stands the right column and the strip down.</summary>
        private void AssertOnTheStage(RectTransform panel)
        {
            var stage = ScreenRect(panel);
            Assert.That(stage.width * stage.height, Is.GreaterThan(Screen.width * Screen.height * .55f),
                "The episode screen takes most of the frame: " + stage + ".");
            foreach (var name in new[] { CastRail.RootName, EpisodeHud.RecentEventsCardName, EpisodeHud.HouseVibeCardName })
                Assert.That(ActiveRect(name), Is.Null, name + " stands down while the screen is up.");
        }

        private void AssertClearOfTheChrome(RectTransform panel)
        {
            var shot = ScreenRect(panel);
            foreach (var name in new[] { "Status", CastRail.RootName, IconRail.RootName, "Rail ground", "Week chip", "Objective", "House pill",
                EpisodeHud.FollowChipName, EpisodeDirector.LiveFeedCardName, EpisodeHud.RecentEventsCardName, EpisodeHud.HouseVibeCardName,
                EpisodeHud.RelationshipsCardName })
            {
                var chrome = ActiveRect(name);
                if (chrome == null) continue;
                Assert.That(shot.Overlaps(ScreenRect(chrome)), Is.False, "The episode screen grew over '" + name + "'.");
            }
        }

        /// <summary>
        /// Free time on the episode screen: the panel grows with what it holds instead of leaving a
        /// 174-unit strip to scroll, stays clear of the chrome around it, and pins the way on under
        /// the scroll - "Begin the next competition" used to sit under the meter, the house-wide
        /// actions and Listen in. The rules the panel states (the budget, the odds of listening in)
        /// are all still said.
        /// </summary>
        [UnityTest]
        public IEnumerator PhasePanel_FreeTimePinsTheWayOnAndKeepsItsRules()
        {
            yield return SettleCast();
            // Walking to the station follows the player, which puts the follow chip up top-centre -
            // where a panel grown to the top bar would stand on it.
            director.FollowHouseguest(director.Snapshot.playerId);
            yield return null;
            Assert.That(ActiveRect(EpisodeHud.FollowChipName), Is.Not.Null, "The fixture follows somebody.");
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            var hud = director.GetComponentInChildren<EpisodeHud>();
            Assume.That(hud.CurrentActivityLayout, Is.Not.EqualTo(EpisodeHud.ActivityLayout.HouseEvent),
                "The fixture's free time has no house event pending.");
            Assert.That(hud.CurrentActivityLayout, Is.EqualTo(EpisodeHud.ActivityLayout.Stage), "Free time takes the stage.");

            var panel = ActiveRect("Episode panel");
            var begin = ButtonWithCaption("Begin the next competition");
            Assert.That(begin.transform.parent, Is.SameAs(panel), "The way on is pinned to the panel, not in its scroll.");
            AssertInside(ScreenRect(panel), (RectTransform)begin.transform, "'Begin the next competition'");
            Assert.That(panel.rect.height, Is.GreaterThan(300f), "Free time holds more than the docked strip showed.");
            AssertOnTheStage(panel);
            AssertClearOfTheChrome(panel);
            AssertScrollClearOfPinned(panel, begin);
            // The rail stays up beside the stage, its groups under their names.
            var railLabels = director.GetComponentsInChildren<TMP_Text>().Where(text => text.name == "Rail label").Select(text => text.text).ToArray();
            Assert.That(railLabels, Does.Contain("PLAY").And.Contain("NOTEBOOK & SETTINGS"));
            // The column's last control scrolls into view above the pinned row, not under it.
            ButtonWithCaption("Listen in on a conversation");

            string words = string.Join("\n", PanelWords(panel));
            Assert.That(words, Does.Contain("seven times in ten"), "Listening in still says its odds.");
            Assert.That(words, Does.Contain("half its number in actions"), "and the week still says its budget.");

            // The hint says "Scroll for more" exactly when there is more.
            var content = ActiveRect("Episode content");
            var viewport = (RectTransform)content.parent;
            string hint = panel.Find(EpisodeHud.PanelHintName).GetComponent<TMP_Text>().text;
            Assert.That(hint, Is.EqualTo(content.rect.height > viewport.rect.height + .5f
                ? EpisodeHud.PanelHintScrollCopy : EpisodeHud.PanelHintCopy));
            if (Application.isBatchMode) yield return CaptureFraming("phase-free-time");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// A resolved competition, then a beat with nothing to decide. The results keep "Review
        /// competition results" as the control they open on, rank the standings as the engine does
        /// with the winner marked, and pin "Continue to the next ceremony" - it sat some 320 units
        /// down, under the standings. A panel whose only way on is pinned opens on it, so Enter
        /// moves the week on rather than landing on Close; its house status is one line.
        /// </summary>
        [UnityTest]
        public IEnumerator PhasePanel_ResultsPinContinueAndAQuietBeatOpensOnIt()
        {
            yield return SettleCast();
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Accessible alternative: steady 1-point bonus").onClick.Invoke();
            yield return null;
            // The result is the screen until Continue: its card fills most of the frame's height.
            var resultCard = SceneComponents<CompetitionResult>().Single(card => card.IsPlaying)
                .GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Card");
            Canvas.ForceUpdateCanvases();
            Assert.That(ScreenRect(resultCard).height, Is.GreaterThan(Screen.height * .85f), "The result fills the frame: " + ScreenRect(resultCard) + ".");
            Assert.That(ScreenRect(resultCard).yMin, Is.GreaterThanOrEqualTo(0f), "and fits it.");
            Assert.That(ScreenRect(resultCard).yMax, Is.LessThanOrEqualTo(Screen.height));
            yield return ContinueCompetitionResults(byKeyboard: false);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();

            var state = director.Snapshot;
            Assert.That(state.competitionResolved, Is.True);
            var panel = ActiveRect("Episode panel");
            var next = ButtonWithCaption("Continue to the next ceremony");
            Assert.That(next.transform.parent, Is.SameAs(panel), "The way on is pinned.");
            AssertOnTheStage(panel);
            AssertInside(ScreenRect(panel), (RectTransform)next.transform, "'Continue to the next ceremony'");
            AssertScrollClearOfPinned(panel, next);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Review competition results"),
                "The review stays the control the results open on.");
            var words = PanelWords(panel);
            Assert.That(words, Does.Contain("FINAL STANDINGS"));
            var ranked = state.competitionScores.OrderByDescending(score => score.score).ToList();
            for (int rank = 0; rank < ranked.Count; rank++)
            {
                var who = state.Find(ranked[rank].contestantId);
                string line = words.SingleOrDefault(w => w.StartsWith((rank + 1) + ".  " + HudPrimitives.WithYou(who.name, who.isPlayer) + "   "));
                Assert.That(line, Is.Not.Null, who.name + " is ranked " + (rank + 1) + ".");
                Assert.That(line.EndsWith("winner"), Is.EqualTo(rank == 0), "Only the engine's winner is marked.");
            }
            AssertClearOfTheChrome(panel);
            if (Application.isBatchMode) yield return CaptureFraming("phase-results");

            ButtonWithCaption("Continue to the next ceremony").onClick.Invoke();
            yield return null; yield return null;
            var quiet = director.Snapshot;
            Assert.That(quiet.phase, Is.EqualTo(EpisodePhase.Nomination));
            Assume.That(quiet.hohId, Is.Not.EqualTo(quiet.playerId), "The player's steady point does not win the HoH.");
            if (!director.IsPanelOpen)
            {
                WarpPlayer(director.StationPosition);
                Assert.That(director.TryOpenPhasePanel(), Is.True);
            }
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            panel = ActiveRect("Episode panel");
            var carryOn = ButtonWithCaption("Continue episode");
            Assert.That(carryOn.transform.parent, Is.SameAs(panel), "The way on is pinned.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(carryOn.gameObject),
                "A beat with nothing to decide opens on the way on, not on Close.");
            var quietWords = PanelWords(panel);
            Assert.That(quietWords, Does.Contain(EpisodeDirector.HouseStatus(quiet)), "The house's status is one line.");
            Assert.That(quietWords.Where(w => w.StartsWith("Nominees: ") || w.StartsWith("Veto holder: ")), Is.Empty,
                "and no fact of it stands on a line of its own.");
            Assert.That(panel.rect.height, Is.LessThan(300f), "A short beat is a short card.");
            Assert.That(panel.Find(EpisodeHud.PanelHintName).GetComponent<TMP_Text>().text, Is.EqualTo(EpisodeHud.PanelHintCopy),
                "Nothing here scrolls, so the hint does not say it does.");

            int revision = quiet.revision;
            yield return PressKey(Key.Enter);
            yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision + 1), "Enter moves the week on.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The veto meeting on the episode screen, the player holding the veto: the two nominees are
        /// peers, side by side in one row, under "Do not use the veto"; every caption fits its tile.
        /// The diary's private review keeps its column of rows.
        /// </summary>
        [UnityTest]
        public IEnumerator PhasePanel_TheVetoHoldersSavesArePeers()
        {
            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.VetoMeeting && !state.vetoResolved
                && state.vetoHolderId == state.playerId && state.hohId != state.playerId && state.nominees.Count == 2
                && !EpisodeEngine.VetoIsLockedAtFinalFour(state) && EpisodeEngine.ReplacementCandidates(state).Any(),
                "the player holding the veto at the meeting");
            var before = director.Snapshot;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();

            var saves = before.nominees.Select(id => ButtonWithCaption("Save " + before.Find(id).name + " (HoH chooses replacement)")).ToArray();
            Assert.That(saves[0].transform.parent.name, Is.EqualTo(EpisodeHud.ChoiceRowName));
            Assert.That(saves[1].transform.parent, Is.SameAs(saves[0].transform.parent), "The nominees share one row.");
            var a = ScreenRect((RectTransform)saves[0].transform);
            var b = ScreenRect((RectTransform)saves[1].transform);
            Assert.That(Mathf.Abs(a.yMin - b.yMin), Is.LessThan(1f), "side by side,");
            Assert.That(a.Overlaps(b), Is.False, "and neither covers the other.");
            Assert.That(Mathf.Abs(a.width - b.width), Is.LessThan(1f), "as equals.");
            // The stage stands the strip's badges down, so the week's roles head the decision.
            Assert.That(PanelWords(ActiveRect("Episode panel")), Does.Contain(EpisodeDirector.HouseStatus(before)),
                "Who holds what this week is said over the decision.");
            var decline = ButtonWithCaption("Do not use the veto");
            Assert.That(decline.transform.parent.name, Is.Not.EqualTo(EpisodeHud.ChoiceRowName), "Declining is its own row.");
            foreach (var label in ActiveRect("Episode panel").GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False, "'" + label.text + "' fits its box.");
            }
            if (Application.isBatchMode) yield return CaptureFraming("phase-veto");
            AssertEquivalent(before, director.Snapshot);

            // At the larger text half the column is too narrow for a name and a trust reading:
            // one choice a row, as before.
            director.ClosePanels();
            yield return ApplyTextSize(true);
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null; yield return null;
            foreach (var id in before.nominees)
                Assert.That(FindButton("Save " + before.Find(id).name + " (HoH chooses replacement)").transform.parent.name,
                    Is.EqualTo("Episode content"), "At the larger text each save is a row of its own.");
            director.ClosePanels();
            yield return ApplyTextSize(false);

            // The diary's private review keeps its column of rows. (Its column is narrower than a
            // pair needs, so this is the diary as a player sees it rather than a check of the
            // private-room guard alone.)
            yield return OpenDiaryFixturePanel();
            foreach (var id in before.nominees)
            {
                var save = FindButton("Save " + before.Find(id).name + " (HoH chooses replacement)");
                Assert.That(save.transform.parent.name, Is.EqualTo("Episode content"), "The diary keeps its column.");
            }
            AssertEquivalent(before, director.Snapshot);
            director.ClosePanels();
            yield return null;

            // The HoH choosing a replacement after an NPC's save: the candidates are peers too.
            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.VetoMeeting && !state.vetoResolved
                && state.hohId == state.playerId && state.vetoHolderId != state.playerId
                && EpisodeEngine.NpcVetoSave(state) != null && EpisodeEngine.ReplacementCandidates(state).Count() >= 2,
                "HoH replacement for an NPC veto, two candidates or more");
            var replacing = director.Snapshot;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null; yield return null;
            var candidates = EpisodeEngine.ReplacementCandidates(replacing).Select(c => FindButton(c.name)).ToArray();
            Assert.That(candidates[0].transform.parent.name, Is.EqualTo(EpisodeHud.ChoiceRowName));
            Assert.That(candidates[1].transform.parent, Is.SameAs(candidates[0].transform.parent), "The first two candidates share a row.");
            AssertEquivalent(replacing, director.Snapshot);
            director.ClosePanels();
            yield return null;
        }
    }
}
