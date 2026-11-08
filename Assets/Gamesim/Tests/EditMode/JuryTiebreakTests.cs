using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// B9's round 4, item 1 (Assets/Plans/B9-BALANCE-PLAN.md): the jury's tiebreak. The owner's call 3 of 2026-11-07 fixes
    /// the player's systematic loss of jury ties for new rules. At tuning 0 a tie still goes to the second finalist in cast
    /// order, with its old line. At tuning 1 it goes to the season's keyed coin (<see cref="TuningRules.JuryTieWinner"/>):
    /// fair to either finalist, the same in any cast order, drawing nothing from the season's stream, and re-derived by
    /// validation. The final Head of Household's read of the jury does not read the coin: at tuning 1 it counts a tie as half
    /// a win, whatever the seed, so a houseguest's choice cannot follow a coin the player never sees. Unity-free, so the dotnet subset
    /// runs it; <c>JuryTiebreakEditorTests</c> (the editor's only) carries the real store's round trip and the finale panel's line.
    /// </summary>
    public sealed class JuryTiebreakTests
    {
        internal const string Maya = ContentCatalog.MayaId;

        /// <summary>Tuning 0's line for a tied jury, as every season before B9 wrote it.</summary>
        internal const string SourceTieLine = "Jury tie: the source game's tie rule awards the win to the second finalist in cast order.";

        // ---------------------------------------------------------------- tuning 0, as before

        [Test]
        public void UnderTuningNoughtATieGoesToTheSecondFinalistInCastOrderWithItsOldLine()
        {
            foreach (bool mayaFirst in new[] { false, true })
            {
                // Seed 3's coin gives the player the win: with the player first in the cast, tuning 0 still gives it to Maya.
                var jury = TiedJury(3u, TuningRules.Off, mayaFirst);
                string second = jury.Active.Last().id, first = jury.Active.First().id;
                Assert.That(second, Is.EqualTo(mayaFirst ? jury.playerId : Maya), "Fixture: the cast order.");
                var done = Resolve(jury);
                Assert.That((done.winnerId, done.runnerUpId), Is.EqualTo((second, first)), "Maya first: " + mayaFirst);
                var added = NewEvents(jury, done);
                Assert.That(added.Where(e => e.kind == "jury-tie").Select(e => e.text), Is.EqualTo(new[] { SourceTieLine }));
                Assert.That(added.FindIndex(e => e.kind == "jury-tie"), Is.LessThan(added.FindIndex(e => e.kind == "winner")),
                    "The tie's line comes before the crowning, as it always did.");
            }
        }

        // ---------------------------------------------------------------- tuning 1, the coin

        [Test]
        public void UnderTuningOneATieGoesToTheSeasonsKeyedCoinWithALineOfItsOwn()
        {
            // Recorded with the rule: seed 1's coin gives Maya the win, seed 3's the player.
            Assert.That(TuningRules.JuryTieWinner(1u, ContentCatalog.PlayerId, Maya), Is.EqualTo(Maya));
            Assert.That(TuningRules.JuryTieWinner(3u, ContentCatalog.PlayerId, Maya), Is.EqualTo(ContentCatalog.PlayerId));
            int differ = 0;
            for (uint seed = 1; seed <= 12; seed++)
            {
                var jury = TiedJury(seed, TuningRules.Bands);
                string coin = TuningRules.JuryTieWinner(seed, jury.playerId, Maya);
                var done = Resolve(jury);
                Assert.That(done.winnerId, Is.EqualTo(coin), "seed " + seed);
                Assert.That(done.runnerUpId, Is.EqualTo(coin == Maya ? jury.playerId : Maya), "seed " + seed);
                Assert.That(done.Find(coin).status, Is.EqualTo(ContestantStatus.Winner));
                var added = NewEvents(jury, done);
                Assert.That(added.Where(e => e.kind == "jury-tie").Select(e => e.text), Is.EqualTo(new[]
                {
                    "Jury tie: the season's tiebreak draw gives the win to " + (coin == jury.playerId ? "you" : "Maya Hassan") + ".",
                }), "seed " + seed);
                // The same lines in the same order as tuning 0 writes for the same jury: only the tie's words differ.
                var old = Resolve(TiedJury(seed, TuningRules.Off));
                Assert.That(added.Select(e => e.kind), Is.EqualTo(NewEvents(jury, old).Select(e => e.kind)), "seed " + seed);
                if (done.winnerId != old.winnerId) differ++;
            }
            Assert.That(differ, Is.GreaterThan(0), "Some tie in twelve now goes to the player, first in the cast, where tuning 0 gave none.");
        }

        [Test]
        public void TheCoinIsFairToEitherFinalist()
        {
            int playerWins = 0;
            for (uint seed = 1; seed <= 400; seed++)
            {
                var done = Resolve(TiedJury(seed, TuningRules.Bands));
                Assert.That(done.winnerId, Is.EqualTo(TuningRules.JuryTieWinner(seed, done.playerId, Maya)), "seed " + seed);
                if (done.winnerId == done.playerId) playerWins++;
            }
            Assert.That(playerWins, Is.InRange(180, 220),
                "The player-side finalist wins 45-55% of 400 constructed ties (192 when the rule was written).");
            for (uint seed = 1; seed <= 20; seed++)
                Assert.That(Resolve(TiedJury(seed, TuningRules.Off)).winnerId, Is.EqualTo(Maya), "Tuning 0: the player, first in the cast, wins no tie.");

            // The coin alone, for 2,000 low seeds and 2,000 spread over the whole range (a director's seed is the clock's).
            foreach (var (a, b) in new[]
                     {
                         (ContentCatalog.PlayerId, Maya), ("alex-chen", "emma-brown"), ("a", "b"), (ContentCatalog.PlayerId, "quinn-martinez"),
                         ("taylor-kim", "casey-wilson"),
                     })
            {
                int low = 0, spread = 0;
                for (uint i = 0; i < 2000; i++)
                {
                    if (TuningRules.JuryTieWinner(i, a, b) == a) low++;
                    if (TuningRules.JuryTieWinner(unchecked(i * 2654435761u), a, b) == a) spread++;
                }
                Assert.That(low, Is.InRange(940, 1060), a + " / " + b + ", low seeds");
                Assert.That(spread, Is.InRange(940, 1060), a + " / " + b + ", spread seeds");
            }
        }

        [Test]
        public void CastOrderDoesNotMoveTheCoin()
        {
            for (uint seed = 1; seed <= 40; seed++)
            {
                var playerFirst = TiedJury(seed, TuningRules.Bands);
                var mayaFirst = TiedJury(seed, TuningRules.Bands, mayaFirst: true);
                var one = Resolve(playerFirst);
                var other = Resolve(mayaFirst);
                Assert.That(other.winnerId, Is.EqualTo(one.winnerId), "seed " + seed);
                Assert.That(NewEvents(mayaFirst, other).Single(e => e.kind == "jury-tie").text,
                    Is.EqualTo(NewEvents(playerFirst, one).Single(e => e.kind == "jury-tie").text), "seed " + seed);
                Assert.That(TuningRules.JuryTieWinner(seed, Maya, ContentCatalog.PlayerId),
                    Is.EqualTo(TuningRules.JuryTieWinner(seed, ContentCatalog.PlayerId, Maya)));
                Assert.That(TuningRules.JuryTiebreakHash(seed, Maya, ContentCatalog.PlayerId),
                    Is.EqualTo(TuningRules.JuryTiebreakHash(seed, ContentCatalog.PlayerId, Maya)));
                // The control: at tuning 0 the same swap moves the win, as the source's rule reads the cast.
                Assert.That(Resolve(TiedJury(seed, TuningRules.Off)).winnerId, Is.EqualTo(Maya));
                Assert.That(Resolve(TiedJury(seed, TuningRules.Off, mayaFirst: true)).winnerId, Is.EqualTo(ContentCatalog.PlayerId));
            }
        }

        [Test]
        public void TheTiebreakDrawsNothingFromTheSeasonsStream()
        {
            // Every ballot already in: neither tuning draws at all.
            foreach (int tuning in new[] { TuningRules.Off, TuningRules.Bands })
            {
                var cast = TiedJury(3u, tuning);
                Assert.That(Resolve(cast).randomState, Is.EqualTo(cast.randomState), "Tuning " + tuning + ": a tie the ballots made draws nothing.");
            }

            // The engine casting every ballot, each juror on the fence: two final impressions a juror at either tuning, so the
            // same ballots, the same stream after, and where the count tied, each tuning's own rule.
            int ties = 0;
            for (uint seed = 1; seed <= 40; seed++)
            {
                var open = OpenJury(seed, TuningRules.Off);
                var old = Resolve(open);
                var tuned = Resolve(OpenJury(seed, TuningRules.Bands));
                Assert.That(Ballots(tuned), Is.EqualTo(Ballots(old)), "seed " + seed + ": the same ballots.");
                Assert.That(tuned.randomState, Is.EqualTo(old.randomState), "seed " + seed + ": the same draws, and not one more.");
                Assert.That(old.randomState, Is.Not.EqualTo(open.randomState), "seed " + seed + ": the jurors drew their impressions.");
                int forPlayer = old.votes.Count(v => v.targetId == old.playerId), forMaya = old.votes.Count(v => v.targetId == Maya);
                Assert.That(forPlayer + forMaya, Is.EqualTo(4), "seed " + seed + ": four jurors.");
                if (forPlayer != forMaya)
                {
                    Assert.That(tuned.winnerId, Is.EqualTo(old.winnerId), "seed " + seed + ": a majority is a majority.");
                    continue;
                }
                ties++;
                Assert.That(old.winnerId, Is.EqualTo(Maya), "seed " + seed + ": tuning 0, the second finalist in cast order.");
                Assert.That(tuned.winnerId, Is.EqualTo(TuningRules.JuryTieWinner(seed, old.playerId, Maya)), "seed " + seed + ": tuning 1, the coin.");
            }
            Assert.That(ties, Is.GreaterThan(0), "Fixture: a jury on the fence ties two to two in some seed of forty.");
        }

        // ---------------------------------------------------------------- validation

        [Test]
        public void ValidationRefusesATuningOneSeasonWhoseTiedJuryCrownedTheOtherFinalist()
        {
            foreach (uint seed in new[] { 1u, 3u })
            {
                var finished = Resolve(TiedJury(seed, TuningRules.Bands));
                Valid(finished);
                var forged = Swapped(finished);
                Refused(forged, TuningRules.JuryTieRefusal);
                // Held at tuning 0, the same season is what validation always admitted: tuning 0 is asked nothing new.
                var held = forged.Clone();
                held.tuningRulesVersion = TuningRules.Off;
                Valid(held);
                Valid(Swapped(Resolve(TiedJury(seed, TuningRules.Off))));
            }
        }

        // ---------------------------------------------------------------- the final Head of Household's read

        /// <summary>
        /// FinalChoiceTests' house on the fence (seed 337 is its own): with every juror, and either finalist cut, even
        /// between the two, the Head of Household wins three or four of four (5/16), or ties two to two (6/16). Tuning 0
        /// gives the tie by cast order, as ResolveJury does (Maya after the player, Taylor after Maya): 11/16 and 5/16.
        /// Tuning 1 counts the tie as half a win beside either finalist, 5/16 + 6/16 × 0.5 = 8/16, without reading the coin:
        /// the player never sees the coin before the jury, so a Head of Household who chose by it would leave the player
        /// beside them mostly where it goes against them. The two seeds' coins, recorded with the rule, fall both ways
        /// (seed 330's against Maya beside the player, seed 337's for her), and the read is the same for every seed.
        /// </summary>
        [TestCase(337u, true, true)]
        [TestCase(330u, false, true)]
        public void TheFinalHeadOfHouseholdReadsATieAsHalfAWinWithoutReadingTheCoin(uint seed, bool coinForThemBesidePlayer, bool coinForThemBesideOther)
        {
            var old = FinalThree(seed, TuningRules.Off, out string hoh, out string other);
            Assert.That(EpisodeEngine.FinalJuryChance(old, hoh, old.playerId), Is.EqualTo(11.0 / 16).Within(1e-9), "Tuning 0: the tie is Maya's, after the player.");
            Assert.That(EpisodeEngine.FinalJuryChance(old, hoh, other), Is.EqualTo(5.0 / 16).Within(1e-9), "Tuning 0: the tie is Taylor's, after Maya.");
            var tuned = FinalThree(seed, TuningRules.Bands, out hoh, out other);
            Assert.That(TuningRules.JuryTieWinner(seed, hoh, tuned.playerId) == hoh, Is.EqualTo(coinForThemBesidePlayer), "Fixture: the coin beside the player.");
            Assert.That(TuningRules.JuryTieWinner(seed, hoh, other) == hoh, Is.EqualTo(coinForThemBesideOther), "Fixture: the coin beside Taylor.");
            Assert.That(EpisodeEngine.FinalJuryChance(tuned, hoh, tuned.playerId), Is.EqualTo(8.0 / 16).Within(1e-9), "Tuning 1, beside the player: the tie at a half.");
            Assert.That(EpisodeEngine.FinalJuryChance(tuned, hoh, other), Is.EqualTo(8.0 / 16).Within(1e-9), "Tuning 1, beside Taylor: the tie at a half.");

            // The same house under other seeds, which move only the coin: the read never moves, whichever way the coin falls.
            int forThem = 0, againstThem = 0;
            for (uint coinSeed = 0; coinSeed < 64; coinSeed++)
            {
                var reseeded = tuned.Clone();
                reseeded.seed = coinSeed;
                foreach (string beside in new[] { reseeded.playerId, other })
                {
                    if (TuningRules.JuryTieWinner(coinSeed, hoh, beside) == hoh) forThem++;
                    else againstThem++;
                    Assert.That(EpisodeEngine.FinalJuryChance(reseeded, hoh, beside), Is.EqualTo(8.0 / 16).Within(1e-9), "seed " + coinSeed + ", beside " + beside);
                }
            }
            Assert.That((forThem > 0, againstThem > 0), Is.EqualTo((true, true)), "Fixture: the coin falls both ways in 128 pairs.");
        }

        // ---------------------------------------------------------------- the coin's own pins

        /// <summary>
        /// The coin, literally, for a handful of seeds and pairs (recorded with the rule by an independent reimplementation):
        /// its 64 bits and the winner. A change to the key, the hash, the byte order or the finalizer moves these, and with
        /// them who won every tied tuning-1 season ever saved. The pair is given in both orders.
        /// </summary>
        [TestCase(0u, "a", "b", "b", 0x07e9786d6e7dd5d9UL)]
        [TestCase(1u, "maya-hassan", "player", "maya-hassan", 0x3c37e1c8deec605eUL)]
        [TestCase(3u, "maya-hassan", "player", "player", 0x2711069a67e1b3c9UL)]
        [TestCase(20260910u, "alex-chen", "player", "player", 0xc08d47eb2383710bUL)]
        [TestCase(4294967295u, "casey-wilson", "taylor-kim", "taylor-kim", 0x70a6d28304b1175bUL)]
        [TestCase(7u, "emma-brown", "alex-chen", "emma-brown", 0xef6b5ed1279915f5UL)]
        [TestCase(12345u, "riley-johnson", "jamie-roberts", "jamie-roberts", 0x2fbc25bc3b221960UL)]
        public void TheCoinIsPinned(uint seed, string first, string second, string winner, ulong hash)
        {
            Assert.That(TuningRules.JuryTieWinner(seed, first, second), Is.EqualTo(winner));
            Assert.That(TuningRules.JuryTieWinner(seed, second, first), Is.EqualTo(winner));
            Assert.That(TuningRules.JuryTiebreakHash(seed, first, second), Is.EqualTo(hash));
            Assert.That(TuningRules.JuryTiebreakHash(seed, second, first), Is.EqualTo(hash));
            Assert.That(TuningRules.JuryTiebreakKey, Is.EqualTo("gamesim/tuning-1/jury-tiebreak"));
        }

        // ---------------------------------------------------------------- fixtures

        /// <summary>
        /// The catalogue's house of six at its jury, built rather than played (EpisodeFinaleTests' route): the player and
        /// Maya the final two, the player first in the cast, the other four on the jury with two ballots each way already
        /// cast, so the engine casts none and draws nothing. Competition rules 4, which tuning 1 needs. With
        /// <paramref name="mayaFirst"/> Maya sits ahead of the player in the cast.
        /// </summary>
        internal static EpisodeState TiedJury(uint seed, int tuning, bool mayaFirst = false) => Jury(seed, tuning, mayaFirst, ballots: true);

        /// <summary>The same jury with no ballot cast: the engine casts all four, each juror even between the two finalists.</summary>
        internal static EpisodeState OpenJury(uint seed, int tuning) => Jury(seed, tuning, false, ballots: false);

        private static EpisodeState Jury(uint seed, int tuning, bool mayaFirst, bool ballots)
        {
            var s = ContentCatalog.Create(seed);
            s.competitionRulesVersion = CompetitionRules.Widened;
            s.tuningRulesVersion = tuning;
            s.week = 4;
            s.phase = EpisodePhase.Jury;
            var jurors = s.contestants.Where(c => c.id != s.playerId && c.id != Maya).ToList();
            foreach (var juror in jurors) juror.status = ContestantStatus.Jury;
            s.hohId = Maya;
            s.finalPart1WinnerId = Maya;
            s.finalPart2WinnerId = s.playerId;
            // The same game behind both finalists, and every juror as warm on one as on the other: a ballot the engine casts
            // is its two final impressions alone.
            s.Find(Maya).stats.strategic = s.Find(s.playerId).stats.strategic;
            foreach (var juror in jurors)
            {
                SetScore(s, juror.id, s.playerId, 0);
                SetScore(s, juror.id, Maya, 0);
            }
            if (ballots)
                for (int i = 0; i < jurors.Count; i++)
                    s.votes.Add(new VoteState { voterId = jurors[i].id, targetId = i % 2 == 0 ? s.playerId : Maya, reason = "The fixture's ballot." });
            if (mayaFirst)
            {
                var maya = s.Find(Maya);
                s.contestants.Remove(maya);
                s.contestants.Insert(0, maya);
            }
            Valid(s);
            return s;
        }

        /// <summary>
        /// FinalChoiceTests' final three (Maya Head of Household, the player and Taylor beside her, Jamie, Casey and Riley on
        /// the jury) with every juror, and either finalist cut, even between the Head of Household and the one beside them.
        /// </summary>
        private static EpisodeState FinalThree(uint seed, int tuning, out string hoh, out string other)
        {
            var s = ContentCatalog.Create(seed);
            s.competitionRulesVersion = CompetitionRules.Widened;
            s.tuningRulesVersion = tuning;
            s.week = 4;
            s.phase = EpisodePhase.FinalEviction;
            foreach (var actor in s.contestants.Skip(3)) actor.status = ContestantStatus.Jury;
            hoh = s.contestants[1].id;
            other = s.contestants[2].id;
            s.hohId = hoh;
            s.finalPart1WinnerId = hoh;
            s.finalPart2WinnerId = other;
            foreach (var finalist in s.contestants.Take(3)) { finalist.traits = new List<string>(); finalist.stats.strategic = 6; }
            SetScore(s, hoh, s.playerId, 20);
            SetScore(s, hoh, other, 20);
            string head = hoh;
            foreach (var juror in s.contestants.Where(c => c.id != head))
                foreach (var finalist in s.contestants.Take(3))
                    if (juror.id != finalist.id) SetScore(s, juror.id, finalist.id, 0);
            EpisodeEngine.EnableCommitments(s);
            Valid(s);
            return s;
        }

        /// <summary>The jury resolved by the engine's own Advance: the season finished.</summary>
        internal static EpisodeState Resolve(EpisodeState jury)
        {
            var engine = new EpisodeEngine(jury);
            var result = engine.Apply(EpisodeEngineTests.Command(engine.Snapshot, EpisodeCommandKind.Advance));
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.phase, Is.EqualTo(EpisodePhase.Finished));
            return result.state;
        }

        /// <summary>A finished season with its winner and runner-up exchanged: a save that claims the other finalist won.</summary>
        internal static EpisodeState Swapped(EpisodeState finished)
        {
            var s = finished.Clone();
            string winner = s.winnerId, runnerUp = s.runnerUpId;
            s.winnerId = runnerUp;
            s.runnerUpId = winner;
            s.Find(runnerUp).status = ContestantStatus.Winner;
            s.Find(winner).status = ContestantStatus.RunnerUp;
            return s;
        }

        private static List<EpisodeEvent> NewEvents(EpisodeState before, EpisodeState after) =>
            after.events.Where(e => e.sequence >= before.nextSequence).ToList();

        private static List<string> Ballots(EpisodeState s) => s.votes.Select(v => v.voterId + ">" + v.targetId).ToList();

        private static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        internal static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);

        private static void Refused(EpisodeState s, string message)
        {
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.False, "Refused: " + message);
            Assert.That(error, Is.EqualTo(message));
        }
    }
}
