using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// B9's jury tiebreak (<see cref="JuryTiebreakTests"/>) where the editor's assemblies reach: the real save store at
    /// schema 30 and the finale panel's line. A tied jury saved and loaded just before it resolves crowns the same finalist
    /// as the season played straight through, so a load cannot reroll the tie; a tuning-1 save whose tied jury crowned the
    /// other finalist is refused on save and on load, and a tuning-0 save is asked nothing new. Only unique temporary slots
    /// are written. Not in the dotnet subset: it reaches Gamesim.Persistence and the director.
    /// </summary>
    public sealed class JuryTiebreakEditorTests
    {
        [TestCase(1u)]
        [TestCase(3u)]
        public void ATiedJuryAtTuningOneRoundTripsThroughTheRealStoreAtThirtyAndCrownsTheSameFinalist(uint seed)
        {
            var jury = JuryTiebreakTests.TiedJury(seed, TuningRules.Bands);
            string coin = TuningRules.JuryTieWinner(seed, jury.playerId, JuryTiebreakTests.Maya);
            var straight = JuryTiebreakTests.Resolve(jury);
            Assert.That(straight.winnerId, Is.EqualTo(coin), "Played straight through, the coin.");

            Assert.That(EpisodeState.LiveSchema, Is.GreaterThanOrEqualTo(30), "The tuning rules are schema 30's.");
            using var slot = new Slot();
            slot.Store.Save(jury);
            var written = (JObject)JObject.Parse(File.ReadAllText(slot.Store.SavePath))["state"];
            Assert.That((int)written["schemaVersion"], Is.EqualTo(EpisodeState.LiveSchema));
            Assert.That((int)written["tuningRulesVersion"], Is.EqualTo(TuningRules.Bands));
            Assert.That((uint)written["seed"], Is.EqualTo(seed), "The coin's seed is the season's own, saved with it.");
            byte[] bytes = File.ReadAllBytes(slot.Store.SavePath);
            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(File.ReadAllBytes(slot.Store.SavePath), Is.EqualTo(bytes), "Loading current data never rewrites it.");
            Assert.That(JToken.DeepEquals(Payload(loaded), Payload(jury)), Is.True, "Every saved field survives at 30.");
            // Saved again from the load and loaded once more: whatever the number of loads, the tie goes the same way.
            slot.Store.Save(loaded);
            Assert.That(slot.Store.TryLoad(out var again, out message), Is.True, message);
            var afterLoads = JuryTiebreakTests.Resolve(again);
            Assert.That(afterLoads.winnerId, Is.EqualTo(coin));
            Assert.That(JToken.DeepEquals(Payload(afterLoads), Payload(straight)), Is.True,
                "Resolved after the loads, the season is the one played straight through, field for field.");
            Assert.That(afterLoads.randomState, Is.EqualTo(jury.randomState), "Nothing drawn: no stream to reroll.");

            // The finished season saves and loads: validation re-derives the coin on the way in, and agrees.
            slot.Store.Save(afterLoads);
            Assert.That(slot.Store.TryLoad(out var finished, out message), Is.True, message);
            Assert.That((finished.winnerId, finished.runnerUpId), Is.EqualTo((straight.winnerId, straight.runnerUpId)));
            Assert.That(finished.events.Single(e => e.kind == "jury-tie").text,
                Is.EqualTo("Jury tie: the season's tiebreak draw gives the win to " + (coin == jury.playerId ? "you" : "Maya Hassan") + "."));
        }

        [Test]
        public void TheStoreRefusesATuningOneSeasonWhoseTiedJuryCrownedTheOtherFinalist()
        {
            var forged = JuryTiebreakTests.Swapped(JuryTiebreakTests.Resolve(JuryTiebreakTests.TiedJury(3u, TuningRules.Bands)));
            using (var slot = new Slot())
            {
                var refused = Assert.Throws<InvalidDataException>(() => slot.Store.Save(forged));
                Assert.That(refused.Message, Is.EqualTo(TuningRules.JuryTieRefusal));
                Assert.That(File.Exists(slot.Store.SavePath), Is.False, "Nothing is written.");
                // On disk with a valid checksum, as a hand-edited save would be: the load refuses it for the same reason.
                File.WriteAllText(slot.Store.SavePath, PersistenceMigrationTests.Envelope(Payload(forged)));
                Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.False);
                Assert.That(loaded, Is.Null);
                Assert.That(message, Does.Contain(TuningRules.JuryTieRefusal));
            }

            // The same forgery at tuning 0 is what the store always admitted: tuning 0 is asked nothing new.
            var old = JuryTiebreakTests.Swapped(JuryTiebreakTests.Resolve(JuryTiebreakTests.TiedJury(3u, TuningRules.Off)));
            using (var slot = new Slot())
            {
                slot.Store.Save(old);
                Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
                Assert.That(loaded.winnerId, Is.EqualTo(old.winnerId));
            }
        }

        /// <summary>
        /// The finale panel's jury line names who a tie goes to, as the engine settles it: at tuning 0 the second finalist in
        /// cast order, in its old words; at tuning 1 the coin, in any cast order. Seed 1's coin gives Maya the win, seed 3's
        /// the player.
        /// </summary>
        [TestCase(1u, "Maya Hassan")]
        [TestCase(3u, "you")]
        public void TheFinalePanelSaysWhoATieGoesToAsTheSeasonSettlesIt(uint seed, string tieWinner)
        {
            Assert.That(EpisodeDirector.JuryLine(JuryTiebreakTests.TiedJury(seed, TuningRules.Off)),
                Is.EqualTo("4 jurors choose the winner. If they split evenly, the win goes to Maya Hassan, as the reference game's tie rule has it."));
            string tuned = "4 jurors choose the winner. If they split evenly, the season's tiebreak draw gives the win to " + tieWinner + ".";
            var jury = JuryTiebreakTests.TiedJury(seed, TuningRules.Bands);
            Assert.That(EpisodeDirector.JuryLine(jury), Is.EqualTo(tuned));
            Assert.That(EpisodeDirector.JuryLine(JuryTiebreakTests.TiedJury(seed, TuningRules.Bands, mayaFirst: true)), Is.EqualTo(tuned),
                "Cast order moves nothing.");
            var done = JuryTiebreakTests.Resolve(jury);
            Assert.That(done.winnerId, Is.EqualTo(tieWinner == "you" ? done.playerId : JuryTiebreakTests.Maya), "The panel says what the engine does.");
        }

        // ---------------------------------------------------------------- helpers

        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

        private static JObject Payload(EpisodeState s) => JObject.FromObject(s, Serializer());

        /// <summary>A unique temporary save slot, removed with everything in it.</summary>
        private sealed class Slot : IDisposable
        {
            private const string Prefix = "GamesimJuryTiebreak-";
            private readonly string directory = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
            public readonly EpisodeSaveStore Store;

            public Slot()
            {
                Directory.CreateDirectory(directory);
                Store = new EpisodeSaveStore(Path.Combine(directory, "episode.json"));
            }

            public void Dispose()
            {
                var path = Path.GetFullPath(directory);
                Assert.That(Path.GetDirectoryName(path),
                    Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                Assert.That(Path.GetFileName(path), Does.StartWith(Prefix));
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
        }
    }
}
