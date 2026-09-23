using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The episode screen's house status: one line, each fact only when it is so, in the order the
    /// week gives them - the HoH, then the veto, then the nominees.
    /// </summary>
    public sealed class HouseStatusTests
    {
        [Test]
        public void HouseStatus_IsOneLineOfWhatIsSo()
        {
            var state = ContentCatalog.Create(20260910);
            Assert.That(EpisodeDirector.HouseStatus(state), Is.Null, "Nobody holds anything before the first competition.");

            var cast = state.contestants.Where(c => !c.isPlayer).ToArray();
            state.hohId = cast[0].id;
            Assert.That(EpisodeDirector.HouseStatus(state), Is.EqualTo("HoH: " + cast[0].name));

            state.nominees.Add(cast[1].id);
            state.nominees.Add(state.playerId);
            state.vetoHolderId = cast[2].id;
            Assert.That(EpisodeDirector.HouseStatus(state), Is.EqualTo("HoH: " + cast[0].name
                + "  \u00b7  Veto holder: " + cast[2].name
                + "  \u00b7  Nominees: " + cast[1].name + " and " + state.Find(state.playerId).name));
        }
    }
}
