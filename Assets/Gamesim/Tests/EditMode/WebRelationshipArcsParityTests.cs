using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;
#if UNITY_5_3_OR_NEWER
using System.IO;
using UnityEngine;
#endif

namespace Gamesim.Tests.EditMode
{
    public sealed class WebRelationshipArcsParityTests
    {
#if UNITY_5_3_OR_NEWER
        [Test]
        public void ArcHistorySentimentIntensityEscalationAndNarrativeMatchOriginalSource()
        {
            VerifyFixture(JsonConvert.DeserializeObject<Goldens>(File.ReadAllText(Path.Combine(Application.dataPath,
                "Gamesim/Tests/EditMode/Fixtures/WebArcFixtures.json"))));
        }
#endif

        public static void VerifyFixture(Goldens fixture)
        {
            Assert.That(fixture.version, Is.EqualTo(1));
            Assert.That(fixture.cases, Has.Length.EqualTo(5));
            Assert.That(fixture.cases.Sum(item => item.steps.Length), Is.EqualTo(40));
            foreach (var program in fixture.cases)
            {
                var arcs = new List<RelationshipArcState>();
                foreach (var step in program.steps)
                {
                    var before = arcs.Select(arc => arc.Clone()).ToArray();
                    var input = step.input;
                    var actual = WebRelationshipArcs.Update(arcs, input.npcId, input.npcName, input.delta, input.reason, input.week);
                    Assert.That(actual.arcs.Count, Is.EqualTo(step.arcs.Length), program.name);
                    for (int index = 0; index < actual.arcs.Count; index++) SameArc(actual.arcs[index], step.arcs[index], program.name);
                    for (int index = 0; index < arcs.Count; index++) SameArc(arcs[index], before[index], program.name + " input was mutated");
                    if (step.escalationEvent == null) Assert.That(actual.escalationEvent, Is.Null, program.name);
                    else
                    {
                        Assert.That(actual.escalationEvent, Is.Not.Null, program.name);
                        Assert.That(actual.escalationEvent.type, Is.EqualTo(step.escalationEvent.type), program.name);
                        Assert.That(actual.escalationEvent.npcName, Is.EqualTo(step.escalationEvent.npcName), program.name);
                        Assert.That(actual.escalationEvent.level, Is.EqualTo(step.escalationEvent.level), program.name);
                    }
                    Assert.That(WebRelationshipArcs.Narrative(actual.arcs[0]), Is.EqualTo(step.narrative), program.name);
                    arcs = actual.arcs;
                }
            }
            foreach (var threshold in fixture.thresholds)
                Assert.That(WebRelationshipArcs.GetEscalationLevel(threshold.value), Is.EqualTo(threshold.level));
            foreach (var label in fixture.labels)
                Assert.That(WebRelationshipArcs.GetEscalationLabel(label.value), Is.EqualTo(label.label));
        }

        [Test]
        public void EscalationUsesTheRetainedArcNameAndReturnedHistoryIsDetached()
        {
            var input = new[] { new RelationshipArcState { npcId = "npc", npcName = "Original", intensity = 20,
                weeklyHistory = new List<ArcHistory> { new ArcHistory { week = 1, delta = 10, reason = "first" } } } };
            var result = WebRelationshipArcs.Update(input, "npc", "Renamed argument", 10, "second", 2);
            Assert.That(result.escalationEvent.npcName, Is.EqualTo("Original"));
            Assert.That(result.arcs[0].npcName, Is.EqualTo("Original"));
            result.arcs[0].weeklyHistory[0].reason = "changed result";
            Assert.That(input[0].weeklyHistory[0].reason, Is.EqualTo("first"));
            Assert.That(input[0].weeklyHistory.Count, Is.EqualTo(1));
            Assert.That(input[0].intensity, Is.EqualTo(20));
        }

        [Test]
        public void NativeArcGuardsRejectNonFiniteChangesWithoutMutatingExistingHistory()
        {
            var arc = new RelationshipArcState { npcId = "npc", npcName = "NPC" };
            Assert.Throws<ArgumentOutOfRangeException>(() => WebRelationshipArcs.Update(new[] { arc }, "npc", "NPC", double.NaN, "bad", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebRelationshipArcs.Update(new[] { arc }, "npc", "NPC", double.PositiveInfinity, "bad", 1));
            Assert.That(arc.weeklyHistory, Is.Empty);
            Assert.That(WebRelationshipArcs.Update(null, "npc", "NPC", 1, "first", 1).arcs.Count, Is.EqualTo(1));
        }

        private static void SameArc(RelationshipArcState actual, RelationshipArcState expected, string label)
        {
            Assert.That(actual.npcId, Is.EqualTo(expected.npcId), label);
            Assert.That(actual.npcName, Is.EqualTo(expected.npcName), label);
            Assert.That(actual.arcType, Is.EqualTo(expected.arcType), label);
            Assert.That(actual.intensity, Is.EqualTo(expected.intensity).Within(1e-10), label);
            Assert.That(actual.escalationLevel, Is.EqualTo(expected.escalationLevel), label);
            Assert.That(actual.weeklyHistory.Count, Is.EqualTo(expected.weeklyHistory.Count), label);
            for (int index = 0; index < actual.weeklyHistory.Count; index++)
            {
                Assert.That(actual.weeklyHistory[index].week, Is.EqualTo(expected.weeklyHistory[index].week), label);
                Assert.That(actual.weeklyHistory[index].delta, Is.EqualTo(expected.weeklyHistory[index].delta).Within(1e-12), label);
                Assert.That(actual.weeklyHistory[index].reason, Is.EqualTo(expected.weeklyHistory[index].reason), label);
            }
        }

        [Serializable] public sealed class Goldens { public int version; public ArcCase[] cases; public Threshold[] thresholds; public Label[] labels; }
        [Serializable] public sealed class ArcCase { public string name; public ArcStep[] steps; }
        [Serializable] public sealed class ArcStep { public ArcInput input; public RelationshipArcState[] arcs; public ArcEscalation escalationEvent; public string narrative; }
        [Serializable] public sealed class ArcInput { public string npcId, npcName, reason; public double delta; public int week; }
        [Serializable] public sealed class Threshold { public double value; public int level; }
        [Serializable] public sealed class Label { public int value; public string label; }
    }
}
