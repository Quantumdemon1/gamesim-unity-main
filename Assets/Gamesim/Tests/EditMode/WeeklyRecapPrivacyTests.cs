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
    /// list. Everything else the player is one end of moves the recap as it always did.
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

        private static RelationshipState Edge(EpisodeState s, string from, string to)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            return edge;
        }
    }
}
