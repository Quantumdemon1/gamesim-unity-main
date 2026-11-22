using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// A final two deal from minds level 1 (<see cref="FinalTwoDeals"/>, the owner's ruling of 2026-11-21): its obligation
    /// counts once in the final Head of Household's choice, and it lapses only if the partner nominated the player. Each
    /// case runs the level beside level 0, which must play as the season always did.
    /// </summary>
    public sealed class FinalTwoDealsTests
    {
        // ------------------------------------------------------------------ counted once

        [Test]
        public void AFinalTwoDealIsTheObligationAloneFromTheLevelAndTheDealTermTooBelow()
        {
            var below = Final(MindRules.Off); below.deals.Add(Deal(below, "final-two", below.hohId, below.playerId));
            var at = Final(FinalTwoDeals.Level); at.deals.Add(Deal(at, "final-two", at.hohId, at.playerId));

            var dealBelow = Factor(below, below.playerId, "deal");
            Assert.That(dealBelow.evidenceIds, Does.Contain("final-two"), "Below the level the deal term counts the deal,");
            Assert.That(dealBelow.value, Is.GreaterThan(0));
            Assert.That(Factor(below, below.playerId, "obligation").value, Is.EqualTo(EpisodeEngine.FinalTwoObligation), "and the obligation counts it again.");

            var dealAt = Factor(at, at.playerId, "deal");
            Assert.That(dealAt?.evidenceIds ?? new List<string>(), Does.Not.Contain("final-two"), "From the level the deal term leaves it out,");
            Assert.That(dealAt?.value ?? 0, Is.EqualTo(0));
            var obligation = Factor(at, at.playerId, "obligation");
            Assert.That(obligation.value, Is.EqualTo(EpisodeEngine.FinalTwoObligation), "and the obligation is its one count.");
            Assert.That(obligation.evidenceIds, Is.EqualTo(new[] { "final-two" }));

            double drop = Score(below, below.playerId) - Score(at, at.playerId);
            Assert.That(drop, Is.EqualTo(dealBelow.value).Within(1e-9), "The player's score moves by the deal term's count and nothing else.");
            Assert.That(Score(below, Other(below)), Is.EqualTo(Score(at, Other(at))).Within(1e-9), "The other finalist's score does not move.");
        }

        [Test]
        public void ACalledInFinalTwoPromiseIsCountedOnce()
        {
            var below = Final(MindRules.Off); CallIn(below);
            var at = Final(FinalTwoDeals.Level); CallIn(at);
            string evidence = "promise:" + below.hohId + ":" + below.playerId + ":final_2";
            Assert.That(Factor(below, below.playerId, "deal").evidenceIds, Does.Contain(evidence), "Below the level the deal term counts the promise.");
            Assert.That(Factor(at, at.playerId, "deal")?.evidenceIds ?? new List<string>(), Does.Not.Contain(evidence), "From the level it does not,");
            Assert.That(Factor(at, at.playerId, "obligation").evidenceIds, Is.EqualTo(new[] { "promise:f2-promise" }), "since the obligation counts it.");
        }

        [Test]
        public void ADealTheObligationDoesNotCountStaysInTheDealTerm()
        {
            // A Sneaky Head of Household's word is worth nothing, so no obligation counts the deal: the deal term still does, once.
            var at = Final(FinalTwoDeals.Level); at.Find(at.hohId).traits = new List<string> { "Sneaky" };
            at.deals.Add(Deal(at, "final-two", at.hohId, at.playerId));
            Assert.That(Factor(at, at.playerId, "obligation"), Is.Null);
            Assert.That(Factor(at, at.playerId, "deal").evidenceIds, Does.Contain("final-two"));
            // And a final two promise never called in stays there too.
            var promise = Final(FinalTwoDeals.Level); promise.promises.Add(Promise(promise));
            Assert.That(Factor(promise, promise.playerId, "obligation"), Is.Null);
            Assert.That(Factor(promise, promise.playerId, "deal").evidenceIds, Does.Contain("promise:" + promise.hohId + ":" + promise.playerId + ":final_2"));
        }

        [Test]
        public void CountingOnceWritesNothing()
        {
            var at = Final(FinalTwoDeals.Level); at.deals.Add(Deal(at, "final-two", at.hohId, at.playerId)); CallIn(at);
            string before = JsonConvert.SerializeObject(at); uint random = at.randomState;
            EpisodeEngine.FinalChoice(at);
            Assert.That(JsonConvert.SerializeObject(at), Is.EqualTo(before));
            Assert.That(at.randomState, Is.EqualTo(random));
        }

        // ------------------------------------------------------------------ the lapse

        [TestCase(MindRules.Off)] [TestCase(FinalTwoDeals.Level)]
        public void APartnerWhoNominatesThePlayerLapsesTheDealFromTheLevel(int level)
        {
            var s = PartnerHoldsTheNominations(level, out string partner);
            var after = Step(s);
            Assert.That(after.nominees, Does.Contain(after.playerId), "The partner put the player up.");
            var deal = after.deals.Single(d => d.id == "f2");
            if (level == MindRules.Off)
            {
                Assert.That(deal.status, Is.EqualTo(DealStatus.Active), "Below the level a nomination ends no final two deal.");
                Assert.That(after.events.Any(e => e.text == FinalTwoDeals.LapseLine(after.Find(partner).name)), Is.False);
                return;
            }
            Assert.That((deal.status, deal.brokenById, deal.settledWeek), Is.EqualTo((DealStatus.Expired, (string)null, 0)), "It lapses: broken by nobody.");
            Assert.That(after.events.Count(e => e.kind == "deal-outcome" && e.text == FinalTwoDeals.LapseLine(after.Find(partner).name)), Is.EqualTo(1),
                "The player is told once.");
            Assert.That(Entries(after, "deal_broken"), Is.Empty, "No breach is on anybody's record.");
            Assert.That(EndgameCommitments.Read(after, partner, new[] { after.playerId }), Is.Empty, "It weighs nothing in the final choice.");
            Valid(after);
        }

        [Test]
        public void TheLapseDrawsNothingAndMovesNothingElse()
        {
            var below = Step(PartnerHoldsTheNominations(MindRules.Off, out _));
            var at = Step(PartnerHoldsTheNominations(FinalTwoDeals.Level, out string partner));
            Assert.That(at.randomState, Is.EqualTo(below.randomState), "The lapse draws nothing.");
            Assert.That(at.nextSequence, Is.EqualTo(below.nextSequence + 1), "It mints one sequence number: its line's.");
            Assert.That(at.nominees, Is.EqualTo(below.nominees), "The same nominations,");
            Assert.That(JsonConvert.SerializeObject(at.relationships), Is.EqualTo(JsonConvert.SerializeObject(below.relationships)),
                "the same views and records,");
            Assert.That(JsonConvert.SerializeObject(at.memories), Is.EqualTo(JsonConvert.SerializeObject(below.memories)), "the same memories,");
            string line = FinalTwoDeals.LapseLine(at.Find(partner).name);
            Assert.That(at.events.Where(e => e.text != line).Select(e => e.kind + ":" + e.text),
                Is.EqualTo(below.events.Select(e => e.kind + ":" + e.text)), "and the same lines but the lapse's own.");
            Assert.That(at.deals.Where(d => d.id != "f2").Select(d => d.type + ":" + d.status + ":" + d.proposerId + ":" + d.recipientId),
                Is.EqualTo(below.deals.Where(d => d.id != "f2").Select(d => d.type + ":" + d.status + ":" + d.proposerId + ":" + d.recipientId)),
                "No other deal moves.");
        }

        [Test]
        public void TheVetosReplacementNamingThePlayerLapsesItAndANominationOfSomebodyElseDoesNot()
        {
            var s = Final(FinalTwoDeals.Level);
            string partner = s.hohId, other = Other(s);
            s.deals.Add(Deal(s, "f2", partner, s.playerId));
            var named = DealResolution.Verdicts(s, DealResolution.Nominates, partner, new List<string> { s.playerId }).Single();
            Assert.That((named.status, named.actorId, named.lapses), Is.EqualTo((DealStatus.Expired, partner, true)), "A replacement is a nomination.");
            Assert.That(DealResolution.Verdicts(s, DealResolution.Nominates, partner, new List<string> { other }), Is.Empty,
                "Nominating somebody else does not end it,");
            Assert.That(DealResolution.Verdicts(s, DealResolution.Nominates, other, new List<string> { s.playerId }), Is.Empty,
                "nor does somebody else's nomination of the player.");
        }

        [Test]
        public void ADealBetweenTwoHouseguestsAnswersToNoNomination()
        {
            var s = Final(FinalTwoDeals.Level);
            string first = s.hohId, second = Other(s);
            s.deals.Add(Deal(s, "npc-f2", first, second));
            Assert.That(DealResolution.Verdicts(s, DealResolution.Nominates, first, new List<string> { second }), Is.Empty);
            Assert.That(DealResolution.Verdicts(s, DealResolution.Nominates, second, new List<string> { first }), Is.Empty);
        }

        [TestCase(MindRules.Off)] [TestCase(FinalTwoDeals.Level)]
        public void ThePlayerWhoNominatesTheirPartnerBreaksItFromTheLevel(int level)
        {
            var s = Season(29);
            var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            s.phase = EpisodePhase.Nomination; s.hohId = s.playerId;
            EpisodeEngine.EnableCommitments(s);
            s.story.mindRules = level;
            var pool = EpisodeEngine.NominationCandidates(s).Select(c => c.id).ToList();
            string partner = pool[0], other = pool[1];
            s.deals.Add(Deal(s, "f2", s.playerId, partner));
            Valid(s);
            var result = new EpisodeEngine(s).Apply(Command(s, EpisodeCommandKind.Nominate, partner, other));
            Assert.That(result.accepted, Is.True, result.reason);
            var deal = result.state.deals.Single(d => d.id == "f2");
            if (level == MindRules.Off) { Assert.That(deal.status, Is.EqualTo(DealStatus.Active)); return; }
            Assert.That((deal.status, deal.brokenById, deal.settledWeek), Is.EqualTo((DealStatus.Broken, result.state.playerId, result.state.week)),
                "The player broke it.");
            Assert.That(Entries(result.state, "deal_broken").Any(e => e.from == partner && e.to == result.state.playerId), Is.True,
                "The partner holds it against them.");
            Valid(result.state);
        }

        [Test]
        public void TheDecisionScreensDryRunWarnsThePlayerFromTheLevel()
        {
            foreach (int level in new[] { MindRules.Off, FinalTwoDeals.Level })
            {
                var s = Season(29);
                s.phase = EpisodePhase.Nomination; s.hohId = s.playerId;
                EpisodeEngine.EnableCommitments(s);
                s.story.mindRules = level;
                var pool = EpisodeEngine.NominationCandidates(s).Select(c => c.id).ToList();
                s.deals.Add(Deal(s, "f2", s.playerId, pool[0]));
                var verdicts = DealResolution.Verdicts(s, DealResolution.Nominates, s.playerId, new List<string> { pool[0], pool[1] });
                Assert.That(verdicts.Any(v => v.deal.id == "f2" && v.status == DealStatus.Broken), Is.EqualTo(level != MindRules.Off), "level " + level);
            }
        }

        // ------------------------------------------------------------------ no other lapse

        [TestCase(MindRules.Off)] [TestCase(FinalTwoDeals.Level)]
        public void AFinalTwoPaidForAVetoTheHolderThenRefusedNoLongerLapsesFromTheLevel(int level)
        {
            var s = VetoMeeting(87, level);
            var ask = new DealState
            {
                id = NpcDeals.VetoAskPrefix + s.nextSequence++, type = DealKind.VetoUse, proposerId = s.nominees[0], recipientId = s.playerId,
                status = DealStatus.Proposed, week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(DealKind.VetoUse),
            };
            s.deals.Add(ask);
            Valid(s);
            var engine = new EpisodeEngine(s);
            var yes = engine.Apply(Command(engine.Snapshot, EpisodeCommandKind.RespondToDeal, ask.id, null, EpisodeEngine.AcceptDeal));
            Assert.That(yes.accepted, Is.True, yes.reason);
            var price = engine.Snapshot.deals.Single(Negotiation.IsPrice);
            Assert.That((price.type, price.recipientId), Is.EqualTo((DealKind.FinalTwo, s.playerId)), "The nominee paid a final two for the veto.");
            var before = engine.Snapshot;
            var veto = Command(before, EpisodeCommandKind.ResolveVeto, null, EpisodeEngine.ReplacementCandidates(before).First().id);
            veto.useVeto = false;
            var result = engine.Apply(veto);
            Assert.That(result.accepted, Is.True, result.reason);
            var after = result.state;
            Assert.That(after.deals.Single(d => d.id == ask.id).status, Is.EqualTo(DealStatus.Broken), "The player did not use the veto they promised.");
            var owed = after.deals.Single(d => d.id == price.id);
            Assert.That(owed.status, Is.EqualTo(level == MindRules.Off ? DealStatus.Expired : DealStatus.Active),
                level == MindRules.Off ? "Below the level the final two it bought is void." : "From the level the final two stands: only a nomination ends it.");
            Assert.That(Negotiation.Voided(after, owed), Is.EqualTo(level == MindRules.Off));
            Valid(after);
        }

        [Test]
        public void ThePriceRuleKeepsOnlyAFinalTwoAndOnlyFromTheLevel()
        {
            var at = Final(FinalTwoDeals.Level);
            var below = Final(MindRules.Off);
            var finalTwo = Deal(at, "p", at.playerId, at.hohId);
            var vote = Deal(at, "v", at.playerId, at.hohId); vote.type = DealKind.VoteSave;
            Assert.That(FinalTwoDeals.KeepsThePrice(at, finalTwo), Is.True);
            Assert.That(FinalTwoDeals.KeepsThePrice(at, vote), Is.False);
            Assert.That(FinalTwoDeals.KeepsThePrice(below, finalTwo), Is.False);
            var noRules = Final(FinalTwoDeals.Level); noRules.commitmentRulesStartWeek = 0;
            Assert.That(FinalTwoDeals.On(noRules), Is.False, "The level plays only under the commitment rules.");
        }

        // ------------------------------------------------------------------ fixtures

        /// <summary>The final eviction: the player and two houseguests in the house, the rest on the jury, a houseguest at the head of it.</summary>
        private static EpisodeState Final(int level)
        {
            var s = ContentCatalog.Create(337); s.week = 4; s.phase = EpisodePhase.FinalEviction;
            foreach (var person in s.contestants.Skip(3)) person.status = ContestantStatus.Jury;
            s.hohId = s.contestants[1].id; s.finalPart1WinnerId = s.hohId; s.finalPart2WinnerId = s.contestants[2].id;
            s.Find(s.hohId).traits.Clear();
            EpisodeEngine.EnableCommitments(s);
            s.story.mindRules = level;
            return s;
        }

        private static string Other(EpisodeState s) => s.Active.Single(c => !c.isPlayer && c.id != s.hohId).id;

        private static WebVoteFactor Factor(EpisodeState s, string nominee, string code) =>
            EpisodeEngine.FinalChoice(s).nomineeEvaluations.Single(n => n.nomineeId == nominee).factors.FirstOrDefault(f => f.code == code);

        private static double Score(EpisodeState s, string nominee) =>
            EpisodeEngine.FinalChoice(s).nomineeEvaluations.Single(n => n.nomineeId == nominee).score;

        private static DealState Deal(EpisodeState s, string id, string from, string to) => new DealState
        {
            id = id, type = DealKind.FinalTwo, proposerId = from, recipientId = to, status = DealStatus.Active, week = s.week, expiresWeek = 0,
            trustImpact = DealKind.DefaultTrust(DealKind.FinalTwo),
        };

        private static PromiseState Promise(EpisodeState s) => new PromiseState
        { id = "f2-promise", fromId = s.hohId, toId = s.playerId, kind = PromiseKind.FinalTwo, status = PromiseStatus.Active, week = s.week };

        /// <summary>A final two promise the Head of Household made the player, called in by a reminder.</summary>
        private static void CallIn(EpisodeState s)
        {
            s.promises.Add(Promise(s));
            RelationshipLedger.RecordOneWay(s, s.hohId, s.playerId, Negotiation.HeldType(PromiseKind.FinalTwo, Negotiation.Remind), 0, "Called in.");
        }

        private static EpisodeState Season(uint seed, int size = 8) => SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);

        /// <summary>
        /// A houseguest at the head of the house who holds a final two deal with the player and still puts them up: they
        /// like everybody else, and not the player.
        /// </summary>
        private static EpisodeState PartnerHoldsTheNominations(int level, out string partner)
        {
            var s = Season(29);
            string head = s.Active.First(c => !c.isPlayer).id;
            partner = head;
            s.phase = EpisodePhase.Nomination;
            s.hohId = head;
            foreach (var other in s.contestants.Where(c => c.id != head)) SetScore(s, head, other.id, other.isPlayer ? -90 : 60);
            s.deals.Add(Deal(s, "f2", s.playerId, head));
            EpisodeEngine.EnableCommitments(s);
            s.story.mindRules = level;
            Valid(s);
            return s;
        }

        /// <summary>The veto meeting at the endgame, the player holding the veto, two houseguests on the block.</summary>
        private static EpisodeState VetoMeeting(uint seed, int level)
        {
            var s = Season(seed, NpcDeals.EndgameSize);
            s.Find(s.playerId).traits = new List<string>();
            EpisodeEngine.EnableLevers(s);
            EpisodeEngine.EnableCommitments(s);
            EpisodeEngine.EnableWeek(s);
            s.story.mindRules = level;
            var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            s.phase = EpisodePhase.VetoMeeting;
            s.hohId = npcs[0];
            s.nominees = new List<string> { npcs[1], npcs[2] };
            s.vetoHolderId = s.playerId;
            s.vetoPlayers = s.Active.Select(c => c.id).Take(EpisodeEngine.VetoPlayerCount(s.Active.Count())).ToList();
            if (!s.vetoPlayers.Contains(s.vetoHolderId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.vetoHolderId;
            Valid(s);
            return s;
        }

        private static EpisodeState Step(EpisodeState s)
        {
            var engine = new EpisodeEngine(s);
            var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }

        private static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        private static List<(string from, string to)> Entries(EpisodeState s, string type) =>
            s.relationships.SelectMany(r => r.events.Where(e => e.type == type).Select(e => (r.fromId, r.toId))).ToList();

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            new EpisodeCommand
            {
                id = "f2-" + kind + "-" + s.revision, actorId = s.playerId, kind = kind,
                targetId = target, secondTargetId = second, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
    }
}
