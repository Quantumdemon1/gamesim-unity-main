using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The first-run tour, driven directly: the reference build's seven steps in this HUD's words,
    /// the dim with its hole round each step's chrome, the captions, the step sound and the way out.
    ///
    /// <para>The director never shows the tour in batchmode - it is the one overlay that waits for a
    /// click - so these call <see cref="HouseTutorial.Show"/> with the director's own kind of lookup,
    /// the way the walkthrough photographs it. What is asserted is state and geometry, never pixels.</para>
    ///
    /// <para>The hole is measured at two window sizes that the batchmode screen is not. A window
    /// cannot be resized from a PlayMode test, so the tour's canvas and a stand-in for the HUD are
    /// taken into world space and given exactly the transform an overlay canvas gets on that
    /// window: centred on it, scaled by the scaler's factor, and that factor's fraction of it in
    /// size. World units are then that window's pixels, which is the space the tour converts
    /// through. The first size is the one the old arithmetic happened to be right at; the second is
    /// the one it was 160 units out at.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static readonly string[] TourTitles =
        {
            "Welcome to Gamesim: The House!", "Top Bar", "Side Rail", "The Episode Screen", "Cast Strip", "Phase Panels", "You're Ready!",
        };

        private HouseTutorial DirectorTour() =>
            director.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseTutorial>(true)).Single();

        /// <summary>The director's own lookup: a panel anywhere in the scene, by name, switched on.</summary>
        private RectTransform TourChrome(string name) =>
            director.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                .FirstOrDefault(rect => rect.name == name && rect.gameObject.activeInHierarchy);

        /// <summary>A live control on the tour, by the caption on it - the contract, not the object's name.</summary>
        private static Button TourButton(HouseTutorial tour, string caption) =>
            tour.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.IsActive() && button.IsInteractable()
                    && button.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text == caption));

        private static string TourText(HouseTutorial tour, string name) =>
            tour.GetComponentsInChildren<TMP_Text>(true).First(label => label.name == name).text;

        private static RectTransform TourPart(HouseTutorial tour, string name) =>
            tour.GetComponentsInChildren<RectTransform>(true).First(rect => rect.name == name);

        private static Rect TourWorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static Rect TourUnion(IEnumerable<Rect> rects) =>
            rects.Aggregate((a, b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax)));

        private static Rect TourInflate(Rect rect, float by) =>
            Rect.MinMaxRect(rect.xMin - by, rect.yMin - by, rect.xMax + by, rect.yMax + by);

        /// <summary>Whether <paramref name="inner"/> lies within <paramref name="outer"/>, to a unit.</summary>
        private static bool TourEncloses(Rect outer, Rect inner) =>
            inner.xMin >= outer.xMin - 1f && inner.yMin >= outer.yMin - 1f && inner.xMax <= outer.xMax + 1f && inner.yMax <= outer.yMax + 1f;

        private static float TourArea(Rect rect) => Mathf.Max(0f, rect.width) * Mathf.Max(0f, rect.height);

        private static float TourOverlap(Rect a, Rect b) =>
            Mathf.Max(0f, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin))
            * Mathf.Max(0f, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));

        private static IEnumerator TourFrames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private static IEnumerator TourRealSeconds(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        /// <summary>The factor the scaler every overlay here uses (1600x900, width and height matched equally) gives a window.</summary>
        private static float TourScale(int wide, int high) => Mathf.Sqrt(wide / 1600f * (high / 900f));

        /// <summary>
        /// Gives a canvas the transform an overlay canvas has on a <paramref name="wide"/> by
        /// <paramref name="high"/> window, in world space where nothing drives it back: centred on
        /// the window, scaled by the scaler's factor, and the window divided by it in size.
        /// </summary>
        private static void EmulateTourScreen(Canvas canvas, int wide, int high)
        {
            float k = TourScale(wide, high);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = null;
            var rect = (RectTransform)canvas.transform;
            rect.pivot = new Vector2(.5f, .5f);
            rect.position = new Vector3(wide * .5f, high * .5f, 0f);
            rect.rotation = Quaternion.identity;
            rect.localScale = new Vector3(k, k, 1f);
            rect.sizeDelta = new Vector2(wide / k, high / k);
        }

        /// <summary>
        /// A stand-in for the HUD on an emulated window: the six pieces of chrome the tour lights,
        /// where the HUD draws them in its 1600x900 units - the week chip and house pill on the top
        /// bar, the left rail's ground and its "Go to episode screen" row, the cast strip along the
        /// floor and the status line over it.
        /// </summary>
        private RectTransform EmulatedTourHud(int wide, int high)
        {
            var root = new GameObject("Emulated HUD", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, director.gameObject.scene);
            EmulateTourScreen(root.GetComponent<Canvas>(), wide, high);
            var frame = (RectTransform)root.transform;

            RectTransform Part(string name, Vector2 anchor, Vector2 position, Vector2 size)
            {
                var part = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                part.SetParent(frame, false);
                part.anchorMin = anchor; part.anchorMax = anchor; part.pivot = anchor;
                part.anchoredPosition = position;
                part.sizeDelta = size;
                return part;
            }

            var topLeft = new Vector2(0f, 1f);
            Part("Week chip", topLeft, new Vector2(270f, -14f), new Vector2(280f, 52f));
            Part("House pill", topLeft, new Vector2(894f, -14f), new Vector2(360f, 52f));
            Part("Rail ground", topLeft, new Vector2(8f, -74f), new Vector2(212f, 560f));
            Part("Go to episode screen", topLeft, new Vector2(20f, -420f), new Vector2(188f, 40f));
            Part("Status", new Vector2(.5f, 0f), new Vector2(0f, 124f), new Vector2(620f, 50f));
            var strip = new GameObject(CastRail.StripName, typeof(RectTransform)).GetComponent<RectTransform>();
            strip.SetParent(frame, false);
            strip.anchorMin = Vector2.zero; strip.anchorMax = new Vector2(1f, 0f); strip.pivot = new Vector2(.5f, 0f);
            strip.offsetMin = new Vector2(8f, 8f); strip.offsetMax = new Vector2(-8f, 116f);
            return frame;
        }

        /// <summary>
        /// The reference build's seven steps, in its order, with this HUD's titles; the controls line
        /// on the first card; "Skip Tutorial" on every card, "Next" on all but the last and "Let's
        /// Go!" on the last; and at the batchmode window, each lit step's ring round the real HUD
        /// chrome it names.
        /// </summary>
        [UnityTest]
        public IEnumerator Tour_TheReferenceStepsInThisHudsWords()
        {
            var tour = DirectorTour();
            tour.Show(TourChrome);
            yield return TourFrames(2);
            Assert.That(tour.StepTitles, Is.EqualTo(TourTitles),
                "The reference build's order, every step pointed at a piece of the standard HUD that is on screen.");
            Assert.That(TourText(tour, "Body"), Does.Contain(
                "Click the floor to walk. Press E near a houseguest to talk. Esc closes this tour."));

            for (int i = 0; i < TourTitles.Length; i++)
            {
                string step = TourTitles[i];
                bool last = i == TourTitles.Length - 1;
                Assert.That(tour.StepIndex, Is.EqualTo(i), step);
                Assert.That(TourText(tour, "Title"), Is.EqualTo(step));
                Assert.That(TourText(tour, "Counter"), Is.EqualTo((i + 1) + " of " + TourTitles.Length), step);
                Assert.That(TourButton(tour, "Skip Tutorial"), Is.Not.Null, step + ": the skip is on every card.");
                Assert.That(TourButton(tour, last ? "Let's Go!" : "Next"), Is.Not.Null, step);
                Assert.That(TourButton(tour, last ? "Next" : "Let's Go!"), Is.Null, step);

                var ring = TourPart(tour, HouseTutorial.RingName);
                if (tour.CurrentAnchors.Count == 0)
                    Assert.That(ring.gameObject.activeInHierarchy, Is.False, step + " is a centred card over a plain dim.");
                else
                {
                    Assert.That(ring.gameObject.activeInHierarchy, Is.True, step + " lights its chrome.");
                    var lit = TourUnion(tour.CurrentAnchors.Select(TourChrome).Where(rect => rect != null).Select(TourWorldRect));
                    Assert.That(TourEncloses(TourWorldRect(ring), lit), Is.True,
                        step + ": the ring " + TourWorldRect(ring) + " surrounds " + lit + " on the real HUD.");
                }

                if (!last)
                {
                    TourButton(tour, "Next").onClick.Invoke();
                    yield return null;
                }
            }
            Assert.That(TourText(tour, "Body"), Does.Not.Contain("Head of Household"),
                "The last card promises no competition: the introductions come next, and an import can be weeks in.");
            tour.Skip();
            yield return null;
        }

        /// <summary>
        /// Every lit step's hole contains its chrome and hugs it, the dim covers the rest of the frame
        /// and none of the chrome, and the card stands on screen clear of what it explains - on a
        /// 1600x900 window and a 1920x1080 one.
        /// </summary>
        [UnityTest]
        public IEnumerator Tour_TheCutoutSurroundsItsAnchorAt1600x900And1920x1080()
        {
            foreach (var (wide, high) in new[] { (1600, 900), (1920, 1080) })
            {
                var tour = HouseTutorial.Attach(director.gameObject);
                tour.RememberCompletion = false;
                tour.ReducedMotion = true;
                EmulateTourScreen(tour.GetComponent<Canvas>(), wide, high);
                var hud = EmulatedTourHud(wide, high);
                RectTransform Chrome(string name) => hud.GetComponentsInChildren<RectTransform>(true)
                    .FirstOrDefault(rect => rect.name == name && rect.gameObject.activeInHierarchy);
                try
                {
                    tour.Show(Chrome);
                    yield return TourFrames(2);
                    string shape = wide + "x" + high;
                    Assert.That(tour.Count, Is.EqualTo(7), shape + ": every piece of chrome is on the stand-in.");

                    var screen = new Rect(0f, 0f, wide, high);
                    float whole = wide * (float)high;
                    for (int i = 0; i < tour.Count; i++)
                    {
                        string where = shape + ", " + tour.StepTitles[i];
                        var ring = TourPart(tour, HouseTutorial.RingName);
                        var dims = HouseTutorial.DimNames.Select(name => TourWorldRect(TourPart(tour, name))).ToArray();
                        var card = TourWorldRect(TourPart(tour, HouseTutorial.CardName));
                        Assert.That(TourEncloses(screen, card), Is.True, where + ": the card " + card + " is on screen.");

                        if (tour.CurrentAnchors.Count == 0)
                        {
                            Assert.That(ring.gameObject.activeInHierarchy, Is.False, where);
                            Assert.That(dims.Sum(TourArea), Is.EqualTo(whole).Within(whole * .01f), where + ": the whole frame is dimmed.");
                        }
                        else
                        {
                            var lit = TourUnion(tour.CurrentAnchors.Select(Chrome).Select(TourWorldRect));
                            var hole = TourWorldRect(ring);
                            Assert.That(ring.gameObject.activeInHierarchy, Is.True, where);
                            Assert.That(TourEncloses(hole, lit), Is.True, where + ": the hole " + hole + " contains " + lit + ".");
                            Assert.That(TourEncloses(TourInflate(lit, 20f * TourScale(wide, high)), hole), Is.True,
                                where + ": the hole " + hole + " hugs " + lit + " rather than opening the frame.");
                            foreach (var dim in dims)
                                Assert.That(TourOverlap(dim, lit), Is.LessThan(1f), where + ": the dim " + dim + " covers none of " + lit + ".");
                            Assert.That(TourOverlap(card, lit), Is.LessThan(1f), where + ": the card " + card + " covers none of " + lit + ".");
                            var open = Rect.MinMaxRect(Mathf.Max(hole.xMin, 0f), Mathf.Max(hole.yMin, 0f),
                                Mathf.Min(hole.xMax, wide), Mathf.Min(hole.yMax, high));
                            Assert.That(dims.Sum(TourArea) + TourArea(open), Is.EqualTo(whole).Within(whole * .01f),
                                where + ": everything but the hole is dimmed.");
                        }

                        if (i < tour.Count - 1)
                        {
                            TourButton(tour, "Next").onClick.Invoke();
                            yield return null;
                        }
                    }
                }
                finally
                {
                    tour.Skip();
                    UnityEngine.Object.Destroy(tour.gameObject);
                    UnityEngine.Object.Destroy(hud.gameObject);
                }
                yield return null;
            }
        }

        /// <summary>
        /// A step whose chrome is missing, or there but switched off, is left out of the count and
        /// the walk, as the reference build leaves its desktop-only step out on a phone - including
        /// chrome that goes away after the tour has begun, which is dropped when its step comes up.
        /// </summary>
        [UnityTest]
        public IEnumerator Tour_AStepWhoseChromeIsMissingOrHiddenIsDropped()
        {
            var tour = DirectorTour();
            var hidden = new GameObject("Status", typeof(RectTransform)).GetComponent<RectTransform>();
            hidden.sizeDelta = new Vector2(620f, 50f);
            hidden.gameObject.SetActive(false);
            try
            {
                tour.Show(name => name == CastRail.StripName ? null : name == "Status" ? hidden : TourChrome(name));
                yield return TourFrames(2);
                var expected = new[] { "Welcome to Gamesim: The House!", "Top Bar", "Side Rail", "The Episode Screen", "You're Ready!" };
                Assert.That(tour.Count, Is.EqualTo(5), "No strip, and a status line that is switched off: two steps fewer.");
                Assert.That(tour.StepTitles, Is.EqualTo(expected));
                Assert.That(TourText(tour, "Counter"), Is.EqualTo("1 of 5"));
                for (int i = 1; i < expected.Length; i++)
                {
                    TourButton(tour, "Next").onClick.Invoke();
                    yield return null;
                    Assert.That(TourText(tour, "Title"), Is.EqualTo(expected[i]), "Step " + (i + 1) + " of 5.");
                }
                Assert.That(TourButton(tour, "Let's Go!"), Is.Not.Null, "The last kept step is the last step.");
                tour.Skip();
                yield return null;

                // Chrome that goes after the tour has begun: all seven at the start, and the status
                // line gone by the time its step comes up.
                bool statusGone = false;
                tour.Show(name => statusGone && name == "Status" ? null : TourChrome(name));
                yield return null;
                Assert.That(tour.Count, Is.EqualTo(7));
                while (tour.StepIndex < 4) { TourButton(tour, "Next").onClick.Invoke(); yield return null; }
                Assert.That(TourText(tour, "Title"), Is.EqualTo("Cast Strip"));
                statusGone = true;
                TourButton(tour, "Next").onClick.Invoke();
                yield return null;
                Assert.That(TourText(tour, "Title"), Is.EqualTo("You're Ready!"),
                    "Phase Panels is dropped, not drawn as a dim with nothing in its hole.");
                Assert.That(tour.Count, Is.EqualTo(6));
                Assert.That(TourText(tour, "Counter"), Is.EqualTo("6 of 6"));
                tour.Skip();
                yield return null;
            }
            finally
            {
                if (tour.IsShowing) tour.Skip();
                UnityEngine.Object.Destroy(hidden.gameObject);
            }
        }

        /// <summary>The step sound plays once for every press of Next and never for Skip Tutorial.</summary>
        [UnityTest]
        public IEnumerator Tour_StepSoundPlaysOnEveryNextAndNeverOnTheSkip()
        {
            var tour = DirectorTour();
            int sounds = 0;
            tour.StepSound = () => sounds++;
            tour.Show(TourChrome);
            yield return null;
            Assert.That(sounds, Is.Zero, "Showing the tour is not a step.");

            TourButton(tour, "Next").onClick.Invoke();
            Assert.That(sounds, Is.EqualTo(1));
            Assert.That(tour.StepIndex, Is.EqualTo(1));
            yield return null;
            TourButton(tour, "Next").onClick.Invoke();
            Assert.That(sounds, Is.EqualTo(2));
            yield return null;

            TourButton(tour, "Skip Tutorial").onClick.Invoke();
            Assert.That(tour.IsShowing, Is.False, "Skip Tutorial leaves the tour.");
            Assert.That(sounds, Is.EqualTo(2), "Skip Tutorial makes no step sound.");
            yield return null;
            tour.StepSound = null;
        }

        /// <summary>
        /// The last step's control says "Let's Go!" - still the control the keyboard lands on - and
        /// pressing it ends the tour, remembered for this isolated session and nowhere else.
        /// </summary>
        [UnityTest]
        public IEnumerator Tour_LetsGoOnTheLastStepFinishesTheTour()
        {
            var tour = DirectorTour();
            bool hadKey = PlayerPrefs.HasKey(HouseTutorial.SeenKey);
            int previous = PlayerPrefs.GetInt(HouseTutorial.SeenKey, -1);
            tour.Show(TourChrome);
            yield return null;
            int steps = tour.Count;
            for (int i = 1; i < steps; i++)
            {
                TourButton(tour, "Next").onClick.Invoke();
                yield return null;
            }
            Assert.That(tour.StepIndex, Is.EqualTo(steps - 1));
            Assert.That(TourButton(tour, "Next"), Is.Null, "The last step does not say Next.");
            var go = TourButton(tour, "Let's Go!");
            Assert.That(go, Is.Not.Null);
            Assert.That(go.name, Is.EqualTo("Next"), "The same control, which the keyboard lands on.");

            go.onClick.Invoke();
            Assert.That(tour.IsShowing, Is.False);
            Assert.That(tour.HasSeen, Is.True);
            Assert.That(tour.gameObject.activeSelf, Is.False, "The tour is off the screen.");
            Assert.That(PlayerPrefs.HasKey(HouseTutorial.SeenKey), Is.EqualTo(hadKey));
            Assert.That(PlayerPrefs.GetInt(HouseTutorial.SeenKey, -1), Is.EqualTo(previous));
            yield return null;
        }

        /// <summary>
        /// The tour fades in from nothing with its card 20 units low, and is fully up and in place
        /// once the reference build's 0.3 s fade and 0.15 + 0.35 s rise are over; under reduced motion
        /// a step arrives whole on the press. (A later step keeps the dim: see the test after this.)
        /// </summary>
        [UnityTest]
        public IEnumerator Tour_EachStepFadesInWithItsCardRising_AllAtOnceUnderReducedMotion()
        {
            var tour = HouseTutorial.Attach(director.gameObject);
            tour.RememberCompletion = false;
            EmulateTourScreen(tour.GetComponent<Canvas>(), 1600, 900);
            try
            {
                tour.ReducedMotion = false;
                tour.Show(_ => null);
                var layer = TourPart(tour, HouseTutorial.StepLayerName).GetComponent<CanvasGroup>();
                var card = TourPart(tour, HouseTutorial.CardName);
                var cardGroup = card.GetComponent<CanvasGroup>();
                Assert.That(layer.alpha, Is.EqualTo(0f).Within(.001f), "The step fades in from nothing.");
                Assert.That(cardGroup.alpha, Is.EqualTo(0f).Within(.001f), "The card waits out its delay unseen.");
                float low = card.anchoredPosition.y;

                yield return TourRealSeconds(.7f);
                Assert.That(layer.alpha, Is.EqualTo(1f));
                Assert.That(cardGroup.alpha, Is.EqualTo(1f));
                Assert.That(card.anchoredPosition.y - low, Is.EqualTo(20f).Within(.5f), "The card rose 20 units into place.");

                tour.ReducedMotion = true;
                TourButton(tour, "Next").onClick.Invoke();
                Assert.That(layer.alpha, Is.EqualTo(1f), "Under reduced motion the step arrives whole.");
                Assert.That(cardGroup.alpha, Is.EqualTo(1f));
                float placed = card.anchoredPosition.y;
                yield return null;
                Assert.That(card.anchoredPosition.y, Is.EqualTo(placed).Within(.01f), "And in place: nothing rises after it.");
            }
            finally
            {
                if (tour.IsShowing) tour.Skip();
                UnityEngine.Object.Destroy(tour.gameObject);
            }
        }

        /// <summary>
        /// Next never lifts the dim: the new step's fade picks up from the dim as it stands, so the
        /// house does not jump to full brightness for a frame and darken again on every step - only
        /// the card waits out its delay and rises in again. Next is pressed once part-way into the
        /// first step's fade and once with the dim fully up, and the dim is sampled on the press and
        /// every frame for 0.4 s after each.
        /// </summary>
        [UnityTest]
        public IEnumerator Tour_NextNeverLiftsTheDim_OnlyTheCardRisesAgain()
        {
            var tour = HouseTutorial.Attach(director.gameObject);
            tour.RememberCompletion = false;
            tour.ReducedMotion = false;
            EmulateTourScreen(tour.GetComponent<Canvas>(), 1600, 900);
            var hud = EmulatedTourHud(1600, 900);
            RectTransform Chrome(string name) => hud.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => rect.name == name && rect.gameObject.activeInHierarchy);
            try
            {
                tour.Show(Chrome);
                var layer = TourPart(tour, HouseTutorial.StepLayerName).GetComponent<CanvasGroup>();
                var cardGroup = TourPart(tour, HouseTutorial.CardName).GetComponent<CanvasGroup>();
                Assert.That(tour.Count, Is.EqualTo(7), "Every step, so two presses of Next still leave steps to come.");

                foreach (float wait in new[] { .12f, .7f })
                {
                    yield return TourRealSeconds(wait);
                    float before = layer.alpha;
                    string when = "Next pressed with the dim at " + before.ToString("0.000");
                    TourButton(tour, "Next").onClick.Invoke();
                    float last = layer.alpha;
                    Assert.That(last, Is.GreaterThanOrEqualTo(before - .001f), when + ": on the press it went to " + last.ToString("0.000") + ".");
                    Assert.That(cardGroup.alpha, Is.LessThan(.01f), when + ": the new card waits out its delay unseen.");

                    float until = Time.realtimeSinceStartup + .4f;
                    for (int frame = 1; Time.realtimeSinceStartup < until; frame++)
                    {
                        yield return null;
                        Assert.That(layer.alpha, Is.GreaterThanOrEqualTo(last - .001f),
                            when + ": " + frame + " frames after it, the dim fell from " + last.ToString("0.000") + " to " + layer.alpha.ToString("0.000") + ".");
                        last = layer.alpha;
                    }
                    Assert.That(layer.alpha, Is.EqualTo(1f), when + ": and 0.4 s on it is fully up.");
                    Assert.That(cardGroup.alpha, Is.GreaterThan(0f), when + ": the new card has begun to rise in.");
                }
            }
            finally
            {
                if (tour.IsShowing) tour.Skip();
                UnityEngine.Object.Destroy(tour.gameObject);
                UnityEngine.Object.Destroy(hud.gameObject);
            }
        }

        /// <summary>
        /// A click in the lit hole reaches the tour, not the chrome it lights or the floor behind it:
        /// on every lit step the first thing under the middle of the hole is the ring, as the
        /// reference build's full-screen tour layer takes a click there. Offered to an imported
        /// season the tour sits over a live HUD with the player's input on, and a click on the lit
        /// rail opened a notebook page under it.
        /// </summary>
        [UnityTest]
        public IEnumerator Tour_AClickInTheLitHoleGoesToTheTour()
        {
            var tour = DirectorTour();
            tour.Show(TourChrome);
            yield return TourFrames(2);
            try
            {
                Assert.That(EventSystem.current, Is.Not.Null, "The fixture has an event system to raycast with.");
                var screen = new Rect(0f, 0f, Screen.width, Screen.height);
                var hits = new List<RaycastResult>();
                int lit = 0;
                for (int i = 0; i < tour.Count; i++)
                {
                    string step = tour.StepTitles[i];
                    if (tour.CurrentAnchors.Count > 0)
                    {
                        lit++;
                        var ring = TourPart(tour, HouseTutorial.RingName);
                        // The tour draws on an overlay canvas, whose world units are the screen's pixels.
                        var centre = TourWorldRect(ring).center;
                        Assert.That(screen.Contains(centre), Is.True, step + ": the hole's middle " + centre + " is on the screen.");
                        hits.Clear();
                        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = centre }, hits);
                        string seen = "[" + string.Join(", ", hits.Select(hit => hit.gameObject.name + "@" + hit.module.name)) + "]";
                        Assert.That(hits, Is.Not.Empty, step + ": nothing at all under the hole's middle " + centre + ".");
                        Assert.That(hits[0].gameObject.transform, Is.SameAs(ring),
                            step + ": a click in the hole at " + centre + " went to " + seen + " before the tour.");
                    }
                    if (i < tour.Count - 1)
                    {
                        TourButton(tour, "Next").onClick.Invoke();
                        yield return TourFrames(2);
                    }
                }
                Assert.That(lit, Is.EqualTo(5), "Five of the seven steps light a piece of the HUD.");
            }
            finally
            {
                if (tour.IsShowing) tour.Skip();
            }
            yield return null;
        }

        /// <summary>
        /// "Skip Tutorial" can be seen with the keyboard on it. It has no fill, so its words are what
        /// brighten, from the grey they rest in to the HUD's paper white, and settle back when the
        /// keyboard leaves; the caption is the same throughout. The tint went on its transparent
        /// fill, and moving from Next to it changed nothing on it at all.
        /// </summary>
        [UnityTest]
        public IEnumerator Tour_SkipTutorialBrightensWhenTheKeyboardIsOnIt()
        {
            var tour = DirectorTour();
            tour.Show(_ => null);
            yield return TourFrames(2);
            try
            {
                var events = EventSystem.current;
                Assert.That(events, Is.Not.Null, "The fixture has an event system to focus with.");
                var skip = TourButton(tour, "Skip Tutorial");
                var next = TourButton(tour, "Next");
                Assert.That(skip, Is.Not.Null);
                Assert.That(events.currentSelectedGameObject, Is.SameAs(next.gameObject), "The tour lands on Next.");
                float resting = TourSeen(skip);

                events.SetSelectedGameObject(skip.gameObject);
                // Unity's colour fade is 0.1 s on the real clock; a batchmode frame is far shorter.
                yield return TourRealSeconds(.3f);
                Assert.That(events.currentSelectedGameObject, Is.SameAs(skip.gameObject), "The keyboard is on the skip.");
                float focused = TourSeen(skip);
                Assert.That(focused - resting, Is.GreaterThan(.2f),
                    "With the keyboard on it the skip looks " + focused.ToString("0.000") + " bright, against " + resting.ToString("0.000") + " at rest.");
                Assert.That(TourButton(tour, "Skip Tutorial"), Is.SameAs(skip), "The caption is untouched: the words brighten, they do not change.");

                events.SetSelectedGameObject(next.gameObject);
                yield return TourRealSeconds(.3f);
                Assert.That(TourSeen(skip), Is.EqualTo(resting).Within(.02f), "It settles back when the keyboard leaves.");
            }
            finally
            {
                if (tour.IsShowing) tour.Skip();
            }
            yield return null;
        }

        /// <summary>
        /// How bright a control looks: the brightest of its graphics, each its own colour times the
        /// tint its renderer carries, weighted by its alpha - so a tint on a transparent fill counts
        /// for nothing, which is what a player sees of it.
        /// </summary>
        private static float TourSeen(Button button) =>
            button.GetComponentsInChildren<Graphic>(false)
                .Select(graphic =>
                {
                    var drawn = graphic.color * graphic.canvasRenderer.GetColor();
                    return drawn.grayscale * Mathf.Clamp01(drawn.a);
                })
                .DefaultIfEmpty(0f).Max();
    }
}
