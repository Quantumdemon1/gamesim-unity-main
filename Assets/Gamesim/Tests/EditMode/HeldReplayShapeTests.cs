#if !UNITY_5_3_OR_NEWER
// The held replays' shape guard: Tools/SimulationTests only, beside the replays it guards (they are compiled out of the
// editor's assemblies, as Unity's batch runner executes [Explicit] tests).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Gamesim.Simulation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// A held replay cannot rot silently. Each held replay ([Explicit], run by name) hashes its seasons' states against
    /// digests recorded at an older schema, so every checkpoint must take the live state through the observer for the live
    /// schema to the shape the digests were recorded in. When a schema bump added saved fields and one replay still hashed
    /// the raw live JSON, every one of its seasons "moved" although none had: the replay stopped guarding anything, and
    /// nothing outside the replay's own long run said so. These cases are ordinary and quick: each held replay's own first
    /// season goes through its own checkpoint, and the JSON it digests must stand below the live schema.
    /// </summary>
    public sealed class HeldReplayShapeTests
    {
        private const BindingFlags Statics = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

        /// <summary>Every held replay with a checkpoint, and how to build its own first season.</summary>
        private static readonly Dictionary<string, Func<EpisodeState>> Guarded = new Dictionary<string, Func<EpisodeState>>
        {
            { nameof(RolesSeasonDigests), () => Season(typeof(RolesSeasonDigests), 6, 1u) },
            { nameof(CommitmentRulesSeasonDigests), () => Season(typeof(CommitmentRulesSeasonDigests), "director", 6, 1u, true) },
            { nameof(ModeOneSeasonDigests), () => Season(typeof(ModeOneSeasonDigests), "director", 6, 1u) },
        };

        private static IEnumerable<string> GuardedNames() => Guarded.Keys;

        [TestCaseSource(nameof(GuardedNames))]
        public void AHeldReplayDigestsTheStateBelowTheLiveSchema(string replay)
        {
            var type = typeof(HeldReplayShapeTests).Assembly.GetType("Gamesim.Tests.EditMode." + replay, true);
            var checkpoint = type.GetMethod("Checkpoint", Statics, null, new[] { typeof(EpisodeState), typeof(StringBuilder) }, null);
            Assert.That(checkpoint, Is.Not.Null, replay + " writes each checkpoint's text through Checkpoint(EpisodeState, StringBuilder).");
            var season = Guarded[replay]();
            Assert.That(season.schemaVersion, Is.EqualTo(EpisodeState.LiveSchema), "The season is a live state.");

            var trace = new StringBuilder();
            checkpoint.Invoke(null, new object[] { season, trace });
            string first = trace.ToString().Split('\n')[0];
            var digested = JObject.Parse(first);
            string live = JObject.FromObject(season).ToString(Newtonsoft.Json.Formatting.None);
            string why = replay + " hashes the live schema's JSON: its recorded digests were taken at an older schema, so its checkpoint "
                + "must go through LegacyDigestSchema" + EpisodeState.LiveSchema + "Observer to the shape it was recorded in.";
            Assert.That(first, Is.Not.EqualTo(live), why);
            // The oldest recorded shapes predate the header; a header kept must stand below the live schema.
            var header = digested["schemaVersion"];
            if (header != null)
            {
                Assert.That(header.Type, Is.EqualTo(JTokenType.Integer), replay + " digests a state's JSON first.");
                Assert.That((long)header, Is.LessThan(EpisodeState.LiveSchema), why);
            }
        }

        [Test]
        public void EveryHeldReplayWithACheckpointIsGuarded()
        {
            var replays = typeof(HeldReplayShapeTests).Assembly.GetTypes()
                .Where(t => t.Namespace == "Gamesim.Tests.EditMode" && t.Name.EndsWith("SeasonDigests", StringComparison.Ordinal))
                .Where(t => t.GetMethods(Statics | BindingFlags.DeclaredOnly)
                    .Any(m => m.Name == "Checkpoint" && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(EpisodeState)))
                .Select(t => t.Name).OrderBy(name => name, StringComparer.Ordinal).ToList();
            Assert.That(replays, Is.Not.Empty, "The held replays are compiled into this assembly.");
            Assert.That(replays, Is.SubsetOf(Guarded.Keys), "A held replay with a checkpoint is listed here, so its shape is guarded.");
        }

        [Test]
        public void TheLiveSchemaHasItsDigestObserver()
        {
            var observer = typeof(HeldReplayShapeTests).Assembly.GetType("Gamesim.Tests.EditMode.LegacyDigestSchema" + EpisodeState.LiveSchema + "Observer");
            Assert.That(observer, Is.Not.Null, "Schema " + EpisodeState.LiveSchema + " has its digest observer, which every held replay goes through.");
            var live = observer.GetField("Live", Statics);
            Assert.That(live, Is.Not.Null, "The observer names the live schema it reads.");
            Assert.That(live.GetRawConstantValue(), Is.EqualTo(EpisodeState.LiveSchema));
        }

        private static EpisodeState Season(Type replay, params object[] arguments)
        {
            var season = replay.GetMethod("Season", Statics, null, arguments.Select(a => a.GetType()).ToArray(), null);
            Assert.That(season, Is.Not.Null, replay.Name + " builds its own seasons.");
            return (EpisodeState)season.Invoke(null, arguments);
        }
    }
}
#endif
