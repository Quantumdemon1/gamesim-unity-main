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
    /// through the retained observers, which the 54 recorded seasons (the explicit digests) hold.
    /// </summary>
    public sealed class CommitmentRulesSeasonDigestsNativeTests
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;

        [Test]
        public void APreviewSeasonIsDigestedAtItsOwnSchemaThenByEveryReader()
        {
            foreach (var state in new[] { DialoguePreviewWitness.Find().Campaign, DialoguePreviewWitness.Find().Conversation })
            {
                string before = JObject.FromObject(state).ToString(Formatting.None);
                var trace = new StringBuilder();
                Invoke("Checkpoint", state, trace);
                var readers = new StringBuilder();
                Invoke("Readers", state, readers);
                Assert.That(readers.Length, Is.GreaterThan(0), "The readers have lines to give.");
                Assert.That(trace.ToString(), Is.EqualTo(before + "\n" + readers),
                    "The state's own JSON at 29, as it is, then the readers' lines: nothing projected, nothing removed.");
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

        private static void Invoke(string name, EpisodeState state, StringBuilder trace)
        {
            var method = typeof(CommitmentRulesSeasonDigests).GetMethod(name, Private, null, new[] { typeof(EpisodeState), typeof(StringBuilder) }, null);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(null, new object[] { state, trace });
        }
    }
}
#endif
