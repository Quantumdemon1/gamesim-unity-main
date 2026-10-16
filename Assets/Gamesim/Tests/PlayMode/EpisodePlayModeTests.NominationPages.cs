using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The nomination's steps at the largest text (the owner's contract of 2026-10-15;
    /// EpisodeHud.NominationPages.cs): a step that cannot fit the stage on one page is dealt onto
    /// pages, the shared pager at each page's foot, reached by the mouse, the keyboard and the pad,
    /// and turning one commits nothing. The page counts are literals, measured per frame and taken
    /// for the frame running (<see cref="NominationFrameNow"/>): the batch canvas and the 4:3 frame
    /// are one shape, the 16:9 frame is shorter.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The ceremony of sixteen at the largest text: its pages on the 16:9 frame and on 4:3 (the batch canvas's shape).</summary>
        private static readonly (int At16x9, int At4x3) CeremonyOfSixteenLargestPages = (2, 2);
        /// <summary>The Head of Household's picker of sixteen, a story waiting, at the largest text: on 16:9 and on 4:3.</summary>
        private static readonly (int At16x9, int At4x3) PickerOfSixteenLargestPages = (2, 2);
        /// <summary>The picker of sixteen with nothing waiting, and its backdoor plan, at the largest text: on 16:9 and on 4:3.</summary>
        private static readonly (int At16x9, int At4x3) BarePickerOfSixteenLargestPages = (2, 2), BackdoorOfSixteenLargestPages = (2, 2);
        /// <summary>A story's WHO? of the fifteen others in a house of sixteen at the largest text: on 16:9 and on 4:3.</summary>
        private static readonly (int At16x9, int At4x3) WhoOfSixteenLargestPages = (2, 2);

        /// <summary>
        /// The frame the HUD is laid out for now, by its canvas's shape, under the names the
        /// nomination's per-frame literals use: "16x9" or "4x3" (the batch run's canvas is 4:3). Any
        /// other shape has no literal, and says so plainly rather than borrowing one.
        /// </summary>
        private string NominationFrameNow()
        {
            var root = ((RectTransform)HudCanvas().transform).rect;
            float aspect = root.width / root.height;
            if (Mathf.Abs(aspect - 16f / 9f) < .02f) return "16x9";
            if (Mathf.Abs(aspect - 4f / 3f) < .02f) return "4x3";
            Assert.Fail("The suite names the nomination's pages at the largest text for the 16:9 and 4:3 frames only; this canvas is " + root.size + ".");
            return null;
        }

        /// <summary>The pages a nomination step has at the largest text on the frame running: the suite's literal for that frame.</summary>
        private int NominationPagesNow((int At16x9, int At4x3) pages) => NominationFrameNow() == "16x9" ? pages.At16x9 : pages.At4x3;

        /// <summary>The pages a nomination step has at the text size running: one at the standard and the larger, the frame's literal at the largest.</summary>
        private int NominationPagesHere((int At16x9, int At4x3) pages) => director.TextSize == TextSize.Largest ? NominationPagesNow(pages) : 1;

        /// <summary>
        /// The batch run photographs every page of the step open now on the 16:9 frame and on a 4:3
        /// one, each as the page the suite names for that frame, so the pages can be read as a
        /// player sees them.
        /// </summary>
        private IEnumerator CaptureNominationPages(string name, (int At16x9, int At4x3) pages, string where)
        {
            if (!Application.isBatchMode) yield break;
            foreach (var frame in new[] { (Width: 1600, Height: 900, Name: "16x9", Pages: pages.At16x9), (Width: 1200, Height: 900, Name: "4x3", Pages: pages.At4x3) })
            {
                for (int page = 1; page <= frame.Pages; page++)
                {
                    int turns = page - 1, shown = page, named = frame.Pages;
                    yield return CaptureFraming(name + "-largest-" + frame.Name + "-p" + page, settle: false, width: frame.Width, height: frame.Height,
                        arrange: () =>
                        {
                            ShowFirstPage();
                            for (int turn = 0; turn < turns; turn++) LivePageControl(EpisodeHud.NextPageCaption).onClick.Invoke();
                            Canvas.ForceUpdateCanvases();
                        },
                        inspect: picture =>
                        {
                            Assert.That(NominationFrameNow(), Is.EqualTo(frame.Name), where + " is laid out for the " + frame.Name + " frame.");
                            Assert.That(LargestPageIndicator(), Is.EqualTo((shown, named)),
                                where + " on the " + frame.Name + " frame: page " + shown + " of the suite's " + named + ".");
                        });
                }
            }
        }

        /// <summary>
        /// The ceremony of sixteen at the largest text: it opens on the first of its pages with the
        /// keyboard on the way on, never on the pager; a press where "Next page" stands turns the
        /// page and the keyboard stays on the pager; Enter turns back, each committing nothing; and
        /// opened again, the pad's d-pad walks down from where the step opens to "Next page" and A
        /// on it turns the page, the walk and the turn committing nothing; the ring walks the second
        /// page, the pager and the footer; a rebuild of the open panel keeps the page, and the panel
        /// opened again starts on the first.
        /// </summary>
        [UnityTest]
        public IEnumerator NominationScreen_AtTheLargestTextAStepPagesByMouseKeyboardAndPad()
        {
            yield return InstallNomination(16, false, NominationBlock.Open);
            yield return ApplyTextSize(TextSize.Largest);
            yield return OpenStation();
            yield return null; yield return null;
            int pages = NominationPagesNow(CeremonyOfSixteenLargestPages);
            string where = "The ceremony in a house of sixteen " + TextScales.At(TextSize.Largest);
            Assert.That(LargestPageIndicator(), Is.EqualTo((1, pages)), where + ": the panel opens on the first of " + pages + " pages.");
            var opened = EventSystem.current.currentSelectedGameObject;
            Assert.That(opened != null ? opened.name : "nothing", Is.EqualTo("Continue episode"), where + " opens on the way on, not on the pager.");

            // The mouse.
            int revision = director.Snapshot.revision;
            var next = ButtonWithCaption(EpisodeHud.NextPageCaption);
            Assert.That(PressAt(ScreenCentre(next)), Is.SameAs(next.gameObject), where + ": the press lands on '" + EpisodeHud.NextPageCaption + "'.");
            Assert.That(LargestPageIndicator(), Is.EqualTo((2, pages)), where + ": the press turns to the second page,");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "committing nothing.");
            yield return null; yield return null;
            var focus = EventSystem.current.currentSelectedGameObject;
            Assert.That(focus != null ? focus.name : "nothing", Is.EqualTo(EpisodeHud.PreviousPageCaption),
                where + ": the keyboard stays on the pager as it turns.");

            // The keyboard: Enter on the way back.
            yield return PressKey(Key.Enter);
            yield return null;
            Assert.That(LargestPageIndicator(), Is.EqualTo((1, pages)), where + ": Enter on '" + EpisodeHud.PreviousPageCaption + "' turns back,");
            focus = EventSystem.current.currentSelectedGameObject;
            Assert.That(focus != null ? focus.name : "nothing", Is.EqualTo(EpisodeHud.NextPageCaption), "and the keyboard is on the way on again.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Still nothing committed.");

            // The pad, from where the step opens: the d-pad walks the panel's ring down to the way on
            // - the pager is in the ring like any control - and A on it turns the page; the walk and
            // the turn commit nothing. Opening the panel may commit the house's elapsed time, so what
            // stays as it was is read once it is open.
            director.ClosePanels();
            yield return null;
            yield return OpenStation();
            yield return null; yield return null;
            Assert.That(LargestPageIndicator(), Is.EqualTo((1, pages)), where + ": opened again, the step is on its first page,");
            Assert.That(SelectedName(), Is.EqualTo("Continue episode"), "with the keyboard on the way on.");
            revision = director.Snapshot.revision;
            var walked = new List<string> { SelectedName() };
            for (int steps = 0; SelectedName() != EpisodeHud.NextPageCaption && steps < 40; steps++)
            {
                yield return PressOnPad(GamepadButton.DpadDown);
                walked.Add(SelectedName());
            }
            Assert.That(SelectedName(), Is.EqualTo(EpisodeHud.NextPageCaption),
                where + ": the d-pad walks down to '" + EpisodeHud.NextPageCaption + "'. Walked: [" + string.Join(" | ", walked) + "].");
            Assert.That(LargestPageIndicator(), Is.EqualTo((1, pages)), where + ": walking to the pager turns nothing.");
            yield return PressOnPad(GamepadButton.South);
            yield return Frames(2);
            Assert.That(LargestPageIndicator(), Is.EqualTo((2, pages)), where + ": the pad's A on '" + EpisodeHud.NextPageCaption + "' turns the page.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "The d-pad's walk and A committed nothing.");
            yield return AssertKeyboardRing("the ceremony, page 2 " + TextScales.At(TextSize.Largest), ModalRoot);

            // A rebuild keeps the page; the panel opened again starts on the first.
            RenderHudForTheCurrentCanvas();
            Canvas.ForceUpdateCanvases();
            Assert.That(LargestPageIndicator(), Is.EqualTo((2, pages)), where + ": a rebuild of the open panel keeps its page.");
            yield return null;
            director.ClosePanels();
            yield return null;
            yield return OpenStation();
            yield return null; yield return null;
            Assert.That(LargestPageIndicator(), Is.EqualTo((1, pages)), where + ": closed and opened again, the step starts on its first page.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Nothing the pager did was committed.");

            yield return CaptureNominationPages("nomination-ceremony-16", CeremonyOfSixteenLargestPages, where);
            director.ClosePanels();
            yield return ApplyTextSize(false);
        }

        /// <summary>
        /// The Head of Household's picker of sixteen at the largest text, its cards dealt onto pages:
        /// a pick on one page and a pick on another are both the picks - lit wherever their cards
        /// are, named in the selection line, which stands at every page's foot beside the pager so a
        /// pick is named on the page it was made on, kept across the turns - and committing names
        /// those two. The comparison takes the grid's place without a render, as one view: the
        /// step's head with the picks named in it and the records, no pager, no scroll; hidden, the
        /// page is back with its pager and the line at its foot. The backdoor plan behind its door
        /// pages too, and the way back starts the picker on its first page.
        /// </summary>
        [UnityTest]
        public IEnumerator NominationScreen_AtTheLargestTextPicksOnAnyPageAreThePicks()
        {
            yield return InstallNomination(16, true, NominationBlock.Open);
            yield return ApplyTextSize(TextSize.Largest);
            yield return OpenStation();
            yield return null; yield return null;
            string where = "The picker in a house of sixteen " + TextScales.At(TextSize.Largest);
            int pages = NominationPagesNow(BarePickerOfSixteenLargestPages);
            int planPages = NominationPagesNow(BackdoorOfSixteenLargestPages);
            var candidates = EpisodeEngine.NominationCandidates(director.Snapshot).ToList();
            int revision = director.Snapshot.revision;
            Assert.That(LargestPageIndicator(), Is.EqualTo((1, pages)), where + " opens on the first of " + pages + " pages.");
            AssertSelectionAtTheFoot(where + ", page 1 with nothing picked", EpisodeHud.ChooseTwoAtTheFootWords);
            var first = candidates[0];
            var last = candidates[candidates.Count - 1];
            Assert.That(FindCandidateCard(first.name), Is.Not.Null, where + ": the first candidate is on the first page.");
            Assert.That(FindCandidateCard(last.name), Is.Null, where + ": the last is not.");

            // A pick on the first page, then one on the last, each named on the page it was made on.
            ButtonWithCaption(first.name).onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            AssertSelectionAtTheFoot(where + ", page 1 after its pick", "Selected: " + first.name + " and —");
            for (int turns = 0; FindCandidateCard(last.name) == null; turns++)
            {
                Assert.That(turns < pages, Is.True, where + ": no page shows " + last.name + ".");
                LivePageControl(EpisodeHud.NextPageCaption).onClick.Invoke();
                Canvas.ForceUpdateCanvases();
            }
            int lastPage = LargestPageNow();
            AssertSelectionAtTheFoot(where + ", page " + lastPage + ", turned to", "Selected: " + first.name + " and —");
            ButtonWithCaption(last.name).onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            Assert.That(Picked(last.name), Is.True, where + ": the card just picked is lit, on its own page.");
            AssertSelectionAtTheFoot(where + ", page " + lastPage + ", the page the second pick was made on", "Selected: " + first.name + " and " + last.name);
            ShowFirstPage();
            Assert.That(Picked(first.name), Is.True, where + ": the first pick is still lit after the turns.");
            AssertSelectionAtTheFoot(where + ", page 1 again: a pick on each page, both named", "Selected: " + first.name + " and " + last.name);
            for (int turns = 0; FindCandidateCard(last.name) == null; turns++)
            {
                Assert.That(turns < pages, Is.True, where + ": no page shows " + last.name + ".");
                LivePageControl(EpisodeHud.NextPageCaption).onClick.Invoke();
                Canvas.ForceUpdateCanvases();
            }
            Assert.That(Picked(last.name), Is.True, where + ": the second pick is still lit, turned back to.");
            ShowFirstPage();
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), where + ": picking and turning commit nothing.");

            // The comparison is one view, in the grid's place: no pager, no scroll, and the picks
            // named at the step's head as the one-page step has them.
            ButtonWithCaption(EpisodeHud.ShowCandidateContextCaption).onClick.Invoke();
            AssertNominationScreen("The comparison " + TextScales.At(TextSize.Largest), "Commit nominations", EpisodeHud.HideCandidateContextCaption);
            Assert.That(ActiveRect(EpisodeHud.CandidateContextName), Is.Not.Null, "The comparison is up,");
            Assert.That(ActiveRect(EpisodeHud.NomineeGridName), Is.Null, "in the grid's place,");
            Assert.That(LargestPagerNow(), Is.Null, "as one view, with no pager.");
            var head = ActiveRect(EpisodeHud.SelectedNomineesName);
            Assert.That(head != null && head.parent == ActiveRect("Episode content"), Is.True, "The comparison names the picks at the step's head,");
            Assert.That(head.GetComponent<TMP_Text>().text, Is.EqualTo("Selected: " + first.name + " and " + last.name), "both of them.");
            ButtonWithCaption(EpisodeHud.HideCandidateContextCaption).onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            Assert.That(LargestPageIndicator(), Is.EqualTo((1, pages)), where + ": hidden again, the page is back with its pager.");
            AssertNominationScreen(where + ", the comparison hidden", "Commit nominations", EpisodeHud.ShowCandidateContextCaption);
            AssertSelectionAtTheFoot(where + ", the comparison hidden", "Selected: " + first.name + " and " + last.name);
            yield return null;
            yield return CaptureNominationPages("nomination-picker-16", BarePickerOfSixteenLargestPages, where);

            // The backdoor plan pages too, from its own first page though its door is on the
            // picker's last; and the way back from its last page is the picker's first.
            ShowFirstPage();
            ShowPageWith(EpisodeHud.PlanBackdoorRowName);
            Assert.That(LargestPageNow(), Is.EqualTo(pages), where + ": the door to the backdoor plan is on the picker's last page.");
            ButtonWithCaption(EpisodeHud.PlanBackdoorCaption).onClick.Invoke();
            yield return null;
            string plan = "The backdoor plan in a house of sixteen " + TextScales.At(TextSize.Largest);
            Assert.That(LargestPageIndicator(), Is.EqualTo((1, planPages)), plan + " opens on its own first page.");
            var aims = new List<string>();
            AssertLargestPages(plan, planPages, page =>
            {
                AssertNominationScreen(plan + ", page " + page, EpisodeHud.BackToNomineesCaption);
                aims.AddRange(ActiveRect(EpisodeHud.BackdoorAimsName).GetComponentsInChildren<Button>().Select(aim => aim.name));
            });
            Assert.That(aims, Is.EquivalentTo(candidates.Select(candidate => "Aim this week at " + candidate.name)), plan + ": every aim on exactly one page.");
            yield return CaptureNominationPages("nomination-backdoor-16", BackdoorOfSixteenLargestPages, plan);
            ShowFirstPage();
            for (int turns = 1; turns < planPages; turns++)
            {
                LivePageControl(EpisodeHud.NextPageCaption).onClick.Invoke();
                Canvas.ForceUpdateCanvases();
            }
            Assert.That(LargestPageNow(), Is.EqualTo(planPages), plan + ": turned to its last page.");
            ButtonWithCaption(EpisodeHud.BackToNomineesCaption).onClick.Invoke();
            yield return null;
            Assert.That(LargestPageIndicator(), Is.EqualTo((1, pages)), where + ": back from the plan's last page, the picker starts on its first.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), where + ": none of it committed anything.");

            // Committing names the two picked on different pages.
            ButtonWithCaption("Commit nominations").onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision + 1), "The commit is one command.");
            Assert.That(director.Snapshot.nominees, Is.EquivalentTo(new[] { first.id, last.id }), "It names the two picked, one from each page.");
            director.ClosePanels();
            yield return ApplyTextSize(false);
        }

        /// <summary>A candidate's card on the page shown, or null.</summary>
        private Button FindCandidateCard(string name)
        {
            var grid = ActiveRect(EpisodeHud.NomineeGridName);
            return grid == null ? null : grid.GetComponentsInChildren<Button>().FirstOrDefault(card => card.name == name);
        }

        /// <summary>Whether a candidate's card on the page shown is marked picked.</summary>
        private bool Picked(string name)
        {
            var card = FindCandidateCard(name);
            var mark = card != null ? card.transform.Find("Picked") : null;
            return mark != null && mark.gameObject.activeSelf;
        }

        /// <summary>
        /// The picker's selection line on the page of a paged picker shown now: at the page's foot,
        /// in the pager's band and left of where the page is, inside the stage's view, saying
        /// <paramref name="words"/> whole in a box 1.3 times its type or more.
        /// </summary>
        private void AssertSelectionAtTheFoot(string where, string words)
        {
            Canvas.ForceUpdateCanvases();
            var line = ActiveRect(EpisodeHud.SelectedNomineesName);
            Assert.That(line, Is.Not.Null, where + ": the selection line is on the page.");
            var foot = ActiveRect(EpisodeHud.StepPagesRowName);
            Assert.That(foot != null && line.IsChildOf(foot), Is.True, where + ": the selection line stands at the page's foot, beside the pager.");
            var said = line.GetComponent<TMP_Text>();
            Assert.That(said.text, Is.EqualTo(words), where + ": the selection line names the picks.");
            var count = LargestPagerNow().GetComponentsInChildren<TMP_Text>().Single(label => label.name == EpisodeHud.LargestPageCountName);
            Rect at = ScreenRect(line), page = ScreenRect(count.rectTransform);
            Assert.That(at.xMax, Is.LessThanOrEqualTo(page.xMin + .5f), where + ": the line is left of where the page is, clear of it.");
            Assert.That(at.yMin < page.yMax && page.yMin < at.yMax, Is.True, where + ": the line is level with the pager.");
            AssertInside(ScreenRect((RectTransform)ActiveRect("Episode content").parent), line, where + ": the selection line");
            said.ForceMeshUpdate(true);
            Assert.That(said.isTextTruncated, Is.False, where + ": '" + said.text + "' says all of its words, at " + said.fontSize.ToString("0.#") + ".");
            Assert.That(line.rect.height, Is.GreaterThanOrEqualTo(1.3f * said.fontSize - .5f), where + ": in a box 1.3 times its type or more.");
        }
    }
}
