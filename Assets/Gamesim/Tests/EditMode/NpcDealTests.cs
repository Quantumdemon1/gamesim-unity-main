using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Houseguests striking bargains.
    ///
    /// <para>The test that matters most is <see cref="TheEvictionVoteCanFinallySeeADeal"/>. Everything
    /// else here checks that deals are made sensibly; that one checks the reason they exist at all —
    /// the vote has weighed deals since it was written and has never had one to weigh.</para>
    /// </summary>
    public sealed class NpcDealTests
    {
        // ---------------------------------------------------------------- the point of the exercise

        /// <summary>
        /// A deal changes how somebody votes. Before this system there was no way to make that true.
        /// </summary>
        [Test]
        public void TheEvictionVoteCanFinallySeeADeal()
        {
            var state = Nominated(out string voter, out string nominee, out string other);

            var before = WebEvictionVoting.FromNative(state, voter);
            Assert.That(before.state.deals, Is.Empty, "Nothing has been agreed yet.");

            state.deals.Add(new DealState
            {
                id = "deal-1", type = DealKind.VoteSave, proposerId = nominee, recipientId = voter,
                targetId = nominee, status = DealStatus.Active, week = state.week, expiresWeek = state.week,
                trustImpact = DealKind.DefaultTrust(DealKind.VoteSave),
            });

            var after = WebEvictionVoting.FromNative(state, voter);
            Assert.That(after.state.deals, Has.Count.EqualTo(1),
                "FromNative never copied deals across, so every deal weight in the evaluator "
                + "was multiplying an empty list.");
            Assert.That(after.state.deals[0].type, Is.EqualTo(DealKind.VoteSave));
            Assert.That(after.state.deals[0].targetHouseguestId, Is.EqualTo(nominee));
        }

        // ---------------------------------------------------------------- the ladder

        [Test]
        public void NobodyDealsWithSomebodyTheyBarelyKnow()
        {
            var state = Season(5u, 8);
            var cast = Npcs(state);
            Set(state, cast[0].id, cast[1].id, NpcDeals.DealingFloor - 1);
            Assert.That(NpcDeals.Offer(state, cast[0].id, cast[1].id), Is.Null);
        }

        /// <summary>
        /// A broken deal is read as fifteen points of coldness by everybody, for the rest of the
        /// season. Two of them and a warm relationship stops being warm enough to bargain over.
        /// </summary>
        [Test]
        public void BreakingYourWordFollowsYouIntoEveryLaterNegotiation()
        {
            var state = Season(5u, 8);
            var cast = Npcs(state);
            Set(state, cast[0].id, cast[1].id, 42);
            Assert.That(NpcDeals.Adjusted(state, cast[0].id, cast[1].id), Is.EqualTo(42).Within(0.001));

            state.deals.Add(Broken(cast[1].id, cast[2].id, "d1"));
            state.deals.Add(Broken(cast[1].id, cast[3].id, "d2"));

            Assert.That(NpcDeals.BrokenDeals(state, cast[1].id), Is.EqualTo(2));
            Assert.That(NpcDeals.Adjusted(state, cast[0].id, cast[1].id),
                Is.EqualTo(42 - 2 * NpcDeals.BrokenDealPenalty).Within(0.001));
            Assert.That(NpcDeals.Offer(state, cast[0].id, cast[1].id), Is.Null,
                "Twelve adjusted is above the floor but below every rung of the ladder.");
        }

        /// <summary>Desperation outranks comfort: the block is bargained from before anything else.</summary>
        [Test]
        public void ANomineeBegsTheVetoHolderBeforeAnybodyNegotiatesComfortably()
        {
            var state = Nominated(out string voter, out string nominee, out _);
            state.vetoHolderId = voter;
            Set(state, nominee, voter, 80);
            Set(state, voter, nominee, 80);

            Assert.That(NpcDeals.Offer(state, nominee, voter), Is.EqualTo(DealKind.VetoUse),
                "Eighty would otherwise be a final two; the block outranks it.");
        }

        [Test]
        public void ANomineeWithoutTheVetoHolderAsksForAVote()
        {
            var state = Nominated(out string voter, out string nominee, out _);
            state.vetoHolderId = null;
            Set(state, nominee, voter, NpcDeals.VoteSaveWarmth + 5);
            Assert.That(NpcDeals.Offer(state, nominee, voter), Is.EqualTo(DealKind.VoteSave));
        }

        /// <summary>
        /// A vote deal names whoever it protects, and the evaluator reads that id directly. Pointing
        /// it at the other nominee would have the voter obliged to save the wrong person.
        /// </summary>
        [Test]
        public void AVoteDealNamesTheNomineeItIsMeantToKeep()
        {
            var state = Season(7u, 8);
            var cast = Npcs(state);
            state.hohId = cast[0].id;
            state.nominees = new List<string> { cast[1].id, cast[2].id };
            state.vetoHolderId = null;
            state.dealRulesStartWeek = 1;
            // Only the first nominee is warm enough to ask; the second cannot deal at all, so the
            // one offer on the table is unambiguously theirs.
            foreach (var edge in state.relationships) edge.score = NpcDeals.DealingFloor - 1;
            Set(state, cast[1].id, state.playerId, NpcDeals.VoteSaveWarmth + 5);

            NpcDeals.Propose(state);

            var deal = state.deals.Single();
            Assert.That(deal.type, Is.EqualTo(DealKind.VoteSave));
            Assert.That(deal.proposerId, Is.EqualTo(cast[1].id));
            Assert.That(deal.targetId, Is.EqualTo(cast[1].id),
                "The deal protects whoever asked for it, not the other nominee.");

            // And the evaluator reads that id as the obligation it is.
            var seen = WebEvictionVoting.FromNative(state, state.playerId).state.deals.Single();
            Assert.That(seen.targetHouseguestId, Is.EqualTo(cast[1].id));
        }

        /// <summary>
        /// A competition beast draws a coalition even when nobody has fallen out with them — two wins
        /// and one cool reading is enough, which is the source's second route to a common threat.
        /// </summary>
        [Test]
        public void TwoCompetitionWinsMakeSomebodyEverybodysProblem()
        {
            var state = Season(5u, 8);
            var cast = Npcs(state);
            var beast = cast[2];
            beast.hohWins = 1; beast.vetoWins = 1;
            Set(state, cast[0].id, cast[1].id, NpcDeals.CommonThreatWarmth + 5);
            foreach (var c in cast) Set(state, cast[0].id, c.id, c.id == cast[1].id ? NpcDeals.CommonThreatWarmth + 5 : 10);

            Assert.That(NpcDeals.CommonThreat(state, cast[0].id, cast[1].id), Is.EqualTo(beast.id));
            Assert.That(NpcDeals.Offer(state, cast[0].id, cast[1].id), Is.EqualTo(DealKind.TargetAgreement));
        }

        [Test]
        public void APartnershipAndASafetyPactBecomeAnAllianceInvitation()
        {
            var state = Season(5u, 8);
            var cast = Npcs(state);
            Set(state, cast[0].id, cast[1].id, NpcDeals.UpgradeWarmth + 5);
            state.deals.Add(Active(cast[0].id, cast[1].id, DealKind.Partnership, "p"));
            state.deals.Add(Active(cast[0].id, cast[1].id, DealKind.SafetyAgreement, "s"));

            Assert.That(NpcDeals.Offer(state, cast[0].id, cast[1].id), Is.EqualTo(DealKind.AllianceInvite));
        }

        [Test]
        public void OnlyTheSneakyAndTheStrategicThinkToTradeInformation()
        {
            var state = Season(5u, 8);
            var cast = Npcs(state);
            foreach (var edge in state.relationships) edge.score = NpcDeals.InformationWarmth + 2;

            var schemer = cast.First(c => c.traits.Any(t => t == "Sneaky" || t == "Strategic"));
            var plain = cast.FirstOrDefault(c => !c.traits.Any(t => t == "Sneaky" || t == "Strategic"));
            Assert.That(plain, Is.Not.Null, "The pool should hold somebody who is neither.");

            Assert.That(NpcDeals.Offer(state, schemer.id, plain.id), Is.EqualTo(DealKind.InformationSharing));
            Assert.That(NpcDeals.Offer(state, plain.id, schemer.id), Is.Null,
                "Thirty-two clears the information rung and nothing below it.");
        }

        /// <summary>
        /// The endgame rung is below partnership, so a pair reach it only once they are already
        /// partners — and then only when the house is small enough for the question to be real.
        /// </summary>
        [Test]
        public void AFinalTwoNeedsBothAnExistingPartnershipAndASmallHouse()
        {
            var big = Warm(Season(5u, 10), NpcDeals.FinalTwoWarmth + 10);
            var bigCast = Npcs(big);
            big.deals.Add(Active(bigCast[0].id, bigCast[1].id, DealKind.Partnership, "p"));
            Assert.That(NpcDeals.Offer(big, bigCast[0].id, bigCast[1].id), Is.Not.EqualTo(DealKind.FinalTwo),
                "A full house is too early to be asking — whatever else they might suggest.");

            var small = Season(5u, 10);
            foreach (var evicted in small.contestants.Where(c => !c.isPlayer).Skip(NpcDeals.EndgameSize - 1))
                evicted.status = ContestantStatus.Jury;
            Warm(small, NpcDeals.FinalTwoWarmth + 10);
            var smallCast = Npcs(small);
            small.deals.Add(Active(smallCast[0].id, smallCast[1].id, DealKind.Partnership, "p"));

            Assert.That(small.Active.Count(), Is.EqualTo(NpcDeals.EndgameSize));
            Assert.That(NpcDeals.Offer(small, smallCast[0].id, smallCast[1].id), Is.EqualTo(DealKind.FinalTwo));
        }

        /// <summary>A warm pair with nothing else in place become partners, and nothing higher.</summary>
        [Test]
        public void AWarmPairBecomePartnersBeforeTheyBecomeAnythingElse()
        {
            var state = Warm(Season(5u, 10), NpcDeals.FinalTwoWarmth + 10);
            var cast = Npcs(state);
            Assert.That(NpcDeals.Offer(state, cast[0].id, cast[1].id), Is.EqualTo(DealKind.Partnership),
                "Partnership sits above the endgame on the ladder, and the order is the rule.");
        }

        // ---------------------------------------------------------------- the pass

        [Test]
        public void SettlingSpendsNoRandomness()
        {
            var state = Warm(Season(7u, 10), 60);
            uint before = state.randomState;
            NpcDeals.Settle(state);
            Assert.That(state.randomState, Is.EqualTo(before));
        }

        [Test]
        public void TheSameHouseStrikesTheSameBargainsEveryTime()
        {
            var first = Warm(Season(11u, 10), 60);
            var second = Warm(Season(11u, 10), 60);
            NpcDeals.Settle(first);
            NpcDeals.Settle(second);
            CollectionAssert.AreEqual(Digest(first), Digest(second));
        }

        [Test]
        public void NobodyStrikesTwoBargainsInOneWeek()
        {
            var state = Warm(Season(9u, 12), 60);
            NpcDeals.Settle(state);
            var proposers = state.deals.Select(d => d.proposerId).ToList();
            CollectionAssert.AreEqual(proposers.Distinct().OrderBy(x => x).ToList(),
                proposers.OrderBy(x => x).ToList());
        }

        /// <summary>The player is never party to a bargain they were not asked about.</summary>
        [Test]
        public void ThePlayerIsNeverGivenADealTheyDidNotAgreeTo()
        {
            var state = Warm(Season(3u, 10), 70);
            for (int week = 1; week <= 6; week++) { state.week = week; NpcDeals.Settle(state); }

            Assert.That(state.deals.Any(d => d.proposerId == state.playerId || d.recipientId == state.playerId),
                Is.False);
        }

        [Test]
        public void ADealThatRunsOutIsNotADealAnybodyBroke()
        {
            var state = Season(3u, 8);
            var cast = Npcs(state);
            var weekly = Active(cast[0].id, cast[1].id, DealKind.VoteSave, "w");
            weekly.expiresWeek = 1;
            var openEnded = Active(cast[2].id, cast[3].id, DealKind.FinalTwo, "o");
            state.deals.Add(weekly);
            state.deals.Add(openEnded);

            state.week = 3;
            NpcDeals.Settle(state);

            Assert.That(weekly.status, Is.EqualTo(DealStatus.Expired));
            Assert.That(openEnded.status, Is.EqualTo(DealStatus.Active), "Zero means open-ended, not overdue.");
            Assert.That(state.relationships.SelectMany(r => r.events).Any(e => e.type == "deal_broken"),
                Is.False, "Expiring is not betrayal, and nothing should say it was.");
        }

        [Test]
        public void AHouseBeforeItsRulesBeginStrikesNothing()
        {
            var state = Warm(Season(4u, 10), 70);
            state.dealRulesStartWeek = state.week + 1;
            long sequence = state.nextSequence;
            NpcDeals.Settle(state);
            Assert.That(state.deals, Is.Empty);
            Assert.That(state.nextSequence, Is.EqualTo(sequence));
        }

        [Test]
        public void ThirtyWeeksOfBargainingLeaveAValidSeason()
        {
            var state = Warm(Season(12u, 12), 65);
            for (int week = 1; week <= 30; week++) { state.week = week; NpcDeals.Settle(state); }

            Assert.That(state.deals.Count, Is.LessThanOrEqualTo(NpcDeals.DealCeiling));
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
        }

        /// <summary>
        /// A warm house played through the engine strikes bargains without anybody asking it to —
        /// the pass is wired into the week, not something a test has to call.
        /// </summary>
        [Test]
        public void ASeasonPlayedThroughTheEngineStrikesItsOwnDeals()
        {
            var engine = new EpisodeEngine(Warm(ContentCatalog.Create(7u), 60));
            for (int guard = 0; guard < 400 && engine.Snapshot.phase != EpisodePhase.Finished; guard++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }

            var finished = engine.Snapshot;
            Assert.That(finished.deals.Any(d => d.id.StartsWith("deal-npc-")), Is.True,
                "The house should have agreed something over a whole season.");
            // The house may ASK the player things, and did — NpcDeals.Propose files those. What it
            // must never do is answer for them: nothing the player never accepted may be binding.
            Assert.That(finished.deals.Where(d => d.proposerId == finished.playerId
                                                  || d.recipientId == finished.playerId),
                Has.All.Matches<DealState>(d => d.status != DealStatus.Active),
                "A season played without answering a single offer must leave none of them in force.");
            Assert.That(EpisodeValidation.TryValidate(finished, out var error), Is.True, error);
        }

        // ---------------------------------------------------------------- fixtures

        private static EpisodeState Season(uint seed, int size) =>
            SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);

        private static List<ContestantState> Npcs(EpisodeState state) =>
            state.Active.Where(c => !c.isPlayer).ToList();

        private static EpisodeState Nominated(out string voter, out string nominee, out string other)
        {
            var state = Season(7u, 8);
            var cast = Npcs(state);
            state.hohId = cast[0].id;
            state.nominees = new List<string> { cast[1].id, cast[2].id };
            nominee = cast[1].id;
            other = cast[2].id;
            voter = cast[3].id;
            return state;
        }

        private static DealState Active(string a, string b, string kind, string id) => new DealState
        {
            id = id, type = kind, proposerId = a, recipientId = b,
            status = DealStatus.Active, week = 1, trustImpact = DealKind.DefaultTrust(kind),
        };

        private static DealState Broken(string a, string b, string id) => new DealState
        {
            id = id, type = DealKind.Partnership, proposerId = a, recipientId = b,
            status = DealStatus.Broken, week = 1, trustImpact = DealTrust.Medium,
        };

        private static EpisodeState Warm(EpisodeState state, double score)
        {
            foreach (var edge in state.relationships) edge.score = score;
            return state;
        }

        private static void Set(EpisodeState state, string from, string to, double score)
        {
            foreach (var edge in state.relationships.Where(r => r.fromId == from && r.toId == to))
                edge.score = score;
        }

        private static List<string> Digest(EpisodeState state) =>
            state.deals.Select(d => d.proposerId + ">" + d.recipientId + ":" + d.type + ":" + d.status)
                .OrderBy(x => x).ToList();
    }
}
