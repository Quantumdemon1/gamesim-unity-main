using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Backlog item 21 on the director's side: a profile opened from the vote page or the room map
    /// goes back there by name, and the profile's "From your notes" says a call they followed, as
    /// the notes card does. EditMode: the builders are the director's statics; the built screens
    /// are EpisodePlayModeTests.NotebookRowsProfiles.
    /// </summary>
    public sealed class NotebookRowsProfilesTests
    {
        [Test]
        public void AProfileOpenedFromTheVoteOrTheRoomMap_NamesThatPageOnItsWayBack()
        {
            Assert.That(EpisodeDirector.ProfileBackCaptionFor(EpisodeDirector.NotebookSection.Votes), Is.EqualTo("Back to the vote"));
            Assert.That(EpisodeDirector.ProfileBackCaptionFor(EpisodeDirector.NotebookSection.Rooms), Is.EqualTo("Back to Who is where"));
            Assert.That(EpisodeDirector.ProfilePlace(EpisodeDirector.NotebookSection.Votes), Is.Not.Null, "The vote page opens profiles.");
            Assert.That(EpisodeDirector.ProfilePlace(EpisodeDirector.NotebookSection.Rooms), Is.Not.Null, "The room map opens profiles.");
            Assert.That(WayOut.IsWayBack(EpisodeDirector.ProfileBackCaptionFor(EpisodeDirector.NotebookSection.Votes)), Is.True);
            Assert.That(WayOut.IsWayBack(EpisodeDirector.ProfileBackCaptionFor(EpisodeDirector.NotebookSection.Rooms)), Is.True);
        }

        [Test]
        public void TheProfilesNotes_SayACallTheyFollowed_Once()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 21);
            s.strategyRulesStartWeek = 1; s.blocRulesStartWeek = 1;
            EpisodeEngine.EnableStory(s); EpisodeEngine.EnableRead(s); EpisodeEngine.EnableLevers(s); EpisodeEngine.EnableWeek(s); EpisodeEngine.EnableAgency(s);
            s.week = 4;
            var others = s.contestants.Where(c => !c.isPlayer).ToList();
            var a = others[0]; var c = others[2];
            string first = a.name.Split(' ')[0];
            s.promises.Add(new PromiseState { id = "p1", fromId = a.id, toId = s.playerId, kind = PromiseKind.Safety, status = PromiseStatus.Kept, week = 2 });
            s.ledger.calls.Add(new BlocCallRow { week = 3, allianceId = "alliance-x", callerId = s.playerId, targetId = c.id, followed = new List<string> { a.id } });
            s.alliances.Add(new AllianceState { id = "alliance-x", name = "The Pact", members = new List<string> { s.playerId, a.id }, active = true });
            s.ledger.alliances.Add(new AllianceRow { id = "alliance-x", startedWeek = 2 });

            var lines = EpisodeDirector.ProfileNotesLines(s, a.id, out _);
            string call = "Week 3 · " + first + " followed your call to evict " + c.name;
            Assert.That(lines.Count(l => l == call), Is.EqualTo(1), "The call is on the profile, once.");
            Assert.That(lines.Any(l => l.Contains(first + " promised you")), Is.False, "The promise is in its records, not in its notes.");
            Assert.That(lines, Is.SubsetOf(HouseguestNotes.For(s, a.id).Select(n => n.ToString()).ToList()), "Nothing the notes card does not say.");
        }
    }
}
