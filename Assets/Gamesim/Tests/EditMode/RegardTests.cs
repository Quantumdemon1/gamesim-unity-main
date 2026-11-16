using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// How each houseguest sees the player (plan 32 §J, <see cref="Regard"/>; S1's rules-12 terms): one installed fixture per
    /// term, each moving faith by its stated amount; every term reads 0 below story rules 12; the reading is pure and draws
    /// nothing; it ignores what the houseguest cannot know; and the threat totals and the NPCs' deals never read it. Pure
    /// simulation, so the Unity-free subset runs it.
    /// </summary>
    public sealed class RegardTests
    {
        private static (EpisodeState s, string npc) House(int rules = StoryRules.Manners, bool commitments = false)
        {
            var s = VoteAnswerTests.Campaign(rules, 7, commitments);
            // A houseguest off the block and out of the power: the fixtures put them where each term needs them.
            string npc = s.Active.Where(c => !c.isPlayer && c.id != s.hohId && c.id != s.vetoHolderId && !s.nominees.Contains(c.id)).Select(c => c.id).First();
            s.Find(npc).traits = new List<string> { "Funny" };
            return (s, npc);
        }

        private static double Term(EpisodeState s, string npc, string term) => Regard.Of(s, npc).Term(term);

        private static PowerRow Row(EpisodeState s, int week)
        {
            var row = s.ledger.power.LastOrDefault(p => p.week == week);
            if (row == null) s.ledger.power.Add(row = new PowerRow { week = week });
            return row;
        }

        // ------------------------------------------------------------ one fixture per term

        [Test]
        public void APactThatSharesIntelIsWorthTwenty()
        {
            var (s, npc) = House(commitments: true);
            VoteAnswerTests.SetScore(s, npc, s.playerId, 20);
            Assert.That(Term(s, npc, Regard.Terms.Pact), Is.Zero);
            VoteAnswerTests.Pact(s, s.playerId, npc);
            Assert.That(EpisodeEngine.SharesIntel(s, npc), Is.True);
            Assert.That(Term(s, npc, Regard.Terms.Pact), Is.EqualTo(Regard.PactBonus));
        }

        [TestCase(BondKinds.Confidant, 15)]
        [TestCase(BondKinds.RideOrDie, 15)]
        [TestCase(BondKinds.Nemesis, -25)]
        public void ABondMovesFaith(string kind, double expected)
        {
            var (s, npc) = House();
            VoteAnswerTests.Bond(s, npc, kind);
            Assert.That(Term(s, npc, Regard.Terms.Bond), Is.EqualTo(expected));
        }

        [Test]
        public void ABetrayedBondCostsTen()
        {
            var (s, npc) = House();
            VoteAnswerTests.Bond(s, npc, BondKinds.Confidant);
            Bonds.End(s, s.playerId, npc, BondKinds.Confidant, true);
            Assert.That(Term(s, npc, Regard.Terms.Bond), Is.EqualTo(Regard.BetrayedBondCost));
        }

        [Test]
        public void YourVetoSavingThemIsTenAWeekToTwenty()
        {
            var (s, npc) = House();
            s.week = 5;
            foreach (int w in new[] { 2, 3, 4 })
            {
                var row = Row(s, w);
                row.vetoHolderId = s.playerId; row.vetoUsed = true; row.savedId = npc;
                if (w == 2) Assert.That(Term(s, npc, Regard.Terms.Saved), Is.EqualTo(10));
            }
            Assert.That(Term(s, npc, Regard.Terms.Saved), Is.EqualTo(Regard.SavedCap));
            Assert.That(Regard.Of(s, npc).reasons, Does.Contain("Your veto saved them"));
        }

        [Test]
        public void PuttingThemUpIsEightAWeekThenFourAfterThreeWeeks()
        {
            var (s, npc) = House();
            s.week = 6;
            var who = s.Find(npc);
            Row(s, 2).hohId = s.playerId; who.nominationWeeks.Add(2);
            Assert.That(Term(s, npc, Regard.Terms.PutUp), Is.EqualTo(Regard.PutUpFaded), "Four weeks ago: faded.");
            Row(s, 5).hohId = s.playerId; who.nominationWeeks.Add(5);
            Assert.That(Term(s, npc, Regard.Terms.PutUp), Is.EqualTo(Regard.PutUpFaded + Regard.PutUpPerWeek));
            // This week, before any row: the live Head of Household and block.
            s.hohId = s.playerId; who.nominationWeeks.Add(6);
            Assert.That(Term(s, npc, Regard.Terms.PutUp), Is.EqualTo(Regard.PutUpFaded + 2 * Regard.PutUpPerWeek));
            Row(s, 1).hohId = s.playerId; who.nominationWeeks.Add(1);
            Row(s, 3).hohId = s.playerId; who.nominationWeeks.Add(3);
            Assert.That(Term(s, npc, Regard.Terms.PutUp), Is.EqualTo(Regard.PutUpCap));
            Assert.That(Regard.Of(s, npc).reasons, Does.Contain("You put them up in week 6"));
        }

        [Test]
        public void LeavingThemUpWithYourVetoIsFiveAWeekAndNeverTheReplacement()
        {
            var (s, npc) = House();
            s.nominees = new List<string> { npc, s.nominees[1] };
            s.vetoHolderId = s.playerId;
            var row = Row(s, s.week);
            row.vetoHolderId = s.playerId; row.vetoUsed = false;
            Assert.That(Term(s, npc, Regard.Terms.LeftUp), Is.EqualTo(Regard.LeftUpPerWeek), "This week, from the meeting on: the live block.");
            row.vetoUsed = true; row.savedId = s.nominees[1]; row.replacementId = npc;
            Assert.That(Term(s, npc, Regard.Terms.LeftUp), Is.Zero, "The replacement went up after the veto was used.");
        }

        [Test]
        public void TookTheVetoTheyNeededIsFiveMoreForTheCompetitive()
        {
            var (s, npc) = House();
            s.nominees = new List<string> { npc, s.nominees[1] };
            s.vetoHolderId = s.playerId;
            var row = Row(s, s.week);
            row.vetoHolderId = s.playerId;
            Assert.That(Term(s, npc, Regard.Terms.TookTheVeto), Is.Zero, "Not for a Funny houseguest.");
            s.Find(npc).traits = new List<string> { "Competitive" };
            Assert.That(Term(s, npc, Regard.Terms.TookTheVeto), Is.EqualTo(Regard.TookTheVetoPerWeek));
            Assert.That(Regard.Of(s, npc).reasons, Does.Contain("You took the veto they needed"));
        }

        [Test]
        public void TheDoubleDealingReceiptCostsTen()
        {
            var (s, npc) = House();
            RelationshipLedger.RecordOneWay(s, npc, s.playerId, StoryReceipts.DoubleDealt, StoryReceipts.Impact(StoryReceipts.DoubleDealt), "kept another alliance from them");
            Assert.That(Term(s, npc, Regard.Terms.DoubleDealing), Is.EqualTo(Regard.DoubleDealingCost));
        }

        [Test]
        public void APactOfYoursTheyCanSeeWithTheirRivalCostsTenAndWithTheirPactMateGivesFive()
        {
            var (s, npc) = House();
            var others = s.Active.Where(c => !c.isPlayer && c.id != npc).Select(c => c.id).ToList();
            string rival = others[0], mate = others[1];
            VoteAnswerTests.SetScore(s, npc, rival, -50);
            VoteAnswerTests.Pact(s, s.playerId, rival);
            Assert.That(Term(s, npc, Regard.Terms.RivalPact), Is.EqualTo(Regard.RivalPactCost));
            VoteAnswerTests.Pact(s, npc, mate);
            VoteAnswerTests.Pact(s, s.playerId, mate);
            Assert.That(Term(s, npc, Regard.Terms.MatePact), Is.EqualTo(Regard.MatePactBonus));
        }

        [Test]
        public void APactTheyCannotSeeMovesNothing()
        {
            var (s, npc) = House();
            string rival = s.Active.First(c => !c.isPlayer && c.id != npc).id;
            VoteAnswerTests.SetScore(s, npc, rival, -50);
            var pact = VoteAnswerTests.Pact(s, s.playerId, rival);
            s.story.facts.Add(new HouseFactState { id = "fact-pact", kind = FactKinds.Alliance, refId = pact.id, actorId = s.playerId, subjectId = rival,
                visibility = FactVisibility.Private, week = s.week, knowers = new List<string> { s.playerId, rival } });
            Assert.That(Term(s, npc, Regard.Terms.RivalPact), Is.Zero, "An unseen pact is never held against you.");
            Assert.That(Regard.Of(s, npc).reasons.Any(r => r.StartsWith("They know you're with", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void SeenWithTheirRivalCostsFiveAndWithTheirAllyGivesThree()
        {
            var (s, npc) = House();
            var others = s.Active.Where(c => !c.isPlayer && c.id != npc).ToList();
            var rival = others[0];
            VoteAnswerTests.SetScore(s, npc, rival.id, -50);
            s.memories.Add(new MemoryState { ownerId = npc, subjectId = s.playerId, text = "Saw you stand up for " + rival.name + ".", week = 1, isPrivate = true });
            Assert.That(Term(s, npc, Regard.Terms.SeenWithRival), Is.Zero, "One sighting is not enough.");
            s.memories.Add(new MemoryState { ownerId = npc, subjectId = s.playerId, text = "Saw you stand up for " + rival.name + ".", week = 2, isPrivate = true });
            Assert.That(Term(s, npc, Regard.Terms.SeenWithRival), Is.EqualTo(Regard.SeenWithRivalCost));
            var ally = others[1];
            VoteAnswerTests.Pact(s, npc, ally.id);
            s.story.cooldowns.Add(new StoryCooldownState { key = "invite:" + s.week + ":" + ally.id, untilWeek = s.week + 1 });
            Assert.That(Term(s, npc, Regard.Terms.SeenWithAlly), Is.EqualTo(Regard.SeenWithAllyBonus));
        }

        [Test]
        public void TheCompetitiveRespectAWinnerWhoNeverPutThemUp()
        {
            var (s, npc) = House();
            s.week = 3;
            s.Find(npc).traits = new List<string> { "Competitive" };
            s.Find(s.playerId).hohWins = 2;
            Assert.That(Term(s, npc, Regard.Terms.Respect), Is.EqualTo(2 * Regard.RespectPerWin));
            s.Find(s.playerId).vetoWins = 3;
            Assert.That(Term(s, npc, Regard.Terms.Respect), Is.EqualTo(Regard.RespectCap));
            Row(s, 1).hohId = s.playerId; s.Find(npc).nominationWeeks.Add(1);
            Assert.That(Term(s, npc, Regard.Terms.Respect), Is.Zero, "Not once you put them up.");
        }

        [Test]
        public void AFloaterFromWeekFiveCostsFiveWithTheStrategic()
        {
            var (s, npc) = House();
            s.Find(npc).traits = new List<string> { "Strategic" };
            s.week = 4;
            Assert.That(Term(s, npc, Regard.Terms.Floater), Is.Zero);
            s.week = 5;
            Assert.That(Term(s, npc, Regard.Terms.Floater), Is.EqualTo(Regard.FloaterCost));
            s.Find(s.playerId).vetoWins = 1;
            Assert.That(Term(s, npc, Regard.Terms.Floater), Is.Zero, "A winner is no floater.");
        }

        [Test]
        public void ThrowingAVetoTheyNeededCostsEightWithTheAnalytical()
        {
            var (s, npc) = House();
            s.week = 4;
            s.Find(npc).traits = new List<string> { "Analytical" };
            s.Find(s.playerId).hohWins = 1;
            s.Find(npc).nominationWeeks.Add(3);
            s.ledger.competitions.Add(new CompetitionRow { week = 3, kind = EpisodePhase.Veto.ToString(), field = 6, placement = 6, entry = "played" });
            var row = Row(s, 3);
            row.vetoHolderId = s.Active.First(c => !c.isPlayer && c.id != npc).id;
            Assert.That(Term(s, npc, Regard.Terms.ThrewIt), Is.EqualTo(Regard.ThrewItCost));
            s.week = 6;
            Assert.That(Term(s, npc, Regard.Terms.ThrewIt), Is.Zero, "Only within three weeks.");
        }

        [Test]
        public void TheRunnerUpToYourWinEnviesOrRespectsIt()
        {
            var (s, npc) = House();
            s.story.cooldowns.Add(new StoryCooldownState { key = Regard.HeadToHeadPrefix + s.week + ":hoh:" + npc, untilWeek = s.week + 3 });
            Assert.That(Term(s, npc, Regard.Terms.HeadToHead), Is.Zero, "Neither for a Funny houseguest.");
            s.Find(npc).traits = new List<string> { "Competitive" };
            Assert.That(Term(s, npc, Regard.Terms.HeadToHead), Is.EqualTo(Regard.HeadToHeadEnvy));
            s.Find(npc).traits = new List<string> { "Analytical" };
            Assert.That(Term(s, npc, Regard.Terms.HeadToHead), Is.EqualTo(Regard.HeadToHeadRespect));
        }

        [TestCase(7, 3)]
        [TestCase(20, 5)]
        public void TimeTogetherIsHalfTheRapportToFive(int rapport, double expected)
        {
            var (s, npc) = House();
            s.story.contacts.Add(new ContactState { npcId = npc, rapport = rapport, weekCount = 1, lastWeek = s.week });
            Assert.That(Term(s, npc, Regard.Terms.Time), Is.EqualTo(expected));
        }

        /// <summary>The engine's head-to-head record (§F): from 12, the runner-up of the player's win, for three weeks; nothing below 12 or for another's win.</summary>
        [TestCase(StoryRules.Manners, true, true)]
        [TestCase(StoryRules.Manners, false, false)]
        [TestCase(StoryRules.Roles, true, false)]
        public void TheEngineRecordsTheRunnerUpOfYourWin(int rules, bool playerWins, bool recorded)
        {
            var (s, npc) = House(rules);
            string other = s.Active.First(c => !c.isPlayer && c.id != npc).id;
            s.competitionScores = new List<CompetitionScore>
            {
                new CompetitionScore { contestantId = other, score = 40 },
                new CompetitionScore { contestantId = npc, score = 70 },
                new CompetitionScore { contestantId = s.playerId, score = playerWins ? 90 : 10 },
            };
            uint stream = s.randomState;
            typeof(EpisodeEngine).GetMethod("RecordHeadToHead", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { s, StoryAnchors.HohCrowned });
            var keys = s.story.cooldowns.Where(c => c.key.StartsWith(Regard.HeadToHeadPrefix, StringComparison.Ordinal)).ToList();
            Assert.That(keys.Select(k => k.key), recorded ? Is.EqualTo(new[] { Regard.HeadToHeadPrefix + s.week + ":hoh:" + npc }) : Is.Empty);
            if (recorded) Assert.That(keys[0].untilWeek, Is.EqualTo(s.week + EpisodeEngine.HeadToHeadWeeks));
            Assert.That(s.randomState, Is.EqualTo(stream), "It draws nothing.");
            s.competitionScores.Clear();
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, "The record is a valid one: " + error);
        }

        [Test]
        public void YourStyleInTheirStoriesResonatesOrGrates()
        {
            var (s, npc) = House();
            // The first catalogue option taken in the warm approach, in a cycle they were cast in.
            var found = StoryCatalog.All.SelectMany(arc => arc.beats.SelectMany(beat => beat.options
                    .Where(o => o.approach == Personality.Approach.Warm).Select(o => (arc, beat, option: o)))).First();
            s.storylines.Add(new StorylineState
            {
                id = "cycle-style", templateId = found.arc.id, week = s.week,
                cast = new List<StoryRoleState> { new StoryRoleState { role = "them", contestantId = npc } },
                path = new List<StoryStepState> { new StoryStepState { beatId = found.beat.id, optionId = found.option.id, week = s.week } },
            });
            Assert.That(Regard.TopStyle(s, npc), Is.EqualTo(Personality.Approach.Warm));
            s.Find(npc).traits = new List<string> { "Emotional" };
            Assert.That(Term(s, npc, Regard.Terms.Style), Is.EqualTo(Regard.StyleTerm), "Warm resonates with the warm.");
            s.Find(npc).traits = new List<string> { "Manipulative", "Analytical" };
            Assert.That(Personality.Reception(s.Find(npc), Personality.Approach.Warm), Is.EqualTo(-1));
            Assert.That(Term(s, npc, Regard.Terms.Style), Is.EqualTo(-Regard.StyleTerm), "and grates on the cold and steady.");
            Assert.That(Regard.TopStyle(s, s.Active.First(c => !c.isPlayer && c.id != npc).id), Is.Null, "Only their own stories count.");
        }

        // ------------------------------------------------------------ the rules

        [Test]
        public void EveryRulesTwelveTermReadsZeroBelowTwelve()
        {
            foreach (int rules in new[] { StoryRules.Roles, StoryRules.Manners })
            {
                var (s, npc) = House(rules, true);
                VoteAnswerTests.SetScore(s, npc, s.playerId, 20);
                VoteAnswerTests.Pact(s, s.playerId, npc);
                VoteAnswerTests.Bond(s, npc, BondKinds.Confidant);
                s.Find(npc).traits = new List<string> { "Competitive" };
                s.Find(s.playerId).hohWins = 2;
                s.story.contacts.Add(new ContactState { npcId = npc, rapport = 9, weekCount = 1, lastWeek = s.week });
                var names = Regard.Of(s, npc).terms.Select(t => t.Key).ToList();
                if (rules < StoryRules.Manners)
                    Assert.That(names.All(n => n == Regard.Terms.Trust || n == Regard.Terms.View || n == Regard.Terms.Grudge), Is.True, string.Join(",", names));
                else
                    Assert.That(names, Is.SupersetOf(new[] { Regard.Terms.Pact, Regard.Terms.Bond, Regard.Terms.Respect, Regard.Terms.Time }));
            }
        }

        [Test]
        public void FaithAndFearStayInsideTheirBounds()
        {
            var (s, npc) = House();
            VoteAnswerTests.SetScore(s, npc, s.playerId, 100);
            VoteAnswerTests.Bond(s, npc, BondKinds.Confidant);
            Assert.That(Regard.Of(s, npc).faith, Is.InRange(0, 100));
            VoteAnswerTests.SetScore(s, npc, s.playerId, -100);
            VoteAnswerTests.Nemesis(s, npc);
            Grudges.Add(s, npc, s.playerId, 100, GrudgeCauses.Nominated);
            var low = Regard.Of(s, npc);
            Assert.That(low.terms.Sum(t => t.Value), Is.LessThan(0), "The terms sum below nothing,");
            Assert.That(low.faith, Is.EqualTo(0), "and faith stops at 0.");
            s.Find(s.playerId).hohWins = 4; s.Find(s.playerId).vetoWins = 2;
            Assert.That(Regard.Of(s, npc).fear, Is.EqualTo(Regard.FearCap));
            s.Find(s.playerId).hohWins = 1; s.Find(s.playerId).vetoWins = 1;
            Assert.That(Regard.Of(s, npc).fear, Is.EqualTo(14));
        }

        [Test]
        public void TheReadingIsPureAndDrawsNothing()
        {
            var (s, npc) = House();
            string before = JObject.FromObject(s).ToString(Formatting.None);
            var a = Regard.Of(s, npc);
            var b = Regard.Of(s, npc);
            Assert.That(JObject.FromObject(s).ToString(Formatting.None), Is.EqualTo(before));
            Assert.That(b.faith, Is.EqualTo(a.faith));
        }

        [Test]
        public void TheReadingIgnoresWhatTheyCannotKnow()
        {
            var (s, npc) = House();
            double faith = Regard.Of(s, npc).faith;
            var other = s.Active.First(c => !c.isPlayer && c.id != npc).id;
            // The player's own ballot, rapport with others, and another houseguest's hidden view of the player.
            s.ledger.ballots.Add(new BallotRow { week = s.week, voterId = s.playerId, targetId = npc });
            s.story.contacts.Add(new ContactState { npcId = other, rapport = 20, weekCount = 3, lastWeek = s.week });
            VoteAnswerTests.SetScore(s, other, s.playerId, -90);
            Assert.That(Regard.Of(s, npc).faith, Is.EqualTo(faith));
        }

        /// <summary>The threat totals and the NPCs' deals never read Regard: comments aside, neither file names it.</summary>
        [TestCase("ThreatAssessment.cs")]
        [TestCase("NpcDeals.cs")]
        public void NoNpcChoiceReadsRegard(string file)
        {
            string code = File.ReadAllText(Path.Combine(SourceRoot(), "Simulation", file));
            code = System.Text.RegularExpressions.Regex.Replace(code, @"^\s*//.*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            Assert.That(code.Length, Is.GreaterThan(1000), "The file was read.");
            Assert.That(System.Text.RegularExpressions.Regex.IsMatch(code, @"\bRegard\s*\."), Is.False);
        }

        /// <summary>Assets/Gamesim, found upward from the working directory or the test binary (StressHouseTests' finder).</summary>
        private static string SourceRoot()
        {
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string binary = typeof(RegardTests).Assembly.Location;
            var starts = new List<string> { Directory.GetCurrentDirectory() };
            if (!string.IsNullOrEmpty(binary)) starts.Add(Path.GetDirectoryName(binary));
            foreach (string start in starts)
            {
                string at = start;
                for (int depth = 0; depth < 12 && !string.IsNullOrEmpty(at); depth++)
                {
                    string candidate = Path.Combine(at, "Assets", "Gamesim");
                    if (File.Exists(Path.Combine(candidate, "Simulation", "SeasonBuilder.cs"))) { found.Add(Plain(Path.GetFullPath(candidate))); break; }
                    string next = Path.GetDirectoryName(at);
                    if (next == at) break;
                    at = next;
                }
            }
            Assert.That(found, Has.Count.EqualTo(1), "Exactly one Assets/Gamesim source tree above the test: " + string.Join(", ", found));
            return found.Single();
        }

        private static string Plain(string path) =>
            path.StartsWith(@"\\?\UNC\", StringComparison.Ordinal) ? @"\\" + path.Substring(8)
            : path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path.Substring(4) : path;
    }
}
