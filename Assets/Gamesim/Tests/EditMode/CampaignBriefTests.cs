using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The campaign screen's goals and intel (PACK8-PASS-PLAN B4), and the plea's words
    /// (decision 8): each read from rows the player owns, never from how a houseguest feels about
    /// anybody. Runs without Unity.
    /// </summary>
    public sealed class CampaignBriefTests
    {
        /// <summary>A season walked to its first campaign with two on the block.</summary>
        private static EpisodeState Campaign(uint seed)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            for (int i = 0; i < 600 && !(engine.Snapshot.phase == EpisodePhase.Campaign && engine.Snapshot.nominees.Count == 2); i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var s = engine.Snapshot;
            Assert.That(s.phase == EpisodePhase.Campaign && s.nominees.Count == 2, Is.True, "The season never reached a campaign.");
            return s;
        }

        /// <summary>The campaign with the week's roles set by hand: a houseguest at the head of the house, and two others on the block, or the player and one other.</summary>
        private static EpisodeState Shaped(uint seed, bool playerOnTheBlock)
        {
            var s = Campaign(seed);
            var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            s.hohId = npcs[0];
            s.vetoHolderId = npcs[0];
            s.nominees = playerOnTheBlock ? new List<string> { s.playerId, npcs[1] } : new List<string> { npcs[1], npcs[2] };
            // Nothing learned and nothing said yet, so every row a test adds is the only one of its kind.
            s.replyCards.Clear();
            s.lobbies.Clear();
            s.promises.Clear();
            s.deals.Clear();
            s.memories.Clear();
            s.alliances.RemoveAll(a => a.members.Contains(s.playerId));
            s.ledger.replies.Clear();
            s.ledger.claims.Clear();
            s.ledger.standings.Clear();
            s.ledger.calls.Clear();
            return s;
        }

        private static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var row = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (row == null) s.relationships.Add(row = new RelationshipState { fromId = from, toId = to });
            row.score = score;
        }

        private static ContestantState[] NpcVoters(EpisodeState s) => EpisodeEngine.Voters(s).Where(v => !v.isPlayer).ToArray();

        /// <summary>The goals a test is about: the plays the walk may have taken on are its own business.</summary>
        private static string[] Rows(EpisodeState s) =>
            CampaignBrief.Goals(s).Where(g => g.kind != CampaignBrief.GoalKinds.Play).Select(g => g.ToString()).ToArray();

        [Test]
        public void APleasWordsFollowThePlayersOwnReadingNeverTheirs()
        {
            var s = Shaped(31, false);
            string from = s.nominees[0];
            var plea = new ReplyCardState { id = "reply-1", week = s.week, kind = ReplyCards.Plea, fromId = from, aboutId = s.nominees[1] };
            SetScore(s, s.playerId, from, 0);
            SetScore(s, from, s.playerId, 90);
            string cold = ReplyCards.Message(s, plea);
            Assert.That(cold, Does.Contain("seen eye to eye"), "Their warmth toward the player is theirs: the card does not say it.");
            SetScore(s, from, s.playerId, -90);
            Assert.That(ReplyCards.Message(s, plea), Is.EqualTo(cold), "and the words do not move when it does.");
            SetScore(s, s.playerId, from, ReplyCards.CloseReading);
            Assert.That(ReplyCards.Message(s, plea), Does.Contain("we've been close"), "At the player's own Friendly line, the close words.");
            SetScore(s, s.playerId, from, -10);
            s.alliances.Add(new AllianceState { id = "alliance-close", name = "The Close", members = new List<string> { s.playerId, from }, active = true });
            Assert.That(ReplyCards.Message(s, plea), Does.Contain("we've been close"), "and in a pact with them, whatever the number.");

            var confronting = new ReplyCardState { id = "reply-2", week = s.week, kind = ReplyCards.Confrontation, fromId = s.nominees[1] };
            SetScore(s, s.playerId, s.nominees[1], 0);
            SetScore(s, s.nominees[1], s.playerId, -90);
            Assert.That(ReplyCards.Message(s, confronting), Does.StartWith("“We need to talk."), "Their hostility is theirs too.");
            SetScore(s, s.playerId, s.nominees[1], ReplyCards.HostileReading);
            Assert.That(ReplyCards.Message(s, confronting), Does.StartWith("“I'm done pretending"), "At the player's own Hostile line, the hard words.");
        }

        [Test]
        public void TheGoalsAreTheWeeksPleasTheReadAndTheCalls()
        {
            var s = Shaped(31, false);
            string first = s.nominees[0], second = s.nominees[1];
            var voters = NpcVoters(s);
            Assert.That(voters, Is.Not.Empty);
            s.ledger.replies.Add(new ReplyRow { week = s.week, cardId = "reply-1", kind = ReplyCards.Plea, fromId = first, replyKey = "noncommittal" });
            s.replyCards.Add(new ReplyCardState { id = "reply-2", week = s.week, kind = ReplyCards.Plea, fromId = second, aboutId = first });
            Assert.That(Rows(s), Is.EqualTo(new[]
            {
                "[x] Answer " + s.Find(first).name + "'s plea · You: stay noncommittal",
                "[ ] Answer " + s.Find(second).name + "'s plea · Waiting on you",
                "[ ] Get a read on the voters · 0 of " + voters.Length,
            }), "The pleas in the order they came, answered or waiting, then the read.");

            s.ledger.claims.Add(new ClaimRow { week = s.week, voterId = voters[0].id, targetId = first, source = ClaimSource.Told });
            var read = CampaignBrief.Goals(s).Single(g => g.kind == CampaignBrief.GoalKinds.Read);
            Assert.That((read.progress, read.done), Is.EqualTo(("1 of " + voters.Length, voters.Length == 1)), "What a voter told the player is a read on them.");

            Assert.That(CampaignBrief.Goals(s).Any(g => g.kind == CampaignBrief.GoalKinds.Call), Is.False, "No pact, no call.");
            EpisodeEngine.EnableLevers(s);
            var pact = new AllianceState { id = "alliance-brief", name = "The Brief", members = new List<string> { s.playerId, voters[0].id }, active = true };
            s.alliances.Add(pact);
            var call = CampaignBrief.Goals(s).Single(g => g.kind == CampaignBrief.GoalKinds.Call);
            Assert.That((call.text, call.progress, call.done), Is.EqualTo(("Call the vote in The Brief", "Once this week", false)));
            s.ledger.calls.Add(new BlocCallRow { week = s.week, allianceId = pact.id, callerId = s.playerId, targetId = first });
            call = CampaignBrief.Goals(s).Single(g => g.kind == CampaignBrief.GoalKinds.Call);
            Assert.That((call.progress, call.done), Is.EqualTo(("Called", true)), "Called, it is done.");
            Assert.That(CampaignBrief.Goals(s).Any(g => g.kind == CampaignBrief.GoalKinds.Ask), Is.False, "Off the block there is nothing to plead for.");

            foreach (var row in s.ledger.replies) row.week = s.week - 1;
            Assert.That(Rows(s).Count(line => line.StartsWith("[x] Answer ")), Is.EqualTo(0), "Last week's answer is last week's.");

            s.phase = EpisodePhase.Eviction;
            Assert.That(CampaignBrief.Goals(s), Is.Empty, "Outside the campaign there are none.");
        }

        [Test]
        public void FromTheBlockTheGoalIsToAskTheVotersToKeepYou()
        {
            var s = Shaped(31, true);
            s.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableLevers(s);
            var voters = NpcVoters(s);
            Assert.That(voters, Is.Not.Empty);
            var ask = CampaignBrief.Goals(s).Single(g => g.kind == CampaignBrief.GoalKinds.Ask);
            Assert.That((ask.text, ask.progress, ask.done), Is.EqualTo(("Ask the voters to keep you", "0 of " + voters.Length, false)));
            foreach (var voter in voters)
                s.lobbies.Add(new LobbyState
                {
                    week = s.week, phase = EpisodePhase.Campaign, deciderId = voter.id, ask = LobbyAsk.Vote, subjectId = s.playerId,
                    approach = LobbyApproach.Emotional, response = LobbyResponse.Open,
                });
            ask = CampaignBrief.Goals(s).Single(g => g.kind == CampaignBrief.GoalKinds.Ask);
            Assert.That((ask.progress, ask.done), Is.EqualTo((voters.Length + " of " + voters.Length, true)), "Every voter asked, it is done.");
            s.leverRulesStartWeek = 0;
            Assert.That(CampaignBrief.Goals(s).Any(g => g.kind == CampaignBrief.GoalKinds.Ask), Is.False, "Before the levers the plea does not exist.");
        }

        [Test]
        public void IntelIsWhatCameToYouAndWhatYouLearnedNewestFirst()
        {
            var s = Shaped(31, false);
            s.week = 3;
            s.events.Clear();
            var voters = NpcVoters(s);
            Assert.That(voters.Length, Is.GreaterThanOrEqualTo(2));
            string other = s.Active.First(c => !c.isPlayer && !voters.Any(v => v.id == c.id)).id;
            int sequence = 1000;
            void Log(int week, string kind, string text, params string[] audience) =>
                s.events.Add(new EpisodeEvent { sequence = sequence++, week = week, phase = EpisodePhase.Campaign, kind = kind, text = text, audienceIds = audience.ToList() });
            Log(2, "gossip", "Week two's rumour about you.", s.playerId);
            Log(2, "campaign", "Last week's plea.", s.playerId);
            Log(3, "campaign", "This week's plea.", s.playerId);
            Log(3, "talk", "Two houseguests talking.");
            Log(3, "information", "Somebody else's news.", other);
            Log(3, "information", "This week's name.", s.playerId);
            s.ledger.claims.Add(new ClaimRow { week = 3, voterId = voters[0].id, targetId = s.nominees[0], source = ClaimSource.Told });
            s.ledger.standings.Add(new StandingRow { week = 1, fromId = voters[1].id, toId = s.playerId, source = ClaimSource.Read, score = 40 });

            string told = voters[0].name.Split(' ')[0] + " told you: evict " + s.Find(s.nominees[0]).name;
            string readThem = "You read " + voters[1].name.Split(' ')[0] + ": warm on you";
            Assert.That(CampaignBrief.RecentIntel(s).Select(line => line.text), Is.EqualTo(new[] { "This week's name.", "This week's plea.", told }),
                "The newest three: what was brought to the player this week, newest first, then what they learned this week.");
            Assert.That(CampaignBrief.RecentIntel(s, 10).Select(line => line.text),
                Is.EqualTo(new[] { "This week's name.", "This week's plea.", told, "Week two's rumour about you.", readThem }),
                "Never somebody else's news, a line the player was not told, or an old plea that says 'this week'.");
            Assert.That(CampaignBrief.RecentIntel(s, 10).Select(line => CampaignBrief.When(s, line.week)),
                Is.EqualTo(new[] { "This week", "This week", "This week", "Week 2", "Week 1" }), "Dated by the week, as the sim keeps them.");
        }
    }
}
