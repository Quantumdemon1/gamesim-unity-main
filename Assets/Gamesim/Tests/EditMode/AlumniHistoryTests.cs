#if GAMESIM_ALLSTAR_CANDIDATE
using System;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class AlumniHistoryTests
    {
        public static EpisodeState Fresh(bool histories = true, string player = null, int size = 12)
        {
            var choice = new SeasonBuilder.Choice { Roster = CastTemplates.Roster.AllStars,
                HouseSize = size, PlayerTemplateId = player, PlayerHistories = histories };
            var state = SeasonBuilder.Create(choice, 991);
            ShippedRules.ApplyFresh(state, choice);
            return state;
        }
        private static string Person(EpisodeState state, string name) => state.contestants.Single(c => c.name == name).id;

        [Test]
        public void HistoricalStandingIsSeparateFromCurrentActsAndNewDealTrust()
        {
            var state = Fresh();
            string will = Person(state, "Dr. Will Kirby"), janelle = Person(state, "Janelle Pierzina");
            Assert.That(state.Score(will, janelle), Is.EqualTo(20));
            Assert.That(ThreatAssessment.TrustScore(state, janelle, will), Is.EqualTo(40));
            Assert.That(state.relationships.Single(r => r.fromId == will && r.toId == janelle).events, Is.Empty);
            Assert.That(state.story.grudges, Is.Empty);
            string derrick = Person(state, "Derrick Levasseur"), cody = Person(state, "Cody Calafiore");
            Assert.That(NpcDeals.Between(state, derrick, cody).Count(d => d.type == DealKind.FinalTwo), Is.EqualTo(1));
            Assert.That(ThreatAssessment.TrustScore(state, cody, derrick), Is.EqualTo(81));
            Assert.That(state.alliances, Is.Empty, "Historical ties do not stack an automatic alliance on the final two.");
        }

        [Test]
        public void HistoryOffKeepsCareerReputationsWithoutPersonalStandingOrDeals()
        {
            var state = Fresh(false);
            Assert.That(state.relationships.All(r => r.priorTrustOffset == 0), Is.True);
            Assert.That(state.story.facts.Any(f => f.kind == AlumniHistory.PriorHistory), Is.False);
            Assert.That(state.story.facts.Any(f => f.kind == AlumniHistory.PriorCareer), Is.True);
            Assert.That(state.deals, Is.Empty);
            Assert.That(AlumniStrategy.CareerRisk(state, Person(state, "Dr. Will Kirby")), Is.GreaterThan(0));
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
        }

        [TestCase("derrick-levasseur")]
        [TestCase("cody-calafiore")]
        public void HumanPartnerChoosesAndOfferSlotsCannotRefill(string player)
        {
            var state = Fresh(player: player);
            var offer = state.deals.Single(d => d.recipientId == state.playerId && d.type == DealKind.FinalTwo);
            Assert.That(offer.status, Is.EqualTo(DealStatus.Proposed));
            Assert.That(state.deals.Where(d => d.recipientId == state.playerId && d.id.StartsWith(NpcDeals.OfferPrefix)).Count(), Is.LessThanOrEqualTo(3));
            var command = EpisodeEngineTests.Command(state, EpisodeCommandKind.RespondToDeal);
            command.targetId = offer.id; command.text = "accept";
            var result = new EpisodeEngine(state).Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.deals.Single(d => d.id == offer.id).status, Is.EqualTo(DealStatus.Active));
            Assert.That(EpisodeEngine.SocialActionsSpent(result.state), Is.EqualTo(EpisodeEngine.SocialActionsSpent(state)));
            int count = result.state.deals.Count;
            NpcDeals.Propose(result.state); NpcDeals.Propose(result.state);
            Assert.That(result.state.deals.Count, Is.EqualTo(count));
        }

        [Test]
        public void InitializationNeverReplaysOnReloadAndDoesNotDrawTheSeasonGenerator()
        {
            var choice = new SeasonBuilder.Choice { Roster = CastTemplates.Roster.AllStars, HouseSize = 12 };
            var state = SeasonBuilder.Create(choice, 991); ShippedRules.ApplyFresh(state);
            uint before = state.randomState;
            AlumniHistory.InitializeFresh(state, choice);
            Assert.That(state.randomState, Is.EqualTo(before));
            var copy = state.Clone();
            Assert.Throws<ArgumentException>(() => AlumniHistory.InitializeFresh(copy, choice));
            Assert.That(copy.randomState, Is.EqualTo(before));
            Assert.That(copy.deals.Count, Is.EqualTo(state.deals.Count));
        }

        [Test]
        public void RenamedAlumnusDoesNotInheritTheRealPersonsConnections()
        {
            var choice = new SeasonBuilder.Choice { Roster = CastTemplates.Roster.AllStars, HouseSize = 12 };
            var state = SeasonBuilder.Create(choice, 991);
            string will = Person(state, "Dr. Will Kirby"); state.Find(will).name = "Morgan Example";
            ShippedRules.ApplyFresh(state, choice);
            Assert.That(AlumniHistory.TryProfile(state, will, out _), Is.False);
            Assert.That(state.story.facts.Any(f => f.actorId == will || f.subjectId == will), Is.False);
            Assert.That(state.relationships.Where(r => r.fromId == will || r.toId == will).All(r => r.priorTrustOffset == 0), Is.True);
        }

        [Test]
        public void DuplicateIdentityIsExcludedInsteadOfAssigningAnArbitraryHistoricalPartner()
        {
            var choice = new SeasonBuilder.Choice { Roster = CastTemplates.Roster.AllStars, HouseSize = 8 };
            var state = SeasonBuilder.Create(choice, 991);
            var original = state.contestants.Single(c => c.name == "Dr. Will Kirby");
            var duplicate = original.Clone(); duplicate.id = "duplicate-will";
            state.contestants.Add(duplicate);
            foreach (var person in state.contestants.Where(c => c.id != duplicate.id))
            {
                state.relationships.Add(new RelationshipState { fromId = person.id, toId = duplicate.id });
                state.relationships.Add(new RelationshipState { fromId = duplicate.id, toId = person.id });
            }
            ShippedRules.ApplyFresh(state, choice);
            Assert.That(AlumniHistory.TryProfile(state, original.id, out _), Is.False);
            Assert.That(AlumniHistory.TryProfile(state, duplicate.id, out _), Is.False);
            Assert.That(state.story.facts.Any(f => f.actorId == original.id || f.subjectId == original.id
                || f.actorId == duplicate.id || f.subjectId == duplicate.id), Is.False);
        }

        [Test]
        public void ForgedInheritedStandingCannotClaimAnUnrelatedOrDifferentCataloguePrior()
        {
            var state = Fresh();
            var edge = state.relationships.Single(r => r.fromId == Person(state, "Dr. Will Kirby")
                && r.toId == Person(state, "Janelle Pierzina"));
            edge.priorTrustOffset = 25;
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.False);
            Assert.That(error, Does.Contain("catalogue provenance"));
        }

        [Test]
        public void OrdinaryFactPruningKeepsTheSelectedHistoryEditionAndItsEvidence()
        {
            var state = Fresh();
            var originals = state.story.facts.Select(f => f.id).ToArray();
            while (state.story.facts.Count < Knowledge.Ceiling)
                state.story.facts.Add(new HouseFactState { id = "ordinary-" + state.story.facts.Count,
                    kind = FactKinds.Conversation, actorId = state.playerId, week = 1, visibility = FactVisibility.Private });
            Knowledge.MakeRoom(state);
            Assert.That(state.story.facts.Count, Is.EqualTo(Knowledge.Ceiling - 1));
            Assert.That(originals.All(id => state.story.facts.Any(f => f.id == id)), Is.True);
        }
    }
}
#endif
