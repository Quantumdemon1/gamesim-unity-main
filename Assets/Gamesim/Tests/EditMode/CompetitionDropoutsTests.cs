using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Competition drop-outs and the tower show. In the ranked endurance game the other competitors let go
    /// around the player from a preview of the very command the attempt commits, run on a copy of the
    /// season (<see cref="CompetitionHoldDropouts"/>): the preview is the commit, seed after seed; whoever
    /// is shown letting go before the player is ranked under them; nobody still holding is ranked under
    /// anybody who let go; and the season's state and generator are exactly what they were. A ring tower
    /// not played is staged as a show whose finishes are the committed standings and whose rings work the
    /// tower to its end on the finish of the one at the stacks (<see cref="CompetitionTowerShowRings"/>).
    /// Unity-free: the subset runs it too.
    /// </summary>
    public sealed class CompetitionDropoutsTests
    {
        private const string CommandId = "competition-dropouts-test";

        /// <summary>Seasons at an endurance competition the player is in: weekly Heads of Household under rules 3, 4 and 5, and vetoes.</summary>
        private static IEnumerable<EpisodeState> EnduranceSeasons(int count)
        {
            int found = 0;
            for (uint seed = 1; seed < 4000 && found < count; seed++)
            {
                int rules = 3 + (int)(seed % 3);
                if (seed % 4 == 0)
                {
                    var veto = ContentCatalog.Create(seed);
                    veto.competitionRulesVersion = rules; veto.phase = EpisodePhase.Veto;
                    var npcs = veto.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
                    veto.hohId = npcs[0];
                    // The player on the block in every other veto, so the nominee's clutch is in some scores.
                    veto.nominees = seed % 8 == 0 ? new List<string> { veto.playerId, npcs[1] } : new List<string> { npcs[1], npcs[2] };
                    var players = new List<string> { npcs[0] }.Concat(veto.nominees).Concat(new[] { veto.playerId }).Concat(npcs.Skip(3)).Distinct();
                    veto.vetoPlayers = players.Take(EpisodeEngine.VetoPlayerCount(veto.Active.Count())).ToList();
                    if (EpisodeEngine.CompetitionCategory(veto) != "Endurance" || !veto.vetoPlayers.Contains(veto.playerId)) continue;
                    if (!EpisodeValidation.TryValidate(veto, out _)) continue;
                    found++; yield return veto;
                    continue;
                }
                var state = ContentCatalog.Create(seed);
                state.competitionRulesVersion = rules; state.phase = EpisodePhase.HoH;
                if (EpisodeEngine.CompetitionCategory(state) != "Endurance") continue;
                found++; yield return state;
            }
        }

        private static EpisodeState Commit(EpisodeState state, double performance)
        {
            var result = new EpisodeEngine(state).Apply(new EpisodeCommand
            {
                id = CommandId, actorId = state.playerId, expectedPhase = state.phase, expectedRevision = state.revision,
                kind = EpisodeCommandKind.Compete, performance = performance,
            });
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }

        private static List<string> Standings(EpisodeState committed)
            => committed.competitionScores.OrderByDescending(entry => entry.score).Select(entry => entry.contestantId).ToList();

        private static string Json(object value) => JsonConvert.SerializeObject(value);

        [Test]
        public void ThePreviewIsTheCommitSeedAfterSeedAndNeverTouchesTheSeason()
        {
            int seasons = 0;
            foreach (var origin in EnduranceSeasons(60))
            {
                string before = Json(origin);
                var dropouts = CompetitionHoldDropouts.Preview(origin, CommandId);
                Assert.That(dropouts, Is.Not.Null, "seed " + origin.seed + ": a competition the player is in has a preview.");
                foreach (double performance in new[] { 0d, .23d, .5d, .871d, 1d })
                {
                    var committed = Commit(origin, performance);
                    Assert.That(dropouts.StandingsAt(performance), Is.EqualTo(Standings(committed)),
                        "seed " + origin.seed + " at " + performance + ": the preview's standings are the commit's.");
                }
                Assert.That(Json(origin), Is.EqualTo(before), "seed " + origin.seed + ": the preview writes nothing to the season.");
                seasons++;
            }
            Assert.That(seasons, Is.EqualTo(60));
        }

        [Test]
        public void ASeasonCommitsAndDrawsExactlyAsItDidWhetherOrNotTheFieldIsShownLettingGo()
        {
            foreach (var origin in EnduranceSeasons(30))
            {
                var plain = Commit(origin.Clone(), .6);
                var shown = origin.Clone();
                var dropouts = CompetitionHoldDropouts.Preview(shown, CommandId);
                for (int step = 0; step <= 200; step++) dropouts.Due(System.Math.Min(.6, step / 250d), step * .1);
                dropouts.LetGo(.6);
                var after = Commit(shown, .6);
                Assert.That(after.randomState, Is.EqualTo(plain.randomState), "seed " + origin.seed + ": the generator stands where it did.");
                Assert.That(Json(after.competitionScores), Is.EqualTo(Json(plain.competitionScores)), "seed " + origin.seed + ": the standings are byte-equal.");
                Assert.That(Json(after), Is.EqualTo(Json(plain)), "seed " + origin.seed + ": the whole committed season is byte-equal.");
            }
        }

        /// <summary>
        /// A hold played out: effort banked at a steady rate up to <paramref name="final"/>, ten readings a
        /// second, then the let-go; then the commit with exactly that performance.
        /// </summary>
        private static (List<string> before, List<string> with, List<string> standings) Hold(EpisodeState origin, double final, double rate)
        {
            var dropouts = CompetitionHoldDropouts.Preview(origin, CommandId);
            var before = new List<string>();
            double seconds = 0, performance = 0;
            while (performance < final)
            {
                seconds += .1; performance = System.Math.Min(final, performance + rate * .1);
                before.AddRange(dropouts.Due(performance, seconds));
            }
            var with = dropouts.LetGo(final).ToList();
            return (before, with, Standings(Commit(origin, final)));
        }

        [Test]
        public void WhoeverIsShownLettingGoBeforeThePlayerIsRankedUnderThemAndNobodyStillHoldingIsUnderAnyoneWhoLetGo()
        {
            int midHold = 0, holdingOnPast = 0;
            uint draw = 11;
            foreach (var origin in EnduranceSeasons(80))
            {
                draw = draw * 1103515245u + 12345u;
                double final = (draw >> 8) % 1001 / 1000d;
                double rate = .03 + (draw >> 20) % 10 / 100d;
                var (before, with, standings) = Hold(origin, final, rate);
                int you = standings.IndexOf(origin.playerId);
                var under = standings.Skip(you + 1).ToList();
                string where = "seed " + origin.seed + " rules " + origin.competitionRulesVersion + " " + origin.phase + " final " + final;
                Assert.That(before.All(under.Contains), Is.True, where + ": everyone shown letting go before the player is ranked under them.");
                Assert.That(with.All(under.Contains), Is.True, where + ": everyone letting go with the player is ranked under them.");
                Assert.That(before.Concat(with).ToList(), Is.EqualTo(Enumerable.Reverse(under).ToList()),
                    where + ": they let go from the bottom of the standings up, so nobody still holding is ranked under anyone who let go.");
                Assert.That(standings.Take(you).Intersect(before.Concat(with)), Is.Empty, where + ": nobody ranked over the player lets go.");
                midHold += before.Count;
                holdingOnPast += you;
            }
            Assert.That(midHold, Is.GreaterThan(0), "Housemates let go while the player still holds on.");
            Assert.That(holdingOnPast, Is.GreaterThan(0), "Somebody outlasts the player in some seasons, and is never shown letting go before them.");
        }

        [Test]
        public void TheFieldLetsGoOneAtATimeAndNotInTheFirstSeconds()
        {
            int drops = 0;
            foreach (var origin in EnduranceSeasons(10))
            {
                var dropouts = CompetitionHoldDropouts.Preview(origin, CommandId);
                Assert.That(dropouts.Live, Is.True, "seed " + origin.seed + ": a weekly hold shows its field letting go.");
                Assert.That(dropouts.Due(1, CompetitionHoldDropouts.FirstDropSeconds - .01), Is.Empty, "Nobody lets go in the first seconds.");
                var times = new List<double>();
                for (double t = CompetitionHoldDropouts.FirstDropSeconds; t < 60; t += .05)
                {
                    var now = dropouts.Due(1, t);
                    Assert.That(now.Count, Is.LessThanOrEqualTo(1), "One at a time.");
                    if (now.Count == 1) times.Add(t);
                }
                for (int i = 1; i < times.Count; i++)
                    Assert.That(times[i] - times[i - 1], Is.GreaterThanOrEqualTo(CompetitionHoldDropouts.DropGapSeconds - 1e-9), "Each drop-out reads on its own.");
                Assert.That(dropouts.StillHolding, Is.EqualTo(dropouts.Ascending.Count - times.Count));
                drops += times.Count;
            }
            Assert.That(drops, Is.GreaterThan(0));
        }

        [Test]
        public void TheFinalPartOneShowsOnlyTheLetGoWhichCannotContradictTheResult()
        {
            var state = ContentCatalog.Create(337);
            state.week = 4; state.phase = EpisodePhase.FinalHoHPart1; state.competitionRulesVersion = 4;
            foreach (var actor in state.contestants.Skip(3)) actor.status = ContestantStatus.Jury;
            state.hohId = null; state.competitionResolved = false;
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            var dropouts = CompetitionHoldDropouts.Preview(state, CommandId);
            Assert.That(dropouts, Is.Not.Null);
            Assert.That(dropouts.Live, Is.False, "Part 1's drop times move with the player's effort: nobody lets go mid-hold.");
            for (int step = 0; step < 300; step++) Assert.That(dropouts.Due(step / 300d, step * .1), Is.Empty);
            var with = dropouts.LetGo(.7);
            var standings = Standings(Commit(state, .7));
            var under = standings.Skip(standings.IndexOf(state.playerId) + 1).Reverse().ToList();
            Assert.That(with, Is.EqualTo(under), "At the let-go, exactly those the commit ranks under the player let go, from the bottom.");
            Assert.That(dropouts.LetGo(.7), Is.Empty, "Once only.");
        }

        [Test]
        public void NoPreviewForACompetitionThePlayerIsNotInOrOneAlreadyDecided()
        {
            var origin = EnduranceSeasons(1).First();
            var decided = Commit(origin, .5);
            Assert.That(CompetitionHoldDropouts.Preview(decided, CommandId), Is.Null, "A decided competition has nothing to preview.");
            Assert.That(CompetitionHoldDropouts.Preview(null, CommandId), Is.Null);
            var outside = origin.Clone(); outside.phase = EpisodePhase.Nomination;
            Assert.That(CompetitionHoldDropouts.Preview(outside, CommandId), Is.Null, "No competition, no preview.");
        }

        // ------------------------------------------------------------ the tower show

        [Test]
        public void AnUnplayedRingTowerIsStagedAsATowerShowInTheCommittedOrder()
        {
            Assert.That(CompetitionShowFamilies.For(CompetitionDefinitions.RingTower.Id, "Mental"), Is.EqualTo(CompetitionShowFamily.Tower));
            uint seed = 1;
            while (seed < 2000 && CompetitionDefinitions.Version5(seed, 1, EpisodePhase.HoH) != CompetitionDefinitions.RingTower) seed++;
            var state = ContentCatalog.Create(seed);
            state.competitionRulesVersion = CompetitionRules.RingTower; state.phase = EpisodePhase.HoH;
            Assert.That(CompetitionDefinitions.For(state), Is.SameAs(CompetitionDefinitions.RingTower));
            foreach (var way in new[] { EpisodeCommandKind.SimulateCompetition, EpisodeCommandKind.ThrowCompetition })
            {
                var result = new EpisodeEngine(state).Apply(new EpisodeCommand
                {
                    id = "tower-show-" + way, actorId = state.playerId, expectedPhase = state.phase, expectedRevision = state.revision, kind = way,
                });
                Assert.That(result.accepted, Is.True, result.reason);
                var plan = CompetitionShowPlan.For(result.state, EpisodePhase.HoH, EpisodeEngine.CompetitionCategory(state), CompetitionDefinitions.RingTower.Id);
                Assert.That(plan, Is.Not.Null, way + ": the tower is staged.");
                Assert.That(plan.Family, Is.EqualTo(CompetitionShowFamily.Tower));
                Assert.That(plan.Standings, Is.EqualTo(Standings(result.state)));
                var timeline = plan.Timeline(false);
                Assert.That(timeline.Order, Is.EqualTo(Standings(result.state)), way + ": they finish in the committed order, the winner first, time called on the last.");
                var finishes = timeline.Beats.Where(beat => beat.Kind == CompetitionShowBeatKind.Finish).Select(beat => beat.Ids[0]).ToList();
                Assert.That(finishes, Is.EqualTo(Standings(result.state).Take(finishes.Count).ToList()));
                Assert.That(timeline.Beats.First(beat => beat.Kind == CompetitionShowBeatKind.Finish).At,
                    Is.EqualTo(CompetitionShowTimeline.TowerWorkSeconds(false)), "The field works the stacks long enough for the tower's moves to be seen.");
            }
        }

        [Test]
        public void TheTowerIsSolvedOnTheFinishOfTheOneAtTheStacksAndTwoThirdsDoneWhenTimeIsCalledOnThem()
        {
            var standings = new[] { "a", "b", "c", "d" };
            foreach (bool quick in new[] { false, true })
            {
                var timeline = CompetitionShowTimeline.For(CompetitionShowFamily.Tower, standings, quick);
                foreach (string subject in standings)
                {
                    var rings = CompetitionTowerShowRings.For(timeline, subject);
                    Assert.That(rings, Is.Not.Null, subject);
                    bool finishes = subject != "d";
                    Assert.That(rings.Finishes, Is.EqualTo(finishes));
                    float done = timeline.Beats.First(beat => (beat.Kind == CompetitionShowBeatKind.Finish || beat.Kind == CompetitionShowBeatKind.TimeUp)
                        && beat.Ids[0] == subject).At;
                    Assert.That(rings.DoneAt, Is.EqualTo(done));
                    int last = -1;
                    for (float t = 0; t < done; t += .02f)
                    {
                        int moves = rings.MovesAt(t);
                        Assert.That(moves, Is.GreaterThanOrEqualTo(last), "The tower only moves forward.");
                        Assert.That(rings.PuzzleAt(t).Solved, Is.False, subject + ": not solved before their finish.");
                        last = moves;
                    }
                    var end = rings.PuzzleAt(done + .01f);
                    Assert.That(end.Solved, Is.EqualTo(finishes), subject + (finishes ? ": solved on their finish." : ": time is called on an unsolved tower."));
                    Assert.That(end.Moves, Is.EqualTo(finishes ? 15 : 10));
                    Assert.That(rings.PuzzleAt(0f).Moves, Is.Zero, "The rings start on the first stack.");
                }
            }
            Assert.That(CompetitionTowerShowRings.For(null, "a"), Is.Null);
            Assert.That(CompetitionTowerShowRings.For(CompetitionShowTimeline.For(CompetitionShowFamily.Tower, standings, false), "nobody"), Is.Null);
        }
    }
}
