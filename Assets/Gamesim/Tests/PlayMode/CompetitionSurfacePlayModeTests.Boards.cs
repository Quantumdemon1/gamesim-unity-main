using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>The three boards redrawn: cards with symbols, a target that reads as one, a stamina panel.</summary>
    public sealed partial class CompetitionSurfacePlayModeTests
    {
        private Transform Card(int number) => screen.GetComponentsInChildren<Button>().Single(button => button.name == "Memory card " + number).transform;
        private static bool Showing(Transform card, string part) =>
            card.GetComponentsInChildren<Image>().Any(image => image.name == part);

        /// <summary>
        /// Face down, face up, no match and matched differ by mark as well as colour: a back mark,
        /// the symbol, a cross, a check. The caption keeps the card's name, and the tray and the
        /// feedback say what just happened.
        /// </summary>
        [UnityTest]
        public IEnumerator Memory_EachStateHasItsOwnMark()
        {
            CreateInput();
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Memory, 21, 3, CompetitionDefinitions.HouseMemory);
            screen.Show(run, "Head of Household · House Memory", "Player", true, i => run.Flip(i), () => {}, d => {}, () => {}, () => screen.Hide());
            yield return null; screen.AdvanceReady(4f); yield return null;
            int a = 0, b = Enumerable.Range(1, 15).First(i => run.Faces[i] == run.Faces[0]);
            int c = Enumerable.Range(1, 15).First(i => i != b && run.Faces[i] != run.Faces[0]);
            int d = Enumerable.Range(1, 15).First(i => i != b && i != c && run.Faces[i] != run.Faces[0] && run.Faces[i] != run.Faces[c]);
            run.Flip(a); run.Flip(b); screen.Refresh();
            Assert.That(Text("Attempt feedback").text, Does.StartWith("Pair found"));
            run.Flip(c); screen.Refresh();
            var single = Card(c + 1);
            Assert.That(Showing(single, "Card face"), Is.True, "One card turned is face up.");
            Assert.That(Showing(single, "Mismatch badge"), Is.False, "and not yet a miss.");
            Assert.That(Showing(single, "Matched badge"), Is.False);
            Assert.That(Showing(single, "Back mark"), Is.False);
            Assert.That(single.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Card " + (c + 1) + ": " + FaceWord(single)));
            run.Flip(d); screen.Refresh();

            foreach (int pair in new[] { a, b })
            {
                var card = Card(pair + 1);
                Assert.That(Showing(card, "Matched badge"), Is.True, card.name + " is matched.");
                Assert.That(Showing(card, "Mismatch badge"), Is.False);
                Assert.That(Showing(card, "Back mark"), Is.False);
                Assert.That(card.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Card " + (pair + 1) + ": " + FaceWord(card) + "  ·  MATCHED"));
            }
            var faceA = Card(a + 1).GetComponentsInChildren<Image>().Single(image => image.name == "Card face").sprite;
            Assert.That(faceA, Is.Not.Null, "A card shows its symbol.");
            Assert.That(Card(b + 1).GetComponentsInChildren<Image>().Single(image => image.name == "Card face").sprite, Is.SameAs(faceA), "A pair shares its symbol.");
            foreach (int miss in new[] { c, d })
            {
                var card = Card(miss + 1);
                Assert.That(Showing(card, "Mismatch badge"), Is.True, card.name + " is the miss.");
                Assert.That(Showing(card, "Matched badge"), Is.False);
            }
            int down = Enumerable.Range(0, 16).First(i => i != a && i != b && i != c && i != d);
            Assert.That(Showing(Card(down + 1), "Back mark"), Is.True, "A card face down shows its back.");
            Assert.That(Showing(Card(down + 1), "Card face"), Is.False);
            Assert.That(Card(down + 1).GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Card " + (down + 1)), "Its caption is its name, exactly.");
            Assert.That(Text("Progress").text, Is.EqualTo("1 / 8 pairs  ·  1 mistake (-0.15)"));
            Assert.That(Text("Attempt feedback").text, Does.StartWith("No match"));
            Assert.That(screen.GetComponentsInChildren<Image>().Count(image => image.name == "Pair symbol"), Is.EqualTo(1), "The tray lights the pair found.");

            run.Tick(1.1); screen.Refresh();
            Assert.That(Showing(Card(c + 1), "Mismatch badge"), Is.False, "The miss turns back down.");
            Assert.That(Showing(Card(c + 1), "Back mark"), Is.True);

            // One focus ring, on the card the keyboard is on.
            yield return KeyPress(Key.RightArrow); yield return KeyPress(Key.DownArrow);
            AssertSelected("Memory card 6");
            var rings = screen.GetComponentsInChildren<RectTransform>().Where(rect => rect.name == "Focus ring").ToArray();
            Assert.That(rings.Length, Is.EqualTo(1));
            Assert.That(rings[0].parent.name, Is.EqualTo("Memory card 6"));
            yield return KeyPress(Key.Tab);
            Assert.That(screen.GetComponentsInChildren<RectTransform>().Count(rect => rect.name == "Focus ring"), Is.Zero, "and none once it leaves the board.");
        }

        private static string FaceWord(Transform card)
        {
            string text = card.GetComponentInChildren<TMP_Text>().text;
            int colon = text.IndexOf(':'), dot = text.IndexOf("  ·  ", System.StringComparison.Ordinal);
            return text.Substring(colon + 2, (dot < 0 ? text.Length : dot) - colon - 2);
        }

        /// <summary>
        /// The target is a disc: its label says the window, its arrow the direction, its ring the
        /// window left; only the disc takes the click; and it is the board's size, never the text's.
        /// </summary>
        [UnityTest]
        public IEnumerator Reaction_TheTargetReadsAsATargetAndMarksWhereEachPressLanded()
        {
            CreateInput();
            float side = 0f;
            foreach (float scale in new[] { 1f, 1.2f })
            {
                screen.FontScale = scale;
                var run = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 17, 3, CompetitionDefinitions.SwitchbackSignals);
                screen.Show(run, "Veto · Switchback Signals", "Player", true, i => {}, () => {}, direction => run.Tap(direction), () => {}, () => screen.Hide(), run.MissPointer);
                yield return null; screen.AdvanceReady(4f); yield return null;
                while (!run.TargetLive) run.Tick(.01);
                screen.Refresh();
                Assert.That(Text("Progress").text, Does.Not.Contain("accuracy"), "The target still up is neither hit nor missed.");
                var target = screen.GetComponentsInChildren<Button>().Single(button => button.name == "Reaction target");
                var disc = target.GetComponent<Image>();
                Assert.That(disc.sprite, Is.SameAs(UiTheme.Circle()));
                Assert.That(disc.alphaHitTestMinimumThreshold, Is.GreaterThanOrEqualTo(.5f), "Only the disc takes the click.");
                Assert.That(target.GetComponentInChildren<TMP_Text>().text, Does.Contain("0.65 s"));
                var arrow = target.GetComponentsInChildren<Image>().Single(image => image.name == "Target arrow");
                float expected = run.TargetDirection == MiniGameRun.Direction.Up ? 90f : run.TargetDirection == MiniGameRun.Direction.Down ? 270f
                    : run.TargetDirection == MiniGameRun.Direction.Left ? 180f : 0f;
                Assert.That(Mathf.DeltaAngle(arrow.rectTransform.localEulerAngles.z, expected), Is.EqualTo(0f).Within(.5f), "The arrow points the target's way.");
                var window = target.GetComponentsInChildren<Image>().Single(image => image.name == "Target window");
                run.Tick(.3); screen.Refresh();
                Assert.That(window.fillAmount, Is.EqualTo((float)(run.TargetRemaining / run.TargetWindowSeconds)).Within(.01f));
                Assert.That(window.fillAmount, Is.EqualTo((.65f - .3f) / .65f).Within(.02f), "The window drains.");
                float size = ((RectTransform)target.transform).rect.width;
                if (side == 0f) side = size;
                else Assert.That(size, Is.EqualTo(side).Within(.5f), "The target is the board's size, not the text's.");

                // A wrong press leaves a red mark, which fades.
                var wrong = (MiniGameRun.Direction)(((int)run.TargetDirection + 1) % 4);
                run.Tap(wrong); screen.Refresh();
                var mark = screen.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Reaction mark");
                Assert.That(mark.GetComponentsInChildren<Image>().Single(image => image.name == "Mark ring").color.r, Is.EqualTo(UiTheme.Danger.r).Within(.01f));
                Assert.That(mark.GetSiblingIndex(), Is.LessThan(target.transform.GetSiblingIndex()), "under the next target.");
                float until = Time.realtimeSinceStartup + .7f;
                while (Time.realtimeSinceStartup < until) yield return null;
                Assert.That(mark.gameObject.activeInHierarchy, Is.False, "and is gone in half a second.");
            }
            // The scoring quarter is lit on the edge legend.
            var run2 = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 9, 3, CompetitionDefinitions.SignalSprint);
            screen.Show(run2, "Veto · Signal Sprint", "Player", true, i => {}, () => {}, direction => run2.Tap(direction), () => {}, () => screen.Hide(), run2.MissPointer);
            yield return null; screen.AdvanceReady(4f); yield return null;
            for (int n = 0; n < 6; n++)
            {
                while (!run2.TargetLive) run2.Tick(.01);
                screen.Refresh();
                foreach (var direction in new[] { "Up", "Right", "Down", "Left" })
                {
                    var word = screen.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Direction legend " + direction)
                        .GetComponentInChildren<TMP_Text>();
                    bool lit = word.color == UiTheme.Gold;
                    Assert.That(lit, Is.EqualTo(run2.TargetDirection.ToString() == direction), direction + " lit for a " + run2.TargetDirection + " target.");
                }
                run2.Tap(run2.TargetDirection); screen.Refresh();
            }
            Assert.That(screen.GetComponentsInChildren<Graphic>().Where(graphic => graphic.name.StartsWith("Direction") || graphic.transform.parent.name.StartsWith("Direction"))
                .Any(graphic => graphic.raycastTarget), Is.False, "The guides and the legend take no clicks.");
        }

        [UnityTest]
        public IEnumerator Reaction_OnlyTheDiscTakesTheClick()
        {
            CreateInput(); var run = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 77, 3, CompetitionDefinitions.SignalSprint);
            screen.Show(run, "Veto · Signal Sprint", "Player", true, i => {}, () => run.Tap(run.TargetDirection), direction => run.Tap(direction), () => {}, () => screen.Hide(), run.MissPointer);
            yield return null; screen.AdvanceReady(4f); yield return null;
            while (!run.TargetLive) run.Tick(.01);
            screen.Refresh(); yield return null;
            var rect = (RectTransform)screen.GetComponentsInChildren<Button>().Single(button => button.name == "Reaction target").transform;
            Vector2 corner = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.min + new Vector2(4f, 4f)));
            yield return ClickAt(corner);
            Assert.That(run.PointerMisses, Is.EqualTo(1), "The corner of the target's box is outside the disc: a miss.");
            Assert.That(run.Hits, Is.Zero);
        }

        /// <summary>
        /// Under reduced motion nothing on the board pops, pulses, squeezes or fades - GO, the last
        /// seconds, a card turning, a press's mark - and the screen's own beats ask for their sounds:
        /// a tick on each count, and the start sting once, at GO.
        /// </summary>
        [UnityTest]
        public IEnumerator ReducedMotion_NothingMovesAndTheBeatsAskForTheirSounds()
        {
            owner = new GameObject("Reduced motion test"); screen = CompetitionGameScreen.Attach(owner); screen.ReducedMotion = true;
            var cues = new System.Collections.Generic.List<HouseAudio.Cue>();
            screen.CueRequested += cue => cues.Add(cue);
            var memory = new MiniGameRun(CompetitionMiniGames.Kind.Memory, 21, 3, CompetitionDefinitions.HouseMemory);
            screen.Show(memory, "Veto · House Memory", "Player", true, i => memory.Flip(i), () => {}, d => {}, () => {}, () => {});
            yield return null;
            for (int i = 0; i < 6 && !screen.IsPlaying; i++) screen.AdvanceReady(.6f);
            Assert.That(screen.IsPlaying, Is.True);
            Assert.That(cues.Count(cue => cue == HouseAudio.Cue.CompetitionStart), Is.EqualTo(1), "The start sting, once, at GO.");
            Assert.That(cues.Last(), Is.EqualTo(HouseAudio.Cue.CompetitionStart));
            Assert.That(cues.Count(cue => cue == HouseAudio.Cue.Hover), Is.EqualTo(2), "A tick as the count falls to 2 and to 1.");
            Assert.That(Text("Countdown").rectTransform.localScale, Is.EqualTo(Vector3.one), "GO does not pop.");
            memory.Flip(0); screen.Refresh();
            var art = Card(1).GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Card art");
            Assert.That(art.localScale.x, Is.EqualTo(1f).Within(1e-4f), "A card turns at once.");

            var reaction = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 21, 3, CompetitionDefinitions.SignalSprint);
            screen.Show(reaction, "Veto · Signal Sprint", "Player", true, i => {}, () => {}, direction => reaction.Tap(direction), () => {}, () => {});
            yield return null; screen.AdvanceReady(4f);
            while (!reaction.TargetLive) reaction.Tick(.01);
            screen.Refresh();
            reaction.Tap((MiniGameRun.Direction)(((int)reaction.TargetDirection + 1) % 4)); screen.Refresh();
            var mark = screen.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Reaction mark");
            for (int frame = 0; frame < 5; frame++) yield return null;
            Assert.That(mark.GetComponentsInChildren<Image>().Single(image => image.name == "Mark ring").color.a, Is.EqualTo(1f).Within(1e-4f), "A mark does not fade.");
            Assert.That(mark.localScale, Is.EqualTo(Vector3.one), "or grow.");

            var endurance = new MiniGameRun(CompetitionMiniGames.Kind.Endurance, 21, 3, CompetitionDefinitions.HoldYourGround);
            screen.Show(endurance, "Veto · Hold Your Ground", "Player", true, i => {}, () => {}, d => {}, () => {}, () => {});
            yield return null; screen.AdvanceReady(4f);
            endurance.Tick(30 - 5.2); screen.Refresh(); yield return null;
            int ticks = cues.Count(cue => cue == HouseAudio.Cue.Hover);
            endurance.Tick(.4); screen.Refresh(); yield return null; yield return null;
            Assert.That(cues.Count(cue => cue == HouseAudio.Cue.Hover), Is.EqualTo(ticks + 1), "The fifth-last second ticks.");
            Assert.That(Text("Competition clock").rectTransform.localScale, Is.EqualTo(Vector3.one), "and the digits do not pulse.");
        }

        /// <summary>
        /// The stamina panel holds both numbers that matter - the grip, and the effort banked towards
        /// full marks - and warns before the grip runs out; Pressure Cooker's waves are on a timeline.
        /// </summary>
        [UnityTest]
        public IEnumerator Endurance_ThePanelShowsGripEffortAndTheWavesToCome()
        {
            owner = new GameObject("Endurance panel test"); screen = CompetitionGameScreen.Attach(owner);
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Endurance, 17, 3, CompetitionDefinitions.PressureCooker);
            screen.Show(run, "Head of Household · Pressure Cooker", "Player", true, i => {}, () => {}, d => {}, () => run.SetHolding(!run.Holding), () => {});
            yield return null; screen.AdvanceReady(4f); screen.Refresh();
            Assert.That(Text("Grip rate").text, Is.EqualTo("steady"), "A full grip is not refilling.");
            Assert.That(Text("Endurance headline").text, Is.EqualTo("RESTING  ·  GRIP FULL"));
            run.SetHolding(true); run.Tick(5); screen.Refresh();
            var banked = Rect("Effort banked");
            Assert.That(banked.anchorMax.x, Is.EqualTo((float)(run.Held / 19.5)).Within(.01f), "Effort banked towards 19.5 s.");
            Assert.That(Text("Effort value").text, Does.Contain("19.5"));
            Assert.That(Text("Grip value").text, Does.StartWith("Grip ").And.Contain("WAVE"), "The grip line keeps its words.");
            var waves = screen.GetComponentsInChildren<RectTransform>().Where(rect => rect.name.StartsWith("Pressure wave ")).OrderBy(rect => rect.anchorMin.x).ToArray();
            Assert.That(waves.Length, Is.EqualTo(5));
            float[] starts = { .15f, .35f, .55f, .75f, .95f };
            for (int i = 0; i < 5; i++) Assert.That(waves[i].anchorMin.x, Is.EqualTo(starts[i]).Within(.01f), "Wave " + (i + 1) + " where the rules put it.");
            Assert.That(Rect("Pressure playhead").anchorMin.x, Is.EqualTo(5f / 30f).Within(.01f), "The playhead is the clock.");
            Assert.That(waves[0].GetComponent<Image>().color.r, Is.EqualTo(UiTheme.Danger.r).Within(.01f), "The wave under way is red.");
            while (run.Meter >= 25 && !run.Finished) run.Tick(.05);
            screen.Refresh();
            var warning = screen.GetComponentsInChildren<TMP_Text>().Single(label => label.name == "Warning words");
            Assert.That(warning.text, Does.StartWith("LOW GRIP"), "A low grip says so before it is gone.");
            Assert.That(Rect("Grip remaining").GetComponent<Image>().color, Is.EqualTo(UiTheme.Danger));
            run.SetHolding(false); run.Tick(1); screen.Refresh();
            Assert.That(screen.GetComponentsInChildren<TMP_Text>().Any(label => label.name == "Warning words"), Is.False, "Recovering, there is nothing to warn of.");
            Assert.That(Text("Grip rate").text, Is.EqualTo("+28.0 %/s"));
            var hold = screen.GetComponentsInChildren<Button>().Single(button => button.name == "Toggle grip");
            Assert.That(hold.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Hold to earn effort"), "The caption is the contract.");

            // Version 1 inverts the meter: holding refills it and letting go drains it.
            var legacy = new MiniGameRun(CompetitionMiniGames.Kind.Endurance, 17, 1);
            screen.Show(legacy, "Veto", "Player", true, i => {}, () => {}, d => {}, () => legacy.SetHolding(!legacy.Holding), () => {});
            yield return null; screen.AdvanceReady(4f);
            legacy.SetHolding(true); legacy.Tick(1); screen.Refresh();
            var caption = screen.GetComponentsInChildren<Button>().Single(button => button.name == "Toggle grip").GetComponentInChildren<TMP_Text>();
            Assert.That(caption.text, Is.EqualTo("Let go (grip drains)"), "Letting go in version 1 drains the grip, and the button says so.");
            legacy.SetHolding(false); legacy.Tick(1); screen.Refresh();
            Assert.That(Text("Endurance headline").text, Is.EqualTo("LET GO  ·  GRIP DRAINING"));
            Assert.That(caption.text, Is.EqualTo("Hold to earn effort"));
        }

        /// <summary>
        /// The standings a commit opens on: a face on every row, the player's own row outlined, the
        /// game's name as the heading, the player's attempt said, Continue drawn as the primary - and
        /// the larger text honoured, which the card used to ignore.
        /// </summary>
        [UnityTest]
        public IEnumerator Result_EveryRowHasAFaceAndThePlayersAttemptIsSaid()
        {
            CreateInput(); screen.Hide();
            float winnerAtOne = 0f;
            foreach (float scale in new[] { 1f, 1.2f })
            {
                var result = CompetitionResult.Attach(owner); result.FontScale = scale;
                try
                {
                    var rows = new[]
                    {
                        new CompetitionResult.Standing("Maya", 6.6, true, false, null),
                        new CompetitionResult.Standing("You", 6.4, false, true, null),
                        new CompetitionResult.Standing("Riley", 5.9, false, false, null),
                    };
                    result.Play("Head of Household · Pressure Cooker", "Endurance", 2, rows, true, null, "Your attempt  ·  held 12.4 s  ·  performance 64%");
                    yield return null;
                    var texts = result.GetComponentsInChildren<TMP_Text>();
                    Assert.That(texts.Single(text => text.name == "Award").text, Is.EqualTo("PRESSURE COOKER"), "The game's own name heads the card.");
                    Assert.That(texts.Single(text => text.name == "Week").text, Does.Contain("HEAD OF HOUSEHOLD"));
                    Assert.That(texts.Single(text => text.name == "Player attempt").text, Does.Contain("held 12.4 s"));
                    Assert.That(result.GetComponentsInChildren<RectTransform>().Count(rect => rect.name == "Row portrait"), Is.EqualTo(3), "A face on every row.");
                    var mine = result.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Standing 2");
                    Assert.That(mine.Find("Border"), Is.Not.Null, "Your row is outlined.");
                    Assert.That(mine.Find("Border").GetComponent<Image>().color, Is.EqualTo(UiTheme.Edge(UiTheme.Emphasis.Active)));
                    Assert.That(result.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Standing 3").Find("Border"), Is.Null);
                    var go = result.GetComponentsInChildren<Button>().Single(button => button.name == "Continue from competition results");
                    Assert.That(go.GetComponent<Image>().sprite != null && go.GetComponent<Image>().sprite.name.Contains("button_primary"), Is.True,
                        "Continue is drawn as the primary.");
                    Assert.That(result.GetComponentsInChildren<Button>().Length, Is.EqualTo(2));
                    float winner = texts.Single(text => text.name == "Winner").fontSizeMax;
                    if (scale == 1f) winnerAtOne = winner;
                    else Assert.That(winner, Is.GreaterThan(winnerAtOne), "The larger text is honoured.");
                    foreach (var text in texts.Where(text => text.transform.IsChildOf(result.transform)))
                    { text.ForceMeshUpdate(); Assert.That(text.isTextOverflowing, Is.False, text.name + " at " + scale); }
                }
                finally { Object.Destroy(result.gameObject); }
                yield return null;
            }
        }
    }
}
