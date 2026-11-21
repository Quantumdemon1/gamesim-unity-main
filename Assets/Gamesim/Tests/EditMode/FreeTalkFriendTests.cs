using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Free talk slice 2 (talk rules 2, ruling T7): a Friend's honesty when asked the vote, a Close friend's standing, and the
    /// further cost of nominating a Friend or closer, each without one more or one fewer draw from any stream. Simulation only.
    /// </summary>
    public sealed class FreeTalkFriendTests
    {
        /// <summary>A housemate made a Friend in any week: kindred (week 2's gate falls to 1), rapport 8, their view of you 40.</summary>
        private static void MakeFriend(EpisodeState s, string npcId)
        {
            FreeTalkS2Fixture.Kindred(s, npcId).rapport = 8;
            FreeTalkS2Fixture.Views(s, npcId, 40, 40);
            Assert.That(TalkFriendship.AtLeast(s, npcId, FriendStage.Friend), Is.True, "The control: a Friend.");
        }

        private static EpisodeState AtTalkRules(EpisodeState s, int level)
        {
            var copy = s.Clone();
            copy.story.talkRules = level;
            return copy;
        }

        [Test]
        public void AFriendsHonestyHasAFloorOfEightTenthsAtTalkRulesTwoOnly()
        {
            var s = FreeTalkFixture.Fresh(TalkRules.Chemistry);
            var npcs = FreeTalkFixture.Npcs(s);
            MakeFriend(s, npcs[0].id);
            Assert.That(EpisodeEngine.FriendHonesty(s, npcs[0].id, 0.25), Is.EqualTo(0.8));
            Assert.That(EpisodeEngine.FriendHonesty(s, npcs[0].id, 0.95), Is.EqualTo(0.95));
            Assert.That(EpisodeEngine.FriendHonesty(s, npcs[1].id, 0.25), Is.EqualTo(0.25), "Not a friend.");
            Assert.That(EpisodeEngine.FriendHonesty(AtTalkRules(s, TalkRules.Talk), npcs[0].id, 0.25), Is.EqualTo(0.25), "Talk rules 1.");
        }

        [Test]
        public void TheHonestFloorOnlyMovesTheThresholdOfTheSameCoin()
        {
            var engine = new EpisodeEngine(FreeTalkFixture.Fresh(TalkRules.Chemistry));
            FreeTalkFixture.WalkUntil(engine, x => x.phase == EpisodePhase.Campaign);
            var s = engine.Snapshot;
            foreach (var npc in FreeTalkFixture.Npcs(s))
            {
                string plain = Candour.Choose(s, npc.id, Candour.Vote, null);
                Assert.That(Candour.Choose(s, npc.id, Candour.Vote, null, 0), Is.EqualTo(plain), npc.id);
                string floored = Candour.Choose(s, npc.id, Candour.Vote, null, 1);
                Assert.That(floored, Is.EqualTo(Candour.Fixed(s, npc.id) ?? Candour.Honest), npc.id);
            }
        }

        [Test]
        public void AskingAFriendTheVoteDrawsWhatItDrewBefore()
        {
            var engine = new EpisodeEngine(FreeTalkFixture.Fresh(TalkRules.Chemistry));
            FreeTalkFixture.WalkUntil(engine, x => x.phase == EpisodePhase.Campaign);
            var s = engine.Snapshot;
            foreach (var voter in EpisodeEngine.Voters(s).Where(v => !v.isPlayer).Select(v => v.id).ToList())
            {
                var friendly = s.Clone();
                MakeFriend(friendly, voter);
                FreeTalkS2Fixture.AssertValid(friendly);
                var states = new[] { TalkRules.Talk, TalkRules.Chemistry }.Select(level =>
                {
                    var at = AtTalkRules(friendly, level);
                    var command = EpisodeEngineTests.Command(at, EpisodeCommandKind.AskVote);
                    command.targetId = voter;
                    var result = new EpisodeEngine(at).Apply(command);
                    Assert.That(result.accepted, Is.True, result.reason);
                    return result.state;
                }).ToList();
                // The answer may differ (that is the floor); the draws may not.
                Assert.That(states[1].randomState, Is.EqualTo(states[0].randomState), voter + ": the same draws as talk rules 1.");
            }
        }

        [Test]
        public void ACloseFriendsStandingIsNeverALie()
        {
            // Sneaky housemates, so a dishonest draw is on the weights even at a close friend's faith (Candour.Shift).
            var s = FreeTalkFixture.Fresh(TalkRules.Chemistry);
            foreach (var npc in FreeTalkFixture.Npcs(s))
            {
                s.Find(npc.id).traits = new List<string> { "Sneaky" };
                FreeTalkFixture.ContactOf(s, npc.id).rapport = 12;
                FreeTalkS2Fixture.Views(s, npc.id, 70, 70);
            }
            int lies = 0;
            for (int week = 3; week <= 12; week++)
            {
                s.week = week;
                var one = AtTalkRules(s, TalkRules.Talk);
                foreach (var npc in FreeTalkFixture.Npcs(s))
                {
                    Assert.That(TalkFriendship.AtLeast(s, npc.id, FriendStage.CloseFriend), Is.True, "The control: a Close friend.");
                    string drawn = Candour.Choose(s, npc.id, Candour.Where, null);
                    if (drawn == Candour.Dishonest) lies++;
                    Assert.That(TalkStanding.Stance(s, npc.id), Is.EqualTo(drawn == Candour.Dishonest ? Candour.Neutral : drawn), npc.id);
                    Assert.That(TalkStanding.Stance(one, npc.id), Is.EqualTo(drawn), "Talk rules 1 says what was drawn.");
                }
            }
            Assert.That(lies, Is.GreaterThan(0), "The control: some draws were dishonest.");
        }

        /// <summary>A season walked to a nomination the player makes as Head of Household.</summary>
        private static EpisodeState PlayerNominates()
        {
            for (uint seed = 3201u; seed < 3261u; seed++)
            {
                var engine = new EpisodeEngine(FreeTalkFixture.Fresh(TalkRules.Chemistry, seed: seed));
                FreeTalkFixture.WalkUntil(engine, x => x.phase == EpisodePhase.Nomination && x.nominees.Count == 0 && x.hohId == x.playerId
                    || x.week > 3 || x.Find(x.playerId).status != ContestantStatus.Active || x.phase == EpisodePhase.Finished);
                var at = engine.Snapshot;
                if (at.phase == EpisodePhase.Nomination && at.nominees.Count == 0 && at.hohId == at.playerId) return at;
            }
            Assert.Fail("No seed made the player Head of Household by week 3.");
            return null;
        }

        [Test]
        public void NominatingAFriendCostsEightMoreAtTalkRulesTwoOnlyWithTheSameDraws()
        {
            var s = PlayerNominates();
            var pool = EpisodeEngine.NominationCandidates(s).Select(c => c.id).ToList();
            string friend = pool[0], other = pool[1];
            MakeFriend(s, friend);
            FreeTalkS2Fixture.Views(s, other, 40, 40);
            FreeTalkS2Fixture.AssertValid(s);
            var states = new[] { TalkRules.Talk, TalkRules.Chemistry }.Select(level =>
            {
                var at = AtTalkRules(s, level);
                var command = EpisodeEngineTests.Command(at, EpisodeCommandKind.Nominate);
                command.targetId = friend; command.secondTargetId = other;
                var result = new EpisodeEngine(at).Apply(command);
                Assert.That(result.accepted, Is.True, result.reason);
                return result.state;
            }).ToList();
            Assert.That(states[1].randomState, Is.EqualTo(states[0].randomState), "The nomination draws what it drew.");
            double friendCost = states[0].Score(friend, s.playerId) - states[1].Score(friend, s.playerId);
            double otherCost = states[0].Score(other, s.playerId) - states[1].Score(other, s.playerId);
            // The house's beats after the commit read the scores, so allow them a point or two either way.
            Assert.That(friendCost, Is.InRange(6.0, 10.0), "A Friend pays a further 8 at talk rules 2.");
            Assert.That(otherCost, Is.InRange(-2.0, 2.0), "Somebody who is not a friend pays nothing more.");
        }
    }
}
