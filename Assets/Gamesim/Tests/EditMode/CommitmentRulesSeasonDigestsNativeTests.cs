#if !UNITY_5_3_OR_NEWER
// CommitmentRulesSeasonDigests is compiled for Tools/SimulationTests only, so this is too. It lives in a file of its
// own because the digests' file must keep compiling against the recorded build before the commitment rules, which
// has neither the conversation preview nor this test's helpers.
using System.Reflection;
using System.Text;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The digest checkpoint's native-29 arm (DIALOGUE-PLAN D1): a season on dialogue rules 1 has no schema-28 form and
    /// no recorded digest, so its checkpoint is its own JSON, admitted by the schema-29 observer's native contract, and
    /// then every reader the checkpoint reads of any season - none of the legacy removals. Rules 0 keeps the projection
    /// through the retained observers, which the 54 recorded seasons (the explicit digests) hold. Since SCH30 the live state
    /// is at 30: the schema-30 observer's native entry takes it to its 29 (only the pooled three, off, removed; the store
    /// kept), which is the JSON the build that started it wrote, and that is what the checkpoint digests. A season on
    /// dialogue rules 2 (D2) has no 29: the observer's dialogue arm admits it at 30 as it is, and that is what is digested.
    /// </summary>
    public sealed class CommitmentRulesSeasonDigestsNativeTests
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;

        /// <summary>
        /// A season on dialogue rules 1 by the literal (<see cref="DialoguePreviewWitness"/>), the preview's season
        /// until the chip moved to rules 2; the name is kept so the stamped runner lists hold. The chip's rules since
        /// that move, rules 2, are digested by <see cref="ARules2SeasonIsDigestedAtThirtyAsItIsThenByEveryReader"/>.
        /// </summary>
        [Test]
        public void APreviewSeasonIsDigestedAtItsOwnSchemaThenByEveryReader()
        {
            foreach (var state in new[] { DialoguePreviewWitness.Find().Campaign, DialoguePreviewWitness.Find().Conversation })
            {
                Assert.That(state.schemaVersion, Is.EqualTo(30), "The live schema.");
                string before = JObject.FromObject(state).ToString(Formatting.None);
                var at29 = JObject.FromObject(state);
                foreach (string pooled in LegacyDigestSchema30Observer.Pooled)
                {
                    Assert.That(at29[pooled].Type, Is.EqualTo(pooled == "alumniCatalogEdition" ? JTokenType.Null : JTokenType.Integer), pooled);
                    if (at29[pooled].Type == JTokenType.Integer) Assert.That((int)at29[pooled], Is.Zero, pooled + " is off.");
                    at29.Remove(pooled);
                }
                at29["schemaVersion"] = 29;
                var trace = new StringBuilder();
                Invoke("Checkpoint", state, trace);
                var readers = new StringBuilder();
                Invoke("Readers", state, readers);
                Assert.That(readers.Length, Is.GreaterThan(0), "The readers have lines to give.");
                Assert.That(trace.ToString(), Is.EqualTo(at29.ToString(Formatting.None) + "\n" + readers),
                    "The state's JSON at 29, as its build wrote it - schema 30's pooled three, off, removed - then the readers' lines: nothing projected, nothing else removed.");
                Assert.That(JObject.FromObject(state).ToString(Formatting.None), Is.EqualTo(before), "Digesting changes nothing.");
            }
        }

        [Test]
        public void ARules1CheckpointStillRefusesWhatTheNativeContractRefuses()
        {
            var state = DialoguePreviewWitness.Find().Conversation;
            state.dialogue.grants.Add(new DialogueRoleGrantState());
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False);
            // The checkpoint reaches the observer by reflection too, so its refusal arrives wrapped twice.
            var error = Assert.Throws<TargetInvocationException>(() => Invoke("Checkpoint", state, new StringBuilder()));
            Assert.That(error.InnerException, Is.InstanceOf<TargetInvocationException>());
            Assert.That(error.InnerException.InnerException, Is.InstanceOf<System.IO.InvalidDataException>(),
                "The observer's own refusal, never a digest of an invalid season.");
        }

        /// <summary>
        /// The digest checkpoint's dialogue arm (DIALOGUE-PLAN D2): a season on dialogue rules 2 exists only from 30, so it
        /// has no 29 to be taken to. Its checkpoint is its own JSON at 30 - the pooled three at their off literals and kept,
        /// the store kept - admitted by the schema-30 observer's own native entry, then every reader. Played to the campaign,
        /// the conversation and its reply, and held at tuning 0 by hand, as every digest season is built.
        /// </summary>
        [Test]
        public void ARules2SeasonIsDigestedAtThirtyAsItIsThenByEveryReader()
        {
            foreach (string stage in new[] { "campaign", "conversation", "reply" })
            {
                var state = LegacyDigestSchema30ObserverTests.Rules2Season(stage, tuningHeld: true);
                string before = JObject.FromObject(state).ToString(Formatting.None);
                var trace = new StringBuilder();
                Invoke("Checkpoint", state, trace);
                var readers = new StringBuilder();
                Invoke("Readers", state, readers);
                Assert.That(readers.Length, Is.GreaterThan(0), "The readers have lines to give.");
                Assert.That(trace.ToString(), Is.EqualTo(before + "\n" + readers),
                    stage + ": the state's JSON at 30 as it is - nothing projected, nothing removed - then the readers' lines.");
                Assert.That(JObject.FromObject(state).ToString(Formatting.None), Is.EqualTo(before), "Digesting changes nothing.");
            }
        }

        /// <summary>
        /// The dialogue arm's checkpoint refuses an invalid rules-2 season (a role grant) and one on the tuning rules, which
        /// this checkpoint has no arm for, each by the observer's own refusal, wrapped twice by the reflection.
        /// </summary>
        [TestCase("grants")] [TestCase("tuning-1")]
        public void ARules2CheckpointStillRefusesWhatTheNativeContractRefuses(string defect)
        {
            var state = LegacyDigestSchema30ObserverTests.Rules2Season("conversation", tuningHeld: defect != "tuning-1");
            if (defect == "grants")
            {
                state.dialogue.grants.Add(new DialogueRoleGrantState());
                Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False);
            }
            var error = Assert.Throws<TargetInvocationException>(() => Invoke("Checkpoint", state, new StringBuilder()));
            Assert.That(error.InnerException, Is.InstanceOf<TargetInvocationException>());
            Assert.That(error.InnerException.InnerException, Is.InstanceOf<System.IO.InvalidDataException>());
            Assert.That(error.InnerException.InnerException.Message, Does.StartWith(defect == "grants" ? "The current source state is invalid:"
                : "Only a season on dialogue rules 2 alone is admitted natively by the dialogue arm."));
        }

        private static void Invoke(string name, EpisodeState state, StringBuilder trace)
        {
            var method = typeof(CommitmentRulesSeasonDigests).GetMethod(name, Private, null, new[] { typeof(EpisodeState), typeof(StringBuilder) }, null);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(null, new object[] { state, trace });
        }
    }
}
#endif
