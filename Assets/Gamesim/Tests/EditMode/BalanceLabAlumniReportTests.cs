#if !UNITY_5_3_OR_NEWER
// ALL-STAR-HISTORIES-PLAN 14.H's report and 14.L.H's pact watch: Tools/SimulationTests only, as the balance lab they test
// is compiled out of Unity's assemblies.
using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
            // The watch's consistency rows read the live pairs from 3: at 4 they agree with the rules all season, as does every other row.
            Assert.That(run.pactWatch.mustBeZero.Where(p => p.Value != 0).Select(p => p.Key + " " + p.Value), Is.Empty);
            Assert.That(run.pactWatch.watchError, Is.Null, "The watch saw the season whole.");
            Assert.That(run.pactWatch.scoresHash, Does.Match("^[0-9a-f]{16}$"), "It hashed the final scores.");
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
            Assert.That(BalanceLabAlumniReport.ReportArms[0], Is.EqualTo(BalanceLabAlumniReport.ShippedArm), "5 is the shipped arm.");
            Assert.That(BalanceLabAlumniReport.ShippedArm, Is.EqualTo(AlumniRules.GrowingPacts), "S6's value, held since the titled names (6, words only) ship.");
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
                Assert.That(BalanceLabAlumniReport.ArmOf(shipped) == BalanceLabAlumniReport.ShippedArm && !shipped.alumniOff
                    && shipped.alumniHeld == (BalanceLabAlumniReport.ShippedArm == AlumniRules.Latest ? 0 : BalanceLabAlumniReport.ShippedArm), Is.True,
                    g.Key + ": the shipped arm is the cell left unheld, or held at 5 while a later value that changes only words is the latest.");
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

            // The cap (NpcAgencyHarnessTests' rule): seven NPCs hold at most two pacts of their own at once, so a third is a breach.
            Assert.That(NpcAlliances.PactCap(s), Is.EqualTo(2), "Precondition: seven NPCs, a cap of two.");
            var capped = s.Clone();
            capped.alliances.Add(new AllianceState { id = NpcAlliances.HouseguestPactPrefix + "watch-2", name = "Second", members = { vanessa, danielle } });
            var overCap = capped.Clone();
            overCap.alliances.Add(new AllianceState { id = NpcAlliances.HouseguestPactPrefix + "watch-3", name = "Third", members = { will, derrick } });
            Only(WatchOne(capped, overCap), "past the cap", ("a cap breach", 1));
            Only(WatchOne(s, capped), "at the cap");

            // One each: Janelle in a second pact of the houseguests' own.
            var doubled = s.Clone();
            doubled.alliances.Add(new AllianceState { id = NpcAlliances.HouseguestPactPrefix + "watch-2", name = "Second", members = { janelle, vanessa } });
            Only(WatchOne(s, doubled), "two pacts at once", ("a houseguest in two pacts of their own", 1));

            // A story's pact of two NPCs, private and a week old, that rises: a join into a pact not the houseguests' own.
            var story = s.Clone();
            var storyPact = new AllianceState { id = "alliance-story-watch", name = "Story", members = { vanessa, danielle } };
            story.alliances.Add(storyPact);
            story.week--;
            Knowledge.AllianceFormed(story, storyPact);
            story.week++;
            var storyJoined = story.Clone();
            HandAdd(storyJoined, storyPact.id, will);
            Only(WatchOne(story, storyJoined), "a story's pact", ("a join into a pact not the houseguests' own", 1));

            // The cards move under the watch: Cody renamed, the rules seat no Hitmen while the watch still reads them live - at 5
            // the live pairs and the bound both differ, at 3 (no bound yet) only the live pairs.
            var renamed = s.Clone();
            renamed.Find(cody).name += " (renamed)";
            Only(WatchOne(s, renamed), "renamed at 5", ("the watch's live pairs differ from the rules'", 1), ("the watch's bound differs from the rules'", 1));
            var atThree = s.Clone();
            atThree.alumniRulesVersion = AlumniRules.Reputation;
            var renamedAtThree = atThree.Clone();
            renamedAtThree.Find(cody).name += " (renamed)";
            Only(WatchOne(atThree, renamedAtThree), "renamed at 3", ("the watch's live pairs differ from the rules'", 1));

            // A read that throws ends the watch for the season and keeps its record with the error; nothing is read after it.
            var guarded = new BalanceLab.PactWatch(s);
            var kept = guarded.record;
            BalanceLab.PactRecord failed = null;
            var broken = s.Clone();
            broken.deals = null;
            BalanceLab.PactWatch.Guard(ref guarded, ref failed, w => w.Watch(Advance, s, broken));
            Assert.That(guarded, Is.Null, "The watch ends.");
            Assert.That(failed, Is.SameAs(kept), "Its record is kept.");
            Assert.That(failed.watchError, Does.StartWith(nameof(NullReferenceException) + ": "), "With the error on it.");
            int reads = 0;
            BalanceLab.PactWatch.Guard(ref guarded, ref failed, w => reads++);
            Assert.That(reads, Is.Zero, "Nothing is read once it has thrown.");
            var healthy = new BalanceLab.PactWatch(s);
            BalanceLab.PactRecord none = null;
            BalanceLab.PactWatch.Guard(ref healthy, ref none, w => w.Watch(Advance, s, s.Clone()));
            Assert.That((healthy != null, none), Is.EqualTo((true, (BalanceLab.PactRecord)null)), "A read that does not throw leaves the watch on.");
        }

        [Test]
        public void ThePactWatchReadsTheKnownPairRowsAndWhoLeftAGrownPactFromTheRecord()
        {
            Fixture(5402u, AlumniRules.GrowingPacts, out _, out _, out var s, out string pactId);
            string dan = On(s, Dan), janelle = On(s, Janelle), will = On(s, Will), vanessa = On(s, Vanessa), danielle = On(s, Danielle);
            string derrick = On(s, Derrick), cody = On(s, Cody);
            Assert.That(s.ledger.power, Is.Empty, "Precondition: no power row yet.");

            // Dan, an NPC Head of Household, puts both Hitmen up one week and Derrick and Will the next: of the live pair, Derrick
            // and Cody could go up each week (four member-weeks), and three went up. Nobody of a pair put the other up: no public split.
            var block = s.Clone();
            block.phase = EpisodePhase.Nomination;
            block.hohId = dan;
            block.nominees.Clear();
            var named = block.Clone();
            named.nominees.AddRange(new[] { derrick, cody });
            var watch = new BalanceLab.PactWatch(block);
            watch.Watch(Advance, block, named);
            var nextBlock = named.Clone();
            nextBlock.week++;
            nextBlock.nominees.Clear();
            nextBlock.ledger.power.Add(new PowerRow { week = named.week, hohId = dan, nominees = { derrick, cody } });
            var nextNamed = nextBlock.Clone();
            nextNamed.nominees.AddRange(new[] { derrick, will });
            watch.Watch(Advance, nextBlock, nextNamed);
            var final = nextNamed.Clone();
            final.ledger.power.Add(new PowerRow { week = nextNamed.week, hohId = dan, nominees = { derrick, will } });
            var closed = watch.Close(final);
            Assert.That((closed.memberWeeks, closed.memberPutUps, closed.publicSplit), Is.EqualTo((4, 3, false)));
            Assert.That(closed.scoresHash, Is.EqualTo(BalanceLab.PactWatch.ScoresHash(final)));

            // Cody's own block holds Derrick: the Hitmen split in public.
            var split = s.Clone();
            split.ledger.power.Add(new PowerRow { week = split.week, hohId = cody, nominees = { derrick, vanessa } });
            Assert.That(new BalanceLab.PactWatch(s).Close(split).publicSplit, Is.True);

            // The scores' hash reads every edge's score, whatever order the edges are kept in.
            var reordered = final.Clone();
            reordered.relationships.Reverse();
            Assert.That(BalanceLab.PactWatch.ScoresHash(reordered), Is.EqualTo(closed.scoresHash));
            AllWeekCadenceTests.Score(reordered, vanessa, danielle, 1);
            Assert.That(BalanceLab.PactWatch.ScoresHash(reordered), Is.Not.EqualTo(closed.scoresHash));

            // The Hitmen in a pact of their own again.
            var reformed = s.Clone();
            reformed.alliances.Add(new AllianceState { id = NpcAlliances.HouseguestPactPrefix + "watch-hitmen", name = "Hitmen", members = { derrick, cody } });
            Assert.That((WatchOne(s, s.Clone()).hitmenReformed, WatchOne(s, reformed).hitmenReformed), Is.EqualTo((false, true)));

            // Dan, Janelle and Will, grown by the rule, vote Vanessa out whole: three of four votes, and without them Danielle's
            // one would have led - an eviction an outsider bloc carried. Five of five is no bloc's doing.
            var grown = s.Clone();
            Assert.That(NpcAlliances.TryJoin(grown, will, new HashSet<string>(StringComparer.Ordinal), out _), Is.True);
            grown.week++;
            grown.votes.Clear();
            VoteState Vote(string voter, string target) => new VoteState { voterId = voter, targetId = target };
            var carried = grown.Clone();
            carried.votes.AddRange(new[] { Vote(dan, vanessa), Vote(janelle, vanessa), Vote(will, vanessa), Vote(derrick, danielle) });
            carried.ledger.power.Add(new PowerRow { week = carried.week, hohId = cody, nominees = { vanessa, danielle }, tally = { 3, 1 }, evicteeId = vanessa });
            Assert.That(WatchOne(grown, carried).blocCarried, Is.EqualTo(1));
            var landslide = grown.Clone();
            landslide.votes.AddRange(new[] { Vote(dan, vanessa), Vote(janelle, vanessa), Vote(will, vanessa), Vote(derrick, vanessa), Vote(cody, vanessa) });
            landslide.ledger.power.Add(new PowerRow { week = landslide.week, hohId = s.playerId, nominees = { vanessa, danielle }, tally = { 5, 0 }, evicteeId = vanessa });
            Assert.That(WatchOne(grown, landslide).blocCarried, Is.Zero);

            // Out of a grown pact that stands on: a founder is a founder and a joiner a joiner, whatever place in the roster a
            // founder's leaving has moved the joiner to.
            var leaves = new BalanceLab.PactWatch(s);
            var joined = s.Clone();
            Assert.That(NpcAlliances.TryJoin(joined, will, new HashSet<string>(StringComparer.Ordinal), out _), Is.True);
            leaves.Watch(Advance, s, joined);
            var next = joined.Clone();
            next.week++;
            var founderOut = next.Clone();
            Pact(founderOut, pactId).members.Remove(dan);
            leaves.Watch(Advance, next, founderOut);
            Assert.That((leaves.record.foundersOut, leaves.record.joinersOut, leaves.record.shed), Is.EqualTo((1, 0, 0)), "Dan, a founder, leaves.");
            var joinerOut = founderOut.Clone();
            Assert.That(Pact(joinerOut, pactId).members.IndexOf(will), Is.EqualTo(1), "Precondition: Will now stands second.");
            Pact(joinerOut, pactId).members.Remove(will);
            leaves.Watch(Advance, founderOut, joinerOut);
            Assert.That((leaves.record.foundersOut, leaves.record.joinersOut, leaves.record.shed), Is.EqualTo((1, 1, 0)), "Will, a joiner, leaves.");

            // The report's known-pair rows read the autopsy: out by week 2, and the week the first leaves.
            var metrics = AlumniReport.Metrics();
            BalanceLab.SeasonRun Season(params (string id, int outWeek)[] guests) => new BalanceLab.SeasonRun
            {
                cell = AllStars8(),
                autopsy = new SeasonAutopsy.Report { houseguests = guests.Select(g => new SeasonAutopsy.Houseguest { id = g.id, outWeek = g.outWeek }).ToList() },
                pactWatch = new BalanceLab.PactRecord { members = { "m1", "m2" } },
            };
            double? Read(string name, BalanceLab.SeasonRun run) => metrics.Single(x => x.name == name).of(run);
            Assert.That(Read("a known-pair member out by week 2", Season(("m1", 2), ("m2", 3), ("o1", 1))), Is.EqualTo(1));
            Assert.That(Read("a known-pair member out by week 2", Season(("m1", 3), ("m2", 0), ("o1", 1))), Is.EqualTo(0), "Week 3 is past it; an outsider's week 1 is not a member's.");
            Assert.That(Read("week the first known-pair member leaves", Season(("m1", 0), ("m2", 4), ("o1", 1))), Is.EqualTo(4), "A finalist never left.");
            var failedWatch = Season(("m1", 2), ("m2", 3));
            failedWatch.pactWatch.watchError = "NullReferenceException: test";
            Assert.That((Read("a known-pair member out by week 2", failedWatch), Read("weeks a season", failedWatch)), Is.EqualTo(((double?)null, (double?)0)),
                "A watch that threw loses its own rows, never the season's others.");
        }

        // ------------------------------------------------------------------ the statistics (S4's review)

        [Test]
        public void TheRatioAndRateRowsAndStopOnesPairedReadingReadAsTheirLabelsSay()
        {
            const double Z = 1.959963984540054;
            // A pooled ratio: the cell's put-ups over its member-weeks, each season one cluster - never the mean of the seasons' ratios (0.381 here).
            var pooled = AlumniReport.RatioSummary(new[] { (2.0, 2.0), (2.0, 14.0), (0.0, 4.0) });
            Assert.That((pooled.n, pooled.num, pooled.den), Is.EqualTo((3, 4.0, 20.0)));
            Assert.That(pooled.mean, Is.EqualTo(0.2).Within(1e-12));
            Assert.That(pooled.low, Is.EqualTo(0.2 - Z * 0.12).Within(1e-12), "Residuals 1.6, -0.8, -0.8: se 0.8 / (20/3).");
            Assert.That(pooled.high, Is.EqualTo(0.2 + Z * 0.12).Within(1e-12));
            Assert.That(AlumniReport.RatioSummary(new[] { (0.0, 0.0), (0.0, 0.0) }).mean, Is.NaN, "No exposure, no ratio.");

            // Two pooled ratios paired by seed: R(later) - R(earlier), each pair's linearised terms 0.12, -0.06 and -0.06.
            BalanceLab.SeasonRun Run(int index, int num, int den) => new BalanceLab.SeasonRun { index = index, seed = (uint)(900 + index), commands = num, own = den };
            var later = new[] { Run(0, 2, 2), Run(1, 2, 14), Run(2, 0, 4) };
            var earlier = new[] { Run(0, 1, 2), Run(1, 1, 14), Run(2, 0, 4) };
            var ratio = AlumniReport.PairedRatio(later, earlier, x => (x.commands, x.own));
            Assert.That((ratio.n, ratio.tested, ratio.beyond), Is.EqualTo((3, true, false)), "0.1 within 0.118 of nought.");
            Assert.That(ratio.mean, Is.EqualTo(0.1).Within(1e-12));
            Assert.That(ratio.low, Is.EqualTo(0.1 - Z * 0.06).Within(1e-12));
            Assert.That(ratio.high, Is.EqualTo(0.1 + Z * 0.06).Within(1e-12));
            Assert.Throws<ArgumentException>(() => AlumniReport.PairedRatio(later, new[] { Run(0, 1, 2), Run(1, 1, 14), Run(5, 0, 4) }, x => (x.commands, x.own)));

            // A rare rate: four seasons in a hundred move one way. The normal interval of the 0/1 differences calls it beyond
            // noise; McNemar (4:0, p 0.125) does not, and Newcombe's interval holds nought.
            BalanceLab.SeasonRun Flagged(int index, bool flag) => new BalanceLab.SeasonRun { index = index, seed = (uint)(100 + index), commands = flag ? 1 : 0 };
            var none = Enumerable.Range(0, 100).Select(i => Flagged(i, false)).ToList();
            var four = Enumerable.Range(0, 100).Select(i => Flagged(i, i < 4)).ToList();
            Func<BalanceLab.SeasonRun, double?> flagOf = x => x.commands;
            Assert.That(AlumniReport.Beyond(AlumniReport.Paired(four, none, flagOf)), Is.True, "Precondition: the normal interval excludes nought.");
            var rate = AlumniReport.PairedRate(four, none, flagOf);
            Assert.That((rate.n, rate.b, rate.c, rate.tested, rate.beyond), Is.EqualTo((100, 4, 0, true, false)));
            Assert.That(rate.p, Is.EqualTo(0.125).Within(1e-12));
            Assert.That(rate.mean, Is.EqualTo(0.04).Within(1e-12));
            Assert.That(rate.low < 0 && rate.high > 0.04, Is.True, "Newcombe's interval: " + rate.low + " to " + rate.high);
            // Six in twenty all one way: p = 2/64, beyond noise, and Newcombe's interval agrees.
            var six = Enumerable.Range(0, 20).Select(i => Flagged(i, i < 6)).ToList();
            var twentyNone = Enumerable.Range(0, 20).Select(i => Flagged(i, false)).ToList();
            var r6 = AlumniReport.PairedRate(six, twentyNone, flagOf);
            Assert.That((r6.b, r6.c, r6.beyond), Is.EqualTo((6, 0, true)));
            Assert.That(r6.p, Is.EqualTo(2.0 / 64).Within(1e-12));
            Assert.That(r6.low, Is.GreaterThan(0));
            var back = AlumniReport.PairedRate(twentyNone, six, flagOf);
            Assert.That((back.b, back.c, back.beyond, back.mean), Is.EqualTo((0, 6, true, -0.3)));
            // Newcombe's interval: arms that always agree have none; φ joins the margins (the same arms, uncorrelated, would spread).
            var agree = AlumniReport.Newcombe(10, 0, 0, 10);
            Assert.That(agree.low, Is.EqualTo(0).Within(1e-9), "Arms that always agree.");
            Assert.That(agree.high, Is.EqualTo(0).Within(1e-9));
            var spread = AlumniReport.Newcombe(10, 6, 0, 4);
            Assert.That(spread.low < 0.3 && spread.high > 0.3 && spread.low > 0, Is.True, "A difference of 0.3, ten pairs agreeing: " + spread.low + " to " + spread.high);
            Assert.That(AlumniReport.Newcombe(0, 0, 0, 0).low, Is.NaN);

            // Stop condition 1's two readings. Seventy per cent of twenty is within noise of 80% by arm 5's own interval, but six
            // seasons in twenty went the wrong way against arm 1: the paired reading fires, growth the cause where arm 4 meets it.
            Assert.That(AlumniReport.StopOne(0.80, false, (20, 20), (20, 20), (14, 20)), Is.EqualTo((false, false)));
            Assert.That(AlumniReport.StopOnePaired(0.80, false, (20, 20), (20, 20), (14, 20), (0, 6, 2.0 / 64)), Is.EqualTo((true, true)));
            Assert.That(AlumniReport.StopOnePaired(0.80, false, (20, 20), (12, 20), (14, 20), (0, 6, 2.0 / 64)), Is.EqualTo((true, false)), "Arm 4 crosses too.");
            Assert.That(AlumniReport.StopOnePaired(0.80, false, (20, 20), (20, 20), (14, 20), (6, 0, 2.0 / 64)), Is.EqualTo((false, false)), "The right way is no crossing.");
            Assert.That(AlumniReport.StopOnePaired(0.80, false, (20, 20), (20, 20), (14, 20), (0, 4, 0.125)), Is.EqualTo((false, false)), "Within noise.");
            Assert.That(AlumniReport.StopOnePaired(0.80, false, (15, 20), (20, 20), (14, 20), (0, 6, 2.0 / 64)), Is.EqualTo((false, false)), "Arm 1 does not meet it.");
            Assert.That(AlumniReport.StopOnePaired(0.80, false, (20, 20), (20, 20), (16, 20), (0, 4, 0.02)), Is.EqualTo((false, false)), "Arm 5 at the line is not past it.");
            Assert.That(AlumniReport.StopOnePaired(0.25, true, (0, 20), (0, 20), (6, 20), (6, 0, 2.0 / 64)), Is.EqualTo((true, true)), "Pile-ons: more is the wrong way.");
        }

        // ------------------------------------------------------------------ the report whole (S4's review)

        private const int Seeds = 20;

        /// <summary>A season the report reads, made by hand: a known pair (m1, m2) who reach the end, an outsider out first, four member-weeks and one put-up.</summary>
        private static BalanceLab.SeasonRun Synthetic(BalanceLab.Cell cell, int index)
        {
            int arm = BalanceLabAlumniReport.ArmOf(cell);
            var record = new BalanceLab.PactRecord { rulesLow = arm, rulesHigh = arm, members = { "m1", "m2" }, pairs = { "fact:m1|m2" }, memberWeeks = 4, memberPutUps = 1, scoresHash = "s" };
            foreach (string row in BalanceLab.PactWatch.MustBeZero) record.mustBeZero[row] = 0;
            return new BalanceLab.SeasonRun
            {
                cell = cell, index = index, seed = BalanceLab.Seed(cell, index), commands = 100,
                autopsy = new SeasonAutopsy.Report
                {
                    weeks = 7,
                    houseguests = { new SeasonAutopsy.Houseguest { id = "m1" }, new SeasonAutopsy.Houseguest { id = "m2" }, new SeasonAutopsy.Houseguest { id = "o1", outWeek = 1 } },
                    weeksPlayed = { new SeasonAutopsy.WeekRow { week = 1, evicteeId = "o1" } },
                },
                pace = new StoryPacingTests.Pace(),
                pactWatch = record,
            };
        }

        [Test]
        public void TheReportPutsItsBugLinesTheNearZeroRuleAndBothStopConditionsWhereThePlanSays()
        {
            var cells = BalanceLabAlumniReport.ReportCells();
            var runs = cells.SelectMany(c => Enumerable.Range(0, Seeds).Select(i => Synthetic(c, i))).ToList();
            List<BalanceLab.SeasonRun> At(string policy, string card, int size, int arm) =>
                runs.Where(r => r.cell.policy == policy && r.cell.playerTemplate == card && r.cell.size == size && BalanceLabAlumniReport.ArmOf(r.cell) == arm).OrderBy(r => r.index).ToList();
            const string Passive = BalancePolicies.Passive, Studier = BalancePolicies.Studier, Social = BalancePolicies.Social, Card = BalanceLabAlumniReport.Card;
            // The twelve-house grows at 5; the eight-house never does: the near-zero rule fires for each eight-house player.
            foreach (var r in runs.Where(r => r.cell.size == 12 && BalanceLabAlumniReport.ArmOf(r.cell) == 5)) r.pactWatch.joins = 1;
            // One cap breach at 5, one season not at its arm's value at 3, one watch that threw at 2.
            At(Passive, null, 12, 5)[0].pactWatch.mustBeZero["a cap breach"] = 1;
            var wrongArm = At(Studier, null, 12, 3)[5];
            wrongArm.pactWatch.rulesLow = wrongArm.pactWatch.rulesHigh = 4;
            At(Social, null, 12, 2)[3].pactWatch.watchError = "NullReferenceException: test";
            // Stop 1 as built and paired, growth the cause: the social newcomer's 8-house piles on at 5 and at 2, never at 1 or 4.
            foreach (var r in At(Social, null, 8, 5).Concat(At(Social, null, 8, 2))) r.pace.pileOns = 1;
            // Paired only: the studier's 8-house loses six pariah-free seasons in twenty at 5 (70%, within noise of 80% by its own interval).
            foreach (var r in At(Studier, null, 8, 5).Take(6)) r.pace.pariah = true;
            // Stop 1 with arm 4 crossing too: Cody's card in the twelve-house piles on at 4 and 5.
            foreach (var r in At(Passive, Card, 12, 4).Concat(At(Passive, Card, 12, 5))) r.pace.pileOns = 1;
            // Stop 2 at 4 only: a known-pair member the first evictee in every passive 8-house season at 4.
            foreach (var r in At(Passive, null, 8, 4)) r.autopsy.weeksPlayed[0].evicteeId = "m1";
            // A pooled ratio unlike the seasons' mean: the studier's twelve-house at 5, ten seasons of 1 in 2 and ten of 1 in 14.
            var studier12 = At(Studier, null, 12, 5);
            for (int i = 0; i < Seeds; i++) studier12[i].pactWatch.memberWeeks = i < 10 ? 2 : 14;
            // A rare rate: four public splits in twenty at 2 against none at 1 (McNemar 4:0, p 0.125).
            foreach (var r in At(Passive, null, 12, 2).Take(4)) r.pactWatch.publicSplit = true;
            // Play that differs: one pair's row, and two pairs' final scores, at 2 against 1.
            var passive8 = At(Passive, null, 8, 2);
            passive8[0].commands = 101;
            passive8[1].pactWatch.scoresHash = passive8[2].pactWatch.scoresHash = "t";

            var built = AlumniReport.Build(cells, runs, new AlumniReport.Meta { seeds = Seeds, threads = 1, minutes = 1, commit = "test", command = "test", host = "test", started = "test" });
            var lines = built.text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            string Who(string policy, string card) => card == null ? policy + " newcomer" : policy + " on " + card;
            Assert.That(lines[0], Does.StartWith("Near-zero rule (14.L.H): fewer than one season in ten of an 8-house cell at alumni rules 5 holds a join - " + Who(Passive, null) + " 0.0%"));
            Assert.That(lines[0], Does.Contain("In the twelve-house: " + Who(Passive, null) + " at 12 100.0%"));
            Assert.That(lines[1], Does.StartWith("BUG: must be zero, is 1 at arm 5 (shipped): a cap breach (first: " + At(Passive, null, 12, 5)[0].cell.Key + " #0)."));
            Assert.That(lines[2], Does.StartWith("BUG: must be zero, is 1 at arm 3 (held): a season not at its arm's value (first: " + wrongArm.cell.Key + " #5)."));
            Assert.That(lines[3], Does.StartWith("BUG: must be zero, is 1 at arm 2 (held): the watch threw").And.Contain("The watch's error: NullReferenceException: test."));
            Assert.That(lines[4], Does.StartWith("# All-Star loyalty, reputation and known blocs"));
            Assert.That(built.text, Does.Contain("| 2 - 1 | 3 - 2 | 4 - 3 | 5 - 4 | 4 - 0 | 5 - 0 |"), "Every comparison 14.H names, in its order.");
            Assert.That(built.text, Does.Contain("- Stop condition 1, as built (arm 5's own interval past the line), **FIRES** in 2: " + Who(Social, null) + " 8 (pile-on"));
            Assert.That(built.text, Does.Contain("- Stop condition 1, paired reading (the 5 - 1 difference beyond noise the wrong way), **FIRES** in 3: "));
            Assert.That(built.text, Does.Contain("- Stop condition 2 **FIRES** in 1: " + Who(Passive, null) + " 8 at 4 (100.0%"));

            var json = JObject.Parse(built.json);
            string Key(JToken x) => (string)x["player"] + "/" + (int)x["size"] + "/" + (string)x["budget"];
            var one = json["stopConditions"].Where(x => (int)x["condition"] == 1).ToList();
            Assert.That(one, Has.Count.EqualTo(16), "Two budgets in each of eight cells.");
            string pile = "pile-on <= 25%", pariah = "pariah-free >= 80%";
            Assert.That(one.Where(x => (bool)x["fires"]).Select(x => Key(x) + ":" + (bool)x["growthTheCause"]),
                Is.EquivalentTo(new[] { Who(Social, null) + "/8/" + pile + ":True", Who(Passive, Card) + "/12/" + pile + ":False" }));
            Assert.That(one.Where(x => (bool)x["firesPaired"]).Select(x => Key(x) + ":" + (bool)x["growthTheCausePaired"]),
                Is.EquivalentTo(new[] { Who(Social, null) + "/8/" + pile + ":True", Who(Studier, null) + "/8/" + pariah + ":True", Who(Passive, Card) + "/12/" + pile + ":False" }));
            var two = json["stopConditions"].Where(x => (int)x["condition"] == 2).ToList();
            Assert.That(two.Where(x => (bool)x["firesAt4"]).Select(x => (string)x["player"] + "/" + (int)x["size"]), Is.EqualTo(new[] { Who(Passive, null) + "/8" }));
            Assert.That(two.Where(x => (bool)x["firesAt5"]), Is.Empty);
            Assert.That(json["mustBeZero"].Where(x => (int)x["count"] != 0).Select(x => (string)x["row"] + "@" + (int)x["arm"] + "=" + (int)x["count"]),
                Is.EquivalentTo(new[] { "a cap breach@5=1", "a season not at its arm's value@3=1", "the watch threw@2=1" }));
            Assert.That(json["mustBeZero"].Select(x => (string)x["row"]).Distinct(),
                Is.EqualTo(BalanceLab.PactWatch.MustBeZero.Concat(AlumniReport.SeasonBugs.Select(b => b.row))), "Every row, the watch's first.");
            Assert.That(json["nearZero"].Where(x => (bool)x["fires"]).Select(x => (int)x["size"]), Is.EqualTo(new[] { 8, 8, 8, 8 }));

            // The pooled ratio, the rare rate, and the play that differs.
            var put = json["cells"].Single(x => (string)x["metric"] == "known-pair member put up by an NPC HoH, per member-week (pooled)" && (string)x["player"] == Who(Studier, null)
                && (int)x["size"] == 12 && (int)x["arm"] == 5);
            Assert.That(((double)put["mean"], (double)put["sums"]["num"], (double)put["sums"]["den"]), Is.EqualTo((0.125, 20.0, 160.0)), "20 put-ups over 160 member-weeks, not 0.286.");
            var splitRow = json["comparisons"].Single(x => (string)x["metric"] == "a known pair split in public" && (string)x["player"] == Who(Passive, null)
                && (int)x["size"] == 12 && (int)x["later"] == 2 && (int)x["earlier"] == 1);
            Assert.That(((bool)splitRow["beyond"], (int)splitRow["mcNemar"]["b"], (int)splitRow["mcNemar"]["c"], (double)splitRow["mean"]), Is.EqualTo((false, 4, 0, 0.2)));
            var differ = json["playsDifferently"].Single(x => (string)x["player"] == Who(Passive, null) && (int)x["size"] == 8 && (int)x["later"] == 2 && (int)x["earlier"] == 1);
            Assert.That(((int)differ["pairs"], (int)differ["rowsDiffer"], (int)differ["scoresDiffer"]), Is.EqualTo((Seeds, 1, 2)));
            Assert.That(json["playsDifferently"].Where(x => (int)x["later"] == 2 && (int)x["earlier"] == 1 && (string)x["player"] != Who(Passive, null)).Sum(x => (int)x["rowsDiffer"]),
                Is.Zero, "Arms 1 and 2 play alike in every other cell (the watch that threw changes no row).");

            // The primary family, 14.H's headline and known-pair rows, is listed first and counted apart, and only a comparison with a
            // test counts: Q4, the weekly wins and the week a member leaves read no season here, so seven primary rows in eight cells at six comparisons.
            Assert.That(json["meta"]["primaryRows"].Select(x => (string)x), Is.EqualTo(new[]
            {
                "Q3 season wins", "Q4 nomination rate (NPC HoH)", "weekly competition win rate", "known-pair member put up by an NPC HoH, per member-week (pooled)",
                "first evictee a known-pair member", "a known-pair member out by week 2", "week the first known-pair member leaves", "a known pair split in public",
                "pile-on seasons", "pariah-free seasons",
            }));
            Assert.That((int)json["crossed"]["primaryTested"], Is.EqualTo(7 * 8 * 6));
            Assert.That(json["crossed"]["primary"].Select(x => (string)x), Has.Some.StartsWith("pile-on seasons, " + Who(Social, null) + " 8, 5 - 4: "), "Piling on at 5 and never at 4.");
            Assert.That(json["crossed"]["other"].Select(x => (string)x), Has.None.StartsWith("pile-on seasons"));

            // The watch's tables reach the JSON cell by cell and pooled, and the CSV carries the ratio rows' parts and the hashes.
            var tables = (JObject)json["pactTables"];
            Assert.That(tables.Properties().Select(p => p.Name), Is.EquivalentTo(new[] { "largestOutsiderPact", "heldBack", "grownEnds", "hitmenFinalTwo", "pileOns" }));
            Assert.That(tables["heldBack"].Select(x => (string)x["player"]).Distinct(), Is.EquivalentTo(BalanceLabAlumniReport.ReportPlayers.Select(p => Who(p.policy, p.card)).Concat(new[] { "every player" })));
            Assert.That(tables["heldBack"], Has.Count.EqualTo(2 * 5 * 6), "Eight cells and two pooled lines, at six arms.");
            var csv = built.csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.That(csv[0], Does.EndWith(",memberWeeks,memberPutUps,joins,blocLiveWeeks,foundersOut,scoresHash,rowHash,watchError"));
            Assert.That(csv, Has.Length.EqualTo(1 + runs.Count));
        }
    }
}
#endif
