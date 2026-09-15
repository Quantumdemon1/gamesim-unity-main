using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class WebLoyaltyOathParityTests
    {
        public static string FixturePath => Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "WebOathFixtures.json");
        public static IEnumerable<TestCaseData> OathCases() => Read()["cases"].Select(item =>
            new TestCaseData(item).SetName("Web oath: " + (string)item["name"]));
        public static IEnumerable<TestCaseData> ProposalCases() => Read()["proposals"].Select((item,index) =>
            new TestCaseData(item).SetName("Web oath campaign proposal " + index));

        [TestCaseSource(nameof(OathCases))]
        public void OathPlanMatchesSource(JObject item)
        {
            var snapshot = item["input"].ToObject<WebOathSnapshot>();
            for (int index = 0; index < snapshot.oaths.Count; index++)
            {
                int alias = (int)item["aliasOf"][index];
                if (alias >= 0) snapshot.oaths[index] = snapshot.oaths[alias];
            }
            string before = JsonConvert.SerializeObject(snapshot);
            var actor = snapshot.actors[0].id; var victim = snapshot.actors[1].id;
            var result = (bool)item["eviction"] ? WebLoyaltyOaths.EvictionVote(snapshot, actor, victim)
                : WebLoyaltyOaths.Nomination(snapshot, actor, victim, item["neutralRolls"].ToObject<double[]>());
            Equivalent(item["expected"], JObject.FromObject(result), (string)item["name"]);
            Assert.That(JsonConvert.SerializeObject(snapshot), Is.EqualTo(before), "The pure leaf may not mutate its source snapshot.");
            if (!(bool)item["eviction"])
                Assert.That(WebLoyaltyOaths.NominationNeutralWitnesses(snapshot, actor, victim),
                    Is.EqualTo(item["expected"]["neutralWitnessIds"].ToObject<string[]>()));
        }

        [TestCaseSource(nameof(ProposalCases))]
        public void CampaignProposalMatchesSourceWithoutInventingAnOath(JObject item)
        {
            var input = item["input"];
            var result = WebLoyaltyOaths.CampaignProposal(input["playerTraits"].ToObject<string[]>(),
                input["npcTraits"].ToObject<string[]>(), (double)input["score"], "Player");
            Equivalent(item["expected"], JObject.FromObject(result), "proposal");
            Assert.That(result.createsStructuredOath, Is.False);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-0.001)]
        [TestCase(1)]
        public void InvalidNominationEvidenceRejectsWithoutMutation(double roll)
        {
            var snapshot = Basic(); var before = JsonConvert.SerializeObject(snapshot);
            Assert.Throws<ArgumentOutOfRangeException>(() => WebLoyaltyOaths.Nomination(snapshot,
                snapshot.actors[0].id, snapshot.actors[1].id, new[] { roll }));
            Assert.That(JsonConvert.SerializeObject(snapshot), Is.EqualTo(before));
        }

        [Test]
        public void ExactSampleCountAndFiniteInputsAreRequired()
        {
            var snapshot = Basic(); var actor = snapshot.actors[0].id; var victim = snapshot.actors[1].id;
            Assert.Throws<ArgumentException>(() => WebLoyaltyOaths.Nomination(snapshot, actor, victim, Array.Empty<double>()));
            Assert.Throws<ArgumentException>(() => WebLoyaltyOaths.Nomination(snapshot, actor, victim, new[] { .2, .3 }));
            snapshot.relationships[0].score = double.NaN;
            Assert.Throws<ArgumentOutOfRangeException>(() => WebLoyaltyOaths.EvictionVote(snapshot, actor, victim));
        }

        [Test]
        public void ReturnedPlanIsDetachedAndDoesNotChangeAnotherEvaluation()
        {
            var snapshot = Basic(); var actor = snapshot.actors[0].id; var victim = snapshot.actors[1].id;
            string before = JsonConvert.SerializeObject(snapshot);
            var first = WebLoyaltyOaths.Nomination(snapshot, actor, victim, new[] { .5 });
            string expected = JsonConvert.SerializeObject(first);
            first.ripples[0].score = 100; first.arcChanges.Clear(); first.remainingOathIndices.Add(999);
            Assert.That(JsonConvert.SerializeObject(WebLoyaltyOaths.Nomination(snapshot, actor, victim, new[] { .5 })), Is.EqualTo(expected));
            Assert.That(JsonConvert.SerializeObject(snapshot), Is.EqualTo(before));
        }

        private static JObject Read() => JObject.Parse(File.ReadAllText(FixturePath));
        private static WebOathSnapshot Basic() => Read()["cases"][0]["input"].ToObject<WebOathSnapshot>();
        private static bool Numeric(JToken value) => value.Type == JTokenType.Integer || value.Type == JTokenType.Float;
        private static void Equivalent(JToken expected, JToken actual, string path)
        {
            if (Numeric(expected) && Numeric(actual))
            { Assert.That((double)actual, Is.EqualTo((double)expected).Within(1e-10), path); return; }
            Assert.That(actual.Type, Is.EqualTo(expected.Type), path);
            if (expected is JObject obj)
            {
                Assert.That(((JObject)actual).Properties().Select(p => p.Name), Is.EquivalentTo(obj.Properties().Select(p => p.Name)), path);
                foreach (var property in obj.Properties()) Equivalent(property.Value, actual[property.Name], path + "." + property.Name);
            }
            else if (expected is JArray array)
            {
                Assert.That(((JArray)actual).Count, Is.EqualTo(array.Count), path);
                for (int index = 0; index < array.Count; index++) Equivalent(array[index], actual[index], path + "[" + index + "]");
            }
            else Assert.That(JToken.DeepEquals(expected, actual), Is.True, path);
        }
    }
}

