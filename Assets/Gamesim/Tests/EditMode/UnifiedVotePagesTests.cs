using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Vote family V7: the vote family's pages under the unified vote rules (mode 2), against their mode-1 twins. Your week
    /// (<see cref="YourWeek"/>) is an inventory of what ended, so it is read row by row as mode 1 reads it - a Vote deal or
    /// promise a reveal decided is a line of its own whether or not it owned its Rule2 group (the lead's decision D1 counts an
    /// incident once where a consequence is counted, as Game Sense and the finalist's record do) - behind the same knowledge
    /// gate. The Your word page (<see cref="CommitmentsRead"/>, <see cref="YourWord"/>) and the jury's receipts
    /// (<see cref="FinaleQuestions"/>) read the same fixtures as mode 1 does. Each fixture is a public mode-1 campaign, its
    /// reveal played through the public engine and through its mode-2 twin (<see cref="ModeTwoReaderSweep.Reveal"/>).
    /// </summary>
    public sealed class UnifiedVotePagesTests
    {
        // ------------------------------------------------------------ Your week: one line a row

        /// <summary>
        /// The review's note on D1's two counting rules: one known ballot breaks two of a pair's deals, one Rule2 incident. Only
        /// its owner wrote the record mode 1's line is read from, yet the week lists both deals, each as the player can know it,
        /// as mode 1 lists them - and the Your word page reads them alike.
        /// </summary>
        [Test]
        public void AKnownDealThatOwnedNoRule2GroupIsStillALineOfTheWeek()
        {
            var s = ModeTwoReaderSweep.Campaign();
            var voters = PinnedVoteSeason.NpcVoters(s).ToList();
            string partner = voters[0], x = s.nominees[0], y = s.nominees[1];
            s = ModeTwoReaderSweep.Strike(ModeTwoReaderSweep.Strike(s, partner, DealKind.VoteSave, x), partner, DealKind.VoteEvict, y);
            // Every houseguest votes x out, the partner too: the count tells their ballot, and both deals are theirs to break.
            var reveal = ModeTwoReaderSweep.Reveal(s, y, voters.ToDictionary(id => id, id => x, StringComparer.Ordinal));
            int week = reveal.Mode2.week;
            Assert.That(KnownBallots.Knows(reveal.Mode2, week, partner), Is.True, "Fixture: the count tells the partner's ballot.");
            var incident = UnifiedVoteHistory.Incidents(reveal.Mode2).Single();
            Assert.That((incident.ActorId, incident.WrongedId, incident.EvidenceIds.Count), Is.EqualTo((partner, reveal.Mode2.playerId, 2)),
                "Fixture: one Rule2 incident holds both deals.");
            Assert.That(Records(reveal.Mode2, partner, week, YourWeek.DealBroken), Is.EqualTo(1), "Fixture: only its owner wrote a record.");
            Assert.That(Records(reveal.Mode1, partner, week, YourWeek.DealBroken), Is.EqualTo(2), "Mode 1 wrote both.");

            string name = reveal.Mode2.Find(partner).name;
            var two = YourWeek.Build(reveal.Mode2, week).word;
            Assert.That(two.Select(line => (line.kind, line.verdict, line.byId, line.text)), Is.EqualTo(new[]
            {
                (YourWeek.Kinds.Deal, YourWeek.Verdicts.Broken, partner, name + " broke the vote-to-save deal with you."),
                (YourWeek.Kinds.Deal, YourWeek.Verdicts.Broken, partner, name + " broke the vote-to-evict deal with you."),
            }), "Both deals, each a line, in the order they were struck.");
            Assert.That(Json(two), Is.EqualTo(Json(YourWeek.Build(reveal.Mode1, week).word)), "As mode 1 lists them.");
            Assert.That(Json(YourWeek.Build(reveal.Projection, week)), Is.EqualTo(Json(YourWeek.Build(reveal.Mode2, week))),
                "The projection of mode 1's season reads as mode 2's.");
            AssertTheWordPageReadsAsModeOne(reveal);
            Assert.That(CommitmentsRead.Of(reveal.Mode2).Where(c => c.kind == CommitmentsRead.Kinds.Deal).Select(c => c.status),
                Is.EqualTo(new[] { "broken by them", "broken by them" }), "The page says who broke each.");
        }

        /// <summary>
        /// The plan's own case: one known ballot breaks the partner's word on the vote and a deal with the player. The word
        /// owns the incident (a vote promise's breach is the more severe), so the deal wrote nothing - yet the week lists the
        /// broken deal and the broken word, as mode 1 does.
        /// </summary>
        [Test]
        public void ADealTheBrokenWordsGroupOwnedIsStillALineOfTheWeek()
        {
            var s = ModeTwoReaderSweep.Campaign();
            var voters = PinnedVoteSeason.NpcVoters(s).ToList();
            string partner = voters[0], x = s.nominees[0], y = s.nominees[1];
            s = ModeTwoReaderSweep.Strike(s, partner, DealKind.VoteEvict, x);
            string word = Word(ref s, partner);
            // Every other houseguest votes y out, the partner too, and the player x: the count tells every ballot.
            var reveal = ModeTwoReaderSweep.Reveal(s, x, voters.ToDictionary(id => id, id => y, StringComparer.Ordinal));
            int week = reveal.Mode2.week;
            Assert.That(KnownBallots.Knows(reveal.Mode2, week, partner), Is.True, "Fixture: the count tells the partner's ballot.");
            var incident = UnifiedVoteHistory.Incidents(reveal.Mode2).Single();
            Assert.That((incident.OwnerId, incident.EvidenceIds.Count), Is.EqualTo((word, 2)), "Fixture: the word owns the incident.");
            Assert.That(Records(reveal.Mode2, partner, week, YourWeek.DealBroken), Is.Zero, "Fixture: the deal wrote nothing.");

            string name = reveal.Mode2.Find(partner).name;
            var two = YourWeek.Build(reveal.Mode2, week).word;
            Assert.That(two.Select(line => (line.kind, line.verdict, line.byId, line.text)), Is.EqualTo(new[]
            {
                (YourWeek.Kinds.Deal, YourWeek.Verdicts.Broken, partner, name + " broke the vote-to-evict deal with you."),
                (YourWeek.Kinds.Promise, YourWeek.Verdicts.Broken, partner, name + " broke their vote promise to you."),
            }), "The deal and the word, each a line.");
            Assert.That(Json(two), Is.EqualTo(Json(YourWeek.Build(reveal.Mode1, week).word)), "As mode 1 lists them.");
            AssertTheWordPageReadsAsModeOne(reveal);
        }

        /// <summary>
        /// A kept group: the player and the partner keep both of their deals, the player's ballot first in each, so the player
        /// is the keeper of both - one Rule2 fulfillment. Both are lines of the week, as mode 1 lists them.
        /// </summary>
        [Test]
        public void AKeptDealThatOwnedNoRule2GroupIsStillALineOfTheWeek()
        {
            var s = ModeTwoReaderSweep.Campaign();
            var voters = PinnedVoteSeason.NpcVoters(s).ToList();
            string partner = voters[0], x = s.nominees[0], y = s.nominees[1];
            s = ModeTwoReaderSweep.Strike(ModeTwoReaderSweep.Strike(s, partner, DealKind.VoteSave, x), partner, DealKind.VoteEvict, y);
            var reveal = ModeTwoReaderSweep.Reveal(s, y, voters.ToDictionary(id => id, id => y, StringComparer.Ordinal));
            int week = reveal.Mode2.week;
            Assert.That(KnownBallots.Knows(reveal.Mode2, week, partner), Is.True, "Fixture: the vote is unanimous.");
            var kept = UnifiedVoteHistory.Fulfillments(reveal.Mode2).Single();
            Assert.That((kept.ActorId, kept.EvidenceIds.Count), Is.EqualTo((reveal.Mode2.playerId, 2)), "Fixture: one kept group, the player's.");

            string name = reveal.Mode2.Find(partner).name;
            var two = YourWeek.Build(reveal.Mode2, week).word;
            Assert.That(two.Select(line => (line.verdict, line.byId, line.text)), Is.EqualTo(new[]
            {
                (YourWeek.Verdicts.Kept, reveal.Mode2.playerId, "You kept the vote-to-save deal with " + name + "."),
                (YourWeek.Verdicts.Kept, reveal.Mode2.playerId, "You kept the vote-to-evict deal with " + name + "."),
            }));
            Assert.That(Json(two), Is.EqualTo(Json(YourWeek.Build(reveal.Mode1, week).word)), "As mode 1 lists them.");
            AssertTheWordPageReadsAsModeOne(reveal);
        }

        /// <summary>
        /// The knowledge gate on the same rule: with the partner's ballot hidden, the deal and the word are each a not-known line
        /// whichever way that ballot went - so neither a line's presence nor its place says how they voted, though which row owned
        /// its Rule2 group differs between the two seasons.
        /// </summary>
        [Test]
        public void AHiddenBallotsRowsAreEachANotKnownLineWhicheverWayItWent()
        {
            var pair = ModeTwoReaderSweep.Flip(false, word: true);
            int week = pair.Kept.week;
            Assert.That(KnownBallots.Knows(pair.Kept, week, pair.PartnerId), Is.False, "Fixture: the partner's ballot is hidden.");
            Assert.That(UnifiedVoteHistory.Incidents(pair.Broken).Single().EvidenceIds.Count, Is.EqualTo(2), "Fixture: one incident holds both rows here,");
            Assert.That(UnifiedVoteHistory.Incidents(pair.Kept).Single().EvidenceIds.Count, Is.EqualTo(1), "and the word alone there.");
            foreach (var season in new[] { pair.Kept, pair.Broken })
            {
                var lines = YourWeek.Build(season, week).word;
                Assert.That(lines.Select(line => (line.kind, line.verdict, line.byId, line.text)), Is.EqualTo(new[]
                {
                    (YourWeek.Kinds.Deal, YourWeek.Verdicts.NotKnown, (string)null, "The vote-to-evict deal with " + season.Find(pair.PartnerId).name
                        + " is " + KnownBallots.Unresolved + "."),
                    (YourWeek.Kinds.Promise, YourWeek.Verdicts.NotKnown, (string)null, "Their vote promise to you is " + KnownBallots.Unresolved + "."),
                }));
            }
            pair.AssertBlind("YourWeek", state => YourWeek.Build(state, week));
        }

        // ------------------------------------------------------------ the jury's receipt

        /// <summary>
        /// The jury flow's receipt of the player's word: the player promised a voter their vote and broke it. In mode 2 that word
        /// is a canonical Vote row, the owner of its incident, and the voter - asked as a juror - has it as the accountability
        /// receipt, with the receipt line and the kicker mode 1 gives its raw promise; a promise kept to another voter is their
        /// personal receipt alike.
        /// </summary>
        [Test]
        public void TheJurysReceiptOfTheWordThePlayerBrokeOrKeptReadsAsModeOne()
        {
            var s = ModeTwoReaderSweep.Campaign();
            var voters = PinnedVoteSeason.NpcVoters(s).ToList();
            string wronged = voters[0], thanked = voters[1], x = s.nominees[0], y = s.nominees[1];
            s = Promised(Promised(s, wronged, x), thanked, y);
            // The player votes y out: the word to the first is broken, the word to the second kept.
            var reveal = ModeTwoReaderSweep.Reveal(s, y, voters.ToDictionary(id => id, id => y, StringComparer.Ordinal));
            var broken = reveal.Mode2.unifiedCommitments.Single(row => row.beneficiaryId == wronged && row.makerId == reveal.Mode2.playerId);
            var kept = reveal.Mode2.unifiedCommitments.Single(row => row.beneficiaryId == thanked && row.makerId == reveal.Mode2.playerId);
            Assert.That((broken.kind, broken.status, kept.status), Is.EqualTo((UnifiedVoteTogether.Vote, DealStatus.Broken, DealStatus.Fulfilled)),
                "Fixture: two canonical Vote promises of the player's, one broken and one kept.");

            foreach (var (juror, category, id, words) in new[]
            {
                (wronged, FinaleQuestions.Accountability, broken.id, "you gave them your word, and broke it"),
                (thanked, FinaleQuestions.Personal, kept.id, "you gave them your word, and kept it"),
            })
            {
                var receipt = FinaleQuestions.Receipts(reveal.Mode2, juror).Single(r => r.category == category);
                Assert.That((receipt.kind, receipt.id), Is.EqualTo((FinaleQuestions.PromiseReceipt, id)), category + ": the canonical word is the receipt.");
                var exchange = new JuryExchangeState { questionerId = juror, finalistId = reveal.Mode2.playerId, category = category,
                    receiptKind = receipt.kind, receiptId = receipt.id };
                Assert.That(FinaleQuestions.Kicker(reveal.Mode2, exchange), Is.EqualTo("Week " + reveal.Mode2.week + " · " + words), category);
                foreach (var mode1 in new[] { reveal.Mode1, reveal.Projection })
                {
                    Assert.That(Json(FinaleQuestions.Receipts(reveal.Mode2, juror)), Is.EqualTo(Json(FinaleQuestions.Receipts(mode1, juror))),
                        category + ": the receipts read as mode 1's.");
                    foreach (var read in new Func<EpisodeState, string>[] { state => FinaleQuestions.ReceiptLine(state, exchange),
                        state => FinaleQuestions.Kicker(state, exchange), state => Convert.ToString(FinaleQuestions.ReceiptWeek(state, exchange)) })
                        Assert.That(read(reveal.Mode2), Is.EqualTo(read(mode1)), category + ": the receipt's words read as mode 1's.");
                }
                Assert.That(Json(JuryHouseRead.ReadJuror(reveal.Mode2, juror)), Is.EqualTo(Json(JuryHouseRead.ReadJuror(reveal.Mode1, juror))),
                    category + ": the jury house reads the juror as mode 1 does.");
            }
        }

        // ------------------------------------------------------------ helpers

        /// <summary>The Your word page's model - every commitment, each one's line, the house's reading - reads the reveal as mode 1 does.</summary>
        private static void AssertTheWordPageReadsAsModeOne(ModeTwoReaderSweep.Revealed reveal)
        {
            foreach (var mode1 in new[] { reveal.Mode1, reveal.Projection })
            {
                Assert.That(Json(CommitmentsRead.Of(reveal.Mode2)), Is.EqualTo(Json(CommitmentsRead.Of(mode1))), "The Your word page's commitments.");
                Assert.That(CommitmentsRead.Of(reveal.Mode2).Select(CommitmentsRead.Line), Is.EqualTo(CommitmentsRead.Of(mode1).Select(CommitmentsRead.Line)),
                    "Each one's line.");
                Assert.That(new[] { YourWord.Title(reveal.Mode2), YourWord.Summary(reveal.Mode2) }.Concat(YourWord.Lines(reveal.Mode2)),
                    Is.EqualTo(new[] { YourWord.Title(mode1), YourWord.Summary(mode1) }.Concat(YourWord.Lines(mode1))), "The house's reading.");
            }
        }

        /// <summary>The partner's word on the vote to the player, as a story gives it (no target: any ballot of theirs breaks it), filed as <see cref="ModeTwoReaderSweep.Flip"/> files it.</summary>
        private static string Word(ref EpisodeState s, string partner)
        {
            string id = "promise-" + s.nextSequence;
            s.promises.Add(new PromiseState { id = id, fromId = partner, toId = s.playerId, kind = PromiseKind.Vote,
                status = PromiseStatus.Active, week = s.week, expiresWeek = s.week });
            s.nextSequence++;
            s = ProspectiveVoteTwins.Valid(s);
            return id;
        }

        /// <summary>The player's own word on the vote, through the real command: to <paramref name="voter"/>, to evict <paramref name="evict"/>.</summary>
        private static EpisodeState Promised(EpisodeState s, string voter, string evict)
        {
            var result = new EpisodeEngine(ProspectiveVoteTwins.Valid(s)).Apply(ProspectiveVoteTwins.Command(s, EpisodeCommandKind.PromiseVote, voter, evict));
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }

        /// <summary>How many records of a deal's ending of <paramref name="type"/> the player holds of <paramref name="partner"/> from <paramref name="week"/>.</summary>
        private static int Records(EpisodeState s, string partner, int week, string type) =>
            s.relationships.Where(edge => edge.fromId == s.playerId && edge.toId == partner)
                .SelectMany(edge => edge.events).Count(e => e.week == week && e.type == type);

        private static string Json(object value) => ModeTwoReaderSweep.Json(value);
    }
}
