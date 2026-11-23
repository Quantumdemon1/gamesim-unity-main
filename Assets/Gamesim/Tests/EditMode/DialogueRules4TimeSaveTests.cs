using System;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Dialogue rules 4's conversation time through the real save store (DIALOGUE-PLAN D5c): a season saved mid-window with
    /// an applied +1 loads whole through EpisodeSaveStore with the same actions left. EditMode only (the store is the
    /// runtime's); the Mono harness runs it.
    /// </summary>
    public sealed class DialogueRules4TimeSaveTests
    {
        private string directory;

        [SetUp]
        public void MakeAPrivateFolder()
        {
            directory = Path.Combine(Path.GetTempPath(), "GamesimDialogueRules4Time-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void RemoveThePrivateFolder()
        {
            if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void ASeasonSavedWithAReceiptMidWindowLoadsWithTheSameActionsLeft()
        {
            var saved = DialogueRules4TimeTests.AfterAWarmLanding();
            Assert.That(saved.dialogue.episodes.Last().time.appliedDelta, Is.EqualTo(1));
            Assert.That(EpisodeEngine.Window(saved), Is.EqualTo(Windows.AfterVeto), "The window the +1 was won in is still open.");

            var store = new EpisodeSaveStore(Path.Combine(directory, "season.json"));
            store.Save(saved);
            Assert.That(store.TryLoad(out var loaded, out string error), Is.True, error);
            Assert.That(EpisodeValidation.TryValidate(loaded, out error), Is.True, error);
            Assert.That(DialogueRules2Season.Json(loaded), Is.EqualTo(DialogueRules2Season.Json(saved)), "The load is the season saved.");
            Assert.That(DialogueRules2Season.Remaining(loaded), Is.EqualTo(DialogueRules2Season.Remaining(saved)));
            Assert.That(EpisodeEngine.SocialActionBudget(loaded), Is.EqualTo(EpisodeEngine.SocialActionBudget(saved)));
        }
    }
}
