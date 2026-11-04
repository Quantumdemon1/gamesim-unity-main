using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// B9's tuning rules (Assets/Plans/B9-BALANCE-PLAN.md): each lever at tuning 0, where it plays exactly as every season
    /// did before B9, and at tuning 1; the validation's range; new seasons' enrolment; and the lab's pins that a tuning-0
    /// season plays the rows it played before B9 and a tuning-1 season the rows B9's measurement played.
    /// </summary>
    public sealed class TuningRulesTests
    {
        // ---------------------------------------------------------------- the rules class

        [Test]
        public void TheRulesDefineNoughtAndOneAndNewSeasonsStartOnOne()
        {
            Assert.That((TuningRules.Off, TuningRules.Bands, TuningRules.Latest, TuningRules.Shipped), Is.EqualTo((0, 1, 1, 1)));
            Assert.That(new[] { -1, 0, 1, 2 }.Select(TuningRules.IsDefined), Is.EqualTo(new[] { false, true, true, false }));
            var built = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 1u);
            Assert.That(built.tuningRulesVersion, Is.Zero, "A built season stays at tuning 0: every test-built season plays as before.");
            Assert.That(ContentCatalog.Create(20260910).tuningRulesVersion, Is.Zero, "The default scene's season too.");
            ShippedRules.ApplyFresh(built);
            Assert.That(built.tuningRulesVersion, Is.EqualTo(TuningRules.Shipped), "A new season starts on B9's tuning.");
            Assert.That(ShippedRules.Fields(built).Single(f => f.Key == "tuningRulesVersion").Value, Is.EqualTo(1));
            Assert.That(TuningRules.At(built, TuningRules.Bands) && TuningRules.At(built, TuningRules.Off), Is.True);
            Assert.That(TuningRules.At(null, TuningRules.Off), Is.False);
        }

        [Test]
        public void ThePreparationLimitsAreFiveBeforeB9AndOneOrTwoUnderIt()
        {
            foreach (int size in new[] { 6, 8, 9, 12 })
            {
                var s = Built(size, 3u, TuningRules.Off);
                Assert.That((TuningRules.StudyLimit(s), TuningRules.PreparationBound(s)), Is.EqualTo((5, 5)), "Tuning 0 at " + size);
                s.tuningRulesVersion = TuningRules.Bands;
                bool small = size <= TuningRules.SmallHouse;
                Assert.That(TuningRules.InSmallHouse(s), Is.EqualTo(small));
                Assert.That((TuningRules.StudyLimit(s), TuningRules.PreparationBound(s)), Is.EqualTo((1, small ? 2 : 1)), "Tuning 1 at " + size);
                // The final week: three remain.
                foreach (var gone in s.contestants.Where(c => !c.isPlayer).Skip(2)) gone.status = ContestantStatus.Evicted;
                Assert.That(s.Active.Count(), Is.EqualTo(3));
                Assert.That(TuningRules.StudyLimit(s), Is.EqualTo(small ? 2 : 1), "The final week's study fills to two only in a small house, at " + size);
                s.tuningRulesVersion = TuningRules.Off;
                Assert.That(TuningRules.StudyLimit(s), Is.EqualTo(5));
            }
            var watched = Built(8, 3u, TuningRules.Bands);
            watched.playerStudyBonus = 2;
            Assert.That(TuningRules.PreparationOutOf(watched), Is.EqualTo(2), "The copy counts out of the limit, or the preparation where watching took it past.");
            watched.tuningRulesVersion = TuningRules.Off;
            Assert.That(TuningRules.PreparationOutOf(watched), Is.EqualTo(5));
        }

        // ---------------------------------------------------------------- the study

        [Test]
        public void AStudyFillsPreparationToFiveBeforeB9AndToOneUnderIt()
        {
            var before = new EpisodeEngine(Built(8, 11u, TuningRules.Off));
            for (int i = 0; i < 6; i++) Accept(before, Command(before.Snapshot, EpisodeCommandKind.StudyHouse, "memorize-layout"));
            Assert.That(before.Snapshot.playerStudyBonus, Is.EqualTo(5), "Tuning 0: five memorizes reach five, and a sixth is spent for nothing.");
            Assert.That(before.Snapshot.events.Last().text, Does.EndWith("Preparation: 5/5. One social action spent."));

            var tuned = new EpisodeEngine(Built(8, 11u, TuningRules.Bands));
            Accept(tuned, Command(tuned.Snapshot, EpisodeCommandKind.StudyHouse, "memorize-layout"));
            Assert.That(tuned.Snapshot.playerStudyBonus, Is.EqualTo(1));
            Assert.That(tuned.Snapshot.events.Last().text, Does.EndWith("Preparation: 1/1. One social action spent."));
            var full = tuned.Snapshot;
            foreach (string approach in new[] { "memorize-layout", "sneak-peek" })
            {
                var refused = tuned.Apply(Command(full, EpisodeCommandKind.StudyHouse, approach));
                Assert.That(refused.accepted, Is.False, "A study at the limit is refused under tuning 1 (decision 6).");
                Assert.That(refused.reason, Is.EqualTo("Your preparation is full. It is used in the next Head of Household or veto competition you enter."));
            }
            Assert.That(Json(tuned.Snapshot), Is.EqualTo(Json(full)), "Refused: nothing spent, nothing drawn.");

            // A sneak-peek's +2 is held to the limit, on the one roll the study always draws.
            var peek = Built(8, 11u, TuningRules.Bands);
            var rng = new SeededRandom(peek.randomState);
            var plan = WebStudyHouse.PlanStudy(0, "sneak-peek", null, rng.NextDouble(), 1);
            var peeked = new EpisodeEngine(peek);
            Accept(peeked, Command(peek, EpisodeCommandKind.StudyHouse, "sneak-peek"));
            Assert.That(peeked.Snapshot.randomState, Is.EqualTo(rng.State));
            Assert.That(peeked.Snapshot.playerStudyBonus, Is.EqualTo(plan.studyBonus));
            Assert.That(peeked.Snapshot.playerStudyBonus, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void TheCapOverloadAtFiveIsTheSourcesStudy()
        {
            foreach (string approach in new[] { "memorize-layout", "sneak-peek" })
                for (int bonus = 0; bonus <= 5; bonus++)
                    foreach (double roll in new[] { 0.0, 0.44, 0.45, 0.46, 0.99 })
                    {
                        var source = WebStudyHouse.PlanStudy(bonus, approach, null, roll);
                        var capped = WebStudyHouse.PlanStudy(bonus, approach, null, roll, WebStudyHouse.SourceCap);
                        Assert.That((capped.chance, capped.success, capped.bonusDelta, capped.studyBonus),
                            Is.EqualTo((source.chance, source.success, source.bonusDelta, source.studyBonus)));
                    }
            Assert.That(WebStudyHouse.ApplyBonus(1, 2, 1), Is.EqualTo(1));
            Assert.That(WebStudyHouse.ApplyBonus(0, -1, 1), Is.Zero);
            Assert.That(WebStudyHouse.ApplyBonus(4, 2), Is.EqualTo(5));
        }

        // ---------------------------------------------------------------- the spend and the watch

        [Test]
        public void AWeeklyCompetitionThePlayerEntersSpendsPreparationUnderTuningOneOnly()
        {
            foreach (int tuning in new[] { TuningRules.Off, TuningRules.Bands })
            {
                var engine = new EpisodeEngine(Built(8, 11u, tuning));
                Accept(engine, Command(engine.Snapshot, EpisodeCommandKind.StudyHouse, "memorize-layout"));
                // Week one's free time, then the Head of Household the whole house plays.
                WalkWhile(engine, s => s.phase == EpisodePhase.Social);
                Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.HoH));
                WalkWhile(engine, s => s.phase == EpisodePhase.HoH);
                var s1 = engine.Snapshot;
                var row = s1.ledger.competitions.Last(r => r.week == 1 && r.kind == "HoH");
                Assert.That(row.entry, Is.EqualTo(CompetitionEntry.Played), "Precondition: the player played it.");
                if (tuning == TuningRules.Off)
                {
                    Assert.That(s1.playerStudyBonus, Is.EqualTo(1), "Tuning 0: preparation is never spent.");
                    Assert.That(s1.events.Any(e => e.kind == "study-house" && e.text.StartsWith("Your preparation was used", StringComparison.Ordinal)), Is.False);
                }
                else
                {
                    Assert.That(s1.playerStudyBonus, Is.Zero, "Tuning 1: the Advance out of the Head of Household spent it.");
                    var spent = s1.events.Last(e => e.kind == "study-house");
                    Assert.That(spent.text, Is.EqualTo("Your preparation was used in the competition. Preparation: 0/1."));
                    Assert.That(spent.audienceIds, Is.EqualTo(new[] { s1.playerId }));
                }
            }
        }

        [Test]
        public void AStudiedPlayerWhoSitsAHeadOfHouseholdOutLearnsFromWatchingItInASmallHouseOnly()
        {
            foreach (int size in new[] { 8, 12 })
            {
                uint seed = OutgoingHohSeed(size);
                foreach (int tuning in new[] { TuningRules.Off, TuningRules.Bands })
                    foreach (bool studies in new[] { false, true })
                    {
                        var engine = new EpisodeEngine(Built(size, seed, tuning));
                        // The free time after the player's week as Head of Household: the next one is played without them.
                        WalkWhile(engine, OutgoingHoh);
                        Assert.That(engine.Snapshot.hohId, Is.EqualTo(engine.Snapshot.playerId), "Precondition: the player is the outgoing Head of Household.");
                        Assert.That(engine.Snapshot.playerStudyBonus, Is.Zero, "Nothing spent that was not studied.");
                        if (studies) Accept(engine, Command(engine.Snapshot, EpisodeCommandKind.StudyHouse, "memorize-layout"));
                        WalkWhile(engine, s => s.phase != EpisodePhase.HoH);
                        WalkWhile(engine, s => s.phase == EpisodePhase.HoH);
                        var after = engine.Snapshot;
                        var row = after.ledger.competitions.Last(r => r.kind == "HoH");
                        Assert.That(row.week, Is.EqualTo(after.week));
                        Assert.That((row.entry, row.placement), Is.EqualTo((CompetitionEntry.Watched, 0)), "Precondition: they sat it out.");
                        int expected = !studies ? 0 : tuning == TuningRules.Bands && size <= TuningRules.SmallHouse ? 2 : 1;
                        Assert.That(after.playerStudyBonus, Is.EqualTo(expected), "size " + size + ", tuning " + tuning + ", studied " + studies);
                        bool logged = after.events.Any(e => e.kind == "study-house" && e.text.StartsWith("You watched the Head of Household", StringComparison.Ordinal));
                        Assert.That(logged, Is.EqualTo(expected == 2));
                        if (logged) Assert.That(after.events.Last(e => e.kind == "study-house").text, Does.EndWith("Preparation: 2/2."));
                        Assert.That(EpisodeValidation.TryValidate(after, out string error), Is.True, error);
                    }
            }
        }

        // ---------------------------------------------------------------- validation

        [Test]
        public void ValidationHoldsPreparationToTheTuningsBoundAndAdmitsNoTuningPastOne()
        {
            var small = Built(8, 5u, TuningRules.Bands);
            small.playerStudyBonus = 2;
            Valid(small);
            small.playerStudyBonus = 3;
            Refused(small, "Study preparation must be between zero and two.");
            var big = Built(12, 5u, TuningRules.Bands);
            big.playerStudyBonus = 1;
            Valid(big);
            big.playerStudyBonus = 2;
            Refused(big, "Study preparation must be between zero and one.");
            var old = Built(12, 5u, TuningRules.Off);
            old.playerStudyBonus = 5;
            Valid(old);
            old.playerStudyBonus = 6;
            Refused(old, "Study preparation must be between zero and five.");

            var past = Built(8, 5u, TuningRules.Bands);
            past.tuningRulesVersion = TuningRules.Latest + 1;
            Refused(past, "Schema 30's pooled rules are not enabled in this build.");
            past.tuningRulesVersion = -1;
            Refused(past, "Schema 30's pooled rules are not enabled in this build.");
            var earlier = Built(8, 5u, TuningRules.Bands);
            earlier.competitionRulesVersion = 3;
            Refused(earlier, "The tuning rules need competition rules 4.");
            earlier.tuningRulesVersion = TuningRules.Off;
            Valid(earlier);
        }

        // ---------------------------------------------------------------- Game Sense

        [Test]
        public void GameSenseWeighsItsFacesByTheSeasonsTuning()
        {
            var engine = new EpisodeEngine(Built(8, 11u, TuningRules.Off));
            WalkWhile(engine, s => s.week < 3);
            var s0 = engine.Snapshot;
            var s1 = s0.Clone();
            s1.tuningRulesVersion = TuningRules.Bands;
            var r0 = GameSense.Evaluate(s0);
            var r1 = GameSense.Evaluate(s1);
            Assert.That((r1.competitions, r1.strategy, r1.social), Is.EqualTo((r0.competitions, r0.strategy, r0.social)), "The faces read the same notes.");
            Assert.That(r0.score, Is.EqualTo((int)Math.Round(r0.competitions * 0.3 + r0.strategy * 0.4 + r0.social * 0.3)));
            Assert.That(r1.score, Is.EqualTo((int)Math.Round(r1.competitions * 0.2 + r1.strategy * 0.6 + r1.social * 0.2)));
            Assert.That(TuningRules.SenseWeights(s0), Is.EqualTo((GameSense.CompetitionWeight, GameSense.StrategyWeight, GameSense.SocialWeight)));
            Assert.That(TuningRules.SenseWeights(s1), Is.EqualTo((0.2, 0.6, 0.2)));
        }

        // ---------------------------------------------------------------- the counter

        [Test]
        public void UnderTuningOneANomineesSayCountsOnNeitherSideOfACounter()
        {
            // The player's pact of four with the first nominee in it: the two voters say the first nominee, the nominee says
            // the other. Today that is two for the counter (the player's and the nominee's) against two: a tie, and the
            // player founded the pact, so the counter carries. Under tuning 1 the nominee's say is not counted: one to two.
            var s = PactPlanTests.WarRoom(out var pact, extra: 1);
            var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            foreach (var id in new[] { npcs[3], npcs[4], npcs[1] }) s.Find(id).traits = new List<string> { "Sneaky" };
            string plan = s.nominees[0], counter = s.nominees[1];
            Assert.That(npcs[1], Is.EqualTo(plan), "Precondition: the nominee in the pact is the plan's target.");
            var row = PactPlanTests.Row(s, (npcs[3], plan), (npcs[4], plan), (npcs[1], counter));
            Assert.That(PactPlans.MembersPlan(row.says), Is.EqualTo(plan));
            var today = PactPlans.Settle(s, pact, row, PactPlans.Counter, counter);
            Assert.That((today.stance, today.targetId), Is.EqualTo((PactPlanStance.Countered, counter)), "Tuning 0: a tie, the player's side.");
            var tuned = s.Clone();
            tuned.competitionRulesVersion = CompetitionRules.Widened;
            tuned.tuningRulesVersion = TuningRules.Bands;
            var settled = PactPlans.Settle(tuned, tuned.alliances.Single(a => a.id == PactPlanTests.PactId), row, PactPlans.Counter, counter);
            Assert.That(settled.cameRound, Is.Empty);
            Assert.That((settled.stance, settled.targetId, settled.counterId), Is.EqualTo((PactPlanStance.Countered, plan, counter)), "Tuning 1: one to two, the plan holds.");
        }

        [Test]
        public void UnderTuningOneATieCarriesThePlayersCounterOnlyWhereAMemberCameRound()
        {
            // The player's pact of the two voters off the block: one says the plan and cannot come round, the other says
            // nothing - the player's one against the plan's one, a tie.
            var s = PactPlanTests.WarRoom(out var pact, extra: 0);
            var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            foreach (var id in new[] { npcs[3], npcs[4] }) s.Find(id).traits = new List<string> { "Sneaky" };
            string plan = s.nominees[0], counter = s.nominees[1];
            var row = PactPlanTests.Row(s, (npcs[3], plan));
            Assert.That(row.present, Does.Contain(npcs[4]), "Precondition: the quiet member was at the meeting.");
            Assert.That(EpisodeEngine.PlayerFounded(s, pact), Is.True);
            var today = PactPlans.Settle(s, pact, row, PactPlans.Counter, counter);
            Assert.That(today.cameRound, Is.Empty);
            Assert.That(today.targetId, Is.EqualTo(counter), "Tuning 0: one against one, and a tie goes to the player who founded it.");
            var tuned = s.Clone();
            tuned.competitionRulesVersion = CompetitionRules.Widened;
            tuned.tuningRulesVersion = TuningRules.Bands;
            var settled = PactPlans.Settle(tuned, tuned.alliances.Single(a => a.id == PactPlanTests.PactId), row, PactPlans.Counter, counter);
            Assert.That(settled.cameRound, Is.Empty);
            Assert.That((settled.stance, settled.targetId), Is.EqualTo((PactPlanStance.Countered, plan)), "Tuning 1: nobody came round, so the plan stands.");
        }

        // ---------------------------------------------------------------- the lab's pins

#if !UNITY_5_3_OR_NEWER
        // The balance lab is Tools/SimulationTests only (BalanceLab.cs): so are its pins.

        /// <summary>
        /// The studier at fixed 0.5 at eight and twelve, two seasons each, held at tuning 0: the rows B9's reference played
        /// before any tuning existed (the competition tier at 26b65f09, D:/ClaudeScratch/b9/analyst/results/competition.jsonl,
        /// whose merged rows hash 92c74b608cc446a0; these four lines of it hash to this).
        /// </summary>
        internal const string StudierTuning0Rows = "9b9bf8a15644b745";

        /// <summary>The same four seasons at tuning 1: the rows B9's measurement played (round 3's S8F) - re-pinned only with B9's numbers.</summary>
        internal const string StudierTuning1Rows = "0f53e79cd8e9a2f2";

        [Test]
        public void ATuningNoughtSeasonPlaysTheRowsItPlayedBeforeB9AndATuningOneSeasonB9s()
        {
            var cells = BalanceLab.Grid(new[] { BalancePolicies.Studier }, new[] { 8, 12 }, PerformanceModel.Fixed(0.5));
            var held = cells.Select(c => new BalanceLab.Cell { policy = c.policy, size = c.size, performance = c.performance, tuningHeld = true }).ToList();
            var before = BalanceLab.Run(held, 2);
            var tuned = BalanceLab.Run(cells, 2);
            Assert.That(before.Concat(tuned).Where(r => r.error != null).Select(r => r.error), Is.Empty);
            string beforeRows = BalanceLab.Jsonl(before), tunedRows = BalanceLab.Jsonl(tuned);
            TestContext.WriteLine("tuning 0 " + BalanceLabTests.Hash(beforeRows) + "; tuning 1 " + BalanceLabTests.Hash(tunedRows));
            Assert.That(held.Select(c => c.House), Is.EqualTo(new[] { "Regular/8/npc0/tuning0", "Regular/12/npc0/tuning0" }));
            Assert.That(held.Select(c => BalanceLab.Seed(c, 0)), Is.EqualTo(cells.Select(c => BalanceLab.Seed(c, 0))), "A held cell plays its shipped cell's seasons.");
            Assert.That(BalanceLabTests.Hash(beforeRows), Is.EqualTo(StudierTuning0Rows), "Tuning 0 plays the seasons it played before B9.");
            Assert.That(BalanceLabTests.Hash(tunedRows), Is.EqualTo(StudierTuning1Rows), "Tuning 1 plays the seasons B9's measurement played.");
        }

#endif

        // ---------------------------------------------------------------- helpers

        /// <summary>A built season of the size, at the tuning: competition rules 4 (SeasonBuilder's), no other rule switched on.</summary>
        private static EpisodeState Built(int size, uint seed, int tuning)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);
            Assert.That(s.competitionRulesVersion, Is.EqualTo(CompetitionRules.Widened));
            s.tuningRulesVersion = tuning;
            Valid(s);
            return s;
        }

        /// <summary>Still walking: not yet at a free time while the player is Head of Household, with a weekly one to come.</summary>
        private static bool OutgoingHoh(EpisodeState s) =>
            !(s.phase == EpisodePhase.Social && s.hohId == s.playerId && s.Active.Count() > 4 && s.Find(s.playerId).status == ContestantStatus.Active);

        /// <summary>The first seed whose built season, walked, reaches a free time with the player its Head of Household.</summary>
        private static uint OutgoingHohSeed(int size)
        {
            for (uint seed = 1; seed < 400; seed++)
            {
                var engine = new EpisodeEngine(Built(size, seed, TuningRules.Off));
                WalkWhile(engine, OutgoingHoh);
                if (!OutgoingHoh(engine.Snapshot)) return seed;
            }
            Assert.Fail("No seed under 400 makes the player Head of Household before a free time at " + size + ".");
            return 0;
        }

        private static void WalkWhile(EpisodeEngine engine, Func<EpisodeState, bool> going)
        {
            for (int guard = 0; guard < 400 && going(engine.Snapshot) && engine.Snapshot.phase != EpisodePhase.Finished; guard++)
                Accept(engine, EpisodeEngineTests.NextCommand(engine.Snapshot));
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null) => new EpisodeCommand
        {
            id = "tuning-test-" + s.revision + "-" + kind + "-" + target, expectedRevision = s.revision, expectedPhase = s.phase, actorId = s.playerId,
            kind = kind, targetId = target,
        };

        private static void Accept(EpisodeEngine engine, EpisodeCommand command)
        {
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
        }

        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);

        private static void Refused(EpisodeState s, string message)
        {
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.False);
            Assert.That(error, Is.EqualTo(message));
        }

        private static string Json(EpisodeState s) => Newtonsoft.Json.JsonConvert.SerializeObject(s);
    }
}
