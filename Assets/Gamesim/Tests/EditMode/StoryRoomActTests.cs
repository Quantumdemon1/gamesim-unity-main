using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The room acts (decision D-E): the web's room-bound acts as commands named for the act. Each
    /// is a social action; each is part of the story system's staging rules and of nothing before
    /// them; and the ones with rules of their own - the invitation, the audience, the ally, the
    /// practice cap - keep them.
    /// </summary>
    public sealed class StoryRoomActTests
    {
        private static EpisodeState FreeTime(uint seed = 7, int size = 8)
        {
            var s = StorySeasonTests.StorySeason(seed, size);
            s.phase = EpisodePhase.Social;
            return s;
        }

        private static List<ContestantState> Npcs(EpisodeState s) => s.contestants.Where(c => !c.isPlayer && c.status == ContestantStatus.Active).ToList();

        private static EpisodeCommand Act(EpisodeState s, EpisodeCommandKind kind, string target, string text = null)
        {
            var c = EpisodeEngineTests.Command(s, kind);
            c.targetId = target; c.text = text;
            return c;
        }

        private static EpisodeState Apply(EpisodeState s, EpisodeCommand c)
        {
            var result = new EpisodeEngine(s).Apply(c);
            Assert.That(result.accepted, Is.True, c.kind + ": " + result.reason);
            return result.state;
        }

        private static string Refused(EpisodeState s, EpisodeCommand c)
        {
            var result = new EpisodeEngine(s).Apply(c);
            Assert.That(result.accepted, Is.False, c.kind + " should have been refused.");
            return result.reason;
        }

        [Test]
        public void EveryRoomActIsASocialActionThatWarmsTheOneItIsWith()
        {
            foreach (var kind in new[] { EpisodeCommandKind.PillowTalk, EpisodeCommandKind.PlayAGame, EpisodeCommandKind.Cook })
            {
                var s = FreeTime();
                var target = Npcs(s)[0].id;
                double before = s.Score(s.playerId, target);
                var after = Apply(s, Act(s, kind, target));
                Assert.That(after.Score(after.playerId, target), Is.GreaterThan(before), kind.ToString());
                Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(s) + 1), kind + " spends an action.");
            }
        }

        [Test]
        public void TheRoomActsBelongToTheStorySystemsStagingRules()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7);
            s.phase = EpisodePhase.Social;
            Assert.That(EpisodeEngine.RoomActsOpen(s), Is.False);
            Assert.That(Refused(s, Act(s, EpisodeCommandKind.PillowTalk, Npcs(s)[0].id)), Does.Contain("rules"));
        }

        [Test]
        public void OnlyTheHeadOfHouseholdInvitesAndOnlyTwoAWeek()
        {
            var s = FreeTime();
            var npcs = Npcs(s);
            Assert.That(Refused(s, Act(s, EpisodeCommandKind.InviteUp, npcs[0].id)), Does.Contain("Head of Household"));
            s.hohId = s.playerId;
            s = Apply(s, Act(s, EpisodeCommandKind.InviteUp, npcs[0].id));
            Assert.That(Refused(s, Act(s, EpisodeCommandKind.InviteUp, npcs[0].id)), Does.Contain("already"));
            s = Apply(s, Act(s, EpisodeCommandKind.InviteUp, npcs[1].id));
            Assert.That(Refused(s, Act(s, EpisodeCommandKind.InviteUp, npcs[2].id)), Does.Contain("two"));
        }

        [Test]
        public void TheHouseNoticesWhoYouPicked()
        {
            var s = FreeTime();
            s.hohId = s.playerId;
            var npcs = Npcs(s);
            var fan = npcs[1];
            // The fan ranks the player warmest of anyone in the house, and is not the one invited.
            foreach (var other in s.contestants.Where(c => c.id != fan.id))
                s.relationships.First(r => r.fromId == fan.id && r.toId == other.id).score = other.isPlayer ? 80 : 0;
            var after = Apply(s, Act(s, EpisodeCommandKind.InviteUp, npcs[0].id));
            Assert.That(after.relationships.Single(r => r.fromId == fan.id && r.toId == after.playerId).events.Any(e => e.type == StoryReceipts.Snubbed), Is.True,
                "The houseguest who ranks you warmest noticed they were not the one invited.");
        }

        [Test]
        public void StandingUpForSomebodyNeedsSomebodyToSeeIt()
        {
            var s = FreeTime();
            var npcs = Npcs(s);
            Assert.That(Refused(s, Act(s, EpisodeCommandKind.PublicDefense, npcs[0].id)), Does.Contain("see"));
            Assert.That(Refused(s, Act(s, EpisodeCommandKind.PublicDefense, npcs[0].id, s.playerId)), Does.Contain("there"),
                "The player is not their own witness.");
            double witnessBefore = s.Score(s.playerId, npcs[1].id);
            var after = Apply(s, Act(s, EpisodeCommandKind.PublicDefense, npcs[0].id, npcs[1].id + " " + npcs[2].id));
            Assert.That(after.Score(after.playerId, npcs[1].id), Is.GreaterThan(witnessBefore), "A witness warms to you too.");
        }

        [Test]
        public void APrivateMeetingIsForAnAlly()
        {
            var s = FreeTime();
            var ally = Npcs(s)[0];
            Assert.That(Refused(s, Act(s, EpisodeCommandKind.AllianceMeet, ally.id)), Does.Contain("ally"));
            s.alliances.Add(new AllianceState { id = "alliance-meet", name = "Meet", members = new List<string> { s.playerId, ally.id }, active = true });
            Apply(s, Act(s, EpisodeCommandKind.AllianceMeet, ally.id));
        }

        [Test]
        public void PracticeAddsUpToThreeAndThenRefuses()
        {
            var s = FreeTime();
            var partner = Npcs(s)[0].id;
            double before = EpisodeEngine.CommonCompetitionBonus(s);
            for (int session = 1; session <= 3; session++)
            {
                s = Apply(s, Act(s, EpisodeCommandKind.CompPractice, partner));
                Assert.That(EpisodeEngine.CommonCompetitionBonus(s), Is.EqualTo(before + session * EpisodeEngine.PracticeBonus).Within(1e-9));
            }
            Assert.That(Refused(s, Act(s, EpisodeCommandKind.CompPractice, partner)), Does.Contain("ready"));
        }

        [Test]
        public void PillowTalkIsWhatTheShowmanceReads()
        {
            var s = FreeTime();
            s.week = 2;
            var partner = Npcs(s)[0].id;
            Assert.That(EpisodeEngine.PillowTalkedLately(s, partner), Is.False);
            var after = Apply(s, Act(s, EpisodeCommandKind.PillowTalk, partner));
            Assert.That(EpisodeEngine.PillowTalkedLately(after, partner), Is.True);
            after.week = 3;
            Assert.That(EpisodeEngine.PillowTalkedLately(after, partner), Is.True, "Last week's still counts.");
            after.week = 4;
            Assert.That(EpisodeEngine.PillowTalkedLately(after, partner), Is.False);
        }
    }
}
