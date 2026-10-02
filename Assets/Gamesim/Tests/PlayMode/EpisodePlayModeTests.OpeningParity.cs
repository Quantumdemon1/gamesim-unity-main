using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    /// The intro, the house entry and the walk-in against the reference build - the GitHub web
    /// game's IntroSequence.tsx, HouseEntrySequence.tsx and HouseWalkInSequence.tsx under
    /// D:/gamesim-web/src/components/game-phases - and the show's name, Gamesim: The House, on
    /// every surface that carries it.
    ///
    /// <para>Every flourish here is movement, and batchmode skips the movement unless a plan asks
    /// for it, so the tests of a flourish set <see cref="OpeningSequence.Settings.MotionInBatchmode"/>
    /// and the tests of its absence leave the reduced motion <c>SequencePlan</c> sets. Waits are on
    /// the real clock and bounded in seconds: the sequence animates in unscaled seconds, and a
    /// batchmode frame is under half a millisecond. A timed test lets a couple of frames pass
    /// before it starts the sequence, so the frame it starts on is not the long one after setup.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary><c>SequencePlan</c> with the movement on, in batchmode too.</summary>
        private static OpeningSequence.Settings ParityMotionPlan(List<string> recorded, EpisodeState season)
        {
            var plan = SequencePlan(recorded, season);
            plan.ReducedMotion = false;
            plan.MotionInBatchmode = true;
            return plan;
        }

        /// <summary>Real seconds, spent a frame at a time: what the sequence's unscaled clock counts.</summary>
        private static IEnumerator ParitySeconds(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            for (int frame = 0; frame < 500000 && Time.realtimeSinceStartup < until; frame++) yield return null;
        }

        /// <summary>The newest on-screen label with this name under <paramref name="root"/>, or null.</summary>
        private static TMP_Text ParityLabel(Component root, string name) =>
            SequenceLabels(root).LastOrDefault(label => label.name == name);

        private static bool ParityUpperCase(TMP_Text label) =>
            label != null && (label.fontStyle & FontStyles.UpperCase) == FontStyles.UpperCase;

        private static Vector3 ParityCentre(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return (corners[0] + corners[2]) / 2f;
        }

        /// <summary>
        /// A front door whose bodies are still assembling until the test says otherwise - the one
        /// thing <c>FakeStage</c>, whose house is always built, cannot stand in for.
        /// </summary>
        private sealed class ParityStage : OpeningSequence.IStage
        {
            public float Readiness;
            public int Placements;

            public ParityStage(float readiness) { Readiness = readiness; }

            public float BodyReadiness => Readiness;
            public bool TryPlace() { Placements++; Placed = true; return true; }
            public bool Placed { get; private set; }
            public bool Ready => true;
            public bool Failed => false;
            public void RestoreHome() { }
            public House.HouseCameraRig.Shot DoorShot => new House.HouseCameraRig.Shot { Seconds = 0.6f };
            public House.HouseCameraRig.Shot PushInShot => new House.HouseCameraRig.Shot { Seconds = 0.5f };
            public bool OnDeck(string id) => true;
            public bool ToDoor(string id) => true;
            public bool AtDoor(string id) => true;
            public void OpenDoor() { }
            public void CloseDoor() { }
            public bool ThroughDoor(string id) => true;
            public bool OnMark(string id) => true;
            public void Present(string id) { }
            public void SendOff(string id) { }
            public void StrikeSet() { }
            public void SendHome(string id) { }
            public bool AllHome => true;
            public void HoldForIntroductions() { }
        }

        // ---------------------------------------------------------------- the title

        /// <summary>
        /// The title card names the show as a lockup - "GAMESIM" over "THE HOUSE", centred on it:
        /// Gamesim: The House - and says which season this is, from the career record, where the
        /// reference build read "Season 2" on every new game. Under reduced motion the season and the
        /// subtitle are simply there, in the reference's capitals.
        /// </summary>
        [UnityTest]
        public IEnumerator Parity_TheTitleNamesTheShowAndNumbersTheSeason()
        {
            var season = SequenceSeason();
            var plan = SequencePlan(new List<string>(), season);
            plan.SeasonNumber = 3;
            var sequence = Opening();
            sequence.Play(new string[0], plan);
            yield return null;
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.Intro));
            Assert.That(sequence.CurrentGuestId, Is.Null, "The title card is up, not a houseguest.");

            var card = SequenceNode(sequence, "Title card");
            Assert.That(card, Is.Not.Null);
            CollectionAssert.AreEqual(new[] { "GAMESIM" }, SequenceTexts(card, "Title"));
            CollectionAssert.AreEqual(new[] { "THE HOUSE" }, SequenceTexts(card, "Brand line"), "The wordmark is the lockup Gamesim: The House.");
            var word = ParityLabel(card, "Title");
            var line = ParityLabel(card, "Brand line");
            Assert.That(line.rectTransform.position.y, Is.LessThan(word.rectTransform.position.y), "THE HOUSE stands under GAMESIM,");
            Assert.That(Mathf.Abs(line.rectTransform.position.x - word.rectTransform.position.x), Is.LessThan(1f), "centred on it.");

            var seasonLine = ParityLabel(card, "Season");
            Assert.That(seasonLine, Is.Not.Null, "The title says which season this is.");
            Assert.That(seasonLine.text, Is.EqualTo("Season 3"), "Numbered from the career record.");
            Assert.That(ParityUpperCase(seasonLine), Is.True, "In the reference's capitals.");
            Assert.That(seasonLine.maxVisibleCharacters, Is.GreaterThanOrEqualTo(seasonLine.text.Length),
                "Under reduced motion the season is there at once, not typed.");
            Assert.That(seasonLine.rectTransform.position.y, Is.LessThan(line.rectTransform.position.y), "It stands under the wordmark.");

            var subtitle = ParityLabel(card, "Subtitle");
            Assert.That(subtitle.text, Is.EqualTo("8 Houseguests. 1 Winner."), "Its words are unchanged; the capitals are type.");
            Assert.That(ParityUpperCase(subtitle), Is.True);
            Assert.That(subtitle.alpha, Is.GreaterThan(0.5f), "Under reduced motion the subtitle is up at once.");
            Assert.That(subtitle.rectTransform.position.y, Is.LessThan(seasonLine.rectTransform.position.y), "It stands under the season.");

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// With motion, "Season N" types itself out a letter at a time from 1.8 s, and the subtitle
        /// waits for 2.4 s and then rises its 20 px into place - the reference build's title.
        /// </summary>
        [UnityTest]
        public IEnumerator Parity_TheSeasonTypesOutThenTheSubtitleRises()
        {
            var season = SequenceSeason();
            var plan = ParityMotionPlan(new List<string>(), season);
            plan.SeasonNumber = 3;
            var sequence = Opening();
            yield return null;
            yield return null;
            float start = Time.realtimeSinceStartup;
            sequence.Play(new string[0], plan);
            yield return null;

            var card = SequenceNode(sequence, "Title card");
            Assert.That(card, Is.Not.Null, "The title card is up.");
            var seasonLine = ParityLabel(card, "Season");
            var subtitle = ParityLabel(card, "Subtitle");
            Assert.That(seasonLine.maxVisibleCharacters, Is.Zero, "Nothing of the season is typed yet.");
            Assert.That(subtitle.alpha, Is.Zero, "The subtitle waits for its moment.");
            float subtitleFrom = subtitle.rectTransform.anchoredPosition.y;

            var counts = new HashSet<int>();
            string early = null;
            while (Time.realtimeSinceStartup - start < 3.5f && seasonLine != null)
            {
                float t = Time.realtimeSinceStartup - start;
                counts.Add(seasonLine.maxVisibleCharacters);
                if (early == null && t < 1.7f && seasonLine.maxVisibleCharacters > 0) early = "a letter of the season at " + t.ToString("0.000") + " s";
                if (early == null && t < 2.3f && subtitle.alpha > 0f) early = "the subtitle at " + t.ToString("0.000") + " s";
                yield return null;
            }

            Assert.That(early, Is.Null, "Nothing arrives before its time, but here came " + early + ".");
            Assert.That(counts.Any(count => count > 0 && count < seasonLine.text.Length), Is.True,
                "The season is typed a letter at a time, not put up whole.");
            Assert.That(seasonLine.maxVisibleCharacters, Is.GreaterThanOrEqualTo(seasonLine.text.Length), "and is all there in the end.");
            Assert.That(subtitle.alpha, Is.GreaterThan(0.8f), "The subtitle has come up,");
            Assert.That(subtitle.rectTransform.anchoredPosition.y - subtitleFrom, Is.EqualTo(20f).Within(0.5f), "rising 20 px into place.");

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// The main menu carries the same lockup: "THE HOUSE" under the wordmark, centred on the word
        /// rather than on the house mark and the word together. The word's own label still reads
        /// "GAMESIM", which is how everything else finds it.
        /// </summary>
        [UnityTest]
        public IEnumerator Parity_TheMainMenuCarriesTheHouseUnderTheName()
        {
            director.OpenMainMenu();
            yield return null;
            yield return null;

            var labels = Menu().GetComponentsInChildren<TMP_Text>(false);
            var word = labels.Where(label => label.text == "GAMESIM").ToArray();
            var line = labels.Where(label => label.text == "THE HOUSE").ToArray();
            Assert.That(word, Has.Length.EqualTo(1), "The menu's wordmark still reads GAMESIM.");
            Assert.That(line, Has.Length.EqualTo(1), "THE HOUSE stands with it.");
            var wordCentre = ParityCentre(word[0].rectTransform);
            var lineCentre = ParityCentre(line[0].rectTransform);
            Assert.That(lineCentre.y, Is.LessThan(wordCentre.y), "Under the word,");
            Assert.That(Mathf.Abs(lineCentre.x - wordCentre.x), Is.LessThan(2f), "centred on it.");

            director.CloseMainMenu();
            yield return null;
        }

        /// <summary>
        /// The sign over the front door is the lockup too, fitted to it: "THE HOUSE" under "GAMESIM",
        /// both clear of the crown below and the eye above, and both well inside the jambs, centred
        /// on the doorway.
        /// </summary>
        [UnityTest]
        public IEnumerator Parity_TheFrontDoorsSignReadsGamesimTheHouse()
        {
            var set = OpeningDoorSet.Build(director.gameObject.scene);
            yield return null;
            var labels = set.GetComponentsInChildren<TextMeshPro>(true);
            var word = labels.SingleOrDefault(label => label.name == "Sign");
            var line = labels.SingleOrDefault(label => label.name == "Sign line");
            Assert.That(word, Is.Not.Null, "The door has its sign.");
            Assert.That(line, Is.Not.Null, "and the sign has its second line.");
            Assert.That(word.text, Is.EqualTo("GAMESIM"));
            Assert.That(line.text, Is.EqualTo("THE HOUSE"));

            ParityGlyphs(word, out float wordBottom, out float wordTop, out float wordFrom, out float wordTo);
            ParityGlyphs(line, out float lineBottom, out float lineTop, out float lineFrom, out float lineTo);
            Assert.That(lineTop, Is.LessThan(wordBottom), "THE HOUSE stands under GAMESIM, clear of it.");
            Assert.That(lineBottom, Is.GreaterThan(2.8f), "Both clear the crown, whose top is at 2.8 m,");
            Assert.That(wordTop, Is.LessThan(3.25f), "and stay under the eye, whose quad starts at 3.25 m.");
            Assert.That(wordTo - wordFrom, Is.LessThan(2.3f), "The word runs inside the jambs,");
            Assert.That(lineTo - lineFrom, Is.LessThan(2.3f), "and so does the line.");
            Assert.That((wordFrom + wordTo) / 2f, Is.EqualTo(OpeningDoorSet.DoorCentre.z).Within(0.15f), "Both are centred on the doorway.");
            Assert.That((lineFrom + lineTo) / 2f, Is.EqualTo(OpeningDoorSet.DoorCentre.z).Within(0.15f));

            UnityEngine.Object.Destroy(set.gameObject);
            yield return null;
        }

        /// <summary>The world extent of a sign line's glyphs: height, and how far they run along the facade.</summary>
        private static void ParityGlyphs(TextMeshPro label, out float bottom, out float top, out float from, out float to)
        {
            label.ForceMeshUpdate(true);
            bottom = float.MaxValue; top = float.MinValue; from = float.MaxValue; to = float.MinValue;
            var info = label.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                var glyph = info.characterInfo[i];
                if (!glyph.isVisible) continue;
                foreach (var corner in new[] { glyph.bottomLeft, glyph.topRight })
                {
                    var world = label.transform.TransformPoint(corner);
                    bottom = Mathf.Min(bottom, world.y);
                    top = Mathf.Max(top, world.y);
                    from = Mathf.Min(from, world.z);
                    to = Mathf.Max(to, world.z);
                }
            }
        }

        // ---------------------------------------------------------------- loading and the music cues

        /// <summary>
        /// While the house is still being built, the loader says the reference build's "Preparing the
        /// show..." in its capitals from the gate's first frame, and the theme is held. The moment the
        /// bodies are built the theme is let go, the house is placed once, and the title comes up.
        /// </summary>
        [UnityTest]
        public IEnumerator Parity_TheLoaderPreparesTheShowAndHoldsTheTheme()
        {
            var season = SequenceSeason();
            var stage = new ParityStage(0.5f);
            var plan = ParityMotionPlan(new List<string>(), season);
            plan.Stage = stage;
            var sequence = Opening();
            sequence.Play(new string[0], plan);

            Assert.That(sequence.MusicHeld, Is.True, "The theme waits while the house is still assembling.");
            var gate = SequenceNode(sequence, "Loading");
            Assert.That(gate, Is.Not.Null, "The loader is up from the gate's first frame.");
            CollectionAssert.AreEqual(new[] { "Preparing the show..." }, SequenceTexts(gate, "Caption"), "The reference's words,");
            Assert.That(ParityUpperCase(ParityLabel(gate, "Caption")), Is.True, "in its capitals.");
            Assert.That(SequenceNode(sequence, "Title card"), Is.Null, "The title waits for the house.");

            yield return ParitySeconds(0.2f);
            Assert.That(sequence.MusicHeld, Is.True, "Still held while the bodies assemble.");
            Assert.That(stage.Placements, Is.Zero, "Nobody is placed mid-assembly.");

            stage.Readiness = 1f;
            yield return SequenceWait(() => !sequence.MusicHeld, 2f, 20000);
            Assert.That(sequence.MusicHeld, Is.False, "The theme is let go the moment the house is built.");
            yield return null;
            Assert.That(stage.Placements, Is.EqualTo(1), "The house is placed, once.");
            Assert.That(SequenceNode(sequence, "Loading"), Is.Null, "The loader has gone.");
            Assert.That(SequenceNode(sequence, "Title card"), Is.Not.Null, "The title is up.");
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.Intro));

            sequence.Skip();
            yield return null;
            Assert.That(sequence.MusicHeld, Is.False);
        }

        /// <summary>
        /// Nothing that ends the loader early leaves the theme held behind it: a skip, the show's skip
        /// that stops at the introductions, and a cancel all let it go at once.
        /// </summary>
        [UnityTest]
        public IEnumerator Parity_EndingTheLoaderEarlyLetsTheThemeGo()
        {
            var season = SequenceSeason();
            var sequence = Opening();

            var plan = ParityMotionPlan(new List<string>(), season);
            plan.Stage = new ParityStage(0.5f);
            sequence.Play(new string[0], plan);
            yield return null;
            Assert.That(sequence.MusicHeld, Is.True);
            sequence.Skip();
            Assert.That(sequence.MusicHeld, Is.False, "A skip lets the theme go at once.");
            yield return null;
            Assert.That(sequence.IsPlaying, Is.False);

            var pill = ParityMotionPlan(new List<string>(), season);
            pill.Stage = new ParityStage(0.5f);
            pill.Introduce = SequenceIntroductions(season, new List<string>());
            sequence.Play(new string[0], pill);
            yield return null;
            Assert.That(sequence.MusicHeld, Is.True);
            Assert.That(sequence.IntroductionsPending, Is.True, "There are houseguests to meet.");
            sequence.PressSkip();
            Assert.That(sequence.MusicHeld, Is.False, "The show's skip lets the theme go too.");
            yield return SequenceWait(() => sequence.IsMeeting, 2f, 20000);
            Assert.That(sequence.IsMeeting, Is.True, "The introductions follow.");
            Assert.That(sequence.MusicHeld, Is.False, "and nothing holds the theme under them.");
            sequence.Skip();
            yield return null;

            var cancel = ParityMotionPlan(new List<string>(), season);
            cancel.Stage = new ParityStage(0.5f);
            sequence.Play(new string[0], cancel);
            yield return null;
            Assert.That(sequence.MusicHeld, Is.True);
            sequence.Cancel();
            Assert.That(sequence.MusicHeld, Is.False, "A cancel lets the theme go.");
            yield return null;
        }

        /// <summary>
        /// The theme closes with the intro: <see cref="OpeningSequence.MusicClosing"/> is up for the
        /// fade to black and no longer - down as the house entry starts, down at a skip, and down at
        /// once when the show's skip leaves a fade over a staged house for the introductions.
        /// </summary>
        [UnityTest]
        public IEnumerator Parity_TheFadeToBlackClosesTheTheme()
        {
            var season = SequenceSeason();
            var sequence = Opening();
            sequence.Play(new string[0], SequencePlan(new List<string>(), season));
            Assert.That(sequence.MusicClosing, Is.False, "Nothing is closing at the start.");
            yield return SequenceStep(sequence, () => sequence.MusicClosing);
            Assert.That(sequence.MusicClosing, Is.True, "The intro reaches its fade to black.");
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.Intro), "which is still the intro,");
            Assert.That(SequenceNode(sequence, "Fade"), Is.Not.Null, "with the fade on screen.");
            yield return SequenceStep(sequence, () => sequence.CurrentBeat != OpeningBeat.Intro);
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.HouseEntry));
            Assert.That(sequence.MusicClosing, Is.False, "The house entry is past the close.");
            sequence.Skip();
            yield return null;

            sequence.Play(new string[0], SequencePlan(new List<string>(), season));
            yield return SequenceStep(sequence, () => sequence.MusicClosing);
            Assert.That(sequence.MusicClosing, Is.True);
            sequence.Skip();
            Assert.That(sequence.MusicClosing, Is.False, "A skip in the fade ends the close.");
            yield return null;

            // Over a staged house the show's skip puts it back behind black for a moment first.
            var stage = new FakeStage(season.Active.Count());
            var staged = ParityMotionPlan(new List<string>(), season);
            staged.Stage = stage;
            staged.Introduce = SequenceIntroductions(season, new List<string>());
            sequence.Play(new string[0], staged);
            yield return SequenceStep(sequence, () => sequence.MusicClosing, 15f);
            Assert.That(sequence.MusicClosing, Is.True, "The staged intro reaches its fade to black.");
            Assert.That(stage.Placed, Is.True);
            sequence.PressSkip();
            Assert.That(sequence.MusicClosing, Is.False, "The show's skip ends the close at the press.");
            yield return SequenceWait(() => sequence.IsMeeting, 5f);
            Assert.That(sequence.IsMeeting, Is.True);
            Assert.That(sequence.MusicClosing, Is.False);
            sequence.Skip();
            yield return null;
        }

        // ---------------------------------------------------------------- the reveals

        /// <summary>
        /// The frame flashes white as a front door gives - never more than eight tenths white and
        /// gone again in a moment, the reference build's camera flash - and nothing flashes before a
        /// door opens.
        ///
        /// <para>The flash is sampled once a frame, and it is up and down again in 0.15 s: how close
        /// a sample comes to the 0.8 peak depends on how long the frames around the peak were, and
        /// frames of 40 ms - an interactive editor under 27 fps, or a single stall in batchmode -
        /// read it as low as 0.6 while it is right. So this asserts what sampling can guarantee, and
        /// the envelope itself - clear, 0.8 at 30%, clear at the end - is pinned exactly by
        /// OpeningPresentationTests.TheDoorFlashPeaksAtEightTenthsAndClears, which asks
        /// <see cref="OpeningSequence.FlashAlpha"/> directly.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator Parity_TheDoorFlashesWhiteAsItOpens()
        {
            var season = SequenceSeason();
            var stage = new FakeStage(season.Active.Count());
            var plan = ParityMotionPlan(new List<string>(), season);
            plan.Stage = stage;
            var sequence = Opening();
            sequence.Play(new string[0], plan);
            yield return null;
            Assert.That(SequenceNode(sequence, "Flash"), Is.Null, "Nothing flashes under the title.");

            // Continue pressed once a frame, as SequenceStep presses it, with every frame before the
            // door gives watched for a flash: the door opens and the flash starts in the same frame,
            // so a flash seen while nothing has opened came early - over the lower third, say, as the
            // houseguest is named.
            string early = null;
            float until = Time.realtimeSinceStartup + 10f;
            for (int frame = 0; frame < 50000 && stage.Count("OpenDoor") == 0 && sequence.IsPlaying && Time.realtimeSinceStartup < until; frame++)
            {
                if (early == null && SequenceNode(sequence, "Flash") != null)
                    early = "a flash with no door open, " + (sequence.CurrentGuestId == null ? "under the title" : "at " + sequence.CurrentGuestId + "'s reveal");
                sequence.Advance();
                yield return null;
            }
            Assert.That(early, Is.Null, "Nothing flashes before a door opens, but there was " + early + ".");
            Assert.That(stage.Count("OpenDoor"), Is.EqualTo(1), "The first door has opened.");
            var flash = SequenceNode(sequence, "Flash");
            Assert.That(flash, Is.Not.Null, "The frame flashes as the door gives.");
            var image = flash.GetComponent<Image>();
            Assert.That(image.raycastTarget, Is.False, "The flash takes no clicks from the frame under it.");
            float opened = Time.realtimeSinceStartup, peak = 0f;
            bool white = true;
            while (image != null && Time.realtimeSinceStartup - opened < 1f)
            {
                peak = Mathf.Max(peak, image.color.a);
                white &= image.color.r >= 0.99f && image.color.g >= 0.99f && image.color.b >= 0.99f;
                yield return null;
            }
            float lasted = Time.realtimeSinceStartup - opened;
            Assert.That(white, Is.True, "It is white.");
            Assert.That(peak, Is.GreaterThan(0f), "It is seen: the frame does go white.");
            Assert.That(peak, Is.LessThanOrEqualTo(0.8001f), "Never more than eight tenths white.");
            Assert.That(image == null, Is.True, "Then it is gone,");
            Assert.That(lasted, Is.LessThan(0.6f), "in a moment.");
            Assert.That(SequenceNode(sequence, "Flash"), Is.Null);

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// The lower third comes in on the reference build's stagger: the whole fades in without
        /// rising, the name slides forty pixels in from the left from 1.6 s, the details slide in
        /// from the right, and the count is in capitals. The name is the name alone - no "(You)".
        /// </summary>
        [UnityTest]
        public IEnumerator Parity_TheLowerThirdNameSlidesIn()
        {
            var season = SequenceSeason();
            season.Active.Single(person => person.isPlayer).name = "Dana Reyes";
            var plan = ParityMotionPlan(new List<string>(), season);
            var sequence = Opening();
            yield return null;
            yield return null;
            sequence.Play(new string[0], plan);
            yield return SequenceStep(sequence, () => sequence.CurrentGuestId == season.playerId);
            float start = Time.realtimeSinceStartup;
            Assert.That(sequence.CurrentGuestId, Is.EqualTo(season.playerId));

            var third = (RectTransform)SequenceNode(sequence, "Lower third");
            Assert.That(third, Is.Not.Null);
            var name = ParityLabel(third, "Name");
            Assert.That(name.text, Is.EqualTo("Dana Reyes"), "The name alone, as the reference shows it.");
            var counter = ParityLabel(third, "Counter");
            Assert.That(counter.text, Is.EqualTo("1 of 8"));
            Assert.That(ParityUpperCase(counter), Is.True, "The count is in capitals.");
            var details = (RectTransform)SequenceNode(third, "Details");
            Assert.That(details, Is.Not.Null, "The details are one block.");
            float thirdY = third.anchoredPosition.y;
            float detailsFrom = details.anchoredPosition.x;

            var samples = new List<Vector2>();
            while (Time.realtimeSinceStartup - start < 2.6f && name != null)
            {
                samples.Add(new Vector2(Time.realtimeSinceStartup - start, name.rectTransform.anchoredPosition.x));
                Assert.That(third.anchoredPosition.y, Is.EqualTo(thirdY), "The whole fades in without rising.");
                yield return null;
            }
            float first = samples[0].y, last = samples[samples.Count - 1].y;
            Assert.That(last - first, Is.EqualTo(40f).Within(0.5f), "The name slides forty pixels in from the left,");
            Assert.That(samples.Where(sample => sample.x < 1.5f).All(sample => Mathf.Abs(sample.y - first) < 0.01f), Is.True,
                "not before its moment,");
            Assert.That(samples.Any(sample => sample.y > first + 2f && sample.y < last - 2f), Is.True, "and is seen on its way.");
            Assert.That(samples.Zip(samples.Skip(1), (a, b) => b.y >= a.y - 1e-3f).All(forward => forward), Is.True,
                "always moving right, never back.");
            Assert.That(detailsFrom, Is.EqualTo(30f).Within(0.5f), "The details start thirty pixels to the right");
            Assert.That(details.anchoredPosition.x, Is.EqualTo(0f).Within(0.5f), "and slide into place.");
            Assert.That(third.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(1e-3f), "The whole is in.");

            sequence.Skip();
            yield return null;
        }

        // ---------------------------------------------------------------- the house together

        /// <summary>
        /// The house together is welcomed to Gamesim: The House, in light capitals, under a burst of
        /// the reference build's 120 pieces of confetti thrown upward - golds with the house's blues
        /// among them so gold does not own the moment - which is gone with the beat. Under reduced
        /// motion there is no confetti, as the reference disables it.
        /// </summary>
        [UnityTest]
        public IEnumerator Parity_TheGroupCardWelcomesTheHouseWithConfetti()
        {
            var season = SequenceSeason();
            var sequence = Opening();
            sequence.Play(new string[0], ParityMotionPlan(new List<string>(), season));
            yield return SequenceStep(sequence, () => SequenceNode(sequence, "Welcome") != null, 10f);

            var welcome = ParityLabel(sequence, "Welcome");
            Assert.That(welcome, Is.Not.Null, "The house together is welcomed.");
            Assert.That(welcome.text, Is.EqualTo("Welcome to Gamesim: The House"));
            Assert.That(ParityUpperCase(welcome), Is.True, "In the reference's capitals.");

            var burst = SequenceNode(sequence, "Confetti");
            Assert.That(burst, Is.Not.Null, "Confetti is thrown over the house together.");
            var pieces = burst.GetComponentsInChildren<Image>(false).Where(image => image.name == "Piece").ToArray();
            Assert.That(pieces, Has.Length.EqualTo(120), "The reference's 120 pieces.");
            Color[] golds = { UiTheme.Hex("FBBF24"), UiTheme.Hex("F59E0B"), UiTheme.Hex("D97706") };
            Color[] blues = { UiTheme.Heading, UiTheme.Accent };
            bool Like(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;
            int gold = pieces.Count(piece => golds.Any(colour => Like(piece.color, colour)));
            int blue = pieces.Count(piece => blues.Any(colour => Like(piece.color, colour)));
            Assert.That(gold, Is.GreaterThan(0), "The reference's golds are in it,");
            Assert.That(blue, Is.GreaterThan(0), "the house's blues too,");
            Assert.That(gold, Is.LessThan(pieces.Length / 2), "and gold does not own it.");

            // Watched for half a second rather than read once: the slowest piece is above its start
            // for most of a second, and a long frame must not decide the answer.
            var rose = new bool[pieces.Length];
            float thrown = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - thrown < 0.5f && !rose.All(up => up))
            {
                for (int i = 0; i < pieces.Length; i++)
                    if (pieces[i] != null && pieces[i].rectTransform.anchoredPosition.y > 20f) rose[i] = true;
                yield return null;
            }
            Assert.That(rose.Count(up => up), Is.EqualTo(pieces.Length), "Every piece is thrown upward from where it started.");

            yield return SequenceStep(sequence, () => sequence.CurrentBeat == OpeningBeat.HouseEntry);
            yield return null;
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.HouseEntry));
            Assert.That(SequenceNode(sequence, "Confetti"), Is.Null, "The confetti is gone with the beat.");
            sequence.Skip();
            yield return null;

            sequence.Play(new string[0], SequencePlan(new List<string>(), season));
            yield return SequenceStep(sequence, () => SequenceNode(sequence, "Welcome") != null, 10f);
            Assert.That(SequenceNode(sequence, "Welcome"), Is.Not.Null, "Under reduced motion the house is still welcomed,");
            Assert.That(SequenceNode(sequence, "Confetti"), Is.Null, "but no confetti is thrown.");
            sequence.Skip();
            yield return null;
        }

        // ---------------------------------------------------------------- house entry and walk-in

        /// <summary>
        /// The house entry is the reference build's card: "Welcome to the House" with how many have
        /// entered under it in capitals, the card growing in from nine tenths. The season's arrival
        /// line fades in at 2 s, as the reference's "Head of Household competition starting soon"
        /// did - in the card, under the count, rather than low on the screen where the reference's
        /// stood: there it ran edge to edge over the Continue hint (UI-UX-PASS-PLAN S0). A departure
        /// that follows from it: the reference's card fades at 4 s, and this one holds the beat,
        /// because the line is read inside it.
        /// </summary>
        [UnityTest]
        public IEnumerator Parity_TheHouseEntryWelcomesThenCounts()
        {
            var season = SequenceSeason();
            var plan = SequencePlan(new List<string>(), season);
            var sequence = Opening();
            sequence.Play(new[] { OpeningBeat.Intro }, plan);
            yield return SequenceWait(() => sequence.CurrentBeat == OpeningBeat.HouseEntry, 2f, 200);
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.HouseEntry));

            var card = SequenceNode(sequence, "Card");
            Assert.That(card, Is.Not.Null);
            var heading = ParityLabel(card, "Heading");
            var entered = ParityLabel(card, "Entered");
            Assert.That(heading.text, Is.EqualTo("Welcome to the House"));
            Assert.That(entered.text, Is.EqualTo("8 Houseguests have entered"));
            Assert.That(ParityUpperCase(entered), Is.True, "The count is in capitals,");
            Assert.That(entered.rectTransform.position.y, Is.LessThan(heading.rectTransform.position.y), "under the welcome.");

            var line = ParityLabel(sequence, "Line");
            Assert.That(line.text, Is.EqualTo(plan.ArrivalLine), "The season's arrival line, word for word,");
            Assert.That(ParityUpperCase(line), Is.True, "in capitals,");
            Assert.That(line.transform.IsChildOf(card), Is.True, "in the card,");
            Assert.That(line.rectTransform.position.y, Is.LessThan(entered.rectTransform.position.y), "under the count,");
            Assert.That(Inside((RectTransform)card, line.rectTransform), Is.True, "inside the card's edge.");
            var everything = SequenceLabels(sequence).Select(label => label.text ?? string.Empty).ToArray();
            Assert.That(everything.Where(text => text.IndexOf("Head of Household", System.StringComparison.OrdinalIgnoreCase) >= 0), Is.Empty,
                "Nothing promises a competition.");
            sequence.Skip();
            yield return null;

            yield return null;
            sequence.Play(new[] { OpeningBeat.Intro }, ParityMotionPlan(new List<string>(), season));
            float start = Time.realtimeSinceStartup;
            yield return null;
            card = SequenceNode(sequence, "Card");
            line = ParityLabel(sequence, "Line");
            Assert.That(card.localScale.x, Is.EqualTo(0.9f).Within(1e-3f), "With motion the card starts at nine tenths,");
            Assert.That(line.alpha, Is.Zero, "and the line waits.");
            yield return ParitySeconds(1.2f - (Time.realtimeSinceStartup - start));
            Assert.That(card.localScale.x, Is.EqualTo(1f).Within(1e-3f), "The card has grown into place.");
            Assert.That(line.alpha, Is.Zero, "The line is still waiting for 2 s.");
            yield return ParitySeconds(2.8f - (Time.realtimeSinceStartup - start));
            Assert.That(line.alpha, Is.GreaterThan(0.5f), "and then it is up.");
            yield return ParitySeconds(4.6f - (Time.realtimeSinceStartup - start));
            Assert.That(card.GetComponent<CanvasGroup>().alpha, Is.GreaterThanOrEqualTo(0.99f),
                "The card holds the beat past the reference's 4 s: the season's line is read inside it.");
            Assert.That(line.alpha, Is.GreaterThan(0.5f), "and the line with it.");
            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// The walk-in's caption is the reference build's: "The houseguests enter the house..." in
        /// capitals at about 20 pt, standing 128 px off the bottom once it has risen its 10 px from
        /// 0.5 s, and gone at 3 s.
        /// </summary>
        [UnityTest]
        public IEnumerator Parity_TheWalkInCaptionRisesAndGoes()
        {
            var season = SequenceSeason();
            var sequence = Opening();
            yield return null;
            yield return null;
            float start = Time.realtimeSinceStartup;
            sequence.Play(new[] { OpeningBeat.Intro, OpeningBeat.HouseEntry }, ParityMotionPlan(new List<string>(), season));
            yield return null;
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.WalkIn));

            var caption = ParityLabel(sequence, "Caption");
            Assert.That(caption, Is.Not.Null, "The walk-in has its caption.");
            Assert.That(caption.text, Is.EqualTo("The houseguests enter the house..."));
            Assert.That(ParityUpperCase(caption), Is.True, "in capitals,");
            Assert.That(caption.fontSize, Is.EqualTo(20f).Within(0.5f), "at about 20 pt,");
            Assert.That(caption.rectTransform.anchorMin, Is.EqualTo(new Vector2(0.5f, 0f)), "hung from the bottom of the frame,");
            Assert.That(caption.rectTransform.pivot.y, Is.EqualTo(0f));
            Assert.That(caption.rectTransform.anchoredPosition.y, Is.EqualTo(118f).Within(0.01f), "starting 10 px low,");
            Assert.That(caption.alpha, Is.Zero, "and unseen.");

            yield return ParitySeconds(1.4f - (Time.realtimeSinceStartup - start));
            Assert.That(caption != null && caption.gameObject.activeInHierarchy, Is.True, "It is up,");
            Assert.That(caption.rectTransform.anchoredPosition.y, Is.EqualTo(128f).Within(0.01f), "128 px off the bottom,");
            Assert.That(caption.alpha, Is.GreaterThan(0.5f));

            yield return ParitySeconds(3.2f - (Time.realtimeSinceStartup - start));
            Assert.That(SequenceNode(sequence, "Caption"), Is.Null, "and gone at 3 s.");
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.WalkIn), "while the walk-in goes on.");

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// The walk-in's camera keeps the reference build's schedule: it opens pulled back from the
        /// doorway and pushes in to it at once, rises when that second is up, and cranes over the
        /// house at 4 s.
        /// </summary>
        [UnityTest]
        public IEnumerator Parity_TheWalkInPushesInThenRisesThenCranes()
        {
            var rig = Rig();
            Assert.That(rig, Is.Not.Null, "The scene should have a camera rig.");
            rig.ControlsEnabled = true;
            var season = SequenceSeason();
            var plan = ParityMotionPlan(new List<string>(), season);
            plan.Rig = rig;
            plan.WalkInKeys = Gamesim.Episode.EpisodeDirector.WalkInKeys;
            var keys = plan.WalkInKeys;
            Assert.That(keys.Count, Is.EqualTo(4), "Four keys: pulled back, the doorway, the rise, over the top.");
            var sequence = Opening();
            yield return null;
            yield return null;
            float start = Time.realtimeSinceStartup;
            sequence.Play(new[] { OpeningBeat.Intro, OpeningBeat.HouseEntry }, plan);
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.WalkIn));
            Assert.That(rig.DesiredDistance, Is.EqualTo(keys[0].Distance).Within(0.01f), "It opens pulled back from the doorway.");

            float pushedAt = -1f, roseAt = -1f, cranedAt = -1f;
            while (Time.realtimeSinceStartup - start < 4.8f && sequence.CurrentBeat == OpeningBeat.WalkIn)
            {
                float t = Time.realtimeSinceStartup - start, distance = rig.DesiredDistance;
                if (pushedAt < 0f && Mathf.Abs(distance - keys[1].Distance) < 0.01f) pushedAt = t;
                if (roseAt < 0f && Mathf.Abs(distance - keys[2].Distance) < 0.01f) roseAt = t;
                if (cranedAt < 0f && Mathf.Abs(distance - keys[3].Distance) < 0.01f) cranedAt = t;
                yield return null;
            }
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.WalkIn), "The walk-in is still playing.");
            Assert.That(pushedAt, Is.InRange(0f, 0.25f), "The push-in toward the doorway starts at once,");
            Assert.That(roseAt, Is.InRange(0.8f, 1.4f), "the rise when its second is up,");
            Assert.That(cranedAt, Is.InRange(3.8f, 4.5f), "and the crane when the three seconds' rise is.");

            sequence.Skip();
            yield return null;
            Assert.That(rig.ControlsEnabled, Is.True, "The camera is given back.");
        }
    }
}
