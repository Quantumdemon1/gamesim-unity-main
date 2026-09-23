using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The season's votes, read back out of the public record for the notebook's vote page.
    ///
    /// <para>The page used to read the current week's ballot box, which the engine empties when the
    /// next week begins - so from week two it said nobody had voted, over a season whose first vote
    /// had sent somebody to the jury.</para>
    /// </summary>
    public sealed class VoteRecordsTests
    {
        [Test]
        public void BeforeAnyEvictionThereIsNothingAndSayingSoIsTrue()
        {
            var book = VoteRecords.Read(ContentCatalog.Create(4u));
            Assert.That(book.NoEvictionYet, Is.True);
            Assert.That(book.Records, Is.Empty);
            Assert.That(book.Missing, Is.Zero);
        }

        /// <summary>The regression: week two's competition, the ballot box empty, week one's vote still on the page.</summary>
        [Test]
        public void WeekOnesVoteIsStillThereOnceWeekTwoBegins()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(5u));
            for (int guard = 0; guard < 200 && !(engine.Snapshot.week == 2 && engine.Snapshot.phase == EpisodePhase.HoH); guard++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var state = engine.Snapshot;
            Assume.That(state.week, Is.EqualTo(2));
            Assert.That(state.votes, Is.Empty, "Precondition: the engine has emptied the ballot box.");

            var book = VoteRecords.Read(state);
            Assert.That(book.NoEvictionYet, Is.False, "Somebody has been evicted.");
            var week1 = book.Records.Single(r => r.Week == 1);
            var juror = state.contestants.Single(c => c.status == ContestantStatus.Jury);
            Assert.That(week1.Complete, Is.True);
            Assert.That(week1.EvictedName, Is.EqualTo(juror.name));
            Assert.That(week1.Ballots, Is.Not.Empty);
            Assert.That(week1.Counts.Sum(t => t.Votes), Is.EqualTo(week1.Ballots.Count(b => !b.TieBreak)));
            Assert.That(book.VoteInProgress, Is.False);
        }

        [Test]
        public void EveryWeekOfAPlayedSeasonReadsAsItsPublicRecordSays()
        {
            var state = PlayedSeason(5u);
            var book = VoteRecords.Read(state);
            Assert.That(book.Records.Select(r => r.Week), Is.Ordered.Descending, "Newest first.");
            foreach (var record in book.Records)
            {
                var reveals = state.events.Where(e => e.week == record.Week && e.kind == "vote-reveal").ToList();
                Assert.That(record.Ballots.Count, Is.EqualTo(reveals.Count), "week " + record.Week + ": every published ballot reads.");
                if (!record.Complete) continue;
                var gone = state.events.First(e => e.week == record.Week && (e.kind == "eviction" || e.kind == "final-eviction"));
                Assert.That(record.EvictedName, Is.Not.Null.And.Not.Empty, gone.text);
                Assert.That(state.contestants.Single(c => c.name == record.EvictedName).status,
                    Is.EqualTo(ContestantStatus.Jury).Or.EqualTo(ContestantStatus.Evicted), gone.text);
                if (record.FinalDecision) { Assert.That(record.Ballots, Is.Empty, "The final Head of Household decides alone."); continue; }
                Assert.That(record.Counts.Sum(t => t.Votes), Is.EqualTo(record.Ballots.Count(b => !b.TieBreak)));
                // The evictee drew the most votes, or the tie was broken for them.
                if (!record.TieBroken) Assert.That(record.Counts.First().Name, Is.EqualTo(record.EvictedName), gone.text);
                else Assert.That(record.Ballots.Single(b => b.TieBreak).TargetName, Is.EqualTo(record.EvictedName));
            }
            int evictions = state.contestants.Count(c => c.status == ContestantStatus.Jury || c.status == ContestantStatus.Evicted);
            Assert.That(book.Records.Count(r => r.Complete) + book.Missing, Is.EqualTo(evictions),
                "Every eviction is either on the record or counted as missing from it.");
            if (book.Missing == 0)
                Assert.That(book.Records.Any(r => r.FinalDecision), Is.True, "A finished season ends with the final Head of Household's choice.");
        }

        /// <summary>The ballots in a vote not yet revealed: only the player's own, never the house's.</summary>
        [Test]
        public void AnUnrevealedVoteShowsOnlyThePlayersOwnBallot()
        {
            var state = ContentCatalog.Create(6u);
            var others = state.contestants.Where(c => !c.isPlayer).Take(3).ToArray();
            state.phase = EpisodePhase.Eviction; state.evictionResolved = false;
            state.votes.Add(new VoteState { voterId = others[2].id, targetId = others[0].id, reason = "A private reason." });
            var book = VoteRecords.Read(state);
            Assert.That(book.VoteInProgress, Is.True);
            Assert.That(book.OwnPendingBallot, Is.Null, "The player has not voted; another houseguest's ballot is not theirs to read.");

            state.votes.Add(new VoteState { voterId = state.playerId, targetId = others[1].id, reason = VoteRecords.PlayerReason });
            book = VoteRecords.Read(state);
            Assert.That(book.OwnPendingBallot, Is.Not.Null);
            Assert.That(book.OwnPendingBallot.TargetName, Is.EqualTo(others[1].name));
            Assert.That(book.Records, Is.Empty, "Nothing is revealed, so nothing is on the record.");
        }

        [Test]
        public void AWeekWhoseEvictionLineHasRolledOffIsMissingAndCarriesNoTally()
        {
            var state = PlayedSeason(5u);
            int week = state.events.First(e => e.kind == "eviction").week;
            int before = VoteRecords.Read(state).Missing;
            state.events.RemoveAll(e => e.week == week && e.kind == "eviction");
            var book = VoteRecords.Read(state);
            Assert.That(book.Missing, Is.EqualTo(before + 1));
            var week1 = book.Records.SingleOrDefault(r => r.Week == week);
            if (week1 != null)
            {
                Assert.That(week1.Complete, Is.False);
                Assert.That(week1.Counts, Is.Empty, "Ballots without their eviction line are a fragment: no tally is invented.");
                Assert.That(week1.EvictedName, Is.Null);
            }
        }

        [Test]
        public void ABallotReadsWhoVotedForWhomAndWhy()
        {
            var state = ContentCatalog.Create(7u);
            var player = state.Find(state.playerId);
            var a = state.contestants.First(c => !c.isPlayer);
            var b = state.contestants.Last(c => !c.isPlayer);

            var own = VoteRecords.ReadBallot(state, player.name + " voted to evict " + a.name + ". " + VoteRecords.PlayerReason);
            Assert.That(own.ByPlayer, Is.True);
            Assert.That(own.TargetName, Is.EqualTo(a.name));
            Assert.That(own.Reason, Is.Null, "The engine's placeholder says nothing to the player.");

            var against = VoteRecords.ReadBallot(state, a.name + " voted to evict you. We never really talked.");
            Assert.That(against.AgainstPlayer, Is.True);
            Assert.That(against.TargetName, Is.EqualTo(player.name));
            Assert.That(against.Reason, Is.EqualTo("We never really talked."));

            var tie = VoteRecords.ReadBallot(state, b.name + " voted to evict " + a.name + ". HoH tie-break: Loyalty first.");
            Assert.That(tie.TieBreak, Is.True);
            Assert.That(tie.Reason, Is.EqualTo("Loyalty first."));

            Assert.That(VoteRecords.ReadBallot(state, "Somebody else voted to evict nobody."), Is.Null);
            Assert.That(VoteRecords.ReadBallot(state, null), Is.Null);
        }

        [Test]
        public void ReadingTheRecordChangesNothing()
        {
            var state = PlayedSeason(9u);
            string before = JsonUtility.ToJson(state);
            VoteRecords.Read(state);
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
        }

        private static EpisodeState PlayedSeason(uint seed)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            for (int guard = 0; guard < 400 && engine.Snapshot.phase != EpisodePhase.Finished; guard++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished));
            return engine.Snapshot;
        }
    }
}
