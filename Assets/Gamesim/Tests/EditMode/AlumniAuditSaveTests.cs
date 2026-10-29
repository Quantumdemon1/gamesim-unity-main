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
    /// The alumni audit (story plan 20 §3.10, decision 10) and the save store. A season cast
    /// before the audit holds the older words in its save. The store hands it back as it was
    /// saved, the season is valid, and it plays on exactly as the same season cast with the
    /// audited words does, whichever words the cards hold. This is the store's side of
    /// <see cref="CastTemplateTests.ASeasonCastBeforeTheAuditKeepsItsBiosAndPlaysTheSame"/>, and it
    /// runs in Unity only: the dotnet subset compiles no Gamesim.Persistence.
    /// </summary>
    public sealed class AlumniAuditSaveTests
    {
        private const string Prefix = "GamesimAlumniAuditSave-";
        private string directory;

        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        private static JObject Payload(EpisodeState s) => JObject.FromObject(s, Serializer());
        private static ContestantState On(EpisodeState s, string templateId) =>
            s.contestants.Single(c => StoryPeople.TemplateOf(c)?.Id == templateId);

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

        [Test]
        public void ASeasonSavedBeforeTheAuditLoadsWithItsOwnWordsAndPlaysTheSame()
        {
            var audited = CastTemplateTests.CastAllStars();
            var older = CastTemplateTests.CastAllStars();
            foreach (var pair in CastTemplateTests.BiosBeforeTheAudit)
            {
                var who = On(older, pair.Key);
                Assert.That(who.bio, Is.EqualTo(CastTemplates.Find(pair.Key).Bio), "A new season is cast with the audited words.");
                who.bio = pair.Value;
            }
            var saved = Payload(older);

            var store = new EpisodeSaveStore(Path.Combine(directory, "season.json"));
            store.Save(older);
            Assert.That(store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(JToken.DeepEquals(Payload(loaded), saved), Is.True, "The store hands the season back as it was saved, older words and all.");
            Assert.That(EpisodeValidation.TryValidate(loaded, out var invalid), Is.True, invalid);
            foreach (var pair in CastTemplateTests.BiosBeforeTheAudit)
            {
                var who = On(loaded, pair.Key);
                Assert.That(who.bio, Is.EqualTo(pair.Value), pair.Key);
                Assert.That(StoryPeople.IsRealPerson(who), Is.True, pair.Key);
                Assert.That(WebIntroductions.AlumnusOf(who), Is.SameAs(CastTemplates.Find(pair.Key)), pair.Key);
            }

            var playedLoaded = CastTemplateTests.PlayOut(loaded);
            var playedAudited = CastTemplateTests.PlayOut(audited);
            var playedOnTheOlderCards = CastTemplateTests.OnTheCardsBeforeTheAudit(() => CastTemplateTests.PlayOut(loaded));
            Assert.That(JToken.DeepEquals(Payload(playedOnTheOlderCards), Payload(playedLoaded)), Is.True,
                "The older build's cards and the audited ones play the loaded season to the same state.");

            foreach (var pair in CastTemplateTests.BiosBeforeTheAudit)
            {
                var who = On(playedLoaded, pair.Key);
                Assert.That(who.bio, Is.EqualTo(pair.Value), "The season plays on with the words it was saved with.");
                who.bio = CastTemplates.Find(pair.Key).Bio;
            }
            Assert.That(JToken.DeepEquals(Payload(playedLoaded), Payload(playedAudited)), Is.True,
                "Apart from its alumni's words, the loaded season ends as the one cast with the audited words does.");
        }
    }
}
