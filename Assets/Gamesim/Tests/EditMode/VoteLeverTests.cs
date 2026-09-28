using System;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The levers (STRATEGY-LOOP-PLAN.md §3): a vote deal with a voter is an obligation in their
    /// ballot, sized by their view of the player and their word; the reveal judges it; and the lever
    /// says what it moved. Plus the fixes the levers bring: bought time is the week's, a lie sounds
    /// like one, and asking about the vote leaves no memory behind.
    /// </summary>
    public sealed class VoteLeverTests
    {
        private static EpisodeEngine Reach(uint seed, Func<EpisodeState, bool> until, int limit = 600)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            for (int i = 0; i < limit && !until(engine.Snapshot); i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            Assert.That(until(engine.Snapshot), Is.True, "The season never reached the state the test needs.");
            return engine;
        }

        /// <summary>A campaign with two on the block, the levers on.</summary>
        private static EpisodeState Levered(uint seed)
        {
            var s = Reach(seed, x => x.phase == EpisodePhase.Campaign && x.nominees.Count == 2).Snapshot;
            EpisodeEngine.EnableLevers(s);
            return s;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string targetId, string secondTargetId = null, string text = null) =>
            new EpisodeCommand
            {
                id = "lever-" + kind + "-" + s.revision + "-" + Guid.NewGuid().ToString("N"), actorId = s.playerId, kind = kind,
                targetId = targetId, secondTargetId = secondTargetId, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        private static ContestantState[] NpcVoters(EpisodeState s) => EpisodeEngine.Voters(s).Where(v => !v.isPlayer).ToArray();

        private static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var row = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (row == null) s.relationships.Add(row = new RelationshipState { fromId = from, toId = to });
            row.score = score;
        }

        /// <summary>The player learns how a voter stands toward a nominee, as an overheard line would teach it.</summary>
        private static void Learn(EpisodeState s, string voterId, string nomineeId) =>
            s.ledger.standings.Add(new StandingRow { week = s.week, fromId = voterId, toId = nomineeId, source = ClaimSource.Overheard, score = s.Score(voterId, nomineeId) });

        private static DealState Deal(EpisodeState s, string voterId, string type, string targetId) => new DealState
        {
            id = "deal-test-" + type, type = type, proposerId = s.playerId, recipientId = voterId, targetId = targetId,
            status = DealStatus.Active, week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(type),
        };

        private static void PlainWord(ContestantState voter) => voter.traits.RemoveAll(t => t == "Loyal" || t == "Sneaky");

        private static double Term(EpisodeState s, string voterId, string nomineeId) =>
            EpisodeEngine.Obligations(s, voterId).Where(t => t.nomineeId == nomineeId).Sum(t => t.value);

        private static VoteRead.VoterRead Read(EpisodeState s, string voterId) => VoteRead.Read(s).voters.Single(r => r.voterId == voterId);

        [Test]
        public void AnObligationIsSizedByTheirViewOfYouAndTheirWord()
        {
            var s = Levered(31);
            var voter = NpcVoters(s).First(); string target = s.nominees[0];
            PlainWord(voter);
            s.deals.Add(Deal(s, voter.id, DealKind.VoteEvict, target));
            SetScore(s, voter.id, s.playerId, 50);
            Assert.That(Term(s, voter.id, target), Is.EqualTo(-8).Within(1e-9), "Whole at a view of fifty, against the one it names.");
            SetScore(s, voter.id, s.playerId, 25);
            Assert.That(Term(s, voter.id, target), Is.EqualTo(-4).Within(1e-9), "Half at twenty-five.");
            SetScore(s, voter.id, s.playerId, -10);
            Assert.That(Term(s, voter.id, target), Is.EqualTo(0).Within(1e-9), "A stranger's word is worth nothing.");
            SetScore(s, voter.id, s.playerId, 50); voter.traits.Add("Loyal");
            Assert.That(Term(s, voter.id, target), Is.EqualTo(-12).Within(1e-9), "Loyal keeps their word and a half.");
            voter.traits.Add("Sneaky");
            Assert.That(Term(s, voter.id, target), Is.EqualTo(0).Within(1e-9), "Sneaky's word was never worth anything.");
            voter.traits.Remove("Sneaky");
            s.deals.Add(Deal(s, voter.id, DealKind.VoteSave, target));
            Assert.That(Term(s, voter.id, target), Is.EqualTo(0).Within(1e-9), "A vote to keep and a vote to evict the same person cancel.");
            Assert.That(Term(s, voter.id, s.nominees[1]), Is.EqualTo(0).Within(1e-9), "and name nobody else.");
            s.leverRulesStartWeek = 0;
            Assert.That(EpisodeEngine.Obligations(s, voter.id), Is.Empty, "Before the levers there are no obligations.");
        }

        [Test]
        public void AnObligationEntersTheBallotAsATermThePlayerKnows()
        {
            var s = Levered(31);
            var voter = NpcVoters(s).First(); string target = s.nominees[0];
            PlainWord(voter); SetScore(s, voter.id, s.playerId, 50);
            var plain = WebEvictionVoting.EvaluateNative(s, voter.id);
            Assert.That(plain.nomineeEvaluations.SelectMany(n => n.factors).Any(f => f.code == "obligation"), Is.False,
                "No deal, no term: the web's factors are exactly the web's.");
            s.deals.Add(Deal(s, voter.id, DealKind.VoteEvict, target));
            var owed = WebEvictionVoting.EvaluateNative(s, voter.id);
            var factor = owed.nomineeEvaluations.Single(n => n.nomineeId == target).factors.Single(f => f.code == "obligation");
            Assert.That(factor.value, Is.EqualTo(-8).Within(1e-9));
            Assert.That(factor.visibility, Is.EqualTo("playerKnown"));
            Assert.That(factor.evidenceIds, Is.EqualTo(new[] { "deal-test-" + DealKind.VoteEvict }));
            Assert.That(VoteRead.FactorKnown(s, voter.id, target, factor), Is.True, "Your own deal is a term you know.");
            Assert.That(owed.nomineeEvaluations.Single(n => n.nomineeId != target).factors.Any(f => f.code == "obligation"), Is.False,
                "The term sits on the nominee the deal names.");
            s.leverRulesStartWeek = 0;
            Assert.That(WebEvictionVoting.EvaluateNative(s, voter.id).nomineeEvaluations.SelectMany(n => n.factors).Any(f => f.code == "obligation"), Is.False,
                "Before the levers the ballot is what it was.");
        }

        [Test]
        public void ADealAndAStandingFlipATornVoterAndNeverAFirmOne()
        {
            var s = Levered(31);
            var voter = NpcVoters(s).First(); string first = s.nominees[0], second = s.nominees[1];
            PlainWord(voter); SetScore(s, voter.id, s.playerId, 50);
            s.alliances.RemoveAll(a => a.members.Contains(voter.id));
            // Torn toward evicting the first (they like the second a little more): a deal to evict
            // the second turns them around, and they are still torn.
            SetScore(s, voter.id, first, 0); SetScore(s, voter.id, second, 15);
            Learn(s, voter.id, first); Learn(s, voter.id, second);
            var torn = Read(s, voter.id);
            Assume.That(torn.confidence, Is.EqualTo(VoteRead.Torn)); Assume.That(torn.leaningId, Is.EqualTo(first));
            s.deals.Add(Deal(s, voter.id, DealKind.VoteEvict, second));
            var turned = Read(s, voter.id);
            Assert.That(turned.leaningId, Is.EqualTo(second), "A deal turns a torn voter.");
            // Torn toward evicting the second already: the same deal makes it a lean.
            s.deals.Clear();
            SetScore(s, voter.id, first, 15); SetScore(s, voter.id, second, 0);
            s.ledger.standings.Clear(); Learn(s, voter.id, first); Learn(s, voter.id, second);
            Assume.That(Read(s, voter.id).confidence, Is.EqualTo(VoteRead.Torn));
            s.deals.Add(Deal(s, voter.id, DealKind.VoteEvict, second));
            var leaning = Read(s, voter.id);
            Assert.That(leaning.confidence, Is.EqualTo(VoteRead.Leaning), "A deal and a standing make a lean.");
            Assert.That(leaning.leaningId, Is.EqualTo(second));
            // Firm: far closer with the first. A deal against that does not turn them.
            s.deals.Clear();
            SetScore(s, voter.id, first, 90); SetScore(s, voter.id, second, -90);
            s.ledger.standings.Clear(); Learn(s, voter.id, first); Learn(s, voter.id, second);
            var firm = Read(s, voter.id);
            Assume.That(firm.confidence, Is.EqualTo(VoteRead.Firm)); Assume.That(firm.leaningId, Is.EqualTo(second));
            s.deals.Add(Deal(s, voter.id, DealKind.VoteEvict, first));
            var held = Read(s, voter.id);
            Assert.That(held.leaningId, Is.EqualTo(second), "A firm voter keeps their lean: relationship still dominates.");
            Assert.That(held.confidence, Is.EqualTo(VoteRead.Firm));
        }

        [TestCase(true)] [TestCase(false)]
        public void AVoteDealIsJudgedAtTheRevealAndABrokenWordIsRemembered(bool kept)
        {
            var s = Reach(31, x => x.phase == EpisodePhase.Eviction && x.evictionStage == EvictionStage.Voting && x.votes.Count == 0).Snapshot;
            EpisodeEngine.EnableLevers(s); EpisodeEngine.EnableStory(s);
            // The voter surest of their ballot, so the web's own deal term cannot turn them either way.
            var voter = NpcVoters(s).OrderByDescending(v => EpisodeEngine.ProjectBallot(s, v.id).margin).First();
            var projected = EpisodeEngine.ProjectBallot(s, voter.id);
            Assume.That(projected.margin, Is.GreaterThan(8), "A ballot a deal cannot move.");
            string target = kept ? projected.selectedNomineeId : s.nominees.First(id => id != projected.selectedNomineeId);
            SetScore(s, voter.id, s.playerId, 0);
            s.deals.Add(Deal(s, voter.id, DealKind.VoteEvict, target));
            var engine = new EpisodeEngine(s);
            for (int i = 0; i < 20 && !engine.Snapshot.evictionResolved; i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var after = engine.Snapshot;
            Assert.That(after.evictionResolved, Is.True);
            var deal = after.deals.Single(d => d.id == "deal-test-" + DealKind.VoteEvict);
            Assert.That(deal.status, Is.EqualTo(kept ? DealStatus.Fulfilled : DealStatus.Broken), "The reveal judges a vote deal.");
            Assert.That(after.events.Any(e => e.kind == "deal-outcome" && e.text.Contains(voter.name) && e.text.Contains("vote to evict")), Is.True,
                "and says so.");
            var reckoning = after.story.reckonings.FirstOrDefault(r => r.npcId == voter.id);
            if (kept) Assert.That(reckoning, Is.Null);
            else
            {
                Assert.That(reckoning, Is.Not.Null, "A broken word waits for the next conversation.");
                Assert.That(reckoning.betrayerIsPlayer, Is.False);
            }
        }

        [Test]
        public void TheLeverSaysWhatItMovedInTheReadsTerms()
        {
            for (uint seed = 31; seed < 71; seed++)
            {
                var s = Levered(seed);
                if (EpisodeEngine.SocialActionsSpent(s) >= EpisodeEngine.SocialActionBudget(s)) continue;
                var voter = NpcVoters(s).First(); string first = s.nominees[0], second = s.nominees[1];
                PlainWord(voter); SetScore(s, voter.id, s.playerId, 60);
                s.alliances.RemoveAll(a => a.members.Contains(voter.id));
                SetScore(s, voter.id, first, 15); SetScore(s, voter.id, second, 0);
                Learn(s, voter.id, first); Learn(s, voter.id, second);
                if (Read(s, voter.id).confidence != VoteRead.Torn) continue;
                var engine = new EpisodeEngine(s);
                Assert.That(engine.Apply(Command(s, EpisodeCommandKind.ProposeDeal, voter.id, second, DealKind.VoteEvict)).accepted, Is.True);
                var after = engine.Snapshot;
                if (!after.deals.Any(d => d.type == DealKind.VoteEvict && d.recipientId == voter.id)) continue;   // turned down: the roll's business
                var line = after.events.Last(e => e.kind == "lever");
                Assert.That(line.text, Does.StartWith(voter.name + ": torn → leaning evict " + after.Find(second).name).And.Contain("your deal"),
                    "The lever prints what it moved, in the read's terms.");
                Assert.That(line.audienceIds, Is.EquivalentTo(new[] { s.playerId, voter.id }));
                return;
            }
            Assert.Fail("No seed had a torn voter take the deal.");
        }

        [TestCase(true)] [TestCase(false)]
        public void BoughtTimeIsTheWeeksUnderTheLevers(bool levers)
        {
            var s = Levered(31);
            if (!levers) s.leverRulesStartWeek = 0;
            s.boughtActionPoints = 3;
            var engine = new EpisodeEngine(s);
            int week = s.week;
            for (int i = 0; i < 80 && engine.Snapshot.week == week; i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            Assert.That(engine.Snapshot.week, Is.EqualTo(week + 1));
            Assert.That(engine.Snapshot.boughtActionPoints, Is.EqualTo(levers ? 0 : 3),
                levers ? "The week turns and the time bought with it is spent." : "Before the levers, bought time was the season's.");
        }

        [Test]
        public void ALieSoundsLikeOneToThePersonToldUnderTheLevers()
        {
            var s = Levered(31);
            var told = s.Active.First(c => !c.isPlayer); var about = s.Active.First(c => !c.isPlayer && c.id != told.id);
            var engine = new EpisodeEngine(s);
            Assert.That(engine.Apply(Command(s, EpisodeCommandKind.SpreadLie, told.id, about.id)).accepted, Is.True);
            var memory = engine.Snapshot.memories.Last(m => m.ownerId == told.id && m.subjectId == about.id);
            Assert.That(memory.text, Does.Contain("suspicious").And.Contain(about.name), "so the ballot's memory term reads it as the lie it is.");
        }

        [Test]
        public void AskingAboutTheVoteLeavesNoMemoryBehind()
        {
            var s = Levered(31);
            var voter = NpcVoters(s).First();
            var engine = new EpisodeEngine(s);
            Assert.That(engine.Apply(Command(s, EpisodeCommandKind.AskVote, voter.id)).accepted, Is.True);
            Assert.That(engine.Snapshot.memories.Any(m => m.ownerId == voter.id && m.subjectId == s.playerId && m.text.Contains("asked me")), Is.False,
                "The claim is the record; a memory naming \"you\" would count for the player in every ballot they are on the block for.");
        }

        [Test]
        public void AnAnsweredReplyCardIsOnTheRecord()
        {
            var s = Levered(31);
            s.strategyRulesStartWeek = 1;   // the cards are the strategy windows'
            var from = s.Active.First(c => !c.isPlayer); var about = s.Active.First(c => !c.isPlayer && c.id != from.id);
            ReplyCards.Offer(s, ReplyCards.Gossip, from.id, about.id);
            var card = ReplyCards.Pending(s);
            Assume.That(card, Is.Not.Null, "A card in front of the player.");
            var engine = new EpisodeEngine(s);
            var reply = ReplyCards.Replies(card.kind)[1];
            Assert.That(engine.Apply(Command(s, EpisodeCommandKind.ReplyToHouseguest, card.id, null, reply.Key)).accepted, Is.True);
            var row = engine.Snapshot.ledger.replies.Single();
            Assert.That(row.cardId, Is.EqualTo(card.id));
            Assert.That(row.kind, Is.EqualTo(card.kind));
            Assert.That(row.fromId, Is.EqualTo(card.fromId));
            Assert.That(row.listenerId, Is.EqualTo(card.aboutId));
            Assert.That(row.replyKey, Is.EqualTo(reply.Key));
            Assert.That(row.toThem, Is.EqualTo(reply.ToThem));
            Assert.That(row.week, Is.EqualTo(s.week));
            Assert.That(engine.Snapshot.replyCards.All(r => r.id != card.id), Is.True, "and the card is settled.");
        }
    }
}
