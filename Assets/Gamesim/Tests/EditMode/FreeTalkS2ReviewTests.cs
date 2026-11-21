using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Free talk slice 2's review fixes (talk rules 2): the house's read of a player whose own card is a real person says nothing
    /// romantic (R7, R14); the further cost of naming a Friend as the veto's replacement; and the star pool's stage talks hold no
    /// coy stage direction or double meaning. Simulation only, so the Unity-free subset runs it too.
    /// </summary>
    public sealed class FreeTalkS2ReviewTests
    {
        [Test]
        public void AnAllStarPlayerIsReadAsABoldTalkerNeverAFlirt()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice
            {
                HouseSize = 8, Roster = CastTemplates.Roster.AllStars, PlayerTemplateId = "dan-gheesling",
            }, 3111u);
            ShippedRules.ApplyFresh(s);
            s.story.talkRules = TalkRules.Chemistry;
            Assert.That(StoryPeople.IsRealPerson(s, s.playerId), Is.True, "The control: the player's own card is a real person.");

            foreach (var start in new[] { TalkPools.Spark, TalkPools.Steady })
            {
                FreeTalkS2Fixture.ClassAs(s, s.playerId, start);
                s.story.talkLean = TalkRules.MostLean;
                Assert.That(TalkLean.PlayerClass(s), Is.EqualTo(TalkPools.Spark), start + ": the control, the read is Spark.");
                Assert.That(TalkNotes.HouseReadsYou(s), Is.EqualTo("The house reads you as a bold talker."), start);
                var shown = TalkScreenLines.YourReadBlocks(s).SelectMany(b => b.Lines).ToList();
                Assert.That(shown, Is.Not.Empty, start + ": the block is shown.");
                Assert.That(shown.Where(l => l.IndexOf("flirt", StringComparison.OrdinalIgnoreCase) >= 0), Is.Empty, start);
            }

            // A renamed card is not a real person, and keeps the game's word.
            s.Find(s.playerId).name = "Pat Example";
            Assert.That(StoryPeople.IsRealPerson(s, s.playerId), Is.False, "The control: a renamed card.");
            Assert.That(TalkNotes.HouseReadsYou(s), Is.EqualTo("The house reads you as a flirt."));
        }

        /// <summary>A housemate made a Friend: kindred, rapport 8, their view of you 40 (as <see cref="FreeTalkFriendTests"/> does).</summary>
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

        /// <summary>
        /// A season at a veto meeting where the player is Head of Household and the veto will be used, with a Friend among the
        /// replacements: the meeting's state, the nominee saved and the Friend.
        /// </summary>
        private static EpisodeState PlayerReplaces(out string saved, out string friend)
        {
            for (uint seed = 3201u; seed < 3401u; seed++)
            {
                var engine = new EpisodeEngine(FreeTalkFixture.Fresh(TalkRules.Chemistry, seed: seed));
                FreeTalkFixture.WalkUntil(engine, x => x.phase == EpisodePhase.VetoMeeting && !x.vetoResolved && x.hohId == x.playerId
                    || x.week > 4 || x.Find(x.playerId).status != ContestantStatus.Active || x.phase == EpisodePhase.Finished);
                var at = engine.Snapshot;
                if (!(at.phase == EpisodePhase.VetoMeeting && !at.vetoResolved && at.hohId == at.playerId)) continue;
                var candidate = EpisodeEngine.ReplacementCandidates(at).FirstOrDefault();
                if (candidate == null) continue;
                var s = at.Clone();
                MakeFriend(s, candidate.id);
                saved = s.vetoHolderId == s.playerId ? s.nominees[0] : EpisodeEngine.NpcVetoSave(s);
                if (saved == null) continue;
                friend = candidate.id;
                return s;
            }
            Assert.Fail("No seed made the player Head of Household at a used veto by week 4.");
            saved = friend = null;
            return null;
        }

        [Test]
        public void NamingAFriendAsTheReplacementCostsEightMoreAtTalkRulesTwoOnlyWithTheSameDraws()
        {
            var s = PlayerReplaces(out string saved, out string friend);
            FreeTalkS2Fixture.AssertValid(s);
            var states = new[] { TalkRules.Talk, TalkRules.Chemistry }.Select(level =>
            {
                var at = AtTalkRules(s, level);
                var command = EpisodeEngineTests.Command(at, EpisodeCommandKind.ResolveVeto);
                command.useVeto = true; command.targetId = saved; command.secondTargetId = friend;
                var result = new EpisodeEngine(at).Apply(command);
                Assert.That(result.accepted, Is.True, "Talk rules " + level + ": " + result.reason);
                Assert.That(result.state.nominees, Does.Contain(friend), "The control: the Friend is the replacement.");
                return result.state;
            }).ToList();
            Assert.That(states[1].randomState, Is.EqualTo(states[0].randomState), "The veto meeting draws what it drew.");
            double friendCost = states[0].Score(friend, s.playerId) - states[1].Score(friend, s.playerId);
            // The house's beats after the commit read the scores, so allow them a point or two either way.
            Assert.That(friendCost, Is.InRange(6.0, 10.0), "A Friend named as the replacement pays a further 8 at talk rules 2.");
        }

        /// <summary>Stage directions and asides that read as coy or romantic rather than the game: none in an All-Star's talk.</summary>
        static readonly string[] Coy = { "however you like", "half shut", "door shut", "closed door", "behind closed doors", "wink" };

        [Test]
        public void NoStarStageTalkHoldsACoyAsideOrStageDirection()
        {
            var lines = new List<KeyValuePair<string, string>>();
            foreach (var row in TalkTables.All.Where(r => r.Pool == TalkPools.Star && r.Kind == TalkKind.Stage))
            {
                lines.Add(new KeyValuePair<string, string>(row.Id + " open", row.Open));
                foreach (var r in row.Replies)
                    foreach (var text in new[] { r.Line, r.Lands, r.Even, r.Grates })
                        lines.Add(new KeyValuePair<string, string>(row.Id + " reply", text));
            }
            Assert.That(lines.Count, Is.GreaterThan(12), "The control: the star pool's stage talks were read.");
            var bad = lines.Where(l => Coy.Any(c => (l.Value ?? "").IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0))
                .Select(l => l.Key + ": " + l.Value).ToList();
            Assert.That(bad, Is.Empty);
        }
    }
}
