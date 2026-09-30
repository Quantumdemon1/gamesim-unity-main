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
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The jury house as drawn: a card per juror, each with a photo slot and a band word, every line fitting.</summary>
        private void AssertTheJuryHouse(EpisodeState state)
        {
            Assert.That(director.InJuryHouse, Is.True);
            Assert.That(Words(LastActive(EpisodeHud.ScreenHeadName)), Does.Contain("THE JURY HOUSE"));
            Assert.That(LastActive(EpisodeHud.JuryMattersName), Is.Not.Null, "What matters to this jury.");
            var jurors = FinalistRead.Jurors(state);
            Assert.That(jurors, Is.Not.Empty);
            var bands = JuryHouseRead.Bands.Select(band => band.ToUpperInvariant()).ToArray();
            foreach (var juror in jurors)
            {
                var card = LastActive(EpisodeHud.JurorCardPrefix + juror.name);
                Assert.That(card, Is.Not.Null, juror.name + " has a card.");
                Assert.That(card.GetComponentsInChildren<CharacterPortraitBinding>(true), Has.Length.EqualTo(1), "A photo slot bound to them.");
                var band = card.GetComponentsInChildren<TMP_Text>().Single(text => text.name == EpisodeHud.JurorBandName);
                Assert.That(bands, Does.Contain(band.text), juror.name + "'s band is one of the six words.");
                Assert.That(band.text, Is.EqualTo(JuryHouseRead.ReadJuror(state, juror.id).band.ToUpperInvariant()), "The band the read gives.");
                Assert.That(card.GetComponentsInChildren<Button>(), Is.Empty, "Observe only: nothing on a card acts.");
                AssertDecisionCopyFits(card);
            }
            Assert.That(director.GetComponentsInChildren<Button>().Any(button => button.IsActive() && button.name.StartsWith("Evict ")), Is.False);

            // MOCKUP-PASS M12: the tableau, a callout per juror in their recorded words or the
            // band's reason, the highlights beside it, and the observe-only chip - none a control.
            var tableau = LastActive(EpisodeHud.JuryTableauName);
            Assert.That(tableau, Is.Not.Null, "The jury as a tableau.");
            Assert.That(tableau.GetComponentsInChildren<Button>(), Is.Empty, "Observe only: the tableau is not a control.");
            foreach (var juror in jurors)
            {
                var callout = LastActive(EpisodeHud.JurorCalloutPrefix + juror.name);
                Assert.That(callout, Is.Not.Null, juror.name + " has a callout.");
                Assert.That(callout.IsChildOf(tableau), Is.True);
                var said = JuryHouseRead.Line(state, juror.id);
                var line = callout.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Callout line").text;
                if (said.when != null) Assert.That(line, Does.StartWith(said.when + ": “"), juror.name + "'s own recorded words, dated.");
                else Assert.That(line, Is.EqualTo(said.words), juror.name + "'s reason, unquoted.");
                if (said.noteTail != null) Assert.That(line, Does.EndWith("” · " + said.noteTail), "The note under their name, without the name again.");
                // The screen's own wording, never the juror's: a recorded question or plea may say
                // "feel" ("Do you feel any remorse…"), and it is quoted, not the screen's claim.
                int open = line.IndexOf('“'), close = line.LastIndexOf('”');
                string wording = said.quoted && open >= 0 && close > open ? line.Remove(open, close - open + 1) : line;
                Assert.That(wording, Does.Not.Contain("feel"), "Never worded as how they feel about you.");
                AssertDecisionCopyFits(callout);
            }
            Assert.That(LastActive(EpisodeHud.JuryHighlightsName), Is.Not.Null, "The season's highlights beside the tableau.");
            var observe = LastActive(EpisodeHud.ObserveOnlyName);
            Assert.That(observe, Is.Not.Null, "Observe only.");
            Assert.That(observe.IsChildOf(LastActive(EpisodeHud.ScreenHeadName)), Is.True, "In the head.");
            Assert.That(observe.GetComponentInParent<Button>(), Is.Null, "A chip, not a control.");
        }

        /// <summary>
        /// ENDGAME-PLAN F4's jury house, from its two doors: a free tile in the window at three and
        /// a row after the Final 2's own controls. Each juror is a card with the band the read
        /// gives, at both text sizes; nothing is committed; leaving goes back to the panel it
        /// opened over.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_TheJuryHouseOpensFromTheWindowAndFromTheFinalTwo()
        {
            HoldTheHouseForTheFixture();
            yield return InstallDiaryFixture(
                state => state.phase == EpisodePhase.Social && state.Active.Count() == 3
                    && state.Active.Any(actor => actor.isPlayer) && state.pendingDiary == null,
                "the player in the window at three");
            yield return PutAwayTheCards();
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                yield return OpenFreeTime();
                var before = director.Snapshot;
                var tile = ButtonWithCaption(EpisodeDirector.JuryHouseCaption);
                Assert.That(tile.transform.IsChildOf(LastActive(EpisodeHud.HouseMovesName)), Is.True, "A tile among the moves at three.");
                tile.onClick.Invoke();
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                AssertTheJuryHouse(before);
                AssertEquivalent(before, director.Snapshot);
                if (!larger && Application.isBatchMode) yield return CaptureFraming("endgame-jury-house-three", settle: false);
                ButtonWithCaption(EpisodeDirector.LeaveJuryHouseCaption).onClick.Invoke();
                yield return null; yield return null;
                Assert.That(director.InJuryHouse, Is.False);
                Assert.That(Words(LastActive(EpisodeHud.ScreenHeadName)), Does.Contain(EpisodeDirector.EndgamePreparationTitle), "Back in the window.");
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);

            // The Final 2: the door is a row after the questioning's own controls. The house is held
            // again: the reload replaced the director the first hold stopped.
            HoldTheHouseForTheFixture();
            yield return InstallFinaleFixture(true);
            yield return PutAwayTheCards();
            yield return OpenFinalePanel();
            Canvas.ForceUpdateCanvases();
            var state = director.Snapshot;
            Assume.That(state.phase, Is.EqualTo(EpisodePhase.JuryQuestioning));
            var door = ButtonWithCaption(EpisodeDirector.JuryHouseCaption);
            var controls = door.transform.parent.GetComponentsInChildren<Button>().Where(button => button.transform.parent == door.transform.parent).ToList();
            Assert.That(controls.Last(), Is.SameAs(door), "The door comes after the panel's own controls, so the opening focus stays where it was.");
            door.onClick.Invoke();
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            AssertTheJuryHouse(state);
            AssertEquivalent(state, director.Snapshot);
            ButtonWithCaption(EpisodeDirector.LeaveJuryHouseCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.InJuryHouse, Is.False);
            Assert.That(director.GetComponentsInChildren<Button>().Any(button => button.IsActive()
                && (button.name == EpisodeHud.JuryContinueCaption || button.name.StartsWith("A · "))), Is.True, "Back at the questions.");
            if (Application.isBatchMode)
            {
                door = ButtonWithCaption(EpisodeDirector.JuryHouseCaption);
                door.onClick.Invoke();
                yield return null; yield return null;
                yield return CaptureFraming("endgame-jury-house", settle: false);
                director.CloseJuryHouse();
            }

            // The Final 2 under the finale rules (MOCKUP-PASS M12): what the jury values by theme,
            // and tonight's question on top of the highlights before it is answered.
            HoldTheHouseForTheFixture();
            yield return InstallFinaleFixture(true, finaleRules: true);
            yield return PutAwayTheCards();
            yield return OpenFinalePanel();
            var ruled = director.Snapshot;
            Assert.That(ruled.phase, Is.EqualTo(EpisodePhase.JuryQuestioning));
            Assert.That(EpisodeEngine.FinaleOn(ruled), Is.True);
            ButtonWithCaption(EpisodeDirector.JuryHouseCaption).onClick.Invoke();
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            AssertTheJuryHouse(ruled);
            AssertEquivalent(ruled, director.Snapshot);
            string matters = Words(LastActive(EpisodeHud.JuryMattersName));
            Assert.That(FinalArgument.Themes.Any(theme => matters.Contains(FinalArgument.Value(theme))), Is.True, "What the jury values, by theme.");
            var asked = ruled.juryExchanges[ruled.juryQuestionIndex];
            string highlights = Words(LastActive(EpisodeHud.JuryHighlightsName));
            Assert.That(highlights, Does.Contain("Tonight · " + JuryHouseRead.ShortName(ruled, asked.questionerId) + " " + JuryHouseRead.Asked(ruled, asked)),
                "Tonight's question on top of the season.");
            Assert.That(highlights, Does.Not.Contain("took your answer").And.Not.Contain("was not moved"), "Nothing says how an answer will land before it is given.");
            if (Application.isBatchMode) yield return CaptureFraming("endgame-jury-house-finale", settle: false);
            director.CloseJuryHouse();
            yield return null;
        }
    }
}
