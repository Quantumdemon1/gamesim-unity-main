using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
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
    /// The key ceremony and the live eviction as the owner asked for them: paced to build suspense,
    /// quicker or skipped when the player wants, and truthful about what they reveal - a tie is a
    /// tie, the Head of Household's vote is its own mark rather than one more of the house's, and
    /// nothing gives a result away before the card reaches it.
    ///
    /// <para>Each card is attached to a plain owner, as <see cref="CeremonyGlassPlayModeTests"/>
    /// does, so what is measured is the card and not the director around it. Input is a synthetic
    /// keyboard - and a pad, where a test needs one - with queued state, which is how a real press
    /// arrives and where the cards read it from; HeadlessInputSettings lets it through in
    /// batchmode.</para>
    ///
    /// <para>Everything timed is timed on the wall clock. The cards run on unscaled real time and a
    /// batchmode frame is under a millisecond, so a count of frames measures nothing.</para>
    /// </summary>
    public sealed class CeremonyRevealPlayModeTests
    {
        private static readonly string[] Names =
        {
            "Emma Brown", "Riley Johnson", "Alex Chen", "Sam Patel", "Noah Kim", "Ava Lopez",
            "Liam Novak", "Mia Rossi", "Ethan Park", "Zoe Adams", "Owen Hart", "Ivy Moreno",
        };

        /// <summary>The block, in every test: Jordan Taylor on the left, Casey Wilson on the right.</summary>
        private const string Jordan = "a", Casey = "b";

        private GameObject owner;
        private readonly List<GameObject> cards = new List<GameObject>();
        private Keyboard keyboard;
        private Gamepad pad;

        [SetUp]
        public void CreateFixture()
        {
            owner = new GameObject("Ceremony reveal owner");
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [UnityTearDown]
        public IEnumerator DestroyFixture()
        {
            foreach (var card in cards) if (card != null) Object.Destroy(card);
            cards.Clear();
            if (owner != null) Object.Destroy(owner);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            keyboard = null;
            pad = null;
            yield return null;
        }

        // ------------------------------------------------------------------ pace

        /// <summary>
        /// A reveal's length is the pacing table's sum and nothing else, at either pace - and a card
        /// left alone runs for exactly that long, with motion or without.
        /// </summary>
        [UnityTest]
        public IEnumerator Duration_IsThePacingSumAtBothPacesAndTheCardsRunExactlyThatLong()
        {
            foreach (var pace in new[] { CeremonyPace.Suspenseful, CeremonyPace.Quick })
            {
                foreach (int count in new[] { 1, 3, 8, 12 })
                {
                    var keys = Keys(count, pace);
                    float expected = CeremonyPacing.FadeIn + CeremonyPacing.KeyIntro(pace)
                        + count * CeremonyPacing.PerKey(pace, count) + CeremonyPacing.LastKeyBeat(pace)
                        + CeremonyPacing.BlockHold(pace) + CeremonyPacing.FadeOut;
                    Assert.That(keys.Duration, Is.EqualTo(expected).Within(0.0005f),
                        pace + ", " + count + " keys: the fade, the Head of Household's line, a hold on every key, "
                        + "the beat before the last, the block and the fade out.");
                    keys.Cancel();
                }

                var counted = Reveal(new[] { Vote(0, Jordan), Vote(1, Jordan), Vote(2, Casey) }, Jordan, pace);
                float three = CeremonyPacing.FadeIn + CeremonyPacing.VoteIntro(pace)
                    + 3 * CeremonyPacing.PerVote(pace, 3) + CeremonyPacing.LastVoteBeat(pace);
                Assert.That(counted.Duration, Is.EqualTo(three + CeremonyPacing.ResultHold(pace) + CeremonyPacing.FadeOut).Within(0.0005f),
                    pace + ", three votes: the fade, the block, a hold on every vote, the beat before the last, the result and the fade out.");
                counted.Cancel();

                var tied = Reveal(new[] { Vote(0, Jordan), Vote(1, Casey), TieBreak(Jordan) }, Jordan, pace);
                float two = CeremonyPacing.FadeIn + CeremonyPacing.VoteIntro(pace)
                    + 2 * CeremonyPacing.PerVote(pace, 2) + CeremonyPacing.LastVoteBeat(pace);
                Assert.That(tied.Duration, Is.EqualTo(two + CeremonyPacing.TieBeat(pace) + CeremonyPacing.PerVote(pace, 2)
                        + CeremonyPacing.ResultHold(pace) + CeremonyPacing.FadeOut).Within(0.0005f),
                    pace + ", a tie: the house's two votes, the tie called, the deciding vote held like a vote, then the result.");
                tied.Cancel();
            }
            yield return null;

            // Played through untouched, with the fades, they end when Duration says they will.
            var timedKeys = Keys(3, CeremonyPace.Quick, reducedMotion: false);
            var timedVotes = Reveal(new[] { Vote(0, Jordan), Vote(1, Jordan) }, Jordan, CeremonyPace.Quick, reducedMotion: false);
            float started = Time.realtimeSinceStartup, keysRan = -1f, votesRan = -1f;
            float limit = started + Mathf.Max(timedKeys.Duration, timedVotes.Duration) + 5f;
            while ((timedKeys.IsPlaying || timedVotes.IsPlaying) && Time.realtimeSinceStartup < limit)
            {
                yield return null;
                float ran = Time.realtimeSinceStartup - started;
                if (keysRan < 0f && !timedKeys.IsPlaying) keysRan = ran;
                if (votesRan < 0f && !timedVotes.IsPlaying) votesRan = ran;
            }
            Assert.That(keysRan, Is.EqualTo(timedKeys.Duration).Within(0.25f), "The key ceremony ran for its Duration.");
            Assert.That(votesRan, Is.EqualTo(timedVotes.Duration).Within(0.25f), "The live eviction ran for its Duration.");
        }

        [UnityTest]
        public IEnumerator KeyCeremony_SpaceRunsTheKeysThreeTimesFasterAndBackAgain()
        {
            // Ten keys at the suspenseful pace: 1.4 s a key, so every step timed is over a second.
            var keys = Keys(10, CeremonyPace.Suspenseful);
            var rhythm = new Rhythm();
            yield return TimeTheSpeedUp(() => keys.KeysShown, () => keys.SpeedMultiplier, keys, rhythm);

            Assert.That(rhythm.Normal, Is.EqualTo(CeremonyPacing.PerKey(CeremonyPace.Suspenseful, 10)).Within(0.15f),
                "At its own speed a key holds for PerKey.");
            Assert.That(rhythm.Normal / rhythm.Fast, Is.EqualTo(CeremonyPacing.SpeedUp).Within(0.5f),
                "Sped up, the keys came " + (rhythm.Normal / rhythm.Fast).ToString("F2") + " times as fast.");
        }

        [UnityTest]
        public IEnumerator VoteReveal_SpaceRunsTheCountThreeTimesFasterAndBackAgain()
        {
            // Nine of the house's votes at the suspenseful pace: 1.4 s a vote.
            var ballots = Enumerable.Range(0, 9).Select(i => Vote(i, i % 2 == 0 ? Jordan : Casey)).ToArray();
            var reveal = Reveal(ballots, Jordan, CeremonyPace.Suspenseful);
            var rhythm = new Rhythm();
            yield return TimeTheSpeedUp(() => reveal.VotesShown, () => reveal.SpeedMultiplier, reveal, rhythm);

            Assert.That(rhythm.Normal, Is.EqualTo(CeremonyPacing.PerVote(CeremonyPace.Suspenseful, 9)).Within(0.15f),
                "At its own speed a vote holds for PerVote.");
            Assert.That(rhythm.Normal / rhythm.Fast, Is.EqualTo(CeremonyPacing.SpeedUp).Within(0.5f),
                "Sped up, the votes came " + (rhythm.Normal / rhythm.Fast).ToString("F2") + " times as fast.");
        }

        [UnityTest]
        public IEnumerator KeyCeremony_TheLastKeyWaitsABeatLongerThanTheOthers()
        {
            const CeremonyPace pace = CeremonyPace.Suspenseful;
            var keys = Keys(3, pace);
            yield return Until(() => keys.KeysShown >= 1, 10f);
            float first = Time.realtimeSinceStartup;
            yield return Until(() => keys.KeysShown >= 2, 10f);
            float second = Time.realtimeSinceStartup;
            Assert.That(keys.KeysShown, Is.EqualTo(2), "The second key never came.");

            yield return Until(() => Text(keys, "Progress").text == "One key left", 10f);
            float called = Time.realtimeSinceStartup;
            Assert.That(Text(keys, "Progress").text, Is.EqualTo("One key left"),
                "While the last key waits the card says one is left,");
            Assert.That(keys.KeysShown, Is.EqualTo(2), "and nobody holds it yet.");

            yield return Until(() => keys.KeysShown >= 3, 10f);
            float third = Time.realtimeSinceStartup;
            Assert.That(keys.KeysShown, Is.EqualTo(3), "The last key never came.");
            Assert.That(keys.ShowingBlock, Is.False, "The last key has its own hold before the block.");

            float rhythm = second - first, last = third - second;
            Assert.That(rhythm, Is.EqualTo(CeremonyPacing.PerKey(pace, 3)).Within(0.15f), "The keys before it keep PerKey's rhythm.");
            Assert.That(last - rhythm, Is.EqualTo(CeremonyPacing.LastKeyBeat(pace)).Within(0.15f),
                "The last key comes LastKeyBeat later than the rhythm would have brought it,");
            Assert.That(third - called, Is.EqualTo(CeremonyPacing.LastKeyBeat(pace)).Within(0.15f),
                "and that wait is spent under 'One key left'.");
        }

        // ------------------------------------------------------------------ skipping

        [UnityTest]
        public IEnumerator KeyCeremony_EnterSkipsToTheBlockAndASecondEnterCloses()
        {
            var cues = new List<HouseAudio.Cue>();
            var keys = Keys(3, CeremonyPace.Suspenseful, cues: cues);
            yield return PastTheReadDelay();
            Assert.That(keys.KeysShown, Is.Zero, "Still on the Head of Household's line.");
            Assert.That(keys.ShowingBlock, Is.False);

            yield return Press(Key.Enter);
            Assert.That(keys.IsPlaying, Is.True, "The first press shows the block; it does not skip it.");
            Assert.That(keys.ShowingBlock, Is.True, "The first press goes straight to the block,");
            Assert.That(keys.KeysShown, Is.EqualTo(3), "handing out every remaining key on the way.");
            Assert.That(Texts(keys, "Nominee"), Is.EquivalentTo(new[] { "Jordan Taylor", "Casey Wilson" }),
                "Skipping gives up the order, never the result.");
            Assert.That(cues, Is.EqualTo(new[] { HouseAudio.Cue.Nomination }),
                "A skip sounds the block once, not a pile of chimes for every key it hands out at once.");

            yield return Press(Key.Enter);
            Assert.That(keys.IsPlaying, Is.False, "A press on the block ends the card.");
        }

        [UnityTest]
        public IEnumerator VoteReveal_EnterSkipsToTheResultAndASecondEnterCloses()
        {
            var cues = new List<HouseAudio.Cue>();
            var reveal = Reveal(new[] { Vote(0, Jordan), Vote(1, Jordan), Vote(2, Casey) }, Jordan, CeremonyPace.Suspenseful, cues: cues);
            yield return PastTheReadDelay();
            Assert.That(reveal.VotesShown, Is.Zero, "Still on the block.");
            Assert.That(reveal.ShowingResult, Is.False);

            yield return Press(Key.Enter);
            Assert.That(reveal.IsPlaying, Is.True, "The first press shows the result; it does not skip it.");
            Assert.That(reveal.ShowingResult, Is.True, "The first press goes straight to the result,");
            Assert.That(reveal.VotesShown, Is.EqualTo(3), "with every vote on the board.");
            Assert.That(Texts(reveal, "Votes"), Is.EqualTo(new[] { "2", "1" }));
            Assert.That(Text(reveal, "Result").text, Is.EqualTo("JORDAN TAYLOR  ·  EVICTED"));
            Assert.That(cues, Is.EqualTo(new[] { HouseAudio.Cue.Eviction }), "A skip sounds the result once.");

            yield return Press(Key.Enter);
            Assert.That(reveal.IsPlaying, Is.False, "A press on the result ends the card.");
        }

        [UnityTest]
        public IEnumerator EscapeAndThePadsFaceButtonsSkipAndCloseTheRevealsToo()
        {
            var keys = Keys(3, CeremonyPace.Suspenseful);
            var reveal = Reveal(new[] { Vote(0, Jordan), Vote(1, Jordan), Vote(2, Casey) }, Jordan, CeremonyPace.Suspenseful);
            yield return PastTheReadDelay();
            yield return Press(Key.Escape);
            Assert.That(keys.ShowingBlock, Is.True, "Escape skips the keys to the block,");
            Assert.That(reveal.ShowingResult, Is.True, "and the count to the result.");
            yield return Press(GamepadButton.East);
            Assert.That(keys.IsPlaying, Is.False, "The pad's B ends a card on its block,");
            Assert.That(reveal.IsPlaying, Is.False, "and on its result.");

            keys = Keys(3, CeremonyPace.Suspenseful);
            reveal = Reveal(new[] { Vote(0, Jordan), Vote(1, Jordan), Vote(2, Casey) }, Jordan, CeremonyPace.Suspenseful);
            yield return PastTheReadDelay();
            yield return Press(GamepadButton.South);
            Assert.That(keys.ShowingBlock, Is.True, "The pad's A skips to the block,");
            Assert.That(reveal.ShowingResult, Is.True, "and to the result,");
            Assert.That(Text(keys, "Controls").text, Is.EqualTo(CeremonyTakeover.PadControlsCaption),
                "and the hints now name the pad's buttons.");
            yield return Press(Key.NumpadEnter);
            Assert.That(keys.IsPlaying, Is.False, "The keypad's Enter ends a card too.");
            Assert.That(reveal.IsPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator Takeover_EnterClosesItOnceItHasBeenRead()
        {
            var takeover = CeremonyTakeover.Attach(owner);
            cards.Add(takeover.gameObject);
            takeover.Play(CeremonySting.EvictionKind, 3,
                new[] { new CeremonyTakeover.Subject("Casey Wilson", "EVICTED", null) }, true);

            yield return Press(Key.Enter);
            Assert.That(takeover.IsPlaying, Is.True,
                "An Enter already in flight when the card appears does not skip what it has not shown.");
            yield return PastTheReadDelay();
            Assert.That(takeover.IsPlaying, Is.True, "Its hold is longer than the read delay.");

            yield return Press(Key.Enter);
            Assert.That(takeover.IsPlaying, Is.False, "Enter ends the card, as a click does.");
        }

        // ------------------------------------------------------------------ the count

        [UnityTest]
        public IEnumerator VoteReveal_ATieIsCalledAtTwoAllAndTheHeadOfHouseholdsVoteIsItsOwnMark()
        {
            // Two all in the house; then Maya Hassan, Head of Household, evicts Jordan.
            var reveal = Reveal(new[] { Vote(0, Jordan), Vote(1, Casey), Vote(2, Casey), Vote(3, Jordan), TieBreak(Jordan) },
                Jordan, CeremonyPace.Quick);

            yield return Until(() => reveal.VotesShown >= 1, 10f);
            Assert.That(Texts(reveal, "Votes"), Is.EqualTo(new[] { "1", "0" }));
            Assert.That(Rects(reveal, "Pip"), Has.Length.EqualTo(4), "One pip per vote the house cast.");
            Assert.That(IsUp(reveal, "Tie-break pip"), Is.False,
                "While the house's votes are read nothing says a tie is coming: not a place waiting for the deciding vote,");
            Assert.That(IsUp(reveal, "Tie-break vote"), Is.False, "nor its mark.");

            yield return Until(() => Text(reveal, "Progress").text.StartsWith("The vote is tied", System.StringComparison.Ordinal), 10f);
            Assert.That(Text(reveal, "Progress").text, Is.EqualTo("The vote is tied, 2 to 2."));
            Assert.That(Text(reveal, "Host").text, Is.EqualTo("As Head of Household, Maya Hassan must break the tie."));
            Assert.That(Texts(reveal, "Votes"), Is.EqualTo(new[] { "2", "2" }));
            Assert.That(reveal.VotesShown, Is.EqualTo(4));
            Assert.That(IsUp(reveal, "Tie-break vote"), Is.False, "The deciding vote is not cast yet.");
            Assert.That(reveal.ShowingResult, Is.False);

            yield return Until(() => reveal.VotesShown >= 5, 10f);
            Assert.That(reveal.VotesShown, Is.EqualTo(5), "The Head of Household's vote goes on the board after the tie is called,");
            Assert.That(Texts(reveal, "Votes"), Is.EqualTo(new[] { "2", "2" }),
                "as its own mark: counted with the house's votes, a 2-2 tie read 3-2.");
            Assert.That(IsUp(reveal, "Tie-break pip"), Is.True);
            Assert.That(Rect(reveal, "Tie-break pip").GetComponent<Image>().color, Is.EqualTo(UiTheme.Gold),
                "The deciding pip is lit in gold, apart from the house's row.");
            Assert.That(IsUp(reveal, "Tie-break vote"), Is.True, "An HOH chip stands beside the figure of the nominee it names -");
            Assert.That(Text(reveal, "Tie-break").text, Is.EqualTo("HOH"));
            var figures = reveal.GetComponentsInChildren<TMP_Text>(true).Where(label => label.name == "Votes").ToArray();
            float chip = Rect(reveal, "Tie-break vote").anchoredPosition.x;
            Assert.That(Mathf.Abs(chip - figures[0].rectTransform.anchoredPosition.x),
                Is.LessThan(Mathf.Abs(chip - figures[1].rectTransform.anchoredPosition.x)), "Jordan's.");

            yield return Until(() => reveal.ShowingResult, 10f);
            Assert.That(Text(reveal, "Host").text,
                Is.EqualTo("By the Head of Household's tie-breaking vote, Jordan Taylor, you have been evicted."));
            Assert.That(Texts(reveal, "Votes"), Is.EqualTo(new[] { "2", "2" }), "The result does not add the deciding vote to the count either.");
        }

        [UnityTest]
        public IEnumerator VoteReveal_ReadsTheCountAsTheHousesVotes()
        {
            var reveal = Reveal(new[] { Vote(0, Jordan), Vote(1, Casey), Vote(2, Jordan), Vote(3, Jordan) }, Jordan, CeremonyPace.Suspenseful);
            yield return SkipToTheResult(reveal);
            Assert.That(Text(reveal, "Host").text, Is.EqualTo("By a vote of 3 to 1, Jordan Taylor, you have been evicted."));
            Assert.That(Text(reveal, "Result").text, Is.EqualTo("JORDAN TAYLOR  ·  EVICTED"), "and the banner still stamps it.");
        }

        [UnityTest]
        public IEnumerator VoteReveal_NamesASoleVoter()
        {
            var reveal = Reveal(new[] { Vote(0, Casey) }, Casey, CeremonyPace.Suspenseful);
            yield return SkipToTheResult(reveal);
            Assert.That(Text(reveal, "Host").text,
                Is.EqualTo("Emma Brown cast the sole vote to evict. Casey Wilson, you have been evicted."));
        }

        [UnityTest]
        public IEnumerator VoteReveal_CreditsATieToTheHeadOfHouseholdsVote()
        {
            var reveal = Reveal(new[] { Vote(0, Jordan), Vote(1, Casey), TieBreak(Casey) }, Casey, CeremonyPace.Suspenseful);
            yield return SkipToTheResult(reveal);
            Assert.That(Text(reveal, "Host").text,
                Is.EqualTo("By the Head of Household's tie-breaking vote, Casey Wilson, you have been evicted."));
            Assert.That(Texts(reveal, "Votes"), Is.EqualTo(new[] { "1", "1" }), "Skipped to, a tie is still the house's 1 to 1,");
            Assert.That(reveal.VotesShown, Is.EqualTo(3));
            Assert.That(IsUp(reveal, "Tie-break vote"), Is.True, "with the deciding vote marked on its own.");
        }

        [UnityTest]
        public IEnumerator VoteReveal_SpeaksToAnEvictedPlayer()
        {
            var counted = Reveal(new[] { Vote(0, Jordan), Vote(1, Casey), Vote(2, Jordan), Vote(3, Jordan) }, Jordan,
                CeremonyPace.Suspenseful, evictedIsPlayer: true);
            var sole = Reveal(new[] { Vote(0, Jordan) }, Jordan, CeremonyPace.Suspenseful, evictedIsPlayer: true);
            var tied = Reveal(new[] { Vote(0, Jordan), Vote(1, Casey), TieBreak(Jordan) }, Jordan,
                CeremonyPace.Suspenseful, evictedIsPlayer: true);
            yield return PastTheReadDelay();
            yield return Press(Key.Enter);
            Assert.That(counted.ShowingResult && sole.ShowingResult && tied.ShowingResult, Is.True, "Enter skips each to its result.");
            Assert.That(Text(counted, "Host").text, Is.EqualTo("By a vote of 3 to 1, you have been evicted."),
                "The player is spoken to, not named,");
            Assert.That(Text(sole, "Host").text, Is.EqualTo("Emma Brown cast the sole vote to evict. You have been evicted."));
            Assert.That(Text(tied, "Host").text, Is.EqualTo("By the Head of Household's tie-breaking vote, you have been evicted."));
        }

        [UnityTest]
        public IEnumerator VoteReveal_AsksAPlayerHeadOfHouseholdToBreakTheTie()
        {
            var reveal = Reveal(new[] { Vote(0, Jordan), Vote(1, Casey), TieBreak(Casey) }, Casey, CeremonyPace.Quick, hohIsPlayer: true);
            yield return Until(() => Text(reveal, "Progress").text.StartsWith("The vote is tied", System.StringComparison.Ordinal), 10f);
            Assert.That(Text(reveal, "Progress").text, Is.EqualTo("The vote is tied, 1 to 1."));
            Assert.That(Text(reveal, "Host").text, Is.EqualTo("As Head of Household, you must break the tie."),
                "The player holding the deciding vote is asked in the second person.");
            yield return Until(() => reveal.VotesShown >= 3, 10f);
            Assert.That(Text(reveal, "Progress").text, Is.EqualTo("You have cast the deciding vote."));
        }

        // ------------------------------------------------------------------ the house around it

        [UnityTest]
        public IEnumerator VoteReveal_HoldsTheHousesClicksOnEveryFrameItPlays()
        {
            yield return null;
            yield return null;
            Assert.That(CeremonyOverlays.OnScreen, Is.False, "Nothing is on screen before the reveal.");

            var reveal = Reveal(new[] { Vote(0, Jordan), Vote(1, Jordan), Vote(2, Casey) }, Jordan, CeremonyPace.Suspenseful);
            yield return null;
            yield return null;
            Assert.That(CeremonyOverlays.OnScreen, Is.True,
                "The eviction's tally is a ceremony card: a click that skips it is not a click on the floor behind it.");
            yield return Seconds(1f);
            Assert.That(reveal.IsPlaying, Is.True);
            Assert.That(CeremonyOverlays.OnScreen, Is.True, "On every frame it plays, not only its first.");

            reveal.Cancel();
            yield return null;
            yield return null;
            yield return null;
            Assert.That(CeremonyOverlays.OnScreen, Is.False, "The house has its clicks back when the card is gone.");
        }

        [UnityTest]
        public IEnumerator KeyCeremony_ChimesForEachKeyAndSoundsTheBlockOnce()
        {
            var cues = new List<HouseAudio.Cue>();
            var keys = Keys(3, CeremonyPace.Quick, cues: cues);
            yield return Until(() => keys.KeysShown >= 1, 10f);
            Assert.That(cues, Is.EqualTo(new[] { HouseAudio.Cue.Save }), "The safe chime comes with the first key, and nothing before it.");

            yield return Until(() => keys.ShowingBlock, 10f);
            yield return null;
            Assert.That(cues, Is.EqualTo(new[] { HouseAudio.Cue.Save, HouseAudio.Cue.Save, HouseAudio.Cue.Save, HouseAudio.Cue.Nomination }),
                "One safe chime per key, then the nomination's sound once, at the block.");
        }

        [UnityTest]
        public IEnumerator VoteReveal_SoundsEachBallotAndTheResultOnce()
        {
            var cues = new List<HouseAudio.Cue>();
            var reveal = Reveal(new[] { Vote(0, Jordan), Vote(1, Casey), TieBreak(Jordan) }, Jordan, CeremonyPace.Quick, cues: cues);
            yield return Until(() => reveal.ShowingResult, 10f);
            yield return null;
            Assert.That(cues, Is.EqualTo(new[] { HouseAudio.Cue.Vote, HouseAudio.Cue.Vote, HouseAudio.Cue.Vote, HouseAudio.Cue.Eviction }),
                "The vote's sound for every ballot on the board - the Head of Household's too - and the eviction's once, at the result.");
        }

        /// <summary>
        /// The lines this pass added draw their copy when they are up: TextMesh Pro draws nothing in a
        /// box under Inter's 1.21 line while the text property still reads right, which is all the other
        /// assertions here look at. The house-wide sweep (Labels_EveryScreenAndCardDrawsItsCopy) sees
        /// each card only on its first frame, before the speed mark, the host's line or the tie-break
        /// chip are up.
        /// </summary>
        [UnityTest]
        public IEnumerator TheNewLinesDrawTheirCopy()
        {
            var keys = Keys(3, CeremonyPace.Suspenseful);
            var reveal = Reveal(new[] { Vote(0, Jordan), Vote(1, Casey), TieBreak(Jordan) }, Jordan, CeremonyPace.Suspenseful);
            yield return PastTheReadDelay();
            Assert.That(Text(keys, "Controls").text, Is.EqualTo(CeremonyTakeover.ControlsCaption), "The keys the card answers to,");
            Assert.That(Text(keys, "Dismiss").text, Is.EqualTo(CeremonyTakeover.DismissCaption), "beside the line other code knows it by.");
            Assert.That(Text(reveal, "Controls").text, Is.EqualTo(CeremonyTakeover.ControlsCaption));

            yield return Press(Key.Space);
            Assert.That(IsUp(keys, "Speed") && IsUp(reveal, "Speed"), Is.True, "Both corners say fast-forward.");
            AssertEveryLabelDraws(keys, "The key ceremony, sped up");
            AssertEveryLabelDraws(reveal, "The live eviction, sped up");

            yield return Press(Key.Enter);
            Assert.That(keys.ShowingBlock && reveal.ShowingResult, Is.True);
            Assert.That(IsUp(reveal, "Tie-break vote") && !string.IsNullOrEmpty(Text(reveal, "Host").text), Is.True,
                "The result's own lines are up to be checked.");
            AssertEveryLabelDraws(keys, "The key ceremony's block");
            AssertEveryLabelDraws(reveal, "The live eviction's result, after a tie");
        }

        // ------------------------------------------------------------------ the screen's board (MOCKUP-PASS-PLAN M18)

        /// <summary>The pieces only the screen's frame has; the HUD's card is drawn as it always was.</summary>
        private static readonly string[] ScreenOnly = { "Vote heading", "Board", "Roster", "Ballot", "Result block" };

        /// <summary>
        /// On the living room's screen the voters fill in a roster between the two faces, a row a
        /// vote in the order cast, each naming the nominee that voter evicts in that nominee's side
        /// colour - the colour of the nominee's own figure. The title, the pips and the figures stay.
        /// </summary>
        [UnityTest]
        public IEnumerator VoteReveal_OnTheScreenTheRosterFillsInAVoteAtATime()
        {
            var screen = LivingScreen();
            var reveal = Reveal(new[] { Vote(0, Jordan), Vote(1, Casey), Vote(2, Jordan) }, Jordan, CeremonyPace.Quick, screen: screen);
            Assert.That(reveal.Surface, Is.SameAs(screen), "The card plays on the screen.");
            Assert.That(Text(reveal, "Title").text, Is.EqualTo("LIVE EVICTION"), "The title stays,");
            Assert.That(Text(reveal, "Vote heading").text, Is.EqualTo("THE VOTE"), "with the vote under it.");
            Assert.That(Rects(reveal, "Pip"), Has.Length.EqualTo(3), "The pip row stays: one per vote the house cast.");
            Assert.That(Rects(reveal, "Ballot"), Has.Length.EqualTo(3), "A row is built for every ballot,");
            Assert.That(RowsUp(reveal), Is.Empty, "and none is up before the first vote is read.");

            yield return Until(() => reveal.VotesShown >= 1, 10f);
            Assert.That(RowsUp(reveal).Select(row => Part(row, "Ballot voter").text), Is.EqualTo(new[] { "Emma Brown" }));
            Assert.That(RowsUp(reveal).Select(row => Part(row, "Ballot target").text), Is.EqualTo(new[] { "EVICT JORDAN" }));

            yield return Until(() => reveal.VotesShown >= 2, 10f);
            var rows = RowsUp(reveal);
            Assert.That(rows.Select(row => Part(row, "Ballot voter").text), Is.EqualTo(new[] { "Emma Brown", "Riley Johnson" }),
                "A row a vote, in the order the house cast them.");
            Assert.That(rows.Select(row => Part(row, "Ballot target").text), Is.EqualTo(new[] { "EVICT JORDAN", "EVICT CASEY" }));
            Assert.That(Texts(reveal, "Votes"), Is.EqualTo(new[] { "1", "1" }), "The figures count as they did.");

            var figures = reveal.GetComponentsInChildren<TMP_Text>(true).Where(label => label.name == "Votes").ToArray();
            Assert.That(Part(rows[0], "Ballot target").color, Is.EqualTo(figures[0].color), "A chip is in its nominee's colour:");
            Assert.That(Part(rows[1], "Ballot target").color, Is.EqualTo(figures[1].color), "Jordan's on the left, Casey's on the right,");
            Assert.That(figures[0].color, Is.Not.EqualTo(figures[1].color), "and the two sides differ.");
            Assert.That(figures.Select(figure => figure.color), Has.None.EqualTo(UiTheme.Danger),
                "Neither side is the eviction's red: that is kept for the result.");
            AssertEveryLabelDraws(reveal, "The screen's board, two votes in");
        }

        /// <summary>
        /// Decision 5 A's rule: the board never says who is leaving before the result. Given the same
        /// ballots and told a different evictee, two cards draw the same board - every row, chip,
        /// figure and colour - at every step of the count.
        /// </summary>
        [UnityTest]
        public IEnumerator VoteReveal_OnTheScreenTheBoardIsTheSameWhoeverIsLeaving()
        {
            var screen = LivingScreen();
            var ballots = new[] { Vote(0, Jordan), Vote(1, Casey), Vote(2, Jordan), Vote(3, Casey), Vote(4, Jordan) };
            var jordanLeaves = Reveal(ballots, Jordan, CeremonyPace.Quick, screen: screen);
            var caseyLeaves = Reveal(ballots, Casey, CeremonyPace.Quick, screen: screen);

            // Both run on the same clock from the same frame, so they are at the same step on every
            // frame. Compared at each step of the count, not on every frame.
            string step = null;
            int compared = 0;
            float until = Time.realtimeSinceStartup + 20f;
            while (jordanLeaves.IsPlaying && !jordanLeaves.ShowingResult && Time.realtimeSinceStartup < until)
            {
                string now = jordanLeaves.VotesShown + " " + Text(jordanLeaves, "Progress").text;
                if (now != step)
                {
                    step = now;
                    compared++;
                    Assert.That(Drawn(caseyLeaves), Is.EqualTo(Drawn(jordanLeaves)), "At '" + now + "' the boards differ.");
                }
                yield return null;
            }
            Assert.That(jordanLeaves.ShowingResult, Is.True, "The count reached its result.");
            Assert.That(compared, Is.GreaterThanOrEqualTo(ballots.Length), "Every vote's step was compared.");
            Assert.That(Text(caseyLeaves, "Result name").text, Is.Not.EqualTo(Text(jordanLeaves, "Result name").text),
                "and only the result tells them apart.");
        }

        /// <summary>
        /// At the result the board - faces, names, figures, the VS disc, the pips and the roster -
        /// fades, every piece of it kept, and the result is read in lines: the count, the evictee's
        /// figure in red and the other in its side's colour, the first name, and that they are
        /// evicted. The Host line and the banner keep their words.
        /// </summary>
        [UnityTest]
        public IEnumerator VoteReveal_OnTheScreenTheResultIsReadInLinesOverTheBoard()
        {
            var reveal = Reveal(new[] { Vote(0, Jordan), Vote(1, Casey), Vote(2, Jordan), Vote(3, Jordan) }, Jordan,
                CeremonyPace.Suspenseful, screen: LivingScreen());
            Assert.That(IsUp(reveal, "Result block"), Is.False, "Nothing of the result is up before it is read.");
            yield return SkipToTheResult(reveal);

            var rows = RowsUp(reveal);
            Assert.That(rows.Select(row => Part(row, "Ballot voter").text),
                Is.EqualTo(new[] { "Emma Brown", "Riley Johnson", "Alex Chen", "Sam Patel" }), "A skip puts every row up at once.");
            Assert.That(IsUp(reveal, "Result block"), Is.True);
            Assert.That(Text(reveal, "Result lead").text, Is.EqualTo("BY A VOTE OF"));
            Assert.That(Text(reveal, "Result count").text, Is.EqualTo("3"), "The evictee's votes,");
            Assert.That(Text(reveal, "Result count").color, Is.EqualTo(UiTheme.Danger), "in red,");
            Assert.That(Text(reveal, "Result to").text, Is.EqualTo("TO"));
            Assert.That(Text(reveal, "Result other count").text, Is.EqualTo("1"), "and the other nominee's,");
            Assert.That(Text(reveal, "Result other count").color, Is.EqualTo(Part(rows[1], "Ballot target").color), "in Casey's colour.");
            Assert.That(Text(reveal, "Result name").text, Is.EqualTo("JORDAN"));
            Assert.That(Text(reveal, "Result name").color, Is.EqualTo(UiTheme.Danger));
            Assert.That(Text(reveal, "Result line").text, Is.EqualTo("IS EVICTED."));
            Assert.That(Text(reveal, "Host").text, Is.EqualTo("By a vote of 3 to 1, Jordan Taylor, you have been evicted."),
                "The host's line is unchanged,");
            Assert.That(Text(reveal, "Result").text, Is.EqualTo("JORDAN TAYLOR  ·  EVICTED"), "and so is the banner.");

            // Reduced motion hands over at once; with motion the board fades out over the result's first moment.
            Assert.That(Rect(reveal, "Board").GetComponent<CanvasGroup>().alpha, Is.Zero, "The board has given way,");
            Assert.That(Rect(reveal, "Result block").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f), "to the result.");
            Assert.That(Texts(reveal, "Votes"), Is.EqualTo(new[] { "3", "1" }), "The board's pieces are kept:");
            Assert.That(Rects(reveal, "Pip"), Has.Length.EqualTo(4), "the pips,");
            Assert.That(IsUp(reveal, "Versus disc") && IsUp(reveal, "Roster"), Is.True, "the disc and the roster.");
            AssertEveryLabelDraws(reveal, "The screen's result");
        }

        /// <summary>
        /// The result's variants: a tie is credited to the Head of Household with no count - the
        /// house's was level - and their gold row joins the roster only once the tie is called and
        /// broken; an evicted player is told; a sole vote reads one to nothing.
        /// </summary>
        [UnityTest]
        public IEnumerator VoteReveal_OnTheScreenTheResultReadsATieASoleVoteAndThePlayer()
        {
            var screen = LivingScreen();
            var tied = Reveal(new[] { Vote(0, Jordan), Vote(1, Casey), TieBreak(Jordan) }, Jordan, CeremonyPace.Quick, screen: screen);
            Assert.That(Rects(tied, "Ballot"), Has.Length.EqualTo(3), "Two of the house's rows and the deciding vote's,");
            yield return Until(() => Text(tied, "Progress").text.StartsWith("The vote is tied", System.StringComparison.Ordinal), 10f);
            Assert.That(RowsUp(tied), Has.Length.EqualTo(2), "which is not up when the tie is called.");
            yield return Until(() => tied.VotesShown >= 3, 10f);
            var deciding = RowsUp(tied);
            Assert.That(deciding, Has.Length.EqualTo(3), "The Head of Household's vote goes on the roster,");
            Assert.That(Part(deciding[2], "Ballot voter").text, Is.EqualTo("Maya Hassan"));
            Assert.That(Part(deciding[2], "Ballot voter").color, Is.EqualTo(UiTheme.Gold), "in gold,");
            Assert.That(Part(deciding[2], "Ballot target").text, Is.EqualTo("EVICT JORDAN"), "naming who they evict.");
            yield return Until(() => tied.ShowingResult, 10f);
            Assert.That(Text(tied, "Result lead").text, Is.EqualTo("BY THE HEAD OF HOUSEHOLD'S VOTE"));
            Assert.That(IsUp(tied, "Result tally"), Is.False, "A tie-break gives no count: the house's was level.");
            Assert.That(Text(tied, "Result name").text, Is.EqualTo("JORDAN"));
            Assert.That(Text(tied, "Result line").text, Is.EqualTo("IS EVICTED."));
            AssertEveryLabelDraws(tied, "The screen's result, after a tie");
            tied.Cancel();

            var player = Reveal(new[] { Vote(0, Jordan), Vote(1, Casey), Vote(2, Jordan) }, Jordan, CeremonyPace.Suspenseful,
                evictedIsPlayer: true, screen: screen);
            var sole = Reveal(new[] { Vote(0, Casey) }, Casey, CeremonyPace.Suspenseful, screen: screen);
            yield return PastTheReadDelay();
            yield return Press(Key.Enter);
            Assert.That(player.ShowingResult && sole.ShowingResult, Is.True, "Enter skips each to its result.");
            Assert.That(Text(player, "Result line").text, Is.EqualTo("YOU ARE EVICTED."), "The player is told,");
            Assert.That(Text(player, "Host").text, Is.EqualTo("By a vote of 2 to 1, you have been evicted."), "as the host tells them.");
            Assert.That(Text(sole, "Result lead").text, Is.EqualTo("BY A VOTE OF"));
            Assert.That(new[] { Text(sole, "Result count").text, Text(sole, "Result other count").text }, Is.EqualTo(new[] { "1", "0" }),
                "A sole vote is one to nothing.");
            Assert.That(Text(sole, "Result name").text, Is.EqualTo("CASEY"));
        }

        /// <summary>
        /// The roster holds from one vote to a full house's thirteen: one column up to seven rows and
        /// two past that, every row on the face, clear of the others and of the faces, names and
        /// figures at the sides. A deciding vote takes the next place without moving the house's
        /// rows, so the columns do not say a tie is coming.
        /// </summary>
        [UnityTest]
        public IEnumerator VoteReveal_OnTheScreenTheRosterFitsTheFaceFromOneVoteToAFullHouse()
        {
            var screen = LivingScreen();
            foreach (int count in new[] { 1, 7, 8, 13 })
            {
                var ballots = Enumerable.Range(0, count)
                    .Select(i => new VoteReveal.Ballot("v" + i, "Houseguest " + (i + 1), i % 3 == 1 ? Casey : Jordan, false, null))
                    .ToArray();
                var reveal = Reveal(ballots, Jordan, CeremonyPace.Suspenseful, screen: screen);
                yield return SkipToTheResult(reveal);
                var rows = RowsUp(reveal).Select(row => OnTheCard(reveal, row)).ToArray();
                Assert.That(rows, Has.Length.EqualTo(count), count + " votes: a row each, all up.");

                var lanes = rows.GroupBy(row => Mathf.RoundToInt(row.center.x)).ToArray();
                Assert.That(lanes, Has.Length.EqualTo(count > 7 ? 2 : 1), count + " votes: one column up to seven, two past that.");
                Assert.That(lanes.Max(lane => lane.Count()), Is.LessThanOrEqualTo(7), count + " votes: seven rows to a column at most.");

                var face = UnityEngine.Rect.MinMaxRect(-600f, -400f, 600f, 400f);
                var sides = new[] { "Ring", "Nominee", "Votes", "Versus disc", "Dots", "Vote heading" }
                    .SelectMany(name => Rects(reveal, name)).Select(rect => (rect.name, OnTheCard(reveal, rect))).ToArray();
                for (int i = 0; i < rows.Length; i++)
                {
                    Assert.That(face.Contains(rows[i].min) && face.Contains(rows[i].max), Is.True, count + " votes: row " + i + " " + rows[i] + " is on the face.");
                    for (int j = i + 1; j < rows.Length; j++)
                        Assert.That(rows[i].Overlaps(rows[j]), Is.False, count + " votes: rows " + i + " and " + j + " overlap.");
                    foreach (var (name, rect) in sides)
                        Assert.That(rows[i].Overlaps(rect), Is.False, count + " votes: row " + i + " " + rows[i] + " covers '" + name + "' " + rect + ".");
                }
                AssertEveryLabelDraws(reveal, count + " votes on the screen");
                reveal.Cancel();
            }

            // Twelve of the house's votes, tied, and the Head of Household's: the house's rows sit where
            // they would with no tie to break, and the deciding row takes the second column's seventh place.
            var twelve = Enumerable.Range(0, 12).Select(i => Vote(i, i % 2 == 0 ? Jordan : Casey)).ToArray();
            var untied = Reveal(twelve, Jordan, CeremonyPace.Suspenseful, screen: screen);
            var tied = Reveal(twelve.Concat(new[] { TieBreak(Jordan) }).ToArray(), Jordan, CeremonyPace.Suspenseful, screen: screen);
            yield return null;
            var house = Rects(untied, "Ballot").Select(row => row.anchoredPosition).ToArray();
            var withTie = Rects(tied, "Ballot").Select(row => row.anchoredPosition).ToArray();
            Assert.That(withTie.Take(12), Is.EqualTo(house), "The house's rows do not move for a tie that is coming.");
            Assert.That(withTie[12].x, Is.EqualTo(house[11].x), "The deciding row is in the second column,");
            Assert.That(withTie[12].y, Is.LessThan(house[11].y), "under the house's last.");
            yield return SkipToTheResult(tied);
            var deciding = OnTheCard(tied, RowsUp(tied).Last());
            Assert.That(deciding.yMin, Is.GreaterThan(OnTheCard(tied, Rect(tied, "Dots")).yMax), "and clear of the pips.");
        }

        /// <summary>
        /// The HUD's card has none of the screen's board, and the one card the director keeps for
        /// both frames strikes the board when it goes back to the HUD.
        /// </summary>
        [UnityTest]
        public IEnumerator VoteReveal_TheHudCardHasNoBoard()
        {
            var ballots = new[] { Vote(0, Jordan), Vote(1, Jordan), Vote(2, Casey) };
            var reveal = Reveal(ballots, Jordan, CeremonyPace.Suspenseful);
            yield return null;
            foreach (var name in ScreenOnly) Assert.That(Rect(reveal, name), Is.Null, "The HUD's card has no '" + name + "'.");
            Assert.That(reveal.GetComponentsInChildren<TMP_Text>(true).Where(label => label.name == "Votes").Select(label => label.color),
                Is.All.EqualTo(UiTheme.Paper), "The HUD's figures are paper white.");
            yield return SkipToTheResult(reveal);
            foreach (var name in ScreenOnly) Assert.That(Rect(reveal, name), Is.Null, "Nor at the result: '" + name + "'.");

            reveal.Cancel();
            Assert.That(reveal.Play(4, Block(), ballots, Jordan, true, CeremonyPace.Suspenseful, "Maya Hassan", screen: LivingScreen()), Is.True);
            yield return null;
            Assert.That(Rect(reveal, "Board"), Is.Not.Null, "On the screen the same card has its board;");
            reveal.Cancel();
            Assert.That(reveal.Play(4, Block(), ballots, Jordan, true, CeremonyPace.Suspenseful, "Maya Hassan"), Is.True);
            yield return null;
            foreach (var name in ScreenOnly) Assert.That(Rect(reveal, name), Is.Null, "back on the HUD it has no '" + name + "'.");
            Assert.That(reveal.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        }

        // ------------------------------------------------------------------ helpers

        private KeyCeremony Keys(int safeCount, CeremonyPace pace, bool reducedMotion = true, List<HouseAudio.Cue> cues = null)
        {
            var keys = KeyCeremony.Attach(owner);
            cards.Add(keys.gameObject);
            if (cues != null) keys.CueRequested += cues.Add;
            var safe = Enumerable.Range(0, safeCount).Select(i => new KeyCeremony.Person("s" + i, Names[i], null)).ToArray();
            var block = new[]
            {
                new KeyCeremony.Person(Jordan, "Jordan Taylor", null),
                new KeyCeremony.Person(Casey, "Casey Wilson", null),
            };
            Assert.That(keys.Play(1, "Maya Hassan", false, safe, block, reducedMotion, pace), Is.True,
                "Keys to hand out and a block is a shape the ceremony narrates.");
            return keys;
        }

        private VoteReveal Reveal(IList<VoteReveal.Ballot> ballots, string evicted, CeremonyPace pace,
            bool reducedMotion = true, bool hohIsPlayer = false, bool evictedIsPlayer = false, List<HouseAudio.Cue> cues = null,
            ScreenSurface screen = null)
        {
            var reveal = VoteReveal.Attach(owner);
            cards.Add(reveal.gameObject);
            if (cues != null) reveal.CueRequested += cues.Add;
            Assert.That(reveal.Play(4, Block(), ballots, evicted, reducedMotion, pace, "Maya Hassan", hohIsPlayer, evictedIsPlayer, screen),
                Is.True, "Two nominees and a ballot is a shape the reveal narrates.");
            return reveal;
        }

        /// <summary>Jordan Taylor on the left, Casey Wilson on the right.</summary>
        private static VoteReveal.Nominee[] Block() => new[]
        {
            new VoteReveal.Nominee(Jordan, "Jordan Taylor", null),
            new VoteReveal.Nominee(Casey, "Casey Wilson", null),
        };

        /// <summary>
        /// A stand-in for the living room's screen: a board 2.6 by 1.5 metres facing the room,
        /// measured the way the set's own is, so a card can be played on a face with no house round it.
        /// </summary>
        private ScreenSurface LivingScreen()
        {
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Screen board";
            board.transform.position = new Vector3(0f, 2f, -3f);
            board.transform.localScale = new Vector3(2.6f, 1.5f, 0.05f);
            cards.Add(board);
            var screen = ScreenSurface.Measure(board.transform, "Living", Vector3.zero);
            Assert.That(screen, Is.Not.Null, "A board with a renderer is a screen to play a card on.");
            return screen;
        }

        /// <summary>The roster's rows that are up, in the order they were cast.</summary>
        private static RectTransform[] RowsUp(Component card) =>
            Rects(card, "Ballot").Where(row => row.gameObject.activeSelf).ToArray();

        private static TMP_Text Part(RectTransform row, string name) =>
            row.GetComponentsInChildren<TMP_Text>(true).First(label => label.name == name);

        /// <summary><paramref name="rect"/> in the card's own space, where the screen's face runs ±600 by ±400.</summary>
        private static UnityEngine.Rect OnTheCard(Component card, RectTransform rect)
        {
            var space = Rect(card, "Card");
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var a = space.InverseTransformPoint(corners[0]);
            var b = space.InverseTransformPoint(corners[2]);
            return UnityEngine.Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>Everything the card draws, as words: each graphic's name, its copy, its colour, and whether it is up.</summary>
        private static string[] Drawn(Component card) =>
            card.GetComponentsInChildren<Graphic>(true)
                .Select(graphic => graphic.name + (graphic is TMP_Text label ? " '" + label.text + "'" : string.Empty)
                    + " #" + ColorUtility.ToHtmlStringRGBA(graphic.color) + (graphic.gameObject.activeInHierarchy ? " up" : " down"))
                .ToArray();

        /// <summary>One of the house's ballots, cast by the named houseguest.</summary>
        private static VoteReveal.Ballot Vote(int voter, string target) => new VoteReveal.Ballot(Names[voter], target);

        /// <summary>The Head of Household's deciding vote.</summary>
        private static VoteReveal.Ballot TieBreak(string target) => new VoteReveal.Ballot("Maya Hassan", target, tieBreak: true);

        private sealed class Rhythm
        {
            public float Normal, Fast;
        }

        /// <summary>
        /// Times one step at the card's own speed, presses Space, times the three steps after the first
        /// one wholly past the press, and presses Space again - checking the speed and its corner mark
        /// on the way.
        /// </summary>
        private IEnumerator TimeTheSpeedUp(System.Func<int> revealed, System.Func<float> multiplier, Component card, Rhythm rhythm)
        {
            yield return Until(() => revealed() >= 1, 15f);
            Assert.That(revealed(), Is.EqualTo(1), "The first reveal never came.");
            float first = Time.realtimeSinceStartup;
            yield return Until(() => revealed() >= 2, 15f);
            Assert.That(revealed(), Is.EqualTo(2), "The second reveal never came.");
            rhythm.Normal = Time.realtimeSinceStartup - first;
            Assert.That(multiplier(), Is.EqualTo(1f), "A reveal starts at its own speed,");
            Assert.That(IsUp(card, "Speed"), Is.False, "and nothing says fast-forward while it is not.");

            yield return Press(Key.Space);
            Assert.That(multiplier(), Is.EqualTo(CeremonyPacing.SpeedUp), "Space speeds the reveal up,");
            Assert.That(IsUp(card, "Speed"), Is.True, "and the corner says so:");
            Assert.That(Text(card, "Speed").text, Is.EqualTo(CeremonyTakeover.SpeedCaption));

            int from = revealed() + 1;
            yield return Until(() => revealed() >= from, 15f);
            float start = Time.realtimeSinceStartup;
            yield return Until(() => revealed() >= from + 3, 15f);
            Assert.That(revealed(), Is.EqualTo(from + 3), "The sped-up reveals never came.");
            rhythm.Fast = (Time.realtimeSinceStartup - start) / 3f;

            yield return Press(Key.Space);
            Assert.That(multiplier(), Is.EqualTo(1f), "A second press puts it back at its own pace,");
            Assert.That(IsUp(card, "Speed"), Is.False, "and the corner goes quiet.");
        }

        private IEnumerator SkipToTheResult(VoteReveal reveal)
        {
            yield return PastTheReadDelay();
            yield return Press(Key.Enter);
            Assert.That(reveal.ShowingResult, Is.True, "Enter skips to the result.");
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private IEnumerator Press(GamepadButton button)
        {
            if (pad == null) pad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
        }

        /// <summary>Waits on the wall clock until <paramref name="done"/>, or <paramref name="seconds"/> at most.</summary>
        private static IEnumerator Until(System.Func<bool> done, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < until) yield return null;
        }

        private static IEnumerator Seconds(float seconds) => Until(() => false, seconds);

        /// <summary>
        /// Past the cards' read-first delay - the fade and 0.35 s after it - before which a press is
        /// taken for one already in flight when the card appeared, and ignored.
        /// </summary>
        private static IEnumerator PastTheReadDelay() => Seconds(CeremonyPacing.FadeIn + 0.5f);

        private static TMP_Text Text(Component card, string name) =>
            card.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(label => label.name == name);

        private static string[] Texts(Component card, string name) =>
            card.GetComponentsInChildren<TMP_Text>(true).Where(label => label.name == name).Select(label => label.text).ToArray();

        private static RectTransform Rect(Component card, string name) =>
            card.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(rect => rect.name == name);

        private static RectTransform[] Rects(Component card, string name) =>
            card.GetComponentsInChildren<RectTransform>(true).Where(rect => rect.name == name).ToArray();

        private static bool IsUp(Component card, string name)
        {
            var rect = Rect(card, name);
            return rect != null && rect.gameObject.activeInHierarchy;
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
    }
}
