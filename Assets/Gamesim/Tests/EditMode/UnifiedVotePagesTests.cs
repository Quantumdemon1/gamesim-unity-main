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

        // ------------------------------------------------------------ the record and the log: the owner's line, gated

        /// <summary>
        /// The record and the log tell a Rule2 incident once, in its owner's line - the engine's write, which stays as it is (a
        /// line a row would re-mint every mode-2 season since V6). The owner's line can itself tell a ballot: the player proposed a
        /// vote to evict and kept it, so the reveal writes "You honoured a vote to evict with ..." to their log and to their record
        /// of the partner - written only where the partner's hidden ballot kept the deal too. Under mode 2 neither the player's
        /// record nor their log tells it while that ballot is hidden, as Your week and the Your word page do not: the flip pair
        /// reads alike. Once the player was told and the reveal judged it, the keep is theirs to read; mode 1 tells it, as its
        /// recorded seasons did.
        /// </summary>
        [Test]
        public void TheRecordAndTheLogTellAKeptVoteDealOnlyWhereThePartnersBallotIsKnown()
        {
            var blind = ModeTwoReaderSweep.Flip(false);
            int week = blind.Kept.week;
            string keep = Keep(blind.Kept, blind.PartnerId);
            Assert.That(KnownBallots.Knows(blind.Kept, week, blind.PartnerId), Is.False, "Fixture: the partner's ballot is hidden.");
            Assert.That(Log(blind.Kept, week, gated: false), Does.Contain(keep), "Fixture: the reveal wrote the keep to the player's log,");
            Assert.That(Record(blind.Kept, blind.PartnerId, week, gated: false).Select(entry => entry.Item1), Does.Contain(keep),
                "and to their record of the partner,");
            Assert.That(Log(blind.Broken, week, gated: false), Does.Not.Contain(keep), "and to neither where the partner broke it.");
            blind.AssertBlind("The player's log", s => Log(s, week, gated: true));
            blind.AssertBlind("The player's record of the partner", s => Record(s, blind.PartnerId, week, gated: true));
            blind.AssertBlind("Every line of the player's log", s => s.events.Where(e => e.week == week && Addressed(s, e)
                && !KnownBallots.TellsAnUnknownBallot(s, e) && e.kind != "deal-outcome" && e.kind != "promise-outcome").Count());

            var told = ModeTwoReaderSweep.Flip(true);
            Assert.That(KnownBallots.Knows(told.Kept, week, told.PartnerId), Is.True, "Fixture: the player was told, and the reveal judged it.");
            Assert.That(Log(told.Kept, week, gated: true), Does.Contain(Keep(told.Kept, told.PartnerId)), "The keep, theirs to read.");
            told.AssertControl("The player's log", s => Log(s, week, gated: true));
            told.AssertControl("The player's record of the partner", s => Record(s, told.PartnerId, week, gated: true));

            // The same reveal in mode 1 and its mode-2 twin: mode 1 tells the keep, as its recorded seasons did.
            var s0 = ModeTwoReaderSweep.Campaign();
            var voters = PinnedVoteSeason.NpcVoters(s0).ToList();
            string partner = voters[0], other = voters[1], x = s0.nominees[0], y = s0.nominees[1];
            s0 = ModeTwoReaderSweep.Strike(s0, partner, DealKind.VoteEvict, x);
            var reveal = ModeTwoReaderSweep.Reveal(s0, x, voters.ToDictionary(id => id, id => id == other ? y : x, StringComparer.Ordinal));
            Assert.That(KnownBallots.Knows(reveal.Mode1, week, partner) || KnownBallots.Knows(reveal.Mode2, week, partner), Is.False,
                "Fixture: the partner's ballot is hidden in both.");
            Assert.That(Log(reveal.Mode1, week, gated: true), Does.Contain(Keep(reveal.Mode1, partner)), "Mode 1's log tells the keep,");
            Assert.That(Record(reveal.Mode1, partner, week, gated: true).Select(entry => entry.Item1), Does.Contain(Keep(reveal.Mode1, partner)),
                "and so does its record.");
            Assert.That(Log(reveal.Mode2, week, gated: true), Does.Not.Contain(Keep(reveal.Mode2, partner)), "Mode 2's does not,");
            Assert.That(Record(reveal.Mode2, partner, week, gated: true).Select(entry => entry.Item1), Does.Not.Contain(Keep(reveal.Mode2, partner)),
                "nor its record.");
            Assert.That(KnownBallots.TellsAnUnknownBallot(reveal.Mode1, partner, Keep(reveal.Mode1, partner), week), Is.False, "Mode 1 is unchanged.");
        }

        /// <summary>
        /// Why dropping a Rule2 group's other lines tells nothing (vote family V7, the lead's question on the owners' lines): the
        /// player's own word on the vote - the one line of the incident mode 1 always shows them, written by their own ballot -
        /// owns every group it is in, since a vote promise outweighs every vote deal there is. The family's validation holds a
        /// vote deal to its kind's own trust (<see cref="DealKind.DefaultTrust"/>), and an accepted offer's breach is one step
        /// heavier (<see cref="DealResolution.BreachWeight"/>): the promise's keep and breach still weigh more, as the reveal's
        /// settlement ranks them. A change of weights that reversed this would let a hidden ballot decide
        /// whether the player's own line is written.
        /// </summary>
        [Test]
        public void TheWordOnTheVoteOutweighsEveryVoteDealSoItsOwnLineIsAlwaysWritten()
        {
            double kept = WebRules.PromiseImpact("vote", "fulfilled"), broken = WebRules.PromiseImpact("vote", "broken");
            foreach (string kind in new[] { DealKind.VoteTogether, DealKind.VoteSave, DealKind.VoteEvict })
            {
                string trust = DealKind.DefaultTrust(kind);
                Assert.That(kept, Is.GreaterThan(DealResolution.FulfilledBase * DealTrust.Weight(trust)), kind + ": a kept word outweighs a kept deal.");
                foreach (string weight in new[] { trust, DealTrust.Heavier(trust) })
                    Assert.That(broken, Is.LessThan(DealResolution.BrokenBase * DealTrust.Weight(weight)), kind + " at " + weight + ": a broken word is the more severe.");
            }
        }

        /// <summary>
        /// The other side of the gate: a keep the player's own ballot made alone - the partner cast none, as the Head of
        /// Household with no tie to break - tells nobody's ballot, and is shown in mode 2 as in mode 1.
        /// </summary>
        [Test]
        public void AKeepThePartnerCastNoBallotOnIsTheirsToRead()
        {
            var s = ModeTwoReaderSweep.Campaign();
            var voters = PinnedVoteSeason.NpcVoters(s).ToList();
            string hoh = s.hohId, x = s.nominees[0];
            Assert.That(hoh, Is.Not.Null.And.Not.EqualTo(s.playerId), "Fixture: a houseguest holds the house.");
            s = ModeTwoReaderSweep.Strike(s, hoh, DealKind.VoteEvict, x);
            var y = s.nominees[1];
            var reveal = ModeTwoReaderSweep.Reveal(s, x, voters.ToDictionary(id => id, id => id == voters[0] ? y : x, StringComparer.Ordinal));
            int week = reveal.Mode2.week;
            var sheet = KnownBallots.Read(reveal.Mode2, week);
            Assert.That(sheet.voters.Contains(hoh) || (sheet.tieBroken && sheet.hohId == hoh), Is.False, "Fixture: the partner cast no ballot.");
            foreach (var season in new[] { reveal.Mode2, reveal.Mode1 })
            {
                Assert.That(Log(season, week, gated: true), Does.Contain(Keep(season, hoh)), "The keep is the player's own ballot's.");
                Assert.That(KnownBallots.YourKeepTellsTheirBallot(season, hoh, Keep(season, hoh), week), Is.False);
            }
        }

        /// <summary>
        /// The consolidation check's D5 (the V7 review's V7-1): a partnership is decided by its voter alone, its partner the
        /// nominee, who casts no ballot (DealResolution.PartnershipAtTheVote). The player struck one with a nominee and kept it
        /// by their own ballot, so the keep tells nobody's ballot - Your week says so in mode 2 as mode 1 does, not "unresolved:
        /// you do not know how they voted" of a partner who never voted. The other side of the gate stays: a partnership the
        /// partner's ballot kept for the player is not theirs to know until they know that ballot.
        /// </summary>
        [Test]
        public void APartnershipThePlayerKeptByTheirOwnBallotIsTheirsToReadAsInModeOne()
        {
            var s = ModeTwoReaderSweep.Campaign();
            var voters = PinnedVoteSeason.NpcVoters(s).ToList();
            string partner = s.nominees[0], y = s.nominees[1];
            s = ModeTwoReaderSweep.Strike(s, partner, DealKind.Partnership);
            // The player votes the other nominee out, and so does the house: the partnership is kept by the player's ballot.
            var reveal = ModeTwoReaderSweep.Reveal(s, y, voters.ToDictionary(id => id, id => y, StringComparer.Ordinal));
            int week = reveal.Mode2.week;
            var sheet = KnownBallots.Read(reveal.Mode2, week);
            Assert.That(sheet.voters.Contains(partner) || (sheet.tieBroken && sheet.hohId == partner), Is.False, "Fixture: the partner cast no ballot.");
            string keep = reveal.Mode2.Find(reveal.Mode2.playerId).name + " honoured a " + DealKind.Title(DealKind.Partnership).ToLowerInvariant()
                + " with " + reveal.Mode2.Find(partner).name + ".";
            foreach (var season in new[] { reveal.Mode1, reveal.Mode2 })
                Assert.That(Record(season, partner, week, gated: true).Select(entry => entry.Item1), Does.Contain(keep),
                    "Fixture: the player's record of the partner tells the keep, in both modes.");

            string name = reveal.Mode2.Find(partner).name;
            var two = YourWeek.Build(reveal.Mode2, week).word;
            Assert.That(two.Select(line => (line.kind, line.verdict, line.byId, line.text)), Is.EqualTo(new[]
            {
                (YourWeek.Kinds.Deal, YourWeek.Verdicts.Kept, reveal.Mode2.playerId, "You kept the partnership with " + name + "."),
            }), "The player's own keep, theirs to read.");
            Assert.That(Json(two), Is.EqualTo(Json(YourWeek.Build(reveal.Mode1, week).word)), "As mode 1 lists it.");
            Assert.That(Json(YourWeek.Build(reveal.Projection, week)), Is.EqualTo(Json(YourWeek.Build(reveal.Mode2, week))),
                "The projection of mode 1's season reads as mode 2's.");
        }

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

        // ------------------------------------------------------------ reveal-order regression and detached record diagnostics

        /// <summary>
        /// Actual public mode-1 progression, with the walk's source-observed mode-2 projection: no ballot, draw,
        /// history or role is supplied by this regression. This is not a public mode-2 or native execution claim.
        /// </summary>
        [Test]
        public void Seed23ListsTheKeptVoteBeforeTheInformationBreachWithoutChangingEitherGraph()
        {
            var pair = InformationOrderWitness();
            string oneBefore = PinnedVoteSeason.Json(pair.One), twoBefore = PinnedVoteSeason.Json(pair.Two);
            var one = YourWeek.Build(pair.One, 1);
            var two = YourWeek.Build(pair.Two, 1);
            Assert.That(PinnedVoteSeason.Json(pair.One), Is.EqualTo(oneBefore), "Every field and collection in mode 1 is unchanged.");
            Assert.That(PinnedVoteSeason.Json(pair.Two), Is.EqualTo(twoBefore), "Every field and collection in the projection is unchanged.");
            var expected = new[]
            {
                (YourWeek.Kinds.Deal, YourWeek.Verdicts.Kept, "jordan-taylor", (string)null,
                    "You and Jordan Taylor held to your voting bloc."),
                (YourWeek.Kinds.Deal, YourWeek.Verdicts.Broken, "jordan-taylor", "jordan-taylor",
                    "Jordan Taylor broke the information deal with you."),
            };
            foreach (var week in new[] { one, two })
                Assert.That(week.word.Select(line => (line.kind, line.verdict, line.aboutId, line.byId, line.text)), Is.EqualTo(expected),
                    "Exactly the two known outcomes, in the source's reveal order; no comparison normalization.");
            Assert.That(Json(two), Is.EqualTo(Json(one)), "The complete week reads alike at this non-overlapping actual checkpoint.");
            var recorded = pair.One.relationships.Where(edge => edge.fromId == pair.One.playerId && edge.toId == "jordan-taylor")
                .SelectMany(edge => edge.events).Where(entry => entry.week == 1 && (entry.type == YourWeek.DealKept || entry.type == YourWeek.DealBroken))
                .OrderBy(entry => entry.sequence).ToList();
            Assert.That(recorded.Select(entry => (entry.sequence, entry.type)),
                Is.EqualTo(new[] { (75, YourWeek.DealKept), (85, YourWeek.DealBroken) }), "The actual records prove kept-before-broken.");
        }

        /// <summary>
        /// Detached reader-input diagnostic, not an accepted engine state: keeps the actual canonical rows and frame,
        /// but crafts raw records and one raw partnership to isolate pre-veto / merged vote / post-information ordering.
        /// </summary>
        [Test]
        public void DetachedMixedRecordsKeepPreVoteAndPostVoteOrderAroundTheMergedLane()
        {
            var original = InformationOrderWitness().Two;
            var s = DiagnosticOrderRecords(original);
            const string partner = "jordan-taylor";
            s.deals.Add(new DealState { id = "deal-reader-diagnostic-1", type = DealKind.Partnership, proposerId = s.playerId,
                recipientId = partner, status = DealStatus.Fulfilled, week = 1, settledWeek = 1, expiresWeek = 1 });
            // Deliberately inserted out of record sequence. The partnership's creation precedes the actual Vote row,
            // although its recorded outcome follows that row; the information outcomes must keep record order instead.
            AddOrderRecord(s, partner, 110, YourWeek.DealBroken, InformationRecord(s, partner, false, false));
            AddOrderRecord(s, partner, 80, YourWeek.DealKept, "You and Jordan Taylor held to their partnership.");
            AddOrderRecord(s, partner, 10, YourWeek.DealKept, "You honoured a veto commitment with Jordan Taylor.");
            AddOrderRecord(s, partner, 100, YourWeek.DealBroken, InformationRecord(s, partner, true, false));
            Assert.That(Json(s.unifiedCommitments), Is.EqualTo(Json(original.unifiedCommitments)), "No canonical row is fabricated or changed.");
            Assert.That(Json(s.unifiedVoteReveals), Is.EqualTo(Json(original.unifiedVoteReveals)), "The real reveal frame is retained unchanged.");
            var lines = ReadDiagnosticOrder(s);
            Assert.That(lines.Select(line => (line.verdict, line.byId, line.text)), Is.EqualTo(new[]
            {
                (YourWeek.Verdicts.Kept, s.playerId, "You kept the veto commitment with Jordan Taylor."),
                (YourWeek.Verdicts.Kept, (string)null, "You and Jordan Taylor held to your partnership."),
                (YourWeek.Verdicts.Kept, (string)null, "You and Jordan Taylor held to your voting bloc."),
                (YourWeek.Verdicts.Broken, s.playerId, "You broke the information deal with Jordan Taylor."),
                (YourWeek.Verdicts.Broken, partner, "Jordan Taylor broke the information deal with you."),
            }), "Pre-vote records, then the creation-ordered merged lane, then information breaches in record-sequence order.");
        }

        /// <summary>Detached malformed legacy-reader records must not acquire a supported source post-vote classification.</summary>
        [TestCase("unknown")]
        [TestCase("mismatched")]
        public void DetachedUnrecognisedOrInconsistentInformationRecordsKeepTheirOriginalPosition(string malformed)
        {
            var s = DiagnosticOrderRecords(InformationOrderWitness().Two);
            const string partner = "jordan-taylor";
            string text = malformed == "unknown" ? "Jordan Taylor gave an unrecognised information-deal ending."
                : InformationRecord(s, partner, false, false);
            string recordType = malformed == "unknown" ? YourWeek.DealBroken : YourWeek.DealKept;
            bool read = YourWeek.ReadDeal(s, text, partner, out _, out var kind, out var said);
            Assert.That(read, Is.EqualTo(malformed != "unknown"), "The control proves which parser boundary it exercises.");
            if (read)
            {
                Assert.That(kind, Is.EqualTo(DealKind.InformationSharing));
                Assert.That(said, Is.False, "The recognised sentence says broken while its record says kept.");
            }
            AddOrderRecord(s, partner, 100, recordType, text);
            var lines = ReadDiagnosticOrder(s);
            Assert.That(lines.Select(line => (line.verdict, line.byId, line.text)), Is.EqualTo(new[]
            {
                (malformed == "unknown" ? YourWeek.Verdicts.Broken : YourWeek.Verdicts.Kept, (string)null, text),
                (YourWeek.Verdicts.Kept, (string)null, "You and Jordan Taylor held to your voting bloc."),
            }), "Unknown or contradictory words remain exactly as recorded, in the ordinary pre-lane position.");
        }

        /// <summary>
        /// Detached record compatibility only: fulfilled information stays in the ordinary lane; genuinely fresh modes 0/1
        /// retain their raw sequence. These crafted outcome records are not claimed to be real settled production states.
        /// </summary>
        [Test]
        public void DetachedFulfilledInformationAndLegacyModesKeepTheirExistingRecordOrder()
        {
            var s = DiagnosticOrderRecords(InformationOrderWitness().Two);
            AddOrderRecord(s, "jordan-taylor", 100, YourWeek.DealKept, InformationRecord(s, "jordan-taylor", true, true));
            Assert.That(ReadDiagnosticOrder(s).Select(line => line.text), Is.EqualTo(new[]
            {
                "You kept the information deal with Jordan Taylor.",
                "You and Jordan Taylor held to your voting bloc.",
            }), "A consistent fulfilled information record is not moved into the post-vote list.");
            foreach (int mode in new[] { 0, 1 })
            {
                // The existing fresh helper validates the real empty opening before these diagnostic records are added.
                var legacy = DiagnosticOrderRecords(PinnedVoteSeason.Fresh(23, mode: mode, size: 6));
                string partner = legacy.contestants.First(c => !c.isPlayer).id, name = legacy.Find(partner).name;
                AddOrderRecord(legacy, partner, 30, YourWeek.DealKept, InformationRecord(legacy, partner, true, true));
                AddOrderRecord(legacy, partner, 10, YourWeek.DealBroken, InformationRecord(legacy, partner, false, false));
                AddOrderRecord(legacy, partner, 20, YourWeek.DealKept, "You honoured a veto commitment with " + name + ".");
                Assert.That(ReadDiagnosticOrder(legacy).Select(line => line.text), Is.EqualTo(new[]
                {
                    name + " broke the information deal with you.",
                    "You kept the veto commitment with " + name + ".",
                    "You kept the information deal with " + name + ".",
                }), "Mode " + mode + " preserves the original raw record-sequence order, not insertion order.");
            }
        }

        /// <summary>The unchanged busy public walker, stopped at the exact originally observed checkpoint.</summary>
        private static (EpisodeState One, EpisodeState Two) InformationOrderWitness()
        {
            EpisodeState one = null, two = null;
            var walked = ModeTwoReaderSweep.Walk(23, (mode1, mode2, where) =>
            {
                if (mode1.revision != 20) return;
                one = mode1.Clone(); two = mode2.Clone();
            }, size: 6, done: () => one != null);
            Assert.That(one, Is.Not.Null, "The original public walk must reach revision 20: " + walked);
            Assert.That(two, Is.Not.Null);
            Assert.That(walked.Commands, Is.EqualTo(20), "No alternate search or fabricated ballots replaces the original accepted-command sequence.");
            foreach (var state in new[] { one, two })
                Assert.That((state.week, state.phase, state.revision, state.evictionResolved),
                    Is.EqualTo((1, EpisodePhase.Eviction, 20, true)), "The actual week-1 reveal checkpoint.");
            Assert.That(one.unifiedCommitmentRulesVersion, Is.EqualTo(1));
            Assert.That(two.unifiedCommitmentRulesVersion, Is.EqualTo(2), "A source-observed projection, not a separately executed mode-2 season.");
            Assert.That(KnownBallots.Knows(two, 1, "jordan-taylor"), Is.True, "The regression's two endings are known.");
            Assert.That(two.unifiedCommitments.Count(row => row.kind == UnifiedVoteTogether.Vote && row.sourcePolicy == UnifiedCommitments.DealPolicy
                && row.subtype == DealKind.VoteTogether && row.status == DealStatus.Fulfilled && row.settledWeek == 1), Is.EqualTo(1));
            return (one, two);
        }

        /// <summary>
        /// Isolates a detached raw-record diagnostic. Never validates or installs the crafted state as an engine/save positive;
        /// the input graph, canonical rows, reveal frames and knowledge are not changed by this fixture preparation.
        /// </summary>
        private static EpisodeState DiagnosticOrderRecords(EpisodeState source)
        {
            string before = PinnedVoteSeason.Json(source);
            var s = source.Clone();
            foreach (var edge in s.relationships.Where(edge => edge.fromId == s.playerId))
                edge.events.RemoveAll(entry => entry.week == s.week && (entry.type == YourWeek.DealKept || entry.type == YourWeek.DealBroken));
            Assert.That(PinnedVoteSeason.Json(source), Is.EqualTo(before), "The genuine source witness is untouched.");
            Assert.That(Json(s.unifiedCommitments), Is.EqualTo(Json(source.unifiedCommitments)));
            Assert.That(Json(s.unifiedVoteReveals), Is.EqualTo(Json(source.unifiedVoteReveals)));
            return s;
        }

        private static void AddOrderRecord(EpisodeState s, string partner, int sequence, string type, string text)
        {
            var edge = s.relationships.SingleOrDefault(row => row.fromId == s.playerId && row.toId == partner);
            if (edge == null)
            {
                edge = new RelationshipState { fromId = s.playerId, toId = partner };
                s.relationships.Add(edge);
            }
            edge.events.Add(new RelationshipEventState { sequence = sequence, week = s.week, type = type, description = text });
        }

        private static string InformationRecord(EpisodeState s, string partner, bool playerActs, bool kept) =>
            (playerActs ? "You" : s.Find(partner).name) + (kept ? " honoured a " : " broke a ")
                + DealKind.Title(DealKind.InformationSharing).ToLowerInvariant() + " with " + (playerActs ? s.Find(partner).name : "You") + ".";

        private static List<YourWeek.Line> ReadDiagnosticOrder(EpisodeState s)
        {
            string before = PinnedVoteSeason.Json(s);
            var lines = YourWeek.Build(s, s.week).word;
            Assert.That(PinnedVoteSeason.Json(s), Is.EqualTo(before), "The complete detached graph is unchanged by the reader.");
            return lines;
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

        /// <summary>The engine's line for a vote to evict the player kept with <paramref name="partner"/> (EpisodeEngine.UnifiedVoteNamedRecipe).</summary>
        private static string Keep(EpisodeState s, string partner) =>
            s.Find(s.playerId).name + " honoured a " + DealKind.Title(DealKind.VoteEvict).ToLowerInvariant() + " with " + s.Find(partner).name + ".";

        /// <summary>Whether the log line is written to the player: to the house, or to them among its audience.</summary>
        private static bool Addressed(EpisodeState s, EpisodeEvent e) => e.audienceIds == null || e.audienceIds.Count == 0 || e.audienceIds.Contains(s.playerId);

        /// <summary>The deals' and promises' endings the player's log holds from <paramref name="week"/>, in order - as its readers print them where <paramref name="gated"/>.</summary>
        private static List<string> Log(EpisodeState s, int week, bool gated) =>
            s.events.Where(e => e.week == week && (e.kind == "deal-outcome" || e.kind == "promise-outcome") && Addressed(s, e)
                && !(gated && KnownBallots.TellsAnUnknownBallot(s, e))).Select(e => e.text).ToList();

        /// <summary>The player's record of <paramref name="partner"/> from <paramref name="week"/>: each entry's words and weight - as the web's history and the recap's sums read it where <paramref name="gated"/>.</summary>
        private static List<(string, double)> Record(EpisodeState s, string partner, int week, bool gated) =>
            s.relationships.Where(edge => edge.fromId == s.playerId && edge.toId == partner).SelectMany(edge => edge.events)
                .Where(e => e.week == week && !(gated && KnownBallots.TellsAnUnknownBallot(s, partner, e.description, e.week)))
                .Select(e => (e.description, e.impactScore)).ToList();

        private static string Json(object value) => ModeTwoReaderSweep.Json(value);
    }
}
