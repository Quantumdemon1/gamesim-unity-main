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
    /// that starts the next season on the pinned dialogue rules - 2, D2's vote-word conversation, since the chip moved
    /// from D1's pilot, rules 1 - and nothing else. Driven through the
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
        public IEnumerator CastSelect_TheConversationPreviewIsOffUntilChosenAndStartsTheSeasonOnDialogueRules2()
        {
            Assert.That(DialogueRules.Offered, Is.EqualTo(2), "The chip writes dialogue rules 2 since this change: D2's vote-word tree, frozen by the move.");
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
            Assert.That(fresh.dialogueRulesVersion, Is.EqualTo(2), "The preview's season plays dialogue rules 2, the chip's since this change.");
            Assert.That(DialogueRules.On(fresh), Is.True);
            Assert.That(new Gamesim.Persistence.EpisodeSaveStore(director.SavePath).TryLoad(out var disk, out string message), Is.True, message);
            Assert.That(disk.dialogueRulesVersion, Is.EqualTo(2), "The season carries it on disk, as its own rule.");

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
            Assert.That(director.Snapshot.dialogueRulesVersion, Is.EqualTo(2),
                "The creator's start keeps the preview chosen on the cast screen: rules 2, the chip's since this change.");
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

        /// <summary>
        /// The footer's own parts keep apart at every text size (the owner's quick fixes, 2026-11-03, "the footer"), on 16:9,
        /// 4:3, 5:4, 1:1 and 21:9, with the preview off and on. The start's glow, drawn ten past its edge, clears the box of
        /// "You will play as" above it (it ran 3 into it in the wide footer), the footnote's box (7 into it in the narrow
        /// one) and the line under it. The house size's Fewer, its count and More lie inside the panel's glass (More ran 11
        /// past it on 5:4, 1280x1024, at the largest text: the narrow panel shrinks with the frame and its controls stood at
        /// fixed places), the count's caption and the range line beside Fewer draw whole, and every part is in the footer.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_TheFootersPartsKeepApartAtEveryTextSize([Values(1f, 1.2f, 1.4f)] float text)
        {
            yield return OpenCastScreen();
            var screen = CastScreen();
            CastButtons(CastTemplates.Find("emma-brown").Name)[0].onClick.Invoke();
            yield return null;
            var canvas = screen.GetComponent<Canvas>();
            var failures = new List<string>();
            try
            {
                screen.FontScale = text;
                foreach (var frame in new[] { (wide: 1600, high: 900, shape: "16:9"), (wide: 1024, high: 768, shape: "4:3"),
                             (wide: 1280, high: 1024, shape: "5:4"), (wide: 1024, high: 1024, shape: "1:1"), (wide: 2560, high: 1080, shape: "21:9") })
                {
                    yield return LayCastScreenOutAt(frame.wide, frame.high, text);
                    foreach (bool on in new[] { false, true })
                    {
                        if (screen.CampaignConversations != on)
                        {
                            PreviewChip(!on)[0].onClick.Invoke();
                            yield return null;
                        }
                        Canvas.ForceUpdateCanvases();
                        FooterPartsKeepApart(screen, frame.shape + " " + frame.wide + "x" + frame.high + " at text " + text.ToString("0.0")
                            + ", " + (on ? "on" : "off"), failures);
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
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        /// <summary>The footer's parts measured on one frame, each gap and margin logged, each fault added to <paramref name="failures"/>.</summary>
        private void FooterPartsKeepApart(CastSelect screen, string shape, List<string> failures)
        {
            var root = (RectTransform)screen.transform;
            // As drawn, a glow included (the bounds take in every child), and a box by its own rect.
            Rect Drawn(RectTransform rect) { var b = RectTransformUtility.CalculateRelativeRectTransformBounds(root, rect); return new Rect(b.min, b.size); }
            Rect Own(RectTransform rect)
            {
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Vector2 low = root.InverseTransformPoint(corners[0]), high = root.InverseTransformPoint(corners[2]);
                return Rect.MinMaxRect(Mathf.Min(low.x, high.x), Mathf.Min(low.y, high.y), Mathf.Max(low.x, high.x), Mathf.Max(low.y, high.y));
            }
            void Apart(string what, Rect a, Rect b)
            {
                var inset = new Rect(a.x + .5f, a.y + .5f, a.width - 1f, a.height - 1f);
                bool clear = !inset.Overlaps(b);
                float gap = Mathf.Max(Mathf.Max(b.yMin - a.yMax, a.yMin - b.yMax), Mathf.Max(b.xMin - a.xMax, a.xMin - b.xMax));
                TestContext.Out.WriteLine(shape + ": " + what + ", gap " + gap.ToString("0.0") + (clear ? "" : " OVERLAP"));
                if (!clear) failures.Add(shape + ": " + what + " overlap by " + (-gap).ToString("0.0") + ": " + a + " / " + b);
            }
            void Inside(string what, Rect a, Rect box)
            {
                bool inside = a.xMin >= box.xMin - .5f && a.xMax <= box.xMax + .5f && a.yMin >= box.yMin - .5f && a.yMax <= box.yMax + .5f;
                string margins = "left " + (a.xMin - box.xMin).ToString("0.0") + ", right " + (box.xMax - a.xMax).ToString("0.0")
                    + ", top " + (box.yMax - a.yMax).ToString("0.0") + ", foot " + (a.yMin - box.yMin).ToString("0.0");
                TestContext.Out.WriteLine(shape + ": " + what + ": " + margins + (inside ? "" : " OUTSIDE"));
                if (!inside) failures.Add(shape + ": " + what + " lies outside (" + margins + "): " + a + " in " + box);
            }
            void Whole(TMP_Text label)
            {
                label.ForceMeshUpdate(true);
                int glyphs = label.text.Count(c => !char.IsWhiteSpace(c));
                int drawn = Enumerable.Range(0, label.textInfo.characterCount).Count(i => label.textInfo.characterInfo[i].isVisible);
                if (label.isTextOverflowing || label.isTextTruncated || drawn != glyphs)
                    failures.Add(shape + ": '" + label.text + "' draws " + drawn + " of " + glyphs + " glyphs in " + label.rectTransform.rect.size.ToString("0.0"));
            }
            TMP_Text Label(System.Func<TMP_Text, bool> which) => screen.GetComponentsInChildren<TMP_Text>().Last(label => label.isActiveAndEnabled && which(label));
            RectTransform Control(string caption) => (RectTransform)CastButtons(caption).Last().transform;

            var footer = Own(screen.GetComponentsInChildren<RectTransform>().Last(rect => rect.name == "Fixed season footer" && rect.gameObject.activeInHierarchy));
            var start = Drawn(Control(CastSelect.StartCaption));
            Apart("the start's glow and 'You will play as'", start, Own(Label(label => label.text.StartsWith("You will play as")).rectTransform));
            Apart("the start's glow and the footnote", start, Own(Label(label => label.text.StartsWith("A shorter season")).rectTransform));
            Apart("the start's glow and the preview line", start, Own(Label(label => label.name == CastSelect.ConversationPreviewLineName).rectTransform));
            var panel = Own(screen.GetComponentsInChildren<RectTransform>().Last(rect => rect.name == CastSelect.HouseSizePanelName && rect.gameObject.activeInHierarchy));
            Inside("the house size panel in the footer", panel, footer);
            var fewer = Drawn(Control("Fewer houseguests"));
            Inside("Fewer in the house size's glass", fewer, panel);
            Inside("More in the house size's glass", Drawn(Control("More houseguests")), panel);
            var count = Label(label => label.name == "House size");
            Inside("the count in the house size's glass", Own(count.rectTransform), panel);
            Whole(count);
            var range = Label(label => label.text.StartsWith("Between "));
            Whole(range);
            Apart("the range line and Fewer", Own(range.rectTransform), fewer);
            Inside("the start in the footer", start, footer);
            Inside("cancel in the footer", Drawn(Control(CastSelect.CancelCaption)), footer);
        }

        private void AssertThePreviewFits(CastSelect screen, string shape, bool on)
        {
            Canvas.ForceUpdateCanvases();
            AssertPreviewReads(on, shape);
            var root = (RectTransform)screen.transform;
            var footer = screen.GetComponentsInChildren<RectTransform>().Last(rect => rect.name == "Fixed season footer" && rect.gameObject.activeInHierarchy);
            // A part as drawn: its rect and every child's, so a lit pill's glow, ten past its edge, counts. These checks
            // count the glow on purpose (it is drawn), where UiTheme.Glass's layout checks read the rect alone.
            Rect In(RectTransform rect) { var b = RectTransformUtility.CalculateRelativeRectTransformBounds(root, rect); return new Rect(b.min, b.size); }
            // A container by its own rect, children left out. In(footer) and In(root) take in every child, the chip and the
            // line among them, so they always held both: D1's run measured the lit chip's glow 4 under the screen's foot, and
            // neither check below, though both run before the overlap, said so.
            Rect Own(RectTransform rect)
            {
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Vector2 low = root.InverseTransformPoint(corners[0]), high = root.InverseTransformPoint(corners[2]);
                return Rect.MinMaxRect(Mathf.Min(low.x, high.x), Mathf.Min(low.y, high.y), Mathf.Max(low.x, high.x), Mathf.Max(low.y, high.y));
            }
            var chip = (RectTransform)PreviewChip(on)[0].transform;
            var line = PreviewLine();
            Assert.That(chip.rect.height, Is.GreaterThanOrEqualTo(38f - .5f), shape + ": the chip is tall enough to press.");
            var bounds = Own(footer);
            foreach (var part in new[] { chip, line.rectTransform })
            {
                var at = In(part);
                Assert.That(at.xMin >= bounds.xMin - .5f && at.xMax <= bounds.xMax + .5f && at.yMin >= bounds.yMin - .5f && at.yMax <= bounds.yMax + .5f, Is.True,
                    shape + ": '" + part.name + "' lies inside the footer: " + at + " in " + bounds + ".");
                Assert.That(at.yMin, Is.GreaterThanOrEqualTo(Own(root).yMin - .5f), shape + ": '" + part.name + "' is on the screen.");
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
