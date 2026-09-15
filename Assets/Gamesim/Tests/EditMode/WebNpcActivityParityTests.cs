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
    public sealed class WebNpcActivityParityTests
    {
        private static string FixturePath =>
#if UNITY_EDITOR
            Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "WebNpcActivityFixtures.json");
#else
            Path.Combine(AppContext.BaseDirectory, "WebNpcActivityFixtures.json");
#endif
        private static readonly Lazy<JObject> Golden = new Lazy<JObject>(() => JObject.Parse(File.ReadAllText(FixturePath)));
        private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
            { ObjectCreationHandling = ObjectCreationHandling.Replace, MissingMemberHandling = MissingMemberHandling.Error });
        private static T Read<T>(JToken value) => value.ToObject<T>(Serializer);
        private static string Json(object value) => JsonConvert.SerializeObject(value);
        private static IEnumerable<JToken> Rows(string name, int count)
        {
            var rows = (JArray)Golden.Value[name]; Assert.That(rows.Count, Is.EqualTo(count), name); return rows;
        }
        private static WebNpcMotiveState Motives(double value = 75) => new WebNpcMotiveState
            { social = value, rest = value, fun = value, energy = value, hygiene = value };
        private static WebNpcActivityPoint Point() => new WebNpcActivityPoint
            { id = "test", room = "living", activity = "sit", capacity = 2,
                satisfaction = new List<WebNpcMotiveAmount> { new WebNpcMotiveAmount("social", 3) } };
        private static void Same(JToken expected, object actual, string context)
        {
            SameToken(expected, actual == null ? JValue.CreateNull() : JToken.FromObject(actual, Serializer), context);
        }
        private static void SameToken(JToken expected, JToken actual, string context)
        {
            if (expected.Type == JTokenType.Float || expected.Type == JTokenType.Integer)
            { Assert.That(actual.Type == JTokenType.Float || actual.Type == JTokenType.Integer, Is.True, context); Assert.That((double)actual, Is.EqualTo((double)expected), context); return; }
            Assert.That(actual.Type, Is.EqualTo(expected.Type), context);
            if (expected is JObject obj)
            {
                Assert.That(((JObject)actual).Properties().Select(p => p.Name), Is.EquivalentTo(obj.Properties().Select(p => p.Name)), context);
                foreach (var property in obj.Properties()) SameToken(property.Value, actual[property.Name], context + "." + property.Name);
            }
            else if (expected is JArray array)
            { Assert.That(actual.Count(), Is.EqualTo(array.Count), context); for (int i = 0; i < array.Count; i++) SameToken(array[i], actual[i], context + "[" + i + "]"); }
            else Assert.That(JToken.DeepEquals(expected, actual), Is.True, context);
        }
        private sealed class Draw
        {
            private readonly double value; private readonly int expected; private int calls;
            public Draw(double value, int expected = 1) { this.value = value; this.expected = expected; }
            public double Next() { Assert.That(calls, Is.LessThan(expected), "Unexpected draw"); calls++; return value; }
            public void Done() => Assert.That(calls, Is.EqualTo(expected), "Draw count");
        }

        [Test]
        public void OriginalSourceFixtureIsPinnedAndCatalogPreservesOrderWithoutWorldCoordinates()
        {
            using var hash = SHA256.Create();
            Assert.That(BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(FixturePath))).Replace("-", "").ToLowerInvariant(),
                Is.EqualTo("3e1997f03f97b69ef0a3b6a55c55d6f4219d804566e07173f3f28c17b6558cab"));
            Assert.That((string)Golden.Value["format"], Is.EqualTo("gamesim-npc-activity-leaf-v1"));
            var catalog = Golden.Value["catalog"];
            Same(catalog["points"], WebNpcActivityCatalog.Points, "points");
            Same(catalog["phases"], WebNpcActivityCatalog.Phases, "phases");
            Same(catalog["chains"], WebNpcActivityCatalog.Chains, "chains");
            Same(catalog["carryChains"], WebNpcActivityCatalog.CarryChains, "carryChains");
            Assert.That(typeof(WebNpcActivityPoint).GetFields().Select(field => field.Name),
                Is.EquivalentTo(new[] { "id", "label", "room", "activity", "capacity", "satisfaction" }));
        }

        [Test]
        public void EveryTraitRoomWeightPreservesDuplicatesUnknownTraitsAndEmptyInputs()
        {
            foreach (var row in Rows("traitCases", 130))
            {
                Assert.That(WebNpcActivityRules.TraitRoomBonus(Read<List<string>>(row["traits"]), (string)row["room"]), Is.EqualTo((double)row["output"]));
                Assert.That((int)row["draws"], Is.Zero);
            }
        }

        [Test]
        public void FurnitureScoresPreserveNegativeSatisfactionRoomEvidenceAndStrictSocialThresholds()
        {
            foreach (var row in Rows("scoreCases", 157))
            {
                var input = row["input"]; var motives = Read<WebNpcMotiveState>(input["motives"]);
                var point = Read<WebNpcActivityPoint>(input["point"]); var traits = Read<List<string>>(input["traits"]);
                var context = Read<WebNpcPointContext>(input["context"]); string before = Json(new { motives, point, traits, context });
                double actual = WebNpcActivityRules.ScorePoint(motives, point, (double)input["distance"], traits, context);
                // Only this weighted score accumulates the cross-runtime Math.Pow urgency leaf.
                Assert.That(actual, Is.EqualTo((double)row["output"]).Within(2e-13), (string)row["name"]);
                Assert.That(Json(new { motives, point, traits, context }), Is.EqualTo(before)); Assert.That((int)row["draws"], Is.Zero);
            }
        }

        [Test]
        public void WeightedTopChoiceMutatesEntireListStablyAndReturnsOriginalPointReference()
        {
            foreach (var row in Rows("topCases", 108))
            {
                var values = Read<List<WebNpcScoredPoint>>(row["input"]); var originals = values.ToArray();
                var draw = new Draw((double)row["roll"], (int)row["draws"]);
                var chosen = WebNpcActivityRules.PickFromTopScoredInPlace(values, draw.Next, (int)row["topN"]);
                Assert.That(chosen?.id, Is.EqualTo((string)row["output"]), (string)row["name"]);
                Assert.That(values.Select(value => value.point.id), Is.EqualTo(row["sortedIds"].Values<string>()), "Full source sort");
                if (chosen != null) Assert.That(chosen, Is.SameAs(originals.Single(value => value.point.id == chosen.id).point));
                Assert.That(values.All(value => originals.Any(original => ReferenceEquals(original, value))), Is.True);
                draw.Done();
            }
        }

        [Test]
        public void SeekScoreLeafPreservesAllModifiersAndPersonaArithmeticExactly()
        {
            foreach (var row in Rows("seekScoreCases", 100))
            {
                var input = row["input"];
                double actual = WebNpcActivityRules.ScoreSeekTarget(Read<WebNpcMotiveState>(input["motives"]),
                    (double)input["distance"], (double)input["relationship"], (bool)input["allied"], Read<List<string>>(input["traits"]),
                    (bool)input["isPlayer"], (string)input["playerPersonaLabel"]);
                Assert.That(actual, Is.EqualTo((double)row["output"]), (string)row["name"]); Assert.That((int)row["draws"], Is.Zero);
            }
        }

        [Test]
        public void ActualSeekBlockPreservesChanceOrderEligibilityIntentThresholdsAndFirstTie()
        {
            foreach (var row in Rows("seekCases", 240))
            {
                var input = row["input"]; var motives = Read<WebNpcMotiveState>(input["motives"]);
                var candidates = Read<List<WebNpcSeekCandidate>>(input["candidates"]); var traits = Read<List<string>>(input["traits"]);
                string before = Json(new { motives, candidates, traits }); var draw = new Draw((double)row["roll"], (int)row["draws"]);
                var choice = WebNpcActivityRules.SelectSeekTarget(motives, (string)input["npcId"], (string)input["playerId"], candidates, traits,
                    (double)input["bestFurnitureScore"], (double)input["millisecondsSinceLastChange"], (bool)input["hasSeekTarget"],
                    (bool)input["hasRelationshipMap"], draw.Next, (string)input["playerPersonaLabel"]);
                Same(row["output"], choice, (string)row["name"]); draw.Done();
                Assert.That(Json(new { motives, candidates, traits }), Is.EqualTo(before));
            }
        }

        [Test]
        public void OrderedChainsPreserveInclusiveChanceStrictThresholdsAndShadowedTowel()
        {
            foreach (var row in Rows("chainCases", 284))
            {
                var motives = Read<WebNpcMotiveState>(row["motives"]); string before = Json(motives);
                var draw = new Draw((double)row["roll"], (int)row["draws"]);
                Same(row["output"], WebNpcActivityRules.NextChainActivity((string)row["activity"], motives, draw.Next, new[] { "Unknown" }), (string)row["name"]);
                draw.Done(); Assert.That(Json(motives), Is.EqualTo(before));
            }
        }

        [Test]
        public void PhasesCarryAndDropMetadataPreserveUndefinedSocializingTransition()
        {
            foreach (var row in Rows("phaseCases", 72))
            {
                string activity = (string)row["activity"], phase = (string)row["phase"];
                Assert.That(WebNpcActivityRules.NextInteractionPhase(phase), Is.EqualTo((string)row["nextPhase"]));
                Assert.That(WebNpcActivityRules.PhaseDuration(activity, phase), Is.EqualTo((int)row["duration"]));
                Assert.That(WebNpcActivityRules.PhaseGesture(activity, phase), Is.EqualTo((string)row["gesture"]));
                Assert.That(WebNpcActivityRules.CarryItemOnStart(activity), Is.EqualTo((string)row["carry"]));
                Assert.That(WebNpcActivityRules.ShouldDropItemOnEnd(activity), Is.EqualTo((bool)row["drop"]));
            }
        }

        [Test]
        public void EveryCatalogResultIsDeeplyDetached()
        {
            var points = WebNpcActivityCatalog.Points; points[0].id = "changed"; points[0].satisfaction[0].amount = -999; points.RemoveAt(1);
            var phases = WebNpcActivityCatalog.Phases; phases[0].activity = "changed"; phases.Clear();
            var chains = WebNpcActivityCatalog.Chains; chains[0].steps[0] = "changed"; chains[0].carryItem = "changed";
            var carry = WebNpcActivityCatalog.CarryChains; carry[0].item = "changed"; carry.Clear();
            Same(Golden.Value["catalog"]["points"], WebNpcActivityCatalog.Points, "detached points");
            Same(Golden.Value["catalog"]["phases"], WebNpcActivityCatalog.Phases, "detached phases");
            Same(Golden.Value["catalog"]["chains"], WebNpcActivityCatalog.Chains, "detached chains");
            Same(Golden.Value["catalog"]["carryChains"], WebNpcActivityCatalog.CarryChains, "detached carry chains");
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void NonfiniteSuppliedArithmeticRejectsWithoutCallingRandom(double invalid)
        {
            var motives = Motives(); motives.social = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => WebNpcActivityRules.ScorePoint(motives, Point(), 0, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebNpcActivityRules.ScorePoint(Motives(), Point(), invalid, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebNpcActivityRules.ScoreSeekTarget(Motives(), 0, invalid, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebNpcActivityRules.SeekIntent(invalid, false, null));
            var draw = new Draw(0, 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => WebNpcActivityRules.NextChainActivity("unknown", motives, draw.Next));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebNpcActivityRules.SelectSeekTarget(Motives(), "self", "player", Array.Empty<WebNpcSeekCandidate>(),
                null, invalid, 0, false, true, draw.Next)); draw.Done();
        }

        [Test]
        public void InvalidDrawsAreRejectedButEmptyTopSelectionDoesNotRequireADraw()
        {
            Assert.That(WebNpcActivityRules.PickFromTopScoredInPlace(new List<WebNpcScoredPoint>(), null), Is.Null);
            foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -.001, 1, 2 })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => WebNpcActivityRules.NextChainActivity("unknown", Motives(), () => invalid));
                Assert.Throws<ArgumentOutOfRangeException>(() => WebNpcActivityRules.PickFromTopScoredInPlace(
                    new List<WebNpcScoredPoint> { new WebNpcScoredPoint { point = Point(), score = 1 } }, () => invalid));
            }
            Assert.Throws<ArgumentNullException>(() => WebNpcActivityRules.NextChainActivity("unknown", Motives(), null));
        }

        [Test]
        public void SuppliedInputBoundariesRejectImpossibleGeometryAndDuplicateMotiveKeys()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WebNpcActivityRules.ScorePoint(Motives(), Point(), -1, null));
            var point = Point(); point.satisfaction.Add(new WebNpcMotiveAmount("social", 1));
            Assert.Throws<ArgumentException>(() => WebNpcActivityRules.ScorePoint(Motives(), point, 0, null));
            point = Point(); point.satisfaction.Add(new WebNpcMotiveAmount("unknown", 1));
            Assert.Throws<ArgumentException>(() => WebNpcActivityRules.ScorePoint(Motives(), point, 0, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebNpcActivityRules.PickFromTopScoredInPlace(new List<WebNpcScoredPoint>(), () => 0, 0));
            Assert.That(WebNpcActivityRules.PhaseDuration("unknown", "active"), Is.Zero);
            Assert.That(WebNpcActivityRules.PhaseGesture("unknown", "active"), Is.Null);
            Assert.Throws<ArgumentException>(() => WebNpcActivityRules.CarryItemOnStart("unknown"));
        }

        [Test]
        public void MissingRelationshipMapStillConsumesChanceAndDoesNotApplyPlayerPersona()
        {
            var candidates = new[] { new WebNpcSeekCandidate { id = "target", state = "idle", relationship = 21 } };
            var missing = new Draw(0);
            Assert.That(WebNpcActivityRules.SelectSeekTarget(Motives(), "self", "player", candidates, null, 0, 40000, false, false, missing.Next), Is.Null);
            missing.Done(); var first = new Draw(0); var second = new Draw(0);
            var ruthless = WebNpcActivityRules.SelectSeekTarget(Motives(), "self", "player", candidates, null, 0, 40000, false, true, first.Next, "Ruthless");
            var butterfly = WebNpcActivityRules.SelectSeekTarget(Motives(), "self", "player", candidates, null, 0, 40000, false, true, second.Next, "Social Butterfly");
            Assert.That(Json(ruthless), Is.EqualTo(Json(butterfly))); first.Done(); second.Done();
        }

        [Test]
        public void SourceMapInputsRejectDuplicateAndNullIdsBeforeRandomEvenForFilteredActors()
        {
            foreach (string duplicate in new[] { "self", "player", "target", "" })
            {
                var context = new WebNpcPointContext { npcId = "self", actors = new List<WebNpcRoomPresence>
                {
                    new WebNpcRoomPresence { id = duplicate, sameRoom = false, allied = true },
                    new WebNpcRoomPresence { id = duplicate, sameRoom = false, allied = true }
                } };
                string before = Json(context);
                Assert.Throws<ArgumentException>(() => WebNpcActivityRules.ScorePoint(Motives(), Point(), 0, null, context));
                Assert.That(Json(context), Is.EqualTo(before));
                var candidates = new[]
                {
                    new WebNpcSeekCandidate { id = duplicate, state = "walking" },
                    new WebNpcSeekCandidate { id = duplicate, state = "walking" }
                };
                before = Json(candidates); var draw = new Draw(0, 0);
                Assert.Throws<ArgumentException>(() => WebNpcActivityRules.SelectSeekTarget(Motives(), "self", "player", candidates, null,
                    0, 0, false, false, draw.Next));
                Assert.That(Json(candidates), Is.EqualTo(before)); draw.Done();
            }
            var nullActor = new WebNpcPointContext { npcId = "self", actors = new List<WebNpcRoomPresence> { new WebNpcRoomPresence() } };
            Assert.Throws<ArgumentException>(() => WebNpcActivityRules.ScorePoint(Motives(), Point(), 0, null, nullActor));
            var noDraw = new Draw(0, 0);
            Assert.Throws<ArgumentException>(() => WebNpcActivityRules.SelectSeekTarget(Motives(), "self", "player",
                new[] { new WebNpcSeekCandidate { state = "idle" } }, null, 0, 0, false, true, noDraw.Next));
            Assert.Throws<ArgumentNullException>(() => WebNpcActivityRules.SelectSeekTarget(Motives(), null, null,
                Array.Empty<WebNpcSeekCandidate>(), null, 0, 0, false, true, noDraw.Next));
            Assert.Throws<ArgumentNullException>(() => WebNpcActivityRules.ScorePoint(Motives(), Point(), 0, null, new WebNpcPointContext()));
            Assert.Throws<ArgumentNullException>(() => WebNpcActivityRules.IsSeekCandidate("self", null, null, "idle"));
            Assert.Throws<ArgumentNullException>(() => WebNpcActivityRules.IsSeekCandidate(null, null, "target", "idle"));
            noDraw.Done();
        }

        [Test]
        public void SourceMapIdsUseOrdinalStringsWithoutNormalizationAndPlayerIdCanBeAbsent()
        {
            var context = new WebNpcPointContext { npcId = "self", actors = new List<WebNpcRoomPresence>
            {
                new WebNpcRoomPresence { id = "target", sameRoom = true, allied = true },
                new WebNpcRoomPresence { id = "Target", sameRoom = true, allied = true },
                new WebNpcRoomPresence { id = " target ", sameRoom = true, allied = true },
                new WebNpcRoomPresence { id = "", sameRoom = true, allied = true }
            } };
            double baseline = WebNpcActivityRules.ScorePoint(Motives(), Point(), 0, null);
            Assert.That(WebNpcActivityRules.ScorePoint(Motives(), Point(), 0, null, context), Is.EqualTo(baseline + 12));
            var candidates = context.actors.Select(actor => new WebNpcSeekCandidate { id = actor.id, state = "idle" }).ToArray();
            var draw = new Draw(0);
            Assert.That(WebNpcActivityRules.SelectSeekTarget(Motives(), "self", null, candidates, null, 0, 0, false, true, draw.Next).id,
                Is.EqualTo("target")); draw.Done();
        }
    }
}
