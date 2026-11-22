using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Genuine seasons on dialogue rules 3 (DIALOGUE-PLAN D3) for the tests that play one: the shipped season enrolled by
    /// the literal 3, walked by the plain public walk to a campaign, or to a veto meeting, where the player is nominated,
    /// the house's open asks answered with their own neutral exits. Past the enrolment nothing is assigned by hand except
    /// where a test says so. Nothing here reads <see cref="DialogueRules.Offered"/>: the season is enrolled by the literal 3,
    /// which since D3 is also the chip's season for the same seed, field for field (ShippedRulesTests holds that).
    /// </summary>
    internal static class DialogueRules3Season
    {
        internal const string Trees = DialogueCampaignTrees.TemplateId, Holder = DialogueHolderAppeal.TemplateId;

        /// <summary>The shipped house of a seed, before its first command, enrolled on dialogue rules 3 by its literal.</summary>
        internal static EpisodeState Start(uint seed, int houseSize = 8)
        {
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = houseSize }, seed);
            ShippedRules.ApplyFresh(fresh);
            Assert.That(fresh.dialogueRulesVersion, Is.Zero, "A shipped season starts on dialogue rules 0.");
            fresh.dialogueRulesVersion = DialogueRules.Rules3;
            return fresh;
        }

        internal sealed class Stop
        {
            internal uint Seed;
            internal EpisodeState State;
            internal string[] People;
        }

        /// <summary>
        /// The first campaign in seeds 1 to 40 (weeks <paramref name="fromWeek"/> to 3) where the player is nominated, the
        /// asks are answered, and at least two voters can be talked to with two actions left. A detached copy.
        /// </summary>
        internal static Stop FindCampaign(int fromWeek = 1)
        {
            for (uint seed = 1; seed <= 40; seed++)
            {
                var engine = new EpisodeEngine(Start(seed));
                for (int step = 0; step < 400; step++)
                {
                    var s = engine.Snapshot;
                    if (s.week > 3 || s.phase == EpisodePhase.Finished || s.Find(s.playerId).status != ContestantStatus.Active) break;
                    if (s.phase == EpisodePhase.Campaign && s.week >= fromWeek && s.nominees.Contains(s.playerId))
                    {
                        DialogueRules2Season.CloseAsks(engine);
                        s = engine.Snapshot;
                        var voters = EpisodeEngine.Voters(s).Where(c => !c.isPlayer).Select(c => c.id)
                            .Where(id => DialogueRules.CanStart(s, id, out _)).ToArray();
                        if (voters.Length >= 2 && DialogueRules2Season.Remaining(s) >= 2)
                        {
                            TestContext.Out.WriteLine("Rules-3 campaign: seed " + seed + ", week " + s.week + ", revision " + s.revision + ".");
                            return new Stop { Seed = seed, State = s.Clone(), People = voters };
                        }
                    }
                    DialogueRules2Season.Accepted(engine, EpisodeEngineTests.NextCommand(engine.Snapshot));
                }
            }
            Assert.Fail("No rules-3 season of 40 met a nominated campaign with two voters to talk to.");
            return null;
        }

        /// <summary>
        /// The first veto meeting in seeds 1 to 60 (weeks 1 to 5) where the player is nominated, does not hold the veto, the
        /// decision is still open, the asks are answered and the appeal to the holder can start. A detached copy; its one
        /// person is the holder. The walk is the plain public walk except that the player plays every competition at zero:
        /// at the walk's usual 0.5 the player won the veto in 39 of the 40 nominated meetings it met (d3c1).
        /// </summary>
        internal static Stop FindVetoMeeting()
        {
            // Why each nominated veto meeting the walk met was passed over, for the failure's message.
            var passed = new Dictionary<string, int>(StringComparer.Ordinal);
            void Pass(string why) => passed[why] = passed.TryGetValue(why, out int n) ? n + 1 : 1;
            for (uint seed = 1; seed <= 60; seed++)
            {
                var engine = new EpisodeEngine(Start(seed));
                for (int step = 0; step < 600; step++)
                {
                    var s = engine.Snapshot;
                    if (s.week > 5 || s.phase == EpisodePhase.Finished || s.Find(s.playerId).status != ContestantStatus.Active) break;
                    if (s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved && s.nominees.Contains(s.playerId))
                    {
                        if (s.vetoHolderId == null || s.vetoHolderId == s.playerId) Pass("the player holds the veto or nobody does");
                        else if (s.nominees.Contains(s.vetoHolderId)) Pass("the holder is the other nominee");
                        else
                        {
                            DialogueRules2Season.CloseAsks(engine);
                            s = engine.Snapshot;
                            if (DialogueRules.CanStart(s, s.vetoHolderId, out string why))
                            {
                                TestContext.Out.WriteLine("Rules-3 veto meeting: seed " + seed + ", week " + s.week + ", revision " + s.revision + ".");
                                return new Stop { Seed = seed, State = s.Clone(), People = new[] { s.vetoHolderId } };
                            }
                            Pass("CanStart: " + why);
                        }
                    }
                    var next = EpisodeEngineTests.NextCommand(engine.Snapshot);
                    if (next.kind == EpisodeCommandKind.Compete) next.performance = 0;
                    DialogueRules2Season.Accepted(engine, next);
                }
            }
            Assert.Fail("No rules-3 season of 60 met a veto meeting with the player nominated and the holder free to talk. Passed over: "
                + string.Join("; ", passed.Select(kv => kv.Key + " x" + kv.Value)));
            return null;
        }

        /// <summary>The entry the HUD builds for a houseguest: the public plan, never a hand-made payload.</summary>
        internal static EpisodeCommand Entry(EpisodeState s, string npcId, string template)
        {
            Assert.That(DialogueRules.TryBuildEntryCommand(s, npcId, out var plan), Is.True, "The HUD's own entry plan.");
            Assert.That(plan.text, Is.EqualTo(template));
            plan.id = "dialogue-r3-entry-" + s.revision + "-" + npcId;
            return plan;
        }

        internal static DialogueEpisodeState Open(EpisodeState s) => DialogueRules.Active(s);

        internal static string Beat(EpisodeState s) => DialogueRules.Cycle(s, DialogueRules.Active(s)).beatId;

        internal static string[] Options(EpisodeState s)
        {
            var cycle = DialogueRules.Cycle(s, DialogueRules.Active(s));
            return s.houseEvents.Single(e => e.id == cycle.eventId).choices.Select(c => c.optionId).ToArray();
        }

        internal static string Suffix(DialogueResponseState response) =>
            response.responseKey.Substring(response.responseKey.IndexOf('/') + 1);

        /// <summary>Every relationship row whose score differs between two states, as "from>to".</summary>
        internal static string[] Moved(EpisodeState before, EpisodeState after)
        {
            var a = DialogueRules2Season.Scores(before);
            var b = DialogueRules2Season.Scores(after);
            return b.Where(kv => !a.TryGetValue(kv.Key, out double old) || Math.Abs(old - kv.Value) > 1e-12).Select(kv => kv.Key)
                .Concat(a.Keys.Where(k => !b.ContainsKey(k))).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        }
    }
}
