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
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The nomination's steps at the largest text (the owner's contract of 2026-10-15;
    /// EpisodeHud.NominationPages.cs): a step that cannot fit the stage on one page is dealt onto
    /// pages, the shared pager at each page's foot, reached by the mouse, the keyboard and the pad,
    /// and turning one commits nothing. The page counts are literals, measured per frame: the batch
    /// canvas and the 4:3 frame are one shape, the 16:9 frame is shorter.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The ceremony of sixteen at the largest text: its pages on the batch canvas (4:3) and on the 16:9 frame.</summary>
        private const int CeremonyOfSixteenLargestPages = 2, CeremonyOfSixteenLargestPagesWide = 2;
        /// <summary>The Head of Household's picker of sixteen, a story waiting, at the largest text: on 4:3 and on 16:9.</summary>
        private const int PickerOfSixteenLargestPages = 2, PickerOfSixteenLargestPagesWide = 2;
        /// <summary>The picker of sixteen with nothing waiting, and its backdoor plan, at the largest text: on 4:3 and on 16:9.</summary>
        private const int BarePickerOfSixteenLargestPages = 2, BarePickerOfSixteenLargestPagesWide = 2;
        private const int BackdoorOfSixteenLargestPages = 2, BackdoorOfSixteenLargestPagesWide = 2;
        /// <summary>A story's WHO? of the fifteen others in a house of sixteen at the largest text, on 4:3.</summary>
        private const int WhoOfSixteenLargestPages = 2;

        /// <summary>
        /// The batch run photographs every page of the step open now on the 16:9 frame and on a 4:3
        /// one, each as the page the suite names for that frame, so the pages can be read as a
        /// player sees them.
        /// </summary>
        private IEnumerator CaptureLargestPages(string name, int wide, int square, string where)
        {
            if (!Application.isBatchMode) yield break;
            foreach (var frame in new[] { (Width: 1600, Height: 900, Name: "16x9", Pages: wide), (Width: 1200, Height: 900, Name: "4x3", Pages: square) })
            {
                for (int page = 1; page <= frame.Pages; page++)
                {
                    int turns = page - 1, shown = page, pages = frame.Pages;
                    yield return CaptureFraming(name + "-largest-" + frame.Name + "-p" + page, settle: false, width: frame.Width, height: frame.Height,
                        arrange: () =>
                        {
                            ShowFirstPage();
                            for (int turn = 0; turn < turns; turn++) LivePageControl(EpisodeHud.NextPageCaption).onClick.Invoke();
                            Canvas.ForceUpdateCanvases();
                        },
                        inspect: picture => Assert.That(LargestPageIndicator(), Is.EqualTo((shown, pages)),
                            where + " on the " + frame.Name + " frame: page " + shown + " of the suite's " + pages + "."));
                }
            }
        }

        /// <summary>
        /// The ceremony of sixteen at the largest text: it opens on the first of its pages with the
        /// keyboard on the way on, never on the pager; a press where "Next page" stands turns the
        /// page and the keyboard stays on the pager; Enter turns back and the pad's A on again, each
        /// committing nothing; the ring walks the second page, the pager and the footer; a rebuild of
        /// the open panel keeps the page, and the panel opened again starts on the first.
        /// </summary>
        [UnityTest]
        public IEnumerator NominationScreen_AtTheLargestTextAStepPagesByMouseKeyboardAndPad()
        {
            yield return InstallNomination(16, false, NominationBlock.Open);
            int pages = CeremonyOfSixteenLargestPages;
            yield return ApplyTextSize(TextSize.Largest);
            yield return OpenStation();
            yield return null; yield return null;
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

            // The pad: A on the way on.
            yield return PadSubmitCaption(EpisodeHud.NextPageCaption);
            Assert.That(LargestPageIndicator(), Is.EqualTo((2, pages)), where + ": the pad's A on '" + EpisodeHud.NextPageCaption + "' turns the page.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Still nothing committed.");
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

            yield return CaptureLargestPages("nomination-ceremony-16", CeremonyOfSixteenLargestPagesWide, pages, where);
            director.ClosePanels();
            yield return ApplyTextSize(false);
        }

        /// <summary>
        /// The Head of Household's picker of sixteen at the largest text, its cards dealt onto pages:
        /// a pick on one page and a pick on another are both the picks - lit wherever their cards
        /// are, named in the selection, kept across the turns - and committing names those two. The
        /// comparison takes the grid's place without a render, as one view: the step's head and the
        /// records, no pager, no scroll; hidden, the page is back with its pager. The backdoor plan
        /// behind its door pages too, and the way back starts the picker on its first page.
        /// </summary>
        [UnityTest]
        public IEnumerator NominationScreen_AtTheLargestTextPicksOnAnyPageAreThePicks()
        {
            yield return InstallNomination(16, true, NominationBlock.Open);
            yield return ApplyTextSize(TextSize.Largest);
            yield return OpenStation();
            yield return null; yield return null;
            string where = "The picker in a house of sixteen " + TextScales.At(TextSize.Largest);
            int pages = BarePickerOfSixteenLargestPages;
            var candidates = EpisodeEngine.NominationCandidates(director.Snapshot).ToList();
            int revision = director.Snapshot.revision;
            Assert.That(LargestPageIndicator(), Is.EqualTo((1, pages)), where + " opens on the first of " + pages + " pages.");
            var first = candidates[0];
            var last = candidates[candidates.Count - 1];
            Assert.That(FindCandidateCard(first.name), Is.Not.Null, where + ": the first candidate is on the first page.");
            Assert.That(FindCandidateCard(last.name), Is.Null, where + ": the last is not.");

            // A pick on the first page, then one on the last.
            ButtonWithCaption(first.name).onClick.Invoke();
            for (int turns = 0; FindCandidateCard(last.name) == null; turns++)
            {
                Assert.That(turns < pages, Is.True, where + ": no page shows " + last.name + ".");
                LivePageControl(EpisodeHud.NextPageCaption).onClick.Invoke();
                Canvas.ForceUpdateCanvases();
            }
            ButtonWithCaption(last.name).onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            Assert.That(Picked(last.name), Is.True, where + ": the card just picked is lit, on its own page.");
            ShowFirstPage();
            Assert.That(Picked(first.name), Is.True, where + ": the first pick is still lit after the turns.");
            Assert.That(ActiveRect(EpisodeHud.SelectedNomineesName).GetComponent<TMP_Text>().text, Is.EqualTo("Selected: " + first.name + " and " + last.name),
                where + ": a pick on each page, both named at the head of the step.");
            for (int turns = 0; FindCandidateCard(last.name) == null; turns++)
            {
                Assert.That(turns < pages, Is.True, where + ": no page shows " + last.name + ".");
                LivePageControl(EpisodeHud.NextPageCaption).onClick.Invoke();
                Canvas.ForceUpdateCanvases();
            }
            Assert.That(Picked(last.name), Is.True, where + ": the second pick is still lit, turned back to.");
            ShowFirstPage();
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), where + ": picking and turning commit nothing.");

            // The comparison is one view, in the grid's place: no pager, no scroll.
            ButtonWithCaption(EpisodeHud.ShowCandidateContextCaption).onClick.Invoke();
            AssertNominationScreen("The comparison " + TextScales.At(TextSize.Largest), "Commit nominations", EpisodeHud.HideCandidateContextCaption);
            Assert.That(ActiveRect(EpisodeHud.CandidateContextName), Is.Not.Null, "The comparison is up,");
            Assert.That(ActiveRect(EpisodeHud.NomineeGridName), Is.Null, "in the grid's place,");
            Assert.That(LargestPagerNow(), Is.Null, "as one view, with no pager.");
            ButtonWithCaption(EpisodeHud.HideCandidateContextCaption).onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            Assert.That(LargestPageIndicator(), Is.EqualTo((1, pages)), where + ": hidden again, the page is back with its pager.");
            AssertNominationScreen(where + ", the comparison hidden", "Commit nominations", EpisodeHud.ShowCandidateContextCaption);
            yield return null;
            yield return CaptureLargestPages("nomination-picker-16", BarePickerOfSixteenLargestPagesWide, pages, where);

            // The backdoor plan pages too, from its own first page though its door is on the
            // picker's last; and the way back from its last page is the picker's first.
            ShowFirstPage();
            ShowPageWith(EpisodeHud.PlanBackdoorRowName);
            Assert.That(LargestPageNow(), Is.EqualTo(pages), where + ": the door to the backdoor plan is on the picker's last page.");
            ButtonWithCaption(EpisodeHud.PlanBackdoorCaption).onClick.Invoke();
            yield return null;
            string plan = "The backdoor plan in a house of sixteen " + TextScales.At(TextSize.Largest);
            Assert.That(LargestPageIndicator(), Is.EqualTo((1, BackdoorOfSixteenLargestPages)), plan + " opens on its own first page.");
            var aims = new List<string>();
            AssertLargestPages(plan, BackdoorOfSixteenLargestPages, page =>
            {
                AssertNominationScreen(plan + ", page " + page, EpisodeHud.BackToNomineesCaption);
                aims.AddRange(ActiveRect(EpisodeHud.BackdoorAimsName).GetComponentsInChildren<Button>().Select(aim => aim.name));
            });
            Assert.That(aims, Is.EquivalentTo(candidates.Select(candidate => "Aim this week at " + candidate.name)), plan + ": every aim on exactly one page.");
            yield return CaptureLargestPages("nomination-backdoor-16", BackdoorOfSixteenLargestPagesWide, BackdoorOfSixteenLargestPages, plan);
            ShowFirstPage();
            LivePageControl(EpisodeHud.NextPageCaption).onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            Assert.That(LargestPageNow(), Is.EqualTo(BackdoorOfSixteenLargestPages), plan + ": turned to its last page.");
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
    }
}
