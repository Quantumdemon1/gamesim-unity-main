using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The vote answer at story rules 12 (plan 32 §G1 and §G4, S1): below 12 "where's your head at?" answers exactly as it
    /// always did; at 12 the voter answers in their voice and a manner, the stance chosen by <see cref="Candour"/>, the record
    /// kept in a <c>said:</c> key, the season's main stream drawn exactly as at 11, the reveal teaching a tell only from a real
    /// lie, the lie's cost through a keyed change, and the read giving its word on the claim from a keyed coin. Pure
    /// simulation, so the Unity-free subset runs it.
    /// </summary>
    public sealed class VoteAnswerTests
    {
        // ------------------------------------------------------------ below 12, unchanged

        [Test]
        public void AtElevenTheAnswerIsTheLineAndTheClaimItAlwaysWas()
        {
            var s = Campaign(StoryRules.Roles);
            string npc = Voter(s, 0);
            s.Find(npc).traits = new List<string> { "Loyal" };
            string truth = EpisodeEngine.ProjectBallot(s, npc).selectedNomineeId;
            var after = Accepted(s, EpisodeCommandKind.AskVote, npc);
            var line = after.events.Last();
            Assert.That(line.text, Is.EqualTo("You asked " + after.Find(npc).name + " where their head is at. \"" + after.Find(truth).name + ".\""));
            Assert.That(after.ledger.claims.Single().targetId, Is.EqualTo(truth));
            Assert.That(after.story.cooldowns.Any(c => c.key.StartsWith(EpisodeEngine.SaidPrefix, StringComparison.Ordinal)), Is.False, "No record below 12.");
        }

        [Test]
        public void AtElevenAStrategistStillDeflectsWithTodaysLine()
        {
            var s = Campaign(StoryRules.Roles);
            string npc = Voter(s, 0);
            s.Find(npc).traits = new List<string> { "Strategic" };
            SetScore(s, npc, s.playerId, 0);
            var after = Accepted(s, EpisodeCommandKind.AskVote, npc);
            Assert.That(after.events.Last().text, Is.EqualTo(after.Find(npc).name + " wouldn't say where their vote is. \"Ask me after.\""));
            Assert.That(after.randomState, Is.EqualTo(s.randomState));
        }

        // ------------------------------------------------------------ at 12

        [Test]
        public void AtTwelveEveryAnswerWritesOneRecordAndTheStanceItsRow()
        {
            int honest = 0, dishonest = 0, neutral = 0;
            for (uint seed = 1; seed <= 120; seed++)
            {
                var s = Campaign(StoryRules.Manners, seed);
                string npc = Voter(s, (int)(seed % 2));
                s.Find(npc).traits = Traits[(int)(seed % Traits.Length)].ToList();
                SetScore(s, npc, s.playerId, -40 + (seed % 9) * 10);
                string truth = EpisodeEngine.ProjectBallot(s, npc).selectedNomineeId;
                var after = Accepted(s, EpisodeCommandKind.AskVote, npc);
                var keys = after.story.cooldowns.Where(c => c.key.StartsWith(EpisodeEngine.SaidPrefix, StringComparison.Ordinal)).ToList();
                Assert.That(keys.Count, Is.EqualTo(1), "One record per answer.");
                Assert.That(keys[0].untilWeek, Is.EqualTo(after.week + 1), "Read this week only.");
                var said = EpisodeEngine.Said(after, npc, after.week);
                Assert.That(said, Is.Not.Null);
                var line = after.events.Last();
                Assert.That(line.kind, Is.EqualTo("vote-read"));
                Assert.That(line.text, Does.StartWith("You asked " + after.Find(npc).name + " where their head is at. \""));
                Assert.That(EpisodeEngine.AskedThisWeek(after, npc), Is.True, "The week's ask, whatever the stance.");
                if (said.stance == Candour.Neutral)
                {
                    neutral++;
                    Assert.That(after.ledger.claims, Is.Empty);
                    Assert.That(after.ledger.standings.Single().source, Is.EqualTo(ClaimSource.Deflected), "Neutral writes the Deflected standing.");
                    Assert.That(said.manner, Is.EqualTo(Manner.NeutralFor(Voice.Of(after.Find(npc), false))));
                    Assert.That(after.Active.Where(c => !c.isPlayer).All(c => !line.text.Contains(FinalistRead.FirstName(c.name) + ".")), Is.True,
                        "A non-answer names nobody.");
                    continue;
                }
                var claim = after.ledger.claims.Single();
                Assert.That((claim.voterId, claim.source), Is.EqualTo((npc, ClaimSource.Told)));
                if (said.stance == Candour.Honest) { honest++; Assert.That(claim.targetId, Is.EqualTo(truth)); }
                else { dishonest++; Assert.That(claim.targetId, Is.Not.EqualTo(truth)); Assert.That(s.nominees, Does.Contain(claim.targetId)); }
                string first = claim.targetId == after.playerId ? "you" : FinalistRead.FirstName(after.Find(claim.targetId).name);
                Assert.That(line.text.ToLowerInvariant(), Does.Contain(first.ToLowerInvariant()), "The line names who they said.");
                Assert.That(line.text, Does.Not.Contain(HouseAnswers.Name));
            }
            Assert.That(honest, Is.GreaterThan(0), "The seeds see an honest answer,");
            Assert.That(dishonest, Is.GreaterThan(0), "a lie,");
            Assert.That(neutral, Is.GreaterThan(0), "and a non-answer.");
        }

        /// <summary>The main stream is drawn exactly where rules 11 draws it, on every branch: an ally, the deflect test, and anybody else.</summary>
        [Test]
        public void AtTwelveTheMainStreamIsTheSameAsAtElevenOnEveryBranch()
        {
            for (uint seed = 1; seed <= 200; seed++)
            {
                foreach (string branch in new[] { "ally", "deflect", "roll" })
                {
                    uint at11 = Ask(StoryRules.Roles, seed, branch), at12 = Ask(StoryRules.Manners, seed, branch);
                    Assert.That(at12, Is.EqualTo(at11), "seed " + seed + " " + branch);
                }
            }
        }

        private static uint Ask(int rules, uint seed, string branch)
        {
            var s = Campaign(rules, seed, branch == "ally");
            string npc = Voter(s, 0);
            if (branch == "ally")
            {
                s.Find(npc).traits = new List<string> { "Strategic", "Social" };
                SetScore(s, npc, s.playerId, 10);
                Pact(s, s.playerId, npc);
                Assert.That(EpisodeEngine.SharesIntel(s, npc), Is.True);
            }
            else if (branch == "deflect")
            {
                s.Find(npc).traits = new List<string> { "Strategic" };
                SetScore(s, npc, s.playerId, (int)(seed % 20));
            }
            else
            {
                s.Find(npc).traits = Traits[(int)(seed % Traits.Length)].Where(t => t != "Strategic").ToList();
                SetScore(s, npc, s.playerId, -40 + (seed % 9) * 10);
            }
            return Accepted(s, EpisodeCommandKind.AskVote, npc).randomState;
        }

        [Test]
        public void AnAllyAtTwelveAnswersHonestlyAndDrawsNothing()
        {
            var s = Campaign(StoryRules.Manners, 7, true);
            string npc = Voter(s, 0);
            s.Find(npc).traits = new List<string> { "Strategic", "Social" };
            SetScore(s, npc, s.playerId, 10);
            Pact(s, s.playerId, npc);
            Nemesis(s, npc);
            string truth = EpisodeEngine.ProjectBallot(s, npc).selectedNomineeId;
            var after = Accepted(s, EpisodeCommandKind.AskVote, npc);
            Assert.That(EpisodeEngine.Said(after, npc, after.week).stance, Is.EqualTo(Candour.Honest), "A C6 ally is Honest, nemesis or not.");
            Assert.That(after.ledger.claims.Single().targetId, Is.EqualTo(truth));
            Assert.That(after.ledger.standings.Any(r => r.source == ClaimSource.Deflected), Is.False);
            Assert.That(after.randomState, Is.EqualTo(s.randomState), "An ally's answer is decided: no roll.");
        }

        // ------------------------------------------------------------ the reveal

        [Test]
        public void TheRevealTeachesATellOnlyFromALieInATellsManner()
        {
            foreach (var (stance, manner, taught) in new[]
                     {
                         (Candour.Dishonest, Manner.Hedged, true), (Candour.Dishonest, Manner.Definite, false),
                         (Candour.Honest, Manner.Hedged, false), (Candour.Honest, Manner.LaidOnThick, false),
                     })
            {
                var s = Campaign(StoryRules.Manners);
                string npc = Voter(s, 0);
                string said = s.nominees[0], voted = s.nominees[1];
                s.ledger.claims.Add(new ClaimRow { week = s.week, voterId = npc, targetId = said, source = ClaimSource.Told });
                s.story.cooldowns.Add(new StoryCooldownState { key = EpisodeEngine.SaidPrefix + s.week + ":" + npc + ":" + stance + ":" + manner, untilWeek = s.week + 1 });
                s.votes.Add(new VoteState { voterId = npc, targetId = voted });
                uint before = s.randomState;
                Settle(s, voted);
                Assert.That(s.ledger.claims.Single().status, Is.EqualTo(ClaimStatus.Lied), "Judged a lie by the ballot either way,");
                Assert.That(EpisodeEngine.TellsOf(s, npc), taught ? Is.EqualTo(new[] { manner }) : Is.Empty,
                    stance + " " + manner + ": a changed mind teaches nothing, and a plain lie shows no tell.");
                Assert.That(s.randomState, Is.EqualTo(before), "The lie's cost goes through a keyed change at 12, never the main stream.");
                Assert.That(s.memories.Any(m => m.ownerId == s.playerId && m.text == s.Find(npc).name + " told me in week " + s.week
                    + " they would vote out " + s.Find(said).name + ", and voted out " + s.Find(voted).name + "."), Is.True, "The reveal's memory, byte for byte.");
                Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
            }
        }

        [Test]
        public void AtElevenTheLiesCostStillDrawsFromTheMainStream()
        {
            var s = Campaign(StoryRules.Roles);
            string npc = Voter(s, 0);
            s.ledger.claims.Add(new ClaimRow { week = s.week, voterId = npc, targetId = s.nominees[0], source = ClaimSource.Told });
            s.votes.Add(new VoteState { voterId = npc, targetId = s.nominees[1] });
            uint before = s.randomState;
            Settle(s, s.nominees[1]);
            Assert.That(s.randomState, Is.Not.EqualTo(before), "Below 12 the reveal's change is the season's own, as it always was.");
            Assert.That(s.story.cooldowns.Any(c => c.key.StartsWith(EpisodeEngine.TellPrefix, StringComparison.Ordinal)), Is.False);
        }

        // ------------------------------------------------------------ the read

        [Test]
        public void TheReadsWordOnAClaimDrawsOnlyItsKeyedCoin()
        {
            int words = 0;
            for (uint seed = 1; seed <= 60; seed++)
            {
                var s12 = Campaign(StoryRules.Manners, seed);
                var s11 = Campaign(StoryRules.Roles, seed);
                string npc = Voter(s12, 0);
                foreach (var s in new[] { s11, s12 }) s.Find(s.playerId).traits = new List<string> { "Intuitive" };
                // The record is 12's own key: a season at 11 never holds one, and its read draws what it always drew.
                s12.story.cooldowns.Add(new StoryCooldownState { key = EpisodeEngine.SaidPrefix + s12.week + ":" + npc + ":" + Candour.Dishonest + ":" + Manner.Hedged, untilWeek = s12.week + 1 });
                var a11 = new EpisodeEngine(s11).Apply(Command(s11, EpisodeCommandKind.ReadPerson, npc));
                var after = Accepted(s12, EpisodeCommandKind.ReadPerson, npc);
                Assert.That(after.randomState, Is.EqualTo(a11.state.randomState), "seed " + seed + ": the main stream as at 11.");
                bool hit = after.ledger.standings.Any(r => r.fromId == npc && r.source == ClaimSource.Read);
                var last = after.events.Last().text;
                bool word = last == HouseAnswers.ReadDoubt || last == HouseAnswers.ReadHolds;
                Assert.That(word, Is.EqualTo(hit), "The word comes with a hit only.");
                if (word) words++;
            }
            Assert.That(words, Is.GreaterThan(10));
        }

        [TestCase(Candour.Dishonest, 0.6)]
        [TestCase(Candour.Honest, 0.2)]
        public void TheReadDoubtsALieAtSixInTenAndTheTruthAtTwo(string stance, double expected)
        {
            int doubts = 0, n = 0;
            for (uint seed = 1; seed <= 4000; seed++)
            {
                var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                double doubt = stance == Candour.Dishonest ? EpisodeEngine.ReadDoubtAfterLie : EpisodeEngine.ReadDoubtAfterTruth;
                if (StoryRandom.Unit(s, "w" + s.week + ":read-claim:" + s.contestants[1].id) < doubt) doubts++;
                n++;
            }
            Assert.That(doubts / (double)n, Is.EqualTo(expected).Within(0.03));
        }

        [Test]
        public void TheReadSaysNothingOfANonAnswerOrAfterTheReveal()
        {
            var s = Campaign(StoryRules.Manners, 3);
            string npc = Voter(s, 0);
            s.Find(s.playerId).traits = new List<string> { "Intuitive", "Analytical" };
            s.story.cooldowns.Add(new StoryCooldownState { key = EpisodeEngine.SaidPrefix + s.week + ":" + npc + ":" + Candour.Neutral + ":" + Manner.NonAnswer, untilWeek = s.week + 1 });
            for (uint seed = 1; seed <= 20; seed++)
            {
                var copy = s.Clone();
                copy.randomState = seed * 7919u;
                var result = new EpisodeEngine(copy).Apply(Command(copy, EpisodeCommandKind.ReadPerson, npc));
                Assert.That(result.accepted, Is.True, result.reason);
                Assert.That(result.state.events.Any(e => e.text == HouseAnswers.ReadDoubt || e.text == HouseAnswers.ReadHolds), Is.False);
            }
        }

        // ------------------------------------------------------------ the conversation shows the line

        [Test]
        public void AtTwelveTheConversationShowsTheQuotedLine()
        {
            for (uint seed = 1; seed <= 30; seed++)
            {
                var before = Campaign(StoryRules.Manners, seed);
                string npc = Voter(before, 0);
                before.Find(npc).traits = Traits[(int)(seed % Traits.Length)].ToList();
                var command = Command(before, EpisodeCommandKind.AskVote, npc);
                var after = Accepted(before, command);
                var events = after.events.Where(e => e.sequence >= before.nextSequence).ToList();
                var exchange = ConversationExchange.Create(before, after, command, events);
                Assert.That(exchange, Is.Not.Null, "seed " + seed);
                Assert.That(exchange.PlayerLine, Is.EqualTo("Where's your head at on this week's vote?"));
                string text = events.Single(e => e.kind == "vote-read").text;
                string prefix = "You asked " + after.Find(npc).name + " where their head is at. \"";
                Assert.That(exchange.NpcLine, Is.EqualTo(text.Substring(prefix.Length, text.Length - prefix.Length - 1)));
                Assert.That(exchange.OutcomeLine, Is.EqualTo(text));
            }
        }

        // ------------------------------------------------------------ fixtures

        internal static readonly string[][] Traits =
        {
            new[] { "Loyal", "Social" }, new[] { "Sneaky", "Competitive" }, new[] { "Deceptive", "Strategic" }, new[] { "Confrontational", "Emotional" },
            new[] { "Analytical", "Introverted" }, new[] { "Funny", "Charming" }, new[] { "Manipulative", "Impulsive" }, new[] { "Stubborn", "Flexible" },
        };

        /// <summary>The campaign of the catalogue's six-house (AlliesShareIntelTests' fixture), at a story-rules level written by name.</summary>
        internal static EpisodeState Campaign(int rules, uint seed = 7, bool commitments = false)
        {
            var s = ContentCatalog.Create(seed);
            s.strategyRulesStartWeek = 1;
            var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            s.phase = EpisodePhase.Campaign;
            s.hohId = npcs[0];
            s.nominees = new List<string> { npcs[1], npcs[2] };
            s.vetoHolderId = npcs[3];
            s.vetoPlayers = s.Active.Select(c => c.id).Take(EpisodeEngine.VetoPlayerCount(s.Active.Count())).ToList();
            if (!s.vetoPlayers.Contains(s.vetoHolderId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.vetoHolderId;
            s.vetoResolved = true;
            s.randomState = unchecked(seed * 2654435761u + 17u);
            EpisodeEngine.EnableStory(s);
            if (commitments) EpisodeEngine.EnableCommitments(s);
            s.story.rulesVersion = rules;
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
            return s;
        }

        internal static string Voter(EpisodeState s, int index) => EpisodeEngine.Voters(s).Where(v => !v.isPlayer).Select(v => v.id).ElementAt(index);

        internal static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        internal static AllianceState Pact(EpisodeState s, params string[] members)
        {
            var pact = new AllianceState { id = "alliance-talk-" + s.alliances.Count, name = "The Talk Pact " + s.alliances.Count, members = members.ToList(), active = true };
            s.alliances.Add(pact);
            s.ledger.alliances.Add(new AllianceRow { id = pact.id, startedWeek = s.week, why = "player" });
            return pact;
        }

        internal static void Bond(EpisodeState s, string npc, string kind) =>
            Assert.That(Bonds.Form(s, s.playerId, npc, kind, BondStatus.Public, null), Is.Not.Null);

        internal static void Nemesis(EpisodeState s, string npc) => Bond(s, npc, BondKinds.Nemesis);

        internal static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null) => new EpisodeCommand
        {
            id = "house-talk-" + s.revision + "-" + kind, actorId = s.playerId, expectedRevision = s.revision, expectedPhase = s.phase,
            kind = kind, targetId = target,
        };

        internal static EpisodeState Accepted(EpisodeState s, EpisodeCommandKind kind, string target) => Accepted(s, Command(s, kind, target));

        internal static EpisodeState Accepted(EpisodeState s, EpisodeCommand command)
        {
            var result = new EpisodeEngine(s).Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }

        private static readonly MethodInfo SettleOf = typeof(EpisodeEngine).GetMethod("SettleVoteRead", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("EpisodeEngine.SettleVoteRead is the reveal's judge.");

        private static void Settle(EpisodeState s, string evicted) => SettleOf.Invoke(null, new object[] { s, evicted });
    }
}
