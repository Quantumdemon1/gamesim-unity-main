using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// A genuine season on dialogue rules 1 (DIALOGUE-PLAN D1), for the tests of what reads one: the shipped season
    /// enrolled by the literal 1, as the pilot's own fixtures enrol it (<see cref="Start"/>), walked by the plain public
    /// walk to the first nominated first-week campaign, the house's open asks answered with their own neutral exits,
    /// and the conversation opened through the public entry command the HUD builds. Past the enrolment nothing is
    /// assigned by hand: no nominee, counter, roll or store row.
    ///
    /// <para>Rules 1 by its literal, never by the chip: tests that mean rules 1 never read
    /// <see cref="DialogueRules.Offered"/> (DIALOGUE-PLAN §2), which moves when a later stack brings the chip a new
    /// version, and would then hand these schema-29, rules-1 tests a season on another version. That the chip's
    /// season is this start, field for field, is the chip's own tests' to say (ShippedRulesTests).</para>
    /// </summary>
    internal static class DialoguePreviewWitness
    {
        internal sealed class Witness
        {
            internal uint Seed;
            internal string Voter;
            /// <summary>The campaign, before the conversation: rules 1 with the empty store.</summary>
            internal EpisodeState Campaign;
            /// <summary>The same, one command on: the conversation open at its case node, paid with one action.</summary>
            internal EpisodeState Conversation;
        }

        private static Witness found;

        /// <summary>The first such season in seeds 1 to 24, 96 commands each; cached, and handed out as detached copies.</summary>
        internal static Witness Find()
        {
            if (found == null) found = Search();
            return new Witness { Seed = found.Seed, Voter = found.Voter, Campaign = found.Campaign.Clone(), Conversation = found.Conversation.Clone() };
        }

        /// <summary>The witness's house of eight for a seed, before its first command: the shipped season, then dialogue rules 1 by its literal.</summary>
        internal static EpisodeState Start(uint seed)
        {
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
            ShippedRules.ApplyFresh(fresh);
            fresh.dialogueRulesVersion = 1;
            return fresh;
        }

        private static Witness Search()
        {
            for (uint seed = 1; seed <= 24; seed++)
            {
                var engine = new EpisodeEngine(Start(seed));
                for (int step = 0; step < 96; step++)
                {
                    var s = engine.Snapshot;
                    if (s.week != 1 || s.phase == EpisodePhase.Finished) break;
                    if (s.phase == EpisodePhase.Campaign)
                    {
                        if (!s.nominees.Contains(s.playerId)) break;
                        CloseOldAsks(engine);
                        s = engine.Snapshot;
                        string voter = EpisodeEngine.Voters(s).Where(c => !c.isPlayer).Select(c => c.id)
                            .FirstOrDefault(id => DialogueRules.CanStart(s, id, out _));
                        if (voter == null) break;
                        Assert.That(DialogueRules.TryBuildEntryCommand(s, voter, out var entry), Is.True);
                        entry.id = "preview-witness-entry";
                        var result = engine.Apply(entry);
                        Assert.That(result.accepted, Is.True, result.reason);
                        TestContext.Out.WriteLine("Preview witness: seed " + seed + ", week 1 campaign, revision " + s.revision + ", voter " + voter + ".");
                        return new Witness { Seed = seed, Voter = voter, Campaign = s.Clone(), Conversation = result.state.Clone() };
                    }
                    var next = EpisodeEngineTests.NextCommand(s);
                    var applied = engine.Apply(next);
                    Assert.That(applied.accepted, Is.True, next.kind + ": " + applied.reason);
                }
            }
            Assert.Fail("No rules-1 season of 24 met a nominated first-week campaign with a voter the conversation can start with.");
            return null;
        }

        /// <summary>The house's open asks, answered with their own neutral exits, as the pilot's fixtures do: the conversation waits for them.</summary>
        private static void CloseOldAsks(EpisodeEngine engine)
        {
            for (int answer = 0; answer < 24; answer++)
            {
                var s = engine.Snapshot;
                var item = s.houseEvents.FirstOrDefault(e => !e.resolved && HouseEventKind.Asks(e.kind));
                if (item == null) return;
                var command = new EpisodeCommand
                {
                    id = "preview-witness-ask-" + s.revision, actorId = s.playerId, expectedRevision = s.revision, expectedPhase = s.phase,
                    kind = item.IsStory ? EpisodeCommandKind.ProgressStoryline : EpisodeCommandKind.ResolveHouseEvent, targetId = item.id,
                };
                if (item.IsStory) command.secondTargetId = item.lapseOptionId; else command.text = item.choices.First().label;
                var result = engine.Apply(command);
                Assert.That(result.accepted, Is.True, result.reason);
            }
            Assert.Fail("The house's asks did not close within 24 neutral answers.");
        }
    }
}
