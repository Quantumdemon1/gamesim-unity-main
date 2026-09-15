using System;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;
#if UNITY_5_3_OR_NEWER
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
#endif

namespace Gamesim.Tests.EditMode
{
    public sealed class WebJurySentimentParityTests
    {
#if UNITY_5_3_OR_NEWER
        [Test]
        public void JuryLedgerUpdatesClampsHistoriesAndMeansMatchOriginalSource()
        {
            VerifyFixture(JsonConvert.DeserializeObject<Goldens>(File.ReadAllText(Path.Combine(Application.dataPath,
                "Gamesim/Tests/EditMode/Fixtures/WebJurySentimentFixtures.json"))));
        }
#endif

        public static void VerifyFixture(Goldens fixture)
        {
            Assert.That(fixture.schemaVersion, Is.EqualTo(1));
            Assert.That(fixture.programs, Has.Length.EqualTo(20));
            Assert.That(fixture.programs.Sum(program => program.steps.Length), Is.EqualTo(90));
            SameState(WebJurySentiment.CreateInitial(), fixture.initial, "initial");
            foreach (var program in fixture.programs)
            {
                var state = WebJurySentiment.CreateInitial();
                foreach (var step in program.steps)
                {
                    var before = state.Clone();
                    var input = step.input;
                    var actual = input.operation == "add" ? WebJurySentiment.AddJuror(state, input.jurorId, input.jurorName, input.relationshipScore)
                        : input.operation == "update" ? WebJurySentiment.UpdateJurorSentiment(state, input.jurorId, input.delta, input.reason, input.week)
                        : WebJurySentiment.ShiftAllJurorSentiment(state, input.delta, input.reason, input.week);
                    SameState(actual, step.expected, program.name);
                    SameState(state, before, program.name + " input changed");
                    Assert.That(ReferenceEquals(state, actual), Is.EqualTo(step.expectedSameReference), program.name);
                    Assert.That(actual.jurors.Where(juror => IsNegativeZero(juror.sentiment)).Select(juror => juror.jurorId),
                        Is.EqualTo(step.expectedNegativeZeroJurorIds), program.name);
                    Assert.That(IsNegativeZero(actual.overallSentiment), Is.EqualTo(step.expectedNegativeZeroOverall), program.name);
                    state = actual;
                }
            }
        }

        [Test]
        public void EmptyLedgerDoesNotBankDiaryShiftsAndAddOverwritesRatherThanDoubleCounting()
        {
            var empty = WebJurySentiment.CreateInitial();
            var shifted = WebJurySentiment.ShiftAllJurorSentiment(empty, 5, "Diary Room: Remorseful", 2);
            Assert.That(shifted.jurors, Is.Empty);
            Assert.That(shifted.overallSentiment, Is.Zero);
            var first = WebJurySentiment.AddJuror(shifted, "npc", "Original", 0);
            Assert.That(first.jurors[0].sentiment, Is.Zero, "Earlier empty-ledger shifts do not affect a future juror.");
            var changed = WebJurySentiment.ShiftAllJurorSentiment(first, 5, "Diary Room: Remorseful", 3);
            var replaced = WebJurySentiment.AddJuror(changed, "npc", "New name", 5);
            Assert.That(replaced.jurors.Count, Is.EqualTo(1));
            Assert.That(replaced.jurors[0].sentiment, Is.EqualTo(3));
            Assert.That(replaced.jurors[0].events.Count, Is.EqualTo(1));
            Assert.That(replaced.jurors[0].events[0].reason, Is.EqualTo("Entered jury"));
            Assert.That(changed.jurors[0].events.Count, Is.EqualTo(2));
        }

        [Test]
        public void ClampedSentimentPreservesRawEventDeltaAndDeeplyDetachedHistory()
        {
            var state = WebJurySentiment.AddJuror(WebJurySentiment.CreateInitial(), "npc", "NPC", 100);
            var result = WebJurySentiment.ShiftAllJurorSentiment(state, 100, "huge shift", 4);
            Assert.That(result.jurors[0].sentiment, Is.EqualTo(100));
            Assert.That(result.jurors[0].events[1].delta, Is.EqualTo(100), "Raw delta is not replaced by effective +50.");
            result.jurors[0].events[0].reason = "result-only mutation";
            Assert.That(state.jurors[0].events[0].reason, Is.EqualTo("Entered jury"));
            Assert.That(state.jurors[0].sentiment, Is.EqualTo(50));
        }

        [Test]
        public void MissingJurorIsNoOpAndMalformedNativeInputsFailBeforeMutation()
        {
            var state = WebJurySentiment.CreateInitial();
            Assert.That(WebJurySentiment.UpdateJurorSentiment(state, "missing", 5, "ignored", 1), Is.SameAs(state));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebJurySentiment.AddJuror(state, "npc", "NPC", double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebJurySentiment.ShiftAllJurorSentiment(state, double.PositiveInfinity, "invalid", 1));
            Assert.That(state.jurors, Is.Empty);
            var duplicate = WebJurySentiment.AddJuror(state, "npc", "NPC", 0);
            duplicate.jurors.Add(duplicate.jurors[0].Clone());
            Assert.Throws<ArgumentException>(() => WebJurySentiment.ShiftAllJurorSentiment(duplicate, 1, "invalid", 1));
        }

        private static bool IsNegativeZero(double value) => value == 0 && BitConverter.DoubleToInt64Bits(value) < 0;
        private static void SameState(WebJurySentimentState actual, WebJurySentimentState expected, string label)
        {
            Assert.That(actual.overallSentiment, Is.EqualTo(expected.overallSentiment), label);
            Assert.That(actual.jurors.Count, Is.EqualTo(expected.jurors.Count), label);
            for (int index = 0; index < actual.jurors.Count; index++)
            {
                var a = actual.jurors[index]; var e = expected.jurors[index];
                Assert.That(a.jurorId, Is.EqualTo(e.jurorId), label);
                Assert.That(a.jurorName, Is.EqualTo(e.jurorName), label);
                Assert.That(a.sentiment, Is.EqualTo(e.sentiment), label);
                Assert.That(a.events.Select(item => item.week), Is.EqualTo(e.events.Select(item => item.week)), label);
                Assert.That(a.events.Select(item => item.delta), Is.EqualTo(e.events.Select(item => item.delta)), label);
                Assert.That(a.events.Select(item => item.reason), Is.EqualTo(e.events.Select(item => item.reason)), label);
            }
        }

        [Serializable] public sealed class Goldens { public int schemaVersion; public WebJurySentimentState initial; public LedgerProgram[] programs; }
        [Serializable] public sealed class LedgerProgram { public string name; public LedgerStep[] steps; }
        [Serializable] public sealed class LedgerStep
        {
            public LedgerInput input;
            public WebJurySentimentState expected;
            public bool expectedSameReference, expectedNegativeZeroOverall;
            public string[] expectedNegativeZeroJurorIds;
        }
        [Serializable] public sealed class LedgerInput
        {
            public string operation, jurorId, jurorName, reason;
            public double relationshipScore, delta;
            public int week;
        }
    }
}
