using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Vote family V5f, the runtime readers - the editor's only, since the Unity-free subset builds no Runtime: the goodbye's
    /// tone (<see cref="EpisodeDirector.GoodbyeTone"/>), the weekly recap's carried word and the standalone verification's deal
    /// lookups read mode 2 as they read mode 1. The fixtures are <see cref="ModeTwoPageReaderTests"/>' own, which the subset
    /// holds to their premises (a struck vote deal, a bloc the box did not decide).
    /// </summary>
    public sealed class ModeTwoRuntimeReaderTests
    {
        [Test]
        public void TheGoodbyeOfAHouseguestAVoteDealBindsIsDealtInBothModes()
        {
            var (mode1, mode2, partner, id) = ModeTwoPageReaderTests.StruckVoteDeal();
            Assert.That(mode2.deals.Any(d => d.id == id), Is.False, "Fixture: mode 2 holds the deal canonical only.");
            Assert.That(EpisodeDirector.GoodbyeTone(mode1, partner), Is.EqualTo(EpisodeDirector.GoodbyeKind.Dealt), "Fixture: mode 1's deal binds them.");
            Assert.That(EpisodeDirector.GoodbyeTone(mode2, partner), Is.EqualTo(EpisodeDirector.GoodbyeKind.Dealt), "Mode 2's canonical deal binds them alike.");
        }

        [Test]
        public void TheRecapCarriesAVoteRowTheBoxDidNotDecideInBothModes()
        {
            var (reveal, hoh, id) = ModeTwoPageReaderTests.UndecidedBloc();
            int week = reveal.Mode2.week;
            var one = WeeklyRecap.Build(reveal.Mode1, week);
            var two = WeeklyRecap.Build(reveal.Mode2, week);
            Assert.That(one.closed && two.closed, Is.True, "Fixture: the week has closed.");
            Assert.That(one.whatsNext.Any(line => line.StartsWith("You carry")), Is.True, "Fixture: mode 1 carries the bloc into the week.");
            Assert.That(two.whatsNext, Is.EqualTo(one.whatsNext), "Mode 2 carries its canonical bloc alike.");
        }

        [Test]
        public void TheVerificationFindsACanonicalVoteDeal()
        {
            var (mode1, mode2, partner, id) = ModeTwoPageReaderTests.StruckVoteDeal();
            Assert.That(mode2.deals.Any(d => d.id == id), Is.False, "Fixture: the raw list does not hold it.");
            var found = PortVerification.SeasonDeal(mode2, id);
            Assert.That(found, Is.Not.Null, "The verification's lookup finds the canonical deal.");
            Assert.That(found.status, Is.EqualTo(DealStatus.Active));
            Assert.That(ModeTwoReaderSweep.Json(found), Is.EqualTo(ModeTwoReaderSweep.Json(PortVerification.SeasonDeal(mode1, id))));
        }
    }
}
