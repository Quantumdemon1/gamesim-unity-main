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
        }
    }
}
