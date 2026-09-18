using System;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class EpisodePreparationTests
    {
        [Test]
        public void StudyIsOneAtomicPrivateActionWithOnePersistedDrawAndNoOtherEffects()
        {
            var original = ContentCatalog.Create(1701);
            original.contestants[0].traits.Add("Strategic"); // This is not source personalityTraits.
            var rng = new SeededRandom(original.randomState);
            var expected = WebStudyHouse.PlanStudy(0, "sneak-peek", null, rng.NextDouble());
            var engine = new EpisodeEngine(original);
            var command = Command(original, EpisodeCommandKind.StudyHouse, "sneak-peek");
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.randomState, Is.EqualTo(rng.State));
            Assert.That(result.state.playerStudyBonus, Is.EqualTo(expected.studyBonus));
            Assert.That(result.state.socialActions, Is.EqualTo(1));
            Assert.That(result.state.revision, Is.EqualTo(original.revision + 1));
            Assert.That(result.state.events.Last().kind, Is.EqualTo("study-house"));
            Assert.That(result.state.events.Last().audienceIds, Is.EqualTo(new[] { original.playerId }));
            Assert.That(Json(result.state.relationships), Is.EqualTo(Json(original.relationships)));
            Assert.That(Json(result.state.relationshipArcs), Is.EqualTo(Json(original.relationshipArcs)));
            Assert.That(Json(result.state.contestants), Is.EqualTo(Json(original.contestants)), "Stats, mood and stress do not change.");
            Assert.That(Json(result.state.memories), Is.EqualTo(Json(original.memories)));
            Assert.That(Json(result.state.playerPersona), Is.EqualTo(Json(original.playerPersona)));
            Assert.That(Json(result.state.jurySentiment), Is.EqualTo(Json(original.jurySentiment)));
            Assert.That(Json(result.state.promises), Is.EqualTo(Json(original.promises)));
            Assert.That(Json(result.state.loyaltyOaths), Is.EqualTo(Json(original.loyaltyOaths)));
            Assert.That(result.state.phaseEventSocialBonus, Is.Zero);
            Assert.That(result.state.phaseEventCompBonus, Is.Zero);
            var committed = Json(engine.Snapshot);
            Assert.That(engine.Apply(command).duplicate, Is.True);
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committed));
            result.state.playerStudyBonus = 99;
            original.playerStudyBonus = 99;
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committed), "Inputs and result snapshots are detached.");
        }

        [Test]
        public void RejectedStudyCommandsPreserveEverythingIncludingRandomState()
        {
            var state = ContentCatalog.Create(1702);
            var engine = new EpisodeEngine(state);
            var stale = Command(state, EpisodeCommandKind.StudyHouse, "memorize-layout"); stale.expectedRevision++;
            Reject(engine, stale);
            var actor = Command(state, EpisodeCommandKind.StudyHouse, "memorize-layout"); actor.actorId = ContentCatalog.MayaId;
            Reject(engine, actor);
            Reject(engine, Command(state, EpisodeCommandKind.StudyHouse, "forged"));
            state.socialActions = 18;
            Reject(new EpisodeEngine(state), Command(state, EpisodeCommandKind.StudyHouse, "memorize-layout"));
            state.socialActions = 0;
            state.Find(state.playerId).status = ContestantStatus.Jury;
            Reject(new EpisodeEngine(state), Command(state, EpisodeCommandKind.StudyHouse, "memorize-layout"));
            state = ContentCatalog.Create(1703);
            var evictee = state.Active.First(guest => !guest.isPlayer);
            evictee.status = ContestantStatus.Jury;
            state.pendingDiary = new DiaryPromptState { id = "diary-post_eviction-1", trigger = "post_eviction", evictedId = evictee.id, week = 1 };
            Reject(new EpisodeEngine(state), Command(state, EpisodeCommandKind.StudyHouse, "memorize-layout"));
        }

        [Test]
        public void StudyCapStillCostsOneActionAndOneDrawButCannotExceedBudget()
        {
            var state = ContentCatalog.Create(1704); state.playerStudyBonus = 5; state.socialActions = 17;
            var rng = new SeededRandom(state.randomState); rng.NextDouble();
            var engine = new EpisodeEngine(state);
            Apply(engine, EpisodeCommandKind.StudyHouse, "memorize-layout");
            Assert.That(engine.Snapshot.playerStudyBonus, Is.EqualTo(5));
            Assert.That(engine.Snapshot.socialActions, Is.EqualTo(18));
            Assert.That(engine.Snapshot.randomState, Is.EqualTo(rng.State));
            Reject(engine, Command(engine.Snapshot, EpisodeCommandKind.StudyHouse, "memorize-layout"));
        }

        [Test]
        public void PreparationSurvivesRealWeekProgressionAndStoreReloadWithoutResetOrReplay()
        {
            using var files = new StoreFixture();
            var engine = new EpisodeEngine(ContentCatalog.Create(1705));
            var study = Command(engine.Snapshot, EpisodeCommandKind.StudyHouse, "memorize-layout");
            Assert.That(engine.Apply(study).accepted, Is.True);
            files.Store.Save(engine.Snapshot);
            Assert.That(files.Store.TryLoad(out var loaded, out var message), Is.True, message);
            engine = new EpisodeEngine(loaded);
            Assert.That(engine.Apply(study).duplicate, Is.True);
            Assert.That(engine.Snapshot.playerStudyBonus, Is.EqualTo(1));
            int guard = 0;
            while (engine.Snapshot.week < 2 && guard++ < 50)
            {
                var next = EpisodeEngineTests.NextCommand(engine.Snapshot);
                var result = engine.Apply(next);
                Assert.That(result.accepted, Is.True, result.reason);
                Assert.That(result.state.playerStudyBonus, Is.EqualTo(1));
            }
            Assert.That(engine.Snapshot.week, Is.EqualTo(2));
            files.Store.Save(engine.Snapshot);
            Assert.That(files.Store.TryLoad(out loaded, out message), Is.True, message);
            Assert.That(loaded.playerStudyBonus, Is.EqualTo(1));
            Assert.That(loaded.randomState, Is.EqualTo(engine.Snapshot.randomState));
            Assert.That(loaded.schemaVersion, Is.EqualTo(9));
        }

        [TestCase(EpisodePhase.HoH)]
        [TestCase(EpisodePhase.Veto)]
        public void WeeklySimulationUsesOnlyRawStudyPlusStoredPhaseBonusWithExactDraws(EpisodePhase phase)
        {
            var initial = CompetitionFixture(phase); initial.playerStudyBonus = 5; initial.phaseEventCompBonus = 11;
            var rng = new SeededRandom(initial.randomState);
            var expected = EpisodeEngine.CompetitionPlayers(initial).Select(guest => new CompetitionScore { contestantId = guest.id,
                score = WebRules.WeightedCompetitionScore(guest.stats, "Skill", initial.nominees.Contains(guest.id), guest.isPlayer ? 16 : 0, rng.NextDouble(), 0) }).ToArray();
            var engine = new EpisodeEngine(initial);
            var command = Command(initial, EpisodeCommandKind.SimulateCompetition);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(Json(result.state.competitionScores), Is.EqualTo(Json(expected)));
            Assert.That(result.state.randomState, Is.EqualTo(rng.State));
            Assert.That(result.state.playerStudyBonus, Is.EqualTo(5));
            Assert.That(result.state.phaseEventCompBonus, Is.EqualTo(11));
            Assert.That(result.state.competitionResolved, Is.True);
            Assert.That(result.state.phase, Is.EqualTo(phase));
            string winner = expected.OrderByDescending(item => item.score).First().contestantId;
            Assert.That(phase == EpisodePhase.HoH ? result.state.hohId : result.state.vetoHolderId, Is.EqualTo(winner));
            Assert.That(phase == EpisodePhase.HoH ? result.state.Find(winner).hohWins : result.state.Find(winner).vetoWins, Is.EqualTo(1));
            Assert.That(engine.Apply(command).duplicate, Is.True);
            Reject(engine, Command(engine.Snapshot, EpisodeCommandKind.SimulateCompetition));
            Reject(engine, Command(engine.Snapshot, EpisodeCommandKind.Compete));
        }

        [Test]
        public void PlayedPrecisionModeRemainsUnaffectedAndSimulationRejectsPrecisionInput()
        {
            var withBonus = CompetitionFixture(EpisodePhase.HoH); withBonus.playerStudyBonus = 5; withBonus.phaseEventCompBonus = 1000;
            var withoutBonus = withBonus.Clone(); withoutBonus.playerStudyBonus = 0; withoutBonus.phaseEventCompBonus = 0;
            var a = new EpisodeEngine(withBonus); var b = new EpisodeEngine(withoutBonus);
            var ca = Command(withBonus, EpisodeCommandKind.Compete); ca.performance = 0.75;
            var cb = Command(withoutBonus, EpisodeCommandKind.Compete); cb.performance = 0.75;
            Assert.That(a.Apply(ca).accepted, Is.True);
            Assert.That(b.Apply(cb).accepted, Is.True);
            Assert.That(Json(a.Snapshot.competitionScores), Is.EqualTo(Json(b.Snapshot.competitionScores)));
            Assert.That(a.Snapshot.randomState, Is.EqualTo(b.Snapshot.randomState));
            var engine = new EpisodeEngine(withBonus);
            var invalid = Command(withBonus, EpisodeCommandKind.SimulateCompetition); invalid.performance = 0.75;
            Reject(engine, invalid);
            var valid = Apply(engine, EpisodeCommandKind.SimulateCompetition);
            Assert.That(valid.state.competitionScores.Single(item => item.contestantId == withBonus.playerId).score, Is.GreaterThan(1000));
            Assert.That(EpisodeValidation.TryValidate(valid.state, out var error), Is.True, error);
        }

        [Test]
        public void AllFinaleModesRejectNewSimulationAndStoredStudyCannotAffectExistingFinale()
        {
            var a = new EpisodeEngine(ContentCatalog.Create(1706));
            var seeded = ContentCatalog.Create(1706); seeded.playerStudyBonus = 5; seeded.phaseEventCompBonus = 11;
            var b = new EpisodeEngine(seeded);
            var seen = new System.Collections.Generic.HashSet<EpisodePhase>();
            for (int guard = 0; guard < 280; guard++)
            {
                var state = a.Snapshot;
                if (state.phase != EpisodePhase.HoH && state.phase != EpisodePhase.Veto)
                {
                    Reject(a, Command(state, EpisodeCommandKind.SimulateCompetition));
                    seen.Add(state.phase);
                }
                if (state.phase != EpisodePhase.Social) Reject(a, Command(state, EpisodeCommandKind.StudyHouse, "memorize-layout"));
                if (state.phase == EpisodePhase.Finished) break;
                var command = EpisodeEngineTests.NextCommand(state);
                Assert.That(a.Apply(command).accepted, Is.True);
                Assert.That(b.Apply(command).accepted, Is.True);
                Assert.That(Json(a.Snapshot.competitionScores), Is.EqualTo(Json(b.Snapshot.competitionScores)));
                Assert.That(a.Snapshot.randomState, Is.EqualTo(b.Snapshot.randomState));
                Assert.That(a.Snapshot.winnerId, Is.EqualTo(b.Snapshot.winnerId));
            }
            Assert.That(seen, Is.SupersetOf(new[] { EpisodePhase.FinalHoHPart1, EpisodePhase.FinalHoHPart2, EpisodePhase.FinalHoHPart3,
                EpisodePhase.FinalEviction, EpisodePhase.JuryQuestioning, EpisodePhase.FinalSpeeches, EpisodePhase.Jury, EpisodePhase.Finished }));
        }

        [Test]
        public void StudyValidationRequiresBoundedIntegerStateAndPreservesOldOrdinals()
        {
            foreach (int invalid in new[] { -1, 6, int.MaxValue })
            {
                var state = ContentCatalog.Create(1707); state.playerStudyBonus = invalid;
                Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False);
                Assert.Throws<InvalidDataException>(() => EpisodeSaveValidation.Validate(state));
            }
            Assert.That((int)EpisodeCommandKind.DeclineLoyalty, Is.EqualTo(19));
            Assert.That((int)EpisodeCommandKind.StudyHouse, Is.EqualTo(20));
            Assert.That((int)EpisodeCommandKind.SimulateCompetition, Is.EqualTo(21));
            Assert.That((int)EpisodePhase.Finished, Is.EqualTo(13));
            Assert.That((int)EpisodePhase.FinalSpeeches, Is.EqualTo(15));
        }

        private static EpisodeState CompetitionFixture(EpisodePhase phase)
        {
            var state = ContentCatalog.Create(1708); state.phase = phase;
            if (phase == EpisodePhase.Veto)
            {
                state.hohId = state.Active.First(guest => !guest.isPlayer).id;
                state.nominees = state.Active.Where(guest => guest.id != state.hohId).Take(2).Select(guest => guest.id).ToList();
                state.vetoPlayers = state.Active.Select(guest => guest.id).ToList();
            }
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
            return state;
        }
        private static string Json(object state) => JsonConvert.SerializeObject(state);
        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null) => new EpisodeCommand {
            id = "preparation-test-" + s.revision, expectedRevision = s.revision, expectedPhase = s.phase, actorId = s.playerId, kind = kind, targetId = target };
        private static CommandResult Apply(EpisodeEngine engine, EpisodeCommandKind kind, string target = null)
        {
            var result = engine.Apply(Command(engine.Snapshot, kind, target)); Assert.That(result.accepted, Is.True, result.reason); return result;
        }
        private static void Reject(EpisodeEngine engine, EpisodeCommand command)
        {
            string before = Json(engine.Snapshot); var result = engine.Apply(command);
            Assert.That(result.accepted || result.duplicate, Is.False, "Expected rejection: " + command.kind);
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before), result.reason);
        }
        private sealed class StoreFixture : IDisposable
        {
            private readonly string directory = Path.Combine(Path.GetTempPath(), "GamesimStudyTests-" + Guid.NewGuid().ToString("N"));
            public readonly EpisodeSaveStore Store;
            public StoreFixture() { Directory.CreateDirectory(directory); Store = new EpisodeSaveStore(Path.Combine(directory, "episode.json")); }
            public void Dispose()
            {
                var path = Path.GetFullPath(directory);
                Assert.That(Path.GetDirectoryName(path), Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)));
                Assert.That(Path.GetFileName(path), Does.StartWith("GamesimStudyTests-"));
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
        }
    }
}
