using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Whole seasons played with the story system on: every command goes through the engine's own
    /// validation, so a story that ever writes an invalid state, strands a beat, or wedges the
    /// week fails here. The driver answers story beats, spends social actions to open conversation
    /// beats, and otherwise plays the season the way <see cref="EpisodeEngineTests.NextCommand"/> does.
    /// </summary>
    public sealed class StorySeasonTests
    {
        internal static EpisodeState StorySeason(uint seed, int houseSize = 8,
            CastTemplates.Roster roster = CastTemplates.Roster.Regular)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = houseSize, Roster = roster }, seed);
            EpisodeEngine.EnableStory(state);
            return state;
        }

        internal sealed class Run
        {
            public EpisodeState final;
            public readonly List<string> arcs = new List<string>();
            public int beatsAnswered, socialActions;
        }

        private static readonly EpisodeCommandKind[] Verbs =
        {
            EpisodeCommandKind.Talk, EpisodeCommandKind.SmallTalk, EpisodeCommandKind.PersonalChat,
            EpisodeCommandKind.RelationshipBuilding, EpisodeCommandKind.DiscussGame, EpisodeCommandKind.VentAbout,
            EpisodeCommandKind.ShareSecret,
        };

        /// <summary>A number from the state and a salt, so each season plays differently and replays identically.</summary>
        private static int Mix(EpisodeState s, int salt, int purpose) =>
            (int)(SeededRandom.HashSeed(salt + ":" + s.revision + ":" + purpose) % 1000);

        internal static EpisodeCommand StoryNext(EpisodeState s, int salt)
        {
            var player = s.Find(s.playerId);
            bool active = player.status == ContestantStatus.Active;
            bool socialTime = s.phase == EpisodePhase.Social || s.phase == EpisodePhase.Campaign;
            bool actionsLeft = EpisodeEngine.SocialActionsSpent(s) < EpisodeEngine.SocialActionBudget(s);

            if (active && EpisodeEngine.IntroductionsOpen(s) && !s.openingBeatsSeen.Contains(OpeningBeat.MeetAndGreet))
            {
                var mark = EpisodeEngineTests.Command(s, EpisodeCommandKind.MarkOpeningBeat);
                mark.targetId = OpeningBeat.MeetAndGreet;
                return mark;
            }

            if (active && Mix(s, salt, 1) < 800)
            {
                foreach (var beat in EpisodeEngine.OpenStoryBeats(s))
                {
                    var options = beat.choices.Where(c => !c.locked && (!c.costsAction || (socialTime && actionsLeft))
                                                          && (!c.pickPerson || c.eligibleIds.Any(id => s.Find(id)?.status == ContestantStatus.Active))).ToList();
                    if (options.Count == 0) continue;
                    var choice = options[Mix(s, salt, 2) % options.Count];
                    var answer = EpisodeEngineTests.Command(s, EpisodeCommandKind.ProgressStoryline);
                    answer.targetId = beat.id;
                    answer.secondTargetId = choice.optionId;
                    if (choice.pickPerson)
                    {
                        var eligible = choice.eligibleIds.Where(id => s.Find(id)?.status == ContestantStatus.Active).ToList();
                        answer.text = eligible[Mix(s, salt, 3) % eligible.Count];
                    }
                    return answer;
                }
            }

            if (active && s.phase == EpisodePhase.Social && s.pendingDiary == null && Mix(s, salt, 8) < 60)
            {
                var npcs = s.Active.Where(c => !c.isPlayer).ToList();
                if (npcs.Count >= 2)
                {
                    var first = npcs[Mix(s, salt, 9) % npcs.Count];
                    var second = npcs.Where(n => n.id != first.id).ToList()[Mix(s, salt, 10) % (npcs.Count - 1)];
                    if (EpisodeEngine.ProximityOpen(s, first.id, second.id))
                    {
                        var walk = EpisodeEngineTests.Command(s, EpisodeCommandKind.WitnessProximity);
                        walk.targetId = first.id; walk.secondTargetId = second.id; walk.text = "the kitchen";
                        return walk;
                    }
                }
            }

            if (active && socialTime && actionsLeft && s.pendingDiary == null && Mix(s, salt, 11) < 120)
            {
                // Commitments, so broken words, reckonings and alliance stories have something to stand on.
                var ally = s.Active.Where(c => !c.isPlayer && !s.Allied(s.playerId, c.id) && s.Score(c.id, s.playerId) >= 8)
                    .OrderBy(c => c.id, StringComparer.Ordinal).FirstOrDefault();
                if (ally != null && Mix(s, salt, 12) < 500)
                {
                    var form = EpisodeEngineTests.Command(s, EpisodeCommandKind.FormAlliance);
                    form.targetId = ally.id;
                    return form;
                }
                var promised = s.Active.Where(c => !c.isPlayer && !s.promises.Any(p => p.status == PromiseStatus.Active && p.fromId == s.playerId
                                                                                        && p.toId == c.id && p.kind == PromiseKind.Safety))
                    .OrderBy(c => c.id, StringComparer.Ordinal).FirstOrDefault();
                if (promised != null)
                {
                    var promise = EpisodeEngineTests.Command(s, EpisodeCommandKind.PromiseSafety);
                    promise.targetId = promised.id;
                    return promise;
                }
            }

            if (active && socialTime && actionsLeft && s.pendingDiary == null && Mix(s, salt, 4) < 450)
            {
                var npcs = s.Active.Where(c => !c.isPlayer).ToList();
                if (npcs.Count >= 2)
                {
                    var verb = Verbs[Mix(s, salt, 5) % Verbs.Length];
                    var talk = EpisodeEngineTests.Command(s, verb);
                    var target = npcs[Mix(s, salt, 6) % npcs.Count];
                    talk.targetId = target.id;
                    if (verb == EpisodeCommandKind.VentAbout)
                    {
                        var others = npcs.Where(n => n.id != target.id).ToList();
                        talk.secondTargetId = others[Mix(s, salt, 7) % others.Count].id;
                    }
                    return talk;
                }
            }

            var next = EpisodeEngineTests.NextCommand(s);
            // A player production removed is not a juror, and has no vote to cast.
            if (next.kind == EpisodeCommandKind.CastVote && s.phase == EpisodePhase.Jury && player.status == ContestantStatus.Expelled)
                next.kind = EpisodeCommandKind.Advance;
            return next;
        }

        internal static Run Play(EpisodeState initial, int salt, int maxCommands = 4000)
        {
            var engine = new EpisodeEngine(initial);
            var run = new Run();
            for (int i = 0; i < maxCommands && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var s = engine.Snapshot;
                var command = StoryNext(s, salt);
                var result = engine.Apply(command);
                Assert.That(result.accepted, Is.True, "salt " + salt + ", week " + s.week + " " + s.phase + " (" + s.evictionStage + "): "
                    + command.kind + " " + command.targetId + " " + command.secondTargetId + ": " + result.reason);
                if (command.kind == EpisodeCommandKind.ProgressStoryline) run.beatsAnswered++;
                if (Array.IndexOf(Verbs, command.kind) >= 0) run.socialActions++;
            }
            run.final = engine.Snapshot;
            run.arcs.AddRange(run.final.storylines.Where(x => x.beatId != null).Select(x => x.templateId));
            Assert.That(run.final.phase, Is.EqualTo(EpisodePhase.Finished), "salt " + salt + " did not finish.");
            Assert.That(EpisodeValidation.TryValidate(run.final, out var error), Is.True, error);
            return run;
        }

#if !UNITY_5_3_OR_NEWER
        // For the Unity-free run: Unity's batch runner executes an [Explicit] test.
        /// <summary>A report, not a check: which arcs a sweep of seasons reaches, how often, and how they end.</summary>
        [Test, Explicit("Diagnostic: prints arc coverage for tuning.")]
        public void ArcCoverageReport()
        {
            var counts = new Dictionary<string, int>();
            var endings = new Dictionary<string, int>();
            int beats = 0, seasons = 0, expelled = 0, removals = 0;
            foreach (var size in new[] { 6, 8, 12, 16 })
                for (uint seed = 1; seed <= 20; seed++)
                {
                    var run = Play(StorySeason(seed * 13 + (uint)size, size), (int)seed);
                    seasons++;
                    beats += run.beatsAnswered;
                    foreach (var cycle in run.final.storylines.Where(x => x.beatId != null))
                    {
                        counts[cycle.templateId] = counts.TryGetValue(cycle.templateId, out var n) ? n + 1 : 1;
                        string key = cycle.templateId + " -> " + cycle.endingId;
                        endings[key] = endings.TryGetValue(key, out var m) ? m + 1 : 1;
                    }
                    if (run.final.Find(run.final.playerId).status == ContestantStatus.Expelled) expelled++;
                    removals += run.final.story.removals.Count;
                }
            TestContext.WriteLine("seasons " + seasons + ", beats answered " + beats + ", removals " + removals + " (player " + expelled + ")");
            foreach (var arc in StoryCatalog.All.OrderBy(a => a.id))
                TestContext.WriteLine((counts.TryGetValue(arc.id, out var n) ? n : 0).ToString().PadLeft(5) + "  " + arc.id);
            foreach (var pair in endings.OrderBy(p => p.Key)) TestContext.WriteLine(pair.Value.ToString().PadLeft(5) + "  " + pair.Key);
        }
#endif

        [Test]
        public void TheStorySystemIsOffUntilSomethingSwitchesItOn()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            Assert.That(state.story, Is.Not.Null);
            Assert.That(state.story.rulesStartWeek, Is.Zero);
            Assert.That(EpisodeEngine.StoryOn(state), Is.False);
            EpisodeEngine.EnableStory(state);
            Assert.That(EpisodeEngine.StoryOn(state), Is.True);
            Assert.That(state.story.rulesVersion, Is.EqualTo(StoryRules.Current));
            Assert.That(state.story.lore.Count, Is.EqualTo(state.contestants.Count(c => !c.isPlayer)));
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
        }

        [Test]
        public void AnEmptyStoryStateReproducesEveryConsumerExactly()
        {
            var state = StorySeason(11u);
            var npcs = state.contestants.Where(c => !c.isPlayer).ToList();
            foreach (var a in npcs)
                foreach (var b in state.contestants.Where(x => x.id != a.id))
                {
                    Assert.That(StoryConsumers.NominationPreference(state, a.id, b.id), Is.EqualTo(state.Score(a.id, b.id)));
                    Assert.That(StoryConsumers.SavePreference(state, a.id, b.id), Is.EqualTo(state.Score(a.id, b.id)));
                    Assert.That(StoryConsumers.WillNotSave(state, a.id, b.id), Is.False);
                    Assert.That(StoryConsumers.VoteGrudge(state, a.id, b.id), Is.Zero);
                    Assert.That(StoryConsumers.VoteBond(state, a.id, b.id), Is.Zero);
                    Assert.That(StoryConsumers.JuryStory(state, a.id, b.id), Is.Zero);
                }
        }

        [Test]
        public void EightHouseSeasonsFinishWithEveryCommandValidated()
        {
            var arcs = new HashSet<string>();
            int beats = 0;
            for (uint seed = 1; seed <= 24; seed++)
            {
                var run = Play(StorySeason(seed), (int)seed);
                beats += run.beatsAnswered;
                foreach (var arc in run.arcs) arcs.Add(arc);
            }
            Assert.That(beats, Is.GreaterThan(24), "Seasons with the story system on should put beats in front of the player.");
            Assert.That(arcs.Count, Is.GreaterThanOrEqualTo(15), "Arcs seen: " + string.Join(", ", arcs.OrderBy(x => x)));
        }

        [Test]
        public void EveryHouseSizeAndRosterFinishes([Values(6, 12, 16)] int size, [Values(CastTemplates.Roster.Regular, CastTemplates.Roster.AllStars)] CastTemplates.Roster roster)
        {
            for (uint seed = 1; seed <= 6; seed++)
                Play(StorySeason(seed * 31, size, roster), (int)(seed * 7 + size));
        }

        [Test]
        public void TheDefaultSceneSeasonFinishesWithTheStoryOn()
        {
            for (uint seed = 0; seed < 12; seed++)
            {
                var state = ContentCatalog.Create(seed);
                EpisodeEngine.EnableStory(state);
                Play(state, (int)seed);
            }
        }

        [Test]
        public void AnsweringAStoryBeatNeverDrawsFromTheSeasonsStream()
        {
            int checkedBeats = 0;
            for (uint seed = 1; seed <= 12 && checkedBeats < 12; seed++)
            {
                var engine = new EpisodeEngine(StorySeason(seed));
                for (int i = 0; i < 1500 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
                {
                    var s = engine.Snapshot;
                    var command = StoryNext(s, (int)seed);
                    var result = engine.Apply(command);
                    Assert.That(result.accepted, Is.True, result.reason);
                    if (command.kind != EpisodeCommandKind.ProgressStoryline) continue;
                    Assert.That(result.state.randomState, Is.EqualTo(s.randomState),
                        "Answering " + command.secondTargetId + " moved the season's main stream.");
                    checkedBeats++;
                }
            }
            Assert.That(checkedBeats, Is.GreaterThan(0));
        }

        [Test]
        public void ReplayingAStorySeasonReproducesItExactly()
        {
            var a = new EpisodeEngine(StorySeason(5u));
            var b = new EpisodeEngine(StorySeason(5u));
            for (int i = 0; i < 3000 && a.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var command = StoryNext(a.Snapshot, 5);
                Assert.That(a.Apply(command).accepted, Is.True);
                Assert.That(b.Apply(command).accepted, Is.True);
                if (i % 25 == 0) b = new EpisodeEngine(b.Snapshot);
            }
            var x = a.Snapshot; var y = b.Snapshot;
            Assert.That(y.winnerId, Is.EqualTo(x.winnerId));
            Assert.That(y.randomState, Is.EqualTo(x.randomState));
            Assert.That(y.events.Select(e => e.text), Is.EqualTo(x.events.Select(e => e.text)));
            Assert.That(y.story.grudges.Select(g => g.holderId + g.targetId + g.severity), Is.EqualTo(x.story.grudges.Select(g => g.holderId + g.targetId + g.severity)));
        }
    }
}
