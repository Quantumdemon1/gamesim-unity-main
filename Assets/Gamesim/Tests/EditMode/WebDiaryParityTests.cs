using System;
using System.Globalization;
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
    public sealed class WebDiaryParityTests
    {
#if UNITY_5_3_OR_NEWER
        [Test]
        public void DiaryCatalogTriggersPersonaAndEffectiveChoicePlansMatchOriginalSource()
        {
            VerifyFixture(JsonConvert.DeserializeObject<Goldens>(File.ReadAllText(Path.Combine(Application.dataPath,
                "Gamesim/Tests/EditMode/Fixtures/WebDiaryFixtures.json"))));
        }
#endif

        public static void VerifyFixture(Goldens fixture)
        {
            Assert.That(fixture.schemaVersion, Is.EqualTo(1));
            Assert.That(fixture.events, Has.Length.EqualTo(80));
            Assert.That(fixture.triggers, Has.Length.EqualTo(96));
            Assert.That(fixture.personas, Has.Length.EqualTo(128));
            Assert.That(fixture.personas.Sum(program => program.steps.Length), Is.EqualTo(150));
            Assert.That(fixture.plans, Has.Length.EqualTo(22));
            SamePersona(WebDiaryRoom.CreateInitialPersonaState(), fixture.initialPersona, "initial");
            foreach (var item in fixture.events)
                SameEvent(WebDiaryRoom.GenerateEvent(item.trigger, item.week, item.evictedName, item.playerWasInvolved, item.isNominee), item.expected);
            foreach (var item in fixture.triggers)
            {
                int draws = 0;
                bool actual = WebDiaryRoom.ShouldTrigger(item.trigger, item.week, item.lastDiaryRoomWeek, () =>
                {
                    draws++; return double.Parse(item.rollText, NumberStyles.Float, CultureInfo.InvariantCulture);
                });
                Assert.That(actual, Is.EqualTo(item.expected), item.trigger + ":" + item.rollText);
                Assert.That(draws, Is.EqualTo(item.expectedDraws), item.trigger + " draw count");
            }
            foreach (var program in fixture.personas)
            {
                var state = program.initial.Clone();
                foreach (var step in program.steps)
                {
                    var before = state.Clone();
                    var actual = WebDiaryRoom.ApplyPersonaChoice(state, step.choice, step.week);
                    SamePersona(actual, step.expected, program.name);
                    SamePersona(state, before, program.name + " input mutation");
                    state = actual;
                }
            }
            foreach (var item in fixture.plans)
            {
                var actual = WebDiaryRoom.PlanChoice(item.inputPersona, item.diaryEvent, item.choiceId, item.currentWeek, item.currentPhase);
                SamePersona(actual.persona, item.expected.persona, item.name);
                foreach (var field in typeof(WebDiaryChoicePlan).GetFields().Where(field => field.Name != "persona"))
                    Assert.That(field.GetValue(actual), Is.EqualTo(field.GetValue(item.expected)), item.name + ":" + field.Name);
                Assert.That(7 + actual.socialBonusDelta.GetValueOrDefault(), Is.EqualTo(item.expectedStoredSocialBonus), item.name);
                Assert.That(11 + actual.competitionBonusDelta.GetValueOrDefault(), Is.EqualTo(item.expectedStoredCompetitionBonus), item.name);
            }
        }

        [Test]
        public void DeclaredReputationAndStealthEffectsDoNotBecomeEffectiveDispatches()
        {
            var diaryEvent = WebDiaryRoom.GenerateEvent("post_eviction", 3, "Maya");
            var calculated = diaryEvent.choices.Single(choice => choice.id == "calculated");
            Assert.That(calculated.effects.stealthModifier, Is.EqualTo(3));
            Assert.That(calculated.effects.reputationDelta, Is.EqualTo(0));
            var actual = WebDiaryRoom.PlanChoice(null, diaryEvent, "calculated", 4, "SocialInteraction");
            Assert.That(actual.dispatchTypes, Is.EqualTo(new[] { "SET_PLAYER_PERSONA", "SET_LAST_DIARY_WEEK", "LOG_EVENT" }));
            Assert.That(actual.socialBonusDelta, Is.Null);
            Assert.That(actual.competitionBonusDelta, Is.Null);
            Assert.That(actual.juryDelta, Is.Null);
            Assert.That(actual.lastDiaryRoomWeek, Is.EqualTo(4), "Source uses dismissal week, not the displayed event's prior week.");
            Assert.That(actual.persona.history.Single().week, Is.EqualTo(4));
            Assert.That(diaryEvent.week, Is.EqualTo(3));
        }

        [Test]
        public void InitialTieOrderDiffersFromRestoredInsertionOrderAndSourceNeverShufflesIt()
        {
            var persona = WebDiaryRoom.CreateInitialPersonaState();
            persona.scores.Reverse();
            foreach (var score in persona.scores) score.score = score.persona == "Neutral" ? 1 : 2;
            var choice = WebDiaryRoom.GenerateEvent("post_nomination", 2).choices.Single(item => item.persona == "Neutral");
            var result = WebDiaryRoom.ApplyPersonaChoice(persona, choice, 2);
            Assert.That(result.current, Is.EqualTo("Social Butterfly"));
            Assert.That(result.scores.Select(score => score.persona), Is.EqualTo(persona.scores.Select(score => score.persona)));
            Assert.That(persona.history, Is.Empty);
        }

        [Test]
        public void CatalogFactoriesAreDetachedAndUnusedContextDoesNotAlterTheQuestion()
        {
            var original = WebDiaryRoom.GenerateEvent("post_eviction", 3, "Name $& {token}", false, false);
            var same = WebDiaryRoom.GenerateEvent("post_eviction", 3, "Name $& {token}", true, true);
            SameEvent(original, same);
            Assert.That(original.narrative, Does.Contain("Name $& {token}"));
            original.choices[0].text = "changed";
            original.choices[0].effects.juryDelta = 999;
            Assert.That(same.choices[0].text, Is.Not.EqualTo("changed"));
            Assert.That(same.choices[0].effects.juryDelta, Is.EqualTo(5));
            Assert.That(WebDiaryRoom.GenerateEvent("post_eviction", 3, "").choices.Count, Is.EqualTo(2), "Missing evictee falls through to generic branch.");
        }

        [Test]
        public void InvalidPlansAreRejectedAndQuotaBranchesDoNotRequestRandomSamples()
        {
            Assert.That(WebDiaryRoom.ShouldTrigger("post_eviction", 3, null, null), Is.True);
            Assert.That(WebDiaryRoom.ShouldTrigger("post_nomination", 3, 3, null), Is.False);
            Assert.That(WebDiaryRoom.ShouldTrigger("unknown", 3, null, null), Is.False);
            Assert.Throws<ArgumentNullException>(() => WebDiaryRoom.ShouldTrigger("mid_week", 3, null, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebDiaryRoom.ShouldTrigger("mid_week", 3, null, () => 1));
            Assert.Throws<ArgumentException>(() => WebDiaryRoom.PlanChoice(null, WebDiaryRoom.GenerateEvent("mid_week", 3), "forged", 3, "Social"));
            var state = WebDiaryRoom.CreateInitialPersonaState();
            state.scores.Add(state.scores[0].Clone());
            Assert.Throws<ArgumentException>(() => WebDiaryRoom.ApplyPersonaChoice(state, WebDiaryRoom.GenerateEvent("mid_week", 3).choices[0], 3));
        }

        private static void SamePersona(WebPersonaState actual, WebPersonaState expected, string label)
        {
            Assert.That(actual.current, Is.EqualTo(expected.current), label);
            Assert.That(actual.scores.Select(score => score.persona), Is.EqualTo(expected.scores.Select(score => score.persona)), label);
            Assert.That(actual.scores.Select(score => score.score), Is.EqualTo(expected.scores.Select(score => score.score)), label);
            Assert.That(actual.history.Select(item => item.persona), Is.EqualTo(expected.history.Select(item => item.persona)), label);
            Assert.That(actual.history.Select(item => item.week), Is.EqualTo(expected.history.Select(item => item.week)), label);
        }

        private static void SameEvent(WebDiaryEvent actual, WebDiaryEvent expected)
        {
            Assert.That(actual.id, Is.EqualTo(expected.id));
            Assert.That(actual.week, Is.EqualTo(expected.week));
            Assert.That(actual.trigger, Is.EqualTo(expected.trigger));
            Assert.That(actual.narrative, Is.EqualTo(expected.narrative));
            Assert.That(actual.choices.Count, Is.EqualTo(expected.choices.Count));
            for (int index = 0; index < actual.choices.Count; index++)
            {
                foreach (var field in typeof(WebDiaryChoice).GetFields().Where(field => field.Name != "effects"))
                    Assert.That(field.GetValue(actual.choices[index]), Is.EqualTo(field.GetValue(expected.choices[index])));
                foreach (var field in typeof(WebDiaryEffects).GetFields())
                    Assert.That(field.GetValue(actual.choices[index].effects), Is.EqualTo(field.GetValue(expected.choices[index].effects)));
            }
        }

        // These fixtures use Json.NET, including nullable fields, not Unity serialization.
        public sealed class Goldens
        {
            public int schemaVersion;
            public WebPersonaState initialPersona;
            public EventCase[] events;
            public TriggerCase[] triggers;
            public PersonaProgram[] personas;
            public PlanCase[] plans;
        }
        public sealed class EventCase { public string trigger, evictedName; public int week; public bool playerWasInvolved, isNominee; public WebDiaryEvent expected; }
        public sealed class TriggerCase { public string trigger, rollText; public int week, expectedDraws; public int? lastDiaryRoomWeek; public bool expected; }
        public sealed class PersonaProgram { public string name; public WebPersonaState initial; public PersonaStep[] steps; }
        public sealed class PersonaStep { public WebDiaryChoice choice; public int week; public WebPersonaState expected; }
        public sealed class PlanCase
        {
            public string name, choiceId, currentPhase;
            public WebPersonaState inputPersona;
            public WebDiaryEvent diaryEvent;
            public int currentWeek, expectedStoredSocialBonus, expectedStoredCompetitionBonus;
            public WebDiaryChoicePlan expected;
        }
    }
}
