using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
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
    /// The veto's player selection on one screen (PACK8-PASS-PLAN B2, the owner's mockup 76): the
    /// three who play by right, the bag with a chip for each seat the draw fills, and the eligible
    /// pool, in one row that fits the strategy stage without a scroll in a house of seven or sixteen
    /// at either text size; "Continue episode" still draws, wearing 'REVEAL THE DRAW' beside its
    /// caption. After the commit the chips turn into the faces they drew, on a card that reads the
    /// committed lineup and writes nothing - in play only: a batch run and reduced motion keep the
    /// field card, so the audited walk and every card test see what they always saw.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>A house of <paramref name="houseguests"/> at the veto's draw: the first houseguest at the head, the next two on the block.</summary>
        private IEnumerator InstallVetoDraw(uint seed, int houseguests)
        {
            var state = FullHouse(seed, houseguests);
            var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            state.phase = EpisodePhase.VetoSelection;
            state.hohId = npcs[0];
            state.nominees = new List<string> { npcs[1], npcs[2] };
            Assert.That(EpisodeValidation.TryValidate(state, out var invalid), Is.True, invalid);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
        }

        /// <summary>The pill on a houseguest's card under <paramref name="root"/>: its word, "" for a card with none, null for no card.</summary>
        private static string PillOn(RectTransform root, EpisodeState state, string id)
        {
            var card = root.GetComponentsInChildren<RectTransform>().FirstOrDefault(rect => rect.name == "Face · " + state.Find(id).name);
            if (card == null) return null;
            var pill = card.GetComponentsInChildren<RectTransform>().FirstOrDefault(rect => rect.name == "Role");
            return pill != null ? pill.GetComponentInChildren<TMP_Text>().text : "";
        }

        private static int FacesUnder(RectTransform root) =>
            root.GetComponentsInChildren<RectTransform>().Count(rect => rect.name.StartsWith("Face · "));

        /// <summary>
        /// A house of eight: the Head of Household and the block by right with their pills, the other
        /// five in the pool with none, three chips in the bag, the one line and the column headings
        /// the mockup has, nothing on the row a control, the footer's strip saying what the way on does
        /// and the way on wearing the mockup's words beside the caption it is found by. Pressed, it
        /// draws three of the five, and in a batch run the field card plays as it always did.
        /// </summary>
        [UnityTest]
        public IEnumerator VetoDraw_AHouseOfEightDrawsThreeChipsFromTheEligible()
        {
            yield return InstallVetoDraw(48, 8);
            yield return OpenStation();
            yield return null;
            var state = director.Snapshot;
            var board = VetoDraw.Before(state);
            Assert.That(board.EveryonePlays, Is.False, "Eight is a house with a draw.");
            Assert.That(EpisodeEngine.LapsingOnAdvance(state), Is.Empty, "The fixture lets no storyline lapse, so the footer's strip is the draw's.");
            AssertOnTheStrategyStage("Continue episode", "The veto's draw in a house of eight");

            Assert.That(ActiveRect(EpisodeHud.CeremonyTitleName).GetComponent<TMP_Text>().text, Is.EqualTo("Power of Veto Player Selection"),
                "The draw names itself under the name a test has always found it by,");
            Assert.That(ActiveRect(EpisodeHud.VetoDrawLineName).GetComponent<TMP_Text>().text,
                Is.EqualTo("6 players compete: HoH, both nominees, and 3 players drawn at random."), "over one line.");

            var row = ActiveRect(EpisodeHud.VetoDrawBoardName);
            var playing = ActiveRect(EpisodeHud.DrawPlayingName);
            var eligible = ActiveRect(EpisodeHud.DrawEligibleName);
            Assert.That(row != null && playing != null && eligible != null, Is.True, "The row has its three columns.");
            Assert.That(PillOn(playing, state, state.hohId), Is.EqualTo("HOH"), "The Head of Household plays by right,");
            foreach (var nominee in state.nominees) Assert.That(PillOn(playing, state, nominee), Is.EqualTo("NOM"), "and so do both nominees.");
            Assert.That(FacesUnder(playing), Is.EqualTo(3), "Three play by right.");
            foreach (var id in board.Eligible) Assert.That(PillOn(eligible, state, id), Is.EqualTo(""), state.Find(id).name + " is in the pool, with no pill.");
            Assert.That(FacesUnder(eligible), Is.EqualTo(5), "The other five are eligible for the draw.");

            var bag = ActiveRect(EpisodeHud.ChipBagName);
            Assert.That(bag.GetComponentsInChildren<RectTransform>().Count(rect => rect.name == "Chip"), Is.EqualTo(3), "Three chips in the bag.");
            var headings = row.GetComponentsInChildren<TMP_Text>().Select(label => label.text).ToArray();
            Assert.That(headings, Does.Contain("AUTOMATICALLY PLAYING").And.Contain("DRAW 3 PLAYERS").And.Contain("ELIGIBLE FOR DRAW")
                .And.Contain("3 chips to draw"), "The mockup's headings, and how many chips there are.");
            Assert.That(row.GetComponentsInChildren<Selectable>(), Is.Empty, "Nothing on the row is a control: the draw is the way on.");
            AssertEveryLabelDraws(row, "The veto's draw");
            AssertVetoDrawHeadingsComplete(row, (RectTransform)ActiveRect("Episode content").parent,
                ActiveRect("Episode panel"), director.TextSize, "The veto's draw in a house of eight");

            var wayOn = FindButton("Continue episode");
            Assert.That(wayOn.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Continue episode"), "The caption stays the way on's first label,");
            var headline = wayOn.GetComponentsInChildren<TMP_Text>().Single(label => label.name == EpisodeHud.DrawWayOnHeadlineName);
            Assert.That(headline.text, Is.EqualTo("REVEAL THE DRAW"), "with the mockup's words beside it, never in it.");
            AssertEveryLabelDraws((RectTransform)wayOn.transform, "The way on");
            var strip = ActiveRect(EpisodeHud.StrategyStripName);
            Assert.That(strip, Is.Not.Null, "The footer's strip says what the way on does.");
            var note = strip.GetComponentsInChildren<TMP_Text>().Single();
            Assert.That(note.name, Is.EqualTo(EpisodeHud.UpNextName));
            Assert.That(note.text, Is.EqualTo("Draw 3 players from the eligible pool to complete the Veto competition."));
            Assert.That(note.color, Is.EqualTo(UiTheme.Accent), "in the accent, not the warning colour.");

            var takeover = SceneComponents<CeremonyTakeover>().Single();
            ButtonWithCaption("Continue episode").onClick.Invoke();
            yield return null;
            var drawn = director.Snapshot;
            Assert.That(drawn.phase, Is.EqualTo(EpisodePhase.Veto), "Continue draws, and the veto is next.");
            Assert.That(drawn.vetoPlayers, Has.Count.EqualTo(6));
            var added = drawn.vetoPlayers.Where(id => !board.ByRight.Contains(id)).ToList();
            Assert.That(added, Has.Count.EqualTo(3), "Three chips, three drawn,");
            Assert.That(added.All(board.Eligible.Contains), Is.True, "all from the pool the screen showed.");
            if (Application.isBatchMode)
            {
                Assert.That(takeover.PlayingKind, Is.EqualTo(CeremonyTakeover.VetoSelectionKind), "A batch run keeps the field card,");
                Assert.That(director.DrawRevealCard == null || !director.DrawRevealCard.IsPlaying, Is.True, "and the reveal stays for play.");
            }
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The owner's ask: the draw fits one screen. A house of seven and a house of sixteen - the
        /// largest - at both text sizes, on the strategy stage with its one way on: the column does
        /// not scroll, every card sits inside the scroll's window and no narrower than a name reads
        /// at, and every label draws.
        /// </summary>
        [UnityTest]
        public IEnumerator VetoDraw_FitsTheFrameWithoutScrollingInHousesOfSevenAndSixteen()
        {
            foreach (int houseguests in new[] { 7, 16 })
            {
                yield return InstallVetoDraw(49, houseguests);
                yield return AtEachTextSize(textSize =>
                {
                    bool larger = textSize != TextSize.Standard;
                    string where = "The veto's draw in a house of " + houseguests + (larger ? " " + TextScales.At(textSize) : "");
                    AssertOnTheStrategyStage("Continue episode", where);
                    var content = ActiveRect("Episode content");
                    var viewport = (RectTransform)content.parent;
                    Assert.That(content.rect.height, Is.LessThanOrEqualTo(viewport.rect.height + .5f),
                        where + " scrolls: " + content.rect.height.ToString("0") + " in " + viewport.rect.height.ToString("0") + ".");
                    var window = ScreenRect(viewport);
                    var row = ActiveRect(EpisodeHud.VetoDrawBoardName);
                    var cards = row.GetComponentsInChildren<RectTransform>().Where(rect => rect.name.StartsWith("Face · ")).ToList();
                    Assert.That(cards, Has.Count.EqualTo(houseguests), where + " shows the whole house.");
                    float scale = TextSizes.Scale(textSize);
                    foreach (var card in cards)
                    {
                        AssertInside(window, card, where + "'s '" + card.name + "'");
                        Assert.That(card.rect.width, Is.GreaterThanOrEqualTo(EpisodeHud.FaceCardFloor * scale - .5f),
                            where + "'s '" + card.name + "' is narrower than a name reads at.");
                    }
                    AssertEveryLabelDraws(row, where);
                    AssertVetoDrawHeadingsComplete(row, viewport, ActiveRect("Episode panel"), textSize, where);
                    var wayOn = (RectTransform)FindButton("Continue episode").transform;
                    Assert.That(wayOn.GetComponentsInChildren<TMP_Text>().Single(label => label.name == EpisodeHud.DrawWayOnHeadlineName).text,
                        Is.EqualTo("REVEAL THE DRAW"), where + "'s way on wears the draw it makes.");
                    AssertEveryLabelDraws(wayOn, where + "'s way on");
                });
                // On the 16:9 frame as on the batch canvas's 4:3, at both text sizes: the pool's
                // seventh card at sixteen - the last of its first row - inside the scroll's window
                // and the panel (UI-UX-PASS-PLAN T0; the capture read it as cut, and its pixels
                // put it 46 units short of the shell's edge).
                if (houseguests != 16) continue;
                foreach (var (textSize, larger) in TextScales.Steps)
                {
                    yield return ApplyTextSize(textSize);
                    yield return OpenStation();
                    yield return null;
                    string size = larger ? " " + TextScales.At(textSize) : "";
                    var headingFrames = new HashSet<string>(System.StringComparer.Ordinal);
                    yield return AtBothFrames(frame =>
                    {
                        string where = "The draw of sixteen" + size + " on the " + frame + " frame";
                        var panel = LastActive("Episode panel");
                        var content = LastActive("Episode content");
                        var viewport = (RectTransform)content.parent;
                        var row = LastActive(EpisodeHud.VetoDrawBoardName);
                        Assert.That(row, Is.Not.Null, where + " has its row.");
                        var cards = row.GetComponentsInChildren<RectTransform>().Where(rect => rect.name.StartsWith("Face · ")).ToList();
                        Assert.That(cards, Has.Count.EqualTo(16), where + " shows the whole house.");
                        foreach (var card in cards)
                        {
                            AssertWithin(OnTheHud(viewport), OnTheHud(card), where + "'s '" + card.name + "'", "the scroll's window");
                            AssertWithin(OnTheHud(panel), OnTheHud(card), where + "'s '" + card.name + "'", "the panel");
                        }
                        Assert.That(content.rect.height, Is.LessThanOrEqualTo(viewport.rect.height + .5f),
                            where + " scrolls: " + content.rect.height.ToString("0") + " in " + viewport.rect.height.ToString("0") + ".");
                        AssertEveryLabelDraws(row, where);
                        if (textSize == TextSize.Largest)
                        {
                            Assert.That(headingFrames.Add(frame), Is.True, "Each required Largest frame is observed once.");
                            AssertVetoDrawHeadingsComplete(row, viewport, panel, textSize, where);
                        }
                    });
                    if (textSize == TextSize.Largest)
                        Assert.That(headingFrames, Is.EquivalentTo(new[] { "16:9", "4:3" }), "Neither required full-heading frame may be skipped.");
                    director.ClosePanels();
                    yield return null;
                }
                yield return ApplyTextSize(false);
            }
            // The largest house, photographed for review: how the row splits between the three by
            // right and a pool of thirteen is a judgement the owner makes by eye, and the strategy
            // stage's own capture is the house of six, where nobody is drawn.
            if (Application.isBatchMode)
            {
                yield return OpenStation();
                yield return CaptureFraming("strategy-veto-draw-sixteen");
                director.ClosePanels();
                yield return null;
            }
        }

        /// <summary>
        /// The reveal, asked for in a batch run as a player's house plays it: the commit makes the
        /// draw, then the chips turn over one at a time into the faces drawn, in the seeded order and
        /// in place of the field card, with the chrome stood aside - the status line names the drawn
        /// the moment the commit lands. A press turns every chip still in the bag, a second ends the
        /// card, and nothing it did wrote to the season. The next beat takes it down.
        /// </summary>
        [UnityTest]
        public IEnumerator VetoDraw_TheRevealTurnsTheChipsIntoTheDrawnAndWritesNothing()
        {
            yield return InstallVetoDraw(50, 8);
            director.DrawRevealsInBatchRuns = true;
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            var hud = director.GetComponentInChildren<EpisodeHud>();
            yield return OpenStation();
            yield return null;
            var eligible = VetoDraw.Before(director.Snapshot).Eligible;
            ButtonWithCaption("Continue episode").onClick.Invoke();

            // The card opens inside the commit, so its opening is read before the next frame. It runs
            // on the unscaled clock, which Time.captureDeltaTime does not pin, and the frame after a
            // commit carries the save, the render and the panel closing for the competition. The card
            // no longer counts that frame (VetoDraw_TheRevealsTimeStartsWhenItIsDrawn), and its
            // opening is read inside the commit all the same.
            var committed = director.Snapshot;
            var reveal = director.DrawRevealCard;
            Assert.That(reveal != null && reveal.IsPlaying, Is.True, "The draw plays as chips turning into faces,");
            Assert.That(takeover.IsPlaying, Is.False, "in place of the field card, not under it.");
            Assert.That(reveal.Order, Is.EqualTo(VetoDraw.Drawn(committed)), "The chips turn in the seeded order,");
            Assert.That(reveal.Order.OrderBy(id => id), Is.EqualTo(committed.vetoPlayers
                .Where(id => id != committed.hohId && !committed.nominees.Contains(id)).OrderBy(id => id)), "and they are the committed draw,");
            Assert.That(reveal.Order.All(eligible.Contains), Is.True, "every one from the pool the screen showed.");
            Assert.That(reveal.ChipsOut, Is.Zero, "Every chip is in the bag as the card opens.");
            // The hold is read a frame on: the ceremonies' tick lets it go as soon as nothing it counts is playing.
            yield return null;
            Assert.That(hud.IsHeldForReveal, Is.True, "The chrome stands aside: the status line names the drawn.");

            // Past the read-first delay and the first chip, on the wall clock.
            float until = Time.realtimeSinceStartup + 4f;
            while (reveal.IsPlaying && reveal.ChipsOut < 1 && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(reveal.ChipsOut, Is.GreaterThanOrEqualTo(1), "The first chip comes out on its own.");
            string first = committed.Find(reveal.Order[0]).name;
            until = Time.realtimeSinceStartup + 2f;
            while (!reveal.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == VetoDrawReveal.DrawnPrefix + first)
                   && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(reveal.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == VetoDrawReveal.DrawnPrefix + first), Is.True,
                "and turns into " + first + ".");

            yield return PressKey(Key.Enter);
            Assert.That(reveal.IsPlaying && reveal.ShowingField, Is.True, "A press turns every chip still in the bag and shows the field;");
            Assert.That(reveal.ChipsOut, Is.EqualTo(3));
            foreach (var id in reveal.Order)
                Assert.That(reveal.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == VetoDrawReveal.DrawnPrefix + committed.Find(id).name),
                    Is.True, committed.Find(id).name + " is up with the field.");
            Assert.That(reveal.GetComponentsInChildren<RectTransform>().Count(rect => rect.name.StartsWith(VetoDrawReveal.PlayingPrefix)), Is.EqualTo(3),
                "beside the three who play by right.");
            Assert.That(reveal.GetComponentsInChildren<TMP_Text>().Single(label => label.name == VetoDrawReveal.ProgressName).text,
                Is.EqualTo(VetoDrawReveal.FieldCaption));
            AssertEveryLabelDraws((RectTransform)reveal.transform, "The draw's reveal");
            Assert.That(director.Snapshot.revision, Is.EqualTo(committed.revision), "The card commits nothing,");
            Assert.That(director.Snapshot.vetoPlayers, Is.EqualTo(committed.vetoPlayers), "and the field is the one saved.");

            yield return PressKey(Key.Enter);
            Assert.That(reveal.IsPlaying, Is.False, "A press on the field ends the card.");
            yield return null;
            Assert.That(hud.IsHeldForReveal, Is.False, "The chrome comes back when it is down.");

            // Played again from the same save, it turns the same chips in the same order; the next
            // beat takes it down.
            yield return InstallVetoDraw(50, 8);
            director.DrawRevealsInBatchRuns = true;
            yield return OpenStation();
            yield return null;
            ButtonWithCaption("Continue episode").onClick.Invoke();
            yield return null;
            reveal = director.DrawRevealCard;
            Assert.That(reveal.IsPlaying, Is.True);
            Assert.That(reveal.Order, Is.EqualTo(VetoDraw.Drawn(committed)), "A reload shows the same draw in the same order.");
            var result = director.Submit(NextCommand(director.Snapshot));
            Assert.That(result.accepted, Is.True, result.reason);
            yield return null;
            Assert.That(reveal.IsPlaying, Is.False, "The veto's result takes the draw's card down.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The draw's time starts when it is drawn. The commit plays the card, and its frame - the
        /// commit, its save, the house's render and the card's own build - is not the card's however
        /// long it runs: a frame of 1.6 seconds, past the fade and the beat before the first chip, with
        /// an Enter landing in it, leaves every chip in the bag and the card up.
        /// </summary>
        [UnityTest]
        public IEnumerator VetoDraw_TheRevealsTimeStartsWhenItIsDrawn()
        {
            yield return InstallVetoDraw(50, 8);
            director.DrawRevealsInBatchRuns = true;
            yield return OpenStation();
            yield return null;
            if (testKeyboard == null) testKeyboard = InputSystem.AddDevice<Keyboard>();
            ButtonWithCaption("Continue episode").onClick.Invoke();
            var reveal = director.DrawRevealCard;
            Assert.That(reveal != null && reveal.IsPlaying, Is.True, "The draw plays.");
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Enter));
            System.Threading.Thread.Sleep(1600);
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            // One frame of the card's own time, with the Enter let go: the first it counts.
            yield return null;

            Assert.That(reveal.IsPlaying, Is.True, "The draw is still up,");
            Assert.That(reveal.ShowingField, Is.False, "and the Enter that landed before it was drawn has not turned every chip.");
            Assert.That(reveal.ChipsOut, Is.Zero, "Every chip is still in the bag.");
            reveal.Cancel();
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// Where the reveal does not play, the field card does, so the beat is never silent: a batch
        /// run that has not asked for it, reduced motion, and a house of six, where nobody is drawn.
        /// </summary>
        [UnityTest]
        public IEnumerator VetoDraw_BatchRunsReducedMotionAndSixKeepTheFieldCard()
        {
            foreach (var (houseguests, ask, reduced, why) in new[]
            {
                (8, false, false, "A batch run that has not asked"),
                (8, true, true, "Reduced motion"),
                (6, true, false, "A house of six, where nobody is drawn,"),
            })
            {
                // Outside a batch run the house plays the reveal whether asked or not.
                if (!ask && !Application.isBatchMode) continue;
                yield return InstallVetoDraw(51, houseguests);
                if (!ask) Assert.That(director.DrawRevealsInBatchRuns, Is.False, "The default: the reveal is for play, as a stage is.");
                director.DrawRevealsInBatchRuns = ask;
                typeof(EpisodeDirector).GetField("reducedMotion", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(director, reduced);
                var takeover = SceneComponents<CeremonyTakeover>().Single();
                yield return OpenStation();
                yield return null;
                AssertVetoDrawHeadingsComplete(ActiveRect(EpisodeHud.VetoDrawBoardName), (RectTransform)ActiveRect("Episode content").parent,
                    ActiveRect("Episode panel"), director.TextSize, why + " shows the veto draw");
                ButtonWithCaption("Continue episode").onClick.Invoke();
                yield return null;
                Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.Veto));
                Assert.That(takeover.PlayingKind, Is.EqualTo(CeremonyTakeover.VetoSelectionKind), why + " keeps the field card,");
                Assert.That(director.DrawRevealCard == null || !director.DrawRevealCard.IsPlaying, Is.True, why + " plays no reveal.");
                typeof(EpisodeDirector).GetField("reducedMotion", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(director, false);
                director.ClosePanels();
                yield return null;
            }
        }

        /// <summary>Every heading is complete at its requested type size, including inactive/missing-label failures.</summary>
        private void AssertVetoDrawHeadingsComplete(RectTransform row, RectTransform viewport, RectTransform panel, TextSize textSize, string where)
        {
            Assert.That(row != null && viewport != null && panel != null, Is.True, where + " has complete heading bounds.");
            var board = VetoDraw.Before(director.Snapshot);
            var expected = new List<string> { VetoDraw.PlayingHeading(board), VetoDraw.DrawHeading(board) };
            if (board.Eligible.Count > 0) expected.Add(VetoDraw.EligibleHeading);
            var headings = row.GetComponentsInChildren<TMP_Text>(true).Where(label => label.name == "Column heading").ToArray();
            Assert.That(headings, Has.Length.EqualTo(expected.Count), where + " has every required heading and no hidden duplicate.");
            Canvas.ForceUpdateCanvases();
            var boxes = new List<Rect>();
            foreach (string copy in expected)
            {
                Assert.That(copy, Is.Not.Null.And.Not.Empty, where + " has required heading copy.");
                var found = headings.Where(label => label.text == copy).ToArray();
                Assert.That(found, Has.Length.EqualTo(1), where + " has exactly one complete '" + copy + "'.");
                var label = found[0];
                Assert.That(label.enabled && label.gameObject.activeInHierarchy, Is.True, where + " does not skip a hidden '" + copy + "'.");
                label.ForceMeshUpdate(true);
                Assert.That(label.enableAutoSizing, Is.False, where + " cannot shrink the heading to license its allocation.");
                Assert.That(label.fontSize, Is.EqualTo(Mathf.RoundToInt(15f * TextSizes.Scale(textSize))).Within(.01f), where + " retains requested heading type.");
                Assert.That(label.isTextOverflowing || label.isTextTruncated, Is.False, where + " does not cut '" + copy + "'.");
                Assert.That(label.textInfo.lineCount, Is.EqualTo(1), where + " keeps '" + copy + "' on one complete line.");
                var glyphs = label.textInfo.characterInfo.Take(label.textInfo.characterCount).Where(character => character.isVisible).ToArray();
                Assert.That(glyphs.Select(character => character.character).ToArray(), Is.EqualTo(copy.Where(character => !char.IsWhiteSpace(character)).ToArray()),
                    where + " draws the full character sequence of '" + copy + "', not an ellipsis or fragment.");
                var rect = OnTheHud(label.rectTransform);
                Assert.That(rect.width > 0f && rect.height > 0f, Is.True, where + " has a real box for '" + copy + "'.");
                AssertWithin(OnTheHud(row), rect, copy, "the draw row");
                AssertWithin(OnTheHud(viewport), rect, copy, "the viewport");
                AssertWithin(OnTheHud(panel), rect, copy, "the panel");
                foreach (var glyph in glyphs)
                {
                    var drawn = GlyphRect(label, glyph);
                    AssertWithin(rect, drawn, copy + "'s glyph '" + glyph.character + "'", "its complete heading allocation");
                    AssertWithin(OnTheHud(row), drawn, copy + "'s glyph '" + glyph.character + "'", "the draw row");
                    AssertWithin(OnTheHud(viewport), drawn, copy + "'s glyph '" + glyph.character + "'", "the viewport");
                    AssertWithin(OnTheHud(panel), drawn, copy + "'s glyph '" + glyph.character + "'", "the panel");
                }
                boxes.Add(rect);
            }
            for (int i = 0; i < boxes.Count; i++)
                for (int j = i + 1; j < boxes.Count; j++)
                    Assert.That(boxes[i].Overlaps(boxes[j]), Is.False, where + " keeps its complete headings separate.");
            var parts = row.GetComponentsInChildren<RectTransform>(true);
            var rules = parts.Where(part => part.name == "Column rule").ToArray();
            Assert.That(rules, Has.Length.EqualTo(expected.Count), where + " retains every heading's rule.");
            foreach (var rule in rules)
            {
                Assert.That(rule.gameObject.activeInHierarchy, Is.True, where + " has an active heading rule.");
                var rect = OnTheHud(rule);
                AssertWithin(OnTheHud(row), rect, "the column rule", "the draw row");
                AssertWithin(OnTheHud(viewport), rect, "the column rule", "the viewport");
                boxes.Add(rect);
            }
            Assert.That(parts.Count(part => part.name == "Draw area"), Is.EqualTo(1), where + " has its complete bag frame.");
            Assert.That(parts.Count(part => part.name == "Bag"), Is.EqualTo(1), where + " has its bag image.");
            Assert.That(parts.Count(part => part.name == "Chips line"), Is.EqualTo(1), where + " has its chips copy.");
            var area = parts.Single(part => part.name == "Draw area");
            var frameImages = area.GetComponentsInChildren<Image>(true).Where(image => image.transform.parent == area
                && (image.name == "Art" || image.name == EndScreenKit.DrawnFrameName)).ToArray();
            Assert.That(frameImages, Has.Length.EqualTo(1), where + " retains exactly its actual packed/drawn frame image.");
            var frame = frameImages[0];
            Assert.That(frame.enabled && frame.gameObject.activeInHierarchy, Is.True, where + " cannot hide its frame quad.");
            var frameBounds = OnTheHud(frame.rectTransform);
            Assert.That(frameBounds.width > 0f && frameBounds.height > 0f, Is.True, where + " has a real frame image quad.");
            AssertWithin(OnTheHud(row), frameBounds, "the actual frame image", "the draw row");
            AssertWithin(OnTheHud(viewport), frameBounds, "the actual frame image", "the viewport");
            AssertWithin(OnTheHud(panel), frameBounds, "the actual frame image", "the panel");
            foreach (var box in boxes)
                Assert.That(box.Overlaps(frameBounds), Is.False, where + " keeps full heading copy/rules clear of the actual frame image quad.");
            var occupied = parts.Where(part => part.name.StartsWith("Face · ") || part.name == "Draw area"
                || part.name == "Bag" || part.name == "Chips line" || part.name == "Chip").ToArray();
            foreach (var part in occupied)
                foreach (var box in boxes)
                    Assert.That(box.Overlaps(OnTheHud(part)), Is.False, where + " keeps all heading copy/rules clear of '" + part.name + "'.");
        }
    }
}
