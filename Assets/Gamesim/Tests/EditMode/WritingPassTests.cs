using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The Tier 5 writing pass: every template set carries more than one phrasing, and the choice
    /// of phrasing is deterministic and roll-free. A line is presentation; the season's randomness
    /// is not spent on it, so a seeded season plays out identically however it is worded.
    /// </summary>
    public sealed class WritingPassTests
    {
        [Test]
        public void AGreetingChangesWithTheWeekAndComesBackRound()
        {
            var state = ContentCatalog.Create(412);
            var first = Weeks(state, 1);
            var second = Weeks(state, 2);
            var third = Weeks(state, 3);
            Assert.That(second, Is.Not.EqualTo(first), "Week two must not repeat week one.");
            Assert.That(third, Is.Not.EqualTo(first).And.Not.EqualTo(second), "Week three has its own words.");
            Assert.That(Weeks(state, 4), Is.EqualTo(first), "Three variants, then round again.");
        }

        [Test]
        public void EveryVoiceHasThreePhrasingsOfTheOpening()
        {
            var state = ContentCatalog.Create(412);
            foreach (var npc in state.contestants.Where(actor => !actor.isPlayer))
            {
                var lines = new[] { 1, 2, 3 }.Select(week => { state.week = week; return HouseDialogue.Greeting(state, npc.id); }).ToArray();
                Assert.That(lines.Distinct().Count(), Is.EqualTo(3), npc.id + " should greet differently across three weeks.");
                Assert.That(lines, Has.All.Not.Empty);
            }
            state.week = 1;
        }

        [Test]
        public void WordingNeverTouchesTheSeason()
        {
            // The same seed, whatever week the lines are read at: the state is untouched by reading.
            var state = ContentCatalog.Create(7);
            string snapshot = Newtonsoft.Json.JsonConvert.SerializeObject(state);
            for (int week = 1; week <= 3; week++)
            {
                var probe = ContentCatalog.Create(7);
                probe.week = week;
                HouseDialogue.Greeting(probe, ContentCatalog.MayaId);
                HouseDialogue.EvictionPlea(probe, ContentCatalog.MayaId);
                probe.week = 1;
                Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(probe), Is.EqualTo(snapshot));
            }
        }

        [Test]
        public void ASituationIsWordedByWeekWithItsMechanicsUnchanged()
        {
            foreach (var template in HouseEvents.Catalog)
            {
                Assert.That(template.alternatives, Is.Not.Null.And.Length.EqualTo(2), template.id + " carries two more phrasings.");
                var words = new[] { 1, 2, 3 }.Select(week => HouseEvents.Narrative(template.narrative, template.alternatives, week)).ToArray();
                Assert.That(words.Distinct().Count(), Is.EqualTo(3), template.id);
                Assert.That(HouseEvents.Narrative(template.narrative, template.alternatives, 4), Is.EqualTo(template.narrative));
                foreach (var role in template.roles)
                    Assert.That(words, Has.All.Contain(role), template.id + ": every phrasing names the same people.");
            }
            foreach (var template in Storylines.Catalog)
            {
                Assert.That(template.alternatives, Is.Not.Null.And.Length.EqualTo(2), template.id);
                foreach (var role in template.roles)
                    Assert.That(template.alternatives, Has.All.Contain(role), template.id + ": every phrasing names the same people.");
            }
        }

        [Test]
        public void AJurorsReasonVariesBySeatAndKeepsItsPoint()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 5u);
            var cast = state.contestants.Where(c => !c.isPlayer).ToList();
            var played = cast[0]; var liked = cast[1];
            played.hohWins = 4; played.vetoWins = 3; played.timesNominated = 3; played.stats.strategic = 9;
            liked.stats.strategic = 2;
            var reasons = cast.Skip(2).Take(3).Select(juror =>
            {
                foreach (var edge in state.relationships.Where(r => r.fromId == juror.id && r.toId == liked.id)) edge.score = 60;
                foreach (var edge in state.relationships.Where(r => r.fromId == juror.id && r.toId == played.id)) edge.score = 10;
                return WebJuryVoting.Reason(state, juror.id, played, liked);
            }).ToArray();
            Assert.That(reasons, Has.All.Contain("better game"), "The point survives every phrasing.");
            Assert.That(reasons.Distinct().Count(), Is.EqualTo(3), "Three jurors in three seats give it three ways.");
        }

        private static string Weeks(EpisodeState state, int week)
        {
            state.week = week;
            string line = HouseDialogue.Greeting(state, ContentCatalog.MayaId);
            state.week = 1;
            return line;
        }
    }
}
