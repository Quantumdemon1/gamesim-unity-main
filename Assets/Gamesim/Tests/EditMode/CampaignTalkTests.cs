using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// What a "Talk to" on the campaign board opens the conversation on (CampaignTalk, UI-UX plan
    /// D9): the plea for a player on the block who can still make it, the vote question for anybody
    /// else's talk to a voter, and the topics for everybody else. A read only: the season is as it
    /// was. Runs without Unity; the built conversation is checked in PlayMode
    /// (EpisodePlayModeTests.ConversationIntent.cs).
    /// </summary>
    public sealed class CampaignTalkTests
    {
        /// <summary>A season walked to its first campaign, with the week's roles set by hand: a houseguest at the head of the house, and two others on the block, or the player and one other.</summary>
        private static EpisodeState Campaign(uint seed, bool playerOnTheBlock, bool levers = true)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            for (int i = 0; i < 600 && !(engine.Snapshot.phase == EpisodePhase.Campaign && engine.Snapshot.nominees.Count == 2); i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var s = engine.Snapshot;
            Assert.That(s.phase == EpisodePhase.Campaign && s.nominees.Count == 2, Is.True, "The season never reached a campaign.");
            var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            s.hohId = npcs[0];
            s.vetoHolderId = npcs[0];
            s.nominees = playerOnTheBlock ? new List<string> { s.playerId, npcs[1] } : new List<string> { npcs[1], npcs[2] };
            // No plea made yet, so the one a test adds is the only one.
            s.lobbies.Clear();
            s.strategyRulesStartWeek = 1;
            if (levers) EpisodeEngine.EnableLevers(s);
            else s.leverRulesStartWeek = 0;
            return s;
        }

        private static ContestantState[] NpcVoters(EpisodeState s) => EpisodeEngine.Voters(s).Where(v => !v.isPlayer).ToArray();

        [Test]
        public void OffTheBlockATalkToAVoterOpensOnTheVoteQuestion()
        {
            var s = Campaign(31, false);
            var voters = NpcVoters(s);
            Assert.That(voters, Is.Not.Empty, "The campaign has voters.");
            foreach (var voter in voters)
                Assert.That(CampaignTalk.For(s, voter.id), Is.EqualTo(CampaignTalk.Opens.Asks), voter.name + " is asked how they will vote.");
            Assert.That(CampaignTalk.For(s, s.hohId), Is.EqualTo(CampaignTalk.Opens.Topics), "The Head of Household votes only on a tie: the topics.");
            foreach (string id in s.nominees)
                Assert.That(CampaignTalk.For(s, id), Is.EqualTo(CampaignTalk.Opens.Topics), "A nominee does not vote: the topics.");
        }

        [Test]
        public void OnTheBlockATalkToAVoterOpensOnThePleaUntilItIsMade()
        {
            var s = Campaign(31, true);
            var voters = NpcVoters(s);
            Assert.That(voters.Length, Is.GreaterThanOrEqualTo(2), "The campaign has two voters to plead with.");
            foreach (var voter in voters)
            {
                Assert.That(StrategyRules.CanBeAskedForTheirVote(s, voter.id), Is.True, voter.name + " can be asked for their vote.");
                Assert.That(CampaignTalk.For(s, voter.id), Is.EqualTo(CampaignTalk.Opens.Plea), voter.name + " hears the plea first.");
            }
            string other = s.nominees.Single(id => id != s.playerId);
            Assert.That(CampaignTalk.For(s, other), Is.EqualTo(CampaignTalk.Opens.Topics), "The other nominee has no vote to plead for.");
            Assert.That(CampaignTalk.For(s, s.hohId), Is.EqualTo(CampaignTalk.Opens.Topics), "Nor has the Head of Household.");

            s.lobbies.Add(new LobbyState
            {
                week = s.week, phase = s.phase, deciderId = voters[0].id, ask = LobbyAsk.Vote, subjectId = s.playerId,
                approach = LobbyApproach.Emotional, response = "moved",
            });
            Assert.That(CampaignTalk.For(s, voters[0].id), Is.EqualTo(CampaignTalk.Opens.Asks),
                "Once the plea is made, the vote question is what is left to come for.");
            Assert.That(CampaignTalk.For(s, voters[1].id), Is.EqualTo(CampaignTalk.Opens.Plea), "Every other voter still hears it.");
        }

        [Test]
        public void WithoutTheLeversANomineeOpensOnTheVoteQuestion()
        {
            var s = Campaign(31, true, levers: false);
            var voters = NpcVoters(s);
            Assert.That(voters, Is.Not.Empty);
            foreach (var voter in voters)
            {
                Assert.That(StrategyRules.Pleas(s, voter.id), Is.Empty, "A season without the levers has no plea for a vote.");
                Assert.That(CampaignTalk.For(s, voter.id), Is.EqualTo(CampaignTalk.Opens.Asks), voter.name);
            }
        }

        [Test]
        public void OutsideTheCampaignOrOutOfTheGameATalkOpensOnTheTopics()
        {
            var s = Campaign(31, true);
            string voter = NpcVoters(s)[0].id;
            Assert.That(CampaignTalk.For(s, voter), Is.EqualTo(CampaignTalk.Opens.Plea), "The plea, as the campaign stands.");
            Assert.That(CampaignTalk.For(null, voter), Is.EqualTo(CampaignTalk.Opens.Topics), "No season.");
            Assert.That(CampaignTalk.For(s, null), Is.EqualTo(CampaignTalk.Opens.Topics), "Nobody.");
            Assert.That(CampaignTalk.For(s, "nobody-at-all"), Is.EqualTo(CampaignTalk.Opens.Topics), "Somebody not in the season.");
            Assert.That(CampaignTalk.For(s, s.playerId), Is.EqualTo(CampaignTalk.Opens.Topics), "The player themselves.");

            s.phase = EpisodePhase.Eviction;
            Assert.That(CampaignTalk.For(s, voter), Is.EqualTo(CampaignTalk.Opens.Topics), "After the campaign.");
            s.phase = EpisodePhase.Campaign;
            s.Find(s.playerId).status = ContestantStatus.Evicted;
            Assert.That(CampaignTalk.For(s, voter), Is.EqualTo(CampaignTalk.Opens.Topics), "A player out of the game.");
            s.Find(s.playerId).status = ContestantStatus.Active;
            s.Find(voter).status = ContestantStatus.Evicted;
            Assert.That(CampaignTalk.For(s, voter), Is.EqualTo(CampaignTalk.Opens.Topics), "A houseguest out of the game.");
        }

        [Test]
        public void ReadingWhereATalkOpensChangesNothingInTheSeason()
        {
            foreach (bool onTheBlock in new[] { false, true })
            {
                var s = Campaign(31, onTheBlock);
                string before = JsonConvert.SerializeObject(s);
                uint random = s.randomState;
                foreach (var person in s.contestants) CampaignTalk.For(s, person.id);
                Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(before), "Where a talk opens is a read: the state is as it was,");
                Assert.That(s.randomState, Is.EqualTo(random), "and nothing is drawn from the season's stream.");
            }
        }
    }
}
