using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The player's week, judged (ACTIONS-DEALS-ALLIANCES-PLAN V4): every read and claim against the
    /// ballots the reveal read aloud, the deals and promises that ended as the player was told it,
    /// the calls they made and who followed, and Game Sense so far in the parts the player can see.
    /// Seeded weeks: a season walked to its vote with the week's holdings pinned as fixtures and the
    /// vote played out by the engine, and seasons built with the rows a week leaves.
    /// </summary>
    public sealed class YourWeekTests
    {
        // ---------------------------------------------------------------- against the engine's own vote

        /// <summary>
        /// A voter who told the player one name and was overheard saying the other: the reveal can
        /// bear out only one of them, and it is the ballot the whole house heard read that decides
        /// which.
        /// </summary>
        [Test]
        public void ARightReadAndAWrongReadAreJudgedAgainstTheBallotsTheRevealRead()
        {
            var s = AtTheVote(31);
            int week = s.week;
            var voter = NpcVoters(s).First();
            s.ledger.claims.Add(new ClaimRow { week = week, voterId = voter.id, targetId = s.nominees[0], source = ClaimSource.Told });
            s.ledger.claims.Add(new ClaimRow { week = week, voterId = voter.id, targetId = s.nominees[1], source = ClaimSource.Overheard });
            var after = Reveal(s, s.nominees[0]);

            var claims = YourWeek.Build(after, week).reads.Where(l => l.kind == YourWeek.Kinds.Claim).ToList();
            Assert.That(claims, Has.Count.EqualTo(2), "Both of the week's claims are judged.");
            Assert.That(claims.Select(l => l.verdict), Is.EquivalentTo(new[] { YourWeek.Verdicts.Right, YourWeek.Verdicts.Wrong }), "One held, one did not.");
            Assert.That(claims.Select(l => l.aboutId), Is.All.EqualTo(voter.id), "Each line carries the voter's face.");

            // The ballot the reveal read for this voter, to the whole house.
            var read = after.events.Single(e => e.week == week && e.kind == "vote-reveal" && e.text.StartsWith(voter.name + " voted to evict ", StringComparison.Ordinal));
            Assert.That(read.audienceIds, Is.Empty, "Every ballot is read aloud: the verdict rests on nothing private.");
            string votedOut = after.nominees.Single(id => read.text.StartsWith(voter.name + " voted to evict " + after.Find(id).name + ".", StringComparison.Ordinal));
            string spared = after.nominees.Single(id => id != votedOut);
            var told = claims.Single(l => l.text.StartsWith(voter.name + " told you", StringComparison.Ordinal));
            var overheard = claims.Single(l => l.text.StartsWith("Overheard: ", StringComparison.Ordinal));
            var rightOne = votedOut == after.nominees[0] ? told : overheard;
            var wrongOne = votedOut == after.nominees[0] ? overheard : told;
            Assert.That(rightOne.verdict, Is.EqualTo(YourWeek.Verdicts.Right));
            Assert.That(rightOne.text, Does.EndWith(after.Find(votedOut).name + ", and voted that way."));
            Assert.That(wrongOne.verdict, Is.EqualTo(YourWeek.Verdicts.Wrong));
            Assert.That(wrongOne.text, Does.Contain(after.Find(spared).name).And.EndWith(", and voted to evict " + after.Find(votedOut).name + "."),
                "A wrong read says what the reveal showed instead.");
        }

        /// <summary>
        /// A vote deal the voter kept and a vote promise the player broke, both ended by the engine's
        /// own reveal: each is on the list once, as the player was told it, with who kept or broke it.
        /// </summary>
        [Test]
        public void AKeptDealAndABrokenPromiseAreJudgedAsThePlayerWasToldThem()
        {
            for (uint seed = 31; seed < 71; seed++)
            {
                var s = AtTheVote(seed);
                int week = s.week;
                // The voter surest of their ballot, so neither the deal nor the house can turn them.
                var voter = NpcVoters(s).OrderByDescending(v => EpisodeEngine.ProjectBallot(s, v.id).margin).First();
                var projected = EpisodeEngine.ProjectBallot(s, voter.id);
                if (projected.margin <= 8) continue;
                string target = projected.selectedNomineeId, spared = s.nominees.First(id => id != target);
                SetScore(s, voter.id, s.playerId, 0);
                s.deals.Add(new DealState
                {
                    id = "deal-your-week", type = DealKind.VoteEvict, proposerId = voter.id, recipientId = s.playerId, targetId = target,
                    status = DealStatus.Active, week = week, expiresWeek = week, trustImpact = DealKind.DefaultTrust(DealKind.VoteEvict),
                });
                var promisee = s.Active.First(c => !c.isPlayer && c.id != voter.id);
                s.promises.Add(new PromiseState
                {
                    id = "promise-your-week", fromId = s.playerId, toId = promisee.id, targetId = spared, kind = PromiseKind.Vote,
                    status = PromiseStatus.Active, week = week, expiresWeek = week,
                });
                // The player votes the deal's way, which is against their promise.
                var after = Reveal(s, target);
                if (after.deals.Single(d => d.id == "deal-your-week").status != DealStatus.Fulfilled) continue;

                string you = after.Find(after.playerId).name;
                var word = YourWeek.Build(after, week).word;
                var deal = word.Single(l => l.kind == YourWeek.Kinds.Deal);
                Assert.That(deal.verdict, Is.EqualTo(YourWeek.Verdicts.Kept));
                Assert.That(deal.byId, Is.EqualTo(voter.id), "They voted on it first, and kept it.");
                Assert.That(deal.aboutId, Is.EqualTo(voter.id));
                Assert.That(deal.text, Is.EqualTo(voter.name + " kept the vote-to-evict deal with you."));
                Assert.That(after.events.Any(e => e.week == week && e.kind == "deal-outcome" && e.audienceIds.Contains(after.playerId)
                    && e.text == voter.name + " honoured a vote to evict with " + you + "."), Is.True, "It is the line the player was told.");

                var promise = word.Single(l => l.kind == YourWeek.Kinds.Promise);
                Assert.That(promise.verdict, Is.EqualTo(YourWeek.Verdicts.Broken));
                Assert.That(promise.byId, Is.EqualTo(after.playerId), "The player broke it.");
                Assert.That(promise.aboutId, Is.EqualTo(promisee.id));
                Assert.That(promise.text, Is.EqualTo("You broke your vote promise to " + promisee.name + "."));
                Assert.That(after.events.Any(e => e.week == week && e.kind == "promise-outcome" && e.audienceIds.Contains(after.playerId)
                    && e.text == you + " broke a Vote promise."), Is.True, "It is the line the player was told.");
                Assert.That(word, Has.Count.EqualTo(2), "Told and remembered, each is still one line.");
                return;
            }
            Assert.Fail("No seed had a voter sure enough of their ballot to keep a vote deal.");
        }

        // ---------------------------------------------------------------- seeded weeks

        [Test]
        public void TheWhipCountIsRightWrongOrNotKnown()
        {
            var s = Fresh();
            s.week = 2;
            var npcs = s.contestants.Where(c => !c.isPlayer).ToList();
            s.ledger.power.Add(new PowerRow { week = 1, hohId = npcs[0].id, evicteeId = npcs[1].id, nominees = new List<string> { npcs[1].id, npcs[2].id }, tally = new List<int> { 3, 1 } });
            var ballot = new BallotRow { week = 1, voterId = s.playerId, targetId = npcs[1].id, readBefore = npcs[1].id, correct = true };
            s.ledger.ballots.Add(ballot);

            var right = YourWeek.Build(s, 1).reads.Single();
            Assert.That(right.kind, Is.EqualTo(YourWeek.Kinds.WhipCount));
            Assert.That(right.verdict, Is.EqualTo(YourWeek.Verdicts.Right));
            Assert.That(right.text, Is.EqualTo("Your whip count said " + npcs[1].name + " would go, and they did."));

            ballot.readBefore = npcs[2].id; ballot.correct = false;
            var wrong = YourWeek.Build(s, 1).reads.Single();
            Assert.That(wrong.verdict, Is.EqualTo(YourWeek.Verdicts.Wrong));
            Assert.That(wrong.text, Is.EqualTo("Your whip count said " + npcs[2].name + " would go; " + npcs[1].name + " went instead."));

            ballot.readBefore = null;
            var none = YourWeek.Build(s, 1).reads.Single();
            Assert.That(none.verdict, Is.EqualTo(YourWeek.Verdicts.NotKnown), "A count that called nobody has nothing to be right about.");
        }

        /// <summary>A claim whose voter never voted, and one in a week whose vote is still to come, are not known yet.</summary>
        [Test]
        public void AClaimTheRevealCouldNotJudgeIsNotKnown()
        {
            var s = Fresh();
            var npcs = s.contestants.Where(c => !c.isPlayer).ToList();
            s.ledger.claims.Add(new ClaimRow { week = 1, voterId = npcs[3].id, targetId = npcs[1].id, source = ClaimSource.Told });
            var waiting = YourWeek.Build(s, 1).reads.Single();
            Assert.That(waiting.verdict, Is.EqualTo(YourWeek.Verdicts.NotKnown));
            Assert.That(waiting.text, Is.EqualTo(npcs[3].name + " told you: evict " + npcs[1].name + ". The vote is still to come."));

            s.ledger.power.Add(new PowerRow { week = 1, hohId = npcs[0].id, evicteeId = npcs[1].id, nominees = new List<string> { npcs[1].id, npcs[2].id }, tally = new List<int> { 2, 1 } });
            Assert.That(YourWeek.Build(s, 1).reads.Single().text, Does.EndWith("Their vote never came."), "Revealed, and still open: they never voted.");
        }

        /// <summary>A claim that the player is the one going says so plainly, and a wrong one says who they voted out instead.</summary>
        [Test]
        public void AClaimThatNamesThePlayerSaysSoPlainly()
        {
            var s = Fresh();
            s.week = 2;
            var npcs = s.contestants.Where(c => !c.isPlayer).ToList();
            s.ledger.power.Add(new PowerRow { week = 1, hohId = npcs[0].id, evicteeId = npcs[1].id, nominees = new List<string> { npcs[1].id, s.playerId }, tally = new List<int> { 3, 1 } });
            s.ledger.claims.Add(new ClaimRow { week = 1, voterId = npcs[2].id, targetId = s.playerId, source = ClaimSource.Told, status = ClaimStatus.Lied });
            s.ledger.claims.Add(new ClaimRow { week = 1, voterId = npcs[3].id, targetId = s.playerId, source = ClaimSource.Overheard, status = ClaimStatus.Kept });
            var reads = YourWeek.Build(s, 1).reads;
            Assert.That(reads.Select(l => l.verdict), Is.EqualTo(new[] { YourWeek.Verdicts.Wrong, YourWeek.Verdicts.Right }));
            Assert.That(reads[0].text, Is.EqualTo(npcs[2].name + " told you they would vote you out, and voted to evict " + npcs[1].name + "."));
            Assert.That(reads[1].text, Is.EqualTo("Overheard: " + npcs[3].name + " is voting you out, and voted that way."));
        }

        [Test]
        public void ACallWithADefectorSaysWhoFollowedAndWhoDidNot()
        {
            var s = Fresh();
            s.week = 2;
            var npcs = s.contestants.Where(c => !c.isPlayer).ToList();
            s.alliances.Add(new AllianceState { id = "alliance-test", name = "The Test Pact", members = new List<string> { s.playerId, npcs[3].id, npcs[4].id }, active = true });
            s.ledger.power.Add(new PowerRow { week = 1, hohId = npcs[0].id, evicteeId = npcs[1].id, nominees = new List<string> { npcs[1].id, npcs[2].id }, tally = new List<int> { 2, 1 } });
            s.ledger.calls.Add(new BlocCallRow
            {
                week = 1, allianceId = "alliance-test", callerId = s.playerId, targetId = npcs[1].id,
                followed = new List<string> { npcs[3].id }, defected = new List<string> { npcs[4].id },
            });

            var calls = YourWeek.Build(s, 1).calls;
            Assert.That(calls, Has.Count.EqualTo(3), "The call, then each member.");
            Assert.That(calls[0].kind, Is.EqualTo(YourWeek.Kinds.Call));
            Assert.That(calls[0].verdict, Is.Null, "The call itself carries no verdict; its members do.");
            Assert.That(calls[0].text, Is.EqualTo("You called it in The Test Pact: evict " + npcs[1].name + ", and " + npcs[1].name + " went home."));
            Assert.That(calls[1].verdict, Is.EqualTo(YourWeek.Verdicts.Followed));
            Assert.That(calls[1].aboutId, Is.EqualTo(npcs[3].id));
            Assert.That(calls[1].text, Is.EqualTo(npcs[3].name + " followed your call."));
            Assert.That(calls[2].verdict, Is.EqualTo(YourWeek.Verdicts.Defected));
            Assert.That(calls[2].aboutId, Is.EqualTo(npcs[4].id));
            Assert.That(calls[2].text, Is.EqualTo(npcs[4].name + " would not follow your call."));
            Assert.That(YourWeek.Build(s, 2).calls, Is.Empty, "A call is its own week's.");
        }

        [Test]
        public void AnEmptyWeekHasNothingToJudge()
        {
            var s = Fresh();
            var mine = YourWeek.Build(s, 1);
            Assert.That(mine.Empty, Is.True);
            Assert.That(mine.Lines, Is.Empty);
            Assert.That(mine.sense.strategy, Is.EqualTo(50), "The middle, before the ledger says anything.");
            Assert.That(mine.sense.offered, Is.EqualTo(0));
            Assert.That(mine.sense.rows, Is.Empty);
            Assert.That(YourWeek.Build(s, 0).Empty, Is.True);
            Assert.That(YourWeek.Build(s, 99).Empty, Is.True, "A week not played yet.");
            Assert.That(YourWeek.Build(null, 1).Empty, Is.True);
        }

        /// <summary>Who kept or broke a deal is read from the words the player was told, in all three of the engine's shapes.</summary>
        [Test]
        public void ADealsLineSaysWhoKeptOrBrokeIt()
        {
            var s = Fresh();
            var npc = s.contestants.First(c => !c.isPlayer);
            string you = s.Find(s.playerId).name;
            RelationshipLedger.Record(s, s.playerId, npc.id, YourWeek.DealBroken, -30, npc.name + " broke a safety pact with " + you + ".");
            RelationshipLedger.Record(s, npc.id, s.playerId, YourWeek.DealKept, 24, you + " honoured a veto commitment with " + npc.name + ".");
            RelationshipLedger.Record(s, s.playerId, npc.id, YourWeek.DealBroken, -22, you + " and " + npc.name + " fell out over their voting block.");
            RelationshipLedger.Record(s, s.playerId, npc.id, YourWeek.DealBroken, -22, "Words nobody wrote.");

            var word = YourWeek.Build(s, 1).word;
            Assert.That(word, Has.Count.EqualTo(4), "One line for each ending, read from the player's own copy of each.");
            Assert.That(word[0].verdict, Is.EqualTo(YourWeek.Verdicts.Broken)); Assert.That(word[0].byId, Is.EqualTo(npc.id));
            Assert.That(word[0].text, Is.EqualTo(npc.name + " broke the safety pact with you."));
            Assert.That(word[1].verdict, Is.EqualTo(YourWeek.Verdicts.Kept)); Assert.That(word[1].byId, Is.EqualTo(s.playerId));
            Assert.That(word[1].text, Is.EqualTo("You kept the veto commitment with " + npc.name + "."));
            Assert.That(word[2].verdict, Is.EqualTo(YourWeek.Verdicts.Broken)); Assert.That(word[2].byId, Is.Null, "A pair fell out together.");
            Assert.That(word[2].text, Is.EqualTo("You and " + npc.name + " fell out over your voting block."));
            Assert.That(word[3].verdict, Is.EqualTo(YourWeek.Verdicts.Broken), "Words the reader does not know still carry how it ended.");
            Assert.That(word[3].text, Is.EqualTo("Words nobody wrote."));
            Assert.That(word.Select(l => l.aboutId), Is.All.EqualTo(npc.id));
        }

        /// <summary>The log keeps 256 lines; the player's own memory of a promise's end outlasts it, and the two are never counted twice.</summary>
        [Test]
        public void APromiseIsJudgedFromTheLogOrThePlayersOwnMemory()
        {
            var s = Fresh();
            var npc = s.contestants.First(c => !c.isPlayer);
            string mine = s.Find(s.playerId).name + " fulfilled a Safety promise.";
            s.memories.Add(new MemoryState { ownerId = s.playerId, subjectId = npc.id, text = mine, week = 1, isPrivate = true });
            var kept = YourWeek.Build(s, 1).word.Single();
            Assert.That(kept.verdict, Is.EqualTo(YourWeek.Verdicts.Kept));
            Assert.That(kept.byId, Is.EqualTo(s.playerId));
            Assert.That(kept.text, Is.EqualTo("You kept your safety promise to " + npc.name + "."));

            s.events.Add(Line(s, "promise-outcome", mine, s.playerId, npc.id));
            Assert.That(YourWeek.Build(s, 1).word, Has.Count.EqualTo(1), "Told and remembered, it is one promise.");

            s.events.Add(Line(s, "promise-outcome", npc.name + " broke a FinalTwo promise.", npc.id, s.playerId));
            var broken = YourWeek.Build(s, 1).word.Single(l => l.verdict == YourWeek.Verdicts.Broken);
            Assert.That(broken.byId, Is.EqualTo(npc.id));
            Assert.That(broken.text, Is.EqualTo(npc.name + " broke their final two promise to you."));
        }

        /// <summary>A deal or promise between two other houseguests is theirs: their records, their lines, their memories.</summary>
        [Test]
        public void SomebodyElsesDealOrPromiseNeverReachesYourWeek()
        {
            var s = Fresh();
            var npcs = s.contestants.Where(c => !c.isPlayer).ToList();
            RelationshipLedger.Record(s, npcs[0].id, npcs[1].id, YourWeek.DealBroken, -30, npcs[0].name + " broke a safety pact with " + npcs[1].name + ".");
            s.events.Add(Line(s, "deal-outcome", npcs[0].name + " broke a safety pact with " + npcs[1].name + ".", npcs[0].id, npcs[1].id));
            s.events.Add(Line(s, "promise-outcome", npcs[0].name + " broke a Vote promise.", npcs[0].id, npcs[1].id));
            s.memories.Add(new MemoryState { ownerId = npcs[1].id, subjectId = npcs[0].id, text = npcs[0].name + " broke a Vote promise.", week = 1, isPrivate = true });
            Assert.That(YourWeek.Build(s, 1).word, Is.Empty);
        }

        // ---------------------------------------------------------------- Game Sense so far

        /// <summary>
        /// Game Sense so far is the verdict's own notes, the known ones: the power, the player's own
        /// throw, never a win weighed by every competitor's strength, another Head of Household's
        /// plan, or how the house sees the player. The verdict at the end still counts them all.
        /// </summary>
        [Test]
        public void GameSenseSoFarShowsOnlyWhatThePlayerCanSee()
        {
            var s = Fresh();
            s.week = 2;
            var npcs = s.contestants.Where(c => !c.isPlayer).ToList();
            foreach (var npc in npcs) SetScore(s, npc.id, s.playerId, 60);
            s.ledger.competitions.Add(new CompetitionRow { week = 1, kind = "HoH", field = 6, placement = 1, entry = CompetitionEntry.Played, performance = 0.8, expectedWin = 0.2 });
            s.ledger.competitions.Add(new CompetitionRow { week = 1, kind = "Veto", field = 6, placement = 6, entry = CompetitionEntry.Thrown });
            s.ledger.power.Add(new PowerRow
            {
                week = 1, hohId = npcs[0].id, vetoHolderId = npcs[3].id, evicteeId = npcs[1].id, nominees = new List<string> { npcs[1].id, s.playerId },
                tally = new List<int> { 3, 1 }, backdoorTargetId = s.playerId, backdoorResult = "survived",
            });

            var full = GameSense.Evaluate(s);
            var sense = YourWeek.Build(s, 1).sense;
            Assert.That(sense.rows.Any(n => n.text.Contains("odds")), Is.False, "A win weighed by every competitor's strength waits for the end.");
            Assert.That(sense.rows.Any(n => n.text.Contains("backdoor")), Is.False, "Another Head of Household's plan is theirs.");
            Assert.That(sense.rows.Any(n => n.face == GameSense.Social), Is.False, "How the house sees the player is the house's.");
            Assert.That(sense.rows.Any(n => n.text.Contains("you threw the veto")), Is.True, "A throw is the player's own, and the block is public.");
            Assert.That(sense.rows.Any(n => n.text.Contains("you were on the block")), Is.True);
            Assert.That(sense.rows.All(n => n.known && n.week == 1), Is.True);

            Assert.That(sense.strategy, Is.EqualTo(50 - 4), "On the block: the known strategy rows alone.");
            Assert.That(full.strategy, Is.EqualTo(50 - 4 + 8), "The verdict at the end counts the backdoor dodged as well.");
            Assert.That(full.notes.Where(n => n.rowKind == "standing" || n.rowKind == "grudge" || n.rowKind == "bond" || n.rowKind == "memory").All(n => !n.known), Is.True);
            Assert.That(full.notes.Where(n => n.rowKind == "ballot" || n.rowKind == "opportunity" || n.rowKind == "call").All(n => n.known), Is.True);
            Assert.That(YourWeek.RowText(sense.rows.Single(n => n.text.Contains("on the block"))), Is.EqualTo("You were on the block."), "A week's own recap drops its week.");
        }

        [Test]
        public void ChancesTakenSoFarCountTheWeeksThroughThisOne()
        {
            var s = Fresh();
            s.week = 3;
            s.ledger.opportunities.Add(new OpportunityRow { id = "play-1", kind = OpportunityKinds.Play, week = 1, response = OpportunityResponse.Taken, outcome = OpportunityOutcome.Won });
            s.ledger.opportunities.Add(new OpportunityRow { id = "deal-2", kind = OpportunityKinds.Deal, week = 2, response = OpportunityResponse.Expired, outcome = OpportunityOutcome.NotApplicable });
            s.ledger.opportunities.Add(new OpportunityRow { id = "play-3", kind = OpportunityKinds.Play, week = 3, response = OpportunityResponse.Taken, outcome = OpportunityOutcome.Lost });
            var second = YourWeek.Build(s, 2).sense;
            Assert.That(second.offered, Is.EqualTo(2));
            Assert.That(second.taken, Is.EqualTo(1));
            Assert.That(second.strategy, Is.EqualTo(50 + 6 - 1), "So far is through the week: the third week's play is not in it yet.");
            Assert.That(second.rows.Single().rowId, Is.EqualTo("deal-2"), "The week's own rows only.");
        }

        /// <summary>
        /// A play still running has been neither taken up nor let pass: the verdict, which reads a
        /// season that is over, would count it as let pass; so far waits until it closes.
        /// </summary>
        [Test]
        public void APlayStillRunningIsNotYetLetPass()
        {
            var s = Fresh();
            s.week = 2;
            s.storylines.Add(new StorylineState { id = "cycle-running", templateId = "the-secret-alliance", status = StorylineStatus.Active, week = 2 });
            s.ledger.opportunities.Add(new OpportunityRow { id = "cycle-running", kind = OpportunityKinds.Play, week = 2, source = "the-secret-alliance",
                response = OpportunityResponse.Ignored, outcome = OpportunityOutcome.NotApplicable });
            Assert.That(GameSense.Evaluate(s).notes.Any(n => n.rowId == "cycle-running" && n.text.Contains("let pass")), Is.True,
                "The verdict reads it as let pass.");
            var running = YourWeek.Build(s, 2).sense;
            Assert.That(running.rows, Is.Empty, "Still running: neither taken nor let pass.");
            Assert.That(running.strategy, Is.EqualTo(50));
            Assert.That(running.offered, Is.EqualTo(1), "It was offered all the same.");
            s.storylines[0].status = StorylineStatus.Abandoned;
            var closed = YourWeek.Build(s, 2).sense;
            Assert.That(closed.rows.Single().rowId, Is.EqualTo("cycle-running"), "Closed unanswered, it was let pass.");
            Assert.That(closed.strategy, Is.EqualTo(50 - 2));
        }

        // ---------------------------------------------------------------- reading changes nothing

        [Test]
        public void ReadingEveryWeekOfASeasonChangesNothingAndReadsTheSameTwice()
        {
            var engine = new EpisodeEngine(Fresh(9));
            for (int i = 0; i < 900 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var s = engine.Snapshot;
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.Finished));
            uint random = s.randomState;
            long sequence = s.nextSequence;
            int events = s.events.Count, revision = s.revision, memories = s.memories.Count;

            var first = Enumerable.Range(1, s.week).Select(w => YourWeek.Build(s, w)).ToList();
            var second = Enumerable.Range(1, s.week).Select(w => YourWeek.Build(s, w)).ToList();
            Assert.That(second.SelectMany(w => w.Lines.Select(l => l.ToString())), Is.EqualTo(first.SelectMany(w => w.Lines.Select(l => l.ToString()))));
            Assert.That(second.Select(w => w.sense.strategy), Is.EqualTo(first.Select(w => w.sense.strategy)));
            foreach (var week in first)
                foreach (var line in week.Lines)
                    Assert.That(line.verdict == null || YourWeek.Verdicts.All.Contains(line.verdict), Is.True, line.ToString());

            Assert.That(s.randomState, Is.EqualTo(random));
            Assert.That(s.nextSequence, Is.EqualTo(sequence));
            Assert.That(s.events.Count, Is.EqualTo(events));
            Assert.That(s.memories.Count, Is.EqualTo(memories));
            Assert.That(s.revision, Is.EqualTo(revision));
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
        }

        // ---------------------------------------------------------------- fixtures

        private static EpisodeState Fresh(uint seed = 31) => ContentCatalog.Create(seed);

        private static ContestantState[] NpcVoters(EpisodeState s) => EpisodeEngine.Voters(s).Where(v => !v.isPlayer).ToArray();

        private static bool Voting(EpisodeState x) => x.phase == EpisodePhase.Eviction && x.evictionStage == EvictionStage.Voting && x.votes.Count == 0
            && EpisodeEngine.Voters(x).Any(v => v.isPlayer) && EpisodeEngine.Voters(x).Count(v => !v.isPlayer) >= 2;

        /// <summary>The first season from <paramref name="from"/> walked to a vote the player casts beside two houseguests, with the levers on.</summary>
        private static EpisodeState AtTheVote(uint from)
        {
            for (uint seed = from; seed < from + 40; seed++)
            {
                var engine = new EpisodeEngine(ContentCatalog.Create(seed));
                for (int i = 0; i < 600 && !Voting(engine.Snapshot); i++)
                    if (!engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted) break;
                if (!Voting(engine.Snapshot)) continue;
                var s = engine.Snapshot;
                EpisodeEngine.EnableLevers(s);
                return s;
            }
            Assert.Fail("No season from seed " + from + " reached a vote the player casts beside two houseguests.");
            return null;
        }

        /// <summary>Plays the vote out: the player votes <paramref name="playerVotes"/>, the house as it will, and the reveal reads every ballot.</summary>
        private static EpisodeState Reveal(EpisodeState s, string playerVotes)
        {
            var engine = new EpisodeEngine(s);
            for (int i = 0; i < 20 && !engine.Snapshot.evictionResolved; i++)
            {
                var command = EpisodeEngineTests.NextCommand(engine.Snapshot);
                if (command.kind == EpisodeCommandKind.CastVote) command.targetId = playerVotes;
                var result = engine.Apply(command);
                Assert.That(result.accepted, Is.True, result.reason);
            }
            Assert.That(engine.Snapshot.evictionResolved, Is.True, "The vote was revealed.");
            return engine.Snapshot;
        }

        private static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var row = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (row == null) s.relationships.Add(row = new RelationshipState { fromId = from, toId = to });
            row.score = score;
        }

        private static EpisodeEvent Line(EpisodeState s, string kind, string text, params string[] audience) => new EpisodeEvent
        {
            sequence = (int)s.nextSequence++, week = s.week, phase = s.phase, kind = kind, text = text, audienceIds = audience.ToList(),
        };
    }
}
