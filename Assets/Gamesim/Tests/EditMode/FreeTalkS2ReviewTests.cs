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

            // A renamed card keeps its template, so it keeps the same word (R7: by the template, whatever the card is named).
            s.Find(s.playerId).name = "Pat Example";
            Assert.That(StoryPeople.IsRealPerson(s, s.playerId), Is.False, "The control: a renamed card.");
            Assert.That(FreeTalk.IsAllStar(s.Find(s.playerId)), Is.True, "The control: the card was built from an All-Stars template.");
            Assert.That(TalkNotes.HouseReadsYou(s), Is.EqualTo("The house reads you as a bold talker."));

            // A card of the regular cast keeps the game's word.
            var regular = FreeTalkFixture.Fresh(TalkRules.Chemistry, seed: 3112u);
            FreeTalkS2Fixture.ClassAs(regular, regular.playerId, TalkPools.Spark);
            regular.story.talkLean = TalkRules.MostLean;
            Assert.That(FreeTalk.IsAllStar(regular.Find(regular.playerId)), Is.False, "The control: a regular card.");
            Assert.That(TalkNotes.HouseReadsYou(regular), Is.EqualTo("The house reads you as a flirt."));
        }

        /// <summary>
        /// A housemate made a Friend: kindred, rapport 8, their view of you 80 and yours of them 40. Their view stands well clear
        /// of the Friend stage's 35: the house's last beats before the veto is used run inside the veto's own command, before the
        /// replacement is named, and one rumour among them costs a view 15, so a Friend at 40 can be only Friendly by the time
        /// they are named - and the engine reads the stage as it stands then, as it does at the ceremony.
        /// </summary>
        private static void MakeFriend(EpisodeState s, string npcId)
        {
            FreeTalkS2Fixture.Kindred(s, npcId).rapport = 8;
            FreeTalkS2Fixture.Views(s, npcId, 80, 40);
            Assert.That(TalkFriendship.AtLeast(s, npcId, FriendStage.Friend), Is.True, "The control: a Friend.");
        }

        private static EpisodeState AtTalkRules(EpisodeState s, int level)
        {
            var copy = s.Clone();
            copy.story.talkRules = level;
            return copy;
        }

        /// <summary>
        /// The first season of a fixed run of seeds walked to a veto meeting, not yet resolved and with a replacement to name, that
        /// <paramref name="fits"/>: a copy of the meeting's state.
        /// </summary>
        private static EpisodeState AtAVetoMeeting(Func<EpisodeState, bool> fits, string what)
        {
            for (uint seed = 3201u; seed < 3401u; seed++)
            {
                var engine = new EpisodeEngine(FreeTalkFixture.Fresh(TalkRules.Chemistry, seed: seed));
                FreeTalkFixture.WalkUntil(engine, x => x.phase == EpisodePhase.VetoMeeting && !x.vetoResolved && fits(x)
                    || x.week > 4 || x.Find(x.playerId).status != ContestantStatus.Active || x.phase == EpisodePhase.Finished);
                var at = engine.Snapshot;
                if (at.phase == EpisodePhase.VetoMeeting && !at.vetoResolved && fits(at) && EpisodeEngine.ReplacementCandidates(at).Any())
                    return at.Clone();
            }
            Assert.Fail("No seed reached a veto meeting by week 4 where " + what + ".");
            return null;
        }

        /// <summary>The veto used on <paramref name="saved"/>, the player naming <paramref name="replacement"/> where they may: the states after it at talk rules 1 and 2.</summary>
        private static List<EpisodeState> UseTheVetoAtBothLevels(EpisodeState s, string saved, string replacement) =>
            new[] { TalkRules.Talk, TalkRules.Chemistry }.Select(level =>
            {
                var at = AtTalkRules(s, level);
                var command = EpisodeEngineTests.Command(at, EpisodeCommandKind.ResolveVeto);
                command.useVeto = true; command.targetId = saved; command.secondTargetId = replacement;
                var result = new EpisodeEngine(at).Apply(command);
                Assert.That(result.accepted, Is.True, "Talk rules " + level + ": " + result.reason);
                Assert.That(result.state.vetoResolved, Is.True, "The control: the veto meeting is over.");
                return result.state;
            }).ToList();

        [Test]
        public void NamingAFriendAsTheReplacementCostsEightMoreAtTalkRulesTwoOnlyWithTheSameDraws()
        {
            // The player is Head of Household and a housemate holds the veto and uses it: the player names the replacement.
            var s = AtAVetoMeeting(x => x.hohId == x.playerId && x.vetoHolderId != x.playerId && EpisodeEngine.NpcVetoSave(x) != null,
                "the player is Head of Household and a housemate uses the veto");
            string friend = EpisodeEngine.ReplacementCandidates(s).First().id;
            MakeFriend(s, friend);
            string saved = EpisodeEngine.NpcVetoSave(s);
            Assert.That(saved, Is.Not.Null, "The control: the housemate still uses the veto.");
            FreeTalkS2Fixture.AssertValid(s);
            var states = UseTheVetoAtBothLevels(s, saved, friend);
            foreach (var state in states)
                Assert.That(state.nominees, Does.Contain(friend).And.Not.Contain(saved), "The control: the Friend is the replacement.");
            Assert.That(states[1].randomState, Is.EqualTo(states[0].randomState), "The veto meeting draws what it drew.");
            double friendCost = states[0].Score(friend, s.playerId) - states[1].Score(friend, s.playerId);
            // The house's beats after the commit read the scores, so allow them a point or two either way.
            Assert.That(friendCost, Is.InRange(6.0, 10.0), "A Friend named as the replacement pays a further 8 at talk rules 2.");
        }

        [Test]
        public void AFriendAHousemateHeadOfHouseholdNamesAfterThePlayersVetoCostsThePlayerNothingMore()
        {
            // The player holds the veto and uses it; the Head of Household, a housemate, names the replacement.
            var s = AtAVetoMeeting(x => x.vetoHolderId == x.playerId && x.hohId != x.playerId,
                "the player holds the veto and is not Head of Household");
            foreach (var candidate in EpisodeEngine.ReplacementCandidates(s).ToList()) MakeFriend(s, candidate.id);
            FreeTalkS2Fixture.AssertValid(s);
            string saved = s.nominees[0];
            var states = UseTheVetoAtBothLevels(s, saved, null);
            Assert.That(states[1].nominees, Is.EqualTo(states[0].nominees), "The control: the Head of Household names the same housemate.");
            string named = states[0].nominees.Single(id => !s.nominees.Contains(id));
            Assert.That(TalkFriendship.AtLeast(states[1], named, FriendStage.Friend), Is.True,
                "The control: the one named is a Friend of the player's, still, after the meeting.");
            Assert.That(states[1].randomState, Is.EqualTo(states[0].randomState), "The veto meeting draws what it drew.");
            Assert.That(states[1].Score(named, s.playerId), Is.EqualTo(states[0].Score(named, s.playerId)).Within(2.0),
                "Only the player's own naming costs: a Friend the Head of Household names thinks no less of the player for it.");
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
