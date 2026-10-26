// Frozen29 (SCH30): carried from FrozenV28SerializerCacheTests.cs by name; the fixed29 reader keeps the same cache contract.
// Additive cache safety tests shared by native Edit Mode and both pure reader projects.
// Actual hydration-to-private-factory wiring must be independently source-reviewed.
// No timing assertions, guaranteed cold-cache claims or mutable-contract spies.
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gamesim.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    [TestFixture]
#if UNITY_EDITOR
    [Parallelizable(ParallelScope.None)]
#else
    [NonParallelizable]
#endif
    public sealed class FrozenV29SerializerCacheTests
    {
        private const string Alliance = "{\"id\":\"a\",\"name\":\"2026-10-12T00:00:00Z\",\"members\":[\"second\",null,\"first\",\"second\"],\"active\":true,\"playerJoined\":false}";
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
#if UNITY_EDITOR
        private static readonly Assembly Owner = typeof(EpisodeSaveStore).Assembly;
#else
        private static readonly Assembly Owner = typeof(FrozenEpisodeV29).Assembly;
#endif
        private static readonly Type Input = Owner.GetType("Gamesim.Persistence.Frozen29Components.FrozenV29Input", true);
        private static readonly Type AllianceType = Owner.GetType("Gamesim.Persistence.Frozen29Data.AllianceState", true);

        [Test]
        public void FreshSerializersReuseOnlyFixedLocalPublicFieldContracts()
        {
            var first = Serializer();
            var second = Serializer();
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first.ContractResolver, Is.SameAs(second.ContractResolver));
            AssertStrict(first);
            AssertStrict(second);
            var root = Owner.GetType("Gamesim.Persistence.Frozen29Data.EpisodeState", true);
            foreach (var type in new[] { root, AllianceType, typeof(List<>).MakeGenericType(AllianceType), typeof(string) })
                Assert.That(first.ContractResolver.ResolveContract(type),
                    Is.SameAs(second.ContractResolver.ResolveContract(type)), type.FullName);
            var contract = (JsonObjectContract)first.ContractResolver.ResolveContract(AllianceType);
            CollectionAssert.AreEquivalent(AllianceType.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Select(field => field.Name), contract.Properties.Select(property => property.PropertyName));
            Assert.That(first.ContractResolver.GetType().IsNestedPrivate, Is.True);
            Assert.That(first.ContractResolver.GetType().DeclaringType, Is.EqualTo(Input));
            var foreignRefusal = Assert.Throws<InvalidDataException>(() => first.ContractResolver.ResolveContract(typeof(ForeignDto)));
            Assert.That(foreignRefusal.Message, Is.EqualTo("Frozen29 component hydration left the fixed local snapshot."));
            Assert.That(second.ContractResolver.ResolveContract(AllianceType), Is.SameAs(contract));
        }

        [Test]
        public void PerOperationOptionsCannotLeakAndMalformedThenValidHydrationRemainsStrict()
        {
            var changed = Serializer();
            var originalResolver = changed.ContractResolver;
            changed.TypeNameHandling = TypeNameHandling.All;
            changed.MissingMemberHandling = MissingMemberHandling.Ignore;
            changed.ObjectCreationHandling = ObjectCreationHandling.Auto;
            changed.DateParseHandling = DateParseHandling.DateTime;
            changed.MaxDepth = 3;
            changed.Culture = CultureInfo.GetCultureInfo("fr-FR");
            changed.NullValueHandling = NullValueHandling.Ignore;
            changed.DefaultValueHandling = DefaultValueHandling.Ignore;
            changed.Formatting = Formatting.Indented;
            changed.Converters.Add(new RejectEveryConverter());
            changed.ContractResolver = new DefaultContractResolver();
            var next = Serializer();
            Assert.That(next, Is.Not.SameAs(changed));
            Assert.That(next.ContractResolver, Is.SameAs(originalResolver));
            AssertStrict(next);

            var valid = Parse(Alliance);
            var before = valid.DeepClone();
            var unknown = (JObject)valid.DeepClone();
            unknown.Add("unexpected", 1);
            var unknownBefore = unknown.DeepClone();
            Assert.Throws<InvalidDataException>(() => Hydrate(unknown));
            Assert.That(JToken.DeepEquals(unknown, unknownBefore), Is.True);
            var missing = (JObject)valid.DeepClone();
            missing.Property("members").Remove();
            var missingBefore = missing.DeepClone();
            Assert.Throws<InvalidDataException>(() => Hydrate(missing));
            Assert.That(JToken.DeepEquals(missing, missingBefore), Is.True);

            // Literal shape permits this Integer; actual Int32 hydration must refuse.
            var overflow = Parse("{\"id\":\"a\",\"why\":null,\"startedWeek\":2147483648,\"endedWeek\":0}");
            var overflowBefore = overflow.DeepClone();
            var shape = Owner.GetType("Gamesim.Persistence.Frozen29Components.FrozenV29Shape", true);
            var check = shape.GetMethod("CheckComponent", StaticPrivate, null,
                new[] { typeof(JToken), typeof(string) }, null);
            Assert.That(check, Is.Not.Null);
            Assert.DoesNotThrow(() => check.Invoke(null, new object[] { overflow, "AllianceRow" }));
            var refusal = Assert.Throws<InvalidDataException>(() => Hydrate(overflow, "AllianceRow"));
            Assert.That(refusal.Message, Is.EqualTo("Frozen29 component hydration failed."));
            Assert.That(JToken.DeepEquals(overflow, overflowBefore), Is.True);
            var repairedCopy = (JObject)overflow.DeepClone();
            repairedCopy["startedWeek"] = 0;
            var row = Hydrate(repairedCopy, "AllianceRow");
            Assert.That(row.GetType().GetField("startedWeek").GetValue(row), Is.EqualTo(0));

            var result = Hydrate(valid);
            Assert.That(Field(result, "id"), Is.EqualTo("a"));
            Assert.That(Field(result, "name"), Is.EqualTo("2026-10-12T00:00:00Z"));
            CollectionAssert.AreEqual(new[] { "second", null, "first", "second" }, Members(result));
            Assert.That(Field(result, "playerJoined"), Is.False);
            Assert.That(JToken.DeepEquals(valid, before), Is.True);
            var nullable = (JObject)valid.DeepClone();
            nullable["name"] = JValue.CreateNull();
            nullable["members"] = JValue.CreateNull();
            var nullResult = Hydrate(nullable);
            Assert.That(Field(nullResult, "name"), Is.Null);
            Assert.That(Field(nullResult, "members"), Is.Null);
            AssertStrict(Serializer());
        }

        [Test]
        public void ConcurrentHydrationIgnoresGlobalDefaultsAndKeepsResultsAndInputIndependent()
        {
            var previous = JsonConvert.DefaultSettings;
            var ownedThreads = new ConcurrentDictionary<int, byte>();
            int defaultCalls = 0;
            Func<JsonSerializerSettings> hostile = () =>
            {
                // NonParallelizable does not stop unrelated Editor background JSON work.
                if (!ownedThreads.ContainsKey(Thread.CurrentThread.ManagedThreadId))
                    return previous == null ? null : previous();
                Interlocked.Increment(ref defaultCalls);
                return new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All,
                    ObjectCreationHandling = ObjectCreationHandling.Auto,
                    Culture = CultureInfo.GetCultureInfo("fr-FR"),
                    Converters = new List<JsonConverter> { new RejectEveryConverter() }
                };
            };
            try
            {
                JsonConvert.DefaultSettings = hostile;
                InOwnedProbe(ownedThreads, () =>
                {
                    // Positive control: our synchronous scope really reaches the hostile branch.
                    var positive = JsonConvert.DefaultSettings();
                    Assert.That(defaultCalls, Is.EqualTo(1));
                    Assert.That(positive.Converters.Single(), Is.TypeOf<RejectEveryConverter>());
                    Interlocked.Exchange(ref defaultCalls, 0);

                    var token = Parse(Alliance);
                    var before = token.DeepClone();
                    var results = new object[32];
                    var failures = new ConcurrentQueue<Exception>();
                    // Cache may already be warm. This is not a guaranteed first-use race.
                    Parallel.For(0, results.Length, i => InOwnedProbe(ownedThreads, () =>
                    {
                        try { results[i] = Hydrate(token); }
                        catch (Exception error) { failures.Enqueue(error); }
                    }));
                    Assert.That(failures, Is.Empty);
                    Assert.That(defaultCalls, Is.Zero);
                    Assert.That(JsonConvert.DefaultSettings, Is.SameAs(hostile));
                    for (int i = 0; i < results.Length; i++)
                    {
                        Assert.That(results[i].GetType(), Is.EqualTo(AllianceType));
                        Assert.That(Field(results[i], "id"), Is.EqualTo("a"));
                        CollectionAssert.AreEqual(new[] { "second", null, "first", "second" }, Members(results[i]));
                        for (int j = 0; j < i; j++)
                        {
                            Assert.That(results[i], Is.Not.SameAs(results[j]));
                            Assert.That(Members(results[i]), Is.Not.SameAs(Members(results[j])));
                        }
                    }
                    Members(results[0]).Clear();
                    AllianceType.GetField("id").SetValue(results[0], "changed");
                    Assert.That(Field(results[1], "id"), Is.EqualTo("a"));
                    Assert.That(Members(results[1]).Count, Is.EqualTo(4));
                    Assert.That(JToken.DeepEquals(token, before), Is.True);
                    AssertStrict(Serializer());
                    Assert.That(defaultCalls, Is.Zero);
                    Assert.That(JsonConvert.DefaultSettings, Is.SameAs(hostile));
                });
                Assert.That(ownedThreads, Is.Empty);
            }
            finally { JsonConvert.DefaultSettings = previous; }
            Assert.That(JsonConvert.DefaultSettings, Is.SameAs(previous));
        }

        private static void InOwnedProbe(ConcurrentDictionary<int, byte> owned, Action action)
        {
            int thread = Thread.CurrentThread.ManagedThreadId;
            bool added = owned.TryAdd(thread, 0);
            try { action(); }
            finally { if (added) owned.TryRemove(thread, out _); }
        }

        private static JsonSerializer Serializer() => (JsonSerializer)Invoke("CreateSerializer", Type.EmptyTypes, null);
        private static JObject Parse(string text) => (JObject)Invoke("ParseObject", new[] { typeof(byte[]) },
            new object[] { Encoding.UTF8.GetBytes(text) });
        private static object Hydrate(JObject token, string component = "AllianceState") => Invoke("HydrateComponentShapeOnly",
            new[] { typeof(JToken), typeof(string) }, new object[] { token, component });
        private static object Field(object value, string name) => AllianceType.GetField(name).GetValue(value);
        private static IList Members(object value) => (IList)Field(value, "members");

        private static object Invoke(string method, Type[] parameters, object[] arguments)
        {
            var exact = Input.GetMethod(method, StaticPrivate, null, parameters, null);
            Assert.That(exact, Is.Not.Null, "Missing exact frozen API: " + method);
            try { return exact.Invoke(null, arguments); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
        }

        private static void AssertStrict(JsonSerializer serializer)
        {
            Assert.That(serializer.TypeNameHandling, Is.EqualTo(TypeNameHandling.None));
            Assert.That(serializer.MissingMemberHandling, Is.EqualTo(MissingMemberHandling.Error));
            Assert.That(serializer.ObjectCreationHandling, Is.EqualTo(ObjectCreationHandling.Replace));
            Assert.That(serializer.DateParseHandling, Is.EqualTo(DateParseHandling.None));
            Assert.That(serializer.MaxDepth, Is.EqualTo(64));
            Assert.That(serializer.Culture, Is.EqualTo(CultureInfo.InvariantCulture));
            Assert.That(serializer.NullValueHandling, Is.EqualTo(NullValueHandling.Include));
            Assert.That(serializer.DefaultValueHandling, Is.EqualTo(DefaultValueHandling.Include));
            Assert.That(serializer.Formatting, Is.EqualTo(Formatting.None));
            Assert.That(serializer.Converters, Is.Empty);
        }

        private sealed class ForeignDto
        {
            public int value = 1;
        }

        private sealed class RejectEveryConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => true;
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
                => throw new InvalidOperationException("Global or previous serializer settings leaked into frozen hydration.");
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
                => throw new InvalidOperationException("This test never writes through the sentinel converter.");
        }
    }
}
