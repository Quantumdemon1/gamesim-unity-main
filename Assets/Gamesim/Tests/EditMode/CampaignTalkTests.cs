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
    ///
    /// <para>And the conversation's way on, "Next voter" (UI-UX plan D11): the next voter on the
    /// board not yet talked to, by first name, never outside the campaign. Built and pressed in
    /// PlayMode (EpisodePlayModeTests.CampaignNextVoter.cs).</para>
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

        // ------------------------------------------------------------ the way on: "Next voter" (UI-UX plan D11)

        /// <summary>
        /// A plain campaign: the player and seven others, hg-1 at the head of the house, hg-2 and hg-3
        /// on the block, so hg-4 to hg-7 vote with the player, in that order on the board.
        /// </summary>
        private static EpisodeState PlainCampaign()
        {
            var s = new EpisodeState { sessionId = "plain", playerId = "player", phase = EpisodePhase.Campaign, week = 2 };
            s.contestants.Add(new ContestantState { id = "player", name = "You", isPlayer = true, status = ContestantStatus.Active });
            for (int i = 1; i <= 7; i++)
                s.contestants.Add(new ContestantState { id = "hg-" + i, name = "Name" + i + " Surname", status = ContestantStatus.Active });
            s.hohId = "hg-1";
            s.nominees = new List<string> { "hg-2", "hg-3" };
            return s;
        }

        private static string Next(EpisodeState s, string talkingTo, params string[] talked) =>
            CampaignTalk.NextVoter(s, talkingTo, new HashSet<string>(talked))?.id;

        [Test]
        public void TheNextVoterIsTheNextOnTheBoardNotYetTalkedTo()
        {
            var s = PlainCampaign();
            Assert.That(EpisodeEngine.Voters(s).Select(v => v.id), Is.EqualTo(new[] { "player", "hg-4", "hg-5", "hg-6", "hg-7" }), "The board's voters, in order.");
            Assert.That(Next(s, null), Is.EqualTo("hg-4"), "Nobody talked to yet: the head of the board.");
            Assert.That(Next(s, "hg-4", "hg-4"), Is.EqualTo("hg-5"), "From a voter, the one after them.");
            Assert.That(Next(s, "hg-5", "hg-4", "hg-5"), Is.EqualTo("hg-6"));
            Assert.That(Next(s, "hg-4", "hg-4", "hg-5"), Is.EqualTo("hg-6"), "A voter already talked to is passed over.");
            Assert.That(Next(s, "hg-7", "hg-5", "hg-6", "hg-7"), Is.EqualTo("hg-4"), "Past the end of the board, round to its head.");
            Assert.That(Next(s, "hg-6", "hg-6"), Is.EqualTo("hg-7"), "The one being talked to counts as talked to.");
            Assert.That(Next(s, "hg-6"), Is.EqualTo("hg-7"), "Even when the set does not hold them yet.");
            Assert.That(Next(s, "hg-1", "hg-1"), Is.EqualTo("hg-4"), "From the Head of Household, the first voter not talked to.");
            Assert.That(Next(s, "hg-2", "hg-2", "hg-4"), Is.EqualTo("hg-5"), "From a nominee too.");
            Assert.That(CampaignTalk.NextVoter(s, "hg-4", null)?.id, Is.EqualTo("hg-5"), "No set: only the one being talked to is passed over.");
        }

        [Test]
        public void NoVoterIsNamedOnceAllAreTalkedToOutsideTheCampaignOrForAPlayerOutOfTheGame()
        {
            var s = PlainCampaign();
            Assert.That(Next(s, "hg-7", "hg-4", "hg-5", "hg-6", "hg-7"), Is.Null, "Every voter talked to: nobody, and never the player, who votes too.");
            Assert.That(Next(s, "hg-1", "hg-4", "hg-5", "hg-6", "hg-7"), Is.Null, "Nor from the Head of Household.");
            Assert.That(CampaignTalk.NextVoter(null, "hg-4", null), Is.Null, "No season.");

            s.Find("hg-5").status = ContestantStatus.Evicted;
            Assert.That(Next(s, "hg-4", "hg-4"), Is.EqualTo("hg-6"), "A houseguest out of the game is no voter.");
            s.Find("hg-5").status = ContestantStatus.Active;

            foreach (var phase in new[] { EpisodePhase.Social, EpisodePhase.VetoMeeting, EpisodePhase.Eviction })
            {
                s.phase = phase;
                Assert.That(Next(s, "hg-4", "hg-4"), Is.Null, phase + " is not the campaign.");
            }
            s.phase = EpisodePhase.Campaign;
            s.Find("player").status = ContestantStatus.Evicted;
            Assert.That(Next(s, "hg-4", "hg-4"), Is.Null, "A player out of the game has nobody to campaign to.");
        }

        [Test]
        public void TheWayOnNamesTheVoterByTheirFirstName()
        {
            Assert.That(CampaignTalk.NextVoterPrefix, Is.EqualTo("Next voter: "));
            Assert.That(CampaignTalk.NextVoterCaption("Robin Vale"), Is.EqualTo("Next voter: Robin"));
            Assert.That(CampaignTalk.NextVoterCaption("Dr. Robin Vale"), Is.EqualTo("Next voter: Robin"), "A title is passed over, as the board's Talk to passes it.");
            Assert.That(CampaignTalk.NextVoterCaption("Robin"), Is.EqualTo("Next voter: Robin"));
            Assert.That(CampaignTalk.NextVoterCaption(null), Is.EqualTo("Next voter: "), "No name, no crash.");
        }

        [Test]
        public void NamingTheNextVoterChangesNothingInTheSeason()
        {
            foreach (bool onTheBlock in new[] { false, true })
            {
                var s = Campaign(31, onTheBlock);
                var talked = new HashSet<string>();
                string before = JsonConvert.SerializeObject(s);
                uint random = s.randomState;
                foreach (var person in s.contestants)
                {
                    var next = CampaignTalk.NextVoter(s, person.id, talked);
                    Assert.That(next == null || EpisodeEngine.Voters(s).Any(v => v.id == next.id && !v.isPlayer), Is.True, "Only ever a voter, never the player.");
                    Assert.That(next?.id, Is.Not.EqualTo(person.id), "Never the one being talked to.");
                }
                Assert.That(talked, Is.Empty, "The talked-to set is the caller's: naming the next voter adds nobody to it,");
                Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(before), "the state is as it was,");
                Assert.That(s.randomState, Is.EqualTo(random), "and nothing is drawn from the season's stream.");
            }
        }
    }
}
