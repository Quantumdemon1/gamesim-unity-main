using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The player's side of the deal table: what they may ask, what the answer depends on, and
    /// what happens to an offer they are handed.
    ///
    /// <para><see cref="NpcDealTests"/> covers the house bargaining among itself, which spends no
    /// randomness. This half does spend it, and the tests below pin that it spends it exactly once
    /// per question and through the season's own generator, because a replay that draws a different
    /// number is a replay that tells a different story.</para>
    /// </summary>
    public sealed class PlayerDealTests
    {
        // ---------------------------------------------------------------- what may be asked

        [Test]
        public void AVetoCommitmentCanOnlyBeAskedOfWhoeverHoldsTheVeto()
        {
            var state = Season(7u, 8);
            var cast = Npcs(state);
            state.vetoHolderId = cast[1].id;

            Assert.That(PlayerDeals.CanPropose(state, cast[0].id, DealKind.VetoUse, null, out string refusal), Is.False);
            Assert.That(refusal, Does.Contain(cast[0].name));
            Assert.That(PlayerDeals.CanPropose(state, cast[1].id, DealKind.VetoUse, null, out _), Is.True);
        }

        [Test]
        public void AFinalTwoCannotBeAskedWhileTheHouseIsStillFull()
        {
            var state = Season(7u, 10);
            var cast = Npcs(state);
            Assert.That(PlayerDeals.CanPropose(state, cast[0].id, DealKind.FinalTwo, null, out _), Is.False);

            foreach (var evicted in Npcs(state).Skip(NpcDeals.EndgameSize - 1)) evicted.status = ContestantStatus.Jury;
            Assert.That(PlayerDeals.CanPropose(state, Npcs(state)[0].id, DealKind.FinalTwo, null, out _), Is.True);
        }

        [Test]
        public void TheSameArrangementCannotBeAgreedTwiceWithTheSamePerson()
        {
            var state = Season(7u, 8);
            var cast = Npcs(state);
            Assert.That(PlayerDeals.CanPropose(state, cast[0].id, DealKind.Partnership, null, out _), Is.True);

            state.deals.Add(new DealState
            {
                id = "d", type = DealKind.Partnership, proposerId = state.playerId, recipientId = cast[0].id,
                status = DealStatus.Active, week = 1, trustImpact = DealTrust.Medium,
            });
            Assert.That(PlayerDeals.CanPropose(state, cast[0].id, DealKind.Partnership, null, out string refusal), Is.False);
            Assert.That(refusal, Does.Contain(cast[0].name));
            Assert.That(PlayerDeals.CanPropose(state, cast[0].id, DealKind.SafetyAgreement, null, out _), Is.True);
        }

        [Test]
        public void NothingCanBeAskedBeforeTheHousesDealRulesBegin()
        {
            var state = Season(7u, 8);
            state.dealRulesStartWeek = state.week + 1;
            Assert.That(PlayerDeals.Available(state, Npcs(state)[0].id), Is.Empty);
        }

        [Test]
        public void EveryOfferedTypeIsOneTheRulesWouldActuallyAccept()
        {
            var state = Season(7u, 8);
            var target = Npcs(state)[0].id;
            var offered = PlayerDeals.Available(state, target);
            Assert.That(offered, Is.Not.Empty);
            foreach (string type in offered)
            {
                // A target agreement is the one type that needs a subject before it is a question at
                // all, so it is checked against one rather than against nobody.
                string about = type == DealKind.TargetAgreement
                    ? PlayerDeals.Subjects(state, target).First() : null;
                Assert.That(PlayerDeals.CanPropose(state, target, type, about, out string why), Is.True, type + ": " + why);
            }
        }

        /// <summary>
        /// A deal about somebody is offered when there is somebody it could be about, and not
        /// otherwise — the list used to answer no every time, which made the control unreachable.
        /// </summary>
        [Test]
        public void ADealAboutSomebodyIsOfferedWhenThereIsSomebodyToNameAndNotOtherwise()
        {
            var state = Season(7u, 8);
            var target = Npcs(state)[0].id;
            Assert.That(PlayerDeals.Available(state, target), Does.Contain(DealKind.TargetAgreement));
            Assert.That(PlayerDeals.Subjects(state, target),
                Is.EquivalentTo(Npcs(state).Where(c => c.id != target).Select(c => c.id)));

            foreach (var other in Npcs(state).Where(c => c.id != target)) other.status = ContestantStatus.Jury;
            Assert.That(PlayerDeals.Subjects(state, target), Is.Empty);
            Assert.That(PlayerDeals.Available(state, target), Does.Not.Contain(DealKind.TargetAgreement));
        }

        // ---------------------------------------------------------------- what they would say

        [Test]
        public void TheAnswerIsNeverCertainInEitherDirection()
        {
            var state = Season(7u, 8);
            var cast = Npcs(state);
            foreach (double score in new[] { -100d, -60, -20, 0, 20, 50, 80, 100 })
            {
                Set(state, cast[0].id, state.playerId, score);
                double chance = PlayerDeals.AcceptanceChance(state, cast[0].id, DealKind.Partnership, null);
                Assert.That(chance, Is.InRange(PlayerDeals.MinimumChance, PlayerDeals.MaximumChance), score.ToString());
            }
        }

        /// <summary>Warmth is the dominant term, and the tiers join up rather than stepping.</summary>
        [Test]
        public void WarmerHouseguestsAreAlwaysAtLeastAsWillingAsColderOnes()
        {
            var state = Season(7u, 8);
            var npc = Npcs(state)[0];
            double previous = 0;
            for (double score = -100; score <= 100; score += 5)
            {
                Set(state, npc.id, state.playerId, score);
                double chance = PlayerDeals.AcceptanceChance(state, npc.id, DealKind.SafetyAgreement, null);
                Assert.That(chance, Is.GreaterThanOrEqualTo(previous - 0.001), "at " + score);
                previous = chance;
            }
        }

        [Test]
        public void BreakingDealsMakesTheWholeHouseHarderToBargainWith()
        {
            var state = Season(7u, 8);
            var cast = Npcs(state);
            Set(state, cast[0].id, state.playerId, 40);
            double clean = PlayerDeals.AcceptanceChance(state, cast[0].id, DealKind.Partnership, null);

            state.deals.Add(new DealState
            {
                id = "b", type = DealKind.Partnership, proposerId = state.playerId, recipientId = cast[1].id,
                status = DealStatus.Broken, week = 1, trustImpact = DealTrust.Medium,
            });
            double stained = PlayerDeals.AcceptanceChance(state, cast[0].id, DealKind.Partnership, null);

            Assert.That(clean - stained, Is.EqualTo(PlayerDeals.BrokenDealPenalty).Within(0.001),
                "Somebody who has broken one deal is twelve points harder to bargain with, "
                + "and the person they broke it with is not the only one who notices.");
        }

        /// <summary>Asking somebody to turn on their own ally is the hardest thing on the table.</summary>
        [Test]
        public void NobodyWillReadilyAgreeToTargetTheirOwnAlly()
        {
            var state = Season(7u, 8);
            var cast = Npcs(state);
            Set(state, cast[0].id, state.playerId, 60);
            double stranger = PlayerDeals.AcceptanceChance(state, cast[0].id, DealKind.TargetAgreement, cast[2].id);

            state.alliances.Add(new AllianceState
            {
                id = "a", name = "The Pact", members = new List<string> { cast[0].id, cast[2].id },
            });
            double ally = PlayerDeals.AcceptanceChance(state, cast[0].id, DealKind.TargetAgreement, cast[2].id);

            Assert.That(ally, Is.LessThan(stranger));
        }

        [Test]
        public void ATraitMovesTheAnswerByTheReferencesOwnNumber()
        {
            Assert.That(PlayerDeals.TraitModifier(new[] { "Loyal" }, DealKind.FinalTwo), Is.EqualTo(20));
            Assert.That(PlayerDeals.TraitModifier(new[] { "Loyal" }, DealKind.TargetAgreement), Is.EqualTo(-10));
            Assert.That(PlayerDeals.TraitModifier(new[] { "Sneaky" }, DealKind.InformationSharing), Is.EqualTo(10),
                "Fifteen for the type, less the five a sneaky houseguest takes off everything.");
            Assert.That(PlayerDeals.TraitModifier(new[] { "Sneaky" }, DealKind.Partnership), Is.EqualTo(-5));
            Assert.That(PlayerDeals.TraitModifier(new[] { "Paranoid" }, DealKind.SafetyAgreement), Is.EqualTo(-5));
            Assert.That(PlayerDeals.TraitModifier(new[] { "Analytical" }, DealKind.TargetAgreement), Is.EqualTo(15));
            Assert.That(PlayerDeals.TraitModifier(new[] { "Loyal", "Emotional" }, DealKind.FinalTwo), Is.EqualTo(45),
                "Two traits both apply; the reference sums them.");
            Assert.That(PlayerDeals.TraitModifier(new[] { "Unheard Of" }, DealKind.Partnership), Is.Zero);
        }

        // ---------------------------------------------------------------- asking, through the engine

        [Test]
        public void AskingSpendsExactlyOneDrawFromTheSeasonsGenerator()
        {
            var engine = Engine(out var state, 60);
            var target = Npcs(state)[0];
            uint before = engine.Snapshot.randomState;

            var result = engine.Apply(Propose(engine.Snapshot, target.id, DealKind.Partnership));
            Assert.That(result.accepted, Is.True, result.reason);

            var after = engine.Snapshot;
            var expected = new SeededRandom(before);
            expected.NextDouble();
            Assert.That(after.randomState, Is.Not.EqualTo(before), "The answer is a roll.");
            // The relationship change that follows also draws, so the check is that the FIRST draw
            // was the answer: replaying it from the recorded state reproduces the same verdict.
            double chance = PlayerDeals.AcceptanceChance(state, target.id, DealKind.Partnership, null);
            bool agreed = new SeededRandom(before).NextDouble() * 100 < chance;
            Assert.That(after.deals.Any(d => d.proposerId == after.playerId && d.recipientId == target.id),
                Is.EqualTo(agreed));
        }

        [Test]
        public void TheSameSeasonAskedTheSameQuestionGetsTheSameAnswer()
        {
            var first = Engine(out var a, 60);
            var second = Engine(out var b, 60);
            var target = Npcs(a)[0].id;

            first.Apply(Propose(first.Snapshot, target, DealKind.Partnership));
            second.Apply(Propose(second.Snapshot, target, DealKind.Partnership));

            Assert.That(first.Snapshot.randomState, Is.EqualTo(second.Snapshot.randomState));
            CollectionAssert.AreEqual(
                first.Snapshot.deals.Select(d => d.type + ":" + d.status).ToList(),
                second.Snapshot.deals.Select(d => d.type + ":" + d.status).ToList());
        }

        [Test]
        public void AskingForSomethingTheRulesForbidIsRefusedWithoutSpendingAnything()
        {
            var engine = Engine(out var state, 60);
            var target = Npcs(state)[0];
            uint before = engine.Snapshot.randomState;
            int deals = engine.Snapshot.deals.Count;

            var result = engine.Apply(Propose(engine.Snapshot, target.id, DealKind.VetoUse));
            Assert.That(result.accepted, Is.False);
            Assert.That(result.reason, Does.Contain(target.name));
            Assert.That(engine.Snapshot.randomState, Is.EqualTo(before), "A refused command spends nothing.");
            Assert.That(engine.Snapshot.deals.Count, Is.EqualTo(deals));
        }

        [Test]
        public void AskingIsASocialActionAndDrawsOnTheSameBudget()
        {
            var engine = Engine(out var state, 60);
            int spent = EpisodeEngine.SocialActionsSpent(engine.Snapshot);

            var result = engine.Apply(Propose(engine.Snapshot, Npcs(state)[0].id, DealKind.Partnership));
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(EpisodeEngine.SocialActionsSpent(engine.Snapshot), Is.EqualTo(spent + 1));
        }

        /// <summary>A warm house says yes often enough that the control is worth having.</summary>
        [Test]
        public void AWarmHouseAgreesMoreOftenThanACoolOne()
        {
            Assert.That(Agreements(70), Is.GreaterThan(Agreements(-40)));
            Assert.That(Agreements(70), Is.GreaterThan(20), "Out of forty warm asks, most should land.");
            Assert.That(Agreements(-40), Is.LessThan(20));
        }

        // ---------------------------------------------------------------- being asked

        [Test]
        public void HouseguestsPutThingsToThePlayerAndThoseOffersBindNobodyYet()
        {
            var state = Warm(Season(4u, 10), 60);
            state.week = 2;
            NpcDeals.Propose(state);

            var offers = NpcDeals.Pending(state);
            Assert.That(offers, Is.Not.Empty);
            Assert.That(offers.Count, Is.LessThanOrEqualTo(NpcDeals.ProposalsPerWeek));
            Assert.That(offers, Has.All.Matches<DealState>(d => d.status == DealStatus.Proposed
                                                                && d.recipientId == state.playerId));
            CollectionAssert.AllItemsAreUnique(offers.Select(d => d.type).ToList());
            CollectionAssert.AllItemsAreUnique(offers.Select(d => d.proposerId).ToList());
        }

        [Test]
        public void AnsweringPromptlyDoesNotInviteMoreQuestionsTheSameWeek()
        {
            var state = Warm(Season(4u, 10), 60);
            state.week = 2;
            NpcDeals.Propose(state);
            int asked = NpcDeals.Pending(state).Count;
            Assert.That(asked, Is.GreaterThan(0));

            foreach (var offer in NpcDeals.Pending(state)) offer.status = DealStatus.Declined;
            NpcDeals.Propose(state);
            Assert.That(NpcDeals.Pending(state), Is.Empty, "Clearing the table must not refill it.");

            state.week = 3;
            NpcDeals.Propose(state);
            Assert.That(NpcDeals.Pending(state), Is.Not.Empty, "A new week is a new round.");
        }

        /// <summary>Somebody on the block asks before somebody suggesting a comfortable arrangement.</summary>
        [Test]
        public void ANomineeBeggingIsHeardBeforeAnybodyElse()
        {
            Assert.That(NpcDeals.Urgency(DealKind.VetoUse), Is.GreaterThan(NpcDeals.Urgency(DealKind.VoteSave)));
            Assert.That(NpcDeals.Urgency(DealKind.VoteSave), Is.GreaterThan(NpcDeals.Urgency(DealKind.Partnership)));

            var state = Warm(Season(4u, 10), 20);
            var cast = Npcs(state);
            state.hohId = cast[0].id;
            state.nominees = new List<string> { cast[1].id, cast[2].id };
            state.vetoHolderId = state.playerId;
            state.week = 2;
            // The nominee is the coolest person in the house who will still deal at all, so only
            // urgency can put them first. The other nominee is below the floor and asks nothing,
            // which keeps the one veto commitment on the table theirs.
            Set(state, cast[1].id, state.playerId, 12);
            Set(state, cast[2].id, state.playerId, NpcDeals.DealingFloor - 1);

            NpcDeals.Propose(state);
            var offers = NpcDeals.Pending(state);
            Assert.That(offers, Is.Not.Empty);
            Assert.That(offers[0].type, Is.EqualTo(DealKind.VetoUse));
            Assert.That(offers[0].proposerId, Is.EqualTo(cast[1].id));
        }

        [Test]
        public void AcceptingAnOfferMakesItBindAndDecliningOneCostsTheAsker()
        {
            foreach (bool accept in new[] { true, false })
            {
                var engine = Engine(out var state, 60);
                var snapshot = engine.Snapshot;
                var asker = Npcs(snapshot)[0];
                var offer = Ask(engine, asker.id, DealKind.Partnership);

                double before = engine.Snapshot.Score(asker.id, snapshot.playerId);
                var result = engine.Apply(Respond(engine.Snapshot, offer.id, accept));
                Assert.That(result.accepted, Is.True, result.reason);

                var after = engine.Snapshot;
                var settled = after.deals.Single(d => d.id == offer.id);
                Assert.That(settled.status, Is.EqualTo(accept ? DealStatus.Active : DealStatus.Declined));
                Assert.That(after.Score(asker.id, after.playerId),
                    accept ? Is.GreaterThan(before) : Is.LessThan(before));
            }
        }

        [Test]
        public void AnOfferCanOnlyBeAnsweredOnce()
        {
            var engine = Engine(out var state, 60);
            var offer = Ask(engine, Npcs(engine.Snapshot)[0].id, DealKind.Partnership);

            Assert.That(engine.Apply(Respond(engine.Snapshot, offer.id, true)).accepted, Is.True);
            var again = engine.Apply(Respond(engine.Snapshot, offer.id, false));
            Assert.That(again.accepted, Is.False);
            Assert.That(again.reason, Does.Contain("no longer on the table"));
        }

        [Test]
        public void AnsweringAnOfferCostsNoSocialAction()
        {
            var engine = Engine(out var state, 60);
            var offer = Ask(engine, Npcs(engine.Snapshot)[0].id, DealKind.Partnership);
            int spent = EpisodeEngine.SocialActionsSpent(engine.Snapshot);

            Assert.That(engine.Apply(Respond(engine.Snapshot, offer.id, false)).accepted, Is.True);
            Assert.That(EpisodeEngine.SocialActionsSpent(engine.Snapshot), Is.EqualTo(spent),
                "The offer was somebody else's move; declining it should not cost the player a turn.");
        }

        [Test]
        public void ADealThePlayerAgreedIsOneTheEvictionVoteCanSee()
        {
            var engine = Engine(out var state, 60);
            var cast = Npcs(engine.Snapshot);
            var asker = cast[0];
            var offer = Ask(engine, asker.id, DealKind.Partnership);
            Assert.That(engine.Apply(Respond(engine.Snapshot, offer.id, true)).accepted, Is.True);

            // The evaluator needs a block to read; what is under test is whether the deal reaches it.
            var final = engine.Snapshot;
            final.nominees = new List<string> { cast[1].id, cast[2].id };
            var seen = WebEvictionVoting.FromNative(final, asker.id).state.deals;
            Assert.That(seen.Any(d => d.id == offer.id && d.status == DealStatus.Active), Is.True);
        }

        /// <summary>
        /// A question nobody answered does not stand all season, and one that is answered runs for
        /// as long as the arrangement itself runs for.
        /// </summary>
        [Test]
        public void AnUnansweredOfferLapsesButAnAcceptedOneKeepsItsOwnTerm()
        {
            var state = Warm(Season(4u, 10), 60);
            state.week = 2;
            NpcDeals.Propose(state);
            var offered = NpcDeals.Pending(state);
            Assert.That(offered, Is.Not.Empty);
            Assert.That(offered, Has.All.Matches<DealState>(d => d.expiresWeek == state.week),
                "An offer is this week's question, whatever the deal behind it would run for.");

            state.week = 3;
            NpcDeals.Settle(state);
            Assert.That(NpcDeals.Pending(state), Is.Empty, "Last week's unanswered questions have lapsed.");

            var engine = Engine(out var live, 60);
            var offer = Ask(engine, Npcs(engine.Snapshot)[0].id, DealKind.Partnership);
            Assert.That(engine.Apply(Respond(engine.Snapshot, offer.id, true)).accepted, Is.True);
            Assert.That(engine.Snapshot.deals.Single(d => d.id == offer.id).expiresWeek, Is.Zero,
                "A partnership is open-ended once it is agreed.");
        }

        [Test]
        public void ASeasonFullOfOffersStaysValid()
        {
            var state = Warm(Season(6u, 12), 60);
            for (int week = 1; week <= 30; week++)
            {
                state.week = week;
                NpcDeals.Settle(state);
                NpcDeals.Propose(state);
            }
            Assert.That(state.deals.Count, Is.LessThanOrEqualTo(NpcDeals.DealCeiling));
            Assert.That(EpisodeValidation.TryValidate(state, out string error), Is.True, error);
        }

        // ---------------------------------------------------------------- fixtures

        private static EpisodeState Season(uint seed, int size) =>
            SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);

        private static List<ContestantState> Npcs(EpisodeState state) =>
            state.Active.Where(c => !c.isPlayer).ToList();

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

        /// <summary>An engine parked in the social week with the house at a chosen warmth.</summary>
        private static EpisodeEngine Engine(out EpisodeState state, double warmth)
        {
            state = Warm(Season(7u, 10), warmth);
            state.phase = EpisodePhase.Social;
            state.dealRulesStartWeek = 1;
            return new EpisodeEngine(state);
        }

        private static EpisodeCommand Propose(EpisodeState s, string to, string type, string about = null) =>
            new EpisodeCommand
            {
                id = "cmd-" + s.revision, kind = EpisodeCommandKind.ProposeDeal,
                actorId = s.playerId, targetId = to, secondTargetId = about, text = type,
                expectedRevision = s.revision, expectedPhase = s.phase,
            };

        private static EpisodeCommand Respond(EpisodeState s, string dealId, bool accept) =>
            new EpisodeCommand
            {
                id = "cmd-" + s.revision, kind = EpisodeCommandKind.RespondToDeal,
                actorId = s.playerId, targetId = dealId,
                text = accept ? EpisodeEngine.AcceptDeal : "decline",
                expectedRevision = s.revision, expectedPhase = s.phase,
            };

        /// <summary>
        /// Puts an offer on the table by hand.
        ///
        /// <para>The engine files these at phase transitions, and driving a whole week to reach one
        /// would make every test below depend on how that week went. What is being tested here is
        /// the answer, not the arrival — <see cref="HouseguestsPutThingsToThePlayerAndThoseOffersBindNobodyYet"/>
        /// covers the arrival.</para>
        /// </summary>
        private static DealState Ask(EpisodeEngine engine, string fromId, string type)
        {
            var state = Field(engine);
            var offer = new DealState
            {
                id = "deal-ask-fixture", type = type, proposerId = fromId, recipientId = state.playerId,
                status = DealStatus.Proposed, week = state.week, trustImpact = DealKind.DefaultTrust(type),
            };
            state.deals.Add(offer);
            return offer;
        }

        private static EpisodeState Field(EpisodeEngine engine) => (EpisodeState)typeof(EpisodeEngine)
            .GetField("current", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(engine);

        /// <summary>How many of forty asks at this warmth are agreed.</summary>
        private static int Agreements(double warmth)
        {
            int agreed = 0;
            for (uint seed = 1; seed <= 40; seed++)
            {
                var state = Warm(Season(seed, 10), warmth);
                state.phase = EpisodePhase.Social;
                state.dealRulesStartWeek = 1;
                var engine = new EpisodeEngine(state);
                var target = Npcs(engine.Snapshot)[0];
                engine.Apply(Propose(engine.Snapshot, target.id, DealKind.Partnership));
                if (engine.Snapshot.deals.Any(d => d.proposerId == state.playerId)) agreed++;
            }
            return agreed;
        }
    }
}
