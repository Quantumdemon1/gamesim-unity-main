using System;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// A free talk is not contact (talk rules 1, ruling R5's arcless path): whatever its warmth, it leaves the player's and the
    /// housemate's edges as they were dated, so it never shows an ally's pact terms in the vote read or turns on the "gone quiet"
    /// notes (<see cref="Allegiance.CommitmentKnown"/>). A neutral talk never moved the scores at all; a warm or a cold one must
    /// not count either. Simulation only, so the Unity-free subset runs it.
    /// </summary>
    public sealed class FreeTalkEdgeTouchTests
    {
        [TestCase(1)] [TestCase(-1)]
        public void AWarmOrColdFreeTalkLeavesTheEdgesUndated(int landing)
        {
            using (TalkBank.Use(FreeTalkFixture.Table()))
            {
                var s = FreeTalkFixture.Fresh();
                Assert.That(s.week, Is.GreaterThanOrEqualTo(1), "A fresh season starts in week 1.");
                foreach (var other in FreeTalkFixture.Npcs(s)) { FreeTalkFixture.SetScore(s, s.playerId, other.id, 0); FreeTalkFixture.SetScore(s, other.id, s.playerId, 0); }
                foreach (var edge in s.relationships.Where(r => r.fromId == s.playerId || r.toId == s.playerId)) edge.lastInteractionWeek = 0;
                Assert.That(FreeTalkFixture.TryFind(s, landing, null, out string npc, out string tone), Is.True, "A tone that lands at " + landing);
                var engine = new EpisodeEngine(s);
                var before = engine.Snapshot;
                bool knownBefore = Allegiance.CommitmentKnown(before, npc);

                Assert.That(FreeTalkFixture.Talk(engine, npc, tone).accepted, Is.True);
                var after = engine.Snapshot;
                double mine = after.Score(after.playerId, npc);
                Assert.That(Math.Sign(mine), Is.EqualTo(landing), "The warmth moved, so the score path ran.");

                var mineEdge = after.relationships.Single(r => r.fromId == after.playerId && r.toId == npc);
                var theirEdge = after.relationships.Single(r => r.fromId == npc && r.toId == after.playerId);
                Assert.That(mineEdge.lastInteractionWeek, Is.EqualTo(0), "The player's edge is not dated by a free talk.");
                Assert.That(theirEdge.lastInteractionWeek, Is.EqualTo(0), "The housemate's edge is not dated by a free talk.");
                Assert.That(Allegiance.CommitmentKnown(after, npc), Is.EqualTo(knownBefore), "A free talk shows no pact terms.");
            }
        }
    }
}
