using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The screen a week ends on, inside a real episode.
    ///
    /// <para><c>WeeklyRecapTests</c> covers what the recap says. This covers that the screen exists
    /// in the scene, opens when a week closes, says what the recap says, and gives the house back
    /// when it is dismissed — the last of which is the one a pure test cannot see, because a screen
    /// that hides itself without telling the director leaves the player unable to move.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator WeeklyRecap_OpensWhenTheWeekClosesAndGivesTheHouseBackWhenDismissed()
        {
            var screen = director.GetComponentInChildren<WeeklyRecapScreen>(true);
            Assert.That(screen, Is.Not.Null, "The episode should stage a weekly recap screen.");
            Assert.That(screen.IsOpen, Is.False, "Nothing has closed yet.");

            // Play until the first eviction commits, then let the beats that narrate it finish.
            int closedWeek = 0;
            for (int guard = 0; guard < 400 && closedWeek == 0; guard++)
            {
                var before = director.Snapshot;
                if (before.phase == EpisodePhase.Finished) break;
                int known = before.events.Count;
                var result = director.Submit(NextCommand(before));
                yield return null;
                if (!result.accepted) continue;
                if (result.state.events.Skip(known).Any(e => e.kind == "eviction")) closedWeek = before.week;
            }
            Assert.That(closedWeek, Is.GreaterThan(0), "The season should have reached an eviction.");

            // A wall-clock deadline, not a frame count. The vote reveal holds for roughly fourteen real
            // seconds at the suspenseful pace and batchmode renders far faster than that, so counting
            // frames waits a fraction of the time the beat actually takes — which is the same trap
            // reduced motion exists to sidestep and the reason nothing in this file is timed in frames.
            float deadline = Time.realtimeSinceStartup + 30f;
            while (!screen.IsOpen && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(screen.IsOpen, Is.True,
                "The recap should open once the eviction is narrated. Reveal still playing: "
                + (SceneComponents<VoteReveal>().FirstOrDefault()?.IsPlaying.ToString() ?? "no reveal"));
            Assert.That(screen.OpenWeek, Is.EqualTo(closedWeek));
            Assert.That(screen.Browsing, Is.False);
            Assert.That(director.IsWeeklyRecapOpen, Is.True);
            Assert.That(director.IsPanelOpen, Is.True, "A full-screen scrim is a panel.");
            Assert.That(player.InputEnabled, Is.False, "The player should not be walking behind it.");

            // What it says is what the recap says.
            var expected = WeeklyRecap.Build(director.Snapshot, closedWeek);
            Assert.That(screen.Lines, Is.Not.Empty);
            Assert.That(screen.Lines.Any(line => line.StartsWith("Evicted: ")), Is.True);
            if (expected.evicted != null)
                Assert.That(screen.Lines, Does.Contain("Evicted: " + expected.evicted));
            if (Application.isBatchMode) yield return CaptureFraming("weekly-recap");

            var dismiss = ButtonWithCaption(WeeklyRecapScreen.ContinueCaption);
            Assert.That(dismiss.IsInteractable(), Is.True);
            dismiss.onClick.Invoke();
            yield return null;

            Assert.That(screen.IsOpen, Is.False);
            Assert.That(director.IsWeeklyRecapOpen, Is.False);
            Assert.That(player.InputEnabled, Is.True, "Dismissing has to give the house back.");
        }

        [UnityTest]
        public IEnumerator WeeklyRecap_ReviewingAnEarlierWeekIsReadOnlyAndClosesCleanly()
        {
            var screen = director.GetComponentInChildren<WeeklyRecapScreen>(true);
            var before = director.Snapshot;

            director.ReviewWeek(1);
            yield return null;

            Assert.That(screen.IsOpen, Is.True);
            Assert.That(screen.Browsing, Is.True, "Looking back is not closing a week.");
            Assert.That(screen.OpenWeek, Is.EqualTo(1));
            Assert.That(DirectorHasButton(WeeklyRecapScreen.ContinueCaption), Is.False,
                "There is nothing to continue to; this week has not finished.");

            AssertEquivalent(before, director.Snapshot);

            ButtonWithCaption(WeeklyRecapScreen.BackCaption).onClick.Invoke();
            yield return null;
            Assert.That(screen.IsOpen, Is.False);
            Assert.That(player.InputEnabled, Is.True);
            AssertEquivalent(before, director.Snapshot);
        }

        [UnityTest]
        public IEnumerator WeeklyRecap_BackFromAReviewOpenedOverTheLiveWeekReturnsToItsContinue()
        {
            var screen = director.GetComponentInChildren<WeeklyRecapScreen>(true);
            var state = director.Snapshot;
            screen.Show(state, () => { });
            yield return null;
            Assert.That(screen.IsOpen && !screen.Browsing, Is.True);
            // The same path the recap's own Review button takes, for a week that has one behind it.
            screen.Review(state, 1);
            yield return null;
            Assert.That(screen.Browsing, Is.True);
            ButtonWithCaption(WeeklyRecapScreen.BackCaption).onClick.Invoke();
            yield return null;
            Assert.That(screen.IsOpen, Is.True, "Back from a review over the live week returns to that week's recap,");
            Assert.That(screen.Browsing, Is.False);
            Assert.That(DirectorHasButton(WeeklyRecapScreen.ContinueCaption), Is.True, "with its Continue.");
            ButtonWithCaption(WeeklyRecapScreen.ContinueCaption).onClick.Invoke();
            yield return null;
            Assert.That(screen.IsOpen, Is.False);
        }

        /// <summary>
        /// YOUR WEEK (ACTIONS-DEALS-ALLIANCES-PLAN V4) on the recap. A week closed by the engine, with
        /// what the player held pinned as fixtures in the rows the engine writes: a claim the reveal
        /// bore out and one it did not, a deal the other side kept, a promise the player broke, and a
        /// call one ally followed and one did not, judged by the ballots the reveal read. The Your
        /// week tab shows every line the reader judged behind its verdict chip, and Game Sense so far;
        /// every line fits its card at both text sizes, the seam carries the same lines, and reading it
        /// commits nothing. Photographed at both text sizes in the 16:9 frame and the 4:3 one the
        /// recap was laid out for.
        /// </summary>
        [UnityTest]
        public IEnumerator WeeklyRecap_YourWeekShowsWhatTheWeekMadeOfWhatYouHeld()
        {
            var screen = director.GetComponentInChildren<WeeklyRecapScreen>(true);
            var state = FirstWeekClosed();
            int week = state.week;
            var you = state.Find(state.playerId);
            var ballot = state.votes.First(vote => vote.voterId != state.playerId && vote.voterId != state.hohId);
            string spared = state.nominees.First(id => id != ballot.targetId);
            state.ledger.claims.Add(new ClaimRow { week = week, voterId = ballot.voterId, targetId = ballot.targetId, source = ClaimSource.Told, status = ClaimStatus.Kept });
            state.ledger.claims.Add(new ClaimRow { week = week, voterId = ballot.voterId, targetId = spared, source = ClaimSource.Overheard, status = ClaimStatus.Lied });
            var others = state.contestants.Where(c => !c.isPlayer && c.id != ballot.voterId && !state.nominees.Contains(c.id)).ToList();
            var partner = state.Find(ballot.voterId);
            RelationshipLedger.Record(state, state.playerId, partner.id, YourWeek.DealKept, 18, partner.name + " honoured a vote to evict with " + you.name + ".");
            var promised = others.FirstOrDefault() ?? state.Find(spared);
            state.promises.Add(new PromiseState { id = "promise-your-week", fromId = state.playerId, toId = promised.id, targetId = spared,
                kind = PromiseKind.Vote, status = PromiseStatus.Broken, week = week, expiresWeek = week });
            state.memories.Add(new MemoryState { ownerId = state.playerId, subjectId = promised.id, text = you.name + " broke a Vote promise.", week = week, isPrivate = true });
            string evicted = state.ledger.power.Single(p => p.week == week).evicteeId;
            string called = evicted != state.playerId ? evicted : state.nominees.First(id => id != state.playerId);
            // The week is still on screen, so the reveal's ballots judge the call: one member who voted
            // out who was called and one who did not, or, where the vote gave nobody of either, one who
            // did not vote and stands as they said at the call.
            var active = state.contestants.Where(c => !c.isPlayer && c.status == ContestantStatus.Active).ToList();
            string BallotOf(string id) => state.votes.FirstOrDefault(vote => vote.voterId == id)?.targetId;
            var follower = active.FirstOrDefault(c => BallotOf(c.id) == called) ?? active.First(c => BallotOf(c.id) == null);
            var defector = active.FirstOrDefault(c => c.id != follower.id && BallotOf(c.id) != null && BallotOf(c.id) != called)
                ?? active.First(c => c.id != follower.id && BallotOf(c.id) == null);
            state.alliances.Add(new AllianceState { id = "alliance-your-week", name = "The Recap Pact", members = new List<string> { state.playerId, follower.id, defector.id }, active = true });
            state.ledger.calls.Add(new BlocCallRow
            {
                week = week, allianceId = "alliance-your-week", callerId = state.playerId, targetId = called,
                followed = new List<string> { follower.id }, defected = new List<string> { defector.id },
            });

            var mine = YourWeek.Build(state, week);
            var verdicts = mine.Lines.Select(line => line.verdict).ToList();
            foreach (var verdict in new[] { YourWeek.Verdicts.Right, YourWeek.Verdicts.Wrong, YourWeek.Verdicts.Kept, YourWeek.Verdicts.Broken,
                         YourWeek.Verdicts.Followed, YourWeek.Verdicts.Defected })
                Assert.That(verdicts, Does.Contain(verdict), "The fixture holds a line the week judged " + verdict + ".");
            foreach (var member in mine.calls.Where(line => line.kind == YourWeek.Kinds.Member))
                Assert.That(member.basis, Is.EqualTo(BallotOf(member.aboutId) != null ? YourWeek.Bases.Ballot : YourWeek.Bases.Call),
                    "A member who voted is judged by the ballot the reveal read; one who did not, by what they said at the call.");
            string before = JsonUtility.ToJson(state);

            foreach (bool larger in new[] { false, true })
            {
                screen.FontScale = larger ? 1.2f : 1f;
                yield return null;
                screen.Show(state, () => { });
                yield return null;
                ButtonWithCaption(WeeklyRecapScreen.YourWeekCaption).onClick.Invoke();
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                string where = (larger ? "Larger text" : "Resting text") + ": ";
                Assert.That(screen.OpenTab, Is.EqualTo(System.Array.IndexOf(WeeklyRecapScreen.TabCaptions, WeeklyRecapScreen.YourWeekCaption)), where + "the tab opened.");
                Assert.That(ButtonWithCaption(WeeklyRecapScreen.ContinueCaption), Is.Not.Null, where + "Continue is still the one way on.");

                var body = LastActive(WeeklyRecapScreen.TabBodyName);
                Assert.That(body, Is.Not.Null);
                foreach (var part in new[] { WeeklyRecapScreen.ReadsName, WeeklyRecapScreen.WordName, WeeklyRecapScreen.CallsName, WeeklyRecapScreen.SenseName })
                {
                    var card = LastActive(part);
                    Assert.That(card, Is.Not.Null, where + part);
                    Assert.That(card.IsChildOf(body), Is.True, where + part + " is the tab's.");
                }
                var words = LabelsUnder(body);
                foreach (var line in mine.Lines)
                {
                    Assert.That(words, Does.Contain(line.text), where + "every judged line is on the tab.");
                    if (line.verdict != null) Assert.That(words, Does.Contain(WeeklyRecapScreen.VerdictWord(line.verdict)), where + line.text);
                }
                Assert.That(words, Does.Contain("STRATEGY").And.Contain(mine.sense.strategy.ToString()), where + "Game Sense so far, in the part the player can see.");
                Assert.That(words, Does.Contain(WeeklyRecapScreen.SenseSubtitle), where + "and what waits for the season's end.");
                Assert.That(words, Does.Not.Contain("SOCIAL").And.Not.Contain("GAME SENSE"), where + "neither the number nor the faces that rest on what the house keeps to itself.");
                // Each judged line's verdict is the chip on its own row.
                foreach (var row in body.GetComponentsInChildren<RectTransform>().Where(rect => rect.name == WeeklyRecapScreen.VerdictRowName))
                {
                    var text = row.GetComponentsInChildren<TMPro.TMP_Text>().Select(label => label.text).ToList();
                    var line = mine.Lines.FirstOrDefault(item => text.Contains(item.text));
                    Assert.That(line, Is.Not.Null, where + "a row carries a judged line: " + string.Join(" | ", text));
                    if (line.verdict != null) Assert.That(text, Does.Contain(WeeklyRecapScreen.VerdictWord(line.verdict)), where + line.text);
                }
                AssertDecisionCopyFits(body);
                foreach (var line in mine.Lines) Assert.That(screen.Lines, Does.Contain(line.ToString()), where + "the seam carries it too.");
                Assert.That(screen.Lines, Does.Contain(WeeklyRecapScreen.SenseLine(mine.sense)));
                if (Application.isBatchMode)
                {
                    // The tab is the foot of the card's scroll: bring it into view, as a player reading it would.
                    var scroll = screen.GetComponentsInChildren<ScrollRect>().LastOrDefault(item => item.isActiveAndEnabled);
                    if (scroll != null) { scroll.verticalNormalizedPosition = 0f; Canvas.ForceUpdateCanvases(); }
                    string name = larger ? "weekly-recap-your-week-large" : "weekly-recap-your-week";
                    yield return CaptureFraming(name);
                    // The batch canvas is 4:3, and the recap is laid out for the canvas it opened on:
                    // the 4:3 frame shows that layout whole, where the 16:9 one crops what overflows it.
                    yield return CaptureFraming(name + "-4x3", width: 1200, height: 900);
                }
                screen.Hide();
                yield return null;
            }
            screen.FontScale = 1f;
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before), "Reading the week changes nothing.");
        }

        private bool DirectorHasButton(string caption) =>
            director.GetComponentsInChildren<Button>(true).Any(button => button.IsActive()
                && button.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(text => text.text == caption));
    }
}
