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
    public sealed class WebFinalSpeechParityTests
    {
#if UNITY_5_3_OR_NEWER
        [Test]
        public void FinalSpeechTextContextFragmentsAndDrawScheduleMatchExecutedOriginalSource()
        {
            VerifyFixture(JsonUtility.FromJson<Goldens>(File.ReadAllText(Path.Combine(Application.dataPath,
                "Gamesim/Tests/EditMode/Fixtures/WebFinalSpeechFixtures.json"))));
        }
#endif

        public static void VerifyFixture(Goldens fixture)
        {
            Assert.That(fixture.schemaVersion, Is.EqualTo(1));
            Assert.That(fixture.cases, Has.Length.EqualTo(155));
            foreach (var item in fixture.cases)
            {
                int draws = 0;
                var random = new SeededRandom(item.seed);
                var numerators = new List<double>();
                string actual = WebFinalSpeeches.Generate(item.traits, "Source Speaker", item.context, () =>
                {
                    Assert.That(draws, Is.LessThan(item.expectedDraws), item.name + " consumed an extra draw");
                    double sample = item.useSeeded ? random.NextDouble() : double.Parse(item.sampleTexts[draws], NumberStyles.Float, CultureInfo.InvariantCulture);
                    numerators.Add(sample * 4294967296d); draws++; return sample;
                });
                Assert.That(actual, Is.EqualTo(item.expected), item.name);
                Assert.That(draws, Is.EqualTo(item.expectedDraws), item.name);
                Assert.That(WebFinalSpeeches.TraitFlavor(item.traits), Is.EqualTo(item.expectedFlavor), item.name);
                if (item.useSeeded) Assert.That(numerators, Is.EqualTo(item.sampleNumerators.Select(value => (double)value)), item.name);
            }
        }

        [Test]
        public void NativeSpeechCannotRevealAnUnrelatedAllianceAndUsesOnlyRealContext()
        {
            var state = new EpisodeState { randomState = 123, week = 4 };
            state.contestants.Add(new ContestantState { id = "speaker", name = "NPC Speaker", traits = new List<string> { "Competitive" }, hohWins = 2, vetoWins = 1 });
            state.contestants.Add(new ContestantState { id = "other", name = "Other NPC" });
            state.alliances.Add(new AllianceState { id = "private", name = "UNRELATED SECRET ALLIANCE", members = new List<string> { "other" } });
            state.alliances.Add(new AllianceState { id = "own", name = "Speaker Alliance", active = false, members = new List<string> { "speaker", "other" } });
            // These records deliberately must NOT be reinterpreted as source deals or storylines.
            state.promises.Add(new PromiseState { id = "promise", fromId = "speaker", toId = "other", kind = PromiseKind.Safety });
            state.relationshipArcs.Add(new RelationshipArcState { npcId = "speaker", npcName = "NPC Speaker", arcType = "friendship", intensity = 80 });
            var expectedContext = new WebSpeechContext { allianceNames = new[] { "Speaker Alliance" }, compWins = new WebSpeechWins { hoh = 2, pov = 1, other = 0 }, week = 4 };
            for (uint seed = 0; seed < 100; seed++)
            {
                var expectedRandom = new SeededRandom(seed);
                var actualRandom = new SeededRandom(seed);
                string expected = WebFinalSpeeches.Generate(state.contestants[0].traits, "NPC Speaker", expectedContext, expectedRandom.NextDouble);
                string actual = WebFinalSpeeches.GenerateNative(state, "speaker", actualRandom.NextDouble);
                Assert.That(actual, Is.EqualTo(expected), "Seed " + seed);
                Assert.That(actual, Does.Not.Contain("UNRELATED SECRET ALLIANCE"));
                Assert.That(actualRandom.State, Is.EqualTo(expectedRandom.State));
            }
            Assert.That(WebFinalSpeeches.GenerateNative(state, "speaker", () => 0), Does.Contain("Speaker Alliance"));
            Assert.That(state.randomState, Is.EqualTo(123), "Caller owns the persisted draw stream.");
            Assert.That(state.relationshipArcs[0].intensity, Is.EqualTo(80));
            Assert.That(state.contestants[0].hohWins, Is.EqualTo(2));
            Assert.That(state.promises[0].status, Is.EqualTo(PromiseStatus.Active));
        }

        [Test]
        public void SpeechGenerationLeavesContextAndStatsUntouchedAndEmptyContextConsumesOnlyBaseDraw()
        {
            var story = new WebSpeechStoryline { title = "Story", category = "social_drama", involvedNPCNames = new[] { "Retain Me" } };
            var context = new WebSpeechContext { completedStorylines = new[] { story } };
            string result = WebFinalSpeeches.Generate(new[] { "Social" }, "Speaker", context, () => 0);
            Assert.That(result, Does.Contain("between me and them"));
            Assert.That(story.involvedNPCNames, Is.EqualTo(new[] { "Retain Me" }));
            int draws = 0;
            WebFinalSpeeches.Generate(Array.Empty<string>(), "Speaker", new WebSpeechContext(), () => { draws++; return 0; });
            Assert.That(draws, Is.EqualTo(1));
            Assert.That(WebFinalSpeeches.TraitFlavor(new[] { "", "Strategic" }), Is.EqualTo("social"));
        }

        [Test]
        public void InvalidNativeSpeakerOrRandomSampleCannotInventASpeech()
        {
            Assert.Throws<ArgumentException>(() => WebFinalSpeeches.GenerateNative(new EpisodeState(), "missing", () => 0));
            Assert.Throws<ArgumentNullException>(() => WebFinalSpeeches.GenerateNative(null, "missing", () => 0));
            Assert.Throws<ArgumentNullException>(() => WebFinalSpeeches.Generate(null, "Speaker", null, () => 0));
            Assert.Throws<ArgumentNullException>(() => WebFinalSpeeches.Generate(Array.Empty<string>(), "Speaker", null, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebFinalSpeeches.Generate(Array.Empty<string>(), "Speaker", null, () => 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebFinalSpeeches.Generate(Array.Empty<string>(), "Speaker", null, () => double.NaN));
        }

        [Serializable] public sealed class Goldens { public int schemaVersion; public SpeechCase[] cases; }
        [Serializable] public sealed class SpeechCase
        {
            public string name, expectedFlavor, expected;
            public string[] traits, sampleTexts;
            public bool useSeeded;
            public uint seed;
            public uint[] sampleNumerators;
            public int expectedDraws;
            public WebSpeechContext context;
        }
    }
}
