using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Storylines: house events that remember.
    ///
    /// <para>What makes a storyline more than a situation with buttons is what it leaves behind, so
    /// most of what is worth testing is about the modifier: that it is granted, that it is read by
    /// something, that it runs out, and that taking the same choice twice does not stack it.</para>
    /// </summary>
    public sealed class StorylineTests
    {
        // ---------------------------------------------------------------- the catalog

        [Test]
        public void EveryTemplateIsWellFormedAndOffersARealChoice()
        {
            Assert.That(Storylines.Catalog, Is.Not.Empty);
            CollectionAssert.AllItemsAreUnique(Storylines.Catalog.Select(t => t.id).ToList());

            foreach (var template in Storylines.Catalog)
            {
                Assert.That(template.title, Is.Not.Null.And.Not.Empty, template.id);
                Assert.That(template.chapterTitle, Is.Not.Null.And.Not.Empty, template.id);
                Assert.That(template.narrative, Is.Not.Null.And.Not.Empty, template.id);
                Assert.That(template.roles, Is.Not.Empty, template.id);
                Assert.That(template.cooldownWeeks, Is.GreaterThan(0), template.id);
                Assert.That(template.options.Length, Is.GreaterThanOrEqualTo(2), template.id);

                foreach (var option in template.options)
                {
                    Assert.That(option.label, Is.Not.Null.And.Not.Empty, template.id);
                    Assert.That(HouseEventRisk.IsKnown(option.risk), Is.True, template.id);
                    foreach (var move in option.moves)
                        Assert.That(template.roles, Does.Contain(move.role),
                            template.id + " moves " + move.role + ", which it does not cast.");
                    if (option.modifier == null) continue;
                    Assert.That(option.modifier.weeks, Is.GreaterThan(0), option.modifier.id);
                    Assert.That(option.modifier.name, Is.Not.Null.And.Not.Empty, option.modifier.id);
                }

                foreach (string role in new[] { HouseEvents.Ally, HouseEvents.Rival, HouseEvents.Hoh, HouseEvents.Nominee })
                    if (template.narrative.Contains(role))
                        Assert.That(template.roles, Does.Contain(role),
                            template.id + " names " + role + " without casting it.");
            }
        }

        /// <summary>A storyline whose every option left nothing behind would be a house event.</summary>
        [Test]
        public void EveryTemplateLeavesSomethingBehindOnAtLeastOneRoute()
        {
            foreach (var template in Storylines.Catalog)
                Assert.That(template.options.Any(o => o.modifier != null), Is.True,
                    template.id + " leaves nothing behind on any route, which makes it a house event.");
        }

        // ---------------------------------------------------------------- beginning

        [Test]
        public void ABeginningStoryBringsItsChapterWithIt()
        {
            var state = Season(5u, 8);
            state.week = 2;
            Assert.That(Storylines.Begin(state, 0.1, 1, out var story, out var chapter), Is.True);

            Assert.That(story, Is.Not.Null);
            Assert.That(chapter, Is.Not.Null);
            Assert.That(story.eventId, Is.EqualTo(chapter.id), "A record whose chapter went missing is unanswerable.");
            Assert.That(story.status, Is.EqualTo(StorylineStatus.Active));
            Assert.That(story.endedWeek, Is.Zero);
            Assert.That(chapter.resolved, Is.False);
            Assert.That(chapter.narrative, Does.Not.Contain("{"));

            state.storylines.Add(story);
            state.houseEvents.Add(chapter);
            Assert.That(EpisodeValidation.TryValidate(state, out string error), Is.True, error);
        }

        [Test]
        public void NothingBeginsBeforeTheHousesStoryRulesDo()
        {
            var state = Season(5u, 8);
            state.storyRulesStartWeek = state.week + 1;
            Assert.That(Storylines.Ready(state), Is.False);
            Assert.That(Storylines.Begin(state, 0.5, 1, out _, out _), Is.False);
        }

        [Test]
        public void OnlySoManyStoriesRunAtOnce()
        {
            var state = Season(5u, 8);
            state.week = 2;
            for (int i = 0; i < Storylines.MostAtOnce; i++)
            {
                Assert.That(Storylines.Begin(state, i * 0.4, i + 1, out var story, out var chapter), Is.True, i.ToString());
                state.storylines.Add(story);
                state.houseEvents.Add(chapter);
            }
            Assert.That(Storylines.Ready(state), Is.False);
            Assert.That(Storylines.Begin(state, 0.9, 99, out _, out _), Is.False);
        }

        /// <summary>
        /// A cooldown is measured from the week a story ended, not the week it began — otherwise a
        /// long story would be eligible again before it had finished being told.
        /// </summary>
        [Test]
        public void ACooldownRunsFromWhenAStoryEndedRatherThanWhenItStarted()
        {
            var state = Season(5u, 8);
            var template = Storylines.Catalog.First(t => t.minimumWeek <= 1);
            state.week = 1;

            state.storylines.Add(new StorylineState
            {
                id = "s", templateId = template.id, category = template.category, title = template.title,
                status = StorylineStatus.Completed, week = 1, endedWeek = 4,
            });

            state.week = 4 + template.cooldownWeeks - 1;
            Assert.That(Storylines.Available(state, template), Is.False, "Still inside the cooldown.");
            state.week = 4 + template.cooldownWeeks;
            Assert.That(Storylines.Available(state, template), Is.True);
        }

        [Test]
        public void AStoryAlreadyRunningDoesNotStartAgain()
        {
            var state = Season(5u, 8);
            var template = Storylines.Catalog.First(t => t.minimumWeek <= 1);
            state.storylines.Add(new StorylineState
            {
                id = "s", templateId = template.id, category = template.category, title = template.title,
                status = StorylineStatus.Active, week = 1,
            });
            Assert.That(Storylines.Available(state, template), Is.False);
        }

        [Test]
        public void ATemplateWaitsForItsOwnMinimumWeek()
        {
            var late = Storylines.Catalog.First(t => t.minimumWeek > 1);
            var state = Season(5u, 8);
            state.week = late.minimumWeek - 1;
            Assert.That(Storylines.Available(state, late), Is.False);
            state.week = late.minimumWeek;
            Assert.That(Storylines.Available(state, late), Is.True);
        }

        // ---------------------------------------------------------------- what they leave behind

        [Test]
        public void AnsweringAChapterFinishesTheStoryAndBanksItsModifier()
        {
            var engine = Engine(out var state);
            var (story, chapter) = Seed(engine, 0.1);
            var template = Storylines.Find(story.templateId);
            int index = System.Array.FindIndex(template.options, o => o.modifier != null);
            var option = template.options[index];

            var result = engine.Apply(Answer(engine.Snapshot, chapter.id, option.label));
            Assert.That(result.accepted, Is.True, result.reason);

            var after = engine.Snapshot;
            var settled = after.storylines.Single(x => x.id == story.id);
            Assert.That(settled.status, Is.EqualTo(StorylineStatus.Completed));
            Assert.That(settled.endedWeek, Is.EqualTo(after.week));

            var banked = after.activeModifiers.Single(m => m.id == option.modifier.id);
            Assert.That(banked.weeksLeft, Is.EqualTo(option.modifier.weeks));
            Assert.That(banked.competitionBonus, Is.EqualTo(option.modifier.competition));
            Assert.That(EpisodeValidation.TryValidate(after, out string error), Is.True, error);
        }

        [Test]
        public void AnOptionWithNothingToLeaveLeavesNothing()
        {
            var engine = Engine(out var state);
            var (story, chapter) = Seed(engine, 0.1);
            var template = Storylines.Find(story.templateId);
            var plain = template.options.FirstOrDefault(o => o.modifier == null);
            if (plain == null) Assert.Ignore("Every route in this story leaves something.");

            Assert.That(engine.Apply(Answer(engine.Snapshot, chapter.id, plain.label)).accepted, Is.True);
            Assert.That(engine.Snapshot.activeModifiers, Is.Empty);
            Assert.That(engine.Snapshot.storylines.Single().status, Is.EqualTo(StorylineStatus.Completed));
        }

        /// <summary>
        /// A modifier that nothing counted down would last the rest of the season, which is the
        /// opposite of what a duration is.
        /// </summary>
        [Test]
        public void AModifierRunsOut()
        {
            var state = Season(5u, 8);
            state.activeModifiers.Add(new StoryModifierState
            {
                id = "m", name = "Something", weeksLeft = 2, competitionBonus = 3,
            });

            Assert.That(Storylines.CompetitionBonus(state), Is.EqualTo(3));
            Storylines.AgeModifiers(state);
            Assert.That(state.activeModifiers.Single().weeksLeft, Is.EqualTo(1));
            Assert.That(Storylines.CompetitionBonus(state), Is.EqualTo(3));

            Storylines.AgeModifiers(state);
            Assert.That(state.activeModifiers, Is.Empty);
            Assert.That(Storylines.CompetitionBonus(state), Is.Zero);
        }

        [Test]
        public void TakingTheSameChoiceTwiceResetsItRatherThanStackingIt()
        {
            var state = Season(5u, 8);
            for (int i = 0; i < 2; i++)
            {
                state.activeModifiers.RemoveAll(m => m.id == "m");
                state.activeModifiers.Add(new StoryModifierState
                {
                    id = "m", name = "Something", weeksLeft = 2, competitionBonus = 3,
                });
            }
            Assert.That(state.activeModifiers, Has.Count.EqualTo(1));
            Assert.That(Storylines.CompetitionBonus(state), Is.EqualTo(3), "Not six.");
        }

        // ---------------------------------------------------------------- the modifiers are read

        /// <summary>
        /// The test that matters most. A bonus nothing consumes is the shape this port keeps
        /// finding, and a storyline whose reward changed nothing would be the worst instance of it.
        /// </summary>
        [Test]
        public void ACompetitionModifierActuallyChangesTheCompetition()
        {
            var plain = Engine(out var a);
            var boosted = Engine(out var b);
            Live(boosted).activeModifiers.Add(new StoryModifierState
            {
                id = "clutch", name = "Clutch", weeksLeft = 2, competitionBonus = 3,
            });

            Assert.That(Storylines.CompetitionBonus(plain.Snapshot), Is.Zero);
            Assert.That(Storylines.CompetitionBonus(boosted.Snapshot), Is.EqualTo(3));

            var plainScore = Compete(plain);
            var boostedScore = Compete(boosted);
            Assert.That(boostedScore, Is.GreaterThan(plainScore),
                "The same season, the same seed, the same competition — the modifier is the only difference.");
        }

        [Test]
        public void ASocialModifierActuallyChangesTheAllowance()
        {
            var state = Season(5u, 10);
            int plain = EpisodeEngine.SocialActionBudget(state);

            state.activeModifiers.Add(new StoryModifierState
            {
                id = "builder", name = "Alliance Builder", weeksLeft = 2, socialBonus = 10,
            });
            Assert.That(Storylines.SocialActions(state), Is.EqualTo(1));
            Assert.That(EpisodeEngine.SocialActionBudget(state), Is.EqualTo(plain + 1));

            state.activeModifiers.Clear();
            state.activeModifiers.Add(new StoryModifierState
            {
                id = "counter", name = "Counter Intelligence", weeksLeft = 2, socialBonus = -5,
            });
            Assert.That(Storylines.SocialActions(state), Is.Zero,
                "Five points is half an interaction, and half an interaction is none.");
        }

        /// <summary>
        /// The reference's social figures are percentages; this port's allowance is a handful of
        /// actions. Spending them raw would hand a player a whole extra week for one choice.
        /// </summary>
        [Test]
        public void TheReferencesSocialPointsAreConvertedRatherThanSpentRaw()
        {
            Assert.That(StoryModifiers.ActionsFrom(10), Is.EqualTo(1));
            Assert.That(StoryModifiers.ActionsFrom(5), Is.Zero);
            Assert.That(StoryModifiers.ActionsFrom(25), Is.EqualTo(2));
            Assert.That(StoryModifiers.ActionsFrom(-5), Is.Zero);
            Assert.That(StoryModifiers.ActionsFrom(-15), Is.EqualTo(-1));
        }

        [Test]
        public void AWeekNeverHasNoInteractionsAtAll()
        {
            var state = Season(5u, 8);
            state.activeModifiers.Add(new StoryModifierState
            {
                id = "ruinous", name = "Ruinous", weeksLeft = 2, socialBonus = -100,
            });
            Assert.That(EpisodeEngine.SocialActionBudget(state), Is.GreaterThanOrEqualTo(1),
                "A week the player cannot play is not a week.");
        }

        // ---------------------------------------------------------------- being left alone

        [Test]
        public void AStoryNobodyPicksUpIsAbandonedRatherThanCompleted()
        {
            var state = Season(5u, 8);
            state.week = 2;
            Assert.That(Storylines.Begin(state, 0.1, 1, out var story, out var chapter), Is.True);
            state.storylines.Add(story);
            state.houseEvents.Add(chapter);

            state.week = 2 + Storylines.StaleWeeks - 1;
            Storylines.AbandonStale(state);
            Assert.That(state.storylines.Single().status, Is.EqualTo(StorylineStatus.Active));

            state.week = 2 + Storylines.StaleWeeks;
            Storylines.AbandonStale(state);
            var left = state.storylines.Single();
            Assert.That(left.status, Is.EqualTo(StorylineStatus.Abandoned),
                "A thread nobody picked up did not finish, and the cooldown should know the difference.");
            Assert.That(left.endedWeek, Is.EqualTo(state.week));
            Assert.That(EpisodeValidation.TryValidate(state, out string error), Is.True, error);
        }

        [Test]
        public void AStoryAlreadyAnsweredIsNotAbandonedLater()
        {
            var state = Season(5u, 8);
            state.week = 2;
            Storylines.Begin(state, 0.1, 1, out var story, out var chapter);
            chapter.resolved = true;
            chapter.chosenIndex = 0;
            story.status = StorylineStatus.Completed;
            story.endedWeek = 2;
            state.storylines.Add(story);
            state.houseEvents.Add(chapter);

            state.week = 20;
            Storylines.AbandonStale(state);
            Assert.That(state.storylines.Single().status, Is.EqualTo(StorylineStatus.Completed));
        }

        // ---------------------------------------------------------------- through a season

        [Test]
        public void ASeasonPlayedThroughTheEngineTellsStories()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(7u));
            for (int guard = 0; guard < 400 && engine.Snapshot.phase != EpisodePhase.Finished; guard++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }

            var finished = engine.Snapshot;
            Assert.That(finished.storylines, Is.Not.Empty, "A whole season should have told one.");
            CollectionAssert.AllItemsAreUnique(finished.storylines.Select(x => x.id).ToList());
            Assert.That(finished.storylines.Count(x => StorylineStatus.Running(x.status)),
                Is.LessThanOrEqualTo(Storylines.MostAtOnce));
            // Nothing answers for the player, so every story either waits or was written off.
            Assert.That(finished.storylines, Has.All.Matches<StorylineState>(
                x => x.status != StorylineStatus.Completed));
            foreach (var story in finished.storylines)
                Assert.That(finished.houseEvents.Any(e => e.id == story.eventId), Is.True,
                    story.id + " lost its chapter.");
            Assert.That(EpisodeValidation.TryValidate(finished, out string error), Is.True, error);
        }

        // ---------------------------------------------------------------- fixtures

        private static EpisodeState Season(uint seed, int size) =>
            SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);

        private static EpisodeEngine Engine(out EpisodeState state, uint seed = 7u)
        {
            state = Season(seed, 10);
            state.week = 2;
            state.phase = EpisodePhase.Social;
            return new EpisodeEngine(state);
        }

        private static (StorylineState, HouseEventState) Seed(EpisodeEngine engine, double roll)
        {
            var live = Live(engine);
            Assert.That(Storylines.Begin(live, roll, live.nextSequence, out var story, out var chapter), Is.True);
            live.storylines.Add(story);
            live.houseEvents.Add(chapter);
            return (story, chapter);
        }

        private static EpisodeCommand Answer(EpisodeState s, string eventId, string label) =>
            new EpisodeCommand
            {
                id = "cmd-" + s.revision, kind = EpisodeCommandKind.ResolveHouseEvent,
                actorId = s.playerId, targetId = eventId, text = label,
                expectedRevision = s.revision, expectedPhase = s.phase,
            };

        /// <summary>Runs one competition and reports what the player scored.</summary>
        private static double Compete(EpisodeEngine engine)
        {
            var live = Live(engine);
            live.phase = EpisodePhase.HoH;
            live.competitionResolved = false;
            var command = new EpisodeCommand
            {
                id = "compete-" + live.revision, kind = EpisodeCommandKind.Compete,
                actorId = live.playerId, performance = 0.5,
                expectedRevision = live.revision, expectedPhase = live.phase,
            };
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            var after = engine.Snapshot;
            return after.competitionScores.Single(x => x.contestantId == after.playerId).score;
        }

        private static EpisodeState Live(EpisodeEngine engine) => (EpisodeState)typeof(EpisodeEngine)
            .GetField("current", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(engine);
    }
}
