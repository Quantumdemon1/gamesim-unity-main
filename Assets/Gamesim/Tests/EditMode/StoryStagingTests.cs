using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Where a story happens (plan §5): which Advance lets which beat pass, the venues the house
    /// stages beats in, and the Fallout lines that give a moment its ceremony card.
    /// </summary>
    public sealed class StoryStagingTests
    {
        private static EpisodeState Season(uint seed = 3, int size = 8) => StorySeasonTests.StorySeason(seed, size);

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind) => EpisodeEngineTests.Command(s, kind);

        private static EpisodeState Apply(EpisodeState s, EpisodeCommand c)
        {
            var result = new EpisodeEngine(s).Apply(c);
            Assert.That(result.accepted, Is.True, c.kind + ": " + result.reason);
            return result.state;
        }

        private static List<ContestantState> Npcs(EpisodeState s) => s.contestants.Where(c => !c.isPlayer).ToList();

        [Test]
        public void EachAdvanceNamesTheAnchorItCloses()
        {
            var s = Season();
            s.phase = EpisodePhase.Social;
            Assert.That(EpisodeEngine.AdvanceCloses(s), Is.EqualTo(StoryAnchors.SocialClose));
            s.phase = EpisodePhase.Campaign;
            Assert.That(EpisodeEngine.AdvanceCloses(s), Is.EqualTo(StoryAnchors.EvictionEve));
            s.phase = EpisodePhase.HoH; s.competitionResolved = false;
            Assert.That(EpisodeEngine.AdvanceCloses(s), Is.Null, "Playing the competition closes nothing.");
            s.competitionResolved = true;
            Assert.That(EpisodeEngine.AdvanceCloses(s), Is.EqualTo(StoryAnchors.HohCrowned));
            s.phase = EpisodePhase.Veto;
            Assert.That(EpisodeEngine.AdvanceCloses(s), Is.EqualTo(StoryAnchors.VetoWon));
            s.phase = EpisodePhase.VetoMeeting; s.vetoResolved = false;
            Assert.That(EpisodeEngine.AdvanceCloses(s), Is.Null, "The veto decision comes first.");
            s.vetoResolved = true;
            Assert.That(EpisodeEngine.AdvanceCloses(s), Is.EqualTo(StoryAnchors.BlockSet));
            s.phase = EpisodePhase.Nomination; s.hohId = s.playerId;
            Assert.That(EpisodeEngine.AdvanceCloses(s), Is.Null, "A player Head of Household nominates first.");
            s.hohId = Npcs(s)[0].id;
            Assert.That(EpisodeEngine.AdvanceCloses(s), Is.EqualTo(StoryAnchors.NomsSet));
        }

        private static EpisodeState MeetingSeason(out ContestantState caller)
        {
            // A real week-two campaign, played with the story system off so nothing else is open,
            // then switched on for the meeting.
            var engine = new EpisodeEngine(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 31));
            for (int i = 0; i < 500 && !(engine.Snapshot.week == 2 && engine.Snapshot.phase == EpisodePhase.Campaign); i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var s = engine.Snapshot;
            Assert.That(s.week == 2 && s.phase == EpisodePhase.Campaign, Is.True, "The fixture reaches week two's campaign.");
            Assert.That(s.Find(s.playerId).status, Is.EqualTo(ContestantStatus.Active), "The fixture's player is still in the house.");
            EpisodeEngine.EnableStory(s, s.week);
            caller = s.Active.First(c => !c.isPlayer);
            // Somebody who resents the player, and knows something about them to say.
            Grudges.Add(s, caller.id, s.playerId, 70, GrudgeCauses.Story);
            Knowledge.Create(s, FactKinds.Secret, s.playerId, null, FactVisibility.Private, null, caller.id);
            Assert.That(EpisodeEngine.StartStory(s, "emergency-meeting", StoryAnchors.EvictionEve), Is.True, "The meeting casts.");
            return s;
        }

        [Test]
        public void TheAdvanceWarningListsExactlyTheBeatsTheAdvanceLapses()
        {
            var s = MeetingSeason(out _);
            var open = EpisodeEngine.OpenStoryBeats(s);
            Assert.That(open, Has.Count.EqualTo(1));
            var passing = EpisodeEngine.LapsingOnAdvance(s);
            var after = Apply(s, Command(s, EpisodeCommandKind.Advance));
            var lapsed = after.houseEvents.Where(e => e.IsStory && e.resolved && open.Any(o => o.id == e.id)).Select(e => e.id).ToList();
            Assert.That(passing.Select(e => e.id), Is.EquivalentTo(after.houseEvents
                .Where(e => open.Any(o => o.id == e.id) && e.resolved).Select(e => e.id)),
                "The warning names what the Advance let pass, no more and no less.");
            Assert.That(lapsed, Is.EquivalentTo(passing.Select(e => e.id)));
        }

        [Test]
        public void AMeetingIsStagedInTheLivingRoomAndAnsweringItCallsTheHouseTogether()
        {
            var s = MeetingSeason(out var caller);
            var beat = EpisodeEngine.OpenStoryBeats(s).Single();
            Assert.That(beat.surface, Is.EqualTo(StorySurfaces.Meeting));
            Assert.That(beat.venue, Is.EqualTo(StoryVenues.Living));
            Assert.That(beat.involvedIds.Concat(beat.cast.Select(r => r.contestantId)), Does.Contain(caller.id));
            var answer = beat.choices.First(c => c.optionId != beat.lapseOptionId && !c.locked && !c.pickPerson && !c.costsAction && !c.conduct);
            var command = Command(s, EpisodeCommandKind.ProgressStoryline);
            command.targetId = beat.id; command.secondTargetId = answer.optionId;
            var after = Apply(s, command);
            var fallout = after.events.Where(e => e.kind == StoryLog.HouseMeeting).ToList();
            Assert.That(fallout, Has.Count.EqualTo(1), "The house meeting is marked once.");
            Assert.That(fallout[0].audienceIds, Does.Contain(after.playerId), "The player sees it.");
            Assert.That(fallout[0].text, Does.Not.Contain(caller.name), "The line is name-free: the card's faces carry who.");
        }

        [Test]
        public void ALapsedMeetingIsNotMarked()
        {
            var s = MeetingSeason(out _);
            var after = Apply(s, Command(s, EpisodeCommandKind.Advance));
            Assert.That(after.events.Any(e => StoryLog.IsFallout(e.kind)), Is.False);
        }

        [Test]
        public void ProductionsPenaltyAndRemovalAreTheirOwnCeremonies()
        {
            var s = Season(5, 10);
            var npc = Npcs(s)[0];
            s.phase = EpisodePhase.Social; s.evictionResolved = true;
            // The ladder as the engine climbs it: a strike effect, through the story system's hook.
            for (int rung = 1; rung <= 3; rung++)
            {
                Assert.That(EpisodeEngine.ProductionStrike(s, npc.id, "rung " + rung), Is.EqualTo(rung));
                Production.For(s, npc.id, false).lastStrikeWeek = 0;
            }
            Assert.That(s.events.Count(e => e.kind == StoryLog.Penalty), Is.EqualTo(1), "The second strike is the penalty's card.");
            Assert.That(s.story.pendingRemovalId, Is.EqualTo(npc.id));
            var after = Apply(s, Command(s, EpisodeCommandKind.Advance));
            Assert.That(after.Find(npc.id).status, Is.EqualTo(ContestantStatus.Expelled));
            var removal = after.events.Single(e => e.kind == StoryLog.Expulsion);
            Assert.That(removal.text, Does.Contain(npc.name), "Production's removal is announced to the house by name.");
        }

        [Test]
        public void EveryStagedBeatNamesAKnownVenue()
        {
            foreach (var arc in StoryCatalog.All)
                foreach (var beat in arc.beats)
                {
                    if (beat.venue != null) Assert.That(StoryVenues.IsKnown(beat.venue), Is.True, arc.id + "/" + beat.id + ": " + beat.venue);
                    if (beat.fallout != null) Assert.That(StoryLog.IsFallout(beat.fallout), Is.True, arc.id + "/" + beat.id + ": " + beat.fallout);
                    // A meeting is the house in one room, so it always names the room.
                    if (beat.surface == StorySurfaces.Meeting)
                        Assert.That(beat.venue, Is.Not.Null, arc.id + "/" + beat.id + " is staged somewhere.");
                }
        }

        [Test]
        public void TheBlowUpsAndMeetingsAreMarked()
        {
            var marked = StoryCatalog.All.SelectMany(arc => arc.beats.Where(b => b.fallout != null).Select(b => arc.id + "/" + b.id + "=" + b.fallout)).ToList();
            Assert.That(marked, Does.Contain("kitchen-blowup/blow-up=" + StoryLog.Blowup));
            Assert.That(marked, Does.Contain("emergency-meeting/meeting=" + StoryLog.HouseMeeting));
            Assert.That(marked, Does.Contain("the-accounting/living-room-now=" + StoryLog.HouseMeeting));
        }
    }
}
