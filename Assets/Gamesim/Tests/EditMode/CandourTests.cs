using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Whether a houseguest tells the player the truth, and how it sounds (plan 32 §J "Candour" and §G2 "Manners and
    /// tells", S1): the band × stake × trait matrix always sums to one, the pinned cells and the worked examples, the fixed
    /// values in their order, the keyed coin that never re-rolls, and the tells' rates against their formulas over keyed
    /// occasions. Pure simulation, so the Unity-free subset runs it.
    /// </summary>
    public sealed class CandourTests
    {
        private static ContestantState Who(params string[] traits) => new ContestantState { id = "hg-x", name = "Casey Test", traits = traits.ToList() };

        private static readonly string[][] ShiftGroups =
        {
            new[] { "Strategic" }, new[] { "Sneaky" }, new[] { "Emotional" }, new[] { "Analytical" }, new[] { "Loyal" }, new[] { "Deceptive" }, new[] { "Introverted" },
        };

        // ------------------------------------------------------------ the matrix

        [Test]
        public void EveryCellOfTheMatrixIsThreeWeightsThatSumToOne()
        {
            foreach (double lean in new[] { -40.0, 0, 14.99, 15, 29.5, 30, 44, 45, 64.9, 65, 100 })
            foreach (int active in new[] { 3, 6, 7, 12 })
            foreach (bool nemesisVote in new[] { false, true })
            for (int mask = 0; mask < 1 << ShiftGroups.Length; mask++)
            {
                var traits = Enumerable.Range(0, ShiftGroups.Length).Where(i => (mask & (1 << i)) != 0).SelectMany(i => ShiftGroups[i]).ToArray();
                var w = Candour.Shift(Candour.Band(lean), Who(traits), active, nemesisVote);
                Assert.That(w.honest >= 0 && w.neutral >= 0 && w.dishonest >= 0, Is.True, lean + " " + string.Join("+", traits) + ": " + w);
                Assert.That(w.honest + w.neutral + w.dishonest, Is.EqualTo(100), lean + " " + string.Join("+", traits));
                if (nemesisVote) Assert.That(w.honest, Is.Zero, "A nemesis is never honest on the vote, Loyal included.");
                if (traits.Contains("Loyal")) Assert.That(w.dishonest, Is.Zero, "A Loyal houseguest never lies; they clam up.");
            }
        }

        [Test]
        public void TheTwoPinnedCells()
        {
            Assert.That(Candour.Shift(Candour.Band(10), Who("Strategic"), 6, false).ToString(), Is.EqualTo("0/35/65"), "Strategic, lean < 15, six left.");
            Assert.That(Candour.Shift(Candour.Band(70), Who("Deceptive", "Emotional"), 10, false).ToString(), Is.EqualTo("90/0/10"), "Deceptive+Emotional, lean ≥ 65.");
        }

        [Test]
        public void TheBandsAreHalfOpenFromTheBottom()
        {
            Assert.That(Candour.Band(65).ToString(), Is.EqualTo("90/10/0"));
            Assert.That(Candour.Band(64.999).ToString(), Is.EqualTo("60/30/10"));
            Assert.That(Candour.Band(45).ToString(), Is.EqualTo("60/30/10"));
            Assert.That(Candour.Band(30).ToString(), Is.EqualTo("35/40/25"));
            Assert.That(Candour.Band(15).ToString(), Is.EqualTo("20/35/45"));
            Assert.That(Candour.Band(14.999).ToString(), Is.EqualTo("10/25/65"));
        }

        /// <summary>§J's worked examples, from their leans: Jamie the wary stranger and Casey the burned rival.</summary>
        [Test]
        public void TheWorkedExamples()
        {
            // Jamie (Strategic+Analytical: Honest −1, Steady 4): faith 51, stake 1, lean 51 − 15 − 5 = 31.
            var jamie = Who("Strategic", "Analytical");
            Assert.That(Personality.Of(jamie).Honest, Is.EqualTo(-1));
            Assert.That(Personality.Of(jamie).Steady, Is.EqualTo(4));
            double lean = 51 - Candour.StakeLean * 1 + Candour.HonestLean * Personality.Of(jamie).Honest;
            Assert.That(lean, Is.EqualTo(31));
            Assert.That(Candour.Shift(Candour.Band(lean), jamie, 7, false).ToString(), Is.EqualTo("25/50/25"));
            Assert.That(Candour.Shift(Candour.Band(lean), jamie, 6, false).ToString(), Is.EqualTo("15/60/25"));
            Assert.That(Manner.LiarTell(jamie, false), Is.EqualTo(0.18).Within(1e-9));
            Assert.That(Manner.Dominant(jamie), Is.EqualTo(Manner.TurnedBack));
            // Casey (Sneaky+Competitive: Bold 1, Honest −1, Steady 1): faith 0, stake 2, lean −35.
            var casey = Who("Sneaky", "Competitive");
            Assert.That(Candour.Shift(Candour.Band(0 - 30 - 5), casey, 8, false).ToString(), Is.EqualTo("10/10/80"));
            Assert.That(Manner.LiarTell(casey, false), Is.EqualTo(0.36).Within(1e-9));
            Assert.That(Manner.Dominant(casey), Is.EqualTo(Manner.Hedged));
        }

        // ------------------------------------------------------------ the fixed values

        [Test]
        public void TheFixedValuesInTheirOrder()
        {
            var s = VoteAnswerTests.Campaign(StoryRules.Manners, 7, true);
            string npc = VoteAnswerTests.Voter(s, 0);
            Assert.That(Candour.Fixed(s, npc), Is.Null, "Nobody special: the coin decides.");
            VoteAnswerTests.Bond(s, npc, BondKinds.Confidant);
            Assert.That(Candour.Fixed(s, npc), Is.EqualTo(Candour.Honest), "A confidant is Honest.");
            VoteAnswerTests.Nemesis(s, npc);
            Assert.That(Candour.Fixed(s, npc), Is.Null, "A confidant who is also a nemesis is never fixed Honest.");
            s.story.cooldowns.Add(new StoryCooldownState { key = Candour.LiarPrefix + npc, untilWeek = s.week + 2 });
            Assert.That(Candour.Fixed(s, npc), Is.EqualTo(Candour.Neutral), "Under liar: they avoid you.");
            VoteAnswerTests.SetScore(s, npc, s.playerId, 30);
            VoteAnswerTests.Pact(s, s.playerId, npc);
            Assert.That(EpisodeEngine.SharesIntel(s, npc), Is.True);
            Assert.That(Candour.Fixed(s, npc), Is.EqualTo(Candour.Honest), "A C6 ally is Honest above everything.");
        }

        [Test]
        public void ANemesisOnTheVoteHasNoHonestWeight()
        {
            var s = VoteAnswerTests.Campaign(StoryRules.Manners, 11);
            string npc = VoteAnswerTests.Voter(s, 0);
            VoteAnswerTests.SetScore(s, npc, s.playerId, 100);
            VoteAnswerTests.Nemesis(s, npc);
            foreach (var traits in new[] { new[] { "Loyal" }, new[] { "Emotional" }, new string[0] })
            {
                s.Find(npc).traits = traits.ToList();
                Assert.That(Candour.WeightsFor(s, npc, Candour.Vote, null).honest, Is.Zero, string.Join("+", traits));
                Assert.That(Candour.WeightsFor(s, npc, Candour.Intel, null).honest, Is.GreaterThan(0), "Shift 5 is the vote's alone.");
            }
        }

        // ------------------------------------------------------------ the coin

        [Test]
        public void TheSameKeyAndStateGiveTheSameAnswerAndDrawNothing()
        {
            var s = VoteAnswerTests.Campaign(StoryRules.Manners, 5);
            string npc = VoteAnswerTests.Voter(s, 1);
            uint stream = s.randomState;
            string first = Candour.Choose(s, npc, Candour.Vote, null);
            var copy = Newtonsoft.Json.JsonConvert.DeserializeObject<EpisodeState>(Newtonsoft.Json.JsonConvert.SerializeObject(s));
            Assert.That(Candour.Choose(copy, npc, Candour.Vote, null), Is.EqualTo(first), "A reload never re-rolls.");
            Assert.That(Candour.Choose(s, npc, Candour.Vote, null), Is.EqualTo(first));
            Assert.That(s.randomState, Is.EqualTo(stream));
            Assert.That(Candour.Key(s, npc, Candour.Vote, null), Is.EqualTo("w" + s.week + ":candour:vote:" + npc + ":-"));
        }

        [Test]
        public void TheStakeIsTwoWhenTheyAreAfterYouOrFourAreLeft()
        {
            var s = VoteAnswerTests.Campaign(StoryRules.Manners, 9, false);
            s.nominees[0] = s.playerId;
            foreach (var voter in EpisodeEngine.Voters(s).Where(v => !v.isPlayer))
            {
                int stake = Candour.Stake(s, voter.id, Candour.Vote, null);
                Assert.That(stake, Is.InRange(0, Candour.StakeMost));
                if (Candour.TargetOf(s, voter.id) == s.playerId) Assert.That(stake, Is.EqualTo(Candour.StakeMost), "After the player: the most.");
            }
            // Four left: the most, whoever is asked about what.
            string asked = s.nominees[1];
            foreach (var gone in s.Active.Where(c => !c.isPlayer && c.id != asked && c.id != s.hohId).Skip(1).ToList()) gone.status = ContestantStatus.Evicted;
            Assert.That(s.Active.Count(), Is.LessThanOrEqualTo(Candour.StakeAtActive));
            foreach (string question in new[] { Candour.Vote, Candour.Talk, Candour.Intel })
                Assert.That(Candour.Stake(s, asked, question, null), Is.EqualTo(Candour.StakeMost), question);
        }

        [Test]
        public void TheStakeReadsOnlyPactsTheyCanSee()
        {
            bool seen = false;
            for (uint seed = 1; seed <= 40 && !seen; seed++)
            {
                var s = VoteAnswerTests.Campaign(StoryRules.Manners, seed, false);
                string npc = VoteAnswerTests.Voter(s, 0);
                string target = Candour.TargetOf(s, npc);
                if (target == null || target == s.playerId) continue;
                int before = Candour.Stake(s, npc, Candour.Vote, null);
                if (before >= Candour.StakeMost) continue;
                seen = true;
                var pact = VoteAnswerTests.Pact(s, s.playerId, target);
                s.story.facts.Add(new HouseFactState { id = "fact-hidden", kind = FactKinds.Alliance, refId = pact.id, actorId = s.playerId, subjectId = target,
                    visibility = FactVisibility.Private, week = s.week, knowers = new List<string> { s.playerId, target } });
                Assert.That(Candour.Stake(s, npc, Candour.Vote, null), Is.EqualTo(before), "A pact they cannot see is no stake.");
                s.story.facts.Single(f => f.id == "fact-hidden").knowers.Add(npc);
                Assert.That(Candour.Stake(s, npc, Candour.Vote, null), Is.EqualTo(before + 1), "One they know of is.");
            }
            Assert.That(seen, Is.True, "The seeds see a voter after someone else, below the most stake.");
        }

        // ------------------------------------------------------------ manners

        [Test]
        public void TheTellFormulas()
        {
            Assert.That(Manner.LiarTell(Who("Deceptive", "Strategic"), false), Is.EqualTo(0.15).Within(1e-9), "The clamp's floor.");
            Assert.That(Manner.LiarTell(Who("Deceptive", "Strategic"), true), Is.EqualTo(0.24).Within(1e-9));
            Assert.That(Manner.LiarTell(Who("Confrontational", "Emotional"), false), Is.EqualTo(0.64).Within(1e-9));
            Assert.That(Manner.LiarTell(Who("Confrontational", "Impulsive"), false), Is.EqualTo(0.70).Within(1e-9));
            Assert.That(Manner.Dominant(Who("Confrontational", "Emotional")), Is.EqualTo(Manner.LaidOnThick));
            Assert.That(Manner.HonestTell(Who("Sneaky", "Competitive"), true, false), Is.EqualTo(0.37).Within(1e-9), "Casey hedges on a thin margin.");
            Assert.That(Manner.HonestTell(Who("Strategic"), false, false), Is.EqualTo(0.07).Within(1e-9));
            Assert.That(Manner.HonestTell(Who("Emotional", "Impulsive"), true, false), Is.EqualTo(0.45).Within(1e-9));
        }

        private static readonly string[][] Cells =
        {
            new[] { "Deceptive", "Strategic" }, new[] { "Confrontational", "Emotional" }, new[] { "Sneaky", "Competitive" },
            new[] { "Analytical" }, new[] { "Funny" }, new[] { "Charming", "Social" },
        };

        /// <summary>Over 2,000 keyed occasions per cell and margin: the rates match t_L and t_H, both outcomes show, and the dominant tell is 70%.</summary>
        [Test]
        public void TheTellsMatchTheirFormulasOverKeyedOccasions()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 4242u);
            int pooledTold = 0, pooledDominant = 0;
            foreach (var traits in Cells)
            foreach (bool thin in new[] { false, true })
            {
                var who = Who(traits);
                int lies = 0, liesTold = 0, liesDominant = 0, truths = 0, truthsTold = 0, truthsDominant = 0;
                string dominant = Manner.Dominant(who);
                for (int i = 0; i < 2000; i++)
                {
                    who.id = "hg-" + (thin ? "t" : "w") + i;
                    s.week = 1 + i % 13;
                    string lie = Manner.Choose(s, who, Candour.Vote, Candour.Dishonest, thin, false);
                    lies++;
                    if (Manner.IsTell(lie)) { liesTold++; if (lie == dominant) liesDominant++; }
                    who.id = "hg-h" + (thin ? "t" : "w") + i;
                    string truth = Manner.Choose(s, who, Candour.Vote, Candour.Honest, thin, false);
                    truths++;
                    if (Manner.IsTell(truth)) { truthsTold++; if (truth == dominant) truthsDominant++; }
                }
                string cell = string.Join("+", traits) + (thin ? " thin" : " wide");
                Assert.That(liesTold / (double)lies, Is.EqualTo(Manner.LiarTell(who, false)).Within(0.03), cell + ": t_L");
                Assert.That(truthsTold / (double)truths, Is.EqualTo(Manner.HonestTell(who, thin, false)).Within(0.03), cell + ": t_H");
                Assert.That(liesTold, Is.InRange(1, lies - 1), cell + ": a liar both tells and doesn't.");
                pooledTold += liesTold; pooledDominant += liesDominant;
                if (!thin || dominant != Manner.Hedged)
                    Assert.That(liesDominant / (double)lies, Is.GreaterThan(truthsDominant / (double)truths), cell + ": the dominant tell is evidence of a lie.");
                if (thin) Assert.That(truthsDominant == 0 || dominant == Manner.Hedged, Is.True, cell + ": an honest tell on a thin margin is a hedge.");
            }
            // A liar's tell is their dominant one 70% of the time, over every cell's told lies together (a cell's own few
            // hundred are too few to hold ±3%).
            Assert.That(pooledTold, Is.GreaterThan(4000));
            Assert.That(pooledDominant / (double)pooledTold, Is.EqualTo(Manner.DominantShare).Within(0.03), "the dominant tell");
        }

        [Test]
        public void ANeutralAnswerTakesItsVoicesMannerAndDrawsNothing()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 1u);
            Assert.That(Manner.Choose(s, Who("Competitive"), Candour.Vote, Candour.Neutral, false, false), Is.EqualTo(Manner.Deflect), "Blunt");
            Assert.That(Manner.Choose(s, Who("Analytical"), Candour.Vote, Candour.Neutral, false, false), Is.EqualTo(Manner.Deflect), "Guarded");
            Assert.That(Manner.Choose(s, Who("Social"), Candour.Vote, Candour.Neutral, false, false), Is.EqualTo(Manner.NonAnswer), "Chatty");
            Assert.That(Manner.Choose(s, Who("Emotional"), Candour.Vote, Candour.Neutral, false, false), Is.EqualTo(Manner.NonAnswer), "Warm");
            Assert.That(Manner.Choose(s, Who("Stubborn"), Candour.Vote, Candour.Neutral, false, false), Is.EqualTo(Manner.NonAnswer), "Plain");
        }

        [Test]
        public void TheVoicesReadTraitsButNeverTheHonestAxis()
        {
            Assert.That(Voice.Of(Who("Competitive"), false), Is.EqualTo(Voice.Blunt));
            Assert.That(Voice.Of(Who("Emotional"), false), Is.EqualTo(Voice.Warm));
            Assert.That(Voice.Of(Who("Social"), false), Is.EqualTo(Voice.Chatty));
            Assert.That(Voice.Of(Who("Analytical"), false), Is.EqualTo(Voice.Guarded));
            Assert.That(Voice.Of(Who("Introverted"), false), Is.EqualTo(Voice.Guarded));
            Assert.That(Voice.Of(Who("Loyal"), false), Is.EqualTo(Voice.Plain));
            Assert.That(Voice.Of(Who("Deceptive"), false), Is.EqualTo(Voice.Plain), "Loyal and Deceptive differ only in Honest, and sound the same.");
            Assert.That(Voice.Of(Who("Competitive"), true), Is.EqualTo(Voice.Desperate));
        }
    }
}
