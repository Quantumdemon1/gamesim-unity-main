using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The context card's role line (UI-UX-PASS-PLAN U0): before anybody is Head of Household
    /// nothing is decided - it said "You are safe this week" in week one's free time, under a top
    /// bar saying "Awaiting HoH" - and once the roles are out the line is the week's.
    /// </summary>
    public sealed class FreeTimeContextTests
    {
        /// <summary>A house of six in free time with no role held by anybody.</summary>
        private static EpisodeState House()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 6 }, 7);
            state.phase = EpisodePhase.Social;
            state.hohId = null;
            state.vetoHolderId = null;
            state.nominees = new List<string>();
            return state;
        }

        [Test]
        public void BeforeAnybodyIsHeadOfHouseholdNothingIsDecided()
        {
            var state = House();
            Assert.That(EpisodeDirector.ContextRole(state, out string why), Is.EqualTo(EpisodeDirector.NothingDecidedRole));
            Assert.That(why, Is.EqualTo(EpisodeDirector.NothingDecidedWhy));
            Assert.That(EpisodeDirector.ContextRole(state, out _), Does.Not.Contain("safe"), "Nobody is safe before the first competition.");
        }

        [Test]
        public void OnceTheRolesAreOutTheLineIsTheWeeks()
        {
            var state = House();
            var others = state.contestants.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            state.hohId = others[0];
            Assert.That(EpisodeDirector.ContextRole(state, out _), Is.EqualTo("You are safe this week"), "Safe once somebody else is Head of Household.");
            state.nominees = new List<string> { state.playerId, others[1] };
            Assert.That(EpisodeDirector.ContextRole(state, out _), Is.EqualTo("You are on the block"));
            state.nominees = new List<string> { others[1], others[2] };
            state.vetoHolderId = state.playerId;
            Assert.That(EpisodeDirector.ContextRole(state, out _), Is.EqualTo("You hold the veto"));
            state.hohId = state.playerId;
            Assert.That(EpisodeDirector.ContextRole(state, out _), Is.EqualTo("You are HOH"), "The Head of Household's line outranks the veto's.");
        }
    }
}
