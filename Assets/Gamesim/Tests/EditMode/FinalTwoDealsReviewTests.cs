using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The review's fixes to a final two deal at minds level 1 (<see cref="FinalTwoDeals"/>): the decision screen's dry run
    /// (<see cref="CommitmentsRead.WouldBreak"/>) warns the player before they nominate their partner or name them as the
    /// veto's replacement; and a replacement the Head of Household had no choice about - at the final four, always -
    /// neither breaks nor lapses the deal. Each case runs the level beside level 0, which plays as the season always did.
    /// </summary>
    public sealed class FinalTwoDealsReviewTests
    {
        [TestCase(MindRules.Off)] [TestCase(FinalTwoDeals.Level)]
        public void TheDryRunWarnsBeforeThePlayerNominatesTheirPartner(int level)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 29);
            s.phase = EpisodePhase.Nomination; s.hohId = s.playerId;
            EpisodeEngine.EnableCommitments(s);
            s.story.mindRules = level;
            var pool = EpisodeEngine.NominationCandidates(s).Select(c => c.id).ToList();
            s.deals.Add(Deal(s, pool[0]));
            Valid(s);
            var warned = CommitmentsRead.WouldBreak(s, CommitmentsRead.Decision.Nominate(pool[0], pool[1]));
            Assert.That(warned.Any(b => b.id == "f2"), Is.EqualTo(level != MindRules.Off), "Naming the partner is warned of from the level.");
            Assert.That(CommitmentsRead.WouldBreak(s, CommitmentsRead.Decision.Nominate(pool[1], pool[2])).Any(b => b.id == "f2"), Is.False,
                "Naming two others breaks nothing.");
        }

        [TestCase(MindRules.Off)] [TestCase(FinalTwoDeals.Level)]
        public void TheDryRunWarnsBeforeThePlayerNamesTheirPartnerAsTheReplacement(int level)
        {
            var s = VetoMeeting(level, 4, out string partner, out string holder);
            var decision = CommitmentsRead.Decision.Veto(true, holder, partner);
            Assert.That(CommitmentsRead.WouldBreak(s, decision).Any(b => b.id == "f2"), Is.EqualTo(level != MindRules.Off),
                "With somebody else to name, naming the partner is warned of from the level,");
            var after = Apply(s, holder, partner);
            Assert.That(after.deals.Single(d => d.id == "f2").status, Is.EqualTo(level == MindRules.Off ? DealStatus.Active : DealStatus.Broken),
                "and the engine breaks it as warned.");
        }

        [TestCase(MindRules.Off)] [TestCase(FinalTwoDeals.Level)]
        public void AReplacementThePlayerHadNoChoiceAboutEndsNothing(int level)
        {
            var s = VetoMeeting(level, 3, out string partner, out string holder);
            Assert.That(s.Active.Count(), Is.EqualTo(4), "The final four.");
            Assert.That(EpisodeEngine.ReplacementCandidates(s).Select(c => c.id), Is.EqualTo(new[] { partner }), "The partner is the only name left.");
            Assert.That(CommitmentsRead.WouldBreak(s, CommitmentsRead.Decision.Veto(true, holder, partner)), Is.Empty, "Nothing to warn of,");
            var after = Apply(s, holder, partner);
            Assert.That(after.nominees, Does.Contain(partner));
            Assert.That(after.deals.Single(d => d.id == "f2").status, Is.EqualTo(DealStatus.Active), "and the deal stands.");
            Assert.That(after.relationships.Any(r => r.fromId == partner && r.toId == after.playerId && r.events.Any(e => e.type == "deal_broken")),
                Is.False, "The partner holds nothing against the player.");
            Valid(after);
        }

        [Test]
        public void AReplacementThePartnerHadNoChoiceAboutDoesNotLapseIt()
        {
            foreach (int npcs in new[] { 3, 4 })
            {
                // The partner at the head of the house names the player in the saved holder's place: the state as the
                // engine reads it after the replacement.
                var s = VetoMeeting(FinalTwoDeals.Level, npcs, out string partner, out string holder);
                string other = s.nominees.Single(id => id != holder);
                s.hohId = partner;
                s.nominees = new List<string> { other, s.playerId };
                s.Find(s.playerId).nominationWeeks.Add(s.week);
                var verdicts = DealResolution.Verdicts(s, DealResolution.Nominates, partner, new List<string> { s.playerId });
                if (npcs == 3) Assert.That(verdicts, Is.Empty, "At the final four the player was the only name left: the deal stands.");
                else Assert.That(verdicts.Single().lapses, Is.True, "With somebody else to name, naming the player lapses it.");
            }
        }

        // ------------------------------------------------------------------ fixtures

        /// <summary>
        /// The veto meeting with the player at the head of the house and a final two deal with a houseguest: a houseguest
        /// holds the veto on the block beside another, and <paramref name="npcs"/> houseguests are left in the house (three
        /// is the final four).
        /// </summary>
        private static EpisodeState VetoMeeting(int level, int npcs, out string partner, out string holder)
        {
            var s = ContentCatalog.Create(337); s.week = 5;
            var house = s.contestants.Where(c => !c.isPlayer).Select(c => c.id).Take(npcs).ToList();
            foreach (var person in s.contestants.Where(c => !c.isPlayer && !house.Contains(c.id))) person.status = ContestantStatus.Jury;
            holder = house[0]; partner = house[2];
            s.phase = EpisodePhase.VetoMeeting; s.hohId = s.playerId;
            s.nominees = new List<string> { house[0], house[1] };
            foreach (var id in s.nominees) { var guest = s.Find(id); guest.timesNominated++; guest.nominationWeeks.Add(s.week); }
            s.vetoHolderId = holder;
            s.vetoPlayers = s.Active.Select(c => c.id).ToList();
            s.deals.Add(Deal(s, partner));
            EpisodeEngine.EnableCommitments(s);
            s.story.mindRules = level;
            Valid(s);
            return s;
        }

        /// <summary>The houseguest holding the veto saves themselves, and the player names <paramref name="replacement"/>.</summary>
        private static EpisodeState Apply(EpisodeState s, string saved, string replacement)
        {
            Assert.That(EpisodeEngine.NpcVetoSave(s), Is.EqualTo(saved), "The holder saves themselves.");
            var command = new EpisodeCommand
            {
                id = "f2r-veto-" + s.revision, actorId = s.playerId, kind = EpisodeCommandKind.ResolveVeto, useVeto = true,
                targetId = saved, secondTargetId = replacement, expectedRevision = s.revision, expectedPhase = s.phase,
            };
            var result = new EpisodeEngine(s).Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }

        private static DealState Deal(EpisodeState s, string partner) => new DealState
        {
            id = "f2", type = DealKind.FinalTwo, proposerId = s.playerId, recipientId = partner, status = DealStatus.Active, week = s.week,
            expiresWeek = 0, trustImpact = DealKind.DefaultTrust(DealKind.FinalTwo),
        };

        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
    }
}
