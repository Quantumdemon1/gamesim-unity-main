using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The story's ceremonies (plan §5.1, Fallout) are registered everywhere a ceremony is: every
    /// kind through every table, so a kind added to <see cref="StoryLog.Fallout"/> and missed in one
    /// of them fails here rather than playing a blank card.
    /// </summary>
    public sealed class StoryFalloutTests
    {
        /// <summary>The rooms the house builds: its room markers' names.</summary>
        private static readonly string[] HouseRooms = { "Bedroom", "Games", "HoH", "Kitchen", "Living", "Nomination", "Private", "Yard" };

        [Test]
        public void EveryStoryCeremonyIsInEveryTable()
        {
            foreach (var kind in StoryLog.Fallout)
            {
                Assert.That(StoryFallout.IsFallout(kind), Is.True, kind);
                Assert.That(CeremonyTakeover.TitleFor(kind), Is.Not.Null.And.Not.Empty, kind + " has a title");
                Assert.That(CeremonyTakeover.FlavourFor(kind), Is.Not.Empty, kind + " has a line");
                Assert.That(UiTheme.Icon(StoryFallout.IconFor(kind)), Is.Not.Null, kind + "'s mark loads");
                Assert.That(StoryFallout.BadgeFor(kind), Is.Not.Null.And.Not.Empty, kind + " badges its faces");
                Assert.That(HouseRooms, Does.Contain(EpisodeDirector.CeremonyRoom(kind)), kind + " is framed in a room the house builds");
            }
        }

        [Test]
        public void TheShowsOwnCeremoniesKeepTheirOwnTables()
        {
            foreach (var kind in new[] { CeremonySting.NominationKind, CeremonySting.VetoKind, CeremonySting.EvictionKind, CeremonySting.WinnerKind })
            {
                Assert.That(StoryFallout.IsFallout(kind), Is.False, kind);
                Assert.That(StoryFallout.TitleFor(kind), Is.Null, kind);
                Assert.That(CeremonyTakeover.TitleFor(kind), Is.Not.Null, kind);
            }
            Assert.That(CeremonyTakeover.TitleFor("an unknown kind"), Is.Null, "A kind nobody registered still gets no card.");
        }

        [Test]
        public void AStoryCeremonyCountsAsAGameMove()
        {
            var state = StorySeasonTests.StorySeason(3);
            int before = HouseVibe.Of(state).GameStakes;
            foreach (var kind in StoryLog.Fallout)
                state.events.Add(new EpisodeEvent { sequence = state.nextSequence++, week = state.week, phase = state.phase, kind = kind, text = "x" });
            Assert.That(HouseVibe.Of(state).GameStakes, Is.EqualTo(before + StoryLog.Fallout.Length));
        }

        [Test]
        public void EveryVenueMapsToARoomTheHouseBuildsOrToNone()
        {
            foreach (var venue in StoryVenues.All)
            {
                var room = EpisodeDirector.VenueRoom(venue);
                if (room != null) Assert.That(HouseRooms, Does.Contain(room), venue);
                // And back: the engine reads the room a walk-in names as the venue it is.
                if (room != null) Assert.That(StoryVenues.ForRoom(room), Is.EqualTo(venue), room);
            }
            foreach (var room in HouseRooms)
                Assert.That(StoryVenues.ForRoom(room), Is.Not.Null, room + " is a venue, or a room no venue names.");
            // A meeting is the house in one room, so the room has to exist.
            foreach (var arc in StoryCatalog.All)
                foreach (var beat in arc.beats.Where(b => b.surface == StorySurfaces.Meeting))
                    Assert.That(EpisodeDirector.VenueRoom(beat.venue), Is.Not.Null, arc.id + "/" + beat.id + " meets in a room the house builds.");
        }
    }
}
