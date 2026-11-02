using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The All-Star histories (ALL-STAR-HISTORIES-PLAN H1b) and the save store. A fresh season on the alumni rules saves at
    /// schema 30 with its edition and receipts, loads back as it was saved with no migration, validates, and plays on
    /// exactly as the season in memory does, saved and loaded again mid-season. An All-Stars season saved before the
    /// histories (the SCH30 build's fixture, alumni rules 0) loads as it was and is never enrolled. The store's side of
    /// <see cref="AlumniHistoriesTests"/>; Unity only, because the dotnet subset compiles no Gamesim.Persistence.
    /// </summary>
    public sealed class AlumniHistoriesSaveTests
    {
        private const string Prefix = "GamesimAlumniHistoriesSave-";
        private string directory;

        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        private static JObject Payload(EpisodeState s) => JObject.FromObject(s, Serializer());
        private static string Text(JToken token) => token.ToString(Formatting.None);
        private static int Receipts(EpisodeState s) => s.relationships.SelectMany(r => r.events).Count(AlumniHistories.IsReceipt);

        [SetUp]
        public void IsolatedDirectory()
        {
            directory = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
            Assert.That(Directory.Exists(directory), Is.False);
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void RemoveOnlyThisFixtureDirectory()
        {
            if (directory == null) return;
            string resolved = Path.GetFullPath(directory);
            Assert.That(resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(resolved), Does.StartWith(Prefix));
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
            directory = null;
        }

        [TestCase(8, null)] [TestCase(12, null)] [TestCase(8, "derrick-levasseur")]
        public void AFreshSeasonOnTheAlumniRulesSavesLoadsAndPlaysOnTheSame(int size, string player)
        {
            var start = AlumniHistoriesTests.AllStars(size, 3101u, player);
            Assert.That(start.alumniRulesVersion, Is.EqualTo(AlumniRules.Latest));
            int receipts = Receipts(start);
            Assert.That(receipts, Is.GreaterThan(0));
            var store = new EpisodeSaveStore(Path.Combine(directory, "season.json"));
            store.Save(start);
            var written = JObject.Parse(File.ReadAllText(store.SavePath));
            Assert.That((int)written["state"]["schemaVersion"], Is.EqualTo(30));
            Assert.That((int)written["state"]["alumniRulesVersion"], Is.EqualTo(AlumniRules.Latest));
            Assert.That((string)written["state"]["alumniCatalogEdition"], Is.EqualTo("allstar-histories-1"));
            Assert.That(store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message ?? "", Does.Not.Contain("migrated"));
            Assert.That(Text(Payload(loaded)), Is.EqualTo(Text(Payload(start))), "The store hands the season back as it was saved.");
            Assert.That(Receipts(loaded), Is.EqualTo(receipts));
            Assert.That(EpisodeValidation.TryValidate(loaded, out string error), Is.True, error);

            var memory = new EpisodeEngine(start);
            var disk = new EpisodeEngine(loaded);
            for (int i = 0; i < 600 && memory.Snapshot.week <= 2; i++)
            {
                var command = EpisodeEngineTests.NextCommand(memory.Snapshot);
                var kept = memory.Apply(command);
                Assert.That(kept.accepted, Is.True, kept.reason);
                Assert.That(Text(Payload(disk.Apply(command).state)), Is.EqualTo(Text(Payload(kept.state))), "command " + i);
            }
            Assert.That(memory.Snapshot.week, Is.GreaterThan(2));
            store.Save(disk.Snapshot);
            Assert.That(store.TryLoad(out var again, out message), Is.True, message);
            Assert.That(Text(Payload(again)), Is.EqualTo(Text(Payload(memory.Snapshot))), "Saved and loaded in week three, the same season.");
            Assert.That(Receipts(again), Is.EqualTo(receipts), "Its past games with it.");
            Assert.That(store.TryRecoverBackup(out var recovered, out message), Is.True, message);
            Assert.That(Text(Payload(recovered)), Is.EqualTo(Text(Payload(start))), "The backup is the season's first save.");
        }

        /// <summary>The SCH30 build's All-Stars season (alumni rules 0) is an old season: it loads as it was, and nothing enrols it.</summary>
        [Test]
        public void AnAllStarsSeasonSavedBeforeTheHistoriesStaysOffThem()
        {
            string fixture = Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "OldSaves",
                "s30-mode2-allstars-w3-campaign.save.json");
            var store = new EpisodeSaveStore(Path.Combine(directory, "season.json"));
            File.Copy(fixture, store.SavePath);
            Assert.That(store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(loaded.alumniRulesVersion, Is.Zero);
            Assert.That(loaded.alumniCatalogEdition, Is.Null);
            Assert.That(Receipts(loaded), Is.Zero);
            Assert.That(AlumniHistories.Seated(loaded, AlumniEditions.Latest), Is.Not.Empty, "It seats the alumni all the same.");
            string before = Text(Payload(loaded));
            EpisodeEngine.EnableAlumniHistories(loaded);
            Assert.That(Text(Payload(loaded)), Is.EqualTo(before), "A season under way is never moved onto the alumni rules.");
        }
    }
}
