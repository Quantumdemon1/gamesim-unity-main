using System;
using System.Collections.Generic;
using System.IO;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The tour's card docks beside the chrome each step lights and never over it, at every text
    /// size on a 16:9 screen and a 16:10 one, with its title on one line (TourDock); a returning
    /// player is offered the tour rather than shown it (TourOfferWords, the opening's call); and
    /// the player reads "tour", never "tutorial".
    ///
    /// <para>The chrome stands where the HUD draws it, in its 1600x900 units (the stand-in
    /// EpisodePlayModeTests.Tour.cs measures against, and the HUD's docked panel and cast strip at
    /// each text size). Text is measured by a deliberately generous model: wider letters and taller
    /// lines than Inter's, and every step given more words than the longest step has, so a card
    /// that docks here has room to spare on the real one. The built card is checked in PlayMode
    /// (EpisodePlayModeTests.TourOffer.cs).</para>
    ///
    /// <para>Unity-free, so the subset runs it too.</para>
    /// </summary>
    public sealed class TourDockTests
    {
        private const float HolePadding = 6f;
        /// <summary>More words than any step's body has, with the pad's controls line in it.</summary>
        private const int BodyCharacters = 460;
        private const string LongestTitle = "Welcome to Gamesim: The House!";

        private static readonly float[] TextScales = { 1f, 1.2f, 1.4f };

        /// <summary>The two screen shapes in canvas units: 16:9 is the reference; 16:10 matches width and height equally.</summary>
        private static readonly (string Name, float W, float H)[] Screens =
        {
            ("16:9", 1600f, 900f),
            ("16:10", 1920f / (float)Math.Sqrt(1920.0 / 1600.0 * (1200.0 / 900.0)), 1200f / (float)Math.Sqrt(1920.0 / 1600.0 * (1200.0 / 900.0))),
        };

        // ------------------------------------------------------------------ the text model

        private static float LineHeight(float fontSize) => fontSize * 1.3f;

        private static float Lines(int characters, float fontSize, float inner, float letter) =>
            (float)Math.Max(1.0, Math.Ceiling(characters * letter * fontSize / Math.Max(1f, inner)));

        private static float TitleWidth(string title, float s) => title.Length * .6f * 21f * s;

        private static Func<float, float> HeightFor(string title, float s, bool offer = false) => width =>
            TourDock.Measure(width, s,
                inner => Lines(title.Length, 21f * s, inner, .6f) * LineHeight(21f * s),
                inner => Lines(offer ? TourOfferWords.OfferBody.Length : BodyCharacters, 15f * s, inner, .56f) * LineHeight(15f * s),
                inner => Lines(TourOfferWords.ReplayNote.Length, 14f * s, inner, .56f) * LineHeight(14f * s),
                offer).Height;

        /// <summary>A box placed from the frame's top-left, as the HUD anchors its chrome.</summary>
        private static TourDock.Box FromTop(float frameH, float x, float top, float w, float h) => new TourDock.Box(x, frameH - top - h, w, h);

        private static TourDock.Box Pad(TourDock.Box box) =>
            new TourDock.Box(box.X - HolePadding, box.Y - HolePadding, box.W + 2f * HolePadding, box.H + 2f * HolePadding);

        private static TourDock.Box Union(TourDock.Box a, TourDock.Box b) =>
            TourDock.Box.MinMax(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.XMax, b.XMax), Math.Max(a.YMax, b.YMax));

        /// <summary>The panel's floor at a text scale: the cast strip, the status band, as the HUD sums it (EpisodeHud.PanelFloor).</summary>
        private static float PanelFloor(float s) => (14f + 96f * s + 14f) + 50f * s + 8f;

        /// <summary>Every lit step's chrome on a frame at a text scale: the target and the step's title.</summary>
        private static IEnumerable<(string Step, string Title, TourDock.Box Target)> Targets(float w, float h, float s)
        {
            yield return ("topbar", "Top Bar", Union(FromTop(h, 270f, 14f, 280f, 52f), FromTop(h, 894f, 14f, 360f, 52f)));
            yield return ("sidemenu", "Side Rail", FromTop(h, 8f, 74f, 212f, 560f));
            yield return ("gamephase in the rail", "The Episode Screen", FromTop(h, 20f, 420f, 188f, 40f));
            yield return ("gamephase on the objective card", "The Episode Screen", FromTop(h, 244f, 206f, 294f, 44f));
            yield return ("carousel", "Cast Strip", new TourDock.Box(8f, 8f, w - 16f, 108f * s));
            yield return ("overlay on the objective chip", "Phase Panels", FromTop(h, 564f, 14f, 320f, 52f));
            yield return ("overlay on the objective card", "Phase Panels", FromTop(h, 226f, 80f, 330f, 238f));
        }

        private static IEnumerable<TestCaseData> LitSteps()
        {
            foreach (var screen in Screens)
                foreach (float s in TextScales)
                    foreach (var target in Targets(screen.W, screen.H, s))
                        yield return new TestCaseData(screen.Name, screen.W, screen.H, s, target.Step, target.Title, target.Target)
                            .SetName("TheCardDocksBeside(" + target.Step + ", " + screen.Name + ", x" + s + ")");
        }

        private static void AssertDocked(string where, TourDock.Box frame, TourDock.Box hole, float s, string title, TourDock.Placement placement)
        {
            var card = placement.Card;
            var inside = new TourDock.Box(frame.X + TourDock.Margin, frame.Y + TourDock.Margin, frame.W - 2f * TourDock.Margin, frame.H - 2f * TourDock.Margin);
            Assert.That(placement.Docked, Is.True, where + ": the card " + card + " found a side clear of " + hole + ".");
            Assert.That(card.Within(inside), Is.True, where + ": the card " + card + " stands inside the frame " + inside + ".");
            Assert.That(card.Overlaps(hole), Is.False, where + ": the card " + card + " covers none of what glows, " + hole + ".");
            Assert.That(card.W, Is.GreaterThanOrEqualTo(TourDock.TitleFit(s, TitleWidth(title, s)) - .01f),
                where + ": the card is wide enough for its title on one line.");
            Assert.That(card.H, Is.EqualTo(HeightFor(title, s)(card.W)).Within(.01f), where + ": the card is laid out at the width it stands at.");
        }

        [TestCaseSource(nameof(LitSteps))]
        public void TheCardDocksBesideWhatItsStepLights(string shape, float w, float h, float s, string step, string title, TourDock.Box target)
        {
            var frame = new TourDock.Box(0f, 0f, w, h);
            var hole = Pad(target);
            float titleWidth = TitleWidth(title, s);
            var placement = TourDock.Place(frame, hole, TourDock.BaseWidth(s, titleWidth, w), TourDock.MinWidth(s, titleWidth), HeightFor(title, s));
            AssertDocked(step + " at " + shape + " x" + s, frame, hole, s, title, placement);
        }

        private static IEnumerable<TestCaseData> Frames()
        {
            foreach (var screen in Screens)
                foreach (float s in TextScales)
                    yield return new TestCaseData(screen.Name, screen.W, screen.H, s).SetName("Frame(" + screen.Name + ", x" + s + ")");
        }

        /// <summary>
        /// The phase step over an open decision panel: beside it where a side has the room, and where
        /// none has (the widest panel at the largest text), the objective - which the step names too -
        /// takes the glow and the card docks beside that (HouseTutorial.Place).
        /// </summary>
        [TestCaseSource(nameof(Frames))]
        public void ThePhaseStepDocksBesideTheOpenPanel_OrItsObjective(string shape, float w, float h, float s)
        {
            var frame = new TourDock.Box(0f, 0f, w, h);
            var panel = Pad(new TourDock.Box(w * .5f - 450f, PanelFloor(s), 900f, 300f));
            float titleWidth = TitleWidth("Phase Panels", s);
            float baseWidth = TourDock.BaseWidth(s, titleWidth, w), minWidth = TourDock.MinWidth(s, titleWidth);
            var placement = TourDock.Place(frame, panel, baseWidth, minWidth, HeightFor("Phase Panels", s));
            if (placement.Docked)
            {
                AssertDocked("the open panel at " + shape + " x" + s, frame, panel, s, "Phase Panels", placement);
                return;
            }
            foreach (var objective in new[] { FromTop(h, 564f, 14f, 320f, 52f), FromTop(h, 226f, 80f, 330f, 238f) })
            {
                var hole = Pad(objective);
                AssertDocked("the objective, for the panel at " + shape + " x" + s, frame, hole, s, "Phase Panels",
                    TourDock.Place(frame, hole, baseWidth, minWidth, HeightFor("Phase Panels", s)));
            }
        }

        /// <summary>The panel at the standard text size on 16:9 has the room above it: the step lights the decision itself there.</summary>
        [Test]
        public void ThePanelAtTheStandardSizeKeepsItsGlow()
        {
            var frame = new TourDock.Box(0f, 0f, 1600f, 900f);
            var panel = Pad(new TourDock.Box(350f, PanelFloor(1f), 900f, 300f));
            float titleWidth = TitleWidth("Phase Panels", 1f);
            var placement = TourDock.Place(frame, panel, TourDock.BaseWidth(1f, titleWidth, 1600f), TourDock.MinWidth(1f, titleWidth), HeightFor("Phase Panels", 1f));
            Assert.That(placement.Docked, Is.True);
            Assert.That(placement.Side, Is.EqualTo(TourDock.Side.Above), "Over the floor, as the reference stands it.");
            Assert.That(placement.Card.W, Is.GreaterThan(TourDock.CardWidth), "Widened, so its words take fewer lines and it fits over the panel.");
        }

        /// <summary>The first and last steps light nothing: a centred card, inside the frame, as wide as its title needs.</summary>
        [TestCaseSource(nameof(Frames))]
        public void TheCentredStepsStandInTheMiddle(string shape, float w, float h, float s)
        {
            var frame = new TourDock.Box(0f, 0f, w, h);
            foreach (string title in new[] { LongestTitle, "You're Ready!", TourOfferWords.OfferTitle })
            {
                bool offer = title == TourOfferWords.OfferTitle;
                float titleWidth = TitleWidth(title, s);
                var placement = TourDock.Place(frame, null, TourDock.BaseWidth(s, titleWidth, w), TourDock.MinWidth(s, titleWidth), HeightFor(title, s, offer));
                var card = placement.Card;
                Assert.That(placement.Side, Is.EqualTo(TourDock.Side.Centre), title);
                Assert.That(card.CentreX, Is.EqualTo(w * .5f).Within(.5f), title + " at " + shape + " x" + s);
                Assert.That(card.CentreY, Is.EqualTo(h * .5f).Within(.5f), title + " at " + shape + " x" + s);
                Assert.That(card.W, Is.GreaterThanOrEqualTo(TourDock.TitleFit(s, titleWidth) - .01f), title + ": one line.");
            }
        }

        /// <summary>The welcome card's title wrapped at the reference width: the card now widens for it.</summary>
        [Test]
        public void ALongTitleWidensTheCard_NotWrapsIt()
        {
            float titleWidth = TitleWidth(LongestTitle, 1f);
            Assert.That(TourDock.TitleFit(1f, titleWidth), Is.GreaterThan(TourDock.CardWidth), "The model's welcome title is wider than the reference card's inside.");
            Assert.That(TourDock.BaseWidth(1f, titleWidth, 1600f), Is.EqualTo(TourDock.TitleFit(1f, titleWidth)));
            Assert.That(TourDock.BaseWidth(1f, TitleWidth("Top Bar", 1f), 1600f), Is.EqualTo(TourDock.CardWidth), "A short title keeps the reference width.");
            Assert.That(TourDock.BaseWidth(1.4f, 5000f, 1600f), Is.EqualTo(1600f - 2f * TourDock.Margin), "Never wider than the frame.");
        }

        /// <summary>
        /// Against the right edge the card stands to its left, tops level; against the left edge, to
        /// its right; under the top bar; over the floor - the reference build's sides, kept.
        /// </summary>
        [Test]
        public void TheReferenceSidesAreTriedFirst()
        {
            var frame = new TourDock.Box(0f, 0f, 1600f, 900f);
            Assert.That(TourDock.Preferred(frame, new TourDock.Box(1500f, 200f, 80f, 500f)), Is.EqualTo(TourDock.Side.LeftOf));
            Assert.That(TourDock.Preferred(frame, new TourDock.Box(8f, 200f, 212f, 560f)), Is.EqualTo(TourDock.Side.RightOf));
            Assert.That(TourDock.Preferred(frame, new TourDock.Box(270f, 820f, 980f, 60f)), Is.EqualTo(TourDock.Side.Below));
            Assert.That(TourDock.Preferred(frame, new TourDock.Box(8f, 8f, 1584f, 108f)), Is.EqualTo(TourDock.Side.Above));

            var rail = new TourDock.Box(2f, 260f, 224f, 572f);
            var placement = TourDock.Place(frame, rail, 384f, 300f, HeightFor("Side Rail", 1f));
            Assert.That(placement.Side, Is.EqualTo(TourDock.Side.RightOf));
            Assert.That(placement.Card.X, Is.EqualTo(rail.XMax + TourDock.GapRightOf).Within(.01f), "Fourteen clear of the rail.");
            Assert.That(placement.Card.YMax, Is.EqualTo(rail.YMax).Within(.01f), "Tops level.");
        }

        /// <summary>
        /// Where the reference corner would land the card on what glows - a target too tall to stand
        /// over or under, against an edge with no room beside it - another side is taken; with no
        /// side at all, the card is still kept inside the frame and says it is not docked.
        /// </summary>
        [Test]
        public void ASideWithNoRoomGivesWayToOneWithRoom_AndNoRoomAnywhereIsSaid()
        {
            var frame = new TourDock.Box(0f, 0f, 1600f, 900f);
            // A block from x 200 to the right margin, top to bottom: 168 units to its left, nothing elsewhere.
            var block = new TourDock.Box(200f, 12f, 1388f, 876f);
            var placement = TourDock.Place(frame, block, 384f, 300f, HeightFor("Top Bar", 1f));
            Assert.That(placement.Docked, Is.False, "Nothing but a sliver beside a target that fills the frame.");
            Assert.That(placement.Card.Within(frame), Is.True, "Still on screen.");

            var tall = new TourDock.Box(560f, 12f, 480f, 876f);
            placement = TourDock.Place(frame, tall, 384f, 300f, HeightFor("Top Bar", 1f));
            Assert.That(placement.Docked, Is.True, "Beside a tall middle block.");
            Assert.That(placement.Side, Is.EqualTo(TourDock.Side.RightOf).Or.EqualTo(TourDock.Side.LeftOf));
            Assert.That(placement.Card.Overlaps(tall), Is.False);
        }

        // ------------------------------------------------------------------ the card's inside

        [Test]
        public void ANarrowCardPutsSkipOnARowOfItsOwn_AndEveryBoxIsOneAndAThirdItsType()
        {
            foreach (float s in TextScales)
            {
                var wide = TourDock.Measure(TourDock.OneRowWidth(s), s, _ => 0f, _ => 0f, _ => 0f);
                Assert.That(wide.TwoRows, Is.False, "x" + s + ": the three buttons share a row at the reference width.");
                Assert.That(wide.SkipY, Is.EqualTo(wide.ButtonsY));

                var narrow = TourDock.Measure(TourDock.MinWidth(s, 0f), s, _ => 0f, _ => 0f, _ => 0f);
                Assert.That(narrow.TwoRows, Is.True, "x" + s + ": narrower, Skip moves under Previous and Next.");
                Assert.That(narrow.SkipY, Is.GreaterThanOrEqualTo(narrow.ButtonsY + TourDock.ButtonHeight(s)));
                Assert.That(narrow.Height, Is.GreaterThan(wide.Height));
                Assert.That(narrow.NoteY, Is.GreaterThanOrEqualTo(narrow.SkipY + TourDock.ButtonHeight(s)), "The note stays under the buttons.");

                Assert.That(wide.TitleHeight, Is.GreaterThanOrEqualTo(21f * s * 1.3f));
                Assert.That(wide.BodyHeight, Is.GreaterThanOrEqualTo(15f * s * 1.3f));
                Assert.That(wide.NoteHeight, Is.GreaterThanOrEqualTo(14f * s * 1.3f));
                Assert.That(TourDock.MinWidth(s, 0f), Is.GreaterThanOrEqualTo(2f * TourDock.Pad(s) + TourDock.BackWidth(s) + TourDock.NextWidth(s)),
                    "Previous and Next always fit side by side.");
            }
        }

        [Test]
        public void TheOfferCardHasNoCounterAndNoNote_AndItsAnswersShareARow()
        {
            var offer = TourDock.Measure(TourDock.CardWidth, 1f, _ => 27.3f, _ => 39f, _ => 18.2f, offer: true);
            var step = TourDock.Measure(TourDock.CardWidth, 1f, _ => 27.3f, _ => 39f, _ => 18.2f);
            Assert.That(offer.TwoRows, Is.False);
            Assert.That(offer.TitleY, Is.EqualTo(offer.Pad), "No counter above the offer's title.");
            Assert.That(offer.NoteHeight, Is.EqualTo(0f), "No replay note on the offer: it is the offer.");
            Assert.That(offer.Height, Is.LessThan(step.Height));
            Assert.That(TourDock.Pad(1f) - 10f + TourDock.SkipWidth(1f) + 8f + TourDock.TakeWidth(1f) + TourDock.Pad(1f),
                Is.LessThanOrEqualTo(TourDock.CardWidth), "Not now and Take the tour fit side by side at the reference width.");
        }

        // ------------------------------------------------------------------ the offer and the words

        [Test]
        public void AReturningPlayerIsOffered_AFirstTimePlayerGetsTheTour()
        {
            Assert.That(TourOfferWords.OpensWithOffer(seenBefore: true), Is.True, "Finished or skipped before: \"Take the tour?\".");
            Assert.That(TourOfferWords.OpensWithOffer(seenBefore: false), Is.False, "Never seen: the tour itself, as today.");
        }

        [Test]
        public void ThePlayerReadsTour_NeverTutorial()
        {
            foreach (string line in new[] { TourOfferWords.OfferTitle, TourOfferWords.OfferBody, TourOfferWords.TakeCaption, TourOfferWords.NotNowCaption, TourOfferWords.ReplayNote })
                Assert.That(line.ToLowerInvariant(), Does.Not.Contain("tutorial"), line);
            Assert.That(TourOfferWords.OfferTitle, Is.EqualTo("Take the tour?"));
            Assert.That(TourOfferWords.TakeCaption, Does.Contain("tour"));
            Assert.That(TourOfferWords.ReplayNote, Does.Contain("new season offers the tour"), "The note says how to take it again: a new season offers it.");
            Assert.That(TourOfferWords.ReplayNote, Does.Contain("Settings in the house"), "And the Settings' replay, which is still there.");
        }

        /// <summary>
        /// The season's opening asks (Offer), and the Settings' "Replay the tour" still plays it
        /// (Show): held in the source, since the director only runs in a built scene.
        /// </summary>
        [Test]
        public void TheOpeningOffersTheTour_AndSettingsReplayIt()
        {
            string opening = File.ReadAllText(Path.Combine(RuntimeRoot(), "Episode", "EpisodeDirector.Opening.cs"));
            int run = opening.IndexOf("RunTutorial = done =>", StringComparison.Ordinal);
            Assert.That(run, Is.GreaterThan(0));
            string runBody = opening.Substring(run, Math.Min(400, opening.Length - run));
            Assert.That(runBody, Does.Contain("tutorial.Offer(FindChrome)"), "The opening offers the tour rather than replaying it.");
            Assert.That(runBody, Does.Not.Contain("tutorial.Show("));

            string season = File.ReadAllText(Path.Combine(RuntimeRoot(), "Episode", "EpisodeDirector.Season.cs"));
            int replay = season.IndexOf("public void ReplayTour()", StringComparison.Ordinal);
            Assert.That(replay, Is.GreaterThan(0));
            Assert.That(season.Substring(replay, Math.Min(300, season.Length - replay)), Does.Contain("tutorial.Show(FindChrome)"),
                "Settings' Replay the tour plays the tour itself.");
        }

        private static string RuntimeRoot()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Runtime"));
#else
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "Gamesim", "Runtime");
                if (Directory.Exists(candidate)) return Path.GetFullPath(candidate);
            }
            Assert.Fail("Assets/Gamesim/Runtime was not found above " + AppContext.BaseDirectory);
            return null;
#endif
        }
    }
}
