using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;
using Capacity = Gamesim.Tests.EditMode.DialogueCapacityFixture;
using R2 = Gamesim.Tests.EditMode.DialogueRules2Season;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Dialogue rules 2's two capacity refusals (DIALOGUE-PLAN §5.5), frozen with rules 2 when the chip moved to it, each
    /// reached through the gate that returns it, <see cref="DialogueRules.CanStart"/> - and so the HUD's entry plan, its
    /// fallback (<see cref="DialogueRules.PleaWithoutConversation"/>) and the engine's entry - at its limit, and one step
    /// short of it, where the conversation still opens through the HUD's own entry plan:
    /// <list type="bullet">
    /// <item><see cref="DialogueRules.BudgetRefusal"/> once the season holds <see cref="DialogueRules.ConversationBudget"/>
    /// of rules 2's conversations, every started one counted, a left one too (§5.5: the attempt is the row);</item>
    /// <item><see cref="DialogueRules.RoomRefusal"/> once the storylines reach <see cref="DialogueRules.Rules2StorylineCeiling"/>,
    /// or the house events leave less than one whole rules-2 path (<see cref="DialogueRules.StepsFor"/>) under
    /// <see cref="DialogueRules.MaximumEvents"/>.</item>
    /// </list>
    /// <para>Capacity fixtures, NOT catalogue-produced history, as DialogueCampaignCapacityTests' are: a genuine rules-2
    /// campaign the plain public walk reaches supplies every gameplay fact, and only inert rows are added - for the room
    /// that fixture's completed legacy storylines and resolved ambient events (<see cref="DialogueCapacityFixture.Pad"/>),
    /// and for the budget closed copies of one conversation the engine played, re-keyed into earlier weeks with the
    /// sequence counter moved past their ids. Every state handed to CanStart or the engine passes the complete
    /// EpisodeValidation.</para>
    /// </summary>
    public sealed class DialogueRules2CapacityTests
    {
        private R2.Campaign campaign;

        private R2.Campaign Found => campaign ?? (campaign = R2.FindCampaign());

        /// <summary>
        /// The storyline ceiling: with 91 storylines a conversation still opens - its own cycle is the 92nd - and with 92
        /// none does, in the room's words, the house events far from their own bound.
        /// </summary>
        [Test]
        public void TheStorylineCeilingLeavesRoomForOneMoreConversationAndRefusesAtNinetyTwo()
        {
            if (R2.HeldBelowItsSchema()) return;
            var source = Found.State;
            string voter = Found.Voters[0];
            int ceiling = DialogueRules.Rules2StorylineCeiling, events = source.houseEvents.Count;
            Assert.That(ceiling, Is.EqualTo(92));
            Assert.That(source.storylines.Count, Is.LessThan(ceiling - 1), "The witness campaign has storylines to spare.");
            Assert.That(events, Is.LessThanOrEqualTo(DialogueRules.MaximumEvents - DialogueRules.StepsFor(DialogueRules.Rules2)),
                "Only the storylines are at their bound.");

            var below = Capacity.Pad(source, events, ceiling - 1);
            AssertOpens(below, voter, "91 storylines");
            var entered = R2.Accepted(new EpisodeEngine(below.Clone()), R2.Entry(below, voter));
            Assert.That(entered.storylines, Has.Count.EqualTo(ceiling), "The last conversation's own cycle is the 92nd storyline.");

            var at = Capacity.Pad(source, events, ceiling);
            AssertRoomRefused(at, voter, "92 storylines");
        }

        /// <summary>
        /// The event room: with 395 house events a conversation still opens - its open path and the events it reserves fill
        /// the house's 400 exactly - and with 396, one fewer than a whole five-step path, none does, in the room's words.
        /// </summary>
        [Test]
        public void TheEventRoomLeavesAWholePathAndRefusesOneEventPastIt()
        {
            if (R2.HeldBelowItsSchema()) return;
            var source = Found.State;
            string voter = Found.Voters[0];
            int room = DialogueRules.MaximumEvents - DialogueRules.StepsFor(DialogueRules.Rules2), cycles = source.storylines.Count;
            Assert.That(room, Is.EqualTo(395), "Rules 2's whole path, five steps, kept free under the house's 400 events.");
            Assert.That(cycles, Is.LessThan(DialogueRules.Rules2StorylineCeiling - 1), "Only the house events are at their bound.");

            var below = Capacity.Pad(source, room, cycles);
            AssertOpens(below, voter, "395 house events");
            var entered = R2.Accepted(new EpisodeEngine(below.Clone()), R2.Entry(below, voter));
            Assert.That(entered.houseEvents.Count + DialogueRules.ReservedEvents(entered), Is.EqualTo(DialogueRules.MaximumEvents),
                "The open path's events and its reservation fill the house's events exactly.");

            var at = Capacity.Pad(source, room + 1, cycles);
            AssertRoomRefused(at, voter, "396 house events");
        }

        /// <summary>
        /// The budget: a season that has had 31 conversations still opens a 32nd through the HUD's entry plan, and once that
        /// one is left - left, not finished: every started conversation counts - the season has had all of rules 2's, so the
        /// next voter's is refused in the budget's words, though the voter is eligible, their occasion unused and the house
        /// far from its storyline and event bounds. The 31 are closed copies of one conversation the engine played in this
        /// campaign, re-keyed into the season's earlier weeks; the 32nd and the refusal are the engine's own.
        /// </summary>
        [Test]
        public void TheThirtySecondConversationIsTheSeasonsLastAndTheNextIsRefusedInTheBudgetsWords()
        {
            if (R2.HeldBelowItsSchema()) return;
            int budget = DialogueRules.ConversationBudget(DialogueRules.Rules2);
            Assert.That(budget, Is.EqualTo(DialogueRules.Rules2Conversations));
            var late = FindLateCampaign(budget - 1);
            string voter = late.Voters[0], other = late.Voters[1];
            var spent = Spend(late, budget - 1);
            Assert.That(spent.dialogue.episodes, Has.Count.EqualTo(budget - 1));

            AssertOpens(spent, voter, (budget - 1) + " conversations");
            var engine = new EpisodeEngine(spent.Clone());
            var entered = R2.Accepted(engine, R2.Entry(spent, voter));
            Assert.That(entered.dialogue.episodes, Has.Count.EqualTo(budget), "The budget's last conversation opened.");
            var left = R2.Accepted(engine, R2.Choice(entered, DialogueCampaignWord.Leave));
            Assert.That(DialogueRules.Active(left), Is.Null, "Left at the reply: closed, and still counted.");
            Assert.That(left.dialogue.episodes, Has.Count.EqualTo(budget));

            // Nothing but the budget stands in the next voter's way.
            Assert.That(left.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(left.nominees, Does.Contain(left.playerId));
            Assert.That(left.dialogue.episodes.Any(e => e.occasionId == DialogueRules.Occasion(left.week, other)), Is.False, "Their occasion is unused.");
            Assert.That(left.storylines.Count, Is.LessThan(DialogueRules.Rules2StorylineCeiling), "Storylines to spare.");
            Assert.That(left.houseEvents.Count, Is.LessThanOrEqualTo(DialogueRules.MaximumEvents - DialogueRules.StepsFor(DialogueRules.Rules2)),
                "House events to spare.");
            Assert.That(left.houseEvents.Any(e => !e.resolved && HouseEventKind.Asks(e.kind)), Is.False, "No house moment waits.");

            Assert.That(DialogueRules.CanStart(left, other, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo(DialogueRules.BudgetRefusal));
            Assert.That(reason, Is.EqualTo("This season has had all its conversations."), "The frozen words.");
            Assert.That(DialogueRules.TryBuildEntryCommand(left, other, out _), Is.False, "No entry is offered.");
            Assert.That(DialogueRules.PleaWithoutConversation(left, other, out string why), Is.True, "The plea is the ordinary one-shot plea.");
            Assert.That(why, Is.EqualTo(reason), "The HUD's paragraph gives CanStart's own words.");
            Assert.That(R2.Refused(engine, StartCommand(left, other)), Is.EqualTo(reason), "The engine refuses the entry in the same words.");
        }

        // ---------------------------------------------------------------- the gate at each side of a limit

        private static void AssertOpens(EpisodeState s, string voter, string where)
        {
            Capacity.Valid(s);
            Assert.That(DialogueRules.CanStart(s, voter, out string refusal), Is.True, where + ": one step short of the limit, refused: " + refusal);
            Assert.That(DialogueRules.TryBuildEntryCommand(s, voter, out _), Is.True, where + ": the HUD's entry plan is built.");
            Assert.That(DialogueRules.PleaWithoutConversation(s, voter, out _), Is.False, where + ": the press is the conversation.");
        }

        private static void AssertRoomRefused(EpisodeState s, string voter, string where)
        {
            Capacity.Valid(s);
            Assert.That(DialogueRules.Active(s), Is.Null, where);
            Assert.That(DialogueRules.CanStart(s, voter, out string reason), Is.False, where);
            Assert.That(reason, Is.EqualTo(DialogueRules.RoomRefusal), where);
            Assert.That(reason, Is.EqualTo("The house has no room for another conversation."), where + ": the frozen words.");
            Assert.That(DialogueRules.TryBuildEntryCommand(s, voter, out _), Is.False, where + ": no entry is offered.");
            Assert.That(DialogueRules.PleaWithoutConversation(s, voter, out string why), Is.True, where + ": the plea is the ordinary one-shot plea.");
            Assert.That(why, Is.EqualTo(reason), where + ": the HUD's paragraph gives CanStart's own words.");
            Assert.That(R2.Refused(new EpisodeEngine(s.Clone()), StartCommand(s, voter)), Is.EqualTo(reason),
                where + ": the engine refuses the entry in the same words.");
        }

        /// <summary>The entry a client could send without the HUD's plan: the open, rules 2's template, for this voter.</summary>
        private static EpisodeCommand StartCommand(EpisodeState s, string voter)
        {
            var start = R2.Command(s, EpisodeCommandKind.StartDialogue);
            start.targetId = voter; start.secondTargetId = DialogueCampaignWord.OpenCase; start.text = DialogueCampaignWord.TemplateId;
            start.id = "capacity-r2-start-" + s.revision + "-" + voter;
            return start;
        }

        // ---------------------------------------------------------------- the budget's fixture

        private sealed class LateCampaign
        {
            internal uint Seed;
            internal EpisodeState State;
            internal string[] Voters;
            /// <summary>One conversation the engine played in this campaign: opened, answered without a word given, left at the reply.</summary>
            internal DialogueEpisodeState Episode;
            internal StorylineState Cycle;
            internal HouseEventState[] Items;
            /// <summary>The earlier weeks' occasions the copies take, in week order.</summary>
            internal List<(int week, string npc)> Slots;
        }

        /// <summary>
        /// The first campaign in seeds 1 to 24 of twelve houseguests, walked by the plain public walk, in which the player is
        /// nominated in a week late enough that the earlier weeks hold <paramref name="copies"/> occasions, the house's asks
        /// are answered, two voters can be talked to with an action left, and the house has room for the copies and two
        /// conversations more. There, on a detached copy, the first voter whose conversation opens without their word on the
        /// vote and without a rescue to cite is talked to and left at the reply: the conversation the copies are made of.
        /// </summary>
        private static LateCampaign FindLateCampaign(int copies)
        {
            int steps = DialogueRules.StepsFor(DialogueRules.Rules2);
            for (uint seed = 1; seed <= 24; seed++)
            {
                var engine = new EpisodeEngine(R2.Start(seed, 12));
                var weeks = new HashSet<int>();
                for (int step = 0; step < 1200; step++)
                {
                    var s = engine.Snapshot;
                    if (s.phase == EpisodePhase.Finished || s.Find(s.playerId).status != ContestantStatus.Active) break;
                    if (s.phase == EpisodePhase.Campaign && s.nominees.Contains(s.playerId) && weeks.Add(s.week))
                    {
                        R2.CloseAsks(engine);
                        s = engine.Snapshot;
                        var slots = Slots(s);
                        var voters = EpisodeEngine.Voters(s).Where(c => !c.isPlayer).Select(c => c.id)
                            .Where(id => DialogueRules.CanStart(s, id, out _)).ToArray();
                        if (slots.Count >= copies && voters.Length >= 2 && R2.Remaining(s) >= 1 && s.dialogue.episodes.Count == 0
                            && s.storylines.Count + copies + 2 < DialogueRules.Rules2StorylineCeiling
                            && s.houseEvents.Count + 2 * copies + 2 * steps <= DialogueRules.MaximumEvents - steps)
                        {
                            var found = Played(s, voters);
                            if (found != null)
                            {
                                found.Seed = seed; found.Slots = slots.Take(copies).ToList();
                                TestContext.Out.WriteLine("Rules-2 budget campaign: seed " + seed + ", week " + s.week + ", revision " + s.revision
                                    + ", " + s.storylines.Count + " storylines, " + s.houseEvents.Count + " house events; copied conversation with "
                                    + found.Episode.npcId + " (" + R2.Suffix(found.Episode.responses[0]) + ", " + found.Cycle.beatId + ").");
                                return found;
                            }
                        }
                    }
                    R2.Accepted(engine, ModeTwoReaderSweep.Next(engine.Snapshot));
                }
            }
            Assert.Fail("No rules-2 season of twelve in 24 met a late nominated campaign with room for " + copies + " earlier conversations.");
            return null;
        }

        /// <summary>
        /// Every earlier week's campaign occasion with somebody in the season, the player aside, in week then cast order,
        /// leaving out anyone who ever used the veto to save the player before that week: a copy is a conversation with no
        /// rescue to cite, which rules 2 offers wherever one exists (DialogueRules.PublicRescueSource), so those are skipped.
        /// </summary>
        private static List<(int week, string npc)> Slots(EpisodeState s)
        {
            var rescues = s.ledger?.power?.Where(row => row != null && row.vetoUsed && row.savedId == s.playerId).ToList()
                ?? new List<PowerRow>();
            var slots = new List<(int week, string npc)>();
            for (int week = 1; week < s.week; week++)
                foreach (var person in s.contestants.Where(c => !c.isPlayer))
                    if (!rescues.Any(row => row.week < week && row.vetoHolderId == person.id)) slots.Add((week, person.id));
            return slots;
        }

        private static LateCampaign Played(EpisodeState s, string[] voters)
        {
            foreach (string voter in voters)
            {
                var engine = new EpisodeEngine(s.Clone());
                var entered = R2.Accepted(engine, R2.Entry(s, voter));
                var opened = R2.Conversation(entered, voter);
                string answer = R2.Suffix(opened.responses[0]);
                string reply = R2.Cycle(entered, opened).beatId;
                if (answer != DialogueCampaignWord.Guarded && answer != DialogueCampaignWord.Concern || DialogueCampaignWord.OffersRescue(reply)) continue;
                var left = R2.Accepted(engine, R2.Choice(entered, DialogueCampaignWord.Leave));
                var episode = R2.Conversation(left, voter);
                var cycle = R2.Cycle(left, episode);
                var items = left.houseEvents.Where(e => e.cycleId == episode.id).ToArray();
                Assert.That((episode.responses.Count, cycle.path.Count, items.Length), Is.EqualTo((2, 2, 2)), "Opened, answered and left at the reply.");
                Assert.That(cycle.endingId, Is.EqualTo("neutral"));
                Assert.That(episode.statement, Is.Null, "No word given, so no statement copied.");
                Assert.That(episode.evidence, Is.Empty, "Nothing cited.");
                return new LateCampaign
                {
                    State = s.Clone(), Voters = voters, Episode = episode.Clone(), Cycle = cycle.Clone(), Items = items.Select(e => e.Clone()).ToArray(),
                };
            }
            return null;
        }

        /// <summary>
        /// The late campaign with its earlier weeks' occasions spent: each a closed copy of the played conversation, re-keyed to
        /// its week and voter - its own ids from the sequence counter, which is moved past them, its own pair of revisions in
        /// order before the campaign's, and its own window tick in its week - and nothing else of the season touched.
        /// </summary>
        private static EpisodeState Spend(LateCampaign late, int count)
        {
            var s = late.State.Clone();
            Capacity.Valid(s);
            Assert.That(s.dialogue.episodes, Is.Empty);
            Assert.That(late.Slots, Has.Count.EqualTo(count));
            string untouched = R2.Json(s);
            int sequence = s.nextSequence, revision = 0, first = s.nextSequence, storylines = s.storylines.Count, events = s.houseEvents.Count;
            var ticks = new Dictionary<int, int>();
            foreach (var (week, npc) in late.Slots)
            {
                int start = sequence, open = sequence + 1, opened = sequence + 2, reply = sequence + 3, replied = sequence + 4;
                sequence += 5;
                int entered = ++revision, left = ++revision;
                ticks.TryGetValue(week, out int tick);
                ticks[week] = ++tick;
                string id = "story-" + start.ToString(CultureInfo.InvariantCulture);

                var episode = late.Episode.Clone();
                episode.id = id; episode.npcId = npc; episode.occasionId = DialogueRules.Occasion(week, npc); episode.week = week;
                episode.entryEventId = House(open); episode.startedSequence = start; episode.startedRevision = entered;
                episode.entry.revision = entered; episode.entry.windowActionBefore = tick - 1; episode.entry.windowActionAfter = tick;
                episode.responses[0].eventId = House(open); episode.responses[0].speakerId = npc; episode.responses[0].revision = entered;
                episode.responses[1].eventId = House(reply); episode.responses[1].speakerId = npc; episode.responses[1].revision = left;
                episode.time.revision = left; episode.time.sequence = replied;

                var cycle = late.Cycle.Clone();
                cycle.id = id; cycle.week = week; cycle.endedWeek = week; cycle.eventId = House(reply);
                cycle.cast.Single().contestantId = npc;
                cycle.path[0].week = week; cycle.path[0].logSequence = opened;
                cycle.path[1].week = week; cycle.path[1].logSequence = replied;

                var items = late.Items.Select(e => e.Clone()).ToArray();
                for (int index = 0; index < items.Length; index++)
                {
                    items[index].id = House(index == 0 ? open : reply);
                    items[index].week = week; items[index].cycleId = id;
                    items[index].cast.Single().contestantId = npc;
                    items[index].involvedIds = new List<string> { npc };
                }
                s.dialogue.episodes.Add(episode);
                s.storylines.Add(cycle);
                s.houseEvents.AddRange(items);
            }
            Assert.That(revision, Is.LessThanOrEqualTo(s.revision), "Every copy's revisions precede the campaign's own.");
            s.nextSequence = sequence;
            Capacity.Valid(s);
            Assert.That(DialogueRules.TryValidate(s, out string error), Is.True, error);

            // Only the copies and the counter moved: strip them from a detached copy and the campaign is as it was.
            var stripped = s.Clone();
            stripped.dialogue.episodes.Clear();
            stripped.storylines.RemoveRange(storylines, count);
            stripped.houseEvents.RemoveRange(events, 2 * count);
            stripped.nextSequence = first;
            Assert.That(R2.Json(stripped), Is.EqualTo(untouched));
            return s;
        }

        private static string House(int sequence) => "house-event-" + sequence.ToString(CultureInfo.InvariantCulture);
    }
}
