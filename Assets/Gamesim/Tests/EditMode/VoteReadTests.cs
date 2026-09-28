using System;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The vote read (STRATEGY-LOOP-PLAN.md §2): a leaning built from the evaluator's own terms and
    /// stripped of what the player has not learned; the ways of learning; and the reveal judging what
    /// the player was told. Runs without Unity.
    /// </summary>
    public sealed class VoteReadTests
    {
        private static EpisodeEngine Reach(uint seed, Func<EpisodeState, bool> until, int limit = 600)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            for (int i = 0; i < limit && !until(engine.Snapshot); i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            Assert.That(until(engine.Snapshot), Is.True, "The season never reached the state the test needs.");
            return engine;
        }

        private static EpisodeEngine Campaign(uint seed) =>
            Reach(seed, s => s.phase == EpisodePhase.Campaign && s.nominees.Count == 2);

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string targetId, string secondTargetId = null) =>
            new EpisodeCommand
            {
                id = "read-" + kind + "-" + s.revision + "-" + Guid.NewGuid().ToString("N"), actorId = s.playerId, kind = kind,
                targetId = targetId, secondTargetId = secondTargetId, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        private static ContestantState[] NpcVoters(EpisodeState s) => EpisodeEngine.Voters(s).Where(v => !v.isPlayer).ToArray();

        [Test]
        public void TheReadIsUnknownUntilYouLearnSomething()
        {
            var s = Campaign(31).Snapshot;
            var sheet = VoteRead.Read(s);
            Assert.That(sheet.available, Is.True);
            Assert.That(sheet.voters, Has.Count.EqualTo(NpcVoters(s).Length));
            foreach (var read in sheet.voters)
            {
                Assert.That(read.confidence, Is.EqualTo(VoteRead.Unknown), read.voterId + ": nothing learned, nothing read.");
                Assert.That(read.unknownTerms, Is.GreaterThanOrEqualTo(1), read.voterId + " has terms the player cannot see.");
                Assert.That(read.leaningId, Is.Null);
            }
            Assert.That(sheet.predictedEvicteeId, Is.Null, "An unknown house predicts nothing.");
            Assert.That(sheet.unknown, Is.EqualTo(sheet.voters.Count));
        }

        [Test]
        public void AStandingYouLearnedMakesTheReadSpeak()
        {
            var s = Campaign(31).Snapshot;
            var voter = NpcVoters(s).First();
            foreach (var nominee in s.nominees)
                s.ledger.standings.Add(new StandingRow { week = s.week, fromId = voter.id, toId = nominee, source = ClaimSource.Overheard, score = s.Score(voter.id, nominee) });
            var read = VoteRead.Read(s).voters.Single(r => r.voterId == voter.id);
            Assert.That(read.confidence, Is.Not.EqualTo(VoteRead.Unknown));
            Assert.That(read.knownTerms, Does.Contain("relationship"));
            if (read.knownMargin >= VoteRead.LeaningMargin) Assert.That(s.nominees, Does.Contain(read.leaningId));
            else Assert.That(read.confidence, Is.EqualTo(VoteRead.Torn));
            // A standing from three weeks ago has gone stale, and the read says nothing again.
            foreach (var row in s.ledger.standings) row.week = s.week - VoteRead.StandingShelfLife - 1;
            Assert.That(VoteRead.Read(s).voters.Single(r => r.voterId == voter.id).confidence, Is.EqualTo(VoteRead.Unknown));
        }

        [Test]
        public void TheProjectionIsTheBallotWhenNothingChanges()
        {
            var engine = Reach(47, s => s.phase == EpisodePhase.Eviction && s.evictionStage == EvictionStage.Voting);
            var s = engine.Snapshot;
            if (EpisodeEngine.Voters(s).Any(v => v.isPlayer) && !s.votes.Any(v => v.voterId == s.playerId))
            {
                Assert.That(engine.Apply(Command(s, EpisodeCommandKind.CastVote, s.nominees[0])).accepted, Is.True);
                s = engine.Snapshot;
            }
            var projected = NpcVoters(s).ToDictionary(v => v.id, v => EpisodeEngine.ProjectBallot(s, v.id).selectedNomineeId);
            Assert.That(projected, Is.Not.Empty);
            for (int i = 0; i < 10 && !engine.Snapshot.votes.Any(v => v.voterId != engine.Snapshot.playerId); i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var after = engine.Snapshot;
            foreach (var pair in projected)
            {
                var ballot = after.votes.FirstOrDefault(v => v.voterId == pair.Key);
                Assert.That(ballot, Is.Not.Null, pair.Key + " voted.");
                Assert.That(ballot.targetId, Is.EqualTo(pair.Value), pair.Key + "'s ballot is the projection, because nothing changed between.");
            }
        }

        private static bool WillAnswer(EpisodeState s, ContestantState voter) =>
            !(voter.traits.Contains("Strategic") && !voter.traits.Contains("Loyal") && s.Score(voter.id, s.playerId) < 25);

        [Test]
        public void AskingStraightRecordsAClaimAndTheRevealJudgesIt()
        {
            for (uint seed = 1; seed <= 30; seed++)
            {
                var engine = Campaign(seed);
                var s = engine.Snapshot;
                var voter = NpcVoters(s).FirstOrDefault(v => WillAnswer(s, v));
                if (voter == null) continue;
                Assert.That(engine.Apply(Command(s, EpisodeCommandKind.AskVote, voter.id)).accepted, Is.True);
                s = engine.Snapshot;
                var claim = s.ledger.claims.Single(k => k.voterId == voter.id && k.week == s.week);
                Assert.That(claim.source, Is.EqualTo(ClaimSource.Told));
                Assert.That(claim.status, Is.EqualTo(ClaimStatus.Open));
                Assert.That(s.nominees, Does.Contain(claim.targetId));
                Assert.That(s.events.Last().kind, Is.EqualTo("vote-read"));
                Assert.That(VoteRead.Read(s).voters.Single(r => r.voterId == voter.id).saysId, Is.EqualTo(claim.targetId), "The read shows what they said.");
                Assert.That(s.socialActions, Is.EqualTo(engine.Snapshot.socialActions), "Asking is free.");

                var week = s.week;
                for (int i = 0; i < 40 && !(engine.Snapshot.evictionResolved || engine.Snapshot.week > week); i++)
                    Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
                var after = engine.Snapshot;
                var judged = after.ledger.claims.Single(k => k.voterId == voter.id && k.week == week);
                var ballot = after.votes.FirstOrDefault(v => v.voterId == voter.id) ?? after.events
                    .Where(e => e.kind == "vote-reveal" && e.text.StartsWith(voter.name + " voted", StringComparison.Ordinal)).Select(e => (VoteState)null).FirstOrDefault();
                Assert.That(judged.status, Is.Not.EqualTo(ClaimStatus.Open), "The reveal judged the claim.");
                if (judged.status == ClaimStatus.Lied)
                {
                    Assert.That(after.events.Any(e => e.kind == "vote-lie"), Is.True, "A lie to your face is in the log.");
                    Assert.That(after.relationships.Single(r => r.fromId == after.playerId && r.toId == voter.id).events.Any(e => e.type == "vote-lie"), Is.True);
                }
                return;
            }
            Assert.Fail("No seed produced a voter who would answer.");
        }

        [Test]
        public void AskingTwiceInAWeekIsRefused()
        {
            for (uint seed = 1; seed <= 30; seed++)
            {
                var engine = Campaign(seed);
                var s = engine.Snapshot;
                var voter = NpcVoters(s).FirstOrDefault(v => WillAnswer(s, v));
                if (voter == null) continue;
                Assert.That(engine.Apply(Command(s, EpisodeCommandKind.AskVote, voter.id)).accepted, Is.True);
                var again = engine.Apply(Command(engine.Snapshot, EpisodeCommandKind.AskVote, voter.id));
                Assert.That(again.accepted, Is.False);
                Assert.That(again.reason, Does.Contain("already asked"));
                return;
            }
            Assert.Fail("No seed produced a voter who would answer.");
        }

        /// <summary>A campaign whose first NPC voter wears exactly the traits given, on a fresh engine.</summary>
        private static (EpisodeEngine engine, ContestantState voter) VoterWith(uint seed, params string[] traits)
        {
            var s = Campaign(seed).Snapshot;
            var voter = NpcVoters(s).First();
            voter.traits = traits.ToList();
            return (new EpisodeEngine(s), voter);
        }

        [Test]
        public void ALoyalVoterTellsTheTruth()
        {
            for (uint seed = 1; seed <= 12; seed++)
            {
                var (engine, voter) = VoterWith(seed, "Loyal");
                var s = engine.Snapshot;
                string truth = EpisodeEngine.ProjectBallot(s, voter.id).selectedNomineeId;
                var result = engine.Apply(Command(s, EpisodeCommandKind.AskVote, voter.id));
                Assert.That(result.accepted, Is.True, result.reason);
                Assert.That(engine.Snapshot.ledger.claims.Single(k => k.voterId == voter.id).targetId, Is.EqualTo(truth),
                    "Seed " + seed + ": a Loyal houseguest says where their vote really is.");
            }
        }

        [Test]
        public void ASneakyVoterLiesSometimesAndNeverAlways()
        {
            int lies = 0, truths = 0;
            for (uint seed = 1; seed <= 40; seed++)
            {
                var (engine, voter) = VoterWith(seed, "Sneaky");
                var s = engine.Snapshot;
                string truth = EpisodeEngine.ProjectBallot(s, voter.id).selectedNomineeId;
                var result = engine.Apply(Command(s, EpisodeCommandKind.AskVote, voter.id));
                Assert.That(result.accepted, Is.True, result.reason);
                if (engine.Snapshot.ledger.claims.Single(k => k.voterId == voter.id).targetId == truth) truths++; else lies++;
            }
            Assert.That(lies, Is.GreaterThan(0), "Forty seeds and a Sneaky houseguest never lied.");
            Assert.That(truths, Is.GreaterThan(0), "Forty seeds and a Sneaky houseguest never told the truth.");
        }

        [Test]
        public void ReadingSomebodyRecordsHowTheySeeYouOnceAWeek()
        {
            for (uint seed = 1; seed <= 40; seed++)
            {
                var engine = Campaign(seed);
                var s = engine.Snapshot;
                var target = s.Active.First(c => !c.isPlayer);
                Assert.That(engine.Apply(Command(s, EpisodeCommandKind.ReadPerson, target.id)).accepted, Is.True);
                var after = engine.Snapshot;
                Assert.That(after.socialActions, Is.EqualTo(s.socialActions), "Reading is free.");
                Assert.That(after.events.Last().kind, Is.EqualTo("vote-read"));
                var row = after.ledger.standings.FirstOrDefault(r => r.fromId == target.id && r.toId == after.playerId && r.source == ClaimSource.Read);
                if (row == null) continue;
                Assert.That(row.score, Is.EqualTo(after.Score(target.id, after.playerId)), "What was learned is how they see you.");
                Assert.That(after.events.Last().text, Does.StartWith("You read " + target.name));
                var again = engine.Apply(Command(after, EpisodeCommandKind.ReadPerson, target.id));
                Assert.That(again.accepted, Is.False);
                Assert.That(again.reason, Does.Contain("already read"));
                return;
            }
            Assert.Fail("Forty seeds and never a successful read.");
        }

        [Test]
        public void ListeningInOnANamedPairWritesWhatYouOverheard()
        {
            for (uint seed = 1; seed <= 40; seed++)
            {
                var engine = Campaign(seed);
                var s = engine.Snapshot;
                var voters = NpcVoters(s);
                if (voters.Length < 2) continue;
                var result = engine.Apply(Command(s, EpisodeCommandKind.Eavesdrop, voters[0].id, voters[1].id));
                Assert.That(result.accepted, Is.True, result.reason);
                var after = engine.Snapshot;
                if (after.events.Last().text.Contains("caught you")) continue;
                Assert.That(after.events.Last().text, Does.StartWith("You overheard " + voters[0].name + " and " + voters[1].name), "The pair named is the pair overheard.");
                Assert.That(after.ledger.standings.Any(r => r.fromId == voters[0].id && r.toId == voters[1].id && r.source == ClaimSource.Overheard), Is.True);
                var claim = after.ledger.claims.SingleOrDefault(k => k.voterId == voters[0].id && k.source == ClaimSource.Overheard);
                Assert.That(claim, Is.Not.Null, "Two voters in a campaign talk about the vote.");
                Assert.That(after.events.Last().text, Does.Contain("is voting out " + after.Find(claim.targetId).name));
                return;
            }
            Assert.Fail("Forty seeds and always caught.");
        }

        [Test]
        public void YourBallotRecordsTheReadItWasCastOnAndLearnsWhetherItWasRight()
        {
            for (uint seed = 1; seed <= 20; seed++)
            {
                var engine = Reach(seed, s => s.phase == EpisodePhase.Eviction && s.evictionStage == EvictionStage.Voting);
                var s = engine.Snapshot;
                if (!EpisodeEngine.Voters(s).Any(v => v.isPlayer) || s.votes.Any(v => v.voterId == s.playerId)) continue;
                Assert.That(engine.Apply(Command(s, EpisodeCommandKind.CastVote, s.nominees[0])).accepted, Is.True);
                var row = engine.Snapshot.ledger.ballots.Single(b => b.week == s.week && b.voterId == s.playerId);
                Assert.That(row.targetId, Is.EqualTo(s.nominees[0]));
                Assert.That(row.readBefore, Is.Null, "An unknown house predicts nothing, and the ballot says so.");
                for (int i = 0; i < 10 && !engine.Snapshot.evictionResolved; i++)
                    Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
                Assert.That(engine.Snapshot.ledger.ballots.Single(b => b.week == s.week).correct, Is.False, "No read, so it cannot have been right.");
                return;
            }
            Assert.Fail("Twenty seeds and the player never voted.");
        }

        [Test]
        public void TheReadIsNotAvailableWithoutAVoteToRead()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(5));
            var s = engine.Snapshot;
            Assert.That(VoteRead.Available(s), Is.False);
            Assert.That(VoteRead.Read(s).available, Is.False);
            var refused = engine.Apply(Command(s, EpisodeCommandKind.AskVote, s.Active.First(c => !c.isPlayer).id));
            Assert.That(refused.accepted, Is.False);
            Assert.That(refused.reason, Does.Contain("no vote"));
        }
    }
}
