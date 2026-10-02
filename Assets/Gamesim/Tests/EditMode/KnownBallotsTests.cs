using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// What a ballot looks like to the player (UI-UX-PASS-PLAN B0): their own, the Head of
    /// Household's tie-break, what the count proves, what they were told and judged, what a save
    /// from before ballots went private read in the open - and nothing else. Hand-built weeks on the
    /// default cast, read by the one reader every screen goes through.
    /// </summary>
    public sealed class KnownBallotsTests
    {
        /// <summary>A six-house in its second week, with week one's count on the record: the first houseguest held the house, the second and third were on the block, the second went.</summary>
        private static EpisodeState PastWeek(int against, int others, uint seed = 31)
        {
            var s = ContentCatalog.Create(seed);
            s.week = 2;
            var npcs = Npcs(s);
            s.ledger.power.Add(new PowerRow
            {
                week = 1, hohId = npcs[0].id, evicteeId = against >= others ? npcs[1].id : npcs[2].id,
                nominees = new List<string> { npcs[1].id, npcs[2].id }, tally = new List<int> { against, others },
            });
            return s;
        }

        private static ContestantState[] Npcs(EpisodeState s) => s.contestants.Where(c => !c.isPlayer).ToArray();

        private static void Own(EpisodeState s, int week, string targetId) =>
            s.ledger.ballots.Add(new BallotRow { week = week, voterId = s.playerId, targetId = targetId });

        private static void Claim(EpisodeState s, int week, string voterId, string targetId, string source, string status) =>
            s.ledger.claims.Add(new ClaimRow { week = week, voterId = voterId, targetId = targetId, source = source, status = status });

        private static EpisodeEvent Line(EpisodeState s, int week, string kind, string text, params string[] audience) =>
            new EpisodeEvent { sequence = s.nextSequence++, week = week, phase = EpisodePhase.Eviction, kind = kind, text = text, audienceIds = audience.ToList() };

        [Test]
        public void TheWeeksVotersAreEverybodyButTheHeadOfHouseholdAndTheBlock()
        {
            var s = PastWeek(2, 1);
            var npcs = Npcs(s);
            var sheet = KnownBallots.Read(s, 1);
            Assert.That(sheet.Revealed, Is.True);
            Assert.That(sheet.voters, Is.EquivalentTo(new[] { s.playerId, npcs[3].id, npcs[4].id }), "Three vote in a six-house.");
            Assert.That(sheet.hohId, Is.EqualTo(npcs[0].id));
            Assert.That(sheet.evictedId, Is.EqualTo(npcs[1].id));
            Assert.That(sheet.Against(npcs[1].id), Is.EqualTo(2));
            Assert.That(sheet.Against(npcs[2].id), Is.EqualTo(1));
            Assert.That(sheet.ballots, Has.Count.EqualTo(3), "A slot for every ballot cast,");
            Assert.That(sheet.Known, Is.Zero, "none of them placed:");
            Assert.That(sheet.Unknown, Is.EqualTo(3));
            Assert.That(sheet.ballots.Select(b => b.voterId), Is.All.Null, "an unknown slot names nobody.");
        }

        [Test]
        public void YourOwnBallotIsKnownAndNothingElseIs()
        {
            var s = PastWeek(2, 1);
            var npcs = Npcs(s);
            Own(s, 1, npcs[1].id);
            var sheet = KnownBallots.Read(s, 1);
            var own = sheet.Of(s.playerId);
            Assert.That(own, Is.Not.Null);
            Assert.That((own.targetId, own.basis, own.Certain), Is.EqualTo((npcs[1].id, KnownBallots.Basis.Own, true)));
            Assert.That(sheet.ballots[0], Is.SameAs(own), "The player's own ballot leads the sheet.");
            Assert.That(sheet.Unknown, Is.EqualTo(2), "The other two are slots: a 2-1 count with one placed proves nothing about them.");
            Assert.That(sheet.Knows(npcs[3].id), Is.False);
            Assert.That(sheet.Knows(npcs[4].id), Is.False);
            Assert.That(KnownBallots.TargetOf(s, 1, npcs[3].id), Is.Null);
            Assert.That(KnownBallots.TargetOf(s, 1, s.playerId), Is.EqualTo(npcs[1].id));
        }

        [Test]
        public void TheHeadOfHouseholdsTieBreakIsPublic()
        {
            var s = ContentCatalog.Create(31);
            s.week = 2;
            var npcs = Npcs(s);
            // Week one's vote tied two all among four voters: the fifth houseguest was on the block
            // with the second, and the first, holding the house, sent the second home.
            s.ledger.power.Add(new PowerRow
            {
                week = 1, hohId = npcs[0].id, evicteeId = npcs[1].id,
                nominees = new List<string> { npcs[1].id, npcs[4].id }, tally = new List<int> { 2, 2 },
            });
            var sheet = KnownBallots.Read(s, 1);
            Assert.That(sheet.tieBroken, Is.True);
            var deciding = sheet.Of(npcs[0].id);
            Assert.That(deciding, Is.Not.Null, "The deciding vote is on the sheet,");
            Assert.That((deciding.targetId, deciding.basis), Is.EqualTo((npcs[1].id, KnownBallots.Basis.TieBreak)), "against the one who went, by its basis.");
            Assert.That(sheet.voters, Does.Not.Contain(npcs[0].id), "The Head of Household is not one of the house's voters,");
            Assert.That(sheet.Unknown, Is.EqualTo(4), "and the house's four ballots are slots.");
            Assert.That(sheet.ballots[0], Is.SameAs(deciding), "The tie-break leads a sheet with no ballot of the player's.");
        }

        [Test]
        public void AUnanimousCountProvesEveryBallot()
        {
            var s = PastWeek(3, 0);
            var npcs = Npcs(s);
            Own(s, 1, npcs[1].id);
            var sheet = KnownBallots.Read(s, 1);
            Assert.That(sheet.Unknown, Is.Zero);
            foreach (var id in new[] { npcs[3].id, npcs[4].id })
            {
                var proven = sheet.Of(id);
                Assert.That(proven, Is.Not.Null, id);
                Assert.That((proven.targetId, proven.basis, proven.Certain), Is.EqualTo((npcs[1].id, KnownBallots.Basis.Proven, true)));
            }
            Assert.That(sheet.Of(s.playerId).basis, Is.EqualTo(KnownBallots.Basis.Own), "The player's own stays their own.");
            Assert.That(sheet.ballots.Select(b => b.voterId), Is.EqualTo(new[] { s.playerId, npcs[3].id, npcs[4].id }), "Own first, then the cast's order.");

            // Without the player's row the count still proves the house's: nobody voted the other way.
            s.ledger.ballots.Clear();
            Assert.That(KnownBallots.Read(s, 1).ballots.Where(b => b.Known).Select(b => b.voterId),
                Is.EquivalentTo(new[] { s.playerId, npcs[3].id, npcs[4].id }), "Everybody who voted went the one way.");
        }

        [Test]
        public void TheLastBallotIsProvenOnceEveryOtherIsPlaced()
        {
            var s = PastWeek(2, 1);
            var npcs = Npcs(s);
            Own(s, 1, npcs[1].id);
            Claim(s, 1, npcs[3].id, npcs[2].id, ClaimSource.Told, ClaimStatus.Kept);
            var sheet = KnownBallots.Read(s, 1);
            var last = sheet.Of(npcs[4].id);
            Assert.That(last, Is.Not.Null, "The one ballot left is the count's remainder:");
            Assert.That((last.targetId, last.basis), Is.EqualTo((npcs[1].id, KnownBallots.Basis.Proven)));
            Assert.That(sheet.Unknown, Is.Zero);

            // A voter the record cannot place makes the list inexact, and the remainder is not drawn.
            s.contestants.Add(new ContestantState { id = "ghost", name = "Ghost Voter", status = ContestantStatus.Active });
            Assert.That(KnownBallots.Read(s, 1).Knows(npcs[4].id), Is.False, "With a fourth voter the count no longer places the last ballot,");
            Assert.That(KnownBallots.Read(s, 1).Knows(npcs[3].id), Is.True, "and what was told still stands.");
        }

        [Test]
        public void AToldClaimIsKnownAsItWasJudgedTrueOrFalse()
        {
            var s = PastWeek(2, 1);
            var npcs = Npcs(s);
            Claim(s, 1, npcs[3].id, npcs[1].id, ClaimSource.Told, ClaimStatus.Kept);
            Claim(s, 1, npcs[4].id, npcs[1].id, ClaimSource.Told, ClaimStatus.Lied);
            var sheet = KnownBallots.Read(s, 1);
            var truth = sheet.Of(npcs[3].id);
            Assert.That((truth.targetId, truth.saidId, truth.basis, truth.verdict, truth.Lied), Is.EqualTo((npcs[1].id, npcs[1].id, KnownBallots.Basis.Told, ClaimStatus.Kept, false)));
            var lie = sheet.Of(npcs[4].id);
            Assert.That((lie.targetId, lie.saidId, lie.basis, lie.verdict, lie.Lied), Is.EqualTo((npcs[2].id, npcs[1].id, KnownBallots.Basis.Told, ClaimStatus.Lied, true)),
                "A lie caught names the ballot the voter cast: the other nominee.");
            // The player's own row is not on this record; with the other two placed, a 2-1 count
            // leaves one vote and one voter, and the remainder places it.
            var mine = sheet.Of(s.playerId);
            Assert.That((mine.targetId, mine.basis), Is.EqualTo((npcs[1].id, KnownBallots.Basis.Proven)));
            Assert.That(sheet.Unknown, Is.Zero);
            Assert.That(sheet.ballots.Select(b => b.voterId), Is.EqualTo(new[] { s.playerId, npcs[3].id, npcs[4].id }), "The player first, then the cast's order.");
        }

        [Test]
        public void AnOverheardClaimAndAnAllysAccountCarryTheirOwnBasisWords()
        {
            var s = PastWeek(2, 1);
            var npcs = Npcs(s);
            Claim(s, 1, npcs[3].id, npcs[1].id, ClaimSource.Overheard, ClaimStatus.Kept);
            Claim(s, 1, npcs[4].id, npcs[2].id, ClaimSource.Ally, ClaimStatus.Kept);
            var sheet = KnownBallots.Read(s, 1);
            Assert.That(sheet.Of(npcs[3].id).basis, Is.EqualTo(KnownBallots.Basis.Overheard));
            Assert.That(sheet.Of(npcs[4].id).basis, Is.EqualTo(KnownBallots.Basis.Reported));
            Assert.That(KnownBallots.Basis.Word(KnownBallots.Basis.Overheard), Is.EqualTo("overheard"));
            Assert.That(KnownBallots.Basis.Word(KnownBallots.Basis.Reported), Is.EqualTo("an ally's account"));
            Assert.That(KnownBallots.Basis.Word(KnownBallots.Basis.Proven), Is.EqualTo("proven by the count"));
            Assert.That(KnownBallots.Basis.Word(KnownBallots.Basis.Told), Is.EqualTo("told you"));
            Assert.That(KnownBallots.Basis.Word(KnownBallots.Basis.Own), Is.EqualTo("your ballot"));
            Assert.That(KnownBallots.Basis.Word(KnownBallots.Basis.TieBreak), Is.EqualTo("the Head of Household's tie-break"));
            Assert.That(KnownBallots.Basis.Word(KnownBallots.Basis.Revealed), Is.EqualTo("read in the open"));
            Assert.That(KnownBallots.Basis.Word(KnownBallots.Basis.Unknown), Is.EqualTo("unknown"));
            Assert.That(KnownBallots.Basis.All.All(KnownBallots.Basis.IsKnown), Is.True);
        }

        [Test]
        public void AnOpenClaimPlacesNothing()
        {
            var s = PastWeek(2, 1);
            var npcs = Npcs(s);
            Claim(s, 1, npcs[3].id, npcs[1].id, ClaimSource.Told, ClaimStatus.Open);
            var sheet = KnownBallots.Read(s, 1);
            Assert.That(sheet.Of(npcs[3].id), Is.Null, "What they said, unjudged, is a read and not a ballot.");
            Assert.That(sheet.Unknown, Is.EqualTo(3));
        }

        [Test]
        public void ALineReadInTheOpenStaysKnown()
        {
            var s = PastWeek(2, 1);
            var npcs = Npcs(s);
            // A line given to its voter alone is a private one: it is not read as a public source,
            // and the player's own ballot comes from their row, not from it.
            s.events.Add(Line(s, 1, "vote-reveal", npcs[3].name + " voted to evict " + npcs[1].name + ". We never really talked.", npcs[3].id));
            Assert.That(KnownBallots.Read(s, 1).Known, Is.Zero, "A line private to its voter is nobody else's.");
            s.events.Clear();
            // A save from before ballots went private kept the reveal's public lines; and the house
            // is told when a loyalty oath breaks by a vote.
            s.events.Add(Line(s, 1, "vote-reveal", npcs[3].name + " voted to evict " + npcs[1].name + ". We never really talked."));
            s.events.Add(Line(s, 1, "loyalty_oath_broken", npcs[4].name + " broke their loyalty oath to " + npcs[2].name + " by voting to evict them!"));
            var sheet = KnownBallots.Read(s, 1);
            var read = sheet.Of(npcs[3].id);
            Assert.That((read.targetId, read.basis, read.reason), Is.EqualTo((npcs[1].id, KnownBallots.Basis.Revealed, "We never really talked.")));
            var oath = sheet.Of(npcs[4].id);
            Assert.That((oath.targetId, oath.basis), Is.EqualTo((npcs[2].id, KnownBallots.Basis.Revealed)));
            Assert.That(sheet.Of(s.playerId).basis, Is.EqualTo(KnownBallots.Basis.Proven), "Two of three read in the open: the count places the third.");
            Assert.That(sheet.Unknown, Is.Zero);

            var own = KnownBallots.ReadRevealLine(s, s.Find(s.playerId).name + " voted to evict " + npcs[1].name + ". " + KnownBallots.PlayerReason);
            Assert.That((own.voterId, own.targetId, own.basis, own.reason), Is.EqualTo((s.playerId, npcs[1].id, KnownBallots.Basis.Own, (string)null)));
            var against = KnownBallots.ReadRevealLine(s, npcs[3].name + " voted to evict you. Nothing personal.");
            Assert.That((against.targetId, against.reason), Is.EqualTo((s.playerId, "Nothing personal.")));
            var tie = KnownBallots.ReadRevealLine(s, npcs[0].name + " voted to evict " + npcs[1].name + ". HoH tie-break: Loyalty first.");
            Assert.That((tie.basis, tie.reason), Is.EqualTo((KnownBallots.Basis.TieBreak, "Loyalty first.")));
            Assert.That(KnownBallots.ReadRevealLine(s, "Somebody else voted to evict nobody."), Is.Null);
            Assert.That(KnownBallots.ReadRevealLine(s, null), Is.Null);
        }

        [Test]
        public void TheLiveBoxIsCountedAndNeverRead()
        {
            var s = ContentCatalog.Create(31);
            var npcs = Npcs(s);
            s.phase = EpisodePhase.Eviction; s.evictionResolved = true;
            s.hohId = npcs[0].id; s.nominees = new List<string> { npcs[1].id, npcs[2].id };
            s.votes.Add(new VoteState { voterId = s.playerId, targetId = npcs[1].id, reason = KnownBallots.PlayerReason });
            s.votes.Add(new VoteState { voterId = npcs[3].id, targetId = npcs[2].id, reason = "private" });
            s.votes.Add(new VoteState { voterId = npcs[4].id, targetId = npcs[1].id, reason = "private" });
            var sheet = KnownBallots.Read(s, 1);
            Assert.That(sheet.Revealed, Is.True, "The box is the live week's record,");
            Assert.That(sheet.tally, Is.EqualTo(new[] { 2, 1 }), "counted,");
            Assert.That(sheet.evictedId, Is.EqualTo(npcs[1].id));
            Assert.That(sheet.Of(s.playerId).basis, Is.EqualTo(KnownBallots.Basis.Own));
            Assert.That(sheet.Knows(npcs[3].id), Is.False, "and never read for anybody else:");
            Assert.That(sheet.Knows(npcs[4].id), Is.False);
            Assert.That(sheet.Unknown, Is.EqualTo(2));

            // Before the reveal the sheet is pending: the player's own ballot and nothing about the count.
            s.evictionResolved = false;
            var pending = KnownBallots.Read(s, 1);
            Assert.That(pending.pending, Is.True);
            Assert.That(pending.Revealed, Is.False);
            Assert.That(pending.ballots.Select(b => b.voterId), Is.EqualTo(new[] { s.playerId }));
            Assert.That(pending.Unknown, Is.Zero, "No slot before the count is read.");
            s.votes.RemoveAll(v => v.voterId == s.playerId);
            Assert.That(KnownBallots.Read(s, 1).ballots, Is.Empty);
        }

        [Test]
        public void TheCountProvesTheSameForAnotherHouseguestsEyes()
        {
            var s = ContentCatalog.Create(31);
            var npcs = Npcs(s);
            s.phase = EpisodePhase.Eviction; s.evictionResolved = true;
            s.hohId = npcs[0].id; s.nominees = new List<string> { npcs[1].id, npcs[2].id };
            s.votes.Add(new VoteState { voterId = s.playerId, targetId = npcs[1].id });
            s.votes.Add(new VoteState { voterId = npcs[3].id, targetId = npcs[2].id });
            s.votes.Add(new VoteState { voterId = npcs[4].id, targetId = npcs[1].id });
            // The one who voted the other way sees a 2-1 count with their own vote the 1: the other two went against the evictee.
            var theirs = KnownBallots.ProvenFor(s, 1, npcs[3].id);
            Assert.That(theirs, Is.EquivalentTo(new Dictionary<string, string> { { npcs[3].id, npcs[2].id }, { s.playerId, npcs[1].id }, { npcs[4].id, npcs[1].id } }));
            // One of the two sees 2-1 with their own among the 2: the other two could have gone either way.
            var other = KnownBallots.ProvenFor(s, 1, npcs[4].id);
            Assert.That(other, Is.EquivalentTo(new Dictionary<string, string> { { npcs[4].id, npcs[1].id } }));
            // The evictee cast nothing and sees only the count: nothing.
            Assert.That(KnownBallots.ProvenFor(s, 1, npcs[1].id), Is.Empty);
            // The player's eyes are the sheet's.
            Assert.That(KnownBallots.ProvenFor(s, 1, s.playerId), Is.EquivalentTo(new Dictionary<string, string> { { s.playerId, npcs[1].id } }));

            // Unanimous, everybody can place everybody - the evictee included.
            s.votes[1].targetId = npcs[1].id;
            Assert.That(KnownBallots.ProvenFor(s, 1, npcs[1].id).Keys, Is.EquivalentTo(new[] { s.playerId, npcs[3].id, npcs[4].id }));
            // Nothing before the count is read.
            s.evictionResolved = false;
            Assert.That(KnownBallots.ProvenFor(s, 1, npcs[3].id), Is.Empty);
        }

        [Test]
        public void TheFinalEvictionHasNoBallots()
        {
            var s = ContentCatalog.Create(31);
            s.week = 5;
            var npcs = Npcs(s);
            s.ledger.power.Add(new PowerRow { week = 4, hohId = npcs[0].id, evicteeId = npcs[1].id, nominees = new List<string> { npcs[1].id, npcs[2].id } });
            var sheet = KnownBallots.Read(s, 4);
            Assert.That(sheet.final, Is.True);
            Assert.That(sheet.ballots, Is.Empty);
            Assert.That(KnownBallots.Weeks(s), Is.Empty, "A choice is not a vote on the record.");
        }

        [Test]
        public void TheWeeksOnRecordAreTheCountedOnesTheJudgedOnesAndTheLiveBox()
        {
            var s = PastWeek(2, 1);
            Claim(s, 3, Npcs(s)[3].id, Npcs(s)[1].id, ClaimSource.Told, ClaimStatus.Kept);
            Claim(s, 4, Npcs(s)[3].id, Npcs(s)[1].id, ClaimSource.Told, ClaimStatus.Open);
            Assert.That(KnownBallots.Weeks(s), Is.EqualTo(new[] { 1, 3 }));
        }

        // ---------------------------------------------------------------- the verdicts the player is told

        [Test]
        public void AVotePromiseOrDealIsUnresolvedUntilThePartnersBallotIsKnown()
        {
            var s = PastWeek(2, 1);
            var npcs = Npcs(s);
            Own(s, 1, npcs[1].id);
            var promise = new PromiseState { id = "p", fromId = npcs[3].id, toId = s.playerId, targetId = npcs[1].id, kind = PromiseKind.Vote, status = PromiseStatus.Broken, week = 1, expiresWeek = 1 };
            s.promises.Add(promise);
            Assert.That(KnownBallots.PromiseSettledWeek(s, promise), Is.EqualTo(1));
            Assert.That(KnownBallots.PromiseOutcomeKnown(s, promise), Is.False, "They broke it by a ballot the player cannot place.");
            var deal = new DealState { id = "d", type = DealKind.VoteEvict, proposerId = npcs[3].id, recipientId = s.playerId, targetId = npcs[1].id, status = DealStatus.Fulfilled, week = 1, expiresWeek = 1 };
            s.deals.Add(deal);
            Assert.That(KnownBallots.DealSettledWeek(s, deal, npcs[3].id), Is.EqualTo(1));
            Assert.That(KnownBallots.DealOutcomeKnown(s, deal), Is.False, "Honoured by a ballot the player cannot place.");
            var block = new DealState { id = "b", type = DealKind.VoteTogether, proposerId = s.playerId, recipientId = npcs[4].id, status = DealStatus.Broken, week = 1, expiresWeek = 1 };
            s.deals.Add(block);
            Assert.That(KnownBallots.DealOutcomeKnown(s, block), Is.False, "A voting bloc that fell apart says how the other voted.");

            Claim(s, 1, npcs[3].id, npcs[2].id, ClaimSource.Told, ClaimStatus.Lied);
            Assert.That(KnownBallots.PromiseOutcomeKnown(s, promise), Is.True, "Told, and caught: the ballot is known, and so is the verdict.");
            Assert.That(KnownBallots.DealOutcomeKnown(s, deal), Is.True);
            Assert.That(KnownBallots.DealOutcomeKnown(s, block), Is.True, "With two of three placed the count proves the third.");

            // The player's own ballot settles a deal they broke themselves.
            var mine = new DealState { id = "m", type = DealKind.VoteSave, proposerId = s.playerId, recipientId = npcs[4].id, targetId = npcs[1].id, status = DealStatus.Broken, week = 1, expiresWeek = 1 };
            s.ledger.claims.Clear();
            Assert.That(KnownBallots.DealOutcomeKnown(s, mine), Is.True, "You voted to evict the one you agreed to keep: you broke it, and you know.");

            // Not the player's, not a vote's, or not settled: nothing to withhold.
            Assert.That(KnownBallots.PromiseOutcomeKnown(s, new PromiseState { fromId = npcs[3].id, toId = npcs[4].id, kind = PromiseKind.Vote, status = PromiseStatus.Broken, week = 1 }), Is.True);
            Assert.That(KnownBallots.PromiseOutcomeKnown(s, new PromiseState { fromId = npcs[3].id, toId = s.playerId, kind = PromiseKind.Safety, status = PromiseStatus.Broken, week = 1 }), Is.True);
            Assert.That(KnownBallots.PromiseOutcomeKnown(s, new PromiseState { fromId = npcs[3].id, toId = s.playerId, kind = PromiseKind.Vote, status = PromiseStatus.Active, week = 1 }), Is.True);
            Assert.That(KnownBallots.DealOutcomeKnown(s, new DealState { type = DealKind.SafetyAgreement, proposerId = npcs[3].id, recipientId = s.playerId, status = DealStatus.Broken, week = 1 }), Is.True);
        }

        [Test]
        public void ALineThatTellsAnUnknownBallotIsRecognised()
        {
            var s = PastWeek(2, 1);
            var npcs = Npcs(s);
            string you = s.Find(s.playerId).name, them = npcs[3].name;
            Assert.That(KnownBallots.TellsAnUnknownBallot(s, npcs[3].id, them + " broke a Vote promise.", 1), Is.True);
            Assert.That(KnownBallots.TellsAnUnknownBallot(s, npcs[3].id, them + " fulfilled a Vote promise.", 1), Is.True);
            Assert.That(KnownBallots.TellsAnUnknownBallot(s, npcs[3].id, them + " broke a vote to evict with " + you + ".", 1), Is.True);
            Assert.That(KnownBallots.TellsAnUnknownBallot(s, npcs[3].id, you + " and " + them + " fell out over their voting bloc.", 1), Is.True);
            Assert.That(KnownBallots.TellsAnUnknownBallot(s, npcs[3].id, you + " and " + them + " fell out over their voting block.", 1), Is.True,
                "A line written under the reference's spelling, before the word was corrected, tells the same ballot.");
            Assert.That(KnownBallots.TellsAnUnknownBallot(s, npcs[3].id, them + " broke a safety pact with " + you + ".", 1), Is.False, "A nomination is public.");
            Assert.That(KnownBallots.TellsAnUnknownBallot(s, npcs[3].id, them + " talked about me to somebody.", 1), Is.False);
            Claim(s, 1, npcs[3].id, npcs[1].id, ClaimSource.Told, ClaimStatus.Kept);
            Assert.That(KnownBallots.TellsAnUnknownBallot(s, npcs[3].id, them + " broke a Vote promise.", 1), Is.False, "Once the ballot is known the line tells nothing new.");
        }

        /// <summary>
        /// A vote deal with somebody who cast no ballot that week - the Head of Household with no tie
        /// to break, a nominee - was settled by the player's own ballot alone: theirs to know with
        /// nothing of the partner's to learn. A voter's side of one is their ballot, still theirs.
        /// </summary>
        [Test]
        public void ADealThePlayerSettledAloneIsKnownWithoutThePartnersBallot()
        {
            var s = PastWeek(2, 1);
            var npcs = Npcs(s);
            Own(s, 1, npcs[1].id);
            string hoh = npcs[0].id;
            var withHoh = new DealState { id = "h", type = DealKind.VoteEvict, proposerId = hoh, recipientId = s.playerId, targetId = npcs[1].id, status = DealStatus.Fulfilled, week = 1, expiresWeek = 1 };
            Assert.That(KnownBallots.DealSettledWeek(s, withHoh, hoh), Is.EqualTo(1), "Settled the week the player voted on it,");
            Assert.That(KnownBallots.Knows(s, 1, hoh), Is.False, "with no ballot of the Head of Household's to know:");
            Assert.That(KnownBallots.DealOutcomeKnown(s, withHoh), Is.True, "the player's own ballot fulfilled it.");
            var withNominee = new DealState { id = "n", type = DealKind.VoteSave, proposerId = npcs[2].id, recipientId = s.playerId, targetId = npcs[2].id, status = DealStatus.Fulfilled, week = 1, expiresWeek = 1 };
            Assert.That(KnownBallots.DealOutcomeKnown(s, withNominee), Is.True, "A nominee cast no ballot either.");
            var withVoter = new DealState { id = "v", type = DealKind.VoteEvict, proposerId = npcs[3].id, recipientId = s.playerId, targetId = npcs[1].id, status = DealStatus.Fulfilled, week = 1, expiresWeek = 1 };
            Assert.That(KnownBallots.DealOutcomeKnown(s, withVoter), Is.False, "A voter's side of it is their ballot, and a 2-1 count proves neither of the other two.");
        }

        /// <summary>The Head of Household's tie-break is a ballot the engine judges deals and promises by, and it is read live: a promise or a deal of theirs settles the week they broke the tie, and the player knows how.</summary>
        [Test]
        public void ATieBreakSettlesTheHeadOfHouseholdsPromiseAndDealInTheOpen()
        {
            var s = PastWeek(1, 1);
            var npcs = Npcs(s);
            string hoh = npcs[0].id;
            // Two house voters, so a 1-1 count is the whole house: the player and the fourth houseguest.
            npcs[4].status = ContestantStatus.Jury;
            Own(s, 1, npcs[2].id);
            var sheet = KnownBallots.Read(s, 1);
            Assert.That((sheet.tieBroken, sheet.voters.Count), Is.EqualTo((true, 2)));
            Assert.That(sheet.TargetOf(hoh), Is.EqualTo(npcs[1].id), "The deciding vote is read live,");
            Assert.That(sheet.TargetOf(npcs[3].id), Is.EqualTo(npcs[1].id), "and the player's own ballot proves the other voter's.");
            var promise = new PromiseState { id = "p", fromId = hoh, toId = s.playerId, targetId = npcs[1].id, kind = PromiseKind.Vote, status = PromiseStatus.Fulfilled, week = 1, expiresWeek = 1 };
            Assert.That(KnownBallots.PromiseSettledWeek(s, promise), Is.EqualTo(1), "A vote promise of the Head of Household's settled with the tie-break,");
            Assert.That(KnownBallots.PromiseOutcomeKnown(s, promise), Is.True, "in the open.");
            var block = new DealState { id = "b", type = DealKind.VoteTogether, proposerId = s.playerId, recipientId = hoh, status = DealStatus.Broken, week = 1, expiresWeek = 1 };
            Assert.That(KnownBallots.DealSettledWeek(s, block, hoh), Is.EqualTo(1), "So did a voting bloc with them,");
            Assert.That(KnownBallots.DealOutcomeKnown(s, block), Is.True, "known.");
            var evict = new DealState { id = "e", type = DealKind.VoteEvict, proposerId = hoh, recipientId = s.playerId, targetId = npcs[1].id, status = DealStatus.Broken, week = 1, expiresWeek = 1 };
            Assert.That(KnownBallots.DealOutcomeKnown(s, evict), Is.True, "The player's own ballot broke a vote to evict with them.");
        }

        /// <summary>An oath broken by a vote against the player was announced to the house; the breach on the player's own arc, in the oath rule's words, keeps the ballot known once the announcement has rolled off the log.</summary>
        [Test]
        public void AnOathsBreachOnThePlayersArcIsKnownOnceItsLineHasRolledOff()
        {
            var s = PastWeek(2, 1);
            var npcs = Npcs(s);
            var row = s.ledger.power[0];
            row.nominees = new List<string> { s.playerId, npcs[2].id };
            row.evicteeId = npcs[2].id;
            row.tally = new List<int> { 1, 2 };
            s.relationshipArcs.Add(new RelationshipArcState { npcId = npcs[3].id, npcName = npcs[3].name, weeklyHistory = new List<ArcHistory>
                { new ArcHistory { week = 1, delta = -20, reason = KnownBallots.OathBreachByVoteAgainstYou + "1" } } });
            Assert.That(s.events.Any(e => e.kind == "loyalty_oath_broken"), Is.False, "No line of the announcement is on the log,");
            var sheet = KnownBallots.Read(s, 1);
            var breach = sheet.Of(npcs[3].id);
            Assert.That(breach, Is.Not.Null, "and the breach is known from the arc:");
            Assert.That((breach.targetId, breach.basis, breach.Certain), Is.EqualTo((s.playerId, KnownBallots.Basis.Revealed, true)));
            Assert.That(sheet.Unknown, Is.Zero, "The breach placed, a 1-2 count leaves the other nominee's two votes to the two voters left: proven.");
            Assert.That(sheet.ballots.Where(b => b.voterId != npcs[3].id).Select(b => (b.targetId, b.basis)),
                Is.EquivalentTo(new[] { (npcs[2].id, KnownBallots.Basis.Proven), (npcs[2].id, KnownBallots.Basis.Proven) }));
            s.relationshipArcs[0].weeklyHistory[0].reason = "Broke loyalty oath by nominating you in week 1";
            Assert.That(KnownBallots.Knows(s, 1, npcs[3].id), Is.False, "A breach by a nomination tells no ballot,");
            Assert.That(KnownBallots.Read(s, 1).Unknown, Is.EqualTo(3), "and nothing is placed.");
        }

        /// <summary>The player's memories, less any that tells a ballot they do not know, for every reader that prints them.</summary>
        [Test]
        public void ThePlayersMemoriesLeaveOutTheOnesThatTellAnUnknownBallot()
        {
            var s = PastWeek(2, 1);
            var npcs = Npcs(s);
            string them = npcs[3].name;
            s.memories.Clear();
            s.memories.Add(new MemoryState { ownerId = s.playerId, subjectId = npcs[3].id, week = 1, text = them + " talked about me to somebody.", isPrivate = true });
            s.memories.Add(new MemoryState { ownerId = s.playerId, subjectId = npcs[3].id, week = 1, text = them + " broke a Vote promise.", isPrivate = true });
            s.memories.Add(new MemoryState { ownerId = npcs[3].id, subjectId = s.playerId, week = 1, text = "Theirs, not yours.", isPrivate = true });
            Assert.That(KnownBallots.PlayerMemories(s).Select(m => m.text), Is.EqualTo(new[] { them + " talked about me to somebody." }),
                "The verdict waits for the ballot; another's memory is never the player's.");
            Claim(s, 1, npcs[3].id, npcs[1].id, ClaimSource.Told, ClaimStatus.Kept);
            Assert.That(KnownBallots.PlayerMemories(s).Select(m => m.text), Is.EqualTo(new[] { them + " talked about me to somebody.", them + " broke a Vote promise." }),
                "Known, the memory reads, in the order it was written.");
            Assert.That(KnownBallots.PlayerMemories(null), Is.Empty);
        }

        [Test]
        public void ReadingChangesNothing()
        {
            var s = PastWeek(2, 1);
            var npcs = Npcs(s);
            Own(s, 1, npcs[1].id);
            Claim(s, 1, npcs[3].id, npcs[1].id, ClaimSource.Told, ClaimStatus.Kept);
            string before = JsonConvert.SerializeObject(s);
            KnownBallots.Read(s, 1);
            KnownBallots.ProvenFor(s, 1, npcs[4].id);
            KnownBallots.Weeks(s);
            Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(before));
            Assert.That(KnownBallots.Read(null, 1).ballots, Is.Empty);
            Assert.That(KnownBallots.Read(s, 0).ballots, Is.Empty);
            Assert.That(KnownBallots.ProvenFor(s, 1, "nobody"), Is.Empty);
        }
    }
}
