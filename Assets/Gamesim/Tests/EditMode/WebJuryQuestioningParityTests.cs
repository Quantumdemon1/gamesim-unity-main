using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;
#if UNITY_5_3_OR_NEWER
using System.IO;
using UnityEngine;
#endif

namespace Gamesim.Tests.EditMode
{
    public sealed class WebJuryQuestioningParityTests
    {
#if UNITY_5_3_OR_NEWER
        [Test]
        public void QuestionsChoicesOutcomesAndDrawScheduleMatchExecutedOriginalComponent()
        {
            VerifyFixture(JsonUtility.FromJson<Goldens>(File.ReadAllText(Path.Combine(Application.dataPath,
                "Gamesim/Tests/EditMode/Fixtures/WebJuryQuestioningFixtures.json"))));
        }
#endif

        // Also callable from a managed-only harness during a Unity build, without loading the Editor.
        public static void VerifyFixture(Goldens fixture)
        {
            Assert.That(fixture.schemaVersion, Is.EqualTo(1));
            Assert.That(fixture.pairCases, Has.Length.EqualTo(184));
            Assert.That(fixture.toneCases, Has.Length.EqualTo(7));
            Assert.That(fixture.finalistCases, Has.Length.EqualTo(30));
            Assert.That(fixture.jurorOptionCases, Has.Length.EqualTo(7));
            Assert.That(fixture.fallbackCases, Has.Length.EqualTo(10));
            foreach (var item in fixture.pairCases)
                SameFields(WebJuryQuestioning.GetResponsePair(item.traits, item.index, Roll(item.flipRollText)), item.expected, item.name);
            foreach (var item in fixture.toneCases)
                Assert.That(WebJuryQuestioning.GetQuestionType(Roll(item.rollText)), Is.EqualTo(item.expected), item.rollText);

            SeededRandom random = null;
            foreach (var item in fixture.finalistCases)
            {
                if (item.index == 0) random = new SeededRandom(item.seed);
                Assert.That(random, Is.Not.Null, item.name);
                var numerators = new List<double>();
                var actual = WebJuryQuestioning.CreateFinalistQuestion(item.traits, item.index, () =>
                {
                    double sample = random.NextDouble(); numerators.Add(sample * 4294967296d); return sample;
                });
                SameFields(actual, item.expected, item.name);
                Assert.That(numerators.Count, Is.EqualTo(item.expectedDraws), item.name);
                // JSON decimal-to-double parsing cannot weaken exact uint RNG parity.
                Assert.That(numerators, Is.EqualTo(item.sampleNumerators.Select(value => (double)value)), item.name);
                Assert.That(WebJuryQuestioning.MatchesSource(actual, item.traits, item.index), Is.True, item.name);
                foreach (var choice in item.choices)
                {
                    var impact = WebJuryQuestioning.EvaluateChoice(actual, choice.choice, "juror-" + item.index,
                        "Juror " + item.index, "player-finalist");
                    SameFields(impact, choice.expected, item.name + "-" + choice.choice);
                    Assert.That(choice.expectedDraws, Is.Zero, "The original choice callback dispatches intent without RNG.");
                }
            }
            foreach (var item in fixture.jurorOptionCases)
            {
                var actual = WebJuryQuestioning.GetJurorQuestionOptions(item.index);
                Assert.That(actual.Length, Is.EqualTo(item.expected.Length));
                for (int index = 0; index < actual.Length; index++) SameFields(actual[index], item.expected[index], "juror-options-" + item.index);
                Assert.That(item.expectedDraws, Is.Zero);
            }
            foreach (var item in fixture.fallbackCases)
            {
                int draws = 0;
                var actual = WebJuryQuestioning.CreateFallbackAnswer(() => { draws++; return Roll(item.rollText); });
                SameFields(actual, item.expected, "fallback-" + item.rollText);
                Assert.That(draws, Is.EqualTo(item.expectedDraws));
                Assert.That(item.expectedDispatchCount, Is.Zero, "Player-juror questions must not fabricate a relationship bonus.");
            }
        }

        [Test]
        public void ReturnedObjectsAreDetachedAndSavedQuestionsCanBeCheckedWithoutRerolling()
        {
            var traits = new[] { "Loyal", "Strategic" };
            var question = WebJuryQuestioning.CreateFinalistQuestion(traits, 1, () => 0.5);
            Assert.That(WebJuryQuestioning.MatchesSource(question, traits, 1), Is.True);
            Assert.That(question.correctIs, Is.EqualTo("B"));
            question.optionA = "forged choice";
            Assert.That(WebJuryQuestioning.MatchesSource(question, traits, 1), Is.False);
            var fresh = WebJuryQuestioning.CreateFinalistQuestion(traits, 1, () => 0.5);
            Assert.That(WebJuryQuestioning.MatchesSource(fresh, traits, 1), Is.True);
            Assert.That(fresh.optionA, Is.Not.EqualTo(question.optionA));
            fresh.correctIs = "C";
            Assert.That(WebJuryQuestioning.MatchesSource(fresh, traits, 1), Is.False);
            var options = WebJuryQuestioning.GetJurorQuestionOptions(0);
            options[0].text = "forged question";
            Assert.That(WebJuryQuestioning.GetJurorQuestionOptions(0)[0].text, Is.Not.EqualTo(options[0].text));
            Assert.That(traits, Is.EqualTo(new[] { "Loyal", "Strategic" }));
        }

        [Test]
        public void PersistedRandomStateResumesNextQuestionExactlyIncludingZeroSeed()
        {
            var initial = new SeededRandom(0);
            WebJuryQuestioning.CreateFinalistQuestion(new[] { "Competitive" }, 0, initial.NextDouble);
            var resumed = new SeededRandom(initial.State);
            var uninterrupted = WebJuryQuestioning.CreateFinalistQuestion(new[] { "Strategic" }, 1, initial.NextDouble);
            var actual = WebJuryQuestioning.CreateFinalistQuestion(new[] { "Strategic" }, 1, resumed.NextDouble);
            SameFields(actual, uninterrupted, "resumed");
            Assert.That(resumed.State, Is.EqualTo(initial.State));
        }

        [Test]
        public void InvalidInputsAreRejectedWithoutConsumingRandomnessOrTrustingChoiceTokens()
        {
            int draws = 0;
            Func<double> random = () => { draws++; return 0.5; };
            Assert.Throws<ArgumentOutOfRangeException>(() => WebJuryQuestioning.CreateFinalistQuestion(null, -1, random));
            Assert.That(draws, Is.Zero);
            Assert.Throws<ArgumentNullException>(() => WebJuryQuestioning.CreateFinalistQuestion(null, 0, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebJuryQuestioning.GetQuestionType(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebJuryQuestioning.GetQuestionType(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebJuryQuestioning.GetResponsePair(null, 0, -0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebJuryQuestioning.GetJurorQuestionOptions(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebJuryQuestioning.CreateFallbackAnswer(() => double.PositiveInfinity));
            var question = WebJuryQuestioning.CreateFinalistQuestion(null, 0, random);
            Assert.Throws<ArgumentException>(() => WebJuryQuestioning.EvaluateChoice(question, "C", "juror", "Juror", "player"));
            Assert.Throws<ArgumentException>(() => WebJuryQuestioning.EvaluateChoice(question, "A", "player", "Juror", "player"));
            Assert.That(WebJuryQuestioning.MatchesSource(null, null, 0), Is.False);
        }

        private static double Roll(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        private static void SameFields<T>(T actual, T expected, string label)
        {
            foreach (var field in typeof(T).GetFields()) Assert.That(field.GetValue(actual), Is.EqualTo(field.GetValue(expected)), label + ":" + field.Name);
        }

        [Serializable] public sealed class Goldens
        {
            public int schemaVersion;
            public PairCase[] pairCases;
            public ToneCase[] toneCases;
            public FinalistCase[] finalistCases;
            public JurorOptionCase[] jurorOptionCases;
            public FallbackCase[] fallbackCases;
        }
        [Serializable] public sealed class PairCase
        {
            public string name, flipRollText;
            public string[] traits;
            public int index, expectedDraws;
            public WebJuryResponsePair expected;
        }
        [Serializable] public sealed class ToneCase { public string rollText, expected; public int expectedDraws; }
        [Serializable] public sealed class FinalistCase
        {
            public string name;
            public uint seed;
            public int index, expectedDraws;
            public string[] traits;
            public uint[] sampleNumerators;
            public WebJuryQuestion expected;
            public ChoiceCase[] choices;
        }
        [Serializable] public sealed class ChoiceCase { public string choice; public WebJuryChoiceImpact expected; public int expectedDraws; }
        [Serializable] public sealed class JurorOptionCase { public int index, expectedDraws; public WebJuryQuestionOption[] expected; }
        [Serializable] public sealed class FallbackCase { public string rollText; public int expectedDraws, expectedDispatchCount; public WebJuryAnswer expected; }
    }
}
