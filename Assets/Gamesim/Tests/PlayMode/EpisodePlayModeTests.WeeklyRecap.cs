using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Persistence;
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
                int fromSequence = before.nextSequence;
                var result = director.Submit(NextCommand(before));
                yield return null;
                if (!result.accepted) continue;
                if (result.state.events.Any(e => e.sequence >= fromSequence && e.kind == "eviction")) closedWeek = before.week;
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

        /// <summary>
        /// The recap after an eviction that commits into a full log. The season's log keeps its last
        /// 256 lines (EpisodeEngine.Log), so from then on a commit pushes out as many lines as it
        /// adds, and the director counted from where the log had ended to find what was new: it found
        /// nothing, so the eviction had no reveal and the week no recap - a twelve-person house from
        /// about week six, and the eight-person walk's final four once the house's own turns had
        /// filled it (rc1, and r2-walk's first walk: "The week 4 recap never opened"). Found by
        /// number now (EpisodeDirector.CommittedEvents). The season is a legal one, played by this
        /// fixture's own driver up to that commit.
        /// </summary>
        [UnityTest, Timeout(180000)]
        public IEnumerator WeeklyRecap_OpensAfterAnEvictionThatCommitsIntoAFullLog()
        {
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 12 && fixture == null; seed++)
            {
                var engine = new EpisodeEngine(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 12 }, seed));
                for (int guard = 0; guard < 400; guard++)
                {
                    var current = engine.Snapshot;
                    if (current.phase == EpisodePhase.Finished) break;
                    var result = engine.Apply(NextCommand(current));
                    Assert.That(result.accepted, Is.True, result.reason);
                    if (result.state.events.Count == current.events.Count
                        && result.state.events.Any(entry => entry.kind == "eviction" && entry.sequence >= current.nextSequence))
                    { fixture = current; break; }
                }
            }
            Assert.That(fixture, Is.Not.Null, "A bounded legal twelve-person season fills its log before an eviction.");
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            AssertEquivalent(fixture, director.Snapshot);
            // Nothing of the house's own may commit under the reveal: the recap drops itself then, rightly.
            HoldTheHouseForTheFixture();
            var screen = director.GetComponentInChildren<WeeklyRecapScreen>(true);
            Assert.That(screen.IsOpen, Is.False, "Nothing has closed yet.");

            var before = director.Snapshot;
            bool inputBefore = player.InputEnabled;
            var committed = director.Submit(NextCommand(before));
            Assert.That(committed.accepted, Is.True, committed.reason);
            Assert.That(committed.state.events.Count, Is.EqualTo(before.events.Count), "The log was full: the commit's lines pushed out as many as they added.");
            Assert.That(committed.state.events.Any(entry => entry.kind == "eviction" && entry.sequence >= before.nextSequence), Is.True,
                "The commit evicted somebody: its own lines, by number.");
            float deadline = Time.realtimeSinceStartup + 30f;
            while (!screen.IsOpen && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(screen.IsOpen, Is.True, "The week's recap opens once the eviction is narrated, full log or not. Reveal playing: "
                + (SceneComponents<VoteReveal>().FirstOrDefault()?.IsPlaying.ToString() ?? "no reveal"));
            Assert.That(screen.OpenWeek, Is.EqualTo(before.week));
            Assert.That(screen.Lines, Does.Contain("Evicted: " + WeeklyRecap.Build(director.Snapshot, before.week).evicted));
            ButtonWithCaption(WeeklyRecapScreen.ContinueCaption).onClick.Invoke();
            yield return null;
            Assert.That(screen.IsOpen, Is.False);
            Assert.That(director.IsWeeklyRecapOpen, Is.False);
            // What the house held before the night's commit, it holds after the recap: an eviction
            // night loaded at the vote is not the free roam the first-week test dismisses into.
            Assert.That(player.InputEnabled, Is.EqualTo(inputBefore), "Dismissing gives back what the recap took.");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortSeasonWalk_RecordsAFullLogEvictionAndItsObservedRecap()
        {
            var fixture = FullLogCommitFixture("eviction", state =>
                state.phase == EpisodePhase.Eviction && !state.evictionResolved
                && NextCommand(state).kind == EpisodeCommandKind.Advance);
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            AssertEquivalent(fixture, director.Snapshot);
            HoldTheHouseForTheFixture();
            player.Agent.speed = 25f; player.Agent.acceleration = 100f;
            var runner = BindPortConversationVerifier(out var owner);
            var previousPace = director.CeremonyPaceSetting;
            try
            {
                // The inert companion deliberately never runs Start. Match the standalone
                // workload's Quick pace before exercising its bounded recap observer.
                director.SetCeremonyPace(CeremonyPace.Quick);
                yield return PortConversationRoutine(runner, "OpenSeasonStation");
                var before = director.Snapshot;
                AssertEquivalent(fixture, before);
                Assert.That(PortConversationReport<int>(runner, "fullLogEvictions"), Is.Zero,
                    "Opening the actual station is not an eviction.");
                yield return PortConversationRoutine(runner, "PerformSeasonDecision", before, false);
                var after = director.Snapshot;
                Assert.That(SceneComponents<VoteReveal>().Single().Pace, Is.EqualTo(CeremonyPace.Quick),
                    "The actual reveal must use the standalone verifier's initialized pace.");
                Assert.That(after.revision, Is.EqualTo(before.revision + 1));
                var eviction = after.events.Single(entry => entry.sequence >= before.nextSequence && entry.kind == "eviction");
                Assert.That(before.events.Count, Is.EqualTo(256));
                Assert.That(after.events.Count, Is.EqualTo(256));
                Assert.That(PortConversationReport<int>(runner, "fullLogEvictions"), Is.EqualTo(1));
                Assert.That(PortConversationReport<int>(runner, "fullLogEvictionRecaps"), Is.Zero,
                    "A committed eviction is not evidence that the walk observed its recap.");
                var observations = PortConversationReport<IList>(runner, "evictions");
                Assert.That(observations.Count, Is.EqualTo(1));
                var observed = observations[0];
                var type = observed.GetType();
                Assert.That(type.GetField("eventSequence").GetValue(observed), Is.EqualTo(eviction.sequence));
                Assert.That(type.GetField("revisionBefore").GetValue(observed), Is.EqualTo(before.revision));
                Assert.That(type.GetField("revisionAfter").GetValue(observed), Is.EqualTo(after.revision));
                Assert.That(type.GetField("entriesBefore").GetValue(observed), Is.EqualTo(256));
                Assert.That(type.GetField("entriesAfter").GetValue(observed), Is.EqualTo(256));
                Assert.That(type.GetField("recapObserved").GetValue(observed), Is.EqualTo(false));

                yield return PortConversationRoutine(runner, "DismissWeeklyRecap", after, false);
                Assert.That(director.IsWeeklyRecapOpen, Is.False);
                AssertEquivalent(after, director.Snapshot);
                Assert.That(PortConversationReport<int>(runner, "fullLogEvictionRecaps"), Is.EqualTo(1));
                Assert.That(type.GetField("recapObserved").GetValue(observed), Is.EqualTo(true));
                // Visiting the same state again cannot manufacture another observation.
                yield return PortConversationRoutine(runner, "DismissWeeklyRecap", after, false);
                Assert.That(PortConversationReport<int>(runner, "fullLogEvictions"), Is.EqualTo(1));
                Assert.That(PortConversationReport<int>(runner, "fullLogEvictionRecaps"), Is.EqualTo(1));
                Assert.That(PortConversationReport<int>(runner, "weeklyRecaps"), Is.EqualTo(1));
            }
            finally
            {
                director.SetCeremonyPace(previousPace);
                UnityEngine.Object.Destroy(owner);
            }
        }

        /// <summary>A bounded, legally played history; no hand-edited phase, role or log records.</summary>
        private static EpisodeState FullLogCommitFixture(string eventKind, System.Func<EpisodeState, bool> eligible)
        {
            for (uint seed = 1; seed <= 12; seed++)
            {
                var engine = new EpisodeEngine(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 12 }, seed));
                for (int guard = 0; guard < 400; guard++)
                {
                    var before = engine.Snapshot;
                    if (before.phase == EpisodePhase.Finished) break;
                    var result = engine.Apply(NextCommand(before));
                    Assert.That(result.accepted, Is.True, result.reason);
                    if (before.events.Count == 256 && eligible(before)
                        && result.state.events.Any(entry => entry.sequence >= before.nextSequence && entry.kind == eventKind))
                        return before;
                }
            }
            Assert.Fail("No bounded legal full-log history for " + eventKind + ".");
            return null;
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

            foreach (var textSize in TextSizes.All)
            {
                screen.FontScale = TextSizes.Scale(textSize);
                yield return null;
                screen.Show(state, () => { });
                yield return null;
                ButtonWithCaption(WeeklyRecapScreen.YourWeekCaption).onClick.Invoke();
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                string where = TextScales.At(textSize) + ": ";
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
                    string name = "weekly-recap-your-week" + TextScales.Suffix(textSize);
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
