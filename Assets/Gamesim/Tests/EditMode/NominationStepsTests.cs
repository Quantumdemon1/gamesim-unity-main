using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The nomination's tracker (PACK8-PASS-PLAN B1): its steps are this week's stories, read from the
    /// house's events on every render, then the Head of Household's decision, the ceremony and the
    /// outcome - three or four of them, exactly one current by the plan's rule, and only a waiting
    /// story that is not current a thing the player can press.
    /// </summary>
    public sealed class NominationStepsTests
    {
        private static EpisodeState Nomination(bool playerHoh = false, uint seed = 20260930)
        {
            var s = ContentCatalog.Create(seed);
            s.phase = EpisodePhase.Nomination;
            s.week = 2;
            s.hohId = playerHoh ? s.playerId : s.contestants.First(c => !c.isPlayer).id;
            s.houseEvents.Clear();
            return s;
        }

        /// <summary>A story beat as the engine stores one: its cycle, the anchor it closes at, and a lapse option.</summary>
        private static HouseEventState Beat(EpisodeState s, string id, string cycle, string title, string closes = StoryAnchors.NomsSet,
            int? week = null)
        {
            var beat = new HouseEventState
            {
                id = id, kind = HouseEventKind.Story, cycleId = cycle, title = title, narrative = "Something is happening.",
                week = week ?? s.week, closesAnchor = closes, lapseOptionId = "not-now",
                choices = new List<HouseEventChoice>
                {
                    new HouseEventChoice { label = "Listen", optionId = "listen" },
                    new HouseEventChoice { label = "Not now", optionId = "not-now", lapse = true },
                },
            };
            s.houseEvents.Add(beat);
            return beat;
        }

        private static void Answer(HouseEventState beat, string optionId)
        {
            beat.resolved = true;
            beat.chosenIndex = beat.choices.FindIndex(c => c.optionId == optionId);
        }

        private static string[] Labels(List<NominationSteps.Step> steps) => steps.Select(step => step.label).ToArray();

        /// <summary>What every list of steps holds to: three or four, one current, numbered captions that never repeat.</summary>
        private static void AssertWellFormed(List<NominationSteps.Step> steps, string where)
        {
            Assert.That(steps.Count, Is.InRange(3, NominationSteps.MostSteps), where + ": three or four steps.");
            Assert.That(steps.Count(step => step.standing == NominationSteps.Standing.Current), Is.EqualTo(1), where + ": exactly one step is current.");
            Assert.That(Labels(steps), Is.Unique, where + ": every step's caption is its own.");
            for (int i = 0; i < steps.Count; i++)
                Assert.That(steps[i].label, Does.StartWith((i + 1) + ". "), where + ": numbered in order.");
            Assert.That(steps.Where(step => step.Opens).All(step => step.kind == NominationSteps.Kind.Story), Is.True,
                where + ": only a story can be pressed.");
        }

        [Test]
        public void AWeekWithNoStoriesIsTheHeadOfHouseholdTheCeremonyAndTheOutcome()
        {
            var s = Nomination();
            var steps = NominationSteps.Build(s);
            AssertWellFormed(steps, "No stories");
            Assert.That(Labels(steps), Is.EqualTo(new[] { "1. Head of Household", "2. Nomination Ceremony", "3. Outcome" }));
            Assert.That(steps.Select(step => step.standing), Is.EqualTo(new[]
                { NominationSteps.Standing.Done, NominationSteps.Standing.Current, NominationSteps.Standing.Next }));
            Assert.That(steps.Any(step => step.Opens), Is.False, "Nothing waits, so nothing is pressed.");
        }

        [Test]
        public void AWaitingStoryIsTheCurrentStepAndTheCeremonyWaitsBehindIt()
        {
            var s = Nomination();
            var lobby = Beat(s, "e1", "c1", "The Lobby");
            var steps = NominationSteps.Build(s);
            AssertWellFormed(steps, "One story");
            Assert.That(Labels(steps), Is.EqualTo(new[] { "1. Head of Household", "2. The Lobby", "3. Nomination Ceremony", "4. Outcome" }));
            var current = NominationSteps.Current(steps);
            Assert.That(current.kind, Is.EqualTo(NominationSteps.Kind.Story));
            Assert.That(current.eventId, Is.EqualTo(lobby.id), "The current step is the beat on screen.");
            Assert.That(current.Opens, Is.False, "The step on screen is not a control as well.");
            Assert.That(steps[2].standing, Is.EqualTo(NominationSteps.Standing.Next));
        }

        [Test]
        public void TwoStoriesOneAtATimeAndTheOtherOneOpensFromTheTracker()
        {
            var s = Nomination();
            var lobby = Beat(s, "e1", "c1", "The Lobby");
            var play = Beat(s, "e2", "c2", "Stay Off the Block");
            var steps = NominationSteps.Build(s);
            AssertWellFormed(steps, "Two stories");
            Assert.That(Labels(steps), Is.EqualTo(new[] { "1. The Lobby", "2. Stay Off the Block", "3. Nomination Ceremony", "4. Outcome" }),
                "Two stories take the Head of Household's place.");
            Assert.That(NominationSteps.Current(steps).eventId, Is.EqualTo(lobby.id), "The first open beat is current.");
            Assert.That(steps[1].standing, Is.EqualTo(NominationSteps.Standing.Waiting));
            Assert.That(steps[1].Opens, Is.True, "The other waits, and pressing it opens it.");
            Assert.That(steps[1].eventId, Is.EqualTo(play.id));

            var opened = NominationSteps.Build(s, play.id);
            AssertWellFormed(opened, "The second story opened");
            Assert.That(NominationSteps.Current(opened).eventId, Is.EqualTo(play.id), "The beat opened from the tracker is current.");
            Assert.That(opened[0].Opens, Is.True, "and the first now waits.");

            Answer(play, "listen");
            var answered = NominationSteps.Build(s, play.id);
            AssertWellFormed(answered, "The opened story answered");
            Assert.That(NominationSteps.Current(answered).eventId, Is.EqualTo(lobby.id), "An answered beat gives the step back to the first open one.");
            Assert.That(answered[1].standing, Is.EqualTo(NominationSteps.Standing.Done));
        }

        [Test]
        public void ThreeStoriesFoldTheLaterOnesIntoOneStep()
        {
            var s = Nomination();
            var first = Beat(s, "e1", "c1", "The Lobby");
            Beat(s, "e2", "c2", "Stay Off the Block");
            var third = Beat(s, "e3", "c3", "The Agenda");
            var steps = NominationSteps.Build(s);
            AssertWellFormed(steps, "Three stories");
            Assert.That(Labels(steps), Is.EqualTo(new[] { "1. The Lobby", "2. " + NominationSteps.FoldedTitle, "3. Nomination Ceremony", "4. Outcome" }));
            Assert.That(steps[1].stories, Is.EqualTo(2), "The fold stands for both later stories.");
            Assert.That(NominationSteps.Current(steps).eventId, Is.EqualTo(first.id));
            Assert.That(NominationSteps.Build(s, third.id)[1].eventId, Is.EqualTo(third.id),
                "A beat in the fold opened from the tracker is the one the fold shows.");
            Assert.That(NominationSteps.Build(s, third.id)[1].standing, Is.EqualTo(NominationSteps.Standing.Current));
        }

        [Test]
        public void AStoryOverIsDoneOrLetPassByWhatItTook()
        {
            var s = Nomination();
            var lobby = Beat(s, "e1", "c1", "The Lobby");
            var play = Beat(s, "e2", "c2", "Stay Off the Block");
            Answer(lobby, "listen");
            Answer(play, "not-now");
            // Last week's settled story is not this nomination's.
            Answer(Beat(s, "old", "c0", "Old news", week: s.week - 1), "listen");
            var npcs = s.contestants.Where(c => !c.isPlayer && c.id != s.hohId).ToList();
            s.nominees = new List<string> { npcs[0].id, npcs[1].id };
            var steps = NominationSteps.Build(s);
            AssertWellFormed(steps, "After the nominations");
            Assert.That(Labels(steps), Is.EqualTo(new[] { "1. The Lobby", "2. Stay Off the Block", "3. Nomination Ceremony", "4. Outcome" }));
            Assert.That(steps.Select(step => step.standing), Is.EqualTo(new[]
            {
                NominationSteps.Standing.Done, NominationSteps.Standing.LetPass, NominationSteps.Standing.Done, NominationSteps.Standing.Current,
            }), "An answer is done, a lapse was let pass, the ceremony is over and the outcome is current.");
            Assert.That(NominationSteps.StandingWord(NominationSteps.Standing.LetPass), Is.EqualTo("Let pass"));
        }

        [Test]
        public void ABeatOpenedAtTheNominationsComesBeforeTheOutcome()
        {
            var s = Nomination();
            Answer(Beat(s, "e1", "c1", "The Lobby"), "not-now");
            var npcs = s.contestants.Where(c => !c.isPlayer && c.id != s.hohId).ToList();
            s.nominees = new List<string> { npcs[0].id, npcs[1].id };
            var tension = Beat(s, "e2", "c2", "Tensions Rise", StoryAnchors.VetoWon);
            var steps = NominationSteps.Build(s);
            AssertWellFormed(steps, "A beat after the names");
            var current = NominationSteps.Current(steps);
            Assert.That(current.eventId, Is.EqualTo(tension.id), "The new beat is current before the outcome.");
            Assert.That(steps.Last().standing, Is.EqualTo(NominationSteps.Standing.Next));
        }

        [Test]
        public void APlayerHeadOfHouseholdPicksFirstWhileAStoryWaitsAndCommittingLetsItPass()
        {
            var s = Nomination(playerHoh: true);
            EpisodeEngine.EnableStory(s, s.week);
            var steps = NominationSteps.Build(s);
            AssertWellFormed(steps, "Player HoH, no story");
            Assert.That(Labels(steps), Is.EqualTo(new[] { "1. Head of Household", "2. Name your nominees", "3. Nomination Ceremony", "4. Outcome" }));
            Assert.That(NominationSteps.Current(steps).kind, Is.EqualTo(NominationSteps.Kind.Picker));
            Assert.That(NominationSteps.LapsingOnNominate(s), Is.Empty);

            var invite = Beat(s, "e1", "c1", "The Invite List");
            steps = NominationSteps.Build(s);
            AssertWellFormed(steps, "Player HoH with The Invite List");
            Assert.That(Labels(steps), Is.EqualTo(new[] { "1. The Invite List", "2. Name your nominees", "3. Nomination Ceremony", "4. Outcome" }));
            Assert.That(NominationSteps.Current(steps).kind, Is.EqualTo(NominationSteps.Kind.Picker),
                "The picker is current while a story waits, so the names and Commit are pressed at once.");
            Assert.That(steps[0].Opens, Is.True, "The story waits in the tracker.");
            Assert.That(NominationSteps.LapsingOnNominate(s).Select(e => e.id), Is.EqualTo(new[] { invite.id }),
                "Committing lets it pass, and the screen says so.");
            Assert.That(EpisodeEngine.LapsingOnAdvance(s), Is.Empty, "The Advance warning never saw it: that was the silent lapse.");

            var opened = NominationSteps.Build(s, invite.id);
            Assert.That(NominationSteps.Current(opened).eventId, Is.EqualTo(invite.id), "Opened from the tracker, the story is current,");
            Assert.That(opened[1].standing, Is.EqualTo(NominationSteps.Standing.Next), "and the picker waits behind it.");

            Beat(s, "e2", "c2", "The HoH Room");
            steps = NominationSteps.Build(s);
            AssertWellFormed(steps, "Player HoH with two stories");
            Assert.That(steps[0].title, Is.EqualTo(NominationSteps.FoldedTitle), "Two stories fold into the one slot a picker leaves.");

            s.nominees = s.contestants.Where(c => !c.isPlayer).Take(2).Select(c => c.id).ToList();
            s.houseEvents.ForEach(e => Answer(e, "not-now"));
            steps = NominationSteps.Build(s);
            AssertWellFormed(steps, "Player HoH after the names");
            Assert.That(steps.Single(step => step.kind == NominationSteps.Kind.Picker).standing, Is.EqualTo(NominationSteps.Standing.Done));
            Assert.That(NominationSteps.Current(steps).kind, Is.EqualTo(NominationSteps.Kind.Outcome));
            Assert.That(NominationSteps.LapsingOnNominate(s), Is.Empty, "Nothing is left to commit.");
        }

        [Test]
        public void AHouseguestOutOfTheHouseIsShownNoStoryAndNoPicker()
        {
            var s = Nomination();
            Beat(s, "e1", "c1", "The Lobby");
            s.Find(s.playerId).status = ContestantStatus.Evicted;
            var steps = NominationSteps.Build(s);
            AssertWellFormed(steps, "Watching");
            Assert.That(steps.Select(step => step.kind), Is.EqualTo(new[]
                { NominationSteps.Kind.HeadOfHousehold, NominationSteps.Kind.Ceremony, NominationSteps.Kind.Outcome }));
            Assert.That(NominationSteps.Build(Nomination(), null).Count, Is.EqualTo(3));
            s.phase = EpisodePhase.VetoSelection;
            Assert.That(NominationSteps.Build(s), Is.Empty, "Only the nomination has these steps.");
        }

        /// <summary>
        /// Every mix of stories the tracker can meet - none to four, open or settled, a player or a
        /// houseguest at the head of the house, before and after the names, with and without a beat
        /// opened from it - builds three or four steps with one current.
        /// </summary>
        [Test]
        public void EveryWeekBuildsThreeOrFourStepsWithOneCurrent()
        {
            foreach (bool playerHoh in new[] { false, true })
            foreach (bool named in new[] { false, true })
            for (int stories = 0; stories <= 4; stories++)
            for (int settled = 0; settled <= stories; settled++)
            {
                var s = Nomination(playerHoh);
                var beats = Enumerable.Range(0, stories).Select(i => Beat(s, "e" + i, "c" + i, "Story " + i)).ToList();
                for (int i = 0; i < settled; i++) Answer(beats[i], i % 2 == 0 ? "listen" : "not-now");
                if (named) s.nominees = s.contestants.Where(c => !c.isPlayer && c.id != s.hohId).Take(2).Select(c => c.id).ToList();
                string where = (playerHoh ? "Player" : "Houseguest") + " HoH, " + (named ? "named, " : "") + stories + " stories, " + settled + " settled";
                AssertWellFormed(NominationSteps.Build(s), where);
                foreach (var beat in beats.Where(beat => !beat.resolved))
                {
                    var opened = NominationSteps.Build(s, beat.id);
                    AssertWellFormed(opened, where + ", " + beat.id + " opened");
                    Assert.That(NominationSteps.Current(opened).eventId, Is.EqualTo(beat.id), where + ": the opened beat is current.");
                }
            }
        }

        /// <summary>
        /// A real week: the engine's own Lobby, cast at the Head of Household's crowning in a season
        /// played to its nomination with the story off, is the tracker's step under the arc's name.
        /// </summary>
        [Test]
        public void TheEnginesLobbyIsAStepUnderItsOwnName()
        {
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 60 && fixture == null; seed++)
            {
                var engine = new EpisodeEngine(ContentCatalog.Create(seed));
                for (int guard = 0; guard < 80; guard++)
                {
                    var current = engine.Snapshot;
                    if (current.phase == EpisodePhase.Nomination && current.nominees.Count == 0
                        && current.hohId != current.playerId && current.Find(current.playerId).status == ContestantStatus.Active)
                    {
                        EpisodeEngine.EnableStory(current, current.week);
                        if (EpisodeEngine.StartStory(current, "the-lobby", StoryAnchors.HohCrowned)
                            && EpisodeEngine.OpenStoryBeats(current).Count == 1) fixture = current;
                        break;
                    }
                    if (current.phase == EpisodePhase.Finished) break;
                    Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(current)).accepted, Is.True);
                }
            }
            Assert.That(fixture, Is.Not.Null, "No bounded legal fixture with The Lobby found.");
            var steps = NominationSteps.Build(fixture);
            AssertWellFormed(steps, "The engine's Lobby");
            var lobby = steps.Single(step => step.kind == NominationSteps.Kind.Story);
            Assert.That(lobby.title, Is.EqualTo("The Lobby"));
            Assert.That(lobby.standing, Is.EqualTo(NominationSteps.Standing.Current));
            Assert.That(EpisodeEngine.LapsingOnAdvance(fixture).Select(e => e.id), Is.EqualTo(new[] { lobby.eventId }),
                "Continue episode lets the current step's beat pass, and the footer says so.");

            // Letting it pass is what the tracker then says.
            var after = new EpisodeEngine(fixture).Apply(EpisodeEngineTests.Command(fixture, EpisodeCommandKind.Advance));
            Assert.That(after.accepted, Is.True, after.reason);
            var named = NominationSteps.Build(after.state);
            AssertWellFormed(named, "After the Lobby lapsed");
            Assert.That(named.Single(step => step.title == "The Lobby").standing, Is.EqualTo(NominationSteps.Standing.LetPass));
        }
    }
}
