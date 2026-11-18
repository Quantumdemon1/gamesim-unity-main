using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Menu slice 2, part b - the front door: the cast screen's one main button and its one way
    /// back, the picked card's look against the focus ring, and the tour's step back. The words are
    /// pinned without Unity in MenuOneWayBackFrontWordsTests; what only the built screen shows is here.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The buttons on the cast screen worded as a way back, live and pressable.</summary>
        private Button[] CastWaysBack() =>
            CastScreen().GetComponentsInChildren<Button>()
                .Where(button => button.IsActive() && button.IsInteractable() && WayOut.IsWayBack(WayOut.CaptionOf(button))).ToArray();

        /// <summary>The bright action-blue buttons on the cast screen that start a season or play as somebody.</summary>
        private Button[] CastStarts() =>
            CastScreen().GetComponentsInChildren<Button>()
                .Where(button => button.IsActive() && button.GetComponent<Image>() != null && button.GetComponent<Image>().color == UiTheme.ActionBlue
                    && button.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text.StartsWith(FrontDoorWords.StartAsPrefix)
                        || label.text.StartsWith("Play as ") || label.text == "Start this season")).ToArray();

        /// <summary>
        /// One main button (the review's change 3): before a pick, the start reads "Start as a
        /// newcomer" and stands alone; after one, it names the pick, and the details carry no second
        /// bright button - only "Edit &lt;name&gt;", in quiet glass. "Play as" and "Customize" are gone.
        /// </summary>
        [UnityTest]
        public IEnumerator MenuOneWayBackFront_TheCastScreenHasOneBrightStartThatNamesThePick()
        {
            yield return OpenCastScreen();
            var starts = CastStarts();
            Assert.That(starts, Has.Length.EqualTo(1), "One start with nobody picked.");
            Assert.That(WayOut.CaptionOf(starts[0]), Is.EqualTo(FrontDoorWords.StartAsNewcomer));
            Assert.That(starts[0].name, Is.EqualTo(CastSelect.StartObjectName));
            Assert.That(starts[0].IsInteractable(), Is.True, "The newcomer start is still there to press, as Start this season was.");

            var emma = CastTemplates.Find("emma-brown");
            CastButtons(emma.Name)[0].onClick.Invoke();
            yield return null;
            starts = CastStarts();
            Assert.That(starts, Has.Length.EqualTo(1), "Still one bright start after a pick: the details' Play as is gone.");
            Assert.That(WayOut.CaptionOf(starts[0]), Is.EqualTo(FrontDoorWords.StartAs("Emma")), "and it names her.");
            var details = ActiveCastRect(CastSelect.DetailPanelName);
            Assert.That(details.GetComponentsInChildren<Button>().Count(button => button.GetComponent<Image>().color == UiTheme.ActionBlue), Is.Zero,
                "No bright button in the details.");
            var edit = CastButtons(FrontDoorWords.Edit("Emma"));
            Assert.That(edit, Has.Length.EqualTo(1), "Edit Emma, the one name for changing a houseguest.");
            Assert.That(edit[0].GetComponent<Image>().color, Is.Not.EqualTo(UiTheme.ActionBlue));
            Assert.That(CastScreen().GetComponentsInChildren<TMP_Text>(true).Where(label => label.gameObject.activeInHierarchy)
                .Any(label => label.text.StartsWith("Play as ") || label.text.StartsWith("Customize ")), Is.False, "No Play as, no Customize.");
            CastScreen().Dismiss();
        }

        /// <summary>
        /// The one way back (the review's change 1): top left, drawn by the shared helper, named for
        /// where it lands and what it keeps - Settings here - with the old cancel and its words gone
        /// from the footer, and pressing it does what the cancel did.
        /// </summary>
        [UnityTest]
        public IEnumerator MenuOneWayBackFront_TheCastScreenHasOneWayBackAtItsTopLeft()
        {
            var before = director.Snapshot;
            yield return OpenCastScreen();
            Canvas.ForceUpdateCanvases();
            var ways = CastWaysBack();
            Assert.That(ways, Has.Length.EqualTo(1), "Exactly one way back.");
            var back = ways[0];
            Assert.That(back.name, Is.EqualTo(WayOut.BackObjectName), "Drawn by the shared helper.");
            Assert.That(WayOut.CaptionOf(back), Is.EqualTo("Back to Settings (season kept)"), "It names where it lands and what it keeps.");
            var rect = (RectTransform)back.transform;
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)), "Anchored to the screen's top left.");
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(rect.parent.name, Is.EqualTo("Scrim"), "On the screen itself, not inside the footer.");
            Assert.That(CastScreen().GetComponentsInChildren<TMP_Text>(true).Where(label => label.gameObject.activeInHierarchy)
                .Any(label => label.text == "Cancel — keep this season" || label.text == "Back to the menu"), Is.False, "The old cancel and its words are gone.");

            back.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.False, "The way back closes the screen,");
            Assert.That(director.IsPanelOpen, Is.True, "onto the Settings it names.");
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(before.sessionId), "and the season is kept, as it says.");
        }

        /// <summary>
        /// The way back keeps the corner on every frame the screen is laid out for: the title shrinks
        /// into the room right of it where the centred title would run under it (the largest text on
        /// 16:9), and the brand stands below it.
        /// </summary>
        [UnityTest]
        public IEnumerator MenuOneWayBackFront_TheTitleAndTheBrandStayClearOfTheWayBack()
        {
            yield return OpenCastScreen();
            var canvas = CastScreen().GetComponent<Canvas>();
            try
            {
                foreach (var frame in new[] { (wide: 2560, high: 1080, text: 1f), (wide: 1920, high: 1080, text: 1f), (wide: 1920, high: 1080, text: 1.4f), (wide: 1440, high: 1080, text: 1.4f) })
                {
                    string shape = frame.wide + "x" + frame.high + " at " + frame.text;
                    yield return LayCastScreenOutAt(frame.wide, frame.high, frame.text);
                    var back = WorldRect((RectTransform)CastWaysBack().Single().transform);
                    var title = CastScreen().GetComponentsInChildren<TMP_Text>().Single(label => label.name == CastSelect.TitleName);
                    title.ForceMeshUpdate(true);
                    Assert.That(title.isTextOverflowing, Is.False, "The whole title is drawn on " + shape + ".");
                    Assert.That(title.fontSize, Is.GreaterThanOrEqualTo(24f - .01f), "and no smaller than 24 on " + shape + ".");
                    var bounds = title.textBounds;
                    Vector3 low = title.rectTransform.TransformPoint(bounds.min), high = title.rectTransform.TransformPoint(bounds.max);
                    var words = Rect.MinMaxRect(low.x, low.y, high.x, high.y);
                    Assert.That(words.Overlaps(back), Is.False, "The title's words " + words + " stay clear of the way back " + back + " on " + shape + ".");
                    var brand = CastScreen().GetComponentsInChildren<RectTransform>().FirstOrDefault(item => item.name == "Brand");
                    if (brand != null)
                        Assert.That(WorldRect(brand).Overlaps(back), Is.False, "The brand stands below the way back on " + shape + ".");
                }
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            CastScreen().Dismiss();
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        /// <summary>
        /// One selected look, never the focus look: a picked card is gold - its glow, its edge and
        /// the PLAYING AS word - and the theme's blue is the focus ring's alone, lit only on the card
        /// the keyboard or the pad is on.
        /// </summary>
        [UnityTest]
        public IEnumerator MenuOneWayBackFront_APickedCardIsGoldAndTheFocusRingIsBlue()
        {
            yield return OpenCastScreen();
            var events = EventSystem.current;
            Assert.That(events, Is.Not.Null);
            var emma = CastTemplates.Find("emma-brown");
            var casey = CastTemplates.Find("casey-wilson");
            CastButtons(emma.Name)[0].onClick.Invoke();
            yield return null;

            RectTransform Card(string name) => CastScreen().GetComponentsInChildren<RectTransform>().Single(item => item.name == name && item.GetComponent<Button>() != null);
            Image Ring(string name) => Card(name).Find(CastSelect.CardFocusRingName).GetComponent<Image>();
            var picked = Card(emma.Name);
            var glow = picked.Find(CastSelect.CardGlowName).GetComponent<Image>();
            Assert.That(new Vector3(glow.color.r, glow.color.g, glow.color.b), Is.Not.EqualTo(new Vector3(UiTheme.Glow.r, UiTheme.Glow.g, UiTheme.Glow.b)),
                "The pick's glow is not the focus blue.");
            Assert.That(picked.GetComponentsInChildren<TMP_Text>().Any(label => label.text == "PLAYING AS"), Is.True, "The pick says so in a word.");
            Assert.That(Ring(emma.Name).color, Is.EqualTo(UiTheme.Glow), "The focus ring is the theme's blue.");

            events.SetSelectedGameObject(Card(casey.Name).gameObject);
            yield return null;
            Assert.That(Ring(casey.Name).enabled, Is.True, "The focused card shows the ring.");
            Assert.That(Ring(emma.Name).enabled, Is.False, "The picked card, not focused, does not.");
            Assert.That(Card(casey.Name).GetComponentsInChildren<TMP_Text>().Any(label => label.text == "PLAYING AS"), Is.False,
                "Focus is not a pick.");
            events.SetSelectedGameObject(Card(emma.Name).gameObject);
            yield return null;
            Assert.That(Ring(emma.Name).enabled, Is.True);
            Assert.That(Ring(casey.Name).enabled, Is.False, "One ring at a time.");
            CastScreen().Dismiss();
        }

        /// <summary>
        /// The way back names where it really lands from each door: the finale's page (its New season,
        /// or the report over it) and the house with nothing open, besides Settings and the main menu.
        /// </summary>
        [UnityTest]
        public IEnumerator MenuOneWayBackFront_TheWayBackNamesTheHouseWhenNothingWasOpen()
        {
            director.ClosePanels();
            yield return null;
            Assert.That(director.IsPanelOpen, Is.False);
            director.NewSeason();
            yield return null;
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.True);
            var back = CastWaysBack().Single();
            Assert.That(WayOut.CaptionOf(back), Is.EqualTo("Back to the house (season kept)"));
            back.onClick.Invoke();
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.False);
            Assert.That(director.IsPanelOpen, Is.False, "It lands on the house, as it says.");
        }

        /// <summary>The tour's step back reads "Previous": a bare "Back" is folded away, and it is no way out of the tour.</summary>
        [UnityTest]
        public IEnumerator MenuOneWayBackFront_TheToursStepBackReadsPrevious()
        {
            var tour = DirectorTour();
            tour.Show(TourChrome);
            yield return TourFrames(2);
            try
            {
                TourButton(tour, HouseTutorial.NextCaption).onClick.Invoke();
                yield return null;
                Assert.That(TourButton(tour, "Previous"), Is.Not.Null, "The step back reads Previous.");
                Assert.That(tour.GetComponentsInChildren<Button>().Where(button => button.IsActive())
                    .Any(button => button.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text == "Back")), Is.False, "No bare Back.");
                Assert.That(TourButton(tour, HouseTutorial.SkipCaption), Is.Not.Null, "Skip the tour stays its way out.");
            }
            finally
            {
                tour.Skip();
            }
        }
    }
}
