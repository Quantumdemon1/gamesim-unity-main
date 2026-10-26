using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    /// <summary>
    /// The cast screen's conversation preview (DIALOGUE-PLAN D1): a chip in the footer, off whenever the screen opens,
    /// that starts the next season on the pinned dialogue rules 1 - the merged pilot - and nothing else. Driven through the
    /// screen's own controls: the mouse's press, the keyboard's and the pad's ring, and the creator's start; and laid out
    /// at the three text sizes on 16:9, 4:3 and 21:9, inside the footer's two fixed heights, every label set at thirteen
    /// or more in a box 1.3 times its type and drawing its words.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private Button[] PreviewChip(bool on) => CastButtons(on ? CastSelect.ConversationPreviewOffCaption : CastSelect.ConversationPreviewOnCaption);

        private TMP_Text PreviewLine() => CastScreen().GetComponentsInChildren<TMP_Text>()
            .Last(label => label.name == CastSelect.ConversationPreviewLineName && label.isActiveAndEnabled);

        private void AssertPreviewReads(bool on, string how)
        {
            Assert.That(CastScreen().CampaignConversations, Is.EqualTo(on), how + ": the screen's state.");
            Assert.That(PreviewChip(on), Has.Length.EqualTo(1), how + ": one chip, captioned for what a press does.");
            Assert.That(PreviewChip(!on), Is.Empty, how + ": and never both captions.");
            Assert.That(PreviewLine().text, Is.EqualTo(on ? CastSelect.ConversationPreviewOnLine : CastSelect.ConversationPreviewOffLine),
                how + ": the line says the state in words.");
        }

        [UnityTest]
        public IEnumerator CastSelect_TheConversationPreviewIsOffUntilChosenAndStartsTheSeasonOnDialogueRules1()
        {
            Assert.That(DialogueRules.Offered, Is.EqualTo(1), "The chip offers the merged pilot, dialogue rules 1, frozen with D1.");
            var before = director.Snapshot;
            string slot = director.SavePath;
            new Gamesim.Persistence.EpisodeSaveStore(slot).Save(before);

            yield return OpenCastScreen();
            // The slot as it stands with the screen up and the house held, so that only the chip could move it: opening
            // a panel may commit the house's pending tick, which is the panel's doing, not the chip's.
            HoldTheHouseForTheFixture();
            yield return null;
            byte[] bytes = File.ReadAllBytes(slot);
            AssertPreviewReads(false, "Opened");
            PreviewChip(false)[0].onClick.Invoke();
            yield return null;
            AssertPreviewReads(true, "Pressed once");
            PreviewChip(true)[0].onClick.Invoke();
            yield return null;
            AssertPreviewReads(false, "Pressed again");
            PreviewChip(false)[0].onClick.Invoke();
            yield return null;
            AssertPreviewReads(true, "And once more");
            Assert.That(director.SavePath, Is.EqualTo(slot), "The chip opens no slot.");
            Assert.That(File.ReadAllBytes(slot), Is.EqualTo(bytes), "and writes nothing: the choice is held in memory until Start.");
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(before.sessionId));

            CastButtons(CastSelect.StartCaption)[0].onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.False);
            var fresh = director.Snapshot;
            Assert.That(fresh.sessionId, Is.Not.EqualTo(before.sessionId), "A new season started.");
            Assert.That(fresh.dialogueRulesVersion, Is.EqualTo(1), "The preview's season plays dialogue rules 1.");
            Assert.That(DialogueRules.On(fresh), Is.True);
            Assert.That(new Gamesim.Persistence.EpisodeSaveStore(director.SavePath).TryLoad(out var disk, out string message), Is.True, message);
            Assert.That(disk.dialogueRulesVersion, Is.EqualTo(1), "The season carries it on disk, as its own rule.");

            // The next time the screen opens it is off again: nothing remembered the last choice.
            string previewSession = fresh.sessionId;
            yield return OpenCastScreen();
            AssertPreviewReads(false, "Opened again");
            CastButtons(CastSelect.StartCaption)[0].onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(director.Snapshot.sessionId, Is.Not.EqualTo(previewSession));
            Assert.That(director.Snapshot.dialogueRulesVersion, Is.Zero, "Left off, the season is today's shipped season.");
        }

        /// <summary>The creator works on a copy of the screen's choice, so a season started there keeps the preview; Back keeps it on the screen.</summary>
        [UnityTest]
        public IEnumerator CastSelect_TheConversationPreviewGoesWithTheChoiceThroughTheCreator()
        {
            yield return OpenCastScreen();
            var emma = CastTemplates.Find("emma-brown");
            CastButtons(emma.Name)[0].onClick.Invoke();
            yield return null;
            PreviewChip(false)[0].onClick.Invoke();
            yield return null;
            AssertPreviewReads(true, "Chosen before the creator");
            CastButtons("Customize Emma")[0].onClick.Invoke();
            yield return null;
            Assert.That(Creator().IsShowing, Is.True);
            CastButtons(CharacterCreator.BackCaption)[0].onClick.Invoke();
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.True);
            AssertPreviewReads(true, "Back from the creator");
            CastButtons("Customize Emma")[0].onClick.Invoke();
            yield return null;
            string before = director.Snapshot.sessionId;
            CastButtons(CharacterCreator.StartCaption)[0].onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(Creator().IsShowing, Is.False, "The season started from the creator.");
            Assert.That(director.Snapshot.sessionId, Is.Not.EqualTo(before));
            Assert.That(director.Snapshot.dialogueRulesVersion, Is.EqualTo(1), "The creator's start keeps the preview chosen on the cast screen.");
        }

        /// <summary>
        /// The chip is on the ring the keyboard's Down and the pad's d-pad walk, and pressed by Enter and by A. A press
        /// rebuilds the screen; the focus stays on the chip, under its new caption, so a second press undoes the first.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator CastSelect_TheConversationPreviewIsReachedAndPressedByTheKeyboardAndThePad()
        {
            yield return OpenCastScreen();
            yield return Frames(2);
            yield return ReachAndPress(CastSelect.ConversationPreviewOnCaption, pad: false);
            AssertPreviewReads(true, "Enter on the chip");
            Assert.That(SelectedCarries(CastSelect.ConversationPreviewOffCaption), Is.True, "The keyboard is still on the chip: " + SelectedName());
            yield return PressSubmit(false);
            yield return Frames(2);
            AssertPreviewReads(false, "Enter again");
            Assert.That(SelectedCarries(CastSelect.ConversationPreviewOnCaption), Is.True, "and still on the chip: " + SelectedName());

            yield return PressOnPad(UnityEngine.InputSystem.LowLevel.GamepadButton.DpadDown);
            yield return ReachAndPress(CastSelect.ConversationPreviewOnCaption, pad: true);
            AssertPreviewReads(true, "A on the chip");
            Assert.That(SelectedCarries(CastSelect.ConversationPreviewOffCaption), Is.True, "The pad is still on the chip: " + SelectedName());
            CastScreen().Dismiss();
            yield return null;
        }

        /// <summary>
        /// The chip and its line fit the footer at the three text sizes, on 16:9, 4:3 and 21:9, in both states: inside
        /// the footer's fixed height, clear of the house size, the start and cancel and the footnote; the chip tall
        /// enough to press; every label of the chip and the line set at thirteen or more, in a box 1.3 times its type,
        /// drawing every glyph (a TMP box under Inter's 1.21 line draws nothing while its text reads right); the line in
        /// two lines at most in the wide footer's box of 40, and in one in the narrow footer's.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_TheConversationPreviewFitsTheFooterAtEveryTextSize([Values(1f, 1.2f, 1.4f)] float text)
        {
            // The words this fit is measured for (owner, 2026-10-25: never lose the plea). The On line said the plea waits while
            // a house moment is open; the desperate plea is now the one-click plea then, so the line says that instead.
            Assert.That(CastSelect.ConversationPreviewOnLine,
                Is.EqualTo("On for this season: a desperate plea becomes a short talk when the house is free, and one click when not."));
            Assert.That(CastSelect.ConversationPreviewOffLine, Is.EqualTo("Off. A desperate plea is one click, as in every season."));
            yield return OpenCastScreen();
            var screen = CastScreen();
            var emma = CastTemplates.Find("emma-brown");
            CastButtons(emma.Name)[0].onClick.Invoke();
            yield return null;
            var canvas = screen.GetComponent<Canvas>();
            try
            {
                screen.FontScale = text;
                foreach (var frame in new[] { (wide: 1600, high: 900, shape: "16:9"), (wide: 1024, high: 768, shape: "4:3"), (wide: 2560, high: 1080, shape: "21:9") })
                {
                    yield return LayCastScreenOutAt(frame.wide, frame.high, text);
                    foreach (bool on in new[] { false, true })
                    {
                        if (screen.CampaignConversations != on)
                        {
                            PreviewChip(!on)[0].onClick.Invoke();
                            yield return null;
                            Canvas.ForceUpdateCanvases();
                        }
                        AssertThePreviewFits(screen, frame.shape + " at text " + text.ToString("0.0") + ", " + (on ? "on" : "off"), on);
                    }
                }
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                screen.FontScale = TextSizes.Scale(director.TextSize);
            }
            screen.Dismiss();
            yield return null;
        }

        private void AssertThePreviewFits(CastSelect screen, string shape, bool on)
        {
            Canvas.ForceUpdateCanvases();
            AssertPreviewReads(on, shape);
            var root = (RectTransform)screen.transform;
            var footer = screen.GetComponentsInChildren<RectTransform>().Last(rect => rect.name == "Fixed season footer" && rect.gameObject.activeInHierarchy);
            Rect In(RectTransform rect) { var b = RectTransformUtility.CalculateRelativeRectTransformBounds(root, rect); return new Rect(b.min, b.size); }
            var chip = (RectTransform)PreviewChip(on)[0].transform;
            var line = PreviewLine();
            Assert.That(chip.rect.height, Is.GreaterThanOrEqualTo(38f - .5f), shape + ": the chip is tall enough to press.");
            var bounds = In(footer);
            foreach (var part in new[] { chip, line.rectTransform })
            {
                var at = In(part);
                Assert.That(at.xMin >= bounds.xMin - .5f && at.xMax <= bounds.xMax + .5f && at.yMin >= bounds.yMin - .5f && at.yMax <= bounds.yMax + .5f, Is.True,
                    shape + ": '" + part.name + "' lies inside the footer: " + at + " in " + bounds + ".");
                Assert.That(at.yMin, Is.GreaterThanOrEqualTo(In(root).yMin - .5f), shape + ": '" + part.name + "' is on the screen.");
            }
            // Clear of every other control and line the footer draws.
            var others = new List<RectTransform>();
            foreach (var caption in new[] { CastSelect.StartCaption, CastSelect.CancelCaption, "Fewer houseguests", "More houseguests" })
                others.Add((RectTransform)CastButtons(caption).Last().transform);
            others.Add(screen.GetComponentsInChildren<RectTransform>().Last(rect => rect.name == CastSelect.HouseSizePanelName && rect.gameObject.activeInHierarchy));
            others.Add(screen.GetComponentsInChildren<TMP_Text>().Last(label => label.isActiveAndEnabled && label.text.StartsWith("A shorter season")).rectTransform);
            foreach (var mine in new[] { chip, line.rectTransform })
                foreach (var other in others)
                {
                    var a = In(mine); var b = In(other);
                    a = new Rect(a.x + .5f, a.y + .5f, a.width - 1f, a.height - 1f);
                    Assert.That(a.Overlaps(b), Is.False, shape + ": '" + mine.name + "' " + a + " runs into '" + other.name + "' " + b + ".");
                }
            foreach (var label in chip.GetComponentsInChildren<TMP_Text>().Concat(new[] { line }))
            {
                label.ForceMeshUpdate(true);
                string said = "'" + label.text + "' (" + label.name + ") on " + shape;
                Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(CastSelect.FootnoteSize - .01f), said + " is set under thirteen: " + label.fontSize.ToString("0.#") + ".");
                Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.fontSize * 1.3f - .5f),
                    said + " has a box " + label.rectTransform.rect.height.ToString("0.0") + " high for a " + label.fontSize.ToString("0.#") + " font.");
                Assert.That(label.isTextOverflowing || label.isTextTruncated, Is.False, said + " runs out of its box.");
                int glyphs = label.text.Count(c => !char.IsWhiteSpace(c));
                int drawn = Enumerable.Range(0, label.textInfo.characterCount).Count(i => label.textInfo.characterInfo[i].isVisible);
                Assert.That(drawn, Is.EqualTo(glyphs), said + " draws " + drawn + " of its " + glyphs + " glyphs.");
            }
            Assert.That(chip.GetComponentInChildren<TMP_Text>().textInfo.lineCount, Is.EqualTo(1), shape + ": the chip's caption stays on one line.");
            // The wide footer gives the line a box of 40 beside the chip, two lines of thirteen; the narrow footer one line of 20.
            bool wideLine = line.rectTransform.rect.height > 30f;
            Assert.That(line.textInfo.lineCount, Is.LessThanOrEqualTo(wideLine ? 2 : 1),
                shape + ": the line sets in " + line.textInfo.lineCount + " lines in its box " + line.rectTransform.rect.size.ToString("0") + ".");
        }
    }
}
