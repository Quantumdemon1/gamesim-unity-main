using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;
#if UNITY_5_3_OR_NEWER
using System.IO;
using System.Reflection;
using Gamesim.Persistence;
using Newtonsoft.Json.Linq;
#endif

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// B9's tuning rules across a save and a load (B9-BALANCE-PLAN, the owner's call 5 of 2026-11-07): each lever of tuning 1
    /// played through a save and a load and compared, command for command, with the same season played straight through.
    /// Preparation banked in a house of eight and spent at the next Head of Household after the load; a throw keeping it
    /// across a load and the veto after a second load spending it; the study's limit after a load, 1, and 2 in a small
    /// house's final week; the small house's watch; the final Head of Household's three parts counting it across loads; a
    /// war room's counter settled after a load under K5 and K1; and the Game Sense score read after a load. In Unity alone,
    /// the real store holds preparation to its bound by house size, on save and on load.
    ///
    /// <para><b>The save and the load</b> (<see cref="Carry"/>). In Unity they are the real store's (<c>EpisodeSaveStore</c>)
    /// at the live schema, 30, in a temporary slot of each test's own: the file must hold the season's own tuning and
    /// preparation, loading must not rewrite it, and the loaded season must be the saved one field for field. The dotnet
    /// subset has no store, so there they are the saved fields' JSON round trip, the one the subset's other reload tests
    /// use: it proves the scenarios and the levers' keys, and Unity's EditMode run is the store's proof.</para>
    ///
    /// <para><b>What catches a lever dropped on load.</b> Were the store to load a season at tuning 0, or without its banked
    /// preparation, the carry's own comparison fails at once, and so does each test's lever: the point would not be spent
    /// (the Head of Household's case) or not be in the score, the throw's case and the final week's would lose the point,
    /// a study at the limit would be accepted, the watch would not add one, the war room's tally would count the nominee's
    /// say or carry an unbroken tie (the K5 and K1-none cases), and Game Sense would weigh 0.3 / 0.4 / 0.3.</para>
    ///
    /// <para>The seasons are the season builder's, put on tuning 1 by hand as <see cref="TuningRulesTests"/> does, and the
    /// war rooms <see cref="PactPlanTests.WarRoom"/>'s; every walk is <see cref="EpisodeEngineTests.NextCommand"/>. The
    /// expected numbers are tuning 1's as built: a study fills to 1, a small house's final week to 2, and the watch adds one
    /// up to 2. If the owner's measurement drops the small house's boost from tuning 1, the watch's case at 8 and the final
    /// week's at 8 move with it.</para>
    /// </summary>
    public sealed class TuningSaveReloadTests
    {
        private const string UsedLine = "Your preparation was used in the competition. Preparation: 0/1.";
        private const string WatchedLine = "You watched the Head of Household competition and learned from it. Preparation: 2/2.";

        // ---------------------------------------------------------------- preparation: banked, saved, loaded, spent

        /// <summary>
        /// A study in the first free time of a house of eight banks one point; the season is saved and loaded there, and the
        /// Head of Household the whole house plays next is entered and played on both lines. After the load the score reads
        /// the point - one more than the same commands give with it dropped. The season is saved and loaded again between
        /// the result and its Advance, so the spend reads the competition's Played row from a load; the Advance spends the
        /// point, with the same private line, exactly as the season played straight through.
        /// </summary>
        [Test]
        public void PreparationBankedInAHouseOfEightIsSpentAfterALoadAtTheNextHeadOfHouseholdAsWithoutTheSave()
        {
            var straight = new EpisodeEngine(Built(8, 11u));
            Accept(straight, Study(straight.Snapshot));
            var banked = straight.Snapshot;
            Assert.That((banked.phase, banked.playerStudyBonus), Is.EqualTo((EpisodePhase.Social, 1)), "Precondition: one point banked in the free time.");
            var loadedState = Carry(banked);
            Assert.That((loadedState.tuningRulesVersion, loadedState.playerStudyBonus), Is.EqualTo((TuningRules.Bands, 1)), "The load keeps the tuning and the point.");
            var loaded = new EpisodeEngine(loadedState);

            // To the Head of Household's result, the player having played it.
            var commands = PlayBoth(straight, loaded, s => s.phase != EpisodePhase.HoH || !s.competitionResolved);
            var played = loaded.Snapshot;
            Assert.That((played.phase, played.competitionResolved), Is.EqualTo((EpisodePhase.HoH, true)));
            var row = played.ledger.competitions.Last(r => r.kind == "HoH");
            Assert.That((row.week, row.entry), Is.EqualTo((played.week, CompetitionEntry.Played)), "Precondition: the player played it.");
            Assert.That(played.playerStudyBonus, Is.EqualTo(1), "Nothing is spent before the Advance.");
            // The same commands on the loaded season with the point taken away: the only difference is the player's score.
            var droppedState = loadedState.Clone();
            droppedState.playerStudyBonus = 0;
            var dropped = new EpisodeEngine(droppedState);
            foreach (var command in commands) Accept(dropped, command);
            Assert.That(PlayerScore(played) - PlayerScore(dropped.Snapshot), Is.EqualTo(1.0).Within(1e-9), "The point is in the competition's score after the load.");

            // Saved and loaded again between the result and its Advance: the spend reads the week's Played row from the load.
            loaded = new EpisodeEngine(Carry(played));
            PlayBoth(straight, loaded, s => s.phase == EpisodePhase.HoH);
            var after = loaded.Snapshot;
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.Nomination));
            Assert.That(after.playerStudyBonus, Is.Zero, "The Advance out of the Head of Household spent it.");
            Assert.That(NewStudyLines(loadedState, after), Is.EqualTo(new[] { UsedLine }));
            Assert.That(after.events.Last(e => e.kind == "study-house").audienceIds, Is.EqualTo(new[] { after.playerId }), "Private to the player.");
            Valid(after);
        }

        /// <summary>
        /// One point banked and week one's Head of Household thrown; the season is saved and loaded before the Advance out of
        /// it, which keeps the point, as a throw does. Saved and loaded again at the veto the player is drawn into, the veto
        /// they play spends it. Both lines are the season played straight through, command for command.
        /// </summary>
        [Test]
        public void AThrowKeepsPreparationAcrossALoadAndTheVetoThePlayerEntersAfterASecondLoadSpendsIt()
        {
            var thrown = ThrownWithTheVetoToCome();
            Assert.That((thrown.phase, thrown.competitionResolved, thrown.playerStudyBonus), Is.EqualTo((EpisodePhase.HoH, true, 1)));
            Assert.That(thrown.ledger.competitions.Last(r => r.kind == "HoH").entry, Is.EqualTo(CompetitionEntry.Thrown), "Precondition: thrown.");
            var straight = new EpisodeEngine(thrown);
            var loadedState = Carry(thrown);
            Assert.That(loadedState.playerStudyBonus, Is.EqualTo(1));
            var loaded = new EpisodeEngine(loadedState);

            PlayBoth(straight, loaded, s => s.phase == EpisodePhase.HoH);
            var kept = loaded.Snapshot;
            Assert.That(kept.phase, Is.EqualTo(EpisodePhase.Nomination));
            Assert.That(kept.playerStudyBonus, Is.EqualTo(1), "A throw keeps it after the load, as without it.");
            Assert.That(NewStudyLines(loadedState, kept), Is.Empty, "Nothing used is logged.");

            PlayBoth(straight, loaded, s => s.week == thrown.week && (s.phase != EpisodePhase.Veto || s.pendingDiary != null));
            var veto = loaded.Snapshot;
            Assert.That((veto.week, veto.phase, veto.competitionResolved), Is.EqualTo((thrown.week, EpisodePhase.Veto, false)));
            Assert.That(EpisodeEngine.CompetitionPlayers(veto).Any(p => p.isPlayer), Is.True, "Precondition: the player is in the veto.");
            var reloadedState = Carry(veto);
            Assert.That(reloadedState.playerStudyBonus, Is.EqualTo(1), "Kept through the second load.");
            loaded = new EpisodeEngine(reloadedState);

            PlayBoth(straight, loaded, s => s.phase == EpisodePhase.Veto);
            var after = loaded.Snapshot;
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.VetoMeeting));
            var row = after.ledger.competitions.Last(r => r.kind == "Veto");
            Assert.That((row.week, row.entry), Is.EqualTo((veto.week, CompetitionEntry.Played)), "Precondition: the player played the veto.");
            Assert.That(after.playerStudyBonus, Is.Zero, "The veto the player entered after the second load spent it.");
            Assert.That(NewStudyLines(reloadedState, after), Is.EqualTo(new[] { UsedLine }));
            Valid(after);
        }

        // ---------------------------------------------------------------- the study's limit

        /// <summary>
        /// A house of eight outside its final week: one study fills preparation to its limit of 1; after a save and a load,
        /// a study of either approach is refused in the same words as straight through, before it spends an action or draws.
        /// </summary>
        [Test]
        public void AStudyAtTheLimitIsRefusedAfterALoadWithNothingSpentOrDrawn()
        {
            var straight = new EpisodeEngine(Built(8, 11u));
            Accept(straight, Study(straight.Snapshot));
            var full = straight.Snapshot;
            Assert.That((full.playerStudyBonus, TuningRules.StudyLimit(full), TuningRules.FinalWeek(full)), Is.EqualTo((1, 1, false)), "Precondition: at the limit of one.");
            var loaded = new EpisodeEngine(Carry(full));
            foreach (string approach in new[] { "memorize-layout", "sneak-peek" })
            {
                var there = straight.Apply(Study(straight.Snapshot, approach));
                var here = loaded.Apply(Study(loaded.Snapshot, approach));
                Assert.That((here.accepted, here.reason), Is.EqualTo((there.accepted, there.reason)), approach + ": after the load as without it.");
                Assert.That(here.accepted, Is.False, approach + ": refused at the limit after the load.");
                Assert.That(here.reason, Is.EqualTo("Your preparation is full. It is used in the next Head of Household or veto competition you enter."));
            }
            Assert.That(Json(loaded.Snapshot), Is.EqualTo(Json(full)), "Refused after the load: nothing spent, nothing drawn.");
            Assert.That(Json(straight.Snapshot), Is.EqualTo(Json(full)));
        }

#if UNITY_5_3_OR_NEWER
        /// <summary>
        /// The limits through the real store (Unity only: the subset has no store). Validation holds preparation to two in a
        /// house that moved in eight and to one in a house of twelve, in any week. A tuning-1 season of twelve holding two is
        /// refused on save, with nothing written, and on load from a file sealed by hand with a valid checksum, in
        /// validation's words; the same file at one loads. A season of eight holding two outside its final week, where only
        /// watching a Head of Household can put it, loads from the same kind of file, and saves and loads again.
        /// </summary>
        [Test]
        public void TheStoreHoldsPreparationToItsBoundByHouseSizeOnSaveAndOnLoad()
        {
            const string TwelveBound = "Study preparation must be between zero and one.";
            var twelve = Built(12, 5u);
            twelve.playerStudyBonus = 2;
            var refused = Assert.Throws<InvalidDataException>(() => store.Save(twelve));
            Assert.That(refused.Message, Is.EqualTo(TwelveBound));
            Assert.That(File.Exists(store.SavePath), Is.False, "Nothing is written.");
            File.WriteAllText(store.SavePath, PersistenceMigrationTests.Envelope(Payload(twelve)));
            Assert.That(store.TryLoad(out var loaded, out string message), Is.False, "Two in a house of twelve is refused on load.");
            Assert.That(loaded, Is.Null);
            Assert.That(message, Does.Contain(TwelveBound));
            Assert.That(message, Does.Not.Contain("checksum"), "Refused for the bound, not the seal.");

            var atOne = Built(12, 5u);
            atOne.playerStudyBonus = 1;
            var one = new EpisodeSaveStore(Path.Combine(directory, "twelve-at-one.json"));
            File.WriteAllText(one.SavePath, PersistenceMigrationTests.Envelope(Payload(atOne)));
            Assert.That(one.TryLoad(out loaded, out message), Is.True, message);
            Assert.That((loaded.tuningRulesVersion, loaded.playerStudyBonus), Is.EqualTo((TuningRules.Bands, 1)), "The same route at the bound loads.");

            var eight = Built(8, 5u);
            eight.playerStudyBonus = 2;
            Assert.That((TuningRules.InSmallHouse(eight), TuningRules.FinalWeek(eight), TuningRules.StudyLimit(eight)), Is.EqualTo((true, false, 1)),
                "Precondition: a house of eight outside its final week, where a study stops at one.");
            var small = new EpisodeSaveStore(Path.Combine(directory, "eight.json"));
            File.WriteAllText(small.SavePath, PersistenceMigrationTests.Envelope(Payload(eight)));
            Assert.That(small.TryLoad(out loaded, out message), Is.True, message);
            Assert.That((loaded.tuningRulesVersion, loaded.playerStudyBonus), Is.EqualTo((TuningRules.Bands, 2)), "Two in a house of eight loads.");
            small.Save(loaded);
            Assert.That(small.TryLoad(out loaded, out message), Is.True, message);
            Assert.That(loaded.playerStudyBonus, Is.EqualTo(2), "Saved and loaded again by the store itself.");
        }
#endif

        /// <summary>
        /// The final week's free time, three left: the season is saved and loaded, and saved and loaded again after every
        /// study. A study fills preparation to the final week's limit - 2 in a house that moved in eight, 1 in a house of
        /// twelve - and the next is refused in the final week's words. Saved and loaded before the final Head of Household
        /// and between its first two parts, all three parts count the preparation and none spends it, as straight through.
        /// </summary>
        [TestCase(8, 2)]
        [TestCase(12, 1)]
        public void TheFinalWeeksStudyAndTheFinalHeadOfHouseholdsThreePartsCountItAcrossLoads(int size, int limit)
        {
            var straight = FinalWeek(size);
            var start = straight.Snapshot;
            Assert.That(TuningRules.StudyLimit(start), Is.EqualTo(limit), "Precondition: the final week's limit at " + size + ".");
            int since = start.nextSequence;
            var loaded = new EpisodeEngine(Carry(start));
            for (int study = 1; study <= limit + 1; study++)
            {
                var before = loaded.Snapshot;
                var there = straight.Apply(Study(straight.Snapshot));
                var here = loaded.Apply(Study(before));
                Assert.That((here.accepted, here.reason), Is.EqualTo((there.accepted, there.reason)), "Study " + study + " at " + size);
                if (study <= limit)
                {
                    Assert.That(here.accepted, Is.True, here.reason);
                    Assert.That(here.state.events.Last().text, Does.EndWith("Preparation: " + study + "/" + limit + ". One social action spent."));
                }
                else
                {
                    Assert.That(here.accepted, Is.False, "At the limit after a load, at " + size);
                    Assert.That(here.reason, Is.EqualTo("Your preparation is full. All three parts of the final Head of Household count it."));
                    Assert.That(Json(loaded.Snapshot), Is.EqualTo(Json(before)), "Refused: nothing spent, nothing drawn.");
                }
                Assert.That(Json(loaded.Snapshot), Is.EqualTo(Json(straight.Snapshot)), "Study " + study + ": the two lines meet.");
                loaded = new EpisodeEngine(Carry(loaded.Snapshot));
            }
            Assert.That(loaded.Snapshot.playerStudyBonus, Is.EqualTo(limit));

            // The free time leads straight to the final Head of Household: saved and loaded at its first part.
            PlayBoth(straight, loaded, s => s.phase == EpisodePhase.Social);
            Assert.That(loaded.Snapshot.phase, Is.EqualTo(EpisodePhase.FinalHoHPart1));
            loaded = new EpisodeEngine(Carry(loaded.Snapshot));
            PlayBoth(straight, loaded, s => s.phase == EpisodePhase.FinalHoHPart1);
            var second = loaded.Snapshot;
            Assert.That(second.phase, Is.EqualTo(EpisodePhase.FinalHoHPart2));
            // All three of the final three play part 1: the line that explains the player's score counts the preparation.
            var explained = second.events.Last(e => e.kind == "competition-performance" && e.sequence >= since).text;
            Assert.That(explained, Does.Contain("; preparation +" + limit + "; "), "Part 1 counted the preparation after the load.");
            Assert.That(second.playerStudyBonus, Is.EqualTo(limit), "Part 1 spent nothing.");

            // Saved and loaded between the first two parts, then the other two played.
            loaded = new EpisodeEngine(Carry(second));
            PlayBoth(straight, loaded, s => s.phase == EpisodePhase.FinalHoHPart2 || s.phase == EpisodePhase.FinalHoHPart3);
            var after = loaded.Snapshot;
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.FinalEviction), "Precondition: past the final Head of Household's three parts.");
            Assert.That(after.playerStudyBonus, Is.EqualTo(limit), "Kept through the final Head of Household at " + size + ".");
            var log = after.events.Where(e => e.sequence >= since && e.kind == "study-house").Select(e => e.text).ToList();
            Assert.That(log.Count, Is.EqualTo(limit), "Only the studies are logged: nothing used, nothing learned.");
            Assert.That(log.All(text => text.StartsWith("You studied the house", StringComparison.Ordinal)), Is.True, string.Join(" | ", log));
            Valid(after);
        }

        // ---------------------------------------------------------------- the small house's watch

        /// <summary>
        /// The free time after the player's week as Head of Household: a study banks one point, the season is saved and
        /// loaded, and the next Head of Household is played without the player. In a house that moved in eight they learn
        /// from watching it, one more up to 2, with its private line; in a house of twelve the point stays as it was.
        /// </summary>
        [TestCase(8, 2)]
        [TestCase(12, 1)]
        public void AStudiedPlayerWhoSitsAHeadOfHouseholdOutAfterALoadLearnsFromWatchingItInASmallHouseOnly(int size, int expected)
        {
            var straight = new EpisodeEngine(Built(size, OutgoingHohSeed(size)));
            WalkWhile(straight, OutgoingHoh);
            Assert.That(straight.Snapshot.hohId, Is.EqualTo(straight.Snapshot.playerId), "Precondition: the player is the outgoing Head of Household.");
            Assert.That(straight.Snapshot.playerStudyBonus, Is.Zero, "Nothing banked that was not studied.");
            Accept(straight, Study(straight.Snapshot));
            var banked = straight.Snapshot;
            Assert.That(banked.playerStudyBonus, Is.EqualTo(1));
            var loadedState = Carry(banked);
            var loaded = new EpisodeEngine(loadedState);

            PlayBoth(straight, loaded, s => s.phase != EpisodePhase.HoH);
            PlayBoth(straight, loaded, s => s.phase == EpisodePhase.HoH);
            var after = loaded.Snapshot;
            var row = after.ledger.competitions.Last(r => r.kind == "HoH");
            Assert.That((row.week, row.entry, row.placement), Is.EqualTo((after.week, CompetitionEntry.Watched, 0)), "Precondition: they sat it out.");
            Assert.That(after.playerStudyBonus, Is.EqualTo(expected), "After the load, at " + size + ".");
            Assert.That(NewStudyLines(loadedState, after), Is.EqualTo(expected == 2 ? new[] { WatchedLine } : new string[0]));
            Valid(after);
        }

        // ---------------------------------------------------------------- the war room's counter (K5, K1)

        /// <summary>
        /// K5. The player's pact of four with the first nominee in it: the two voters say the first nominee, the nominee says
        /// the other. After the load the player counters for the other: the nominee's say counts on neither side, so it is
        /// one to two and the plan holds. The tuning-0 twin counts it - two to two, a tie the player's founding carries.
        /// </summary>
        [Test]
        public void AfterALoadANomineesSayForTheCounterCountsOnNeitherSideAndThePlanHolds()
        {
            var s = PactPlanTests.WarRoom(out _, extra: 1);
            var npcs = Npcs(s);
            foreach (var id in new[] { npcs[3], npcs[4], npcs[1] }) s.Find(id).traits = new List<string> { "Sneaky" };
            string plan = s.nominees[0], counter = s.nominees[1];
            Assert.That(npcs[1], Is.EqualTo(plan), "Precondition: the nominee in the pact is the plan's target.");
            var open = OpenAtTuningOne(s, (npcs[3], plan), (npcs[4], plan), (npcs[1], counter));
            var settled = CounterAfterALoad(open, counter, out var expected, out var nought);
            Assert.That(settled.cameRound, Is.Empty);
            Assert.That((settled.stance, settled.targetId, settled.counterId), Is.EqualTo((PactPlanStance.Countered, plan, counter)), "Tuning 1 after the load: one to two.");
            Assert.That(settled.targetId, Is.EqualTo(expected.targetId));
            Assert.That((nought.stance, nought.targetId), Is.EqualTo((PactPlanStance.Countered, counter)), "Tuning 0: two to two, and the player founded it.");
        }

        /// <summary>
        /// K5 on its own. The same pact; the nominee in it and one voter say the other nominee, the plan, and the second voter
        /// says the pact's own nominee, as the player's counter does. After the load the nominee's say for the plan is not
        /// counted either: two to one, and the counter carries without a tie (with the say counted and K1 alone it would be a
        /// tie nobody came round in, and the plan would stand).
        /// </summary>
        [Test]
        public void AfterALoadANomineesSayForThePlanCountsOnNeitherSideEither()
        {
            var s = PactPlanTests.WarRoom(out _, extra: 1);
            var npcs = Npcs(s);
            foreach (var id in new[] { npcs[3], npcs[4], npcs[1] }) s.Find(id).traits = new List<string> { "Sneaky" };
            string inPact = s.nominees[0], other = s.nominees[1];
            Assert.That(npcs[1], Is.EqualTo(inPact), "Precondition: the first nominee is in the pact.");
            var open = OpenAtTuningOne(s, (npcs[3], other), (npcs[1], other), (npcs[4], inPact));
            var settled = CounterAfterALoad(open, inPact, out var expected, out var nought);
            Assert.That(settled.cameRound, Is.Empty);
            Assert.That((settled.stance, settled.targetId, settled.counterId), Is.EqualTo((PactPlanStance.Countered, inPact, inPact)), "Tuning 1 after the load: two to one.");
            Assert.That(settled.targetId, Is.EqualTo(expected.targetId));
            Assert.That(nought.targetId, Is.EqualTo(inPact), "Tuning 0: two to two, and the player founded it.");
        }

        /// <summary>
        /// K1. The player's pact of the two voters off the block: one says the plan and cannot come round, the other says
        /// nothing - one against one. After the load nobody came round, so the tie does not carry the counter and the plan
        /// stands; the tuning-0 twin gives the tie to the player, who founded the pact.
        /// </summary>
        [Test]
        public void AfterALoadATieNobodyCameRoundInLeavesThePlanStanding()
        {
            var s = PactPlanTests.WarRoom(out _, extra: 0);
            var npcs = Npcs(s);
            foreach (var id in new[] { npcs[3], npcs[4] }) s.Find(id).traits = new List<string> { "Sneaky" };
            string plan = s.nominees[0], counter = s.nominees[1];
            var open = OpenAtTuningOne(s, (npcs[3], plan));
            Assert.That(PactPlans.OpenPlan(open, PactPlanTests.PactId).present, Does.Contain(npcs[4]), "Precondition: the quiet member was at the meeting.");
            var settled = CounterAfterALoad(open, counter, out var expected, out var nought);
            Assert.That(settled.cameRound, Is.Empty);
            Assert.That((settled.stance, settled.targetId, settled.counterId), Is.EqualTo((PactPlanStance.Countered, plan, counter)), "Tuning 1 after the load: nobody came round.");
            Assert.That(settled.targetId, Is.EqualTo(expected.targetId));
            Assert.That(nought.targetId, Is.EqualTo(counter), "Tuning 0: the tie goes to the player who founded it.");
        }

        /// <summary>
        /// K1's other half. The player's pact of the two voters and the Head of Household, all saying the plan: two Sneaky,
        /// who never come round, and one Loyal voter who thinks the world of the player. On the first seed where the Loyal
        /// one comes round on their coin it is two against two, a tie a member came round in: after the load the counter
        /// carries, at tuning 1 as at tuning 0.
        /// </summary>
        [Test]
        public void AfterALoadATieAMemberCameRoundInStillCarriesTheCounter()
        {
            for (uint seed = 1; seed <= 60; seed++)
            {
                var s = PactPlanTests.WarRoom(out var pact, seed: seed);
                var npcs = Npcs(s);
                pact.members.Add(npcs[0]);
                PactPlanTests.Warm(s, pact);
                foreach (var id in new[] { npcs[3], npcs[0] }) s.Find(id).traits = new List<string> { "Sneaky" };
                s.Find(npcs[4]).traits = new List<string> { "Loyal" };
                PactPlanTests.SetScore(s, npcs[4], s.playerId, 100);
                if (!PactPlans.ComesRound(s, PactPlanTests.PactId, npcs[4])) continue;
                string plan = s.nominees[0], counter = s.nominees[1];
                Assert.That(new[] { npcs[3], npcs[4], npcs[0] }.Intersect(s.nominees), Is.Empty, "Precondition: the three are off the block.");
                var open = OpenAtTuningOne(s, (npcs[3], plan), (npcs[4], plan), (npcs[0], plan));
                var settled = CounterAfterALoad(open, counter, out var expected, out var nought);
                Assert.That(settled.cameRound, Is.EqualTo(new[] { npcs[4] }), "Seed " + seed + ": the Loyal member came round after the load, on the same coin.");
                Assert.That((settled.stance, settled.targetId, settled.counterId), Is.EqualTo((PactPlanStance.Countered, counter, counter)), "Tuning 1 after the load: a tie a member came round in carries.");
                Assert.That(settled.targetId, Is.EqualTo(expected.targetId));
                Assert.That(nought.cameRound, Is.EqualTo(settled.cameRound), "Tuning 0: the same coin.");
                Assert.That(nought.targetId, Is.EqualTo(counter), "Tuning 0: the same tie, the same side.");
                return;
            }
            Assert.Fail("No seed up to 60 brings the Loyal member round.");
        }

        // ---------------------------------------------------------------- Game Sense

        /// <summary>
        /// A week of a house of eight whose faces make the two weightings round apart (the first from week 3 on seeds from
        /// 11, as <see cref="TuningRulesTests"/> finds it): Game Sense read after a load is the straight-through season's,
        /// weighs its faces 0.2 / 0.6 / 0.2, and differs from tuning 0's 0.3 / 0.4 / 0.3 on the same faces. Played on into
        /// the next week, the two lines still read the same verdict, on tuning 1's weights.
        /// </summary>
        [Test]
        public void GameSenseReadAfterALoadWeighsItsFacesAsTheSeasonPlayedStraightThroughDoes()
        {
            EpisodeEngine straight = null;
            for (uint seed = 11; seed < 41 && straight == null; seed++)
            {
                var walk = new EpisodeEngine(Built(8, seed));
                for (int week = 3; week <= 5 && straight == null; week++)
                {
                    WalkWhile(walk, s => s.week < week);
                    if (walk.Snapshot.phase == EpisodePhase.Finished) break;
                    if (Sense(walk.Snapshot, TuningRules.Bands).score != Sense(walk.Snapshot, TuningRules.Off).score) straight = walk;
                }
            }
            Assert.That(straight, Is.Not.Null, "Precondition: a week whose two weightings round apart.");
            var at = straight.Snapshot;
            var loadedState = Carry(at);
            var read = GameSense.Evaluate(loadedState);
            var there = GameSense.Evaluate(at);
            var nought = Sense(loadedState, TuningRules.Off);
            Assert.That((read.score, read.competitions, read.strategy, read.social), Is.EqualTo((there.score, there.competitions, there.strategy, there.social)),
                "Read after the load, the straight-through verdict.");
            Assert.That(read.score, Is.EqualTo((int)Math.Round(read.competitions * 0.2 + read.strategy * 0.6 + read.social * 0.2)), "Tuning 1's weights.");
            Assert.That((nought.competitions, nought.strategy, nought.social), Is.EqualTo((read.competitions, read.strategy, read.social)), "The same faces.");
            Assert.That(nought.score, Is.EqualTo((int)Math.Round(nought.competitions * 0.3 + nought.strategy * 0.4 + nought.social * 0.3)));
            Assert.That(read.score, Is.Not.EqualTo(nought.score), "Not tuning 0's weighting.");

            var loaded = new EpisodeEngine(loadedState);
            PlayBoth(straight, loaded, s => s.week <= at.week);
            var later = GameSense.Evaluate(loaded.Snapshot);
            var laterThere = GameSense.Evaluate(straight.Snapshot);
            Assert.That((later.score, later.competitions, later.strategy, later.social), Is.EqualTo((laterThere.score, laterThere.competitions, laterThere.strategy, laterThere.social)));
            Assert.That(later.score, Is.EqualTo((int)Math.Round(later.competitions * 0.2 + later.strategy * 0.6 + later.social * 0.2)));
        }

        // ---------------------------------------------------------------- the save and the load

#if UNITY_5_3_OR_NEWER
        private const string Prefix = "GamesimTuningSaveReload-";
        private string directory;
        private EpisodeSaveStore store;

        [SetUp]
        public void ASlotOfItsOwn()
        {
            directory = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
            Assert.That(Directory.Exists(directory), Is.False);
            Directory.CreateDirectory(directory);
            store = new EpisodeSaveStore(Path.Combine(directory, "episode.json"));
        }

        [TearDown]
        public void RemoveOnlyThisSlot()
        {
            store = null;
            if (directory == null) return;
            string resolved = Path.GetFullPath(directory);
            directory = null;
            Assert.That(Path.GetDirectoryName(resolved),
                Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
            Assert.That(Path.GetFileName(resolved), Does.StartWith(Prefix));
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }

        /// <summary>
        /// The save and the load, through the real store in this test's slot, as the director saves and loads: the file holds
        /// the live schema (30) and the season's own tuning and preparation, loading neither migrates nor rewrites it, and the
        /// loaded season is the saved one, field for field.
        /// </summary>
        private EpisodeState Carry(EpisodeState s)
        {
            Assert.That(EpisodeState.LiveSchema, Is.GreaterThanOrEqualTo(30), "The tuning rules are schema 30's.");
            string before = Json(s);
            store.Save(s);
            Assert.That(Json(s), Is.EqualTo(before), "Saving changes nothing in the season.");
            var written = (JObject)JObject.Parse(File.ReadAllText(store.SavePath))["state"];
            Assert.That((int)written["schemaVersion"], Is.EqualTo(EpisodeState.LiveSchema));
            Assert.That((int)written["tuningRulesVersion"], Is.EqualTo(s.tuningRulesVersion), "The file holds the season's tuning.");
            Assert.That((int)written["playerStudyBonus"], Is.EqualTo(s.playerStudyBonus), "The file holds the banked preparation.");
            byte[] bytes = File.ReadAllBytes(store.SavePath);
            Assert.That(store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Not.Contain("migrated"));
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(bytes), "Loading never rewrites the save.");
            Assert.That(Json(loaded), Is.EqualTo(before), "The loaded season is the saved one, field for field.");
            return loaded;
        }

        /// <summary>The store's own serializer (internal to Persistence, so by reflection, as the other store tests reach it).</summary>
        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

        /// <summary>A season's saved fields, as the store writes them under "state".</summary>
        private static JObject Payload(EpisodeState s) => JObject.FromObject(s, Serializer());
#else
        /// <summary>
        /// The dotnet subset's save and load: the saved fields' JSON round trip, as the subset's other reload tests make it
        /// (no store here), and the loaded season the saved one, field for field. Unity's EditMode run carries through the
        /// real store.
        /// </summary>
        private EpisodeState Carry(EpisodeState s)
        {
            string before = Json(s);
            var loaded = JsonConvert.DeserializeObject<EpisodeState>(before, new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
            Valid(loaded);
            Assert.That(Json(loaded), Is.EqualTo(before), "The loaded season is the saved one, field for field.");
            return loaded;
        }
#endif

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// The same season on two lines - straight through, and after a save and a load - played by the plain walk while
        /// <paramref name="going"/> holds on the straight line: the two equal before every command, the same command applied
        /// to both, and equal at the end. Returns the commands played.
        /// </summary>
        private static List<EpisodeCommand> PlayBoth(EpisodeEngine straight, EpisodeEngine loaded, Func<EpisodeState, bool> going, int limit = 400)
        {
            var played = new List<EpisodeCommand>();
            for (int guard = 0; guard < limit; guard++)
            {
                var there = straight.Snapshot;
                if (!going(there) || there.phase == EpisodePhase.Finished) break;
                Assert.That(Json(loaded.Snapshot), Is.EqualTo(Json(there)), "The two lines meet before command " + (played.Count + 1) + ".");
                var command = EpisodeEngineTests.NextCommand(there);
                Accept(straight, command);
                Accept(loaded, command);
                played.Add(command);
            }
            Assert.That(Json(loaded.Snapshot), Is.EqualTo(Json(straight.Snapshot)), "The two lines end equal.");
            return played;
        }

        /// <summary>
        /// The war room's counter after a load: <paramref name="open"/> saved and loaded, and the player's counter for
        /// <paramref name="counter"/>, said to the member the plan was held through, played on the loaded season and on the
        /// same season straight through, which must end equal; and on its tuning-0 twin. Returns the settled row after the
        /// load, what <see cref="PactPlans.Settle"/> predicts from the loaded season, and the twin's row.
        /// </summary>
        private PactPlanRow CounterAfterALoad(EpisodeState open, string counter, out PactPlans.Settlement expected, out PactPlanRow nought)
        {
            string through = PactPlans.OpenPlan(open, PactPlanTests.PactId).throughId;
            var loadedState = Carry(open);
            Assert.That(loadedState.tuningRulesVersion, Is.EqualTo(TuningRules.Bands));
            var probe = loadedState.Clone();
            expected = PactPlans.Settle(probe, probe.alliances.Single(a => a.id == PactPlanTests.PactId),
                PactPlans.OpenPlan(probe, PactPlanTests.PactId), PactPlans.Counter, counter);
            var command = Answer(open, through, counter);
            var there = new EpisodeEngine(open).Apply(command);
            var here = new EpisodeEngine(loadedState).Apply(command);
            Assert.That(there.accepted, Is.True, there.reason);
            Assert.That(here.accepted, Is.True, here.reason);
            Assert.That(Json(here.state), Is.EqualTo(Json(there.state)), "The counter settles after the load as without it.");
            var twin = open.Clone();
            twin.tuningRulesVersion = TuningRules.Off;
            Valid(twin);
            var old = new EpisodeEngine(twin).Apply(command);
            Assert.That(old.accepted, Is.True, old.reason);
            nought = PactPlans.ThisWeek(old.state, PactPlanTests.PactId);
            return PactPlans.ThisWeek(here.state, PactPlanTests.PactId);
        }

        /// <summary>The war room's campaign put on tuning 1 (competition rules 4, which it needs), with its pact's plan open on the ledger with these says.</summary>
        private static EpisodeState OpenAtTuningOne(EpisodeState s, params (string member, string target)[] says)
        {
            s.competitionRulesVersion = CompetitionRules.Widened;
            s.tuningRulesVersion = TuningRules.Bands;
            s.ledger.plans.Add(PactPlanTests.Row(s, says));
            Assert.That(PactPlans.OpenPlan(s, PactPlanTests.PactId), Is.Not.Null, "Precondition: the plan is open.");
            Valid(s);
            return s;
        }

        /// <summary>A built season of the size on tuning 1: competition rules 4 (the season builder's), no other rule switched on.</summary>
        private static EpisodeState Built(int size, uint seed)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);
            Assert.That(s.competitionRulesVersion, Is.EqualTo(CompetitionRules.Widened));
            s.tuningRulesVersion = TuningRules.Bands;
            Valid(s);
            return s;
        }

        /// <summary>
        /// The first seed from 1 whose house of eight, the player having studied once and thrown week one's Head of Household,
        /// reaches that week's veto with the player in its field: the state just after the throw, before the Advance.
        /// </summary>
        private static EpisodeState ThrownWithTheVetoToCome()
        {
            for (uint seed = 1; seed < 200; seed++)
            {
                var engine = new EpisodeEngine(Built(8, seed));
                Accept(engine, Study(engine.Snapshot));
                WalkWhile(engine, s => s.phase == EpisodePhase.Social);
                WalkWhile(engine, s => s.pendingDiary != null);
                Accept(engine, Command(engine.Snapshot, EpisodeCommandKind.ThrowCompetition));
                var thrown = engine.Snapshot;
                WalkWhile(engine, s => s.week == thrown.week && (s.phase != EpisodePhase.Veto || s.pendingDiary != null));
                var at = engine.Snapshot;
                if (at.week == thrown.week && at.phase == EpisodePhase.Veto && !at.competitionResolved && EpisodeEngine.CompetitionPlayers(at).Any(p => p.isPlayer))
                    return thrown;
            }
            Assert.Fail("No seed under 200 puts the player in the veto after a thrown Head of Household in a house of eight.");
            return null;
        }

        /// <summary>The first seed from 1 whose built season, walked on tuning 1, reaches the final week's free time with the player in it.</summary>
        private static EpisodeEngine FinalWeek(int size)
        {
            bool There(EpisodeState s) => s.phase == EpisodePhase.Social && s.pendingDiary == null && s.Active.Count() == 3
                && s.Find(s.playerId).status == ContestantStatus.Active;
            for (uint seed = 1; seed < 300; seed++)
            {
                var engine = new EpisodeEngine(Built(size, seed));
                WalkWhile(engine, s => !There(s) && s.Find(s.playerId).status == ContestantStatus.Active, 4000);
                if (!There(engine.Snapshot)) continue;
                Assert.That(engine.Snapshot.playerStudyBonus, Is.Zero, "Precondition: the walk never studied.");
                return engine;
            }
            Assert.Fail("No seed under 300 brings the player to the final week at " + size + ".");
            return null;
        }

        /// <summary>Still walking: not yet at a free time while the player is Head of Household, with a weekly one to come.</summary>
        private static bool OutgoingHoh(EpisodeState s) =>
            !(s.phase == EpisodePhase.Social && s.hohId == s.playerId && s.Active.Count() > 4 && s.Find(s.playerId).status == ContestantStatus.Active);

        /// <summary>The first seed whose built season, walked on tuning 1, reaches a free time with the player its Head of Household.</summary>
        private static uint OutgoingHohSeed(int size)
        {
            for (uint seed = 1; seed < 400; seed++)
            {
                var engine = new EpisodeEngine(Built(size, seed));
                WalkWhile(engine, OutgoingHoh);
                if (!OutgoingHoh(engine.Snapshot)) return seed;
            }
            Assert.Fail("No seed under 400 makes the player Head of Household before a free time at " + size + ".");
            return 0;
        }

        private static void WalkWhile(EpisodeEngine engine, Func<EpisodeState, bool> going, int limit = 400)
        {
            for (int guard = 0; guard < limit && going(engine.Snapshot) && engine.Snapshot.phase != EpisodePhase.Finished; guard++)
                Accept(engine, EpisodeEngineTests.NextCommand(engine.Snapshot));
        }

        /// <summary>The study-house lines logged since <paramref name="from"/>, in order.</summary>
        private static string[] NewStudyLines(EpisodeState from, EpisodeState to) =>
            to.events.Where(e => e.sequence >= from.nextSequence && e.kind == "study-house").Select(e => e.text).ToArray();

        private static double PlayerScore(EpisodeState s) => s.competitionScores.Single(x => x.contestantId == s.playerId).score;

        private static GameSense.Report Sense(EpisodeState s, int tuning)
        {
            var copy = s.Clone();
            copy.tuningRulesVersion = tuning;
            return GameSense.Evaluate(copy);
        }

        private static List<string> Npcs(EpisodeState s) => s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();

        private static EpisodeCommand Study(EpisodeState s, string approach = "memorize-layout") => Command(s, EpisodeCommandKind.StudyHouse, approach);

        private static EpisodeCommand Answer(EpisodeState s, string throughId, string nomineeId)
        {
            var c = Command(s, EpisodeCommandKind.AnswerPactPlan, throughId);
            c.secondTargetId = nomineeId;
            c.text = PactPlanTests.PactId;
            return c;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null) => new EpisodeCommand
        {
            id = "tuning-reload-" + s.revision + "-" + kind + "-" + target, expectedRevision = s.revision, expectedPhase = s.phase,
            actorId = s.playerId, kind = kind, targetId = target,
        };

        private static void Accept(EpisodeEngine engine, EpisodeCommand command)
        {
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + ": " + result.reason);
        }

        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);

        private static string Json(object value) => JsonConvert.SerializeObject(value);
    }
}
