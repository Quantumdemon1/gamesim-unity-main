#if !UNITY_5_3_OR_NEWER
// Plan 32 S2a's lab arm (§M, "The balance lab"): Tools/SimulationTests only. Unity's batch runner executes [Explicit] tests,
// so the file is compiled out of the editor's assemblies.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The house talk's lab arm (plan 32 §M): story rules 11 against 12 on the same seasons, with the attentive walker - a
    /// player who, at each Social and Campaign moment, notices and listens in on the NPC world's pending conversations. Its
    /// own season loop, built from the lab's parts (<see cref="BalanceLab.Fresh"/>, <see cref="BalanceLab.Seed"/>, the
    /// synthetic NPC world, <see cref="BalancePolicies.Create"/> and <see cref="BalanceLab.Step"/>); it never edits them.
    ///
    /// <para><b>The walker</b> commits next to the world's slice and outside <see cref="BalanceLab.Step"/>, so its Notices
    /// and Listens are free actions, not decisions. Three settings (§M): none; casual, 2 Notices a week; attentive, 6
    /// Notices and 2 Listens a week, a Listen only on a conversation whose topic the house's linger would hold for (§I2).
    /// It commits only what <see cref="EpisodeEngine.OverhearRefusal"/> allows, so it is never refused. Each commit moves the
    /// revision, which the policies' coins never see: it is counted with the NPC world's operations, so every walk setting
    /// plays the policies' coins as the none setting does.</para>
    ///
    /// <para><b>The report</b> (Explicit, run by name) is Stack A's paired measurement for its enrolment (A-ON, the lead's):
    /// win and final-two rates, the read ballots' accuracy (<see cref="BallotRow.correct"/> where <c>readBefore</c> is set),
    /// hunch accuracy against the pair's true standing, free actions, decisions, and the ledger's dropped rows, each arm
    /// against rules 11 with no walker. It decides nothing itself.</para>
    /// </summary>
    public sealed class HouseTalkLab
    {
        internal enum Walk { None, Casual, Attentive }

        /// <summary>The topics a lingering Listen holds for (§I2): the rest give a Notice only.</summary>
        internal static readonly string[] ListenTopics = { "strategy", "alliance_talk", "nominations", "gossip", "tension" };

        internal static int Notices(Walk walk) => walk == Walk.Attentive ? EpisodeEngine.NoticesAWeek : walk == Walk.Casual ? 2 : 0;
        internal static int Listens(Walk walk) => walk == Walk.Attentive ? EpisodeEngine.ListensAWeek : 0;

        /// <summary>One season of one arm, and what the walker did in it.</summary>
        internal sealed class Run
        {
            public BalanceLab.SeasonRun season;
            public Walk walk;
            public int notices, listens, caught, hunchesTrue, ballotsRead, ballotsCorrect, dropped, decisions;
            public bool finished, won, finalTwo;
            public string error;
        }

        /// <summary>One season of a cell under one walker: the lab's loop, the walker beside the world's slice.</summary>
        internal static Run Play(BalanceLab.Cell cell, int index, Walk walk)
        {
            var season = new BalanceLab.SeasonRun { cell = cell, index = index, seed = BalanceLab.Seed(cell, index) };
            var run = new Run { season = season, walk = walk };
            try { PlayInto(run); }
            catch (Exception error) { run.error = "exception: " + error.GetType().Name + ": " + error.Message; }
            return run;
        }

        private static void PlayInto(Run run)
        {
            var season = run.season;
            var cell = season.cell;
            var engine = new EpisodeEngine(BalanceLab.Fresh(cell, season.seed));
            var world = cell.npcTicks > 0 ? new BalanceLabNpcWorld(engine, cell.npcTicks, season) : null;
            Func<EpisodeEngine> engineOf = world == null ? (Func<EpisodeEngine>)(() => engine) : world.Current;
            var agent = BalancePolicies.Create(cell.policy, cell.answersBoards);
            var s = engine.Snapshot;
            while (s.phase != EpisodePhase.Finished && season.commands < BalanceLab.CommandCap)
            {
                if (world != null && world.SpendSlice()) s = world.Engine.Snapshot;
                if (WalkTheHouse(engineOf(), run)) s = engineOf().Snapshot;
                var applied = BalanceLab.Step(engineOf, world == null ? (Func<bool>)null : world.SpendRest, agent, s, season, out var used, out var at);
                s = at;
                if (!applied.accepted)
                {
                    run.error = "week " + s.week + " " + s.phase + ": the walker's " + used.kind + " was refused: " + applied.reason;
                    break;
                }
                season.commands++;
                if (used.kind != EpisodeCommandKind.Advance) run.decisions++;
                s = applied.state;
            }
            run.finished = s.phase == EpisodePhase.Finished;
            if (run.error == null && !run.finished) run.error = "did not finish in " + season.commands + " commands";
            if (run.error == null && !EpisodeValidation.TryValidate(s, out string invalid)) run.error = "the final state is invalid: " + invalid;
            var autopsy = SeasonAutopsy.Of(new List<SeasonAutopsy.PhaseChange>(), s);
            run.won = autopsy != null && autopsy.outcome == SeasonAutopsy.Outcomes.Winner;
            run.finalTwo = autopsy != null && (autopsy.outcome == SeasonAutopsy.Outcomes.Winner || autopsy.outcome == SeasonAutopsy.Outcomes.RunnerUp);
            var read = s.ledger.ballots.Where(b => b.voterId == s.playerId && b.readBefore != null).ToList();
            run.ballotsRead = read.Count;
            run.ballotsCorrect = read.Count(b => b.correct);
            run.dropped = s.ledger.dropped;
        }

        /// <summary>
        /// The walker at one moment: every pending conversation, a Notice while the week's Notices are under the setting's,
        /// then a Listen on a lingering topic while its Listens are. Returns whether it committed anything.
        /// </summary>
        internal static bool WalkTheHouse(EpisodeEngine engine, Run run)
        {
            if (run.walk == Walk.None) return false;
            var s = engine.Snapshot;
            if (!EpisodeEngine.StoryAt(s, StoryRules.Manners) || s.npcSocial == null) return false;
            bool any = false;
            foreach (var conversation in s.npcSocial.pending.ToList())
            {
                foreach (bool listen in new[] { false, true })
                {
                    s = engine.Snapshot;
                    if (listen ? EpisodeEngine.ListenedThisWeek(s) >= Listens(run.walk) || !ListenTopics.Contains(conversation.topic)
                               : EpisodeEngine.NoticedThisWeek(s) >= Notices(run.walk)) continue;
                    string text = EpisodeEngine.ConversationText(conversation.sequence, listen);
                    if (EpisodeEngine.OverhearRefusal(s, text, conversation.firstId, conversation.secondId) != null) continue;
                    var result = engine.Apply(new EpisodeCommand
                    {
                        id = "overhear-" + s.revision.ToString(CultureInfo.InvariantCulture) + "-" + text, actorId = s.playerId,
                        kind = EpisodeCommandKind.WitnessNpcAct, targetId = conversation.firstId, secondTargetId = conversation.secondId,
                        text = text, expectedRevision = s.revision, expectedPhase = s.phase,
                    });
                    if (!result.accepted) { run.error = run.error ?? "the walker's " + text + " was refused: " + result.reason; continue; }
                    // Counted with the world's operations, so the policies' coins fall as they do without the walker.
                    run.season.npcOps++;
                    any = true;
                    if (!listen)
                    {
                        run.notices++;
                        if (HunchHoldsTrue(s, conversation)) run.hunchesTrue++;
                    }
                    else
                    {
                        run.listens++;
                        if (result.state.ledger.standings.Any(r => r.week == s.week && r.source == ClaimSource.Missed
                                && r.fromId == conversation.firstId && r.toId == conversation.secondId)) run.caught++;
                    }
                }
            }
            return any;
        }

        /// <summary>
        /// Whether a Notice's tone agrees with how the two truly stand (the report's hunch accuracy): warm with a mutual view
        /// of 0 or more, tense below 0, scheming with a pact of the two or a mutual view of 25 or more.
        /// </summary>
        internal static bool HunchHoldsTrue(EpisodeState s, NpcConversationState conversation)
        {
            double mutual = (s.Score(conversation.firstId, conversation.secondId) + s.Score(conversation.secondId, conversation.firstId)) / 2;
            switch (HunchTone.OfTopic(conversation.topic))
            {
                case HunchTone.Warm: return mutual >= 0;
                case HunchTone.Tense: return mutual < 0;
                default: return s.Allied(conversation.firstId, conversation.secondId) || mutual >= 25;
            }
        }

        // ------------------------------------------------------------ the walker plays (subset)

        /// <summary>
        /// The walker overhears at 12 and commits nothing at 11, within the week's caps, and the season still plays to its end
        /// valid: the arm the report pairs is a real one.
        /// </summary>
        [Test]
        public void TheAttentiveWalkerOverhearsAtTwelveWithinTheCapsAndNothingAtEleven()
        {
            int notices = 0;
            for (int index = 0; index < 3; index++)
            {
                // Since A-ON the unnamed cell plays 12, every new season's rules, and 11 is the named cell (a cell names its
                // level only when it is not Current, BalanceLab.CheckedStoryRules).
                var shipped = new BalanceLab.Cell { policy = BalancePolicies.Passive, size = 8, npcTicks = 300 };
                Assert.That(BalanceLab.Fresh(shipped, 1u).story.rulesVersion, Is.EqualTo(StoryRules.Manners), "The unnamed cell plays 12.");
                var eleven = Play(shipped.AtStoryRules(StoryRules.Roles), index, Walk.Attentive);
                Assert.That(eleven.error, Is.Null);
                Assert.That(eleven.notices + eleven.listens, Is.Zero, "Rules 11 has no overhearing.");
                var twelve = Play(shipped, index, Walk.Attentive);
                Assert.That(twelve.error, Is.Null);
                Assert.That(twelve.finished, Is.True);
                notices += twelve.notices;
                Assert.That(twelve.caught, Is.LessThanOrEqualTo(twelve.listens));
            }
            Assert.That(notices, Is.GreaterThan(0), "The attentive walker noticed somebody in three seasons at 12.");
        }

        // ------------------------------------------------------------ Stack A's report (Explicit)

        private static int Seeds => int.TryParse(Environment.GetEnvironmentVariable("HOUSE_TALK_SEEDS"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 0 ? n : 50;

        [Test, Explicit("Plan 32 §M: Stack A's paired report, story rules 11 against 12 with the walker none, casual and attentive, on the same seasons (HOUSE_TALK_SEEDS, 50). Run by name.")]
        public void StackAReport()
        {
            var policies = new[] { BalancePolicies.Passive, BalancePolicies.Social, BalancePolicies.Reader };
            var md = new StringBuilder();
            md.AppendLine("# Stack A: story rules 11 against 12 (plan 32 section M)").AppendLine();
            md.AppendLine("Seeds a cell: " + Seeds + ". Each row is paired with rules 11 and no walker on the same seasons.").AppendLine();
            md.AppendLine("| cell | arm | errors | wins | final two | read ballots correct | notices/season | listens/season | caught | hunch true | decisions/season | dropped |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (int ticks in new[] { 0, 900 })
                foreach (int size in new[] { 8, 12 })
                    foreach (string policy in policies)
                    {
                        // Since A-ON the unnamed cell plays 12 and 11 is the named cell, so the arms pair the seasons they paired
                        // before the enrolment.
                        var cell = new BalanceLab.Cell { policy = policy, size = size, npcTicks = ticks };
                        var arms = new (string name, BalanceLab.Cell cell, Walk walk)[]
                        {
                            ("11 none", cell.AtStoryRules(StoryRules.Roles), Walk.None), ("12 none", cell, Walk.None),
                            ("12 casual", cell, Walk.Casual), ("12 attentive", cell, Walk.Attentive),
                        };
                        List<Run> control = null;
                        foreach (var arm in arms)
                        {
                            var runs = Enumerable.Range(0, Seeds).Select(i => Play(arm.cell, i, arm.walk)).ToList();
                            control = control ?? runs;
                            md.AppendLine(Row(cell.Key, arm.name, runs, control));
                        }
                    }
            string folder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "balance");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "house-talk-stack-a.md"), md.ToString(), new UTF8Encoding(false));
            TestContext.WriteLine(md.ToString());
        }

        private static string Row(string cell, string arm, List<Run> runs, List<Run> control)
        {
            int n = runs.Count;
            var mc = BalanceLab.McNemar(control.Select(r => r.won).ToList(), runs.Select(r => r.won).ToList());
            int read = runs.Sum(r => r.ballotsRead), correct = runs.Sum(r => r.ballotsCorrect);
            int notices = runs.Sum(r => r.notices);
            return "| " + cell + " | " + arm + " | " + runs.Count(r => r.error != null)
                + " | " + BalanceLab.Pct(runs.Count(r => r.won) / (double)n) + " (McNemar b " + mc.b + " c " + mc.c + " p " + BalanceLab.Num(mc.p, "0.000") + ")"
                + " | " + BalanceLab.Pct(runs.Count(r => r.finalTwo) / (double)n)
                + " | " + (read == 0 ? "n/a" : BalanceLab.Pct(correct / (double)read) + " of " + read)
                + " | " + BalanceLab.Num(notices / (double)n) + " | " + BalanceLab.Num(runs.Sum(r => r.listens) / (double)n)
                + " | " + runs.Sum(r => r.caught) + " | " + (notices == 0 ? "n/a" : BalanceLab.Pct(runs.Sum(r => r.hunchesTrue) / (double)notices))
                + " | " + BalanceLab.Num(runs.Average(r => r.decisions)) + " | " + runs.Sum(r => r.dropped) + " |";
        }
    }
}
#endif
