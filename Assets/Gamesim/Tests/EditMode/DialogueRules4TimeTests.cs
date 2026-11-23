using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Dialogue rules 4's conversation time (DIALOGUE-PLAN D5c, §4 and §5.4): a campaign conversation ends with +1 when the
    /// voter warmed to a reply, -1 when a reply the screen marks as a risk backfired, otherwise 0; the window's budget reads
    /// what applied. Played on genuine seasons enrolled by the literal 4, plus the budget arithmetic over a week on a real
    /// state with receipts placed by hand. Shared cases: the dotnet subset runs them.
    /// </summary>
    public sealed class DialogueRules4TimeTests
    {
        private const string Trees = DialogueRules3Season.Trees;

        private static EpisodeState Accepted(EpisodeEngine engine, EpisodeCommand command) => DialogueRules2Season.Accepted(engine, command);

        private static int Remaining(EpisodeState s) => DialogueRules2Season.Remaining(s);

        private static DialogueEpisodeState Last(EpisodeState s) => s.dialogue.episodes.Last();

        private static string LastOutcome(EpisodeState s) => s.events.Last(e => e.kind == StoryLog.Outcome).text;

        // ---------------------------------------------------------------- the season

        /// <summary>One way a conversation went from the stop: its voter, approach and reply, and what the voter did.</summary>
        private sealed class Talk
        {
            internal string Voter, Approach, Reply, Reaction, Risk;
            internal bool Risky => Risk == HouseEventRisk.Medium || Risk == HouseEventRisk.High;
            public override string ToString() => Voter + " " + Approach + " " + Reply + " (" + Risk + "): " + Reaction;
        }

        private sealed class Stop
        {
            internal uint Seed;
            internal EpisodeState State;
            internal string[] Voters;
            internal List<Talk> Talks;
        }

        private static Stop stop;

        /// <summary>Opens a conversation with the voter and takes the approach; null when the approach is not offered.</summary>
        private static EpisodeState Approach(EpisodeState from, string voter, string approach)
        {
            var engine = new EpisodeEngine(from.Clone());
            var s = Accepted(engine, DialogueRules3Season.Entry(from, voter, Trees));
            if (!DialogueRules3Season.Options(s).Contains(approach)) return null;
            return Accepted(engine, DialogueRules2Season.Choice(s, approach));
        }

        private static EpisodeState Choose(EpisodeState from, string option) =>
            Accepted(new EpisodeEngine(from.Clone()), DialogueRules2Season.Choice(from, option));

        /// <summary>The conversation played to its end: the approach, the reply, then "keep it open" at the finish.</summary>
        private static EpisodeState Play(EpisodeState from, Talk talk) =>
            Choose(Choose(Approach(from, talk.Voter, talk.Approach), talk.Reply), DialogueCampaignTrees.KeepOpen);

        /// <summary>Every approach and reply of every voter startable at the stop, each played once to its reaction.</summary>
        private static List<Talk> Explore(EpisodeState s, IEnumerable<string> voters)
        {
            var talks = new List<Talk>();
            string[] approaches = { DialogueCampaignTrees.AskStand, DialogueCampaignTrees.KeepEasy, DialogueCampaignTrees.BringUp, DialogueCampaignTrees.Lean };
            foreach (string voter in voters)
                foreach (string approach in approaches)
                {
                    var reply = Approach(s, voter, approach);
                    if (reply == null) continue;
                    var item = reply.houseEvents.Single(e => e.id == DialogueRules.Cycle(reply, DialogueRules.Active(reply)).eventId);
                    foreach (var choice in item.choices.Where(c => c.optionId != DialogueCampaignTrees.Leave))
                    {
                        var after = Choose(reply, choice.optionId);
                        string beat = DialogueRules3Season.Beat(after);
                        talks.Add(new Talk { Voter = voter, Approach = approach, Reply = choice.optionId, Risk = choice.risk,
                            Reaction = beat.Substring(DialogueCampaignTrees.FinishPrefix.Length) });
                    }
                }
            return talks;
        }

        /// <summary>
        /// The first rules-4 campaign in seeds 1 to 60 (weeks 1 to 5) with the player nominated, the asks answered, the
        /// nominee's grant still open, and among the voters' conversations two different voters who warm and a risky reply
        /// that backfires. A detached copy.
        /// </summary>
        private static Stop Campaign()
        {
            if (stop != null) return stop;
            for (uint seed = 1; seed <= 60; seed++)
            {
                var engine = new EpisodeEngine(DialogueRules4Season.Start(seed));
                for (int step = 0; step < 600; step++)
                {
                    var s = engine.Snapshot;
                    if (s.week > 5 || s.phase == EpisodePhase.Finished || s.Find(s.playerId).status != ContestantStatus.Active) break;
                    if (s.phase == EpisodePhase.Campaign && s.nominees.Contains(s.playerId))
                    {
                        DialogueRules2Season.CloseAsks(engine);
                        s = engine.Snapshot;
                        var voters = EpisodeEngine.Voters(s).Where(c => !c.isPlayer).Select(c => c.id)
                            .Where(id => DialogueRules.CanStart(s, id, out _)).ToArray();
                        if (voters.Length >= 2 && DialogueRules.GrantPaysNow(s) && Remaining(s) >= 2)
                        {
                            var talks = Explore(s, voters);
                            if (talks.Where(t => t.Reaction == DialogueCampaignTrees.Warmed).Select(t => t.Voter).Distinct().Count() >= 2
                                && talks.Any(t => t.Reaction == DialogueCampaignTrees.Backfired && t.Risky))
                            {
                                TestContext.Out.WriteLine("Rules-4 campaign: seed " + seed + ", week " + s.week + ", revision " + s.revision + ".");
                                return stop = new Stop { Seed = seed, State = s.Clone(), Voters = voters, Talks = talks };
                            }
                        }
                        break;
                    }
                    var next = EpisodeEngineTests.NextCommand(engine.Snapshot);
                    if (next.kind == EpisodeCommandKind.Compete) next.performance = 0;
                    Accepted(engine, next);
                }
            }
            Assert.Fail("No rules-4 season of 60 met a campaign with two voters who warm and a risky reply that backfires.");
            return null;
        }

        private static bool IsWarm(Talk t) => t.Reaction == DialogueCampaignTrees.Warmed;

        private static bool IsSoured(Talk t) => t.Reaction == DialogueCampaignTrees.Backfired && t.Risky;

        private static Talk Warm(Stop at, string notVoter = null) => at.Talks.First(t => IsWarm(t) && t.Voter != notVoter);

        private static Talk Soured(Stop at) => at.Talks.First(IsSoured);

        /// <summary>
        /// A talk found again from a later state of the same week: the reaction is the reply's keyed coin, but which reply
        /// is offered follows the answer, and the answer how the voter sees the player now, so it is looked up afresh.
        /// </summary>
        private static Talk Find(EpisodeState s, IEnumerable<string> voters, Func<Talk, bool> pick)
        {
            var talk = Explore(s, voters).FirstOrDefault(pick);
            Assert.That(talk, Is.Not.Null, "The talk is still there to have from revision " + s.revision + ".");
            return talk;
        }

        /// <summary>A conversation opened with this voter and left at once: no reaction, no time.</summary>
        private static EpisodeState LeaveAtOnce(EpisodeState from, string voter) =>
            Choose(Accepted(new EpisodeEngine(from.Clone()), DialogueRules3Season.Entry(from, voter, Trees)), DialogueCampaignTrees.Leave);

        /// <summary>The campaign after a first conversation that warmed: a +1 applied mid-window, the window still open.</summary>
        internal static EpisodeState AfterAWarmLanding()
        {
            var at = Campaign();
            return Play(at.State, Warm(at));
        }

        // ---------------------------------------------------------------- the receipts in play

        [Test]
        public void EveryWayAConversationEndsProposesTheTimeItsPathEarns()
        {
            var at = Campaign();
            foreach (var talk in at.Talks)
            {
                var end = Play(at.State, talk);
                var time = Last(end).time;
                int expected = talk.Reaction == DialogueCampaignTrees.Warmed ? 1
                    : talk.Reaction == DialogueCampaignTrees.Backfired && talk.Risky ? -1 : 0;
                Assert.That(time.proposedDelta, Is.EqualTo(expected), talk.ToString());
                Assert.That(time.appliedDelta, Is.EqualTo(expected), "The grant paid, so two conversations remained: " + talk);
                Assert.That(time.window, Is.EqualTo(Windows.AfterVeto));
                Assert.That(time.spentRevision + time.spentSequence + time.spentWindowAction, Is.Zero);
            }
        }

        [Test]
        public void AWarmLandingGivesOneMoreConversationInThisWindowAndSaysSo()
        {
            var at = Campaign();
            var end = Play(at.State, Warm(at));
            Assert.That(Last(end).entry.kind, Is.EqualTo(DialogueRules.GrantEntry), "The grant paid for the first conversation.");
            Assert.That(EpisodeEngine.SocialActionBudget(end), Is.EqualTo(EpisodeEngine.SocialActionBudget(at.State) + 1));
            Assert.That(Remaining(end), Is.EqualTo(Remaining(at.State) + 1));
            Assert.That(LastOutcome(end), Does.EndWith(" " + DialogueRules.EarnedTimeLine));
        }

        [Test]
        public void ARiskyReplyThatBackfiresCostsAnOpportunityAndSaysSo()
        {
            var at = Campaign();
            Assert.That(Remaining(at.State), Is.GreaterThanOrEqualTo(2), "Two remain after a conversation the grant pays for.");
            var end = Play(at.State, Soured(at));
            Assert.That(Last(end).time.appliedDelta, Is.EqualTo(-1));
            Assert.That(Remaining(end), Is.EqualTo(Remaining(at.State) - 1));
            Assert.That(LastOutcome(end), Does.EndWith(" " + DialogueRules.LostTimeLine));
        }

        [Test]
        public void ARiskyBackfireCostsNothingWhenFewerThanTwoWouldRemain()
        {
            var at = Campaign();
            string souredVoter = Soured(at).Voter;
            string other = at.Voters.First(v => v != souredVoter);
            var s = LeaveAtOnce(at.State, other);
            Assert.That(Last(s).entry.kind, Is.EqualTo(DialogueRules.GrantEntry), "The grant pays for the first, which ends at once.");
            // Spend down to exactly two before the soured talk, so its own cost leaves one.
            Assert.That(Remaining(s), Is.GreaterThanOrEqualTo(2));
            int extra = Remaining(s) - 2;
            var spare = at.Voters.Where(v => v != souredVoter && v != other).Take(extra).ToArray();
            Assert.That(spare.Length, Is.EqualTo(extra), "Enough voters to spend down to two.");
            foreach (string voter in spare) s = LeaveAtOnce(s, voter);
            Assert.That(Remaining(s), Is.EqualTo(2));
            var end = Play(s, Find(s, new[] { souredVoter }, IsSoured));
            Assert.That(Last(end).time.proposedDelta, Is.EqualTo(-1));
            Assert.That(Last(end).time.appliedDelta, Is.Zero, "One remained after the cost: the -1 is refused and recorded as proposed only.");
            Assert.That(Remaining(end), Is.EqualTo(1));
            Assert.That(LastOutcome(end), Does.Not.Contain(DialogueRules.LostTimeLine));
        }

        [Test]
        public void ASecondWarmLandingInTheWeekIsRecordedButGivesNothing()
        {
            var at = Campaign();
            var first = Warm(at);
            var s = Play(at.State, first);
            int before = Remaining(s);
            var end = Play(s, Find(s, at.Voters.Where(v => v != first.Voter), IsWarm));
            Assert.That(Last(end).entry.kind, Is.EqualTo(DialogueRules.ActionEntry));
            Assert.That(Last(end).time.proposedDelta, Is.EqualTo(1));
            Assert.That(Last(end).time.appliedDelta, Is.Zero, "One +1 a week.");
            Assert.That(Remaining(end), Is.EqualTo(before - 1), "The second conversation cost its action and won nothing back.");
        }

        [Test]
        public void TheActionsLeftCountAgreesWithItsOwnBookkeepingAtEveryStepOfTheWindow()
        {
            var at = Campaign();
            var warm = Warm(at);
            var path = new List<EpisodeState> { at.State };
            var s = Approach(at.State, warm.Voter, warm.Approach); path.Add(s);
            s = Choose(s, warm.Reply); path.Add(s);
            s = Choose(s, DialogueCampaignTrees.KeepOpen); path.Add(s);
            var second = Find(s, at.Voters.Where(v => v != warm.Voter), IsWarm);
            s = Approach(s, second.Voter, second.Approach); path.Add(s);
            s = Choose(s, second.Reply); path.Add(s);
            s = Choose(s, DialogueCampaignTrees.KeepOpen); path.Add(s);
            int left = Remaining(at.State);
            foreach (var state in path)
            {
                var paid = state.dialogue.episodes.Where(e => e.week == state.week && e.window == Windows.AfterVeto).ToArray();
                int expected = left - paid.Count(e => e.entry.kind == DialogueRules.ActionEntry)
                    + paid.Where(e => e.time != null).Sum(e => e.time.appliedDelta);
                Assert.That(Remaining(state), Is.EqualTo(expected), "Revision " + state.revision + ".");
            }
            Assert.That(Remaining(s), Is.EqualTo(left - 1 + 1), "Grant, +1, then one action spent and a capped +1.");
        }

        [Test]
        public void ASeasonWithAReceiptMidWindowRoundTripsWithTheSameActionsLeft()
        {
            var at = Campaign();
            var end = Play(at.State, Warm(at));
            var loaded = DialogueRules2Season.RoundTrip(end);
            Assert.That(EpisodeValidation.TryValidate(loaded, out string error), Is.True, error);
            Assert.That(DialogueRules2Season.Json(loaded), Is.EqualTo(DialogueRules2Season.Json(end)));
            Assert.That(Remaining(loaded), Is.EqualTo(Remaining(end)));
            Assert.That(EpisodeEngine.SocialActionBudget(loaded), Is.EqualTo(EpisodeEngine.SocialActionBudget(end)));
        }

        // ---------------------------------------------------------------- the validator

        [Test]
        public void AReceiptItsPathDoesNotEarnIsRefused()
        {
            var at = Campaign();
            var s = LeaveAtOnce(at.State, at.Voters[0]).Clone();
            Assert.That(Last(s).time.proposedDelta, Is.Zero);
            Last(s).time.proposedDelta = 1; Last(s).time.appliedDelta = 1;
            Assert.That(EpisodeValidation.TryValidate(s, out _), Is.False);
        }

        [Test]
        public void TwoAppliedPlusOnesInOneWeekAreRefused()
        {
            var at = Campaign();
            var first = Warm(at);
            var s = Play(at.State, first);
            var end = Play(s, Find(s, at.Voters.Where(v => v != first.Voter), IsWarm)).Clone();
            Assert.That(EpisodeValidation.TryValidate(end, out string error), Is.True, error);
            Last(end).time.appliedDelta = 1;
            Assert.That(EpisodeValidation.TryValidate(end, out _), Is.False);
        }

        // ---------------------------------------------------------------- the budget arithmetic

        private static EpisodeState campaignState;

        /// <summary>A rules-4 campaign (window after the veto, two seats) whose week has no extras of its own: bought time sets them.</summary>
        internal static EpisodeState QuietCampaign()
        {
            if (campaignState != null) return campaignState.Clone();
            for (uint seed = 1; seed <= 20; seed++)
            {
                var engine = new EpisodeEngine(DialogueRules4Season.Start(seed));
                for (int step = 0; step < 400; step++)
                {
                    var s = engine.Snapshot;
                    if (s.week > 4 || s.phase == EpisodePhase.Finished) break;
                    if (s.phase == EpisodePhase.Campaign && EpisodeEngine.WeeklyExtras(s) == 0 && s.boughtActionPoints == 0 && s.moveInExtrasSpent == 0)
                    { campaignState = s.Clone(); return s.Clone(); }
                    Accepted(engine, EpisodeEngineTests.NextCommand(s));
                }
            }
            Assert.Fail("No rules-4 campaign without extras in seeds 1 to 20.");
            return null;
        }

        internal static void Receipt(EpisodeState s, int week, int window, int applied) => s.dialogue.episodes.Add(new DialogueEpisodeState
        {
            id = "time-" + s.dialogue.episodes.Count, week = week, window = window,
            time = new DialogueTimeReceiptState { window = window, proposedDelta = applied, appliedDelta = applied },
        });

        // nominations window receipt, its spending, the open window's receipt, a +1 of last week's same window, extras
        [TestCase(0, 0, 0, false, 1, 3)]
        [TestCase(0, 0, 1, false, 1, 4)]
        [TestCase(0, 0, -1, false, 1, 2)]
        [TestCase(0, 0, -1, false, 0, 1)]
        [TestCase(0, 2, 0, false, 1, 2)]
        [TestCase(1, 2, 0, false, 1, 3)]
        [TestCase(1, 3, 1, false, 1, 3)]
        [TestCase(-1, 1, 0, false, 1, 2)]
        [TestCase(-1, 2, -1, false, 2, 1)]
        [TestCase(0, 0, 0, true, 1, 3)]
        public void TheBudgetReadsTheWeeksReceipts(int nominations, int spentThere, int open, bool lastWeek, int extras, int budget)
        {
            var s = QuietCampaign();
            Assert.That(EpisodeEngine.Window(s), Is.EqualTo(Windows.AfterVeto));
            Assert.That(EpisodeEngine.WindowSeats(s, Windows.AfterVeto), Is.EqualTo(2));
            Assert.That(EpisodeEngine.WindowSeats(s, Windows.AfterNominations), Is.EqualTo(1));
            s.boughtActionPoints = extras;
            for (int w = 0; w < s.windowActions.Count; w++) s.windowActions[w] = 0;
            s.windowActions[Windows.AfterNominations] = spentThere;
            if (nominations != 0) Receipt(s, s.week, Windows.AfterNominations, nominations);
            if (open != 0) Receipt(s, s.week, Windows.AfterVeto, open);
            if (lastWeek) Receipt(s, s.week - 1, Windows.AfterVeto, 1);
            Assert.That(EpisodeEngine.SocialActionBudget(s), Is.EqualTo(budget));

            var three = s.Clone();
            three.dialogueRulesVersion = DialogueRules.Rules3;
            three.boughtActionPoints = extras;
            int plain = 2 + Math.Max(0, extras - Math.Max(0, spentThere - 1));
            Assert.That(EpisodeEngine.SocialActionBudget(three), Is.EqualTo(plain), "Below rules 4 no receipt is read.");
        }
    }
}
