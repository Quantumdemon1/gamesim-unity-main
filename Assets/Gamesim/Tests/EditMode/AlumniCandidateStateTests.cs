#if GAMESIM_ALLSTAR_CANDIDATE
using System;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>Private candidate invariants; these do not release schema 29 or qualify historical saves.</summary>
    public sealed class AlumniCandidateStateTests
    {
        private static SeasonBuilder.Choice Choice(bool histories) => new SeasonBuilder.Choice
        {
            Roster = CastTemplates.Roster.AllStars, HouseSize = 12, PlayerHistories = histories,
        };

        private static EpisodeState Fresh(bool histories)
        {
            var choice = Choice(histories);
            var state = SeasonBuilder.Create(choice, 947u);
            ShippedRules.ApplyFresh(state, choice);
            return state;
        }

        [Test]
        public void CandidateChoiceDefaultsToHistoryAndCopyKeepsIt()
        {
            var choice = new SeasonBuilder.Choice();
            Assert.That(choice.PlayerHistories, Is.True);
            choice.PlayerHistories = false;
            var copy = choice.Copy();
            choice.PlayerHistories = true;
            Assert.That(copy.PlayerHistories, Is.False);
        }

        [Test]
        public void OrdinaryFreshSetupDoesNotActivateTheCandidate()
        {
            var state = SeasonBuilder.Create(Choice(true), 947u);
            ShippedRules.ApplyFresh(state);
            Assert.That(state.schemaVersion, Is.EqualTo(29));
            Assert.That(state.alumniRulesVersion, Is.Zero);
            Assert.That(state.alumniCatalogVersion, Is.Null.Or.Empty);
            Assert.That(state.playerHistoriesEnabled, Is.False);
            Assert.That(state.relationships.All(edge => edge.priorTrustOffset == 0), Is.True);
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CandidateClonePreservesConfigurationAndDetachedEdges(bool histories)
        {
            var state = Fresh(histories);
            var copy = state.Clone();
            Assert.That(copy.alumniRulesVersion, Is.EqualTo(state.alumniRulesVersion));
            Assert.That(copy.alumniCatalogVersion, Is.EqualTo(AlumniCatalog.Version));
            Assert.That(copy.playerHistoriesEnabled, Is.EqualTo(histories));
            Assert.That(copy.relationships.Select(edge => edge.priorTrustOffset),
                Is.EqualTo(state.relationships.Select(edge => edge.priorTrustOffset)));
            Assert.That(copy.relationships[0], Is.Not.SameAs(state.relationships[0]));
            double before = state.relationships[0].priorTrustOffset;
            copy.relationships[0].priorTrustOffset = before + 1;
            Assert.That(state.relationships[0].priorTrustOffset, Is.EqualTo(before));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(30.01)]
        [TestCase(-30.01)]
        public void CandidateRejectsNonFiniteOrUnboundedPriorTrust(double offset)
        {
            var state = Fresh(true);
            state.relationships[0].priorTrustOffset = offset;
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.False);
            Assert.That(error, Does.Contain("inherited trust"));
        }

        [Test]
        public void HistoryOffRefusesAStoredNonzeroHistoryOffset()
        {
            var state = Fresh(false);
            Assert.That(state.relationships.All(edge => edge.priorTrustOffset == 0), Is.True);
            state.relationships[0].priorTrustOffset = 1;
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.False);
            Assert.That(error, Does.Contain("inherited trust"));
        }

        [Test]
        public void CandidateCannotAcceptAnUnallocatedHistoricalSchemaAsCurrent()
        {
            var state = Fresh(true);
            state.schemaVersion = 28;
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.False);
            Assert.That(error, Does.Contain("private alumni candidate schema"));
        }

        [Test]
        public void UnsupportedCatalogueIsRefusedWithoutChangingTheCandidate()
        {
            var state = Fresh(true);
            state.alumniCatalogVersion = "not-the-audited-catalogue";
            var before = state.Clone();
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False);
            Assert.That(state.randomState, Is.EqualTo(before.randomState));
            Assert.That(state.nextSequence, Is.EqualTo(before.nextSequence));
            Assert.That(state.revision, Is.EqualTo(before.revision));
            Assert.That(state.alumniCatalogVersion, Is.EqualTo(before.alumniCatalogVersion));
            Assert.That(state.relationships.Select(edge => edge.score), Is.EqualTo(before.relationships.Select(edge => edge.score)));
        }
    }
}
#endif
