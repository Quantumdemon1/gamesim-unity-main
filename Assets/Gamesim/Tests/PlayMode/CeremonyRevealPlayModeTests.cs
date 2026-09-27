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
            bool reducedMotion = true, bool hohIsPlayer = false, bool evictedIsPlayer = false, List<HouseAudio.Cue> cues = null)
        {
            var reveal = VoteReveal.Attach(owner);
            cards.Add(reveal.gameObject);
            if (cues != null) reveal.CueRequested += cues.Add;
            var block = new[]
            {
                new VoteReveal.Nominee(Jordan, "Jordan Taylor", null),
                new VoteReveal.Nominee(Casey, "Casey Wilson", null),
            };
            Assert.That(reveal.Play(4, block, ballots, evicted, reducedMotion, pace, "Maya Hassan", hohIsPlayer, evictedIsPlayer), Is.True,
                "Two nominees and a ballot is a shape the reveal narrates.");
            return reveal;
        }

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
