using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class WebNpcMotiveParityTests
    {
        private static string FixturePath =>
#if UNITY_EDITOR
            Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "WebNpcMotiveFixtures.json");
#else
            Path.Combine(AppContext.BaseDirectory, "WebNpcMotiveFixtures.json");
#endif
        private static readonly Lazy<JObject> Golden = new Lazy<JObject>(() => JObject.Parse(File.ReadAllText(FixturePath)));
        private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
            { ObjectCreationHandling = ObjectCreationHandling.Replace, MissingMemberHandling = MissingMemberHandling.Error });
        private static T Read<T>(JToken value) => value.ToObject<T>(Serializer);
        private static string Json(object value) => JsonConvert.SerializeObject(value);
        private static WebNpcMotiveState State(JToken value) => Read<WebNpcMotiveState>(value);
        private static List<WebNpcMotiveAmount> Amounts(JToken value) => Read<List<WebNpcMotiveAmount>>(value);
        private static IEnumerable<JToken> Rows(string name, int count)
        {
            var rows = (JArray)Golden.Value[name];
            Assert.That(rows.Count, Is.EqualTo(count), name);
            return rows;
        }
        private static void SameState(JToken expected, WebNpcMotiveState actual)
        {
            foreach (string name in WebNpcMotives.MotiveNames)
                Assert.That(actual.Get(name), Is.EqualTo((double)expected[name]), name);
        }
        private static void NoSourceDraws(JToken row)
        { Assert.That((int)row["randomDraws"], Is.Zero); Assert.That(row["drawTrace"].Count(), Is.Zero); }

        [Test]
        public void ImmutableOriginalSourceFixtureAndOrderedConstantsArePinned()
        {
            using var hash = SHA256.Create();
            Assert.That(BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(FixturePath))).Replace("-", "").ToLowerInvariant(),
                Is.EqualTo("f3228526a3f8dbbcb442f8a9243492f2b61da96b2304bd66daafbb0720ace6e8"));
            Assert.That((string)Golden.Value["provenance"]["sourceSha256"], Is.EqualTo("f05a5a8cd3aeebf418db04ac0a62d7e263ae31b9503db8275685152667e2f4a7"));
            Assert.That(WebNpcMotives.MotiveNames, Is.EqualTo(Golden.Value["motiveNames"].Values<string>()));
            foreach (string name in WebNpcMotives.MotiveNames)
                Assert.That(WebNpcMotives.CriticalThresholds[name], Is.EqualTo((double)Golden.Value["criticalThresholds"][name]));
        }

        [Test]
        public void InitializationPreservesTraitOrderDuplicatesAndPerEntryClamps()
        {
            foreach (var row in Rows("initial",119))
            {
                var traits = Read<List<string>>(row["input"]); string before = Json(traits);
                SameState(row["expected"], WebNpcMotives.CreateInitialMotives(traits));
                Assert.That(Json(traits), Is.EqualTo(before)); NoSourceDraws(row);
            }
        }

        [Test]
        public void DecayPreservesEveryTraitMultiplicationOrderSignedTimeAndLowerOnlyClamp()
        {
            foreach (var row in Rows("decay",881))
            {
                var input = row["input"]; var state = State(input["state"]); var traits = Read<List<string>>(input["traits"]);
                string before = Json(new { state, traits });
                var result = WebNpcMotives.DecayMotives(state, (double)input["deltaSeconds"], traits);
                SameState(row["expected"], result); Assert.That(result, Is.Not.SameAs(state));
                Assert.That(Json(new { state, traits }), Is.EqualTo(before)); NoSourceDraws(row);
            }
        }

        [Test]
        public void SatisfactionIgnoresNonpositiveEntriesAndDoesNotNormalizeTheInput()
        {
            foreach (var row in Rows("satisfaction",260))
            {
                var state = State(row["input"]["state"]); var satisfaction = Amounts(row["input"]["satisfaction"]);
                string before = Json(new { state, satisfaction });
                var result = WebNpcMotives.SatisfyMotives(state, satisfaction);
                SameState(row["expected"], result); Assert.That(result, Is.Not.SameAs(state));
                Assert.That(Json(new { state, satisfaction }), Is.EqualTo(before)); NoSourceDraws(row);
            }
        }

        [Test]
        public void ActivityDefaultsPreserveSourceKeyOrderOmissionsAndNegativeExerciseEnergy()
        {
            foreach (var row in Rows("activityDefaults",14))
            {
                var actual = WebNpcMotives.GetActivitySatisfaction((string)row["input"]);
                var expected = (JArray)row["expected"];
                Assert.That(actual.Count, Is.EqualTo(expected.Count));
                for (int index = 0; index < actual.Count; index++)
                {
                    Assert.That(actual[index].motive, Is.EqualTo((string)expected[index]["motive"]), (string)row["input"]);
                    Assert.That(actual[index].amount, Is.EqualTo((double)expected[index]["amount"]), (string)row["input"]);
                }
                NoSourceDraws(row);
            }
            var altered = WebNpcMotives.GetActivitySatisfaction("exercise");
            altered[0].amount = 500; altered.Clear();
            Assert.That(WebNpcMotives.GetActivitySatisfaction("exercise")[0].amount, Is.EqualTo(-3));
        }

        [Test]
        public void UrgencyMatchesSourceClampsAndExponentWithinCrossRuntimePowPrecision()
        {
            foreach (var row in Rows("urgency",42))
            {
                // Math.Pow is implemented by different native libraries in Node/.NET/Mono.
                // Only this transcendental result uses tolerance; branch decisions/state arithmetic below are exact.
                Assert.That(WebNpcMotives.GetUrgency((double)row["input"]), Is.EqualTo((double)row["expected"]).Within(2e-15));
                NoSourceDraws(row);
            }
        }

        [Test]
        public void DurationAndSpeedMatchAllStrictThresholdNeighborsAndEmptyTargetCases()
        {
            foreach (var row in Rows("activityModifiers",1530))
            {
                var state = State(row["input"]["state"]); var satisfaction = Amounts(row["input"]["satisfaction"]);
                string before = Json(new { state, satisfaction });
                Assert.That(WebNpcMotives.GetActivityDurationMultiplier(state,satisfaction), Is.EqualTo((double)row["expected"]["duration"]));
                Assert.That(WebNpcMotives.GetWalkSpeedMultiplier(state,satisfaction), Is.EqualTo((double)row["expected"]["speed"]));
                Assert.That(Json(new { state, satisfaction }), Is.EqualTo(before)); NoSourceDraws(row);
            }
        }

        [Test]
        public void IdleMicroBehaviorConsumesOneDrawAtInclusivePointThreeAndPreservesPriorityAndUnusedTraits()
        {
            foreach (var row in Rows("micro",640))
            {
                var input = row["input"]; var state = State(input["state"]); var traits = Read<List<string>>(input["traits"]);
                string before = Json(new { state, traits }); int draws = 0;
                double Draw() { Assert.That(draws++, Is.Zero, "Unexpected extra cosmetic draw."); return (double)input["roll"]; }
                Assert.That(WebNpcMotives.GetIdleMicroBehavior(state,Draw,traits), Is.EqualTo((string)row["expected"]));
                Assert.That(draws, Is.EqualTo((int)row["randomDraws"])); Assert.That(draws, Is.EqualTo(1));
                Assert.That((double)row["drawTrace"][0], Is.EqualTo((double)input["roll"]));
                Assert.That(Json(new { state, traits }), Is.EqualTo(before));
            }
        }

        [Test]
        public void EmergencyReturnsFirstQualifyingMotiveNotLowestValueOrGreatestUrgency()
        {
            foreach (var row in Rows("emergency",47))
            {
                var state = State(row["input"]); string before = Json(state);
                Assert.That(WebNpcMotives.GetCriticalMotive(state), Is.EqualTo((string)row["expected"]));
                Assert.That(Json(state), Is.EqualTo(before)); NoSourceDraws(row);
            }
        }

        [Test]
        public void MoodRippleRequiresStrictlyGreaterThanThirtyAndDoesNotApplyTheBoostItself()
        {
            foreach (var row in Rows("mood",90))
            {
                var beforeState = State(row["input"]["before"]); var after = State(row["input"]["after"]);
                string input = Json(new { beforeState, after });
                var result = WebNpcMotives.GetMoodBoost(beforeState,after);
                if (row["expected"].Type == JTokenType.Null) Assert.That(result, Is.Null);
                else
                {
                    Assert.That(result, Is.Not.Null);
                    Assert.That(result.speedBoost, Is.EqualTo((double)row["expected"]["speedBoost"]));
                    Assert.That(result.socialBoost, Is.EqualTo((double)row["expected"]["socialBoost"]));
                }
                Assert.That(Json(new { beforeState, after }), Is.EqualTo(input)); NoSourceDraws(row);
            }
        }

        [Test]
        public void CanonicalValidationDoesNotSilentlyRepairInvalidKeysOrNonfiniteInputs()
        {
            var state = WebNpcMotives.CreateInitialMotives(); string before = Json(state); int draws = 0;
            double Draw() { draws++; return .1; }
            var unknown = new[] { new WebNpcMotiveAmount("unknown",1) };
            var duplicates = new[] { new WebNpcMotiveAmount("fun",1), new WebNpcMotiveAmount("fun",2) };
            Assert.Throws<ArgumentException>(() => WebNpcMotives.SatisfyMotives(state,unknown));
            Assert.Throws<ArgumentException>(() => WebNpcMotives.GetActivityDurationMultiplier(state,duplicates));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebNpcMotives.DecayMotives(state,double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebNpcMotives.GetUrgency(double.PositiveInfinity));
            var invalid = state.Clone(); invalid.energy = double.NaN;
            Assert.Throws<ArgumentOutOfRangeException>(() => WebNpcMotives.GetIdleMicroBehavior(invalid,Draw));
            Assert.That(draws, Is.Zero); Assert.That(Json(state), Is.EqualTo(before));
        }

        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(-.001)] [TestCase(1)]
        public void InvalidCosmeticSamplesRejectAfterTheirSingleSuppliedDraw(double value)
        {
            int count = 0; var state = WebNpcMotives.CreateInitialMotives(); string before = Json(state);
            Assert.Throws<ArgumentOutOfRangeException>(() => WebNpcMotives.GetIdleMicroBehavior(state,() => { count++; return value; }));
            Assert.That(count, Is.EqualTo(1)); Assert.That(Json(state), Is.EqualTo(before));
        }

        [Test]
        public void TransientDtosDoNotClaimUnityPersistenceAndSourceUnknownTraitsAreIgnored()
        {
            foreach (var type in new[] { typeof(WebNpcMotiveState), typeof(WebNpcMotiveAmount), typeof(WebNpcMoodBoost) })
                Assert.That(Attribute.IsDefined(type,typeof(SerializableAttribute)), Is.False);
            Assert.That(Json(WebNpcMotives.CreateInitialMotives(new[] { "unknown", "social" })), Is.EqualTo(Json(WebNpcMotives.CreateInitialMotives())));
            Assert.That(WebNpcMotives.GetActivitySatisfaction("unknown"), Is.Empty);
        }
    }
}
