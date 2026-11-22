using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The tour offered, not replayed, and its card docked beside what each step lights, on the
    /// built card. The arithmetic is checked step by step in the Unity-free suite (TourDockTests);
    /// these two hold what only a built card shows: the offer's real controls and the keyboard on
    /// its yes, and the real text's card clear of its chrome with its title on one line at every
    /// text size on a 16:9 and a 16:10 window.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// A first offer is the tour itself; once it has been skipped, the offer asks "Take the
        /// tour?" with "Take the tour" (the keyboard on it) and "Not now"; the yes starts the tour
        /// at its first step, the no closes the offer.
        /// </summary>
        [UnityTest]
        public IEnumerator TourOffer_AReturningPlayerIsAskedFirst()
        {
            var tour = HouseTutorial.Attach(director.gameObject);
            tour.RememberCompletion = false;
            tour.ReducedMotion = true;
            try
            {
                tour.Offer(TourChrome);
                yield return TourFrames(2);
                Assert.That(tour.IsOffering, Is.False, "A first-time player gets the tour itself.");
                Assert.That(tour.StepIndex, Is.EqualTo(0));
                Assert.That(TourButton(tour, HouseTutorial.NextCaption), Is.Not.Null);
                tour.Skip();
                yield return null;

                tour.Offer(TourChrome);
                yield return TourFrames(2);
                Assert.That(tour.IsShowing, Is.True, "The offer is up.");
                Assert.That(tour.IsOffering, Is.True);
                Assert.That(tour.StepIndex, Is.EqualTo(-1), "No step of the tour yet.");
                Assert.That(TourText(tour, "Title"), Is.EqualTo(TourOfferWords.OfferTitle));
                var take = TourButton(tour, HouseTutorial.TakeCaption);
                Assert.That(take, Is.Not.Null, "\"Take the tour\" is on the offer.");
                Assert.That(TourButton(tour, HouseTutorial.NotNowCaption), Is.Not.Null, "\"Not now\" is on the offer.");
                Assert.That(TourButton(tour, HouseTutorial.NextCaption), Is.Null, "The tour's own controls wait for the yes.");
                Assert.That(TourButton(tour, HouseTutorial.SkipCaption), Is.Null);
                Assert.That(EventSystem.current, Is.Not.Null, "The fixture has an event system.");
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(take.gameObject), "The keyboard and the pad start on the yes.");
                var card = TourPart(tour, HouseTutorial.CardName);
                var titleLabel = tour.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == "Title");
                Assert.That(titleLabel.rectTransform.rect.height, Is.GreaterThanOrEqualTo(titleLabel.fontSize * 1.3f - .5f));
                Assert.That(card.rect.width, Is.GreaterThan(0f));

                take.onClick.Invoke();
                yield return TourFrames(2);
                Assert.That(tour.IsOffering, Is.False);
                Assert.That(tour.StepIndex, Is.EqualTo(0), "The yes starts the tour at its first step.");
                Assert.That(TourButton(tour, HouseTutorial.NextCaption), Is.Not.Null);
                Assert.That(TourButton(tour, HouseTutorial.TakeCaption), Is.Null, "The offer's answers go with it.");
                tour.Skip();
                yield return null;

                tour.Offer(TourChrome);
                yield return TourFrames(2);
                TourButton(tour, HouseTutorial.NotNowCaption).onClick.Invoke();
                yield return null;
                Assert.That(tour.IsShowing, Is.False, "Not now closes the offer.");
                Assert.That(tour.HasSeen, Is.True);
            }
            finally
            {
                if (tour.IsShowing) tour.Skip();
                Object.Destroy(tour.gameObject);
            }
            yield return null;
        }

        /// <summary>
        /// On a 1920x1080 and a 1920x1200 window, at the standard, larger and largest text: every
        /// step's card is on screen, covers none of what glows, says it is docked, and draws its
        /// title on one line.
        /// </summary>
        [UnityTest]
        public IEnumerator TourOffer_TheCardDocksBesideEachStepAtEveryTextSizeOn16x9And16x10()
        {
            foreach (var (wide, high) in new[] { (1920, 1080), (1920, 1200) })
                foreach (float scale in new[] { 1f, 1.2f, 1.4f })
                {
                    var tour = HouseTutorial.Attach(director.gameObject);
                    tour.RememberCompletion = false;
                    tour.ReducedMotion = true;
                    tour.FontScale = scale;
                    EmulateTourScreen(tour.GetComponent<Canvas>(), wide, high);
                    var hud = EmulatedTourHud(wide, high);
                    RectTransform Chrome(string name) => hud.GetComponentsInChildren<RectTransform>(true)
                        .FirstOrDefault(rect => rect.name == name && rect.gameObject.activeInHierarchy);
                    try
                    {
                        tour.Show(Chrome);
                        yield return TourFrames(2);
                        var screen = new Rect(0f, 0f, wide, high);
                        for (int i = 0; i < tour.Count; i++)
                        {
                            string where = wide + "x" + high + " x" + scale + ", " + tour.StepTitles[i];
                            var card = TourWorldRect(TourPart(tour, HouseTutorial.CardName));
                            Assert.That(TourEncloses(screen, card), Is.True, where + ": the card " + card + " is on screen.");
                            Assert.That(tour.CardDocked, Is.True, where + ": the card found a side.");
                            if (tour.CurrentAnchors.Count > 0)
                            {
                                var lit = TourUnion(tour.CurrentAnchors.Select(Chrome).Select(TourWorldRect));
                                Assert.That(TourOverlap(card, lit), Is.LessThan(1f), where + ": the card " + card + " covers none of " + lit + ".");
                                Assert.That(TourOverlap(card, TourWorldRect(TourPart(tour, HouseTutorial.RingName))), Is.LessThan(1f),
                                    where + ": nor the ring round it.");
                            }
                            var title = tour.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == "Title");
                            title.ForceMeshUpdate();
                            Assert.That(title.textInfo.lineCount, Is.EqualTo(1), where + ": the title \"" + title.text + "\" is on one line.");

                            if (i < tour.Count - 1)
                            {
                                TourButton(tour, HouseTutorial.NextCaption).onClick.Invoke();
                                yield return null;
                            }
                        }
                    }
                    finally
                    {
                        tour.Skip();
                        Object.Destroy(tour.gameObject);
                        Object.Destroy(hud.gameObject);
                    }
                    yield return null;
                }
        }
    }
}
