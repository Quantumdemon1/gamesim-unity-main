using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The main menu's season card on a save the store itself wrote (change 10): the look at its
    /// header gives the season's week and houseguests, changes not one byte nor the file's time,
    /// and leaves the save as loadable as it was; a held save gives the plain card. And the career
    /// strip's figures, labelled in words. EditMode only: the save store is not in the Unity-free suite.
    /// </summary>
    public sealed class MainMenuSeasonCardSaveTests
    {
        private const string Prefix = "GamesimMenuSeasonCard-";
        private string folder;

        [SetUp]
        public void MakeAFolderOfItsOwn()
        {
            folder = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
        }

        [TearDown]
        public void RemoveOnlyThatFolder()
        {
            if (string.IsNullOrEmpty(folder)) return;
            var resolved = Path.GetFullPath(folder);
            Assert.That(Path.GetFileName(resolved), Does.StartWith(Prefix));
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }

        private string Saved(EpisodeState state)
        {
            string path = Path.Combine(folder, "episode.json");
            new EpisodeSaveStore(path).Save(state);
            return path;
        }

        [Test]
        public void ARealSave_GivesItsWeekAndHouseguests_AndIsByteEqualAfterTheLook()
        {
            var state = ContentCatalog.Create(17);
            string path = Saved(state);
            var bytes = File.ReadAllBytes(path);
            var written = File.GetLastWriteTimeUtc(path);
            DateTime before = DateTime.UtcNow;

            var header = SaveHeader.Peek(path);

            Assert.That(header.HasFacts, Is.True);
            Assert.That(header.Week, Is.EqualTo(state.week));
            Assert.That(header.Left, Is.EqualTo(state.contestants.Count(c => c.status == ContestantStatus.Active)));
            Assert.That(header.SavedUtc, Is.Not.Null);
            Assert.That(header.SavedUtc.Value, Is.InRange(before.AddMinutes(-5), before.AddMinutes(1)), "The envelope's savedAt.");
            Assert.That(SeasonCardWords.Facts(header, header.SavedUtc.Value),
                Is.EqualTo("Week " + state.week + " · " + header.Left + " houseguests left · saved just now"));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes), "The look changes no byte,");
            Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(written), "nor the file's time,");
            Assert.That(new EpisodeSaveStore(path).TryLoad(out var loaded, out var message), Is.True, message);
            Assert.That(loaded.sessionId, Is.EqualTo(state.sessionId), "and the save loads as it did.");
        }

        [Test]
        public void ARealSaveHeldByAnotherProgram_GivesThePlainCard_AndIsByteEqualAfter()
        {
            string path = Saved(ContentCatalog.Create(18));
            var bytes = File.ReadAllBytes(path);
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                SaveHeader held = null;
                Assert.DoesNotThrow(() => held = SaveHeader.Peek(path));
                Assert.That(held.HasFacts, Is.False);
                Assert.That(held.Held, Is.True);
                Assert.That(SeasonCardWords.Facts(held, DateTime.UtcNow), Is.Null, "The card shows its heading alone.");
            }
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
            Assert.That(SaveHeader.Peek(path).HasFacts, Is.True, "Let go, the card has its facts again.");
        }

        [Test]
        public void ARealSaveAnotherProgramIsOnlyReading_StillGivesItsFacts()
        {
            string path = Saved(ContentCatalog.Create(19));
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.That(SaveHeader.Peek(path).HasFacts, Is.True, "A reader that shares reading does not hide the facts.");
        }

        [Test]
        public void TheBackupTheStoreRotated_IsLookedAtTheSameWay()
        {
            var first = ContentCatalog.Create(20);
            string path = Saved(first);
            new EpisodeSaveStore(path).Save(ContentCatalog.Create(21));
            Assert.That(File.Exists(path + ".backup"), Is.True, "The second save set the first aside as the backup.");
            var backup = SaveHeader.Peek(path + ".backup");
            Assert.That(backup.Week, Is.EqualTo(first.week));
            Assert.That(backup.Left, Is.EqualTo(first.contestants.Count(c => c.status == ContestantStatus.Active)));
        }

        [Test]
        public void TheCareerStrip_LabelsEachFigureInWords()
        {
            Assert.That(CareerStrip.Of(null), Is.Empty);
            Assert.That(CareerStrip.Of(CareerSummary.Of(new CareerRecord())), Is.Empty, "No finished season, no strip.");

            var record = new CareerRecord();
            record.seasons.Add(new CareerSeason { sessionId = "a", placement = 1, outcome = "Winner", hohWins = 2, vetoWins = 1 });
            record.seasons.Add(new CareerSeason { sessionId = "b", placement = 4, outcome = "Jury", hohWins = 1, vetoWins = 0 });
            record.seasons.Add(new CareerSeason { sessionId = "c", placement = 9, outcome = "Pre-jury", hohWins = 0, vetoWins = 0 });
            var stats = CareerStrip.Of(CareerSummary.Of(record));
            var pairs = stats.Select(stat => stat.Value + " / " + stat.Label).ToList();
            Assert.That(pairs, Is.EqualTo(new List<string>
            {
                "3 / seasons", "1 / win", "4th / median finish", "1st / best finish",
                "3 / Head of Household wins", "1 / Power of Veto win",
            }));

            var one = new CareerRecord();
            one.seasons.Add(new CareerSeason { sessionId = "d", placement = 2, outcome = "Jury", hohWins = 1, vetoWins = 2 });
            Assert.That(CareerStrip.Of(CareerSummary.Of(one)).Select(stat => stat.Label).ToList(), Is.EqualTo(new List<string>
            {
                "season", "wins", "median finish", "best finish", "Head of Household win", "Power of Veto wins",
            }));
        }
    }
}
