using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Dialogue rules 4 after its review (DIALOGUE-PLAN D4): the competition screen's Simulate lapses an open veto question as
    /// playing does, each cause of a refused question has its own words, and the player's own veto dry run never weighs a
    /// veto_if. Shared cases: the dotnet subset runs them.
    /// </summary>
    public sealed class DialogueRules4ReviewTests
    {
        private static EpisodeState Accepted(EpisodeEngine engine, EpisodeCommand command) => DialogueRules4Season.Accepted(engine, command);

        /// <summary>Simulate on the competition screen with a question open: accepted, the question lapses, and the veto goes as it would have.</summary>
        [Test]
        public void SimulatingTheVetoWithAQuestionOpenLapsesItAndTheVetoPlaysAsItWouldHave()
        {
            var stop = DialogueRules4Season.Stops()[0];
            string npc = stop.People[0];
            var engine = new EpisodeEngine(stop.State.Clone());
            var opened = Accepted(engine, DialogueRules4Season.Ask(stop.State, npc));
            var twin = new EpisodeEngine(opened.Clone());
            Accepted(twin, DialogueRules4Season.Choice(opened, DialogueVetoQuestion.Leave));
            var lapsed = Accepted(engine, DialogueRules2Season.Command(opened, EpisodeCommandKind.SimulateCompetition));
            var episode = DialogueRules4Season.Question(lapsed, npc);
            var cycle = DialogueRules.Cycle(lapsed, episode);
            Assert.That((cycle.status, cycle.endingId, episode.time.reason), Is.EqualTo((StorylineStatus.Completed, "lapsed", "occasion-closed")));
            Assert.That(DialogueRules.Active(lapsed), Is.Null);
            Assert.That(lapsed.competitionResolved, Is.True);
            var played = Accepted(twin, DialogueRules2Season.Command(twin.Snapshot, EpisodeCommandKind.SimulateCompetition));
            Assert.That(lapsed.randomState, Is.EqualTo(played.randomState), "The lapse draws nothing.");
            Assert.That(lapsed.vetoHolderId, Is.EqualTo(played.vetoHolderId), "And the veto is won by the same houseguest.");
        }

        /// <summary>A houseguest who is not playing in the veto is refused in those words, not as if the competition had passed.</summary>
        [Test]
        public void AskingSomebodyOutsideTheVetoIsRefusedInItsOwnWords()
        {
            foreach (var stop in DialogueRules4Season.Stops())
            {
                var s = stop.State;
                var outside = s.contestants.FirstOrDefault(c => c.status == ContestantStatus.Active && c.id != s.playerId && !s.vetoPlayers.Contains(c.id));
                if (outside == null) continue;
                Assert.That(DialogueRules.CanAskVetoQuestion(s, outside.id), Is.False);
                Assert.That(DialogueRules.CanStart(s, outside.id, out string why), Is.False);
                Assert.That(why, Is.EqualTo(DialogueRules.VetoQuestionNotAVetoPlayer));
                return;
            }
            Assert.Fail("Every stop's whole house played in the veto.");
        }

        /// <summary>A player off the block is refused in those words: only a nominee has the veto question to ask.</summary>
        [Test]
        public void APlayerOffTheBlockIsRefusedInItsOwnWords()
        {
            for (uint seed = 1; seed <= 20; seed++)
            {
                var engine = new EpisodeEngine(DialogueRules4Season.Start(seed));
                for (int step = 0; step < 600; step++)
                {
                    var s = engine.Snapshot;
                    if (s.week > 5 || s.phase == EpisodePhase.Finished || s.Find(s.playerId).status != ContestantStatus.Active) break;
                    if (s.phase == EpisodePhase.Veto && !s.competitionResolved && !s.nominees.Contains(s.playerId))
                    {
                        string npc = s.vetoPlayers.FirstOrDefault(id => id != s.playerId && s.Find(id)?.status == ContestantStatus.Active);
                        if (npc != null)
                        {
                            Assert.That(DialogueRules.CanAskVetoQuestion(s, npc), Is.False);
                            Assert.That(DialogueRules.CanStart(s, npc, out string why), Is.False);
                            Assert.That(why, Is.EqualTo(DialogueRules.VetoQuestionNotNominated));
                            return;
                        }
                    }
                    var next = EpisodeEngineTests.NextCommand(s);
                    if (next.kind == EpisodeCommandKind.Compete) next.performance = 0;
                    Accepted(engine, next);
                }
            }
            Assert.Fail("No rules-4 season of 20 met a veto competition with the player off the block.");
        }

        /// <summary>A veto_if is never the player's own word (§4.2 point 5): the dry run of the player's own veto decision does not weigh it.</summary>
        [Test]
        public void AConditionalVetoPromiseIsNeverAtStakeInThePlayersOwnVetoDecision()
        {
            var promise = DialogueRules4Season.Promises()
                .FirstOrDefault(p => !CommitmentsRead.AtStake(p.Thanked, CommitmentsRead.DecisionKinds.Veto));
            Assert.That(promise, Is.Not.Null, "A promise whose thanked twin has nothing at stake at the veto.");
            Assert.That(promise.Promised.deals.Count(d => d.type == DealKind.VetoIf && d.status == DealStatus.Active), Is.EqualTo(1));
            Assert.That(CommitmentsRead.AtStake(promise.Promised, CommitmentsRead.DecisionKinds.Veto), Is.False,
                "The promise binds its maker, never the player.");
        }
    }
}
