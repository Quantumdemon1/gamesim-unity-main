using System;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Capacity stress fixtures, NOT catalogue-produced history. A real public-command first week
    /// supplies every gameplay fact; only inert completed legacy rows are appended afterwards.
    /// Every source handed to Apply passes the complete EpisodeValidation, not just DialogueRules.
    /// </summary>
    public sealed class DialogueCampaignCapacityTests
    {
        private EpisodeState campaign;
        private string voter;

        [OneTimeSetUp]
        public void FindPublicCampaign() => campaign = DialogueCapacityFixture.FindCampaign(out voter);

        [Test]
        public void Additive397EventsAnd99CyclesAllowTheThreeStepPathToFinishAtBothCaps()
        {
            var source = DialogueCapacityFixture.Pad(campaign, 397, 99);
            string original = DialogueCapacityFixture.Json(source);
            var engine = new EpisodeEngine(source);
            var entered = DialogueCapacityFixture.Accept(engine, DialogueCapacityFixture.Start(source, voter));
            string id = entered.dialogue.episodes.Single().id;
            Assert.That(entered.houseEvents.Count, Is.EqualTo(399));
            Assert.That(entered.storylines.Count, Is.EqualTo(100));
            Assert.That(DialogueRules.ReservedEvents(entered), Is.EqualTo(1));
            Assert.That(DialogueCapacityFixture.Remaining(entered), Is.Zero);
            Assert.That(entered.windowActions[Windows.AfterVeto],
                Is.EqualTo(source.windowActions[Windows.AfterVeto] + 1));
            var middle = DialogueCapacityFixture.Accept(engine,
                DialogueCapacityFixture.Choose(entered, id, "case", "appeal-personally"));
            Assert.That(middle.houseEvents.Count, Is.EqualTo(400));
            Assert.That(DialogueRules.ReservedEvents(middle), Is.Zero);
            var finished = DialogueCapacityFixture.Accept(engine,
                DialogueCapacityFixture.Choose(middle, id, "finish-personal", "make-plea"));
            Assert.That(finished.houseEvents.Count, Is.EqualTo(400));
            Assert.That(finished.storylines.Count, Is.EqualTo(100));
            DialogueCapacityFixture.AssertClosed(finished, id, "answered", 3);
            Assert.That(finished.windowActions, Is.EqualTo(entered.windowActions));
            Assert.That(finished.outOfPhaseSocialActions, Is.EqualTo(entered.outOfPhaseSocialActions));
            DialogueCapacityFixture.AssertPaddingUnchanged(source, finished);
            Assert.That(DialogueCapacityFixture.Json(source), Is.EqualTo(original));
        }

        [TestCase(398)]
        [TestCase(399)]
        [TestCase(400)]
        public void FullValidEventHistoryWithoutThreeFreeSlotsRefusesBeforeAnyDebitOrMint(int count)
        {
            var source = DialogueCapacityFixture.Pad(campaign, count, campaign.storylines.Count);
            Assert.That(DialogueRules.CanStart(source, voter, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("The complete dialogue path cannot fit."));
            var engine = new EpisodeEngine(source);
            DialogueCapacityFixture.Refuse(engine, DialogueCapacityFixture.Start(source, voter),
                "The complete dialogue path cannot fit.");
            Assert.That(engine.Snapshot.dialogue.episodes, Is.Empty);
        }

        [Test]
        public void FullValidHundredCyclesRefusesEntryEvenWhenAllThreeEventSlotsExist()
        {
            var source = DialogueCapacityFixture.Pad(campaign, 397, 100);
            Assert.That(DialogueRules.CanStart(source, voter, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("The complete dialogue path cannot fit."));
            DialogueCapacityFixture.Refuse(new EpisodeEngine(source), DialogueCapacityFixture.Start(source, voter),
                "The complete dialogue path cannot fit.");
        }

        [TestCase("case", "leave", false, 399, 2)]
        [TestCase("finish-personal", "leave", false, 400, 3)]
        [TestCase("finish-personal", "keep-vote-open", false, 400, 3)]
        [TestCase("case", "leave", true, 399, 2)]
        [TestCase("finish-personal", "leave", true, 400, 3)]
        public void FullValidCapacityFixtureCanCloseAtZeroApWithoutAFourthEvent(
            string node, string option, bool advance, int expectedEvents, int pathLength)
        {
            var source = DialogueCapacityFixture.Pad(campaign, 397, 99);
            var engine = new EpisodeEngine(source);
            var entered = DialogueCapacityFixture.Accept(engine, DialogueCapacityFixture.Start(source, voter));
            string id = entered.dialogue.episodes.Single().id;
            if (node == "finish-personal")
                DialogueCapacityFixture.Accept(engine,
                    DialogueCapacityFixture.Choose(engine.Snapshot, id, "case", "appeal-personally"));
            var before = engine.Snapshot;
            Assert.That(DialogueCapacityFixture.Remaining(before), Is.Zero);
            var command = advance ? DialogueCapacityFixture.Command(before, EpisodeCommandKind.Advance)
                : DialogueCapacityFixture.Choose(before, id, node, option);
            var closed = DialogueCapacityFixture.Accept(engine, command);
            Assert.That(closed.houseEvents.Count, Is.EqualTo(expectedEvents));
            Assert.That(closed.storylines.Count, Is.EqualTo(100));
            Assert.That(closed.windowActions, Is.EqualTo(before.windowActions));
            Assert.That(closed.outOfPhaseSocialActions, Is.EqualTo(before.outOfPhaseSocialActions));
            Assert.That(DialogueCapacityFixture.Remaining(closed), Is.Zero);
            Assert.That(closed.phase, Is.EqualTo(advance ? EpisodePhase.Eviction : EpisodePhase.Campaign));
            DialogueCapacityFixture.AssertClosed(closed, id, advance ? "lapsed" : "neutral", pathLength);
            DialogueCapacityFixture.AssertPaddingUnchanged(source, closed);
            DialogueCapacityFixture.Refuse(engine,
                DialogueCapacityFixture.Choose(closed, id, node, option));
        }

        [Test]
        public void FullValidatorRefusesAnAdditiveEventThatConsumesTheRealPausedPathsReservation()
        {
            var source = DialogueCapacityFixture.Pad(campaign, 397, 99);
            var engine = new EpisodeEngine(source);
            var entered = DialogueCapacityFixture.Accept(engine, DialogueCapacityFixture.Start(source, voter));
            Assert.That(entered.houseEvents.Count, Is.EqualTo(399));
            string original = DialogueCapacityFixture.Json(entered);
            var corrupted = entered.Clone();
            corrupted.houseEvents.Add(DialogueCapacityFixture.PaddingEvent(corrupted, 399));
            Assert.That(corrupted.houseEvents.Count, Is.EqualTo(400));
            Assert.That(EpisodeValidation.TryValidate(corrupted, out string error), Is.False);
            Assert.That(error, Is.EqualTo("The active dialogue's remaining event capacity is reserved."));
            Assert.That(DialogueCapacityFixture.Json(entered), Is.EqualTo(original));
            // This is a saved-state adversarial control, NOT an assertion that a real competing
            // writer produced the invalid row. Continue from the original accepted public state.
            var id = entered.dialogue.episodes.Single().id;
            var middle = DialogueCapacityFixture.Accept(engine,
                DialogueCapacityFixture.Choose(entered, id, "case", "appeal-personally"));
            Assert.That(middle.houseEvents.Count, Is.EqualTo(400));
        }
    }

    /// <summary>Same-assembly fixture shared with the native SaveStore boundary suite.</summary>
    internal static class DialogueCapacityFixture
    {
        internal const string Prefix = "capacity-fixture-";
        private const string Template = "dialogue-campaign-v1";

        internal static EpisodeState FindCampaign(out string voter)
        {
            voter = null;
            // The bounded search is solely for actual nomination/eligibility, never an outcome roll.
            for (uint seed = 1; seed <= 24; seed++)
            {
                var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(fresh);
                Assert.That(fresh.dialogueRulesVersion, Is.Zero);
                fresh.dialogueRulesVersion = 1; // Unpublished fixture opt-in only.
                Valid(fresh);
                var engine = new EpisodeEngine(fresh);
                for (int step = 0; step < 96; step++)
                {
                    var s = engine.Snapshot;
                    if (s.week != 1 || s.phase == EpisodePhase.Finished) break;
                    if (s.phase == EpisodePhase.Campaign)
                    {
                        if (!s.nominees.Contains(s.playerId)) break;
                        CloseOldAsks(engine);
                        for (int talk = 0; talk < 16 && Remaining(engine.Snapshot) > 1; talk++)
                        {
                            var c = Command(engine.Snapshot, EpisodeCommandKind.Talk);
                            c.targetId = engine.Snapshot.nominees.First(id => id != engine.Snapshot.playerId);
                            Accept(engine, c);
                            CloseOldAsks(engine);
                        }
                        s = engine.Snapshot;
                        if (Remaining(s) != 1) break;
                        voter = EpisodeEngine.Voters(s).Where(c => !c.isPlayer).Select(c => c.id)
                            .FirstOrDefault(id => DialogueRules.CanStart(s, id, out _));
                        if (voter == null) break;
                        Valid(s);
                        TestContext.Out.WriteLine("Capacity fixture actual base: seed " + seed
                            + ", week " + s.week + ", revision " + s.revision + ", voter " + voter + ".");
                        return s.Clone();
                    }
                    Accept(engine, ModeTwoReaderSweep.Next(s));
                }
            }
            Assert.Fail("No actual nominated first-week Campaign with one AP in 24 seeds x 96 public commands.");
            return null;
        }

        internal static EpisodeState Pad(EpisodeState original, int events, int cycles)
        {
            Valid(original);
            string before = Json(original);
            Assert.That(events, Is.InRange(original.houseEvents.Count, 400));
            Assert.That(cycles, Is.InRange(original.storylines.Count, 100));
            var padded = original.Clone();
            while (padded.houseEvents.Count < events)
                padded.houseEvents.Add(PaddingEvent(padded, padded.houseEvents.Count));
            while (padded.storylines.Count < cycles)
                padded.storylines.Add(new StorylineState
                {
                    id = Prefix + "cycle-" + padded.storylines.Count,
                    templateId = Prefix + "legacy", title = "Additive capacity test record",
                    week = padded.week, endedWeek = padded.week, status = StorylineStatus.Completed
                });
            Valid(padded);
            // Remove only this fixture's appended ranges from a detached copy. Exact original
            // serialization proves every pre-existing field, history row, RNG and counter is intact.
            var stripped = padded.Clone();
            stripped.houseEvents.RemoveRange(original.houseEvents.Count, events - original.houseEvents.Count);
            stripped.storylines.RemoveRange(original.storylines.Count, cycles - original.storylines.Count);
            Assert.That(Json(stripped), Is.EqualTo(before));
            Assert.That(Json(original), Is.EqualTo(before));
            return padded;
        }

        internal static HouseEventState PaddingEvent(EpisodeState s, int index) => new HouseEventState
        {
            id = Prefix + "event-" + index, kind = HouseEventKind.Ambient,
            title = "Additive capacity test record",
            narrative = "Inert capacity padding, not an authored or generated gameplay occurrence.",
            week = s.week, resolved = true
        };

        internal static void AssertPaddingUnchanged(EpisodeState before, EpisodeState after)
        {
            Assert.That(Json(after.houseEvents.Where(e => e.id.StartsWith(Prefix, StringComparison.Ordinal))),
                Is.EqualTo(Json(before.houseEvents.Where(e => e.id.StartsWith(Prefix, StringComparison.Ordinal)))));
            Assert.That(Json(after.storylines.Where(c => c.id.StartsWith(Prefix, StringComparison.Ordinal))),
                Is.EqualTo(Json(before.storylines.Where(c => c.id.StartsWith(Prefix, StringComparison.Ordinal)))));
        }

        internal static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = "capacity-" + s.revision + "-" + kind, kind = kind, actorId = s.playerId,
            expectedRevision = s.revision, expectedPhase = s.phase
        };

        internal static EpisodeCommand Start(EpisodeState s, string npc)
        {
            var c = Command(s, EpisodeCommandKind.StartDialogue);
            c.targetId = npc; c.secondTargetId = "open-case"; c.text = Template;
            return c;
        }

        internal static EpisodeCommand Choose(EpisodeState s, string id, string node, string option)
        {
            var c = Command(s, EpisodeCommandKind.ChooseDialogue);
            c.targetId = id; c.secondTargetId = option; c.text = node;
            return c;
        }

        internal static EpisodeState Accept(EpisodeEngine engine, EpisodeCommand c)
        {
            var source = engine.Snapshot;
            Valid(source);
            string before = Json(source), command = Json(c);
            var result = engine.Apply(c);
            Assert.That(result.accepted, Is.True, source.phase + " " + c.kind + ": " + result.reason);
            Assert.That(result.duplicate, Is.False);
            Assert.That(result.state.revision, Is.EqualTo(source.revision + 1));
            Valid(result.state);
            Assert.That(Json(source), Is.EqualTo(before));
            Assert.That(Json(c), Is.EqualTo(command));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(Json(result.state)));
            return result.state;
        }

        internal static void Refuse(EpisodeEngine engine, EpisodeCommand c, string expectedReason = null)
        {
            var source = engine.Snapshot;
            Valid(source);
            string before = Json(source), command = Json(c);
            var result = engine.Apply(c);
            Assert.That(result.accepted, Is.False);
            Assert.That(result.duplicate, Is.False);
            Assert.That(result.reason, Is.Not.Null.And.Not.Empty);
            if (expectedReason != null) Assert.That(result.reason, Is.EqualTo(expectedReason));
            Assert.That(Json(result.state), Is.EqualTo(before), "No RNG/AP/IDs/log/history change on refusal.");
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before));
            Assert.That(Json(source), Is.EqualTo(before));
            Assert.That(Json(c), Is.EqualTo(command));
            Valid(result.state);
        }

        internal static void AssertClosed(EpisodeState s, string id, string ending, int steps)
        {
            var cycle = s.storylines.Single(c => c.id == id);
            var episode = s.dialogue.episodes.Single(e => e.id == id);
            Assert.That(cycle.status, Is.EqualTo(StorylineStatus.Completed));
            Assert.That(cycle.endingId, Is.EqualTo(ending));
            Assert.That(cycle.path.Count, Is.EqualTo(steps));
            Assert.That(s.houseEvents.Count(e => e.cycleId == id), Is.EqualTo(steps));
            Assert.That(s.houseEvents.Where(e => e.cycleId == id).All(e => e.resolved), Is.True);
            Assert.That(DialogueRules.Active(s), Is.Null);
            Assert.That(DialogueRules.ReservedEvents(s), Is.Zero);
            Assert.That(episode.time, Is.Not.Null);
            Assert.That(episode.time.reason, Is.EqualTo(ending == "lapsed" ? "occasion-closed" : ending));
            Assert.That(episode.time.revision, Is.EqualTo(s.revision));
            Assert.That(episode.time.sequence, Is.EqualTo(cycle.path.Last().logSequence));
            Assert.That(new[] { episode.time.proposedDelta, episode.time.appliedDelta,
                episode.time.spentRevision, episode.time.spentSequence, episode.time.spentWindowAction },
                Is.All.EqualTo(0));
            Assert.That(s.dialogue.grants, Is.Empty);
            Assert.That(Remaining(s), Is.Zero);
            Valid(s);
        }

        private static void CloseOldAsks(EpisodeEngine engine)
        {
            for (int i = 0; i < 24; i++)
            {
                var s = engine.Snapshot;
                var e = s.houseEvents.FirstOrDefault(item => !item.resolved && HouseEventKind.Asks(item.kind));
                if (e == null) return;
                Assert.That(e.contentId?.StartsWith(Template + ":", StringComparison.Ordinal) == true, Is.False);
                var c = Command(s, e.IsStory ? EpisodeCommandKind.ProgressStoryline : EpisodeCommandKind.ResolveHouseEvent);
                c.targetId = e.id;
                if (e.IsStory)
                {
                    var leave = e.choices.Single(o => o.optionId == e.lapseOptionId);
                    Assert.That(leave.locked || leave.costsAction || leave.pickPerson, Is.False);
                    c.secondTargetId = leave.optionId;
                }
                else c.text = e.choices.First().label;
                Accept(engine, c);
            }
            Assert.Fail("The actual old asks did not close in 24 public neutral answers.");
        }

        internal static int Remaining(EpisodeState s) => EpisodeEngine.SocialActionBudget(s) - EpisodeEngine.SocialActionsSpent(s);
        internal static string Json(object value) => JsonConvert.SerializeObject(value, Formatting.None);
        internal static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
    }
}
