#if GAMESIM_ALLSTAR_CANDIDATE
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>Real candidate SaveStore tests; synthetic historical envelopes only prove refusal, not compatibility.</summary>
    public sealed class AlumniCandidatePersistenceTests
    {
        private string directory;
        private EpisodeSaveStore store;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "GamesimAlumniCandidate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            store = new EpisodeSaveStore(Path.Combine(directory, "episode.json"));
        }

        [TearDown]
        public void TearDown()
        {
            string resolved = Path.GetFullPath(directory);
            Assert.That(resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(resolved), Does.StartWith("GamesimAlumniCandidate-"));
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }

        private static EpisodeState Fresh(bool histories)
        {
            var choice = new SeasonBuilder.Choice
            {
                Roster = CastTemplates.Roster.AllStars, HouseSize = 12, PlayerHistories = histories,
            };
            var state = SeasonBuilder.Create(choice, 947u);
            ShippedRules.ApplyFresh(state, choice);
            return state;
        }

        private JObject Payload(EpisodeState state)
        {
            var observed = new EpisodeSaveStore(Path.Combine(directory, "observed-" + Guid.NewGuid().ToString("N") + ".json"));
            observed.Save(state);
            return (JObject)JObject.Parse(File.ReadAllText(observed.SavePath))["state"];
        }

        private void WriteEnvelope(JObject envelope)
        {
            // Independently construct a checksum-valid malformed fixture through the public file boundary.
            JToken Ordered(JToken token) => token is JObject obj
                ? new JObject(obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => new JProperty(p.Name, Ordered(p.Value))))
                : token is JArray array ? new JArray(array.Select(Ordered)) : token.DeepClone();
            using var hash = SHA256.Create();
            string canonical = Ordered(envelope["state"]).ToString(Newtonsoft.Json.Formatting.None);
            envelope["checksum"] = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-", "").ToLowerInvariant();
            File.WriteAllText(store.SavePath, envelope.ToString());
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FreshCandidateRoundTripKeepsExactHistoryConfigurationAndState(bool histories)
        {
            var state = Fresh(histories);
            var before = Payload(state);
            store.Save(state);
            var written = File.ReadAllBytes(store.SavePath);
            Assert.That(store.TryLoad(out var loaded, out var message), Is.True, message);
            Assert.That(loaded.schemaVersion, Is.EqualTo(29));
            Assert.That(loaded.alumniCatalogVersion, Is.EqualTo(AlumniCatalog.Version));
            Assert.That(loaded.playerHistoriesEnabled, Is.EqualTo(histories));
            Assert.That(JToken.DeepEquals(before, Payload(loaded)), Is.True);
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(written), "Reading never rewrites the candidate save.");
            Assert.That(Directory.GetFiles(directory, "*.pending-*"), Is.Empty);
        }

        [TestCase("alumniRulesVersion")]
        [TestCase("alumniCatalogVersion")]
        [TestCase("playerHistoriesEnabled")]
        [TestCase("priorTrustOffset")]
        public void MissingCandidateFieldsCannotBeNormalizedOnLoad(string field)
        {
            var state = Fresh(true);
            store.Save(state);
            var envelope = JObject.Parse(File.ReadAllText(store.SavePath));
            var payload = (JObject)envelope["state"];
            if (field == "priorTrustOffset") ((JObject)payload["relationships"][0]).Remove(field);
            else payload.Remove(field);
            WriteEnvelope(envelope);
            var written = File.ReadAllBytes(store.SavePath);
            Assert.That(store.TryLoad(out var loaded, out var message), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(message, Does.Contain("missing or unknown schema fields"));
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(written));
            Assert.Throws<InvalidDataException>(() => store.Save(state));
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(written));
            Assert.That(File.Exists(store.BackupPath), Is.False);
        }

        [TestCase(1)]
        [TestCase(27)]
        [TestCase(28)]
        public void HistoricalDispatchRefusesBeforeAnyPromotionOrMutation(int version)
        {
            var synthetic = new JObject { ["schemaVersion"] = version, ["sentinel"] = "untouched" };
            var before = (JObject)synthetic.DeepClone();
            var error = Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(synthetic, out _));
            Assert.That(error.Message, Does.Contain("FrozenEpisodeV28"));
            Assert.That(JToken.DeepEquals(before, synthetic), Is.True);
        }

        [Test]
        public void SyntheticSchema28SaveRemainsUntouchedAndCannotBeOverwrittenByTheCandidate()
        {
            var candidate = Fresh(true);
            store.Save(candidate);
            var envelope = JObject.Parse(File.ReadAllText(store.SavePath));
            var synthetic = (JObject)envelope["state"];
            synthetic["schemaVersion"] = 28;
            synthetic.Remove("alumniRulesVersion");
            synthetic.Remove("alumniCatalogVersion");
            synthetic.Remove("playerHistoriesEnabled");
            foreach (var edge in synthetic["relationships"].OfType<JObject>()) edge.Remove("priorTrustOffset");
            WriteEnvelope(envelope);
            var historical = File.ReadAllBytes(store.SavePath);
            Assert.That(store.TryLoad(out var loaded, out var message), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(message, Does.Contain("FrozenEpisodeV28"));
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(historical));
            Assert.Throws<InvalidDataException>(() => store.Save(candidate));
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(historical));
            Assert.That(File.Exists(store.BackupPath), Is.False);
        }

        [Test]
        public void UnknownCandidateFieldIsNotAnAcceptedExtension()
        {
            store.Save(Fresh(true));
            var envelope = JObject.Parse(File.ReadAllText(store.SavePath));
            ((JObject)envelope["state"]).Add("unreviewedAlumniExtension", true);
            WriteEnvelope(envelope);
            var written = File.ReadAllBytes(store.SavePath);
            Assert.That(store.TryLoad(out var loaded, out var message), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(message, Does.Contain("missing or unknown schema fields"));
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(written));
        }
    }
}
#endif
