using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The All-Star histories and the weekly recap (ALL-STAR-HISTORIES-PLAN §7.3): a houseguest's past-game receipt about
    /// the player is written in week one, but nothing moved that week, so the recap never sums it into a move of their
    /// read of you. Off the alumni rules the same entry is an ordinary one and is summed as every entry always was.
    /// Unity only: the recap is Gamesim.Runtime's.
    /// </summary>
    public sealed class AlumniHistoriesRecapTests
    {
        [Test]
        public void WeekOnesRecapReportsNoPastGameAsAMove()
        {
            var s = AlumniHistoriesTests.AllStars(8, 3201u, "derrick-levasseur");
            const string cody = "cody-calafiore";
            Assert.That(s.relationships.Single(r => r.fromId == cody && r.toId == s.playerId).events.Count(AlumniHistories.IsReceipt), Is.EqualTo(1));
            // Cody's past-game receipt (+15) and an ordinary week-one word (+6) on the same edge: 21 would cross the recap's 20.
            RelationshipLedger.RecordOneWay(s, cody, s.playerId, "talk", 6, "A friendly word.");
            Assert.That(WeeklyRecap.NotableMove, Is.EqualTo(20));
            Assert.That(WeeklyRecap.Build(s, 1).relationships.Where(m => m.otherId == cody && m.aboutYou), Is.Empty,
                "The past game is no move of this week.");
            var off = s.Clone();
            off.alumniRulesVersion = AlumniRules.Off;
            off.alumniCatalogEdition = null;
            Assert.That(WeeklyRecap.Build(off, 1).relationships.Where(m => m.otherId == cody && m.aboutYou).Select(m => m.delta),
                Is.EqualTo(new[] { 21.0 }), "Off the rules the same entry is summed, as every entry always was.");
        }
    }
}
