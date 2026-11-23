using System;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Dialogue rules 4 through the real save store (DIALOGUE-PLAN D4): a season saved with the veto question open, and one
    /// saved with the conditional promise standing before the veto is played, loads whole through EpisodeSaveStore and plays
    /// on exactly as the season that was never saved. EditMode only (the store is the runtime's); the Mono harness runs it.
    /// </summary>
    public sealed class DialogueRules4SaveTests
    {
        private string directory;

        [SetUp]
        public void MakeAPrivateFolder()
        {
            directory = Path.Combine(Path.GetTempPath(), "GamesimDialogueRules4-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void RemoveThePrivateFolder()
        {
            if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private EpisodeState SaveAndLoad(EpisodeState state)
        {
            var store = new EpisodeSaveStore(Path.Combine(directory, "season.json"));
            store.Save(state);
            Assert.That(store.TryLoad(out var loaded, out string error), Is.True, error);
            Assert.That(EpisodeValidation.TryValidate(loaded, out error), Is.True, error);
            Assert.That(DialogueRules2Season.Json(loaded), Is.EqualTo(DialogueRules2Season.Json(state)), "The load is the season saved.");
            return loaded;
        }

        [Test]
        public void ASeasonSavedMidQuestionLoadsAndTheQuestionGoesOn()
        {
            var promise = DialogueRules4Season.Promises().FirstOrDefault();
            Assert.That(promise, Is.Not.Null, "A yes to make a promise of.");
            var loaded = SaveAndLoad(promise.Asked);
            Assert.That(DialogueRules.Active(loaded)?.occasionKind, Is.EqualTo(DialogueVetoQuestion.Kind));
            var promised = DialogueRules4Season.Accepted(new EpisodeEngine(loaded), DialogueRules4Season.Choice(loaded, DialogueVetoQuestion.AskPromise));
            Assert.That(DialogueRules2Season.Json(promised), Is.EqualTo(DialogueRules2Season.Json(promise.Promised)));
        }

        [Test]
        public void ASeasonSavedMidPromiseLoadsAndTheVetoSettlesItAsIfNeverSaved()
        {
            var promise = DialogueRules4Season.Promises().FirstOrDefault(p => p.Maker == p.Stop.Winner);
            Assert.That(promise, Is.Not.Null, "A promise by the veto's winner.");
            var loaded = SaveAndLoad(promise.Promised);
            Assert.That(loaded.deals.Single(d => d.type == DealKind.VetoIf).status, Is.EqualTo(DealStatus.Active));
            var settled = DialogueRules4Season.ToTheDecision(new EpisodeEngine(loaded));
            var live = DialogueRules4Season.ToTheDecision(new EpisodeEngine(promise.Promised.Clone()));
            Assert.That(DialogueRules2Season.Json(settled[settled.Length - 1]), Is.EqualTo(DialogueRules2Season.Json(live[live.Length - 1])));
            SaveAndLoad(settled[settled.Length - 1]);
        }
    }
}
