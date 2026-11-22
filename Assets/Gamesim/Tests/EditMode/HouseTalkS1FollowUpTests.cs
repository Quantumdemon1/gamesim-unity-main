using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;
using R2 = Gamesim.Tests.EditMode.DialogueRules2Season;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The house talk's slice S1, the cases its reviews left to a later slice (plan 32 §N), written in S2a: the validator's
    /// remaining single checks (a week that is not a number, week 0, the part counts of <c>tell:</c> and <c>h2h:</c>),
    /// Regard's broken-word term, the head-to-head record through the engine's own Advance, and the vote answer at 12 under
    /// dialogue rules 2. Pure simulation, so the Unity-free subset runs it.
    /// </summary>
    public sealed class HouseTalkS1FollowUpTests
    {
        // ------------------------------------------------------------ the validator's single checks

        // The key's placeholders: {w} this week, {npc} a seated voter.
        [TestCase("said:x:{npc}:honest:definite")]
        [TestCase("said:{w}:{npc}:honest:definite:again")]
        [TestCase("h2h:x:hoh:{npc}")]
        [TestCase("h2h:0:hoh:{npc}")]
        [TestCase("h2h:{w}:hoh")]
        [TestCase("h2h:{w}:hoh:{npc}:again")]
        [TestCase("tell:{npc}")]
        [TestCase("tell:{npc}:hedged:again")]
        public void AtTwelveEachOfTheRecordsSingleChecksRefuses(string template)
        {
            var s = VoteAnswerTests.Campaign(StoryRules.Manners);
            string key = template.Replace("{w}", s.week.ToString()).Replace("{npc}", VoteAnswerTests.Voter(s, 0));
            s.story.cooldowns.Add(new StoryCooldownState { key = key, untilWeek = s.week + 1 });
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.False, key);
            Assert.That(error, Is.EqualTo("Invalid house talk records."));
        }

        // ------------------------------------------------------------ Regard: heard of a broken word

        /// <summary>The player's broken word, as the story knows it: a whispered fact the houseguests in <paramref name="knowers"/> have heard.</summary>
        private static void Breach(EpisodeState s, int n, params string[] knowers) =>
            s.story.facts.Add(new HouseFactState
            {
                id = "fact-broken-" + n, kind = FactKinds.BrokenWord, actorId = s.playerId, subjectId = s.hohId, refId = "deal-broken-" + n,
                visibility = FactVisibility.Whispered, week = s.week, knowers = knowers.ToList(),
            });

        [Test]
        public void HeardOfABrokenWordCostsEightAHearingToTwentyFour()
        {
            var s = VoteAnswerTests.Campaign(StoryRules.Manners, 7, true);
            string npc = s.Active.Where(c => !c.isPlayer && c.id != s.hohId && c.id != s.vetoHolderId && !s.nominees.Contains(c.id)).Select(c => c.id).First();
            string other = s.Active.First(c => !c.isPlayer && c.id != npc && c.id != s.hohId).id;
            Assert.That(YourWord.On(s), Is.True, "Precondition: the house keeps the player's word.");
            double Term(EpisodeState x) => Regard.Of(x, npc).Term(Regard.Terms.Broken);
            Assert.That(Term(s), Is.Zero);
            Breach(s, 1, other);
            Assert.That(Term(s), Is.Zero, "A breach they never heard of costs nothing with them.");
            Breach(s, 2, npc, other);
            Assert.That(Term(s), Is.EqualTo(Regard.BrokenPerHearing), "One hearing: -8.");
            Breach(s, 3, npc);
            Assert.That(Term(s), Is.EqualTo(2 * Regard.BrokenPerHearing), "Two: -16.");
            Breach(s, 4, npc);
            Breach(s, 5, npc);
            Assert.That(Term(s), Is.EqualTo(Regard.BrokenCap), "Four hearings stop at -24.");
            var eleven = s.Clone();
            eleven.story.rulesVersion = StoryRules.Roles;
            Assert.That(Regard.Of(eleven, npc).Term(Regard.Terms.Broken), Is.Zero, "Below 12 the term reads nothing.");
        }

        // ------------------------------------------------------------ the head-to-head record through the engine

        /// <summary>A season the shipped rules start, held at story rules <paramref name="rules"/>, walked to its first Head of Household competition with the player in it.</summary>
        private static EpisodeEngine AtTheHoH(int rules, uint seed)
        {
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
            ShippedRules.ApplyFresh(fresh);
            fresh.story.rulesVersion = rules;
            Assert.That(EpisodeValidation.TryValidate(fresh, out var error), Is.True, error);
            var engine = new EpisodeEngine(fresh);
            for (int i = 0; i < 400; i++)
            {
                var s = engine.Snapshot;
                if (s.phase == EpisodePhase.HoH && !s.competitionResolved && EpisodeEngine.CompetitionPlayers(s).Any(p => p.isPlayer)) return engine;
                var result = engine.Apply(EpisodeEngineTests.NextCommand(s));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            Assert.Fail("No Head of Household competition with the player in it.");
            return null;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = "h2h-" + s.revision + "-" + kind, actorId = s.playerId, kind = kind, expectedRevision = s.revision, expectedPhase = s.phase,
        };

        /// <summary>
        /// The one hook line (EpisodeEngine.cs, the Head of Household crowned): a player who wins the HoH at 12 has the runner-up
        /// on the record as the Advance crosses into the nominations, and the same win at 11 records nothing.
        /// </summary>
        [Test]
        public void APlayersHoHWinAtTwelveRecordsTheRunnerUpThroughTheEnginesOwnAdvance()
        {
            for (uint seed = 1; seed <= 60; seed++)
            {
                var engine = AtTheHoH(StoryRules.Manners, seed);
                var before = engine.Snapshot;
                var compete = Command(before, EpisodeCommandKind.Compete);
                compete.performance = 1;
                var competed = engine.Apply(compete);
                Assert.That(competed.accepted, Is.True, competed.reason);
                var won = competed.state;
                if (won.hohId != won.playerId || won.phase != EpisodePhase.HoH) continue;
                string runnerUp = won.competitionScores.OrderByDescending(x => x.score).Select(x => x.contestantId).ElementAt(1);
                var crowned = engine.Apply(Command(won, EpisodeCommandKind.Advance));
                Assert.That(crowned.accepted, Is.True, crowned.reason);
                Assert.That(crowned.state.phase, Is.EqualTo(EpisodePhase.Nomination));
                string key = Regard.HeadToHeadPrefix + won.week + ":hoh:" + runnerUp;
                Assert.That(crowned.state.story.cooldowns.Select(c => c.key), Does.Contain(key), "seed " + seed + ": the record the Advance wrote.");

                var at11 = AtTheHoH(StoryRules.Roles, seed);
                var compete11 = Command(at11.Snapshot, EpisodeCommandKind.Compete);
                compete11.performance = 1;
                var won11 = at11.Apply(compete11).state;
                var crowned11 = at11.Apply(Command(won11, EpisodeCommandKind.Advance));
                Assert.That(crowned11.accepted, Is.True, crowned11.reason);
                Assert.That(crowned11.state.story.cooldowns.Any(c => c.key.StartsWith(Regard.HeadToHeadPrefix, StringComparison.Ordinal)), Is.False,
                    "At 11 nothing is recorded.");
                return;
            }
            Assert.Fail("The player won no Head of Household in sixty seasons.");
        }

        // ------------------------------------------------------------ the vote answer at 12 under dialogue rules 2

        /// <summary>
        /// Plan 32 §N's dialogue-rules-2 case: in the witness campaign (no information deal), a rules-2 conversation started
        /// after an ask at 12 copies the target the voter told the player into its statement, and the voter's stance stays the
        /// ask's one: the conversation adds no answer record of its own.
        /// </summary>
        [Test]
        public void AtTwelveARulesTwoConversationAfterAnAskCopiesTheToldTargetAndAddsNoStance()
        {
            Assert.That(R2.AtItsSchema, Is.True, "Dialogue rules 2 is defined on this base (schema 30 on).");
            var found = R2.FindCampaign();
            int copied = 0;
            foreach (string voter in found.Voters)
            {
                var s = found.State.Clone();
                s.story.rulesVersion = StoryRules.Manners;
                Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
                var engine = new EpisodeEngine(s);
                var asked = R2.Accepted(engine, R2.AskVote(s, voter));
                var said = EpisodeEngine.Said(asked, voter, asked.week);
                Assert.That(said, Is.Not.Null, "At 12 every answer is on the record.");
                var claim = asked.ledger.claims.LastOrDefault(k => k.week == asked.week && k.voterId == voter && k.source == ClaimSource.Told);
                if (claim == null) { Assert.That(said.stance, Is.EqualTo(Candour.Neutral), "Only a non-answer tells nothing."); continue; }
                var entered = R2.Accepted(engine, R2.Entry(asked, voter));
                var episode = R2.Conversation(entered, voter);
                Assert.That(R2.Suffix(episode.responses[0]), Is.EqualTo(claim.targetId == entered.playerId ? DialogueCampaignWord.Against : DialogueCampaignWord.Word),
                    "The answer is their word, not a coin of the conversation's.");
                Assert.That(episode.statement, Is.Not.Null);
                Assert.That(episode.statement.targetId, Is.EqualTo(claim.targetId), "The told target, honest or not, copied.");
                var records = entered.story.cooldowns.Where(c => c.key.StartsWith(EpisodeEngine.SaidPrefix + entered.week + ":" + voter + ":", StringComparison.Ordinal)).ToList();
                Assert.That(records.Count, Is.EqualTo(1), "One stance per voter per week.");
                var still = EpisodeEngine.Said(entered, voter, entered.week);
                Assert.That((still.stance, still.manner), Is.EqualTo((said.stance, said.manner)));
                copied++;
            }
            Assert.That(copied, Is.GreaterThan(0), "Some voter of the witness campaign told the player where their vote is.");
        }
    }
}
