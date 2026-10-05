using System;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Move-in night's actions (ACTIONS-DEALS-ALLIANCES-PLAN 1.1 and X-a). Week one's first free
    /// time is the window the week's rules call 'after the eviction', open before anybody has been
    /// evicted, and in a house of three to eight it has one seat. Nothing the opening does spends it:
    /// the opening's beats are bookkeeping, the introductions are free, and so is the first night's
    /// one real conversation. A player who sees no actions left on their first look at free time
    /// spent one in the house before they got there.
    ///
    /// <para>The night's wording is the free-time screen's (FreeTimeBudgetRuleTests); this is the
    /// engine's side of it, so it runs without Unity.</para>
    /// </summary>
    public sealed class MoveInNightTests
    {
        /// <summary>A season of <paramref name="houseguests"/> on its first night, under the week's rules and the story, as every season the director starts is.</summary>
        private static EpisodeEngine MoveIn(int houseguests = 8, bool newEconomy = false)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = houseguests }, 5u);
            EpisodeEngine.EnableStory(state);
            EpisodeEngine.EnableWeek(state);
            if (newEconomy) EpisodeEngine.EnableEconomy(state);
            return new EpisodeEngine(state);
        }

        private static CommandResult Apply(EpisodeEngine engine, EpisodeCommandKind kind, string target = null, string second = null, string text = null)
        {
            var state = engine.Snapshot;
            return engine.Apply(new EpisodeCommand
            {
                id = "move-in-" + kind + "-" + state.revision + "-" + Guid.NewGuid().ToString("N"),
                actorId = state.playerId, expectedRevision = state.revision, expectedPhase = state.phase,
                kind = kind, targetId = target, secondTargetId = second, text = text,
            });
        }

        private static void Accepted(CommandResult result, string what) => Assert.That(result.accepted, Is.True, what + ": " + result.reason);

        [TestCase(false, 1)]
        [TestCase(true, 2)]
        public void TheOpeningSpendsNoneOfTheNightsActionsUnderEitherEconomy(bool newEconomy, int budget)
        {
            var engine = MoveIn(newEconomy: newEconomy);
            var night = engine.Snapshot;
            Assert.That(EpisodeEngine.IsFirstNight(night), Is.True, "The fixture is move-in night.");
            Assert.That(EpisodeEngine.WeekRulesOn(night), Is.True, "under the week's rules,");
            Assert.That(EpisodeEngine.Window(night), Is.EqualTo(Windows.AfterEviction), "whose free time is the window after the eviction.");
            Assert.That(EpisodeEngine.SocialActionBudget(night), Is.EqualTo(budget));
            Assert.That(EpisodeEngine.SocialActionsSpent(night), Is.Zero);

            // The opening, as the sequence plays it: its beats in order, an introduction to everybody
            // between the tour and the meet-and-greet's close, then the meet-and-greet.
            foreach (var beat in OpeningBeat.InOrder.Where(beat => beat != OpeningBeat.MeetAndGreet))
                Accepted(Apply(engine, EpisodeCommandKind.MarkOpeningBeat, beat), beat);
            var guests = EpisodeEngine.StillToMeet(engine.Snapshot);
            Assert.That(guests, Has.Count.EqualTo(7), "Everybody is still to meet.");
            for (int i = 0; i < guests.Count; i++)
                Accepted(Apply(engine, EpisodeCommandKind.Introduce, guests[i].id, WebIntroductions.Approaches[i % 3].Id), "Meeting " + guests[i].name);
            Accepted(Apply(engine, EpisodeCommandKind.MarkOpeningBeat, OpeningBeat.MeetAndGreet), OpeningBeat.MeetAndGreet);
            var opened = engine.Snapshot;
            Assert.That(EpisodeEngine.SocialActionsSpent(opened), Is.Zero, "The opening spends nothing.");
            Assert.That(EpisodeEngine.SocialActionBudget(opened), Is.EqualTo(budget), "The opening spends none of the night's base actions.");

            // The meet-and-greet over, the first night's one real conversation is waiting, and it is free.
            var conversation = EpisodeEngine.OpenStoryBeats(opened).SingleOrDefault(item => StoryText.ArcOf(item)?.id == "first-night");
            Assert.That(conversation, Is.Not.Null, "The first night's conversation is waiting once the meet-and-greet is over.");
            Assert.That(conversation.choices.Any(choice => choice.costsAction), Is.False, "None of its answers spends an action.");
            var answer = conversation.choices.First(choice => !choice.lapse && !choice.locked);
            string person = answer.pickPerson ? answer.eligibleIds.First() : null;
            Accepted(Apply(engine, EpisodeCommandKind.ProgressStoryline, conversation.id, answer.optionId, person), answer.label);
            var answered = engine.Snapshot;
            Assert.That(answered.houseEvents.Single(item => item.id == conversation.id).resolved, Is.True, "The conversation is had,");
            Assert.That(EpisodeEngine.SocialActionsSpent(answered), Is.Zero, "and it spent nothing:");
            Assert.That(EpisodeEngine.SocialActionBudget(answered) - EpisodeEngine.SocialActionsSpent(answered), Is.EqualTo(budget),
                "the night's actions are still there to spend.");
        }

        /// <summary>
        /// The night's seats are the window's after-the-eviction seats: one for houses of three to
        /// eight, and then one more for every two houseguests, five for a house of sixteen.
        /// </summary>
        [TestCase(3, 1)]
        [TestCase(6, 1)]
        [TestCase(8, 1)]
        [TestCase(9, 2)]
        [TestCase(12, 3)]
        [TestCase(16, 5)]
        public void MoveInNightsSeatsScaleWithTheHouse(int houseguests, int seats)
        {
            var night = MoveIn(houseguests).Snapshot.Clone();
            // Past the roster's largest the house is filled the way the cast-size tests fill it: the
            // seats read nobody but who is in it.
            while (night.contestants.Count < houseguests)
            {
                int index = night.contestants.Count;
                night.contestants.Add(new ContestantState
                {
                    id = "extra-" + index, name = "Extra " + index, pronouns = "they/them", homeRoom = "Living",
                    motive = "Added by a test to fill the house.", status = ContestantStatus.Active,
                    traits = new System.Collections.Generic.List<string> { "Social" }, stats = new ContestantStats(),
                });
            }
            Assert.That(night.Active.Count(), Is.EqualTo(houseguests), "A house of " + houseguests + ".");
            Assert.That(EpisodeEngine.IsFirstNight(night), Is.True);
            Assert.That(EpisodeEngine.WindowSeats(night, Windows.AfterEviction), Is.EqualTo(seats));
            Assert.That(EpisodeEngine.SocialActionBudget(night), Is.EqualTo(seats), "Nothing else is added on the first night.");
        }
    }
}
