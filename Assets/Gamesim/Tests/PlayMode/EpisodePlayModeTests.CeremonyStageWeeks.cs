using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The cut scenes every week (PACK8-PASS-PLAN A1). The first staged eviction stopped the house's
    /// world as its walk out ended: the stage, still keeping the house in its seats, counted the
    /// evicted as the house's after the walk out had switched their body off. The coordinator failed
    /// on "An eligible NPC root is inactive.", and every ceremony after it played on the HUD with
    /// nothing in the log to say why. These play past that eviction into the weeks after it.
    ///
    /// <para>Every wait is bounded in real seconds, as the stage's own tests are: the stage, the
    /// cards and the walk out run on the unscaled clock.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>Who the eviction played by <see cref="PlayTheStagedEvictionToItsWalkOut"/> sent out of the door.</summary>
        private string stagedLeaving;

        /// <summary>The kind of the ceremony <see cref="PlayOnToTheNextStagedCeremony"/> last found staged.</summary>
        private string stagedKind;

        /// <summary>
        /// Commits eviction night until it is staged, plays its card on the living room's screen and
        /// skips it, and sits out the goodbye: the evicted are walking out, and the house keeps its
        /// seats while they go.
        /// </summary>
        private IEnumerator PlayTheStagedEvictionToItsWalkOut()
        {
            stagedLeaving = null;
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The eviction is staged in the living room.");
            Assert.That(director.CeremonyStageKind, Is.EqualTo(CeremonySting.EvictionKind));
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing,
                EpisodeDirector.SummonsHardSeconds(CeremonyPace.Suspenseful) + 6f, "the reveal plays once the nominees have taken the hot seats");
            string evicted = SceneComponents<VoteReveal>().Single().EvictedId;
            Assert.That(evicted, Is.Not.Null.And.Not.EqualTo(director.Snapshot.playerId), "A houseguest is evicted.");
            yield return SkipReveals();
            // PACK8-PASS-PLAN C2: the goodbye comes first.
            yield return WaitFor(() => director.WalkingOutId != null, EpisodeDirector.GoodbyeSeconds + 2f, "the goodbye gives way to the walk out");
            Assert.That(director.WalkingOutId, Is.EqualTo(evicted), "The evicted walk out,");
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Release), "while the house keeps its seats.");
            stagedLeaving = evicted;
        }

        /// <summary>After a staged walk out: the house's world still running, the stage letting the house go, and the world ready for the next ceremony.</summary>
        private IEnumerator AssertTheHouseRunsOn(string after)
        {
            // The frame the walk out ends is the frame the fault struck.
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, after + ": the house's world is still running.");
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, after + ": the house gets up");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, after + ": the house's world is still running once it is up.");
            yield return WaitFor(() => director.NpcAutonomyReady, 5f, after + ": the house's world is ready for the next ceremony");
        }

        /// <summary>The week's recap follows an eviction's walk out; closed as the player closes it, if it came.</summary>
        private IEnumerator CloseTheWeeklyRecap()
        {
            for (int wait = 0; wait < 10 && !director.IsWeeklyRecapOpen; wait++) yield return null;
            if (!director.IsWeeklyRecapOpen) yield break;
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// Plays the season on with the house's next legal decisions until a nomination or an
        /// eviction is committed, and asserts the house stages it: the card on the set's own screen,
        /// skipped as a player skips it, the walk out after an eviction skipped too, and the house's
        /// world still running for the ceremony after.
        /// </summary>
        private IEnumerator PlayOnToTheNextStagedCeremony(string what, int steps = 40)
        {
            stagedKind = null;
            for (int step = 0; step < steps; step++)
            {
                yield return ContinueCompetitionResults(byKeyboard: false);
                var before = director.Snapshot;
                Assert.That(before.phase, Is.Not.EqualTo(EpisodePhase.Finished), what + ": the season ended first.");
                var result = director.Submit(NextCommand(before));
                Assert.That(result.accepted, Is.True, before.phase + ": " + result.reason);
                var ceremony = director.Snapshot.events.Skip(before.events.Count).LastOrDefault(entry =>
                    (entry.kind == CeremonySting.NominationKind || entry.kind == CeremonySting.EvictionKind)
                    && (entry.audienceIds.Count == 0 || entry.audienceIds.Contains(before.playerId)));
                if (ceremony == null) { yield return null; continue; }

                stagedKind = ceremony.kind;
                Assert.That(director.IsCeremonyStaged, Is.True, what + " (week " + before.week + ") is staged in the house, not played on the HUD."
                    + " The house's world: " + (director.NpcAutonomyDiagnostic ?? "running") + ".");
                Assert.That(director.CeremonyStageKind, Is.EqualTo(ceremony.kind), what + " is staged as itself.");
                director.SkipCeremonySummons();
                yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing, 3f,
                    what + ": the card plays once the summons is skipped");
                string evicted = null;
                if (ceremony.kind == CeremonySting.NominationKind)
                {
                    var keys = SceneComponents<KeyCeremony>().Single();
                    Assert.That(keys.IsPlaying && keys.Surface != null, Is.True, what + ": the keys play on the set's screen.");
                    Assert.That(keys.Surface.Room, Is.EqualTo("Nomination"), what + ": at the nomination table.");
                }
                else
                {
                    var vote = SceneComponents<VoteReveal>().Single();
                    Assert.That(vote.IsPlaying && vote.Surface != null, Is.True, what + ": the vote plays on the set's screen.");
                    Assert.That(vote.Surface.Room, Is.EqualTo("Living"), what + ": in the living room.");
                    evicted = vote.EvictedId;
                }
                yield return SkipReveals();
                if (evicted != null && evicted != before.playerId)
                {
                    yield return Frames(3);
                    // The goodbye, or the walk out after it: a press goes straight to the shut door from either.
                    Assert.That(director.DepartingId == evicted || director.WalkingOutId == evicted, Is.True,
                        what + ": the evicted say goodbye and walk out.");
                    director.SkipWalkOut();
                    yield return null;
                    Assert.That(director.StagedExitRunning, Is.False, what + ": the skip reaches the shut door.");
                }
                yield return AssertTheHouseRunsOn(what);
                if (ceremony.kind == CeremonySting.EvictionKind) yield return CloseTheWeeklyRecap();
                yield break;
            }
            Assert.Fail(what + ": no nomination or eviction within " + steps + " decisions.");
        }

        /// <summary>
        /// The first season from <paramref name="from"/> on, shaped by <paramref name="shape"/>, in
        /// which the house's next legal decisions keep the player in the house to the end of
        /// <paramref name="throughWeek"/>: the walk below asserts a staged nomination and eviction in
        /// every week, and a player evicted on the way would end it early. Measured on the engine
        /// alone, as the other fixtures are found; the director plays the same decisions, and its
        /// own world commits nothing in the frames the walk spends in free time. Measured
        /// 2026-09-30 from 52, eviction night: 52 loses the player in week two, 53 and 54 in week
        /// three, and 55 keeps them to the final three.
        /// </summary>
        private static uint SeasonThePlayerSurvives(uint from, System.Action<EpisodeState> shape, int throughWeek)
        {
            for (uint seed = from; seed < from + 60; seed++)
            {
                var engine = new EpisodeEngine(StrategySeason(seed, shape));
                bool kept = true;
                for (int guard = 0; guard < 240; guard++)
                {
                    var state = engine.Snapshot;
                    if (state.Find(state.playerId).status != ContestantStatus.Active) { kept = false; break; }
                    if (state.week > throughWeek || state.phase == EpisodePhase.Finished) break;
                    if (!engine.Apply(NextCommand(state)).accepted) { kept = false; break; }
                }
                if (kept && engine.Snapshot.week > throughWeek) return seed;
            }
            Assert.Fail("No season from seed " + from + " keeps the player in the house through week " + throughWeek + ".");
            return from;
        }

        /// <summary>
        /// The regression itself (PACK8-PASS-PLAN §1.1): the evicted walk out to the deck and the walk
        /// ends while the stage still keeps the house in its seats. It failed here with "An eligible
        /// NPC root is inactive.", and nothing was staged again until a load.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_AStagedEvictionsWalkOutLeavesTheHouseRunning()
        {
            yield return InstallStagedSeason(52, AtEviction);
            yield return PlayTheStagedEvictionToItsWalkOut();
            var body = SceneComponents<HouseNpc>().Single(npc => npc.Id == stagedLeaving);
            yield return WaitFor(() => director.WalkingOutId == null, EpisodeDirector.WalkOutSeconds + 2f, "the walk-out ends");
            Assert.That(body.gameObject.activeInHierarchy, Is.False, "The evicted are gone at the end of their walk.");
            yield return AssertTheHouseRunsOn("A staged walk out that ran to its end");
        }

        /// <summary>The same with the walk out skipped, as a press skips it: the body goes at once, under the stage's release.</summary>
        [UnityTest]
        public IEnumerator CeremonyStage_ASkippedStagedWalkOutLeavesTheHouseRunning()
        {
            yield return InstallStagedSeason(52, AtEviction);
            yield return PlayTheStagedEvictionToItsWalkOut();
            var body = SceneComponents<HouseNpc>().Single(npc => npc.Id == stagedLeaving);
            director.SkipWalkOut();
            Assert.That(director.WalkingOutId, Is.Null, "The skip ends the walk out,");
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Release), "with the house still in its seats for a moment,");
            Assert.That(body.gameObject.activeInHierarchy, Is.False, "and the evicted go with it.");
            yield return AssertTheHouseRunsOn("A staged walk out skipped");
        }

        /// <summary>
        /// Path B (PACK8-PASS-PLAN §1.1): the evicted cannot take their navigation back for the hot
        /// seat. Their body is moved in the frame the stage re-binds it, as the owner's log had a
        /// root left on a couch, so the binding fails. The house lets that one body go, the card
        /// stops waiting for them, and the house's world carries on - it used to fail whole, and
        /// the card played on the HUD.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_AnEvictedBodyThatCannotRebindDoesNotStopTheHouse()
        {
            yield return InstallStagedSeason(52, AtEviction);
            HouseNpc moved = null;
            for (int step = 0; step < 12 && !director.IsCeremonyStaged; step++)
            {
                var result = director.Submit(NextCommand(director.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
                if (director.IsCeremonyStaged && director.DepartingId != null)
                {
                    // Before any frame runs: the stage has just begun re-binding them.
                    moved = SceneComponents<HouseNpc>().Single(npc => npc.Id == director.DepartingId);
                    moved.transform.position = new Vector3(60f, 0f, 60f);
                    Physics.SyncTransforms();
                }
                yield return null;
            }
            Assert.That(moved, Is.Not.Null, "The eviction is staged with a houseguest leaving.");
            string leaving = moved.Id;
            string LineFor() => StageReport().Split('\n').FirstOrDefault(line => line.StartsWith("  " + leaving + " -> "));
            yield return WaitFor(() => LineFor() != null && LineFor().EndsWith(":: let go (their body could not take its navigation back)"), 5f,
                leaving + " is let go when their body cannot take its navigation back");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "The house's world carries on without them.");
            Assert.That(director.IsCeremonyStaged, Is.True, "The eviction is still staged.");

            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing,
                EpisodeDirector.SummonsHardSeconds(CeremonyPace.Suspenseful) + 6f, "the card plays without them");
            var vote = SceneComponents<VoteReveal>().Single();
            Assert.That(vote.IsPlaying && vote.Surface != null && vote.Surface.Room == "Living", Is.True, "on the living room's screen.");
            Assert.That(vote.EvictedId, Is.EqualTo(leaving), "The vote is the committed one.");
            yield return SkipReveals();
            yield return WaitFor(() => director.WalkingOutId == null && director.DepartingId == null, 10f, "The walk out gives up on them.");
            Assert.That(moved.gameObject.activeInHierarchy, Is.False, "They go as they always did.");
            yield return AssertTheHouseRunsOn("An evicted body let go");
            yield return CloseTheWeeklyRecap();
            yield return PlayOnToTheNextStagedCeremony("The next nomination");
            Assert.That(stagedKind, Is.EqualTo(CeremonySting.NominationKind), "The next ceremony is the nomination, and it is staged.");
        }

        /// <summary>
        /// A competition started while the staged evicted are still walking out ends their walk
        /// before the arena lets the stage go (Challenge.cs's FinishWalkOut, then
        /// BeginCompetitionArena's EndCeremonyStage), so the body goes while the stage still keeps
        /// the house. The player stays at the episode screen for it: their move to the gallery is
        /// refused, as it is whenever something else has them.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_ACompetitionStartedMidWalkLetsTheStagedEvictedGo()
        {
            yield return InstallStagedSeason(52, AtEviction);
            WarpPlayer(director.StationPosition);
            var elsewhere = new object();
            Assert.That(player.TryBeginActivityMove(elsewhere, OnFootFrom(director.StationPosition, 0.5f, 2f), out var why), Is.True, why);
            yield return PlayTheStagedEvictionToItsWalkOut();
            player.ReleaseActivityMove(elsewhere);
            string leaving = stagedLeaving;
            var body = SceneComponents<HouseNpc>().Single(npc => npc.Id == leaving);

            // On to the next competition while they walk: the week does not wait for them.
            for (int step = 0; step < 8 && !EpisodeEngine.IsCompetition(director.Snapshot.phase); step++)
            {
                var result = director.Submit(NextCommand(director.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
                yield return null;
            }
            Assert.That(EpisodeEngine.IsCompetition(director.Snapshot.phase), Is.True, "The next competition is up.");
            Assert.That(director.WalkingOutId, Is.EqualTo(leaving), "They are still walking out as the week moves on,");
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Release), "and the house still keeps its seats.");
            Assert.That(director.TryOpenPhasePanel(), Is.True, "The player is at the episode screen.");
            // The chrome is aside for the whole staged exit (PACK8-PASS-PLAN C2): it is drawn at
            // alpha 0 and takes no clicks, and the stage owns the press, so no player can press
            // this button now and ButtonWithCaption's pressability check would rightly refuse it.
            // The test calls the button's own handler with FindButton instead, because what it
            // proves is the guard behind it - StartChallenge's FinishWalkOut(immediate) under a
            // staged walk - which a competition started any other way would reach as well.
            FindButton("Practice this competition").onClick.Invoke();
            yield return null;
            Assert.That(director.IsChallengeActive, Is.True, "The competition starts,");
            Assert.That(director.WalkingOutId, Is.Null, "the walk out gives way to it,");
            Assert.That(body.gameObject.activeInHierarchy, Is.False, "the evicted go as they always went,");
            // PACK8-PASS-PLAN C2: at once - no hold on a shut door, no dip - with the door struck.
            Assert.That(director.StagedExitRunning, Is.False, "the exit is over,");
            Assert.That(DoorSetUp(), Is.False, "the door struck with it,");
            Assert.That(director.IsCeremonyStaged, Is.False, "the arena lets the stage go,");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "and the house's world is still running.");
        }

        /// <summary>
        /// A season continued at the nominations (PACK8-PASS-PLAN §1.1, "a load mid-week"): the
        /// house's world was built only in free time and the campaign, so a load at the nominations
        /// had none and that week's ceremonies were never staged. It is built from the load now, as
        /// a played season carries it, paused outside free time - and without the diagnostic build
        /// the stage's other fixtures use.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_ASeasonLoadedAtTheNominationsStagesThem()
        {
            HoldTheHouseForTheFixture();
            yield return InstallStrategySeason(51, AtNomination);
            yield return Frames(3);
            if (Application.isBatchMode)
                Assert.That(director.NpcAutonomyReady, Is.False,
                    "A batch run asking for neither stages nor walk outs builds no world past free time: its fixtures were written without one.");
            AskForTheStages();
            yield return WaitFor(() => director.NpcAutonomyReady, 5f, "The loaded season builds the house's world at the nominations.");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
            Assert.That(NpcSocialState.IsEligible(director.Snapshot), Is.False, "The nominations are past free time: the world is there, paused.");

            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The nomination of a season loaded at the nominations is staged.");
            Assert.That(director.CeremonyStageKind, Is.EqualTo(CeremonySting.NominationKind));
            director.SkipCeremonySummons();
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing, 3f, "the card plays");
            var keys = SceneComponents<KeyCeremony>().Single();
            Assert.That(keys.Surface != null && keys.Surface.Room == "Nomination", Is.True, "on the nomination table's screen.");
            yield return SkipReveals();
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the stage ends with the card");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
        }

        /// <summary>
        /// The owner's report (PACK8-PASS-PLAN §1.1): the cut scenes played in week one and never
        /// again. From eviction night of week one, the season is played on through two more weeks;
        /// every nomination and every eviction is staged in the house, each on its own set's screen.
        /// </summary>
        [UnityTest]
        public IEnumerator CeremonyStage_EveryWeeksNominationAndEvictionIsStagedAfterAStagedEviction()
        {
            uint seed = SeasonThePlayerSurvives(52, AtEviction, 3);
            yield return InstallStagedSeason(seed, AtEviction);
            yield return PlayTheStagedEvictionToItsWalkOut();
            yield return WaitFor(() => director.WalkingOutId == null, EpisodeDirector.WalkOutSeconds + 2f, "week 1's walk-out ends");
            yield return AssertTheHouseRunsOn("Week 1's eviction");
            yield return CloseTheWeeklyRecap();
            foreach (int week in new[] { 2, 3 })
            {
                var state = director.Snapshot;
                Assert.That(state.Find(state.playerId).status, Is.EqualTo(ContestantStatus.Active),
                    "The player is in the house for week " + week + ", as the engine alone played season " + seed + ".");
                yield return PlayOnToTheNextStagedCeremony("Week " + week + "'s nomination");
                Assert.That(stagedKind, Is.EqualTo(CeremonySting.NominationKind), "Week " + week + " stages its nomination first.");
                Assert.That(director.Snapshot.week, Is.EqualTo(week));
                yield return PlayOnToTheNextStagedCeremony("Week " + week + "'s eviction");
                Assert.That(stagedKind, Is.EqualTo(CeremonySting.EvictionKind), "and then its eviction.");
                Assert.That(director.Snapshot.week, Is.EqualTo(week));
            }
        }
    }
}
