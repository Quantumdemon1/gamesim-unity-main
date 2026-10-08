using System;
using System.IO;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The pure digest's schema-28 observer (WAVE-D-NPC-PACTS-PLAN §0.3, X3): the live trace bound to
    /// its own state, every Wave D field present and inert, removed, the fixed27 neutral inverse, and
    /// the hand-off to the schema-27 field checks. Witnesses are the shared lawful public ones; altered
    /// JSON/state controls are detached diagnostics after a proved valid baseline.
    /// </summary>
    public sealed class LegacyDigestSchema28ObserverTests
    {
        [TestCase(0, "fresh")] [TestCase(1, "fresh")] [TestCase(2, "fresh")]
        [TestCase(2, "events")] [TestCase(1, "broken-deal")] [TestCase(2, "broken-deal")]
        public void LiveTracesProjectThroughExact27ToTheLegacy26Trace(int mode, string kind)
        {
            var state = LegacyDigestSchema27TestStates.Witness(mode, kind);
            var trace = LegacyDigestSchema27TestStates.Trace(state);
            string stateBefore = Text(state), before = Text(trace);
            var field = LegacyDigestSchema28Observer.CheckFieldObserver(state, trace);
            Assert.That(Text(field), Is.EqualTo(Text(LegacyDigestSchema27TestStates.WithoutGetters(trace))));
            Assert.That(field["schemaVersion"].Value<int>(), Is.EqualTo(28));
            var projected = LegacyDigestSchema28Observer.ProjectTrace(state, trace);
            var expected = LegacyDigestSchema27TestStates.Neutral26(LegacyDigestSchema27TestStates.Neutral27(trace));
            Assert.That(Text(projected), Is.EqualTo(Text(expected)), "Compact JSON pins every retained property and list position.");
            Assert.That(JToken.DeepEquals(projected["Active"], trace["Active"]), Is.True, "Getters stay in the legacy trace.");
            foreach (string name in LegacyDigestSchema28Observer.RootFields) Assert.That(projected.Property(name), Is.Null);
            foreach (string name in LegacyDigestSchema28Observer.NpcSocialFields) Assert.That(((JObject)projected["npcSocial"]).Property(name), Is.Null);
            Assert.That(((JObject)projected["ledger"]).Property("plans"), Is.Null);
            var restored = LegacyDigestSchema27TestStates.Restore28(LegacyDigestSchema27TestStates.Restore27(projected));
            Assert.That(Text(restored), Is.EqualTo(before), "The inert literals restore the original compact order too.");
            projected["contestants"][0]["name"] = "Detached projection"; field["contestants"][0]["name"] = "Detached field view";
            Assert.That(Text(trace), Is.EqualTo(before));
            Assert.That(Text(state), Is.EqualTo(stateBefore));
        }

        [TestCase("allianceLeakRulesStartWeek")] [TestCase("pactPlanRulesStartWeek")] [TestCase("allWeekRulesStartWeek")]
        public void EveryStartWeekMustBePresentLiteralZero(string name)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "promise");
            foreach (string defect in new[] { "missing", "null", "string", "fractional", "bool", "one", "negative" })
                Refused(state, trace => Scalar(trace, name, defect));
        }

        [TestCase("beatWeek")] [TestCase("beatWindow")] [TestCase("beatsFired")] [TestCase("beatSeats")]
        public void EveryBeatScalarMustBePresentAndInert(string name)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "promise");
            foreach (string defect in new[] { "missing", "null", "string", "fractional", "bool", "one", "negative", "zero" })
            {
                // beatWindow's inert value is -1, every other scalar's 0.
                if ((name == "beatWindow" && defect == "negative") || (name != "beatWindow" && defect == "zero")) continue;
                Refused(state, trace => Scalar((JObject)trace["npcSocial"], name, defect));
            }
        }

        [TestCase("npcSocial", "beatPlan")] [TestCase("npcSocial", "acts")] [TestCase("ledger", "plans")]
        public void EveryWaveDListMustBePresentAndEmpty(string owner, string name)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "promise");
            foreach (string defect in new[] { "missing", "null", "object", "string", "row" })
                Refused(state, trace =>
                {
                    var holder = (JObject)trace[owner];
                    switch (defect)
                    {
                        case "missing": holder.Remove(name); break;
                        case "null": holder[name] = JValue.CreateNull(); break;
                        case "object": holder[name] = new JObject(); break;
                        case "string": holder[name] = "[]"; break;
                        default: holder[name] = new JArray(name == "beatPlan" ? (JToken)state.contestants.First(c => !c.isPlayer).id : new JObject()); break;
                    }
                });
        }

        /// <summary>The serializer declares Wave D's fields last; anywhere else the order inverse refuses the trace.</summary>
        [TestCase("root")] [TestCase("npcSocial")] [TestCase("ledger")]
        public void WaveDFieldsStandWhereTheSerializerPutsThem(string owner)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "promise");
            Refused(state, trace =>
            {
                var holder = owner == "root" ? trace : (JObject)trace[owner];
                string name = owner == "root" ? "allWeekRulesStartWeek" : owner == "npcSocial" ? "acts" : "plans";
                var moved = holder.Property(name); moved.Remove(); holder.Properties().First().AddAfterSelf(moved);
            });
        }

        [TestCase("header-27")] [TestCase("header-29")] [TestCase("header-text")] [TestCase("header-missing")]
        [TestCase("unknown-root")] [TestCase("unknown-npc-social")] [TestCase("unknown-ledger")] [TestCase("unknown-act-version")]
        public void HeadersAndUnknownMembersAreNeverProjectedOrPruned(string defect)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "events");
            Refused(state, trace =>
            {
                switch (defect)
                {
                    case "header-27": trace["schemaVersion"] = 27; break;
                    case "header-29": trace["schemaVersion"] = 29; break;
                    case "header-text": trace["schemaVersion"] = "28"; break;
                    case "header-missing": trace.Remove("schemaVersion"); break;
                    case "unknown-root": trace["allWeekRulesVersion"] = 0; break;
                    case "unknown-npc-social": trace["npcSocial"]["beatCursor"] = 0; break;
                    case "unknown-ledger": trace["ledger"]["planCalls"] = new JArray(); break;
                    default: trace["actRulesVersion"] = JValue.CreateNull(); break;
                }
            });
        }

        /// <summary>
        /// A valid season with a Wave D start week set is a season that plays it, whatever its fields
        /// hold so far: legacy goldens never activate it, so the observer refuses rather than erase it.
        /// </summary>
        [TestCase("allianceLeakRulesStartWeek")] [TestCase("pactPlanRulesStartWeek")] [TestCase("allWeekRulesStartWeek")]
        public void AnEnabledButStillEmptyDesignIsNotLegacyParity(string name)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "promise");
            typeof(EpisodeState).GetField(name).SetValue(state, 1);
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
            RefusedUnchanged(state, LegacyDigestSchema27TestStates.Trace(state));
        }

        [TestCase("session")] [TestCase("rng")] [TestCase("invalid")]
        public void OnlyThisValidStatesOwnTraceIsObserved(string defect)
        {
            var state = LegacyDigestSchema27TestStates.Witness(2, "promise");
            var other = state.Clone();
            if (defect == "session") other.sessionId += "-another-valid-session";
            else if (defect == "rng") other.randomState = other.randomState == uint.MaxValue ? 0 : other.randomState + 1;
            if (defect == "invalid")
            {
                state.npcSocial.beatsFired = 1;
                Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.False);
                Assert.That(reason, Does.Contain("all-week"));
                RefusedUnchanged(state, LegacyDigestSchema27TestStates.Trace(state));
                return;
            }
            LegacyDigestSchema27TestStates.Valid(other);
            var otherTrace = LegacyDigestSchema27TestStates.Trace(other);
            Assert.DoesNotThrow(() => LegacyDigestSchema28Observer.CheckFieldObserver(other, otherTrace));
            RefusedUnchanged(state, otherTrace);
        }

        private static void Scalar(JObject holder, string name, string defect)
        {
            switch (defect)
            {
                case "missing": holder.Remove(name); break;
                case "null": holder[name] = JValue.CreateNull(); break;
                case "string": holder[name] = name == "beatWindow" ? "-1" : "0"; break;
                case "fractional": holder[name] = name == "beatWindow" ? -1.0 : 0.0; break;
                case "bool": holder[name] = false; break;
                case "one": holder[name] = 1; break;
                case "negative": holder[name] = -1; break;
                case "zero": holder[name] = 0; break;
                default: Assert.Fail("Unknown scalar control."); break;
            }
        }

        /// <summary>A proved baseline, then the defect on a detached copy of its own trace.</summary>
        private static void Refused(EpisodeState state, Action<JObject> defect)
        {
            var trace = LegacyDigestSchema27TestStates.Trace(state);
            Assert.DoesNotThrow(() => LegacyDigestSchema28Observer.CheckFieldObserver(state, trace));
            defect(trace);
            RefusedUnchanged(state, trace);
        }

        private static void RefusedUnchanged(EpisodeState state, JObject trace)
        {
            string stateBefore = Text(state), traceBefore = Text(trace);
            Assert.Throws<InvalidDataException>(() => LegacyDigestSchema28Observer.CheckFieldObserver(state, trace));
            Assert.Throws<InvalidDataException>(() => LegacyDigestSchema28Observer.ProjectTrace(state, trace));
            Assert.That(Text(trace), Is.EqualTo(traceBefore));
            Assert.That(Text(state), Is.EqualTo(stateBefore));
        }

        private static string Text(EpisodeState state) => LegacyDigestSchema27TestStates.Text(state);
        private static string Text(JToken token) => LegacyDigestSchema27TestStates.Text(token);
    }
}
