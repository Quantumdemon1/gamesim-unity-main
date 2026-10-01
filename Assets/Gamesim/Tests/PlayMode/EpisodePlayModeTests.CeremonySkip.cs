using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// A skip you can see (PACK8-PASS-PLAN A1). A press already moved a staged ceremony on, but
    /// nothing on screen said so, and while the house gathered the press reached the hidden HUD as
    /// well. The chip names the press while a stage runs; one press is one step - the summons starts
    /// the card at its block or its result, a press on the card closes it, a press on the walk out
    /// ends it - and none of them changes what the commit decided.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private RectTransform SkipChip() => SceneRoot(CeremonySkipChip.RootName).GetComponentsInChildren<RectTransform>(true)
            .Single(rect => rect.name == CeremonySkipChip.ChipName);

        private TMP_Text SkipChipLabel(string text) => SkipChip().GetComponentsInChildren<TMP_Text>(true).Single(label => label.text == text);

        /// <summary>
        /// A click on the skip chip, where a player would click it. It is a press on the devices like
        /// any other, which is how the summons, the cards and the walk out read it.
        /// </summary>
        private IEnumerator ClickTheSkipChip()
        {
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            SkipChip().GetWorldCorners(corners);
            // An overlay canvas's world corners are its screen pixels.
            Vector2 centre = (corners[0] + corners[2]) * 0.5f;
            if (testMouse == null) testMouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = centre }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = centre });
            yield return null;
        }

        [UnityTest]
        public IEnumerator CeremonyStage_TheSkipChipTakesOneStepAPress()
        {
            yield return InstallStagedSeason(51, AtNomination);
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The nomination is staged in the house.");
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Summons), "The house is summoned.");
            Assert.That(director.CeremonySkipShowing, Is.True, "The chip says a press moves the ceremony on, from the summons on.");
            Assert.That(SkipChipLabel(CeremonySkipChip.Caption), Is.Not.Null);
            var committed = director.Snapshot;
            var keys = SceneComponents<KeyCeremony>().Single();

            // Past the guard that keeps the press that committed the nominations from skipping the summons.
            yield return RealSeconds(0.5f);
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Summons), "The house is still gathering.");
            yield return ClickTheSkipChip();
            Assert.That(keys.IsPlaying, Is.True, "One press starts the card,");
            Assert.That(keys.Surface, Is.SameAs(director.CeremonyStageScreen), "on the set's screen,");
            Assert.That(keys.ShowingBlock, Is.True, "at its block: every key out, the order given up.");
            yield return Frames(2);
            Assert.That(keys.IsPlaying, Is.True, "The same press does not close it as well.");
            Assert.That(director.CeremonySkipShowing, Is.True, "The chip stays while the card plays.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(committed.revision), "A skip commits nothing,");
            Assert.That(director.Snapshot.nominees, Is.EqualTo(committed.nominees), "and the block is the committed one.");

            // Once the card has been up long enough to be read, the next press closes it.
            yield return RealSeconds(1f);
            yield return ClickTheSkipChip();
            Assert.That(keys.IsPlaying, Is.False, "The next press closes the card,");
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the stage lets the house go,");
            yield return null;
            Assert.That(director.CeremonySkipShowing, Is.False, "and the chip goes with it.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(committed.revision));
        }

        [UnityTest]
        public IEnumerator CeremonyStage_TheSkipStepsThroughAnEvictionAndItsWalkOut()
        {
            yield return InstallStagedSeason(52, AtEviction);
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The eviction is staged in the living room.");
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Summons), "The house is summoned.");
            string evicted = director.DepartingId;
            var committed = director.Snapshot;
            Assert.That(evicted, Is.Not.Null, "A houseguest is evicted.");
            var body = SceneComponents<HouseNpc>().Single(npc => npc.Id == evicted);
            var vote = SceneComponents<VoteReveal>().Single();
            Assert.That(director.CeremonySkipShowing, Is.True, "The chip is up for the summons,");
            Assert.That(SkipChipLabel(CeremonySkipChip.KeyboardKey), Is.Not.Null, "naming the key that skips.");

            yield return RealSeconds(0.5f);
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Summons), "The house is still gathering.");
            yield return PressKey(Key.Enter);
            Assert.That(vote.IsPlaying && vote.ShowingResult, Is.True, "One press starts the vote at its result,");
            Assert.That(vote.EvictedId, Is.EqualTo(evicted), "the committed one.");
            Assert.That(director.WalkingOutId, Is.Null, "Nobody walks out while it is up.");

            yield return RealSeconds(1f);
            yield return PressKey(Key.Enter);
            Assert.That(vote.IsPlaying, Is.False, "The next press closes it,");
            yield return Frames(2);
            // PACK8-PASS-PLAN C2: the evicted stand to say goodbye before they walk out.
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Goodbye), "the evicted stand to say goodbye,");
            Assert.That(director.DepartingId, Is.EqualTo(evicted));
            Assert.That(director.WalkingOutId, Is.Null, "before anybody walks,");
            Assert.That(director.CeremonySkipShowing, Is.True, "and the chip stays for the goodbye.");

            // Past the guard that keeps the press that closed the card from ending the goodbye too.
            yield return RealSeconds(0.5f);
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Goodbye),
                "The press that closed the card did not end the goodbye as well.");
            yield return ClickTheSkipChip();
            Assert.That(director.DepartingId, Is.Null, "A click on the chip goes straight to the door shut behind them:");
            Assert.That(director.WalkingOutId, Is.Null, "nobody walks out,");
            Assert.That(body.gameObject.activeInHierarchy, Is.False, "and the evicted go.");
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "The house gets up,");
            yield return null;
            Assert.That(director.CeremonySkipShowing, Is.False, "and the chip goes.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(committed.revision), "Skipping commits nothing:");
            Assert.That(director.Snapshot.Find(evicted).status, Is.EqualTo(committed.Find(evicted).status), "the outcome is the committed one.");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
        }

        /// <summary>
        /// The press that starts the card during the summons is the card's (PACK8-PASS-PLAN §1.2).
        /// The nominations are committed from the episode screen, as in play, so its panel is still
        /// open under the held chrome with a control focused: Escape used to close it and Enter to
        /// press whatever had the keyboard - the next beat's Continue, say - under the summons.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_TheSummonsHoldsTheHudsInput()
        {
            yield return InstallStagedSeason(51, AtNomination);
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null;
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The nomination is staged in the house.");
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Summons), "The house is summoned.");
            Assert.That(director.IsPanelOpen, Is.True, "The episode screen is still open under the summons.");
            var committed = director.Snapshot;
            yield return Frames(2);
            Assert.That(director.IsSubmitHeldForCeremony, Is.True, "Submit waits through the summons, as it does for a card.");

            yield return RealSeconds(0.5f);
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Summons), "The house is still gathering.");
            yield return PressKey(Key.Escape);
            Assert.That(SceneComponents<KeyCeremony>().Single().ShowingBlock, Is.True, "Escape moves the ceremony on,");
            Assert.That(director.IsPanelOpen, Is.True, "and does not also close the panel under it;");
            Assert.That(director.Snapshot.revision, Is.EqualTo(committed.revision), "nothing under the summons was pressed.");
            yield return SkipReveals();
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the stage ends with the card");
            Assert.That(director.IsSubmitHeldForCeremony, Is.False, "Submit is back with the house.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The chip at both text sizes: every label drawn in a box at least 1.3 of its font (Inter
        /// draws nothing in less), clear of the rail down the left, on the canvas, in its lower half
        /// away from the ceremony strip, and above the cards.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_TheSkipChipIsLaidOutAtBothTextSizes()
        {
            yield return InstallStagedSeason(51, AtNomination);
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The nomination is staged in the house.");
            float captionAtOne = 0f;
            foreach (bool large in new[] { false, true })
            {
                director.SetLargeText(large);
                yield return Frames(2);
                Assert.That(director.CeremonySkipShowing, Is.True, "The chip is up while the stage runs.");
                Canvas.ForceUpdateCanvases();
                string where = "The skip chip at " + (large ? "the larger" : "the resting") + " text size";
                var chip = SkipChip();
                foreach (var label in chip.GetComponentsInChildren<TMP_Text>())
                    Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.fontSize * 1.3f - 0.01f),
                        where + ": '" + label.text + "' has a box " + label.rectTransform.rect.height.ToString("0.0")
                        + " high for a " + label.fontSize.ToString("0.#") + " font.");
                AssertEveryLabelDraws(chip, where);
                var caption = SkipChipLabel(CeremonySkipChip.Caption);
                if (!large) captionAtOne = caption.fontSize;
                else Assert.That(caption.fontSize, Is.GreaterThan(captionAtOne), where + ": the words grow with the preference.");

                var canvas = chip.GetComponentInParent<Canvas>().rootCanvas;
                var space = (RectTransform)canvas.transform;
                var corners = new Vector3[4];
                chip.GetWorldCorners(corners);
                Vector2 low = space.InverseTransformPoint(corners[0]), high = space.InverseTransformPoint(corners[2]);
                var bounds = space.rect;
                Assert.That(low.x - bounds.xMin, Is.GreaterThan(EpisodeHud.LeftColumnX), where + ": clear of the rail down the left.");
                Assert.That(high.x, Is.LessThanOrEqualTo(bounds.xMax + 0.5f), where + ": on the canvas at the right,");
                Assert.That(low.y, Is.GreaterThanOrEqualTo(bounds.yMin - 0.5f), "and at the bottom,");
                Assert.That(high.y - bounds.yMin, Is.LessThan(bounds.height * 0.5f), "in its lower half, away from the ceremony strip across the top.");
                Assert.That(canvas.sortingOrder, Is.GreaterThan(SceneComponents<KeyCeremony>().Single().GetComponent<Canvas>().sortingOrder),
                    where + ": above the cards.");
            }
            director.SetLargeText(false);
            yield return SkipReveals();
        }

        /// <summary>
        /// Commits eviction night until it is staged, skips the summons and the vote as a player
        /// who has seen enough does, sits out the goodbye, and leaves the evicted walking out under
        /// the stage, the chrome aside and the chip up.
        /// </summary>
        private IEnumerator SkipToTheStagedWalkOut()
        {
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The eviction is staged in the living room.");
            Assert.That(director.CeremonyStageKind, Is.EqualTo(CeremonySting.EvictionKind));
            string evicted = director.DepartingId;
            Assert.That(evicted, Is.Not.Null, "A houseguest is evicted.");
            director.SkipCeremonySummons();
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing, 3f, "the vote plays");
            yield return SkipReveals();
            yield return WaitFor(() => director.WalkingOutId != null, EpisodeDirector.GoodbyeSeconds + 2f, "the goodbye gives way to the walk out");
            Assert.That(director.WalkingOutId, Is.EqualTo(evicted), "The evicted walk out,");
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Release), "the house keeps its seats,");
            Assert.That(director.CeremonySkipShowing, Is.True, "and the chip names the press that ends the walk.");
            // PACK8-PASS-PLAN C2 (MOCKUP-PASS-PLAN M19): the exit is full-bleed to the shut door.
            Assert.That(Hud.IsHeldForReveal, Is.True, "The chrome stays aside for the walk.");
        }

        /// <summary>
        /// The press the chip names during a staged walk out ends the walk and nothing else. The
        /// eviction is committed from the episode screen, as in play, and the screen is still open
        /// under the walk - under the held chrome now (PACK8-PASS-PLAN C2) - with its way on
        /// focused: the stage held the house's input only while it narrated, so Enter ended the
        /// walk and pressed the way on as well, committing the next beat under the goodbye, and
        /// Escape closed the panel.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_TheWalkOutsPressEndsTheWalkAndNothingElse()
        {
            yield return InstallStagedSeason(52, AtEviction);
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null;
            yield return SkipToTheStagedWalkOut();
            string evicted = director.WalkingOutId;
            Assert.That(director.IsPanelOpen, Is.True, "The episode screen is still open under the walk.");
            Assert.That(CeremonyOverlays.OnScreen, Is.True, "The house's input gate says the press is the walk out's,");
            Assert.That(director.IsSubmitHeldForCeremony, Is.True, "and the UI's Submit waits, as it does for a card.");
            var committed = director.Snapshot;

            // Past the guard that keeps the press that closed the card from ending the walk too,
            // with the keyboard on the episode screen's way on, where the panel keeps it.
            yield return RealSeconds(0.5f);
            Assert.That(director.WalkingOutId, Is.EqualTo(evicted), "They are still walking.");
            string wayOn = KeyboardCaptions(committed).First();
            var control = ControlCarrying(wayOn);
            Assert.That(control, Is.Not.Null, "The episode screen offers '" + wayOn + "' under the walk.");
            EventSystem.current.SetSelectedGameObject(control.gameObject);
            yield return PressKey(Key.Enter);
            Assert.That(director.WalkingOutId, Is.Null, "Enter ends the walk out,");
            Assert.That(director.Snapshot.revision, Is.EqualTo(committed.revision), "and presses nothing under it: '" + wayOn + "' was not pressed,");
            Assert.That(director.IsPanelOpen, Is.True, "and the episode screen is still open.");
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "The house gets up");
            yield return Frames(2);
            Assert.That(director.CeremonySkipShowing, Is.False, "The chip goes with the stage,");
            Assert.That(director.IsSubmitHeldForCeremony, Is.False, "and Submit is back with the house.");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The chip through a staged walk out, at both text sizes. It used to stand clear of the
        /// chrome, which came back for the walk: its corner is the cast strip's right-hand end, and
        /// it stood on the faces there. The exit is full-bleed now (PACK8-PASS-PLAN C2, MOCKUP-PASS-
        /// PLAN M19): the chrome stays aside to the shut door, the HUD reports nothing on screen,
        /// and the chip keeps its own corner, still in the frame's lower half.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_TheSkipChipStandsClearOfTheChromeThroughTheWalkOut()
        {
            yield return InstallStagedSeason(52, AtEviction);
            yield return SkipToTheStagedWalkOut();
            string evicted = director.WalkingOutId;
            var chrome = new List<Rect>();
            foreach (bool large in new[] { false, true })
            {
                director.SetLargeText(large);
                yield return Frames(3);
                string where = "The skip chip through the walk out at " + (large ? "the larger" : "the resting") + " text size";
                Assert.That(director.WalkingOutId, Is.EqualTo(evicted), where + ": they are still walking,");
                Assert.That(director.CeremonySkipShowing, Is.True, where + ": the chip is up,");
                Assert.That(Hud.IsHeldForReveal, Is.True, where + ": the chrome is aside for the walk.");
                Canvas.ForceUpdateCanvases();
                var chip = SkipChip();
                Hud.ChromeOnScreen(chrome);
                Assert.That(chrome, Is.Empty, where + ": the HUD reports no chrome on screen while it is aside.");
                foreach (var label in chip.GetComponentsInChildren<TMP_Text>())
                    Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.fontSize * 1.3f - 0.01f),
                        where + ": '" + label.text + "' has a box " + label.rectTransform.rect.height.ToString("0.0") + " high.");
                AssertEveryLabelDraws(chip, where);

                var space = (RectTransform)chip.GetComponentInParent<Canvas>().rootCanvas.transform;
                var corners = new Vector3[4];
                chip.GetWorldCorners(corners);
                Vector2 low = space.InverseTransformPoint(corners[0]), high = space.InverseTransformPoint(corners[2]);
                var bounds = space.rect;
                Assert.That(high.x, Is.LessThanOrEqualTo(bounds.xMax + 0.5f), where + ": on the canvas at the right,");
                Assert.That(low.y, Is.GreaterThanOrEqualTo(bounds.yMin - 0.5f), where + ": and at the bottom,");
                Assert.That(high.y - bounds.yMin, Is.LessThan(bounds.height * 0.5f), where + ": in its lower half,");
                Assert.That(low.y - bounds.yMin, Is.LessThan(40f), where + ": in its own corner, with no chrome to stand above.");
            }
            director.SetLargeText(false);
            yield return null;
            director.SkipWalkOut();
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "The house gets up");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
        }

        /// <summary>Reduced motion stages nothing, so no chip goes up: the keys play on the HUD frame, whose own lines say what moves them on.</summary>
        [UnityTest]
        public IEnumerator CeremonyStage_ReducedMotionStagesNothingAndShowsNoSkip()
        {
            HoldTheHouseForTheFixture();
            yield return InstallStrategySeason(51, AtNomination);
            director.BuildNpcWorldForDiagnostics();
            AskForTheStages(reduced: true);
            yield return null;
            yield return PlayUntilTheCeremony();
            var keys = SceneComponents<KeyCeremony>().Single();
            Assert.That(director.IsCeremonyStaged, Is.False, "Reduced motion stages nothing,");
            Assert.That(keys.IsPlaying && keys.Surface == null, Is.True, "the keys play on the HUD frame,");
            yield return null;
            Assert.That(director.CeremonySkipShowing, Is.False, "and no skip chip goes up.");
            yield return SkipReveals();
        }
    }
}
