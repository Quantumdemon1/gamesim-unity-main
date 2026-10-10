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

        /// <summary>
        /// Vote family V7: the week's recap - its deals, its turning points, the feelings that moved - and the house's count of the
        /// week's events read the player's log and record, where a Rule2 incident is its owner's line. That line can tell a ballot:
        /// "You honoured a vote to evict with ..." is written only where the partner's hidden ballot kept the deal too. Under mode 2
        /// the flip pair reads alike in every one of them; with the player told and the reveal judged, the recap tells the keep.
        /// </summary>
        [Test]
        public void TheRecapAndTheHouseCountTellAKeptVoteDealOnlyWhereThePartnersBallotIsKnown()
        {
            var blind = ModeTwoReaderSweep.Flip(false);
            int week = blind.Kept.week;
            string keep = blind.Kept.Find(blind.Kept.playerId).name + " honoured a " + DealKind.Title(DealKind.VoteEvict).ToLowerInvariant()
                + " with " + blind.Kept.Find(blind.PartnerId).name + ".";
            Assert.That(blind.Kept.events.Any(e => e.week == week && e.kind == "deal-outcome" && e.text == keep && e.audienceIds.Contains(blind.Kept.playerId)),
                Is.True, "Fixture: the reveal wrote the keep to the player's log.");
            Assert.That(blind.Kept.week, Is.EqualTo(blind.Broken.week));
            blind.AssertBlind("The recap's deals", s => WeeklyRecap.Build(s, week).deals);
            blind.AssertBlind("The recap's turning points", s => WeeklyRecap.Build(s, week).moments);
            blind.AssertBlind("The recap's feelings", s => WeeklyRecap.Build(s, week).relationships.Select(m => (m.otherId, m.aboutYou, m.delta)).ToList());
            blind.AssertBlind("The house's count", s => { var read = HouseVibe.Of(s); return new[] { read.Activity, read.Commitments, read.GameStakes }; });
            Assert.That(WeeklyRecap.Build(blind.Kept, week).deals, Has.None.EqualTo(keep), "The recap leaves the keep out.");

            var told = ModeTwoReaderSweep.Flip(true);
            Assert.That(WeeklyRecap.Build(told.Kept, week).deals, Has.Some.EqualTo(keep.Replace(blind.Kept.Find(blind.PartnerId).name,
                told.Kept.Find(told.PartnerId).name)), "Told and judged, the recap tells the keep.");
            told.AssertControl("The house's count", s => { var read = HouseVibe.Of(s); return new[] { read.Activity, read.Commitments, read.GameStakes }; });
        }
    }
}
