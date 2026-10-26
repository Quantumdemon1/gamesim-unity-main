using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// SCH30's pooled schema 30 (ALL-STAR-HISTORIES-PLAN 5.1, 5.3): the live root is a schema-29 writer's root plus
    /// exactly the three pooled fields, appended after the dialogue store in their fixed order, with their types and off
    /// defaults; a commitment row gains nothing (AS3's association was not added); and live validation accepts only the
    /// off values until each field's own slice widens it.
    /// </summary>
    public sealed class PooledSchema30ShapeTests
    {
        private const BindingFlags Fields = BindingFlags.Public | BindingFlags.Instance;
        private static readonly string[] Pooled = { "alumniRulesVersion", "alumniCatalogEdition", "tuningRulesVersion" };

        [TestCase("s29-default-w2-social")] [TestCase("s29-mode1-allweek-w3-veto-meeting")] [TestCase("s29-mode2-allweek-w3-campaign")]
        public void TheLiveRootIsASchemaTwentyNineWritersRootPlusExactlyThePooledThreeInOrder(string id)
        {
            var former = Fixture(id);
            Assert.That((int)former["schemaVersion"], Is.EqualTo(29));
            Assert.That(OrderedFields(typeof(EpisodeState)).Select(f => f.Name),
                Is.EqualTo(former.Properties().Select(p => p.Name).Concat(Pooled)));
            var row = ((JArray)former["unifiedCommitments"]).OfType<JObject>().FirstOrDefault();
            if (row != null)
                Assert.That(OrderedFields(typeof(UnifiedCommitmentState)).Select(f => f.Name), Is.EqualTo(row.Properties().Select(p => p.Name)),
                    "A commitment row gains no field at schema 30: AS3's association is not in the pool.");
        }

        [Test]
        public void ThePooledFieldsHaveTheirTypesAndOffDefaults()
        {
            Assert.That(EpisodeState.LiveSchema, Is.EqualTo(30));
            var fresh = new EpisodeState();
            Assert.That(fresh.schemaVersion, Is.EqualTo(EpisodeState.LiveSchema));
            Assert.That(Member("alumniRulesVersion").FieldType, Is.EqualTo(typeof(int)));
            Assert.That(Member("alumniCatalogEdition").FieldType, Is.EqualTo(typeof(string)));
            Assert.That(Member("tuningRulesVersion").FieldType, Is.EqualTo(typeof(int)));
            Assert.That(fresh.alumniRulesVersion, Is.Zero);
            Assert.That(fresh.alumniCatalogEdition, Is.Null);
            Assert.That(fresh.tuningRulesVersion, Is.Zero);
            var names = ShippedRules.Fields(fresh).Select(f => f.Key).ToList();
            int dialogue = names.IndexOf("dialogueRulesVersion");
            Assert.That(names.Skip(dialogue + 1).Take(2), Is.EqualTo(new[] { "alumniRulesVersion", "tuningRulesVersion" }),
                "Both pooled rule ints are rule boundaries; the edition is content, not a rule field.");
        }

        [TestCase(false)] [TestCase(true)]
        public void DefaultFactoriesAdoptSchemaThirtyWithEveryPooledFieldOff(bool builder)
        {
            var state = builder ? SeasonBuilder.Create(new SeasonBuilder.Choice(), 3001u) : ContentCatalog.Create(3001u);
            Assert.That(state.schemaVersion, Is.EqualTo(30));
            Assert.That(state.alumniRulesVersion, Is.Zero);
            Assert.That(state.alumniCatalogEdition, Is.Null);
            Assert.That(state.tuningRulesVersion, Is.Zero);
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
        }

        [TestCase("alumniRulesVersion", 1)] [TestCase("alumniRulesVersion", -1)] [TestCase("alumniRulesVersion", int.MaxValue)]
        [TestCase("tuningRulesVersion", 1)] [TestCase("tuningRulesVersion", -1)]
        [TestCase("alumniCatalogEdition", "allstar-histories-1")] [TestCase("alumniCatalogEdition", "")]
        public void LiveValidationAcceptsOnlyTheOffValuesUntilEachSliceDefinesOne(string field, object value)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8, Roster = CastTemplates.Roster.AllStars }, 3002u);
            ShippedRules.ApplyFresh(state);
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
            var bad = state.Clone();
            Member(field).SetValue(bad, value);
            Assert.That(EpisodeValidation.TryValidate(bad, out reason), Is.False, field + " = " + value);
            Assert.That(reason, Is.EqualTo("Schema 30's pooled rules are not enabled in this build."));
            Assert.That(EpisodeValidation.TryValidate(state, out reason), Is.True, "The control is untouched: " + reason);
        }

        [TestCase(29)] [TestCase(31)]
        public void TheLiveValidatorHoldsASeasonToTheLiveSchemaOnly(int schema)
        {
            var state = ContentCatalog.Create(3003u);
            state.schemaVersion = schema;
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("Unsupported episode schema."));
        }

        [Test]
        public void DialogueRulesReadTheSchemaThatIntroducedThemAsALowerBound()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 3004u);
            ShippedRules.ApplyFresh(state);
            state.dialogueRulesVersion = 1;
            Assert.That(DialogueRules.On(state), Is.True, "Schema 30 carries the schema-29 dialogue store as it is.");
            Assert.That(DialogueRules.TryValidate(state, out string reason), Is.True, reason);
            Assert.That(EpisodeValidation.TryValidate(state, out reason), Is.True, reason);
            state.schemaVersion = 28;
            Assert.That(DialogueRules.On(state), Is.False);
            Assert.That(DialogueRules.TryValidate(state, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("Unsupported dialogue schema."));
        }

        private static FieldInfo[] OrderedFields(Type type) => type.GetFields(Fields).OrderBy(f => f.MetadataToken).ToArray();

        private static FieldInfo Member(string name)
        {
            var field = typeof(EpisodeState).GetField(name, Fields);
            Assert.That(field, Is.Not.Null, "Missing pooled field " + name);
            return field;
        }

        private static JObject Fixture(string id)
        {
#if UNITY_EDITOR
            string root = Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "OldSaves");
#else
            string root = Path.Combine(AppContext.BaseDirectory, "OldSaves");
#endif
            return (JObject)JObject.Parse(File.ReadAllText(Path.Combine(root, id + ".save.json")))["state"];
        }
    }
}
