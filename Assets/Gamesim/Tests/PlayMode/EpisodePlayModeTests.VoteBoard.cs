using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The live eviction's board on a set's screen (UI-UX-PASS-PLAN B0, the owner's mockup): the
    /// two nominees with their counts, the CURRENT TALLY card, one anonymous slot a vote, the
    /// progress dots and "Revealing vote N of M"; the gold deciding row on a tie; the result read
    /// over the board. Nothing on it names a voter. Captured for the look sheet on a stand-in screen
    /// - the card is a function of the screen it is given, so a board floating clear of the house is
    /// the same board the living room's screen draws - and the HUD's own card at both text sizes.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static VoteReveal.Ballot Ballot(string target, bool tieBreak = false) => new VoteReveal.Ballot(target, tieBreak);

        private static string Words(Component card, string name) =>
            card.GetComponentsInChildren<TMP_Text>(true).First(label => label.name == name).text;

        private static RectTransform[] SlotsUp(Component card) =>
            card.GetComponentsInChildren<RectTransform>(true).Where(rect => rect.name == "Ballot" && rect.gameObject.activeSelf).ToArray();

        /// <summary>No child the old roster named a voter by, and no label naming the Head of Household but the deciding row's and the host's (<paramref name="hohName"/> null where the Head of Household is the player, whom the row addresses rather than names).</summary>
        private static void AssertBoardNamesNobody(Component card, string hohName)
        {
            foreach (var name in new[] { "Ballot voter", "Ballot face", "Ballot target", "Roster" })
                Assert.That(card.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == name), Is.False, "The board has no '" + name + "'.");
            if (hohName == null) return;
            var labels = card.GetComponentsInChildren<TMP_Text>(true).Where(label => label.name != "Deciding voter" && label.name != "Host");
            Assert.That(labels.Select(label => label.text), Has.None.Contains(hohName), "Only the deciding row names the Head of Household.");
        }

        private IEnumerator CaptureBoard(ScreenSurface screen, string name)
        {
            cameraRig.MoveTo(screen.Shot());
            yield return Frames(2);
            if (Application.isBatchMode) yield return CaptureFraming(name, settle: false);
        }

        [UnityTest]
        public IEnumerator VoteBoard_TheScreensBoardNamesNobodyAndIsCaptured()
        {
            yield return null;
            // A stand-in screen well above the house, facing the way the living room's does.
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Vote board stand-in";
            board.transform.position = new Vector3(0f, 40f, -3f);
            board.transform.localScale = new Vector3(2.6f, 1.5f, 0.05f);
            var screen = ScreenSurface.Measure(board.transform, "Living", new Vector3(0f, 40f, 0f));
            Assert.That(screen, Is.Not.Null, "A board with a renderer is a screen to play a card on.");
            var block = new[] { new VoteReveal.Nominee("a", "Emma Brown", null), new VoteReveal.Nominee("b", "Riley Johnson", null) };
            const string hoh = "Maya Hassan";
            var card = VoteReveal.Attach(director.gameObject);

            // Five votes, three read: the mockup's frame. The card runs on the unscaled clock, which
            // no captureDeltaTime pins, so it is held at the moment while the frame is taken: the
            // fourth vote cannot land between the wait and the capture.
            Assert.That(card.Play(1, block, new[] { Ballot("a"), Ballot("b"), Ballot("a"), Ballot("a"), Ballot("b") }, "a", true,
                CeremonyPace.Suspenseful, hoh, screen: screen), Is.True);
            yield return WaitFor(() => card.VotesShown >= 3, 20f, "three votes on the board");
            card.Held = true;
            yield return Frames(2);
            Assert.That(card.VotesShown, Is.EqualTo(3), "Held, the count stands.");
            Assert.That(SlotsUp(card), Has.Length.EqualTo(3), "One anonymous slot a vote read.");
            Assert.That(Words(card, "Progress"), Is.EqualTo("Revealing vote 3 of 5"));
            Assert.That(Words(card, "Tally heading"), Is.EqualTo("CURRENT TALLY"));
            Assert.That(Words(card, "Tally line"), Is.EqualTo("Votes are revealed anonymously."));
            Assert.That(Words(card, "Anonymous badge text"), Is.EqualTo("The identity of each voter remains a secret."));
            AssertBoardNamesNobody(card, hoh);
            yield return CaptureBoard(screen, "vote-board-anonymous");
            Assert.That(Words(card, "Progress"), Is.EqualTo("Revealing vote 3 of 5"), "The frame taken is the third vote's.");
            card.Cancel();
            yield return null;

            // Two all, and the Head of Household's deciding vote: the gold row, the one with a name on
            // it - held before the result takes the board over.
            Assert.That(card.Play(1, block, new[] { Ballot("a"), Ballot("b"), Ballot("b"), Ballot("a"), Ballot("a", true) }, "a", true,
                CeremonyPace.Quick, hoh, screen: screen), Is.True);
            yield return WaitFor(() => card.VotesShown >= 5, 30f, "the deciding vote");
            card.Held = true;
            yield return Frames(2);
            var deciding = card.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(rect => rect.name == "Deciding vote");
            Assert.That(deciding != null && deciding.gameObject.activeInHierarchy, Is.True, "The deciding row is up once the tie is broken.");
            Assert.That(Words(deciding, "Deciding voter"), Is.EqualTo(hoh));
            AssertBoardNamesNobody(card, hoh);
            yield return CaptureBoard(screen, "vote-board-tiebreak");
            Assert.That(card.ShowingResult, Is.False, "The frame taken is the board's, before the result.");
            card.Held = false;
            yield return WaitFor(() => card.ShowingResult, 30f, "the result");
            // The board hands over to the result block over the result's first moment; held there
            // before the quick pace's fade-out.
            yield return new WaitForSecondsRealtime(0.6f);
            card.Held = true;
            yield return Frames(2);
            Assert.That(card.IsPlaying, Is.True, "The result is still up to be photographed.");
            Assert.That(Words(card, "Result lead"), Is.EqualTo("BY THE HEAD OF HOUSEHOLD'S VOTE"));
            yield return CaptureBoard(screen, "vote-board-result");
            Assert.That(card.IsPlaying && card.ShowingResult, Is.True, "The frame taken is the result's.");
            card.Cancel();
            yield return null;

            // The HUD's own card at both text sizes: no board, and a single vote names nobody.
            foreach (bool larger in new[] { false, true })
            {
                card.FontScale = larger ? 1.2f : 1f;
                Assert.That(card.Play(1, block, new[] { Ballot("a") }, "a", true, CeremonyPace.Suspenseful, hoh), Is.True);
                card.SkipToResult();
                yield return Frames(2);
                Assert.That(Words(card, "Host"), Is.EqualTo("By a single vote, Emma Brown, you have been evicted."));
                Assert.That(card.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "Tally card" || rect.name == "Ballot"), Is.False,
                    "The HUD's card has no board.");
                AssertBoardNamesNobody(card, hoh);
                if (Application.isBatchMode) yield return CaptureFraming(larger ? "vote-card-hud-large" : "vote-card-hud");
                card.Cancel();
                yield return null;
            }

            Object.Destroy(card.gameObject);
            Object.Destroy(board);
            yield return null;
        }
    }
}
