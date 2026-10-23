using System;
using System.IO;
using Gamesim.Persistence;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The role moments' cards through the shipping save store (ROLE-MOMENTS-PLAN R1, §4.1): a season at the veto meeting,
    /// saved and loaded by <see cref="EpisodeSaveStore"/>, shows every card - tone, situation word, read and line - as it
    /// did, and saving changes nothing a card reads. <see cref="RoleToneTests"/> reloads through Newtonsoft alone; the
    /// subset leaves the store out, so this is EditMode only. Only a unique temporary slot is written.
    /// </summary>
    public sealed class RoleTonePersistenceTests
    {
        [Test]
        public void TheSaveStoreReloadsTheSameCards()
        {
            var s = RoleToneTests.Meeting(out _, out _).Snapshot;
            var cards = RoleToneTests.Cards(s);
            string folder = Path.Combine(Path.GetTempPath(), "GamesimRoleTone-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var store = new EpisodeSaveStore(Path.Combine(folder, "episode.json"));
                store.Save(s);
                Assert.That(RoleToneTests.Cards(s), Is.EqualTo(cards), "Saving changes nothing a card reads.");
                Assert.That(new EpisodeSaveStore(store.SavePath).TryLoad(out var loaded, out string message), Is.True, message);
                Assert.That(RoleToneTests.Cards(loaded), Is.EqualTo(cards), "A reload through the store shows the same cards.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
