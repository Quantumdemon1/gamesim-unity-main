using System;
using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The frames of PLAN A's A3 and A4 (A.3: every visible change is photographed at both text sizes,
    /// on a 16:9 frame and a 4:3 one): the pause menu by Escape and by Start, its head saying PAUSED;
    /// the pad's chips beside the captions that name a key, in the pause menu and on the rail; the
    /// controls page unfolded; the house on a pad - the help card and the prompt in the pad's words;
    /// and the tour's controls line on a pad.
    ///
    /// <para>In a batch run each is written beside the project as 'input-pause-menu',
    /// 'input-pause-menu-pad', 'input-controls-page', 'input-house-pad' and 'input-tour-pad', with
    /// '-large' at the larger text and '-4x3' on the 4:3 frame, and checked on each frame while it is
    /// laid out there: every hint drawn whole in a box at least 1.3 times its words, and every chip on
    /// the screen, inside whatever clips its control, and over none of the words around it - its own
    /// control's, or its neighbours'. In the editor the same checks run on the game view's shape, and
    /// nothing is written.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest, Timeout(900000)]
        public IEnumerator Hints_TheChipsTheHelpCardTheControlsPageAndThePauseMenuHoldAtBothTextSizesOnBothFrames()
        {
            HoldTheHouseForTheFixture();
            yield return SettleCast();
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                string size = larger ? "-large" : "";
                string at = larger ? " at the larger text" : " at the standard text";

                // The pause menu by Escape, on the keyboard: PAUSED at its head, and no chip anywhere.
                yield return PressKey(Key.LeftCtrl);
                director.ClosePanels();
                yield return Frames(2);
                yield return PressKey(Key.Escape);
                yield return Frames(2);
                Assert.That(director.HintsForPad, Is.False, "The keyboard's words" + at + ".");
                Assert.That(director.IsPanelOpen && PausedEyebrowShown(), Is.True, "Escape with nothing open is the pause menu" + at + ".");
                yield return OnBothFrames("input-pause-menu" + size, "The pause menu by Escape" + at, where =>
                {
                    AssertPausedEyebrowFits(where);
                    AssertNoGlyphChips(where);
                });

                // By Start on a pad: the same menu, with the pad's B beside Close and its L3 beside Save now.
                director.ClosePanels();
                yield return Frames(2);
                yield return PressOnPad(GamepadButton.Start);
                yield return Frames(2);
                Assert.That(director.HintsForPad, Is.True, "The pad's words" + at + ".");
                Assert.That(director.IsPanelOpen && PausedEyebrowShown(), Is.True, "Start with nothing open is the pause menu" + at + ".");
                yield return OnBothFrames("input-pause-menu-pad" + size, "The pause menu by Start" + at, where =>
                {
                    AssertPausedEyebrowFits(where);
                    AssertGlyphChipsBeside(where, "Close  [Esc]", "Save now  [F5]");
                });

                // The controls page, unfolded, its head at the top of the column.
                ButtonWithCaption(EpisodeDirector.ShowControlsCaption).onClick.Invoke();
                yield return Frames(2);
                Action head = () => ScrollRowToTheTop((RectTransform)FindButton(EpisodeDirector.HideControlsCaption).transform);
                head();
                yield return OnBothFrames("input-controls-page" + size, "The controls page" + at, AssertControlsPageHolds, head);
                ButtonWithCaption(EpisodeDirector.HideControlsCaption).onClick.Invoke();
                yield return Frames(2);

                // The house on a pad: the rail's chips, the help card and the prompt in the pad's words.
                director.ClosePanels();
                yield return Frames(2);
                WarpPlayer(director.StationPosition);
                yield return PressOnPad(GamepadButton.RightStick);
                yield return Frames(2);
                ButtonWithCaption("Help · controls").onClick.Invoke();
                yield return Frames(2);
                Assert.That(HelpCardShows(InputGlossary.HelpCard(true)), Is.True, "The help card in the pad's words" + at + ".");
                yield return OnBothFrames("input-house-pad" + size, "The house on a pad" + at, where =>
                {
                    AssertGlyphChipsBeside(where, "Notebook [J]", "Save [F5]", EpisodeHud.DiaryTravelCaption);
                    AssertHintFits(InputGlossary.HelpCard(true), where + ", the help card");
                    string prompt = PromptWords().FirstOrDefault(words => words.StartsWith(InputGlossary.PromptKey(true) + "  ·  ", StringComparison.Ordinal));
                    Assert.That(prompt, Is.Not.Null, where + ": the prompt names the pad's X: " + string.Join(" | ", PromptWords()));
                    AssertHintFits(prompt, where + ", the prompt");
                });
                ButtonWithCaption("Hide controls").onClick.Invoke();
                yield return Frames(2);

                // The tour on a pad: its controls line names the pad's buttons.
                var tour = DirectorTour();
                tour.Show(TourChrome);
                yield return Frames(2);
                yield return PressOnPad(GamepadButton.RightStick);
                yield return Frames(2);
                Assert.That(tour.IsShowing && tour.PadLine, Is.True, "The tour is up, in the pad's words" + at + ".");
                yield return OnBothFrames("input-tour-pad" + size, "The tour on a pad" + at, where =>
                {
                    var line = tour.GetComponentsInChildren<TMP_Text>()
                        .LastOrDefault(text => text.isActiveAndEnabled && text.text.Contains(InputGlossary.TourLine(true)));
                    Assert.That(line, Is.Not.Null, where + ": the tour's card carries the pad's controls line.");
                    AssertLineHasRoom(line, where + ", the tour's card");
                });
                tour.Skip();
                yield return Frames(2);
            }
            yield return PressKey(Key.LeftCtrl);
            yield return ApplyTextSize(false);
        }

        /// <summary>
        /// In a batch run, photographs the screen as it stands as '<paramref name="name"/>' on the 16:9
        /// frame and '<paramref name="name"/>-4x3' on the 4:3 one, and checks it on each while it is
        /// laid out there; <paramref name="arrange"/> runs again after each frame's render. In the
        /// editor the game view is whatever shape it happens to be: the check runs on it, and nothing
        /// is written.
        /// </summary>
        private IEnumerator OnBothFrames(string name, string what, Action<string> check, Action arrange = null)
        {
            if (!Application.isBatchMode)
            {
                arrange?.Invoke();
                Canvas.ForceUpdateCanvases();
                yield return null;
                check(what);
                yield break;
            }
            yield return CaptureFraming(name, inspect: _ => check(what + " on the 16:9 frame"), arrange: arrange);
            yield return CaptureFraming(name + "-4x3", inspect: _ => check(what + " on the 4:3 frame"), width: 1200, height: 900, arrange: arrange);
        }

        /// <summary>The settings' head says PAUSED, drawn whole in a box at least 1.3 times its words.</summary>
        private void AssertPausedEyebrowFits(string where) => AssertHintFits(EpisodeDirector.PausedEyebrow, where + ", the PAUSED eyebrow");

        /// <summary>The live label carrying exactly these words is drawn whole, in a box at least 1.3 times them.</summary>
        private void AssertHintFits(string words, string where)
        {
            Canvas.ForceUpdateCanvases();
            var label = director.GetComponentsInChildren<TMP_Text>()
                .LastOrDefault(text => text.isActiveAndEnabled && text.text == words);
            Assert.That(label, Is.Not.Null, where + ": '" + words + "' is on screen.");
            AssertLineHasRoom(label, where);
        }

        /// <summary>On the keyboard, no control wears a pad chip.</summary>
        private void AssertNoGlyphChips(string where)
        {
            var shown = director.GetComponentsInChildren<RectTransform>()
                .Where(rect => rect.name == EpisodeHud.PadGlyphName && rect.gameObject.activeInHierarchy)
                .Select(rect => rect.parent != null ? rect.parent.name : "-").ToArray();
            Assert.That(shown, Is.Empty, where + ": pad chips on the keyboard, beside " + string.Join(", ", shown));
        }

        /// <summary>
        /// Each control with one of these captions wears the pad's chip, and the chip holds: its glyph
        /// drawn whole in a box at least 1.3 times it, the chip on the screen and inside whatever clips
        /// its control while the control itself is in view, and over no word of its control or of the
        /// controls and copy beside it - every visible character under the control's parent.
        /// </summary>
        private void AssertGlyphChipsBeside(string where, params string[] captions)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var caption in captions)
            {
                string what = where + ", '" + caption + "'";
                var control = FindButton(caption);
                var chip = Chip(control);
                Assert.That(chip != null && chip.activeInHierarchy, Is.True, what + " wears the pad's button beside it.");
                Assert.That(CaptionWords(control), Does.Contain(caption), what + ": its caption stays its own.");
                AssertLineHasRoom(chip.GetComponentInChildren<TMP_Text>(), what + "'s chip");

                var badge = CanvasRect((RectTransform)chip.transform);
                var screen = CanvasRect((RectTransform)control.GetComponentInParent<Canvas>().rootCanvas.transform);
                Assert.That(Encloses(screen, badge), Is.True, what + ": the chip " + badge + " is off the screen " + screen + ".");
                var body = CanvasRect((RectTransform)control.transform);
                foreach (var mask in chip.GetComponentsInParent<RectMask2D>())
                {
                    var area = CanvasRect(mask.rectTransform);
                    if (!Encloses(area, body)) continue; // the control is scrolled part out of view: so is its chip, rightly
                    Assert.That(Encloses(area, badge), Is.True, what + ": the chip " + badge + " is cut by '" + mask.name + "' " + area + ".");
                }

                var around = control.transform.parent != null ? control.transform.parent : control.transform;
                foreach (var words in around.GetComponentsInChildren<TMP_Text>())
                {
                    if (!words.isActiveAndEnabled || string.IsNullOrWhiteSpace(words.text) || words.transform.IsChildOf(chip.transform)) continue;
                    // A line scrolled out of its column's view is not drawn, wherever it stands.
                    var clips = words.GetComponentsInParent<RectMask2D>().Select(clip => CanvasRect(clip.rectTransform)).ToArray();
                    words.ForceMeshUpdate();
                    var info = words.textInfo;
                    for (int i = 0; i < info.characterCount; i++)
                    {
                        var glyph = info.characterInfo[i];
                        if (!glyph.isVisible) continue;
                        var drawn = GlyphRect(words, glyph);
                        if (clips.Any(clip => !clip.Overlaps(drawn))) continue;
                        Assert.That(badge.Overlaps(drawn), Is.False,
                            what + ": the chip " + badge + " covers '" + glyph.character + "' of '" + words.text + "' at " + drawn + ".");
                    }
                }
            }
        }

        /// <summary>One drawn character's box, in the HUD canvas's own space (<see cref="CanvasRect"/>).</summary>
        private static Rect GlyphRect(TMP_Text label, TMP_CharacterInfo glyph)
        {
            var canvas = label.GetComponentInParent<Canvas>().rootCanvas.transform;
            Vector3 a = canvas.InverseTransformPoint(label.transform.TransformPoint(glyph.bottomLeft));
            Vector3 b = canvas.InverseTransformPoint(label.transform.TransformPoint(glyph.topRight));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>
        /// The controls page as it is photographed: its fold-out and its first line in the column's
        /// view, and a line for every action on it, each drawn whole in a box at least 1.3 times its
        /// words - the long ones, a ceremony card's Skip with its four keys and two buttons, wrapped
        /// rather than cut.
        /// </summary>
        private void AssertControlsPageHolds(string where)
        {
            Canvas.ForceUpdateCanvases();
            var fold = (RectTransform)FindButton(EpisodeDirector.HideControlsCaption).transform;
            var lines = ControlLines();
            Assert.That(lines.Select(line => line.name).Distinct().Count(), Is.EqualTo(ControlsPage.Lines(cameraRig.Actions).Count),
                where + ": a line for every action.");
            AssertRowInView(fold, where + ", the fold-out");
            AssertRowInView(lines.First().rectTransform, where + ", '" + lines.First().text + "'");
            foreach (var line in lines) AssertLineHasRoom(line, where + ", " + line.name);
        }

        /// <summary>A row inside its scrolling column's view.</summary>
        private static void AssertRowInView(RectTransform row, string what)
        {
            var scroll = row.GetComponentInParent<ScrollRect>();
            Assert.That(scroll, Is.Not.Null, what + " is in a scrolling column.");
            var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            Assert.That(Encloses(CanvasRect(viewport), CanvasRect(row)), Is.True, what + " is in the column's view.");
        }

        /// <summary>Scrolls a row's column so the row stands at the top of its view, a little below the edge.</summary>
        private static void ScrollRowToTheTop(RectTransform row)
        {
            var scroll = row.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.content == null) return;
            Canvas.ForceUpdateCanvases();
            var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, row);
            var position = scroll.content.anchoredPosition;
            position.y = Mathf.Clamp(position.y + (viewport.rect.yMax - bounds.max.y) - 4f, 0f,
                Mathf.Max(0f, scroll.content.rect.height - viewport.rect.height));
            scroll.StopMovement();
            scroll.content.anchoredPosition = position;
            Canvas.ForceUpdateCanvases();
        }
    }
}
