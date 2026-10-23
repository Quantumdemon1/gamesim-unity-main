using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// R1 shape/clone controls, shared by native Edit Mode and the existing pure subset.
    /// Reflection intentionally compiles at schema28 and fails on the missing reviewed contract.
    /// Populated clone sentinels are NOT playable histories or evidence of dialogue gameplay.
    /// </summary>
    public sealed class DialogueSchema29ShapeTests
    {
        private const BindingFlags Fields = BindingFlags.Public | BindingFlags.Instance;
        private static readonly string[] Shapes =
        {
            "DialogueState|episodes:List<DialogueEpisodeState>|grants:List<DialogueRoleGrantState>|vetoOccasions:List<DialogueVetoOccasionState>",
            "DialogueEpisodeState|id:string|npcId:string|occasionKind:string|occasionId:string|entryEventId:string|contentVersion:int|week:int|window:int|startedRevision:int|startedSequence:int|startedPhase:EpisodePhase|entry:DialogueEntryReceiptState|responses:List<DialogueResponseState>|evidence:List<DialogueEvidenceState>|statement:DialogueStatementState|time:DialogueTimeReceiptState",
            "DialogueEntryReceiptState|kind:string|grantId:string|revision:int|windowActionBefore:int|windowActionAfter:int",
            "DialogueResponseState|eventId:string|speakerId:string|responseKey:string|pathIndex:int|revision:int",
            "DialogueEvidenceState|id:string|kind:string|eventId:string|pathIndex:int|capturedRevision:int|power:PowerRow|memory:MemoryState|statementId:string",
            "DialogueStatementState|id:string|eventId:string|speakerId:string|targetId:string|answer:string|conditionKind:string|pathIndex:int|week:int|revision:int|sequence:int|audienceIds:List<string>",
            "DialogueRoleGrantState|id:string|ownerId:string|role:string|sourceKind:string|status:string|consumedEpisodeId:string|expiryReason:string|week:int|sourceRevision:int|sourceSequence:int|consumedRevision:int|expiredRevision:int|sourcePhase:EpisodePhase|sourceNominees:List<string>",
            "DialogueTimeReceiptState|revision:int|sequence:int|window:int|proposedDelta:int|appliedDelta:int|reason:string|spentRevision:int|spentSequence:int|spentWindowAction:int",
            "DialogueVetoOccasionState|week:int|resultRevision:int|resultSequence:int|hohId:string|holderId:string|participants:List<string>|nomineesAtResult:List<string>|decision:DialogueVetoDecisionState",
            "DialogueVetoDecisionState|revision:int|sequence:int|activeIds:List<string>|nominees:List<string>|replacementCandidates:List<string>|used:bool|savedId:string|replacementId:string",
            "ConditionalVetoTermsState|statementId:string|consentEventId:string|consentPathIndex:int|consentRevision:int|resolution:ConditionalVetoResolutionState",
            "ConditionalVetoResolutionState|reason:string|departedId:string|departedStatus:string|revision:int|sequence:int|vetoOccasionWeek:int"
        };

        [TestCaseSource(nameof(Shapes))]
        public void ReviewedDtoHasExactlyTheWireFieldsTypesAndDefaults(string shape)
        {
            var pieces = shape.Split('|');
            Type type = ContractType(pieces[0]);
            Assert.That(type.IsPublic && type.IsSealed, Is.True, pieces[0]);
            Assert.That(type.GetCustomAttributes(typeof(SerializableAttribute), false), Has.Length.EqualTo(1));
            Assert.That(type.GetProperties(Fields), Is.Empty, "No competing property-based wire owner.");
            var fields = OrderedFields(type);
            Assert.That(fields.Select(f => f.Name), Is.EqualTo(pieces.Skip(1).Select(p => p.Split(':')[0])));
            object instance = Activator.CreateInstance(type);
            for (int i = 0; i < fields.Length; i++)
            {
                Type expected = ContractType(pieces[i + 1].Split(':')[1]);
                Assert.That(fields[i].FieldType, Is.EqualTo(expected), fields[i].Name);
                object value = fields[i].GetValue(instance);
                if (typeof(IList).IsAssignableFrom(expected))
                    Assert.That(value, Is.Not.Null.And.Empty, fields[i].Name);
                else if (!expected.IsValueType)
                    Assert.That(value, Is.Null, "Optional payload/string defaults remain literal null: " + fields[i].Name);
                else Assert.That(value, Is.EqualTo(Activator.CreateInstance(expected)), fields[i].Name);
            }
        }

        [Test]
        public void ExistingRootAndCanonicalWireInventoriesGainOnlyReviewedAppendages()
        {
            var former = HistoricalPayload("s28-mode2-allweek-w3-campaign");
            var row = ((JArray)former["unifiedCommitments"]).OfType<JObject>().First();
            Assert.That(OrderedFields(typeof(EpisodeState)).Select(f => f.Name),
                Is.EqualTo(former.Properties().Select(p => p.Name).Concat(new[] { "dialogueRulesVersion", "dialogue" })));
            Assert.That(OrderedFields(typeof(UnifiedCommitmentState)).Select(f => f.Name),
                Is.EqualTo(row.Properties().Select(p => p.Name).Concat(new[] { "conditionalVeto" })));
            Assert.That(Member(typeof(EpisodeState), "dialogueRulesVersion").FieldType, Is.EqualTo(typeof(int)));
            Assert.That(Member(typeof(EpisodeState), "dialogue").FieldType, Is.EqualTo(ContractType("DialogueState")));
            Assert.That(Member(typeof(UnifiedCommitmentState), "conditionalVeto").FieldType,
                Is.EqualTo(ContractType("ConditionalVetoTermsState")));
            var state = ContentCatalog.Create(2901);
            var rules = ShippedRules.Fields(state).Where(rowValue => rowValue.Key == "dialogueRulesVersion").ToArray();
            Assert.That(rules, Has.Length.EqualTo(1));
            Assert.That(rules[0].Value, Is.Zero, "Reporting the boundary is not enrolling an old/default season.");
        }

        [TestCase(false)] [TestCase(true)]
        public void DefaultFactoriesAdoptSchema29ButLeaveAllDialogueStorageInert(bool builder)
        {
            var state = builder ? SeasonBuilder.Create(new SeasonBuilder.Choice(), 2902) : ContentCatalog.Create(2902);
            Accepted29(state);
            Assert.That(Value(state, "dialogueRulesVersion"), Is.EqualTo(0));
            object dialogue = Value(state, "dialogue");
            Assert.That(dialogue, Is.Not.Null);
            foreach (string name in new[] { "episodes", "grants", "vetoOccasions" })
                Assert.That(Value(dialogue, name), Is.Not.Null.And.Empty, name);
            Assert.That(Value(new UnifiedCommitmentState(), "conditionalVeto"), Is.Null);
        }

        // The enabled semantic owner accepts only an explicitly selected unpublished pilot.
        // Fresh defaults below still leave the new storage inert; this is not shipping activation.
        [Test]
        public void ExplicitRuleOneAcceptsEmptyStorageWithCompleteFreshPrerequisitesWithoutMutation()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 2931u);
            ShippedRules.ApplyFresh(state);
            Accepted29(state);
            AssertFoundationFreshPrerequisites(state);
            Assert.That(Value(state, "dialogueRulesVersion"), Is.EqualTo(0));
            string accepted = Json(state);
            try
            {
                // Only the new boundary changes; validation must not select it on anyone's behalf.
                Put(state, "dialogueRulesVersion", 1);
                string selected = Json(state);
                Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
                Assert.That(Json(state), Is.EqualTo(selected),
                    "Validation must not enroll, repair, erase, spend, draw, or mutate the selected input.");
            }
            finally { Put(state, "dialogueRulesVersion", 0); }
            Accepted29(state);
            Assert.That(Json(state), Is.EqualTo(accepted),
                "Restoring only the probed rule leaves the complete accepted fresh state intact.");
        }

        [Test]
        public void FoundationApplyFreshKeepsDialogueOffWithoutAuthoringNewStorage()
        {
            var original = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 2932u);
            Accepted29(original);
            string untouched = Json(original);
            var fresh = original.Clone();
            string emptyDialogue = Json(Value(fresh, "dialogue"));
            uint random = fresh.randomState;
            int sequence = fresh.nextSequence;
            string events = Json(fresh.events);
            string windowActions = Json(fresh.windowActions);
            // Exercise the real shipped-rule owner, not a hand-written copy of its initialization.
            ShippedRules.ApplyFresh(fresh);
            Accepted29(fresh);
            AssertFoundationFreshPrerequisites(fresh);
            Assert.That(Value(fresh, "dialogueRulesVersion"), Is.EqualTo(0));
            Assert.That(Json(Value(fresh, "dialogue")), Is.EqualTo(emptyDialogue));
            foreach (string name in new[] { "episodes", "grants", "vetoOccasions" })
                Assert.That(Value(Value(fresh, "dialogue"), name), Is.Not.Null.And.Empty, name);
            Assert.That(fresh.unifiedCommitments, Is.Empty, "Fresh setup creates no conditional or old canonical duty.");
            Assert.That(fresh.randomState, Is.EqualTo(random), "Fresh setup draws no gameplay roll.");
            Assert.That(fresh.nextSequence, Is.EqualTo(sequence), "Fresh setup allocates no source identity.");
            Assert.That(Json(fresh.events), Is.EqualTo(events));
            Assert.That(Json(fresh.windowActions), Is.EqualTo(windowActions));
            Assert.That(Json(original), Is.EqualTo(untouched), "The detached fresh setup cannot mutate its caller's source.");
            string settled = Json(fresh);
            var reported = ShippedRules.Fields(fresh).Where(row => row.Key == "dialogueRulesVersion").ToArray();
            Assert.That(reported, Has.Length.EqualTo(1));
            Assert.That(reported[0].Value, Is.Zero);
            Accepted29(fresh);
            Assert.That(Json(fresh), Is.EqualTo(settled), "Reporting and validation are read-only.");
        }

        private static void AssertFoundationFreshPrerequisites(EpisodeState state)
        {
            Assert.That(state.week, Is.EqualTo(1));
            Assert.That(state.phase, Is.EqualTo(EpisodePhase.Social));
            Assert.That(state.revision, Is.Zero);
            Assert.That(state.acceptedCommandIds, Is.Empty);
            Assert.That(state.socialActions, Is.Zero);
            Assert.That(state.moveInExtrasSpent, Is.Zero);
            Assert.That(state.windowActions, Is.Not.Null);
            Assert.That(state.windowActions, Is.All.EqualTo(0));
            Assert.That(state.unifiedCommitmentRulesVersion, Is.EqualTo(2));
            Assert.That(state.unifiedHearingRulesVersion, Is.EqualTo(1));
            Assert.That(state.economyRulesVersion, Is.EqualTo(1));
            Assert.That(state.story, Is.Not.Null);
            Assert.That(state.story.rulesVersion, Is.EqualTo(9));
            var fields = ShippedRules.Fields(state).ToDictionary(row => row.Key, row => row.Value);
            foreach (string name in new[]
            {
                "readRulesStartWeek", "leverRulesStartWeek", "weekRulesStartWeek", "strategyRulesStartWeek",
                "commitmentRulesStartWeek", "agencyRulesStartWeek", "allWeekRulesStartWeek", "dealRulesStartWeek",
                "storyRulesStartWeek", "story.rulesStartWeek", "npcSocial.rulesStartWeek"
            })
                Assert.That(fields[name], Is.EqualTo(1), "The reviewed fresh prerequisite is present: " + name);
        }

        [TestCase(-1)] [TestCase(2)] [TestCase(int.MaxValue)]
        public void UnknownDialogueRuleRefusesEvenWithEmptyStorageWithoutRepair(int version)
        {
            var state = ContentCatalog.Create(2903); Accepted29(state);
            Put(state, "dialogueRulesVersion", version); RefusedUnchanged(state);
        }

        [TestCase("dialogue")] [TestCase("episodes")] [TestCase("grants")] [TestCase("vetoOccasions")]
        public void RequiredDialogueStorageCannotBeNormalizedFromNull(string name)
        {
            var state = ContentCatalog.Create(2904); Accepted29(state);
            if (name == "dialogue") Put(state, name, null);
            else Put(Value(state, "dialogue"), name, null);
            RefusedUnchanged(state);
        }

        [TestCase("episodes")] [TestCase("grants")] [TestCase("vetoOccasions")] [TestCase("conditionalVeto")]
        public void DisabledRulesNeverAdmitAnyNewStoreOrCanonicalTerms(string name)
        {
            var payload = HistoricalPayload("s28-mode2-allweek-w3-campaign");
            payload["schemaVersion"] = 29;
            payload.Add("dialogueRulesVersion", 0);
            payload.Add("dialogue", JObject.Parse("{'episodes':[],'grants':[],'vetoOccasions':[]}"));
            foreach (JObject oldRow in (JArray)payload["unifiedCommitments"]) oldRow.Add("conditionalVeto", JValue.CreateNull());
            var state = payload.ToObject<EpisodeState>(JsonSerializer.Create(new JsonSerializerSettings
            {
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                MissingMemberHandling = MissingMemberHandling.Error, DateParseHandling = DateParseHandling.None
            })); Accepted29(state);
            if (name == "conditionalVeto")
            {
                var row = state.unifiedCommitments.First();
                Put(row, name, Populated(ContractType("ConditionalVetoTermsState")));
            }
            else
            {
                var list = (IList)Value(Value(state, "dialogue"), name);
                list.Add(Populated(list.GetType().GetGenericArguments()[0]));
            }
            RefusedUnchanged(state);
        }

        [Test]
        public void EpisodeCloneDetachesEveryNestedDialoguePayloadListAndPowerSnapshot()
        {
            var state = ContentCatalog.Create(2906); Accepted29(state);
            Put(state, "dialogue", Populated(ContractType("DialogueState")));
            var row = new UnifiedCommitmentState { id = "clone-sentinel", kind = "conditional-veto" };
            Put(row, "conditionalVeto", Populated(ContractType("ConditionalVetoTermsState")));
            state.unifiedCommitments.Add(row);
            string before = Json(state);
            var copy = state.Clone();
            Assert.That(Json(copy), Is.EqualTo(before));
            AssertDetached(Value(state, "dialogue"), Value(copy, "dialogue"), "dialogue");
            AssertDetached(state.unifiedCommitments[0], copy.unifiedCommitments[0], "canonical");
            Mutate(Value(copy, "dialogue"));
            Mutate(copy.unifiedCommitments[0]);
            Assert.That(Json(copy), Is.Not.EqualTo(before));
            Assert.That(Json(state), Is.EqualTo(before), "Every sentinel, including PowerRow nominees/tally, remains owned by the original.");
        }

        [Test]
        public void CanonicalRowCloneCopiesTermsAndResolutionInsteadOfSharingThem()
        {
            var original = new UnifiedCommitmentState { id = "row-clone" };
            Put(original, "conditionalVeto", Populated(ContractType("ConditionalVetoTermsState")));
            string before = Json(original);
            var copy = original.Clone();
            Assert.That(Json(copy), Is.EqualTo(before));
            AssertDetached(original, copy, "canonical");
            Mutate(copy);
            Assert.That(Json(copy), Is.Not.EqualTo(before));
            Assert.That(Json(original), Is.EqualTo(before));
        }

        private static void Accepted29(EpisodeState state)
        {
            Assert.That(state.schemaVersion, Is.EqualTo(29), "R1 is not adopted yet; never mask this as an ignored test.");
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
        }
        private static void RefusedUnchanged(EpisodeState state)
        {
            string before = Json(state);
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.False);
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
            Assert.That(Json(state), Is.EqualTo(before));
        }
        private static Type ContractType(string name)
        {
            if (name == "int") return typeof(int);
            if (name == "bool") return typeof(bool);
            if (name == "string") return typeof(string);
            if (name.StartsWith("List<", StringComparison.Ordinal))
                return typeof(List<>).MakeGenericType(ContractType(name.Substring(5, name.Length - 6)));
            var type = typeof(EpisodeState).Assembly.GetType("Gamesim.Simulation." + name, false);
            Assert.That(type, Is.Not.Null, "Missing reviewed R1 type " + name);
            return type;
        }
        private static FieldInfo[] OrderedFields(Type type) => type.GetFields(Fields).OrderBy(f => f.MetadataToken).ToArray();
        private static FieldInfo Member(Type type, string name)
        {
            var field = type.GetField(name, Fields);
            Assert.That(field, Is.Not.Null, "Missing reviewed field " + type.Name + "." + name);
            return field;
        }
        private static object Value(object owner, string name) => Member(owner.GetType(), name).GetValue(owner);
        private static void Put(object owner, string name, object value) => Member(owner.GetType(), name).SetValue(owner, value);

        // Deliberately populated clone sentinels, including mutually exclusive optional payloads.
        // This does not claim a valid episode, consent, history, grant or gameplay transition.
        private static object Populated(Type type)
        {
            if (type == typeof(string)) return "clone-owned";
            if (type == typeof(int)) return 7;
            if (type == typeof(bool)) return true;
            if (type == typeof(double)) return 7.25;
            if (type.IsEnum) return Enum.GetValues(type).GetValue(0);
            var value = Activator.CreateInstance(type);
            if (value is IList list) list.Add(Populated(type.GetGenericArguments()[0]));
            else foreach (var field in OrderedFields(type)) field.SetValue(value, Populated(field.FieldType));
            return value;
        }
        private static void AssertDetached(object original, object copy, string path)
        {
            Assert.That(copy, Is.Not.Null, path);
            Assert.That(copy.GetType(), Is.EqualTo(original.GetType()), path);
            if (original is string || original.GetType().IsValueType)
            { Assert.That(copy, Is.EqualTo(original), path); return; }
            Assert.That(copy, Is.Not.SameAs(original), path);
            if (original is IList oldList)
            {
                var newList = (IList)copy;
                Assert.That(newList.Count, Is.EqualTo(oldList.Count), path);
                for (int i = 0; i < oldList.Count; i++) AssertDetached(oldList[i], newList[i], path + "[" + i + "]");
            }
            else foreach (var field in OrderedFields(original.GetType()))
            {
                object oldValue = field.GetValue(original), newValue = field.GetValue(copy);
                if (oldValue == null) Assert.That(newValue, Is.Null, path + "." + field.Name);
                else AssertDetached(oldValue, newValue, path + "." + field.Name);
            }
        }
        private static void Mutate(object owner)
        {
            if (owner is IList list)
            {
                foreach (object value in list)
                    if (value != null && !(value is string) && !value.GetType().IsValueType) Mutate(value);
                list.Clear(); return;
            }
            foreach (var field in OrderedFields(owner.GetType()))
            {
                object value = field.GetValue(owner);
                if (value == null) continue;
                if (field.FieldType == typeof(string)) field.SetValue(owner, value + "-changed");
                else if (field.FieldType == typeof(int)) field.SetValue(owner, (int)value + 1);
                else if (field.FieldType == typeof(bool)) field.SetValue(owner, !(bool)value);
                else if (field.FieldType == typeof(double)) field.SetValue(owner, (double)value + 1);
                else if (!field.FieldType.IsValueType) Mutate(value);
            }
        }
        private static JObject HistoricalPayload(string id)
        {
#if UNITY_EDITOR
            string root = Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "OldSaves");
#else
            string root = Path.Combine(AppContext.BaseDirectory, "OldSaves");
#endif
            return (JObject)JObject.Parse(File.ReadAllText(Path.Combine(root, id + ".save.json")))["state"];
        }
        private static string Json(object value) => JsonConvert.SerializeObject(value, Formatting.None);
    }
}
