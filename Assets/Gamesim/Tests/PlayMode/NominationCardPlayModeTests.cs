using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The nomination card as the owner asked for it (UI-UX-PASS-PLAN N0 and N1, decision 9): the
    /// key hangs left of the whole title on every play of the card, not only the first; the set's
    /// screen frame stands on an opaque ground, so the screen's own lit face no longer glows through
    /// the glass; every label on both frames of both ceremony cards stands inside the card, clear of
    /// its edge, in a box Inter draws in, and on the screen frames no two cross; the host's line
    /// fits the screen's face in every wording the format reads; the screen frame carries a roster
    /// of the house whose chips say only what the keys have said, its names and the block's drawn
    /// at one size and never above their type; and the key stands on its pedestal through every
    /// beat, gold only while the last key waits.
    ///
    /// <para>Each card is attached to a plain owner, as <see cref="CeremonyGlassPlayModeTests"/> does,
    /// and the screen frame is mounted on a board measured the way the set's screen is, so what is
    /// measured is the card and not the house around it. The staged card's own captures are in
    /// EpisodePlayModeTests.CeremonyStage and .CeremonyStageWeeks. The frames here are rendered
    /// with a camera of the test's own on the screen's shot, so a pixel can be read: a transform can
    /// read right while the picture is wrong.</para>
    /// </summary>
    public sealed class NominationCardPlayModeTests
    {
        private static readonly string[] Names =
        {
            "Emma Brown", "Riley Johnson", "Alex Chen", "Sam Patel", "Noah Kim", "Ava Lopez",
            "Liam Novak", "Mia Rossi", "Ethan Park", "Zoe Adams", "Owen Hart", "Ivy Moreno",
            "Leo Fischer", "Nora Blake",
        };

        /// <summary>The block, in every test: Jordan Taylor and Casey Wilson.</summary>
        private const string Jordan = "a", Casey = "b", Hoh = "Maya Hassan";

        private const float LargeText = 1.2f;

        private GameObject owner;
        private readonly List<Object> props = new List<Object>();

        [SetUp]
        public void CreateFixture() => owner = new GameObject("Nomination card owner");

        [UnityTearDown]
        public IEnumerator DestroyFixture()
        {
            foreach (var prop in props) if (prop != null) Object.Destroy(prop);
            props.Clear();
            if (owner != null) Object.Destroy(owner);
            yield return null;
        }

        // ------------------------------------------------------------------ the title's mark (N0, 6.1 and 6.2)

        /// <summary>
        /// On every play after the first, Cancel had deactivated the column and Build measured the
        /// title under it before its Awake, so TextMesh Pro returned a tenth of the width and the
        /// mark landed over the T of NOMINATION (the owner's week-four frame). Played, cancelled and
        /// played again, at both text sizes, on the HUD and on a mounted screen: the mark - the key,
        /// not the trophy - never crosses the title as drawn.
        /// </summary>
        [UnityTest]
        public IEnumerator TheKeyHangsLeftOfTheWholeTitleOnEveryPlay()
        {
            foreach (float fontScale in new[] { 1f, LargeText })
            {
                var keys = Keys(3, CeremonyPace.Quick, fontScale);
                yield return null;
                AssertTheMarkClearsTheTitle(keys, "The HUD's first play at " + fontScale);
                keys.Cancel();
                PlayAgain(keys, 4, null);
                yield return null;
                AssertTheMarkClearsTheTitle(keys, "The HUD's second play at " + fontScale);
                if (fontScale > 1f) yield return CaptureTheHud(keys, "nomination-card-week2-large");
                keys.Cancel();

                var screen = Screen();
                PlayAgain(keys, 2, screen);
                yield return null;
                AssertTheMarkClearsTheTitle(keys, "The screen's play after the HUD's at " + fontScale);
                keys.Cancel();
                PlayAgain(keys, 3, screen);
                yield return null;
                AssertTheMarkClearsTheTitle(keys, "The screen's second play at " + fontScale);
                if (fontScale == 1f) yield return CaptureTheFace(screen, "nomination-card-week2-screen");
                keys.Cancel();
            }
        }

        // ------------------------------------------------------------------ the ground (N0, 6.3)

        /// <summary>
        /// The glass alone let fifteen percent of the screen's lit face through the card: a warm blob
        /// in the band and lighter diagonals from the corners. On the screen frame the column's first
        /// child is a ground tinted opaque and drawn with a fill that is opaque - the pack's panel
        /// fill, tinted opaque, is 236 of 255 at its centre, and the nineteen parts of the bright
        /// board it let through lifted the band's pixels to three times the ground's - and a frame
        /// of the card's first beat on the screen's shot reads the card's own colour where the blob
        /// was and in the band's middle, nearer the card's colour than the bright, unlit board behind
        /// it, which is read beside the card for the comparison. The HUD's card keeps its 85 % glass
        /// over the room.
        /// </summary>
        [UnityTest]
        public IEnumerator TheScreenFrameStandsOnAnOpaqueGround()
        {
            var screen = Screen();
            var keys = Keys(3, CeremonyPace.Quick, 1f, screen);
            yield return null;
            var column = Rect(keys, "Card");
            var ground = column.GetChild(0).GetComponent<Image>();
            Assert.That(column.GetChild(0).name, Is.EqualTo("Screen ground"), "The column's first child on the screen frame is the ground.");
            Assert.That(ground.color.a, Is.EqualTo(1f).Within(0.001f), "and its tint is opaque.");
            var fill = ground.sprite;
            Assert.That(fill != null && fill.texture != null && fill.texture.isReadable, Is.True, "The ground is drawn with the kit's own fill, which the test can read,");
            Assert.That(fill.texture.GetPixel(fill.texture.width / 2, fill.texture.height / 2).a, Is.EqualTo(1f).Within(0.002f),
                "and that fill is opaque at its centre: the pack's panel fill is 236 of 255 there, and the board behind the card showed through the rest.");
            var glass = Rect(keys, "Card glass").GetComponent<Image>();
            Assert.That(glass.color.a, Is.EqualTo(UiTheme.GlassFill.a).Within(0.005f), "The glass over it is still the glass.");
            Assert.That(keys.KeysShown, Is.Zero, "The first beat: the Head of Household's line and the unlit keys.");

            // What the card draws where nothing else does: the ground under the glass.
            var expected = Color.Lerp(ground.color, glass.color, glass.color.a);
            yield return CaptureTheFace(screen, "nomination-card-ground", (frame, camera) =>
            {
                Color Pixel(Vector2 at)
                {
                    var world = column.TransformPoint(new Vector3(at.x, at.y, 0f));
                    var pixel = camera.WorldToScreenPoint(world);
                    return frame.GetPixel(Mathf.RoundToInt(pixel.x), Mathf.RoundToInt(pixel.y));
                }
                // The board beside the card, in the face's bezel: bright, so a leak through the card is seen.
                var board = Pixel(new Vector2(-625f, -400f));
                Assert.That(Distance(board, expected), Is.GreaterThan(0.3f),
                    "The board behind the card reads " + ColorUtility.ToHtmlStringRGB(board) + ": not bright enough beside the card's "
                    + ColorUtility.ToHtmlStringRGB(expected) + " to tell a leak from the ground.");
                foreach (var at in new[] { new Vector2(0f, -230f), new Vector2(0f, -400f) })
                {
                    var colour = Pixel(at);
                    Assert.That(Distance(colour, expected), Is.LessThan(Distance(colour, board)),
                        "The card at " + at + " is " + ColorUtility.ToHtmlStringRGB(colour) + ": nearer the board behind it than its own ground.");
                    foreach (var (channel, got, want) in new[] { ("r", colour.r, expected.r), ("g", colour.g, expected.g), ("b", colour.b, expected.b) })
                        Assert.That(got, Is.EqualTo(want).Within(0.03f),
                            "The card at " + at + " is " + ColorUtility.ToHtmlStringRGB(colour) + " where its ground is "
                            + ColorUtility.ToHtmlStringRGB(expected) + " (" + channel + "): something shows through the card.");
                }
            });
            keys.Cancel();

            var hud = Keys(3, CeremonyPace.Quick);
            yield return null;
            var hudColumn = Rect(hud, "Card");
            Assert.That(hudColumn.GetChild(0).name, Is.EqualTo("Card glass"), "On the HUD the glass is still the column's first child,");
            Assert.That(hudColumn.GetChild(0).GetComponent<Image>().color.a, Is.EqualTo(UiTheme.GlassFill.a).Within(0.005f), "at 85 %.");
            Assert.That(Rect(hud, "Screen ground"), Is.Null, "There is no ground on the HUD's card.");
        }

        private static float Distance(Color a, Color b) =>
            Mathf.Sqrt((a.r - b.r) * (a.r - b.r) + (a.g - b.g) * (a.g - b.g) + (a.b - b.b) * (a.b - b.b));

        // ------------------------------------------------------------------ the labels (N0, 6.4 and 6.8)

        /// <summary>
        /// Every label on the key card, the live eviction and the takeover - on the HUD at both text
        /// sizes and on the screen frame - stands inside the card's glass, its words at least 3 % of
        /// the card's height from the edge, in a box at least 1.3 times its type, and draws; on the
        /// screen frames no two labels' boxes cross, and no ring's art reaches a label it does not
        /// carry. The screen frames' controls lines ended on the face's edge (one at 13 points), the
        /// vote screen's result banner and controls overprinted, and the HUD vote's title, names and
        /// figures and the takeover's names sat under the 1.3 floor.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryLabelStandsInsideTheCardOnBothFramesAtBothSizes()
        {
            foreach (float fontScale in new[] { 1f, LargeText })
            {
                string size = " at " + fontScale;
                var keys = Keys(3, CeremonyPace.Quick, fontScale);
                yield return null;
                AssertLabelsStandInsideTheCard(keys, "The key card on the HUD, first beat" + size, "Tagline");
                keys.SkipToResult();
                yield return null;
                AssertLabelsStandInsideTheCard(keys, "The key card on the HUD, the block" + size, "Tagline");
                keys.Cancel();

                var reveal = Reveal(fontScale);
                yield return null;
                AssertLabelsStandInsideTheCard(reveal, "The live eviction on the HUD, before the votes" + size);
                reveal.SkipToResult();
                yield return null;
                AssertLabelsStandInsideTheCard(reveal, "The live eviction on the HUD, the result" + size);
                reveal.Cancel();

                var takeover = CeremonyTakeover.Attach(owner);
                props.Add(takeover.gameObject);
                takeover.FontScale = fontScale;
                takeover.Play(CeremonySting.VetoKind, 2, new[]
                {
                    new CeremonyTakeover.Subject("Maya Hassan", "HOLDER", null),
                    new CeremonyTakeover.Subject("Jordan Taylor", "SAVED", null),
                    new CeremonyTakeover.Subject("Casey Wilson", "ON THE BLOCK", null),
                }, true);
                yield return null;
                AssertLabelsStandInsideTheCard(takeover, "The takeover" + size);
                takeover.Cancel();
            }

            var screen = Screen();
            var staged = Keys(3, CeremonyPace.Quick, 1f, screen);
            yield return null;
            AssertLabelsStandInsideTheCard(staged, "The key card on the screen, first beat");
            yield return Until(() => staged.KeysShown >= 1, 10f);
            AssertLabelsStandInsideTheCard(staged, "The key card on the screen, a key out");
            staged.SkipToResult();
            yield return null;
            AssertLabelsStandInsideTheCard(staged, "The key card on the screen, the block");
            staged.Cancel();

            var board = Reveal(1f, screen);
            yield return null;
            AssertLabelsStandInsideTheCard(board, "The live eviction on the screen, before the votes");
            board.SkipToResult();
            yield return null;
            AssertLabelsStandInsideTheCard(board, "The live eviction on the screen, the result");
            board.Cancel();
        }

        // ------------------------------------------------------------------ the host's line (N0, 6.4)

        /// <summary>
        /// The host's line on the screen frame, in every wording the format reads - the house's
        /// count, the Head of Household's tie-break, a sole vote, and the player spoken to on the
        /// count and on the tie-break - stands on one line inside the face's 3 % margin, whole, drawn
        /// no smaller than twenty points and no larger than its thirty. Boxed at the face's full
        /// width, the tie-break's wording ended on the edge.
        /// </summary>
        [UnityTest]
        public IEnumerator TheHostsLineFitsTheScreensFaceInEveryWording()
        {
            var screen = Screen();
            var wordings = new (string What, VoteReveal.Ballot[] Board, bool PlayerEvicted, string Line)[]
            {
                ("the count", Ballots(3, 1, false), false, "By a vote of 3 to 1, Jordan Taylor, you have been evicted."),
                ("the tie-break", Ballots(2, 2, true), false, "By the Head of Household's tie-breaking vote, Jordan Taylor, you have been evicted."),
                ("the sole vote", Ballots(1, 0, false), false, Names[0] + " cast the sole vote to evict. Jordan Taylor, you have been evicted."),
                ("the player evicted", Ballots(3, 1, false), true, "By a vote of 3 to 1, you have been evicted."),
                ("the player evicted on the tie-break", Ballots(2, 2, true), true, "By the Head of Household's tie-breaking vote, you have been evicted."),
            };
            foreach (var wording in wordings)
            {
                string where = "The live eviction on the screen, " + wording.What;
                var board = Reveal(1f, screen, wording.Board, wording.PlayerEvicted);
                yield return null;
                board.SkipToResult();
                yield return null;
                var host = Text(board, "Host");
                Assert.That(host.text, Is.EqualTo(wording.Line), where + ": the host's line.");
                AssertLabelsStandInsideTheCard(board, where);
                host.ForceMeshUpdate(true);
                Assert.That(host.textInfo.lineCount, Is.EqualTo(1), where + ": the host's line is drawn on one line.");
                Assert.That(host.isTextTruncated, Is.False, where + ": the host's line loses its tail.");
                Assert.That(host.fontSize, Is.GreaterThanOrEqualTo(20f - 0.05f).And.LessThanOrEqualTo(30f + 0.05f),
                    where + ": the host's line is drawn at " + host.fontSize.ToString("0.#") + " points, not between twenty and its thirty.");
                board.Cancel();
            }
        }

        // ------------------------------------------------------------------ the roster (N1, 6.5)

        /// <summary>
        /// The roster says only what the keys have said: at the first beat the Head of Household's
        /// chip reads HOH and every other is empty, after key k exactly k read SAFE, and only at the
        /// block do the two nominees read NOMINATED - while the block's own count, the labels named
        /// "Nominee", is what it always was. No roster child borrows that name or the "Key " prefix.
        /// </summary>
        [UnityTest]
        public IEnumerator TheRosterSaysOnlyWhatTheKeysHave()
        {
            var screen = Screen();
            var keys = Keys(3, CeremonyPace.Quick, 1f, screen);
            yield return null;
            Assert.That(Rects(keys, "Roster face"), Has.Length.EqualTo(6), "A face for the Head of Household, three keys' holders and the block.");
            Assert.That(Texts(keys, "Roster name"), Is.EqualTo(new[] { Hoh, Names[0], Names[1], Names[2], "Jordan Taylor", "Casey Wilson" }),
                "in cast order, the Head of Household first.");
            var chips = Chips(keys);
            Assert.That(chips.Count(word => word == "NOMINATED"), Is.Zero, "Nothing says who is on the block before the keys do.");
            Assert.That(chips.Where(word => !string.IsNullOrEmpty(word)), Is.EqualTo(new[] { "HOH" }), "Only the Head of Household's chip is up at the first beat.");
            Assert.That(chips[0], Is.EqualTo("HOH"), "and it is theirs.");

            for (int k = 1; k <= 3; k++)
            {
                int key = k;
                yield return Until(() => keys.KeysShown >= key, 10f);
                Assert.That(keys.KeysShown, Is.EqualTo(key), "Key " + key + " never came.");
                chips = Chips(keys);
                Assert.That(chips.Count(word => word == "SAFE"), Is.EqualTo(key), "After key " + key + " exactly " + key + " read SAFE.");
                Assert.That(chips.Skip(1).Take(key), Is.All.EqualTo("SAFE"), "on the holders of the keys out so far,");
                Assert.That(chips.Count(word => word == "NOMINATED"), Is.Zero, "and nobody is called nominated yet.");
            }

            yield return Until(() => keys.ShowingBlock, 10f);
            Assert.That(keys.ShowingBlock, Is.True, "The block never came.");
            chips = Chips(keys);
            Assert.That(chips, Is.EqualTo(new[] { "HOH", "SAFE", "SAFE", "SAFE", "NOMINATED", "NOMINATED" }), "At the block the nominees read NOMINATED.");
            Assert.That(Texts(keys, "Nominee"), Is.EquivalentTo(new[] { "Jordan Taylor", "Casey Wilson" }), "The block's own count is unchanged.");
            var roster = Rect(keys, "Roster");
            Assert.That(roster.GetComponentsInChildren<Transform>(true).Any(part => part.name == "Nominee"), Is.False, "No roster child is named 'Nominee',");
            Assert.That(roster.GetComponentsInChildren<Transform>(true).Any(part => part.name.StartsWith("Key ", System.StringComparison.Ordinal)), Is.False,
                "and none starts with 'Key '.");
            Assert.That(keys.GetComponentsInChildren<RectTransform>(true).Count(rect => rect.name.StartsWith("Key ", System.StringComparison.Ordinal)),
                Is.EqualTo(3), "The key slots are still counted by their prefix alone.");
            Assert.That(Rect(keys, "Roster").GetComponentsInChildren<RectTransform>(true).Count(rect => rect.name == "Role mark"), Is.EqualTo(3),
                "The Head of Household's crown, and a target on each nominee only at the block.");
            AssertEveryLabelDraws(keys, "The roster at the block");
            AssertNoRosterWordIsCut(keys, "The roster at the block");
        }

        /// <summary>
        /// Every word on the roster and the stage's faces is drawn whole: the names and the chips'
        /// words are drawn smaller to fit their boxes, never cut - the HUD's labels truncate, and
        /// NOMINATED at the chip's full size is wider than a chip.
        /// </summary>
        private static void AssertNoRosterWordIsCut(KeyCeremony keys, string where)
        {
            Canvas.ForceUpdateCanvases();
            var words = keys.GetComponentsInChildren<TMP_Text>()
                .Where(label => label.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(label.text))
                .Where(label => label.name == "Roster name" || label.name == "Roster badge text" || label.name == "Name"
                    || label.name == "Nominee" || label.name == "Badge text").ToArray();
            Assert.That(words, Is.Not.Empty, where + ": no roster word is up.");
            foreach (var word in words)
            {
                word.ForceMeshUpdate(true);
                Assert.That(word.isTextTruncated, Is.False, where + ": '" + word.text + "' (" + word.name + ", " + word.fontSize.ToString("0.#")
                    + " in a box " + word.rectTransform.rect.width.ToString("0") + " wide) loses its tail.");
            }
        }

        /// <summary>
        /// The roster at eight and at sixteen, the house's largest: every face inside the card, no two
        /// overlapping, and the stage's own column between them clear - the key over the pedestal,
        /// the beat's face over that - for the look sheet. One text size: the screen frame is the
        /// screen's own and does not grow with the large-text preference.
        /// </summary>
        [UnityTest]
        public IEnumerator TheRosterFitsEightAndSixteenFacesOnTheFace()
        {
            var screen = Screen();
            foreach (int house in new[] { 8, 16 })
            {
                var keys = Keys(house - 3, CeremonyPace.Suspenseful, 1f, screen);
                yield return null;
                string where = "A house of " + house;
                var column = Rect(keys, "Card");
                var faces = Rects(keys, "Roster face").Select(rect => OnTheCard(keys, rect)).ToArray();
                Assert.That(faces, Has.Length.EqualTo(house), where + ": a face for everyone.");
                var card = OnTheCard(keys, column);
                for (int i = 0; i < faces.Length; i++)
                {
                    Assert.That(card.Contains(faces[i].min) && card.Contains(faces[i].max), Is.True, where + ": face " + i + " " + faces[i] + " is on the card " + card + ".");
                    for (int j = i + 1; j < faces.Length; j++)
                        Assert.That(faces[i].Overlaps(faces[j]), Is.False, where + ": faces " + i + " and " + j + " overlap.");
                    Assert.That(Mathf.Abs(faces[i].center.x), Is.GreaterThan(150f), where + ": face " + i + " stands clear of the stage's column.");
                }
                var stage = OnTheCard(keys, Rect(keys, "Stage"));
                var held = Rect(keys, "Last key");
                Assert.That(held, Is.Not.Null, where + ": the key is up at the first beat.");
                var key = OnTheCard(keys, held);
                Assert.That(stage.Contains(key.center), Is.True, where + ": the key stands in the stage's band.");
                foreach (var face in faces) Assert.That(face.Overlaps(key), Is.False, where + ": a face covers the key.");
                AssertLabelsStandInsideTheCard(keys, where);
                yield return CaptureTheFace(screen, "nomination-card-roster-" + house);
                yield return Until(() => keys.KeysShown >= 1, 10f);
                Assert.That(Rects(keys, "Stage face"), Has.Length.EqualTo(1), where + ": the beat's face is on the stage,");
                Assert.That(Rects(keys, "Badge"), Has.Length.EqualTo(1), where + ": wearing the SAFE chip,");
                AssertLabelsStandInsideTheCard(keys, where + ", a key out");
                keys.SkipToResult();
                yield return null;
                Assert.That(Rects(keys, "Nominee face"), Has.Length.EqualTo(2), where + ": and the block's two faces at the block.");
                AssertLabelsStandInsideTheCard(keys, where + ", the block");
                AssertNoRosterWordIsCut(keys, where + ", the block");
                keys.Cancel();
            }
        }

        /// <summary>
        /// The roster's names are drawn at one size - the largest at which every one of them fits
        /// its box - and never above their type: auto-sized each to its own box, "Noah Kim" stood at
        /// the full thirty points beside "Riley Johnson" at twenty on the same row (the
        /// nomination-card-roster-8 capture), two weights of name on one roster. The same for the
        /// block's two names, on the screen and on the HUD at both text sizes.
        /// </summary>
        [UnityTest]
        public IEnumerator TheNamesOnTheRosterAndTheBlockAreOneSizeAndNeverAboveTheirType()
        {
            var screen = Screen();
            foreach (int house in new[] { 6, 8, 16 })
            {
                string where = "A house of " + house;
                var keys = Keys(house - 3, CeremonyPace.Suspenseful, 1f, screen);
                yield return null;
                AssertNamesAreOneSize(keys, "Roster name", 36f, where + ", the roster");
                keys.SkipToResult();
                yield return null;
                AssertNamesAreOneSize(keys, "Roster name", 36f, where + ", the roster at the block");
                AssertNamesAreOneSize(keys, "Nominee", 28f, where + ", the block");
                keys.Cancel();
            }
            foreach (float fontScale in new[] { 1f, LargeText })
            {
                var hud = Keys(3, CeremonyPace.Quick, fontScale);
                yield return null;
                hud.SkipToResult();
                yield return null;
                AssertNamesAreOneSize(hud, "Nominee", 15f * fontScale, "The HUD's block at " + fontScale);
                hud.Cancel();
            }
        }

        /// <summary>
        /// Every live label named <paramref name="name"/> on the card is drawn whole at one size, no
        /// larger than <paramref name="type"/>; and when that size is under the type, some name at
        /// the type would overrun its box - the set is shrunk only as far as its longest name needs.
        /// </summary>
        private static void AssertNamesAreOneSize(KeyCeremony keys, string name, float type, string where)
        {
            Canvas.ForceUpdateCanvases();
            var names = keys.GetComponentsInChildren<TMP_Text>().Where(label => label.name == name && label.gameObject.activeInHierarchy).ToArray();
            Assert.That(names, Is.Not.Empty, where + ": no '" + name + "' is up.");
            foreach (var label in names) label.ForceMeshUpdate(true);
            float size = names.Min(label => label.fontSize);
            foreach (var label in names)
            {
                string what = where + ": '" + label.text + "' (" + name + ")";
                Assert.That(label.fontSize, Is.LessThanOrEqualTo(type + 0.01f),
                    what + " is drawn at " + label.fontSize.ToString("0.##") + ", above its type's " + type + ".");
                Assert.That(label.fontSize, Is.EqualTo(size).Within(0.1f),
                    what + " is drawn at " + label.fontSize.ToString("0.##") + " where another is at " + size.ToString("0.##") + ": two weights of name.");
                Assert.That(label.isTextTruncated, Is.False, what + " loses its tail.");
            }
            if (size >= type - 0.5f) return;
            Assert.That(names.Any(label => WordsWidth(label) * type / label.fontSize > label.rectTransform.rect.width - 0.5f), Is.True,
                where + ": the names are drawn at " + size.ToString("0.##") + " though every one of them fits its box at " + type + ".");
        }

        /// <summary>The width of the words a label draws, in its own units: from the first visible character's origin to the last's advance.</summary>
        private static float WordsWidth(TMP_Text label)
        {
            var info = label.textInfo;
            float xMin = float.MaxValue, xMax = float.MinValue;
            for (int i = 0; i < info.characterCount; i++)
            {
                var glyph = info.characterInfo[i];
                if (!glyph.isVisible) continue;
                xMin = Mathf.Min(xMin, glyph.origin); xMax = Mathf.Max(xMax, glyph.xAdvance);
            }
            return xMin > xMax ? 0f : xMax - xMin;
        }

        // ------------------------------------------------------------------ the key on its pedestal (N1, 6.6)

        /// <summary>
        /// On the screen a glyph stands on its pedestal under the stage in every beat - the Head of
        /// Household's line, each key, the beat before the last and the block - so Show(0) no longer
        /// leaves the stage empty, and it is gold only while the progress line reads "One key left".
        /// </summary>
        [UnityTest]
        public IEnumerator TheKeyStandsOnTheStageThroughEveryBeatAndGoesGoldOnlyForTheLastKey()
        {
            // One key: the line, the beat before the last key, the key, the block - every phase in seven seconds.
            var keys = Keys(1, CeremonyPace.Suspenseful, 1f, Screen());
            var seen = new List<string>();
            string last = null;
            float until = Time.realtimeSinceStartup + 25f;
            while (keys.IsPlaying && Time.realtimeSinceStartup < until)
            {
                string progress = Text(keys, "Progress").text;
                if (progress != last)
                {
                    last = progress;
                    seen.Add(progress);
                    var stage = Rect(keys, "Stage");
                    var held = stage.GetComponentsInChildren<Image>(true).FirstOrDefault(image => image.name == "Last key");
                    Assert.That(held != null && held.gameObject.activeInHierarchy, Is.True, "At '" + progress + "' the key is up under the stage.");
                    Assert.That(stage.GetComponentsInChildren<Image>(true).Any(image => image.name == "Pedestal" && image.gameObject.activeInHierarchy), Is.True,
                        "At '" + progress + "' it stands on the pedestal.");
                    bool gold = progress == "One key left";
                    Assert.That(held.color, Is.EqualTo(gold ? UiTheme.Gold : UiTheme.Glow), "At '" + progress + "' the key is " + (gold ? "gold." : "blue."));
                }
                if (keys.ShowingBlock) break;
                yield return null;
            }
            Assert.That(seen, Is.EqualTo(new[] { "1 key. Two will not get one.", "One key left", "Revealing key 1 of 1", "Nominated for eviction" }),
                "Every beat was seen in order.");
            keys.Cancel();
        }

        /// <summary>
        /// The HUD's board has no pedestal and shows the gold key alone while the last key waits, as it
        /// always did: at the block its two nominee frames stand 44 units apart, and a key and a
        /// pedestal built under the stage showed through the gap between them.
        /// </summary>
        [UnityTest]
        public IEnumerator TheHudBoardShowsTheGoldKeyOnlyWhileTheLastKeyWaitsAndNothingBetweenTheBlocksFrames()
        {
            var keys = Keys(1, CeremonyPace.Suspenseful);
            yield return Until(() => Text(keys, "Progress").text == "One key left", 10f);
            Assert.That(Text(keys, "Progress").text, Is.EqualTo("One key left"), "The beat before the last key never came.");
            var held = Rect(keys, "Last key");
            Assert.That(held != null && held.gameObject.activeInHierarchy, Is.True, "While the last key waits the HUD shows the key,");
            Assert.That(held.GetComponent<Image>().color, Is.EqualTo(UiTheme.Gold), "in gold.");
            Assert.That(Rect(keys, "Pedestal"), Is.Null, "The HUD's board has no pedestal.");

            yield return Until(() => keys.ShowingBlock, 15f);
            Assert.That(keys.ShowingBlock, Is.True, "The block never came.");
            var slots = Rects(keys, "Nominee slot").Where(slot => slot.gameObject.activeInHierarchy).Select(slot => OnTheCard(keys, slot)).OrderBy(slot => slot.xMin).ToArray();
            Assert.That(slots, Has.Length.EqualTo(2), "The block's two frames are up.");
            var gap = UnityEngine.Rect.MinMaxRect(slots[0].xMax, Mathf.Min(slots[0].yMin, slots[1].yMin), slots[1].xMin, Mathf.Max(slots[0].yMax, slots[1].yMax));
            Assert.That(gap.width, Is.GreaterThan(0f), "The frames stand apart: " + gap);
            var between = keys.GetComponentsInChildren<RectTransform>()
                .Where(rect => (rect.name == "Last key" || rect.name == "Pedestal") && rect.gameObject.activeInHierarchy)
                .Where(rect => OnTheCard(keys, rect).Overlaps(gap))
                .Select(rect => rect.name + " " + OnTheCard(keys, rect)).ToArray();
            Assert.That(between, Is.Empty, "Nothing of the key's stands in the gap " + gap + " between the block's frames: " + string.Join(", ", between));
            Assert.That(keys.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "Last key" && rect.gameObject.activeInHierarchy), Is.False,
                "and the HUD's key is down once the block is up.");
            keys.Cancel();
        }

        // ------------------------------------------------------------------ helpers

        private KeyCeremony Keys(int safeCount, CeremonyPace pace, float fontScale = 1f, ScreenSurface screen = null)
        {
            var keys = KeyCeremony.Attach(owner);
            props.Add(keys.gameObject);
            keys.FontScale = fontScale;
            Assert.That(keys.Play(1, Hoh, false, Safe(safeCount), Block(), true, pace, screen, Roster(safeCount)), Is.True,
                "Keys to hand out and a block is a shape the ceremony narrates.");
            return keys;
        }

        /// <summary>The same card played again for a later week, as the director plays one card every week.</summary>
        private static void PlayAgain(KeyCeremony keys, int week, ScreenSurface screen)
        {
            Assert.That(keys.Play(week, Hoh, false, Safe(3), Block(), true, CeremonyPace.Quick, screen, Roster(3)), Is.True);
        }

        private static KeyCeremony.Person[] Safe(int count) =>
            Enumerable.Range(0, count).Select(i => new KeyCeremony.Person("s" + i, Names[i], null)).ToArray();

        private static KeyCeremony.Person[] Block() => new[]
        {
            new KeyCeremony.Person(Jordan, "Jordan Taylor", null),
            new KeyCeremony.Person(Casey, "Casey Wilson", null),
        };

        /// <summary>The house in cast order, the Head of Household first: the keys' holders, then the block.</summary>
        private static List<KeyCeremony.Person> Roster(int safeCount)
        {
            var people = new List<KeyCeremony.Person> { new KeyCeremony.Person("hoh", Hoh, null) };
            people.AddRange(Safe(safeCount));
            people.AddRange(Block());
            return people;
        }

        /// <summary>The live eviction on its usual board: two votes each way and the Head of Household's tie-break for Jordan.</summary>
        private VoteReveal Reveal(float fontScale, ScreenSurface screen = null) => Reveal(fontScale, screen, new[]
        {
            new VoteReveal.Ballot(Names[0], Jordan), new VoteReveal.Ballot(Names[1], Casey), new VoteReveal.Ballot(Names[2], Jordan),
            new VoteReveal.Ballot(Names[3], Casey), new VoteReveal.Ballot(Hoh, Jordan, tieBreak: true),
        });

        /// <summary>The live eviction of Jordan Taylor on <paramref name="ballots"/>, spoken to as the player when <paramref name="evictedIsPlayer"/>.</summary>
        private VoteReveal Reveal(float fontScale, ScreenSurface screen, VoteReveal.Ballot[] ballots, bool evictedIsPlayer = false)
        {
            var reveal = VoteReveal.Attach(owner);
            props.Add(reveal.gameObject);
            reveal.FontScale = fontScale;
            var block = new[]
            {
                new VoteReveal.Nominee(Jordan, "Jordan Taylor", null),
                new VoteReveal.Nominee(Casey, "Casey Wilson", null),
            };
            Assert.That(reveal.Play(4, block, ballots, Jordan, true, CeremonyPace.Quick, Hoh, false, evictedIsPlayer, screen), Is.True,
                "Two nominees and a ballot is a shape the reveal narrates.");
            return reveal;
        }

        /// <summary>
        /// A board of the house's ballots: <paramref name="forJordan"/> to evict Jordan, then
        /// <paramref name="forCasey"/> to evict Casey, cast by the roster's names in order, and the
        /// Head of Household's tie-break for Jordan when <paramref name="tieBreak"/>.
        /// </summary>
        private static VoteReveal.Ballot[] Ballots(int forJordan, int forCasey, bool tieBreak)
        {
            var ballots = new List<VoteReveal.Ballot>();
            for (int i = 0; i < forJordan + forCasey; i++) ballots.Add(new VoteReveal.Ballot(Names[i], i < forJordan ? Jordan : Casey));
            if (tieBreak) ballots.Add(new VoteReveal.Ballot(Hoh, Jordan, tieBreak: true));
            return ballots.ToArray();
        }

        /// <summary>
        /// A stand-in for the set's screen: a board 2.6 by 1.5 metres, measured the way the set's own
        /// is, stood well away from whatever another fixture left in the scene, and painted a bright
        /// unlit colour so that anything a card lets through it is seen in a frame.
        /// </summary>
        private ScreenSurface Screen()
        {
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Screen board";
            board.transform.position = new Vector3(0f, 40f, 0f);
            board.transform.localScale = new Vector3(2.6f, 1.5f, 0.05f);
            props.Add(board);
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) unlit = Shader.Find("Unlit/Color");
            if (unlit != null)
            {
                var paint = new Material(unlit) { color = new Color(1f, 0.85f, 0.4f, 1f) };
                props.Add(paint);
                board.GetComponent<Renderer>().material = paint;
            }
            var screen = ScreenSurface.Measure(board.transform, "Nomination", new Vector3(0f, 40f, 10f));
            Assert.That(screen, Is.Not.Null, "A board with a renderer is a screen to play a card on.");
            return screen;
        }

        /// <summary>The roster's chips, in roster order: the word on each, or nothing while it is empty.</summary>
        private static string[] Chips(KeyCeremony keys) =>
            keys.GetComponentsInChildren<TMP_Text>(true).Where(label => label.name == "Roster badge text")
                .Select(label => label.gameObject.activeInHierarchy ? label.text : string.Empty).ToArray();

        private static TMP_Text Text(Component card, string name) =>
            card.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(label => label.name == name);

        private static string[] Texts(Component card, string name) =>
            card.GetComponentsInChildren<TMP_Text>(true).Where(label => label.name == name).Select(label => label.text).ToArray();

        private static RectTransform Rect(Component card, string name) =>
            card.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(rect => rect.name == name);

        private static RectTransform[] Rects(Component card, string name) =>
            card.GetComponentsInChildren<RectTransform>(true).Where(rect => rect.name == name).ToArray();

        /// <summary><paramref name="rect"/> in the card's column's own space: on the screen's face that runs ±600 by 0 to -800 from the column's top.</summary>
        private static UnityEngine.Rect OnTheCard(Component card, RectTransform rect)
        {
            var space = Rect(card, "Card");
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var a = space.InverseTransformPoint(corners[0]);
            var b = space.InverseTransformPoint(corners[2]);
            return UnityEngine.Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>
        /// The words a label draws, in the card's column's space: the lines its visible characters
        /// stand on - from the first origin to the last advance, the descender to the ascender - not
        /// its box, and not its mesh, whose quads carry the font's padding past the glyphs.
        /// </summary>
        private static UnityEngine.Rect TextOnTheCard(Component card, TMP_Text label)
        {
            var info = label.textInfo;
            float xMin = float.MaxValue, xMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
            for (int i = 0; i < info.characterCount; i++)
            {
                var glyph = info.characterInfo[i];
                if (!glyph.isVisible) continue;
                xMin = Mathf.Min(xMin, glyph.origin); xMax = Mathf.Max(xMax, glyph.xAdvance);
                yMin = Mathf.Min(yMin, glyph.descender); yMax = Mathf.Max(yMax, glyph.ascender);
            }
            if (xMin > xMax) { xMin = xMax = 0f; yMin = yMax = 0f; }
            var space = Rect(card, "Card");
            var a = space.InverseTransformPoint(label.rectTransform.TransformPoint(new Vector3(xMin, yMin, 0f)));
            var b = space.InverseTransformPoint(label.rectTransform.TransformPoint(new Vector3(xMax, yMax, 0f)));
            return UnityEngine.Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>The mesh a label draws, in the card's column's space: its quads, the font's padding included - the whole of what is painted.</summary>
        private static UnityEngine.Rect MeshOnTheCard(Component card, TMP_Text label)
        {
            var space = Rect(card, "Card");
            var bounds = label.textBounds;
            var a = space.InverseTransformPoint(label.rectTransform.TransformPoint(bounds.min));
            var b = space.InverseTransformPoint(label.rectTransform.TransformPoint(bounds.max));
            return UnityEngine.Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>
        /// The title's mark is the key, and as drawn it hangs wholly left of the title's words: its
        /// rect and the title's painted mesh do not cross, in the card's own space.
        /// </summary>
        private static void AssertTheMarkClearsTheTitle(KeyCeremony keys, string where)
        {
            Canvas.ForceUpdateCanvases();
            var title = Text(keys, "Title");
            var mark = Rect(keys, "Title mark");
            Assert.That(mark, Is.Not.Null, where + ": the card has no 'Title mark'.");
            var sprite = mark.GetComponent<Image>().sprite;
            var key = UiTheme.Pack(PackArt.Pack9IconsIcKey);
            if (key == null) key = UiTheme.Pack(PackArt.Pack9NominationCeremonyKeyIcon);
            if (key == null) key = UiTheme.Icon("key");
            Assert.That(sprite, Is.SameAs(key), where + ": the mark is the key, not the trophy.");
            title.ForceMeshUpdate();
            var words = MeshOnTheCard(keys, title);
            var glyph = OnTheCard(keys, mark);
            Assert.That(words.width, Is.GreaterThan(100f), where + ": the title was measured as drawn (" + words + ").");
            Assert.That(glyph.Overlaps(words), Is.False, where + ": the mark " + glyph + " crosses the title's words " + words + ".");
            Assert.That(glyph.xMax, Is.LessThan(words.xMin), where + ": the mark hangs left of the whole title.");
        }

        /// <summary>
        /// Every label up on the card - the ones named as outside by design excepted - stands inside the
        /// card's glass, draws its words at least 3 % of the card's height inside the glass's edge, in
        /// a box at least 1.3 times its type (Inter draws nothing in one under 1.21), and draws. On a
        /// screen frame no two labels' boxes cross (the speed mark, which shares the eyebrow's row by
        /// design, excepted), and no ring's art reaches a label the ring does not carry.
        /// </summary>
        private static void AssertLabelsStandInsideTheCard(Component card, string where, params string[] outsideByDesign)
        {
            Canvas.ForceUpdateCanvases();
            var glass = Rect(card, "Card glass");
            Assert.That(glass, Is.Not.Null, where + ": no 'Card glass' to stand on.");
            var frame = OnTheCard(card, glass);
            float inset = frame.height * 0.03f;
            var labels = card.GetComponentsInChildren<TMP_Text>()
                .Where(label => label.enabled && label.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(label.text)
                    && !outsideByDesign.Contains(label.name)).ToArray();
            Assert.That(labels, Is.Not.Empty, where + ": no label is up.");
            foreach (var label in labels)
            {
                string what = where + ": '" + label.text + "' (" + label.name + ")";
                float size = label.enableAutoSizing ? label.fontSizeMax : label.fontSize;
                Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(size * 1.3f - 0.5f),
                    what + " stands in a box " + label.rectTransform.rect.height.ToString("0.#") + " tall for words of " + size.ToString("0.#") + ".");
                var box = OnTheCard(card, label.rectTransform);
                Assert.That(box.xMin >= frame.xMin - 0.5f && box.xMax <= frame.xMax + 0.5f && box.yMin >= frame.yMin - 0.5f && box.yMax <= frame.yMax + 0.5f,
                    Is.True, what + " has its box " + box + " off the card " + frame + ".");
                label.ForceMeshUpdate(true);
                Assert.That(label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(glyph => glyph.isVisible), Is.True, what + " draws nothing.");
                var words = TextOnTheCard(card, label);
                Assert.That(words.xMin >= frame.xMin + inset - 0.5f && words.xMax <= frame.xMax - inset + 0.5f
                        && words.yMin >= frame.yMin + inset - 0.5f && words.yMax <= frame.yMax - inset + 0.5f,
                    Is.True, what + " is drawn at " + words + ", within " + inset.ToString("0.#") + " of the card's edge " + frame + ".");
            }

            var canvas = card.GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace) return;
            var boxes = labels.Where(label => label.name != "Speed")
                .Select(label => (What: label.name + " '" + label.text + "'", Box: OnTheCard(card, label.rectTransform), At: label.transform)).ToArray();
            for (int i = 0; i < boxes.Length; i++)
                for (int j = i + 1; j < boxes.Length; j++)
                    Assert.That(boxes[i].Box.Overlaps(boxes[j].Box), Is.False,
                        where + ": " + boxes[i].What + " " + boxes[i].Box + " crosses " + boxes[j].What + " " + boxes[j].Box + ".");
            foreach (var ring in card.GetComponentsInChildren<Image>().Where(image => image.name == "Ring art" && image.gameObject.activeInHierarchy))
            {
                var rim = ring.transform.parent;
                var art = OnTheCard(card, ring.rectTransform);
                foreach (var box in boxes)
                {
                    if (box.At.IsChildOf(rim)) continue;
                    Assert.That(art.Overlaps(box.Box), Is.False,
                        where + ": the ring art " + art + " under " + rim.name + " reaches " + box.What + " " + box.Box + ".");
                }
            }
        }

        /// <summary>Fails on any label on the card with copy that draws not a single character.</summary>
        private static void AssertEveryLabelDraws(Component card, string where)
        {
            Canvas.ForceUpdateCanvases();
            var blank = card.GetComponentsInChildren<TMP_Text>()
                .Where(label => label.enabled && label.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(label.text))
                .Where(label =>
                {
                    label.ForceMeshUpdate(true);
                    var info = label.textInfo;
                    return !info.characterInfo.Take(info.characterCount).Any(glyph => glyph.isVisible);
                })
                .Select(label => "'" + label.text + "' (" + label.name + "; " + label.fontSize.ToString("0.#")
                    + " in a box " + label.rectTransform.rect.size.ToString("0") + ")")
                .ToArray();
            Assert.That(blank, Is.Empty, where + ": copy that draws nothing: " + string.Join(" | ", blank));
        }

        /// <summary>
        /// A frame of the mounted card from the screen's own shot (<see cref="ScreenSurface.Shot"/>),
        /// rendered by a camera of the test's own with no post-processing, so a pixel reads the card
        /// and anything the card lets through is seen. Written beside the other look-sheet frames in
        /// a batch run, and handed to <paramref name="inspect"/> before it goes.
        /// </summary>
        private IEnumerator CaptureTheFace(ScreenSurface screen, string name, System.Action<Texture2D, Camera> inspect = null)
        {
            const int width = 1600, height = 900;
            var shot = screen.Shot();
            var rig = new GameObject("Nomination card camera", typeof(Camera));
            props.Add(rig);
            var camera = rig.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.07f, 0.10f, 1f);
            camera.fieldOfView = shot.FieldOfView;
            camera.nearClipPlane = 0.1f;
            camera.transform.SetPositionAndRotation(screen.Centre + screen.Normal * shot.Distance, Quaternion.LookRotation(-screen.Normal, Vector3.up));
            yield return Render(camera, name, width, height, inspect);
            Object.Destroy(rig);
        }

        /// <summary>A frame of a card on the HUD's frame: its canvas drawn through a camera of the test's own, as the sting's capture is.</summary>
        private IEnumerator CaptureTheHud(Component card, string name)
        {
            const int width = 1600, height = 900;
            var rig = new GameObject("Nomination card camera", typeof(Camera));
            props.Add(rig);
            var camera = rig.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.07f, 0.10f, 1f);
            camera.transform.position = new Vector3(0f, 80f, 0f);
            var canvas = card.GetComponent<Canvas>();
            var previousMode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            yield return Render(camera, name, width, height, null);
            canvas.renderMode = previousMode;
            canvas.worldCamera = null;
            Object.Destroy(rig);
        }

        private static IEnumerator Render(Camera camera, string name, int width, int height, System.Action<Texture2D, Camera> inspect)
        {
            var texture = new RenderTexture(width, height, 24) { name = "Nomination card capture" };
            var readback = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = texture;
                Canvas.ForceUpdateCanvases();
                yield return null;
                camera.Render();
                RenderTexture.active = texture;
                readback.ReadPixels(new UnityEngine.Rect(0, 0, width, height), 0, 0);
                readback.Apply();
                var pixels = readback.GetPixels32();
                var distinct = new HashSet<int>();
                for (int i = 0; i < pixels.Length; i += 29) distinct.Add((pixels[i].r << 16) | (pixels[i].g << 8) | pixels[i].b);
                Assert.That(distinct.Count, Is.GreaterThan(8), "The capture '" + name + "' is nearly one flat colour, so the card did not draw.");
                if (Application.isBatchMode)
                {
                    var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", name + ".png"));
                    System.IO.File.WriteAllBytes(path, readback.EncodeToPNG());
                    Debug.Log("[Gamesim] nomination card capture -> " + path);
                }
                inspect?.Invoke(readback, camera);
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = null;
                Object.Destroy(readback);
                texture.Release();
                Object.Destroy(texture);
            }
        }

        /// <summary>Waits on the wall clock until <paramref name="done"/>, or <paramref name="seconds"/> at most.</summary>
        private static IEnumerator Until(System.Func<bool> done, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < until) yield return null;
        }
    }
}
