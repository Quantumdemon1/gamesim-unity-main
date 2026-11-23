using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The hints in the device's words (U7): on a pad the panel's control hint names the D-pad and A
    /// and the follow chip the bumpers and R3, reworded in place as the device changes; and the
    /// keyboard's help card, seven lines now, clips nothing at the largest text on either device.
    ///
    /// <para>The pad press is the right stick's click with a panel open, which holds the camera
    /// still, and the key press Left Ctrl, which is nobody's (as in the Hints cases).</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator DeviceHints_ThePanelHintAndTheFollowChipRewordAsTheDeviceChanges()
        {
            yield return SettleCast();
            director.FollowHouseguest(director.Snapshot.playerId);
            yield return null;
            Assert.That(FollowChipWords(), Is.Not.Null, "The fixture follows somebody, so the chip is up.");
            Assert.That(FollowChipWords(), Does.EndWith(InputGlossary.FollowHint(false)), "On the keyboard the chip names F and the brackets.");
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null; yield return null;
            Assert.That(new[] { InputGlossary.PanelHint(false, false), InputGlossary.PanelHint(false, true) }, Does.Contain(PanelHintWords()),
                "On the keyboard the panel's hint is the one it always was.");

            yield return PressOnPad(GamepadButton.RightStick);
            yield return Frames(2);
            Assert.That(new[] { InputGlossary.PanelHint(true, false), InputGlossary.PanelHint(true, true) }, Does.Contain(PanelHintWords()),
                "On a pad the hint names the D-pad and A.");
            Assert.That(PanelHintWords(), Does.StartWith("D-pad select · A confirm"));
            Assert.That(FollowChipWords(), Does.EndWith(InputGlossary.FollowHint(true)), "and the chip the bumpers and R3, in place.");

            yield return PressKey(Key.LeftCtrl);
            yield return Frames(2);
            Assert.That(new[] { InputGlossary.PanelHint(false, false), InputGlossary.PanelHint(false, true) }, Does.Contain(PanelHintWords()),
                "A key press puts the keyboard's hint back.");
            Assert.That(FollowChipWords(), Does.EndWith(InputGlossary.FollowHint(false)), "and the chip's keys.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeviceHints_TheHelpCardGrowsWithTheTextAndClipsNothingAtTheLargest()
        {
            director.ClosePanels();
            yield return null;
            ButtonWithCaption("Help · controls").onClick.Invoke();
            yield return null;
            float standard = HelpCardBox().rect.height;
            Assert.That(HelpCardLabel(false), Is.Not.Null, "The keyboard's card is open.");
            Assert.That(HelpCardLabel(false).text.Split('\n'), Has.Length.EqualTo(7), "Seven lines on the keyboard.");
            ButtonWithCaption("Hide controls").onClick.Invoke();
            yield return null;

            yield return ApplyTextSize(TextSize.Largest);
            director.ClosePanels();
            yield return null;
            ButtonWithCaption("Help · controls").onClick.Invoke();
            yield return null;
            Assert.That(HelpCardBox().rect.height, Is.GreaterThan(standard + 1f), "The card grows with the text.");
            var keyboard = HelpCardLabel(false);
            Assert.That(keyboard, Is.Not.Null, "The keyboard's card at the largest text.");
            keyboard.ForceMeshUpdate();
            Assert.That(keyboard.isTextOverflowing, Is.False, "The keyboard's seven lines clip nothing at the largest text.");

            yield return PressOnPad(GamepadButton.RightStick);
            yield return Frames(2);
            var pad = HelpCardLabel(true);
            Assert.That(pad, Is.Not.Null, "The pad's card, reworded in place.");
            pad.ForceMeshUpdate();
            Assert.That(pad.isTextOverflowing, Is.False, "The pad's card clips nothing at the largest text.");

            yield return PressKey(Key.LeftCtrl);
            yield return Frames(2);
            ButtonWithCaption("Hide controls").onClick.Invoke();
            yield return null;
            yield return ApplyTextSize(TextSize.Standard);
        }

        /// <summary>The newest follow chip's words, shown or stood down under a panel; null with no chip.</summary>
        private string FollowChipWords()
        {
            var chip = director.GetComponentsInChildren<RectTransform>(true).LastOrDefault(rect => rect.name == EpisodeHud.FollowChipName);
            var words = chip != null ? chip.GetComponentInChildren<TMP_Text>(true) : null;
            return words != null ? words.text : null;
        }

        /// <summary>The open panel's control hint, shown or stood down by its layout.</summary>
        private string PanelHintWords()
        {
            var panel = ActiveRect("Episode panel");
            Assert.That(panel, Is.Not.Null, "A panel is open.");
            var hint = panel.Find(EpisodeHud.PanelHintName);
            Assert.That(hint, Is.Not.Null, "The panel has its hint.");
            return hint.GetComponent<TMP_Text>().text;
        }

        /// <summary>The newest controls card.</summary>
        private RectTransform HelpCardBox() =>
            director.GetComponentsInChildren<RectTransform>().LastOrDefault(rect => rect.name == "Exploration controls");

        /// <summary>The card's words for one device, or null when the card says something else.</summary>
        private TMP_Text HelpCardLabel(bool pad)
        {
            var box = HelpCardBox();
            string words = Localisation.Text(InputGlossary.HelpCard(pad));
            return box == null ? null : box.GetComponentsInChildren<TMP_Text>().FirstOrDefault(text => text.isActiveAndEnabled && text.text == words);
        }
    }
}
