using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The weekly recap's movements read only what the player can know (UI-UX-PASS-PLAN decision 4):
    /// an entry whose own words tell a ballot the player cannot place - how a voting bloc with that
    /// houseguest ended - is left out of the week's sum, as the web's history leaves it out of its
    /// list. Everything else the player is one end of moves the recap as it always did. And its
    /// temperature, read from the lines the player was told, counts a pact grown as grown
    /// (ACTIONS-DEALS-ALLIANCES-PLAN C5), never as one formed.
    /// </summary>
    public sealed class WeeklyRecapPrivacyTests
    {
        [Test]
        public void AMovementThatTellsAnUnknownBallotIsLeftOutOfTheWeek()
        {
            var s = ContentCatalog.Create(31);
            s.week = 2;
            var npcs = s.contestants.Where(c => !c.isPlayer).ToArray();
            // Week one's eviction, its count read out and nobody's ballot placed.
            s.ledger.power.Add(new PowerRow
            {
                week = 1, hohId = npcs[0].id, evicteeId = npcs[1].id,
                nominees = new List<string> { npcs[1].id, npcs[2].id }, tally = new List<int> { 2, 1 },
            });
            string you = s.Find(s.playerId).name, them = npcs[3].name;
            string fellOut = you + " and " + them + " fell out over their voting bloc.";
            Assert.That(KnownBallots.TellsAnUnknownBallot(s, npcs[3].id, fellOut, 1), Is.True, "The line tells a ballot the player cannot place.");
            Edge(s, s.playerId, npcs[3].id).events.Add(new RelationshipEventState
                { sequence = s.nextSequence++, week = 1, type = "deal_broken", description = fellOut, impactScore = -25 });
            Edge(s, s.playerId, npcs[4].id).events.Add(new RelationshipEventState
                { sequence = s.nextSequence++, week = 1, type = "confrontation", description = npcs[4].name + " confronted you.", impactScore = -25 });

            var moved = WeeklyRecap.Build(s, 1).relationships;
            Assert.That(moved.Any(m => m.otherId == npcs[3].id), Is.False, "How the bloc ended is not the player's to read, nor its weight.");
            Assert.That(moved.Any(m => m.otherId == npcs[4].id && !m.aboutYou && m.delta == -25), Is.True, "What the player can know still moves the recap.");
        }

        /// <summary>
        /// Somebody the player brought into a pact (C5, "Maya Hassan joined The Riley Pact.") grew it:
        /// the week reads "Alliances growing", and no alliance formed. A pact formed beside it still
        /// reads as one formed, and the join is still one join.
        /// </summary>
        [Test]
        public void AJoinIsCountedAsAJoinNotAFormation()
        {
            var s = ContentCatalog.Create(31);
            s.week = 2;
            var npcs = s.contestants.Where(c => !c.isPlayer).ToArray();
            // Week one is closed: its vote is in.
            s.ledger.power.Add(new PowerRow
            {
                week = 1, hohId = npcs[0].id, evicteeId = npcs[1].id,
                nominees = new List<string> { npcs[1].id, npcs[2].id }, tally = new List<int> { 2, 1 },
            });
            AllianceLine(s, EpisodeEngine.JoinedLine(npcs[4].name, "The Riley Pact"), s.playerId, npcs[3].id, npcs[4].id);
            var joined = WeeklyRecap.Build(s, 1).temperature;
            Assert.That(joined.Select(r => r.label), Does.Contain("Alliances growing"), "A join grows a pact,");
            Assert.That(joined.Select(r => r.label), Does.Not.Contain("Alliances forming"), "and forms none.");
            Assert.That(joined.Single(r => r.label == "Alliances growing").evidence, Is.EqualTo("Somebody joined an alliance of yours."));

            AllianceLine(s, "You and " + npcs[3].name + " formed a private alliance.", s.playerId, npcs[3].id);
            var both = WeeklyRecap.Build(s, 1).temperature;
            Assert.That(both.Single(r => r.label == "Alliances forming").evidence, Is.EqualTo("An alliance formed that you saw."), "A pact formed is one formed,");
            Assert.That(both.Single(r => r.label == "Alliances growing").evidence, Is.EqualTo("Somebody joined an alliance of yours."), "and the join is still one join.");
        }

        /// <summary>A line of week one's, of the alliances' kind, told to these people.</summary>
        private static void AllianceLine(EpisodeState s, string text, params string[] audience) =>
            s.events.Add(new EpisodeEvent
                { sequence = s.nextSequence++, week = 1, phase = EpisodePhase.Social, kind = "alliance", text = text, audienceIds = audience.ToList() });

        private static RelationshipState Edge(EpisodeState s, string from, string to)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            return edge;
        }
    }
}
