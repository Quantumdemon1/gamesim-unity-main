using System;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Threads (plan 31): a season's stories seeded from its cast at the first eviction nights, told in
    /// chapters aimed at their people, and ended at a fixed point that reads the season - or at a stated
    /// end. StorySeasonTests.ThreadsReport sweeps whole seasons.
    /// </summary>
    public sealed class StoryThreadTests
    {
        private static EpisodeState Apply(EpisodeState s, EpisodeCommand c)
        {
            var result = new EpisodeEngine(s).Apply(c);
            Assert.That(result.accepted, Is.True, c.kind + ": " + result.reason);
            return result.state;
        }

        /// <summary>The first eviction settled, its night still to come, with the story switched on.</summary>
        private static EpisodeState BeforeTheFirstEvictionNight(uint seed = 31, int rules = StoryRules.Current)
        {
            var engine = new EpisodeEngine(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed));
            bool Night(EpisodeState x) => x.week == 1 && x.phase == EpisodePhase.Eviction && x.evictionResolved;
            for (int i = 0; i < 800 && !Night(engine.Snapshot); i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var s = engine.Snapshot;
            Assert.That(Night(s), Is.True, "The fixture reaches its moment.");
            Assert.That(s.Find(s.playerId).status, Is.EqualTo(ContestantStatus.Active), "The fixture's player survived the first eviction.");
            EpisodeEngine.EnableStory(s, s.week);
            s.story.rulesVersion = rules;
            return s;
        }

        /// <summary>Plays through eviction night: the diary it may offer, then the Advance that runs its anchor.</summary>
        private static EpisodeState ThroughEvictionNight(EpisodeState s)
        {
            for (int i = 0; i < 6 && s.phase != EpisodePhase.Social; i++) s = Apply(s, EpisodeEngineTests.NextCommand(s));
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.Social), "Eviction night has passed.");
            return s;
        }

        private static void Score(EpisodeState s, string from, string to, double value) =>
            s.relationships.First(r => r.fromId == from && r.toId == to).score = value;

        private static void Both(EpisodeState s, string a, string b, double value) { Score(s, a, b, value); Score(s, b, a, value); }

        /// <summary>A house with a clear friend, a clear rival and an alliance of three: who each thread is about.</summary>
        private static (string friend, string rival, string ally1, string ally2) Cast(EpisodeState s)
        {
            var npcs = s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            foreach (var npc in npcs)
            {
                Both(s, s.playerId, npc.id, 0);
                Grudges.Ease(s, npc.id, s.playerId, 100);
            }
            foreach (var alliance in s.alliances) alliance.active = false;
            string friend = npcs[0].id, rival = npcs[1].id, ally1 = npcs[2].id, ally2 = npcs[3].id;
            Both(s, s.playerId, friend, 30);
            Grudges.Add(s, rival, s.playerId, 60, GrudgeCauses.Story);
            Both(s, s.playerId, ally1, 10); Both(s, s.playerId, ally2, 8);
            s.alliances.Add(new AllianceState { id = "alliance-three", name = "The Three", active = true, members = new[] { s.playerId, ally1, ally2 }.ToList() });
            return (friend, rival, ally1, ally2);
        }

        private static StorylineState Thread(EpisodeState s, string arcId) => s.storylines.Single(x => x.templateId == arcId);

        private static string Role(StorylineState cycle, string role) => cycle.cast.Single(r => r.role == role).contestantId;

        [Test]
        public void TheFirstEvictionNightSeedsABondARivalryAndTheNumbers()
        {
            var s = BeforeTheFirstEvictionNight();
            var (friend, rival, ally1, ally2) = Cast(s);
            s = ThroughEvictionNight(s);

            var views = EpisodeEngine.Threads(s);
            Assert.That(views.Select(v => v.kind), Is.EquivalentTo(new[] { ThreadKinds.Bond, ThreadKinds.Rivalry, ThreadKinds.Numbers }));
            Assert.That(Role(Thread(s, "thread-bond"), "FRIEND"), Is.EqualTo(friend), "The bond is your warmest houseguest.");
            Assert.That(Role(Thread(s, "thread-rivalry"), "RIVAL"), Is.EqualTo(rival), "The rivalry is whoever holds the most against you.");
            Assert.That(new[] { Role(Thread(s, "thread-numbers"), "ALLY1"), Role(Thread(s, "thread-numbers"), "ALLY2") }, Is.EquivalentTo(new[] { ally1, ally2 }),
                "The numbers are your own alliance.");
            Assert.That(views.All(v => v.ending == null), Is.True, "They have only begun.");
            var lines = s.events.Where(e => e.kind == StoryLog.Thread).Select(e => e.text).ToList();
            Assert.That(lines, Has.Some.StartsWith("Your bond with " + s.Find(friend).name + ": "), "Each begins with a line that says who it is about.");
            Assert.That(s.events.Where(e => e.kind == StoryLog.Thread).All(e => e.audienceIds.SequenceEqual(new[] { s.playerId })), Is.True,
                "A thread's lines are the player's alone.");
        }

        [Test]
        public void TheBondAndTheRivalryAreNeverTheSamePerson()
        {
            var s = BeforeTheFirstEvictionNight();
            var npcs = s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            foreach (var npc in npcs) { Grudges.Ease(s, npc.id, s.playerId, 100); Both(s, s.playerId, npc.id, -20); }
            // Everybody is cold; one is less cold than the rest - the bond - and nobody holds a grudge.
            Both(s, s.playerId, npcs[0].id, -5);
            s = ThroughEvictionNight(s);
            string friend = Role(Thread(s, "thread-bond"), "FRIEND");
            Assert.That(friend, Is.EqualTo(npcs[0].id));
            Assert.That(Role(Thread(s, "thread-rivalry"), "RIVAL"), Is.Not.EqualTo(friend));
        }

        [Test]
        public void ASeasonOnTheRulesBeforeThreadsSeedsNone()
        {
            var s = BeforeTheFirstEvictionNight(rules: StoryRules.Reach);
            Cast(s);
            s = ThroughEvictionNight(s);
            Assert.That(EpisodeEngine.Threads(s), Is.Empty, "Threads belong to the threads rules.");
        }

        [Test]
        public void AThreadsChapterIsAimedAtItsPersonAndRecordsHowItWent()
        {
            var s = BeforeTheFirstEvictionNight();
            var (friend, _, _, _) = Cast(s);
            // Somebody warmer towards you than the friend, who would be Their Word's pick left to itself.
            var other = s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).Last().id;
            Score(s, other, s.playerId, 40); Score(s, s.playerId, other, -30);
            // What Know Them would teach is already known, so the bond's first chapter is their word.
            foreach (var fact in Lore.FactsOf(s, friend).Take(1)) Lore.Learn(s, fact.id);
            s = ThroughEvictionNight(s);

            var bond = Thread(s, "thread-bond");
            var chapter = EpisodeEngine.Threads(s).Single(v => v.kind == ThreadKinds.Bond).chapters.Single();
            Assert.That(chapter.arcId, Is.EqualTo("their-word"));
            Assert.That(chapter.result, Is.EqualTo(ThreadChapterResults.Open));
            var play = s.storylines.Single(x => x.id == chapter.cycleId);
            Assert.That(play.cast.Single(r => r.role == "FRIEND").contestantId, Is.EqualTo(friend),
                "The chapter is aimed at the thread's person, not whoever the play would pick.");

            // The friend gives their word, and the chapter went the thread's way.
            s.promises.Add(new PromiseState { id = "promise-fixture", fromId = friend, toId = s.playerId, kind = PromiseKind.Safety,
                status = PromiseStatus.Active, week = s.week, expiresWeek = s.week + 1 });
            var answer = EpisodeEngineTests.Command(s, EpisodeCommandKind.ProgressStoryline);
            answer.targetId = s.houseEvents.Single(e => e.cycleId == play.id && !e.resolved).id;
            answer.secondTargetId = PlayOptions.TakeItOn;
            s = Apply(s, answer);
            Assert.That(s.storylines.Single(x => x.id == play.id).endingId, Is.EqualTo(PlayEndings.Won));
            // The thread reads it at its next anchor.
            for (int i = 0; i < 20 && EpisodeEngine.Threads(s).Single(v => v.kind == ThreadKinds.Bond).chapters.First().result == ThreadChapterResults.Open; i++)
                s = Apply(s, EpisodeEngineTests.NextCommand(s));
            Assert.That(EpisodeEngine.Threads(s).Single(v => v.kind == ThreadKinds.Bond).chapters.First().result, Is.EqualTo(ThreadChapterResults.Landed));
            Assert.That(EpisodeEngine.ChapterLanded(new StoryCycle(bond = Thread(s, "thread-bond"), StoryCatalog.Find("thread-bond")), "their-word"), Is.True);
        }

        [Test]
        public void TheRivalryEndsWhenOneOfYouLeaves([Values(true, false)] bool madePeace)
        {
            var s = BeforeTheFirstEvictionNight();
            var (_, rival, _, _) = Cast(s);
            s = ThroughEvictionNight(s);
            var rules = StoryCatalog.Find("thread-rivalry");
            var thread = new StoryCycle(Thread(s, "thread-rivalry"), rules);
            if (madePeace)
                thread.record.path.Add(new StoryStepState { beatId = "chapter:settle-it", optionId = "story-x", result = StoryResults.Success, week = s.week });
            Assert.That(rules.thread.climax(new StoryContext(s, StoryAnchors.EvictionNight), thread), Is.Null, "Both still here: it goes on.");
            s.Find(rival).status = ContestantStatus.Jury;
            Assert.That(rules.thread.climax(new StoryContext(s, StoryAnchors.EvictionNight), thread),
                Is.EqualTo(madePeace ? ThreadEndings.MadePeace : ThreadEndings.Won), "The rival left first.");
            Assert.That(rules.thread.onPlayerExit, Is.EqualTo(ThreadEndings.Lost), "And had you left first, you lost it.");
        }

        [Test]
        public void TheBondEndsAtTheFinaleWithTheFriendsVote([Values("for", "against", "final")] string how)
        {
            var s = BeforeTheFirstEvictionNight();
            var (friend, _, _, _) = Cast(s);
            s = ThroughEvictionNight(s);
            var rules = StoryCatalog.Find("thread-bond");
            var thread = new StoryCycle(Thread(s, "thread-bond"), rules);
            var somebody = s.Active.First(c => !c.isPlayer && c.id != friend).id;
            if (how == "final") s.Find(friend).status = ContestantStatus.RunnerUp;
            else
            {
                s.Find(friend).status = ContestantStatus.Jury;
                s.votes.Add(new VoteState { voterId = friend, targetId = how == "for" ? s.playerId : somebody });
            }
            Assert.That(rules.thread.finale(new StoryContext(s, null), thread),
                Is.EqualTo(how == "for" ? ThreadEndings.ForYou : how == "against" ? ThreadEndings.AgainstYou : ThreadEndings.ToTheEnd));
            Assert.That(rules.thread.good, Does.Contain(ThreadEndings.ForYou).And.Contain(ThreadEndings.ToTheEnd).And.Not.Contain(ThreadEndings.AgainstYou));
        }

        [Test]
        public void TheNumbersClimaxAtTheFinalFour([Values(true, false)] bool held)
        {
            var s = BeforeTheFirstEvictionNight();
            var (friend, rival, ally1, ally2) = Cast(s);
            s = ThroughEvictionNight(s);
            var rules = StoryCatalog.Find("thread-numbers");
            var thread = new StoryCycle(Thread(s, "thread-numbers"), rules);
            Assert.That(rules.thread.climax(new StoryContext(s, StoryAnchors.EvictionNight), thread), Is.Null, "Eight in the house: it goes on.");
            // Down to four: the player, the friend, the rival, and one ally - or none.
            foreach (var npc in s.Active.Where(c => !c.isPlayer && c.id != friend && c.id != rival && c.id != ally1).ToList()) npc.status = ContestantStatus.Jury;
            if (!held) s.alliances.Single(a => a.id == "alliance-three").active = false;
            Assert.That(rules.thread.climax(new StoryContext(s, StoryAnchors.EvictionNight), thread), Is.EqualTo(held ? ThreadEndings.Held : ThreadEndings.Broken));
        }

        [Test]
        public void TheClimaxComesAtItsPointWhateverChapterIsUnderWay()
        {
            var s = BeforeTheFirstEvictionNight();
            var (friend, rival, ally1, _) = Cast(s);
            s = ThroughEvictionNight(s);
            // A chapter of the numbers under way, and one that runs to the end of the season: the record
            // points at a cycle that will not finish before the finale (the bond itself will do).
            var numbers = Thread(s, "thread-numbers");
            numbers.path.Add(new StoryStepState { beatId = "chapter:their-word", optionId = Thread(s, "thread-bond").id,
                result = StoryResults.Plain, week = s.week });
            // The house is down to four: the player, the friend, the rival and an ally.
            foreach (var npc in s.Active.Where(c => !c.isPlayer && c.id != friend && c.id != rival && c.id != ally1).ToList()) npc.status = ContestantStatus.Jury;
            for (int i = 0; i < 60 && StorylineStatus.Running(Thread(s, "thread-numbers").status); i++)
                s = Apply(s, EpisodeEngineTests.NextCommand(s));
            Assert.That(Thread(s, "thread-numbers").endingId, Is.EqualTo(ThreadEndings.Held),
                "The final four is the numbers' climax, chapter or no chapter.");
            Assert.That(s.Active.Count(), Is.EqualTo(4), "It came at the next anchor, with four still in the house - not at the finale.");
        }

        [Test]
        public void AThreadOutlivesTheLegacyAbandonRule()
        {
            var s = BeforeTheFirstEvictionNight();
            Cast(s);
            s = ThroughEvictionNight(s);
            var bond = Thread(s, "thread-bond");
            s.week += 6;
            Storylines.AbandonStale(s);
            Assert.That(StorylineStatus.Running(bond.status), Is.True, "A thread has no card of its own, and its own rules end it.");
        }

        [Test]
        public void EveryThreadReachesItsClimaxOrAStatedEnd()
        {
            var known = new[]
            {
                ThreadEndings.ToTheEnd, ThreadEndings.ForYou, ThreadEndings.AgainstYou, ThreadEndings.Parted, ThreadEndings.MadePeace, ThreadEndings.Won,
                ThreadEndings.Lost, ThreadEndings.Held, ThreadEndings.Broken, ThreadEndings.Faded, ThreadEndings.LeftHouse,
            };
            int threads = 0;
            for (uint seed = 1; seed <= 8; seed++)
            {
                var run = StorySeasonTests.Play(StorySeasonTests.StorySeason(seed * 7 + 3, 8), (int)seed);
                foreach (var view in EpisodeEngine.Threads(run.final))
                {
                    threads++;
                    Assert.That(view.ending, Is.Not.Null, "Seed " + seed + ": " + view.label + " was still running when the season ended.");
                    Assert.That(known, Does.Contain(view.ending), "Seed " + seed + ": " + view.label + " ended as " + view.ending + ", which is no thread's ending.");
                    Assert.That(view.outcome, Is.Not.Null.And.Not.Empty, "Seed " + seed + ": " + view.label + " says how it ended.");
                }
            }
            Assert.That(threads, Is.GreaterThan(8), "The seasons had threads to end.");
        }
    }
}
