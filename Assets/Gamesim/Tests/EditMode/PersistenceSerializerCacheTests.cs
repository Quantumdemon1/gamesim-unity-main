using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class PersistenceSerializerCacheTests
    {
        private static JsonSerializer CreateSerializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, null);

        [Test]
        public void DistinctSerializersReuseOnlyTheSamePublicFieldContracts()
        {
            var first = CreateSerializer(); var second = CreateSerializer();
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first.ContractResolver, Is.SameAs(second.ContractResolver));
            var frozen = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.FrozenEpisodeV4+State", true);
            foreach (Type type in new[] { typeof(EpisodeState), typeof(ContestantState), typeof(ContestantStats),
                typeof(System.Collections.Generic.List<ContestantState>), typeof(WebPersonaState), frozen })
                Assert.That(first.ContractResolver.ResolveContract(type), Is.SameAs(second.ContractResolver.ResolveContract(type)), type.FullName);
            var contract = (JsonObjectContract)first.ContractResolver.ResolveContract(typeof(EpisodeState));
            Assert.That(contract.Properties.Select(property => property.PropertyName), Is.EquivalentTo(
                typeof(EpisodeState).GetFields(BindingFlags.Instance | BindingFlags.Public).Select(field => field.Name)));
            Assert.That(contract.Properties.Any(property => property.PropertyName == "Active"), Is.False);
            AssertStrictOptions(first); AssertStrictOptions(second);
        }

        [Test]
        public void PerSerializerOptionChangesCannotLeakIntoTheNextOperation()
        {
            var changed = CreateSerializer(); var sharedResolver = changed.ContractResolver;
            // Change this serializer only. Never mutate the shared resolver's configuration or cached contracts.
            changed.TypeNameHandling = TypeNameHandling.All;
            changed.MissingMemberHandling = MissingMemberHandling.Ignore;
            changed.ObjectCreationHandling = ObjectCreationHandling.Auto;
            changed.DateParseHandling = DateParseHandling.DateTime;
            changed.MaxDepth = 3;
            changed.NullValueHandling = NullValueHandling.Ignore;
            changed.DefaultValueHandling = DefaultValueHandling.Ignore;
            changed.Formatting = Formatting.Indented;
            changed.ContractResolver = new DefaultContractResolver();
            var next = CreateSerializer();
            Assert.That(next, Is.Not.SameAs(changed));
            Assert.That(next.ContractResolver, Is.SameAs(sharedResolver));
            AssertStrictOptions(next);
            var payload = JObject.FromObject(ContentCatalog.Create(531), next);
            payload["unexpectedField"] = 1;
            Assert.Throws<JsonSerializationException>(() => payload.ToObject<EpisodeState>(CreateSerializer()));
        }

        [Test]
        public void CachedContractsDoNotShareStateAndPreserveIsoProseNullsAndDefaults()
        {
            const string prose = "2026-09-13T00:00:00.000Z";
            var original = ContentCatalog.Create(532); original.randomState = 0;
            original.contestants[0].motive = null;
            int memoryIndex = original.memories.Count;
            original.memories.Add(new MemoryState { ownerId = original.playerId, subjectId = original.contestants[1].id,
                text = prose, week = original.week, isPrivate = true });
            var token = JObject.FromObject(original, CreateSerializer());
            Assert.That(token["hohId"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That(token["contestants"][0]["motive"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That((bool)token["competitionResolved"], Is.False);
            Assert.That((int)token["playerStudyBonus"], Is.Zero);
            Assert.That((double)token["contestants"][0]["stats"]["physical"], Is.EqualTo(original.contestants[0].stats.physical));
            string json = token.ToString(Formatting.None);
            var first = Read<EpisodeState>(json); var second = Read<EpisodeState>(json);
            var roundTripToken = Read<JObject>(json);
            Assert.That(roundTripToken["memories"][memoryIndex]["text"].Type, Is.EqualTo(JTokenType.String));
            Assert.That((string)roundTripToken["memories"][memoryIndex]["text"], Is.EqualTo(prose));
            Assert.That(JToken.DeepEquals(token, roundTripToken), Is.True);
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first.contestants, Is.Not.SameAs(second.contestants));
            Assert.That(first.contestants[0], Is.Not.SameAs(second.contestants[0]));
            Assert.That(first.contestants[0].stats, Is.Not.SameAs(second.contestants[0].stats));
            Assert.That(first.contestants[0].traits, Is.Not.SameAs(second.contestants[0].traits));
            Assert.That(first.relationships, Is.Not.SameAs(second.relationships));
            Assert.That(first.relationships[0].notes, Is.Not.SameAs(second.relationships[0].notes));
            Assert.That(first.relationships[0].events, Is.Not.SameAs(second.relationships[0].events));
            Assert.That(first.playerPersona, Is.Not.SameAs(second.playerPersona));
            Assert.That(first.playerPersona.scores, Is.Not.SameAs(second.playerPersona.scores));
            Assert.That(first.playerPersona.scores[0], Is.Not.SameAs(second.playerPersona.scores[0]));
            Assert.That(first.memories[memoryIndex], Is.Not.SameAs(second.memories[memoryIndex]));
            Assert.That(first.playerPersona.scores.Count, Is.EqualTo(original.playerPersona.scores.Count));
            Assert.That(second.playerPersona.scores.Count, Is.EqualTo(original.playerPersona.scores.Count));
            Assert.That(second.randomState, Is.Zero);
            Assert.That(second.hohId, Is.Null); Assert.That(second.contestants[0].motive, Is.Null);
            Assert.That(second.memories[memoryIndex].text, Is.EqualTo(prose));
            first.memories[memoryIndex].text = "changed"; first.contestants[0].stats.physical = -1;
            first.contestants[0].traits.Add("changed"); first.playerPersona.scores[0].score = 99;
            first.relationships[0].notes.Add("changed");
            Assert.That(JToken.DeepEquals(token, JObject.FromObject(second, CreateSerializer())), Is.True);
            Assert.That(JToken.DeepEquals(token, JObject.FromObject(original, CreateSerializer())), Is.True);
        }

        private static T Read<T>(string json)
        {
            using var text = new StringReader(json);
            using var reader = new JsonTextReader(text);
            return CreateSerializer().Deserialize<T>(reader);
        }
        private static void AssertStrictOptions(JsonSerializer serializer)
        {
            Assert.That(serializer.TypeNameHandling, Is.EqualTo(TypeNameHandling.None));
            Assert.That(serializer.MissingMemberHandling, Is.EqualTo(MissingMemberHandling.Error));
            Assert.That(serializer.ObjectCreationHandling, Is.EqualTo(ObjectCreationHandling.Replace));
            Assert.That(serializer.DateParseHandling, Is.EqualTo(DateParseHandling.None));
            Assert.That(serializer.MaxDepth, Is.EqualTo(64));
            Assert.That(serializer.NullValueHandling, Is.EqualTo(NullValueHandling.Include));
            Assert.That(serializer.DefaultValueHandling, Is.EqualTo(DefaultValueHandling.Include));
            Assert.That(serializer.Formatting, Is.EqualTo(Formatting.None));
        }
    }
}
