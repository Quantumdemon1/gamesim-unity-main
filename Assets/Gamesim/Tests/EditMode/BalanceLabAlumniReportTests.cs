#if !UNITY_5_3_OR_NEWER
// ALL-STAR-HISTORIES-PLAN 14.H's report and 14.L.H's pact watch: Tools/SimulationTests only, as the balance lab they test
// is compiled out of Unity's assemblies.
using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The lab's alumni arms and 14.L.H's pact watch (ALL-STAR-HISTORIES-PLAN 14.H, slice S4): a held arm plays its whole
    /// season at its value, an unheld cell keeps the name and seeds it always had and plays the same season with the watch as
    /// without it, the report's 48 cells pair by seed and enrol at their arms, H1b's gate still holds its arm at 1, the stop
    /// conditions read as the plan writes them, and the watch counts a birth, a join, a shed and every must-be-zero row it is
    /// shown - by fixtures in CounterPactGrowthTests' style, never by a walk.
    /// </summary>
    public sealed class BalanceLabAlumniReportTests
    {
        private const string Dan = "dan-gheesling", Will = "dr-will-kirby", Derrick = "derrick-levasseur", Janelle = "janelle-pierzina",
            Cody = "cody-calafiore", Vanessa = "vanessa-rousso", Danielle = "danielle-reyes";

        private static readonly EpisodeCommand Advance = new EpisodeCommand { kind = EpisodeCommandKind.Advance };

        private static BalanceLab.Cell AllStars8(int held = 0, bool off = false) =>
            new BalanceLab.Cell { policy = BalancePolicies.Passive, size = 8, roster = CastTemplates.Roster.AllStars, alumniHeld = held, alumniOff = off };

        // ------------------------------------------------------------------ the cells (14.H)

        [Test]
        public void AHeldArmPlaysItsWholeSeasonAtItsValueAndOnlyAnEnrolledSeasonIsHeld()
        {
            var held = AllStars8(held: AlumniRules.CounterBlocs);
            uint seed = BalanceLab.Seed(held, 0);
            Assert.That(BalanceLab.Fresh(held, seed).alumniRulesVersion, Is.EqualTo(AlumniRules.CounterBlocs), "Held at 4 after ApplyFresh.");
            Assert.That(BalanceLab.Fresh(AllStars8(), seed).alumniRulesVersion, Is.EqualTo(AlumniRules.Latest), "Unheld: the shipped value.");
            Assert.That(BalanceLab.Fresh(AllStars8(off: true), seed).alumniRulesVersion, Is.EqualTo(AlumniRules.Off), "No histories: 0.");
            // A Regular house seats no alumnus and does not enrol: a hold never reaches it.
            var regular = new BalanceLab.Cell { policy = BalancePolicies.Passive, size = 8, alumniHeld = AlumniRules.CounterBlocs };
            Assert.That(BalanceLab.Fresh(regular, BalanceLab.Seed(regular, 0)).alumniRulesVersion, Is.EqualTo(AlumniRules.Off));

            var run = BalanceLab.Play(held, 0);
            Assert.That(run.error, Is.Null);
            Assert.That((run.pactWatch.rulesLow, run.pactWatch.rulesHigh), Is.EqualTo((AlumniRules.CounterBlocs, AlumniRules.CounterBlocs)),
                "Every state from the first command to the finale at 4.");
            Assert.That(run.pactWatch.joins, Is.Zero, "Nothing grows at 4.");
        }

        [Test]
        public void AnUnheldCellKeepsItsNameAndSeedsAndTheWatchNeverChangesItsSeason()
        {
            var shipped = AllStars8();
            Assert.That(shipped.House, Is.EqualTo("AllStars/8/npc0"), "An unheld cell is named as it always was.");
            Assert.That(AllStars8(held: 4).House, Is.EqualTo("AllStars/8/npc0/alumni4"));
            Assert.That(AllStars8(off: true).House, Is.EqualTo("AllStars/8/npc0/alumni0"));
            for (int i = 0; i < 100; i++)
            {
                uint seed = BalanceLab.Seed(shipped, i);
                Assert.That(seed, Is.EqualTo(SeededRandom.HashSeed("balance-lab/v1/AllStars/8/npc0/" + i)), "The seed the lab always drew.");
                Assert.That((BalanceLab.Seed(AllStars8(held: 4), i), BalanceLab.Seed(AllStars8(off: true), i)), Is.EqualTo((seed, seed)), "Every arm plays it.");
            }

            var watched = BalanceLab.Play(shipped, 0);
            var unwatched = BalanceLab.Play(shipped, 0, watch: false);
            Assert.That((watched.error, unwatched.error), Is.EqualTo(((string)null, (string)null)));
            Assert.That(unwatched.pactWatch, Is.Null);
            Assert.That(BalanceLab.Row(watched), Is.EqualTo(BalanceLab.Row(unwatched)), "The watch only reads: the same season with it as without it.");
            Assert.That(JsonConvert.SerializeObject(watched.autopsy), Is.EqualTo(JsonConvert.SerializeObject(unwatched.autopsy)));
            var fresh = BalanceLab.Fresh(shipped, watched.seed);
            var record = watched.pactWatch;
            Assert.That((record.rulesLow, record.rulesHigh), Is.EqualTo((AlumniRules.Latest, AlumniRules.Latest)));
            Assert.That(record.pairs, Is.EqualTo(new[] { "bb16-hitmen:" + AlumniHistoriesTests.On(fresh, Derrick) + "|" + AlumniHistoriesTests.On(fresh, Cody) }),
                "The eight-house seats one known pair, the Hitmen.");
            Assert.That(record.mustBeZero.Where(p => p.Value != 0).Select(p => p.Key + " " + p.Value), Is.Empty);
            Assert.That(record.mustBeZero.Keys, Is.EquivalentTo(BalanceLab.PactWatch.MustBeZero), "Every must-be-zero row is present.");
        }

        [Test]
        public void TheReportsArmsPairBySeedAndEachEnrolsAtItsOwnValue()
        {
            Assert.That(BalanceLabAlumniReport.ReportArms, Is.EqualTo(new[] { 5, 4, 3, 2, 1, 0 }));
            Assert.That(BalanceLabAlumniReport.ReportArms[0], Is.EqualTo(AlumniRules.Latest), "5 is the shipped arm.");
            Assert.That(BalanceLabAlumniReport.Comparisons, Is.EqualTo(new[] { (2, 1), (3, 2), (4, 3), (5, 4), (4, 0), (5, 0) }));
            var cells = BalanceLabAlumniReport.ReportCells();
            Assert.That(cells, Has.Count.EqualTo(48), "Sizes 8 and 12, four players, six arms.");
            Assert.That(cells.Select(c => c.Key).Distinct().Count(), Is.EqualTo(48));
            Assert.That(cells.Select(c => c.size).Distinct(), Is.EqualTo(new[] { 8, 12 }));
            Assert.That(cells.Count(c => c.playerTemplate == BalanceLabAlumniReport.Card && c.policy == BalancePolicies.Passive), Is.EqualTo(12));
            foreach (var g in cells.GroupBy(c => c.policy + "/" + c.size + "/" + c.playerTemplate))
            {
                Assert.That(g.Select(BalanceLabAlumniReport.ArmOf), Is.EqualTo(BalanceLabAlumniReport.ReportArms), g.Key);
                var shipped = g.First();
                Assert.That(shipped.alumniHeld == 0 && !shipped.alumniOff && !shipped.House.Contains("/alumni"), Is.True, g.Key + ": the shipped arm is the cell left unheld.");
                string pairs = null;
                foreach (var cell in g)
                {
                    Assert.That(BalanceLab.Seed(cell, 7), Is.EqualTo(BalanceLab.Seed(shipped, 7)), cell.Key + ": the arms pair by seed.");
                    var fresh = BalanceLab.Fresh(cell, BalanceLab.Seed(cell, 7));
                    int arm = BalanceLabAlumniReport.ArmOf(cell);
                    Assert.That(fresh.alumniRulesVersion, Is.EqualTo(arm), cell.Key);
                    Assert.That(AlumniRules.On(fresh), Is.EqualTo(arm != AlumniRules.Off), cell.Key + ": enrolled at every arm but 0.");
                    // The known pairs are the cast's, read the same way at every arm, 0 included.
                    string these = string.Join(",", new BalanceLab.PactWatch(fresh).record.pairs);
                    Assert.That(these, Is.EqualTo(pairs ?? these), cell.Key);
                    Assert.That(these, Is.Not.Empty, cell.Key);
                    pairs = these;
                }
            }
            // H1b's gate keeps its histories arm held at 1, named "/alumni1", against its controls on the same seeds.
            var gate = BalanceLabAlumniReport.Cells();
            Assert.That(gate.Where(c => !c.alumniOff).Select(c => c.alumniHeld).Distinct(), Is.EqualTo(new[] { AlumniRules.Histories }));
            foreach (var on in gate.Where(c => !c.alumniOff))
            {
                Assert.That(on.House, Does.EndWith("/alumni1"), on.Key);
                Assert.That(BalanceLab.Fresh(on, BalanceLab.Seed(on, 0)).alumniRulesVersion, Is.EqualTo(AlumniRules.Histories), on.Key);
                var controls = gate.Where(c => c.alumniOff && c.policy == on.policy && c.size == on.size && c.playerTemplate == on.playerTemplate).ToList();
                Assert.That(controls, Has.Count.EqualTo(2), on.Key);
                Assert.That(controls.Select(c => BalanceLab.Seed(c, 3)), Is.EqualTo(new[] { BalanceLab.Seed(on, 3), BalanceLab.Seed(on, 3) }), on.Key);
            }
        }

        [Test]
        public void TheStopConditionsAndTheNearZeroRuleReadAsThePlanWritesThem()
        {
            // Stop condition 1: the shipped arm past a budget beyond noise where arm 1 meets it; growth the cause where arm 4 meets it too.
            Assert.That(AlumniReport.StopOne(0.25, true, (20, 100), (22, 100), (60, 100)), Is.EqualTo((true, true)));
            Assert.That(AlumniReport.StopOne(0.25, true, (20, 100), (55, 100), (60, 100)), Is.EqualTo((true, false)), "Arm 4 crosses too: not growth's doing.");
            Assert.That(AlumniReport.StopOne(0.25, true, (30, 100), (22, 100), (60, 100)), Is.EqualTo((false, false)), "Arm 1 does not meet it.");
            Assert.That(AlumniReport.StopOne(0.25, true, (20, 100), (22, 100), (30, 100)), Is.EqualTo((false, false)), "Past the line, within noise.");
            Assert.That(AlumniReport.StopOne(0.80, false, (85, 100), (84, 100), (60, 100)), Is.EqualTo((true, true)), "Pariah-free: at least the line.");
            Assert.That(AlumniReport.StopOne(0.80, false, (85, 100), (84, 100), (75, 100)), Is.EqualTo((false, false)), "Within noise of 80%.");
            Assert.That(AlumniReport.StopOne(0.45, false, (50, 100), (50, 100), (30, 100)), Is.EqualTo((true, true)), "The twelve-house's 45%.");
            Assert.That((BalanceLabAlumniReport.PileOnBudget, BalanceLabAlumniReport.PariahFreeBudget, BalanceLabAlumniReport.PariahFreeBudgetTwelve),
                Is.EqualTo((0.25, 0.80, 0.45)), "NPC-AGENCY-PLAN constraint 1.");
            // Stop condition 2: more than half the seasons.
            Assert.That((AlumniReport.StopTwo(51, 100), AlumniReport.StopTwo(50, 100), AlumniReport.StopTwo(0, 0)), Is.EqualTo((true, false, false)));
            // The near-zero rule: fewer than one season in ten.
            Assert.That((AlumniReport.NearZero(9, 100), AlumniReport.NearZero(10, 100), AlumniReport.NearZero(0, 0)), Is.EqualTo((true, false, false)));

            // The paired difference pairs by seed and gives the gate's interval.
            BalanceLab.SeasonRun Run(int index, uint seed, int commands) => new BalanceLab.SeasonRun { index = index, seed = seed, commands = commands };
            var later = new[] { Run(0, 11u, 3), Run(1, 12u, 5), Run(2, 13u, 7) };
            var earlier = new[] { Run(0, 11u, 1), Run(1, 12u, 2), Run(2, 13u, 3) };
            var d = AlumniReport.Paired(later, earlier, r => r.commands);
            double half = 1.959963984540054 / Math.Sqrt(3);
            Assert.That(d.n, Is.EqualTo(3));
            Assert.That(d.mean, Is.EqualTo(3).Within(1e-12));
            Assert.That(d.low, Is.EqualTo(3 - half).Within(1e-12));
            Assert.That(d.high, Is.EqualTo(3 + half).Within(1e-12));
            Assert.That(AlumniReport.Beyond(d), Is.True);
            Assert.Throws<ArgumentException>(() => AlumniReport.Paired(later, new[] { Run(0, 11u, 1), Run(1, 99u, 2), Run(2, 13u, 3) }, r => r.commands), "Seasons of two seeds never pair.");
        }

        // ------------------------------------------------------------------ the pact watch (14.L.H), by fixtures

        /// <summary>
        /// CounterPactGrowthTests' eight-house: the shipped All-Stars season held at <paramref name="value"/>, every view at
        /// nought and nobody with a trait, and Dan and Janelle warm to each other in week 1 (<paramref name="unpaired"/>); then
        /// paired by the weekly rule (<paramref name="paired"/>, its alliance row recorded); then week 2, Will wanting in
        /// through Janelle (<paramref name="s"/>).
        /// </summary>
        private static void Fixture(uint seed, int value, out EpisodeState unpaired, out EpisodeState paired, out EpisodeState s, out string pactId)
        {
            unpaired = AlumniHistoriesTests.AllStars(8, seed, null, value);
            foreach (var a in unpaired.contestants)
            {
                a.traits = new List<string>();
                foreach (var b in unpaired.contestants.Where(c => c.id != a.id)) AllWeekCadenceTests.Score(unpaired, a.id, b.id, 0);
            }
            string dan = On(unpaired, Dan), janelle = On(unpaired, Janelle), will = On(unpaired, Will);
            AllWeekCadenceTests.Score(unpaired, dan, janelle, 90);
            AllWeekCadenceTests.Score(unpaired, janelle, dan, 90);
            paired = unpaired.Clone();
            Assert.That(NpcAlliances.TryPropose(paired, dan, new HashSet<string>(StringComparer.Ordinal)), Is.True, "The pair forms.");
            pactId = paired.alliances[paired.alliances.Count - 1].id;
            EpisodeEngine.ReconcileAllianceRows(paired);
            s = paired.Clone();
            s.week++;
            AllWeekCadenceTests.Score(s, will, janelle, 60);
            AllWeekCadenceTests.Score(s, janelle, will, 30);
        }

        private static string On(EpisodeState s, string templateId) => AlumniHistoriesTests.On(s, templateId);

        private static AllianceState Pact(EpisodeState s, string id) => s.alliances.Single(a => a.id == id);

        /// <summary>A member written onto a pact as a join writes it, without the ledger: on the end, and a knower of its fact.</summary>
        private static void HandAdd(EpisodeState s, string pactId, string id)
        {
            Pact(s, pactId).members.Add(id);
            Knowledge.AddKnower(s, Knowledge.Of(s, FactKinds.Alliance, pactId), id);
        }

        /// <summary>One command watched on its own: what a fresh watch of this season counts from the two states.</summary>
        private static BalanceLab.PactRecord WatchOne(EpisodeState before, EpisodeState after)
        {
            var watch = new BalanceLab.PactWatch(before);
            watch.Watch(Advance, before, after);
            return watch.record;
        }

        /// <summary>Exactly these must-be-zero rows at these counts, and every other row at nought.</summary>
        private static void Only(BalanceLab.PactRecord record, string because, params (string row, int count)[] expected)
        {
            foreach (string row in expected.Select(e => e.row)) Assert.That(BalanceLab.PactWatch.MustBeZero, Does.Contain(row), "A row of the watch's: " + row);
            var want = BalanceLab.PactWatch.MustBeZero.ToDictionary(row => row, row => expected.Where(e => e.row == row).Sum(e => e.count));
            Assert.That(record.mustBeZero, Is.EquivalentTo(want), because + ": " + string.Join("; ", record.mustBeZero.Where(p => p.Value != 0).Select(p => p.Key + " " + p.Value)));
        }

        [Test]
        public void ThePactWatchCountsABirthAJoinAndAShed()
        {
            Fixture(5401u, AlumniRules.GrowingPacts, out var unpaired, out var paired, out var s, out string pactId);
            string dan = On(s, Dan), janelle = On(s, Janelle), will = On(s, Will);
            var watch = new BalanceLab.PactWatch(unpaired);

            // The birth: a houseguests' pact of two, outsiders, by week 3.
            watch.Watch(Advance, unpaired, paired);
            Assert.That((watch.record.births, watch.record.joins, watch.record.outsiderPactByWeek3), Is.EqualTo((1, 0, true)));
            Assert.That(watch.record.largestOutsiderPact, Is.EqualTo(2));

            // The join, by the rule itself: Will in through Janelle in week 2, the Hitmen live, the bound three.
            var joined = s.Clone();
            Assert.That(NpcAlliances.TryJoin(joined, will, new HashSet<string>(StringComparer.Ordinal), out string sponsor), Is.True);
            Assert.That(sponsor, Is.EqualTo(janelle));
            watch.Watch(Advance, s, joined);
            var r = watch.record;
            Assert.That((r.births, r.joins, r.firstJoinWeek, r.largestOutsiderPact), Is.EqualTo((1, 1, 2, 3)));
            Assert.That(r.blocLiveWeeks, Is.EqualTo(new[] { 1, 2 }), "The Hitmen live in both weeks: the bound above two.");
            Assert.That(r.mustBeZero.Where(p => p.Value != 0).Select(p => p.Key), Is.Empty, "A join the rule made breaks no rule.");

            // The shed: a week on, Dan sours on Will past the sour line while the pact is private to its members.
            var before = joined.Clone();
            before.week++;
            var shed = before.Clone();
            AllWeekCadenceTests.Score(shed, dan, will, NpcAlliances.SourLine - 1);
            NpcAlliances.Dissolve(shed);
            Assert.That(Pact(shed, pactId).members, Is.EqualTo(new[] { dan, janelle }), "Precondition: Will is shed and the founders stand.");
            watch.Watch(Advance, before, shed);
            Assert.That((r.shed, r.joinersOut, r.joins), Is.EqualTo((1, 0, 1)));
            Assert.That(r.heldBack, Is.EquivalentTo(new Dictionary<string, int> { { "a member left", 1 } }),
                "Below the bound only because its shed member still knows of it.");
            Assert.That(r.mustBeZero.Where(p => p.Value != 0).Select(p => p.Key), Is.Empty);

            var closed = watch.Close(shed);
            Assert.That(closed.grownEnds.Select(e => (e.why, e.weeks, e.largest, e.known)), Is.EqualTo(new[] { ("standing", 2, 3, true) }),
                "Standing at the end, grown to three, known to its shed member.");
            Assert.That((closed.rulesLow, closed.rulesHigh), Is.EqualTo((AlumniRules.GrowingPacts, AlumniRules.GrowingPacts)));

            // A part carries the record whole (BalanceLabParts.RoundTrip).
            var run = new BalanceLab.SeasonRun { cell = AllStars8(), index = 0, seed = 1u, pactWatch = closed, seasonMilliseconds = 1234.5 };
            var back = BalanceLabParts.RoundTrip(run);
            Assert.That(JsonConvert.SerializeObject(back.pactWatch), Is.EqualTo(JsonConvert.SerializeObject(closed)));
            Assert.That(back.seasonMilliseconds, Is.EqualTo(1234.5));
        }

        [Test]
        public void ThePactWatchCountsEveryMustBeZeroRowItIsShown()
        {
            Fixture(5402u, AlumniRules.GrowingPacts, out _, out var paired, out var s, out string pactId);
            string dan = On(s, Dan), janelle = On(s, Janelle), will = On(s, Will), vanessa = On(s, Vanessa), danielle = On(s, Danielle);
            string derrick = On(s, Derrick), cody = On(s, Cody);

            // Three written in at once: past the bound, past four in the house, more than one a week.
            var three = s.Clone();
            foreach (string id in new[] { will, vanessa, danielle }) HandAdd(three, pactId, id);
            var r = WatchOne(s, three);
            Only(r, "three at once", ("a join above its bound", 1), ("more than one join a pact a week", 1), ("more than 4 of a pact in the house", 1));
            Assert.That(r.joins, Is.EqualTo(3));

            // Derrick gone, so no known pair is live: a join is one with no live pair, past a bound of nought.
            var gone = s.Clone();
            gone.Find(derrick).status = ContestantStatus.Evicted;
            var noPair = gone.Clone();
            HandAdd(noPair, pactId, will);
            Only(WatchOne(gone, noPair), "no live pair", ("a join with no live pair", 1), ("a join above its bound", 1));

            // Cody, of the live Hitmen, written in.
            var bloc = s.Clone();
            HandAdd(bloc, pactId, cody);
            Only(WatchOne(s, bloc), "a live-pair member", ("a live-pair member in a grown pact", 1));

            // At 4, nothing may grow at all.
            var four = s.Clone();
            four.alumniRulesVersion = AlumniRules.CounterBlocs;
            var fourJoined = four.Clone();
            HandAdd(fourJoined, pactId, will);
            Only(WatchOne(four, fourJoined), "at 4", ("a join below alumni rules 5", 1));

            // Known outside it: whispered before the join.
            var whispered = s.Clone();
            Knowledge.Of(whispered, FactKinds.Alliance, pactId).visibility = FactVisibility.Whispered;
            var known = whispered.Clone();
            HandAdd(known, pactId, will);
            Only(WatchOne(whispered, known), "known outside", ("a join into a pact known outside it", 1));

            // In its fact's week: the pair formed this week and grew the same week.
            var sameWeek = paired.Clone();
            HandAdd(sameWeek, pactId, will);
            Only(WatchOne(paired, sameWeek), "its founding week", ("a join in its fact's week", 1));

            // A member out of the house.
            var left = s.Clone();
            left.Find(janelle).status = ContestantStatus.Evicted;
            var leftJoined = left.Clone();
            HandAdd(leftJoined, pactId, will);
            Only(WatchOne(left, leftJoined), "a member gone", ("a join into a pact with a member out of the house", 1));

            // A target agreement among the pact-to-be, standing on both sides of the join: the join breaks rule 8, and the
            // agreement is then aimed at a pact-mate of both, counted once however often it is watched.
            var agreed = s.Clone();
            agreed.deals.Add(new DealState
            {
                id = "deal-watch-test", type = DealKind.TargetAgreement, status = DealStatus.Active, proposerId = dan, recipientId = janelle, targetId = will,
                week = agreed.week, expiresWeek = agreed.week,
            });
            var agreedJoined = agreed.Clone();
            HandAdd(agreedJoined, pactId, will);
            var watch = new BalanceLab.PactWatch(agreed);
            watch.Watch(Advance, agreed, agreedJoined);
            watch.Watch(Advance, agreedJoined, agreedJoined.Clone());
            Only(watch.record, "an agreement inside it", ("a join while a target agreement stands among the pact-to-be", 1),
                ("a target agreement at a non-player pact-mate of both in a houseguests' pact", 1));
            Assert.That(watch.record.agreementsAtPactMate, Is.EqualTo(1));

            // Hunts, from the ledger entries a hunt writes: one aimed at a pact-mate of both in the grown pact, and one at Derrick
            // of the live Hitmen - a bloc hunt.
            var grown = s.Clone();
            Assert.That(NpcAlliances.TryJoin(grown, will, new HashSet<string>(StringComparer.Ordinal), out _), Is.True);
            grown.week++;
            var hunted = grown.Clone();
            string Name(string id) => hunted.Find(id).name;
            RelationshipLedger.Record(hunted, janelle, will, "rumor", EpisodeEngine.HuntImpact, "What " + Name(janelle) + " heard from " + Name(dan) + " about " + Name(will));
            RelationshipLedger.Record(hunted, janelle, derrick, "rumor", EpisodeEngine.HuntImpact, "What " + Name(janelle) + " heard from " + Name(dan) + " about " + Name(derrick));
            // A rumour is not a hunt.
            RelationshipLedger.Record(hunted, vanessa, cody, "rumor", NpcSocialActions.RumorImpact, "Something " + Name(vanessa) + " heard about " + Name(cody));
            r = WatchOne(grown, hunted);
            Only(r, "hunts", ("a hunt at a non-player pact-mate of both in a houseguests' pact", 1));
            Assert.That((r.hunts, r.blocHunts, r.huntsAtPactMate, r.mostHuntsOnOneTarget), Is.EqualTo((2, 1, 1, 1)));
        }
    }
}
#endif
