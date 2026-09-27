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

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The finale's vote, read the way the reference reads it and more honestly: one juror at a time,
    /// "{n} votes to win", a pause before any vote that could decide it whoever it names, the winner
    /// called the moment they have enough, the jury's whole count at the end, a tie called as a tie,
    /// and confetti for the winner.
    ///
    /// <para>The card is attached to a plain owner, as the other reveals' tests do, and timed on the
    /// wall clock: it runs on unscaled real time.</para>
    /// </summary>
    public sealed class JuryRevealPlayModeTests
    {
        private const string Alex = "alex", Casey = "casey";
        private static readonly string[] Jurors = { "Emma Brown", "Riley Johnson", "Sam Patel", "Noah Kim", "Ava Lopez", "Liam Novak", "Mia Rossi" };

        private GameObject owner;
        private readonly List<GameObject> cards = new List<GameObject>();
        private Keyboard keyboard;

        [SetUp]
        public void CreateFixture()
        {
            owner = new GameObject("Jury reveal owner");
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [UnityTearDown]
        public IEnumerator DestroyFixture()
        {
            foreach (var card in cards) if (card != null) Object.Destroy(card);
            cards.Clear();
            if (owner != null) Object.Destroy(owner);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator JuryReveal_ReadsOneJurorAtATimeAndCallsTheWinnerWhenTheyHaveEnough()
        {
            // Alex reaches four of seven at the sixth vote; the seventh goes to Casey.
            var reveal = Reveal(new[] { Alex, Casey, Alex, Alex, Casey, Alex, Casey }, Alex);
            Assert.That(reveal.Majority, Is.EqualTo(4));
            Assert.That(reveal.DecidingVote, Is.EqualTo(6));
            Assert.That(Text(reveal, "Progress").text, Is.EqualTo("4 votes to win"));
            Assert.That(Texts(reveal, "Juror"), Is.EqualTo(Jurors.Select(name => name.Split(' ')[0]).ToArray()), "A face each, in the order given.");
            AssertEveryLabelDraws(reveal, "before the first vote");

            int most = 0;
            float until = Time.realtimeSinceStartup + 20f;
            while (!reveal.ShowingResult && Time.realtimeSinceStartup < until)
            {
                Assert.That(reveal.VotesShown, Is.GreaterThanOrEqualTo(most), "The count only climbs.");
                most = reveal.VotesShown;
                Assert.That(most, Is.LessThanOrEqualTo(6), "Nothing past the deciding vote is read before the winner is called.");
                yield return null;
            }
            Assert.That(reveal.ShowingResult, Is.True);
            Assert.That(most, Is.EqualTo(6), "Every vote up to the deciding one was read in turn.");
            Assert.That(reveal.VotesShown, Is.EqualTo(7), "With the winner, the rest of the jury's votes go up.");
            Assert.That(Texts(reveal, "Votes"), Is.EqualTo(new[] { "4", "3" }), "The jury's whole count, not the count at the deciding vote.");
            Assert.That(Text(reveal, "Host").text, Is.EqualTo("By a vote of 4 to 3, Alex Chen, you are the winner of Gamesim: The House!"));
            Assert.That(Text(reveal, "Result").text, Is.EqualTo("ALEX CHEN  ·  WINNER"));
            Assert.That(Rects(reveal, "Vote").Count(chip => chip.gameObject.activeSelf), Is.EqualTo(7));
            AssertEveryLabelDraws(reveal, "at the result");
        }

        [UnityTest]
        public IEnumerator JuryReveal_PausesBeforeAnyVoteThatCouldDecideItWhoeverItNames()
        {
            // Alex three up: the fourth vote could decide it, and goes to Casey. Three all: the last
            // could, whichever way.
            var reveal = Reveal(new[] { Alex, Alex, Alex, Casey, Casey, Casey, Alex }, Alex);
            var heldAt = new HashSet<int>();
            float until = Time.realtimeSinceStartup + 25f;
            while (!reveal.ShowingResult && Time.realtimeSinceStartup < until)
            {
                if (reveal.HoldingForDecidingVote) heldAt.Add(reveal.VotesShown);
                yield return null;
            }
            Assert.That(heldAt, Does.Contain(3), "A pause before the fourth vote, which Casey takes: a pause never says a vote decides it.");
            Assert.That(heldAt, Does.Contain(6), "And before the seventh.");
            Assert.That(heldAt.Contains(1) || heldAt.Contains(2), Is.False, "Nobody was a vote from winning then.");
            Assert.That(reveal.DecidingVote, Is.EqualTo(7));
        }

        [UnityTest]
        public IEnumerator JuryReveal_ATiedJuryIsCalledAsATieAndGoesByTheHouseRule()
        {
            var reveal = Reveal(new[] { Alex, Casey, Alex, Casey, Alex, Casey }, Casey);
            Assert.That(reveal.DecidingVote, Is.Zero, "Nobody reaches four of six.");
            yield return Until(() => reveal.ShowingResult, 25f);
            Assert.That(reveal.ShowingResult, Is.True);
            Assert.That(Text(reveal, "Progress").text, Is.EqualTo("The jury is tied, 3 to 3."));
            Assert.That(Text(reveal, "Host").text, Is.EqualTo("Under the house's tie rule, the win goes to Casey Wilson."));
            Assert.That(Text(reveal, "Result").text, Is.EqualTo("CASEY WILSON  ·  WINNER"));
        }

        [UnityTest]
        public IEnumerator JuryReveal_AFinalistWhoIsThePlayerIsSpokenTo()
        {
            var reveal = Reveal(new[] { Alex, Casey, Alex, Alex, Casey }, Alex, playerFinalist: Alex);
            yield return SkipToTheResult(reveal);
            Assert.That(Text(reveal, "Host").text, Is.EqualTo("By a vote of 3 to 2, you are the winner of Gamesim: The House!"));
            Assert.That(Text(reveal, "Result").text, Is.EqualTo("YOU  ·  WINNER"));
            Assert.That(Texts(reveal, "Finalist"), Does.Contain("Alex Chen (You)"));
        }

        [UnityTest]
        public IEnumerator JuryReveal_EnterSkipsToTheWholeCountAndASecondEnterCloses()
        {
            var reveal = Reveal(new[] { Alex, Casey, Alex, Alex, Casey }, Alex);
            yield return Press(Key.Enter);
            Assert.That(reveal.ShowingResult, Is.False, "An Enter already in flight when the card appears skips nothing.");
            yield return SkipToTheResult(reveal);
            Assert.That(reveal.VotesShown, Is.EqualTo(5), "Every vote on the board,");
            Assert.That(Texts(reveal, "Votes"), Is.EqualTo(new[] { "3", "2" }), "and the whole count.");
            yield return Press(Key.Enter);
            Assert.That(reveal.IsPlaying, Is.False, "A second press ends the card.");
        }

        [UnityTest]
        public IEnumerator JuryReveal_ThrowsConfettiForTheWinnerUnlessMotionIsReduced()
        {
            var lively = Reveal(new[] { Alex, Casey, Alex }, Alex, reducedMotion: false);
            Assert.That(lively.Celebrating, Is.False, "Nothing is thrown before the winner is called.");
            yield return SkipToTheResult(lively);
            yield return null;
            Assert.That(lively.Celebrating, Is.True, "Confetti for the winner.");

            var still = Reveal(new[] { Alex, Casey, Alex }, Alex, reducedMotion: true);
            yield return SkipToTheResult(still);
            yield return null;
            Assert.That(still.Celebrating, Is.False, "None under reduced motion.");
        }

        [UnityTest]
        public IEnumerator JuryReveal_RunsForItsDurationAndNoLonger()
        {
            var reveal = Reveal(new[] { Alex, Casey, Alex }, Alex);
            // Quick: the fade, the finalists, three votes with a pause before the second and third
            // (either finalist a vote from winning), a hold on the deciding vote, the winner, the fade.
            float expected = CeremonyPacing.FadeIn + CeremonyPacing.JuryIntro(CeremonyPace.Quick)
                + 2 * CeremonyPacing.PerJuror(CeremonyPace.Quick, 3) + 2 * CeremonyPacing.DecidingBeat(CeremonyPace.Quick)
                + CeremonyPacing.PerJuror(CeremonyPace.Quick, 3) + CeremonyPacing.WinnerHold(CeremonyPace.Quick) + CeremonyPacing.FadeOut;
            Assert.That(reveal.Duration, Is.EqualTo(expected).Within(1e-4f));
            float started = Time.realtimeSinceStartup;
            yield return Until(() => !reveal.IsPlaying, expected + 3f);
            Assert.That(reveal.IsPlaying, Is.False);
            Assert.That(Time.realtimeSinceStartup - started, Is.EqualTo(expected).Within(0.6f));
        }

        [Test]
        public void JuryReveal_DeclinesAShapeItCannotTell()
        {
            var reveal = JuryReveal.Attach(owner);
            cards.Add(reveal.gameObject);
            var pair = Pair(null);
            var jury = Jury(new[] { Alex, Casey, Alex });
            Assert.That(reveal.Play(pair.Take(1).ToList(), jury, Alex, true), Is.False, "One finalist.");
            Assert.That(reveal.Play(pair, new List<JuryReveal.Juror>(), Alex, true), Is.False, "No jury.");
            Assert.That(reveal.Play(pair, jury, "nobody", true), Is.False, "A winner who is not a finalist.");
            Assert.That(reveal.Play(pair, Jury(new[] { Alex, "stranger" }), Alex, true), Is.False, "A vote for somebody not in the final.");
            Assert.That(reveal.IsPlaying, Is.False);
            Assert.That(reveal.Play(pair, jury, Alex, true), Is.True);
        }

        // ------------------------------------------------------------------ helpers

        private JuryReveal Reveal(string[] votes, string winner, bool reducedMotion = true, string playerFinalist = null)
        {
            var reveal = JuryReveal.Attach(owner);
            cards.Add(reveal.gameObject);
            Assert.That(reveal.Play(Pair(playerFinalist), Jury(votes), winner, reducedMotion, CeremonyPace.Quick, 7), Is.True);
            return reveal;
        }

        private static List<JuryReveal.Finalist> Pair(string player) => new List<JuryReveal.Finalist>
        {
            new JuryReveal.Finalist(Alex, "Alex Chen", null, null, player == Alex),
            new JuryReveal.Finalist(Casey, "Casey Wilson", null, null, player == Casey),
        };

        private static List<JuryReveal.Juror> Jury(IEnumerable<string> votes) =>
            votes.Select((target, i) => new JuryReveal.Juror("juror-" + i, Jurors[i % Jurors.Length], target)).ToList();

        private IEnumerator SkipToTheResult(JuryReveal reveal)
        {
            yield return Seconds(CeremonyPacing.FadeIn + 0.5f);
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

        private static IEnumerator Until(System.Func<bool> done, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < until) yield return null;
        }

        private static IEnumerator Seconds(float seconds) => Until(() => false, seconds);

        private static TMP_Text Text(Component card, string name) =>
            card.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(label => label.name == name);

        private static string[] Texts(Component card, string name) =>
            card.GetComponentsInChildren<TMP_Text>(true).Where(label => label.name == name).Select(label => label.text).ToArray();

        private static RectTransform[] Rects(Component card, string name) =>
            card.GetComponentsInChildren<RectTransform>(true).Where(rect => rect.name == name).ToArray();

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
                .Select(label => "'" + label.text + "' (" + label.name + ")")
                .ToArray();
            Assert.That(blank, Is.Empty, where + ": copy that draws nothing: " + string.Join(" | ", blank));
        }
    }
}
